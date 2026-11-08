using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Neue Kundin: weitere Angaben, Anlegen aus der leeren Suche, Buchung direkt danach (R07 Slice 140) ----------
public partial class CustomersViewModel
{
    /// <summary>„Weitere Angaben“ (Anrede, Titel, Rufname, Notiz) ist aufgeklappt; beim Öffnen der Maske immer zu.</summary>
    [ObservableProperty]
    public partial bool NewMoreOpen { get; set; }

    /// <summary>Die Suche links hat keinen Treffer: sie bietet „Neue Kundin ‚…‘ anlegen“ mit dem Suchtext als Namen an.</summary>
    [ObservableProperty]
    public partial bool ShowCreateFromSearch { get; private set; }

    [ObservableProperty]
    public partial string CreateFromSearchText { get; private set; } = "";

    /// <summary>„Anlegen und Termin buchen“: die Shell öffnet die Buchung mit dem neuen Gast.</summary>
    public Action<CustomerDto>? BookRequested { get; set; }

    partial void OnShowNewChanged(bool value) => RefreshCreateOffer();

    private void RefreshCreateOffer()
    {
        var text = Query.Trim();
        ShowCreateFromSearch = !ShowNew && Results.Count == 0 && text.Length >= 2;
        CreateFromSearchText = $"Neue Kundin „{text}“ anlegen";
    }
}
