using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

                affixWords = new List<string> ();
                foreach ( Rule rule in affixRulePack.Rules.Where(r => r.keyword.StartsWith("AFFIX_")) ) {
                    affixWords.Add( rule.Generate() );
                }
                return affixWords;
            }
        }

        public string FullAffixLabel {
            get {
                return string.Join(" / ", AffixWords);
            }
        }

        public override void ResolveReferences() {
            base.ResolveReferences();
            foreach (LootAffixModifier modifier in modifiers) {
                modifier.ResolveReferences(this);
            }
        }

        public override void PostLoad() {
            base.PostLoad();
            foreach (LootAffixModifier modifier in modifiers) {
                modifier.PostLoadSpecial(this);
            }
        }

        // FIXME: Deep checks into affixRulePack
        public override IEnumerable<string> ConfigErrors () {
            foreach (string configError in base.ConfigErrors())
                yield return configError;
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
                    "Did not find an appropriate affix rule for " + defName + ".  Either ran out of word classes or affixRulePack is bugged."
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

        public string FullStatsReport (string preLabel) {
            if (preLabel == null) preLabel = FullAffixLabel;  // fallback

            string str = preLabel + ":\n";                
            foreach (LootAffixModifier modifier in modifiers) {
                str += "    " + modifier.ModifierChangeLabel + "\n";
            }
            return str;
        }

        public IEnumerable<Dialog_InfoCard.Hyperlink> GetModifierHyperlinks (ThingWithComps parentThing) {
            foreach (LootAffixModifier modifier in modifiers) {
                foreach (var hyperlink in modifier.GetHyperlinks(parentThing, this)) {
                    yield return hyperlink;
                }
            }
        }

        // TODO: Add its own Dialog_InfoCard -> Def
        /*
        public override IEnumerable<Dialog_InfoCard.Hyperlink> GetHyperlinks (ThingWithComps parentThing, LootAffixDef parentDef) {
            // FIXME: statIndex?  Does StatDef work here?
            yield return new Dialog_InfoCard.Hyperlink(parentDef);
        }
        */

    }
}
