using System.Threading;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace NightVisionToggle
{
    /// <summary>
    /// Adds a rebindable hotkey that turns the night vision mask effect on and off.
    /// The on/off state lives on the mask itemstack, so the server owns it and it syncs
    /// to clients through the normal inventory sync. While off, the mask burns no fuel.
    /// </summary>
    public class NightVisionToggleModSystem : ModSystem
    {
        public const string HarmonyId = "salsa.nightvisiontoggle";
        public const string HotkeyCode = "nightvisiontoggle";
        public const string ChannelName = "nightvisiontoggle";

        /// <summary>Stack attribute holding the toggle state. Absent means on, so existing masks keep working.</summary>
        public const string EnabledAttribute = "nightVisionToggleEnabled";

        public const string EnableSoundPath = "nightvisiontoggle:sounds/nightvision-toggle-on";
        public const string DisableSoundPath = "nightvisiontoggle:sounds/nightvision-toggle-off";

        const float SoundRange = 8f;
        const float SoundVolume = 1f;

        // In singleplayer the client and server both instantiate this mod system in the same
        // process, so the patches are applied once and removed once.
        static Harmony harmony;
        static int loadedInstances;

        ICoreAPI api;
        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            api.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<NightVisionTogglePacket>();

            if (Interlocked.Increment(ref loadedInstances) == 1)
            {
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll();
                CombatOverhaulCompat.TryPatch(harmony, Mod?.Logger);
            }
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            this.capi = capi;
            NightVisionRenderPatch.Capi = capi;

            clientChannel = capi.Network.GetChannel(ChannelName);

            capi.Input.RegisterHotKey(
                HotkeyCode,
                Lang.Get("nightvisiontoggle:hotkey-nightvisiontoggle"),
                GlKeys.N,
                HotkeyType.CharacterControls
            );
            capi.Input.SetHotKeyHandler(HotkeyCode, OnToggleHotkey);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            sapi.Network.GetChannel(ChannelName)
                .SetMessageHandler<NightVisionTogglePacket>(OnTogglePacketReceived);
        }

        public override void Dispose()
        {
            if (api?.Side == EnumAppSide.Client)
            {
                NightVisionRenderPatch.Capi = null;
                NightVisionFade.Reset();
            }

            if (Interlocked.Decrement(ref loadedInstances) == 0)
            {
                harmony?.UnpatchAll(HarmonyId);
                harmony = null;
            }

            base.Dispose();
        }

        bool OnToggleHotkey(KeyCombination comb)
        {
            var slot = GetWornNightVisionSlot(capi.World?.Player);
            var stack = slot?.Itemstack;

            if (stack?.Collectible is not ItemNightvisiondevice)
            {
                capi.TriggerIngameError(this, "nonightvisiondevice", Lang.Get("nightvisiontoggle:ingameerror-nodevice"));
                return true;
            }

            // Without the mod on the server the toggle cannot hold: the server keeps draining
            // fuel and its next inventory sync overwrites the local change a few seconds later.
            // Fail loudly rather than let the effect silently flicker back on.
            if (clientChannel?.Connected != true)
            {
                capi.TriggerIngameError(this, "servermissingmod", Lang.Get("nightvisiontoggle:ingameerror-servermissing"));
                return true;
            }

            bool enabled = !IsEnabled(stack);

            // Apply locally first so the shader reacts on the next frame instead of after a
            // network round trip. The server confirms by syncing the slot back down.
            SetEnabled(stack, enabled);
            clientChannel?.SendPacket(new NightVisionTogglePacket() { Enabled = enabled });
            PlayToggleSound(capi.World.Player, enabled);

            return true;
        }

        void OnTogglePacketReceived(IServerPlayer fromPlayer, NightVisionTogglePacket packet)
        {
            var slot = GetWornNightVisionSlot(fromPlayer);
            var stack = slot?.Itemstack;

            if (stack?.Collectible is not ItemNightvisiondevice) return;
            if (IsEnabled(stack) == packet.Enabled) return;

            SetEnabled(stack, packet.Enabled);
            slot.MarkDirty();
            PlayToggleSound(fromPlayer, packet.Enabled);
        }

        /// <summary>
        /// Plays the toggle sound. Called on both sides: the client plays it immediately so the
        /// wearer hears it without waiting for a round trip, and the server sends it to everyone
        /// else in earshot. Passing the toggling player as dualCallByPlayer server side is what
        /// keeps them from hearing it twice.
        /// </summary>
        void PlayToggleSound(IPlayer player, bool enabled)
        {
            if (player == null) return;

            string path = enabled ? EnableSoundPath : DisableSoundPath;
            if (!api.Assets.Exists(new AssetLocation(path + ".ogg"))) return;

            api.World.PlaySoundAt(
                new AssetLocation(path),
                player,
                api.Side == EnumAppSide.Server ? player : null,
                randomizePitch: false,
                range: SoundRange,
                volume: SoundVolume
            );
        }

        public static bool IsEnabled(ItemStack stack)
        {
            return stack?.Attributes?.GetBool(EnabledAttribute, true) ?? false;
        }

        public static void SetEnabled(ItemStack stack, bool enabled)
        {
            stack?.Attributes?.SetBool(EnabledAttribute, enabled);
        }

        /// <summary>
        /// Finds the worn night vision mask. Checks the vanilla head armour slot first, then falls
        /// back to scanning the character inventory, because mods that rework armour (Combat
        /// Overhaul among them) move the mask into a slot at a different index. Anything sitting
        /// in the character inventory is by definition equipped, so the scan cannot pick up a
        /// spare mask out of a backpack.
        /// </summary>
        public static ItemSlot GetWornNightVisionSlot(IPlayer player)
        {
            var inv = player?.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null) return null;

            int index = (int)EnumCharacterDressType.ArmorHead;
            if (index < inv.Count && inv[index]?.Itemstack?.Collectible is ItemNightvisiondevice) return inv[index];

            foreach (var slot in inv)
            {
                if (slot?.Itemstack?.Collectible is ItemNightvisiondevice) return slot;
            }

            return null;
        }
    }
}
