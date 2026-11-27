---
art: erklaerung
bereich: Kosten
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
stichwoerter: wie gerechnet, Berechnung, Formel, Vollkosten, Marge, Material, Personal, Raum, Raumpflege, Gemeinkosten, Mietanteil, Stundensatz, Öffnungsminuten, how costs are calculated, calculation
---
# Wie die Vollkosten gerechnet werden

Hier steht, wie die Vollkosten je Behandlung gerechnet werden; die Tabelle selbst beschreibt [Vollkosten pro Behandlung](hilfe:kosten#berechnung).

## So wird gerechnet {#rechnung}

Die Vollkosten je Termin sind die Summe aus fünf Teilen, jeder auf Cent gerundet:

1. **Material**: Menge mal Preis je Einheit aller Zeilen im Bereich „Material“ der Behandlung (Stammdaten; in der Windows-App die Gruppe „Material je Termin“ im Bereich „Durchführung“).
2. **Personal**: Vorbereitung, Dauer und Nachbereitung der Behandlung (in Minuten, aus den Stammdaten unter „Behandlungen“) zum Stundensatz, geteilt durch die „Produktive Arbeitszeit (%)“. Eine Einwirkzeit der Behandlung (Person frei, Raum belegt) zählt nicht als Personalzeit.
3. **Raum**: die Raumminuten zum Mietanteil je Minute. Raumminuten sind Vorbereitung, Dauer, Nachbereitung und die Raumpflege des Raums; eine Einwirkzeit zählt voll mit, weil der Raum belegt bleibt.
4. **Raumpflege**: die Minuten für die Raumpflege zum Satz „Raumpflege pro Stunde“ plus das „Pflegematerial je Reinigung“ des Raums.
5. **Gemeinkosten**: die Raumminuten zum Gemeinkostensatz je Minute.

So kommen die Sätze zustande:

- **Öffnungsminuten**: für jeden Kalendertag des gewählten Monats die Öffnungszeiten seines Wochentags, zusammengezählt. Die Zahl steht über der Tabelle als „Öffnungsstunden je Raum im Monat“.
- **Nutzbare Minuten je Raum**: Öffnungsminuten mal „Geplante Raumauslastung (%)“.
- **Mietanteil je Minute**: „Miete und Nebenkosten pro Monat“ geteilt durch die nutzbaren Minuten. Passt eine Behandlung in mehrere Räume, gilt der Durchschnitt dieser Räume. Gleiches gilt für Raumpflege-Minuten und Pflegematerial.
- **Gemeinkostensatz je Minute**: „Gemeinkosten pro Monat“ geteilt durch die nutzbaren Minuten aller aktiven Räume des Standorts zusammen.
- **Stundensatz Personal**: der Durchschnitt der Stundensätze aller aktiven Personen, die am Standort Arbeitszeiten haben und alle Qualifikationen der Behandlung mitbringen. Wer keinen eigenen Satz hat, zählt mit dem „Standard-Stundensatz Personal“. Passt niemand, gilt der Standardsatz.
