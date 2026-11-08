namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Die KI-Funktionen der Windows-App (Design-Brief 5.28). Jede steht mit ihrer Kennung in <see cref="AiOutputAttribute"/> am Modell, das ihre Ausgabe zeigt;
/// ein Test sucht alle Modelle mit Hinweis auf eine KI-Ausgabe und verlangt dort das Kennzeichen samt Pille, Hinweiszeile, Begründung und Rückmeldung.
/// </summary>
public static class AiFunctions
{
    /// <summary>„KI und Hilfe“: Antworten des Assistenten mit Zahlen, Vorschlägen und Verweisen ins Handbuch.</summary>
    public const string Assistant = "assistent";

    /// <summary>„Wunsch eintippen“ (Strg+K): Bausteine und Vorschläge aus einem Wunschtext.</summary>
    public const string Request = "wunsch";

    /// <summary>Posteingang: erkannte Absicht, Zuordnung des Absenders und vorgeschlagene Zeiten.</summary>
    public const string InboxRead = "posteingang-lesen";

    /// <summary>Posteingang: Antwortentwurf, Überarbeiten und Übersetzung zur Kontrolle.</summary>
    public const string InboxDraft = "antwortentwurf";

    /// <summary>Meldungen › „Was tun?“: Antwort des Assistenten zur Meldung.</summary>
    public const string WhatToDo = "was-tun";

    public static IReadOnlyList<(string Id, string Name)> All { get; } =
    [
        (Assistant, "KI und Hilfe: Assistent"),
        (Request, "Wunsch eintippen"),
        (InboxRead, "Posteingang: Erkanntes, Zuordnung, Zeiten"),
        (InboxDraft, "Posteingang: Antwortentwurf"),
        (WhatToDo, "Meldungen: Was tun?"),
    ];
}

/// <summary>Kennzeichnet ein Modell, das die Ausgabe einer KI-Funktion zeigt (<see cref="AiFunctions"/>).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AiOutputAttribute(params string[] functions) : Attribute
{
    public IReadOnlyList<string> Functions { get; } = functions;
}
