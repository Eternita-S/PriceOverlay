using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

//SimpleTweaksPlugin
//https://github.com/Caraxi/SimpleTweaksPlugin/
namespace PriceOverlay
{
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    public unsafe struct InventoryContainer
    {
        [FieldOffset(0x00)] public InventoryItem* Items;
        [FieldOffset(0x08)] public InventoryType Type;
        [FieldOffset(0x0C)] public int SlotCount;
        [FieldOffset(0x10)] public byte Loaded;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public unsafe struct InventoryItem
    {

        [FieldOffset(0x00)] public InventoryType Container;
        [FieldOffset(0x04)] public short Slot;
        [FieldOffset(0x08)] public uint ItemId;
        [FieldOffset(0x0C)] public uint Quantity;
        [FieldOffset(0x10)] public ushort Spiritbond;
        [FieldOffset(0x12)] public ushort Condition;
        [FieldOffset(0x14)] public ItemFlags Flags;
        [FieldOffset(0x20)] public ushort Materia0;
        [FieldOffset(0x22)] public ushort Materia1;
        [FieldOffset(0x24)] public ushort Materia2;
        [FieldOffset(0x26)] public ushort Materia3;
        [FieldOffset(0x28)] public ushort Materia4;
        [FieldOffset(0x2A)] public byte MateriaLevel0;
        [FieldOffset(0x2B)] public byte MateriaLevel1;
        [FieldOffset(0x2C)] public byte MateriaLevel2;
        [FieldOffset(0x2D)] public byte MateriaLevel3;
        [FieldOffset(0x2E)] public byte MateriaLevel4;
        [FieldOffset(0x2F)] public byte Stain;
        [FieldOffset(0x30)] public uint GlamourId;
        public bool IsHQ => (Flags & ItemFlags.HQ) == ItemFlags.HQ;

        public IEnumerable<(ushort materiaId, byte level)> Materia()
        {
            if (Materia0 != 0) yield return (Materia0, MateriaLevel0); else yield break;
            if (Materia1 != 0) yield return (Materia1, MateriaLevel1); else yield break;
            if (Materia2 != 0) yield return (Materia2, MateriaLevel2); else yield break;
            if (Materia3 != 0) yield return (Materia3, MateriaLevel3); else yield break;
            if (Materia4 != 0) yield return (Materia4, MateriaLevel4);
        }

        public Item GetItem(DalamudPluginInterface pi)
        {
            return pi.Data.Excel.GetSheet<Item>().GetRow(this.ItemId);
        }
    }

    [Flags]
    public enum ItemFlags : byte
    {
        None,
        HQ,
    }

    public enum InventoryType
    {
        Bag0 = 0,
        Bag1 = 1,
        Bag2 = 2,
        Bag3 = 3,

        GearSet0 = 1000,
        GearSet1 = 1001,

        Currency = 2000,
        Crystal = 2001,
        Mail = 2003,
        KeyItem = 2004,
        HandIn = 2005,
        DamagedGear = 2007,
        UNKNOWN_2008 = 2008,
        Examine = 2009,

        ArmoryOff = 3200,
        ArmoryHead = 3201,
        ArmoryBody = 3202,
        ArmoryHand = 3203,
        ArmoryWaist = 3204,
        ArmoryLegs = 3205,
        ArmoryFeet = 3206,
        ArmoryEar = 3207,
        ArmoryNeck = 3208,
        ArmoryWrist = 3209,
        ArmoryRing = 3300,

        ArmorySoulCrystal = 3400,
        ArmoryMain = 3500,

        SaddleBag0 = 4000,
        SaddleBag1 = 4001,
        PremiumSaddleBag0 = 4100,
        PremiumSaddleBag1 = 4101,

        RetainerBag0 = 10000,
        RetainerBag1 = 10001,
        RetainerBag2 = 10002,
        RetainerBag3 = 10003,
        RetainerBag4 = 10004,
        RetainerBag5 = 10005,
        RetainerBag6 = 10006,
        RetainerEquippedGear = 11000,
        RetainerGil = 12000,
        RetainerCrystal = 12001,
        RetainerMarket = 12002,

        FreeCompanyBag0 = 20000,
        FreeCompanyBag1 = 20001,
        FreeCompanyBag2 = 20002,
        FreeCompanyBag3 = 20003,
        FreeCompanyBag4 = 20004,
        FreeCompanyBag5 = 20005,
        FreeCompanyBag6 = 20006,
        FreeCompanyBag7 = 20007,
        FreeCompanyBag8 = 20008,
        FreeCompanyBag9 = 20009,
        FreeCompanyBag10 = 20010,
        FreeCompanyGil = 22000,
        FreeCompanyCrystal = 22001,

        HousingInteriorAppearance = 25002,

        HousingInteriorPlacedItems1 = 25003,
        HousingInteriorPlacedItems2 = 25004,
        HousingInteriorPlacedItems3 = 25005,
        HousingInteriorPlacedItems4 = 25006,
        HousingInteriorPlacedItems5 = 25007,
        HousingInteriorPlacedItems6 = 25008,
        HousingInteriorPlacedItems7 = 25009,
        HousingInteriorPlacedItems8 = 25010,

        HousingInteriorStoreroom1 = 27001,
        HousingInteriorStoreroom2 = 27002,
        HousingInteriorStoreroom3 = 27003,
        HousingInteriorStoreroom4 = 27004,
        HousingInteriorStoreroom5 = 27005,
        HousingInteriorStoreroom6 = 27006,
        HousingInteriorStoreroom7 = 27007,
        HousingInteriorStoreroom8 = 27008,

        HousingExteriorAppearance = 25000,
        HousingExteriorPlacedItems = 25001,
        HousingExteriorStoreroom = 27000,
    }

}
