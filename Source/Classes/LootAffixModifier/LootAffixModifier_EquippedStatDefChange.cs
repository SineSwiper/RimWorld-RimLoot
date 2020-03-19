using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_EquippedStatDefChange : LootAffixModifier_StatDefChange {

        public new ModifierTarget appliesTo = ModifierTarget.Pawn;

        public override void ResolveReferences (LootAffixDef parentDef) {
            var statPart = new StatPart_LootAffix_Equipped {
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

            // Other affectedStat sanity checks
            if (!affectedStat.showOnPawns)      yield return "The affectedStat won't show up on pawns";
            if (!affectedStat.showOnHumanlikes) yield return "The affectedStat won't show up on humanlikes";

            StatCategoryDef category = affectedStat.category;
            List<StatCategoryDef> acceptableCategories = new List<StatCategoryDef>() {
                StatCategoryDefOf.Basics, StatCategoryDefOf.BasicsImportant, StatCategoryDefOf.BasicsNonPawn,
                StatCategoryDefOf.BasicsPawn, StatCategoryDefOf.BasicsPawnImportant, StatCategoryDefOf.PawnCombat,
                StatCategoryDefOf.PawnMisc, StatCategoryDefOf.PawnSocial, StatCategoryDefOf.PawnWork,
            };
            if (!acceptableCategories.Contains(category)) yield return "The affectedStat isn't in the typical stat categories: " + category;
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            /* Either it's a stat that's supposed to apply to any pawn that equips it, or it's a bugged modifier
            /* that will show up in the ConfigErrors.
            /*
             * But, let's keep these abilities on apparel for now.
             */
            return thing is Apparel;
        }

        public override void PostApplyAffix (ThingWithComps parentThing, LootAffixDef parentDef) {

        }

        public virtual IEnumerable<CompLootAffixableThing> AppliedOnGearFrom (StatRequest req) {
            if (!(req.Thing is Pawn pawn)) yield break;

            foreach (CompLootAffixableThing comp in AppliedOnGearFrom(pawn))
                yield return comp;
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStatsForThing(ThingWithComps parentThing, string preLabel) {
            // Add additional Equipped Stat Offsets modifiers
            var statDrawEntry = new StatDrawEntry(
                category:    StatCategoryDefOf.EquippedStatOffsets,
                label:       affectedStat.LabelCap,
                valueString: ModifierChangeString,  // much more flexible than value
                reportText:  affectedStat.description,
                displayPriorityWithinCategory: 10
            );

            StatRequest req = StatRequest.For(parentThing);

            // Extra properties, since we're overriding the typical stat value display
            statDrawEntry.stat           = affectedStat;
            statDrawEntry.hasOptionalReq = true;
            statDrawEntry.optionalReq    = req;

            // Calculate an example value
            StatWorker worker = affectedStat.Worker;
            float exampleValue = 
                parentThing.ParentHolder != null && parentThing.ParentHolder is Pawn pawn ?
                worker.GetValueUnfinalized( StatRequest.For(pawn) ) :
                affectedStat.defaultBaseValue
            ;

            // Use the Thing-tied StatRequest to hit our StatPart
            worker.FinalizeValue(req, ref exampleValue, true);

            // And finally, another private we need to dodge around to install both kinds of StatDrawEntry fields.

            // [Reflection] statDrawEntry.value = exampleValue;
            FieldInfo valueField = AccessTools.Field(typeof(StatDrawEntry), "value");
            valueField.SetValue(statDrawEntry, exampleValue);

            yield return statDrawEntry;
        }
    }
}
