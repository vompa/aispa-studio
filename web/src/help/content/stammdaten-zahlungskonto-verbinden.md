---
art: ablauf
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
stichwoerter: Zahlungskonto, Stripe verbinden, Online-Zahlung, Kreditkarte online, Gutschein online verkaufen, Ausfallgebühr einziehen, Zahlungsanbieter, Webhook, API-Schlüssel, payment provider, connect Stripe, online payments
---
# Zahlungskonto verbinden

Damit Gäste online Gutscheine kaufen und für eine Ausfallgebühr eine Karte hinterlegen können, braucht das Spa ein Stripe-Konto. Das Geld geht direkt auf dieses Konto; jede Zahlung bekommt trotzdem ihren Beleg an der Kassa. Einrichten darf das nur der Inhaber.

## Mit Stripe verbinden (empfohlen) {#connect}

1. In der Windows-App **Verwaltung** öffnen, im Balken **Kassa & Geld** und dann den Reiter **Zahlungen** wählen.
2. Bei **Wie nimmt das Spa Online-Zahlungen an?** die Möglichkeit **Mit Stripe verbinden (empfohlen)** wählen.
3. Auf **Mit Stripe verbinden** klicken. AISPA bereitet das Konto vor.
4. Auf **Stripe öffnen** klicken. Die Einrichtung öffnet sich im normalen Browser.
5. Bei Stripe Firmendaten, vertretungsberechtigte Person und Bankkonto angeben.
6. Zurück in AISPA: In der Windows-App **Stand abfragen** wählen.

![Zahlungen in der Windows-App](images/app/verwaltung-zahlungen.png)

Ist die Einrichtung unterbrochen, setzt **Einrichtung bei Stripe fortsetzen** dort fort. Steht unter der Möglichkeit „Auf diesem Server nicht eingerichtet …“, geht nur der Weg mit eigenen Schlüsseln.

## Eigene Stripe-Schlüssel {#eigene-schluessel}

1. Reiter **Zahlungen**, **Eigene Stripe-Schlüssel** wählen.
2. **Geheimer Schlüssel** und **Webhook-Geheimnis** aus dem Stripe-Dashboard eintragen.
3. Auf **Speichern** klicken.
4. Die danach angezeigte Webhook-Adresse im Stripe-Dashboard als Webhook mit den angezeigten Ereignissen eintragen.

## Gut zu wissen {#hinweise}

- Oben steht immer der Stand des Zahlungskontos, z. B. ob es verbunden ist.
- Beim Wechsel des Kontos warnt die Seite: Hinterlegte Karten und frühere Zahlungen gehören zum bisherigen Konto.
- **Keine Online-Zahlung** schaltet Online-Gutscheine und Ausfallgebühren ab.

Schritt für Schritt mit allen Feldern: [Stripe Connect](hilfe:zahlungen#stripe-connect), [Eigene Stripe-Schlüssel](hilfe:zahlungen#eigene-schluessel), [Wechsel des Kontos](hilfe:zahlungen#wechsel).
