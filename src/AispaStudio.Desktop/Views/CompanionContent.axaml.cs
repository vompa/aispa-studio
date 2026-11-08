using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Inhalt von „KI und Hilfe“: Erklärung zur offenen Seite und KI-Assistent. Im Kalender ist er ein Reiter der Seitenleiste (Design-Brief 5.28),
/// auf den übrigen Seiten steht er in der Leiste der Shell.
/// </summary>
public partial class CompanionContent : UserControl
{
    private CompanionViewModel? _model;

    public CompanionContent()
    {
        InitializeComponent();
        // Tunnel: das Feld bewegt bei Pfeil nach oben sonst nur die Marke.
        CompanionQuestion.AddHandler(KeyDownEvent, OnQuestionKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.Restarted -= FocusQuestion;
        }

        _model = DataContext as CompanionViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
            _model.Restarted += FocusQuestion;
        }
    }

    /// <summary>Neue Antwort: die Leiste scrollt ihren Kopf ins Bild, damit nach dem Fragen nie „nichts zu sehen“ ist (Ä316).</summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CompanionViewModel.Answer) or nameof(CompanionViewModel.Error))
        {
            Dispatcher.UIThread.Post(() => (_model?.HasError == true ? (Control)ErrorBar : AnswerCard).BringIntoView(), DispatcherPriority.Background);
        }
    }

    /// <summary>Pfeil nach oben im leeren Feld: die letzte Frage kommt zurück, die Marke steht am Ende (Ä316).</summary>
    private void OnQuestionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up && e.KeyModifiers == KeyModifiers.None && _model?.RecallQuestion() == true)
        {
            CompanionQuestion.CaretIndex = CompanionQuestion.Text?.Length ?? 0;
            e.Handled = true;
        }
    }

    /// <summary>Setzt den Fokus ins Fragefeld, damit man gleich lostippen kann.</summary>
    public void FocusQuestion() => CompanionQuestion.Focus();

    /// <summary>„Kopieren“ unter einer Antwort mit Kontaktdaten oder Anreise: legt den Text in die Zwischenablage und meldet „Kopiert.“.</summary>
    private async void OnCopyAnswerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string text } && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);
            (DataContext as CompanionViewModel)?.MarkCopied();
        }
    }
}
