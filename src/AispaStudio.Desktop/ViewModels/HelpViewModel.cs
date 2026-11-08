using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Hilfe und Handbuch (ADR 0079): die Hilfe-Seite (<c>help/index.html</c>) in einem eingebetteten Browser, beim
/// gesuchten Thema und Abschnitt (<c>?thema=…&amp;abschnitt=…</c>). Suche, Themenliste und Bilder zeichnet die Seite selbst, wie im Browser.
/// </summary>
/// <param name="serverWeb">Wurzel der Web-Adresse des Servers (<see cref="ServerWeb"/>); ohne Angabe <c>AISPA_TEAM_WEB_URL</c> (setzt der Aspire-AppHost).</param>
/// <param name="webRoot">Ordner <c>web</c> neben der App (Windows-Paket); ohne Angabe der Ordner der laufenden App. Der Browser ordnet ihn dem
/// virtuellen Host <see cref="VirtualHost"/> zu, die Hilfe-Seite liegt dann unter <c>https://help.aispa.local/help/index.html</c>.</param>
/// <param name="questions">Ablage der offenen Fragen an die Hilfe (Slice 56); ohne Angabe <see cref="UnansweredQuestionLog.Current"/>.</param>
/// <param name="prepare">Läuft vor dem ersten Anzeigen (<see cref="ShowAsync"/>), z. B. die Frage nach der Web-Adresse des Servers (<see cref="ServerWeb.LookUpAsync"/>).</param>
public sealed partial class HelpViewModel(Func<Uri?>? serverWeb = null, string? webRoot = null, IUnansweredQuestionStore? questions = null,
    TimeProvider? time = null, Func<Task>? prepare = null) : ViewModelBase
{
    /// <summary>Virtueller Hostname, dem der eingebettete Browser den Ordner <see cref="LocalFolder"/> zuordnet.</summary>
    public const string VirtualHost = "help.aispa.local";

    private readonly Func<Uri?> _serverWeb = serverWeb ?? ConfiguredServer;
    private readonly string _webRoot = webRoot ?? LocalServerPaths.Default.WebRoot;
    private int _shown;

    /// <summary>Rolle der angemeldeten Person; die Suche der Hilfe reiht passende Abläufe vor.</summary>
    public StaffRole? Role { get; set; }

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    private IUnansweredQuestionStore Questions => questions ?? UnansweredQuestionLog.Current;

    /// <summary>Inhaber, Manager und Standortleitung sehen über der Hilfe die offenen Fragen an die Hilfe (Slice 56); alle anderen nicht.</summary>
    public bool CanReviewQuestions => Role is StaffRole.Owner or StaffRole.GeneralManager or StaffRole.Manager;

    /// <summary>Fragen ohne Treffer der letzten 90 Tage auf diesem PC, die neueste zuerst.</summary>
    public ObservableCollection<OpenQuestionRow> OpenQuestions { get; } = [];

    /// <summary>Die Liste ist aufgeklappt; zu Beginn zu, damit die Hilfe die ganze Fläche hat.</summary>
    [ObservableProperty]
    public partial bool QuestionsExpanded { get; set; }

    // Beim Aufklappen neu lesen: die Leitung kann inzwischen den Assistenten gefragt haben, während die Hilfe offen war.
    partial void OnQuestionsExpandedChanged(bool value)
    {
        if (value)
        {
            RefreshQuestions();
        }
    }

    public string QuestionsTitle => $"Offene Fragen an die Hilfe ({OpenQuestions.Count})";

    public bool HasNoQuestions => OpenQuestions.Count == 0;

    /// <summary>Liest die Ablage neu; beim Anzeigen der Seite und nach „Erledigt“.</summary>
    public void RefreshQuestions()
    {
        OpenQuestions.Clear();
        if (CanReviewQuestions)
        {
            foreach (var entry in Questions.List(_time.GetUtcNow()))
            {
                OpenQuestions.Add(new OpenQuestionRow(entry, Meta(entry)));
            }
        }

        OnPropertyChanged(nameof(QuestionsTitle));
        OnPropertyChanged(nameof(HasNoQuestions));
    }

    [RelayCommand]
    private void MarkDone(OpenQuestionRow row)
    {
        Questions.Remove(row.Entry);
        RefreshQuestions();
    }

    private string Meta(UnansweredQuestion entry)
    {
        var parts = new List<string>();
        if (entry.Page is { } page)
        {
            parts.Add(HelpContent.Find($"seite-{page}")?.Title ?? page);
        }

        parts.Add(TimeZoneInfo.ConvertTime(entry.At, _time.LocalTimeZone).ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-AT")));
        if (entry.Count > 1)
        {
            parts.Add($"{entry.Count}-mal gefragt");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Adresse der Hilfe-Seite mit Ziel; <c>null</c>, solange nichts geöffnet ist oder die Seite fehlt (dann steht <see cref="Error"/> da).</summary>
    [ObservableProperty]
    public partial Uri? Address { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Die Seite kommt aus dem Ordner neben der App (über den virtuellen Host) und geht ohne Server und ohne Internet.</summary>
    public bool IsLocal => Address?.Host == VirtualHost;

    /// <summary>Ordner <c>web</c> des Windows-Pakets; der Browser ordnet ihn <see cref="VirtualHost"/> zu, bevor er die Seite lädt.</summary>
    public string LocalFolder => _webRoot;

    private string LocalPage => Path.Combine(_webRoot, "help", "index.html");

    /// <summary>Öffnet die Hilfe beim Thema (Kennung der Datei) und Abschnitt (Anker); ohne Thema steht die Übersicht da.</summary>
    public void Show(string? topic, string? section)
    {
        var page = FindPage();
        if (page is null)
        {
            Address = null;
            Error = "Die Hilfe ist hier nicht eingebaut. Im Windows-Paket liegt sie neben der App unter „web\\help“; beim Entwickeln die Adresse des "
                + "Web-Servers (Vite) in AISPA_TEAM_WEB_URL setzen oder die App über den AppHost starten.";
            return;
        }

        Error = null;
        // Die laufende Nummer macht jede Adresse neu, damit der Browser auch dann dorthin springt, wenn er schon einmal dort war und weitergeklickt wurde.
        var query = new List<string>();
        if (topic is not null)
        {
            query.Add($"thema={Uri.EscapeDataString(topic)}");
            if (section is not null)
            {
                query.Add($"abschnitt={Uri.EscapeDataString(section)}");
            }
        }

        // Nur eingebettet meldet die Seite Strg+Leertaste an die App (appShortcuts.ts).
        query.Add("eingebettet=1");
        query.Add($"oberflaeche={HelpContent.Surface}");
        if (HelpContent.RoleKey(Role) is { } role)
        {
            query.Add($"rolle={role}");
        }

        query.Add($"n={++_shown}");
        Address = new UriBuilder(page) { Query = string.Join('&', query) }.Uri;
    }

    /// <summary>Wie <see cref="Show"/>, aber erst, wenn die Vorbereitung (Web-Adresse des Servers) fertig ist.</summary>
    public async Task ShowAsync(string? topic, string? section)
    {
        if (prepare is not null)
        {
            await prepare().ConfigureAwait(true);
        }

        Show(topic, section);
    }

    /// <summary>Beim ersten Anzeigen ohne Ziel die Übersicht öffnen; ein Thema, das inzwischen geöffnet wurde, bleibt.</summary>
    public void EnsureShown()
    {
        RefreshQuestions();
        _ = ShowOverviewAsync();
    }

    private async Task ShowOverviewAsync()
    {
        if (prepare is not null)
        {
            await prepare().ConfigureAwait(true);
        }

        if (Address is null && Error is null)
        {
            Show(null, null);
        }
    }

    /// <summary>Strg+Leertaste im eingebetteten Browser (Nachricht <c>{"type":"companion"}</c>, <c>appShortcuts.ts</c>): die Shell schaltet „KI und Hilfe“ wie beim Kurzbefehl.</summary>
    public event Action? CompanionRequested;

    /// <summary>Nachricht der Hilfe-Seite; alles außer „companion“ ignoriert die App.</summary>
    public void OnWebMessage(string? body)
    {
        if (WebMessage.TypeOf(body) == "companion")
        {
            CompanionRequested?.Invoke();
        }
    }

    /// <summary>Der Ordner ließ sich dem virtuellen Host nicht zuordnen (kein WebView2): die Hilfe lässt sich nicht öffnen.</summary>
    public void ReportMappingFailed()
    {
        Address = null;
        Error = "Die Hilfe lässt sich hier nicht öffnen: Der eingebettete Browser (WebView2) nahm den Hilfe-Ordner neben der App nicht an.";
    }

    /// <summary>Der Server liefert die Hilfe-Seite nicht aus (<see cref="HelpPageCheck"/>): der Grund steht statt der Seite da.</summary>
    public void ReportServerProblem(string problem)
    {
        Address = null;
        Error = problem;
    }

    /// <summary>Nur die Hilfe-Seite selbst (oder die Seiten des eigenen Servers) bleibt in der App; alles andere öffnet im Standardbrowser.</summary>
    public bool IsOwnPage(Uri uri) => Address is { } own
        && Uri.Compare(uri, own, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;

    private Uri? FindPage() => File.Exists(LocalPage)
        ? new Uri($"https://{VirtualHost}/help/index.html")
        : _serverWeb() is { } web ? new Uri(web, "help/") : null;

    private static Uri? ConfiguredServer() =>
        Uri.TryCreate(Environment.GetEnvironmentVariable("AISPA_TEAM_WEB_URL"), UriKind.Absolute, out var url) ? new Uri(url, "/") : null;
}

/// <summary>Eine Zeile der Liste „Offene Fragen an die Hilfe“: Frage, darunter Seite, Datum und Anzahl; der Knopf „Erledigt“ nennt die Frage für den Screenreader.</summary>
public sealed record OpenQuestionRow(UnansweredQuestion Entry, string Meta)
{
    public string Question => Entry.Question;

    public string DoneLabel => $"Erledigt: {Entry.Question}";
}
