using Microsoft.Xna.Framework;

namespace SpaceBurst
{
    sealed class FeedbackDirector
    {
        private readonly CameraRig camera;
        private readonly AudioDirector audio;
        private float hudPulse;
        private float impactPulse;
        private float pickupPulse;
        private bool rewindLoopActive;

        public FeedbackDirector(CameraRig camera, AudioDirector audio)
        {
            this.camera = camera;
            this.audio = audio;
        }

        public float HudPulse
        {
            get { return hudPulse; }
        }

        public float ImpactPulse
        {
            get { return impactPulse; }
        }

        public float PickupPulse
        {
            get { return pickupPulse; }
        }

        public void Update(float deltaSeconds, GameAudioState state, ScreenShakeStrength shakeStrength)
        {
            hudPulse = System.Math.Max(0f, hudPulse - deltaSeconds * 1.65f);
            impactPulse = System.Math.Max(0f, impactPulse - deltaSeconds * 1.8f);
            pickupPulse = System.Math.Max(0f, pickupPulse - deltaSeconds * 1.45f);
            camera.Update(deltaSeconds, state.ScrollSpeed, state.TransitionWarpStrength, state.RewindStrength, shakeStrength);

            bool rewindActive = state.RewindStrength > 0.02f;
            if (rewindActive && !rewindLoopActive)
                Handle(new FeedbackEvent(FeedbackEventType.RewindStart, Vector2.Zero, state.RewindStrength));
            else if (!rewindActive && rewindLoopActive)
                Handle(new FeedbackEvent(FeedbackEventType.RewindStop, Vector2.Zero));

            rewindLoopActive = rewindActive;
            audio?.SetRewindAmount(state.RewindStrength);
        }

        public void Handle(FeedbackEvent feedbackEvent)
        {
            if (audio == null)
            {
                switch (feedbackEvent.Type)
                {
                    case FeedbackEventType.PlayerShot:
                        camera.AddKick(Vector2.UnitX, 1.4f + feedbackEvent.Intensity * 1.2f);
                        break;
                    case FeedbackEventType.EnemyHit:
                        camera.AddShake(0.05f + feedbackEvent.Intensity * 0.08f);
                        impactPulse = System.Math.Max(impactPulse, 0.18f + feedbackEvent.Intensity * 0.12f);
                        break;
                    case FeedbackEventType.EnemyDestroyed:
                        camera.AddShake(0.12f + feedbackEvent.Intensity * 0.18f);
                        impactPulse = System.Math.Max(impactPulse, 0.35f + feedbackEvent.Intensity * 0.16f);
                        break;
                    case FeedbackEventType.PlayerDamaged:
                        camera.AddShake(0.24f + feedbackEvent.Intensity * 0.2f);
                        hudPulse = System.Math.Max(hudPulse, 0.7f + feedbackEvent.Intensity * 0.2f);
                        impactPulse = System.Math.Max(impactPulse, 0.3f);
                        break;
                    case FeedbackEventType.Pickup:
                        pickupPulse = System.Math.Max(pickupPulse, 0.58f);
                        camera.AddShake(0.08f);
                        break;
                    case FeedbackEventType.Upgrade:
                        pickupPulse = System.Math.Max(pickupPulse, 0.82f);
                        hudPulse = System.Math.Max(hudPulse, 0.22f);
                        camera.AddShake(0.1f);
                        break;
                    case FeedbackEventType.BossEntry:
                        camera.AddShake(0.18f);
                        impactPulse = System.Math.Max(impactPulse, 0.42f);
                        break;
                    case FeedbackEventType.StageTransition:
                        impactPulse = System.Math.Max(impactPulse, 0.3f);
                        break;
                }

                return;
            }

            switch (feedbackEvent.Type)
            {
                case FeedbackEventType.PlayerShot:
                    camera.AddKick(Vector2.UnitX, 1.4f + feedbackEvent.Intensity * 1.2f);
                    audio.PlayPlayerShot(feedbackEvent.StyleId, feedbackEvent.Intensity);
                    break;
                case FeedbackEventType.EnemyHit:
                    camera.AddShake(0.05f + feedbackEvent.Intensity * 0.08f);
                    impactPulse = System.Math.Max(impactPulse, 0.18f + feedbackEvent.Intensity * 0.12f);
                    audio.PlayEnemyImpact(feedbackEvent.Intensity, feedbackEvent.Highlight);
                    break;
                case FeedbackEventType.EnemyDestroyed:
                    camera.AddShake(0.12f + feedbackEvent.Intensity * 0.18f);
                    impactPulse = System.Math.Max(impactPulse, 0.35f + feedbackEvent.Intensity * 0.16f);
                    audio.PlayExplosion(feedbackEvent.Intensity, feedbackEvent.Highlight);
                    break;
                case FeedbackEventType.PlayerDamaged:
                    camera.AddShake(0.24f + feedbackEvent.Intensity * 0.2f);
                    hudPulse = System.Math.Max(hudPulse, 0.7f + feedbackEvent.Intensity * 0.2f);
                    impactPulse = System.Math.Max(impactPulse, 0.3f);
                    audio.PlayPlayerDamaged();
                    break;
                case FeedbackEventType.Pickup:
                    pickupPulse = System.Math.Max(pickupPulse, 0.58f);
                    camera.AddShake(0.08f);
                    audio.PlayPickup(feedbackEvent.StyleId, feedbackEvent.Highlight);
                    break;
                case FeedbackEventType.Upgrade:
                    pickupPulse = System.Math.Max(pickupPulse, 0.82f);
                    hudPulse = System.Math.Max(hudPulse, 0.22f);
                    camera.AddShake(0.1f);
                    audio.PlayUpgrade(feedbackEvent.StyleId);
                    break;
                case FeedbackEventType.BossEntry:
                    camera.AddShake(0.18f);
                    impactPulse = System.Math.Max(impactPulse, 0.42f);
                    audio.PlayBossCue();
                    break;
                case FeedbackEventType.StageTransition:
                    impactPulse = System.Math.Max(impactPulse, 0.3f);
                    audio.PlayTransitionWhoosh();
                    break;
                case FeedbackEventType.RewindStart:
                    audio.StartRewindLoop();
                    break;
                case FeedbackEventType.RewindStop:
                    audio.StopRewindLoop();
                    break;
                case FeedbackEventType.UiConfirm:
                    audio.PlayUiConfirm();
                    break;
                case FeedbackEventType.UiCancel:
                    audio.PlayUiCancel();
                    break;
            }
        }
    }
}
