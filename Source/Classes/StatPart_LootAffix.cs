using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimLoot {
    class StatPart_LootAffix : StatPart {
        public LootAffixModifier_StatDefChange parentStatChanger;
        public LootAffixDef                    parentLootAffix;

        public override void TransformValue (StatRequest req, ref float val) {
            if (!parentStatChanger.AppliedOn(req)) return;
            val = parentStatChanger.ChangeValue(val);
        }

        public override string ExplanationPart (StatRequest req) {
            if (!parentStatChanger.AppliedOn(req)) return null;

            var thing = (ThingWithComps)req.Thing;
            var comp  = thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return null;

            return
                "RimLoot_AffixStatExplanationPart".Translate(comp.AllAffixesByAffixDefs[parentLootAffix]) + ": " +
                parentStatChanger.ModifierChangeString
            ;
        }

        // FIXME: MarketValue affix adjustments within the main MarketValue class

    }
}
