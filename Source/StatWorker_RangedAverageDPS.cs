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

            CompEquippable comp = null;
            if (req.HasThing) {
                var thing = (ThingWithComps)req.Thing;
                comp = thing.TryGetComp<CompEquippable>();
            }

            Verb           verb      = comp?.AllVerbs.First(v => v.verbProps.isPrimary);
            VerbProperties verbProps = verb != null ? verb.verbProps : def.Verbs.First(vp => vp.isPrimary);
            Pawn           attacker  = req.HasThing ? GetCurrentWeaponUser(req.Thing) : null;
            var            projProps = verbProps.defaultProjectile.projectile;

            float damage = verbProps.burstShotCount;
            damage *= 
                req.HasThing         ? projProps.GetDamageAmount(req.Thing) :
                req.StuffDef != null ? projProps.GetDamageAmount( def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier, req.StuffDef) ) :
                                       projProps.GetDamageAmount( def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier) )
            ;

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

            CompEquippable comp = null;
            if (req.HasThing) {
                var thing = (ThingWithComps)req.Thing;
                comp = thing.TryGetComp<CompEquippable>();
            }

            Verb           verb      = comp?.AllVerbs.First(v => v.verbProps.isPrimary);
            VerbProperties verbProps = verb != null ? verb.verbProps : def.Verbs.First(vp => vp.isPrimary);
            Pawn           attacker  = req.HasThing ? GetCurrentWeaponUser(req.Thing) : null;
            var            projProps = verbProps.defaultProjectile.projectile;

            float baseDamage = 
                req.HasThing         ? projProps.GetDamageAmount(req.Thing) :
                req.StuffDef != null ? projProps.GetDamageAmount( def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier, req.StuffDef) ) :
                                       projProps.GetDamageAmount( def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier) )
            ;
            float damage = baseDamage * verbProps.burstShotCount;

            string reportText = "Damage".Translate() + ":\n";
            reportText += string.Format("    {0}: {1}\n", "Projectile".Translate(), verbProps.defaultProjectile.LabelCap);
            reportText += string.Format("    {0}: {1} * {2} = {3}\n\n",
                "Damage".Translate(),
                baseDamage,
                verbProps.burstShotCount,
                baseDamage * verbProps.burstShotCount
            );


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
            reportText += string.Format("    {0}: {1}\n", "WarmupTime"            .Translate(), "PeriodSeconds".Translate( verbProps.warmupTime.ToString("0.##") ));
            if (burstShotTime > 0)
                reportText += string.Format("    {0}: {1}\n", "BurstShotFireRate" .Translate(), "PeriodSeconds".Translate( burstShotTime       .ToString("0.##") ));
            reportText += string.Format("    {0}: {1}\n", "CooldownTime"          .Translate(), "PeriodSeconds".Translate( cooldown            .ToString("0.##") ));
            reportText += string.Format("{0}: {1}\n\n",   "StatsReport_TotalValue".Translate(), "PeriodSeconds".Translate( secondsSpent        .ToString("0.##") ));

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
