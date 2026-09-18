#!/usr/bin/env bash
set -euo pipefail

pwsh_exe="/mnt/c/Users/newsy/AppData/Local/Microsoft/WindowsApps/pwsh.exe"
if [[ ! -x "$pwsh_exe" ]]; then
  printf 'No se encontró PowerShell de Windows en %s\n' "$pwsh_exe" >&2
  exit 1
fi

exec "$pwsh_exe" -NoProfile -ExecutionPolicy Bypass "$@"
