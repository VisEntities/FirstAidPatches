using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Active Sort", "Mevent", "1.0.8")]
    [Description("Sorts furnace and refinery on click")]
    internal class ActiveSort : RustPlugin
    {
        #region Fields

        private static ActiveSort _instance;

        private readonly Dictionary<BasePlayer, ActiveSortUI> _components = new Dictionary<BasePlayer, ActiveSortUI>();

        private const string PermUse = "activesort.use";

        private const string Layer = "UI.ActiveSort";

        private enum FurnaceType
        {
            Furnace,
            Refinery
        }

        private readonly Dictionary<string, string> _furnaceItems = new Dictionary<string, string>
        {
            ["sulfur.ore"] = "sulfur",
            ["metal.ore"] = "metal.fragments",
            ["hq.metal.ore"] = "metal.refined"
        };

        private readonly Dictionary<string, string> _refineryItems = new Dictionary<string, string>
        {
            ["crude.oil"] = "lowgradefuel"
        };

        #endregion

        #region Config

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "ShowUI")]
            public bool ShowUI = true;

            [JsonProperty(PropertyName = "ButtonPositionOffset")]
            public Vector2 ButtonPositionOffset = new Vector2(0, 0);

            [JsonProperty(PropertyName = "ButtonSize")]
            public Vector2 ButtonSize = new Vector2(115, 30);

            [JsonProperty(PropertyName = "ButtonColorHex")]
            public string ButtonColorHex = "#6F8344";

            [JsonProperty(PropertyName = "ButtonCaptionColorHex")]
            public string ButtonCaptionColorHex = "#A5BA7A";

            [JsonProperty(PropertyName = "ButtonCaptionFontSize")]
            public int ButtonCaptionFontSize = 16;

            [JsonProperty(PropertyName = "ButtonCaptionIsBold")]
            public bool ButtonCaptionIsBold;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;

            permission.RegisterPermission(PermUse, this);
            if (!_config.ShowUI)
            {
                Unsubscribe(nameof(OnPlayerLootEnd));
                Unsubscribe(nameof(OnLootEntity));
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

            _components.Values.ToList().ForEach(component =>
            {
                if (component != null)
                    component.Kill();
            });

            _instance = null;
            _config = null;
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (CanSort(player) && GetComponent(player) == null)
                player.gameObject.AddComponent<ActiveSortUI>();
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if (inventory == null) return;

            var ui = inventory.GetComponent<ActiveSortUI>();
            if (ui == null) return;

            ui.Kill();
        }

        #endregion

        #region Commands

        [ConsoleCommand("activesort.sort")]
        private void HandlerSortLoot(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || GetComponent(player) == null) return;

            SortLoot(player);
        }

        #endregion

        #region Component

        private class ActiveSortUI : FacepunchBehaviour
        {
            private readonly Vector2 _buttonBasePosition = new Vector2(365, 85);

            private BasePlayer _player;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();

                _instance._components[_player] = this;

                RenderUI();
            }

            private void RenderUI()
            {
                CuiHelper.DestroyUi(_player, Layer);
                CuiHelper.AddUi(_player, new CuiElementContainer
                {
                    {
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 0.0",
                                AnchorMax = "0.5 0.0",
                                OffsetMax =
                                    $"{_buttonBasePosition.x + _config.ButtonPositionOffset.x + _config.ButtonSize.x / 2} " +
                                    $"{_buttonBasePosition.y + _config.ButtonPositionOffset.y + _config.ButtonSize.y / 2}",
                                OffsetMin =
                                    $"{_buttonBasePosition.x + _config.ButtonPositionOffset.x - _config.ButtonSize.x / 2} " +
                                    $"{_buttonBasePosition.y + _config.ButtonPositionOffset.y - _config.ButtonSize.y / 2}"
                            },
                            Text =
                            {
                                Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor(_config.ButtonCaptionColorHex),
                                Font = _config.ButtonCaptionIsBold
                                    ? "RobotoCondensed-Bold.ttf"
                                    : "robotocondensed-regular.ttf",
                                Text = _instance.Msg("BUTTON_CAPTION", _player.UserIDString),
                                FontSize = _config.ButtonCaptionFontSize
                            },
                            Button =
                            {
                                Color = HexToCuiColor(_config.ButtonColorHex),
                                Command = "activesort.sort"
                            }
                        },
                        "Overlay", Layer
                    }
                });
            }

            private void OnDestroy()
            {
                CuiHelper.DestroyUi(_player, Layer);

                _instance?._components?.Remove(_player);

                Destroy(this);
            }

            public void Kill()
            {
                DestroyImmediate(this);
            }
        }

        #endregion

        #region Utils

        private ActiveSortUI GetComponent(BasePlayer player)
        {
            ActiveSortUI sortUI;
            return _components.TryGetValue(player, out sortUI) ? sortUI : null;
        }

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {alpha / 100f}";
        }

        private void PutToContainer(Dictionary<ulong, int> data, Dictionary<string, Item> items, string shortname,
            ItemContainer container,
            BasePlayer player, ref int spaceLeft, bool reserve = false)
        {
            if (items.ContainsKey(shortname))
            {
                var stackToContainer = TakeStack(data, items[shortname]);
                if (stackToContainer != null)
                {
                    stackToContainer.MoveToContainer(container);
                    ReturnToPlayer(data, player, items[shortname]);
                }
                else
                {
                    items[shortname].MoveToContainer(container);
                    items.Remove(shortname);
                }
            }

            if (items.ContainsKey(shortname) || reserve) spaceLeft--;
        }

        private Item TakeStack(Dictionary<ulong, int> data, Item item, int targetCount = -1)
        {
            int count;

            if (!data.TryGetValue(item.uid.Value, out count)) count = item.MaxStackable();

            if (targetCount != -1) count = Math.Min(count, targetCount);
            if (item.amount > count) return item.SplitItem(count);

            return null;
        }

        private void FilterOnlyNotProcessed(Dictionary<ulong, int> data, Dictionary<string, Item> items,
            BasePlayer player, ItemContainer container,
            FurnaceType type)
        {
            var allowedItems = type == FurnaceType.Furnace ? _furnaceItems : _refineryItems;
            foreach (var shortname in items.Keys.ToList())
                if (allowedItems.ContainsValue(shortname) &&
                    !items.ContainsKey(allowedItems.FirstOrDefault(x => x.Value == shortname).Key))
                {
                    ReturnToPlayer(data, player, items[shortname]);
                    items.Remove(shortname);
                }
        }

        private void FilterWhitelist(Dictionary<ulong, int> data, Dictionary<string, Item> items, BasePlayer player,
            ItemContainer container,
            FurnaceType type)
        {
            var allowedItems = type == FurnaceType.Furnace ? _furnaceItems : _refineryItems;
            foreach (var shortname in items.Keys.ToList())
                if (!allowedItems.ContainsKey(shortname) && !allowedItems.ContainsValue(shortname))
                {
                    ReturnToPlayer(data, player, items[shortname]);
                    items.Remove(shortname);
                }
        }

        private void ReturnToPlayer(Dictionary<ulong, int> data, BasePlayer player, Item item)
        {
            while (item != null)
            {
                var nextToGive = TakeStack(data, item);
                if (nextToGive == null)
                {
                    nextToGive = item;
                    item = null;
                }

                player.GiveItem(nextToGive);
            }
        }

        private readonly Dictionary<BasePlayer, Dictionary<ulong, int>> _stackByItem =
            new Dictionary<BasePlayer, Dictionary<ulong, int>>();

        private Dictionary<string, Item> CloneAndPackItems(ItemContainer container, Dictionary<ulong, int> data)
        {
            var items = new Dictionary<string, Item>();
            foreach (var it in container.itemList)
                if (items.ContainsKey(it.info.shortname))
                {
                    items[it.info.shortname].amount += it.amount;
                }
                else
                {
                    var item = ItemManager.Create(it.info, it.amount, it.skin);

                    items[it.info.shortname] = item;

                    int stackable;
                    if (data.TryGetValue(it.uid.Value, out stackable))
                    {
                        data[item.uid.Value] = stackable;

                        data.Remove(it.uid.Value);
                    }
                }

            return items;
        }

        private void ClearContainer(ItemContainer container)
        {
            while (container.itemList.Count > 0)
            {
                var item = container.itemList[0];
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        #endregion

        #region Lang

        private string Msg(string key, string userIDString)
        {
            return lang.GetMessage(key, this, userIDString);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BUTTON_CAPTION"] = "Sort"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BUTTON_CAPTION"] = "Сортировать"
            }, this, "ru");
        }

        #endregion

        #region API

        private void SortLoot(BasePlayer player)
        {
            if (!CanSort(player)) return;

            if (player.inventory.loot.containers == null || player.inventory.loot.containers.Count == 0) return;

            var container = player.inventory.loot.containers[0];
            var furnace = player.inventory.loot.entitySource.GetComponent<BaseOven>();
            var type = FurnaceType.Furnace;
            if (furnace.name.Contains("refinery")) type = FurnaceType.Refinery;
            var allowedItems = type == FurnaceType.Furnace ? _furnaceItems : _refineryItems;

            if (!_stackByItem.ContainsKey(player))
                _stackByItem.Add(player, new Dictionary<ulong, int>());

            var data = _stackByItem[player];

            foreach (var it in container.itemList.ToList())
            {
                data[it.uid.Value] = it.MaxStackable();
                if (it.info.shortname != "wood" && !allowedItems.ContainsKey(it.info.shortname))
                    ReturnToPlayer(data, player, it);
            }

            var items = CloneAndPackItems(container, data);
            ClearContainer(container);

            var spaceLeft = container.capacity;
            PutToContainer(data, items, "wood", container, player, ref spaceLeft, true);
            PutToContainer(data, items, "charcoal", container, player, ref spaceLeft, true);
            FilterWhitelist(data, items, player, container, type);
            FilterOnlyNotProcessed(data, items, player, container, type);

            while (true)
            {
                var toSortKinds = items.Keys.Count(shortname => allowedItems.ContainsKey(shortname));
                if (toSortKinds == 0)
                {
                    if (items.Count > 0)
                        items.Keys.ToList().ForEach(shortname => { ReturnToPlayer(data, player, items[shortname]); });
                    break;
                }

                if (toSortKinds * 2 > spaceLeft)
                {
                    var toCancel = items.Keys.ToList()[0];
                    ReturnToPlayer(data, player, items[toCancel]);
                    if (items.ContainsKey(allowedItems[toCancel]))
                        ReturnToPlayer(data, player, items[allowedItems[toCancel]]);

                    items.Remove(toCancel);
                    items.Remove(allowedItems[toCancel]);
                    continue;
                }

                var cellForEach = spaceLeft / toSortKinds;
                var cellAdditional = spaceLeft % toSortKinds;

                var cellCountByName = new Dictionary<string, int>();
                foreach (var shortname in items.Keys)
                    if (allowedItems.ContainsKey(shortname))
                    {
                        cellCountByName[shortname] = cellForEach;
                        if (cellAdditional > 0)
                        {
                            cellCountByName[shortname]++;
                            cellAdditional--;
                        }
                    }

                foreach (var shortname in cellCountByName.Keys)
                {
                    var cellAmount = items[shortname].amount / (cellCountByName[shortname] - 1);
                    if (cellAmount > 0)
                        for (var i = 0; i < cellCountByName[shortname] - 2; i++)
                        {
                            var entry = TakeStack(data, items[shortname], cellAmount);
                            entry.MoveToContainer(container, -1, false);
                        }

                    var lastPart = TakeStack(data, items[shortname]);
                    if (lastPart == null)
                        lastPart = items[shortname];
                    else
                        ReturnToPlayer(data, player, items[shortname]);

                    lastPart.MoveToContainer(container, -1, false);
                    if (items.ContainsKey(allowedItems[shortname]))
                    {
                        var processedToContainer = TakeStack(data, items[allowedItems[shortname]]);
                        if (processedToContainer == null)
                            processedToContainer = items[allowedItems[shortname]];
                        else
                            ReturnToPlayer(data, player, items[allowedItems[shortname]]);

                        processedToContainer.MoveToContainer(container);
                    }

                    items.Remove(shortname);
                    items.Remove(allowedItems[shortname]);
                }
            }

            var longestCookingTime = 0.0f;

            foreach (var it in container.itemList)
            {
                var cookable = it.info.GetComponent<ItemModCookable>();
                if (cookable != null)
                {
                    var cookingTime = cookable.cookTime * it.amount;
                    if (cookingTime > longestCookingTime) longestCookingTime = cookingTime;
                }
            }

            var fuelAmount = furnace.fuelType.GetComponent<ItemModBurnable>().fuelAmount;
            var neededFuel = Mathf.CeilToInt(longestCookingTime * (furnace.cookingTemperature / 200.0f) / fuelAmount);

            foreach (var it in container.itemList)
                if (it.info.shortname == "wood")
                {
                    if (neededFuel == 0)
                    {
                        ReturnToPlayer(data, player, it);
                    }
                    else if (it.amount > neededFuel)
                    {
                        var unneded = it.SplitItem(it.amount - neededFuel);
                        ReturnToPlayer(data, player, unneded);
                    }

                    break;
                }

            _stackByItem.Remove(player);
        }

        private bool CanSort(BasePlayer player)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermUse)) return false;

            var furnace = player.inventory?.loot?.entitySource?.GetComponent<BaseOven>();
            return furnace != null && (furnace.name.Contains("furnace") || furnace.name.Contains("refinery"));
        }

        #endregion
    }
}