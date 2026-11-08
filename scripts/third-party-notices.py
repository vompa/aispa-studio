"""Erzeugt THIRD-PARTY-NOTICES.md: Lizenzhinweise aller Fremdbausteine, die mit der Windows-App ausgeliefert werden.

Quellen: NuGet-Pakete der Windows-App und der API (`dotnet list package --include-transitive`, Angaben aus der
`.nuspec` im NuGet-Cache), npm-Pakete im Web-Bundle (`npm ls --omit=dev`) und das mitgelieferte PostgreSQL.
Lizenztexte stehen je SPDX-Kennung einmal in `scripts/third-party-licenses/`; Pakete mit eigener Lizenzdatei
(ohne SPDX-Ausdruck) bringen ihren Text selbst mit.

Aufruf aus dem Hauptordner nach `dotnet restore` und `npm ci` in `web` (die CI tut das vor dem Packen):
    python scripts/third-party-notices.py
"""

from __future__ import annotations

import json
import os
import pathlib
import re
import subprocess
import sys
import xml.etree.ElementTree as ElementTree
from dataclasses import dataclass, field

ROOT = pathlib.Path(__file__).resolve().parent.parent
LICENSE_TEXTS = ROOT / "scripts" / "third-party-licenses"
OUTPUT = ROOT / "THIRD-PARTY-NOTICES.md"
PROJECTS = ["src/AispaStudio.Desktop", "src/AispaStudio.Api"]
NUGET_CACHE = pathlib.Path(os.environ.get("NUGET_PACKAGES", pathlib.Path.home() / ".nuget" / "packages"))
# Nur im Debug-Build eingebunden (IncludeAssets None außerhalb von Debug, AispaStudio.Desktop.csproj), nicht im Paket.
NOT_SHIPPED = {"AvaloniaUI.DiagnosticsSupport"}
LICENSE_FILE = re.compile(r"^(licen[cs]e|copying)(\.(md|txt))?$", re.IGNORECASE)


@dataclass(frozen=True)
class Component:
    """Ein Fremdbaustein mit Lizenz (SPDX-Ausdruck oder eigener Text) und Copyright-Zeile."""

    name: str
    version: str
    source: str
    license: str
    copyright: str
    url: str
    own_text: str = ""


@dataclass
class LicenseGroup:
    """Alle Bausteine mit derselben Lizenz und demselben Text."""

    title: str
    text: str
    components: list[Component] = field(default_factory=list)


def run(command: list[str], cwd: pathlib.Path) -> str:
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True, encoding="utf-8", check=False, shell=os.name == "nt")
    if result.returncode != 0:
        raise SystemExit(f"Fehler bei {' '.join(command)}:\n{result.stderr}")
    return result.stdout


def nuget_packages() -> dict[str, str]:
    """Paket-Id → Version aller Pakete (direkt und transitiv) der ausgelieferten Projekte."""
    packages: dict[str, str] = {}
    for project in PROJECTS:
        data = json.loads(run(["dotnet", "list", project, "package", "--include-transitive", "--format", "json"], ROOT))
        for entry in data["projects"]:
            for framework in entry.get("frameworks", []):
                for package in framework.get("topLevelPackages", []) + framework.get("transitivePackages", []):
                    if package["id"] not in NOT_SHIPPED:
                        packages[package["id"]] = package["resolvedVersion"]
    return packages


def read_license_file(folder: pathlib.Path) -> str:
    for candidate in sorted(folder.iterdir()):
        if candidate.is_file() and LICENSE_FILE.match(candidate.name):
            return candidate.read_text(encoding="utf-8", errors="replace").strip()
    return ""


def nuget_component(package_id: str, version: str) -> Component:
    folder = NUGET_CACHE / package_id.lower() / version.lower()
    nuspec = folder / f"{package_id.lower()}.nuspec"
    if not nuspec.exists():
        raise SystemExit(f"{nuspec} fehlt: zuerst dotnet restore")
    metadata = ElementTree.parse(nuspec).getroot().find("{*}metadata")
    if metadata is None:
        raise SystemExit(f"{nuspec}: kein metadata-Element")

    def value(tag: str) -> str:
        element = metadata.find(f"{{*}}{tag}")
        return (element.text or "").strip() if element is not None else ""

    license_element = metadata.find("{*}license")
    expression = ""
    own_text = ""
    if license_element is not None and license_element.get("type") == "expression":
        expression = (license_element.text or "").strip()
    elif license_element is not None and license_element.get("type") == "file":
        own_text = (folder / (license_element.text or "").strip()).read_text(encoding="utf-8", errors="replace").strip()
    else:
        own_text = read_license_file(folder)
    return Component(
        name=package_id,
        version=version,
        source="NuGet",
        license=expression or ("eigene Lizenz" if own_text else value("licenseUrl") or "unbekannt"),
        copyright=value("copyright") or value("authors"),
        url=value("projectUrl") or value("licenseUrl"),
        own_text=own_text,
    )


def npm_components() -> list[Component]:
    """Pakete im Web-Bundle (ohne Entwicklungswerkzeuge)."""
    web = ROOT / "web"
    # --parseable nennt nur installierte Pakete (fehlende optionale Abhängigkeiten nicht), je Zeile ein Ordner.
    folders = run(["npm", "ls", "--omit=dev", "--all", "--parseable"], web).splitlines()[1:]
    found: dict[tuple[str, str], Component] = {}
    for line in folders:
        folder = pathlib.Path(line.strip())
        manifest = json.loads((folder / "package.json").read_text(encoding="utf-8"))
        author = manifest.get("author", "")
        if isinstance(author, dict):
            author = author.get("name", "")
        repository = manifest.get("repository", "")
        if isinstance(repository, dict):
            repository = repository.get("url", "")
        own_text = read_license_file(folder)
        component = Component(
            name=manifest["name"],
            version=manifest.get("version", ""),
            source="npm",
            license=str(manifest.get("license", "unbekannt")),
            copyright=copyright_line(own_text) or author,
            url=manifest.get("homepage", "") or repository,
            own_text=own_text,
        )
        found[(component.name, component.version)] = component
    return list(found.values())


def copyright_line(text: str) -> str:
    for line in text.splitlines():
        if line.strip().lower().startswith("copyright"):
            return line.strip()
    return ""


def postgresql() -> Component:
    return Component(
        name="PostgreSQL",
        version="Build des CI-Runners (EnterpriseDB)",
        source="Programm",
        license="PostgreSQL",
        copyright="Portions Copyright (c) 1996-2026, The PostgreSQL Global Development Group; "
        "Portions Copyright (c) 1994, The Regents of the University of California",
        url="https://www.postgresql.org/about/licence/",
    )


def standard_text(spdx: str) -> str:
    path = LICENSE_TEXTS / f"{spdx}.txt"
    if not path.exists():
        raise SystemExit(f"Lizenztext {path} fehlt: SPDX-Text dort ablegen (https://spdx.org/licenses/{spdx}.html)")
    return path.read_text(encoding="utf-8").strip()


def group(components: list[Component]) -> list[LicenseGroup]:
    groups: dict[tuple[str, str], LicenseGroup] = {}
    for component in components:
        if component.own_text:
            key = (component.license, component.own_text)
            title = f"{component.name} (eigene Lizenz)" if component.license == "eigene Lizenz" else f"{component.license}, Wortlaut von {component.name}"
            text = component.own_text
        else:
            key = (component.license, "")
            title = component.license
            text = standard_text(component.license)
        groups.setdefault(key, LicenseGroup(title=title, text=text)).components.append(component)
    return sorted(groups.values(), key=lambda g: (-len(g.components), g.title))


def render(groups: list[LicenseGroup]) -> str:
    lines = [
        "# Third-Party Notices",
        "",
        "AISPA Studio steht unter der MIT-Lizenz (`LICENSE`). Die Windows-App liefert Fremdbausteine mit, die ihre eigenen",
        "Lizenzen behalten. Diese Datei nennt sie mit Copyright und Lizenztext. Erzeugt mit",
        "`python scripts/third-party-notices.py`; nicht von Hand ändern.",
        "",
        "Das mitgelieferte PostgreSQL (Ordner `postgres`) bringt weitere Bibliotheken des EnterpriseDB-Builds mit",
        "(zum Beispiel OpenSSL, ICU, zlib). Sie sind hier noch nicht einzeln aufgeführt.",
        "",
    ]
    for license_group in groups:
        lines += [f"## {license_group.title}", ""]
        for component in sorted(license_group.components, key=lambda c: c.name.lower()):
            detail = "; ".join(part for part in (component.copyright, component.url) if part)
            lines.append(f"- {component.name} {component.version} ({component.source}){': ' + detail if detail else ''}")
        lines += ["", "```text", license_group.text, "```", ""]
    return "\n".join(lines)


def main() -> int:
    components = [nuget_component(i, v) for i, v in sorted(nuget_packages().items())]
    components += npm_components()
    components.append(postgresql())
    groups = group(components)
    OUTPUT.write_bytes(render(groups).encode("utf-8"))
    print(f"{OUTPUT.name}: {len(components)} Bausteine, {len(groups)} Lizenztexte")
    return 0


if __name__ == "__main__":
    sys.exit(main())
