using System.Runtime.InteropServices;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Kurze Bestätigung neben einem Kopier-Knopf (Kundenkarte T-024, Kundenübersicht T-031): „Kopiert.“ für <see cref="Duration"/>, „Nicht kopiert.“, wenn
/// die Zwischenablage gerade belegt ist.
/// </summary>
public sealed partial class CopyFeedback(TimeProvider time) : ObservableObject
{
    private int _version;

    /// <summary>„Kopiert.“, „Nicht kopiert.“ oder null.</summary>
    [ObservableProperty]
    public partial string? Text { get; private set; }

    /// <summary>Wie lange „Kopiert.“ stehen bleibt.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Legt den Text in die Zwischenablage und bestätigt kurz. Vorher leeren: Nur ein geänderter Text wird vom Bildschirmleser angesagt, so auch beim
    /// zweiten Klick. Ist die Zwischenablage gerade von einem anderen Programm belegt, steht „Nicht kopiert.“ da.
    /// </summary>
    public async Task CopyAsync(IContactActions? actions, string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || actions is null)
        {
            return;
        }

        var version = ++_version;
        Text = null;
        try
        {
            await actions.CopyAsync(text.Trim()).ConfigureAwait(true);
            Text = "Kopiert.";
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or TimeoutException)
        {
            Text = "Nicht kopiert.";
        }

        _ = HideAsync(version);
    }

    /// <summary>Blendet die Bestätigung nach der Anzeigedauer aus, außer ein neuer Klick hat sie inzwischen erneuert.</summary>
    private async Task HideAsync(int version)
    {
        await Task.Delay(Duration, time).ConfigureAwait(true);
        if (version == _version)
        {
            Text = null;
        }
    }
}
