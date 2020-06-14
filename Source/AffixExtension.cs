using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    static public class AffixExtension {
        static public void InitializeAffixes (this CompLootAffixableThing comp, float affixPoints = 0, int ttlAffixes = 0) {  // options for debug only
            comp.affixes.Clear();
            comp.AddNewAffixes(affixPoints, ttlAffixes);
            comp.PostAffixCleanup();
        }

        static public void PostAffixCleanup (this CompLootAffixableThing comp, bool fixLabel = true) {
            ThingWithComps thing = comp.parent;

            comp.ClearAffixCaches();

            if (fixLabel) {
                comp.affixRules.Clear();
                comp.fullStuffLabel = null;
                string name = thing.LabelNoCount;
                name = comp.TransformLabel(name);
            }

            thing.def.SpecialDisplayStats(StatRequest.For(thing));

            foreach (LootAffixDef affix in comp.affixes) {
                affix.PostApplyAffix(thing);
            }
        }

        static public void AddNewAffixes (this CompLootAffixableThing comp, float affixPoints = 0, int ttlAffixes = 0) {  // options for debug only
            List<LootAffixDef> affixes = comp.affixes;
            ThingWithComps     thing   = comp.parent;

            affixes.Clear();
            if (affixPoints == 0) affixPoints = CalculateTotalLootAffixPoints(thing);

            if (ttlAffixes == 0) {
                for (int i = 1; i <= 4; i++) {
                    // FIXME: Add config sliders for percentages here
                    // 25% chance for each affix (compounded)
                    if (0.25f < Random.Range(0.0f, 1.0f)) break;
                    ttlAffixes = i;
                }
            }

            if (ttlAffixes == 0) return;

            // Baseline of affixes that can be used (since affixPoints could change upward or downward)
            List<LootAffixDef> baseAffixDefs = 
                DefDatabase<LootAffixDef>.AllDefsListForReading.
                FindAll( lad => lad.CanBeAppliedToThing(thing) )
            ;

            // Affix picking loop
            for (int curAffixes = affixes.Count + 1; curAffixes <= 4; curAffixes++) {
                LootAffixDef newAffix = PickAffix(thing, baseAffixDefs, curAffixes, ttlAffixes, affixPoints);
                if (newAffix == null) return;

                affixes.Add(newAffix);
                affixPoints -= newAffix.GetRealAffixCost(thing);
                baseAffixDefs = baseAffixDefs.FindAll(lad => lad.groupName != newAffix.groupName);
            }
        }

        static private int CalculateTotalLootAffixPoints (ThingWithComps thing) {
            float ptsF = 0f;

            // Up to 6 points based on total wealth (1M max)
            float wealth = 0f;
            if (Current.ProgramState == ProgramState.Playing) {  // don't bother while initializing
                if      (thing.Map       != null && thing.Map.wealthWatcher       != null) wealth = thing.Map.wealthWatcher.WealthTotal;
                else if (Find.CurrentMap != null && Find.CurrentMap.wealthWatcher != null) wealth = Find.CurrentMap.wealthWatcher.WealthTotal;
                else if (Find.World      != null)                                          wealth = Find.World.PlayerWealthForStoryteller;
            }

            ptsF += Mathf.Min(wealth / 166_666, 6);

            // Up to 8 points based on item quality
            QualityCategory qc;
            thing.TryGetQuality(out qc);

            // Normal = 1, Good = 2, Excellent = 4, Masterwork = 6, Legendary = 8
            ptsF += Mathf.Pow((int)qc, 2f) / 4.5f;

            // Capped at 12
            ptsF = Mathf.Clamp(ptsF, 0, 12);

            return Mathf.RoundToInt(ptsF);
        }

        static private LootAffixDef PickAffix (ThingWithComps thing, List<LootAffixDef> baseAffixDefs, int curAffixes, int ttlAffixes, float affixPoints) {
            if (curAffixes > ttlAffixes) return null;
            int remainAffixes = ttlAffixes - curAffixes + 1;

            // FIXME: Add config sliders for percentages here

            // First affix has a 90% chance of getting a random pool; other affixes are at 10%
            bool isRandomPool = curAffixes == 1 ?
                0.90f > Random.Range(0.0f, 1.0f) :
                0.10f > Random.Range(0.0f, 1.0f)
            ;

            List<LootAffixDef> filteredAffixDefs = baseAffixDefs.FindAll(lad => lad.GetRealAffixCost(thing) <= affixPoints);

            if (isRandomPool) {
                // Random pool of anything (within the cost)
                return filteredAffixDefs.RandomElementWithFallback();
            }
            else {
                // Rebalance the weight priorities to better reflect the current point total
                float paRatio = affixPoints / remainAffixes;
                paRatio = Mathf.Clamp(paRatio, 1, 6);
                
                return filteredAffixDefs.RandomElementByWeightWithFallback(lad =>
                    /* https://www.desmos.com/calculator/f7yscp2vyj
                     * 3 / max(abs(ac-pa)¹·⁵, 0.25)
                     * eg: p=12 for cost right at the average (with a ±0.5 swing)
                     *     p= 1 for one that's 2.08 away from the average, including negatives
                     */
                    3 / Mathf.Max(
                        Mathf.Pow(Mathf.Abs(lad.GetRealAffixCost(thing) - paRatio), 1.5f),
                    0.25f)
                );
            }
        }

        static public string GetSetFullStuffLabel (this CompLootAffixableThing comp, string label) {
            List<LootAffixDef> affixes    = comp.affixes;
            List<Rule>         affixRules = comp.affixRules;
            ThingWithComps     thing      = comp.parent;

            // Make sure we're not saving color tag information
            label = label.StripTags();
        
            // Short-circuit: No affixes
            if (comp.AffixCount == 0) return label;

            // Short-circuit: Already calculated the full label and no replacement required
            string stuffLabel = GenLabel.ThingLabel(thing.def, thing.Stuff, 1).StripTags();
            if (comp.fullStuffLabel != null && stuffLabel == label) return comp.fullStuffLabel;

            // Short-circuit: Still have the calculated full label
            string preExtra  = "";
            string postExtra = "";
            int pos = label.IndexOf(stuffLabel);
            if (pos >= 0) {
                preExtra  = label.Substring(0, pos);
                postExtra = label.Substring(pos + stuffLabel.Length);
            }

            if (comp.fullStuffLabel != null) return preExtra + comp.fullStuffLabel + postExtra;
            
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
                        if (rule.keyword.StartsWith("AFFIX_")) comp.affixRules.Add(rule);
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

            if (affixRules.Count != comp.AffixCount) {
                Log.Error("Chosen affix words for " + thing + " don't match the number of affixes:\n" + string.Join(
                    "\nvs.\n", 
                    string.Join(" | ", affixRules), 
                    string.Join(", ", affixes)
                ));
            }

            // Add a few types of labels for maximum language flexibility
            request.Rules.Add(new Rule_String( "STUFF_label",      thing.Stuff != null ? thing.Stuff.LabelAsStuff : ""));
            request.Rules.Add(new Rule_String( "THING_defLabel",   thing.def.label));
            request.Rules.Add(new Rule_String( "THING_stuffLabel", stuffLabel));

            string rootKeyword = "r_affix" + comp.AffixCount;
            comp.fullStuffLabel = NameGenerator.GenerateName(request, null, false, rootKeyword, rootKeyword);

            // It's possible we might end up hitting this later than we expected, and run into affixes/word
            // desyncs, so clear the cache, just in case.
            comp.ClearAffixCaches();

            return preExtra + comp.fullStuffLabel + postExtra;
        }
    }
}
