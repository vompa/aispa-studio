using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Gemeinsames der Berichtsreiter (R05 Slice 102): Standort und Tag kommen von <see cref="ReportsViewModel"/>, jeder Reiter lädt selbst. <see cref="Version"/> verwirft Antworten
/// und beendet das Nachfragen, sobald der Reiter neu geladen oder verlassen wurde. Alle Zahlen kommen fertig von der API; hier wird nur formatiert.
/// </summary>
public abstract partial class ReportTabViewModel(IWorkstationApi api, TimeProvider time) : ViewModelBase, ISavesFiles
{
    /// <summary>Abstand beim Nachfragen (KI-Zusammenfassung, TSE-Export), wie im Web.</summary>
    protected static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public IFileSaver? Files { get; set; }

    protected IWorkstationApi Api { get; } = api;

    protected TimeProvider Time { get; } = time;

    protected LocationDto? Location { get; private set; }

    protected DateOnly Date { get; private set; }

    protected int Version { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; set; }

    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    /// <summary>Statusband oben (R07 Slice 144): ein Satz, bei offenen Punkten mit den Punkten darunter.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIsWarning))]
    public partial bool StatusIsOk { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusPoints))]
    public partial IReadOnlyList<string> StatusPoints { get; private set; } = [];

    /// <summary>Die Wertezeile unter dem Status: kleine Bezeichnung, großer Wert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValues))]
    public partial IReadOnlyList<ReportValue> Values { get; private set; } = [];

    public bool StatusIsWarning => !StatusIsOk;

    public bool HasStatusPoints => StatusPoints.Count > 0;

    public bool HasValues => Values.Count > 0;

    public bool HasLoadError => LoadError is not null;

    public bool HasMessage => Message is not null;

    public bool HasError => Error is not null;

    public Task ShowAsync(LocationDto location, DateOnly date)
    {
        (Location, Date) = (location, date);
        return ReloadAsync();
    }

    /// <summary>Der Reiter ist nicht mehr zu sehen: späte Antworten und das Nachfragen enden.</summary>
    public void Leave() => Version++;

    protected abstract Task ReloadAsync();

    protected int NextVersion() => ++Version;

    /// <summary>Setzt Statusband und Wertezeile; <paramref name="ok"/> zeigt das ruhige Band, sonst das Warnband mit den <paramref name="points"/> darunter.</summary>
    protected void SetOverview(string status, bool ok, IReadOnlyList<string> points, IReadOnlyList<ReportValue> values)
    {
        (StatusText, StatusIsOk, StatusPoints, Values) = (status, ok, points, values);
    }

    /// <summary>Zurück auf leer, wenn der Reiter nichts laden konnte: keine alten Zahlen unter dem neuen Stand.</summary>
    protected void ClearOverview() => SetOverview("", false, [], []);

    protected static string Plural(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";

    [RelayCommand]
    private Task RetryAsync() => ReloadAsync();

    [RelayCommand]
    private void CloseMessage() => Message = null;

    [RelayCommand]
    private void CloseError() => Error = null;

    protected static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected static string Number(decimal value, int digits = 0) => value.ToString(digits == 0 ? "#,##0" : $"#,##0.{new string('0', digits)}", CostsViewModel.German);

    protected string Clock(DateTimeOffset moment) => DayLayout.Local(moment, Zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    protected string Day(DateTimeOffset moment) => StockFormat.Date(DateOnly.FromDateTime(DayLayout.Local(moment, Zone)));

    /// <summary>Fragt nach dem Speicherort und schreibt die Datei der API unverändert; Fehler stehen danach in <see cref="Error"/>.</summary>
    protected async Task SaveFileAsync(string path, string suggestedName, string typeName, string pattern)
    {
        Error = Message = null;
        var result = await Api.GetBytesAsync(path).ConfigureAwait(true);
        if (result.Value is not { } content)
        {
            Error = result.Error ?? "Die Datei konnte nicht geladen werden.";
            return;
        }

        try
        {
            if (Files is not null && await Files.SaveAsync(suggestedName, typeName, [pattern], content).ConfigureAwait(true))
            {
                Message = $"{suggestedName} gespeichert.";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = "Die Datei konnte nicht gespeichert werden. Wählen Sie einen anderen Ort.";
        }
    }

    private TimeZoneInfo Zone => Location is { } location ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone) : Time.LocalTimeZone;
}

/// <summary>
/// Aktualisiert abhakbare Zeilen an Ort und Stelle, solange dieselben Zeilen bleiben: So behält ein Kontrollkästchen nach dem Neuladen den Tastaturfokus.
/// </summary>
internal static class ReportRows
{
    public static void Apply<TRow, TItem, TKey>(
        ObservableCollection<TRow> rows, IReadOnlyList<TItem> items, Func<TRow, TKey> rowKey, Func<TItem, TKey> itemKey, Func<TItem, TRow> create, Action<TRow, TItem> update)
    {
        if (rows.Select(rowKey).SequenceEqual(items.Select(itemKey)))
        {
            for (var i = 0; i < rows.Count; i++)
            {
                update(rows[i], items[i]);
            }

            return;
        }

        rows.Clear();
        foreach (var item in items)
        {
            rows.Add(create(item));
        }
    }
}

/// <summary>Eine abhakbare Zeile: Tagesstart, Raumpflege, Bereitstellen oder Vorbereitung. <see cref="Detail"/> steht klein daneben („Lisa, 08:10“).</summary>
public sealed partial class ReportCheckRow(Guid id, Func<ReportCheckRow, Task> toggle, Guid parentId = default) : ViewModelBase
{
    public Guid Id { get; } = id;

    /// <summary>Der Termin einer Bereitstellung: Dieselbe Zeile gilt nur für denselben Termin.</summary>
    public Guid ParentId { get; } = parentId;

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial bool Done { get; set; }

    [ObservableProperty]
    public partial bool CanToggle { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial string? Detail { get; set; }

    [ObservableProperty]
    public partial bool IsWarning { get; set; }

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    [RelayCommand]
    private Task ToggleAsync() => toggle(this);
}

/// <summary>Überschrift mit Textzeilen: ein Abschnitt des Tagesstarts.</summary>
public sealed record ReportSection(string Title, IReadOnlyList<string> Lines);

/// <summary>Ein Wert der Wertezeile (R07 Slice 144, Ä352): die Bezeichnung steht klein über dem Wert.</summary>
public sealed record ReportValue(string Label, string Text);
