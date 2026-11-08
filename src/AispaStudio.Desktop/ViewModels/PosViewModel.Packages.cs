using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Gekauftes Paket des Gastes, das Termine auf dieser Rechnung abdecken kann.</summary>
public sealed record PackageHint(VoucherDto Voucher, string Text, string ButtonText, decimal Amount);

// ---------- Pakete (z. B. 10er-Block): verkaufen und beim Kassieren abziehen ----------
public partial class PosViewModel
{
    /// <summary>Zahl der Behandlungen im Paket.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SellPackageCommand))]
    public partial string PackageUnits { get; set; } = "10";

    /// <summary>Paketpreis; leer heißt Einzelpreis mal Anzahl.</summary>
    [ObservableProperty]
    public partial string PackagePrice { get; set; } = "";

    /// <summary>Freiwilliges Ablaufdatum, leer heißt unbefristet.</summary>
    [ObservableProperty]
    public partial string PackageValidUntil { get; set; } = "";

    public ObservableCollection<PackageHint> PackageHints { get; } = [];

    public bool HasPackageHints => PackageHints.Count > 0;

    /// <summary>Paket für die gewählte Behandlung verkaufen; der Gast der Rechnung bekommt es beim Bezahlen.</summary>
    [RelayCommand(CanExecute = nameof(CanSellPackage))]
    private async Task SellPackageAsync()
    {
        if (VoucherTreatment is not { } treatment || !int.TryParse(PackageUnits.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var units))
        {
            return;
        }

        decimal? price = null;
        if (PackagePrice.Trim().Length > 0)
        {
            if (Money.Parse(PackagePrice) is not { } p || p <= 0)
            {
                Error = "Paketpreis nicht lesbar.";
                return;
            }

            price = p;
        }

        DateOnly? until = null;
        var validUntil = DateInputParser.Parse(PackageValidUntil, Today);
        if (validUntil.Error is { } validUntilError)
        {
            Error = validUntilError;
            return;
        }

        until = validUntil.Value;

        await AddLineAsync(new BillLineInput(BillLineKind.Voucher, null, null, 1, price, 0, null, null, null, VoucherKind.SinglePurpose, treatment.Id,
            Units: units, ValidUntil: until)).ConfigureAwait(true);
        if (Error is null)
        {
            VoucherTreatment = null;
            PackagePrice = "";
            PackageValidUntil = "";
        }
    }

    private bool CanSellPackage() => VoucherTreatment is not null
        && int.TryParse(PackageUnits.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var units) && units is >= 2 and <= 50;

    partial void OnVoucherTreatmentChanged(TreatmentDto? value) => SellPackageCommand.NotifyCanExecuteChanged();

    partial void OnBillChanged(BillDto? value)
    {
        _ = LoadPackagesAsync(value);
        _ = LoadPromotionsAsync(value);
        _ = LoadRecommendationsAsync(value);
    }

    /// <summary>Pakete des Gastes, die zu Terminen auf der Rechnung passen; „Abziehen“ trägt sie als Gutschein-Zahlung ein.</summary>
    private async Task LoadPackagesAsync(BillDto? bill)
    {
        if (bill?.CustomerId is not { } customerId || !bill.Lines.Any(l => l.Kind == BillLineKind.Appointment && l.Amount > l.PaidAmount))
        {
            ShowPackages([]);
            return;
        }

        var result = await _api.GetAsync<List<VoucherDto>>($"api/pos/customers/{customerId}/packages").ConfigureAwait(true);
        if (Bill?.Id != bill.Id)
        {
            return;
        }

        var hints = new List<PackageHint>();
        foreach (var package in result.Value ?? [])
        {
            var lines = bill.Lines.Where(l => l.Kind == BillLineKind.Appointment && l.TreatmentId == package.TreatmentId && l.Amount > l.PaidAmount)
                .Take(package.UnitsLeft ?? 0).ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            var name = Treatments.FirstOrDefault(t => t.Id == package.TreatmentId)?.Name ?? "Behandlung";
            hints.Add(new PackageHint(package,
                $"Paket {name}: {package.UnitsLeft} von {package.Units} offen{(package.ValidUntil is { } until ? $", gültig bis {until:dd.MM.yyyy}" : "")}",
                lines.Count == 1 ? "1 Termin abziehen" : $"{lines.Count} Termine abziehen",
                lines.Sum(l => l.Amount - l.PaidAmount)));
        }

        ShowPackages(hints);
    }

    private void ShowPackages(List<PackageHint> hints)
    {
        PackageHints.Clear();
        foreach (var hint in hints)
        {
            PackageHints.Add(hint);
        }

        OnPropertyChanged(nameof(HasPackageHints));
    }

    /// <summary>Trägt das Paket als Gutschein-Zahlung über den Betrag der passenden Termine ein; der Server zieht je Termin eine Behandlung ab.</summary>
    [RelayCommand]
    private void UsePackage(PackageHint hint)
    {
        if (Payments.FirstOrDefault(p => p.IsVoucher) is not { } entry)
        {
            Error = "Gutscheine sind an dieser Kasse nicht als Zahlungsart eingestellt.";
            return;
        }

        entry.VoucherCode = hint.Voucher.Code;
        entry.Amount = Money.Input(hint.Amount, Currency);
    }
}
