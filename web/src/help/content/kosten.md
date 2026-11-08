---
art: einstellung
bereich: Kosten
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
---
# Kosten

Die Seite „Kosten“ zeigt, was eine Behandlung an einem Standort wirklich kostet, und darunter die Kostensätze, aus denen sich diese Vollkosten ergeben. Sehen dürfen die Seite nur Personen mit dem Recht „Berichte mit Beträgen“ ([Berichte mit Beträgen](hilfe:zugaenge#berichte-mit-betraegen)), weil Beträge und Stundensätze vertraulich sind: Inhaber, Manager und Backoffice immer, Standortleitung und Rezeption nur, wenn es ihnen vergeben ist. Ändern dürfen Inhaber, Manager und Standortleitung; alle anderen lesen nur. Alle Beträge sind netto, ohne Umsatzsteuer.

## Vollkosten pro Behandlung {#berechnung}

Oben wählt man **Standort** und **Monat**. Die Tabelle zeigt jede aktive Behandlung, die an diesem Standort in mindestens einem aktiven Raum stattfinden kann. Ein Klick auf den Namen der Behandlung klappt die Rechnung Schritt für Schritt auf; darunter führt **Material pflegen** zum Material in den Stammdaten.

Die Vollkosten je Termin sind die Summe aus fünf Teilen, jeder auf Cent gerundet:

1. **Material**: Menge mal Preis je Einheit aller Zeilen im Bereich „Material“ der Behandlung (Stammdaten).
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

Die übrigen Spalten:

- **Preis netto**: der Preis der Behandlung an diesem Standort ohne Umsatzsteuer. Das Land für den Steuersatz kommt von der ersten aktiven Kassa des Standorts, ohne Kassa gilt Österreich.
- **Marge**: Preis netto minus Vollkosten, dahinter in Prozent vom Nettopreis. Ein negativer Wert ist hervorgehoben.
- **Termine**: Termine dieser Behandlung im gewählten Monat, ohne stornierte und nicht erschienene. Auch noch kommende Termine des Monats zählen mit.
- **Deckungsbeitrag**: für jeden dieser Termine der tatsächlich gebuchte Nettopreis minus Vollkosten, zusammengezählt.

Fehlt eine Angabe, steht unter dem Namen der Behandlung ein Hinweis, zum Beispiel „Kein Material erfasst.“, „Kein Stundensatz für das Personal.“, „Keine Öffnungszeiten im Monat.“, „Mietanteil fehlt für: …“ oder „Kein Stundensatz für die Raumpflege.“. Der betreffende Teil wird dann mit 0 gerechnet.

Die Vollkostenrechnung ist eine Auswertung für die Leitung. Sie ändert keine Preise, keine Belege und keine Buchhaltung.

## Kostensätze des Standorts {#kostensaetze}

Diese Sätze gelten nur für den oben gewählten Standort und wirken sofort auf die Vollkosten aller Behandlungen dort. Gespeichert wird mit **Speichern** (Strg+S). Beträge sind in Euro netto, ab 0 und mit höchstens zwei Nachkommastellen; ein leeres Feld zählt als 0.

### Raumpflege pro Stunde {#raumpflege-pro-stunde}

Was eine Stunde Reinigung kostet, also die Arbeitgeberkosten der Person, die den Raum zwischen zwei Terminen herrichtet. Standard ist 0.

- Gerechnet wird mit der Raumpflege-Zeit des Raums („Raumpflege in Minuten“ unter „Verwaltung“ › „Betrieb“ › „Standorte & Räume“).
- Bleibt der Satz 0, obwohl Räume eine Raumpflege-Zeit haben, erscheint der Hinweis „Kein Stundensatz für die Raumpflege.“.

### Standard-Stundensatz Personal {#standard-stundensatz}

Arbeitgeberkosten pro bezahlter Stunde für alle Personen, die unter „Personal“ keinen eigenen Satz haben. Standard ist 0.

- Ist weder ein eigener noch ein Standardsatz da, erscheint „Kein Stundensatz für das Personal.“.

### Gemeinkosten pro Monat {#gemeinkosten}

Alle monatlichen Kosten, die keinem Raum direkt gehören, zum Beispiel Rezeption, Energie, Versicherung oder Software. Standard ist 0.

- Sie werden wie die Miete auf die nutzbaren Raumminuten verteilt, hier aber auf alle aktiven Räume des Standorts zusammen. Eine Behandlung trägt also umso mehr Gemeinkosten, je länger sie einen Raum belegt.

### Geplante Raumauslastung (%) {#raumauslastung}

Welcher Anteil der Öffnungszeit ein Raum im Schnitt wirklich belegt ist. Erlaubt sind 1 bis 100, Standard ist 70.

- Je niedriger die Auslastung, desto weniger Minuten tragen Miete und Gemeinkosten, und desto höher wird der Raum- und Gemeinkostenanteil je Termin.
- Die Zahl ist eine Planannahme. Die Software misst sie hier nicht nach.

### Produktive Arbeitszeit (%) {#produktive-arbeitszeit}

Welcher Anteil der bezahlten Arbeitszeit für Behandlungen genutzt wird; der Rest sind Pausen ohne Gast, Leerlauf, Besprechungen und Ähnliches. Erlaubt sind 1 bis 100, Standard ist 75.

- Die Personalkosten einer Behandlung werden durch diesen Anteil geteilt. Bei 75 % kostet eine Stunde Behandlung also so viel wie 1,33 bezahlte Stunden.

## Räume {#raeume}

Eine Zeile je aktivem Raum des Standorts. Jede Zeile wird mit ihrem eigenen „Speichern“ gespeichert. Beträge in Euro netto, ab 0, höchstens zwei Nachkommastellen.

### Miete und Nebenkosten pro Monat {#miete}

Der Anteil an Miete und Betriebskosten, der auf diesen Raum entfällt, pro Monat. Standard ist 0.

- Daraus wird der Mietanteil je Minute (siehe „Vollkosten pro Behandlung“). Bleibt der Betrag 0, nennt die Tabelle den Raum unter „Mietanteil fehlt für:“.

### Pflegematerial je Reinigung {#pflegematerial}

Was eine Reinigung des Raums an Material kostet, zum Beispiel frische Wäsche, Reinigungsmittel, Einwegauflagen. Standard ist 0.

- Der Betrag kommt bei jeder Behandlung in diesem Raum einmal zur Raumpflege dazu.

## Personal {#personal}

Eine Zeile je aktiver Person mit Arbeitszeiten an diesem Standort. Eingetragen werden die Arbeitgeberkosten pro bezahlter Stunde, also Bruttolohn samt Lohnnebenkosten, in Euro mit höchstens zwei Nachkommastellen.

### Stundensatz einer Person {#stundensatz-person}

- Ein eigener Satz gilt für diese Person an allen Standorten.
- Ein leeres Feld mit „Speichern“ entfernt den eigenen Satz; dann gilt der „Standard-Stundensatz Personal“ des jeweiligen Standorts.
- Die Sätze sind nur hier zu sehen. Sie fließen in die Vollkosten ein, aber nicht in die Lohnverrechnung.

## Material je Behandlung {#material}

Das Material je Termin einer Behandlung wird nicht hier, sondern bei der Behandlung gepflegt: **Verwaltung** › **Angebot** › **Behandlungen**, Bereich **Material** (in der Tabelle öffnet **Material pflegen** diese Stelle). Die Vollkosten rechnen mit dieser Liste. Alle Angaben dazu stehen unter [Behandlungen, Material](hilfe:behandlungen#material).
