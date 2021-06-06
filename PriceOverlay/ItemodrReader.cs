using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceOverlay
{
    class ItemodrReader
    {
        const byte XOR8 = 0x73;
        const ushort XOR16 = 0x7373;
        const uint XOR32 = 0x73737373;
        PriceOverlay p;

        public ItemodrReader(PriceOverlay p)
        {
            this.p = p;
        }

        void UnexpectedSizeError(int expected, int real)
        {
            throw new Exception("Incorrect Size - Expected " + expected + " but got " + real);
        }

        void UnexpectedIdentifierError(int expected, int real)
        {
            throw new Exception("Unexpected Identifier - Expected " + expected + " but got " + real);
        }

        (int slotIndex, int containerIntex) readSlot(FileStream reader)
        {
            var s = ReadUInt8(reader) ^ XOR8;
            if (s != 4) UnexpectedSizeError(4, s);
            return ( ReadUInt16(reader) ^ XOR16, ReadUInt16(reader) ^ XOR16 );
        }

        List<(int slotIndex, int containerIndex)> readInventory(FileStream reader)
        {
            var s = ReadUInt8(reader) ^ XOR8;
            if (s != 4) UnexpectedSizeError(4, s);
            var slotCount = ReadUInt32(reader) ^ XOR32;

            var inventory = new List<(int slotIndex, int containerIndex)>();
            for (var i = 0; i < slotCount; i++)
            {
                var x = ReadUInt8(reader) ^ XOR8;
                if (x != 0x69) UnexpectedIdentifierError(0x69, x);
                var slot = readSlot(reader);
                inventory.Add(slot);
            }

            return inventory;
        }

        string[] inventoryNames = {
            "PlayerInventory",
            "ArmouryMainHand",
            "ArmouryHead",
            "ArmouryBody",
            "ArmouryHands",
            "ArmouryWaist",
            "ArmouryLegs",
            "ArmouryFeet",
            "ArmouryOffHand",
            "ArmouryEars",
            "ArmouryNeck",
            "ArmouryWrists",
            "ArmouryRings",
            "ArmourySoulCrystal",
            "SaddleBagLeft",
            "SaddleBagRight",
        };

        public Dictionary<string, List<(int slotIndex, int containerIndex)>> ParseItemOrder()
        {
            using (FileStream reader = File.OpenRead(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "FINAL FANTASY XIV - A Realm Reborn",
            $"FFXIV_CHR{p.pi.ClientState.LocalContentId:X16}", "ITEMODR.DAT")))
            {
                Advance(reader, 16);
                Dictionary<string, List<(int slotIndex, int containerIndex)>> data = new Dictionary<string, List<(int slotIndex, int containerIndex)>>();

                Advance(reader, 1); // Unknown Byte, Appears to be the main inventory size, but that is 
                var inventoryIndex = 0;
                try
                {
                    while (true)
                    {

                        var identifier = ReadUInt8(reader) ^ XOR8;
                        switch (identifier)
                        {
                            case 0x56:
                                {
                                    // Unknown
                                    Advance(reader, ReadUInt8(reader) ^ XOR8);
                                    break;
                                }
                            case 0x6E:
                                {
                                    // Start of an inventory
                                    var inventory = readInventory(reader);

                                    var inventoryName = inventoryNames[inventoryIndex++];
                                    data[inventoryName] = inventory;

                                    break;
                                }

                            case 0x4E:
                                {
                                    return data; //idc about retainers for now
                                                 //var retainers = readRetainers(reader);
                                                 //data.Retainers = retainers;
                                                 //break;
                                }

                            case 0x73:
                                {
                                    return data;
                                }
                            default:
                                {
                                    throw new Exception("Unexpected Identifier: " + identifier);
                                }

                        }
                    }
                }
                catch (Exception e)
                {
                    p.pi.Framework.Gui.Chat.Print("[PriceOverlay error] " + e.Message + "\n" + e.StackTrace);
                }

                return data;
            }
        }

        byte ReadUInt8(FileStream reader)
        {
            return (byte)reader.ReadByte();
        }

        ushort ReadUInt16(FileStream reader) //why? because why not?
        {
            return BitConverter.ToUInt16(new byte[] { (byte)reader.ReadByte(), (byte)reader.ReadByte() }, 0);
        }

        uint ReadUInt32(FileStream reader)
        {
            return BitConverter.ToUInt32(new byte[] { 
                (byte)reader.ReadByte(), (byte)reader.ReadByte(), (byte)reader.ReadByte(), (byte)reader.ReadByte() }, 0);
        }

        void Advance(FileStream reader, int amount)
        {
            reader.Position += amount;
        }
    }
}
