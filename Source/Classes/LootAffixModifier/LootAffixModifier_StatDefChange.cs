using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_StatDefChange : LootAffixModifier {
        public StatDef          affectedStat;
        public ValueModifierSet valueModifier;

        public override ModifierTarget AppliesTo {
            get { return ModifierTarget.Item; }
        }

        public override TaggedString ModifierChangeStat {
            get {
                return affectedStat.LabelForFullStatListCap;
            }
        }
        
        public override TaggedString ModifierChangeString {
            get { return valueModifier.ModifierChangeString(affectedStat.ToStringStyleUnfinalized); }
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
            if (affectedStat == null) {
                yield return "The affectedStat is not set!";
                yield break;
            }
            if (affectedStat.forInformationOnly) yield return "The affectedStat is for information purposes only";
            if (affectedStat.alwaysHide)         yield return "The affectedStat is always hidden";

            // ValueModifierSet sanity checks
            if (valueModifier == null) {
                yield return "The valueModifer is not set!";
                yield break;
            }

            foreach (string configError in valueModifier.ConfigErrors(parentDef, this))
                yield return configError;

            ValueModifierSet vm = valueModifier;
            StatDef         ast = affectedStat;

            if (vm.preMinValue != -9999999f) {
                if (vm.preMinValue >  ast.maxValue) yield return string.Format("The preMinValue is higher than the affectedStat's maxValue: {0} > {1}", vm.preMinValue, ast.maxValue);
                if (vm.preMinValue <  ast.minValue) yield return string.Format("The preMinValue is lower than the affectedStat's minValue: {0} < {1}",  vm.preMinValue, ast.minValue);
                if (vm.preMinValue == ast.minValue) yield return string.Format("The preMinValue is equal than the affectedStat's minValue: {0}",        vm.preMinValue);
            }
            if (vm.minValue    != -9999999f) {
                if (vm.minValue >  ast.maxValue) yield return string.Format("The minValue is higher than the affectedStat's maxValue: {0} > {1}", vm.minValue, ast.maxValue);
                if (vm.minValue <  ast.minValue) yield return string.Format("The minValue is lower than the affectedStat's minValue: {0} < {1}",  vm.minValue, ast.minValue);
                if (vm.minValue == ast.minValue) yield return string.Format("The minValue is equal than the affectedStat's minValue: {0}",        vm.minValue);
            }
            if (vm.maxValue    !=  9999999f) {
                if (vm.maxValue >  ast.maxValue) yield return string.Format("The maxValue is higher than the affectedStat's maxValue: {0} > {1}", vm.maxValue, ast.maxValue);
                if (vm.maxValue <  ast.minValue) yield return string.Format("The maxValue is lower than the affectedStat's minValue: {0} < {1}",  vm.maxValue, ast.minValue);
                if (vm.maxValue == ast.maxValue) yield return string.Format("The maxValue is equal than the affectedStat's maxValue: {0}",        vm.maxValue);
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
            float newVal = valueModifier.ChangeValue(oldVal);

            // Just in case we're exceeding normal StatDef bounds
            if (affectedStat.postProcessCurve == null) newVal = Mathf.Clamp(newVal, affectedStat.minValue, affectedStat.maxValue);

            return newVal;
        }

    }
}
