using System.Globalization;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.MailIntake;

/// <summary>Was über den Absender einer Nachricht bekannt ist; leere Angaben zählen nicht.</summary>
/// <param name="SenderEmail">Absenderadresse der Mail (bei Treatwell die Adresse des Gasts).</param>
/// <param name="SenderPhone">Absendernummer bei SMS, WhatsApp und Anruf (bei Treatwell die Nummer des Gasts).</param>
/// <param name="ExtractedPhone">Von der KI aus Text oder Signatur gelesene Telefonnummer.</param>
/// <param name="ExcludeCustomerId">Dieser Kunde ist es nicht (die Rezeption hat die Zuordnung gelöst).</param>
internal sealed record CustomerMatchRequest(
    string? SenderEmail,
    string? SenderPhone,
    string? ExtractedPhone,
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate = null,
    Guid? ExcludeCustomerId = null);

/// <summary>
/// Ergebnis des Abgleichs: ein sicherer Treffer (Stufe 1) oder höchstens drei Vorschläge. Nur der sichere Treffer darf ohne die
/// Rezeption zugeordnet werden.
/// </summary>
internal sealed record CustomerMatch(Customer? Certain, CustomerCandidateDto? CertainInfo, IReadOnlyList<CustomerCandidateDto> Candidates)
{
    /// <summary>Für den Dubletten-Hinweis: auch der sichere Treffer ist ein Vorschlag, zuerst.</summary>
    public IReadOnlyList<CustomerCandidateDto> Everyone => CertainInfo is null ? Candidates : [CertainInfo, .. Candidates];
}

/// <summary>
/// Ordnet eine Nachricht (oder die Angaben eines neuen Kunden) dem Kundenstamm des Spas zu, gestuft nach Sicherheit
/// (ADR 0061, ADR 0008: die KI schlägt vor, der Mensch bestätigt):
/// 1 = Mailadresse (Haupt- oder weitere) oder Absendernummer gleich, 2 = vom Gast genannte Nummer passt, 3 = Name plus zweites Merkmal
/// (Ziffern der Nummer, Geburtstag, Termin in den letzten 24 Monaten), 4 = nur ähnlicher Name. Gesperrte Kunden („Nicht mehr
/// buchen“) bleiben Kandidaten und sind im Grund gekennzeichnet.
/// </summary>
internal sealed class CustomerMatcher(AispaDbContext db, TenantContext tenants, TimeProvider time)
{
    public const int MaxCandidates = 3;

    private const int RecentMonths = 24;
    private const string DefaultCountry = "AT";

    public async Task<CustomerMatch> MatchAsync(CustomerMatchRequest request, CancellationToken ct)
    {
        var country = await db.Tenants.AsNoTracking().Where(t => t.Id == tenants.TenantId).Select(t => t.Country).FirstOrDefaultAsync(ct) ?? DefaultCountry;
        var zone = LocalTime.Zone(await db.Locations.AsNoTracking().Select(l => l.TimeZone).FirstOrDefaultAsync(ct) ?? "Europe/Vienna");
        var email = Clean(request.SenderEmail)?.ToLowerInvariant();
        var senderPhone = PhoneNumbers.ToE164(request.SenderPhone, country);
        var extractedPhone = PhoneNumbers.ToE164(request.ExtractedPhone, country);
        // Titel („Dr.“, „MSc“) aus Mail oder Signatur zählen beim Vergleich nicht.
        var (first, last) = (Fold(PersonNames.WithoutTitles(request.FirstName)), Fold(PersonNames.WithoutTitles(request.LastName)));
        var exclude = request.ExcludeCustomerId;

        var sure = new Dictionary<Guid, string>();
        if (email is { Length: > 0 })
        {
            var pattern = email.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            var ids = await db.Customers.AsNoTracking().Where(c => c.DeletedAt == null && c.Email != null && EF.Functions.ILike(c.Email, pattern)).Select(c => c.Id)
                .Union(db.CustomerEmails.AsNoTracking()
                    .Where(e => e.NormalizedAddress == email && db.Customers.Any(c => c.Id == e.CustomerId && c.DeletedAt == null)).Select(e => e.CustomerId))
                .ToListAsync(ct);
            foreach (var id in ids)
            {
                sure[id] = "gleiche Mailadresse";
            }
        }

        // Der Stamm wird einmal schlank geladen und im Speicher verglichen: Schreibweisen von Nummern und Namen lassen sich in SQL nicht
        // zuverlässig vereinheitlichen. Für Spas mit höchstens 50 Mitarbeitenden bleibt das klein.
        var everyone = await db.Customers.AsNoTracking().Where(c => c.DeletedAt == null)
            .Select(c => new Person(c.Id, c.FirstName, c.LastName, c.Phone, c.BirthDate, c.PreferredName)).ToListAsync(ct);
        var extractedMatches = new HashSet<Guid>();
        foreach (var person in everyone.Where(p => !string.IsNullOrWhiteSpace(p.Phone)))
        {
            var stored = PhoneNumbers.ToE164(person.Phone, country);
            if (stored.Length == 0)
            {
                continue;
            }

            if (stored == senderPhone)
            {
                sure.TryAdd(person.Id, "gleiche Telefonnummer");
            }
            else if (stored == extractedPhone)
            {
                extractedMatches.Add(person.Id);
            }
        }

        if (exclude is { } excluded)
        {
            sure.Remove(excluded);
            extractedMatches.Remove(excluded);
        }

        if (sure.Count == 1)
        {
            var id = sure.Keys.Single();
            var certain = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == id, ct);
            return new CustomerMatch(certain, new CustomerCandidateDto(id, certain.InHouseName, 1, sure[id]), []);
        }

        // Ab hier ist niemand sicher: Namen vergleichen und Vorschläge sammeln.
        var contact = Digits4(senderPhone.Length > 0 ? senderPhone : extractedPhone);
        var named = new Dictionary<Guid, NameMatch>();
        if (last.Length > 0)
        {
            foreach (var person in everyone.Where(p => p.Id != exclude))
            {
                if (Compare(first, last, person) is var match and not NameMatch.None)
                {
                    named[person.Id] = match;
                }
            }
        }

        var known = new HashSet<Guid>([.. sure.Keys, .. extractedMatches, .. named.Keys]);
        var customers = everyone.Where(p => known.Contains(p.Id)).ToDictionary(p => p.Id);

        var visits = await VisitsAsync([.. known], zone, ct);
        var blocked = await BlockedAsync([.. known], ct);

        var candidates = new List<(CustomerCandidateDto Dto, DateTimeOffset? Last)>();
        foreach (var id in known)
        {
            var customer = customers[id];
            visits.TryGetValue(id, out var visit);
            var nameMatch = named.GetValueOrDefault(id);
            var extras = new List<string>();
            var secondFeature = false;
            if (contact.Length == 4 && Digits4(PhoneNumbers.ToE164(customer.Phone, country)) == contact)
            {
                extras.Add($"Telefon endet auf {contact}");
                secondFeature = true;
            }

            if (request.BirthDate is { } birth && customer.BirthDate == birth)
            {
                extras.Add("Geburtstag stimmt");
                secondFeature = true;
            }

            if (visit is not null)
            {
                extras.Add(visit.Text);
                secondFeature |= visit.IsRecent;
            }

            var (stage, lead) =
                sure.TryGetValue(id, out var why) ? (1, why + ", mehrere Kunden teilen sie")
                : extractedMatches.Contains(id) ? (2, "Telefonnummer aus der Nachricht passt")
                : nameMatch == NameMatch.Exact && secondFeature ? (3, "gleicher Name")
                : (4, nameMatch == NameMatch.Exact ? "gleicher Name" : "ähnlicher Name");
            var parts = new List<string> { lead };
            if (stage is 1 or 2 && nameMatch != NameMatch.None)
            {
                parts.Add(nameMatch == NameMatch.Exact ? "gleicher Name" : "ähnlicher Name");
            }

            parts.AddRange(extras.Where(e => !parts.Contains(e)));
            if (blocked.Contains(id))
            {
                parts.Add("„Nicht mehr buchen“ ist gesetzt");
            }

            candidates.Add((new CustomerCandidateDto(id, PersonNames.InHouse(customer.FirstName, customer.LastName, customer.PreferredName), stage,
                string.Join(", ", parts)), visit?.At));
        }

        var top = candidates.OrderBy(c => c.Dto.Stage).ThenByDescending(c => c.Last ?? DateTimeOffset.MinValue)
            .ThenBy(c => c.Dto.Name, StringComparer.Ordinal).Take(MaxCandidates).Select(c => c.Dto).ToList();
        return new CustomerMatch(null, null, top);
    }

    private sealed record Visit(DateTimeOffset At, string Text, bool IsRecent);

    /// <summary>Letzter Besuch (sonst nächster Termin) je Kunde; „kürzlich“ heißt: ein Termin in den letzten 24 Monaten oder danach.</summary>
    private async Task<Dictionary<Guid, Visit>> VisitsAsync(List<Guid> ids, TimeZoneInfo zone, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var since = now.AddMonths(-RecentMonths);
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => ids.Contains(a.CustomerId) && a.Start >= since && a.Status != AppointmentStatus.Cancelled)
            .Select(a => new { a.CustomerId, a.Start, a.Status })
            .ToListAsync(ct);
        var result = new Dictionary<Guid, Visit>();
        foreach (var group in appointments.GroupBy(a => a.CustomerId))
        {
            var past = group.Where(a => a.Start <= now && a.Status != AppointmentStatus.NoShow).Select(a => (DateTimeOffset?)a.Start).Max();
            var upcoming = group.Where(a => a.Start > now).Select(a => (DateTimeOffset?)a.Start).Min();
            string Date(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, zone).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            if (past is { } visited)
            {
                result[group.Key] = new Visit(visited, $"letzter Besuch {Date(visited)}", true);
            }
            else if (upcoming is { } next)
            {
                result[group.Key] = new Visit(next, $"nächster Termin {Date(next)}", true);
            }
        }

        return result;
    }

    private async Task<HashSet<Guid>> BlockedAsync(List<Guid> ids, CancellationToken ct) =>
        [.. await db.CustomerFlags.AsNoTracking().Where(f => ids.Contains(f.CustomerId) && f.RemovedAt == null && f.Kind == CustomerFlagKind.DoNotBook)
            .Select(f => f.CustomerId).Distinct().ToListAsync(ct)];

    internal enum NameMatch
    {
        None,
        Similar,
        Exact,
    }

    private sealed record Person(Guid Id, string FirstName, string LastName, string? Phone, DateOnly? BirthDate, string? PreferredName = null);

    /// <summary>Gültiger Vorname oder Rufname: „Steffi Huber“ passt genauso wie „Stefanie Huber“; das bessere Ergebnis zählt.</summary>
    private static NameMatch Compare(string first, string last, Person customer)
    {
        var legal = Compare(first, last, customer.FirstName, customer.LastName);
        return string.IsNullOrWhiteSpace(customer.PreferredName) || legal == NameMatch.Exact
            ? legal
            : (NameMatch)Math.Max((int)legal, (int)Compare(first, last, customer.PreferredName, customer.LastName));
    }

    private static NameMatch Compare(string first, string last, string firstName, string lastName)
    {
        var (customerFirst, customerLast) = (Fold(firstName), Fold(lastName));
        if (customerLast.Length == 0)
        {
            // Nur ein Name hinterlegt: gilt der als Nachname, wenn der Vorname fehlt.
            (customerFirst, customerLast) = ("", customerFirst);
        }

        if (customerLast == last)
        {
            if (first == customerFirst && first.Length > 0)
            {
                return NameMatch.Exact;
            }

            return first.Length == 0 || customerFirst.Length == 0 || customerFirst.StartsWith(first, StringComparison.Ordinal)
                || first.StartsWith(customerFirst, StringComparison.Ordinal)
                ? NameMatch.Similar
                : NameMatch.None;
        }

        // Tippfehler: nur bei vorhandenem Vornamen und längeren Namen, damit „Li Wu“ nicht zu jedem passt.
        if (first.Length == 0 || customerFirst.Length == 0)
        {
            return NameMatch.None;
        }

        var (a, b) = ($"{first} {last}", $"{customerFirst} {customerLast}");
        var allowed = Math.Min(a.Length, b.Length) >= 14 ? 2 : Math.Min(a.Length, b.Length) >= 6 ? 1 : 0;
        return allowed > 0 && Distance(a, b, allowed) <= allowed ? NameMatch.Similar : NameMatch.None;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Digits4(string number) => PhoneNumbers.LastDigits(number, 4) is { Length: 4 } d ? d : "";

    /// <summary>
    /// Name zum Vergleichen: klein, ä/ö/ü als ae/oe/ue, ß als ss, Akzente weg, Bindestriche und Satzzeichen als Leerzeichen.
    /// „Müller“, „Mueller“ und „MÜLLER“ ergeben dasselbe, „Zoë“ und „Zoe“ auch.
    /// </summary>
    internal static string Fold(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        var text = name.Normalize(NormalizationForm.FormC).ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal).Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal).Replace("ß", "ss", StringComparison.Ordinal);
        var plain = new StringBuilder();
        foreach (var ch in text.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            plain.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', plain.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Bearbeitungsabstand (Einfügen, Löschen, Ersetzen); bricht ab, sobald er über <paramref name="limit"/> liegt.</summary>
    internal static int Distance(string a, string b, int limit)
    {
        if (Math.Abs(a.Length - b.Length) > limit)
        {
            return limit + 1;
        }

        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var row = new int[b.Length + 1];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                row[j] = Math.Min(Math.Min(row[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            previous = row;
        }

        return previous[b.Length];
    }
}
