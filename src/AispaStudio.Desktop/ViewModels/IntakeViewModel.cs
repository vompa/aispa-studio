using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Art des Postfachs in der Auswahl.</summary>
public sealed record MailboxKindOption(MailboxKind Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Verschlüsselung der Verbindung in der Auswahl.</summary>
public sealed record SecurityOption(MailboxSecurity Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Postfach und Nummern (R05 Slice 97), native Maske nach <c>IntakeTab.tsx</c>: das eigene Postfach des Spas für die Terminannahme (IMAP oder Microsoft 365), Antworten aus diesem Postfach,
/// „Jetzt prüfen“, „Postfach entfernen“, die Nummern des Spas (nur lesend, der Betreiber vergibt sie) und darunter die Absenderregeln (<see cref="SenderRules"/>, eigene Speicherebene).
/// Das Passwort bzw. Client-Geheimnis wird nur geschrieben, nie angezeigt; leer heißt „unverändert“.
/// </summary>
public sealed partial class IntakeViewModel : ViewModelBase, INativeArea
{
    private const string SecretKept = "gespeichert, leer lassen zum Behalten";
    private const int CheckAttempts = 20;

    private static readonly string[] Inputs =
    [
        nameof(Enabled), nameof(Kind), nameof(Address), nameof(Host), nameof(Port), nameof(Security), nameof(UserName), nameof(Folder), nameof(DirectoryTenantId),
        nameof(ClientId), nameof(Secret), nameof(SendReplies), nameof(SmtpHost), nameof(SmtpPort), nameof(SmtpSecurity), nameof(SaveSentCopy), nameof(SentFolder),
    ];

    private static readonly Regex SoftSendNotice = new("^(Gesendet,|Senden klappt,)", RegexOptions.CultureInvariant);

    private readonly IWorkstationApi _api;
    private readonly TimeZoneInfo _zone;
    private readonly TimeSpan _checkInterval;
    private SpaMailboxDto? _box;
    private string? _processedFolder;
    private string? _savedSignature;
    private bool _applying;

    public IntakeViewModel(IWorkstationApi api, TimeZoneInfo zone, TimeSpan? checkInterval = null)
    {
        _api = api;
        _zone = zone;
        _checkInterval = checkInterval ?? TimeSpan.FromMilliseconds(750);
        SenderRules = new SenderRulesViewModel(api, AskAsync);
        Kind = MailboxKinds[0];
        Security = SecurityOptions[0];
        SmtpSecurity = SecurityOptions[1];
    }

    public string Title => "Postfach & Nummern";

    public static IReadOnlyList<MailboxKindOption> MailboxKinds { get; } =
        [new(MailboxKind.Imap, "IMAP (z. B. beim Hoster)"), new(MailboxKind.Graph, "Microsoft 365")];

    public static IReadOnlyList<SecurityOption> SecurityOptions { get; } =
    [
        new(MailboxSecurity.SslOnConnect, "SSL/TLS"),
        new(MailboxSecurity.StartTls, "STARTTLS"),
        new(MailboxSecurity.None, "Keine (nur Test)"),
    ];

    public SenderRulesViewModel SenderRules { get; }

    /// <summary>Die Nummern des Spas, je Zeile „Kanal: Adresse“.</summary>
    public ObservableCollection<string> Routes { get; } = [];

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Ein Postfach ist gespeichert (nicht nur eingetippt).</summary>
    [ObservableProperty]
    public partial bool HasMailbox { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CheckText))]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    public partial bool IsChecking { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    [ObservableProperty]
    public partial bool Enabled { get; set; } = true;

    [ObservableProperty]
    public partial MailboxKindOption Kind { get; set; }

    [ObservableProperty]
    public partial string Address { get; set; } = "";

    [ObservableProperty]
    public partial string Host { get; set; } = "";

    [ObservableProperty]
    public partial decimal? Port { get; set; } = 993;

    [ObservableProperty]
    public partial SecurityOption Security { get; set; }

    [ObservableProperty]
    public partial string UserName { get; set; } = "";

    [ObservableProperty]
    public partial string Folder { get; set; } = "";

    [ObservableProperty]
    public partial string DirectoryTenantId { get; set; } = "";

    [ObservableProperty]
    public partial string ClientId { get; set; } = "";

    [ObservableProperty]
    public partial string Secret { get; set; } = "";

    [ObservableProperty]
    public partial bool SendReplies { get; set; }

    [ObservableProperty]
    public partial string SmtpHost { get; set; } = "";

    [ObservableProperty]
    public partial decimal? SmtpPort { get; set; } = 587;

    [ObservableProperty]
    public partial SecurityOption SmtpSecurity { get; set; }

    [ObservableProperty]
    public partial bool SaveSentCopy { get; set; } = true;

    [ObservableProperty]
    public partial string SentFolder { get; set; } = "";

    public bool IsImap => Kind.Value == MailboxKind.Imap;

    public bool IsGraph => !IsImap;

    public string SecretLabel => IsImap ? "Passwort" : "Client-Geheimnis";

    /// <summary>Hinweis im leeren Feld: ein gespeichertes Geheimnis gilt nur für dieselbe Art.</summary>
    public string SecretPlaceholder => _box is { HasSecret: true } box && box.Kind == Kind.Value ? SecretKept : "";

    public bool ShowSmtp => SendReplies && IsImap;

    public bool ShowSentFolder => ShowSmtp && SaveSentCopy;

    public string RepliesHint => !SendReplies
        ? "Sonst gehen sie über den gemeinsamen Versand, mit dem Namen des Spas als Absender. Antworten der Gäste kommen trotzdem in dieses Postfach."
        : IsImap
            ? "Gesendet wird über den SMTP-Server des Postfachs, mit demselben Benutzer und Passwort."
            : "Die App in Microsoft 365 braucht dafür zusätzlich die Berechtigung Mail.Send. Gesendete Mails liegen dann unter „Gesendete Elemente“.";

    /// <summary>Stand des letzten Abrufs über den Feldern; ohne gespeichertes Postfach gibt es ihn nicht.</summary>
    public string? StatusLine => _box switch
    {
        null => null,
        { LastError: { Length: > 0 } error } => $"Letzter Abruf fehlgeschlagen: {error}",
        { LastCheckedAt: { } at } => $"Zuletzt abgerufen {TimeZoneInfo.ConvertTime(at, _zone).ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture)}.",
        _ => "Noch nicht abgerufen.",
    };

    public bool HasStatusLine => StatusLine is not null;

    public bool StatusIsWarning => _box is { LastError: { Length: > 0 } };

    /// <summary>
    /// Stand des letzten Versands, nur wenn es etwas zu sagen gibt. Fehlt nur die Ablage unter „Gesendet“, ist die Mail trotzdem angekommen; der Server sagt das im Text selbst.
    /// </summary>
    public string? SendNotice => _box is { LastSendError: { Length: > 0 } error }
        ? SoftSendNotice.IsMatch(error) ? error : $"Letzter Versand fehlgeschlagen: {error}"
        : null;

    public bool HasSendNotice => SendNotice is not null;

    public bool HasRoutes => Routes.Count > 0;

    public string CheckText => IsChecking ? "Prüfe …" : "Jetzt prüfen";

    /// <summary>Prüfen geht nur mit einem gespeicherten, eingeschalteten Postfach.</summary>
    public bool CanCheck => _box is { Enabled: true } && !IsChecking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert. …“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Rückmeldung zu „Jetzt prüfen“ und „Postfach entfernen“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; private set; } = InfoBarSeverity.Info;

    public bool HasMessage => Message is not null;

    public bool HasUnsavedChanges => _savedSignature is not null && Signature() != _savedSignature;

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var box = await _api.GetAsync<SpaMailboxDto>("api/intake/mailbox").ConfigureAwait(true);
        var routes = box.Ok ? await _api.GetAsync<List<IntakeRouteDto>>("api/intake/routes").ConfigureAwait(true) : null;
        if (!box.Ok || routes is not { Ok: true })
        {
            LoadError = box.Error ?? routes?.Error ?? "Das Postfach wurde nicht geladen.";
            return;
        }

        ApplyBox(box.Value);
        Routes.Clear();
        foreach (var route in routes.Value ?? [])
        {
            Routes.Add($"{ChannelLabel(route.Channel)}: {route.Address}");
        }

        OnPropertyChanged(nameof(HasRoutes));
        await SenderRules.LoadAsync().ConfigureAwait(true);
        IsLoaded = true;
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving)
        {
            return false;
        }

        Error = null;
        Message = null;
        IsSaving = true;
        var result = await _api.PutAsync<SpaMailboxDto>("api/intake/mailbox", Input()).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Das Postfach wurde nicht gespeichert.";
            return false;
        }

        ApplyBox(saved);
        StatusText = "Gespeichert. Das Postfach wird beim nächsten Abruf gelesen.";
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        ApplyBox(_box);
        Error = null;
    }

    /// <summary>Sofort abrufen (und den Versand prüfen); das Ergebnis kommt über die Queue, deshalb wird kurz nachgefragt.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        var before = _box?.LastCheckedAt;
        IsChecking = true;
        Message = null;
        var started = await _api.PostAsync<object>("api/intake/mailbox/check", null).ConfigureAwait(true);
        if (!started.Ok)
        {
            IsChecking = false;
            Notify("Prüfen geht nur mit einem eingeschalteten Postfach.", InfoBarSeverity.Warning);
            return;
        }

        for (var attempt = 0; attempt < CheckAttempts; attempt++)
        {
            await Task.Delay(_checkInterval).ConfigureAwait(true);
            var current = await _api.GetAsync<SpaMailboxDto>("api/intake/mailbox").ConfigureAwait(true);
            if (current.Value is { } data && data.LastCheckedAt != before)
            {
                _box = data;
                IsChecking = false;
                RefreshStatus();
                var failed = !string.IsNullOrEmpty(data.LastError) || !string.IsNullOrEmpty(data.LastSendError);
                Notify(failed ? "Geprüft, siehe Fehler oben." : "Geprüft: das Postfach antwortet.", failed ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
                return;
            }
        }

        IsChecking = false;
        Notify("Noch kein Ergebnis. Der Stand oben aktualisiert sich beim nächsten Öffnen.", InfoBarSeverity.Info);
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        var answer = await AskAsync(new ConfirmRequest("Postfach entfernen?",
            $"Das Postfach wird nicht mehr abgerufen, das gespeicherte {SecretLabel} wird gelöscht. Bereits eingegangene Nachrichten bleiben im Posteingang.", "Entfernen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _api.DeleteAsync<object>("api/intake/mailbox").ConfigureAwait(true);
        if (!result.Ok)
        {
            Notify(result.Error ?? "Das Postfach wurde nicht entfernt.", InfoBarSeverity.Error);
            return;
        }

        ApplyBox(null);
        Error = null;
        Notify("Postfach entfernt.", InfoBarSeverity.Success);
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseMessage() => Message = null;

    /// <summary>Das Postfach hat keine Abschnitte, Einträge oder Felder, zu denen Kalender oder Assistent springen.</summary>
    public void Open(Guid? id, string? section, string? field)
    {
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applying || e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        if (name == nameof(Kind))
        {
            // Ein eingetipptes Geheimnis gehört zur Art; ein gespeichertes gilt nur für dieselbe.
            Secret = "";
            OnPropertyChanged(nameof(IsImap));
            OnPropertyChanged(nameof(IsGraph));
            OnPropertyChanged(nameof(SecretLabel));
            OnPropertyChanged(nameof(SecretPlaceholder));
        }

        if (name is nameof(Kind) or nameof(SendReplies) or nameof(SaveSentCopy))
        {
            OnPropertyChanged(nameof(ShowSmtp));
            OnPropertyChanged(nameof(ShowSentFolder));
            OnPropertyChanged(nameof(RepliesHint));
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Setzt die Felder auf ein gespeichertes Postfach oder, ohne, auf die leere Vorgabe (wie <c>EMPTY</c> im Web).</summary>
    private void ApplyBox(SpaMailboxDto? box)
    {
        _applying = true;
        _box = box;
        _processedFolder = box?.ProcessedFolder;
        HasMailbox = box is not null;
        Enabled = box?.Enabled ?? true;
        Kind = MailboxKinds.First(k => k.Value == (box?.Kind ?? MailboxKind.Imap));
        Address = box?.Address ?? "";
        Host = box?.Host ?? "";
        Port = box?.Port ?? 993;
        Security = SecurityOptions.First(s => s.Value == (box?.Security ?? MailboxSecurity.SslOnConnect));
        UserName = box?.UserName ?? "";
        Folder = box?.Folder ?? "";
        DirectoryTenantId = box?.DirectoryTenantId ?? "";
        ClientId = box?.ClientId ?? "";
        Secret = "";
        SendReplies = box?.SendReplies ?? false;
        SmtpHost = box?.SmtpHost ?? "";
        SmtpPort = box?.SmtpPort ?? 587;
        SmtpSecurity = SecurityOptions.First(s => s.Value == (box?.SmtpSecurity ?? MailboxSecurity.StartTls));
        SaveSentCopy = box?.SaveSentCopy ?? true;
        SentFolder = box?.SentFolder ?? "";
        _applying = false;
        _savedSignature = Signature();
        foreach (var name in new[] { nameof(IsImap), nameof(IsGraph), nameof(SecretLabel), nameof(ShowSmtp), nameof(ShowSentFolder), nameof(RepliesHint), nameof(HasUnsavedChanges) })
        {
            OnPropertyChanged(name);
        }

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        foreach (var name in new[] { nameof(StatusLine), nameof(HasStatusLine), nameof(StatusIsWarning), nameof(SendNotice), nameof(HasSendNotice), nameof(SecretPlaceholder) })
        {
            OnPropertyChanged(name);
        }

        OnPropertyChanged(nameof(CanCheck));
        CheckCommand.NotifyCanExecuteChanged();
    }

    private void Notify(string text, InfoBarSeverity severity)
    {
        Message = text;
        MessageSeverity = severity;
    }

    /// <summary>Was gespeichert würde; leere Felder heißen „keine Angabe“, ein leeres Geheimnis „unverändert“.</summary>
    private SpaMailboxInput Input() => new(
        Kind.Value, Address.Trim(), Enabled, Text(Host), Whole(Port), Security.Value, Text(UserName), Text(Folder), _processedFolder, Text(DirectoryTenantId), Text(ClientId),
        Secret.Length == 0 ? null : Secret, SendReplies, Text(SmtpHost), Whole(SmtpPort), SmtpSecurity.Value, SaveSentCopy, Text(SentFolder));

    private string Signature() => string.Join('\u001f', Enabled, Kind.Value, Address, Host, Port, Security.Value, UserName, Folder, DirectoryTenantId, ClientId, Secret, SendReplies,
        SmtpHost, SmtpPort, SmtpSecurity.Value, SaveSentCopy, SentFolder);

    private static string? Text(string value) => value.Trim().Length == 0 ? null : value.Trim();

    private static int Whole(decimal? value) => (int)(value ?? 0);

    private static string ChannelLabel(BookingChannel channel) => channel switch
    {
        BookingChannel.Email => "Mail",
        BookingChannel.Sms => "SMS",
        BookingChannel.WhatsApp => "WhatsApp",
        BookingChannel.Phone => "Telefon",
        _ => channel.ToString(),
    };
}
