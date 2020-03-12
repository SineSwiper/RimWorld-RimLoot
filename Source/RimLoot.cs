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
                t.category == ThingCategory.Item && t.selectable && t.alwaysHaulable && (
                    // Apparel check
                    t.IsApparel ||
                    // Weapons check 
                    (
                        (t.IsMeleeWeapon || t.IsRangedWeapon) &&
                        // No beer bottles or resource-acquired weapons (like Thrumbo horns)
                        t.equipmentType == EquipmentType.Primary &&
                        t.HasComp(typeof(CompEquippable)) && t.HasComp(typeof(CompBiocodableWeapon))
                    )
                )
            ) ) {
                thingDef.comps.Add( new CompProperties_LootAffixableThing() );
            }

            // FIXME: Add sanity checks for LootAffixDefs, like CanBeAppliedToThing
        }

        public void ProcessSettings () {
            // FIXME
        }

    }
}
