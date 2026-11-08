using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Datumsfeld (Design-Brief 5.17): <c>TT.MM.JJJJ</c>. Liest tolerant (<c>21102026</c>, <c>211026</c>, <c>21.10.26</c>, <c>21.10.</c> im laufenden Jahr, <c>1.2.26</c>,
/// siehe <see cref="DateInputParser"/>), ein Datum, das es nicht gibt, ist ein Fehler am Feld. ↑/↓ ändern um einen Tag, Umschalt+↑/↓ um eine Woche,
/// Bild auf/ab um einen Monat, Strg+Bild auf/ab um ein Jahr; ohne Wert setzt ↑ den heutigen Tag. Das Rad wirkt nur bei Fokus.
/// Das Kalender-Popover aus dem Brief gibt es noch nicht (kein bestehender Datumswähler in der App); das Feld bleibt ein Textfeld mit Leser.
/// </summary>
public sealed class DateField : ParsedField
{
    public static readonly StyledProperty<DateOnly?> ValueProperty =
        AvaloniaProperty.Register<DateField, DateOnly?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateOnly?> MinimumProperty = AvaloniaProperty.Register<DateField, DateOnly?>(nameof(Minimum));

    public static readonly StyledProperty<DateOnly?> MaximumProperty = AvaloniaProperty.Register<DateField, DateOnly?>(nameof(Maximum));

    /// <summary>Ohne Jahr ist ein Datum nur erlaubt, wenn das Feld es zulässt; ein Geburtstag verlangt das Jahr.</summary>
    public static readonly StyledProperty<bool> RequireYearProperty = AvaloniaProperty.Register<DateField, bool>(nameof(RequireYear));

    /// <summary>Geburtstag: ein zweistelliges Jahr und das Datum selbst liegen nie in der Zukunft.</summary>
    public static readonly StyledProperty<bool> NotInFutureProperty = AvaloniaProperty.Register<DateField, bool>(nameof(NotInFuture));

    private bool _readingValue;

    static DateField()
    {
        ValueProperty.Changed.AddClassHandler<DateField>((field, e) => field.OnValueChanged(e.NewValue as DateOnly?));
        MinimumProperty.Changed.AddClassHandler<DateField>((field, _) => field.Revalidate());
        MaximumProperty.Changed.AddClassHandler<DateField>((field, _) => field.Revalidate());
    }

    public DateField()
    {
        MaxLength = DateInputParser.MaxLength;
        Width = 160;
        HorizontalAlignment = HorizontalAlignment.Left;
        PlaceholderText = "z. B. 21.10.2026";
    }

    public DateOnly? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public DateOnly? Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public DateOnly? Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public bool RequireYear
    {
        get => GetValue(RequireYearProperty);
        set => SetValue(RequireYearProperty, value);
    }

    public bool NotInFuture
    {
        get => GetValue(NotInFutureProperty);
        set => SetValue(NotInFutureProperty, value);
    }

    /// <summary>Der heutige Tag; nur für Tests und Bilder fest vorgegeben.</summary>
    public DateOnly? TodayOverride { get; set; }

    protected override string AllowedCharacters => DateInputParser.AllowedCharacters;

    protected override string MissingMessage => DateInputParser.MissingError;

    private DateOnly Today => TodayOverride ?? DateOnly.FromDateTime(DateTime.Now);

    private DateRules Rules => new(Minimum, Maximum, RequireYear, NotInFuture);

    protected override FieldReading Read(string raw)
    {
        var result = DateInputParser.Parse(raw, Today, Rules);
        if (result.IsEmpty)
        {
            return FieldReading.Empty;
        }

        return result.Value is { } date ? FieldReading.Ok(DateInputParser.Format(date)) : FieldReading.Fail(result.Error!);
    }

    protected override void ValueRead(FieldReading reading)
    {
        _readingValue = true;
        try
        {
            Value = reading.IsEmpty || reading.Display is null ? null : DateInputParser.TryParse(reading.Display, Today);
        }
        finally
        {
            _readingValue = false;
        }
    }

    protected override bool HandleStepKey(KeyEventArgs e)
    {
        var shift = e.KeyModifiers == KeyModifiers.Shift;
        var control = e.KeyModifiers == KeyModifiers.Control;
        if (!shift && !control && e.KeyModifiers != KeyModifiers.None)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.Up when !control:
                Change(days: shift ? 7 : 1);
                return true;
            case Key.Down when !control:
                Change(days: shift ? -7 : -1);
                return true;
            case Key.PageUp when !shift:
                Change(months: control ? 12 : 1);
                return true;
            case Key.PageDown when !shift:
                Change(months: control ? -12 : -1);
                return true;
            default:
                return false;
        }
    }

    protected override void Nudge(int direction, KeyModifiers modifiers) =>
        Change(days: direction * (modifiers.HasFlag(KeyModifiers.Shift) ? 7 : 1));

    private void Change(int days = 0, int months = 0)
    {
        var current = DateInputParser.TryParse(Text, Today, Rules) ?? Value;
        SetAndCommit(DateInputParser.Format(DateInputParser.Step(current, Today, days, months, rules: Rules)));
    }

    private void OnValueChanged(DateOnly? value)
    {
        if (!_readingValue)
        {
            CommittedText = value is { } date ? DateInputParser.Format(date) : "";
        }
    }
}
