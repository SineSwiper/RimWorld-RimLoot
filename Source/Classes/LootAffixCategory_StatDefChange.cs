using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixCategory_StatDefChange : LootAffixCategory {
        public StatDef affectedStat;
        public float   preMinValue = -9999999f;  // mostly used if value was originally zero or too low for multiplication
        public float   addValue    = 0;
        public float   multiplier  = 1;
        public float   minValue    = -9999999f;
        public float   maxValue    =  9999999f;

        public new ModifierTarget appliesTo = ModifierTarget.Item;

        public string ModifierChangeString {
            get {
                // FIXME: Deal with stranger combinations, like min/max manipulation
                string str = "";
                if (addValue   != 0) str += string.Format("+{0} ", GenText.ToStringDecimalIfSmall(addValue));
                if (multiplier != 1) str += string.Format("x{0} ", GenText.ToStringPercent(multiplier));
                return str;
            }
        }

        public override string ModifierChangeLabel {
            get {
                return affectedStat.LabelForFullStatListCap + ": " + ModifierChangeString;
            }
        }

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

            if (affectedStat.forInformationOnly)  yield return "The affectedStat is for information purposes only";
            if (addValue == 0 && multiplier == 1) yield return "This modifier doesn't actually change anything";

            // FIXME: Other checks for StatDefs that would never apply
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            StatRequest req = StatRequest.For(thing);
            return affectedStat.Worker.ShouldShowFor(req);
        }

        public override void PostApplyAffix (ThingWithComps parentThing, LootAffixDef parentDef) {
            // Make sure any changes in max values fixes the current HPs
            if (affectedStat == StatDefOf.MaxHitPoints) {
                parentThing.HitPoints = parentThing.MaxHitPoints;
            }
        }

        public bool AppliedOn (StatRequest req) {
            if (req == null)       return false;
            if (req.Thing == null) return false;
            if (req.Thing.GetType() != typeof(ThingWithComps)) return false;

            return AppliedOn( (ThingWithComps)req.Thing );
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
