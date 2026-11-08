# 0079. Eine Hilfe für Windows-App und Web

- **Status:** angenommen
- **Datum:** 23.10.2026
- **Entschieden von:** Claude, autonom nach ADR 0019 (Auftrag T-010)

## Kontext

Die Hilfe aus ADR 0055 und 0056 hatte zwei Darstellungen derselben Markdown-Texte: die Schublade der Team-Oberfläche (`web/src/help`, mit Suche, Anker und Bildern) und eine eigene Umsetzung in Avalonia (`HelpView`, `HelpSectionView`, `HelpWindow`, `HelpContent`, dazu Zeilenvorlagen und Stile). Beide liefen auseinander: Die Web-Hilfe hatte nach dem Umbau der Suche Rangfolge nach Rolle und Oberfläche, die App-Hilfe zeichnete Listen und Absätze auf ihre Weise. Jede Änderung an Bildern, Ankern oder Darstellung war doppelt zu machen. Die 99 Handbuchbilder lagen außerdem in einem Ordner, ohne Trennung nach Windows-App und Web-Oberfläche. Die Windows-App bettete alle Bilder zusätzlich als Ressourcen ein, obwohl dieselben Dateien schon im Web-Build stecken.

## Optionen

| Option | Bewertung |
| --- | --- |
| Beide Darstellungen behalten und angleichen | doppelte Arbeit bleibt, die Abweichung kommt wieder |
| Hilfe nur als Schublade der Verwaltung (`/?eingebettet=1`) in den WebView | die Hilfe wäre nur mit der Verwaltung und den Rechten dort erreichbar, auch vor der Anmeldung nicht |
| Hilfe als eigenes Web-Modul `/help/` mit eigenem Einstieg; die App zeigt es im WebView2, die Schublade der Verwaltung bleibt und nutzt dieselben Bausteine | gewählt |

## Entscheidung

- **Ein Web-Modul.** `web/help/index.html` mit `web/src/help/main.tsx` ist ein eigener Einstieg im Mehrseiten-Build (`/help/`, neben `/buchen/`, `/mein-tag/`, `/betreiber/`). Links stehen Suche und Themenliste, rechts das Thema mit Ankern und Bildern. Das Ziel steht in der Adresse: `?thema=<kennung>&abschnitt=<anker>`; die App ergänzt `oberflaeche=windows-app` und `rolle=<rolle>`, damit die Suche passende Themen vorreiht. Der Stand folgt dem Verlauf (Zurück und Vor gehen durch die besuchten Themen).
- **Ein Baustein, keine zweite Umsetzung.** Markdown-Leser, Themenliste, Suche und Zeichnen der Blöcke (`HelpBlocks`, neu `HelpIndex`) sind dieselben wie in der Schublade. Der Einstieg importiert nichts aus der Team-Oberfläche außer diesen Bausteinen und `index.css`. Eigenes Budget in `npm run check:bundle`: **100 kB gzip** für alles, was die Seite beim ersten Aufruf lädt (gemessen 88 kB: React samt DOM etwa 68 kB, `index.css` 8 kB, Hilfe-Code 12 kB). Themen und Bilder laden erst bei Bedarf.
- **Windows-App.** Der Hilfe-Knopf (`HelpButton`) öffnet in der App und vor der Anmeldung dasselbe Thema an derselben Stelle: Die Seite „Hilfe“ der App hostet den eingebetteten Browser (`NativeWebView`, unter Windows WebView2 wie die Verwaltung nach ADR 0053) mit `help/index.html?thema=…&abschnitt=…`. Liegt der Ordner `web` mit der gebauten Hilfe neben der App (Windows-Paket, Einzelplatz und Mehrplatz), ordnet der Browser ihn dem virtuellen Host `help.aispa.local` zu (WebView2 `SetVirtualHostNameToFolderMapping`, Zugriff „DenyCors“) und lädt `https://help.aispa.local/help/index.html?…`; die Hilfe geht dann ohne Server und ohne Internet (ADR 0056), ohne `file://` und ohne gelockerte Browser-Schalter. Sonst (Entwicklung) kommt sie vom Server unter `/help/`. Der Browser läuft InPrivate in einem eigenen Datenordner (`hilfe`), weil die Verwaltung im Mehrplatz andere Browser-Argumente setzt und WebView2 je Datenordner nur eine Umgebung zulässt. Links auf fremde Seiten öffnen sich im Standardbrowser. Die Zuordnung steckt in `EmbeddedBrowser.TryMapFolder`: Avalonia 12.1.1 gibt sie nicht öffentlich frei, nur den Zeiger auf den CoreWebView2; die Methode ruft über COM (`StrategyBasedComWrappers`) `ICoreWebView2_3` aus der Avalonia-Assembly auf. Das ist am echten WebView2 geprüft (Seite, Thema und Abschnitt laden); ein Test sorgt dafür, dass ein Paketupdate den Typ nicht unbemerkt entfernt.
- **Absolute Adressen im Build bleiben.** Weil der Ordner `web` (nicht `web\help`) dem Host zugeordnet wird, lösen sich `/assets/…` und `/help/index.html` wie auf dem Server auf; der Build der Hilfe-Seite ist derselbe wie der der anderen Seiten (eigener Einstieg im Mehrseiten-Build, keine Sonderkonfiguration). Die Dateien liegen nur einmal im Paket (`web/`), die Windows-App bringt keine eigene Kopie der Bilder mehr mit.
- **Bilder nach Oberfläche getrennt.** `web/src/help/images/app/` (Windows-App, von den Bildtests erzeugt: `scripts/help-images.sh`) und `web/src/help/images/web/` (Web-Oberfläche, `web/scripts/help-images.mjs`). In den Hilfetexten steht `![…](images/app/<name>.png)` bzw. `images/web/<name>.png`; Leser in TypeScript und C# und die Tests verlangen den Ordner.
- **Entfällt in der Windows-App:** `HelpView`-Darstellung der Themen (Avalonia), `HelpSectionView`, `HelpText`, die Zeilenvorlagen und Stile der Hilfe, die eingebetteten Bild-Ressourcen. Es bleiben: der `HelpButton` (öffnet das Ziel), `HelpContent` mit Themen, Suche und Rollenschlüssel für den Assistenten und die Namen der Fragezeichen, das Fenster `HelpWindow` für die Zeit vor der Anmeldung und die eingebetteten Texte (`HelpText.*.md` in `AispaStudio.Help`), weil der Server (Assistent, `RoleAssistant`) und die App sie zum Suchen brauchen. Wo die KI-Seitenleiste und das Meldungs-Panel bisher Schritte und Bilder eines Abschnitts zeichneten, zeigt die App jetzt Titel und ersten Satz mit dem Knopf „In der Hilfe öffnen“; die Schritte lesen sich in der Hilfe selbst.

## Begründung

Eine Darstellung heißt: Suche, Anker, Bilder und Rangfolge ändern sich an einer Stelle, in App und Browser gleich. Das Muster (eigener Einstieg, im WebView2) hat sich bei der Verwaltung bewährt. Aus dem lokalen Ordner statt vom Server geladen, bleibt die Hilfe so unabhängig wie die alte Avalonia-Hilfe, ohne dass die Texte ein zweites Mal gerendert werden. Das Paket wird nicht größer: Die Bilder liegen nur noch im Web-Build (vorher dort und als Ressource in der App).

## Folgen und Stand

- Code: `web/help/index.html`, `web/src/help/` (`HelpApp.tsx`, `HelpIndex.tsx`, `helpUrl.ts`, `helpPage.css`), `src/AispaStudio.Desktop/Views/HelpView.axaml(.cs)`, `ViewModels/HelpViewModel.cs`, `Services/HelpContent.cs`; Auslieferung in `docker/nginx.conf` und `System/TeamWebRoot.cs` (`help`).
- Die Windows-App zeigt die Hilfe nur mit echtem WebView2; die Tests prüfen die Adresse (virtueller Host, Thema, Abschnitt, Rolle), nicht das Bild im WebView2. Die Zuordnung und das Laden der gebauten Seite wurden mit einem kleinen Avalonia-Probeprogramm gegen das echte WebView2 geprüft (Navigation erfolgreich, Thema „Kassa“, Abschnitt „Barzahlungsrabatt“ markiert), nicht in der ganzen App.
- Die Hilfe folgt dem Farbschema des Betriebssystems, nicht dem Schema aus den Einstellungen der App (wie die Verwaltung im WebView2).
- Offen: Handbuchbilder neu erzeugen (Skripte laufen weiter, aber nur mit Testdaten bzw. Aspire); Hilfe im Kundenbereich; eine Vorlesehilfe-Prüfung der Hilfe-Seite mit Narrator.

## Quellen

- ADR 0053 (Verwaltung im WebView2), ADR 0055 und 0056 (Hilfe im Repository, Handbuch mit Bildern)
- [Hilfe und Handbuch](../../web/src/help/README.md), PR-Protokoll „T-010: Eine Hilfe für Windows-App und Web“
