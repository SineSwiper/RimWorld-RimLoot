﻿using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    public class CompLootAffixableThing : ThingComp {
        internal string fullStuffLabel = null;

        private List<LootAffixDef> affixes    = new List<LootAffixDef>();
        private List<Rule>         affixRules = new List<Rule>();

        // Cached values
        private List<string>                     affixStringsCached;
        private Dictionary<string, LootAffixDef> affixDefDictCached;
        private Dictionary<LootAffixDef, string> affixStringsDictCached;

        private HashSet<LootAffixModifier>       modifiersCached;

        private float?                           ttlAffixPoints;

        public List<LootAffixDef> AllAffixDefs {
            get { return affixes; }
        }

        public List<string> AffixStrings {
            get {
                if (affixStringsCached != null) return affixStringsCached;
                MakeAffixCaches();
                return affixStringsCached;
            }
        }

        public Dictionary<string, LootAffixDef> AllAffixDefsByAffixes {
            get {
                if (affixDefDictCached != null) return affixDefDictCached;
                MakeAffixCaches();
                return affixDefDictCached;
            }
        }

        public Dictionary<LootAffixDef, string> AllAffixesByAffixDefs {
            get {
                if (affixStringsDictCached != null) return affixStringsDictCached;
                MakeAffixCaches();
                return affixStringsDictCached;
            }
        }

        public HashSet<LootAffixModifier> AllModifiers {
            get {
                if (modifiersCached != null) return modifiersCached;
                MakeAffixCaches();
                return modifiersCached;
            }
        }

        public float TotalAffixPoints {
            get {
                if (ttlAffixPoints != null) return (float)ttlAffixPoints;
                MakeAffixCaches();
                return (float)ttlAffixPoints;
            }
        }

        private void MakeAffixCaches() {
            affixStringsCached = affixRules.Select(r => r.Generate()).ToList();

            affixDefDictCached     = new Dictionary<string, LootAffixDef> {};
            affixStringsDictCached = new Dictionary<LootAffixDef, string> {};
            modifiersCached        = new HashSet<LootAffixModifier>       {};
            ttlAffixPoints         = 0;

            for (int i = 0; i < affixes.Count; i++) {
                affixDefDictCached    [ AffixStrings[i] ] = affixes[i];
                affixStringsDictCached[ affixes[i] ] = AffixStrings[i];
                modifiersCached.AddRange(affixes[i].modifiers);
                ttlAffixPoints += affixes[i].affixCost;
            }
        }

        private void ClearAffixCaches() {
            affixStringsCached?.Clear();
            affixDefDictCached?.Clear();
            affixStringsDictCached?.Clear();
            modifiersCached?.Clear();
            ttlAffixPoints = null;
        }

        public override void PostExposeData() {
            base.PostExposeData();
            Scribe_Values.Look(ref fullStuffLabel, "fullStuffLabel", null, false);
            Scribe_Collections.Look(ref affixes, false, "affixes", LookMode.Def, (object) this);

            if      (Scribe.mode == LoadSaveMode.Saving) {
                List<string> affixRuleStrings = affixRules.Select(r => r.ToString()).ToList();
                Scribe_Collections.Look(ref affixRuleStrings, false, "affixRules", LookMode.Value, (object) this);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars) {
                List<string> affixRuleStrings = new List<string>();
                Scribe_Collections.Look(ref affixRuleStrings, false, "affixRules", LookMode.Value, (object) this);
                affixRules.Clear();
                affixRules.AddRange( affixRuleStrings.Select(rs => new Rule_String(rs)) );
            }
            
        }

        public override void ReceiveCompSignal(string signal) {
            if (signal == "SetQuality") InitializeAffixes();
        }
    
        public override void CompTick() {
            // FIXME
        }

        public override void CompTickRare() {
            // FIXME
        }

        public void InitializeAffixes(float affixPoints = 0, int ttlAffixes = 0) {  // options for debug only
            affixes.Clear();
            AddNewAffixes(affixPoints, ttlAffixes);
            PostAffixCleanup();
        }

        public void PostAffixCleanup() {
            ClearAffixCaches();
            affixRules.Clear();

            fullStuffLabel = null;
            string name = parent.LabelNoCount;
            name = TransformLabel(name);

            foreach (LootAffixDef affix in affixes) {
                affix.PostApplyAffix(parent);
            }
        }

        // FIXME: Test the ratio system
        public void AddNewAffixes(float affixPoints = 0, int ttlAffixes = 0) {  // options for debug only
            affixes.Clear();
            if (affixPoints == 0) affixPoints = CalculateTotalLootAffixPoints();

            if (ttlAffixes == 0) {
                for (int i = 1; i <= 4; i++) {
                    // 25% chance for each affix (compounded)
                    if (0.25f < Random.Range(0.0f, 1.0f)) break;
                    ttlAffixes = i;
                }
            }

            Log.Message("Affix/Points: " + string.Join("/", ttlAffixes, affixPoints));

            if (ttlAffixes == 0) return;

            // Baseline of affixes that can be used (since affixPoints could change upward or downward)
            List<LootAffixDef> baseAffixDefs = 
                DefDatabase<LootAffixDef>.AllDefsListForReading.
                FindAll( lad => lad.CanBeAppliedToThing(parent) )
            ;
            List<LootAffixDef> filteredAffixDefs = baseAffixDefs.FindAll( lad => lad.affixCost <= affixPoints );

            // First affix: Random pool of anything (within the cost)
            LootAffixDef firstAffix = filteredAffixDefs.RandomElementWithFallback();
            if (firstAffix == null) return;

            affixes.Add(firstAffix);
            affixPoints -= firstAffix.affixCost;
            baseAffixDefs = baseAffixDefs.FindAll(lad => lad.groupName != firstAffix.groupName);
            Log.Message("1 Affix: " + string.Join("/", firstAffix.defName, firstAffix.affixCost));

            // Remaining affixes: rebalance the weight priorities to better reflect the current point total
            for (int curAffixes = 2; curAffixes <= 4; curAffixes++) {
                if (curAffixes > ttlAffixes) return;
                int remainAffixes = ttlAffixes - curAffixes + 1;

                // FIXME: Test
                //float paRatio = remainAffixes > 1 ? affixPoints / remainAffixes : affixPoints * 0.70f;
                float paRatio = affixPoints / remainAffixes;
                paRatio = Mathf.Clamp(paRatio, 1, 6);
                
                filteredAffixDefs     = baseAffixDefs.FindAll(lad => lad.affixCost <= affixPoints);
                LootAffixDef newAffix = filteredAffixDefs.RandomElementByWeightWithFallback(lad =>
                    // 6 / max(abs(ac-pa)³, 0.25)
                    // eg: p=24 for cost right at the average (with a ±0.6 swing)
                    //     p= 1 for one that's 1.78 away from the average, including negatives
                    6 / Mathf.Max(
                        Mathf.Pow(Mathf.Abs(lad.affixCost - paRatio), 3),
                    0.25f)
                );
                if (newAffix == null) return;

                affixes.Add(newAffix);
                affixPoints -= newAffix.affixCost;
                baseAffixDefs = baseAffixDefs.FindAll(lad => lad.groupName != newAffix.groupName);
                Log.Message(curAffixes + " Affix: " + string.Join("/", newAffix.defName, newAffix.affixCost));
            }
        }

        public int CalculateTotalLootAffixPoints() {
            float ptsF = 0f;

            // Up to 4 points based on total wealth (1M max)
            float wealth = 0f;
            if (Current.ProgramState == ProgramState.Playing) {  // don't bother while initializing
                if      (parent.Map      != null && parent.Map.wealthWatcher      != null) wealth = parent.Map.wealthWatcher.WealthTotal;
                else if (Find.CurrentMap != null && Find.CurrentMap.wealthWatcher != null) wealth = Find.CurrentMap.wealthWatcher.WealthTotal;
                else if (Find.World      != null)                                          wealth = Find.World.PlayerWealthForStoryteller;
            }

            ptsF += Mathf.Min(wealth, 1_000_000) / 250_000;

            // Up to 8 points based on item quality
            QualityCategory qc;
            parent.TryGetQuality(out qc);

            // Normal = 1, Good = 2, Excellent = 4, Masterwork = 6, Legendary = 8
            ptsF += Mathf.Pow((int)qc, 2f) / 4.5f;

            return Mathf.RoundToInt(ptsF);
        }

        public override string TransformLabel(string label) {
            // Short-circuit: No affixes
            if (affixes.Count == 0) return label;

            // Short-circuit: Already calculated the full label and no replacement required
            string stuffLabel = GenLabel.ThingLabel(parent.def, parent.Stuff, 1);
            if (fullStuffLabel != null && stuffLabel == label) return fullStuffLabel;

            // Short-circuit: Still have the calculated full label
            string preExtra  = "";
            string postExtra = "";
            int pos = label.IndexOf(stuffLabel);
            if (pos >= 0) {
                preExtra  = label.Substring(0, pos);
                postExtra = label.Substring(pos + stuffLabel.Length);
            }

            if (fullStuffLabel != null) return preExtra + fullStuffLabel + postExtra;
            
            // Need the calculate the label then...
            var namerDef = DefDatabase<LootAffixNamerRulePackDef>.GetNamed("RimLoot_LootAffixNamer");
            GrammarRequest request = new GrammarRequest();
            request.Includes.Add(namerDef);

            // Word class counter set-up
            Dictionary<string, int> maxWordClasses = namerDef.maxWordClasses;
            Dictionary<string, int> curWordClasses = new Dictionary<string, int> ();
            for (int i = 0; i < 5; i++) {
                curWordClasses = maxWordClasses.ToDictionary( k => k.Key, v => 0 );  // shallow clone to k=0

                // Add in the affixes
                foreach (LootAffixDef affix in affixes) {
                    foreach (Rule rule in affix.PickAffixRulesForLabeling(curWordClasses, maxWordClasses)) {
                        if (rule.keyword.StartsWith("AFFIX_")) affixRules.Add(rule);
                        request.Rules.Add(rule);
                        if (rule.keyword.StartsWith("AFFIXPROP_")) request.Constants.Add(rule.keyword, rule.Generate());
                    }
                }

                // Double-check we didn't hit one of those disallowed combinations
                if (namerDef.IsWordClassComboAllowed(affixRules)) break;
                else {
                    affixRules.Clear();
                    continue;
                }
            }

            if (affixRules.Count != affixes.Count) {
                Log.Error("Chosen affix words for " + parent + " don't match the number of affixes:\n" + string.Join(
                    "\nvs.\n", 
                    string.Join(" | ", affixRules), 
                    string.Join(", ", affixes)
                ));
            }

            // Add a few types of labels for maximum language flexibility
            request.Rules.Add(new Rule_String( "STUFF_label",      parent.Stuff != null ? parent.Stuff.LabelAsStuff : ""));
            request.Rules.Add(new Rule_String( "THING_defLabel",   parent.def.label));
            request.Rules.Add(new Rule_String( "THING_stuffLabel", stuffLabel));

            string rootKeyword = "r_affix" + affixes.Count;
            fullStuffLabel = NameGenerator.GenerateName(request, null, false, rootKeyword, rootKeyword);

            Log.Message("Chosen affix words for " + parent + ":\n" + string.Join(
                "\nand\n", 
                string.Join(" | ", affixRules), 
                string.Join(", ", affixes)
            ));

            // It's possible we might end up hitting this later than we expected, and run into affixes/word
            // desyncs, so clear the cache, just in case.
            ClearAffixCaches();

            return preExtra + fullStuffLabel + postExtra;
        }

        // FIXME: Use these for cursed items?
        public override string CompInspectStringExtra() {
            if (affixes.Count > 0) {
                return
                    "RimLoot_Affixes".Translate() + ": " +
                    GenText.ToCommaList(
                        AllAffixDefsByAffixes.Select( kv => kv.Value.LabelWithStyle(kv.Key) ), false
                    )
                ;
            }
            return null;
        }

        public override string GetDescriptionPart() {
            // FIXME
            return null;
        }

        // FIXME: Icons on the affix hyperlinks
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats() {
            StatCategoryDef category =
                parent.def.IsApparel ? StatCategoryDefOf.Apparel :
                parent.def.IsWeapon  ? StatCategoryDefOf.Weapon  :
                StatCategoryDefOf.BasicsImportant
            ;

            string reportText = "RimLoot_LootAffixDescription".Translate() + "\n\n";
            var affixDict = AllAffixDefsByAffixes;
            foreach (string affixKey in AffixStrings) {
                LootAffixDef affix = affixDict[affixKey];
                reportText += affix.FullStatsReport(affixKey) + "\n";
            }

            if (Prefs.DevMode) {
                reportText += "[DEV] Affix Rules:\n    " + string.Join("\n    ", affixRules) + "\n\n";
                reportText += "[DEV] Total Points: " + affixes.Select( lad => lad.affixCost ).Sum() + 
                    "\n    " +
                    string.Join("\n    ", affixes.Select( lad => AllAffixesByAffixDefs[lad] + ": " + lad.affixCost )) + 
                    "\n\n"
                ;
            }

            yield return new StatDrawEntry(
                category:    category,
                label:       "RimLoot_LootAffixModifiers".Translate(),
                valueString: GenText.ToCommaList(
                    AllAffixDefsByAffixes.Select( kv => kv.Value.LabelWithStyle(kv.Key) ), false
                ),
                reportText:  reportText,
                hyperlinks:  affixes.SelectMany(lad => lad.GetHyperlinks(parent)),
                displayPriorityWithinCategory: 1
            );

            // Add any additional entries from the defs or modifiers
            foreach (string affixKey in AffixStrings) {
                LootAffixDef affix = affixDict[affixKey];
                foreach (var statDrawEntry in affix.SpecialDisplayStatsForThing(parent, affixKey)) {
                    yield return statDrawEntry;
                }
            }
        }
    }
}
