# Slice 118 – Verwalten von zu Hause: Server-Adresse in der App eintragen

**Ziel:** Die Windows-App lässt sich ohne Umgebungsvariable mit einem Server verbinden: auf dem Startbildschirm und unter „Netz“ trägt man die Adresse des Servers ein, die App prüft die Verbindung und merkt sie; das Handbuch beschreibt das Verwalten von zu Hause im Server-Betrieb und über ein VPN des Spas.
**Runde:** R05
**Quelle:** R05 – Ä196, Ä197, Ä198
**Hängt ab von:** 92
**Modell:** sonnet
**IDs für neue Befunde:** Ä278–Ä280

## Umfang
- Startbildschirm: neben „Mit Haupt-PC im Netz verbinden“ ein Weg „Mit Server verbinden“ (Adresse `https://…`), Prüfung der Verbindung mit gültigem Zertifikat, dann in den Einstellungen merken und neu starten wie in ADR 0049
- Seite „Netz“: eingetragenen Server anzeigen und wieder lösen
- `AISPA_API_URL` behält Vorrang (Entwicklung, Aspire)
- Handbuch „Verwalten von zu Hause“: Server-Betrieb; Haupt-PC im eigenen Netz nur über ein VPN des Spas, keine Portweiterleitung (das Programm öffnet keine Ports, Ä197); bei verlorenem Gerät Zugang sperren, Windows-Kennwort und Laufwerksverschlüsselung, weil der Offline-Stand auf dem Gerät bleibt (Ä198)
- Hilfe nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/NetworkConnectView.axaml – Verbinden mit dem Haupt-PC
- src/AispaStudio.Desktop/App.axaml.cs – Auswahl der API-Adresse
- docs/entscheidungen/0049-mehrplatz-im-eigenen-netz.md

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/NetworkConnectView.axaml
- src/AispaStudio.Desktop/Views/NetworkView.axaml
- src/AispaStudio.Desktop/App.axaml.cs
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Portfreigabe, VPN oder Tunnel im Programm
- Windows-App für den Mac
- Verwalten im Browser

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 118 "Verwalten von zu Hause: Server-Adresse in der App eintragen" um (diese Datei).
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
- [ ] Given eine erreichbare Server-Adresse mit gültigem Zertifikat When die Inhaberin sie auf dem Startbildschirm einträgt Then prüft die App die Verbindung, merkt die Adresse und startet mit diesem Server
- [ ] Given eine Adresse, die nicht antwortet When sie eingetragen wird Then steht eine Meldung am Feld und nichts wird gemerkt
- [ ] Given `AISPA_API_URL` ist gesetzt When die App startet Then verbindet sie sich mit dieser Adresse, auch wenn ein Server eingetragen ist
- [ ] Manuell: Über einen Handy-Hotspot mit dem Server verbinden, anmelden und eine Verwaltungsmaske öffnen

## Nicht in dieser Slice
–
