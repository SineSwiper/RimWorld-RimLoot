using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    public class CompLootAffixableThing : ThingComp {
        internal string fullStuffLabel = null;

        internal List<LootAffixDef> affixes    = new List<LootAffixDef>();
        internal List<Rule>         affixRules = new List<Rule>();

        // Cached values
        private List<string>                     affixStringsCached;
        private Dictionary<string, LootAffixDef> affixDefDictCached;
        private Dictionary<LootAffixDef, string> affixStringsDictCached;
        private HashSet<LootAffixModifier>       modifiersCached;
        private float?                           ttlAffixPoints;

        // Cached modified property objects
        private List<VerbProperties> verbProperties;
        private List<Tool>           tools;

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
            // Use the Base cache here, which would prevent MakeAffixCaches loops
            get {
                return Base.origVerbPropertiesCache.GetOrAddIfNotExist(parent.def.defName, parent.def.Verbs);
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
            // Use the Base cache here, which would prevent MakeAffixCaches loops
            get {
                return Base.origToolsCache.GetOrAddIfNotExist(
                    parent.def.defName,
                    // tools doesn't have a null safeguard like EmptyVerbPropertiesList
                    parent.def.tools ?? new List<Tool> {}
                );
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
                ttlAffixPoints += affixes[i].GetRealAffixCost(parent);
            }

            // Add new modified VerbProperties, if necessary
            var verbModifierDefs = affixes.Where(
                lad => lad.modifiers.Any( lam => lam.AppliesTo == ModifierTarget.VerbProperties )
            ).ToList();

            if (verbModifierDefs.Count > 0) {
                verbProperties = VerbPropertiesFromDef.Select(vp => vp.MemberwiseClone()).ToList();

                foreach (LootAffixDef lad in verbModifierDefs) {
                    foreach (var vp in verbProperties) {
                        lad.ModifyVerbProperties(parent, vp);
                    }
                }
            }
            else {
                verbProperties = VerbPropertiesFromDef;
            }

            // Add new modified Tools, if necessary
            var toolModifierDefs = affixes.Where(
                lad => lad.modifiers.Any( lam => lam.AppliesTo == ModifierTarget.Tools )
            ).ToList();

            if (toolModifierDefs.Count > 0) {
                // [Reflection prep] tool.MemberwiseClone()
                MethodInfo ToolMemberwiseClone = AccessTools.Method(typeof(Tool), "MemberwiseClone");

                tools = ToolsFromDef.Select( t => (Tool)ToolMemberwiseClone.Invoke(t, new object[] {}) ).ToList();

                foreach (LootAffixDef lad in toolModifierDefs) {
                    foreach (var tool in tools) {
                        lad.ModifyTool(parent, tool);
                    }
                }
            }
            else {
                tools = ToolsFromDef;
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

        internal void ClearAffixCaches() {
            affixStringsCached?    .Clear();
            affixDefDictCached?    .Clear();
            affixStringsDictCached?.Clear();
            modifiersCached?       .Clear();
            ttlAffixPoints         = null;
            verbProperties         = null;
            tools                  = null;
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

                // Clean out any buggy labels with color tags
                fullStuffLabel.StripTags();
            }

            this.PostAffixCleanup(false);

            // FIXME: Might not need this now...
            foreach (LootAffixDef affix in affixes) {
                affix.PostExposeData(parent);
            }
        }

        public override void ReceiveCompSignal(string signal) {
            if (signal == "SetQuality") {
                // One of the affixes changes quality, so don't clobber it
                if (affixes.Count == 0) this.InitializeAffixes();
            }
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

        public override void PostDestroy (DestroyMode mode, Map previousMap) {
            foreach (LootAffixDef affix in affixes) {
                affix.PostDestroy(parent);
            }
        }

        public override void CompTick() {
            foreach (LootAffixDef affix in affixes) {
                affix.CheckTick(parent);
            }
        }
    
        public override void CompTickRare() {
            foreach (LootAffixDef affix in affixes) {
                affix.CheckTick(parent);
            }
        }

        public override void Notify_Equipped(Pawn pawn) {
            CheckAndSendNegativeDeadlyAffixLetter(pawn);
        }

        public void Notify_ApparelAdded(Pawn pawn) {
            CheckAndSendNegativeDeadlyAffixLetter(pawn);
        }

        public void CheckAndSendNegativeDeadlyAffixLetter(Pawn pawn) {
            if (Current.ProgramState != ProgramState.Playing) return;
            LootAffixDef deadlyAffix = affixes.FirstOrFallback(lad => lad.IsNegativeDeadly(parent));
            if (deadlyAffix == null || pawn.Faction != Faction.OfPlayer) return;

            ChoiceLetter choiceLetter = LetterMaker.MakeLetter(
                label:       "RimLoot_CursedItem".Translate() + ": " + pawn.LabelShortCap + " → " + deadlyAffix.LabelCap,
                text:        "RimLoot_NegativeDeadlyAffixLetter_Text".Translate(pawn.Named("PAWN")),
                def:         DefDatabase<LetterDef>.GetNamed("RimLoot_NegativeDeadlyAffix"),
                lookTargets: new LookTargets( new GlobalTargetInfo(pawn), new GlobalTargetInfo(parent) )
            );
            Find.LetterStack.ReceiveLetter(choiceLetter);
        }

        public override string TransformLabel(string label) {
            return this.GetSetFullStuffLabel(label);
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

        public override string CompInspectStringExtra() {
            if (AffixCount > 0) {
                return
                    "RimLoot_Affixes".Translate() + ": " +
                    GenText.ToCommaList(
                        AllAffixDefsByAffixes.Select( kv => kv.Value.LabelWithStyle(parent, kv.Key) ), false
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
                lad => lad.IsDeadly(parent),  // deadly overrides others
                affixes.OrderByDescending(lad => lad.GetRealAffixCost(parent)).First()
            );
            ColorUtility.TryParseHtmlString(highestAffix.LabelColor(parent), out Color color);

            string texPart = AffixCount + "Affix";
            if (highestAffix.IsDeadly(parent)) texPart = "Deadly";

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
                reportText += "[DEV] Total Points: " + affixes.Select( lad => lad.GetRealAffixCost(parent) ).Sum() + 
                    "\n    " +
                    string.Join("\n    ", affixes.Select( lad => AllAffixesByAffixDefs[lad] + ": " + lad.GetRealAffixCost(parent) )) + 
                    "\n\n"
                ;
            }

            yield return new StatDrawEntry(
                category:    category,
                label:       "RimLoot_LootAffixModifiers".Translate(),
                valueString: GenText.ToCommaList(
                    AllAffixDefsByAffixes.Select( kv => kv.Value.LabelWithStyle(parent, kv.Key) ), false
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
            // Fix firefoam damage displays
            if (
                statDrawEntry.LabelCap == "Damage".Translate() && parent.def.IsWeaponUsingProjectiles &&
                PrimaryVerbProps?.defaultProjectile?.projectile.damageDef?.harmsHealth == false
            ) {
                // [Reflection] statDrawEntry.value = 0f
                FieldInfo valueField = AccessTools.Field(typeof(StatDrawEntry), "value");
                valueField.SetValue(statDrawEntry, 0f);
            }

            var affixDict = AllAffixDefsByAffixes;
            foreach (string affixKey in AffixStrings) {
                LootAffixDef affix = affixDict[affixKey];
                affix.SpecialDisplayStatsInjectors(statDrawEntry, parent, affixKey);
            }
        }
    }
}
