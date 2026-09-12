using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace NightVisionToggle
{
    public static class NightVisionFade
    {
        public const float EffectPeakValue = 1.5f;
        public const float FadeInSeconds = 1.5f;
        public const float SettleSeconds = 0.5f;
        public const float FadeOutSeconds = 0.4f;

        static bool tracking;
        static bool active;
        static float startFactor;
        static long startMs;

        public static float GetFactor(ICoreClientAPI capi)
        {
            long now = capi.World.ElapsedMilliseconds;
            bool wanted = IsActive(capi);

            if (!tracking)
            {
                tracking = true;
                active = wanted;
                startFactor = wanted ? 1f : 0f;
                startMs = now;
                return startFactor;
            }

            if (wanted != active)
            {
                startFactor = FactorAt(now);
                active = wanted;
                startMs = now;
            }

            return FactorAt(now);
        }

        public static void Reset() => tracking = false;

        static float FactorAt(long now)
        {
            long elapsed = now - startMs;

            if (!active)
            {
                float fadeOutMs = FadeOutSeconds * 1000f;
                if (fadeOutMs <= 0f) return 0f;

                return Ease(startFactor, 0f, elapsed / fadeOutMs);
            }

            float riseMs = FadeInSeconds * 1000f;
            if (riseMs > 0f && elapsed < riseMs) return Ease(startFactor, EffectPeakValue, elapsed / riseMs);

            float settleMs = SettleSeconds * 1000f;
            if (settleMs <= 0f) return 1f;

            return Ease(EffectPeakValue, 1f, (elapsed - riseMs) / settleMs);
        }

        static float Ease(float from, float to, float t)
        {
            t = GameMath.Clamp(t, 0f, 1f);
            t = t * t * (3f - 2f * t);  // smoothstep
            return from + (to - from) * t;
        }

        static bool IsActive(ICoreClientAPI capi)
        {
            var stack = NightVisionToggleModSystem.GetWornNightVisionSlot(capi.World?.Player)?.Itemstack;
            if (stack?.Collectible is not ItemNightvisiondevice device) return false;
            if (!NightVisionToggleModSystem.IsEnabled(stack)) return false;

            return device.GetFuelHours(stack) > 0;
        }
    }
}
