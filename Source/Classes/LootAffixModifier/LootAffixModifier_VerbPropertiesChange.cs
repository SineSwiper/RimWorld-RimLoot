using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public abstract class LootAffixModifier_VerbPropertiesChange : LootAffixModifier {
        public string affectedField;

        private protected BasicStatDescDef basicStatDesc;

        public override ModifierTarget AppliesTo {
            get { return ModifierTarget.VerbProperties; }
        }

        public override TaggedString ModifierChangeStat {
            get { return basicStatDesc.GetModifierChangeStat(); }
        }
        
        public abstract override TaggedString ModifierChangeString {
            get;
        }

        public override void ResolveReferences (LootAffixDef parentDef)  {
            basicStatDesc = BasicStatDescDef.Named(typeof(VerbProperties), affectedField);

            // Call this last, to get the resolvedDef before LootAffixDef needs it for ModifierChangeString
            base.ResolveReferences();
        }

        public override IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            foreach (string configError in base.ConfigErrors(parentDef))
                yield return configError;

            if (affectedField == null) {
                yield return "The affectedField is not set!";
                yield break;
            }

            // Check for reflection errors
            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
            if (field == null) {
                yield return "The affectedField doesn't exist in VerbProperties: " + affectedField;
                yield break;
            }
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Only range weapons have verb properties that make sense here
            return thing.def.IsRangedWeapon;
        }

        public abstract override void ModifyVerbProperties (ThingWithComps parentThing, VerbProperties verbProperties, LootAffixDef parentDef);

        public abstract override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel);
    }
}
