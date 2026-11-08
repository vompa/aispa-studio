using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Betragsfeld (Design-Brief 5.20) für Preis, Rabatt, Trinkgeld, Kassenbetrag: <c>decimal</c>, immer zwei Nachkommastellen, ohne Pfeilknöpfe.
/// <c>€</c> steht in der Beschriftung. Liest <c>12</c> als 12,00, <c>12,5</c> und <c>12.5</c> als 12,50, <c>1.234,56</c>; mehr als zwei
/// Nachkommastellen sind ein Fehler, nie stilles Runden (RKSV/TSE, ADR 0010). Leer ist „keine Angabe“, nicht 0,00. Negative Beträge nur mit
/// <see cref="AllowNegative"/> (Storno, Gutschrift) und dann in der Fehlerfarbe. ↑/↓ ändern um 1,00, Umschalt+↑/↓ um 0,10, Bild auf/ab um 10,00.
/// </summary>
public sealed class MoneyField : ParsedField
{
    public static readonly StyledProperty<decimal?> ValueProperty =
        AvaloniaProperty.Register<MoneyField, decimal?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> AllowNegativeProperty = AvaloniaProperty.Register<MoneyField, bool>(nameof(AllowNegative));

    public static readonly StyledProperty<decimal?> MinimumProperty = AvaloniaProperty.Register<MoneyField, decimal?>(nameof(Minimum));

    public static readonly StyledProperty<decimal?> MaximumProperty = AvaloniaProperty.Register<MoneyField, decimal?>(nameof(Maximum));

    private bool _readingValue;

    static MoneyField()
    {
        ValueProperty.Changed.AddClassHandler<MoneyField>((field, e) => field.OnValueChanged(e.NewValue as decimal?));
    }

    public MoneyField()
    {
        MaxLength = 20;
        Width = 140;
        HorizontalAlignment = HorizontalAlignment.Left;
        PlaceholderText = "z. B. 12,50";
    }

    public decimal? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Negativ nur in Feldern für Storno und Gutschrift.</summary>
    public bool AllowNegative
    {
        get => GetValue(AllowNegativeProperty);
        set => SetValue(AllowNegativeProperty, value);
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

    /// <summary>Ein gültiger Betrag gilt schon beim Tippen, z. B. für „Wertgutschein verkaufen“ (Ä311).</summary>
    protected override bool PublishesWhileTyping => true;

    protected override string AllowedCharacters => MoneyInputParser.AllowedCharacters(AllowNegative);

    protected override string MissingMessage => MoneyInputParser.MissingError;

    private MoneyRules Rules => new(AllowNegative, Minimum, Maximum);

    protected override FieldReading Read(string raw)
    {
        var result = MoneyInputParser.Parse(raw, Rules);
        if (result.IsEmpty)
        {
            return FieldReading.Empty;
        }

        return result.Value is { } amount ? FieldReading.Ok(MoneyInputParser.Format(amount)) : FieldReading.Fail(result.Error!);
    }

    protected override void ValueRead(FieldReading reading)
    {
        _readingValue = true;
        try
        {
            Value = reading.IsEmpty || reading.Display is null ? null : MoneyInputParser.TryParse(reading.Display, Rules);
        }
        finally
        {
            _readingValue = false;
        }

        Classes.Set("negative", Value < 0);
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
                Change(shift ? 0.10m : 1m);
                return true;
            case Key.Down:
                Change(shift ? -0.10m : -1m);
                return true;
            case Key.PageUp when !shift:
                Change(10m);
                return true;
            case Key.PageDown when !shift:
                Change(-10m);
                return true;
            case Key.Home when Minimum is { } min && !shift:
                SetAndCommit(MoneyInputParser.Format(min));
                return true;
            case Key.End when Maximum is { } max && !shift:
                SetAndCommit(MoneyInputParser.Format(max));
                return true;
            default:
                return false;
        }
    }

    protected override void Nudge(int direction, KeyModifiers modifiers) => Change(direction * (modifiers.HasFlag(KeyModifiers.Shift) ? 0.10m : 1m));

    private void Change(decimal delta)
    {
        var current = MoneyInputParser.TryParse(Text, Rules) ?? Value;
        SetAndCommit(MoneyInputParser.Format(MoneyInputParser.Step(current, delta, Rules)));
    }

    private void OnValueChanged(decimal? value)
    {
        if (!_readingValue)
        {
            CommittedText = value is { } amount ? MoneyInputParser.Format(amount) : "";
        }
    }
}
