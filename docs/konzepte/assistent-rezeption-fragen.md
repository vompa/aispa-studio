# Fragen der Rezeption an den Assistenten

Blick: Rezeptionistin am Tresen, Telefon in der Hand, Gast vor ihr. Sie tippt schnell, klein, ohne Satzzeichen, mit Vornamen, Spitznamen oder nur „die Moser“.

Legende Stand: ✅ Thema gibt es schon (`BusinessTopic`, Tagesüberblick, Kundensuche, Freie Zeiten) · ◐ gibt es, versteht aber unscharfe Fragen schlecht oder zeigt zu wenig · ❌ fehlt.

## Grundsätze für die Behandlung in der App

1. **Antwort aus den Daten, KI erkennt nur das Thema** (ADR 0072): gilt weiter, auch ohne Claude-Schlüssel (regelbasiert).
2. **Unscharfe Namen** (Ä315): Anrede, Nachname allein, Vorname allein, Rufname („Kathi“), Kleinschreibung, Tippfehler (ein Buchstabe falsch), „die Moser“/„der Steiner“. Ein Treffer → direkt antworten. Mehrere → Auswahl als Liste (Name, Ort, nächster Termin), ein Klick beantwortet die ursprüngliche Frage. Keiner → „Keinen Gast ‚Steiner‘ gefunden“ + „Neuen Gast anlegen“.
3. **Bezug auf das, was offen ist:** „sein Termin“, „die“, „der Gast“ beziehen sich auf den offenen Termin, die gewählte Kundenkarte oder die letzte Antwort (Gesprächsfaden, nur in dieser Leiste).
4. **Antwort = kurzer Kopfsatz + Liste mit Aktionen** (Ä316): eine Zeile je Ergebnis, Zeile anklickbar (Termin öffnen, Kundenkarte, Person im Dienstplan, Artikel im Lager). Keine Doppelung von Text und Liste.
5. **Aktionen nur als Vorschlag** (Responsible AI): „Buche …“, „Verschiebe …“, „Sag … ab“ öffnet die passende Maske vorausgefüllt; gespeichert wird erst mit dem Knopf der Maske. Nie still ändern.
6. **Rechte:** Beträge, Umsätze, Personaldaten nur mit Recht; sonst sagt die Antwort, dass das Recht fehlt, statt nichts.
7. **Nicht verstanden:** sagen, was erkannt wurde, und 2–3 Vorschläge zum Antippen („Meinten Sie: nächster Termin von Christian Steiner?“); Frage bleibt stehen (Ä316). Unbeantwortete Fragen landen wie bisher in `UnansweredQuestions` zum Nachbessern.

## Kunden

| Frage (wie getippt) | Stand | Antwort in der App |
| --- | --- | --- |
| wann kommt frau moser / hat herr steiner einen termin | ◐ | Nächster Termin (Tag, Uhrzeit, Behandlung, Person), weitere geplante darunter; Zeile öffnet Termin |
| wann war die moser zuletzt da | ✅ | Letzter Besuch + Behandlung; Zeile öffnet Termin |
| historie / verlauf christian steiner | ✅ | „Kundenkarte öffnen“, nächster Termin, letzte 10 Besuche, Nichterscheinen/Absagen der letzten 12 Monate, mit Kassarecht Käufe ohne Beträge; Hilfethemen darunter (Slice 135) |
| telefonnummer von frau hofer | ◐ | Telefon und Mail aus der Kundenkarte, Knopf „Kopieren“ |
| hat die gassner eine allergie / anamnese | ◐ | Stand der Anamnese (ausgefüllt, fällig, fehlt), mit Recht auf Gesundheitsdaten die Hinweise als Stichwort (Allergie, Schwangerschaft) (Slice 135); „schick … den Bogen“ öffnet den Termin mit dem Abschnitt Anamnese (Slice 147) |
| welchen rabatt hat moser | ✅ | Kundenrabatt aus der Kartei |
| hat sie noch einen gutschein / paket offen | ✅ | Offene Wertgutscheine (auf einem Beleg des Gastes verkauft oder eingelöst) mit Restwert, Paket-Einheiten (Slice 135) |
| welche massage hatte er letztes mal / bei wem | ◐ | Letzte Behandlung + Person; „Gleich wieder buchen“ |
| wer hat heute geburtstag | ✅ | Gäste mit Termin heute und Geburtstag |
| ist frau koller stammkundin | ❌ | Besuche im Jahr, Gruppe (Stamm/regelmäßig/selten) |
| hat schwarz schon bezahlt / offene rechnung | ◐ | Offene Rechnung und Anzahlung mit Betrag, nur mit Kassarecht (Slice 135); „offene rechnung …“ springt in die Kassa mit der Rechnung (Slice 147) |
| kunde mit nummer 0664… / mail … | ✅ | Kundensuche |

## Termine

| Frage | Stand | Antwort |
| --- | --- | --- |
| termine morgen / heute nachmittag / am freitag | ✅ | Tagesüberblick als Liste (Ä316); „vormittag“ bis 12, „nachmittag“ 12–17, „abend“ ab 17 Uhr wie die Warteliste (Slice 135) |
| wann ist heute noch was frei / freie termine thai massage samstag | ✅/◐ | Freie Zeiten je Person; Zeile öffnet „Termin anlegen“ vorausgefüllt |
| hat malee morgen um 14 uhr zeit | ◐ | Ja/Nein + nächstgelegene freie Zeiten bei ihr |
| nächster freier termin für ein paar | ❌ | Paarzeiten (zwei Personen, zwei Räume oder Doppelraum) |
| wie viele termine haben wir noch heute | ✅ | Zahl + Liste |
| wer ist noch nicht eingecheckt / wer ist zu spät | ✅ | Termine nach Beginn ohne Check-in |
| wer kommt als nächstes | ✅ | Nächste 5 Termine ab jetzt (Slice 135) |
| buche frau moser donnerstag 15 uhr hot stone | ✅ | Öffnet „Neuer Termin“ vorausgefüllt (Gast, Behandlung, Person, Tag, Zeit; Fehlendes bleibt leer); gebucht wird nur per Knopf (Slice 147) |
| verschiebe den termin von steiner auf morgen | ✅ | Öffnet den Termin mit „Verschieben“, Tag und Zeit eingetragen; verschoben wird nur per Knopf (Slice 147) |
| wer ist auf der warteliste | ◐ | Warteliste mit Wunschzeit und „Kundenkarte öffnen“ (Slice 135); „biete … an“ zeigt die freien Zeiten zum Wunsch (Slice 147) |

## Absagen und Nichterscheinen

| Frage | Stand | Antwort |
| --- | --- | --- |
| was ist unsere stornoregel | ✅ | Regel aus den Buchungseinstellungen |
| frau moser sagt ab – fällt eine gebühr an | ◐ | Gebühr nach Regel und Zeitpunkt für den offenen oder nächsten Termin, Betrag nur mit Kassarecht (Slice 135); „sag … ab“ öffnet die Rückfrage zum Stornieren mit der Gebühr (Slice 147) |
| wer hat heute abgesagt | ✅ | Liste der Absagen heute mit Grund und „spät“ innerhalb der Frist (Slice 135) |
| wie oft ist steiner nicht erschienen | ◐ | Nichterscheinen/Absagen des Gastes, Hinweis bei Häufung |
| wer könnte die lücke um 15 uhr füllen | ❌ | Passende Einträge der Warteliste |

## Personal

| Frage | Stand | Antwort |
| --- | --- | --- |
| wer arbeitet heute / samstag | ✅ | Liste mit Dienstzeiten |
| ist elif morgen da / im urlaub / krank | ✅ | Status + Dienstzeit |
| wer kann hamam / wer macht thai | ✅ | Personen mit Qualifikation, heute im Dienst zuerst |
| wann hat sophie pause / wann geht sie heim | ✅ | Dienstzeit, Dienstende und Pausen aus Dienstplan und Sperrzeiten „Pause“; ohne Eintrag die verlangte Dauer nach dem Arbeitszeitrecht (Slice 136) |
| wer ist heute ersthelfer | ✅ | Ersthelfer im Dienst mit gültigem Zertifikat; sonst die Zuständigen mit Grund und Vertretung (Slice 136) |
| wer ist für die kassa / das lager zuständig | ✅ | Person und Vertretung je Zuständigkeit (Slice 136) |
| hat marco heute noch freie zeit | ◐ | Freie Zeiten der Person (nicht in Slice 136, bleibt offen) |

## Räume und Bereiche

| Frage | Stand | Antwort |
| --- | --- | --- |
| welcher raum ist frei / ist raum 3 frei | ✅ | Freie Räume jetzt; mit Zeit „um 15 uhr“ |
| wie voll ist die sauna | ✅ | Belegung Gemeinschaftsbereich |
| ist der hamam morgen gesperrt | ✅ | Raumsperren mit Art, Zeitraum und Grund; ohne Tag die nächsten zwei Monate (Slice 136) |
| wann ist der paarraum frei | ✅ | Freie Zeiten eines Raums an einem Tag aus Öffnungszeit, Belegung und Sperren; heute ab jetzt (Slice 136) |

## Lager und Verkauf

| Frage | Stand | Antwort |
| --- | --- | --- |
| haben wir das arganöl noch / wie viel bodylotion | ✅ | Bestand je Lagerort und gesamt, Hinweis unter dem Mindestbestand des Materials (Slice 136); der Mindestbestand je Lagerort fehlt noch |
| was kostet die gesichtscreme | ✅ | Verkaufspreis aus dem Artikel der Kassa; heißt er wie eine Behandlung, stehen beide da (Slice 136) |
| was ist bald aus / was muss bestellt werden | ✅ | Materialien unter dem Mindestbestand mit Bestand und schon bestellter Menge (Slice 136); Bestellen selbst ist ein Auftrag (Ä319) |
| wann kommt die lieferung | ✅ | Offene Bestellungen mit voraussichtlichem Termin (Bestelldatum plus Lieferzeit des Lieferanten, nur mit dem Recht, das Lager zu verwalten) (Slice 136) |
| was kostet die hot stone / wie lange dauert | ✅ | Preis und Dauer |
| welche aktionen laufen | ✅ | Aktionen |
| wie viel ist auf gutschein ab12… | ✅ | Restwert |
| gibt es pakete | ◐ | Pakete mit Preis und Einheiten |

## Öffnungszeiten, Feiertage, Ferien, Standort

| Frage | Stand | Antwort |
| --- | --- | --- |
| haben wir am 8. dezember offen | ✅ | Feiertag + Öffnungszeit des Tages in einer Antwort (Test in Slice 136) |
| welche feiertage sind im dezember | ✅ | Feiertage des Zeitraums |
| wann sind semesterferien / ferien in wien | ✅ | Schulferien aus Abo |
| wie lange haben wir heute offen | ✅ | Öffnungszeit |
| wie kommt man her / parken | ✅ | Anreise |
| telefonnummer / adresse von graz | ✅ | Kontakt des Standorts |

## Betrieb und Notfall

| Frage | Stand | Antwort |
| --- | --- | --- |
| wen rufe ich bei wasserrohrbruch / heizung | ✅ | Betriebskontakte |
| wie mache ich einen tagesabschluss | ✅ | Handbuch |
| kassa geht nicht / drucker druckt nicht | ◐ | Handbuch-Abschnitt Fehlerbehebung + Kontakt Haustechnik/Support |

## Umsetzung in R06

- **A – Namen und Bezug** (Slice 133) (Ä315, Grundsatz 2–3): gemeinsame Namenssuche (`NameMatch`) mit Anrede, Rufname, Tippfehler, Auswahl bei mehreren; Gesprächsbezug. Erledigt in Slice 133.
- **B – Darstellung und Bedienung** (Slice 134) (Ä316, Grundsatz 4, 7): Kopfsatz + Liste, Ladezustand, Frage bleibt, Vorschläge bei Nichtverstehen.
- **C – Neue Themen Kunden/Termine/Absagen** (Slice 135): Verlauf des Gastes, offene Gutscheine/Pakete/Rechnungen, Anamnese-Stand, nicht eingecheckt, nächste Termine, Absagen heute + Gebühr, Warteliste. Erledigt in Slice 135.
- **D – Neue Themen Personal/Räume/Lager** (Slice 136): Pausen/Dienstende, Ersthelfer, freie Zeiten eines Raums, Lagerbestand, Mindestbestand, Artikelpreis, offene Bestellungen. Erledigt in Slice 136; offen bleiben die freie Zeit einer Person, der Mindestbestand je Lagerort und ein gespeicherter, zugesagter Liefertermin.
- **E – Aktionen als Vorschlag** (Slice 147, Ä319): „buche / verschiebe / sag ab / schick den Bogen / biete an / offene rechnung“ öffnet die Maske vorausgefüllt, gespeichert wird nur per Knopf (Nachtrag Slice 147 zu ADR 0072). Erledigt in Slice 147; Aufträge an Lager und Dienstplan bleiben bewusst draußen.
