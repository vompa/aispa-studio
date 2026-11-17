using System.ComponentModel;
using System.Diagnostics;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Services;

/// <summary>Öffnet den Explorer von Windows mit der Datei markiert („Ordner öffnen“ nach dem Speichern eines Exports).</summary>
public sealed class ExplorerFolderOpener : IFolderOpener
{
    public void Reveal(string path)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { $"/select,{path}" }, UseShellExecute = false });
        }
        catch (Win32Exception)
        {
            // Kein Explorer (z. B. Test-Rechner ohne Oberfläche): die Datei ist trotzdem gespeichert.
        }
    }
}
