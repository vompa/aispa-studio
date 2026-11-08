# work/ – Archiv des vibe-coding-Ablaufs

Dieser Ordner enthält Slice-Pläne, Rundenakten (R01 bis R07), Checkliste und Konventionen aus der Zeit, in der das Projekt mit dem Plugin vibe-coding gearbeitet hat (`/slice`, Slice-Schleife, Merge-Warteschlange).

Seit dem 03.11.2026 wird mit Code Guardian gearbeitet: Ablauf in [docs/arbeitsweise.md](../docs/arbeitsweise.md) Abschnitt 9, Entscheidung in [ADR 0086](../docs/entscheidungen/0086-code-guardian-statt-vibe-coding.md).

- Bis zum Rundenabschluss von R07 am 04.11.2026 kamen nur noch die Handtestblöcke der Slices 138 bis 147 in die Manuelle Testliste von [rounds/R07-reception-daily-layout.md](rounds/R07-reception-daily-layout.md). Seitdem ist der Ordner eingefroren; neue Handtestblöcke stehen in [docs/umsetzung/handtests.md](../docs/umsetzung/handtests.md).
- Die Slice-Dateien 139 bis 147 in `slices/` waren die Detaildateien der Code-Guardian-Aufgaben T-001 bis T-009. Ihr Feld „Prüfstufe“ galt dabei nicht mehr.
- Was hier über Prüfstufen, Merge-Warteschlange, `.slice-loop/` und `/slice` steht (`conventions.md`, `session-hints.md`, `checklist.md`, `_slices.json`), gilt nicht mehr. Die Dateien bleiben unverändert, weil die Doku und `web/src/findingsMarked.test.ts` sie als Quelle lesen.
