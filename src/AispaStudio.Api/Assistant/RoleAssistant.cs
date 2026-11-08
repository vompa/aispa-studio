using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using AispaStudio.Help;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant;

/// <summary>
/// KI-Assistent je Rolle. Die KI versteht nur die Frage (<see cref="IAssistantClassifier"/>, <see cref="IBookingAssistant"/>);
/// freie Zeiten, Kunden und Kennzahlen kommen aus Kalender und Kassa. Er schlägt nur vor: gebucht wird erst, wenn die Person
/// einen Vorschlag in der Oberfläche bestätigt, und nur mit ihren Rechten.
/// </summary>
internal sealed class RoleAssistant(
    AispaDbContext db,
    BookingService booking,
    IAssistantClassifier classifier,
    IBookingAssistant reader,
    BusinessAnswers answers,
    GuestFinder guests,
    IAnswerPhraser phraser,
    AssistantOrders orders,
    TimeProvider time)
{
    private const int MaxSlots = 6;
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public static string Disclosure => "Antwort eines KI-Assistenten. Zahlen und Zeiten stammen aus Kalender und Kassa; bitte vor dem Buchen kurz prüfen.";

    /// <summary>Hinweis unter Antworten aus den Daten des Spas (ADR 0072): die Angaben stehen fest, die KI erkennt die Frage und formuliert höchstens den Satz.</summary>
    public static string BlockDisclosure => "Antwort eines KI-Assistenten. Die Angaben stammen aus den Daten des Spas (Quelle unten), nicht von der KI; bei Zweifeln bitte dort nachsehen.";

    public async Task<AssistantAnswer> AskStaffAsync(UserAccess access, AssistantQuestion question, CancellationToken ct)
    {
        var audience = access.Has(AccessRight.ViewFinance) ? AssistantAudience.Management
            : access.Has(AccessRight.Book) ? AssistantAudience.Reception
            : AssistantAudience.Therapist;
        AssistantAnswer Answer(string text, IReadOnlyList<AssistantFigure>? figures = null, IReadOnlyList<AssistantSuggestion>? suggestions = null,
            IReadOnlyList<AssistantHelpLink>? help = null, AssistantBlock? block = null, string? unanswered = null) =>
            new(audience, text, figures ?? [], suggestions ?? [], block is null ? Disclosure : BlockDisclosure, help ?? [], block, unanswered);
        var helpContext = new HelpContext(question.Surface, RoleKey(access.Role), question.Page);

        var locations = (await db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct)).Where(l => access.CanSee(l.Id)).ToList();
        var location = locations.FirstOrDefault(l => l.Id == question.LocationId) ?? locations.FirstOrDefault();
        if (location is null)
        {
            return Answer("Für dieses Konto ist kein Standort freigegeben.");
        }

        var now = time.GetUtcNow();
        var zone = LocalTime.Zone(location.TimeZone);
        var today = LocalTime.LocalDate(now, zone);

        // Aufträge („buche …“, „sag … ab“) vor allem anderen: sie öffnen eine Maske vorausgefüllt, gespeichert wird erst dort per Knopf (Ä319).
        if (AssistantCommands.Detect(question.Question) is { } command)
        {
            var referredGuest = question.GuestId is null ? await ReferredGuestAsync(access, question.Reference, ct) : null;
            var referredAppointment = await ReferredAppointmentAsync(access, question.Reference, ct);
            var order = await orders.AnswerAsync(command, question.Question,
                new AnswerContext(access, location, zone, now, today, question.GuestId, referredGuest, referredAppointment), ct);
            return Answer(order.Text, suggestions: order.Suggestions, block: order.Block);
        }

        // Zuerst die festen Regeln (billig, immer dieselbe Antwort mit und ohne KI-Schlüssel), dann erst das Sprachmodell. Ein Wort ohne Anrede ist nur ein Name, wenn es
        // den Gast gibt; „rechnung splitten“ geht sonst wie jede nicht erkannte Frage an den Klassifizierer und die Hilfe.
        var intent = BusinessIntents.Detect(question.Question, today);
        if (intent is not null && question.GuestId is null && BusinessIntents.NamesGuestOnlyByWord(intent, question.Question) && !await guests.ExistsAsync(intent.Subject, ct))
        {
            intent = null;
        }

        intent ??= await classifier.ClassifyAsync(question.Question, now, location.TimeZone, ct);
        if (BusinessIntents.Question(intent, question.Question, today) is { } business)
        {
            var referred = question.GuestId is null && GuestTopics.Contains(business.Topic) ? await ReferredGuestAsync(access, question.Reference, ct) : null;
            var appointment = business.Topic == BusinessTopic.CancellationFee ? await ReferredAppointmentAsync(access, question.Reference, ct) : null;
            var block = await answers.AnswerAsync(business, new AnswerContext(access, location, zone, now, today, question.GuestId, referred, appointment), ct);
            var text = await phraser.PhraseAsync(block, question.Question, ct);
            // „Historie …“ (Ä317): der Verlauf des Gastes ist die Antwort, die Hilfe zum Verlauf steht nur ergänzend darunter (höchstens zwei klare Treffer).
            IReadOnlyList<AssistantHelpLink> help = business.Topic == BusinessTopic.GuestHistory && block.Kind == AssistantBlockKind.Answer
                ? [.. HelpAnswer(question.Question, question.Question, helpContext, StrongHit).Links.Take(2)]
                : [];
            return Answer(text ?? block.Text, help: help, block: text is null ? block : block with { Text = text });
        }

        if (intent.Task == AssistantTask.Business)
        {
            // Das Modell hat „Betriebsfrage“ gesagt, aber kein Thema genannt, das es gibt: wie eine nicht erkannte Frage behandeln.
            intent = intent with { Task = AssistantTask.Unknown };
        }

        // Das Backoffice hat keinen Kalender (ADR 0070): außer Auswertungen und Handbuch beantwortet der Assistent ihm nichts aus dem Kalender.
        if (!access.Has(AccessRight.ViewCalendar) && intent.Task is AssistantTask.FreeSlots or AssistantTask.FindCustomer or AssistantTask.DayOverview)
        {
            var denied = Blocks.Denied(intent.Task.ToString(), "Dieser Zugang nutzt den Assistenten für Auswertungen und Fragen zur Bedienung; Kalender, Team und Gäste sieht er nicht.");
            return Answer(denied.Text, block: denied);
        }

        if (intent.Task == AssistantTask.Help || intent.Task == AssistantTask.Unknown)
        {
            // Ohne erkannte Absicht nur bei einem klaren Treffer, sonst geht es wie bisher mit freien Zeiten weiter.
            var unknown = intent.Task == AssistantTask.Unknown;
            var (helpText, links) = HelpAnswer(intent.HelpQuery ?? question.Question, question.Question, helpContext, unknown ? StrongHit : 0);
            if (links.Count > 0)
            {
                return Answer(helpText, help: links);
            }

            if (!unknown)
            {
                // Hilfe-Frage ohne Treffer im Handbuch: für die Liste „Offene Fragen an die Hilfe“ (Slice 56).
                return Answer(helpText, unanswered: await UnansweredQuestions.CleanAsync(db, question.Question, ct));
            }
        }

        switch (intent.Task)
        {
            case AssistantTask.Figures when !access.Has(AccessRight.ViewAmountReports):
                return Answer("Auswertungen zu Umsatz und Auslastung sehen der Inhaber, Manager und das Backoffice sowie Personen mit dem Recht „Berichte mit Beträgen“. Der Assistent zeigt freie Zeiten, Kunden oder den Tagesüberblick.");
            case AssistantTask.Figures:
                var (figures, summary) = await FiguresAsync(location, intent.From ?? today.AddDays(-6), intent.Until ?? today, ct);
                return Answer(summary, figures);
            case AssistantTask.FindCustomer when !access.Has(AccessRight.Book):
                return Answer("Kontaktdaten von Kunden sieht die Rezeption. Die eigenen Termine zeigt der Tagesüberblick.");
            case AssistantTask.FindCustomer:
                var (customerText, customerSuggestions, customerList) = await FindCustomersAsync(intent.CustomerQuery, zone, ct);
                return Answer(customerText, suggestions: customerSuggestions, block: customerList);
            case AssistantTask.DayOverview:
                var day = intent.From ?? today;
                var own = audience == AssistantAudience.Therapist ? access.EmployeeId ?? Guid.Empty : (Guid?)null;
                var overview = await OverviewAsync(location, day, own, AnswerDates.Part(question.Question), ct);
                return Answer(overview.Text, block: overview);
            default:
                var found = access.Has(AccessRight.ViewCalendar)
                    ? await SlotsAsync(question.Question, location, access.Has(AccessRight.Book), intent.Task == AssistantTask.Unknown, ct)
                    : null;
                if (found is { } slotAnswer)
                {
                    return Answer(slotAnswer.Text, suggestions: slotAnswer.Suggestions, block: slotAnswer.List);
                }

                // Ohne Kalender (Backoffice) keine Fragen zu Tag und Team und kein Hinweis, wer zum Team gehört.
                var noIntent = NoIntent(audience, access.Has(AccessRight.ViewCalendar)
                    ? await UnderstoodAsync(question.Question, location, today, ct)
                    : new UnderstoodParts(null, []));
                return Answer(noIntent.Text, block: noIntent, unanswered: await UnansweredQuestions.CleanAsync(db, question.Question, ct));
        }
    }

    /// <summary>
    /// Der Gast, den „sie“, „er“ oder „der Gast“ meint (Ä315): der Gast des offenen Termins, wenn der Zugang dessen Standort sieht, sonst die offene Kundenkarte, sonst
    /// der zuletzt genannte Gast der Leiste. Kunden sind über den Mandantenfilter getrennt; nur Gäste, die es gibt, zählen.
    /// </summary>
    private async Task<Guid?> ReferredGuestAsync(UserAccess access, AssistantReference? reference, CancellationToken ct)
    {
        if (reference?.AppointmentId is { } appointmentId
            && await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId).Select(a => new { a.CustomerId, a.LocationId }).FirstOrDefaultAsync(ct) is { } open
            && access.CanSee(open.LocationId))
        {
            return open.CustomerId;
        }

        foreach (var id in new[] { reference?.CustomerId, reference?.LastGuestId })
        {
            if (id is { } guest && await db.Customers.AsNoTracking().AnyAsync(c => c.Id == guest, ct))
            {
                return guest;
            }
        }

        return null;
    }

    /// <summary>Themen zu einem Gast: ohne Namen meint die Frage den Gast, auf den sie sich bezieht (Ä315, Ä317).</summary>
    private static readonly HashSet<BusinessTopic> GuestTopics =
    [
        BusinessTopic.GuestLastVisit, BusinessTopic.GuestNextVisit, BusinessTopic.GuestHistory, BusinessTopic.GuestBalance, BusinessTopic.GuestAnamnesis,
        BusinessTopic.CancellationFee,
    ];

    /// <summary>Der offene Termin der Frage (Ä317), wenn der Zugang dessen Standort sieht; sonst <c>null</c>.</summary>
    private async Task<Guid?> ReferredAppointmentAsync(UserAccess access, AssistantReference? reference, CancellationToken ct) =>
        reference?.AppointmentId is { } appointmentId
        && await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId).Select(a => (Guid?)a.LocationId).FirstOrDefaultAsync(ct) is { } locationId
        && access.CanSee(locationId)
            ? appointmentId
            : null;

    /// <summary>
    /// „Wie mache ich …?“ aus dem Handbuch: die passenden Abläufe mit ihrem ersten Satz. Die Schritte und Bilder zeigt die
    /// Oberfläche aus ihrer eingebauten Hilfe; die KI schreibt keine Anleitung, sie findet nur die richtige Stelle.
    /// </summary>
    internal static (string Text, IReadOnlyList<AssistantHelpLink> Links) HelpAnswer(string query, string question, HelpContext context, double minScore = 0)
    {
        var hits = HelpSearch.Search(HelpLibrary.Topics, query, context, 3);
        if (hits.Count == 0 && query != question)
        {
            hits = HelpSearch.Search(HelpLibrary.Topics, question, context, 3);
        }

        if (hits.Count > 0 && hits[0].Score < minScore)
        {
            return ("", []);
        }

        if (hits.Count == 0)
        {
            if (minScore > 0)
            {
                return ("", []);
            }

            // Allgemeine Frage wie „Was kann ich hier machen?“: die Erklärung zur aktuellen Seite.
            return context.Page is { } page && HelpLibrary.ForPage(page) is { } pageTopic
                ? ($"Das geht auf dieser Seite: {pageTopic.Summary}", [new(pageTopic.Id, null, pageTopic.Title, pageTopic.Summary)])
                : ("Dazu habe ich im Handbuch nichts gefunden. Unter „Hilfe“ sind alle Abläufe nach Bereichen geordnet; vielleicht hilft ein anderes Wort.", []);
        }

        var links = hits.Select(h => new AssistantHelpLink(h.Topic.Id, h.Section, h.Title, h.Excerpt)).ToList();
        var first = hits[0];
        // Die Schritte zeigt die Oberfläche darunter aus der Hilfe; der Text nennt nur, wo es steht.
        var text = $"So geht es: {first.Title}.";
        return (hits.Count > 1 ? $"{text} Weitere passende Themen stehen darunter." : text, links);
    }

    /// <summary>Mindestwert, ab dem eine Frage ohne erkannte Absicht aus dem Handbuch beantwortet wird (Titel oder Stichwort getroffen).</summary>
    private const double StrongHit = 10;

    internal static string RoleKey(StaffRole role) => role switch
    {
        StaffRole.Owner => "inhaber",
        StaffRole.GeneralManager => "manager",
        StaffRole.Manager => "leitung",
        StaffRole.Backoffice => "backoffice",
        StaffRole.Reception => "rezeption",
        _ => "therapeut",
    };

    /// <summary>Kunde in der Online-Buchung: bis zu drei freie Zeiten zum Wunsch, nur online buchbare Behandlungen.</summary>
    public async Task<OnlineAssistantAnswer?> AskCustomerAsync(Guid locationId, string question, OnlinePortal portal, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var treatments = await OnlineBookingEndpoints.BookableTreatmentsAsync(db, location.Id, ct);
        var analysis = await reader.AnalyzeAsync(Request(question, time.GetUtcNow(), location, treatments, []), ct);
        var language = GuestLanguage.Normalize(analysis.Language);
        var treatment = treatments.FirstOrDefault(t => t.Id == analysis.TreatmentId);
        if (treatment is null)
        {
            return new OnlineAssistantAnswer(CustomerText(language, "treatment", string.Join(", ", treatments.Select(t => t.Name)), null),
                language, [], CustomerDisclosure(language));
        }

        var (first, last, earliest) = await portal.BookableRangeAsync(location.Id, zone, ct);
        var wishes = analysis.Wishes.Where(w => w.Date >= first && w.Date <= last).ToList();
        var slots = await FreeAsync(location, treatment.Id, null, wishes.Count > 0 ? wishes : NextDays(first, 7), earliest, 3, ct);
        var suggestions = slots.Select(s => new OnlineSuggestedTime(treatment.Id, treatment.Name, s.Start)).ToList();
        var text = suggestions.Count == 0
            ? CustomerText(language, "none", treatment.Name, null)
            : CustomerText(language, "slots", treatment.Name, string.Join(", ", suggestions.Select(s => When(s.Start, zone, language))));
        return new OnlineAssistantAnswer(text, language, suggestions, CustomerDisclosure(language));
    }

    /// <summary>
    /// Keine Absicht erkannt und im Handbuch nichts Passendes: „Dazu habe ich nichts gefunden“ (ADR 0072). Nennt die Frage einen Tag oder eine Person des Teams, sagt der
    /// Text das, und die Angebote sind Fragen dazu (Ä316), aufgefüllt bis drei mit den Fragen der Rolle; ohne Erkanntes bleiben es die zwei Fragen der Rolle.
    /// Das Handbuch bleibt die Rückfallebene davor; hier ist sonst nichts mehr zu zeigen.
    /// </summary>
    private static AssistantBlock NoIntent(AssistantAudience audience, UnderstoodParts understood)
    {
        AssistantFollowUp[] offers = audience switch
        {
            AssistantAudience.Management => [new("Wie lange haben wir heute offen?", "Wie lange haben wir heute offen?"), new("Top-Behandlungen diesen Monat", "Top-Behandlungen diesen Monat")],
            AssistantAudience.Reception => [new("Wer arbeitet morgen?", "Wer arbeitet morgen?"), new("Welche Aktionen laufen gerade?", "Welche Rabatte oder Aktionen laufen gerade?")],
            _ => [new("Wer arbeitet morgen?", "Wer arbeitet morgen?"), new("Was steht heute an?", "Was steht heute an?")],
        };
        var named = understood.Questions();
        var text = named.Count == 0
            ? "Dazu habe ich nichts gefunden. Meinen Sie vielleicht eine dieser Fragen?"
            : $"Dazu habe ich nichts gefunden. Erkannt: {string.Join(", ", understood.Parts)}. Meinen Sie vielleicht eine dieser Fragen?";
        List<AssistantFollowUp> followUps = named.Count == 0
            ? [.. offers]
            : [.. named.Select(q => new AssistantFollowUp(q, q)).Concat(offers).DistinctBy(f => f.Question).Take(3)];
        return new AssistantBlock(AssistantBlockKind.NoIntent, nameof(AssistantTask.Unknown), text, null, "", null, null, null, followUps);
    }

    /// <summary>Tag und Personen des Teams am Standort, die eine nicht erkannte Frage nennt (Ä316).</summary>
    private async Task<UnderstoodParts> UnderstoodAsync(string question, Location location, DateOnly today, CancellationToken ct)
    {
        var team = await db.Employees.AsNoTracking()
            .Where(e => e.IsActive && e.WorkingHours.Any(w => w.LocationId == location.Id)).ToListAsync(ct);
        var people = team.Select(e => e.InHouseName).Where(name => NameMatch.Mentions(question, name)).Order(StringComparer.CurrentCulture).ToList();
        return new UnderstoodParts(UnderstoodParts.Day(question, today), people);
    }

    private async Task<(string Text, IReadOnlyList<AssistantSuggestion> Suggestions, AssistantBlock? List)?> SlotsAsync(string question, Location location, bool canBook, bool unknown, CancellationToken ct)
    {
        var treatments = await OnlineBookingEndpoints.OfferedTreatmentsAsync(db, location.Id, ct);
        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.IsActive && e.WorkingHours.Any(w => w.LocationId == location.Id)).OrderBy(e => e.PreferredName ?? e.DisplayName).ToListAsync(ct);
        var analysis = await reader.AnalyzeAsync(Request(question, time.GetUtcNow(), location, treatments, employees), ct);
        var treatment = treatments.FirstOrDefault(t => t.Id == analysis.TreatmentId);
        var zone = LocalTime.Zone(location.TimeZone);
        var now = time.GetUtcNow();
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == location.Id && r.IsActive && !r.IsArea).ToListAsync(ct);
        var roomId = CalendarRequestService.RoomNamed(question, rooms);
        var wishedDay = analysis.Wishes.OrderBy(w => w.Date).Select(w => (DateOnly?)w.Date).FirstOrDefault() ?? LocalTime.LocalDate(now, zone);

        // „Freie Zeit suchen“ als Knopf: öffnet die Suche im Kalender mit dem, was die Frage nennt; gesucht wird erst nach dem Klick.
        List<AssistantSuggestion> Search() => !canBook ? [] :
        [
            new(AssistantSuggestionKind.SearchFreeTime, "Freie Zeit suchen", location.Id, treatment?.Id, analysis.EmployeeId, null, null,
                LocalTime.ToUtc(wishedDay, TimeOnly.MinValue, zone), roomId),
        ];
        if (treatment is null && unknown)
        {
            return null;
        }

        if (treatment is null)
        {
            return ($"Für welche Behandlung sollen freie Zeiten gesucht werden? Am Standort {location.Name} gibt es: {string.Join(", ", treatments.Select(t => t.Name))}. "
                + "Der Assistent kann auch Kunden suchen, den Tagesüberblick zeigen oder (für die Standortleitung) Auswertungen machen.", Search(), null);
        }

        var employee = employees.FirstOrDefault(e => e.Id == analysis.EmployeeId);
        var wishes = analysis.Wishes.Count > 0 ? analysis.Wishes : NextDays(LocalTime.LocalDate(now, zone), 3);
        var slots = await FreeAsync(location, treatment.Id, employee?.Id, wishes, now, MaxSlots, ct);
        var withOthers = false;
        if (slots.Count == 0 && employee is not null)
        {
            slots = await FreeAsync(location, treatment.Id, null, wishes, now, MaxSlots, ct);
            withOthers = slots.Count > 0;
        }

        if (slots.Count == 0)
        {
            return ($"Für {treatment.Name} ist im gewünschten Zeitraum in {location.Name} nichts frei. Eine Frage nach einem anderen Tag oder einem späteren Zeitraum hilft weiter.", Search(), null);
        }

        var names = employees.ToDictionary(e => e.Id, e => e.InHouseName);
        var who = employee is null ? "" : withOthers ? $" (bei {employee.InHouseName} ist nichts frei, daher mit anderen)" : $" mit {employee.InHouseName}";
        // Ä316: der Kopfsatz nennt nur die Zahl; jede Zeit steht einmal als Zeile, ein Klick öffnet die Buchung (gebucht wird erst nach Bestätigung).
        var text = $"Freie Zeiten für {treatment.Name} in {location.Name}{who}: {slots.Count} {(slots.Count == 1 ? "Vorschlag" : "Vorschläge")}.";
        var items = slots.Select(s =>
        {
            var local = TimeZoneInfo.ConvertTime(s.Start, zone);
            var book = canBook
                ? new AssistantSuggestion(AssistantSuggestionKind.BookSlot, $"{When(s.Start, zone, "de")} · {names.GetValueOrDefault(s.EmployeeId)}", location.Id, treatment.Id,
                    s.EmployeeId, null, null, s.Start)
                : null;
            return new AssistantItem(local.ToString("dddd, d. MMMM", German), names.GetValueOrDefault(s.EmployeeId), local.ToString("HH:mm", German), book, null,
                canBook ? "Buchen …" : "");
        }).ToList();
        return (text, Search(), Blocks.List(nameof(AssistantTask.FreeSlots), text, $"Kalender und Dienstplan des Standorts {location.Name}", items));
    }

    /// <summary>Freie Zeiten in den Wunschzeiträumen, nächste zur Wunschzeit zuerst, mindestens 30 Minuten auseinander.</summary>
    private async Task<List<AvailableSlot>> FreeAsync(Location location, Guid treatmentId, Guid? employeeId,
        IReadOnlyList<RequestedTime> wishes, DateTimeOffset earliest, int max, CancellationToken ct)
    {
        var zone = LocalTime.Zone(location.TimeZone);
        var picked = new List<AvailableSlot>();
        foreach (var wish in wishes.OrderBy(w => w.Date))
        {
            var context = await booking.LoadContextAsync(location.Id, treatmentId, wish.Date, null, ct);
            if (context is null)
            {
                break;
            }

            var target = LocalTime.ToUtc(wish.Date, wish.From ?? new TimeOnly(0, 0), zone);
            var (from, until) = (wish.From?.AddHours(wish.Until is null ? -2 : 0) ?? new TimeOnly(0, 0), wish.Until ?? wish.From?.AddHours(2) ?? new TimeOnly(23, 59));
            var candidates = new ResourcePlanner(context).FreeSlots(wish.Date, Step, employeeId)
                .Where(s => s.Start >= earliest)
                .Where(s => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.Start, zone).DateTime) is var local && local >= from && local <= until)
                .DistinctBy(s => s.Start)
                .OrderBy(s => wish.From is null ? s.Start.Ticks : Math.Abs((s.Start - target).Ticks));
            foreach (var slot in candidates)
            {
                if (picked.Count >= max)
                {
                    return [.. picked.OrderBy(p => p.Start)];
                }

                if (picked.All(p => Math.Abs((p.Start - slot.Start).TotalMinutes) >= 30))
                {
                    picked.Add(slot);
                }
            }
        }

        return [.. picked.OrderBy(p => p.Start)];
    }

    private async Task<(string, IReadOnlyList<AssistantSuggestion>, AssistantBlock?)> FindCustomersAsync(string? query, TimeZoneInfo zone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return ("Wen soll der Assistent suchen? Bitte einen Namen, eine Telefonnummer oder eine Mailadresse nennen.", [], null);
        }

        var q = query.Trim();
        var digits = PhoneNumbers.Tail(PhoneNumbers.Normalize(q));
        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Like).ToList();
        var emailPattern = Like(q);
        IQueryable<Customer> customers;
        if (q.Contains('@', StringComparison.Ordinal))
        {
            customers = db.Customers.Where(c => c.Email != null && EF.Functions.ILike(c.Email, emailPattern));
        }
        else if (digits.Length >= 6 && q.Count(char.IsDigit) >= 6)
        {
            customers = db.Customers.Where(c => c.Phone != null && Regex.Replace(c.Phone, "[^0-9]", "").EndsWith(digits));
        }
        else
        {
            customers = db.Customers.AsQueryable();
            foreach (var token in tokens)
            {
                customers = customers.Where(c => EF.Functions.ILike(c.FirstName, token) || EF.Functions.ILike(c.LastName, token));
            }
        }

        var found = await customers.AsNoTracking().OrderBy(c => c.LastName).ThenBy(c => c.FirstName).Take(5).ToListAsync(ct);
        if (found.Count == 0)
        {
            return ($"Keinen Kunden zu „{q}“ gefunden.", [], null);
        }

        var ids = found.Select(c => c.Id).ToList();
        var now = time.GetUtcNow();
        var next = (await db.Appointments.AsNoTracking().Include(a => a.Treatment)
                .Where(a => ids.Contains(a.CustomerId) && a.Start > now && a.Status == AppointmentStatus.Booked)
                .ToListAsync(ct))
            .GroupBy(a => a.CustomerId).ToDictionary(g => g.Key, g => g.MinBy(a => a.Start)!);
        // Ä316: je Gast eine Zeile (Kontakt und nächster Termin darunter), ein Klick öffnet die Kundenkarte.
        var items = found.Select(c =>
        {
            var upcoming = next.TryGetValue(c.Id, out var a) ? $"nächster Termin {a.Treatment.Name} am {When(a.Start, zone, "de")}" : "kein kommender Termin";
            var detail = string.Join(" · ", new[] { c.Phone, c.Email, upcoming }.Where(v => !string.IsNullOrWhiteSpace(v)));
            return new AssistantItem(c.InHouseName, detail, null,
                new AssistantSuggestion(AssistantSuggestionKind.OpenCustomer, c.InHouseName, null, null, null, c.Id, null, null), null, "Kundenkarte öffnen");
        }).ToList();

        // Bei einem Treffer zusätzlich der nächste Termin im Kalender und alle Termine über sieben Tage; bei mehreren erst den Gast wählen.
        List<AssistantSuggestion> actions = [];
        if (found is [var one])
        {
            if (next.TryGetValue(one.Id, out var upcoming))
            {
                actions.Add(new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, $"Nächster Termin: {When(upcoming.Start, zone, "de")} {upcoming.Treatment.Name}",
                    upcoming.LocationId, upcoming.TreatmentId, upcoming.EmployeeId, one.Id, upcoming.Id, upcoming.Start));
            }

            actions.Add(new AssistantSuggestion(AssistantSuggestionKind.ShowCustomerAppointments, $"Alle Termine von {one.InHouseName}", null, null, null, one.Id, null, null));
        }

        var text = found.Count == 1 ? "1 Gast gefunden." : $"{found.Count} Gäste gefunden.";
        return (text, actions, Blocks.List(nameof(AssistantTask.FindCustomer), text, "Kundenstamm", items));

        static string Like(string value) =>
            "%" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
    }

    /// <summary>
    /// Tagesüberblick (Ä316): Kopfsatz mit Tag, Standort und Zahl, darunter je Termin eine Zeile (Uhrzeit, Gast, Behandlung und Person); ein Klick zeigt den Termin im
    /// Kalender. Therapeut:innen sehen nur die eigenen Termine, ohne Personennamen. Mit Tageszeit („Termine heute nachmittag“) nur die Termine, die in ihr beginnen
    /// (Grenzen der Warteliste, Ä317).
    /// </summary>
    private async Task<AssistantBlock> OverviewAsync(Location location, DateOnly day, Guid? employeeId, DayPart? part, CancellationToken ct)
    {
        var zone = LocalTime.Zone(location.TimeZone);
        var (from, until) = part is { } dayPart ? Waitlist.Window(dayPart, day, zone) : LocalTime.Day(day, zone);
        var query = db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
            .Where(a => a.LocationId == location.Id && a.Start >= from && a.Start < until && a.Status != AppointmentStatus.Cancelled);
        if (employeeId is { } own)
        {
            query = query.Where(a => a.EmployeeId == own);
        }

        var appointments = await query.ToListAsync(ct);
        var dayName = day.ToString("dddd, d. MMMM", German) + (part is null ? "" : $" {AnswerDates.PartWord(part)}");
        var source = employeeId is null ? $"Kalender des Standorts {location.Name}" : $"Ihre Termine im Kalender des Standorts {location.Name}";
        if (appointments.Count == 0)
        {
            return Blocks.List(nameof(AssistantTask.DayOverview), $"{dayName}, {location.Name}: {(employeeId is null ? "keine Termine" : "keine Termine für Sie")}.", source, []);
        }

        var names = (await db.Employees.AsNoTracking().ToListAsync(ct)).ToDictionary(e => e.Id, e => e.InHouseName);
        var items = appointments
            .OrderBy(a => a.Start).ThenBy(a => names.GetValueOrDefault(a.EmployeeId), StringComparer.CurrentCulture).ThenBy(a => a.Id)
            .Take(Blocks.MaxItems)
            .Select(a =>
            {
                var time = TimeZoneInfo.ConvertTime(a.Start, zone).ToString("HH:mm", German);
                var detail = employeeId is null ? $"{a.Treatment.Name} · {names.GetValueOrDefault(a.EmployeeId)}" : a.Treatment.Name;
                return new AssistantItem(a.Customer.InHouseName, detail, time,
                    new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, $"{time} {a.Treatment.Name}", location.Id, a.TreatmentId, a.EmployeeId, a.CustomerId, a.Id,
                        a.Start),
                    null, "Im Kalender zeigen");
            }).ToList();
        var checkedIn = appointments.Count(a => a.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed);
        var text = $"{dayName}, {location.Name}: {appointments.Count} Termin{(appointments.Count == 1 ? "" : "e")}"
            + (checkedIn > 0 ? $", davon {checkedIn} eingecheckt oder abgeschlossen" : "") + ".";
        return Blocks.List(nameof(AssistantTask.DayOverview), text, source, items);
    }

    private sealed record Period(int Appointments, int Completed, int NoShows, int Cancelled, decimal Value, decimal Till, double Utilization, IReadOnlyList<string> Top);

    private async Task<(IReadOnlyList<AssistantFigure>, string)> FiguresAsync(Location location, DateOnly from, DateOnly until, CancellationToken ct)
    {
        if (until < from)
        {
            (from, until) = (until, from);
        }

        var length = until.DayNumber - from.DayNumber + 1;
        var current = await PeriodAsync(location, from, until, ct);
        var previous = await PeriodAsync(location, from.AddDays(-length), from.AddDays(-1), ct);
        var money = CultureInfo.GetCultureInfo(location.Currency == "CHF" ? "de-CH" : "de-AT");
        string Money(decimal v) => v.ToString("#,##0.00", money) + " " + location.Currency;
        string Percent(double v) => v.ToString("0 %", German);
        string Rate(int part, int all) => all == 0 ? "–" : Percent((double)part / all);

        var figures = new List<AssistantFigure>
        {
            new("Termine", current.Appointments.ToString(German), previous.Appointments.ToString(German)),
            new("Terminwert", Money(current.Value), Money(previous.Value)),
            new("Kassenumsatz (brutto)", Money(current.Till), Money(previous.Till)),
            new("Auslastung (laut Arbeitszeiten)", Percent(current.Utilization), Percent(previous.Utilization)),
            new("No-Shows", $"{current.NoShows} ({Rate(current.NoShows, current.Appointments)})", $"{previous.NoShows} ({Rate(previous.NoShows, previous.Appointments)})"),
            new("Stornos", $"{current.Cancelled} ({Rate(current.Cancelled, current.Appointments + current.Cancelled)})",
                $"{previous.Cancelled} ({Rate(previous.Cancelled, previous.Appointments + previous.Cancelled)})"),
        };

        var range = from == until ? from.ToString("d. MMMM yyyy", German) : $"{from.ToString("d.M.", German)} bis {until.ToString("d.M.yyyy", German)}";
        var trend = previous.Value == 0 ? "" : current.Value >= previous.Value
            ? $" Der Terminwert liegt {Percent((double)((current.Value - previous.Value) / previous.Value))} über dem Vergleichszeitraum."
            : $" Der Terminwert liegt {Percent((double)((previous.Value - current.Value) / previous.Value))} unter dem Vergleichszeitraum.";
        var top = current.Top.Count == 0 ? "" : $" Am häufigsten gebucht: {string.Join(", ", current.Top)}.";
        var hint = current.Appointments > 0 && (double)current.NoShows / current.Appointments >= 0.1
            ? " Die No-Show-Quote ist hoch; Erinnerungen per SMS oder eine Anzahlung bei der Online-Buchung können helfen."
            : "";
        return (figures, $"{location.Name}, {range} (Vergleich: die {length} Tage davor): {current.Appointments} Termine, "
            + $"Auslastung {Percent(current.Utilization)}.{trend}{top}{hint}");
    }

    private async Task<Period> PeriodAsync(Location location, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var zone = LocalTime.Zone(location.TimeZone);
        var start = LocalTime.Day(from, zone).From;
        var end = LocalTime.Day(until, zone).Until;
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Treatment)
            .Where(a => a.LocationId == location.Id && a.Start >= start && a.Start < end).ToListAsync(ct);
        var held = appointments.Where(a => a.Status != AppointmentStatus.Cancelled).ToList();

        var registers = db.CashRegisters.Where(r => r.LocationId == location.Id).Select(r => r.Id);
        var till = await db.Receipts.Where(r => registers.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end
                && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
            .SumAsync(r => (decimal?)r.Total, ct) ?? 0m;

        var hours = await db.Employees.AsNoTracking().Where(e => e.IsActive).SelectMany(e => e.WorkingHours)
            .Where(w => w.LocationId == location.Id).ToListAsync(ct);
        var plannedMinutes = 0.0;
        for (var day = from; day <= until; day = day.AddDays(1))
        {
            plannedMinutes += hours.Where(h => h.Day == day.DayOfWeek).Sum(h => (h.End - h.Start).TotalMinutes);
        }

        var bookedMinutes = held.Sum(a => (a.End - a.Start).TotalMinutes);
        return new Period(
            held.Count,
            held.Count(a => a.Status == AppointmentStatus.Completed),
            held.Count(a => a.Status == AppointmentStatus.NoShow),
            appointments.Count - held.Count,
            held.Where(a => a.Status != AppointmentStatus.NoShow).Sum(a => a.Price),
            till,
            plannedMinutes <= 0 ? 0 : Math.Min(1, bookedMinutes / plannedMinutes),
            [.. held.GroupBy(a => a.Treatment.Name).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(3).Select(g => $"{g.Key} ({g.Count()})")]);
    }

    /// <summary>Die Frage als Anfrage an die Terminannahme, die Behandlung, Person und Wunschzeiten liest; „jetzt“ ist <paramref name="now"/>.</summary>
    internal static MailAnalysisRequest Request(string question, DateTimeOffset now, Location location, IEnumerable<Treatment> treatments,
        IEnumerable<Employee> employees) => new(
        "assistent@aispa.local", null, "Frage an den Assistenten", question, now, location.TimeZone,
        [.. treatments.Select(t => new NamedOption(t.Id, t.Name))],
        [new NamedOption(location.Id, location.Name)],
        [.. employees.Select(e => new NamedOption(e.Id, EmployeeNames.Recognizable(e.DisplayName, e.PreferredName)))]);

    private static List<RequestedTime> NextDays(DateOnly first, int count) =>
        [.. Enumerable.Range(0, count).Select(i => new RequestedTime(first.AddDays(i), null, null))];

    private static string When(DateTimeOffset start, TimeZoneInfo zone, string language)
    {
        var local = TimeZoneInfo.ConvertTime(start, zone);
        return language switch
        {
            "de" => local.ToString("ddd dd.MM. HH:mm", German),
            "fr" => local.ToString("ddd dd.MM. HH:mm", CultureInfo.GetCultureInfo("fr-CH")),
            "it" => local.ToString("ddd dd.MM. HH:mm", CultureInfo.GetCultureInfo("it-CH")),
            _ => local.ToString("ddd d MMM, HH:mm", CultureInfo.GetCultureInfo("en-GB")),
        };
    }

    private static string CustomerText(string language, string kind, string subject, string? list) => (language, kind) switch
    {
        ("de", "treatment") => $"Welche Behandlung möchten Sie? Online buchbar sind: {subject}.",
        ("de", "none") => $"Für {subject} habe ich im gewünschten Zeitraum leider nichts Freies gefunden. Versuchen Sie gern einen anderen Tag.",
        ("de", _) => $"Für {subject} sind diese Zeiten frei: {list}. Wählen Sie eine Zeit, um weiter zu buchen.",
        ("fr", "treatment") => $"Quel soin souhaitez-vous ? Réservables en ligne : {subject}.",
        ("fr", "none") => $"Je n'ai pas trouvé de créneau libre pour {subject} à cette période. Essayez un autre jour.",
        ("fr", _) => $"Créneaux libres pour {subject} : {list}. Choisissez un créneau pour continuer.",
        ("it", "treatment") => $"Quale trattamento desidera? Prenotabili online: {subject}.",
        ("it", "none") => $"Non ho trovato orari liberi per {subject} in quel periodo. Provi un altro giorno.",
        ("it", _) => $"Orari liberi per {subject}: {list}. Scelga un orario per continuare.",
        (_, "treatment") => $"Which treatment would you like? Bookable online: {subject}.",
        (_, "none") => $"I could not find a free time for {subject} in that period. Please try another day.",
        _ => $"Free times for {subject}: {list}. Pick a time to continue booking.",
    };

    private static string CustomerDisclosure(string language) => language switch
    {
        "de" => "Vorschläge eines KI-Assistenten. Gebucht wird erst, wenn Sie die Buchung bestätigen.",
        "fr" => "Suggestions d'un assistant IA. La réservation n'est faite qu'après votre confirmation.",
        "it" => "Proposte di un assistente IA. La prenotazione avviene solo dopo la sua conferma.",
        _ => "Suggestions from an AI assistant. Nothing is booked until you confirm.",
    };
}
