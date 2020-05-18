using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class LootAffixModifier_EquippedRandomLocalTeleport : LootAffixModifier_DoOverTime {
        // FIXME: Consider replacing with ModifierChangeLabelTranslateKey
        public override TaggedString ModifierChangeLabel {
            get { return "RimLoot_EquippedRandomLocalTeleportLabel".Translate( ModifierChangeString.Named("period") ); }
        }

        public override bool CanBeAppliedToThing (ThingWithComps thing) {
            // Anything that can be equipped
            return true;
        }

        public override void DoActivation (ThingWithComps thing) {
            Log.Message(thing + ": Doing activation for " + this);

            Pawn pawn = GetEquippedPawn(thing);
            if (pawn == null)           return;
            if (!(pawn.Map is Map map)) return;

            Vector3 oldDrawPos = pawn.DrawPos;

            IntVec3 newCell = map.AllCells.RandomElementByWeightWithFallback(
                // No burning, buildings, or non-standables
                c => c.Standable(map) && c.GetEdifice(map) == null && !c.ContainsStaticFire(map) ? 1 : 0,
                IntVec3.Invalid
            );
            if (newCell == IntVec3.Invalid) return;

            // Teleport!
            pawn.Position = newCell;
            pawn.Notify_Teleported(true, true);

            // SFX for the old location
            MoteMaker.ThrowTornadoDustPuff(oldDrawPos, map, 1.5f, Color.cyan);
            MoteMaker.ThrowDustPuffThick(oldDrawPos, map, 3f, Color.cyan);
            MoteMaker.ThrowLightningGlow(oldDrawPos, map, 3f);

            // Unfog the new location
            if (map.fogGrid.IsFogged(newCell)) {
                // [Reflection] map.fogGrid.FloodUnfogAdjacent(newCell);
                MethodInfo FloodUnfogAdjacent = AccessTools.Method(typeof(FogGrid), "FloodUnfogAdjacent");
                FloodUnfogAdjacent.Invoke(map.fogGrid, new object[] { newCell });
            }
        }
    }
}
