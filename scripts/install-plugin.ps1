[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PluginRoot = Join-Path $ProjectRoot "streamdeck"
$PluginBundle = Join-Path $PluginRoot "com.wildsdeck.streamdeck.sdPlugin"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1")
}

Push-Location $PluginRoot
try {
    npx streamdeck dev
    npx streamdeck link $PluginBundle
    npx streamdeck restart com.wildsdeck.streamdeck
} finally {
    Pop-Location
}

Write-Host "WildsDeck is linked to Stream Deck. The Town and Hunt profiles are bundled and auto-installable."

