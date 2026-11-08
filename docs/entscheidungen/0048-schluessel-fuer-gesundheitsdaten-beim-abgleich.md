# 0048. Schlüssel für Gesundheitsdaten beim Abgleich: je Stelle, mit dem Schlüsselcode des Spas geteilt

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Claude, autonom nach ADR 0019 (Auftraggeber: „weitermachen“, 18.10.2026)

## Kontext

Gesundheitsdaten (Anamnese) liegen mit AES-256-GCM verschlüsselt im Schema `health`, damit ein Datenbankauszug allein nichts Lesbares enthält. Jeder Einzelplatz erzeugt beim ersten Start einen eigenen Schlüssel (ADR 0041, Kennung `lokal-…`). Der Abgleich (Schritt 4) überträgt die verschlüsselten Zeilen unverändert. Ohne den Schlüssel der Stelle, die sie geschrieben hat, kann keine andere Stelle eine Anamnese lesen: der Gast füllt in Gmunden aus, in Bad Ischl sieht die Therapeutin nur einen Fehler.

## Optionen

| Option | Bewertung |
| --- | --- |
| Schlüssel im Klartext zentral ablegen | einfach, aber ein Auszug der zentralen Datenbank enthält dann lesbare Gesundheitsdaten; widerspricht Art. 9 und 32 DSGVO und ADR 0041 |
| Ein Schlüssel für alle Stellen, beim Verbinden von Hand übertragen (Base64, 44 Zeichen) | keine Speicherung, aber fehleranfällig abzutippen; bestehende Anamnesen der Stelle müssten neu verschlüsselt werden |
| Bestehende Daten beim Verbinden mit einem gemeinsamen Schlüssel neu verschlüsseln | ändert jede Anamnese-Zeile, jede Stelle schreibt sie beim Abgleich neu; Fehler mitten im Umschlüsseln schwer zu beheben |
| Jede Stelle behält ihren Schlüssel und legt ihn, mit einem Schlüsselcode des Spas verschlüsselt, auf beiden Seiten ab | gewählt: nichts wird umgeschlüsselt, ein Auszug bleibt ohne Code unlesbar, der Code ist abtippbar wie der Wiederherstellungsschlüssel |

## Entscheidung

- Das Spa hat einen **Schlüsselcode** (24 Zeichen, gleiches Format und Verfahren wie der Wiederherstellungsschlüssel: PBKDF2-SHA256 mit 600 000 Runden, AES-256-GCM, `KeySeal` in `AispaStudio.Contracts`).
- Die erste Stelle, die eine zentrale Datenbank einträgt, lässt das Feld leer; die App erzeugt den Code und zeigt ihn einmal. Jede weitere Stelle trägt ihn beim Verbinden ein. Die App merkt ihn sich DPAPI-geschützt in `zugang.bin` und kann ihn der Leitung wieder anzeigen; die API bekommt ihn als `HealthData:ShareCode`.
- Bei jedem Abgleich öffnet die Stelle zuerst die Schlüssel in `sync.health_keys` der zentralen Seite. Passt der Code nicht, bricht der Abgleich ab, bevor etwas übertragen wird, und die Seite „Abgleich“ zeigt das. Danach legt sie ihren eigenen Schlüssel, mit dem Code verschlüsselt, lokal und zentral ab und übernimmt die der anderen auch lokal.
- Geöffnete Schlüssel liegen nur im Speicher der API (`HealthKeyRing`) und dienen nur zum Entschlüsseln; verschlüsselt wird immer mit dem eigenen Schlüssel.
- Der Code liegt nie in einer Datenbank und nie neben einer Sicherung.

## Begründung

Jede Stelle bleibt für sich lesbar, auch ohne Verbindung. Es wird nichts umgeschlüsselt, also kann dabei nichts verloren gehen. Die zentrale Datenbank und jede Sicherung enthalten nur verschlüsselte Schlüssel; wer sie stiehlt, braucht zusätzlich den Code. Das Verfahren ist dasselbe wie beim Wiederherstellungsschlüssel, die Leitung kennt es also schon.

## Folgen und Stand

- Umgesetzt mit PR #113: `HealthKeyExchange` und `HealthKeyRing` in der API, `sync.health_keys`, Feld „Schlüsselcode des Spas“ auf der Seite „Abgleich“ der Windows-App.
- Wird der Code geändert, legt die Stelle ihren Schlüssel neu ab; die anderen Stellen melden dann „Schlüsselcode passt nicht“, bis sie den neuen eintragen. Einen geordneten Wechsel des Codes gibt es noch nicht.
- Eine zentrale API (Server-Betrieb mit vielen Spas) liest die Gesundheitsdaten der Einzelplätze erst, wenn sie den Code je Spa kennt; das ist offen und gehört zum Umstieg auf einen Server.
- Vorabstände der Windows-App ohne eindeutige Kennung (alle „lokal1“) teilen ihren Schlüssel nicht; neue Einrichtungen bekommen seit PR #96 eine eindeutige Kennung `lokal-…`.
- Ein neuer PC ohne Spa erkennt am Code, zu welchem Spa er gehört, und holt es beim ersten Abgleich (PR #115); ohne Code geht das nicht.
- Ein Schlüsselwechsel einer Stelle (neue Kennung) funktioniert ohne Zusatz: der neue Schlüssel wird beim nächsten Abgleich wie ein weiterer abgelegt.

## Quellen

- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), Abschnitt Abgleich und Gesundheitsdaten
- PR #110 (Wiederherstellungsschlüssel), PR #113
