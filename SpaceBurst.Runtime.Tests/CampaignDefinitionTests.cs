using SpaceBurst.RuntimeData;
using Xunit;

namespace SpaceBurst.Runtime.Tests
{
    public sealed class CampaignDefinitionTests
    {
        [Fact]
        public void StageFilesDeserializeAndValidate()
        {
            string repoRoot = FindRepositoryRoot();
            string levelsDirectory = Path.Combine(repoRoot, "Levels");

            EnemyArchetypeCatalogDefinition catalog = LevelSerializer.LoadArchetypesFromFile(Path.Combine(levelsDirectory, "enemy-archetypes.json"));
            Dictionary<string, EnemyArchetypeDefinition> archetypes = catalog.Archetypes.ToDictionary(x => x.Id, x => x);
            List<StageDefinition> stages = new();

            for (int stageNumber = 1; stageNumber <= 50; stageNumber++)
                stages.Add(LevelSerializer.LoadLevelFromFile(Path.Combine(levelsDirectory, $"level-{stageNumber:00}.json")));

            Assert.Empty(LevelValidator.ValidateArchetypes(catalog));
            Assert.Empty(LevelValidator.ValidateCampaign(stages, archetypes));
        }

        [Fact]
        public void BossStagesOnlyAppearEveryTenthStage()
        {
            string repoRoot = FindRepositoryRoot();
            string levelsDirectory = Path.Combine(repoRoot, "Levels");

            for (int stageNumber = 1; stageNumber <= 50; stageNumber++)
            {
                StageDefinition stage = LevelSerializer.LoadLevelFromFile(Path.Combine(levelsDirectory, $"level-{stageNumber:00}.json"));
                Assert.Equal(stageNumber % 10 == 0, stage.Boss != null);
            }
        }

        [Fact]
        public void OverdriveSliceStagesCarryContinuousChapterPacing()
        {
            string repoRoot = FindRepositoryRoot();
            string levelsDirectory = Path.Combine(repoRoot, "Levels");
            List<StageDefinition> sliceStages = new();

            for (int stageNumber = 1; stageNumber <= 10; stageNumber++)
                sliceStages.Add(LevelSerializer.LoadLevelFromFile(Path.Combine(levelsDirectory, $"level-{stageNumber:00}.json")));

            Assert.All(sliceStages, stage =>
            {
                Assert.Equal("Chapter 1: Overdrive", stage.SliceChapterName);
                Assert.Equal(720f, stage.SliceTargetDurationSeconds);
                Assert.True(stage.HordePackets.Count >= (stage.StageNumber == 10 ? 7 : 5));
                Assert.True(stage.KillChainEvents.Count >= 3);
                Assert.True(stage.PresentationCues.Count >= 1);
            });

            float authoredDuration = sliceStages.Sum(stage => stage.Sections.Max(section => section.StartSeconds + section.DurationSeconds));
            Assert.True(authoredDuration >= 715f);
            Assert.Contains(sliceStages[3].PresentationCues, cue => cue.Label == "4 MINUTE ESCALATION");
            Assert.Contains(sliceStages[6].PresentationCues, cue => cue.Label == "8 MINUTE REDLINE");
            Assert.Contains(sliceStages[8].EliteBursts, burst => burst.WarningText == "PURSUIT DREADNOUGHT");
            Assert.NotNull(sliceStages[9].Boss);
            Assert.Equal("Pursuit Dreadnought", sliceStages[9].Boss.DisplayName);
        }

        [Fact]
        public void LevelSerializer_RoundTripsOverdriveSliceFields()
        {
            var definition = new StageDefinition
            {
                StageNumber = 1,
                Name = "Overdrive Launch",
                Theme = "Nebula",
                SliceChapterName = "Chapter 1: Overdrive",
                SliceTargetDurationSeconds = 720,
                Sections =
                {
                    new SectionDefinition
                    {
                        Label = "Overdrive 1",
                        StartSeconds = 0.75f,
                        DurationSeconds = 34.2f,
                        Groups =
                        {
                            new SpawnGroupDefinition
                            {
                                ArchetypeId = "Walker",
                                StartSeconds = 0f,
                                Count = 1,
                            }
                        }
                    }
                },
                HordePackets =
                {
                    new HordePacketDefinition
                    {
                        ArchetypeId = "Interceptor",
                        StartSeconds = 4f,
                        BurstCount = 3,
                        CountPerBurst = 5,
                        SpawnIntervalSeconds = 0.1f,
                    }
                },
                EliteBursts =
                {
                    new EliteBurstEventDefinition
                    {
                        WarningText = "FRACTURE CONVOY",
                        StartSeconds = 52f,
                        ArchetypeId = "Destroyer",
                        EliteCount = 2,
                        ScrapReward = 1,
                        RewindRefillPercent = 0.16f,
                    }
                },
                KillChainEvents =
                {
                    new KillChainEventDefinition
                    {
                        TriggerMultiplier = 16,
                        Label = "OVERDRIVE",
                        BonusXp = 6.5f,
                        BonusScrap = 2,
                        BonusRewindPercent = 0.1f,
                    }
                },
                PresentationCues =
                {
                    new PresentationCueDefinition
                    {
                        Kind = PresentationCueKind.ChapterBeat,
                        StartSeconds = 1f,
                        DurationSeconds = 2.2f,
                        Label = "CHAPTER 1: OVERDRIVE",
                        AccentColor = "#56F0FF",
                        Intensity = 1.1f,
                    }
                }
            };

            string json = LevelSerializer.SerializeLevel(definition);
            StageDefinition roundTripped = LevelSerializer.DeserializeLevel(json);

            Assert.Equal("Chapter 1: Overdrive", roundTripped.SliceChapterName);
            Assert.Equal(720f, roundTripped.SliceTargetDurationSeconds);
            Assert.Single(roundTripped.HordePackets);
            Assert.Equal("Interceptor", roundTripped.HordePackets[0].ArchetypeId);
            Assert.Equal(3, roundTripped.HordePackets[0].BurstCount);
            Assert.Single(roundTripped.EliteBursts);
            Assert.Equal("FRACTURE CONVOY", roundTripped.EliteBursts[0].WarningText);
            Assert.Equal(0.16f, roundTripped.EliteBursts[0].RewindRefillPercent);
            Assert.Single(roundTripped.KillChainEvents);
            Assert.Equal(16, roundTripped.KillChainEvents[0].TriggerMultiplier);
            Assert.Equal(0.1f, roundTripped.KillChainEvents[0].BonusRewindPercent);
            Assert.Single(roundTripped.PresentationCues);
            Assert.Equal(PresentationCueKind.ChapterBeat, roundTripped.PresentationCues[0].Kind);
            Assert.Equal("CHAPTER 1: OVERDRIVE", roundTripped.PresentationCues[0].Label);
        }

        [Fact]
        public void DamageMaskRemovesCellsAndDestroysOnCoreBreach()
        {
            var sprite = new ProceduralSpriteDefinition
            {
                Rows = new List<string>
                {
                    ".....",
                    ".###.",
                    ".#C#.",
                    ".###.",
                    ".....",
                },
                VitalCore = new VitalCoreMaskDefinition
                {
                    Rows = new List<string>
                    {
                        ".....",
                        ".....",
                        "..X..",
                        ".....",
                        ".....",
                    }
                }
            };

            MaskGrid mask = DamageMaskMath.CreateGrid(sprite);
            DamageResult result = DamageMaskMath.ApplyPointDamage(mask, 2, 2, 0, 1, 15);

            Assert.Equal(1, result.CellsRemoved);
            Assert.Equal(1, result.CoreCellsRemoved);
            Assert.True(result.Destroyed);
            Assert.Equal(8, result.RemainingOccupied);
        }

        [Fact]
        public void ImpactDamageRemovesMultipleCellsAndSupportsSplash()
        {
            var sprite = new ProceduralSpriteDefinition
            {
                Rows = new List<string>
                {
                    ".......",
                    "..###..",
                    ".#####.",
                    ".##C##.",
                    ".#####.",
                    "..###..",
                    ".......",
                },
                VitalCore = new VitalCoreMaskDefinition
                {
                    Rows = new List<string>
                    {
                        ".......",
                        ".......",
                        ".......",
                        "...X...",
                        ".......",
                        ".......",
                        ".......",
                    }
                }
            };

            MaskGrid mask = DamageMaskMath.CreateGrid(sprite);
            DamageResult result = DamageMaskMath.ApplyImpactDamage(
                mask,
                3,
                2,
                new ImpactProfileDefinition
                {
                    Kernel = ImpactKernelShape.Blast5,
                    BaseCellsRemoved = 4,
                    BonusCellsPerDamage = 1,
                    SplashRadius = 1,
                    SplashPercent = 50,
                },
                2,
                25);

            Assert.True(result.CellsRemoved >= 5);
            Assert.True(result.CoreCellsRemoved >= 0);
            Assert.True(result.RemainingOccupied < mask.InitialOccupiedCount);
        }

        [Fact]
        public void CoreBreachCanDestroyNonBossWithoutDestroyingBoss()
        {
            var sprite = new ProceduralSpriteDefinition
            {
                Rows = new List<string>
                {
                    ".......",
                    "..###..",
                    ".##C##.",
                    ".##C##.",
                    "..###..",
                    ".......",
                },
                VitalCore = new VitalCoreMaskDefinition
                {
                    Rows = new List<string>
                    {
                        ".......",
                        ".......",
                        "..XX...",
                        "..XX...",
                        ".......",
                        ".......",
                    }
                }
            };

            MaskGrid mask = DamageMaskMath.CreateGrid(sprite);
            DamageResult result = DamageMaskMath.ApplyImpactDamage(
                mask,
                2,
                2,
                new ImpactProfileDefinition
                {
                    Kernel = ImpactKernelShape.Point,
                    BaseCellsRemoved = 1,
                    BonusCellsPerDamage = 0,
                },
                1,
                10);

            Assert.Equal(1, result.CoreCellsRemoved);
            Assert.False(result.Destroyed);
            Assert.True(DamageMaskMath.ShouldDestroyOnImpact(result, true));
            Assert.False(DamageMaskMath.ShouldDestroyOnImpact(result, false));
        }

        [Fact]
        public void IntegrityThresholdFallbackStillDestroysWithoutCoreHit()
        {
            var sprite = new ProceduralSpriteDefinition
            {
                Rows = new List<string>
                {
                    ".#########.",
                },
                VitalCore = new VitalCoreMaskDefinition
                {
                    Rows = new List<string>
                    {
                        "........X..",
                    }
                }
            };

            MaskGrid mask = DamageMaskMath.CreateGrid(sprite);
            DamageResult result = DamageMaskMath.ApplyPointDamage(
                mask,
                1,
                0,
                0,
                5,
                60);

            Assert.Equal(0, result.CoreCellsRemoved);
            Assert.True(result.Destroyed);
            Assert.True(DamageMaskMath.ShouldDestroyOnImpact(result, true));
        }

        [Fact]
        public void InwardTargetingPrefersCellsCloserToCore()
        {
            var sprite = new ProceduralSpriteDefinition
            {
                Rows = new List<string>
                {
                    "#####",
                    "#####",
                    "##C##",
                    "#####",
                    "#####",
                },
                VitalCore = new VitalCoreMaskDefinition
                {
                    Rows = new List<string>
                    {
                        ".....",
                        ".....",
                        "..X..",
                        ".....",
                        ".....",
                    }
                }
            };

            MaskGrid mask = DamageMaskMath.CreateGrid(sprite);
            DamageResult result = DamageMaskMath.ApplyImpactDamage(
                mask,
                0,
                2,
                new ImpactProfileDefinition
                {
                    Kernel = ImpactKernelShape.Point,
                    BaseCellsRemoved = 2,
                    BonusCellsPerDamage = 0,
                },
                1,
                20);

            Assert.Equal(2, result.CellsRemoved);
            Assert.False(mask.IsOccupied(0, 2));
            Assert.False(mask.IsOccupied(1, 2));
        }

        [Fact]
        public void PresentationTierRolloutFollowsChapterBands()
        {
            Assert.Equal(PresentationTier.Pixel2D, PresentationProgression.GetTierForStage(1));
            Assert.Equal(PresentationTier.VoxelShell, PresentationProgression.GetTierForStage(10));
            Assert.Equal(PresentationTier.VoxelShell, PresentationProgression.GetTierForStage(24));
            Assert.Equal(PresentationTier.HybridMesh, PresentationProgression.GetTierForStage(30));
            Assert.Equal(PresentationTier.Late3D, PresentationProgression.GetTierForStage(40));
        }

        [Fact]
        public void ChaseViewUnlockAndBossScaleProgressionIncreaseLateGamePresence()
        {
            Assert.False(PresentationProgression.IsChaseViewUnlocked(39));
            Assert.True(PresentationProgression.IsChaseViewUnlocked(40));
            Assert.True(PresentationProgression.GetBossPresentationScale(20) > PresentationProgression.GetBossPresentationScale(10));
            Assert.True(PresentationProgression.GetBossPresentationScale(30) > PresentationProgression.GetBossPresentationScale(20));
            Assert.True(PresentationProgression.GetBossPresentationScale(50) > PresentationProgression.GetBossPresentationScale(40));
            Assert.True(PresentationProgression.GetBossCoverageTarget(50) >= 0.5f);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Levels")) && File.Exists(Path.Combine(directory.FullName, "SpaceBurst.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the SpaceBurst repository root.");
        }
    }
}
