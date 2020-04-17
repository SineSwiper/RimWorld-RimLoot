using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_ToolsChange_ExtraDamage : LootAffixModifier_ObjectChanger {
        public ExtraDamage extraDamage;

        public override ModifierTarget AppliesTo {
            get { return ModifierTarget.Tools; }
        }

        public override TaggedString ModifierChangeStat {
            get {
                return extraDamage.def.LabelCap;
            }
        }
        
        public override TaggedString ModifierChangeString {
            get {
                return base.ModifierChangeString;  // {0} chance
            }
        }

        public override TaggedString ModifierChangeLabel {
            get {
                return "RimLoot_ToolExtraDamageModifierLabel".Translate(
                    GenText.ToStringPercent(chance).Named("chance"),
                    extraDamage.amount.Named("amount"),
                    extraDamage.def.Named("DAMAGE")
                );
            }
        }

        public override void ResolveReferences (LootAffixDef parentDef) {
            // FIXME: Might need to split this framework for ProjectileProperties + SurpriseAttackProps
            affectedField = "extraMeleeDamages";

            if (chance != 1) extraDamage.chance = chance;
            else             chance = extraDamage.chance;

            base.ResolveReferences(parentDef);
        }

        public override IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            foreach (string configError in base.ConfigErrors(parentDef))
                yield return configError;

            if (extraDamage == null) {
                yield return "extraDamage is not set!";
                yield break;
            }
        }

        // Also check for the same damage types
        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            if (!thing.def.IsMeleeWeapon) return false;
            return !thing.def.tools.
                SelectMany(t   => t.capacities.AsEnumerable()).
                SelectMany(tcd => tcd.VerbsProperties).
                Any       (vp  => vp.meleeDamageDef == extraDamage.def)
            ;
        }

        public override void ModifyTool (ThingWithComps parentThing, Tool tool) {
            if (tool.extraMeleeDamages == null) tool.extraMeleeDamages = new List<ExtraDamage> { extraDamage };
            else                                tool.extraMeleeDamages.AddDistinct(extraDamage);
        }

        public override void ResetTool (ThingWithComps parentThing, Tool srcTool, Tool destTool) {
            if (destTool.extraMeleeDamages != null) destTool.extraMeleeDamages.Remove(extraDamage);
        }

        // FIXME
        public override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {
            basicStatDesc.SpecialDisplayStatsInjectors(
                statDrawEntry:  statDrawEntry,
                preLabel:       preLabel,
                parentThing:    parentThing,
                parentModifier: this,
                curDef:         extraDamage.def  // FIXME: placeholder
            );
        }

    }
}
