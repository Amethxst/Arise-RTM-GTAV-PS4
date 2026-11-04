using System;
using System.Collections.Generic;
using System.Text;

namespace Arise_RTM
{
    /// <summary>Reads and writes the outfit slots (names and components) directly in the game's memory.</summary>
    public class GTAOutfits
    {
        private readonly PS4Connection connection;
        private int currentPid = -1;

        // Clothes field offsets (same for the Draw and Tex arrays)
        private const ulong FieldMask = 0x00;
        private const ulong FieldHair = 0x08;
        private const ulong FieldGloves = 0x10;
        private const ulong FieldLegs = 0x18;
        private const ulong FieldBag = 0x20;
        private const ulong FieldShoes = 0x28;
        private const ulong FieldMisc = 0x30;
        private const ulong FieldUndershirt = 0x38;
        private const ulong FieldArmor = 0x40;
        private const ulong FieldDecals = 0x48;
        private const ulong FieldTop = 0x50;

        // Props field offsets (same two-array structure)
        private const ulong PropHat = 0x00;
        private const ulong PropGlasses = 0x08;
        private const ulong PropEarring = 0x10;
        private const ulong PropWatches = 0x30;
        private const ulong PropBracelets = 0x38;

        public struct OutfitComponents
        {
            public int Mask;       public int MaskTex;
            public int Hair;       public int HairTex;
            public int Gloves;     public int GlovesTex;
            public int Legs;       public int LegsTex;
            public int Bag;        public int BagTex;
            public int Shoes;      public int ShoesTex;
            public int Misc;       public int MiscTex;
            public int Undershirt; public int UndershirtTex;
            public int Armor;      public int ArmorTex;
            public int Decals;     public int DecalsTex;
            public int Top;        public int TopTex;
            public int Hat;        public int HatTex;
            public int Glasses;    public int GlassesTex;
            public int Earring;    public int EarringTex;
            public int Watches;    public int WatchesTex;
            public int Bracelets;  public int BraceletsTex;
        }

        public GTAOutfits(PS4Connection connection)
        {
            this.connection = connection;
        }

        public void SetProcess(int pid)
        {
            currentPid = pid;
        }

        private ulong Base => OutfitOffsets.NamesBase;

        private int ReadInt32(ulong address)
        {
            byte[] data = connection.ReadMemory(currentPid, address, 4);
            return data != null && data.Length == 4 ? BitConverter.ToInt32(data, 0) : 0;
        }

        private void WriteInt32(ulong address, int value)
        {
            connection.WriteMemory(currentPid, address, BitConverter.GetBytes(value));
        }

        public List<string> ReadNames()
        {
            List<string> names = new List<string>();
            if (currentPid == -1)
                return names;

            int slotSize = OutfitOffsets.SlotSize;
            byte[] data = connection.ReadMemory(currentPid, Base, OutfitOffsets.SlotCount * slotSize);
            if (data == null)
                return names;

            for (int i = 0; i < OutfitOffsets.SlotCount; i++)
            {
                string name = Encoding.UTF8.GetString(data, i * slotSize, slotSize);
                int terminator = name.IndexOf('\0');
                names.Add(terminator >= 0 ? name.Substring(0, terminator) : name);
            }

            return names;
        }

        public void WriteName(int slotIndex, string newName)
        {
            if (currentPid == -1)
                return;

            int slotSize = OutfitOffsets.SlotSize;
            byte[] nameBytes = Encoding.UTF8.GetBytes(newName);
            byte[] payload = new byte[slotSize];
            Array.Copy(nameBytes, payload, Math.Min(nameBytes.Length, slotSize - 1));
            connection.WriteMemory(currentPid, Base + (ulong)(slotIndex * slotSize), payload);
        }

        public OutfitComponents ReadComponents(int slotIndex)
        {
            OutfitComponents c = new OutfitComponents();
            if (currentPid == -1)
                return c;

            GetBases(slotIndex, out ulong drawBase, out ulong texBase, out ulong propsDrawBase, out ulong propsTexBase);

            c.Mask = ReadInt32(drawBase + FieldMask);             c.MaskTex = ReadInt32(texBase + FieldMask);
            c.Hair = ReadInt32(drawBase + FieldHair);             c.HairTex = ReadInt32(texBase + FieldHair);
            c.Gloves = ReadInt32(drawBase + FieldGloves);         c.GlovesTex = ReadInt32(texBase + FieldGloves);
            c.Legs = ReadInt32(drawBase + FieldLegs);             c.LegsTex = ReadInt32(texBase + FieldLegs);
            c.Bag = ReadInt32(drawBase + FieldBag);               c.BagTex = ReadInt32(texBase + FieldBag);
            c.Shoes = ReadInt32(drawBase + FieldShoes);           c.ShoesTex = ReadInt32(texBase + FieldShoes);
            c.Misc = ReadInt32(drawBase + FieldMisc);             c.MiscTex = ReadInt32(texBase + FieldMisc);
            c.Undershirt = ReadInt32(drawBase + FieldUndershirt); c.UndershirtTex = ReadInt32(texBase + FieldUndershirt);
            c.Armor = ReadInt32(drawBase + FieldArmor);           c.ArmorTex = ReadInt32(texBase + FieldArmor);
            c.Decals = ReadInt32(drawBase + FieldDecals);         c.DecalsTex = ReadInt32(texBase + FieldDecals);
            c.Top = ReadInt32(drawBase + FieldTop);               c.TopTex = ReadInt32(texBase + FieldTop);

            c.Hat = ReadInt32(propsDrawBase + PropHat);           c.HatTex = ReadInt32(propsTexBase + PropHat);
            c.Glasses = ReadInt32(propsDrawBase + PropGlasses);   c.GlassesTex = ReadInt32(propsTexBase + PropGlasses);
            c.Earring = ReadInt32(propsDrawBase + PropEarring);   c.EarringTex = ReadInt32(propsTexBase + PropEarring);
            c.Watches = ReadInt32(propsDrawBase + PropWatches);   c.WatchesTex = ReadInt32(propsTexBase + PropWatches);
            c.Bracelets = ReadInt32(propsDrawBase + PropBracelets); c.BraceletsTex = ReadInt32(propsTexBase + PropBracelets);

            return c;
        }

        public void WriteComponents(int slotIndex, OutfitComponents c)
        {
            if (currentPid == -1)
                return;

            GetBases(slotIndex, out ulong drawBase, out ulong texBase, out ulong propsDrawBase, out ulong propsTexBase);

            WriteInt32(drawBase + FieldMask, c.Mask);             WriteInt32(texBase + FieldMask, c.MaskTex);
            WriteInt32(drawBase + FieldHair, c.Hair);             WriteInt32(texBase + FieldHair, c.HairTex);
            WriteInt32(drawBase + FieldGloves, c.Gloves);         WriteInt32(texBase + FieldGloves, c.GlovesTex);
            WriteInt32(drawBase + FieldLegs, c.Legs);             WriteInt32(texBase + FieldLegs, c.LegsTex);
            WriteInt32(drawBase + FieldBag, c.Bag);               WriteInt32(texBase + FieldBag, c.BagTex);
            WriteInt32(drawBase + FieldShoes, c.Shoes);           WriteInt32(texBase + FieldShoes, c.ShoesTex);
            WriteInt32(drawBase + FieldMisc, c.Misc);             WriteInt32(texBase + FieldMisc, c.MiscTex);
            WriteInt32(drawBase + FieldUndershirt, c.Undershirt); WriteInt32(texBase + FieldUndershirt, c.UndershirtTex);
            WriteInt32(drawBase + FieldArmor, c.Armor);           WriteInt32(texBase + FieldArmor, c.ArmorTex);
            WriteInt32(drawBase + FieldDecals, c.Decals);         WriteInt32(texBase + FieldDecals, c.DecalsTex);
            WriteInt32(drawBase + FieldTop, c.Top);               WriteInt32(texBase + FieldTop, c.TopTex);

            WriteInt32(propsDrawBase + PropHat, c.Hat);           WriteInt32(propsTexBase + PropHat, c.HatTex);
            WriteInt32(propsDrawBase + PropGlasses, c.Glasses);   WriteInt32(propsTexBase + PropGlasses, c.GlassesTex);
            WriteInt32(propsDrawBase + PropEarring, c.Earring);   WriteInt32(propsTexBase + PropEarring, c.EarringTex);
            WriteInt32(propsDrawBase + PropWatches, c.Watches);   WriteInt32(propsTexBase + PropWatches, c.WatchesTex);
            WriteInt32(propsDrawBase + PropBracelets, c.Bracelets); WriteInt32(propsTexBase + PropBracelets, c.BraceletsTex);
        }

        private void GetBases(int slotIndex, out ulong drawBase, out ulong texBase, out ulong propsDrawBase, out ulong propsTexBase)
        {
            drawBase = (Base - OutfitOffsets.ClothesDrawDelta) + (ulong)(slotIndex * OutfitOffsets.ClothesSlotStride);
            texBase = (Base - OutfitOffsets.ClothesTexDelta) + (ulong)(slotIndex * OutfitOffsets.ClothesSlotStride);
            propsDrawBase = (Base - OutfitOffsets.PropsDrawDelta) + (ulong)(slotIndex * OutfitOffsets.PropsSlotStride);
            propsTexBase = (Base - OutfitOffsets.PropsTexDelta) + (ulong)(slotIndex * OutfitOffsets.PropsSlotStride);
        }
    }
}
