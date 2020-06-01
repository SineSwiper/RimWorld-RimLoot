using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimLoot {
    public class LootAffixModifier_EquippedDeath : LootAffixModifier_DoOverTime {

        public override string ModifierChangeLabelTranslateKey() {
            return "RimLoot_EquippedDeathLabel";
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Anything that can be equipped
            return true;
        }

        // FIXME: Need curse-equipped letters
        public override void DoActivation (ThingWithComps thing) {
            Pawn pawn = GetEquippedPawn(thing);
            if (pawn == null) return;
            if (pawn.Dead)    return;  // お前はもう死んでいる

            // SFX for the death location
            if (pawn.Map is Map map) {
                Vector3 oldDrawPos = pawn.DrawPos;
                for (int i = 1; i <= 10; i++) {
                    Color color = Rand.Bool ? Color.black : Color.red;
                    if (Rand.Bool) MoteMaker.ThrowTornadoDustPuff(oldDrawPos, map, i, color);
                    else           MoteMaker.ThrowDustPuffThick  (oldDrawPos, map, i, color);
                }
                MoteMaker.ThrowFireGlow(oldDrawPos.ToIntVec3(), map, 3f);
            }

            // Mark an entry in the battle log to make it more clear
            Find.BattleLog.Add(
                new BattleLogEntry_Event(
                    subject:   pawn,
                    eventDef:  DefDatabase<RulePackDef>.GetNamed("RimLoot_Event_DeathCurse"),
                    initiator: thing
                )
            );

            // Now DIE!
            pawn.Kill(
                new DamageInfo(
                    def:            DefDatabase<DamageDef>.GetNamed("RimLoot_DeathCurse"),
                    amount:         999_999,
                    instigator:     thing,
                    intendedTarget: pawn,
                    weapon:         thing.def
                )
            );
        }
    }
}
