using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    public enum IconType : byte {
        Large,
        Overlay
    };

    static public class IconUtility {
        static private readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D> {};

        static public Texture2D FetchOrMakeIcon (string texPart, Color color, IconType type) {
            string cacheKey = string.Join("*", texPart, color, type);
            if (iconCache.ContainsKey(cacheKey)) return iconCache[cacheKey];

            // Make it from stratch
            string   largeCacheKey = string.Join("*", texPart, color, IconType.Large);
            string overlayCacheKey = string.Join("*", texPart, color, IconType.Overlay);
                
            Texture2D overlayIcon = ContentFinder<Texture2D>.Get("UI/Overlays/RimLoot_" + texPart, true).CloneAsReadable();

            // Colorize and that's the largeIcon
            overlayIcon.Colorize(color);
            overlayIcon.Apply();
            Texture2D largeIcon = overlayIcon.Clone();
            largeIcon.Apply(true, true);  // lock down the larger icon

            // Scale it and that's the overlayIcon
            overlayIcon.BilinearScale(
                Mathf.RoundToInt(largeIcon.width  * .25f),
                Mathf.RoundToInt(largeIcon.height * .25f)
            );
            overlayIcon.Apply();

            // Stash it into cache
            iconCache[largeCacheKey]   = largeIcon;
            iconCache[overlayCacheKey] = overlayIcon;
            return iconCache[cacheKey];
        }
        
    }
}
