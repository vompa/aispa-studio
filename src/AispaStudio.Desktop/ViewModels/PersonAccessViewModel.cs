using System.ComponentModel;
using System.Text.Json;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Was der Reiter „Zugang und Rolle“ zeigt.</summary>
public enum AccessMode
{
    /// <summary>Zugang bearbeiten (<c>MemberForm</c>), gespeichert mit der Leiste der Karte.</summary>
    Member,

    /// <summary>Der eigene Zugang: nur lesbar.</summary>
    Own,

    /// <summary>Einladen (<c>InviteForm</c>).</summary>
    Invite,

    /// <summary>Einzelplatz: Zugang anlegen (<c>AccountForm</c>).</summary>
    Account,

    /// <summary>Offene Einladung mit „Einladung zurückziehen“.</summary>
    Invitation,
}

/// <summary>
/// Reiter „Zugang und Rolle“ der Personenkarte (R05 Slice 98): die Felder von <c>AccessEditor</c>, <c>MemberForm</c>, <c>InviteForm</c> und <c>AccountForm</c> aus
/// <c>web/src/admin/StaffPage.tsx</c> mit denselben Regeln (Backoffice für alle Standorte, „Berichte mit Beträgen“ nur Standortleitung und Rezeption und nur durch
/// Inhaber und Manager vergeben, „Auswertungen ohne Beträge“ nur Therapeut:in, keine Befristung für Inhaber). Die Rollenauswahl zeigt alle Rollen; was die
/// angemeldete Person nicht vergeben darf, lehnt die API mit Grund ab.
/// </summary>
public sealed partial class PersonAccessViewModel : ViewModelBase
{
    private static readonly string[] Inputs =
        [nameof(Role), nameof(Person), nameof(Until), nameof(AmountReports), nameof(PlainReports), nameof(AllLocations), nameof(IsActive),
            nameof(InviteName), nameof(InviteEmail), nameof(AccountName), nameof(UserName), nameof(Password)];

    private readonly TeamContext _context;
    private readonly Action _changed;
    private readonly Guid? _employeeId;
    private readonly string _employeeName;
    private InvitationDto? _invitation;
    private StaffMemberDto? _member;
    private string _state = "";
    private bool _applying;

    public PersonAccessViewModel(TeamContext context, AccessMode mode, StaffMemberDto? member = null, InvitationDto? invitation = null,
        EmployeeDto? employee = null, Action? changed = null)
    {
        _context = context;
        _changed = changed ?? (() => { });
        _member = member;
        _invitation = invitation;
        Mode = mode;
        _employeeId = employee?.Id;
        _employeeName = employee?.DisplayName ?? "";
        _applying = true;
        InviteName = _employeeName;
        AccountName = _employeeName;
        Locations = [.. context.Data.Locations.Select(l => new CheckOption(l.Id, l.Name, false, Changed))];
        Role = RoleChoice.All[0];
        People = [];
        _applying = false;
        Apply(member);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMember), nameof(IsOwn), nameof(IsInvite), nameof(IsAccount), nameof(IsInvitation), nameof(ShowsFields), nameof(Heading),
        nameof(HelpSection), nameof(CanExtend), nameof(CanResetPassword), nameof(InvitationText))]
    public partial AccessMode Mode { get; private set; }

    public bool IsMember => Mode == AccessMode.Member;

    public bool IsOwn => Mode == AccessMode.Own;

    public bool IsInvite => Mode == AccessMode.Invite;

    public bool IsAccount => Mode == AccessMode.Account;

    public bool IsInvitation => Mode == AccessMode.Invitation;

    /// <summary>Die Felder von Rolle bis Standorte: beim Bearbeiten, Einladen und Anlegen.</summary>
    public bool ShowsFields => Mode is AccessMode.Member or AccessMode.Invite or AccessMode.Account;

    /// <summary>Überschrift des Reiters je nach Fall.</summary>
    public string Heading => Mode switch
    {
        AccessMode.Invite => "Person einladen",
        AccessMode.Account => "Zugang anlegen",
        AccessMode.Invitation => "Offene Einladung",
        _ => "Zugang und Rolle",
    };

    /// <summary>Abschnitt der Hilfe zum Fragezeichen der Überschrift.</summary>
    public string HelpSection => Mode switch
    {
        AccessMode.Invite => "person-einladen",
        AccessMode.Account => "zugang-anlegen",
        AccessMode.Invitation => "offene-einladungen",
        _ => "zugang-bearbeiten",
    };

    /// <summary>Unter der Überschrift: Adresse und Passkeys, im Einzelplatz der Benutzername (wie <c>MemberForm</c>).</summary>
    public string? InfoText => _member is not { } m ? null
        : _context.Standalone
            ? $"Benutzername {m.UserName ?? "?"}{(m.MustChangePassword ? " · Startpasswort noch nicht geändert" : "")}"
            : $"{m.Email} · {m.PasskeyCount} Passkey(s)";

    public bool HasInfoText => InfoText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleHint), nameof(ShowsPlainReports), nameof(ShowsAmountReports), nameof(ShowsUntil), nameof(IsBackoffice),
        nameof(ShowsLocationChoice), nameof(CanExtend))]
    public partial RoleChoice Role { get; set; }

    /// <summary>Warum die API die geänderte Rolle abgelehnt hat, am Feld (masken.md R8).</summary>
    [ObservableProperty]
    public partial string? RoleError { get; private set; }

    /// <summary>Hinweis unter der Rolle; bei Inhaber und Manager, wenn die angemeldete Person nicht Inhaber ist, dazu, wer sie vergibt.</summary>
    public string RoleHint => Role.Role is StaffRole.Owner or StaffRole.GeneralManager && !_context.IsOwner
        ? $"{RoleChoice.HintOf(Role.Role)} Vergeben kann das nur der Inhaber."
        : RoleChoice.HintOf(Role.Role);

    /// <summary>„Person im Team“: Personen ohne eigenen Zugang; die erste Wahl heißt je nach Rolle „Bitte wählen“ oder „Keine (z. B. nur Rezeption)“.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Choice> People { get; private set; }

    [ObservableProperty]
    public partial Choice? Person { get; set; }

    public bool ShowsPlainReports => Role.Role == StaffRole.Therapist;

    [ObservableProperty]
    public partial bool PlainReports { get; set; }

    public bool ShowsAmountReports => Role.Role is StaffRole.Manager or StaffRole.Reception;

    [ObservableProperty]
    public partial bool AmountReports { get; set; }

    /// <summary>„Berichte mit Beträgen“ vergeben nur Inhaber und Manager.</summary>
    public bool CanGrant => _context.IsOwnerLevel;

    public bool ShowsAmountHint => !CanGrant;

    /// <summary>Keine Befristung für Inhaber.</summary>
    public bool ShowsUntil => Role.Role != StaffRole.Owner;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExtend))]
    public partial DateOnly? Until { get; set; }

    /// <summary>„Um 30 Tage verlängern“ beim Bearbeiten eines befristeten Zugangs.</summary>
    public bool CanExtend => IsMember && ShowsUntil && Until is not null;

    public bool IsBackoffice => Role.Role == StaffRole.Backoffice;

    public bool CanAllLocations => _context.CanAllLocations;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLocationList))]
    public partial bool AllLocations { get; set; }

    public bool ShowsLocationChoice => !IsBackoffice;

    public bool ShowsLocationList => !AllLocations;

    public IReadOnlyList<CheckOption> Locations { get; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    // Einladen
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InviteReason), nameof(CanInvite))]
    public partial string InviteName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InviteReason), nameof(CanInvite))]
    public partial string InviteEmail { get; set; } = "";

    /// <summary>Warum „Einladung senden“ nicht geht, wie <c>missingText</c> im Web.</summary>
    public string? InviteReason =>
        MissingText(InviteName.Trim().Length == 0 ? "Name" : null, InviteEmail.Contains('@', StringComparison.Ordinal) ? null : "E-Mail");

    public bool CanInvite => InviteReason is null;

    /// <summary>Nach dem Senden: „Einladung an … geschickt.“</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSent))]
    public partial string? SentText { get; private set; }

    public bool HasSent => SentText is not null;

    /// <summary>Der Einladungslink zum Kopieren.</summary>
    [ObservableProperty]
    public partial string? InviteLink { get; private set; }

    /// <summary>„Link kopiert.“ oder der Weg ohne Zwischenablage.</summary>
    [ObservableProperty]
    public partial string? CopyText { get; private set; }

    // Zugang anlegen (Einzelplatz)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountReason), nameof(CanCreateAccount))]
    public partial string AccountName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountReason), nameof(CanCreateAccount))]
    public partial string UserName { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountReason), nameof(CanCreateAccount))]
    public partial string Password { get; set; } = "";

    public string? AccountReason =>
        MissingText(AccountName.Trim().Length == 0 ? "Name" : null, UserName.Trim().Length == 0 ? "Benutzername" : null)
        ?? (Password.Length < 8 ? "Das Startpasswort braucht mindestens 8 Zeichen." : null);

    public bool CanCreateAccount => AccountReason is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreated))]
    public partial string? CreatedText { get; private set; }

    public bool HasCreated => CreatedText is not null;

    // Passwort zurücksetzen (Einzelplatz)
    public bool CanResetPassword => _context.Standalone && IsMember;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetReason), nameof(CanReset))]
    public partial string NewPassword { get; set; } = "";

    public string? ResetReason => NewPassword.Length < 8 ? "Das Startpasswort braucht mindestens 8 Zeichen." : null;

    public bool CanReset => ResetReason is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResetText))]
    public partial string? ResetText { get; private set; }

    public bool HasResetText => ResetText is not null;

    // Offene Einladung
    public string? InvitationText => _invitation is { } i ? $"{i.Email} · {RoleChoice.LabelOf(i.Role)} · gültig bis {_context.DateText(i.ExpiresAt)}" : null;

    /// <summary>Fehler der API bei Einladen, Anlegen, Zurücksetzen oder Zurückziehen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionError))]
    public partial string? ActionError { get; private set; }

    public bool HasActionError => ActionError is not null;

    /// <summary>Daten auf dem Server haben sich geändert (eingeladen, angelegt, zurückgezogen); die Liste lädt neu, die Karte bleibt.</summary>
    public event Func<bool, Task>? DataChanged;

    /// <summary>Geänderter Zugang oder ein angefangenes Formular „Person einladen“ bzw. „Zugang anlegen“.</summary>
    public bool HasUnsavedChanges => ShowsFields && State() != _state;

    /// <summary>Im Formular steht etwas, das nur der eigene Knopf abschickt.</summary>
    public bool IsFormFilled => (IsInvite || IsAccount) && State() != _state;

    /// <summary>Speichert den geänderten Zugang; der Fehler der API steht dann in der Karte.</summary>
    public async Task<ApiResult<StaffMemberDto>> SaveAsync()
    {
        var (all, ids) = LocationsOf();
        var input = new StaffAccessInput(Role.Role, all, ids, Person?.Id, IsActive, ExpiresOf(), AmountOf(), PlainOf());
        var result = await _context.Api.PutAsync<StaffMemberDto>($"api/staff/{_member!.UserId}", input).ConfigureAwait(true);
        if (result.Value is { } saved)
        {
            _member = saved;
            _state = State();
        }

        return result;
    }

    public void Discard() => Apply(_member);

    /// <summary>Die API hat den Zugang abgelehnt: betraf die Änderung die Rolle, steht der Grund auch am Feld.</summary>
    public void Rejected(string reason)
    {
        if (Role.Role != _member?.Role)
        {
            RoleError = reason;
        }
    }

    /// <summary>Die eben gesendete Einladung ist offen; der Link bleibt sichtbar, er wird nur einmal gezeigt.</summary>
    public void ShowInvitation(InvitationDto invitation)
    {
        _invitation = invitation;
        Mode = AccessMode.Invitation;
    }

    /// <summary>„Um 30 Tage verlängern“: ab dem späteren Tag aus Ende und heute; gilt mit dem Speichern.</summary>
    [RelayCommand]
    private void Extend()
    {
        var today = _context.Today;
        Until = (Until is { } until && until > today ? until : today).AddDays(30);
    }

    [RelayCommand]
    private async Task InviteAsync()
    {
        var (all, ids) = LocationsOf();
        var email = InviteEmail.Trim();
        var input = new InvitationInput(email, InviteName.Trim(), Role.Role, all, ids, Person?.Id, ExpiresOf(), AmountOf(), PlainOf());
        var result = await _context.Api.PostAsync<InvitationCreated>("api/staff/invitations", input).ConfigureAwait(true);
        if (result.Value is not { } created)
        {
            ActionError = result.Error ?? "Die Einladung wurde nicht gesendet.";
            return;
        }

        ActionError = null;
        SentText = $"Einladung an {email} geschickt.";
        InviteLink = created.Link;
        CopyText = null;
        ClearForm(() =>
        {
            InviteName = "";
            InviteEmail = "";
        });
        await ReportAsync(true).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreateAccountAsync()
    {
        var (all, ids) = LocationsOf();
        var name = AccountName.Trim();
        var userName = UserName.Trim();
        var input = new StaffAccountInput(userName, name, Password, Role.Role, all, ids, Person?.Id, ExpiresOf(), AmountOf(), PlainOf());
        var result = await _context.Api.PostAsync<StaffMemberDto>("api/staff/accounts", input).ConfigureAwait(true);
        if (result.Value is null)
        {
            ActionError = result.Error ?? "Der Zugang wurde nicht angelegt.";
            return;
        }

        ActionError = null;
        CreatedText = $"{name} kann sich jetzt mit „{userName}“ und dem Startpasswort anmelden und wählt dabei ein eigenes Passwort.";
        ClearForm(() =>
        {
            AccountName = "";
            UserName = "";
            Password = "";
        });
        await ReportAsync(true).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        var result = await _context.Api.PostAsync<object>($"api/staff/{_member!.UserId}/password", new PasswordReset(NewPassword)).ConfigureAwait(true);
        ActionError = result.Error;
        if (result.Error is null)
        {
            ResetText = $"Startpasswort gesetzt. {_member.DisplayName} ändert es bei der nächsten Anmeldung.";
            NewPassword = "";
        }
    }

    [RelayCommand]
    private async Task RevokeAsync()
    {
        if (_invitation is not { } invitation || _context.Dialogs is not { } dialogs)
        {
            return;
        }

        var answer = await dialogs.AskAsync(new ConfirmRequest("Einladung zurückziehen?",
            $"Der Link für {invitation.DisplayName} ({invitation.Email}) gilt danach nicht mehr. Eine neue Einladung ist jederzeit möglich.", "Zurückziehen"))
            .ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _context.Api.DeleteAsync<object>($"api/staff/invitations/{invitation.Id}").ConfigureAwait(true);
        ActionError = result.Error;
        if (result.Error is null)
        {
            await ReportAsync(false).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void CloseActionError() => ActionError = null;

    /// <summary>Die Ansicht hat den Link in die Zwischenablage gelegt (oder es ging nicht).</summary>
    public void LinkCopied(bool ok) =>
        CopyText = ok ? "Link kopiert." : "Kopieren nicht möglich. Den markierten Link mit Strg+C kopieren.";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Role) && !_applying)
        {
            RoleError = null;
            var person = Person?.Id;
            People = PeopleFor(Role.Role);
            _applying = true;
            Person = People.FirstOrDefault(p => p.Id == person) ?? People[0];
            _applying = false;
        }

        if (!_applying && e.PropertyName is { } name && Inputs.Contains(name))
        {
            Changed();
        }
    }

    /// <summary>Personen im Team ohne eigenen Zugang (außer diesem).</summary>
    private IReadOnlyList<Choice> PeopleFor(StaffRole role)
    {
        var linked = _context.Data.Members.Where(m => m.UserId != _member?.UserId && m.EmployeeId is not null).Select(m => m.EmployeeId).ToHashSet();
        return [new Choice(null, role == StaffRole.Therapist ? "Bitte wählen" : "Keine (z. B. nur Rezeption)"),
            .. _context.Data.Employees.Where(e => !linked.Contains(e.Id)).Select(e => new Choice(e.Id, e.DisplayName))];
    }

    private void Changed()
    {
        if (!_applying)
        {
            _changed();
        }
    }

    /// <summary>Setzt die Felder auf den gespeicherten Zugang (leer: Vorgaben wie im Web, Rezeption und ein einziger Standort angehakt).</summary>
    private void Apply(StaffMemberDto? member)
    {
        _applying = true;
        foreach (var location in Locations)
        {
            location.IsChecked = member is not null ? member.LocationIds.Contains(location.Id) : Locations.Count == 1;
        }

        Role = RoleChoice.All.First(r => r.Role == (member?.Role ?? StaffRole.Reception));
        People = PeopleFor(Role.Role);
        Person = People.FirstOrDefault(p => p.Id == (member?.EmployeeId ?? _employeeId)) ?? People[0];
        Until = _context.LastDay(member?.ExpiresAt);
        AmountReports = member?.AmountReports ?? false;
        PlainReports = member?.PlainReports ?? true;
        AllLocations = member?.AllLocations ?? false;
        IsActive = member?.IsActive ?? true;
        InviteName = _employeeName;
        InviteEmail = "";
        AccountName = _employeeName;
        UserName = "";
        Password = "";
        RoleError = null;
        _applying = false;
        _state = State();
    }

    /// <summary>Nach dem Absenden: das leere Formular gilt als unverändert.</summary>
    private void ClearForm(Action clear)
    {
        _applying = true;
        clear();
        _applying = false;
        _state = State();
        _changed();
    }

    private async Task ReportAsync(bool keepCard)
    {
        if (DataChanged is { } changed)
        {
            await changed(keepCard).ConfigureAwait(true);
        }
    }

    /// <summary>Das Backoffice arbeitet für alle Standorte; der Server verlangt es (<c>locationsOf</c> im Web).</summary>
    private (bool All, IReadOnlyList<Guid> Ids) LocationsOf() =>
        Role.Role == StaffRole.Backoffice ? (true, []) : (AllLocations, [.. Locations.Where(l => l.IsChecked).Select(l => l.Id)]);

    /// <summary>„Berichte mit Beträgen“ gilt nur für Standortleitung und Rezeption (<c>amountReportsOf</c>).</summary>
    private bool AmountOf() => AmountReports && ShowsAmountReports;

    /// <summary>„Auswertungen ohne Beträge“ lässt sich nur bei Therapeut:in abschalten (<c>plainReportsOf</c>).</summary>
    private bool PlainOf() => !ShowsPlainReports || PlainReports;

    private DateTimeOffset? ExpiresOf() => Role.Role == StaffRole.Owner ? null : _context.ExpiresAt(Until);

    /// <summary>„Name fehlt.“, „Name und E-Mail fehlen.“ wie <c>missingText</c> im Web.</summary>
    private static string? MissingText(params string?[] missing)
    {
        var names = missing.OfType<string>().ToList();
        return names.Count switch
        {
            0 => null,
            1 => $"{names[0]} fehlt.",
            _ => $"{string.Join(", ", names[..^1])} und {names[^1]} fehlen.",
        };
    }

    private string State() => JsonSerializer.Serialize(new
    {
        Role = Role.Role,
        Person = Person?.Id,
        Until,
        AmountReports,
        PlainReports,
        AllLocations,
        IsActive,
        InviteName,
        InviteEmail,
        AccountName,
        UserName,
        Password,
        Locations = Locations.Where(l => l.IsChecked).Select(l => l.Id).ToList(),
    }, ApiJson.Options);
}
