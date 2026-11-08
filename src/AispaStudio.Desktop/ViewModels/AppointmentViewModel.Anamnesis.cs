using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Antwort des Bogens in der Karte; <c>IsFlagged</c> hebt ein „Ja“ hervor.</summary>
public sealed record AnamnesisAnswerRow(string Question, string Answer, bool IsFlagged);

/// <summary>Ein fotografierter Bogen, der noch gelesen wird oder auf Prüfung wartet.</summary>
public sealed record AnamnesisScanRow(string Text, bool CanReview, IRelayCommand Review);

// ---------- Anamnese am Termin (Ä300, Ä304): Stand mit ViewCalendar, Antworten und Prüfen mit ViewHealthData, Abläufe mit Book; der Server prüft alles ----------
public partial class AppointmentViewModel
{
    private const long MaxPhotoBytes = 50 * 1024 * 1024;

    private const string PhotoRejected = "Foto nicht angenommen. Erlaubt ist ein Foto als JPEG, PNG oder WebP bis 5 MB.";

    private const string PhotoFailed = "Foto konnte nicht hochgeladen werden.";

    private const string PhotoWaits = "Foto hochgeladen. Prüfen kann, wer Anamnesen lesen darf";

    private static readonly string[] PhotoPatterns = ["*.jpg", "*.jpeg", "*.png", "*.webp"];

    /// <summary>Kurztitel für die Übersicht im Termin; der volle Fragetext steht im Bogen.</summary>
    private static readonly Dictionary<string, string> AnamnesisQuestions = new()
    {
        ["allergies"] = "Allergien",
        ["skin"] = "Haut",
        ["circulation"] = "Herz/Kreislauf",
        ["pregnancy"] = "Schwanger/stillend",
        ["medication"] = "Medikamente",
        ["surgery"] = "OP/Verletzung",
        ["other"] = "Sonstiges",
    };

    private string AnamnesisPath => $"api/customers/{Appointment.CustomerId}/anamnesis";

    /// <summary>Der Dateidialog für das Foto des Bogens.</summary>
    public IFilePicker? Files { get; init; }

    /// <summary>Antworten lesen und fotografierte Bögen prüfen (<see cref="AccessRight.ViewHealthData"/>); ohne das Recht nur den Stand.</summary>
    public bool MayReadHealthData { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnamnesis), nameof(AnamnesisStateText), nameof(HasAnamnesisRecord), nameof(CanReadAnamnesisRecord), nameof(AnamnesisScans))]
    public partial AnamnesisOverview? AnamnesisOverview { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecordText), nameof(ShowRecordName))]
    public partial bool ShowAnamnesisRecord { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<AnamnesisAnswerRow> AnamnesisAnswers { get; private set; } = [];

    [ObservableProperty]
    public partial ConsentLanguage AnamnesisLanguage { get; set; } = ConsentLanguage.All[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnamnesisMessage))]
    public partial string? AnamnesisMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTabletLink), nameof(TabletLinkUrl), nameof(TabletLinkValidity))]
    public partial AnamnesisTabletLink? TabletLink { get; private set; }

    [ObservableProperty]
    public partial Bitmap? TabletQr { get; private set; }

    public bool HasAnamnesis => AnamnesisOverview is not null;

    public bool HasAnamnesisRecord => AnamnesisOverview?.LastSubmittedAt is not null;

    public bool CanReadAnamnesisRecord => MayReadHealthData && HasAnamnesisRecord;

    public bool HasAnamnesisMessage => AnamnesisMessage is not null;

    public bool HasTabletLink => TabletLink is not null;

    /// <summary>Tablet, Papier und Foto brauchen das Recht zum Buchen; der Server prüft es ebenso.</summary>
    public bool CanRunAnamnesisFlows => MayBook;

    public string ShowRecordText => ShowAnamnesisRecord ? "Ausblenden" : "Ansehen";

    public string ShowRecordName => ShowAnamnesisRecord ? "Antworten ausblenden" : "Antworten ansehen";

    public string TabletLinkUrl => TabletLink?.Url ?? "";

    public string TabletLinkValidity => TabletLink is { } link
        ? $"Gültig bis {DayLayout.Local(link.ExpiresAt, Zone):HH:mm} Uhr, nur einmal."
        : "";

    public string AnamnesisStateText => AnamnesisOverview switch
    {
        null => "",
        { LastSubmittedAt: { } submitted } overview =>
            $"{(overview.ValidUntil > Time.GetUtcNow() ? "Liegt vor" : "Abgelaufen")}: {ChannelText(overview.Channel)} am {Day(submitted)}"
            + (overview.ValidUntil is { } until ? $", gültig bis {Day(until)}." : "."),
        _ => "Fehlt noch.",
    };

    public IReadOnlyList<AnamnesisScanRow> AnamnesisScans => AnamnesisOverview is { } overview
        ? [.. overview.OpenScans.Select(s => new AnamnesisScanRow(
            $"Papierbogen vom {Day(s.UploadedAt)} {(s.Status == AnamnesisScanStatus.Reading ? "wird gelesen" : "wartet auf Prüfung")}.",
            MayReadHealthData, new AsyncRelayCommand(() => ReviewScanAsync(s.Id))))]
        : [];

    private static string ChannelText(string? channel) => channel switch
    {
        "Link" => "online",
        "Tablet" => "am Tablet",
        "Papier" => "auf Papier",
        _ => channel ?? "",
    };

    /// <summary>Erstes Element der Wege Tablet, Papier und Foto: Sprungziel für „schick … den Bogen“ (T-022, Ä385).</summary>
    public const string AnamnesisLanguageBox = "AnamnesisLanguageBox";

    /// <summary>Die Ladeaufrufe beim Öffnen (Anzahlung, Garantie, Erinnerungen, Einwilligung, Anamnese, Empfehlungen); der Kalender setzt sie.</summary>
    public Task Loads { get; internal set; } = Task.CompletedTask;

    /// <summary>
    /// „schick … den Bogen“ (T-022, Ä385): wartet, bis alle Karten des Panels geladen sind (sonst schöbe eine später sichtbare Karte darüber das Ziel wieder
    /// hinaus), und springt dann zum Abschnitt Anamnese.
    /// </summary>
    public async Task ShowAnamnesisAsync()
    {
        await Loads.ConfigureAwait(true);
        FocusRequested?.Invoke(AnamnesisLanguageBox);
    }

    /// <summary>Lädt den Stand; bei einem Fehler zeigt das Panel die Karte nicht.</summary>
    public async Task LoadAnamnesisAsync() =>
        AnamnesisOverview = (await Api.GetAsync<AnamnesisOverview>($"{AnamnesisPath}/overview").ConfigureAwait(true)).Value;

    /// <summary>Holt den Bogen erst auf Klick: Wer ihn nicht öffnet, lädt keine Gesundheitsdaten.</summary>
    [RelayCommand]
    private async Task ToggleAnamnesisRecordAsync()
    {
        if (ShowAnamnesisRecord)
        {
            ShowAnamnesisRecord = false;
            AnamnesisAnswers = [];
            return;
        }

        var result = await Api.GetAsync<AnamnesisRecordDto>(AnamnesisPath).ConfigureAwait(true);
        if (result.Value is not { } record)
        {
            ShowFailure("Bogen nicht geladen", result.Error);
            return;
        }

        AnamnesisAnswers = [.. record.Answers.Select(a => new AnamnesisAnswerRow(
            AnamnesisQuestions.GetValueOrDefault(a.Key, a.Key), AnswerText(a), a.Yes == true))];
        ShowAnamnesisRecord = true;
    }

    private static string AnswerText(AnamnesisAnswer answer)
    {
        var yesNo = answer.Yes switch { true => "Ja", false => "Nein", null => "" };
        return (yesNo, answer.Details) switch
        {
            ("", null or "") => "–",
            ("", { } details) => details,
            (_, null or "") => yesNo,
            (_, { } details) => $"{yesNo}: {details}",
        };
    }

    [RelayCommand]
    private async Task CreateTabletLinkAsync()
    {
        AnamnesisMessage = null;
        var result = await Api.PostAsync<AnamnesisTabletLink>($"{AnamnesisPath}/tablet", new AnamnesisLinkRequest(Appointment.Id, AnamnesisLanguage.Code)).ConfigureAwait(true);
        if (result.Value is not { } link)
        {
            AnamnesisMessage = "Tablet-Link konnte nicht erstellt werden.";
            return;
        }

        TabletQr?.Dispose();
        TabletQr = QrCodeImage.Create(link.Url);
        TabletLink = link;
    }

    [RelayCommand]
    private void CloseTabletLink()
    {
        TabletLink = null;
        var qr = TabletQr;
        TabletQr = null;
        qr?.Dispose();
    }

    [RelayCommand]
    private async Task OpenTabletLinkAsync()
    {
        if (TabletLink is { } link && ContactActions() is { } actions)
        {
            await actions.LaunchAsync(new Uri(link.Url)).ConfigureAwait(true);
        }
    }

    /// <summary>Der Bogen kommt aus der API; der Standardbrowser zeigt ihn und öffnet den Druckdialog.</summary>
    [RelayCommand]
    private async Task PrintPaperSheetAsync()
    {
        AnamnesisMessage = null;
        var result = await Api.PostAsync<AnamnesisPaperSheet>($"{AnamnesisPath}/paper", new AnamnesisLinkRequest(Appointment.Id, AnamnesisLanguage.Code)).ConfigureAwait(true);
        if (result.Value is not { } sheet || ContactActions() is not { } actions)
        {
            AnamnesisMessage = "Papierbogen konnte nicht erstellt werden.";
            return;
        }

        try
        {
            await actions.LaunchAsync(PrintFile.Write(PaperSheetDocument.Html(sheet), Time)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AnamnesisMessage = "Papierbogen konnte nicht geöffnet werden.";
        }
    }

    [RelayCommand]
    private async Task UploadPhotoAsync()
    {
        if (Files is null)
        {
            return;
        }

        try
        {
            if (await Files.PickBytesAsync("Foto des ausgefüllten Bogens", "Foto", PhotoPatterns, MaxPhotoBytes).ConfigureAwait(true) is not { } photo)
            {
                return;
            }

            if (photo.Length == 0)
            {
                AnamnesisMessage = PhotoRejected;
                return;
            }

            AnamnesisMessage = "Foto wird hochgeladen …";
            var shrunk = await Task.Run(() => PhotoShrinker.Shrink(photo)).ConfigureAwait(true);
            using var body = new MultipartFormDataContent();
            var file = new ByteArrayContent(shrunk);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            body.Add(file, "file", "bogen.jpg");
            body.Add(new StringContent(Appointment.Id.ToString("D", CultureInfo.InvariantCulture)), "appointmentId");
            body.Add(new StringContent(AnamnesisLanguage.Code), "language");
            var result = await Api.PostAsync<AnamnesisScanDto>($"{AnamnesisPath}/scans", body).ConfigureAwait(true);
            if (result.Value is not { } scan)
            {
                AnamnesisMessage = result.Status == HttpStatusCode.BadRequest ? PhotoRejected : PhotoFailed;
                return;
            }

            AnamnesisMessage = null;
            await LoadAnamnesisAsync().ConfigureAwait(true);
            if (!MayReadHealthData)
            {
                ShowToast?.Invoke(PhotoWaits);
                return;
            }

            await ReviewScanAsync(scan.Id).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AnamnesisMessage = PhotoFailed;
        }
    }

    /// <summary>Der Vorschlag der KI gilt erst, wenn jemand aus dem Team ihn übernimmt; Schließen speichert nichts.</summary>
    private async Task ReviewScanAsync(Guid scanId)
    {
        if (Dialogs is not { } dialogs)
        {
            return;
        }

        ScanReviewDialogViewModel? dialog = null;
        var answer = await dialogs.ShowAsync(done =>
        {
            dialog = new ScanReviewDialogViewModel(Api, AnamnesisPath, scanId, Time, done);
            _ = dialog.LoadAsync();
            return dialog;
        }).ConfigureAwait(true);
        dialog?.Dispose();
        if (answer.Choice == ConfirmChoice.Cancelled)
        {
            return;
        }

        ShowAnamnesisRecord = false;
        AnamnesisAnswers = [];
        ShowToast?.Invoke(answer.IsConfirmed ? "Anamnese vom Papierbogen übernommen" : "Foto verworfen");
        await LoadAnamnesisAsync().ConfigureAwait(true);
    }
}
