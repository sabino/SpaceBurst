param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$docsRoot = Join-Path $root "docs"
$siteRoot = Join-Path $docsRoot "_site"
$docsConfig = Join-Path $docsRoot "docfx.json"

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed."
}

if (Test-Path $siteRoot) {
    Remove-Item $siteRoot -Recurse -Force
}

dotnet build (Join-Path $root "SpaceBurst.sln") -c $Configuration -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "Solution build failed."
}

dotnet tool run docfx -- "$docsConfig"
if ($LASTEXITCODE -ne 0) {
    throw "DocFX build failed."
}

Write-Host "Docs built to $siteRoot"
