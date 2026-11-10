using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- „…“ › „Kunden importieren …“ (Export/Import, Aufgabe E) ----------
public partial class CustomersViewModel
{
    /// <summary>Der Dateidialog des Fensters für die Importdatei; die Ansicht setzt ihn beim Anzeigen.</summary>
    public IFilePicker? FilePicker { get; set; }

    /// <summary>Kunden importiert nur die Leitung (Inhaberin, Geschäftsführung); der Server prüft dasselbe.</summary>
    public bool CanImport => IsLeadership;

    private bool IsLeadership => User?.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager;

    /// <summary>Öffnet den Dialog mit den vier Schritten; nach einem echten Import meldet die Seite das Ergebnis unten und lädt die Liste neu.</summary>
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportCustomersAsync()
    {
        if (Dialogs is not { } dialogs)
        {
            return;
        }

        CustomerImportViewModel? dialog = null;
        var answer = await dialogs.ShowAsync(done => dialog = new CustomerImportViewModel(api, FilePicker, () => Export.Files, done)).ConfigureAwait(true);
        if (!answer.IsConfirmed || dialog?.Outcome is not { } result)
        {
            return;
        }

        ShowToast($"Importiert: {CustomerImportViewModel.ResultText(result)}");
        await SearchAsync(Query).ConfigureAwait(true);
    }
}
