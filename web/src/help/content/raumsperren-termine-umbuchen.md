---
art: ablauf
bereich: Raumsperren
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kalender
stichwoerter: Raumsperre Termine, Termine im gesperrten Raum, Raum gesperrt umbuchen, anderer Raum, Raumwechsel, Umbuchungshilfe, Wasserschaden Termine, Wartung Termine, Termine umbuchen, Gäste informieren, rebook, other room, room closure appointments
---
# Termine im gesperrten Raum umbuchen

Wird ein Raum gesperrt, bleiben seine Termine gebucht. Die Umbuchungshilfe schlägt für jeden offenen Termin im gesperrten Raum vor, wohin er kann: zuerst ein anderer Raum zur selben Zeit bei derselben Person, sonst die nächste freie Zeit am selben Tag. Die Rezeption wählt aus und übernimmt; dieselbe Hilfe wie der [Ausfall-Assistent](hilfe:kalender-krankmeldung#ausfall-assistent). Nichts wird von selbst umgebucht. Dafür braucht es das Recht „Buchen“ (Rezeption, Standortleitung, Inhaber).

Die Meldung „Raum Lotus ist gesperrt (…): 4 Termine umbuchen“ steht in der Seitenleiste **Meldungen**, sobald eine Raumsperre offene Termine im Raum hat, egal wie fern sie liegt. Sie ist eine **Warnung**, solange die Sperre später als in 14 Tagen beginnt, und ein **Fehler**, wenn sie läuft oder in 14 Tagen beginnt.

![Meldung „Raum Lotus ist gesperrt: 4 Termine umbuchen“ mit dem Knopf Umbuchen](images/app/meldung-raumsperre.png)

## Umbuchen {#umbuchen}

1. Im Kalender in der Seitenleiste **Meldungen** bei „… ist gesperrt: n Termine umbuchen“ auf **Umbuchen …** klicken. Oder in der Raumansicht im Kopf des gesperrten Raums auf **n Termine umbuchen** klicken, oder mit der rechten Maustaste auf den Kopf und **Termine in andere Räume umbuchen**.
2. Rechts öffnet sich **Umbuchung aus Raum …** mit dem Zeitraum der Sperre. Für jeden offenen Termin steht ein Vorschlag da, mit Tag, Gast und einem Satz, warum: „gleiche Zeit bei Elif Demir, Raum Lavendel“ oder, wenn zur selben Zeit kein anderer Raum frei ist, die nächste freie Zeit am selben Tag. Ohne Vorschlag steht „kein anderer Raum an diesem Tag“; den Gast ruft die Rezeption an.
3. Mit den Häkchen einzelne Vorschläge abwählen. Auf Wunsch **Gäste per Mail über die Änderung informieren** ankreuzen: wer eine Mailadresse hat und dessen Zeit oder Person sich ändert, bekommt eine Mail; bei einem reinen Raumwechsel geht keine Mail hinaus, weil er den Gast nicht betrifft.
4. Auf **Gewählte übernehmen** klicken. Der Server verschiebt jeden Termin mit denselben Prüfungen wie beim Verschieben im Kalender; was inzwischen belegt ist, steht an der Zeile („Nicht verschoben: …“). Unten steht, wie viele Termine umgebucht wurden.

![Umbuchungshilfe mit drei Vorschlägen, der dritte ohne anderen Raum](images/app/kalender-raumsperre-umbuchen.png)

## Hinweise {#hinweise}

- Das Fenster zeigt immer alle offenen Termine der Sperre, auch an anderen Tagen, mit dem Tag vor der Zeit.
- Ein einzelner Termin lässt sich auch von Hand in einen anderen Raum ziehen: [Termin verschieben](hilfe:kalender-termin-verschieben#andere-person).
- Die Sperre selbst legt man in der Windows-App unter **Verwaltung** › **Betrieb** › **Raumsperren** an oder hebt sie dort auf: [Raumsperren](hilfe:raumsperren).
