using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Back Pump Jack", "Arainrr", "1.4.14")]
    [Description("Allows players to use survey charges to create an oil crater")]
    public class BackPumpJack : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Friends, Clans;

        private const string PREFAB_CRATER_OIL = "assets/prefabs/tools/surveycharge/survey_crater_oil.prefab";

        private readonly HashSet<ulong> _checkedCraters = new HashSet<ulong>();
        private readonly List<QuarryData> _activeCraters = new List<QuarryData>();
        private readonly List<MiningQuarry> _miningQuarries = new List<MiningQuarry>();
        private readonly Dictionary<ulong, PermissionSettings> _activeSurveyCharges = new Dictionary<ulong, PermissionSettings>();
        private readonly object _true = true;
        private readonly object _false = false;

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            LoadData();
            foreach (var permissionSetting in _configData.Permissions)
            {
                if (!permission.PermissionExists(permissionSetting.Permission, this))
                {
                    permission.RegisterPermission(permissionSetting.Permission, this);
                }
            }
            Unsubscribe(nameof(CanBuild));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityTakeDamage));
        }

        private void OnServerInitialized(bool initial)
        {
            if (_configData.Global.CantDeploy)
            {
                Subscribe(nameof(CanBuild));
            }
            if (_configData.Global.CantDamage)
            {
                Subscribe(nameof(OnEntityTakeDamage));
            }

            var quarry = GameManager.server.FindPrefab("assets/prefabs/deployable/quarry/mining_quarry.prefab")?.GetComponent<MiningQuarry>();
            if (quarry != null)
            {
                QuarrySettings.WorkPerMinute = 60f / quarry.processRate * quarry.workToAdd;
            }
            var pumpjack = GameManager.server.FindPrefab("assets/prefabs/deployable/oil jack/mining.pumpjack.prefab")?.GetComponent<MiningQuarry>();
            if (pumpjack != null)
            {
                PumpJackSettings.WorkPerMinute = 60f / pumpjack.processRate * pumpjack.workToAdd;
            }

            foreach (var serverEntity in BaseNetworkable.serverEntities)
            {
                var surveyCrater = serverEntity as SurveyCrater;
                if (surveyCrater != null)
                {
                    if (!surveyCrater.OwnerID.IsSteamId())
                    {
                        continue;
                    }
                    var deposit = ResourceDepositManager.GetOrCreate(surveyCrater.transform.position);
                    if (deposit?._resources == null || deposit._resources.Count <= 0)
                    {
                        continue;
                    }
                    var mineralItems = deposit._resources.Select(depositEntry => new MineralItemData
                    {
                        amount = depositEntry.amount,
                        shortname = depositEntry.type.shortname,
                        workNeeded = depositEntry.workNeeded
                    }).ToList();
                    _activeCraters.Add(new QuarryData
                    {
                        position = surveyCrater.transform.position,
                        isLiquid = surveyCrater.ShortPrefabName == "survey_crater_oil",
                        mineralItems = mineralItems
                    });
                    continue;
                }
                var miningQuarry = serverEntity as MiningQuarry;
                if (miningQuarry != null)
                {
                    OnEntitySpawned(miningQuarry);
                }
            }

            CheckValidData();
            if (initial)
            {
                timer.Once(10f, () => RefillMiningQuarries());
            }
            else
            {
                RefillMiningQuarries();
            }
        }

        private void OnServerSave()
        {
            timer.Once(Random.Range(0f, 60f), () => RefillMiningQuarries());
        }

        private void OnEntitySpawned(MiningQuarry miningQuarry)
        {
            if (miningQuarry == null || !miningQuarry.OwnerID.IsSteamId())
            {
                return;
            }
            _miningQuarries.Add(miningQuarry);
        }

        private void OnEntityKill(MiningQuarry miningQuarry)
        {
            if (miningQuarry == null || !miningQuarry.OwnerID.IsSteamId())
            {
                return;
            }
            _miningQuarries.Remove(miningQuarry);
        }

        private void OnExplosiveThrown(BasePlayer player, SurveyCharge surveyCharge)
        {
            if (surveyCharge == null || surveyCharge.net == null)
            {
                return;
            }
            var permissionSetting = GetPermissionSetting(player);
            if (permissionSetting == null)
            {
                return;
            }
            surveyCharge.OwnerID = player.userID;
            _activeSurveyCharges.Add(surveyCharge.net.ID.Value, permissionSetting);
        }

        private void OnEntityKill(SurveyCharge surveyCharge)
        {
            if (surveyCharge == null || surveyCharge.net == null)
            {
                return;
            }
            PermissionSettings permissionSettings;
            if (_activeSurveyCharges.TryGetValue(surveyCharge.net.ID.Value, out permissionSettings))
            {
                _activeSurveyCharges.Remove(surveyCharge.net.ID.Value);
                ModifyResourceDeposit(permissionSettings, surveyCharge.transform.position, surveyCharge.OwnerID);
            }
        }

        private void OnEntityBuilt(Planner planner, GameObject obj)
        {
            var miningQuarry = obj.ToBaseEntity() as MiningQuarry;
            if (miningQuarry == null || !miningQuarry.OwnerID.IsSteamId())
            {
                return;
            }
            for (var i = _activeCraters.Count - 1; i >= 0; i--)
            {
                var quarryData = _activeCraters[i];
                if (Vector3Ex.Distance2D(quarryData.position, miningQuarry.transform.position) < 2f)
                {
                    _storedData.quarryDataList.Add(quarryData);
                    CreateResourceDeposit(miningQuarry, quarryData);
                    _activeCraters.RemoveAt(i);
                    SaveData();
                    break;
                }
            }
        }

        private object OnEntityTakeDamage(SurveyCrater surveyCrater, HitInfo info)
        {
            if (surveyCrater == null || !surveyCrater.OwnerID.IsSteamId())
            {
                return null;
            }
            var player = info?.InitiatorPlayer;
            if (player == null || !player.userID.IsSteamId())
            {
                return _true;
            }
            if (!AreFriends(surveyCrater.OwnerID, player.userID))
            {
                Print(player, Lang("NoDamage", player.UserIDString));
                return _true;
            }
            return null;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null)
            {
                return null;
            }
            var surveyCrater = target.entity as SurveyCrater;
            if (surveyCrater == null || !surveyCrater.OwnerID.IsSteamId())
            {
                return null;
            }
            var player = planner.GetOwnerPlayer();
            if (player == null)
            {
                return null;
            }
            if (!AreFriends(surveyCrater.OwnerID, player.userID))
            {
                Print(player, Lang("NoDeploy", player.UserIDString));
                return _false;
            }
            return null;
        }

        #endregion Oxide Hooks

        #region Methods

        private int RefillMiningQuarries()
        {
            var count = 0;
            foreach (var miningQuarry in _miningQuarries)
            {
                if (miningQuarry == null || miningQuarry.IsDestroyed)
                {
                    continue;
                }
                foreach (var quarryData in _storedData.quarryDataList)
                {
                    if (quarryData == null)
                    {
                        continue;
                    }
                    if (Vector3Ex.Distance2D(quarryData.position, miningQuarry.transform.position) < 2f)
                    {
                        count++;
                        CreateResourceDeposit(miningQuarry, quarryData);
                    }
                }
            }
            return count;
        }

        private void CheckValidData()
        {
            if (_miningQuarries.Count <= 0)
            {
                return;
            }
            foreach (var quarryData in _storedData.quarryDataList.ToArray())
            {
                var validData = false;
                foreach (var miningQuarry in _miningQuarries)
                {
                    if (Vector3Ex.Distance2D(quarryData.position, miningQuarry.transform.position) < 2f)
                    {
                        validData = true;
                        break;
                    }
                }
                if (!validData)
                {
                    _storedData.quarryDataList.Remove(quarryData);
                }
            }
            SaveData();
        }

        private static void CreateResourceDeposit(MiningQuarry miningQuarry, QuarryData quarryData)
        {
            if (quarryData.isLiquid)
            {
                miningQuarry.canExtractLiquid = true;
            }
            else
            {
                miningQuarry.canExtractSolid = true;
            }

            miningQuarry._linkedDeposit._resources.Clear();
            foreach (var mineralItem in quarryData.mineralItems)
            {
                var itemDefinition = ItemManager.FindItemDefinition(mineralItem.shortname);
                if (itemDefinition == null)
                {
                    continue;
                }
                miningQuarry._linkedDeposit.Add(itemDefinition, 1f, mineralItem.amount, mineralItem.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, quarryData.isLiquid);
            }
            miningQuarry.SendNetworkUpdateImmediate();
        }

        private void ModifyResourceDeposit(PermissionSettings permissionSettings, Vector3 checkPosition, ulong playerID)
        {
            NextTick(() =>
            {
                var surveyCraterList = Pool.GetList<SurveyCrater>();
                Vis.Entities(checkPosition, 1f, surveyCraterList, Layers.Mask.Default);
                foreach (var surveyCrater in surveyCraterList)
                {
                    if (surveyCrater == null || surveyCrater.IsDestroyed)
                    {
                        continue;
                    }
                    if (_checkedCraters.Contains(surveyCrater.net?.ID.Value ?? 0))
                    {
                        continue;
                    }
                    if (Random.Range(0f, 100f) < permissionSettings.OilCraterChance)
                    {
                        var oilCrater = GameManager.server.CreateEntity(PREFAB_CRATER_OIL, surveyCrater.transform.position) as SurveyCrater;
                        if (oilCrater == null)
                        {
                            continue;
                        }
                        surveyCrater.Kill();
                        oilCrater.Spawn();
                        _checkedCraters.Add(oilCrater.net?.ID.Value ?? 0);
                        {
                            var deposit = ResourceDepositManager.GetOrCreate(oilCrater.transform.position);
                            if (deposit != null)
                            {
                                oilCrater.OwnerID = playerID;
                                deposit._resources.Clear();

                                var mineralItems = permissionSettings.PumpJack.RefillResourceDeposit(deposit);
                                _activeCraters.Add(new QuarryData
                                {
                                    position = oilCrater.transform.position,
                                    isLiquid = permissionSettings.PumpJack.IsLiquid,
                                    mineralItems = mineralItems
                                });
                            }
                        }
                    }
                    else if (Random.Range(0f, 100f) < permissionSettings.Quarry.ModifyChance)
                    {
                        var deposit = ResourceDepositManager.GetOrCreate(surveyCrater.transform.position);
                        if (deposit != null)
                        {
                            surveyCrater.OwnerID = playerID;
                            deposit._resources.Clear();

                            var mineralItems = permissionSettings.Quarry.RefillResourceDeposit(deposit);
                            _activeCraters.Add(new QuarryData
                            {
                                position = surveyCrater.transform.position,
                                isLiquid = permissionSettings.Quarry.IsLiquid,
                                mineralItems = mineralItems
                            });
                        }
                    }
                    if (!surveyCrater.IsDestroyed)
                    {
                        _checkedCraters.Add(surveyCrater.net?.ID.Value ?? 0);
                    }
                }
                Pool.FreeList(ref surveyCraterList);
            });
        }

        private PermissionSettings GetPermissionSetting(BasePlayer player)
        {
            PermissionSettings permissionSettings = null;
            var priority = 0;
            foreach (var perm in _configData.Permissions)
            {
                if (perm.Priority >= priority && permission.UserHasPermission(player.UserIDString, perm.Permission))
                {
                    priority = perm.Priority;
                    permissionSettings = perm;
                }
            }
            return permissionSettings;
        }

        #region AreFriends

        private bool AreFriends(ulong playerID, ulong friendID)
        {
            if (playerID == friendID)
            {
                return true;
            }
            if (_configData.Global.UseTeams && SameTeam(playerID, friendID))
            {
                return true;
            }
            if (_configData.Global.UseFriends && HasFriend(playerID, friendID))
            {
                return true;
            }
            if (_configData.Global.UseClans && SameClan(playerID, friendID))
            {
                return true;
            }
            return false;
        }

        private bool HasFriend(ulong playerID, ulong friendID)
        {
            if (Friends == null)
            {
                return false;
            }
            return (bool)Friends.Call("HasFriend", playerID, friendID);
        }

        private bool SameTeam(ulong playerID, ulong friendID)
        {
            if (!RelationshipManager.TeamsEnabled())
            {
                return false;
            }
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerID);
            if (playerTeam == null)
            {
                return false;
            }
            var friendTeam = RelationshipManager.ServerInstance.FindPlayersTeam(friendID);
            if (friendTeam == null)
            {
                return false;
            }
            return playerTeam == friendTeam;
        }

        private bool SameClan(ulong playerID, ulong friendID)
        {
            if (Clans == null)
            {
                return false;
            }
            //Clans
            var isMember = Clans.Call("IsClanMember", playerID.ToString(), friendID.ToString());
            if (isMember != null)
            {
                return (bool)isMember;
            }
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerID);
            if (playerClan == null)
            {
                return false;
            }
            var friendClan = Clans.Call("GetClanOf", friendID);
            if (friendClan == null)
            {
                return false;
            }
            return (string)playerClan == (string)friendClan;
        }

        #endregion AreFriends

        #endregion Methods

        #region Commands

        [ConsoleCommand("backpumpjack.refill")]
        private void CCmdRefresh(ConsoleSystem.Arg arg)
        {
            var count = RefillMiningQuarries();
            SendReply(arg, $"Refreshed {count} quarry resources.");
        }

        #endregion Commands

        #region ConfigurationFile

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public GlobalSettings Global { get; set; } = new GlobalSettings();

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings Chat { get; set; } = new ChatSettings();

            [JsonProperty(PropertyName = "Permission List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<PermissionSettings> Permissions { get; set; } = new List<PermissionSettings>
            {
                new PermissionSettings
                {
                    Permission = "backpumpjack.use",
                    Priority = 0,
                    OilCraterChance = 20f,
                    PumpJack = new PumpJackSettings
                    {
                        AmountMin = 1,
                        AmountMax = 1,
                        AllowDuplication = false,
                        MineralItems = new List<MineralItem>
                        {
                            new MineralItem
                            {
                                ShortName = "crude.oil",
                                Chance = 50f,
                                PmMin = 28.8f,
                                PmMax = 28.8f
                            },
                            new MineralItem
                            {
                                ShortName = "lowgradefuel",
                                Chance = 50f,
                                PmMin = 81.6f,
                                PmMax = 81.6f,
                            }
                        }
                    },
                    Quarry = new QuarrySettings()
                },
                new PermissionSettings
                {
                    Permission = "backpumpjack.vip",
                    Priority = 1,
                    OilCraterChance = 40f,
                    PumpJack = new PumpJackSettings
                    {
                        AmountMin = 2,
                        AmountMax = 2,
                        AllowDuplication = false,
                        MineralItems = new List<MineralItem>
                        {
                            new MineralItem
                            {
                                ShortName = "crude.oil",
                                Chance = 50f,
                                PmMin = 38f,
                                PmMax = 38f
                            },
                            new MineralItem
                            {
                                ShortName = "lowgradefuel",
                                Chance = 50f,
                                PmMin = 100f,
                                PmMax = 100f,
                            }
                        }
                    },
                    Quarry = new QuarrySettings
                    {
                        AmountMin = 1,
                        AmountMax = 3,
                        ModifyChance = 50,
                        AllowDuplication = false,
                        MineralItems = new List<MineralItem>
                        {
                            new MineralItem
                            {
                                ShortName = "stones",
                                Chance = 60f,
                                PmMin = 120f,
                                PmMax = 180f
                            },
                            new MineralItem
                            {
                                ShortName = "metal.ore",
                                Chance = 50f,
                                PmMin = 15f,
                                PmMax = 25f
                            },
                            new MineralItem
                            {
                                ShortName = "sulfur.ore",
                                Chance = 50f,
                                PmMin = 15f,
                                PmMax = 15f
                            },
                            new MineralItem
                            {
                                ShortName = "hq.metal.ore",
                                Chance = 50f,
                                PmMin = 1.5f,
                                PmMax = 2f
                            }
                        }
                    }
                }
            };

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber Version { get; set; }
        }

        private class GlobalSettings
        {
            [JsonProperty(PropertyName = "Use Teams")]
            public bool UseTeams { get; set; } = true;

            [JsonProperty(PropertyName = "Use Friends")]
            public bool UseFriends { get; set; } = true;

            [JsonProperty(PropertyName = "Use Clans")]
            public bool UseClans { get; set; } = false;

            [JsonProperty(PropertyName = "Block damage another player's survey crater")]
            public bool CantDamage { get; set; } = true;

            [JsonProperty(PropertyName = "Block deploy a quarry on another player's survey crater")]
            public bool CantDeploy { get; set; } = true;
        }

        private class ChatSettings
        {
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix { get; set; } = "<color=#00FFFF>[BackPumpJack]</color>: ";

            [JsonProperty(PropertyName = "Chat SteamID Icon")]
            public ulong SteamIdIcon { get; set; } = 0;
        }

        private class PermissionSettings
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission { get; set; }

            [JsonProperty(PropertyName = "Priority")]
            public int Priority { get; set; }

            [JsonProperty(PropertyName = "Oil Crater Chance")]
            public float OilCraterChance { get; set; }

            [JsonProperty(PropertyName = "Oil Crater Settings")]
            public PumpJackSettings PumpJack { get; set; } = new PumpJackSettings();

            [JsonProperty(PropertyName = "Normal Crater Settings")]
            public QuarrySettings Quarry { get; set; } = new QuarrySettings();
        }

        private abstract class MiningSettings
        {
            [JsonProperty(PropertyName = "Minimum Mineral Amount")]
            public int AmountMin { get; set; }

            [JsonProperty(PropertyName = "Maximum Mineral Amount")]
            public int AmountMax { get; set; }

            [JsonProperty(PropertyName = "Allow Duplication Of Mineral Item")]
            public bool AllowDuplication { get; set; }

            [JsonProperty(PropertyName = "Mineral Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MineralItem> MineralItems { get; set; } = new List<MineralItem>();

            [JsonIgnore]
            public abstract bool IsLiquid { get; }

            public abstract float GetWorkPerMinute();

            public List<MineralItemData> RefillResourceDeposit(ResourceDepositManager.ResourceDeposit deposit)
            {
                var amountsRemaining = Random.Range(AmountMin, AmountMax + 1);
                var mineralItems = new List<MineralItemData>();
                for (var i = 0; i < 200; i++)
                {
                    if (amountsRemaining <= 0)
                    {
                        break;
                    }
                    var mineralItem = MineralItems.GetRandom();
                    if (!AllowDuplication && deposit._resources.Any(x => x.type.shortname == mineralItem.ShortName))
                    {
                        continue;
                    }
                    if (Random.Range(0f, 100f) < mineralItem.Chance)
                    {
                        var itemDef = ItemManager.FindItemDefinition(mineralItem.ShortName);
                        if (itemDef != null)
                        {
                            var amount = Random.Range(10000, 100000);
                            var workNeeded = GetWorkPerMinute() / Random.Range(mineralItem.PmMin, mineralItem.PmMax);
                            deposit.Add(itemDef, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, IsLiquid);
                            mineralItems.Add(new MineralItemData
                            {
                                amount = amount,
                                shortname = itemDef.shortname,
                                workNeeded = workNeeded
                            });
                        }
                        amountsRemaining--;
                    }
                }
                return mineralItems;
            }
        }

        private class QuarrySettings : MiningSettings
        {
            [JsonProperty(PropertyName = "Modify Chance (If not modified, use default mineral)", Order = -1)]
            public float ModifyChance { get; set; }

            public static float WorkPerMinute { get; set; }
            public override bool IsLiquid => false;
            public override float GetWorkPerMinute() => WorkPerMinute;
        }

        private class PumpJackSettings : MiningSettings
        {
            public static float WorkPerMinute { get; set; }
            public override bool IsLiquid => true;
            public override float GetWorkPerMinute() => WorkPerMinute;
        }

        private class MineralItem
        {
            [JsonProperty(PropertyName = "Mineral Item Short Name")]
            public string ShortName { get; set; }

            [JsonProperty(PropertyName = "Chance")]
            public float Chance { get; set; }

            [JsonProperty(PropertyName = "Minimum pM")]
            public float PmMin { get; set; }

            [JsonProperty(PropertyName = "Maximum pM")]
            public float PmMax { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
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
            _configData = new ConfigData();
            _configData.Version = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_configData);
        }

        private void UpdateConfigValues()
        {
            if (_configData.Version < Version)
            {
                if (_configData.Version <= default(VersionNumber))
                {
                    string prefix, prefixColor;
                    if (GetConfigValue(out prefix, "Chat Settings", "Chat Prefix") && GetConfigValue(out prefixColor, "Chat Settings", "Chat Prefix Color"))
                    {
                        _configData.Chat.Prefix = $"<color={prefixColor}>{prefix}</color>: ";
                    }
                }
                _configData.Version = Version;
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

        #region DataFile

        private StoredData _storedData;

        private class StoredData
        {
            public readonly List<QuarryData> quarryDataList = new List<QuarryData>();
        }

        private class QuarryData
        {
            public Vector3 position;
            public bool isLiquid;
            public List<MineralItemData> mineralItems = new List<MineralItemData>();
        }

        private class MineralItemData
        {
            public string shortname;
            public int amount;
            public float workNeeded;
        }

        private void LoadData()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                _storedData = null;
            }
            if (_storedData == null)
            {
                ClearData();
            }
        }

        private void ClearData()
        {
            _storedData = new StoredData();
            SaveData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }

        private void OnNewSave()
        {
            ClearData();
        }

        #endregion DataFile

        #region LanguageFile

        private void Print(BasePlayer player, string message)
        {
            Player.Message(player, message, _configData.Chat.Prefix, _configData.Chat.SteamIdIcon);
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
                ["NoDamage"] = "You can't damage another player's survey crater.",
                ["NoDeploy"] = "You can't deploy a quarry on another player's survey crater."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoDamage"] = "您不能伤害别人的矿坑",
                ["NoDeploy"] = "您不能放置挖矿机到别人的矿坑上"
            }, this, "zh-CN");
        }

        #endregion LanguageFile
    }
}