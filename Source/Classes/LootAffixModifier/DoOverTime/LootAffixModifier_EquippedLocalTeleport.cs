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
    public class LootAffixModifier_EquippedLocalTeleport : LootAffixModifier_DoOverTime {
        public bool isRandom = false;
    
        public override string ModifierChangeLabelTranslateKey() {
            return "RimLoot_Equipped" + (isRandom ? "Random" : "") + "LocalTeleportLabel";
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Anything that can be equipped
            return true;
        }

        public override void DoActivation (ThingWithComps thing) {
            Pawn pawn = GetEquippedPawn(thing);
            if (pawn == null)           return;
            if (!(pawn.Map is Map map)) return;

            Vector3 oldDrawPos = pawn.DrawPos;

            IntVec3 newCell = IntVec3.Invalid;
            if (isRandom) {
                newCell = map.AllCells.RandomElementByWeightWithFallback(
                    // No burning, buildings, or non-standables
                    c => c.Standable(map) && c.GetEdifice(map) == null && !c.ContainsStaticFire(map) ? 1 : 0,
                    IntVec3.Invalid
                );
            }
            else {
                Pawn_PathFollower pather = pawn.pather;
                if (pather == null || !pather.Moving) return;
                newCell = pather.LastPassableCellInPath;
            }
            if (newCell == IntVec3.Invalid) return;

            // Teleport!
            pawn.Position = newCell;
            pawn.Notify_Teleported(isRandom, true);
            if (!isRandom && pawn.CurJob != null) pawn.jobs.curDriver.Notify_PatherArrived();

            // SFX for the old/new location
            foreach (Vector3 pos in new Vector3[] { oldDrawPos, newCell.ToVector3() } ) {
                for (float i = 0.5f; i <= 3; i += 0.5f) {
                    Color color = i <= 1.5f ? Color.blue : Color.cyan;
                    if (Rand.Bool) MoteMaker.ThrowTornadoDustPuff(pos, map, i, color);
                    else           MoteMaker.ThrowDustPuffThick  (pos, map, i, color);
                }
                MoteMaker.ThrowLightningGlow(pos, map, 3f);
            }

            // Unfog the new location
            if (map.fogGrid.IsFogged(newCell)) {
                // [Reflection] map.fogGrid.FloodUnfogAdjacent(newCell);
                MethodInfo FloodUnfogAdjacent = AccessTools.Method(typeof(FogGrid), "FloodUnfogAdjacent");
                FloodUnfogAdjacent.Invoke(map.fogGrid, new object[] { newCell });
            }
        }
    }
}
