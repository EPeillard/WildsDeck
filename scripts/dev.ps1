[CmdletBinding()]
param(
    [ValidateSet("cycle", "town", "hunt")]
    [string]$Mock = "cycle",
    [switch]$SkipBuild,
    [switch]$SkipPluginLink
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1")
}

if (-not $SkipPluginLink) {
    & (Join-Path $PSScriptRoot "install-plugin.ps1") -SkipBuild
}

$MockArgument = switch ($Mock) {
    "town" { "--mock-town" }
    "hunt" { "--mock-hunt" }
    default { "--mock" }
}

Write-Host "Starting WildsDeck Bridge in mock mode ($Mock)..."
Push-Location $ProjectRoot
try {
    dotnet run --project "bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj" -- $MockArgument
} finally {
    Pop-Location
}

