#!/usr/bin/env bash
set -euo pipefail

# Build self-contained single-file for Linux x64
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet publish -r linux-x64 --configuration Release -f net8.0 -p:UseAppHost=true --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true

mkdir -p linux-dist/PQLauncher
mv bin/Release/net8.0/linux-x64/publish/* "linux-dist/PQLauncher/"
rm "linux-dist/PQLauncher/PQLauncher.pdb"

tar -czf linux-dist/PQLauncher-linux-x64.tar.gz -C linux-dist PQLauncher
