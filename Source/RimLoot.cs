using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using RimWorld;
using Verse;

namespace RimLoot {
    public class Base : ModBase {
        public override string ModIdentifier {
            get { return "RimLoot"; }
        }
        public static Base         Instance    { get; private set; }
        public static bool IsDebug             { get; private set; }

        internal HugsLib.Utils.ModLogger ModLogger { get; private set; }

        public Base() {
            Instance    = this;
            ModLogger   = this.Logger;
            IsDebug     = false;
        }

        // FIXME
        // internal Dictionary<string, SettingHandle> config = new Dictionary<string, SettingHandle>();

        public override void DefsLoaded() {
            ProcessSettings();

            // Add CompLootAffixableThing to all apparel and weapons
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where( t => 
                // Basic checks
                t.category == ThingCategory.Item && t.selectable && t.alwaysHaulable &&
                t.HasComp(typeof(CompQuality)) && (
                    // Apparel check
                    t.IsApparel ||
                    // Weapons check 
                    (
                        (t.IsMeleeWeapon || t.IsRangedWeapon) &&
                        t.equipmentType == EquipmentType.Primary && t.HasComp(typeof(CompEquippable)) &&
                        // No beer bottles or resource-acquired weapons (like Thrumbo horns)
                        !t.IsIngestible
                    )
                )
            ) ) {
                thingDef.comps.Add( new CompProperties_LootAffixableThing() );
            }

            // Add extra StatParts for various affix multipliers
            StatDefOf.MarketValue.parts.Add(new StatPart_LootAffix_MarketValue { parentStat = StatDefOf.MarketValue });

            StatDef adps = StatDefOf.MeleeWeapon_AverageDPS;
            if (adps.parts == null) adps.parts = new List<StatPart> {};
            adps.parts.Add(new StatPart_LootAffix_MeleeAverageDPS { parentStat = adps });

            // FIXME: Add sanity checks for LootAffixDefs, like CanBeAppliedToThing
            // FIXME: Post-loading code to add in CompProperties_LootAffixableThing
        }

        public void ProcessSettings () {
            // FIXME
        }

    }
}
