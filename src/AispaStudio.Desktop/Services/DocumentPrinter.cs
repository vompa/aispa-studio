using System.Runtime.InteropServices;
using AispaStudio.Desktop.Views;

namespace AispaStudio.Desktop.Services;

/// <summary>Öffnet für ein PDF den Druckdialog von Windows; die Windows-App geht über WebView2, Tests antworten selbst (ADR 0092, ADR 0101).</summary>
public interface IDocumentPrinter
{
    /// <summary>
    /// Öffnet den Druckdialog mit dem PDF; null, sobald der Dialog offen ist, sonst die Meldung für den Toast („Kein Drucker eingerichtet“).
    /// Ob danach gedruckt oder abgebrochen wird, meldet Windows nicht zurück.
    /// </summary>
    Task<string?> PrintAsync(string fileName, byte[] pdf, CancellationToken ct);
}

/// <summary>
/// Schreibt das PDF in eine Temp-Datei und öffnet dafür den Druckdialog von Windows über ein unsichtbares WebView2 (<see cref="PrintDialogHost"/>, ADR 0101):
/// Drucker, Seiten, Kopien und Ausrichtung wählt der Nutzer, „Abbrechen“ druckt nichts. Kein Bondrucker (den bedient die Kassa über ESC/POS).
/// Alte Druckdateien räumt der nächste Druck weg.
/// </summary>
public sealed class WindowsPrintDialogPrinter : IDocumentPrinter
{
    public const string NoPrinter = "Kein Drucker eingerichtet";
    public const string NoDialog = "Der Druckdialog ließ sich nicht öffnen";

    private static readonly TimeSpan KeepFiles = TimeSpan.FromDays(1);

    // Ein Fenster für die ganze Laufzeit: der Dialog meldet nicht, wann er zugeht (PrintDialogHost).
    private static PrintDialogHost? SharedHost;

    public async Task<string?> PrintAsync(string fileName, byte[] pdf, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || !HasDefaultPrinter())
        {
            return NoPrinter;
        }

        if (!EmbeddedBrowser.UseNative)
        {
            return NoDialog;
        }

        var folder = Path.Combine(Path.GetTempPath(), "AispaStudio", "Druck");
        Directory.CreateDirectory(folder);
        CleanUp(folder);
        var path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}.pdf");
        // Auf dem UI-Thread bleiben: das Fenster des Dialogs gehört ihm.
        await File.WriteAllBytesAsync(path, pdf, ct).ConfigureAwait(true);
        SharedHost ??= new PrintDialogHost();
        return await SharedHost.ShowAsync(new Uri(path), ct).ConfigureAwait(true) ? null : NoDialog;
    }

    private static void CleanUp(string folder)
    {
        try
        {
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*.pdf").Where(f => DateTime.UtcNow - f.LastWriteTimeUtc > KeepFiles))
            {
                file.Delete();
            }
        }
        catch (IOException)
        {
            // Eine Datei ist noch beim Drucken offen: beim nächsten Mal.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool HasDefaultPrinter()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var size = 0;
        _ = GetDefaultPrinter(null, ref size);
        if (size == 0)
        {
            return false;
        }

        var name = new char[size];
        return GetDefaultPrinter(name, ref size) && size > 1;
    }

    // DllImport statt LibraryImport: das bräuchte unsicheren Code im Projekt, für einen Aufruf.
#pragma warning disable SYSLIB1054
    [DllImport("winspool.drv", EntryPoint = "GetDefaultPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDefaultPrinter([Out] char[]? buffer, ref int size);
#pragma warning restore SYSLIB1054
}
