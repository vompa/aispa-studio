using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.Services;

/// <summary>Wendet das gewählte Farbschema an und merkt es sich für den nächsten Start.</summary>
public sealed partial class ThemeController : ObservableObject
{
    private readonly ISettingsStore _store;

    public ThemeController(ISettingsStore store)
    {
        _store = store;
        Choice = store.Load().Theme;
    }

    /// <summary>Einstellungen dieses Arbeitsplatzes, auch für Drucker und Terminal.</summary>
    public ISettingsStore Store => _store;

    [ObservableProperty]
    public partial ThemeChoice Choice { get; private set; }

    /// <summary>System (wie Windows), hell, dunkel, dann wieder von vorn.</summary>
    public void Next() => Set(Choice switch
    {
        ThemeChoice.System => ThemeChoice.Light,
        ThemeChoice.Light => ThemeChoice.Dark,
        _ => ThemeChoice.System,
    });

    public void Set(ThemeChoice choice)
    {
        Choice = choice;
        _store.Save(_store.Load() with { Theme = choice });
        Apply();
    }

    public void Apply()
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = Choice switch
            {
                ThemeChoice.Light => ThemeVariant.Light,
                ThemeChoice.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }
}
