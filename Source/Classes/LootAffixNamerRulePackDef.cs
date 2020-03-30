using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    // Also houses utilities for item naming

    // FIXME: Migrate more stuff here
    class LootAffixNamerRulePackDef : RulePackDef {
        public Dictionary<string,int> maxWordClasses = new Dictionary<string,int> ();
        public List<string> disallowedAffixCombos    = new List<string> ();

        Regex rgxKeywordClean = new Regex(@"AFFIX_(\w+?)\d+");
        Regex rgxStrDelimiter = new Regex(@"[^a-zA-Z0-9_/]+");
        public bool IsWordClassComboAllowed (List<Rule> rulesToCheck) {
            string rulesString = string.Join( ",",
                rulesToCheck.
                Where  ( r => r.keyword.StartsWith("AFFIX_")  ).
                Select ( r => rgxKeywordClean.Match(r.keyword) ).
                Select ( m => m.Groups[1].ToString() ).
                OrderBy( s => s ).
                ToArray()
            );

            foreach (string comboString in disallowedAffixCombos) {
                if (rulesString == comboString) return false;
            }

            return true;
        }

        // XXX: This is mostly just for maxWordClasses, but you can't define a LoadDataFromXmlCustom for a
        // Dictionary, sadly.
        public void LoadDataFromXmlCustom(XmlNode xmlRoot) {
            foreach (XmlNode xmlMain in xmlRoot.ChildNodes) {
                if (xmlMain.NodeType != XmlNodeType.Element) continue;

                switch (xmlMain.Name) {
                    case "defName":
                        defName = ParseHelper.FromString<string>(xmlMain.FirstChild.Value);
                        break;

                    case "rulePack":
                        RulePack rulePackFromXml = DirectXmlToObject.ObjectFromXml<RulePack>(xmlMain, false);
                    
                        // [Reflection] this.rulePack = rulePackFromXml;
                        FieldInfo rulePackField = AccessTools.Field(typeof(RulePackDef), "rulePack");
                        rulePackField.SetValue(this, rulePackFromXml);
                    break;

                    case "disallowedAffixCombos":
                        disallowedAffixCombos = DirectXmlToObject.ObjectFromXml<List<string>>(xmlMain, false);
                    break;

                    case "maxWordClasses":
                        foreach (XmlNode node in xmlMain.ChildNodes) {
                            string key = ParseHelper.FromString<string>(node.Name);
                            int    val = ParseHelper.FromString<int>   (node.FirstChild.Value);
                            maxWordClasses.Add(key, val);
                        }
                    break;
                    
                    default:
                        Log.Error("XML error: " + xmlMain.Name + " doesn't correspond to any field in type " + this.GetType().Name + ". Context: " + xmlMain.OuterXml);
                    break;
                }
            }

        }

        public override void PostLoad() {
            base.PostLoad();

            // Clean up delimiters in disallowedAffixCombos
            for (int i = 0; i < disallowedAffixCombos.Count; i++) {
                List<string> combo = rgxStrDelimiter.Split(disallowedAffixCombos[i]).ToList();
                disallowedAffixCombos[i] = string.Join( ",", combo.OrderBy( s => s ).ToArray() );
            }
        }

    }
}
