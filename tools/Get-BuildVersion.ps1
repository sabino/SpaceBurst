param(
    [string]$BaseVersion = "1.1",
    [string]$Commit = "",
    [datetime]$UtcNow = [datetime]::UtcNow
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Commit)) {
    $Commit = (git rev-parse --short HEAD).Trim()
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
    throw "Unable to determine git commit for version stamping."
}

$versionParts = $BaseVersion.Split('.')
if ($versionParts.Length -lt 2) {
    throw "BaseVersion must contain at least major and minor components."
}

$major = [int]$versionParts[0]
$minor = [int]$versionParts[1]
$year = $UtcNow.Year % 100
$dayOfYear = $UtcNow.DayOfYear
$hour = $UtcNow.Hour
$minute = $UtcNow.Minute
$second = $UtcNow.Second

$buildStamp = "{0:D2}{1:D3}{2:D2}{3:D2}{4:D2}" -f $year, $dayOfYear, $hour, $minute, $second
$displayVersion = "{0}.{1}.{2}" -f $major, $minor, $buildStamp
$buildVersion = "{0}.{1}.{2}.{3}" -f $major, $minor, ($year * 1000 + $dayOfYear), ($hour * 100 + $minute)
$fileVersion = $buildVersion
$informationalVersion = "{0}+{1}" -f $displayVersion, $Commit
$artifactTag = "v{0}" -f $displayVersion
$androidVersionEpoch = [datetime]::SpecifyKind([datetime]"2025-01-01T00:00:00", [System.DateTimeKind]::Utc)
$androidVersionCode = [int][Math]::Floor(($UtcNow - $androidVersionEpoch).TotalSeconds)

$result = [ordered]@{
    BaseVersion = $BaseVersion
    DisplayVersion = $displayVersion
    BuildVersion = $buildVersion
    FileVersion = $fileVersion
    InformationalVersion = $informationalVersion
    AndroidVersionCode = $androidVersionCode
    ArtifactTag = $artifactTag
    Commit = $Commit
}

$result | ConvertTo-Json -Compress
