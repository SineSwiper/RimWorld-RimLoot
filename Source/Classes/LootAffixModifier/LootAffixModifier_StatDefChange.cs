using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_StatDefChange : LootAffixModifier {
        public StatDef affectedStat;
        public float   preMinValue = -9999999f;  // mostly used if value was originally zero or too low for multiplication
        public float   addValue    = 0;
        public float   multiplier  = 1;
        public float   minValue    = -9999999f;
        public float   maxValue    =  9999999f;

        public new ModifierTarget appliesTo = ModifierTarget.Item;  // default

        public override string ModifierChangeStat {
            get {
                return affectedStat.LabelForFullStatListCap;
            }
        }
        
        public override string ModifierChangeString {
            get {
                string str = "";
                StatWorker worker = affectedStat.Worker;

                if (preMinValue != -9999999f) str += string.Format("{0}={1} ", "min".Translate(), worker.ValueToString(preMinValue, false, ToStringNumberSense.Absolute));
                if (addValue    != 0)         str += string.Format("{0} ",                        worker.ValueToString(addValue,    false, ToStringNumberSense.Offset));
                if (multiplier  != 1)         str += string.Format("{0} ",                        worker.ValueToString(multiplier,  false, ToStringNumberSense.Factor));
                if (minValue    != -9999999f) str += string.Format("{0}={1} ", "min".Translate(), worker.ValueToString(minValue,    false, ToStringNumberSense.Absolute));
                if (maxValue    !=  9999999f) str += string.Format("{0}={1} ", "max".Translate(), worker.ValueToString(maxValue,    false, ToStringNumberSense.Absolute));
                return str;
            }
        }

        // FIXME: Some way to combine StatParts for multiple LADs?
        public override void ResolveReferences (LootAffixDef parentDef) {
            var statPart = new StatPart_LootAffix {
                parentStat        = affectedStat,
                parentStatChanger = this,
                parentLootAffix   = parentDef,
            };

            if (affectedStat.parts == null)
                affectedStat.parts = new List<StatPart> { statPart };
            else
                affectedStat.parts.Add(statPart)
            ;

            affectedStat.ResolveReferences();
            affectedStat.PostLoad();  // sometimes a reload, since we added a new part
        }

        public override IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            foreach (string configError in base.ConfigErrors(parentDef))
                yield return configError;

            // affectedStat sanity checks
            if (affectedStat.forInformationOnly) yield return "The affectedStat is for information purposes only";
            if (affectedStat.alwaysHide)         yield return "The affectedStat is always hidden";
            // FIXME: showOnPawns, showOnHumanlikes checks for subclasses

            // min/max sanity checks
            if (addValue == 0 && multiplier == 1 && preMinValue == -9999999f && minValue == -9999999f && maxValue == 9999999f)
                yield return "This modifier doesn't actually change anything";

            if (preMinValue != -9999999f) {
                if (preMinValue >               maxValue) yield return string.Format("The preMinValue is higher than the maxValue: {0} > {1}",                preMinValue,              maxValue);
                if (preMinValue >  affectedStat.maxValue) yield return string.Format("The preMinValue is higher than the affectedStat's maxValue: {0} > {1}", preMinValue, affectedStat.maxValue);
                if (preMinValue <  affectedStat.minValue) yield return string.Format("The preMinValue is lower than the affectedStat's minValue: {0} < {1}",  preMinValue, affectedStat.minValue);
                if (preMinValue == affectedStat.minValue) yield return string.Format("The preMinValue is equal than the affectedStat's minValue: {0}",        preMinValue);
            }
            if (minValue    != -9999999f) {
                if (minValue >               maxValue) yield return string.Format("The minValue is higher than the maxValue: {0} > {1}",                minValue,              maxValue);
                if (minValue >  affectedStat.maxValue) yield return string.Format("The minValue is higher than the affectedStat's maxValue: {0} > {1}", minValue, affectedStat.maxValue);
                if (minValue <  affectedStat.minValue) yield return string.Format("The minValue is lower than the affectedStat's minValue: {0} < {1}",  minValue, affectedStat.minValue);
                if (minValue == affectedStat.minValue) yield return string.Format("The minValue is equal than the affectedStat's minValue: {0}",        minValue);
            }
            if (maxValue    !=  9999999f) {
                if (maxValue >  affectedStat.maxValue) yield return string.Format("The maxValue is higher than the affectedStat's maxValue: {0} > {1}", maxValue, affectedStat.maxValue);
                if (maxValue <  affectedStat.minValue) yield return string.Format("The maxValue is lower than the affectedStat's minValue: {0} < {1}",  maxValue, affectedStat.minValue);
                if (maxValue == affectedStat.maxValue) yield return string.Format("The maxValue is equal than the affectedStat's maxValue: {0}",        maxValue);
            }
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // ShouldShowFor doesn't show Max HP, so force it in
            if (affectedStat == StatDefOf.MaxHitPoints) {
                return thing.def.useHitPoints;
            }

            StatRequest req = StatRequest.For(thing);
            return affectedStat.Worker.ShouldShowFor(req);
        }

        public override void PostApplyAffix (ThingWithComps parentThing, LootAffixDef parentDef) {
            // Make sure any changes in max values fixes the current HPs
            if (affectedStat == StatDefOf.MaxHitPoints) {
                parentThing.HitPoints = parentThing.MaxHitPoints;
            }
        }

        public virtual bool AppliedOn (StatRequest req) {
            if (req.Thing is ThingWithComps thingWithComps) return AppliedOn(thingWithComps);
            return false;
        }

        public float ChangeValue (float oldVal) {
            float newVal = oldVal;

            newVal = Mathf.Clamp(newVal, preMinValue, maxValue);
            newVal += addValue;
            newVal *= multiplier;
            newVal = Mathf.Clamp(newVal, minValue, maxValue);

            // Just in case we're exceeding normal StatDef bounds
            newVal = Mathf.Clamp(newVal, affectedStat.minValue, affectedStat.maxValue);

            return newVal;
        }

    }
}
