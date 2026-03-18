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
        [int]$IntegrityThresholdPercent
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
        [object[]]$Groups
    )

    return [ordered]@{
        Label = $Label
        StartSeconds = [math]::Round($StartSeconds, 2)
        DurationSeconds = [math]::Round($DurationSeconds, 2)
        Checkpoint = $Checkpoint
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

$archetypeCatalog = [ordered]@{
    Archetypes = @(
        (New-Archetype -Id "Walker" -DisplayName "Walker" -RenderScale 1 -MoveSpeed 190 -HitPoints 6 -ScoreValue 80 -SpawnLeadDistance 220 -FireIntervalSeconds 0 -MovementAmplitude 28 -MovementFrequency 1 -MovePattern "StraightFlyIn" -FirePattern "None" -Sprite (New-Sprite -Id "WalkerSprite" -PixelScale 4 -PrimaryColor "#CFEFFF" -SecondaryColor "#65A7C5" -AccentColor "#F4B860" -Rows @("....##......","..######....",".##++++##...","##++CC++##..","##++++++##..",".##+**+##...","..######....","....##......","............") -CoreRows @("............","............","............","....XXXX....","....XXXX....","............","............","............","............")) -ContactDamage 2 -ProjectileDamage 1 -DamageRadius 1 -IntegrityThresholdPercent 15),
        (New-Archetype -Id "Interceptor" -DisplayName "Interceptor" -RenderScale 0.95 -MoveSpeed 250 -HitPoints 4 -ScoreValue 90 -SpawnLeadDistance 260 -FireIntervalSeconds 0 -MovementAmplitude 60 -MovementFrequency 1.2 -MovePattern "SineWave" -FirePattern "None" -Sprite (New-Sprite -Id "InterceptorSprite" -PixelScale 4 -PrimaryColor "#FFF2D6" -SecondaryColor "#F2AE49" -AccentColor "#FF7A59" -Rows @("...##.....",".######...","##++++##..","++++CC++++","##++++##..",".######...","...##.....") -CoreRows @("..........","..........","..........","....XX....","..........","..........","..........")) -ContactDamage 2 -ProjectileDamage 1 -DamageRadius 1 -IntegrityThresholdPercent 15),
        (New-Archetype -Id "Destroyer" -DisplayName "Destroyer" -RenderScale 1 -MoveSpeed 170 -HitPoints 10 -ScoreValue 125 -SpawnLeadDistance 300 -FireIntervalSeconds 1.7 -MovementAmplitude 52 -MovementFrequency 0.9 -MovePattern "Dive" -FirePattern "ForwardPulse" -Sprite (New-Sprite -Id "DestroyerSprite" -PixelScale 4 -PrimaryColor "#F2DCE5" -SecondaryColor "#B56A8A" -AccentColor "#FF9B54" -Rows @(".....####.......","...########.....",".###++++++###...","###++CCCC++###..","###++++++++###**",".###++++++###...","...########.....",".....####.......","................") -CoreRows @("................","................","................",".....XXXX.......",".....XXXX.......","................","................","................","................")) -ContactDamage 3 -ProjectileDamage 1 -DamageRadius 1 -IntegrityThresholdPercent 15),
        (New-Archetype -Id "Carrier" -DisplayName "Carrier" -RenderScale 1 -MoveSpeed 145 -HitPoints 12 -ScoreValue 160 -SpawnLeadDistance 320 -FireIntervalSeconds 1.4 -MovementAmplitude 40 -MovementFrequency 0.8 -MovePattern "TurretCarrier" -FirePattern "SpreadPulse" -Sprite (New-Sprite -Id "CarrierSprite" -PixelScale 4 -PrimaryColor "#DDF0D3" -SecondaryColor "#7AAA6E" -AccentColor "#F4B860" -Rows @("......####........","....########......","..###++##++###....",".###+++CC+++###...","###++++CC++++###..","###+++****+++###..",".###++++++++###...","..###++++++###....","....########......","......####........","..................") -CoreRows @("..................","..................","..................","......XXXX........","......XXXX........","..................","..................","..................","..................","..................","..................")) -ContactDamage 3 -ProjectileDamage 1 -DamageRadius 1 -IntegrityThresholdPercent 14),
        (New-Archetype -Id "Bulwark" -DisplayName "Bulwark" -RenderScale 1 -MoveSpeed 135 -HitPoints 16 -ScoreValue 190 -SpawnLeadDistance 340 -FireIntervalSeconds 1.2 -MovementAmplitude 36 -MovementFrequency 0.7 -MovePattern "RetreatBackfire" -FirePattern "ForwardPulse" -Sprite (New-Sprite -Id "BulwarkSprite" -PixelScale 4 -PrimaryColor "#E9E3D2" -SecondaryColor "#A98F6C" -AccentColor "#FFB347" -Rows @(".....######.......","...##########.....",".###++++++++###...","###++CCCCCC++###..","###++++++++++###..","###+++****+++###..",".###++++++++###...","...##########.....",".....######.......","..................") -CoreRows @("..................","..................","..................",".....XXXXXX.......",".....XXXXXX.......","..................","..................","..................","..................","..................")) -ContactDamage 4 -ProjectileDamage 2 -DamageRadius 1 -IntegrityThresholdPercent 12),
        (New-Archetype -Id "BossDestroyer" -DisplayName "Destroyer Prime" -RenderScale 1 -MoveSpeed 130 -HitPoints 90 -ScoreValue 600 -SpawnLeadDistance 360 -FireIntervalSeconds 0.85 -MovementAmplitude 90 -MovementFrequency 0.8 -MovePattern "BossCharge" -FirePattern "BossFan" -Sprite (New-Sprite -Id "BossDestroyerSprite" -PixelScale 4 -PrimaryColor "#F6E4EA" -SecondaryColor "#C06A8D" -AccentColor "#FFB347" -Rows @(".......######...........",".....##########.........","...####++++++####.......",".####++++CC++++####.....","####++++CCCC++++####....","####++++CCCC++++####.**.","####++++CCCC++++####....",".####++++CC++++####.....","...####++++++####.......",".....##########.........",".......######...........","........................") -CoreRows @("........................","........................","........................","........XXXX............",".......XXXXXX...........",".......XXXXXX...........",".......XXXXXX...........","........XXXX............","........................","........................","........................","........................")) -ContactDamage 5 -ProjectileDamage 2 -DamageRadius 1 -IntegrityThresholdPercent 10),
        (New-Archetype -Id "BossWalker" -DisplayName "Walker Matron" -RenderScale 1 -MoveSpeed 120 -HitPoints 105 -ScoreValue 700 -SpawnLeadDistance 360 -FireIntervalSeconds 0.78 -MovementAmplitude 96 -MovementFrequency 0.75 -MovePattern "BossOrbit" -FirePattern "BossFan" -Sprite (New-Sprite -Id "BossWalkerSprite" -PixelScale 4 -PrimaryColor "#E1F7D7" -SecondaryColor "#78B46A" -AccentColor "#FFD166" -Rows @("........####............","......########..........","....####++++####........","..####+++CC+++####......",".####++++CCCC++++####...","####++++++CC++++++####..","####++++******++++####..",".####++++CCCC++++####...","..####+++CC+++####......","....####++++####........","......########..........","........####............") -CoreRows @("........................","........................","........................",".........XX.............","........XXXX............","........XXXX............","........................","........XXXX............",".........XX.............","........................","........................","........................")) -ContactDamage 5 -ProjectileDamage 2 -DamageRadius 1 -IntegrityThresholdPercent 10),
        (New-Archetype -Id "BossFinal" -DisplayName "Final Core" -RenderScale 1 -MoveSpeed 135 -HitPoints 135 -ScoreValue 1000 -SpawnLeadDistance 380 -FireIntervalSeconds 0.7 -MovementAmplitude 110 -MovementFrequency 0.7 -MovePattern "BossOrbit" -FirePattern "BossFan" -Sprite (New-Sprite -Id "BossFinalSprite" -PixelScale 4 -PrimaryColor "#F1EEDC" -SecondaryColor "#8AA7D1" -AccentColor "#FF9F59" -Rows @(".........######...........","......############........","....####++++++++####......","..####+++++CC+++++####....",".####+++++CCCC+++++####...","####+++++CCCCCC+++++####..","####++++***CC***++++####..","####+++++CCCCCC+++++####..",".####+++++CCCC+++++####...","..####+++++CC+++++####....","....####++++++++####......","......############........",".........######...........") -CoreRows @("..........................","..........................","..........................","..........XXXX............",".........XXXXXX...........","........XXXXXXXX..........","..........XXXX............","........XXXXXXXX..........",".........XXXXXX...........","..........XXXX............","..........................","..........................","..........................")) -ContactDamage 6 -ProjectileDamage 2 -DamageRadius 1 -IntegrityThresholdPercent 10)
    )
}

Write-JsonFile -Path (Join-Path $levelsDir "enemy-archetypes.json") -Object $archetypeCatalog

$themes = @("Nebula", "Ion", "Solar")
$moveTemplates = @("StraightFlyIn", "SineWave", "Dive", "RetreatBackfire", "TurretCarrier")
$fireTemplates = @("None", "ForwardPulse", "SpreadPulse", "AimedShot")

for ($stage = 1; $stage -le 50; $stage++)
{
    $chapter = [int][math]::Ceiling($stage / 10)
    $isBossStage = ($stage % 10 -eq 0)
    $theme = $themes[($stage - 1) % $themes.Count]
    $name = Get-StageName -Stage $stage
    $sections = @()
    $checkpointMarkers = @()

    if ($isBossStage)
    {
        $warmupCount = if ($stage -eq 50) { 3 } else { 2 }
        for ($sectionIndex = 0; $sectionIndex -lt $warmupCount; $sectionIndex++)
        {
            $startSeconds = 0.75 + $sectionIndex * 12.5
            $checkpoint = ($sectionIndex -eq ($warmupCount - 1))
            $laneBase = ($stage + $sectionIndex) % 5
            $groups = @(
                (New-Group -ArchetypeId "Interceptor" -StartSeconds 0 -Lane $laneBase -Count (4 + $chapter + $sectionIndex) -SpawnLeadDistance 260 -SpawnIntervalSeconds 0.18 -SpacingX 76 -SpeedMultiplier (1.05 + $chapter * 0.05) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (52 + $sectionIndex * 12) -Frequency 1.15),
                (New-Group -ArchetypeId ($(if ($sectionIndex % 2 -eq 0) { "Destroyer" } else { "Carrier" })) -StartSeconds 4.6 -Lane (($laneBase + 2) % 5) -Count (2 + $chapter) -SpawnLeadDistance 320 -SpawnIntervalSeconds 0.28 -SpacingX 108 -SpeedMultiplier (1 + $chapter * 0.04) -MovePatternOverride ($(if ($sectionIndex % 2 -eq 0) { "Dive" } else { "TurretCarrier" })) -FirePatternOverride ($(if ($chapter -ge 3) { "ForwardPulse" } else { "None" })) -Amplitude 48 -Frequency 0.9)
            )

            $section = New-Section -Label ("Warmup {0}" -f ($sectionIndex + 1)) -StartSeconds $startSeconds -DurationSeconds 11.5 -Checkpoint $checkpoint -Groups $groups
            $sections += $section
            if ($checkpoint)
            {
                $checkpointMarkers += $section.StartSeconds
            }
        }

        switch ($stage)
        {
            10 { $boss = [ordered]@{ Type = "DestroyerBoss"; DisplayName = "Destroyer Prime"; ArchetypeId = "BossDestroyer"; IntroSeconds = 1.2; TargetY = 0.48; ArenaScrollSpeed = 58; HitPoints = 90; PhaseThresholds = @(0.75, 0.5, 0.25); MovePattern = "BossCharge"; FirePattern = "BossFan" } }
            20 { $boss = [ordered]@{ Type = "WalkerBoss"; DisplayName = "Walker Matron"; ArchetypeId = "BossWalker"; IntroSeconds = 1.2; TargetY = 0.5; ArenaScrollSpeed = 52; HitPoints = 105; PhaseThresholds = @(0.78, 0.54, 0.28); MovePattern = "BossOrbit"; FirePattern = "BossFan" } }
            30 { $boss = [ordered]@{ Type = "DestroyerBossMk2"; DisplayName = "Destroyer Mk II"; ArchetypeId = "BossDestroyer"; IntroSeconds = 1.25; TargetY = 0.46; ArenaScrollSpeed = 48; HitPoints = 115; PhaseThresholds = @(0.8, 0.55, 0.3); MovePattern = "BossCharge"; FirePattern = "BossFan" } }
            40 { $boss = [ordered]@{ Type = "WalkerBossMk2"; DisplayName = "Walker Mk II"; ArchetypeId = "BossWalker"; IntroSeconds = 1.3; TargetY = 0.5; ArenaScrollSpeed = 46; HitPoints = 125; PhaseThresholds = @(0.82, 0.58, 0.32); MovePattern = "BossOrbit"; FirePattern = "BossFan" } }
            50 { $boss = [ordered]@{ Type = "FinalBoss"; DisplayName = "Final Core"; ArchetypeId = "BossFinal"; IntroSeconds = 1.4; TargetY = 0.5; ArenaScrollSpeed = 42; HitPoints = 135; PhaseThresholds = @(0.84, 0.6, 0.36, 0.18); MovePattern = "BossOrbit"; FirePattern = "BossFan" } }
        }

        $stageObject = [ordered]@{
            StageNumber = $stage
            Name = $name
            IntroSeconds = 1.45
            ScrollSpeed = [math]::Round(165 + $chapter * 10, 2)
            Theme = $theme
            BackgroundSeed = $stage * 7
            CheckpointMarkers = $checkpointMarkers
            Sections = $sections
            Boss = $boss
        }
    }
    else
    {
        $sectionCount = if ($chapter -ge 4) { 5 } elseif ($chapter -ge 2) { 4 } else { 3 }
        $sectionSpacing = 13.25
        $checkpointIndex = [int][math]::Floor($sectionCount / 2)

        for ($sectionIndex = 0; $sectionIndex -lt $sectionCount; $sectionIndex++)
        {
            $startSeconds = 0.75 + $sectionIndex * $sectionSpacing
            $checkpoint = ($sectionIndex -eq $checkpointIndex)
            $template = ($stage + $sectionIndex) % 5
            $laneBase = ($stage + $sectionIndex * 2) % 5
            $groups = @()

            switch ($template)
            {
                0
                {
                    $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 0 -Lane $laneBase -Count (4 + $chapter) -SpawnLeadDistance 240 -SpawnIntervalSeconds 0.16 -SpacingX 74 -SpeedMultiplier (1.05 + $chapter * 0.04) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (44 + $chapter * 8) -Frequency 1.12
                    $groups += New-Group -ArchetypeId "Walker" -StartSeconds 5.4 -Lane (($laneBase + 2) % 5) -Count (3 + $chapter) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.26 -SpacingX 86 -SpeedMultiplier (1 + $chapter * 0.03) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 30 -Frequency 1
                }
                1
                {
                    $groups += New-Group -ArchetypeId "Destroyer" -StartSeconds 0.5 -Lane (($laneBase + 1) % 5) -Count (2 + $chapter) -SpawnLeadDistance 300 -SpawnIntervalSeconds 0.34 -SpacingX 112 -SpeedMultiplier (0.96 + $chapter * 0.04) -MovePatternOverride "Dive" -FirePatternOverride "ForwardPulse" -Amplitude 48 -Frequency 0.92
                    $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 5.8 -Lane (($laneBase + 3) % 5) -Count (3 + $chapter) -SpawnLeadDistance 260 -SpawnIntervalSeconds 0.18 -SpacingX 76 -SpeedMultiplier (1.08 + $chapter * 0.05) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (54 + $chapter * 8) -Frequency 1.18
                }
                2
                {
                    $groups += New-Group -ArchetypeId "Carrier" -StartSeconds 0.6 -Lane 2 -Count 2 -SpawnLeadDistance 330 -SpawnIntervalSeconds 0.52 -SpacingX 132 -SpeedMultiplier (0.98 + $chapter * 0.03) -MovePatternOverride "TurretCarrier" -FirePatternOverride "SpreadPulse" -Amplitude 34 -Frequency 0.8
                    $groups += New-Group -ArchetypeId "Walker" -StartSeconds 4.2 -Lane (($laneBase + 4) % 5) -Count (2 + $chapter) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.24 -SpacingX 88 -SpeedMultiplier (1 + $chapter * 0.04) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 28 -Frequency 1
                    if ($chapter -ge 3)
                    {
                        $groups += New-Group -ArchetypeId "Bulwark" -StartSeconds 8.1 -Lane (($laneBase + 2) % 5) -Count 1 -SpawnLeadDistance 340 -SpawnIntervalSeconds 0.5 -SpacingX 128 -SpeedMultiplier (0.96 + $chapter * 0.03) -MovePatternOverride "RetreatBackfire" -FirePatternOverride "ForwardPulse" -Amplitude 32 -Frequency 0.82
                    }
                }
                3
                {
                    $groups += New-Group -ArchetypeId "Walker" -StartSeconds 0 -Lane (($laneBase + 1) % 5) -Count (4 + $chapter) -SpawnLeadDistance 220 -SpawnIntervalSeconds 0.18 -SpacingX 84 -SpeedMultiplier (1 + $chapter * 0.04) -MovePatternOverride "StraightFlyIn" -FirePatternOverride "None" -Amplitude 30 -Frequency 1
                    $groups += New-Group -ArchetypeId "Bulwark" -StartSeconds 5.9 -Lane (($laneBase + 3) % 5) -Count (1 + [math]::Min(2, $chapter)) -SpawnLeadDistance 340 -SpawnIntervalSeconds 0.46 -SpacingX 136 -SpeedMultiplier (0.95 + $chapter * 0.03) -MovePatternOverride "RetreatBackfire" -FirePatternOverride "ForwardPulse" -Amplitude 32 -Frequency 0.78
                }
                default
                {
                    $groups += New-Group -ArchetypeId "Interceptor" -StartSeconds 0.2 -Lane $laneBase -Count (3 + $chapter) -SpawnLeadDistance 250 -SpawnIntervalSeconds 0.16 -SpacingX 78 -SpeedMultiplier (1.08 + $chapter * 0.05) -MovePatternOverride "SineWave" -FirePatternOverride "None" -Amplitude (50 + $chapter * 10) -Frequency 1.2
                    $groups += New-Group -ArchetypeId "Destroyer" -StartSeconds 4.7 -Lane (($laneBase + 2) % 5) -Count (2 + [math]::Min(2, $chapter)) -SpawnLeadDistance 300 -SpawnIntervalSeconds 0.3 -SpacingX 110 -SpeedMultiplier (1 + $chapter * 0.04) -MovePatternOverride "Dive" -FirePatternOverride ($(if ($chapter -ge 3) { "SpreadPulse" } else { "ForwardPulse" })) -Amplitude 42 -Frequency 0.9
                    if ($chapter -ge 4)
                    {
                        $groups += New-Group -ArchetypeId "Carrier" -StartSeconds 8.3 -Lane 2 -Count 1 -SpawnLeadDistance 330 -SpawnIntervalSeconds 0.4 -SpacingX 128 -SpeedMultiplier 1 -MovePatternOverride "TurretCarrier" -FirePatternOverride "SpreadPulse" -Amplitude 36 -Frequency 0.8
                    }
                }
            }

            $section = New-Section -Label ("Section {0}" -f ($sectionIndex + 1)) -StartSeconds $startSeconds -DurationSeconds 11.8 -Checkpoint $checkpoint -Groups $groups
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
            ScrollSpeed = [math]::Round(155 + $chapter * 12 + ($stage % 4) * 2, 2)
            Theme = $theme
            BackgroundSeed = $stage * 7
            CheckpointMarkers = $checkpointMarkers
            Sections = $sections
            Boss = $null
        }
    }

    $fileName = "level-{0}.json" -f $stage.ToString("00")
    Write-JsonFile -Path (Join-Path $levelsDir $fileName) -Object $stageObject
}
