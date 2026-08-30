[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Building WildsDeck bridge (.NET 10)..."
dotnet restore (Join-Path $ProjectRoot "bridge/WildsDeck.Bridge.slnx")
dotnet build (Join-Path $ProjectRoot "bridge/WildsDeck.Bridge.slnx") --configuration Release --no-restore
dotnet test (Join-Path $ProjectRoot "bridge/WildsDeck.Bridge.slnx") --configuration Release --no-build

Write-Host "Building and validating Stream Deck plugin..."
Push-Location (Join-Path $ProjectRoot "streamdeck")
try {
    if (Test-Path "package-lock.json") {
        npm ci
    } else {
        npm install
    }
    npm run check
    npm test
    npm run build
    npm run validate
} finally {
    Pop-Location
}

Write-Host "Build complete: streamdeck/com.wildsdeck.streamdeck.sdPlugin"

