[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$BridgeProject = Join-Path $ProjectRoot "bridge\src\WildsDeck.Bridge\WildsDeck.Bridge.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot "streamdeck\com.wildsdeck.streamdeck.sdPlugin\bin\bridge"
}

if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Write-Host "Publishing self-contained WildsDeck bridge for Windows x64..."
$publishArgs = @(
    "publish"
    $BridgeProject
    "--configuration"
    "Release"
    "--runtime"
    "win-x64"
    "--self-contained"
    "true"
    "--output"
    $OutputDirectory
    "-p:PublishSingleFile=true"
    "-p:DebugType=None"
    "-p:DebugSymbols=false"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item -Path (Join-Path $ProjectRoot "maps") -Destination (Join-Path $OutputDirectory "maps") -Recurse -Force
Copy-Item -Path (Join-Path $ProjectRoot "wildsdeck.json") -Destination (Join-Path $OutputDirectory "wildsdeck.json") -Force

$BridgeExe = Join-Path $OutputDirectory "WildsDeck.Bridge.exe"
if (-not (Test-Path $BridgeExe)) {
    throw "Bundled bridge executable was not produced at $BridgeExe."
}

Write-Host "Bundled bridge ready: $BridgeExe"
