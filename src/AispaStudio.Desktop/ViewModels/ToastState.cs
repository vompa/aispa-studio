using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Die Bestätigung einer eigenen Aktion als Toast: eine Zeile, ohne Schlusspunkt, höchstens <see cref="MaxLength"/> Zeichen, 5 Sekunden
/// (mit Aktion wie „Rückgängig“ 8), der Mauszeiger darüber hält sie an (design.md 5.10, meldungen.md M1). Die App hat einen Toast:
/// die Shell erzeugt ihn, zeigt ihn unten mittig im Seitenbereich und reicht ihn an die Seiten weiter; ein neuer ersetzt den alten.
/// Ein Hinweis, der stehen bleiben soll, ist eine InfoBar (M2), kein Toast.
/// </summary>
public sealed partial class ToastState(TimeProvider time) : ObservableObject
{
    public const int MaxLength = 60;

    /// <summary>Dauer ohne Aktion; die Bildtests schalten Zeitgeber ab dieser Frist ab (ViewTests.SampleTime).</summary>
    internal static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ActionDuration = TimeSpan.FromSeconds(8);

    private int _clock;
    private bool _held;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    public partial string? Text { get; private set; }

    /// <summary>Beschriftung der einen Aktion im Toast, z. B. „Rückgängig“ oder „Ordner öffnen“; ohne Aktion <c>null</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAction))]
    public partial string? ActionText { get; private set; }

    [ObservableProperty]
    public partial ICommand? Action { get; private set; }

    /// <summary>Der ungekürzte Text für den Tooltip, wenn die Zeile gekürzt wurde (z. B. ein langer Dateiname); sonst <c>null</c>.</summary>
    [ObservableProperty]
    public partial string? FullText { get; private set; }

    public bool IsShown => Text is not null;

    public bool HasAction => ActionText is not null;

    /// <summary>Zeile für den Toast: ohne Schlusspunkt und höchstens <see cref="MaxLength"/> Zeichen (sonst mit „…“ gekürzt).</summary>
    public static string Fit(string text)
    {
        var line = text.Trim().TrimEnd('.');
        return line.Length <= MaxLength ? line : string.Concat(line.AsSpan(0, MaxLength - 1).TrimEnd(), "…");
    }

    /// <summary>Ersetzt den bisherigen Toast samt seiner Aktion.</summary>
    public void Show(string text, string? actionText = null, ICommand? action = null)
    {
        ActionText = action is null ? null : actionText;
        Action = action is null || actionText is null ? null : action;
        var line = Fit(text);
        var full = text.Trim().TrimEnd('.');
        FullText = full == line ? null : full;
        Text = line;
        StartClock();
    }

    [RelayCommand]
    public void Close()
    {
        _clock++;
        // Der geschlossene Toast liegt nicht mehr unter dem Mauszeiger: der nächste läuft wieder ab.
        _held = false;
        FullText = null;
        Text = null;
        ActionText = null;
        Action = null;
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
            _ = ExpireAsync(clock, HasAction ? ActionDuration : Duration);
        }
    }

    private async Task ExpireAsync(int clock, TimeSpan duration)
    {
        await Task.Delay(duration, time).ConfigureAwait(true);
        if (clock == _clock)
        {
            Close();
        }
    }
}
