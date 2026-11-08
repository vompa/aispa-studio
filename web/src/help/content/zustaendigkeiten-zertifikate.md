---
art: einstellung
bereich: Team
oberflaeche: verwaltung, windows-app, handy
rollen: inhaber, manager
seite: verwaltung
stichwoerter: Zuständigkeit, Ersthelfer, Ersthelferin, Erste Hilfe, Sicherheitsvertrauensperson, Brandschutz, Hygiene, Lager, Zertifikat, Kurs, Nachweis, ablaufen, erneuern, Scan, Erinnerung, Vertretung
---
# Zuständigkeiten und Zertifikate

Wer ist Ersthelferin, wer kümmert sich um das Lager, wessen Erste-Hilfe-Kurs läuft bald ab? In der Windows-App liegt das im Bereich **Verwaltung** › **Team**: auf der Karte einer Person im Reiter **Zuständigkeiten** und für alle zusammen in der Ansicht **Zuständigkeiten**. Nur Inhaber und Manager sehen und ändern beides.

> Hinweis: Das Programm prüft keine Rechtsvorschrift. Wie viele Ersthelferinnen, Brandschutzbeauftragte oder Sicherheitsvertrauenspersonen ein Spa braucht, legt die Leitung selbst fest. Die App erinnert und warnt nur nach den Angaben in der Liste.

Eine Zuständigkeit gibt keine Rechte. Wer das Lager öffnen oder die Kassa abschließen darf, entscheidet weiter die Rolle im Reiter **Zugang und Rolle** und die Vollmachten (siehe [Regelungen und Vollmachten](hilfe:regelungen-vollmachten)).

![Reiter Zuständigkeiten auf der Karte von Lena Huber mit abgelaufenem Erste-Hilfe-Kurs](images/app/verwaltung-team-person-zustaendigkeiten.png)

## Auf der Karte einer Person {#person}

Der Reiter **Zuständigkeiten** zeigt zwei Listen. Bei einer ausgeschiedenen Person lässt er sich nur lesen.

### Zuständigkeit {#zustaendigkeit}

**Zuständigkeit hinzufügen** öffnet das Formular:

- **Zuständigkeit**: ein Eintrag aus der Liste des Spas, z. B. Ersthelfer, Lager oder Hygiene.
- **Gültig von** ist heute vorbelegt. **Gültig bis** darf leer bleiben, der Tag selbst zählt noch.
- **Vertretung**: eine andere Person im Team. Sie bekommt die Lageraufgaben, wenn die Person laut Dienstplan abwesend ist.

Eine Zuständigkeit gilt für alle Standorte.

### Zertifikat {#zertifikat}

**Zertifikat hinzufügen** öffnet das Formular:

- **Bezeichnung**: Die Liste schlägt die verlangten Zertifikate vor, eine andere Bezeichnung geht auch. Ein Zertifikat erfüllt eine Zuständigkeit, wenn die Bezeichnung dem verlangten Zertifikat gleicht; Groß- und Kleinschreibung zählen nicht.
- **Ausgestellt am** und **Ausstellende Stelle** sind freiwillig.
- **Gültig bis**: Leer heißt unbefristet; dann gibt es keine Erinnerung.
- **Erinnerungsfrist in Tagen**: 60 Tage sind vorbelegt, erlaubt sind 1 bis 365.

### Erneuern {#erneuern}

**Erneuern** an einem Zertifikat öffnet ein neues Formular mit Bezeichnung, ausstellender Stelle und Erinnerungsfrist des alten, ausgestellt heute. Das alte Zertifikat bleibt als Verlauf stehen. Sobald das neue ein späteres **Gültig bis** hat, endet die Erinnerung an das alte.

### Scan {#scan}

**Scan hinzufügen** lädt ein PDF, JPG oder PNG bis 5 MB zum Zertifikat. Der Scan liegt verschlüsselt in der Datenbank; ist der Schlüssel für vertrauliche Daten nicht eingerichtet, lässt er sich nicht speichern, das Zertifikat selbst schon. **Scan speichern unter …** legt ihn als Datei ab, **Scan entfernen** löscht ihn.

### Warnung und Kennzeichen {#kennzeichen}

Ein Zertifikat in der Erinnerungsfrist zeigt „Läuft am … ab“, ein abgelaufenes „Abgelaufen am …“, jeweils mit Warnsymbol. Oben auf der Karte und in der Teamliste stehen die heute gültigen Zuständigkeiten als Kennzeichen. Fehlt das verlangte Zertifikat oder ist es abgelaufen, steht dort zuerst „Warnung: Ersthelfer – Erste-Hilfe-Kurs abgelaufen am 3.10.“.

## Wer ist wofür zuständig? {#uebersicht}

Die Ansicht **Zuständigkeiten** zeigt je Eintrag der Liste, wer zuständig ist, mit Vertretung und Gültigkeit. Steht niemand dabei, heißt es „Niemand eingetragen“. Abgelaufene Zuständigkeiten stehen ausgegraut. Ändern lassen sie sich auf der Karte der Person.

Darunter nennt **Was läuft bald ab?** alle Zertifikate aktiver Personen in der Erinnerungsfrist und die abgelaufenen, die noch nicht erneuert sind, nach Datum.

![Ansicht Zuständigkeiten mit Ersthelfer, Hygiene und einem abgelaufenen Kurs](images/app/verwaltung-team-zustaendigkeiten.png)

## Liste der Zuständigkeiten {#liste}

Unter **Liste der Zuständigkeiten** pflegt die Leitung, welche Zuständigkeiten es im Spa gibt. Sieben Vorgaben sind von Anfang an da: Ersthelfer, Sicherheitsvertrauensperson, Lager, Brandschutz, Hygiene, Kassa-Verantwortung und Ausbildung. Vorgaben lassen sich umbenennen und ausblenden, aber nicht löschen. **Eintrag hinzufügen** legt einen eigenen Eintrag an; **Speichern** oder **Strg+S** speichert die Liste.

### Verlangtes Zertifikat {#verlangtes-zertifikat}

Das **Verlangte Zertifikat** ist die Bezeichnung des Nachweises, den die Zuständigkeit braucht, z. B. „Erste-Hilfe-Kurs“ beim Ersthelfer. Fehlt er einer zuständigen Person oder ist er abgelaufen, warnen Karte und Teamliste.

### Mindestzahl im Dienst {#mindestzahl}

Die **Mindestzahl im Dienst** gibt es nur beim Ersthelfer; vorbelegt ist 1, 0 schaltet die Warnung ab. Die Warnung gilt erst, sobald mindestens eine Person als Ersthelfer eingetragen ist. Beim Speichern eines Tages im Dienstplan zählt die App am Standort die Personen im Dienst, die an diesem Tag als Ersthelfer zuständig sind und einen gültigen Kurs haben. Sind es weniger, warnt die Maske, etwa „Keine Ersthelferin im Dienst am Samstag, 7.11.“, und **Trotzdem speichern** speichert den Tag. Trifft zugleich eine Sonderregelung zu, stehen beide Texte in derselben Warnung. Siehe [Dienst für einen Tag planen](hilfe:dienstplan-tag-planen#hinweise).

### Ausblenden {#ausblenden}

**Ausblenden** nimmt einen Eintrag aus der Auswahl im Formular und aus der Übersicht. Bestehende Zuständigkeiten bleiben gespeichert.

## Erinnerung {#erinnerung}

Ab „Gültig bis“ minus Erinnerungsfrist erinnert der Kalender mit einer Meldung, etwa „Erste-Hilfe-Kurs läuft am 31.12. ab“. Nach dem Ablauf heißt sie „ist am … abgelaufen“ und bleibt, bis ein neueres Zertifikat gleicher Bezeichnung da ist oder die Person ausgeschieden ist. Die Person selbst sieht ihre eigenen Erinnerungen im Kalender und in der Handy-App auf **Heute** unter **Erinnerungen**. Inhaber und Manager sehen die aller aktiven Personen mit Namen; **Zur Personenkarte** führt zum Reiter **Zuständigkeiten**. Eine Mail oder Push-Nachricht gibt es nicht.

## Lager {#lager}

Bestellvorschläge und bald ablaufende Chargen eines Standorts gehen als je eine Meldung im Kalender an die Person, die heute als Lager zuständig ist, etwa „2 Materialien nachbestellen“. Ist sie laut Dienstplan heute abwesend, geht die Meldung an ihre Vertretung. Hat niemand die Zuständigkeit Lager, bekommt sie wie bisher die Leitung des Standorts. Den Knopf **Zum Lager** sieht nur, wer das Lager öffnen darf. Die Reiter **Aufgaben** und **Bestellen** im Lager bleiben für alle mit dem Recht dazu gleich. Siehe [Bestellen und Lieferung buchen](hilfe:lager-bestellen) und [Bald ablaufende Chargen abarbeiten](hilfe:lager-ablauf-aufgaben).

## Verlauf {#verlauf}

Der Reiter **Verlauf** nennt unter **Zuständigkeiten und Zertifikate**, wer wann eine Zuständigkeit, ein Zertifikat oder einen Scan angelegt, geändert oder gelöscht hat, aber nie den Inhalt.
