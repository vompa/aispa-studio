using System.Collections.ObjectModel;
using AispaStudio.Desktop.Services;
using AispaStudio.Help;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Hilfe in der Leiste (ADR 0079, Nachtrag 17.11.2026): Ein Fragezeichen öffnet „KI und Hilfe“ mit einem Hilfe-Bereich oben, der genau Thema und Abschnitt zeigt;
/// die Maske bleibt stehen. Den Inhalt zeichnet die eine Web-Hilfe im eingebetteten Browser (<see cref="HelpArea"/>, Modus <c>ansicht=leiste</c>),
/// Kopf und Schließen stehen nativ darüber. Ein weiteres Fragezeichen tauscht nur den Inhalt; Schließen stellt den Stand davor wieder her.
/// </summary>
public sealed partial class CompanionViewModel
{
    /// <summary>Die Leiste ging nur für die Hilfe auf; schließt die Hilfe, geht sie wieder zu. Offen oder zu je Seite merkt sich die App dabei nicht.</summary>
    private bool _openedForHelp;

    /// <summary>Die Hilfe-Seite der Leiste (<c>ansicht=leiste</c>); ohne Rahmen (Tests ohne Shell) <c>null</c>.</summary>
    public HelpViewModel? HelpArea { get; init; }

    /// <summary>Thema und Abschnitt im Hilfe-Bereich; <c>null</c>, wenn keine Hilfe steht.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsHelp), nameof(HelpTitle), nameof(HelpSubtitle), nameof(ShowsPageSection))]
    public partial HelpTarget? Help { get; private set; }

    /// <summary>Unter dem Kopf das Thema, wenn ein Abschnitt steht (bei „Schritte“ wüsste man sonst nicht, wovon).</summary>
    public string? HelpSubtitle => Help is { Section: not null } help ? _find(help.Topic)?.Title : null;

    /// <summary>Der Hilfe-Bereich ist zu sehen: eine Hilfe steht und die Leiste ist offen.</summary>
    public bool ShowsHelp => Help is not null && IsOpen;

    /// <summary>Kopf des Hilfe-Bereichs: „Hilfe: “ und der Abschnitt, ohne Abschnitt das Thema.</summary>
    public string HelpTitle => Help is { } help && _find(help.Topic) is { } topic
        ? $"Hilfe: {(help.Section is not null && topic.Blocks.OfType<HelpHeading>().FirstOrDefault(h => h.Id == help.Section) is { } heading ? heading.Text : topic.Title)}"
        : "Hilfe";

    /// <summary>Der erste Absatz des Abschnitts: steht da, bis die Hilfe-Seite geladen ist (der Browser liegt dann darüber), und ohne Browser in Tests.</summary>
    public ObservableCollection<HelpParagraph> HelpLead { get; } = [];

    /// <summary>Zeigt Thema und Abschnitt im Hilfe-Bereich und öffnet dafür die Leiste; ohne Thema die Erklärung zur offenen Seite.</summary>
    public bool ShowHelp(string? topic, string? section)
    {
        topic ??= Topic?.Id;
        if (topic is null || _find(topic) is not { } found)
        {
            return false;
        }

        if (!IsOpen)
        {
            _openedForHelp = true;
            SetOpenQuietly(true);
        }

        Help = new HelpTarget(topic, section is not null && found.HasSection(section) ? section : null);
        HelpLead.Clear();
        if (LeadOf(found, Help.Section) is { } lead)
        {
            HelpLead.Add(lead);
        }

        _ = HelpArea?.ShowAsync(Help.Topic, Help.Section);
        return true;
    }

    /// <summary>Kreuz im Kopf des Hilfe-Bereichs oder Esc: die Hilfe geht weg; war die Leiste nur für sie offen, geht sie ganz zu.</summary>
    [RelayCommand]
    public void CloseHelp()
    {
        if (Help is null)
        {
            return;
        }

        Help = null;
        HelpLead.Clear();
        if (_openedForHelp)
        {
            _openedForHelp = false;
            SetOpenQuietly(false);
        }
    }

    /// <summary>„Im Handbuch öffnen“: die Seite „Hilfe“ an derselben Stelle; der Seitenwechsel schließt den Hilfe-Bereich.</summary>
    [RelayCommand]
    private void OpenManual() => (ManualRequested ?? HelpContent.OpenManual)(Help?.Topic, Help?.Section);

    /// <summary>Zeigt die Seite „Hilfe“ an einer Stelle (setzt der Rahmen); ohne Angabe <see cref="HelpContent.OpenManual"/>.</summary>
    public Action<string?, string?>? ManualRequested { get; init; }

    /// <summary>Hilfe weg, ohne die Leiste anzufassen (Kreuz der ganzen Leiste, Seitenwechsel).</summary>
    private void ForgetHelp()
    {
        _openedForHelp = false;
        Help = null;
        HelpLead.Clear();
    }

    /// <summary>Offen oder zu, ohne es je Seite zu merken und ohne den Fokus ins Fragefeld zu setzen (der Fokus bleibt in der Maske).</summary>
    private void SetOpenQuietly(bool open)
    {
        _quiet = true;
        IsOpen = open;
        _quiet = false;
        if (open)
        {
            LoadHistory();
        }
    }

    private static HelpParagraph? LeadOf(HelpTopic topic, string? section) =>
        (section is null ? topic.Blocks : topic.Section(section)).OfType<HelpParagraph>().FirstOrDefault();
}

/// <summary>Ziel in der Hilfe: Thema (Kennung der Datei) und Abschnitt (Anker).</summary>
public sealed record HelpTarget(string Topic, string? Section);
