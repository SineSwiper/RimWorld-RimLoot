using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        // Cached property objects (modified and otherwise)
        private List<VerbProperties> verbProperties;
        private List<VerbProperties> verbPropertiesFromDef;
        private List<Tool>           tools;
        private List<Tool>           toolsFromDef;

        // Cached graphics
        private Texture2D overlayIcon;
        private Texture2D uiIcon;

        public List<LootAffixDef> AllAffixDefs {
            get { return affixes; }
        }

        public int AffixCount {
            get { return affixes.Count; }
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

        public List<VerbProperties> VerbProperties {
            get {
                if (verbProperties != null) return verbProperties;
                MakeAffixCaches();
                return verbProperties;
            }
        }

        public List<VerbProperties> VerbPropertiesFromDef {
            get {
                if (verbPropertiesFromDef != null) return verbPropertiesFromDef;
                MakeAffixCaches();
                return verbPropertiesFromDef;
            }
        }

        public VerbProperties PrimaryVerbProps {
            get {
                return VerbProperties.FirstOrFallback(x => x.isPrimary);
            }
        }

        public VerbProperties PrimaryVerbPropsFromDef {
            get {
                return VerbPropertiesFromDef.FirstOrFallback(x => x.isPrimary);
            }
        }

        public List<Tool> Tools {
            get {
                if (tools != null) return tools;
                MakeAffixCaches();
                return tools;
            }
        }

        public List<Tool> ToolsFromDef {
            get {
                if (toolsFromDef != null) return toolsFromDef;
                MakeAffixCaches();
                return toolsFromDef;
            }
        }

        public Texture2D OverlayIcon {
            get {
                if (overlayIcon != null) return overlayIcon;
                MakeIcons();
                return overlayIcon;
            }
        }

        public Texture2D UIIcon {
            get {
                if (uiIcon != null) return uiIcon;
                MakeIcons();
                return uiIcon;
            }
        }

        private void MakeAffixCaches() {
            // Affix caches
            affixStringsCached = affixRules.Select(r => r.Generate()).ToList();

            affixDefDictCached     = new Dictionary<string, LootAffixDef> {};
            affixStringsDictCached = new Dictionary<LootAffixDef, string> {};
            modifiersCached        = new HashSet<LootAffixModifier>       {};
            ttlAffixPoints         = 0;

            for (int i = 0; i < AffixCount; i++) {
                // Null is some bizarre error from Prepare Carefully
                // The "null_output" was an old save bug which clobbered the rule data
                if (affixStringsCached[i] == null || affixStringsCached[i] == "null_output") {
                    affixStringsCached[i] = affixes[i].LabelCap;
                    affixRules[i] = new Rule_String("unknown->" + affixStringsCached[i]);
                }

                affixDefDictCached    [ affixStringsCached[i] ] = affixes[i];
                affixStringsDictCached[ affixes[i] ] = affixStringsCached[i];
                modifiersCached.AddRange(affixes[i].modifiers);
                ttlAffixPoints += affixes[i].affixCost;
            }

            // Add new modified VerbProperties, if necessary
            var verbModifierDefs = affixes.Where(
                lad => lad.modifiers.Any( lam => lam.AppliesTo == ModifierTarget.VerbProperties )
            ).ToList();

            if (verbModifierDefs.Count > 0) {
                verbPropertiesFromDef = parent.def.Verbs;
                verbProperties        = parent.def.Verbs.Select(vp => vp.MemberwiseClone()).ToList();

                foreach (LootAffixDef lad in verbModifierDefs) {
                    foreach (var vp in verbProperties) {
                        lad.ModifyVerbProperties(parent, vp);
                    }
                }
            }
            else {
                verbProperties = verbPropertiesFromDef = parent.def.Verbs;
            }

            // Add new modified Tools, if necessary
            var toolModifierDefs = affixes.Where(
                lad => lad.modifiers.Any( lam => lam.AppliesTo == ModifierTarget.Tools )
            ).ToList();

            toolsFromDef = parent.def.tools;
            // prevent infinite null check loops from Tools/ToolsFromDef -> InitVerbsFromZero -> CompEquippable.Tools
            if (toolsFromDef == null) toolsFromDef = new List<Tool> {};

            if (toolModifierDefs.Count > 0) {
                // [Reflection prep] tool.MemberwiseClone()
                MethodInfo ToolMemberwiseClone = AccessTools.Method(typeof(Tool), "MemberwiseClone");

                tools = toolsFromDef.Select( t => (Tool)ToolMemberwiseClone.Invoke(t, new object[] {}) ).ToList();

                foreach (LootAffixDef lad in toolModifierDefs) {
                    foreach (var tool in tools) {
                        lad.ModifyTool(parent, tool);
                    }
                }
            }
            else {
                tools = toolsFromDef;
            }

            // Refresh the VerbTracker cache
            var equippable = parent.TryGetComp<CompEquippable>();
            if (equippable != null && (verbModifierDefs.Count > 0 || toolModifierDefs.Count > 0)) {
                // [Reflection] verbTracker.verbs
                FieldInfo verbsField = AccessTools.Field(typeof(VerbTracker), "verbs");
                List<Verb> verbs = (List<Verb>)verbsField.GetValue(equippable.VerbTracker);

                // Don't call this if it's already empty.  It may already be in the middle of filling it in.
                if (!verbs.NullOrEmpty()) {
                    // [Reflection] equippable.VerbTracker.InitVerbsFromZero()
                    MethodInfo InitVerbsFromZero = AccessTools.Method(typeof(VerbTracker), "InitVerbsFromZero");
                    InitVerbsFromZero.Invoke(equippable.VerbTracker, new object[] {});
                }
            }

            MakeIcons();
        }

        private void ClearAffixCaches() {
            affixStringsCached?    .Clear();
            affixDefDictCached?    .Clear();
            affixStringsDictCached?.Clear();
            modifiersCached?       .Clear();
            ttlAffixPoints         = null;
            verbProperties         = null;
            verbPropertiesFromDef  = null;
            overlayIcon            = null;
            uiIcon                 = null;

            // Clear the VerbTracker cache
            var equippable = parent.TryGetComp<CompEquippable>();
            if (equippable != null) {
                // [Reflection] equippable.VerbTracker.verbs = null
                FieldInfo verbsField = AccessTools.Field(typeof(VerbTracker), "verbs");
                verbsField.SetValue(equippable.VerbTracker, null);
            }
        }

        public override void PostExposeData() {
            Scribe_Values.Look(ref fullStuffLabel, "fullStuffLabel", null, false);
            Scribe_Collections.Look(ref affixes, false, "affixes", LookMode.Def, (object) this);

            if      (Scribe.mode == LoadSaveMode.Saving) {
                List<string> affixRuleStrings = affixRules.Select(r => r.ToString().Replace(" → ", "->")).ToList();
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
            if (signal == "AboutToFireShot") {
                foreach (LootAffixDef affix in affixes) {
                    affix.PreShotFired(parent);
                }
            }
            if (signal == "FiredShot") {
                foreach (LootAffixDef affix in affixes) {
                    affix.PostShotFired(parent);
                }
            }
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

        public void PostAffixCleanup(bool fixLabel = true) {
            ClearAffixCaches();

            if (fixLabel) {
                affixRules.Clear();
                fullStuffLabel = null;
                string name = parent.LabelNoCount;
                name = TransformLabel(name);
            }

            foreach (LootAffixDef affix in affixes) {
                affix.PostApplyAffix(parent);
            }
        }

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
            // 10% chance it doesn't follow this first rule
            if (0.10f < Random.Range(0.0f, 1.0f)) {
                LootAffixDef firstAffix = filteredAffixDefs.RandomElementWithFallback();
                if (firstAffix == null) return;

                affixes.Add(firstAffix);
                affixPoints -= firstAffix.affixCost;
                baseAffixDefs = baseAffixDefs.FindAll(lad => lad.groupName != firstAffix.groupName);
            }

            // Remaining affixes: rebalance the weight priorities to better reflect the current point total
            for (int curAffixes = affixes.Count + 1; curAffixes <= 4; curAffixes++) {
                if (curAffixes > ttlAffixes) return;
                int remainAffixes = ttlAffixes - curAffixes + 1;

                float paRatio = affixPoints / remainAffixes;
                paRatio = Mathf.Clamp(paRatio, 1, 6);
                
                filteredAffixDefs     = baseAffixDefs.FindAll(lad => lad.affixCost <= affixPoints);
                LootAffixDef newAffix = filteredAffixDefs.RandomElementByWeightWithFallback(lad =>
                    /* https://www.desmos.com/calculator/f7yscp2vyj
                     * 6 / max(abs(ac-pa)³, 0.25)
                     * eg: p=24 for cost right at the average (with a ±0.6 swing)
                     *     p= 1 for one that's 1.78 away from the average, including negatives
                     */
                    6 / Mathf.Max(
                        Mathf.Pow(Mathf.Abs(lad.affixCost - paRatio), 3),
                    0.25f)
                );
                if (newAffix == null) return;

                affixes.Add(newAffix);
                affixPoints -= newAffix.affixCost;
                baseAffixDefs = baseAffixDefs.FindAll(lad => lad.groupName != newAffix.groupName);
            }
        }

        public int CalculateTotalLootAffixPoints() {
            float ptsF = 0f;

            // Up to 6 points based on total wealth (1M max)
            float wealth = 0f;
            if (Current.ProgramState == ProgramState.Playing) {  // don't bother while initializing
                if      (parent.Map      != null && parent.Map.wealthWatcher      != null) wealth = parent.Map.wealthWatcher.WealthTotal;
                else if (Find.CurrentMap != null && Find.CurrentMap.wealthWatcher != null) wealth = Find.CurrentMap.wealthWatcher.WealthTotal;
                else if (Find.World      != null)                                          wealth = Find.World.PlayerWealthForStoryteller;
            }

            ptsF += Mathf.Min(wealth / 166_666, 6);

            // Up to 8 points based on item quality
            QualityCategory qc;
            parent.TryGetQuality(out qc);

            // Normal = 1, Good = 2, Excellent = 4, Masterwork = 6, Legendary = 8
            ptsF += Mathf.Pow((int)qc, 2f) / 4.5f;

            // Capped at 12
            ptsF = Mathf.Clamp(ptsF, 0, 12);

            return Mathf.RoundToInt(ptsF);
        }

        public override string TransformLabel(string label) {
            // Short-circuit: No affixes
            if (AffixCount == 0) return label;

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

            if (affixRules.Count != AffixCount) {
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

            string rootKeyword = "r_affix" + AffixCount;
            fullStuffLabel = NameGenerator.GenerateName(request, null, false, rootKeyword, rootKeyword);

            // It's possible we might end up hitting this later than we expected, and run into affixes/word
            // desyncs, so clear the cache, just in case.
            ClearAffixCaches();

            return preExtra + fullStuffLabel + postExtra;
        }

        public override bool AllowStackWith (Thing other) {
            var otherComp = other.TryGetComp<CompLootAffixableThing>();
            if (otherComp == null) return false;

            // Count short-circuits
            if (AffixCount != otherComp.AffixCount) return false;
            // same counts at this point, so no need to check the other side
            if (AffixCount == 0)                    return true;

            // Only allow affixes with the same name
            return affixRules.Except(otherComp.affixRules).Count() == 0;
        }

        public override void PostSplitOff (Thing piece) {
            base.PostSplitOff(piece);

            // (ToList = MemberwiseClone)
            var comp = piece.TryGetComp<CompLootAffixableThing>();
            comp.affixes        = affixes   .ToList();
            comp.affixRules     = affixRules.ToList();
            comp.fullStuffLabel = fullStuffLabel;

            comp.PostAffixCleanup(false);
        }

        // FIXME: Use these for cursed items?
        public override string CompInspectStringExtra() {
            if (AffixCount > 0) {
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

        private void MakeIcons () {
            Texture2D defIcon = parent.def.uiIcon;
            uiIcon = defIcon;

            if (!UnityData.IsInMainThread) return;  // too early to be fetching this stuff
            if (AffixCount == 0)           return;

            // Use the highest affix cost for the color
            LootAffixDef highestAffix = affixes.FirstOrFallback(
                lad => Mathf.Abs(lad.affixCost) >= 5,  // deadly overrides others
                affixes.OrderByDescending(lad => lad.affixCost).First()
            );
            Color color = Color.white;
            ColorUtility.TryParseHtmlString(highestAffix.LabelColor, out color);

            string texPart = AffixCount + "Affix";
            if (Mathf.Abs(highestAffix.affixCost) >= 5) texPart = "Deadly";

            // Grab the overlay icon
            float scale = Mathf.Sqrt(defIcon.width * defIcon.height) / 256;  // 64x64 -> 16x16 overlays
            overlayIcon = IconUtility.FetchOrMakeIcon(texPart, color, scale);

            // Apply the overlay onto the Thing icon
            uiIcon = defIcon.CloneAsReadable();
            uiIcon.AddOverlayToBLCorner(overlayIcon);
            uiIcon.Apply(true, true);  // apply and lock
        }

        public override void PostDraw() {
            if (AffixCount == 0) return;
            
            // NOTE: Everything in RimWorld treats X=horizonal, Z=vertical, Y=depth
            Vector3 vector = parent.DrawPos;
            vector.x -= .40f;
            vector.z -= .30f;
            vector.y  = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);

            Vector3   scale  = new Vector3(.25f, 1f, .25f);
            Matrix4x4 matrix = default;
            matrix.SetTRS(vector, Quaternion.AngleAxis(0f, Vector3.up), scale);

            Graphics.DrawMesh(MeshPool.plane10, matrix, MaterialPool.MatFrom(OverlayIcon), 0);
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
                reportText += affix.FullStatsReport(parent, affixKey) + "\n";
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

        // NOTE: This also will get the SpecialDisplayStats entries above
        public void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry) {
            var affixDict = AllAffixDefsByAffixes;
            foreach (string affixKey in AffixStrings) {
                LootAffixDef affix = affixDict[affixKey];
                affix.SpecialDisplayStatsInjectors(statDrawEntry, parent, affixKey);
            }
        }
    }
}
