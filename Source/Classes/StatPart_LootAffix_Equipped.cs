﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimLoot {
    class StatPart_LootAffix_Equipped : StatPart_LootAffix {
        public new LootAffixModifier_EquippedStatDefChange parentStatChanger;

        // FIXME: Fix this!  Not actually applying at Pawn.
        public override void TransformValue (StatRequest req, ref float val) {
            // Typical Pawn stat request
            List<CompLootAffixableThing> comps = parentStatChanger.AppliedOnGearFrom(req).ToList();
            if (comps.Count > 0) {
                foreach (CompLootAffixableThing comp in comps) {
                    val = parentStatChanger.ChangeValue(val);
                    Log.Message("TransformValue: " + string.Join(", ", comp.parent, val));
                }
            }
            // Thing stat request for the EquippedStatOffsets section; works off of a base value
            else if (parentStatChanger.AppliedOn(req)) {
                val = parentStatChanger.ChangeValue(val);
            }
        }

        public override string ExplanationPart (StatRequest req) {
            // Typical Pawn stat request
            List<CompLootAffixableThing> comps = parentStatChanger.AppliedOnGearFrom(req).ToList();
            if (comps.Count > 0) {
                List<string> partTexts = new List<string>();
                foreach (CompLootAffixableThing comp in comps) {
                    partTexts.Add(
                        "RimLoot_AffixStatOnGearExplanationPart".Translate(
                            comp.AllAffixesByAffixDefs[parentLootAffix],
                            comp.parent.def.LabelCap  // use smaller label from ThingDef
                        ) + ": " +
                        parentStatChanger.ModifierChangeString
                    );
                }
                return string.Join("\n", partTexts);
            }
            // Thing stat request for the EquippedStatOffsets section
            else if (parentStatChanger.AppliedOn(req)) {
                var thing = (ThingWithComps)req.Thing;
                var comp  = thing.TryGetComp<CompLootAffixableThing>();
                if (comp == null) return null;

                return
                    "RimLoot_AffixStatExplanationPart".Translate(comp.AllAffixesByAffixDefs[parentLootAffix]) + ": " +
                    parentStatChanger.ModifierChangeString
                ;
            }

            return null;
        }

        public override IEnumerable<Dialog_InfoCard.Hyperlink> GetInfoCardHyperlinks(StatRequest req) {
            // Typical Pawn stat request; should have hyperlinks back to gear
            List<CompLootAffixableThing> comps = parentStatChanger.AppliedOnGearFrom(req).ToList();
            if (comps.Count > 0) {
                foreach (CompLootAffixableThing comp in comps) {
                    yield return new Dialog_InfoCard.Hyperlink(comp.parent);
                }
            }
        }

    }
}
