using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    public class LootAffixModifier_ShootThroughWalls : LootAffixModifier_VerbPropertiesChange_Boolean {
        public override TaggedString ModifierChangeString {
            get { return "RimLoot_ShootsThroughWalls".Translate(); }
        }

        public override TaggedString ModifierChangeLabel {
            get { return ModifierChangeString; }
        }

        // FIXME: Need some other LoS fixes for jobs.  @Garthor had some IL transplier code for it.
        public override void ResolveReferences (LootAffixDef parentDef) {
            // Among other overrides from HarmonyPatches
            affectedField = "requireLineOfSight";
            newValue      = false;
            base.ResolveReferences(parentDef);
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Also include checks for tech level
            return thing.def.IsRangedWeapon && thing.def.techLevel >= TechLevel.Spacer;
        }

        public override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {
            // Nothing to inject
        }

    }
}
