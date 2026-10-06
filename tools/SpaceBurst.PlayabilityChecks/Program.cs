using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using SpaceBurst;
using SpaceBurst.RuntimeData;

// Run the production game host with a real graphics device and isolated storage.
// These checks exercise private orchestration paths that unit tests cannot cover.
string storagePath = Path.Combine(Path.GetTempPath(), "spaceburst-checks-" + Guid.NewGuid());
try
{
    PlatformServices.Initialize(PlatformCapabilities.CreateDesktop(),
        new FileStorageBackend(storagePath, null),
        PlatformServices.CreateDefaultTextAssetProvider(),
        PlatformServices.CreateImmediateAudioStartGate());
    PersistentStorage.SaveOptions(new OptionsData
    {
        DisplayMode = DesktopDisplayMode.Windowed,
        MasterVolume = 0,
        TutorialCompleted = true,
    });
    Texture2D sharedTexture;
    Texture2D playerTexture;
    using (var game = new PlayabilityCheckGame())
    {
        game.Run();
        if (!game.Completed)
            throw new InvalidOperationException("Game host exited before checks completed.");
        sharedTexture = game.SharedTexture;
        playerTexture = game.PlayerTexture;
    }
    if (!sharedTexture.IsDisposed || !playerTexture.IsDisposed)
        throw new InvalidOperationException("Host shutdown did not release cached/player textures.");
    Console.WriteLine("PASS: Host shutdown releases cached projectiles and persistent player textures");
}
finally
{
    if (Directory.Exists(storagePath))
        Directory.Delete(storagePath, true);
}

sealed class PlayabilityCheckGame : Game1
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private double bootSeconds;
    public bool Completed { get; private set; }
    public Texture2D SharedTexture { get; private set; }
    public Texture2D PlayerTexture { get; private set; }

    protected override void Update(GameTime time)
    {
        base.Update(time);
        bootSeconds += time.ElapsedGameTime.TotalSeconds;
        if (!(bool)Get(this, "bootComplete", typeof(Game1)))
        {
            if (bootSeconds > 60)
                throw new InvalidOperationException("Boot did not complete within 60 simulated seconds.");
            return;
        }

        var director = (CampaignDirector)Get(this, "campaignDirector", typeof(Game1));
        CheckTutorialCompletion(director);
        CheckCampaignState(director);
        CheckDraftChoices(director);
        CheckRunRewards(director);
        CheckSurvivalEconomy(director);
        CheckResourceOwnership(director);
        CheckCampaignLiveness(director);
        PlayerTexture = Player1.Instance.SpriteInstance.Texture;
        Completed = true;
        Exit();
    }

    private static void CheckTutorialCompletion(CampaignDirector director)
    {
        double seconds = 0;
        foreach (int choice in new[] { 0, 1, 2 })
        {
            typeof(CampaignDirector).GetMethods(PrivateInstance)
                .Single(method => method.Name == "StartTutorial" && method.GetParameters().Length == 3)
                .Invoke(director, new object[] { false, GameFlowState.Playing, GameDifficulty.Normal });
            KeyboardState previous = new KeyboardState();
            bool completed = false;
            for (int frame = 0; frame < 10800; frame++)
            {
                TutorialStep step = (TutorialStep)Get(director, "tutorialStep");
                var keys = new List<Keys>();
                bool fire = false;
                bool rewind = false;
                if (director.CurrentState == GameFlowState.UpgradeDraft)
                {
                    keys.Add(new[] { Keys.A, Keys.S, Keys.D }[choice]);
                }
                else
                {
                    switch (step)
                    {
                        case TutorialStep.Move:
                            keys.Add(Keys.D);
                            break;
                        case TutorialStep.Aim:
                            keys.Add(Keys.Right);
                            break;
                        case TutorialStep.Fire:
                            keys.Add(Keys.Right);
                            keys.Add(Keys.Space);
                            fire = true;
                            Enemy target = EntityManager.Enemies.FirstOrDefault();
                            if (target != null)
                                Steer(keys, new Vector2(0, target.Position.Y - Player1.Instance.Position.Y));
                            break;
                        case TutorialStep.Rewind:
                            keys.Add(Keys.R);
                            rewind = true;
                            break;
                        case TutorialStep.CollectPower:
                            // Keep holding R after the lesson completes: it must not undo progress.
                            keys.Add(Keys.R);
                            rewind = true;
                            PowerupPickup pickup = EntityManager.Powerups.FirstOrDefault();
                            if (pickup != null)
                                Steer(keys, pickup.Position - Player1.Instance.Position);
                            break;
                        case TutorialStep.SwitchStyle:
                            if (!previous.IsKeyDown(Keys.E))
                                keys.Add(Keys.E);
                            break;
                        case TutorialStep.ShipsAndLives:
                            keys.Add(Keys.Enter);
                            break;
                    }
                }
                var current = new KeyboardState(keys.ToArray());
                typeof(Input).GetField("lastKeyboardState", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previous);
                typeof(Input).GetField("keyboardState", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, current);
                typeof(Input).GetField("fireHeld", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, fire);
                typeof(Input).GetField("rewindHeld", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, rewind);
                previous = current;
                seconds += 1.0 / 60;
                typeof(Game1).GetProperty("GameTime").SetValue(null,
                    new GameTime(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(1.0 / 60)));
                director.Update();
                if (director.CurrentState != GameFlowState.Tutorial && director.CurrentState != GameFlowState.UpgradeDraft)
                {
                    completed = true;
                    break;
                }
            }
            Require(completed && director.CurrentStageNumber == 1 && ((OptionsData)Get(director, "options")).TutorialCompleted,
                $"Prompt-following tutorial completes into stage 1 with draft choice {choice + 1}");
        }
        SetKeys();
    }

    private static void Steer(List<Keys> keys, Vector2 delta)
    {
        if (delta.X > 8) keys.Add(Keys.D);
        else if (delta.X < -8) keys.Add(Keys.A);
        if (delta.Y > 8) keys.Add(Keys.S);
        else if (delta.Y < -8) keys.Add(Keys.W);
    }

    private static void CheckRunRewards(CampaignDirector director)
    {
        director.TryConsoleLoadStage(1);
        PlayerStatus.BeginCampaign(new StageDefinition(), GameDifficulty.Normal);
        Set(director, "state", GameFlowState.Playing);
        Player1.Instance.RefreshLoadout();
        int ships = PlayerStatus.Ships;
        using (var first = PowerupPickup.CreateScrapCache(Player1.Instance.Position, 4))
            Player1.Instance.CollectPowerup(first);
        Require(PlayerStatus.Ships == ships, "Partial salvage does not grant an early ship");
        using (var next = PowerupPickup.CreateScrapCache(Player1.Instance.Position, 1))
            Player1.Instance.CollectPowerup(next);
        Require(PlayerStatus.Ships == ships + 1, "Five collected scrap grant an actual spare ship");
        PlayerStatus.RunProgress.AddScrap(4);
        var controller = new RunProgressionController();
        float meter = 1;
        controller.ApplyDraftSelection(PlayerStatus.RunProgress,
            new UpgradeDraftCard { Type = UpgradeCardType.ScrapCache, RewardAmount = 2 }, ref meter, 8);
        Require(PlayerStatus.Ships == ships + 2 && PlayerStatus.RunProgress.ScrapTowardNextShip == 1,
            "Draft salvage grants the same threshold reward and carries its remainder");
        Player1.Instance.CollectPowerup(WeaponStyleId.Fortress);
        Require(PlayerStatus.RunProgress.RunXp == 2 && PlayerStatus.RunProgress.StoredUpgradeCharges == 0
            && !PlayerStatus.RunProgress.Weapons.OwnsStyle(WeaponStyleId.Fortress),
            "Campaign spare cores grant XP without banking dead charges or bypassing chapters");
        Player1.Instance.CollectPowerup(WeaponStyleId.Pulse);
        Require(Player1.Instance.ActiveWeaponLevel == 1 && PlayerStatus.RunProgress.RunXp == 2,
            "Matching cores retain their immediate weapon upgrade");
        Set(director, "state", GameFlowState.Tutorial);
        Player1.Instance.CollectPowerup(WeaponStyleId.Spread);
        Require(PlayerStatus.RunProgress.GetStoredCharge(WeaponStyleId.Spread) == 1,
            "Tutorial cores preserve the practice draft charge");
        Set(director, "state", GameFlowState.Playing);
    }

    private static void CheckCampaignState(CampaignDirector director)
    {
        director.TryConsoleLoadStage(1);
        Set(director, "state", GameFlowState.Playing);
        var repository = (CampaignRepository)Get(director, "repository");
        EnemyArchetypeDefinition archetype = repository.ArchetypesById["Destroyer"];
        EntityManager.Add(new Enemy(archetype, new Vector2(900, 320), 320,
            archetype.MovePattern, archetype.FirePattern, 1, 0, 1));
        var snapshot = (RunSaveData)Call(director, "CaptureRunSaveData", 0, false);
        uint rngState = snapshot.GameplayRngState;
        Call(director, "RestoreRunSaveData", snapshot, false, true);
        Require(director.GameplayRandom.State == rngState, "Restoring enemies preserves the gameplay RNG stream");
        Require(EntityManager.Enemies.Count() == 1, "Enemy survives snapshot reconstruction");

        var frames = (IList)Get(director, "rewindFrames");
        frames.Clear();
        frames.Add(snapshot);
        frames.Add(snapshot);
        Call(director, "UpdateRewind", 0.1f);
        Require(!PlayerStatus.RunProgress.MedalEligible, "Rewind permanently disqualifies medals");
        Call(director, "UpdateRewind", 0.1f);
        Require(!PlayerStatus.RunProgress.MedalEligible, "Repeated rewind cannot restore medal eligibility");

        snapshot.RewindMeterSeconds = 0;
        Call(director, "RestoreRunSaveData", snapshot, true, false);
        Require((float)Get(director, "rewindMeterSeconds") == 0, "Load preserves an empty rewind meter");
        snapshot.RewindMeterSeconds = 2.25f;
        Call(director, "RestoreRunSaveData", snapshot, true, false);
        Require((float)Get(director, "rewindMeterSeconds") == 2.25f, "Load preserves a partially spent rewind meter");

        Set(director, "stageHadDeath", true);
        Set(director, "campaignHadDeath", true);
        Call(director, "StartStageFromTransition", 2);
        Require(!(bool)Get(director, "stageHadDeath"), "New stage resets its own no-death tracking");
        Require((bool)Get(director, "campaignHadDeath"), "New stage preserves campaign death history");

        // Holding an unavailable rewind must not freeze the simulation for free.
        Set(director, "rewindMeterSeconds", 0f);
        typeof(Input).GetField("rewindHeld", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        float before = (float)Get(director, "stageElapsedSeconds");
        director.Update();
        Require((float)Get(director, "stageElapsedSeconds") > before, "Empty rewind does not freeze gameplay");
        typeof(Input).GetField("rewindHeld", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
    }

    private static void CheckDraftChoices(CampaignDirector director)
    {
        PlayerStatus.BeginCampaign(new StageDefinition(), GameDifficulty.Normal);
        PlayerStatus.RunProgress.AddXp(6);
        SetKeys();
        Call(director, "OpenUpgradeDraft", GameFlowState.Playing, false);
        int pending = PlayerStatus.RunProgress.PendingLevelUps;
        for (int frame = 0; frame < 1200; frame++)
            Call(director, "UpdateUpgradeDraft");
        Require(director.CurrentState == GameFlowState.UpgradeDraft && PlayerStatus.RunProgress.PendingLevelUps == pending,
            "Manual draft waits without spending the pending upgrade");

        SetKeys(Keys.Escape);
        Call(director, "UpdateUpgradeDraft");
        Require(director.CurrentState == GameFlowState.Paused && PlayerStatus.RunProgress.PendingLevelUps == pending,
            "Escape pauses a draft without choosing a card");
        var draftSave = (RunSaveData)Call(director, "CaptureRunSaveData", 0, false);
        Require(draftSave.State == GameFlowState.UpgradeDraft && draftSave.DraftCards.Count == 3,
            "Paused draft captures its logical state and cards");
        PersistentStorage.SaveRunSlot(1, draftSave);
        RunSaveData loadedDraft = PersistentStorage.LoadRunSlot(1);
        Require(loadedDraft != null, "Pending draft survives sealed file save/load");
        SetKeys();
        Call(director, "RestoreRunSaveData", loadedDraft, true, false);
        Require(director.CurrentState == GameFlowState.Paused && (GameFlowState)Get(director, "pauseReturnState") == GameFlowState.UpgradeDraft,
            "Loaded draft stays paused with the choice intact");
        SetKeys(Keys.Escape);
        Call(director, "UpdatePause");
        Require(director.CurrentState == GameFlowState.UpgradeDraft, "Resume returns to the saved draft");
        SetKeys();

        var cards = (List<UpgradeDraftCard>)Get(director, "draftCards");
        var options = (OptionsData)Get(director, "options");
        foreach (int percent in new[] { 70, 100, 150, 220 })
        {
            options.UiScalePercent = percent;
            BitmapFontRenderer.GlobalScaleMultiplier = UiScaleHelper.GetUiTextMultiplier(percent);
            Rectangle[] bounds = (Rectangle[])Call(director, "GetUpgradeDraftCardBounds");
            foreach (Rectangle cardBounds in bounds)
                Require(Game1.SafeUiBounds.Contains(cardBounds), "Draft interactive bounds fit the safe screen");
            foreach (WeaponEvolutionDefinition evolution in WeaponProgressionCatalog.Evolutions)
            {
                Rectangle textBounds = new Rectangle(0, 0, bounds[0].Width - 32, bounds[0].Height - 182);
                object[] args = { evolution.Description, textBounds, 0f };
                string text = (string)typeof(CampaignDirector).GetMethod("FitDraftDescription", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
                Vector2 size = (Vector2)typeof(CampaignDirector).GetMethod("MeasureDraftDescription", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { text, args[2] });
                Require(size.X <= textBounds.Width && size.Y <= textBounds.Height, "Evolution description fits inside its card");
            }
        }
        BitmapFontRenderer.GlobalScaleMultiplier = 1f;
        options.UiScalePercent = 100;
        Rectangle pause = (Rectangle)Call(director, "GetUpgradeDraftPauseBounds");
        Click(director, new Vector2(pause.Center.X, pause.Center.Y));
        Require(director.CurrentState == GameFlowState.Paused && PlayerStatus.RunProgress.PendingLevelUps == pending,
            "Pointer pause button preserves the pending upgrade");
        SetKeys(Keys.Escape);
        Call(director, "UpdatePause");
        SetKeys();

        options.AutoUpgradeDraft = true;
        Set(director, "draftTimer", 0.001f);
        Call(director, "UpdateUpgradeDraft");
        Require(PlayerStatus.RunProgress.PendingLevelUps == pending - 1, "Opt-in auto draft still selects on timeout");
        options.AutoUpgradeDraft = false;
        Call(director, "RestoreRunSaveData", loadedDraft, true, false);
        Set(director, "state", GameFlowState.UpgradeDraft);
        Rectangle card = ((Rectangle[])Call(director, "GetUpgradeDraftCardBounds"))[0];
        Click(director, new Vector2(card.X + 1, card.Y + 1));
        Require(PlayerStatus.RunProgress.PendingLevelUps == pending - 1,
            "Clicking the visible top edge of a card selects the upgrade");

        Set(director, "state", GameFlowState.Paused);
        Set(director, "pauseReturnState", GameFlowState.Tutorial);
        var tutorialSave = (RunSaveData)Call(director, "CaptureRunSaveData", 0, false);
        Require(tutorialSave.State == GameFlowState.Tutorial, "Paused tutorial captures its logical state");
        Call(director, "RestoreRunSaveData", tutorialSave, true, false);
        Require((GameFlowState)Get(director, "pauseReturnState") == GameFlowState.Tutorial,
            "Loaded tutorial resumes tutorial flow");
    }

    private static void CheckSurvivalEconomy(CampaignDirector director)
    {
        director.TryConsoleLoadStage(1);
        PlayerStatus.BeginCampaign(new StageDefinition(), GameDifficulty.Normal);
        Player1.Instance.ResetForStage();
        PlayerStatus.GrantLife(int.MaxValue);
        PlayerStatus.GrantShips(int.MaxValue);
        Require(PlayerStatus.Lives == PlayerStatus.MaximumLives && PlayerStatus.Ships == PlayerStatus.MaximumShips,
            "Life and ship rewards remain within stock limits without integer overflow");
        PlayerStatus.AddPoints(3000);
        for (int death = 0; death < PlayerStatus.MaximumShips; death++)
            Require(PlayerStatus.ConsumeDeath(null) == PlayerDeathOutcome.RespawnInPlace, "Spare ships provide in-place respawns");
        Require(PlayerStatus.ConsumeDeath(null) == PlayerDeathOutcome.RestartStage && PlayerStatus.Lives == 8,
            "Exhausting spare ships spends a life and restarts the stage");
        PlayerStatus.AddPoints(1);
        Require(PlayerStatus.Lives == 8, "A full stock advances score thresholds without banking old life awards");
        PlayerStatus.AddPoints(2999);
        Require(PlayerStatus.Lives == 9, "A new score threshold still replenishes a spent life");
        PlayerStatus.AddPoints(int.MaxValue);
        Require(PlayerStatus.Score == int.MaxValue && PlayerStatus.Lives == 9, "Large score awards saturate safely");
        var snapshot = PlayerStatus.CaptureSnapshot();
        snapshot.Lives = 186;
        snapshot.Ships = 100;
        PersistentStorage.SaveRunSlot(2, new RunSaveData { PlayerStatus = snapshot });
        RunSaveData loaded = PersistentStorage.LoadRunSlot(2);
        Require(loaded != null && loaded.PlayerStatus.Lives == 9 && loaded.PlayerStatus.Ships == 9,
            "Sealed save/load normalizes surplus survival stock");
        snapshot.Lives = 186;
        snapshot.Ships = 100;
        PlayerStatus.RestoreSnapshot(snapshot);
        Require(PlayerStatus.Lives == 9 && PlayerStatus.Ships == 9, "Direct snapshot restoration enforces stock limits");
        snapshot = PlayerStatus.CaptureSnapshot();
        snapshot.Lives = 8;
        PlayerStatus.RestoreSnapshot(snapshot);
        PlayerStatus.AddPoints(1);
        Require(PlayerStatus.Lives == 8, "Exhausted integer score ceiling cannot mint repeated lives");
        PlayerStatus.BeginCampaign(new StageDefinition(), GameDifficulty.Normal);
        int restarts = 0;
        while (!PlayerStatus.IsGameOver)
        {
            if (PlayerStatus.ConsumeDeath(null) == PlayerDeathOutcome.RestartStage)
                restarts++;
        }
        Require(restarts == 1, "Deaths eventually reach game over after the default Normal stage retry");
    }

    private void CheckResourceOwnership(CampaignDirector director)
    {
        Set(director, "state", GameFlowState.Title);
        director.TryConsoleLoadStage(1);
        var repository = (CampaignRepository)Get(director, "repository");
        EnemyArchetypeDefinition archetype = repository.ArchetypesById["Destroyer"];
        Enemy CreateEnemy() => new Enemy(archetype, new Vector2(1100, 180), 180,
            archetype.MovePattern, archetype.FirePattern, 1, 0, 1);

        var expired = CreateEnemy();
        Texture2D expiredTexture = expired.SpriteInstance.Texture;
        EntityManager.Add(expired);
        expired.IsExpired = true;
        EntityManager.Update();
        Require(expiredTexture.IsDisposed, "Expired enemies release their owned textures");

        Texture2D oldHull = Player1.Instance.SpriteInstance.Texture;
        Texture2D oldCannon = Player1.Instance.CannonSpriteInstance.Texture;
        Player1.Instance.RefreshLoadout();
        Require(oldHull.IsDisposed && oldCannon.IsDisposed, "Loadout refresh releases replaced hull/cannon textures");
        Texture2D retainedHull = Player1.Instance.SpriteInstance.Texture;
        var resetEnemy = CreateEnemy();
        Texture2D resetTexture = resetEnemy.SpriteInstance.Texture;
        EntityManager.Add(resetEnemy);
        EntityManager.Reset();
        Require(resetTexture.IsDisposed && !retainedHull.IsDisposed, "Reset releases enemies while preserving the reusable player");
        EntityManager.Add(Player1.Instance);

        Bullet CreateBullet() => new Bullet(new Vector2(600, 500), Vector2.UnitX, true, 1,
            null, WeaponCatalog.CreateProjectileDefinition(WeaponStyleId.Pulse, 0, true), 0, 4f, 0f);
        var first = CreateBullet();
        var second = CreateBullet();
        SharedTexture = first.SpriteInstance.Texture;
        Require(ReferenceEquals(first.SpriteInstance, second.SpriteInstance), "Matching projectile specifications share an immutable sprite");
        first.Dispose();
        Require(!SharedTexture.IsDisposed, "Disposing one projectile does not invalidate another projectile");
        EntityManager.Add(second);
        EntityManager.Add(CreateEnemy());
        Set(director, "state", GameFlowState.Playing);
        var snapshot = (RunSaveData)Call(director, "CaptureRunSaveData", 0, false);
        int cacheCount = ProjectileSprites.Count;
        for (int iteration = 0; iteration < 100; iteration++)
        {
            Texture2D previousEnemy = EntityManager.Enemies.First().SpriteInstance.Texture;
            Call(director, "RestoreRunSaveData", snapshot, true, true);
            if (!previousEnemy.IsDisposed)
                throw new InvalidOperationException("Repeated rewind reconstruction retains old enemy textures.");
        }
        Require(ProjectileSprites.Count == cacheCount && !SharedTexture.IsDisposed,
            "Repeated rewind reuses projectile textures and releases replaced enemy textures");

        for (int shot = 0; shot < 1000; shot++)
            CreateBullet().Dispose();
        Require(ProjectileSprites.Count == cacheCount, "A thousand matching shots do not allocate a thousand GPU sprites");

        WorldPresentationRenderer.ClearCache();
        Late3DRenderer.ReleaseResources();
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        for (int shape = 0; shape < 260; shape++)
        {
            using var sprite = new ProceduralSpriteInstance(GraphicsDevice, new ProceduralSpriteDefinition
            {
                Id = "CacheBudget" + shape,
                Rows = new List<string> { "##", "##" },
                PrimaryColor = "#FFFFFF", SecondaryColor = "#FFFFFF", AccentColor = "#FFFFFF",
            });
            typeof(WorldPresentationRenderer).GetMethod("GetHullCache", flags).Invoke(null, new object[] { sprite });
            typeof(Late3DRenderer).GetMethod("GetMeshCache", flags).Invoke(null, new object[] { sprite });
        }
        using var pale = new ProceduralSpriteInstance(GraphicsDevice, new ProceduralSpriteDefinition
        {
            Id = "PaletteCheck", Rows = new List<string> { "##", "##" }, PrimaryColor = "#FFFFFF",
        });
        using var red = new ProceduralSpriteInstance(GraphicsDevice, new ProceduralSpriteDefinition
        {
            Id = "PaletteCheck", Rows = new List<string> { "##", "##" }, PrimaryColor = "#FF0000",
        });
        Require(pale.RenderStateKey != red.RenderStateKey, "Procedural render cache keys distinguish palette changes");
        var hulls = (IDictionary)typeof(WorldPresentationRenderer).GetField("hullCacheByKey", flags).GetValue(null);
        var meshes = (IDictionary)typeof(Late3DRenderer).GetField("cacheByKey", flags).GetValue(null);
        Require(hulls.Count <= WorldPresentationRenderer.MaximumCachedHulls && meshes.Count <= Late3DRenderer.MaximumCachedMeshes,
            "Procedural damaged-shape caches stay within bounded budgets");
    }

    private static void CheckCampaignLiveness(CampaignDirector director)
    {
        foreach (GameDifficulty difficulty in new[] { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard, GameDifficulty.Insane, GameDifficulty.Realistic })
            RunControlledCampaign(director, difficulty);
    }

    private static void RunControlledCampaign(CampaignDirector director, GameDifficulty difficulty)
    {
        // This is a structural liveness test: invulnerability and scripted damage
        // ensure threats clear. It does not measure human skill, balance, or fun.
        SetKeys();
        Set(director, "state", GameFlowState.Title);
        ((OptionsData)Get(director, "options")).LastSelectedDifficulty = difficulty;
        director.TryConsoleLoadStage(1);
        Set(director, "state", GameFlowState.Playing);
        var stages = new HashSet<int>();
        var bosses = new HashSet<int>();
        int drafts = 0;
        for (int frame = 1; frame <= 432000; frame++)
        {
            typeof(Game1).GetProperty("GameTime").SetValue(null,
                new GameTime(TimeSpan.FromSeconds(frame / 60.0), TimeSpan.FromSeconds(1.0 / 60)));
            stages.Add(director.CurrentStageNumber);
            if (director.CurrentState == GameFlowState.UpgradeDraft)
            {
                Call(director, "ApplyDraftSelection", 0);
                drafts++;
            }
            else
            {
                Player1.Instance.MakeInvulnerable(2f);
                director.Update();
            }
            foreach (Enemy enemy in EntityManager.Enemies.ToArray())
            {
                if (enemy.IsExpired || enemy.Travel >= Game1.VirtualWidth - 80)
                    continue;
                if (enemy.IsBoss)
                    bosses.Add(director.CurrentStageNumber);
                enemy.ApplyBeamHit(enemy.Position, 999, new ImpactProfileDefinition
                {
                    BaseCellsRemoved = 512, BonusCellsPerDamage = 1,
                    SplashRadius = 64, SplashPercent = 100,
                });
            }
            if (director.CurrentState == GameFlowState.CampaignComplete)
            {
                Require(director.CurrentStageNumber == 50 && stages.SetEquals(Enumerable.Range(1, 50)) && bosses.SetEquals(new[] { 10, 20, 30, 40, 50 })
                    && PlayerStatus.RunProgress.Difficulty == difficulty && PlayerStatus.Lives <= PlayerStatus.MaximumLives,
                    $"Controlled {difficulty} simulation reaches all 50 stages/five bosses and the ending with bounded stock");
                Console.WriteLine($"LIVENESS: difficulty={difficulty}, score={PlayerStatus.Score}, seconds={frame / 60.0:F1}, drafts={drafts}, lives={PlayerStatus.Lives}, cachedProjectileSprites={Game1.Instance.ProjectileSprites.Count}");
                return;
            }
            if (director.CurrentState == GameFlowState.GameOver)
                throw new InvalidOperationException("Controlled campaign unexpectedly reached game over.");
        }
        throw new InvalidOperationException("Campaign stalled before completion within 7200 simulated seconds.");
    }

    private static void Click(CampaignDirector director, Vector2 point)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        void SetPointer(string field, object value) => typeof(Input).GetField(field, flags).SetValue(null, value);
        SetPointer("pointerPosition", point);
        SetPointer("uiPointerPressPosition", point);
        SetPointer("uiPointerPressed", true);
        SetPointer("uiPointerReleased", false);
        SetPointer("uiPointerDragging", false);
        Call(director, "UpdateUpgradeDraft");
        SetPointer("uiPointerPressed", false);
        SetPointer("uiPointerReleased", true);
        SetPointer("uiPointerReleasePosition", point);
        Call(director, "UpdateUpgradeDraft");
        SetPointer("uiPointerReleased", false);
    }

    private static void SetKeys(params Keys[] keys)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(Input).GetField("keyboardState", flags).SetValue(null, new KeyboardState(keys));
        typeof(Input).GetField("lastKeyboardState", flags).SetValue(null, new KeyboardState());
        ((HashSet<Keys>)typeof(Input).GetField("consumedKeys", flags).GetValue(null)).Clear();
    }

    private static object Get(object target, string field, Type owner = null) =>
        (owner ?? target.GetType()).GetField(field, PrivateInstance).GetValue(target);
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, PrivateInstance).Invoke(target, args);
    private static void Require(bool passed, string description)
    {
        if (!passed)
            throw new InvalidOperationException(description);
        Console.WriteLine("PASS: " + description);
    }
}
