using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public abstract class LootAffixModifier_DoOverTime : LootAffixModifier {
        public float mtbDays = 0f;
    
        public override ModifierTarget AppliesTo {
            get { return ModifierTarget.PawnOverTime; }
        }

        // Dummy entry, since label should be overridden by the subclass
        public override TaggedString ModifierChangeStat {
            get { return "Something happens!"; }
        }

        public override TaggedString ModifierChangeString {
            get {
                return GenDate.ToStringTicksToPeriod(
                    numTicks:     Mathf.RoundToInt(mtbDays * GenDate.TicksPerDay),
                    allowSeconds: false
                );
            }
        }

        public override TaggedString ModifierChangeLabel {
            get { return ModifierChangeLabelTranslateKey().Translate( ModifierChangeString.Named("period") ); }
        }

        public abstract string ModifierChangeLabelTranslateKey();

        public override IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            foreach (string configError in base.ConfigErrors(parentDef))
                yield return configError;

            if (mtbDays == 0f) yield return "mtbDays is not set!";
        }

        public override void PostApplyAffix (ThingWithComps parentThing, LootAffixDef parentDef) {
            RegisterToTickManager(parentThing);
        }

        public override void PostDestroy (ThingWithComps parentThing, LootAffixDef parentDef) {
            DeregisterFromTickManager(parentThing);
        }

        public void RegisterToTickManager (ThingWithComps thing) {
            TickerType tickerType = thing.def.tickerType;
            if (tickerType == TickerType.Rare)   return;  // already done
            if (tickerType == TickerType.Normal) return;  // faster than what we like, but we'll manage
            if (tickerType == TickerType.Long) {          // not supported
                // If we get anything like this, we might want to know about it eventually.  We could force in
                // CompTickLong support via Harmony patch.
                Log.Error(
                    "Unable to register " + thing + " to TickManager.  Using this kind of LootAffixModifier is " +
                    "(currently) unsupported for ThingDefs with tickerType=Long (like " + thing.def + ")."
                );
                return;
            }

            // Manipulating TickLists outside of ThingDef.tickerType requires private list access

            // [Reflection] TickManager.tickListRare.RegisterThing(thing)
            FieldInfo tickListRareField = AccessTools.Field(typeof(TickManager), "tickListRare");
            TickList tickListRare = (TickList)tickListRareField.GetValue(Find.TickManager);
            tickListRare.RegisterThing(thing);
        }

        public void DeregisterFromTickManager (ThingWithComps thing) {
            if (thing.def.tickerType != TickerType.Never) return;  // already done or complained about

            // [Reflection] TickManager.tickListRare.DeregisterThing(thing)
            FieldInfo tickListRareField = AccessTools.Field(typeof(TickManager), "tickListRare");
            TickList tickListRare = (TickList)tickListRareField.GetValue(Find.TickManager);
            tickListRare.DeregisterThing(thing);
        }

        public Pawn GetEquippedPawn (ThingWithComps thing) {
            IThingHolder holder = thing.ParentHolder;
            if (holder == null) return null;

            Pawn pawn = null;
            if      (thing  is Apparel apparel)                      pawn = apparel.Wearer;
            else if (holder is Pawn_ApparelTracker   apparelTracker) pawn = apparelTracker.pawn;
            else if (holder is Pawn_EquipmentTracker equipTracker)   pawn = equipTracker.pawn;

            return pawn;
        }

        public override bool ShouldActivate (ThingWithComps thing) {
            Pawn pawn = GetEquippedPawn(thing);
            if (pawn == null) return false;

            float tickInternal = thing.def.tickerType == TickerType.Normal ? 1 : GenTicks.TickRareInterval;
            return Rand.MTBEventOccurs(mtbDays, GenDate.TicksPerDay, tickInternal);
        }

        public abstract override void DoActivation (ThingWithComps thing);
    }
}
