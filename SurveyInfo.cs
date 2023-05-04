using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Survey Info", "Diesel_42o/Arainrr", "1.0.5", ResourceId = 2463)]
    [Description("Displays loot from survey charges")]
    internal class SurveyInfo : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin RustTranslationAPI;

        private const string PERMISSION_USE = "surveyinfo.use";
        private const string PERMISSION_CHECK = "surveyinfo.check";

        private static SurveyInfo _instance;

        private float _quarryWorkPerMinute;
        private float _pumpjackWorkPerMinute;
        private readonly HashSet<ulong> _checkedCraters = new HashSet<ulong>();

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            _instance = this;
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_CHECK, this);
            cmd.AddChatCommand(configData.command, this, nameof(CmdCraterInfo));
        }

        private void OnServerInitialized()
        {
            var quarry = GameManager.server.FindPrefab("assets/prefabs/deployable/quarry/mining_quarry.prefab")?.GetComponent<MiningQuarry>();
            if (quarry != null)
            {
                _quarryWorkPerMinute = 60f / quarry.processRate * quarry.workToAdd;
            }
            var pumpjack = GameManager.server.FindPrefab("assets/prefabs/deployable/oil jack/mining.pumpjack.prefab")?.GetComponent<MiningQuarry>();
            if (pumpjack != null)
            {
                _pumpjackWorkPerMinute = 60f / pumpjack.processRate * pumpjack.workToAdd;
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                UnityEngine.Object.Destroy(player.GetComponent<SurveyerComponent>());
            }
            _instance = null;
        }

        private void OnAnalysisComplete(SurveyCrater crater, BasePlayer player)
        {
            if (player == null || crater == null)
            {
                return;
            }
            var deposit = ResourceDepositManager.GetOrCreate(crater.transform.position);
            if (deposit?._resources == null)
            {
                return;
            }
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(Lang("MineralAnalysis", player.UserIDString));
            stringBuilder.AppendLine();
            stringBuilder.AppendLine();
            var language = lang.GetLanguage(player.UserIDString);
            foreach (var resource in deposit._resources)
            {
                var workPerMinute = crater.ShortPrefabName == "survey_crater_oil" ? _pumpjackWorkPerMinute : _quarryWorkPerMinute;
                var pM = workPerMinute / resource.workNeeded;
                var displayName = GetItemDisplayName(language, resource.type);
                stringBuilder.AppendLine($"{displayName} : {pM:0.00} pM");
            }
            var noteItem = ItemManager.CreateByName("note");
            noteItem.text = stringBuilder.ToString();
            player.GiveItem(noteItem, BaseEntity.GiveItemReason.PickedUp);
        }

        private void OnEntityKill(SurveyCharge surveyCharge)
        {
            if (surveyCharge == null)
            {
                return;
            }
            var player = surveyCharge.creatorEntity as BasePlayer;
            if (player == null)
            {
                return;
            }
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                return;
            }
            var checkPosition = surveyCharge.transform.position;
            timer.Once(0.5f, () =>
            {
                var list = Pool.GetList<SurveyCrater>();
                Vis.Entities(checkPosition, 1f, list, Layers.Mask.Default);
                var surveyCrater = list.FirstOrDefault();
                if (surveyCrater != null)
                {
                    var surveyItems = Pool.GetList<SurveyItem>();
                    if (!_checkedCraters.Contains(surveyCrater.net?.ID.Value ?? 0))
                    {
                        var deposit = ResourceDepositManager.GetOrCreate(surveyCrater.transform.position);
                        if (deposit != null)
                        {
                            foreach (var resource in deposit._resources)
                            {
                                surveyItems.Add(new SurveyItem { itemDefinition = resource.type, amount = resource.amount, workNeeded = resource.workNeeded });
                            }
                            _checkedCraters.Add(surveyCrater.net?.ID.Value ?? 0);
                        }
                    }
                    if (surveyItems.Count > 0)
                    {
                        SendMineralAnalysis(player, surveyItems, surveyCrater.ShortPrefabName == "survey_crater_oil");
                    }
                    Pool.FreeList(ref surveyItems);
                }
                Pool.FreeList(ref list);
            });
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null || newItem == null)
            {
                return;
            }
            if (oldItem?.info?.shortname != "surveycharge" && newItem.info.shortname == "surveycharge")
            {
                if (!permission.UserHasPermission(player.UserIDString, PERMISSION_CHECK))
                {
                    return;
                }
                UnityEngine.Object.Destroy(player.GetComponent<SurveyerComponent>());
                player.gameObject.AddComponent<SurveyerComponent>();
            }
        }

        #endregion Oxide Hooks

        #region Component

        private class SurveyerComponent : MonoBehaviour
        {
            private BasePlayer _player;
            private Item _heldItem;
            private float _lastCheck;
            private ulong _currentItemID;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                _heldItem = _player.GetActiveItem();
                _currentItemID = _player.svActiveItemID.Value;
            }

            private void Update()
            {
                if (_player == null || !_player.IsConnected || !_player.CanInteract())
                {
                    Destroy(this);
                    return;
                }
                if (_player.svActiveItemID.Value != _currentItemID)
                {
                    _heldItem = _player.GetActiveItem();
                    if (_heldItem != null && _heldItem.info.shortname != "surveycharge")
                    {
                        Destroy(this);
                        return;
                    }
                    _currentItemID = _player.svActiveItemID.Value;
                }
                if (Time.realtimeSinceStartup - _lastCheck >= 0.5f)
                {
                    if (!_player.serverInput.IsDown(BUTTON.FIRE_SECONDARY))
                    {
                        return;
                    }
                    var surveyPosition = GetSurveyPosition();
                    _instance.Print(_player, CanSpawnCrater(surveyPosition) ? _instance.Lang("CanSpawnCrater", _player.UserIDString) : _instance.Lang("CantSpawnCrater", _player.UserIDString));
                    _lastCheck = Time.realtimeSinceStartup;
                }
            }

            private Vector3 GetSurveyPosition()
            {
                RaycastHit hitInfo;
                return Physics.Raycast(_player.eyes.HeadRay(), out hitInfo, 100f, Layers.Solid) ? hitInfo.point : _player.transform.position;
            }

            private static bool CanSpawnCrater(Vector3 position)
            {
                if (WaterLevel.Test(position))
                {
                    return false;
                }
                var deposit = ResourceDepositManager.GetOrCreate(position);
                if (deposit?._resources == null || Time.realtimeSinceStartup - deposit.lastSurveyTime < 10f)
                {
                    return false;
                }
                RaycastHit hitOut;
                if (!TransformUtil.GetGroundInfo(position, out hitOut, 0.3f, 8388608))
                {
                    return false;
                }
                var list = Pool.GetList<SurveyCrater>();
                Vis.Entities(position, 10f, list, 1);
                var flag = list.Count > 0;
                Pool.FreeList(ref list);
                if (flag)
                {
                    return false;
                }
                foreach (var resource in deposit._resources)
                {
                    if (resource.spawnType == ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM && !resource.isLiquid && resource.amount >= 1000)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        #endregion Component

        #region Chat Command

        private void CmdCraterInfo(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            RaycastHit hitInfo;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hitInfo, 10f, Layers.Mask.Default) || !(hitInfo.GetEntity() is SurveyCrater))
            {
                Print(player, Lang("NotLookingAtCrater", player.UserIDString));
                return;
            }
            var surveyCrater = hitInfo.GetEntity() as SurveyCrater;
            var surveyItems = Pool.GetList<SurveyItem>();
            var deposit = ResourceDepositManager.GetOrCreate(surveyCrater.transform.position);
            if (deposit != null)
            {
                foreach (var resource in deposit._resources)
                {
                    surveyItems.Add(new SurveyItem { itemDefinition = resource.type, amount = resource.amount, workNeeded = resource.workNeeded });
                }
            }
            if (surveyItems.Count <= 0)
            {
                Print(player, Lang("NoMinerals", player.UserIDString));
                return;
            }
            SendMineralAnalysis(player, surveyItems, surveyCrater.ShortPrefabName == "survey_crater_oil");
            Pool.FreeList(ref surveyItems);
        }

        #endregion Chat Command

        #region Methods

        private void SendMineralAnalysis(BasePlayer player, List<SurveyItem> surveyItems, bool isOilCrater)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(Lang("MineralAnalysis", player.UserIDString));
            var language = lang.GetLanguage(player.UserIDString);
            foreach (var surveyItem in surveyItems)
            {
                var workPerMinute = isOilCrater ? _pumpjackWorkPerMinute : _quarryWorkPerMinute;
                var pM = workPerMinute / surveyItem.workNeeded;
                stringBuilder.AppendLine(Lang("MineralInfo", player.UserIDString, GetItemDisplayName(language, surveyItem.itemDefinition), surveyItem.amount, pM.ToString("0.00")));
            }
            Print(player, stringBuilder.ToString());
        }

        private struct SurveyItem
        {
            public ItemDefinition itemDefinition;
            public int amount;
            public float workNeeded;
        }

        #region RustTranslationAPI

        private string GetItemTranslationByShortName(string language, string itemShortName)
        {
            return (string)RustTranslationAPI.Call("GetItemTranslationByShortName", language, itemShortName);
        }

        private string GetItemDisplayName(string language, ItemDefinition itemDefinition)
        {
            string displayName;
            if (RustTranslationAPI != null)
            {
                displayName = GetItemTranslationByShortName(language, itemDefinition.shortname);
                if (!string.IsNullOrEmpty(displayName))
                {
                    return displayName;
                }
            }
            if (!configData.displayNames.TryGetValue(itemDefinition.displayName.english, out displayName))
            {
                displayName = itemDefinition.displayName.english;
                configData.displayNames.Add(displayName, displayName);
                SaveConfig();
            }
            return displayName;
        }

        #endregion RustTranslationAPI

        #endregion Methods

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Chat Command")]
            public string command = "craterinfo";

            [JsonProperty(PropertyName = "Chat Prefix")]
            public string prefix = "<color=#00FFFF>[SurveyInfo]</color>: ";

            [JsonProperty(PropertyName = "Chat SteamID Icon")]
            public ulong steamIDIcon = 0;

            [JsonProperty(PropertyName = "Display Names")]
            public Dictionary<string, string> displayNames = new Dictionary<string, string>();
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
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData);
        }

        #endregion ConfigurationFile

        #region LanguageFile

        private void Print(BasePlayer player, string message)
        {
            Player.Message(player, message, configData.prefix, configData.steamIDIcon);
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
                ["NotAllowed"] = "You don't have permission to use this command.",
                ["MineralAnalysis"] = "- Mineral Analysis -",
                ["MineralInfo"] = "<color=#05EB59>{0}</color> x<color=#FFA500>{1}</color> -- <color=#FF4500> pM: {2} </color>",
                ["CanSpawnCrater"] = "A crater <color=#8ee700>can</color> be spawned at the position you are looking at.",
                ["CantSpawnCrater"] = "A crater <color=#ce422b>cannot</color> be spawned at the position you are looking at.",
                ["NotLookingAtCrater"] = "You must be looking at a crater.",
                ["NoMinerals"] = "There are no minerals in this crater"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "您没有权限使用该命令",
                ["MineralAnalysis"] = "- 矿物分析 -",
                ["MineralInfo"] = "<color=#05EB59>{0}</color> x<color=#FFA500>{1}</color> -- <color=#FF4500> pM: {2} </color>",
                ["CanSpawnCrater"] = "您看着的位置 <color=#8ee700>可以</color> 勘探出矿物",
                ["CantSpawnCrater"] = "您看着的位置 <color=#ce422b>不能</color> 勘探出矿物",
                ["NotLookingAtCrater"] = "您必须看着一个矿坑",
                ["NoMinerals"] = "这个矿坑内没有任何矿物"
            }, this, "zh-CN");
        }

        #endregion LanguageFile
    }
}