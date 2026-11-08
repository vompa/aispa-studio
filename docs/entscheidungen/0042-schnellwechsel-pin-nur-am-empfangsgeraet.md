# 0042. Schnellwechsel per PIN nur am angemeldeten Empfangsgerät

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom nach ADR 0019)

## Kontext

Am Empfang teilen sich mehrere Personen ein Gerät. Jede Buchung, jeder Beleg und jeder Statuswechsel soll der richtigen Person zugeordnet sein (Kassa, Berichte, Zeiterfassung), aber ein Passkey-Dialog bei jedem Wechsel ist im Betrieb zu langsam. Offener Punkt seit PR #6: „PIN- oder Chipkarten-Schnellwechsel am Empfang“. Die Anmeldung bleibt sonst ausschließlich Passkey ([ADR 0007](0007-anmeldung-passkeys-identity.md)).

## Optionen

| Option | Bewertung |
| --- | --- |
| A: PIN als zweite Anmeldeart überall | vier Ziffern wären von jedem Gerät aus angreifbar; die Passkey-Entscheidung wäre ausgehöhlt |
| B: PIN nur auf Geräten, die die Leitung als Empfangsgerät angemeldet hat, mit Sperre nach Fehlversuchen | gewählt |
| C: Chipkarten oder NFC | braucht Hardware je Standort; später als zweite Variante möglich, dieselbe Geräte-Logik |
| D: Ein gemeinsames Empfangskonto | Buchungen und Belege wären nicht mehr der Person zugeordnet; für Kassa und Zeiterfassung unbrauchbar |

## Entscheidung

- Jemand mit Verwaltungsrecht (Inhaber, Standortleitung) meldet einen Browser als **Empfangsgerät eines Standorts** an. Das Gerät bekommt ein eigenes Cookie (ein Jahr, nur dieser Browser); gespeichert wird der Hash.
- Jede Person legt ihre **eigene PIN** fest (4 bis 8 Ziffern, Inhaber mindestens 6, nicht alle gleich). Gespeichert wird sie wie ein Passwort (PBKDF2 über Identity).
- Am Empfangsgerät wählt man seinen Namen und gibt die PIN ein. Der Server prüft Gerät, PIN und ob der Zugang den Standort des Geräts abdeckt; aktiv, nicht befristet abgelaufen, Spa nicht gesperrt gelten wie bei jeder Anmeldung.
- Nach **fünf Fehlversuchen** ist die PIN 15 Minuten gesperrt (423); eine neue PIN (mit Passkey angemeldet) hebt die Sperre auf.
- Die Anmeldung per PIN ist **nicht dauerhaft**: sie endet mit dem Browser. Passkey und Windows-App-Code bleiben wie sie sind.
- Ohne Empfangsgerät gilt die PIN nirgends; die Leitung kann ein Gerät jederzeit abmelden.

## Begründung

Die PIN ist kurz, weil sie schnell sein muss. Was sie sicher genug macht, ist der Ort: nur ein von der Leitung angemeldetes Gerät im Spa, nur Personen dieses Standorts, und die Sperre nach Fehlversuchen (bei vier Ziffern höchstens rund 480 Versuche am Tag). Wer das Gerät mitnimmt, hat damit noch keine PIN; wer eine PIN kennt, braucht das Gerät. Jede Handlung bleibt der Person zugeordnet.

## Folgen und Stand

- Code: `Access/QuickSwitch.cs` (`StaffPin`, `SharedDevice`, Endpunkte unter `/api/auth`), Oberfläche `auth/QuickSwitch.tsx`, Empfangsgerät unter „Zugänge“.
- Demo-Daten: alle Testkonten haben die PIN `1234`, die Inhaberin `123456` ([docs/testdaten.md](../testdaten.md)).
- Abmelden nach Leerlauf (PR #90): je Gerät 1 bis 120 Minuten, Standard 5. Die Oberfläche meldet am Empfangsgerät jede Anmeldung ab und warnt 30 Sekunden vorher; eine PIN-Anmeldung läuft zusätzlich am Server nach derselben Zeit ohne Anfragen ab (gleitender Ablauf).
- Windows-App (PR #92): derselbe Ablauf; das Gerätekennzeichen liegt mit DPAPI verschlüsselt in `%LOCALAPPDATA%\AispaStudio\empfangsgeraet.bin`, die Anmeldung per PIN wird nicht gemerkt.
- Offen: Chipkarte oder NFC als zweite Variante.

## Quellen

- [ADR 0007](0007-anmeldung-passkeys-identity.md), [ADR 0003](0003-rollenbasierte-oberflaechen.md), README „Rollen und Zugänge“
