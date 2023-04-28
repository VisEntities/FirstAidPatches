using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/*
v2.5.0 - Clearshot

fixed cookable item splitting
fixed auto fuel
fixed ui updating when disabled
fixed fuel and time calculations
fixed bugs with right clicking items into furnace
fixed FormatTime displaying long decimal points
clamp total stacks to 1 instead of 0

added support for multiple items
added support for splitting stack from rust item splitter ui
added support for multiple fuel slots
added config option for enabing/disabling furnace splitter per furnace
added config option for fuel multiplier per furnace
added config option for saving player data

dynamically generate oven config
dynamically generate initialStackOptions

notes:
before updating from 2.4.1 -> 2.5.0
    - unload FurnaceSplitter (oxide.unload furnacesplitter)
    - delete /oxide/data/FurnaceSplitter.json
    - optionally delete /oxide/config/FurnaceSplitter.json, but it should update with new config options automatically
*/

namespace Oxide.Plugins
{
    [Info("Furnace Splitter", "FastBurst", "2.5.1")]
    [Description("Splits up resources in furnaces automatically and shows useful furnace information")]
    public class FurnaceSplitter : RustPlugin
    {
        [PluginReference]
        private Plugin UIScaleManager;

        private class OvenSlot
        {
            /// <summary>The item in this slot. May be null.</summary>
            public Item Item;

            /// <summary>The slot position</summary>
            public int? Position;

            /// <summary>The slot's index in the itemList list.</summary>
            public int Index;

            /// <summary>How much should be added/removed from stack</summary>
            public int DeltaAmount;
        }

        public class OvenInfo
        {
            public float ETA;
            public float FuelNeeded;
        }

        private class StoredData
        {
            public Dictionary<ulong, PlayerOptions> AllPlayerOptions { get; private set; } = new Dictionary<ulong, PlayerOptions>();
        }

        private class PlayerOptions
        {
            public bool Enabled;
            public Dictionary<string, int> TotalStacks = new Dictionary<string, int>();
        }

        public enum MoveResult
        {
            Ok,
            SlotsFilled,
            NotEnoughSlots
        }

        private StoredData storedData = new StoredData();
        private Dictionary<ulong, PlayerOptions> allPlayerOptions => storedData.AllPlayerOptions;
        private Dictionary<string, int> initialStackOptions = new Dictionary<string, int>();
        private PluginConfig config;

        private const string permUse = "furnacesplitter.use";

        private readonly Dictionary<ulong, string> openUis = new Dictionary<ulong, string>();
        private readonly Dictionary<BaseOven, List<BasePlayer>> looters = new Dictionary<BaseOven, List<BasePlayer>>();
        private readonly Stack<BaseOven> queuedUiUpdates = new Stack<BaseOven>();

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private void OnServerInitialized()
        {
            var saveCfg = false;
            foreach (var prefab in GameManifest.Current.entities)
            {
                var gameObj = GameManager.server.FindPrefab(prefab);
                if (gameObj == null) continue;

                var oven = gameObj.GetComponent<BaseOven>();
                if (oven != null && oven.allowByproductCreation) // ignore pumpkins, lanterns, etc
                {
                    if (!initialStackOptions.ContainsKey(oven.ShortPrefabName))
                    {
                        //Puts($"Add oven [{oven.ShortPrefabName}] - fuelSlots: {oven.fuelSlots}, inputSlots: {oven.inputSlots}, outputSlots: {oven.outputSlots}");
                        initialStackOptions[oven.ShortPrefabName] = oven.inputSlots;
                    }

                    if (!config.ovens.ContainsKey(oven.ShortPrefabName))
                    {
                        config.ovens[oven.ShortPrefabName] = new PluginConfig.OvenConfig {
                            enabled = false, // global '*' is enabled by default, disable all oven configs
                            fuelMultiplier = 1.0f
                        };
                        saveCfg = true;
                    }
                }
            }

            if (saveCfg)
                SaveConfig();

            Unsubscribe(nameof(OnServerSave));
            if (config.savePlayerData)
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
                Subscribe(nameof(OnServerSave));
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DestroyUI(player);
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void SaveData()
        {
            if (!config.savePlayerData) return;

            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void InitPlayer(BasePlayer player)
        {
            if (!allPlayerOptions.ContainsKey(player.userID))
            {
                allPlayerOptions[player.userID] = new PlayerOptions
                {
                    Enabled = true,
                    TotalStacks = new Dictionary<string, int>()
                };
            }

            PlayerOptions options = allPlayerOptions[player.userID];

            foreach (var kv in initialStackOptions)
            {
                if (!options.TotalStacks.ContainsKey(kv.Key))
                    options.TotalStacks.Add(kv.Key, kv.Value);
            }
        }

        private void OnTick()
        {
            while (queuedUiUpdates.Count > 0)
            {
                BaseOven oven = queuedUiUpdates.Pop();

                if (!oven || oven.IsDestroyed)
                    continue;

                OvenInfo ovenInfo = GetOvenInfo(oven);

                GetLooters(oven)?.ForEach(player =>
                {
                    if (player != null && !player.IsDestroyed && HasPermission(player) && GetEnabled(player))
                    {
                        CreateUi(player, oven, ovenInfo);
                    }
                });
            }
        }

        public OvenInfo GetOvenInfo(BaseOven oven)
        {
            OvenInfo result = new OvenInfo();
            PluginConfig.OvenConfig ovenCfg = GetOvenConfig(oven.ShortPrefabName);
            float fuelMultiplier = ovenCfg != null ? ovenCfg.fuelMultiplier : 1.0f;
            float ETA = GetTotalSmeltTime(oven) / oven.smeltSpeed;
            float fuelUnits = oven.fuelType.GetComponent<ItemModBurnable>().fuelAmount;
            float neededFuel = (float)Math.Ceiling(ETA * (oven.cookingTemperature / 200.0f) / fuelUnits);

            result.FuelNeeded = neededFuel * fuelMultiplier;
            result.ETA = ETA;

            return result;
        }

        private void Unload()
        {
            SaveData();

            foreach (var kv in openUis.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                BasePlayer player = BasePlayer.FindByID(kv.Key);
                DestroyUI(player);
            }
        }

        private bool GetEnabled(BasePlayer player)
        {
            if (!allPlayerOptions.ContainsKey(player.userID))
                InitPlayer(player);
            
            return allPlayerOptions[player.userID].Enabled;
        }

        private void SetEnabled(BasePlayer player, bool enabled)
        {
            if (allPlayerOptions.ContainsKey(player.userID))
                allPlayerOptions[player.userID].Enabled = enabled;
            CreateUiIfFurnaceOpen(player);
        }

        private bool IsSlotCompatible(Item item, BaseOven oven, ItemDefinition itemDefinition)
        {
            ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();

            if (item.amount < item.info.stackable && item.info == itemDefinition)
                return true;

            if (oven.allowByproductCreation && oven.fuelType.GetComponent<ItemModBurnable>().byproductItem == item.info)
                return true;

            if (cookable == null || cookable.becomeOnCooked == itemDefinition)
                return true;

            if (CanCook(cookable, oven))
                return true;

            return false;
        }

        private void OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (IsOvenCompatible(oven))
                queuedUiUpdates.Push(oven);
        }

        private List<BasePlayer> GetLooters(BaseOven oven)
        {
            if (looters.ContainsKey(oven))
                return looters[oven];

            return null;
        }

        private void AddLooter(BaseOven oven, BasePlayer player)
        {
            if (!looters.ContainsKey(oven))
                looters[oven] = new List<BasePlayer>();

            var list = looters[oven];
            list.Add(player);
        }

        private void RemoveLooter(BaseOven oven, BasePlayer player)
        {
            if (!looters.ContainsKey(oven))
                return;

            looters[oven].Remove(player);
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainerId, int targetSlotIndex, int splitAmount)
        {
            if (item == null || inventory == null)
                return null;

            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            BaseOven oven = inventory.loot.entitySource as BaseOven;
            if (oven == null)
                return null;

            ItemContainer targetContainer = inventory.FindContainer(targetContainerId);
            if (targetContainer != null && !(targetContainer?.entityOwner is BaseOven))
                return null; // ignore moving items within player inventory or container that is not an oven

            ItemContainer container = oven.inventory;
            ItemContainer originalContainer = item.GetRootContainer();
            if (container == null || originalContainer == null || originalContainer?.entityOwner is BaseOven)
                return null; // ignore invalid container or moving items within oven

            BaseOven.MinMax? allowedSlots = oven.GetAllowedSlots(item);
            if (allowedSlots == null)
                return null;

            for (int i = allowedSlots.Value.Min; i <= allowedSlots.Value.Max; i++)
            {
                Item slot = oven.inventory.GetSlot(i);
                if (slot != null && slot.info.shortname != item.info.shortname)
                    return null; // ignore splitting for different item types but allow item to be added to oven
            }

            Func<object> splitFunc = () =>
            {
                if (player == null || !HasPermission(player) || !GetEnabled(player))
                    return null;

                PlayerOptions playerOptions = allPlayerOptions[player.userID];

                if (container == null || originalContainer == null || container == item.GetRootContainer())
                    return null;

                ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();

                if (oven == null || cookable == null || oven.IsOutputItem(item))
                    return null;

                int totalSlots = oven.inputSlots;
                if (playerOptions.TotalStacks.ContainsKey(oven.ShortPrefabName))
                {
                    totalSlots = playerOptions.TotalStacks[oven.ShortPrefabName];
                }

                if (cookable.lowTemp > oven.cookingTemperature || cookable.highTemp < oven.cookingTemperature)
                    return null;

                MoveSplitItem(item, oven, totalSlots, splitAmount);
                return true;
            };

            object returnValue = splitFunc();

            if (HasPermission(player) && GetEnabled(player))
            {
                if (oven != null && IsOvenCompatible(oven))
                {
                    if (returnValue is bool && (bool)returnValue)
                        AutoAddFuel(inventory, oven);

                    queuedUiUpdates.Push(oven);
                }
            }

            return returnValue;
        }

        private MoveResult MoveSplitItem(Item item, BaseOven oven, int totalSlots, int splitAmount)
        {
            ItemContainer container = oven.inventory;
            int numOreSlots = totalSlots;
            int totalMoved = 0;
            int itemAmount = item.amount > splitAmount ? splitAmount : item.amount;
            int totalAmount = Math.Min(itemAmount + container.itemList.Where(slotItem => slotItem.info == item.info).Take(numOreSlots).Sum(slotItem => slotItem.amount), Math.Abs(item.info.stackable * numOreSlots));

            if (numOreSlots <= 0)
            {
                return MoveResult.NotEnoughSlots;
            }

            //Puts("---------------------------");

            int totalStackSize = Math.Min(totalAmount / numOreSlots, item.info.stackable);
            int remaining = totalAmount - totalAmount / numOreSlots * numOreSlots;

            List<int> addedSlots = new List<int>();

            //Puts("total: {0}, remaining: {1}, totalStackSize: {2}", totalAmount, remaining, totalStackSize);

            List<OvenSlot> ovenSlots = new List<OvenSlot>();

            for (int i = 0; i < numOreSlots; ++i)
            {
                Item existingItem;
                int slot = FindMatchingSlotIndex(oven, container, out existingItem, item.info, addedSlots);

                if (slot == -1) // full
                {
                    return MoveResult.NotEnoughSlots;
                }

                addedSlots.Add(slot);

                OvenSlot ovenSlot = new OvenSlot
                {
                    Position = existingItem?.position,
                    Index = slot,
                    Item = existingItem
                };

                int currentAmount = existingItem?.amount ?? 0;
                int missingAmount = totalStackSize - currentAmount + (i < remaining ? 1 : 0);
                ovenSlot.DeltaAmount = missingAmount;

                //Puts("[{0}] current: {1}, delta: {2}, total: {3}", slot, currentAmount, ovenSlot.DeltaAmount, currentAmount + missingAmount);

                if (currentAmount + missingAmount <= 0)
                    continue;

                ovenSlots.Add(ovenSlot);
            }

            foreach (OvenSlot slot in ovenSlots)
            {
                if (slot.Item == null)
                {
                    Item newItem = ItemManager.Create(item.info, slot.DeltaAmount, item.skin);
                    slot.Item = newItem;
                    newItem.MoveToContainer(container, slot.Position ?? slot.Index);
                }
                else
                {
                    slot.Item.amount += slot.DeltaAmount;
                }

                totalMoved += slot.DeltaAmount;
            }

            container.MarkDirty();

            if (totalMoved >= item.amount)
            {
                item.Remove();
                item.GetRootContainer()?.MarkDirty();
                return MoveResult.Ok;
            }
            else
            {
                item.amount -= totalMoved;
                item.GetRootContainer()?.MarkDirty();
                return MoveResult.SlotsFilled;
            }
        }

        private void AutoAddFuel(PlayerInventory playerInventory, BaseOven oven)
        {
            int neededFuel = (int)Math.Ceiling(GetOvenInfo(oven).FuelNeeded);
            neededFuel -= oven.inventory.GetAmount(oven.fuelType.itemid, false);
            var playerFuel = playerInventory.FindItemIDs(oven.fuelType.itemid);
            int fuelSlotIndex = 0;

            if (neededFuel <= 0 || playerFuel.Count <= 0)
                return;

            foreach (Item fuelItem in playerFuel)
            {
                var existingFuel = oven.inventory.GetSlot(fuelSlotIndex);
                if (existingFuel != null && existingFuel.amount >= existingFuel.info.stackable) // fuel slot full
                {
                    if (fuelSlotIndex < oven.fuelSlots) // check fuel slots
                        fuelSlotIndex++; // move to next fuel slot
                    else
                        break; // break if no fuel slots available
                }

                Item largestFuelStack = oven.inventory.itemList.Where(item => item.info == oven.fuelType).OrderByDescending(item => item.amount).FirstOrDefault();
                int toTake = Math.Min(neededFuel, (oven.fuelType.stackable * oven.fuelSlots) - (largestFuelStack?.amount ?? 0));

                if (toTake > fuelItem.amount)
                    toTake = fuelItem.amount;

                if (toTake <= 0)
                    break;

                neededFuel -= toTake;

                int currentFuelAmount = oven.inventory.GetAmount(oven.fuelType.itemid, false);
                if (currentFuelAmount >= oven.fuelType.stackable * oven.fuelSlots)
                    break; // Break if oven is full

                if (toTake >= fuelItem.amount)
                {
                    fuelItem.MoveToContainer(oven.inventory, fuelSlotIndex);
                }
                else
                {
                    Item splitItem = fuelItem.SplitItem(toTake);
                    if (!splitItem.MoveToContainer(oven.inventory, fuelSlotIndex)) // Break if oven is full
                        break;
                }

                if (neededFuel <= 0)
                    break;
            }
        }

        private int FindMatchingSlotIndex(BaseOven oven, ItemContainer container, out Item existingItem, ItemDefinition itemType, List<int> indexBlacklist)
        {
            existingItem = null;
            int firstIndex = -1;
            int inputSlotsMin = oven._inputSlotIndex;
            int inputSlotsMax = oven._inputSlotIndex + oven.inputSlots;
            Dictionary<int, Item> existingItems = new Dictionary<int, Item>();

            for (int i = inputSlotsMin; i < inputSlotsMax; ++i)
            {
                if (indexBlacklist.Contains(i))
                    continue;

                Item itemSlot = container.GetSlot(i);
                if (itemSlot == null || itemType != null && itemSlot.info == itemType)
                {
                    if (itemSlot != null)
                        existingItems.Add(i, itemSlot);

                    if (firstIndex == -1)
                    {
                        existingItem = itemSlot;
                        firstIndex = i;
                    }
                }
            }

            if (existingItems.Count <= 0 && firstIndex != -1)
            {
                return firstIndex;
            }
            else if (existingItems.Count > 0)
            {
                var largestStackItem = existingItems.OrderByDescending(kv => kv.Value.amount).First();
                existingItem = largestStackItem.Value;
                return existingItem.position;
            }

            existingItem = null;
            return -1;
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            BaseOven oven = entity as BaseOven;

            if (oven == null || !HasPermission(player) || !IsOvenCompatible(oven))
                return;

            AddLooter(oven, player);
            if (GetEnabled(player))
                queuedUiUpdates.Push(oven); // queue ui updates
            else
                CreateUi(player, oven, new OvenInfo()); // create ui without updates
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            BaseOven oven = entity as BaseOven;

            if (oven == null || !IsOvenCompatible(oven))
                return;

            DestroyUI(player);
            RemoveLooter(oven, player);
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            BaseOven oven = networkable as BaseOven;

            if (oven != null)
            {
                DestroyOvenUI(oven);
            }
        }

        private void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (IsOvenCompatible(oven))
                queuedUiUpdates.Push(oven);
        }

        private void CreateUiIfFurnaceOpen(BasePlayer player)
        {
            BaseOven oven = player.inventory.loot?.entitySource as BaseOven;

            if (oven != null && IsOvenCompatible(oven))
                queuedUiUpdates.Push(oven);
        }

        private CuiElementContainer CreateUi(BasePlayer player, BaseOven oven, OvenInfo ovenInfo)
        {
            PlayerOptions options = allPlayerOptions[player.userID];
            int totalSlots = GetTotalStacksOption(player, oven) ?? oven.inputSlots;
            string remainingTimeStr;
            string neededFuelStr;

            if (ovenInfo.ETA <= 0)
            {
                remainingTimeStr = "0s";
                neededFuelStr = "0";
            }
            else
            {
                remainingTimeStr = FormatTime(ovenInfo.ETA);
                neededFuelStr = ovenInfo.FuelNeeded.ToString("##,###");
            }

            float uiScale = 1.0f;
            float[] playerUiInfo = UIScaleManager?.Call<float[]>("API_CheckPlayerUIInfo", player.UserIDString);
            if (playerUiInfo?.Length > 0)
            {
                uiScale = playerUiInfo[2];
            }
            string contentColor = "0.7 0.7 0.7 1.0";
            int contentSize = Convert.ToInt32(10 * uiScale);
            string toggleStateStr = (!options.Enabled).ToString();
            string toggleButtonColor = !options.Enabled
                    ? "0.415 0.5 0.258 0.4"
                    : "0.8 0.254 0.254 0.4";
            string toggleButtonTextColor = !options.Enabled
                    ? "0.607 0.705 0.431"
                    : "0.705 0.607 0.431";
            string buttonColor = "0.75 0.75 0.75 0.1";
            string buttonTextColor = "0.77 0.68 0.68 1";

            int nextDecrementSlot = totalSlots - 1;
            int nextIncrementSlot = totalSlots + 1;

            DestroyUI(player);

            Vector2 uiPosition = new Vector2(
                ((((config.UiPosition.x) - 0.5f) * uiScale) + 0.5f),
                (config.UiPosition.y - 0.02f) + 0.02f * uiScale);
            Vector2 uiSize = new Vector2(0.1785f * uiScale, 0.111f * uiScale);

            CuiElementContainer result = new CuiElementContainer();
            string rootPanelName = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = uiPosition.x + " " + uiPosition.y,
                    AnchorMax = uiPosition.x + uiSize.x + " " + (uiPosition.y + uiSize.y)
                    //AnchorMin = "0.6505 0.022",
                    //AnchorMax = "0.829 0.133"
                }
            }, "Hud.Menu");

            string headerPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0.75 0.75 0.75 0.1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0.775",
                    AnchorMax = "1 1"
                }
            }, rootPanelName);

            // Header label
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.051 0",
                    AnchorMax = "1 0.95"
                },
                Text =
                {
                    Text = lang.GetMessage("title", this, player.UserIDString),
                    Align = TextAnchor.MiddleLeft,
                    Color = "0.77 0.7 0.7 1",
                    FontSize = Convert.ToInt32(13 * uiScale)
                }
            }, headerPanel);

            string contentPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0.65 0.65 0.65 0.06"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.74"
                }
            }, rootPanelName);

            // ETA label
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.02 0.7",
                    AnchorMax = "0.98 1"
                },
                Text =
                {
                    Text = string.Format("{0}: " + (ovenInfo.ETA > 0 ? "~" : "") + remainingTimeStr + " (" + neededFuelStr +  " " + oven.fuelType.displayName.english.ToLower() + ")", lang.GetMessage("eta", this, player.UserIDString)),
                    Align = TextAnchor.MiddleLeft,
                    Color = contentColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            // Toggle button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.02 0.4",
                    AnchorMax = "0.25 0.7"
                },
                Button =
                {
                    Command = "furnacesplitter.enabled " + toggleStateStr,
                    Color = toggleButtonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = options.Enabled ? lang.GetMessage("turnoff", this, player.UserIDString) : lang.GetMessage("turnon", this, player.UserIDString),
                    Color = toggleButtonTextColor,
                    FontSize = Convert.ToInt32(11 * uiScale)
                }
            }, contentPanel);

            // Trim button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.27 0.4",
                    AnchorMax = "0.52 0.7"
                },
                Button =
                {
                    Command = "furnacesplitter.trim",
                    Color = buttonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = lang.GetMessage("trim", this, player.UserIDString),
                    Color = contentColor,
                    FontSize = Convert.ToInt32(11 * uiScale)
                }
            }, contentPanel);

            // Decrease stack button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.02 0.05",
                    AnchorMax = "0.07 0.35"
                },
                Button =
                {
                    Command = "furnacesplitter.totalstacks " + nextDecrementSlot,
                    Color = buttonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = "<",
                    Color = buttonTextColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            // Empty slots label
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.08 0.05",
                    AnchorMax = "0.19 0.35"
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = totalSlots.ToString(),
                    Color = contentColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            // Increase stack button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.19 0.05",
                    AnchorMax = "0.25 0.35"
                },
                Button =
                {
                    Command = "furnacesplitter.totalstacks " + nextIncrementSlot,
                    Color = buttonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = ">",
                    Color = buttonTextColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            // Stack itemType label
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.27 0.05",
                    AnchorMax = "1 0.35"
                },
                Text =
                {
                    Align = TextAnchor.MiddleLeft,
                    Text = string.Format("({0})", lang.GetMessage("totalstacks", this, player.UserIDString)),
                    Color = contentColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            openUis.Add(player.userID, rootPanelName);
            CuiHelper.AddUi(player, result);
            return result;
        }

        private string FormatTime(float totalSeconds)
        {
            int hours = (int)Math.Floor(totalSeconds / 3600);
            int minutes = (int)Math.Floor(totalSeconds / 60 % 60);
            int seconds = (int)Math.Floor(totalSeconds % 60);

            if (hours <= 0 && minutes <= 0)
                return seconds + "s";
            if (hours <= 0)
                return minutes + "m" + seconds + "s";
            return hours + "h" + minutes + "m" + seconds + "s";
        }

        private float GetTotalSmeltTime(BaseOven oven)
        {
            float ETA = 0f;
            for (int i = oven._inputSlotIndex; i < oven._inputSlotIndex + oven.inputSlots; i++)
            {
                Item inputItem = oven.inventory.GetSlot(i);
                if (inputItem == null) continue;

                ItemModCookable cookable = inputItem.info.GetComponent<ItemModCookable>();
                if (cookable == null) continue;

                ETA += GetSmeltTime(cookable, inputItem.amount);
            }
            return ETA;
        }

        private bool CanCook(ItemModCookable cookable, BaseOven oven)
        {
            return oven.cookingTemperature >= cookable.lowTemp && oven.cookingTemperature <= cookable.highTemp;
        }

        private float GetSmeltTime(ItemModCookable cookable, int amount)
        {
            float smeltTime = cookable.cookTime * amount;
            return smeltTime;
        }

        private int? GetTotalStacksOption(BasePlayer player, BaseOven oven)
        {
            PlayerOptions options = allPlayerOptions[player.userID];

            if (options.TotalStacks.ContainsKey(oven.ShortPrefabName))
                return options.TotalStacks[oven.ShortPrefabName];

            return null;
        }

        private void DestroyUI(BasePlayer player)
        {
            if (!openUis.ContainsKey(player.userID))
                return;

            string uiName = openUis[player.userID];

            if (openUis.Remove(player.userID))
                CuiHelper.DestroyUi(player, uiName);
        }

        private void DestroyOvenUI(BaseOven oven)
        {
            if (oven == null) throw new ArgumentNullException(nameof(oven));

            foreach (KeyValuePair<ulong, string> kv in openUis.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                BasePlayer player = BasePlayer.FindByID(kv.Key);

                BaseOven playerLootOven = player.inventory.loot?.entitySource as BaseOven;

                if (playerLootOven != null && oven == playerLootOven)
                {
                    DestroyUI(player);
                    RemoveLooter(oven, player);
                }
            }
        }

        private PluginConfig.OvenConfig GetOvenConfig(string ovenShortname)
        {
            PluginConfig.OvenConfig ovenCfg;
            if (config.ovens.TryGetValue(ovenShortname, out ovenCfg) && ovenCfg.enabled)
                return ovenCfg;

            if (config.ovens.TryGetValue("*", out ovenCfg) && ovenCfg.enabled)
                return ovenCfg;

            return null;
        }

        private bool IsOvenCompatible(BaseOven oven)
        {
            if (oven == null || !oven.allowByproductCreation)
                return false;

            return GetOvenConfig(oven.ShortPrefabName) != null;
        }

        [ChatCommand("fs")]
        void cmdToggle(BasePlayer player, string cmd, string[] args)
        {
            if (!HasPermission(player))
            {
                player.ConsoleMessage(lang.GetMessage("nopermission", this, player.UserIDString));
                return;
            }

            var status = string.Empty;
            var statuson = lang.GetMessage("StatusONColor", this, player.UserIDString);
            var statusoff = lang.GetMessage("StatusOFFColor", this, player.UserIDString);
            if (args.Length == 0)
            {
                if (GetEnabled(player)) status = statuson;
                else status = statusoff;

                var helpmsg = new StringBuilder();
                helpmsg.Append("<size=22><color=green>Furnace Splitter</color></size> by: FastBurst\n");
                helpmsg.Append(lang.GetMessage("StatusMessage", this, player.UserIDString) + status + "\n");
                helpmsg.Append("<color=orange>/fs on</color> - Toggles Furnace Splitter to ON\n");
                helpmsg.Append("<color=orange>/fs off</color> - Toggles Furnace Splitter to OFF\n");
                player.ChatMessage(helpmsg.ToString().TrimEnd());
                return;
            }

            switch (args[0].ToLower())
            {
                case "on":
                    SetEnabled(player, true);
                    CreateUiIfFurnaceOpen(player);

                    if (GetEnabled(player)) status = statuson;
                    else status = statusoff;

                    player.ChatMessage(lang.GetMessage("StatusMessage", this, player.UserIDString) + status);
                    break;
                case "off":
                    SetEnabled(player, false);
                    DestroyUI(player);

                    if (GetEnabled(player)) status = statuson;
                    else status = statusoff;

                    player.ChatMessage(lang.GetMessage("StatusMessage", this, player.UserIDString) + status);
                    break;
                default:
                    player.ChatMessage("Invalid syntax!");
                    break;
            }
        }

        [ConsoleCommand("furnacesplitter.enabled")]
        private void ConsoleCommand_Toggle(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (!HasPermission(player))
            {
                player.ConsoleMessage(lang.GetMessage("nopermission", this, player.UserIDString));
                return;
            }

            if (!arg.HasArgs())
            {
                player.ConsoleMessage(GetEnabled(player).ToString());
                return;
            }

            bool enabled = arg.GetBool(0);
            SetEnabled(player, enabled);
            if (enabled)
            {
                CreateUiIfFurnaceOpen(player); // queue ui updates
            }
            else
            {
                BaseOven oven = player.inventory.loot?.entitySource as BaseOven;
                CreateUi(player, oven, new OvenInfo()); // create ui without updates
            }
        }

        [ConsoleCommand("furnacesplitter.totalstacks")]
        private void ConsoleCommand_TotalStacks(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            BaseOven lootSource = player.inventory.loot?.entitySource as BaseOven;

            if (!HasPermission(player))
            {
                player.ConsoleMessage(lang.GetMessage("nopermission", this, player.UserIDString));
                return;
            }

            if (lootSource == null || !IsOvenCompatible(lootSource))
            {
                player.ConsoleMessage(lang.GetMessage("lootsource_invalid", this, player.UserIDString));
                return;
            }

            if (!GetEnabled(player))
                return;

            string ovenName = lootSource.ShortPrefabName;
            PlayerOptions playerOption = allPlayerOptions[player.userID];

            if (playerOption.TotalStacks.ContainsKey(ovenName))
            {
                if (!arg.HasArgs())
                {
                    player.ConsoleMessage(playerOption.TotalStacks[ovenName].ToString());
                }
                else
                {
                    int newValue = (int)Mathf.Clamp(arg.GetInt(0), 1, lootSource.inputSlots);
                    playerOption.TotalStacks[ovenName] = newValue;
                }
            }
            else
            {
                PrintWarning($"Unsupported furnace '{ovenName}'");
                player.ConsoleMessage(lang.GetMessage("unsupported_furnace", this, player.UserIDString));
            }

            CreateUiIfFurnaceOpen(player);
        }

        [ConsoleCommand("furnacesplitter.trim")]
        private void ConsoleCommand_Trim(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (!GetEnabled(player))
                return;

            BaseOven lootSource = player.inventory.loot?.entitySource as BaseOven;

            if (!HasPermission(player))
            {
                player.ConsoleMessage(lang.GetMessage("nopermission", this, player.UserIDString));
                return;
            }

            if (lootSource == null || !IsOvenCompatible(lootSource))
            {
                player.ConsoleMessage(lang.GetMessage("lootsource_invalid", this, player.UserIDString));
                return;
            }

            OvenInfo ovenInfo = GetOvenInfo(lootSource);
            var fuelSlots = lootSource.inventory.itemList.Where(item => item.info == lootSource.fuelType).ToList();
            int totalFuel = fuelSlots.Sum(item => item.amount);
            int toRemove = (int)Math.Floor(totalFuel - ovenInfo.FuelNeeded);

            if (toRemove <= 0)
                return;

            foreach (Item fuelItem in fuelSlots)
            {
                int toTake = Math.Min(fuelItem.amount, toRemove);
                toRemove -= toTake;

                Vector3 dropPosition = player.GetDropPosition();
                Vector3 dropVelocity = player.GetDropVelocity();

                if (toTake >= fuelItem.amount)
                {
                    if (!player.inventory.GiveItem(fuelItem))
                        fuelItem.Drop(dropPosition, dropVelocity, Quaternion.identity);
                }
                else
                {
                    Item splitItem = fuelItem.SplitItem(toTake);
                    if (!player.inventory.GiveItem(splitItem))
                        splitItem.Drop(dropPosition, dropVelocity, Quaternion.identity);
                }

                if (toRemove <= 0)
                    break;
            }
        }

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permUse);
        }

        #region Configuration

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                // English
                { "turnon", "Turn On" },
                { "turnoff", "Turn Off" },
                { "title", "Furnace Splitter" },
                { "eta", "ETA" },
                { "totalstacks", "Total stacks" },
                { "trim", "Trim fuel" },
                { "lootsource_invalid", "Current loot source invalid" },
                { "unsupported_furnace", "Unsupported furnace." },
                { "nopermission", "You don't have permission to use this." },
                { "StatusONColor", "<color=green>ON</color>"},
                { "StatusOFFColor", "<color=red>OFF</color>"},
                { "StatusMessage", "Furnace Splitter status set to: "}
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating default config for FurnaceSplitter.");
            config = GetDefaultConfig();
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig {
                UiPosition = new Vector2(0.6505f, 0.022f),
                savePlayerData = true,
                ovens = new SortedDictionary<string, PluginConfig.OvenConfig> {
                    { "*", new PluginConfig.OvenConfig { enabled = true, fuelMultiplier = 1.0f } }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.Converters.Add(new Vector2Converter());
            config = Config.ReadObject<PluginConfig>();

            if (!config.ovens.ContainsKey("*"))
            {
                config.ovens["*"] = new PluginConfig.OvenConfig {
                    enabled = true,
                    fuelMultiplier = 1.0f
                };
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        private class PluginConfig
        {
            public Vector2 UiPosition;
            public bool savePlayerData;
            public SortedDictionary<string, OvenConfig> ovens = new SortedDictionary<string, OvenConfig>();

            public class OvenConfig
            {
                public bool enabled;
                public float fuelMultiplier = 1.0f;
            }
        }

        private class Vector2Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Vector2 vec = (Vector2)value;
                serializer.Serialize(writer, new { vec.x, vec.y });
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                Vector2 result = new Vector2();
                JObject jVec = JObject.Load(reader);

                result.x = jVec["x"].ToObject<float>();
                result.y = jVec["y"].ToObject<float>();

                return result;
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector2);
            }
        }

        #endregion Configuration

        #region Exposed plugin methods

        [HookMethod("MoveSplitItem")]
        public string Hook_MoveSplitItem(Item item, BaseOven oven, int totalSlots, int splitAmount)
        {
            MoveResult result = MoveSplitItem(item, oven, totalSlots, splitAmount);
            return result.ToString();
        }

        [HookMethod("GetOvenInfo")]
        public JObject Hook_GetOvenInfo(BaseOven oven)
        {
            OvenInfo ovenInfo = GetOvenInfo(oven);
            return JObject.FromObject(ovenInfo);
        }

        #endregion Exposed plugin methods
    }
}