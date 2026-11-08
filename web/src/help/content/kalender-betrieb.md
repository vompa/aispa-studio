---
art: ablauf
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, therapeut, leitung, inhaber, manager
seite: kalender
stichwoerter: Tagesstart, Raumpflege, Raum pflegen, Raumcheck, Mindestbestand, abhaken, Abnahme, Bereitstellen, Tee, Früchteteller, Handbuch, Hinweis, Lager, Warnung, Hygiene, überfällig, morning check, room care, serving
---
# Betrieb im Kalender: Tagesstart, Raumpflege, Bereitstellen

Der Kalender zeigt, was im Haus zu tun ist: ob die Räume am Morgen kontrolliert sind, ob nach einem Termin gepflegt wurde und was zu welcher Uhrzeit bereitgestellt wird. Abhaken kann man gleich dort; nichts davon sperrt eine Buchung oder ein Einchecken.

![Seitenleiste Meldungen neben der Raumansicht](images/app/kalender-betrieb.png)

## Raumpflege abhaken {#raumpflege}

In der [Raumansicht](hilfe:kalender-raumansicht) steht unter jedem Termin ein Streifen „Nachb. 10 · Raumpflege 10 Min.“. Er hat kein Kästchen; Farbe und Text zeigen den Zustand der Aufgabe „Raum pflegen bis HH:MM“:

- **grau:** offen
- **grün:** erledigt; der Hinweis am Streifen nennt Person und Uhrzeit
- **orange:** überfällig, die Pflegezeit ist vorbei

Ein Klick auf den Streifen hakt ab, ein zweiter Klick nimmt den Haken zurück; der Hinweis am Streifen endet mit „Klick: Raumpflege abhaken“ bzw. „Klick: Raumpflege zurücknehmen“. Liegt im Schrank des Raums nach dem Verbrauch Material unter der Mindestmenge, hakt der Klick nicht direkt ab, sondern öffnet die Seitenleiste **Raumpflege** mit der Liste zum Auffüllen; der Hinweis am Streifen nennt dann „2 Materialien nachfüllen“ (siehe [Raum nach der Behandlung prüfen](hilfe:kalender-raumcheck)). Im Rechtsklick-Menü des Streifens stehen dafür **Raumpflege erledigt** bzw. **Raumpflege zurücknehmen**. Abhaken darf, wer den Status von Terminen ändern darf. Ohne dieses Recht zeigt der Streifen nur den Zustand.

Im Kopf der Raumspalte steht kein Text mehr zum Stand des Raums. Ist etwas offen oder überfällig, zeigt ein kleines Symbol mit Hinweis („2 Raumpflege offen · Tagesstart offen“) darauf; ein Klick öffnet die [Meldungen](hilfe:kalender-meldungen) nur für diesen Raum. Überfällig wird die Raumpflege mit der Uhr: ohne Neuladen erscheint die Meldung, sobald die Pflegezeit vorbei ist.

## Tagesstart und Meldungen {#betriebsleiste}

Die frühere Betriebszeile über dem Kalender und die Seitenleiste „Tagesstart“ gibt es nicht mehr: alles steht in der Seitenleiste **Meldungen** (Schaltfläche im Kalenderkopf oder Taste **M**), siehe [Meldungen](hilfe:kalender-meldungen):

- **Tagesstart offen:** eine Meldung je Raum mit Checkliste, solange nicht alle Punkte abgehakt sind. **Tagesstart öffnen** zeigt im Detail die Punkte zum Abhaken und den Stand („offen“, „1 von 3“, „geprüft“, „abgenommen von …“). Abgehakt wird heute oder für vergangene Tage, nicht im Voraus; nach der Abnahme sind die Haken gesperrt.
- **Räume unter Mindestbestand:** eine Meldung wie „2 Räume unter Mindestbestand“, wenn im Raumschrank Material unter der Mindestmenge liegt. **Raumansicht** wechselt zu den Räumen, dort zeigen die Streifen, wo nachzufüllen ist.
- **Lager:** Warnungen der [Bedarfsprüfung](hilfe:lager-bedarf-pruefen), z. B. „Lavendelöl: Bestand reicht nur bis 14:30“; nur mit dem Recht zu buchen.
- **Handbuch:** die Einträge des [Rezeptionshandbuchs](hilfe:handbuch), die am Tagesstart erinnern, als Hinweis.

Meldungen gibt es im Tageskalender, nicht in der 7-Tage-Ansicht. Ohne Verbindung zeigt die Seitenleiste den letzten Stand, die Aktionen sind abgeschaltet.

## Tagesstart abnehmen {#abnehmen}

Sind alle Punkte eines Raums abgehakt, erscheint **Abnehmen**. Wer das Recht „Standort verwalten“ hat, nimmt damit selbst ab. Sonst fragt die Seitenleiste nach der Leitung und ihrer PIN: Name wählen, **PIN** eintippen, **Abnehmen** klicken. Die Leitung muss sich dafür nicht anmelden (siehe [Tagesstart und Raumpflege abnehmen](hilfe:hygiene-abnehmen)).

## Bereitstellen im Termin {#bereitstellen}

Im Panel eines Termins steht der Abschnitt **Bereitstellen** mit den Einträgen der Behandlung, z. B. „Kräutertee 10:30“ und „Früchteteller 11:10“, mit Kästchen zum Abhaken. Darunter stehen die Wünsche des Gastes von der Kundenkarte (z. B. die Teesorte) und, wenn ein Gesundheitsfragebogen vorliegt, nur der Hinweis **Anamnese beachten**; die Antworten stehen in der Karte [Anamnese](hilfe:kalender-termin-anamnese) und erscheinen erst auf Klick. Die Hover-Karte des Termins nennt dasselbe in einer Zeile.

![Termin mit Bereitstellen und Hinweis auf die Anamnese](images/app/kalender-bereitstellen.png)

Das Abhaken bucht nichts im Lager; abgebucht wird erst beim Abschließen der Behandlung.

## Hinweise nach Buchen und Einchecken {#hinweise}

Hat eine Buchung eine Lagergrenze gerissen (Bestand reicht nicht, Mindestmenge, nur noch wenige Behandlungen) oder ist beim Einchecken der Tagesstart des Raums nicht abgeschlossen bzw. die Raumpflege davor nicht abgehakt, erscheint in den [Meldungen](hilfe:kalender-meldungen) eine Warnung „Hinweis“. Sie bleibt stehen, bis man sie wegklickt. Gebucht bzw. eingecheckt ist trotzdem.

## Gut zu wissen {#gut-zu-wissen}

- Die Checklisten pflegt die Leitung unter [Standorte & Räume](hilfe:standorte#tagesstart-kontrolle), die Zeiten für Raumpflege bei den [Räumen](hilfe:standorte#raumpflege), „Zum Termin bereitstellen“ bei der [Behandlung](hilfe:behandlungen-bereitstellen-einrichten).
- Das Protokoll je Tag und Raum lädt die Leitung unter [Hygieneprotokoll laden](hilfe:hygiene-protokoll-laden).
