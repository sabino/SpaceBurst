$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$levelsDir = Join-Path $repoRoot "Levels"

if (-not (Test-Path $levelsDir))
{
    New-Item -ItemType Directory -Path $levelsDir | Out-Null
}

function Write-JsonFile
{
    param(
        [string]$Path,
        $Object
    )

    $json = $Object | ConvertTo-Json -Depth 16
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine)
}

function New-Sprite
{
    param(
        [string]$Id,
        [int]$PixelScale,
        [string]$PrimaryColor,
        [string]$SecondaryColor,
        [string]$AccentColor,
        [string[]]$Rows,
        [string[]]$CoreRows
    )

    return [ordered]@{
        Id = $Id
        PixelScale = $PixelScale
        PrimaryColor = $PrimaryColor
        SecondaryColor = $SecondaryColor
        AccentColor = $AccentColor
        Rows = $Rows
        VitalCore = [ordered]@{
            Rows = $CoreRows
        }
    }
}

function New-Archetype
{
    param(
        [string]$Id,
        [string]$DisplayName,
        [double]$RenderScale,
        [double]$MoveSpeed,
        [int]$HitPoints,
        [int]$ScoreValue,
        [double]$SpawnLeadDistance,
        [double]$FireIntervalSeconds,
        [double]$MovementAmplitude,
        [double]$MovementFrequency,
        [string]$MovePattern,
        [string]$FirePattern,
        $Sprite,
        [int]$ContactDamage,
        [int]$ProjectileDamage,
        [int]$DamageRadius,
        [int]$IntegrityThresholdPercent,
        [bool]$DestroyOnCoreBreach = $true,
        [bool]$ShowDurabilityBar = $false
    )

    return [ordered]@{
        Id = $Id
        DisplayName = $DisplayName
        RenderScale = $RenderScale
        MoveSpeed = $MoveSpeed
        HitPoints = $HitPoints
        ScoreValue = $ScoreValue
        SpawnLeadDistance = $SpawnLeadDistance
        FireIntervalSeconds = $FireIntervalSeconds
        MovementAmplitude = $MovementAmplitude
        MovementFrequency = $MovementFrequency
        DestroyOnCoreBreach = $DestroyOnCoreBreach
        ShowDurabilityBar = $ShowDurabilityBar
        MovePattern = $MovePattern
        FirePattern = $FirePattern
        Sprite = $Sprite
        DamageMask = [ordered]@{
            ContactDamage = $ContactDamage
            ProjectileDamage = $ProjectileDamage
            DamageRadius = $DamageRadius
            IntegrityThresholdPercent = $IntegrityThresholdPercent
        }
    }
}

function New-Group
{
    param(
        [string]$ArchetypeId,
        [double]$StartSeconds,
        [int]$Lane,
        [int]$Count,
        [double]$SpawnLeadDistance,
        [double]$SpawnIntervalSeconds,
        [double]$SpacingX,
        [double]$SpeedMultiplier,
        [string]$MovePatternOverride,
        [string]$FirePatternOverride,
        [double]$Amplitude,
        [double]$Frequency,
        [double]$TargetY = -1
    )

    $group = [ordered]@{
        ArchetypeId = $ArchetypeId
        StartSeconds = [math]::Round($StartSeconds, 2)
        Lane = $Lane
        Count = $Count
        SpawnLeadDistance = [math]::Round($SpawnLeadDistance, 2)
        SpawnIntervalSeconds = [math]::Round($SpawnIntervalSeconds, 2)
        SpacingX = [math]::Round($SpacingX, 2)
        SpeedMultiplier = [math]::Round($SpeedMultiplier, 2)
        MovePatternOverride = $MovePatternOverride
        FirePatternOverride = $FirePatternOverride
        Amplitude = [math]::Round($Amplitude, 2)
        Frequency = [math]::Round($Frequency, 2)
    }

    if ($TargetY -ge 0)
    {
        $group.TargetY = [math]::Round($TargetY, 2)
    }

    return $group
}

function New-Section
{
    param(
        [string]$Label,
        [double]$StartSeconds,
        [double]$DurationSeconds,
        [bool]$Checkpoint,
        [object[]]$Groups,
        [double]$PowerDropBonusChance = 0,
        [double]$ScrollMultiplier = 1,
        [double]$EnemySpeedMultiplier = 1,
        $Mood = $null,
        [object[]]$EventWindows = @()
    )

    return [ordered]@{
        Label = $Label
        StartSeconds = [math]::Round($StartSeconds, 2)
        DurationSeconds = [math]::Round($DurationSeconds, 2)
        Checkpoint = $Checkpoint
        PowerDropBonusChance = [math]::Round($PowerDropBonusChance, 3)
        ScrollMultiplier = [math]::Round($ScrollMultiplier, 2)
        EnemySpeedMultiplier = [math]::Round($EnemySpeedMultiplier, 2)
        Mood = $Mood
        EventWindows = @($EventWindows)
        Groups = $Groups
    }
}

function Get-StageName
{
    param([int]$Stage)

    $standardNames = @(
        "Opening Drift", "First Corridor", "Blue Crosswind", "Wing Pair", "Three Lane Heat", "Signal Arc", "Bridge Sweep", "Pressure Grid", "Edge Lesson",
        "Afterburn Line", "Twin Currents", "Glass Teeth", "Ion Relay", "Carrier Pass", "Burning Turn", "Backfire Lane", "Stacked Wake", "Mirror Run", "Gate Chorus",
        "Steel Fall", "Red Channel", "Lockstep", "Fracture Line", "Apex Field", "Turret Rain", "Core Weave", "Shift Corridor", "Hot Approach", "Strike Rehearsal",
        "Molten Span", "Long Drift", "Cracked Ion", "Outer Teeth", "Carrier Knot", "Split Furnace", "Flare Lift", "Cold Mirror", "Wake Pressure", "Anvil Flight",
        "Night Current", "Heavy Sky", "Dark Relay", "Flank Burn", "Breakwater", "Final Spiral", "Vector Scar", "Deep Pulse", "Late Horizon", "The Last Gate"
    )

    $bossNames = @{
        10 = "Boss 10: Destroyer Prime"
        20 = "Boss 20: Walker Matron"
        30 = "Boss 30: Destroyer Mk II"
        40 = "Boss 40: Walker Mk II"
        50 = "Boss 50: Final Core"
    }

    if ($bossNames.ContainsKey($Stage))
    {
        return $bossNames[$Stage]
    }

    return $standardNames[$Stage - 1]
}

function New-Mood
{
    param(
        [string]$PrimaryColor,
        [string]$SecondaryColor,
        [string]$AccentColor,
        [string]$GlowColor,
        [double]$StarDensity,
        [double]$DustDensity,
        [double]$LightIntensity,
        [double]$PlanetPresence,
        [double]$Contrast
    )

    return [ordered]@{
        PrimaryColor = $PrimaryColor
        SecondaryColor = $SecondaryColor
        AccentColor = $AccentColor
        GlowColor = $GlowColor
        StarDensity = [math]::Round($StarDensity, 2)
        DustDensity = [math]::Round($DustDensity, 2)
        LightIntensity = [math]::Round($LightIntensity, 2)
        PlanetPresence = [math]::Round($PlanetPresence, 2)
        Contrast = [math]::Round($Contrast, 2)
    }
}

function New-EventWindow
{
    param(
        [string]$EventType,
        [double]$StartSeconds,
        [double]$DurationSeconds,
        [double]$Weight,
        [double]$Intensity
    )

    return [ordered]@{
        EventType = $EventType
        StartSeconds = [math]::Round($StartSeconds, 2)
        DurationSeconds = [math]::Round($DurationSeconds, 2)
        Weight = [math]::Round($Weight, 2)
        Intensity = [math]::Round($Intensity, 2)
    }
}

function Get-StageDensityBand
{
    param([int]$Stage)

    if ($Stage -le 5) { return 0 }
    if ($Stage -le 15) { return 1 }
    if ($Stage -le 30) { return 2 }
    return 3
}

function Get-StageBaseScrollSpeed
{
    param([int]$Stage)

    return [math]::Round([math]::Min(186, 118 + ($Stage - 1) * 1.4), 2)
}

function Get-SectionScrollMultiplier
{
    param(
        [int]$SectionIndex,
        [int]$SectionCount
    )

    $curve = if ($SectionCount -ge 5) { @(0.90, 0.98, 1.06, 1.12, 1.18) } else { @(0.90, 0.99, 1.09, 1.18) }
    return $curve[[math]::Min($SectionIndex, $curve.Count - 1)]
}

function Get-SectionEnemySpeedMultiplier
{
    param(
        [int]$SectionIndex,
        [int]$SectionCount
    )

    $curve = if ($SectionCount -ge 5) { @(0.82, 0.94, 1.00, 1.12, 1.18) } else { @(0.82, 0.96, 1.08, 1.18) }
    return $curve[[math]::Min($SectionIndex, $curve.Count - 1)]
}

function Get-ThemeMood
{
    param(
        [string]$Theme,
        [int]$Stage,
        [int]$SectionIndex = 0,
        [int]$SectionCount = 5,
        [bool]$Boss = $false
    )

    $band = Get-StageDensityBand -Stage $Stage
    $progress = if ($SectionCount -le 1) { 0 } else { $SectionIndex / [double]($SectionCount - 1) }

    switch ($Theme)
    {
        "Ion"
        {
            $mood = New-Mood -PrimaryColor "#07151F" -SecondaryColor "#163B47" -AccentColor "#73F3E8" -GlowColor "#A7E8FF" -StarDensity 1.05 -DustDensity 0.92 -LightIntensity 0.62 -PlanetPresence 0.42 -Contrast 0.88
        }
        "Solar"
        {
            $mood = New-Mood -PrimaryColor "#1A0E10" -SecondaryColor "#46303A" -AccentColor "#FFB057" -GlowColor "#FFD99E" -StarDensity 0.92 -DustDensity 1.12 -LightIntensity 0.76 -PlanetPresence 0.58 -Contrast 0.94
        }
        default
        {
            $mood = New-Mood -PrimaryColor "#0C1122" -SecondaryColor "#1C3055" -AccentColor "#6EC1FF" -GlowColor "#F6C674" -StarDensity 1.00 -DustDensity 1.00 -LightIntensity 0.70 -PlanetPresence 0.55 -Contrast 0.80
        }
    }

    $mood.StarDensity = [math]::Round([math]::Min(1.45, $mood.StarDensity + $progress * 0.10 + $band * 0.04), 2)
    $mood.DustDensity = [math]::Round([math]::Min(1.35, $mood.DustDensity + $progress * 0.08 + $band * 0.03), 2)
    $mood.LightIntensity = [math]::Round([math]::Min(1.18, $mood.LightIntensity + $progress * 0.12 + ($Boss ? 0.10 : 0)), 2)
    $mood.PlanetPresence = [math]::Round([math]::Max(0.16, $mood.PlanetPresence - $progress * 0.08 + ($Boss ? 0.06 : 0)), 2)
    $mood.Contrast = [math]::Round([math]::Min(1.18, $mood.Contrast + $band * 0.04 + ($Boss ? 0.06 : 0)), 2)
    return $mood
}

function Get-SectionEventWindows
{
    param(
        [int]$Stage,
        [int]$SectionIndex,
        [int]$SectionCount,
        [bool]$IsBossStage = $false
    )

    $band = Get-StageDensityBand -Stage $Stage
    $windows = @()

    if ($IsBossStage)
    {
        if ($Stage -ge 20 -and $SectionIndex -eq 0)
        {
            $windows += New-EventWindow -EventType "DebrisDrift" -StartSeconds 2.3 -DurationSeconds 3.2 -Weight 1 -Intensity (0.78 + $band * 0.06)
        }

        return ,@($windows)
    }

    if ($Stage -le 2 -and $SectionIndex -eq 0)
    {
        return ,@($windows)
    }

    $types = @("DebrisDrift", "CometSwarm", "MeteorShower", "SolarFlare")
    $type = $types[($Stage + $SectionIndex) % $types.Count]
    $startSeconds = 2.2 + (($Stage + $SectionIndex) % 3) * 0.55
    $durationSeconds = if ($type -eq "SolarFlare") { 2.6 } else { 3.0 + [math]::Min(1.2, $band * 0.35 + $SectionIndex * 0.15) }
    $intensity = [math]::Min(1.35, 0.74 + $band * 0.12 + $SectionIndex * 0.08)
    $windows += New-EventWindow -EventType $type -StartSeconds $startSeconds -DurationSeconds $durationSeconds -Weight 1 -Intensity $intensity

    if ($band -ge 2 -and $SectionIndex -ge 2)
    {
        $secondary = if ($type -eq "MeteorShower") { "DebrisDrift" } else { "MeteorShower" }
        $windows += New-EventWindow -EventType $secondary -StartSeconds ([math]::Min(7.2, $startSeconds + 3.0)) -DurationSeconds 2.4 -Weight 0.7 -Intensity ([math]::Max(0.65, $intensity - 0.12))
    }

    return ,@($windows)
}

$archetypeCatalog = [ordered]@{
    Archetypes = @(
        (New-Archetype -Id "Walker" -DisplayName "Walker" -RenderScale 1 -MoveSpeed 190 -HitPoints 6 -ScoreValue 80 -SpawnLeadDistance 220 -FireIntervalSeconds 0 -MovementAmplitude 28 -MovementFrequency 1 -MovePattern "StraightFlyIn" -FirePattern "None" -Sprite (New-Sprite -Id "WalkerSprite" -PixelScale 4 -PrimaryColor "#CFEFFF" -SecondaryColor "#65A7C5" -AccentColor "#F4B860" -Rows @("....##......","..######....",".##++++##...","##++CC++##..","##++++++##..",".##+**+##...","..######....","....##......","............") -CoreRows @("............","............","............","....XXXX....","....XXXX....","............","............","............","............")) -ContactDamage 2 -ProjectileDamage 1 -DamageRadius 1 -IntegrityThresholdPercent 55 -DestroyOnCoreBreach $true -ShowDurabilityBar $false),
        (New-Archetype -Id "Interceptor" -DisplayName "Interceptor" -RenderScale 0.95 -MoveSpeed 250 -HitPoints 4 -ScoreValue 90 -SpawnLeadDistance 260 -FireIntervalSeconds 0 -MovementAmplitude 60 -MovementFrequency 1.2 -MovePattern "SineWave" -FirePattern "None" -Sprite (New-Sprite -Id "InterceptorSprite" -PixelScale 4 -PrimaryColor "#FFF2D6" -SecondaryColor "#F2AE49" -AccentColor "#FF7A59" -Rows @("...##.....",".######...","##++++##..","++++CC++++","##++++##..",".######...","...##.....") -CoreRows @("..........","..........","..........","....XX....","..........","..........","..........")) -ContactDamage 2 -ProjectileDamage 1 -DamageRadius 1 -IntegrityThresholdPercent 55 -DestroyOnCoreBreach $true -ShowDurabilityBar $false),
        (New-Archetype -Id "Destroyer" -DisplayName "Destroyer" -RenderScale 1 -MoveSpeed 170 -HitPoints 10 -ScoreValue 125 -SpawnLeadDistance 300 -FireIntervalSeconds 1.7 -MovementAmplitude 52 -MovementFrequency 0.9 -MovePattern "Dive" -FirePattern "ForwardPulse" -Sprite (New-Sprite -Id "DestroyerSprite" -PixelScale 4 -PrimaryColor "#F2DCE5" -SecondaryColor "#B56A8A" -AccentColor "#FF9B54" -Rows @(".....####.......","...########.....",".###++++++###...","###++CCCC++###..","###++++++++###**",".###++++++###...","...########.....",".....####.......","................") -CoreRows @("................","................","................",".....XXXX.......",".....XXXX.......","................","................","................","................")) -ContactDamage 3 -ProjectileDamage 1 -DamageRadius 1 -IntegrityThresholdPercent 45 -DestroyOnCoreBreach $true -ShowDurabilityBar $true),
        (New-Archetype -Id "Carrier" -DisplayName "Carrier" -RenderScale 1 -MoveSpeed 145 -HitPoints 12 -ScoreValue 160 -SpawnLeadDistance 320 -FireIntervalSeconds 1.4 -MovementAmplitude 40 -MovementFrequency 0.8 -MovePattern "TurretCarrier" -FirePattern "SpreadPulse" -Sprite (New-Sprite -Id "CarrierSprite" -PixelScale 4 -PrimaryColor "#DDF0D3" -SecondaryColor "#7AAA6E" -AccentColor "#F4B860" -Rows @("......####........","....########......","..###++##++###....",".###+++CC+++###...","###++++CC++++###..","###+++****+++###..",".###++++++++###...","..###++++++###....","....########......","......####........","..................") -CoreRows @("..................","..................","..................","......XXXX........","......XXXX........","..................","..................","..................","..................","..................","..................")) -ContactDamage 3 -ProjectileDamage 1 -DamageRadius 1 -IntegrityThresholdPercent 45 -DestroyOnCoreBreach $true -ShowDurabilityBar $true),
        (New-Archetype -Id "Bulwark" -DisplayName "Bulwark" -RenderScale 1 -MoveSpeed 135 -HitPoints 16 -ScoreValue 190 -SpawnLeadDistance 340 -FireIntervalSeconds 1.2 -MovementAmplitude 36 -MovementFrequency 0.7 -MovePattern "RetreatBackfire" -FirePattern "ForwardPulse" -Sprite (New-Sprite -Id "BulwarkSprite" -PixelScale 4 -PrimaryColor "#E9E3D2" -SecondaryColor "#A98F6C" -AccentColor "#FFB347" -Rows @(".....######.......","...##########.....",".###++++++++###...","###++CCCCCC++###..","###++++++++++###..","###+++****+++###..",".###++++++++###...","...##########.....",".....######.......","..................") -CoreRows @("..................","..................","..................",".....XXXXXX.......",".....XXXXXX.......","..................","..................","..................","..................","..................")) -ContactDamage 4 -ProjectileDamage 2 -DamageRadius 1 -IntegrityThresholdPercent 38 -DestroyOnCoreBreach $true -ShowDurabilityBar $true),
        (New-Archetype -Id "BossDestroyer" -DisplayName "Destroyer Prime" -RenderScale 1 -MoveSpeed 130 -HitPoints 90 -ScoreValue 600 -SpawnLeadDistance 360 -FireIntervalSeconds 0.85 -MovementAmplitude 90 -MovementFrequency 0.8 -MovePattern "BossCharge" -FirePattern "BossFan" -Sprite (New-Sprite -Id "BossDestroyerSprite" -PixelScale 4 -PrimaryColor "#F6E4EA" -SecondaryColor "#C06A8D" -AccentColor "#FFB347" -Rows @(".......######...........",".....##########.........","...####++++++####.......",".####++++CC++++####.....","####++++CCCC++++####....","####++++CCCC++++####.**.","####++++CCCC++++####....",".####++++CC++++####.....","...####++++++####.......",".....##########.........",".......######...........","........................") -CoreRows @("........................","........................","........................","........XXXX............",".......XXXXXX...........",".......XXXXXX...........",".......XXXXXX...........","........XXXX............","........................","........................","........................","........................")) -ContactDamage 5 -ProjectileDamage 2 -DamageRadius 1 -IntegrityThresholdPercent 10 -DestroyOnCoreBreach $false -ShowDurabilityBar $false),
        (New-Archetype -Id "BossWalker" -DisplayName "Walker Matron" -RenderScale 1 -MoveSpeed 120 -HitPoints 105 -ScoreValue 700 -SpawnLeadDistance 360 -FireIntervalSeconds 0.78 -MovementAmplitude 96 -MovementFrequency 0.75 -MovePattern "BossOrbit" -FirePattern "BossFan" -Sprite (New-Sprite -Id "BossWalkerSprite" -PixelScale 4 -PrimaryColor "#E1F7D7" -SecondaryColor "#78B46A" -AccentColor "#FFD166" -Rows @("........####............","......########..........","....####++++####........","..####+++CC+++####......",".####++++CCCC++++####...","####++++++CC++++++####..","####++++******++++####..",".####++++CCCC++++####...","..####+++CC+++####......","....####++++####........","......########..........","........####............") -CoreRows @("........................","........................","........................",".........XX.............","........XXXX............","........XXXX............","........................","........XXXX............",".........XX.............","........................","........................","........................")) -ContactDamage 5 -ProjectileDamage 2 -DamageRadius 1 -IntegrityThresholdPercent 10 -DestroyOnCoreBreach $false -ShowDurabilityBar $false),
        (New-Archetype -Id "BossFinal" -DisplayName "Final Core" -RenderScale 1 -MoveSpeed 135 -HitPoints 135 -ScoreValue 1000 -SpawnLeadDistance 380 -FireIntervalSeconds 0.7 -MovementAmplitude 110 -MovementFrequency 0.7 -MovePattern "BossOrbit" -FirePattern "BossFan" -Sprite (New-Sprite -Id "BossFinalSprite" -PixelScale 4 -PrimaryColor "#F1EEDC" -SecondaryColor "#8AA7D1" -AccentColor "#FF9F59" -Rows @(".........######...........","......############........","....####++++++++####......","..####+++++CC+++++####....",".####+++++CCCC+++++####...","####+++++CCCCCC+++++####..","####++++***CC***++++####..","####+++++CCCCCC+++++####..",".####+++++CCCC+++++####...","..####+++++CC+++++####....","....####++++++++####......","......############........",".........######...........") -CoreRows @("..........................","..........................","..........................","..........XXXX............",".........XXXXXX...........","........XXXXXXXX..........","..........XXXX............","........XXXXXXXX..........",".........XXXXXX...........","..........XXXX............","..........................","..........................","..........................")) -ContactDamage 6 -ProjectileDamage 2 -DamageRadius 1 -IntegrityThresholdPercent 10 -DestroyOnCoreBreach $false -ShowDurabilityBar $false)
    )
}

Write-JsonFile -Path (Join-Path $levelsDir "enemy-archetypes.json") -Object $archetypeCatalog

$themes = @("Nebula", "Ion", "Solar")
$moveTemplates = @("StraightFlyIn", "SineWave", "Dive", "RetreatBackfire", "TurretCarrier")
$fireTemplates = @("None", "ForwardPulse", "SpreadPulse", "AimedShot")

for ($stage = 1; $stage -le 50; $stage++)
{
    $chapter = [int][math]::Ceiling($stage / 10)
    $densityBand = Get-StageDensityBand -Stage $stage
    $isBossStage = ($stage % 10 -eq 0)
    $theme = $themes[($stage - 1) % $themes.Count]
    $name = Get-StageName -Stage $stage
    $sections = @()
    $checkpointMarkers = @()
    $baseScrollSpeed = Get-StageBaseScrollSpeed -Stage $stage
    $stageMood = Get-ThemeMood -Theme $theme -Stage $stage -SectionIndex 0 -SectionCount 5 -Boss:$isBossStage

    if ($isBossStage)
    {
        $warmupCount = if ($stage -eq 50) { 3 } else { 2 }
        for ($sectionIndex = 0; $sectionIndex -lt $warmupCount; $sectionIndex++)
        {
            $startSeconds = 0.75 + $sectionIndex * 11.25
            $checkpoint = ($sectionIndex -eq ($warmupCount - 1))
            $laneBase = ($stage + $sectionIndex) % 5
            $sectionMood = Get-ThemeMood -Theme $theme -Stage $stage -SectionIndex $sectionIndex -SectionCount $warmupCount -Boss $true
            $sectionEvents = Get-SectionEventWindows -Stage $stage -SectionIndex $sectionIndex -SectionCount $warmupCount -IsBossStage $true
            $groups = @(
                (New-Group -ArchetypeId "Interceptor" -StartSeconds 0 -Lane $laneBase -Count (5 + $chapter + $sectionIndex) -SpawnLeadDistance 250 -SpawnIntervalSeconds 0.14 -SpacingX 68 -SpeedMultiplier (1.08 + $chapter * 0.05) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (50 + $sectionIndex * 12) -Frequency 1.18),
                (New-Group -ArchetypeId "Walker" -StartSeconds 3.2 -Lane (($laneBase + 3) % 5) -Count (3 + $chapter + $sectionIndex) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.2 -SpacingX 80 -SpeedMultiplier (1.02 + $chapter * 0.04) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 28 -Frequency 1),
                (New-Group -ArchetypeId ($(if ($sectionIndex % 2 -eq 0) { "Destroyer" } else { "Carrier" })) -StartSeconds 6.2 -Lane (($laneBase + 2) % 5) -Count (2 + [math]::Min(2, $chapter)) -SpawnLeadDistance 318 -SpawnIntervalSeconds 0.24 -SpacingX 96 -SpeedMultiplier (1 + $chapter * 0.04) -MovePatternOverride ($(if ($sectionIndex % 2 -eq 0) { "Dive" } else { "TurretCarrier" })) -FirePatternOverride ($(if ($chapter -ge 3) { "ForwardPulse" } else { "None" })) -Amplitude 44 -Frequency 0.9)
            )

            $section = New-Section -Label ("Warmup {0}" -f ($sectionIndex + 1)) -StartSeconds $startSeconds -DurationSeconds 10.7 -Checkpoint $checkpoint -PowerDropBonusChance (0.02 + $sectionIndex * 0.01 + $densityBand * 0.005) -ScrollMultiplier (Get-SectionScrollMultiplier -SectionIndex $sectionIndex -SectionCount $warmupCount) -EnemySpeedMultiplier (Get-SectionEnemySpeedMultiplier -SectionIndex $sectionIndex -SectionCount $warmupCount) -Mood $sectionMood -EventWindows $sectionEvents -Groups $groups
            $sections += $section
            if ($checkpoint)
            {
                $checkpointMarkers += $section.StartSeconds
            }
        }

        switch ($stage)
        {
            10 { $boss = [ordered]@{ Type = "DestroyerBoss"; DisplayName = "Destroyer Prime"; ArchetypeId = "BossDestroyer"; IntroSeconds = 1.2; TargetY = 0.48; ArenaScrollSpeed = 58; HitPoints = 90; AllowRandomEvents = $false; HazardOverrides = @(); MoodOverride = (Get-ThemeMood -Theme $theme -Stage $stage -SectionIndex 1 -SectionCount 2 -Boss $true); PhaseThresholds = @(0.75, 0.5, 0.25); MovePattern = "BossCharge"; FirePattern = "BossFan" } }
            20 { $boss = [ordered]@{ Type = "WalkerBoss"; DisplayName = "Walker Matron"; ArchetypeId = "BossWalker"; IntroSeconds = 1.2; TargetY = 0.5; ArenaScrollSpeed = 52; HitPoints = 105; AllowRandomEvents = $false; HazardOverrides = @(); MoodOverride = (Get-ThemeMood -Theme $theme -Stage $stage -SectionIndex 1 -SectionCount 2 -Boss $true); PhaseThresholds = @(0.78, 0.54, 0.28); MovePattern = "BossOrbit"; FirePattern = "BossFan" } }
            30 { $boss = [ordered]@{ Type = "DestroyerBossMk2"; DisplayName = "Destroyer Mk II"; ArchetypeId = "BossDestroyer"; IntroSeconds = 1.25; TargetY = 0.46; ArenaScrollSpeed = 48; HitPoints = 115; AllowRandomEvents = $false; HazardOverrides = @(); MoodOverride = (Get-ThemeMood -Theme $theme -Stage $stage -SectionIndex 2 -SectionCount 3 -Boss $true); PhaseThresholds = @(0.8, 0.55, 0.3); MovePattern = "BossCharge"; FirePattern = "BossFan" } }
            40 { $boss = [ordered]@{ Type = "WalkerBossMk2"; DisplayName = "Walker Mk II"; ArchetypeId = "BossWalker"; IntroSeconds = 1.3; TargetY = 0.5; ArenaScrollSpeed = 46; HitPoints = 125; AllowRandomEvents = $false; HazardOverrides = @(); MoodOverride = (Get-ThemeMood -Theme $theme -Stage $stage -SectionIndex 2 -SectionCount 3 -Boss $true); PhaseThresholds = @(0.82, 0.58, 0.32); MovePattern = "BossOrbit"; FirePattern = "BossFan" } }
            50 { $boss = [ordered]@{ Type = "FinalBoss"; DisplayName = "Final Core"; ArchetypeId = "BossFinal"; IntroSeconds = 1.4; TargetY = 0.5; ArenaScrollSpeed = 42; HitPoints = 135; AllowRandomEvents = $false; HazardOverrides = @(); MoodOverride = (Get-ThemeMood -Theme $theme -Stage $stage -SectionIndex 2 -SectionCount 3 -Boss $true); PhaseThresholds = @(0.84, 0.6, 0.36, 0.18); MovePattern = "BossOrbit"; FirePattern = "BossFan" } }
        }

        $stageObject = [ordered]@{
            StageNumber = $stage
            Name = $name
            IntroSeconds = 1.45
            ScrollSpeed = $baseScrollSpeed
            BaseScrollSpeed = $baseScrollSpeed
            Theme = $theme
            BackgroundSeed = $stage * 7
            StartingLives = 3
            ShipsPerLife = 2
            BackgroundMood = $stageMood
            CheckpointMarkers = $checkpointMarkers
            Sections = $sections
            Boss = $boss
        }
    }
    else
    {
        $sectionCount = if ($densityBand -ge 2) { 5 } else { 4 }
        $sectionSpacing = switch ($densityBand)
        {
            0 { 10.9 }
            1 { 10.5 }
            2 { 9.95 }
            default { 9.5 }
        }
        $checkpointIndex = [int][math]::Floor($sectionCount / 2)
        $templateSequence = switch ($densityBand)
        {
            0 { @(0, 4, 3, 1, 2) }
            1 { @(4, 0, 1, 2, 3) }
            2 { @(0, 1, 4, 2, 3) }
            default { @(1, 4, 2, 3, 0) }
        }

        for ($sectionIndex = 0; $sectionIndex -lt $sectionCount; $sectionIndex++)
        {
            $startSeconds = 0.75 + $sectionIndex * $sectionSpacing
            $checkpoint = ($sectionIndex -eq $checkpointIndex)
            $template = $templateSequence[($stage + $sectionIndex - 1) % $templateSequence.Count]
            $laneBase = ($stage + $sectionIndex * 2) % 5
            $pressureBonus = [math]::Min(2, [math]::Floor(($sectionIndex + $densityBand + 1) / 2))
            $sectionMood = Get-ThemeMood -Theme $theme -Stage $stage -SectionIndex $sectionIndex -SectionCount $sectionCount
            $sectionEvents = Get-SectionEventWindows -Stage $stage -SectionIndex $sectionIndex -SectionCount $sectionCount
            $groups = @()

            switch ($template)
            {
                0
                {
                    $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 0 -Lane $laneBase -Count (5 + $chapter + $pressureBonus) -SpawnLeadDistance 240 -SpawnIntervalSeconds 0.13 -SpacingX 68 -SpeedMultiplier (1.05 + $chapter * 0.04) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (44 + $chapter * 8) -Frequency 1.12
                    $groups += New-Group -ArchetypeId "Walker" -StartSeconds 3.7 -Lane (($laneBase + 2) % 5) -Count (3 + $chapter + [math]::Min(1, $densityBand)) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.22 -SpacingX 80 -SpeedMultiplier (1 + $chapter * 0.03) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 30 -Frequency 1
                    if ($densityBand -ge 1 -or $sectionIndex -ge 2)
                    {
                        $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 7.0 -Lane (($laneBase + 4) % 5) -Count (3 + $chapter + [math]::Min(1, $pressureBonus)) -SpawnLeadDistance 250 -SpawnIntervalSeconds 0.14 -SpacingX 72 -SpeedMultiplier (1.09 + $chapter * 0.04) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (50 + $chapter * 8) -Frequency 1.18
                    }
                }
                1
                {
                    $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 0 -Lane $laneBase -Count (4 + $chapter + $pressureBonus) -SpawnLeadDistance 250 -SpawnIntervalSeconds 0.14 -SpacingX 70 -SpeedMultiplier (1.08 + $chapter * 0.05) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (52 + $chapter * 8) -Frequency 1.18
                    $groups += New-Group -ArchetypeId "Destroyer" -StartSeconds 3.8 -Lane (($laneBase + 2) % 5) -Count (2 + $chapter + [math]::Min(1, $densityBand)) -SpawnLeadDistance 300 -SpawnIntervalSeconds 0.28 -SpacingX 96 -SpeedMultiplier (0.98 + $chapter * 0.04) -MovePatternOverride "Dive" -FirePatternOverride "ForwardPulse" -Amplitude 46 -Frequency 0.92
                    if ($densityBand -ge 1 -or $sectionIndex -ge 1)
                    {
                        $groups += New-Group -ArchetypeId "Walker" -StartSeconds 7.4 -Lane (($laneBase + 4) % 5) -Count (2 + $chapter + [math]::Min(1, $pressureBonus)) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.2 -SpacingX 78 -SpeedMultiplier (1.02 + $chapter * 0.03) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 28 -Frequency 1
                    }
                }
                2
                {
                    $groups += New-Group -ArchetypeId "Walker" -StartSeconds 0 -Lane (($laneBase + 4) % 5) -Count (3 + $chapter + $pressureBonus) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.2 -SpacingX 80 -SpeedMultiplier (1 + $chapter * 0.04) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 28 -Frequency 1
                    $groups += New-Group -ArchetypeId "Carrier" -StartSeconds 3.9 -Lane 2 -Count (2 + [math]::Min(1, $densityBand)) -SpawnLeadDistance 330 -SpawnIntervalSeconds 0.4 -SpacingX 116 -SpeedMultiplier (0.98 + $chapter * 0.03) -MovePatternOverride "TurretCarrier" -FirePatternOverride "SpreadPulse" -Amplitude 34 -Frequency 0.8
                    $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 7.2 -Lane $laneBase -Count (3 + $chapter) -SpawnLeadDistance 250 -SpawnIntervalSeconds 0.14 -SpacingX 72 -SpeedMultiplier (1.1 + $chapter * 0.05) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (52 + $chapter * 10) -Frequency 1.2
                    if ($chapter -ge 3)
                    {
                        $groups += New-Group -ArchetypeId "Bulwark" -StartSeconds 9.0 -Lane (($laneBase + 2) % 5) -Count 1 -SpawnLeadDistance 340 -SpawnIntervalSeconds 0.42 -SpacingX 122 -SpeedMultiplier (0.96 + $chapter * 0.03) -MovePatternOverride "RetreatBackfire" -FirePatternOverride "ForwardPulse" -Amplitude 32 -Frequency 0.82
                    }
                }
                3
                {
                    $groups += New-Group -ArchetypeId "Walker" -StartSeconds 0 -Lane (($laneBase + 1) % 5) -Count (5 + $chapter + $pressureBonus) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.17 -SpacingX 78 -SpeedMultiplier (1 + $chapter * 0.04) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 30 -Frequency 1
                    $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 3.6 -Lane (($laneBase + 4) % 5) -Count (3 + $chapter + [math]::Min(1, $pressureBonus)) -SpawnLeadDistance 248 -SpawnIntervalSeconds 0.14 -SpacingX 70 -SpeedMultiplier (1.08 + $chapter * 0.05) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (48 + $chapter * 8) -Frequency 1.18
                    if ($chapter -ge 2)
                    {
                        $groups += New-Group -ArchetypeId "Bulwark" -StartSeconds 7.2 -Lane (($laneBase + 3) % 5) -Count (1 + [math]::Min(2, $chapter - 1)) -SpawnLeadDistance 340 -SpawnIntervalSeconds 0.4 -SpacingX 124 -SpeedMultiplier (0.96 + $chapter * 0.03) -MovePatternOverride "RetreatBackfire" -FirePatternOverride "ForwardPulse" -Amplitude 32 -Frequency 0.78
                    }
                }
                default
                {
                    $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 0 -Lane $laneBase -Count (4 + $chapter + $pressureBonus) -SpawnLeadDistance 250 -SpawnIntervalSeconds 0.14 -SpacingX 72 -SpeedMultiplier (1.08 + $chapter * 0.05) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (50 + $chapter * 10) -Frequency 1.2
                    $groups += New-Group -ArchetypeId "Destroyer" -StartSeconds 3.8 -Lane (($laneBase + 2) % 5) -Count (2 + [math]::Min(2, $chapter) + [math]::Min(1, $densityBand)) -SpawnLeadDistance 300 -SpawnIntervalSeconds 0.26 -SpacingX 96 -SpeedMultiplier (1 + $chapter * 0.04) -MovePatternOverride "Dive" -FirePatternOverride ($(if ($chapter -ge 3) { "SpreadPulse" } else { "ForwardPulse" })) -Amplitude 42 -Frequency 0.9
                    $groups += New-Group -ArchetypeId "Walker" -StartSeconds 7.0 -Lane (($laneBase + 4) % 5) -Count (2 + $chapter) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.2 -SpacingX 80 -SpeedMultiplier (1.01 + $chapter * 0.04) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 30 -Frequency 1
                    if ($chapter -ge 4)
                    {
                        $groups += New-Group -ArchetypeId "Carrier" -StartSeconds 8.8 -Lane 2 -Count 1 -SpawnLeadDistance 330 -SpawnIntervalSeconds 0.38 -SpacingX 118 -SpeedMultiplier 1 -MovePatternOverride "TurretCarrier" -FirePatternOverride "SpreadPulse" -Amplitude 36 -Frequency 0.8
                    }
                }
            }

            $section = New-Section -Label ("Section {0}" -f ($sectionIndex + 1)) -StartSeconds $startSeconds -DurationSeconds ($sectionSpacing - 0.65) -Checkpoint $checkpoint -PowerDropBonusChance ([math]::Min(0.12, 0.01 + $sectionIndex * 0.01 + $densityBand * 0.005)) -ScrollMultiplier (Get-SectionScrollMultiplier -SectionIndex $sectionIndex -SectionCount $sectionCount) -EnemySpeedMultiplier (Get-SectionEnemySpeedMultiplier -SectionIndex $sectionIndex -SectionCount $sectionCount) -Mood $sectionMood -EventWindows $sectionEvents -Groups $groups
            $sections += $section
            if ($checkpoint)
            {
                $checkpointMarkers += $section.StartSeconds
            }
        }

        $stageObject = [ordered]@{
            StageNumber = $stage
            Name = $name
            IntroSeconds = 1.2
            ScrollSpeed = $baseScrollSpeed
            BaseScrollSpeed = $baseScrollSpeed
            Theme = $theme
            BackgroundSeed = $stage * 7
            StartingLives = 3
            ShipsPerLife = 2
            BackgroundMood = $stageMood
            CheckpointMarkers = $checkpointMarkers
            Sections = $sections
            Boss = $null
        }
    }

    $fileName = "level-{0}.json" -f $stage.ToString("00")
    Write-JsonFile -Path (Join-Path $levelsDir $fileName) -Object $stageObject
}
