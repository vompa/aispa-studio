using System.Runtime.InteropServices;
using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Befund B8: Die Hover-Karte eines Termins ist ein eigenes Fenster und lag rechts der Karte über dem Knopf „Einchecken“ im Seitenpanel;
/// der Mausklick traf das Fenster der Karte, schloss sie und löste den Knopf nicht aus (5 von 9, später 7 von 10 Klicks verloren).
/// Windows reicht Mausklicks nur durch ein Fenster hindurch, wenn es zugleich WS_EX_LAYERED und WS_EX_TRANSPARENT trägt und sichtbar bleibt (Deckkraft gesetzt);
/// der Test prüft diese Stile an einem echten Fenster. Die Wirkung auf die App belegt die Live-Probe (docs/umsetzung/live-sichtpruefung-2026-11-09.md, B8).
/// </summary>
public sealed class ToolTipClickThroughTests
{
    private const int ExStyleIndex = -20;
    private const nint Layered = 0x80000;
    private const nint Transparent = 0x20;
    private const uint Popup = 0x80000000;

    [Fact]
    public void Ein_Tooltip_Fenster_laesst_Mausklicks_durch()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Nur unter Windows (Fenster mit WS_EX_LAYERED).");
        var window = CreateWindowEx(0, "STATIC", "B8", Popup, 0, 0, 10, 10, 0, 0, 0, 0);
        Assert.NotEqual(0, window);
        try
        {
            Assert.Equal(0, GetWindowLongPtr(window, ExStyleIndex) & (Layered | Transparent));

            ToolTipBehavior.MakeClickThrough(window);

            Assert.Equal(Layered | Transparent, GetWindowLongPtr(window, ExStyleIndex) & (Layered | Transparent));
            Assert.True(GetLayeredWindowAttributes(window, out _, out var alpha, out var flags));
            Assert.Equal(255, alpha);
            Assert.Equal(2u, flags & 2u); // LWA_ALPHA: ohne Deckkraft bliebe das Fenster unsichtbar
        }
        finally
        {
            DestroyWindow(window);
        }
    }

    // DllImport statt LibraryImport: das bräuchte unsicheren Code im Testprojekt, für vier Aufrufe.
#pragma warning disable SYSLIB1054
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(int exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(nint window, out uint colorKey, out byte alpha, out uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
#pragma warning restore SYSLIB1054
}
