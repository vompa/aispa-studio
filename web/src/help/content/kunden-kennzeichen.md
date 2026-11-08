---
art: ablauf
bereich: Kunden
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager, therapeut
seite: kunden
stichwoerter: Kennzeichen, Flag, schwieriger Gast, anspruchsvoller Gast, mehr Zeit, Puffer, Nicht bei mir, Ausschluss, sperren, Sperre, nicht mehr buchen, blockieren, Leitung informieren, Warnung, block client, client alert
---
# Kennzeichen setzen und aufheben

Statt eines Freitexts wie „schwierige Kundin“ gibt es feste, sachliche Kennzeichen. Jedes beschreibt einen Bedarf und hat eine Wirkung im Kalender. Die Kennzeichen stehen auf der Kundenkarte unter „Kennzeichen“.

![Kennzeichen auf der Kundenkarte](images/app/kunden-kennzeichen.png)

## Welche es gibt {#arten}

| Kennzeichen | Wer setzt es | Wirkung |
|---|---|---|
| Braucht mehr Zeit | wer Termine buchen darf | Jeder neue Termin bekommt 10 Minuten Puffer nach der Behandlung. Im Kalender steht **+10**; der Puffer steht getrennt von der Nachbereitung im Streifen („Nachb. 10 · Puffer 10 Min.“), in der Hover-Karte und im Terminfenster. |
| Leitung informieren | Standortleitung, Inhaber | Im Kalender steht **L** am Termin; wer den Gast empfängt, sagt der Leitung Bescheid. |
| Nicht bei mir | die Therapeut:in für sich selbst, die Leitung | Kalender, KI-Terminannahme und Online-Buchung schlagen diese Person für den Gast nicht mehr vor. Wird sie ausdrücklich gewählt, lehnt der Kalender ab. Der Gast erfährt davon nichts. |
| Nicht mehr buchen | Standortleitung, Inhaber, mit Grund | Online-Buchung und KI-Terminannahme buchen nicht und schlagen nichts vor; die Anfrage geht an die Rezeption. Die Rezeption kann trotzdem buchen. Im Kalender steht **STOP**. |

## Setzen {#setzen}

1. Die Kundenkarte öffnen.
2. Bei „Kennzeichen“ auf **+** klicken, oder ohne Kennzeichen unter „Noch nicht erfasst“ auf **+ Kennzeichen**. Leitung und Inhaber öffnen „Leitung informieren“ und „Nicht mehr buchen“ auch über das Menü **…**. Die Auswahl zeigt nur, was die eigene Rolle setzen darf.
3. Kennzeichen wählen, bei Bedarf einen Grund eintragen (bei „Nicht mehr buchen“ Pflicht) und **Setzen** klicken.

Neue Termine richten sich sofort danach. Schon gebuchte Termine bleiben, wie sie sind.

## Aufheben {#aufheben}

Auf das Kreuz am Chip des Kennzeichens klicken (Tooltip „Aufheben“). Wirkung und Grund zeigt der Tooltip des Chips. Aufheben darf, wer es auch setzen darf; „Nicht bei mir“ nur die Person selbst oder die Leitung. Aufgehobene Kennzeichen bleiben mit Zeitpunkt und Person gespeichert, damit nachvollziehbar ist, wer was geändert hat.

## Gut zu wissen {#hinweise}

- Den Grund bei „Nicht bei mir“ sehen nur die Person selbst und die Leitung, die anderen sehen nur „Nicht bei Lena Huber“.
- Gründe sachlich formulieren: Der Gast hat ein Recht auf Auskunft über alles, was über ihn gespeichert ist (Art. 15 DSGVO). Gesundheitsangaben gehören nicht hierher; erkennt die Eingabe solche Wörter, erscheint ein Hinweis.
- Die KI wendet nur die Wirkung an (Puffer, andere Person) und reiht niemanden heimlich nach. Über „Nicht mehr buchen“ entscheidet immer ein Mensch.
- Weitere Kennzeichen wie „Kommt oft zu spät“ oder „Beschwerde offen“ und automatische Vorschläge folgen.
