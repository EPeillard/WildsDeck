[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipProfileImport
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PluginRoot = Join-Path $ProjectRoot "streamdeck"
$PluginBundle = Join-Path $PluginRoot "com.wildsdeck.streamdeck.sdPlugin"
$TownProfile = Join-Path $PluginBundle "WildsDeck - Town.streamDeckProfile"
$HuntProfile = Join-Path $PluginBundle "WildsDeck - Hunt.streamDeckProfile"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1")
}

Push-Location $PluginRoot
try {
    npm run profiles
    npx streamdeck dev
    npx streamdeck link $PluginBundle
    npx streamdeck restart com.wildsdeck.streamdeck
} finally {
    Pop-Location
}

if (-not $SkipProfileImport) {
    foreach ($Profile in @($TownProfile, $HuntProfile)) {
        if (-not (Test-Path $Profile)) {
            throw "Bundled Stream Deck profile not found: $Profile"
        }

        Write-Host "Opening Stream Deck profile installer: $(Split-Path $Profile -Leaf)"
        Start-Process -FilePath $Profile
        Start-Sleep -Milliseconds 750
    }

    Write-Host "Accept the Stream Deck prompts to import WildsDeck - Town and WildsDeck - Hunt."
} else {
    Write-Host "WildsDeck is linked to Stream Deck. Bundled profile import was skipped."
}
