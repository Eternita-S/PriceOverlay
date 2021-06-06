using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PriceOverlay
{
    unsafe class PriceOverlay : IDalamudPlugin
    {
        internal DalamudPluginInterface pi;
        internal Dictionary<ushort, long> prices;
        internal Dictionary<int, string> overlays;
        internal bool drawGui = false;
        internal IntPtr inventoryManager;
        internal delegate InventoryContainer* GetInventoryContainer(IntPtr inventoryManager, int inventoryId);
        internal delegate InventoryItem* GetContainerSlot(InventoryContainer* inventoryContainer, int slotId);
        internal GetInventoryContainer getInventoryContainer;
        internal GetContainerSlot getContainerSlot;
        internal ItemodrReader reader;
        internal HashSet<(float x, float y, string text)> gui = new HashSet<(float x, float y, string text)>();
        public string Name => "PriceOverlay";

        public void Dispose()
        {
            pi.UiBuilder.OnBuildUi -= Draw;
            pi.Framework.OnUpdateEvent -= Tick;
            pi.CommandManager.RemoveHandler("/oprice");
            pi.Dispose();
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            pi = pluginInterface;
            //SimpleTweaksPlugin
            //https://github.com/Caraxi/SimpleTweaksPlugin/
            inventoryManager = pi.TargetModuleScanner.GetStaticAddressFromSig("BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0");
            var getInventoryContainerPtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 55 BB");
            var getContainerSlotPtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 5B 0C");
            getInventoryContainer = Marshal.GetDelegateForFunctionPointer<GetInventoryContainer>(getInventoryContainerPtr);
            getContainerSlot = Marshal.GetDelegateForFunctionPointer<GetContainerSlot>(getContainerSlotPtr);
            pi.UiBuilder.OnBuildUi += Draw;
            pi.Framework.OnUpdateEvent += Tick;
            pi.CommandManager.AddHandler("/oprice", new CommandInfo(ProcessCommand));
            prices = new Dictionary<ushort, long>();
            overlays = new Dictionary<int, string>();
            reader = new ItemodrReader(this);
        }

        [HandleProcessCorruptedStateExceptions]
        void ProcessCommand(object _, object __)
        {
            try
            {
                //drawGui = true;
                InventoryContainer*[] inv = { getInventoryContainer(inventoryManager, (int)InventoryType.Bag0),
                    getInventoryContainer(inventoryManager, (int)InventoryType.Bag1),
                    getInventoryContainer(inventoryManager, (int)InventoryType.Bag2),
                    getInventoryContainer(inventoryManager, (int)InventoryType.Bag3)};
                var order = reader.ParseItemOrder();
                for (int i = 0; i < order["PlayerInventory"].Count; i++)
                {
                    var slot = getContainerSlot(inv[order["PlayerInventory"][i].containerIndex], order["PlayerInventory"][i].slotIndex);
                    //pi.Framework.Gui.Chat.Print(i + ": " + slot->GetItem(pi).Name);

                    //if (getContainerSlot(inv, i)->ItemId == 12669) pi.Framework.Gui.Chat.Print(i.ToString());
                }
                var inv0 = pi.Framework.Gui.GetUiObjectByName("InventoryGrid0", 1);
                var inv1 = pi.Framework.Gui.GetUiObjectByName("InventoryGrid1", 1);
                gui.Clear();
                if (inv0 != IntPtr.Zero && inv1 != IntPtr.Zero)
                {
                    var inv0node = (AtkUnitBase*)inv0;
                    var basex = inv0node->X + inv0node->UldManager.NodeList[2]->X * inv0node->Scale;
                    var basey = inv0node->Y + inv0node->UldManager.NodeList[2]->Y * inv0node->Scale;
                    for (var n = 0; n < 35; n++)
                    {
                        var slot = getContainerSlot(inv[order["PlayerInventory"][n].containerIndex], order["PlayerInventory"][n].slotIndex);
                        gui.Add((basex + (inv0node->UldManager.NodeList[37 - n]->X + inv0node->UldManager.NodeList[37 - n]->Width/2) * inv0node->Scale,
                            basey + inv0node->UldManager.NodeList[37 - n]->Y * inv0node->Scale,
                            slot->ItemId.ToString()));
                    }
                }
            }
            catch (Exception e)
            {
                pi.Framework.Gui.Chat.Print(e.Message + "\n" + e.StackTrace);
            }
        }

        [HandleProcessCorruptedStateExceptions]
        void Tick(object _)
        {
            try
            {
                var inv0 = pi.Framework.Gui.GetUiObjectByName("InventoryGrid0", 1);
                if (inv0 != IntPtr.Zero)
                {
                    var inv0atk = (AtkUnitBase*)inv0;
                    if (inv0atk->IsVisible)
                    {
                        drawGui = true;
                    }
                    else
                    {
                        drawGui = false;
                    }
                }
                else
                {
                    drawGui = false;
                }
            }
            catch(Exception e)
            {
                pi.Framework.Gui.Chat.Print(e.Message + "\n" + e.StackTrace);
            }
        }

        void Draw()
        {
            if (drawGui)
            {
                int counter = 0;
                bool _ = true;
                foreach(var element in gui)
                {
                    var textsize = ImGui.CalcTextSize(element.text);
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(element.x - textsize.X/2 - 1, element.y + 5));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2, 0));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0, 0));
                    ImGui.Begin("PriceOverlayElement##" + ++counter, ref _,
                        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar
                        | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
                        | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysUseWindowPadding);
                    ImGui.Text(element.text);
                    ImGui.End();
                    ImGui.PopStyleVar(2);
                }
            }
        }
    }
}
