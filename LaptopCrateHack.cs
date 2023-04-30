using System;
using System.Collections.Generic;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Laptop Crate Hack", "TheSurgeon/Arainrr", "1.1.7")]
    [Description("Require a laptop to hack a crate.")]
    public class LaptopCrateHack : RustPlugin
    {
        #region Fields

        [PluginReference] private readonly Plugin Friends, Clans;

        private const int LaptopItemId = 1523195708;

        private Dictionary<ulong, int> _extraHackTimes;
        private Dictionary<ulong, float> _coolDowns;

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            if (!_configData.ownCrate)
            {
                Unsubscribe(nameof(CanLootEntity));
            }
            if (!_configData.extraHack)
            {
                Unsubscribe(nameof(OnPlayerInput));
            }
            if (_configData.hackCooldown > 0)
            {
                _coolDowns = new Dictionary<ulong, float>();
            }
            if (_configData.maxExtraHack < 0)
            {
                Unsubscribe(nameof(OnEntityKill));
            }
            else
            {
                _extraHackTimes = new Dictionary<ulong, int>();
            }
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate == null || crate.OwnerID.IsSteamId())
            {
                return null;
            }
            if (_configData.requireInHand)
            {
                var activeItem = player.GetActiveItem();
                if (activeItem == null || activeItem.info.itemid != LaptopItemId)
                {
                    Print(player, Lang("NotHolding", player.UserIDString));
                    return false;
                }
            }
            if (_configData.hackCooldown > 0)
            {
                float lastUse;
                if (_coolDowns.TryGetValue(player.userID, out lastUse))
                {
                    var timeLeft = _configData.hackCooldown - (Time.realtimeSinceStartup - lastUse);
                    if (timeLeft > 0)
                    {
                        Print(player, Lang("OnCooldown", player.UserIDString, Mathf.CeilToInt(timeLeft)));
                        return false;
                    }
                }
            }
            if (_configData.numberRequired > 0)
            {
                var amount = player.inventory.GetAmount(LaptopItemId);
                if (amount < _configData.numberRequired)
                {
                    Print(player, Lang("YouNeed", player.UserIDString, _configData.numberRequired, amount));
                    return false;
                }
                if (_configData.consumeLaptop)
                {
                    List<Item> collect = Pool.GetList<Item>();
                    player.inventory.Take(collect, LaptopItemId, _configData.numberRequired);
                    foreach (Item item in collect)
                    {
                        item.Remove();
                    }
                    Pool.FreeList(ref collect);
                }
                if (_configData.unlockTime > 0)
                {
                    crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - _configData.unlockTime;
                }
            }
            if (_configData.ownCrate)
            {
                crate.OwnerID = player.userID;
            }
            if (_configData.hackCooldown > 0)
            {
                _coolDowns[player.userID] = Time.realtimeSinceStartup;
            }
            return null;
        }

        private object CanLootEntity(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate.OwnerID.IsSteamId() && !AreFriends(crate.OwnerID, player.userID))
            {
                Print(player, Lang("YouDontOwn", player.UserIDString));
                return false;
            }
            return null;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null)
            {
                return;
            }
            if (input.WasJustPressed(BUTTON.USE))
            {
                var activeItem = player.GetActiveItem();
                if (activeItem != null && activeItem.info.itemid == LaptopItemId)
                {
                    var crate = GetEntityLookingAt(player);
                    if (crate == null || crate.net == null || crate.IsFullyHacked() || !crate.IsBeingHacked())
                    {
                        return;
                    }

                    if (crate.hackSeconds > HackableLockedCrate.requiredHackSeconds)
                    {
                        return;
                    }
                    if (_configData.maxExtraHack > 0)
                    {
                        int times;
                        if (_extraHackTimes.TryGetValue(crate.net.ID.Value, out times) && times >= _configData.maxExtraHack)
                        {
                            return;
                        }

                        if (_extraHackTimes.ContainsKey(crate.net.ID.Value))
                        {
                            _extraHackTimes[crate.net.ID.Value]++;
                        }
                        else
                        {
                            _extraHackTimes.Add(crate.net.ID.Value, 1);
                        }
                    }

                    activeItem.UseItem();
                    crate.hackSeconds += _configData.extraUnlockTime;
                }
            }
        }

        private void OnEntityKill(HackableLockedCrate crate)
        {
            if (crate == null || crate.net == null) return;
            _extraHackTimes.Remove(crate.net.ID.Value);
        }

        #endregion Oxide Hooks

        #region Methods

        private bool AreFriends(ulong playerID, ulong friendID)
        {
            if (playerID == friendID) return true;
            if (_configData.useTeams && SameTeam(playerID, friendID)) return true;
            if (_configData.useFriends && HasFriend(playerID, friendID)) return true;
            if (_configData.useClans && SameClan(playerID, friendID)) return true;
            return false;
        }

        private bool HasFriend(ulong playerID, ulong friendID)
        {
            if (Friends == null) return false;
            return (bool)Friends.Call("HasFriend", playerID, friendID);
        }

        private bool SameTeam(ulong playerID, ulong friendID)
        {
            if (!RelationshipManager.TeamsEnabled()) return false;
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerID);
            if (playerTeam == null) return false;
            var friendTeam = RelationshipManager.ServerInstance.FindPlayersTeam(friendID);
            if (friendTeam == null) return false;
            return playerTeam == friendTeam;
        }

        private bool SameClan(ulong playerID, ulong friendID)
        {
            if (Clans == null) return false;
            //Clans
            var isMember = Clans.Call("IsClanMember", playerID.ToString(), friendID.ToString());
            if (isMember != null) return (bool)isMember;
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerID);
            if (playerClan == null) return false;
            var friendClan = Clans.Call("GetClanOf", friendID);
            if (friendClan == null) return false;
            return (string)playerClan == (string)friendClan;
        }

        #endregion Methods

        #region Helpers

        private static HackableLockedCrate GetEntityLookingAt(BasePlayer player)
        {
            RaycastHit hitInfo;
            return Physics.Raycast(player.eyes.HeadRay(), out hitInfo, 10f, Rust.Layers.Solid) ? hitInfo.GetEntity() as HackableLockedCrate : null;
        }

        #endregion Helpers

        #region ConfigurationFile

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Require laptop to be in hand when start unlocking")]
            public bool requireInHand = false;

            [JsonProperty(PropertyName = "Laptops required to start unlocking (0 = Disable)")]
            public int numberRequired = 1;

            [JsonProperty(PropertyName = "Laptop consumed when start unlocking")]
            public bool consumeLaptop = true;

            [JsonProperty(PropertyName = "Hack crate unlock time (Seconds)")]
            public float unlockTime = 900f;

            [JsonProperty(PropertyName = "Use additional hack (Use laptop to reduce crate unlocking time)")]
            public bool extraHack = false;

            [JsonProperty(PropertyName = "Maximum times of additional hack (0 = Disable)")]
            public int maxExtraHack = 0;

            [JsonProperty(PropertyName = "When a laptop consumed, how much unlock time reduces? (Seconds)")]
            public float extraUnlockTime = 300f;

            [JsonProperty(PropertyName = "Only player that hacked can loot?")]
            public bool ownCrate = false;

            [JsonProperty(PropertyName = "Hack cooldown")]
            public float hackCooldown = 0;

            [JsonProperty(PropertyName = "Use Teams")]
            public bool useTeams = false;

            [JsonProperty(PropertyName = "Use Friends")]
            public bool useFriends = false;

            [JsonProperty(PropertyName = "Use Clans")]
            public bool useClans = false;

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings chatS = new ChatSettings();

            public class ChatSettings
            {
                [JsonProperty(PropertyName = "Chat Prefix")]
                public string prefix = "<color=#00FFFF>[LaptopCrateHack]</color>: ";

                [JsonProperty(PropertyName = "Chat SteamID Icon")]
                public ulong steamIDIcon = 0;
            }
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
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion ConfigurationFile

        #region LanguageFile

        private void Print(BasePlayer player, string message) => Player.Message(player, message, _configData.chatS.prefix, _configData.chatS.steamIDIcon);

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
                ["YouNeed"] = "You need <color=#FF1919>{0}</color> Targeting Computers and you only have <color=#009EFF>{1}</color>.",
                ["NotHolding"] = "You must be holding a Targeting Computer in your hand to hack this crate.",
                ["YouDontOwn"] = "Only the player that hacked this crate can loot it.",
                ["OnCooldown"] = "You must wait <color=#FF1919>{0}</color> seconds before you can hack the next crate."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["YouNeed"] = "破解黑客箱需要 <color=#FF1919>{0}</color> 个计算机，但是您只有 <color=#009EFF>{1}</color> 个.",
                ["NotHolding"] = "您手上必须拿着计算机才可以破解黑客箱",
                ["YouDontOwn"] = "只有破解这个黑客箱的玩家才可以掠夺它",
                ["OnCooldown"] = "您必须等待 <color=#FF1919>{0}</color> 秒后才能解锁下个黑客箱"
            }, this, "zh-CN");
        }

        #endregion LanguageFile
    }
}