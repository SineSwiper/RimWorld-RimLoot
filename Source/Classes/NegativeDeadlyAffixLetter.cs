using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimLoot {
    public class NegativeDeadlyAffixLetter : ChoiceLetter {
        public DiaOption Option_ReadMore {
            get {
                GlobalTargetInfo target = lookTargets.TryGetPrimaryTarget();
                var diaOption = new DiaOption( "ReadMore".Translate() );
                diaOption.action = () => {
                    CameraJumper.TryJumpAndSelect(target);
                    Find.LetterStack.RemoveLetter(this);
                    InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Gear));
                };
                diaOption.resolveTree = true;
        
                if (!target.IsValid) diaOption.Disable(null);
                return diaOption;
            }
        }

        public override IEnumerable<DiaOption> Choices {
            get {
                yield return Option_Close;
                if (lookTargets.IsValid()) yield return Option_ReadMore;
            }
        }

        public override void OpenLetter() {
            Pawn pawn = lookTargets.TryGetPrimaryTarget().Thing as Pawn;

            ThingWithComps deadlyItem = (ThingWithComps)lookTargets.targets.First(gti => gti.HasThing && gti.Thing.TryGetComp<CompLootAffixableThing>() != null).Thing;
            var            comp       = deadlyItem.TryGetComp<CompLootAffixableThing>();
            LootAffixDef   affix      = comp.AllAffixDefs.First(lad => lad.IsNegativeDeadly);
            string         affixLabel = comp.AllAffixesByAffixDefs[affix];

            TaggedString text = "RimLoot_NegativeDeadlyAffixLetter_Desc".Translate(
                pawn.Named("PAWN"),
                deadlyItem.Named("ITEM"),
                affix.FullStatsReport(deadlyItem, affixLabel).Named("EFFECT")
            );
            
            DiaNode nodeRoot = new DiaNode(text);
            nodeRoot.options.AddRange(Choices);
            Find.WindowStack.Add(new Dialog_NodeTreeWithFactionInfo(
                nodeRoot:  nodeRoot,
                faction:   relatedFaction,
                radioMode: radioMode,
                title:     title
            ));
        }
    }
}
