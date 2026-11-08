# Slice 92 – Grundsatz: alle Masken nativ, Web nur für Dashboard und Handy

**Ziel:** ADR 0084 hält fest, dass jede Eingabe- und Verwaltungsmaske nativ in der Windows-App läuft und das Web nur noch das Dashboard für den Pausenraum, die Handy-App für Mitarbeitende und die Handy-App für Kunden liefert; ein Masken-Inventar ordnet jede heutige Web-Seite einer Slice zu.
**Runde:** R05
**Quelle:** R05 – Ä191, Ä192, Ä193, Ä194, Ä195
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** Ä196–Ä198

## Umfang
- ADR 0084 nach docs/entscheidungen/vorlage.md mit den Rulings und den beim Start beantworteten offenen Entscheidungen dieser Runde
- Nachträge zu ADR 0041 (Webmodule: Dashboard, Mitarbeiter-App, Kunden-App), ADR 0053 (eingebettete Verwaltung wird abgelöst) und ADR 0059 (Verwaltung nativ statt eingebettet)
- Masken-Inventar docs/umsetzung/masken-inventar.md: jede Seite und jeder Reiter der Web-Team-Oberfläche (web/src/App.tsx, admin/, roster/, time/, payroll/, stock/, promotions/, subscriptions/, reports/, costs/, accounting/, businessinfo/, closures/) mit Ziel (nativ in Slice NN, entfällt, Handy-App) und den Sprungzielen aus Kalender und Assistent
- Fernzugriff (E2): Verwalten von zu Hause geht über dieselbe Windows-App mit Verbindung zum Server des Spas bzw. zum Cloud-Betrieb, nicht im Einzelplatz und nicht am Mac; prüfen, was dafür heute fehlt (Erreichbarkeit von außen, TLS und Zertifikats-Pin, Anmeldung, Sperre bei Verlust des Geräts) und Lücken als Befunde Ä196–Ä198 festhalten
- CLAUDE.md: Regel „Web-Oberflächen (Verwaltung im WebView2 …)“ und Hinweise zur eingebetteten Verwaltung an ADR 0084 anpassen; nächste freie ADR-Nummer nachziehen
- docs/stand-und-offene-punkte.md und docs/uebergabe.md nachziehen

## Muster übernehmen
- docs/entscheidungen/0059-zum-start-nur-die-windows-app.md – Aufbau und Ton
- docs/entscheidungen/0053-verwaltung-in-der-windows-app-eingebettet.md – was abgelöst wird

## Voraussichtlich geänderte Dateien
- docs/entscheidungen/
- docs/umsetzung/masken-inventar.md
- docs/stand-und-offene-punkte.md
- docs/uebergabe.md
- CLAUDE.md

## NICHT bauen
- Code-Änderungen
- neue Masken

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 92 "Grundsatz: alle Masken nativ, Web nur für Dashboard und Handy" um (diese Datei).
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
- [ ] Given das Masken-Inventar When es mit den Seiten in web/src/App.tsx und den Reitern der Stammdaten verglichen wird Then hat jede Seite und jeder Reiter genau ein Ziel
- [ ] Given ADR 0084 When docs/entscheidungen/README.md gelesen wird Then steht er im Index und ADR 0053 trägt den Status „abgelöst durch 0084“
- [ ] Given ADR 0084 When der Auftraggeber ihn liest Then nennt er den Weg der Verwaltung von zu Hause (Windows-App mit Serververbindung) und was dafür noch fehlt
- [ ] Manuell: der Auftraggeber liest ADR 0084 und das Inventar und bestätigt die Reihenfolge der Masken-Slices

## Nicht in dieser Slice
–
