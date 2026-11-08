# Eingabeprüfung am Rundenende: Vorschlag

Stand: 27.10.2026 (Runde R03, Slice 81, Ä98). Konzept, nichts davon ist gebaut. Anlass: der Auftraggeber fragte beim Test von R02, ob eine Prüfung von Eingaben und Formularen (Skills `input-validation`, `form-validation`) am Ende jeder Runde laufen soll.

## Kurzfassung

- **Die beiden Skills lohnen sich nicht.** Sie sind kleine Gemeinschafts-Skills für andere Technik (Zod im Backend, React-Formulare mit Fremdbibliotheken). Der Wortlaut ist nicht mehr abrufbar. Skills dieser Art prüfen nichts, sie geben Claude nur Schreibregeln mit.
- **Ein fester Schritt am Rundenende lohnt sich trotzdem**, aber aus eigenen Mitteln: Das Projekt hat die Bausteine (`FormField`, `MoneyField`, `useFocusFirstInvalid`, `ParsedField`), die Prüfliste in `fluent2-web` und 34 Testdateien mit axe-core. Es fehlt nur, dass am Rundenende jemand fragt, welche Masken in der Runde neu oder geändert wurden und ob sie diese Prüfungen bestehen.
- **Vorschlag:** ein Schritt „Eingabeprüfung“ im Rundenabschluss, Aufwand rund 1 bis 2 Stunden je Runde (einmalig 3 bis 4 Stunden für den Prüftest, siehe Abschnitt 3).

## 1. Die beiden Skills

Gelesen, nicht installiert und nicht ausgeführt. **Alles unten stammt aus Fremdquellen und ist in eigenen Worten zusammengefasst.**

| | `input-validation` | `form-validation` |
| --- | --- | --- |
| Verzeichnis | [claudskills.com/skills/input-validation](https://claudskills.com/skills/input-validation/) | [claudskills.com/skills/form-validation](https://claudskills.com/skills/form-validation/) |
| Autor, Herkunft | FRmicrow, Repository `dataFootV1`, Ordner `.claude/skills/backend/input-validation` | FRmicrow, dasselbe Repository, Ordner `.claude/skills/frontend/form-validation` |
| Zweck laut Beschreibung (französisch) | Eingaben mit Zod prüfen, wenn man ein Zod-Schema unter `backend/src/schemas/` anlegt oder ändert | React-Formulare auf der Clientseite prüfen, wenn ein Formular Fehler an die Nutzerin zurückmeldet |
| Lizenz | auf der Seite nicht genannt | auf der Seite nicht genannt |
| Inhalt | Der Text der Skill-Datei steht nicht auf der Seite | wie links |
| Sicherheitsprüfung des Verzeichnisses | „Grad A“, automatische Prüfung auf Risikomuster, keine Handprüfung | wie links |

**Grenze der Recherche:** Das Repository `FRmicrow/dataFootV1` ist über GitHub nicht mehr erreichbar (404, gelöscht oder privat). Der Wortlaut beider Skills ließ sich deshalb nicht lesen; belegt sind nur Autor, Pfad, Beschreibung und, dass das Verzeichnis keine Skripte nennt. Was die Skills im Einzelnen anweisen, ist **nicht geprüft**.

Zum Vergleich gibt es unter dem Namen `form-validation` eine zweite, unabhängige Fassung im Sammel-Repository `aj-geddes/useful-ai-prompts` ([claude-plugins.dev](https://claude-plugins.dev/skills/@aj-geddes/useful-ai-prompts/form-validation)): React Hook Form, Formik mit Yup, Vee-Validate und ein selbstgebauter Prüf-Hook, dazu allgemeine Hinweise („testen, Fehler behandeln, nichts fest verdrahten“). Barrierefreiheit kommt auf der Seite nicht vor. Eine Fassung `input-validation` derselben Sammlung war nicht abrufbar (Serverfehler). Beide Namen gibt es also von mehreren Verfassern; „der Skill `form-validation`“ ist keine feste Sache.

**Was Skills dieser Art tun:** Sie geben Claude beim Schreiben von Code Regeln und Beispiele mit. Sie führen keine Prüfung aus, erzeugen keinen Bericht und sagen nichts darüber, ob ein fertiges Formular besteht.

## 2. Abgleich mit dem Projekt

| Baustein im Projekt | Was er schon leistet | Was die Skills dazu beitrügen |
| --- | --- | --- |
| `FormField` (Web, `web/src/components/FormField.tsx`) | Beschriftung, Pflichtstern ohne Lesefehler, Hilfetext, Fehler ersetzt Hilfetext, `aria-invalid`, `aria-describedby`, `aria-required`; Regeln R6 bis R8 in `docs/gestaltung/masken.md` | nichts: Fremdbibliotheken (React Hook Form, Formik, Zod) sind im Projekt nicht im Einsatz, und neue Pakete brauchen Freigabe |
| `MoneyField` (Web, `web/src/pos/`) | tolerant lesen, beim Verlassen `12,50` schreiben, Fehler bleibt mit dem Text am Feld stehen | nichts; Schemas kennen keine Beträge im österreichischen Format |
| `useFocusFirstInvalid` | Fokus nach misslungenem Speichern auf das erste ungültige Feld (masken.md R8) | nichts |
| `ParsedField`, `DateField`, `FormField` (Windows-App, `Controls/`) | dieselben Regeln in Avalonia | die Skills behandeln nur React |
| Server | Prüfung im Backend bei jedem Endpunkt (`RequirePermission`, Prüfungen im Dienst) | `input-validation` meint Zod im Node-Backend, nicht ASP.NET |
| `fluent2-web`, Abschnitt Eingaben | Prüfliste: Hilfe und Fehler verknüpft (`toHaveAccessibleDescription`), Fokus nach fehlgeschlagenem Absenden, genau ein Prüfkanal (`noValidate`), Parser-Tests mit zwei Sprachumgebungen | deckt mehr ab als beide Skills zusammen, mit Quellen |
| axe-core | 34 Testdateien im Web rufen `axe.run` auf (zum Beispiel `SignupForm.test.tsx`, Kontrastregel aus, weil jsdom keine Farben kennt) | nichts |
| Quelltext-Prüftests | `formFieldUsage.test.ts` verbietet eigene `Row`/`Group` in den Formularseiten und `focusFirstInvalid` mit `setTimeout` | nichts |

**Ergebnis:** Die Skills passen weder zur Technik noch zu den Regeln. Ihr einziger denkbarer Nutzen, Regeln beim Schreiben bereitzuhalten, ist durch `masken.md`, `CLAUDE.md` und `fluent2-web` schon abgedeckt, mit Quellen und für diesen Stack.

**Was fehlt wirklich:**
1. Nichts verlangt am Rundenende den Blick auf alle in der Runde neuen oder geänderten Masken. Heute prüft jede Slice ihre eigene Maske; ob eine zweite Slice dieselbe Maske verschlechtert hat, sieht niemand.
2. axe läuft nur dort, wo jemand einen Test geschrieben hat. Dass eine Maske ohne axe-Test bleibt, meldet nichts.
3. Für die Windows-App gibt es Scan-Tests über alle Seiten (`…jeder_Seite…`, `Jede_Ansicht…`), aber keinen eigenen für die Eingabefelder einer Seite.

## 3. Vorschlag: fester Schritt „Eingabeprüfung“ im Rundenabschluss

**Wann:** beim Schließen der Runde, nach der vollen Prüfkette und vor der Testliste für den Auftraggeber (`/slice close`, Schritte 2 bis 3).

**Was:** zwei Teile.

1. **Mechanisch (Test, läuft in der Prüfkette mit).** Ein Vitest-Prüftest `web/src/formCoverage.test.ts` nach dem Vorbild von `findingsMarked.test.ts` und `formFieldUsage.test.ts`:
   - findet alle Quelltexte mit `<input`, `<select` oder `<textarea` (ohne Tests),
   - verlangt, dass jedes Feld in `FormField` oder `MoneyField` steckt oder in einer kleinen, begründeten Ausnahmeliste steht,
   - verlangt zu jeder Seite mit einem Formular eine Testdatei mit `axe.run` oder einen Eintrag in der Ausnahmeliste.

   Ergebnisformat: ein roter Test mit der Liste der Dateien, wie die bestehenden Prüftests.
2. **Durchsicht (Mensch oder lesender Agent auf Sonnet).** Für die in der Runde geänderten Masken (aus `git diff --name-only` seit Rundenbeginn, gefiltert auf `web/src` und `src/AispaStudio.Desktop/Views`) die Kurz-Checkliste aus `docs/gestaltung/masken.md` und den Abschnitt „Eingaben“ von `fluent2-web` bzw. `fluent2-avalonia` abgehen: Fehler am Feld, Fokus auf das erste ungültige Feld, kein Speichern-Knopf, der ohne Grund gesperrt ist, Eingabe bleibt nach Fehler stehen, Tastatur. Ergebnisformat: eine Tabelle in der Rundenakte unter „Überraschungen“, eine Zeile je Maske: *Maske, geprüft gegen, Ergebnis (ok oder Befund-Kennung)*. Neue Befunde bekommen die nächste Kennung wie bisher.

**Was bewusst nicht:** keine Fremd-Skills installieren, keine neuen Pakete, keine Formularbibliothek; die Windows-App bekommt im ersten Schritt nur die Durchsicht, keinen eigenen Prüftest.

**Aufwand (Schätzung, nicht gemessen):**

| Teil | Einmalig | Je Runde |
| --- | --- | --- |
| Prüftest `formCoverage.test.ts` mit Ausnahmeliste (die erste Liste erzeugt viele Treffer, die einzeln zu beurteilen sind) | 3 bis 4 Stunden | wenige Minuten Laufzeit, Pflege der Liste |
| Durchsicht der geänderten Masken | 0,5 Stunden für die Vorlage der Tabelle | 1 bis 2 Stunden, je nach Zahl der Masken |
| Satz im Rundenabschluss | 15 Minuten in `work/conventions.md`; der Ablauf selbst (`/slice close`) gehört zum Plugin `vibe-coding` und ist nicht Teil dieser Slice | – |

## 4. Empfehlung

Beide Teile aufnehmen, die Skills nicht. Zuerst Teil 2 für eine Runde ausprobieren (kostet nur Zeit) und am Ende von R04 entscheiden, ob Teil 1 den Aufwand wert ist: Ob der Prüftest mehr findet als die Durchsicht, ist offen, und die Zahl der Treffer der ersten Liste entscheidet das.

Offen für den Auftraggeber: ob der Schritt in `work/conventions.md` und damit in den Rundenabschluss kommt. Die Frage steht in der Rundenakte R03 unter „Offene Entscheidungen“.
