using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AispaStudio.LiveTests;

/// <summary>Das Vordergrundfenster von Windows: nur damit prüft der Tester, dass Tasten in die App gehen und nie in ein fremdes Fenster.</summary>
internal static partial class NativeWindow
{
    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    /// <summary>Prozess des Vordergrundfensters; 0, wenn keines da ist.</summary>
    public static int ForegroundProcessId()
    {
        var window = GetForegroundWindow();
        if (window == 0)
        {
            return 0;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        return (int)processId;
    }

    /// <summary>Name des Prozesses zu <paramref name="processId"/> für die Abbruchmeldung.</summary>
    public static string ProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return $"{process.ProcessName} (Prozess {processId}, Titel „{process.MainWindowTitle}“)";
        }
        catch (ArgumentException)
        {
            return $"Prozess {processId}";
        }
        catch (InvalidOperationException)
        {
            return $"Prozess {processId}";
        }
    }
}

/// <summary>Das Vordergrundfenster gehört nicht der App: der Lauf endet, ohne Tasten zu senden.</summary>
internal sealed class ForegroundLostException(string message) : OperationCanceledException(message);
