using System.ComponentModel;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Hilfe (ADR 0079): ein eingebetteter Browser (unter Windows WebView2) mit der Hilfe-Seite, aus dem Ordner neben der App
/// (über einen virtuellen Host, ohne Server und Internet) oder vom Server. Er speichert nichts dauerhaft (InPrivate), die Seite braucht keine Anmeldung;
/// eigener Datenordner der App für die Hilfe.
/// </summary>
public partial class HelpView : UserControl
{
    private NativeWebView? _browser;
    private HelpViewModel? _model;

    public HelpView()
    {
        InitializeComponent();
        // Der Knopf „KI und Hilfe“ steht unter dem Browser, nicht dahinter.
        Host.Margin = new Thickness(0, 0, 0, EmbeddedBrowser.DockHeight);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is HelpViewModel model)
        {
            _model = model;
            model.PropertyChanged += OnModelChanged;
            model.EnsureShown();
            ShowBrowser();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HelpViewModel.Address))
        {
            ShowBrowser();
        }
    }

    // Vom Server prüft die App die Seite vorher: eine 404 der API oder JSON bekommt der Browser sonst roh zu sehen (Ä155).
    private static async Task NavigateAsync(NativeWebView browser, HelpViewModel model, Uri address)
    {
        try
        {
            if (!model.IsLocal && await HelpPageCheck.ProblemAsync(address) is { } problem)
            {
                // Hat inzwischen ein neuer Klick eine andere Adresse gesetzt, gilt diese Antwort nicht mehr.
                if (model.Address == address)
                {
                    browser.IsVisible = false;
                    model.ReportServerProblem(problem);
                }

                return;
            }

            if (model.Address == address)
            {
                browser.IsVisible = true;
                browser.Navigate(address);
            }
        }
        catch (InvalidOperationException ex)
        {
            browser.IsVisible = false;
            model.ReportServerProblem($"Die Hilfe lässt sich nicht öffnen: {ex.Message}");
        }
    }

    private void ShowBrowser()
    {
        if (!EmbeddedBrowser.UseNative || _model is not { Address: { } address } model)
        {
            return;
        }

        if (_browser is not null)
        {
            // Der Fragezeichen-Knopf schickt die Hilfe zu einer anderen Stelle.
            _ = NavigateAsync(_browser, model, address);
            return;
        }

        var browser = new NativeWebView();
        browser.EnvironmentRequested += (_, e) =>
        {
            e.EnableDevTools = false;
            if (e is WindowsWebView2EnvironmentRequestedEventArgs webView2)
            {
                webView2.UserDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudio", "hilfe");
                webView2.IsInPrivateModeEnabled = true;
                webView2.Language = "de-AT";
            }
        };
        browser.AdapterCreated += (_, _) =>
        {
            // Der Ordner neben der App wird zu https://help.aispa.local/; das Laden geht erst danach los.
            if (model.IsLocal && !EmbeddedBrowser.TryMapFolder(browser, HelpViewModel.VirtualHost, model.LocalFolder))
            {
                browser.IsVisible = false;
                model.ReportMappingFailed();
                return;
            }

            _ = NavigateAsync(browser, model, model.Address ?? address);
        };
        browser.NavigationStarted += (_, e) =>
        {
            if (e.Request is { } uri && !model.IsOwnPage(uri))
            {
                e.Cancel = true;
                EmbeddedBrowser.OpenOutside(this, uri);
            }
        };
        // Strg+Leertaste im Browser kommt als Nachricht der Seite (appShortcuts.ts); die Shell schaltet „KI und Hilfe“.
        browser.WebMessageReceived += (_, e) => model.OnWebMessage(e.Body);
        browser.NewWindowRequested += (_, e) =>
        {
            if (e.Request is { } uri)
            {
                e.Handled = true;
                EmbeddedBrowser.OpenOutside(this, uri);
            }
        };

        _browser = browser;
        Host.Children.Add(browser);
        Placeholder.IsVisible = false;
    }
}
