using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Art einer Zeile im Bereich „Team“.</summary>
public enum TeamEntryKind
{
    /// <summary>Person im Team, mit oder ohne Zugang bzw. offene Einladung.</summary>
    Person,

    /// <summary>Zugang ohne Person im Team (z. B. nur Rezeption).</summary>
    Account,

    /// <summary>Offene Einladung ohne Person im Team.</summary>
    Invitation,

    /// <summary>Ausgeschiedene Person (aus dem Team genommen).</summary>
    Former,

    /// <summary>„Person anlegen“.</summary>
    NewPerson,

    /// <summary>„Person einladen“, im Einzelplatz „Zugang anlegen“.</summary>
    NewAccess,
}

/// <summary>
/// Zeile der Teamliste (R05 Slice 98): Name und darunter die Rolle; ein Kennzeichen am Zeilenende steht nur bei einer Abweichung (ADR 0098, Nachtrag zu P5). Eine Person im Team ist eine Zeile, auch mit Zugang oder Einladung; Zugänge und
/// Einladungen ohne Person stehen für sich.
/// </summary>
public sealed record TeamEntry(TeamEntryKind Kind, string Title, string Role, Guid? EmployeeId = null, string? UserId = null, Guid? InvitationId = null,
    string? Email = null, string? FlagText = null, bool HasFlagWarning = false, string? Badge = null, EntryBadgeKind BadgeKind = EntryBadgeKind.Neutral, bool HasNoAccess = false)
{
    /// <summary>Die Rolle unter dem Namen in der Liste; leer bei einer Person ohne Zugang.</summary>
    public bool HasRole => Role.Length > 0;

    /// <summary>Das Kennzeichen am Zeilenende steht nur bei einer Abweichung (befristet, gesperrt, eingeladen, ohne Zugang).</summary>
    public bool HasBadge => Badge is not null;

    public bool BadgeIsCritical => HasBadge && BadgeKind == EntryBadgeKind.Critical;

    public bool BadgeIsWarning => HasBadge && BadgeKind == EntryBadgeKind.Warning;

    public bool BadgeIsNeutral => HasBadge && BadgeKind == EntryBadgeKind.Neutral;

    public bool HasFlags => FlagText is not null;

    public bool IsNew => Kind is TeamEntryKind.NewPerson or TeamEntryKind.NewAccess;

    /// <summary>Für den Screenreader beides in einem Satz.</summary>
    public string Name => string.Join(", ", new[] { Title, HasRole ? Role : null, Badge, FlagText }.Where(p => !string.IsNullOrEmpty(p)));

    /// <summary>Dieselbe Zeile nach dem Neuladen (Zugang und Einladung können dazukommen).</summary>
    public bool SameAs(TeamEntry other) => Kind == other.Kind && (Kind switch
    {
        TeamEntryKind.Person or TeamEntryKind.Former => EmployeeId == other.EmployeeId,
        TeamEntryKind.Account => UserId == other.UserId,
        TeamEntryKind.Invitation => InvitationId == other.InvitationId,
        _ => true,
    });

    public override string ToString() => Title;
}

/// <summary>Farbe des Kennzeichens in der Teamliste: gesperrt und abgelaufen kritisch, befristet gelb, sonst grau.</summary>
public enum EntryBadgeKind
{
    Neutral,
    Warning,
    Critical,
}

/// <summary>Ein Kennzeichen oben auf der Personenkarte; eine Warnung steht mit Warnsymbol vorn (R05 Slice 117).</summary>
public sealed record FlagLine(string Text, bool IsWarning);

/// <summary>Eintrag einer segmentierten Schiene (Brief 5.4): Bereich der Personenkarte oder Ansicht des Teams.</summary>
public sealed partial class CardTab(string id, string label, Action<string> select) : ObservableObject
{
    public string Id => id;

    public string Label => label;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [RelayCommand]
    private void Select() => select(id);
}

/// <summary>Rolle mit dem Namen der Oberfläche (<c>ROLE_LABEL</c> in <c>web/src/auth/access.ts</c>).</summary>
public sealed record RoleChoice(StaffRole Role, string Label)
{
    /// <summary>Alle Rollen in der Reihenfolge des Web; die Auswahl zeigt alle, was nicht vergeben werden darf, lehnt die API mit Grund ab.</summary>
    public static IReadOnlyList<RoleChoice> All { get; } =
    [
        new(StaffRole.Owner, "Inhaber"), new(StaffRole.GeneralManager, "Manager"), new(StaffRole.Manager, "Standortleitung"),
        new(StaffRole.Reception, "Rezeption"), new(StaffRole.Backoffice, "Backoffice"), new(StaffRole.Therapist, "Therapeut:in"),
    ];

    public static string LabelOf(StaffRole role) => All.First(r => r.Role == role).Label;

    /// <summary>Hinweis unter der Rolle (<c>ROLE_HINT</c>).</summary>
    public static string HintOf(StaffRole role) => role switch
    {
        StaffRole.Owner => "Alles an allen Standorten, auch Standorte anlegen und Inhaber ernennen. Der Inhaber bleibt Eigentümer des Kontos.",
        StaffRole.GeneralManager =>
            "Darf wie der Inhaber alles, legt aber keine Inhaber- und Manager-Zugänge an, ändert oder entfernt sie nicht und kündigt weder Lizenz noch Abrechnung.",
        StaffRole.Manager => "Plant, pflegt Stammdaten und lädt Personen für die eigenen Standorte ein. Umsätze nur mit dem Recht „Berichte mit Beträgen“.",
        StaffRole.Reception => "Bucht, verschiebt, storniert, checkt ein und legt Kunden an, liest die Anamnese. Umsätze nur mit dem Recht „Berichte mit Beträgen“.",
        StaffRole.Backoffice =>
            "Für Buchhaltung und Lohnverrechnung: liest Berichte mit Beträgen, Belege, Tagesabschlüsse, Dienstplan und Arbeitszeiten und exportiert sie. Bucht und kassiert nicht, sieht keine Kunden und ändert keine Einstellungen.",
        _ => "Sieht den Kalender, liest die Anamnese und setzt den Status der eigenen Termine.",
    };

    public override string ToString() => Label;
}

/// <summary>Wochentag einer Arbeitszeit (Montag zuerst, wie <c>DAYS</c> im Web).</summary>
public sealed record WorkDayChoice(DayOfWeek Day, string Label)
{
    public static IReadOnlyList<WorkDayChoice> All { get; } =
    [
        new(DayOfWeek.Monday, "Montag"), new(DayOfWeek.Tuesday, "Dienstag"), new(DayOfWeek.Wednesday, "Mittwoch"), new(DayOfWeek.Thursday, "Donnerstag"),
        new(DayOfWeek.Friday, "Freitag"), new(DayOfWeek.Saturday, "Samstag"), new(DayOfWeek.Sunday, "Sonntag"),
    ];

    public override string ToString() => Label;
}

/// <summary>„Für Gäste mit Wunsch“: keine Angabe, Therapeutin oder Therapeut.</summary>
public sealed record PersonGenderChoice(TherapistGender? Gender, string Label)
{
    public static IReadOnlyList<PersonGenderChoice> All { get; } = [new(null, "keine Angabe"), new(TherapistGender.Female, "Therapeutin"), new(TherapistGender.Male, "Therapeut")];

    public override string ToString() => Label;
}

/// <summary>Sprache außer Deutsch, in der die Person behandeln kann.</summary>
public sealed partial class LanguageOption(string code, string label, bool isChecked, Action changed) : ObservableObject
{
    public string Code => code;

    public string Label => label;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;

    partial void OnIsCheckedChanged(bool value) => changed();
}

/// <summary>Eine Wochenarbeitszeit: Tag, Standort, von–bis.</summary>
public sealed partial class WorkingHoursRow : ObservableObject
{
    private const string OtherLocation = "Anderer Standort";

    private readonly Action _changed;

    public WorkingHoursRow(WorkingHours hours, IReadOnlyList<Choice> locations, Action changed, Action<WorkingHoursRow> remove)
    {
        // Ein Standort, den die angemeldete Person nicht sieht, bleibt mit seiner Id stehen wie im Web; die API behält solche Zeiten selbst (MasterDataEndpoints).
        var known = locations.FirstOrDefault(l => l.Id == hours.LocationId);
        Locations = known is null ? [.. locations, new Choice(hours.LocationId, OtherLocation)] : locations;
        Day = WorkDayChoice.All.First(d => d.Day == hours.Day);
        Location = known ?? Locations[^1];
        Start = hours.Start;
        End = hours.End;
        _changed = changed;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public IReadOnlyList<Choice> Locations { get; }

    public IRelayCommand RemoveCommand { get; }

    [ObservableProperty]
    public partial WorkDayChoice Day { get; set; }

    [ObservableProperty]
    public partial Choice? Location { get; set; }

    [ObservableProperty]
    public partial TimeOnly? Start { get; set; }

    [ObservableProperty]
    public partial TimeOnly? End { get; set; }

    partial void OnDayChanged(WorkDayChoice value) => _changed?.Invoke();

    partial void OnLocationChanged(Choice? value) => _changed?.Invoke();

    partial void OnStartChanged(TimeOnly? value) => _changed?.Invoke();

    partial void OnEndChanged(TimeOnly? value) => _changed?.Invoke();

    public WorkingHours ToDto() => new(Location?.Id ?? Guid.Empty, Day.Day, Start ?? default, End ?? default);
}

/// <summary>Eine Qualifikation in „Qualifikationen verwalten“: Umbenennen wirkt sofort (wie <c>QualificationsTab.tsx</c>).</summary>
public sealed partial class QualificationRow(QualificationDto qualification, Func<QualificationRow, Task> rename) : ObservableObject
{
    public Guid Id => qualification.Id;

    public string SavedName => qualification.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRename))]
    public partial string Name { get; set; } = qualification.Name;

    /// <summary>„Umbenennen“ steht nur da, wenn der Name geändert und nicht leer ist.</summary>
    public bool CanRename => Name.Trim().Length > 0 && Name != qualification.Name;

    [RelayCommand]
    private Task RenameAsync() => rename(this);
}
