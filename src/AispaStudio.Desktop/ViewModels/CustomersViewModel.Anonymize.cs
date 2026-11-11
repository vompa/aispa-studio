using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Gast anonymisieren: nur Inhaber und Geschäftsführung; die Karte eines anonymisierten Gastes ist nur noch lesbar ----------
public partial class CustomersViewModel
{
    /// <summary>Der Gast ist anonymisiert („Gelöschter Gast“): Bearbeiten, Buchen und Kassieren fehlen.</summary>
    public bool IsAnonymized => Selected?.DeletedAt is not null;

    /// <summary>Hinweis im Kopf der Karte, z. B. „Anonymisiert am 10.11.2026“.</summary>
    public string? AnonymizedNotice => Selected?.DeletedAt is { } at
        ? $"Anonymisiert am {DayLayout.Local(at, time.LocalTimeZone).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}"
        : null;

    /// <summary>Der Menüeintrag „Gast anonymisieren …“; der Server prüft dieselbe Ebene (403).</summary>
    public bool MayAnonymize => User?.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager && !IsAnonymized;

    /// <summary>Rückfrage, dann POST; bei Erfolg schließt die Karte und die Liste lädt neu, ein 409 zeigt das Offene.</summary>
    [RelayCommand]
    private async Task AskAnonymizeAsync()
    {
        if (Selected is not { } customer || !MayAnonymize)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Gast anonymisieren?",
            $"Name, Kontaktdaten, Notizen, Anamnese und Online-Zugang von {customer.InHouseName} werden endgültig gelöscht. "
            + "Termine und Belege bleiben als „Gelöschter Gast“ erhalten. Das lässt sich nicht rückgängig machen.",
            "Endgültig anonymisieren")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await api.PostAsync<object>($"api/customers/{customer.Id}/anonymize", null).ConfigureAwait(true);
        if (!result.Ok)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        Selected = null;
        await SearchAsync(Query).ConfigureAwait(true);
    }
}
