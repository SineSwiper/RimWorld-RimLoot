using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimLoot {
    static public class IconUtility {
        static private readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D> {};

        static public Texture2D FetchOrMakeIcon (string texPart, Color color, float scale) {
            string cacheKey = string.Join("*", texPart, color, scale);
            if (iconCache.ContainsKey(cacheKey)) return iconCache[cacheKey];

            // Make it from stratch
            string   largeCacheKey = string.Join("*", texPart, color,    1f);
            string overlayCacheKey = string.Join("*", texPart, color, scale);

            Texture2D largeIcon;
            if (iconCache.ContainsKey(largeCacheKey)) largeIcon = iconCache[largeCacheKey];
            else {
                largeIcon = ContentFinder<Texture2D>.Get("UI/Overlays/RimLoot_" + texPart, true).CloneAsReadable();

                // Colorize and that's the largeIcon
                largeIcon.Colorize(color);
                largeIcon.Apply();
                iconCache[largeCacheKey] = largeIcon;
            }

            Texture2D overlayIcon = largeIcon.Clone();

            // Scale it and that's the overlayIcon
            overlayIcon.BilinearScale(
                Mathf.RoundToInt(largeIcon.width  * scale),
                Mathf.RoundToInt(largeIcon.height * scale)
            );
            overlayIcon.Apply();

            // Stash it into cache
            iconCache[overlayCacheKey] = overlayIcon;
            return iconCache[cacheKey];
        }
        
    }
}
