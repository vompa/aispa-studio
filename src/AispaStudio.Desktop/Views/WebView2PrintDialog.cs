using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Ruft <c>ICoreWebView2_16::ShowPrintUI(COREWEBVIEW2_PRINT_DIALOG_KIND_SYSTEM)</c> auf, den Druckdialog von Windows (ADR 0101). Das Paket
/// Avalonia.Controls.WebView macht nur die Druckvorschau des Browsers öffentlich (<c>ShowPrintUI()</c>, braucht ein sichtbares Fenster); den Dialog von Windows
/// erreicht die App über den öffentlichen Plattform-Handle und die COM-Tabelle. Stelle 114: 3 Methoden von IUnknown, 110 bis ICoreWebView2_15, dann
/// <c>Print</c> (113) und <c>ShowPrintUI</c> (114); so heißt die Methode auch im Paket selbst („ShowPrintUI_114“).
/// </summary>
internal static class WebView2PrintDialog
{
    private const int ShowPrintUISlot = 114;
    private const int KindSystem = 1;
    private static readonly Guid IidCoreWebView2_16 = new("0EB34DC9-9F91-41E1-8639-95CD5943906B");

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ShowPrintUIFunction(IntPtr self, int kind);

    /// <summary>true, wenn WebView2 den Dialog angenommen hat; false ohne WebView2 oder mit einer zu alten Laufzeit (vor ICoreWebView2_16).</summary>
    public static bool TryShow(NativeWebView browser)
    {
        if (browser.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle { CoreWebView2: var core } || core == IntPtr.Zero)
        {
            return false;
        }

        var iid = IidCoreWebView2_16;
        if (Marshal.QueryInterface(core, in iid, out var webView16) != 0)
        {
            return false;
        }

        try
        {
            var table = Marshal.ReadIntPtr(webView16);
            var showPrintUI = Marshal.GetDelegateForFunctionPointer<ShowPrintUIFunction>(Marshal.ReadIntPtr(table, ShowPrintUISlot * IntPtr.Size));
            return showPrintUI(webView16, KindSystem) >= 0;
        }
        finally
        {
            Marshal.Release(webView16);
        }
    }
}
