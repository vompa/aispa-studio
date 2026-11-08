using AispaStudio.Desktop.Services;
using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Eine Zeile unter einer KI-Ausgabe (Design-Brief 5.28, gekürzt am 29.10.2026 vom Auftraggeber): Hinweis und dahinter das aufklappbare „Warum?“.
/// Die Rückmeldung „Hilfreich / Nicht hilfreich“ gibt es nicht mehr.
/// <see cref="Disclaimer"/> ist der Hinweis des Servers, wenn er einen mitbringt (AI Act Art. 50); sonst gilt der zentrale Wortlaut aus <see cref="AiLabels"/>.
/// </summary>
public partial class AiFooter : UserControl
{
    public static readonly StyledProperty<string?> WhyProperty = AvaloniaProperty.Register<AiFooter, string?>(nameof(Why));

    /// <summary>Überschrift des aufklappbaren Abschnitts: „Warum?“, bei Antworten aus den Daten „Warum diese Antwort?“.</summary>
    public static readonly StyledProperty<string> WhyTitleProperty = AvaloniaProperty.Register<AiFooter, string>(nameof(WhyTitle), AiLabels.WhyTitle);

    /// <summary>Ein leerer Hinweis (ohne Hinweis des Servers) ergibt immer den zentralen Wortlaut.</summary>
    public static readonly StyledProperty<string?> DisclaimerProperty = AvaloniaProperty.Register<AiFooter, string?>(
        nameof(Disclaimer), AiLabels.Disclaimer, coerce: (_, value) => string.IsNullOrWhiteSpace(value) ? AiLabels.Disclaimer : value);

    public AiFooter()
    {
        InitializeComponent();
    }

    /// <summary>Quelle und Regel hinter der Ausgabe in zwei, drei Sätzen; leer heißt: kein Abschnitt.</summary>
    public string? Why
    {
        get => GetValue(WhyProperty);
        set => SetValue(WhyProperty, value);
    }

    public string WhyTitle
    {
        get => GetValue(WhyTitleProperty);
        set => SetValue(WhyTitleProperty, value);
    }

    /// <summary>Hinweis des Servers zur KI; leer heißt: der zentrale Wortlaut.</summary>
    public string? Disclaimer
    {
        get => GetValue(DisclaimerProperty);
        set => SetValue(DisclaimerProperty, value);
    }
}
