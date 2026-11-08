using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Zahlungskonto des Spas (ADR 0054) als native Maske der Verwaltung (R05 Slice 96): dieselben Möglichkeiten, Texte und Hilfen wie <c>web/src/admin/PaymentsTab.tsx</c>.
/// Stripe Connect richtet die API über die Queue ein; die Maske fragt den Stand ab, bis er da ist. Schlüssel gehen nur hin: ein hinterlegter Schlüssel steht nur als
/// „••••“ mit seinen letzten vier Zeichen da, die Felder bleiben leer, und ein leeres Feld behält den gespeicherten.
/// </summary>
public sealed partial class PaymentsViewModel(IWorkstationApi api) : ViewModelBase, INativeArea
{
    /// <summary>Ereignisse, die Stripe an den Webhook schicken muss (wie im Web).</summary>
    public const string CheckoutEvents =
        "checkout.session.completed, checkout.session.expired, checkout.session.async_payment_succeeded, checkout.session.async_payment_failed";

    private static readonly string[] Inputs = [nameof(Mode), nameof(SecretKey), nameof(WebhookSecret)];

    private static readonly string[] Derived =
    [
        nameof(IsStripe), nameof(AccountStatus), nameof(HasAccountError), nameof(ConnectAvailable), nameof(ConnectDescription), nameof(NoneDescription),
        nameof(ShowPlatformHint), nameof(IsSwitching), nameof(ShowConnect), nameof(ShowOwnKeys), nameof(ShowSaveBar), nameof(ConnectAccountLine),
        nameof(HasConnectAccount), nameof(ShowConnectButton), nameof(ConnectButtonText), nameof(CanOpenStripe), nameof(ShowCheck), nameof(SecretKeyPlaceholder),
        nameof(WebhookSecretPlaceholder), nameof(ShowTestKeyWarning), nameof(WebhookUrl), nameof(HasWebhookUrl), nameof(OwnKeysAccountLine),
        nameof(HasOwnKeysAccount), nameof(IsConnect), nameof(IsOwnKeys), nameof(IsNone), nameof(HasUnsavedChanges),
    ];

    private bool _applying;

    public string Title => "Zahlungen";

    /// <summary>Abstand zwischen zwei Abfragen des Stands (wie im Web 1,5 s); Tests setzen 0.</summary>
    public TimeSpan PollDelay { get; set; } = TimeSpan.FromMilliseconds(1500);

    /// <summary>Das Zahlungskonto wurde gespeichert; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    /// <summary>„Stripe öffnen“: die Ansicht öffnet die Einrichtungsseite im Browser.</summary>
    public event Action<Uri>? OpenRequested;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Der zuletzt gemeldete Stand des Kontos.</summary>
    [ObservableProperty]
    public partial PaymentAccountDto? Account { get; private set; }

    /// <summary>Die gewählte Möglichkeit.</summary>
    [ObservableProperty]
    public partial PaymentAccountMode Mode { get; set; }

    /// <summary>Neuer geheimer Schlüssel; leer behält den gespeicherten. Wird nach dem Speichern geleert.</summary>
    [ObservableProperty]
    public partial string SecretKey { get; set; } = "";

    /// <summary>Neues Webhook-Geheimnis; leer behält das gespeicherte. Wird nach dem Speichern geleert.</summary>
    [ObservableProperty]
    public partial string WebhookSecret { get; set; } = "";

    /// <summary>Ein Aufruf läuft (Speichern, Verbinden, Stand abfragen).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ConnectCommand), nameof(CheckCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Titel der Fehlermeldung: „Nicht gespeichert“ beim Speichern, „Stripe nicht erreicht“ bei Verbinden und Stand abfragen.</summary>
    [ObservableProperty]
    public partial string ErrorTitle { get; private set; } = "Nicht gespeichert";

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    public bool IsConnect
    {
        get => Mode == PaymentAccountMode.Connect;
        set => Choose(value, PaymentAccountMode.Connect);
    }

    public bool IsOwnKeys
    {
        get => Mode == PaymentAccountMode.OwnKeys;
        set => Choose(value, PaymentAccountMode.OwnKeys);
    }

    public bool IsNone
    {
        get => Mode == PaymentAccountMode.None;
        set => Choose(value, PaymentAccountMode.None);
    }

    /// <summary>Nur mit Stripe als Anbieter gibt es etwas einzustellen; sonst steht nur der Stand da.</summary>
    public bool IsStripe => Account?.Provider == "Stripe";

    public string AccountStatus => Account?.Status ?? "";

    public bool HasAccountError => Account?.Error is not null;

    public bool ConnectAvailable => Account?.ConnectAvailable == true;

    public string ConnectDescription => ConnectAvailable
        ? "Ein paar Klicks, keine Schlüssel. Stripe fragt Firmendaten und Bankkonto selbst ab und zahlt direkt an das Spa aus."
        : "Auf diesem Server nicht eingerichtet; das richtet der Betreiber von AISPA einmal ein.";

    public string NoneDescription => Account?.ServerAccount == true
        ? "Es gilt das Stripe-Konto dieses Servers."
        : "Online gibt es dann keine Gutscheine und keine Ausfallgebühr.";

    public bool ShowPlatformHint => !ConnectAvailable;

    /// <summary>Ein anderes Konto als das eingerichtete ist gewählt: Karten und Erstattungen bleiben beim bisherigen.</summary>
    public bool IsSwitching => Account is { Mode: not PaymentAccountMode.None } account && Mode != account.Mode;

    public bool ShowConnect => IsConnect && ConnectAvailable;

    public bool ShowOwnKeys => IsOwnKeys;

    /// <summary>Wie im Web: bei „Mit Stripe verbinden“ handeln die Knöpfe selbst, es gibt nichts zu speichern.</summary>
    public bool ShowSaveBar => IsStripe && !IsConnect;

    public bool HasConnectAccount => Account?.ConnectAccountId is not null;

    public string ConnectAccountLine => Account?.ConnectAccountId is { } id ? $"Konto: {Account.AccountName ?? "ohne Namen"} ({id})" : "";

    public bool ShowConnectButton => Account is not { Mode: PaymentAccountMode.Connect, Ready: true };

    public string ConnectButtonText => HasConnectAccount ? "Einrichtung bei Stripe fortsetzen" : "Mit Stripe verbinden";

    public bool CanOpenStripe => Account?.ConnectOnboardingUrl is not null;

    public bool ShowCheck => Account is { Mode: PaymentAccountMode.Connect, ConnectAccountId: not null };

    /// <summary>Der hinterlegte Schlüssel nur als Punkte mit den letzten vier Zeichen, nie im Klartext.</summary>
    public string SecretKeyPlaceholder => Mask(Account?.SecretKeyHint) is { } masked ? $"Hinterlegt: {masked}" : "sk_live_…";

    public string WebhookSecretPlaceholder => Account?.HasWebhookSecret == true ? "Hinterlegt" : "whsec_…";

    public bool ShowTestKeyWarning => Account is { SecretKeyHint: not null, LiveMode: false };

    public string? WebhookUrl => Account?.WebhookUrl;

    public bool HasWebhookUrl => WebhookUrl is not null;

    public string OwnKeysAccountLine => Account?.AccountName is { } name ? $"Stripe-Konto: {name}" : "";

    public bool HasOwnKeysAccount => Account?.AccountName is not null;

    /// <summary>Ungespeichert ist eine andere Möglichkeit als die eingerichtete (außer „Mit Stripe verbinden“) oder ein eingetippter Schlüssel.</summary>
    public bool HasUnsavedChanges => Account is { } account && !IsConnect &&
        (Mode != account.Mode || (IsOwnKeys && (SecretKey.Length > 0 || WebhookSecret.Length > 0)));

    /// <summary>„••••“ mit den letzten vier Zeichen des Hinweises der API (z. B. „sk_live_…wxyz“ → „••••wxyz“).</summary>
    public static string? Mask(string? hint) => hint is null ? null : "••••" + (hint.Length > 4 ? hint[^4..] : hint);

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var result = await api.GetAsync<PaymentAccountDto>("api/tenant/payments").ConfigureAwait(true);
        if (result.Value is not { } account)
        {
            LoadError = result.Error ?? "Das Zahlungskonto wurde nicht geladen.";
            return;
        }

        Show(account);
        IsLoaded = true;
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    private bool CanSave => !IsBusy;

    private bool CanConnect => !IsBusy && Account?.ConnectPreparing != true;

    private bool CanCheck => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    /// <summary>Bei „Mit Stripe verbinden“ gibt es nichts zu speichern; sonst geht die Möglichkeit mit den eingetippten Schlüsseln hin, leere Felder behalten die gespeicherten.</summary>
    public async Task<bool> SaveChangesAsync()
    {
        if (IsBusy || Account is null)
        {
            return false;
        }

        if (IsConnect)
        {
            return true;
        }

        Error = null;
        ErrorTitle = "Nicht gespeichert";
        IsBusy = true;
        var input = new PaymentAccountInput(Mode, SecretKey.Length == 0 ? null : SecretKey, WebhookSecret.Length == 0 ? null : WebhookSecret);
        var result = await api.PutAsync<PaymentAccountDto>("api/tenant/payments", input).ConfigureAwait(true);
        IsBusy = false;
        if (result.Value is not { } account)
        {
            Error = result.Error ?? "Das Zahlungskonto wurde nicht gespeichert.";
            return false;
        }

        Show(account);
        StatusText = "Gespeichert.";
        Saved?.Invoke();
        if (account.Mode == PaymentAccountMode.OwnKeys)
        {
            // Den Namen des Stripe-Kontos holt die API im Hintergrund; der Stand kommt nach.
            _ = PollAsync(a => a.AccountName is not null || a.Error is not null, 10);
        }

        return true;
    }

    /// <summary>„Mit Stripe verbinden“ bzw. „Einrichtung bei Stripe fortsetzen“: die API legt Konto und Link im Hintergrund an.</summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    public async Task ConnectAsync()
    {
        Error = null;
        IsBusy = true;
        ErrorTitle = "Stripe nicht erreicht";
        var result = await api.PostAsync<PaymentAccountDto>("api/tenant/payments/connect", null).ConfigureAwait(true);
        if (result.Value is not { } account)
        {
            IsBusy = false;
            Error = result.Error ?? "Die Verbindung mit Stripe wurde nicht gestartet.";
            return;
        }

        Account = account;
        await PollAsync(a => !a.ConnectPreparing, 20).ConfigureAwait(true);
        IsBusy = false;
    }

    /// <summary>„Stand abfragen“: die API fragt bei Stripe nach.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    public async Task CheckAsync()
    {
        Error = null;
        IsBusy = true;
        ErrorTitle = "Stripe nicht erreicht";
        var result = await api.PostAsync<PaymentAccountDto>("api/tenant/payments/check", null).ConfigureAwait(true);
        if (result.Value is not { } account)
        {
            IsBusy = false;
            Error = result.Error ?? "Der Stand wurde nicht abgefragt.";
            return;
        }

        Account = account;
        await PollAsync(a => a.Ready, 5).ConfigureAwait(true);
        IsBusy = false;
    }

    /// <summary>Fragt den Stand ab, bis er passt oder die Versuche aufgebraucht sind; die gewählte Möglichkeit bleibt.</summary>
    private async Task PollAsync(Func<PaymentAccountDto, bool> done, int attempts)
    {
        for (var i = 0; i < attempts && Account is { } current && !done(current); i++)
        {
            await Task.Delay(PollDelay).ConfigureAwait(true);
            var result = await api.GetAsync<PaymentAccountDto>("api/tenant/payments").ConfigureAwait(true);
            if (result.Value is not { } account)
            {
                return;
            }

            Account = account;
        }
    }

    [RelayCommand]
    private void OpenStripe()
    {
        if (Account?.ConnectOnboardingUrl is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            OpenRequested?.Invoke(uri);
        }
    }

    [RelayCommand]
    public void Discard()
    {
        _applying = true;
        if (Account is { } account)
        {
            Mode = InitialMode(account);
        }

        SecretKey = "";
        WebhookSecret = "";
        _applying = false;
        Error = null;
        Refresh();
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    /// <summary>Das Zahlungskonto hat keine Einträge und Abschnitte zum Anspringen.</summary>
    public void Open(Guid? id, string? section, string? field)
    {
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Account) or nameof(Mode))
        {
            Refresh();
        }

        if (_applying || e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Wie im Web: ohne Konto, aber mit Connect auf diesem Server, ist „Mit Stripe verbinden“ vorgewählt.</summary>
    private static PaymentAccountMode InitialMode(PaymentAccountDto account) =>
        account is { Mode: PaymentAccountMode.None, ConnectAvailable: true, ServerAccount: false } ? PaymentAccountMode.Connect : account.Mode;

    private void Choose(bool chosen, PaymentAccountMode mode)
    {
        if (chosen)
        {
            Mode = mode;
        }
    }

    private void Refresh()
    {
        foreach (var name in Derived)
        {
            base.OnPropertyChanged(new PropertyChangedEventArgs(name));
        }

        ConnectCommand.NotifyCanExecuteChanged();
    }

    private void Show(PaymentAccountDto account)
    {
        _applying = true;
        Account = account;
        Mode = InitialMode(account);
        SecretKey = "";
        WebhookSecret = "";
        _applying = false;
        Refresh();
    }
}
