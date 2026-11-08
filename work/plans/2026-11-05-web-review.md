# Durchsicht aller Web-Masken mit `impeccable` und `emil-design-eng`

Stand: 05.11.2026, Auftrag vom Auftraggeber. Ziel ist ein **Befundbericht**, keine Änderung: Die Durchsicht ändert keinen Code. Welche Befunde umgesetzt werden, entscheidet der Auftraggeber danach.

## Umfang

Alle sechs Web-Oberflächen (Einstiege in `web/vite.config.ts`), geprüft als Inhaberin, damit alle Seiten erreichbar sind:

| Nr. | Oberfläche | Pfad | Quelle | Wer sieht sie |
| --- | --- | --- | --- | --- |
| W1 | Kundenbereich (Handy-App für Kunden) | `/buchen/` | `web/src/booking/` (19 Komponenten: Buchen, Meine Termine, Verwalten, Verschieben, Warteliste, Gutscheine, Anzahlung, Kartengarantie, Anamnese, Einwilligung, Anmelden, Impressum, Barrierefreiheit) | Gäste |
| W2 | Mein Tag (Handy-App für Mitarbeitende) | `/mein-tag/` | `web/src/myday/` (Heute, Woche, Abwesenheit, Wareneingang, Inventur, Barcode, Erinnerungen, Anmelden) | Team |
| W3 | Dashboard für den Pausenraum | `/dashboard/` | `web/src/dashboard/` (Tafel, Koppeln) | Team, großer Bildschirm |
| W4 | Hilfe und Handbuch | `/help/` | `web/src/help/` (Index, Thema, Schublade, Rückmeldung, offene Fragen; auch im WebView2 der App) | Team |
| W5 | Anmelden | `/anmelden/` | `web/src/signin/` (Passwort, Einladung, Registrierung) | Team |
| W6 | Betreiberseite | `/betreiber/` | `web/src/operator/` | Betreiber der Plattform |

Gemeinsam genutzt: `web/src/components/` (`ConfirmDialog`, `FormField`), `web/src/index.css`.

## Maßstab

- **Wertequelle bleibt der Brief** `docs/design.md` (Farben, Abstände, Schriftgrößen, Radien, Bewegung). Die Skills bewerten, der Brief entscheidet. Ein Vorschlag, der vom Brief abweicht, wird als „Abweichung vom Brief, braucht Entscheidung des Auftraggebers“ markiert, nicht als Fehler.
- **`impeccable`** (Modus Kritik und Audit): Hierarchie, Layout, Typografie, Farbe und Kontrast, Zustände (leer, lädt, Fehler, ohne Recht), Texte in der Oberfläche, Randfälle (lange Namen, viele Einträge, schmale Bildschirme), Barrierefreiheit der Gestaltung.
- **`emil-design-eng`**: Feinschliff, Bewegung und ihr Zweck, Übergänge, Rückmeldung bei Druck und Hover, gefühlte Geschwindigkeit, unsichtbare Details (Fokusringe, Auswahl, Scrollen, Cursor), `prefers-reduced-motion`.
- Zusätzlich die Hausregeln: `docs/gestaltung/masken.md` (Kurz-Checkliste), `docs/gestaltung/meldungen.md`, `docs/gestaltung/texte.md`.

## Vorgehen

1. **Umgebung:** `docker compose -p web-review up -d --build db mailpit api` plus `npm run dev` in `web/` (Port 5173), Demo-Daten aus der leeren Datenbank. Anmeldung mit den Testkonten (`POST /api/auth/dev/login`) als Inhaberin; im Kundenbereich als Testgast.
2. **Aufnahmen je Seite** mit headless Chrome (wie `web/scripts/help-images.mjs`): Handybreite 390 × 844 für W1 und W2, 1920 × 1080 für W3, 1280 × 800 für W4 bis W6; jeweils hell und dunkel. Dazu die Zustände leer, lädt (gedrosselt), Fehler (API gestoppt) und mindestens ein Randfall. Ablage im Scratchpad, nicht im Repo.
3. **Durchsicht je Oberfläche** durch einen Subagenten (Sonnet), der beide Skills lädt, den Brief und die Hausregeln liest, Quelltext und Aufnahmen ansieht und Befunde im festen Format liefert. W1 und W2 laufen zuerst (am meisten Nutzer, Handy), dann W3 und W4, zuletzt W5 und W6. Höchstens zwei Agenten gleichzeitig (Last, siehe Fallstricke zu Vitest und Builds).
4. **Prüfen der Befunde** durch Claude im Hauptgespräch: jeden Befund an Aufnahme oder Quelltext nachsehen, Doppelte zusammenlegen, gegen den Brief abgleichen. Nur belegte Befunde kommen in den Bericht.
5. **Bericht** als Datei `work/reviews/2026-11-05-web-review.md`: je Oberfläche eine Tabelle mit Befund, Fundstelle (Datei:Zeile, Aufnahme), Schwere (blockiert Nutzung / stört / Feinschliff), Skill, Vorschlag, Aufwand (klein, mittel, groß), Brief-Konflikt ja/nein. Oben eine Zusammenfassung mit den zehn wichtigsten Punkten.
6. **Aufräumen:** `docker compose -p web-review down -v`, eigenen Vite-Prozess per Port beenden.

## Befundformat (für die Agenten)

```
- ID: W1-03
  Seite: /buchen/ › Buchen › Uhrzeit wählen (390 px, dunkel)
  Fundstelle: web/src/booking/BookPage.tsx:212, Aufnahme w1-book-slots-dunkel.png
  Befund: …
  Skill: impeccable | emil-design-eng
  Schwere: blockiert | stört | Feinschliff
  Vorschlag: …
  Aufwand: klein | mittel | groß
  Brief: passt | weicht ab (Abschnitt …)
```

## Bewusst nicht Teil der Durchsicht

- Technik nach `modern-web-guidance` und Verhalten nach `fluent2-web` (Tastatur, ARIA, CSP, INP/LCP): andere Skills, eigene Durchsicht, falls gewünscht.
- Die Windows-App (Avalonia) und die Masken, die dorthin gewandert sind (ADR 0084).
- Änderungen am Code; die Umsetzung kommt danach in eigenen Aufgaben.

## Aufwand und Dauer (geschätzt, nicht gemessen)

Sechs Agentenläufe plus Aufnahmen und Prüfung; grob zwei bis drei Stunden Laufzeit, davon das meiste in den Agenten. Kosten entstehen nur durch die Modellnutzung, keine Azure- oder Veröffentlichungskosten.

## Offen vor dem Start

- Kundenbereich: Soll er auch im eingebetteten Zustand (Buchungs-Widget auf der Spa-Website, `TenantEmbedAllowedOrigins`) geprüft werden?
- Dashboard: echte Bildschirmgröße im Pausenraum bekannt? Sonst 1920 × 1080.
