param(
    [string]$AndroidSdkDirectory = "C:\Android\sdk",
    [string]$Configuration = "Debug",
    [switch]$InstallDependencies,
    [switch]$InstallOnDevice
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "SpaceBurst.Android\SpaceBurst.Android.csproj"
$framework = "net8.0-android34.0"
$contentObj = Join-Path $PSScriptRoot "SpaceBurst\Content\obj\Android"
$contentOutput = Join-Path $PSScriptRoot "SpaceBurst\Content\bin\Android\Content"
$mgcb = Join-Path $PSScriptRoot "SpaceBurst\Content\Content.mgcb"
$apkDirectory = Join-Path $PSScriptRoot "SpaceBurst.Android\bin\$Configuration\$framework"
$androidObj = Join-Path $PSScriptRoot "SpaceBurst.Android\obj"
$androidBin = Join-Path $PSScriptRoot "SpaceBurst.Android\bin\$Configuration"
$adb = Join-Path $AndroidSdkDirectory "platform-tools\adb.exe"
$env:ANDROID_SDK_ROOT = $AndroidSdkDirectory
$env:ANDROID_HOME = $AndroidSdkDirectory

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed."
}

if ($InstallDependencies -or -not (Test-Path $adb)) {
    dotnet build $project `
        -t:InstallAndroidDependencies `
        -f $framework `
        -p:AcceptAndroidSdkLicenses=True `
        -v minimal

    if ($LASTEXITCODE -ne 0) {
        throw "Android SDK installation failed."
    }
}

if (Test-Path $contentObj) {
    cmd /c rmdir /s /q "$contentObj"
}

if (Test-Path $androidObj) {
    cmd /c rmdir /s /q "$androidObj"
}

if (Test-Path $androidBin) {
    cmd /c rmdir /s /q "$androidBin"
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

if ($InstallOnDevice) {
    if (-not (Test-Path $adb)) {
        throw "adb.exe was not found at $adb."
    }

    & $adb install -r $apk.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "adb install failed."
    }
}
