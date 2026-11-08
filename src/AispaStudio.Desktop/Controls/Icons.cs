namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Symbole der App als Pfaddaten auf dem 24×24-Raster; in XAML mit <c>{x:Static ui:Icons.Calendar}</c>.
/// Als Text statt als Geometrie, weil Geometrien an den Thread gebunden sind, der sie erzeugt.
/// </summary>
public static class Icons
{
    public const string Logo = "M3 17c3-1 4.5-4 9-4s6 3 9 4M3 21c3-1 4.5-3 9-3s6 2 9 3M12 3c2 2.5 2 5.5 0 8c-2-2.5-2-5.5 0-8z";

    public const string Calendar = "M8 2v4M16 2v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z";

    public const string Users = "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 3a4 4 0 1 1 0 8a4 4 0 0 1 0-8zM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75";

    public const string User = "M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 3a4 4 0 1 1 0 8a4 4 0 0 1 0-8z";

    public const string Register = "M4 7h16a1 1 0 0 1 1 1v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a1 1 0 0 1 1-1zM6 7V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v3M7 11h2M11 11h2M15 11h2M7 15h2M11 15h2M15 15h2";

    public const string Sun = "M12 8a4 4 0 1 1 0 8a4 4 0 0 1 0-8zM12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41";

    public const string Moon = "M12 3a6 6 0 0 0 9 9a9 9 0 1 1-9-9z";

    public const string Monitor = "M4 4h16a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1zM8 20h8M12 16v4";

    public const string SignOut = "M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9";

    public const string ChevronLeft = "M15 18l-6-6 6-6";

    public const string ChevronRight = "M9 18l6-6-6-6";

    public const string ChevronUp = "M18 15l-6-6-6 6";

    public const string ChevronDown = "M6 9l6 6 6-6";

    public const string Plus = "M12 5v14M5 12h14";

    public const string Minus = "M5 12h14";

    /// <summary>Sauna und Ruheraum: Dampf über Wellen.</summary>
    public const string Steam = "M2 16c2-2 4-2 6 0s4 2 6 0 4-2 6 0M2 21c2-2 4-2 6 0s4 2 6 0 4-2 6 0M8 3c-1 1.5 1 2.5 0 4M12 3c-1 1.5 1 2.5 0 4M16 3c-1 1.5 1 2.5 0 4";

    public const string Archive = "M3 3h18v5H3zM5 8v11a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8M10 12h4";

    public const string Refresh = "M21 12a9 9 0 1 1-2.64-6.36L21 8M21 3v5h-5";

    public const string Search = "M11 4a7 7 0 1 1 0 14a7 7 0 0 1 0-14zM21 21l-4.3-4.3";

    public const string Close = "M18 6L6 18M6 6l12 12";

    public const string Clock = "M12 3a9 9 0 1 1 0 18a9 9 0 0 1 0-18zM12 7v5l3 2";

    public const string Check = "M20 6L9 17l-5-5";

    public const string Door = "M3 21h18M6 21V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v17M14 12h.01";

    /// <summary>Stecknadel: Standort.</summary>
    public const string Pin = "M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0zM12 7a3 3 0 1 1 0 6a3 3 0 0 1 0-6z";

    public const string Undo = "M9 14L4 9l5-5M4 9h10.5a5.5 5.5 0 0 1 0 11H11";

    public const string Note = "M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9zM14 3v6h6M8 13h8M8 17h5";

    /// <summary>Stift: bearbeiten.</summary>
    public const string Pencil = "M17 3a2.83 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z";

    /// <summary>Anrede Frau (♀), klein hinter dem Gastnamen; als Strich gezeichnet, damit keine Emoji-Schrift ein buntes Bild daraus macht.</summary>
    public const string Female = "M12 3a5 5 0 1 1 0 10a5 5 0 0 1 0-10zM12 13v8M9 18h6";

    /// <summary>Anrede Herr (♂).</summary>
    public const string Male = "M10 9a5 5 0 1 1 0 10a5 5 0 0 1 0-10zM13.5 10.5L20 4M15 4h5v5";

    /// <summary>Icon zur Anrede aus dem Textzeichen ♀/♂; null ohne Anrede.</summary>
    public static string? Salutation(string? symbol) => symbol switch
    {
        "♀" => Female,
        "♂" => Male,
        _ => null,
    };

    /// <summary>Stecknadel für die Postanschrift.</summary>
    public const string Location = "M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0zM12 7a3 3 0 1 1 0 6a3 3 0 0 1 0-6z";

    public const string Globe = "M12 2a10 10 0 1 1 0 20a10 10 0 0 1 0-20zM2 12h20M12 2a15 15 0 0 1 0 20M12 2a15 15 0 0 0 0 20";

    public const string Phone = "M22 16.9v3a2 2 0 0 1-2.2 2a19.8 19.8 0 0 1-8.6-3.1a19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2A2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .4 1.9.7 2.8a2 2 0 0 1-.5 2.1L8 9.9a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.4c.9.3 1.8.6 2.8.7a2 2 0 0 1 1.7 2z";

    public const string Mail = "M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1zM3 7l9 6 9-6";

    public const string Move = "M5 9l-3 3 3 3M9 5l3-3 3 3M15 19l-3 3-3-3M19 9l3 3-3 3M2 12h20M12 2v20";

    public const string Printer = "M6 9V3h12v6M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v7H6z";

    public const string Card = "M3 5h18a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1zM2 10h20M6 15h4";

    public const string Receipt = "M5 2h14v20l-3-2-2 2-2-2-2 2-2-2-3 2zM9 7h6M9 11h6M9 15h4";

    public const string Wifi = "M5 12.55a11 11 0 0 1 14 0M8.5 16.1a6 6 0 0 1 7 0M12 20h.01M2 8.82a15 15 0 0 1 20 0";

    public const string Percent = "M19 5 5 19M6.5 9a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5zM17.5 20a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z";

    public const string Gift = "M20 12v10H4V12M2 7h20v5H2zM12 22V7M12 7H7.5a2.5 2.5 0 0 1 0-5C11 2 12 7 12 7zM12 7h4.5a2.5 2.5 0 0 0 0-5C13 2 12 7 12 7z";

    public const string Help = "M12 2a10 10 0 1 1 0 20a10 10 0 0 1 0-20zM9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3M12 17h.01";

    public const string Sliders = "M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6";

    /// <summary>Meldungen (InfoBar): Info, Erfolg, Warnung, Fehler; immer zusätzlich zur Farbe.</summary>
    public const string Info = "M12 2a10 10 0 1 1 0 20a10 10 0 0 1 0-20zM12 16v-4M12 8h.01";

    public const string CheckCircle = "M12 2a10 10 0 1 1 0 20a10 10 0 0 1 0-20zM8 12l3 3 5-6";

    public const string Warning = "M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01";

    public const string ErrorCircle = "M12 2a10 10 0 1 1 0 20a10 10 0 0 1 0-20zM15 9l-6 6M9 9l6 6";

    /// <summary>Glocke: Schaltfläche und Seitenleiste „Meldungen“.</summary>
    public const string Bell = "M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10.3 21a1.94 1.94 0 0 0 3.4 0";

    /// <summary>Besen: Raumpflege.</summary>
    public const string Broom = "M20 4l-8 8M14 10l-4 4M9.5 12.5l2 2c-.5 2.5-2 4.8-4.5 6.5H3c1-3.5 2.8-6.5 6.5-8.5zM6 17.5l2 2";

    /// <summary>Sprechblase: Sprachen.</summary>
    public const string Chat = "M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z";

    /// <summary>Zeiger: Hinweis auf den Klick.</summary>
    public const string Cursor = "M4.04 4.69a.5.5 0 0 1 .65-.65l16 6.5a.5.5 0 0 1-.06.95l-6.12 1.58a2 2 0 0 0-1.44 1.43l-1.58 6.13a.5.5 0 0 1-.95.06z";

    public const string Copy = "M9 9h11a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V10a1 1 0 0 1 1-1zM5 15H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1h11a1 1 0 0 1 1 1v1";

    /// <summary>Papierkorb: „Entfernen“ in Zeilenlisten (Web, Design-Brief 5.1).</summary>
    public const string Trash = "M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M10 11v6M14 11v6";

    /// <summary>Schloss: Sperrzeit in der Konfliktmeldung.</summary>
    public const string Lock = "M7 11V7a5 5 0 0 1 10 0v4M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z";

    /// <summary>Pfeil in die Tür hinein: „Einchecken“ im Termin-Menü.</summary>
    public const string SignIn = "M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4M10 17l5-5-5-5M15 12H3";

    /// <summary>Person mit Kreuz: „Nicht erschienen“ im Termin-Menü.</summary>
    public const string UserX = "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 3a4 4 0 1 1 0 8a4 4 0 0 1 0-8zM17 8l5 5M22 8l-5 5";

    /// <summary>Kalender mit Plus: „Folgetermin“ im Termin-Menü.</summary>
    /// <summary>Kalender-Abo (ADR 0075): Kalender mit kreisenden Pfeilen unten rechts.</summary>
    public const string CalendarSync = "M8 2v4M16 2v4M3 10h18M21 11V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h6M14.5 16.5a4 4 0 0 1 7-1.5M22 13v3h-3M21.5 19.5a4 4 0 0 1-7 1.5M14 24v-3h3";

    public const string CalendarPlus = "M8 2v4M16 2v4M21 13V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h8M3 10h18M19 16v6M16 19h6";

    /// <summary>Kalender mit Lupe: „Freie Zeit suchen“ neben „+ Termin“ im Kalenderkopf.</summary>
    public const string CalendarSearch = "M8 2v4M16 2v4M3 10h18M21 11.75V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h7.25M18 15a3 3 0 1 1 0 6a3 3 0 0 1 0-6zM22 22l-1.9-1.9";

    /// <summary>Schraubenschlüssel: Gerät in der Konfliktmeldung.</summary>
    public const string Wrench = "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z";
}
