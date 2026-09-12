using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace NightVisionToggle
{
    /// <summary>
    /// Blocks fuel consumption while the mask is toggled off. Only drains are skipped, so
    /// refuelling with a temporal gear still works. This runs wherever the drain happens,
    /// which for the vanilla mod system is server side only.
    /// </summary>
    [HarmonyPatch(typeof(ItemNightvisiondevice), nameof(ItemNightvisiondevice.AddFuelHours))]
    public static class NightVisionFuelDrainPatch
    {
        static bool Prefix(ItemStack stack, double fuelHours)
        {
            if (fuelHours >= 0) return true;
            return NightVisionToggleModSystem.IsEnabled(stack);
        }
    }

    /// <summary>
    /// Scales the strength vanilla just applied by the fade factor. Letting vanilla compute its
    /// own fuel based value first means the toggle and the ramp ride on top of it instead of
    /// reimplementing it: a factor of 0 is fully off, 1 is untouched vanilla behaviour.
    /// </summary>
    [HarmonyPatch(typeof(ModSystemNightVision), nameof(ModSystemNightVision.OnRenderFrame))]
    public static class NightVisionRenderPatch
    {
        public static ICoreClientAPI Capi;

        static void Postfix()
        {
            var capi = Capi;
            if (capi == null) return;

            // Always ask, even at full strength, so the ramp keeps tracking state changes.
            // Only skip at exactly 1, since the ramp overshoots above 1 before settling.
            float factor = NightVisionFade.GetFactor(capi);
            if (factor == 1f) return;

            capi.Render.ShaderUniforms.NightVisionStrength *= factor;
        }
    }

    /// <summary>Shows the toggle state in the mask tooltip.</summary>
    [HarmonyPatch(typeof(ItemNightvisiondevice), nameof(ItemNightvisiondevice.GetHeldItemInfo))]
    public static class NightVisionHeldInfoPatch
    {
        static void Postfix(ItemSlot inSlot, StringBuilder dsc)
        {
            var stack = inSlot?.Itemstack;
            if (stack == null) return;

            dsc.AppendLine(Lang.Get(NightVisionToggleModSystem.IsEnabled(stack)
                ? "nightvisiontoggle:heldinfo-enabled"
                : "nightvisiontoggle:heldinfo-disabled"));
        }
    }
}
