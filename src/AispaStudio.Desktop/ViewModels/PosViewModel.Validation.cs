using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Fehler an den Feldern der Kassa (masken.md, R8): die Knöpfe bleiben aktiv, der Klick prüft und meldet am Feld ----------
public partial class PosViewModel
{
    /// <summary>Namen der Felder (<c>x:Name</c>) für <see cref="FocusTarget"/>: das erste ungültige Feld bekommt nach dem Klick den Fokus. Den Empfänger gibt es nach dem Bezahlen und unter „Belege“, deshalb je zweimal.</summary>
    public const string CountedCashField = "CountedCashBox";

    public const string MovementAmountField = "MovementAmountBox";

    public const string MovementReasonField = "MovementReasonBox";

    public const string RecipientNameField = "RecipientNameBox";

    public const string RecipientAddressField = "RecipientAddressBox";

    public const string RecipientVatIdField = "RecipientVatIdBox";

    public const string JournalRecipientNameField = "JournalRecipientNameBox";

    public const string JournalRecipientAddressField = "JournalRecipientAddressBox";

    public const string JournalRecipientVatIdField = "JournalRecipientVatIdBox";

    public const string DiscountPercentField = "DiscountPercentBox";

    public const string DiscountAmountField = "DiscountAmountBox";

    public const string ApproverField = "DiscountApproverBox";

    public const string ApprovalPinField = "DiscountApprovalPinBox";

    public const string VoucherValueField = "VoucherValueBox";

    public const string VoucherTreatmentField = "VoucherTreatmentBox";

    public const string PackageUnitsField = "PackageUnitsBox";

    public const string VoidReasonField = "VoidReasonBox";

    /// <summary>Das Feld, das die Ansicht fokussieren soll (der <c>x:Name</c>; die Ansicht leert die Angabe).</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    /// <summary>Fehler an den Feldern; jeder verschwindet mit der nächsten Eingabe im Feld.</summary>
    [ObservableProperty]
    public partial string? CountedCashError { get; private set; }

    [ObservableProperty]
    public partial string? MovementAmountError { get; private set; }

    [ObservableProperty]
    public partial string? MovementReasonError { get; private set; }

    [ObservableProperty]
    public partial string? RecipientNameError { get; private set; }

    [ObservableProperty]
    public partial string? RecipientAddressError { get; private set; }

    [ObservableProperty]
    public partial string? RecipientVatIdError { get; private set; }

    [ObservableProperty]
    public partial string? DiscountInputError { get; private set; }

    [ObservableProperty]
    public partial string? ApproverError { get; private set; }

    [ObservableProperty]
    public partial string? ApprovalPinError { get; private set; }

    [ObservableProperty]
    public partial string? VoucherValueError { get; private set; }

    [ObservableProperty]
    public partial string? VoucherTreatmentError { get; private set; }

    [ObservableProperty]
    public partial string? PackageUnitsError { get; private set; }

    [ObservableProperty]
    public partial string? VoidReasonError { get; private set; }

    partial void OnCountedCashChanged(string value) => CountedCashError = null;

    partial void OnMovementAmountChanged(string value) => MovementAmountError = null;

    partial void OnMovementReasonChanged(string value) => MovementReasonError = null;

    partial void OnRecipientNameChanged(string value) => RecipientNameError = null;

    partial void OnRecipientAddressChanged(string value) => RecipientAddressError = null;

    partial void OnRecipientVatIdChanged(string value) => RecipientVatIdError = null;

    partial void OnApproverChanged(QuickSwitchPerson? value) => ApproverError = null;

    partial void OnApprovalPinChanged(string value) => ApprovalPinError = null;

    partial void OnVoucherValueChanged(string value) => VoucherValueError = null;

    partial void OnPackageUnitsChanged(string value) => PackageUnitsError = null;

    partial void OnVoidReasonInputChanged(string value) => VoidReasonError = null;
}
