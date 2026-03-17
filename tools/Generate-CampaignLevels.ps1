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

    $json = $Object | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine)
}

function New-Group
{
    param(
        [string]$ArchetypeId,
        [int]$Count,
        [string]$Formation,
        [string]$EntrySide,
        [string]$PathType,
        [double]$AnchorX,
        [double]$AnchorY,
        [double]$Spacing,
        [double]$DelayBetweenSpawns,
        [double]$TravelDuration,
        [double]$SpeedMultiplier
    )

    return [ordered]@{
        ArchetypeId = $ArchetypeId
        Count = $Count
        Formation = $Formation
        EntrySide = $EntrySide
        PathType = $PathType
        AnchorX = [math]::Round($AnchorX, 2)
        AnchorY = [math]::Round($AnchorY, 2)
        Spacing = [math]::Round($Spacing, 2)
        DelayBetweenSpawns = [math]::Round($DelayBetweenSpawns, 2)
        TravelDuration = [math]::Round($TravelDuration, 2)
        SpeedMultiplier = [math]::Round($SpeedMultiplier, 2)
    }
}

function New-Wave
{
    param(
        [string]$Label,
        [double]$StartSeconds,
        [bool]$Checkpoint,
        [object[]]$Groups
    )

    return [ordered]@{
        Label = $Label
        StartSeconds = [math]::Round($StartSeconds, 2)
        Checkpoint = $Checkpoint
        Groups = $Groups
    }
}

function Get-LevelName
{
    param([int]$Level)

    $standardNames = @(
        "Opening Lane", "First Turn", "Crosswind", "Split Orbit", "Edge Sweep", "Twin Vectors", "Pressure Gate", "Swoop Line", "Outer Ring",
        "Breaker March", "Arrow Screen", "Mirror Cross", "High Flank", "Orbit Net", "Hammer Arc", "Rapid Line", "Pincer Field", "Pivot Gate", "Fuse Run",
        "Shock Drift", "River Teeth", "Three Lane", "False Calm", "Hook Entry", "Orbit Furnace", "Latch Point", "Fast Divide", "Signal Burst", "Hard Turn",
        "Steel Chorus", "Skirmish Wake", "Wide Orbit", "Vector Prism", "Stacked Arc", "Center Pressure", "Relay Fire", "Mirror Lift", "Split Column", "Last Calm",
        "Rift Current", "High Orbit", "Long Push", "Crossfire Deep", "Anchor Break", "Spiral March", "Late Flank", "Burning Sky", "Final Corridor", "Last Approach"
    )

    $bossNames = @{
        10 = "Boss 10: Destroyer Prime"
        20 = "Boss 20: Walker Matron"
        30 = "Boss 30: Destroyer Mk II"
        40 = "Boss 40: Walker Mk II"
        50 = "Boss 50: Final Core"
    }

    if ($bossNames.ContainsKey($Level))
    {
        return $bossNames[$Level]
    }

    return $standardNames[$Level - 1]
}

$archetypeCatalog = [ordered]@{
    Archetypes = @(
        [ordered]@{ Id = "Walker"; Texture = "Walker"; RenderScale = 0.46; CollisionRadius = 13; Speed = 2.7; HitPoints = 1; ScoreValue = 40; SpawnDelaySeconds = 0.6 },
        [ordered]@{ Id = "Interceptor"; Texture = "Walker"; RenderScale = 0.38; CollisionRadius = 11; Speed = 3.45; HitPoints = 1; ScoreValue = 55; SpawnDelaySeconds = 0.48 },
        [ordered]@{ Id = "Destroyer"; Texture = "Destroyer"; RenderScale = 0.53; CollisionRadius = 17; Speed = 2.2; HitPoints = 2; ScoreValue = 80; SpawnDelaySeconds = 0.72 },
        [ordered]@{ Id = "Turret"; Texture = "Turret"; RenderScale = 0.58; CollisionRadius = 19; Speed = 1.75; HitPoints = 3; ScoreValue = 95; SpawnDelaySeconds = 0.78 },
        [ordered]@{ Id = "Bulwark"; Texture = "Destroyer"; RenderScale = 0.64; CollisionRadius = 23; Speed = 1.9; HitPoints = 4; ScoreValue = 125; SpawnDelaySeconds = 0.82 }
    )
}

Write-JsonFile -Path (Join-Path $levelsDir "enemy-archetypes.json") -Object $archetypeCatalog

$anchorsX = @(0.18, 0.3, 0.5, 0.7, 0.82)
$anchorsY = @(0.16, 0.2, 0.24, 0.3, 0.34)
$formations = @("Line", "Column", "V", "Arc", "Ring")
$pathCycle = @("Straight", "Swoop", "LaneSweep", "ChaseAfterDelay", "OrbitAnchor")

for ($level = 1; $level -le 50; $level++)
{
    $chapter = [int][math]::Ceiling($level / 10)
    $isBossLevel = ($level % 10 -eq 0)
    $name = Get-LevelName -Level $level
    $waves = @()

    switch ($chapter)
    {
        1 { $pool = @("Walker", "Interceptor", "Destroyer") }
        2 { $pool = @("Walker", "Interceptor", "Destroyer", "Turret") }
        3 { $pool = @("Walker", "Destroyer", "Turret", "Interceptor") }
        4 { $pool = @("Destroyer", "Turret", "Interceptor", "Bulwark") }
        default { $pool = @("Walker", "Destroyer", "Turret", "Bulwark", "Interceptor") }
    }

    if ($isBossLevel)
    {
        $warmupCount = if ($level -eq 50) { 3 } else { 2 }
        $warmupTimes = @(0.8, 12.5, 25.5)

        for ($i = 0; $i -lt $warmupCount; $i++)
        {
            $primaryArchetype = $pool[($level + $i) % $pool.Count]
            $secondaryArchetype = $pool[($level + $i + 1) % $pool.Count]
            $anchorLeft = $anchorsX[($level + $i) % 2]
            $anchorRight = $anchorsX[$anchorsX.Count - 1 - (($level + $i) % 2)]
            $anchorY = 0.18 + $i * 0.06
            $entrySide = if ($i % 2 -eq 0) { "Right" } else { "Left" }

            $groups = @(
                (New-Group -ArchetypeId $primaryArchetype -Count (3 + $chapter + $i) -Formation $formations[($level + $i) % $formations.Count] -EntrySide "Top" -PathType $pathCycle[($level + $i) % $pathCycle.Count] -AnchorX $anchorLeft -AnchorY $anchorY -Spacing (64 + $chapter * 4) -DelayBetweenSpawns 0.18 -TravelDuration 3.4 -SpeedMultiplier (1 + $chapter * 0.05)),
                (New-Group -ArchetypeId $secondaryArchetype -Count (2 + $chapter + $i) -Formation $formations[($level + $i + 2) % $formations.Count] -EntrySide $entrySide -PathType $pathCycle[($level + $i + 1) % $pathCycle.Count] -AnchorX $anchorRight -AnchorY ($anchorY + 0.05) -Spacing (68 + $chapter * 3) -DelayBetweenSpawns 0.22 -TravelDuration 3.6 -SpeedMultiplier (1.02 + $chapter * 0.06))
            )

            $waves += New-Wave -Label ("Boss Warmup {0}" -f ($i + 1)) -StartSeconds $warmupTimes[$i] -Checkpoint ($i -eq $warmupCount - 1) -Groups $groups
        }

        switch ($level)
        {
            10 { $boss = [ordered]@{ Type = "DestroyerBoss"; DisplayName = "Destroyer Prime"; ArchetypeId = "Destroyer"; EntrySide = "Top"; AnchorX = 0.5; AnchorY = 0.2; RenderScale = 1.65; HitPoints = 70 } }
            20 { $boss = [ordered]@{ Type = "WalkerBoss"; DisplayName = "Walker Matron"; ArchetypeId = "Turret"; EntrySide = "Top"; AnchorX = 0.5; AnchorY = 0.22; RenderScale = 1.75; HitPoints = 90 } }
            30 { $boss = [ordered]@{ Type = "DestroyerBossMk2"; DisplayName = "Destroyer Mk II"; ArchetypeId = "Destroyer"; EntrySide = "Top"; AnchorX = 0.5; AnchorY = 0.21; RenderScale = 1.88; HitPoints = 115 } }
            40 { $boss = [ordered]@{ Type = "WalkerBossMk2"; DisplayName = "Walker Mk II"; ArchetypeId = "Turret"; EntrySide = "Top"; AnchorX = 0.5; AnchorY = 0.22; RenderScale = 1.98; HitPoints = 140 } }
            50 { $boss = [ordered]@{ Type = "FinalBoss"; DisplayName = "Final Core"; ArchetypeId = "Bulwark"; EntrySide = "Top"; AnchorX = 0.5; AnchorY = 0.2; RenderScale = 2.12; HitPoints = 185 } }
        }

        $levelObject = [ordered]@{
            LevelNumber = $level
            Name = $name
            IntroSeconds = if ($level -eq 50) { 1.8 } else { 1.5 }
            Waves = $waves
            Boss = $boss
        }
    }
    else
    {
        if ($chapter -ge 4)
        {
            $waveCount = 5
        }
        elseif ($chapter -ge 2)
        {
            $waveCount = 4
        }
        else
        {
            $waveCount = 3
        }

        $stageStarts = @(0.8, 11.5, 23.5, 36.5, 50.0)
        $checkpointWave = [int][math]::Floor($waveCount / 2)

        for ($waveIndex = 0; $waveIndex -lt $waveCount; $waveIndex++)
        {
            $template = ($level + $waveIndex) % 6
            $leftX = $anchorsX[($waveIndex + $level) % 2]
            $centerX = $anchorsX[2]
            $rightX = $anchorsX[$anchorsX.Count - 1 - (($waveIndex + $level) % 2)]
            $upperY = $anchorsY[($waveIndex + $chapter - 1) % $anchorsY.Count]
            $midY = [math]::Min(0.42, $upperY + 0.08)
            $primary = $pool[($level + $waveIndex) % $pool.Count]
            $secondary = $pool[($level + $waveIndex + 1) % $pool.Count]
            $tertiary = $pool[($level + $waveIndex + 2) % $pool.Count]
            $groups = @()

            switch ($template)
            {
                0
                {
                    $groups += New-Group -ArchetypeId $primary -Count (3 + $chapter) -Formation "Line" -EntrySide "Top" -PathType "Straight" -AnchorX $leftX -AnchorY $upperY -Spacing (62 + $chapter * 4) -DelayBetweenSpawns 0.16 -TravelDuration 3.2 -SpeedMultiplier (1 + $chapter * 0.05)
                    $groups += New-Group -ArchetypeId $secondary -Count (3 + $chapter) -Formation "Line" -EntrySide "Top" -PathType "Straight" -AnchorX $rightX -AnchorY ($upperY + 0.04) -Spacing (62 + $chapter * 4) -DelayBetweenSpawns 0.16 -TravelDuration 3.2 -SpeedMultiplier (1 + $chapter * 0.05)
                }
                1
                {
                    $groups += New-Group -ArchetypeId $primary -Count (4 + $chapter) -Formation "V" -EntrySide "Left" -PathType "Swoop" -AnchorX ($leftX + 0.06) -AnchorY $midY -Spacing (58 + $chapter * 5) -DelayBetweenSpawns 0.12 -TravelDuration 3.0 -SpeedMultiplier (1.05 + $chapter * 0.06)
                }
                2
                {
                    $groups += New-Group -ArchetypeId $primary -Count (2 + [math]::Min(3, $chapter)) -Formation "Column" -EntrySide "Top" -PathType "LaneSweep" -AnchorX $centerX -AnchorY ($upperY + 0.03) -Spacing (74 + $chapter * 3) -DelayBetweenSpawns 0.18 -TravelDuration 3.8 -SpeedMultiplier (1 + $chapter * 0.04)
                    $groups += New-Group -ArchetypeId $secondary -Count (2 + $chapter) -Formation "Arc" -EntrySide "Right" -PathType "ChaseAfterDelay" -AnchorX $rightX -AnchorY $midY -Spacing (66 + $chapter * 4) -DelayBetweenSpawns 0.2 -TravelDuration 3.6 -SpeedMultiplier (1.02 + $chapter * 0.06)
                }
                3
                {
                    $groups += New-Group -ArchetypeId $primary -Count (3 + $chapter) -Formation "Ring" -EntrySide "Top" -PathType "OrbitAnchor" -AnchorX $centerX -AnchorY ($upperY + 0.02) -Spacing (56 + $chapter * 3) -DelayBetweenSpawns 0.1 -TravelDuration 4.0 -SpeedMultiplier (0.98 + $chapter * 0.05)
                }
                4
                {
                    $groups += New-Group -ArchetypeId $primary -Count (3 + $chapter) -Formation "Arc" -EntrySide "Right" -PathType "Swoop" -AnchorX $rightX -AnchorY $upperY -Spacing (64 + $chapter * 3) -DelayBetweenSpawns 0.14 -TravelDuration 3.2 -SpeedMultiplier (1.08 + $chapter * 0.06)
                    $groups += New-Group -ArchetypeId $secondary -Count (2 + $chapter) -Formation "Column" -EntrySide "Left" -PathType "LaneSweep" -AnchorX $leftX -AnchorY ($midY + 0.04) -Spacing (70 + $chapter * 3) -DelayBetweenSpawns 0.2 -TravelDuration 3.7 -SpeedMultiplier (1 + $chapter * 0.05)
                }
                default
                {
                    $groups += New-Group -ArchetypeId $primary -Count (2 + $chapter) -Formation "Line" -EntrySide "Bottom" -PathType "ChaseAfterDelay" -AnchorX $leftX -AnchorY ($midY + 0.04) -Spacing (68 + $chapter * 2) -DelayBetweenSpawns 0.22 -TravelDuration 3.5 -SpeedMultiplier (1.04 + $chapter * 0.06)
                    $groups += New-Group -ArchetypeId $tertiary -Count (2 + $chapter) -Formation "V" -EntrySide "Top" -PathType "Straight" -AnchorX $rightX -AnchorY $upperY -Spacing (64 + $chapter * 4) -DelayBetweenSpawns 0.14 -TravelDuration 3.3 -SpeedMultiplier (1.02 + $chapter * 0.05)
                }
            }

            if ($chapter -ge 4 -and $waveIndex -eq $waveCount - 1)
            {
                $groups += New-Group -ArchetypeId "Bulwark" -Count 2 -Formation "Line" -EntrySide "Top" -PathType "LaneSweep" -AnchorX $centerX -AnchorY 0.22 -Spacing 96 -DelayBetweenSpawns 0.28 -TravelDuration 4.2 -SpeedMultiplier (0.96 + $chapter * 0.03)
            }

            $waves += New-Wave -Label ("Wave {0}" -f ($waveIndex + 1)) -StartSeconds $stageStarts[$waveIndex] -Checkpoint ($waveIndex -eq $checkpointWave) -Groups $groups
        }

        $levelObject = [ordered]@{
            LevelNumber = $level
            Name = $name
            IntroSeconds = 1.25
            Waves = $waves
            Boss = $null
        }
    }

    $fileName = "level-{0}.json" -f $level.ToString("00")
    Write-JsonFile -Path (Join-Path $levelsDir $fileName) -Object $levelObject
}
