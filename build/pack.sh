#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Release}"

dotnet restore
dotnet build -c "$CONFIGURATION"
dotnet test -c "$CONFIGURATION"
dotnet pack src/GenericPipeline/GenericPipeline.csproj -c "$CONFIGURATION" -o ./artifacts
