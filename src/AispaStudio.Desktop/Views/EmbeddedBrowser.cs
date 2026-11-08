using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AispaStudio.Desktop.Views;

/// <summary>Gemeinsames für den eingebetteten Browser der App: die Hilfe nach ADR 0079 (die eingebettete Verwaltung entfiel mit Slice 103).</summary>
internal static class EmbeddedBrowser
{
    /// <summary>Nur in der laufenden App; in Tests und im Designer zeigen die Ansichten nur den Platzhalter.</summary>
    public static bool UseNative { get; set; }

    /// <summary>
    /// Höhe des Streifens unter dem Browser, in dem der Knopf „KI und Hilfe“ steht (48 px plus Rand 20, Design-Brief 5.28): das native Browserfenster
    /// liegt über allem, was Avalonia in derselben Fläche zeichnet; ohne den Streifen wäre der Knopf verdeckt und nicht klickbar (R02 Slice 31, Ä74).
    /// </summary>
    public const double DockHeight = 68;

    // Avalonia 12.1.1 gibt die Zuordnung eines Ordners zu einem Hostnamen (ICoreWebView2_3) nicht öffentlich frei, nur den Zeiger auf den
    // CoreWebView2. Der Typ steckt in der Avalonia-Assembly (ein Test prüft, dass er nach einem Paketupdate noch da ist).
    internal const string FolderMappingInterface = "Avalonia.Controls.Win.WebView2.Interop.ICoreWebView2_3";

    private const int HostResourceAccessDenyCors = 2;

    /// <summary>
    /// Ordnet unter WebView2 einen Ordner einem virtuellen Hostnamen zu (<c>SetVirtualHostNameToFolderMapping</c>): <c>https://&lt;host&gt;/…</c>
    /// liefert dann die Dateien des Ordners, ohne Netz, Server und <c>file://</c>. Zugriff „DenyCors“: Seiten fremder Herkunft dürfen sie nicht
    /// laden. Nach <see cref="NativeWebView.AdapterCreated"/> und vor der ersten Navigation aufrufen; <c>false</c>, wenn es nicht ging.
    /// </summary>
    public static bool TryMapFolder(NativeWebView browser, string host, string folder)
    {
        if (!OperatingSystem.IsWindows()
            || browser.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle { CoreWebView2: var core }
            || core == IntPtr.Zero
            || typeof(NativeWebView).Assembly.GetType(FolderMappingInterface)?.GetMethod("SetVirtualHostNameToFolderMapping") is not { } method)
        {
            return false;
        }

        try
        {
            var webView = new StrategyBasedComWrappers().GetOrCreateObjectForComInstance(core, CreateObjectFlags.None);
            method.Invoke(webView, [host, folder, HostResourceAccessDenyCors]);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or COMException or System.Reflection.TargetInvocationException)
        {
            return false;
        }
    }

    /// <summary>Fremde Adressen öffnen sich im Standardbrowser, nie in der App.</summary>
    public static void OpenOutside(Control owner, Uri uri)
    {
        if (uri.Scheme is "http" or "https" or "mailto")
        {
            _ = TopLevel.GetTopLevel(owner)?.Launcher.LaunchUriAsync(uri);
        }
    }
}
