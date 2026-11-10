using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Chip unter „Gut zu wissen“, z. B. „Druck: sanft“; „Nicht behandeln: …“ ist ein Warn-Chip.</summary>
/// <summary>Ein Wunsch als Chip; <paramref name="Tip"/> trägt den vollen Wortlaut, wenn der Chip kürzt (Klammerzusatz bei „Nicht behandeln“).</summary>
public sealed record PreferenceChip(string Text, bool IsWarning = false, string? Tip = null);

/// <summary>Zeile im Verlauf der Kundenkarte, z. B. „Do. 6.11. 10:30“ und „Hot-Stone-Massage bei Lena Huber“; <c>Status</c> nur bei Abweichungen.</summary>
public sealed record TimelineEntry(string When, string Text, string? Status = null, bool IsProblem = false);

/// <summary>Erkennt Wörter, die nach Gesundheitsdaten klingen; die gehören verschlüsselt in die Anamnese, nicht in Vermerke.</summary>
public static class HealthWords
{
    private static readonly string[] Words =
    [
        "allerg", "schwanger", "diabet", "medikament", "operation", "herz", "blutdruck", "krebs", "tumor", "epilep",
        "thrombose", "krampfader", "asthma", "migräne", "depress", "bandscheibe", "chemo", "krankheit", "erkrank", "diagnose",
    ];

    /// <summary>Das erste gefundene Wort im Text (z. B. „Allergie“), sonst null.</summary>
    public static string? Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var word in text.Split([' ', ',', '.', ';', ':', '!', '?', '(', ')', '/', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (Words.Any(w => word.Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                return word;
            }
        }

        return null;
    }

    public static string? Warning(string? text) => Find(text) is { } word
        ? $"„{word}“ klingt nach einer Gesundheitsangabe. Die gehört verschlüsselt in die Anamnese; hier lesen sie alle im Team, und der Gast kann den Vermerk einsehen."
        : null;
}

// ---------- Kundenkarte: Kennzahlen als eine Zeile, Sterne, „Gut zu wissen“ (Wünsche, Rezeptionsvermerk) und der Verlauf rechts (R07 Slice 141) ----------
public partial class CustomersViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarsGlyph), nameof(StarsChipText), nameof(StarsName), nameof(HasStars), nameof(MaySeeReceptionNote), nameof(ReceptionNoteText), nameof(BirthdayInfo))]
    [NotifyPropertyChangedFor(nameof(StatsLine), nameof(StatsWarning), nameof(StatsWarningTip), nameof(HasAttention))]
    public partial CustomerInsights? Insights { get; private set; }

    /// <summary>„★★“; berechnet aus Besuchen und Umsatz, keine Bewertung durch das Team.</summary>
    public string StarsGlyph => new('★', Insights?.Stars ?? 0);

    /// <summary>Chip im Kopf: „★ Stammgast“, bei mehr Sternen „★★ Stammgast“.</summary>
    public string StarsChipText => $"{StarsGlyph} Stammgast";

    /// <summary>„Stammgast, 2 Sterne“: der Name des Chips „★ Stammgast“ für den Screenreader; den Grund nennt der Tooltip (<c>StarsText</c>).</summary>
    public string StarsName => Insights?.Stars == 1 ? "Stammgast, 1 Stern" : $"Stammgast, {Insights?.Stars ?? 0} Sterne";

    public bool HasStars => Insights?.Stars > 0;

    /// <summary>„45 Jahre · Nächster Geburtstag in 12 Tagen“ bzw. „Geburtstag heute · 46“ (Ä45); der Server rechnet, nicht die Oberfläche.</summary>
    public string? BirthdayInfo => BirthdayText.Card(Insights?.Birthday);

    public bool MaySeeReceptionNote => Insights?.MaySeeReceptionNote == true;

    public string ReceptionNoteText => Insights?.ReceptionNote ?? "";

    /// <summary>Kennzahlen in einer Zeile statt Kacheln, z. B. „4 Besuche in 12 Monaten · zuletzt am 15.10. · meist alle 16 Tage · nie gefehlt“.</summary>
    public string StatsLine => Insights is { } i ? Stats(i, DateOnly.FromDateTime(time.GetLocalNow().DateTime)) : "";

    /// <summary>Was die Rezeption wissen muss, in warn mit Dreieck: „1× nicht erschienen · 1 späte Absage“; ohne beides null.</summary>
    public string? StatsWarning => Insights is { } i ? Warnings(i) : null;

    /// <summary>Die Daten zu <see cref="StatsWarning"/> für den Tooltip.</summary>
    public string? StatsWarningTip => Insights is { } i && Warnings(i) is not null
        ? string.Join('\n', new[]
        {
            i.NoShows.Count > 0 ? $"Nicht erschienen: {Dates(i.NoShows)}" : null,
            i.LateCancellations.Count > 0 ? $"Späte Absagen: {Dates(i.LateCancellations)}" : null,
        }.OfType<string>())
        : null;

    /// <summary>Leere Angaben fallen weg; ohne jeden Besuch „Noch kein Besuch“. Den Umsatz sendet der Server nur mit dem Recht „Zahlen sehen“.</summary>
    public static string Stats(CustomerInsights i, DateOnly today)
    {
        if (i.Visits == 0 && i.NoShows.Count == 0)
        {
            return "Noch kein Besuch";
        }

        var parts = new List<string> { $"{i.VisitsLastYear} {(i.VisitsLastYear == 1 ? "Besuch" : "Besuche")} in 12 Monaten" };
        if (i.LastVisit is { } last)
        {
            parts.Add($"zuletzt am {last.ToString(last.Year == today.Year ? "d.M." : "d.M.yyyy", CultureInfo.InvariantCulture)}");
        }

        if (i.IntervalDays is { } days)
        {
            parts.Add($"meist {IntervalText(days)}");
        }

        if (i.NoShows.Count == 0)
        {
            parts.Add("nie gefehlt");
        }

        parts.AddRange((i.Revenue ?? []).Select(r => $"{Money.Format(r.LastYear, r.Currency)} Umsatz in 12 Monaten ({Money.Format(r.Total, r.Currency)} gesamt)"));
        return string.Join(" · ", parts);
    }

    private static string? Warnings(CustomerInsights i)
    {
        var parts = new[]
        {
            i.NoShows.Count > 0 ? $"{i.NoShows.Count}× nicht erschienen" : null,
            i.LateCancellations.Count > 0 ? $"{i.LateCancellations.Count} {(i.LateCancellations.Count == 1 ? "späte Absage" : "späte Absagen")}" : null,
        }.OfType<string>().ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private async Task LoadInsightsAsync(CustomerDto customer, int version)
    {
        var insights = (await api.GetAsync<CustomerInsights>($"api/customers/{customer.Id}/insights").ConfigureAwait(true)).Value;
        var preferences = (await api.GetAsync<CustomerPreferences>($"api/reports/customers/{customer.Id}/preferences").ConfigureAwait(true)).Value;
        if (version != _detailVersion)
        {
            return;
        }

        Insights = insights;
        Wishes = preferences;
        ShowPackages(insights);
    }

    /// <summary>„alle 5 Wochen“ ab drei Wochen, sonst in Tagen.</summary>
    public static string IntervalText(int days) => days >= 21
        ? $"alle {Math.Round(days / 7.0).ToString(CultureInfo.InvariantCulture)} Wochen"
        : $"alle {days} {(days == 1 ? "Tag" : "Tage")}";

    private static string Dates(IReadOnlyList<DateOnly> dates) =>
        string.Join(", ", dates.Take(5).Select(d => d.ToString("d.M.yyyy", CultureInfo.InvariantCulture))) + (dates.Count > 5 ? $" und {dates.Count - 5} weitere" : "");

    // ---------- Rezeptionsvermerk ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool IsEditingReceptionNote { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReceptionNoteWarning))]
    public partial string ReceptionNoteDraft { get; set; } = "";

    public string? ReceptionNoteWarning => HealthWords.Warning(ReceptionNoteDraft);

    [RelayCommand]
    private void EditReceptionNote()
    {
        CloseEditors();
        ReceptionNoteDraft = Insights?.ReceptionNote ?? "";
        IsEditingReceptionNote = true;
    }

    [RelayCommand]
    private void CancelReceptionNote() => IsEditingReceptionNote = false;

    [RelayCommand]
    private async Task SaveReceptionNoteAsync()
    {
        if (Selected is not { } customer || Insights is not { } insights)
        {
            return;
        }

        var text = ReceptionNoteDraft.Trim();
        var result = await api.PutAsync<object>($"api/customers/{customer.Id}/reception-note", new CustomerReceptionNoteInput(text)).ConfigureAwait(true);
        if (result.Error is not null)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        IsEditingReceptionNote = false;
        Insights = insights with { ReceptionNote = text.Length == 0 ? null : text };
    }

    // ---------- Wünsche des Gastes: für das Team in „Mein Tag“ und in der Hover-Karte ----------

    [ObservableProperty]
    public partial CustomerPreferences? Wishes { get; private set; }

    /// <summary>Die Wünsche als Chips; „Nicht behandeln: …“ steht nicht hier, sondern im Warnstreifen unter dem Kopf (<see cref="AvoidWarning"/>).</summary>
    public ObservableCollection<PreferenceChip> WishChips { get; } = [];

    /// <summary>„Nicht behandeln: Füße“ im Warnstreifen über der oberen Drittellinie der Karte (masken.md R14); der Grund in Klammern steht im Tooltip.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAttention))]
    public partial PreferenceChip? AvoidWarning { get; private set; }

    /// <summary>Ob der Warnstreifen erscheint: etwas, das nicht behandelt werden darf, oder Ausfälle (nicht erschienen, späte Absagen).</summary>
    public bool HasAttention => AvoidWarning is not null || StatsWarning is not null;

    partial void OnWishesChanged(CustomerPreferences? value)
    {
        WishChips.Clear();
        foreach (var chip in value is null ? [] : Chips(value))
        {
            WishChips.Add(chip);
        }

        AvoidWarning = value?.AvoidAreas is { } avoid ? AvoidChip(avoid) : null;
    }

    public static IEnumerable<PreferenceChip> Chips(CustomerPreferences p) => new[]
    {
        p.Pressure is { } pressure ? new PreferenceChip($"Druck: {pressure}") : null,
        p.Scent is { } scent ? new PreferenceChip($"Duft: {scent}") : null,
        p.RoomTemperature is { } temperature ? new PreferenceChip($"Raum: {temperature}") : null,
        p.Music is { } music ? new PreferenceChip($"Musik: {music}") : null,
        p.PrefersQuiet == true ? new PreferenceChip("wenig Gespräch") : null,
        p.FocusAreas is { } focus ? new PreferenceChip($"Fokus: {focus}") : null,
        p.Other is { } other ? new PreferenceChip(other) : null,
    }.OfType<PreferenceChip>();

    /// <summary>„Füße (kitzlig)“ wird zum Chip „Nicht behandeln: Füße“; der Grund in Klammern steht im Tooltip.</summary>
    public static PreferenceChip AvoidChip(string avoid)
    {
        var full = $"Nicht behandeln: {avoid}";
        var cut = avoid.IndexOf(" (", StringComparison.Ordinal);
        return cut > 0 ? new PreferenceChip($"Nicht behandeln: {avoid[..cut]}", true, full) : new PreferenceChip(full, true);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool IsEditingWishes { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WishesWarning))]
    public partial string WishPressure { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WishesWarning))]
    public partial string WishScent { get; set; } = "";

    [ObservableProperty]
    public partial string WishTemperature { get; set; } = "";

    [ObservableProperty]
    public partial string WishMusic { get; set; } = "";

    [ObservableProperty]
    public partial bool WishQuiet { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WishesWarning))]
    public partial string WishFocus { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WishesWarning))]
    public partial string WishAvoid { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WishesWarning))]
    public partial string WishOther { get; set; } = "";

    public string? WishesWarning => HealthWords.Warning(string.Join(' ', WishPressure, WishScent, WishFocus, WishAvoid, WishOther));

    [RelayCommand]
    private void EditWishes()
    {
        CloseEditors();
        WishPressure = Wishes?.Pressure ?? "";
        WishScent = Wishes?.Scent ?? "";
        WishTemperature = Wishes?.RoomTemperature ?? "";
        WishMusic = Wishes?.Music ?? "";
        WishQuiet = Wishes?.PrefersQuiet == true;
        WishFocus = Wishes?.FocusAreas ?? "";
        WishAvoid = Wishes?.AvoidAreas ?? "";
        WishOther = Wishes?.Other ?? "";
        IsEditingWishes = true;
    }

    [RelayCommand]
    private void CancelWishes() => IsEditingWishes = false;

    [RelayCommand]
    private async Task SaveWishesAsync()
    {
        if (Selected is not { } customer)
        {
            return;
        }

        static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var result = await api.PutAsync<CustomerPreferences>($"api/reports/customers/{customer.Id}/preferences", new CustomerPreferences(
            Blank(WishPressure), Blank(WishScent), Blank(WishTemperature), Blank(WishMusic), WishQuiet ? true : null,
            Blank(WishFocus), Blank(WishAvoid), Blank(WishOther))).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        IsEditingWishes = false;
        Wishes = saved;
    }

    private void ResetCard()
    {
        Flags.Clear();
        Insights = null;
        Wishes = null;
        ShowPackages(null);
        RaiseSections();
    }
}
