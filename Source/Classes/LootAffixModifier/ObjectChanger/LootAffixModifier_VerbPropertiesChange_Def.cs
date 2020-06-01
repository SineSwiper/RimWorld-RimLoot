using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_VerbPropertiesChange_Def : LootAffixModifier_ObjectChanger {
        public string newDef;
        public Def    resolvedDef;

        public override ModifierTarget AppliesTo {
            get { return ModifierTarget.VerbProperties; }
        }

        public override TaggedString ModifierChangeString {
            get { return basicStatDesc.GetModifierChangeString(resolvedDef); }
        }

        public override void ResolveReferences (LootAffixDef parentDef) {
            // Set the resolvedDef object, with paranoia checks
            if (affectedField == null || newDef == null) return;
            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
            if (field == null) return;

            Type type = field.FieldType;
            if (!typeof(Def).IsAssignableFrom(type)) return;

            Type defDBType = typeof(DefDatabase<>).MakeGenericType(type);
            MethodInfo getNamedMethod = defDBType.GetMethod("GetNamed");
            resolvedDef = (Def)getNamedMethod.Invoke(null, new object[] { newDef, true });

            // Call this last, to get the resolvedDef before LootAffixDef needs it for ModifierChangeString
            base.ResolveReferences(parentDef);
        }

        public override IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            foreach (string configError in base.ConfigErrors(parentDef))
                yield return configError;

            if (newDef == null) {
                yield return "The newDef is not set!";
                yield break;
            }

            // Check for reflection errors
            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
            Type       type = field.FieldType;
            if (!typeof(Def).IsAssignableFrom(type)) yield return "Unsupported type: " + type;
        }

        public override void ModifyVerbProperty (ThingWithComps parentThing, VerbProperties verbProperties) {
            SetVerbProperty(verbProperties, resolvedDef);
        }

        public override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {
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
