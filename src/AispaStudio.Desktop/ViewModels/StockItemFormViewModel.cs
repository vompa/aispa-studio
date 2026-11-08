using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Verkaufsprodukt der Kassa in der Auswahl; ohne Kennung steht es für „keines“.</summary>
public sealed record ArticleChoice(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Ein Lieferant in der Auswahl des Materials; ohne Kennung steht er für „kein Lieferant“.</summary>
public sealed record SupplierChoice(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// „Material anlegen“ und „Material ändern“ (R05 Slice 101, Slice 112) nach <c>StockItemForm.tsx</c>: Material oder Verkaufsprodukt, gilt für alle Standorte; den Bestand führt das Lager je Lagerort.
/// Pflicht sind Bezeichnung und Einheit; dazu Lieferant aus dem Stamm, Packung, Gebinde, Richtwert je Behandlung und Ablaufwarnung. Ob die Angaben zusammenpassen, prüft die API. Nur mit dem Recht auf
/// den Katalog (ManageCatalog).
/// </summary>
public sealed partial class StockItemFormViewModel : ViewModelBase, IFocusTarget, IEditableArea
{
    public const string NameField = "ItemNameField";

    public const string UnitField = "ItemUnitField";

    public const string UnitCostField = "ItemUnitCostField";

    public const string MinimumField = "ItemMinimumField";

    public const string PackUnitField = "ItemPackUnitField";

    public const string PackSizeField = "ItemPackSizeField";

    public const string CaseUnitField = "ItemCaseUnitField";

    public const string PacksPerCaseField = "ItemPacksPerCaseField";

    public const string DoseField = "ItemDoseField";

    public const string WarningField = "ItemWarningField";

    private const string DefaultUnit = "Stück";

    private const int DefaultWarningDays = 30;

    private static readonly ArticleChoice NoArticle = new(null, "keines");

    private static readonly SupplierChoice NoSupplier = new(null, "kein Lieferant");

    private static readonly string[] Inputs =
    [
        nameof(ItemName), nameof(Unit), nameof(UnitCost), nameof(MinimumQuantity), nameof(SelectedArticle), nameof(SelectedSupplier), nameof(SupplierArticleNumber), nameof(PackUnit),
        nameof(PackSize), nameof(CaseUnit), nameof(PacksPerCase), nameof(TreatmentDose), nameof(ExpiryWarningDays), nameof(Barcode),
    ];

    private readonly StockViewModel _owner;
    private readonly IWorkstationApi _api;
    private List<ArticleDto> _articles = [];
    private StockItemDto? _editing;
    private string _editingCostText = "";
    private string _savedSignature;

    internal StockItemFormViewModel(StockViewModel owner, IWorkstationApi api)
    {
        _owner = owner;
        _api = api;
        Articles = [NoArticle];
        Suppliers = [NoSupplier];
        SelectedArticle = NoArticle;
        SelectedSupplier = NoSupplier;
        _savedSignature = Signature();
    }

    public string Title => "Material";

    public bool HasUnsavedChanges => IsDirty;

    public Task<bool> SaveChangesAsync() => CreateItemAsync();

    public void Discard() => Clear();

    public ObservableCollection<ArticleChoice> Articles { get; }

    public ObservableCollection<SupplierChoice> Suppliers { get; }

    [ObservableProperty]
    public partial string ItemName { get; set; } = "";

    [ObservableProperty]
    public partial string Unit { get; set; } = DefaultUnit;

    /// <summary>Einstandspreis netto je Einheit; leer zählt als 0.</summary>
    [ObservableProperty]
    public partial string UnitCost { get; set; } = "";

    /// <summary>Mindestbestand je Standort; leer zählt als 0.</summary>
    [ObservableProperty]
    public partial string MinimumQuantity { get; set; } = "";

    [ObservableProperty]
    public partial ArticleChoice? SelectedArticle { get; set; }

    [ObservableProperty]
    public partial SupplierChoice? SelectedSupplier { get; set; }

    [ObservableProperty]
    public partial string SupplierArticleNumber { get; set; } = "";

    /// <summary>Code auf der Verpackung; ein Handscanner im Lager oder an der Kassa findet das Material damit.</summary>
    [ObservableProperty]
    public partial string Barcode { get; set; } = "";

    /// <summary>Name der Packung, z. B. Flasche.</summary>
    [ObservableProperty]
    public partial string PackUnit { get; set; } = "";

    /// <summary>Menge je Packung in der Einheit des Materials, z. B. 500.</summary>
    [ObservableProperty]
    public partial string PackSize { get; set; } = "";

    /// <summary>Name des Gebindes, z. B. Karton.</summary>
    [ObservableProperty]
    public partial string CaseUnit { get; set; } = "";

    /// <summary>Packungen je Gebinde, z. B. 4.</summary>
    [ObservableProperty]
    public partial string PacksPerCase { get; set; } = "";

    /// <summary>Richtwert je Behandlung in der Einheit des Materials, z. B. 15; bucht nichts ab.</summary>
    [ObservableProperty]
    public partial string TreatmentDose { get; set; } = "";

    /// <summary>Tage vor dem Ablauf, ab denen eine Charge in den Aufgaben steht; leer zählt als 30.</summary>
    [ObservableProperty]
    public partial string ExpiryWarningDays { get; set; } = "30";

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? UnitError { get; private set; }

    [ObservableProperty]
    public partial string? UnitCostError { get; private set; }

    [ObservableProperty]
    public partial string? MinimumError { get; private set; }

    [ObservableProperty]
    public partial string? PackUnitError { get; private set; }

    [ObservableProperty]
    public partial string? PackSizeError { get; private set; }

    [ObservableProperty]
    public partial string? CaseUnitError { get; private set; }

    [ObservableProperty]
    public partial string? PacksPerCaseError { get; private set; }

    [ObservableProperty]
    public partial string? DoseError { get; private set; }

    [ObservableProperty]
    public partial string? WarningError { get; private set; }

    /// <summary>Das Feld, das nach einem Fehler den Fokus bekommen soll; die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool IsSaving { get; private set; }

    public bool IsEditing => _editing is not null;

    public string HeadingText => IsEditing ? "Material ändern" : "Material anlegen";

    public string ButtonText => IsEditing ? "Speichern" : "Material anlegen";

    public string ErrorTitle => IsEditing ? "Nicht gespeichert" : "Nicht angelegt";

    /// <summary>Eine Eingabe weicht vom leeren Formular oder vom gewählten Material ab.</summary>
    public bool IsDirty => Signature() != _savedSignature;

    /// <summary>Wie im Web: ohne Bezeichnung und Einheit ist der Knopf aus; beim Ändern auch ohne Änderung.</summary>
    public bool CanCreate => !IsSaving && ItemName.Trim().Length > 0 && Unit.Trim().Length > 0 && (!IsEditing || IsDirty);

    internal void SetArticles(IEnumerable<ArticleDto> articles)
    {
        var selected = SelectedArticle;
        _articles = [.. articles];
        Articles.Clear();
        Articles.Add(NoArticle);
        foreach (var article in _articles.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Articles.Add(new ArticleChoice(article.Id, article.Name));
        }

        SelectedArticle = Keep(Articles, a => a.Id == selected?.Id, selected) ?? NoArticle;
    }

    /// <summary>Die aktiven Lieferanten; ein schon gewählter, inzwischen deaktivierter bleibt in der Auswahl.</summary>
    internal void SetSuppliers(IEnumerable<StockSupplierDto> suppliers)
    {
        var selected = SelectedSupplier;
        Suppliers.Clear();
        Suppliers.Add(NoSupplier);
        foreach (var supplier in suppliers.Where(s => s.IsActive).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Suppliers.Add(new SupplierChoice(supplier.Id, supplier.Name));
        }

        SelectedSupplier = Keep(Suppliers, s => s.Id == selected?.Id, selected) ?? NoSupplier;
    }

    /// <summary>Der passende Eintrag der Liste; fehlt er, wird der bisherige ergänzt, damit eine Verknüpfung nicht verloren geht.</summary>
    private static T? Keep<T>(ObservableCollection<T> list, Func<T, bool> same, T? previous)
        where T : class
    {
        if (list.FirstOrDefault(same) is { } found)
        {
            return found;
        }

        if (previous is not null)
        {
            list.Add(previous);
        }

        return previous;
    }

    /// <summary>Lädt ein Material zum Ändern ins Formular.</summary>
    internal void Edit(StockItemDto item)
    {
        _editing = item;
        ItemName = item.Name;
        Unit = item.Unit;
        UnitCost = StockFormat.Cost(item.UnitCost);
        _editingCostText = UnitCost;
        MinimumQuantity = StockFormat.Plain(item.MinimumQuantity);
        SelectedArticle = Keep(Articles, a => a.Id == item.ArticleId, item.ArticleId is { } article ? new ArticleChoice(article, "Verknüpftes Produkt (deaktiviert)") : NoArticle);
        SelectedSupplier = Keep(Suppliers, s => s.Id == item.SupplierId, item.SupplierId is { } supplier ? new SupplierChoice(supplier, $"{item.Supplier} (deaktiviert)") : NoSupplier);
        SupplierArticleNumber = item.SupplierArticleNumber ?? "";
        Barcode = item.Barcode ?? "";
        PackUnit = item.PackUnit ?? "";
        PackSize = item.PackSize is { } size ? StockFormat.Plain(size) : "";
        CaseUnit = item.CaseUnit ?? "";
        PacksPerCase = item.PacksPerCase?.ToString(CultureInfo.InvariantCulture) ?? "";
        TreatmentDose = item.TreatmentDose is { } dose ? StockFormat.Plain(dose) : "";
        ExpiryWarningDays = item.ExpiryWarningDays.ToString(CultureInfo.InvariantCulture);
        ResetState();
        FocusTarget = NameField;
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync() => await CreateItemAsync().ConfigureAwait(true);

    /// <summary>Legt das Material an oder speichert das gewählte; false bei einer ungültigen Eingabe oder einem Fehler der API (die Eingabe bleibt dann stehen).</summary>
    public async Task<bool> CreateItemAsync()
    {
        if (IsSaving)
        {
            return false;
        }

        NameError = ItemName.Trim().Length == 0 ? "Die Bezeichnung fehlt. Trage sie ein." : null;
        UnitError = Unit.Trim().Length == 0 ? "Die Einheit fehlt. Trage sie ein, z. B. ml oder Stück." : null;
        var cost = StockFormat.ParseOrZero(UnitCost);
        var minimum = StockFormat.ParseOrZero(MinimumQuantity);
        UnitCostError = cost is null or < 0 ? "Der Preis ist keine Zahl ab 0. Gib ihn z. B. als 12,50 ein." : null;
        MinimumError = minimum is null or < 0 ? "Der Mindestbestand ist keine Zahl ab 0. Gib ihn z. B. als 5 ein." : null;
        var packSize = StockFormat.Parse(PackSize);
        var packsPerCase = Whole(PacksPerCase);
        CheckPackaging(packSize, packsPerCase);
        var dose = StockFormat.Parse(TreatmentDose);
        DoseError = TreatmentDose.Trim().Length > 0 && dose is null or <= 0 ? "Die Menge je Behandlung ist keine Zahl über 0. Gib sie z. B. als 15 ein." : null;
        var warning = ExpiryWarningDays.Trim().Length == 0 ? DefaultWarningDays : Whole(ExpiryWarningDays);
        WarningError = warning is null or < 1 or > 365 ? "Die Ablaufwarnung ist keine ganze Zahl von 1 bis 365. Gib sie z. B. als 30 ein." : null;
        if (FirstInvalidField() is { } invalid)
        {
            FocusTarget = invalid;
            return false;
        }

        Error = null;
        IsSaving = true;
        var editing = _editing;
        // Unverändert heißt: Der Preis des Lagers gilt, auch wenn ihn ein Wareneingang inzwischen geändert hat.
        var unitCost = editing is not null && UnitCost == _editingCostText ? _owner.Items.FirstOrDefault(i => i.Id == editing.Id)?.UnitCost ?? editing.UnitCost : cost!.Value;
        var body = new StockItemInput(ItemName.Trim(), Unit.Trim(), unitCost, minimum!.Value, SelectedArticle?.Id, editing?.IsActive ?? true, null, Blank(SupplierArticleNumber),
            SelectedSupplier?.Id, Blank(PackUnit), packSize, Blank(CaseUnit), packsPerCase, dose, warning!.Value, Blank(Barcode));
        var result = editing is null
            ? await _api.PostAsync<StockItemDto>("api/stock/items", body).ConfigureAwait(true)
            : await _api.PutAsync<StockItemDto>($"api/stock/items/{editing.Id}", body).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? (editing is null ? "Das Material wurde nicht angelegt." : "Das Material wurde nicht gespeichert.");
            return false;
        }

        Clear();
        await _owner.ReloadAsync(editing is null ? $"{saved.Name} angelegt." : $"{saved.Name} gespeichert.").ConfigureAwait(true);
        return true;
    }

    private string? FirstInvalidField() =>
        NameError is not null ? NameField
        : UnitError is not null ? UnitField
        : UnitCostError is not null ? UnitCostField
        : MinimumError is not null ? MinimumField
        : PackUnitError is not null ? PackUnitField
        : PackSizeError is not null ? PackSizeField
        : CaseUnitError is not null ? CaseUnitField
        : PacksPerCaseError is not null ? PacksPerCaseField
        : DoseError is not null ? DoseField
        : WarningError is not null ? WarningField
        : null;

    /// <summary>Name und Menge gehören zusammen, ein Gebinde braucht eine Packung; die gleichen Regeln wie in der API, damit der Fehler am Feld steht.</summary>
    private void CheckPackaging(decimal? packSize, int? packsPerCase)
    {
        var hasPack = PackUnit.Trim().Length > 0;
        var hasSize = PackSize.Trim().Length > 0;
        var hasCase = CaseUnit.Trim().Length > 0;
        var hasCount = PacksPerCase.Trim().Length > 0;
        PackUnitError = !hasPack && (hasSize || hasCase) ? "Der Name der Packung fehlt. Trage ihn ein, z. B. Flasche." : null;
        PackSizeError = hasSize && packSize is null or <= 0 ? "Die Menge je Packung ist keine Zahl über 0. Gib sie z. B. als 500 ein."
            : hasPack && !hasSize ? "Die Menge je Packung fehlt. Trage sie ein, z. B. 500."
            : null;
        CaseUnitError = !hasCase && hasCount ? "Der Name des Gebindes fehlt. Trage ihn ein, z. B. Karton." : null;
        PacksPerCaseError = hasCount && packsPerCase is null or < 1 or > 10_000 ? "Die Packungen je Gebinde sind keine ganze Zahl von 1 bis 10000. Gib sie z. B. als 4 ein."
            : hasCase && !hasCount ? "Die Packungen je Gebinde fehlen. Trage sie ein, z. B. 4."
            : null;
    }

    private static int? Whole(string text) => int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string? Blank(string value) => value.Trim().Length == 0 ? null : value.Trim();

    /// <summary>Zurück auf das leere Formular.</summary>
    [RelayCommand]
    public void Clear()
    {
        _editing = null;
        ItemName = "";
        Unit = DefaultUnit;
        UnitCost = "";
        MinimumQuantity = "";
        SelectedArticle = NoArticle;
        SelectedSupplier = NoSupplier;
        SupplierArticleNumber = "";
        Barcode = "";
        PackUnit = "";
        PackSize = "";
        CaseUnit = "";
        PacksPerCase = "";
        TreatmentDose = "";
        ExpiryWarningDays = DefaultWarningDays.ToString(CultureInfo.InvariantCulture);
        // Ein beim Ändern ergänzter deaktivierter Eintrag verschwindet wieder aus den Listen.
        SetArticles(_articles);
        SetSuppliers(_owner.Suppliers);
        ResetState();
    }

    /// <summary>Der eben gesetzte Stand gilt als Ausgangsstand; Fehler und Anzeige folgen.</summary>
    private void ResetState()
    {
        _savedSignature = Signature();
        NameError = UnitError = UnitCostError = MinimumError = PackUnitError = PackSizeError = CaseUnitError = PacksPerCaseError = DoseError = WarningError = null;
        Error = null;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(HeadingText));
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(ErrorTitle));
        Touch();
    }

    private void Touch()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanCreate));
        CreateCommand.NotifyCanExecuteChanged();
        _owner.NotifyChanged();
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        switch (name)
        {
            case nameof(ItemName):
                NameError = null;
                break;
            case nameof(Unit):
                UnitError = null;
                break;
            case nameof(UnitCost):
                UnitCostError = null;
                break;
            case nameof(MinimumQuantity):
                MinimumError = null;
                break;
            case nameof(PackUnit):
                PackUnitError = null;
                break;
            case nameof(CaseUnit):
                CaseUnitError = null;
                break;
            case nameof(PackSize):
                PackSizeError = null;
                break;
            case nameof(PacksPerCase):
                PacksPerCaseError = null;
                break;
            case nameof(TreatmentDose):
                DoseError = null;
                break;
            case nameof(ExpiryWarningDays):
                WarningError = null;
                break;
        }

        Touch();
    }

    private string Signature() => string.Join('\u001f', ItemName, Unit, UnitCost, MinimumQuantity, SelectedArticle?.Id, SelectedSupplier?.Id, SupplierArticleNumber, PackUnit, PackSize, CaseUnit,
        PacksPerCase, TreatmentDose, ExpiryWarningDays, Barcode);
}
