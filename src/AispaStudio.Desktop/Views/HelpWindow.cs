using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>Hilfe im eigenen Fenster, z. B. vor der Anmeldung; immer dasselbe Fenster, das nach vorne kommt.</summary>
public sealed class HelpWindow : Window
{
    private static HelpWindow? Current;

    private readonly HelpViewModel _vm = new();

    private HelpWindow()
    {
        Title = "AISPA Studio · Hilfe";
        Width = 960;
        Height = 680;
        Content = new HelpView { DataContext = _vm };
        Closed += (_, _) => Current = null;
    }

    public static void Show(string? topic, string? section)
    {
        Current ??= new HelpWindow();
        Current._vm.Show(topic, section);
        Current.Show();
        Current.Activate();
    }
}
