using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AispaStudio.Desktop.Controls;

/// <summary>Was unter einem Feld steht, in der Rangfolge: Fehler ersetzt Warnung, Warnung ersetzt Erfolg, der Erfolg ersetzt den Hilfetext.</summary>
public enum FieldMessageKind
{
    None,
    Help,
    Success,
    Warning,
    Error,
}

/// <summary>
/// Gemeinsamer Rahmen eines Eingabefelds (Design-Brief 5.2, 5.16): Beschriftung <b>über</b> dem Feld (Stufe „Beschriftung“, Abstand 4), bei Pflicht ein
/// Stern in der Textfarbe, darunter Hilfetext oder Meldung. Ein Fehler (rot, mit Symbol) ersetzt die Warnung, die Warnung den Erfolg, der Erfolg den
/// Hilfetext, damit nichts springt und nur eine Zeile steht. Die Meldung eines <see cref="ParsedField"/> (nicht lesbare Uhrzeit usw.) zeigt der Rahmen
/// von selbst; ein ViewModel setzt <see cref="Error"/>, <see cref="Warning"/> oder <see cref="Success"/>.
/// Der Rahmen bindet Name, Hilfetext und Pflicht an das Feld (<c>AutomationProperties.LabeledBy</c>, <c>HelpText</c>, <c>IsRequiredForForm</c>),
/// setzt am Feld die Klasse <c>invalid</c> bzw. <c>warning</c> (Rahmen danger bzw. warn, nur im Stil) und meldet Fehler dem Screenreader sofort.
/// Das Fragezeichen der Hilfe steht nicht hier, sondern am Gruppentitel (Brief 5.11).
/// </summary>
public sealed class FormField : ContentControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<FormField, string?>(nameof(Label));

    public static readonly StyledProperty<string?> HelpProperty = AvaloniaProperty.Register<FormField, string?>(nameof(Help));

    public static readonly StyledProperty<string?> ErrorProperty = AvaloniaProperty.Register<FormField, string?>(nameof(Error));

    public static readonly StyledProperty<string?> WarningProperty = AvaloniaProperty.Register<FormField, string?>(nameof(Warning));

    public static readonly StyledProperty<string?> SuccessProperty = AvaloniaProperty.Register<FormField, string?>(nameof(Success));

    public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<FormField, bool>(nameof(IsRequired));

    public static readonly StyledProperty<bool> HideMessageProperty = AvaloniaProperty.Register<FormField, bool>(nameof(HideMessage));

    private IDisposable? _fieldError;
    private CompositeDisposable? _counterSubscription;
    private TextBlock? _label;
    private Control? _message;
    private Icon? _icon;
    private TextBlock? _messageText;
    private TextBlock? _counter;

    static FormField()
    {
        LabelProperty.Changed.AddClassHandler<FormField>((field, _) => field.Update());
        HelpProperty.Changed.AddClassHandler<FormField>((field, _) => field.Update());
        ErrorProperty.Changed.AddClassHandler<FormField>((field, _) => field.Update());
        WarningProperty.Changed.AddClassHandler<FormField>((field, _) => field.Update());
        SuccessProperty.Changed.AddClassHandler<FormField>((field, _) => field.Update());
        IsRequiredProperty.Changed.AddClassHandler<FormField>((field, _) => field.Update());
        HideMessageProperty.Changed.AddClassHandler<FormField>((field, _) => field.Update());
        ContentProperty.Changed.AddClassHandler<FormField>((field, _) => field.OnContentChanged());
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Ein Satz mit Punkt unter dem Feld, höchstens eine Zeile.</summary>
    public string? Help
    {
        get => GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    public string? Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public string? Warning
    {
        get => GetValue(WarningProperty);
        set => SetValue(WarningProperty, value);
    }

    public string? Success
    {
        get => GetValue(SuccessProperty);
        set => SetValue(SuccessProperty, value);
    }

    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    /// <summary>
    /// Zeigt die Meldung nicht unter dem Feld: das Feld bleibt rot und meldet sie dem Screenreader, den Text zeigt eine eigene Zeile der Maske über die
    /// ganze Zeilenbreite (ein <see cref="FormField"/> ohne Inhalt, mit demselben Fehler). Für schmale Felder in einer Zeile (PLZ, Land), deren Fehlersatz
    /// in der Feldbreite über mehrere Zeilen bräche (Brief 5.2: eine Zeile).
    /// </summary>
    public bool HideMessage
    {
        get => GetValue(HideMessageProperty);
        set => SetValue(HideMessageProperty, value);
    }

    /// <summary>Was gerade unter dem Feld steht.</summary>
    public FieldMessageKind MessageKind { get; private set; }

    /// <summary>Der Text unter dem Feld, sonst null.</summary>
    public string? Message { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<TextBlock>("PART_Label");
        _message = e.NameScope.Find<Control>("PART_Message");
        _icon = e.NameScope.Find<Icon>("PART_MessageIcon");
        _messageText = e.NameScope.Find<TextBlock>("PART_MessageText");
        _counter = e.NameScope.Find<TextBlock>("PART_Counter");
        Update();
    }

    private void OnContentChanged()
    {
        _fieldError?.Dispose();
        _fieldError = Content is ParsedField field ? field.GetObservable(ParsedField.ErrorProperty).Subscribe(new ActionObserver<string?>(_ => Update())) : null;
        // Ein mehrzeiliges Feld mit Höchstlänge zeigt rechts in der Hilfezeile, wie viele Zeichen noch frei sind (Brief 5.16). Aus XAML gebaut, steht
        // AcceptsReturn erst nach dem Zuweisen des Inhalts fest, deshalb hört das Feld jedes TextBox auch auf diese Eigenschaft.
        _counterSubscription?.Dispose();
        _counterSubscription = Content is TextBox box
            ? new CompositeDisposable(
                box.GetObservable(TextBox.TextProperty).Subscribe(new ActionObserver<string?>(_ => Update())),
                box.GetObservable(TextBox.MaxLengthProperty).Subscribe(new ActionObserver<int>(_ => Update())),
                box.GetObservable(TextBox.AcceptsReturnProperty).Subscribe(new ActionObserver<bool>(_ => Update())))
            : null;
        Update();
    }

    private void Update()
    {
        var parsed = (Content as ParsedField)?.Error;
        var (kind, text) = !string.IsNullOrEmpty(Error) ? (FieldMessageKind.Error, Error)
            : !string.IsNullOrEmpty(parsed) ? (FieldMessageKind.Error, parsed)
            : !string.IsNullOrEmpty(Warning) ? (FieldMessageKind.Warning, Warning)
            : !string.IsNullOrEmpty(Success) ? (FieldMessageKind.Success, Success)
            : !string.IsNullOrEmpty(Help) ? (FieldMessageKind.Help, Help)
            : (FieldMessageKind.None, null);
        MessageKind = kind;
        Message = text;

        var counter = CounterText();
        if (_counter is not null)
        {
            _counter.Text = counter.Text;
            _counter.IsVisible = counter.Text is not null;
            _counter.Classes.Set("warn", counter.Warn);
            _counter.Classes.Set("muted", !counter.Warn);
        }

        if (_message is not null && _messageText is not null && _icon is not null)
        {
            _message.IsVisible = !HideMessage && (text is not null || counter.Text is not null);
            _messageText.Text = text;
            _messageText.Classes.Set("crit", kind == FieldMessageKind.Error);
            _messageText.Classes.Set("warn", kind == FieldMessageKind.Warning);
            _messageText.Classes.Set("ok", kind == FieldMessageKind.Success);
            _messageText.Classes.Set("muted", kind == FieldMessageKind.Help);
            _icon.IsVisible = kind is FieldMessageKind.Error or FieldMessageKind.Warning or FieldMessageKind.Success;
            _icon.Classes.Set("crit", kind == FieldMessageKind.Error);
            _icon.Classes.Set("warn", kind == FieldMessageKind.Warning);
            _icon.Classes.Set("ok", kind == FieldMessageKind.Success);
            _icon.Data = kind switch
            {
                FieldMessageKind.Error => Icons.ErrorCircle,
                FieldMessageKind.Warning => Icons.Warning,
                _ => Icons.CheckCircle,
            };
            // Ein Fehler unterbricht den Screenreader, eine Warnung wartet, bis er fertig ist.
            AutomationProperties.SetLiveSetting(_messageText, kind == FieldMessageKind.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        }

        if (Content is ParsedField parsedField)
        {
            parsedField.IsRequired = IsRequired;
        }

        if (Content is Control input)
        {
            input.Classes.Set("invalid", kind == FieldMessageKind.Error);
            input.Classes.Set("warning", kind == FieldMessageKind.Warning);
            AutomationProperties.SetIsRequiredForForm(input, IsRequired);
            AutomationProperties.SetHelpText(input, text ?? "");
            if (_label is not null)
            {
                AutomationProperties.SetLabeledBy(input, _label);
            }
        }
    }

    /// <summary>„noch 120 Zeichen“; ab 90 % des Limits in der Warnfarbe.</summary>
    private (string? Text, bool Warn) CounterText()
    {
        if (Content is not TextBox { AcceptsReturn: true, MaxLength: > 0 } box)
        {
            return (null, false);
        }

        var left = box.MaxLength - (box.Text?.Length ?? 0);
        return (left == 1 ? "noch 1 Zeichen" : $"noch {left} Zeichen", (box.Text?.Length ?? 0) >= box.MaxLength * 0.9);
    }

    private sealed class ActionObserver<T>(Action<T> action) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(T value) => action(value);
    }

    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
