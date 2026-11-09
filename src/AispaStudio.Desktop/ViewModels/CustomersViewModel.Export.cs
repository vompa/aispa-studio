using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Auswahl in der Kundenkarte: Ja, Nein oder nicht gefragt.</summary>
public sealed record ConsentChoice(MarketingConsentState State, string Label)
{
    public static IReadOnlyList<ConsentChoice> All { get; } =
        [new(MarketingConsentState.Confirmed, "Ja"), new(MarketingConsentState.Refused, "Nein"), new(MarketingConsentState.None, "Nicht gefragt")];

    public override string ToString() => Label;
}

// ---------- Export der Kundenliste, Kontakt als vCard, Werbe-Einwilligung am Empfang, Meldung unten (Export/Import, Aufgabe B) ----------
public partial class CustomersViewModel
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(5);

    private ExportViewModel? _export;
    private int _toastClock;
    private bool _toastHeld;

    /// <summary>Der Knopf „Exportieren“ über der Liste: Excel, CSV, Portal-CSV, vCard, für die Leitung dazu die Newsletter-Liste; jeder Eintrag nimmt die aktuelle Suche mit.</summary>
    public ExportViewModel Export => _export ??= new ExportViewModel(api, ExportOptions())
    {
        Saved = ShowToast,
        Failed = text => Error = text,
    };

    /// <summary>„…“ › „Kontakt als vCard“: die ausgewählte Kundin als eine Datei.</summary>
    public ExportOption VCardOption => ExportOption.For(ExportFormat.Vcf, () => $"api/customers/{Selected?.Id}/vcard", "Kontakt als vCard");

    private List<ExportOption> ExportOptions()
    {
        var options = new List<ExportOption>
        {
            ExportOption.For(ExportFormat.Xlsx, () => ExportPath("xlsx")),
            ExportOption.For(ExportFormat.Csv, () => ExportPath("csv")),
            ExportOption.For(ExportFormat.Portal, () => ExportPath("portal")),
            ExportOption.For(ExportFormat.Vcf, () => ExportPath("vcf")),
        };
        if (User?.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager)
        {
            options.Add(ExportOption.For(ExportFormat.Csv, () => $"{ExportPath("csv")}&list=newsletter", "Newsletter-Liste"));
        }

        return options;
    }

    private string ExportPath(string format) => $"api/customers/export?format={format}&search={Uri.EscapeDataString(Query.Trim())}";

    // ---------- Meldung unten („Gespeichert: Kunden_2026-11-09.xlsx“), eine Zeile, 5 Sekunden, die Maus darüber hält sie an (meldungen.md M1) ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToast))]
    public partial string? Toast { get; private set; }

    public bool HasToast => Toast is not null;

    private void ShowToast(string text)
    {
        Toast = text;
        StartToastClock();
    }

    [RelayCommand]
    private void CloseToast() => Toast = null;

    public void HoldToast(bool held)
    {
        _toastHeld = held;
        StartToastClock();
    }

    private void StartToastClock()
    {
        var clock = ++_toastClock;
        if (Toast is not null && !_toastHeld)
        {
            _ = ExpireToastAsync(clock);
        }
    }

    private async Task ExpireToastAsync(int clock)
    {
        await Task.Delay(ToastDuration, time).ConfigureAwait(true);
        if (clock == _toastClock)
        {
            Toast = null;
        }
    }

    // ---------- Werbe-Einwilligung: Stand aus der bestehenden Einwilligung (ADR 0040), am Empfang als Ja, Nein oder nicht gefragt erfasst ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConsentText), nameof(ConsentSectionVisible))]
    public partial MarketingConsentDto? Consent { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny), nameof(ConsentSectionVisible))]
    public partial bool IsEditingConsent { get; private set; }

    [ObservableProperty]
    public partial ConsentChoice ConsentChoice { get; set; } = ConsentChoice.All[2];

    public IReadOnlyList<ConsentChoice> ConsentChoices { get; } = ConsentChoice.All;

    public bool ConsentSectionVisible => Consent is not null || IsEditingConsent;

    /// <summary>
    /// Die Quelle getrennt lesbar, weil sie der Nachweis ist (Art. 7 Abs. 1 DSGVO, ADR 0040): „Ja, am Empfang erfasst am 9.11.2026 von Lena“ gegen „Ja, per Mail bestätigt am …“
    /// oder „Ja, aus dem Import übernommen am …“; dazu „Nein, am Empfang erfasst am …“, „Angefragt am …“, „Widerrufen am …“ oder „Nicht gefragt“.
    /// </summary>
    public string ConsentText => Consent switch
    {
        null => "",
        { State: MarketingConsentState.Confirmed, Source: ReceptionSource } c => $"Ja, am Empfang erfasst am {Day(c.ConfirmedAt)}{By(c)}",
        { State: MarketingConsentState.Confirmed, Source: ImportSource } c => $"Ja, aus dem Import übernommen am {Day(c.ConfirmedAt)}",
        { State: MarketingConsentState.Confirmed } c => $"Ja, per Mail bestätigt am {Day(c.ConfirmedAt)} ({c.Email})",
        { State: MarketingConsentState.Refused, Source: ReceptionSource } c => $"Nein, am Empfang erfasst am {Day(c.WithdrawnAt)}{By(c)}",
        { State: MarketingConsentState.Refused } c => $"Nein, seit {Day(c.WithdrawnAt)}",
        { State: MarketingConsentState.Pending } c => $"Angefragt am {Day(c.RequestedAt)}, wartet auf die Bestätigung durch den Gast ({c.Email})",
        { State: MarketingConsentState.Withdrawn } c => $"Widerrufen am {Day(c.WithdrawnAt)}",
        _ => "Nicht gefragt",
    };

    /// <summary>Quellen wie in der API (<c>MarketingConsents.SourceReception</c> und <c>SourceImport</c>).</summary>
    private const string ReceptionSource = "Rezeption";

    private const string ImportSource = "Import";

    private static string By(MarketingConsentDto consent) => consent.RecordedBy is { Length: > 0 } name ? $" von {name}" : "";

    private string Day(DateTimeOffset? moment) => moment is { } value ? TimeZoneInfo.ConvertTime(value, time.LocalTimeZone).ToString("d.M.yyyy", CultureInfo.InvariantCulture) : "–";

    private async Task LoadConsentAsync(CustomerDto? customer)
    {
        Consent = null;
        if (customer is null)
        {
            return;
        }

        var version = _detailVersion;
        var result = await api.GetAsync<MarketingConsentDto>($"api/customers/{customer.Id}/marketing-consent").ConfigureAwait(true);
        if (version == _detailVersion && Selected?.Id == customer.Id)
        {
            Consent = result.Value;
        }
    }

    [RelayCommand]
    private void EditConsent()
    {
        if (Selected is null)
        {
            return;
        }

        CloseEditors();
        Error = null;
        ConsentChoice = Consent?.State switch
        {
            MarketingConsentState.Confirmed => ConsentChoice.All[0],
            MarketingConsentState.Refused or MarketingConsentState.Withdrawn => ConsentChoice.All[1],
            _ => ConsentChoice.All[2],
        };
        IsEditingConsent = true;
    }

    [RelayCommand]
    private void CancelConsent() => IsEditingConsent = false;

    [RelayCommand]
    private async Task SaveConsentAsync()
    {
        if (Selected is not { } customer)
        {
            return;
        }

        if (ConsentChoice.State == MarketingConsentState.Confirmed && Consent?.State != MarketingConsentState.Confirmed && !await ConfirmConsentGivenAsync(customer))
        {
            return;
        }

        var result = await api.PutAsync<MarketingConsentDto>($"api/customers/{customer.Id}/marketing-consent", new MarketingConsentDecision(ConsentChoice.State)).ConfigureAwait(true);
        if (result.Value is not { } consent)
        {
            Error = result.Error ?? "Die Einwilligung wurde nicht gespeichert. Verbindung prüfen und erneut versuchen.";
            return;
        }

        Consent = consent;
        IsEditingConsent = false;
        ShowToast("Werbe-Einwilligung gespeichert");
    }

    /// <summary>„Ja“ am Empfang ersetzt die Bestätigung per Mail; die Rückfrage hält fest, dass der Gast mündlich oder schriftlich zugestimmt hat (meldungen.md, Dialog).</summary>
    private async Task<bool> ConfirmConsentGivenAsync(CustomerDto customer)
    {
        var address = string.IsNullOrWhiteSpace(customer.Email) ? "" : $" an {customer.Email}";
        var answer = await AskAsync(new ConfirmRequest("Hat der Gast zugestimmt?",
            $"{customer.FirstName} {customer.LastName} steht danach in der Newsletter-Liste und bekommt Werbung per Mail{address}. Nur erfassen, wenn der Gast mündlich oder schriftlich zugestimmt hat.",
            "Erfassen", IsDestructive: false)).ConfigureAwait(true);
        return answer.IsConfirmed;
    }
}
