# 0065. Raumpflege als Aufgabe, Tagesstart-Kontrolle je Raum und Abnahme durch die Standortleitung

- **Status:** angenommen
- **Datum:** 21.10.2026
- **Entschieden von:** Auftraggeber (Vorgaben vom 21.10.2026), Ausgestaltung Claude

## Kontext

Der Kalender hält nach jedem Termin die Raumpflege des Raums frei (`Room.CleaningMinutes`), aber ob der Raum danach wirklich gepflegt wurde, wusste die Software nicht. Ebenso gab es keine Kontrolle vor dem ersten Termin des Tages (Sauna aufheizen, Temperatur prüfen, Handtücher). Vorgabe vom Auftraggeber: Raumpflege als abhakbare Aufgabe mit Person und Zeit und mit Zustand im Kalender (offen, erledigt, überfällig); eine Checkliste je Raum oder Bereich für den Tagesstart, je Tag abzuhaken; Hinweise, nicht Sperren, wenn beim Einchecken etwas fehlt; Abnahme durch die Standortleitung, auch per PIN am Empfangsgerät; ein Hygieneprotokoll je Tag und Raum zum Exportieren.

## Optionen

| Option | Bewertung |
| --- | --- |
| Ein Termin kann ohne abgehakte Raumpflege oder Tagesstart nicht eingecheckt werden | sicher, aber an der Rezeption kaum durchzuhalten; ein vergessener Haken würde Gäste warten lassen |
| Nur Hinweise beim Einchecken, alles bleibt buchbar und einchecken | gewählt: das Team wird erinnert, der Betrieb läuft weiter, das Protokoll zeigt Lücken |
| Raumpflege je Raum und Pflegezeit als eigener Eintrag ohne Bezug zum Termin | einfacher zu speichern, aber „Raum X pflegen bis 11:15“ gehört fachlich zu einem Termin und seinem Ende |
| Aufgabe hängt am Termin, gleiche Raumbelegung (Paarsuite) wird zu einer Aufgabe | gewählt: ein Haken für den Raum, gespeichert je Termin |
| Abnahme nur mit angemeldeter Leitung | die Leitung ist morgens oft nicht am Empfangsgerät angemeldet |
| Abnahme mit angemeldeter Leitung oder mit PIN der Leitung am selben Platz (`QuickSwitch.ApproveAsync`, wie bei Rabatten über der Grenze) | gewählt: bestehender Weg, gleiche Sperre nach Fehlversuchen |

## Entscheidung

- **Raumpflege** entsteht nach jedem Termin (nicht storniert, nicht „nicht erschienen“) in einem Raum mit Raumpflege über 0 Minuten. Die Aufgabe heißt „Raum X pflegen bis HH:MM“ (Ende der Belegung des Termins plus Raumpflege des Raums). Termine im selben Raum mit gleichem Ende der Belegung sind eine Aufgabe. Zustand: **offen**, **erledigt** (abgehakt, mit Person und Zeit) oder **überfällig** (Pflegezeit vorbei, nicht abgehakt). Abhaken darf, wer Termine bearbeiten darf (Recht „Status ändern“, auch Therapeut:innen); nicht abgehakte Aufgaben vergangener Tage bleiben überfällig.
- **Tagesstart-Kontrolle**: je Raum oder Bereich eine Checkliste (Stammdaten › Standorte & Räume, Recht „Standort verwalten“, nur an eigenen Standorten). Je Tag (Ortszeit des Standorts) wird jeder Punkt mit Person und Zeit abgehakt, heute oder rückwirkend, nicht im Voraus. Stand je Raum: ohne Checkliste, offen, teilweise, **freigegeben** (alle Punkte abgehakt), **abgenommen**. Entfernte Punkte werden ausgeschaltet, nicht gelöscht, damit das Protokoll sie behält.
- **Hinweise, nie Sperren**: Beim Einchecken eines Termins meldet die Antwort in `Warnings`, wenn der Tagesstart des Raums nicht abgeschlossen ist („0 von 3 Punkten“) oder die Raumpflege nach dem vorigen Termin im Raum nicht abgehakt ist. Eingecheckt wird trotzdem. Der Kalendertag trägt Aufgaben und Raumzustand für die spätere Anzeige (Streifen grau, grün, orange; Zustand im Raumkopf).
- **Abnahme** je Raum, Tag und Art (Tagesstart, Raumpflege) durch die Standortleitung: wer das Recht „Standort verwalten“ hat (Inhaber, Standortleitung am Standort), nimmt selbst ab; sonst bestätigt die Leitung mit Name und PIN am selben Platz, ohne sich anzumelden. Voraussetzung: alle Punkte bzw. alle Aufgaben des Tages sind abgehakt. Eine Abnahme lässt sich nicht wiederholen; danach sind die Haken des Tagesstarts gesperrt. Gespeichert werden Abnehmende und, wer am Gerät angemeldet war.
- **Hygieneprotokoll** je Tag und Raum (bis zu 92 Tage, bis heute): Punkte des Tagesstarts und Raumpflege-Aufgaben mit Fälligkeit, Erledigung, Person, Abnahme; als Daten und als CSV (Semikolon, UTF-8 mit BOM, Formelzeichen entschärft). Nur mit dem Recht „Standort verwalten“.

## Begründung

Hinweise statt Sperren folgen dem Prinzip der Rezeption: der Betrieb darf nie an einem Haken scheitern, aber Lücken müssen sichtbar und nachweisbar sein. Die Abnahme nutzt den vorhandenen PIN-Weg, damit keine zweite Anmeldelogik entsteht. Aufgaben am Termin statt am Raum geben „bis HH:MM“ von selbst, wenn sich ein Termin verschiebt.

## Folgen und Stand

- Code: `src/AispaStudio.Api/Operations` (`RoomCare.cs`, `RoomCareEndpoints.cs`), Migration `RaumpflegeTagesstartUndAbnahme`, `CalendarDay.RoomCare` und `AppointmentDto.Warnings` in `AispaStudio.Contracts`. Web: Reiter „Hygiene“ unter „Berichte“, Checklisten bei „Standorte & Räume“.
- `QuickSwitch.ApproveAsync` und `ApproversAsync` nehmen jetzt das verlangte Recht als Parameter (Standard bleibt die Kassenaufsicht, damit die Rabattfreigabe unverändert ist).
- **Nicht geprüft:** ob es für Day-Spas in Österreich, Deutschland oder der Schweiz gesetzliche Aufzeichnungspflichten zur Hygiene gibt (etwa Hygieneplan, Aufbewahrungsfrist). Das Protokoll ist ein betriebliches Hilfsmittel und kein Rechtsnachweis; eine Aufbewahrungsfrist ist nicht festgelegt, Einträge werden nie gelöscht.
- Windows-Kalender (Nachtrag 21.10.2026): Raumpflege-Streifen grau, grün, orange mit Abhaken per Klick und Rechtsklick, Stand von Tagesstart und Raumpflege im Raumkopf, Betriebsleiste über dem Kalender (Tagesstart je Raum mit Abhaken und Abnahme per PIN, Bereitstellen-Summen, wichtige Handbuch-Einträge, Lagerwarnungen), Bereitstellen mit Haken im Terminfenster und in der Hover-Karte, Hinweise nach Buchen und Einchecken als stehenbleibende Meldung (Hilfe: `kalender-betrieb`).
- Offen: Raumpflege-Aufgaben für Bereiche (Sauna, Ruheraum) gibt es nicht, sie haben nur die Tagesstart-Kontrolle; keine Erinnerung per Nachricht bei überfälliger Raumpflege.
- Verwandte Neuerungen derselben Lieferung (Entscheidungen im Bericht und im Hilfetext): Bedarfsprüfung des Lagers, Material je Vorbereitungsschritt, „Zum Termin bereitstellen“ und Rezeptionshandbuch.

## Quellen

- [Raumpflege je Raum](../../web/src/help/content/standorte.md), [Hygiene in der Hilfe](../../web/src/help/content/hygiene.md)
- [ADR 0059](0059-zum-start-nur-die-windows-app.md), [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), [ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md)
