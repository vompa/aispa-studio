"""Verkleinert die Bilder der Hilfe (web/src/help/images/app und images/web) auf 256 Farben, damit App und Web klein bleiben.

Bildschirmfotos haben wenige Farben, der Unterschied ist nicht zu sehen. Braucht Pillow (pip install pillow).
Aufruf im Hauptordner: python3 scripts/help-images-shrink.py [Datei …]  (ohne Angabe alle Bilder)
"""
import pathlib
import sys

from PIL import Image

folder = pathlib.Path(__file__).resolve().parent.parent / "web" / "src" / "help" / "images"
files = [pathlib.Path(f) for f in sys.argv[1:]] or sorted(folder.glob("*/*.png"))
for file in files:
    image = Image.open(file)
    if image.mode == "P":
        continue  # schon verkleinert
    before = file.stat().st_size
    image.convert("RGB").quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).save(file, optimize=True)
    print(f"{file.name}: {before // 1024} kB -> {file.stat().st_size // 1024} kB")
