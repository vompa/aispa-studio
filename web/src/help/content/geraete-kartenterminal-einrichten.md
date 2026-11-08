---
art: ablauf
bereich: Geräte
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: geraete
stichwoerter: Kartenterminal einrichten, Terminal verbinden, Bankomat anschließen, ZVT, OPI, Worldline, Hobex, Zahlungsterminal, Kartengerät, card terminal setup, payment terminal, connect terminal
---
# Kartenterminal einrichten

Ein Kartenterminal im Netz (ZVT oder OPI) steuert die Kassa selbst an: sie schickt den Betrag, das Ergebnis kommt automatisch zurück. Das Terminal wird einmal für das Spa angelegt und an genau einem PC gewählt, dem PC, neben dem es steht.

## Terminal anlegen (Leitung) {#anlegen}

Nur Inhaber und Standortleitung.

1. In der Navigation **Geräte** öffnen.
2. Unter „Kartenterminal“ bei „Neues Terminal im Netz“ den Namen eintragen, z. B. „Empfang“.
3. Den Standort und das Protokoll wählen: ZVT (Deutschland, Österreich; meist Port 20007) oder OPI (z. B. Worldline, Schweiz; meist Port 20002). Welches, sagt der Zahlungsdienstleister.
4. Die Adresse eintragen, z. B. `192.168.1.50:20007`.
5. Auf **Terminal anlegen** klicken. Das neue Terminal ist oben gleich gewählt.
6. Auf **Speichern** klicken. Erst dann steuert dieser PC das Terminal an.

## Terminal an diesem PC wählen {#waehlen}

Ist das Terminal schon angelegt:

1. An dem PC, neben dem das Terminal steht, **Geräte** öffnen.
2. Unter „Kartenterminal“ das Terminal in der Auswahl wählen.
3. Auf **Speichern** klicken. Unter der Auswahl steht dann, was das Terminal meldet, z. B. „Wartet auf Zahlungen.“

![Geräte mit gewähltem Kartenterminal](images/app/geraete.png)

## Gut zu wissen {#hinweise}

- Das Terminal nur an **einem** PC wählen. Kassen an anderen PCs desselben Standorts zahlen trotzdem damit; ihre Aufträge laufen über den Server an diesen PC. Die App muss an diesem PC dafür offen sein.
- An allen anderen PCs bleibt „Keines (Karte am eigenständigen Gerät oder anderer PC)“ gewählt.
- Ein gelber Hinweis heißt: die Verbindung brach mitten in einer Zahlung ab. Am Terminal oder auf dem Terminal-Beleg nachsehen, ob die Karte belastet wurde.
- **Gewähltes Terminal entfernen** (nur Leitung) legt das Terminal still; bisherige Zahlungen bleiben.
- Bezahlen am Terminal: [Bezahlen](hilfe:kassa-bezahlen#kartenterminal). Alle Felder: [Kartenterminal](hilfe:geraete#kartenterminal).
