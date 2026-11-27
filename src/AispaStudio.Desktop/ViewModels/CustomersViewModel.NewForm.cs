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

    /// <summary>Name des E-Mail-Felds der Maske „Neue Kundin“ (<c>x:Name</c>); so verlangt <see cref="FocusTarget"/> den Fokus dorthin.</summary>
    public const string NewEmailField = "NewEmailBox";

    /// <summary>Name des Vornamen-Felds der Maske „Neue Kundin“ (<c>x:Name</c>); das erste Feld, wenn der Name fehlt.</summary>
    public const string NewFirstNameField = "NewFirstNameBox";

    /// <summary>Feldfehler unter dem Vornamen der Maske „Neue Kundin“, wenn weder Vor- noch Nachname steht; <see langword="null"/> ohne Fehler.</summary>
    [ObservableProperty]
    public partial string? NewNameError { get; private set; }

    /// <summary>Feldfehler unter dem E-Mail-Feld der Maske „Neue Kundin“; <see langword="null"/> ohne Fehler.</summary>
    [ObservableProperty]
    public partial string? NewEmailError { get; private set; }

    /// <summary>Das Feld, das nach einem Fehler den Fokus bekommen soll (Name des Feldes); die Ansicht setzt ihn und leert die Angabe (masken.md R8).</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    /// <summary>Pflicht ist nur ein Name (Vor- oder Nachname); Telefon, E-Mail und Notiz lassen sich in der Kundenkarte mit „Bearbeiten“ ergänzen.</summary>
    private bool ValidateNewName(bool focusOnError)
    {
        NewNameError = NewFirstName.Trim().Length > 0 || NewLastName.Trim().Length > 0
            ? null
            : "Bitte einen Vor- oder Nachnamen eintragen.";
        if (NewNameError is not null && focusOnError)
        {
            FocusTarget = NewFirstNameField;
        }

        return NewNameError is null;
    }

    /// <summary>
    /// Prüft die E-Mail-Adresse der Maske mit derselben Regel wie die API (leer oder mit „@“; <c>CustomerEndpoints</c>), nicht strenger.
    /// Beim Verlassen des Felds ohne Fokuswechsel, beim Anlegen mit Fokus auf das Feld.
    /// </summary>
    public bool ValidateNewEmail(bool focusOnError = false)
    {
        var email = NewEmail.Trim();
        NewEmailError = email.Length == 0 || email.Contains('@', StringComparison.Ordinal)
            ? null
            : "Keine gültige E-Mail-Adresse. Mit @ angeben, z. B. name@beispiel.at.";
        if (NewEmailError is not null && focusOnError)
        {
            FocusTarget = NewEmailField;
        }

        return NewEmailError is null;
    }

    partial void OnShowNewChanged(bool value) => RefreshCreateOffer();

    private void RefreshCreateOffer()
    {
        var text = Query.Trim();
        ShowCreateFromSearch = !ShowNew && Results.Count == 0 && text.Length >= 2;
        CreateFromSearchText = $"Neue Kundin „{text}“ anlegen";
    }
}
