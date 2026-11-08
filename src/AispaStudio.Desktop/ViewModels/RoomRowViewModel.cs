using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Kassa-Artikel in der Auswahl „Eintritt“ eines Bereichs: ohne Id heißt „Nicht an der Kassa“.</summary>
public sealed record ArticleOption(Guid? Id, string Title)
{
    public static ArticleOption None { get; } = new(null, "Nicht an der Kassa");

    /// <summary>„Eintritt Sauna (12,00 €)“ wie in der Web-Maske.</summary>
    public static ArticleOption From(ArticleDto article) =>
        new(article.Id, $"{article.Name} ({article.Price.ToString("C", CultureInfo.GetCultureInfo("de-AT"))})");

    public override string ToString() => Title;
}

/// <summary>
/// Eine Zeile der Raumtabelle (Standorte &amp; Räume, R05 Slice 95) wie <c>RoomRow</c> in <c>LocationsTab.tsx</c>: Name, Raumpflege, Plätze, Bereich und Eintritts-Artikel.
/// Die Zeile speichert für sich (Knopf „Speichern“, bei der neuen Zeile „Hinzufügen“); er erscheint erst mit einer Änderung und einem Namen. Die letzte Zeile ist immer die
/// neue, ohne Id und ohne „Deaktivieren“.
/// </summary>
public sealed partial class RoomRowViewModel : ObservableObject
{
    private readonly Func<RoomRowViewModel, Task> _save;
    private readonly Func<RoomRowViewModel, Task>? _deactivate;
    private RoomDto? _saved;
    private IReadOnlyList<ArticleOption> _articles;

    public RoomRowViewModel(RoomDto? room, IReadOnlyList<ArticleOption> articles, Func<RoomRowViewModel, Task> save, Func<RoomRowViewModel, Task>? deactivate)
    {
        _articles = articles;
        _save = save;
        _deactivate = deactivate;
        Articles = articles;
        Load(room);
    }

    /// <summary>Die Auswahl der Eintritts-Artikel, mit „Nicht an der Kassa“ vorn.</summary>
    public IReadOnlyList<ArticleOption> Articles { get; private set; }

    public Guid? Id => _saved?.Id;

    public bool IsNew => _saved is null;

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    /// <summary>Raumpflege nach jedem Termin in Minuten (0 bis 240).</summary>
    [ObservableProperty]
    public partial decimal? CleaningMinutes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacesMaximum))]
    public partial decimal? Places { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacesMaximum))]
    public partial bool IsArea { get; set; }

    [ObservableProperty]
    public partial ArticleOption Admission { get; set; } = ArticleOption.None;

    /// <summary>Ein Raum nimmt höchstens 8 Gäste einer Buchung auf, ein Bereich bis zu 200 Eintritte.</summary>
    public decimal PlacesMaximum => IsArea ? 200 : 8;

    /// <summary>Beispiel im Namensfeld der neuen Zeile.</summary>
    public string? Placeholder => IsNew ? "Neuer Raum" : null;

    /// <summary>Die Beschriftung des Knopfes: „Hinzufügen“ in der neuen Zeile, sonst „Speichern“.</summary>
    public string SaveText => IsNew ? "Hinzufügen" : "Speichern";

    /// <summary>Name für den Screenreader an Knopf und Zeile, damit mehrere „Speichern“ unterscheidbar sind.</summary>
    public string SaveName => IsNew ? "Neuen Raum hinzufügen" : $"Raum {_saved!.Name} speichern";

    public string DeactivateName => $"Raum {_saved?.Name} entfernen";

    /// <summary>Etwas an der Zeile ist geändert (die neue Zeile gilt immer als geändert, wie im Web).</summary>
    public bool IsDirty => _saved is null || Name != _saved.Name || Minutes(CleaningMinutes) != _saved.CleaningMinutes || Minutes(Places) != _saved.Places
        || IsArea != _saved.IsArea || (IsArea && Admission.Id != _saved.AdmissionArticleId);

    /// <summary>Der Knopf erscheint, sobald etwas geändert ist und der Raum einen Namen hat.</summary>
    public bool CanSave => IsDirty && Name.Trim().Length > 0;

    /// <summary>Eine geänderte, noch nicht gespeicherte Zeile eines bestehenden Raums; sie zeigt den Hinweis „Nicht gespeichert“.</summary>
    public bool HasPendingChanges => !IsNew && IsDirty;

    public bool CanDeactivate => _deactivate is not null && !IsNew;

    public RoomInput Input() => new(Name.Trim(), Minutes(CleaningMinutes), Minutes(Places), IsArea, IsArea ? Admission.Id : null);

    /// <summary>Stellt die Zeile aus dem gespeicherten Raum her; eine bearbeitete Zeile behält ihre Eingabe und vergleicht fortan mit dem neuen Stand.</summary>
    public void Rebase(RoomDto room)
    {
        var edited = HasPendingChanges;
        _saved = room;
        if (!edited)
        {
            Load(room);
        }

        Refresh();
    }

    /// <summary>Die Artikel haben sich geladen; die Auswahl bleibt, auch wenn der Artikel erst jetzt in der Liste steht.</summary>
    public void SetArticles(IReadOnlyList<ArticleOption> articles)
    {
        _articles = articles;
        Articles = articles;
        Admission = articles.FirstOrDefault(a => a.Id == Admission.Id) ?? ArticleOption.None;
        OnPropertyChanged(nameof(Articles));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => _save(this);

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private Task DeactivateAsync() => _deactivate!(this);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Name) or nameof(CleaningMinutes) or nameof(Places) or nameof(IsArea) or nameof(Admission))
        {
            Refresh();
        }
    }

    private void Load(RoomDto? room)
    {
        _saved = room;
        Name = room?.Name ?? "";
        CleaningMinutes = room?.CleaningMinutes ?? 10;
        Places = room?.Places ?? 1;
        IsArea = room?.IsArea ?? false;
        Admission = _articles.FirstOrDefault(a => a.Id == room?.AdmissionArticleId) ?? ArticleOption.None;
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(SaveName));
        OnPropertyChanged(nameof(DeactivateName));
        SaveCommand.NotifyCanExecuteChanged();
        DeactivateCommand.NotifyCanExecuteChanged();
    }

    private static int Minutes(decimal? value) => (int)(value ?? 0);
}
