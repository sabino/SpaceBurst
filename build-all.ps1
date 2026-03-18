param(
    [string]$AndroidSdkDirectory = "C:\Android\sdk",
    [string]$WindowsRuntimeIdentifier = "win-x64",
    [string]$WindowsConfiguration = "Release",
    [string]$AndroidConfiguration = "Debug",
    [switch]$SkipAndroid,
    [switch]$InstallAndroidDependencies,
    [switch]$InstallOnDevice
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$gameProject = Join-Path $root "SpaceBurst\SpaceBurst.csproj"
$levelToolProject = Join-Path $root "SpaceBurst.LevelTool\SpaceBurst.LevelTool.csproj"
$commit = (git -C $root rev-parse --short HEAD).Trim()
if (-not $commit) {
    throw "Unable to determine git commit."
}

$artifactsRoot = Join-Path $root "artifacts\release\bundle-$commit"
$gameOutput = Join-Path $artifactsRoot "game-$WindowsRuntimeIdentifier"
$levelToolOutput = Join-Path $artifactsRoot "leveltool-$WindowsRuntimeIdentifier"
$androidOutput = Join-Path $artifactsRoot "android-apk"

function Invoke-Step {
    param(
        [string]$Label,
        [scriptblock]$Action
    )

    Write-Host "==> $Label"
    & $Action
}

if (Test-Path $artifactsRoot) {
    Remove-Item $artifactsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $artifactsRoot | Out-Null

Invoke-Step "Restoring local tools" {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed."
    }
}

Invoke-Step "Building solution" {
    dotnet build (Join-Path $root "SpaceBurst.sln") -c $WindowsConfiguration
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed."
    }
}

Invoke-Step "Publishing Windows game single-file exe" {
    dotnet publish $gameProject `
        -c $WindowsConfiguration `
        -r $WindowsRuntimeIdentifier `
        -p:SelfContained=true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $gameOutput

    if ($LASTEXITCODE -ne 0) {
        throw "Game publish failed."
    }
}

Invoke-Step "Publishing Windows level editor single-file exe" {
    dotnet publish $levelToolProject `
        -c $WindowsConfiguration `
        -r $WindowsRuntimeIdentifier `
        -p:SelfContained=true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $levelToolOutput

    if ($LASTEXITCODE -ne 0) {
        throw "Level editor publish failed."
    }
}

if (-not $SkipAndroid) {
    Invoke-Step "Building Android APK" {
        $androidProject = Join-Path $root "SpaceBurst.Android\SpaceBurst.Android.csproj"
        $framework = "net8.0-android34.0"
        $contentObj = Join-Path $root "SpaceBurst\Content\obj\Android"
        $contentOutput = Join-Path $root "SpaceBurst\Content\bin\Android\Content"
        $mgcb = Join-Path $root "SpaceBurst\Content\Content.mgcb"
        $androidObj = Join-Path $root "SpaceBurst.Android\obj"
        $androidBin = Join-Path $root "SpaceBurst.Android\bin\$AndroidConfiguration"
        $adb = Join-Path $AndroidSdkDirectory "platform-tools\adb.exe"
        $env:ANDROID_SDK_ROOT = $AndroidSdkDirectory
        $env:ANDROID_HOME = $AndroidSdkDirectory

        if ($InstallAndroidDependencies -or -not (Test-Path $adb)) {
            dotnet build $androidProject `
                -t:InstallAndroidDependencies `
                -f $framework `
                -p:AcceptAndroidSdkLicenses=True `
                -v minimal

            if ($LASTEXITCODE -ne 0) {
                throw "Android dependency installation failed."
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
            /workingDir:"$(Join-Path $root 'SpaceBurst\Content\')"

        if ($LASTEXITCODE -ne 0) {
            throw "Android content build failed."
        }

        dotnet build $androidProject `
            -f $framework `
            -c $AndroidConfiguration `
            -v minimal

        if ($LASTEXITCODE -ne 0) {
            throw "Android build failed."
        }
    }

    $apkSource = Get-ChildItem (Join-Path $root "SpaceBurst.Android\bin\$AndroidConfiguration\net8.0-android34.0") -Filter "*-Signed.apk" | Select-Object -First 1
    if (-not $apkSource) {
        $apkSource = Get-ChildItem (Join-Path $root "SpaceBurst.Android\bin\$AndroidConfiguration\net8.0-android34.0") -Filter "*.apk" | Select-Object -First 1
    }

    if (-not $apkSource) {
        throw "Android build completed but no APK was found."
    }

    New-Item -ItemType Directory -Path $androidOutput | Out-Null
    Copy-Item $apkSource.FullName (Join-Path $androidOutput $apkSource.Name) -Force

    if ($InstallOnDevice) {
        $adb = Join-Path $AndroidSdkDirectory "platform-tools\adb.exe"
        if (-not (Test-Path $adb)) {
            throw "adb.exe was not found at $adb."
        }

        & $adb install -r $apkSource.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "adb install failed."
        }
    }
}

$summaryPath = Join-Path $artifactsRoot "build-summary.txt"
$summary = [System.Collections.Generic.List[string]]::new()
$summary.Add("Commit: $commit")
$summary.Add("Game EXE: $(Join-Path $gameOutput 'SpaceBurst.exe')")
$summary.Add("Level Tool EXE: $(Join-Path $levelToolOutput 'SpaceBurst.LevelTool.exe')")
if (-not $SkipAndroid) {
    $apkCopied = Get-ChildItem $androidOutput -Filter "*.apk" | Select-Object -First 1
    $summary.Add("Android APK: $($apkCopied.FullName)")
}
$summary | Set-Content -Path $summaryPath

Write-Host ""
Write-Host "Artifacts ready:"
$summary | ForEach-Object { Write-Host $_ }
Write-Host "Summary: $summaryPath"
