#!/usr/bin/env bash
# Führt die API-Tests in einer von zwei Gruppen aus (CI: ein Job je Gruppe, damit ein Läufer mit 2 Kernen nicht überläuft).
# Gruppe 0: Testklassen mit den Anfangsbuchstaben unten; Gruppe 1: alle übrigen, auch neue Anfangsbuchstaben.
# Vorher bauen: dotnet build AispaStudio.slnx -c Release. Weitere Argumente gehen an den Testlauf (z. B. --filter-method).
# Aufruf im Hauptordner: scripts/api-tests.sh 0|1 [Argumente]
set -euo pipefail

readonly CLASS_PREFIX='AispaStudio.Api.Tests.'
# Nach Laufzeit etwa gleich lang (Messung 07.11.2026, lokal: Gruppe 0 710 Tests 15,5 min, Gruppe 1 743 Tests 12 min).
readonly GROUP_ZERO_LETTERS='ABCDEFGH'

main() {
  local group="${1:-}"
  if [[ "${group}" != 0 && "${group}" != 1 ]]; then
    echo "Aufruf: scripts/api-tests.sh 0|1 [Argumente]" >&2
    exit 2
  fi
  shift

  local root
  root="$(cd "$(dirname "$0")/.." && pwd)"
  local option='--filter-class'
  if [[ "${group}" == 1 ]]; then
    option='--filter-not-class'
  fi

  local -a filters=()
  local letter
  for letter in $(echo "${GROUP_ZERO_LETTERS}" | fold -w1); do
    filters+=("${option}" "${CLASS_PREFIX}${letter}*")
  done

  dotnet test --project "${root}/tests/AispaStudio.Api.Tests" -c Release --no-build -- "${filters[@]}" "$@"
}

main "$@"
