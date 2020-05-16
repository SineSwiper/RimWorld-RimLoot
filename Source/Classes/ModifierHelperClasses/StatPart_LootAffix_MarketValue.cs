using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimLoot {
    class StatPart_LootAffix_MarketValue : StatPart {
        public override void TransformValue (StatRequest req, ref float val) {
            float? factor = GetFactor(req);
            if (factor == null) return;
            val *= (float)factor;
        }

        public override string ExplanationPart (StatRequest req) {
            float? factor = GetFactor(req);
            if (factor == null) return null;

            return
                "RimLoot_LootAffixModifiers".Translate() + ": " +
                parentStat.Worker.ValueToString( (float)factor, false, ToStringNumberSense.Factor )
            ;
        }

        private float? GetFactor (StatRequest req) {
            if (!req.HasThing) return null;
            if ( !typeof(ThingWithComps).IsAssignableFrom(req.Thing.GetType()) ) return null;

            var comp = req.Thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return null;

            /* https://www.desmos.com/calculator/wox5ocylro
             * 
             * This should _mostly_ to resolve to a y=x+1 graph, except in cases of x<1, where it does the
             * proper thing and keeps y positive.
             */
            double pts = comp.TotalAffixPoints;
            double exp = Math.Sqrt(Math.Abs(pts)) * Math.Sign(pts);
            return Math.Pow(2, exp).ChangeType<float>();
        }
    }
}
