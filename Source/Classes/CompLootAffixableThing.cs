using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    class CompLootAffixableThing : ThingComp {
        private string fullStuffLabel = null;

        private List<LootAffixDef> affixes    = new List<LootAffixDef>();
        private List<Rule>         affixRules = new List<Rule>();

        private List<string>                     affixStringsCached;
        private Dictionary<string, LootAffixDef> affixDefDictCached;
        private Dictionary<LootAffixDef, string> affixStringsDictCached;

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

        private void MakeAffixCaches() {
            affixStringsCached = affixRules.Select(r => r.Generate()).ToList();

            affixDefDictCached     = new Dictionary<string, LootAffixDef> {};
            affixStringsDictCached = new Dictionary<LootAffixDef, string> {};
            for (int i = 0; i < affixes.Count; i++) {
                affixDefDictCached    [ AffixStrings[i] ] = affixes[i];
                affixStringsDictCached[ affixes[i] ] = AffixStrings[i];
            }
        }

        public override void PostPostMake() {
            // DEBUG - Fix with real system later
            if (0.50f < Random.Range(0.0f, 1.0f)) affixes.Add( DefDatabase<LootAffixDef>.GetNamed("Tough") );

            // NOT DEBUG: Keep this
            foreach (LootAffixDef affix in affixes) {
                affix.PostApplyAffix(parent);
            }
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

        public override void CompTick() {
            // FIXME
        }

        public override void CompTickRare() {
            // FIXME
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
