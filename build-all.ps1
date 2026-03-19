param(
    [string]$AndroidSdkDirectory = "",
    [string]$WindowsRuntimeIdentifier = "win-x64",
    [string]$WindowsConfiguration = "Release",
    [string]$AndroidConfiguration = "Debug",
    [switch]$SkipAndroid,
    [switch]$InstallAndroidDependencies,
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

$root = $PSScriptRoot
$gameProject = Join-Path $root "SpaceBurst\SpaceBurst.csproj"
$levelToolProject = Join-Path $root "SpaceBurst.LevelTool\SpaceBurst.LevelTool.csproj"
$AndroidSdkDirectory = Resolve-AndroidSdkDirectory $AndroidSdkDirectory
$env:ANDROID_SDK_ROOT = $AndroidSdkDirectory
$env:ANDROID_HOME = $AndroidSdkDirectory
$commit = (git -C $root rev-parse --short HEAD).Trim()
if (-not $commit) {
    throw "Unable to determine git commit."
}

$artifactsRoot = Join-Path $root "artifacts\release\bundle-$commit"
$latestRoot = Join-Path $root "artifacts\release\latest"
$gameOutput = Join-Path $artifactsRoot "game-$WindowsRuntimeIdentifier"
$levelToolOutput = Join-Path $artifactsRoot "leveltool-$WindowsRuntimeIdentifier"
$androidOutput = Join-Path $artifactsRoot "android-apk"
$contentBin = Join-Path $root "SpaceBurst\Content\bin"
$contentObj = Join-Path $root "SpaceBurst\Content\obj"

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

Invoke-Step "Cleaning generated content outputs" {
    if (Test-Path $contentBin) {
        Remove-Item $contentBin -Recurse -Force
    }

    if (Test-Path $contentObj) {
        Remove-Item $contentObj -Recurse -Force
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
                -p:AndroidSdkDirectory=$AndroidSdkDirectory `
                -v minimal

            if ($LASTEXITCODE -ne 0) {
                throw "Android dependency installation failed."
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
            /workingDir:"$(Join-Path $root 'SpaceBurst\Content\')"

        if ($LASTEXITCODE -ne 0) {
            throw "Android content build failed."
        }

        dotnet build $androidProject `
            -f $framework `
            -c $AndroidConfiguration `
            -p:AndroidSdkDirectory=$AndroidSdkDirectory `
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

if (Test-Path $latestRoot) {
    Remove-Item $latestRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $latestRoot | Out-Null
Copy-Item (Join-Path $artifactsRoot '*') $latestRoot -Recurse -Force

$latestSummaryPath = Join-Path $latestRoot "build-summary.txt"
$latestSummary = [System.Collections.Generic.List[string]]::new()
$latestSummary.Add("Commit: $commit")
$latestSummary.Add("Game EXE: $(Join-Path $latestRoot "game-$WindowsRuntimeIdentifier\SpaceBurst.exe")")
$latestSummary.Add("Level Tool EXE: $(Join-Path $latestRoot "leveltool-$WindowsRuntimeIdentifier\SpaceBurst.LevelTool.exe")")
if (-not $SkipAndroid) {
    $latestApk = Get-ChildItem (Join-Path $latestRoot "android-apk") -Filter "*.apk" | Select-Object -First 1
    if ($latestApk) {
        $latestSummary.Add("Android APK: $($latestApk.FullName)")
    }
}
$latestSummary | Set-Content -Path $latestSummaryPath

Write-Host ""
Write-Host "Artifacts ready:"
$summary | ForEach-Object { Write-Host $_ }
Write-Host "Summary: $summaryPath"
Write-Host "Latest: $latestRoot"
