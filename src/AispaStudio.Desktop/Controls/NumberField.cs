using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Zahlenfeld (Design-Brief 5.19) für Dauer, Anzahl, Bestand: ersetzt das <c>NumericUpDown</c>. Dezimalkomma, Tausenderpunkt, keine Formeln
/// (<see cref="NumberInputParser"/>). ↑/↓ ändern um <see cref="Increment"/>, Umschalt+↑/↓ um den kleinsten Schritt (1, bei Nachkommastellen 0,1),
/// Bild auf/ab um <see cref="LargeIncrement"/>, Pos1/Ende springen auf Minimum und Maximum; das Rad wirkt nur bei Fokus; kein Umlauf an den Grenzen.
/// Außerhalb des Bereichs oder nicht lesbar ist ein Fehler am Feld, nie stilles Begrenzen. Die Pfeilspalte (<see cref="ShowSpinner"/>, nur bei
/// Bereichen bis etwa 20 Schritte) ist nur für die Maus: die Knöpfe sind nicht in der Tab-Reihenfolge und am Rand abgeschaltet, das Feld bleibt bedienbar.
/// </summary>
public sealed class NumberField : ParsedField
{
    public static readonly StyledProperty<decimal?> ValueProperty =
        AvaloniaProperty.Register<NumberField, decimal?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<decimal?> MinimumProperty = AvaloniaProperty.Register<NumberField, decimal?>(nameof(Minimum));

    public static readonly StyledProperty<decimal?> MaximumProperty = AvaloniaProperty.Register<NumberField, decimal?>(nameof(Maximum));

    public static readonly StyledProperty<decimal> IncrementProperty = AvaloniaProperty.Register<NumberField, decimal>(nameof(Increment), 1m);

    public static readonly StyledProperty<decimal> LargeIncrementProperty = AvaloniaProperty.Register<NumberField, decimal>(nameof(LargeIncrement), 10m);

    /// <summary>Erlaubte Nachkommastellen; 0 heißt ganze Zahl.</summary>
    public static readonly StyledProperty<int> DecimalPlacesProperty = AvaloniaProperty.Register<NumberField, int>(nameof(DecimalPlaces));

    public static readonly StyledProperty<bool> ShowSpinnerProperty = AvaloniaProperty.Register<NumberField, bool>(nameof(ShowSpinner));

    private readonly RepeatButton _up = SpinButton("Erhöhen", Icons.ChevronUp);
    private readonly RepeatButton _down = SpinButton("Verringern", Icons.ChevronDown);
    private bool _readingValue;
    private int _repeats;

    static NumberField()
    {
        ValueProperty.Changed.AddClassHandler<NumberField>((field, e) => field.OnValueChanged(e.NewValue as decimal?));
        MinimumProperty.Changed.AddClassHandler<NumberField>((field, _) => field.RefreshSpinner());
        MaximumProperty.Changed.AddClassHandler<NumberField>((field, _) => field.RefreshSpinner());
        ShowSpinnerProperty.Changed.AddClassHandler<NumberField>((field, _) => field.RefreshSpinner());
    }

    public NumberField()
    {
        MaxLength = 16;
        Width = 120;
        HorizontalAlignment = HorizontalAlignment.Left;
        _up.Click += (_, _) => Spin(1);
        _down.Click += (_, _) => Spin(-1);
        foreach (var button in new[] { _up, _down })
        {
            button.PointerReleased += (_, _) => _repeats = 0;
            button.PointerCaptureLost += (_, _) => _repeats = 0;
        }
    }

    public decimal? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public decimal? Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public decimal? Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public decimal Increment
    {
        get => GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public decimal LargeIncrement
    {
        get => GetValue(LargeIncrementProperty);
        set => SetValue(LargeIncrementProperty, value);
    }

    public int DecimalPlaces
    {
        get => GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    public bool ShowSpinner
    {
        get => GetValue(ShowSpinnerProperty);
        set => SetValue(ShowSpinnerProperty, value);
    }

    /// <summary>Eine gültige Zahl gilt schon beim Tippen (Ä311).</summary>
    protected override bool PublishesWhileTyping => true;

    protected override string AllowedCharacters => NumberInputParser.AllowedCharacters(Rules);

    protected override string MissingMessage => NumberInputParser.MissingError;

    private NumberRules Rules => new(Minimum, Maximum, DecimalPlaces);

    private decimal SmallStep => DecimalPlaces > 0 ? 1m / (decimal)Math.Pow(10, DecimalPlaces) : 1m;

    protected override FieldReading Read(string raw)
    {
        var result = NumberInputParser.Parse(raw, Rules);
        if (result.IsEmpty)
        {
            return FieldReading.Empty;
        }

        return result.Value is { } number ? FieldReading.Ok(NumberInputParser.Format(number, Rules)) : FieldReading.Fail(result.Error!);
    }

    protected override void ValueRead(FieldReading reading)
    {
        _readingValue = true;
        try
        {
            Value = reading.IsEmpty || reading.Display is null ? null : NumberInputParser.TryParse(reading.Display, Rules);
        }
        finally
        {
            _readingValue = false;
        }

        RefreshSpinner();
    }

    protected override bool HandleStepKey(KeyEventArgs e)
    {
        var shift = e.KeyModifiers == KeyModifiers.Shift;
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.Up:
                Change(shift ? SmallStep : Increment);
                return true;
            case Key.Down:
                Change(-(shift ? SmallStep : Increment));
                return true;
            case Key.PageUp when !shift:
                Change(LargeIncrement);
                return true;
            case Key.PageDown when !shift:
                Change(-LargeIncrement);
                return true;
            case Key.Home when Minimum is { } min && !shift:
                SetAndCommit(NumberInputParser.Format(min, Rules));
                return true;
            case Key.End when Maximum is { } max && !shift:
                SetAndCommit(NumberInputParser.Format(max, Rules));
                return true;
            default:
                return false;
        }
    }

    protected override void Nudge(int direction, KeyModifiers modifiers) =>
        Change(direction * (modifiers.HasFlag(KeyModifiers.Shift) ? SmallStep : Increment));

    protected override AutomationPeer OnCreateAutomationPeer() => new NumberFieldPeer(this);

    private static RepeatButton SpinButton(string name, string icon)
    {
        var button = new RepeatButton
        {
            Content = new Icon { Data = icon, Width = 12, Height = 12 },
            Focusable = false,
            IsTabStop = false,
            Delay = 300,
            Interval = 100,
        };
        button.Classes.Add("spin");
        AutomationProperties.SetName(button, name);
        return button;
    }

    private void Change(decimal delta)
    {
        var current = NumberInputParser.TryParse(Text, Rules) ?? Value;
        SetAndCommit(NumberInputParser.Format(NumberInputParser.Step(current, delta, Rules), Rules));
    }

    /// <summary>Halten des Pfeilknopfs: nach 300 ms wiederholt der Knopf, und bei längerem Halten wird er schneller.</summary>
    private void Spin(int direction)
    {
        _repeats++;
        var button = direction > 0 ? _up : _down;
        button.Interval = Math.Max(30, 100 - (_repeats * 8));
        Change(direction * Increment);
    }

    private void RefreshSpinner()
    {
        if (!ShowSpinner)
        {
            InnerRightContent = null;
            return;
        }

        InnerRightContent ??= new StackPanel
        {
            Width = 24,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _up, _down },
        };
        _up.IsEnabled = Maximum is not { } max || Value is not { } up || up < max;
        _down.IsEnabled = Minimum is not { } min || Value is not { } down || down > min;
    }

    private void OnValueChanged(decimal? value)
    {
        if (!_readingValue)
        {
            CommittedText = value is { } number ? NumberInputParser.Format(number, Rules) : "";
        }

        RefreshSpinner();
    }

    /// <summary>Meldet Bereich, Wert und Schritt an den Screenreader (Range-Value-Muster), zusätzlich zum Text.</summary>
    private sealed class NumberFieldPeer(NumberField owner) : TextBoxAutomationPeer(owner), IRangeValueProvider
    {
        double IRangeValueProvider.Minimum => (double)(owner.Minimum ?? decimal.MinValue);

        double IRangeValueProvider.Maximum => (double)(owner.Maximum ?? decimal.MaxValue);

        double IRangeValueProvider.SmallChange => (double)owner.Increment;

        double IRangeValueProvider.LargeChange => (double)owner.LargeIncrement;

        double IRangeValueProvider.Value => (double)(owner.Value ?? 0m);

        bool IRangeValueProvider.IsReadOnly => owner.IsReadOnly;

        void IRangeValueProvider.SetValue(double value) => owner.SetAndCommit(NumberInputParser.Format((decimal)value, owner.Rules));
    }
}
