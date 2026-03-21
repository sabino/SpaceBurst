param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$docsRoot = Join-Path $root "docs"
$siteRoot = Join-Path $docsRoot "_site"
$docsConfig = Join-Path $docsRoot "docfx.json"
$webPublishRoot = Join-Path $root "artifacts\pages\web"
$webSiteRoot = Join-Path $siteRoot "web"

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed."
}

if (Test-Path $siteRoot) {
    Remove-Item $siteRoot -Recurse -Force
}

if (Test-Path $webPublishRoot) {
    Remove-Item $webPublishRoot -Recurse -Force
}

dotnet build (Join-Path $root "SpaceBurst.sln") -c $Configuration -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "Solution build failed."
}

dotnet tool run docfx -- "$docsConfig"
if ($LASTEXITCODE -ne 0) {
    throw "DocFX build failed."
}

dotnet publish (Join-Path $root "SpaceBurst.Web\SpaceBurst.Web.csproj") -c $Configuration --no-build -o $webPublishRoot
if ($LASTEXITCODE -ne 0) {
    throw "Browser publish failed."
}

if (Test-Path $webSiteRoot) {
    Remove-Item $webSiteRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $webSiteRoot | Out-Null
Copy-Item (Join-Path $webPublishRoot "wwwroot\*") $webSiteRoot -Recurse -Force
Set-Content -Path (Join-Path $siteRoot ".nojekyll") -Value "" -NoNewline

Write-Host "Docs and browser site built to $siteRoot"
