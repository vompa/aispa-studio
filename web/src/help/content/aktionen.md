---
art: einstellung
bereich: Aktionen
oberflaeche: verwaltung
rollen: leitung, inhaber
seite: verwaltung
stichwoerter: Aktion, Aktionen, Rabattaktion, Angebot, Sonderangebot, Rabatt, Prozent, Betrag, Herbstaktion, Zeitraum, pausieren, archivieren, laufende Aktionen, kurzfristig, Last Minute, schwache Zeiten, ruhige Zeiten, Wochenraster, Vorlauf, Gästepreis, promotion, offer, discount, campaign, sale, last minute, off-peak
---
# Aktionen

Eine Aktion ist ein Rabatt auf Behandlungen oder Pakete, an allen oder an ausgewählten Standorten. Es gibt drei Arten: **Befristet**, **Kurzfristig frei** und **Schwache Zeiten**; was sie bedeuten, steht unter [Art](#art). Die Kassa schlägt eine laufende Aktion vor, wenn eine passende Behandlung auf der Rechnung steht, und setzt den Rabatt erst, wenn man auf **Anwenden** klickt; der Beleg nennt dann den Namen der Aktion. Die Aktion legt nur den Rabatt fest. Gebucht, versteuert und signiert wird wie bei jedem Verkauf über den Beleg: [Aktion an der Kassa anwenden](hilfe:kassa-aktion-anwenden).

Aktionen verwalten dürfen Inhaber und Standortleitung (Recht „Katalog pflegen“). Die Standortleitung sieht und ändert nur Aktionen ihrer eigenen Standorte und legt keine für alle Standorte an. Die laufenden Aktionen eines Standorts kennen alle Zugänge, weil Kassa und Assistent sie brauchen: Der Assistent beantwortet damit „Welche Rabatte oder Aktionen laufen gerade?“.

## Aktionen verwalten {#aktionen}

1. In der Windows-App unter „Verwaltung“ **Aktionen** öffnen.
2. In der Liste steht bei jeder Aktion ihre Art und ihr Zustand: Läuft, Geplant, Pausiert oder Abgelaufen. Archivierte erscheinen erst, wenn **Archivierte anzeigen** unter der Liste eingeschaltet ist.
3. Links eine Aktion wählen oder oben auf **Neue Aktion** klicken, rechts die Maske ausfüllen und auf **Speichern** (Strg+S) klicken. Unten erscheint kurz „Gespeichert“.

![Aktionen in der Windows-App: Liste mit Art und Zustand, rechts eine befristete Aktion](images/app/verwaltung-aktionen.png)

Ob eine Aktion läuft, richtet sich nach dem heutigen Tag am Standort. **Pausiert** ist eine Aktion, die man angehalten hat und die noch nicht abgelaufen ist; **Abgelaufen** ist sie am Tag nach ihrem Ende. Im Menü **⋯** im Kopf der Aktion stehen **Pausieren** bzw. **Fortsetzen** und **Archivieren**; bei einer archivierten Aktion holt der Knopf **Zurückholen** sie zurück. Gelöscht wird nie: abgelaufene und archivierte Aktionen bleiben als Verlauf erhalten, sie laufen nur nicht mehr und stehen nicht mehr an der Kassa. Bereits gegebene Rabatte auf Belegen ändern sich dadurch nicht.

## Aktion {#aktion}

- **Name**: kurz, höchstens 40 Zeichen, steht auf dem Beleg („Aktion Herbst 10 %“). Pflicht; unter den nicht archivierten Aktionen gibt es jeden Namen nur einmal.
- **Gästetext**: ein kurzer Satz für Gäste und den Assistenten, höchstens 200 Zeichen, zum Beispiel „10 % auf alle Massagen im Oktober“. Pflicht.

## Art {#art}

- **Befristet**: Der Rabatt gilt an jedem Tag des Zeitraums, zum Beispiel „Herbst: 10 % auf alle Massagen bis Ende Oktober“.
- **Kurzfristig frei**: Der Rabatt gilt für Termine, die höchstens so viele Stunden vor Beginn gebucht werden, wie unter **Vorlauf (Stunden)** steht (1 bis 168, also höchstens eine Woche). Bei 24 bekommt ein Termin um 10 Uhr den Rabatt, wenn er am Vortag um 10 Uhr oder später gebucht wurde.
- **Schwache Zeiten**: Der Rabatt gilt für Termine, die in einem abgehakten Feld des Wochenrasters beginnen. Das Raster hat für jeden Wochentag drei Felder: Vormittag (bis 12 Uhr), Nachmittag (12 bis 17 Uhr) und Abend (ab 17 Uhr), jeweils in der Zeit des Standorts. Mindestens ein Feld abhaken. Alle Felder einer Aktion haben denselben Rabatt; für einen anderen Satz legt man eine zweite Aktion an.

![Schwache Zeiten mit Wochenraster und Vorschau des Gästepreises](images/app/verwaltung-aktionen-schwache-zeiten.png)

Rechts neben der Maske steht **So sieht es der Gast**: ein Beispieltermin, an dem die Aktion gilt, und für die ersten Behandlungen der Preis und der Preis mit Rabatt. Die Vorschau rechnet wie die Kassa und ändert sich bei jeder Eingabe, auch vor dem Speichern.

In der Online-Buchung sehen Gäste weiter den vollen Preis; den Rabatt gibt es an der Kassa.

## Rabatt {#rabatt}

Entweder **Prozent** oder **Betrag**, nie beides.

- **Prozent**: Prozent vom Preis der Position, zwischen 0,01 und 99,99. Die Kassa rundet auf Cent.
- **Betrag**: ein fester Betrag je Position (je Behandlung bzw. je Paket), nicht je Stück. Ist der Betrag nicht kleiner als der Preis der Position, schlägt die Kassa diese Position nicht vor.

Der Beleg nennt immer den Prozentsatz, auch bei einem Betrag (zum Beispiel „Aktion Fünfer 6,3 %“, wenn 5 € von 79 € abgehen).

## Zeitraum {#zeitraum}

**Von** und **Bis** sind Kalendertage am Standort, beide Tage zählen mit. Das Ende darf nicht vor dem Beginn liegen. Bei **Kurzfristig frei** und **Schwache Zeiten** ist es der Zeitraum, in dem die Regel gilt; eine neue Aktion dieser Arten läuft zunächst ein Jahr. **Aktiv** ausgeschaltet pausiert die Aktion: sie wird an der Kassa nicht vorgeschlagen, bis man sie wieder einschaltet.

## Gilt für {#gilt-fuer}

- **Alle Behandlungen**: jede Behandlung, die als Termin auf der Rechnung steht.
- **Ausgewählte Behandlungen**: nur die Behandlungen, die man unter **Behandlung hinzufügen** wählt (Namen eintippen, aus der Liste wählen; das Kreuz entfernt eine Behandlung wieder).
- **Pakete ausgewählter Behandlungen**: der Verkauf eines Pakets (Mehrfachkarte, z. B. 10er-Block) dieser Behandlungen. Der Rabatt senkt den Paketpreis; der Gutschein trägt den bezahlten Wert. Nur bei befristeten Aktionen, weil die beiden anderen Arten am Termin hängen.

Auf Gutscheine mit einem Wert, auf Anzahlungen, Artikel und Eintritte wirkt keine Aktion. Positionen, die schon einen Rabatt haben (Kundenrabatt, von Hand gegeben oder eine andere Aktion), bekommen keine Aktion dazu: Rabatte werden nicht gestapelt. Passen mehrere Aktionen, schlägt die Kassa die mit dem höheren Rabatt zuerst vor.

## Standorte {#standorte}

**Alle Standorte** oder **Ausgewählte Standorte** (Häkchen je Standort). Aktionen für alle Standorte legt nur der Inhaber an.
