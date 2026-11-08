---
art: einstellung
bereich: Aktionen
oberflaeche: verwaltung
rollen: leitung, inhaber
seite: verwaltung
stichwoerter: Aktion, Aktionen, Rabattaktion, Angebot, Sonderangebot, Rabatt, Prozent, Betrag, Herbstaktion, Zeitraum, pausieren, archivieren, laufende Aktionen, promotion, offer, discount, campaign, sale
---
# Aktionen

Eine Aktion ist ein zeitlich begrenzter Rabatt auf Behandlungen oder Pakete, an allen oder an ausgewählten Standorten, zum Beispiel „Herbst: 10 % auf alle Massagen bis Ende Oktober“. Die Kassa schlägt die laufende Aktion vor, wenn eine passende Behandlung auf der Rechnung steht, und setzt den Rabatt erst, wenn man auf **Anwenden** klickt; der Beleg nennt dann den Namen der Aktion. Die Aktion legt nur den Rabatt fest. Gebucht, versteuert und signiert wird wie bei jedem Verkauf über den Beleg: [Aktion an der Kassa anwenden](hilfe:kassa-aktion-anwenden).

Aktionen verwalten dürfen Inhaber und Standortleitung (Recht „Katalog pflegen“). Die Standortleitung sieht und ändert nur Aktionen ihrer eigenen Standorte und legt keine für alle Standorte an. Die laufenden Aktionen eines Standorts kennen alle Zugänge, weil Kassa und Assistent sie brauchen: Der Assistent beantwortet damit „Welche Rabatte oder Aktionen laufen gerade?“.

## Aktionen verwalten {#aktionen}

1. In der Windows-App unter „Verwaltung“ **Aktionen** öffnen.
2. In der Liste steht bei jeder Aktion ihr Zustand: Läuft, Geplant, Pausiert oder Abgelaufen. Archivierte erscheinen erst, wenn **Archivierte anzeigen** eingeschaltet ist.
3. Links eine Aktion wählen oder auf **Neue Aktion** klicken, rechts die Maske ausfüllen und auf **Speichern** (Strg+S) klicken. Es erscheint „Gespeichert“.

Ob eine Aktion läuft, richtet sich nach dem heutigen Tag am Standort. **Pausiert** ist eine Aktion, die man angehalten hat und die noch nicht abgelaufen ist; **Abgelaufen** ist sie am Tag nach ihrem Ende. Über die Knöpfe **Pausieren** bzw. **Fortsetzen**, **Archivieren** und im Archiv **Zurückholen** lässt sich der Zustand ändern. Gelöscht wird nie: abgelaufene und archivierte Aktionen bleiben als Verlauf erhalten, sie laufen nur nicht mehr und stehen nicht mehr an der Kassa. Bereits gegebene Rabatte auf Belegen ändern sich dadurch nicht.

## Aktion {#aktion}

- **Name**: kurz, höchstens 40 Zeichen, steht auf dem Beleg („Aktion Herbst 10 %“). Pflicht; unter den nicht archivierten Aktionen gibt es jeden Namen nur einmal.
- **Gästetext**: ein kurzer Satz für Gäste und den Assistenten, höchstens 200 Zeichen, zum Beispiel „10 % auf alle Massagen im Oktober“. Pflicht.

## Rabatt {#rabatt}

Entweder **Prozent** oder **Betrag**, nie beides.

- **Prozent**: Prozent vom Preis der Position, zwischen 0,01 und 99,99. Die Kassa rundet auf Cent.
- **Betrag**: ein fester Betrag je Position (je Behandlung bzw. je Paket), nicht je Stück. Ist der Betrag nicht kleiner als der Preis der Position, schlägt die Kassa diese Position nicht vor.

Der Beleg nennt immer den Prozentsatz, auch bei einem Betrag (zum Beispiel „Aktion Fünfer 6,3 %“, wenn 5 € von 79 € abgehen).

## Zeitraum {#zeitraum}

**Von** und **Bis** sind Kalendertage am Standort, beide Tage zählen mit. Das Ende darf nicht vor dem Beginn liegen. **Aktiv** ausgeschaltet pausiert die Aktion: sie wird an der Kassa nicht vorgeschlagen, bis man sie wieder einschaltet.

## Gilt für {#gilt-fuer}

- **Alle Behandlungen**: jede Behandlung, die als Termin auf der Rechnung steht.
- **Ausgewählte Behandlungen**: nur die Behandlungen, die man unter **Behandlung hinzufügen** wählt (Namen eintippen, aus der Liste wählen; das Kreuz entfernt eine Behandlung wieder).
- **Pakete ausgewählter Behandlungen**: der Verkauf eines Pakets (Mehrfachkarte, z. B. 10er-Block) dieser Behandlungen. Der Rabatt senkt den Paketpreis; der Gutschein trägt den bezahlten Wert.

Auf Gutscheine mit einem Wert, auf Anzahlungen, Artikel und Eintritte wirkt keine Aktion. Positionen, die schon einen Rabatt haben (Kundenrabatt oder von Hand gegeben), bekommen keine Aktion dazu: Rabatte werden nicht gestapelt.

## Standorte {#standorte}

**Alle Standorte** oder **Ausgewählte Standorte** (Häkchen je Standort). Aktionen für alle Standorte legt nur der Inhaber an.
