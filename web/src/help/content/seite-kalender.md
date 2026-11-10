---
art: seite
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, therapeut, leitung, inhaber, manager
seite: kalender
stichwoerter: Geburtstag, Geschenk-Symbol, Geburtstag heute, Kalender, Tageskalender, Terminkalender, Terminbuch, Tagesansicht, Planer, Übersicht, Termine, Spalten, Team, Räume, Tagesüberblick, Auslastung, als Nächstes, calendar, schedule, appointment book, day view
---
# Kalender

Der Kalender zeigt einen Tag an einem Standort: je Person eine Spalte (oder je Raum), darin die Termine mit Uhrzeit, Gast und Behandlung. Hier wird gebucht, verschoben, eingecheckt und an die Kassa übergeben.

![Kalender mit geöffnetem Termin](images/app/kalender.png)

## Wer die Seite sieht {#wer}

- Alle Rollen sehen den ganzen Tag des Standorts, auch Therapeut:innen.
- Buchen, Verschieben, Stornieren, Pausen, Sperrzeiten und Notizen gibt es nur mit dem Recht zu buchen (Rezeption, Standortleitung, Inhaber). Ohne dieses Recht fehlen **Termin**, die Einträge im Rechtsklick-Menü und die Tasten dafür.
- Therapeut:innen setzen den Status nur bei ihren eigenen Terminen (**Einchecken**, **Erledigt**, **Nicht erschienen**).
- **Umsatz laut Kalender** im Tagesüberblick sieht nur, wer Umsätze sehen darf (Standortleitung, Inhaber).
- Krank melden darf nur, wer den Standort verwaltet (Standortleitung, Inhaber).

## Kopfzeile {#kopfzeile}

Die Kopfzeile hat **zwei Zeilen**, nie eine dritte. Alle Schaltflächen außer **Termin** haben dieselbe Form: eine Kapsel mit Symbol und Text (ein **Chip**); Zustände zeigen nur die weiche Fläche (blaugrün für „eingeschaltet“, orange für Warnungen, rot für Fehler).

**Zeile 1**

- Ganz links **Heute**, Pfeil links (**Vortag**) und Pfeil rechts (**Nächster Tag**). Sie stehen immer an derselben Stelle, auch wenn der Tagestitel daneben wechselt.
- **Tag und Datum** danach („Heute“ bzw. der Wochentag, darunter das Datum). Bei mehreren Standorten steht der Name dahinter und wird bei Platzmangel gekürzt (der volle Name steht im Tooltip). Ein Klick öffnet den Datumswähler; gibt es mehrere Standorte, steht darüber **Standort** zur Auswahl. Ohne Verbindung steht darunter „Ohne Verbindung · Stand …“.
- Rechts das Suchfeld **Suchen (Strg+F)**, siehe [Termin finden](hilfe:kalender-termin-finden), der Pfeilkreis **Aktualisieren (F5)**, der Chip **Meldungen**, **Termin** und **Freie Zeit suchen**.
- **Termin** (normaler Knopf, öffnet sofort **Neuer Termin**) mit Pfeil daneben für das Menü: **Termin**, **Freie Zeit suchen**, **Wunsch eintippen (KI)** und, durch einen Trenner abgesetzt, **Paar oder Spa-Tag**, **Sauna-Eintritt**, **Warteliste ansehen**, **Lücken schließen**, **Tagesnotiz**.
- **Freie Zeit suchen** (Symbol Kalender mit Lupe, die zweite Schaltfläche direkt rechts neben **Termin**, Strg+N): öffnet die [Suche nach freier Zeit](hilfe:kalender-freie-zeit-suchen).

**Zeile 2**

- **Team** (Symbol Personen) und **Räume** (Symbol Tür) schalten zwischen Spalten je Person und je Raum um, siehe [Raumansicht](hilfe:kalender-raumansicht). Es sind zwei Chips nebeneinander, der gewählte ist blaugrün hinterlegt und fett.
- **Tagesüberblick** (Chip mit z. B. „17 Termine · 2 da · 1 offen“): öffnet die Zahlen **Termine**, **Eingecheckt**, **Noch erwartet** und **Auslastung Team**, sobald Gäste über den Link zusagen auch **Noch nicht zugesagt**, bei begonnenen, nicht eingecheckten Terminen **Überfällig** (in Orange, nur wenn es welche gibt), dazu **Als Nächstes** mit den nächsten Gästen. Hat ein Gast mit Termin am Tag Geburtstag, steht darüber die Zeile „Geburtstag heute: Brigitte Müller (10:30)“ ([Geburtstag](hilfe:seite-kalender#geburtstag)). Ein Klick auf eine Zahl hebt die passenden Termine im Kalender hervor, ein Klick auf **Auslastung Team** öffnet [Freie Zeit suchen](hilfe:kalender-freie-zeit-suchen).
- Bereichs-Chip, z. B. „Sauna 10 / 12“, nur an Standorten mit Sauna oder Ruheraum: öffnet [Sauna und Ruheraum](hilfe:kalender-sauna-eintritt).
- **Warteliste** mit Anzahl, nur wenn jemand wartet (orange): öffnet die [Warteliste](hilfe:kalender-warteliste).
- **Storniert** mit Anzahl, nur wenn am angezeigten Tag Termine storniert sind (im schmalen Fenster nur die Zahl): öffnet die Seite [Verlauf](hilfe:seite-verlauf) für diesen Tag mit dem Filter „Stornos“.
- Das Etikett **Lena Huber · 7 Tage** mit **Filter aufheben** erscheint nur in der [7-Tage-Ansicht](hilfe:kalender-sieben-tage).
- **Tag einpassen** erscheint, sobald gezoomt wurde, und zeigt den ganzen Tag wieder ohne Scrollen. Zoomen geht mit Strg + Mausrad oder Strg + / −.
- Rechts auf derselben Linie **Alle zeigen (2)** (nur in der Teamansicht): zeigt zusätzlich Mitarbeitende ohne Dienst an diesem Standort; die Zahl nennt, wie viele es sind. Danach heißt der Chip **Nur mit Dienst** und ist farbig hinterlegt. Gibt es an diesem Tag niemanden, der sonst fehlen würde, erscheint er nicht; in der Raumansicht steht rechts nichts. Siehe [Alle Mitarbeitenden](hilfe:seite-kalender#alle-mitarbeitende). Jeder Arbeitsplatz merkt sich die Einstellung für sich.

**Wird es schmal** (der Kalender ist weniger als 1100 Pixel breit, zum Beispiel mit geöffneter Seitenleiste), zeigen die Chips nur Symbol und Zahl („24“, „10 / 12“, „4“); der Text steht im Hinweis beim Daraufzeigen. Reicht auch das nicht, scrollt die zweite Zeile waagrecht; es bricht nie etwas in eine dritte Zeile um.

![Nur die noch erwarteten Gäste hervorgehoben](images/app/kalender-noch-erwartet.png)

## Meldungen und Notizen {#tagesleiste}

![Kopfzeile mit der Schaltfläche Meldungen und dem Notiz-Punkt](images/app/kalender-meldungen.png)

Über den Spalten gibt es keine gelben oder roten Bänder mehr. Was Aufmerksamkeit braucht, steht in der Seitenleiste **Meldungen**:

- Der Chip **Meldungen** steht in der ersten Zeile der Kopfzeile links neben **Termin** und zeigt die Zahl der offenen Meldungen („Meldungen 2“): orange bei Warnungen, rot bei Fehlern, sonst wie die anderen Chips. Gibt es Tagesnotizen, trägt er daneben einen kleinen Notiz-Punkt; die Zahl zählt nur Meldungen. Ein Klick oder die Taste **M** öffnet die Seitenleiste.
- Im Kopf einer Person oder eines Raums steht höchstens ein kleines Symbol (Dreieck bei Warnungen, Kreis bei Fehlern), dessen Hinweis kurz sagt, was los ist. Ein Klick öffnet die Meldungen nur für diese Spalte. „3 Termine umbuchen“ bei einer Krankmeldung bleibt im Kopf.
- **Notizen heute**: die Tagesnotizen stehen ganz oben in der Seitenleiste **Meldungen** (Kästchen zum Abhaken, **+ Notiz** für eine neue, die Taste **.**). Wer heute nicht da ist, zeigen die Spalten mit **Alle Mitarbeitenden**. Mehr unter [Pausen, Sperrzeiten und Notizen](hilfe:kalender-pausen-und-notizen).

Gruppen, Karten, **Wegklicken**, die Detailansicht und **Was tun?** beschreibt [Meldungen](hilfe:kalender-meldungen); Tagesstart und Raumpflege stehen unter [Betrieb im Kalender](hilfe:kalender-betrieb).

Unten im Kalender bestätigt eine kurze Meldung, was eben geschehen ist („Termin verschoben auf …“). Sie hat eine Zeile und verschwindet nach fünf Sekunden; **Rückgängig** nimmt eine Verschiebung zurück, dann bleibt sie acht Sekunden. Fehler bleiben stehen, bis man sie schließt.

## Spalten und Termine {#spalten}

- Der Spaltenkopf hat bei Personen und Räumen genau **zwei Zeilen**: oben der Name (zu langer Name endet mit „…“, der volle Name steht im Hinweis), darunter bei Personen die Dienstzeit, bei Abwesenden z. B. „krank gemeldet“ oder „Urlaub bis 23.10.“, bei Räumen „Raumpflege 10 Min. · 13 Behandlungen“. Bei Krankmeldung steht in der zweiten Zeile statt dessen „3 Termine umbuchen“. Ein Paarraum trägt das Symbol zweier Personen statt der Tür, ein Etikett gibt es nicht. Rechts im Kopf steht höchstens ein Warn-Symbol (Dreieck oder Kreis). Ein Klick auf den Kopf zeigt die Termine der Person [über sieben Tage](hilfe:kalender-sieben-tage).
- **Hinweis am Kopf:** wer kurz mit der Maus auf dem Kopf bleibt, sieht höchstens fünf kurze Zeilen mit je einem Symbol. Bei Räumen: die Art („Einzelraum“, „Paarraum · 2 Plätze“), die Raumpflege, die Zahl der Termine heute und die Warnung; bei Personen: die Dienstzeit, bei Dienst an einem anderen Standort der Ort, die Sprachen (nur wenn eingetragen) und die Warnung. Die letzte Zeile sagt, was ein Klick tut. Eine Aufzählung der Behandlungen gibt es dort nicht; die steht in den [Raum-Infos](hilfe:kalender-raumansicht#raum-infos).
- Außerhalb der Dienst- bzw. Öffnungszeit ist die Spalte schraffiert; Mitarbeitende ohne Dienst zeigt der Kalender nur mit [Alle zeigen](hilfe:seite-kalender#alle-mitarbeitende). Die rote Linie mit Uhrzeit zeigt am heutigen Tag, wie spät es ist.
- Ein Termin zeigt Gast, Uhrzeit und Behandlung, darunter in der dritten Zeile zuerst, was zu tun ist: „überfällig 10 Min.“, „umbuchen“, der Status, wenn er nicht mehr „gebucht“ ist (z. B. „Eingecheckt“), „kommt 10:15“, „✓ zugesagt“ oder „vorgemerkt“, danach die Uhrzeit. Schmale Termine kürzen die Zeile von hinten, das Wichtige bleibt lesbar. Begonnene, aber nicht eingecheckte Termine sind orange. „vorgemerkt“ heißt: ohne Verbindung eingetragen, der Server hat es noch nicht bestätigt.
- Kennzeichen am Termin: **!** Hinweis aus der Kundenkarte oder mehrmals nicht erschienen, **N** Erstbesuch, **★** Stammgast (ab drei Besuchen in zwölf Monaten). Aus den [Kennzeichen der Kundenkarte](hilfe:kunden-kennzeichen): **+10** braucht mehr Zeit, **L** Leitung informieren, **STOP** nicht mehr buchen. Ein Sprachkürzel wie **IT** oder **EN** zeigt die [Sprache des Gastes](hilfe:sprachen-und-quelle), wenn sie nicht Deutsch ist.
- Hat nicht das Team gebucht, steht die Quelle in der dritten Zeile, z. B. „Online“, „per Mail“ oder „Treatwell“. Bei Treatwell-Terminen nennt die Hover-Karte auch die Zahlung („Vorab über Treatwell bezahlt: 35,00 €“ oder „Zahlt vor Ort“) und, solange es etwas zu tun gibt, orange „In Treatwell noch annehmen“ bzw. „Zuordnung offen“. Siehe [Treatwell-Buchung im Posteingang](hilfe:anfragen-treatwell-buchung).
- Hinter dem Namen steht klein **♀** oder **♂**, wenn in der Kundenkarte die [Anrede](hilfe:kunden-name-und-anrede#anrede) Frau oder Herr ist. Das Zeichen hat in der Karte einen festen Platz vor den Kennzeichen und bleibt auch dann stehen, wenn ein langer Name gekürzt wird. Der Name ist der [Rufname im Haus](hilfe:kunden-name-und-anrede#rufname), wenn einer eingetragen ist.
- Paar und Spa-Tag tragen ein farbiges Etikett, z. B. „Paar mit Jakob Weiss“ oder „Spa-Tag 1/3“.
- Streifen über und unter einem Termin zeigen Vor- und Nachbereitung, in der Raumansicht dazu die Raumpflege (mit der Nachbereitung in einem Streifen). Sie haben einen grauen Hintergrund mit Rand und sind mindestens 6 Pixel hoch, auch wenn der Kalender klein gezoomt ist. Der Text steht ab genügend Höhe im Streifen („Vorb. 5 Min.“, „Nachb. 10 Min.“, bei der Vorbereitung ab etwas mehr Höhe mit dem Schritt: „Vorb. 5 · Steine heizen“); reicht der Platz nicht, zeigt der Hinweis ihn vollständig. Hat der Gast das Kennzeichen „Braucht mehr Zeit“, steht der Puffer getrennt dabei („Nachb. 10 · Puffer 10 Min.“). Im Seitenpanel des Termins steht unter der Uhrzeit die gebundene Zeit, z. B. „Gebunden 09:55–11:10 · Vorb. 5 · Nachb. 10 · Puffer 10 Min.“, oder „Keine Vor- und Nachbereitung“; weichen Vorbereitung oder Nachbereitung von der Behandlung ab, steht „(geändert)“ dahinter. Ein Rechtsklick auf den Streifen bietet **Zeiten dieses Termins ändern …** (nur dieser Termin, siehe [Vor-/Nachbereitung je Termin ändern](hilfe:kalender-termin-verschieben#zeiten-je-termin)) und **Behandlungszeiten bearbeiten …** (die Behandlung). Der Raumpflege-Streifen ist grau (offen), grün (erledigt) oder orange (überfällig) und trägt kein Kästchen, nur Text und Farbe; ein Klick hakt ab, der Hinweis endet mit „Klick: Raumpflege abhaken“ bzw. „… zurücknehmen“, siehe [Betrieb im Kalender](hilfe:kalender-betrieb#raumpflege).
- Hat die Behandlung eine **Einwirkzeit** (Packung, Maske; siehe [Behandlungen](hilfe:behandlungen#einwirkzeit)), liegt im Termin der Person ein flaches Band „Einwirkzeit 20 Min. · frei“ in der Farbe der Vor- und Nachbereitung, mit feinen Linien oben und unten: ab dort liegt der Gast allein im Raum und die Person ist frei. Der Text steht im Band nur, wenn er die Zeilen der Karte nicht überdeckt; sonst nennt ihn die Hover-Karte. Liegt in der Einwirkzeit ein zweiter Termin derselben Person, teilt sich die Spalte in zwei Bahnen zu je halber Breite, der später gebuchte rechts; beide Termine lassen sich wie sonst anklicken und ziehen. Das gilt für alles, was sich zeitlich überschneidet: in einer Spalte steht nichts übereinander, sondern in Bahnen nebeneinander, Termine immer links. Sperrzeiten, Pausen und Zettel neben einem Termin stehen in einer schmalen Bahn rechts (ein Drittel der Spalte, mindestens 72 Pixel), der Termin bekommt den Rest; ohne Überschneidung hat jedes Element die volle Breite. Auch der Zettel mit **Bis** wird so neben dem Termin gezeichnet, siehe [Notiz an einer Uhrzeit](hilfe:kalender-pausen-und-notizen#notiz). In der Raumansicht gibt es kein Band, weil der Raum belegt bleibt. Ein Klick in die Einwirkzeit öffnet kein neues Panel: gebucht wird dort über [Freie Zeit suchen](hilfe:kalender-freie-zeit-suchen), das solche Zeiten findet. Pausen- und Lückenprüfung rechnen vorerst mit der ganzen Bindung der Person.

  ![Einwirkzeit im Termin und zweiter Termin in der zweiten Bahn](images/app/kalender-einwirkzeit.png)
- Wer kurz mit der Maus auf einem Termin bleibt, sieht eine Karte, siehe [Hover-Karte](hilfe:seite-kalender#hover-karte).

## Alle Mitarbeitenden {#alle-mitarbeitende}

![Alle Mitarbeitenden: Spalten ohne Dienst sind schraffiert und nennen den Grund](images/app/kalender-alle-mitarbeitende.png)

Normalerweise zeigt der Tag nur Mitarbeitende mit Dienst an diesem Standort. Der Chip **Alle zeigen (n)** rechts in der zweiten Zeile der Kopfzeile blendet zusätzlich alle aus dem Team des Standorts ein, die an dem Tag nicht hier arbeiten; **n** ist ihre Zahl. Sie erscheint nur, wenn es solche Personen gibt. Ein weiterer Klick (**Nur mit Dienst**) nimmt sie wieder weg. Ihre Spalten sind ganz schraffiert, und der Kopf nennt den Grund: „frei“, „Urlaub bis 23.10.“, „krank gemeldet“ oder, bei Dienst an einem anderen Standort, in einer Zeile die Stecknadel, der Ort und die Uhrzeit („Salzburg · 10:00–18:00“).

- Wer in so eine Spalte buchen will, wird erst gefragt: oben erscheint „… hat an diesem Tag keinen Dienst hier. Trotzdem einen Termin eintragen?“ mit **Trotzdem buchen**. Erst danach geht **Neuer Termin** auf. Die Belegungsprüfung des Servers verlangt für eine gewünschte Person weiterhin einen Dienst; ohne Dienst lehnt sie den Termin ab und nennt den Grund.
- Die Spalten der übrigen Personen behalten beim Zuschalten ihre Breite; die zusätzlichen Spalten haben dieselbe Breite, und der Kalender lässt sich waagrecht scrollen.
- Der Chip gilt nur in der Teamansicht, nicht in der [Raumansicht](hilfe:kalender-raumansicht). Jeder Arbeitsplatz merkt sich die Einstellung für sich, auch nach dem Neustart.
- Die Spalten kommen aus dem Team des Standorts (Personen mit Arbeitszeit oder Dienstplan dort); wer nie an diesem Standort eingeteilt war, erscheint nicht.

## Hover-Karte {#hover-karte}

![Hover-Karte eines Termins](images/app/kalender-hoverkarte-karte.png)

- Oben in drei Zeilen, die Hauptangaben in Grundschrift und **fett**: der Name mit ♀/♂ und dem Etikett **Stammgast** oder **Erstbesuch**, rechts der Status, wenn der Termin nicht mehr nur gebucht ist; die **Behandlung**; **Tag und Uhrzeit** und der **Raum**, dazwischen normal Dauer und Person („Mo. 12.4. · 12:30–13:30 · 60 Min. · Elif Demir · Kosmetikkabine“). In der Raumansicht steht die Person fett.
- Darunter **orange** alles, was Handeln verlangt: überfällig, kommt später, umbuchen, verkürzt, der Hinweis aus der Kundenkarte und nicht erschienen (ab dem ersten Mal: „1× nicht erschienen“), „Nicht mehr buchen“, „Leitung informieren“, feste Therapeut:in bei jemand anderem, Wunsch Therapeutin/Therapeut abweichend, Zuordnung offen, in Treatwell annehmen. Grün steht die Zusage über den Link.
- Seltene Angaben zum Termin nur, wenn es sie gibt: Buchung (Paar, Spa-Tag), Bereiche, Serie, Quelle, Paket, Zahlung.
- **Geburtstag**: „Geburtstag heute · 42“ (das Alter nur bei bekanntem Geburtsjahr) bzw. drei Tage davor und danach „Geburtstag am 25.10.“, mit kleinem Geschenk-Symbol, siehe [Geburtstag](hilfe:seite-kalender#geburtstag).
- **Kennzeichen** nur noch für die übrigen Zeichen mit Erklärung: **+10**, Sprache, Paket.
- **Gast**: Besuche („2 in 12 Monaten · zuletzt 19.10.2026“, nur bei Gästen ohne Etikett Stammgast oder Erstbesuch, denn das Etikett sagt es schon), Wünsche und Vorlieben je in einer Zeile (zu lange Texte enden mit „…“; vollständig in der Kundenkarte).
- **Ablauf** nur, wenn er etwas sagt (Einwirkzeit oder Zeiten, die von der Behandlung abweichen; der übliche Ablauf steht in den Streifen im Kalender und im Termin-Panel), in einer Zeile („Vorb. 5 · Nachb. 5 · Puffer 10 · Raumpflege 5 Min.“; mit Einwirkzeit „Vorb. 5 · Hände 20 · Einwirken 20 · Ausarbeiten 20 · Nachb. 10 Min.“; „(geändert)“, wenn die Zeiten dieses Termins von der Behandlung abweichen), darunter der Vorbereitungshinweis der Behandlung, zuletzt die Notiz zum Termin.
- Stornierte Termine stehen nicht mehr im Kalender.

## Geburtstag {#geburtstag}

Hat ein Gast am Tag des Termins Geburtstag, trägt der Eintrag rechts im Kopf, neben Stern und Hinweis, ein kleines **Geschenk-Symbol** (ohne Text; der Hinweis darauf und der Bildschirmleser sagen „Geburtstag heute“). Es erscheint nur am Tag selbst und nur bei Gästen mit Geburtsdatum.

![Eintrag mit Geschenk-Symbol bei einem Gast, der heute Geburtstag hat](images/app/kalender-geburtstag.png)

- Die [Hover-Karte](hilfe:seite-kalender#hover-karte) und das Panel des Termins nennen es in einer Zeile: „Geburtstag heute · 42“ (das Alter nur dort und nur, wenn das Geburtsjahr bekannt ist). Drei Tage davor und danach steht dort ein dezenter Hinweis „Geburtstag am 25.10.“; am Eintrag steht dann nichts.
- Der **Tagesüberblick** nennt oberhalb von „Als Nächstes“ „Geburtstag heute: Brigitte Müller (10:30)“, mehrere mit „ · “ getrennt, nur wenn es einen gibt.
- Wer am 29. Februar geboren ist, feiert in Jahren ohne Schalttag am 28. Februar. Gerechnet wird im Server, in der Zeitzone des Standorts.
- Der [KI-Assistent](hilfe:ki-assistent-fragen#betrieb) beantwortet „Wer hat heute Geburtstag?“ und „Wer hat diese Woche Geburtstag?“, nur für Gäste mit Termin im Zeitraum. Alter und nächsten Geburtstag zeigt die [Kundenkarte](hilfe:kunden-kundenkarte).

![Termin-Panel mit der Zeile Geburtstag heute und dem Alter](images/app/termin-geburtstag.png)

## Maus {#maus}

- Klick auf einen Termin öffnet ihn rechts im Seitenpanel; die Pfeiltasten markieren Termine, ohne sie zu öffnen. Der markierte bzw. geöffnete Termin trägt einen kräftigen türkisen Rahmen; Farbe und Balken zeigen weiter den Status (z. B. orange bei überfällig).
- Klick auf eine freie Stelle öffnet **Neuer Termin** zu dieser Viertelstunde; die Viertelstunde unter dem Mauszeiger ist vorher hell markiert.
- Termin ziehen verschiebt ihn (Beginn in 5-Minuten-Schritten). Über einem Termin, den man verschieben darf, ist der Mauszeiger eine Hand; **Esc** während des Ziehens bricht ab.
- Rechtsklick auf Termin, freie Stelle, Pause, Notiz oder Spaltenkopf zeigt die passenden Schritte.
- Ein Klick auf eine freie Stelle öffnet rechts das Panel für neue Einträge; eine Leiste oben wechselt zwischen Termin, Paar/Spa, Pause, Sperrzeit, Notiz und Sauna, siehe [Art wechseln](hilfe:kalender-pausen-und-notizen#art-wechseln). Bei kurzer freier Zeit ist **Pause** vorgewählt, siehe [Termin buchen](hilfe:kalender-termin-buchen#vorschlag).

## Seitenpanel {#seitenpanel}

Rechts öffnet sich je nach Schritt: der geöffnete Termin, **Neuer Termin**, **Paar oder Spa-Tag**, **Sauna und Ruheraum**, **Warteliste**, **Freie Zeit suchen**, **Wunsch eintippen**, **Sperrzeit**, **Notiz**, die **Meldungen**, der Ausfall-Assistent oder **KI und Hilfe**. Alle Leisten sind gleich gebaut: gleich breit (360 Pixel, nur das Formular für Kundendaten ist breiter), oben fest Titel und Untertitel mit dem Kreuz rechts, darunter scrollt der Inhalt. Das Kreuz schließt sie, ebenso **Esc**. „KI und Hilfe“ ist eine dieser Seiten und hat wie die anderen einen Reiter (mit dem AISPA-Zeichen): der runde Knopf am Seitenrand und **Strg+Leertaste** wählen ihn und öffnen dafür die Seitenleiste, falls sie zu ist; das Kreuz und **Esc** schließen nur diesen Reiter, die übrigen bleiben stehen (siehe [Den KI-Assistenten fragen](hilfe:ki-assistent-fragen)). Es steht nie mehr als eine Leiste da: geht eine auf, macht die vorige Platz und bleibt als **Reiter** am unteren Rand der Leiste stehen (siehe unten). Leisten zum Eingeben (Freie Zeit, Wunsch, Neuer Eintrag …) setzen den Fokus ins erste Feld, beim Ansehen eines Termins bleibt er im Kalender, damit die Pfeiltasten weiter wirken. Per Maus blendet eine Leiste in 200 ms ein, beim Schließen schneller; bei Tastenkürzeln und bei abgeschalteten Windows-Animationen ohne Bewegung.

**Reiter unten:** am unteren Rand der Seitenleiste steht für offene Seiten je ein Reiter mit Symbol und kurzem Text („Termin“, „Warteliste“, „Freie Zeit“, „KI und Hilfe“ …); der gewählte hat eine helle Fläche und blaugrüne Schrift. Die zuletzt geöffnete Seite steht **ganz links**, und es sind **höchstens zwei Reiter mit Text** zu sehen: der gewählte und der davor benutzte. Ab der dritten offenen Seite zeigen die Reiter nur ihr Symbol (der Name steht im Tooltip), und es sind bis zu **fünf** zugleich zu sehen. Sind mehr Seiten offen, als die Schiene zeigt (ab der sechsten), erscheint rechts in der Schiene der Chip **⋯ 1** (die Zahl nennt die übrigen Seiten); ein Klick darauf öffnet eine Liste aller offenen Seiten mit Symbol und Text, ein Klick auf einen Eintrag wählt sie. Daneben blättern zwei Pfeile (links und rechts) durch die Reihe der offenen Seiten; am Ende beginnt es von vorn. Ein Klick auf einen Reiter wechselt zur Seite, **Strg+Tab** zum nächsten und **Strg+Umschalt+Tab** zum vorigen. Das Kreuz im Kopf der Leiste und **Esc** schließen nur die gewählte Seite; danach ist der nächste gewählt, und ohne weitere Seite geht die Leiste zu. Es gibt höchstens sechs offene Seiten, je Art von Seite eine (ein zweiter Termin ersetzt den ersten); öffnet sich eine siebte, fällt die älteste nicht gewählte weg. Die Reiter lassen sich nicht verschieben und nicht als Fenster lösen, die Schiene scrollt nicht. Der Reiter von **Neuer Termin** nennt den gewählten Gast („Neuer Termin · Brigitte Müller“) und trägt am Symbol einen Punkt: der Termin ist in Bearbeitung und noch nicht gebucht. Beim Wechsel des Standorts schließen alle Seiten samt Reitern.

Oben im Panel eines Termins steht fest der Kopf: links die **Initialen** des Gastes, daneben der **Name** groß mit **♀**/**♂** und dem Stift, darunter die Behandlung (bei langem Namen läuft der Name über zwei Zeilen), ganz rechts das Kreuz; Status und Dauer stehen als Pillen in einer Zeile direkt unter dem Kopf. Der Stift öffnet die [Kundendaten zum Ergänzen](hilfe:kunden-bearbeiten#aus-dem-kalender) direkt im Panel, ohne die Kundenkarte zu öffnen. Ein Klick auf Initialen oder Namen zeigt die [Kundenübersicht](hilfe:seite-kalender#kundenuebersicht). Ein Klick auf den Raum zeigt die [Raum-Infos](hilfe:kalender-raumansicht#raum-infos). Bei Paar und Spa-Tag stehen darunter [alle Teile der Buchung](hilfe:kalender-paar-und-spa-tag#aendern).

Unter der Uhrzeit im Panel eines Termins zeigt der **Zeitbalken**, wie die gebundene Zeit aufgeteilt ist: Vorbereitung (schraffiert), die Behandlung in der Farbe des Status wie die Terminkarte (mit Einwirkzeit in Hände, Einwirken und Ausarbeiten geteilt, das Einwirken blasser), Nachbereitung (grau), der Puffer (orange) und in der Raumansicht die Raumpflege (grau, erledigt grün, überfällig orange). Jeder Abschnitt ist so breit wie seine Minuten; die Minuten stehen darüber, Beginn und Ende der Bindung links und rechts darunter. Ein Klick auf Vorbereitung oder Nachbereitung öffnet die Zahlenfelder, siehe [Vor-/Nachbereitung je Termin ändern](hilfe:kalender-termin-verschieben#zeiten-je-termin).

Unter den Terminangaben (Mitarbeiter:in, Raum, Preis) steht **Notiz hinzufügen**, mit Notiz die gelbe Fläche mit einem Stift zum Ändern, siehe [Notiz zu einem Termin](hilfe:kalender-pausen-und-notizen#termin-notiz). Darunter steht der zugeklappte Abschnitt **Kunde** mit vier Kennzahlen, siehe [Kunde im Termin](hilfe:seite-kalender#kunde-im-termin).

Im Panel eines Termins steht oben ein Hauptknopf je Status: **Einchecken** bei „Gebucht“, **Kassieren** bei „Eingecheckt“ und „Erledigt“. Darunter stehen die weiteren Aktionen in fester Reihenfolge: bei „Gebucht“ **Verschieben …** über die volle Breite, bei „Eingecheckt“ **Einchecken zurücknehmen**, und bei beiden rot **Termin stornieren …**; Stornieren geht auch ohne vorher einzuchecken. Bei „Storniert“ steht **Stornierung zurücknehmen** als Hauptknopf. Die Kundenkarte öffnet man über die [Kundenübersicht](hilfe:seite-kalender#kundenuebersicht) mit **Kundenkarte öffnen**. Alles Seltenere liegt hinter **Mehr** (zunächst zugeklappt; der Pfeil daneben zeigt nach unten, aufgeklappt nach oben): **Erledigt**, **Nicht erschienen**, **Wiederholen …** und **Zeiten dieses Termins ändern …**. Die Rückfrage zum Stornieren und die Karte zum Verschieben öffnen sich unabhängig davon, auch über die Tasten (**Entf**, **Alt** + Pfeile).

## Kunde im Termin {#kunde-im-termin}

![Abschnitt Kunde im Termin-Panel, aufgeklappt](images/app/termin-kunde-auf.png)

Der Abschnitt **Kunde** im Panel eines Termins gibt den Überblick über den Gast, ohne die Kundenkarte zu öffnen. Er ist zu, bis man ihn öffnet (Klick, **Enter** oder **Leertaste** auf der Kopfzeile), und lädt beim ersten Zeigen:

- **Kopfzeile** (immer zu sehen): vier Kennzahlen in zwei Spalten, **Besuche gesamt** (erledigte Termine), **Letzter Besuch**, **Nicht erschienen** (orange, wenn es welche gibt) und **Nächster Termin**. Das Datum steht kurz („3.10.“), das volle Datum im Hinweis. Mit dem Recht „Berichte mit Beträgen“ folgt eine Zeile wie „Offen 45,00 € · Paket Massage: 7 von 10 offen“, wenn es etwas Offenes gibt.
- **Aufgeklappt**: die **Kundennotiz** aus den Kundendaten („Weitere Angaben“) auf zwei Zeilen gekürzt (der volle Text steht im Hinweis), der Stand der **Anamnese** („vorhanden“ oder „fehlt“; die Antworten stehen für Rollen mit Leserecht in der Karte [Anamnese](hilfe:kalender-termin-anamnese)), die Liste **Zuletzt** mit den letzten bis zu fünf Behandlungen (Datum, Behandlung, Person; ein Klick öffnet den Termin, auch an einem anderen Tag) und **Kontakt**.
- **Kontakt**: die Telefonnummer ist ein Link, der die Anruf-App des Rechners öffnet; **Per WhatsApp schreiben** öffnet den Chat (nur mit Telefonnummer; eine Nummer mit Null davor bekommt die Vorwahl des Standorts); die E-Mail-Adresse öffnet das Mailprogramm mit dem Namen des Gastes im Betreff. Neben Telefon und E-Mail kopiert ein Knopf den Wert, die Meldung unten sagt „Kopiert“.
- **Kundenkarte öffnen** (auch **Strg+Enter**, solange der Fokus im Abschnitt ist) öffnet die [Kundenkarte](hilfe:kunden-kundenkarte).

Bei einem Gast ohne Namen gibt es den Abschnitt nicht. Die [Kundenübersicht](hilfe:seite-kalender#kundenuebersicht) hinter Initialen und Namen bleibt daneben bestehen.

## Kundenübersicht {#kundenuebersicht}

![Kundenübersicht des Gastes neben dem Termin](images/app/kalender-kundenuebersicht.png)

Ein Klick auf die Initialen oder den Namen im Kopf des Termins öffnet links neben dem Panel eine Übersicht über den Gast, so wie die Raum-Infos beim Raum. Sie lädt bei jedem Öffnen neu und zeigt:

- den Namen im Haus mit ♀/♂ und Pronomen, darunter die Adresszeile mit Anrede und Titeln („Frau Mag. Brigitte Müller“) und den Rufnamen,
- Telefon und E-Mail-Adresse, je mit einem Knopf zum Kopieren (daneben steht kurz „Kopiert.“), und die Postanschrift,
- Besuche in zwölf Monaten, den letzten Besuch und die nächsten drei Termine,
- Kennzeichen („Braucht mehr Zeit“, „Nicht bei …“), die Notiz, die Wünsche des Gastes in einer Zeile und offene Pakete,
- die Knöpfe **Kundenkarte öffnen** und **Bearbeiten** (öffnet die Kundendaten im Panel).

Gesundheitsdaten stehen hier nie. Offene Gutscheine zeigt die Übersicht noch nicht. Bei einem Gast ohne Namen („Gast 3 · Name folgt“) gibt es keine Übersicht.

## Unterleiste {#unterleiste}

Unten stehen die wichtigsten Tasten für die aktuelle Lage, z. B. mit markiertem Termin **Enter** Details, **E** Einchecken, **Entf** Stornieren. **?** oder F1 zeigt alle Tasten, siehe [Tastenkürzel im Kalender](hilfe:kalender-tastenkuerzel).

## Abläufe {#ablaeufe}

- [Termin buchen](hilfe:kalender-termin-buchen)
- [Termin verschieben](hilfe:kalender-termin-verschieben)
- [Termin stornieren oder „Nicht erschienen“](hilfe:kalender-termin-stornieren)
- [Status setzen: einchecken, erledigt](hilfe:kalender-status-setzen)
- [Termin kassieren](hilfe:kalender-kassieren)
- [Paar oder Spa-Tag buchen](hilfe:kalender-paar-und-spa-tag)
- [Sauna- und Ruheraum-Eintritt](hilfe:kalender-sauna-eintritt)
- [Freie Zeit suchen](hilfe:kalender-freie-zeit-suchen)
- [Wunsch eintippen (KI-Anfrage)](hilfe:kalender-ki-anfrage)
- [Warteliste ansehen](hilfe:kalender-warteliste)
- [Terminbestätigung als PDF drucken oder senden](hilfe:kalender-terminbestaetigung)
- [Tagesplan drucken](hilfe:kalender-tagesplan-drucken)
- [Lücken schließen](hilfe:kalender-luecken-schliessen)
- [Pausen, Sperrzeiten und Notizen](hilfe:kalender-pausen-und-notizen)
- [Krank melden und Ausfall-Assistent](hilfe:kalender-krankmeldung)
- [Raumansicht](hilfe:kalender-raumansicht)
- [Meldungen: Warnungen und Hinweise des Tages](hilfe:kalender-meldungen)
- [Betrieb im Kalender: Tagesstart, Raumpflege, Bereitstellen](hilfe:kalender-betrieb)
- [Termin finden, Tag und Standort wechseln](hilfe:kalender-termin-finden)
- [Arbeiten ohne Verbindung](hilfe:kalender-offline)
- [Tastenkürzel im Kalender](hilfe:kalender-tastenkuerzel)
