# Führt die ViewTests in vier Prozessen aus: Eine Sitzung hat nur einen Oberflächen-Thread, also geht parallel nur über Prozesse.
# Vorher bauen: dotnet build tests/AispaStudio.Desktop.Tests -c Release. Die Protokolle landen in .test-logs.
# Mit -Group 0..3 läuft nur diese Gruppe im Vordergrund, Ausgabe auf der Konsole (CI: ein Job je Gruppe).
param([ValidateRange(-1, 3)][int]$Group = -1)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$logs = Join-Path $root '.test-logs'
$class = 'AispaStudio.Desktop.Tests.ViewTests'
$project = 'tests/AispaStudio.Desktop.Tests'
# Nach Anfangsbuchstaben etwa gleich lang (Messung 01.11.2026); die letzte Gruppe nimmt alles Übrige, auch neue Anfangsbuchstaben.
$groups = @('GENMU', 'JAHBL', 'KTWDPI')

New-Item -ItemType Directory -Force $logs | Out-Null
$named = $groups | ForEach-Object { $_.ToCharArray() }
$filters = @($groups | ForEach-Object { ($_.ToCharArray() | ForEach-Object { "--filter-method `"$class.$_*`"" }) -join ' ' })
$filters += "--filter-class `"$class`" " + (($named | ForEach-Object { "--filter-not-method `"$class.$_*`"" }) -join ' ')

if ($Group -ge 0) {
    $process = Start-Process dotnet -PassThru -NoNewWindow -WorkingDirectory $root `
        -ArgumentList "test --project $project -c Release --no-build -- $($filters[$Group])"
    $null = $process.Handle
    $process.WaitForExit()
    exit $process.ExitCode
}

$runs = foreach ($index in 0..3) {
    $log = Join-Path $logs "view-tests-$index.log"
    $process = Start-Process dotnet -PassThru -NoNewWindow -WorkingDirectory $root -RedirectStandardOutput $log -RedirectStandardError "$log.err" `
        -ArgumentList "test --project $project -c Release --no-build -- $($filters[$index])"
    # Handle merken, sonst liefert ExitCode unter Windows nach dem Ende nichts.
    $null = $process.Handle
    [pscustomobject]@{ Process = $process; Log = $log }
}

$failed = 0
foreach ($run in $runs) {
    $run.Process.WaitForExit()
    Select-String -Path $run.Log -Pattern '^  (gesamt|fehlgeschlagen|übersprungen|Dauer):' | ForEach-Object { $_.Line }
    if ($run.Process.ExitCode -ne 0) {
        $failed++
        "Rot: $($run.Log)"
    }
}

exit $failed
