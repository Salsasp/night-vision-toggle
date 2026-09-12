using System;
using HarmonyLib;
using Vintagestory.API.Common;

namespace NightVisionToggle
{
    /// <summary>
    /// Optional interop with Combat Overhaul, which also ships inside overhaulliblegacycompat.
    /// Its FueledItemSystem is a renderer in its own right: every frame it pushes a cached
    /// strength straight into ShaderUniforms.NightVisionStrength, so suppressing only the vanilla
    /// night vision renderer leaves the effect visible. Because that mod is not a dependency,
    /// everything here is looked up by name and quietly skipped when it is not installed.
    /// </summary>
    public static class CombatOverhaulCompat
    {
        const string TypeName = "CombatOverhaul.FueledItemSystem";
        const string MethodName = "SetNightVisionStrength";

        public static void TryPatch(Harmony harmony, ILogger logger)
        {
            var type = AccessTools.TypeByName(TypeName);
            if (type == null) return;

            var target = AccessTools.Method(type, MethodName, new[] { typeof(float) });
            if (target == null)
            {
                logger?.Warning("[nightvisiontoggle] Found {0} but no {1}(float). Night vision may stay visible while toggled off.", TypeName, MethodName);
                return;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(CombatOverhaulCompat), nameof(SetStrengthPrefix)));
                logger?.Notification("[nightvisiontoggle] Patched {0}.{1} for Combat Overhaul compatibility.", TypeName, MethodName);
            }
            catch (Exception e)
            {
                logger?.Warning("[nightvisiontoggle] Could not patch {0}.{1}: {2}", TypeName, MethodName, e.Message);
            }
        }

        /// <summary>
        /// Scales the strength Combat Overhaul is about to apply by the same fade factor the
        /// vanilla path uses. It re-applies its cached value every frame, so the ramp animates
        /// naturally and a factor of 1 leaves its behaviour untouched.
        /// </summary>
        public static void SetStrengthPrefix(ref float strength)
        {
            var capi = NightVisionRenderPatch.Capi;
            if (capi == null) return;

            strength *= NightVisionFade.GetFactor(capi);
        }
    }
}
