# 0056. Handbuch mit Abläufen und Bildern als Grundlage der KI-Hilfe

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber („damit das funktioniert muss die gesamte App, wie man eine Rechnung erstellt, einen Termin anlegt, verschiebt, einfach alle Themen so sauber beschrieben sein, damit die Hilfe dann auch auf das Thema mit Bildern usw. möglich ist“, ausdrücklich auch für Admins), Ausgestaltung Claude

## Kontext

Die KI-Seitenleiste (rechts, ausblendbar, KI-Knopf rechts unten, in App und Web) soll zur aktuellen Seite erklären und Fragen wie „Wie erstelle ich eine Rechnung?“ beantworten. Bisher gab es nur Hilfetexte zu Einstellungen (ADR 0055). Die KI darf nichts erfinden (KI-Texte nur aus geprüften Daten), die Windows-App läuft auch ohne Internet, und Bilder veralten, sobald sich die Oberfläche ändert.

## Optionen

| Option | Bewertung |
| --- | --- |
| Die KI erklärt frei aus ihrem Wissen über die App | erfindet Knöpfe und Abläufe, nicht prüfbar, ohne Bilder |
| Handbuch außerhalb des Repositorys (Wiki, PDF), Bilder von Hand | veraltet unbemerkt, offline nicht da, KI kommt nicht heran |
| Handbuch als Hilfethemen im Repository mit Kopfangaben, erzeugten Bildern und Suche; die KI findet nur die passende Stelle | gewählt |

## Entscheidung

- Drei Arten von Hilfethemen in `web/src/help/content/` ([Format](../../web/src/help/README.md)): **Einstellungen** (wie bisher), **Abläufe** (eine Aufgabe Schritt für Schritt) und **Seiten** (Erklärung zur aktuellen Seite). Kopfangaben zwischen `---` nennen Art, Bereich, Oberfläche (`windows-app`, `verwaltung`, `handy`), Rollen, Seite und Suchwörter.
- Bilder liegen unter `web/src/help/images/` und werden erzeugt: die der Windows-App von den Bildtests (`scripts/help-images.sh`), die der Team-Oberfläche mit den Testkonten an einer Umgebung mit Testdaten (`web/scripts/help-images.mjs`), danach auf 256 Farben verkleinert. Tests schlagen fehl, wenn ein Bild, ein Thema oder ein Anker fehlt, auf den ein Text verweist.
- Leser und Suche gibt es einmal in C# (`AispaStudio.Help`, von API und Windows-App genutzt, Texte eingebaut) und einmal in TypeScript (`web/src/help`), mit derselben Rangfolge: Titel vor Suchwörtern vor Überschriften vor Text, Abläufe vor Einstellungen, passende Rolle, Oberfläche und Seite zuerst. Ohne KI und ohne Server.
- Der Assistent bekommt die Absicht `Help`. Die KI (oder ohne Schlüssel die Regeln) erkennt „Wie mache ich …?“ und nennt die Aufgabe in den Worten des Handbuchs; der Server sucht und liefert bis zu drei Verweise (`AssistantAnswer.Help`: Thema, Abschnitt, Titel, erster Satz). Die Oberfläche zeigt Schritte und Bilder aus ihrer eingebauten Hilfe. Die KI schreibt keine Anleitung.
- `AssistantQuestion` trägt `Page` und `Surface`. Allgemeine Fragen („Was kann ich hier machen?“) beantwortet das Seitenthema.
- Zum Einbetten in die Seitenleiste: `HelpSectionView` (Windows-App) und `<HelpSection>` (Web); `HelpContent.ForPage` bzw. das Seitenthema `seite-<seite>` liefern die Erklärung zur aktuellen Seite.
- Regel ab jetzt: Wer einen Ablauf ändert oder neu baut, ändert im selben Pull Request den Ablauf im Handbuch und erzeugt betroffene Bilder neu.

## Begründung

Eine Quelle für Hilfe-Seite, Seitenleiste und KI, offline lesbar und durch Tests an die Oberfläche gebunden. Die KI wird nur zum Finden gebraucht; was angezeigt wird, steht geprüft im Repository. Erzeugte Bilder bleiben mit einem Skriptlauf aktuell.

## Folgen und Stand

- Code: `src/AispaStudio.Help/`, `src/AispaStudio.Desktop/Services/HelpContent.cs`, `Controls/HelpSectionView.cs`, `Views/HelpView.axaml`, `web/src/help/` (`search.ts`, `HelpDrawer.tsx` mit `HelpSection`), `src/AispaStudio.Api/Assistant/`.
- **Nachtrag (T-010, 23.10.2026, [ADR 0079](0079-eine-hilfe-fuer-windows-app-und-web.md)):** Bilder liegen getrennt unter `web/src/help/images/app/` und `images/web/`; die Windows-App bettet sie nicht mehr ein, sondern zeigt die Hilfe-Seite `/help/` im WebView2 (aus dem Ordner neben der App über einen virtuellen Host, offline). `HelpSectionView` ist durch den Kurzhinweis `HelpTeaser` ersetzt (Titel, erster Satz, „In der Hilfe öffnen“); die Schritte und Bilder zeigt die Hilfe selbst. Die Texte bleiben in `AispaStudio.Help` eingebaut, weil der Assistent auf dem Server und die Namen der Fragezeichen sie brauchen.
- Die Bilder der Team-Oberfläche hängen von den Testdaten und vom Tag ab, an dem sie erzeugt werden.
- Offen: Hilfe in weiteren Sprachen; Bilder im dunklen Farbschema; Markierungen (Pfeile, Nummern) in den Bildern; Hilfe im Kundenbereich.
