using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLoot {
    public class BasicStatDescDef_RPM : BasicStatDescDef {
        // Converts ticks into rounds per minute
        private ValueModifierSet cachedInversedValueModifier;

        public override TaggedString GetModifierChangeString (ValueModifierSet valueModifier) {
            if (cachedInversedValueModifier == null) {
                var newValueModifier = valueModifier.MemberwiseClone();
                if (newValueModifier.setValue != null && newValueModifier.setValue   != 0) newValueModifier.setValue   = 60f / ((int)newValueModifier.setValue).TicksToSeconds();
                if (                                     newValueModifier.addValue   != 0) newValueModifier.addValue   = 60f / ((int)newValueModifier.addValue).TicksToSeconds();
                if (                                     newValueModifier.multiplier != 0) newValueModifier.multiplier = 1 / newValueModifier.multiplier;
                cachedInversedValueModifier = newValueModifier;
            }

            return cachedInversedValueModifier.ModifierChangeString(toStringStyle);
        }

        public override TaggedString GetModifierChangeString (float value) {
            value = 60f / ((int)value).TicksToSeconds();
            return value.ToString("0.##") + " rpm";
        }
    }
}
