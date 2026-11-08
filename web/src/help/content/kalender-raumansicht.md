---
art: ablauf
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, therapeut, leitung, inhaber, manager
seite: kalender
stichwoerter: Raumansicht, Räume, Raum, Kabine, Behandlungsraum, Paarsuite, Raumbelegung, Raumpflege, Reinigung, Team-Ansicht, Ansicht wechseln, Spalten nach Raum, room view, rooms, cleaning
---
# Raumansicht

Statt einer Spalte je Person zeigt der Kalender eine Spalte je Raum. So sieht man, welcher Raum wann belegt ist und wann er gereinigt wird.

## Umschalten {#umschalten}

1. In der zweiten Zeile der Kopfzeile auf den Chip **Räume** klicken.
2. Zurück zur Ansicht je Person mit dem Chip **Team**.

![Raumansicht mit markierter Paar-Buchung](images/app/kalender-raeume.png)

## Was die Raumansicht zeigt {#inhalt}

- Je Raum eine Spalte, **genauso breit wie in der Ansicht Team** und mit demselben Kopf: die Breite richtet sich nach den Personenspalten des Tages, nicht nach der Zahl der Räume. Gibt es mehr Räume, als ins Fenster passen, scrollt der Kalender waagrecht wie bei vielen Personen. Der Kopf hat wie bei Personen genau zwei Zeilen: oben den Namen (lange Namen enden mit „…“, der volle Name steht im Hinweis), darunter z. B. „Raumpflege 10 Min. · 6 Behandlungen“. Wer kurz mit der Maus auf dem Kopf bleibt, sieht höchstens fünf kurze Zeilen: Raumart, Raumpflege, die Zahl der Termine heute, eine Warnung und was ein Klick tut. Welche Behandlungen im Raum möglich sind, steht in den [Raum-Infos](hilfe:kalender-raumansicht#raum-infos).
- **Paarräume** (Raum mit zwei Plätzen) heben sich ab: eigenes Symbol mit zwei Personen in der Akzentfarbe statt des grauen Türsymbols der Einzelräume. Ein Etikett gibt es nicht; „Paarraum · 2 Plätze“ steht im Hinweis am Kopf.
- Hell ist die Öffnungszeit des Standorts, außerhalb ist die Spalte schraffiert. Auch eine [Raumsperre](hilfe:raumsperren) ist schraffiert, mit einer Kapsel für Art und Grund; im Kopf steht dann „gesperrt: Wartung bis 3.12.“ und, wenn Termine im Raum liegen, **n Termine umbuchen**.
- Jeder Termin zeigt in einem farbigen Kreis, wer behandelt (den vollen Namen nennt die Hover-Karte); so bleibt in der Karte mehr Platz für Gast und Behandlung.
- Nach jedem Termin liegt **ein** schmaler Streifen für Nachbereitung und Raumpflege zusammen, z. B. „Nachb. 5 · Raumpflege 10 Min.“. Der Streifen hat kein Kästchen, nur Text und Farbe (grau offen, grün erledigt, orange überfällig); ein Klick hakt die Raumpflege ab. Reicht der Platz nicht für den Text, steht er im Hinweis, wenn man mit der Maus auf dem Streifen bleibt; der Hinweis endet mit „Klick: Raumpflege abhaken“.
- Ein Klick auf den Kopf eines Raums zeigt dessen Termine [über sieben Tage](hilfe:kalender-sieben-tage). Ein kleines Warnsymbol im Kopf (z. B. Raumpflege überfällig) öffnet die [Meldungen](hilfe:kalender-meldungen) nur für diesen Raum.
- Die Räume stehen nach Kategorie: erst die Behandlungsräume, dann **Sauna und Ruheraum** (bei Sauna und Ruheraum steht die Kategorie im Hinweis am Kopf). Sauna und Ruheraum zeigen im Kopf die Plätze und darunter die Eintritte des Tages als Blöcke mit Name und Personenzahl; ein Klick in die Spalte öffnet den [Eintritt](hilfe:kalender-sauna-eintritt), die gestrichelte Vorschau steht dann in der Spalte.
- Ist ein Raum den ganzen Tag gesperrt, erscheint in der Kopfzeile der Chip **Nur verfügbare Räume**. Er blendet solche Räume aus, **Alle Räume** holt sie zurück. Eine kurze Sperre, etwa eine Reinigung am Vormittag, zählt nicht. Die Wahl gilt nur für diese Sitzung.

![Vorbereitung, Nachbereitung und Raumpflege als Streifen](images/app/kalender-raumpflege.png)

## Arbeiten in der Raumansicht {#arbeiten}

- Wer in der Ansicht Team einen Termin angefangen hat und auf **Räume** umschaltet, behält ihn: er wechselt in den Raum der Behandlung, die gestrichelte Vorschau steht dort und lässt sich ziehen, der Kalender rückt den Raum ins Bild.
- Klick auf eine freie Stelle bucht in diesem Raum; unter dem Titel des Panels steht dann z. B. „Raum Lotus“, und die Liste **Behandlung** zeigt nur, was in diesem Raum möglich ist, siehe [Termin buchen](hilfe:kalender-termin-buchen#frei-waehlen).
- Ziehen in eine andere Spalte wechselt den Raum, die Person bleibt, siehe [Termin verschieben](hilfe:kalender-termin-verschieben#andere-person).
- Sperrzeiten gehen auch für Räume (z. B. „Wartung“), siehe [Sperrzeit](hilfe:kalender-pausen-und-notizen#sperrzeit). Soll ein Raum für Tage oder Wochen gesperrt sein, legt man eine [Raumsperre](hilfe:raumsperren) an: Rechtsklick auf den Kopf des Raums, **Raum sperren …**. Die Termine darin bucht man [um](hilfe:raumsperren-termine-umbuchen). **Pause hier** und Krankmeldung gibt es nur in der Ansicht **Team**.

## Raum-Infos im Termin {#raum-infos}

Im Seitenpanel eines Termins ist der Raum unterstrichen. Ein Klick darauf (oder Enter) öffnet links neben dem Panel eine Karte mit Raumart, Raumpflege, den Behandlungen des Raums (ab sieben Zeilen mit **Alle anzeigen**) und den Terminen des Raums an dem Tag; der Termin im Panel ist hervorgehoben. Wer Standorte verwalten darf, findet darunter **In der Verwaltung bearbeiten**; das öffnet Stammdaten bei **Standorte & Räume**.

![Raum-Infos im Terminfenster](images/app/kalender-raum-info.png)

## Hinweise {#hinweise}

- Geräte gehören zu Behandlungen, nicht zu Räumen; deshalb zeigt der Raum keine Geräte.
- Raumpflege und Plätze stellt man beim Raum ein, siehe [Standorte & Räume](hilfe:standorte#raumpflege).
- Die Ansicht wechseln dürfen alle Rollen.
