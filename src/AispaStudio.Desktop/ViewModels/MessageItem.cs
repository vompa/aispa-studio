using System.Windows.Input;
using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Wie schwer eine Meldung ist; je Stufe eine Gruppe in der Seitenleiste „Meldungen“ (aufsteigend: Hinweis, Warnung, Fehler).</summary>
public enum MessageSeverity
{
    Notice,
    Warning,
    Error,
}

/// <summary>Woher eine Meldung kommt; bestimmt Pille, Hilfethema und die eine Aktion.</summary>
public enum MessageKind
{
    Error,
    Break,
    Stock,
    RoomCare,
    DayStart,
    RoomsBelowMinimum,
    Reminder,
    Absence,
    Posted,
    Closure,

    /// <summary>Kalender-Abo (ADR 0075): Änderung nach dem Abruf, Fehler beim Abruf, Erinnerung im November, „Falsch gemeldet“.</summary>
    Subscription,

    /// <summary>Vormerkung aus dem Posteingang, auf die der Gast nicht geantwortet hat (ADR 0083).</summary>
    Hold,

    /// <summary>Raumsperre (ADR 0076) mit offenen Terminen im gesperrten Raum.</summary>
    RoomClosure,

    /// <summary>Fälliger Folgetermin aus einem Mitarbeitergespräch (R05 Slice 110); nennt keinen Inhalt der Personalakte.</summary>
    HrFollowUp,

    /// <summary>Zertifikat in der Erinnerungsfrist oder abgelaufen (R05 Slice 117).</summary>
    Certificate,

    /// <summary>Lageraufgabe für die Person mit der Zuständigkeit Lager (R05 Slice 117).</summary>
    StockDuty,
}

public enum MessageSubjectKind
{
    Person,
    Room,
    Material,
}

/// <summary>Wen oder was eine Meldung betrifft: eine Person, ein Raum oder ein Material; Spaltenköpfe und Filter erkennen sie an der Id.</summary>
public sealed record MessageSubject(MessageSubjectKind Kind, Guid Id, string Name)
{
    public string Icon => Kind switch
    {
        MessageSubjectKind.Person => Icons.User,
        MessageSubjectKind.Room => Icons.Door,
        _ => Icons.Archive,
    };
}

/// <summary>
/// Eine Meldung der Seitenleiste „Meldungen“ (docs/gestaltung/meldungen.md, Schritt 16): Titel, Kurztext, die eine passende Aktion und „Wegklicken“.
/// <see cref="Key"/> erkennt dieselbe Meldung wieder (Quelle plus Betroffener), <see cref="Trigger"/> den Auslöser: „Wegklicken“ blendet sie nur aus,
/// bis sich der Auslöser ändert (z. B. Raumpflege von offen auf überfällig).
/// </summary>
public sealed class MessageItem
{
    public required string Key { get; init; }

    public string Trigger { get; init; } = "";

    public required MessageKind Kind { get; init; }

    public required MessageSeverity Severity { get; init; }

    public required string Title { get; init; }

    public required string Text { get; init; }

    /// <summary>Kurzfassung für den Hinweis am Spaltenkopf, z. B. „Raumpflege überfällig“.</summary>
    public string ShortText { get; init; } = "";

    /// <summary>Weitere Zeilen für die Detailansicht, z. B. die Materialien unter der Mindestmenge.</summary>
    public IReadOnlyList<string> Lines { get; init; } = [];

    public IReadOnlyList<MessageSubject> Subjects { get; init; } = [];

    /// <summary>Wann der Auslöser eintrat (z. B. Ende der Pflegezeit); sonst, wann die App die Meldung zum ersten Mal sah.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>„seit 11:10“.</summary>
    public string? SinceText { get; init; }

    /// <summary>Hilfethema und Abschnitt zur Meldungsart für „Was tun?“ ohne KI.</summary>
    public (string Topic, string? Section)? Help { get; init; }

    public string? ActionLabel { get; init; }

    public ICommand? ActionCommand { get; init; }

    public ICommand? DismissCommand { get; init; }

    /// <summary>Klick auf die Karte: die Detailansicht.</summary>
    public ICommand? OpenCommand { get; init; }

    /// <summary>Wurde während der Sitzung erledigt; steht blass in der Gruppe „Erledigt“.</summary>
    public bool IsDone { get; init; }

    public string? DoneText { get; init; }

    public bool HasAction => ActionLabel is not null && ActionCommand is not null;

    public bool IsError => Severity == MessageSeverity.Error && !IsDone;

    public bool IsWarning => Severity == MessageSeverity.Warning && !IsDone;

    public bool IsNotice => Severity == MessageSeverity.Notice && !IsDone;

    /// <summary>Was der Screenreader für den Kopf der Karte liest: Titel und Text.</summary>
    public string AccessibleName => $"{Title}. {Text}";

    public double CardOpacity => IsDone ? 0.6 : 1;

    public string Icon => IsDone ? Icons.CheckCircle : Severity switch
    {
        MessageSeverity.Error => Icons.ErrorCircle,
        MessageSeverity.Warning => Icons.Warning,
        _ => Icons.Info,
    };

    /// <summary>Text der Pille rechts oben.</summary>
    public string KindText => IsDone ? "Erledigt" : Kind switch
    {
        MessageKind.Error => "Fehler",
        MessageKind.Break => "Pause",
        MessageKind.Stock => "Lager",
        MessageKind.RoomCare => "Raumpflege",
        MessageKind.DayStart => "Tagesstart",
        MessageKind.RoomsBelowMinimum => "Mindestbestand",
        MessageKind.Reminder => "Handbuch",
        MessageKind.Absence => "Ausfall",
        MessageKind.Subscription => "Kalender-Abo",
        MessageKind.Closure => "Schließzeit",
        MessageKind.RoomClosure => "Raumsperre",
        MessageKind.Hold => "Vormerkung",
        MessageKind.HrFollowUp => "Personalakte",
        MessageKind.Certificate => "Zertifikat",
        MessageKind.StockDuty => "Lager",
        _ => "Hinweis",
    };

    /// <summary>Dieselbe Meldung als erledigt: ohne Aktion, blass, mit der Uhrzeit der Erledigung.</summary>
    public MessageItem AsDone(string doneText) => new()
    {
        Key = Key,
        Trigger = Trigger,
        Kind = Kind,
        Severity = Severity,
        Title = Title,
        Text = Text,
        ShortText = ShortText,
        Lines = Lines,
        Subjects = Subjects,
        Since = Since,
        SinceText = SinceText,
        Help = Help,
        OpenCommand = OpenCommand,
        IsDone = true,
        DoneText = doneText,
    };
}

/// <summary>Eine Gruppe der Seitenleiste: Zwischentitel mit Zähler, darunter die Karten.</summary>
public sealed record MessageGroup(string Title, IReadOnlyList<MessageItem> Items)
{
    public int Count => Items.Count;
}
