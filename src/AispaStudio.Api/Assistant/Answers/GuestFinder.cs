using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Der Gast einer Frage (Ä315), gemeinsam für alle Antwortgeber zu Gästen: der aus einer Auswahl gewählte Gast, sonst der Name der Frage (unscharf, <see cref="NameMatch"/>),
/// sonst der Gast, auf den sich die Frage bezieht (offener Termin, Kundenkarte, zuletzt genannter Gast). Passen mehrere Gäste, bietet <see cref="ChoiceAsync"/> sie zur Auswahl an.
/// </summary>
internal sealed class GuestFinder(AispaDbContext db)
{
    private const int MaxGuests = 5;

    /// <summary>Ein Gast, wie die Antwort ihn braucht: Name im Haus, Ort der Anschrift und Anrede.</summary>
    internal sealed record Guest(Guid Id, string FirstName, string LastName, string? PreferredName, string? City, CustomerSalutation Salutation)
    {
        public string Name => PersonNames.InHouse(FirstName, LastName, PreferredName);
    }

    /// <summary>Höchstens fünf passende Gäste und wie viele es insgesamt sind; <see cref="Block"/> statt Gästen, wenn keiner genannt ist oder keiner passt.</summary>
    internal sealed record Found(IReadOnlyList<Guest> Guests, int Total, AssistantBlock? Block);

    public async Task<Found> FindAsync(BusinessQuestion question, AnswerContext context, string source, CancellationToken ct)
    {
        var name = NameMatch.ParseName(question.Subject);
        var guests = context.ChosenGuestId is { } chosen ? await ByIdAsync(chosen, ct)
            : !name.IsEmpty ? await ByNameAsync(name.Words, NameMatch.ParseName(question.Text).Salutation ?? name.Salutation, ct)
            : context.ReferredGuestId is { } referred ? await ByIdAsync(referred, ct)
            : null;
        if (guests is { Count: 0 } && context.ReferredGuestId is { } fallback && BusinessIntents.RefersToGuest(question.Text))
        {
            // „wann hat sie übermorgen ihren termin“: ein Wort, das kein Gast ist, verdrängt den Bezug nicht.
            guests = await ByIdAsync(fallback, ct);
        }

        if (guests is null)
        {
            return new([], 0, Blocks.NotFound(question.Topic, "Welcher Gast ist gemeint? Bitte den Namen nennen, zum Beispiel „Frau Müller“.", source));
        }

        if (guests.Count == 0)
        {
            return new([], 0, string.IsNullOrWhiteSpace(question.Subject)
                ? Blocks.NotFound(question.Topic, "Diesen Gast gibt es im Kundenstamm nicht mehr.", source)
                : Blocks.NotFound(question.Topic, $"Keinen Gast „{question.Subject.Trim()}“ gefunden.", source) with
                {
                    FollowUps = [new AssistantFollowUp("Gast suchen", $"Kunde {question.Subject.Trim()}")],
                });
        }

        return new([.. guests.Take(MaxGuests)], guests.Count, null);
    }

    /// <summary>Ob es zum Namen einen Gast gibt, mit denselben Regeln wie <see cref="FindAsync"/> (ganze Namen, sonst ein Tippfehler).</summary>
    public async Task<bool> ExistsAsync(string? name, CancellationToken ct) =>
        NameMatch.ParseName(name) is { IsEmpty: false } parsed && (await ByNameAsync(parsed.Words, parsed.Salutation, ct)).Count > 0;

    /// <summary>Mehrere Gäste passen: Name, Ort und nächster Termin, je Gast ein Knopf, der die Frage für ihn stellt.</summary>
    public async Task<AssistantBlock> ChoiceAsync(BusinessQuestion question, Found found, AnswerContext context, string source, CancellationToken ct)
    {
        var ids = found.Guests.Select(g => g.Id).ToList();
        var since = context.Now.AddHours(-1);
        var upcoming = await Visible(db.Appointments.AsNoTracking(), context)
            .Where(a => ids.Contains(a.CustomerId) && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.CheckedIn) && a.Start >= since)
            .GroupBy(a => a.CustomerId)
            .Select(g => new { CustomerId = g.Key, Start = g.Min(a => a.Start) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Start, ct);
        string NextOf(Guest guest) => upcoming.TryGetValue(guest.Id, out var start) ? When(start, context) : "kein Termin geplant";
        var text = $"Der Name passt zu {found.Total} Gästen. Bitte den Gast wählen."
            + (found.Total > found.Guests.Count ? " Es gibt weitere Treffer; bitte den Namen genauer angeben." : "");
        return Blocks.Answer(question.Topic, text, source,
            Blocks.Table(["Gast", "Ort", "Nächster Termin"], found.Guests.Select(g => (IReadOnlyList<string>)[g.Name, g.City ?? "–", NextOf(g)])),
            followUps: [.. found.Guests.Select(g => new AssistantFollowUp(g.City is null ? g.Name : $"{g.Name}, {g.City}", question.Text, g.Id))]);
    }

    /// <summary>
    /// Termine, die die Person sieht: nur an ihren Standorten, und Therapeut:innen (ohne Buchen und ohne Finanzen) nur die eigenen, wie im Tagesüberblick und bei
    /// <see cref="DayAnswers"/> (Ä317).
    /// </summary>
    public static IQueryable<Appointment> Visible(IQueryable<Appointment> appointments, AnswerContext context)
    {
        var visible = context.Access.LocationIds.ToList();
        var all = context.Access.AllLocations;
        var query = appointments.Where(a => all || visible.Contains(a.LocationId));
        if (context.OwnOnly)
        {
            var own = context.OwnEmployeeId;
            query = query.Where(a => a.EmployeeId == own);
        }

        return query;
    }

    /// <summary>Tag und Uhrzeit in Ortszeit, „Montag, 08.03.2027, 14:00 Uhr“.</summary>
    public static string When(DateTimeOffset start, AnswerContext context) => $"{AnswerDates.Long(Local(start, context))}, {context.Time(start)} Uhr";

    public static DateOnly Local(DateTimeOffset at, AnswerContext context) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, context.Zone).DateTime);

    private async Task<List<Guest>> ByIdAsync(Guid id, CancellationToken ct) =>
        await Project(db.Customers.AsNoTracking().Where(c => c.Id == id && c.DeletedAt == null)).ToListAsync(ct);

    /// <summary>
    /// Gäste zum Namen: Kandidaten mit den Wörtern in Vor-, Nach- oder Rufname aus der Datenbank, davon nur die ganzen Namen (<see cref="NameMatch.Fit"/>, „lena“ ist
    /// nicht „Helena“); ohne Treffer die Namen ähnlicher Länge im Speicher mit einem Tippfehler und ohne Umlaute; erst danach der erste Name allein (ein zweites Wort,
    /// das kein Name ist). Die Anrede der Frage grenzt ein, wenn sie zu einem Treffer passt.
    /// </summary>
    private async Task<List<Guest>> ByNameAsync(IReadOnlyList<string> words, CustomerSalutation? salutation, CancellationToken ct)
    {
        words = [.. words.Where(w => NameMatch.Key(w).Length >= 2)];
        if (words.Count == 0)
        {
            return [];
        }

        var found = await ExactAsync(words, ct);
        if (found.Count == 0)
        {
            found = await SimilarAsync(words, ct);
        }

        if (found.Count == 0 && words.Count > 1)
        {
            found = await ExactAsync(words.Take(1).ToList(), ct);
        }

        return NameMatch.BySalutation(found, salutation, g => g.Salutation);
    }

    private async Task<List<Guest>> ExactAsync(IReadOnlyList<string> words, CancellationToken ct)
    {
        var candidates = await Project(Matching([.. words.Select(Like)]).OrderBy(c => c.LastName).ThenBy(c => c.FirstName)).ToListAsync(ct);
        return NameMatch.Best(candidates, g => NameMatch.Fit(words, g.FirstName, g.LastName, g.PreferredName) == 0 ? 0 : null);
    }

    /// <summary>Namen mit höchstens zwei Buchstaben Längenunterschied („ue“ für „ü“), geprüft im Speicher auf einen Tippfehler.</summary>
    private async Task<List<Guest>> SimilarAsync(IReadOnlyList<string> words, CancellationToken ct)
    {
        var lengths = words.Select(w => NameMatch.Key(w).Length).ToList();
        var (shortest, longest) = (lengths.Min() - 2, lengths.Max() + 2);
        var similar = await Project(db.Customers.AsNoTracking().Where(c => c.DeletedAt == null)
                .Where(c => (c.LastName.Length >= shortest && c.LastName.Length <= longest)
                    || (c.FirstName.Length >= shortest && c.FirstName.Length <= longest)
                    || (c.PreferredName != null && c.PreferredName.Length >= shortest && c.PreferredName.Length <= longest))
                .OrderBy(c => c.LastName).ThenBy(c => c.FirstName))
            .ToListAsync(ct);
        return NameMatch.Best(similar, g => NameMatch.Fit(words, g.FirstName, g.LastName, g.PreferredName));
    }

    private static IQueryable<Guest> Project(IQueryable<Customer> customers) =>
        customers.Select(c => new Guest(c.Id, c.FirstName, c.LastName, c.PreferredName, c.City, c.Salutation));

    private IQueryable<Customer> Matching(List<string> likes)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.DeletedAt == null);
        foreach (var like in likes)
        {
            query = query.Where(c => EF.Functions.ILike(c.FirstName, like) || EF.Functions.ILike(c.LastName, like)
                || (c.PreferredName != null && EF.Functions.ILike(c.PreferredName, like)));
        }

        return query;
    }

    private static string Like(string value) =>
        "%" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
}
