Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$rid = 'win-x64'

dotnet publish -r $rid --configuration Release -f net8.0 -p:UseAppHost=true --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true

$publishDir = "bin/Release/net8.0/$rid/publish"
$distDir = "win-dist/PQLauncher"
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Get-ChildItem $publishDir | Move-Item -Destination $distDir -Force

$zipPath = "win-dist/PQLauncher-windows-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$distDir/*" -DestinationPath $zipPath
