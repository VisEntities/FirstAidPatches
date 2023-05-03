using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Collections;

using UnityEngine;

using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("GUIAnnouncements", "JoeSheep", "2.0.4", ResourceId = 1222)]
    [Description("Creates announcements with custom messages across the top of player's screens.")]

    class GUIAnnouncements : RustPlugin
    {
        #region Configuration

        #region Permissions
        const string PermAnnounce = "GUIAnnouncements.announce";
        const string PermAnnounceToggle = "GUIAnnouncements.toggle";
        const string PermAnnounceGetNextRestart = "GUIAnnouncements.getnextrestart";
        const string PermAnnounceJoinLeave = "GUIAnnouncements.announcejoinleave";
        #endregion

        #region Global Declerations
        private Dictionary<ulong, string> Exclusions = new Dictionary<ulong, string>();
        private HashSet<ulong> JustJoined = new HashSet<ulong>();
        private HashSet<ulong> GlobalTimerPlayerList = new HashSet<ulong>();
        private Dictionary<BasePlayer, Timer> PrivateTimers = new Dictionary<BasePlayer, Timer>();
        private Dictionary<BasePlayer, Timer> NewPlayerPrivateTimers = new Dictionary<BasePlayer, Timer>();
        private Dictionary<BasePlayer, Timer> PlayerRespawnedTimers = new Dictionary<BasePlayer, Timer>();
        private Timer PlayerTimer;
        private Timer GlobalTimer;
        private Timer NewPlayerTimer;
        private Timer PlayerRespawnedTimer;
        private Timer RealTimeTimer;
        private bool RealTimeTimerStarted = false;
        private Timer SixtySecondsTimer;
        private Timer AutomaticAnnouncementsTimer;
        private Timer GetNextRestartTimer;
        private string HeliLastHitPlayer = String.Empty;
        private string CH47LastHitPlayer = String.Empty;
        private string APCLastHitPlayer = String.Empty;
        private HashSet<ulong> HeliNetIDs = new HashSet<ulong>();
        private HashSet<ulong> CH47NetIDs = new HashSet<ulong>();
        private HashSet<ulong> KilledCH47NetIDs = new HashSet<ulong>();
        private bool ConfigUpdated;
        private static readonly int WorldSize = ConVar.Server.worldsize;
        private List<DateTime> RestartTimes = new List<DateTime>();
        private Dictionary<DateTime, TimeSpan> CalcNextRestartDict = new Dictionary<DateTime, TimeSpan>();
        private DateTime NextRestart;
        List<TimeSpan> RestartAnnouncementsTimeSpans = new List<TimeSpan>();
        private int LastHour;
        private int LastMinute;
        private bool RestartCountdown;
        private bool RestartJustScheduled = false;
        private bool RestartScheduled = false;
        private string RestartReason = String.Empty;
        private List<string> RestartAnnouncementsWhenStrings;
        private DateTime ScheduledRestart;
        private TimeSpan AutomaticTimedAnnouncementsRepeatTimeSpan;
        private bool RestartSuspended = false;
        private bool DontCheckNextRestart = false;
        private bool MutingBans = false;
        private bool AnnouncingGameTime = false;
        private string LastGameTime;
        private int GameTimeCurrentCount = 0;
        private bool AnnouncingRealTime = false;
        private string LastRealTime;
        private int RealTimeCurrentCount = 0;
        private List<string> GameTimes = new List<string>();
        private List<string> RealTimes = new List<string>();
        private IEnumerator<List<string>> ATALEnum;
        private List<List<string>> AutomaticTimedAnnouncementsStringList = new List<List<string>>();

        string BannerTintGrey = "0.1 0.1 0.1 0.7";
        string BannerTintRed = "0.5 0.1 0.1 0.7";
        string BannerTintOrange = "0.95294 0.37255 0.06275 0.7";
        string BannerTintYellow = "1 0.92 0.016 0.7";
        string BannerTintGreen = "0.1 0.4 0.1 0.5";
        string BannerTintCyan = "0 1 1 0.7";
        string BannerTintBlue = "0.09020 0.07843 0.71765 0.7";
        string BannerTintPurple = "0.53333 0.07843 0.77647 0.7";
        string TextRed = "0.5 0.2 0.2";
        string TextOrange = "0.8 0.5 0.1";
        string TextYellow = "1 0.92 0.016";
        string TextGreen = "0 1 0";
        string TextCyan = "0 1 1";
        string TextBlue = "0.09020 0.07843 0.71765";
        string TextPurple = "0.53333 0.07843 0.77647";
        string TextWhite = "1 1 1";
        string BannerAnchorMaxX = "1.026 ";
        string BannerAnchorMaxYDefault = "0.9743";
        string BannerAnchorMaxY = "0.9743";
        string BannerAnchorMinX = "-0.027 ";
        string BannerAnchorMinYDefault = "0.915";
        string BannerAnchorMinY = "0.915";
        string TextAnchorMaxX = "0.868 ";
        string TextAnchorMaxYDefault = "0.9743";
        string TextAnchorMaxY = "0.9743";
        string TextAnchorMinX = "0.131 ";
        string TextAnchorMinYDefault = "0.915";
        string TextAnchorMinY = "0.915";
        string TABannerAnchorMaxY = "0.9743";
        string TABannerAnchorMinY = "0.915";
        string TATextAnchorMaxY = "0.9743";
        string TATextAnchorMinY = "0.915";

        #endregion
        //============================================================================================================
        #region Config Option Declerations

        //Color List
        public string BannerColorList { get; private set; } = "Grey, Red, Orange, Yellow, Green, Cyan, Blue, Purple";
        public string TextColorList { get; private set; } = "White, Red, Orange, Yellow, Green, Cyan, Blue, Purple";

        //Airdrop Announcements
        public bool AirdropAnnouncementsEnabled { get; private set; } = false;
        public bool AirdropAnnouncementsLocation { get; private set; } = false;
        public string AirdropAnnouncementsText { get; private set; } = "Airdrop en route!";
        public string AirdropAnnouncementsTextWithGrid { get; private set; } = "Airdrop en route to {grid}.";
        public string AirdropAnnouncementsBannerColor { get; private set; } = "Green";
        public string AirdropAnnouncementsTextColor { get; private set; } = "Yellow";

        //Automatic Game Time Announcements
        public bool AutomaticGameTimeAnnouncementsEnabled { get; private set; } = false;
        public Dictionary<string, List<object>> AutomaticGameTimeAnnouncementsList { get; private set; } = new Dictionary<string, List<object>>
        {
            {"18:15", new List<object>{ "The in game time is 18:15 announcement 1.", "The in game time is 18:15 announcement 2.", "The in game time is 18:15 announcement 3." } },
            {"00:00", new List<object>{ "The in game time is 00:00 announcement 1.", "The in game time is 00:00 announcement 2.", "The in game time is 00:00 announcement 3." } },
            {"12:00", new List<object>{ "The in game time is 12:00 announcement 1.", "The in game time is 12:00 announcement 2.", "The in game time is 12:00 announcement 3." } },
        };
        public string AutomaticGameTimeAnnouncementsBannerColor { get; private set; } = "Grey";
        public string AutomaticGameTimeAnnouncementsTextColor { get; private set; } = "White";

        //Automatic Timed Announcements
        public bool AutomaticTimedAnnouncementsEnabled { get; private set; } = false;
        public static List<object> AutomaticTimedAnnouncementsList { get; private set; } = new List<object>
        {
            new List<object>{ "1st Automatic Timed Announcement 1", "1st Automatic Timed Announcement 2" },
            new List<object>{ "2nd Automatic Timed Announcement 1", "2nd Automatic Timed Announcement 2" },
            new List<object>{ "3rd Automatic Timed Announcement 1", "3rd Automatic Timed Announcement 2" },
        };
        public string AutomaticTimedAnnouncementsRepeat { get; private set; } = "00:30:00";
        public string AutomaticTimedAnnouncementsBannerColor { get; private set; } = "Grey";
        public string AutomaticTimedAnnouncementsTextColor { get; private set; } = "White";

        //Automatic Realtime Announcements
        public bool AutomaticRealtimeAnnouncementsEnabled { get; private set; } = false;
        public Dictionary<string, List<object>> AutomaticRealTimeAnnouncementsList { get; private set; } = new Dictionary<string, List<object>>
        {
            {"18:15", new List<object>{ "The local time is 18:15 announcement 1.", "The local time is 18:15 announcement 2.", "The local time is 18:15 announcement 3." } },
            {"00:00", new List<object>{ "The local time is 00:00 announcement 1.", "The local time is 00:00 announcement 2.", "The local time is 00:00 announcement 3." } },
            {"12:00", new List<object>{ "The local time is 12:00 announcement 1.", "The local time is 12:00 announcement 2.", "The local time is 12:00 announcement 3." } },
        };
        public string AutomaticRealTimeAnnouncementsBannerColor { get; private set; } = "Grey";
        public string AutomaticRealTimeAnnouncementsTextColor { get; private set; } = "White";

        //Christmas Stocking Refill Announcement
        public bool StockingRefillAnnouncementsEnabled { get; private set; } = false;
        public string StockingRefillAnnouncementText { get; private set; } = "Santa has refilled your stockings. Go check what you've got!";
        public string StockingRefillAnnouncementBannerColor { get; private set; } = "Green";
        public string StockingRefillAnnouncementTextColor { get; private set; } = "Red";

        //General Settings
        public float AnnouncementDuration { get; private set; } = 10f;
        public int FontSize { get; private set; } = 18;
        public float FadeOutTime { get; private set; } = 0.5f;
        public float FadeInTime { get; private set; } = 0.5f;
        public static float AdjustVPosition { get; private set; } = 0.0f;

        //Global Join/Leave Announcements
        public bool GlobalLeaveAnnouncementsEnabled { get; private set; } = false;
        public bool GlobalJoinAnnouncementsEnabled { get; private set; } = false;
        public bool GlobalJoinLeavePermissionOnly { get; private set; } = true;
        public string GlobalLeaveText { get; private set; } = "{rank} {playername} has left.";
        public string GlobalJoinText { get; private set; } = "{rank} {playername} has joined.";
        public string GlobalLeaveAnnouncementBannerColor { get; private set; } = "Grey";
        public string GlobalLeaveAnnouncementTextColor { get; private set; } = "White";
        public string GlobalJoinAnnouncementBannerColor { get; private set; } = "Grey";
        public string GlobalJoinAnnouncementTextColor { get; private set; } = "White";

        //Helicopter Announcements
        public bool HelicopterSpawnAnnouncementEnabled { get; private set; } = false;
        public bool HelicopterDespawnAnnouncementEnabled { get; private set; } = false;
        public bool HelicopterDestroyedAnnouncementEnabled { get; private set; } = false;
        public bool HelicopterDestroyedAnnouncementWithDestroyer { get; private set; } = false;
        public string HelicopterSpawnAnnouncementText { get; private set; } = "Patrol Helicopter Inbound!";
        public string HelicopterDespawnAnnouncementText { get; private set; } = "The patrol helicopter has left.";
        public string HelicopterDestroyedAnnouncementText { get; private set; } = "The patrol helicopter has been taken down!";
        public string HelicopterDestroyedAnnouncementWithDestroyerText { get; private set; } = "{playername} got the last shot on the helicopter taking it down!";
        public string HelicopterSpawnAnnouncementBannerColor { get; private set; } = "Red";
        public string HelicopterSpawnAnnouncementTextColor { get; private set; } = "Orange";
        public string HelicopterDestroyedAnnouncementBannerColor { get; private set; } = "Red";
        public string HelicopterDestroyedAnnouncementTextColor { get; private set; } = "White";
        public string HelicopterDespawnAnnouncementBannerColor { get; private set; } = "Red";
        public string HelicopterDespawnAnnouncementTextColor { get; private set; } = "White";

        //Chinook Announcements
        public bool CH47SpawnAnnouncementsEnabled { get; private set; } = false;
        public bool CH47DespawnAnnouncementsEnabled { get; private set; } = false;
        public bool CH47DestroyedAnnouncementsEnabled { get; private set; } = false;
        public bool CH47DestroyedAnnouncementsWithDestroyer { get; private set; } = false;
        public bool CH47CrateDroppedAnnouncementsEnabled { get; private set; } = false;
        public bool CH47CrateDroppedAnnouncementsWithLocation { get; private set; } = false;
        public string CH47SpawnAnnouncementText { get; private set; } = "Chinook inbound!";
        public string CH47DespawnAnnouncementText { get; private set; } = "The Chinook has left.";
        public string CH47DestroyedAnnouncementText { get; private set; } = "The Chinook has been taken down!";
        public string CH47DestroyedAnnouncementWithDestroyerText { get; private set; } = "{playername} got the last shot on the Chinook taking it down!";
        public string CH47CrateDroppedAnnouncementText { get; private set; } = "The Chinook has dropped a crate!";
        public string CH47CrateDroppedAnnouncementTextWithGrid { get; private set; } = "The Chinook has dropped a crate in {grid}.";
        public string CH47SpawnAnnouncementBannerColor { get; private set; } = "Red";
        public string CH47SpawnAnnouncementTextColor { get; private set; } = "Yellow";
        public string CH47DestroyedAnnouncementBannerColor { get; private set; } = "Red";
        public string CH47DestroyedAnnouncementTextColor { get; private set; } = "White";
        public string CH47DespawnAnnouncementBannerColor { get; private set; } = "Red";
        public string CH47DespawnAnnouncementTextColor { get; private set; } = "White";
        public string CH47CrateDroppedAnnouncementBannerColor { get; private set; } = "Red";
        public string CH47CrateDroppedAnnouncementTextColor { get; private set; } = "Yellow";

        //Bradley APC Announcements
        public bool APCSpawnAnnouncementsEnabled { get; private set; } = false;
        public bool APCDestroyedAnnouncementsEnabled { get; private set; } = false;
        public bool APCDestroyedAnnouncementsWithDestroyer { get; private set; } = false;
        public string APCSpawnAnnouncementText { get; private set; } = "An APC is patrolling the launch site!";
        public string APCDestroyedAnnouncementText { get; private set; } = "The APC has been destroyed.";
        public string APCDestroyedAnnouncementWithDestroyerText { get; private set; } = "{playername} got the last shot on the APC destroying it!";
        public string APCSpawnAnnouncementBannerColor { get; private set; } = "Red";
        public string APCSpawnAnnouncementTextColor { get; private set; } = "Yellow";
        public string APCDestroyedAnnouncementBannerColor { get; private set; } = "Red";
        public string APCDestroyedAnnouncementTextColor { get; private set; } = "White";

        //Cargoship Announcements
        public bool CargoshipSpawnAnnouncementsEnabled { get; private set; } = false;
        public bool CargoshipEgressAnnouncementsEnabled { get; private set; } = false;
        public string CargoshipSpawnAnnouncementText { get; private set; } = "Cargoship ahoy!";
        public string CargoshipEgressAnnouncementText { get; private set; } = "The cargoship is departing.";
        public string CargoshipSpawnAnnouncementBannerColor { get; private set; } = "Blue";
        public string CargoshipSpawnAnnouncementTextColor { get; private set; } = "Yellow";
        public string CargoshipEgressAnnouncementBannerColor { get; private set; } = "Blue";
        public string CargoshipEgressAnnouncementTextColor { get; private set; } = "White";

        //Crate Hack Announcements
        public bool CrateHackAnnouncementsEnabled { get; private set; } = false;
        public bool CrateHackSpecifyOilRig { get; private set; } = false;
        public string CrateHackAnnouncementText { get; private set; } = "An oil rig crate is being hacked!";
        public string CrateHackAnnouncementSmallOilRigText { get; private set; } = "The small oil rig crate is being hacked!";
        public string CrateHackAnnouncementLargeOilRigText { get; private set; } = "The large oil rig crate is being hacked!";
        public string CrateHackAnnouncementBannerColor { get; private set; } = "Orange";
        public string CrateHackAnnouncementTextColor { get; private set; } = "Yellow";

        //New Player Announcements
        public bool NewPlayerAnnouncementsEnabled { get; private set; } = false;
        public string NewPlayerAnnouncementsBannerColor { get; private set; } = "Grey";
        public string NewPlayerAnnouncementsTextColor { get; private set; } = "White";
        public Dictionary<int, List<object>> NewPlayerAnnouncementsList { get; private set; } = new Dictionary<int, List<object>>
        {
            {1, new List<object>{ "1st Join {rank} {playername} New player announcement 1.", "1st Join {rank} {playername} New player announcement 2.", "1st Join {rank} {playername} New player announcement 3." } },
            {2, new List<object>{ "2nd Join {rank} {playername} New player announcement 1.", "2nd Join {rank} {playername} New player announcement 2.", "2nd Join {rank} {playername} New player announcement 3." } },
            {3, new List<object>{ "3rd Join {rank} {playername} New player announcement 1.", "3rd Join {rank} {playername} New player announcement 2.", "3rd Join {rank} {playername} New player announcement 3." } },
        };

        //Player Banned Announcement
        public bool PlayerBannedAnnouncementsEnabled { get; private set; } = false;
        public string PlayerBannedAnnouncmentText { get; private set; } = "{playername} has been banned. {reason}.";
        public string PlayerBannedAnnouncementBannerColor { get; private set; } = "Grey";
        public string PlayerBannedAnnouncementTextColor { get; private set; } = "Red";

        //Respawn Announcements
        public bool RespawnAnnouncementsEnabled { get; private set; } = false;
        public string RespawnAnnouncementsBannerColor { get; private set; } = "Grey";
        public string RespawnAnnouncementsTextColor { get; private set; } = "White";
        public List<object> RespawnAnnouncementsList { get; private set; } = new List<object>
        {
                    "{playername} Respawn announcement 1.",
                    "{playername} Respawn announcement 2.",
                    "{playername} Respawn announcement 3."
        };

        //Restart Announcements
        public bool RestartAnnouncementsEnabled { get; private set; } = false;
        public string RestartAnnouncementsFormat { get; private set; } = "Restarting in {time}";
        public string RestartAnnouncementsBannerColor { get; private set; } = "Grey";
        public string RestartAnnouncementsTextColor { get; private set; } = "White";
        public List<object> RestartTimesList { get; private set; } = new List<object>
        {
            "08:00:00",
            "20:00:00"
        };
        public List<object> RestartAnnouncementsTimes { get; private set; } = new List<object>
        {
            "12:00:00",
            "11:00:00",
            "10:00:00",
            "09:00:00",
            "08:00:00",
            "07:00:00",
            "06:00:00",
            "05:00:00",
            "04:00:00",
            "03:00:00",
            "02:00:00",
            "01:00:00",
            "00:45:00",
            "00:30:00",
            "00:15:00",
            "00:05:00"
        };
        public bool RestartServer { get; private set; } = false;
        public string RestartSuspendedAnnouncement { get; private set; } = "The restart in {time} has been suspended.";
        public string RestartCancelledAnnouncement { get; private set; } = "The restart in {time} has been cancelled.";

        //Test Announcement
        public float TestAnnouncementDuration { get; private set; } = 10f;
        public int TestAnnouncementFontSize { get; private set; } = 18;
        public float TestAnnouncementFadeOutTime { get; private set; } = 0.5f;
        public float TestAnnouncementFadeInTime { get; private set; } = 0.5f;
        public static float TestAnnouncementAdjustVPosition { get; private set; } = 0.0f;
        public string TestAnnouncementBannerColor { get; private set; } = "Grey";
        public string TestAnnouncementsTextColor { get; private set; } = "White";

        //Third Party Plugin Support
        public bool DoNotOverlayLustyMap { get; private set; } = false;
        public string LustyMapPosition { get; private set; } = "Left";

        //Welcome Announcement
        public bool WelcomeAnnouncementsEnabled { get; private set; } = false;
        public string WelcomeAnnouncementText { get; private set; } = "Welcome {playername}! There are {playercount} player(s) online.";
        public string WelcomeBackAnnouncementText { get; private set; } = "Welcome back {playername}! There are {playercount} player(s) online.";
        public string WelcomeAnnouncementBannerColor { get; private set; } = "Grey";
        public string WelcomeAnnouncementTextColor { get; private set; } = "White";
        public float WelcomeAnnouncementDuration { get; private set; } = 20f;
        public float WelcomeAnnouncementDelay { get; private set; } = 0f;
        public bool WelcomeBackAnnouncement { get; private set; } = false;
        #endregion

        //============================================================================================================
        #region LoadConfig
        private void LoadGUIAnnouncementsConfig()
        {
            string BColor; string TColor; string Category; string Setting;

            //Color List
            BannerColorList = GetConfig("A List Of Available Colors To Use (DO NOT CHANGE)", "Banner Colors", BannerColorList);
            if (BannerColorList != "Grey, Red, Orange, Yellow, Green, Cyan, Blue, Purple")
            {
                PrintWarning("Banner color list changed. Reverting changes.");
                Config["A List Of Available Colors To Use(DO NOT CHANGE)", "Banner Colors"] = "Grey, Red, Orange, Yellow, Green, Cyan, Blue, Purple";
                ConfigUpdated = true;
            }
            TextColorList = GetConfig("A List Of Available Colors To Use (DO NOT CHANGE)", "Text Colors", TextColorList);
            if (TextColorList != "White, Red, Orange, Yellow, Green, Cyan, Blue, Purple")
            {
                PrintWarning("Text color list changed. Reverting changes.");
                Config["A List Of Available Colors To Use(DO NOT CHANGE)", "Text Colors"] = "White, Red, Orange, Yellow, Green, Cyan, Blue, Purple";
                ConfigUpdated = true;
            }

            //Airdrop Announcements
            AirdropAnnouncementsEnabled = GetConfig("Public Airdrop Announcements", "Enabled", AirdropAnnouncementsEnabled);
            AirdropAnnouncementsText = GetConfig("Public Airdrop Announcements", "Text", AirdropAnnouncementsText);
            AirdropAnnouncementsTextWithGrid = GetConfig("Public Airdrop Announcements", "Text With Grid", AirdropAnnouncementsTextWithGrid);
            AirdropAnnouncementsLocation = GetConfig("Public Airdrop Announcements", "Show Location", AirdropAnnouncementsLocation);

            BColor = AirdropAnnouncementsBannerColor; Category = "Public Airdrop Announcements"; Setting = "Banner Color";
            AirdropAnnouncementsBannerColor = GetConfig(Category, Setting, AirdropAnnouncementsBannerColor);
            CheckBannerColor(AirdropAnnouncementsBannerColor, Category, Setting, BColor);

            TColor = AirdropAnnouncementsTextColor; Category = "Public Airdrop Announcements"; Setting = "Text Color";
            AirdropAnnouncementsTextColor = GetConfig(Category, Setting, AirdropAnnouncementsTextColor);
            CheckTextColor(AirdropAnnouncementsTextColor, Category, Setting, TColor);

            //Automatic Game Time Announcements
            AutomaticGameTimeAnnouncementsEnabled = GetConfig("Public Automatic Game Time Announcements", "Enabled", AutomaticGameTimeAnnouncementsEnabled);
            AutomaticGameTimeAnnouncementsList = GetConfig("Public Automatic Game Time Announcements", "Announcement List (Show at this in game time : Announcements to show)", AutomaticGameTimeAnnouncementsList);

            BColor = AutomaticGameTimeAnnouncementsBannerColor; Category = "Public Automatic Game Time Announcements"; Setting = "Banner Color";
            AutomaticGameTimeAnnouncementsBannerColor = GetConfig(Category, Setting, AutomaticGameTimeAnnouncementsBannerColor);
            CheckBannerColor(AutomaticGameTimeAnnouncementsBannerColor, Category, Setting, BColor);

            TColor = AutomaticGameTimeAnnouncementsTextColor; Category = "Public Automatic Game Time Announcements"; Setting = "Text Color";
            AutomaticGameTimeAnnouncementsTextColor = GetConfig(Category, Setting, AutomaticGameTimeAnnouncementsTextColor);
            CheckTextColor(AutomaticGameTimeAnnouncementsTextColor, Category, Setting, TColor);

            //Automatic Timed Announcements
            AutomaticTimedAnnouncementsEnabled = GetConfig("Public Automatic Timed Announcements", "Enabled", AutomaticTimedAnnouncementsEnabled);
            AutomaticTimedAnnouncementsList = GetConfig("Public Automatic Timed Announcements", "Announcement List", AutomaticTimedAnnouncementsList);
            AutomaticTimedAnnouncementsRepeat = GetConfig("Public Automatic Timed Announcements", "Show Every (HH:MM:SS)", AutomaticTimedAnnouncementsRepeat);
            if (!TimeSpan.TryParse(AutomaticTimedAnnouncementsRepeat, out AutomaticTimedAnnouncementsRepeatTimeSpan))
            {
                PrintWarning("Config: \"Automatic Timed Announcements - Show Every (HH:MM:SS)\" is not of the correct format HH:MM:SS, or has numbers out of range and should not be higher than 23:59:59. Resetting to default.");
                Config["Public Automatic Timed Announcements", "Show Every (HH:MM:SS)"] = "00:30:00";
                ConfigUpdated = true;
            }

            BColor = AutomaticTimedAnnouncementsBannerColor; Category = "Public Automatic Timed Announcements"; Setting = "Banner Color";
            AutomaticTimedAnnouncementsBannerColor = GetConfig(Category, Setting, AutomaticTimedAnnouncementsBannerColor);
            CheckBannerColor(AutomaticTimedAnnouncementsBannerColor, Category, Setting, BColor);

            TColor = AutomaticTimedAnnouncementsTextColor; Category = "Public Automatic Timed Announcements"; Setting = "Text Color";
            AutomaticTimedAnnouncementsTextColor = GetConfig(Category, Setting, AutomaticTimedAnnouncementsTextColor);
            CheckTextColor(AutomaticTimedAnnouncementsTextColor, Category, Setting, TColor);

            //Automatic Realtime Announcements
            AutomaticRealtimeAnnouncementsEnabled = GetConfig("Public Automatic Real Time Announcements", "Enabled", AutomaticRealtimeAnnouncementsEnabled);
            AutomaticRealTimeAnnouncementsList = GetConfig("Public Automatic Real Time Announcements", "Announcement List (Show at this local time : Announcements to show)", AutomaticRealTimeAnnouncementsList);

            BColor = AutomaticRealTimeAnnouncementsBannerColor; Category = "Public Automatic Real Time Announcements"; Setting = "Banner Color";
            AutomaticRealTimeAnnouncementsBannerColor = GetConfig(Category, Setting, AutomaticRealTimeAnnouncementsBannerColor);
            CheckBannerColor(AutomaticRealTimeAnnouncementsBannerColor, Category, Setting, BColor);

            TColor = AutomaticRealTimeAnnouncementsTextColor; Category = "Public Automatic Real Time Announcements"; Setting = "Text Color";
            AutomaticRealTimeAnnouncementsTextColor = GetConfig(Category, Setting, AutomaticRealTimeAnnouncementsTextColor);
            CheckTextColor(AutomaticRealTimeAnnouncementsTextColor, Category, Setting, TColor);

            //Christmas Stocking Refill Announcement
            StockingRefillAnnouncementsEnabled = GetConfig("Public Christmas Stocking Refill Announcement", "Enabled", StockingRefillAnnouncementsEnabled);
            StockingRefillAnnouncementText = GetConfig("Public Christmas Stocking Refill Announcement", "Text", StockingRefillAnnouncementText);

            BColor = StockingRefillAnnouncementBannerColor; Category = "Public Christmas Stocking Refill Announcement"; Setting = "Banner Color";
            StockingRefillAnnouncementBannerColor = GetConfig(Category, Setting, StockingRefillAnnouncementBannerColor);
            CheckBannerColor(StockingRefillAnnouncementBannerColor, Category, Setting, BColor);

            TColor = StockingRefillAnnouncementTextColor; Category = "Public Christmas Stocking Refill Announcement"; Setting = "Text Color";
            StockingRefillAnnouncementTextColor = GetConfig(Category, Setting, StockingRefillAnnouncementTextColor);
            CheckTextColor(StockingRefillAnnouncementTextColor, Category, Setting, TColor);

            //General Settings
            AnnouncementDuration = GetConfig("General Settings", "Announcement Duration", AnnouncementDuration);
            if (AnnouncementDuration == 0)
            {
                PrintWarning("Config: \"General Settings - Announcement Duration\" set to 0, resetting to 10f.");
                Config["General Settings", "Announcement Duration"] = 10f;
                ConfigUpdated = true;
            }
            FontSize = GetConfig("General Settings", "Font Size", FontSize);
            if (FontSize > 33 | FontSize == 0)
            {
                PrintWarning("Config: \"General Settings - Font Size\" greater than 28 or 0, resetting to 18.");
                Config["General Settings", "Font Size"] = 18;
                ConfigUpdated = true;
            }
            FadeInTime = GetConfig("General Settings", "Fade In Time", FadeInTime);
            if (FadeInTime > AnnouncementDuration / 2)
            {
                PrintWarning("Config: \"General Settings - Fade In Time\" is greater than half of AnnouncementShowDuration, resetting to half of AnnouncementShowDuration.");
                Config["General Settings", "Fade In Time"] = AnnouncementDuration / 2;
                ConfigUpdated = true;
            }
            FadeOutTime = GetConfig("General Settings", "Fade Out Time", FadeOutTime);
            if (FadeOutTime > AnnouncementDuration / 2)
            {
                PrintWarning("Config: \"General Settings - Fade Out Time\" is greater than half of AnnouncementShowDuration, resetting to half of AnnouncementShowDuration.");
                Config["General Settings", "Fade Out Time"] = AnnouncementDuration / 2;
                ConfigUpdated = true;
            }
            AdjustVPosition = GetConfig("General Settings", "Adjust Vertical Position", AdjustVPosition);
            if (AdjustVPosition != 0f)
            {
                BannerAnchorMaxY = (float.Parse(BannerAnchorMaxYDefault) + AdjustVPosition).ToString();
                BannerAnchorMinY = (float.Parse(BannerAnchorMinYDefault) + AdjustVPosition).ToString();
                TextAnchorMaxY = (float.Parse(TextAnchorMaxYDefault) + AdjustVPosition).ToString();
                TextAnchorMinY = (float.Parse(TextAnchorMinYDefault) + AdjustVPosition).ToString();
            }

            //Global Join/Leave Announcements
            GlobalLeaveAnnouncementsEnabled = GetConfig("Public Join/Leave Announcements", "Leave Enabled", GlobalLeaveAnnouncementsEnabled);
            GlobalLeaveText = GetConfig("Public Join/Leave Announcements", "Leave Text", GlobalLeaveText);
            GlobalJoinLeavePermissionOnly = GetConfig("Public Join/Leave Announcements", "Announce Only Players With Permission", GlobalJoinLeavePermissionOnly);

            BColor = GlobalLeaveAnnouncementBannerColor; Category = "Public Join/Leave Announcements"; Setting = "Leave Banner Color";
            GlobalLeaveAnnouncementBannerColor = GetConfig(Category, Setting, GlobalLeaveAnnouncementBannerColor);
            CheckBannerColor(GlobalLeaveAnnouncementBannerColor, Category, Setting, BColor);

            TColor = GlobalLeaveAnnouncementTextColor; Category = "Public Join/Leave Announcements"; Setting = "Leave Text Color";
            GlobalLeaveAnnouncementTextColor = GetConfig(Category, Setting, GlobalLeaveAnnouncementTextColor);
            CheckTextColor(GlobalLeaveAnnouncementTextColor, Category, Setting, TColor);

            GlobalJoinAnnouncementsEnabled = GetConfig("Public Join/Leave Announcements", "Join Enabled", GlobalJoinAnnouncementsEnabled);
            GlobalJoinText = GetConfig("Public Join/Leave Announcements", "Join Text", GlobalJoinText);

            BColor = GlobalJoinAnnouncementBannerColor; Category = "Public Join/Leave Announcements"; Setting = "Join Banner Color";
            GlobalJoinAnnouncementBannerColor = GetConfig(Category, Setting, GlobalJoinAnnouncementBannerColor);
            CheckBannerColor(GlobalJoinAnnouncementBannerColor, Category, Setting, BColor);

            TColor = GlobalJoinAnnouncementTextColor; Category = "Public Join/Leave Announcements"; Setting = "Join Text Color";
            GlobalJoinAnnouncementTextColor = GetConfig(Category, Setting, GlobalJoinAnnouncementTextColor);
            CheckTextColor(GlobalJoinAnnouncementTextColor, Category, Setting, TColor);

            //Helicopter Announcements
            HelicopterSpawnAnnouncementEnabled = GetConfig("Public Helicopter Announcements", "Spawn", HelicopterSpawnAnnouncementEnabled);
            HelicopterSpawnAnnouncementText = GetConfig("Public Helicopter Announcements", "Spawn Text", HelicopterSpawnAnnouncementText);
            HelicopterDespawnAnnouncementEnabled = GetConfig("Public Helicopter Announcements", "Despawn", HelicopterDespawnAnnouncementEnabled);
            HelicopterDespawnAnnouncementText = GetConfig("Public Helicopter Announcements", "Despawn Text", HelicopterDespawnAnnouncementText);
            HelicopterDestroyedAnnouncementEnabled = GetConfig("Public Helicopter Announcements", "Destroyed", HelicopterDestroyedAnnouncementEnabled);
            HelicopterDestroyedAnnouncementWithDestroyer = GetConfig("Public Helicopter Announcements", "Show Destroyer", HelicopterDestroyedAnnouncementWithDestroyer);
            HelicopterDestroyedAnnouncementText = GetConfig("Public Helicopter Announcements", "Destroyed Text", HelicopterDestroyedAnnouncementText);
            HelicopterDestroyedAnnouncementWithDestroyerText = GetConfig("Public Helicopter Announcements", "Destroyed Text With Destroyer", HelicopterDestroyedAnnouncementWithDestroyerText);

            BColor = HelicopterSpawnAnnouncementBannerColor; Category = "Public Helicopter Announcements"; Setting = "Spawn Banner Color";
            HelicopterSpawnAnnouncementBannerColor = GetConfig(Category, Setting, HelicopterSpawnAnnouncementBannerColor);
            CheckBannerColor(HelicopterSpawnAnnouncementBannerColor, Category, Setting, BColor);

            TColor = HelicopterSpawnAnnouncementTextColor; Category = "Public Helicopter Announcements"; Setting = "Spawn Text Color";
            HelicopterSpawnAnnouncementTextColor = GetConfig(Category, Setting, HelicopterSpawnAnnouncementTextColor);
            CheckTextColor(HelicopterSpawnAnnouncementTextColor, Category, Setting, TColor);

            BColor = HelicopterDespawnAnnouncementBannerColor; Category = "Public Helicopter Announcements"; Setting = "Despawn Banner Color";
            HelicopterDespawnAnnouncementBannerColor = GetConfig(Category, Setting, HelicopterDespawnAnnouncementBannerColor);
            CheckBannerColor(HelicopterDespawnAnnouncementBannerColor, Category, Setting, BColor);

            TColor = HelicopterDespawnAnnouncementTextColor; Category = "Public Helicopter Announcements"; Setting = "Despawn Text Color";
            HelicopterDespawnAnnouncementTextColor = GetConfig(Category, Setting, HelicopterDespawnAnnouncementTextColor);
            CheckTextColor(HelicopterDespawnAnnouncementTextColor, Category, Setting, TColor);

            BColor = HelicopterDestroyedAnnouncementBannerColor; Category = "Public Helicopter Announcements"; Setting = "Destroyed Banner Color";
            HelicopterDestroyedAnnouncementBannerColor = GetConfig(Category, Setting, HelicopterDestroyedAnnouncementBannerColor);
            CheckBannerColor(HelicopterDestroyedAnnouncementBannerColor, Category, Setting, BColor);

            TColor = HelicopterDestroyedAnnouncementTextColor; Category = "Public Helicopter Announcements"; Setting = "Destroyed Text Color";
            HelicopterDestroyedAnnouncementTextColor = GetConfig(Category, Setting, HelicopterDestroyedAnnouncementTextColor);
            CheckTextColor(HelicopterDestroyedAnnouncementTextColor, Category, Setting, TColor);

            //Chinook Announcements
            CH47SpawnAnnouncementsEnabled = GetConfig("Public Chinook Announcements", "Spawn", CH47SpawnAnnouncementsEnabled);
            CH47DespawnAnnouncementsEnabled = GetConfig("Public Chinook Announcements", "Despawn", CH47DespawnAnnouncementsEnabled);
            CH47DestroyedAnnouncementsEnabled = GetConfig("Public Chinook Announcements", "Destroyed", CH47DestroyedAnnouncementsEnabled);
            CH47DestroyedAnnouncementsWithDestroyer = GetConfig("Public Chinook Announcements", "Show Destroyer", CH47DestroyedAnnouncementsWithDestroyer);
            CH47CrateDroppedAnnouncementsEnabled = GetConfig("Public Chinook Announcements", "Announce Crate Drops", CH47CrateDroppedAnnouncementsEnabled);
            CH47CrateDroppedAnnouncementsWithLocation = GetConfig("Public Chinook Announcements", "Show Crate Drop Location", CH47CrateDroppedAnnouncementsWithLocation);
            CH47SpawnAnnouncementText = GetConfig("Public Chinook Announcements", "Spawn Text", CH47SpawnAnnouncementText);
            CH47DespawnAnnouncementText = GetConfig("Public Chinook Announcements", "Despawn Text", CH47DespawnAnnouncementText);
            CH47DestroyedAnnouncementText = GetConfig("Public Chinook Announcements", "Destroyed Text", CH47DestroyedAnnouncementText);
            CH47DestroyedAnnouncementWithDestroyerText = GetConfig("Public Chinook Announcements", "Destroyed Text With Destroyer", CH47DestroyedAnnouncementWithDestroyerText);
            CH47CrateDroppedAnnouncementText = GetConfig("Public Chinook Announcements", "Crate Dropped Text", CH47CrateDroppedAnnouncementText);
            CH47CrateDroppedAnnouncementTextWithGrid = GetConfig("Public Chinook Announcements", "Crate Dropped Text With Grid", CH47CrateDroppedAnnouncementTextWithGrid);

            BColor = CH47SpawnAnnouncementBannerColor; Category = "Public Chinook Announcements"; Setting = "Spawn Banner Color";
            CH47SpawnAnnouncementBannerColor = GetConfig(Category, Setting, CH47SpawnAnnouncementBannerColor);
            CheckBannerColor(CH47SpawnAnnouncementBannerColor, Category, Setting, BColor);

            TColor = CH47SpawnAnnouncementTextColor; Category = "Public Chinook Announcements"; Setting = "Spawn Text Color";
            CH47SpawnAnnouncementTextColor = GetConfig(Category, Setting, CH47SpawnAnnouncementTextColor);
            CheckTextColor(CH47SpawnAnnouncementTextColor, Category, Setting, TColor);

            BColor = CH47DespawnAnnouncementBannerColor; Category = "Public Chinook Announcements"; Setting = "Despawn Banner Color";
            CH47DespawnAnnouncementBannerColor = GetConfig(Category, Setting, CH47DespawnAnnouncementBannerColor);
            CheckBannerColor(CH47DespawnAnnouncementBannerColor, Category, Setting, BColor);

            TColor = CH47DespawnAnnouncementTextColor; Category = "Public Chinook Announcements"; Setting = "Despawn Text Color";
            CH47DespawnAnnouncementTextColor = GetConfig(Category, Setting, CH47DespawnAnnouncementTextColor);
            CheckTextColor(CH47DespawnAnnouncementTextColor, Category, Setting, TColor);

            BColor = CH47DestroyedAnnouncementBannerColor; Category = "Public Chinook Announcements"; Setting = "Destroyed Banner Color";
            CH47DestroyedAnnouncementBannerColor = GetConfig(Category, Setting, CH47DestroyedAnnouncementBannerColor);
            CheckBannerColor(CH47DestroyedAnnouncementBannerColor, Category, Setting, BColor);

            TColor = CH47DestroyedAnnouncementTextColor; Category = "Public Chinook Announcements"; Setting = "Destroyed Text Color";
            CH47DestroyedAnnouncementTextColor = GetConfig(Category, Setting, CH47DestroyedAnnouncementTextColor);
            CheckTextColor(CH47DestroyedAnnouncementTextColor, Category, Setting, TColor);

            BColor = CH47CrateDroppedAnnouncementBannerColor; Category = "Public Chinook Announcements"; Setting = "Crate Dropped Banner Color";
            CH47CrateDroppedAnnouncementBannerColor = GetConfig(Category, Setting, CH47CrateDroppedAnnouncementBannerColor);
            CheckBannerColor(CH47CrateDroppedAnnouncementBannerColor, Category, Setting, BColor);

            TColor = CH47CrateDroppedAnnouncementTextColor; Category = "Public Chinook Announcements"; Setting = "Crate Dropped Text Color";
            CH47CrateDroppedAnnouncementTextColor = GetConfig(Category, Setting, CH47CrateDroppedAnnouncementTextColor);
            CheckTextColor(CH47CrateDroppedAnnouncementTextColor, Category, Setting, TColor);

            //Bradley APC Announcements
            APCSpawnAnnouncementsEnabled = GetConfig("Public Bradley APC Announcements", "Spawn", APCSpawnAnnouncementsEnabled);
            APCDestroyedAnnouncementsEnabled = GetConfig("Public Bradley APC Announcements", "Destroyed", APCDestroyedAnnouncementsEnabled);
            APCDestroyedAnnouncementsWithDestroyer = GetConfig("Public Bradley APC Announcements", "Show Destroyer", APCDestroyedAnnouncementsWithDestroyer);
            APCSpawnAnnouncementText = GetConfig("Public Bradley APC Announcements", "Spawn Text", APCSpawnAnnouncementText);
            APCDestroyedAnnouncementText = GetConfig("Public Bradley APC Announcements", "Destroyed Text", APCDestroyedAnnouncementText);
            APCDestroyedAnnouncementWithDestroyerText = GetConfig("Public Bradley APC Announcements", "Destroyed With Destroyer Text", APCDestroyedAnnouncementWithDestroyerText);

            BColor = APCSpawnAnnouncementBannerColor; Category = "Public Bradley APC Announcements"; Setting = "Spawn Banner Color";
            APCSpawnAnnouncementBannerColor = GetConfig(Category, Setting, APCSpawnAnnouncementBannerColor);
            CheckBannerColor(APCSpawnAnnouncementBannerColor, Category, Setting, BColor);

            TColor = APCSpawnAnnouncementTextColor; Category = "Public Bradley APC Announcements"; Setting = "Spawn Text Color";
            APCSpawnAnnouncementTextColor = GetConfig(Category, Setting, APCSpawnAnnouncementTextColor);
            CheckTextColor(APCSpawnAnnouncementTextColor, Category, Setting, TColor);

            BColor = APCDestroyedAnnouncementBannerColor; Category = "Public Bradley APC Announcements"; Setting = "Destroyed Banner Color";
            APCDestroyedAnnouncementBannerColor = GetConfig(Category, Setting, APCDestroyedAnnouncementBannerColor);
            CheckBannerColor(APCDestroyedAnnouncementBannerColor, Category, Setting, BColor);

            TColor = APCDestroyedAnnouncementTextColor; Category = "Public Bradley APC Announcements"; Setting = "Destroyed Text Color";
            APCDestroyedAnnouncementTextColor = GetConfig(Category, Setting, APCDestroyedAnnouncementTextColor);
            CheckTextColor(APCDestroyedAnnouncementTextColor, Category, Setting, TColor);

            //Cargoship Announcements
            CargoshipSpawnAnnouncementsEnabled = GetConfig("Public Cargoship Announcements", "Spawn", CargoshipSpawnAnnouncementsEnabled);
            CargoshipEgressAnnouncementsEnabled = GetConfig("Public Cargoship Announcements", "Leave", CargoshipEgressAnnouncementsEnabled);
            CargoshipSpawnAnnouncementText = GetConfig("Public Cargoship Announcements", "Spawn Text", CargoshipSpawnAnnouncementText);
            CargoshipEgressAnnouncementText = GetConfig("Public Cargoship Announcements", "Leave Text", CargoshipEgressAnnouncementText);

            BColor = CargoshipSpawnAnnouncementBannerColor; Category = "Public Cargoship Announcements"; Setting = "Spawn Banner Color";
            CargoshipSpawnAnnouncementBannerColor = GetConfig(Category, Setting, CargoshipSpawnAnnouncementBannerColor);
            CheckBannerColor(CargoshipSpawnAnnouncementBannerColor, Category, Setting, BColor);

            TColor = CargoshipSpawnAnnouncementTextColor; Category = "Public Cargoship Announcements"; Setting = "Spawn Text Color";
            CargoshipSpawnAnnouncementTextColor = GetConfig(Category, Setting, CargoshipSpawnAnnouncementTextColor);
            CheckTextColor(CargoshipSpawnAnnouncementTextColor, Category, Setting, TColor);

            BColor = CargoshipEgressAnnouncementBannerColor; Category = "Public Cargoship Announcements"; Setting = "Leave Banner Color";
            CargoshipEgressAnnouncementBannerColor = GetConfig(Category, Setting, CargoshipEgressAnnouncementBannerColor);
            CheckBannerColor(CargoshipEgressAnnouncementBannerColor, Category, Setting, BColor);

            TColor = CargoshipEgressAnnouncementTextColor; Category = "Public Cargoship Announcements"; Setting = "Leave Text Color";
            CargoshipEgressAnnouncementTextColor = GetConfig(Category, Setting, CargoshipEgressAnnouncementTextColor);
            CheckTextColor(CargoshipEgressAnnouncementTextColor, Category, Setting, TColor);

            //Crate Hack Announcements
            CrateHackAnnouncementsEnabled = GetConfig("Public Oil Rig Crate Hack Announcements", "Enabled", CrateHackAnnouncementsEnabled);
            CrateHackSpecifyOilRig = GetConfig("Public Oil Rig Crate Hack Announcements", "Specify Which Oil Rig", CrateHackSpecifyOilRig);
            CrateHackAnnouncementText = GetConfig("Public Oil Rig Crate Hack Announcements", "Hack Text", CrateHackAnnouncementText);
            CrateHackAnnouncementSmallOilRigText = GetConfig("Public Oil Rig Crate Hack Announcements", "Hack Text With Small Oil Rig", CrateHackAnnouncementSmallOilRigText);
            CrateHackAnnouncementLargeOilRigText = GetConfig("Public Oil Rig Crate Hack Announcements", "Hack Text With Large Oil Rig", CrateHackAnnouncementLargeOilRigText);

            BColor = CrateHackAnnouncementBannerColor; Category = "Public Oil Rig Crate Hack Announcements"; Setting = "Banner Color";
            CrateHackAnnouncementBannerColor = GetConfig("Public Oil Rig Crate Hack Announcements", "Banner Color", CrateHackAnnouncementBannerColor);
            CheckBannerColor(CrateHackAnnouncementBannerColor, Category, Setting, BColor);

            TColor = CrateHackAnnouncementTextColor; Category = "Public Oil Rig Crate Hack Announcements"; Setting = "Text Color";
            CrateHackAnnouncementTextColor = GetConfig("Public Oil Rig Crate Hack Announcements", "Text Color", CrateHackAnnouncementTextColor);
            CheckBannerColor(CrateHackAnnouncementTextColor, Category, Setting, TColor);

            //New Player Announcements
            NewPlayerAnnouncementsEnabled = GetConfig("Private New Player Announcements", "Enabled", NewPlayerAnnouncementsEnabled);
            NewPlayerAnnouncementsList = GetConfig("Private New Player Announcements", "Announcements List (Show On This Many Joins : List To Show)", NewPlayerAnnouncementsList);

            BColor = NewPlayerAnnouncementsBannerColor; Category = "Private New Player Announcements"; Setting = "Banner Color";
            NewPlayerAnnouncementsBannerColor = GetConfig(Category, Setting, NewPlayerAnnouncementsBannerColor);
            CheckBannerColor(NewPlayerAnnouncementsBannerColor, Category, Setting, BColor);

            TColor = NewPlayerAnnouncementsTextColor; Category = "Private New Player Announcements"; Setting = "Text Color";
            NewPlayerAnnouncementsTextColor = GetConfig(Category, Setting, NewPlayerAnnouncementsTextColor);
            CheckTextColor(NewPlayerAnnouncementsTextColor, Category, Setting, TColor);

            //Player Banned Announcement
            PlayerBannedAnnouncementsEnabled = GetConfig("Public Player Banned Announcement", "Enabled", PlayerBannedAnnouncementsEnabled);
            PlayerBannedAnnouncmentText = GetConfig("Public Player Banned Announcement", "Text", PlayerBannedAnnouncmentText);

            BColor = PlayerBannedAnnouncementBannerColor; Category = "Public Player Banned Announcement"; Setting = "Banner Color";
            PlayerBannedAnnouncementBannerColor = GetConfig(Category, Setting, PlayerBannedAnnouncementBannerColor);
            CheckBannerColor(PlayerBannedAnnouncementBannerColor, Category, Setting, BColor);

            TColor = PlayerBannedAnnouncementTextColor; Category = "Public Player Banned Announcement"; Setting = "Text Color";
            PlayerBannedAnnouncementTextColor = GetConfig(Category, Setting, PlayerBannedAnnouncementTextColor);
            CheckTextColor(PlayerBannedAnnouncementTextColor, Category, Setting, TColor);

            //Respawn Announcements
            RespawnAnnouncementsEnabled = GetConfig("Private Respawn Announcements", "Enabled", RespawnAnnouncementsEnabled);
            RespawnAnnouncementsList = GetConfig("Private Respawn Announcements", "Announcements List", RespawnAnnouncementsList);

            BColor = RespawnAnnouncementsBannerColor; Category = "Private Respawn Announcements"; Setting = "Banner Color";
            RespawnAnnouncementsBannerColor = GetConfig(Category, Setting, RespawnAnnouncementsBannerColor);
            CheckBannerColor(RespawnAnnouncementsBannerColor, Category, Setting, BColor);

            TColor = RespawnAnnouncementsTextColor; Category = "Private Respawn Announcements"; Setting = "Text Color";
            RespawnAnnouncementsTextColor = GetConfig(Category, Setting, RespawnAnnouncementsTextColor);
            CheckTextColor(RespawnAnnouncementsTextColor, Category, Setting, TColor);

            //Restart Announcements
            RestartAnnouncementsEnabled = GetConfig("Public Restart Announcements", "Enabled", RestartAnnouncementsEnabled);
            RestartTimesList = GetConfig("Public Restart Announcements", "Restart At (HH:MM:SS)", RestartTimesList);
            if (RestartTimesList.Contains("24:00:00"))
            {
                RestartTimesList[RestartTimesList.IndexOf("24:00:00")] = "00:00:00";
                PrintWarning("\"Public Restart Announcements - Restart At (HH:MM:SS): \"24:00:00\" The 24th hour does not exist, converting to 00:00:00 (the same time)");
                Config["Public Restart Announcements", "Restart Times"] = RestartTimesList;
                ConfigUpdated = true;
            }
            RestartAnnouncementsTimes = GetConfig("Public Restart Announcements", "Announce With Time Left (HH:MM:SS)", RestartAnnouncementsTimes);
            RestartServer = GetConfig("Public Restart Announcements", "Restart My Server", RestartServer);
            RestartAnnouncementsFormat = GetConfig("Public Restart Announcements", "Restart Announcement Text", RestartAnnouncementsFormat);
            RestartSuspendedAnnouncement = GetConfig("Public Restart Announcements", "Suspended Restart Text", RestartSuspendedAnnouncement);
            RestartCancelledAnnouncement = GetConfig("Public Restart Announcements", "Cancelled Scheduled Restart Text", RestartCancelledAnnouncement);

            BColor = RestartAnnouncementsBannerColor; Category = "Public Restart Announcements"; Setting = "Banner Color";
            RestartAnnouncementsBannerColor = GetConfig(Category, Setting, RestartAnnouncementsBannerColor);
            CheckBannerColor(RestartAnnouncementsBannerColor, Category, Setting, BColor);

            TColor = RestartAnnouncementsTextColor; Category = "Public Restart Announcements"; Setting = "Text Color";
            RestartAnnouncementsTextColor = GetConfig(Category, Setting, RestartAnnouncementsTextColor);
            CheckTextColor(RestartAnnouncementsTextColor, Category, Setting, TColor);

            //Test Announcement
            TestAnnouncementFontSize = GetConfig("Private Test Announcement", "Font Size", TestAnnouncementFontSize);
            TestAnnouncementDuration = GetConfig("Private Test Announcement", "Duration", TestAnnouncementDuration);
            TestAnnouncementFadeInTime = GetConfig("Private Test Announcement", "Fade In Time", TestAnnouncementFadeInTime);
            TestAnnouncementFadeOutTime = GetConfig("Private Test Announcement", "Fade Out Time", TestAnnouncementFadeOutTime);
            TestAnnouncementAdjustVPosition = GetConfig("Private Test Announcement", "Adjust Vertical Position", TestAnnouncementAdjustVPosition);
            if (TestAnnouncementAdjustVPosition != 0f)
            {
                TABannerAnchorMaxY = (float.Parse("0.9743") + TestAnnouncementAdjustVPosition).ToString();
                TABannerAnchorMinY = (float.Parse("0.915") + TestAnnouncementAdjustVPosition).ToString();
                TATextAnchorMaxY = (float.Parse("0.9743") + TestAnnouncementAdjustVPosition).ToString();
                TATextAnchorMinY = (float.Parse("0.915") + TestAnnouncementAdjustVPosition).ToString();
            }

            BColor = TestAnnouncementBannerColor; Category = "Private Test Announcement"; Setting = "Banner Color";
            TestAnnouncementBannerColor = GetConfig(Category, Setting, TestAnnouncementBannerColor);
            CheckBannerColor(TestAnnouncementBannerColor, Category, Setting, BColor);

            TColor = TestAnnouncementsTextColor; Category = "Private Test Announcement"; Setting = "Text Color";
            TestAnnouncementsTextColor = GetConfig(Category, Setting, TestAnnouncementsTextColor);
            CheckTextColor(TestAnnouncementsTextColor, Category, Setting, TColor);

            //Third Party Plugin Support
            DoNotOverlayLustyMap = GetConfig("Third Party Plugin Support", "Do Not Overlay LustyMap", DoNotOverlayLustyMap);
            LustyMapPosition = GetConfig("Third Party Plugin Support", "LustyMap Position (Left/Right)", LustyMapPosition);
            if (LustyMapPosition.ToLower() != "left" && LustyMapPosition.ToLower() != "right" || LustyMapPosition == string.Empty || LustyMapPosition == null)
            {
                PrintWarning("Config: \"Third Party Plugin Support - LustyMap Position(Left / Right)\" is not left or right, resetting to left.");
                Config["Third Party Plugin Support", "LustyMap Position (Left/Right)"] = "Left";
                ConfigUpdated = true;
            }
            if (DoNotOverlayLustyMap == true)
            {
                if (LustyMapPosition.ToLower() == "left")
                    BannerAnchorMinX = "0.131 ";
                if (LustyMapPosition.ToLower() == "right")
                    BannerAnchorMaxX = "0.868 ";
            }

            //Welcome Announcements
            WelcomeAnnouncementsEnabled = GetConfig("Private Welcome Announcements", "Enabled", WelcomeAnnouncementsEnabled);
            WelcomeBackAnnouncement = GetConfig("Private Welcome Announcements", "Show Welcome Back If Player Has Been Here Before", WelcomeBackAnnouncement);
            WelcomeAnnouncementText = GetConfig("Private Welcome Announcements", "Welcome Text", WelcomeAnnouncementText);
            WelcomeBackAnnouncementText = GetConfig("Private Welcome Announcements", "Welcome Back Text", WelcomeBackAnnouncementText);
            WelcomeAnnouncementDuration = GetConfig("Private Welcome Announcements", "Duration", WelcomeAnnouncementDuration);
            if (WelcomeAnnouncementDuration == 0)
            {
                PrintWarning("Config: \"Private Welcome Announcement - Duration\" set to 0, resetting to 20f.");
                Config["Private Welcome Announcements", "Duration"] = 20f;
                ConfigUpdated = true;
            }
            WelcomeAnnouncementDelay = GetConfig("Private Welcome Announcements", "Delay", WelcomeAnnouncementDelay);

            BColor = WelcomeAnnouncementBannerColor; Category = "Private Welcome Announcements"; Setting = "Banner Color";
            WelcomeAnnouncementBannerColor = GetConfig(Category, Setting, WelcomeAnnouncementBannerColor);
            CheckBannerColor(WelcomeAnnouncementBannerColor, Category, Setting, BColor);

            TColor = WelcomeAnnouncementTextColor; Category = "Private Welcome Announcements"; Setting = "Text Color";
            WelcomeAnnouncementTextColor = GetConfig(Category, Setting, WelcomeAnnouncementTextColor);
            CheckTextColor(WelcomeAnnouncementTextColor, Category, Setting, TColor);

            if (ConfigUpdated)
            {
                Puts("Configuration file has been updated.");
                SaveConfig();
                ConfigUpdated = false;
                LoadGUIAnnouncementsConfig();
            }
        }

        protected override void LoadDefaultConfig() => PrintWarning("A new configuration file has been created.");

        private T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                ConfigUpdated = true;
            }
            if (data.TryGetValue(setting, out value))
            {
                if (setting == "Announcements List (Show On This Many Joins : List To Show)" || setting == "Announcement List (Show at this in game time : Announcements to show)" || setting == "Announcement List (Show at this local time : Announcements to show)")
                {
                    var keyType = typeof(T).GetGenericArguments()[0];
                    var valueType = typeof(T).GetGenericArguments()[1];
                    var dict = (IDictionary)Activator.CreateInstance(typeof(T));
                    foreach (var key in ((IDictionary)value).Keys)
                        dict.Add(Convert.ChangeType(key, keyType), Convert.ChangeType(((IDictionary)value)[key], valueType));
                    return (T)dict;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            value = defaultValue;
            data[setting] = value;
            ConfigUpdated = true;
            Puts("1");
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private List<string> ConvertObjectListToString(object value)
        {
            if (value is List<object>)
            {
                List<object> list = (List<object>)value;
                List<string> strings = list.Select(s => (string)s).ToList();
                return strings;
            }
            else return (List<string>)value;
        }
        #endregion
        #endregion
        //============================================================================================================
        #region PlayerData

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("GUIAnnouncementsPlayerData", storedData);

        void LoadSavedData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("GUIAnnouncementsPlayerData");
            if (storedData == null)
            {
                PrintWarning("GUIAnnouncement's datafile is null. Recreating data file...");
                storedData = new StoredData();
                SaveData();
                timer.Once(5, () =>
                {
                    PrintWarning("Reloading...");
                    Interface.Oxide.ReloadPlugin(Title);
                });
            }
        }

        class StoredData
        {
            public Dictionary<ulong, PlayerData> PlayerData = new Dictionary<ulong, PlayerData>();
            public StoredData() { }
        }

        class PlayerData
        {
            public string Name;
            public string UserID;
            public int TimesJoined;
            public bool Dead;
            public PlayerData() { }
        }

        void CreatePlayerData(BasePlayer player)
        {
            var Data = new PlayerData
            {
                Name = player.displayName,
                UserID = player.userID.ToString(),
                TimesJoined = 0
            };
            storedData.PlayerData.Add(player.userID, Data);
            SaveData();
        }

        StoredData storedData;
        void OnServerSave() => SaveData();

        private Dictionary<ulong, AnnouncementInfo> AnnouncementsData = new Dictionary<ulong, AnnouncementInfo>();

        class AnnouncementInfo
        {
            public string BannerTintColor;
            public string TextColor;
            public bool IsTestAnnouncement;
            public AnnouncementInfo() { }
        }

        void StoreAnnouncementData(BasePlayer player, string BannerTintColor, string TextColor, bool IsTestAnnouncement)
        {
            var Data = new AnnouncementInfo
            {
                BannerTintColor = BannerTintColor,
                TextColor = TextColor,
                IsTestAnnouncement = IsTestAnnouncement
            };
            AnnouncementsData.Add(player.userID, Data);
        }

        #endregion
        //============================================================================================================
        #region Localization

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                //Commands
                    {"AnnouncementsCommandPrefix", "announce"},
                    {"OperationsCommandPrefix", "guia"},
                    {"CommandSuffixAnnounceToPlayer", "toplayer"},
                    {"CommandSuffixAnnounceToGroup", "togroup"},
                    {"CommandSuffixAnnouncementTest", "test"},
                    {"CommandSuffixDestroyAnnouncement", "destroy"},
                    {"CommandSuffixMuteBans", "mutebans"},
                    {"CommandSuffixToggleAnnouncements", "toggle"},
                    {"CommandSuffixScheduleRestart", "schedulerestart"},
                    {"CommandSuffixSuspendRestart", "suspendrestart"},
                    {"CommandSuffixResumeRestart", "resumerestart"},
                    {"CommandSuffixGetNextRestart", "nextrestart"},
                    {"CommandSuffixCancelScheduledRestart", "cancelscheduledrestart"},
                    {"CommandSuffixHelp", "help"},
                //NotFound
                    {"PlayerNotFound", "Player {playername} not found, check the name and if they are online."},
                    {"GroupNotFound", "Group {group} not found, check the name."},
                //Permission
                    {"NoPermission", "You do not possess the required permissions."},
                //CommandUsage
                    {"AnnouncementsCommandPrefixUsage", "Usage: /announce <message>, /announce toplayer <player> <message>, /announce togroup <group> <message>."},
                    {"OperationsCommandPrefixUsage", "Usage: /guia test, /guia destroy, /guia toggle [player], /guia mutebans, /guia schedulerestart <time> [reason], /guia cancelscheduledrestart, /guia suspendrestart, /guia resumerestart, /guia nextrestart, /guia help."},
                    {"AnnounceToPlayerUsage", "Usage: /announce toplayer <player> <message>."},
                    {"AnnounceToGroupUsage", "Usage: /announce togroup <group> <message>."},
                    {"AnnouncementsToggleUsage", "Usage: /guia toggle [player]."},
                    {"ScheduleRestartUsage", "Usage: /guia schedulerestart <hh:mm:ss>."},
                    {"RunTestAnnouncementFromInGame", "You must run this command from in the game."},
                //RestartMessages
                    {"ServerAboutToRestart", "Your server is about to restart, there is no need to schedule a restart right now."},
                    {"RestartScheduled", "Restart scheduled in {time}{reason}."},
                    {"RestartAlreadyScheduled", "A restart has already been scheduled for {time}, please cancel that restart first with /guia cancelscheduledrestart." },
                    {"LaterThanNextRestart", "Restart not scheduled. Your time will be scheduled later than the next restart at {time}, please make sure you schedule a restart before {time}." },
                    {"RestartNotScheduled", "A restart has not been scheduled for you to cancel. You can schedule one with /guia schedulerstart <time> [reason]."},
                    {"ScheduledRestartCancelled", "A manually scheduled restart for {time} has been cancelled."},
                    {"GetNextRestart", "Next restart is in {time1} at {time2}."},
                    {"NoRestartScheduled", "A restart has not been scheduled." },
                    {"RestartSuspended", "The next restart at {time} has been suspended. Type /guia resumerestart to resume that restart."},
                    {"RestartAlreadySuspended", "The next restart at {time} has already been suspended. Type /guia resumerestart to resume that restart."},
                    {"NoRestartToSuspend", "A restart has not been scheduled for you to suspend. You can schedule one with /guia schedulerstart <time> [reason]."},
                    {"RestartResumed", "The previously suspended restart at {time} has been resumed."},
                    {"RestartNotSuspended", "The restart at {time} has not been suspended and cannot be resumed. You can suspend it with /guia suspendrestart."},
                    {"NoRestartToResume", "A restart has not been suspended for you to resume. You can schedule one with /guia schedulerestart <time> [reason]."},
                    {"SuspendedRestartPassed", "The previously suspended restart at {time} has passed."},
                //ToggleMessages
                    {"Excluded", "{playername} has been excluded from announcements."},
                    {"ExcludedTo", "You have been excluded from announcements."},
                    {"Included", "{playername} is being included in announcements."},
                    {"IncludedTo", "You are being included in announcements."},
                    {"IsExcluded", "{playername} is currently excluded from announcements."},
                    {"YouAreExcluded", "You are excluded from announcements and cannot see that test announcement."},
                    {"CannotExcludeServer", "You cannot exclude the server console from announcements, try specifying a name."},
                //Bans
                    {"BansMuted", "Ban announcements have been muted."},
                    {"BansUnmuted", "Ban announcements have been unmuted."},
                //Help
                    {"PlayerHelp", "Commands: /guia toggle, /guia nextrestart."},
                //Time
                    {"Hours", "hours"},
                    {"Hour", "hour"},
                    {"Minutes", "minutes"},
                    {"Seconds", "seconds"},
                    {"Second", "second"}
            }, this);
        }

        #endregion
        //============================================================================================================
        #region Initialization

        void OnServerInitialized()
        {
            LoadGUIAnnouncementsConfig();
            LoadSavedData();
            LoadDefaultMessages();
            permission.RegisterPermission(PermAnnounce, this);
            permission.RegisterPermission(PermAnnounceToggle, this);
            permission.RegisterPermission(PermAnnounceGetNextRestart, this);
            permission.RegisterPermission(PermAnnounceJoinLeave, this);

            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (!storedData.PlayerData.ContainsKey(activePlayer.userID))
                {
                    CreatePlayerData(activePlayer);
                    storedData.PlayerData[activePlayer.userID].TimesJoined = storedData.PlayerData[activePlayer.userID].TimesJoined + 1;
                    SaveData();
                }
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (!storedData.PlayerData.ContainsKey(sleepingPlayer.userID))
                {
                    CreatePlayerData(sleepingPlayer);
                    storedData.PlayerData[sleepingPlayer.userID].TimesJoined = storedData.PlayerData[sleepingPlayer.userID].TimesJoined + 1;
                    SaveData();
                }
            }

            if (AutomaticGameTimeAnnouncementsEnabled)
                GameTimeAnnouncementsStart();

            if (AutomaticRealtimeAnnouncementsEnabled)
                AutomaticRealTimeAnnouncementsStart();

            if (AutomaticTimedAnnouncementsEnabled)
                AutomaticTimedAnnouncementsStart();

            if (RestartAnnouncementsEnabled)
                RestartAnnouncementsStart();

            cmd.AddChatCommand(Lang("AnnouncementsCommandPrefix"), this, "CMDHandler");
            cmd.AddChatCommand(Lang("OperationsCommandPrefix"), this, "CMDHandler");
            cmd.AddConsoleCommand(Lang("AnnouncementsCommandPrefix"), this, "ConsoleCMDInput");
            cmd.AddConsoleCommand(Lang("OperationsCommandPrefix"), this, "ConsoleCMDInput");
        }
        #endregion
        //============================================================================================================
        #region GUI

        [HookMethod("CreateAnnouncement")]
        void CreateAnnouncement(string Msg, string bannerTintColor, string textColor, BasePlayer player = null, float APIAdjustVPosition = 0f, bool isWelcomeAnnouncement = false, bool isRestartAnnouncement = false, string group = null, bool isTestAnnouncement = false)
        {
            CuiElementContainer GUITEXT = new CuiElementContainer();
            CuiElementContainer GUIBANNER = new CuiElementContainer();
            string BAMaxY = "", BAMinY = "", TAMaxY = "", TAMinY = "";
            if (APIAdjustVPosition != 0f)
            {
                BAMaxY = (float.Parse(BannerAnchorMaxYDefault) + APIAdjustVPosition).ToString();
                BAMinY = (float.Parse(BannerAnchorMinYDefault) + APIAdjustVPosition).ToString();
                TAMaxY = (float.Parse(TextAnchorMaxYDefault) + APIAdjustVPosition).ToString();
                TAMinY = (float.Parse(TextAnchorMinYDefault) + APIAdjustVPosition).ToString();
            }
            else if (isTestAnnouncement)
            {
                BAMaxY = TABannerAnchorMaxY; BAMinY = TABannerAnchorMinY; TAMaxY = TATextAnchorMaxY; TAMinY = TATextAnchorMinY;
            }
            else
            {
                BAMaxY = BannerAnchorMaxY; BAMinY = BannerAnchorMinY; TAMaxY = TextAnchorMaxY; TAMinY = TextAnchorMinY;
            }
            GUIBANNER.Add(new CuiElement
            {
                Name = "AnnouncementBanner",
                Components =
                        {
                            new CuiImageComponent {Color = ConvertBannerColor(bannerTintColor), FadeIn = FadeInTime},
                            new CuiRectTransformComponent {AnchorMin = BannerAnchorMinX + BAMinY, AnchorMax = BannerAnchorMaxX + BAMaxY}
                        },
                FadeOut = FadeOutTime
            });
            GUITEXT.Add(new CuiElement
            {
                Name = "AnnouncementText",
                Components =
                        {
                             new CuiTextComponent {Text = Msg, FontSize = FontSize, Align = TextAnchor.MiddleCenter, FadeIn = FadeInTime, Color = ConvertTextColor(textColor)},
                             new CuiRectTransformComponent {AnchorMin = TextAnchorMinX + TAMinY, AnchorMax = TextAnchorMaxX + TAMaxY}
                        },
                FadeOut = FadeOutTime
            });
            if (player == null)
            {
                var e = BasePlayer.activePlayerList.GetEnumerator();
                for (var i = 0; e.MoveNext(); i++)
                {
                    if (!Exclusions.ContainsKey(e.Current.userID))
                    {
                        if (group == null)
                        {
                            DestroyAllTimers(e.Current);
                            GlobalTimerPlayerList.Add(e.Current.userID);
                            if (AnnouncementsData.ContainsKey(e.Current.userID) && AnnouncementsData[e.Current.userID].IsTestAnnouncement == isTestAnnouncement)
                            {
                                if (AnnouncementsData[e.Current.userID].BannerTintColor != bannerTintColor)
                                {
                                    CuiHelper.DestroyUi(e.Current, "AnnouncementBanner");
                                    CuiHelper.AddUi(e.Current, GUIBANNER);
                                }
                                CuiHelper.DestroyUi(e.Current, "AnnouncementText");
                                CuiHelper.AddUi(e.Current, GUITEXT);
                                AnnouncementsData.Remove(e.Current.userID);
                            }
                            else
                            {
                                AnnouncementsData.Remove(e.Current.userID);
                                CuiHelper.DestroyUi(e.Current, "AnnouncementBanner");
                                CuiHelper.DestroyUi(e.Current, "AnnouncementText");
                                CuiHelper.AddUi(e.Current, GUIBANNER);
                                CuiHelper.AddUi(e.Current, GUITEXT);
                            }
                            StoreAnnouncementData(e.Current, bannerTintColor, textColor, isTestAnnouncement);
                        }
                        else if (group != null)
                        {
                            if (permission.GetUserGroups(e.Current.UserIDString).Any(group.ToLower().Contains))
                            {
                                DestroyAllTimers(e.Current);
                                GlobalTimerPlayerList.Add(e.Current.userID);
                                if (AnnouncementsData.ContainsKey(e.Current.userID) && AnnouncementsData[e.Current.userID].IsTestAnnouncement == isTestAnnouncement)
                                {
                                    if (AnnouncementsData[e.Current.userID].BannerTintColor != bannerTintColor)
                                    {
                                        CuiHelper.DestroyUi(e.Current, "AnnouncementBanner");
                                        CuiHelper.AddUi(e.Current, GUIBANNER);
                                    }
                                    CuiHelper.DestroyUi(e.Current, "AnnouncementText");
                                    CuiHelper.AddUi(e.Current, GUITEXT);
                                    AnnouncementsData.Remove(e.Current.userID);
                                }
                                else
                                {
                                    AnnouncementsData.Remove(e.Current.userID);
                                    CuiHelper.DestroyUi(e.Current, "AnnouncementBanner");
                                    CuiHelper.DestroyUi(e.Current, "AnnouncementText");
                                    CuiHelper.AddUi(e.Current, GUIBANNER);
                                    CuiHelper.AddUi(e.Current, GUITEXT);
                                }
                                StoreAnnouncementData(e.Current, bannerTintColor, textColor, isTestAnnouncement);
                            }
                        }
                    }
                    else if (isRestartAnnouncement)
                        SendReply(e.Current, Msg, e.Current.userID);
                }
                GlobalTimer = timer.Once(AnnouncementDuration, () => DestroyGlobalGUI());
                return;
            }
            if (player != null)
            {
                DestroyPrivateTimer(player);
                if (AnnouncementsData.ContainsKey(player.userID) && AnnouncementsData[player.userID].IsTestAnnouncement == isTestAnnouncement)
                {
                    if (AnnouncementsData[player.userID].BannerTintColor != bannerTintColor)
                    {
                        CuiHelper.DestroyUi(player, "AnnouncementBanner");
                        CuiHelper.AddUi(player, GUIBANNER);
                    }
                    CuiHelper.DestroyUi(player, "AnnouncementText");
                    CuiHelper.AddUi(player, GUITEXT);
                    AnnouncementsData.Remove(player.userID);
                }
                else
                {
                    if (AnnouncementsData.ContainsKey(player.userID))
                        AnnouncementsData.Remove(player.userID);
                    CuiHelper.DestroyUi(player, "AnnouncementBanner");
                    CuiHelper.DestroyUi(player, "AnnouncementText");
                    CuiHelper.AddUi(player, GUIBANNER);
                    CuiHelper.AddUi(player, GUITEXT);
                }
                if (JustJoined.Contains(player.userID) && WelcomeAnnouncementsEnabled && isWelcomeAnnouncement)
                {
                    JustJoined.Remove(player.userID);
                    PrivateTimers[player] = timer.Once(WelcomeAnnouncementDuration, () => DestroyPrivateGUI(player));
                }
                else PrivateTimers[player] = timer.Once(AnnouncementDuration, () => DestroyPrivateGUI(player));
                StoreAnnouncementData(player, bannerTintColor, textColor, isTestAnnouncement);
            }
        }

        #endregion
        //============================================================================================================
        #region Functions

        void OnPlayerConnected(BasePlayer player)
        {
            if (WelcomeAnnouncementsEnabled || NewPlayerAnnouncementsEnabled || RespawnAnnouncementsEnabled || GlobalJoinAnnouncementsEnabled)
                JustJoined.Add(player.userID);
            if (!storedData.PlayerData.ContainsKey(player.userID))
                CreatePlayerData(player);
            if (storedData.PlayerData.ContainsKey(player.userID))
            {
                storedData.PlayerData[player.userID].TimesJoined = storedData.PlayerData[player.userID].TimesJoined + 1;
                SaveData();
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (JustJoined.Contains(player.userID))
                JustJoined.Remove(player.userID);
            NewPlayerPrivateTimers.TryGetValue(player, out NewPlayerTimer);
            if (NewPlayerTimer != null && !NewPlayerTimer.Destroyed)
                NewPlayerTimer.Destroy();
            PlayerRespawnedTimers.TryGetValue(player, out PlayerRespawnedTimer);
            if (PlayerRespawnedTimer != null && !PlayerRespawnedTimer.Destroyed)
                PlayerRespawnedTimer.Destroy();
            if (GlobalTimerPlayerList.Contains(player.userID))
                GlobalTimerPlayerList.Remove(player.userID);
            DestroyPrivateGUI(player);
            if (GlobalLeaveAnnouncementsEnabled)
            {
                BasePlayer.activePlayerList.Remove(player); //Need to remove player earlier than server, otherwise global leave will create announcement for the disconnecting player
                string Group = "";
                if (permission.GetUserGroups(player.UserIDString)[0].ToLower() != "default")
                    Group = char.ToUpper(permission.GetUserGroups(player.UserIDString)[0][0]) + permission.GetUserGroups(player.UserIDString)[0].Substring(1);
                if (GlobalJoinLeavePermissionOnly && HasPermission(player, PermAnnounceJoinLeave))
                    CreateAnnouncement(GlobalLeaveText.Replace("{playername}", player.displayName).Replace("{rank}", Group), GlobalLeaveAnnouncementBannerColor, GlobalLeaveAnnouncementTextColor);
                else CreateAnnouncement(GlobalLeaveText.Replace("{playername}", player.displayName).Replace("{rank}", Group), GlobalLeaveAnnouncementBannerColor, GlobalLeaveAnnouncementTextColor);
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!storedData.PlayerData.ContainsKey(player.userID))
            {
                CreatePlayerData(player);
                storedData.PlayerData[player.userID].TimesJoined = storedData.PlayerData[player.userID].TimesJoined + 1;
                SaveData();
            }
            bool wasDead = false;
            if (storedData.PlayerData[player.userID].Dead)
            {
                wasDead = true;
                storedData.PlayerData[player.userID].Dead = false;
            }
            if (JustJoined.Contains(player.userID))
            {
                float wait = 0f;
                if (GlobalJoinAnnouncementsEnabled)
                {
                    string Group = "";
                    if (permission.GetUserGroups(player.UserIDString)[0].ToLower() != "default")
                        Group = char.ToUpper(permission.GetUserGroups(player.UserIDString)[0][0]) + permission.GetUserGroups(player.UserIDString)[0].Substring(1);
                    if (GlobalJoinLeavePermissionOnly && HasPermission(player, PermAnnounceJoinLeave))
                    {
                        CreateAnnouncement(GlobalJoinText.Replace("{playername}", player.displayName).Replace("{rank}", Group), GlobalJoinAnnouncementBannerColor, GlobalJoinAnnouncementTextColor);
                        wait = wait + AnnouncementDuration;
                    }
                    else
                    {
                        CreateAnnouncement(GlobalJoinText.Replace("{playername}", player.displayName).Replace("{rank}", Group), GlobalJoinAnnouncementBannerColor, GlobalJoinAnnouncementTextColor);
                        wait = wait + AnnouncementDuration;
                    }
                }
                if (WelcomeAnnouncementsEnabled)
                {
                    timer.Once(wait, () => WelcomeAnnouncement(player));
                    wait = wait + WelcomeAnnouncementDuration + WelcomeAnnouncementDelay;
                }
                if (NewPlayerAnnouncementsEnabled)
                {
                    int timesJoined = storedData.PlayerData[player.userID].TimesJoined;
                    if (NewPlayerAnnouncementsList.ContainsKey(timesJoined) || NewPlayerAnnouncementsList.ContainsKey(0))
                    {
                        timer.Once(wait, () => NewPlayerAnnouncements(player));
                        if (NewPlayerAnnouncementsList.ContainsKey(0))
                            wait = wait + (NewPlayerAnnouncementsList[0].Count * AnnouncementDuration);
                        else wait = wait + (NewPlayerAnnouncementsList[timesJoined].Count * AnnouncementDuration);
                    }
                }
                if (wasDead == true && RespawnAnnouncementsEnabled)
                {
                    timer.Once(wait, () => RespawnedAnnouncements(player));
                    wasDead = false;
                }
            }
            else if (!JustJoined.Contains(player.userID) && wasDead == true && RespawnAnnouncementsEnabled)
            {
                RespawnedAnnouncements(player);
                wasDead = false;
            }
            if (!JustJoined.Contains(player.userID) && wasDead == true && !WelcomeAnnouncementsEnabled && !NewPlayerAnnouncementsEnabled && RespawnAnnouncementsEnabled)
            {
                RespawnedAnnouncements(player);
                wasDead = false;
            }
        }

        void DestroyAllGUI()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (AnnouncementsData.ContainsKey(player.userID))
                    AnnouncementsData.Remove(player.userID);
                DestroyAllTimers(player);
                CuiHelper.DestroyUi(player, "AnnouncementBanner");
                CuiHelper.DestroyUi(player, "AnnouncementText");
            }
        }

        void DestroyGlobalGUI()
        {
            if (GlobalTimer != null && !GlobalTimer.Destroyed)
                GlobalTimer.Destroy();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (AnnouncementsData.ContainsKey(player.userID))
                    AnnouncementsData.Remove(player.userID);
                if (GlobalTimerPlayerList.Contains(player.userID))
                {
                    GlobalTimerPlayerList.Remove(player.userID);
                    CuiHelper.DestroyUi(player, "AnnouncementBanner");
                    CuiHelper.DestroyUi(player, "AnnouncementText");
                }
            }
        }

        void DestroyPrivateGUI(BasePlayer player)
        {
            if (AnnouncementsData.ContainsKey(player.userID))
                AnnouncementsData.Remove(player.userID);
            DestroyPrivateTimer(player);
            CuiHelper.DestroyUi(player, "AnnouncementBanner");
            CuiHelper.DestroyUi(player, "AnnouncementText");
        }

        void DestroyAllTimers(BasePlayer player)
        {
            if (GlobalTimer != null && !GlobalTimer.Destroyed)
                GlobalTimer.Destroy();
            if (GlobalTimerPlayerList.Contains(player.userID))
                GlobalTimerPlayerList.Remove(player.userID);
            PrivateTimers.TryGetValue(player, out PlayerTimer);
            if (PlayerTimer != null && !PlayerTimer.Destroyed)
                PlayerTimer.Destroy();
        }

        void DestroyPrivateTimer(BasePlayer player)
        {
            if (GlobalTimerPlayerList.Contains(player.userID))
                GlobalTimerPlayerList.Remove(player.userID);
            PrivateTimers.TryGetValue(player, out PlayerTimer);
            if (PlayerTimer != null && !PlayerTimer.Destroyed)
                PlayerTimer.Destroy();
        }

        void Unload()
        {
            DestroyAllGUI();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                NewPlayerPrivateTimers.TryGetValue(player, out NewPlayerTimer);
                if (NewPlayerTimer != null && !NewPlayerTimer.Destroyed)
                    NewPlayerTimer.Destroy();
                PlayerRespawnedTimers.TryGetValue(player, out PlayerRespawnedTimer);
                if (PlayerRespawnedTimer != null && !PlayerRespawnedTimer.Destroyed)
                    PlayerRespawnedTimer.Destroy();
            }
            if (SixtySecondsTimer != null && !SixtySecondsTimer.Destroyed)
                SixtySecondsTimer.Destroy();
            if (AutomaticAnnouncementsTimer != null && !AutomaticAnnouncementsTimer.Destroyed)
                AutomaticAnnouncementsTimer.Destroy();
            if (RealTimeTimer != null && !RealTimeTimer.Destroyed)
                RealTimeTimer.Destroy();
            SaveData();
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BaseHelicopter && info.Initiator is BasePlayer && HelicopterDestroyedAnnouncementWithDestroyer)
                HeliLastHitPlayer = info.Initiator.ToPlayer().displayName;
            if (entity is CH47Helicopter && info.Initiator is BasePlayer && CH47DestroyedAnnouncementsWithDestroyer)
                CH47LastHitPlayer = info.Initiator.ToPlayer().displayName;
            if (entity is BradleyAPC && info.Initiator is BasePlayer && APCDestroyedAnnouncementsWithDestroyer)
                APCLastHitPlayer = info.Initiator.ToPlayer().displayName;
        }

        string ConvertBannerColor(string BColor)
        {
            BColor = BColor.ToLower();
            if (BColor == "grey")
                return BannerTintGrey;
            if (BColor == "red")
                return BannerTintRed;
            if (BColor == "orange")
                return BannerTintOrange;
            if (BColor == "yellow")
                return BannerTintYellow;
            if (BColor == "green")
                return BannerTintGreen;
            if (BColor == "cyan")
                return BannerTintCyan;
            if (BColor == "blue")
                return BannerTintBlue;
            if (BColor == "purple")
                return BannerTintPurple;
            else PrintWarning("Banner color not found.");
            return BannerTintGrey;
        }

        string ConvertTextColor(string TColor)
        {
            TColor = TColor.ToLower();
            if (TColor == "red")
                return TextRed;
            if (TColor == "orange")
                return TextOrange;
            if (TColor == "yellow")
                return TextYellow;
            if (TColor == "green")
                return TextGreen;
            if (TColor == "cyan")
                return TextCyan;
            if (TColor == "blue")
                return TextBlue;
            if (TColor == "purple")
                return TextPurple;
            if (TColor == "white")
                return TextWhite;
            else PrintWarning("Text color not found.");
            return TextWhite;
        }

        void CheckBannerColor(string BColor, string Category, string Setting, string Default)
        {
            if (!BannerColorList.ToLower().Contains(BColor.ToLower()))
            {
                PrintWarning("\"" + Category + " - " + Setting + ": " + BColor + "\" is not a valid color, resetting to default.");
                Config[Category, Setting] = Default;
                ConfigUpdated = true;
            }
        }

        void CheckTextColor(string TColor, string Category, string Setting, string Default)
        {
            if (!TextColorList.ToLower().Contains(TColor.ToLower()))
            {
                PrintWarning("\"" + Category + " - " + Setting + ": " + TColor + "\" is not a valid color, resetting to default.");
                Config[Category, Setting] = Default;
                ConfigUpdated = true;
            }
        }

        private static BasePlayer FindPlayer(string IDName)
        {
            foreach (BasePlayer targetPlayer in BasePlayer.activePlayerList)
            {
                if (targetPlayer.UserIDString == IDName)
                    return targetPlayer;
                if (targetPlayer.displayName.Contains(IDName, CompareOptions.OrdinalIgnoreCase))
                    return targetPlayer;
            }
            return null;
        }

        private bool HasPermission(BasePlayer player, string perm)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), perm))
                return false;
            return true;
        }

        void GameTimeAnnouncementsStart()
        {
            GameTimes = AutomaticGameTimeAnnouncementsList.Keys.ToList();
            timer.Every(5f, () => GameTimeAnnouncements());
        }

        void AutomaticRealTimeAnnouncementsStart()
        {
            RealTimes = AutomaticRealTimeAnnouncementsList.Keys.ToList();
            timer.Every(5f, () => RealTimeAnnouncements());
        }

        void AutomaticTimedAnnouncementsStart()
        {
            foreach (List<object> L in AutomaticTimedAnnouncementsList)
            {
                List<string> l = ConvertObjectListToString(L);
                AutomaticTimedAnnouncementsStringList.Add(l);
            }
            ATALEnum = AutomaticTimedAnnouncementsStringList.GetEnumerator();
            AutomaticAnnouncementsTimer = timer.Every((float)AutomaticTimedAnnouncementsRepeatTimeSpan.TotalSeconds, () =>
            {
                if (ATALEnum.MoveNext() == false)
                {
                    ATALEnum.Reset();
                    ATALEnum.MoveNext();
                }
                AutomaticTimedAnnouncements();
            });
        }

        void RestartAnnouncementsStart()
        {
            if (RestartAnnouncementsEnabled)
            {
                List<string> convertRestartTimesList = ConvertObjectListToString(RestartTimesList);
                RestartTimes = convertRestartTimesList.Select(date => DateTime.Parse(date)).ToList();
            }
            RestartAnnouncementsWhenStrings = ConvertObjectListToString(RestartAnnouncementsTimes);
            RestartAnnouncementsTimeSpans = RestartAnnouncementsWhenStrings.Select(date => TimeSpan.Parse(date)).ToList();
            GetNextRestart(RestartTimes);
            if (!RealTimeTimerStarted)
                RealTimeTimer = timer.Every(1f, () => RestartAnnouncements());
        }


        void GetNextRestart(List<DateTime> DateTimes)
        {
            var e = DateTimes.GetEnumerator();
            for (var i = 0; e.MoveNext(); i++)
            {
                if (DateTime.Compare(DateTime.Now, e.Current) < 0)
                    CalcNextRestartDict.Add(e.Current, e.Current.Subtract(DateTime.Now));
                if (DateTime.Compare(DateTime.Now, e.Current) > 0)
                    CalcNextRestartDict.Add(e.Current.AddDays(1), e.Current.AddDays(1).Subtract(DateTime.Now));
            }
            NextRestart = CalcNextRestartDict.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
            CalcNextRestartDict.Clear();
            Puts("Next restart is in " + NextRestart.Subtract(DateTime.Now).ToShortString() + " at " + NextRestart.ToLongTimeString());
        }

        private string GetGrid(Vector3 position, BasePlayer player = null)
        {
            char letter1 = 'A';
            char letter2 = '\0';
            float x = Mathf.Floor((position.x + (WorldSize / 2)) / 146.3f);
            if (x > 25)
                letter2 = 'A';
            x = x % 26;
            float z = (Mathf.Floor(WorldSize / 146.3f) - 1) - Mathf.Floor((position.z + (WorldSize / 2)) / 146.3f);
            letter1 = (char)(letter1 + x);
            return $"{letter2}{letter1}{z}".Replace("\0", "");
        }

        private void SendReply(BasePlayer Commander, string format, bool isConsole, params object[] args)
        {
            string message = args.Length > 0 ? string.Format(format, args) : format;
            if (Commander?.net != null && isConsole)
            {
                Commander.SendConsoleCommand("echo " + message); return;
            }
            if (Commander?.net == null && isConsole)
            {
                Puts(message); return;
            }
            if (Commander?.net != null)
            {
                PrintToChat(Commander, format, args); return;
            }
        }

        string Lang(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
        //============================================================================================================
        #region Auto Announcements

        void RestartAnnouncements()
        {
            var currentTime = DateTime.Now;
            if (NextRestart <= currentTime)
            {
                if (RestartSuspended)
                {
                    Puts(Lang("SuspendedRestartPassed").Replace("{time}", NextRestart.ToLongTimeString()));
                    RestartSuspended = false;
                }
                if (RestartScheduled)
                {
                    RestartReason = String.Empty;
                    RestartTimes.Remove(ScheduledRestart);
                    RestartScheduled = false;
                }
                if (RestartAnnouncementsEnabled && DontCheckNextRestart == false)
                {
                    DontCheckNextRestart = true;
                    GetNextRestartTimer = timer.Once(3f, () =>
                    {
                        GetNextRestart(RestartTimes);
                        DontCheckNextRestart = false;
                    });
                }
                return;
            }
            if (!RestartSuspended)
            {
                TimeSpan timeLeft = NextRestart.Subtract(currentTime);
                string secondsString = String.Empty;
                int hoursLeft = timeLeft.Hours, minutesLeft = timeLeft.Minutes, secondsLeft = timeLeft.Seconds;
                if ((!RestartCountdown && RestartAnnouncementsWhenStrings.Contains(timeLeft.ToShortString()) && ((LastHour != currentTime.Hour) || (LastMinute != currentTime.Minute))) || RestartJustScheduled)
                {
                    string timeLeftString = String.Empty;
                    if (RestartJustScheduled)
                        RestartJustScheduled = false;
                    if (hoursLeft > 1)
                    {
                        timeLeftString = timeLeftString + hoursLeft + " " + Lang("Hours");
                        LastHour = currentTime.Hour;
                    }
                    if (hoursLeft == 1)
                    {
                        timeLeftString = timeLeftString + hoursLeft + " " + Lang("Hour");
                        LastHour = currentTime.Hour;
                    }
                    if (minutesLeft > 0)
                    {
                        timeLeftString = timeLeftString + minutesLeft + " " + Lang("Minutes");
                        LastMinute = currentTime.Minute;
                    }
                    if (String.IsNullOrEmpty(RestartReason) && timeLeft > new TimeSpan(00, 01, 00))
                    {
                        Puts(RestartAnnouncementsFormat.Replace("{time}", timeLeftString) + ".");
                        CreateAnnouncement(RestartAnnouncementsFormat.Replace("{time}", timeLeftString) + ".", RestartAnnouncementsBannerColor, RestartAnnouncementsTextColor, isRestartAnnouncement: true);
                    }
                    else if (timeLeft > new TimeSpan(00, 01, 00))
                    {
                        Puts(RestartAnnouncementsFormat.Replace("{time}", timeLeftString) + ":" + RestartReason);
                        CreateAnnouncement(RestartAnnouncementsFormat.Replace("{time}", timeLeftString) + ":" + RestartReason, RestartAnnouncementsBannerColor, RestartAnnouncementsTextColor, isRestartAnnouncement: true);
                    }
                }
                if (timeLeft <= new TimeSpan(00, 01, 00) && !RestartCountdown)
                {
                    int countDown = timeLeft.Seconds;
                    RestartCountdown = true;
                    if (String.IsNullOrEmpty(RestartReason))
                    {
                        Puts(RestartAnnouncementsFormat.Replace("{time}", countDown.ToString() + " " + Lang("Seconds")));
                        CreateAnnouncement(RestartAnnouncementsFormat.Replace("{time}", countDown.ToString()) + " " + Lang("Seconds"), RestartAnnouncementsBannerColor, RestartAnnouncementsTextColor, isRestartAnnouncement: true);
                    }
                    else
                    {
                        Puts(RestartAnnouncementsFormat.Replace("{time}", countDown.ToString()) + " " + Lang("Seconds") + ":" + RestartReason);
                        CreateAnnouncement(RestartAnnouncementsFormat.Replace("{time}", countDown.ToString()) + " " + Lang("Seconds") + ":" + RestartReason, RestartAnnouncementsBannerColor, RestartAnnouncementsTextColor, isRestartAnnouncement: true);
                    }
                    SixtySecondsTimer = timer.Repeat(1, countDown + 1, () =>
                        {
                            if (countDown == 1)
                                secondsString = " " + Lang("Second");
                            else secondsString = " " + Lang("Seconds");
                            if (String.IsNullOrEmpty(RestartReason))
                                CreateAnnouncement(RestartAnnouncementsFormat.Replace("{time}", countDown.ToString() + secondsString), RestartAnnouncementsBannerColor, RestartAnnouncementsTextColor);
                            else CreateAnnouncement(RestartAnnouncementsFormat.Replace("{time}", countDown.ToString() + secondsString + ":" + RestartReason), RestartAnnouncementsBannerColor, RestartAnnouncementsTextColor);
                            countDown = countDown - 1;
                            if (countDown == 0)
                            {
                                if (RealTimeTimer != null && RealTimeTimer.Destroyed)
                                    RealTimeTimer.Destroy();
                                Puts("Restart countdown finished.");
                                if (RestartScheduled)
                                    RestartScheduled = false;
                                RestartCountdown = false;
                                if (RestartServer)
                                {
                                    rust.RunServerCommand("save");
                                    timer.Once(3, () => rust.RunServerCommand("restart 0"));
                                }
                            }
                        });
                }
            }
        }

        void GameTimeAnnouncements()
        {
            if (!RestartCountdown)
            {
                DateTime CurrentGameTime = TOD_Sky.Instance.Cycle.DateTime;
                string CurrentGameTimeString = CurrentGameTime.ToShortTimeString();
                if (CurrentGameTimeString != LastGameTime)
                {
                    if (GameTimes.Contains(CurrentGameTimeString) && !AnnouncingGameTime)
                    {
                        LastGameTime = CurrentGameTimeString;
                        IEnumerator<object> e = AutomaticGameTimeAnnouncementsList[CurrentGameTimeString].GetEnumerator();
                        if (e.MoveNext())
                        {
                            int wait = 0;
                            if (AnnouncingRealTime)
                                wait = RealTimeCurrentCount * (int)AnnouncementDuration;
                            int count = AutomaticGameTimeAnnouncementsList[CurrentGameTimeString].Count;
                            AnnouncingGameTime = true;
                            GameTimeCurrentCount = count;
                            timer.Once(wait, () =>
                            {
                                CreateAnnouncement(e.Current.ToString(), AutomaticGameTimeAnnouncementsBannerColor, AutomaticGameTimeAnnouncementsTextColor);
                                if (count > 1)
                                {
                                    timer.Repeat(AnnouncementDuration, count, () =>
                                    {
                                        if (e.MoveNext())
                                        {
                                            CreateAnnouncement(e.Current.ToString(), AutomaticGameTimeAnnouncementsBannerColor, AutomaticGameTimeAnnouncementsTextColor);
                                            GameTimeCurrentCount = GameTimeCurrentCount - 1;
                                        }
                                        else
                                        {
                                            AnnouncingGameTime = false;
                                            GameTimeCurrentCount = 0;
                                        }
                                    });
                                }
                                else timer.Once(AnnouncementDuration, () =>
                                {
                                    AnnouncingGameTime = false;
                                    GameTimeCurrentCount = 0;
                                });
                            });
                        }
                    }
                }
            }
        }

        void RealTimeAnnouncements()
        {
            if (!RestartCountdown)
            {
                DateTime CurrentLocalTime = DateTime.Now;
                string CurrentLocalTimeString = CurrentLocalTime.ToShortTimeString();
                if (CurrentLocalTimeString != LastRealTime)
                {
                    if (RealTimes.Contains(CurrentLocalTimeString) && !AnnouncingRealTime)
                    {
                        LastRealTime = CurrentLocalTimeString;
                        IEnumerator<object> e = AutomaticRealTimeAnnouncementsList[CurrentLocalTimeString].GetEnumerator();
                        if (e.MoveNext())
                        {
                            int wait = 0;
                            if (AnnouncingGameTime)
                                wait = GameTimeCurrentCount * (int)AnnouncementDuration;
                            int count = AutomaticRealTimeAnnouncementsList[CurrentLocalTimeString].Count;
                            AnnouncingRealTime = true;
                            RealTimeCurrentCount = count;
                            timer.Once(wait, () =>
                            {
                                CreateAnnouncement(e.Current.ToString(), AutomaticRealTimeAnnouncementsBannerColor, AutomaticRealTimeAnnouncementsTextColor);
                                if (count > 1)
                                {
                                    timer.Repeat(AnnouncementDuration, count, () =>
                                    {
                                        if (e.MoveNext())
                                        {
                                            CreateAnnouncement(e.Current.ToString(), AutomaticRealTimeAnnouncementsBannerColor, AutomaticRealTimeAnnouncementsTextColor);
                                            RealTimeCurrentCount = RealTimeCurrentCount - 1;
                                        }
                                        else
                                        {
                                            AnnouncingRealTime = false;
                                            RealTimeCurrentCount = 0;
                                        }
                                    });
                                }
                                else timer.Once(AnnouncementDuration, () =>
                                {
                                    AnnouncingRealTime = false;
                                    RealTimeCurrentCount = 0;
                                });
                            });
                        }
                    }
                }
            }
        }

        void OnUserBanned(string id, string name, string IP, string reason)
        {
            if (!RestartCountdown)
            {
                if (PlayerBannedAnnouncementsEnabled && !MutingBans)
                    CreateAnnouncement(PlayerBannedAnnouncmentText.Replace("{playername}", name).Replace("{reason}", reason), PlayerBannedAnnouncementBannerColor, PlayerBannedAnnouncementTextColor);
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!RestartCountdown)
            {
                if (HelicopterSpawnAnnouncementEnabled && entity is BaseHelicopter)
                    CreateAnnouncement(HelicopterSpawnAnnouncementText, HelicopterSpawnAnnouncementBannerColor, HelicopterSpawnAnnouncementTextColor);
                if (CH47SpawnAnnouncementsEnabled && entity is CH47Helicopter)
                {
                    CH47Helicopter CH47 = entity as CH47Helicopter;
                    timer.Once(0.5f, () =>
                    {
                        if (CH47.GetDriver()?.ShortPrefabName == "scientist_gunner")
                            CreateAnnouncement(CH47SpawnAnnouncementText, CH47SpawnAnnouncementBannerColor, CH47SpawnAnnouncementTextColor);
                    });
                }
                if (APCSpawnAnnouncementsEnabled && entity is BradleyAPC)
                    CreateAnnouncement(APCSpawnAnnouncementText, APCSpawnAnnouncementBannerColor, APCSpawnAnnouncementTextColor);
                if (CargoshipSpawnAnnouncementsEnabled && entity is CargoShip)
                    CreateAnnouncement(CargoshipSpawnAnnouncementText, CargoshipSpawnAnnouncementBannerColor, CargoshipSpawnAnnouncementTextColor);
                if (StockingRefillAnnouncementsEnabled && entity is XMasRefill)
                    CreateAnnouncement(StockingRefillAnnouncementText, StockingRefillAnnouncementBannerColor, StockingRefillAnnouncementTextColor);
            }
        }

        void OnEntityDeath(BaseCombatEntity entity)
        {
            if (!RestartCountdown)
            {
                if (entity is BaseHelicopter)
                {
                    var entityNetID = entity.net.ID.Value;
                    if (HelicopterDespawnAnnouncementEnabled)
                        HeliNetIDs.Add(entityNetID);
                    if (HelicopterDestroyedAnnouncementEnabled)
                    {
                        if (HelicopterDestroyedAnnouncementWithDestroyer)
                        {
                            CreateAnnouncement(HelicopterDestroyedAnnouncementWithDestroyerText.Replace("{playername}", HeliLastHitPlayer), HelicopterDestroyedAnnouncementBannerColor, HelicopterDestroyedAnnouncementTextColor);
                            HeliLastHitPlayer = String.Empty;
                        }
                        else CreateAnnouncement(HelicopterDestroyedAnnouncementText, HelicopterDestroyedAnnouncementBannerColor, HelicopterDestroyedAnnouncementTextColor);
                    }
                }
                if (entity is CH47Helicopter)
                {
                    var entityNetID = entity.net.ID.Value;
                    if (CH47DespawnAnnouncementsEnabled)
                        CH47NetIDs.Add(entityNetID);
                    if (CH47DestroyedAnnouncementsEnabled)
                    {
                        if (CH47DestroyedAnnouncementsWithDestroyer)
                        {
                            CreateAnnouncement(CH47DestroyedAnnouncementWithDestroyerText.Replace("{playername}", CH47LastHitPlayer), CH47DestroyedAnnouncementBannerColor, CH47DestroyedAnnouncementTextColor);
                            CH47LastHitPlayer = String.Empty;
                        }
                        else CreateAnnouncement(CH47DestroyedAnnouncementText, CH47DestroyedAnnouncementBannerColor, CH47DestroyedAnnouncementTextColor);
                    }
                }
                if (APCDestroyedAnnouncementsEnabled && entity is BradleyAPC)
                {
                    if (APCDestroyedAnnouncementsWithDestroyer)
                    {
                        CreateAnnouncement(APCDestroyedAnnouncementWithDestroyerText.Replace("{playername}", APCLastHitPlayer), APCDestroyedAnnouncementBannerColor, APCDestroyedAnnouncementTextColor);
                        APCLastHitPlayer = String.Empty;
                    }
                    else CreateAnnouncement(APCDestroyedAnnouncementText, APCDestroyedAnnouncementBannerColor, APCDestroyedAnnouncementTextColor);
                }
                if (entity is BasePlayer && entity.net.connection != null)
                {
                    if (storedData.PlayerData.ContainsKey(entity.ToPlayer().userID))
                    {
                        storedData.PlayerData[entity.ToPlayer().userID].Dead = true;
                        SaveData();
                    }
                }
            }
        }

        void OnCargoShipEgress(CargoShip cargoship)
        {
            if (!RestartCountdown)
                if (CargoshipEgressAnnouncementsEnabled)
                    CreateAnnouncement(CargoshipEgressAnnouncementText, CargoshipEgressAnnouncementBannerColor, CargoshipEgressAnnouncementTextColor);
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (!RestartCountdown)
            {
                if (entity is BaseHelicopter)
                {
                    var entityNetID = entity.net.ID.Value;
                    timer.Once(2, () =>
                    {
                        if (HeliNetIDs.Contains(entityNetID))
                            HeliNetIDs.Remove(entityNetID);
                        else if (HelicopterDespawnAnnouncementEnabled)
                            CreateAnnouncement(HelicopterDespawnAnnouncementText, HelicopterDespawnAnnouncementBannerColor, HelicopterDespawnAnnouncementTextColor);
                    });
                }
                if (entity is CH47Helicopter)
                {
                    var entityNetID = entity.net.ID.Value;
                    timer.Once(2, () =>
                    {
                        if (CH47NetIDs.Contains(entityNetID))
                        {
                            CH47NetIDs.Remove(entityNetID);
                            KilledCH47NetIDs.Add(entityNetID);
                        }
                        else if (KilledCH47NetIDs.Contains(entityNetID))
                            KilledCH47NetIDs.Remove(entityNetID);
                        else if (CH47DespawnAnnouncementsEnabled)
                        {
                            KilledCH47NetIDs.Remove(entityNetID);
                            CreateAnnouncement(CH47DespawnAnnouncementText, CH47DespawnAnnouncementBannerColor, CH47DespawnAnnouncementTextColor);
                        }
                    });
                }
            }
        }

        void OnAirdrop(CargoPlane plane, Vector3 location)
        {
            if (!RestartCountdown)
            {
                timer.Once(0.5f, () =>
                {
                    if (AirdropAnnouncementsEnabled && !plane.dropped)
                    {
                    if (AirdropAnnouncementsLocation)
                        CreateAnnouncement(AirdropAnnouncementsTextWithGrid.Replace("{grid}", GetGrid(location)), AirdropAnnouncementsBannerColor, AirdropAnnouncementsTextColor);
                    else CreateAnnouncement(AirdropAnnouncementsText, AirdropAnnouncementsBannerColor, AirdropAnnouncementsTextColor);
                    }
                });
            }
        }

        void OnHelicopterDropCrate(CH47HelicopterAIController heli)
        {
            if (!RestartCountdown)
            {
                if (CH47CrateDroppedAnnouncementsEnabled && !CH47NetIDs.Contains(heli.net.ID.Value))
                {
                    if (CH47CrateDroppedAnnouncementsWithLocation)
                        CreateAnnouncement(CH47CrateDroppedAnnouncementTextWithGrid.Replace("{grid}", GetGrid(heli.GetDropPosition())), CH47CrateDroppedAnnouncementBannerColor, CH47CrateDroppedAnnouncementTextColor);
                    else CreateAnnouncement(CH47CrateDroppedAnnouncementText, CH47CrateDroppedAnnouncementBannerColor, CH47CrateDroppedAnnouncementTextColor);
                }
            }
        }

        void OnCrateHack(HackableLockedCrate crate)
        {
            if (!RestartCountdown)
            {
                if (CrateHackAnnouncementsEnabled)
                {
                    Vector3 cratePos = crate.transform.position;
                    float distance = Vector3Ex.Distance2D(CH47LandingZone.GetClosest(cratePos).transform.position, cratePos);
                    if (CrateHackSpecifyOilRig)
                    {
                        if (distance > 10.6f && distance < 12.6f)
                            CreateAnnouncement(CrateHackAnnouncementSmallOilRigText, CrateHackAnnouncementBannerColor, CrateHackAnnouncementTextColor);
                        if (distance > 40.6f && distance < 42.6f)
                            CreateAnnouncement(CrateHackAnnouncementLargeOilRigText, CrateHackAnnouncementBannerColor, CrateHackAnnouncementTextColor);
                    }
                    else if (distance < 50f)
                        CreateAnnouncement(CrateHackAnnouncementText, CrateHackAnnouncementBannerColor, CrateHackAnnouncementTextColor);
                }
            }
        }

        void WelcomeAnnouncement(BasePlayer player)
        {
            if (!RestartCountdown)
            {
                if (WelcomeAnnouncementsEnabled)
                {
                    timer.Once(WelcomeAnnouncementDelay, () =>
                    {
                        if (WelcomeBackAnnouncement && storedData.PlayerData[player.userID].TimesJoined > 1)
                            CreateAnnouncement(WelcomeBackAnnouncementText.Replace("{playername}", player.displayName).Replace("{playercount}", BasePlayer.activePlayerList.Count.ToString()), WelcomeAnnouncementBannerColor, WelcomeAnnouncementTextColor, player, isWelcomeAnnouncement: true);
                        else CreateAnnouncement(WelcomeAnnouncementText.Replace("{playername}", player.displayName).Replace("{playercount}", BasePlayer.activePlayerList.Count.ToString()), WelcomeAnnouncementBannerColor, WelcomeAnnouncementTextColor, player, isWelcomeAnnouncement: true);
                    });
                }
            }
        }

        void NewPlayerAnnouncements(BasePlayer player)
        {
            if (!RestartCountdown)
            {
                if (JustJoined.Contains(player.userID))
                    JustJoined.Remove(player.userID);
                List<string> AnnouncementList = new List<string>();
                if (NewPlayerAnnouncementsList.ContainsKey(storedData.PlayerData[player.userID].TimesJoined))
                    AnnouncementList = ConvertObjectListToString(NewPlayerAnnouncementsList[storedData.PlayerData[player.userID].TimesJoined]);
                else if (NewPlayerAnnouncementsList.ContainsKey(0))
                    AnnouncementList = ConvertObjectListToString(NewPlayerAnnouncementsList[0]);
                if (AnnouncementList.Count > 0)
                {
                    string Group = "";
                    if (permission.GetUserGroups(player.UserIDString)[0].ToLower() != "default")
                        Group = char.ToUpper(permission.GetUserGroups(player.UserIDString)[0][0]) + permission.GetUserGroups(player.UserIDString)[0].Substring(1);
                    IEnumerator<string> e = AnnouncementList.GetEnumerator();
                    if (storedData.PlayerData[player.userID].Dead == true && RespawnAnnouncementsEnabled)
                    {
                        PlayerRespawnedTimers[player] = timer.Once(AnnouncementDuration * AnnouncementList.Count, () => RespawnedAnnouncements(player));
                        storedData.PlayerData[player.userID].Dead = false;
                        SaveData();
                    }
                    if (e.MoveNext())
                    {
                        CreateAnnouncement(e.Current.Replace("{playername}", player.displayName).Replace("{rank}", Group), NewPlayerAnnouncementsBannerColor, NewPlayerAnnouncementsTextColor, player);
                        if (AnnouncementList.Count > 1)
                            NewPlayerPrivateTimers[player] = timer.Repeat(AnnouncementDuration, AnnouncementList.Count - 1, () =>
                            {
                                if (e.MoveNext())
                                    CreateAnnouncement(e.Current.Replace("{playername}", player.displayName).Replace("{rank}", Group), NewPlayerAnnouncementsBannerColor, NewPlayerAnnouncementsTextColor, player);
                            });
                    }
                }
            }
        }

        void RespawnedAnnouncements(BasePlayer player)
        {
            if (!RestartCountdown)
            {
                if (JustJoined.Contains(player.userID))
                    JustJoined.Remove(player.userID);
                List<string> ConvertRespawnAnnouncementsList = ConvertObjectListToString(RespawnAnnouncementsList);
                IEnumerator<string> e = ConvertRespawnAnnouncementsList.GetEnumerator();
                if (e.MoveNext())
                    CreateAnnouncement(e.Current.Replace("{playername}", player.displayName), RespawnAnnouncementsBannerColor, RespawnAnnouncementsTextColor, player);
                if (RespawnAnnouncementsList.Count > 1)
                    PlayerRespawnedTimers[player] = timer.Repeat(AnnouncementDuration, RespawnAnnouncementsList.Count - 1, () =>
                    {
                        if (e.MoveNext())
                            CreateAnnouncement(e.Current.Replace("{playername}", player.displayName), RespawnAnnouncementsBannerColor, RespawnAnnouncementsTextColor, player);
                    });
            }
        }

        void AutomaticTimedAnnouncements()
        {
            if (!RestartCountdown)
            {
                IEnumerator<string> e = ATALEnum.Current.GetEnumerator();
                if (e.MoveNext())
                    CreateAnnouncement(e.Current, AutomaticTimedAnnouncementsBannerColor, AutomaticTimedAnnouncementsTextColor);
                if (ATALEnum.Current.Count > 1)
                    timer.Repeat(AnnouncementDuration, ATALEnum.Current.Count - 1, () =>
                    {
                        if (e.MoveNext())
                            CreateAnnouncement(e.Current, AutomaticTimedAnnouncementsBannerColor, AutomaticTimedAnnouncementsTextColor);
                    });
            }
        }

        #endregion
        //============================================================================================================
        #region Commands

        void ConsoleCMDInput(ConsoleSystem.Arg inputter)
        {
            BasePlayer commander = (BasePlayer)inputter.Connection?.player;
            string cmd = inputter.cmd.Name, userID = inputter.Connection?.userid.ToString();
            string[] args = inputter.Args;
            CMDHandler(commander, cmd, args, true, inputter.IsAdmin, userID);
        }

        void CMDHandler(BasePlayer commander, string cmd, string[] args, bool isConsole = false, bool isAdmin = false, string userID = "")
        {
            if (userID == "" && !isConsole)
                userID = commander.UserIDString;
            if (isAdmin || commander.net.connection.authLevel > 0 || HasPermission(commander, PermAnnounce))
            {
                if (cmd == Lang("AnnouncementsCommandPrefix", userID))
                {
                    if (args?.Length != 0)
                    {
                        if (args?[0] == Lang("CommandSuffixAnnounceToPlayer", userID))
                        {
                            if (args?.Length > 2)
                                AnnounceToPlayer(commander, args, userID, isConsole);
                            else SendReply(commander, Lang("AnnounceToPlayerUsage", userID), isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixAnnounceToGroup", userID))
                        {
                            if (args?.Length > 2)
                                AnnounceToGroup(commander, args, isConsole);
                            else SendReply(commander, Lang("AnnounceToGroupUsage", userID), isConsole); return;
                        }
                    }
                    if (args?.Length > 0)
                    {
                        Announce(commander, args); return;
                    }
                    else SendReply(commander, Lang("AnnouncementsCommandPrefixUsage", userID), isConsole);
                }
                if (cmd == Lang("OperationsCommandPrefix", userID))
                {
                    if (args?.Length != 0)
                    {
                        if (args?[0] == Lang("CommandSuffixAnnouncementTest", userID))
                        {
                            AnnounceTest(commander, userID, isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixDestroyAnnouncement", userID))
                        {
                            DestroyAnnouncement(commander); return;
                        }
                        if (args?[0] == Lang("CommandSuffixMuteBans", userID))
                        {
                            MuteBans(commander, userID, isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixToggleAnnouncements", userID))
                        {
                            ToggleAnnouncements(commander, args, userID, isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixScheduleRestart", userID))
                        {
                            ScheduleRestart(commander, args, userID, isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixSuspendRestart", userID))
                        {
                            SuspendRestart(commander, userID, isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixResumeRestart", userID))
                        {
                            ResumeRestart(commander, userID, isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixGetNextRestart", userID))
                        {
                            GetNextRestart(commander, userID, isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixCancelScheduledRestart", userID))
                        {
                            CancelScheduledRestart(commander, userID, isConsole); return;
                        }
                        if (args?[0] == Lang("CommandSuffixHelp", userID))
                        {
                            SendReply(commander, Lang("AnnouncementsCommandPrefixUsage", userID), isConsole);
                            SendReply(commander, Lang("OperationsCommandPrefixUsage", userID), isConsole);
                            return;
                        }
                    }
                    SendReply(commander, Lang("OperationsCommandPrefixUsage", userID), isConsole); return;
                }
            }
            else if (cmd == Lang("OperationsCommandPrefix", userID))
            {
                if (args?.Length == 1 && args?[0] == Lang("CommandSuffixToggleAnnouncements", userID) && HasPermission(commander, PermAnnounceToggle) || Exclusions.ContainsKey(Convert.ToUInt64(userID)))
                {
                    ToggleAnnouncements(commander, args, userID, isConsole); return;
                }
                if (args?.Length == 1 && args?[0] == Lang("CommandSuffixGetNextRestart", userID) && HasPermission(commander, PermAnnounceGetNextRestart))
                {
                    GetNextRestart(commander, userID, isConsole); return;
                }
                if ((args?.Length == 1 && args?[0] == Lang("CommandSuffixHelp", userID) || args?.Length == 0) && (HasPermission(commander, PermAnnounceToggle) || HasPermission(commander, PermAnnounceGetNextRestart)))
                {
                    SendReply(commander, Lang("PlayerHelp", userID), isConsole); return;
                }
            }
            SendReply(commander, Lang("NoPermission", userID), isConsole);
        }

        void Announce(BasePlayer player, string[] args)
        {
            string Msg = "";
            for (int i = 0; i < args.Length; i++)
                Msg = Msg + " " + args[i];
            CreateAnnouncement(Msg, "Grey", "White");
        }

        void AnnounceToPlayer(BasePlayer player, string[] args, string userID, bool isConsole)
        {
            string targetPlayer = args[1].ToLower(), Msg = "";
            for (int i = 2; i < args.Length; i++)
                Msg = Msg + " " + args[i];
            BasePlayer targetedPlayer = FindPlayer(targetPlayer);
            if (targetedPlayer != null)
            {
                if (!Exclusions.ContainsKey(targetedPlayer.userID))
                    CreateAnnouncement(Msg, "Grey", "White", targetedPlayer);
                else SendReply(player, Lang("IsExcluded", userID).Replace("{playername}", targetedPlayer.displayName), isConsole);
            }
            else SendReply(player, Lang("PlayerNotFound", userID).Replace("{playername}", targetPlayer), isConsole);
        }

        void AnnounceToGroup(BasePlayer player, string[] args, bool isConsole)
        {
            string targetGroup = args[1].ToLower(), Msg = "";
            if (permission.GroupExists(targetGroup))
            {
                for (int i = 2; i < args.Length; i++)
                    Msg = Msg + " " + args[i];
                CreateAnnouncement(Msg, "Grey", "White", group: targetGroup);
            }
            else SendReply(player, Lang("GroupNotFound", player.UserIDString).Replace("{group}", targetGroup), isConsole);
        }

        void AnnounceTest(BasePlayer player, string userID, bool isConsole)
        {
            if (player?.net == null && isConsole)
                SendReply(player, Lang("RunTestAnnouncementFromInGame", userID), isConsole);
            else if (!Exclusions.ContainsKey(Convert.ToUInt64(userID)))
                CreateAnnouncement("GUIAnnouncements Test Announcement", TestAnnouncementBannerColor, TestAnnouncementsTextColor, player, isTestAnnouncement: true);
            else SendReply(player, Lang("YouAreExcluded", player.UserIDString), isConsole);
        }

        void DestroyAnnouncement(BasePlayer player)
        {
            DestroyAllGUI();
        }

        void MuteBans(BasePlayer player, string userID, bool isConsole)
        {
            if (MutingBans)
            {
                MutingBans = false;
                SendReply(player, Lang("BansUnmuted", userID), isConsole);
                return;
            }
            if (!MutingBans)
            {
                MutingBans = true;
                SendReply(player, Lang("BansMuted", userID), isConsole);
                return;
            }
        }

        void ToggleAnnouncements(BasePlayer player, string[] args, string userID, bool isConsole)
        {
            if (args.Length == 1) //Self
            {
                if (player?.net != null)
                {
                    if (Exclusions.ContainsKey(Convert.ToUInt64(userID))) //Include
                    {
                        Exclusions.Remove(Convert.ToUInt64(userID));
                        SendReply(player, Lang("IncludedTo", userID), isConsole);
                        return;
                    }
                    else //Exclude
                    {
                        Exclusions.Add(Convert.ToUInt64(userID), player.displayName);
                        SendReply(player, Lang("ExcludedTo", userID), isConsole);
                        return;
                    }
                }
                else SendReply(player, Lang("CannotExcludeServer", userID), isConsole);

            }
            if (args.Length > 1) //Not Self
            {
                string targetPlayer = args[1].ToLower();
                ulong targetPlayerUID64; ulong.TryParse(targetPlayer, out targetPlayerUID64);
                BasePlayer targetedPlayer = FindPlayer(targetPlayer);
                var GetKey = Exclusions.FirstOrDefault(x => x.Value.Contains(targetPlayer, CompareOptions.OrdinalIgnoreCase)).Key;
                if (Exclusions.ContainsKey(GetKey) || Exclusions.ContainsKey(targetPlayerUID64)) //Include
                {
                    string PlayerName = Exclusions[GetKey];
                    Exclusions.Remove(GetKey); Exclusions.Remove(targetPlayerUID64);
                    SendReply(player, Lang("Included", userID).Replace("{playername}", PlayerName), isConsole);
                    if (targetedPlayer != null)
                        SendReply(targetedPlayer, Lang("IncludedTo", targetedPlayer.UserIDString));
                }
                else if (targetedPlayer != null) //Exclude
                {
                    Exclusions.Add(targetedPlayer.userID, targetedPlayer.displayName);
                    SendReply(player, Lang("Excluded", userID).Replace("{playername}", targetedPlayer.displayName), isConsole);
                    SendReply(targetedPlayer, Lang("ExcludedTo", targetedPlayer.UserIDString));
                }
                else SendReply(player, Lang("PlayerNotFound", userID), isConsole);
            }
        }

        void ScheduleRestart(BasePlayer player, string[] args, string userID, bool isConsole)
        {
            if (args?.Length > 0)
            {
                if (!RestartCountdown)
                {
                    if (!RestartScheduled)
                    {
                        TimeSpan scheduleRestart;
                        var currentTime = DateTime.Now;
                        if (TimeSpan.TryParse(args[1], out scheduleRestart))
                        {
                            if (RestartAnnouncementsEnabled && currentTime.Add(scheduleRestart) > NextRestart)
                            {
                                SendReply(player, Lang("LaterThanNextRestart", userID).Replace("{time}", NextRestart.ToShortTimeString()), isConsole);
                                return;
                            }
                            if (args.Length > 2)
                            {
                                RestartReason = "";
                                for (int i = 2; i < args.Length; i++)
                                    RestartReason = RestartReason + " " + args[i];
                                if (player?.net != null && !isConsole)
                                    Puts(Lang("RestartScheduled").Replace("{time}", scheduleRestart.ToShortString()).Replace("{reason}", ":" + RestartReason));
                                SendReply(player, Lang("RestartScheduled", userID).Replace("{time}", scheduleRestart.ToShortString()).Replace("{reason}", ":" + RestartReason), isConsole);
                            }
                            else
                            {
                                if (player?.net != null && !isConsole)
                                    Puts(Lang("RestartScheduled").Replace("{time}", scheduleRestart.ToShortString()).Replace("{reason}", ""));
                                SendReply(player, Lang("RestartScheduled", userID).Replace("{time}", scheduleRestart.ToShortString()).Replace("{reason}", ""), isConsole);
                            }
                            RestartTimes.Add(currentTime.Add(scheduleRestart + new TimeSpan(00, 00, 01)));
                            ScheduledRestart = currentTime.Add(scheduleRestart + new TimeSpan(00, 00, 01));
                            RestartScheduled = true;
                            RestartJustScheduled = true;
                            if (!RestartAnnouncementsEnabled)
                                RestartAnnouncementsStart();
                            else GetNextRestart(RestartTimes);
                        }
                        else SendReply(player, Lang("ChatCommandScheduleRestartUsage", userID), isConsole);
                    }
                    else SendReply(player, Lang("RestartAlreadyScheduled", userID).Replace("{time}", NextRestart.ToShortTimeString()), isConsole);
                }
                else SendReply(player, Lang("ServerAboutToRestart", userID), isConsole);
            }
            else SendReply(player, Lang("ChatCommandScheduleRestartUsage", userID), isConsole);
        }

        void SuspendRestart(BasePlayer player, string userID, bool isConsole)
        {
            if (RestartScheduled)
            {
                if (!RestartSuspended)
                {
                    TimeSpan TimeLeft = NextRestart.Subtract(DateTime.Now);
                    if (SixtySecondsTimer != null && !SixtySecondsTimer.Destroyed && RestartCountdown)
                    {
                        SixtySecondsTimer.Destroy();
                        RestartCountdown = false;
                        CreateAnnouncement(RestartSuspendedAnnouncement.Replace("{time}", TimeLeft.Seconds.ToString() + " " + Lang("Seconds")), RestartAnnouncementsBannerColor, RestartAnnouncementsTextColor, isRestartAnnouncement: true);
                    }
                    RestartCountdown = false;
                    RestartSuspended = true;
                    SendReply(player, Lang("RestartSuspended", userID).Replace("{time}", NextRestart.ToLongTimeString()), isConsole);
                }
                else SendReply(player, Lang("RestartAlreadySuspended", userID).Replace("{time}", NextRestart.ToLongTimeString()), isConsole);
            }
            else SendReply(player, Lang("NoRestartToSuspend", userID), isConsole);
        }

        void ResumeRestart(BasePlayer player, string userID, bool isConsole)
        {
            if (RestartScheduled)
            {
                if (RestartSuspended)
                {
                    RestartSuspended = false;
                    SendReply(player, Lang("RestartResumed", userID).Replace("{time}", NextRestart.ToLongTimeString()), isConsole);
                }
                else SendReply(player, Lang("RestartNotSuspended", userID).Replace("{time}", NextRestart.ToLongTimeString()), isConsole);
            }
            else SendReply(player, Lang("NoRestartToResume", userID), isConsole);
        }

        void GetNextRestart(BasePlayer player, string userID, bool isConsole)
        {
            if (RestartScheduled)
            {
                var timeLeft = NextRestart.Subtract(DateTime.Now);
                SendReply(player, Lang("GetNextRestart", userID).Replace("{time1}", timeLeft.ToShortString()).Replace("{time2}", NextRestart.ToLongTimeString()), isConsole);
            }
            else SendReply(player, Lang("NoRestartScheduled", userID), isConsole);
        }

        void CancelScheduledRestart(BasePlayer player, string userID, bool isConsole)
        {
            if (RestartScheduled)
            {
                TimeSpan TimeLeft = NextRestart.Subtract(DateTime.Now);
                RestartReason = String.Empty;
                RestartTimes.Remove(ScheduledRestart);
                RestartScheduled = false;
                RestartSuspended = false;
                if (SixtySecondsTimer != null && !SixtySecondsTimer.Destroyed && RestartCountdown)
                {
                    SixtySecondsTimer.Destroy();
                    RestartCountdown = false;
                    CreateAnnouncement(RestartCancelledAnnouncement.Replace("{time}", TimeLeft.Seconds.ToString() + " " + Lang("Seconds")), RestartAnnouncementsBannerColor, RestartAnnouncementsTextColor, isRestartAnnouncement: true);
                }
                if (RestartAnnouncementsEnabled)
                    GetNextRestart(RestartTimes);
                else RestartScheduled = false;
                if (player?.net != null && !isConsole)
                    Puts(Lang("ScheduledRestartCancelled").Replace("{time}", ScheduledRestart.ToShortTimeString()));
                SendReply(player, (Lang("ScheduledRestartCancelled", userID).Replace("{time}", ScheduledRestart.ToShortTimeString())), isConsole);
            }
            else SendReply(player, Lang("RestartNotScheduled", userID), isConsole);
        }
#endregion
    }
}