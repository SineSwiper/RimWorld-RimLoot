using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_VerbPropertiesChange_Number : LootAffixModifier_VerbPropertiesChange {
        public ValueModifierSet valueModifier;
        
        public override TaggedString ModifierChangeString {
            get { return basicStatDesc.GetModifierChangeString(valueModifier); }
        }

        public override IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            foreach (string configError in base.ConfigErrors(parentDef))
                yield return configError;

            // Check for reflection errors
            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
            Type      type  = field.FieldType;
            if (!ConvertHelper.CanConvert(1f, type)) yield return "Unsupported type: " + type;

            // ValueModifierSet sanity checks
            if (valueModifier == null) {
                yield return "The valueModifer is not set!";
                yield break;
            }

            foreach (string configError in valueModifier.ConfigErrors(parentDef, this))
                yield return configError;
        }

        public override void ModifyVerbProperties (ThingWithComps parentThing, VerbProperties verbProperties, LootAffixDef parentDef) {
            /* XXX: Yes, we are dynamically modifying a value here via reflection, based on data some rando
             * provided via XML.  Is it dangerous?  Sure.  But, this is the best way to change whatever value
             * we want.
             */

            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
            Type type = field.FieldType;
            float val = ConvertHelper.Convert<float>( field.GetValue(verbProperties) );
            val = valueModifier.ChangeValue(val);
            field.SetValue(verbProperties, ConvertHelper.Convert(val, type));
        }

        public override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {
            var comp = parentThing.TryGetComp<CompLootAffixableThing>();
            VerbProperties  baseVerb = comp.VerbPropertiesFromDef.First(x => x.isPrimary);  // since parentThing.def.verbs is already swapped
            VerbProperties finalVerb = comp.VerbProperties       .First(x => x.isPrimary);

            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
            Type type = field.FieldType;
            float baseValue  = ConvertHelper.Convert<float>( field.GetValue(baseVerb ) );
            float finalValue = ConvertHelper.Convert<float>( field.GetValue(finalVerb) );

            basicStatDesc.SpecialDisplayStatsInjectors(
                statDrawEntry:  statDrawEntry,
                preLabel:       preLabel,
                parentThing:    parentThing,
                parentModifier: this,
                baseValue:      baseValue,
                finalValue:     finalValue
            );
        }

    }
}
