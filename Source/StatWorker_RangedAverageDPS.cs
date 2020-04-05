using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class StatWorker_RangedAverageDPS : StatWorker {
        public override bool ShouldShowFor (StatRequest req) {
            return req.Def is ThingDef def && def.IsRangedWeapon;
        }

        public override float GetValueUnfinalized (StatRequest req, bool applyPostProcess = true) {
            if (!(req.Def is ThingDef def)) return 0.0f;

            CompEquippable         equipComp = null;
            CompLootAffixableThing  lootComp = null;
            if (req.HasThing) {
                var thing = (ThingWithComps)req.Thing;
                equipComp = thing.TryGetComp<CompEquippable>();
                 lootComp = thing.TryGetComp<CompLootAffixableThing>();
            }

            Verb           verb      = equipComp?.AllVerbs.First(v => v.verbProps.isPrimary);
            VerbProperties verbProps = verb != null ? verb.verbProps : def.Verbs.First(vp => vp.isPrimary);
            Pawn           attacker  = req.HasThing ? GetCurrentWeaponUser(req.Thing) : null;
            var            projProps = verbProps.defaultProjectile.projectile;

            var projModifier = (LootAffixModifier_VerbPropertiesChange_Def)lootComp?.AllModifiers.FirstOrFallback(
                lam => lam.AppliesTo == ModifierTarget.VerbProperties && lam is LootAffixModifier_VerbPropertiesChange_Def lamVPCD && lamVPCD.affectedField == "defaultProjectile"
            );
            ThingDef modProjectile = projModifier != null ? (ThingDef)projModifier.resolvedDef : null;
            var      modProjProps  = modProjectile?.projectile;

            float chance = modProjProps != null ? 1f - projModifier.GetRealChance(lootComp.parent) : 1f;
            if (chance <= 0.05f) chance = 1f;  // already permanently set to "base" verbProps

            float baseDamage = 
                req.HasThing         ? projProps.GetDamageAmount(req.Thing) :
                req.StuffDef != null ? projProps.GetDamageAmount( def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier, req.StuffDef) ) :
                                       projProps.GetDamageAmount( def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier) )
            ;
            float damage = baseDamage * verbProps.burstShotCount * chance;

            if (chance < 1f) {
                float  modChance     = 1f - chance;
                float  modBaseDamage = modProjProps.GetDamageAmount(req.Thing);

                damage += modBaseDamage * verbProps.burstShotCount * modChance;
            }

            // FIXME: Confirm warmupTime (and AimingDelayFactor) is used in a full shot cycle
            // FIXME: warmupTime * this.CasterPawn.GetStatValue(StatDefOf.AimingDelayFactor, true)).SecondsToTicks()
            float secondsSpent = 0;
            if (verb != null) secondsSpent = verbProps.AdjustedFullCycleTime(verb, attacker);
            else {
                secondsSpent  = verbProps.warmupTime + ((verbProps.burstShotCount - 1) * verbProps.ticksBetweenBurstShots).TicksToSeconds();
                secondsSpent += 
                    req.HasThing         ? req.Thing.GetStatValue  (StatDefOf.RangedWeapon_Cooldown, true) :
                    req.StuffDef != null ? def.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown, req.StuffDef) :
                                           def.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown)
                ;
            }

            // Every integer range possible as an average
            float avgAccuracy = 0;
            for (int i = 3; i <= verbProps.range; i++) {
                float rngAccuracy = verbProps.GetHitChanceFactor(req.Thing, i);
                if (attacker != null) rngAccuracy *= ShotReport.HitFactorFromShooter(attacker, i);
                avgAccuracy += rngAccuracy;
            }
            if (verbProps.range >= 3) avgAccuracy /= verbProps.range - 2;

            return secondsSpent == 0 ? 0.0f : damage / secondsSpent * avgAccuracy;
        }

        public override string GetExplanationUnfinalized (StatRequest req, ToStringNumberSense numberSense) {
            if (!(req.Def is ThingDef def)) return null;

            /* Damage section */
            CompEquippable         equipComp = null;
            CompLootAffixableThing  lootComp = null;
            if (req.HasThing) {
                var thing = (ThingWithComps)req.Thing;
                equipComp = thing.TryGetComp<CompEquippable>();
                 lootComp = thing.TryGetComp<CompLootAffixableThing>();
            }

            Verb           verb      = equipComp?.AllVerbs.First(v => v.verbProps.isPrimary);
            VerbProperties verbProps = verb != null ? verb.verbProps : def.Verbs.First(vp => vp.isPrimary);
            Pawn           attacker  = req.HasThing ? GetCurrentWeaponUser(req.Thing) : null;
            var            projProps = verbProps.defaultProjectile.projectile;

            var projModifier = (LootAffixModifier_VerbPropertiesChange_Def)lootComp?.AllModifiers.FirstOrFallback(
                lam => lam.AppliesTo == ModifierTarget.VerbProperties && lam is LootAffixModifier_VerbPropertiesChange_Def lamVPCD && lamVPCD.affectedField == "defaultProjectile"
            );
            ThingDef modProjectile = projModifier != null ? (ThingDef)projModifier.resolvedDef : null;
            var      modProjProps  = modProjectile?.projectile;

            float chance = modProjProps != null ? 1f - projModifier.GetRealChance(lootComp.parent) : 1f;
            if (chance <= 0.05f) chance = 1f;  // already permanently set to "base" verbProps
            string chanceStr = GenText.ToStringPercent(chance);

            float baseDamage = 
                req.HasThing         ? projProps.GetDamageAmount(req.Thing) :
                req.StuffDef != null ? projProps.GetDamageAmount( def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier, req.StuffDef) ) :
                                       projProps.GetDamageAmount( def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier) )
            ;
            float damage = baseDamage * verbProps.burstShotCount * chance;

            string reportText = "Damage".Translate() + ":\n";
            if (chance < 1f) {
                reportText += "    " + "RimLoot_StatsReport_ProjectileWithChance".Translate(
                    verbProps.defaultProjectile.Named("PROJECTILE"), chanceStr.Named("chance")
                ) + "\n";
                reportText += string.Format("    {0}: {1} * {2} * {3} = {4}\n\n",
                    "Damage".Translate(),
                    baseDamage.ToStringDecimalIfSmall(),
                    verbProps.burstShotCount,
                    chanceStr,
                    damage.ToStringDecimalIfSmall()
                );

                float  modChance    = 1f - chance;
                string modChanceStr = GenText.ToStringPercent(modChance);

                float  modBaseDamage = modProjProps.GetDamageAmount(req.Thing);
                float  modDamage     = modBaseDamage * verbProps.burstShotCount * modChance;

                reportText += "    " + "RimLoot_StatsReport_ProjectileWithChance".Translate(
                    modProjectile.Named("PROJECTILE"), modChanceStr.Named("chance")
                ) + "\n";
                reportText += string.Format("    {0}: {1} * {2} * {3} = {4}\n\n",
                    "Damage".Translate(),
                    modBaseDamage.ToStringDecimalIfSmall(),
                    verbProps.burstShotCount,
                    modChanceStr,
                    modDamage.ToStringDecimalIfSmall()
                );

                reportText += string.Format("{0}: {1}\n\n", "StatsReport_TotalValue".Translate(), (damage + modDamage).ToStringDecimalIfSmall());
            }
            else {
                reportText += "    " + "RimLoot_StatsReport_Projectile".Translate(verbProps.defaultProjectile.Named("PROJECTILE")) + "\n";
                reportText += string.Format("    {0}: {1} * {2} = {3}\n\n",
                    "Damage".Translate(),
                    baseDamage.ToStringDecimalIfSmall(),
                    verbProps.burstShotCount,
                    damage.ToStringDecimalIfSmall()
                );
            }

            /* Seconds per attack */
            float secondsSpent = 0;
            float cooldown = 
                req.HasThing         ? req.Thing.GetStatValue  (StatDefOf.RangedWeapon_Cooldown, true) :
                req.StuffDef != null ? def.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown, req.StuffDef) :
                                       def.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown)
            ;
            float burstShotTime = ((verbProps.burstShotCount - 1) * verbProps.ticksBetweenBurstShots).TicksToSeconds();

            if (verb != null) secondsSpent = verbProps.AdjustedFullCycleTime(verb, attacker);
            else              secondsSpent = verbProps.warmupTime + cooldown + burstShotTime;

            reportText += GenText.ToTitleCaseSmart( "SecondsPerAttackLower".Translate() ) + ":\n";
            reportText += string.Format("    {0}: {1}\n", "WarmupTime"            .Translate(), "PeriodSeconds".Translate( verbProps.warmupTime.ToStringDecimalIfSmall() ));
            if (burstShotTime > 0)
                reportText += string.Format("    {0}: {1}\n", "BurstShotFireRate" .Translate(), "PeriodSeconds".Translate( burstShotTime       .ToStringDecimalIfSmall() ));
            reportText += string.Format("    {0}: {1}\n", "CooldownTime"          .Translate(), "PeriodSeconds".Translate( cooldown            .ToStringDecimalIfSmall() ));
            reportText += string.Format("{0}: {1}\n\n",   "StatsReport_TotalValue".Translate(), "PeriodSeconds".Translate( secondsSpent        .ToStringDecimalIfSmall() ));

            /* Average accuracy */

            // Every integer range possible as an average
            float wpnAccuracy  = 0;
            float pawnAccuracy = 0;
            for (int i = 3; i <= verbProps.range; i++) {
                wpnAccuracy += verbProps.GetHitChanceFactor(req.Thing, i);
                if (attacker != null) pawnAccuracy += ShotReport.HitFactorFromShooter(attacker, i);
            }
            if (verbProps.range >= 3) {
                wpnAccuracy  /= verbProps.range - 2;
                if (attacker != null) pawnAccuracy /= verbProps.range - 2;
            }

            reportText += "AverageAccuracy".Translate() + ":\n";
            reportText += string.Format("    {0}: {1}\n", "ShootReportWeapon"            .Translate(), wpnAccuracy .ToStringPercent("F1"));
            if (pawnAccuracy > 0)
                reportText += string.Format("    {0}: {1}\n", "ShootReportShooterAbility".Translate(), pawnAccuracy.ToStringPercent("F1"));

            return reportText;
        }

        public override IEnumerable<Dialog_InfoCard.Hyperlink> GetInfoCardHyperlinks(StatRequest req) {
            foreach (var hyperlink in base.GetInfoCardHyperlinks(req))
                yield return hyperlink;
                
            // If there's an owner, link back to it
            if (GetCurrentWeaponUser(req.Thing) is Pawn pawn) yield return new Dialog_InfoCard.Hyperlink(pawn);
        }

        public static Pawn GetCurrentWeaponUser (Thing weapon) {
            if (weapon == null) return null;
            if (weapon.ParentHolder is Pawn_EquipmentTracker parentHolder) return parentHolder.pawn;
            return null;
        }
    }
}
