using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Discord Logger", "MON@H", "2.0.11")]
    [Description("Logs events to Discord channels using webhooks")]
    class DiscordLogger : RustPlugin
    {
        #region Variables

        [PluginReference] private Plugin AntiSpam, BetterChatMute, CallHeli, PersonalHeli, UFilter;

        private readonly Hash<ulong, CargoShip> _cargoShips = new Hash<ulong, CargoShip>();
        private readonly List<ulong> _listBadCargoShips = new List<ulong>();
        private readonly List<ulong> _listSupplyDrops = new List<ulong>();
        private readonly Queue<QueuedMessage> _queue = new Queue<QueuedMessage>();
        private readonly StringBuilder _sb = new StringBuilder();

        private EventSettings _eventSettings;
        private int _retryCount = 0;
        private object _resultCall;
        private QueuedMessage _nextMessage;
        private QueuedMessage _queuedMessage;
        private string _langKey;
        private string[] _profanities;
        private Timer _timerQueue;
        private Timer _timerQueueCooldown;
        private ulong _entityID;
        private Vector3 _locationLargeOilRig;
        private Vector3 _locationOilRig;

        private readonly List<Regex> _regexTags = new List<Regex>
        {
            new Regex("<color=.+?>", RegexOptions.Compiled),
            new Regex("<size=.+?>", RegexOptions.Compiled)
        };

        private readonly List<string> _tags = new List<string>
        {
            "</color>",
            "</size>",
            "<i>",
            "</i>",
            "<b>",
            "</b>"
        };

        private class QueuedMessage
        {
            public string WebhookUrl { set; get; }
            public string Message { set; get; }
        }

        private enum TeamEventType
        {
            Created,
            Disbanded,
            Updated,
        }

        private class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }

            [JsonProperty("countryCode")]
            public string CountryCode { get; set; }
        }

        #endregion Variables

        #region Initialization

        private void Init()
        {
            UnsubscribeHooks();
        }

        private void Unload()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
        }

        private void OnServerInitialized(bool isStartup)
        {
            if (isStartup && _configData.ServerStateSettings.Enabled)
            {
                LogToConsole("Server is online again!");

                DiscordSendMessage(Lang(LangKeys.Event.Initialized), _configData.ServerStateSettings.WebhookURL);
            }

            CacheOilRigsLocation();
            SubscribeHooks();
        }

        private void OnServerShutdown()
        {
            if (_configData.ServerStateSettings.Enabled)
            {
                LogToConsole("Server is shutting down!");

                string url = GetWebhookURL(_configData.ServerStateSettings.WebhookURL);

                if (!string.IsNullOrEmpty(url))
                {
                    webrequest.Enqueue(url, new DiscordMessage(Lang(LangKeys.Event.Shutdown)).ToJson(), DiscordSendMessageCallback, null, RequestMethod.POST, _headers);
                }
            }
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalSettings GlobalSettings = new GlobalSettings();

            [JsonProperty(PropertyName = "Admin Hammer settings")]
            public EventSettings AdminHammerSettings = new EventSettings();

            [JsonProperty(PropertyName = "Admin Radar settings")]
            public EventSettings AdminRadarSettings = new EventSettings();

            [JsonProperty(PropertyName = "Bradley settings")]
            public EventSettings BradleySettings = new EventSettings();

            [JsonProperty(PropertyName = "Cargo Ship settings")]
            public EventSettings CargoShipSettings = new EventSettings();

            [JsonProperty(PropertyName = "Cargo Plane settings")]
            public EventSettings CargoPlaneSettings = new EventSettings();

            [JsonProperty(PropertyName = "Chat settings")]
            public EventSettings ChatSettings = new EventSettings();

            [JsonProperty(PropertyName = "Chat (Team) settings")]
            public EventSettings ChatTeamSettings = new EventSettings();

            [JsonProperty(PropertyName = "CH47 Helicopter settings")]
            public EventSettings ChinookSettings = new EventSettings();

            [JsonProperty(PropertyName = "Christmas settings")]
            public EventSettings ChristmasSettings = new EventSettings();

            [JsonProperty(PropertyName = "Clan settings")]
            public EventSettings ClanSettings = new EventSettings();

            [JsonProperty(PropertyName = "Dangerous Treasures settings")]
            public EventSettings DangerousTreasuresSettings = new EventSettings();

            [JsonProperty(PropertyName = "Duel settings")]
            public EventSettings DuelSettings = new EventSettings();

            [JsonProperty(PropertyName = "Godmode settings")]
            public EventSettings GodmodeSettings = new EventSettings();

            [JsonProperty(PropertyName = "Easter settings")]
            public EventSettings EasterSettings = new EventSettings();

            [JsonProperty(PropertyName = "Error settings")]
            public EventSettings ErrorSettings = new EventSettings();

            [JsonProperty(PropertyName = "Hackable Locked Crate settings")]
            public EventSettings LockedCrateSettings = new EventSettings();

            [JsonProperty(PropertyName = "Halloween settings")]
            public EventSettings HalloweenSettings = new EventSettings();

            [JsonProperty(PropertyName = "Helicopter settings")]
            public EventSettings HelicopterSettings = new EventSettings();

            [JsonProperty(PropertyName = "NTeleportation settings")]
            public EventSettings NTeleportationSettings = new EventSettings();

            [JsonProperty(PropertyName = "Permissions settings")]
            public EventSettings PermissionsSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player death settings")]
            public EventSettings PlayerDeathSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player DeathNotes settings")]
            public EventSettings PlayerDeathNotesSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player connect advanced info settings")]
            public EventSettings PlayerConnectedInfoSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player connect settings")]
            public EventSettings PlayerConnectedSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player disconnect settings")]
            public EventSettings PlayerDisconnectedSettings = new EventSettings();

            [JsonProperty(PropertyName = "Player Respawned settings")]
            public EventSettings PlayerRespawnedSettings = new EventSettings();

            [JsonProperty(PropertyName = "Private Messages settings")]
            public EventSettings PrivateMessagesSettings = new EventSettings();

            [JsonProperty(PropertyName = "Raidable Bases settings")]
            public EventSettings RaidableBasesSettings = new EventSettings();

            [JsonProperty(PropertyName = "Rcon command settings")]
            public EventSettings RconCommandSettings = new EventSettings();

            [JsonProperty(PropertyName = "Rcon connection settings")]
            public EventSettings RconConnectionSettings = new EventSettings();

            [JsonProperty(PropertyName = "Rust Kits settings")]
            public EventSettings RustKitsSettings = new EventSettings();

            [JsonProperty(PropertyName = "SantaSleigh settings")]
            public EventSettings SantaSleighSettings = new EventSettings();

            [JsonProperty(PropertyName = "Server messages settings")]
            public EventSettings ServerMessagesSettings = new EventSettings();

            [JsonProperty(PropertyName = "Server state settings")]
            public EventSettings ServerStateSettings = new EventSettings();

            [JsonProperty(PropertyName = "Supply Drop settings")]
            public EventSettings SupplyDropSettings = new EventSettings();

            [JsonProperty(PropertyName = "Teams settings")]
            public EventSettings TeamsSettings = new EventSettings();

            [JsonProperty(PropertyName = "User Banned settings")]
            public EventSettings UserBannedSettings = new EventSettings();

            [JsonProperty(PropertyName = "User Kicked settings")]
            public EventSettings UserKickedSettings = new EventSettings();

            [JsonProperty(PropertyName = "User Muted settings")]
            public EventSettings UserMutedSettings = new EventSettings();

            [JsonProperty(PropertyName = "User Name Updated settings")]
            public EventSettings UserNameUpdateSettings = new EventSettings();

            [JsonProperty(PropertyName = "Vanish settings")]
            public EventSettings VanishSettings = new EventSettings();
        }

        private class GlobalSettings
        {
            [JsonProperty(PropertyName = "Log to console?")]
            public bool LoggingEnabled = false;

            [JsonProperty(PropertyName = "Use AntiSpam plugin on chat messages")]
            public bool UseAntiSpam = false;

            [JsonProperty(PropertyName = "Use UFilter plugin on chat messages")]
            public bool UseUFilter = false;

            [JsonProperty(PropertyName = "Hide admin connect/disconnect messages")]
            public bool HideAdmin = false;

            [JsonProperty(PropertyName = "Hide NPC death messages")]
            public bool HideNPC = false;

            [JsonProperty(PropertyName = "Replacement string for tags")]
            public string TagsReplacement = "`";

            [JsonProperty(PropertyName = "Queue interval (1 message per ? seconds)")]
            public float QueueInterval = 1f;

            [JsonProperty(PropertyName = "Queue cooldown if connection error (seconds)")]
            public float QueueCooldown = 60f;

            [JsonProperty(PropertyName = "Default WebhookURL")]
            public string DefaultWebhookURL = string.Empty;

            [JsonProperty(PropertyName = "RCON command blacklist", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RCONCommandBlacklist = new List<string>()
            {
                "playerlist",
                "status"
            };
        }

        private class EventSettings
        {
            [JsonProperty(PropertyName = "WebhookURL")]
            public string WebhookURL = "";

            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled = false;
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
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region Localization

        private string Lang(string key, string userIDString = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, userIDString).Replace("{time}", DateTime.Now.ToShortTimeString()), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }

        private static class LangKeys
        {
            public static class Event
            {
                private const string Base = nameof(Event) + ".";
                public const string Bradley = Base + nameof(Bradley);
                public const string CargoPlane = Base + nameof(CargoPlane);
                public const string CargoShip = Base + nameof(CargoShip);
                public const string Chat = Base + nameof(Chat);
                public const string ChatTeam = Base + nameof(ChatTeam);
                public const string Chinook = Base + nameof(Chinook);
                public const string Christmas = Base + nameof(Christmas);
                public const string Death = Base + nameof(Death);
                public const string Easter = Base + nameof(Easter);
                public const string EasterWinner = Base + nameof(EasterWinner);
                public const string Error = Base + nameof(Error);
                public const string Halloween = Base + nameof(Halloween);
                public const string HalloweenWinner = Base + nameof(HalloweenWinner);
                public const string Helicopter = Base + nameof(Helicopter);
                public const string Initialized = Base + nameof(Initialized);
                public const string LockedCrate = Base + nameof(LockedCrate);
                public const string PlayerConnected = Base + nameof(PlayerConnected);
                public const string PlayerConnectedInfo = Base + nameof(PlayerConnectedInfo);
                public const string PlayerDisconnected = Base + nameof(PlayerDisconnected);
                public const string PlayerRespawned = Base + nameof(PlayerRespawned);
                public const string RconCommand = Base + nameof(RconCommand);
                public const string RconConnection = Base + nameof(RconConnection);
                public const string SantaSleigh = Base + nameof(SantaSleigh);
                public const string ServerMessage = Base + nameof(ServerMessage);
                public const string Shutdown = Base + nameof(Shutdown);
                public const string SupplyDrop = Base + nameof(SupplyDrop);
                public const string SupplyDropLanded = Base + nameof(SupplyDropLanded);
                public const string SupplySignal = Base + nameof(SupplySignal);
                public const string Team = Base + nameof(Team);
                public const string UserBanned = Base + nameof(UserBanned);
                public const string UserKicked = Base + nameof(UserKicked);
                public const string UserMuted = Base + nameof(UserMuted);
                public const string UserNameUpdated = Base + nameof(UserNameUpdated);
                public const string UserUnbanned = Base + nameof(UserUnbanned);
                public const string UserUnmuted = Base + nameof(UserUnmuted);
            }

            public static class Permission
            {
                private const string Base = nameof(Permission) + ".";
                public const string GroupCreated = Base + nameof(GroupCreated);
                public const string GroupDeleted = Base + nameof(GroupDeleted);
                public const string UserGroupAdded = Base + nameof(UserGroupAdded);
                public const string UserGroupRemoved = Base + nameof(UserGroupRemoved);
                public const string UserPermissionGranted = Base + nameof(UserPermissionGranted);
                public const string UserPermissionRevoked = Base + nameof(UserPermissionRevoked);
            }

            public static class Plugin
            {
                private const string Base = nameof(Plugin) + ".";
                public const string AdminHammerOff = Base + nameof(AdminHammerOff);
                public const string AdminHammerOn = Base + nameof(AdminHammerOn);
                public const string AdminRadarOff = Base + nameof(AdminRadarOff);
                public const string AdminRadarOn = Base + nameof(AdminRadarOn);
                public const string ClanCreated = Base + nameof(ClanCreated);
                public const string ClanDisbanded = Base + nameof(ClanDisbanded);
                public const string DangerousTreasuresEnded = Base + nameof(DangerousTreasuresEnded);
                public const string DangerousTreasuresStarted = Base + nameof(DangerousTreasuresStarted);
                public const string DeathNotes = Base + nameof(DeathNotes);
                public const string Duel = Base + nameof(Duel);
                public const string GodmodeOff = Base + nameof(GodmodeOff);
                public const string GodmodeOn = Base + nameof(GodmodeOn);
                public const string NTeleportation = Base + nameof(NTeleportation);
                public const string PersonalHelicopter = Base + nameof(PersonalHelicopter);
                public const string PrivateMessage = Base + nameof(PrivateMessage);
                public const string RaidableBaseCompleted = Base + nameof(RaidableBaseCompleted);
                public const string RaidableBaseEnded = Base + nameof(RaidableBaseEnded);
                public const string RaidableBaseStarted = Base + nameof(RaidableBaseStarted);
                public const string RustKits = Base + nameof(RustKits);
                public const string TimedGroupAdded = Base + nameof(TimedGroupAdded);
                public const string TimedGroupExtended = Base + nameof(TimedGroupExtended);
                public const string TimedPermissionExtended = Base + nameof(TimedPermissionExtended);
                public const string TimedPermissionGranted = Base + nameof(TimedPermissionGranted);
                public const string VanishOff = Base + nameof(VanishOff);
                public const string VanishOn = Base + nameof(VanishOn);
            }

            public static class Format
            {
                private const string Base = nameof(Format) + ".";
                public const string CargoShip = Base + nameof(CargoShip);
                public const string Created = Base + nameof(Created);
                public const string Day = Base + nameof(Day);
                public const string Days = Base + nameof(Days);
                public const string Disbanded = Base + nameof(Disbanded);
                public const string Easy = Base + nameof(Easy);
                public const string Expert = Base + nameof(Expert);
                public const string Hard = Base + nameof(Hard);
                public const string Hour = Base + nameof(Hour);
                public const string Hours = Base + nameof(Hours);
                public const string LargeOilRig = Base + nameof(LargeOilRig);
                public const string Medium = Base + nameof(Medium);
                public const string Minute = Base + nameof(Minute);
                public const string Minutes = Base + nameof(Minutes);
                public const string Nightmare = Base + nameof(Nightmare);
                public const string OilRig = Base + nameof(OilRig);
                public const string Second = Base + nameof(Second);
                public const string Seconds = Base + nameof(Seconds);
                public const string Updated = Base + nameof(Updated);
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Event.Bradley] = ":dagger: {time} Bradley spawned `{0}`",
                [LangKeys.Event.CargoPlane] = ":airplane: {time} Cargo Plane incoming `{0}`",
                [LangKeys.Event.CargoShip] = ":ship: {time} Cargo Ship incoming `{0}`",
                [LangKeys.Event.Chat] = ":speech_left: {time} **{0}**: {1}",
                [LangKeys.Event.ChatTeam] = ":busts_in_silhouette: {time} **{0}**: {1}",
                [LangKeys.Event.Chinook] = ":helicopter: {time} Chinook 47 incoming `{0}`",
                [LangKeys.Event.Christmas] = ":christmas_tree: {time} Christmas event started",
                [LangKeys.Event.Death] = ":skull: {time} `{0}` died",
                [LangKeys.Event.Easter] = ":egg: {time} Easter event started",
                [LangKeys.Event.EasterWinner] = ":egg: {time} Easter event ended. The winner is `{0}`",
                [LangKeys.Event.Error] = ":octagonal_sign: {time}\n{0}",
                [LangKeys.Event.Halloween] = ":jack_o_lantern: {time} Halloween event started",
                [LangKeys.Event.HalloweenWinner] = ":jack_o_lantern: {time} Halloween event ended. The winner is `{0}`",
                [LangKeys.Event.Helicopter] = ":dagger: {time} Helicopter incoming `{0}`",
                [LangKeys.Event.Initialized] = ":ballot_box_with_check: {time} Server is online again!",
                [LangKeys.Event.LockedCrate] = ":package: {time} Codelocked crate is here `{0}`",
                [LangKeys.Event.PlayerConnected] = ":white_check_mark: {time} {0} connected",
                [LangKeys.Event.PlayerConnectedInfo] = ":detective: {time} {0} connected. SteamID: `{1}` IP: `{2}`",
                [LangKeys.Event.PlayerDisconnected] = ":x: {time} {0} disconnected ({1})",
                [LangKeys.Event.PlayerRespawned] = ":baby_symbol: {time} `{0}` has been spawned at `{1}`",
                [LangKeys.Event.RconCommand] = ":satellite: {time} RCON command `{0}` is run from `{1}`",
                [LangKeys.Event.RconConnection] = ":satellite: {time} RCON connection is opened from `{0}`",
                [LangKeys.Event.Team] = ":family_man_girl_boy: {time} Team was `{0}`\n{1}",
                [LangKeys.Event.SantaSleigh] = ":santa: {time} SantaSleigh Event started",
                [LangKeys.Event.ServerMessage] = ":desktop: {time} `{0}`",
                [LangKeys.Event.Shutdown] = ":stop_sign: {time} Server is shutting down!",
                [LangKeys.Event.SupplyDrop] = ":parachute: {time} SupplyDrop incoming at `{0}`",
                [LangKeys.Event.SupplyDropLanded] = ":gift: {time} SupplyDrop landed at `{0}`",
                [LangKeys.Event.SupplySignal] = ":firecracker: {time} SupplySignal was thrown by `{0}` at `{1}`",
                [LangKeys.Event.UserBanned] = ":no_entry: {time} Player `{0}` SteamID: `{1}` IP: `{2}` was banned: `{3}`",
                [LangKeys.Event.UserKicked] = ":hiking_boot: {time} Player `{0}` SteamID: `{1}` was kicked: `{2}`",
                [LangKeys.Event.UserMuted] = ":mute: {time} `{0}` was muted by `{1}` for `{2}` (`{3}`)",
                [LangKeys.Event.UserNameUpdated] = ":label: {time} `{0}` changed name to `{1}` SteamID: `{2}`",
                [LangKeys.Event.UserUnbanned] = ":ok: {time} Player `{0}` SteamID: `{1}` IP: `{2}` was unbanned",
                [LangKeys.Event.UserUnmuted] = ":speaker: {time} `{0}` was unmuted `{1}`",
                [LangKeys.Format.CargoShip] = "Cargo Ship",
                [LangKeys.Format.Created] = "created",
                [LangKeys.Format.Day] = "day",
                [LangKeys.Format.Days] = "days",
                [LangKeys.Format.Disbanded] = "disbanded",
                [LangKeys.Format.Easy] = "Easy",
                [LangKeys.Format.Expert] = "Expert",
                [LangKeys.Format.Hard] = "Hard",
                [LangKeys.Format.Hour] = "hour",
                [LangKeys.Format.Hours] = "hours",
                [LangKeys.Format.LargeOilRig] = "Large Oil Rig",
                [LangKeys.Format.Medium] = "Medium",
                [LangKeys.Format.Minute] = "minute",
                [LangKeys.Format.Minutes] = "minutes",
                [LangKeys.Format.Nightmare] = "Nightmare",
                [LangKeys.Format.OilRig] = "Oil Rig",
                [LangKeys.Format.Second] = "second",
                [LangKeys.Format.Seconds] = "seconds",
                [LangKeys.Format.Updated] = "updated",
                [LangKeys.Permission.GroupCreated] = ":family: {time} Group `{0}` has been created",
                [LangKeys.Permission.GroupDeleted] = ":family: {time} Group `{0}` has been deleted",
                [LangKeys.Permission.UserGroupAdded] = ":family: {time} `{0}` `{1}` is added to group `{2}`",
                [LangKeys.Permission.UserGroupRemoved] = ":family: {time} `{0}` `{1}` is removed from group `{2}`",
                [LangKeys.Permission.UserPermissionGranted] = ":key: {time} `{0}` `{1}` is granted `{2}`",
                [LangKeys.Permission.UserPermissionRevoked] = ":key: {time} `{0}` `{1}` is revoked `{2}`",
                [LangKeys.Plugin.AdminHammerOff] = ":hammer: {time} AdminHammer enabled by `{0}`",
                [LangKeys.Plugin.AdminHammerOn] = ":hammer: {time} AdminHammer disabled by `{0}`",
                [LangKeys.Plugin.AdminRadarOff] = ":compass: {time} Admin Radar enabled by `{0}`",
                [LangKeys.Plugin.AdminRadarOn] = ":compass: {time} Admin Radar disabled by `{0}`",
                [LangKeys.Plugin.ClanCreated] = ":family_mwgb: {time} **{0}** clan was created",
                [LangKeys.Plugin.ClanDisbanded] = ":family_mwgb: {time} **{0}** clan was disbanded",
                [LangKeys.Plugin.DangerousTreasuresEnded] = ":pirate_flag: {time} Dangerous Treasures event at `{0}` is ended",
                [LangKeys.Plugin.DangerousTreasuresStarted] = ":pirate_flag: {time} Dangerous Treasures started at `{0}`",
                [LangKeys.Plugin.DeathNotes] = ":skull_crossbones: {time} {0}",
                [LangKeys.Plugin.Duel] = ":crossed_swords: {time} `{0}` has defeated `{1}` in a duel",
                [LangKeys.Plugin.GodmodeOff] = ":angel: {time} Godmode disabled for `{0}`",
                [LangKeys.Plugin.GodmodeOn] = ":angel: {time} Godmode enabled for `{0}`",
                [LangKeys.Plugin.NTeleportation] = ":cyclone: {time} `{0}` teleported from `{1}` `{2}` to `{3}` `{4}`",
                [LangKeys.Plugin.PersonalHelicopter] = ":dagger: {time} Personal Helicopter incoming `{0}`",
                [LangKeys.Plugin.PrivateMessage] = ":envelope: {time} PM from `{0}` to `{1}`: {2}",
                [LangKeys.Plugin.RaidableBaseCompleted] = ":homes: {time} {1} Raidable Base owned by {2} at `{0}` has been raided by **{3}**",
                [LangKeys.Plugin.RaidableBaseEnded] = ":homes: {time} {1} Raidable Base at `{0}` has ended",
                [LangKeys.Plugin.RaidableBaseStarted] = ":homes: {time} {1} Raidable Base spawned at `{0}`",
                [LangKeys.Plugin.RustKits] = ":shopping_bags: {time} `{0}` redeemed a kit `{1}`",
                [LangKeys.Plugin.TimedGroupAdded] = ":timer: {time} `{0}` `{1}` is added to `{2}` for {3}",
                [LangKeys.Plugin.TimedGroupExtended] = ":timer: {time} `{0}` `{1}` timed group `{2}` is extended to {3}",
                [LangKeys.Plugin.TimedPermissionExtended] = ":timer: {time} `{0}` `{1}` timed permission `{2}` is extended to {3}",
                [LangKeys.Plugin.TimedPermissionGranted] = ":timer: {time} `{0}` `{1}` is granted `{2}` for {3}",
                [LangKeys.Plugin.VanishOff] = ":ghost: {time} Vanish: Disabled for `{0}`",
                [LangKeys.Plugin.VanishOn] = ":ghost: {time} Vanish: Enabled for `{0}`",
            }, this);
        }

        #endregion Localization

        #region Events Hooks

        private void OnAdminHammerEnabled(BasePlayer player)
        {
            LogToConsole($"AdminHammer enabled by {player.UserIDString} {player.displayName}");

            DiscordSendMessage(Lang(LangKeys.Plugin.AdminHammerOff, null, ReplaceChars(player.displayName)), _configData.AdminHammerSettings.WebhookURL);
        }

        private void OnAdminHammerDisabled(BasePlayer player)
        {
            LogToConsole($"AdminHammer disabled by {player.UserIDString} {player.displayName}");

            DiscordSendMessage(Lang(LangKeys.Plugin.AdminHammerOn, null, ReplaceChars(player.displayName)), _configData.AdminHammerSettings.WebhookURL);
        }

        private void OnBetterChatMuted(IPlayer target, IPlayer initiator, string reason)
        {
            LogToConsole($"{target.Name} was muted by {initiator.Name} for ever ({reason})");

            DiscordSendMessage(Lang(LangKeys.Event.UserMuted, null, ReplaceChars(target.Name), ReplaceChars(initiator.Name), "ever", ReplaceChars(reason)), _configData.UserMutedSettings.WebhookURL);
        }

        private void OnBetterChatMuteExpired(IPlayer player)
        {
            LogToConsole($"{player.Name} was unmuted by SERVER");

            DiscordSendMessage(Lang(LangKeys.Event.UserUnmuted, null, ReplaceChars(player.Name), "SERVER"), _configData.UserMutedSettings.WebhookURL);
        }

        private void OnBetterChatTimeMuted(IPlayer target, IPlayer initiator, TimeSpan time, string reason)
        {
            LogToConsole($"{target.Name} was muted by {initiator.Name} for {time.ToShortString()} ({reason})");

            DiscordSendMessage(Lang(LangKeys.Event.UserMuted, null, ReplaceChars(target.Name), ReplaceChars(initiator.Name), time.ToShortString(), ReplaceChars(reason)), _configData.UserMutedSettings.WebhookURL);
        }

        private void OnBetterChatUnmuted(IPlayer target, IPlayer initiator)
        {
            LogToConsole($"{target.Name} was unmuted by {initiator.Name}");

            DiscordSendMessage(Lang(LangKeys.Event.UserUnmuted, null, ReplaceChars(target.Name), ReplaceChars(initiator.Name)), _configData.UserMutedSettings.WebhookURL);
        }

        private void OnClanCreate(string tag)
        {
            LogToConsole($"{tag} clan was created");

            DiscordSendMessage(Lang(LangKeys.Plugin.ClanCreated, null, ReplaceChars(tag)), _configData.ClanSettings.WebhookURL);
        }

        private void OnClanDisbanded(string tag)
        {
            LogToConsole($"{tag} clan was disbanded");

            DiscordSendMessage(Lang(LangKeys.Plugin.ClanDisbanded, null, ReplaceChars(tag)), _configData.ClanSettings.WebhookURL);
        }

        private void OnDangerousEventStarted(Vector3 containerPos)
        {
            HandleDangerousTreasures(containerPos, LangKeys.Plugin.DangerousTreasuresStarted);
        }

        private void OnDangerousEventEnded(Vector3 containerPos)
        {
            HandleDangerousTreasures(containerPos, LangKeys.Plugin.DangerousTreasuresEnded);
        }

        private void OnDeathNotice(Dictionary<string, object> data, string message)
        {
            DiscordSendMessage(Lang(LangKeys.Plugin.DeathNotes, null, StripRustTags(Formatter.ToPlaintext(message))), _configData.PlayerDeathNotesSettings.WebhookURL);
        }

        private void OnDuelistDefeated(BasePlayer attacker, BasePlayer victim)
        {
            if (!attacker.IsValid() || !victim.IsValid())
            {
                return;
            }

            LogToConsole($"{attacker.displayName} has defeated {victim.displayName} in a duel");

            DiscordSendMessage(Lang(LangKeys.Plugin.Duel, null, ReplaceChars(attacker.displayName), ReplaceChars(victim.displayName)), _configData.DuelSettings.WebhookURL);
        }

        private void OnEntitySpawned(BaseHelicopter entity)
        {
            NextTick(() => HandleEntity(entity));
        }

        private void OnEntitySpawned(BradleyAPC entity) => HandleEntity(entity);

        private void OnEntitySpawned(CargoPlane entity) => HandleEntity(entity);

        private void OnEntitySpawned(CargoShip entity) => HandleEntity(entity);

        private void OnEntitySpawned(CH47HelicopterAIController entity) => HandleEntity(entity);

        private void OnEntitySpawned(EggHuntEvent entity) => HandleEntity(entity);

        private void OnEntitySpawned(HackableLockedCrate entity) => HandleEntity(entity);

        private void OnEntitySpawned(SantaSleigh entity) => HandleEntity(entity);

        private void OnEntitySpawned(SupplyDrop entity) => HandleEntity(entity);

        private void OnEntitySpawned(XMasRefill entity) => HandleEntity(entity);

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (!player.IsValid() || info == null)
            {
                return;
            }

            if (_configData.GlobalSettings.HideNPC && (player.IsNpc || !player.userID.IsSteamId()))
            {
                return;
            }

            LogToConsole($"{player.displayName} died.");

            DiscordSendMessage(Lang(LangKeys.Event.Death, null, ReplaceChars(player.displayName)), _configData.PlayerDeathSettings.WebhookURL);
        }

        private void OnEntityKill(EggHuntEvent entity)
        {
            if (!entity.IsValid())
            {
                return;
            }

            List<EggHuntEvent.EggHunter> winners = entity.GetTopHunters();
            string winner;
            if (winners.Count > 0)
            {
                winner = ReplaceChars(winners[0].displayName);
            }
            else
            {
                winner = "No winner";
            }

            bool isHalloween = entity is HalloweenHunt;
            if (isHalloween)
            {
                if (_configData.HalloweenSettings.Enabled)
                {
                    LogToConsole("Halloween Hunt Event has ended. The winner is " + winner);

                    DiscordSendMessage(Lang(LangKeys.Event.HalloweenWinner, null, winner), _configData.HalloweenSettings.WebhookURL);
                }
            }
            else
            {
                if (_configData.EasterSettings.Enabled)
                {
                    LogToConsole("Egg Hunt Event has ended. The winner is " + winner);

                    DiscordSendMessage(Lang(LangKeys.Event.EasterWinner, null, winner), _configData.EasterSettings.WebhookURL);
                }
            }
        }

        private void OnEntityKill(CargoShip cargoShip)
        {
            if (cargoShip.IsValid())
            {
                _cargoShips.Remove(cargoShip.net.ID.Value);
            }
        }

        private void OnExplosiveThrown(BasePlayer player, SupplySignal entity) => HandleSupplySignal(player, entity);

        private void OnExplosiveDropped(BasePlayer player, SupplySignal entity) => HandleSupplySignal(player, entity);

        private void OnGodmodeToggled(string playerID, bool enabled)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            if (enabled)
            {
                LogToConsole($"Godmode disabled for {player.Id} {player.Name}");

                DiscordSendMessage(Lang(LangKeys.Plugin.GodmodeOn, null, ReplaceChars(player.Name)), _configData.GodmodeSettings.WebhookURL);

                return;
            }

            LogToConsole($"Godmode enabled for {player.Id} {player.Name}");

            DiscordSendMessage(Lang(LangKeys.Plugin.GodmodeOff, null, ReplaceChars(player.Name)), _configData.GodmodeSettings.WebhookURL);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_configData.PlayerConnectedSettings.Enabled)
            {
                LogToConsole($"Player {player.displayName} connected.");

                if (!_configData.GlobalSettings.HideAdmin || !player.IsAdmin)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.Append(Lang(LangKeys.Event.PlayerConnected, null, ReplaceChars(player.displayName)));

                    if (player.net.connection.ipaddress.StartsWith("127.")
                    || player.net.connection.ipaddress.StartsWith("10.")
                    || player.net.connection.ipaddress.StartsWith("172.16.")
                    || player.net.connection.ipaddress.StartsWith("192.168."))
                    {
                        sb.Append(" :signal_strength:");
                        DiscordSendMessage(sb.ToString(), _configData.PlayerConnectedSettings.WebhookURL);
                    }
                    else
                    {
                        webrequest.Enqueue($"http://ip-api.com/json/{player.net.connection.ipaddress.Split(':')[0]}", null, (code, response) => {
                            if (code == 200 && response != null)
                            {
                                sb.Append(" :flag_");
                                sb.Append(JsonConvert.DeserializeObject<Response>(response).CountryCode.ToLower());
                                sb.Append(":");
                            }

                            DiscordSendMessage(sb.ToString(), _configData.PlayerConnectedSettings.WebhookURL);
                        }, this, RequestMethod.GET);
                    }
                }
            }

            if (_configData.PlayerConnectedInfoSettings.Enabled)
            {
                DiscordSendMessage(Lang(LangKeys.Event.PlayerConnectedInfo, null, ReplaceChars(player.displayName), player.UserIDString, player.net.connection.ipaddress.Split(':')[0]), _configData.PlayerConnectedInfoSettings.WebhookURL);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!player.IsValid())
            {
                return;
            }

            LogToConsole($"Player {player.displayName} disconnected ({reason}).");

            if (!_configData.GlobalSettings.HideAdmin || !player.IsAdmin)
            {
                DiscordSendMessage(Lang(LangKeys.Event.PlayerDisconnected, null, ReplaceChars(player.displayName), ReplaceChars(reason)), _configData.PlayerDisconnectedSettings.WebhookURL);
            }
        }

        private void OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (!player.IsValid() || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (IsPluginLoaded(BetterChatMute))
            {
                _resultCall = BetterChatMute.Call("API_IsMuted", player.IPlayer);

                if (_resultCall is bool && (bool)_resultCall)
                {
                    return;
                }
            }

            if (_configData.GlobalSettings.UseAntiSpam && IsPluginLoaded(AntiSpam))
            {
                _resultCall = AntiSpam.Call("GetSpamFreeText", message);

                message = (_resultCall as string);

                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }
            }

            if (_configData.GlobalSettings.UseUFilter && IsPluginLoaded(UFilter))
            {
                _sb.Clear();
                _sb.Append(message);

                _resultCall = UFilter.Call("Profanities", message);

                if (_resultCall is string[])
                {
                    _profanities = _resultCall as string[];
                }

                foreach (string profanity in _profanities)
                {
                    _sb.Replace(profanity, new string('＊', profanity.Length));
                }

                message = _sb.ToString();

                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }
            }

            message = ReplaceChars(message);

            switch (channel)
            {
                case ConVar.Chat.ChatChannel.Global:
                case ConVar.Chat.ChatChannel.Local:
                    if (_configData.ChatSettings.Enabled)
                    {
                        DiscordSendMessage(Lang(LangKeys.Event.Chat, null, ReplaceChars(player.displayName), message), _configData.ChatSettings.WebhookURL);
                    }
                    break;
                case ConVar.Chat.ChatChannel.Team:
                    if (_configData.ChatTeamSettings.Enabled)
                    {
                        DiscordSendMessage(Lang(LangKeys.Event.ChatTeam, null, ReplaceChars(player.displayName), message), _configData.ChatTeamSettings.WebhookURL);
                    }
                    break;
            }
        }

        private void OnPlayerTeleported(BasePlayer player, Vector3 oldPosition, Vector3 newPosition)
        {
            LogToConsole($"NTeleportation {player.UserIDString} {player.displayName} from {oldPosition} to {newPosition}");

            DiscordSendMessage(Lang(LangKeys.Plugin.NTeleportation, null, ReplaceChars(player.displayName), GetGridPosition(oldPosition), oldPosition, GetGridPosition(newPosition), newPosition), _configData.NTeleportationSettings.WebhookURL);
        }

        private void OnPMProcessed(IPlayer sender, IPlayer target, string message)
        {
            LogToConsole($"PM from `{sender.Name}` to `{target.Name}`: {message}");

            DiscordSendMessage(Lang(LangKeys.Plugin.PrivateMessage, null, ReplaceChars(sender.Name), ReplaceChars(target.Name), ReplaceChars(message)), _configData.PrivateMessagesSettings.WebhookURL);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (string.IsNullOrWhiteSpace(player?.displayName))
            {
                return;
            }

            LogToConsole($"{player.displayName} has been spawned at {GetGridPosition(player.transform.position)}");

            DiscordSendMessage(Lang(LangKeys.Event.PlayerRespawned, null, ReplaceChars(player.displayName), GetGridPosition(player.transform.position)), _configData.PlayerRespawnedSettings.WebhookURL);
        }

        private void OnRadarActivated(BasePlayer player)
        {
            LogToConsole($"Admin Radar enabled by {player.UserIDString} {player.displayName}");

            DiscordSendMessage(Lang(LangKeys.Plugin.AdminRadarOn, null, ReplaceChars(player.displayName)), _configData.AdminRadarSettings.WebhookURL);
        }

        private void OnRadarDeactivated(BasePlayer player)
        {
            LogToConsole($"Admin Radar disabled by {player.UserIDString} {player.displayName}");

            DiscordSendMessage(Lang(LangKeys.Plugin.AdminRadarOff, null, ReplaceChars(player.displayName)), _configData.AdminRadarSettings.WebhookURL);
        }

        private void OnRaidableBaseStarted(Vector3 raidPos, int difficulty)
        {
            HandleRaidableBase(raidPos, difficulty, LangKeys.Plugin.RaidableBaseStarted);
        }

        private void OnRaidableBaseEnded(Vector3 raidPos, int difficulty)
        {
            HandleRaidableBase(raidPos, difficulty, LangKeys.Plugin.RaidableBaseEnded);
        }

        private void OnRaidableBaseCompleted(Vector3 raidPos, int difficulty, bool allowPVP, string id, float spawnTime, float despawnTime, float loadTime, ulong ownerId, BasePlayer owner, List<BasePlayer> raiders)
        {
            HandleRaidableBase(raidPos, difficulty, LangKeys.Plugin.RaidableBaseCompleted, owner, raiders);
        }

        private void OnRconConnection(IPAddress ip)
        {
            LogToConsole($"RCON connection is opened from {ip}");

            DiscordSendMessage(Lang(LangKeys.Event.RconConnection, null, ip.ToString()), _configData.RconConnectionSettings.WebhookURL);
        }

        private void OnRconCommand(IPAddress ip, string command, string[] args)
        {
            foreach (string rconCommand in _configData.GlobalSettings.RCONCommandBlacklist)
            {
                if (command.ToLower().Equals(rconCommand.ToLower()))
                {
                    return;
                }
            }

            for (int i = 0; i < args.Length; i++)
            {
                command += $" {args[i]}";
            }

            LogToConsole($"RCON command {command} is run from {ip}");

            DiscordSendMessage(Lang(LangKeys.Event.RconCommand, null, command, ip), _configData.RconCommandSettings.WebhookURL);
        }

        private void OnSupplyDropLanded(SupplyDrop entity)
        {
            if (!entity.IsValid() || _listSupplyDrops.Contains(entity.net.ID.Value))
            {
                return;
            }

            LogToConsole($"SupplyDrop landed at {GetGridPosition(entity.transform.position)}");

            DiscordSendMessage(Lang(LangKeys.Event.SupplyDropLanded, null, GetGridPosition(entity.transform.position)), _configData.SupplyDropSettings.WebhookURL);

            _entityID = entity.net.ID.Value;

            _listSupplyDrops.Add(_entityID);

            timer.Once(60f, () => _listSupplyDrops.Remove(_entityID));
        }

        private void OnUserBanned(string name, string id, string ipAddress, string reason)
        {
            LogToConsole($"Player {name} ({id}) at {ipAddress} was banned: {reason}");

            DiscordSendMessage(Lang(LangKeys.Event.UserBanned, null, ReplaceChars(name), id, ipAddress, ReplaceChars(reason)), _configData.UserBannedSettings.WebhookURL);
        }

        private void OnUserKicked(IPlayer player, string reason)
        {
            LogToConsole($"Player {player.Name} ({player.Id}) was kicked ({reason})");

            DiscordSendMessage(Lang(LangKeys.Event.UserKicked, null, ReplaceChars(player.Name), player.Id, ReplaceChars(reason)), _configData.UserKickedSettings.WebhookURL);
        }

        private void OnUserUnbanned(string name, string id, string ipAddress)
        {
            LogToConsole($"Player {name} ({id}) at {ipAddress} was unbanned");

            DiscordSendMessage(Lang(LangKeys.Event.UserUnbanned, null, ReplaceChars(name), id, ipAddress), _configData.UserBannedSettings.WebhookURL);
        }

        private void OnUserNameUpdated(string id, string oldName, string newName)
        {
            if (oldName.Equals(newName) || oldName.Equals("Unnamed"))
            {
                return;
            }

            LogToConsole($"Player name changed from {oldName} to {newName} for ID {id}");

            DiscordSendMessage(Lang(LangKeys.Event.UserNameUpdated, null, ReplaceChars(oldName), ReplaceChars(newName), id), _configData.UserNameUpdateSettings.WebhookURL);
        }

        private void OnServerMessage(string message, string name, string color, ulong id)
        {
            LogToConsole($"ServerMessage: {message}");

            DiscordSendMessage(Lang(LangKeys.Event.ServerMessage, null, message), _configData.ServerMessagesSettings.WebhookURL);
        }

        private void OnKitRedeemed(BasePlayer player, string kitName)
        {
            LogToConsole($"{player.UserIDString} {player.displayName} redeemed a kit {kitName}");

            DiscordSendMessage(Lang(LangKeys.Plugin.RustKits, null, ReplaceChars(player.displayName), ReplaceChars(kitName)), _configData.RustKitsSettings.WebhookURL);
        }

        private void OnVanishDisappear(BasePlayer player)
        {
            LogToConsole($"Vanish: Enabled ({player.UserIDString} {player.displayName})");

            DiscordSendMessage(Lang(LangKeys.Plugin.VanishOn, null, ReplaceChars(player.displayName)), _configData.VanishSettings.WebhookURL);
        }

        private void OnVanishReappear(BasePlayer player)
        {
            LogToConsole($"Vanish: Disabled ({player.UserIDString} {player.displayName})");

            DiscordSendMessage(Lang(LangKeys.Plugin.VanishOff, null, ReplaceChars(player.displayName)), _configData.VanishSettings.WebhookURL);
        }

        #region Team Hooks

        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team) => HandleTeam(team, TeamEventType.Created);

        private void OnTeamDisbanded(RelationshipManager.PlayerTeam team) => HandleTeam(team, TeamEventType.Disbanded);

        private void OnTeamUpdated(ulong currentTeam, RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            NextTick(() => {
                if (team.members.Count > 0)
                {
                    HandleTeam(team, TeamEventType.Updated);
                }
            });
        }

        private void OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer newLeader)
        {
            NextTick(() => { HandleTeam(team, TeamEventType.Updated); });
        }

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            NextTick(() => {
                if (team?.members?.Count > 0)
                {
                    HandleTeam(team, TeamEventType.Updated);
                }
            });
        }

        private void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
        {
            NextTick(() => { HandleTeam(team, TeamEventType.Updated); });
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            NextTick(() => { HandleTeam(team, TeamEventType.Updated); });
        }

        #endregion Team Hooks

        #region Permissions

        private void OnGroupCreated(string name)
        {
            LogToConsole($"Group {name} has been created");

            DiscordSendMessage(Lang(LangKeys.Permission.GroupCreated, null, name), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnGroupDeleted(string name)
        {
            LogToConsole($"Group {name} has been deleted");

            DiscordSendMessage(Lang(LangKeys.Permission.GroupDeleted, null, name), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnTimedPermissionGranted(string playerID, string permission, TimeSpan duration)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is granted {permission} for {duration}");

            DiscordSendMessage(Lang(LangKeys.Plugin.TimedPermissionGranted, null, playerID, ReplaceChars(player.Name), permission, GetFormattedDurationTime(duration)), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnTimedPermissionExtended(string playerID, string permission, TimeSpan duration)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} timed permission {permission} is extended for {duration}");

            DiscordSendMessage(Lang(LangKeys.Plugin.TimedPermissionExtended, null, playerID, ReplaceChars(player.Name), permission, GetFormattedDurationTime(duration)), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnTimedGroupAdded(string playerID, string group, TimeSpan duration)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is added to {group} for {duration}");

            DiscordSendMessage(Lang(LangKeys.Plugin.TimedGroupAdded, null, playerID, ReplaceChars(player.Name), group, GetFormattedDurationTime(duration)), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnTimedGroupExtended(string playerID, string group, TimeSpan duration)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} timed group {group} is extended for {duration}");

            DiscordSendMessage(Lang(LangKeys.Plugin.TimedGroupExtended, null, playerID, ReplaceChars(player.Name), group, GetFormattedDurationTime(duration)), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnUserGroupAdded(string playerID, string groupName)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is added to group {groupName}");

            DiscordSendMessage(Lang(LangKeys.Permission.UserGroupAdded, null, playerID, ReplaceChars(player.Name), groupName), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnUserGroupRemoved(string playerID, string groupName)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is removed from group {groupName}");

            DiscordSendMessage(Lang(LangKeys.Permission.UserGroupRemoved, null, playerID, ReplaceChars(player.Name), groupName), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnUserPermissionGranted(string playerID, string permName)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is granted permission {permName}");

            DiscordSendMessage(Lang(LangKeys.Permission.UserPermissionGranted, null, playerID, ReplaceChars(player.Name), permName), _configData.PermissionsSettings.WebhookURL);
        }

        private void OnUserPermissionRevoked(string playerID, string permName)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is revoked permission {permName}");

            DiscordSendMessage(Lang(LangKeys.Permission.UserPermissionRevoked, null, playerID, ReplaceChars(player.Name), permName), _configData.PermissionsSettings.WebhookURL);
        }

        #endregion

        #endregion Events Hooks

        #region Core Methods

        private string ReplaceChars(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            _sb.Clear();
            _sb.Append(text);
            _sb.Replace("*", "＊");
            _sb.Replace("`", "'");
            _sb.Replace("_", "＿");
            _sb.Replace("~", "～");
            _sb.Replace(">", "＞");
            _sb.Replace("@here", "here");
            _sb.Replace("@everyone", "everyone");

            return _sb.ToString();
        }

        private void DiscordSendMessage(string message, string webhookUrl, bool stripTags = false)
        {
            webhookUrl = GetWebhookURL(webhookUrl);

            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                PrintError("DiscordSendMessage: webhookUrl is null or empty!");
                return;
            }

            if (stripTags)
            {
                message = StripRustTags(message);
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                PrintError("DiscordSendMessage: message is null or empty!");
                return;
            }

            _queue.Enqueue(new QueuedMessage()
            {
                Message = message,
                WebhookUrl = webhookUrl
            });

            HandleQueue();
        }

        private void HandleQueue()
        {
            if (_retryCount > 0)
            {
                if (_timerQueueCooldown == null)
                {
                    float timeout = _configData.GlobalSettings.QueueCooldown * Math.Min(_retryCount, 10);
                    PrintWarning($"HandleQueue: connection problem detected! Retry # {_retryCount}. Next try in {timeout} seconds. Messages in queue: {_queue.Count}");

                    _timerQueueCooldown = timer.Once(timeout, () =>
                    {
                        DiscordSendMessage(_queuedMessage.WebhookUrl, new DiscordMessage(_queuedMessage.Message));

                        QueueCooldownDisable();

                        HandleQueue();
                    });
                }

                return;
            }

            if (_timerQueueCooldown == null && _timerQueue == null && _queue.Count > 0)
            {
                _queuedMessage = _queue.Dequeue();

                _sb.Clear();

                if (_queuedMessage.Message.Length > 1990)
                {
                    _queuedMessage.Message = $"{_queuedMessage.Message.Substring(0, 1990)}\n```";
                }

                _sb.AppendLine(_queuedMessage.Message);

                for (int i = 0; i < _queue.Count; i++)
                {
                    _nextMessage = _queue.Peek();

                    if (_sb.Length + _nextMessage.Message.Length > 1990
                     || _queuedMessage.WebhookUrl != _nextMessage.WebhookUrl)
                    {
                        break;
                    }

                    _nextMessage = _queue.Dequeue();
                    _sb.AppendLine(_nextMessage.Message);
                }

                _queuedMessage.Message = _sb.ToString();

                DiscordSendMessage(_queuedMessage.WebhookUrl, new DiscordMessage(_queuedMessage.Message));

                _timerQueue = timer.Once(_configData.GlobalSettings.QueueInterval, () => {
                    _timerQueue?.Destroy();
                    _timerQueue = null;

                    HandleQueue();
                });
            }
        }

        private void QueueCooldownDisable()
        {
            _timerQueueCooldown?.Destroy();
            _timerQueueCooldown = null;
        }

        private void HandleEntity(BaseEntity baseEntity)
        {
            if (!baseEntity.IsValid())
            {
                return;
            }

            Vector3 position = baseEntity.transform.position;

            if (baseEntity is BaseHelicopter)
            {
                _langKey = LangKeys.Event.Helicopter;
                _eventSettings = _configData.HelicopterSettings;
            }
            else if (baseEntity is BradleyAPC)
            {
                _langKey = LangKeys.Event.Bradley;
                _eventSettings = _configData.BradleySettings;
                LogToConsole($"BradleyAPC spawned at {GetGridPosition(position)}");
            }
            else if (baseEntity is CargoPlane)
            {
                _langKey = LangKeys.Event.CargoPlane;
                _eventSettings = _configData.CargoPlaneSettings;
                LogToConsole($"CargoPlane spawned at {GetGridPosition(position)}");
            }
            else if (baseEntity is CargoShip)
            {
                _langKey = LangKeys.Event.CargoShip;
                _eventSettings = _configData.CargoShipSettings;
                LogToConsole($"CargoShip spawned at {GetGridPosition(position)}");

                NextTick(() => {
                    if (baseEntity.IsValid() && !_cargoShips.ContainsKey(baseEntity.net.ID.Value))
                    {
                        _cargoShips[baseEntity.net.ID.Value] = (CargoShip)baseEntity;
                    }
                });
            }
            else if (baseEntity is CH47HelicopterAIController)
            {
                _langKey = LangKeys.Event.Chinook;
                _eventSettings = _configData.ChinookSettings;
                LogToConsole($"CH47Helicopter spawned at {GetGridPosition(position)}");
            }
            else if (baseEntity is HalloweenHunt)
            {
                _langKey = LangKeys.Event.Halloween;
                _eventSettings = _configData.HalloweenSettings;
                LogToConsole($"HalloweenHunt spawned at {GetGridPosition(position)}");
            }
            else if (baseEntity is EggHuntEvent)
            {
                _langKey = LangKeys.Event.Easter;
                _eventSettings = _configData.EasterSettings;
                LogToConsole("Easter event has started");
            }
            else if (baseEntity is HackableLockedCrate)
            {
                _langKey = LangKeys.Event.LockedCrate;
                _eventSettings = _configData.LockedCrateSettings;
                LogToConsole($"HackableLockedCrate spawned at {GetGridPosition(position)}");
            }
            else if (baseEntity is SantaSleigh)
            {
                _langKey = LangKeys.Event.SantaSleigh;
                _eventSettings = _configData.SantaSleighSettings;
                LogToConsole($"SantaSleigh spawned at {GetGridPosition(position)}");
            }
            else if (baseEntity is SupplyDrop)
            {
                _langKey = LangKeys.Event.SupplyDrop;
                _eventSettings = _configData.SupplyDropSettings;
                LogToConsole($"SupplyDrop spawned at {GetGridPosition(position)}");
            }
            else if (baseEntity is SupplySignal)
            {
                _langKey = LangKeys.Event.SupplySignal;
                _eventSettings = _configData.SupplyDropSettings;
                LogToConsole($"SupplySignal dropped at {GetGridPosition(position)}");
            }
            else if (baseEntity is XMasRefill)
            {
                _langKey = LangKeys.Event.Christmas;
                _eventSettings = _configData.ChristmasSettings;
                LogToConsole("Christmas event has started");
            }

            if (_eventSettings.Enabled)
            {
                if (baseEntity is BaseHelicopter)
                {
                    if (IsPluginLoaded(CallHeli))
                    {
                        _resultCall = CallHeli.Call("IsPersonal", baseEntity);

                        if (_resultCall is bool && (bool)_resultCall)
                        {
                            LogToConsole("Personal Helicopter spawned at " + GetGridPosition(position));

                            DiscordSendMessage(Lang(LangKeys.Plugin.PersonalHelicopter, null, GetGridPosition(position)), _eventSettings.WebhookURL);
                            return;
                        }
                    }

                    if (IsPluginLoaded(PersonalHeli))
                    {
                        _resultCall = PersonalHeli.Call("IsPersonal", baseEntity);

                        if (_resultCall is bool && (bool)_resultCall)
                        {
                            LogToConsole("Personal Helicopter spawned at " + GetGridPosition(position));

                            DiscordSendMessage(Lang(LangKeys.Plugin.PersonalHelicopter, null, GetGridPosition(position)), _eventSettings.WebhookURL);
                            return;
                        }
                    }

                    LogToConsole("BaseHelicopter spawned at " + GetGridPosition(position));
                }

                if (baseEntity is HackableLockedCrate)
                {
                    DiscordSendMessage(Lang(_langKey, null, GetHackableLockedCratePosition(position)), _eventSettings.WebhookURL);
                    return;
                }

                DiscordSendMessage(Lang(_langKey, null, GetGridPosition(position)), _eventSettings.WebhookURL);
            }
        }

        private void HandleSupplySignal(BasePlayer player, SupplySignal entity)
        {
            if (_configData.SupplyDropSettings.Enabled)
            {
                NextTick(() => {
                    if (player != null && entity != null)
                    {
                        LogToConsole($"SupplySignal was thrown by {player.displayName} at {GetGridPosition(entity.transform.position)}");

                        DiscordSendMessage(Lang(LangKeys.Event.SupplySignal, null, ReplaceChars(player.displayName), GetGridPosition(entity.transform.position)), _configData.SupplyDropSettings.WebhookURL);
                    }
                });
            }
        }

        private void HandleRaidableBase(Vector3 raidPos, int difficulty, string langKey, BasePlayer owner = null, List<BasePlayer> raiders = null)
        {
            if (raidPos == null)
            {
                PrintError($"{langKey}: raidPos == null");
                return;
            }

            string difficultyString;
            switch (difficulty)
            {
                case 0:
                    difficultyString = LangKeys.Format.Easy;
                    break;
                case 1:
                    difficultyString = LangKeys.Format.Medium;
                    break;
                case 2:
                    difficultyString = LangKeys.Format.Hard;
                    break;
                case 3:
                    difficultyString = LangKeys.Format.Expert;
                    break;
                case 4:
                    difficultyString = LangKeys.Format.Nightmare;
                    break;
                case 512:
                    difficultyString = string.Empty;
                    break;
                default:
                    PrintError($"{langKey}: Unknown difficulty: {difficulty}");
                    return;
            }

            switch (langKey)
            {
                case LangKeys.Plugin.RaidableBaseCompleted:
                    _sb.Clear();
                    for (int i = 0; i < raiders?.Count; i++)
                    {
                        if (i > 0)
                        {
                            _sb.Append(", ");
                        }
                        _sb.Append(raiders[i].displayName);
                    }
                    LogToConsole($"{difficultyString} Raidable Base owned by {owner?.displayName} at {GetGridPosition(raidPos)} has been raided by {_sb.ToString()}");
                    DiscordSendMessage(Lang(langKey, null, GetGridPosition(raidPos), Lang(difficultyString), owner?.displayName, _sb.ToString()), _configData.RaidableBasesSettings.WebhookURL);
                    break;
                case LangKeys.Plugin.RaidableBaseEnded:
                case LangKeys.Plugin.RaidableBaseStarted:
                    LogToConsole(difficultyString + " Raidable Base at " + GetGridPosition(raidPos) + " has " + (langKey == LangKeys.Plugin.RaidableBaseStarted ? "spawned" : "ended"));
                    DiscordSendMessage(Lang(langKey, null, GetGridPosition(raidPos), Lang(difficultyString)), _configData.RaidableBasesSettings.WebhookURL);
                    break;
            }
        }

        private void HandleDangerousTreasures(Vector3 containerPos, string langKey)
        {
            if (containerPos == null)
            {
                PrintError($"{langKey}: containerPos == null");
                return;
            }

            LogToConsole("Dangerous Treasures at " + GetGridPosition(containerPos) + " is " + (langKey == LangKeys.Plugin.DangerousTreasuresStarted ? "spawned" : "ended"));

            DiscordSendMessage(Lang(langKey, null, GetGridPosition(containerPos)), _configData.DangerousTreasuresSettings.WebhookURL);
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (_configData.ErrorSettings.Enabled && type == LogType.Error)
            {
                _sb.Clear();

                _sb.AppendLine("```cs");
                _sb.AppendLine(logString);
                _sb.AppendLine("```");

                if (!string.IsNullOrEmpty(stackTrace))
                {
                    _sb.AppendLine("```cs");
                    _sb.AppendLine(stackTrace);
                    _sb.AppendLine("```");
                }

                DiscordSendMessage(Lang(LangKeys.Event.Error, null, _sb), _configData.ErrorSettings.WebhookURL);
            }
        }

        private void HandleTeam(RelationshipManager.PlayerTeam team, TeamEventType teamEventType)
        {
            _sb.Clear();

            BasePlayer player = RelationshipManager.FindByID(team.teamLeader);

            if (!player.IsValid())
            {
                return;
            }

            _sb.AppendLine("```cs");
            _sb.AppendLine();
            _sb.Append("TeamID: ");
            _sb.Append(team.teamID);
            _sb.AppendLine();
            _sb.Append("TeamLeader: ");
            _sb.Append(player.userID);
            _sb.Append(" (");
            _sb.Append(player.displayName);
            _sb.Append(")");
            if (team.members.Count > 0)
            {
                _sb.AppendLine();
                _sb.Append("Members:");
            }

            foreach (ulong userID in team.members)
            {
                player = RelationshipManager.FindByID(userID);

                if (!player.IsValid())
                {
                    continue;
                }

                _sb.AppendLine();
                _sb.Append(player.userID);
                _sb.Append(" (");
                _sb.Append(player.displayName);
                _sb.Append(")");
            }

            _sb.AppendLine("```");

            string eventType = string.Empty;

            switch (teamEventType)
            {
                case TeamEventType.Created:
                    eventType = Lang(LangKeys.Format.Created);
                    break;
                case TeamEventType.Disbanded:
                    eventType = Lang(LangKeys.Format.Disbanded);
                    break;
                case TeamEventType.Updated:
                    eventType = Lang(LangKeys.Format.Updated);
                    break;
            }

            LogToConsole($"Team was {eventType}\n{_sb.ToString()}");

            DiscordSendMessage(Lang(LangKeys.Event.Team, null, eventType, _sb.ToString()), _configData.TeamsSettings.WebhookURL);
        }

        private void CacheOilRigsLocation()
        {
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (!monument.shouldDisplayOnMap)
                {
                    continue;
                }

                switch (monument.displayPhrase.english)
                {
                    case "Large Oil Rig":
                        _locationLargeOilRig = monument.transform.position;
                        break;
                    case "Oil Rig":
                        _locationOilRig = monument.transform.position;
                        break;
                }
            }
        }

        private string GetHackableLockedCratePosition(Vector3 position)
        {
            if (Vector3.Distance(position, _locationOilRig) < 51f)
            {
                return Lang(LangKeys.Format.OilRig);
            }

            if (Vector3.Distance(position, _locationLargeOilRig) < 51f)
            {
                return Lang(LangKeys.Format.LargeOilRig);
            }

            try
            {
                foreach (KeyValuePair<ulong, CargoShip> cargoShip in _cargoShips)
                {
                    if (!cargoShip.Value.IsValid() || cargoShip.Value.IsDestroyed)
                    {
                        _listBadCargoShips.Add(cargoShip.Key);
                        continue;
                    }

                    if (Vector3.Distance(position, cargoShip.Value.transform.position) < 85f)
                    {
                        return Lang(LangKeys.Format.CargoShip);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < _listBadCargoShips.Count; i++)
                {
                    _cargoShips.Remove(_listBadCargoShips[i]);
                }
                _listBadCargoShips.Clear();
            }

            return GetGridPosition(position);
        }

        #endregion Core Methods

        #region Helpers

        private void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnAdminHammerDisabled));
            Unsubscribe(nameof(OnAdminHammerEnabled));
            Unsubscribe(nameof(OnBetterChatMuted));
            Unsubscribe(nameof(OnBetterChatMuteExpired));
            Unsubscribe(nameof(OnBetterChatTimeMuted));
            Unsubscribe(nameof(OnBetterChatUnmuted));
            Unsubscribe(nameof(OnClanCreate));
            Unsubscribe(nameof(OnClanDisbanded));
            Unsubscribe(nameof(OnDangerousEventEnded));
            Unsubscribe(nameof(OnDangerousEventStarted));
            Unsubscribe(nameof(OnDeathNotice));
            Unsubscribe(nameof(OnDuelistDefeated));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnExplosiveDropped));
            Unsubscribe(nameof(OnExplosiveThrown));
            Unsubscribe(nameof(OnGodmodeToggled));
            Unsubscribe(nameof(OnGroupCreated));
            Unsubscribe(nameof(OnGroupDeleted));
            Unsubscribe(nameof(OnKitRedeemed));
            Unsubscribe(nameof(OnPlayerChat));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnPlayerRespawned));
            Unsubscribe(nameof(OnPlayerTeleported));
            Unsubscribe(nameof(OnPMProcessed));
            Unsubscribe(nameof(OnRadarActivated));
            Unsubscribe(nameof(OnRadarDeactivated));
            Unsubscribe(nameof(OnRaidableBaseCompleted));
            Unsubscribe(nameof(OnRaidableBaseEnded));
            Unsubscribe(nameof(OnRaidableBaseStarted));
            Unsubscribe(nameof(OnRconCommand));
            Unsubscribe(nameof(OnRconConnection));
            Unsubscribe(nameof(OnServerMessage));
            Unsubscribe(nameof(OnSupplyDropLanded));
            Unsubscribe(nameof(OnTeamAcceptInvite));
            Unsubscribe(nameof(OnTeamCreated));
            Unsubscribe(nameof(OnTeamDisbanded));
            Unsubscribe(nameof(OnTeamKick));
            Unsubscribe(nameof(OnTeamLeave));
            Unsubscribe(nameof(OnTeamPromote));
            Unsubscribe(nameof(OnTeamUpdated));
            Unsubscribe(nameof(OnTimedGroupAdded));
            Unsubscribe(nameof(OnTimedGroupExtended));
            Unsubscribe(nameof(OnTimedPermissionExtended));
            Unsubscribe(nameof(OnTimedPermissionGranted));
            Unsubscribe(nameof(OnUserBanned));
            Unsubscribe(nameof(OnUserGroupAdded));
            Unsubscribe(nameof(OnUserGroupRemoved));
            Unsubscribe(nameof(OnUserKicked));
            Unsubscribe(nameof(OnUserNameUpdated));
            Unsubscribe(nameof(OnUserPermissionGranted));
            Unsubscribe(nameof(OnUserPermissionRevoked));
            Unsubscribe(nameof(OnUserUnbanned));
            Unsubscribe(nameof(OnVanishDisappear));
            Unsubscribe(nameof(OnVanishReappear));
        }

        private void SubscribeHooks()
        {
            if (_configData.AdminHammerSettings.Enabled)
            {
                Subscribe(nameof(OnAdminHammerDisabled));
                Subscribe(nameof(OnAdminHammerEnabled));
            }

            if (_configData.UserMutedSettings.Enabled)
            {
                Subscribe(nameof(OnBetterChatMuted));
                Subscribe(nameof(OnBetterChatMuteExpired));
                Subscribe(nameof(OnBetterChatTimeMuted));
                Subscribe(nameof(OnBetterChatUnmuted));
            }

            if (_configData.ClanSettings.Enabled)
            {
                Subscribe(nameof(OnClanCreate));
                Subscribe(nameof(OnClanDisbanded));
            }

            if (_configData.DangerousTreasuresSettings.Enabled)
            {
                Subscribe(nameof(OnDangerousEventEnded));
                Subscribe(nameof(OnDangerousEventStarted));
            }

            if (_configData.PlayerDeathNotesSettings.Enabled)
            {
                Subscribe(nameof(OnDeathNotice));
            }

            if (_configData.DuelSettings.Enabled)
            {
                Subscribe(nameof(OnDuelistDefeated));
            }

            if (_configData.PlayerDeathSettings.Enabled)
            {
                Subscribe(nameof(OnEntityDeath));
            }

            if (_configData.EasterSettings.Enabled
             || _configData.HalloweenSettings.Enabled
             || _configData.LockedCrateSettings.Enabled)
            {
                Subscribe(nameof(OnEntityKill));
            }

            if (_configData.BradleySettings.Enabled
             || _configData.CargoPlaneSettings.Enabled
             || _configData.CargoShipSettings.Enabled
             || _configData.ChinookSettings.Enabled
             || _configData.ChristmasSettings.Enabled
             || _configData.EasterSettings.Enabled
             || _configData.HalloweenSettings.Enabled
             || _configData.HelicopterSettings.Enabled
             || _configData.LockedCrateSettings.Enabled
             || _configData.SantaSleighSettings.Enabled
             || _configData.SupplyDropSettings.Enabled)
            {
                Subscribe(nameof(OnEntitySpawned));
            }

            if (_configData.SupplyDropSettings.Enabled)
            {
                Subscribe(nameof(OnExplosiveDropped));
                Subscribe(nameof(OnExplosiveThrown));
                Subscribe(nameof(OnSupplyDropLanded));
            }

            if (_configData.GodmodeSettings.Enabled)
            {
                Subscribe(nameof(OnGodmodeToggled));
            }

            if (_configData.RustKitsSettings.Enabled)
            {
                Subscribe(nameof(OnKitRedeemed));
            }

            if (_configData.PermissionsSettings.Enabled)
            {
                Subscribe(nameof(OnGroupCreated));
                Subscribe(nameof(OnGroupDeleted));
                Subscribe(nameof(OnTimedGroupAdded));
                Subscribe(nameof(OnTimedGroupExtended));
                Subscribe(nameof(OnTimedPermissionExtended));
                Subscribe(nameof(OnTimedPermissionGranted));
                Subscribe(nameof(OnUserGroupAdded));
                Subscribe(nameof(OnUserGroupRemoved));
                Subscribe(nameof(OnUserPermissionGranted));
                Subscribe(nameof(OnUserPermissionRevoked));
            }

            if (_configData.PlayerConnectedSettings.Enabled
             || _configData.PlayerConnectedInfoSettings.Enabled)
            {
                Subscribe(nameof(OnPlayerConnected));
            }

            if (_configData.ChatSettings.Enabled
             || _configData.ChatTeamSettings.Enabled)
            {
                Subscribe(nameof(OnPlayerChat));
            }

            if (_configData.PlayerDisconnectedSettings.Enabled)
            {
                Subscribe(nameof(OnPlayerDisconnected));
            }

            if (_configData.PlayerRespawnedSettings.Enabled)
            {
                Subscribe(nameof(OnPlayerRespawned));
            }

            if (_configData.NTeleportationSettings.Enabled)
            {
                Subscribe(nameof(OnPlayerTeleported));
            }

            if (_configData.PrivateMessagesSettings.Enabled)
            {
                Subscribe(nameof(OnPMProcessed));
            }

            if (_configData.AdminRadarSettings.Enabled)
            {
                Subscribe(nameof(OnRadarActivated));
                Subscribe(nameof(OnRadarDeactivated));
            }

            if (_configData.RaidableBasesSettings.Enabled)
            {
                Subscribe(nameof(OnRaidableBaseCompleted));
                Subscribe(nameof(OnRaidableBaseEnded));
                Subscribe(nameof(OnRaidableBaseStarted));
            }

            if (_configData.RconCommandSettings.Enabled)
            {
                Subscribe(nameof(OnRconCommand));
            }

            if (_configData.RconConnectionSettings.Enabled)
            {
                Subscribe(nameof(OnRconConnection));
            }

            if (_configData.ServerMessagesSettings.Enabled)
            {
                Subscribe(nameof(OnServerMessage));
            }

            if (_configData.UserBannedSettings.Enabled)
            {
                Subscribe(nameof(OnUserBanned));
                Subscribe(nameof(OnUserUnbanned));
            }

            if (_configData.UserKickedSettings.Enabled)
            {
                Subscribe(nameof(OnUserKicked));
            }

            if (_configData.UserNameUpdateSettings.Enabled)
            {
                Subscribe(nameof(OnUserNameUpdated));
            }

            if (_configData.VanishSettings.Enabled)
            {
                Subscribe(nameof(OnVanishDisappear));
                Subscribe(nameof(OnVanishReappear));
            }

            if (_configData.ErrorSettings.Enabled)
            {
                Application.logMessageReceivedThreaded += HandleLog;
            }

            if (_configData.TeamsSettings.Enabled)
            {
                Subscribe(nameof(OnTeamAcceptInvite));
                Subscribe(nameof(OnTeamCreated));
                Subscribe(nameof(OnTeamDisbanded));
                Subscribe(nameof(OnTeamKick));
                Subscribe(nameof(OnTeamLeave));
                Subscribe(nameof(OnTeamPromote));
                Subscribe(nameof(OnTeamUpdated));
            }
        }

        private string StripRustTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            foreach (string tag in _tags)
            {
                text = text.Replace(tag, _configData.GlobalSettings.TagsReplacement);
            }

            foreach (Regex regexTag in _regexTags)
            {
                text = regexTag.Replace(text, _configData.GlobalSettings.TagsReplacement);
            }

            return text;
        }

        private string GetWebhookURL(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return _configData.GlobalSettings.DefaultWebhookURL;
            }

            return url;
        }

        private string GetGridPosition(Vector3 position) => PhoneController.PositionToGridCoord(position);

        private string GetFormattedDurationTime(TimeSpan time, string id = null)
        {
            _sb.Clear();

            if (time.Days > 0)
            {
                BuildTime(_sb, time.Days == 1 ? LangKeys.Format.Day : LangKeys.Format.Days, id, time.Days);
            }

            if (time.Hours > 0)
            {
                BuildTime(_sb, time.Hours == 1 ? LangKeys.Format.Hour : LangKeys.Format.Hours, id, time.Hours);
            }

            if (time.Minutes > 0)
            {
                BuildTime(_sb, time.Minutes == 1 ? LangKeys.Format.Minute : LangKeys.Format.Minutes, id, time.Minutes);
            }

            BuildTime(_sb, time.Seconds == 1 ? LangKeys.Format.Second : LangKeys.Format.Seconds, id, time.Seconds);

            return _sb.ToString();
        }

        private void BuildTime(StringBuilder sb, string lang, string playerID, int value)
        {
            sb.Append(_configData.GlobalSettings.TagsReplacement);
            sb.Append(value);
            sb.Append(_configData.GlobalSettings.TagsReplacement);
            sb.Append(" ");
            sb.Append(Lang(lang, playerID));
            sb.Append(" ");
        }

        private bool IsPluginLoaded(Plugin plugin) => plugin != null && plugin.IsLoaded;

        private void LogToConsole(string text)
        {
            if (_configData.GlobalSettings.LoggingEnabled)
            {
                Puts(text);
            }
        }

        #endregion Helpers

        #region Discord Embed

        #region Send Embed Methods
        /// <summary>
        /// Headers when sending an embeded message
        /// </summary>
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>()
        {
            {"Content-Type", "application/json"}
        };

        /// <summary>
        /// Sends the DiscordMessage to the specified webhook url
        /// </summary>
        /// <param name="url">Webhook url</param>
        /// <param name="message">Message being sent</param>
        private void DiscordSendMessage(string url, DiscordMessage message)
        {
            webrequest.Enqueue(url, message.ToJson(), DiscordSendMessageCallback, this, RequestMethod.POST, _headers);
        }

        /// <summary>
        /// Callback when sending the embed if any errors occured
        /// </summary>
        /// <param name="code">HTTP response code</param>
        /// <param name="message">Response message</param>
        private void DiscordSendMessageCallback(int code, string message)
        {
            switch (code)
            {
                case 204:
                    _retryCount = 0;
                    QueueCooldownDisable();
                    return;
                case 401:
                    Dictionary<string, object> objectJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                    int messageCode = 0;
                    if (objectJson["code"] != null && int.TryParse(objectJson["code"].ToString(), out messageCode))
                    {
                        if (messageCode == 50027)
                        {
                            PrintError($"Invalid Webhook Token: '{_queuedMessage.WebhookUrl}'");
                            return;
                        }
                    }
                    break;
                case 404:
                    PrintError($"Invalid Webhook (404: Not Found): '{_queuedMessage.WebhookUrl}'");
                    return;
                case 405:
                    PrintError($"Invalid Webhook (405: Method Not Allowed): '{_queuedMessage.WebhookUrl}'");
                    return;
                case 429:
                    message = "You are being rate limited. To avoid this try to increase queue interval in your config file.";
                    break;
                case 500:
                    message = "There are some issues with Discord server (500 Internal Server Error)";
                    break;
                case 502:
                    message = "There are some issues with Discord server (502 Bad Gateway)";
                    break;
                default:
                    message = $"DiscordSendMessageCallback: code = {code} message = {message}";
                    break;
            }

            _retryCount++;
            PrintError(message);
        }
        #endregion Send Embed Methods

        #region Embed Classes

        private class DiscordMessage
        {
            /// <summary>
            /// String only content to be sent
            /// </summary>
            [JsonProperty("content")]
            private string Content { get; set; }

            public DiscordMessage(string content)
            {
                Content = content;
            }

            /// <summary>
            /// Adds string content to the message
            /// </summary>
            /// <param name="content"></param>
            /// <returns></returns>
            public DiscordMessage AddContent(string content)
            {
                Content = content;
                return this;
            }

            /// <summary>
            /// Returns string content of the message
            /// </summary>
            /// <param name="content"></param>
            /// <returns></returns>
            public string GetContent()
            {
                return Content;
            }

            /// <summary>
            /// Returns message as JSON to be sent in the web request
            /// </summary>
            /// <returns></returns>
            public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }
        #endregion Embed Classes

        #endregion Discord Embed
    }
}
