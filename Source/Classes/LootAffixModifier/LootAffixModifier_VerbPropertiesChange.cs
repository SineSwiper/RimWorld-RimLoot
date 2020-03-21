using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_VerbPropertiesChange : LootAffixModifier {
        public string  affectedField;
        public ToStringStyle toStringStyle = ToStringStyle.Integer;

        public float   preMinValue = -9999999f;  // mostly used if value was originally zero or too low for multiplication
        public float   addValue    = 0;
        public float   multiplier  = 1;
        public float   minValue    = -9999999f;
        public float   maxValue    =  9999999f;

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
            get {
                string str = "";

                if (preMinValue != -9999999f) str += string.Format("{0}={1} ", "min".Translate(), preMinValue.ToStringByStyle(toStringStyle, ToStringNumberSense.Absolute));
                if (addValue    != 0)         str += string.Format("{0} ",                        addValue.   ToStringByStyle(toStringStyle, ToStringNumberSense.Offset));
                if (multiplier  != 1)         str += string.Format("{0} ",                        multiplier. ToStringByStyle(toStringStyle, ToStringNumberSense.Factor));
                if (minValue    != -9999999f) str += string.Format("{0}={1} ", "min".Translate(), minValue.   ToStringByStyle(toStringStyle, ToStringNumberSense.Absolute));
                if (maxValue    !=  9999999f) str += string.Format("{0}={1} ", "max".Translate(), maxValue.   ToStringByStyle(toStringStyle, ToStringNumberSense.Absolute));
                return str;
            }
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

            // min/max sanity checks
            if (addValue == 0 && multiplier == 1 && preMinValue == -9999999f && minValue == -9999999f && maxValue == 9999999f)
                yield return "This modifier doesn't actually change anything";

            if (preMinValue != -9999999f) {
                if (preMinValue > maxValue) yield return string.Format("The preMinValue is higher than the maxValue: {0} > {1}", preMinValue, maxValue);
            }
            if (minValue    != -9999999f) {
                if (   minValue > maxValue) yield return string.Format("The minValue is higher than the maxValue: {0} > {1}",       minValue, maxValue);
            }
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
            val = ChangeValue(val);
            field.SetValue(verbProperties, ConvertHelper.Convert(val, type));

            Log.Message(string.Format("Changed {0} for {1}: {2}", affectedField, parentThing, val));
        }

        public float ChangeValue (float oldVal) {
            float newVal = oldVal;

            newVal = Mathf.Clamp(newVal, preMinValue, maxValue);
            newVal += addValue;
            newVal *= multiplier;
            newVal = Mathf.Clamp(newVal, minValue, maxValue);

            return newVal;
        }

    }
}
