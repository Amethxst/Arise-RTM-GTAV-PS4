using System;
using System.Collections.Generic;
using System.IO;

namespace Arise_RTM
{
    public sealed class GameProfile
    {
        private static readonly HashSet<string> ReservedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "address_base",
            "allowed_title_ids"
        };

        public string GameVersion { get; private set; }
        public ulong AddressBase { get; private set; }
        public Dictionary<string, ulong> Handlers { get; } = new Dictionary<string, ulong>(StringComparer.Ordinal);
        public HashSet<string> AllowedTitleIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static GameProfile Load(string path, string version)
        {
            Dictionary<string, string> section = ReadSection(path, version);
            if (section.Count == 0)
                throw new InvalidOperationException($"Section [{version}] not found in {Path.GetFileName(path)}.");

            GameProfile profile = new GameProfile
            {
                GameVersion = version,
                AddressBase = ParseNumber("address_base", Require(section, "address_base"))
            };

            foreach (KeyValuePair<string, string> entry in section)
            {
                if (!ReservedKeys.Contains(entry.Key))
                    profile.Handlers[entry.Key.ToLowerInvariant()] = ParseNumber(entry.Key, entry.Value);
            }

            foreach (string coreKey in HandlerKeys.Core)
            {
                if (!profile.Handlers.ContainsKey(coreKey))
                    throw new InvalidOperationException($"Handler '{coreKey}' is missing in profile.ini.");
            }

            foreach (string titleId in Require(section, "allowed_title_ids").Split(','))
            {
                string clean = titleId.Trim();
                if (clean.Length > 0)
                    profile.AllowedTitleIds.Add(clean);
            }

            return profile;
        }

        private static Dictionary<string, string> ReadSection(string path, string sectionName)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                throw new FileNotFoundException("profile.ini not found next to the executable.", path);

            bool inSection = false;
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
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

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                int comment = value.IndexOf('#');
                if (comment >= 0)
                    value = value.Substring(0, comment).Trim();

                values[key] = value;
            }

            return values;
        }

        private static string Require(Dictionary<string, string> section, string key)
        {
            if (!section.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Key '{key}' is missing in profile.ini.");

            return value;
        }

        private static ulong ParseNumber(string key, string raw)
        {
            raw = raw.Trim();
            try
            {
                return raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToUInt64(raw.Substring(2), 16)
                    : ulong.Parse(raw);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException($"Invalid number for '{key}' in profile.ini: '{raw}'.");
            }
        }
    }
}
