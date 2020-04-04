using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public abstract class LootAffixModifier_VerbPropertiesChange : LootAffixModifier {
        public string affectedField;

        protected FieldInfo fieldInfo;
        protected Type      fieldType;

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
            fieldInfo     = AccessTools.     Field(typeof(VerbProperties), affectedField);
            fieldType     = fieldInfo.FieldType;

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

        /* XXX: Yes, we are dynamically modifying a value here via reflection, based on data some rando
         * provided via XML.  Is it dangerous?  Sure.  But, this is the best way to change whatever value
         * we want.
         */
        public override void ModifyVerbProperty (ThingWithComps parentThing) {
            var comp = parentThing.TryGetComp<CompLootAffixableThing>();
            VerbProperties modVerbProps = comp.VerbProperties.First(x => x.isPrimary);
            ModifyVerbProperty(parentThing, modVerbProps);
        }

        public override void ResetVerbProperty (ThingWithComps parentThing) {
            var comp = parentThing.TryGetComp<CompLootAffixableThing>();
            VerbProperties srcVerbProps  = comp.VerbPropertiesFromDef.First(x => x.isPrimary);
            VerbProperties destVerbProps = comp.VerbProperties       .First(x => x.isPrimary);
            ResetVerbProperty(parentThing, srcVerbProps, destVerbProps);
        }

        public abstract override void ModifyVerbProperty (ThingWithComps parentThing, VerbProperties verbProperties);

        public override void ResetVerbProperty (ThingWithComps parentThing, VerbProperties srcVerbProps, VerbProperties destVerbProps) {
            SetVerbProperty(destVerbProps, fieldInfo.GetValue(srcVerbProps));
        }

        public void SetVerbProperty (VerbProperties verbProperties, object value) {
            Log.Message("SetVerbProperty: " + string.Join(" / ", verbProperties, fieldInfo, fieldType, value));
            fieldInfo.SetValue(verbProperties, ConvertHelper.Convert(value, fieldType));
        }

        public abstract override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel);
    }
}
