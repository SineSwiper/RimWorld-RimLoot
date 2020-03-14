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

        [DebugAction("RimLoot", "Try place random affixable...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TryPlaceRandomAffixableWithOptions() {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int i = 1; i <= 4; i++) {
                options.Add(new FloatMenuOption(i + " Affix", () => {
                    List<FloatMenuOption> options2 = new List<FloatMenuOption>();
                    for (int j = 0; j <= 12; j++) {
                        options2.Add(new FloatMenuOption(j + " points", () => {
                            ThingDef thingDef = DefDatabase<ThingDef>.AllDefs.RandomElementByWeight(t =>
                                DebugThingPlaceHelper.IsDebugSpawnable(t, false) && t.HasComp(typeof(CompLootAffixableThing)) ? 1 : 0
                            );
                            IntVec3 pos = UI.MouseCell();
                            DebugThingPlaceHelper.DebugSpawn(thingDef, pos);

                            ThingWithComps twc = Find.CurrentMap.thingGrid.ThingsAt(pos).Where(t => t is ThingWithComps).Cast<ThingWithComps>().FirstOrDefault();
                            if (twc == null) return;
                            var comp = twc.TryGetComp<CompLootAffixableThing>();
                            if (comp == null) return;
                            comp.InitializeAffixes(j, i);
                        }));
                    }
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("RimLoot", "Add affix...", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddAffix() {
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(Options_AddAffix()));
        }

        private static List<DebugMenuOption> Options_AddAffix() {
            List<DebugMenuOption> debugMenuOptionList = new List<DebugMenuOption>();
            foreach (LootAffixDef affixDef in DefDatabase<LootAffixDef>.AllDefs.OrderBy(lad => lad.affixCost)) {
                LootAffixDef localDef = affixDef;
                debugMenuOptionList.Add(new DebugMenuOption(localDef.defName, DebugMenuOptionMode.Tool, (Action) (() => {
                    ThingWithComps twc = Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).Where(t => t is ThingWithComps).Cast<ThingWithComps>().FirstOrDefault();
                    if (twc == null) return;
                    var comp = twc.TryGetComp<CompLootAffixableThing>();
                    if (comp == null) return;

                    var lads = comp.AllAffixDefs;
                    if (lads.Contains(localDef) || lads.Count >= 4) return;
                    lads.Add(localDef);
                    comp.PostAffixCleanup();
                })));
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
                debugMenuOptionList.Add(new DebugMenuOption(comp.AllAffixesByAffixDefs[localDef], DebugMenuOptionMode.Tool, (Action) (() => {
                    int i = comp.AllAffixDefs.IndexOf(localDef);
                    comp.AllAffixDefs.RemoveAt(i);
                    comp.PostAffixCleanup();
                })));
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
