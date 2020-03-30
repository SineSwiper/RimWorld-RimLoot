using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class ValueModifierSet : Editable {
        public float   preMinValue = -9999999f;  // mostly used if value was originally zero or too low for multiplication
        public float?  setValue;
        public float   addValue    = 0;
        public float   multiplier  = 1;
        public float   minValue    = -9999999f;
        public float   maxValue    =  9999999f;

        public string ModifierChangeString (ToStringStyle toStringStyle = ToStringStyle.FloatTwoOrThree) {
            string str = "";

            if (preMinValue != -9999999f) str += string.Format("{0}={1} ", "min".Translate(), preMinValue.ToStringByStyle(toStringStyle, ToStringNumberSense.Absolute));
            if (setValue    != null)      str += string.Format("={0} ",               ((float)setValue)  .ToStringByStyle(toStringStyle, ToStringNumberSense.Absolute));
            if (addValue    != 0)         str += string.Format("{0} ",                        addValue.   ToStringByStyle(toStringStyle, ToStringNumberSense.Offset  ));
            if (multiplier  != 1)         str += string.Format("{0} ",                        multiplier. ToStringByStyle(toStringStyle, ToStringNumberSense.Factor  ));
            if (minValue    != -9999999f) str += string.Format("{0}={1} ", "min".Translate(), minValue.   ToStringByStyle(toStringStyle, ToStringNumberSense.Absolute));
            if (maxValue    !=  9999999f) str += string.Format("{0}={1} ", "max".Translate(), maxValue.   ToStringByStyle(toStringStyle, ToStringNumberSense.Absolute));

            return str.TrimEnd();
        }

        public virtual IEnumerable<string> ConfigErrors (LootAffixDef parentDef, LootAffixModifier modifier) {
            // min/max sanity checks
            if (setValue == null && addValue == 0 && multiplier == 1 && preMinValue == -9999999f && minValue == -9999999f && maxValue == 9999999f)
                yield return "This modifier doesn't actually change anything";

            if (setValue != null && (addValue != 0 && multiplier != 1 && preMinValue != -9999999f && minValue != -9999999f && maxValue != 9999999f))
                yield return "The setValue option is mutually exclusive to all other value modifier options";
            
            if (multiplier == 0)
                yield return "A multiplier=0 is better displayed as setValue=0";

            if (preMinValue != -9999999f) {
                if (preMinValue > maxValue) yield return string.Format("The preMinValue is higher than the maxValue: {0} > {1}", preMinValue, maxValue);
            }
            if (minValue    != -9999999f) {
                if (   minValue > maxValue) yield return string.Format("The minValue is higher than the maxValue: {0} > {1}",       minValue, maxValue);
            }
        }

        public float ChangeValue (float oldVal) {
            if (setValue != null) return (float)setValue;

            float newVal = oldVal;
            newVal = Mathf.Clamp(newVal, preMinValue, maxValue);
            newVal += addValue;
            newVal *= multiplier;
            newVal = Mathf.Clamp(newVal, minValue, maxValue);

            return newVal;
        }

        public override string ToString() {
            return ModifierChangeString();
        }

        public new ValueModifierSet MemberwiseClone() {
            return (ValueModifierSet) base.MemberwiseClone();
        }
    }
}
