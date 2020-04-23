using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public abstract class LootAffixModifier_ObjectChanger : LootAffixModifier {
        public string affectedField;

        protected FieldInfo fieldInfo;
        protected Type      fieldType;

        private protected BasicStatDescDef basicStatDesc;

        protected Type ObjType {
            get { 
                if (AppliesTo == ModifierTarget.VerbProperties) return typeof(VerbProperties);
                if (AppliesTo == ModifierTarget.Tools)          return typeof(Tool);
                return typeof(object);
            }
        }

        public override TaggedString ModifierChangeStat {
            get { return basicStatDesc.GetModifierChangeStat(); }
        }

        public override void ResolveReferences (LootAffixDef parentDef) {
            basicStatDesc = BasicStatDescDef.Named(ObjType, affectedField);
            fieldInfo     = AccessTools.     Field(ObjType, affectedField);
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
            FieldInfo field = AccessTools.Field(ObjType, affectedField);
            if (field == null) {
                yield return "The affectedField doesn't exist in " + ObjType.Name + ": " + affectedField;
                yield break;
            }
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Only range weapons have verb properties that make sense here
            if (AppliesTo == ModifierTarget.VerbProperties) return thing.def.IsRangedWeapon;

            // Range weapons can have melee properties, but it would be kinda of a waste
            if (AppliesTo == ModifierTarget.Tools)          return thing.def.IsMeleeWeapon;

            // Shouldn't really make it here...
            return thing.def.IsWeapon;
        }

        /* XXX: Yes, we are dynamically modifying a value here via reflection, based on data some rando
         * provided via XML.  Is it dangerous?  Sure.  But, this is the best way to change whatever value
         * we want.
         */
        public override void ModifyVerbProperty (ThingWithComps parentThing) {
            if (AppliesTo != ModifierTarget.VerbProperties) return;

            VerbProperties modVerbProps = parentThing.TryGetComp<CompLootAffixableThing>().PrimaryVerbProps;
            ModifyVerbProperty(parentThing, modVerbProps);
        }

        public override void ResetVerbProperty (ThingWithComps parentThing) {
            if (AppliesTo != ModifierTarget.VerbProperties) return;

            var comp = parentThing.TryGetComp<CompLootAffixableThing>();
            VerbProperties srcVerbProps  = comp.PrimaryVerbPropsFromDef;
            VerbProperties destVerbProps = comp.PrimaryVerbProps;
            ResetVerbProperty(parentThing, srcVerbProps, destVerbProps);
        }

        public override void ResetVerbProperty (ThingWithComps parentThing, VerbProperties srcVerbProps, VerbProperties destVerbProps) {
            if (AppliesTo != ModifierTarget.VerbProperties) return;
            SetVerbProperty(destVerbProps, fieldInfo.GetValue(srcVerbProps));
        }

        public void SetVerbProperty (VerbProperties verbProperties, object value) {
            if (AppliesTo != ModifierTarget.VerbProperties) return;
            fieldInfo.SetValue(verbProperties, ConvertHelper.Convert(value, fieldType));
        }

        public override void ModifyTools (ThingWithComps parentThing) {
            if (AppliesTo != ModifierTarget.Tools) return;

            foreach (Tool modTool in parentThing.TryGetComp<CompLootAffixableThing>().Tools) {
                ModifyTool(parentThing, modTool);
            }
        }

        public override void ResetTools (ThingWithComps parentThing) {
            if (AppliesTo != ModifierTarget.Tools) return;

            var comp = parentThing.TryGetComp<CompLootAffixableThing>();
            List<Tool> srcTools  = comp.ToolsFromDef;
            List<Tool> destTools = comp.Tools;

            for (int i = 0; i < srcTools.Count; i++) {
                ResetTool(parentThing, srcTools[i], destTools[i]);
            }
        }

        public override void ResetTool (ThingWithComps parentThing, Tool srcTool, Tool destTool) {
            if (AppliesTo != ModifierTarget.Tools) return;
            SetTool(destTool, fieldInfo.GetValue(srcTool));
        }

        public void SetTool (Tool tool, object value) {
            if (AppliesTo != ModifierTarget.Tools) return;
            fieldInfo.SetValue(tool, ConvertHelper.Convert(value, fieldType));
        }

        public abstract override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel);
    }
}
