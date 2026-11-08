using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Wann eine rechte Seitenleiste im Layout steht: solange sie offen ist und danach noch so lange, wie das Ausblenden dauert (120 ms),
/// damit das Schließen sichtbar ausklingt statt zu springen. Bei Kurzbefehlen (<see cref="UsingKeyboard"/>) entfällt die Bewegung ganz.
/// Gemeinsam für die Leisten des Kalenders und für „KI und Hilfe“.
/// </summary>
public sealed partial class PanelPresence(TimeProvider time) : ObservableObject
{
    /// <summary>Dauer des Ausblendens; Einblenden dauert 200 ms (Styles.axaml, „Border.sidepanel“).</summary>
    public static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(120);

    private int _clock;

    /// <summary>Die Leiste nimmt Platz im Layout ein.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <summary>Die letzte Eingabe kam von der Tastatur: Leisten erscheinen und verschwinden ohne spürbare Bewegung. Ein Mausklick schaltet es wieder aus.</summary>
    [ObservableProperty]
    public partial bool UsingKeyboard { get; set; }

    /// <summary>Die Leiste wurde geöffnet (<paramref name="open"/>) oder geschlossen.</summary>
    public void Update(bool open)
    {
        var clock = ++_clock;
        if (open)
        {
            IsVisible = true;
        }
        else if (UsingKeyboard)
        {
            IsVisible = false;
        }
        else
        {
            _ = HideAfterFadeAsync(clock);
        }
    }

    /// <summary>Setzt den Stand sofort, ohne Bewegung: die Seite wechselt, und mit ihr, wo der Assistent steht (Reiter oder Leiste der Shell).</summary>
    public void Snap(bool open)
    {
        _clock++;
        IsVisible = open;
    }

    private async Task HideAfterFadeAsync(int clock)
    {
        await Task.Delay(CloseDuration, time).ConfigureAwait(true);
        if (clock == _clock)
        {
            IsVisible = false;
        }
    }
}
