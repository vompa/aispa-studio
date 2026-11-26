using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Unsichtbares Fenster mit einem WebView2, das ein PDF lädt und den Druckdialog von Windows dafür öffnet (ADR 0101). Es liegt außerhalb des Bildschirms
/// und ohne Eintrag in der Taskleiste; der Nutzer sieht nur den Dialog. Ein Fenster für die ganze Laufzeit der App: der Dialog meldet nicht, wann er zugeht,
/// und schlösse das Fenster vorher, ginge der Dialog mit. Es schließt mit dem Hauptfenster, damit es die App nicht am Leben hält.
/// </summary>
internal sealed class PrintDialogHost
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(20);

    // Der PDF-Betrachter zeichnet nach „geladen“ noch; zu früh gedruckt wäre die Seite leer (Probe T-099: 1,5 s reichen).
    private static readonly TimeSpan RenderTime = TimeSpan.FromSeconds(1.5);

    private readonly Window _window;
    private readonly NativeWebView _browser;
    private readonly TaskCompletionSource _adapterReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<bool>? _loaded;

    public PrintDialogHost()
    {
        // Ohne die Vorgabe wählt das Paket unter Windows auch WebView1, und das bricht beim Laden mit E_ABORT ab (Probe T-099).
        _browser = new NativeWebView { AdapterPreference = [WebViewAdapterType.WebView2] };
        _browser.EnvironmentRequested += (_, e) =>
        {
            e.EnableDevTools = false;
            if (e is WindowsWebView2EnvironmentRequestedEventArgs webView2)
            {
                webView2.UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudio", "druck");
                webView2.IsInPrivateModeEnabled = true;
                webView2.Language = "de-AT";
            }
        };
        _browser.AdapterCreated += (_, _) => _adapterReady.TrySetResult();
        _browser.NavigationCompleted += (_, e) => _loaded?.TrySetResult(e.IsSuccess);
        _window = new Window
        {
            Title = "AISPA Studio – Drucken",
            Width = 300,
            Height = 200,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(-4000, -4000),
            Content = _browser,
        };
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } main })
        {
            main.Closed += (_, _) => _window.Close();
        }
    }

    /// <summary>Lädt das PDF und öffnet den Druckdialog; false, wenn das nicht gelingt. Kehrt zurück, sobald der Dialog aufgeht, nicht wenn er schließt.</summary>
    public async Task<bool> ShowAsync(Uri pdf, CancellationToken ct)
    {
        try
        {
            if (!_window.IsVisible)
            {
                _window.Show();
            }

            await _adapterReady.Task.WaitAsync(LoadTimeout, ct).ConfigureAwait(true);
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _loaded = loaded;
            _browser.Navigate(pdf);
            if (!await loaded.Task.WaitAsync(LoadTimeout, ct).ConfigureAwait(true))
            {
                return false;
            }

            await Task.Delay(RenderTime, ct).ConfigureAwait(true);
            return WebView2PrintDialog.TryShow(_browser);
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }
}
