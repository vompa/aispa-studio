using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Die Bestätigung einer eigenen Aktion als Toast: eine Zeile, ohne Schlusspunkt, 5 Sekunden, der Mauszeiger darüber hält sie an (design.md 5.10, meldungen.md M1).
/// Ein Hinweis, der stehen bleiben soll, ist eine InfoBar (M2), kein Toast.
/// </summary>
public sealed partial class ToastState(TimeProvider time) : ObservableObject
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);

    private int _clock;
    private bool _held;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    public partial string? Text { get; private set; }

    public bool IsShown => Text is not null;

    /// <summary>Ersetzt den bisherigen Toast; ein Schlusspunkt fällt weg.</summary>
    public void Show(string text)
    {
        Text = text.TrimEnd('.');
        StartClock();
    }

    [RelayCommand]
    public void Close()
    {
        _clock++;
        Text = null;
    }

    public void Hold(bool held)
    {
        _held = held;
        StartClock();
    }

    private void StartClock()
    {
        var clock = ++_clock;
        if (Text is not null && !_held)
        {
            _ = ExpireAsync(clock);
        }
    }

    private async Task ExpireAsync(int clock)
    {
        await Task.Delay(Duration, time).ConfigureAwait(true);
        if (clock == _clock)
        {
            Text = null;
        }
    }
}
