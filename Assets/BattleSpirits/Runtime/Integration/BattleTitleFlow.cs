using System;

namespace LaoDai.BattleSpirits.Integration
{
    public enum BattleTitlePhase
    {
        Loading,
        WaitingForStart,
        Revealing,
        Complete
    }

    /// <summary>
    /// Pure title-screen timing. Input during loading is never carried into the
    /// title: readiness must be followed by a neutral frame and a fresh press.
    /// </summary>
    public sealed class BattleTitleFlow
    {
        public const float MinimumTitleSeconds = 0.35f;
        public const float RevealSeconds = 0.65f;
        public const float MaximumStepSeconds = 0.1f;

        private float titleSeconds;
        private float revealSeconds;
        private bool inputReleased;

        public BattleTitlePhase Phase { get; private set; } = BattleTitlePhase.Loading;

        public float RevealProgress
        {
            get
            {
                if (Phase == BattleTitlePhase.Complete) return 1f;
                float t = Math.Min(1f, revealSeconds / RevealSeconds);
                return t * t * (3f - 2f * t);
            }
        }

        public void Ready()
        {
            if (Phase == BattleTitlePhase.Loading)
                Phase = BattleTitlePhase.WaitingForStart;
        }

        public void Advance(float unscaledDeltaTime, bool startPressed, bool inputHeld)
        {
            float step = float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime)
                ? 0f : Math.Max(0f, Math.Min(MaximumStepSeconds, unscaledDeltaTime));

            if (Phase == BattleTitlePhase.WaitingForStart)
            {
                titleSeconds += step;
                if (!inputHeld && !startPressed) inputReleased = true;
                if (inputReleased && titleSeconds >= MinimumTitleSeconds && startPressed)
                    Phase = BattleTitlePhase.Revealing;
            }
            else if (Phase == BattleTitlePhase.Revealing)
            {
                revealSeconds = Math.Min(RevealSeconds, revealSeconds + step);
                if (revealSeconds >= RevealSeconds) Phase = BattleTitlePhase.Complete;
            }
        }
    }
}
