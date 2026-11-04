using System;
using System.Collections.Generic;
using System.Threading;

namespace Arise_RTM
{
    public interface IRecoveryOption
    {
        string Name { get; }
        string Description { get; }

        /// <summary>Handlers the option cannot run without; the run is blocked if one is unavailable.</summary>
        IReadOnlyList<string> RequiredHandlers { get; }

        /// <summary>Handlers used when present; they only count for the first-run confirmation.</summary>
        IReadOnlyList<string> OptionalHandlers { get; }

        OptionResult Run(OptionContext context);
    }

    public abstract class OptionBase : IRecoveryOption
    {
        private static readonly string[] None = new string[0];

        public abstract string Name { get; }
        public abstract string Description { get; }
        public virtual IReadOnlyList<string> RequiredHandlers => None;
        public virtual IReadOnlyList<string> OptionalHandlers => None;

        public abstract OptionResult Run(OptionContext context);
    }

    public sealed class OptionResult
    {
        public int Written;
        public int Failed;
        public int Mismatched;
        public int Skipped;
        public bool SaveAttempted;
        public bool SaveSucceeded;

        public bool IsClean => Failed == 0 && Mismatched == 0 && Written > 0 && (!SaveAttempted || SaveSucceeded);

        /// <summary>Replaces the default summary (used by the teleport).</summary>
        public string Note;

        public string Summary => Note ?? $"written {Written}, skipped {Skipped}, failed {Failed}, mismatched {Mismatched}";
    }

    /// <summary>The engine, the selected character, and the Verify/Save toggles. The Write* helpers apply the toggles.</summary>
    public sealed class OptionContext
    {
        private const float FloatTolerance = 0.0001f;

        public StatEngine Engine { get; set; }
        public int Slot { get; set; }
        public bool Verify { get; set; }
        public bool Save { get; set; }
        public Action<string> Log { get; set; }
        public bool Verbose { get; set; } = true;
        public Action<int, int> Progress { get; set; }
        public CancellationToken Cancel { get; set; }

        public string CharacterStat(string suffix)
        {
            return $"MP{Slot}_{suffix}";
        }

        /// <summary>Reports progress and stops the run when Cancel was pressed.</summary>
        public void Tick(int done, int total)
        {
            Cancel.ThrowIfCancellationRequested();
            Progress?.Invoke(done, total);
        }

        public void WriteInt(OptionResult result, string statName, int value)
        {
            if (!Engine.SetInt(statName, value))
            {
                WriteAsRealType(result, statName, value);
                return;
            }

            result.Written++;
            if (!Verify)
            {
                Info($"{statName} = {value}");
                return;
            }

            bool read = Engine.TryGetInt(statName, out int actual);
            Report(result, statName, read && actual == value, read ? actual.ToString() : "unreadable", value.ToString());
        }

        public void WriteBool(OptionResult result, string statName, bool value)
        {
            if (!Engine.SetBool(statName, value))
            {
                Fail(result, statName, "bool");
                return;
            }

            result.Written++;
            if (!Verify)
            {
                Info($"{statName} = {value}");
                return;
            }

            bool read = Engine.TryGetBool(statName, out bool actual);
            Report(result, statName, read && actual == value, read ? actual.ToString() : "unreadable", value.ToString());
        }

        public void WriteFloat(OptionResult result, string statName, float value)
        {
            if (!Engine.SetFloat(statName, value))
            {
                Fail(result, statName, "float");
                return;
            }

            result.Written++;
            if (!Verify)
            {
                Info($"{statName} = {value}");
                return;
            }

            bool read = Engine.TryGetFloat(statName, out float actual);
            Report(result, statName, read && Math.Abs(actual - value) <= FloatTolerance, read ? actual.ToString() : "unreadable", value.ToString());
        }

        public void WritePackedBool(OptionResult result, int index, bool value)
        {
            string label = $"packed[{index}] slot {Slot}";
            Engine.SetPackedBool(index, value, Slot);
            result.Written++;

            if (!Verify)
            {
                Info($"{label} = {value}");
                return;
            }

            Engine.TryGetPackedBool(index, Slot, out bool actual);
            bool matches = actual == value;
            if (matches)
                Engine.ConfirmHandler(HandlerKeys.PackedSetBool);

            Report(result, label, matches, actual.ToString(), value.ToString());
        }

        public void WritePackedInt(OptionResult result, int index, int value)
        {
            Engine.SetPackedInt(index, value, Slot);
            result.Written++;
            Info($"packed[{index}] slot {Slot} = {value}");
        }

        public void SaveIfRequested(OptionResult result)
        {
            if (!Save || result.Written == 0)
                return;

            result.SaveAttempted = true;
            result.SaveSucceeded = Engine.SaveStats();
            Log?.Invoke(result.SaveSucceeded ? "Stat save requested." : "Stat save call returned false.");
        }

        private void Info(string message)
        {
            if (Verbose)
                Log?.Invoke(message);
        }

        /// <summary>The game refused an int write: ask it which type the stat really has and, for a float or bool stat, write that.</summary>
        private void WriteAsRealType(OptionResult result, string statName, int value)
        {
            StatKind kind = Engine.ProbeKind(statName);
            if (kind == StatKind.Float && Engine.SetFloat(statName, value))
            {
                result.Written++;
                Log?.Invoke($"{statName}: float stat, written as {value}.0 instead of an int.");
                return;
            }

            if (kind == StatKind.Bool && Engine.SetBool(statName, value != 0))
            {
                result.Written++;
                Log?.Invoke($"{statName}: bool stat, written as {value != 0} instead of an int.");
                return;
            }

            Fail(result, statName, "int", kind);
        }

        private void Fail(OptionResult result, string name, string requested, StatKind? known = null)
        {
            StatKind kind = known ?? Engine.ProbeKind(name);
            if (kind == StatKind.Unknown)
                result.Skipped++;
            else
                result.Failed++;

            string reason = kind == StatKind.Unknown ? "no such stat in this game build"
                : kind.ToString().Equals(requested, StringComparison.OrdinalIgnoreCase) ? $"it is a {requested} stat but the game refused the value"
                : $"it is a {kind.ToString().ToLowerInvariant()} stat, not {requested}";
            Log?.Invoke($"{name}: refused as {requested} ({reason}).");
        }

        private void Report(OptionResult result, string name, bool matches, string actual, string expected)
        {
            if (matches)
            {
                Info($"{name} = {expected} (verified)");
                return;
            }

            result.Mismatched++;
            Log?.Invoke($"{name}: read-back {actual}, expected {expected}.");
        }
    }
}
