using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_VerbPropertiesChange : LootAffixModifier {
        public string           affectedField;
        public ToStringStyle    toStringStyle = ToStringStyle.Integer;
        public ValueModifierSet valueModifier;

        public override ModifierTarget AppliesTo {
            get { return ModifierTarget.VerbProperties; }
        }

        public override string ModifierChangeStat {
            get {
                // FIXME: Might need some tweaks to look for missing translations
                string key = GenText.ToTitleCaseSmart(affectedField);
                return key.Translate();
            }
        }
        
        public override string ModifierChangeString {
            get { return valueModifier.ModifierChangeString(toStringStyle); }
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
            }
            else {
                System.Type type = field.FieldType;
                if (type != typeof(float) && type != typeof(int)) yield return "Unsupported type: " + type;
            }

            // ValueModifierSet sanity checks
            if (valueModifier == null) {
                yield return "The valueModifer is not set!";
                yield break;
            }

            foreach (string configError in valueModifier.ConfigErrors(parentDef, this))
                yield return configError;
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Only range weapons have verb properties that make sense here
            return thing.def.IsRangedWeapon;
        }

        public override void ModifyVerbProperties (ThingWithComps parentThing, VerbProperties verbProperties, LootAffixDef parentDef) {
            /* XXX: Yes, we are dynamically modifying a value here via reflection, based on data some rando
             * provided via XML.  Is it dangerous?  Sure.  But, this is the best way to change whatever value
             * we want.
             */

            FieldInfo field = AccessTools.Field(typeof(VerbProperties), affectedField);
            System.Type type = field.FieldType;
            float val = (float)field.GetValue(verbProperties);
            val = valueModifier.ChangeValue(val);
            field.SetValue(verbProperties, ConvertHelper.Convert(val, type));
        }

    }
}
