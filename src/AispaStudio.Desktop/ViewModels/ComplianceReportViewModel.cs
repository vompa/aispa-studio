using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

public sealed record WorkingTimeRow(string Name, string Hours, string WorkDays, string VacationDays, string SickDays, string Warnings);

/// <summary>Reiter „Pflichtberichte“ nach web/src/reports/ComplianceView.tsx: Kassen mit ihren Pflichten, Gutscheine, Arbeitszeit. Exporte kommen unverändert von der API.</summary>
public sealed partial class ComplianceReportViewModel(IWorkstationApi api, TimeProvider time) : ReportTabViewModel(api, time)
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAmountsHint), nameof(Reminders), nameof(Vouchers), nameof(NoRegisters), nameof(WorkingTime))]
    public partial ComplianceReportDto? Report { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<RegisterComplianceRow> Registers { get; private set; } = [];

    public bool ShowAmountsHint => Report?.AmountsHidden == true;



    public IReadOnlyList<string> Reminders => Report?.Reminders ?? [];

    public bool NoRegisters => Report is { Registers.Count: 0 };

    public string Vouchers
    {
        get
        {
            if (Report is not { } report)
            {
                return "";
            }

            var liabilities = report.VoucherLiabilities is { } amount ? $" über {Money.Format(amount)} (Verbindlichkeit, aktueller Stand)" : "";
            var packages = report.OpenPackages > 0 ? $", {report.OpenPackages} offene Pakete mit {report.PackageUnitsLeft} Behandlungen" : "";
            return $"{report.OpenVouchers} offene Gutscheine{liabilities}{packages}.";
        }
    }

    public IReadOnlyList<WorkingTimeRow> WorkingTime => [.. (Report?.WorkingTime ?? []).Select(w => new WorkingTimeRow(
        w.Name, Number(w.PlannedHours, 1), Number(w.WorkDays), Number(w.VacationDays), Number(w.SickDays), string.Join(' ', w.Warnings)))];

    protected override async Task ReloadAsync()
    {
        if (Location is not { } location)
        {
            return;
        }

        var version = NextVersion();
        LoadError = null;
        var result = await Api.GetAsync<ComplianceReportDto>($"api/reports/locations/{location.Id}/compliance?date={Iso(Date)}").ConfigureAwait(true);
        if (version != Version)
        {
            return;
        }

        if (result.Value is not { } report)
        {
            LoadError = result.Error ?? "Die Pflichtberichte konnten nicht geladen werden.";
            Registers = [];
            Report = null;
            return;
        }

        Registers = [.. report.Registers.Select(r => new RegisterComplianceRow(r, !report.AmountsHidden, Day, Save, RequestTseAsync))];
        Report = report;
        IsLoaded = true;
        foreach (var register in Registers.Where(r => r.ShowTse))
        {
            _ = LoadTseExportsAsync(register, version);
        }
    }

    private Task Save(string path, string name, string type, string pattern) => SaveFileAsync(path, name, type, pattern);

    private async Task RequestTseAsync(RegisterComplianceRow register)
    {
        Error = Message = null;
        var input = new TseExportInput(new DateOnly(Date.Year, 1, 1), Date);
        var result = await Api.PostAsync<TseExportDto>($"api/pos/registers/{register.RegisterId}/tse-exports", input).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error ?? "TSE-Export konnte nicht angefordert werden.";
            return;
        }

        Message = "TSE-Export angefordert. Er steht gleich in der Liste.";
        _ = LoadTseExportsAsync(register, Version);
    }

    /// <summary>Eine Cloud-TSE braucht länger: Solange ein Export läuft, fragt der Reiter nach, bis er verlassen wird.</summary>
    private async Task LoadTseExportsAsync(RegisterComplianceRow register, int version)
    {
        while (version == Version)
        {
            var result = await Api.GetAsync<List<TseExportDto>>($"api/pos/registers/{register.RegisterId}/tse-exports").ConfigureAwait(true);
            if (version != Version)
            {
                return;
            }

            var exports = result.Value ?? [];
            register.SetExports(exports);
            if (!exports.Exists(e => e.Status == TseExportStatus.Pending))
            {
                return;
            }

            await Task.Delay(PollInterval, Time).ConfigureAwait(true);
        }
    }
}

/// <summary>Eine Kasse in den Pflichtberichten.</summary>
public sealed partial class RegisterComplianceRow : ViewModelBase
{
    private readonly RegisterCompliance _register;
    private readonly Func<string, string, string, string, Task> _save;
    private readonly Func<RegisterComplianceRow, Task> _requestTse;

    public RegisterComplianceRow(RegisterCompliance register, bool showExports, Func<DateTimeOffset, string> day, Func<string, string, string, string, Task> save, Func<RegisterComplianceRow, Task> requestTse)
    {
        (_register, _save, _requestTse) = (register, save, requestTse);
        Title = $"Kassa {register.Code} ({register.Country}){(register.IsActive ? "" : ", außer Betrieb")}";
        Details = string.Join(" · ", new[]
        {
            register.CertificateSerial is { } serial ? $"Zertifikat {serial}" : null,
            register.StartedAt is { } started ? $"in Betrieb seit {day(started)}" : null,
            register.LastMonthlyReceipt is { } monthly ? $"letzter Monatsbeleg {day(monthly)}" : null,
            register.LastYearlyReceipt is { } yearly ? $"letzter Jahresbeleg {day(yearly)}" : null,
        }.Where(part => part is not null));
        ShowExport = showExports && register.ExportPath is not null;
        ExportText = register.Country == FiscalCountry.DE ? "DSFinV-K seit Jahresbeginn herunterladen" : "Datenerfassungsprotokoll (DEP) herunterladen";
        ShowTse = showExports && register.Country == FiscalCountry.DE;
    }

    public Guid RegisterId => _register.RegisterId;

    public string Title { get; }

    public string Details { get; }

    public IReadOnlyList<string> Duties => _register.Duties;

    public bool ShowExport { get; }

    public string ExportText { get; }

    public bool ShowTse { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestTseCommand))]
    public partial IReadOnlyList<TseExportRow> TseExports { get; private set; } = [];

    public void SetExports(IReadOnlyList<TseExportDto> exports) => TseExports = [.. exports.Select(e => new TseExportRow(e, _save))];

    [RelayCommand]
    private Task ExportAsync()
    {
        var isGerman = _register.Country == FiscalCountry.DE;
        return _save(_register.ExportPath!.TrimStart('/'), isGerman ? $"dsfinv-k-{_register.Code}.zip" : $"dep-{_register.Code}.json", isGerman ? "ZIP-Archiv" : "JSON-Datei", isGerman ? "*.zip" : "*.json");
    }

    [RelayCommand(CanExecute = nameof(CanRequestTse))]
    private Task RequestTseAsync() => _requestTse(this);

    private bool CanRequestTse() => !TseExports.Any(e => e.IsPending);
}

/// <summary>Ein angeforderter TSE-Export mit Stand und, wenn fertig, seiner Datei.</summary>
public sealed partial class TseExportRow(TseExportDto export, Func<string, string, string, string, Task> save) : ViewModelBase
{
    public string Text { get; } = $"{StockFormat.Date(export.From)} bis {StockFormat.Date(export.Until)}: {StatusText(export.Status)}{(export.Error is { } error ? $" ({error})" : "")}";

    public bool IsPending => export.Status == TseExportStatus.Pending;

    public bool IsDone => export.Status == TseExportStatus.Done;

    public string SaveText { get; } = $"{export.FileName} herunterladen";

    [RelayCommand]
    private Task SaveAsync() => save($"api/pos/tse-exports/{export.Id}/file", export.FileName ?? "tse-export.tar", "TSE-Export", "*.tar");

    private static string StatusText(TseExportStatus status) => status switch
    {
        TseExportStatus.Pending => "läuft",
        TseExportStatus.Done => "fertig",
        _ => "fehlgeschlagen",
    };
}
