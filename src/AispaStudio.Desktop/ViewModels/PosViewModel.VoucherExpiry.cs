using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Ablaufdatum beim Gutscheinverkauf: Vorbelegung, Regel und Hinweis rechnet der Server je Land der Kasse ----------
public partial class PosViewModel
{
    /// <summary>„Einzulösen bis“ für Wert-, Behandlungsgutschein und Paket; vom Server vorbelegt, änderbar, leer heißt unbefristet.</summary>
    [ObservableProperty]
    public partial string VoucherValidUntil { get; set; } = "";

    /// <summary>Erklärung der Vorbelegung (Frist im Land der Kasse).</summary>
    [ObservableProperty]
    public partial string? VoucherExpiryRule { get; private set; }

    /// <summary>Hinweis zum eingetragenen Datum (kürzer als die übliche Frist, in Österreich bei jedem Datum); der Verkauf bleibt möglich.</summary>
    [ObservableProperty]
    public partial string? VoucherExpiryWarning { get; private set; }

    /// <summary>
    /// Die Vorbelegung kam vom Server. Nur dann heißt ein leeres Feld „bewusst unbefristet“; sonst schickt die Kassa kein Datum,
    /// und der Server setzt die Frist selbst (Netzfehler, Klick vor der Antwort).
    /// </summary>
    private bool _voucherExpiryLoaded;

    /// <summary>Vorbelegung, wie sie im Feld stand; bleibt das Feld so, rechnet der Server die Frist selbst.</summary>
    private string _prefilledValidUntil = "";

    private bool _fillingVoucherExpiry;

    private async Task LoadVoucherExpiryAsync(CashRegisterDto? register)
    {
        _voucherExpiryLoaded = false;
        VoucherExpiryRule = null;
        VoucherExpiryWarning = null;
        FillVoucherValidUntil("");
        if (register is null)
        {
            return;
        }

        var result = await _api.GetAsync<VoucherExpiryDto>($"api/pos/registers/{register.Id}/voucher-expiry").ConfigureAwait(true);
        if (register.Id != Register?.Id || result.Value is not { } expiry)
        {
            return;
        }

        _prefilledValidUntil = expiry.Default is { } date ? DateInputParser.Format(date) : "";
        FillVoucherValidUntil(_prefilledValidUntil);
        VoucherExpiryRule = expiry.Rule;
        _voucherExpiryLoaded = true;
    }

    /// <summary>
    /// Nach dem Abschluss oder Wechsel einer Rechnung die Vorbelegung neu holen (Kaufdatum ist heute, auch über den Jahreswechsel).
    /// Entsteht eine Rechnung erst mit dem ersten Verkauf, bleibt das Feld, wie es ist.
    /// </summary>
    partial void OnBillChanged(BillDto? oldValue, BillDto? newValue)
    {
        if (oldValue is not null && oldValue.Id != newValue?.Id)
        {
            _ = LoadVoucherExpiryAsync(Register);
        }
    }

    private void FillVoucherValidUntil(string text)
    {
        _fillingVoucherExpiry = true;
        VoucherValidUntil = text;
        _fillingVoucherExpiry = false;
    }

    partial void OnVoucherValidUntilChanged(string value)
    {
        if (!_fillingVoucherExpiry)
        {
            _ = LoadVoucherExpiryWarningAsync(value);
        }
    }

    private async Task LoadVoucherExpiryWarningAsync(string text)
    {
        if (Register is not { } register || DateInputParser.TryParse(text, Today) is not { } until)
        {
            VoucherExpiryWarning = null;
            return;
        }

        var result = await _api.GetAsync<VoucherExpiryDto>($"api/pos/registers/{register.Id}/voucher-expiry?until={until:yyyy-MM-dd}").ConfigureAwait(true);
        if (text == VoucherValidUntil)
        {
            VoucherExpiryWarning = result.Value?.Warning;
        }
    }

    /// <summary>Gutscheinposition mit dem Datum aus „Einzulösen bis“; ein unlesbares Datum setzt <see cref="Error"/> und liefert null.</summary>
    private BillLineInput? WithVoucherExpiry(BillLineInput line)
    {
        var parsed = DateInputParser.Parse(VoucherValidUntil, Today);
        if (parsed.Error is { } error)
        {
            Error = error;
            return null;
        }

        // Unveränderte Vorbelegung: nichts mitschicken, der Server rechnet die Frist am Verkaufstag (auch nach dem Jahreswechsel richtig).
        if (_voucherExpiryLoaded && VoucherValidUntil == _prefilledValidUntil)
        {
            return line;
        }

        return parsed.Value is { } date
            ? line with { ValidUntil = date }
            : line with { WithoutExpiry = _voucherExpiryLoaded };
    }
}
