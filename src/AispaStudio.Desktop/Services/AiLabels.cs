namespace AispaStudio.Desktop.Services;

/// <summary>
/// Zentrale Texte der KI-Kennzeichnung (Design-Brief 5.28, Fluent 2 „Responsible AI“, F30). Der Wortlaut steht nur hier, damit er nicht je Ansicht neu getippt wird;
/// die Freigabe des Wortlauts liegt beim Auftraggeber. Die Texte sind sachlich: keine Ich-Form, keine Gefühle, kein „leider“, keine Wertung im Rückmeldungs-Tooltip.
/// </summary>
public static class AiLabels
{
    /// <summary>Text der Pille vor jeder KI-Ausgabe (Mini 12/500, <c>accent-soft</c>/<c>accent</c>).</summary>
    public const string Pill = "KI";

    /// <summary>Name der Pille für den Screenreader und Tooltip.</summary>
    public const string PillName = "Von der KI erstellt";

    /// <summary>Zeile unter jeder KI-Ausgabe, die keinen eigenen Hinweis vom Server mitbringt.</summary>
    public const string Disclaimer = "KI-Vorschlag. Bitte prüfen, bevor Sie ihn übernehmen.";

    /// <summary>Kopf über dem Antwortentwurf im Posteingang, solange er nicht gesendet ist.</summary>
    public const string DraftNotice = "KI-Entwurf, noch nicht gesendet";

    /// <summary>Hinweis unter einem Nachrichtentext, den die KI umformuliert hat (Absage wegen einer Schließzeit, ADR 0073); die Nachricht trägt den Hinweis dann beim Senden.</summary>
    public const string RevisedNotice = "Die KI hat den Text umformuliert. Die Nachricht trägt beim Senden den Hinweis darauf.";

    /// <summary>Aufklappbarer Abschnitt mit Quelle und Regel hinter einem Vorschlag; kurz, weil er in der Hinweiszeile steht (Auftraggeber, 29.10.2026).</summary>
    public const string WhyTitle = "Warum?";

    /// <summary>Überschrift des Abschnitts bei Antworten aus den Daten des Spas (ADR 0072).</summary>
    public const string WhyAnswerTitle = "Warum diese Antwort?";

    // Begründungen („Warum dieser Vorschlag?“): Quelle und Regel in zwei, drei Sätzen, nur was der Code tatsächlich so macht (Zahlen und Zeiten rechnet nie die KI, CLAUDE.md).

    public const string WhyAssistant = "Der Assistent hat nur Ihre Frage verstanden. Zahlen und Zeiten stammen aus Kalender und Kassa, die Anleitungen aus dem Handbuch. Gebucht wird nichts, bevor Sie einen Vorschlag wählen.";

    public const string WhyRequest = "Die KI liest nur den Wunschtext und zerlegt ihn in die Bausteine. Freie Zeiten, Person, Raum und Preis rechnet der Kalender; die Begründung steht bei jedem Vorschlag. Gebucht wird erst auf Ihren Klick.";

    public const string WhyInboxRead = "Die KI liest die Nachricht und erkennt Absicht, Behandlung und Wunschzeit. Die vorgeschlagenen Zeiten rechnet der Kalender, nicht die KI. Eingetragen wird erst, wenn Sie eine Zeit wählen; der Absender wird erst nach Ihrem Klick einem Gast zugeordnet.";

    public const string WhyInboxDraft = "Die KI schreibt den Entwurf aus der erkannten Absicht und den vorgeschlagenen Zeiten, in der Sprache des Gastes. Zahlen übernimmt sie nur aus dem Entwurf; erfindet sie eine, bleibt der Entwurf, wie er ist. Gesendet wird erst, wenn Sie auf Senden klicken.";

    public const string WhyWhatToDo = "Der Assistent hat die Meldung gelesen und fasst sie in wenigen Sätzen zusammen. Zahlen stammen aus Kalender und Kassa. Ausgeführt wird nur, was Sie anklicken.";

    /// <summary>
    /// Begründung einer Antwort aus den Daten des Spas (ADR 0072) mit der Quelle: was die KI getan hat (nur die Frage verstanden) und woher die Angaben stammen.
    /// Ohne Quelle (nichts gefunden, keine Berechtigung) entfällt der zweite Satz.
    /// </summary>
    public static string WhyBlock(string? source) =>
        "Der Assistent hat nur Ihre Frage verstanden. "
        + (string.IsNullOrWhiteSpace(source) ? "" : $"Die Angaben stehen fest und stammen aus: {source}. ")
        + "Die KI rechnet nichts und erfindet nichts; gebucht oder geändert wird nichts.";

    /// <summary>Vor der ersten Frage: was der Assistent beantwortet und was nicht (Fluent: Erwartungen vor der Interaktion).</summary>
    public const string AssistantScope = "Der Assistent antwortet aus den Daten des Spas und dem Handbuch. Er bucht und ändert nichts; gebucht wird erst auf Ihren Klick.";

    /// <summary>Platzhalter im Fragefeld: Beispiele für Fragen, die der Assistent aus den Daten beantwortet.</summary>
    public const string QuestionPlaceholder = "Frage eingeben";

    /// <summary>Wege ohne KI, wenn die KI ausfällt (Fluent: bei KI-Fehlern immer ein Weg ohne KI).</summary>
    public const string WayOutHelp = "Ohne KI weiter: das Handbuch öffnen und dort nachlesen.";

    public const string WayOutFreeTime = "Ohne KI weiter: die freie Zeit selbst suchen.";


    public const string WayOutHelpTopic = "Ohne KI weiter: das Handbuch zur Meldung steht unten.";
}
