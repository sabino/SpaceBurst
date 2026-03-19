using Microsoft.Xna.Framework;
using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    enum FeedbackEventType
    {
        PlayerShot,
        EnemyHit,
        EnemyDestroyed,
        PlayerDamaged,
        Pickup,
        Upgrade,
        BossEntry,
        StageTransition,
        RewindStart,
        RewindStop,
        UiConfirm,
        UiCancel,
    }

    readonly struct FeedbackEvent
    {
        public FeedbackEvent(FeedbackEventType type, Vector2 position, float intensity = 1f, WeaponStyleId styleId = WeaponStyleId.Pulse, bool highlight = false)
        {
            Type = type;
            Position = position;
            Intensity = intensity;
            StyleId = styleId;
            Highlight = highlight;
        }

        public FeedbackEventType Type { get; }
        public Vector2 Position { get; }
        public float Intensity { get; }
        public WeaponStyleId StyleId { get; }
        public bool Highlight { get; }
    }
}
