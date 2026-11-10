---
art: ablauf
bereich: Lager
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
stichwoerter: Inventur, Bestand zählen, Zählung, Ist-Bestand, Bestand korrigieren, Jahresinventur, stocktake, inventory count, physical count, Zählliste, Zählliste drucken, Inventurliste, Inventurliste drucken, Zettel zum Zählen, count sheet, print stocktake
---
# Inventur buchen

Bei der Inventur trägt man den gezählten Bestand eines Lagerorts ein. Das Lager bucht den Unterschied zum bisherigen Bestand selbst, und zwar nur an diesem Lagerort. Die Inventur bucht die Standortleitung, in der Windows-App unter „Verwaltung“ › „Lager“ im Reiter **Inventur**; vor dem Buchen fragt die Maske nach.

## Schritte {#schritte}

1. In der Verwaltung **Lager** öffnen, den Standort wählen und den Reiter **Inventur** öffnen.
2. Bei **Lagerort** den Ort wählen, den man zählt (Pflicht, es gibt kein „Alle Lagerorte“).
3. Auf **Zählung starten** klicken. Die Zählliste zeigt je Material das **Soll**, ein Feld **Gezählt**, die **Differenz** und ihren **Wert**; sie rechnen sich beim Eintippen aus.
4. Bei jedem gezählten Material die Menge unter **Gezählt** eintragen. Leere Felder bleiben unberührt. Mit **Soll ausblenden** zählt man „blind“.
5. Auf **Inventur buchen** klicken und die Rückfrage mit **Inventur buchen** bestätigen.

Danach steht „Inventur gebucht: N Positionen.“, oder „Bestand stimmt, nichts zu buchen.“, wenn die Zählung zum Bestand passt.

## Zählliste und Inventurliste drucken {#pdf}

Zum Zählen mit Zettel gibt es die Zählliste, nach dem Übernehmen einer Zählung die Inventurliste. Der Knopf öffnet jeweils eine Karte mit **Drucken** und **PDF speichern**, ohne **Per Mail senden**.

1. Im Reiter **Inventur** den Lagerort wählen und vor der Zählung neben **Zählung starten** auf **Zählliste drucken …** klicken. `Zählliste-<Lagerort>-<Datum>.pdf` zeigt alle aktiven Materialien mit Einheit und Soll, eine leere Spalte „Gezählt“ zum Hinschreiben und die Unterschriftenzeilen „Gezählt von“ und „Geprüft von“.
2. Nach dem Übernehmen einer Zählung vom Handy (**Übernehmen** bei den wartenden Zählungen) steht unter der Überschrift der Zählung der Knopf **Inventurliste drucken …**. `Inventurliste-<Lagerort>-<Datum>.pdf` zeigt Material, Einheit, Soll, Gezählt, Differenz mit Vorzeichen und Wert sowie „Summe der Differenzen (n Positionen)“, nur für gezähltes Material und mit „gezählt von <Name>“ in der Kopfzeile.

Ein Wechsel des Lagerorts passt den Pfad an. Zählliste und Inventurliste druckt nur die Standortleitung; ohne das Recht steht am grauen Knopf „Die Inventur druckt nur die Standortleitung.“. Wie die Knöpfe arbeiten: [Dokumente drucken, speichern und senden](hilfe:dokumente-drucken-speichern-senden).

## Gut zu wissen {#hinweise}

- Mit einem Handscanner findet man die Zeile schneller: Klicken Sie in das Feld **Material suchen oder scannen** und scannen Sie die Verpackung. Der Cursor springt in das Feld **Gezählt** des Materials: [Mit dem Handscanner arbeiten](hilfe:lager#scanner).
- Gebucht wird je Material, bei dem die Zahl vom Soll abweicht, als Bewegung „Inventur“ am gewählten Lagerort. Die anderen Lagerorte bleiben unberührt.
- Der gezählte Bestand darf nicht negativ sein.
- Gezählt wird auch am Handy; die Leitung schließt die Zählung hier ab: [Inventur am Handy zählen](hilfe:lager-inventur-handy).
- Mehr zu Lagerorten: [Lagerorte](hilfe:lager-lagerorte).
- Die Inventur ist auch der einfachste Weg, den Anfangsbestand eines neuen Materials zu erfassen.
- Für einzelne Abgänge mit Grund, z. B. Bruch, eignet sich die Korrektur: [Korrektur buchen](hilfe:lager-korrektur-buchen).
