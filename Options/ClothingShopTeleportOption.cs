using System;
using System.Globalization;

namespace Arise_RTM
{
    /// <summary>
    /// Moves the player ped to the clothing shop by writing its position in memory (the two copies the game keeps),
    /// without calling any game function: calling the teleport natives from the RPC thread crashed the game.
    /// It first checks that both copies agree, so a wrong layout is never written to. It changes no stats.
    /// The coordinates are the ones of the original tool's shop teleport.
    /// </summary>
    public static class ClothingShopTeleportOption
    {
        public const float ShopX = -814.4328f;
        public const float ShopY = -1085.9870f;
        public const float ShopZ = 10.5673f;
        private const float FeetClearance = 1f;
        private const float MaxCopyDifference = 0.5f;

        public static readonly string[] RequiredHandlerKeys = new string[0];
        public static readonly string[] OptionalHandlerKeys = new string[0];

        public static OptionResult Apply(OptionContext context)
        {
            StatEngine engine = context.Engine;

            if (!PlayerPedMemory.TryLocate(engine, out PlayerPedMemory.Location ped))
                return Stopped("The player ped was not found: be in a session with your character loaded.");

            context.Log?.Invoke($"Ped at 0x{ped.Ped:X}, transform at 0x{ped.Transform:X}.");

            if (!PlayerPedMemory.TryReadPosition(engine, ped.TransformPosition, out float tx, out float ty, out float tz) ||
                !PlayerPedMemory.TryReadPosition(engine, ped.PedPosition, out float px, out float py, out float pz))
                return Stopped("Could not read the current position: nothing was written.");

            context.Log?.Invoke(Format("Current position", tx, ty, tz) + " / " + Format("copy", px, py, pz));

            if (Math.Abs(tx - px) > MaxCopyDifference || Math.Abs(ty - py) > MaxCopyDifference || Math.Abs(tz - pz) > MaxCopyDifference)
                return Stopped("The two position copies disagree (layout differs on this build): nothing was written.");

            float z = ShopZ + FeetClearance;
            PlayerPedMemory.WritePosition(engine, ped.TransformPosition, ShopX, ShopY, z);
            PlayerPedMemory.WritePosition(engine, ped.PedPosition, ShopX, ShopY, z);

            if (PlayerPedMemory.TryReadPosition(engine, ped.TransformPosition, out float rx, out float ry, out float rz))
                context.Log?.Invoke(Format("Read back", rx, ry, rz));

            return new OptionResult { Written = 1, Note = "Teleported to the clothing shop (" + Format(null, ShopX, ShopY, z) + ")." };
        }

        private static OptionResult Stopped(string note)
        {
            return new OptionResult { Skipped = 1, Note = note };
        }

        private static string Format(string label, float x, float y, float z)
        {
            string coords = string.Format(CultureInfo.InvariantCulture, "{0:0.00}, {1:0.00}, {2:0.00}", x, y, z);
            return label == null ? coords : label + ": " + coords;
        }
    }
}
