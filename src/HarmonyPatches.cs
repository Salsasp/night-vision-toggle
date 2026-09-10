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
    /// Forces the night vision shader strength to zero while the worn mask is toggled off.
    /// Vanilla is left to run untouched in every other case.
    /// </summary>
    [HarmonyPatch(typeof(ModSystemNightVision), nameof(ModSystemNightVision.OnRenderFrame))]
    public static class NightVisionRenderPatch
    {
        public static ICoreClientAPI Capi;

        static bool Prefix()
        {
            var capi = Capi;
            if (capi == null) return true;

            var stack = NightVisionToggleModSystem.GetWornNightVisionSlot(capi.World?.Player)?.Itemstack;
            if (stack?.Collectible is not ItemNightvisiondevice) return true;
            if (NightVisionToggleModSystem.IsEnabled(stack)) return true;

            capi.Render.ShaderUniforms.NightVisionStrength = 0;
            return false;
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
