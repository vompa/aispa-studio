using System.ComponentModel;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Die Hilfe-Seite in einem eingebetteten Browser (ADR 0079, unter Windows WebView2) für ein <see cref="HelpViewModel"/>: auf der Seite „Hilfe“ und
/// im Hilfe-Bereich der Leiste „KI und Hilfe“. Aus dem Ordner neben der App (über einen virtuellen Host, ohne Server und Internet) oder vom Server;
/// er speichert nichts dauerhaft (InPrivate), die Seite braucht keine Anmeldung; eigener Datenordner der App für die Hilfe.
/// Das native Browserfenster liegt über allem, was Avalonia in derselben Fläche zeichnet (<see cref="EmbeddedBrowser"/>): nichts darüber legen.
/// </summary>
public sealed class HelpBrowser : Panel
{
    public static readonly StyledProperty<HelpViewModel?> ModelProperty = AvaloniaProperty.Register<HelpBrowser, HelpViewModel?>(nameof(Model));

    private NativeWebView? _browser;
    private HelpViewModel? _watched;

    public HelpViewModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Der Browser steht; ein Platzhalter darunter kann weg.</summary>
    public event Action? Created;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelProperty && VisualRoot is not null)
        {
            Watch(Model);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Model);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Watch(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void Watch(HelpViewModel? model)
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnModelChanged;
        }

        _watched = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelChanged;
            ShowBrowser();
        }
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
        if (!EmbeddedBrowser.UseNative || _watched is not { Address: { } address } model)
        {
            return;
        }

        if (_browser is not null)
        {
            // Ein Fragezeichen schickt die Hilfe zu einer anderen Stelle.
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
        // Strg+Leertaste, F1 und Esc im Browser kommen als Nachricht der Seite (appShortcuts.ts).
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
        Children.Add(browser);
        Created?.Invoke();
    }
}
