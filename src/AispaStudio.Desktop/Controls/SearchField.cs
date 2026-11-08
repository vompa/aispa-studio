using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Suchfeld (Design-Brief 5.16): Lupe links (16), rechts das Löschen-Kreuz (16), nur solange das Feld Text enthält (in normalen Feldern gibt es kein
/// Kreuz, damit Pflichtfelder nicht versehentlich geleert werden). Esc leert das Feld und bleibt darin; erst wenn es leer ist, geht Esc weiter
/// und schließt die Maske. Eine offene Trefferliste schließt die Ansicht vorher (zuerst die Liste, dann das Feld, dann die Maske).
/// </summary>
public sealed class SearchField : TextBox
{
    private readonly Button _clear = new()
    {
        Content = new Icon { Data = Icons.Close, Width = 16, Height = 16 },
        Focusable = false,
        IsTabStop = false,
        IsVisible = false,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 4, 0),
    };

    private readonly Button _add = new()
    {
        Content = new Icon { Data = Icons.Plus, Width = 16, Height = 16 },
        IsVisible = false,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 2, 0),
    };

    /// <summary>Befehl des „+“ im Feld (z. B. neue Kundin anlegen); ohne Befehl gibt es den Knopf nicht.</summary>
    public static readonly StyledProperty<System.Windows.Input.ICommand?> AddCommandProperty = AvaloniaProperty.Register<SearchField, System.Windows.Input.ICommand?>(nameof(AddCommand));

    /// <summary>Tooltip und Name des „+“ für den Screenreader.</summary>
    public static readonly StyledProperty<string?> AddTextProperty = AvaloniaProperty.Register<SearchField, string?>(nameof(AddText));

    /// <summary>Das „+“ lässt sich ausblenden, z. B. wenn schon ein Treffer gewählt ist.</summary>
    public static readonly StyledProperty<bool> ShowAddProperty = AvaloniaProperty.Register<SearchField, bool>(nameof(ShowAdd), true);

    static SearchField()
    {
        TextProperty.Changed.AddClassHandler<SearchField>((field, _) => field._clear.IsVisible = !string.IsNullOrEmpty(field.Text));
        AddCommandProperty.Changed.AddClassHandler<SearchField>((field, _) => field.UpdateAdd());
        ShowAddProperty.Changed.AddClassHandler<SearchField>((field, _) => field.UpdateAdd());
        AddTextProperty.Changed.AddClassHandler<SearchField>((field, _) =>
        {
            ToolTip.SetTip(field._add, field.AddText);
            AutomationProperties.SetName(field._add, field.AddText);
        });
    }

    public System.Windows.Input.ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public string? AddText
    {
        get => GetValue(AddTextProperty);
        set => SetValue(AddTextProperty, value);
    }

    public bool ShowAdd
    {
        get => GetValue(ShowAddProperty);
        set => SetValue(ShowAddProperty, value);
    }

    private void UpdateAdd()
    {
        _add.Command = AddCommand;
        _add.IsVisible = AddCommand is not null && ShowAdd;
    }

    public SearchField()
    {
        _add.Classes.Add("ghost");
        _add.Classes.Add("icon");
        _add.Classes.Add("searchadd");
        InnerLeftContent = new Icon { Data = Icons.Search, Width = 16, Height = 16, Margin = new Thickness(10, 0, 0, 0), Classes = { "searchicon" } };
        _clear.Classes.Add("ghost");
        _clear.Classes.Add("icon");
        _clear.Classes.Add("searchclear");
        ToolTip.SetTip(_clear, "Suche leeren");
        AutomationProperties.SetName(_clear, "Suche leeren");
        _clear.Click += (_, _) =>
        {
            Text = "";
            Focus();
        };
        InnerRightContent = new StackPanel { Orientation = Orientation.Horizontal, Children = { _add, _clear } };
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Tab und Fokus per Code markieren den Suchtext (er wird ersetzt, nicht bearbeitet); ein Klick setzt nur die Marke.
        if (e.NavigationMethod != NavigationMethod.Pointer)
        {
            SelectAll();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && !string.IsNullOrEmpty(Text))
        {
            Text = "";
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
