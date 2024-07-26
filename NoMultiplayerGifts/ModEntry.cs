using System.Reflection.Emit;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using GenericModConfigMenu;
using StardewModdingAPI.Events;

namespace NoMultiplayerGifts
{
    internal sealed class ModEntry : Mod
    {
        private static ModConfig Config;

        public override void Entry(IModHelper helper)
        {
            // Get config
            Config = this.Helper.ReadConfig<ModConfig>();

            // Add event handlers
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

            // Instantiate Harmony
            Harmony harmony = new(ModManifest.UniqueID);

            // Apply offerItem transpiler to Farmer.checkAction method
            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.checkAction)),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(offerItem_Transpiler))
            );
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // Register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(Config)
            );

            // Add config option
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Player Gifting",
                tooltip: () => "Enable or disable player to player gifting",
                getValue: () => Config.PlayerGiftingEnabled,
                setValue: value => Config.PlayerGiftingEnabled = value
            );
        }

        public static IEnumerable<CodeInstruction> offerItem_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Instantiate matcher and necessary MethodInfo objects
            CodeMatcher matcher = new(instructions);
            MethodInfo canBeGivenAsGift = AccessTools.PropertyGetter(typeof(Item), nameof(Item.canBeGivenAsGift));
            MethodInfo getHasValue = AccessTools.PropertyGetter(typeof(Nullable<bool>), nameof(Nullable<bool>.HasValue));
            MethodInfo getValueOrDefault = AccessTools.PropertyGetter(typeof(Nullable<bool>), nameof(Nullable<bool>.GetValueOrDefault));

            // Package ModifyCanBeGivenAsGiftValueOnStack in a MethodInfo object
            MethodInfo modifyInfo = AccessTools.Method(typeof(ModEntry), nameof(ModifyCanBeGivenAsGiftValueOnStack));

            // Match IL code corresponding to offer item prompt if statement
            matcher.MatchEndForward(
                new CodeMatch(OpCodes.Callvirt, canBeGivenAsGift),
                new CodeMatch(OpCodes.Newobj),
                new CodeMatch(OpCodes.Stloc_S),
                new CodeMatch(OpCodes.Ldloca_S),
                new CodeMatch(OpCodes.Call, getHasValue),
                new CodeMatch(OpCodes.Brfalse),
                new CodeMatch(OpCodes.Ldloca_S),
                new CodeMatch(OpCodes.Call, getValueOrDefault)
                )
                // Throw error if unable to match IL code pattern
                .ThrowIfNotMatch($"Could not find entry point for {nameof(offerItem_Transpiler)}")
                .Advance(1)
                // Insert ModifyCanBeGivenAsGiftValueOnStack before evaluation of canBeGivenAsGift
                .Insert(
                    new CodeInstruction(OpCodes.Call, modifyInfo)
                );

            return matcher.InstructionEnumeration();
        }

        // Take canBeGivenAsGift boolean value from the stack, return false if player gifting is disabled
        public static bool ModifyCanBeGivenAsGiftValueOnStack(bool canBeGivenAsGift)
        {
            return canBeGivenAsGift && Config.PlayerGiftingEnabled;
        }
    }
}