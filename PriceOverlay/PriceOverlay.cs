using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PriceOverlay
{
    unsafe class PriceOverlay : IDalamudPlugin
    {
        internal DalamudPluginInterface pi;
        internal HashSet<uint> pending;
        internal Dictionary<int, string> overlays;
        internal bool drawGui = false;
        internal IntPtr inventoryManager;
        internal delegate InventoryContainer* GetInventoryContainer(IntPtr inventoryManager, int inventoryId);
        internal delegate InventoryItem* GetContainerSlot(InventoryContainer* inventoryContainer, int slotId);
        internal GetInventoryContainer getInventoryContainer;
        internal GetContainerSlot getContainerSlot;
        internal ItemodrReader reader;
        internal HashSet<(float x, float y, string text, string id)> gui = new HashSet<(float x, float y, string text, string id)>();
        internal Dictionary<string, List<(int slotIndex, int containerIndex)>> order;
        internal (float x, float y, float w, float h) tooltiparea;
        internal SemaphoreSlim orderSemaphore;
        internal string itemodrPath;
        internal FileSystemWatcher itemodrWatcher;
        internal Dictionary<uint, (long minprice, long avgprice, long avghistory, long minhqprice, long avghqprice, long avghqhistory)> priceCache;
        public string Name => "PriceOverlay";

        public void Dispose()
        {
            pi.UiBuilder.OnBuildUi -= Draw;
            pi.Framework.OnUpdateEvent -= Tick;
            pi.CommandManager.RemoveHandler("/oprice");
            itemodrWatcher.EnableRaisingEvents = false;
            itemodrWatcher.Dispose();
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
            pi.ClientState.OnLogin += OnLogin;
            if (pi.ClientState.LocalPlayer != null) OnLogin();
        }

        void OnLogin(object _ = null, object __ = null)
        {
            priceCache = new Dictionary<uint, (long minprice, long avgprice, long avghistory, long minhqprice, long avghqprice, long avghqhistory)>();
            pending = new HashSet<uint>();
            overlays = new Dictionary<int, string>();
            itemodrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "FINAL FANTASY XIV - A Realm Reborn",
            $"FFXIV_CHR{pi.ClientState.LocalContentId:X16}");
            reader = new ItemodrReader(this);
            if (orderSemaphore != null) orderSemaphore.Dispose();
            orderSemaphore = new SemaphoreSlim(1);
            orderSemaphore.Wait();
            order = reader.ParseItemOrder();
            orderSemaphore.Release();
            if (itemodrWatcher != null)
            {
                itemodrWatcher.EnableRaisingEvents = false;
                itemodrWatcher.Dispose();
            }
            itemodrWatcher = new FileSystemWatcher(itemodrPath);
            itemodrWatcher.NotifyFilter = NotifyFilters.LastWrite;
            itemodrWatcher.Filter = "ITEMODR.DAT";
            itemodrWatcher.Changed += delegate
            {
                orderSemaphore.Wait();
                ParseOrDie(0);
                orderSemaphore.Release();
            };
            itemodrWatcher.EnableRaisingEvents = true;
        }

        void ParseOrDie(int num)
        {
            if(num > 100)
            {
                pi.Framework.Gui.Chat.Print(DateTimeOffset.Now.ToUnixTimeMilliseconds() + " Itemodr parsing failed hard");
                return;
            }
            try
            {
                order = reader.ParseItemOrder();
                pi.Framework.Gui.Chat.Print(DateTimeOffset.Now.ToUnixTimeMilliseconds() + " Itemodr reparsed");
            }
            catch (Exception)
            {
                pi.Framework.Gui.Chat.Print(DateTimeOffset.Now.ToUnixTimeMilliseconds() + " Failed to reparse iremodr");
                Thread.Sleep(50);
                ParseOrDie(++num);
            }
        }

        [HandleProcessCorruptedStateExceptions]
        void ProcessCommand(object _, object __)
        {
            try
            {
                var str = string.Join(",", pending);
                pi.Framework.Gui.Chat.Print(str);
                Task.Run(delegate {
                    var task = new HttpClient().GetAsync("https://universalis.app/api/Spriggan/" + str);
                    task.Wait(5000);
                    if (task.IsCompleted)
                    {
                        var task2 = task.Result.Content.ReadAsStringAsync();
                        task2.Wait();
                        pi.Framework.Gui.Chat.Print("Received response of "+ task2.Result.Length + " length");
                    }
                    else
                    {
                        task.Dispose();
                        pi.Framework.Gui.Chat.Print("Request timed out");
                    }
                });
            }
            catch (Exception e)
            {
                pi.Framework.Gui.Chat.Print(e.Message + "\n" + e.StackTrace);
            }
        }

        [HandleProcessCorruptedStateExceptions]
        void Tick(object _)
        {
            if (orderSemaphore.Wait(0))
            {
                drawGui = false;
                try
                {
                    var invlarge = pi.Framework.Gui.GetUiObjectByName("InventoryLarge", 1);
                    if (invlarge != IntPtr.Zero)
                    {
                        var invlargeAtk = (AtkUnitBase*)invlarge;
                        if (invlargeAtk->IsVisible)
                        {
                            var inv0 = pi.Framework.Gui.GetUiObjectByName("InventoryGrid0", 1);
                            var inv1 = pi.Framework.Gui.GetUiObjectByName("InventoryGrid1", 1);
                            gui.Clear();
                            var activeTab = ((AtkComponentNode*)invlargeAtk->UldManager.NodeList[69])->Component->UldManager.NodeList[2]->IsVisible ? 0 :
                                ((AtkComponentNode*)invlargeAtk->UldManager.NodeList[68])->Component->UldManager.NodeList[2]->IsVisible ? 1 :
                                -1;
                            if (activeTab != -1)
                            {
                                InventoryContainer*[] inv = 
                                {
                                    getInventoryContainer(inventoryManager, (int)InventoryType.Bag0),
                                    getInventoryContainer(inventoryManager, (int)InventoryType.Bag1),
                                    getInventoryContainer(inventoryManager, (int)InventoryType.Bag2),
                                    getInventoryContainer(inventoryManager, (int)InventoryType.Bag3)
                                };
                                pending.Clear();
                                foreach(var inventory in inv)
                                {
                                    for(var i = 0; i < inventory->SlotCount; i++)
                                    {
                                        var item = getContainerSlot(inventory, i);
                                        if (item->ItemId != 0 && !item->GetItem(pi).IsUntradable && !priceCache.ContainsKey(item->ItemId))
                                        {
                                            pending.Add(item->ItemId);
                                        }
                                    }
                                }
                                tooltiparea = (x: 0f, y: 0f, w: 0f, h: 0f);
                                if (!((AtkComponentNode*)invlargeAtk->UldManager.NodeList[1])->Component->UldManager.NodeList[3]->IsVisible)
                                {
                                    CheckObstructedArea("ItemSearch");
                                    CheckObstructedArea("Trade");
                                }
                                CheckObstructedArea("SelectYesno");
                                CheckObstructedArea("AddonContextSub");
                                CheckObstructedArea("ContextMenu");
                                CheckObstructedArea("ItemDetail");
                                if (inv0 != IntPtr.Zero && inv1 != IntPtr.Zero)
                                {
                                    if (activeTab == 0)
                                    {
                                        AddGuiOverlays((AtkUnitBase*)inv0, inv, 0);
                                        AddGuiOverlays((AtkUnitBase*)inv1, inv, 1);
                                    }
                                    else if (activeTab == 1)
                                    {
                                        AddGuiOverlays((AtkUnitBase*)inv0, inv, 2);
                                        AddGuiOverlays((AtkUnitBase*)inv1, inv, 3);
                                    }
                                }
                                drawGui = true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    pi.Framework.Gui.Chat.Print(e.Message + "\n" + e.StackTrace);
                }
                orderSemaphore.Release();
            }
        }

        void CheckObstructedArea(string addon)
        {
            var obj = pi.Framework.Gui.GetUiObjectByName(addon, 1);
            if (obj != IntPtr.Zero)
            {
                var atkobj = (AtkUnitBase*)obj;
                if (atkobj->IsVisible)
                {
                    tooltiparea.x = atkobj->X-20;
                    tooltiparea.y = atkobj->Y-15;
                    tooltiparea.w = atkobj->RootNode->Width * atkobj->RootNode->ScaleX + 40;
                    tooltiparea.h = atkobj->RootNode->Height * atkobj->RootNode->ScaleY + 30;
                }
            }
        }

        void AddGuiOverlays(AtkUnitBase* invnode, InventoryContainer*[] inv, int invIndex)
        {
            var basex = invnode->X + invnode->UldManager.NodeList[2]->X * invnode->Scale;
            var basey = invnode->Y + invnode->UldManager.NodeList[2]->Y * invnode->Scale;
            for (var n = 0; n < 35; n++)
            {
                var x = basex + (invnode->UldManager.NodeList[37 - n]->X + invnode->UldManager.NodeList[37 - n]->Width / 2) * invnode->Scale;
                var y = basey + invnode->UldManager.NodeList[37 - n]->Y * invnode->Scale;
                if (x > tooltiparea.x && x < tooltiparea.x + tooltiparea.w && y > tooltiparea.y && y < tooltiparea.y + tooltiparea.h) continue;
                var slot = getContainerSlot(inv[order["PlayerInventory"][n + 35 * invIndex].containerIndex], order["PlayerInventory"][n + 35 * invIndex].slotIndex);
                if (slot->ItemId == 0) continue;
                var text = new StringBuilder();
                text.Append(slot->ItemId);
                /*text.Append("\n");
                if (slot->GetItem(pi).IsUntradable)
                {
                    text.Append("UNTR");
                }
                else
                {
                    if (priceCache.ContainsKey(slot->ItemId))
                    {
                        text.Append(priceCache[slot->ItemId].minprice);
                    }
                    else
                    {
                        text.Append("???");
                    }
                }*/
                if (text.Length != 0)
                {
                    gui.Add((x, y, text.ToString(), (int)slot->Container + "@" + slot->Slot));
                }
            }
        }

        void Draw()
        {
            if (drawGui)
            {
                bool _ = true;
                foreach(var element in gui)
                {
                    var textsize = ImGui.CalcTextSize(element.text);
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(element.x - textsize.X/2 - 1, element.y + 5));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2, 0));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0, 0));
                    ImGui.Begin("PriceOverlayElement##" + element.id, ref _,
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
