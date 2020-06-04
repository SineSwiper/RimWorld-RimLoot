using System;
using System.Collections.Generic;

namespace RimLoot {
    // Helper methods that not even Verse.GenCollection covers...

    public static class CollectionExtension {
        internal static void AddIfNotExist<K, V> (this Dictionary<K, V> dict, K key, V value) {
            if (!dict.ContainsKey(key)) dict.Add(key, value);
        }

        internal static V GetOrAddIfNotExist<K, V> (this Dictionary<K, V> dict, K key, V value) {
            dict.AddIfNotExist(key, value);
            return dict[key];
        }

    }
}
