using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Uhrzeitfeld (Design-Brief 5.18): ein Textfeld in <c>HH:mm</c>, kein Popup-Rad. Liest tolerant (<c>9</c>, <c>930</c>, <c>9.30</c>, <c>9,30</c>, <c>9:5</c>,
/// siehe <see cref="TimeInputParser"/>) und schreibt nach dem Verlassen <c>09:30</c>. ↑/↓ ändern um das Raster (<see cref="StepMinutes"/>, 5),
/// Umschalt+↑/↓ um 1 Minute, Bild auf/ab um 1 Stunde; ohne Umlauf über Mitternacht; Pos1/Ende nur mit Grenzen. Das Rad wirkt nur bei Fokus.
/// <see cref="NotBefore"/> verlangt ein Ende nach dem Beginn.
/// </summary>
public sealed class TimeField : ParsedField
{
    public static readonly StyledProperty<TimeOnly?> ValueProperty =
        AvaloniaProperty.Register<TimeField, TimeOnly?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> StepMinutesProperty = AvaloniaProperty.Register<TimeField, int>(nameof(StepMinutes), 5);

    public static readonly StyledProperty<TimeOnly?> MinimumProperty = AvaloniaProperty.Register<TimeField, TimeOnly?>(nameof(Minimum));

    public static readonly StyledProperty<TimeOnly?> MaximumProperty = AvaloniaProperty.Register<TimeField, TimeOnly?>(nameof(Maximum));

    /// <summary>Beginn, nach dem dieses Feld (das Ende) liegen muss; ändert er sich, wird das Ende neu geprüft.</summary>
    public static readonly StyledProperty<TimeOnly?> NotBeforeProperty = AvaloniaProperty.Register<TimeField, TimeOnly?>(nameof(NotBefore));

    private bool _readingValue;

    static TimeField()
    {
        ValueProperty.Changed.AddClassHandler<TimeField>((field, e) => field.OnValueChanged(e.NewValue as TimeOnly?));
        NotBeforeProperty.Changed.AddClassHandler<TimeField>((field, _) => field.Revalidate());
    }

    public TimeField()
    {
        MaxLength = TimeInputParser.MaxLength;
        Width = 100;
        HorizontalAlignment = HorizontalAlignment.Left;
        PlaceholderText = "z. B. 09:30";
    }

    public TimeOnly? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Schrittweite von ↑/↓ in Minuten, die Raster des Kalenders (5, 10 oder 15).</summary>
    public int StepMinutes
    {
        get => GetValue(StepMinutesProperty);
        set => SetValue(StepMinutesProperty, value);
    }

    public TimeOnly? Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public TimeOnly? Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public TimeOnly? NotBefore
    {
        get => GetValue(NotBeforeProperty);
        set => SetValue(NotBeforeProperty, value);
    }

    protected override string AllowedCharacters => TimeInputParser.AllowedCharacters;

    protected override string MissingMessage => TimeInputParser.MissingError;

    protected override FieldReading Read(string raw)
    {
        var result = TimeInputParser.Parse(raw);
        if (result.IsEmpty)
        {
            return FieldReading.Empty;
        }

        if (result.Value is not { } time)
        {
            return FieldReading.Fail(result.Error!);
        }

        if (NotBefore is { } start && time <= start)
        {
            return FieldReading.Fail(time < start ? TimeInputParser.EndBeforeStartError : TimeInputParser.EndEqualsStartError);
        }

        if ((Minimum is { } min && time < min) || (Maximum is { } max && time > max))
        {
            return FieldReading.Fail(RangeText());
        }

        return FieldReading.Ok(TimeInputParser.Format(time));
    }

    protected override void ValueRead(FieldReading reading)
    {
        _readingValue = true;
        try
        {
            Value = reading.IsEmpty || reading.Display is null ? null : TimeInputParser.TryParse(reading.Display);
        }
        finally
        {
            _readingValue = false;
        }
    }

    protected override bool HandleStepKey(KeyEventArgs e)
    {
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            return false;
        }

        var shift = e.KeyModifiers == KeyModifiers.Shift;
        switch (e.Key)
        {
            case Key.Up:
                Change(shift ? 1 : StepMinutes);
                return true;
            case Key.Down:
                Change(shift ? -1 : -StepMinutes);
                return true;
            case Key.PageUp when !shift:
                Change(60);
                return true;
            case Key.PageDown when !shift:
                Change(-60);
                return true;
            case Key.Home when Minimum is { } min && !shift:
                SetAndCommit(TimeInputParser.Format(min));
                return true;
            case Key.End when Maximum is { } max && !shift:
                SetAndCommit(TimeInputParser.Format(max));
                return true;
            default:
                return false;
        }
    }

    protected override void Nudge(int direction, KeyModifiers modifiers) =>
        Change(direction * (modifiers.HasFlag(KeyModifiers.Shift) ? 1 : StepMinutes));

    private void Change(int minutes)
    {
        var current = TimeInputParser.TryParse(Text) ?? Value;
        SetAndCommit(TimeInputParser.Format(TimeInputParser.Step(current, minutes, Minimum, Maximum)));
    }

    private string RangeText() => (Minimum, Maximum) switch
    {
        ({ } min, { } max) => $"Die Uhrzeit liegt zwischen {TimeInputParser.Format(min)} und {TimeInputParser.Format(max)}.",
        ({ } min, null) => $"Die Uhrzeit liegt ab {TimeInputParser.Format(min)}.",
        (null, { } max) => $"Die Uhrzeit liegt bis {TimeInputParser.Format(max)}.",
        _ => TimeInputParser.RangeError,
    };

    /// <summary>Ein Wert von außen (typisiert gebunden oder per Code) erscheint im Feld und im bestätigten Text.</summary>
    private void OnValueChanged(TimeOnly? value)
    {
        if (!_readingValue)
        {
            CommittedText = value is { } time ? TimeInputParser.Format(time) : "";
        }
    }
}
