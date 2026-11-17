using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace AispaStudio.Desktop.Views;

/// <summary>Seite „Schließzeiten“ mit Schließzeiten und Raumsperren in einer Liste und dem Formular als Seitenleiste (ADR 0098).</summary>
public partial class ClosuresPageView : UserControl
{
    public ClosuresPageView() => InitializeComponent();

    private void OnToastEntered(object? sender, PointerEventArgs e) => (DataContext as ClosuresPageViewModel)?.HoldToast(true);

    private void OnToastExited(object? sender, PointerEventArgs e) => (DataContext as ClosuresPageViewModel)?.HoldToast(false);
}
