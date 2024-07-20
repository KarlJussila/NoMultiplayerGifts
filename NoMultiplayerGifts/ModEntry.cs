using System;
using System.Reflection.Emit;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace NoMultiplayerGifts
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Harmony harmony = new(ModManifest.UniqueID);

            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.checkAction)),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(offerItem_Transpiler))
            );
        }

        public static IEnumerable<CodeInstruction> offerItem_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);
            MethodInfo canBeGivenAsGift = AccessTools.PropertyGetter(typeof(Item), nameof(Item.canBeGivenAsGift));
            MethodInfo getHasValue = AccessTools.PropertyGetter(typeof(Nullable<bool>), nameof(Nullable<bool>.HasValue));
            MethodInfo getValueOrDefault = AccessTools.PropertyGetter(typeof(Nullable<bool>), nameof(Nullable<bool>.GetValueOrDefault));

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
                .ThrowIfNotMatch($"Could not find entry point for {nameof(offerItem_Transpiler)}")
                .Insert(
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.And)
                );

            return matcher.InstructionEnumeration();
        }
    }
}