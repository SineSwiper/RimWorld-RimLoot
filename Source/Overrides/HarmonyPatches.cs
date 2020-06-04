using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

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
         * Broadcast a pre/post "FireShot" signals to the thing and comps before/after it fires.  This is used by
         * CompLootAffixableThing for projectile changing.
         */
        [HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
        private static class TryCastShotPatch {
            [HarmonyPrefix]
            static void Prefix(Verb_LaunchProjectile __instance) {
                __instance.EquipmentSource?.BroadcastCompSignal("AboutToFireShot");
            }

            [HarmonyPostfix]
            static void Postfix(Verb_LaunchProjectile __instance) {
                __instance.EquipmentSource?.BroadcastCompSignal("FiredShot");
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

        // Ditto for Tools
        [HarmonyPatch(typeof(CompEquippable), "Tools", MethodType.Getter)]
        private static class ToolsPatch {
            [HarmonyPrefix]
            static bool Prefix(CompEquippable __instance, ref List<Tool> __result) {
                var comp = __instance.parent.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return true;  // go to original getter

                __result = comp.Tools;
                return false;
            }
        }

        // Ditto for VerbProperties within ThingDef.SpecialDisplayStats
        [HarmonyPatch(typeof(ThingDef), "SpecialDisplayStats")]
        private static class SpecialDisplayStatsPatches {
            [HarmonyPrefix]
            static bool Prefix(ThingDef __instance, StatRequest req) {
                if (!(req.Thing is ThingWithComps thing)) return true;
                var comp = thing.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return true;  // go to original

                // [Reflection prep] thing.verbs
                FieldInfo verbsField = AccessTools.Field(typeof(ThingDef), "verbs");
            
                // Store the old set, and replace with the new
                Base.origVerbPropertiesCache.AddIfNotExist(__instance.defName, (List<VerbProperties>)verbsField.GetValue(__instance));
                verbsField.SetValue(__instance, comp.VerbProperties);

                return true;
            }

            [HarmonyPostfix]
            static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> values, ThingDef __instance, StatRequest req) {
                CompLootAffixableThing comp = null;
                if (req.Thing is ThingWithComps thing) comp = thing.TryGetComp<CompLootAffixableThing>();

                // Cycle through the entries
                foreach (StatDrawEntry value in values) {
                    // Give it to the comp to meddle with
                    if (comp != null) comp.SpecialDisplayStatsInjectors(value);

                    yield return value;
                }
                
                // Go back to the old set.  Iterators cannot have refs, so we have to replace this with reflection.
                if (!Base.origVerbPropertiesCache.ContainsKey(__instance.defName)) {
                    if (comp != null && !req.Thing.def.Verbs.NullOrEmpty()) Log.Error("Old VerbProperties lost from SpecialDisplayStats swap!");
                    yield break;
                }

                // [Reflection] thing.verbs = Base.origVerbPropertiesCache[__instance.defName];
                FieldInfo verbsField = AccessTools.Field(typeof(ThingDef), "verbs");
                verbsField.SetValue(__instance, Base.origVerbPropertiesCache[__instance.defName]);
            }
        }

        /*
         * Use the UIIcon from CompLootAffixableThing, which has the overlay.  This is important to make sure
         * players can easily identify dangerous weapons from raiders.
         */

        [HarmonyPatch(typeof(VerbTracker), "CreateVerbTargetCommand")]
        private static class CreateVerbTargetCommandPatch {
            [HarmonyPostfix]
            static void Postfix(Thing ownerThing, Command_VerbTarget __result) {
                var comp = ownerThing?.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return;

                if (__result != null) __result.icon = comp.UIIcon;
            }
        }

        // Y U NO NOTIFY APPAREL?
        [HarmonyPatch(typeof(Pawn_ApparelTracker), "Notify_ApparelAdded")]
        private static class Notify_ApparelAddedPatch {
            [HarmonyPostfix]
            static void Postfix(Pawn_ApparelTracker __instance, Apparel apparel) {
                var comp = apparel?.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return;

                comp.Notify_ApparelAdded(__instance.pawn);
            }
        }

        /*
         * Widget DefIcon fixes for LootAffixDef.  This allows for hyperlink icons.
         */
        [HarmonyPatch(typeof(Widgets), "CanDrawIconFor")]
        private static class CanDrawIconForPatch {
            [HarmonyPostfix]
            static void Postfix(Def def, ref bool __result) {
                if (def is LootAffixDef) __result = true;
            }
        }

        [HarmonyPatch(typeof(Widgets), "DefIcon")]
        private static class DefIconPatch {
            [HarmonyPrefix]
            static bool Prefix(Rect rect, Def def, float scale) {
                if (!(def is LootAffixDef lootAffix)) return true;  // go to original

                Widgets.DrawTextureFitted(rect, lootAffix.DefIcon, scale);
                return false;
            }
        }

        /*
         * Fix HitFlags to make sure ShootThroughWalls works properly.
         */
        [HarmonyPatch(typeof(Projectile), "HitFlags", MethodType.Getter)]
        private static class HitFlagsPatch {
            [HarmonyPostfix]
            static void Postfix(Projectile __instance, Thing ___launcher, ref ProjectileHitFlags ___desiredHitFlags, ref ProjectileHitFlags __result) {
                // Short-circuit
                if (!__result.HasFlag(ProjectileHitFlags.NonTargetWorld)) return;
                
                // Find the Pawn's weapon
                if (!(___launcher is Pawn pawn)) return;
                var comp = pawn.equipment?.Primary?.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return;

                // Does the weapon have the ShootThroughWalls modifier?
                if (!comp.AllModifiers.Any(lam => lam is LootAffixModifier_ShootThroughWalls)) return;

                // Remove the NonTargetWorld flag from both current and future results.  (NonTargetPawns are still
                // fair game.)
                __result           &= ~ProjectileHitFlags.NonTargetWorld;
                ___desiredHitFlags &= ~ProjectileHitFlags.NonTargetWorld;
            }
        }
        
        /*
         * Fix BestAttackTarget to remove LOS-related TargetScanFlags for ShootThroughWalls
         */
        [HarmonyPatch(typeof(AttackTargetFinder), "BestAttackTarget")]
        private static class BestAttackTargetPatch {
            [HarmonyPrefix]
            static void Prefix(IAttackTargetSearcher searcher, ref TargetScanFlags flags) {
                var comp = searcher.CurrentEffectiveVerb?.EquipmentSource?.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return;

                // Does the weapon have the ShootThroughWalls modifier?
                if (!comp.AllModifiers.Any(lam => lam is LootAffixModifier_ShootThroughWalls)) return;

                // We don't need no line-of-sight
                flags &= ~(TargetScanFlags.NeedLOSToAll | TargetScanFlags.LOSBlockableByGas);
            }
        }

    }
}
