# Hausschrift Inter

`inter-latin-var.woff2` ist die Hausschrift der Hilfe (Design-Brief Abschnitt 3, Befund B62; bis Slice 103 auch der Web-Team-Oberfläche). Sie ist dieselbe Version wie in der Windows-App (`Avalonia.Fonts.Inter`, Inter 3.019), damit App und WebView2 dieselbe Schrift zeigen. Die Anmeldung, der Kundenbereich, „Mein Tag“, die Betreiber-Seite und die Tafel laden sie nicht.

- Quelle: offizielles Release Inter 3.19, <https://github.com/rsms/inter/releases/tag/v3.19>, Archiv `Inter-3.19.zip`, Datei `Inter Web/Inter-roman.var.woff2` (SHA-256 `17fe38ab302c7e5dbfb5c3d87801092d79be958500db6412ed3bc0f126bd53d3`).
- Lizenz: SIL Open Font License 1.1, Text in `LICENSE.txt` (unverändert aus dem Archiv).
- Inhalt: Achse `wght` auf 400 bis 600 begrenzt (der Brief erlaubt nur 400, 500 und 600), kein Kursiv, Zeichen der Teilmenge „latin“ (Deutsch, €, typografische Anführungszeichen, Gedankenstrich) und die Zeichen, die Oberfläche und Hilfe als Text nutzen und Inter enthält (Pfeile ← ↑ → ↓ ↔ ↕, ✓ ★ ▶ ⚠), alle OpenType-Merkmale (auch `tnum` für gleich breite Ziffern). Größe 39,8 kB. ✕ ▾ ⋯ ⧉ ☐ ♀ ♂ fehlen in Inter und kommen weiter aus der Systemschrift.

## Neu erzeugen

Mit Python und `fonttools` samt `brotli` (`python -m pip install --user fonttools brotli`, nur lokal nötig):

```bash
python -m fontTools.varLib.instancer Inter-roman.var.woff2 wght=400:600 -o inter-wght.ttf
python -m fontTools.subset inter-wght.ttf --layout-features='*' --flavor=woff2 --output-file=inter-latin-var.woff2 \
  --unicodes=U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+20AC,U+2122,U+2190-2195,U+2212,U+2215,U+25B6,U+2605,U+26A0,U+2713,U+FEFF,U+FFFD
```

Eingebunden wird die Datei per `@font-face` in `web/src/index.css` und per `<link rel="preload">` in `web/help/index.html`; geprüft von `web/src/interFont.test.ts` und `web/scripts/check-bundles.mjs`.
