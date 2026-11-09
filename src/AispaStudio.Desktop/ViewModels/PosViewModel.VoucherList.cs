using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

public partial class PosViewModel
{
    /// <summary>Die Karte „Gutscheinliste“ (PDF-Ausgaben, Zusatz; ADR 0091), solange sie offen ist; sonst null.</summary>
    [ObservableProperty]
    public partial VoucherListPrintViewModel? VoucherList { get; private set; }

    /// <summary>Kassenaufsicht oder „Berichte mit Beträgen“: nur dann druckt die Karte; sonst sind die Knöpfe gesperrt, mit Hinweis.</summary>
    public bool MayPrintVoucherList => MaySuperviseCash || MaySeeAmountReports;

    [RelayCommand]
    private void ToggleVoucherList() =>
        VoucherList = VoucherList is null && Register is { } register
            ? new VoucherListPrintViewModel(register.LocationId, MayPrintVoucherList, _api, Dialogs, ShowToast, () => VoucherList = null)
            : null;
}
