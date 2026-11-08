---
art: ablauf
bereich: Anmeldung
seite: anmeldung
stichwoerter: Anmelden im Browser, Code für die Windows-App, Einladung annehmen, Passkey hinzufügen, Spa registrieren, Registrierung, Mein Tag anmelden, Passwort, Einzelplatz, Testkonto, sign in, accept invitation, register, passkey, device code
---
# Seite „Anmelden“ im Browser

Die Seite „Anmelden“ liegt im Browser unter der Adresse des Spas mit `/anmelden/`. Sie ersetzt die frühere Team-Oberfläche im Browser: Verwaltet wird nur noch in der Windows-App, diese Seite erledigt die Anmeldung, die dort nicht möglich ist.

## Wofür die Seite da ist {#wofuer}

- **Code für die Windows-App** holen: [Anmelden](hilfe:anmeldung-anmelden#code).
- Eine **Einladung annehmen** und dabei den Passkey anlegen: [Person einladen](hilfe:zugaenge-person-einladen).
- Einen weiteren **Passkey hinzufügen**, z. B. für ein neues Gerät.
- Ein **neues Spa registrieren** (nur im Betrieb mit Server).
- Im Einzelplatz mit Benutzername und Passwort anmelden, vor allem für „Mein Tag“ am Handy: [„Mein Tag“ aufs Handy holen](hilfe:mein-tag-aufs-handy-holen).

## Code für die Windows-App holen {#code}

1. Die Seite „Anmelden“ im Browser öffnen.
2. Mit **Mit Passkey anmelden** anmelden. Im Einzelplatz stattdessen Benutzername und Passwort eintragen.
3. Auf **Code für die Windows-App** klicken. Die Seite zeigt den Code und die Uhrzeit, bis wann er gilt.
4. Den Code in der Windows-App im Feld „ABCD-EFGH“ eintippen und **Anmelden** wählen.

Der Code gilt nur einmal und nur kurz. Läuft er ab, auf der Seite einen neuen holen.

## Einladung annehmen {#einladung}

1. Den Einladungslink aus der Mail öffnen. Die Seite begrüßt die eingeladene Person mit Namen.
2. **Einladung annehmen und Passkey anlegen** wählen und den Passkey bestätigen (Fingerabdruck, Gesichtserkennung oder Geräte-PIN). Ein Passwort gibt es dabei nicht.
3. Danach ist die Person angemeldet und kann **Code für die Windows-App** holen.

Der Link gilt 7 Tage und nur einmal. Eine abgelaufene oder zurückgezogene Einladung nimmt die Seite nicht an; die Leitung lädt dann neu ein.

## Weiteren Passkey hinzufügen {#passkey}

Angemeldet auf **Passkey hinzufügen** klicken und den Anweisungen des Geräts folgen. Danach steht „Passkey hinzugefügt.“ auf der Seite. Im Einzelplatz gibt es die Passkeys nicht.

## Neues Spa registrieren {#registrieren}

Nur im Betrieb mit Server. Auf **Neues Spa registrieren** klicken, Name des Spas, Land, den eigenen Namen und die E-Mail eintragen. Die Person wird Inhaber:in des Spas und meldet sich danach mit einem Passkey an, ohne Passwort. Das Team lädt sie in der Windows-App unter **Verwaltung** › **Team** ein.

## Gut zu wissen {#hinweise}

- Nach der Einrichtung des Zahlungskontos bei Stripe landet man auf dieser Seite mit dem Hinweis „Zahlungskonto verbunden“. Weiter geht es in der Windows-App unter **Verwaltung** › **Kassa & Geld** › **Zahlungen**: [Zahlungskonto verbinden](hilfe:stammdaten-zahlungskonto-verbinden).
- In der Entwicklung bietet die Seite Testkonten zur Auswahl an. Im Betrieb gibt es sie nicht.
- Kann der Browser keine Passkeys, weist die Seite darauf hin; dann einen aktuellen Browser nehmen.
- Passwort vergessen (Einzelplatz): [Passwort vergessen](hilfe:anmeldung-passwort-vergessen).
