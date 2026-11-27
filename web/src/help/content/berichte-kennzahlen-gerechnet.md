---
art: erklaerung
bereich: Berichte
oberflaeche: verwaltung
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: wie gerechnet, Berechnung, Formel, Definition, Kennzahl erklärt, Umsatz netto, Durchschnittsbon, Deckungsbeitrag, Auslastung, Stammgäste-Anteil, No-Show-Quote, Storno-Quote, entgangen, how figures are calculated, definition, calculation
---
# Wie die Kennzahlen gerechnet werden

Hier steht, wie die Kennzahlen im Management gerechnet werden; die Bedienung beschreibt [Kennzahlen im Management ansehen](hilfe:berichte-kennzahlen-ansehen).

## So wird gerechnet {#rechnung}

Alle Kennzahlen gelten für den gewählten Standort und Zeitraum. Der Vergleichszeitraum wird genauso gerechnet. Ein **Termin** heißt hier ein Termin, der im Zeitraum beginnt. Umsätze kommen aus den Kassenbelegen des Standorts, Stunden aus Kalender und Dienstplan. Netto heißt ohne Umsatzsteuer.

### Umsatz und Ertrag {#ertrag}

- **Umsatz brutto:** Summe aller Belegzeilen der Kassen des Standorts mit Belegdatum im Zeitraum, mit Umsatzsteuer. Ein Storno zieht den Betrag wieder ab.
- **Umsatz netto:** dieselbe Summe ohne Umsatzsteuer, je Steuersatz herausgerechnet.
- **Durchschnittsbon netto:** Umsatz netto geteilt durch die Zahl der Bons (Verkaufsbelege minus Stornobelege).
- **Umsatz ohne Gutscheinverkauf (netto):** Umsatz netto ohne die Zeilen, die Wertgutscheine verkaufen.
- **Durchschnittsbon ohne Gutscheinverkauf (netto):** dieser Umsatz geteilt durch die Bons, die nicht nur Wertgutscheine enthalten.
- **Gutscheine verkauft** und **Gutscheine eingelöst:** siehe [Gutscheine in den Kennzahlen](hilfe:berichte-kennzahlen-ansehen#gutscheine).
- **Deckungsbeitrag:** je Termin der kassierte Betrag netto minus die Vollkosten der Behandlung in seinem Monat, zusammengezählt. Termine ohne gerechnete Vollkosten zählen nicht mit. **Deckungsbeitrag in Prozent** setzt ihn ins Verhältnis zum Umsatz derselben Termine.
- **Umsatz je Dienststunde:** Umsatz ohne Gutscheinverkauf (netto) geteilt durch die geplanten Dienststunden am Standort. **Umsatz je Behandlungsstunde:** derselbe Umsatz geteilt durch die Stunden der Behandlungen.
- **Produktanteil am Umsatz:** Anteil der verkauften Artikel am Umsatz ohne Gutscheinverkauf. **Produktumsatz je Behandlung:** Artikelumsatz netto geteilt durch die Behandlungen. **Umsatz je Gast:** Umsatz ohne Gutscheinverkauf geteilt durch die Gäste.
- **Offene Gutscheine (heute):** Restwert aller aktiven Gutscheine, nicht je Standort. Hat der Betrieb mehr als einen Standort, steht auf der Kachel „ganzer Betrieb“, auch im Bericht eines einzelnen Standorts; in PDF, Word und Excel steht der Zusatz in Klammern hinter dem Namen. Die Kachel zeigt den Stand von heute und hat keinen Vergleich.

### Auslastung {#auslastung}

- **Behandlungen:** Zahl der Termine im Zeitraum ohne No-Shows und ohne stornierte Termine.
- **Auslastung Personal:** Minuten dieser Termine geteilt durch die geplanten Dienstminuten am Standort, in Prozent.
- **Auslastung Räume:** belegte Raumminuten dieser Termine, mit Vor- und Nachbereitung, geteilt durch die Öffnungsminuten mal die Zahl der aktiven Räume, in Prozent.
- **Offene Anfragen (heute):** Mails der Terminannahme, über die die Rezeption noch entscheiden muss. Stand von heute, ohne Vergleich.

### Gäste {#gaeste-rechnung}

- **Gäste:** Zahl der verschiedenen Gäste mit mindestens einem Termin, der stattfindet oder stattfand.
- **Neue Gäste:** Gäste, deren erster Termin überhaupt (nicht storniert, kein No-Show) im Zeitraum liegt.
- **Stammgäste-Anteil:** Gäste ohne die neuen Gäste, geteilt durch alle Gäste, in Prozent.
- **Folgetermin gebucht:** Anteil der Gäste mit abgeschlossener Behandlung im Zeitraum, die einen späteren Termin nach dem Zeitraum haben. Es zählt jeder spätere Termin, der nicht storniert und kein No-Show ist, auch wenn er inzwischen stattgefunden hat.
- **Abwandernde Stammgäste (heute):** siehe [Gäste](hilfe:berichte-kennzahlen-ansehen#gaeste). Stand von heute, ohne Vergleich.

### Ausfälle {#ausfaelle}

- **No-Show-Quote:** No-Shows geteilt durch alle Termine des Zeitraums (stattfindende, No-Shows und stornierte), in Prozent. **Storno-Quote:** stornierte Termine auf dieselbe Weise.
- **Entgangen durch No-Show** und **Entgangen durch Storno:** Summe der Terminpreise (brutto) der Termine, zu denen der Gast nicht kam bzw. die storniert wurden. Weniger ist hier besser. Eine Ausfallgebühr ist nicht abgezogen.
