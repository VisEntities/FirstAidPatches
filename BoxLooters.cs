using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("BoxLooters", "4seti / k1lly0u", "0.3.6")]
    [Description("Log looters for a containers")]
    class BoxLooters : RustPlugin
    {
        #region Fields
        private BoxDS boxData;
        private PlayerDS playerData;
        private DynamicConfigFile bdata;
        private DynamicConfigFile pdata;

        private static BoxLooters ins;
        
        private bool eraseData = false;        
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            bdata = Interface.Oxide.DataFileSystem.GetFile("Boxlooters/box_data");
            pdata = Interface.Oxide.DataFileSystem.GetFile("Boxlooters/player_data");
            
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission("boxlooters.checkbox", this);
        }

        private void OnServerInitialized()
        {
            ins = this;
            LoadVariables();
            LoadData();

            if (eraseData)
                ClearAllData();
            else RemoveOldData();
        }

        private void OnNewSave(string filename) => eraseData = true;

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            SaveData();
            ins = null;
        }

        private void OnLootEntity(BasePlayer looter, BaseEntity entity)
        {
            if (looter == null || entity == null || !entity.IsValid() || !IsValidType(entity)) return;

            double time = GrabCurrentTime();
            string date = DateTime.Now.ToString("d/M @ HH:mm:ss");
            
            if (entity is BasePlayer)
            {
                if (!configData.LogPlayerLoot)
                    return;

                BasePlayer looted = entity.ToPlayer();

                if (!playerData.players.ContainsKey(looted.userID))
                    playerData.players[looted.userID] = new PlayerData(looter, time, date);
                else playerData.players[looted.userID].AddLooter(looter, time, date);                
            }
            else
            {
                if (!configData.LogBoxLoot)
                    return;

                ulong netId = entity.net.ID.Value;

                if (!boxData.boxes.ContainsKey(netId))
                    boxData.boxes[netId] = new BoxData(looter, time, date, entity.transform.position);
                else boxData.boxes[netId].AddLooter(looter, time, date); 
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || !entity.IsValid() || !IsValidType(entity) || entity is BasePlayer)
                return;

            if (hitInfo?.Initiator is BasePlayer)
            {
                ulong netId = entity.net.ID.Value;
                if (!boxData.boxes.ContainsKey(netId))
                    return;
                boxData.boxes[netId].OnDestroyed(hitInfo.InitiatorPlayer);
            }
        }
        #endregion

        #region Data Cleanup
        private void ClearAllData()
        {
            PrintWarning("Detected map wipe, resetting loot data!");
            boxData.boxes.Clear();
            playerData.players.Clear();
            SaveData();
        }

        private void RemoveOldData()
        {
            PrintWarning("Attempting to remove old log entries");
            int boxCount = 0;
            int playerCount = 0;
            double time = GrabCurrentTime() - (configData.RemoveHours * 3600);

            for (int i = 0; i < boxData.boxes.Count; i++)
            {
                KeyValuePair<ulong, BoxData> boxEntry = boxData.boxes.ElementAt(i);
                if (boxEntry.Value.lastAccess < time)
                {
                    boxData.boxes.Remove(boxEntry.Key);
                    ++boxCount;
                }
            }
            PrintWarning($"Removed {boxCount} old records from BoxData");

            for (int i = 0; i < playerData.players.Count; i++)
            {
                KeyValuePair<ulong, PlayerData> playerEntry = playerData.players.ElementAt(i);
                if (playerEntry.Value.lastAccess < time)
                {
                    playerData.players.Remove(playerEntry.Key);
                    ++playerCount;
                }
            }
            PrintWarning($"Removed {playerCount} old records from PlayerData");
        }
        #endregion

        #region Functions
        private object FindBoxFromRay(BasePlayer player)
        {
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 20))
                return null;

            BaseEntity hitEnt = hit.collider.GetComponentInParent<BaseEntity>();
            if (hitEnt != null)
            {
                if (IsValidType(hitEnt))
                    return hitEnt;
            }
            return null;            
        }

        private void ReplyInfo(BasePlayer player, string Id, bool isPlayer = false, string additional = "")
        {
            string entId = Id;
            if (!string.IsNullOrEmpty(additional))
                entId = $"{additional} - {Id}";

            if (!isPlayer)
            {                
                if (boxData.boxes.ContainsKey(uint.Parse(Id)))
                {
                    BoxData box = boxData.boxes[uint.Parse(Id)];
                    SendReply(player, string.Format(msg("BoxInfo", player.userID), entId));

                    if (!string.IsNullOrEmpty(box.killerName))
                        SendReply(player, string.Format(msg("DetectDestr", player.userID), box.killerName, box.killerId));

                    int i = 1;
                    string response1 = string.Empty;
                    string response2 = string.Empty;

                    foreach (LootList.LootEntry data in box.lootList.GetLooters().Reverse().Take(10))
                    {
                        string respString = string.Format(msg("DetectedLooters", player.userID), i, data.userName, data.userId, data.firstLoot, data.lastLoot);
                        if (i < 6) response1 += respString;
                        else response2 += respString;
                        i++;                        
                    }
                    SendReply(player, response1);
                    SendReply(player, response2);
                }
                else SendReply(player, string.Format(msg("NoLooters", player.userID), entId));
            }
            else
            {
                if (playerData.players.ContainsKey(ulong.Parse(Id)))
                {
                    SendReply(player, string.Format(msg("PlayerData", player.userID), entId));

                    int i = 1;
                    string response1 = string.Empty;
                    string response2 = string.Empty;
                    foreach (LootList.LootEntry data in playerData.players[ulong.Parse(Id)].lootList.GetLooters().Reverse().Take(10))
                    {
                        string respString = string.Format(msg("DetectedLooters", player.userID), i, data.userName, data.userId, data.firstLoot, data.lastLoot);
                        if (i < 6) response1 += respString;
                        else response2 += respString;
                        i++;
                    }
                    SendReply(player, response1);
                    SendReply(player, response2);
                }
                else SendReply(player, string.Format(msg("NoLootersPlayer", player.userID), entId));
            }
        }
        #endregion

        #region Helpers
        private double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private bool HasPermission(BasePlayer player) => permission.UserHasPermission(player.UserIDString, "boxlooters.checkbox") || player.net.connection.authLevel > 0;

        private float GetDistance(Vector3 init, Vector3 target) => Vector3.Distance(init, target);

        private bool IsValidType(BaseEntity entity) => !entity.GetComponent<LootContainer>() && (entity is StorageContainer || entity is MiningQuarry || entity is ResourceExtractorFuelStorage || entity is BasePlayer);
        #endregion

        #region Commands
        [ChatCommand("box")]
        private void cmdBox(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player)) return;
            if (args == null || args.Length == 0)
            {
                object success = FindBoxFromRay(player);
                if (success is MiningQuarry)
                {
                    List<BaseEntity> children = (success as MiningQuarry).children;
                    if (children != null)
                    {
                        foreach (BaseEntity child in children)
                        {
                            if (child.GetComponent<StorageContainer>())
                            {
                                ReplyInfo(player, child.net.ID.ToString(), false, child.ShortPrefabName);
                            }
                        }
                    }
                    else SendReply(player, msg("Nothing", player.userID));
                }
                else if (success is BasePlayer)
                    ReplyInfo(player, (success as BasePlayer).UserIDString, true);
                else if (success is BaseEntity)
                    ReplyInfo(player, (success as BaseEntity).net.ID.ToString());

                else SendReply(player, msg("Nothing", player.userID));
                return;
            }
            switch (args[0].ToLower())
            {
                case "help":
                    {
                        SendReply(player, $"<color=#4F9BFF>{Title}  v{Version}</color>");
                        SendReply(player, "<color=#4F9BFF>/box help</color> - Display the help menu");
                        SendReply(player, "<color=#4F9BFF>/box</color> - Retrieve information on the box you are looking at");                        
                        SendReply(player, "<color=#4F9BFF>/box id <number></color> - Retrieve information on the specified box");
                        SendReply(player, "<color=#4F9BFF>/box near <opt:radius></color> - Show nearby boxes (current and destroyed) and their ID numbers");
                        SendReply(player, "<color=#4F9BFF>/box player <partialname/id></color> - Retrieve loot information about a player");
                        SendReply(player, "<color=#4F9BFF>/box clear</color> - Clears all saved data");
                        SendReply(player, "<color=#4F9BFF>/box save</color> - Saves box data");
                    }
                    return;
                case "id":
                    if (args.Length >= 2)
                    {
                        uint id;
                        if (uint.TryParse(args[1], out id))                        
                            ReplyInfo(player, id.ToString());                        
                        else SendReply(player, msg("NoID", player.userID));
                        return;
                    }
                    break;
                case "near":
                    {
                        float radius = 20f;
                        if (args.Length >= 2)
                        {
                            if (!float.TryParse(args[1], out radius))
                                radius = 20f;
                        }
                        foreach(KeyValuePair<ulong, BoxData> box in boxData.boxes)
                        {
                            if (GetDistance(player.transform.position, box.Value.GetPosition()) <= radius)
                            {
                                player.SendConsoleCommand("ddraw.text", 20f, Color.green, box.Value.GetPosition() + new Vector3(0, 1.5f, 0), $"<size=40>{box.Key}</size>");
                                player.SendConsoleCommand("ddraw.box", 20f, Color.green, box.Value.GetPosition(), 1f);
                            }
                        }
                    }
                    return;
                case "player":
                    if (args.Length >= 2)
                    {
                        IPlayer target = covalence.Players.FindPlayer(args[1]);
                        if (target != null)                        
                            ReplyInfo(player, target.Id, true);
                        else SendReply(player, msg("NoPlayer", player.userID));
                        return;
                    }
                    break;
                case "clear":
                    boxData.boxes.Clear();
                    playerData.players.Clear();
                    SendReply(player, msg("ClearData", player.userID));
                    return;
                case "save":
                    SaveData();
                    SendReply(player, msg("SavedData", player.userID));
                    return;
                default:
                    break;
            }
            SendReply(player, msg("SynError", player.userID));
        }
        #endregion

        #region Config        
        private ConfigData configData;

        private class ConfigData
        {
            public int RemoveHours { get; set; }  
            public int RecordsPerContainer { get; set; } 
            public bool LogPlayerLoot { get; set; }
            public bool LogBoxLoot { get; set; }         
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData config = new ConfigData
            {
                RemoveHours = 48,
                RecordsPerContainer = 10,
                LogBoxLoot = true,
                LogPlayerLoot = true
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management        
        private class BoxData
        {
            public float x, y, z;
            public string killerId, killerName;
            public LootList lootList;
            public double lastAccess;

            public BoxData() { }
            public BoxData(BasePlayer player, double time, string date, Vector3 pos)
            {
                x = pos.x;
                y = pos.y;
                z = pos.z;
                lootList = new LootList(player, date);
                lastAccess = time;
            }
            public void AddLooter(BasePlayer looter, double time, string date)
            {
                lootList.AddEntry(looter, date);
                lastAccess = time;
            }

            public void OnDestroyed(BasePlayer killer)
            {
                killerId = killer.UserIDString;
                killerName = killer.displayName;
            }
            public Vector3 GetPosition() => new Vector3(x, y, z);            
        }

        private class PlayerData
        {
            public LootList lootList;
            public double lastAccess;

            public PlayerData() { }
            public PlayerData(BasePlayer player, double time, string date)
            {
                lootList = new LootList(player, date);
                lastAccess = time;
            }
            public void AddLooter(BasePlayer looter, double time, string date)
            {
                lootList.AddEntry(looter, date);
                lastAccess = time;
            }        
        }

        private class LootList
        {
            public List<LootEntry> looters;

            public LootList() { }
            public LootList(BasePlayer player, string date)
            {
                looters = new List<LootEntry>();
                looters.Add(new LootEntry(player, date));
            }
            public void AddEntry(BasePlayer player, string date)
            {
                LootEntry lastEntry = null;
                try { lastEntry = looters.Single(x => x.userId == player.UserIDString); } catch { }                 
                if (lastEntry != null)
                {
                    looters.Remove(lastEntry);
                    lastEntry.lastLoot = date;
                }
                else
                {
                    if (looters.Count == ins.configData.RecordsPerContainer)
                        looters.Remove(looters.ElementAt(0));
                    lastEntry = new LootEntry(player, date);
                }
                looters.Add(lastEntry);
            }
            public LootEntry[] GetLooters() => looters.ToArray();

            public class LootEntry
            {
                public string userId, userName, firstLoot, lastLoot;
                            
                public LootEntry() { }
                public LootEntry(BasePlayer player, string firstLoot)
                {
                    userId = player.UserIDString;
                    userName = player.displayName;
                    this.firstLoot = firstLoot;
                    lastLoot = firstLoot;                    
                }
            }
        }

        private void SaveData()
        {
            if (configData.LogBoxLoot)            
                bdata.WriteObject(boxData);
            
            if (configData.LogPlayerLoot)            
                pdata.WriteObject(playerData);
            
            PrintWarning("Saved Boxlooters data");
        }

        private void LoadData()
        {            
            try
            {
                boxData = bdata.ReadObject<BoxDS>();
            }
            catch
            {
                boxData = new BoxDS();
            }
            try
            {
                playerData = pdata.ReadObject<PlayerDS>();
            }
            catch
            {
                playerData = new PlayerDS();                
            }
        }

        private class BoxDS
        {
            public Hash<ulong, BoxData> boxes = new Hash<ulong, BoxData>();
        }

        private class PlayerDS
        {
            public Hash<ulong, PlayerData> players = new Hash<ulong, PlayerData>();
        }
        #endregion

        #region Localization
        private string msg(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId == 0U ? null : playerId.ToString());

        private Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"BoxInfo", "List of looters for this Box [<color=#F5D400>{0}</color>]:"},
            {"PlayerData", "List of looters for this Player [<color=#F5D400>{0}</color>]:"},            
            {"DetectedLooters", "<color=#F5D400>[{0}]</color><color=#4F9BFF>{1}</color> ({2})\nF:<color=#F80>{3}</color> L:<color=#F80>{4}</color>\n"},
            {"DetectDestr", "Destoyed by: <color=#4F9BFF>{0}</color> ID:{1}"},
            {"NoLooters", "<color=#4F9BFF>The box [{0}] is clear!</color>"},
            {"NoLootersPlayer", "<color=#4F9BFF>The player [{0}] is clear!</color>"},
            {"Nothing", "<color=#4F9BFF>Unable to find a valid entity</color>"},
            {"NoID", "<color=#4F9BFF>You must enter a valid entity ID</color>"},
            {"NoPlayer",  "No players with that name/ID found!"},
            {"SynError", "<color=#F5D400>Syntax Error: Type '/box' to view available options</color>" },
            {"SavedData", "You have successfully saved loot data" },
            {"ClearData", "You have successfully cleared all loot data" }
        };
        #endregion
    }
}
