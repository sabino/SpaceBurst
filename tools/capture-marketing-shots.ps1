param(
    [string]$ExecutablePath = "",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $PSScriptRoot "..\SpaceBurst\bin\Release\net8.0\SpaceBurst.exe"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot "..\docs\media"
}

$ExecutablePath = [System.IO.Path]::GetFullPath($ExecutablePath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

if (-not (Test-Path $ExecutablePath)) {
    throw "Game executable not found at $ExecutablePath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$captures = @(
    @{ Mode = "title"; Path = (Join-Path $OutputDirectory "title-screen.png") },
    @{ Mode = "slot-1"; Path = (Join-Path $OutputDirectory "gameplay-combat.png") },
    @{ Mode = "slot-2"; Path = (Join-Path $OutputDirectory "boss-fx.png") }
)

foreach ($capture in $captures) {
    if (Test-Path $capture.Path) {
        Remove-Item $capture.Path -Force
    }

    $env:SPACEBURST_CAPTURE_MODE = $capture.Mode
    $env:SPACEBURST_CAPTURE_PATH = $capture.Path

    $proc = Start-Process $ExecutablePath -PassThru
    if (-not $proc.WaitForExit(30000)) {
        Stop-Process -Id $proc.Id -Force
        throw "Timed out while generating $($capture.Path)"
    }

    if (-not (Test-Path $capture.Path)) {
        throw "Capture was not written: $($capture.Path)"
    }
}

Remove-Item Env:SPACEBURST_CAPTURE_MODE -ErrorAction SilentlyContinue
Remove-Item Env:SPACEBURST_CAPTURE_PATH -ErrorAction SilentlyContinue

Get-ChildItem $OutputDirectory | Select-Object Name, Length
