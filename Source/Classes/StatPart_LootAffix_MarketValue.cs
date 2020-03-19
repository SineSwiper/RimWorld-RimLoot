using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimLoot {
    class StatPart_LootAffix_MarketValue : StatPart {
        public override void TransformValue (StatRequest req, ref float val) {
            if (!req.HasThing) return;
            if ( !typeof(ThingWithComps).IsAssignableFrom(req.Thing.GetType()) ) return;

            var comp = req.Thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return;

            val *= comp.TotalAffixPoints;
        }

        public override string ExplanationPart (StatRequest req) {
            if (!req.HasThing) return null;
            if ( !typeof(ThingWithComps).IsAssignableFrom(req.Thing.GetType()) )  return null;

            var comp = req.Thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null)  return null;

            return
                "RimLoot_LootAffixModifiers".Translate() + ": " +
                parentStat.Worker.ValueToString(comp.TotalAffixPoints, false, ToStringNumberSense.Factor)
            ;
        }
    }
}
