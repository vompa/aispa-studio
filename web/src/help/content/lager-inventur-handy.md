---
art: ablauf
bereich: Lager
oberflaeche: handy, verwaltung
rollen: therapeut, rezeption, leitung, inhaber, manager
seite: myday
stichwoerter: Lieferschein, Lieferschein fotografieren, Lieferung buchen, Lieferschein-Foto, delivery note, Inventur am Handy, Zählung vom Handy, Lager zählen, Handy zählen, Barcode scannen, Kamera, Wareneingang scannen, Zählung abschließen, Zählung übernehmen, count on phone, scan barcode
betrieb: server
---
# Inventur am Handy zählen

Im Spa zählt man das Material mit dem Handy am Regal, die Leitung schließt die Zählung danach in der Windows-App ab. Erst dann ändert sich der Bestand.

## Am Handy zählen {#zaehlen}

1. In **Mein Tag** den Reiter **Inventur** öffnen.
2. Den Lagerort wählen, z. B. den Schrank eines Raums, und auf **Zählung starten** tippen. Hat jemand an diesem Lagerort schon begonnen, setzt man dessen Zählung fort.
3. Bei jedem Material die Menge im Feld eintragen. Das Soll zeigt die Maske nicht, damit man zählt und nicht abschreibt.
4. Warten, bis neben dem Feld „Gespeichert“ steht. Jede Eingabe ist ein Zwischenstand; ein zweites Handy sieht ihn, und die Zählung geht beim Neuladen der Seite nicht verloren.
5. Auf **Abschicken** tippen und mit **Jetzt abschicken** bestätigen. Danach steht „Die Leitung schließt die Zählung ab.“ und die Felder sind gesperrt.

### Ohne Netz im Lagerraum {#ohne-netz}

Fällt die Verbindung beim Zählen aus, zählen Sie weiter. Das Handy merkt sich die Mengen und zeigt „N Mengen warten auf Verbindung.“ Sobald es wieder Netz hat, sendet es sie von selbst; mit **Jetzt senden** geht es sofort.

- **Zählung starten** und **Abschicken** brauchen eine Verbindung. Abschicken geht erst, wenn keine Menge mehr wartet.
- Die gemerkten Mengen bleiben auch nach dem Schließen der Seite stehen. Wer die Zählung am selben Handy wieder öffnet, sieht sie im Feld; sie gehen dann gleich hinaus.
- **Abmelden** löscht gemerkte Mengen. Meldet sich eine andere Person an diesem Handy an, sind sie ebenfalls weg. Darum vor dem Abmelden warten, bis „Gespeichert“ dasteht.
- Neues Material per Scan hinzufügen geht nur mit Verbindung.

## Mit der Kamera scannen {#scan}

Statt in der Liste zu suchen, scannt man den Code auf der Verpackung. Die Maske springt zur Zeile des Materials.

1. Auf **Kamera starten** tippen und die Freigabe der Kamera erlauben.
2. Den Barcode vor die Kamera halten. Das Handy springt in das Feld des Materials; die Kamera bleibt an, bis die Person sie mit **Kamera beenden** ausschaltet; so lässt sich gleich das nächste Material scannen.
3. Menge eintragen.

Kann das Handy keine Codes lesen, oder ist die Kamera nicht freigegeben, bleibt das Feld **Code eingeben**. Dort tippt man die Nummer unter dem Barcode, und die Maske springt genauso zur Zeile. Gehört der Code zu keinem Material, sagt die Maske es.

## Wareneingang am Handy {#wareneingang}

Rezeption und Leitung buchen im Reiter **Wareneingang** gelieferte Ware: Code scannen oder eintippen, auf **Suchen** tippen, die Menge eintragen, bei Bedarf **Charge (optional)** und das Ablaufdatum, dann auf **Wareneingang buchen** tippen. Therapeut:innen sehen diesen Reiter nicht.

## Lieferung zu einer Bestellung, mit Lieferschein-Foto {#lieferung}

Die Leitung bucht im Reiter **Lieferung** eine Lieferung zu einer offenen Bestellung. Den Reiter sieht nur, wer Standorte verwalten darf.

1. Bei **Bestellung** die Bestellung wählen. Zur Wahl stehen bestellte und teilweise gelieferte Bestellungen des Standorts.
2. Auf **Lieferschein fotografieren (optional)** tippen und den Lieferschein aufnehmen. Die KI liest ihn und trägt je Position Menge, Charge und Ablaufdatum ein, dazu die Lieferscheinnummer.
3. Alle Mengen mit dem Lieferschein vergleichen. Steht bei einer Position „Bitte prüfen: …“, war die KI unsicher oder es ist mehr geliefert als noch offen war.
4. Auf **Lieferung buchen** tippen. Erst dann ändert sich der Bestand.

- Die KI schlägt nur vor. Ohne den Knopf wird nichts gebucht.
- Das Foto wird nur gelesen und nicht gespeichert. Es geht dazu an den KI-Dienst (Anthropic).
- Was auf dem Lieferschein steht, aber nicht zur Bestellung gehört, listet die Maske unter „Auf dem Lieferschein, aber nicht in dieser Bestellung“. Solche Ware bucht man im Reiter **Wareneingang**.
- Ohne Foto geht es genauso: Mengen von Hand eintragen und buchen. Ist die KI nicht eingerichtet oder das Foto unlesbar, sagt die Maske es.
- Eine Position ohne Menge gilt als nicht geliefert; die Bestellung bleibt dafür offen.
- In der Windows-App bucht man Lieferungen wie bisher unter **Verwaltung** › **Lager** bei der Bestellung, dort ohne Foto.

## Zählung in der Windows-App abschließen {#abschliessen}

1. In der Verwaltung **Lager** öffnen und den Reiter **Inventur** wählen. Über dem Lagerort steht „N Zählungen vom Handy warten“ mit Lagerort, zählender Person und Zeit.
2. In der gewünschten Zeile auf **Übernehmen** klicken. Die Zählliste zeigt das Gezählte, das Soll, die Differenz und den Wert.
3. Auf **Inventur buchen** klicken und mit **Inventur buchen** bestätigen. Das Lager bucht jede Abweichung als Inventur und nimmt die Zählung aus der Liste.

Mit **Verwerfen** nimmt man eine Zählung aus der Liste, ohne zu buchen, z. B. wenn am falschen Lagerort gezählt wurde. Mit **Zurück** verlässt man die übernommene Zählung; sie bleibt in der Liste. Abschließen und verwerfen dürfen nur Personen mit dem Recht für den Standort.

## Gut zu wissen {#hinweise}

- Gebucht wird der Unterschied zum Bestand im Moment des Abschlusses, nicht zum Bestand beim Zählen. Wurde dazwischen verbraucht, zieht das Lager es ab.
- Die Windows-Inventur am Rechner bleibt: [Inventur buchen](hilfe:lager-inventur).
- Mit einem Handscanner am Rechner geht es schneller: [Lager, Mit dem Handscanner arbeiten](hilfe:lager#scanner).
- Wie viel Material die Behandlungen laut Rezept brauchen und wie viel die Inventuren fehlen sahen: [Lager, Verbrauch](hilfe:lager#verbrauch).
