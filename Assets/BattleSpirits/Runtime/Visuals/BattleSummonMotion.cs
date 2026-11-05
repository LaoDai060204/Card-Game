using System;

namespace LaoDai.BattleSpirits.Visuals
{
    public readonly struct BattleSummonSample
    {
        public readonly float backdropAlpha, cardAlpha, cardScale, cardFlipY, cardTilt;
        public readonly float illustrationAlpha, illustrationScale, illustrationRise;
        public readonly float flashAlpha, sweep, glow, nameAlpha, landing;

        public BattleSummonSample(float backdropAlpha, float cardAlpha, float cardScale,
            float cardFlipY, float cardTilt, float illustrationAlpha, float illustrationScale,
            float illustrationRise, float flashAlpha, float sweep, float glow,
            float nameAlpha, float landing)
        {
            this.backdropAlpha = backdropAlpha; this.cardAlpha = cardAlpha;
            this.cardScale = cardScale; this.cardFlipY = cardFlipY; this.cardTilt = cardTilt;
            this.illustrationAlpha = illustrationAlpha; this.illustrationScale = illustrationScale;
            this.illustrationRise = illustrationRise; this.flashAlpha = flashAlpha;
            this.sweep = sweep; this.glow = glow; this.nameAlpha = nameAlpha; this.landing = landing;
        }
    }

    // Pure, frame-independent presentation curves. They never advance game rules.
    public static class BattleSummonMotion
    {
        public const float Duration = 1.95f;
        // The destination is already projected by the field camera. Fit the
        // original portrait first, then derive its tilt from that destination;
        // applying a fixed tilt to the projected height would shrink it twice.
        public static void GetLanding(float width, float projectedHeight, float canvasAspect,
            float imageAspect, out float slotHeight, out float tiltDegrees)
        {
            slotHeight = tiltDegrees = 0;
            if (!PositiveFinite(width) || !PositiveFinite(projectedHeight)) return;
            if (!PositiveFinite(canvasAspect)) canvasAspect = 16f / 9;
            if (!PositiveFinite(imageAspect)) imageAspect = 675f / 983;
            double height = (double)width * canvasAspect / imageAspect;
            slotHeight = (float)Math.Min(float.MaxValue, height);
            double cosine = Math.Max(0, Math.Min(1, projectedHeight / (double)slotHeight));
            tiltDegrees = (float)(Math.Acos(cosine) * 180 / Math.PI);
        }

        private static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

        public static BattleSummonSample Sample(float age)
        {
            if (float.IsNaN(age) || float.IsNegativeInfinity(age)) age = 0;
            if (float.IsPositiveInfinity(age)) age = Duration;
            double clock = Math.Max(0, Math.Min(Duration, (double)age));
            float entry = Smooth(clock, 0, 0.44);
            float reveal = Smooth(clock, 0.40, 0.86);
            float hold = Smooth(clock, 0.55, 1.48);
            float landing = Smooth(clock, 1.48, Duration);
            float vanish = Smooth(clock, 1.60, Duration);
            float present = Smooth(clock, 0, 0.14) * (1 - vanish);
            float flash = Pulse(clock, 0.43, 0.72);
            return new BattleSummonSample(
                0.82f * present,
                Smooth(clock, 0.03, 0.17) * (1 - Smooth(clock, 1.82, Duration)),
                0.18f + 0.82f * entry + 0.035f * Pulse(clock, 0.26, 0.70),
                82 * (1 - entry),
                -12 * (1 - entry),
                reveal * (1 - vanish),
                0.88f + 0.12f * reveal + 0.025f * hold,
                reveal,
                flash * 0.34f,
                (float)(clock / Duration),
                (0.35f + 0.65f * flash) * present,
                Smooth(clock, 0.42, 0.70) * (1 - vanish),
                landing);
        }

        private static float Smooth(double value, double start, double end)
        {
            double t = Math.Max(0, Math.Min(1, (value - start) / (end - start)));
            return (float)(t * t * (3 - 2 * t));
        }

        private static float Pulse(double value, double start, double end)
        {
            if (value <= start || value >= end) return 0;
            return (float)Math.Sin((value - start) / (end - start) * Math.PI);
        }
    }
}
