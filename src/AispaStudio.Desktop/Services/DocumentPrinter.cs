using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AispaStudio.Desktop.Services;

/// <summary>Druckt ein PDF auf dem Standarddrucker von Windows; die Windows-App geht über die Shell, Tests antworten selbst (ADR 0091).</summary>
public interface IDocumentPrinter
{
    /// <summary>Gibt das PDF an den Standarddrucker; null, wenn es unterwegs ist, sonst die Meldung für den Toast („Kein Drucker eingerichtet“).</summary>
    Task<string?> PrintAsync(string fileName, byte[] pdf, CancellationToken ct);
}

/// <summary>
/// Schreibt das PDF in eine Temp-Datei und ruft das Verb <c>print</c> der Windows-Shell auf: das PDF-Programm des PCs druckt auf dem Standarddrucker ohne eigenen Dialog.
/// Kein eigener Druckdialog, kein Bondrucker (den bedient die Kassa über ESC/POS). Alte Druckdateien räumt der nächste Druck weg.
/// </summary>
public sealed class ShellDocumentPrinter : IDocumentPrinter
{
    public const string NoPrinter = "Kein Drucker eingerichtet";
    public const string NoPdfApp = "Kein PDF-Programm zum Drucken eingerichtet";

    private static readonly TimeSpan KeepFiles = TimeSpan.FromDays(1);

    public async Task<string?> PrintAsync(string fileName, byte[] pdf, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || !HasDefaultPrinter())
        {
            return NoPrinter;
        }

        var folder = Path.Combine(Path.GetTempPath(), "AispaStudio", "Druck");
        Directory.CreateDirectory(folder);
        CleanUp(folder);
        var path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, pdf, ct).ConfigureAwait(false);
        try
        {
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "print", WindowStyle = ProcessWindowStyle.Hidden });
            return null;
        }
        catch (Win32Exception)
        {
            return NoPdfApp;
        }
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
