param(
    [string]$AndroidSdkDirectory = "",
    [string]$Configuration = "Release",
    [int]$ApplicationVersion = 0,
    [string]$ApplicationDisplayVersion = "",
    [string]$SigningKeyStorePath = "",
    [string]$SigningStorePass = "",
    [string]$SigningKeyAlias = "",
    [string]$SigningKeyPass = "",
    [switch]$InstallDependencies,
    [switch]$InstallOnDevice
)

$ErrorActionPreference = "Stop"

function Resolve-AndroidSdkDirectory {
    param([string]$PreferredPath)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        $candidates += $PreferredPath
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) {
        $candidates += $env:ANDROID_SDK_ROOT
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        $candidates += $env:ANDROID_HOME
    }
    $candidates += "C:\Android\sdk"
    if ($env:LOCALAPPDATA) {
        $candidates += (Join-Path $env:LOCALAPPDATA "Android\Sdk")
    }

    foreach ($candidate in $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        return $PreferredPath
    }

    return "C:\Android\sdk"
}

function Resolve-LocalReleaseSigning {
    $signingRoot = Join-Path $env:USERPROFILE ".spaceburst\android-signing"
    $keystorePath = Join-Path $signingRoot "spaceburst-release.keystore"
    $credentialsPath = Join-Path $signingRoot "spaceburst-release-credentials.txt"

    if (-not (Test-Path $keystorePath) -or -not (Test-Path $credentialsPath)) {
        return $null
    }

    $values = @{}
    foreach ($line in Get-Content $credentialsPath) {
        if ($line -match '^\s*([^:]+):\s*(.+?)\s*$') {
            $values[$matches[1].Trim()] = $matches[2].Trim()
        }
    }

    if (-not $values.ContainsKey("Alias") -or -not $values.ContainsKey("Store password") -or -not $values.ContainsKey("Key password")) {
        return $null
    }

    return [pscustomobject]@{
        SigningKeyStorePath = $keystorePath
        SigningStorePass = $values["Store password"]
        SigningKeyAlias = $values["Alias"]
        SigningKeyPass = $values["Key password"]
    }
}

$project = Join-Path $PSScriptRoot "SpaceBurst.Android\SpaceBurst.Android.csproj"
$framework = "net8.0-android34.0"
$contentObj = Join-Path $PSScriptRoot "SpaceBurst\Content\obj\Android"
$contentOutput = Join-Path $PSScriptRoot "SpaceBurst\Content\bin\Android\Content"
$mgcb = Join-Path $PSScriptRoot "SpaceBurst\Content\Content.mgcb"
$apkDirectory = Join-Path $PSScriptRoot "SpaceBurst.Android\bin\$Configuration\$framework"
$androidObj = Join-Path $PSScriptRoot "SpaceBurst.Android\obj"
$androidBin = Join-Path $PSScriptRoot "SpaceBurst.Android\bin\$Configuration"
$AndroidSdkDirectory = Resolve-AndroidSdkDirectory $AndroidSdkDirectory
$adb = Join-Path $AndroidSdkDirectory "platform-tools\adb.exe"
$env:ANDROID_SDK_ROOT = $AndroidSdkDirectory
$env:ANDROID_HOME = $AndroidSdkDirectory
$commit = (git -C $PSScriptRoot rev-parse --short HEAD).Trim()
if (-not $commit) {
    throw "Unable to determine git commit."
}

$versionInfo = & (Join-Path $PSScriptRoot "tools\Get-BuildVersion.ps1") -Commit $commit | ConvertFrom-Json
if ($ApplicationVersion -le 0) {
    $ApplicationVersion = [int]$versionInfo.AndroidVersionCode
}

if ([string]::IsNullOrWhiteSpace($ApplicationDisplayVersion)) {
    $ApplicationDisplayVersion = [string]$versionInfo.DisplayVersion
}

if ([string]::IsNullOrWhiteSpace($SigningKeyStorePath)) {
    $localSigning = Resolve-LocalReleaseSigning
    if ($localSigning) {
        $SigningKeyStorePath = $localSigning.SigningKeyStorePath
        $SigningStorePass = $localSigning.SigningStorePass
        $SigningKeyAlias = $localSigning.SigningKeyAlias
        $SigningKeyPass = $localSigning.SigningKeyPass
    }
}

$buildProperties = @(
    "-p:AndroidSdkDirectory=$AndroidSdkDirectory",
    "-p:Version=$($versionInfo.BuildVersion)",
    "-p:FileVersion=$($versionInfo.FileVersion)",
    "-p:InformationalVersion=$($versionInfo.InformationalVersion)"
)

if ($ApplicationVersion -gt 0) {
    $buildProperties += "-p:ApplicationVersion=$ApplicationVersion"
}

if (-not [string]::IsNullOrWhiteSpace($ApplicationDisplayVersion)) {
    $buildProperties += "-p:ApplicationDisplayVersion=$ApplicationDisplayVersion"
}

if (-not [string]::IsNullOrWhiteSpace($SigningKeyStorePath)) {
    if ([string]::IsNullOrWhiteSpace($SigningStorePass) -or
        [string]::IsNullOrWhiteSpace($SigningKeyAlias) -or
        [string]::IsNullOrWhiteSpace($SigningKeyPass)) {
        throw "Android signing requires store password, key alias, and key password."
    }

    Write-Host "Using release signing keystore: $SigningKeyStorePath"
    $buildProperties += "-p:AndroidKeyStore=True"
    $buildProperties += "-p:AndroidSigningKeyStore=$SigningKeyStorePath"
    $buildProperties += "-p:AndroidSigningStorePass=$SigningStorePass"
    $buildProperties += "-p:AndroidSigningKeyAlias=$SigningKeyAlias"
    $buildProperties += "-p:AndroidSigningKeyPass=$SigningKeyPass"
}

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed."
}

if ($InstallDependencies -or -not (Test-Path $adb)) {
    dotnet build $project `
        -t:InstallAndroidDependencies `
        -f $framework `
        -p:AcceptAndroidSdkLicenses=True `
        @buildProperties `
        -v minimal

    if ($LASTEXITCODE -ne 0) {
        throw "Android SDK installation failed."
    }
}

if (Test-Path $contentObj) {
    Remove-Item $contentObj -Recurse -Force
}

if (Test-Path $androidObj) {
    Remove-Item $androidObj -Recurse -Force
}

if (Test-Path $androidBin) {
    Remove-Item $androidBin -Recurse -Force
}

dotnet mgcb `
    /quiet `
    /@:"$mgcb" `
    /platform:Android `
    /outputDir:"$contentOutput" `
    /intermediateDir:"$contentObj\net8.0-android34.0\Content" `
    /workingDir:"$(Join-Path $PSScriptRoot 'SpaceBurst\Content\')"

if ($LASTEXITCODE -ne 0) {
    throw "Android content build failed."
}

dotnet build $project `
    -f $framework `
    -c $Configuration `
    @buildProperties `
    -v minimal

if ($LASTEXITCODE -ne 0) {
    throw "Android build failed."
}

$apk = Get-ChildItem $apkDirectory -Filter "*-Signed.apk" | Select-Object -First 1
if (-not $apk) {
    $apk = Get-ChildItem $apkDirectory -Filter "*.apk" | Select-Object -First 1
}

if (-not $apk) {
    throw "No APK was produced."
}

Write-Output "APK: $($apk.FullName)"

$versionedApkName = "SpaceBurst-android-$($versionInfo.ArtifactTag).apk"
$versionedApkPath = Join-Path $apkDirectory $versionedApkName
if ($apk.FullName -ne $versionedApkPath) {
    Copy-Item $apk.FullName $versionedApkPath -Force
    Write-Output "Versioned APK: $versionedApkPath"
}

if ($InstallOnDevice) {
    if (-not (Test-Path $adb)) {
        throw "adb.exe was not found at $adb."
    }

    & $adb install -r $apk.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "adb install failed."
    }
}
