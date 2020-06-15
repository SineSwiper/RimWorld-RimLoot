using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    public class LootAffixModifier_ChangeProjectile : LootAffixModifier_VerbPropertiesChange_Def {

        // FIXME: Are these even used?
        public override TaggedString ModifierChangeStat {
            get {
                return resolvedDef.LabelCap;
            }
        }
        
        public override TaggedString ModifierChangeString {
            get {
                return base.ModifierChangeString;  // {0} chance
            }
        }

        public override TaggedString ModifierChangeLabel {
            get {
                NamedArgument[] args = GetModifierChangeLabelArguments();

                return chance >= 0.95f ?
                    "RimLoot_ChangeProjectileModifierLabel_Permanent".Translate(args) :
                    "RimLoot_ChangeProjectileModifierLabel_Chance"   .Translate(args)
                ;
            }
        }

        // Enhanced label with per-bullet chances
        public override TaggedString GetModifierChangeLabel (ThingWithComps thing) {
            VerbProperties modVerbProps = thing?.TryGetComp<CompLootAffixableThing>()?.PrimaryVerbProps;
            NamedArgument[] args = GetModifierChangeLabelArguments(thing);
            
            int   shotCount  = modVerbProps != null ? modVerbProps.burstShotCount : 0;
            float realChance = GetRealChance(thing);

            return
                realChance >= 0.95f ? "RimLoot_ChangeProjectileModifierLabel_Permanent"   .Translate(args) :
                thing == null       ? "RimLoot_ChangeProjectileModifierLabel_Chance"      .Translate(args) :
                shotCount> 1        ? "RimLoot_ChangeProjectileModifierLabel_ChanceBurst" .Translate(args) :
                                      "RimLoot_ChangeProjectileModifierLabel_ChanceSingle".Translate(args)
            ;
        }

        private NamedArgument[] GetModifierChangeLabelArguments (ThingWithComps thing = null) {
            List<NamedArgument> args = new List<NamedArgument> {};

            args.Add( resolvedDef.Named("PROJECTILE") );
            args.Add( GenText.ToStringPercent(chance).Named("chance") );
            if (thing != null) args.Add( GenText.ToStringPercent( GetRealChance(thing) ).Named("realChance") );

            return args.ToArray();
        }

        public override void ResolveReferences (LootAffixDef parentDef) {
            affectedField = "defaultProjectile";
            base.ResolveReferences(parentDef);
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Also include checks for tech level and its own bullet
            return
                thing.def.IsWeaponUsingProjectiles && thing.def.techLevel >= TechLevel.Industrial &&
                thing.def.Verbs.First(x => x.isPrimary) is VerbProperties vp && vp.defaultProjectile != (ThingDef)resolvedDef
            ;
        }

        // Affix cost needs to be based on whether the source projectile actual does more damage than the destination
        public override float GetNewAffixCost (ThingWithComps thing, float affixCost) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            VerbProperties verbProps = comp.PrimaryVerbPropsFromDef;
            var            projProps = verbProps.defaultProjectile.projectile;
            var         modProjProps = ((ThingDef)resolvedDef).projectile;

            float baseDamage = projProps   .damageDef.harmsHealth == false ? 0f :    projProps.GetDamageAmount(thing);
            float modDamage  = modProjProps.damageDef.harmsHealth == false ? 0f : modProjProps.GetDamageAmount(thing);
            
            float realChance  = GetRealChance(thing, true);  // can't use other affixes to calculate chance while affixes are being applied
            float totalDamage = baseDamage * (1 - realChance) + modDamage * realChance;

            float factor = baseDamage != 0f ? totalDamage / baseDamage : 100f;  // higher factor = positive effect on weapon

            // https://www.desmos.com/calculator/qbe15qrcpu
            // affix cost needs to be negative on fractional factors
            float newAffixCost;
            newAffixCost = Mathf.Log10(factor) * 10;
            newAffixCost = Mathf.Clamp(newAffixCost, -4, 4);  // if it's more dangerous, it shouldn't be dynamic
            return Mathf.RoundToInt(newAffixCost);
        }

        // The def's chance is the overall chance against the whole shot period.  The combined probability
        // needs to be separated out for each shot in the burst.
        public override float GetRealChance (ThingWithComps thing, bool usePreModProps = false) {
            var comp = thing?.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return chance;

            VerbProperties modVerbProps = usePreModProps ? comp.PrimaryVerbPropsFromDef : comp.PrimaryVerbProps;
            if (modVerbProps == null) return chance;

            // Give one-use items a 100% chance
            if (modVerbProps.verbClass == typeof(Verb_ShootOneUse)) return 1;

            // 1 - (1 - chance) to the count-th root 
            float realChance = 1f - Mathf.Pow(1f - chance, 1f / modVerbProps.burstShotCount);
            if (realChance >= 0.95f) realChance = 1;
            return realChance;
        }

        public override void PreShotFired (ThingWithComps parentThing, LootAffixDef parentDef) {
            // 95+% chance: This should have already been set by ModifyVerbProperties
            if (GetRealChance(parentThing) >= 0.95f) return;

            // Switch projectile based on chance hit
            if (ShouldActivate(parentThing)) ModifyVerbProperty(parentThing);
        }

        public override void PostShotFired (ThingWithComps parentThing, LootAffixDef parentDef) {
            // 95+% chance: This should have already been set by ModifyVerbProperties
            if (GetRealChance(parentThing) >= 0.95f) return;

            ResetVerbProperty(parentThing);
        }

        public override void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {
            // Nothing to inject
        }

    }
}
