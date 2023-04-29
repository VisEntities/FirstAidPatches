using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Facepunch.Extend;
using CompanionServer.Handlers;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Logger", "AK", "2.4.1")]
    [Description("Logs admin commands usage in a file and console.")]
    internal class AdminLogger : CovalencePlugin
    {
        [PluginReference] private Plugin Vanish, AdminRadar, NightVision, ConvertStatus, InventoryViewer, PlayerAdministration, Freeze, Backpacks;

        #region Vars

        private Dictionary<ulong, bool> noclipState = new Dictionary<ulong, bool>();
        private Dictionary<ulong, bool> godmodeState = new Dictionary<ulong, bool>();
        private Dictionary<ulong, bool> spectateState = new Dictionary<ulong, bool>();
        private HashSet<BasePlayer> adminList = new HashSet<BasePlayer>();

        #endregion Vars

        #region Config       

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Log to console (true/false)")]
            public bool LogToConsole { get; set; }

            [JsonProperty(PropertyName = "Update frequency (s)")]
            public float UpdateFreq { get; set; }

            [JsonProperty(PropertyName = "Log filename")]
            public string LogFileName { get; set; }

            [JsonProperty(PropertyName = "Enable Discord Messages (true/false)")]
            public bool DiscordLog { get; set; }

            [JsonProperty(PropertyName = "Discord Messages webhook")]
            public string DiscordWebhook { get; set; }

            [JsonProperty(PropertyName = "Exclude List")]
            public List<string> ExcludeList { get; set; }

            [JsonProperty(PropertyName = "Default admin commands")]
            public DefaultCommandsOptions DefaultCommands { get; set; }

            [JsonProperty(PropertyName = "Admin plugins")]
            public PluginsCommandsOptions PluginsCommands { get; set; }

            public class DefaultCommandsOptions
            {
                [JsonProperty(PropertyName = "Admin connections logging (true/false)")]
                public bool ConnectionLog { get; set; }

                [JsonProperty(PropertyName = "Noclip logging (true/false)")]
                public bool NoclipLog { get; set; }

                [JsonProperty(PropertyName = "GodMode logging (true/false)")]
                public bool GodmodeLog { get; set; }

                [JsonProperty(PropertyName = "Spectate logging (true/false)")]
                public bool SpectateLog { get; set; }

                [JsonProperty(PropertyName = "Kill player logging (true/false)")]
                public bool KillPlayerLog { get; set; }

                [JsonProperty(PropertyName = "Admin events logging (true/false)")]
                public bool EventsAllLog { get; set; }

                [JsonProperty(PropertyName = "Admin event commands")]
                public EventsLoggingOptions EventsLogging { get; set; }

                [JsonProperty(PropertyName = "Kick logging (true/false)")]
                public bool KickAllLog { get; set; }

                [JsonProperty(PropertyName = "Kick commands")]
                public KickLoggingOptions KickLogging { get; set; }

                [JsonProperty(PropertyName = "Ban logging (true/false)")]
                public bool BanAllLog { get; set; }

                [JsonProperty(PropertyName = "Ban commands")]
                public BanLoggingOptions BanLogging { get; set; }

                [JsonProperty(PropertyName = "Mute logging (true/false)")]
                public bool MuteAllLog { get; set; }

                [JsonProperty(PropertyName = "Mute commands")]
                public MuteLoggingOptions MuteLogging { get; set; }

                [JsonProperty(PropertyName = "Entity logging (true/false)")]
                public bool EntAllLog { get; set; }

                [JsonProperty(PropertyName = "Entity commands")]
                public EntityLoggingOptions EntityLogging { get; set; }

                [JsonProperty(PropertyName = "Teleport logging (true/false)")]
                public bool TeleportAllLog { get; set; }

                [JsonProperty(PropertyName = "Teleport commands")]
                public TeleportLoggingOptions TeleportLogging { get; set; }

                [JsonProperty(PropertyName = "Give items logging (true/false)")]
                public bool GiveAllLog { get; set; }

                [JsonProperty(PropertyName = "Give commands")]
                public GiveLoggingOptions GiveLogging { get; set; }

                [JsonProperty(PropertyName = "Spawn logging (true/false)")]
                public bool SpawnAllLog { get; set; }

                [JsonProperty(PropertyName = "Spawn commands")]
                public SpawnLoggingOptions SpawnLogging { get; set; }
            }

            public class PluginsCommandsOptions
            {
                [JsonProperty(PropertyName = "Vanish logging (true/false)")]
                public bool VanishLog { get; set; }

                [JsonProperty(PropertyName = "Admin Radar logging (true/false)")]
                public bool RadarLog { get; set; }

                [JsonProperty(PropertyName = "Night Vision logging (true/false)")]
                public bool NightLog { get; set; }

                [JsonProperty(PropertyName = "Convert Status logging (true/false)")]
                public bool ConvertLog { get; set; }

                [JsonProperty(PropertyName = "Inventory Viewer logging (true/false)")]
                public bool InventoryViewerLog { get; set; }

                [JsonProperty(PropertyName = "Backpacks logging (true/false)")]
                public bool BackpacksLog { get; set; }

                [JsonProperty(PropertyName = "Freeze logging (true/false)")]
                public bool FreezeAllLog { get; set; }

                [JsonProperty(PropertyName = "Freeze commands")]
                public FreezeLoggingOptions FreezeLogging { get; set; }

                [JsonProperty(PropertyName = "Player Administration logging (true/false)")]
                public bool PlayerAdministrationAllLog { get; set; }

                [JsonProperty(PropertyName = "Player Administration commands")]
                public PlayerAdministrationLoggingOptions PlayerAdministrationLogging { get; set; }
            }

            public class EventsLoggingOptions
            {
                [JsonProperty(PropertyName = "[Attack Heli] heli.call")]
                public bool HeliCallLog { get; set; }

                [JsonProperty(PropertyName = "[Attack Heli] heli.calltome")]
                public bool HeliCallToMeLog { get; set; }

                [JsonProperty(PropertyName = "[Attack Heli] drop")]
                public bool HeliDropLog { get; set; }

                [JsonProperty(PropertyName = "[Airdrop] supply.call")]
                public bool AirdropRandomLog { get; set; }

                [JsonProperty(PropertyName = "[Airdrop] supply.drop")]
                public bool AirdropPosLog { get; set; }
            }

            public class KickLoggingOptions
            {
                [JsonProperty(PropertyName = "kick")]
                public bool KickLog { get; set; }

                [JsonProperty(PropertyName = "kickall")]
                public bool KickEveryoneLog { get; set; }
            }

            public class BanLoggingOptions
            {
                [JsonProperty(PropertyName = "ban")]
                public bool BanLog { get; set; }

                [JsonProperty(PropertyName = "unban")]
                public bool UnbanLog { get; set; }
            }

            public class MuteLoggingOptions
            {
                [JsonProperty(PropertyName = "mute")]
                public bool MuteLog { get; set; }

                [JsonProperty(PropertyName = "unmute")]
                public bool UnmuteLog { get; set; }
            }

            public class EntityLoggingOptions
            {
                [JsonProperty(PropertyName = "ent kill")]
                public bool EntKillLog { get; set; }

                [JsonProperty(PropertyName = "ent who")]
                public bool EntWhoLog { get; set; }

                [JsonProperty(PropertyName = "ent lock")]
                public bool EntLockLog { get; set; }

                [JsonProperty(PropertyName = "ent unlock")]
                public bool EntUnlockLog { get; set; }

                [JsonProperty(PropertyName = "ent auth")]
                public bool EntAuthLog { get; set; }
            }

            public class TeleportLoggingOptions
            {
                [JsonProperty(PropertyName = "teleport")]
                public bool TeleportLog { get; set; }

                [JsonProperty(PropertyName = "teleportpos")]
                public bool TeleportPosLog { get; set; }

                [JsonProperty(PropertyName = "teleport2me")]
                public bool TeleportToMeLog { get; set; }
            }

            public class GiveLoggingOptions
            {
                [JsonProperty(PropertyName = "give")]
                public bool GiveLog { get; set; }

                [JsonProperty(PropertyName = "giveid")]
                public bool GiveIdLog { get; set; }

                [JsonProperty(PropertyName = "givearm")]
                public bool GiveArmLog { get; set; }

                [JsonProperty(PropertyName = "giveto")]
                public bool GiveToLog { get; set; }

                [JsonProperty(PropertyName = "giveall")]
                public bool GiveAllLog { get; set; }
            }

            public class SpawnLoggingOptions
            {
                [JsonProperty(PropertyName = "spawn")]
                public bool SpawnLog { get; set; }

                [JsonProperty(PropertyName = "spawnat")]
                public bool SpawnAtLog { get; set; }

                [JsonProperty(PropertyName = "spawnhere")]
                public bool SpawnHereLog { get; set; }

                [JsonProperty(PropertyName = "spawnitem")]
                public bool SpawnItemLog { get; set; }
            }

            public class FreezeLoggingOptions
            {
                [JsonProperty(PropertyName = "freeze")]
                public bool FreezeLog { get; set; }

                [JsonProperty(PropertyName = "unfreeze")]
                public bool UnfreezeLog { get; set; }

                [JsonProperty(PropertyName = "freezeall")]
                public bool AllFreezeLog { get; set; }

                [JsonProperty(PropertyName = "unfreezeall")]
                public bool AllUnfreezeLog { get; set; }
            }

            public class PlayerAdministrationLoggingOptions
            {

                [JsonProperty(PropertyName = "OpenPadminCmd")]
                public bool OpenPadminCmdLog { get; set; }

                [JsonProperty(PropertyName = "ClosePadminCmd")]
                public bool ClosePadminCmdLog { get; set; }

                [JsonProperty(PropertyName = "BanUserCmd")]
                public bool BanUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "UnbanUserCmd")]
                public bool UnbanUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "KickUserCmd")]
                public bool KickUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "MuteUserCmd")]
                public bool MuteUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "UnmuteUserCmd")]
                public bool UnmuteUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "FreezeCmd")]
                public bool FreezeCmdLog { get; set; }

                [JsonProperty(PropertyName = "UnreezeCmd")]
                public bool UnreezeCmdLog { get; set; }

                [JsonProperty(PropertyName = "BackpackViewCmd")]
                public bool BackpackViewCmdLog { get; set; }

                [JsonProperty(PropertyName = "InventoryViewCmd")]
                public bool InventoryViewCmdLog { get; set; }

                [JsonProperty(PropertyName = "ClearUserInventoryCmd")]
                public bool ClearUserInventoryCmdLog { get; set; }

                [JsonProperty(PropertyName = "ResetUserBPCmd")]
                public bool ResetUserBPCmdLog { get; set; }

                [JsonProperty(PropertyName = "ResetUserMetabolismCmd")]
                public bool ResetUserMetabolismCmdLog { get; set; }

                [JsonProperty(PropertyName = "RecoverUserMetabolismCmd")]
                public bool RecoverUserMetabolismLog { get; set; }

                [JsonProperty(PropertyName = "TeleportToUserCmd")]
                public bool TeleportToUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "TeleportUserCmd")]
                public bool TeleportUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "SpectateUserCmd")]
                public bool SpectateUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "PermsCmd")]
                public bool PermsCmdLog { get; set; }

                [JsonProperty(PropertyName = "HurtUserCmd")]
                public bool HurtUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "KillUserCmd")]
                public bool KillUserCmdLog { get; set; }

                [JsonProperty(PropertyName = "HealUserCmd")]
                public bool HealUserCmdLog { get; set; }

            }
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
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                LogToConsole = true,
                UpdateFreq = 5f,
                LogFileName = "adminlog",
                DiscordLog = true,
                DiscordWebhook = "",
                ExcludeList = new List<string>(){
                    "76561197960279927",
                    "76561197960287930"
                },

                DefaultCommands = new ConfigData.DefaultCommandsOptions
                {
                    ConnectionLog = true,
                    NoclipLog = true,
                    GodmodeLog = true,
                    SpectateLog = true,
                    KillPlayerLog = true,   
                    
                    EventsAllLog = true,
                    EventsLogging = new ConfigData.EventsLoggingOptions
                    {
                        HeliCallLog = true,
                        HeliCallToMeLog = true,
                        HeliDropLog = true,
                        AirdropRandomLog = true,
                        AirdropPosLog = true
                    },

                    KickAllLog = true,
                    KickLogging = new ConfigData.KickLoggingOptions
                    {
                        KickLog = true,
                        KickEveryoneLog = true
                    },

                    BanAllLog = true,
                    BanLogging = new ConfigData.BanLoggingOptions
                    {
                        BanLog = true,                       
                        UnbanLog = true
                    },

                    MuteAllLog = true,
                    MuteLogging = new ConfigData.MuteLoggingOptions
                    {
                        MuteLog = true,
                        UnmuteLog = true
                    },

                    EntAllLog = true,
                    EntityLogging = new ConfigData.EntityLoggingOptions
                    {
                        EntKillLog = true,
                        EntWhoLog = true,
                        EntLockLog = true,
                        EntUnlockLog = true,
                        EntAuthLog = true
                    },

                    TeleportAllLog = true,
                    TeleportLogging = new ConfigData.TeleportLoggingOptions
                    {
                        TeleportLog = true,
                        TeleportPosLog = true,
                        TeleportToMeLog = true

                    },

                    GiveAllLog = true,
                    GiveLogging = new ConfigData.GiveLoggingOptions
                    {
                        GiveLog = true,
                        GiveIdLog = true,
                        GiveArmLog = true,
                        GiveToLog = true,
                        GiveAllLog = true
                    },

                    SpawnAllLog = true,
                    SpawnLogging = new ConfigData.SpawnLoggingOptions
                    {
                        SpawnLog = true,
                        SpawnAtLog = true,
                        SpawnHereLog = true,
                        SpawnItemLog = true
                    }
                },

                PluginsCommands = new ConfigData.PluginsCommandsOptions
                {
                    VanishLog = true,
                    RadarLog = true,
                    NightLog = true,
                    ConvertLog = true,
                    InventoryViewerLog = true,
                    BackpacksLog = true,

                    FreezeAllLog = true,
                    FreezeLogging = new ConfigData.FreezeLoggingOptions
                    {
                        FreezeLog = true,
                        UnfreezeLog = true,
                        AllFreezeLog = true,
                        AllUnfreezeLog = true                       
                    },

                    PlayerAdministrationAllLog = true,
                    PlayerAdministrationLogging = new ConfigData.PlayerAdministrationLoggingOptions
                    {
                        OpenPadminCmdLog = true,
                        ClosePadminCmdLog = true,
                        BanUserCmdLog = true,
                        UnbanUserCmdLog = true,
                        KickUserCmdLog = true,
                        MuteUserCmdLog = true,
                        UnmuteUserCmdLog = true,
                        FreezeCmdLog = true,
                        UnreezeCmdLog = true,
                        BackpackViewCmdLog = true,
                        InventoryViewCmdLog = true,
                        ClearUserInventoryCmdLog = true,
                        ResetUserBPCmdLog = true,
                        ResetUserMetabolismCmdLog = true,
                        RecoverUserMetabolismLog = true,
                        TeleportToUserCmdLog = true,
                        TeleportUserCmdLog = true,
                        SpectateUserCmdLog = true,
                        PermsCmdLog = true,
                        HurtUserCmdLog = true,
                        KillUserCmdLog = true,
                        HealUserCmdLog = true
                    }
                }           
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        #endregion Config

        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminConnected"] = "{0} [{1}] connected.",
                ["AdminDisconnected"] = "{0} [{1}] disconnected.",
                ["NoclipEnabled"] = "{0} [{1}] enabled Noclip.",
                ["NoclipDisabled"] = "{0} [{1}] disabled Noclip.",
                ["GodmodeEnabled"] = "{0} [{1}] enabled Godmode.",
                ["GodmodeDisabled"] = "{0} [{1}] disabled Godmode.",
                ["SpectateEnabled"] = "{0} [{1}] enabled Spectate mode.",
                ["SpectateDisabled"] = "{0} [{1}] disabled Spectate mode.",
                ["SpectatePlayer"] = "{0} [{1}] started spectating player {2} [{3}].",
                ["KillPlayer"] = "{0} [{1}] killed {2} [{3}].",
                ["KickPlayer"] = "{0} [{1}] kicked {2} [{3}]. Reason: No reason.",
                ["KickPlayerReason"] = "{0} [{1}] kicked {2} [{3}]. Reason: {4}.",
                ["KickAllPlayers"] = "{0} [{1}] kicked all players.",
                ["BanPlayer"] = "{0} [{1}] banned {2} [{3}]. Reason: No reason.",
                ["BanPlayerReason"] = "{0} [{1}] banned {2} [{3}]. Reason: {4}.",
                ["UnbanPlayer"] = "{0} [{1}] unbanned {2} [{3}].",
                ["MutePlayer"] = "{0} [{1}] muted {2} [{3}].",
                ["UnmutePlayer"] = "{0} [{1}] unmuted {2} [{3}].",
                ["VanishEnabled"] = "{0} [{1}] enabled Vanish.",
                ["VanishDisabled"] = "{0} [{1}] disabled Vanish.",
                ["RadarEnabled"] = "{0} [{1}] enabled AdminRadar.",
                ["RadarDisabled"] = "{0} [{1}] disabled AdminRadar.",
                ["NightVisionEnabled"] = "{0} [{1}] enabled NightVision.",
                ["NightVisionDisabled"] = "{0} [{1}] disabled NightVision.",
                ["ConvertStatusEnabled"] = "{0} [{1}] converted into admin status.",
                ["ConvertStatusDisabled"] = "{0} [{1}] converted out of admin status.",
                ["InventoryView"] = "{0} [{1}] used Inventory Viewer on {2} [{3}].",
                ["TeleportSelfToPlayer"] = "{0} [{1}] teleported to {2} [{3}].",
                ["TeleportPlayerToPlayer"] = "{0} [{1}] teleported {2} [{3}] to {4} [{5}].",
                ["TeleportToSelf"] = "{0} [{1}] teleported {2} [{3}] to self.",
                ["TeleportPosition"] = "{0} [{1}] teleported to coordinates {2}.",
                ["GiveSelf"] = "{0} [{1}] gave themselves {2} x {3}.",
                ["GiveSelfArm"] = "{0} [{1}] added 1 x {2} to their belt.",
                ["GiveTo"] = "{0} [{1}] gave {2} [{3}] {4} x {5}.",
                ["GiveAll"] = "{0} [{1}] gave everyone {2} x {3}.",
                ["EntKillPrefab"] = "{0} [{1}] used *kill* on ent: {2} at position {3}.",
                ["EntKillBaseEntity"] = "{0} [{1}] used *kill* on {2} owned by {3} [{4}] at position {5}.",
                ["EntWhoBaseEntity"] = "{0} [{1}] used *who* on {2} owned by {3} [{4}] at position {5}.",
                ["EntLockBaseEntity"] = "{0} [{1}] used *lock* on {2} owned by {3} [{4}] at position {5}.",
                ["EntUnlockBaseEntity"] = "{0} [{1}] used *unlock* on {2} owned by {3} [{4}] at position {5}.",
                ["EntAuthBaseEntity"] = "{0} [{1}] used *auth* on {2} owned by {3} [{4}] at position {5}.",
                ["Spawn"] = "{0} [{1}] spawned {2} at {3}.",
                ["HeliCall"] = "{0} [{1}] called in Attack Helicopter.",
                ["HeliCallToMe"] = "{0} [{1}] called in Attack Helicopter to themselves at position {2}.",
                ["HeliCallDrop"] = "{0} [{1}] spawned Attack Helicopter at their position {2}.",
                ["AirdropCall"] = "{0} [{1}] called in a Supply Drop.",
                ["AirdropCallPos"] = "{0} [{1}] called in a Supply Drop to position (0, 0, 0).",
                ["PadminOpen"] = "{0} [{1}] opened Padmin Menu.",
                ["PadminClose"] = "{0} [{1}] closed Padmin Menu.",
                ["PadminBan"] = "{0} [{1}] banned {2} [{3}] using Padmin. Reason: Administrative decision.",
                ["PadminUnban"] = "{0} [{1}] unbanned {2} [{3}] using Padmin.",
                ["PadminKick"] = "{0} [{1}] kicked {2} [{3}] using Padmin. Reason: Administrative decision.",
                ["PadminMute"] = "{0} [{1}] muted {2} [{3}] using Padmin.",
                ["PadminUnmute"] = "{0} [{1}] unmuted {2} [{3}] using Padmin.",
                ["PadminFreeze"] = "{0} [{1}] Froze player {2} [{3}] using Padmin.",
                ["PadminUnfreeze"] = "{0} [{1}] Unfroze player {2} [{3}] using Padmin.",
                ["PadminBackpackView"] = "{0} [{1}] viewed Backpack of player {2} [{3}] using Padmin.",
                ["PadminInventoryView"] = "{0} [{1}] viewed Inventory of player {2} [{3}] using Padmin.",
                ["PadminClearInventory"] = "{0} [{1}] cleared the inventory of player {2} [{3}] using Padmin.",
                ["PadminResetBP"] = "{0} [{1}] reset the blueprints of player {2} [{3}] using Padmin.",
                ["PadminResetMetabolism"] = "{0} [{1}] reset the metabolism of player {2} [{3}] using Padmin.",
                ["PadminRecoverMetabolism"] = "{0} [{1}] recovered the metabolism of player {2} [{3}] using Padmin.",
                ["PadminTeleportToPlayer"] = "{0} [{1}] teleported to {2} [{3}] using Padmin.",
                ["PadminTeleportPlayer"] = "{0} [{1}] teleported {2} [{3}] to themselves using Padmin.",
                ["PadminSpectate"] = "{0} [{1}] started spectating player {2} [{3}] using Padmin.",
                ["PadminPerms"] = "{0} [{1}] opened the permissions manager for player {2} [{3}] using Padmin.",
                ["PadminHurt"] = "{0} [{1}] hurt player {2} [{3}] for {4} points using Padmin.",
                ["PadminKill"] = "{0} [{1}] killed player {2} [{3}] using Padmin.",
                ["PadminHeal"] = "{0} [{1}] healed player {2} [{3}] for {4} points using Padmin.",
                ["BackpacksView"] = "{0} [{1}] viewed Backpack of player {2} [{3}].",
                ["FreezePlayer"] = "{0} [{1}] Froze player {2} [{3}].",
                ["UnfreezePlayer"] = "{0} [{1}] Unfroze player {2} [{3}].",
                ["FreezeAllPlayers"] = "{0} [{1}] Froze all players.",
                ["UnfreezeAllPlayers"] = "{0} [{1}] Unfroze all players.",

            }, this);
        }

        #endregion Localization

        #region Oxide Hooks

        void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            InvokeHandler.Instance.InvokeRepeating(HandlePlayers, 5f, configData.UpdateFreq);
        }

        void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(HandlePlayers);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (configData.ExcludeList.Contains(player.UserIDString))
            {
                return;
            }


            if (player.IsAdmin || (configData.PluginsCommands.ConvertLog && ConvertStatus && player.IPlayer.HasPermission("convertstatus.use")))
            {
                if (configData.DefaultCommands.ConnectionLog)
                {
                    Log(configData.LogFileName, "AdminConnected", player.displayName, player.UserIDString);
                }

                adminList.Add(player);
                noclipState[player.userID] = false;
                spectateState[player.userID] = false;
                if (player.IsGod())
                {
                    godmodeState[player.userID] = true;
                }
                else
                {
                    godmodeState[player.userID] = false;
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (configData.ExcludeList.Contains(player.UserIDString))
            {
                return;
            }

            if (player.IsAdmin || (configData.PluginsCommands.ConvertLog && ConvertStatus && player.IPlayer.HasPermission("convertstatus.use")))
            {
                if (configData.DefaultCommands.ConnectionLog)
                {
                    Log(configData.LogFileName, "AdminDisconnected", player.displayName, player.UserIDString);
                }

                adminList.Remove(player);
            }
        }

        private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsConnectionAdmin) return;
            string command = arg.cmd.Name;
            string fullCommand = arg.cmd.FullName;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            if (fullCommand == "chat.say") return;
            if (configData.ExcludeList.Contains(player.UserIDString))
            {
                return;
            }

            switch (command)
            {
                case "killplayer":
                    if (configData.DefaultCommands.KillPlayerLog)
                    {
                        KillPlayerLogging(arg);
                    }
                    break;
                case "kick":
                case "kickall":
                    if (configData.DefaultCommands.KickAllLog)
                    {
                        KickLogging(arg);
                    }
                    break;
                case "ban":
                case "unban":
                    if (configData.DefaultCommands.BanAllLog)
                    {
                        BanLogging(arg);
                    }
                    break;
                case "mute": 
                case "unmute":
                    if (configData.DefaultCommands.MuteAllLog)
                    {
                        MuteLogging(arg);
                    }
                    break;
                case "teleport":
                case "teleportpos":
                case "teleport2me":
                    if (configData.DefaultCommands.TeleportAllLog)
                    {
                        TeleportLogging(arg);
                    }
                    break;
                case "spectate":
                    if (configData.DefaultCommands.SpectateLog)
                    {
                        SpectateLogging(arg);
                    }
                    break;
                case "giveid":
                case "give":
                case "givearm":
                case "giveto":
                case "giveall":
                    if (configData.DefaultCommands.GiveAllLog)
                    {
                        GiveItemLogging(arg);
                    }
                    break;
                case "spawn":
                case "spawnat":
                case "spawnhere":
                case "spawnitem":
                    if (configData.DefaultCommands.SpawnAllLog)
                    {
                        SpawnLogging(arg);
                    }
                    break;
                case "entid":
                    if (configData.DefaultCommands.EntAllLog)
                    {
                        EntityLogging(arg);
                    }
                    break;
                case "vanish":
                    if (configData.PluginsCommands.VanishLog && Vanish != null && Vanish.IsLoaded && player.IPlayer.HasPermission("vanish.allow"))
                    {
                        VanishLogging(player);
                    }
                    break;
                case "freeze":
                case "unfreeze":
                case "freezeall":
                case "unfreezeall":
                    if (configData.PluginsCommands.FreezeAllLog && Freeze != null && Freeze.IsLoaded && player.IPlayer.HasPermission("freeze.use"))
                    {
                        FreezeLogging(arg);
                    }
                    break;
            }

            switch (fullCommand)
            {
                case "heli.call":
                case "heli.calltome":
                case "global.drop":
                case "drop":
                case "supply.call":
                case "supply.drop":
                    if (configData.DefaultCommands.EventsAllLog)
                    {
                        EventsLogging(arg);
                    }
                    break;
                case "playeradministration.closeui":
                case "playeradministration.kickuser":
                case "playeradministration.banuser":
                case "playeradministration.unbanuser":
                case "playeradministration.perms":
                case "playeradministration.muteuser":
                case "playeradministration.unmuteuser":
                case "playeradministration.tptouser":
                case "playeradministration.tpuser":
                case "playeradministration.viewbackpack":
                case "playeradministration.viewinventory":
                case "playeradministration.freeze":
                case "playeradministration.unfreeze":
                case "playeradministration.clearuserinventory":
                case "playeradministration.resetuserblueprints":
                case "playeradministration.resetusermetabolism":
                case "playeradministration.recoverusermetabolism":
                case "playeradministration.spectateuser":
                case "playeradministration.hurtuser":
                case "playeradministration.killuser":
                case "playeradministration.healuser":
                    if (configData.PluginsCommands.PlayerAdministrationAllLog && PlayerAdministration != null && PlayerAdministration.IsLoaded)
                    {
                        PadminLogging(arg);
                    }
                    break;
            }
        }

        void OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (configData.ExcludeList.Contains(player.UserIDString))
            {
                return;
            }

            switch (command)
            {
                case "vanish":
                    if (configData.PluginsCommands.VanishLog && Vanish != null && Vanish.IsLoaded && player.IPlayer.HasPermission("vanish.allow"))
                    {
                        VanishLogging(player);
                    }
                    break;
                case "radar":
                    if (configData.PluginsCommands.RadarLog && AdminRadar != null && AdminRadar.IsLoaded && player.IPlayer.HasPermission("adminradar.allowed"))
                    {
                        AdminRadarLogging(player);
                    }
                    break;
                case "nightvision":
                case "nv":
                    if (configData.PluginsCommands.NightLog && NightVision != null && NightVision.IsLoaded && player.IPlayer.HasPermission("nightvision.allowed"))
                    {
                        NightVisionLogging(player);
                    }
                    break;
                case "convert":
                    if (configData.PluginsCommands.ConvertLog && ConvertStatus != null && ConvertStatus.IsLoaded && player.IPlayer.HasPermission("convertstatus.use"))
                    {
                        ConvertStatusLogging(player);
                    }
                    break;
                case "freeze":
                case "unfreeze":
                case "freezeall":
                case "unfreezeall":
                    if (configData.PluginsCommands.FreezeAllLog && Freeze != null && Freeze.IsLoaded && player.IPlayer.HasPermission("freeze.use"))
                    {
                        FreezeLogging(player, command, args);
                    }
                    break;
                case "viewinventory":
                case "viewinv":
                    if (configData.PluginsCommands.InventoryViewerLog && InventoryViewer != null && InventoryViewer.IsLoaded && player.IPlayer.HasPermission("inventoryviewer.allowed"))
                    {
                        InventoryViewerLogging(player, args);
                    }
                    break;
                case "viewbackpack":
                    if (configData.PluginsCommands.BackpacksLog && Backpacks != null && Backpacks.IsLoaded && player.IPlayer.HasPermission("backpacks.admin"))
                    {
                        BackpacksLogging(player, args);
                    }
                    break;
                case "padmin":
                    if (configData.PluginsCommands.PlayerAdministrationAllLog && PlayerAdministration != null && PlayerAdministration.IsLoaded && player.IPlayer.HasPermission("playeradministration.access.show"))
                    {
                        PadminLogging(player);                       
                    }
                    break;
            }
        }

        void OnPlayerSpectateEnd(BasePlayer player, string spectateFilter)
        {
            if (configData.ExcludeList.Contains(player.UserIDString))
            {
                return;
            }

            if (configData.DefaultCommands.SpectateLog)
            {
                Log(configData.LogFileName, "SpectateDisabled", player.displayName, player.UserIDString);
            }
        }

        #endregion Oxide Hooks

        #region Default Commands

        #region Noclip & Godmode

        private void ClientSideCommandDetection(BasePlayer player)
        {
            if (configData.DefaultCommands.NoclipLog)
            {
                if (player.IsFlying && !noclipState[player.userID])
                {
                    Log(configData.LogFileName, "NoclipEnabled", player.displayName, player.UserIDString);
                    noclipState[player.userID] = true;
                }

                if (!player.IsFlying && noclipState[player.userID])
                {
                    Log(configData.LogFileName, "NoclipDisabled", player.displayName, player.UserIDString);
                    noclipState[player.userID] = false;
                }
            }

            if (configData.DefaultCommands.GodmodeLog)
            {
                if (player.IsGod() && godmodeState[player.userID])
                {
                    Log(configData.LogFileName, "GodmodeEnabled", player.displayName, player.UserIDString);
                    godmodeState[player.userID] = false;
                }

                if (!player.IsGod() && !godmodeState[player.userID])
                {
                    Log(configData.LogFileName, "GodmodeDisabled", player.displayName, player.UserIDString);
                    godmodeState[player.userID] = true;
                }
            }
        }

        #endregion Noclip & Godmode

        #region Events

        private void EventsLogging(ConsoleSystem.Arg arg)
        {
            string command = arg.cmd.Name;
            string fullCommand = arg.cmd.FullName;
            ulong playerUserId = arg.Connection.userid;
            var player = BasePlayer.FindByID(playerUserId);

            if (player == null)
            {
                return;
            }

            switch (fullCommand)
            {
                case "heli.call":
                    if (configData.DefaultCommands.EventsLogging.HeliCallLog)
                    {
                        Log(configData.LogFileName, "HeliCall", player.displayName, player.UserIDString);
                    }
                    break;
                case "heli.calltome":
                    if (configData.DefaultCommands.EventsLogging.HeliCallToMeLog)
                    {
                        var position = player.transform.position;
                        Log(configData.LogFileName, "HeliCallToMe", player.displayName, player.UserIDString, position);
                    }
                    break;
                case "global.drop":
                    if (configData.DefaultCommands.EventsLogging.HeliDropLog)
                    {
                        var position = player.transform.position;
                        Log(configData.LogFileName, "HeliCallDrop", player.displayName, player.UserIDString, position);
                    }
                    break;
                case "supply.call":
                    if (configData.DefaultCommands.EventsLogging.AirdropRandomLog)
                    {
                        Log(configData.LogFileName, "AirdropCall", player.displayName, player.UserIDString);
                    }
                    break;
                case "supply.drop":
                    if (configData.DefaultCommands.EventsLogging.AirdropPosLog)
                    {
                        Log(configData.LogFileName, "AirdropCallPos", player.displayName, player.UserIDString);
                    }
                    break;
            }
        }

        #endregion Events

        #region Kill Player

        private void KillPlayerLogging(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0) return;

            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            var player1 = covalence.Players.FindPlayer(arg.Args[0]);

            if (player1 == null) return;

            Log(configData.LogFileName, "KillPlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
        }

        #endregion Kill Player

        #region Kick

        private void KickLogging(ConsoleSystem.Arg arg)
        {
            string command = arg.cmd.Name;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            if (configData.DefaultCommands.KickLogging.KickEveryoneLog && command == "kickall")
            {
                Log(configData.LogFileName, "KickAllPlayers", player.displayName, player.UserIDString);
            }

            if (configData.DefaultCommands.KickLogging.KickLog && command == "kick")
            {
                if (arg.Args == null || arg.Args.Length == 0) return;

                var player1 = covalence.Players.FindPlayer(arg.Args[0]);
                if (player1 == null) return;

                if (arg.Args.Length == 2)
                {
                    string reason = arg.Args[1];
                    Log(configData.LogFileName, "KickPlayerReason", player.displayName, player.UserIDString, player1.Name, player1.Id, reason);
                }
                else
                {
                    Log(configData.LogFileName, "KickPlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                }
            }
        }

        #endregion Kick

        #region Ban

        private void BanLogging(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0) return;

            string command = arg.cmd.Name;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            var player1 = covalence.Players.FindPlayer(arg.Args[0]);
            if (player1 == null) return;

            if (configData.DefaultCommands.BanLogging.BanLog && command == "ban")
            {
                if (arg.Args.Length == 2)
                {
                    string reason = arg.Args[1];
                    Log(configData.LogFileName, "BanPlayerReason", player.displayName, player.UserIDString, player1.Name, player1.Id, reason);
                }
                else
                {
                    Log(configData.LogFileName, "BanPlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                }
            }

            if (configData.DefaultCommands.BanLogging.UnbanLog && command == "unban")
            {
                Log(configData.LogFileName, "UnbanPlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
            }
        }

        #endregion Ban

        #region Mute

        private void MuteLogging(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0) return;

            string command = arg.cmd.Name;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            var player1 = covalence.Players.FindPlayer(arg.Args[0]);
            if (player1 == null) return;

            if (configData.DefaultCommands.MuteLogging.MuteLog && command == "mute")
            {
                Log(configData.LogFileName, "MutePlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
            }

            if (configData.DefaultCommands.MuteLogging.UnmuteLog && command == "unmute")
            {
                Log(configData.LogFileName, "UnmutePlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
            }
        }

        #endregion Mute

        #region Spectate

        private void SpectateLogging(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                Log(configData.LogFileName, "SpectateEnabled", player.displayName, player.UserIDString);
            }

            if (arg.Args.Length == 1)
            {
                var player1 = covalence.Players.FindPlayer(arg.Args[0]);
                if (player1 == null) return;
                Log(configData.LogFileName, "SpectatePlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
            }
        }

        #endregion Spectate

        #region Teleport

        private void TeleportLogging(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0) return;

            string command = arg.cmd.Name;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            if (configData.DefaultCommands.TeleportLogging.TeleportLog && command == "teleport")
            {
                if (arg.Args.Length == 1)
                {
                    var player1 = covalence.Players.FindPlayer(arg.Args[0]);
                    if (player1 == null) return;
                    Log(configData.LogFileName, "TeleportSelfToPlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                }

                if (arg.Args.Length == 2)
                {
                    var player1 = covalence.Players.FindPlayer(arg.Args[0]);
                    if (player1 == null) return;
                    var player2 = covalence.Players.FindPlayer(arg.Args[1]);
                    if (player1 == null) return;
                    Log(configData.LogFileName, "TeleportPlayerToPlayer", player.displayName, player.UserIDString, player1.Name, player1.Id, player2.Name, player2.Id);
                }
            }

            if (configData.DefaultCommands.TeleportLogging.TeleportPosLog && command == "teleportpos")
            {
                Log(configData.LogFileName, "TeleportPosition", player.displayName, player.UserIDString, arg.FullString);
            }

            if (configData.DefaultCommands.TeleportLogging.TeleportToMeLog && command == "teleport2me")
            {
                var player1 = covalence.Players.FindPlayer(arg.Args[0]);
                if (player1 == null) return;
                Log(configData.LogFileName, "TeleportToSelf", player.displayName, player.UserIDString, player1.Name, player1.Id);
            }
        }

        #endregion Teleport

        #region Give

        private void GiveItemLogging(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0) return;
            if (ItemManager.FindItemDefinition(arg.Args[0].ToInt()) == null) return;
        
            string command = arg.cmd.Name;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            switch (command)
            {
                case "giveid":
                    if (configData.DefaultCommands.GiveLogging.GiveIdLog)
                    {
                        if (arg.Args.Length == 1)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[0].ToInt()).shortname;
                            Log(configData.LogFileName, "GiveSelf", player.displayName, player.UserIDString, "1", itemShortName);
                        }

                        if (arg.Args.Length == 2)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[0].ToInt()).shortname;
                            var amount = arg.Args[1];
                            Log(configData.LogFileName, "GiveSelf", player.displayName, player.UserIDString, amount, itemShortName);
                        }

                        if (arg.Args.Length == 3)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[1].ToInt()).shortname;
                            var amount = arg.Args[2];
                            var player1 = covalence.Players.FindPlayer(arg.Args[0]);
                            if (player1 == null) return;
                            Log(configData.LogFileName, "GiveTo", player.displayName, player.UserIDString, player1.Name, player1.Id, amount, itemShortName);
                        }
                    }
                    break;
                case "give":
                    if (configData.DefaultCommands.GiveLogging.GiveLog)
                    {
                        if (arg.Args.Length == 1)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[0].ToInt()).shortname;
                            Log(configData.LogFileName, "GiveSelf", player.displayName, player.UserIDString, "1", itemShortName);
                        }
                        if (arg.Args.Length == 2)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[0].ToInt()).shortname;
                            var amount = arg.Args[1];
                            Log(configData.LogFileName, "GiveSelf", player.displayName, player.UserIDString, amount, itemShortName);
                        }
                    }
                    break;
                case "givearm":
                    if (configData.DefaultCommands.GiveLogging.GiveArmLog)
                    {
                        var itemShortName = ItemManager.FindItemDefinition(arg.Args[0].ToInt()).shortname;
                        Log(configData.LogFileName, "GiveSelfArm", player.displayName, player.UserIDString, itemShortName);
                    }
                    break;
                case "giveto":
                    if (configData.DefaultCommands.GiveLogging.GiveToLog)
                    {
                        var player1 = covalence.Players.FindPlayer(arg.Args[0]);
                        if (player1 == null) return;

                        if (arg.Args.Length == 2)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[1].ToInt()).shortname;
                            Log(configData.LogFileName, "GiveTo", player.displayName, player.UserIDString, player1.Name, player1.Id, "1", itemShortName);
                        }

                        if (arg.Args.Length == 3)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[1].ToInt()).shortname;
                            var amount = arg.Args[2];
                            Log(configData.LogFileName, "GiveTo", player.displayName, player.UserIDString, player1.Name, player1.Id, amount, itemShortName);
                        }
                    }
                    break;
                case "giveall":
                    if (configData.DefaultCommands.GiveLogging.GiveAllLog)
                    {
                        if (arg.Args.Length == 1)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[0].ToInt()).shortname;
                            Log(configData.LogFileName, "GiveAll", player.displayName, player.UserIDString, "1", itemShortName);
                        }
                        if (arg.Args.Length == 2)
                        {
                            var itemShortName = ItemManager.FindItemDefinition(arg.Args[0].ToInt()).shortname;
                            var amount = arg.Args[1];
                            Log(configData.LogFileName, "GiveAll", player.displayName, player.UserIDString, amount, itemShortName);
                        }
                    }
                    break;
            }           
        }

        #endregion Give

        #region Spawn

        private void SpawnLogging(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2) return;

            string command = arg.cmd.Name;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            switch (command)
            {
                case "spawn":
                    if (configData.DefaultCommands.SpawnLogging.SpawnLog)
                    {
                        Log(configData.LogFileName, "Spawn", player.displayName, player.UserIDString, arg.Args[0], arg.Args[1]);
                    }
                    break;
                case "spawnat":
                    if (configData.DefaultCommands.SpawnLogging.SpawnAtLog)
                    {
                        Log(configData.LogFileName, "Spawn", player.displayName, player.UserIDString, arg.Args[0], arg.Args[1]);
                    }
                    break;
                case "spawnhere":
                    if (configData.DefaultCommands.SpawnLogging.SpawnHereLog)
                    {
                        Log(configData.LogFileName, "Spawn", player.displayName, player.UserIDString, arg.Args[0], arg.Args[1]);
                    }
                    break;
                case "spawnitem":
                    if (configData.DefaultCommands.SpawnLogging.SpawnItemLog)
                    {
                        Log(configData.LogFileName, "Spawn", player.displayName, player.UserIDString, arg.Args[0], arg.Args[1]);
                    }
                    break;
            }
        }

        #endregion Spawn

        #region Ent

        private void EntityLogging(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2) return;

            string command = arg.cmd.Name;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            NetworkableId id = new NetworkableId(Convert.ToUInt32(arg.Args[1]));
            var entity = BaseNetworkable.serverEntities.Find(id);
            if (entity == null) return;

            if (command != "entid") return;

            switch (arg.Args[0])
            {
                case "kill":
                    if (configData.DefaultCommands.EntityLogging.EntKillLog)
                    {
                        if (entity is BaseEntity)
                        {
                            var bentity = entity as BaseEntity;
                            var player1 = covalence.Players.FindPlayerById(bentity.OwnerID.ToString());

                            if (player1 != null)
                            {
                                Log(configData.LogFileName, "EntKillBaseEntity", player.displayName, player.UserIDString, bentity.ShortPrefabName, player1.Name, player1.Id, player.transform.position);
                            }
                            else
                            {
                                Log(configData.LogFileName, "EntKillPrefab", player.displayName, player.UserIDString, entity.ShortPrefabName, player.transform.position);
                            }

                        }
                        else
                        {
                            Log(configData.LogFileName, "EntKillPrefab", player.displayName, player.UserIDString, entity.ShortPrefabName, player.transform.position);
                        }
                    }
                    break;
                case "who":
                    if (configData.DefaultCommands.EntityLogging.EntWhoLog)
                    {
                        if (entity is BaseEntity)
                        {
                            var bentity = entity as BaseEntity;
                            var player1 = covalence.Players.FindPlayerById(bentity.OwnerID.ToString());
                            if (player1 != null)
                            {
                                Log(configData.LogFileName, "EntWhoBaseEntity", player.displayName, player.UserIDString, bentity.ShortPrefabName, player1.Name, player1.Id, player.transform.position);
                            }
                        }
                    }
                    break;
                case "lock":
                    if (configData.DefaultCommands.EntityLogging.EntLockLog)
                    {
                        if (entity is BaseEntity)
                        {
                            var bentity = entity as BaseEntity;
                            var player1 = covalence.Players.FindPlayerById(bentity.OwnerID.ToString());
                            if (player1 != null)
                            {
                                Log(configData.LogFileName, "EntLockBaseEntity", player.displayName, player.UserIDString, bentity.ShortPrefabName, player1.Name, player1.Id, player.transform.position);
                            }
                        }
                    }
                    break;
                case "unlock":
                    if (configData.DefaultCommands.EntityLogging.EntUnlockLog)
                    {
                        if (entity is BaseEntity)
                        {
                            var bentity = entity as BaseEntity;
                            var player1 = covalence.Players.FindPlayerById(bentity.OwnerID.ToString());
                            if (player1 != null)
                            {
                                Log(configData.LogFileName, "EntUnlockBaseEntity", player.displayName, player.UserIDString, bentity.ShortPrefabName, player1.Name, player1.Id, player.transform.position);
                            }
                        }
                    }
                    break;
                case "auth":
                    if (configData.DefaultCommands.EntityLogging.EntAuthLog)
                    {
                        if (entity is BaseEntity)
                        {
                            var bentity = entity as BaseEntity;
                            var player1 = covalence.Players.FindPlayerById(bentity.OwnerID.ToString());
                            Log(configData.LogFileName, "EntAuthBaseEntity", player.displayName, player.UserIDString, bentity.ShortPrefabName, player1.Name, player1.Id, player.transform.position);

                        }
                    }
                    break;
            }           
        }

        #endregion Ent

        #endregion Default Commands

        #region Plugins Commands

        #region Vanish

        private void VanishLogging(BasePlayer player)
        {         
            if (Vanish.Call<bool>("IsInvisible", player))
            {
                Log(configData.LogFileName, "VanishDisabled", player.displayName, player.UserIDString);
            }
            else
            {
                Log(configData.LogFileName, "VanishEnabled", player.displayName, player.UserIDString);
            }
        }

        #endregion Vanish

        #region AdminRadar

        private void AdminRadarLogging(BasePlayer player)
        {
            if (AdminRadar.Call<bool>("IsRadar", player.UserIDString))
            {
                Log(configData.LogFileName, "RadarDisabled", player.displayName, player.UserIDString);
            }
            else
            {
                Log(configData.LogFileName, "RadarEnabled", player.displayName, player.UserIDString);
            }
        }

        #endregion AdminRadar

        #region NightVision

        private void NightVisionLogging(BasePlayer player)
        {
            if (NightVision.Call<bool>("IsPlayerTimeLocked", player))
            {
                Log(configData.LogFileName, "NightVisionDisabled", player.displayName, player.UserIDString);
            }
            else
            {
                Log(configData.LogFileName, "NightVisionEnabled", player.displayName, player.UserIDString);
            }
        }

        #endregion NightVision

        #region ConvertStatus

        private void ConvertStatusLogging(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                Log(configData.LogFileName, "ConvertStatusDisabled", player.displayName, player.UserIDString);
            }
            else
            {
                Log(configData.LogFileName, "ConvertStatusEnabled", player.displayName, player.UserIDString);
            }
        }

        #endregion ConvertStatus

        #region Freeze

        private void FreezeLogging(ConsoleSystem.Arg arg)
        {
            string command = arg.cmd.Name;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            switch (command)
            {
                case "freeze":
                    if (configData.PluginsCommands.FreezeLogging.FreezeLog)
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "FreezePlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "unfreeze":
                    if (configData.PluginsCommands.FreezeLogging.UnfreezeLog)
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "UnfreezePlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "freezeall":
                    if (configData.PluginsCommands.FreezeLogging.AllFreezeLog)
                    {
                        Log(configData.LogFileName, "FreezeAllPlayers", player.displayName, player.UserIDString);
                    }
                    break;
                case "unfreezeall":
                    if (configData.PluginsCommands.FreezeLogging.AllFreezeLog)
                    {
                        Log(configData.LogFileName, "UnfreezeAllPlayers", player.displayName, player.UserIDString);
                    }
                    break;
            }
        }

        private void FreezeLogging(BasePlayer player, string command, string[] args)
        {
            switch (command)
            {
                case "freeze":
                    if (configData.PluginsCommands.FreezeLogging.FreezeLog)
                    {
                        if (args == null || args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "FreezePlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "unfreeze":
                    if (configData.PluginsCommands.FreezeLogging.UnfreezeLog)
                    {
                        if (args == null || args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "UnfreezePlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "freezeall":
                    if (configData.PluginsCommands.FreezeLogging.AllFreezeLog)
                    {
                        Log(configData.LogFileName, "FreezeAllPlayers", player.displayName, player.UserIDString);
                    }
                    break;
                case "unfreezeall":
                    if (configData.PluginsCommands.FreezeLogging.AllFreezeLog)
                    {
                        Log(configData.LogFileName, "UnfreezeAllPlayers", player.displayName, player.UserIDString);
                    }
                    break;
            }
        }

        #endregion Freeze

        #region InventoryViewer

        private void InventoryViewerLogging(BasePlayer player, string[] args)
        {
            if (args == null || args.Length == 0) return;
            var player1 = covalence.Players.FindPlayerById(args[0]);
            if (player1 == null) return;
            Log(configData.LogFileName, "InventoryView", player.displayName, player.UserIDString, player1.Name, player1.Id);
        }

        #endregion Backpacks

        #region Backpacks

        private void BackpacksLogging(BasePlayer player, string[] args)
        {
            if (args == null || args.Length == 0) return;
            var player1 = covalence.Players.FindPlayerById(args[0]);
            if (player1 == null) return;
            Log(configData.LogFileName, "BackpacksView", player.displayName, player.UserIDString, player1.Name, player1.Id);
        }

        #endregion Backpacks

        #region Padmin

        private void PadminLogging(BasePlayer player)
        {
            if (!configData.PluginsCommands.PlayerAdministrationLogging.OpenPadminCmdLog) return;
            Log(configData.LogFileName, "PadminOpen", player.displayName, player.UserIDString);
        }

        private void PadminLogging(ConsoleSystem.Arg arg)
        {
            string fullCommand = arg.cmd.FullName;
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            switch (fullCommand)
            {
                case "playeradministration.closeui":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.ClosePadminCmdLog && player.IPlayer.HasPermission("playeradministration.access.show"))
                    {
                        Log(configData.LogFileName, "PadminClose", player.displayName, player.UserIDString);
                    }
                    break;
                case "playeradministration.kickuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.KickUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.kick"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminKick", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.banuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.BanUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.ban"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminBan", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.unbanuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.UnbanUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.ban"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminUnban", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.muteuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.MuteUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.mute"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminMute", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.unmuteuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.UnmuteUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.mute"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminUnmute", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.freeze":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.FreezeCmdLog && Freeze != null && Freeze.IsLoaded && player.IPlayer.HasPermission("playeradministration.access.allowfreeze"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminFreeze", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.unfreeze":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.UnreezeCmdLog && Freeze != null && Freeze.IsLoaded && player.IPlayer.HasPermission("playeradministration.access.allowfreeze"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminUnfreeze", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.viewbackpack":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.BackpackViewCmdLog && Backpacks != null && Backpacks.IsLoaded && player.IPlayer.HasPermission("backpacks.admin"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminBackpackView", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.viewinventory":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.InventoryViewCmdLog && InventoryViewer != null && InventoryViewer.IsLoaded && player.IPlayer.HasPermission("inventoryviewer.allowed"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminInventoryView", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.clearuserinventory":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.ClearUserInventoryCmdLog && player.IPlayer.HasPermission("playeradministration.access.clearinventory"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminClearInventory", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.resetuserblueprints":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.ResetUserBPCmdLog && player.IPlayer.HasPermission("playeradministration.access.resetblueprint"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminResetBP", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.resetusermetabolism":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.ResetUserMetabolismCmdLog && player.IPlayer.HasPermission("playeradministration.access.resetmetabolism"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminResetMetabolism", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.recoverusermetabolism":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.RecoverUserMetabolismLog && player.IPlayer.HasPermission("playeradministration.access.recovermetabolism"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminRecoverMetabolism", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.tptouser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.TeleportToUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.teleport"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminTeleportToPlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.tpuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.TeleportUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.teleport"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminTeleportPlayer", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.spectateuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.SpectateUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.spectate"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminSpectate", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.perms":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.PermsCmdLog && player.IPlayer.HasPermission("playeradministration.access.perms"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminPerms", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.hurtuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.HurtUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.hurt"))
                    {
                        if (arg.Args == null || arg.Args.Length < 2) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminHurt", player.displayName, player.UserIDString, player1.Name, player1.Id, arg.Args[1]);
                    }
                    break;
                case "playeradministration.killuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.KillUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.kill"))
                    {
                        if (arg.Args == null || arg.Args.Length == 0) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminKill", player.displayName, player.UserIDString, player1.Name, player1.Id);
                    }
                    break;
                case "playeradministration.healuser":
                    if (configData.PluginsCommands.PlayerAdministrationLogging.HealUserCmdLog && player.IPlayer.HasPermission("playeradministration.access.heal"))
                    {
                        if (arg.Args == null || arg.Args.Length < 2) return;
                        var player1 = covalence.Players.FindPlayerById(arg.Args[0]);
                        if (player1 == null) return;
                        Log(configData.LogFileName, "PadminHeal", player.displayName, player.UserIDString, player1.Name, player1.Id, arg.Args[1]);
                    }
                    break;

            }
        }

        #endregion Padmin

        #endregion Plugins Commands

        #region Helpers

        private void HandlePlayers()
        {
            foreach (var player in adminList)
            {
                ClientSideCommandDetection(player);
            }
        }

        private void Log(string filename, string key, params object[] args)
        {
            if (configData.LogToConsole)
            {
                Puts($"[{DateTime.Now}] {Lang(key, null, args)}");
            }

            if (configData.DiscordLog)
            {
                DiscordPost($"[{DateTime.Now}] {Lang(key, null, args)}");
            }           

            LogToFile(filename, $"[{DateTime.Now}] {Lang(key, null, args)}", this);
        }

        private string Lang(string key, string id = null, params object[] args)
        {           
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private void DiscordPost(string message)
        {
            var payload = new
            {
                content = message
            };

            var form = new WWWForm();
            form.AddField("payload_json", JsonConvert.SerializeObject(payload));

            InvokeHandler.Instance.StartCoroutine(HandleUpload(configData.DiscordWebhook, form));
        }

        private IEnumerator HandleUpload(string url, WWWForm data)
        {
            var www = UnityWebRequest.Post(url, data);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Puts($"Failed to post Discord webhook message: {www.error}");
            }
        }

        #endregion Helpers

    }
}