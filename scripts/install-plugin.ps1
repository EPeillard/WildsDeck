[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipProfileImport
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PluginRoot = Join-Path $ProjectRoot "streamdeck"
$PluginBundle = Join-Path $PluginRoot "com.wildsdeck.streamdeck.sdPlugin"
$BridgeProject = Join-Path $ProjectRoot "bridge\src\WildsDeck.Bridge\WildsDeck.Bridge.csproj"
$BridgePort = 47653

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
    # Bundled plugin profiles are installed by Stream Deck when the plugin calls
    # switchToProfile(). Opening the .streamDeckProfile file directly is not
    # equivalent and is unreliable on recent Stream Deck/Windows versions.
    # A short mock cycle deliberately emits Town first, then Hunt, causing the
    # plugin to request both bundled profiles through the supported SDK path.
    $existingListener = Get-NetTCPConnection -LocalPort $BridgePort -State Listen -ErrorAction SilentlyContinue
    if ($existingListener) {
        Write-Warning "Port $BridgePort is already in use. Stop the running WildsDeck bridge, then rerun this script to bootstrap the bundled profiles."
    } else {
        Write-Host "Bootstrapping bundled profiles through Stream Deck (Town, then Hunt)..."
        Write-Host "Accept the Stream Deck profile-install prompts as they appear."

        $mockProcess = Start-Process -FilePath "dotnet" `
            -ArgumentList @("run", "--no-build", "--project", $BridgeProject, "--", "--mock") `
            -WorkingDirectory $ProjectRoot `
            -PassThru `
            -WindowStyle Hidden

        try {
            # Mock cycle: Town is immediate; Hunt begins at 8 s. Allow enough
            # time for the bridge debounce and Stream Deck install prompts.
            Start-Sleep -Seconds 12
        } finally {
            if (-not $mockProcess.HasExited) {
                Stop-Process -Id $mockProcess.Id -Force -ErrorAction SilentlyContinue
            }
        }

        Write-Host "Profile bootstrap finished. Start the bridge normally for live telemetry."
    }
} else {
    Write-Host "WildsDeck is linked to Stream Deck. Bundled profile bootstrap was skipped."
}
