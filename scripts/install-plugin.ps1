[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipProfileImport
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PluginRoot = Join-Path $ProjectRoot "streamdeck"
$PluginBundle = Join-Path $PluginRoot "com.wildsdeck.streamdeck.sdPlugin"
$BridgeDirectory = Join-Path $PluginBundle "bin\bridge"
$BridgeExe = Join-Path $BridgeDirectory "WildsDeck.Bridge.exe"
$BridgePort = 47653

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1")
}

if (-not (Test-Path $BridgeExe)) {
    throw "Bundled bridge not found at $BridgeExe. Run .\scripts\build.ps1 first or omit -SkipBuild."
}

Push-Location $PluginRoot
try {
    npm run profiles
    npx streamdeck dev
} finally {
    Pop-Location
}

$mockProcess = $null
try {
    if (-not $SkipProfileImport) {
        $existingListener = Get-NetTCPConnection -LocalPort $BridgePort -State Listen -ErrorAction SilentlyContinue
        if ($existingListener) {
            Write-Warning "Port $BridgePort is already in use. Profile bootstrap will be skipped; close the running bridge and rerun this script if the bundled profiles are not installed."
        } else {
            Write-Host "Bootstrapping the bundled WildsDeck profile (Town page, then Hunt page)..."
            Write-Host "Accept the Stream Deck profile-install prompt if one appears."

            $startArgs = @{
                FilePath = $BridgeExe
                ArgumentList = @("--mock")
                WorkingDirectory = $BridgeDirectory
                PassThru = $true
                WindowStyle = "Hidden"
            }
            $mockProcess = Start-Process @startArgs

            Start-Sleep -Milliseconds 750
            if ($mockProcess.HasExited) {
                throw "The bundled mock bridge exited before Stream Deck could connect."
            }
        }
    }

    Push-Location $PluginRoot
    try {
        npx streamdeck link $PluginBundle
        npx streamdeck restart com.wildsdeck.streamdeck
    } finally {
        Pop-Location
    }

    if ($mockProcess) {
        Start-Sleep -Seconds 12
    }
} finally {
    if ($mockProcess -and -not $mockProcess.HasExited) {
        Stop-Process -Id $mockProcess.Id -Force -ErrorAction SilentlyContinue
    }
}

if ($SkipProfileImport) {
    Write-Host "WildsDeck is linked to Stream Deck. Bundled profile bootstrap was skipped."
} else {
    Write-Host "WildsDeck profile bootstrap finished."
}
Write-Host "The Stream Deck plugin now starts and supervises the bundled bridge automatically."
