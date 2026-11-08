using System.Windows.Input;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Eine Zeile der Konfliktmeldung: Symbol (Termin Kalender, Sperrzeit Schloss, Raum Tür, Gerät Werkzeug) und Text in Ortszeit des Standorts,
/// z. B. „Termin · 09:40–10:50 · Clara Fischer“. Ein Termin lässt sich anklicken und wird dann im Kalender markiert.
/// </summary>
public sealed class ConflictLine(string icon, string text, ICommand? show, ICommand? goTo = null)
{
    public string Icon { get; } = icon;

    public string Text { get; } = text;

    /// <summary>Markiert den Termin im Kalender; nur bei Zeilen mit einem Termin.</summary>
    public ICommand? ShowCommand { get; } = show;

    /// <summary>„Zum Konflikt“: öffnet die Wochenansicht der Person bzw. des Raums am Tag des Termins; nur bei Zeilen mit einem Termin und wenn der Kalender es anbietet.</summary>
    public ICommand? GoToCommand { get; } = goTo;

    public bool CanShow => ShowCommand is not null;

    public bool IsPlain => ShowCommand is null;
}

/// <summary>
/// Was der Kalender einer Konfliktmeldung mitgibt: <paramref name="Show"/> markiert einen Termin (Id und Beginn), <paramref name="GoTo"/> öffnet die Wochenansicht
/// der Person bzw. des Raums im Weg („Zum Konflikt“), <paramref name="Counterpart"/> nennt zu einem Termin und der belegten Art das Gegenstück
/// (bei einer Person den Raum, bei einem Raum die Person), damit die Meldung beides nennt.
/// </summary>
public sealed record ConflictLinks(Action<Guid, DateTimeOffset>? Show, Action<BookingConflict>? GoTo = null, Func<Guid, ResourceKind, string?>? Counterpart = null);

/// <summary>
/// Hinweis nach Design-Brief 5.9 (Art Fehler), wenn eine Buchung, Sperre, Verschiebung oder Zeitänderung abgelehnt wird: Titel, je Konflikt
/// eine Zeile in Ortszeit und eine Handlungszeile. Der Server nennt nur Zeiträume in UTC; die Oberfläche rechnet sie um.
/// </summary>
public sealed partial class ConflictNotice : ObservableObject
{
    /// <summary>Handlungszeile der Meldung (über der Liste), wenn Zeiträume im Weg stehen.</summary>
    public const string ActionLine = "Zuerst verschieben oder einen anderen Zeitraum wählen.";

    /// <summary>Titel der Meldung.</summary>
    public const string TitleText = "Zeitraum ist belegt.";

    /// <summary>Beschriftung der Aktion der Meldung.</summary>
    public const string ActionLabel = "Zum Konflikt";

    private ConflictNotice(IReadOnlyList<ConflictLine> lines, (string Text, ICommand Command)? action = null)
    {
        Lines = lines;
        ActionCommand = action?.Command ?? lines.FirstOrDefault(l => l.GoToCommand is not null)?.GoToCommand ?? lines.FirstOrDefault(l => l.CanShow)?.ShowCommand;
        ActionText = ActionCommand is null ? null : action?.Text ?? ActionLabel;
    }

    public string Title { get; } = TitleText;

    public IReadOnlyList<ConflictLine> Lines { get; }

    public string Action { get; } = ActionLine;

    /// <summary>Aktion der Meldung: „Zum Konflikt“ zum ersten Termin der Liste, fehlt, wenn nur Sperrzeiten, Räume oder Geräte im Weg stehen; oder eine eigene Aktion.</summary>
    public string? ActionText { get; }

    public ICommand? ActionCommand { get; }

    /// <summary>Gesetzt, wenn die Person die Meldung mit dem Kreuz geschlossen hat; eine neue Ablehnung erzeugt eine neue Meldung.</summary>
    [ObservableProperty]
    public partial bool IsDismissed { get; private set; }

    [RelayCommand]
    private void Dismiss() => IsDismissed = true;

    /// <summary>Alles in einer Zeile für die Meldung unten (Toast) und Screenreader: „Zeitraum ist belegt. Termin · 09:40–10:50 · Clara Fischer; …“.</summary>
    public string Summary => $"{Title} {string.Join("; ", Lines.Select(l => l.Text))}";

    /// <summary>
    /// Die Meldung zu einer abgelehnten Anfrage; null, wenn der Server keinen belegten Zeitraum nennt (Qualifikation, Öffnungszeit, kein Raum
    /// geeignet …): dann gilt der Text des Fehlers. <paramref name="day"/> ist der Tag, den der Kalender zeigt: Zeiten an diesem Tag stehen ohne Datum. <paramref name="show"/> markiert einen Termin
    /// (Id und Beginn, falls er an einem anderen Tag liegt) im Kalender.
    /// </summary>
    public static ConflictNotice? From(BookingRejected? rejection, TimeZoneInfo zone, DateOnly day, Action<Guid, DateTimeOffset>? show = null) =>
        From(rejection, zone, day, new ConflictLinks(show));

    /// <summary>Wie oben, mit „Zum Konflikt“ und dem Gegenstück (Person bzw. Raum) des belegenden Termins.</summary>
    public static ConflictNotice? From(BookingRejected? rejection, TimeZoneInfo zone, DateOnly day, ConflictLinks links) => From(rejection, zone, day, links, null);

    /// <summary>Wie oben, mit eigener Aktion statt „Zum Konflikt“ (z. B. „Freie Zeit suchen“ in „Neuer Termin“).</summary>
    public static ConflictNotice? From(BookingRejected? rejection, TimeZoneInfo zone, DateOnly day, ConflictLinks links, (string Text, ICommand Command)? action)
    {
        return rejection is { Conflicts: { } conflicts } && conflicts.Any(c => c.From is not null)
            ? new ConflictNotice([.. conflicts.Select(c => Line(c, zone, day, links))], action)
            : null;
    }

    /// <summary>Der Text einer abgelehnten Anfrage für die Meldung unten: die Liste in Ortszeit, sonst der Text des Fehlers.</summary>
    public static string? ToastText<T>(ApiResult<T> result, TimeZoneInfo zone, DateOnly day) =>
        From(result.Rejection, zone, day)?.Summary ?? result.Error;

    private static ConflictLine Line(BookingConflict conflict, TimeZoneInfo zone, DateOnly day, ConflictLinks links)
    {
        var (icon, label) = conflict switch
        {
            { IsBlock: true } => (Icons.Lock, "Sperrzeit"),
            { AppointmentId: not null } => (Icons.Calendar, "Termin"),
            { Kind: ResourceKind.Room } => (Icons.Door, "Raum"),
            { Kind: ResourceKind.Equipment } => (Icons.Wrench, "Gerät"),
            _ => (Icons.User, "Person"),
        };
        var counterpart = conflict is { AppointmentId: { } booked } ? links.Counterpart?.Invoke(booked, conflict.Kind) : null;
        var text = conflict switch
        {
            { From: { } from, Until: { } until } => $"{label} · {Range(from, until, zone, day)} · {conflict.ResourceName}{(counterpart is null ? "" : $" · {counterpart}")}",
            { NextFree: { } next } => $"{conflict.ResourceName}: {conflict.Reason}. Ein Raum ist frei ab {At(next, zone, day)}",
            _ => $"{conflict.ResourceName}: {conflict.Reason}",
        };
        ICommand? command = conflict is { AppointmentId: { } id, From: { } start } && links.Show is { } show
            ? new RelayCommand(() => show(id, start))
            : null;
        ICommand? goTo = conflict is { AppointmentId: not null, From: not null } && links.GoTo is { } go
            ? new RelayCommand(() => go(conflict))
            : null;
        return new ConflictLine(icon, text, command, goTo);
    }

    /// <summary>„12:20“ am Tag des Kalenders, sonst mit Wochentag und Datum.</summary>
    private static string At(DateTimeOffset at, TimeZoneInfo zone, DateOnly day) =>
        DateOnly.FromDateTime(DayLayout.Local(at, zone)) == day ? $"{DayLayout.Local(at, zone):HH:mm}" : DayLayout.ShortDayTime(at, zone);

    /// <summary>„09:40–10:50“ am Tag des Kalenders, sonst mit Datum: „Do. 22.10. 09:40–10:50“ bzw. über Mitternacht „Do. 22.10. 22:00–Fr. 23.10. 01:00“.</summary>
    public static string Range(DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone, DateOnly day)
    {
        var localFrom = DateOnly.FromDateTime(DayLayout.Local(from, zone));
        var localUntil = DateOnly.FromDateTime(DayLayout.Local(until, zone));
        if (localFrom == day && localUntil == day)
        {
            return DayLayout.TimeText(from, until, zone);
        }

        return localFrom == localUntil
            ? $"{DayLayout.ShortDay(localFrom)} {DayLayout.TimeText(from, until, zone)}"
            : $"{DayLayout.ShortDayTime(from, zone)}–{DayLayout.ShortDayTime(until, zone)}";
    }
}
