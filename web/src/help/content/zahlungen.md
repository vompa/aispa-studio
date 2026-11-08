---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
---
# Zahlungen

Einstellungen in der Windows-App unter „Verwaltung“ › „Kassa & Geld“ › „Zahlungen“. Hier legt das Spa fest, auf welches Konto Online-Zahlungen gehen. Ändern darf das, wer die Firmendaten pflegen darf (Inhaber). In der Windows-App steht ein hinterlegter Schlüssel nur als „••••“ mit seinen letzten vier Zeichen da; ein leeres Feld behält den gespeicherten Schlüssel.

![Zahlungen in der Windows-App](images/app/verwaltung-zahlungen.png)

## Online-Zahlungen {#online-zahlungen}

Online bezahlt werden im Kundenbereich:

- Gutscheine, die Gäste online kaufen,
- hinterlegte Karten für eine Ausfallgebühr (No-Show-Schutz),
- Ausfallgebühren, die die Kassenaufsicht später belastet.

Das Geld geht immer direkt auf das Stripe-Konto des Spas. AISPA hält kein Geld. Jede Online-Zahlung bekommt trotzdem ihren Beleg an der Kassa des Standorts, so wie eine Kartenzahlung vor Ort.

Ganz oben steht der Stand des Zahlungskontos, zum Beispiel „Verbunden. Online-Zahlungen gehen direkt auf das Stripe-Konto des Spas.“ oder „Noch kein Zahlungskonto: online gibt es keine Gutscheine und keine Ausfallgebühr.“

## Wie nimmt das Spa Online-Zahlungen an? {#zahlungsart}

Drei Möglichkeiten, von denen genau eine gilt.

### Mit Stripe verbinden (empfohlen) {#auswahl-connect}

Das Spa verbindet sein eigenes Stripe-Konto mit wenigen Klicks. Es kopiert keine Schlüssel; Stripe fragt Firmendaten und Bankkonto selbst ab und zahlt direkt an das Spa aus. Stripe verrechnet seine Gebühren mit dem Spa. AISPA behält derzeit keine eigene Gebühr ein.

Steht darunter „Auf diesem Server nicht eingerichtet.“, ist der Server noch nicht als Stripe-Plattform angemeldet. Das richtet der Betreiber von AISPA einmal ein, siehe „Stripe Connect als Plattform einrichten“ weiter unten. Bis dahin gehen die eigenen Stripe-Schlüssel.

### Eigene Stripe-Schlüssel {#auswahl-eigene-schluessel}

Für ein Spa, das schon ein Stripe-Konto hat und es selbst einrichten kann. Es trägt den geheimen Schlüssel und das Webhook-Geheimnis aus seinem Stripe-Dashboard ein. Das geht sofort, auch ohne Plattform-Anmeldung.

### Keine Online-Zahlung {#auswahl-keine}

Online gibt es dann keine Gutscheine und keine Ausfallgebühr. Gehört der Server dem Spa selbst (Einzelbetrieb) und ist dort ein Stripe-Konto des Servers hinterlegt, gilt stattdessen dieses Konto; die Oberfläche sagt dann „Es gilt das Stripe-Konto dieses Servers.“

### Wechsel des Kontos {#wechsel}

Hinterlegte Karten und frühere Zahlungen gehören zum bisherigen Konto. Nach einem Wechsel lassen sie sich nicht mehr über AISPA belasten oder erstatten, sondern nur noch im Stripe-Dashboard. Die Oberfläche warnt vor dem Wechsel.

## Stripe Connect {#stripe-connect}

1. „Mit Stripe verbinden“ wählen. AISPA legt im Hintergrund ein Stripe-Konto für das Spa an und holt einen Einrichtungslink („Stripe wird vorbereitet …“).
2. „Stripe öffnen“ öffnet die Einrichtung bei Stripe im Browser. Dort Firmendaten, vertretungsberechtigte Person und Bankkonto angeben.
3. Danach zurück in AISPA. Die Rückkehrseite im Browser nennt den nächsten Schritt; in der Windows-App **Stand abfragen** wählen.
4. Sobald Stripe das Konto freigibt, steht „Verbunden“ da und der Kundenbereich verkauft Gutscheine und verlangt Ausfallgebühren.

Ist die Einrichtung unterbrochen, setzt „Einrichtung bei Stripe fortsetzen“ dort fort, wo man aufgehört hat. Ein Einrichtungslink gilt bei Stripe nur wenige Minuten; AISPA holt bei Bedarf einen neuen. „Angaben bei Stripe gemacht; Stripe prüft sie noch.“ heißt: alles eingegeben, Stripe prüft noch.

## Eigene Stripe-Schlüssel {#eigene-schluessel}

Beides steht im Stripe-Dashboard unter „Entwickler“. Die Schlüssel werden verschlüsselt gespeichert und nie wieder angezeigt, nur ihr Anfang und Ende.

### Geheimer Schlüssel {#geheimer-schluessel}

Beginnt mit `sk_live_` (echtes Geld) oder `sk_test_` (Probe). Ein eingeschränkter Schlüssel beginnt mit `rk_`; er braucht Schreibrechte für Checkout-Sitzungen, Kunden, Zahlungen und Erstattungen. Nach dem Speichern prüft AISPA den Schlüssel bei Stripe; kennt Stripe ihn nicht, ist das Konto nicht bereit. Bei einem Testschlüssel steht „Testschlüssel hinterlegt: Zahlungen sind nur Probe, es fließt kein echtes Geld.“

### Webhook-Geheimnis {#webhook-geheimnis}

Beginnt mit `whsec_`. Ohne es erfährt AISPA nicht, dass bezahlt wurde. So richtet man den Webhook ein:

1. Im Stripe-Dashboard „Entwickler“ › „Webhooks“ › „Endpunkt hinzufügen“.
2. Als Adresse die Webhook-Adresse eintragen, die AISPA nach dem Speichern unter den Feldern anzeigt (sie endet auf `/api/hooks/payments/stripe/` und der Kennung des Spas).
3. Diese Ereignisse auswählen: `checkout.session.completed`, `checkout.session.expired`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`.
4. Das angezeigte Signaturgeheimnis (`whsec_…`) kopieren und in AISPA als Webhook-Geheimnis eintragen, dann „Speichern“.

## Stripe Connect als Plattform einrichten {#plattform-einrichten}

Diese Anleitung ist für den Betreiber von AISPA, nicht für das einzelne Spa. Sie wird einmal je Server gemacht; danach können alle Spas auf diesem Server „Mit Stripe verbinden“ wählen. Die Anmeldung ist eine Vereinbarung mit Stripe und wird deshalb nur vom Betreiber selbst abgeschlossen.

Stripe Connect ist eine Konfigurationsoption des Servers. Ohne sie funktioniert alles mit eigenen Stripe-Schlüsseln weiter.

### Was man braucht {#plattform-voraussetzungen}

- Ein Stripe-Konto für das Unternehmen, das AISPA betreibt, mit Firmendaten, Firmenbuchnummer bzw. UID und einem Geschäftskonto.
- Die öffentliche Adresse des Kundenbereichs, unter der Stripe den Server erreicht (z. B. `https://buchen.example.at`). Sie steht in der Server-Einstellung `OnlineBooking:PublicUrl`.
- Zugriff auf die Einstellungen des Servers (Geheimnisspeicher, Umgebungsvariablen oder user-secrets).

### Bei Stripe anmelden {#plattform-schritte}

1. Unter [dashboard.stripe.com/register](https://dashboard.stripe.com/register) ein Stripe-Konto für den Betreiber anlegen und die Firmenprüfung abschließen (Firmendaten, vertretungsberechtigte Person, Bankkonto).
2. Im Stripe-Dashboard „Connect“ öffnen und die Einrichtung als Plattform starten. Als Geschäftsmodell „Plattform“ wählen: Software, über die eigenständige Betriebe (die Spas) Zahlungen ihrer Kundinnen annehmen.
3. Als Kontotyp der verbundenen Konten **Standard** wählen. Dann hat jedes Spa ein vollwertiges eigenes Stripe-Konto mit eigenem Dashboard, ist selbst Händler gegenüber dem Gast, trägt Stripe-Gebühren und Rückbuchungen selbst und bekommt die Auszahlungen direkt. Das passt zu AISPA: Die App erzeugt direkte Zahlungen auf dem Konto des Spas.
4. Das Plattformprofil ausfüllen (was die Plattform macht, Branche Wellness und Spa, Länder Österreich, Deutschland, Schweiz) und den Bedingungen für Connect zustimmen. Stripe prüft das Profil; das kann einige Tage dauern.
5. Unter „Connect“ › „Einstellungen“ das Erscheinungsbild der Einrichtung festlegen (Name „AISPA Studio“, Symbol, Farbe). Das sehen die Spas, wenn sie „Stripe öffnen“ klicken.
6. Zuerst im **Testmodus** weitermachen: Unter „Entwickler“ › „API-Schlüssel“ den geheimen Schlüssel kopieren (`sk_test_…`).
7. Unter „Entwickler“ › „Webhooks“ einen Endpunkt anlegen, der **Ereignisse von verbundenen Konten** empfängt. Adresse: die öffentliche Adresse plus `/api/hooks/payments/stripe`, also z. B. `https://buchen.example.at/api/hooks/payments/stripe`. Ereignisse: `account.updated`, `checkout.session.completed`, `checkout.session.expired`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`. Das Signaturgeheimnis (`whsec_…`) kopieren.

### Am Server einschalten {#plattform-server}

Diese Einstellungen setzen und den Server neu starten. Schlüssel gehören in den Geheimnisspeicher oder in Umgebungsvariablen, nie in Dateien im Repository.

- `OnlineVouchers:Stripe:Connect` = `true`
- `OnlineVouchers:Stripe:SecretKey` = der geheime Schlüssel der Plattform
- `OnlineVouchers:Stripe:ConnectWebhookSecret` = das Signaturgeheimnis aus dem Webhook
- `OnlineBooking:PublicUrl` = die öffentliche Adresse des Kundenbereichs

Als Umgebungsvariablen (Docker, Windows-Dienst) schreibt man den Doppelpunkt als zwei Unterstriche, z. B. `OnlineVouchers__Stripe__Connect=true`. Auf einem Entwicklungsrechner geht es mit `dotnet user-secrets set "OnlineVouchers:Stripe:Connect" "true" --project src/AispaStudio.Api`.

### Ausprobieren und live schalten {#plattform-live}

1. In AISPA unter „Verwaltung“ › „Kassa & Geld“ › „Zahlungen“ ist „Mit Stripe verbinden“ jetzt wählbar. Mit einem Test-Spa einmal durchspielen: verbinden, die Einrichtung bei Stripe mit Testdaten abschließen, im Kundenbereich einen Gutschein mit einer Stripe-Testkarte kaufen und prüfen, dass der Beleg an der Kassa entsteht.
2. Wenn alles passt: im Stripe-Dashboard den Testmodus verlassen, API-Schlüssel und Webhook im Live-Modus noch einmal anlegen (`sk_live_…`, neuer Webhook mit neuem Geheimnis) und die Werte am Server ersetzen.

### Gut zu wissen {#plattform-hinweise}

- Auf einer Plattform zahlt der Schlüssel der Plattform nie selbst; jede Zahlung läuft auf dem verbundenen Konto des jeweiligen Spas.
- Ohne `Connect` gilt ein am Server hinterlegter Schlüssel (mit `OnlineVouchers:Stripe:WebhookSecret`) als Konto des Servers für Spas ohne eigenes Zahlungskonto. Das ist für einen Server gedacht, der einem einzigen Spa gehört.
- `OnlineVouchers:PaymentProvider` = `None` schaltet Online-Zahlung am ganzen Server ab, `Test` simuliert Zahlungen für Spas ohne Konto (nur zum Ausprobieren).
- Die Schlüssel der Spas (eigene Schlüssel) sind an den Schlüsselring des Servers gebunden. Zieht ein Spa auf einen anderen Server um, trägt es sie dort neu ein.
