using System;

namespace LaoDai.BattleSpirits.Visuals
{
    // Animation curves are independent of frame rate and can be checked outside Unity.
    public enum BattleVisualPose { Idle, Summon, Attack, Block, Damage, Death, Victory, Defeat }

    public readonly struct BattleVisualSample
    {
        public readonly float height, scale, advance, tilt, wing, glow;
        public BattleVisualSample(float height, float scale, float advance, float tilt, float wing, float glow)
        { this.height = height; this.scale = scale; this.advance = advance; this.tilt = tilt; this.wing = wing; this.glow = glow; }
    }

    public static class BattleVisualMotion
    {
        public static float Duration(BattleVisualPose pose)
        {
            switch (pose)
            {
                case BattleVisualPose.Summon: return 1.1f;
                case BattleVisualPose.Attack: return 0.65f;
                case BattleVisualPose.Block: return 0.45f;
                case BattleVisualPose.Damage: return 0.5f;
                case BattleVisualPose.Death: return 0.7f;
                case BattleVisualPose.Victory: return 1.6f;
                case BattleVisualPose.Defeat: return 1.2f;
                default: return 0f;
            }
        }

        public static BattleVisualSample Sample(BattleVisualPose pose, float age, float clock)
        {
            if (float.IsNaN(age) || float.IsInfinity(age)) age = 0;
            if (float.IsNaN(clock) || float.IsInfinity(clock)) clock = 0;
            float duration = Duration(pose);
            float t = duration == 0 ? 1 : Math.Max(0, Math.Min(1, age / duration));
            float pulse = (float)Math.Sin(t * Math.PI);
            // Multiply in double precision: a finite float clock near MaxValue
            // must not overflow to Infinity before reaching Sin.
            float idle = (float)Math.Sin((double)clock * 1.7);
            float wing = (float)Math.Sin((double)clock * 2.8) * 8f;
            float height = idle * 0.06f, scale = 1, advance = 0, tilt = 0, glow = 0;
            switch (pose)
            {
                case BattleVisualPose.Summon:
                    float ease = 1 - (1 - t) * (1 - t) * (1 - t);
                    scale = 0.02f + 0.98f * ease; height += (1 - ease) * 2.5f + pulse * 0.35f;
                    wing += pulse * 35; glow = 1 - t; break;
                case BattleVisualPose.Attack:
                    advance = pulse * 3; height += pulse * 0.45f; tilt = -pulse * 22;
                    wing += pulse * 38; glow = pulse; break;
                case BattleVisualPose.Block:
                    advance = pulse * 0.5f; tilt = pulse * 12; wing -= pulse * 22; glow = pulse * 0.65f; break;
                case BattleVisualPose.Damage:
                    advance = -pulse * 0.6f; tilt = (float)Math.Sin(t * Math.PI * 5) * (1 - t) * 12;
                    glow = pulse; break;
                case BattleVisualPose.Death:
                    scale = Math.Max(0.01f, 1 - t); height += t * 1.1f; tilt = t * 65; glow = 1 - t; break;
                case BattleVisualPose.Victory:
                    height += Math.Abs((float)Math.Sin(t * Math.PI * 3)) * (1 - t) * 0.7f;
                    wing += pulse * 35; glow = pulse * 0.7f; break;
                case BattleVisualPose.Defeat:
                    tilt = t * 22; height -= t * 0.25f; wing -= t * 18; break;
            }
            return new BattleVisualSample(height, scale, advance, tilt, wing, glow);
        }
    }
}
