# Slice 103 – Eingebettete Verwaltung und Web-Team-Oberfläche entfernen

**Ziel:** Nachdem jede Maske nativ ist, fallen die eingebettete Verwaltung (WebView2) und die Web-Team-Oberfläche weg; alle Sprungziele führen in native Masken, der Einzelplatz liefert keine Team-Webseiten mehr aus.
**Runde:** R05
**Quelle:** R05 – Ä191, Ä286
**Hängt ab von:** 94, 95, 96, 97, 98, 99, 100, 101, 102, 123, 124, 125, 126, 127
**Modell:** opus
**IDs für neue Befunde:** Ä229–Ä231

## Umfang
- Scan-Test: jedes Sprungziel (Kalender, Assistent, Hilfe „Jetzt ergänzen“) landet in einer nativen Maske
- AdminView mit eingebettetem Browser und `embeddedTarget` entfernen; WebView2 bleibt nur für die Hilfe (E1)
- Web-Team-Oberfläche (web/src/App.tsx und die nur dort genutzten Ordner) entfernen; Kundenbereich, Mein Tag, Hilfe-Inhalte und Dashboard bleiben
- Einzelplatz (`Hosting:Standalone`) liefert keine Team-Webseiten mehr aus
- CI, Bundle-Prüfung, README und Handbuch nachziehen
- Kleine Seite `/anmelden/` für Code der Windows-App, Annahme einer Einladung mit Passkey, Passkey hinzufügen und Registrierung eines neuen Spas; Link „Mit Passwort anmelden“ in Mein Tag führt dorthin; Nachtrag zu ADR 0084, Punkt 5
- Stripe-Rückkehradresse `/?zahlungen=1` umstellen

## Muster übernehmen
- docs/umsetzung/masken-inventar.md aus Slice 92

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/AdminView.axaml
- src/AispaStudio.Desktop/Views/EmbeddedBrowser.cs
- web/src/
- src/AispaStudio.Api/
- .github/workflows/
- README.md

## NICHT bauen
- Entfernen von Kundenbereich, Mein Tag oder Hilfe-Inhalten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 103 "Eingebettete Verwaltung und Web-Team-Oberfläche entfernen" um (diese Datei).
1. Stelle zuerst einen Plan vor (Dateien, Klassen, Tests) mit einem Abschnitt
   „Annahmen (bei Freigabe bestätigen)“ für jeden offenen Punkt und einem kurzen „Review-Fokus“,
   speichere ihn unter work/plans/ und warte auf Freigabe.
2. Schreibe die Abnahmetests dieser Slice als fehlschlagende Tests (ROT).
3. Implementiere das Minimum bis grün. Halte jede Regel in CLAUDE.md und work/conventions.md ein.
4. Prüfe die Definition of Done. Halte Entscheidungen, Überraschungen und zurückgestellte
   Kleinigkeiten in der Rundenakte und in der Protokolldatei fest, die work/conventions.md nennt.
```

## Definition of Done
- [ ] Prüfungen aus work/conventions.md grün
- [ ] Kein Test abgeschwächt oder übersprungen
- [ ] Keine neuen Pakete ohne Freigabe
- [ ] Entscheidungen festgehalten
- [ ] Given alle Sprungziele des Inventars When der Scan-Test sie öffnet Then zeigt jedes eine native Maske und kein Browser-Fenster
- [ ] Given der Einzelplatz When /team im Browser aufgerufen wird Then antwortet die API mit 404
- [ ] Given der Server-Betrieb ohne Team-Oberfläche When eine eingeladene Person den Einladungslink öffnet Then nimmt sie die Einladung auf `/anmelden/` an und kann die Windows-App mit einem Code anmelden
- [ ] Manuell: Einzelplatz frisch starten und jede Verwaltungsseite einmal öffnen

## Nicht in dieser Slice
–
