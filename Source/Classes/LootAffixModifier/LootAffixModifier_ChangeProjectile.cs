using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_ChangeProjectile : LootAffixModifier_VerbPropertiesChange_Def {

        // FIXME: Are these even used?
        public override TaggedString ModifierChangeStat {
            get {
                return resolvedDef.LabelCap;
            }
        }
        
        public override TaggedString ModifierChangeString {
            get {
                return base.ModifierChangeString;  // {0} chance
            }
        }

        public override TaggedString ModifierChangeLabel {
            get {
                string pluralProjectiles = Find.ActiveLanguageWorker.Pluralize(resolvedDef.label, -1);
                return chance >= 1 ?
                    "RimLoot_ChangeProjectileModifierLabel_Permanent".Translate(pluralProjectiles) :
                    "RimLoot_ChangeProjectileModifierLabel_Chance"   .Translate(GenText.ToStringPercent(chance), resolvedDef.label)
                ;
            }
        }

        public override void ResolveReferences (LootAffixDef parentDef) {
            affectedField = "defaultProjectile";
            base.ResolveReferences(parentDef);
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Also include checks for tech level and its own bullet
            return
                thing.def.IsRangedWeapon && thing.def.techLevel >= TechLevel.Industrial &&
                thing.def.Verbs.First(x => x.isPrimary) is VerbProperties vp && vp.defaultProjectile != (ThingDef)resolvedDef
            ;
        }

        // The def's chance is the overall chance against the whole shot period.  The combined probability
        // needs to be separated out for each shot in the burst.
        public float RealChance (ThingWithComps thing) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            VerbProperties modVerbProps = comp.VerbProperties.First(x => x.isPrimary);

            // 1 - (1 - chance) to the count-th root 
            return 1f - Mathf.Pow(1f - chance, 1f / modVerbProps.burstShotCount);
        }

        public override bool ShouldActivate (ThingWithComps thing) {
            Log.Message("Chances: " + string.Join(", ", chance, RealChance(thing)));  // DEBUG

            if (RealChance(thing) < UnityEngine.Random.Range(0.0f, 1.0f)) return false;
            return true;
        }

        public override void PreShotFired (ThingWithComps parentThing, LootAffixDef parentDef) {
            // 100% chance: This should have already been set by ModifyVerbProperties
            if (chance >= 1) return;

            // Less than 100% chance: Switch projectile based on chance hit
            if (ShouldActivate(parentThing)) ModifyVerbProperty(parentThing);
            else                             ResetVerbProperty (parentThing);
        }

        public override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {
            // FIXME
            basicStatDesc.SpecialDisplayStatsInjectors(
                statDrawEntry:  statDrawEntry,
                preLabel:       preLabel,
                parentThing:    parentThing,
                parentModifier: this,
                curDef:         resolvedDef
            );
        }

    }
}
