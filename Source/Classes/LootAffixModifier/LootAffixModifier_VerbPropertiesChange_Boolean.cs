using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_VerbPropertiesChange_Boolean : LootAffixModifier_ObjectChanger {
        public bool?  newValue;
        
        public override ModifierTarget AppliesTo {
            get { return ModifierTarget.VerbProperties; }
        }

        public override TaggedString ModifierChangeString {
            get { return basicStatDesc.GetModifierChangeString((bool)newValue); }
        }

        public override IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            foreach (string configError in base.ConfigErrors(parentDef))
                yield return configError;

            if (newValue == null) {
                yield return "The newValue is not set!";
                yield break;
            }

            // Check for reflection errors
            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
            Type type = field.FieldType;
            if (type != typeof(bool)) yield return "Unsupported type: " + type;
        }

        public override void ModifyVerbProperty (ThingWithComps parentThing, VerbProperties verbProperties) {
            SetVerbProperty(verbProperties, (bool)newValue);
        }

        public override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {
            basicStatDesc.SpecialDisplayStatsInjectors(
                statDrawEntry:  statDrawEntry,
                preLabel:       preLabel,
                parentThing:    parentThing,
                parentModifier: this,
                curValue:       (bool)newValue
            );
        }
    }
}
