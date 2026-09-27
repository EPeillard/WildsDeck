[CmdletBinding()]
param(
    [ValidateSet("cycle", "town", "hunt")]
    [string]$Mock = "cycle",
    [switch]$SkipBuild,
    [switch]$SkipPluginLink
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PluginBundle = Join-Path $ProjectRoot "streamdeck\com.wildsdeck.streamdeck.sdPlugin"
$BridgeDirectory = Join-Path $PluginBundle "bin\bridge"
$BridgeExe = Join-Path $BridgeDirectory "WildsDeck.Bridge.exe"
$BridgePort = 47653

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1")
}

if (-not (Test-Path $BridgeExe)) {
    throw "Bundled bridge not found at $BridgeExe. Run .\scripts\build.ps1 first."
}

$existingListener = Get-NetTCPConnection -LocalPort $BridgePort -State Listen -ErrorAction SilentlyContinue
if ($existingListener) {
    throw "Port $BridgePort is already in use. Close Stream Deck (or stop the running WildsDeck bridge) before starting a mock development session."
}

$MockArgument = switch ($Mock) {
    "town" { "--mock-town" }
    "hunt" { "--mock-hunt" }
    default { "--mock" }
}

Write-Host "Starting bundled WildsDeck Bridge in mock mode ($Mock)..."
$startArgs = @{
    FilePath = $BridgeExe
    ArgumentList = @($MockArgument)
    WorkingDirectory = $BridgeDirectory
    PassThru = $true
    NoNewWindow = $true
}
$mockProcess = Start-Process @startArgs

try {
    Start-Sleep -Milliseconds 750
    if ($mockProcess.HasExited) {
        throw "The mock bridge exited before Stream Deck could connect."
    }

    if (-not $SkipPluginLink) {
        & (Join-Path $PSScriptRoot "install-plugin.ps1") -SkipBuild -SkipProfileImport
    }

    Write-Host "Mock bridge running. Press Ctrl+C to stop it."
    Wait-Process -Id $mockProcess.Id
} finally {
    if (-not $mockProcess.HasExited) {
        Stop-Process -Id $mockProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
