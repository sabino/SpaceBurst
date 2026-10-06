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
