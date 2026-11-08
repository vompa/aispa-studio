using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace AispaStudio.Desktop.Controls;

/// <summary>Was ein Feld aus seinem Text gelesen hat: leer („keine Angabe“), der Anzeigetext des Werts oder ein Fehlertext.</summary>
public readonly record struct FieldReading(bool IsEmpty, string? Display, string? Error)
{
    public static FieldReading Empty { get; } = new(true, "", null);

    public static FieldReading Ok(string display) => new(false, display, null);

    public static FieldReading Fail(string error) => new(false, null, error);
}

/// <summary>
/// Gemeinsame Grundlage der schnellen Eingabefelder für Uhrzeit, Datum, Zahl und Betrag (Design-Brief 5.16 bis 5.20, Skill „inputs“, Abschnitt 4).
/// Das Feld hält den getippten Rohtext; beim Verlassen und bei Enter liest es ihn mit dem Leser des Typs, schreibt das Anzeigeformat zurück und
/// meldet erst dann den Wert nach außen (<see cref="CommittedText"/>, typisierter <c>Value</c> der Unterklasse); Betrag und Zahl melden einen gültigen
/// Wert schon beim Tippen (<see cref="PublishesWhileTyping"/>), Anzeigeformat und Fehler kommen auch dort erst beim Bestätigen. Ein Fehler bleibt am Feld
/// (<see cref="Error"/>, Rahmen danger), der Text bleibt stehen: nie Absturz, nie stilles Überschreiben, Runden oder Begrenzen.
/// <list type="bullet">
/// <item>Tab und Fokus per Code markieren alles (hier wird ersetzt, nicht bearbeitet); ein Klick setzt nur die Marke.</item>
/// <item>Esc stellt den letzten bestätigten Wert wieder her und markiert ihn; erst das zweite Esc schließt die Maske.</item>
/// <item>Enter bestätigt und löst dann den Standardknopf aus (<c>Button.IsDefault</c>); bei einem Fehler bleibt der Fokus im Feld.</item>
/// <item>Es werden nur die Zeichen des Feldes angenommen; die Pfeiltasten und das Rad (nur bei Fokus) steuern die Unterklassen.</item>
/// </list>
/// </summary>
public abstract class ParsedField : TextBox
{
    /// <summary>Der bestätigte Text im Anzeigeformat; ein leeres Feld ist „“. ViewModels binden hier, nicht an <c>Text</c>.</summary>
    public static readonly StyledProperty<string?> CommittedTextProperty =
        AvaloniaProperty.Register<ParsedField, string?>(nameof(CommittedText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Pflichtfeld: leer ist ein Fehler.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<ParsedField, bool>(nameof(IsRequired));

    public static readonly DirectProperty<ParsedField, string?> ErrorProperty =
        AvaloniaProperty.RegisterDirect<ParsedField, string?>(nameof(Error), field => field.Error);

    /// <summary>Das Feld zeigt keinen Fehler; Knöpfe, die einen typisiert gebundenen Wert (<c>Value</c>) verwenden, binden ihr <c>IsEnabled</c> hier.</summary>
    public static readonly DirectProperty<ParsedField, bool> IsValidProperty =
        AvaloniaProperty.RegisterDirect<ParsedField, bool>(nameof(IsValid), field => field.IsValid);

    private string _confirmed = "";
    private string? _error;
    private bool _committing;
    private bool _settingText;

    static ParsedField()
    {
        CommittedTextProperty.Changed.AddClassHandler<ParsedField>((field, e) => field.OnCommittedTextChanged(e.NewValue as string));
        TextProperty.Changed.AddClassHandler<ParsedField>((field, _) => field.OnTextEdited());
    }

    protected ParsedField()
    {
        Classes.Add("num");
        AcceptsReturn = false;
    }

    /// <summary>Die Felder tragen die Vorlage und die Stile des <c>TextBox</c>.</summary>
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>Wird gemeldet, wenn ein gelesener Wert bestätigt wurde (Enter, Verlassen, Pfeiltaste).</summary>
    public event EventHandler? Committed;

    public string? CommittedText
    {
        get => GetValue(CommittedTextProperty);
        set => SetValue(CommittedTextProperty, value);
    }

    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    /// <summary>Fehlertext am Feld (Problem und Weg), sonst null.</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            var wasValid = IsValid;
            if (SetAndRaise(ErrorProperty, ref _error, value))
            {
                Classes.Set("invalid", value is not null);
                RaisePropertyChanged(IsValidProperty, wasValid, IsValid);
            }
        }
    }

    public bool IsValid => _error is null;

    /// <summary>Der Text weicht vom bestätigten Wert ab oder das Feld zeigt einen Fehler: Esc würde etwas zurückstellen.</summary>
    public bool HasPendingChange => (Text ?? "") != _confirmed || _error is not null;

    /// <summary>
    /// Ein gültiger Wert gilt schon beim Tippen (Betrag, Zahl; Ä311, Abweichung von Fluent in der Fluent-Ableitung Abschnitt 6). Datum und Uhrzeit
    /// bleiben aus: Zwischenstände wie „1“ oder „14“ sind mehrdeutig, und abhängige Felder (Ende nach Beginn) sollen nicht während des Tippens springen.
    /// </summary>
    protected virtual bool PublishesWhileTyping => false;

    protected abstract string AllowedCharacters { get; }

    /// <summary>Fehlertext für ein leeres Pflichtfeld.</summary>
    protected abstract string MissingMessage { get; }

    protected abstract FieldReading Read(string raw);

    /// <summary>Der gelesene Wert steht fest (bestätigt oder von außen gesetzt); die Unterklasse setzt ihren typisierten Wert.</summary>
    protected abstract void ValueRead(FieldReading reading);

    /// <summary>Pfeiltasten und Bildtasten; true, wenn die Taste einen Schritt ausgelöst hat.</summary>
    protected virtual bool HandleStepKey(KeyEventArgs e) => false;

    /// <summary>Ein Schritt per Rad; <paramref name="direction"/> ist +1 oder −1.</summary>
    protected virtual void Nudge(int direction, KeyModifiers modifiers)
    {
    }

    /// <summary>Liest den Text, schreibt bei Erfolg das Anzeigeformat zurück und meldet den Wert; bei einem Fehler bleibt der Text stehen.</summary>
    public bool Commit()
    {
        var reading = Evaluate(Text ?? "");
        if (reading.Error is { } error)
        {
            // Der Text bleibt stehen und das Feld zeigt den Fehler. Nach außen geht der unlesbare Text ohne Wert: so bleibt im ViewModel kein alter,
            // gültiger Wert zurück, der beim Speichern unbemerkt gälte; das ViewModel lehnt den Text selbst ab.
            Error = error;
            Publish(Text ?? "", FieldReading.Empty);
            return false;
        }

        Error = null;
        var display = reading.IsEmpty ? "" : reading.Display ?? "";
        SetText(display);
        _confirmed = display;
        Publish(display, reading);
        Committed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void Publish(string text, FieldReading reading)
    {
        _committing = true;
        try
        {
            CommittedText = text;
            ValueRead(reading);
        }
        finally
        {
            _committing = false;
        }
    }

    /// <summary>Stellt den letzten bestätigten Wert wieder her und markiert ihn (Esc).</summary>
    public void Revert()
    {
        SetText(_confirmed);
        Error = null;
        var reading = Read(_confirmed);
        Publish(_confirmed, reading.Error is null ? reading : FieldReading.Empty);
        SelectAll();
    }

    /// <summary>Prüft den Text erneut, ohne ihn zu bestätigen (z. B. wenn sich eine Grenze geändert hat, von der die Gültigkeit abhängt).</summary>
    protected void Revalidate()
    {
        if (!string.IsNullOrEmpty(Text) || _error is not null)
        {
            Error = Evaluate(Text ?? "").Error;
        }
    }

    /// <summary>Setzt den Text als Pfeiltaste, Rad oder Pfeilknopf und bestätigt ihn sofort; die Auswahl bleibt, damit weiteres Tippen ersetzt.</summary>
    protected void SetAndCommit(string display)
    {
        SetText(display);
        if (Commit())
        {
            SelectAll();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Escape && HasPendingChange)
        {
            Revert();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Enter)
        {
            // Gelingt das Bestätigen, geht Enter weiter zum Standardknopf; bei einem Fehler bleibt der Fokus hier.
            e.Handled = !Commit();
            return;
        }

        if (HandleStepKey(e))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        // Nur die Zeichen des Feldes kommen an; bei einem ganzen Stück Text (Einfügen, Eingabemethode) fallen die übrigen Zeichen weg.
        if (e.Text is { Length: > 0 } typed)
        {
            var allowed = new string([.. typed.Where(c => AllowedCharacters.Contains(c, StringComparison.Ordinal))]);
            if (allowed.Length == 0)
            {
                e.Handled = true;
                return;
            }

            e.Text = allowed;
        }

        base.OnTextInput(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Ein Klick setzt nur die Marke; alles andere (Tab, Umschalt+Tab, Fokus per Code) markiert, weil hier ersetzt wird.
        if (e.NavigationMethod != NavigationMethod.Pointer)
        {
            SelectAll();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // Das Rad ändert den Wert nur bei Fokus; sonst scrollt die Seite weiter.
        if (!IsFocused || e.Delta.Y == 0)
        {
            return;
        }

        Nudge(e.Delta.Y > 0 ? 1 : -1, e.KeyModifiers);
        e.Handled = true;
    }

    private FieldReading Evaluate(string raw)
    {
        var reading = Read(raw);
        return reading.Error is null && reading.IsEmpty && IsRequired ? FieldReading.Fail(MissingMessage) : reading;
    }

    private void SetText(string text)
    {
        if (Text == text)
        {
            return;
        }

        _settingText = true;
        try
        {
            Text = text;
        }
        finally
        {
            _settingText = false;
        }
    }

    private void OnCommittedTextChanged(string? value)
    {
        if (_committing)
        {
            return;
        }

        // Ein Wert von außen (ViewModel): so anzeigen, wie das Feld ihn schreiben würde; Unlesbares bleibt, wie es ist.
        var raw = value ?? "";
        var reading = Read(raw);
        var display = reading.Error is null ? reading.IsEmpty ? "" : reading.Display ?? "" : raw;
        SetText(display);
        _confirmed = display;
        Error = null;
        ValueRead(reading.Error is null ? reading : FieldReading.Empty);
    }

    /// <summary>
    /// Beim Tippen: Nach einem Fehler wird sofort neu geprüft; der Fehler verschwindet, sobald der Wert gültig ist. Felder mit
    /// <see cref="PublishesWhileTyping"/> melden den Wert dabei schon nach außen (ein unlesbarer Zwischenstand als Text ohne Wert), ohne den Text
    /// umzuformatieren und ohne neuen Fehler; der bestätigte Wert für Esc bleibt der vom letzten Bestätigen.
    /// </summary>
    private void OnTextEdited()
    {
        if (_settingText || (_error is null && !PublishesWhileTyping))
        {
            return;
        }

        var reading = Evaluate(Text ?? "");
        if (_error is not null)
        {
            Error = reading.Error;
        }

        if (PublishesWhileTyping)
        {
            var valid = reading.Error is null;
            Publish(valid ? reading.Display ?? "" : Text ?? "", valid ? reading : FieldReading.Empty);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Commit();
    }
}
