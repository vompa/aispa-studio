# 0077. Textregeln nach Google und Microsoft für Hilfe, Oberfläche, ADRs und Schnittstelle

- **Status:** angenommen
- **Datum:** 23.10.2026
- **Entschieden von:** Claude, autonom nach [ADR 0019](0019-autonomes-arbeiten-bis-zum-prototyp.md) (Auftrag T-012)

## Kontext

Der Google Developer Documentation Style Guide und der Microsoft Writing Style Guide stehen in der Arbeitsweise des Auftraggebers als Vorgaben für Dokumentation. Im Repository gab es keine deutsche Kurzfassung, und die Hilfetexte wurden nicht geprüft. Die Durchsicht beim Anlegen der Prüfung fand 13 Stellen mit „bitte“ und 37 mit Richtungswörtern („Unten links auf das Profilbild“); dazu stehen zwei Beschriftungen der App in Du-Form; die Regeln waren also im Bestand nicht durchgehend eingehalten.

## Optionen

| Option | Bewertung |
| --- | --- |
| Beide Leitfäden nur als Link nennen | kostet nichts, aber niemand liest zwei englische Leitfäden vor jedem PR; nichts wird geprüft. Verworfen |
| Eigene Seite mit Regeln, Beispielen aus dem Projekt und einem Prüfsatz im PR-Protokoll, dazu ein automatischer Test für die eindeutig prüfbaren Regeln | gewählt |
| Vollständiger Prosa-Linter (Vale oder LanguageTool) mit Regeldateien | prüft mehr, bringt aber ein weiteres Werkzeug in die CI und braucht deutsche Regelpakete, die wir nicht geprüft haben. Verworfen; bei Bedarf später |

## Entscheidung

- Die Seite [docs/gestaltung/texte.md](../gestaltung/texte.md) fasst die Regeln beider Leitfäden auf Deutsch zusammen (21 Regeln, je mit Beispiel aus dem Projekt) und gilt für Hilfetexte, Oberflächentexte, ADRs, PR-Texte und OpenAPI-Beschreibungen.
- `web/src/help/textRules.test.ts` prüft alle Dateien in `web/src/help/content/` auf „bitte“, „klicken Sie hier“, Du-Form, Richtungspaare („oben links“, „unten rechts“ …), Wörter in Versalien ab vier Buchstaben außer Abkürzungen (Liste im Test) und Ausrufezeichen. Zitate in „…“, fett gesetzte Beschriftungen, Inline-Code, Codeblöcke und die Kopfangaben bleiben ausgenommen. Einzelne Richtungswörter („oben“, „links“) werden nicht geprüft: rund 350 Treffer, meist Lage in Listen oder Zeitachsen.
- Jeder PR-Abschnitt, der Texte ändert, enthält den Prüfsatz aus Abschnitt 4 von texte.md.

## Begründung

Die Leitfäden stehen in der Arbeitsweise des Auftraggebers, und der Bestand zeigte, dass ohne Prüfung Richtungswörter und „bitte“ nachwachsen. Eine kurze Seite mit Beispielen aus dem eigenen Handbuch wird eher gelesen als der Leitfaden; der Test hält die eindeutigen Regeln ohne zusätzliche Werkzeuge ein. Alles, was Urteil verlangt (Aktiv, ein Gedanke je Satz, einheitliche Begriffe), bleibt Durchsicht und steht deshalb als Prüfsatz im PR-Protokoll.

## Folgen und Stand

- Neue Hilfetexte, die die geprüften Regeln brechen, machen die Vitest-Suite rot (`npx vitest run src/help`). Ausnahmen für Abkürzungen kommen in die Liste im Test.
- Die Beschriftungen „Wer bist du?“ und „Dein Name“ in der App stehen noch in Du-Form (Widerspruch zu design.md, Ton der Texte); die Hilfe nennt sie wörtlich, der Test lässt sie zu. Änderung der App-Texte ist offen und braucht Freigabe des Auftraggebers.
- Offen: ein deutsches Wörterbuch der Fachbegriffe (texte.md T12 hat nur eine Auswahl); Prüfung von Oberflächentexten im Code (`Resources`, `AiLabels`, TSX) mit demselben Test.

## Quellen

- [Google Developer Documentation Style Guide](https://developers.google.com/style/)
- [Microsoft Writing Style Guide](https://learn.microsoft.com/style-guide/welcome/)
- [docs/gestaltung/texte.md](../gestaltung/texte.md), [design.md](../design.md) (Ton der Texte), [meldungen.md](../gestaltung/meldungen.md)
