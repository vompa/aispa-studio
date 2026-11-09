# Kalender aus Sicht der Rezeption

Stand 19.10.2026. Vom Auftraggeber am 19.10.2026 freigegeben („arbeitest du bitte die Punkte ab“); umgesetzt wird in den Schritten unter [Umsetzung](#umsetzung-nach-freigabe), jeder als eigener PR.

Bilder der gebauten Oberfläche (Windows-App, aus den Bildtests der Hilfe):

- [Alltag mit Kennzeichen und markiertem Termin](../../web/src/help/images/app/kalender-markiert.png)
- [Freie Zeit suchen](../../web/src/help/images/app/freie-zeit-suchen-1.png)
- [Raumansicht](../../web/src/help/images/app/kalender-raeume.png)
- [Pause eintragen](../../web/src/help/images/app/kalender-eintrag-pause.png)
- [Wunsch am Telefon frei eintippen](../../web/src/help/images/app/kalender-ki-anfrage.png)
- [Details zu einer Buchung](../../web/src/help/images/app/kalender.png)

## Wie der Alltag an der Rezeption aussieht

Telefon am Ohr, Gast an der Theke, nebenbei kommt jemand aus der Sauna zum Zahlen. Was ich zehnmal pro Stunde mache:

1. Freie Zeit für eine Behandlung finden („Haben Sie Donnerstag noch eine Hot-Stone?“).
2. Termin buchen, meist für einen bekannten Gast.
3. Gast oder Termin finden („Ich komme um drei, auf Müller“).
4. Einchecken und kassieren.
5. Verschieben, weil jemand später kommt.
6. Überblick: wer ist frei, wer ist überfällig, was kommt als Nächstes.

Alles andere (Paar, Spa-Tag, Sperrzeit, Ausfall, Lücken schließen, Warteliste) ist seltener und darf einen Klick tiefer liegen.

## Was mich am heutigen Kalender stört

- Die linke Spalte nimmt in einem schmalen Fenster die Hälfte ein; auf dem Screenshot passt nur eine Therapeutin daneben.
- Eine Stunde ist fast 100 Pixel hoch; ein Arbeitstag braucht Scrollen.
- Es gibt keine einzige Taste: kein Suchen, kein Neuer Termin, kein Tag vor oder zurück.
- Kein Rechtsklick; jede Aktion geht über das Seitenpanel.
- Ich sehe auf dem Termin nicht, ob der Gast neu ist, Stammgast, eine Allergie hat, mit Gutschein zahlt oder die Anamnese fehlt.
- Paar und Spa-Tag erkenne ich nur am Wort „gemeinsam“; welche Einträge zusammengehören, sehe ich nicht.
- Freie Zeiten muss ich mit den Augen suchen.

## Wünsche nach Priorität

### A: zum Start (hoher Nutzen, überschaubarer Aufwand)

| # | Wunsch | Ausgestaltung |
|---|---|---|
| A1 | Kalender bekommt die Fläche | Linke Spalte entfällt. Datum oben öffnet den Monat als Klappmenü. Tagesüberblick, Sauna und Warteliste werden Zähler-Chips in der Kopfzeile; Klick öffnet das bestehende Seitenpanel. „Als nächstes“ wandert in den Tagesüberblick-Chip. |
| A2 | Ganzer Tag ohne Scrollen | Stundenhöhe zoombar (Strg + Mausrad, Strg +/−), Standard so, dass die Öffnungszeit des Standorts passt. Einstellung je Gerät merken. |
| A3 | Ein Suchfeld | Gast, Telefon oder Termin. Strg+F oder einfach lostippen. Treffer springt zum Termin und markiert ihn. |
| A4 | Tastatur für die häufigen Schritte | Siehe Tastenkürzel unten. Unterleiste zeigt die fünf wichtigsten, „?“ zeigt alle. |
| A5 | Rechtsklick | Auf Termin: nur die nächsten sinnvollen Schritte je Status (gebucht → Einchecken; da → Kassieren), dazu Kundenkarte, Folgetermin, Verschieben; Sonderfälle unter „Mehr“, Stornieren rot und zuletzt. Auf leere Zelle: Termin hier, Paar/Spa-Tag hier, Sperrzeit hier. |
| A6 | Kennzeichen im Eintrag | Kleine runde Symbole oben rechts, höchstens drei, Rest bei Hover (siehe „Symbole und Farben“). |
| A7 | Überfällig sichtbar | Termin hat begonnen, Gast nicht eingecheckt: orange statt blau, Text „10 Min. überfällig“. |
| A8 | Zusammengehörige Einträge | Paar und Spa-Tag mit Gruppen-Etikett („Paar mit Lisa Koller“, „Spa-Tag 2/3“) in einer Gruppenfarbe; Auswahl eines Teils hebt alle Teile hervor. |
| A9 | Freie Zeit suchen | Behandlung wählen, alle passenden Lücken leuchten über alle Personen (inklusive Raum und Gerät, die Logik liefert der `ResourcePlanner`). Pfeiltasten wechseln, Enter bucht. |
| A10 | Nur wer heute arbeitet | Spalten nur für Personen mit Dienst; „alle zeigen“ im Klappmenü am Spaltenkopf. Auslastung je Person klein im Kopf. |

### B: kurz danach

- B1 Ansicht 3 Tage und Woche (für eine Person oder einen Raum), Tasten T, 3, W.
- B2 Termin per Tastatur verschieben: Alt+↑/↓ um 15 Minuten, Alt+←/→ zur nächsten Person. Strg+Z macht rückgängig (gibt es heute schon als Knopf).
- B3 Kopieren und Einfügen: Strg+C auf Termin, Strg+V in eine Zelle bucht denselben Gast mit derselben Behandlung (Folgetermin).
- B4 Hover-Karte: Notiz, Hinweise, letzter Besuch, offene Anzahlung, ohne zu klicken.
- B5 Vorbereitung je Behandlung (Öl, Steine vorheizen, Raum): kleines Symbol, Details im Panel; Liste „Mein Tag“ für das Team gibt es schon.

### C: später oder nur auf Wunsch

- Farbe nach Behandlungskategorie statt nach Status (Einstellung).
- Spalten frei sortieren und ausblenden, je Gerät gespeichert.
- Vergangene Stunden einklappen.

## Details zu einer Buchung

**Öffnen:** Doppelklick, Enter oder Alt+Enter auf einem Termin. Ein einfacher Klick wählt nur aus, und die Unterleiste zeigt dann die passenden Tasten. Esc schließt. Strg+Enter öffnet statt der Details die volle Kundenkarte.

**Form:** Eine breite Leiste von rechts über dem Kalender. Der markierte Termin bleibt links sichtbar. Alle Abschnitte stehen ohne Reiter auf einer Seite, so dass am Telefon nichts gesucht werden muss. Pfeil hoch oder runter bei offener Leiste springt zum nächsten Termin in der Spalte.

**Inhalt:**

| Abschnitt | Inhalt |
|---|---|
| Kopf | Name, Sterne, Kunde seit, Sprache, Telefon, Mail. Darunter die Kennzeichen als Chips: Hinweis in der Anamnese (nur „vorhanden“, Inhalt nur mit Recht), FEST, Braucht mehr Zeit, Stand der Anamnese. |
| Dieser Termin | Zeit, Behandlung, Dauer mit Vor- und Nachbereitung, Therapeut:in, Raum mit Vorbereitung, Gerät, Preis, Quelle mit Person, die gebucht hat, Status mit Uhrzeit. |
| Heute noch gebucht | Alle Teile desselben Besuchs (Spa-Tag, Paar, Sauna, Essen), gekauftes Paket mit offenem Rest. |
| Letzter Besuch | Datum, Behandlung, Therapeut:in, Rückmeldung der Therapeut:in, gekaufte Produkte, Zufriedenheit aus der Umfrage. |
| Wünsche und Rezeptionsvermerk | Die beiden Vermerkfelder aus der Kundenkartei. |
| Zahlung | Bevorzugte Zahlungsart (aus der Kassa gelernt: zuletzt dreimal Karte), hinterlegte Karte (nur die letzten vier Ziffern), Gutscheinguthaben, offene Beträge, Rechnungsempfänger (Firma oder privat). |
| Kommende Termine | Nächste Termine mit Serie (↻ alle 4 Wochen). Darunter Besuche, Abstand, nicht erschienen; Umsatz nur mit Recht. |
| Aktionen | Ein Hauptknopf je Status (Einchecken, Kassieren), dazu Folgetermin, Verschieben, Notiz, „Mehr“ mit Erledigt, Nicht erschienen, Gast tauschen, Änderungsprotokoll und Stornieren. |

Die Leiste lädt aus dem lokalen Speicher und ist sofort da. Was fehlt (etwa die Umfrage), kommt nach.

## Raumansicht

Heute fehlt dort, wer behandelt. Künftig steht das Kürzel der Therapeutin (Avatar in ihrer Farbe) vor dem Gastnamen, bei Paar beide. Der Spaltenkopf zeigt Raumart und Plätze („2 Liegen (Paar)“). Umgekehrt zeigt die Teamansicht den Raum in der Hover-Karte und, wenn eng, als kurzes Kürzel. Rechtsklick „Raum tauschen“ bietet nur passende freie Räume an.

## Gästeszenarien

| Szenario | Was die Rezeption braucht | Darstellung |
|---|---|---|
| Hotelgast, internationales Publikum | Sprache, Zimmernummer, Abreise, Arrangement inklusive | Hotelgast mit Zimmer und „aufs Zimmer“ bewusst weggelassen ([ADR 0058](../entscheidungen/0058-kein-hotelgast-mit-zimmerbuchung.md)); gebaut ist nur die Sprache. Ursprünglicher Vorschlag: Sprachkürzel (EN, IT, FR …) als Kennzeichen; Zeile „Zi. 214 · Abreise morgen“; Arrangement-Leistungen zeigen „inkl.“ statt Preis. Zahlung „aufs Zimmer“ ist eine Zahlungsart der Kassa mit Beleg und Übergabe an das Hotelsystem, nie am Kassenbeleg vorbei. Hotelgäste aus einer PMS-Schnittstelle (später) kommen mit Zimmer und Abreise. |
| Sprache der Therapeut:in | Wer spricht Englisch, Italienisch? | Sprachen als Eigenschaft der Mitarbeitenden; „Freie Zeit suchen“ bevorzugt passende Sprache und markiert sie. |
| Eigenbuchung (online, KI, Treatwell, Hotel) | Woher kommt der Termin, ist er bestätigt, ist angezahlt? | Quelle klein unter dem Termin nur, wenn nicht von der Rezeption selbst gebucht. Unbestätigte Eigenbuchungen gestrichelt bis zur Bestätigung (falls Freigabe nötig). |
| Stammgast mit fester Therapeutin | Termin darf nicht stillschweigend zu jemand anderem wandern | Kennzeichen „FEST“. „Freie Zeit suchen“ zeigt zuerst die Zeiten dieser Person. Verschieben auf eine andere Person fragt nach. Fällt die Person aus, nennt der Ausfall-Assistent diese Gäste zuerst. |
| Wunschtherapeut:in (nicht fest) | Wunsch berücksichtigen, aber flexibel | Bevorzugt in der Suche, kein Kennzeichen im Kalender, nur in der Hover-Karte. |
| Wunsch Therapeutin oder Therapeut | Nur passende Personen vorschlagen | ♀ / ♂ als Wunsch-Kennzeichen (nicht als Angabe zum Gast). |
| Paar, Gruppe, Firmen- oder Hochzeitsgruppe | Alle zusammen sehen und verschieben | Gruppenfarbe und Etikett; Gruppen über 2 Personen mit Namen der Gruppe („JGA Huber · 6“). |
| Spa-Tag mit mehreren Schritten | Ablauf über Personen, Räume und Sauna | „Spa-Tag 2/3“ plus nächster Schritt; Auswahl hebt alle Teile hervor, auch den Sauna-Eintritt. |
| Walk-in | Sofort freie Person jetzt | Taste N ohne markierte Zelle bucht ab jetzt; Vorschlag der nächsten freien Person. |
| No-Show-Risiko | Wer ist schon zweimal nicht erschienen? | Kennzeichen „!“ mit Hover „2× nicht erschienen“; Anzahlung kann verlangt werden ([ADR 0047](../entscheidungen/0047-anzahlung-und-vorauszahlung.md)). |
| VIP oder Sonderbehandlung | Leitung will informiert sein | Wird als Hinweis „!“ geführt, Text in der Hover-Karte. Kein eigenes Symbol, damit es übersichtlich bleibt. |
| Barrierefreiheit, Schwangerschaft, Minderjährige | Raum und Behandlung passend wählen | Über den Hinweis „!“ und die Anamnese; Inhalt nur für Berechtigte. |
| Gutschein, Paket, Abo | Zahlt nicht bar, Restguthaben | „G“ für Gutschein, Paket und Abo zeigen „Paket 3/10“ in der Hover-Karte. |

**Rangfolge der Kennzeichen** (höchstens drei sichtbar): ! Hinweis, A Anamnese, FEST, Sprache, ★ Stammgast oder N neu, Wunsch ♀/♂, G, €. Alles Weitere steht in der Hover-Karte.

**Neue Daten, die der Kalender-Endpunkt dafür liefern muss:** Sprache des Gastes, Sprachen der Mitarbeitenden, feste und Wunsch-Therapeut:in, Hotelgast mit Zimmer, Abreise und Arrangement, Buchungsquelle, Zahl der Besuche und der Nicht-Erscheinen, Gruppenname. Teile davon gibt es schon (Quelle bei Online und KI, Gruppen, Anzahlung); der Rest kommt als kleine Erweiterung der Kundenkarte.

## Pausen, Vorbereitung, Abwesenheiten und Notizen

Das Backend kennt das meiste schon: Dienstplan mit Urlaub, Krankenstand, Schulung und frei (`RosterKind`), Sperrzeiten für Person, Raum und Gerät, Vor- und Nachbereitung je Behandlung und Raumpflege. Im Kalender ist davon heute nur die Schraffur „nicht im Dienst“ zu sehen.

| Thema | Darstellung | Bedienung |
|---|---|---|
| Pause | Eigener grauer Block mit Text „Pause“. Gesetzliche Pausen (Arbeitszeitregeln AT/DE/CH) sind als „gesetzlich“ markiert. Eine flexible Pause wird gestrichelt mit ihrem Fenster gezeigt („14:30–16:00“) und rutscht beim Buchen mit, solange sie ins Fenster passt. | Rechtsklick auf eine Zelle: „Pause hier“. Ziehen verschiebt sie. Der Kalender warnt, wenn eine gesetzliche Pause verloren geht. |
| Vor- und Nachbereitung | Schmaler, gemusterter Streifen direkt über bzw. unter dem Termin mit Text („Steine heizen“). Er ist nicht buchbar und wandert beim Verschieben mit. | Die Dauer kommt aus der Behandlung. Für einen einzelnen Termin lässt sie sich unter „Mehr“ ändern. |
| Raumpflege | Streifen nur in der Raumansicht, in der Teamansicht nur im Hover. | wie oben |
| Krankenstand | Die Spalte bleibt sichtbar, rot schraffiert, mit Hinweis „krank gemeldet“. Betroffene Termine sind rot gestrichelt und zeigen einen Vorschlag zur Umbuchung. Oben in der Spalte steht „3 Termine umbuchen“; das öffnet den Ausfall-Assistenten. | Krankmeldung über Rechtsklick auf den Spaltenkopf oder im Dienstplan. |
| Urlaub, Schulung, frei | Keine leere Spalte, sondern (seit Runde 5, Ä31, ohne das Band „Heute“) die schraffierte Spalte mit Grund unter „Alle zeigen“ und, wenn die Person offene Termine hat, die Meldung „Abwesenheit“ („Abwesend: Elif Demir, Urlaub bis 23.10.“ stand früher in der Tagesnotiz-Leiste). Wer trotzdem eingeplant wird, löst eine Warnung aus. | „Alle zeigen“ am Spaltenkopf blendet die Spalte wieder ein. |
| Teilabwesenheit (Arzt 10–12) | Sperrzeit in der Spalte mit Grund. | Rechtsklick: „Sperrzeit hier“, Taste B. |
| Tagesnotiz | Gruppe „Notizen heute“ ganz oben in der Seitenleiste „Meldungen“ für den ganzen Standort (Lieferung, Wartung, Gruppe kommt); früher eine gelbe Leiste über dem Kalender (seit Runde 5, Ä31 entfernt). Bei Notizen trägt der Chip „Meldungen“ einen Punkt. | „+ Notiz“ in der Gruppe oder Taste „.“ |
| Notiz an einer Zeit | Gelber Zettel in einer Spalte ohne Termin („Herr Lang ruft zurück“). Er blockiert nichts und lässt sich abhaken. | Rechtsklick: „Notiz hier“. |
| Notiz am Termin | Kennzeichen nur, wenn ein Hinweis da ist; der Text steht in der Hover-Karte. | Im Panel: „Notiz hinzufügen“ bzw. der Stift an der Hinweisfläche (seit Runde 5, Ä33). |

## Gästepräferenzen, wiederkehrende Termine und eigene Pakete

| Thema | Was der Kalender tut |
|---|---|
| Bevorzugte Therapeut:in | Wird in der Kundenkarte gepflegt oder aus der Historie vorgeschlagen (z. B. „4 von 5 Besuchen bei Lena“). „Freie Zeit suchen“ und die KI-Terminannahme zeigen zuerst deren Zeiten. Ist sie nur bevorzugt, steht das in der Hover-Karte; ist sie fest, trägt der Eintrag das Kennzeichen FEST (siehe oben). |
| Bevorzugte Zeiten | Etwa „Do. nachmittags“, „nie vor 10“. Werden aus der Historie vorgeschlagen und bei der Suche hervorgehoben. Bei Ausfall bietet der Assistent zuerst passende Zeiten an. |
| Präferenzen aus Empfehlungen | Wer über eine Empfehlung kommt („Frau Gruber empfiehlt Lena“), bekommt die empfohlene Person als Wunsch. Die Quelle der Empfehlung steht in der Kundenkarte, nicht im Kalender. |
| Weitere Vorlieben (Öl, Druck, Raumtemperatur, Musik, wenig Gespräch) | In der Hover-Karte und in „Mein Tag“ der Therapeutin, nicht im Eintrag. |
| Wiederkehrende Termine | „Wiederholen“ im Termin: alle 1–8 Wochen, an einem festen Wochentag oder „4 Wochen nach dem letzten Besuch“, mit Enddatum oder Anzahl. Der Eintrag bekommt ein kleines ↻ in der Hover-Karte. Konflikte (Urlaub der Therapeutin; ein Feiertag ist keiner, Ä50) werden beim Anlegen gezeigt und lassen sich einzeln lösen. Ändern fragt nach „nur diesen“ oder „diesen und alle folgenden“. |
| Folgetermin beim Kassieren | Nach dem Kassieren schlägt die Kassa den nächsten Termin mit derselben Person zum gewohnten Abstand vor (Taste F). |
| Eigenes Paket (Gast stellt zusammen) | „Spa-Tag“ wird zum Baukasten: Sauna-Zeitfenster, Behandlungen, Ruheraum und Essen in beliebiger Reihenfolge. Der Planer schlägt die Reihenfolge mit den wenigsten Wartezeiten vor. Im Kalender erscheint es wie ein Spa-Tag (Gruppenfarbe, „Paket 2/4“). Den Preis setzt die Kassa aus den Einzelpreisen oder einem Paketpreis zusammen. |
| Paket und Abo (gekauft) | z. B. 10er-Block Massage. Die Hover-Karte zeigt „Paket 3/10“, und die Kassa zieht den Termin ab. Ein Kennzeichen gibt es nur, wenn das Paket bald abläuft oder aufgebraucht ist. |

## Kundenkartei: Kennzahlen, Kennzeichen und anspruchsvolle Gäste

### Kennzahlen (automatisch, aus Kassa und Kalender)

Oben auf der Kundenkarte stehen die Kennzahlen, darunter die Vermerke:

| Kennzahl | Inhalt | Wer sieht es |
|---|---|---|
| Umsatz | gesamt und letzte 12 Monate, aus den Kassenbelegen (nie von der KI gerechnet) | Leitung; die Rezeption nur mit dem Recht „Zahlen sehen“ |
| Besuche und Behandlungen | Anzahl, letzter Besuch, üblicher Abstand („alle 5 Wochen“) | alle |
| Je Therapeut:in | Anzahl und Umsatz je Person, daraus wird die bevorzugte Therapeut:in vorgeschlagen | Anzahl alle, Umsatz nur Leitung |
| Zuverlässigkeit | nicht erschienen, späte Absagen (innerhalb der Stornofrist), Verspätungen, alles mit Datum | alle |
| Offen | Restbetrag, Anzahlung, Gutscheinguthaben, Paket 3/10 | alle |

### Sterne: Wert für das Haus, nicht Sympathie

Ich empfehle Sterne, die das System aus objektiven Daten berechnet. Eine Bewertung nach Laune der Mitarbeitenden empfehle ich nicht:

- ★ Stammgast: mindestens 3 Besuche in 12 Monaten
- ★★ mindestens 6 Besuche oder ein Umsatz über einer Schwelle
- ★★★ Top 5 % des Standorts

Die Schwellen legt die Leitung fest. Sterne steuern Begrüßung, Vorrang auf der Warteliste (nur wenn die Leitung das einschaltet) und Auswertungen. Im Kalender erscheint ★ wie bisher. Eine Bewertung „schwierig/einfach“ durch Personal ist ungenau, verletzt leicht das Sachlichkeitsgebot und muss dem Gast auf Anfrage herausgegeben werden (Art. 15 DSGVO).

### Kennzeichen statt langer Vermerke

Statt eines Freitexts „schwierige Kundin“ gibt es feste, sachliche Kennzeichen, die ein Verhalten oder einen Bedarf beschreiben. Jedes hat eine Wirkung im Kalender:

| Kennzeichen | Herkunft | Wirkung |
|---|---|---|
| Braucht mehr Zeit | Rezeption | +10 Min. Puffer automatisch beim Buchen |
| Kommt oft zu spät | automatisch (3 Verspätungen in 12 Monaten) | Erinnerung 2 h vorher immer, kein Termin direkt vor einer Paarbehandlung |
| Späte Absagen / nicht erschienen | automatisch | Anzahlung oder Karte als Garantie verlangen (gibt es schon), auch bei der KI |
| Nur erfahrene Therapeut:in | Rezeption, Leitung | Planer schlägt nur Personen mit dieser Qualifikation vor |
| Feste / bevorzugte Therapeut:in | Historie, Gast | siehe oben |
| Beschwerde offen | aus Reklamation, Storno mit Grund oder Gutschrift an der Kassa | Leitung wird beim nächsten Termin informiert; endet, wenn die Beschwerde erledigt ist |
| Leitung informieren | Leitung | Hinweis beim Einchecken |
| Nicht bei mir | Therapeut:in selbst, für sich (Grund optional, nur Leitung sieht ihn) | Planer und KI schlagen diese Person für den Gast nicht mehr vor. Bestehende Termine bei ihr werden der Rezeption zur Umbuchung gemeldet. Der Gast sieht nur, dass die Person nicht verfügbar ist. Die Leitung sieht eine Liste aller Ausschlüsse; nur die Therapeut:in selbst oder die Leitung kann einen Ausschluss aufheben. Schließen alle passenden Personen einen Gast aus, gibt die KI an die Rezeption ab. |
| Nicht mehr buchen | nur Leitung, mit Grund und Datum | KI und Online-Buchung schlagen nichts vor und geben an die Rezeption ab; die Rezeption kann mit Recht trotzdem buchen |

Automatische Kennzeichen sind ein **Vorschlag**, den die Rezeption bestätigt. Sie laufen nach 12 Monaten ohne neuen Vorfall ab (Speicherbegrenzung).

### Vermerke in zwei Feldern

- **Wünsche des Gastes:** Öl, Druck, Musik, Temperatur, „wenig Gespräch“. Sichtbar für Therapeut:innen in „Mein Tag“ und in der Hover-Karte, gedacht als Service.
- **Rezeptionsvermerk:** Organisatorisches wie „zahlt immer mit Firmenkarte“ oder „Mann holt ab“. Er ist nur für Rezeption und Leitung sichtbar.

Gesundheitsangaben gehören in keines der beiden Felder, sondern verschlüsselt in die Anamnese (Schema `health`). Erkennt die Eingabe Gesundheitswörter („Allergie“, „schwanger“), weist sie darauf hin. Beide Felder werden so geschrieben, als würde der Gast sie lesen, denn er hat ein Recht auf Auskunft.

### Anspruchsvolle Gäste erkennen

Anspruchsvolle Gäste erkennt man am besten an Fakten, die das System ohnehin hat:

1. Beschwerden und Reklamationen: Storno oder Gutschrift mit Grund „Reklamation“, Wechsel der Therapeut:in nach einem Termin.
2. Umbuchungen, späte Absagen, nicht erschienen.
3. Verspätungen: Zeit des Eincheckens gegenüber dem Beginn.
4. Aufwand in Anfragen: viele Rückfragen oder abgelehnte Vorschläge vor einer Buchung.
5. Kurze Rückmeldung der Therapeut:in nach dem Termin, freiwillig mit festen Auswahlpunkten: „mehr Zeit gebraucht“, „Wunsch geändert“, „unzufrieden“. Ein Freitext ist dafür nicht vorgesehen.

Daraus schlägt das System die passenden Kennzeichen oben vor. Ein Gesamturteil über die Person bildet es nicht.

### Sollen sie bei der KI-Terminvergabe nachgereiht werden?

Meine Empfehlung: **nicht verdeckt nachreihen, sondern sichtbar anders behandeln.**

- Die KI wendet nur die Wirkung der Kennzeichen an: Puffer, erfahrene Therapeut:in, Anzahlung, Erinnerung. Damit wird der Termin für beide Seiten besser, und der Gast merkt keine Benachteiligung.
- Bei „Nicht mehr buchen“ oder einer offenen Beschwerde bucht die KI nicht selbst, sondern legt die Anfrage der Rezeption vor. Ein Mensch entscheidet; das hält Art. 22 DSGVO ein (keine ausschließlich automatisierte Entscheidung mit erheblicher Wirkung).
- Vorrang gibt es nur positiv und offen: Wer möchte, kann Stammgäste auf der Warteliste zuerst benachrichtigen. Die Regel steht in den Einstellungen und in der Datenschutzerklärung.
- Ein heimliches „schlechtere Zeiten für schwierige Gäste“ empfehle ich nicht. Es ist schwer zu begründen, wenn der Gast Auskunft verlangt, es kostet Umsatz, und es spricht sich herum.

### Wie andere Anbieter das handhaben

Ausgewertet habe ich die Hilfeseiten von Fresha, Phorest, Mangomint, Zenoti, Vagaro, Timely, Salonized, Mindbody und Boulevard.

- **Warnhinweise** sind überall Freitext. Sie erscheinen beim Buchen, beim Einchecken oder beim Öffnen des Profils (Fresha „Staff alerts“, Phorest „Prompts“, Zenoti, Mindbody). Feste Kennzeichen mit einer Wirkung im Kalender hat kein Anbieter.
- **Sperren** gibt es nur von Hand, meist nur für die Online-Buchung. Timely sperrt auch Warteliste, Telefon und Mail und storniert auf Wunsch die künftigen Termine gleich mit. Phorest lässt sich mit einer neuen Mailadresse umgehen. Eine automatische Sperre nach mehreren No-Shows habe ich bei keinem Anbieter gefunden.
- **Gegen No-Shows** arbeiten alle mit hinterlegter Karte, Anzahlung und Gebühr (Fresha, Mangomint, Vagaro, Salonized). Salonized zählt No-Shows auf der Kundenkarte und kann nach Wiederholungstätern filtern.
- **Kundenwert:** Zenoti zeigt die Zahl der Besuche, die übrigen Anbieter eine Zeitleiste. Den Umsatz je Therapeut:in auf der Kundenkarte habe ich bei keinem Anbieter gefunden.
- **Sterne-Bewertung von Gästen durch das Personal** gibt es bei keinem Anbieter.
- **Ausschluss je Person:** Vagaro kann Neukunden für einzelne Mitarbeitende sperren. Einen Ausschluss durch die Therapeut:in selbst, wie wir ihn planen, habe ich nicht gefunden.

Folgerung: Kennzeichen mit Wirkung, berechnete Sterne, Umsatz je Therapeut:in und „Nicht bei mir“ würden AISPA von der Konkurrenz abheben. Von Timely übernehmen wir die Sperre über alle Kanäle, das Mitstornieren künftiger Termine und den Hinweis mit Grund, wenn das Team trotzdem bucht. Damit eine Sperre nicht so leicht umgangen wird wie bei Phorest, prüfen wir neue Gäste beim Anlegen auf Ähnlichkeit (Name, Telefon, Mail). Bei einem Treffer erscheint ein Hinweis an die Rezeption, eine automatische Ablehnung gibt es nicht.

Quellen: [Fresha Staff alerts](https://www.fresha.com/help-center/knowledge-base/clients/54-add-staff-alerts-to-client-profiles), [Fresha Sperre](https://www.fresha.com/help-center/knowledge-base/clients/346-block-clients-from-booking), [Phorest Prompts](https://phorest.helpjuice.com/44037-category-1/305330-how-do-i-setup-an-important-alert-for-a-client-for-when-they-check-in), [Phorest Sperre](https://phorest.helpjuice.com/44077-clients-GB/334152-how-do-i-stop-a-client-from-booking-online), [Mangomint Sperre](https://www.mangomint.com/learn/block-a-client-from-online-booking.md), [Zenoti Gastprofil](https://help.zenoti.com/en/appointments/manage-guest-experience/guest-profile-v2.html), [Vagaro Anzahlung](https://www.vagaro.com/learn/deposits-for-hair-appointment-service), [Vagaro Sperre je Mitarbeiter:in](https://www.vagaro.com/en-au/pro/updates/limit-customer-booking), [Timely Sperre](https://www.gettimely.com/support/whats-new/block-problem-clients), [Salonized Sperre](https://help.salonized.com/en/articles/3000976-block-customers-for-online-bookings), [Salonized No-Shows](https://salonized.com/en/blog/reduce-no-shows).

### DSGVO kurz

- Kennzahlen und Kennzeichen sind Profiling. Sie gehören in die Datenschutzerklärung des Spas, mit Zweck (Terminplanung, Service) und Speicherdauer. Eine Vorlage liefern wir mit.
- Auskunft (Art. 15): Der Export der Kundendaten enthält Kennzeichen, Vermerke und Kennzahlen, deshalb sind die Formulierungen sachlich. Berichtigung (Art. 16): Kennzeichen lassen sich mit Begründung entfernen, die Änderung wird protokolliert.
- Datenminimierung: feste Auswahl statt Freitext, Ablauf nach 12 Monaten, Umsatz nur für Berechtigte.
- Ausschluss durch Therapeut:innen: Der Grund bleibt intern und ist sachlich zu formulieren. Er erscheint aber in der Auskunft, wenn er gespeichert ist; deshalb ist er optional.
- Art. 22: Keine automatische Ablehnung; „Nicht mehr buchen“ setzt nur die Leitung, und auch dann entscheidet ein Mensch über die einzelne Anfrage.

### Abstimmung

Die Kundenkartei selbst (Rabatte, Pflichtfelder, Kundendaten ändern) baut der Thread „Windows-App ausbauen“. Kennzahlen, Kennzeichen und Vermerkfelder baut dieser Schritt; abgestimmt am 19.10.2026: Die Kundenkarte gehört dem Rezeptions-Schritt, wichtige Knöpfe nicht unten rechts (dort sitzt der Lotus-Knopf), neue Felder kommen ins Handbuch „Seite Kunden“.

## KI-Terminvorschlag als Hauptfunktion

Grundlage im Repo: Die Terminannahme (`MailIntake`, PRs #8 bis #84) schlägt bei Mail, SMS, WhatsApp und Anruf schon Termine vor. Der Assistent (`/api/assistant`, `AssistantTask.FreeSlots`) versteht freie Fragen. Wiederholen gibt es heute nur bei einem Fehler (`/retry`). Es gilt die bestehende Regel: Die KI versteht den Wunsch, die freien Zeiten rechnet der `ResourcePlanner`. Ohne API-Schlüssel arbeitet sie regelbasiert.

**1. Wunsch am Telefon frei eintippen.** Mit Strg+K, oder im Suchfeld, wenn der Text kein Name ist. Ein Beispiel: „Termin morgen 17:00, eine Frau, etwas stärkere Massage, davor Sauna, nicht bei Marco“. Daraus werden bearbeitbare Chips:
- Tag und Uhrzeit: Di. 20.10., ab ca. 17:00
- Therapeutin (weiblich). Unklares fragt die KI über ein Klappmenü nach: Ist mit „eine Frau“ die Behandlerin gemeint oder eine Gästin?
- Behandlung nach Merkmal: Massage mit kräftigem Druck, also Sportmassage oder Tiefengewebe. Dafür bekommen Behandlungen Merkmale wie Druck, Dauer, Öl oder ohne Öl, Paar-tauglich.
- Kombination: davor Sauna, 60 Minuten. Das wird ein kleines Paket, wie beim Spa-Tag.
- Ausschluss: nicht Marco Berger.
- Gast: Kundenkarte suchen oder neu anlegen. Präferenzen aus der Kundenkarte (feste Therapeutin, Sprache) kommen automatisch als Chips dazu.

Darunter stehen die drei besten Vorschläge mit Person, Raum, Preis und Abstand zur Wunschzeit. Sie leuchten zugleich im Kalender. Enter bucht den ersten, 2 und 3 die anderen. Danach kann man sie mit Strg+S als Vorschlag an den Gast schicken (SMS oder Mail mit Bestätigungslink).

**2. Neu vorschlagen.** Man ergänzt den Wunsch („lieber Donnerstag“, „ohne Sauna“) oder löscht einen Chip, und die Liste rechnet sich sofort neu. Das funktioniert genauso im Posteingang:
- Bestätigt der Gast den Vorschlag nicht oder antwortet „passt nicht, lieber am Abend“, verknüpft der Posteingang die Antwort mit der Anfrage. Die KI macht dann einen neuen Vorschlag mit dem ergänzten Wunsch und schließt bereits abgelehnte Zeiten aus.
- Die Rezeption sieht den Verlauf (Vorschlag 1 abgelehnt, Vorschlag 2) und kann jederzeit „Neu vorschlagen“ drücken, mit oder ohne eigenen Hinweis.
- Vorgeschlagene Zeiten werden kurz vorgemerkt (Vorschlag läuft, z. B. 30 Minuten), damit sie in der Zwischenzeit nicht jemand anderes bekommt. Im Kalender erscheinen sie gestrichelt.

**3. Bausteine im Backend** (für die Umsetzung): Die Absicht `FreeSlots` wird um Geschlechterwunsch, Ausschlüsse, Behandlungsmerkmale und Kombinationen erweitert. Der Planer erhält eine Suche mit diesen Filtern und einer Rangfolge (Wunschzeit, feste Therapeutin, Sprache, wenig Lücken). Ein Vorschlag wird eine kurze Vormerkung. Antworten auf Vorschläge werden der Anfrage zugeordnet.

## Anfragen: Antworten überarbeiten und übersetzen

Heute erkennt der Posteingang die Sprache einer Nachricht (`InboundMail.Language`), und die KI schreibt ihren Antwortvorschlag in der Sprache des Gastes. Eine Übersetzung für die Rezeption gibt es noch nicht.

| Funktion | Ausgestaltung |
|---|---|
| Eingang übersetzt anzeigen | Jede Nachricht in einer anderen Sprache als der Sprache des Spas erscheint gleich übersetzt, mit dem Vermerk „aus dem Italienischen übersetzt“. Der Knopf „Original“ (Taste O) schaltet zwischen Übersetzung und Original um, beide bleiben gespeichert. Namen, Daten, Uhrzeiten und Preise werden nicht übersetzt, sondern gegen das Original geprüft. Weicht eine Zahl ab, zeigt der Posteingang das Original. |
| Antwort in zwei Sprachen | Die Rezeption schreibt oder überarbeitet die Antwort in ihrer Sprache. Daneben steht die Fassung in der Sprache des Gastes, die gesendet wird. Wer sie ändert, sieht die Rückübersetzung zur Kontrolle. |
| Mit KI überarbeiten | Knöpfe unter dem Entwurf: „Freundlicher formulieren“, „Kürzer formulieren“, „Siezen“, „Duzen“, „Entschuldigung ergänzen“. Dazu das freie Feld „Eigener Hinweis“, zum Beispiel „Parkplatz erwähnen“. Jede Überarbeitung lässt sich zurücknehmen (Strg+Z), und die Fassungen bleiben im Verlauf. Zahlen im Text (Uhrzeit, Preis) übernimmt die KI nur aus den Daten des Vorschlags. Erfindet sie eine, wird der Entwurf verworfen (bestehende Regel). |
| Sprache wählen | Die Sprache des Gastes kommt aus der Kundenkarte oder der Erkennung und lässt sich im Entwurf umstellen. Sie wird für spätere Nachrichten und Erinnerungen in der Kundenkarte gespeichert. |
| Telefon | Die Abschrift eines Anrufs wird wie eine Nachricht übersetzt, und die SMS-Antwort geht in der Sprache des Anrufers hinaus. |
| KI-Hinweis | Der Hinweis auf die KI wird in der Sprache des Gastes angehängt, wie bisher. |
| Ohne KI | Ohne API-Schlüssel gibt es keine Übersetzung. Die Nachricht wird im Original gezeigt, mit dem Vermerk „Übersetzung nicht verfügbar“. |

Die Übersetzung läuft über die Wolverine-Queue, nicht im HTTP-Aufruf. Sie ist meist fertig, bevor jemand die Nachricht öffnet; bis dahin steht das Original da.

Sprachen der Gästetexte: Deutsch, Englisch, Italienisch, Französisch, dazu weitere nach Bedarf. Rechtliche Texte (Einwilligung, Ansage) werden nur in gegengelesenen Fassungen verschickt, nie frei übersetzt.

## Was die Konkurrenz macht und was wir übernehmen

Grundlage: Herstellerhilfen und Bewertungen (Capterra, GetApp, trusted.de) zu Mangomint, Zenoti, Trybe, Book4Time, Fresha, Timely, Boulevard, Phorest, Shore und Planity. Zu TAC, SpaSoft und Treatwell Connect gab es nichts Belastbares zum Kalender. Die Quellen stehen in der Liste unten.

| Idee | Wer | Bewertung | Entscheidung |
|---|---|---|---|
| Farbe = Status, Form = Zeitart (umrandet Puffer, halbtransparent Einwirkzeit, schraffiert Sperre), Symbol = Gastmerkmal | Mangomint | klarste Trennung am Markt; Kritik: keine Farbe je Leistung | **übernehmen**, Kategorie-Farbe als Einstellung (C) |
| Symbole: Wunschbehandler (Stern), M/F-Wunsch, NEW, Mitgliedschaft, Formular offen | Mangomint, Zenoti | bestätigt unsere Kennzeichen | **übernehmen**; Stern bei uns = Stammgast, Wunschbehandler = FEST |
| Wunsch-Therapeut:in je Person zählen | Zenoti | gute Kennzahl für Bindung | **übernehmen** in Berichte (später) |
| Einwirkzeit: Therapeut frei, Raum belegt, für andere buchbar | Fresha, Boulevard | fehlt bei uns; wichtig für Packungen und Masken; Phorest-Kritik „Albtraum“ zeigt, dass es einfach sein muss | **übernehmen** als dritte Zusatzzeit neben Vor- und Nachbereitung (B) |
| Sperrzeit-Vorlagen mit Typ, Dauer, bezahlt/unbezahlt, für Person, Raum oder beides | Fresha, Trybe | spart Klicks; Kritik: Serien nur einzeln löschbar (Trybe), Pausen nicht für mehrere (Phorest) | **übernehmen** mit Serien für mehrere Personen und „ganze Serie löschen“ |
| Abwesenheit mit Grund sperrt Buchung automatisch | Planity | haben wir über den Dienstplan | **sichtbar machen** (siehe oben) |
| Paar in einem Schritt, automatisch größerer Raum, verknüpft | Book4Time, Trybe | haben wir; Kritik an Book4Time: Gruppen schwer zu verschieben | **behalten**, Gruppe verschieben per Ziehen anbieten |
| Schnelle Terminanlage im Schwebefenster, aufklappbar | Shore | weniger Klicks als volles Panel | **übernehmen** für N |
| Tastenkürzel, Privatmodus (blendet Namen und Preise aus), Sperrbildschirm | Timely | Privatmodus wichtig, wenn Gäste auf den Bildschirm sehen | **übernehmen**: Strg+H Privatmodus, Sperren über den bestehenden PIN-Schnellwechsel |
| Überfällig ohne Check-in wird markiert | Phorest (grau) | bei uns orange, damit es auffällt | **übernehmen** |
| Online-Anfragen schraffiert, bis bestätigt | Shore | passt zu „Vorschlag läuft“ | **übernehmen** (gestrichelt) |
| Lückenarme Vorschläge, lange Blöcke freihalten | Boulevard | haben wir mit `ResourcePlanner.Fit` | **behalten**, in der KI-Rangfolge nutzen |
| Umsatz vor Rezeption verbergen | Zenoti | deckt sich mit unserem Vorschlag | **übernehmen** (Recht) |
| Änderungsprotokoll je Termin | Zenoti | hilfreich bei Streit („wer hat verschoben?“) | **übernehmen** in der Hover-Karte unter „Mehr“ (B) |
| Tagesplan drucken | Zenoti | manche Häuser wollen Papier für Therapeut:innen | später |
| Feste 15/30-Minuten-Raster | Fresha (Kritik) | erzeugt Lücken | **vermeiden**: Raster 5 Min. beim Ziehen, Anzeige 15 Min. |
| Langsam, überladen | Zenoti, Mindbody (Kritik) | unser Hauptargument | Ziel: jede Aktion ohne Wartezeit (lokal, optimistisch) |

Quellen: [Mangomint Farben und Symbole](https://www.mangomint.com/learn/calendar-color-key-icons), [Zenoti Terminbuch](https://resources.zenoti.com/blogs/13-things-you-should-be-doing-in-your-appointment-book), [Trybe Sperrzeiten](https://help.try.be/en/articles/234822-blocked-times), [Trybe Therapeutenwahl](https://help.try.be/en/articles/662793-practitioner-selection-internally), [Fresha Sperrzeit](https://www.fresha.com/help-center/knowledge-base/calendar/18-set-up-and-manage-blocked-time), [Fresha Zusatzzeit](https://www.fresha.com/help-center/knowledge-base/catalog/101630-add-extra-time-to-services-and-appointments-1), [Timely Tastenkürzel](https://help.gettimely.com/hc/en-gb/articles/1500002195362-How-to-use-keyboard-shortcuts-in-the-Timely-calendar), [Boulevard](https://www.joinblvd.com/blog/ai-powered-scheduling), [Phorest graue Termine](https://phorest.helpjuice.com/333939-why-have-my-appointments-gone-grey), [Shore bei trusted.de](https://trusted.de/shore), [Book4Time bei Capterra](https://www.capterra.com/p/120575/Book4Time/reviews/), [Zenoti bei Capterra](https://www.capterra.com/p/131057/ZENOTI/reviews/), [Phorest bei Capterra](https://capterra.com/p/113530/Phorest-Salon-Software/reviews/), [Fresha bei Capterra](https://www.capterra.com/p/142138/Shedul-com/reviews/?page=6).

## Was ausgeblendet wird und nur auf Wunsch erscheint

| Heute sichtbar | Künftig |
|---|---|
| Standort-Auswahl als eigene Karte | Nur wenn mehrere Standorte; dann im Datumsmenü. |
| Mini-Monat dauerhaft | Klappmenü am Datum (Taste G für „Gehe zu“). |
| Tagesüberblick mit vier Kacheln und Umsatz | Ein Chip „17 Termine · 2 da · 1 offen“. Umsatz nur mit Leitungsrecht im aufgeklappten Panel. |
| Sauna und Ruheraum als Karte | Chip „Sauna 10/12“, Klick öffnet Bereichs-Panel. |
| Warteliste als Karte | Chip mit Zahl, orange wenn jemand wartet. |
| Als nächstes | Im Tagesüberblick-Panel. |
| Paar / Spa-Tag als eigener Knopf | Im Pfeil-Menü von „+ Termin“ nach dem Trenner (Sonderfälle), ebenso Sauna-Eintritt, Sperrzeit, Warteliste, Ausfall einer Person, Lücken schließen. |
| Im Termin-Panel fünf Knöpfe gleich groß | Ein Hauptknopf je Status (Einchecken, dann Kassieren). Erledigt, Nicht erschienen und Stornieren unter „Mehr“. |
| Spalten ohne Dienst | Ausgeblendet. |
| Hinweis „Zum Verschieben ziehen“ | Entfällt; steht in der Tastenhilfe. |

## Symbole und Farben (Fragen des Auftraggebers)

**Farben.** Empfehlung: Farbe trägt genau eine Bedeutung, nämlich den Status: blau gebucht, grün da, orange überfällig, grau erledigt, rot gestrichelt nicht erschienen. Mehr Bedeutungen in der Fläche machen den Kalender bunt und unlesbar. Die einzige zweite Farbe ist die Gruppenfarbe als Etikett für Paar und Spa-Tag. Wer lieber nach Behandlungskategorie färbt, schaltet das in den Einstellungen um (C).

**Gender-Symbole.** Empfehlung: nicht standardmäßig. Für die Einteilung zählt nicht das Geschlecht des Gastes, sondern sein Wunsch („nur Therapeutin“, „nur Therapeut“). Dieser Wunsch wird ein Kennzeichen (♀ bzw. ♂ als Wunsch, nicht als Angabe zur Person), und die Planung schlägt nur passende Personen vor. Das ist zugleich datensparsam.

**Stammgast.** Ja, als ★; Hover zeigt die Zahl der Besuche und den letzten Besuch. Erstbesuch als „N“, damit die Rezeption begrüßt und die Anamnese prüft.

**Kennzeichen im Eintrag** (höchstens drei sichtbar, Reihenfolge nach Wichtigkeit):

| Zeichen | Bedeutung | Quelle |
|---|---|---|
| ! | Hinweis oder Allergie | Kundenkarte (nur „hat Hinweis“, Gesundheitsdaten bleiben im Schema `health`) |
| A | Anamnese fehlt oder abgelaufen | Anamnese-Stand |
| ★ | Stammgast | Besuchszahl |
| N | Erstbesuch | Besuchszahl |
| ♀ / ♂ | Wunsch Therapeutin / Therapeut | Kundenkarte |
| G | zahlt mit Gutschein | Buchung |
| € | Anzahlung oder Rest offen | Kassa |

**Besonderheiten zur Behandlung.** Ins Etikett gehört nur, was die Einteilung ändert: Dauer, wenn abweichend („80 Min.“), Paar, Spa-Tag. Vorbereitung (Öl, Steine, Raum) als kleines Werkzeug-Symbol, Details im Panel und in „Mein Tag“ (B5).

**Paar und Spa-Tag.** Etikett mit Gruppenfarbe und Partner bzw. Schritt („Paar mit Stefan Koller“, „Spa-Tag 1/3 · Sauna 16–17“). Auswahl hebt alle Teile hervor; Verschieben fragt „nur diesen“ oder „ganze Gruppe“ (Gruppe verschieben gibt es im Backend schon).

## Tastenkürzel

| Taste | Wirkung |
|---|---|
| N | Neuer Termin (in der markierten Zelle oder jetzt) |
| Strg+N | Freie Zeit suchen |
| P / S / B | Paar oder Spa-Tag / Sauna-Eintritt / Sperrzeit |
| Strg+F oder lostippen | Suchen |
| Strg+K | Wunsch frei eintippen (KI-Anfrage) |
| Strg+H | Privatmodus: Namen und Preise ausblenden |
| ← / → | Tag zurück / vor; Pos1 = heute |
| G | Datum wählen |
| T / 3 / W | Tag / 3 Tage / Woche |
| ↑ ↓ ← → (Termin markiert) | Nächster Termin in der Spalte / Nachbarspalte |
| Enter, Alt+Enter, Doppelklick | Details zur Buchung |
| Strg+Enter | Kundenkarte |
| E | Einchecken |
| K | Kassieren (öffnet die Kassa mit dem Termin) |
| F | Folgetermin |
| V oder Alt+Pfeile | Verschieben |
| Entf | Stornieren (mit Rückfrage und Grund) |
| Strg+Z | Rückgängig |
| Strg+Mausrad, Strg +/− | Zoom |
| Esc | Panel oder Modus schließen |
| ? | Alle Tasten anzeigen |

Einbuchstaben-Tasten wirken nur, wenn kein Eingabefeld den Fokus hat.

## Umsetzung (nach Freigabe)

1. A1, A2, A10: Kopfzeile, Chips, Zoom, Spalten nur mit Dienst. Reine Oberfläche in `CalendarView`. **Gebaut in #130.**
2. A3, A4, A5: Suche, Tasten, Rechtsklick, Tastenhilfe. **Gebaut in #132.** „Lostippen“ startet die Suche nicht, weil die Buchstaben selbst Tasten sind; der Folgetermin liegt vorerst vier Wochen später.
3. A6, A7, A8 und Raumansicht: Kennzeichen, Überfällig, Gruppen, Therapeut:in in der Raumansicht. Braucht zusätzliche Felder im Kalender-Endpunkt (Besuchszahl, Hinweis vorhanden, Anamnese-Stand, Zahlungsart, Wunsch Therapeut:in). **Gebaut in #133** mit „!“, „★“ und „N“; Anamnese-Stand, Wunsch Therapeut:in, Gutschein und Anzahlung folgen mit ihren Daten (Schritte 7 bis 9).
4. A9: Freie Zeit suchen über den bestehenden Endpunkt für freie Zeiten, mit Vorrang für feste Therapeut:in und Sprache. **Gebaut in #134**; den Vorrang für feste Therapeut:in und Sprache bringt Schritt 7, sobald es diese Daten gibt.
5. Pausen, Vorbereitung, Abwesenheiten und Notizen sichtbar machen (Daten gibt es weitgehend). **Gebaut in #136**: Tagesleiste, Pause, Notiz und Sperrzeit per Rechtsklick, Streifen für Vor- und Nachbereitung und Raumpflege, krank gemeldete Spalte mit markierten Terminen. Krankmeldung über den Spaltenkopf und der Ausfall-Assistent in der Windows-App **gebaut in #137**; die flexible Pause, die beim Buchen mitrutscht, und das Ziehen von Pausen kommen danach.
6. KI-Anfrage am Telefon und „Neu vorschlagen“ in Kalender und Posteingang (erweitert Assistent, Planer und Posteingang), dazu Übersetzen mit Original auf Knopfdruck und KI-Überarbeitung von Antworten. KI-Anfrage im Kalender (Strg+K) mit Bausteinen, Ausschlüssen, Ergänzen und „Andere Zeiten“ **gebaut in #138**; Geschlechterwunsch und Behandlungsmerkmale brauchen erst Felder in Personal und Behandlungen, Kombinationen kommen mit dem Paket-Baukasten (Schritt 7), der Gast als Baustein mit der Kundenkarte (Schritt 8). Übersetzen mit Original auf Knopfdruck (Taste O), deutsche Kontrollfassung der Antwort und KI-Überarbeitung mit Rückgängig (Strg+Z) im Posteingang der Windows-App **gebaut in #140**; die Antwort auf Deutsch schreiben und übersetzen lassen und die Sprache im Entwurf umstellen folgen mit der Kundenkarte (Schritt 8). „Neu vorschlagen“ mit Hinweis, Verknüpfung der Antwort des Gastes mit seiner Anfrage und Auslassen schon vorgeschlagener Zeiten **gebaut in #142**; die kurze Vormerkung vorgeschlagener Zeiten (gestrichelt im Kalender) und der Versand eines Vorschlags aus dem Kalender (Strg+S) folgen.
7. Präferenzen, wiederkehrende Termine und Paket-Baukasten (braucht neue Daten in der Kundenkarte und im Planer). Vorlieben (feste oder bevorzugte Therapeut:in, Wunschtage und -zeiten, Vorschlag aus den Besuchen, FEST im Kalender, Vorrang in der Terminannahme) **gebaut in #146**; die Gewichtung der Wunschzeiten in „Freie Zeit suchen“ folgt. Serientermine mit Vorschau, Konflikten, ↻ im Kalender und „nur diesen / alle folgenden“ **gebaut in #148**; Spa-Tag als Baukasten (Sauna und Ruheraum als Teil, Reihenfolge mit den wenigsten Wartezeiten vom Planer, Ablauf im Kalender) **gebaut in #154**; gekaufte Pakete (10er-Block) mit Paketpreis, Abzug an der Kassa und „Paket 7 von 10“ in der Hover-Karte **gebaut in #156**; Paketpreise für den Baukasten und Wiederholen schon beim Buchen folgen.
8. Kundenkartei: Kennzahlen, Sterne, Kennzeichen mit Wirkung, zwei Vermerkfelder (nach Absprache mit „Windows-App ausbauen“). Kennzahlen, berechnete Sterne, Wünsche des Gastes und Rezeptionsvermerk mit Hinweis bei Gesundheitswörtern **gebaut in #157**; Kennzeichen mit Wirkung (Braucht mehr Zeit, Leitung informieren, Nicht bei mir, Nicht mehr buchen) **gebaut in #158**; automatische Kennzeichen als Vorschlag (kommt oft zu spät, späte Absagen, Beschwerde offen, nur erfahrene Therapeut:in), die Meldung bestehender Termine bei „Nicht bei mir“, einstellbare Schwellen für die Sterne und Verspätungen folgen.
9. Gästeszenarien: Sprache, Hotelgast mit Zimmer und Zahlungsart „aufs Zimmer“ (über die Kassa), feste Therapeut:in, Quelle. Feste Therapeut:in kam mit #146. Sprache des Gastes als Kürzel am Termin, Sprachen der Mitarbeitenden, „Freie Zeit suchen“ nach Sprache und Quelle des Termins **gebaut in #159**; Hotelgast mit Zimmer, Abreise, Arrangement und „aufs Zimmer“ (9b) **bewusst weggelassen** ([ADR 0058](../entscheidungen/0058-kein-hotelgast-mit-zimmerbuchung.md)), Wunsch Therapeutin oder Therapeut **gebaut in #164**.

Ergänzt auf Wunsch des Auftraggebers: Passt beim Verschieben die volle Dauer nicht mehr, bietet der Hinweis an, den Termin auf die längste passende Dauer zu verkürzen (höchstens bis zur Hälfte, mindestens 15 Minuten). **Gebaut in #160.**

Aus der Recherche zu typischen Buchungsproblemen (Buchungsprobleme): Verspätung. Die Rezeption trägt „Kommt später“ am Termin ein, der Kalender zeigt die Ankunft, der Planer sagt, ob der Termin bis zum bisherigen Ende verkürzt oder ganz nach hinten passt. **Gebaut in #162**; die Meldung durch den Gast selbst über den Termin-Link folgt mit dem Bestätigungslink.

Gruppen und Firmen-Events mit Gästen ohne Namen („Name folgt“, im Kalender „Gast 3 · Name folgt“, Namen im Termin nachtragen, bis zu acht Gäste) **gebaut in #163**; Ausfallgebühr nur bei der buchenden Person folgt mit der Anzahlung.

Wunsch Therapeutin oder Therapeut (♀/♂ als Wunsch des Gastes in den Vorlieben, Angabe an der Person im Team, Planer und „Freie Zeit suchen“ schlagen nur passende Personen vor, Hinweis am Termin bei abweichender Person) **gebaut in #164**; die Auswahl durch den Gast bei der Online-Buchung folgt.

Anamnese-Hinweis beim Buchen („Anamnese prüfen“ an der Behandlung, Hinweis im Panel „Neuer Termin“ ohne Inhalt, ohne Sperre) **gebaut in #166**; der Hinweis bei Paar und Spa-Tag und in „Mein Tag“ der Therapeutin folgt.

Anzeige bestätigt/unbestätigt („✓ zugesagt“ am Termin, „unbestätigt“ im Tagesüberblick, **Noch nicht zugesagt** hebt diese Gäste hervor) **gebaut in #168**, auf der Zusage per Link aus #165.

Jeder Schritt ein eigener PR mit Bildern aus den ViewTests.

## Nachtrag 21.10.2026: Vor- und Nachbereitung sichtbar, Zeiten je Termin, Einwirkzeit, Rufname

Anlass: Durchsicht des Auftraggebers am 21.10.2026 („aktuell fehlt mir die Anzeige der Vor- und Nachlaufzeit zum Termin“). Befund: Die Streifen aus Schritt 5 hatten weder Hintergrund noch Rand und zeigten Text erst ab 14 Pixel Höhe; bei 5 Minuten Vorbereitung und „Tag einpassen“ blieb nichts zu sehen. Das Termin-Panel nannte die gebundene Zeit gar nicht. Dazu zwei Wünsche aus derselben Durchsicht: Rufnamen für Mitarbeitende (lange thailändische Namen) und der Tageskopf der 7-Tage-Ansicht, der beim Klick nichts tat. Die Schritte werden auf `dev` gebaut, jeder mit eigenem Commit; die Spezifikation steht hier vor dem Bauen, nach der Regel „erst auf Papier, dann bauen“.

### Schritt 10: Streifen sichtbar, gebundene Zeit im Panel, Tageskopf springt zum Tag

- Streifen bekommen den Hintergrund `Surface3` mit 1 px Rand `LineStrong`, oben bzw. unten abgerundet, mindestens 6 px hoch. Text ab 14 px („Vorb. 5 Min.“, „Nachb. 10 Min.“), ab 20 px mit dem ersten Vorbereitungsschritt („Vorb. 5 · Steine heizen“). Die Hover-Karte bleibt die Vollfassung.
- Termin-Panel: unter der Uhrzeit „Gebunden 10:25–12:00 · Vorb. 5 · Nachb. 10 Min.“ oder „Keine Vor- und Nachbereitung“.
- Beim Ziehen zeigt die Vorschau die gebundene Zeit über und unter dem Termin in Grau.
- 7-Tage-Ansicht: Klick auf den Kopf eines Tages hebt den Filter auf und zeigt diesen Tag mit allen Spalten; Hinweis „Klick: zu diesem Tag“.

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

### Schritt 11: Rufname für Mitarbeitende

Wie bei Kunden (ADR 0064): `PreferredName` an der Person, höchstens 40 Zeichen. Im Haus (Kalender, Hover-Karte, Mein Tag, Tagesstart, Freie Zeit suchen, KI-Anfrage, Posteingang, Erkennung aus Texten) gilt der Rufname; nach außen (Lohn, Export, Zugänge, Einladungen, Belege) der volle Name. Verwaltung › Team: Feld „Name im Haus“ unter „Vollständiger Name“. Nachtrag in ADR 0064.

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

### Schritt 12: Zeiten je Termin und Gast-Puffer getrennt

**Daten.** `Appointment` bekommt `PreparationMinutes`, `FollowUpMinutes` und `ExtraFollowUpMinutes` (Puffer aus dem Kennzeichen „Braucht mehr Zeit“). Sie werden beim Buchen aus dem Plan übernommen. Bestehende Termine werden aus `Start − BlockedFrom` bzw. `BlockedUntil − End` zurückgefüllt, der Puffer mit 0 (rückwirkend nicht trennbar). `AppointmentDto` liefert die drei Werte.

**Regeln.**
- Verschieben, Verspätung („Kommt später“) und Verkürzen rechnen mit den Minuten des Termins, nicht mehr mit denen der Behandlung. Eine Änderung an der Behandlung gilt nur für neue Buchungen (wie bei der Raumpflege).
- Neuer Endpunkt `PUT /api/appointments/{id}/times` mit `PreparationMinutes` und `FollowUpMinutes` (je 0 bis 120), Recht `Book`, nur bei Status Gebucht oder Eingecheckt. Der Planer prüft dieselbe Person, denselben Raum und dasselbe Gerät zur selben Startzeit mit den neuen Minuten; passt es nicht, antwortet er mit 409 und der Begründung („Lena Huber ist zu dieser Zeit schon belegt“). Bei Erfolg Live-Update wie beim Verschieben. Der Puffer bleibt, wie er ist.
- Paar und Spa-Tag: die Zeiten gelten je Termin, nicht für die Gruppe.

**Anzeige (Windows-App).**
- Streifen: „Nachb. 10 · Puffer 10 Min.“; Hover-Karte: „Ablauf: Vorb. 5 · Nachb. 10 · Puffer 10 Min.“; Panel: „Gebunden 10:25–12:10 · Vorb. 5 · Nachb. 10 · Puffer 10 Min.“.
- Weichen die Minuten von der Behandlung ab, steht im Panel und in der Hover-Karte dahinter „(geändert)“.
- Das Kennzeichen „+10“ am Termin bleibt.

**Bedienung.** Termin-Panel › „Mehr“ › „Zeiten dieses Termins ändern …“ öffnet im Panel zwei Zahlenfelder (0 bis 120, Schritte von 5, vorbelegt mit dem Termin) und „Übernehmen“; Esc bricht ab. Derselbe Eintrag steht im Rechtsklick-Menü auf einem Streifen, vor „Behandlungszeiten bearbeiten …“ (das weiter die Behandlung im Katalog ändert). Eine Ablehnung des Planers erscheint als Fehlermeldung unten mit seiner Begründung.

**Wer.** Sehen: alle mit Kalenderzugang. Ändern: wer buchen darf. Web-Kalender zeigt den gebundenen Bereich wie bisher, ohne Ändern.

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

### Schritt 13: Einwirkzeit (Raum belegt, Person frei)

Aus dem Konkurrenzvergleich oben („übernehmen als dritte Zusatzzeit“): bei Packungen und Masken liegt der Gast allein im Raum; die Person kann in der Zeit einen anderen Gast behandeln.

**Daten.** `Treatment.HandsOnMinutes` (Minuten am Gast bis zur Einwirkzeit) und `Treatment.ProcessingMinutes` (Einwirkzeit), beide 0 bis 120; ohne Einwirkzeit sind beide 0. Regel: `HandsOnMinutes + ProcessingMinutes ≤ DurationMinutes`, und Einwirkzeit nur mit `HandsOnMinutes > 0`. Die Dauer bleibt die ganze Zeit des Gastes im Raum; die Ausarbeitung danach ist `Dauer − Hände − Einwirkzeit` (darf 0 sein). Der Termin übernimmt beide Werte beim Buchen (wie Schritt 12), damit Verschieben sie behält.

**Belegung.** Die Person bekommt zwei Zeilen in `ResourceBookings`: von `BlockedFrom` bis `Start + Hände` und von `Start + Hände + Einwirkzeit` bis `BlockedUntil` (die zweite entfällt, wenn sie leer ist). Raum und Gerät wie bisher. Der Planer verlangt, dass die Person in beiden Fenstern frei und im Dienst ist; die Ausschlussbedingung der Datenbank (ADR 0023) gilt je Zeile und bleibt unverändert. Lückenbewertung, Pausenprüfung und Lückenvorschläge rechnen vorerst mit der ganzen Bindung (vorsichtig, nie zu optimistisch). Ein Klick in die Einwirkzeit einer Person bucht nicht; „Freie Zeit suchen“ und die Terminannahme finden die Zeit, weil sie über den Planer gehen.

**Anzeige (Windows-App).** Im Termin der Person liegt ab `Start + Hände` ein flaches Band „Einwirkzeit 20 Min. · frei“ im Ton der Vor- und Nachbereitung mit Linien oben und unten (zuerst halbtransparent schraffiert, am 09.11.2026 auf Wunsch des Auftraggebers geändert); in der Raumansicht kein Band (der Raum ist belegt). Liegt ein zweiter Termin der Person in der Einwirkzeit, teilt sich die Spalte dort in zwei Bahnen zu je halber Breite, der neue Termin rechts. Hover-Karte: „Ablauf: Vorb. 5 · Hände 20 · Einwirken 20 · Ausarbeiten 20 · Nachb. 10 Min.“. Panel: dieselbe Zeile unter „Gebunden“.

**Verwaltung › Behandlungen.** Zwei Felder „Am Gast bis Einwirkzeit (min)“ und „Einwirkzeit (min)“ neben Vor- und Nachbereitung, mit Hilfe. Vollkosten: die Personalzeit zählt die Einwirkzeit nicht. Beispieldaten: zwei Behandlungen mit Einwirkzeit (Körperpackung, Gesichtsmaske).

**Wer.** Sehen: alle. Ändern: Katalogpflege (`ManageCatalog`). Offen für später: „Mein Tag“ zeigt die Einwirkzeit als freie Zeit.

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

## Nachtrag 21.10.2026 (abends): Änderungswünsche des Auftraggebers Ä1 bis Ä11 nach dem Ausprobieren unter Windows

Gesammelt im Chat, freigegeben mit „fertig“. Seitenleisten folgen dem neuen Abschnitt 5.12 des [Design-Briefs](../design.md). Drei Blöcke, jeder ein Commit auf `dev`.

### Schritt 14 (Block A): Kopfzeile des Kalenders

- **Ä1, Ä11 „Alle Mitarbeitenden“:** kein Kontrollkästchen mehr, sondern eine Schaltfläche rechts unter „+ Termin“ (zweite Zeile der Kopfzeile, rechtsbündig). Sie erscheint **nur**, wenn es an diesem Standort und Tag Personen gibt, die ohne die Schaltfläche nicht sichtbar wären (ohne Dienst hier, Dienst an einem anderen Standort). Text und Farbe je Zustand: aus „Alle zeigen (1)“ als sekundärer Knopf; an „Nur mit Dienst“ als Knopf mit Fläche accent-soft, Text accent, Rahmen accent. Beim Umschalten bleibt die Spaltenbreite der vorhandenen Spalten stabil: zusätzliche Spalten bekommen dieselbe Breite, der Kalender scrollt waagrecht, wenn die Mindestbreite (208 px) sonst unterschritten würde (heute werden alle Spalten gestaucht, weil `ColumnWidthFor` aus der Spaltenzahl rechnet). Personen von einem anderen Standort zeigen im Kopf unter dem Namen den Standort mit Symbol (Stufe „Klein“, fg-2) und darunter die Dienstzeit, getrennt.
- **Ä2 Team / Räume:** der Umschalter bekommt je ein Symbol 16 (Personen, Tür) vor dem Text, der Text ist 600.

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

### Schritt 15 (Block B): Seitenleisten nach Abschnitt 5.12

- **Ä4 Reiter unten:** jede offene Seitenleiste ist ein Reiter am unteren Rand (Punkt 7 in 5.12); nicht lösbar.
- **Ä5 Zeitbalken im Termin-Panel:** unter der Zeit-Karte ein Balken (Höhe 10, Radius 4, Abstände 4) mit Abschnitten proportional zu den Minuten: Vorbereitung (surface-3, schraffiert), Behandlung (Statusfarbe des Termins; bei Einwirkzeit dreigeteilt Hände / Einwirken halbtransparent / Ausarbeiten), Nachbereitung (surface-3), Gast-Puffer (warn-soft), in der Raumansicht zusätzlich Raumpflege (surface-3 mit Zustand). Über den Abschnitten die Minuten, unter dem Balken Beginn und Ende der Bindung. Darunter die Zeile „Vorb. 10 · Behandlung 45 · Nachb. 10 Min.“ mit „Puffer 10“ und „(geändert)“ wie bisher. Klick auf den Vor- oder Nachbereitungsabschnitt öffnet die Zahlenfelder aus Schritt 12 an Ort und Stelle (Recht wie dort); „Mehr › Zeiten dieses Termins ändern …“ bleibt.
- **Ä7, Ä9 Layout:** Termin-Panel, Warteliste, Freie Zeit, Wunsch, Raumpflege, Kundendaten und Raum-Infos werden auf die Punkte 1 bis 10 von 5.12 gebracht (Warteliste ist das Vorbild und ändert sich nur, wo sie abweicht).

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

### Schritt 16 (Block C): Meldungen

- **Ä3, Ä7, Ä8 Meldungs-Seitenleiste „Meldungen“:** ersetzt die gelben Bänder über dem Kalender, die Betriebsleiste und die Seitenleiste „Tagesstart“. Inhalt: Gruppen nach Schwere (Fehler danger, Warnungen warn, Hinweise accent-soft, Erledigt blass) mit Zwischentitel und Zähler; je Meldung eine Karte nach 5.12 Punkt 5: Symbol der Art links, Titel („Pause“, „Lager“, „Raumpflege überfällig“, „Tagesstart offen“, „Räume unter Mindestbestand“), Kurztext, rechts oben die Pille der Art, unten die **eine** passende Aktion als sekundärer Knopf („Pause eintragen“, „Anzeigen“, „Abhaken“, „Tagesstart öffnen“) und „Wegklicken“ als Ghost (bis zum nächsten Auslöser ausgeblendet). Klick auf die Karte öffnet die **Detailansicht** in derselben Seitenleiste (Reiter): voller Wortlaut, betroffene Person/Raum/Material mit Links (Kalender springt hin), Uhrzeit des Auslösers, Verlauf (seit wann), die Aktion als primärer Knopf, darunter **KI-Hilfe**: „Was tun?“ mit drei Sätzen aus dem Assistenten zur gewählten Meldung (nutzt den bestehenden Rollen-Assistenten mit dem Handbuch; ohne KI-Schlüssel der Handbuch-Abschnitt zur Meldungsart), Vorschläge als Knöpfe wie in „KI und Hilfe“ (ADR 0066). Die Tagesstart-Kontrolle ist eine Gruppe darin.
- **Ä6 Zugang:** rechts in der ersten Zeile der Kopfzeile, auf einer Linie mit „+ Termin“ und links davon, die Schaltfläche „Meldungen“ mit Zähler („Meldungen 2“); bei mindestens einer Warnung Fläche warn-soft und Text warn, bei Fehlern danger-soft/danger, sonst sekundär; ohne Meldungen „Meldungen“ ohne Zähler. Sie öffnet die Seitenleiste. Die Schaltfläche „Tagesstart öffnen“ und die Betriebsleiste entfallen.
- **Ä8 Spaltenköpfe:** keine Warntexte mehr im Kopf („n Raumpflege offen“, „3 Termine umbuchen“ bleibt als eigener Zustand bei Krankmeldung). Stattdessen höchstens ein Symbol rechts im Kopf (Dreieck warn, bei Fehlern danger) mit Hinweis (Kurzfassung); Klick öffnet „Meldungen“ gefiltert auf diese Person bzw. diesen Raum.
- **Ä10 live:** Meldungen entstehen und verschwinden ohne Neuladen. Der Server veröffentlicht Änderungen an Meldungen über die bestehende Gruppe `location:{id}` (neue Nachricht `OperationsChangedMessage` aus Lager, Raumpflege, Tagesstart, Pausenprüfung); die App hält die Liste und aktualisiert Zähler, Kopf-Symbole und die Seitenleiste sofort. Zeitabhängige Meldungen (überfällig ab Uhrzeit) löst die App über ihren Minutentakt aus. Offline: letzter Stand, Hinweis „ohne Verbindung“ im Kopf der Seitenleiste.
- **Wer:** sehen alle mit Kalenderzugang; Aktionen mit dem Recht der jeweiligen Aktion (Pause eintragen: `Book`; Raumpflege abhaken und Tagesstart: `ChangeStatus`; Lager anzeigen: `ViewCalendar`).

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

### Schritt 17 (Block D): Zweite Runde des Auftraggebers vom 21.10.2026 (Ä12 bis Ä17), freigegeben mit „fertig für das erste“

Werte und Regeln stehen im Design-Brief: Kopfzeilen-Chip (5.8), Reiter (5.12 Punkt 7, geändert), Kopfzeile, Spaltenköpfe, Hinweis am Kopf und Streifen (Abschnitt 7).

- **Ä12 Raumköpfe wie Personenköpfe:** zwei Zeilen (Name gekürzt, Kurzinfo), Paarraum als Symbol statt Etikett, Warn-Symbol rechts; die Raumansicht war mit drei bis vier Zeilen je Kopf überladen.
- **Ä13 Streifen ohne Kästchen:** Vor-/Nachbereitung und Raumpflege zeigen nur Text und Zustandsfarbe; Klick hakt weiter ab.
- **Ä14 Hinweis am Kopf reduziert:** fünf Zeilen mit Symbolen statt der Liste aller Behandlungen; gilt für Räume und Personen.
- **Ä15 Reiter:** neuester ganz links, höchstens zwei sichtbar, Klappliste „⋯ n“ für weitere, Pfeile zum Blättern; kein waagrechtes Scrollen der Schiene.
- **Ä16 Kopfzeile:** höchstens zwei Zeilen; Warteliste in Zeile 2 links, Personen- bzw. Raumfilter rechts auf derselben Linie; bei Platzmangel kompakte Chips, dann Scrollen; Regel gilt für alle Kopfzeilen der App.
- **Ä17 Chips:** alle Kopfzeilen-Elemente im Muster des Zähler-Chips (Höhe 34, Kapsel, Symbol 16, Text 14), Zustände nur über weiche Flächen; „Meldungen“ und der Team/Räume-Umschalter eingeschlossen.

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

### Schritt 18 (Block F): Konfliktmeldung in Ortszeit, Bahnen statt Überlagerung, Notizen mit Endzeit (Ä21 bis Ä23, 21.10.2026, „fertig“)

Werte im Design-Brief: Abschnitt 7 (Bahnen, Konfliktmeldung), 5.9 (Hinweis).

- **Ä21 Konfliktmeldung in Ortszeit:** der Server schreibt keine Uhrzeiten mehr in den Text der Ablehnung („Termin: belegt 2026-10-21 07:40–08:50 UTC“ entfällt). `BookingConflict` trägt zusätzlich `From`/`Until` (UTC), `AppointmentId` (Termin dahinter) und `IsBlock` (Sperrzeit); `Message` bei einer Sperre über Belegtem ist „Zeitraum ist belegt.“, `Reason` „hat hier einen Termin“ bzw. „ist gesperrt“, `ResourceName` der Gast bzw. der Grund der Sperrzeit. Der Planer füllt dieselben Felder, wenn eine Person, ein Raum oder ein Gerät belegt ist. Die Windows-App zeigt bei Sperrzeit, Pause, Buchen, Paar/Spa-Tag und Zeiten ändern die Meldung `ConflictNotice` (Titel, je Belegung eine Zeile mit Symbol und Ortszeit, Klick auf eine Terminzeile markiert den Termin im Kalender, Handlungszeile); beim Verschieben, Verspätung und Verkürzen steht dieselbe Liste in einer Zeile in der Meldung unten. Der Web-Kalender formatiert nur die Zeiten in Ortszeit (`time.ts`).
- **Ä22 Keine Überlagerung in der Spalte:** Termine, Sperrzeiten, Pausen und Zettel teilen sich bei Überschneidung die Spaltenbreite in Bahnen (Termine links; Sperrzeit oder Zettel neben einem Termin in einer schmalen rechten Bahn, ein Drittel, mindestens 72 px). Gilt in Personen- und Raumspalten.
- **Ä23 Notizen mit Endzeit:** `CalendarNote.Until` (optional, nur mit Uhrzeit und nach ihr, Fehlertext „Das Ende muss nach dem Beginn liegen.“). Im Formular „Notiz hier“ das Feld **Bis (optional)** neben **Uhrzeit**; mit Ende reicht der Zettel über die Dauer (mindestens 18 px, Text oben) und zeigt „10:00–10:30 Herr Lang ruft zurück“, ohne Ende wie bisher. Eine Notiz blockiert weiter nichts.

**Gebaut auf `dev`** (Commit siehe PR-Protokoll).

### Schritt 19 (Block A): Eingabefelder nach Fluent 2 (Ä18, Ä20, Ä24, 22.10.2026)

Werte und Verhalten stehen im Design-Brief: 5.2, 5.3, 5.16 bis 5.20; die Umstellung F1 bis F12 in 11.4. Auf dem Branch `fluent2`, nur Windows-App.

- **Ä18 Markieren beim Betreten:** Tab (und Umschalt+Tab) in Uhrzeit, Datum, Zahl, Betrag und Suche markiert den Inhalt, damit das Tippen ihn ersetzt; in einem Textfeld steht der Cursor am Ende. Ein Klick setzt immer nur den Cursor.
- **Ä20 Falsche Uhrzeit:** `99` im Ende-Feld brachte die App früher zum Absturz (`FormatException`). Jetzt meldet das Feld unter sich „Die Uhrzeit liegt zwischen 00:00 und 23:59.“ bzw. „Bitte die Uhrzeit im Format 09:30 eingeben.“, der Text bleibt zum Verbessern stehen. Dazu lesen alle Uhrzeitfelder großzügig: `9`, `930`, `9.30`, `9,30`, `9:5` ergeben 09:00, 09:30, 09:30, 09:30, 09:05; ein Ende vor dem Beginn meldet das Ende-Feld („Ende liegt vor dem Beginn.“).
- **Ä24 Markierung und Platzhalter:** markierter Text steht in Weiß bzw. Dunkel auf der Akzentfarbe (Kontrast 7,7:1 hell, 6,9:1 dunkel); der graue Platzhalter war halb durchsichtig und kaum zu lesen (etwa 2,4:1 hell, 3,1:1 dunkel) und ist jetzt voll deckend (8,1:1).
- **Was die Rezeption sonst anders erlebt:** ↑ ↓ ändern Uhrzeit (5 Minuten), Datum (Tag), Zahl und Betrag; **Esc** stellt einen geänderten Wert zuerst wieder her, erst das zweite **Esc** schließt die Seitenleiste; **Enter** bestätigt das Feld und löst bei Sperren, Pause, Notiz und Zeiten ändern den Knopf aus; ein Betrag mit mehr als zwei Nachkommastellen wird gemeldet statt gerundet; die Suchfelder haben ein Kreuz zum Leeren, und im Kalender schließt das erste **Esc** die Trefferliste. Das Handbuch beschreibt es unter „Tastenkürzel im Kalender“, Abschnitt „Felder ausfüllen“.

**Gebaut auf `fluent2`** (Commits siehe PR-Protokoll).

### Schritt 20 (Blöcke B bis D): Knöpfe, Reiter, Menüs und Rückfragen nach Fluent 2 (Ä19, 22.10.2026)

Werte und Verhalten stehen im Design-Brief: 5.1, 5.2, 5.4, 5.10, 5.12 (Punkt 7), 5.21, 5.22, 5.24, 5.27; die Umstellung F13 bis F21 in 11.4. Auf dem Branch `fluent2`, nur Windows-App.

- **Knöpfe:** ein deaktivierter Knopf sagt im Tooltip, wann er wieder geht („Verfügbar, sobald ein Gast gewählt ist“). Alle Knöpfe sind mindestens 96 breit, ein gewählter Umschalt-Knopf trägt einen Haken. Tooltips erscheinen nach einer Viertelsekunde, auch wenn man mit Tab auf den Knopf kommt, und gehen mit Esc weg.
- **Rückfragen:** Stornieren, Löschen und Entfernen fragen in der Mitte des Fensters nach („Termin stornieren?“, „Nur diesen stornieren“ / „Diesen und alle folgenden“, mit Grund-Feld). Der Fokus steht bei zerstörenden Rückfragen auf „Abbrechen“, bei Rückfragen mit Feld (auch beim Storno) im Feld „Grund“, wo Enter nichts auslöst; Esc bricht ab. Wer eine Seite mit ungespeicherten Eingaben schließt (Kreuz, Esc, Strg+W), wird gefragt: „Änderungen verwerfen?“.
- **Reiter unten:** ← → wechseln den Reiter, Strg+W schließt die Seite, Strg+1 bis 9 wählen einen Reiter. Sind mehr als zwei Seiten offen, zeigen die Reiter nur ihr Symbol.
- **Menüs:** das Menü am Termin (Rechtsklick) hat keine Untermenüs mehr; „Kommt 15 Min. später“ und die übrigen Aktionen stehen untereinander, „Stornieren …“ zuletzt in Rot. Pfeile wählen, Enter löst aus, Esc schließt und gibt den Fokus zurück.
- **Kopfzeile:** wird sie zu schmal, wandern die Chips von rechts in ein Menü „⋯“, statt zu scrollen.
- **Meldungen:** Fehler und Warnungen einer Aktion („Nicht verschoben“, „Krankmeldung nicht gespeichert“, „Nicht alle Termine frei“) stehen als rote bzw. gelbe Leiste oben im Kalender und bleiben, bis man sie schließt; bei Fehlern, die ein zweiter Versuch beheben kann, mit „Erneut versuchen“. Der Toast unten bestätigt nur noch in einer Zeile bis 60 Zeichen ohne Punkt („Verschoben auf 10:15–11:05 (Anna, Raum Zeder)“).

**Gebaut auf `fluent2`** (Commits siehe PR-Protokoll).

### Schritt 21 (Blöcke E bis G): Schrift, Bewegung, Tastatur und KI-Kennzeichnung nach Fluent 2 (22.10.2026)

Werte und Verhalten stehen im Design-Brief: 3, 4.6, 5.21, 5.22, 5.28, 9 und 12; die Umstellung F22 bis F30 in 11.4. Auf dem Branch `fluent2`, nur Windows-App (Handbuch und Schriftgrößen im Web ziehen mit).

- **Schrift:** alle Texte stehen auf den Stufen des Briefs (12, 14, 16, 18, 22); Überschriften der Gruppen („Betrifft“, „Nächste Termine“, „Zahlungsarten“ …) stehen in Satzschreibweise statt in Versalien, ohne Kursiv.
- **Bewegung:** Knöpfe färben sich beim Darüberfahren in einer Zehntelsekunde um; die Meldung unten und die Seitenleisten gleiten sanfter ein und schneller aus. Ist in Windows „Animationen anzeigen“ aus, bewegt sich nichts mehr, auch nichts blendet mehr ein oder aus.
- **Tastatur und Screenreader:** **F6** (mit Umschalt rückwärts) springt zwischen Navigation, Kopfzeile, Kalender, Seitenleiste und den Reitern unten („KI und Hilfe“ ist im Kalender ein Reiter der Seitenleiste, Ä66). Tab folgt der Anzeige; im Terminfenster stehen die Reiter unten zuletzt. Jeder Knopf, jede Navigationszeile und jedes Feld hat einen Namen, den ein Screenreader vorliest (vorher hörte man Typnamen); Fragezeichen heißen „Hilfe zu Kontakt“ statt nur „Hilfe“. Tastenkürzel stehen im Hinweis des Knopfs und in den Menüs („Strg+N“); „Einchecken (E)“ und „Kassieren (K)“ nennen ihre Taste.
- **Kundendaten:** „Kundendaten ändern“ in der Kundenkarte setzt den Cursor gleich in „Vorname“.
- **Menü am Termin:** „Zeiten dieses Termins ändern …“ (nur dieser Termin, Zahlenfelder im Terminfenster) und „Behandlungszeiten bearbeiten …“ (die Behandlung in der Verwaltung, für alle Termine) heißen nicht mehr fast gleich.
- **KI:** an jeder Stelle, an der eine KI etwas ausgibt (Assistent, „Wunsch eintippen“, Posteingang, „Was tun?“), steht die Pille **KI**, darunter der Hinweis „KI-Vorschlag. Bitte prüfen, bevor Sie ihn übernehmen.“ (oder der Hinweis des Servers), „Warum dieser Vorschlag?“ zum Aufklappen und „Hilfreich“ / „Nicht hilfreich“ mit den Arten ungenau, unvollständig, unangemessen, schädlich, voreingenommen, Sonstiges. Der Antwortentwurf heißt „KI-Entwurf, noch nicht gesendet“ und wird nie von selbst gesendet. Geht die KI nicht, steht dabei ein Weg ohne KI („Ohne KI weiter: …“, mit Knopf zum Handbuch, zur Suche nach freier Zeit oder zum Entwurf). Die Rückmeldung bleibt auf dem PC und enthält keine Gästedaten.

**Gebaut auf `fluent2`** (Commits siehe PR-Protokoll).

### Schritt 22 (Runde 5, Block H): Hinweis, Reiter, Tagesüberblick, Kontextmenü und Tastenübersicht (Ä28, Ä29, Ä30, Ä32, Ä35, 22.10.2026)

Werte und Verhalten stehen im Design-Brief: 5.12 (Punkt 7), 5.21, 5.22 und 7; die Umstellung in 11.4 (Zeilen Ä28 bis Ä35). Nur Windows-App.

- **Hinweis am Streifen:** der Hinweis „Vorb. 15 Min.“ und alle anderen Hinweise im Kalender sind lesbar (heller Text auf dunkler Fläche, im dunklen Modus umgekehrt); vorher stand der Text in derselben Farbe wie die Fläche.
- **Reiter unten:** zeigen sie nur das Symbol (ab drei offenen Seiten), stehen bis zu **fünf** nebeneinander; erst die sechste Seite bringt „⋯ 1“ und die Pfeile.
- **Tagesüberblick:** das Fenster unter „24 Termine · 3 da · 18 offen“ ist breiter; unter „Als Nächstes“ stehen Gast und Behandlung · Person je in einer Zeile. Ein Klick (oder Pfeile und Enter) auf einen Eintrag markiert den Termin im Kalender, öffnet sein Fenster rechts und schließt den Überblick.
- **Menü am Termin:** Details öffnen, Einchecken, Kundenkarte, Folgetermin, Verschieben, Erledigt, Nicht erschienen und Stornieren tragen ein Symbol; „Kommt später“ gibt es nur noch mit 5, 10 und 15 Minuten (bei längerer Verspätung verschiebt man den Termin).
- **Tasten im Kalender (?):** Gruppentitel größer und fett, neue Gruppe „Seitenleiste und Reiter“ (Esc, Strg+Tab, Strg+W, Strg+1 … 9, F6), zwei gleich breite Spalten mit fester Tastenspalte, Beschreibungen in höchstens zwei Zeilen; unten der Link „Alle Tasten im Handbuch“.

### Schritt 23 (Runde 5, Block I): Notizen in den Meldungen, Notiz zum Termin und Kunde im Termin (Ä31, Ä33, Ä34, 22.10.2026)

Werte und Verhalten stehen im Design-Brief: 5.12 (Punkt 11), 5.29 und 7; die Umstellung in 11.4 (Zeilen Ä31, Ä33, Ä34). Nur Windows-App, dazu zwei schlanke Endpunkte.

- **Ä31, „Heute“-Band weg:** Frage des Auftraggebers „Für was haben wir das Heute-Band, brauchen wir das noch? Meldungen sind oben, man kann alle MA einblenden.“ (Antwort „ok“). Die Tagesnotizen ohne Uhrzeit stehen jetzt als Gruppe **Notizen heute** ganz oben in der Seitenleiste „Meldungen“ (lesen, abhaken, ändern, löschen mit Rückfrage, „+ Notiz“); ohne Notizen zeigt die Gruppe nur „+ Notiz“. Der Zähler am Chip „Meldungen“ zählt weiter nur Meldungen; gibt es Notizen, trägt der Chip einen kleinen Notiz-Punkt. Die Taste „.“ öffnet die Seitenleiste mit dem Notizfeld. Notizen mit Uhrzeit bleiben Zettel in der Spalte; wer fehlt, zeigen die Spalten und, mit offenen Terminen, die Meldung „Abwesenheit“. Die eigene Seite „Tagesnotiz“ (Reiter) entfällt. Ein Öffnen der Seitenleiste beim ersten Öffnen des Tages gibt es im Code nicht (der Tagesstart ist eine Meldung); das blieb, wie es war.
- **Ä33, Notiz zum Termin:** unter den Terminangaben der Link „Notiz hinzufügen“, mit Notiz die gelbe Hinweisfläche mit Stift; Feld mit Zähler (500 Zeichen), Strg+Enter speichert, Esc stellt zurück und bricht beim zweiten Mal ab. Neu: `PUT /api/appointments/{id}/note` (Recht buchen); die Notiz steht weiter in Hover-Karte und Panel und ist nicht die Kundennotiz.
- **Ä34, Kunde im Termin:** eingeklappter Abschnitt „Kunde“ unter den Terminangaben mit vier Kennzahlen im Kopf (Besuche gesamt, letzter Besuch, nicht erschienen, nächster Termin) und, mit dem Recht „Berichte mit Beträgen“, einer Zeile mit offenem Betrag und Paketen; aufgeklappt die Kundennotiz, der Stand der Anamnese, „Zuletzt“ (bis zu fünf Behandlungen, Klick öffnet den Termin) und klickbarer Kontakt (Anruf, WhatsApp, E-Mail, Kopieren mit Toast „Kopiert“), dazu „Kundenkarte öffnen“ (Strg+Enter). Neu: `GET /api/customers/{id}/summary` (alles in einem Aufruf).

### Schritt 24 (Runde 5, Block K2): Der Assistent beantwortet Betriebsfragen aus den Daten (Ä36, 22.10.2026)

Entscheidung und Aufbau: [ADR 0072](../entscheidungen/0072-assistent-beantwortet-betriebsfragen.md). Windows-App „KI und Hilfe“ und Web-Assistent; Werte der Kennzeichnung stehen im Design-Brief 5.28.

- **Fragen aus den Daten:** „Wie lange haben wir heute offen?“, „Wer arbeitet morgen?“, „Welcher Raum ist jetzt frei?“, „Was kostet Hot Stone?“, „Wann war Frau Müller zuletzt da?“, „Welche Aktionen laufen gerade?“, „Top-Behandlungen diesen Monat“ und die übrigen Fragen aus dem ADR beantwortet der Assistent mit einem Satz, einer Tabelle (höchstens zehn Zeilen) und der **Quelle** („Quelle: Öffnungszeiten des Standorts Wien Innere Stadt“). Sie sind regelbasiert; die KI erkennt nur Umschreibungen und glättet höchstens den Satz, ohne eine Zahl zu ändern.
- **Tabelle in der Leiste:** zwei Spalten stehen als Beschriftung links und Wert rechts, mehr Spalten als Liste (die erste Zelle fett, darunter „Spalte: Wert“; beginnt die Tabelle mit „Platz“, heißt die Zeile „1. Name“).
- **Verweise:** „Im Bericht öffnen“ unter Auswertungen öffnet die Verwaltung beim Management-Bericht mit Standort und Zeitraum der Antwort; „Öffnungszeiten eintragen“, „Anreise eintragen“, „Stornoregel eintragen“ und „Aktionen verwalten“ öffnen die Einstellung (nur mit dem Recht, sie zu ändern). **Kopieren** bei Kontaktdaten und Anreise legt den Text in die Zwischenablage („Kopiert.“).
- **Fehlt etwas:** „Dazu ist nichts hinterlegt: …“ mit dem Knopf zur Einstellung. **Keine Berechtigung:** „Dafür fehlt die Berechtigung. …“ mit Quelle „Berechtigungen dieses Zugangs“; wo es die Auswertung ohne Beträge gibt, steht sie als Frage darunter („Behandlungen nach Anzahl zeigen“). **Keine Absicht:** „Dazu habe ich nichts gefunden. Meinen Sie vielleicht eine dieser Fragen?“ mit zwei Fragen als Knöpfe.
- **Kennzeichnung:** Pille „KI“ an der Antwort, Hinweiszeile vom Server („Die Angaben stammen aus den Daten des Spas (Quelle unten), nicht von der KI …“), „Warum diese Antwort?“ mit der Quelle, Rückmeldung. Im Web dieselben Teile; die Rückmeldung bleibt in diesem Browser.
- **Platzhalter und Beispiele:** das leere Fragefeld nannte „Wie lange haben wir heute offen? · Wer arbeitet morgen? · Top-Behandlungen diesen Monat“; seit 23.10.2026 steht dort nur „Frage eingeben“ (Ä69, die Beispiele stehen schon als Knöpfe darunter). Die Beispiel-Knöpfe nennen je Rolle Fragen zum Betrieb (das Backoffice nur Auswertungen).
- **Backoffice:** die Leiste „KI und Hilfe“ gibt es auch ohne Kalender; der Assistent beantwortet dort nur Auswertungen und Fragen zur Bedienung.
- **Neue Felder:** Anreise und Parkplätze (Freitext am Standort, Stammdaten › Standorte & Räume › Anschrift und Kontakt) und Stornoregel (Freitext, Online-Buchung), je mit Fragezeichen zur Hilfe.

### Schritt 25 (Runde 5b, Block L): Schließzeiten und Betriebsausfall mit Terminabsage (Ä38, 22.10.2026)

Entscheidung und Aufbau: [ADR 0073](../entscheidungen/0073-schliesszeiten-und-terminabsage.md). Werte stehen im Design-Brief 5.8 (Chip), 5.27 (Rückfrage) und 7 (Schließband). Windows-App, Verwaltung im Web und Kundenbereich.

- **Schließband:** an einem Tag mit Schließzeit liegt über allen Spalten eine Fläche in der Farbe der Art (Betriebsurlaub akzent, Sonderschließung Warnung, Ausfall Fehler; 55 % Deckkraft, 1 px Rand oben und unten). Oben links steht der Text in einer Kapsel („Betriebsurlaub · Inventur“, ohne internen Grund der Text für Gäste). Das Band fängt keinen Klick ab, die Termine darunter bleiben bedienbar; ein halber Tag beginnt zur Uhrzeit. Der Hinweis nennt Zeitraum, Grund, Text für Gäste und die Zahl der offenen Termine.
- **Chip in der Kopfzeile:** „Geschlossen · Betriebsurlaub“ (kompakt „Geschlossen“) als Kopfzeilen-Chip in Zeile 2, Betriebsurlaub im Zustand `on`, Sonderschließung `warn`, Ausfall `crit`; ein Klick öffnet die Schließzeiten in der Verwaltung (mit dem Recht, Standorte zu verwalten).
- **Meldung „n Termine in der Schließzeit“:** mit dem Knopf **Termine absagen …** (nur mit den Rechten Standorte verwalten und Buchen), für jede Schließzeit mit offenen Terminen **oder Einträgen der Warteliste**, gleich wie fern sie beginnt (Block L2, Ä49; vorher nur 60 Tage voraus). Eine **Warnung**, solange die Schließzeit später als in 14 Tagen beginnt, ein **Fehler**, wenn sie läuft oder in 14 Tagen beginnt („2 Termine und 1 Eintrag der Warteliste in der Schließzeit (Betriebsurlaub, …)“); sie steht dann in der Gruppe „Fehler“. Sie bleibt, bis die Termine abgesagt und die Einträge benachrichtigt sind; **nichts wird von selbst abgesagt**.
- **Rückfrage „Termine absagen?“:** derselbe Dialog wie jede Rückfrage (Overlay, Fokus auf **Abbrechen**, Esc, Tab im Kreis), nur **600 breit** und mit Inhalt: die offenen Termine mit Häkchen (alle gewählt, **Alle auswählen**), je Zeile Gast, Zeit, Behandlung, Weg der Nachricht (Mail, SMS, kein Kontakt) und Kürzel der **Sprache** der Nachricht in einer Pille (DE, EN, FR, IT; Block L2, Ä46) und Hinweis auf Geld („Anzahlung 30,00 € wird retourniert“, Ä47); darunter als eigener Abschnitt die **Warteliste (n)** mit Auswahl, Wunschzeitraum und Sprache (Ä48; es zählen nur Wünsche, deren Tageszeit in die Schließzeit fällt, Block L3, Ä54); **Betreff** und **Nachricht** (deutsche Vorlage je Art, `{Anrede}`, `{Behandlung}` und `{Zeit}` setzt der Server je Gast ein; Gäste mit einer anderen Sprache bekommen die Standardvorlage ihrer Sprache, das sagt der Hinweis am Feld); **Freundlicher formulieren (KI)**, **Mit Entschuldigung (KI)**, **Förmlicher formulieren (KI)** nur auf Knopfdruck, danach die Pille „KI“ am Feld und der Hinweis, dass die Nachricht den Hinweis auf die KI trägt; **Umbuchung anbieten** (Link in den Kundenbereich); darunter „2 Nachrichten werden gesendet (1 Mail, 1 SMS). 1 Gast hat keinen Kontakt …“, „Nachrichten nach Sprache: 2 Deutsch, 1 Englisch.“ und „2 Anzahlungen (50,00 €) gehen über die Kassa zurück (je ein Storno-Beleg, auf dem Zahlungsweg der Einzahlung).“ Der rote Knopf nennt die Zahl („3 Termine absagen“, mit Warteliste „3 Termine absagen, 2 benachrichtigen“, nur mit Einträgen „2 Einträge benachrichtigen“, dann ohne Textfeld); die Knopfleiste steht fest unten, der Inhalt scrollt in sich. Danach „3 Termine abgesagt, 2 Nachrichten, 1 ohne Kontakt“, bei Warteliste und Rückzahlung „… 2 Wartelisten-Einträge benachrichtigt, 1 Anzahlung retourniert“; ging ein Storno nicht, steht „Anzahlung nicht retourniert“ als Meldung mit dem Weg („Bitte an der Kassa erstatten“).
- **Anzahlung im Termin (Block L2, Ä47):** im Seitenpanel eines Termins mit offener Anzahlung steht für **Inhaber und Manager** der Abschnitt **Anzahlung** („30,00 € bezahlt (Karte), Beleg K1 Nr. 12, 19.10.2026 …“) mit dem Knopf **Anzahlung retournieren …**. Er fragt nach (Rückfragedialog, „Retournieren“, Feld „Grund (Pflicht)“, ohne Grund fragt er noch einmal) und stellt erst dann den Storno-Beleg über die Kassa aus; Standortleitung und Rezeption sehen den Abschnitt nicht. Bei einer Absage wegen einer Schließzeit geht die Anzahlung von selbst zurück (bar bezahlt: „Sie erhalten den Betrag von 30,00 € bar an der Rezeption zurück“, Block L3, Ä53). Dieselbe Rückfrage gibt es in der Verwaltung (Termin und Beleg). Den **Anzahlungsbeleg** selbst storniert an der Kassa nur die Leitung: bei Standortleitung und Rezeption ist „Stornieren“ gesperrt, der Tooltip sagt „Anzahlungsbelege storniert nur die Leitung.“ (Block L3, Ä51).
- **Verwaltung (Web):** Stammdaten › **Schließzeiten** mit Reitern Aktuell und Beendet, Standortwahl, Maske in den Gruppen Zeitraum, Art und Texte, derselben Rückfrage als `<dialog>` und dem Protokoll der Absagen; Hilfe zu jeder Gruppe.
- **Kundenbereich:** bei der Zeitwahl „Vom Montag, 22. März bis Freitag, 26. März geschlossen.“ mit dem Text des Spas oder dem Standardsatz („Betriebsurlaub.“), auf Deutsch, Englisch, Französisch und Italienisch.
- **Assistent:** „Ist am Montag offen?“ („Nein, am Montag, 08.03.2027 ist geschlossen: Betriebsurlaub bis 10.3.“), „Sind wir im Jänner geschlossen?“, „Wann ist der nächste Feiertag?“ (nennt eine Schließzeit an dem Tag).

### Schritt 26 (Runde 5b, Block K4): Kalender-Abos für Feiertage, Schulferien und Hinweise (Ä43, 22.10.2026)

Entscheidung und Aufbau: [ADR 0075](../entscheidungen/0075-kalender-abos-fuer-feiertage-und-ferien.md). Werte stehen im Design-Brief: 5.8 (Chip), 5.23 (Schalter), 7 (Datumstitel, Tagesüberblick, Chip „Kalender“); die Umstellung in 11.4 (Zeile Ä43). Windows-App und Verwaltung.

- **Feiertag im Datumstitel:** ein gesetzlicher Feiertag steht hinter dem Datum in der Kopfzeile, in Team- und Raumansicht gleich („Montag, 19. Oktober 2026 · Nationalfeiertag“, mehrere mit „ · “ getrennt, bei Platzmangel gekürzt); eine Schließzeit des Standorts (Schritt 25) kommt als „· Geschlossen: Betriebsurlaub“ dazu, Band und Chip von Block L bleiben. Die Kopfzeile bleibt bei zwei Zeilen; die Spalten und die Köpfe der Räume ändern sich nicht. Ein Hinweis am Datum nennt jeden Eintrag mit **Quelle und Stand des Abrufs** („Feiertagsregeln Österreich, Wien“, „Abo ‚Schulferien Wien‘, Stand gestern“) und zuletzt „Datum wählen (G)“.
- **Schulferien, Hinweise, Sonstiges:** nicht im Titel, sondern im Hinweis am Datum („Herbstferien bis 2.11.“) und in der ersten Zeile des Tagesüberblicks („Heute: Nationalfeiertag · Herbstferien bis 2.11.“; an anderen Tagen mit dem Datum statt „Heute“).
- **Chip „Kalender“** in Zeile 2 neben Team und Räumen (Symbol Kalender mit kreisenden Pfeilen; nur da, wenn der Standort Abos hat). Ein Klick öffnet die Liste der Abos des Standorts (420 breit): je Abo Name, Art, Stand des Abrufs und Anzahl der Tage, bei einem Fehler der Grund, der Schalter **Zeigen** (wirkt sofort, nur die Anzeige dieses Zugangs), bei Feiertags-Abos der Hinweis „Nur zur Information, ändert keine Buchung; aktiv/inaktiv nur Leitung“, für die Leitung der Schalter **Aktiv (für alle)** und für jede Person **Falsch melden**. Ein Punkt (7 px, accent) am Chip zeigt ausgeblendete Abos, der Name des Chips sagt es auch.
- **Falsch melden:** Rückfrage „Falsch melden?“ mit dem Feld **Grund** (3 bis 300 Zeichen) für den angezeigten Tag; die Leitung bekommt eine Meldung mit Tag und Grund, das Abo ändert sich nicht. Ohne Grund bleibt ein Hinweis mit „Grund eintragen“.
- **Meldungen** (Seitenleiste, Art „Kalender-Abo“): Änderung nach einem Abruf („Abo ‚Feiertage Salzburg‘: 2 neue Tage, 1 Tag entfernt“, **Erledigt**), Fehler beim Abruf (Warnung, **Erneut versuchen** nur für die Leitung), Erinnerung im November („Feiertage für 2028 prüfen“, Leitung) und „Falsch gemeldet“ (Leitung).
- **Verwaltung:** Reiter **Kalender-Abos** unter „Stammdaten“ neben „Standorte & Räume“: Liste mit Stand, Maske in vier Gruppen (Abo, Gilt für, Zustand, Widersprüche) mit Quellenvorschlägen je Land und Region, **Jetzt abrufen**, **Datei importieren** und der Liste der Widersprüche zu den Feiertagsregeln (Übernehmen, Ignorieren). Hilfe: [Kalender-Abos](hilfe:kalender-abos) und [Kalender-Abos im Kalender ansehen](hilfe:kalender-abos-anzeigen).
- **Assistent:** „Wann sind Ferien?“, „Wann sind die Herbstferien?“, „Sind am 28. Oktober Ferien?“ aus den aktiven Schulferien-Abos des Standorts; die Feiertagsantworten nennen das Abo und den Stand in der Quelle.

### Schritt 27 (Runde 5b, Block K3): Weg zum Spa, Auswertungen für die Rezeption, fehlende Angaben und Geburtstage (Ä41, Ä42, Ä44, Ä45, 22.10.2026)

Entscheidung und Aufbau: Nachtrag zu [ADR 0072](../entscheidungen/0072-assistent-beantwortet-betriebsfragen.md). Ä40 (Feiertage aller Schweizer Kantone in `PublicHolidays`) entfällt; sie kommen über die Kalender-Abos (Block K4).

- **Ä41, Weg zum Spa:** im Kundenbereich steht bei einem gebuchten Termin unter dem Standort die Anschrift, der Knopf **Route planen** und, wenn eingetragen, der Text „Anreise und Parkplätze“. Der Knopf ist ein Link in die Karten-App des Geräts mit der Anschrift als Ziel (Apple Karten auf iPhone, iPad und Mac, sonst Google Maps) ohne Startpunkt: die Karten-App nimmt den aktuellen Standort des Geräts und fragt selbst um Erlaubnis; der Kundenbereich fragt keinen Standort ab. Die Bestätigungsmail der Online-Buchung hat dazu die Zeile „Anreise“ mit Text und Link (deutsch, englisch, französisch, italienisch), wenn ein Text eingetragen ist. Kein neues Paket, Kundenbereich bleibt unter 100 kB gzip.
- **Ä42, Auswertungen ohne Beträge für die Rezeption:** „Top-Behandlungen diesen Monat“, „Auslastung letzte Woche“, „Welche Therapeutin hat die meisten Termine?“, „Wie viele Nicht-erschienen hatten wir?“ und „Top-Kunden“ (nach Besuchen) beantwortet der Assistent jeder Person mit dem Kalenderrecht; Umsatz und Top-Kunden nach Ausgaben bleiben beim Recht „Berichte mit Beträgen“ (Ablehnung mit dem Angebot der Auswertung ohne Beträge).
- **Ä44, Unvollständig:** Konnte der Assistent antworten, fehlen aber Angaben zum Thema, steht unter der Quelle eine Hinweisfläche „Unvollständig: Anreise fehlt beim Standort Wien Innere Stadt. Zu ergänzen unter Stammdaten › Standorte › Anreise und Parkplätze.“ mit dem Knopf **Jetzt ergänzen** (nur mit dem Recht, sonst „Nur Leitung“ im Text). Der Knopf öffnet die Maske am Feld: die Verwaltung scrollt dorthin und setzt den Fokus. Nach dem Speichern meldet die Leiste „Daten gespeichert. Frage wiederholen?“ mit dem Knopf **Frage wiederholen**. Alle Verweise kommen aus einer Tabelle Feldkennung → Maske. Bild: `ki-betrieb-unvollstaendig`.
- **Ä45, Geburtstag:** am Eintrag nur am Tag selbst ein kleines **Geschenk-Symbol** (16 px, Akzentfarbe, ohne Text, Hinweis und Bildschirmleser „Geburtstag heute“) rechts im Kopf neben Stern und Hinweis; Hover-Karte und Termin-Panel „Geburtstag heute · 42“ (Alter nur dort und nur bei bekanntem Geburtsjahr), drei Tage davor und danach „Geburtstag am 25.10.“; der Tagesüberblick „Geburtstag heute: Brigitte Müller (10:30)“ über „Als Nächstes“; die Kundenkarte nennt Alter und „Nächster Geburtstag in 12 Tagen“; der Assistent beantwortet „Wer hat heute Geburtstag?“ und „… diese Woche …?“ nur für Gäste mit Termin im Zeitraum. Der Server rechnet (`GuestBirthday`, 29. Februar in Jahren ohne Schalttag am 28.), nicht die Oberfläche. Bilder: `kalender-geburtstag`, `termin-geburtstag`.
- **Ä50, Feiertage sind reine Information:** ein Feiertag (Regeln oder Abo) ändert weder Öffnungszeiten noch freie Zeiten, Buchung, Serienvorschau oder Online-Buchung, weder als Sperre noch als Warnung (Auftraggeber: „Feiertage sind für Spa-Betriebe die umsatzstärksten Tage.“). Er steht im Datumstitel, im Tooltip, im Tagesüberblick, in der Feiertagsliste und in den Antworten des Assistenten („… geöffnet wie an einem Montag, sofern keine Schließzeit eingetragen ist“); wer zu hat, trägt eine Schließzeit ein. Die Serienvorschau markiert einen Termin am Feiertag nicht mehr. Nachtrag in ADR 0075.


### Schritt 28 (Runde 6, Block N1): Verspätung ohne Aufblitzen, volle 7-Tage-Ansicht, Vorschau beim Planen (Ä63, Ä64, Ä67, 23.10.2026)

Drei Befunde des Auftraggebers vom lokalen Test. Umsetzung und Prüfung im [PR-Protokoll](../umsetzung/pr-protokoll.md) („Runde 6 Teil 2, Block N1“).

- **Ä63, „Kommt später“:** Nach „Kommt 15 Min. später“ geht das Flyout „Termin anpassen“ (Kürzen, Nach hinten schieben) am Termin auf. Der Server meldet die eigene Änderung auch an diesen Arbeitsplatz zurück (Live, `AppointmentChanged`); der zweite, gleiche Stand baute den Tag neu auf, die Karte des Termins entstand neu und das Flyout an der alten Karte ging nach einem Augenblick wieder zu. `Apply` überspringt jetzt einen Stand, der dem gezeigten gleicht (`IsSameState`).
- **Ä64, 7-Tage-Ansicht einer Person oder eines Raums:** Die Tagesspalten sind volle Spalten: Klick auf freie Zeit, Rechtsklick, Pause, Sperrzeit, Notiz, Paar/Spa, Hover-Vorschau, Ziehen auch auf einen anderen Tag, Alt+Pfeile (Alt+← → ein Tag, gleiche Uhrzeit) und Zettel bearbeiten. Jede Spalte kennt ihren Tag (`DayStartOf`, `DateOf`); Einträge nehmen die Person bzw. den Raum aus dem Filter (`ResourceColumn`). Nur die Ansicht eines Gastes hat keine Spalte je Person und bleibt ohne Buchen und Ziehen.
- **Ä67, Vorschau:** Solange „Neuer Termin“ offen ist, zeigt die vorhandene Ghost-Karte den geplanten Eintrag (Beginn, Dauer samt Vor- und Nachbereitung, Behandlung bzw. Pause, Sperrzeitgrund oder Notiztext), live und in Warnfarbe bei belegter Zeit. Bilder: `termin-vorschau`, `termin-vorschau-belegt`.

### Schritt 29 (Runde 6, Block N2): Raumsperren und die Raumansicht wie die Teamansicht (Ä65, 23.10.2026)

Entscheidung und Aufbau: [ADR 0076](../entscheidungen/0076-raumsperren.md). Werte stehen im Design-Brief 7 (Raumsperre, Spaltenbreite). Auftraggeber: „Räume sollen wie Urlaub auch gesperrt werden können und sollen das gleiche Layout haben, Spaltenbreite usw.“

- **Raum sperren:** ein Raum ist für Tage oder einen Zeitraum gesperrt (auch halbe Tage), mit Art (Wartung, Schaden, Umbau, Sonstiges) und Grund für das Team. Pflege in der Verwaltung unter **Stammdaten › Raumsperren** oder per Rechtsklick auf den Kopf des Raums, **Raum sperren …**. In der Zeit wählt der Planer den Raum nie: keine freien Zeiten und keine Buchung dort (Online, Freie Zeit suchen, Warteliste, Buchen, Verschieben, Serien, Paar und Spa-Tag, KI-Terminannahme, Eintritt in Bereiche); es gibt die Zeiten in anderen Räumen. Wer den gesperrten Raum ausdrücklich wählt, bekommt „ist gesperrt (Wartung: Grund)“.
- **Raumansicht:** die gesperrte Zeit ist wie bei einer Person ohne Dienst schraffiert, mit einer Kapsel für Art und Grund. Im Kopf steht „gesperrt: Wartung bis 3.12.“, mit offenen Terminen der Link **n Termine umbuchen**; der Hinweis am Kopf nennt den Grund. Die gesperrte Zeit gilt als belegt: beim Zeigen erscheint dort keine freie Zeit, und ein Termin, den man dort trotzdem eingibt, lehnt der Server ab.
- **Teamansicht:** Termine im gesperrten Raum tragen den roten Rand „umbuchen“, die Hover-Karte sagt „Raum Lotus ist gesperrt (…): bitte umbuchen“.
- **Meldung und Umbuchungshilfe:** „n Termine im gesperrten Raum“ mit **Umbuchen …** (Warnung, ab 14 Tage vor Beginn Fehler). Die Hilfe ist die Mechanik des Ausfall-Assistenten: für jeden offenen Termin ein anderer Raum zur selben Zeit bei derselben Person, sonst die nächste freie Zeit am selben Tag, sonst „kein anderer Raum an diesem Tag“; erst **Gewählte übernehmen** bucht um. Eine Mail an den Gast geht nur, wenn sich Zeit oder Person ändern, nicht beim reinen Raumwechsel.
- **Raumansicht wie die Teamansicht:** gleiche Spaltenbreite in Pixeln, gleicher Kopf, gleiche Karten. Die Breite richtet sich nach den Personenspalten des Tages, nicht nach der Zahl der Räume; bei vielen Räumen scrollt der Kalender waagrecht wie bei vielen Personen. Vorher teilten sich mehr Räume als Personen dieselbe Fensterbreite und waren schmaler (Namen wie „Hand- und Fußp…“ wurden abgeschnitten).
- **Nicht gebaut:** Antworten des Assistenten zu Raumsperren, Darstellung in der 7-Tage-Ansicht eines Raums und im Bereichsplan (Sauna), Nachricht an Gäste (ein Raumwechsel betrifft sie meist nicht).

### Schritt 30 (Runde 6 Teil 3, Block P1): Kürzen, Popup „Termin anpassen“, Nach hinten schieben und Alle folgenden auch entfernt (Ä71, 23.10.2026)

Auftraggeber: Ein Termin bleibt stehen, auch wenn sein Beginn in der Vergangenheit liegt. Umsetzung und Prüfung im [PR-Protokoll](../umsetzung/pr-protokoll.md) („Runde 6 Teil 3, Block P1“).

- **Regel:** Ein Termin wird nie von selbst verkürzt oder verschoben. „Kommt später“ setzt nur die Verspätungsmarkierung (Karte „kommt 10:15“, überfällig erst ab dieser Zeit) und zeigt den Toast „… kommt 15 Min. später (10:15)“; **Doch pünktlich** nimmt sie zurück.
- **Entfernt:** das Popup „Termin anpassen“ am Termin, „Auf n Min. kürzen“ und „Auf n Min. verkürzen“ (bei Verspätung und beim abgelehnten Verschieben), „Nach hinten schieben“, „Alle folgenden auch“ (Serienfolge, Endpunkt `series/follow`), die Hover-Zeile „Verkürzt: 35 statt 50 Min.“ und die Meldung „Termin passt nicht mehr“. Der Server schlägt beim Ablehnen keine kürzere Dauer mehr vor (`BookingRejected.ShortenToMinutes`), und eine Verschiebung nimmt keine neue Dauer mehr an (`MoveAppointmentRequest.DurationMinutes`).
- **Überholt** sind dadurch die Beschreibungen des Kürzens, Nach-hinten-Schiebens und der Serienfolge in den Ergänzungen vor Schritt 10 (Verkürzen beim Verschieben, Verspätung), in Schritt 12 (Rechnen mit den Minuten beim Verkürzen) und in Schritt 28 (Ä63, Flyout „Termin anpassen“); sie bleiben als Verlauf stehen.
- **Bleibt:** Serien anlegen und ab hier stornieren, Verschieben per Ziehen, Tastatur und Terminfenster mit Rückgängig, Zeiten je Termin.
