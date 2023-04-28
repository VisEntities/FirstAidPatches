using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("Loot Bouncer", "Sorrow/Arainrr", "1.0.10")]
    [Description("Empty the containers when players do not pick up all the items")]
    public class LootBouncer : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Slap, Trade;

        private readonly Dictionary<ulong, int> _lootEntities = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, HashSet<ulong>> _entityPlayers = new Dictionary<ulong, HashSet<ulong>>();
        private readonly Dictionary<ulong, Timer> _lootDestroyTimer = new Dictionary<ulong, Timer>();

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnPlayerAttack));
        }

        private void OnServerInitialized()
        {
            UpdateConfig();
            if (configData.slapPlayer && Slap == null)
            {
                PrintError("Slap is not loaded, get it at https://umod.org/plugins/slap");
            }
            if (configData.lootContainers.Any(x => IsBarrel(x.Key) && x.Value))
            {
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnPlayerAttack));
            }
        }

        private void Unload()
        {
            foreach (var value in _lootDestroyTimer.Values)
            {
                value?.Destroy();
            }
        }

        private void OnLootEntity(BasePlayer player, LootContainer lootContainer)
        {
            if (lootContainer == null || lootContainer.net == null || player == null)
            {
                return;
            }
            var obj = Trade?.Call("IsTradeBox", lootContainer);
            if (obj is bool && (bool)obj)
            {
                return;
            }
            bool enabled;
            if (configData.lootContainers.TryGetValue(lootContainer.ShortPrefabName, out enabled) && !enabled)
            {
                return;
            }

            var entityID = lootContainer.net.ID.Value;
            if (!_lootEntities.ContainsKey(entityID))
            {
                _lootEntities.Add(entityID, lootContainer.inventory.itemList.Count);
            }

            HashSet<ulong> looters;
            if (_entityPlayers.TryGetValue(entityID, out looters))
            {
                looters.Add(player.userID);
            }
            else
            {
                _entityPlayers.Add(entityID, new HashSet<ulong> { player.userID });
            }

            // If looted again, the timer for emptying will stop
            Timer value;
            if (_lootDestroyTimer.TryGetValue(entityID, out value))
            {
                _lootEntities[entityID] = 666;
                value?.Destroy();
                _lootDestroyTimer.Remove(entityID);
            }
        }

        private void OnLootEntityEnd(BasePlayer player, LootContainer lootContainer)
        {
            if (lootContainer == null || lootContainer.net == null || player == null)
            {
                return;
            }
            var entityID = lootContainer.net.ID.Value;
            HashSet<ulong> looters;
            if (!(lootContainer.inventory?.itemList?.Count > 0))
            {
                _lootEntities.Remove(entityID);
                if (_entityPlayers.TryGetValue(entityID, out looters))
                {
                    looters.Remove(player.userID);
                }
                return;
            }

            int tempItemsCount;
            if (_lootEntities.TryGetValue(entityID, out tempItemsCount))
            {
                _lootEntities.Remove(entityID);
                if (lootContainer.inventory.itemList.Count < tempItemsCount)
                {
                    if (!_lootDestroyTimer.ContainsKey(entityID))
                    {
                        _lootDestroyTimer.Add(entityID, timer.Once(configData.timeBeforeLootEmpty, () => DropItems(lootContainer)));
                    }
                }
                else if (_entityPlayers.TryGetValue(entityID, out looters))
                {
                    looters.Remove(player.userID);
                }
                EmptyJunkPile(lootContainer);
            }
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || !attacker.userID.IsSteamId())
            {
                return;
            }
            var barrel = info?.HitEntity as LootContainer;
            if (barrel == null || barrel.net == null)
            {
                return;
            }
            if (!IsBarrel(barrel.ShortPrefabName))
            {
                return;
            }
            bool enabled;
            if (configData.lootContainers.TryGetValue(barrel.ShortPrefabName, out enabled) && !enabled)
            {
                return;
            }

            var barrelID = barrel.net.ID.Value;
            HashSet<ulong> attackers;
            if (_entityPlayers.TryGetValue(barrelID, out attackers))
            {
                attackers.Add(attacker.userID);
            }
            else
            {
                _entityPlayers.Add(barrelID, new HashSet<ulong> { attacker.userID });
            }

            if (!_lootDestroyTimer.ContainsKey(barrelID))
            {
                _lootDestroyTimer.Add(barrelID, timer.Once(configData.timeBeforeLootEmpty, () => DropItems(barrel)));
            }
            EmptyJunkPile(barrel);
        }

        private void OnEntityDeath(LootContainer barrel, HitInfo info)
        {
            if (barrel == null || barrel.net == null)
            {
                return;
            }
            if (!IsBarrel(barrel.ShortPrefabName))
            {
                return;
            }
            var attacker = info?.InitiatorPlayer;
            if (attacker == null || !attacker.userID.IsSteamId())
            {
                return;
            }

            HashSet<ulong> attackers;
            if (!_entityPlayers.TryGetValue(barrel.net.ID.Value, out attackers))
            {
                return;
            }
            attackers.Remove(attacker.userID);
        }

        private void OnEntityKill(LootContainer lootContainer)
        {
            if (lootContainer == null || lootContainer.net == null)
            {
                return;
            }
            var entityID = lootContainer.net.ID.Value;
            _lootEntities.Remove(entityID);

            Timer value;
            if (_lootDestroyTimer.TryGetValue(entityID, out value))
            {
                value?.Destroy();
                _lootDestroyTimer.Remove(entityID);
            }

            HashSet<ulong> playerIDs;
            if (!_entityPlayers.TryGetValue(entityID, out playerIDs))
            {
                return;
            }
            _entityPlayers.Remove(entityID);
            if (configData.slapPlayer && Slap != null)
            {
                foreach (var playerID in playerIDs)
                {
                    var player = BasePlayer.FindByID(playerID);
                    if (player == null || player.IPlayer == null)
                    {
                        continue;
                    }
                    Slap.Call("SlapPlayer", player.IPlayer);
                    Print(player, Lang("SlapMessage", player.UserIDString));
                }
            }
        }

        #endregion Oxide Hooks

        #region Methods

        private static bool IsBarrel(string shortPrefabName)
        {
            return shortPrefabName.Contains("barrel") || shortPrefabName.Contains("roadsign");
        }

        private void DropItems(LootContainer lootContainer)
        {
            if (lootContainer == null || lootContainer.IsDestroyed)
            {
                return;
            }
            if (configData.removeItems)
            {
                lootContainer.inventory?.Clear();
            }
            else
            {
                DropUtil.DropItems(lootContainer.inventory, lootContainer.GetDropPosition());
            }
            lootContainer.RemoveMe();
        }

        private void EmptyJunkPile(LootContainer lootContainer)
        {
            if (!configData.emptyJunkpile)
            {
                return;
            }
            var spawnGroup = lootContainer.GetComponent<SpawnPointInstance>()?.parentSpawnPointUser as SpawnGroup;
            if (spawnGroup == null)
            {
                return;
            }
            var junkPiles = Pool.GetList<JunkPile>();
            Vis.Entities(lootContainer.transform.position, 10f, junkPiles, Layers.Solid);
            var junkPile = junkPiles.FirstOrDefault(x => x.spawngroups.Contains(spawnGroup));
            var flag = junkPile == null || junkPile.net == null;
            Pool.FreeList(ref junkPiles);
            if (flag)
            {
                return;
            }

            if (_lootDestroyTimer.ContainsKey(junkPile.net.ID.Value))
            {
                return;
            }
            _lootDestroyTimer.Add(junkPile.net.ID.Value, timer.Once(configData.timeBeforeJunkpileEmpty, () =>
            {
                if (junkPile != null && !junkPile.IsDestroyed)
                {
                    if (configData.dropNearbyLoot)
                    {
                        var lootContainers = Pool.GetList<LootContainer>();
                        Vis.Entities(junkPile.transform.position, 10f, lootContainers, Layers.Solid);
                        foreach (var loot in lootContainers)
                        {
                            var lootSpawnGroup = loot.GetComponent<SpawnPointInstance>()?.parentSpawnPointUser as SpawnGroup;
                            if (lootSpawnGroup != null && junkPile.spawngroups.Contains(lootSpawnGroup))
                            {
                                DropItems(loot);
                            }
                        }
                        Pool.FreeList(ref lootContainers);
                    }
                    junkPile.SinkAndDestroy();
                }
            }));
        }

        private void UpdateConfig()
        {
            foreach (var prefab in GameManifest.Current.entities)
            {
                var lootContainer = GameManager.server.FindPrefab(prefab.ToLower())?.GetComponent<LootContainer>();
                if (lootContainer == null || string.IsNullOrEmpty(lootContainer.ShortPrefabName))
                {
                    continue;
                }
                if (!configData.lootContainers.ContainsKey(lootContainer.ShortPrefabName))
                {
                    configData.lootContainers.Add(lootContainer.ShortPrefabName, !lootContainer.ShortPrefabName.Contains("stocking") && !lootContainer.ShortPrefabName.Contains("roadsign"));
                }
            }
            SaveConfig();
        }

        #endregion Methods

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Time before the loot containers are empties (seconds)")]
            public float timeBeforeLootEmpty = 30f;

            [JsonProperty(PropertyName = "Empty the entire junkpile when automatically empty loot")]
            public bool emptyJunkpile = false;

            [JsonProperty(PropertyName = "Empty the nearby loot when emptying junkpile")]
            public bool dropNearbyLoot = false;

            [JsonProperty(PropertyName = "Time before the junkpile are empties (seconds)")]
            public float timeBeforeJunkpileEmpty = 150f;

            [JsonProperty(PropertyName = "Slaps players who don't empty containers")]
            public bool slapPlayer = false;

            [JsonProperty(PropertyName = "Remove instead bouncing")]
            public bool removeItems = false;

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings chat = new ChatSettings();

            public class ChatSettings
            {
                [JsonProperty(PropertyName = "Chat Prefix")]
                public string prefix = "<color=#00FFFF>[BackPumpJack]</color>: ";

                [JsonProperty(PropertyName = "Chat SteamID Icon")]
                public ulong steamIDIcon;
            }

            [JsonProperty(PropertyName = "Loot container settings")]
            public Dictionary<string, bool> lootContainers = new Dictionary<string, bool>();

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber version;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                }
                else
                {
                    UpdateConfigValues();
                }
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted. \n{ex}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
            configData.version = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData);
        }

        private void UpdateConfigValues()
        {
            if (configData.version < Version)
            {
                if (configData.version <= default(VersionNumber))
                {
                    string prefix, prefixColor;
                    if (GetConfigValue(out prefix, "Chat prefix") && GetConfigValue(out prefixColor, "Chat prefix color"))
                    {
                        configData.chat.prefix = $"<color={prefixColor}>{prefix}</color>: ";
                    }

                    ulong steamID;
                    if (GetConfigValue(out steamID, "Chat steamID icon"))
                    {
                        configData.chat.steamIDIcon = steamID;
                    }
                }
                configData.version = Version;
            }
        }

        private bool GetConfigValue<T>(out T value, params string[] path)
        {
            var configValue = Config.Get(path);
            if (configValue == null)
            {
                value = default(T);
                return false;
            }
            value = Config.ConvertValue<T>(configValue);
            return true;
        }

        #endregion ConfigurationFile

        #region LanguageFile

        private void Print(BasePlayer player, string message)
        {
            Player.Message(player, message, configData.chat.prefix, configData.chat.steamIDIcon);
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, id), args);
            }
            catch (Exception)
            {
                PrintError($"Error in the language formatting of '{key}'. (userid: {id}. lang: {lang.GetLanguage(id)}. args: {string.Join(" ,", args)})");
                throw;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SlapMessage"] = "You didn't empty the container. You got slapped by the container!!!"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SlapMessage"] = "wdnmd，不清空容器，给你个大耳刮子"
            }, this, "zh-CN");
        }

        #endregion LanguageFile
    }
}