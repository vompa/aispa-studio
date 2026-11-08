# 0051. Passwort vergessen ohne Leitung: Wiederherstellungsschlüssel am Haupt-PC

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber (Wahl „alle“ offenen Einzelplatz-Punkte), Ausgestaltung Claude (autonom nach ADR 0019)

## Kontext

Im Einzelplatz-Betrieb gibt es Benutzername und Passwort ([ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), Abschnitt 3). Ein vergessenes Passwort setzt die Leitung unter „Zugänge“ zurück. Vergisst die Inhaberin ihr eigenes Passwort und gibt es kein zweites Konto mit diesem Recht, kommt niemand mehr an Kassa, Berichte und Exporte. Ohne Server gibt es keinen Mailversand, über den ein Link zum Zurücksetzen gehen könnte.

Der Zugang zum Windows-Konto des PCs beweist nichts: Am Empfang meldet sich oft das ganze Team mit demselben Windows-Konto an.

## Optionen

| Option | Bewertung |
| --- | --- |
| Rücksetzen per Mail oder SMS | braucht Versanddienst und Internet; widerspricht dem Einzelplatz ohne Webmodule |
| Wer am Haupt-PC sitzt, darf zurücksetzen | jeder am Empfang könnte das Konto der Inhaberin übernehmen |
| Eigener Notfall-Code, der bei der Einrichtung entsteht | noch ein Zettel, den man aufheben muss |
| Der Wiederherstellungsschlüssel der Sicherung (ADR 0041, Schritt 5) | gewählt: liegt ohnehin bei der Leitung getrennt vom PC, 119 Bit, und wer ihn hat, kann schon heute alle Daten samt Gesundheitsdaten wiederherstellen |

## Entscheidung

- „Passwort vergessen?“ auf der Anmeldung (Windows-App und Web) fragt Benutzername, Wiederherstellungsschlüssel und das neue Passwort ab. `POST /api/auth/standalone/recover` setzt das Passwort, hebt eine Sperre nach Fehlversuchen auf, beendet alte Anmeldungen und meldet an. Ein Pflichtwechsel folgt nicht, das Passwort ist selbst gewählt.
- Geprüft wird ohne gespeicherten Code: Die Windows-App legt den mit dem Code verschlüsselten Schlüssel für Gesundheitsdaten als `wiederherstellung.json` in den Datenordner (`Hosting:RecoveryFile`). Der Code passt, wenn er die Datei öffnet und darin genau der Schlüssel steckt, mit dem die API arbeitet. Eine ausgetauschte Datei mit eigenem Code hilft deshalb nicht.
- Nur am Haupt-PC: Anfragen über das Netz (Mehrplatz, ADR 0049) bekommen 403, damit niemand im WLAN Schlüssel durchprobiert.
- Ohne Wiederherstellungsschlüssel (Sicherung nie eingerichtet) antwortet die API mit 409 und verweist auf ein Konto mit dem Recht „Zugänge verwalten“.

## Begründung

Der Wiederherstellungsschlüssel ist schon heute der stärkste Nachweis im Einzelplatz: Mit ihm und einer Sicherung bekommt man alles zurück. Er öffnet also keine Tür, die nicht ohnehin offen wäre. Die Datei ist ohne den Code nutzlos und liegt ohnehin neben jeder Sicherung. PBKDF2 mit 600 000 Runden und 119 Bit machen Raten aussichtslos.

## Folgen und Stand

- Umgesetzt mit PR #123.
- Wer den Wiederherstellungsschlüssel hat, kann jedes Konto übernehmen. Er gehört zur Leitung und nicht an den Empfang; die Seite „Sicherung“ sagt das beim Anlegen.
- Ein neuer Wiederherstellungsschlüssel gilt sofort, der alte nicht mehr.
- Offen: Protokolleintrag „Passwort mit Wiederherstellungsschlüssel gesetzt“ für die Leitung, sobald es ein Ereignisprotokoll für Zugänge gibt.

## Quellen

- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), Abschnitte 3 und 5
- [ADR 0049](0049-mehrplatz-im-eigenen-netz.md)
- PR #123
