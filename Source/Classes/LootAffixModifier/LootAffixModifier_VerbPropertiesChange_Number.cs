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

        public override void ModifyVerbProperty (ThingWithComps parentThing, VerbProperties verbProperties) {
            float val = ConvertHelper.Convert<float>( fieldInfo.GetValue(verbProperties) );
            val = valueModifier.ChangeValue(val);
            SetVerbProperty(verbProperties, ConvertHelper.Convert(val, fieldType));
        }

        public override void ResetVerbProperty (ThingWithComps parentThing, VerbProperties srcVerbProps, VerbProperties destVerbProps) {
            float val = ConvertHelper.Convert<float>( fieldInfo.GetValue(srcVerbProps) );
            SetVerbProperty(destVerbProps, val);
        }

        public override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {
            var comp = parentThing.TryGetComp<CompLootAffixableThing>();
            VerbProperties  baseVerb = comp.PrimaryVerbPropsFromDef;  // since parentThing.def.verbs is already swapped
            VerbProperties finalVerb = comp.PrimaryVerbProps;

            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
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
