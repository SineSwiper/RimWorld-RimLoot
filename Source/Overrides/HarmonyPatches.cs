using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    [StaticConstructorOnStartup]
    internal class HarmonyPatches {

        /*
         * Broadcast a "SetQuality" signal to the thing and comps after the quality is set.  This is used by
         * CompLootAffixableThing to know when to initialize its affixes.
         */
        [HarmonyPatch(typeof(CompQuality), "SetQuality")]
        private static class SetQuality_PostFix {
            [HarmonyPostfix]
            static void Postfix(CompQuality __instance, QualityCategory q) {
                __instance.parent.BroadcastCompSignal("SetQuality");
            }
        }

    }
}
