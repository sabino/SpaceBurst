using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
    using var game = new PlayabilityCheckGame();
    game.Run();
    if (!game.Completed)
        throw new InvalidOperationException("Game host exited before checks completed.");
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
        CheckCampaignState(director);
        CheckDraftChoices(director);
        Completed = true;
        Exit();
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
