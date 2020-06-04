using System.Linq;
using Verse;

namespace RimLoot {
    public abstract class SpecialThingFilterWorker_Affixes : SpecialThingFilterWorker {
        public override bool CanEverMatch (ThingDef def) {
            // DefsLoaded already did the hard work
            return def.HasComp(typeof(CompLootAffixableThing));
        }
    }

    public class SpecialThingFilterWorker_AffixesNone : SpecialThingFilterWorker_Affixes {
        public override bool Matches (Thing thing) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return false;
            return comp.AffixCount == 0;
        }
    }

    public class SpecialThingFilterWorker_AffixesOne : SpecialThingFilterWorker_Affixes {
        public override bool Matches (Thing thing) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return false;
            return comp.AffixCount == 1;
        }
    }

    public class SpecialThingFilterWorker_AffixesMulti : SpecialThingFilterWorker_Affixes {
        public override bool Matches (Thing thing) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return false;
            return comp.AffixCount >= 2;
        }
    }

    public class SpecialThingFilterWorker_AffixesNegative : SpecialThingFilterWorker_Affixes {
        public override bool Matches (Thing thing) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return false;
            return comp.AllAffixDefs.Any(lad => lad.affixCost < 0);
        }
    }

    public class SpecialThingFilterWorker_AffixesPositive : SpecialThingFilterWorker_Affixes {
        public override bool Matches (Thing thing) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return false;
            return comp.AllAffixDefs.Any(lad => lad.affixCost >= 0);  // include 0 in positive filter
        }
    }

    public class SpecialThingFilterWorker_AffixesDeadly : SpecialThingFilterWorker_Affixes {
        public override bool Matches (Thing thing) {
            var comp = thing.TryGetComp<CompLootAffixableThing>();
            if (comp == null) return false;
            return comp.AllAffixDefs.Any(lad => lad.IsDeadly);
        }
    }

}
