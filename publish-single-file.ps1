param(
    [string[]]$RuntimeIdentifier = @("win-x64", "linux-x64")
)

$ErrorActionPreference = "Stop"

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
        -o $output

    if ($LASTEXITCODE -ne 0) {
        throw "Single-file publish failed for $rid."
    }
}
