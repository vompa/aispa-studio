"""Setzt neu erzeugte Bilder der Hilfe zurück, die sich nicht sichtbar geändert haben, damit ein Bildlauf nur echte Änderungen hinterlässt.

Ein Bildlauf ändert viele Dateien, die gleich aussehen: Kantenglättung, Farbreduktion und die Zufallsendung der Ersatzbeleg-Nummer.
Sichtbar geändert heißt: andere Größe oder mindestens 300 Pixel mit Farbabstand über 70 zum Stand in HEAD. Messung vom 20.11.2026:
Rauschen höchstens 216 Pixel, echte Änderungen mindestens 429. Ein Bild darunter bleibt beim alten Stand und steht in der Ausgabe;
wer es trotzdem will, kopiert es von Hand. Neue Bilder ohne Stand in HEAD bleiben. Braucht Pillow (pip install pillow).
Aufruf im Hauptordner: python3 scripts/help-images-keep-changed.py Datei …
"""
import io
import pathlib
import subprocess
import sys

from PIL import Image, ImageChops

VISIBLE_PIXELS = 300
CHANNEL_DISTANCE = 70

root = pathlib.Path(__file__).resolve().parent.parent
sys.stdout.reconfigure(encoding="utf-8")  # Windows schreibt sonst in der Codepage der Konsole, Umlaute gehen in Git Bash kaputt


def changed_pixels(old: bytes, new: bytes) -> int | None:
    """Zahl der sichtbar abweichenden Pixel; None bei anderer Größe."""
    with Image.open(io.BytesIO(old)) as a, Image.open(io.BytesIO(new)) as b:
        a, b = a.convert("RGB"), b.convert("RGB")
    if a.size != b.size:
        return None
    mask = ImageChops.difference(a, b).convert("L").point(lambda v: 255 if v > CHANNEL_DISTANCE else 0)
    return mask.histogram()[255]


kept = 0
for file in (pathlib.Path(f).resolve() for f in sys.argv[1:]):
    head = subprocess.run(["git", "-C", str(root), "show", f"HEAD:{file.relative_to(root).as_posix()}"], capture_output=True)
    new = file.read_bytes()
    if head.returncode != 0:
        kept += 1  # neues Bild
        continue
    if head.stdout == new:
        continue
    pixels = changed_pixels(head.stdout, new)
    if pixels is None or pixels >= VISIBLE_PIXELS:
        kept += 1
        continue
    file.write_bytes(head.stdout)
    if pixels > 0:
        print(f"unverändert gelassen: {file.name} ({pixels} Pixel)")
print(f"{kept} Bilder neu oder sichtbar geändert")
