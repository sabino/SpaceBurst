param(
    [string[]]$RuntimeIdentifier = @("win-x64", "linux-x64")
)

$ErrorActionPreference = "Stop"

$commit = (git -C $PSScriptRoot rev-parse --short HEAD).Trim()
if (-not $commit) {
    throw "Unable to determine git commit."
}

$versionInfo = & (Join-Path $PSScriptRoot "tools\Get-BuildVersion.ps1") -Commit $commit | ConvertFrom-Json
$project = Join-Path $PSScriptRoot "SpaceBurst\\SpaceBurst.csproj"
$artifactsRoot = Join-Path $PSScriptRoot "artifacts\\singlefile"

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed."
}

foreach ($rid in $RuntimeIdentifier) {
    $output = Join-Path $artifactsRoot $rid

    if (Test-Path $output) {
        Remove-Item $output -Recurse -Force
    }

    dotnet publish $project `
        -c Release `
        -r $rid `
        -p:SelfContained=true `
        -p:PublishSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:Version=$($versionInfo.BuildVersion) `
        -p:FileVersion=$($versionInfo.FileVersion) `
        -p:InformationalVersion=$($versionInfo.InformationalVersion) `
        -o $output

    if ($LASTEXITCODE -ne 0) {
        throw "Single-file publish failed for $rid."
    }

    $sourceExe = Get-ChildItem $output -Filter "SpaceBurst*" | Where-Object { -not $_.PSIsContainer -and $_.Extension -in '.exe', '' } | Select-Object -First 1
    if ($sourceExe) {
        $targetName = if ([string]::IsNullOrWhiteSpace($sourceExe.Extension)) {
            "SpaceBurst-$rid-$($versionInfo.ArtifactTag)"
        }
        else {
            "SpaceBurst-$rid-$($versionInfo.ArtifactTag)$($sourceExe.Extension)"
        }

        Copy-Item $sourceExe.FullName (Join-Path $artifactsRoot $targetName) -Force
    }
}
