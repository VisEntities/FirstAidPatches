using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

    #region Changelogs and ToDo
    /*==============================================================================================================
    *    
    *   Scriptzyy the original creator of this plugin
    *   redBDGR the previous maintainer of this plugin
    *   
    *   V2.3.0 : Added Support for Battlepass plugin
    *    
    *    
    *==============================================================================================================*/
    #endregion

namespace Oxide.Plugins
{
    [Info("Barrel Points", "Krungh Crow", "2.3.1")]
    [Description("Gives players extra rewards for destroying barrels")]
    public class BarrelPoints : RustPlugin
    {
        [PluginReference]
        Plugin Battlepass, Economics, ServerRewards;

        #region Variables

        const ulong chaticon = 76561199090290915;
        const string prefix = "<color=yellow>[Barrel Points]</color> ";

        #endregion

        private static Dictionary<string, object> _PermissionDic()
        {
            var x = new Dictionary<string, object>
            {
                {"barrelpoints.default", 2.0},
                {"barrelpoints.vip", 5.0},
                {"barrelpoints.1stcurreny", 2.0},
                {"barrelpoints.2ndcurrency", 5.0},
                {"barrelpoints.1stcurrenyvip", 10.0},
                {"barrelpoints.2ndcurrencyvip", 15.0}
            };
            return x;
        }

        private readonly Dictionary<string, int> playerInfo = new Dictionary<string, int>();
        private readonly List<ulong> crateCache = new List<ulong>();

        private Dictionary<string, object> permissionList;

        private bool changed;
        private bool useEconomy = true;
        private bool useServerRewards;
        private bool useBattlepass;
        private bool useBattlepass1;
        private bool useBattlepass2;
        private bool useItem;
        private string Itemshortname;
        private bool resetBarrelsOnDeath = true;
        private bool sendNotificationMessage = true;
        private bool useCrates;
        private bool useBarrels = true;
        private int givePointsEvery = 1;
        private void OnServerInitialized()
        {
            LoadVariables();

            foreach (var entry in permissionList)
                permission.RegisterPermission(entry.Key, this);

            timer.Once(25f, () =>
            {
                if (useBattlepass && !Battlepass)
                {
                    PrintError("Battlepass was not found! Disabling the \"Use Battlepass\" setting");
                    useBattlepass = false;
                }

                if (useEconomy && !Economics)
                {
                    PrintError("Economics was not found! Disabling the \"Use Economics\" setting");
                    useEconomy = false;
                }

                if (useServerRewards && !ServerRewards)
                {
                    PrintError("ServerRewards was not found! Disabling the \"Use ServerRewards\" setting");
                    useServerRewards = false;
                }

                if (useItem && Itemshortname == "")
                {
                    PrintError("No item for handout was set! Disabling the \"Use Item Rewards\" setting");
                    useItem = false;
                }
            });

        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            permissionList = (Dictionary<string, object>)GetConfig("Point Settings", "Permission List barrels", _PermissionDic());
            useEconomy = Convert.ToBoolean(GetConfig("Plugins", "Use Economics", true));
            useServerRewards = Convert.ToBoolean(GetConfig("Plugins", "Use ServerRewards", false));
            useItem = Convert.ToBoolean(GetConfig("Items", "Use Item Rewards", false));
            Itemshortname = Convert.ToString(GetConfig("Items", "Item Shortname", "scrap"));
            sendNotificationMessage = Convert.ToBoolean(GetConfig("Settings", "Send Notification Message", true));
            givePointsEvery = Convert.ToInt32(GetConfig("Settings", "Give Points Every x Barrels", 1));
            resetBarrelsOnDeath = Convert.ToBoolean(GetConfig("Settings", "Reset Barrel Count on Death", true));
            useBarrels = Convert.ToBoolean(GetConfig("Settings", "Give Points For Barrels", true));
            useCrates = Convert.ToBoolean(GetConfig("Settings", "Give Points For Crates", false));
            //Battlepass
            useBattlepass = Convert.ToBoolean(GetConfig("Plugins", "Use Battlepass", false));
            useBattlepass1 = Convert.ToBoolean(GetConfig("Battlepass Settings", "Use Battlepass 1st currency", false));
            useBattlepass2 = Convert.ToBoolean(GetConfig("Battlepass Settings", "Use Battlepass 2nd currency", false));



            if (!changed)
                return;

            SaveConfig();
            changed = false;
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Economy Notice (Barrel)"] = "You received <color=yellow>${0}</color> for destroying a barrel!",
                ["Economy Notice (Crate)"] = "You received <color=green>${0}</color> for looting a crate!",
                ["RP Notice (Barrel)"] = "You received <color=yellow>{0}</color> RP for destroying a barrel!",
                ["RP Notice (Crate)"] = "You received <color=green>{0}</color> RP for looting a crate!",
                ["Item Notice (Barrel)"] = "You received <color=green>{0}</color> {1} for destroying a barrel!",
                ["Item Notice (Crate)"] = "You received <color=green>{0}</color> {1} for looting a crate!",
                ["BP1 Notice (Barrel)"] = "You received <color=green>{0}</color> BP1 for destroying a barrel!",
                ["BP2 Notice (Barrel)"] = "You received <color=green>{0}</color> BP2 for looting a barrel!",
                ["BP1 Notice (Crate)"] = "You received <color=green>${0}</color> BP1 for looting a crate!",
                ["BP2 Notice (Crate)"] = "You received <color=green>${0}</color> BP2 for looting a crate!"
            }, this);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!useBarrels || info?.Initiator == null)
                return;

            if (!entity.ShortPrefabName.StartsWith("loot-barrel") && !entity.ShortPrefabName.StartsWith("loot_barrel") && entity.ShortPrefabName != "oil_barrel" && entity.ShortPrefabName != "diesel_barrel_world")//diesel_barrel_world test
                return;

            BasePlayer player = info.InitiatorPlayer;
            if (player == null || !player.IsValid())
                return;

            string userPermission = GetPermissionName(player);
            if (userPermission == null)
                return;

            // Checking for number of barrels hit
            if (!playerInfo.ContainsKey(player.UserIDString))
                playerInfo.Add(player.UserIDString, 0);

            if (playerInfo[player.UserIDString] == givePointsEvery - 1)
            {
                // Section that gives the player their money
                if (useEconomy && Economics)
                {
                    Economics.Call("Deposit", player.userID, Convert.ToDouble(permissionList[userPermission]));
                    if (sendNotificationMessage)
                    {
                        Player.Message(player, prefix + string.Format(Msg("Economy Notice (Barrel)", player.UserIDString), permissionList[userPermission]), chaticon);
                    }
                }
                if (useServerRewards && ServerRewards)
                {
                    ServerRewards.Call("AddPoints", player.userID, Convert.ToInt32(permissionList[userPermission]));
                    if (sendNotificationMessage)
                    {
                        Player.Message(player, prefix + string.Format(Msg("RP Notice (Barrel)", player.UserIDString), permissionList[userPermission]), chaticon);
                    }
                }
                if (useItem)
                {
                    {
                        Item currency = ItemManager.CreateByName(Itemshortname, Convert.ToInt32(permissionList[userPermission]));
                        currency.MoveToContainer(player.inventory.containerMain);
                        if (sendNotificationMessage)
                        {
                            Player.Message(player, prefix + string.Format(Msg("Item Notice (Barrel)", player.UserIDString), Convert.ToInt32(permissionList[userPermission]), Itemshortname), chaticon);
                        }
                    }
                }
                if (useBattlepass == true)
                {
                    if (useBattlepass1)
                    {
                        Battlepass?.Call("AddFirstCurrency", player.userID, Convert.ToInt32(permissionList[userPermission]));
                        if (sendNotificationMessage)
                            Player.Message(player, prefix + string.Format(Msg("BP1 Notice (Barrel)", player.UserIDString), permissionList[userPermission]), chaticon);
                    }

                    if (useBattlepass2)
                    {
                        Battlepass?.Call("AddSecondCurrency", player.userID, Convert.ToInt32(permissionList[userPermission]));
                        if (sendNotificationMessage)
                            Player.Message(player, prefix + string.Format(Msg("BP2 Notice (Barrel)", player.UserIDString), permissionList[userPermission]), chaticon);
                    }
                }
                playerInfo[player.UserIDString] = 0;
            }
            else
                playerInfo[player.UserIDString]++;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (!useCrates || entity == null)
                return;

            if (!entity.ShortPrefabName.Contains("crate_") 
                && entity.ShortPrefabName != "heli_crate"
                && entity.ShortPrefabName != "codelockedhackablecrate"
                && entity.ShortPrefabName != "bradley_crate")
                return;

            if (crateCache.Contains(entity.net.ID.Value))
                crateCache.Remove(entity.net.ID.Value);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!useCrates)
                return;

            if (!entity.ShortPrefabName.Contains("crate_") 
                && entity.ShortPrefabName != "heli_crate"
                && entity.ShortPrefabName != "codelockedhackablecrate"
                && entity.ShortPrefabName != "bradley_crate")
                return;

            if (crateCache.Contains(entity.net.ID.Value))
                return;

            crateCache.Add(entity.net.ID.Value);
            string userPermission = GetPermissionName(player);
            if (userPermission == null)
                return;

            if (useEconomy && Economics)
            {
                Economics.Call("Deposit", player.userID, Convert.ToDouble(permissionList[userPermission]));
                if (sendNotificationMessage)
                Player.Message(player, prefix + string.Format(Msg("Economy Notice (Crate)", player.UserIDString), permissionList[userPermission]), chaticon);
            }
            if (useServerRewards)
            {
                ServerRewards.Call("AddPoints", player.userID, Convert.ToInt32(permissionList[userPermission]));
                if (sendNotificationMessage)
                Player.Message(player, prefix + string.Format(Msg("RP Notice (Crate)", player.UserIDString), permissionList[userPermission]), chaticon);
            }
            if (useItem)
            {
                {
                    Item currency = ItemManager.CreateByName(Itemshortname, Convert.ToInt32(permissionList[userPermission]));
                    currency.MoveToContainer(player.inventory.containerMain);
                    if (sendNotificationMessage)
                        Player.Message(player, prefix + string.Format(Msg("Item Notice (Crate)", player.UserIDString), Convert.ToInt32(permissionList[userPermission]), Itemshortname), chaticon);
                }
            }
            if (useBattlepass == true)
            {
                if (useBattlepass1)
                {
                    Battlepass?.Call("AddFirstCurrency", player.userID, Convert.ToInt32(permissionList[userPermission]));
                    if (sendNotificationMessage)
                        Player.Message(player, prefix + string.Format(Msg("BP1 Notice (Crate)", player.UserIDString), permissionList[userPermission]), chaticon);
                }

                if (useBattlepass2)
                {
                    Battlepass?.Call("AddSecondCurrency", player.userID, Convert.ToInt32(permissionList[userPermission]));
                    if (sendNotificationMessage)
                        Player.Message(player, prefix + string.Format(Msg("BP2 Notice (Crate)", player.UserIDString), permissionList[userPermission]), chaticon);
                }
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (!resetBarrelsOnDeath)
                return;

            if (playerInfo.ContainsKey(player.UserIDString))
                playerInfo[player.UserIDString] = 0;
        }

        private string GetPermissionName(BasePlayer player)
        {
            KeyValuePair<string, int> _perms = new KeyValuePair<string, int>(null, 0);
            Dictionary<string, int> perms = permissionList.Where(entry => permission.UserHasPermission(player.UserIDString, entry.Key))
                .ToDictionary(entry => entry.Key, entry => Convert.ToInt32(entry.Value));
            foreach (var entry in perms)
                if (Convert.ToInt32(entry.Value) > _perms.Value)
                    _perms = new KeyValuePair<string, int>(entry.Key, Convert.ToInt32(entry.Value));
            return _perms.Key;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = (Dictionary<string, object>)Config[menu];
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                changed = true;
            }
            return value;
        }

        private string Msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
