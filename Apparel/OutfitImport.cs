using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Arise_RTM
{
    public static class OutfitImport
    {
        public struct DrawTex
        {
            public int Draw;
            public int Tex;
        }

        public sealed class ImportedOutfit
        {
            public string Name;
            public readonly Dictionary<string, DrawTex> Parts = new Dictionary<string, DrawTex>();
        }

        private static readonly Dictionary<string, string> KeyAliases = new Dictionary<string, string>
        {
            { "top", "top" },
            { "undershirt", "undershirt" },
            { "legs", "legs" },
            { "feet", "feet" }, { "shoes", "feet" },
            { "accessories", "accessories" }, { "misc", "accessories" },
            { "bags", "bags" }, { "bag", "bags" },
            { "mask", "mask" },
            { "gloves", "gloves" }, { "torso", "gloves" },
            { "decals", "decals" },
            { "armor", "armor" },
            { "hair", "hair" },
            { "hats", "hats" }, { "hat", "hats" },
            { "glasses", "glasses" },
            { "ears", "ears" }, { "earrings", "ears" },
            { "watches", "watches" }, { "watch", "watches" },
            { "bracelets", "bracelets" }, { "bracelet", "bracelets" }
        };

        private static readonly string[] CanonicalOrder =
        {
            "top", "undershirt", "legs", "feet", "accessories", "bags", "mask",
            "gloves", "decals", "armor", "hair", "hats", "glasses", "ears", "watches", "bracelets"
        };

        // Keys are located by name, not by line, so text pasted without line breaks still parses.
        private static readonly Regex KeyPattern = new Regex(
            "(" + string.Join("|", KeyAliases.Keys.Concat(new[] { "name" })
                .OrderByDescending(k => k.Length).Select(Regex.Escape)) + @")\s*=",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParse(string text, out ImportedOutfit outfit, out string error)
        {
            outfit = new ImportedOutfit();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Nothing to import.";
                return false;
            }

            MatchCollection matches = KeyPattern.Matches(text);
            if (matches.Count == 0)
            {
                error = "No 'field = value' pairs found.";
                return false;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                Match m = matches[i];
                int start = m.Index + m.Length;
                int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                string value = text.Substring(start, end - start);
                string key = m.Groups[1].Value.ToLowerInvariant();

                if (key == "name")
                {
                    int lineEnd = value.IndexOfAny(new[] { '\r', '\n' });
                    if (lineEnd >= 0)
                        value = value.Substring(0, lineEnd);

                    outfit.Name = value.Trim();
                    if (outfit.Name.Length == 0)
                    {
                        error = "The name is empty.";
                        return false;
                    }
                    continue;
                }

                value = value.Trim();
                if (!TryParseDrawTex(value, out DrawTex part))
                {
                    error = $"'{key}' must be 'draw/texture' (e.g. 7/0), found '{value}'.";
                    return false;
                }

                outfit.Parts[KeyAliases[key]] = part;
            }

            return true;
        }

        private static bool TryParseDrawTex(string value, out DrawTex part)
        {
            part = new DrawTex();
            string[] pieces = value.Split('/');
            if (pieces.Length != 2)
                return false;

            return int.TryParse(pieces[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out part.Draw)
                && int.TryParse(pieces[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out part.Tex);
        }

        public static string Format(ImportedOutfit outfit)
        {
            StringBuilder sb = new StringBuilder();
            if (outfit.Name != null)
                sb.Append("name = ").Append(outfit.Name).Append(Environment.NewLine);

            foreach (string key in CanonicalOrder)
            {
                if (outfit.Parts.TryGetValue(key, out DrawTex part))
                    sb.Append(key).Append(" = ").Append(part.Draw).Append('/').Append(part.Tex).Append(Environment.NewLine);
            }

            return sb.ToString().TrimEnd();
        }

        public static GTAOutfits.OutfitComponents ApplyTo(ImportedOutfit outfit, GTAOutfits.OutfitComponents c)
        {
            foreach (KeyValuePair<string, DrawTex> entry in outfit.Parts)
            {
                int d = entry.Value.Draw;
                int t = entry.Value.Tex;
                switch (entry.Key)
                {
                    case "top": c.Top = d; c.TopTex = t; break;
                    case "undershirt": c.Undershirt = d; c.UndershirtTex = t; break;
                    case "legs": c.Legs = d; c.LegsTex = t; break;
                    case "feet": c.Shoes = d; c.ShoesTex = t; break;
                    case "accessories": c.Misc = d; c.MiscTex = t; break;
                    case "bags": c.Bag = d; c.BagTex = t; break;
                    case "mask": c.Mask = d; c.MaskTex = t; break;
                    case "gloves": c.Gloves = d; c.GlovesTex = t; break;
                    case "decals": c.Decals = d; c.DecalsTex = t; break;
                    case "armor": c.Armor = d; c.ArmorTex = t; break;
                    case "hair": c.Hair = d; c.HairTex = t; break;
                    case "hats": c.Hat = d; c.HatTex = t; break;
                    case "glasses": c.Glasses = d; c.GlassesTex = t; break;
                    case "ears": c.Earring = d; c.EarringTex = t; break;
                    case "watches": c.Watches = d; c.WatchesTex = t; break;
                    case "bracelets": c.Bracelets = d; c.BraceletsTex = t; break;
                }
            }
            return c;
        }
    }
}
