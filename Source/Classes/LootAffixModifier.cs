using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimLoot {
    public enum ModifierTarget : byte {
        Item,
        Pawn,
        VerbTarget
    };

    public abstract class LootAffixModifier : Editable {
        public float chance = 1f;
        public ModifierTarget appliesTo;

        // FIXME: Add something to label chances
        public abstract string ModifierChangeLabel {
            get;
        }

        public virtual IEnumerable<string> ConfigErrors (LootAffixDef parentDef) {
            if (chance == 0f)
                yield return "Chance is zero; effect would never occur";
            if (Mathf.Clamp(chance, 0f, 1f) != chance)
                yield return "Chance is out-of-bounds; should be between >0 and 1";
        }

        public virtual void ResolveReferences (LootAffixDef parentDef) {
        
        }

        public virtual void PostLoadSpecial (LootAffixDef parentDef) {

        }

        public virtual void PostApplyAffix (ThingWithComps parentThing, LootAffixDef parentDef) {

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

        public bool ShouldActivate (ThingWithComps thing) {
            if (chance < Random.Range(0.0f, 1.0f)) return false;
            return true;
        }

        public virtual IEnumerable<Dialog_InfoCard.Hyperlink> GetHyperlinks (ThingWithComps parentThing, LootAffixDef parentDef) {
            return Enumerable.Empty<Dialog_InfoCard.Hyperlink>();
        }
    }
}
