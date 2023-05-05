using UnityEngine;
using System;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.IO;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("HitIcon", "FastBurst", "2.0.1")]
    [Description("Configurable precached icon when you hit player | friend | clan member | headshot")]
    class HitIcon : RustPlugin
    {

        [PluginReference] Plugin Friends, Clans;

        #region Variables
        private bool _friendAPI;
        private bool _clansAPI;
        private ImageCache _imageAssets;
        private GameObject _hitObject;
        private StoredData _storedData;
        private Dictionary<ulong, UIHandler> _playersUIHandler = new Dictionary<ulong, UIHandler>();
        #endregion

        #region Oxide
        private void OnServerInitialized()
        {
            if (!configData.ConfigSettings.showDeathSkull && !configData.ConfigSettings.showNpc)
                Unsubscribe("OnEntityDeath");
            CacheImage();
            InitializeAPI();
            foreach (var player in BasePlayer.activePlayerList)
                GetUIHandler(player);
        }

        private void Loaded()
        {
            LoadData();
            InitLanguage();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                player.GetComponent<UIHandler>()?.Destroy();
                CuiHelper.DestroyUi(player, "hitpng");
                CuiHelper.DestroyUi(player, "hitdmg");
            }
            SaveData();
            UnityEngine.Object.Destroy(_hitObject);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            UIHandler value;
            if (!_playersUIHandler.TryGetValue(player.userID, out value)) return;
            _playersUIHandler[player.userID]?.Destroy();
            _playersUIHandler.Remove(player.userID);
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo) => SendHit(attacker, hitinfo);

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info) => SendDeath(entity, info);
        #endregion

        #region API
        private void InitializeAPI()
        {
            if (Friends != null)
            {
                _friendAPI = true;
                PrintWarning("Plugin Friends work with HitIcon");
            }

            if (Clans != null)
            {
                _clansAPI = true;
                PrintWarning("Plugin Clans work with HitIcon");
            }
        }
        private bool AreFriends(string playerId, string friendId)
        {
            try
            {
                bool result = (bool)Friends?.CallHook("AreFriends", playerId, friendId);
                return result;
            }
            catch
            {
                return false;
            }
        }
        private bool AreClanMates(ulong playerID, ulong victimID)
        {
            var playerTag = Clans?.Call<string>("GetClanOf", playerID);
            var victimTag = Clans?.Call<string>("GetClanOf", victimID);
            if (playerTag != null)
                if (victimTag != null)
                    if (playerTag == victimTag)
                        return true;
            return false;
        }
        #endregion

        #region ImageDownloader
        private void CacheImage()
        {
            _hitObject = new GameObject();
            _imageAssets = _hitObject.AddComponent<ImageCache>();
            _imageAssets.imageFiles.Clear();
            string dataDirectory = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar;
            _imageAssets.GetImage("hitimage", dataDirectory + "hit.png");
            _imageAssets.GetImage("deathimage", dataDirectory + "death.png");
            Download();
        }

        class ImageCache : MonoBehaviour
        {
            public Dictionary<string, string> imageFiles = new Dictionary<string, string>();
            private List<Queue> queued = new List<Queue>();
            class Queue
            {
                public string Url { get; set; }
                public string Name { get; set; }
            }

            public void OnDestroy()
            {
                foreach (var value in imageFiles.Values)
                {
                    FileStorage.server.RemoveEntityNum(new NetworkableId(ulong.MaxValue), Convert.ToUInt32(value));
                }
            }

            public void GetImage(string name, string url)
            {
                queued.Add(new Queue
                {
                    Url = url,
                    Name = name
                });
            }

            IEnumerator WaitForRequest(Queue queue)
            {
                using (var www = new WWW(queue.Url))
                {
                    yield return www;

                    if (string.IsNullOrEmpty(www.error))
                        imageFiles.Add(queue.Name, FileStorage.server.Store(www.bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString());
                    else
                    {
                        Debug.LogWarning("\n\n!!!!!!!!!!!!!!!!!!!!!\n\nError downloading image files (death.png and hit.png)\nThey must be in your oxide/data/ !\n\n!!!!!!!!!!!!!!!!!!!!!\n\n");
                        ConsoleSystem.Run(ConsoleSystem.Option.Unrestricted, "oxide.unload HitIcon");
                    }
                }
            }
            public void Process()
            {
                for (int i = 0; i < 2; i++)
                    StartCoroutine(WaitForRequest(queued[i]));
            }
        }

        private string FetchImage(string name)
        {
            string result;
            if (_imageAssets.imageFiles.TryGetValue(name, out result))
                return result;
            return string.Empty;
        }

        private void Download() => _imageAssets.Process();
        #endregion

        #region CUI
        private void Png(BasePlayer player, string name, string image, string start, string end, string color)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = name,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = image,
                        Color = color,
                        Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = start,
                        AnchorMax = end
                    }
                }
            });
            CuiHelper.AddUi(player, container);
        }

        private void Dmg(BasePlayer player, string name, string text, string start, string end, string color, int size)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = name,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text,
                        FontSize = size,
                        Font = configData.ConfigSettings.dmgFont,
                        Color = color,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = start,
                        AnchorMax = end
                    },
                    new CuiOutlineComponent
                    {
                        Color = configData.ConfigSettings.dmgOutlineColor,
                        Distance = configData.ConfigSettings.dmgOutlineDistance
                    }
                }
            });
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region GuiHandler
        class UIHandler : MonoBehaviour
        {
            public BasePlayer player;
            public bool isDestroyed = false;
            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
            public void DestroyUI()
            {
                if (!isDestroyed)
                {
                    CancelInvoke("DestroyUI");
                    CuiHelper.DestroyUi(player, "hitdmg");
                    CuiHelper.DestroyUi(player, "hitpng");
                    Invoke("DestroyUI", configData.ConfigSettings.timeToDestroy);
                    isDestroyed = true;
                    return;
                }
                CuiHelper.DestroyUi(player, "hitdmg");
                CuiHelper.DestroyUi(player, "hitpng");
            }
            public void Destroy() => UnityEngine.Object.Destroy(this);
        }
        #endregion

        #region Helpers
        private void SendHit(BasePlayer attacker, HitInfo info)
        {
            if (info == null || attacker == null || !attacker.IsConnected)
                return;

            if (_storedData.DisabledUsers.Contains(attacker.userID))
                return;

            if (info.HitEntity is BaseNpc && configData.ConfigSettings.showNpc)
            {
                GuiDisplay(attacker, configData.ColorSettings.colorNpc, info);
                return;
            }

            var victim = info.HitEntity as BasePlayer;
            if (victim == null)
                return;

            if (victim == attacker)
                return;

            if (configData.ConfigSettings.useClans && _clansAPI)
            {
                if (AreClanMates(attacker.userID, victim.userID))
                {
                    GuiDisplay(attacker, configData.ColorSettings.colorClan, info, false, "clans");
                    if (configData.ConfigSettings.useSound)
                        EffectNetwork.Send(new Effect(configData.ConfigSettings.mateSound, attacker.transform.position, Vector3.zero), attacker.net.connection);
                    return;
                }
            }

            if (_friendAPI && configData.ConfigSettings.useFriends && AreFriends(victim.userID.ToString(), attacker.userID.ToString()))
            {
                GuiDisplay(attacker, configData.ColorSettings.colorFriend, info, false, "friends");
                if (configData.ConfigSettings.useSound)
                    EffectNetwork.Send(new Effect(configData.ConfigSettings.mateSound, attacker.transform.position, Vector3.zero), attacker.net.connection);
                return;
            }

            if (info.isHeadshot)
            {
                GuiDisplay(attacker, configData.ColorSettings.colorHead, info, false, "", true);
                return;
            }

            GuiDisplay(attacker, configData.ColorSettings.colorBody, info);
        }

        private void SendDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null)
                return;

            if (!configData.ConfigSettings.showDeathSkull)
                return;

            var initiator = (info?.Initiator as BasePlayer);
            if (initiator == null)
                return;

            if (_storedData.DisabledUsers.Contains(initiator.userID))
                return;

            var npc = (entity as BaseNpc);
            if (npc != null)
            {
                if (configData.ConfigSettings.showNpc)
                {
                    NextTick(() => GuiDisplay(initiator, configData.ColorSettings.colorBody, info, true));
                    return;
                }
            }
            var player = entity as BasePlayer;
            if (player == null)
                return;

            if (player == initiator)
                return;

            NextTick(() => GuiDisplay(initiator, configData.ColorSettings.colorBody, info, true));
        }

        private void GuiDisplay(BasePlayer player, string color, HitInfo hitinfo, bool isKill = false, string whatIsIt = "", bool isHead = false)
        {
            var uiHandler = GetUIHandler(player);
            uiHandler.isDestroyed = false;
            uiHandler.DestroyUI();

            if (isKill)
            {
                CuiHelper.DestroyUi(player, "hitdmg");
                Png(player, "hitpng", FetchImage("deathimage"), "0.487 0.482", "0.513 0.518", configData.ColorSettings.colorDeath);
            }
            if (configData.ConfigSettings.showHit && !isKill)
                Png(player, "hitpng", FetchImage("hitimage"), "0.492 0.4905", "0.506 0.5095", color);

            if (configData.ConfigSettings.showDamage)
            {
                NextTick(() => {
                    if (hitinfo.HitEntity == null)
                        return;

                    if ((hitinfo.HitEntity as BaseCombatEntity).IsDead())
                        return;

                    if (whatIsIt == "clans" && !configData.ConfigSettings.showClanDamage)
                        return;

                    if (whatIsIt == "friends" && !configData.ConfigSettings.showFriendDamage)
                        return;

                    if (!isKill && !configData.ConfigSettings.showDeathSkull || !isKill)
                    {
                        CuiHelper.DestroyUi(player, "hitdmg");
                        Dmg(player, "hitdmg", $"-{(int)hitinfo.damageTypes.Total()}", "0.45 0.45", "0.55 0.50", !isHead ? configData.ColorSettings.colorDamage : configData.ColorSettings.colorHeadDamage, configData.ConfigSettings.dmgTextSize);
                    }
                });
            }
        }

        private UIHandler GetUIHandler(BasePlayer player)
        {
            UIHandler value;
            if (!_playersUIHandler.TryGetValue(player.userID, out value))
            {
                _playersUIHandler[player.userID] = player.gameObject.AddComponent<UIHandler>();
                return _playersUIHandler[player.userID];
            }
            return value;
        }
        #endregion

        #region ChatCommand
        [ChatCommand("hit")]
        private void ToggleHit(BasePlayer player)
        {
            if (!_storedData.DisabledUsers.Contains(player.userID))
            {
                _storedData.DisabledUsers.Add(player.userID);
                PrintToChat(player, lang.GetMessage("Disabled", this, player.UserIDString));
            }
            else
            {
                _storedData.DisabledUsers.Remove(player.userID);
                PrintToChat(player, lang.GetMessage("Enabled", this, player.UserIDString));
            }
        }
        #endregion

        #region Config
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Color")]
            public ColorOptions ColorSettings { get; set; }
            [JsonProperty(PropertyName = "Configuration")]
            public ConfigOptions ConfigSettings { get; set; }

            public class ColorOptions
            {
                [JsonProperty(PropertyName = "Hit clan member color")]
                public string colorClan { get; set; }
                [JsonProperty(PropertyName = "Hit friend color")]
                public string colorFriend { get; set; }
                [JsonProperty(PropertyName = "Hit head color")]
                public string colorHead { get; set; }
                [JsonProperty(PropertyName = "Hit body color")]
                public string colorBody { get; set; }
                [JsonProperty(PropertyName = "Hit NPC body color")]
                public string colorNpc { get; set; }
                [JsonProperty(PropertyName = "Hit Death body color")]
                public string colorDeath { get; set; }
                [JsonProperty(PropertyName = "Text damage color")]
                public string colorDamage { get; set; }
                [JsonProperty(PropertyName = "Text head damage color")]
                public string colorHeadDamage { get; set; }
            }

            public class ConfigOptions
            {
                [JsonProperty(PropertyName = "Damage text size")]
                public int dmgTextSize { get; set; }
                [JsonProperty(PropertyName = "Show clan member damage")]
                public bool showClanDamage { get; set; }
                [JsonProperty(PropertyName = "Show damage")]
                public bool showDamage { get; set; }
                [JsonProperty(PropertyName = "Show death kill")]
                public bool showDeathSkull { get; set; }
                [JsonProperty(PropertyName = "Show friend damage")]
                public bool showFriendDamage { get; set; }
                [JsonProperty(PropertyName = "Show hit icon")]
                public bool showHit { get; set; }
                [JsonProperty(PropertyName = "Show hits/deaths on NPC (Bears, wolfs, etc.)")]
                public bool showNpc { get; set; }
                [JsonProperty(PropertyName = "Text Font")]
                public string dmgFont { get; set; }
                [JsonProperty(PropertyName = "Text Outline Color")]
                public string dmgOutlineColor { get; set; }
                [JsonProperty(PropertyName = "Text Outline Distance")]
                public string dmgOutlineDistance { get; set; }
                [JsonProperty(PropertyName = "Time to destroy")]
                public float timeToDestroy { get; set; }
                [JsonProperty(PropertyName = "Use Clans")]
                public bool useClans { get; set; }
                [JsonProperty(PropertyName = "Use Friends")]
                public bool useFriends { get; set; }
                [JsonProperty(PropertyName = "Use sound when clan/friends get attacked")]
                public bool useSound { get; set; }
                [JsonProperty(PropertyName = "When clan/friends get attacked sound fx")]
                public string mateSound { get; set; }

            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                ColorSettings = new ConfigData.ColorOptions
                {
                    colorClan = "0 1 0 1",
                    colorFriend = "0 1 0 1",
                    colorHead = "1 0 0 1",
                    colorBody = "1 1 1 1",
                    colorNpc = "1 1 1 1 ",
                    colorDeath = "1 0 0 1",
                    colorDamage = "1 1 1 1",
                    colorHeadDamage = "1 0 0 1"
                },
                ConfigSettings = new ConfigData.ConfigOptions
                {
                    dmgTextSize = 15,
                    dmgFont = "robotocondensed-regular.ttf",
                    dmgOutlineColor = "0 0 0 1",
                    dmgOutlineDistance = "-0.4 0.4",
                    useFriends = true,
                    useClans = true,
                    showHit = true,
                    useSound = false,
                    showNpc = true,
                    showDamage = true,
                    showClanDamage = false,
                    showFriendDamage = true,
                    showDeathSkull = true,
                    mateSound = "assets/prefabs/instruments/guitar/effects/guitarpluck.prefab",
                    timeToDestroy = 0.45f
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(2, 0, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #endregion

        #region StoreData
        private class StoredData
        {
            public List<ulong> DisabledUsers = new List<ulong>();
        }

        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("HitIcon", _storedData);

        private void LoadData()
        {
            try
            {
                _storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("HitIcon");
            }
            catch
            {
                _storedData = new StoredData();
            }
        }
        #endregion

        #region Localization
        private void InitLanguage()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Enabled", "Hit icon was <color=green>enabled</color>"},
                {"Disabled", "Hit icon was <color=red>disabled</color>"}
            }, this);
        }
        #endregion        
    }
}