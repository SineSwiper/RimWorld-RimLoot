using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {

    // A very simplified version of StatDef, mostly just to describe the stat, instead of represent
    // values for the stat.  ValueModifierSet will usually handle the stat values itself.
    public class BasicStatDescDef : Def {
        public Type   statClass;
        public string statField;

        public string        translationKey;
        public ToStringStyle toStringStyle             = ToStringStyle.Integer;

        public virtual string TranslationKey {
            get {
                if (translationKey != null) return translationKey;

                // Educated guess...
                return GenText.ToTitleCaseSmart(statField);
            }
        }
        
        public virtual TaggedString GetModifierChangeStat () {
            return TranslationKey.Translate();
        }
        
        public virtual TaggedString GetModifierChangeString (ValueModifierSet valueModifier) {
            return valueModifier.ModifierChangeString(toStringStyle);
        }

        public virtual TaggedString GetModifierChangeString (Def def) {
            return def.LabelCap;
        }

        public virtual TaggedString GetModifierChangeString (float value) {
            return value.ToStringByStyle(toStringStyle, ToStringNumberSense.Absolute);
        }

        public virtual TaggedString GetModifierChangeString (bool value) {
            return (value ? "On" : "Off").Translate();
        }

        public virtual TaggedString GetModifierChangeString (string value) {
            return value;  // FIXME: Is this typically a placeholder?  Need Translate()?
        }

        public override void PostLoad() {
            defName = string.Join(".", statClass?.FullName, statField).Replace(".", "_");
            base.PostLoad();
        }

        public override IEnumerable<string> ConfigErrors () {
            foreach (string configError in base.ConfigErrors())
                yield return configError;

            if (statClass == null) yield return "No statClass defined";
            if (statField == null) yield return "No statField defined";

            if (!TranslationKey.CanTranslate()) yield return "The translation key " + TranslationKey + " doesn't have a translation";
        }

        public static BasicStatDescDef Named(string defName) {
            return DefDatabase<BasicStatDescDef>.GetNamed(defName, true);
        }

        public static BasicStatDescDef Named(Type nameClass, string nameField) {
            string namedDefName = string.Join(".", nameClass.FullName, nameField).Replace(".", "_");
            return DefDatabase<BasicStatDescDef>.GetNamed(namedDefName, true);
        }

        // Install our own affix stat data 
        public void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, string preLabel, ThingWithComps parentThing, LootAffixModifier parentModifier, string finalString, string baseString = null) {
            if (statDrawEntry.LabelCap == GetModifierChangeStat()) {
                // [Reflection] string reportText = statDrawEntry.overrideReportText
                string reportText;
                FieldInfo reportTextField = AccessTools.Field(typeof(StatDrawEntry), "overrideReportText");
                reportText = (string)reportTextField.GetValue(statDrawEntry);

                int    finalValuePos = reportText.IndexOf("StatsReport_FinalValue".Translate() + ": ");

                string affixValueStr = "RimLoot_AffixStatExplanationPart".Translate(preLabel) + ": " + parentModifier.ModifierChangeString + "\n";
            
                // Already has a stats report
                if (finalValuePos >= 0) {
                    reportText = reportText.Substring(0, finalValuePos) + affixValueStr + reportText.Substring(finalValuePos);
                }
                // No stats report; make our own
                else {
                    reportText += "\n\n";
                    if (baseString != null) {
                        string baseValueStr  = "StatsReport_BaseValue" .Translate() + ": " + baseString  + "\n\n";
                        string finalValueStr = "StatsReport_FinalValue".Translate() + ": " + finalString + "\n";
                        reportText += baseValueStr + affixValueStr + finalValueStr;
                    }
                    else reportText += affixValueStr;
                }

                reportTextField.SetValue(statDrawEntry, reportText);
            }
        }

        public void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, string preLabel, ThingWithComps parentThing, LootAffixModifier parentModifier, float baseValue, float finalValue) {
            SpecialDisplayStatsInjectors(
                statDrawEntry:  statDrawEntry,
                preLabel:       preLabel,
                parentThing:    parentThing,
                parentModifier: parentModifier,
                baseString:     GetModifierChangeString(baseValue),
                finalString:    GetModifierChangeString(finalValue)
            );
        }

        public void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, string preLabel, ThingWithComps parentThing, LootAffixModifier parentModifier, Def curDef) {
            SpecialDisplayStatsInjectors(
                statDrawEntry:  statDrawEntry,
                preLabel:       preLabel,
                parentThing:    parentThing,
                parentModifier: parentModifier,
                finalString:    GetModifierChangeString(curDef)
            );
        }

        public void SpecialDisplayStatsInjectors(StatDrawEntry statDrawEntry, string preLabel, ThingWithComps parentThing, LootAffixModifier parentModifier, bool curValue) {
            SpecialDisplayStatsInjectors(
                statDrawEntry:  statDrawEntry,
                preLabel:       preLabel,
                parentThing:    parentThing,
                parentModifier: parentModifier,
                finalString:    GetModifierChangeString(curValue)
            );
        }
    }
}
