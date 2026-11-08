using System.Globalization;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Der gelesene Zeitraum einer Schließzeit oder Raumsperre: Kalendertage am Standort, beide einschließlich, und bei einem Teil der Tage die Uhrzeiten (ohne: ganze Tage).</summary>
public readonly record struct PeriodValue(DateOnly From, TimeOnly? FromTime, DateOnly Until, TimeOnly? UntilTime);

/// <summary>
/// Die Felder „Zeitraum“ von Schließzeiten und Raumsperren (<c>closuresModel.ts</c>, <c>roomClosuresModel.ts</c>): Von, Bis und „Nur ein Teil der Tage (mit Uhrzeit)“ mit
/// Ab- und Bis-Uhrzeit. Die Texte stehen so im Feld, wie das Datums- und das Uhrzeitfeld sie bestätigen (<c>TT.MM.JJJJ</c>, <c>HH:mm</c>); <see cref="TryRead"/> prüft dieselben
/// Regeln wie der Server (<c>validate</c> im Web), damit der Fehler am Feld steht, bevor etwas gesendet wird.
/// </summary>
public sealed partial class PeriodFormViewModel : ObservableObject
{
    public const string EndBeforeStart = "Das Ende darf nicht vor dem Beginn liegen.";

    public const string TimeOrder = "Die Uhrzeit des Endes muss nach der des Beginns liegen.";

    /// <summary>Feldkennungen für den Fokus nach einem Fehler.</summary>
    public const string FromDateField = "FromDateField";

    public const string UntilDateField = "UntilDateField";

    public const string FromTimeField = "FromTimeField";

    public const string UntilTimeField = "UntilTimeField";

    private readonly string _firstDayMissing;
    private readonly string _lastDayMissing;
    private readonly Func<DateOnly> _today;
    private bool _applying;

    public PeriodFormViewModel(string firstDayMissing, string lastDayMissing, Func<DateOnly> today)
    {
        _firstDayMissing = firstDayMissing;
        _lastDayMissing = lastDayMissing;
        _today = today;
    }

    /// <summary>Eine Eingabe hat sich geändert (nicht beim Setzen durch <see cref="Set"/>).</summary>
    public event Action? Changed;

    [ObservableProperty]
    public partial string FromDate { get; set; } = "";

    [ObservableProperty]
    public partial string UntilDate { get; set; } = "";

    /// <summary>Halber Tag: die Uhrzeiten gelten; sonst von 0:00 Uhr des ersten bis zum Ende des letzten Tages.</summary>
    [ObservableProperty]
    public partial bool IsPartial { get; set; }

    [ObservableProperty]
    public partial string FromTime { get; set; } = "";

    [ObservableProperty]
    public partial string UntilTime { get; set; } = "";

    [ObservableProperty]
    public partial string? FromDateError { get; private set; }

    [ObservableProperty]
    public partial string? UntilDateError { get; private set; }

    [ObservableProperty]
    public partial string? FromTimeError { get; private set; }

    [ObservableProperty]
    public partial string? UntilTimeError { get; private set; }

    /// <summary>Was die Eingaben unterscheidet, für die Frage „geändert?“.</summary>
    public string Signature => string.Join('|', FromDate, UntilDate, IsPartial, FromTime, UntilTime);

    /// <summary>Stellt die Felder aus einem Zeitraum her; das gilt nicht als Änderung durch die Person.</summary>
    public void Set(DateOnly from, TimeOnly? fromTime, DateOnly until, TimeOnly? untilTime)
    {
        _applying = true;
        FromDate = DateInputParser.Format(from);
        UntilDate = DateInputParser.Format(until);
        IsPartial = fromTime is not null || untilTime is not null;
        FromTime = fromTime is { } f ? TimeInputParser.Format(f) : "";
        UntilTime = untilTime is { } u ? TimeInputParser.Format(u) : "";
        FromDateError = UntilDateError = FromTimeError = UntilTimeError = null;
        _applying = false;
    }

    /// <summary>
    /// Liest und prüft den Zeitraum. Bei einem Fehler stehen die Meldungen an den Feldern, <paramref name="firstInvalid"/> nennt das erste davon (für den Fokus). Uhrzeiten zählen nur
    /// mit dem Haken „Nur ein Teil der Tage“; ohne ihn bleiben sie im Feld stehen und gehen nicht mit.
    /// </summary>
    public bool TryRead(out PeriodValue value, out string? firstInvalid)
    {
        FromDateError = UntilDateError = FromTimeError = UntilTimeError = null;
        var from = ReadDate(FromDate, _firstDayMissing, out var fromError);
        var until = ReadDate(UntilDate, _lastDayMissing, out var untilError);
        FromDateError = fromError;
        UntilDateError = untilError;
        if (from is { } start && until is { } end && end < start)
        {
            UntilDateError = EndBeforeStart;
        }

        TimeOnly? fromTime = null;
        TimeOnly? untilTime = null;
        if (IsPartial)
        {
            fromTime = ReadTime(FromTime, out var fromTimeError);
            untilTime = ReadTime(UntilTime, out var untilTimeError);
            FromTimeError = fromTimeError;
            UntilTimeError = untilTimeError;
            if (from is not null && from == until && fromTime is { } begins && untilTime is { } ends && ends <= begins)
            {
                UntilTimeError = TimeOrder;
            }
        }

        firstInvalid = FromDateError is not null ? FromDateField
            : UntilDateError is not null ? UntilDateField
            : FromTimeError is not null ? FromTimeField
            : UntilTimeError is not null ? UntilTimeField
            : null;
        value = from is { } a && until is { } b ? new PeriodValue(a, fromTime, b, untilTime) : default;
        return firstInvalid is null;
    }

    /// <summary>„24.12.2026 bis 26.12.2026“, ein Tag „24.12.2026“, ein halber Tag „24.12.2026 ab 13:00 Uhr“ (<c>periodLabel</c> im Web).</summary>
    public static string Label(DateOnly from, TimeOnly? fromTime, DateOnly until, TimeOnly? untilTime)
    {
        static string Clock(TimeOnly time) => $"{time.ToString("HH:mm", CultureInfo.InvariantCulture)} Uhr";
        var start = DateInputParser.Format(from);
        if (from == until)
        {
            return (fromTime, untilTime) switch
            {
                ({ } f, { } u) => $"{start}, {f.ToString("HH:mm", CultureInfo.InvariantCulture)} bis {Clock(u)}",
                ({ } f, null) => $"{start} ab {Clock(f)}",
                (null, { } u) => $"{start} bis {Clock(u)}",
                _ => start,
            };
        }

        return $"{start}{(fromTime is { } a ? $", {Clock(a)}" : "")} bis {DateInputParser.Format(until)}{(untilTime is { } b ? $", {Clock(b)}" : "")}";
    }

    partial void OnFromDateChanged(string value) => Edited(() => FromDateError = UntilDateError = null);

    partial void OnUntilDateChanged(string value) => Edited(() => UntilDateError = null);

    partial void OnIsPartialChanged(bool value) => Edited(() => FromTimeError = UntilTimeError = null);

    partial void OnFromTimeChanged(string value) => Edited(() => FromTimeError = UntilTimeError = null);

    partial void OnUntilTimeChanged(string value) => Edited(() => UntilTimeError = null);

    private void Edited(Action clearErrors)
    {
        if (_applying)
        {
            return;
        }

        clearErrors();
        Changed?.Invoke();
    }

    private DateOnly? ReadDate(string text, string missing, out string? error)
    {
        error = null;
        if (text.Trim().Length == 0)
        {
            error = missing;
            return null;
        }

        var result = DateInputParser.Parse(text, _today());
        if (result.IsEmpty)
        {
            error = missing;
            return null;
        }

        error = result.Error;
        return result.Value;
    }

    private static TimeOnly? ReadTime(string text, out string? error)
    {
        error = null;
        if (text.Trim().Length == 0)
        {
            return null;
        }

        var result = TimeInputParser.Parse(text);
        error = result.Error;
        return result.IsEmpty ? null : result.Value;
    }
}
