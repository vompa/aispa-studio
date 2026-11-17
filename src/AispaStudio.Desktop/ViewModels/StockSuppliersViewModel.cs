using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Lieferant in der Liste; ohne Kennung ist es der Eintrag „Neuer Lieferant“.</summary>
public sealed record StockSupplierRow(Guid? Id, string Name, string Sub)
{
    /// <summary>Der Eintrag „Neuer Lieferant“: die Liste zeigt ihn nicht, „Neu“ steht fest über der Liste (ADR 0099).</summary>
    public bool IsNew => Id is null;

    public string Summary => Sub.Length == 0 ? Name : $"{Name}, {Sub}";

    public override string ToString() => Summary;
}

/// <summary>Bestellweg in der Auswahl.</summary>
public sealed record SupplierChannelOption(SupplierOrderChannel Channel, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Lieferanten (R05 Slice 112, Ä251): links die Liste, rechts das Formular mit Kontakt, Lieferzeit, Mindestbestellwert und Bestellweg, nach dem Vorbild der Lagerorte. Löschen geht nur, wenn
/// kein Material mehr am Lieferanten hängt, sonst bleibt „Nicht mehr verwenden“ (ADR 0099; beides im Menü „⋯“). Nur mit ManageCatalog; geprüft wird in der API.
/// </summary>
public sealed partial class StockSuppliersViewModel : ViewModelBase, IFocusTarget, IEditableArea
{
    public const string NameField = "SupplierNameField";

    public const string EmailField = "SupplierEmailField";

    public const string LeadTimeField = "SupplierLeadTimeField";

    public const string MinimumOrderField = "SupplierMinimumOrderField";

    private readonly StockViewModel _owner;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private StockSupplierDto? _current;
    private string? _savedSignature;
    private Guid? _selectAfterLoad;
    private bool _applying;
    private bool _switching;
    private bool _reverting;

    internal StockSuppliersViewModel(StockViewModel owner)
    {
        _owner = owner;
        ChannelOptions = [.. Enum.GetValues<SupplierOrderChannel>().Select(c => new SupplierChannelOption(c, StockFormat.OrderChannel(c)))];
        SelectedChannel = ChannelOptions[0];
    }

    public string Title => "Lieferanten";

    public ObservableCollection<StockSupplierRow> Rows { get; } = [];

    public IReadOnlyList<SupplierChannelOption> ChannelOptions { get; }

    [ObservableProperty]
    public partial StockSupplierRow? SelectedRow { get; set; }

    [ObservableProperty]
    public partial string SupplierName { get; set; } = "";

    [ObservableProperty]
    public partial string ContactPerson { get; set; } = "";

    [ObservableProperty]
    public partial string Email { get; set; } = "";

    [ObservableProperty]
    public partial string Phone { get; set; } = "";

    [ObservableProperty]
    public partial string LeadTimeDays { get; set; } = "";

    [ObservableProperty]
    public partial string MinimumOrderValue { get; set; } = "";

    [ObservableProperty]
    public partial SupplierChannelOption? SelectedChannel { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? EmailError { get; private set; }

    [ObservableProperty]
    public partial string? LeadTimeError { get; private set; }

    [ObservableProperty]
    public partial string? MinimumOrderError { get; private set; }

    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    public string HeadingText => _current?.Name ?? "Neuer Lieferant";

    public bool CanRemove => _current is not null;

    /// <summary>„Nicht mehr verwenden“ gilt dem gespeicherten Stand; mit ungespeicherten Änderungen wäre unklar, was gemeint ist.</summary>
    public bool CanDeactivate => _current is { IsActive: true } && !HasUnsavedChanges;

    public bool HasUnsavedChanges => _savedSignature is not null && Signature() != _savedSignature;

    public bool CanSave => !IsSaving && SupplierName.Trim().Length > 0 && (_current is null || HasUnsavedChanges);

    /// <summary>Baut die Liste aus dem Stand des Besitzers neu auf; eine angefangene Eingabe bleibt stehen.</summary>
    internal void Rebuild()
    {
        var select = _selectAfterLoad ?? _current?.Id;
        var keepInput = _selectAfterLoad is null && HasUnsavedChanges;
        _selectAfterLoad = null;
        _switching = true;
        Rows.Clear();
        foreach (var supplier in _owner.Suppliers)
        {
            Rows.Add(new StockSupplierRow(supplier.Id, supplier.Name, Describe(supplier)));
        }

        Rows.Add(new StockSupplierRow(null, "Neuer Lieferant", ""));
        if (keepInput)
        {
            SelectedRow = Rows.FirstOrDefault(r => r.Id == select) ?? Rows[^1];
            _switching = false;
            return;
        }

        _switching = false;
        Select(select is { } id && _owner.Suppliers.Any(s => s.Id == id) ? id : null);
    }

    private static string Describe(StockSupplierDto supplier)
    {
        var parts = new List<string>();
        if (supplier.LeadTimeDays is { } days)
        {
            parts.Add(days == 1 ? "1 Tag Lieferzeit" : $"{days} Tage Lieferzeit");
        }

        parts.Add(StockFormat.OrderChannel(supplier.OrderChannel));
        if (!supplier.IsActive)
        {
            parts.Add("deaktiviert");
        }

        return string.Join(", ", parts);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving)
        {
            return false;
        }

        var name = SupplierName.Trim();
        NameError = name.Length == 0 ? "Der Name fehlt. Trage ihn ein."
            : _owner.Suppliers.Any(s => s.Id != _current?.Id && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) ? "Diesen Lieferanten gibt es schon. Wähle ihn in der Liste."
            : null;
        EmailError = Email.Trim().Length > 0 && !Email.Contains('@', StringComparison.Ordinal) ? "Die Mailadresse ist nicht gültig. Gib sie z. B. als bestellung@lieferant.example ein." : null;
        int? leadTime = null;
        LeadTimeError = null;
        if (LeadTimeDays.Trim().Length > 0)
        {
            leadTime = int.TryParse(LeadTimeDays.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var days) ? days : null;
            LeadTimeError = leadTime is null or > 365 ? "Die Lieferzeit ist keine ganze Zahl von 0 bis 365. Gib sie z. B. als 5 ein." : null;
        }

        var minimum = MinimumOrderValue.Trim().Length == 0 ? null : StockFormat.Parse(MinimumOrderValue);
        MinimumOrderError = MinimumOrderValue.Trim().Length > 0 && (minimum is null or < 0 || decimal.Round(minimum.Value, 2) != minimum)
            ? "Der Mindestbestellwert ist keine Zahl ab 0 mit höchstens zwei Nachkommastellen. Gib ihn z. B. als 150 ein."
            : null;
        if (NameError is not null || EmailError is not null || LeadTimeError is not null || MinimumOrderError is not null)
        {
            FocusTarget = NameError is not null ? NameField : EmailError is not null ? EmailField : LeadTimeError is not null ? LeadTimeField : MinimumOrderField;
            return false;
        }

        Error = null;
        IsSaving = true;
        var body = new StockSupplierInput(name, Blank(ContactPerson), Blank(Email), Blank(Phone), leadTime, minimum, SelectedChannel?.Channel ?? SupplierOrderChannel.Email, IsActive);
        var result = _current is { } supplier
            ? await _owner.Api.PutAsync<StockSupplierDto>($"api/stock/suppliers/{supplier.Id}", body).ConfigureAwait(true)
            : await _owner.Api.PostAsync<StockSupplierDto>("api/stock/suppliers", body).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Der Lieferant wurde nicht gespeichert.";
            return false;
        }

        _current = saved;
        _savedSignature = Signature();
        NotifyStateChanged();
        _selectAfterLoad = saved.Id;
        await _owner.ReloadAsync("Gespeichert.").ConfigureAwait(true);
        return true;
    }

    private static string? Blank(string value) => value.Trim().Length == 0 ? null : value.Trim();

    [RelayCommand]
    public void Discard()
    {
        Apply(_current);
        Error = null;
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(_owner.Dialogs, this).ConfigureAwait(true))
        {
            Select(null);
        }
    }

    /// <summary>„Löschen …“ im Menü: die API prüft, was am Lieferanten hängt; frei ist er nach der Bestätigung weg, sonst bleibt „Nicht mehr verwenden“ (ADR 0099).</summary>
    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task DeleteAsync()
    {
        if (_current is not { } supplier)
        {
            return;
        }

        var path = $"api/stock/suppliers/{supplier.Id}";
        var outcome = await DeletionDialog.AskAsync(_owner.Api, _owner.Dialogs, $"{path}/deletion-check", supplier.Name).ConfigureAwait(true);
        if (outcome.Error is not null)
        {
            Error = outcome.Error;
            return;
        }

        switch (outcome.Choice)
        {
            case DeletionChoice.Delete:
                var result = await _owner.Api.DeleteAsync<object>(path).ConfigureAwait(true);
                if (!result.Ok)
                {
                    Error = result.Error ?? "Der Lieferant wurde nicht gelöscht.";
                    return;
                }

                Error = null;
                _current = null;
                await _owner.ReloadAsync("Lieferant gelöscht.").ConfigureAwait(true);
                break;
            case DeletionChoice.Alternative:
                await DeactivateAsync().ConfigureAwait(true);
                break;
        }
    }

    /// <summary>Material und Bestellungen behalten den Lieferanten; neue Bestellungen und neues Material wählen ihn nicht mehr. Über „Aktiv“ ist er wieder da.</summary>
    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private async Task DeactivateAsync()
    {
        if (_current is not { } supplier)
        {
            return;
        }

        var body = new StockSupplierInput(supplier.Name, supplier.ContactPerson, supplier.Email, supplier.Phone, supplier.LeadTimeDays, supplier.MinimumOrderValue, supplier.OrderChannel, false);
        var result = await _owner.Api.PutAsync<StockSupplierDto>($"api/stock/suppliers/{supplier.Id}", body).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error ?? "Der Lieferant wurde nicht aus der Verwendung genommen.";
            return;
        }

        Error = null;
        _selectAfterLoad = supplier.Id;
        await _owner.ReloadAsync("Lieferant wird nicht mehr verwendet.").ConfigureAwait(true);
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    partial void OnSelectedRowChanged(StockSupplierRow? oldValue, StockSupplierRow? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt beim alten Lieferanten, bis die Antwort da ist.
            Revert(() => SelectedRow = oldValue);
            _ = LeaveToRowAsync(newValue.Id);
            return;
        }

        Select(newValue.Id);
    }

    private async Task LeaveToRowAsync(Guid? id)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(_owner.Dialogs, this).ConfigureAwait(true))
        {
            Select(id);
        }
    }

    private void Revert(Action restore)
    {
        void Restore()
        {
            _reverting = true;
            try
            {
                restore();
            }
            finally
            {
                _reverting = false;
            }
        }

        if (_ui is null)
        {
            Restore();
        }
        else
        {
            _ui.Post(_ => Restore(), null);
        }
    }

    private void Select(Guid? id)
    {
        _switching = true;
        SelectedRow = Rows.FirstOrDefault(r => r.Id == id) ?? Rows.LastOrDefault();
        _switching = false;
        Apply(id is { } known ? _owner.Suppliers.FirstOrDefault(s => s.Id == known) : null);
    }

    private void Apply(StockSupplierDto? supplier)
    {
        _applying = true;
        _current = supplier;
        SupplierName = supplier?.Name ?? "";
        ContactPerson = supplier?.ContactPerson ?? "";
        Email = supplier?.Email ?? "";
        Phone = supplier?.Phone ?? "";
        LeadTimeDays = supplier?.LeadTimeDays?.ToString(CultureInfo.InvariantCulture) ?? "";
        MinimumOrderValue = supplier?.MinimumOrderValue is { } minimum ? StockFormat.Plain(minimum) : "";
        SelectedChannel = ChannelOptions.First(c => c.Channel == (supplier?.OrderChannel ?? SupplierOrderChannel.Email));
        IsActive = supplier?.IsActive ?? true;
        NameError = EmailError = LeadTimeError = MinimumOrderError = null;
        _applying = false;
        _savedSignature = Signature();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HeadingText));
        OnPropertyChanged(nameof(CanRemove));
        DeleteCommand.NotifyCanExecuteChanged();
        Touch();
    }

    private void Touch()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanDeactivate));
        DeactivateCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        _owner.NotifyChanged();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applying || e.PropertyName is not (nameof(SupplierName) or nameof(ContactPerson) or nameof(Email) or nameof(Phone) or nameof(LeadTimeDays) or nameof(MinimumOrderValue)
            or nameof(SelectedChannel) or nameof(IsActive)))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(SupplierName):
                NameError = null;
                break;
            case nameof(Email):
                EmailError = null;
                break;
            case nameof(LeadTimeDays):
                LeadTimeError = null;
                break;
            case nameof(MinimumOrderValue):
                MinimumOrderError = null;
                break;
        }

        Touch();
    }

    private string Signature() => string.Join('\u001f', SupplierName, ContactPerson, Email, Phone, LeadTimeDays, MinimumOrderValue, SelectedChannel?.Channel, IsActive);
}
