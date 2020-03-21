﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    public class LootAffixDef : Def {
        public string groupName;
        public List<LootAffixModifier> modifiers = new List<LootAffixModifier> ();
        public float affixCost = 1;
        public RulePack affixRulePack;

        private List<string> affixWords;

        public List<string> AffixWords {
            get {
                if (affixWords != null) return affixWords;

                var namerDef = DefDatabase<LootAffixNamerRulePackDef>.GetNamed("RimLoot_LootAffixNamer");
                var maxWordClasses = namerDef.maxWordClasses;

                affixWords = new List<string> ();
                foreach ( Rule rule in affixRulePack.Rules.Where(r =>
                    maxWordClasses.Keys.Any(k => r.keyword == k)
                ) ) {
                    affixWords.Add( rule.Generate() );
                }
                return affixWords;
            }
        }

        public string FullAffixLabel {
            get {
                return string.Join(" / ", AffixWords.Distinct());
            }
        }

        // FIXME: Make this configurable, especially for the color-blind
        public string LabelWithStyle () { return LabelWithStyle(null); }

        public string LabelWithStyle (string preLabel) {
            if (preLabel == null) preLabel = FullAffixLabel;  // fallback

            string styledLabel = preLabel;
            string color = null;
            if      (affixCost >   4) color = "lime";
            else if (affixCost >=  1) color = "cyan";
            else if (affixCost <= -1) color = "yellow";
            else if (affixCost <  -4) color = "red";
            if (color != null) styledLabel = string.Format("<color={0}>{1}</color>", color, styledLabel);

            return styledLabel;
        }

        public override void ResolveReferences() {
            base.ResolveReferences();
            foreach (LootAffixModifier modifier in modifiers) {
                modifier.ResolveReferences(this);
            }

            description =
                "RimLoot_LootAffixDescription".Translate() + "\n\n" +
                FullStatsReport().Trim()
            ;
        }

        public override void PostLoad() {
            base.PostLoad();
            foreach (LootAffixModifier modifier in modifiers) {
                modifier.PostLoadSpecial(this);
            }
        }

        // FIXME: Check all affix words for duplicates
        Regex rgxDigit = new Regex(@"\d");
        public override IEnumerable<string> ConfigErrors () {
            foreach (string configError in base.ConfigErrors())
                yield return configError;

            if (groupName == null)    yield return "No groupName defined";
            if (modifiers.Count == 0) yield return "No modifiers defined";
            if (Mathf.Clamp(affixCost, -6f, 6f) != affixCost)
                yield return "affixCost is out-of-bounds; should be between -6 and 6";

            // affixRulePack checks
            var namerDef = DefDatabase<LootAffixNamerRulePackDef>.GetNamed("RimLoot_LootAffixNamer");
            if (namerDef == null) {
                Log.ErrorOnce("No LootAffixNamerConfigDef called 'RimLoot_LootAffixNamer_Config' to check!", 47764624);
                yield break;
            }
            Dictionary<string, int> maxWordClasses = namerDef.maxWordClasses;
            List<Rule> fullRules = affixRulePack.Rules;

            // Find a word class rule from the list of affixes
            int wordClassRuleCount = fullRules.Where( r => maxWordClasses.ContainsKey(r.keyword) ).Count();
            if      (wordClassRuleCount == 1) yield return "affixRulePack has only 1 rule that matches a word class";
            else if (wordClassRuleCount == 0) yield return "affixRulePack has no rules that matches a word class";
            foreach ( Rule rule in fullRules ) {
                if (rule.keyword.StartsWith("AFFIX")) yield return "affixRulePack should not have an AFFIX prefix: " + rule.keyword;
                if (rgxDigit.IsMatch(rule.keyword))   yield return "affixRulePack should not have any digits: " + rule.keyword;
            }
            
            // Recurse through the modifiers
            foreach (LootAffixModifier modifier in modifiers) {
                foreach (string configError in modifier.ConfigErrors(this)) yield return modifier.ModifierChangeStat + ": " + configError;
            }
        }


        public bool CanBeAppliedToThing (ThingWithComps thing) {
            // If any of the modifiers can apply it, then it passes
            foreach (LootAffixModifier modifier in modifiers) {
                if (modifier.CanBeAppliedToThing(thing)) return true;
            }

            return false;
        }

        // FIXME: CanBeAppliedToThing (ThingDef thing) ?

        public void PostApplyAffix (ThingWithComps parentThing) {
            foreach (LootAffixModifier modifier in modifiers) {
                modifier.PostApplyAffix(parentThing, this);
            }
        }

        public IEnumerable<Rule> PickAffixRulesForLabeling (Dictionary<string, int> curWordClasses, Dictionary<string, int> maxWordClasses) {
            List<Rule> fullRules  = affixRulePack.Rules;

            // Find a word class rule from the list of affixes
            Rule pickedWordClassRule = fullRules.Where( r =>
                maxWordClasses.ContainsKey(r.keyword) &&
                curWordClasses[r.keyword] < maxWordClasses[r.keyword]
            ).RandomElementWithFallback();

            if (pickedWordClassRule == null) {
                Log.Error(
                    "Did not find an appropriate affix rule for " + defName + ".  Either ran out of word classes or affixRulePack is bugged.\n" +
                    "Maxes: " + string.Join(", ", maxWordClasses.Select(kv => kv.Key + "=" + kv.Value)) + "\n" +
                    "affixRulePack: " + string.Join(" | ", fullRules) + "\n"
                );
                yield break;
            }

            // Increment the word class count 
            string pickedWordClass = pickedWordClassRule.keyword;
            curWordClasses[pickedWordClass]++;
            int suffixInt = curWordClasses[pickedWordClass];

            // Add rule and increment the word class count 
            pickedWordClassRule = pickedWordClassRule.DeepCopy();
            pickedWordClassRule.keyword = "AFFIX_" + pickedWordClass + suffixInt.ToString();
            yield return pickedWordClassRule;

            // Add any extra affix property rules
            foreach ( Rule rule in fullRules.Where(r => r.keyword.StartsWith(pickedWordClass) && r.keyword != pickedWordClass ) ) {
                Rule ruleCopy = rule.DeepCopy();
                string prefix = "AFFIXPROP_" + pickedWordClass + suffixInt.ToString();  // eg: AFFIXPROP_wordclass2_some_prop
                ruleCopy.keyword = prefix + ruleCopy.keyword.Substring(pickedWordClass.Length);
                yield return ruleCopy;
            }

            // If it doesn't fit either of those two prefixes, just add it in verbatim
            foreach ( Rule rule in fullRules.Where(r =>
                !maxWordClasses.Keys.Any(k => r.keyword.StartsWith(k))
            ) ) yield return rule;
        }

        public string FullStatsReport () { return FullStatsReport(null); }

        public string FullStatsReport (string preLabel) {
            // Specify where the effect is applied
            string str = (
                modifiers.All(lam => lam.appliesTo == ModifierTarget.Pawn) ?
                    (string)"RimLoot_AffixWhileEquipped".Translate( LabelWithStyle(preLabel) ) :
                    LabelWithStyle(preLabel)
            ) + ":\n";

            foreach (LootAffixModifier modifier in modifiers) {
                str += "    " + modifier.ModifierChangeLabel + "\n";
            }
            return str;
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req) {
            foreach (StatDrawEntry statDrawEntry in base.SpecialDisplayStats(req))
                yield return statDrawEntry;

            // Get the grouped affixes in positive-ascending affixCost order, and then negative-descending order
            IEnumerable<LootAffixDef> affixDefIEnum = 
                DefDatabase<LootAffixDef>.AllDefs.
                Where  (lad => lad.groupName == groupName)
            ;
            List<LootAffixDef> groupedAffixDefs =
                affixDefIEnum.
                Where  (lad => lad.affixCost >= 0).
                OrderBy(lad => lad.affixCost).
                ToList()
            ;
            groupedAffixDefs.AddRange( 
                affixDefIEnum.
                Where  (lad => lad.affixCost <= 0).
                OrderByDescending(lad => lad.affixCost)
            );

            string reportText = "";
            foreach (LootAffixDef affixDef in groupedAffixDefs) {
                reportText += affixDef.FullStatsReport() + "\n";
            }

            yield return new StatDrawEntry(
                category:    StatCategoryDefOf.BasicsImportant,
                label:       "RimLoot_AffixesInThisGroup".Translate(),
                valueString: GenText.ToCommaList(groupedAffixDefs.Select(lad => lad.LabelWithStyle(lad.defName)), false),  // FIXME: Change to lad.label
                reportText:  reportText,
                displayPriorityWithinCategory: 1
            );
        }

        public IEnumerable<StatDrawEntry> SpecialDisplayStatsForThing(ThingWithComps parentThing, string preLabel) {
            foreach (LootAffixModifier modifier in modifiers) {
                foreach (var statDrawEntry in modifier.SpecialDisplayStatsForThing(parentThing, preLabel)) {
                    yield return statDrawEntry;
                }
            }
        }

        public IEnumerable<Dialog_InfoCard.Hyperlink> GetHyperlinks (ThingWithComps parentThing) {
            yield return new Dialog_InfoCard.Hyperlink(this);

            // FIXME: Will we ever use this?
            foreach (LootAffixModifier modifier in modifiers) {
                foreach (var hyperlink in modifier.GetHyperlinks(parentThing, this)) {
                    yield return hyperlink;
                }
            }
        }
    }
}
