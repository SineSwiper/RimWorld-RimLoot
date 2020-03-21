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
        private static class SetQualityPatch {
            [HarmonyPostfix]
            static void Postfix(CompQuality __instance, QualityCategory q) {
                __instance.parent.BroadcastCompSignal("SetQuality");
            }
        }

        /*
         * Acquires VerbProperties from CompLootAffixableThing, if it can.  This allows other LootAffixModifiers
         * to modify stats within the VerbProperties.
         */

        [HarmonyPatch(typeof(CompEquippable), "VerbProperties", MethodType.Getter)]
        private static class VerbPropertiesPatch {
            [HarmonyPrefix]
            static bool Prefix(CompEquippable __instance, ref List<VerbProperties> __result) {
                var comp = __instance.parent.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return true;  // go to original getter

                Log.Message("Called VerbProperties for " + __instance.parent);
                __result = comp.VerbProperties;
                return false;
            }
        }

        // Ditto for ThingDef.SpecialDisplayStats
        [HarmonyPatch(typeof(ThingDef), "SpecialDisplayStats")]
        private static class SpecialDisplayStatsPatches {
            [HarmonyPrefix]
            static bool Prefix(ThingDef __instance, StatRequest req, ref List<VerbProperties> ___verbs, List<VerbProperties> __state) {
                if (!(req.Thing is ThingWithComps thing)) return true;
                var comp = thing.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return true;  // go to original
            
                // Store the old set, and replace with the new
                __state  = ___verbs;
                ___verbs = comp.VerbProperties;

                return true;
            }

            [HarmonyFinalizer]
            static void Finalizer(ThingDef __instance, StatRequest req, ref List<VerbProperties> ___verbs, List<VerbProperties> __state) {
                // Usual sanity checks
                if (!(req.Thing is ThingWithComps thing)) return;
                var comp = thing.TryGetComp<CompLootAffixableThing>();
                if (comp    == null) return;
                if (__state == null) return;
            
                // Go back to the old set
                ___verbs = __state;

                return;
            }
        }


    }
}
