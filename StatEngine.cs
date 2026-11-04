using System;
using System.Collections.Generic;
using System.Linq;
using libdebug;

namespace Arise_RTM
{
    public enum StatKind { Unknown, Int, Bool, Float }

    /// <summary>
    /// Calls the game's native handlers through the ps4debug RPC stub.
    /// A handler is void f(NativeArg_s*): one pointer to a context whose ArgValues (ctx+0x10)
    /// points to 8-byte argument slots. The return value is written to ReturnValue[0].
    /// Preflight is read-only; the RPC stub is installed on the first real call.
    /// </summary>
    public sealed class StatEngine
    {
        private const int SignatureLength = 16;
        private const int ScratchSize = 0x1000;
        private const int ProtExecute = 0x4;

        private const int ArgsOffset = 0x100;
        private const int OutputOffset = 0x200;
        private const int MaxNativeArgs = 8;

        private static readonly byte[] EntryPrologue = { 0x55, 0x48, 0x89, 0xE5 };

        private sealed class HandlerState
        {
            public string Key;
            public ulong Relative;
            public ulong Absolute;
            public bool IsCore;
            public bool Available;
            public bool Verified;
            public bool Succeeded;
            public byte[] Pending;
        }

        private readonly PS4Connection connection;
        private readonly GameProfile profile;
        private readonly SignatureStore signatures;
        private readonly Action<string> log;
        private readonly Dictionary<string, HandlerState> handlers = new Dictionary<string, HandlerState>(StringComparer.Ordinal);

        private int pid = -1;
        private string titleId;
        private ulong rpcStub;
        private ulong scratchBuffer;

        public bool IsReady { get; private set; }

        public StatEngine(PS4Connection connection, GameProfile profile, SignatureStore signatures, Action<string> log)
        {
            this.connection = connection;
            this.profile = profile;
            this.signatures = signatures;
            this.log = log;
        }

        public void Reset()
        {
            IsReady = false;
            pid = -1;
            titleId = null;
            rpcStub = 0;
            scratchBuffer = 0;
            handlers.Clear();
        }

        public bool IsAvailable(string key)
        {
            return handlers.TryGetValue(key, out HandlerState handler) && handler.Available;
        }

        public bool IsVerified(string key)
        {
            return handlers.TryGetValue(key, out HandlerState handler) && handler.Available && handler.Verified;
        }

        public void ConfirmHandler(string key)
        {
            if (handlers.TryGetValue(key, out HandlerState handler))
                handler.Succeeded = true;
        }

        public bool Preflight(int gamePid, string gameTitleId, out string failure)
        {
            Reset();
            failure = null;
            pid = gamePid;
            titleId = gameTitleId;

            ProcessMap map = connection.GetProcessMaps(pid);
            if (map == null || map.entries == null || map.entries.Length == 0)
            {
                failure = "Could not read the process memory map.";
                return false;
            }

            MemoryEntry image = FindMainImage(map);
            if (image != null)
                log($"Main image '{image.name}' starts at 0x{image.start:X} (profile address_base 0x{profile.AddressBase:X}).");

            foreach (KeyValuePair<string, ulong> pair in profile.Handlers)
            {
                HandlerState handler = new HandlerState
                {
                    Key = pair.Key,
                    Relative = pair.Value,
                    Absolute = profile.AddressBase + pair.Value,
                    IsCore = HandlerKeys.Core.Contains(pair.Key)
                };
                handlers[pair.Key] = handler;

                if (CheckHandler(map, handler, out string problem))
                    continue;

                if (handler.IsCore)
                {
                    failure = problem;
                    return false;
                }

                handler.Available = false;
                log("WARNING (optional handler disabled): " + problem);
            }

            IsReady = true;
            return true;
        }

        // ---- stat writes ----------------------------------------------------------------

        public bool SetInt(string statName, int value)
        {
            ulong result = CallNative(HandlerKeys.StatSetInt, Hash(statName), unchecked((uint)value), 1);
            return Succeeded(HandlerKeys.StatSetInt, result);
        }

        public bool SetBool(string statName, bool value)
        {
            ulong result = CallNative(HandlerKeys.StatSetBool, Hash(statName), value ? 1UL : 0UL, 1);
            return Succeeded(HandlerKeys.StatSetBool, result);
        }

        public bool SetFloat(string statName, float value)
        {
            ulong result = CallNative(HandlerKeys.StatSetFloat, Hash(statName), FloatBits(value), 1);
            return Succeeded(HandlerKeys.StatSetFloat, result);
        }

        public void SetPackedBool(int index, bool value, int slot)
        {
            CallNative(HandlerKeys.PackedSetBool, unchecked((uint)index), value ? 1UL : 0UL, unchecked((uint)slot));
        }

        public void SetPackedInt(int index, int value, int slot)
        {
            CallNative(HandlerKeys.PackedSetInt, unchecked((uint)index), unchecked((uint)value), unchecked((uint)slot));
        }

        /// <summary>Which type the game reports for a stat name, by trying the typed readers (read-only). Unknown: no reader accepted the name.</summary>
        public StatKind ProbeKind(string statName)
        {
            try
            {
                if (IsAvailable(HandlerKeys.StatGetFloat) && TryGetFloat(statName, out _))
                    return StatKind.Float;
                if (IsAvailable(HandlerKeys.StatGetBool) && TryGetBool(statName, out _))
                    return StatKind.Bool;
                if (IsAvailable(HandlerKeys.StatGetInt) && TryGetInt(statName, out _))
                    return StatKind.Int;
            }
            catch (InvalidOperationException)
            {
            }

            return StatKind.Unknown;
        }

        public bool SaveStats()
        {
            ulong result = CallNative(HandlerKeys.StatSave, 0, 0, 3, 0);
            return Succeeded(HandlerKeys.StatSave, result);
        }

        // ---- stat reads -----------------------------------------------------------------

        public bool TryGetInt(string statName, out int value)
        {
            bool ok = TryGetRaw(HandlerKeys.StatGetInt, statName, out uint raw);
            value = unchecked((int)raw);
            return ok;
        }

        public bool TryGetBool(string statName, out bool value)
        {
            bool ok = TryGetRaw(HandlerKeys.StatGetBool, statName, out uint raw);
            value = raw != 0;
            return ok;
        }

        public bool TryGetFloat(string statName, out float value)
        {
            bool ok = TryGetRaw(HandlerKeys.StatGetFloat, statName, out uint raw);
            value = BitConverter.ToSingle(BitConverter.GetBytes(raw), 0);
            return ok;
        }

        public bool TryGetPackedBool(int index, int slot, out bool value)
        {
            ulong result = CallNative(HandlerKeys.PackedGetBool, unchecked((uint)index), unchecked((uint)slot));
            handlers[HandlerKeys.PackedGetBool].Succeeded = true;
            value = (result & 0xFF) != 0;
            return true;
        }

        /// <summary>Read-only: copies game memory at an absolute address. Executes nothing.</summary>
        public byte[] ReadGameMemory(ulong address, int length)
        {
            return IsReady ? connection.ReadMemory(pid, address, length) : null;
        }

        /// <summary>Writes game memory at an absolute address (no game code runs). Only for structures whose layout was confirmed.</summary>
        public void WriteGameMemory(ulong address, byte[] data)
        {
            if (!IsReady)
                throw new InvalidOperationException("Preflight has not passed.");

            connection.WriteMemory(pid, address, data);
        }

        public int CommitSignatures()
        {
            int stored = 0;
            foreach (HandlerState handler in handlers.Values)
            {
                if (!handler.Available || !handler.Succeeded || handler.Pending == null)
                    continue;

                signatures.Save(titleId, handler.Key, handler.Pending);
                handler.Pending = null;
                handler.Verified = true;
                stored++;
            }

            if (stored > 0)
                log($"Signatures stored for {titleId}: {stored} handler(s).");

            return stored;
        }

        // ---- internals ------------------------------------------------------------------

        private static uint Hash(string statName)
        {
            return JenkinsHash.Joaat(statName);
        }

        private static ulong FloatBits(float value)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
        }

        private bool Succeeded(string key, ulong result)
        {
            bool ok = (result & 0xFF) != 0;
            if (ok)
                handlers[key].Succeeded = true;

            return ok;
        }

        private bool TryGetRaw(string key, string statName, out uint raw)
        {
            raw = 0;
            EnsureRpc();

            ulong outputAddress = scratchBuffer + OutputOffset;
            connection.WriteMemory(pid, outputAddress, new byte[8]);

            ulong result = CallNative(key, Hash(statName), outputAddress, 0xFFFFFFFFUL);
            if (!Succeeded(key, result))
                return false;

            byte[] data = connection.ReadMemory(pid, outputAddress, 4);
            if (data == null || data.Length < 4)
                return false;

            raw = BitConverter.ToUInt32(data, 0);
            return true;
        }

        private static MemoryEntry FindMainImage(ProcessMap map)
        {
            return map.entries
                .Where(e => e != null && e.name != null &&
                            (e.name.IndexOf("executable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             e.name.IndexOf("eboot", StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(e => e.start)
                .FirstOrDefault();
        }

        private bool CheckHandler(ProcessMap map, HandlerState handler, out string problem)
        {
            problem = null;

            MemoryEntry region = FindRegion(map, handler.Absolute);
            if (region == null)
            {
                problem = $"{handler.Key}: address 0x{handler.Absolute:X} is not mapped. Check address_base in profile.ini.";
                return false;
            }

            if ((region.prot & ProtExecute) == 0)
            {
                problem = $"{handler.Key}: address 0x{handler.Absolute:X} is not in an executable region ({region.name}).";
                return false;
            }

            byte[] code = connection.ReadMemory(pid, handler.Absolute, SignatureLength);
            if (code == null || code.Length < SignatureLength)
            {
                problem = $"{handler.Key}: could not read {SignatureLength} bytes at 0x{handler.Absolute:X}.";
                return false;
            }

            if (!code.Take(EntryPrologue.Length).SequenceEqual(EntryPrologue))
            {
                problem = $"{handler.Key}: 0x{handler.Absolute:X} is not a function entry (expected {ToHex(EntryPrologue)}, found {ToHex(code)}).\n" +
                          "Nothing was called. Check address_base in profile.ini.";
                return false;
            }

            string state;
            if (signatures.TryGet(titleId, handler.Key, out byte[] expected))
            {
                if (!expected.SequenceEqual(code))
                {
                    problem = $"{handler.Key}: bytes at 0x{handler.Absolute:X} differ from the stored signature.\n" +
                              $"Found:    {ToHex(code)}\nExpected: {ToHex(expected)}";
                    return false;
                }

                handler.Verified = true;
                state = "entry OK, signature matches";
            }
            else
            {
                handler.Pending = code;
                state = "entry OK, UNVERIFIED (no stored signature)";
            }

            handler.Available = true;
            log($"{handler.Key} @ 0x{handler.Absolute:X} [{region.name}] {ToHex(code)} - {state}");
            return true;
        }

        private static MemoryEntry FindRegion(ProcessMap map, ulong address)
        {
            return map.entries.FirstOrDefault(e => e != null && address >= e.start && address < e.end);
        }

        private void EnsureRpc()
        {
            if (!IsReady)
                throw new InvalidOperationException("Preflight has not passed.");

            if (rpcStub != 0)
                return;

            rpcStub = connection.InstallRpc(pid);
            if (rpcStub == 0)
                throw new InvalidOperationException("RPC install returned a null stub.");

            scratchBuffer = connection.AllocateMemory(pid, ScratchSize);
            if (scratchBuffer == 0)
                throw new InvalidOperationException("Could not allocate the scratch buffer in the game process.");

            log($"RPC stub at 0x{rpcStub:X}, scratch buffer at 0x{scratchBuffer:X}.");
        }

        private ulong CallNative(string key, params ulong[] args)
        {
            ExecuteHandler(key, args);

            byte[] returned = connection.ReadMemory(pid, scratchBuffer + ArgsOffset, 8);
            return returned != null && returned.Length == 8 ? BitConverter.ToUInt64(returned, 0) : 0;
        }

        private void ExecuteHandler(string key, ulong[] args)
        {
            EnsureRpc();
            if (args.Length > MaxNativeArgs)
                throw new ArgumentException("Too many native arguments.");

            if (!handlers.TryGetValue(key, out HandlerState handler) || !handler.Available)
                throw new InvalidOperationException($"Handler '{key}' is not available on this build.");

            ulong argsAddress = scratchBuffer + ArgsOffset;
            byte[] block = new byte[ArgsOffset + MaxNativeArgs * 8];

            // NativeArg_s: ReturnValue @0x00, ArgCount @0x08, ArgValues @0x10, ReadCount @0x18
            Buffer.BlockCopy(BitConverter.GetBytes(argsAddress), 0, block, 0x00, 8);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)args.Length), 0, block, 0x08, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(argsAddress), 0, block, 0x10, 8);

            for (int i = 0; i < args.Length; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(args[i]), 0, block, ArgsOffset + i * 8, 8);

            connection.WriteMemory(pid, scratchBuffer, block);
            connection.CallFunction(pid, rpcStub, handler.Absolute, scratchBuffer);
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", " ");
        }
    }
}
