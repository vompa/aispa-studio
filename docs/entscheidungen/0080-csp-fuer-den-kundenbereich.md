# 0080. CSP für den Kundenbereich

- **Status:** angenommen
- **Datum:** 26.10.2026
- **Entschieden von:** Claude, autonom nach ADR 0019 (Runde R02, Slice 47, Befund B31; Freigabe der Runde durch den Auftraggeber am 26.10.2026)

## Kontext

Der Kundenbereich `/buchen/` ist die einzige Oberfläche, die Unbekannte im offenen Netz aufrufen. Er nimmt Kontaktdaten, Gesundheitsangaben (Anamnese) und Bezahlungen (Anzahlung, Kartengarantie, Gutscheine) entgegen. Die Befundliste vom 23.10.2026 (B31) fand keine Content-Security-Policy: weder einen Header in der API noch in `docker/nginx.conf` noch ein `<meta http-equiv>` in `web/buchen/index.html`. Eine CSP begrenzt, woher die Seite Skripte, Stile, Bilder und Verbindungen laden darf, und macht eingeschleusten Code wirkungslos.

Ausgeliefert wird `/buchen/` an zwei Stellen: im Server-Betrieb von nginx, im Einzelplatz von der API selbst (`Hosting:WebRoot`, ADR 0041). In der Entwicklung liefert Vite aus.

Die Bezahlung läuft heute nur per Weiterleitung zu Stripe Checkout. Der Kundenbereich lädt kein Stripe.js, bettet keinen Stripe-Frame ein und spricht `api.stripe.com` nicht aus dem Browser an.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: keine CSP | Status quo; eingeschleuster Code liefe ungehindert, gerade auf der Seite mit Zahlungs- und Gesundheitsdaten |
| B: sofort durchsetzende CSP | wirksam ab dem ersten Tag, aber ein übersehener Ladeweg bricht die Buchung für Gäste |
| C: zuerst `Content-Security-Policy-Report-Only`, dann durchsetzen | gewählt: Verstöße erscheinen in der Browser-Konsole, ohne dass etwas bricht; das Umschalten ist ein kleiner, geprüfter Schritt |
| D: CSP als `<meta http-equiv>` in `index.html` | ein Ort für alle Auslieferungen, aber Report-Only gibt es als Meta-Tag nicht, und `frame-ancestors` wirkt dort nicht |

## Entscheidung

1. Jede Antwort auf `/buchen/` trägt den Header `Content-Security-Policy-Report-Only` mit
   `default-src 'self'; base-uri 'self'; object-src 'none'; form-action 'self'`.
2. Die Richtlinie steht einmal im Code (`BookingContentSecurityPolicy` in `src/AispaStudio.Api/System`) und wortgleich in `docker/nginx.conf` (`location /buchen/`, mit `always`). Ein Test hält beide gleich, ein zweiter prüft den Header der API.
3. Keine Stripe-Quelle wird freigegeben, solange die Bezahlung eine Seitennavigation zu Stripe Checkout ist: Navigationen erfasst keine CSP-Direktive. Kommt später Stripe.js oder Stripe Elements, kommen gezielt dazu: `script-src 'self' https://js.stripe.com`, `frame-src https://js.stripe.com https://hooks.stripe.com`, `connect-src 'self' https://api.stripe.com`.
4. Nur der Kundenbereich bekommt eine CSP. Die Team-Oberfläche, „Mein Tag“, die Hilfe und die Seite des Betreibers bleiben vorerst ohne.
5. Der Vite-Dev-Server setzt keine CSP: er spritzt Inline-Skripte für das Neuladen ein, die Meldungen wären nur Rauschen.

## Begründung

`default-src 'self'` passt zum gebauten Kundenbereich: Vite erzeugt externe Skript- und Stildateien unter `/assets/`, alle Datenabrufe gehen an `/api` derselben Herkunft, und Bilder liegen im Build. `base-uri`, `object-src` und `form-action` fallen nicht unter `default-src` und werden deshalb eigens eng gesetzt. Report-Only zuerst, weil ein Fehler in der Richtlinie sonst Buchungen verhindert, die man im Test nicht sieht (andere Browser, Erweiterungen, Übersetzungsdienste).

## Folgen und Stand

- Umgesetzt in Slice 47: `TeamWebRoot.UseTeamWebRoot` setzt den Header für `/buchen`, `docker/nginx.conf` in `location /buchen/`; Tests in `tests/AispaStudio.Api.Tests/TeamWebRootTests.cs`.
- **Weg zum Durchsetzen:** Nach einem ausgelieferten Stand, in dem der Kundenbereich in Chrome, Edge, Firefox und Safari ohne CSP-Meldung in der Konsole läuft (Buchung, Anzahlung, Kartengarantie, Gutschein, Anmeldung), in `BookingContentSecurityPolicy.HeaderName` und in `docker/nginx.conf` `Content-Security-Policy-Report-Only` durch `Content-Security-Policy` ersetzen. Der nginx-Test zieht den Header-Namen aus der Konstanten mit; der Test der API nennt den Namen bewusst wörtlich und wird beim Umschalten auf `Content-Security-Policy` angepasst. Wer vor dem Umschalten Meldungen aus dem Betrieb sehen will, ergänzt einen Meldeweg (`report-to` mit eigenem Endpunkt); das ist neue, öffentlich erreichbare API-Fläche und bekommt dann eine eigene Entscheidung.
- **Einbetten:** entschieden, siehe den Nachtrag unten (Runde R03, Slice 58).
- Andere Webserver vor der API (eigener Proxy beim Kunden) müssen den Header selbst setzen oder durchreichen; nginx aus `docker/` tut es.

## Nachtrag: Einbetten auf der eigenen Website (Runde R03, Slice 58)

Der Auftraggeber entschied in Runde R02, dass Spas den Kundenbereich auf ihrer eigenen Website einbetten dürfen, aber nur dort, wo sie es eintragen. Die Regel:

1. **Einstellung:** Je Spa gibt es die Liste „Einbetten erlaubt auf“ (`Tenant.EmbedAllowedOrigins`, Verwaltung › Stammdaten › Firmendaten, `GET`/`PUT /api/tenant/embedding`, Recht `ManageOrganization`). Jeder Eintrag ist `https://host` oder `https://host:port`: kein Pfad, keine Abfrage, kein Anker, keine Zugangsdaten, kein Platzhalter; höchstens 10; Umlaute im Namen nur als Punycode (`xn--`), denn Header erlauben nur ASCII. Die API vereinheitlicht (Kleinbuchstaben, ohne Schrägstrich am Ende, ohne Doppelte) und weist alles andere mit einem Fehler am Feld `allowedOrigins` ab, ohne etwas zu speichern.
2. **Header:** Jede Seite unter `/buchen/` (auch `index.html`, nicht die übrigen Dateien) trägt zusätzlich einen **durchsetzenden** Header `Content-Security-Policy: frame-ancestors 'self' <Liste>`. Ohne Eintrag steht nur `'self'`. Die Richtlinie für Inhalte bleibt wie oben in `Content-Security-Policy-Report-Only`, denn `frame-ancestors` sperrt im Berichtsmodus nichts. Beim Durchsetzen (siehe „Weg zum Durchsetzen“) werden beide Header zu einem zusammengeführt.
3. **Welches Spa:** `?spa=` der Adresse, sonst der einzige Mandant (wie `OnlineTenant`); sonst `'self'`. Die Adresse im `iframe` trägt `?spa=` wie jeder Link zur Online-Buchung.
4. **Server-Betrieb (nginx):** nginx kennt die Datenbank nicht. `location /buchen/` fragt per `auth_request` (Modul `ngx_http_auth_request_module`, im Image `nginx:alpine` enthalten) den anonymen Endpunkt `GET /api/online/embedding-policy` der API, die den fertigen Wert in `X-Frame-Ancestors` liefert; nginx setzt ihn in `Content-Security-Policy`. Das Spa reicht nginx als `X-Original-URI` weiter, weil eine Unteranfrage die Abfrage der Seite nicht erbt. Der Endpunkt hängt nicht am Modul Online-Buchung und hat keine Ratenbegrenzung, damit ein 429 nie die Seite sperrt. Ist die API nicht erreichbar, antwortet `/buchen/` mit 500; die Buchung ginge ohnehin nicht.
5. **Einzelplatz:** Die API setzt den Header selbst (`TeamWebRoot`). Ein eigener Proxy vor der API muss den Header durchreichen oder wie nginx die Liste erfragen.
6. **Umschalten:** Die Liste gilt sofort ohne Neustart. Wer das Einbetten ganz verbieten will, lässt die Liste leer; eine weitere Stufe (`'none'`) gibt es nicht. Das Durchsetzen der Inhalts-Richtlinie bleibt davon unberührt und folgt dem Weg oben.

Tests: `BookingEmbeddingTests` (Header ohne und mit Eintrag, Abweisen falscher Adressen, Vereinheitlichung, Recht, nginx-Konfiguration) und `TeamWebRootTests`.

## Quellen

- Befund B31 in [docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md](../umsetzung/aenderungsliste-oberflaechen-2026-10-23.md)
- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md) (Einzelplatz liefert die Web-Oberfläche aus der API)
- MDN „Content-Security-Policy“ und „Content-Security-Policy-Report-Only“; Stripe-Dokumentation „Content Security Policy“
