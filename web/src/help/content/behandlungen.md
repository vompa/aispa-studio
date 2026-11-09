---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
---
# Behandlungen

Einstellungen in der Windows-App unter „Verwaltung“ › „Angebot“ › „Behandlungen“: was das Spa anbietet, wie lange es dauert, was es kostet und was dafür gebraucht wird. Die Liste links ist nach Kategorie gruppiert. Alles zu einer Behandlung steht an dieser einen Stelle, gegliedert in sieben Bereiche:

- **Grunddaten**: Name, Dauer, Preis, Kategorie, Gebühren, Hinweis für Gäste.
- **Ablauf**: Vorbereitung und Nachbereitung in Minuten und die Vorbereitungsschritte.
- **Material**: der Verbrauch je Termin.
- **Zum Termin bereitstellen** (in der Windows-App „Bereitstellen“): Getränke, Früchteteller und Extras.
- **Verkauf**: Produkte, die zur Behandlung passen und die Kassa vorschlägt.
- **Qualifikationen, Räume und Geräte** (in der Windows-App „Qualifikationen & Räume“): was für die Behandlung gebraucht wird.
- **Preise**: Preis je Standort und Preise nach Wochentag und Uhrzeit.

Eine neue Behandlung hat zuerst nur Grunddaten, Ablauf (Minuten), Qualifikationen, Räume und Geräte sowie, bei mehreren Standorten, Preise; Material, Bereitstellung, Verkauf, Vorbereitungsschritte und Preisregeln kommen nach dem Anlegen dazu.

Ändern dürfen Inhaber und Standortleitung. Eine Standortleitung ändert Räume, Geräte, Preise und Preisregeln nur an ihren eigenen Standorten; Einträge an anderen Standorten bleiben beim Speichern unverändert.

![Behandlungen in der Windows-App](images/app/verwaltung-behandlungen.png)

In der Windows-App speichert ein **Speichern** unten alles zur offenen Behandlung gemeinsam, aus allen Bereichen:

- **Strg+S** speichert wie der Knopf „Speichern“.
- **Verwerfen** holt den gespeicherten Stand zurück.
- Wer mit ungespeicherten Änderungen eine andere Behandlung wählt oder die Seite verlässt, wird gefragt: „Änderungen speichern?“ mit „Speichern“, „Verwerfen“ und „Abbrechen“. „Abbrechen“ bleibt bei der Behandlung.
- Fehlt der Name oder der Preis, steht der Fehler am Feld und es wird nichts gespeichert.
- Aus dem Kalender führt „Behandlungszeiten bearbeiten …“ am Termin direkt in den Bereich Ablauf der Behandlung.

Änderungen gelten für neue Buchungen; gebuchte Termine behalten Zeiten und Preis.

## Grunddaten {#behandlung}

### Name {#name}

Name der Behandlung, z. B. „Hot-Stone-Massage“. Pflichtfeld. Er erscheint im Kalender und im Kundenbereich.

### Dauer (min) {#dauer}

Wie lange die Behandlung beim Gast dauert. Erlaubt sind 1 bis 600 Minuten, Standard 60. Der Termin im Kalender und im Kundenbereich hat genau diese Länge. Die Behandlung muss ganz in der Öffnungszeit des Standorts liegen. Ein nötiges Gerät ist genau in dieser Zeit belegt.

### Preis {#preis}

Grundpreis in der Währung des Standorts, inklusive Umsatzsteuer. Ab 0, mit Komma oder Punkt und höchstens zwei Nachkommastellen, z. B. „89,90“. Standard ist 0,00. Ein Preis, der fehlt oder sich nicht lesen lässt, bekommt beim Verlassen des Feldes einen Hinweis; **Speichern** bleibt dann grau.

- Er gilt, wenn weder ein Preis je Standort noch eine Preisregel passt.
- Der Preis wird beim Buchen in den Termin übernommen. Spätere Änderungen betreffen nur neue Buchungen.
- Gutscheine für eine Behandlung und die Vollkosten rechnen mit diesem Preis bzw. dem Preis je Standort, nicht mit Preisregeln.

### Kategorie {#kategorie}

Gruppe im Angebot, z. B. „Massagen“ oder „Gesicht“. Freier Text, höchstens 60 Zeichen; bereits vergebene Kategorien werden zur Auswahl vorgeschlagen. Gleiche Schreibweise fasst zusammen. Leer steht die Behandlung unter „Ohne Kategorie“. Die Kategorie gliedert die Liste hier und das Angebot im Kundenbereich.

### Ausfallgebühr (% des Preises) {#ausfallgebuehr}

Eigene Ausfallgebühr nur für diese Behandlung, in Prozent des Terminpreises. Erlaubt sind 0 bis 100.

- Leer (Anzeige „Regel des Standorts“) gilt die Ausfallgebühr aus der Online-Buchung des Standorts.
- 0 heißt: für diese Behandlung nie eine Ausfallgebühr, auch wenn der Standort eine verlangt.
- Ein Wert über 0 gilt statt des Werts des Standorts, z. B. 100 für eine aufwendige Behandlung mit frischen Zutaten.
- Wirkt nur mit eingerichtetem Zahlungsanbieter. Ist am Standort „Ausfallgebühr nur von neuen Gästen verlangen“ gesetzt, gilt das auch hier.

### Anzahlung (% des Preises) {#anzahlung}

Eigene Anzahlung nur für diese Behandlung, in Prozent des Terminpreises. Erlaubt sind 0 bis 100; 100 heißt Vorauszahlung.

- Leer gilt die Anzahlung aus der Online-Buchung des Standorts, 0 heißt: für diese Behandlung nie eine Anzahlung.
- Ein Wert über 0 gilt statt des Werts des Standorts, z. B. 100 für ein Ritual, das vorbereitet werden muss.
- Verlangt die Behandlung eine Anzahlung, braucht sie keine Karte für die Ausfallgebühr. Wirkt nur mit eingerichtetem Zahlungsanbieter und aktiver Kasse am Standort.

### Hinweis für Gäste {#hinweis-fuer-gaeste}

Freier Text für Gäste, die diese Behandlung gebucht haben, z. B. „Bitte 15 Minuten früher kommen, Badekleidung mitbringen“ oder „Bitte vorher keine Sonnencreme“. Höchstens 300 Zeichen. Leer erscheint kein Hinweis.

- Steht in der Bestätigungsmail der Online-Buchung, in der Erinnerungsmail und auf der Terminseite im Kundenbereich.
- In SMS und WhatsApp steht er nicht, damit die Nachricht kurz bleibt.
- Für Hinweise, die für alle Behandlungen eines Standorts gelten, gibt es den „Hinweis für Gäste“ in der Online-Buchung des Standorts.

### Anamnese prüfen {#anamnese-pruefen}

Für Behandlungen, bei denen Angaben aus dem Anamnesebogen wichtig sind, z. B. Hot Stone, Peeling oder Sauna-Ritual. Standard ist aus.

- Bucht die Rezeption diese Behandlung für einen Gast, dessen letzter Bogen Angaben enthält (ein „Ja“ oder Text unter „Sonstiges“), steht im Panel **Neuer Termin** „Anamnese prüfen“.
- Der Hinweis nennt nie, was im Bogen steht, und sperrt die Buchung nicht. Den Bogen liest, wer ihn öffnet.
- Ohne Bogen oder ohne Angaben erscheint nichts.

## Ablauf {#ablauf}

Alles, was um den Termin herum geschieht: die Zeiten davor und danach und die Schritte, die vor jedem Termin zu tun sind. Die Zeiten speichert „Speichern“ unten, die Schritte haben einen eigenen Knopf.

### Vorbereitung (min) {#vorbereitung}

Zeit vor dem Termin, in der die Person und der Raum schon gebunden sind, z. B. zum Vorwärmen der Steine. Erlaubt sind 0 bis 120 Minuten, Standard 5. Die Person muss auch in dieser Zeit im Dienst und frei sein; die Öffnungszeit gilt dafür nicht.

### Nachbereitung (min) {#nachbereitung}

Zeit nach dem Termin, in der Person und Raum noch gebunden sind, z. B. für Notizen oder Aufräumen. Erlaubt sind 0 bis 120 Minuten, Standard 10. Danach bleibt der Raum zusätzlich für die Raumpflege des Raums gesperrt (siehe „Standorte & Räume“).

Vorbereitung, Dauer und Nachbereitung zusammen ergeben die Personalzeit in den Vollkosten je Behandlung.

Vorbereitung, Dauer und Nachbereitung zusammen ergeben die Personalzeit in den Vollkosten je Behandlung; eine Einwirkzeit (siehe unten) zählt dabei nicht mit.

### Am Gast bis Einwirkzeit (min) {#am-gast}

Nur für Behandlungen mit Einwirkzeit, z. B. Körperpackung oder Gesichtsmaske: wie viele Minuten die Person am Gast arbeitet, bevor die Einwirkzeit beginnt (auftragen, einpacken). Erlaubt sind 0 bis 120 Minuten, Standard 0 (keine Einwirkzeit).

- Mit Einwirkzeit muss hier mehr als 0 stehen; ohne Minuten am Gast gibt es keine Einwirkzeit.
- „Am Gast bis Einwirkzeit“ und „Einwirkzeit“ zusammen dürfen die **Dauer** nicht übersteigen. Der Rest der Dauer ist die Ausarbeitung danach (abnehmen, pflegen); sie darf 0 sein. Bei 60 Minuten Dauer, 20 am Gast und 20 Einwirkzeit sind es also 20 Minuten Ausarbeitung.
- Wie bei allen Zeiten gilt die Einstellung für neue Buchungen; gebuchte Termine behalten ihre Minuten.

### Einwirkzeit (min) {#einwirkzeit}

Zeit, in der der Gast allein im Raum liegt und die Person frei ist, z. B. 20 Minuten Reismilch-Packung unter der Wärmefolie. Erlaubt sind 0 bis 120 Minuten, Standard 0 (keine Einwirkzeit).

- Die Person ist in dieser Zeit frei und kann einen anderen Gast behandeln, am Ende der Einwirkzeit aber wieder da sein. Der Planer bucht und findet Zeiten in der Einwirkzeit einer Person ebenso wie sonst freie Zeit.
- Der **Raum** bleibt die ganze Zeit belegt; ein nötiges Gerät ist wie bisher für die Dauer belegt.
- Im Kalender der Windows-App liegt in der Spalte der Person ein flaches Band „Einwirkzeit 20 Min. · frei“ mit feinen Linien oben und unten. Liegt dort ein zweiter Termin, teilt sich die Spalte in zwei Bahnen. In der Raumansicht gibt es kein Band, weil der Raum belegt ist.
- Die Vollkosten rechnen die Personalzeit ohne Einwirkzeit, die Raumzeit mit ihr.
- Die Pausenprüfung und die Lückenvorschläge rechnen vorerst vorsichtig mit der ganzen Bindung der Person, ohne die Einwirkzeit als frei zu zählen.

### Vorbereitungsschritte {#vorbereitungsschritte}

Das Behandlungsrezept: was vor jeder Behandlung dieser Art vorzubereiten ist. Aus diesen Schritten entsteht die Vorbereitungsliste auf jeder Behandlungskarte, zum Beispiel unter „Mein Tag“. Ändern dürfen Inhaber und Standortleitung; sehen und abhaken kann die Schritte auf den Karten das ganze Team. Der Bereich erscheint bei einer gespeicherten Behandlung.

„Schritt hinzufügen“ legt eine Zeile an, „Entfernen“ löscht sie, „Vorbereitung speichern“ speichert die ganze Liste dieser Behandlung in der angezeigten Reihenfolge. Die Schritte gelten an allen Standorten.

#### Schritt {#schritt}

Was zu tun ist, zum Beispiel „Steine auf 55 °C aufheizen“. Pflicht, höchstens 300 Zeichen. Höchstens 30 Schritte je Behandlung.

#### Minuten vorher {#minuten-vorher}

Wie viele Minuten vor Beginn des Termins der Schritt fällig ist, 0 bis 240. Leer heißt: ohne Uhrzeit.

- Die Karte zeigt dann „ab“ mit der Uhrzeit, zum Beispiel bei 30 Minuten und einem Termin um 10:00 „ab 09:30“.

#### Material aus dem Lager {#schritt-material}

Optional: der Lagerartikel, den der Schritt braucht, z. B. „Lavendelöl“. Der Artikel muss unter [Material](hilfe:behandlungen#material) oder bei „Zum Termin bereitstellen“ stehen; die Menge je Termin kommt von dort und wird auch dort abgebucht, der Schritt bucht nichts. Auf der Behandlungskarte steht das Material hinter dem Schritt, z. B. „Öl bereitstellen (Lavendelöl, 3 ml)“. Steht der Artikel nicht beim Material, lehnt das Speichern ihn ab.

#### Abgehakte Schritte {#abgehakt}

Bleibt ein Schritt beim Speichern gleich (gleicher Text, gleiche Minuten und gleiches Material), bleibt er auf den Karten, auf denen er schon abgehakt war, abgehakt. Ein geänderter Schritt gilt als neu und ist überall wieder offen.

## Material {#material}

Welches Material ein Termin dieser Behandlung verbraucht (die Rezeptur). Der Bereich erscheint bei einer gespeicherten Behandlung und hat einen eigenen Knopf; die Vollkosten auf der Seite „Kosten“ rechnen damit. „Material hinzufügen“ legt eine Zeile an, „Entfernen“ löscht sie, „Material speichern“ ersetzt die ganze bisherige Liste. Höchstens 50 Zeilen.

### Aus dem Lager {#aus-dem-lager}

Gibt es im Lager aktives Material, kann man es hier wählen. Dann kommen Bezeichnung, Einheit und Preis je Einheit aus dem Lager, und man trägt nur die **Menge** ein.

- Die Vollkosten rechnen immer mit dem aktuellen Einstandspreis aus dem Lager.
- Jede Behandlung, die auf „abgeschlossen“ gesetzt wird, bucht diese Menge am Standort des Termins ab. Wird der Status wieder zurückgenommen, wird zurückgebucht.
- Das Lager zeigt die Mengen der gebuchten Termine der nächsten 14 Tage als „Verbrauch geplant“. Der Bestellvorschlag rechnet die Termine bis zur Lieferung ein.
- Die Menge muss größer als 0 sein, mit höchstens drei Nachkommastellen.

### Frei erfasst {#frei-erfasst}

Mit „frei erfasst“ trägt man selbst ein:

- **Material**: die Bezeichnung, zum Beispiel „Massageöl“. Pflicht, höchstens 200 Zeichen.
- **Einheit**: zum Beispiel ml oder Stück. Pflicht, höchstens 20 Zeichen.
- **Preis je Einheit**: Einstandspreis netto, nicht negativ.
- **Menge**: Verbrauch je Termin, größer als 0.

Frei erfasstes Material zählt nur für die Vollkosten. Es bucht nichts im Lager ab.

## Zum Termin bereitstellen {#zum-termin-bereitstellen}

Getränke, Früchteteller und Extras, die zu jedem Termin dieser Behandlung gehören. Der Bereich erscheint bei einer gespeicherten Behandlung und gilt an allen Standorten. Je Termin entsteht daraus ein Eintrag mit Uhrzeit, den das Team abhakt, und der Tagesstart zählt die Summen des Tages (z. B. „6 × Früchteteller“). Mit „Bereitstellen speichern“ wird immer die ganze Liste gespeichert; höchstens 20 Einträge.

### Bezeichnung {#extra-bezeichnung}

Was bereitgestellt wird, z. B. „Früchteteller“ oder „Tee“. Pflicht, höchstens 200 Zeichen. Gleich geschriebene Bezeichnungen verschiedener Behandlungen werden im Tagesstart zusammengezählt.

### Zeitpunkt {#extra-zeitpunkt}

Wann es bereitstehen soll: **Bei Ankunft** (Beginn des Termins), **Minuten vor Ende** (dann die Minuten angeben, 0 bis 240) oder **Nach der Behandlung** (Ende des Termins).

### Lagerartikel und Menge je Termin {#extra-lager}

Optional. Mit einem Lagerartikel und einer Menge je Termin bucht jede abgeschlossene Behandlung die Menge im Lager ab, zusammen mit dem [Material](hilfe:behandlungen#material) der Behandlung; beides ergibt eine einzige Abbuchung. Ohne Lagerartikel gibt es keine Menge. Das Abhaken am Termin bucht nichts. Die Bedarfsprüfung des Lagers rechnet die Mengen mit.

## Passt dazu {#passt-dazu}

Produkte aus der Kassa, die zu dieser Behandlung passen, z. B. das Massageöl zur Hot-Stone-Massage. Der Bereich heißt in der Windows-App „Verkauf“ und erscheint bei einer gespeicherten Behandlung. Wählen Sie einen Artikel aus und klicken Sie **Artikel hinzufügen**; höchstens 5 Artikel, jeder einmal. Die Reihenfolge der Liste ist die Reihenfolge der Vorschläge. Gespeichert wird mit der Behandlung.

Nach dem Abschließen des Termins schlagen Termin-Panel und Kassa diese Artikel vor, aber nur, wenn sie am Verkaufsregal des Standorts liegen. Gibt es kein Verkaufsregal, zählt das Hauptlager, wie beim Abbuchen an der Kassa. Ein Artikel ohne Material im Lager wird nie vorgeschlagen, weil sein Bestand unbekannt ist. Eine Provision auf den Verkauf gibt es nicht.

Ändern dürfen Inhaber und Standortleitung.

![Bereich Verkauf einer Behandlung](images/app/verwaltung-behandlungen-verkauf.png)

## Nötige Qualifikationen {#noetige-qualifikationen}

Häkchen bei allen Qualifikationen, die eine Person für diese Behandlung braucht. Gebucht wird nur eine Person, die alle gewählten hat. Ohne Häkchen kann jede Person im Dienst die Behandlung geben. Die Qualifikationen selbst werden unter „Verwaltung“ › „Team“, Reiter „Qualifikationen“ angelegt und unter „Team“ den Personen gegeben.

In der Windows-App fragt das erste „Speichern“ ohne Häkchen nach: Am Feld steht „Keine Qualifikation gewählt: Jede Person im Team kann diese Behandlung übernehmen.“ Ein zweites „Speichern“ speichert ohne Qualifikation, z. B. für ein Fußbad, das alle geben dürfen.

### Wer kann das? {#wer-kann-das}

In der Windows-App stehen unter den Qualifikationen die Personen, die alle gewählten Qualifikationen haben. Ein Klick auf einen Namen öffnet die Person im Bereich „Team“; dort ändert man ihre Qualifikationen. Steht dort „Niemand im Team hat alle gewählten Qualifikationen.“, lässt sich die Behandlung nicht buchen, bis jemand sie bekommt.

## Räume und Geräte je Standort {#raeume-und-geraete}

Je Standort ein Kasten mit dem Namen des Standorts.

- **Geeignete Räume**: in welchen Räumen die Behandlung stattfinden kann. Beim Buchen wird einer davon belegt, der zu dieser Zeit frei ist. Bereiche wie Sauna oder Ruheraum sind hier nicht wählbar. Mindestens ein Raum muss gewählt sein, sonst lässt sich nicht speichern.
- **Braucht eines dieser Geräte**: erscheint nur, wenn der Standort Geräte hat. Sind Geräte angehakt, braucht die Behandlung eines davon, und es muss während der Behandlung frei sein. Ohne Häkchen wird kein Gerät gebraucht.

Angeboten wird die Behandlung an allen Standorten, an denen ein geeigneter Raum gewählt ist. Online buchbar ist sie dort zusätzlich nur, wenn mindestens eine Person mit Wochenarbeitszeit an diesem Standort alle nötigen Qualifikationen hat.

## Preis je Standort {#preis-je-standort}

Erscheint im Bereich Preise, nur bei mehr als einem Standort. Je Standort kann ein abweichender Preis stehen, z. B. höher in der Innenstadt. Ab 0, höchstens zwei Nachkommastellen.

- Leer gilt der Grundpreis. Ein Preis gleich dem Grundpreis wird nicht als Abweichung gespeichert.
- Gebuchte Termine behalten ihren Preis.

## Preise nach Wochentag und Uhrzeit {#preise-nach-zeit}

Erscheint im Bereich Preise, sobald die Behandlung angelegt ist. Hier lassen sich Preise für bestimmte Zeiten festlegen, z. B. günstiger am Vormittag oder teurer am Wochenende. Höchstens 20 Regeln je Behandlung.

Wichtig: Preisregeln haben einen eigenen Knopf „Preisregeln speichern“. Der Knopf „Speichern“ der Behandlung speichert sie nicht mit.

So wird der Preis eines Termins bestimmt:

1. Maßgeblich ist der Beginn des Termins in der Ortszeit des Standorts.
2. Zuerst gelten passende Regeln dieses Standorts, dann passende Regeln für „Alle Standorte“, jeweils in der Reihenfolge der Liste. Die erste passende Regel gilt.
3. Passt keine Regel, gilt der Preis je Standort bzw. der Grundpreis.

Der Kundenbereich zeigt bei Behandlungen mit Preisregeln den Preis zu jeder angebotenen Startzeit. Ein gebuchter Termin behält seinen Preis, auch wenn er später verschoben wird.

„Preisregel hinzufügen“ fügt eine Regel für Montag bis Freitag, 09:00 bis 12:00 Uhr, mit dem Grundpreis hinzu. „Entfernen“ löscht eine Regel; wirksam wird das erst mit „Preisregeln speichern“.

### Bezeichnung {#preisregel-bezeichnung}

Name der Regel, z. B. „Vormittag“ oder „Wochenende“. Pflichtfeld, höchstens 100 Zeichen.

### Von und Bis {#preisregel-zeit}

Die Regel gilt für Termine, die ab „Von“ und vor „Bis“ beginnen. Ein Termin, der genau zur Bis-Zeit beginnt, fällt nicht mehr darunter. „Von“ muss vor „Bis“ liegen.

### Preis {#preisregel-preis}

Preis für Termine, auf die die Regel passt. Ab 0 und unter 100.000, mit Komma oder Punkt und höchstens zwei Nachkommastellen. Ein Preis, der fehlt oder sich nicht lesen lässt, bekommt beim Verlassen des Feldes einen Hinweis; **Preisregeln speichern** bleibt dann grau.

### Standort {#preisregel-standort}

Erscheint nur bei mehr als einem Standort. „Alle Standorte“ (Standard) oder ein bestimmter Standort. Regeln eines Standorts gehen Regeln für alle Standorte vor.

### Wochentage {#preisregel-wochentage}

Die Kästchen Mo bis So. Mindestens ein Tag muss gewählt sein.

## Nicht mehr anbieten {#nicht-mehr-anbieten}

Nimmt die Behandlung aus dem Angebot: Sie lässt sich nicht mehr neu buchen und verschwindet aus dem Kundenbereich. Bereits gebuchte Termine bleiben bestehen. In der Windows-App fragt der Knopf **Nicht mehr anbieten** vorher nach.

## Verlauf {#verlauf}

Der Abschnitt **Verlauf** am Ende einer bestehenden Behandlung zeigt, wer sie und ihre Preise wann geändert hat: [Verlauf einer Behandlung](hilfe:behandlungen-verlauf).
