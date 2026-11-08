using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Das Symbol im Spaltenkopf: Warnung (Dreieck) oder Fehler (Kreis) mit der Kurzfassung der Meldungen zu dieser Person bzw. diesem Raum als Hinweis.</summary>
public sealed record ColumnAlert(MessageSeverity Severity, string Tip)
{
    public bool IsError => Severity == MessageSeverity.Error;

    public string Icon => IsError ? Icons.ErrorCircle : Icons.Warning;
}

/// <summary>Eine Zeile im Hinweis am Spaltenkopf: ein Symbol (16) und ein kurzer Text.</summary>
public sealed record HeaderTipLine(string Icon, string Text);

/// <summary>
/// Schließband (ADR 0073): die Schließzeit des Standorts als Fläche über allen Spalten, von <see cref="Top"/> mit <see cref="Height"/> (auf die gezeigten Stunden begrenzt).
/// Die Farbe richtet sich nach der Art (Betriebsurlaub akzent, Sonderschließung Warnung, Ausfall Fehler), der Text nennt Art und Grund.
/// </summary>
public sealed record ClosureBandItem(Guid Id, double Top, double Height, ClosureKind Kind, string Title, string Label, string Tip, int OpenAppointments)
{
    public bool IsVacation => Kind == ClosureKind.Vacation;

    public bool IsSpecial => Kind == ClosureKind.SpecialClosure;

    public bool IsOutage => Kind == ClosureKind.Outage;

    /// <summary>Der Text oben im Band braucht mindestens eine Zeile Platz.</summary>
    public bool ShowLabel => Height >= 20;
}

/// <summary>
/// Raumsperre in der Spalte des Raums (ADR 0076): die gesperrte Zeit als schraffierte Fläche von <see cref="Top"/> mit <see cref="Height"/> (auf die gezeigten Stunden begrenzt), unter den
/// Terminen, mit dem Grund in einer Kapsel oben links. Wie die Abwesenheit einer Person, nur für die Zeit der Sperre.
/// </summary>
public sealed record RoomClosureSpanItem(Guid Id, double Top, double Height, RoomClosureKind Kind, string Label, string Tip)
{
    /// <summary>Der Text oben in der Fläche braucht mindestens eine Zeile Platz.</summary>
    public bool ShowLabel => Height >= 20;

    /// <summary>Farbe der Kapsel nach Art wie bei den Schließzeiten: Schaden wie Ausfall (Fehler), Umbau wie Sonderschließung (Warnung), sonst Akzent.</summary>
    public bool IsDamage => Kind == RoomClosureKind.Damage;

    public bool IsRenovation => Kind == RoomClosureKind.Renovation;
}

/// <summary>Der Chip „Geschlossen · Betriebsurlaub“ in der Kopfzeile des Kalenders am Tag einer Schließzeit.</summary>
public sealed record ClosureChipInfo(ClosureKind Kind, string Text, string Compact, string Tip)
{
    public bool IsVacation => Kind == ClosureKind.Vacation;

    public bool IsSpecial => Kind == ClosureKind.SpecialClosure;

    public bool IsOutage => Kind == ClosureKind.Outage;
}

/// <summary>Eine Spalte im Tageskalender: eine Person oder ein Raum.</summary>
public sealed class CalendarColumn(Guid id, string title, string color, string subtitle = "", bool isRoom = false) : INotifyPropertyChanged
{
    private ColumnAlert? _alert;
    private string? _noteText;

    /// <summary>Nur das Symbol im Kopf ändert sich nach dem Aufbau (Meldungen kommen und gehen), ohne dass die Spalten neu entstehen.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Höchstens ein Symbol im Kopf; ein Klick öffnet „Meldungen“ gefiltert auf diese Spalte.</summary>
    public ColumnAlert? Alert
    {
        get => _alert;
        set
        {
            if (_alert == value)
            {
                return;
            }

            _alert = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Alert)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAlert)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderTipLines)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderTipText)));
        }
    }

    public bool HasAlert => _alert is not null;

    /// <summary>Hinweis zur Person als Symbol im Kopf (R05 Slice 111); kommt wie die Meldungen nach dem Aufbau.</summary>
    public string? NoteText
    {
        get => _noteText;
        set
        {
            if (_noteText == value)
            {
                return;
            }

            _noteText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoteText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNote)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderTipLines)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderTipText)));
        }
    }

    public bool HasNote => _noteText is not null;

    public Guid Id { get; } = id;
    public string Title { get; } = title;
    public string Color { get; } = color;

    /// <summary>Dienstzeit der Person bzw. bei Räumen Raumpflege und Zahl der Behandlungen.</summary>
    public string Subtitle { get; } = subtitle;

    /// <summary>Standort, an dem die Person an diesem Tag arbeitet (Dienst an einem anderen Standort); sonst leer. Der Kopf zeigt ihn vor der Dienstzeit.</summary>
    public string? Place { get; init; }

    public bool HasPlace => Place is not null;

    /// <summary>Dienstzeit bzw. Grund; bei Dienst an einem anderen Standort ohne den Standort.</summary>
    public string ShiftText { get; init; } = subtitle;

    /// <summary>Zweite und letzte Zeile im Kopf: Dienstzeit bzw. Grund, bei Dienst an einem anderen Standort mit dem Standort davor („Graz · 10:00–18:00“); bei Räumen Raumpflege und Behandlungen.</summary>
    public string SecondLine => HasPlace ? $"{Place} · {ShiftText}" : ShiftText;

    public string Initials { get; init; } = DayLayout.Initials(title);

    /// <summary>Spalte eines Tages in der 7-Tage-Ansicht einer Person oder eines Raums (Titel „Mo. 19.10.“, Kreis mit der Tageszahl).</summary>
    public bool IsDay { get; init; }

    /// <summary>Der Tag einer Spalte der 7-Tage-Ansicht; sonst leer.</summary>
    public DateOnly? Date { get; init; }

    /// <summary>Ein Klick auf den Kopf zeigt die Termine über sieben Tage; in der 7-Tage-Ansicht selbst gibt es nichts weiter einzuschränken.</summary>
    public bool CanFilter => !IsDay;

    /// <summary>Der Kopf reagiert auf den Klick: Person bzw. Raum zeigt sieben Tage, ein Tag der 7-Tage-Ansicht führt zu diesem Tag.</summary>
    public bool CanClick => CanFilter || IsDay;

    /// <summary>Hinweis am Kopf: „Alle Termine von Lena Huber der nächsten 7 Tage“ bzw. „Klick: zu diesem Tag“.</summary>
    public string? FilterTip => IsDay ? "Klick: zu diesem Tag" : $"Klick: alle Termine von {Title} der nächsten {CalendarFilter.Days} Tage";

    /// <summary>Raum für mehr als eine Person (Paarraum): im Kopf das Symbol Personen-Paar statt der Tür; die Art steht im Hinweis.</summary>
    public bool IsCouple { get; init; }

    /// <summary>Die festen Zeilen des Hinweises am Kopf (Raum: Art, Raumpflege; Person: Dienstzeit, Standort, Sprachen); Auslastung, Warnung und Klick-Hinweis kommen dazu.</summary>
    public IReadOnlyList<HeaderTipLine> TipLines { get; init; } = [];

    /// <summary>Hinweis (Hover) am Kopf: höchstens fünf Zeilen mit Symbol und kurzem Text, die letzte der Klick-Hinweis. Keine Aufzählung der Behandlungen.</summary>
    public IReadOnlyList<HeaderTipLine> HeaderTipLines
    {
        get
        {
            var lines = new List<HeaderTipLine>();
            if (IsArea)
            {
                lines.Add(new HeaderTipLine(Icons.Door, Category));
            }

            lines.AddRange(TipLines);
            if (IsRoom && !IsDay && !IsArea)
            {
                var appointments = Items.Count(i => i.Appointment is not null);
                lines.Add(new HeaderTipLine(Icons.Calendar, appointments switch { 0 => "Keine Termine heute", 1 => "1 Termin heute", var n => $"{n} Termine heute" }));
            }

            if (NoteText is { } note)
            {
                lines.Add(new HeaderTipLine(Icons.Note, note));
            }

            if (Alert is { } alert)
            {
                lines.Add(new HeaderTipLine(alert.Icon, alert.Tip));
            }

            if (FilterTip is { } click)
            {
                lines.Add(new HeaderTipLine(Icons.Cursor, click));
            }

            return lines;
        }
    }

    /// <summary>Die Zeilen des Hinweises als Text, für Screenreader und Automation.</summary>
    public string HeaderTipText => string.Join("\n", HeaderTipLines.Select(l => l.Text));

    /// <summary>Räume zeigen ein Tür-Symbol statt Initialen.</summary>
    public bool IsRoom { get; } = isRoom;

    /// <summary>Sauna oder Ruheraum: ein Bereich mit Plätzen und Eintritten statt Terminen.</summary>
    public bool IsArea { get; init; }

    /// <summary>Kategorie in der Raumansicht („Behandlungsräume“, „Sauna und Ruhe“); leer bei Personen.</summary>
    public string Category { get; set; } = "";

    /// <summary>Dienst- bzw. Öffnungszeiten als helle Flächen; außerhalb ist die Spalte schraffiert.</summary>
    public List<CalendarSpan> Available { get; } = [];

    public List<CalendarItem> Items { get; } = [];

    /// <summary>Vor- und Nachbereitung, in der Raumansicht auch die Raumpflege: schmale Streifen ober- und unterhalb der Termine.</summary>
    public List<CalendarStripe> Stripes { get; } = [];

    /// <summary>Zettel zu einer Uhrzeit („Herr Lang ruft zurück“).</summary>
    public List<CalendarNoteItem> Notes { get; } = [];

    /// <summary>Kein Dienst an diesem Standort an dem Tag (frei, Urlaub, krank, anderer Standort): die Spalte ist schraffiert, Buchen fragt vorher nach.</summary>
    public bool OffDuty { get; init; }

    /// <summary>Laut Dienstplan nicht da (Urlaub, krank, Schulung, frei), hat aber trotzdem Termine.</summary>
    public RosterKind? Absence { get; init; }

    public bool IsSick => Absence == RosterKind.Sick;

    public bool IsAbsent => Absence is not null;

    /// <summary>Zeile 2 des Kopfes nennt einen Grund (Abwesenheit, Raumsperre) und steht in Warnfarbe.</summary>
    public bool HasReasonLine => IsAbsent || IsClosed;

    /// <summary>Die Sperren dieses Raums an diesem Tag (nur Raumansicht): schraffierte Flächen in der Spalte, der Grund steht im Kopf.</summary>
    public List<RoomClosureSpanItem> RoomClosures { get; } = [];

    /// <summary>Die Raumsperre mit den offenen Terminen, zu der „n Termine umbuchen“ im Kopf führt (die Umbuchungshilfe); sonst leer.</summary>
    public Guid? RoomClosureId { get; init; }

    /// <summary>Der Raum ist an diesem Tag ganz oder teilweise gesperrt: im Kopf steht Art und Zeitraum statt der Raumpflege.</summary>
    public bool IsClosed => RoomClosures.Count > 0;

    /// <summary>Offene Termine, obwohl die Person nicht da ist (bzw. der Raum gesperrt ist); der Spaltenkopf nennt sie.</summary>
    public int Affected { get; init; }

    public string AffectedText => Affected == 1 ? "1 Termin umbuchen" : $"{Affected} Termine umbuchen";

    public bool HasAffected => Affected > 0;

    /// <summary>Verlangte Pause nach dem Arbeitszeitrecht; eine eingetragene Pause gilt dann als „gesetzlich“.</summary>
    public int BreakMinutes { get; init; }
}

public enum StripeKind
{
    Preparation,
    FollowUp,
    Cleaning,
}

/// <summary>
/// Nicht buchbare Zeit an einem Termin: „Steine heizen“ davor, die Nachbereitung (in der Raumansicht mit der Raumpflege) danach.
/// Der Streifen gehört zu seinem Termin; ein Rechtsklick bietet das Bearbeiten von Vor- und Nachbereitung an.
/// </summary>
public sealed record CalendarStripe(double Top, double Height, string Text, StripeKind Kind)
{
    /// <summary>Text nur, wenn der Streifen hoch genug ist; sonst steht er in der Hover-Karte.</summary>
    public bool ShowText => Height >= 14;

    /// <summary>Ausführlicher Text („Vorb. 5 · Steine heizen“) für Streifen ab 20 Pixel Höhe; leer, wenn es nichts Ausführlicheres gibt.</summary>
    public string? LongText { get; init; }

    /// <summary>Der Text, der im Streifen steht: ausführlich, sobald die Höhe reicht.</summary>
    public string DisplayText => Height >= 20 && LongText is not null ? LongText : Text;

    public bool IsPreparation => Kind == StripeKind.Preparation;

    public bool IsAfter => !IsPreparation;

    /// <summary>Voller Text als Hover-Karte, auch wenn der Streifen ihn nicht zeigen kann oder kürzen muss.</summary>
    public string? Tip { get; init; }

    /// <summary>Name für Screenreader: der Hinweis in einer Zeile, ohne die Klick-Anleitung der Raumpflege.</summary>
    public string? AccessibleName => Tip is null ? null
        : string.Join(", ", Tip.Split('\n').Where(line => !line.StartsWith("Klick:", StringComparison.Ordinal)));

    public AppointmentDto? Appointment { get; init; }

    /// <summary>Raumpflege-Aufgabe, die an diesem Streifen hängt (nur Raumansicht, nur mit Raumpflege am Raum).</summary>
    public RoomCareTaskDto? Care { get; init; }

    public bool HasCare => Care is not null;

    public bool IsCareDone => Care?.State == RoomCareState.Done;

    public bool IsCareOverdue => Care?.State == RoomCareState.Overdue;

    public bool IsCareOpen => Care?.State == RoomCareState.Open;
}

/// <summary>
/// Bahn eines Elements in der Spalte (Termin, Sperrzeit, Zettel): <see cref="Lane"/> von links ab 0 von <see cref="LaneCount"/> Bahnen
/// im eigenen Bereich. <see cref="HasSide"/>: neben dem Element steht anderes (Termine links, Sperrzeiten und Zettel in der schmalen Bahn rechts,
/// <see cref="InSide"/>). Die Pixel rechnet die Ansicht aus der Spaltenbreite (<c>DayLayout.LaneWidth</c>, <c>DayLayout.LaneLeft</c>).
/// </summary>
public readonly record struct LanePlace(int Lane, int LaneCount, bool InSide = false, bool HasSide = false)
{
    public static LanePlace Alone => new(0, 1);
}

/// <summary>Zettel in einer Spalte; abgehakt bleibt er blass stehen. Mit Ende reicht er über die Dauer (<see cref="Height"/>).</summary>
public sealed record CalendarNoteItem(Guid Id, double Top, string Time, string Text, bool Done, CalendarNoteDto Note)
{
    public double Opacity => Done ? 0.55 : 1;

    public double Height { get; init; } = DayLayout.NoteHeight;

    /// <summary>Steht neben einem Termin oder einer Sperrzeit schmaler am rechten Rand, sonst in voller Breite.</summary>
    public LanePlace Place { get; init; } = LanePlace.Alone;

    /// <summary>Voller Text mit Zeit für den Hinweis, auch wenn der Zettel ihn kürzen muss: „10:00–10:30 Herr Lang ruft zurück“.</summary>
    public string Tip => $"{Time} {Text}";

    /// <summary>Name für Screenreader, damit der Zettel nicht wie ein Termin klingt: „Zettel 13:15 Herr Lang ruft zurück“, abgehakt mit „, erledigt“.</summary>
    public string AccessibleName => $"Zettel {Tip}{(Done ? ", erledigt" : "")}";
}

/// <summary>Senkrechte Lage in Pixeln innerhalb des sichtbaren Tagesausschnitts.</summary>
public sealed record CalendarSpan(double Top, double Height);

/// <summary>Termin oder Sperrzeit, fertig positioniert.</summary>
public sealed record CalendarItem(
    Guid Id,
    string Title,
    string Subtitle,
    string TimeText,
    double Top,
    double Height,
    string Color,
    AppointmentStatus? Status,
    AppointmentDto? Appointment) : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isGroupHighlighted;

    /// <summary>Nur die Auswahl ändert sich nach dem Aufbau; die Ansicht zeigt sie, ohne die Spalten neu aufzubauen.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsBlock => Appointment is null;

    /// <summary>Sperrzeit der Person mit Grund „Pause“; <see cref="IsLegalBreak"/>, wenn das Arbeitszeitrecht an dem Tag eine verlangt.</summary>
    public bool IsBreak { get; init; }

    public bool IsLegalBreak { get; init; }

    /// <summary>Die Sperrzeit selbst, damit sie sich löschen lässt.</summary>
    public BlockDto? Block { get; init; }

    /// <summary>Termin bei einer Person, die laut Dienstplan nicht da ist: muss umgebucht werden.</summary>
    public bool IsAffected { get; init; }

    public bool IsCheckedIn => Status == AppointmentStatus.CheckedIn;

    public bool IsDone => Status == AppointmentStatus.Completed;

    public bool IsNoShow => Status == AppointmentStatus.NoShow;

    /// <summary>Kurze Termine zeigen nur eine Zeile, mittlere Name und Zeit, lange zusätzlich die Behandlung.</summary>
    public bool IsCompact => Height < 40;

    public bool ShowSubtitle => Height >= 62;

    /// <summary>Wird gerade gezogen: bleibt blass an der alten Stelle stehen.</summary>
    public bool IsLifted { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value, nameof(IsSelected));
    }

    /// <summary>In der Raumansicht: wer behandelt (Name und Farbe).</summary>
    public string? Who { get; init; }

    public string? WhoColor { get; init; }

    public string WhoInitials => Who is null ? "" : DayLayout.Initials(Who);

    public bool ShowWho => Who is not null;

    /// <summary>Dritte Zeile: zuerst, was Handeln verlangt (überfällig, umbuchen, Status, Verspätung, Zusage, vorgemerkt), dann Uhrzeit, Gruppe, Serie und Quelle. Schmale Karten kürzen von hinten. Wer behandelt, zeigt in der Raumansicht der Kreis vor dem Namen (Hover-Karte: voller Name).</summary>
    public string MetaText => string.Join(" · ", new[]
    {
        IsOverdue ? $"überfällig {OverdueMinutes} Min." : null, IsAffected ? "umbuchen" : null, StatusText, LateLabel, ConfirmedLabel, IsPending ? "vorgemerkt" : null,
        TimeText, GroupLabel ?? (IsGroup ? "gemeinsam" : null), SeriesLabel, SourceLabel,
    }.Where(s => !string.IsNullOrEmpty(s)));

    /// <summary>Begonnen, aber noch nicht eingecheckt: so viele Minuten.</summary>
    public int OverdueMinutes { get; init; }

    public bool IsOverdue => OverdueMinutes > 0;

    /// <summary>Paar oder Spa-Tag: Etikett und Farbe der Gruppe; die anderen Teile leuchten mit, wenn einer ausgewählt ist.</summary>
    public string? GroupLabel { get; init; }

    public string? GroupColor { get; init; }

    public bool IsGroupHighlighted
    {
        get => _isGroupHighlighted;
        set => Set(ref _isGroupHighlighted, value, nameof(IsGroupHighlighted));
    }

    /// <summary>Passt nicht zur Zahl, die im Tagesüberblick angeklickt wurde; bleibt blass im Hintergrund.</summary>
    public bool IsFaded { get; init; }

    /// <summary>
    /// Einwirkzeit (Person frei, Raum belegt): Lage und Höhe des Bandes in Pixeln relativ zum Termin, sonst 0. In der Raumansicht immer 0.
    /// </summary>
    public double ProcessingTop { get; init; }

    public double ProcessingHeight { get; init; }

    /// <summary>„Einwirkzeit 20 Min. · frei“.</summary>
    public string? ProcessingText { get; init; }

    public bool HasProcessing => ProcessingHeight > 0;

    /// <summary>
    /// Der Text im Band braucht mindestens 14 Pixel Höhe und steht am unteren Rand des Bandes; er erscheint nur, wenn er die Textzeilen der
    /// Karte (eine, zwei oder drei, je nach Höhe) nicht überdeckt. Sonst steht die Einwirkzeit in der Hover-Karte.
    /// </summary>
    public bool ShowProcessingText => ProcessingHeight >= 14 && ProcessingTop + ProcessingHeight - 15 >= (IsCompact ? 22 : ShowSubtitle ? 54 : 38);

    /// <summary>
    /// Lage des Bandes im Inhalt der Karte: ohne den Innenabstand der Karte (7 links, 6 rechts, 4 oben), damit es von Rand zu Rand reicht.
    /// </summary>
    public Avalonia.Thickness ProcessingMargin => new(-7, ProcessingTop - 4, -6, 0);

    /// <summary>
    /// Überlappt der Termin einen anderen derselben Person (Einwirkzeit) oder eine Sperrzeit bzw. einen Zettel, stehen sie nebeneinander
    /// (siehe <see cref="LanePlace"/>); Termine immer links.
    /// </summary>
    public LanePlace Place { get; init; } = LanePlace.Alone;

    public int Lane => Place.Lane;

    public int LaneCount => Place.LaneCount;

    /// <summary>Höchstens drei Kennzeichen oben rechts; alles Weitere steht in der Hover-Karte.</summary>
    public IReadOnlyList<GuestFlag> Flags { get; init; } = [];

    public bool HasFlags => Flags.Count > 0;

    /// <summary>Der Gast hat am Tag des Termins Geburtstag (Ä45): ein Geschenk-Symbol rechts im Kopf, ohne Text. Das Alter steht nur in Hover-Karte und Panel.</summary>
    public bool BirthdayToday { get; init; }

    /// <summary>♀ oder ♂ aus der Anrede des Gastes, klein neben dem Namen; anders dargestellt als der Wunsch „Therapeutin/Therapeut“ (Kennzeichen).</summary>
    public string? Symbol { get; init; }

    /// <summary>Gezeichnetes Icon zu <see cref="Symbol"/>.</summary>
    public string? SymbolIcon => Controls.Icons.Salutation(Symbol);

    /// <summary>Hover-Karte: Gast, Zeit, Person und Raum, Besuche, Hinweis, Notiz.</summary>
    public HoverCard? Hover { get; init; }

    /// <summary>Die Hover-Karte als Text, Zeile für Zeile (für Bildschirmleser und Tests).</summary>
    public string? HoverText => Hover?.ToText();

    /// <summary>Teil einer gemeinsamen Buchung (Paar, Spa-Tag).</summary>
    public bool IsGroup { get; init; }

    /// <summary>Wiederkehrender Termin: „↻ 3/6“ in der dritten Zeile.</summary>
    public string? SeriesLabel { get; init; }

    /// <summary>Gemeldete Verspätung: „kommt 10:15“.</summary>
    public string? LateLabel { get; init; }

    /// <summary>„✓ zugesagt“: der Gast hat über den Link in der Erinnerung zugesagt.</summary>
    public string? ConfirmedLabel { get; init; }

    /// <summary>Woher der Termin kommt („Online“, „per Mail“), nur wenn nicht vom Team selbst gebucht.</summary>
    public string? SourceLabel { get; init; }

    /// <summary>Ohne Verbindung geändert oder gebucht; der Server hat es noch nicht bestätigt.</summary>
    public bool IsPending { get; init; }

    public double Opacity => IsLifted ? 0.35 : IsFaded ? 0.22 : IsPending ? 0.6 : Status is AppointmentStatus.Completed or AppointmentStatus.NoShow ? 0.75 : 1;

    /// <summary>Gebucht bleibt ohne Text; eine Vormerkung aus dem Posteingang heißt „Vorgemerkt“ (ADR 0083).</summary>
    public string StatusText => Appointment is { HeldSince: not null, Status: AppointmentStatus.Booked } ? HeldName
        : IsRunning ? RunningName
        : Status is { } status and not AppointmentStatus.Booked ? StatusName(status) : "";

    public const string HeldName = "Vorgemerkt";

    public const string RunningName = "Läuft";

    /// <summary>Die Person hat in der Handy-App „begonnen“ gemeldet (Slice 106).</summary>
    private bool IsRunning => Appointment is { StartedAt: not null, Status: AppointmentStatus.CheckedIn };

    /// <summary>
    /// Name für Screenreader: Beginn, Gast, Behandlung und Status („10:30 Brigitte Müller, Hot-Stone-Massage, Gebucht“), bei Sperrzeiten Beginn, Grund und Art
    /// („12:30 Schulung Peeling, Gesperrt“). Ende, Kennzeichen und Hinweise stehen in der Hover-Karte.
    /// </summary>
    public string AccessibleName => $"{TimeText.Split('–')[0]} " + string.Join(", ", new[] { Title, Subtitle, IsRunning ? RunningName : Status is { } status ? StatusName(status) : null }
        .Where(s => !string.IsNullOrEmpty(s)));

    private void Set(ref bool field, bool value, string name)
    {
        if (field != value)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public static string StatusName(AppointmentStatus status) => status switch
    {
        AppointmentStatus.CheckedIn => "Eingecheckt",
        AppointmentStatus.Completed => "Erledigt",
        AppointmentStatus.NoShow => "Nicht erschienen",
        AppointmentStatus.Cancelled => "Storniert",
        _ => "Gebucht",
    };
}

public enum GuestFlagKind
{
    Note,
    New,
    Regular,

    /// <summary>Feste Therapeut:in; der Termin liegt bei ihr.</summary>
    Fixed,

    /// <summary>Feste Therapeut:in, der Termin liegt aber bei jemand anderem: auffällig, damit es nicht übersehen wird.</summary>
    FixedElsewhere,

    /// <summary>Gekauftes Paket für diese Behandlung ist fast aufgebraucht oder läuft bald ab.</summary>
    Package,

    /// <summary>Kennzeichen „Braucht mehr Zeit“: der Puffer steckt schon im Termin.</summary>
    MoreTime,

    /// <summary>Kennzeichen „Leitung informieren“.</summary>
    InformManagement,

    /// <summary>Kennzeichen „Nicht mehr buchen“: gebucht hat trotzdem jemand an der Rezeption.</summary>
    DoNotBook,

    /// <summary>Sprache des Gastes, wenn nicht Deutsch („EN“).</summary>
    Language,

    /// <summary>Wunsch „Therapeutin“ (♀) oder „Therapeut“ (♂); die Person am Termin passt oder hat keine Angabe.</summary>
    Gender,

    /// <summary>Wunsch „Therapeutin“ oder „Therapeut“, die Person am Termin ist anders eingetragen: auffällig.</summary>
    GenderMismatch,
}

/// <summary>Wunsch „Therapeutin“ oder „Therapeut“ zur Auswahl; <c>Value</c> null ist „egal“.</summary>
public sealed record GenderChoice(TherapistGender? Value, string Label)
{
    public static IReadOnlyList<GenderChoice> All { get; } =
        [new(null, "egal"), new(TherapistGender.Female, "Therapeutin"), new(TherapistGender.Male, "Therapeut")];

    public override string ToString() => Label;
}

/// <summary>Sprache zur Auswahl, z. B. „Italienisch“; <c>Code</c> null ist Deutsch.</summary>
public sealed record LanguageChoice(string? Code, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Kennzeichen im Termin, z. B. „!“ für einen Hinweis oder „★“ für Stammgast.</summary>
/// <summary>Eine Zeile „Bezeichnung – Wert“ in der Hover-Karte.</summary>
public sealed record HoverFact(string Label, string Value);

/// <summary>
/// Hover-Karte eines Termins: Kopf in drei Zeilen (Name mit ♀/♂ und Etikett „Stammgast“/„Erstbesuch“, Status rechts; Behandlung;
/// „12:30–13:30 · 60 Min. · Person · Raum“), oranges Band für alles, was Handeln verlangt, seltene Termin-Angaben, übrige Kennzeichen,
/// Gast, Ablauf in einer Zeile, Notiz. Leere Abschnitte entfallen.
/// </summary>
public sealed record HoverCard(string Title, string When, string? Treatment, string? Status)
{
    /// <summary>♀ oder ♂ aus der Anrede.</summary>
    public string? Symbol { get; init; }

    public string? SymbolIcon => Controls.Icons.Salutation(Symbol);

    /// <summary>„Mo. 28.10. · 10:00–10:50“: Tag mit Uhrzeit, fett in Grundschrift (Ä158); ohne Wert zeigt die Karte nur <see cref="When"/>.</summary>
    public string? DayTime { get; init; }

    /// <summary>„50 Min. · Anna“: Dauer und Person, normal (in der Raumansicht die Person fett, <see cref="PersonFirst"/>).</summary>
    public string? Duration { get; init; }

    public string? Person { get; init; }

    /// <summary>„Raum Zeder“, fett in Grundschrift.</summary>
    public string? Room { get; init; }

    /// <summary>Raumansicht: die Person ist die Angabe, die man sucht, und steht fett.</summary>
    public bool PersonFirst { get; init; }

    public bool HasParts => DayTime is not null;

    public bool HasRoom => Room is not null;

    public bool HasPerson => Person is not null;

    public bool PersonNormal => HasPerson && !PersonFirst;

    public bool PersonStrong => HasPerson && PersonFirst;

    /// <summary>Der Ablauf steht nur in der Karte, wenn er etwas sagt (geänderte Zeiten oder Einwirkzeit); sonst zeigen ihn die Streifen im Kalender.</summary>
    public bool ShowFlow { get; init; }

    /// <summary>„Stammgast“ oder „Erstbesuch“ neben dem Namen.</summary>
    public string? Tag { get; init; }

    /// <summary>Verlangt etwas von der Rezeption (überfällig, umbuchen, Hinweis, FEST bei jemand anderem …); orange hervorgehoben.</summary>
    public IReadOnlyList<string> Alerts { get; init; } = [];

    /// <summary>Gute Nachricht ohne Handlungsbedarf, etwa die Zusage über den Link.</summary>
    public IReadOnlyList<string> Infos { get; init; } = [];

    /// <summary>„Geburtstag heute · 42“ am Tag, „Geburtstag am 25.10.“ drei Tage davor und danach (<see cref="BirthdayText"/>); sonst leer.</summary>
    public string? Birthday { get; init; }

    public bool HasBirthday => Birthday is not null;

    /// <summary>Seltene Angaben zum Termin (Buchung, Bereiche, Serie, Quelle, Paket, Zahlung), nur wenn vorhanden.</summary>
    public IReadOnlyList<HoverFact> Appointment { get; init; } = [];

    /// <summary>Übrige Kennzeichen mit Erklärung (+10, Sprache, Paket).</summary>
    public IReadOnlyList<GuestFlag> Flags { get; init; } = [];

    public IReadOnlyList<HoverFact> Guest { get; init; } = [];

    /// <summary>Vor- und Nachbereitung, Raumpflege in einer Zeile: „Vorb. 5 · Nachb. 5 · Raumpflege 5 Min.“.</summary>
    public string? Flow { get; init; }

    /// <summary>„Tee 11:20, Früchteteller 11:20“: was zum Termin bereitzustellen ist (aus der Betriebsleiste), unter dem Ablauf.</summary>
    public string? Serving { get; init; }

    public bool HasServing => Serving is not null;

    /// <summary>Erster Vorbereitungsschritt der Behandlung („Öl anwärmen“), unter dem Ablauf.</summary>
    public string? Preparation { get; init; }

    public string? Note { get; init; }

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    public bool HasSymbol => Symbol is not null;

    public bool HasTag => Tag is not null;

    public bool IsRegular => Tag == RegularTag;

    public const string RegularTag = "Stammgast";

    public const string NewTag = "Erstbesuch";

    /// <summary>Erste Zeile der Textfassung: „Steffi Huber ♀ · Stammgast · Eingecheckt“.</summary>
    public string Headline => string.Join(" · ", new[] { Symbol is null ? Title : $"{Title} {Symbol}", Tag, Status }.Where(p => !string.IsNullOrEmpty(p)));

    public bool HasAlerts => Alerts.Count > 0;

    public bool HasInfos => Infos.Count > 0;

    public bool HasAppointment => Appointment.Count > 0;

    public bool HasFlags => Flags.Count > 0;

    public bool HasGuest => Guest.Count > 0;

    public bool HasFlow => ShowFlow && Flow is not null;

    public bool HasFlowBlock => HasFlow || Preparation is not null;

    public bool HasPreparation => Preparation is not null;

    public bool HasNote => !string.IsNullOrEmpty(Note);

    public string ToText() => string.Join("\n", new[] { Headline, Treatment, When }
        .Concat(Alerts)
        .Concat(Infos)
        .Append(Birthday)
        .Concat(Appointment.Select(f => $"{f.Label}: {f.Value}"))
        .Concat(Flags.Select(f => f.Tip))
        .Concat(Guest.Select(f => $"{f.Label}: {f.Value}"))
        .Append(HasFlow ? $"Ablauf: {Flow}" : null)
        .Append(Preparation is null ? null : $"Vorbereitung: {Preparation}")
        .Append(Serving is null ? null : $"Bereitstellen: {Serving}")
        .Append(Note is null ? null : $"Notiz: {Note}")
        .Where(l => !string.IsNullOrEmpty(l)));
}

public sealed record GuestFlag(string Symbol, string Tip, GuestFlagKind Kind)
{
    public bool IsNote => Kind is GuestFlagKind.Note or GuestFlagKind.FixedElsewhere or GuestFlagKind.Package
        or GuestFlagKind.InformManagement or GuestFlagKind.DoNotBook or GuestFlagKind.GenderMismatch;

    public bool IsRegular => Kind == GuestFlagKind.Regular;

    /// <summary>Kennzeichen mit mehreren Buchstaben („FEST“) brauchen eine Pille statt eines Kreises.</summary>
    public bool IsWide => Symbol.Length > 1;
}

/// <summary>Beschriftung der Zeitleiste.</summary>
public sealed record HourMark(string Label, double Top);

/// <summary>Vorschau beim Ziehen oder unter dem Mauszeiger, in Pixeln über den Spalten.</summary>
public sealed record CalendarGhost(double Left, double Top, double Height, string Text, bool IsMove, double BeforeHeight = 0, double AfterHeight = 0,
    bool IsWarning = false)
{
    /// <summary>Beginn der Vorbereitung vor dem Ghost.</summary>
    public double BeforeTop => Top - BeforeHeight;

    /// <summary>Ende des Ghosts, wo die Nachbereitung beginnt.</summary>
    public double AfterTop => Top + Height;
}

/// <summary>
/// Termin in der Liste „Als Nächstes“. Jede Zeile steht einzeilig und zeigt höchstens <see cref="MaxChars"/> Zeichen, sonst endet sie mit „…“;
/// dann nennt <see cref="FullText"/> den vollen Text für den Hinweis (Design-Brief 5.22, Runde 5 Ä30).
/// </summary>
public sealed record UpcomingItem(string Time, string Customer, string Detail, string Color, AppointmentDto Appointment)
{
    /// <summary>Längste Zeile in Zeichen.</summary>
    public const int MaxChars = 44;

    public string CustomerShown => Fit(Customer);

    public string DetailShown => Fit(Detail);

    /// <summary>Voller Text, wenn eine Zeile gekürzt ist; sonst null (kein Hinweis, der nur Sichtbares wiederholt).</summary>
    public string? FullText => Customer.Length > MaxChars || Detail.Length > MaxChars ? $"{Customer} · {Detail}" : null;

    /// <summary>Name für den Screenreader, ungekürzt: „13:00 Eva Muster, Massage · Lena Huber“ (sonst nennt er nur die Uhrzeit).</summary>
    public string AccessibleName => $"{Time} {Customer}, {Detail}";

    private static string Fit(string text) => text.Length <= MaxChars ? text : string.Concat(text.AsSpan(0, MaxChars - 1).TrimEnd(), "…");
}

/// <summary>Kennzahlen des Tages für die Seitenleiste. Nur Anzeige; die Kassa rechnet ihre Beträge selbst.</summary>
/// <summary>
/// <c>Unconfirmed</c>: noch erwartet und nicht über den Link zugesagt. <c>UsesConfirmation</c>: an diesem Tag hat mindestens ein Gast
/// zugesagt; nur dann zählt „unbestätigt“, sonst wäre ohne Erinnerungslink jeder Termin unbestätigt.
/// <c>Overdue</c>: begonnen, aber noch nicht eingecheckt (wie <see cref="CalendarItem.IsOverdue"/>).
/// </summary>
public sealed record DayOverview(int Appointments, int CheckedIn, int Open, int Done, string Utilization, string PlannedRevenue,
    int Unconfirmed = 0, bool UsesConfirmation = false, int Overdue = 0);
