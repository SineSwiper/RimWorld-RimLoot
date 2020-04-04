using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimLoot {
    public static class DebugToolsLootAffixes {

        [DebugAction("RimLoot", "Try place random affixable", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TryPlaceRandomAffixable() {
            ThingDef thingDef = DefDatabase<ThingDef>.AllDefs.RandomElementByWeight(t =>
                DebugThingPlaceHelper.IsDebugSpawnable(t, false) && t.HasComp(typeof(CompLootAffixableThing)) ? 1 : 0
            );
            DebugThingPlaceHelper.DebugSpawn(thingDef, UI.MouseCell());
        }

        [DebugAction("RimLoot", "Try place random affixable...", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TryPlaceRandomAffixableWithOptions() {
            List<DebugMenuOption> options = new List<DebugMenuOption>();
            for (int i = 1; i <= 4; i++) {
                int ttlAffixes = i;  // local for closures
                options.Add(new DebugMenuOption(i + " Affix", DebugMenuOptionMode.Action, () => {
                    List<DebugMenuOption> options2 = new List<DebugMenuOption>();
                    for (int j = 0; j <= 12; j++) {
                        float affixPoints = j;  // local for closures
                        options2.Add(new DebugMenuOption(j + " points", DebugMenuOptionMode.Tool, () => {
                            ThingDef thingDef = DefDatabase<ThingDef>.AllDefs.RandomElementByWeight(t =>
                                DebugThingPlaceHelper.IsDebugSpawnable(t, false) && t.HasComp(typeof(CompLootAffixableThing)) ? 1 : 0
                            );
                            DebugSpawnWithAffixes(thingDef, UI.MouseCell(), affixPoints, ttlAffixes);
                        }));
                    }
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(options2));
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        public static void DebugSpawnWithAffixes(ThingDef def, IntVec3 c, float affixPoints = 0, int ttlAffixes = 0) {
            ThingDef stuff = GenStuff.RandomStuffFor(def);
            Thing thing = ThingMaker.MakeThing(def, stuff);

            thing.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityRandomEqualChance(), ArtGenerationContext.Colony);
            if (thing.def.Minifiable) thing = thing.MakeMinified();
            thing.TryGetComp<CompLootAffixableThing>()?.InitializeAffixes(affixPoints, ttlAffixes);

            GenPlace.TryPlaceThing(thing, c, Find.CurrentMap, ThingPlaceMode.Near);
        }

        [DebugAction("RimLoot", "Add affix...", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddAffix() {
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(Options_AddAffix()));
        }

        private static List<DebugMenuOption> Options_AddAffix() {
            List<DebugMenuOption> debugMenuOptionList = new List<DebugMenuOption>();
            foreach (LootAffixDef affixDef in DefDatabase<LootAffixDef>.AllDefs.OrderBy(lad => lad.affixCost)) {
                LootAffixDef localDef = affixDef;
                debugMenuOptionList.Add(new DebugMenuOption(localDef.defName, DebugMenuOptionMode.Tool, () => {
                    CompLootAffixableThing comp = Find.CurrentMap.thingGrid.
                        ThingsAt(UI.MouseCell()).
                        Where (t => t is ThingWithComps).Cast<ThingWithComps>().
                        Select(twc => twc.TryGetComp<CompLootAffixableThing>()).
                        Where (c => c is CompLootAffixableThing).
                        FirstOrDefault()
                    ;

                    var lads = comp.AllAffixDefs;
                    if (lads.Contains(localDef) || lads.Count >= 4) return;
                    lads.Add(localDef);
                    comp.PostAffixCleanup();
                }));
            }
            return debugMenuOptionList;
        }

        [DebugAction("RimLoot", "Remove affix...", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RemoveAffix() {
            foreach (Thing thing in UI.MouseCell().GetThingList(Find.CurrentMap).Where(t => t is ThingWithComps).ToList()) {
                var comp = ((ThingWithComps)thing).TryGetComp<CompLootAffixableThing>();
                if (comp == null) continue;
                    
                Find.WindowStack.Add(new Dialog_DebugOptionListLister(Options_RemoveAffix(comp)));
                break;
            }
        }

        private static List<DebugMenuOption> Options_RemoveAffix(CompLootAffixableThing comp) {
            List<DebugMenuOption> debugMenuOptionList = new List<DebugMenuOption>();
            foreach (LootAffixDef affixDef in comp.AllAffixDefs) {
                LootAffixDef localDef = affixDef;
                debugMenuOptionList.Add(new DebugMenuOption(comp.AllAffixesByAffixDefs[localDef], DebugMenuOptionMode.Action, () => {
                    int i = comp.AllAffixDefs.IndexOf(localDef);
                    comp.AllAffixDefs.RemoveAt(i);
                    comp.PostAffixCleanup();
                }));
            }
            return debugMenuOptionList;
        }

        [DebugAction("RimLoot", "Remove all affixes", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RemoveAllAffix() {
            foreach (Thing thing in UI.MouseCell().GetThingList(Find.CurrentMap).Where(t => t is ThingWithComps).ToList()) {
                var comp = ((ThingWithComps)thing).TryGetComp<CompLootAffixableThing>();
                if (comp == null) continue;

                comp.AllAffixDefs.Clear();
                comp.PostAffixCleanup();
            }
        }

    }
}
