using System;
using System.Collections.Generic;
using System.IO;

namespace Arise_RTM
{
    /// <summary>Memory layout of the outfit slots for the supported game version (offsets.ini).</summary>
    public static class OutfitOffsets
    {
        public static ulong NamesBase { get; private set; }
        public static ulong ClothesDrawDelta { get; private set; }
        public static ulong ClothesTexDelta { get; private set; }
        public static int ClothesSlotStride { get; private set; }
        public static ulong PropsDrawDelta { get; private set; }
        public static ulong PropsTexDelta { get; private set; }
        public static int PropsSlotStride { get; private set; }
        public static int SlotSize { get; private set; }
        public static int SlotCount { get; private set; }

        private static readonly string OffsetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offsets.ini");

        public static void Load(string gameVersion)
        {
            Dictionary<string, string> offsets = ParseSection(OffsetsPath, gameVersion);
            if (offsets.Count == 0)
                throw new InvalidOperationException($"Section [{gameVersion}] not found in offsets.ini.");

            NamesBase = Number(offsets, "vanilla_base");
            ClothesDrawDelta = Number(offsets, "clothes_draw_delta");
            ClothesTexDelta = Number(offsets, "clothes_tex_delta");
            ClothesSlotStride = (int)Number(offsets, "clothes_slot_stride");
            PropsDrawDelta = Number(offsets, "props_draw_delta");
            PropsTexDelta = Number(offsets, "props_tex_delta");
            PropsSlotStride = (int)Number(offsets, "props_slot_stride");
            SlotSize = (int)Number(offsets, "slot_size");
            SlotCount = (int)Number(offsets, "slot_count");
        }

        private static Dictionary<string, string> ParseSection(string path, string sectionName)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return values;

            bool inSection = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("["))
                {
                    inSection = string.Equals(line.Trim('[', ']').Trim(), sectionName, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                int separator = line.IndexOf('=');
                if (!inSection || separator < 1)
                    continue;

                string value = line.Substring(separator + 1).Trim();
                int comment = value.IndexOf('#');
                if (comment > 0)
                    value = value.Substring(0, comment).Trim();

                values[line.Substring(0, separator).Trim()] = value;
            }

            return values;
        }

        private static ulong Number(Dictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string raw))
                throw new InvalidOperationException($"Key '{key}' is missing in offsets.ini.");

            raw = raw.Trim();
            return raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt64(raw.Substring(2), 16)
                : ulong.Parse(raw);
        }
    }
}
