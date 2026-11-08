# Live-Test der Windows-App (ADR 0090): baut App und Tester im Release-Modus und spielt eine Rolle sichtbar durch.
# Voraussetzung: Server mit Demo-Daten, z. B. docker compose up -d --build (API auf http://localhost:5086/).
# Mit -Gui startet statt der Konsole die Starter-App (Rolle, Themen und Knöpfe im Fenster).
# Tasten in der Konsole: Leertaste Pause/Weiter, Enter Einzelschritt (in der Pause), S Bild jetzt, Q Stopp.
# Bericht und Bilder: .test-logs/live/<Zeitstempel>-<rolle>/bericht.md. Exitcode 0 bestanden, 1 Schritt gescheitert, 2 Aufruf/Server, 3 gestoppt.
param(
    [ValidateSet('Reception')][string]$Role = 'Reception',
    [ValidateSet('slow', 'normal', 'fast')][string]$Speed = 'normal',
    [switch]$Step,
    [string[]]$Topics = @(),
    [switch]$Gui,
    [string]$Api = 'http://localhost:5086/'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot

if ($Gui) {
    # Die Starter-App prüft den Server selbst und zeigt den Stand an.
    foreach ($project in 'src/AispaStudio.Desktop', 'tests/AispaStudio.LiveTests.Launcher') {
        dotnet build (Join-Path $root $project) -c Release --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    Start-Process (Join-Path $root 'tests/AispaStudio.LiveTests.Launcher/bin/Release/net10.0-windows/AispaStudio.LiveTests.Launcher.exe')
    exit 0
}

try {
    $status = Invoke-RestMethod -Uri ($Api.TrimEnd('/') + '/api/system/status') -TimeoutSec 5
    if ($status.startup.phase -ne 'Ready') {
        "Server unter $Api startet noch ($($status.startup.phase)); der Tester wartet bis zu drei Minuten."
    }
}
catch {
    "Kein Server unter $Api. Zuerst starten: docker compose up -d --build"
    exit 2
}

foreach ($project in 'src/AispaStudio.Desktop', 'tests/AispaStudio.LiveTests') {
    dotnet build (Join-Path $root $project) -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$app = Join-Path $root 'src/AispaStudio.Desktop/bin/Release/net10.0/AispaStudio.Desktop.exe'
$tester = Join-Path $root 'tests/AispaStudio.LiveTests/bin/Release/net10.0-windows/AispaStudio.LiveTests.exe'
$arguments = @('--app', $app, '--api', $Api, '--speed', $Speed, '--out', (Join-Path $root '.test-logs/live'))
if ($Step) { $arguments += '--step' }
if ($Topics.Count -gt 0) { $arguments += @('--topics', ($Topics -join ',')) }

# Nur eine Rolle gibt es bisher; -Role steht schon da, damit Inhaberin und Mitarbeiterin ohne neuen Aufruf dazukommen.
& $tester @arguments
exit $LASTEXITCODE
