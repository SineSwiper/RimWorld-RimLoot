using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimLoot {
    class StatPart_LootAffix_MeleeAverageDPS : StatPart {
        public override void TransformValue (StatRequest req, ref float val) {
            if (!req.HasThing) return;
            if ( !typeof(ThingWithComps).IsAssignableFrom(req.Thing.GetType()) ) return;

            var comp = req.Thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return;
            if (comp.Tools.NullOrEmpty()) return;

            float ttlToolChanceFactor = comp.Tools.Sum(t => t.chanceFactor);
            foreach (Tool tool in comp.Tools) {
                if (tool.extraMeleeDamages.NullOrEmpty()) continue;
                foreach (ExtraDamage extraDamage in tool.extraMeleeDamages) {
                    val += 
                        extraDamage.amount * tool.AdjustedCooldown(req.Thing) *
                        extraDamage.chance * (tool.chanceFactor / ttlToolChanceFactor)
                    ;
                }
            }
        }

        public override string ExplanationPart (StatRequest req) {
            if (!req.HasThing) return null;
            if ( !typeof(ThingWithComps).IsAssignableFrom(req.Thing.GetType()) ) return null;

            var comp = req.Thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return null;
            if (comp.Tools.NullOrEmpty()) return null;

            string extraParts = "";
            foreach (ExtraDamage extraDamage in comp.Tools.Where(t => t.extraMeleeDamages != null).SelectMany(t => t.extraMeleeDamages).Distinct().ToList()) {
                extraParts += "RimLoot_ToolExtraDamageModifierLabel".Translate(
                    GenText.ToStringPercent(extraDamage.chance).Named("chance"),
                    extraDamage.amount.Named("amount"),
                    extraDamage.def.Named("DAMAGE")
                ) + "\n";
            }

            return extraParts;
        }
    }
}
