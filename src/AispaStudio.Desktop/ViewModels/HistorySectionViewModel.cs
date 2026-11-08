using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Eintrag im Abschnitt „Verlauf“: was geschah und wann (Ortszeit des Standorts).</summary>
public sealed record HistoryRow(string Text, string WhenText)
{
    /// <summary>Für den Screenreader beides in einem Satz.</summary>
    public string Name => $"{Text}, {WhenText}";
}

/// <summary>
/// Abschnitt „Verlauf“ im Termin-Panel und auf der Kundenkarte (Ä184, ADR 0082): eingeklappt, aufgeklappt jede Änderung mit Person, Kanal und Zeitpunkt, neueste oben.
/// Die Sätze baut der Server (<c>GET {path}</c>, z. B. <c>api/appointments/{id}/history</c>); geladen wird bei jedem Aufklappen, damit eigene Änderungen gleich dastehen.
/// </summary>
/// <param name="api">Der Zugang zur API.</param>
/// <param name="path">Der Pfad der Verlauf-Abfrage.</param>
/// <param name="zone">Die Zeitzone für Datum und Uhrzeit.</param>
/// <param name="tip">Der Tooltip des Kopfes.</param>
/// <param name="title">Die Überschrift des Abschnitts; auf der Kundenkarte „Änderungen“, weil dort die rechte Spalte schon „Verlauf“ heißt (Ä369).</param>
/// <param name="notLoadedMessage">Die Meldung, wenn das Laden scheitert; passt zur Überschrift.</param>
public sealed partial class HistorySectionViewModel(IWorkstationApi api, string path, TimeZoneInfo zone, string tip, string title = "Verlauf",
    string notLoadedMessage = "Der Verlauf wurde nicht geladen.") : ViewModelBase
{
    private int _version;

    /// <summary>Der Tooltip des Kopfes: wessen Verlauf das ist.</summary>
    public string Tip => tip;

    /// <summary>Die Überschrift des Abschnitts (Standard „Verlauf“).</summary>
    public string Title => title;

    /// <summary>Die Meldung über dem Knopf „Erneut laden“, wenn das Laden scheitert.</summary>
    public string NotLoadedMessage => notLoadedMessage;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsEmpty))]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntries), nameof(IsEmpty))]
    public partial IReadOnlyList<HistoryRow> Entries { get; private set; } = [];

    public bool HasError => Error is not null;

    public bool HasEntries => Entries.Count > 0;

    /// <summary>Geladen, aber noch nichts protokolliert (Termine von vor dem Verlauf).</summary>
    public bool IsEmpty => !IsLoading && Error is null && Entries.Count == 0 && _version > 0;

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            _ = LoadAsync();
        }
    }

    /// <summary>Lädt den Verlauf (beim Aufklappen und über „Erneut laden“).</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        var version = ++_version;
        IsLoading = true;
        Error = null;
        var result = await api.GetAsync<List<HistoryEntry>>(path).ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (result.Value is not { } entries)
        {
            Error = result.Error ?? "Der Verlauf konnte nicht geladen werden.";
            IsLoading = false;
            return;
        }

        Entries = [.. entries.Select(e => new HistoryRow(e.Text, DayLayout.Local(e.At, zone).ToString("d.M.yyyy, HH:mm", CultureInfo.InvariantCulture)))];
        IsLoading = false;
    }
}
