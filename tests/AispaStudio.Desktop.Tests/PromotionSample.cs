using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 101: Beispieldaten und Antworten der Aktionen-API für die native Maske „Aktionen“ (ViewModel- und Fenstertests teilen sie).</summary>
internal static class PromotionSample
{
    public static readonly Guid AutumnId = Guid.Parse("00000000-0000-0000-0000-0000000c1201");
    public static readonly Guid SpringId = Guid.Parse("00000000-0000-0000-0000-0000000c1202");
    public static readonly Guid PausedId = Guid.Parse("00000000-0000-0000-0000-0000000c1203");
    public static readonly Guid EasterId = Guid.Parse("00000000-0000-0000-0000-0000000c1204");
    public static readonly Guid WinterId = Guid.Parse("00000000-0000-0000-0000-0000000c1205");
    public static readonly Guid LastMinuteId = Guid.Parse("00000000-0000-0000-0000-0000000c1206");
    public static readonly Guid OffPeakId = Guid.Parse("00000000-0000-0000-0000-0000000c1207");

    /// <summary>Die Behandlungen des Katalogs (Namen und Kennungen der Fenstertests).</summary>
    public static readonly TreatmentDto Massage = SpaSample.Treatments[0];
    public static readonly TreatmentDto HamamRitual = SpaSample.Treatments[4];

    public static PromotionDto Autumn(DateOnly today) =>
        new(AutumnId, "Herbst", "10 % auf alle Massagen im Oktober", today.AddDays(-10), today.AddDays(20), true, [], 10m, null, PromotionScope.AllTreatments, [], true, false, PromotionPhase.Running);

    public static PromotionDto Spring(DateOnly today) =>
        new(SpringId, "Frühling", "5 € auf die Klassische Massage", today.AddDays(30), today.AddDays(60), false, [StockSample.Vienna.Id], null, 5m, PromotionScope.SelectedTreatments, [Massage.Id], true, false,
            PromotionPhase.Planned);

    public static PromotionDto Paused(DateOnly today) =>
        new(PausedId, "Sommerpause", "20 % auf Pakete der Hamam-Rituale", today.AddDays(-5), today.AddDays(5), true, [], 20m, null, PromotionScope.SelectedPackages, [HamamRitual.Id], false, false, PromotionPhase.Paused);

    public static PromotionDto Easter(DateOnly today) =>
        new(EasterId, "Ostern", "15 % zu Ostern", today.AddDays(-40), today.AddDays(-30), true, [], 15m, null, PromotionScope.AllTreatments, [], true, false, PromotionPhase.Expired);

    public static PromotionDto Winter(DateOnly today) =>
        new(WinterId, "Winter", "Heißer Tee zur Massage", today.AddDays(-100), today.AddDays(-80), true, [], 8m, null, PromotionScope.AllTreatments, [], true, true, PromotionPhase.Expired);

    /// <summary>Kurzfristig frei (Nachtrag zu ADR 0071): 20 % auf Termine, die höchstens 24 Stunden vorher gebucht werden.</summary>
    public static PromotionDto LastMinute(DateOnly today) =>
        new(LastMinuteId, "Kurzfristig frei", "20 % auf Termine, die Sie höchstens 24 Stunden vorher buchen", today.AddDays(-10), today.AddDays(355), true, [], 20m, null,
            PromotionScope.AllTreatments, [], true, false, PromotionPhase.Running)
        { Kind = PromotionKind.LastMinute, LeadHours = 24, Slots = [] };

    /// <summary>Schwache Zeiten: 15 % an den Vormittagen von Montag bis Mittwoch und am Dienstagnachmittag.</summary>
    public static PromotionDto OffPeak(DateOnly today) =>
        new(OffPeakId, "Ruhige Vormittage", "15 % am Vormittag, Montag bis Mittwoch", today.AddDays(-10), today.AddDays(355), true, [], 15m, null,
            PromotionScope.AllTreatments, [], true, false, PromotionPhase.Running)
        {
            Kind = PromotionKind.OffPeak,
            Slots = [new(DayOfWeek.Monday, DayPart.Morning), new(DayOfWeek.Tuesday, DayPart.Morning), new(DayOfWeek.Tuesday, DayPart.Afternoon), new(DayOfWeek.Wednesday, DayPart.Morning)],
        };

    /// <summary>Der Zustand, den der Server aus Zeitraum und Schalter ableitet (heute am Standort).</summary>
    private static PromotionPhase PhaseOf(PromotionInput input, DateOnly today) =>
        !input.IsActive ? PromotionPhase.Paused : input.ValidUntil < today ? PromotionPhase.Expired : input.ValidFrom > today ? PromotionPhase.Planned : PromotionPhase.Running;

    private static PromotionDto FromInput(Guid id, PromotionInput input, DateOnly today, bool archived) =>
        new(id, input.Name, input.GuestText, input.ValidFrom, input.ValidUntil, input.AllLocations, input.LocationIds ?? [], input.DiscountPercent, input.DiscountAmount, input.Scope,
            input.TreatmentIds ?? [], input.IsActive, archived, PhaseOf(input, today))
        {
            Kind = input.Kind ?? PromotionKind.Limited,
            LeadHours = input.Kind == PromotionKind.LastMinute ? input.LeadHours : null,
            Slots = input.Kind == PromotionKind.OffPeak ? input.Slots ?? [] : [],
        };

    /// <summary>
    /// Die Vorschau des Gästepreises wie die API: Beispieltermin je Art und die ersten drei betroffenen Behandlungen des Katalogs mit Preis und Gästepreis
    /// (Prozent kaufmännisch gerundet oder Betrag; nicht unter den Preis).
    /// </summary>
    private static PromotionPreviewDto Preview(PromotionInput input)
    {
        var treatments = (input.Scope == PromotionScope.AllTreatments ? SpaSample.Treatments : SpaSample.Treatments.Where(t => input.TreatmentIds?.Contains(t.Id) == true)).Take(3);
        var lines = treatments.Select(t =>
        {
            var discount = input.DiscountPercent is { } percent ? decimal.Round(t.Price * percent / 100, 2, MidpointRounding.AwayFromZero) : input.DiscountAmount ?? 0;
            return new PromotionPreviewLineDto(t.Id, t.Name, t.Price, discount > 0 && discount < t.Price ? t.Price - discount : t.Price);
        }).ToList();
        var example = input.Kind switch
        {
            PromotionKind.LastMinute => $"Termin höchstens {input.LeadHours} Stunden vorher gebucht",
            PromotionKind.OffPeak => input.Slots is [var first, ..]
                ? $"{System.Globalization.CultureInfo.GetCultureInfo("de-AT").DateTimeFormat.GetDayName(first.Day)}, {first.Part switch { DayPart.Morning => "Vormittag", DayPart.Afternoon => "Nachmittag", _ => "Abend" }}"
                : "Noch kein Feld im Wochenraster gewählt",
            _ => $"Termin von {input.ValidFrom:dd.MM.} bis {input.ValidUntil:dd.MM.yyyy}",
        };
        return new PromotionPreviewDto(example, lines, "Treffen zwei Rabatte zusammen, schlägt die Kassa den höheren zuerst vor; es gilt immer nur einer.");
    }

    /// <summary>
    /// Die Aktionen-API mit festen Antworten: Liste (immer mit <c>includeArchived=true</c>, wie die Maske sie holt), Katalog, Standorte; schreibende Aufrufe verändern die Liste wie der Server und
    /// stehen in <see cref="StubApi.Calls"/>. <paramref name="today"/> ist der heutige Tag am Standort; <paramref name="two"/> fügt den Standort Salzburg hinzu; <paramref name="catalog"/> aus lässt Standorte und Katalog der Fenstertests stehen.
    /// </summary>
    public static StubApi Api(DateOnly today, StubApi? api = null, bool two = false, bool catalog = true, params PromotionDto[] promotions)
    {
        var list = (promotions.Length > 0 ? promotions : [Autumn(today), Spring(today), Paused(today), Easter(today), Winter(today), LastMinute(today), OffPeak(today)]).ToList();
        var locations = two ? new List<LocationDto> { StockSample.Vienna, StockSample.Salzburg } : [StockSample.Vienna];
        api ??= new StubApi();
        if (catalog)
        {
            api.On("GET /api/locations", locations).On("GET /api/treatments", SpaSample.Treatments.ToList());
        }

        return api
            .OnJson("GET /api/promotions?includeArchived=true", _ => list.ToList())
            .OnJson("POST /api/promotions/preview", body => Preview(JsonSerializer.Deserialize<PromotionInput>(body!, ApiClient.Json)!))
            .OnJson("POST /api/promotions", body =>
            {
                var created = FromInput(Guid.NewGuid(), JsonSerializer.Deserialize<PromotionInput>(body!, ApiClient.Json)!, today, false);
                list.Add(created);
                return created;
            }, HttpStatusCode.Created)
            .OnJson($"PUT /api/promotions/{AutumnId}", body => Replace(list, AutumnId, JsonSerializer.Deserialize<PromotionInput>(body!, ApiClient.Json)!, today))
            .OnJson($"PUT /api/promotions/{SpringId}", body => Replace(list, SpringId, JsonSerializer.Deserialize<PromotionInput>(body!, ApiClient.Json)!, today))
            .OnJson($"PUT /api/promotions/{OffPeakId}", body => Replace(list, OffPeakId, JsonSerializer.Deserialize<PromotionInput>(body!, ApiClient.Json)!, today))
            .OnJson($"POST /api/promotions/{AutumnId}/archive", _ => Archive(list, AutumnId, true))
            .OnJson($"POST /api/promotions/{WinterId}/unarchive", _ => Archive(list, WinterId, false));
    }

    private static PromotionDto Replace(List<PromotionDto> list, Guid id, PromotionInput input, DateOnly today)
    {
        var index = list.FindIndex(p => p.Id == id);
        var changed = FromInput(id, input, today, list[index].IsArchived);
        list[index] = changed;
        return changed;
    }

    private static PromotionDto Archive(List<PromotionDto> list, Guid id, bool archived)
    {
        var index = list.FindIndex(p => p.Id == id);
        list[index] = list[index] with { IsArchived = archived };
        return list[index];
    }

    public static PromotionInput Sent(StubApi api, string route) => JsonSerializer.Deserialize<PromotionInput>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;
}
