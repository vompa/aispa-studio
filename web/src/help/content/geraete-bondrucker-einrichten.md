---
art: ablauf
bereich: Geräte
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: geraete
stichwoerter: Bondrucker einrichten, Drucker anschließen, Belegdrucker, Epson, Star, Bixolon, ESC/POS, Probedruck, druckt nicht, Drucker IP, automatisch drucken, receipt printer setup, test print
---
# Bondrucker einrichten

Ein Bondrucker im Netz (ESC/POS, z. B. Epson, Star, Bixolon) wird an jedem PC einzeln eingetragen. Einen Windows-Druckertreiber braucht es nicht.

1. In der Navigation **Geräte** öffnen.
2. Unter „Bondrucker“ bei „Adresse“ die IP-Adresse des Druckers eintragen, z. B. `192.168.1.60` (Port 9100 gilt ohne Angabe).
3. Bei „Zeichen pro Zeile“ die Breite wählen: 42 oder 48 Zeichen für 80-mm-Papier, 32 Zeichen für 58-mm-Papier.
4. Den Haken „Nach dem Kassieren und Stornieren automatisch drucken“ setzen oder entfernen.
5. Auf **Probedruck** klicken. Daneben steht „Probedruck geschickt.“ oder, warum es nicht ging.
6. Auf **Speichern** klicken. Es erscheint „Gespeichert für diesen PC.“

![Geräte mit eingetragenem Bondrucker](images/app/geraete.png)

## Wenn nichts gedruckt wird {#probleme}

- „Bondrucker unter … nicht erreichbar.“: Ist der Drucker eingeschaltet und im selben Netz? Stimmt die Adresse (steht meist auf dem Selbsttest-Ausdruck des Druckers)?
- Der Beleg bricht mitten in der Zeile um: 42 statt 48 Zeichen wählen.
- In der Kassa steht „Kein Bondrucker eingerichtet (unter „Geräte“).“: Adresse eintragen und **Speichern** nicht vergessen.

## Gut zu wissen {#hinweise}

- Die Einstellung gilt für diesen PC und jede Person, die sich hier anmeldet.
- Einzelheiten zu allen Feldern: [Bondrucker](hilfe:geraete#bondrucker).
- Belege nachträglich drucken: [Belege ansehen und drucken](hilfe:kassa-beleg-drucken).
