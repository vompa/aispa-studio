using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Kartengarantie und Erinnerungsverlauf (Ä285) ----------
public partial class AppointmentViewModel
{
    private const int StatusPolls = 40;

    private static readonly TimeSpan StatusPollInterval = TimeSpan.FromSeconds(1.5);

    /// <summary>Kassenaufsicht: nur sie sieht „Ausfallgebühr belasten“. Der Server prüft es bei jedem Aufruf, die Oberfläche blendet nur aus.</summary>
    public bool MayChargeFee { get; init; }

    private string CardPath => $"api/appointments/{Appointment.Id}/card-guarantee";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCardSection), nameof(HasCardState), nameof(CardStateText), nameof(CardLines), nameof(CanRequestCard), nameof(RequestCardText), nameof(CanChargeFee))]
    public partial CardGuaranteeStaffDto? CardGuarantee { get; private set; }

    /// <summary>Die Karte lässt sich per Link anfordern; ob und mit welcher Gebühr, sagt der Server.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCardSection), nameof(CardLines), nameof(CanRequestCard))]
    public partial CardRequestOffer? CardOffer { get; private set; }

    /// <summary>Bis wann der Gast ohne Gebühr absagt, aus der Hinterlegung oder dem Angebot des Servers; für den Hinweis in der Storno-Rückfrage (L3).</summary>
    public DateTimeOffset? CancellationDeadlineAt { get; private set; }

    /// <summary>Wirksame Ausfallgebühr in Prozent nach den Regeln des Servers (Behandlung vor Standort, „nur neue Gäste“, Zahlungsanbieter); 0 ohne Regel.</summary>
    public int LateCancelFeePercent { get; private set; }

    public bool HasCardSection => CardGuarantee is not null || CardOffer is not null;

    public bool HasCardState => CardGuarantee is not null;

    /// <summary>„Karte hinterlegt · 27,00 € (30 %) seit 19.10.2026 09:00“.</summary>
    public string CardStateText => CardGuarantee is { } card
        ? $"{CardStatusText(card)} · {Money.Format(card.FeeAmount, card.Currency)} ({card.FeePercent} %){CardSinceText(card)}"
        : "";

    /// <summary>Hinweise unter dem Stand; Regeln und Begründungen stammen vom Server.</summary>
    public IReadOnlyList<string> CardLines
    {
        get
        {
            if (CardGuarantee is not { } card)
            {
                return CardOffer is { } offer
                    ? [$"Der Standort verlangt {offer.FeePercent} % ({Money.Format(offer.FeeAmount, offer.Currency)}). Der Gast bekommt einen Link, stimmt zu und hinterlegt eine Karte."]
                    : [];
            }

            List<string> lines = [];
            AddIfPresent(lines, card.Hint);
            if (card.ReleaseAt is { } release)
            {
                lines.Add($"Freigabe ohne Karte am {Stamp(release)}");
            }

            AddIfPresent(lines, card.Error);
            if (card.Status == CardGuaranteeStatus.Charged)
            {
                lines.Add("Beleg an der Kassa erstellt, der Gast bekommt ihn per Mail.");
            }

            return lines;
        }
    }

    public bool CanRequestCard => MayBook && IsBooked && (CardGuarantee is { } card ? card.Status == CardGuaranteeStatus.AwaitingConsent : CardOffer is not null);

    public string RequestCardText => CardGuarantee is null ? "Karte per Link anfordern" : "Link erneut senden";

    public bool CanChargeFee => MayChargeFee && CardGuarantee is { CanCharge: true };

    /// <summary>Lädt die Hinterlegung; ohne sie bei einem gebuchten Termin das Angebot. Ohne beides oder bei einem Fehler zeigt das Panel nichts.</summary>
    public async Task LoadCardGuaranteeAsync()
    {
        CardGuarantee = (await Api.GetAsync<CardGuaranteeStaffDto>(CardPath).ConfigureAwait(true)).Value;
        if (CardGuarantee is { } card)
        {
            CancellationDeadlineAt = card.CancellationDeadlineAt;
            LateCancelFeePercent = card.FeePercent;
        }
        else if (IsBooked && MayBook)
        {
            var offer = await Api.GetAsync<CardRequestOffer>($"{CardPath}/offer").ConfigureAwait(true);
            CancellationDeadlineAt = offer.Value?.CancellationDeadlineAt;
            LateCancelFeePercent = offer.Value?.FeePercent ?? 0;
            CardOffer = offer.Value is { Possible: true } possible ? possible : null;
        }

        await PollChargingAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RequestCardAsync()
    {
        var result = await Api.PostAsync<CardGuaranteeStaffDto>($"{CardPath}/request", null).ConfigureAwait(true);
        if (result.Value is not { } card)
        {
            ShowFailure("Link nicht verschickt", result.Error);
            return;
        }

        CardGuarantee = card;
        ShowToast?.Invoke("Link per Mail verschickt");
    }

    /// <summary>„Ausfallgebühr belasten“ fragt nach (Rückfragedialog, Design-Brief 5.27); erst „Belasten“ bucht von der Karte ab.</summary>
    [RelayCommand]
    private async Task ChargeFeeAsync()
    {
        if (CardGuarantee is not { CanCharge: true } card)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Ausfallgebühr belasten?",
            $"{Money.Format(card.FeeAmount, card.Currency)} werden von der hinterlegten Karte abgebucht. Der Beleg entsteht an der Kassa.", "Belasten")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await Api.PostAsync<CardGuaranteeStaffDto>($"{CardPath}/charge", null).ConfigureAwait(true);
        if (result.Value is not { } charged)
        {
            ShowFailure("Ausfallgebühr nicht belastet", result.Error);
            return;
        }

        CardGuarantee = charged;
        await PollChargingAsync().ConfigureAwait(true);
    }

    /// <summary>Das Belasten läuft im Hintergrund; solange fragt das Panel nach, bis der Server einen Stand meldet.</summary>
    private async Task PollChargingAsync()
    {
        for (var i = 0; i < StatusPolls && CardGuarantee?.Status == CardGuaranteeStatus.Charging; i++)
        {
            await Task.Delay(StatusPollInterval).ConfigureAwait(true);
            if ((await Api.GetAsync<CardGuaranteeStaffDto>(CardPath).ConfigureAwait(true)).Value is not { } current)
            {
                return;
            }

            CardGuarantee = current;
        }
    }

    private static string CardStatusText(CardGuaranteeStaffDto card) => card switch
    {
        { Status: CardGuaranteeStatus.Saved, CardRemovedAt: not null } => "Karte gelöscht",
        { Status: CardGuaranteeStatus.Pending } => "Karte noch nicht hinterlegt",
        { Status: CardGuaranteeStatus.Saved } => "Karte hinterlegt",
        { Status: CardGuaranteeStatus.Failed } => "Hinterlegen abgebrochen",
        { Status: CardGuaranteeStatus.Charging } => "Gebühr wird belastet …",
        { Status: CardGuaranteeStatus.Charged } => "Gebühr belastet",
        { Status: CardGuaranteeStatus.ChargeFailed } => "Belastung fehlgeschlagen",
        { Status: CardGuaranteeStatus.Refunded } => "Gebühr erstattet",
        { Status: CardGuaranteeStatus.AwaitingConsent } => "Link verschickt, Zustimmung offen",
        _ => "Termin freigegeben (keine Karte)",
    };

    private string CardSinceText(CardGuaranteeStaffDto card) => card switch
    {
        { CardRemovedAt: { } removed } => $" am {Stamp(removed)}",
        { SavedAt: { } saved } => $" seit {Stamp(saved)}",
        _ => "",
    };

    // ---------- Erinnerungsverlauf ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReminders))]
    public partial IReadOnlyList<string> Reminders { get; private set; } = [];

    public bool HasReminders => Reminders.Count > 0;

    /// <summary>Welche Erinnerungen der Gast zu diesem Termin bekommen hat; ohne Erinnerungen oder bei einem Fehler keine Karte.</summary>
    public async Task LoadRemindersAsync()
    {
        var result = await Api.GetAsync<List<AppointmentReminderDto>>($"api/appointments/{Appointment.Id}/reminders").ConfigureAwait(true);
        Reminders = [.. (result.Value ?? []).Select(ReminderLine)];
    }

    /// <summary>„1. per Mail: gesendet 19.10.2026 09:00“; ein Fehler steht in Klammern dahinter, solange die Erinnerung nicht gesendet ist.</summary>
    private string ReminderLine(AppointmentReminderDto reminder)
    {
        var channel = reminder.Channel switch
        {
            ReminderChannel.Email => "Mail",
            ReminderChannel.Sms => "SMS",
            _ => "WhatsApp",
        };
        var state = reminder.State switch
        {
            "Queued" => "wird gesendet",
            "Sent" => "gesendet",
            "Skipped" => "entfallen",
            "Failed" => "fehlgeschlagen",
            _ => reminder.State,
        };
        var sent = reminder.SentAt is { } at ? $" {Stamp(at)}" : "";
        var error = reminder.Error is { } text && reminder.State != "Sent" ? $" ({text})" : "";
        return $"{reminder.Stage}. per {channel}: {state}{sent}{error}";
    }

    // ---------- Gemeinsam ----------

    private string Stamp(DateTimeOffset moment) => $"{DayLayout.Local(moment, Zone):dd.MM.yyyy HH:mm}";

    private static void AddIfPresent(List<string> lines, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            lines.Add(text);
        }
    }

    private void ShowFailure(string title, string? error)
    {
        if (ReportProblem is { } report)
        {
            report(title, error);
        }
        else
        {
            Error = error;
        }
    }
}
