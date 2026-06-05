#!/usr/bin/env bash
# Shared .NET startup helpers.
# Sourced by scripts/run-prod.sh at runtime and by tests in scripts/tests/.

# launch_svc <name> <path/to/Service.csproj> [cmd-prefix...]
#
# Verifies the Release DLL exists, then starts the service in the background
# by invoking the compiled DLL directly:
#
#   dotnet <dll>
#
# This uses the .NET runtime host directly (bypassing `dotnet run` and the
# full CLI framework), which avoids the NuGet-Migrations named-mutex that
# fails in Replit autoscale containers where POSIX named semaphores are
# restricted.  The supplied command prefix (e.g. env ASPNETCORE_URLS=...) is
# applied before the dotnet invocation.
#
# Exits with code 1 and an informative message when the binary is missing.
launch_svc() {
  local name="$1" project="$2"
  shift 2
  local dll_dir dll_name
  dll_dir="$(dirname "$project")/bin/Release/net10.0"
  dll_name="$(basename "$project" .csproj).dll"
  if [ ! -f "$dll_dir/$dll_name" ]; then
    echo "[dotnet] ERROR: $name binary not found at $dll_dir/$dll_name — aborting"
    exit 1
  fi
  # Invoke the DLL directly — no 'dotnet run', no CLI NuGet mutex.
  (cd "$(dirname "$project")" && "$@" dotnet "$dll_dir/$dll_name") &
  echo "[dotnet] $name launched (pid $!)"
}
