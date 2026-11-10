#!/usr/bin/env bash
# Erzeugt die Bilder der Windows-App für die Hilfe (web/src/help/images/app) neu: lässt die Bildtests der App ohne Bildschirm laufen
# und übernimmt jedes Bild, das ein Hilfetext mit ![…](images/app/<name>.png) verwendet. Die Bilder der Team-Oberfläche
# (web/src/help/images/web) macht web/scripts/help-images.mjs.
# Die Bildtests der App melden sich als Inhaberin an (Katrin Aigner, alle Rechte), wie der Auftraggeber beim Prüfen; was Rechte ausblenden, prüfen eigene Tests als Rezeption.
# Aufruf im Hauptordner: scripts/help-images.sh
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
help="$root/web/src/help"
shots="$(mktemp -d)"
trap 'rm -rf "$shots"' EXIT

AISPA_SCREENSHOTS="$shots" dotnet test --project "$root/tests/AispaStudio.Desktop.Tests" -c Release -- --filter-class "*ViewTests" >/dev/null

mkdir -p "$help/images/app"
missing=0
for name in $(grep -ho '](images/app/[a-z0-9-]*\.png)' "$help"/content/*.md | sed 's/](images\/app\/\(.*\)\.png)/\1/' | sort -u); do
  if [[ "$name" == "rechnung-als-pdf" ]]; then
    continue # Beispiel-PDF der Rechnung, kein Bildtest (Muster aus ReceiptPdfTests mit AISPA_PDF_SAMPLES, mit pymupdf gerendert)
  elif [[ -f "$shots/$name.png" ]]; then
    cp "$shots/$name.png" "$help/images/app/$name.png"
  else
    echo "Kein Bildtest erzeugt $name.png (Render(\"hell\", \"$name\", …) in ViewTests.cs fehlt)" >&2
    missing=1
  fi
done
# Auf 256 Farben verkleinern (braucht Pillow), damit App und Web klein bleiben.
python3 "$root/scripts/help-images-shrink.py" || echo "Bilder nicht verkleinert: python3 mit Pillow fehlt" >&2
exit $missing
