# Kassen im Standortverbund gemeinsam aktualisieren

Der neue Anbieterrahmen und die Übungskassen benötigen denselben Software- und Datenbankstand auf allen gekoppelten Stellen. Das gilt auch für einen Haupt-PC, der gerade ausgeschaltet oder ohne Verbindung ist. Weitere Windows-Apps im Netz müssen den neuen Stand verwenden, damit sie Übungsbelege sichtbar kennzeichnen.

Ein neuer deutscher Kassen-Code darf bis zu 50 Zeichen haben. Alte Datenbanken nehmen in den beiden Test-TSE-Tabellen höchstens 30 Zeichen an. Eine längere Kennung bricht dort den Abgleich mit PostgreSQL-Fehler `22001` ab; dadurch können auch nachfolgende Änderungen warten. Die additive Form der API-Verträge beseitigt diese Datenbankgrenze nicht. Der Abgleich prüft den Versionsstand der Gegenstelle nicht automatisch.

## Vor dem Upgrade

1. Alle gekoppelten Haupt-PCs, Server, Datenbanken und Windows-Apps erfassen. Je Stelle Namen, Softwarestand, Datenbank und verantwortliche Person notieren. Abgeschaltete PCs und wiederherstellbare Installationen einbeziehen.
2. Aktuelle Sicherungen aller Datenbanken und der zugehörigen geschützten Schlüssel anlegen und ihre Wiederherstellbarkeit prüfen.
3. Auf allen Stellen Schreibbetrieb und Abgleich anhalten. Auch Hintergrunddienste, geplante Starts und selbsttätige Wiederanläufe unterbinden. Den Stillstand je Stelle bestätigen. Ein PC darf beim späteren Einschalten nicht mit dem Altstand wieder abgleichen.

**Anhalten, sobald eine Stelle unbekannt, ihr Stand ungeklärt oder ihr Stillstand unbestätigt ist.** Die neuen Funktionen erst nutzen, wenn jede gekoppelte Stelle bestätigt ist. Ein gewöhnliches automatisches App-Update ersetzt diese gemeinsame Umstellung nicht.

## Jede Stelle aktualisieren

1. Den vorgesehenen gemeinsamen Softwarestand installieren und dessen Datenbankmigrationen im kontrollierten Wartungsbetrieb ausführen. Schreibbetrieb und Abgleich bleiben dabei ausgesetzt.
2. Softwarestand und angewandte Migrationen protokollieren. Für den Anbieterrahmen muss `20261127223411_FiscalProviders` angewandt sein; weitere Migrationen des installierten Standes ebenfalls bestätigen.
3. In jeder beteiligten Datenbank diese lesende Abfrage ausführen:

   ```sql
   SELECT table_name, column_name, character_maximum_length
   FROM information_schema.columns
   WHERE table_schema = 'aispa'
     AND table_name IN ('TestTseClientState', 'TestTseLog')
     AND column_name = 'ClientId'
   ORDER BY table_name;
   ```

   Erwartet sind genau zwei Zeilen, beide mit Länge `50`. Fehlende Tabellen, eine andere Länge oder eine nicht erreichbare Datenbank halten die Umstellung an.
4. Offline-PCs vor ihrem ersten erneuten Abgleich auf denselben Stand bringen. Ebenso nach einer Wiederherstellung einer alten Sicherung zuerst den Wartungsablauf anwenden.
5. Erst nach vollständiger Bestätigung aller Stellen den Abgleich und danach den Schreibbetrieb wieder aufnehmen. Fehlerstand des Abgleichs je Stelle prüfen.

## Probe mit isolierten Testdaten

Die Umstellung vor einer Freigabe im isolierten Verbund erproben. Auf jeder aktualisierten Seite über die Anwendung eine deutsche Übungskassa mit 50-stelliger Kennung anlegen, abgleichen und die vollständige Kennung in Zustand und Protokoll der Gegenseite prüfen. Ein weiterer Abgleich muss ohne Änderungen und ohne Fehler bleiben. Anschließend einen Übungsbeleg ansehen; auch die weitere Windows-App muss den festen Übungshinweis zeigen.

Der automatisierte Integrationstest prüft den regulären Abgleich zwischen zwei bereits aktualisierten Datenbanken in beiden Richtungen. Er beweist keinen unterbrechungsfreien Übergang mit alten Gegenstellen. Der betriebliche Stillstand und die tatsächliche Installation bleiben vor Ort zu bestätigen. Diese Anleitung ist keine Freigabe für einen Produktivrollout.

Siehe [ADR 0105](../entscheidungen/0105-kassa-einrichten-anbieterrahmen-und-uebungskassa.md) und [Betriebsarten: Abgleich](operating-modes.md).
