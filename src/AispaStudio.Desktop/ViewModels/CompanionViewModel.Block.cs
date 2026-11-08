using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Der Antwortblock des Assistenten (ADR 0072): Tabelle, Quelle, Verweis in die Verwaltung („Im Bericht öffnen“, „Öffnungszeiten eintragen“), Kopieren bei Kontaktdaten
/// und Angebote als Fragen. Die Kennzeichnung der KI (Pille, Hinweis, „Warum diese Antwort?“ mit Quelle, Rückmeldung) steht wie bei jeder Antwort darunter.
/// </summary>
public sealed partial class CompanionViewModel
{
    /// <summary>Öffnet die Verwaltung bei der Stelle, auf die die Antwort verweist (Shell: Bereich der Verwaltung, mit Standort und Zeitraum).</summary>
    public Action<AssistantLink>? LinkRequested { get; set; }

    public AssistantBlock? Block => Answer?.Block;

    public AssistantTable? BlockTable => Block?.Table is { Rows.Count: > 0 } table ? table : null;

    /// <summary>„Quelle: Öffnungszeiten des Standorts Wien“; ohne Quelle (nichts gefunden, keine Berechtigung) leer.</summary>
    public string? SourceText => Block is { Source: { Length: > 0 } source } ? $"Quelle: {source}" : null;

    public string? LinkLabel => Block?.Link?.Label;

    public bool HasBlockLink => Block?.Link is not null && LinkRequested is not null;

    /// <summary>Text für „Kopieren“ (Kontaktdaten, Anreise).</summary>
    public string? CopyText => Block?.Copy;

    public bool HasCopy => !string.IsNullOrEmpty(Block?.Copy);

    public IReadOnlyList<AssistantFollowUp> FollowUps => Block?.FollowUps ?? [];

    public bool HasBlockActions => HasBlockLink || HasCopy;

    /// <summary>Begründung für „Warum diese Antwort?“ mit der Quelle; ohne Block der feste Text des Assistenten.</summary>
    public string WhyText => Block is { } block ? AiLabels.WhyBlock(block.Kind is AssistantBlockKind.Answer or AssistantBlockKind.NotFound ? block.Source : null) : AiLabels.WhyAssistant;

    public string WhyTitle => Block is null ? AiLabels.WhyTitle : AiLabels.WhyAnswerTitle;

    /// <summary>„Kopiert.“ nach dem Kopieren; die Ansicht legt den Text in die Zwischenablage und meldet es hier.</summary>
    [ObservableProperty]
    public partial string? CopiedText { get; private set; }

    public void MarkCopied() => CopiedText = "Kopiert.";

    /// <summary>Angaben, die dem Spa fehlen, obwohl die Antwort möglich war („Unvollständig“, mit Verweis auf das Feld oder dem Hinweis „Nur Leitung“).</summary>
    public IReadOnlyList<CompanionGap> Gaps => Block?.Incomplete?.Select(CompanionGap.Of).ToList() ?? [];

    private bool _awaitingSave;

    /// <summary>
    /// „Daten gespeichert. Frage wiederholen?“: erscheint, wenn die Verwaltung nach „Jetzt ergänzen“ etwas gespeichert hat. Die nativen Masken melden das über ihr
    /// Ereignis <c>Saved</c> (die Shell hängt es hier an); gilt nur, wenn die Person über einen Verweis auf ein Feld dorthin gekommen ist.
    /// </summary>
    [ObservableProperty]
    public partial string? SavedNotice { get; private set; }

    public void OnDataSaved()
    {
        if (_awaitingSave && Asked is not null)
        {
            SavedNotice = "Daten gespeichert. Frage wiederholen?";
        }
    }

    [RelayCommand]
    private void DismissSaved()
    {
        SavedNotice = null;
        _awaitingSave = false;
    }

    /// <summary>Stellt die Frage noch einmal, jetzt mit den ergänzten Daten.</summary>
    [RelayCommand]
    private Task RepeatQuestionAsync()
    {
        Question = Asked ?? "";
        return CanAskNow() ? AskAsync() : Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenBlockLink()
    {
        if (Block?.Link is { } link)
        {
            Open(link);
        }
    }

    /// <summary>Verweis des Abschnitts „Unvollständig“ („Jetzt ergänzen“).</summary>
    [RelayCommand]
    private void OpenGap(AssistantLink link) => Open(link);

    private void Open(AssistantLink link)
    {
        _awaitingSave = link.Field is not null;
        LinkRequested?.Invoke(link);
    }

    /// <summary>Ein Angebot der Antwort („Behandlungen nach Anzahl zeigen“) stellt seine Frage gleich.</summary>
    [RelayCommand]
    private Task AskFollowUpAsync(AssistantFollowUp offer)
    {
        Question = offer.Question;
        // Der Text kommt vom Server, nicht von der Person: er gehört nicht in „Zuletzt gefragt“.
        // Auswahl unter mehreren Gästen: die Frage geht mit dem gewählten Gast (Ä315).
        return CanAskNow() ? AskAsync(offer.GuestId, remember: false) : Task.CompletedTask;
    }
}
