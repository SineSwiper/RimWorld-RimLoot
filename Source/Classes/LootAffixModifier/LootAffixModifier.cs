using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimLoot {
    public enum ModifierTarget : byte {
        Item,
        Pawn,
        VerbProperties,
        VerbTarget,
        Tools,
        Projectile,
        PawnOverTime,
    };

    public abstract class LootAffixModifier : Editable {
        public float chance = 1f;
        public bool  dynamicAffixCost = false;

        public abstract ModifierTarget AppliesTo {
            get;
        }

        public abstract TaggedString ModifierChangeStat {
            get;
        }
        
        public virtual TaggedString ModifierChangeString {
            get {
                // FIXME: Do other languages perfer something other than "{0} chance"?
                return string.Format("{0} {1}", GenText.ToStringPercent(chance), "chance".Translate());
            }
        }

        public virtual TaggedString ModifierChangeLabel {
            get {
                return ModifierChangeStat + ": " + ModifierChangeString;
            }
        }

        public virtual TaggedString GetModifierChangeLabel () {
            return ModifierChangeLabel;
        }

        public virtual TaggedString GetModifierChangeLabel (ThingWithComps parentThing) {
            return ModifierChangeLabel;
        }

        public virtual IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            foreach (string configError in base.ConfigErrors())
                yield return configError;

            if (chance == 0f)
                yield return "Chance is zero; effect would never occur";
            if (Mathf.Clamp(chance, 0f, 1f) != chance)
                yield return "Chance is out-of-bounds; should be between >0 and 1";
        }

        public virtual void ResolveReferences (LootAffixDef parentDef) {
            base.ResolveReferences();
        }

        public virtual void PostLoadDef (LootAffixDef parentDef) {

        }

        public virtual void PostExposeData (ThingWithComps parentThing, LootAffixDef parentDef) {

        }

        public virtual void PostApplyAffix (ThingWithComps parentThing, LootAffixDef parentDef) {

        }

        public virtual void PreShotFired (ThingWithComps parentThing, LootAffixDef parentDef) {

        }

        public virtual void PostShotFired (ThingWithComps parentThing, LootAffixDef parentDef) {

        }

        public virtual void PostDestroy (ThingWithComps parentThing, LootAffixDef parentDef) {

        }

        public virtual void ModifyVerbProperty (ThingWithComps parentThing) {

        }

        public virtual void ResetVerbProperty (ThingWithComps parentThing) {

        }

        public virtual void ModifyVerbProperty (ThingWithComps parentThing, VerbProperties verbProperties) {

        }

        public virtual void ResetVerbProperty (ThingWithComps parentThing, VerbProperties srcVerbProps, VerbProperties destVerbProps) {

        }

        public virtual void ModifyTools (ThingWithComps parentThing) {

        }

        public virtual void ResetTools (ThingWithComps parentThing) {

        }

        public virtual void ModifyTool (ThingWithComps parentThing, Tool tool) {

        }

        public virtual void ResetTool (ThingWithComps parentThing, Tool srcTool, Tool destTool) {

        }

        public abstract bool CanBeAppliedToThing (ThingWithComps thing);

        public virtual bool AppliedOn (ThingWithComps thing) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return false;

            // This may sometimes get ran too early in the process for AllModifiers
            if (comp.fullStuffLabel == null) {
                var self = comp.AllAffixDefs.FirstOrFallback( lad => lad.modifiers.Contains(this) );
                return self != null;
            }

            return comp.AllModifiers.Contains<LootAffixModifier>(this);
        }

        public virtual IEnumerable<CompLootAffixableThing> AppliedOnGearFrom (Pawn pawn) {
            if (pawn.apparel != null) {
                foreach (Apparel apparel in pawn.apparel.WornApparel) {
                    if (AppliedOn(apparel)) yield return apparel.TryGetComp<CompLootAffixableThing>();
                }
            }
            if (pawn.equipment != null) {
                foreach (ThingWithComps thing in pawn.equipment.AllEquipmentListForReading) {
                    if (AppliedOn(thing)) yield return thing.TryGetComp<CompLootAffixableThing>();
                }
            }
        }

        public virtual float GetNewAffixCost (ThingWithComps thing, float affixCost) {
            return affixCost;
        }

        public virtual float GetRealChance (ThingWithComps thing, bool usePreModProps = false) {
            return chance;
        }

        public virtual bool ShouldActivate (ThingWithComps thing) {
            if (GetRealChance(thing) < Random.Range(0.0f, 1.0f)) return false;
            return true;
        }

        public virtual void DoActivation (ThingWithComps thing) {

        }

        public virtual IEnumerable<StatDrawEntry> SpecialDisplayStatsForThing(ThingWithComps parentThing, string preLabel) {
            return Enumerable.Empty<StatDrawEntry>();
        }

        public virtual IEnumerable<Dialog_InfoCard.Hyperlink> GetHyperlinks (ThingWithComps parentThing, LootAffixDef parentDef) {
            return Enumerable.Empty<Dialog_InfoCard.Hyperlink>();
        }

        public virtual void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, ThingWithComps parentThing, string preLabel) {

        }

    }
}
