using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
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
            static void Postfix(CompQuality __instance) {
                __instance.parent.BroadcastCompSignal("SetQuality");
            }
        }

        /*
         * Broadcast a "AboutToFireShot" signal to the thing and comps before it fires.  This is used by
         * CompLootAffixableThing for projectile changing.
         */
        [HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
        private static class TryCastShotPatch {
            [HarmonyPrefix]
            static void Prefix(Verb_LaunchProjectile __instance) {
                __instance.EquipmentSource?.BroadcastCompSignal("AboutToFireShot");
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

                __result = comp.VerbProperties;
                return false;
            }
        }

        // Ditto for ThingDef.SpecialDisplayStats
        [HarmonyPatch(typeof(ThingDef), "SpecialDisplayStats")]
        private static class SpecialDisplayStatsPatches {
            [HarmonyPrefix]
            static bool Prefix(ThingDef __instance, StatRequest req, ref List<VerbProperties> ___verbs, out List<VerbProperties> __state) {
                __state = null;
                if (!(req.Thing is ThingWithComps thing)) return true;
                var comp = thing.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return true;  // go to original
            
                // Store the old set, and replace with the new
                __state  = ___verbs;
                ___verbs = comp.VerbProperties;

                return true;
            }

            [HarmonyPostfix]
            static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> values, ThingDef __instance, StatRequest req, List<VerbProperties> __state) {
                CompLootAffixableThing comp = null;
                if (req.Thing is ThingWithComps thing) comp = thing.TryGetComp<CompLootAffixableThing>();

                // Cycle through the entries
                foreach (StatDrawEntry value in values) {
                    // Give it to the comp to meddle with
                    if (comp != null) comp.SpecialDisplayStatsInjectors(value);

                    yield return value;
                }
                
                // Go back to the old set.  Iterators cannot have refs, so we have to replace this with reflection.
                if (__state == null) {
                    if (comp != null) Log.Error("Old VerbProperties lost from SpecialDisplayStats swap!");
                    yield break;
                }

                // [Reflection] thing.verbs = __state;
                FieldInfo field = AccessTools.Field(typeof(ThingDef), "verbs");
                field.SetValue(__instance, __state);
            }
        }

    }
}
