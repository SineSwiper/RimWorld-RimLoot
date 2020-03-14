using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    class CompLootAffixableThing : ThingComp {
        internal string fullStuffLabel = null;

        private List<LootAffixDef> affixes    = new List<LootAffixDef>();
        private List<Rule>         affixRules = new List<Rule>();

        // Cached values
        private List<string>                     affixStringsCached;
        private Dictionary<string, LootAffixDef> affixDefDictCached;
        private Dictionary<LootAffixDef, string> affixStringsDictCached;

        private HashSet<LootAffixModifier>       modifiersCached;

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

        private void MakeAffixCaches() {
            affixStringsCached = affixRules.Select(r => r.Generate()).ToList();

            affixDefDictCached     = new Dictionary<string, LootAffixDef> {};
            affixStringsDictCached = new Dictionary<LootAffixDef, string> {};
            modifiersCached        = new HashSet<LootAffixModifier>       {};
            for (int i = 0; i < affixes.Count; i++) {
                affixDefDictCached    [ AffixStrings[i] ] = affixes[i];
                affixStringsDictCached[ affixes[i] ] = AffixStrings[i];
                modifiersCached.AddRange(affixes[i].modifiers);
            }
        }

        private void ClearAffixCaches() {
            affixStringsCached?.Clear();
            affixDefDictCached?.Clear();
            affixStringsDictCached?.Clear();
            modifiersCached?.Clear();
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

            // Remaining affixes: rebalance the weight priorities to better reflect the current point total
            for (int curAffixes = 2; curAffixes <= 4; curAffixes++) {
                if (curAffixes > ttlAffixes) return;
                int remainAffixes = ttlAffixes - curAffixes + 1;

                float paRatio = remainAffixes > 1 ? affixPoints / remainAffixes : affixPoints * 0.70f;
                
                filteredAffixDefs     = baseAffixDefs.FindAll(lad => lad.affixCost <= affixPoints);
                LootAffixDef newAffix = filteredAffixDefs.RandomElementByWeightWithFallback(lad =>
                    // eg: p=2 for cost right at the average, p=1/3 for one that's 3 away from the average, including negatives
                    1 / Mathf.Max(Mathf.Abs(lad.affixCost - paRatio), 0.5f)
                );
                if (newAffix == null) return;

                affixes.Add(newAffix);
                affixPoints -= newAffix.affixCost;
                baseAffixDefs = baseAffixDefs.FindAll(lad => lad.groupName != firstAffix.groupName);
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

            Log.Message("Affix Points: " + string.Join("/", wealth, qc, (int)qc, (float)qc));

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
            GrammarRequest request = new GrammarRequest();
            request.Includes.Add( DefDatabase<RulePackDef>.GetNamed("RimLoot_LootAffixNamer") );

            var namerConfigDef = DefDatabase<LootAffixNamerConfigDef>.GetNamed("RimLoot_LootAffixNamer_Config");

            // Word class counter set-up
            Dictionary<string, int> maxWordClasses = namerConfigDef.maxWordClasses;
            Dictionary<string, int> curWordClasses = new Dictionary<string, int> ();
            for (int i = 0; i < 5; i++) {
                curWordClasses = maxWordClasses.ToDictionary( k => k.Key, v => 0 );  // shallow clone to k=0

                // Add in the affixes
                foreach (LootAffixDef affix in affixes) {
                    foreach (Rule rule in affix.PickAffixRulesForLabeling(curWordClasses, maxWordClasses)) {
                        Log.Message("affix rule: " + rule);
                        if (rule.keyword.StartsWith("AFFIX_")) affixRules.Add(rule);
                        request.Rules.Add(rule);
                        if (rule.keyword.StartsWith("AFFIXPROP_")) request.Constants.Add(rule.keyword, rule.Generate());
                    }
                }

                // Double-check we didn't hit one of those disallowed combinations
                if (namerConfigDef.IsWordClassComboAllowed(affixRules)) break;
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

            return preExtra + fullStuffLabel + postExtra;
        }

        // FIXME: Use these for cursed items?
        public override string CompInspectStringExtra() {
            // FIXME
            return null;
        }

        public override string GetDescriptionPart() {
            // FIXME
            return null;
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats() {
            StatCategoryDef category =
                parent.def.IsApparel ? StatCategoryDefOf.Apparel :
                parent.def.IsWeapon  ? StatCategoryDefOf.Weapon  :
                StatCategoryDefOf.BasicsImportant
            ;

            // FIXME: Add description to beginning of report
            string reportText = "";
            var affixDict = AllAffixDefsByAffixes;
            foreach (string affixKey in AffixStrings) {
                LootAffixDef affix = affixDict[affixKey];
                reportText += affix.FullStatsReport(affixKey) + "\n";
            }

            yield return new StatDrawEntry(
                category:    category,
                label:       "RimLoot_LootAffixModifiers".Translate(),
                valueString: GenText.ToCommaList(AffixStrings, false),
                reportText:  reportText,
                hyperlinks:  affixes.SelectMany(lad => lad.GetModifierHyperlinks(parent)),
                displayPriorityWithinCategory: 1
            );
        }

        // FIXME: Change MarketPrice
    }
}
