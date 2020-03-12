using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    // Also houses utilities for item naming
    class LootAffixNamerConfigDef : Def {
        public Dictionary<string,int> maxWordClasses = new Dictionary<string,int> ();
        public List<List<string>> disallowedAffixCombos = new List<List<string>> ();
        
        public bool IsWordClassComboAllowed (List<Rule> rulesToCheck) {
            string rulesString = string.Join( ",",
                rulesToCheck.
                Where  ( r => r.keyword.StartsWith("AFFIX_")  ).
                Select ( r => r.keyword.Replace("AFFIX_", "") ).
                OrderBy( s => s ).
                ToArray()
            );

            foreach (List<string> combo in disallowedAffixCombos) {
                string comboString = string.Join( ",", combo.OrderBy( s => s ).ToArray() );
                if (rulesString == comboString) return false;
            }

            return true;
        }
    }
}
