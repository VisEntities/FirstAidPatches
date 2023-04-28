using System;
using System.Collections.Generic;
using ProtoBuf;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Arkan", "Antidote", "1.0.20")]
    [Description("Player shot analysis tool for Admins")]
    class Arkan : RustPlugin
    {
        [PluginReference]
        private Plugin DiscordMessages;
		
        #region Fields
		
		private const string permName = "arkan.allowed";
		private const string permNRDrawViolation = "arkan.nr.draw";
		private const string permAIMDrawViolation = "arkan.aim.draw";
		private const string permIRDrawViolation = "arkan.ir.draw";
		private const string permNRReportChat = "arkan.nr.reportchat";
		private const string permNRReportConsole = "arkan.nr.reportconsole";
		private const string permAIMReportChat = "arkan.aim.reportchat";
		private const string permAIMReportConsole = "arkan.aim.reportconsole";
		private const string permIRReportChat = "arkan.ir.reportchat";
		private const string permIRReportConsole = "arkan.ir.reportconsole";
		private const string permNRWhitelist = "arkan.nrwhitelist";
		private const string permNRBlacklist = "arkan.nrblacklist";
		private const string permAIMWhitelist = "arkan.aimwhitelist";
		private const string permAIMBlacklist = "arkan.aimblacklist";
		private const string permIRWhitelist = "arkan.irwhitelist";
		private const string permIRBlacklist = "arkan.irblacklist";

        private Dictionary<ulong, PlayerFiredProjectlesData> PlayersFiredProjectlesData = new Dictionary<ulong, PlayerFiredProjectlesData>();

        private PlayersViolationsData PlayersViolations = new PlayersViolationsData();
        private PlayersViolationsData tmpPlayersViolations = new PlayersViolationsData();
        private string serverTimeStamp;
        private bool isAttackShow = false; //for development purposes only
		private AdminConfig RconLog = new AdminConfig();
		
		private readonly int world_defaultLayer = LayerMask.GetMask("World", "Default");
		private readonly int world_terrainLayer = LayerMask.GetMask("World", "Terrain");
		private readonly int terrainLayer = LayerMask.GetMask("Terrain");
		private readonly string stringNullValueWarning = "Error: value is null";

        private Dictionary<BasePlayer, AdminConfig> AdminsConfig = new Dictionary<BasePlayer, AdminConfig>();
		
		private static Configuration _config;
		
		private class Configuration
		{
            public float NRProcessTimer = 4f;
            public float EPSILON = 0.005f;
            public float projectileTrajectoryForgiveness = 0.3f;
            public float hitDistanceForgiveness = 0.25f;
            public float minDistanceAIMCheck = 10.0f;			
	 		public float inRockCheckDistance = 200f;
            public bool isDetectAIM = true;
            public bool isDetectNR = true;
	 		public bool isDetectIR = true;
            public bool debugMessages = true;
            public bool autoSave = true;
			public bool isCheckAIMOnTargetNPC = true;
            public float drawTime = 60f;
            public float NRViolationAngle = 0.3f;
            public float NRViolationScreenDistance = 5f;
            public float playerEyesPositionToProjectileInitialPositionDistanceForgiveness = 10f;
			
			[JsonProperty(PropertyName = "The maximum allowed value for the physics.steps parameter")]
			public float minPhysicsStepsAllowed = 40f;	
			
			[JsonProperty(PropertyName = "Check players only on the blacklist")]
	 		public bool checkBlacklist = false;
			
			[JsonProperty(PropertyName = "Notify when a player has a high value of the physics.steps parameter")]
	 		public bool notifyPhysicsStepsWarning = false;
			
			[JsonProperty(PropertyName = "Enable Discord No Recoil Notifications")]
	 		public bool DiscordNRReportEnabled = false;
			
			[JsonProperty(PropertyName = "Enable Discord AIMBOT Notifications")]
	 		public bool DiscordAIMReportEnabled = false;
			
			[JsonProperty(PropertyName = "Enable Discord In Rock Notifications")]
	 		public bool DiscordIRReportEnabled = false;
			
			[JsonProperty(PropertyName = "Discord No Recoil Webhook URL")]
	 		public string DiscordNRWebhookURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
			
			[JsonProperty(PropertyName = "Discord AIMBOT Webhook URL")]
	 		public string DiscordAIMWebhookURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
			
			[JsonProperty(PropertyName = "Discord In Rock Webhook URL")]
	 		public string DiscordIRWebhookURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

			[JsonProperty(PropertyName = "AIMBodyParts", ObjectCreationHandling = ObjectCreationHandling.Replace)]		
			public List<string> AIMBodyParts = new List<string>()
			{
				"head",
				"chest"
			};

			[JsonProperty(PropertyName = "IRBodyParts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> IRBodyParts = new List<string>()
            {
                "body",
				"pelvis",
				"left knee",
				"left foot",
				"left toe",
				"groin",
				"hip",
				"right knee",
				"right foot",
				"right toe",
				"lower spine",
				"stomach",
				"chest",
				"left shoulder",
				"left arm",
				"left forearm",
				"left hand",
				"left ring finger",
				"left thumb",
				"left wrist",
				"neck",
				"head",
				"jaw",
				"left eye",
				"right eye",
				"right shoulder",
				"right arm",
				"right forearm",
				"right hand",
				"right ring finger",
				"right thumb",
				"right wrist"
            };
    
			[JsonProperty(PropertyName = "weaponsConfig")]
            public Dictionary<string, WeaponConfig> weaponsConfig = new Dictionary<string, WeaponConfig>() {
                {"rifle.ak", new WeaponConfig() {NRDetectEnabled = true, AIMDetectEnabled = true, weaponMinTimeShotsInterval = 0.09375f, weaponMaxTimeShotsInterval = 0.15625f, NRMinShotsCountToCheck = 5, NRViolationProbability = 70f}},
                {"lmg.m249", new WeaponConfig() {NRDetectEnabled = true, AIMDetectEnabled = true, weaponMinTimeShotsInterval = 0.09375f, weaponMaxTimeShotsInterval = 0.15625f, NRMinShotsCountToCheck = 5, NRViolationProbability = 70f}},
                {"pistol.nailgun", new WeaponConfig() {NRDetectEnabled = true, AIMDetectEnabled = true, weaponMinTimeShotsInterval = 0.09375f, weaponMaxTimeShotsInterval = 0.15625f, NRMinShotsCountToCheck = 5, NRViolationProbability = 70f}},
                {"smg.2", new WeaponConfig() {NRDetectEnabled = true, AIMDetectEnabled = true, weaponMinTimeShotsInterval = 0.09375f, weaponMaxTimeShotsInterval = 0.125f, NRMinShotsCountToCheck = 5, NRViolationProbability = 70f}},
                {"smg.thompson", new WeaponConfig() {NRDetectEnabled = true, AIMDetectEnabled = true, weaponMinTimeShotsInterval = 0.09375f, weaponMaxTimeShotsInterval = 0.15625f, NRMinShotsCountToCheck = 5, NRViolationProbability = 70f}},
                {"smg.mp5", new WeaponConfig() {NRDetectEnabled = true, AIMDetectEnabled = true, weaponMinTimeShotsInterval = 0.0625f, weaponMaxTimeShotsInterval = 0.125f, NRMinShotsCountToCheck = 5, NRViolationProbability = 70f}},
                {"bow.hunting", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
            //    {"bow.compound", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"crossbow", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"rifle.bolt", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"rifle.l96", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"rifle.m39", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"rifle.semiauto", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"pistol.m92", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"pistol.python", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"pistol.revolver", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"pistol.semiauto", new WeaponConfig() {NRDetectEnabled = false, AIMDetectEnabled = true}},
                {"rifle.lr300", new WeaponConfig() {NRDetectEnabled = true, AIMDetectEnabled = true, weaponMinTimeShotsInterval = 0.09375f, weaponMaxTimeShotsInterval = 0.15625f, NRMinShotsCountToCheck = 5, NRViolationProbability = 70f}}
            };
		}

        private class WeaponConfig
        {
            public bool NRDetectEnabled;
            public bool AIMDetectEnabled;
            public float weaponMinTimeShotsInterval;
            public float weaponMaxTimeShotsInterval;
            public int NRMinShotsCountToCheck;
            public float NRViolationProbability;
        }

        private class ViolationsLog
        {
            public ulong steamID;
            public int NoRecoilViolation;
            public int AIMViolation;
			public int InRockViolation;
        }

        private class AdminConfig
        {
            public ViolationsLog violationsLog = new ViolationsLog();
        }

        private class PlayersViolationsData
        {
            public int seed;
            public int mapSize;
            public string serverTimeStamp;
            public DateTime lastSaveTime;
            public DateTime lastChangeTime;
            public Dictionary<ulong, PlayerViolationsData> Players = new Dictionary<ulong, PlayerViolationsData>();
        }

        private class PlayerFiredProjectlesData
        {
            public ulong PlayerID;
            public string PlayerName;
            public float lastFiredTime;
			public float physicsSteps = 32f;
            public SortedDictionary<int, FiredProjectile> firedProjectiles = new SortedDictionary<int, FiredProjectile>();
            public SortedDictionary<ulong, MeleeThrown> melees = new SortedDictionary<ulong, MeleeThrown>();
            public bool isChecked;
        }

        private class PlayerViolationsData
        {
            public ulong PlayerID;
            public string PlayerName;
            public SortedDictionary<string, NoRecoilViolationData> noRecoilViolations = new SortedDictionary<string, NoRecoilViolationData>();
            public SortedDictionary<string, AIMViolationData> AIMViolations = new SortedDictionary<string, AIMViolationData>();
			public SortedDictionary<string, InRockViolationsData> inRockViolations = new SortedDictionary<string, InRockViolationsData>();
        }

        private class HitData
        {
            public ProjectileRicochet hitData;
            public Vector3 startProjectilePosition;
            public Vector3 startProjectileVelocity;
            public Vector3 hitPositionWorld;
            public Vector3 hitPointStart;
            public Vector3 hitPointEnd;
            public bool isHitPointNearProjectileTrajectoryLastSegmentEndPoint = true;
            public bool isHitPointOnProjectileTrajectory = true;
            public bool isProjectileStartPointAtEndReverseProjectileTrajectory = true;
            public bool isHitPointNearProjectilePlane = true;
            public bool isLastSegmentOnProjectileTrajectoryPlane = true;
            public float distanceFromHitPointToProjectilePlane = 0f;
            public int side;
			public Vector3 pointProjectedOnLastSegmentLine;
            public float travelDistance = 0f;
            public float delta = 1f;
            public Vector3 lastSegmentPointStart;
            public Vector3 lastSegmentPointEnd;
            public Vector3 reverseLastSegmentPointStart;
            public Vector3 reverseLastSegmentPointEnd;
        }

        private class AIMViolationData
        {
            public int projectileID;
            public int violationID;
			public DateTime firedTime;
            public Vector3 startProjectilePosition;
            public Vector3 startProjectileVelocity;
            public string hitInfoInitiatorPlayerName;
            public string hitInfoInitiatorPlayerUserID;
            public string hitInfoHitEntityPlayerName;
            public string hitInfoHitEntityPlayerUserID;
            public string hitInfoBoneName;
            public Vector3 hitInfoHitPositionWorld;
            public float hitInfoProjectileDistance;
            public Vector3 hitInfoPointStart;
            public Vector3 hitInfoPointEnd;
            public float hitInfoProjectilePrefabGravityModifier;
            public float hitInfoProjectilePrefabDrag;
            public string weaponShortName;
            public string ammoShortName;
            public string bodyPart;
            public float damage;
            public bool isEqualFiredProjectileData = true;
            public bool isPlayerPositionToProjectileStartPositionDistanceViolation = false;
            public float distanceDifferenceViolation = 0f;
            public float calculatedTravelDistance;
            public bool isAttackerMount = false;
            public bool isTargetMount = false;
            public string attackerMountParentName;
            public string targetMountParentName;
            public float firedProjectileFiredTime;
            public float firedProjectileTravelTime;
            public Vector3 firedProjectilePosition;
            public Vector3 firedProjectileVelocity;
            public Vector3 firedProjectileInitialPosition;
            public Vector3 firedProjectileInitialVelocity;
            public Vector3 playerEyesLookAt;
            public Vector3 playerEyesPosition;
            public bool hasFiredProjectile = false;
            public List<HitData> hitsData = new List<HitData>();
            public float gravityModifier;
            public float drag;
			public float forgivenessModifier = 1f;
			public float physicsSteps = 32f;
			public List<string> attachments = new List<string>();
        }

        private class NoRecoilViolationData
        {
            public int ShotsCnt;
            public int NRViolationsCnt;
            public float violationProbability;
            public bool isMounted;
            public Vector3 mountParentPosition;
			public Vector4 mountParentRotation;
            public List<string> attachments = new List<string>();
            public string ammoShortName;
            public string weaponShortName;

            public Dictionary<int, SuspiciousProjectileData> suspiciousNoRecoilShots = new Dictionary<int, SuspiciousProjectileData>();
        }
		
		private class InRockViolationsData
        {
			public DateTime dateTime;
			
			public Dictionary<int, InRockViolationData> inRockViolationsData = new Dictionary<int, InRockViolationData>();
        }

		private class InRockViolationData
        {
			public DateTime dateTime;
            public float physicsSteps;
			public float targetHitDistance;
			public string targetName;
			public string targetID;
			public float targetDamage;
			public string targetBodyPart;
			public Vector3 targetHitPosition;
			public Vector3 rockHitPosition;
			public FiredProjectile firedProjectile;
			public int projectileID;
            public float drag;
            public float gravityModifier;
        }

        private struct SuspiciousProjectileData
        {
            public DateTime timeStamp;
            public int projectile1ID;
            public int projectile2ID;
            public float timeInterval;
            public Vector3 projectile1Position;
            public Vector3 projectile2Position;
            public Vector3 projectile1Velocity;
            public Vector3 projectile2Velocity;
            public Vector3 closestPointLine1;
            public Vector3 closestPointLine2;
            public Vector3 prevIntersectionPoint;
            public float recoilAngle;
            public float recoilScreenDistance;
            public bool isNoRecoil;
            public bool isShootedInMotion;
        }

        private class FiredShotsData
        {
            public string ammoShortName;
            public string weaponShortName;
            public List<string> attachments = new List<string>();
            public SortedDictionary<int, FiredProjectile> firedShots = new SortedDictionary<int, FiredProjectile>();
        }

        private class FiredProjectile
        {
            public DateTime firedTime;
            public Vector3 projectileVelocity;
            public Vector3 projectilePosition;
            public Vector3 playerEyesLookAt;
            public Vector3 playerEyesPosition;
            public bool isChecked;
            public string ammoShortName;
            public string weaponShortName;
            public uint weaponUID;
            public bool isMounted;
            public string mountParentName;
            public Vector3 mountParentPosition;
			public Vector4 mountParentRotation;
            public List<ProjectileRicochet> hitsData = new List<ProjectileRicochet>();
            public List<string> attachments = new List<string>();
            public float NRProbabilityModifier = 1f;
        }

        private struct MeleeThrown
        {
            public DateTime firedTime;
            public Vector3 position;
            public Vector3 playerEyesLookAt;
            public Vector3 playerEyesPosition;
            public float projectileVelocity;
            public float drag;
            public float gravityModifier;
            public string meleeShortName;
            public uint meleeUID;
            public bool isMounted;
            public string mountParentName;
            public Vector3 mountParentPosition;
			public Vector4 mountParentRotation;
        }

        private struct ProjectileRicochet
        {
            public int projectileID;
            public Vector3 hitPosition;
            public Vector3 inVelocity;
            public Vector3 outVelocity;
        }
		
		private class TrajectorySegment
        {
			public Vector3 pointStart;
			public Vector3 pointEnd;
        }
		
		public class EmbedFieldList
        {
            public string name { get; set;}
            public string value { get; set; }
            public bool inline { get; set; }
        }

        #endregion Fields

        #region Localization		

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PlayerAIMViolation"] = "<color=red><color=green>Arkan:</color> Player <color=yellow>{0}/{1}</color> has possibly AIMBOT violation <color=yellow>#{2}</color>. Target <color=yellow>{3}/{4}</color></color>",
				["PlayerAIMViolationCon"] = "Player {0}/{1} has possibly AIMBOT violation #{2}. Target {3}/{4}",
				["PlayerIRViolation"] = "<color=red><color=green>Arkan:</color> Player <color=yellow>{0}/{1}</color> has In Rock violation <color=yellow>#{2}</color>. Shots count <color=yellow>{3}</color></color>",
				["PlayerIRViolationCon"] = "<color=green>Arkan:</color> Player {0}/{1} has In Rock violation #{2}. Shots count <color=yellow>{3}</color></color>",
                ["InitPlgText1"] = "Arkan Init: map seed: {0}, map size: {1}, server timestamp: {2}",
                ["AIMText1"] = "AIMBOT probable violation\n",
                ["AIMText2"] = "HitPoint is located before line ProjectileTrajectoryLastSegment",
                ["AIMText3"] = "HitPoint is located behind line ProjectileTrajectoryLastSegment",
                ["AIMText4"] = "AIMBOT Violation: HitPoint is not located within line ProjectileTrajectoryLastSegment. \nBut HitPoint is located near the plane ProjectilePlane at a distance {0}. {1}, distance to {2} {3}",
                ["AIMText6"] = "AIMBOT Violation: ProjectileTrajectoryFirstSegments does not match calculated by FiredProjectile data: pointStart = {0} pointEnd = {1}, by HitInfo data: pointStart = {2} pointEnd = {3}\n",
                ["AIMText7"] = "AIMBOT Violation: HitPoint is not near the plane ProjectilePlane at a distance {0}\n",
                ["AIMText8"] = "AIMBOT Violation: ProjectileTrajectoryLastSegments does not match calculated by FiredProjectile data: pointStart = {0}, by HitInfo data: pointStart = {1} pointEnd = {2}\n",
				["ClearVD"] = "<color=red><color=green>Arkan:</color> Violations data cleared</color>",
                ["SaveVD1"] = "<color=red><color=green>Arkan:</color> Violation data saved in file <color=yellow>{0}</color></color>",
                ["LoadVD1"] = "<color=red><color=green>Arkan:</color> File <color=yellow>{0}</color> exist, saved data loaded</color>",
                ["LoadVD2"] = "Saved data map seed:{0} doesn't match map seed:{1}",
                ["LoadVD3"] = "Saved data map size:{0} doesn't match map size:{1}",
                ["LoadVD4"] = "<color=red><color=green>Arkan:</color> File <color=yellow>{0}</color> doesn't exist</color>",
				["LogText6"] = "     Distance difference {0}\n",
				["LogText7"] = "____Trajectory #{0} check data____",
				["LogText8"] = "____Trajectory #{0} data log end____",
				["VDataLog"] = "Violation data log",
				["ErrorText1"] = "Projectile data collect error: player {0}, missing projectileShoot data",
				["ErrorText2"] = "AIMBOT check error: attacker {0}, target {1}, missing ProjectileID: {2}",				
				["ShowLog1v1.0.13"] = "<size=14><color=orange><color=green>Arkan</color> by Antidote</color></size>\n" +
                    "Commands in chat:\n" +
                    "<color=orange>/arkan</color> - Show all No Recoil/AIMBOT/In Rock violations\n" +
                    "<color=orange>/arkannr</color> - Show all No Recoil violations\n" +
                    "<color=orange>/arkannr SteamID/NAME</color> - Teleport to player's first/next No Recoil violation position\n" +
                    "<color=orange>/arkannr SteamID/NAME 0/num</color> - Teleport to player's first/specific No Recoil violation position\n" +
                    "<color=orange>/arkanaim</color> - Show all AIMBOT violations\n" +
                    "<color=orange>/arkanaim SteamID/NAME</color> - Teleport to player's first/next AIMBOT violation position\n" +
                    "<color=orange>/arkanaim SteamID/NAME 0/num</color> - Teleport to player's first/specific AIMBOT violation position\n" +
					"<color=orange>/arkanir</color> - Show all In Rock violations\n" +
                    "<color=orange>/arkanir SteamID/NAME</color> - Teleport to player's first/next In Rock violation position\n" +
                    "<color=orange>/arkanir SteamID/NAME 0/num</color> - Teleport to player's first/specific In Rock violation position\n" +
                    "<color=orange>/arkansave, /arkansave filename</color> - Saves all violations to datafile\n" +
                    "<color=orange>/arkanload, /arkanload filename</color> - Loads violations from datafile\n\n",
                ["ShowLog2"] = "<size=14><color=orange>Players violations list:</color>\n<color=red>",
                ["ShowLog3"] = "<size=14><color=orange>Players violations list empty",
				["ShowInfo"] = "<size=14><color=orange><color=green>Arkan</color> by Antidote</color></size>\n" +
                    "Arkan commands info: <color=orange>/arkaninfo</color>\n\n",
                ["ShowNRLog2"] = "<size=14><color=orange>Players No Recoil violations list:</color>\n<color=red>",
                ["ShowNRLog3"] = "<size=14><color=orange>Players No Recoil violations list empty",
                ["ShowIRLog2"] = "<size=14><color=orange>Players In Rock violations list:</color>\n<color=red>",
                ["ShowIRLog3"] = "<size=14><color=orange>Players In Rock violations list empty",
				["ShowAIMLog2"] = "<size=14><color=orange>Players AIMBOT violations list:</color>\n<color=red>",	
                ["ShowAIMLog3"] = "<size=14><color=orange>Players AIMBOT violations list empty",
				["DrawIRVD1"] = "Player <color=yellow>{0}</color>\nIn Rock violation <color=gray>#{1}</color>\nEntity <color=gray>{2}</color>",
                ["DrawNRVD1"] = "Player <color=yellow>{0}</color>\nAmmo <color=gray>{1}</color>\nWeapon <color=gray>{2}</color>\nShots count <color=gray>{3}</color>\nViolation probability <color=gray>{4}</color>",
                ["DrawAIMVD3"] = "Target <color=yellow>{0}</color>\nBody part <color=gray>{1}</color>\nDamage <color=gray>{2}</color>",
                ["DrawAIMVD4"] = "Attacker <color=yellow>{0}</color>\nProjectileID <color=gray>{1}</color>\nAmmo <color=gray>{2}</color>\nWeapon <color=gray>{3}</color>\nDistance <color=gray>{4}</color>",
                ["DrawAIMVD5"] = "Attacker <color=yellow>{0}</color>, violation <color=gray>#{1}</color>\nPlayer position: <color=gray>{2}</color>\nProjectile start position: <color=gray>{3}</color>\nDistance: <color=gray>{4}</color>",
                ["ShowD1"] = "Clear current violation number",
                ["ShowD2"] = "Error. There is no such player in the logs",	
                ["ShowD3"] = "Player {0} has no more positive detections",
                ["NoMoreViolations"] = "<color=red>Player <color=yellow>{0}</color> has no more positive detections</color>",
                ["ShowNRD1"] = "Error. Player {0} has no No Recoil violation",
                ["ShowNRD2"] = "No Recoil violations count: <color=yellow>{0}</color>\n",
                ["ShowNRD3"] = "Current No Recoil violation number: <color=yellow>{0}</color>",
				["ShowIRD1"] = "Error. Player {0} has no In Rock violation",
                ["ShowIRD2"] = "In Rock violations count: <color=yellow>{0}</color>\n",
                ["ShowIRD3"] = "Current In Rock violation number: <color=yellow>{0}</color>",
                ["Player"] = "<color=green>Arkan:</color> <color=red>Player <color=yellow>{0}</color>\n",
                ["ShowAIMD1"] = "Error. Player {0} has no AIMBOT violation",
                ["ShowAIMD2"] = "AIMBOT violations count: <color=yellow>{0}</color>\n",
                ["ShowAIMD3"] = "Current AIMBOT violation number: <color=yellow>{0}</color></color>",
				["ClosestPoint"] = "Closest point 1: {0} Closest point 2: {1}, prev {2}",
				["StandingShooting"] = "Standing shooting position",
				["ShootingOnMove"] = "Shooting on the move",
				["FireTimeInterval"] = "Fire time interval {0}",
				["ProjectileID"] = "Projectile ID: {0}",
				["PlayerTxt"] = "Player",
				["Probability"] = "Probability",
				["ShotsCount"] = "Shots count",
				["AttachmentsCount"] = "Attachments count",
				["Attachment"] = "Attachment",
				["Weapon"] = "Weapon",
				["Ammo"] = "Ammo",
				["NoAttachments"] = "No attachments",
				["Attacker"] = "Attacker",
				["Target"] = "Target",
				["HitPart"] = "Hit part",
				["RecoilAngle"] = "Recoil angle: {0}",
				["Distance"] = "Distance",
				["Damage"] = "Damage",
				["NRDetection"] = "No Recoil probable violation",
				["IRDetection"] = "In Rock violation",
				["DistanceToFirstPoint"] = "Distance to first point: {0}",
				["ScreenCoords"] = "Screen coords point1: {0}, coords point2: {1}",
				["NRViolationNum"] = "NR violation #",
				["AIMViolationNum"] = "AIMBOT violation #",
				["IRViolationNum"] = "IR violation #",
				["AIMDetection"] = "AIMBOT probable violation",
				["DateTime"] = "Date/Time",
                ["MountedOn"] = "Attacker mounted on {0}",
				["RicochetsCount"] = "Ricochets count",
				["DistanceBetweenTwoPoints"] = "Distance between two points: {0}",
				["HighPhysicsStepsDetection"] = "Detected high value of the physics.steps parameter",
				["PhysicsStepsChangeWarning"] = "Player {0} probably changed the value of the physics.steps parameter to ~{1}({2}). The default value for physics.steps is 32.\nChanging the value of this parameter to a higher value allows the player to gain advantages over normal game play (allows jumping higher).",
				["Description"] = "Description",
				["DiscordWarning2"] = "Error: Discord reports enabled but plugin DiscordMessages not loaded!",
				["DiscordWarningNR"] = "Error: Discord No Recoil reports enabled but webhook for No Recoil violation is not configured!",
				["DiscordWarningAIM"] = "Error: Discord AIMBOT reports enabled but webhook for AIMBOT violation is not configured!",
				["DiscordWarningIR"] = "Error: Discord In Rock reports enabled but webhook for In Rock violation is not configured!",
                ["PlayerNRViolation"] = "<size=14><color=red><color=green>Arkan:</color> Player <color=yellow>{0}/{1}</color> has possibly No Recoil violation <color=yellow>#{2}</color>\ntotal shots cnt: <color=yellow>{3}</color> | shots with low recoil cnt: <color=yellow>{4}</color>\nprobability: <color=yellow>{5}%</color></color></size>",
				["PlayerNRViolationCon"] = "Arkan: Player {0}/{1} has possibly No Recoil violation #{2}\ntotal shots cnt: {3} | shots with low recoil cnt: {4}\nprobability: {5}",
            }, this, "en");
			
			lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PlayerAIMViolation"] = "<color=red><color=green>Arkan:</color> У игрока <color=yellow>{0}/{1}</color> обнаружено возможное нарушение AIM <color=yellow>#{2}</color>. Жертва <color=yellow>{3}/{4}</color></color>",
				["PlayerAIMViolationCon"] = "У игрока {0}/{1} обнаружено возможное нарушение AIM #{2}. Жертва {3}/{4}",
				["PlayerIRViolation"] = "<color=red><color=green>Arkan:</color> У игрока <color=yellow>{0}/{1}</color> обнаружено нарушение In Rock <color=yellow>#{2}</color>. Количество выстрелов <color=yellow>{3}</color></color>",
				["PlayerIRViolationCon"] = "<color=green>Arkan:</color> У игрока {0}/{1} обнаружено нарушение In Rock #{2}. Количество выстрелов <color=yellow>{3}</color></color>",
                ["InitPlgText1"] = "Arkan Init: сид карты: {0}, размер карты: {1}, timestamp сервера: {2}",
                ["AIMText1"] = "Вероятное нарушение AIMBOT\n",
                ["AIMText2"] = "HitPoint расположен перед сегментом траектории ProjectileTrajectoryLastSegment",
                ["AIMText3"] = "HitPoint расположен за сегментом траектории ProjectileTrajectoryLastSegment",
                ["AIMText4"] = "AIMBOT Нарушение: HitPoint расположен вне сегмента траектории ProjectileTrajectoryLastSegment. \nНо HitPoint расположен недалеко от плоскости ProjectilePlane на расстоянии {0}. {1}, дистанция к {2} {3}",
                ["AIMText6"] = "AIMBOT Нарушение: ProjectileTrajectoryFirstSegments не соответствует рассчитанным данным FiredProjectile: pointStart = {0} pointEnd = {1}, по данным HitInfo: pointStart = {2} pointEnd = {3}\n",
                ["AIMText7"] = "AIMBOT Нарушение: HitPoint расположен слишком далеко от плоскости ProjectilePlane на расстоянии {0}\n",
                ["AIMText8"] = "AIMBOT Нарушение: ProjectileTrajectoryLastSegments не соответствует рассчитанным данным FiredProjectile: pointStart = {0}, по данным HitInfo: pointStart = {1} pointEnd = {2}\n",
				["ClearVD"] = "<color=red><color=green>Arkan:</color> Данные нарушений очищены</color>",
                ["SaveVD1"] = "<color=red><color=green>Arkan:</color> Данные нарушений сохранены в файл <color=yellow>{0}</color></color>",
                ["LoadVD1"] = "<color=red><color=green>Arkan:</color> Файл <color=yellow>{0}</color> существует, сохранненые в нем данные загружены</color>",
                ["LoadVD2"] = "В загружаемом лог-файле сид карты:{0} не соответствует сиду карты на сервере:{1}",
                ["LoadVD3"] = "В загружаемом лог-файле размер карты:{0} не соответствует размеру карты на сервере:{1}",
                ["LoadVD4"] = "<color=red><color=green>Arkan:</color> Файл <color=yellow>{0}</color> не существует</color>",
				["LogText6"] = "     Разница расстояния {0}\n",
				["LogText7"] = "____Данные проверки траектории #{0}____",
				["LogText8"] = "____Конец данных лога проверки траектории #{0}____",
				["VDataLog"] = "Лог данных нарушения",
				["ErrorText1"] = "Ошибка при сборе данных снаряда: игрок {0}, отсутствуют данные projectileShoot",
				["ErrorText2"] = "Ошибка при проверке AIMBOT: атакующий {0}, жертва {1}, пропущен ProjectileID: {2}",
                ["ShowLog1v1.0.13"] = "<size=14><color=orange><color=green>Arkan</color> by Antidote</color></size>\n" +
                    "Команды в чат:\n" +
                    "<color=orange>/arkan</color> - Показывает список всех No Recoil/AIMBOT/In Rock нарушений\n" +
                    "<color=orange>/arkannr</color> - Показывает список всех No Recoil нарушений\n" +
                    "<color=orange>/arkannr SteamID/NAME</color> - Телепортирует на первую/следующую позицию нарушения No Recoil игрока\n" +
                    "<color=orange>/arkannr SteamID/NAME 0/num</color> - Телепортирует на первую/определенную позицию нарушения No Recoil игрока\n" +
                    "<color=orange>/arkanaim</color> - Показывает список всех AIMBOT нарушений\n" +
                    "<color=orange>/arkanaim SteamID/NAME</color> - Телепортирует на первую/следующую позицию нарушения AIMBOT игрока\n" +
                    "<color=orange>/arkanaim SteamID/NAME 0/num</color> - Телепортирует на первую/определенную позицию нарушения AIMBOT игрока\n" +
					"<color=orange>/arkanir</color> - Показывает список всех In Rock нарушений\n" +
                    "<color=orange>/arkanir SteamID/NAME</color> - Телепортирует на первую/следующую позицию нарушения In Rock игрока\n" +
                    "<color=orange>/arkanir SteamID/NAME 0/num</color> - Телепортирует на первую/определенную позицию нарушения No Recoil игрока\n" +
                    "<color=orange>/arkansave, /arkansave filename</color> - Сохраняет все данные нарушений в файл\n" +
                    "<color=orange>/arkanload, /arkanload filename</color> - Загружает сохраненные данные нарушений из файла\n\n",
                ["ShowLog2"] = "<size=14><color=orange>Список нарушений игроков:</color>\n<color=red>",
                ["ShowLog3"] = "<size=14><color=orange>Список нарушений игрока пуст",
				["ShowInfo"] = "<size=14><color=orange><color=green>Arkan</color> by Antidote</color></size>\n" +
                    "Инфо по командам Arkan: <color=orange>/arkaninfo</color>\n\n",
                ["ShowNRLog2"] = "<size=14><color=orange>Список нарушений No Recoil игроков:</color>\n<color=red>",
                ["ShowNRLog3"] = "<size=14><color=orange>Список нарушений No Recoil игрока пуст",
                ["ShowIRLog2"] = "<size=14><color=orange>Список нарушений In Rock игроков:</color>\n<color=red>",
                ["ShowIRLog3"] = "<size=14><color=orange>Список нарушений In Rock игрока пуст",
				["ShowAIMLog2"] = "<size=14><color=orange>Список нарушений AIMBOT игроков:</color>\n<color=red>",
                ["ShowAIMLog3"] = "<size=14><color=orange>Список нарушений AIMBOT игрока пуст",
				["DrawIRVD1"] = "Игрок <color=yellow>{0}</color>\nIn Rock нарушение <color=gray>#{1}</color>\nОбъект <color=gray>{2}</color>",
                ["DrawNRVD1"] = "Игрок <color=yellow>{0}</color>\nАммо <color=gray>{1}</color>\nОружие <color=gray>{2}</color>\nКоличество выстрелов <color=gray>{3}</color>\nВероятность нарушения <color=gray>{4}</color>",
                ["DrawAIMVD3"] = "Жертва <color=yellow>{0}</color>\nЧасть тела <color=gray>{1}</color>\nПовреждение <color=gray>{2}</color>",
                ["DrawAIMVD4"] = "Атакующий <color=yellow>{0}</color>\nProjectileID <color=gray>{1}</color>\nАммо <color=gray>{2}</color>\nОружие <color=gray>{3}</color>\nРасстояние <color=gray>{4}</color>",
                ["DrawAIMVD5"] = "Атакующий <color=yellow>{0}</color>, нарушение <color=gray>#{1}</color>\nПозиция игрока: <color=gray>{2}</color>\nТочка старта снаряда: <color=gray>{3}</color>\nРасстояние: <color=gray>{4}</color>",
                ["ShowD1"] = "Номер текущего нарушения сброшен",
                ["ShowD2"] = "Ошибка. Такого игрока нет в логах",
                ["ShowD3"] = "У игрока {0} нет больше нарушений",
                ["NoMoreViolations"] = "<color=red>У игрока <color=yellow>{0}</color> нет больше нарушений</color>",
                ["ShowNRD1"] = "Ошибка. У игрока {0} нет нарушения No Recoil",
                ["ShowNRD2"] = "Количество нарушений No Recoil: <color=yellow>{0}</color>\n",
                ["ShowNRD3"] = "Номер текущего нарушения No Recoil: <color=yellow>{0}</color>",
				["ShowIRD1"] = "Ошибка. У игрока {0} нет нарушения In Rock",
                ["ShowIRD2"] = "Количество нарушений In Rock: <color=yellow>{0}</color>\n",
                ["ShowIRD3"] = "Номер текущего нарушения In Rock: <color=yellow>{0}</color>",
                ["Player"] = "<color=green>Arkan:</color> <color=red>Игрок <color=yellow>{0}</color>\n",
                ["ShowAIMD1"] = "Ошибка. У игрока {0} нет нарушения AIMBOT",
                ["ShowAIMD2"] = "Количество нарушений AIMBOT: <color=yellow>{0}</color>\n",
                ["ShowAIMD3"] = "Номер текущего нарушения AIMBOT: <color=yellow>{0}</color></color>",
				["ClosestPoint"] = "Ближайшая точка 1: {0} Ближайшая точка 2: {1}, Предыдущая ближайшая точка {2}",
				["StandingShooting"] = "Стрельба из положения стоя",
				["ShootingOnMove"] = "Стрельба в движении",
				["FireTimeInterval"] = "Интервал между выстрелами {0}",
				["ProjectileID"] = "ID снаряда: {0}",
				["PlayerTxt"] = "Игрок",
				["Probability"] = "Вероятность",
				["ShotsCount"] = "Количество выстрелов",
				["Weapon"] = "Оружие",
				["Ammo"] = "Боеприпас",
				["AttachmentsCount"] = "Количество навески",
				["Attachment"] = "Навеска",
				["NoAttachments"] = "Нет навески",
				["Attacker"] = "Атакующий",
				["Target"] = "Жертва",
				["HitPart"] = "Место попадания",
				["RecoilAngle"] = "Угол отдачи: {0}",
				["Distance"] = "Расстояние",
				["Damage"] = "Урон",
				["NRDetection"] = "Вероятное нарушение No Recoil",
				["IRDetection"] = "Нарушение In Rock",
				["DistanceToFirstPoint"] = "Расстояние до первой точки: {0}",
				["ScreenCoords"] = "Экранные координаты точки point1: {0}, экранные координаты точки point2: {1}",
				["NRViolationNum"] = "Нарушение NR #",
				["AIMViolationNum"] = "Нарушение AIMBOT #",
				["IRViolationNum"] = "Нарушение IR #",
				["AIMDetection"] = "Вероятное нарушение AIMBOT",
				["DateTime"] = "Дата/Время",
                ["MountedOn"] = "Атакующий состыкован с {0}",
				["RicochetsCount"] = "Количество рикошетов",
				["DistanceBetweenTwoPoints"] = "Расстояние между двумя точками: {0}",
				["HighPhysicsStepsDetection"] = "Обнаружено высокое значение параметра physics.steps",
				["PhysicsStepsChangeWarning"] = "Игрок {0} вероятно изменил значение параметра physics.steps на ~{1}({2}). Значение по умолчанию для physics.steps 32.\nИзменение значения этого параметра на более высокое значение позволяет игроку получить преимущества по сравнению с обычной игрой (позволяет прыгать выше).",
				["Description"] = "Описание",
				["DiscordWarning2"] = "Ошибка: Отправка отчетов в дискорд включена, только плагин DiscordMessages не загружен!",
				["DiscordWarningNR"] = "Ошибка: Отправка отчетов No Recoil в дискорд включена, только webhook дискорда для нарушения No Recoil не настроен!",
				["DiscordWarningAIM"] = "Ошибка: Отправка отчетов AIMBOT в дискорд включена, только webhook дискорда для нарушения AIMBOT не настроен!",
				["DiscordWarningIR"] = "Ошибка: Отправка отчетов In Rock в дискорд включена, только webhook дискорда для нарушения In Rock не настроен!",
                ["PlayerNRViolation"] = "<size=14><color=red><color=green>Arkan:</color> У игрока <color=yellow>{0}/{1}</color> обнаружено возможное нарушение No Recoil <color=yellow>#{2}</color>\nобщее количество выстрелов: <color=yellow>{3}</color> | количество выстрелов с низкой отдачей: <color=yellow>{4}</color>\nвероятность: <color=yellow>{5}%</color></color></size>",
				["PlayerNRViolationCon"] = "У игрока {0}/{1} обнаружено возможное нарушение No Recoil #{2}\nобщее количество выстрелов: {3} | количество выстрелов с низкой отдачей: {4}\nвероятность: {5}",
            }, this, "ru");			
        }

        #endregion Localization

        #region Initialization & Loading
		
        private void Init()
        {
            //LoadVariables();
			LoadConfig();
		
			Unsubscribe(nameof(OnPlayerAttack));
			Unsubscribe(nameof(OnMeleeThrown));
			Unsubscribe(nameof(OnProjectileRicochet));
			Unsubscribe(nameof(OnWeaponFired));
			Unsubscribe(nameof(OnEntityTakeDamage));
			Unsubscribe(nameof(OnItemPickup));
			
			if (_config.DiscordNRWebhookURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" && _config.DiscordNRReportEnabled)
            {
                PrintWarning(Lang("DiscordWarningNR", null));
                _config.DiscordNRReportEnabled = false;
            }			

			if (_config.DiscordAIMWebhookURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" && _config.DiscordAIMReportEnabled)
            {
                PrintWarning(Lang("DiscordWarningAIM", null));
                _config.DiscordAIMReportEnabled = false;
            }			

			if (_config.DiscordIRWebhookURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" && _config.DiscordIRReportEnabled)
            {
                PrintWarning(Lang("DiscordWarningIR", null));
                _config.DiscordIRReportEnabled = false;
            }						
        }

        private void OnServerInitialized()
        {
			if (_config.isDetectNR)
				Subscribe(nameof(OnWeaponFired));
			
			if (_config.isDetectAIM)
			{
				Subscribe(nameof(OnPlayerAttack));
				Subscribe(nameof(OnMeleeThrown));
				Subscribe(nameof(OnProjectileRicochet));
				Subscribe(nameof(OnWeaponFired));
				Subscribe(nameof(OnEntityTakeDamage));	
				Subscribe(nameof(OnItemPickup));				
			}
						
            DateTime currentDate = DateTime.Now.AddSeconds(-UnityEngine.Time.realtimeSinceStartup);
            serverTimeStamp = currentDate.Year + "." + currentDate.Month + "." + currentDate.Day + "." + currentDate.Hour + "." + currentDate.Minute;
            PlayersViolations.seed = ConVar.Server.seed;
            PlayersViolations.mapSize = ConVar.Server.worldsize;
            PlayersViolations.serverTimeStamp = serverTimeStamp;

            LoadViolationsData(null, null, null);
			
            Puts(Lang("InitPlgText1", null, PlayersViolations.seed, PlayersViolations.mapSize, PlayersViolations.serverTimeStamp));

            foreach (var _player in BasePlayer.activePlayerList.Where(x => x.IsAdmin && permission.UserHasPermission(x.UserIDString, permName)))
				AdminLogInit(_player);	

			if ((_config.DiscordNRReportEnabled || _config.DiscordAIMReportEnabled || _config.DiscordIRReportEnabled) && DiscordMessages == null)
				PrintWarning(Lang("DiscordWarning2", null));
        }

        void OnPlayerConnected(BasePlayer player)
        {
			if (player != null)
			{
				if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, permName))
					return;
			
				AdminLogInit(player);
			}
        }

        #endregion Initialization & Loading

        #region Helpers
		
		private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
		
		public string RemoveFormatting(string source) => source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;

        #endregion Helpers

        #region Oxide Hooks

        private void Unload()
        {
            if (PlayersViolations.lastChangeTime > PlayersViolations.lastSaveTime)
                SaveViolationsData(null, null, null);
			
			PlayersFiredProjectlesData.Clear();
			AdminsConfig.Clear();
			_config.weaponsConfig.Clear();
			_config.AIMBodyParts.Clear();
			_config.IRBodyParts.Clear();
			
			_config = null;
        }

        private void Loaded()
        {
			permission.RegisterPermission(permName, this);
			permission.RegisterPermission(permNRDrawViolation, this);
			permission.RegisterPermission(permAIMDrawViolation, this);
			permission.RegisterPermission(permIRDrawViolation, this);
			permission.RegisterPermission(permNRReportChat, this);
			permission.RegisterPermission(permNRReportConsole, this);
			permission.RegisterPermission(permAIMReportChat, this);
			permission.RegisterPermission(permAIMReportConsole, this);
			permission.RegisterPermission(permIRReportChat, this);
			permission.RegisterPermission(permIRReportConsole, this);
			permission.RegisterPermission(permNRWhitelist, this);
			permission.RegisterPermission(permNRBlacklist, this);
			permission.RegisterPermission(permAIMWhitelist, this);
			permission.RegisterPermission(permAIMBlacklist, this);
			permission.RegisterPermission(permIRWhitelist, this);
			permission.RegisterPermission(permIRBlacklist, this);

            LoadViolationsData(null, null, null);
        }

        private void OnServerSave()
        {
            if (_config.autoSave && PlayersViolations.lastChangeTime > PlayersViolations.lastSaveTime)
                SaveViolationsData(null, null, null);
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || info.Weapon == null)
                return;
				
			if (_config.checkBlacklist)
			{
				if (!(permission.UserHasPermission(attacker.UserIDString, permNRBlacklist) || permission.UserHasPermission(attacker.UserIDString, permAIMBlacklist)))
                    return;
			}
			else
				if (permission.UserHasPermission(attacker.UserIDString, permNRWhitelist) || permission.UserHasPermission(attacker.UserIDString, permAIMWhitelist))
                    return;

            if (PlayersFiredProjectlesData.ContainsKey(attacker.userID))
            {
                if (PlayersFiredProjectlesData[attacker.userID].firedProjectiles.ContainsKey(info.ProjectileID))
                {
                    PlayersFiredProjectlesData[attacker.userID].firedProjectiles[info.ProjectileID].hitsData.Add(new ProjectileRicochet());
                    ProjectileRicochet pr;
                    pr.projectileID = info.ProjectileID;
                    pr.hitPosition = info.HitPositionWorld;
                    pr.inVelocity = (info.PointEnd - info.PointStart) * PlayersFiredProjectlesData[attacker.userID].physicsSteps;
                    pr.outVelocity = info.ProjectileVelocity;
                    PlayersFiredProjectlesData[attacker.userID].firedProjectiles[info.ProjectileID].hitsData[PlayersFiredProjectlesData[attacker.userID].firedProjectiles[info.ProjectileID].hitsData.Count - 1] = pr;
                }
            }

            //for development purposes only
            if (isAttackShow)
            {
                BaseMelee component = info.Weapon.GetComponent<BaseMelee>();

                FiredProjectile fp;
				
                if (component == null)
                {
					foreach (var _player in BasePlayer.activePlayerList.Where(x => x.IsAdmin && permission.UserHasPermission(x.UserIDString, permName)))
                        foreach (KeyValuePair<ulong, PlayerFiredProjectlesData> list in PlayersFiredProjectlesData)
                            if (PlayersFiredProjectlesData[list.Key].firedProjectiles.TryGetValue(info.ProjectileID, out fp))
                                DrawProjectileTrajectory2(_player, _config.drawTime, fp, info, Color.blue, PlayersFiredProjectlesData[list.Key].physicsSteps);
                }
            }
        }

        private void OnMeleeThrown(BasePlayer player, Item item)
        {
            if (player == null || item == null)
                return;
			
			if (_config.checkBlacklist)
			{
				if (!(permission.UserHasPermission(player.UserIDString, permNRBlacklist) || permission.UserHasPermission(player.UserIDString, permAIMBlacklist)))
                    return;
			}
			else
				if (permission.UserHasPermission(player.UserIDString, permNRWhitelist) || permission.UserHasPermission(player.UserIDString, permAIMWhitelist))
                    return;

            ItemModProjectile component = item.info.GetComponent<ItemModProjectile>();

            if (component != null)
            {
                if (component.projectileObject != null)
                {
                    GameObject gameObject = component.projectileObject.Get();
                    if (gameObject != null)
                    {
                        Projectile component1 = gameObject.GetComponent<Projectile>();
                        if (component1 != null)
                        {
                            if (!PlayersFiredProjectlesData.ContainsKey(player.userID))
                            {
                                PlayersFiredProjectlesData.Add(player.userID, new PlayerFiredProjectlesData());
                                PlayersFiredProjectlesData[player.userID].PlayerID = player.userID;
                                PlayersFiredProjectlesData[player.userID].PlayerName = player.displayName;
                            }

                            MeleeThrown _melee = new MeleeThrown();

                            ulong meleeID = item.uid.Value;

							if (!PlayersFiredProjectlesData[player.userID].melees.ContainsKey(meleeID))
                                PlayersFiredProjectlesData[player.userID].melees.Add(meleeID, new MeleeThrown());
							else
							{
								Puts($"Error: OnMeleeThrown(), duplicate meleeID ({meleeID}), player ({player.displayName}/{player.userID})");
								return;
							}

                            _melee.firedTime = DateTime.Now;
                            _melee.projectileVelocity = component.projectileVelocity;
                            _melee.playerEyesLookAt = player.eyes.HeadForward();
                            _melee.playerEyesPosition = player.eyes.position;
							//	_melee.playerEyesPosition = player.eyes.position - player.eyes.HeadForward().normalized * (_config.playerEyesPositionToProjectileInitialPositionDistanceForgiveness * 2f); //uncomment this line to get AIMBOT false positives for testing purposes
                            _melee.drag = component1.drag;
                            _melee.gravityModifier = component1.gravityModifier;
                            _melee.position = player.eyes.position;
							_melee.meleeShortName = item.info.shortname;
                            _melee.meleeUID = (uint)item.uid.Value;

                            if (player.GetParentEntity() != null)
                            {
                                _melee.isMounted = true;
                                BaseEntity parentEntity = player.GetParentEntity();
                                _melee.mountParentName = parentEntity._name;
                                _melee.mountParentPosition = parentEntity.ServerPosition;
								_melee.mountParentRotation.x = parentEntity.ServerRotation.x;
								_melee.mountParentRotation.y = parentEntity.ServerRotation.y;
								_melee.mountParentRotation.z = parentEntity.ServerRotation.z;
								_melee.mountParentRotation.w = parentEntity.ServerRotation.w;
                            }

                            PlayersFiredProjectlesData[player.userID].melees[meleeID] = _melee;

                            PlayersFiredProjectlesData[player.userID].lastFiredTime = UnityEngine.Time.realtimeSinceStartup;

                            timer.Once(9f, () => CleanupExpiredProjectiles(player));
                        }
                    }
                }
            }
        }
		
		private void OnItemPickup(Item item, BasePlayer player)
		{
			if (player == null || item == null)
                return;
			
			if (_config.checkBlacklist)
			{
				if (!(permission.UserHasPermission(player.UserIDString, permNRBlacklist) || permission.UserHasPermission(player.UserIDString, permAIMBlacklist)))
                    return;
			}
			else
				if (permission.UserHasPermission(player.UserIDString, permNRWhitelist) || permission.UserHasPermission(player.UserIDString, permAIMWhitelist))
                    return;
		
			if (PlayersFiredProjectlesData.ContainsKey(player.userID))
				if (PlayersFiredProjectlesData[player.userID].melees.ContainsKey(item.uid.Value))
					PlayersFiredProjectlesData[player.userID].melees.Remove(item.uid.Value);
		}

        private void OnProjectileRicochet(BasePlayer player, PlayerProjectileRicochet playerProjectileRicochet)
        {
            if (player == null || playerProjectileRicochet == null)
                return;
			
			if (_config.checkBlacklist)
			{
				if (!(permission.UserHasPermission(player.UserIDString, permNRBlacklist) || permission.UserHasPermission(player.UserIDString, permAIMBlacklist)))
                    return;
			}
			else
				if (permission.UserHasPermission(player.UserIDString, permNRWhitelist) || permission.UserHasPermission(player.UserIDString, permAIMWhitelist))
                    return;

            if (PlayersFiredProjectlesData.ContainsKey(player.userID))
            {
                if (PlayersFiredProjectlesData[player.userID].firedProjectiles.ContainsKey(playerProjectileRicochet.projectileID))
                {
                    PlayersFiredProjectlesData[player.userID].firedProjectiles[playerProjectileRicochet.projectileID].hitsData.Add(new ProjectileRicochet());
                    ProjectileRicochet pr;
                    pr.projectileID = playerProjectileRicochet.projectileID;
                    pr.hitPosition = playerProjectileRicochet.hitPosition;
                    pr.inVelocity = playerProjectileRicochet.inVelocity;
                    pr.outVelocity = playerProjectileRicochet.outVelocity;
                    PlayersFiredProjectlesData[player.userID].firedProjectiles[playerProjectileRicochet.projectileID].hitsData[PlayersFiredProjectlesData[player.userID].firedProjectiles[playerProjectileRicochet.projectileID].hitsData.Count - 1] = pr;
                }
            }
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProjectileShoot projectileShoot)
        {
            if (player == null || projectile == null || mod == null || projectileShoot == null)
                return;
			
			if (_config.checkBlacklist)
			{
				if (!(permission.UserHasPermission(player.UserIDString, permNRBlacklist) || permission.UserHasPermission(player.UserIDString, permAIMBlacklist)))
                    return;
			}
			else
				if (permission.UserHasPermission(player.UserIDString, permNRWhitelist) || permission.UserHasPermission(player.UserIDString, permAIMWhitelist))
                    return;

            if (projectileShoot.projectiles[0] == null)
            {
                Puts(Lang("ErrorText1", player.UserIDString, player.displayName));
                return;
            }

            Item item = player.GetActiveItem();
			
			if (item == null)
                return;

            WeaponConfig _WeaponConfig;
		
            if (!_config.weaponsConfig.TryGetValue(item.info.shortname, out _WeaponConfig))
                return;

            if (!(_WeaponConfig.NRDetectEnabled || _WeaponConfig.AIMDetectEnabled))
                return;

            if (!PlayersFiredProjectlesData.ContainsKey(player.userID))
            {
                PlayersFiredProjectlesData.Add(player.userID, new PlayerFiredProjectlesData());
                PlayersFiredProjectlesData[player.userID].PlayerID = player.userID;
                PlayersFiredProjectlesData[player.userID].PlayerName = player.displayName;
            }

            float _velocity = mod.projectileVelocity;
            float NRProbabilityModifier = 1f;
            List<string> attachments = new List<string>();

            if (item.contents != null)
                foreach (Item attachment in item.contents.itemList)
                {
					if (attachment == null)
						return;
					
                    attachments.Add(attachment.info.shortname);
                    switch (attachment.info.shortname)
                    {
                        case "weapon.mod.muzzleboost":
                            _velocity -= (_velocity / 100f) * 10f;
                            break;
                        case "weapon.mod.muzzlebrake":
                            _velocity -= (_velocity / 100f) * 20f;
                            NRProbabilityModifier -= 0.1f;
                            break;
                        case "weapon.mod.silencer":
                            _velocity -= (_velocity / 100f) * 25f;
                            NRProbabilityModifier -= 0.05f;
                            break;
                    }
                }

            switch (item.info.shortname)
            {
                case "smg.2":
                    _velocity -= (_velocity / 100f) * 20f;
                    break;
                case "smg.mp5":
                    _velocity -= (_velocity / 100f) * 20f;
                    break;
                case "rifle.bolt":
                    _velocity += (_velocity / 100f) * 75f;
                    break;
                case "rifle.l96":
                    _velocity += (_velocity / 100f) * 200f;
                    break;
                case "rifle.m39":
                    _velocity += (_velocity / 100f) * 25f;
                    break;
                case "lmg.m249":
                    _velocity += (_velocity / 100f) * 30f;
                    break;
                case "crossbow":
                    _velocity += (_velocity / 100f) * 50f;
                    break;
                case "bow.compound":
                    Projectile.Modifier pm = projectile.GetProjectileModifier();
					//SendReply(player, $"bow.compound pm.distanceScale: {pm.distanceScale}, _velocity * pm.distanceScale: {_velocity * pm.distanceScale}, damageScale: {pm.damageScale}, damageOffset: {pm.damageOffset}, distanceOffset: {pm.distanceOffset}");
                    _velocity *= Mathf.Clamp(pm.distanceScale, 1f, 2f);
                    break;
            }

            if (player.modelState.aiming)
                NRProbabilityModifier -= 0.05f;

            if (player.IsDucked())
                NRProbabilityModifier -= 0.05f;

            FiredProjectile fp = new FiredProjectile();

            int projectileID = projectileShoot.projectiles[0].projectileID;

            if (!PlayersFiredProjectlesData[player.userID].firedProjectiles.ContainsKey(projectileID))
                PlayersFiredProjectlesData[player.userID].firedProjectiles.Add(projectileID, new FiredProjectile());
			else
			{
				Puts($"Error: OnWeaponFired(), duplicate projectileID ({projectileID}), player ({player.displayName}/{player.userID})");
				return;
			}	
			
			fp.firedTime = DateTime.Now;
            fp.projectileVelocity = projectileShoot.projectiles[0].startVel.normalized * _velocity;		
            fp.projectilePosition = projectileShoot.projectiles[0].startPos;
            fp.playerEyesLookAt = player.eyes.HeadForward();
            fp.playerEyesPosition = player.eyes.position;
            //	fp.playerEyesPosition = player.eyes.position - player.eyes.HeadForward().normalized * (_config.playerEyesPositionToProjectileInitialPositionDistanceForgiveness * 2f); //uncomment this line to get AIMBOT false positives for testing purposes
            fp.weaponShortName = item.info.shortname;
            fp.weaponUID = (uint)item.uid.Value;
            fp.ammoShortName = projectile.primaryMagazine.ammoType.shortname;
            fp.NRProbabilityModifier = NRProbabilityModifier;
            fp.attachments = attachments;

            if (player.GetParentEntity() != null)
            {
                fp.isMounted = true;
                BaseEntity parentEntity = player.GetParentEntity();
                fp.mountParentName = parentEntity.ShortPrefabName;
                fp.mountParentPosition = parentEntity.ServerPosition;
				fp.mountParentRotation.x = parentEntity.ServerRotation.x;
				fp.mountParentRotation.y = parentEntity.ServerRotation.y;
				fp.mountParentRotation.z = parentEntity.ServerRotation.z;
				fp.mountParentRotation.w = parentEntity.ServerRotation.w;
            }
			else				
				if (player.isMounted)
				{
					BaseMountable parentMount = player.GetMounted(); 
					if (parentMount != null)
					{
						BaseEntity _parentEntity = parentMount.GetParentEntity();
						if (_parentEntity != null)
						{
							fp.isMounted = true;
                            fp.mountParentName = _parentEntity.ShortPrefabName;
                            fp.mountParentPosition = _parentEntity.ServerPosition;
							fp.mountParentRotation.x = _parentEntity.ServerRotation.x;
							fp.mountParentRotation.y = _parentEntity.ServerRotation.y;
							fp.mountParentRotation.z = _parentEntity.ServerRotation.z;
							fp.mountParentRotation.w = _parentEntity.ServerRotation.w;
						}
					}
				}
			
            PlayersFiredProjectlesData[player.userID].firedProjectiles[projectileID] = fp;

            PlayersFiredProjectlesData[player.userID].lastFiredTime = UnityEngine.Time.realtimeSinceStartup;
            PlayersFiredProjectlesData[player.userID].isChecked = false;

            if (_config.isDetectNR)
				if (_config.checkBlacklist)
				{
					if (permission.UserHasPermission(player.UserIDString, permNRBlacklist))
						timer.Once(_config.NRProcessTimer, () => ProcessShots(player));
				}
				else
					if (!permission.UserHasPermission(player.UserIDString, permNRWhitelist))
                        timer.Once(_config.NRProcessTimer, () => ProcessShots(player));

            timer.Once(9f, () => CleanupExpiredProjectiles(player));
        }

        private void OnEntityTakeDamage(BasePlayer entity, HitInfo info)
        {
            if (_config.isDetectAIM || _config.isDetectIR)
            {				
                if (entity == null) return;
				
                if (info == null) return;
				
                if (info.Initiator == null) return;
				
                if (!(info.Initiator is BasePlayer)) return;
				
                BasePlayer attacker = info.Initiator.ToPlayer();
				
                if (attacker == null) return;
				
                if (attacker is NPCPlayer) return;
				
                if (IsNPC(attacker)) return;

				if (_config.checkBlacklist)
				{
					if (!permission.UserHasPermission(attacker.UserIDString, permAIMBlacklist))
						return;
				}
				else
					if (permission.UserHasPermission(attacker.UserIDString, permAIMWhitelist))
                        return;

                if (!PlayersFiredProjectlesData.ContainsKey(attacker.userID))
                    return;

                if (info.HitBone == null) return;
				
                var _bodyPart = entity?.skeletonProperties?.FindBone(info.HitBone)?.name?.english ?? "";

                if (_bodyPart == null) return;
				
				bool isAIMBodyPart = _config.AIMBodyParts.Contains(_bodyPart);
				bool isIRBodyPart = _config.IRBodyParts.Contains(_bodyPart);
				
				if (isAIMBodyPart || isIRBodyPart)
                {
					if (info.HitEntity == null) return; 
					
                    BasePlayer target = info.HitEntity.ToPlayer();
					
                    if (target == null) return;
					
					if (!_config.isCheckAIMOnTargetNPC)
					{					
						if (target is NPCPlayer) return;
						if (IsNPC(target)) return;
					}
					
                    if (info.Weapon == null) return;

                    BaseMelee component = info.Weapon.GetComponent<BaseMelee>();
                    FiredProjectile fp = new FiredProjectile();
					bool isAttackerMount = false;
					bool isTargetMount = false;
					bool isMelee = false;
                    string attackerMountParentName = "";
					string targetMountParentName = "";

                    if (attacker.GetParentEntity() != null)
                    {
                        isAttackerMount = true;
                        BaseEntity attackerParentEntity = attacker.GetParentEntity();
                        attackerMountParentName = attackerParentEntity.ShortPrefabName;
                    }
					else				
						if (attacker.isMounted)
						{
							BaseMountable parentMount = attacker.GetMounted(); 
							if (parentMount != null)
							{
								BaseEntity _parentEntity = parentMount.GetParentEntity();
								if (_parentEntity != null)
								{
									isAttackerMount = true;
									attackerMountParentName = _parentEntity.ShortPrefabName;
								}
							}
						}

                    if (entity.GetParentEntity() != null)
                    {
                        isTargetMount = true;
                        BaseEntity targetParentEntity1 = entity.GetParentEntity();
                        targetMountParentName = targetParentEntity1.ShortPrefabName;
                    }
					else				
						if (entity.isMounted)
						{
							BaseMountable parentMount1 = entity.GetMounted(); 
							if (parentMount1 != null)
							{
								BaseEntity _parentEntity1 = parentMount1.GetParentEntity();
								if (_parentEntity1 != null)
								{
									isAttackerMount = true;
									attackerMountParentName = _parentEntity1.ShortPrefabName;
								}
							}
						}

					if (info.ProjectileID == 0)
					{
					//	Puts($"Error: info.ProjectileID = {info.ProjectileID} is zero. Attacker {attacker.UserIDString}/{attacker.userID} ");
					}
                    
					if (component != null)
                    {
						isMelee = true;

                        MeleeThrown _melee;

                        if (PlayersFiredProjectlesData[attacker.userID].melees.TryGetValue(info.Weapon.ownerItemUID.Value, out _melee))
                        {
                            fp.firedTime = _melee.firedTime;
                            //	fp.projectileVelocity = Vector3.Normalize(_melee.playerEyesLookAt) * _melee.projectileVelocity;
                            fp.projectileVelocity = Vector3.Normalize(attacker.firedProjectiles[info.ProjectileID].initialVelocity) * _melee.projectileVelocity;
                            //	fp.projectilePosition = _melee.position;
                            fp.projectilePosition = attacker.firedProjectiles[info.ProjectileID].initialPosition;
                            fp.playerEyesPosition = _melee.playerEyesPosition;
                            fp.playerEyesLookAt = _melee.playerEyesLookAt;
                            fp.ammoShortName = _melee.meleeShortName;
                            fp.weaponShortName = _melee.meleeShortName;
                            fp.weaponUID = _melee.meleeUID;
                            fp.isMounted = _melee.isMounted;
                            fp.mountParentName = _melee.mountParentName;
                            fp.mountParentPosition = _melee.mountParentPosition;
                            fp.mountParentRotation = _melee.mountParentRotation;
                        }
                        else
						{
						//	Puts($"Error: Melee info.ProjectileID = {info.ProjectileID} not found. Attacker {attacker.UserIDString}/{attacker.userID} ");
                            return;
						}
                    }
                    else
                        if (!PlayersFiredProjectlesData[attacker.userID].firedProjectiles.TryGetValue(info.ProjectileID, out fp))
						{
						//	Puts($"Error: Projectile info.ProjectileID = {info.ProjectileID} not found. Attacker {attacker.UserIDString}/{attacker.userID} ");
							return;
						}
					
					if (_config.isDetectIR && isIRBodyPart)
						if (!isAttackerMount && fp.hitsData.Count <= 1)
							ShootingInRockCheck(attacker, fp, info, _bodyPart, PlayersFiredProjectlesData[attacker.userID].physicsSteps);	
						
					if (_config.isDetectAIM && isAIMBodyPart && info.Initiator.Distance(entity.transform.position) > _config.minDistanceAIMCheck)
					{
						if (fp.hitsData.Count > 1) return; // Temporary fix for 5th March update
						
						AIMViolationData aimvd = new AIMViolationData();
						bool AIMViolation = false;
						List<TrajectorySegment> trajectorySegments = new List<TrajectorySegment>();
						List<TrajectorySegment> trajectorySegmentsRev = new List<TrajectorySegment>();
						
						if (isMelee)
							aimvd.forgivenessModifier = 1.5f;

                        if (attacker.HasFiredProjectile(info.ProjectileID))
                        {
                            aimvd.firedProjectileFiredTime = attacker.firedProjectiles[info.ProjectileID].firedTime;
                            aimvd.firedProjectileTravelTime = attacker.firedProjectiles[info.ProjectileID].travelTime;
                            aimvd.firedProjectilePosition = attacker.firedProjectiles[info.ProjectileID].position;
                            aimvd.firedProjectileVelocity = attacker.firedProjectiles[info.ProjectileID].velocity;
                            aimvd.firedProjectileInitialPosition = attacker.firedProjectiles[info.ProjectileID].initialPosition;
                            aimvd.firedProjectileInitialVelocity = attacker.firedProjectiles[info.ProjectileID].initialVelocity;
                            aimvd.hasFiredProjectile = true;
                
                            if (fp.weaponShortName == "bow.compound")
                                if (attacker.firedProjectiles[info.ProjectileID].initialVelocity.magnitude - fp.projectileVelocity.magnitude < (attacker.firedProjectiles[info.ProjectileID].initialVelocity.magnitude / 100f) * 10f)
                                    fp.projectileVelocity = attacker.firedProjectiles[info.ProjectileID].initialVelocity;
                
                            if (!(attacker.firedProjectiles[info.ProjectileID].initialPosition == fp.projectilePosition) && !(attacker.firedProjectiles[info.ProjectileID].initialVelocity == fp.projectileVelocity))
                            {
                                aimvd.isEqualFiredProjectileData = false;
                                AIMViolation = true;
                            }
                        }
                        else
                        {
                            Puts(Lang("ErrorText2", null, attacker.displayName, target.displayName, info.ProjectileID));
                            return;
                        }
                
                        if (fp.hitsData.Count > 0 && fp.hitsData[fp.hitsData.Count - 1].hitPosition == info.HitPositionWorld)
                            fp.hitsData.RemoveAt(fp.hitsData.Count - 1);
                
                        HitData hitData = new HitData();
                        hitData.startProjectilePosition = fp.projectilePosition;
                        hitData.startProjectileVelocity = fp.projectileVelocity;
                        hitData.hitPositionWorld = info.HitPositionWorld;
                        hitData.hitPointStart = info.PointStart;
                        hitData.hitPointEnd = info.PointEnd;
                
                        aimvd.hitsData.Add(hitData);
                
                        if (fp.hitsData.Count > 0)
                            for (int i = 0; i < fp.hitsData.Count; i++)
                            {
                                hitData = new HitData();
                                hitData.hitData = fp.hitsData[i];
                
                                hitData.startProjectilePosition = fp.hitsData[i].hitPosition;
                                hitData.startProjectileVelocity = fp.hitsData[i].outVelocity;
                                hitData.hitPositionWorld = info.HitPositionWorld;
                                hitData.hitPointStart = info.PointStart;
                                hitData.hitPointEnd = info.PointEnd;
                                aimvd.hitsData.Add(hitData);
                                aimvd.hitsData[i].hitPositionWorld = fp.hitsData[i].hitPosition;
                                aimvd.hitsData[i].hitPointStart = fp.hitsData[i].hitPosition - (fp.hitsData[i].inVelocity / PlayersFiredProjectlesData[attacker.userID].physicsSteps);
                                aimvd.hitsData[i].hitPointEnd = fp.hitsData[i].hitPosition;
                            }
							
						if (fp.ammoShortName.Contains("arrow."))
							aimvd.forgivenessModifier = 1.5f;
						
						aimvd.physicsSteps = PlayersFiredProjectlesData[attacker.userID].physicsSteps;
						
                        AIMViolation = ProcessProjectileTrajectory(out aimvd, aimvd, out trajectorySegments, out trajectorySegmentsRev, info.ProjectilePrefab.gravityModifier, info.ProjectilePrefab.drag);
						
						if (AIMViolation && aimvd.hitsData.Count == 1 && trajectorySegments.Count > 0 && trajectorySegmentsRev.Count > 0)
						{
							float lengthLastSegmentProjectileTrajectory = Vector3.Distance(aimvd.hitsData[0].lastSegmentPointEnd, aimvd.hitsData[0].lastSegmentPointStart);
							float lengthLastSegmentReverseProjectileTrajectory = Vector3.Distance(aimvd.hitsData[0].hitPointEnd, aimvd.hitsData[0].hitPointStart);
							
							Vector3 pointStartProjectedOnLastSegment = ProjectPointOnLine(aimvd.hitsData[0].lastSegmentPointStart, (aimvd.hitsData[0].lastSegmentPointEnd - aimvd.hitsData[0].lastSegmentPointStart).normalized, aimvd.hitsData[0].hitPointStart);
							Vector3 pointEndProjectedOnLastSegment = ProjectPointOnLine(aimvd.hitsData[0].lastSegmentPointStart, (aimvd.hitsData[0].lastSegmentPointEnd - aimvd.hitsData[0].lastSegmentPointStart).normalized, aimvd.hitsData[0].hitPointEnd);
                
							if (Mathf.Abs(Vector3.Distance(pointStartProjectedOnLastSegment, aimvd.hitsData[0].hitPointStart) - Vector3.Distance(pointEndProjectedOnLastSegment, aimvd.hitsData[0].hitPointEnd)) > 0.05f)
							{
								HitData hitData1 = new HitData();
								HitData hitData2 = new HitData();
								if (IsRicochet(trajectorySegments, trajectorySegmentsRev, out hitData1, out hitData2, aimvd.physicsSteps))
								{
									hitData1.startProjectilePosition = aimvd.hitsData[0].startProjectilePosition;
									hitData1.startProjectileVelocity = aimvd.hitsData[0].startProjectileVelocity;
                        
									hitData2.hitPositionWorld = info.HitPositionWorld;
									hitData2.hitPointStart = info.PointStart;
									hitData2.hitPointEnd = info.PointEnd;	
														
									aimvd.hitsData.Clear();
							
									aimvd.hitsData.Add(hitData1);
									aimvd.hitsData.Add(hitData2);
                      
									AIMViolation = ProcessProjectileTrajectory(out aimvd, aimvd, out trajectorySegments, out trajectorySegmentsRev, info.ProjectilePrefab.gravityModifier, info.ProjectilePrefab.drag);
								}							
							}
						}
						
						if (!AIMViolation && Mathf.Abs(PlayersFiredProjectlesData[attacker.userID].physicsSteps - aimvd.physicsSteps) > PlayersFiredProjectlesData[attacker.userID].physicsSteps * 0.063f)
						{
							if (_config.notifyPhysicsStepsWarning)
							{
								if (aimvd.physicsSteps > _config.minPhysicsStepsAllowed)
								{
									if (_config.DiscordAIMReportEnabled)
										if (DiscordMessages == null)
											PrintWarning(Lang("DiscordWarning2", null));
										else
										{
											List<EmbedFieldList> fields = new List<EmbedFieldList>();
											
											string dmAttacker = $"[{attacker.displayName}\n{attacker.UserIDString}](https://steamcommunity.com/profiles/{attacker.UserIDString})";
											if (dmAttacker.Length == 0) dmAttacker = stringNullValueWarning;
											fields.Add(new EmbedFieldList()
											{
												name = Lang("PlayerTxt", null),
												inline = true,
												value = dmAttacker
											});
											
											string dmPhysicsSteps = $"{aimvd.physicsSteps}({Mathf.Round(aimvd.physicsSteps)})";
											if (dmPhysicsSteps.Length == 0) dmPhysicsSteps = stringNullValueWarning;
											fields.Add(new EmbedFieldList()
											{
												name = "physics.steps",
												inline = true,
												value = dmPhysicsSteps
											});	
                        
											string dmDescription = Lang("PhysicsStepsChangeWarning", null, attacker.displayName + "/" + attacker.UserIDString, aimvd.physicsSteps, Mathf.Round(aimvd.physicsSteps));
											if (dmDescription.Length == 0) dmDescription = stringNullValueWarning;
											fields.Add(new EmbedFieldList()
											{
												name = Lang("Description", null),
												inline = false,
												value = dmDescription
											});	
                        
											var fieldsObject = fields.Cast<object>().ToArray();
											
											string json = JsonConvert.SerializeObject(fieldsObject);

											DiscordMessages?.Call("API_SendFancyMessage", _config.DiscordAIMWebhookURL, "Arkan: " + Lang("HighPhysicsStepsDetection", null), 39423, json);
										}
										
									foreach (var _player in BasePlayer.activePlayerList.Where(x => permission.UserHasPermission(x.UserIDString, permName) && x.IsAdmin))
									{	
										if (permission.UserHasPermission(_player.UserIDString, permAIMReportChat))
											SendReply(_player, "<color=green>Arkan: </color>" + "<color=red>" + Lang("HighPhysicsStepsDetection", _player.UserIDString) + "\n" + Lang("PhysicsStepsChangeWarning", null, "<color=yellow>" + attacker.displayName + "/" + attacker.UserIDString + "</color>", $"<color=yellow>{aimvd.physicsSteps}</color>", $"<color=yellow>{Mathf.Round(aimvd.physicsSteps)}</color>") + "</color>");
										
										if (permission.UserHasPermission(_player.UserIDString, permAIMReportConsole))
											_player.ConsoleMessage("<color=green>Arkan: </color>" + "<color=red>" + Lang("HighPhysicsStepsDetection", _player.UserIDString) + "</color>\n" + Lang("PhysicsStepsChangeWarning", null, attacker.displayName + "/" + attacker.UserIDString, aimvd.physicsSteps, Mathf.Round(aimvd.physicsSteps)));
									}
								
									Puts(Lang("PhysicsStepsChangeWarning", null, attacker.displayName + "/" + attacker.UserIDString, aimvd.physicsSteps, Mathf.Round(aimvd.physicsSteps)));
								}
							}													
							PlayersFiredProjectlesData[attacker.userID].physicsSteps = aimvd.physicsSteps;
						}
						
                        if (Vector3.Distance(fp.playerEyesPosition, fp.projectilePosition) > _config.playerEyesPositionToProjectileInitialPositionDistanceForgiveness)// && !fp.isMounted)
                        {
                            AIMViolation = true;
                            aimvd.isPlayerPositionToProjectileStartPositionDistanceViolation = true;
                            aimvd.distanceDifferenceViolation = Vector3.Distance(fp.playerEyesPosition, fp.projectilePosition);
                        }
                
                        if (AIMViolation)
                        {
                            aimvd.projectileID = info.ProjectileID;
							aimvd.firedTime = fp.firedTime;
                            aimvd.startProjectilePosition = fp.projectilePosition;
                            aimvd.startProjectileVelocity = fp.projectileVelocity;
                            aimvd.weaponShortName = fp.weaponShortName;
                            aimvd.ammoShortName = fp.ammoShortName;
                            aimvd.hitInfoInitiatorPlayerName = info.Initiator.ToPlayer().displayName;
                            aimvd.hitInfoInitiatorPlayerUserID = info.Initiator.ToPlayer().userID.ToString();
                            aimvd.hitInfoHitEntityPlayerName = info.HitEntity.ToPlayer().displayName;
                            aimvd.hitInfoHitEntityPlayerUserID = info.HitEntity.ToPlayer().userID.ToString();
                            aimvd.hitInfoBoneName = info.boneName;
                            aimvd.hitInfoHitPositionWorld = info.HitPositionWorld;
                            aimvd.hitInfoProjectileDistance = info.ProjectileDistance;
                            aimvd.hitInfoPointStart = info.PointStart;
                            aimvd.hitInfoPointEnd = info.PointEnd;
                            aimvd.hitInfoProjectilePrefabGravityModifier = info.ProjectilePrefab.gravityModifier;
                            aimvd.hitInfoProjectilePrefabDrag = info.ProjectilePrefab.drag;
                            aimvd.isAttackerMount = isAttackerMount;
                            aimvd.isTargetMount = isTargetMount;
                            aimvd.attackerMountParentName = attackerMountParentName;
                            aimvd.targetMountParentName = targetMountParentName;
                            aimvd.bodyPart = _bodyPart;
                            aimvd.damage = info.damageTypes.Total();
                            aimvd.gravityModifier = info.ProjectilePrefab.gravityModifier;
                            aimvd.drag = info.ProjectilePrefab.drag;
                            aimvd.playerEyesLookAt = fp.playerEyesLookAt;
                            aimvd.playerEyesPosition = fp.playerEyesPosition;
							aimvd.attachments = fp.attachments;
							
                            if (PlayersViolations.Players.ContainsKey(attacker.userID))
                                aimvd.violationID = PlayersViolations.Players[attacker.userID].AIMViolations.Count + 1;
                            else
                                aimvd.violationID = 1;
                
                            AddAIMViolationToPlayer(attacker, aimvd);
							
							int AIMViolationsCnt = PlayersViolations.Players[attacker.userID].AIMViolations.Count;
				
							if (Interface.CallHook("API_ArkanOnAimbotViolation", attacker, AIMViolationsCnt, JsonConvert.SerializeObject(aimvd)) != null)
							{
								return;
							}
                
							foreach (var _player in BasePlayer.activePlayerList.Where(x => permission.UserHasPermission(x.UserIDString, permName) && x.IsAdmin))
							{	
								if (permission.UserHasPermission(_player.UserIDString, permAIMReportChat))
									SendReply(_player, Lang("PlayerAIMViolation", _player.UserIDString, attacker.displayName, attacker.userID, PlayersViolations.Players[attacker.userID].AIMViolations.Count, target.displayName, target.userID));
								
								if (permission.UserHasPermission(_player.UserIDString, permAIMReportConsole))
									_player.ConsoleMessage("<color=green>Arkan:</color> " + Lang("PlayerAIMViolationCon", _player.UserIDString, attacker.displayName, attacker.userID, PlayersViolations.Players[attacker.userID].AIMViolations.Count, target.displayName, target.userID));
							}
							
                            if (_config.debugMessages)
                            {
                                string txt = Lang("AIMText1", null);
								string txt1 = "";
								string logTxt = "";
								Dictionary<int, string> logList = new Dictionary<int, string>();
                
                                txt += Lang("Attacker", null) + ": " + (aimvd.hitInfoInitiatorPlayerName ?? aimvd.hitInfoInitiatorPlayerUserID) + "\n" + Lang("AIMViolationNum", null) + PlayersViolations.Players[attacker.userID].AIMViolations.Count + "\n" + Lang("Weapon", null) + ": " + aimvd.weaponShortName + "\n" + Lang("Ammo", null) + ": " + aimvd.ammoShortName + "\n" + Lang("Distance", null) + ": " + aimvd.hitInfoProjectileDistance + "\n";
                                txt += Lang("Target", null) + ": " + (aimvd.hitInfoHitEntityPlayerName ?? aimvd.hitInfoHitEntityPlayerUserID) + "\n" + Lang("HitPart", null) + ": " + aimvd.hitInfoBoneName + "\n";
								txt += Lang("DateTime", null) + ": " + aimvd.firedTime + "\n\n";
								
								if (aimvd.isAttackerMount && aimvd.attackerMountParentName != null)
									txt += Lang("MountedOn", null, aimvd.attackerMountParentName) + "\n\n";
								
								txt += Lang("AttachmentsCount", null) + " = " + aimvd.attachments.Count + "\n";
                
								if (aimvd.attachments.Count > 0)
									for (int ii = 0; ii < aimvd.attachments.Count; ii++)
										txt += Lang("Attachment", null) + " - " + aimvd.attachments[ii] + "\n";
									
                                txt += Lang("RicochetsCount", null) + " = " + (aimvd.hitsData.Count - 1) + "\n";
                                txt += $"isEqualFiredProjectileData = {aimvd.isEqualFiredProjectileData}\n";
                                txt += $"isPlayerPositionToProjectileStartPositionDistanceViolation = {aimvd.isPlayerPositionToProjectileStartPositionDistanceViolation}\n";
								
								logTxt = $"isEqualFiredProjectileData = {aimvd.isEqualFiredProjectileData}\nisPlayerPositionToProjectileStartPositionDistanceViolation = {aimvd.isPlayerPositionToProjectileStartPositionDistanceViolation}\n";
                                
								if (aimvd.isPlayerPositionToProjectileStartPositionDistanceViolation)
								{
                                    txt += Lang("LogText6", null, aimvd.distanceDifferenceViolation);
									logTxt += Lang("LogText6", null, aimvd.distanceDifferenceViolation);
								}
                
                                for (int j = 0; j < aimvd.hitsData.Count; j++)
                                {
                                    txt += $"-\n" + Lang("LogText7", null, j + 1) + "\n";
                                    txt1 = $"isHitPointNearProjectileTrajectoryLastSegmentEndPoint = {aimvd.hitsData[j].isHitPointNearProjectileTrajectoryLastSegmentEndPoint}\n";
                                    
									if (!aimvd.hitsData[j].isHitPointNearProjectileTrajectoryLastSegmentEndPoint && aimvd.hitsData[j].side > 0)
                                        if (aimvd.hitsData[j].side == 1)
                                            txt1 += "     " + Lang("AIMText4", null, aimvd.hitsData[j].distanceFromHitPointToProjectilePlane, Lang("AIMText2", null), "StartPoint", Vector3.Distance(aimvd.hitsData[j].hitPositionWorld, aimvd.hitsData[j].hitPointStart)) + "\n";
                                        else
                                            txt1 += "     " + Lang("AIMText4", null, aimvd.hitsData[j].distanceFromHitPointToProjectilePlane, Lang("AIMText3", null), "EndPoint", Vector3.Distance(aimvd.hitsData[j].hitPositionWorld, aimvd.hitsData[j].hitPointEnd)) + "\n";
                                    
									txt1 += $"isHitPointOnProjectileTrajectory = {aimvd.hitsData[j].isHitPointOnProjectileTrajectory}\n";
                
                                    txt1 += $"isProjectileStartPointAtEndReverseProjectileTrajectory = {aimvd.hitsData[j].isProjectileStartPointAtEndReverseProjectileTrajectory}\n";
                                    
									if (!aimvd.hitsData[j].isProjectileStartPointAtEndReverseProjectileTrajectory)
                                        txt1 += "     " + Lang("AIMText6", null, aimvd.hitsData[j].lastSegmentPointStart, aimvd.hitsData[j].lastSegmentPointEnd, aimvd.hitsData[j].startProjectilePosition, aimvd.hitsData[j].startProjectilePosition + aimvd.hitsData[j].startProjectileVelocity);
                
                                    txt1 += $"isHitPointNearProjectilePlane = {aimvd.hitsData[j].isHitPointNearProjectilePlane}\n";
                                    
									if (!aimvd.hitsData[j].isHitPointNearProjectilePlane)
                                        txt1 += "     " + Lang("AIMText7", null, aimvd.hitsData[j].distanceFromHitPointToProjectilePlane);
                
                                    txt1 += $"isLastSegmentOnProjectileTrajectoryPlane = {aimvd.hitsData[j].isLastSegmentOnProjectileTrajectoryPlane}\n";
                                    
									if (!aimvd.hitsData[j].isLastSegmentOnProjectileTrajectoryPlane)
                                        txt1 += "     " + Lang("AIMText8", null, aimvd.hitsData[j].lastSegmentPointStart, aimvd.hitsData[j].startProjectilePosition, aimvd.hitsData[j].startProjectilePosition + aimvd.hitsData[j].startProjectileVelocity);
                
                                    txt += txt1 + Lang("LogText8", null, j + 1) + "\n";
									
									logList.Add(logList.Count, txt1);
                                }
                
                                Puts(txt);
								
								if (_config.DiscordAIMReportEnabled)
									if (DiscordMessages == null)
										PrintWarning(Lang("DiscordWarning2", null));
									else
									{
										List<EmbedFieldList> fields = new List<EmbedFieldList>();
										
										string dmAttacker = $"[{attacker.displayName}\n{attacker.UserIDString}](https://steamcommunity.com/profiles/{attacker.UserIDString})";
										if (dmAttacker.Length == 0) dmAttacker = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("Attacker", null),
											inline = true,
											value = dmAttacker
										});
										
										string dmAIMViolationNum = $"{PlayersViolations.Players[attacker.userID].AIMViolations.Count}";
										if (dmAIMViolationNum.Length == 0) dmAIMViolationNum = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("AIMViolationNum", null),
											inline = true,
											value = dmAIMViolationNum
										});	
										
										string dmDateTime = $"{aimvd.firedTime}";
										if (dmDateTime.Length == 0) dmDateTime = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("DateTime", null),
											inline = true,
											value = dmDateTime
										});	
                
										string dmWeapon = $"{aimvd.weaponShortName}";
										if (dmWeapon.Length == 0) dmWeapon = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("Weapon", null),
											inline = true,
											value = dmWeapon
										});	
                
										string dmAmmo = $"{aimvd.ammoShortName}";
										if (dmAmmo.Length == 0) dmAmmo = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("Ammo", null),
											inline = true,
											value = dmAmmo
										});
										
										if (aimvd.attachments.Count == 0)
										{
											fields.Add(new EmbedFieldList()
											{
												name = Lang("AttachmentsCount", null) + " = " + aimvd.attachments.Count,
												inline = true,
												value = Lang("NoAttachments", null)
											});
										}
										else
										{
											string dmAttachmentsList = "";
											for (int j = 0; j < aimvd.attachments.Count; j++)
												dmAttachmentsList += aimvd.attachments[j] + "\n";
											
											if (dmAttachmentsList.Length == 0) dmAttachmentsList = stringNullValueWarning;
											fields.Add(new EmbedFieldList()
											{
												name = Lang("AttachmentsCount", null) + " = " + aimvd.attachments.Count,
												inline = true,
												value = dmAttachmentsList
											});
										}
										
										string targetVal = "";
										if (IsNPC(target))
											targetVal = aimvd.hitInfoHitEntityPlayerName ?? aimvd.hitInfoHitEntityPlayerUserID;
										else
											targetVal = $"[{target.displayName}](https://steamcommunity.com/profiles/{target.UserIDString})";										
                
										if (targetVal.Length == 0) targetVal = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("Target", null),
											inline = true,
											value = targetVal
										});	
                
										string dmHitPart = $"{aimvd.hitInfoBoneName}";
										if (dmHitPart.Length == 0) dmHitPart = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("HitPart", null),
											inline = true,
											value = dmHitPart
										});											
                
										string dmDistance = $"{aimvd.hitInfoProjectileDistance}";
										if (dmDistance.Length == 0) dmDistance = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("Distance", null),
											inline = true,
											value = dmDistance
										});	
										
										string dmRicochetsCount = $"{aimvd.hitsData.Count - 1}";
										if (dmRicochetsCount.Length == 0) dmRicochetsCount = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("RicochetsCount", null),
											inline = false,
											value = dmRicochetsCount
										});	
										
										if (logTxt.Length == 0) logTxt = stringNullValueWarning;
										fields.Add(new EmbedFieldList()
										{
											name = Lang("VDataLog", null),
											inline = false,
											value = logTxt
										});		
										
										string dmLogData = "";
										for (int k = 0; k < logList.Count; k++)
										{
											dmLogData = logList[k];
											if (dmLogData.Length == 0) dmLogData = stringNullValueWarning;
											fields.Add(new EmbedFieldList()
											{
												name = Lang("LogText7", null, k + 1),
												inline = false,
												value = dmLogData
											});				
											
											fields.Add(new EmbedFieldList()
											{
												name = Lang("LogText8", null, k + 1),
												inline = false,
												value = "-"
											});			
										}
								
										var fieldsObject = fields.Cast<object>().ToArray();
										
										string json = JsonConvert.SerializeObject(fieldsObject);

										DiscordMessages?.Call("API_SendFancyMessage", _config.DiscordAIMWebhookURL, "Arkan: " + Lang("AIMDetection", null), 39423, json);
									}
                
                                foreach (var _player in BasePlayer.activePlayerList.Where(x => x.IsAdmin && permission.UserHasPermission(x.UserIDString, permName) && permission.UserHasPermission(x.UserIDString, permAIMDrawViolation)))
                                {
                                    DrawProjectileTrajectory(_player, _config.drawTime, aimvd, Color.blue);
                                    DrawReverseProjectileTrajectory(_player, _config.drawTime, aimvd, Color.green);
                
                                    if (aimvd.isPlayerPositionToProjectileStartPositionDistanceViolation)
                                    {
                                        DDrawSphereToAdmin(_player, _config.drawTime, Color.red, aimvd.playerEyesPosition, 0.05f);
                                        DDrawTextToAdmin(_player, _config.drawTime, Color.cyan, aimvd.playerEyesPosition + Vector3.up, Lang("DrawAIMVD5", null, aimvd.hitInfoInitiatorPlayerName ?? aimvd.hitInfoInitiatorPlayerUserID, aimvd.violationID, aimvd.playerEyesPosition, aimvd.startProjectilePosition, Vector3.Distance(aimvd.playerEyesPosition, aimvd.startProjectilePosition)));
										DDrawArrowToAdmin(_player, _config.drawTime, Color.red, aimvd.playerEyesPosition, aimvd.playerEyesPosition + aimvd.playerEyesLookAt.normalized, 0.05f);
									}
                                }
                            }
                        }
					}
                }               
            }
        }
		
        #endregion Hooks

        #region Config
		
		protected override void LoadConfig()
		{
		    base.LoadConfig();
		    try
		    {
		        _config = Config.ReadObject<Configuration>();
		        if (_config == null) throw new Exception();
		        SaveConfig();
            }
		    catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
            }
		}
        
		protected override void SaveConfig() => Config.WriteObject(_config);

		protected override void LoadDefaultConfig() => _config = new Configuration();	
		
        #endregion Config

        #region Commands

		[ConsoleCommand("arkan")]
		private void ConsoleShowLog(ConsoleSystem.Arg arg)
        {			
			var player = arg.Player();

			if (player == null)
			{
                if (PlayersViolations.Players.Count > 0)
                {
					string txtConsole = RemoveFormatting(Lang("ShowLog2", null));
					
                    foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
					{
						txtConsole += $"{PlayersViolations.Players[list.Key].PlayerName} -";
						
						if (PlayersViolations.Players[list.Key].noRecoilViolations.Count > 0)
							txtConsole += $" NR({PlayersViolations.Players[list.Key].noRecoilViolations.Count})";
						
						if (PlayersViolations.Players[list.Key].AIMViolations.Count > 0)
							txtConsole += $" AIM({PlayersViolations.Players[list.Key].AIMViolations.Count})";
						
						if (PlayersViolations.Players[list.Key].inRockViolations.Count > 0)
							txtConsole += $" IR({PlayersViolations.Players[list.Key].inRockViolations.Count})";

						txtConsole += "; ";
					}
					
					Puts(txtConsole);
                }
                else
					Puts(RemoveFormatting(Lang("ShowLog3")));
			}
		}
		
		[ConsoleCommand("arkan.nr")]
		private void ConsoleShowNoRecoilLog(ConsoleSystem.Arg arg)
        {			
			var player = arg.Player();
			string txtConsole = "";

			if (player == null)
			{
				if (!arg.HasArgs())
				{
					txtConsole = RemoveFormatting(Lang("ShowNRLog2", null));
                    int nrCnt = 0;
					
					if (PlayersViolations.Players.Count > 0)
					{
						foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players.Where(x => (PlayersViolations.Players[x.Key].noRecoilViolations.Count > 0)))
						{
							txtConsole += $"{PlayersViolations.Players[list.Key].PlayerName} - NR({PlayersViolations.Players[list.Key].noRecoilViolations.Count}); ";   
							
							nrCnt++;
						}

						Puts(txtConsole);
					}	
					
					if (nrCnt == 0)
                        Puts(RemoveFormatting(Lang("ShowNRLog3", null)));

					return;
				}
				
                string s = null;
				txtConsole = "";
                ulong id = 0;
				ulong playerID = 0;
        
                if (arg.Args.Length == 2)
                    if (arg.Args[1] == "0")
                    {
						txtConsole += Lang("ShowD1", null) + "\n";
						RconLog = new AdminConfig();
                    }
                    else
                        s = arg.Args[1];
        
                string user = arg.Args[0];
        
                if (user.Contains("765"))
                    ulong.TryParse(arg.Args[0], out id);
        
                foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
                    if (PlayersViolations.Players[list.Key].PlayerID == id || PlayersViolations.Players[list.Key].PlayerName.Contains(user, CompareOptions.IgnoreCase))
                        playerID = PlayersViolations.Players[list.Key].PlayerID;
        
                if (playerID == 0)
					txtConsole += Lang("ShowD2", null) + "\n";
                else
                {
                    if (PlayersViolations.Players[playerID].noRecoilViolations.Count == 0)
						txtConsole += Lang("ShowNRD1", null, PlayersViolations.Players[playerID].PlayerName) + "\n";
                    else
                    {
						if ((long)RconLog.violationsLog.steamID != (long)PlayersViolations.Players[playerID].PlayerID)
                        {
							RconLog.violationsLog.NoRecoilViolation = 1;
							RconLog.violationsLog.steamID = PlayersViolations.Players[playerID].PlayerID;
                        }
                        else
                            if (s == null)
								if (PlayersViolations.Players[playerID].noRecoilViolations.Count >= RconLog.violationsLog.NoRecoilViolation + 1)
									RconLog.violationsLog.NoRecoilViolation++;
                                else
                                {
									txtConsole += Lang("NoMoreViolations", null, PlayersViolations.Players[playerID].PlayerName) + "\n";
									RconLog.violationsLog = new ViolationsLog();
									Puts(txtConsole);
                                    return;
                                }
        
                        int result;
                        int.TryParse(s, out result);
        
                        if (result == 0)
                            result = RconLog.violationsLog.NoRecoilViolation;
        
                        int i = 1;
                        foreach (KeyValuePair<string, NoRecoilViolationData> list in PlayersViolations.Players[playerID].noRecoilViolations)
                        {
                            if (i == result)
                            {
                                NoRecoilViolationData violationData = PlayersViolations.Players[playerID].noRecoilViolations[list.Key];

								txtConsole += "\n" + Lang("PlayerTxt", null) + " " + PlayersViolations.Players[playerID].PlayerName + "\n" + Lang("NRViolationNum", null) + result + "\n" + Lang("ShotsCount", null) + " " + violationData.ShotsCnt + "\n" + Lang("Probability", null) + " " + violationData.violationProbability + "%\n";
								if (violationData.suspiciousNoRecoilShots.ContainsKey(1))
									txtConsole += Lang("DateTime", null) + ": " + violationData.suspiciousNoRecoilShots[1].timeStamp + "\n\n";
								txtConsole += Lang("AttachmentsCount", null) + " = " + violationData.attachments.Count + "\n";
        
                                if (violationData.attachments.Count > 0)
                                    for (int ii = 0; ii < violationData.attachments.Count; ii++)
										txtConsole += Lang("Attachment", null) + " - " + violationData.attachments[ii] + "\n";

								txtConsole += Lang("Weapon", null) + " - " + violationData.weaponShortName + "\n";
								txtConsole += Lang("Ammo", null) + " - " + violationData.ammoShortName + "\n";
								txtConsole += Lang("Probability", null) + " - " + violationData.violationProbability + "%\n";
        
                                int j = 1;
								
                                foreach (KeyValuePair<int, SuspiciousProjectileData> suspiciusProjectile in violationData.suspiciousNoRecoilShots.ToArray())
                                {
                                    SuspiciousProjectileData sp = violationData.suspiciousNoRecoilShots[suspiciusProjectile.Key];
                                    if (sp.isNoRecoil)
                                    {
                                        if (sp.isShootedInMotion)
											txtConsole += Lang("ProjectileID", null, sp.projectile2ID) + " | " + Lang("ShootingOnMove", null) +  " | " + Lang("ClosestPoint", null, sp.closestPointLine1, sp.closestPointLine2, sp.prevIntersectionPoint) + " | " + Lang("FireTimeInterval", null, sp.timeInterval) + "\n";
                                        else
											txtConsole += Lang("ProjectileID", null, sp.projectile2ID) + " | " + Lang("StandingShooting", null) + " | " + Lang("RecoilAngle", null, sp.recoilAngle) + " | " + Lang("FireTimeInterval", null, sp.timeInterval) + "\n";
                                    }
                                    j++;
                                }

								txtConsole += "\n\n.";
                                break;
                            }
                            i++;
                        }
                    }
                }
				Puts(txtConsole);
			}
		}

		[ConsoleCommand("arkan.aim")]
		private void ConsoleShowAIMLog(ConsoleSystem.Arg arg)
        {			
			var player = arg.Player();
			string txtConsole = "";

			if (player == null)
			{
				if (!arg.HasArgs())
				{
					txtConsole = RemoveFormatting(Lang("ShowNRLog2", null));
                    int aimCnt = 0;
					
					if (PlayersViolations.Players.Count > 0)
					{
						foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players.Where(x => (PlayersViolations.Players[x.Key].AIMViolations.Count > 0)))
						{
							txtConsole += $"{PlayersViolations.Players[list.Key].PlayerName} - AIM({PlayersViolations.Players[list.Key].AIMViolations.Count}); ";   
							
							aimCnt++;
						}

						Puts(txtConsole);
					}	
					
					if (aimCnt == 0)
                        Puts(RemoveFormatting(Lang("ShowAIMLog3", null)));

					return;
				}
				
                string s = null;
				txtConsole = "";
                ulong id = 0;
				ulong playerID = 0;
        
                if (arg.Args.Length == 2)
                {
                    if (arg.Args[1] == "0")
                    {
						txtConsole += Lang("ShowD1", null) + "\n";
						RconLog.violationsLog = new ViolationsLog();
                    }
                    else
                        s = arg.Args[1];
                }
        
                string user = arg.Args[0];
        
                if (user.Contains("765"))
                    ulong.TryParse(arg.Args[0], out id);
        
                foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
                    if (PlayersViolations.Players[list.Key].PlayerID == id || PlayersViolations.Players[list.Key].PlayerName.Contains(user, CompareOptions.IgnoreCase))
                        playerID = PlayersViolations.Players[list.Key].PlayerID;

                if (playerID == 0)
					txtConsole += Lang("ShowD2", null) + "\n";
                else
                    if (PlayersViolations.Players[playerID].AIMViolations.Count == 0)
						txtConsole += Lang("ShowAIMD1", null, PlayersViolations.Players[playerID].PlayerName) + "\n";
                else
                {
					if ((long)RconLog.violationsLog.steamID != (long)PlayersViolations.Players[playerID].PlayerID)
                    {
						RconLog.violationsLog.AIMViolation = 1;
						RconLog.violationsLog.steamID = PlayersViolations.Players[playerID].PlayerID;
                    }
                    else if (s == null)
                        if (PlayersViolations.Players[playerID].AIMViolations.Count >= RconLog.violationsLog.AIMViolation + 1)
							RconLog.violationsLog.AIMViolation++;
                        else
                        {
							txtConsole += Lang("ShowD3", null, PlayersViolations.Players[playerID].PlayerName) + "\n";
							RconLog.violationsLog = new ViolationsLog();
							Puts(txtConsole);
                            return;
                        }
        
                    int result;
                    int.TryParse(s, out result);
        
                    if (result == 0)
						result = RconLog.violationsLog.AIMViolation;
        
                    int i = 1;
                    
					foreach (KeyValuePair<string, AIMViolationData> list in PlayersViolations.Players[playerID].AIMViolations)
                    {
                        if (i == result)
                        {
							AIMViolationData violationData = PlayersViolations.Players[playerID].AIMViolations[list.Key];

							txtConsole += Lang("Attacker", null) + ": " + (violationData.hitInfoInitiatorPlayerName ?? violationData.hitInfoInitiatorPlayerUserID) + "\n" + Lang("AIMViolationNum", null) + violationData.violationID + "\n" + Lang("Weapon", null) + ": " + violationData.weaponShortName + "\n" + Lang("Ammo", null) + ": " + violationData.ammoShortName + "\n" + Lang("Distance", null) + ": " + violationData.hitInfoProjectileDistance + "\n";
							txtConsole += Lang("Target", null) + ": " + (violationData.hitInfoHitEntityPlayerName ?? violationData.hitInfoHitEntityPlayerUserID) + "\n" + Lang("HitPart", null) + ": " + violationData.hitInfoBoneName + "\n";
							txtConsole += Lang("DateTime", null) + ": " + violationData.firedTime + "\n\n";
							
							txtConsole += Lang("AttachmentsCount", null) + " = " + violationData.attachments.Count + "\n";
    
                            if (violationData.attachments.Count > 0)
                                for (int ii = 0; ii < violationData.attachments.Count; ii++)
									txtConsole += Lang("Attachment", null) + " - " + violationData.attachments[ii] + "\n";
							
							if (violationData.isAttackerMount && violationData.attackerMountParentName != null)
								txtConsole += Lang("MountedOn", null, violationData.attackerMountParentName) + "\n\n";
                            
							txtConsole += Lang("RicochetsCount", null) + " + " + (violationData.hitsData.Count - 1) + "\n\n";
							txtConsole += $"isEqualFiredProjectileData = {violationData.isEqualFiredProjectileData}\n";
							txtConsole += $"isPlayerPositionToProjectileStartPositionDistanceViolation = {violationData.isPlayerPositionToProjectileStartPositionDistanceViolation}\n";
                            
							if (violationData.isPlayerPositionToProjectileStartPositionDistanceViolation)
								txtConsole += Lang("LogText6", null, violationData.distanceDifferenceViolation);
    
                            for (int j = 0; j < violationData.hitsData.Count; j++)
                            {
								txtConsole += $".\n" + Lang("LogText7", null, j + 1) + "\n";
								txtConsole += $"isHitPointNearProjectileTrajectoryLastSegmentEndPoint = {violationData.hitsData[j].isHitPointNearProjectileTrajectoryLastSegmentEndPoint}" + "\n";
                                
								if (!violationData.hitsData[j].isHitPointNearProjectileTrajectoryLastSegmentEndPoint && violationData.hitsData[j].side > 0)
                                    if (violationData.hitsData[j].side == 1)
										txtConsole += "     " + Lang("AIMText4", null, violationData.hitsData[j].distanceFromHitPointToProjectilePlane, Lang("AIMText2", null), "StartPoint", Vector3.Distance(violationData.hitsData[j].hitPositionWorld, violationData.hitsData[j].hitPointStart)) + "\n";
                                    else
										txtConsole += "     " + Lang("AIMText4", null, violationData.hitsData[j].distanceFromHitPointToProjectilePlane, Lang("AIMText3", null), "EndPoint", Vector3.Distance(violationData.hitsData[j].hitPositionWorld, violationData.hitsData[j].hitPointEnd)) + "\n";
    
								txtConsole += $"isHitPointOnProjectileTrajectory = {violationData.hitsData[j].isHitPointOnProjectileTrajectory}" + "\n";
								txtConsole += $"isProjectileStartPointAtEndReverseProjectileTrajectory = {violationData.hitsData[j].isProjectileStartPointAtEndReverseProjectileTrajectory}" + "\n";
                                
								if (!violationData.hitsData[j].isProjectileStartPointAtEndReverseProjectileTrajectory)
									txtConsole += "     " + Lang("AIMText6", null, violationData.hitsData[j].lastSegmentPointStart, violationData.hitsData[j].lastSegmentPointEnd, violationData.hitsData[j].startProjectilePosition, violationData.hitsData[j].startProjectilePosition + violationData.hitsData[j].startProjectileVelocity) + "\n";
    
								txtConsole += $"isHitPointNearProjectilePlane = {violationData.hitsData[j].isHitPointNearProjectilePlane}" + "\n";
                                
								if (!violationData.hitsData[j].isHitPointNearProjectilePlane)
									txtConsole += "     " + Lang("AIMText7", null, violationData.hitsData[j].distanceFromHitPointToProjectilePlane) + "\n";
    
								txtConsole += $"isLastSegmentOnProjectileTrajectoryPlane = {violationData.hitsData[j].isLastSegmentOnProjectileTrajectoryPlane}" + "\n";
                                
								if (!violationData.hitsData[j].isLastSegmentOnProjectileTrajectoryPlane)
									txtConsole += "     " + Lang("AIMText8", null, violationData.hitsData[j].lastSegmentPointStart, violationData.hitsData[j].startProjectilePosition, violationData.hitsData[j].startProjectilePosition + violationData.hitsData[j].startProjectileVelocity) + "\n";
    
								txtConsole += Lang("LogText8", null, j + 1) + "\n";
                            }
    
							txtConsole += "\n.";				
							
                            break;
                        }
                        i++;
                    }
                }
				Puts(txtConsole);
			}
		}

		[ConsoleCommand("arkan.ir")]
		private void ConsoleShowInRockLog(ConsoleSystem.Arg arg)
        {			
			var player = arg.Player();
			string txtConsole = "";

			if (player == null)
			{
				if (!arg.HasArgs())
				{
					txtConsole = RemoveFormatting(Lang("ShowNRLog2", null));
                    int nrCnt = 0;
					
					if (PlayersViolations.Players.Count > 0)
					{
						foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players.Where(x => (PlayersViolations.Players[x.Key].inRockViolations.Count > 0)))
						{
							txtConsole += $"{PlayersViolations.Players[list.Key].PlayerName} - IR({PlayersViolations.Players[list.Key].inRockViolations.Count}); ";
							
							nrCnt++;
						}

						Puts(txtConsole);
					}	
					
					if (nrCnt == 0)
                        Puts(RemoveFormatting(Lang("ShowIRLog3", null)));

					return;
				}
				
                string s = null;
				txtConsole = "";
                ulong id = 0;
				ulong playerID = 0;
        
                if (arg.Args.Length == 2)
                    if (arg.Args[1] == "0")
                    {
						txtConsole += Lang("ShowD1", null) + "\n";
						RconLog = new AdminConfig();
                    }
                    else
                        s = arg.Args[1];
        
                string user = arg.Args[0];
        
                if (user.Contains("765"))
                    ulong.TryParse(arg.Args[0], out id);
        
                foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
                    if (PlayersViolations.Players[list.Key].PlayerID == id || PlayersViolations.Players[list.Key].PlayerName.Contains(user, CompareOptions.IgnoreCase))
                        playerID = PlayersViolations.Players[list.Key].PlayerID;
        
                if (playerID == 0)
					txtConsole += Lang("ShowD2", null) + "\n";
                else
                {
                    if (PlayersViolations.Players[playerID].inRockViolations.Count == 0)
						txtConsole += Lang("ShowIRD1", null, PlayersViolations.Players[playerID].PlayerName) + "\n";
                    else
                    {
						if ((long)RconLog.violationsLog.steamID != (long)PlayersViolations.Players[playerID].PlayerID)
                        {
							RconLog.violationsLog.InRockViolation = 1;
							RconLog.violationsLog.steamID = PlayersViolations.Players[playerID].PlayerID;
                        }
                        else
                            if (s == null)
								if (PlayersViolations.Players[playerID].inRockViolations.Count >= RconLog.violationsLog.InRockViolation + 1)
									RconLog.violationsLog.InRockViolation++;
                                else
                                {
									txtConsole += Lang("NoMoreViolations", null, PlayersViolations.Players[playerID].PlayerName) + "\n";
									RconLog.violationsLog = new ViolationsLog();
									Puts(txtConsole);
                                    return;
                                }

                        int result;
                        int.TryParse(s, out result);
        
                        if (result == 0)
                            result = RconLog.violationsLog.InRockViolation;
        
                        int i = 1;
                        foreach (KeyValuePair<string, InRockViolationsData> list in PlayersViolations.Players[playerID].inRockViolations)
                        {
                            if (i == result)
                            {
                                InRockViolationsData violationData = PlayersViolations.Players[playerID].inRockViolations[list.Key];

								txtConsole += "\n" + Lang("Attacker", null) + ": " + PlayersViolations.Players[playerID].PlayerName + "/" + PlayersViolations.Players[playerID].PlayerID + "\n" + Lang("IRViolationNum", null) + result + "\n" + Lang("Weapon", null) + ": " + violationData.inRockViolationsData[1].firedProjectile.weaponShortName + "\n" + Lang("Ammo", null) + ": " + violationData.inRockViolationsData[1].firedProjectile.ammoShortName + "\n" + Lang("ShotsCount", null) + ": " + violationData.inRockViolationsData.Count + "\n";

								for (int j = 1; j <= violationData.inRockViolationsData.Count; j++)
								{
									txtConsole += Lang("ProjectileID", null, j) + " | " + Lang("Target", null) + ": " + violationData.inRockViolationsData[j].targetName + "/" + violationData.inRockViolationsData[j].targetID + " | " + Lang("HitPart", null) + ": " + violationData.inRockViolationsData[j].targetBodyPart + " | " + Lang("Damage", null) + ": " + violationData.inRockViolationsData[j].targetDamage;
								}
                                break;
                            }
                            i++;
                        }
                    }
                }
				Puts(txtConsole);
			}
		}
		
		[ConsoleCommand("arkan.clear")]
		private void ConsoleClearViolationsData(ConsoleSystem.Arg arg)
        {			
			var player = arg.Player();

			if (player == null)
			{
				PlayersViolations.Players.Clear();
                DateTime currentDate = DateTime.Now.AddSeconds(-UnityEngine.Time.realtimeSinceStartup);
                PlayersViolations.seed = ConVar.Server.seed;
                PlayersViolations.mapSize = ConVar.Server.worldsize;
                PlayersViolations.serverTimeStamp = currentDate.Year + "." + currentDate.Month + "." + currentDate.Day + "." + currentDate.Hour + "." + currentDate.Minute;
        
                foreach (KeyValuePair<BasePlayer, AdminConfig> list in AdminsConfig)
                    AdminsConfig[list.Key].violationsLog = new ViolationsLog();
        
                SaveViolationsData(null, null, null);
				Puts(RemoveFormatting(Lang("ClearVD", null)));
			}
		}		
		
		[ConsoleCommand("arkan.save")]
		private void ConsoleSaveViolationsData(ConsoleSystem.Arg arg)
        {			
			var player = arg.Player();

			if (player == null)
			{
                string fileName = serverTimeStamp;
        
                if (arg.HasArgs(1))
                    fileName = arg.Args[0];
        
                PlayersViolations.lastSaveTime = DateTime.Now;
                Interface.Oxide.DataFileSystem.WriteObject("Arkan/" + fileName, PlayersViolations);

				Puts(RemoveFormatting(Lang("SaveVD1", null, fileName)));
			}
		}			

		[ConsoleCommand("arkan.load")]
		private void ConsoleLoadViolationsData(ConsoleSystem.Arg arg)
        {			
			var player = arg.Player();

			if (player == null)
			{
                string fileName = serverTimeStamp;
        
                if (arg.HasArgs(1))
                    fileName = arg.Args[0];
        
                if (Interface.Oxide.DataFileSystem.ExistsDatafile("Arkan/" + fileName))
                {
                    tmpPlayersViolations = null;
                    tmpPlayersViolations = Interface.Oxide.DataFileSystem.ReadObject<PlayersViolationsData>("Arkan/" + fileName);
					
                    if (tmpPlayersViolations.seed != ConVar.Server.seed)
                    {
                        Puts(Lang("LoadVD2", null, tmpPlayersViolations.seed, ConVar.Server.seed));
                        return;
                    }
        
                    if (tmpPlayersViolations.mapSize != ConVar.Server.worldsize)
                    {
                        Puts(Lang("LoadVD3", null, tmpPlayersViolations.mapSize, ConVar.Server.worldsize));
                        return;
                    }
        
                    if (tmpPlayersViolations.Players.Count > 0)
                    {
                        bool isChanged = false;
                        PlayerViolationsData playerViolationsData;
                        NoRecoilViolationData nrvd;
                        AIMViolationData aimvd;
						InRockViolationsData irvd;
        
                        foreach (KeyValuePair<ulong, PlayerViolationsData> list in tmpPlayersViolations.Players)
                        {
                            if (!PlayersViolations.Players.TryGetValue(tmpPlayersViolations.Players[list.Key].PlayerID, out playerViolationsData))
                            {
                                PlayersViolations.Players.Add(list.Key, playerViolationsData = new PlayerViolationsData());
                                PlayersViolations.Players[list.Key].PlayerID = tmpPlayersViolations.Players[list.Key].PlayerID;
                                PlayersViolations.Players[list.Key].PlayerName = tmpPlayersViolations.Players[list.Key].PlayerName;
                                isChanged = true;
                            }
        
                            if (tmpPlayersViolations.Players[list.Key].noRecoilViolations.Count > 0)
                                foreach (KeyValuePair<string, NoRecoilViolationData> nrlist in tmpPlayersViolations.Players[list.Key].noRecoilViolations)
                                    if (!PlayersViolations.Players[tmpPlayersViolations.Players[list.Key].PlayerID].noRecoilViolations.TryGetValue(nrlist.Key, out nrvd))
                                    {
                                        PlayersViolations.Players[list.Key].noRecoilViolations.Add(nrlist.Key, nrvd = new NoRecoilViolationData());
                                        PlayersViolations.Players[list.Key].noRecoilViolations[nrlist.Key] = tmpPlayersViolations.Players[list.Key].noRecoilViolations[nrlist.Key];
                                        isChanged = true;
                                    }
        
                            if (tmpPlayersViolations.Players[list.Key].AIMViolations.Count > 0)
                                foreach (KeyValuePair<string, AIMViolationData> aimlist in tmpPlayersViolations.Players[list.Key].AIMViolations)
                                    if (!PlayersViolations.Players[tmpPlayersViolations.Players[list.Key].PlayerID].AIMViolations.TryGetValue(aimlist.Key, out aimvd))
                                    {
                                        PlayersViolations.Players[list.Key].AIMViolations.Add(aimlist.Key, aimvd = new AIMViolationData());
                                        PlayersViolations.Players[list.Key].AIMViolations[aimlist.Key] = tmpPlayersViolations.Players[list.Key].AIMViolations[aimlist.Key];
                                        isChanged = true;
                                    }
						
							if (tmpPlayersViolations.Players[list.Key].inRockViolations.Count > 0)
                                foreach (KeyValuePair<string, InRockViolationsData> irlist in tmpPlayersViolations.Players[list.Key].inRockViolations)
                                    if (!PlayersViolations.Players[tmpPlayersViolations.Players[list.Key].PlayerID].inRockViolations.TryGetValue(irlist.Key, out irvd))
                                    {
                                        PlayersViolations.Players[list.Key].inRockViolations.Add(irlist.Key, irvd = new InRockViolationsData());
                                        PlayersViolations.Players[list.Key].inRockViolations[irlist.Key] = tmpPlayersViolations.Players[list.Key].inRockViolations[irlist.Key];
                                        isChanged = true;
                                    }
                        }
						
                        if (isChanged)
                        {
                            PlayersViolations.lastChangeTime = DateTime.Now;
                            PlayersViolations.lastSaveTime = DateTime.Now;
                        }
						
                        tmpPlayersViolations.Players = null;
        
                        Puts(RemoveFormatting(Lang("LoadVD1", null, fileName)));
                    }
                }
                else
					Puts(RemoveFormatting(Lang("LoadVD4", null, fileName)));
			}
		}	

        [ChatCommand("arkanclear")]
		private void ClearViolationsData(BasePlayer player, string command, string[] args)
        {
            if (player != null)
			{	
                if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
                    return;

				AdminLogInit(player);

                PlayersViolations.Players.Clear();
                DateTime currentDate = DateTime.Now.AddSeconds(-UnityEngine.Time.realtimeSinceStartup);
                PlayersViolations.seed = ConVar.Server.seed;
                PlayersViolations.mapSize = ConVar.Server.worldsize;
                PlayersViolations.serverTimeStamp = currentDate.Year + "." + currentDate.Month + "." + currentDate.Day + "." + currentDate.Hour + "." + currentDate.Minute;
        
                foreach (KeyValuePair<BasePlayer, AdminConfig> list in AdminsConfig)
                    AdminsConfig[list.Key].violationsLog = new ViolationsLog();

				SaveViolationsData(null, null, null);
                SendReply(player, Lang("ClearVD", player.UserIDString));
			}
        }

        [ChatCommand("arkansave")]
		private void SaveViolationsData(BasePlayer player, string command, string[] args)
        {
            if (player != null)
			{	
                if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
                    return;
				
				AdminLogInit(player);
			}

            string fileName = serverTimeStamp;

            if (args != null)
                if (args.Length > 0)
                    fileName = args[0];

            PlayersViolations.lastSaveTime = DateTime.Now;
            Interface.Oxide.DataFileSystem.WriteObject("Arkan/" + fileName, PlayersViolations);

            if (player != null)
                SendReply(player, Lang("SaveVD1", player.UserIDString, fileName));
        }

        [ChatCommand("arkanload")]
		private void LoadViolationsData(BasePlayer player, string command, string[] args)
        {
            if (player != null)
			{	
                if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
                    return;

				AdminLogInit(player);
			}

            string fileName = serverTimeStamp;

            if (args != null)
                if (args.Length > 0)
                    fileName = args[0];

            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Arkan/" + fileName))
            {
                tmpPlayersViolations = null;
                tmpPlayersViolations = Interface.Oxide.DataFileSystem.ReadObject<PlayersViolationsData>("Arkan/" + fileName);
				
                if (tmpPlayersViolations.seed != ConVar.Server.seed)
                {
                    SendReply(player, "<color=green>Arkan: </color><color=red>" + Lang("LoadVD2", player.UserIDString, "<color=yellow>" + tmpPlayersViolations.seed + "</color>", "<color=yellow>" + ConVar.Server.seed + "</color>") + "</color>");
                    Puts(Lang("LoadVD2", player.UserIDString, tmpPlayersViolations.seed, ConVar.Server.seed));
                    return;
                }

                if (tmpPlayersViolations.mapSize != ConVar.Server.worldsize)
                {
                    SendReply(player, "<color=green>Arkan: </color><color=red>" + Lang("LoadVD3", player.UserIDString, "<color=yellow>" + tmpPlayersViolations.mapSize + "</color>", "<color=yellow>" + ConVar.Server.worldsize + "</color>") + "</color>");
                    Puts(Lang("LoadVD3", player.UserIDString, tmpPlayersViolations.mapSize, ConVar.Server.worldsize));
                    return;
                }

                if (tmpPlayersViolations.Players.Count > 0)
                {
                    bool isChanged = false;
                    PlayerViolationsData playerViolationsData;
                    NoRecoilViolationData nrvd;
                    AIMViolationData aimvd;
					InRockViolationsData irvd;

                    foreach (KeyValuePair<ulong, PlayerViolationsData> list in tmpPlayersViolations.Players)
                    {
                        if (!PlayersViolations.Players.TryGetValue(tmpPlayersViolations.Players[list.Key].PlayerID, out playerViolationsData))
                        {
                            PlayersViolations.Players.Add(list.Key, playerViolationsData = new PlayerViolationsData());
                            PlayersViolations.Players[list.Key].PlayerID = tmpPlayersViolations.Players[list.Key].PlayerID;
                            PlayersViolations.Players[list.Key].PlayerName = tmpPlayersViolations.Players[list.Key].PlayerName;
                            isChanged = true;
                        }

                        if (tmpPlayersViolations.Players[list.Key].noRecoilViolations.Count > 0)
                            foreach (KeyValuePair<string, NoRecoilViolationData> nrlist in tmpPlayersViolations.Players[list.Key].noRecoilViolations)
                                if (!PlayersViolations.Players[tmpPlayersViolations.Players[list.Key].PlayerID].noRecoilViolations.TryGetValue(nrlist.Key, out nrvd))
                                {
                                    PlayersViolations.Players[list.Key].noRecoilViolations.Add(nrlist.Key, nrvd = new NoRecoilViolationData());
                                    PlayersViolations.Players[list.Key].noRecoilViolations[nrlist.Key] = tmpPlayersViolations.Players[list.Key].noRecoilViolations[nrlist.Key];
                                    isChanged = true;
                                }

                        if (tmpPlayersViolations.Players[list.Key].AIMViolations.Count > 0)
                            foreach (KeyValuePair<string, AIMViolationData> aimlist in tmpPlayersViolations.Players[list.Key].AIMViolations)
                                if (!PlayersViolations.Players[tmpPlayersViolations.Players[list.Key].PlayerID].AIMViolations.TryGetValue(aimlist.Key, out aimvd))
                                {
                                    PlayersViolations.Players[list.Key].AIMViolations.Add(aimlist.Key, aimvd = new AIMViolationData());
                                    PlayersViolations.Players[list.Key].AIMViolations[aimlist.Key] = tmpPlayersViolations.Players[list.Key].AIMViolations[aimlist.Key];
                                    isChanged = true;
                                }
						
						if (tmpPlayersViolations.Players[list.Key].inRockViolations.Count > 0)
                            foreach (KeyValuePair<string, InRockViolationsData> irlist in tmpPlayersViolations.Players[list.Key].inRockViolations)
								if (!PlayersViolations.Players[tmpPlayersViolations.Players[list.Key].PlayerID].inRockViolations.TryGetValue(irlist.Key, out irvd))
                                {
                                    PlayersViolations.Players[list.Key].inRockViolations.Add(irlist.Key, irvd = new InRockViolationsData());
                                    PlayersViolations.Players[list.Key].inRockViolations[irlist.Key] = tmpPlayersViolations.Players[list.Key].inRockViolations[irlist.Key];
                                    isChanged = true;
                                }
                    }
					
                    if (isChanged)
                    {
                        PlayersViolations.lastChangeTime = DateTime.Now;
                        PlayersViolations.lastSaveTime = DateTime.Now;
                    }
					
                    tmpPlayersViolations.Players = null;

                    if (player != null)
                        SendReply(player, Lang("LoadVD1", player.UserIDString, fileName));
                }
            }
            else
				if (player != null)
					SendReply(player, Lang("LoadVD4", player.UserIDString, fileName));
        }

		[ChatCommand("arkan")]
        private void ShowLog(BasePlayer player, string command, string[] args)
        {
			if (player != null)
			{	
				if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
					return;
				
				AdminLogInit(player);

                string txt = Lang("ShowInfo", player.UserIDString);
        
                if (PlayersViolations.Players.Count > 0)
                {
                    txt += Lang("ShowLog2", player.UserIDString) + "\n";
					string txtTmp = "";
					string txtConsole = "<color=green>Arkan: </color>" + Lang("ShowLog2");
					int i = 1;
					
                    foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
					{
						txtTmp = "";
						txt += $"<size=10><color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> -";
						txtConsole += $"<color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> -";
						
						if (PlayersViolations.Players[list.Key].noRecoilViolations.Count > 0)
							txtTmp += $" NR(<color=yellow>{PlayersViolations.Players[list.Key].noRecoilViolations.Count}</color>)";
						
						if (PlayersViolations.Players[list.Key].AIMViolations.Count > 0)
							txtTmp += $" AIM(<color=yellow>{PlayersViolations.Players[list.Key].AIMViolations.Count}</color>)";
						
						if (PlayersViolations.Players[list.Key].inRockViolations.Count > 0)
							txtTmp += $" IR(<color=yellow>{PlayersViolations.Players[list.Key].inRockViolations.Count}</color>)";
						
						txtConsole += txtTmp + "; ";
                        txt += txtTmp + "; </size>";

						i++;

						if (i == 5)
						{
							txtConsole += "\n";
							i = 1;
						}								
					}
					
					player.ConsoleMessage(txtConsole + "</color></size>");
                }
                else
				{
                    txt += Lang("ShowLog3", player.UserIDString);
					player.ConsoleMessage("<color=green>Arkan: </color>" + Lang("ShowLog3", player.UserIDString) + "</color></size>");
				}
        
                SendReply(player, txt + "</color></size>");
                return;
            }
        }

		[ChatCommand("arkaninfo")]
        private void ShowInfo(BasePlayer player, string command, string[] args)
        {
			if (player != null)
			{	
				if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
					return;
				
				AdminLogInit(player);

                SendReply(player, Lang("ShowLog1v1.0.13", player.UserIDString));
            }
        }

        [ChatCommand("arkannr")]
		private void ShowNoRecoilLog(BasePlayer player, string command, string[] args)
        {
			if (player != null)
			{	
				if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
					return;

				AdminLogInit(player);

                if (args.Length == 0)
                {
                    int nrCnt = 0;
                    string txt = Lang("ShowInfo", player.UserIDString);
					string txtConsole = "";
        
                    if (PlayersViolations.Players.Count > 0)
					{
						int i = 1;
						foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players.Where(x => (PlayersViolations.Players[x.Key].noRecoilViolations.Count > 0)))
                        {
                            if (nrCnt == 0)
							{
                                txt += Lang("ShowNRLog2", player.UserIDString) + "\n";
								txtConsole = "<color=green>Arkan: </color>" + Lang("ShowNRLog2", player.UserIDString);
							}
        
                            txt += $"<size=10><color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> - NR(<color=yellow>{PlayersViolations.Players[list.Key].noRecoilViolations.Count}</color>); </size>";
                            txtConsole += $"<color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> - NR(<color=yellow>{PlayersViolations.Players[list.Key].noRecoilViolations.Count}</color>); ";
							
							i++;
							
							if (i == 5)
							{
								txtConsole += "\n";
								i = 1;
							}   
							
							nrCnt++;
                        }
						
						player.ConsoleMessage(txtConsole + "</color></size>");
					}
        
                    if (nrCnt == 0)
					{
                        txt += Lang("ShowNRLog3", player.UserIDString);
						player.ConsoleMessage("<color=green>Arkan: </color>" + Lang("ShowNRLog3", player.UserIDString) + "</color></size>");
					}
					
                    SendReply(player, txt + "</color></size>");
                    return;
                }

				ShowNoRecoilViolations(player, args);
			}
        }

        [ChatCommand("arkanaim")]
		private void ShowAIMLog(BasePlayer player, string command, string[] args)
        {
			if (player != null)
			{	
				if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
					return;

				AdminLogInit(player);

                if (args.Length == 0)
                {
                    int aimCnt = 0;
                    string txt = Lang("ShowInfo", player.UserIDString);
					string txtConsole = "";
        
                    if (PlayersViolations.Players.Count > 0)
					{
						int i = 1;
						foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players.Where(x => (PlayersViolations.Players[x.Key].AIMViolations.Count > 0)))
                        {
                            if (aimCnt == 0)
							{
                                txt += Lang("ShowAIMLog2", player.UserIDString) + "\n";
								txtConsole = "<color=green>Arkan: </color>" + Lang("ShowAIMLog2", player.UserIDString);
							}
        
                            txt += $"<size=10><color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> - AIM(<color=yellow>{PlayersViolations.Players[list.Key].AIMViolations.Count}</color>); </size>";
                            txtConsole += $"<color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> - AIM(<color=yellow>{PlayersViolations.Players[list.Key].AIMViolations.Count}</color>); ";
							
							i++;
							
							if (i == 5)
							{
								txtConsole += "\n";
								i = 1;
							}   
							
							aimCnt++;
                        }
						
						player.ConsoleMessage(txtConsole + "</color></size>");
					}
        
                    if (aimCnt == 0)
					{
                        txt += Lang("ShowAIMLog3", player.UserIDString);
						player.ConsoleMessage("<color=green>Arkan: </color>" + Lang("ShowAIMLog3", player.UserIDString) + "</color></size>");
					}
        
                    SendReply(player, txt + "</color></size>");
                    return;
                }
        
                ShowAIMViolations(player, args);
			}
        }

        [ChatCommand("arkanaimr")]
		private void ShowAIMLogRecalc(BasePlayer player, string command, string[] args) //for development purposes only
        {
			if (player != null)
			{	
				if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
					return;

				AdminLogInit(player);

                if (args.Length == 0)
                {
                    int nrCnt = 0;
                    string txt = Lang("ShowInfo", player.UserIDString);
        
                    if (PlayersViolations.Players.Count > 0)
						foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players.Where(x => (PlayersViolations.Players[x.Key].AIMViolations.Count > 0)))
                        {
                            if (nrCnt == 0)
                                txt += Lang("ShowAIMLog2", player.UserIDString);
        
                            txt += $"<color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> - AIM(<color=yellow>{PlayersViolations.Players[list.Key].AIMViolations.Count}</color>); ";
                            nrCnt++;
                        }
        
                    if (nrCnt == 0)
                        txt += Lang("ShowAIMLog3", player.UserIDString);
        
                    SendReply(player, txt + "</color></size>");
                    return;
                }
        
                ShowAIMViolationsRecalc(player, args);
			}			
        }

		[ChatCommand("arkanir")]
		private void ShowInRocklLog(BasePlayer player, string command, string[] args)
        {
			if (player != null)
			{	
				if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
					return;

				AdminLogInit(player);

                if (args.Length == 0)
                {
                    int nrCnt = 0;
                    string txt = Lang("ShowInfo", player.UserIDString);
					string txtConsole = "";
        
                    if (PlayersViolations.Players.Count > 0)
					{
						int i = 1;
						foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players.Where(x => (PlayersViolations.Players[x.Key].inRockViolations.Count > 0)))
                        {
                            if (nrCnt == 0)
							{
                                txt += Lang("ShowIRLog2", player.UserIDString) + "\n";
								txtConsole = "<color=green>Arkan: </color>" + Lang("ShowIRLog2", player.UserIDString);
							}
        
                            txt += $"<size=10><color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> - IR(<color=yellow>{PlayersViolations.Players[list.Key].inRockViolations.Count}</color>); </size>";
                            txtConsole += $"<color=green>{PlayersViolations.Players[list.Key].PlayerName}</color> - IR(<color=yellow>{PlayersViolations.Players[list.Key].inRockViolations.Count}</color>); ";
							
							i++;
							
							if (i == 5)
							{
								txtConsole += "\n";
								i = 1;
							}   
							
							nrCnt++;
                        }
						
						player.ConsoleMessage(txtConsole + "</color></size>");
					}
        
                    if (nrCnt == 0)
					{
                        txt += Lang("ShowIRLog3", player.UserIDString);
						player.ConsoleMessage("<color=green>Arkan: </color>" + Lang("ShowIRLog3", player.UserIDString) + "</color></size>");
					}
					
                    SendReply(player, txt + "</color></size>");
                    return;
                }

				ShowInRockViolations(player, args);
			}
        }
		
        #endregion Commands

        #region Functions
		
		private string API_ArkanGetPlayersViolationsData()
        {
			if(PlayersViolations != null)
			{
				return JsonConvert.SerializeObject(PlayersViolations);
			}
			return null;
		}		

        private void CleanupExpiredProjectiles(BasePlayer player)
        {
			if (player != null)
				if (PlayersFiredProjectlesData.ContainsKey(player.userID))
					if (PlayersFiredProjectlesData[player.userID].lastFiredTime < UnityEngine.Time.realtimeSinceStartup - 8f && (PlayersFiredProjectlesData[player.userID].firedProjectiles.Count > 0 || PlayersFiredProjectlesData[player.userID].melees.Count > 0))
					{
						PlayersFiredProjectlesData[player.userID].firedProjectiles.Clear();
						PlayersFiredProjectlesData[player.userID].melees.Clear();
					}
        }

        private void DDrawArrowToAdmin(BasePlayer player, float _drawTime, Color color, Vector3 startPosition, Vector3 endPosition, float arrowHeadSize)
        {
            if (player != null)
            {
                if (player.IsAdmin && permission.UserHasPermission(player.UserIDString, permName))
                    player.SendConsoleCommand("ddraw.arrow", _drawTime, color, startPosition, endPosition, arrowHeadSize);
            }
            else
                foreach (var _player in BasePlayer.activePlayerList.Where(x => x.IsAdmin && permission.UserHasPermission(x.UserIDString, permName)))
                    _player.SendConsoleCommand("ddraw.arrow", _drawTime, color, startPosition, endPosition, arrowHeadSize);
        }

        private void DDrawSphereToAdmin(BasePlayer player, float _drawTime, Color color, Vector3 Position, float sphereSize)
        {
            if (player != null)
            {
                if (player.IsAdmin && permission.UserHasPermission(player.UserIDString, permName))
                    player.SendConsoleCommand("ddraw.sphere", _drawTime, color, Position, sphereSize);
            }
            else
                foreach (var _player in BasePlayer.activePlayerList.Where(x => x.IsAdmin && permission.UserHasPermission(x.UserIDString, permName)))
                    _player.SendConsoleCommand("ddraw.sphere", _drawTime, color, Position, sphereSize);
        }

        private void DDrawTextToAdmin(BasePlayer player, float _drawTime, Color color, Vector3 Position, string text)
        {
            if (player != null)
            {
                if (player.IsAdmin && permission.UserHasPermission(player.UserIDString, permName))
                    player.SendConsoleCommand("ddraw.text", _drawTime, color, Position, text);
            }
            else
                foreach (var _player in BasePlayer.activePlayerList.Where(x => x.IsAdmin && permission.UserHasPermission(x.UserIDString, permName)))
                    _player.SendConsoleCommand("ddraw.text", _drawTime, color, Position, text);
        }
		
		private void DrawInRockViolationsData(BasePlayer player, string suspectPlayerName, string suspectPlayerID, int vsCnt, InRockViolationsData violationData, bool isTeleport)
        {
            if (player != null && violationData != null)
            {                
                if (isTeleport)
                {
                    Vector3 startPos = violationData.inRockViolationsData[1].firedProjectile.projectilePosition;
                    Vector3 tempPos = player.eyes.HeadForward();
                    Vector3 teleportPos = new Vector3(startPos.x - tempPos.x, startPos.y + 0.1f, startPos.z - tempPos.z);
                    player.Teleport(teleportPos);
                }
				
				DDrawTextToAdmin(player, _config.drawTime, Color.cyan, violationData.inRockViolationsData[1].firedProjectile.projectilePosition + Vector3.up * 0.3f, Lang("PlayerTxt", player.UserIDString) + ": <color=yellow>" + suspectPlayerName + "/" +suspectPlayerID + "</color>\n" + Lang("IRViolationNum", player.UserIDString) + ": <color=white>" + vsCnt + "</color>\n" + Lang("Weapon", player.UserIDString) + ": <color=white>" + violationData.inRockViolationsData[1].firedProjectile.weaponShortName + "</color>\n" + Lang("Ammo", player.UserIDString) + ": <color=white>" + violationData.inRockViolationsData[1].firedProjectile.ammoShortName + "</color>\n" + Lang("ShotsCount", player.UserIDString) + ": <color=white>" + violationData.inRockViolationsData.Count + "</color>");
				DDrawSphereToAdmin(player, _config.drawTime, Color.green, violationData.inRockViolationsData[1].firedProjectile.projectilePosition, 0.04f);
				
				for (int i = 1; i <= violationData.inRockViolationsData.Count; i++)
                {					
					float physicsSteps = violationData.inRockViolationsData[i].physicsSteps;
					float fixedDeltaTime = 1f / physicsSteps;
                    Vector3 position = violationData.inRockViolationsData[i].firedProjectile.projectilePosition;
                    Vector3 vector1 = violationData.inRockViolationsData[i].firedProjectile.projectileVelocity / physicsSteps;
                    float distance = violationData.inRockViolationsData[i].targetHitDistance;
					float hitInfoDistance = violationData.inRockViolationsData[i].targetHitDistance;
                    float gravityModifier = violationData.inRockViolationsData[i].gravityModifier;
                    float drag = violationData.inRockViolationsData[i].drag;
                    Vector3 hitPoint = violationData.inRockViolationsData[i].targetHitPosition;
                    Vector3 vector2 = ((Physics.gravity * gravityModifier) / physicsSteps) * fixedDeltaTime;

                    float single1 = drag * fixedDeltaTime;
                    float dist = 0.0f;
					int segmentsCnt = (int)(physicsSteps * 8);
            
                    DDrawSphereToAdmin(player, _config.drawTime, Color.red, hitPoint, 0.04f);
					DDrawSphereToAdmin(player, _config.drawTime, Color.red, violationData.inRockViolationsData[i].rockHitPosition, 0.04f);
            
                    for (int j = 0; j < segmentsCnt; j++)
                    {                        
                        DDrawArrowToAdmin(player, _config.drawTime, Color.yellow, position, position + vector1, 0.05f);
            
                        if ((distance - dist) <= (vector1.magnitude))
                            break;
            
                        dist += vector1.magnitude;
                        position += vector1;
            
                        vector1 += vector2;
                        vector1 -= (vector1 * single1);
                    }					
				}
            }
        }

        private void DrawNoRecoilViolationsData(BasePlayer player, string suspectPlayerName, NoRecoilViolationData violationData, bool isTeleport)
        {
            if (player != null)
            {
                if (violationData.isMounted)
                {
                    Matrix4x4 viewMatrix;
					Quaternion q = new Quaternion(violationData.mountParentRotation.x, violationData.mountParentRotation.y, violationData.mountParentRotation.z, violationData.mountParentRotation.w);
					viewMatrix = Matrix4x4.LookAt(violationData.mountParentPosition, violationData.mountParentPosition + (q * Vector3.forward), Vector3.up);

                    if (isTeleport)
                    {
                        Vector3 startPos = viewMatrix.MultiplyPoint(violationData.suspiciousNoRecoilShots[1].projectile2Position - player.eyes.offset);
                        Vector3 tempPos = player.eyes.HeadForward();
                        Vector3 teleportPos = new Vector3(startPos.x - tempPos.x, startPos.y + 0.1f, startPos.z - tempPos.z);
                        player.Teleport(teleportPos);
                    }

                    foreach (KeyValuePair<int, SuspiciousProjectileData> list in violationData.suspiciousNoRecoilShots.ToArray())
                    {
                        DDrawSphereToAdmin(player, _config.drawTime, Color.green, viewMatrix.MultiplyPoint(violationData.suspiciousNoRecoilShots[list.Key].projectile2Position), 0.01f);
                        
						if (violationData.suspiciousNoRecoilShots[list.Key].isNoRecoil)
                        {
                            DDrawArrowToAdmin(player, _config.drawTime, Color.blue, viewMatrix.MultiplyPoint(violationData.suspiciousNoRecoilShots[list.Key].projectile2Position), viewMatrix.MultiplyPoint(violationData.suspiciousNoRecoilShots[list.Key].projectile2Position + violationData.suspiciousNoRecoilShots[list.Key].projectile2Velocity.normalized * 450f), 0.2f);
                            
							if (violationData.suspiciousNoRecoilShots[list.Key].isShootedInMotion)
                            {
                                DDrawSphereToAdmin(player, _config.drawTime, Color.red, viewMatrix.MultiplyPoint(violationData.suspiciousNoRecoilShots[list.Key].closestPointLine1), 0.1f);
                                DDrawSphereToAdmin(player, _config.drawTime, Color.red, viewMatrix.MultiplyPoint(violationData.suspiciousNoRecoilShots[list.Key].closestPointLine2), 0.1f);
                            }
                        }
                        else
                            DDrawArrowToAdmin(player, _config.drawTime, Color.green, viewMatrix.MultiplyPoint(violationData.suspiciousNoRecoilShots[list.Key].projectile2Position), viewMatrix.MultiplyPoint(violationData.suspiciousNoRecoilShots[list.Key].projectile2Position + violationData.suspiciousNoRecoilShots[list.Key].projectile2Velocity.normalized * 450f), 0.2f);
                    }
                }
                else
                {
                    if (isTeleport)
                    {
                        Vector3 startPos = violationData.suspiciousNoRecoilShots[1].projectile2Position - player.eyes.offset;
                        Vector3 tempPos = player.eyes.HeadForward();
                        Vector3 teleportPos = new Vector3(startPos.x - tempPos.x, startPos.y + 0.1f, startPos.z - tempPos.z);
                        player.Teleport(teleportPos);
                    }

                    DDrawTextToAdmin(player, _config.drawTime, Color.cyan, violationData.suspiciousNoRecoilShots[1].projectile2Position + Vector3.up * 0.3f, Lang("DrawNRVD1", player.UserIDString, suspectPlayerName, violationData.ammoShortName, violationData.weaponShortName, violationData.suspiciousNoRecoilShots.Count(), violationData.violationProbability));

                    foreach (KeyValuePair<int, SuspiciousProjectileData> list in violationData.suspiciousNoRecoilShots.ToArray())
                    {
                        DDrawSphereToAdmin(player, _config.drawTime, Color.green, violationData.suspiciousNoRecoilShots[list.Key].projectile2Position, 0.01f);
                        
						if (violationData.suspiciousNoRecoilShots[list.Key].isNoRecoil)
                        {
                            DDrawArrowToAdmin(player, _config.drawTime, Color.blue, violationData.suspiciousNoRecoilShots[list.Key].projectile2Position, (violationData.suspiciousNoRecoilShots[list.Key].projectile2Position + violationData.suspiciousNoRecoilShots[list.Key].projectile2Velocity.normalized * 450f), 0.2f);
                            
							if (violationData.suspiciousNoRecoilShots[list.Key].isShootedInMotion)
                            {
                                DDrawSphereToAdmin(player, _config.drawTime, Color.red, violationData.suspiciousNoRecoilShots[list.Key].closestPointLine1, 0.1f);
                                DDrawSphereToAdmin(player, _config.drawTime, Color.red, violationData.suspiciousNoRecoilShots[list.Key].closestPointLine2, 0.1f);
                            }
                        }
                        else
                            DDrawArrowToAdmin(player, _config.drawTime, Color.green, violationData.suspiciousNoRecoilShots[list.Key].projectile2Position, (violationData.suspiciousNoRecoilShots[list.Key].projectile2Position + violationData.suspiciousNoRecoilShots[list.Key].projectile2Velocity.normalized * 450f), 0.2f);
                    }
                }
            }
        }

        private void DrawAIMViolationsData(BasePlayer player, AIMViolationData violationData, bool isTeleport)
        {
            if (player != null)
            {
                if (isTeleport)
                {
                    Vector3 startPos = violationData.startProjectilePosition - player.eyes.offset;
                    Vector3 tempPos = player.eyes.HeadForward();
                    Vector3 teleportPos = new Vector3(startPos.x - tempPos.x, startPos.y + 0.1f, startPos.z - tempPos.z);
                    player.Teleport(teleportPos);
                }

                player.ConsoleMessage("<color=green>Arkan:</color>\n" + Lang("Attacker", player.UserIDString) + ": " + violationData.hitInfoInitiatorPlayerName + "/" + violationData.hitInfoInitiatorPlayerUserID + "\n" + Lang("AIMViolationNum", player.UserIDString) + violationData.violationID + "\n" + Lang("Weapon", player.UserIDString) + ": " + violationData.weaponShortName + "\n" + Lang("Ammo", player.UserIDString) + ": " + violationData.ammoShortName + "\n" + Lang("Distance", player.UserIDString) + ": " + violationData.hitInfoProjectileDistance);
				player.ConsoleMessage(Lang("Target", player.UserIDString) + ": " + (violationData.hitInfoHitEntityPlayerName ?? violationData.hitInfoHitEntityPlayerUserID) + "\n" + Lang("HitPart", player.UserIDString) + ": " + violationData.hitInfoBoneName);
				player.ConsoleMessage(Lang("DateTime", player.UserIDString) + ": " + violationData.firedTime);
				
				player.ConsoleMessage(Lang("AttachmentsCount", player.UserIDString) + " = " + violationData.attachments.Count);

                if (violationData.attachments.Count > 0)
                    for (int ii = 0; ii < violationData.attachments.Count; ii++)
                        player.ConsoleMessage(Lang("Attachment", player.UserIDString) + " - " + violationData.attachments[ii]);
				
				if (violationData.isAttackerMount && violationData.attackerMountParentName != null)
					player.ConsoleMessage(Lang("MountedOn", player.UserIDString, violationData.attackerMountParentName) + "\n");	
                
				player.ConsoleMessage(Lang("RicochetsCount", player.UserIDString) + " = " + (violationData.hitsData.Count - 1) + "\n");
                player.ConsoleMessage($"isEqualFiredProjectileData = {violationData.isEqualFiredProjectileData}\n");
                player.ConsoleMessage($"isPlayerPositionToProjectileStartPositionDistanceViolation = {violationData.isPlayerPositionToProjectileStartPositionDistanceViolation}\n");
                
				if (violationData.isPlayerPositionToProjectileStartPositionDistanceViolation)
                    player.ConsoleMessage(Lang("LogText6", player.UserIDString, violationData.distanceDifferenceViolation));

                for (int j = 0; j < violationData.hitsData.Count; j++)
                {
                    player.ConsoleMessage($".\n" + Lang("LogText7", player.UserIDString, j + 1));
                    player.ConsoleMessage($"isHitPointNearProjectileTrajectoryLastSegmentEndPoint = {violationData.hitsData[j].isHitPointNearProjectileTrajectoryLastSegmentEndPoint}");
                    
					if (!violationData.hitsData[j].isHitPointNearProjectileTrajectoryLastSegmentEndPoint && violationData.hitsData[j].side > 0)
                        if (violationData.hitsData[j].side == 1)
                            player.ConsoleMessage("     " + Lang("AIMText4", player.UserIDString, violationData.hitsData[j].distanceFromHitPointToProjectilePlane, Lang("AIMText2", player.UserIDString), "StartPoint", Vector3.Distance(violationData.hitsData[j].hitPositionWorld, violationData.hitsData[j].hitPointStart)));
                        else
                            player.ConsoleMessage("     " + Lang("AIMText4", player.UserIDString, violationData.hitsData[j].distanceFromHitPointToProjectilePlane, Lang("AIMText3", player.UserIDString), "EndPoint", Vector3.Distance(violationData.hitsData[j].hitPositionWorld, violationData.hitsData[j].hitPointEnd)));

                    player.ConsoleMessage($"isHitPointOnProjectileTrajectory = {violationData.hitsData[j].isHitPointOnProjectileTrajectory}");
                    player.ConsoleMessage($"isProjectileStartPointAtEndReverseProjectileTrajectory = {violationData.hitsData[j].isProjectileStartPointAtEndReverseProjectileTrajectory}");
                    
					if (!violationData.hitsData[j].isProjectileStartPointAtEndReverseProjectileTrajectory)
                        player.ConsoleMessage("     " + Lang("AIMText6", player.UserIDString, violationData.hitsData[j].lastSegmentPointStart, violationData.hitsData[j].lastSegmentPointEnd, violationData.hitsData[j].startProjectilePosition, violationData.hitsData[j].startProjectilePosition + violationData.hitsData[j].startProjectileVelocity));

                    player.ConsoleMessage($"isHitPointNearProjectilePlane = {violationData.hitsData[j].isHitPointNearProjectilePlane}");
                    
					if (!violationData.hitsData[j].isHitPointNearProjectilePlane)
                        player.ConsoleMessage("     " + Lang("AIMText7", player.UserIDString, violationData.hitsData[j].distanceFromHitPointToProjectilePlane));

                    player.ConsoleMessage($"isLastSegmentOnProjectileTrajectoryPlane = {violationData.hitsData[j].isLastSegmentOnProjectileTrajectoryPlane}");
                    
					if (!violationData.hitsData[j].isLastSegmentOnProjectileTrajectoryPlane)
                        player.ConsoleMessage("     " + Lang("AIMText8", player.UserIDString, violationData.hitsData[j].lastSegmentPointStart, violationData.hitsData[j].startProjectilePosition, violationData.hitsData[j].startProjectilePosition + violationData.hitsData[j].startProjectileVelocity));

                    player.ConsoleMessage(Lang("LogText8", player.UserIDString, j + 1));

                    DDrawSphereToAdmin(player, _config.drawTime, Color.red, violationData.hitsData[j].startProjectilePosition, 0.05f);
                    DDrawSphereToAdmin(player, _config.drawTime, Color.green, violationData.hitsData[j].lastSegmentPointStart, 0.04f);
                }

                DDrawTextToAdmin(player, _config.drawTime, Color.cyan, violationData.hitInfoHitPositionWorld + new Vector3(0f, 1f, 0f), Lang("DrawAIMVD3", player.UserIDString, violationData.hitInfoHitEntityPlayerName ?? violationData.hitInfoHitEntityPlayerUserID, violationData.hitInfoBoneName, violationData.damage));
                DDrawTextToAdmin(player, _config.drawTime, Color.cyan, violationData.hitsData[0].startProjectilePosition + new Vector3(0f, 1f, 0f), Lang("DrawAIMVD4", player.UserIDString, violationData.hitInfoInitiatorPlayerName ?? violationData.hitInfoInitiatorPlayerUserID, violationData.projectileID, violationData.ammoShortName, violationData.weaponShortName, violationData.hitInfoProjectileDistance));

                DrawProjectileTrajectory(player, _config.drawTime, violationData, Color.blue);
                DrawReverseProjectileTrajectory(player, _config.drawTime, violationData, Color.green);
				DDrawSphereToAdmin(player, _config.drawTime, Color.white, violationData.hitInfoPointStart, 0.04f);
				DDrawSphereToAdmin(player, _config.drawTime, Color.white, violationData.hitInfoPointEnd, 0.04f);
				
                if (violationData.isPlayerPositionToProjectileStartPositionDistanceViolation)
                {
                    DDrawSphereToAdmin(player, _config.drawTime, Color.red, violationData.playerEyesPosition, 0.05f);
                    DDrawTextToAdmin(player, _config.drawTime, Color.cyan, violationData.playerEyesPosition + Vector3.up, Lang("DrawAIMVD5", player.UserIDString, violationData.hitInfoInitiatorPlayerName ?? violationData.hitInfoInitiatorPlayerUserID, violationData.violationID, violationData.playerEyesPosition, violationData.startProjectilePosition, Vector3.Distance(violationData.playerEyesPosition, violationData.startProjectilePosition)));
					DDrawArrowToAdmin(player, _config.drawTime, Color.red, violationData.playerEyesPosition, violationData.playerEyesPosition + violationData.playerEyesLookAt.normalized, 0.05f);
                }
            }
        }

        private void ShowNoRecoilViolations(BasePlayer player, string[] args)
        {
            if (player == null)
                return;

            if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName) || !AdminsConfig.ContainsKey(player))
                return;

            string s = null;
			string adminMsg;
            ulong id = 0;
			ulong playerID = 0;

            if (args.Length == 2)
                if (args[1] == "0")
                {
                    player.ChatMessage(Lang("ShowD1", player.UserIDString));
                    AdminsConfig[player].violationsLog = new ViolationsLog();
                }
                else
                    s = args[1];

            string user = args[0];

            if (user.Contains("765"))
                ulong.TryParse(args[0], out id);

            foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
                if (PlayersViolations.Players[list.Key].PlayerID == id || PlayersViolations.Players[list.Key].PlayerName.Contains(user, CompareOptions.IgnoreCase))
                    playerID = PlayersViolations.Players[list.Key].PlayerID;

            if (playerID == 0)
                player.ChatMessage(Lang("ShowD2", player.UserIDString));
            else
            {
                if (PlayersViolations.Players[playerID].noRecoilViolations.Count == 0)
                    player.ChatMessage(Lang("ShowNRD1", player.UserIDString, PlayersViolations.Players[playerID].PlayerName));
                else
                {
                    adminMsg = Lang("Player", player.UserIDString, PlayersViolations.Players[playerID].PlayerName);

                    if ((long)AdminsConfig[player].violationsLog.steamID != (long)PlayersViolations.Players[playerID].PlayerID)
                    {
                        AdminsConfig[player].violationsLog.NoRecoilViolation = 1;
                        AdminsConfig[player].violationsLog.steamID = PlayersViolations.Players[playerID].PlayerID;

                        adminMsg += Lang("ShowNRD2", player.UserIDString, PlayersViolations.Players[playerID].noRecoilViolations.Count);
                    }
                    else
                        if (s == null)
                            if (PlayersViolations.Players[playerID].noRecoilViolations.Count >= AdminsConfig[player].violationsLog.NoRecoilViolation + 1)
                                AdminsConfig[player].violationsLog.NoRecoilViolation++;
                            else
                            {
                                player.ChatMessage(Lang("NoMoreViolations", player.UserIDString, PlayersViolations.Players[playerID].PlayerName));
                                AdminsConfig[player].violationsLog = new ViolationsLog();
                                return;
                            }

                    int result;
                    int.TryParse(s, out result);

                    if (result == 0)
                        result = AdminsConfig[player].violationsLog.NoRecoilViolation;

                    adminMsg += Lang("ShowNRD3", player.UserIDString, result);
                    player.ChatMessage(adminMsg + "\n");

                    int i = 1;
                    foreach (KeyValuePair<string, NoRecoilViolationData> list in PlayersViolations.Players[playerID].noRecoilViolations)
                    {
                        if (i == result)
                        {
                            DrawNoRecoilViolationsData(player, PlayersViolations.Players[playerID].PlayerName, PlayersViolations.Players[playerID].noRecoilViolations[list.Key], true);

                            NoRecoilViolationData violationData = PlayersViolations.Players[playerID].noRecoilViolations[list.Key];

							player.ConsoleMessage("<color=green>Arkan:</color>\n" + Lang("PlayerTxt", player.UserIDString) + " " + PlayersViolations.Players[playerID].PlayerName + "/" + PlayersViolations.Players[playerID].PlayerID +"\n" + Lang("NRViolationNum", player.UserIDString) + result + "\n" + Lang("ShotsCount", player.UserIDString) + " " + violationData.ShotsCnt + "\n" + Lang("Probability", player.UserIDString) + " " + violationData.violationProbability + "%");
							if (violationData.suspiciousNoRecoilShots.ContainsKey(1))
								player.ConsoleMessage(Lang("DateTime", player.UserIDString) + ": " + violationData.suspiciousNoRecoilShots[1].timeStamp);
                            player.ConsoleMessage(Lang("AttachmentsCount", player.UserIDString) + " = " + violationData.attachments.Count);

                            if (violationData.attachments.Count > 0)
                                for (int ii = 0; ii < violationData.attachments.Count; ii++)
                                    player.ConsoleMessage(Lang("Attachment", player.UserIDString) + " - " + violationData.attachments[ii]);

                            player.ConsoleMessage(Lang("Weapon", player.UserIDString) + " - " + violationData.weaponShortName);
                            player.ConsoleMessage(Lang("Ammo", player.UserIDString) + " - " + violationData.ammoShortName);
							player.ConsoleMessage(Lang("Probability", player.UserIDString) + " - " + violationData.violationProbability + "%");

                            int j = 1;
							
                            foreach (KeyValuePair<int, SuspiciousProjectileData> suspiciusProjectile in violationData.suspiciousNoRecoilShots.ToArray())
                            {
                                SuspiciousProjectileData sp = violationData.suspiciousNoRecoilShots[suspiciusProjectile.Key];
                                if (sp.isNoRecoil)
                                {
                                    if (sp.isShootedInMotion)
										player.ConsoleMessage(Lang("ProjectileID", player.UserIDString, sp.projectile2ID) + " | " + Lang("ShootingOnMove", player.UserIDString) +  " | " + Lang("ClosestPoint", player.UserIDString, sp.closestPointLine1, sp.closestPointLine2, sp.prevIntersectionPoint) + " | " + Lang("FireTimeInterval", player.UserIDString, sp.timeInterval));
									else
										player.ConsoleMessage(Lang("ProjectileID", player.UserIDString, sp.projectile2ID) + " | " + Lang("StandingShooting", player.UserIDString) + " | " + Lang("RecoilAngle", player.UserIDString, sp.recoilAngle) + " | " + Lang("FireTimeInterval", player.UserIDString, sp.timeInterval));
                                }
                                j++;
                            }
                            break;
                        }
                        i++;
                    }
                }
            }
        }

        private void ShowAIMViolations(BasePlayer player, string[] args)
        {
            if (player == null)
                return;

            if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName) || !AdminsConfig.ContainsKey(player))
                return;

            string s = null;
			string adminMsg;
            ulong id = 0;
			ulong playerID = 0;

            if (args.Length == 2)
            {
                if (args[1] == "0")
                {
                    player.ChatMessage(Lang("ShowD1", player.UserIDString));
                    AdminsConfig[player].violationsLog = new ViolationsLog();
                }
                else
                    s = args[1];
            }

            string user = args[0];

            if (user.Contains("765"))
                ulong.TryParse(args[0], out id);

            foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
                if (PlayersViolations.Players[list.Key].PlayerID == id || PlayersViolations.Players[list.Key].PlayerName.Contains(user, CompareOptions.IgnoreCase))
                    playerID = PlayersViolations.Players[list.Key].PlayerID;

            if (playerID == 0)
                player.ChatMessage(Lang("ShowD2", player.UserIDString));
            else
                if (PlayersViolations.Players[playerID].AIMViolations.Count == 0)
					player.ChatMessage(Lang("ShowAIMD1", player.UserIDString, PlayersViolations.Players[playerID].PlayerName));
            else
            {
                adminMsg = Lang("Player", player.UserIDString, PlayersViolations.Players[playerID].PlayerName);

                if ((long)AdminsConfig[player].violationsLog.steamID != (long)PlayersViolations.Players[playerID].PlayerID)
                {
                    AdminsConfig[player].violationsLog.AIMViolation = 1;
                    AdminsConfig[player].violationsLog.steamID = PlayersViolations.Players[playerID].PlayerID;
                    adminMsg += Lang("ShowAIMD2", player.UserIDString, PlayersViolations.Players[playerID].AIMViolations.Count);
                }
                else if (s == null)
                    if (PlayersViolations.Players[playerID].AIMViolations.Count >= AdminsConfig[player].violationsLog.AIMViolation + 1)
                        AdminsConfig[player].violationsLog.AIMViolation++;
                    else
                    {
                        player.ChatMessage(Lang("ShowD3", player.UserIDString, PlayersViolations.Players[playerID].PlayerName));
                        AdminsConfig[player].violationsLog = new ViolationsLog();
                        return;
                    }

                int result;
                int.TryParse(s, out result);

                if (result == 0)
                    result = AdminsConfig[player].violationsLog.AIMViolation;

                adminMsg += Lang("ShowAIMD3", player.UserIDString, result);
                player.ChatMessage(adminMsg);

                int i = 1;
                
				foreach (KeyValuePair<string, AIMViolationData> list in PlayersViolations.Players[playerID].AIMViolations)
                {
                    if (i == result)
                    {
                        DrawAIMViolationsData(player, PlayersViolations.Players[playerID].AIMViolations[list.Key], true);
                        break;
                    }
                    i++;
                }
            }
        }

        private void ShowAIMViolationsRecalc(BasePlayer player, string[] args) //for development purposes only
        {
            if (player == null)
                return;

            if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName) || !AdminsConfig.ContainsKey(player))
                return;

            string s = null;
			string adminMsg;
            ulong id = 0;
			ulong playerID = 0;

            if (args.Length == 2)
                if (args[1] == "0")
                {
                    player.ChatMessage(Lang("ShowD1", player.UserIDString));
                    AdminsConfig[player].violationsLog = new ViolationsLog();
                }
                else
                    s = args[1];

            string user = args[0];

            if (user.Contains("765"))
                ulong.TryParse(args[0], out id);

            foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
                if (PlayersViolations.Players[list.Key].PlayerID == id || PlayersViolations.Players[list.Key].PlayerName.Contains(user, CompareOptions.IgnoreCase))
                    playerID = PlayersViolations.Players[list.Key].PlayerID;

            if (playerID == 0)
                player.ChatMessage(Lang("ShowD2", player.UserIDString));
            else
                if (PlayersViolations.Players[playerID].AIMViolations.Count == 0)
					player.ChatMessage(Lang("ShowAIMD1", player.UserIDString, PlayersViolations.Players[playerID].PlayerName));
            else
            {
                adminMsg = Lang("Player", player.UserIDString, PlayersViolations.Players[playerID].PlayerName);

                if ((long)AdminsConfig[player].violationsLog.steamID != (long)PlayersViolations.Players[playerID].PlayerID)
                {
                    AdminsConfig[player].violationsLog.AIMViolation = 1;
                    AdminsConfig[player].violationsLog.steamID = PlayersViolations.Players[playerID].PlayerID;
                    adminMsg += Lang("ShowAIMD2", player.UserIDString, PlayersViolations.Players[playerID].AIMViolations.Count);
                }
                else
                    if (s == null)
                        if (PlayersViolations.Players[playerID].AIMViolations.Count >= AdminsConfig[player].violationsLog.AIMViolation + 1)
                            AdminsConfig[player].violationsLog.AIMViolation++;
                        else
                        {
                            player.ChatMessage(Lang("ShowD3", player.UserIDString, PlayersViolations.Players[playerID].PlayerName));
                            AdminsConfig[player].violationsLog = new ViolationsLog();
                            return;
                        }

                int result;
                int.TryParse(s, out result);

                if (result == 0)
                    result = AdminsConfig[player].violationsLog.AIMViolation;

                adminMsg += Lang("ShowAIMD3", player.UserIDString, result);
                player.ChatMessage(adminMsg);

                int i = 1;
                
				foreach (KeyValuePair<string, AIMViolationData> list in PlayersViolations.Players[playerID].AIMViolations)
                {
                    if (i == result)
                    {
                        AIMViolationData aimvd = new AIMViolationData();
                        aimvd = PlayersViolations.Players[playerID].AIMViolations[list.Key];
                        Vector3 projectilePosition = PlayersViolations.Players[playerID].AIMViolations[list.Key].startProjectilePosition;
                        Vector3 projectileVelocity = PlayersViolations.Players[playerID].AIMViolations[list.Key].startProjectileVelocity;
                        Vector3 PointStart = PlayersViolations.Players[playerID].AIMViolations[list.Key].hitInfoPointStart;
                        Vector3 PointEnd = PlayersViolations.Players[playerID].AIMViolations[list.Key].hitInfoPointEnd;
                        Vector3 HitPositionWorld = PlayersViolations.Players[playerID].AIMViolations[list.Key].hitInfoHitPositionWorld;

                        float gravityModifier = PlayersViolations.Players[playerID].AIMViolations[list.Key].hitInfoProjectilePrefabGravityModifier;
                        float drag = PlayersViolations.Players[playerID].AIMViolations[list.Key].hitInfoProjectilePrefabDrag;
                        string putsmsg = "";
                        bool AIMViolation = false;
						List<TrajectorySegment> trajectorySegments = new List<TrajectorySegment>();
						List<TrajectorySegment> trajectorySegmentsRev = new List<TrajectorySegment>();
						
						aimvd.hitsData[0].isHitPointNearProjectileTrajectoryLastSegmentEndPoint = true;
						aimvd.hitsData[0].isHitPointOnProjectileTrajectory = true;
						aimvd.hitsData[0].isProjectileStartPointAtEndReverseProjectileTrajectory = true;
						aimvd.hitsData[0].isHitPointNearProjectilePlane = true;
						aimvd.hitsData[0].isLastSegmentOnProjectileTrajectoryPlane = true;

						if (aimvd.hitsData.Count == 2)
						{
							aimvd.hitsData[0].hitPositionWorld = aimvd.hitsData[1].hitPositionWorld;
							aimvd.hitsData[0].hitPointStart = aimvd.hitsData[1].hitPointStart;
							aimvd.hitsData[0].hitPointEnd = aimvd.hitsData[1].hitPointEnd;
							aimvd.hitsData.RemoveAt(1);
						}
						
                        AIMViolation = ProcessProjectileTrajectory(out aimvd, aimvd, out trajectorySegments, out trajectorySegmentsRev, gravityModifier, drag);

						Puts($"Player {PlayersViolations.Players[playerID].PlayerName} physicsSteps={aimvd.physicsSteps}");

						if (AIMViolation && aimvd.hitsData.Count == 1 && trajectorySegments.Count > 0 && trajectorySegmentsRev.Count > 	0)
						{
							float lengthLastSegmentProjectileTrajectory = Vector3.Distance(aimvd.hitsData[0].lastSegmentPointEnd, aimvd.hitsData[0].lastSegmentPointStart);
							float lengthLastSegmentReverseProjectileTrajectory = Vector3.Distance(aimvd.hitsData[0].hitPointEnd, aimvd.hitsData[0].hitPointStart);
							
							Vector3 pointStartProjectedOnLastSegment = ProjectPointOnLine(aimvd.hitsData[0].lastSegmentPointStart, (aimvd.hitsData[0].lastSegmentPointEnd - aimvd.hitsData[0].lastSegmentPointStart).normalized, aimvd.hitsData[0].hitPointStart);
							Vector3 pointEndProjectedOnLastSegment = ProjectPointOnLine(aimvd.hitsData[0].lastSegmentPointStart, (aimvd.hitsData[0].lastSegmentPointEnd - aimvd.hitsData[0].lastSegmentPointStart).normalized, aimvd.hitsData[0].hitPointEnd);
	
							if (Mathf.Abs(Vector3.Distance(pointStartProjectedOnLastSegment, aimvd.hitsData[0].hitPointStart) - Vector3.Distance(pointEndProjectedOnLastSegment, aimvd.hitsData[0].hitPointEnd)) > 0.05f)
							{								
								HitData hitData1 = new HitData();
								HitData hitData2 = new HitData();
								if (IsRicochet(trajectorySegments, trajectorySegmentsRev, out hitData1, out hitData2, aimvd.physicsSteps))
								{
									hitData1.startProjectilePosition = aimvd.hitsData[0].startProjectilePosition;
									hitData1.startProjectileVelocity = aimvd.hitsData[0].startProjectileVelocity;
                        
									hitData2.hitPositionWorld = HitPositionWorld;
									hitData2.hitPointStart = PointStart;
									hitData2.hitPointEnd = PointEnd;	
														
									aimvd.hitsData.Clear();
								
									aimvd.hitsData.Add(hitData1);
									aimvd.hitsData.Add(hitData2);
                        
									putsmsg = "";
									AIMViolation = ProcessProjectileTrajectory(out aimvd, aimvd, out trajectorySegments, out trajectorySegmentsRev, gravityModifier, drag);
								}	
							}							
						}

                        if (Vector3.Distance(aimvd.playerEyesPosition, aimvd.startProjectilePosition) > _config.playerEyesPositionToProjectileInitialPositionDistanceForgiveness)
                        {
                        	AIMViolation = true;
                        	aimvd.isPlayerPositionToProjectileStartPositionDistanceViolation = true;
                        }

                    //  DrawProjectileTrajectory(player, _config.drawTime, aimvd, Color.blue);
                    //  DrawReverseProjectileTrajectory(player, _config.drawTime, aimvd, Color.green);

                        player.ConsoleMessage($"aimvd.calculatedTravelDistance = {aimvd.calculatedTravelDistance}");

                        player.ConsoleMessage($"\n{putsmsg}");
                        player.ConsoleMessage($"____end log____1");

                        DrawAIMViolationsData(player, aimvd, false);

                        DDrawSphereToAdmin(null, 60f, Color.red, HitPositionWorld, 0.02f);
					
                        break;
                    }
                    i++;
                }
            }
        }

        private void ShowInRockViolations(BasePlayer player, string[] args)
        {
            if (player == null)
                return;

            if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName) || !AdminsConfig.ContainsKey(player))
                return;

            string s = null;
			string adminMsg;
            ulong id = 0;
			ulong playerID = 0;

            if (args.Length == 2)
                if (args[1] == "0")
                {
                    player.ChatMessage(Lang("ShowD1", player.UserIDString));
                    AdminsConfig[player].violationsLog = new ViolationsLog();
                }
                else
                    s = args[1];

            string user = args[0];

            if (user.Contains("765"))
                ulong.TryParse(args[0], out id);

            foreach (KeyValuePair<ulong, PlayerViolationsData> list in PlayersViolations.Players)
                if (PlayersViolations.Players[list.Key].PlayerID == id || PlayersViolations.Players[list.Key].PlayerName.Contains(user, CompareOptions.IgnoreCase))
                    playerID = PlayersViolations.Players[list.Key].PlayerID;

            if (playerID == 0)
                player.ChatMessage(Lang("ShowD2", player.UserIDString));
            else
            {
                if (PlayersViolations.Players[playerID].inRockViolations.Count == 0)
                    player.ChatMessage(Lang("ShowIRD1", player.UserIDString, PlayersViolations.Players[playerID].PlayerName));
                else
                {
                    adminMsg = Lang("Player", player.UserIDString, PlayersViolations.Players[playerID].PlayerName);

                    if ((long)AdminsConfig[player].violationsLog.steamID != (long)PlayersViolations.Players[playerID].PlayerID)
                    {
                        AdminsConfig[player].violationsLog.InRockViolation = 1;
                        AdminsConfig[player].violationsLog.steamID = PlayersViolations.Players[playerID].PlayerID;

                        adminMsg += Lang("ShowIRD2", player.UserIDString, PlayersViolations.Players[playerID].inRockViolations.Count);
                    }
                    else
                        if (s == null)
                            if (PlayersViolations.Players[playerID].inRockViolations.Count >= AdminsConfig[player].violationsLog.InRockViolation + 1)
                                AdminsConfig[player].violationsLog.InRockViolation++;
                            else
                            {
                                player.ChatMessage(Lang("NoMoreViolations", player.UserIDString, PlayersViolations.Players[playerID].PlayerName));
                                AdminsConfig[player].violationsLog = new ViolationsLog();
                                return;
                            }

                    int result;
                    int.TryParse(s, out result);

                    if (result == 0)
                        result = AdminsConfig[player].violationsLog.InRockViolation;

                    adminMsg += Lang("ShowIRD3", player.UserIDString, result);
                    player.ChatMessage(adminMsg + "\n");

                    int i = 1;
                    foreach (KeyValuePair<string, InRockViolationsData> list in PlayersViolations.Players[playerID].inRockViolations)
                    {
                        if (i == result)
                        {
							DrawInRockViolationsData(player, PlayersViolations.Players[playerID].PlayerName, $"{playerID}", i, PlayersViolations.Players[playerID].inRockViolations[list.Key], true);
                            player.ConsoleMessage("<color=green>Arkan:</color>\n" + Lang("Attacker", player.UserIDString) + ": " + PlayersViolations.Players[playerID].PlayerName + "/" + PlayersViolations.Players[playerID].PlayerID + "\n" + Lang("IRViolationNum", player.UserIDString) + result + "\n" + Lang("Weapon", player.UserIDString) + ": " + PlayersViolations.Players[playerID].inRockViolations[list.Key].inRockViolationsData[1].firedProjectile.weaponShortName + "\n" + Lang("Ammo", player.UserIDString) + ": " + PlayersViolations.Players[playerID].inRockViolations[list.Key].inRockViolationsData[1].firedProjectile.ammoShortName + "\n");

							for (int j = 1; j <= PlayersViolations.Players[playerID].inRockViolations[list.Key].inRockViolationsData.Count; j++)
							{
								player.ConsoleMessage(Lang("ProjectileID", player.UserIDString, j) + " | " + Lang("Target", player.UserIDString) + ": " + PlayersViolations.Players[playerID].inRockViolations[list.Key].inRockViolationsData[j].targetName + "/" + PlayersViolations.Players[playerID].inRockViolations[list.Key].inRockViolationsData[j].targetID + " | " + Lang("HitPart", player.UserIDString) + ": " + PlayersViolations.Players[playerID].inRockViolations[list.Key].inRockViolationsData[j].targetBodyPart + " | " + Lang("Damage", player.UserIDString) + ": " + PlayersViolations.Players[playerID].inRockViolations[list.Key].inRockViolationsData[j].targetDamage);
							}
							
							break;
                        }
                        i++;
                    }
                }
            }
        }

        //Two non-parallel lines which may or may not touch each other have a point on each line which are closest
        //to each other. This function finds those two points. If the lines are not parallel, the function 
        //outputs true, otherwise false.
        private bool ClosestPointsOnTwoLines(out Vector3 closestPointLine1, out Vector3 closestPointLine2, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
        {
            closestPointLine1 = Vector3.zero;
            closestPointLine2 = Vector3.zero;

            float a = Vector3.Dot(lineVec1, lineVec1);
            float b = Vector3.Dot(lineVec1, lineVec2);
            float e = Vector3.Dot(lineVec2, lineVec2);

            float d = a * e - b * b;

            //lines are not parallel
            if (d != 0.0f)
            {
                Vector3 r = linePoint1 - linePoint2;
                float c = Vector3.Dot(lineVec1, r);
                float f = Vector3.Dot(lineVec2, r);

                float s = (b * f - c * e) / d;
                float t = (a * f - c * b) / d;

                closestPointLine1 = linePoint1 + lineVec1 * s;
                closestPointLine2 = linePoint2 + lineVec2 * t;

                return true;
            }
            else
                return false;
        }

        //This function returns a point which is a projection from a point to a line.
        //The line is regarded infinite. If the line is finite, use ProjectPointOnLineSegment() instead.
        private Vector3 ProjectPointOnLine(Vector3 linePoint, Vector3 lineVec, Vector3 point)
        {
            //get vector from point on line to point in space
            Vector3 linePointToPoint = point - linePoint;

            float t = Vector3.Dot(linePointToPoint, lineVec);

            return linePoint + lineVec * t;
        }

        //This function returns a point which is a projection from a point to a line segment.
        //If the projected point lies outside of the line segment, the projected point will 
        //be clamped to the appropriate line edge.
        //If the line is infinite instead of a segment, use ProjectPointOnLine() instead.
        private Vector3 ProjectPointOnLineSegment(Vector3 linePoint1, Vector3 linePoint2, Vector3 point, out int side)
        {
            Vector3 vector = linePoint2 - linePoint1;

            Vector3 projectedPoint = ProjectPointOnLine(linePoint1, vector.normalized, point);

            side = PointOnWhichSideOfLineSegment(linePoint1, linePoint2, projectedPoint);

            //The projected point is on the line segment
            if (side == 0)
                return projectedPoint;

            if (side == 1)
                return linePoint1;

            if (side == 2)
                return linePoint2;

            //output is invalid
            return Vector3.zero;
        }

        //This function finds out on which side of a line segment the point is located.
        //The point is assumed to be on a line created by linePoint1 and linePoint2. If the point is not on
        //the line segment, project it on the line using ProjectPointOnLine() first.
        //Returns 0 if point is on the line segment.
        //Returns 1 if point is outside of the line segment and located on the side of linePoint1.
        //Returns 2 if point is outside of the line segment and located on the side of linePoint2.
        private int PointOnWhichSideOfLineSegment(Vector3 linePoint1, Vector3 linePoint2, Vector3 point)
        {
            Vector3 lineVec = linePoint2 - linePoint1;
            Vector3 pointVec = point - linePoint1;

            float dot = Vector3.Dot(pointVec, lineVec);

            //point is on side of linePoint2, compared to linePoint1
            if (dot > 0)
            {
                //point is on the line segment
                if (pointVec.magnitude <= lineVec.magnitude)
                    return 0;

                //point is not on the line segment and it is on the side of linePoint2
                else
                    return 2;
            }

            //Point is not on side of linePoint2, compared to linePoint1.
            //Point is not on the line segment and it is on the side of linePoint1.
            else
                return 1;
        }

		private bool IsPointInRock(Vector3 pointPosition, float distance, out int rocksUnderPoint)
		{
			rocksUnderPoint = 0;
			
			RaycastHit[] hits = Physics.RaycastAll(new Ray(pointPosition + Vector3.down * distance, Vector3.up), distance, world_defaultLayer);
			foreach (RaycastHit hit in hits)
            {
                MeshCollider collider = hit.collider.GetComponent<MeshCollider>();
                if (collider == null || !collider.sharedMesh.name.StartsWith("rock_"))
                    continue;
				
				RaycastHit hitInfo;
				if (!hit.collider.Raycast(new Ray(pointPosition, Vector3.down), out hitInfo, distance))
					return true;
				else
					rocksUnderPoint += 1;
            }

			return false;			
		}
		
        private void ProcessShots(BasePlayer player)
        {
            if (player != null)
				if (PlayersFiredProjectlesData.ContainsKey(player.userID))
                    if (UnityEngine.Time.realtimeSinceStartup - PlayersFiredProjectlesData[player.userID].lastFiredTime >= 2f && !PlayersFiredProjectlesData[player.userID].isChecked)
                    {
						int firedShotsDataCnt = 0;
						uint curWeaponUID = 0;
						string curAmmoName = "";
						double curFiredTime = 0;
						float timeIntervalBetweenShots;
						float maxTimeIntervalBetweenShots = 0f;
						float NRProbabilityModifier = 1f;
						bool isFirstShot = true;
						
                        Dictionary<int, FiredShotsData> firedShotsData = new Dictionary<int, FiredShotsData>();
                        FiredShotsData fsd;
                        firedShotsDataCnt++;
                        FiredProjectile curFiredProjectile;
        
                        foreach (KeyValuePair<int, FiredProjectile> list in PlayersFiredProjectlesData[player.userID].firedProjectiles.ToArray())
                        {
                            curFiredProjectile = PlayersFiredProjectlesData[player.userID].firedProjectiles[list.Key];
        
                            if (!curFiredProjectile.isChecked)
                            {
                                if (!_config.weaponsConfig[curFiredProjectile.weaponShortName].NRDetectEnabled)
                                {
                                    curFiredProjectile.isChecked = true;
                                    continue;
                                }
        
                                if (isFirstShot)
                                {
                                    curFiredProjectile.isChecked = true;
                                    PlayersFiredProjectlesData[player.userID].firedProjectiles[list.Key] = curFiredProjectile;
                                    firedShotsData.Add(firedShotsData.Count + 1, fsd = new FiredShotsData());
                                    firedShotsData[firedShotsData.Count].firedShots.Add(list.Key, curFiredProjectile);
                                    curWeaponUID = curFiredProjectile.weaponUID;
                                    curAmmoName = curFiredProjectile.ammoShortName;
                                    curFiredTime = curFiredProjectile.firedTime.Ticks;
                                    maxTimeIntervalBetweenShots = _config.weaponsConfig[curFiredProjectile.weaponShortName].weaponMaxTimeShotsInterval;
                                    firedShotsData[firedShotsData.Count].weaponShortName = curFiredProjectile.weaponShortName;
                                    firedShotsData[firedShotsData.Count].ammoShortName = curFiredProjectile.ammoShortName;
                                    firedShotsData[firedShotsData.Count].attachments = curFiredProjectile.attachments;
                                    isFirstShot = false;
                                    NRProbabilityModifier = curFiredProjectile.NRProbabilityModifier;
                                    continue;
                                }
        
                                timeIntervalBetweenShots = (float)(curFiredProjectile.firedTime.Ticks - curFiredTime) / 10000000f;
                                
								if (!(curFiredProjectile.weaponUID == curWeaponUID) || !(curFiredProjectile.ammoShortName == curAmmoName) || timeIntervalBetweenShots > maxTimeIntervalBetweenShots)
                                {
                                    firedShotsDataCnt++;
                                    firedShotsData.Add(firedShotsDataCnt, fsd = new FiredShotsData());
                                    curFiredProjectile.isChecked = true;
                                    PlayersFiredProjectlesData[player.userID].firedProjectiles[list.Key] = curFiredProjectile;
                                    firedShotsData[firedShotsData.Count].firedShots.Add(list.Key, curFiredProjectile);
                                    curWeaponUID = curFiredProjectile.weaponUID;
                                    curAmmoName = curFiredProjectile.ammoShortName;
                                    curFiredTime = curFiredProjectile.firedTime.Ticks;
                                    maxTimeIntervalBetweenShots = _config.weaponsConfig[curFiredProjectile.weaponShortName].weaponMaxTimeShotsInterval;
                                    firedShotsData[firedShotsData.Count].weaponShortName = curFiredProjectile.weaponShortName;
									if (firedShotsData[firedShotsData.Count].attachments.Count < curFiredProjectile.attachments.Count) 
										firedShotsData[firedShotsData.Count].attachments = curFiredProjectile.attachments;
                                    continue;
                                }
								
                                curFiredProjectile.isChecked = true;
                                PlayersFiredProjectlesData[player.userID].firedProjectiles[list.Key] = curFiredProjectile;
                                firedShotsData[firedShotsData.Count].firedShots.Add(list.Key, curFiredProjectile);
                                curFiredTime = curFiredProjectile.firedTime.Ticks;
                            }
                        }
        
                        PlayersFiredProjectlesData[player.userID].isChecked = true;
        
                        if (firedShotsData != null)
							foreach (KeyValuePair<int, FiredShotsData> list in firedShotsData.ToArray().Where(x => (firedShotsData[x.Key].firedShots.Count >= _config.weaponsConfig[firedShotsData[firedShotsData.Count].weaponShortName].NRMinShotsCountToCheck)))
								ProcessFiredShotsBlock(player, firedShotsData[list.Key], NRProbabilityModifier);
                    }
        }

        private void ProcessFiredShotsBlock(BasePlayer player, FiredShotsData fsd, float NRProbabilityModifier)
        {
            if (player != null && fsd != null)
            {
                int shotsCnt = 0;
                int violationProbabilityForgiveness = 0;
                int prevKey = 0;
                int curKey;
                int NoRecoilViolationsCnt = 0;
                int MoveCntShot = 0;
                int firstKey = 0;
                float angleBetweenShots = 0f;
				float angleWithVectorUpSum = 0f;
                float recoilScreenDistance = 0f;
                string _text = "";
				string shotDataTxt = "";
                bool isNoRecoil = false;
                bool isPrevNoRecoilViolation = false;
                bool isShootedInMotion = false;
                Vector2 scrPoint1 = new Vector2(500, 500);
                Vector2 scrPoint2 = new Vector2();
                Vector3 closestPointLine1 = new Vector3();
                Vector3 closestPointLine2 = new Vector3();
                Vector3 prevIntersectionPoint = new Vector3();
                Vector3 prevIPoint = new Vector3();
                Vector3 point1 = new Vector3();
                Vector3 point2 = new Vector3();
                Matrix4x4 viewMatrix = new Matrix4x4();
                Matrix4x4 perspectiveMatrix = new Matrix4x4();
                Matrix4x4 worldMatrix = Matrix4x4.identity;
                NoRecoilViolationData violationData = new NoRecoilViolationData();
                FiredProjectile prevFiredProjectile = new FiredProjectile();
                FiredProjectile curFiredProjectile = new FiredProjectile();
                SuspiciousProjectileData spd = new SuspiciousProjectileData();
				Dictionary<int, string> shotsList = new Dictionary<int, string>();

                foreach (KeyValuePair<int, FiredProjectile> list in fsd.firedShots.ToArray())
                {
                    curFiredProjectile = fsd.firedShots[list.Key];

                    if (curFiredProjectile.isMounted)
                    {
                        violationData.isMounted = true;
						Quaternion q = new Quaternion(curFiredProjectile.mountParentRotation.x, curFiredProjectile.mountParentRotation.y, curFiredProjectile.mountParentRotation.z, curFiredProjectile.mountParentRotation.w); 
                        viewMatrix = Matrix4x4.LookAt(curFiredProjectile.mountParentPosition, curFiredProjectile.mountParentPosition + (q * Vector3.forward), Vector3.up).inverse;
                        curFiredProjectile.projectileVelocity = viewMatrix.MultiplyPoint(curFiredProjectile.projectilePosition + curFiredProjectile.projectileVelocity);
                        curFiredProjectile.projectilePosition = viewMatrix.MultiplyPoint(curFiredProjectile.projectilePosition);
                        curFiredProjectile.projectileVelocity -= curFiredProjectile.projectilePosition;
                    }

                    curKey = list.Key;
                    isNoRecoil = false;
                    violationData.ShotsCnt++;
                    shotsCnt++;
					
					angleWithVectorUpSum += Vector3.Angle(Vector3.Normalize(curFiredProjectile.projectileVelocity), Vector3.up);

                    if (shotsCnt == 1)
                    {
                        firstKey = list.Key;
                        prevFiredProjectile = curFiredProjectile;
                        prevKey = curKey;
                        spd = new SuspiciousProjectileData();
                        violationData.suspiciousNoRecoilShots.Add(shotsCnt, new SuspiciousProjectileData());
                        spd.projectile1ID = prevKey;
                        spd.projectile2ID = curKey;
                        spd.projectile1Position = prevFiredProjectile.projectilePosition;
                        spd.projectile1Velocity = prevFiredProjectile.projectileVelocity;
                        spd.projectile2Position = curFiredProjectile.projectilePosition;
                        spd.projectile2Velocity = curFiredProjectile.projectileVelocity;
                        violationData.ammoShortName = curFiredProjectile.ammoShortName;
                        violationData.weaponShortName = curFiredProjectile.weaponShortName;
                        spd.timeStamp = curFiredProjectile.firedTime;
                        spd.isNoRecoil = false;
                        spd.isShootedInMotion = false;
                        violationData.suspiciousNoRecoilShots[shotsCnt] = spd;
                        continue;
                    }

                    float timeInterval = (float)(curFiredProjectile.firedTime.Ticks - prevFiredProjectile.firedTime.Ticks) / 10000000f;

                    if (Vector3.Distance(prevFiredProjectile.projectilePosition, curFiredProjectile.projectilePosition) <= _config.EPSILON)
                    {
                        MoveCntShot = 0;
                        isShootedInMotion = false;
                        angleBetweenShots = Vector3.Angle(Vector3.Normalize(curFiredProjectile.projectileVelocity), Vector3.Normalize(prevFiredProjectile.projectileVelocity));

						shotDataTxt = Lang("StandingShooting", null) + " | " + Lang("RecoilAngle", null, angleBetweenShots) + " | " + Lang("FireTimeInterval", null, timeInterval);

                        if (angleBetweenShots < _config.NRViolationAngle)
                        {
                            isNoRecoil = true;
                            isPrevNoRecoilViolation = true;
                        }
                    }
                    else
                    {
                        MoveCntShot++;
                        isShootedInMotion = true;
                        
						if (ClosestPointsOnTwoLines(out closestPointLine1, out closestPointLine2, prevFiredProjectile.projectilePosition, prevFiredProjectile.projectileVelocity, curFiredProjectile.projectilePosition, curFiredProjectile.projectileVelocity))
                        {
							shotDataTxt = Lang("ShootingOnMove", null) + "\n" + Lang("ClosestPoint", null, closestPointLine1, closestPointLine2, prevIntersectionPoint) + "\n" + Lang("FireTimeInterval", null, timeInterval);

                            viewMatrix = Matrix4x4.LookAt(curFiredProjectile.projectilePosition, curFiredProjectile.projectilePosition + curFiredProjectile.projectileVelocity, Vector3.up).inverse;

                            perspectiveMatrix = Matrix4x4.Perspective(70f, 1.0f, 0.01f, 1000f);

                            point1 = viewMatrix.MultiplyPoint(closestPointLine1);

                            if (point1.z > 1)
                            {
                                if (MoveCntShot == 1)
                                    point2 = viewMatrix.MultiplyPoint(closestPointLine1);
                                else
                                    point2 = viewMatrix.MultiplyPoint(prevIntersectionPoint);

                                point2 = perspectiveMatrix.MultiplyPoint(point2);

                                scrPoint2.x = ((point2.x + 1.0f) / 2.0f) * 1000;
                                scrPoint2.y = ((point2.y + 1.0f) / 2.0f) * 1000;

                                angleBetweenShots = Vector3.Angle(curFiredProjectile.projectileVelocity, closestPointLine1 - curFiredProjectile.projectilePosition);
                                recoilScreenDistance = Vector2.Distance(scrPoint1, scrPoint2);

								shotDataTxt += "\n" + Lang("RecoilAngle", null, angleBetweenShots);

                                if (angleBetweenShots < _config.NRViolationAngle && recoilScreenDistance < _config.NRViolationScreenDistance)
                                {
                                    isNoRecoil = true;
                                    isPrevNoRecoilViolation = true;
                                }

                                if (MoveCntShot > 0 && isNoRecoil == false)
                                    MoveCntShot = 0;
                            }

                            prevIPoint = prevIntersectionPoint;
                            prevIntersectionPoint = closestPointLine2;

							shotDataTxt += "\n" + Lang("ScreenCoords", null, scrPoint1, scrPoint2) + "\n" + Lang("DistanceBetweenTwoPoints", null, recoilScreenDistance) + "\n";
                        }
                    }

                    if (isPrevNoRecoilViolation && !isNoRecoil)
                    {
                        isPrevNoRecoilViolation = false;
                        violationProbabilityForgiveness++;
                    }

                    curFiredProjectile.isChecked = true;

                    if (isNoRecoil)
                    {
                        violationData.NRViolationsCnt++;
                        NoRecoilViolationsCnt++;
                        spd = new SuspiciousProjectileData();

                        spd.projectile1ID = prevKey;
                        spd.projectile2ID = curKey;
                        spd.projectile1Position = prevFiredProjectile.projectilePosition;
                        spd.projectile1Velocity = prevFiredProjectile.projectileVelocity;
                        spd.projectile2Position = curFiredProjectile.projectilePosition;
                        spd.projectile2Velocity = curFiredProjectile.projectileVelocity;
                        spd.closestPointLine1 = closestPointLine1;
                        spd.closestPointLine2 = closestPointLine2;
                        spd.recoilAngle = angleBetweenShots;
                        spd.recoilScreenDistance = recoilScreenDistance;
                        spd.isNoRecoil = true;
                        spd.isShootedInMotion = isShootedInMotion;
                        spd.timeInterval = timeInterval;
                        spd.timeStamp = curFiredProjectile.firedTime;
                        spd.prevIntersectionPoint = prevIPoint;

                        angleBetweenShots = 0f;
                        recoilScreenDistance = 0f;

                        violationData.suspiciousNoRecoilShots.Add(shotsCnt, new SuspiciousProjectileData());
                        violationData.suspiciousNoRecoilShots[shotsCnt] = spd;

                        if (!violationData.suspiciousNoRecoilShots[shotsCnt - 1].isNoRecoil)
                        {
                            NoRecoilViolationsCnt++;
                            spd = violationData.suspiciousNoRecoilShots[shotsCnt - 1];
                            spd.isNoRecoil = true;
                            violationData.suspiciousNoRecoilShots[shotsCnt - 1] = spd;
                        }
                    }
                    else
                    {
                        violationData.NRViolationsCnt++;
                        spd = new SuspiciousProjectileData();

                        spd.projectile1ID = prevKey;
                        spd.projectile2ID = curKey;
                        spd.projectile1Position = prevFiredProjectile.projectilePosition;
                        spd.projectile1Velocity = prevFiredProjectile.projectileVelocity;
                        spd.projectile2Position = curFiredProjectile.projectilePosition;
                        spd.projectile2Velocity = curFiredProjectile.projectileVelocity;
                        spd.isNoRecoil = false;
                        spd.timeInterval = timeInterval;
                        spd.timeStamp = curFiredProjectile.firedTime;

                        violationData.suspiciousNoRecoilShots.Add(shotsCnt, new SuspiciousProjectileData());
                        violationData.suspiciousNoRecoilShots[shotsCnt] = spd;
                    }

                    prevFiredProjectile = curFiredProjectile;
                    prevKey = curKey;
					shotsList.Add(list.Key, shotDataTxt);
                }

                float violationProbability = ((NoRecoilViolationsCnt * 100f) / (fsd.firedShots.Count + violationProbabilityForgiveness)) * NRProbabilityModifier;

                if (violationProbability > _config.weaponsConfig[fsd.weaponShortName].NRViolationProbability && (angleWithVectorUpSum/fsd.firedShots.Count) > 30f)
                {
                    _text = "\n" + Lang("AttachmentsCount", null) + " = " + fsd.attachments.Count;
                    
					if (fsd.attachments.Count > 0)
                        for (int j = 0; j < fsd.attachments.Count; j++)
                            _text += "\n" + Lang("Attachment", null) + " - " + fsd.attachments[j];

                    violationData.mountParentPosition = fsd.firedShots[firstKey].mountParentPosition;
                    violationData.mountParentRotation = fsd.firedShots[firstKey].mountParentRotation;
                    violationData.violationProbability = violationProbability;
                    violationData.attachments = fsd.attachments;

                    AddNoRecoilViolationToPlayer(player, violationData);
					
                    int NRViolationsCnt = PlayersViolations.Players[player.userID].noRecoilViolations.Count;
					
			        if (Interface.CallHook("API_ArkanOnNoRecoilViolation", player, NRViolationsCnt, JsonConvert.SerializeObject(violationData)) != null)
					{
						return;
					}
					
                    string conText = Lang("NRDetection", null) + "\n" + Lang("PlayerTxt", null) + " " + player.displayName + "/" + player.userID + "\n" + Lang("NRViolationNum", null) + PlayersViolations.Players[player.userID].noRecoilViolations.Count + "\n" + Lang("ShotsCount", null) + " " + violationData.suspiciousNoRecoilShots.Count + "\n" + Lang("Probability", null) + " " + violationProbability + "%" + _text + "\n" + Lang("Weapon", null) + " - " + violationData.weaponShortName + " \n" + Lang("Ammo", null) + " - " + violationData.ammoShortName;

					foreach (KeyValuePair<int, string> list in shotsList.ToArray())
						conText += "\n" + Lang("ProjectileID", null, list.Key) + " | " + shotsList[list.Key];

                    Puts(conText);
					 
					if (_config.DiscordNRReportEnabled)
						if (DiscordMessages == null)
							PrintWarning(Lang("DiscordWarning2", null));
						else
						{
							List<EmbedFieldList> fields = new List<EmbedFieldList>();
							
							string dmPlayer = $"[{player.displayName}\n{player.UserIDString}](https://steamcommunity.com/profiles/{player.UserIDString})";
							if (dmPlayer.Length == 0) dmPlayer = stringNullValueWarning;
							fields.Add(new EmbedFieldList()
							{
								name = Lang("PlayerTxt", null),
								inline = false,
								value = dmPlayer
							});
							
							string dmVNum = $"{PlayersViolations.Players[player.userID].noRecoilViolations.Count}";
							if (dmVNum.Length == 0) dmVNum = stringNullValueWarning;
							fields.Add(new EmbedFieldList()
							{
								name = Lang("NRViolationNum", null),
								inline = true,
								value = dmVNum
							});
							
							string dmProbability = $"{violationProbability}%";
							if (dmProbability.Length == 0) dmProbability = stringNullValueWarning;
							fields.Add(new EmbedFieldList()
							{
								name = Lang("Probability", null),
								inline = true,
								value = dmProbability
							});
							
							string dmShotsCount = $"{violationData.suspiciousNoRecoilShots.Count}";
							if (dmShotsCount.Length == 0) dmShotsCount = stringNullValueWarning;
							fields.Add(new EmbedFieldList()
							{
								name = Lang("ShotsCount", null),
								inline = true,
								value = dmShotsCount
							});
							
							string dmWeapon = violationData.weaponShortName;
							if (dmWeapon.Length == 0) dmWeapon = stringNullValueWarning;
							fields.Add(new EmbedFieldList()
							{
								name = Lang("Weapon", null),
								inline = true,
								value = dmWeapon
							});
							
							string dmAmmo = violationData.ammoShortName;
							if (dmAmmo.Length == 0) dmAmmo = stringNullValueWarning;
							fields.Add(new EmbedFieldList()
							{
								name = Lang("Ammo", null),
								inline = true,
								value = dmAmmo
							});
							
							if (fsd.attachments.Count == 0)
							{
								fields.Add(new EmbedFieldList()
								{
									name = Lang("AttachmentsCount", null) + " = " + fsd.attachments.Count,
									inline = true,
									value = Lang("NoAttachments", null)
								});
							}
							else
							{
								string dmAttachmentsList = "";
								for (int j = 0; j < fsd.attachments.Count; j++)
									dmAttachmentsList += fsd.attachments[j] + "\n";
								
								if (dmAttachmentsList.Length == 0) dmAttachmentsList = stringNullValueWarning;								
								fields.Add(new EmbedFieldList()
								{
									name = Lang("AttachmentsCount", null) + " = " + fsd.attachments.Count,
									inline = true,
									value = dmAttachmentsList
								});
							}
							
							string dmProjectileData = "";
							foreach (KeyValuePair<int, string> list in shotsList.ToArray())
							{
								dmProjectileData = shotsList[list.Key];
								if (dmProjectileData.Length == 0) dmProjectileData = stringNullValueWarning;
								fields.Add(new EmbedFieldList()
								{
									name = Lang("ProjectileID", null, list.Key),
									inline = false,
									value = dmProjectileData
								});
							}
							
							var fieldsObject = fields.Cast<object>().ToArray();
							
							string json = JsonConvert.SerializeObject(fieldsObject);

							DiscordMessages?.Call("API_SendFancyMessage", _config.DiscordNRWebhookURL, "Arkan: " + Lang("NRDetection", null), 39423, json);
						}

                    foreach (var _player in BasePlayer.activePlayerList.Where(x => x.IsAdmin && permission.UserHasPermission(x.UserIDString, permName)))
					{
						if (permission.UserHasPermission(_player.UserIDString, permNRReportChat))
							SendReply(_player, Lang("PlayerNRViolation", _player.UserIDString, player.displayName, player.userID, NRViolationsCnt, fsd.firedShots.Count, NoRecoilViolationsCnt, violationProbability));
						
						if (permission.UserHasPermission(_player.UserIDString, permNRReportConsole))
							_player.ConsoleMessage("<color=green>Arkan:</color> " + Lang("PlayerNRViolationCon", _player.UserIDString, player.displayName, player.userID, NRViolationsCnt, fsd.firedShots.Count, NoRecoilViolationsCnt, violationProbability));
						
                        if (permission.UserHasPermission(_player.UserIDString, permNRDrawViolation))
							DrawNoRecoilViolationsData(_player, player.displayName, violationData, false);
					}
                }
            }
        }
		
		private void ShootingInRockCheck(BasePlayer player, FiredProjectile fp, HitInfo info, string bodyPart, float physicsSteps)
		{			
			if (player != null && fp != null && info != null)
			{					
				if (_config.checkBlacklist)
				{
					if (!permission.UserHasPermission(player.UserIDString, permIRBlacklist))
						return;
				}
				else
					if (permission.UserHasPermission(player.UserIDString, permIRWhitelist))
						return;
				
				RaycastHit hit = new RaycastHit();
				int rocksUnderPoint = 0;
				bool isColliderTerrain = Physics.Raycast(fp.projectilePosition + Vector3.up * 250f, Vector3.down, out hit, 250f, terrainLayer);
				bool isPointInRock = IsPointInRock(fp.projectilePosition, _config.inRockCheckDistance, out rocksUnderPoint);

				if (!isPointInRock && !isColliderTerrain)
					return;	

				RaycastHit worldHit = new RaycastHit();
				bool isHit = false;
				float totalDistance = info.ProjectileDistance;
				float dist = 0;
				Vector3 pointStart = new Vector3();
				Vector3 pointEnd = new Vector3();
				
				float fixedDeltaTime = 1f / physicsSteps;
				Vector3 position = fp.projectilePosition;
				Vector3 vector1 = fp.projectileVelocity / physicsSteps;
				Vector3 vector2 = ((Physics.gravity * info.ProjectilePrefab.gravityModifier) / physicsSteps) * fixedDeltaTime;
				float single1 = info.ProjectilePrefab.drag * fixedDeltaTime;
				int segmentsCnt = (int)(physicsSteps * 8);
				
				string lastKey = "";
				int vsCnt = 0;
				int sCnt = 0;
				int layer = 0;

				if (isColliderTerrain && !isPointInRock)
					layer = world_terrainLayer;
				else
					layer = world_defaultLayer;
				
				for (int j = 0; j < segmentsCnt; j++)
                {						
					pointStart = position + vector1;
					pointEnd = position;					
					dist = vector1.magnitude;	
					
					if (totalDistance < dist)
					{
						dist = totalDistance;
						pointStart = pointEnd + ((pointStart - pointEnd).normalized * dist);
					}
					
					isHit = Physics.Raycast(pointStart, (pointEnd - pointStart).normalized, out hit, dist, layer);
					if (isHit)
					{
						if (!isColliderTerrain)
						{
							MeshCollider collider = hit.collider.GetComponent<MeshCollider>();
							if (collider is MeshCollider)
							if (collider == null || !collider.sharedMesh.name.StartsWith("rock_"))
								break;	
						}
						else
						{
							if(Physics.Raycast(fp.projectilePosition + Vector3.up * 0.1f, Vector3.down, out worldHit, 50f, world_defaultLayer))
							{
								MeshCollider worldCollider = worldHit.collider.GetComponent<MeshCollider>();
								if (worldCollider != null)
								{
									if(!worldCollider.sharedMesh.name.StartsWith("rock_") && !isPointInRock)
										break;	
									else
									{
										if (rocksUnderPoint > 0 && !isPointInRock)
											break;
									}
								}
							}
						}
						
						InRockViolationData irvd = new InRockViolationData();
						
						irvd.dateTime = DateTime.Now;
						irvd.physicsSteps = physicsSteps;
						irvd.targetHitDistance = info.ProjectileDistance;
						irvd.targetHitPosition = info.HitPositionWorld;
						irvd.firedProjectile = fp;
						irvd.rockHitPosition = hit.point;
						irvd.targetName = info.HitEntity.ToPlayer().displayName;
                        irvd.targetID = info.HitEntity.ToPlayer().userID.ToString();
						irvd.targetDamage = info.damageTypes.Total();
						irvd.targetBodyPart = bodyPart;
						irvd.projectileID = info.ProjectileID;
						irvd.drag = info.ProjectilePrefab.drag;
						irvd.gravityModifier = info.ProjectilePrefab.gravityModifier;						
												
						if (PlayersViolations.Players.ContainsKey(player.userID))
							if (PlayersViolations.Players[player.userID].inRockViolations.Count > 0)
							{
								lastKey = PlayersViolations.Players[player.userID].inRockViolations.Keys.Last();
								sCnt = PlayersViolations.Players[player.userID].inRockViolations[lastKey].inRockViolationsData.Count;
								vsCnt = PlayersViolations.Players[player.userID].inRockViolations.Count;
								
								if (PlayersViolations.Players[player.userID].inRockViolations[lastKey].inRockViolationsData[sCnt].firedProjectile.weaponShortName == fp.weaponShortName)
									if (PlayersViolations.Players[player.userID].inRockViolations[lastKey].inRockViolationsData[sCnt].firedProjectile.ammoShortName == fp.ammoShortName)
									{									
										float timeIntervalBetweenShots = (float)(irvd.dateTime.Ticks - PlayersViolations.Players[player.userID].inRockViolations[lastKey].inRockViolationsData[sCnt].dateTime.Ticks) / 10000000f;
							
										if (timeIntervalBetweenShots < 0.15625f)								
										{
											PlayersViolations.Players[player.userID].inRockViolations[lastKey].inRockViolationsData.Add(sCnt + 1, new InRockViolationData());
											PlayersViolations.Players[player.userID].inRockViolations[lastKey].inRockViolationsData[sCnt + 1] = irvd;
											
											timer.Once(0.5f, () => InRockNotification(player, lastKey, vsCnt, sCnt + 1));
											
											break;
										}
									}
							}
						
						InRockViolationsData violationData = new InRockViolationsData();
						violationData.dateTime = irvd.dateTime;
						int index = 1;
						violationData.inRockViolationsData.Add(index, new InRockViolationData());
						violationData.inRockViolationsData[index] = irvd;						
	
						AddInRockViolationToPlayer(player, violationData);
						
						lastKey = PlayersViolations.Players[player.userID].inRockViolations.Keys.Last();
						vsCnt = PlayersViolations.Players[player.userID].inRockViolations.Count;
						sCnt = PlayersViolations.Players[player.userID].inRockViolations[lastKey].inRockViolationsData.Count;
						
						timer.Once(0.5f, () => InRockNotification(player, lastKey, vsCnt, sCnt));
						
						break;
					}
					
					position += vector1;

					vector1 += vector2;
					vector1 -= (vector1 * single1);
					
					totalDistance -= dist;
					if (totalDistance < 0f)
						break;
				}
			}
		}	

		private void InRockNotification(BasePlayer player, string key, int vsCnt, int sCnt)
        {
			if (PlayersViolations.Players.ContainsKey(player.userID))
				if (PlayersViolations.Players[player.userID].inRockViolations.ContainsKey(key))
					if (PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData.Count == sCnt)
					{
						string pTxt = Lang("IRDetection", null) + "\n" + Lang("PlayerTxt", null) + " " + player.displayName + "/" + player.userID + "\n" + Lang("IRViolationNum", null) + vsCnt + " " + key + "\n" + Lang("Weapon", null) + " " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[1].firedProjectile.weaponShortName + "\n" + Lang("Ammo", null) + " " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[1].firedProjectile.ammoShortName + "\n" + Lang("ShotsCount", null) + " " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData.Count + "\n";
						for (int j = 1; j <= PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData.Count; j++)
						{
							pTxt += Lang("ProjectileID", null, PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[j].projectileID) + " | " + Lang("Target", null) + ": " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[j].targetName + "/" + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[j].targetID + " | " + Lang("HitPart", null) + ": " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[j].targetBodyPart + " | " + Lang("Damage", null) + ": " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[j].targetDamage + "\n";
						}

						Puts(pTxt);

						if (Interface.CallHook("API_ArkanOnInRockViolation", player, vsCnt, JsonConvert.SerializeObject(PlayersViolations.Players[player.userID].inRockViolations[key])) != null)
						{
							return;
						}
						
						foreach (var _player in BasePlayer.activePlayerList.Where(x => x.IsAdmin && permission.UserHasPermission(x.UserIDString, permName)))
						{
							if (permission.UserHasPermission(_player.UserIDString, permIRReportChat))
								SendReply(_player, Lang("PlayerIRViolation", _player.UserIDString, player.displayName, player.userID, vsCnt, PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData.Count));
							
							if (permission.UserHasPermission(_player.UserIDString, permIRReportConsole))
								_player.ConsoleMessage(Lang("PlayerIRViolationCon", _player.UserIDString, player.displayName, player.userID, vsCnt, PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData.Count));
							
                            if(permission.UserHasPermission(_player.UserIDString, permIRDrawViolation))
								DrawInRockViolationsData(_player, player.displayName, player.UserIDString, vsCnt, PlayersViolations.Players[player.userID].inRockViolations[key], false);
						}	

						if (_config.DiscordIRReportEnabled)
							if (DiscordMessages == null)
								PrintWarning(Lang("DiscordWarning2", null));
							else
							{
								List<EmbedFieldList> fields = new List<EmbedFieldList>();
								
								string dmPlayer = $"[{player.displayName}\n{player.UserIDString}](https://steamcommunity.com/profiles/{player.UserIDString})";
								if (dmPlayer.Length == 0) dmPlayer = stringNullValueWarning;
								fields.Add(new EmbedFieldList()
								{
									name = Lang("PlayerTxt", null),
									inline = true,
									value = dmPlayer
								});
								
								string dmVNum = $"{vsCnt}";
								if (dmVNum.Length == 0) dmVNum = stringNullValueWarning;
								fields.Add(new EmbedFieldList()
								{
									name = Lang("IRViolationNum", null),
									inline = true,
									value = dmVNum
								});

								string dmShotsCnt = $"{sCnt}";
								if (dmShotsCnt.Length == 0) dmShotsCnt = stringNullValueWarning;
								fields.Add(new EmbedFieldList()
								{
									name = Lang("ShotsCount", null),
									inline = true,
									value = dmShotsCnt
								});
								
								string dmWeapon = $"{PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[1].firedProjectile.weaponShortName}";
								if (dmWeapon.Length == 0) dmWeapon = stringNullValueWarning;
								fields.Add(new EmbedFieldList()
								{
									name = Lang("Weapon", null),
									inline = true,
									value = dmWeapon
								});
								
								string dmAmmo = $"{PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[1].firedProjectile.ammoShortName}";
								if (dmAmmo.Length == 0) dmAmmo = stringNullValueWarning;
								fields.Add(new EmbedFieldList()
								{
									name = Lang("Ammo", null),
									inline = true,
									value = dmAmmo
								});								
								
								string dmLogData = "";
								for (int k = 1; k <= PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData.Count; k++)
								{
									dmLogData = Lang("Target", null) + ": " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[k].targetName + "/" + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[k].targetID + ", " + Lang("HitPart", null) + ": " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[k].targetBodyPart + ", " + Lang("Damage", null) + ": " + PlayersViolations.Players[player.userID].inRockViolations[key].inRockViolationsData[k].targetDamage;
									if (dmLogData.Length == 0) dmLogData = stringNullValueWarning;
									fields.Add(new EmbedFieldList()
									{
										name = Lang("ProjectileID", null, k),
										inline = false,
										value = dmLogData
									});				
								}
																															
								var fieldsObject = fields.Cast<object>().ToArray();
								
								string json = JsonConvert.SerializeObject(fieldsObject);

								DiscordMessages?.Call("API_SendFancyMessage", _config.DiscordIRWebhookURL, "Arkan: " + Lang("IRDetection", null), 39423, json);
							}	
					}
		}			

        private bool ProcessProjectileTrajectory(out AIMViolationData aimvd, AIMViolationData aimvdIn, out List<TrajectorySegment> trajectorySegments, out List<TrajectorySegment> trajectorySegmentsRev, float gravityModifier, float drag)
        {
            Vector3 lsVecStart;
			Vector3 lsVecEnd;
			Vector3 lsVecStartRev;
			Vector3 lsVecEndRev;
            bool isLastSegmentOnProjectileTrajectoryPlane;
			bool isHitPointOnProjectileTrajectory;
			bool isProjectileStartPointAtEndReverseProjectileTrajectory;
			bool AIMViolation = false;
            float distanceFromHitPointToProjectilePlane;
			float projectileForgiveness = _config.projectileTrajectoryForgiveness * aimvdIn.forgivenessModifier;
			float _hitDistanceForgiveness = _config.hitDistanceForgiveness * aimvdIn.forgivenessModifier;
			float lengthLastSegmentProjectileTrajectory;
			float lengthLastSegmentReverseProjectileTrajectory;
            int side;
			trajectorySegments = new List<TrajectorySegment>();
			trajectorySegmentsRev = new List<TrajectorySegment>();
						
            aimvd = aimvdIn;
			aimvd.calculatedTravelDistance = 0f;
			
            for (int j = 0; j < aimvd.hitsData.Count; j++)
            {
                isLastSegmentOnProjectileTrajectoryPlane = IsLastSegmentCloseToProjectileTrajectoryPlane(aimvd.hitsData[j], projectileForgiveness, out distanceFromHitPointToProjectilePlane);
                isHitPointOnProjectileTrajectory = IsHitPointCloseToProjectileTrajectory(aimvd.hitsData[j], gravityModifier, drag, projectileForgiveness, out lsVecStart, out lsVecEnd, out aimvd.calculatedTravelDistance, out trajectorySegments, aimvd.physicsSteps);

				lengthLastSegmentProjectileTrajectory = Vector3.Distance(lsVecEnd, lsVecStart);
				lengthLastSegmentReverseProjectileTrajectory = Vector3.Distance(aimvd.hitsData[j].hitPointEnd, aimvd.hitsData[j].hitPointStart);

				if (Mathf.Abs(lengthLastSegmentProjectileTrajectory - lengthLastSegmentReverseProjectileTrajectory) > lengthLastSegmentProjectileTrajectory * 0.05f)
				{						
					Vector3 pointStartProjectedOnLastSegment = ProjectPointOnLine(lsVecStart, (lsVecEnd - lsVecStart).normalized, aimvd.hitsData[j].hitPointStart);
					Vector3 pointEndProjectedOnLastSegment = ProjectPointOnLine(lsVecStart, (lsVecEnd - lsVecStart).normalized, aimvd.hitsData[j].hitPointEnd);
					
					if (Vector3.Distance(aimvd.hitsData[j].hitPointStart, pointStartProjectedOnLastSegment) < _config.projectileTrajectoryForgiveness && Vector3.Distance(aimvd.hitsData[j].hitPointEnd, pointEndProjectedOnLastSegment) < _config.projectileTrajectoryForgiveness && (Mathf.Abs(Vector3.Distance(pointEndProjectedOnLastSegment, pointStartProjectedOnLastSegment)) - lengthLastSegmentReverseProjectileTrajectory) < lengthLastSegmentReverseProjectileTrajectory * 0.02f && (Mathf.Abs(Vector3.Distance(pointStartProjectedOnLastSegment, aimvd.hitsData[j].hitPointStart) - Vector3.Distance(pointEndProjectedOnLastSegment, aimvd.hitsData[j].hitPointEnd)) < 0.05f))
					{
						aimvd.physicsSteps = Mathf.Clamp((float)Math.Round((lengthLastSegmentProjectileTrajectory / lengthLastSegmentReverseProjectileTrajectory) * aimvd.physicsSteps, 1), 30f, 60f);
						isHitPointOnProjectileTrajectory = IsHitPointCloseToProjectileTrajectory(aimvd.hitsData[j], gravityModifier, drag, projectileForgiveness, out lsVecStart, out lsVecEnd, out aimvd.calculatedTravelDistance, out trajectorySegments, aimvd.physicsSteps);
					}
				}

                if (j != aimvd.hitsData.Count - 1)
                {
                    aimvd.hitsData[j].hitPointStart = lsVecStart;
                    aimvd.hitsData[j].hitPointEnd = lsVecEnd;
                }
				
                isProjectileStartPointAtEndReverseProjectileTrajectory = IsProjectileStartPointCloseToAtEndReverseProjectileTrajectory(aimvd.hitsData[j], gravityModifier, drag, projectileForgiveness, out lsVecStartRev, out lsVecEndRev, out trajectorySegmentsRev, aimvd.physicsSteps);

                aimvd.hitsData[j].lastSegmentPointStart = lsVecStart;
                aimvd.hitsData[j].lastSegmentPointEnd = lsVecEnd;
                aimvd.hitsData[j].reverseLastSegmentPointStart = lsVecStartRev;
                aimvd.hitsData[j].reverseLastSegmentPointEnd = lsVecEndRev;

                if (aimvd.hitsData.Count > 1 && j < aimvd.hitsData.Count - 1)
                {
                    float single3 = Vector3.Distance(lsVecStart, aimvd.hitsData[j].hitPositionWorld);
                    float single2 = aimvd.hitsData[j + 1].hitData.inVelocity.magnitude / aimvd.physicsSteps;
                    aimvd.hitsData[j + 1].delta = 1f - single3 * (1f / single2);
                    aimvd.hitsData[j + 1].travelDistance = aimvd.calculatedTravelDistance;
                }

                aimvd.hitsData[j].distanceFromHitPointToProjectilePlane = distanceFromHitPointToProjectilePlane;

                if (isLastSegmentOnProjectileTrajectoryPlane)
                {
                    if (distanceFromHitPointToProjectilePlane < _hitDistanceForgiveness)
                    {
						aimvd.hitsData[j].pointProjectedOnLastSegmentLine = ProjectPointOnLine(lsVecStart, (lsVecEnd - lsVecStart).normalized, aimvd.hitsData[j].hitPositionWorld);
						side = PointOnWhichSideOfLineSegment(lsVecStart, lsVecEnd, aimvd.hitsData[j].pointProjectedOnLastSegmentLine);
	
                        aimvd.hitsData[j].side = side;

                        if (side > 0)
                        {
							if (side == 1)
                            {
								if (Vector3.Distance(aimvd.hitsData[j].hitPositionWorld, lsVecStart) > (_config.projectileTrajectoryForgiveness))	
                                {
                                    AIMViolation = true;
                                    aimvd.hitsData[j].isHitPointNearProjectileTrajectoryLastSegmentEndPoint = false;
                                }
                            }
                            else
                            {
								if (Vector3.Distance(aimvd.hitsData[j].hitPositionWorld, lsVecEnd) > (_config.projectileTrajectoryForgiveness))
                                {
                                    AIMViolation = true;
                                    aimvd.hitsData[j].isHitPointNearProjectileTrajectoryLastSegmentEndPoint = false;
                                }
                            }
                        }

                        if (!isHitPointOnProjectileTrajectory)
                        {
                            AIMViolation = true;
                            aimvd.hitsData[j].isHitPointOnProjectileTrajectory = false;
                        }

                        if (!isProjectileStartPointAtEndReverseProjectileTrajectory)
                        {
                            AIMViolation = true;
                            aimvd.hitsData[j].isProjectileStartPointAtEndReverseProjectileTrajectory = false;
                        }
                    }
                    else
                    {
                        AIMViolation = true;
                        aimvd.hitsData[j].isHitPointNearProjectilePlane = false;
                    }
                }
                else
                {
                    AIMViolation = true;
                    aimvd.hitsData[j].isLastSegmentOnProjectileTrajectoryPlane = false;
                }
            }

            return AIMViolation;
        }		
		
        private void AddNoRecoilViolationToPlayer(BasePlayer player, NoRecoilViolationData noRecoilViolationData)
        {
            if (player != null)
            {
                if (!PlayersViolations.Players.ContainsKey(player.userID))
                {
                    PlayersViolations.Players.Add(player.userID, new PlayerViolationsData());
                    PlayersViolations.Players[player.userID].PlayerID = player.userID;
                    PlayersViolations.Players[player.userID].PlayerName = player.displayName;
                }

                string indexStr = serverTimeStamp + "_" + DateTime.Now.Ticks + "_" + noRecoilViolationData.suspiciousNoRecoilShots[1].projectile2ID + "." + noRecoilViolationData.suspiciousNoRecoilShots[noRecoilViolationData.suspiciousNoRecoilShots.Count].projectile2ID;
                PlayersViolations.Players[player.userID].noRecoilViolations.Add(indexStr, new NoRecoilViolationData());
                PlayersViolations.Players[player.userID].noRecoilViolations[indexStr] = noRecoilViolationData;
                PlayersViolations.lastChangeTime = DateTime.Now;
            }
        }

        private void AddAIMViolationToPlayer(BasePlayer player, AIMViolationData _AIMViolationData)
        {
            if (player != null)
            {
                if (!PlayersViolations.Players.ContainsKey(player.userID))
                {
                    PlayersViolations.Players.Add(player.userID, new PlayerViolationsData());
                    PlayersViolations.Players[player.userID].PlayerID = player.userID;
                    PlayersViolations.Players[player.userID].PlayerName = player.displayName;
                }

                string indexStr = DateTime.Now.Ticks + "_" + _AIMViolationData.projectileID;
                PlayersViolations.Players[player.userID].AIMViolations.Add(indexStr, new AIMViolationData());
                PlayersViolations.Players[player.userID].AIMViolations[indexStr] = _AIMViolationData;
                PlayersViolations.lastChangeTime = DateTime.Now;
            }
        }

        private void AddInRockViolationToPlayer(BasePlayer player, InRockViolationsData InRockViolationData)
        {
            if (player != null)
            {
                if (!PlayersViolations.Players.ContainsKey(player.userID))
                {
                    PlayersViolations.Players.Add(player.userID, new PlayerViolationsData());
                    PlayersViolations.Players[player.userID].PlayerID = player.userID;
                    PlayersViolations.Players[player.userID].PlayerName = player.displayName;
                }

                string indexStr = serverTimeStamp + "_" + DateTime.Now.Ticks;
                PlayersViolations.Players[player.userID].inRockViolations.Add(indexStr, new InRockViolationsData());
                PlayersViolations.Players[player.userID].inRockViolations[indexStr] = InRockViolationData;
                PlayersViolations.lastChangeTime = DateTime.Now;
            }
        }

        private bool IsNPC(BasePlayer player)
        {
            if (player == null) return false;
			
            if (player is NPCPlayer) return true;
			
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L))
                return true;
			
            return false;
        }

		private void AdminLogInit(BasePlayer player)
        {		
 			if (player != null)
			{
				if (!player.IsAdmin || !permission.UserHasPermission(player.UserIDString, permName))
					return;

				if (!AdminsConfig.ContainsKey(player))
                {
                    AdminsConfig.Add(player, new AdminConfig());
                }
			}
		}

        private bool IsLastSegmentCloseToProjectileTrajectoryPlane(HitData hitData, float projectileForgiveness, out float distance)
        {
            Vector3 projectileStartPoint = hitData.startProjectilePosition;
            Vector3 projectileVelocity = hitData.startProjectileVelocity;
            Vector3 pointStart = hitData.hitPointStart;
            Vector3 pointEnd = hitData.hitPointEnd;
            Vector3 hitPoint = hitData.hitPositionWorld;

            Plane projectileTrajectoryPlane = new Plane(projectileStartPoint, projectileStartPoint + Vector3.up, projectileStartPoint + projectileVelocity);
            distance = Math.Abs(projectileTrajectoryPlane.GetDistanceToPoint(hitPoint));
			
            if (Math.Abs(projectileTrajectoryPlane.GetDistanceToPoint(pointStart)) <= projectileForgiveness && Math.Abs(projectileTrajectoryPlane.GetDistanceToPoint(pointEnd)) <= projectileForgiveness)
                return true;
			
            return false;
        }

        private bool IsHitPointCloseToProjectileTrajectory(HitData hitData, float gravityModifier, float drag, float projectileForgiveness, out Vector3 lsVecStart, out Vector3 lsVecEnd, out float travelDistance, out List<TrajectorySegment> trajectorySegments, float physicsSteps)
        {
            Vector3 hitPositionWorld = hitData.hitPositionWorld;
            Vector3 position = hitData.startProjectilePosition;
			float fixedDeltaTime = 1f / physicsSteps;
			Vector3 vector1 = hitData.startProjectileVelocity / physicsSteps;
			Vector3 vector2 = ((Physics.gravity * gravityModifier) / physicsSteps) * fixedDeltaTime;
            float single1 = drag * fixedDeltaTime;
            float dist = 0.0f;
            int side;
			int segmentsCnt = (int)(physicsSteps * 8);
            Vector3 pointProjectedOnLine;
            float distanceFromHitPointToLastSegment;
			trajectorySegments = new List<TrajectorySegment>();
			
            if (hitData.delta != 1f)
            {
                float single4 = vector1.magnitude * hitData.delta;
                Vector3 vector3 = hitData.hitData.outVelocity.normalized * single4;
                position += vector3;

				pointProjectedOnLine = ProjectPointOnLine(position - vector3, vector3.normalized, hitPositionWorld);
				side = PointOnWhichSideOfLineSegment(position - vector3, position, pointProjectedOnLine);

                distanceFromHitPointToLastSegment = Vector3.Distance(hitPositionWorld, pointProjectedOnLine);
                dist += vector3.magnitude;

                if (side == 0 && distanceFromHitPointToLastSegment <= projectileForgiveness)
                {
                    lsVecStart = position - vector3;
                    lsVecEnd = position;
                    travelDistance = hitData.travelDistance + Vector3.Distance(hitData.startProjectilePosition, hitPositionWorld);

					trajectorySegments.Add(new TrajectorySegment());
					trajectorySegments[trajectorySegments.Count -1].pointStart = position - vector3;
					trajectorySegments[trajectorySegments.Count -1].pointEnd = position;

                    return true;
                }
            }

            for (int j = 0; j < segmentsCnt; j++)
            {
				pointProjectedOnLine = ProjectPointOnLine(position, vector1.normalized, hitPositionWorld);
				side = PointOnWhichSideOfLineSegment(position, position + vector1, pointProjectedOnLine);
				
                distanceFromHitPointToLastSegment = Vector3.Distance(hitPositionWorld, pointProjectedOnLine);

				trajectorySegments.Add(new TrajectorySegment());
				trajectorySegments[trajectorySegments.Count -1].pointStart = position;
				trajectorySegments[trajectorySegments.Count -1].pointEnd = position + vector1;
				
				if (side == 0 && distanceFromHitPointToLastSegment <= projectileForgiveness)
                {
					
                    lsVecStart = position;
                    lsVecEnd = position + vector1;
                    travelDistance = hitData.travelDistance + dist + Vector3.Distance(position, hitPositionWorld);
                    return true;
                }

                dist += vector1.magnitude;
                position += vector1;
                vector1 += vector2;
                vector1 -= (vector1 * single1);
            }

            lsVecStart = position;
            lsVecEnd = position + vector1;
            travelDistance = hitData.travelDistance + dist + Vector3.Distance((position), hitPositionWorld);
			
			trajectorySegments.Add(new TrajectorySegment());
			trajectorySegments[trajectorySegments.Count -1].pointStart = position;
			trajectorySegments[trajectorySegments.Count -1].pointEnd = position + vector1;
			
            return false;
        }

        private bool IsProjectileStartPointCloseToAtEndReverseProjectileTrajectory(HitData hitData, float gravityModifier, float drag, float projectileForgiveness, out Vector3 lsVecStart, out Vector3 lsVecEnd, out List<TrajectorySegment> trajectorySegmentsRev, float physicsSteps)
        {
            Vector3 projectileStartPoint = hitData.startProjectilePosition;
            Vector3 pointStart = hitData.hitPointStart;
            Vector3 pointEnd = hitData.hitPointEnd;
            Vector3 position = pointEnd;
            Vector3 vector1 = pointStart - pointEnd;
			float fixedDeltaTime = 1f / physicsSteps;
            Vector3 vector2 = ((-Physics.gravity * gravityModifier) / physicsSteps) * fixedDeltaTime;
            float single1 = 1f / (1f - (drag * fixedDeltaTime));
            int side;
			int segmentsCnt = (int)(physicsSteps * 8);
            Vector3 pointProjectedOnLine;
            float distanceFromHitPointToLastSegment;
			trajectorySegmentsRev = new List<TrajectorySegment>();
			
            for (int j = 0; j < segmentsCnt; j++)
            {
                pointProjectedOnLine = ProjectPointOnLineSegment(position, position + vector1, projectileStartPoint, out side);
                distanceFromHitPointToLastSegment = Vector3.Distance(projectileStartPoint, pointProjectedOnLine) + 0.05f;
				
				trajectorySegmentsRev.Add(new TrajectorySegment());
				trajectorySegmentsRev[trajectorySegmentsRev.Count -1].pointStart = position;
				trajectorySegmentsRev[trajectorySegmentsRev.Count -1].pointEnd = position + vector1;

                if ((side == 0 || Vector3.Distance(position + vector1, projectileStartPoint) < projectileForgiveness) && distanceFromHitPointToLastSegment <= projectileForgiveness)
                {
                    lsVecStart = position;
                    lsVecEnd = position + vector1;
                    return true;
                }

                position += vector1;
                vector1 *= single1;
                vector1 -= vector2;
            }

            lsVecStart = position;
            lsVecEnd = position + vector1;
			
			trajectorySegmentsRev.Add(new TrajectorySegment());
			trajectorySegmentsRev[trajectorySegmentsRev.Count -1].pointStart = position;
			trajectorySegmentsRev[trajectorySegmentsRev.Count -1].pointEnd = position + vector1;
			
            return false;
        }

        private void DrawProjectileTrajectory(BasePlayer player, float _drawTime, AIMViolationData aimvd, Color lineColor)
        {
            if (player != null)
            {
				float physicsSteps = aimvd.physicsSteps;
				float fixedDeltaTime = 1f / physicsSteps;
                Vector3 position = aimvd.startProjectilePosition;
                Vector3 vector1 = aimvd.startProjectileVelocity / physicsSteps;
                float distance = aimvd.calculatedTravelDistance;
				float hitInfoDistance = aimvd.hitInfoProjectileDistance;
                float gravityModifier = aimvd.gravityModifier;
                float drag = aimvd.drag;
                Vector3 hitPoint = aimvd.hitInfoHitPositionWorld;
                Vector3 vector2 = ((Physics.gravity * gravityModifier) / physicsSteps) * fixedDeltaTime;
                bool isRicochet = false;
                int ricochetCnt = 0;
                float single1 = drag * fixedDeltaTime;
                float dist = 0.0f;
				int segmentsCnt = (int)(physicsSteps * 8);

                DDrawSphereToAdmin(player, _drawTime, Color.red, hitPoint, 0.05f);

                if (aimvd.hitsData.Count > 1)
                {
                    isRicochet = true;
                    ricochetCnt = 1;
                }

                for (int j = 0; j < segmentsCnt; j++)
                {
                    if (isRicochet)
                        if (Vector3.Distance(position, aimvd.hitsData[ricochetCnt].hitData.hitPosition) <= (vector1.magnitude))
                        {
                            DDrawArrowToAdmin(player, _drawTime, lineColor, position, aimvd.hitsData[ricochetCnt].hitData.hitPosition, 0.1f);

                            float single3 = Vector3.Distance(position, aimvd.hitsData[ricochetCnt].hitData.hitPosition);
                            float single2 = vector1.magnitude;
                            float single4 = 1f - single3 * (1f / single2);
                            position = aimvd.hitsData[ricochetCnt].hitData.hitPosition;
                            vector1 = aimvd.hitsData[ricochetCnt].hitData.outVelocity * fixedDeltaTime;

                            float single5 = vector1.magnitude * single4;
                            Vector3 vector3 = aimvd.hitsData[ricochetCnt].hitData.outVelocity.normalized * single5;

                            vector1 += vector2;
                            vector1 -= (vector1 * single1);
                            DDrawArrowToAdmin(player, _drawTime, Color.green, position, position + vector3, 0.1f);
                            position += vector3;
                            DDrawArrowToAdmin(player, _drawTime, lineColor, position, position + vector1, 0.1f);
                            dist += single3 + single5;

                            if ((aimvd.hitsData.Count - 1) > ricochetCnt)
                                ricochetCnt++;
                            else
                                isRicochet = false;
                        }
                        else
                            DDrawArrowToAdmin(player, _drawTime, lineColor, position, position + vector1, 0.1f);
                    else
                        DDrawArrowToAdmin(player, _drawTime, lineColor, position, position + vector1, 0.1f);

                    if ((distance - dist) <= (vector1.magnitude))
                        break;
					
					if ((hitInfoDistance - dist) <= (vector1.magnitude))
						lineColor = Color.red;

                    dist += vector1.magnitude;
                    position += vector1;

                    vector1 += vector2;
                    vector1 -= (vector1 * single1);
                }
            }
        }

        private void DrawReverseProjectileTrajectory(BasePlayer player, float _drawTime, AIMViolationData aimvd, Color lineColor)
        {
            if (player != null)
            {
                Vector3 pointStart = aimvd.hitInfoPointStart;
                Vector3 pointEnd = aimvd.hitInfoPointEnd;
                Vector3 hitPoint = aimvd.hitInfoHitPositionWorld;
                float distance = aimvd.hitInfoProjectileDistance;
                float gravityModifier = aimvd.gravityModifier;
                float drag = aimvd.drag;
				float physicsSteps = aimvd.physicsSteps;
				float fixedDeltaTime = 1f / physicsSteps;
                Vector3 position = pointEnd;
                Vector3 vector1 = pointStart - pointEnd;
                Vector3 vector2 = ((-Physics.gravity * gravityModifier) / physicsSteps) * fixedDeltaTime;
                bool isRicochet = false;
                int ricochetCnt = 0;
                float single1 = 1f / (1f - (drag * fixedDeltaTime));
                float dist = 0.0f;
				int segmentsCnt = (int)(physicsSteps * 8);

                if (aimvd.hitsData.Count > 0)
                {
                    isRicochet = true;
                    ricochetCnt = aimvd.hitsData.Count - 1;
                }

                for (int j = 0; j < segmentsCnt; j++)
                {
                    if (isRicochet)
                        if (Vector3.Distance(position, aimvd.hitsData[ricochetCnt].hitData.hitPosition) <= (vector1.magnitude + _config.projectileTrajectoryForgiveness))
                        {
                            DDrawArrowToAdmin(player, _drawTime, Color.yellow, position, aimvd.hitsData[ricochetCnt].hitData.hitPosition, 0.1f);
                            position = aimvd.hitsData[ricochetCnt - 1].lastSegmentPointEnd;
                            vector1 = (aimvd.hitsData[ricochetCnt - 1].lastSegmentPointStart - aimvd.hitsData[ricochetCnt - 1].lastSegmentPointEnd);
                            DDrawArrowToAdmin(player, _drawTime, lineColor, aimvd.hitsData[ricochetCnt].hitData.hitPosition, aimvd.hitsData[ricochetCnt - 1].lastSegmentPointStart, 0.1f);

                            if (ricochetCnt > 0)
                                ricochetCnt--;
                            else
                                isRicochet = false;
                        }
                        else
                            DDrawArrowToAdmin(player, _drawTime, lineColor, position, position + vector1, 0.1f);
                    else
                        DDrawArrowToAdmin(player, _drawTime, lineColor, position, position + vector1, 0.1f);

                    if ((distance - dist) <= (vector1.magnitude + _config.projectileTrajectoryForgiveness))
                        break;

                    if (j == 0)
                        dist = Vector3.Distance(pointStart, hitPoint);
                    else
                        dist += vector1.magnitude;

                    position += vector1;
                    vector1 = (vector1 * single1) - vector2;
                }
            }
        }

        private void DrawProjectileTrajectory2(BasePlayer player, float _drawTime, FiredProjectile fp, HitInfo info, Color lineColor, float physicsSteps)
        {
            Vector3 position = fp.projectilePosition;
            Vector3 projectileVelocity = fp.projectileVelocity;
            float distance = info.ProjectileDistance;
            float gravityModifier = info.ProjectilePrefab.gravityModifier;
            float drag = info.ProjectilePrefab.drag;
            Vector3 hitPoint = info.HitPositionWorld;
			float fixedDeltaTime = 1f / physicsSteps;

            Vector3 vector1 = projectileVelocity / physicsSteps;
            Vector3 vector2 = ((Physics.gravity * gravityModifier) / physicsSteps) * fixedDeltaTime;
            bool isRicochet = false;
            int ricochetCnt = 0;
            float single1 = drag * fixedDeltaTime;
            float dist = 0.0f;
			int segmentsCnt = (int)(physicsSteps * 8);

            if (fp.hitsData.Count > 0)
            {
                isRicochet = true;
                ricochetCnt = 1;
            }

            for (int j = 0; j < segmentsCnt; j++)
            {
                if (isRicochet)
                    if (Vector3.Distance(position, fp.hitsData[ricochetCnt - 1].hitPosition) <= (vector1.magnitude + _config.projectileTrajectoryForgiveness))
                    {
                        DDrawArrowToAdmin(player, _drawTime, lineColor, position, fp.hitsData[ricochetCnt - 1].hitPosition, 0.1f);

                        float single3 = Vector3.Distance(position, fp.hitsData[ricochetCnt - 1].hitPosition);
                        float single2 = vector1.magnitude;
                        float single4 = 1f - single3 * (1f / single2);
                        position = fp.hitsData[ricochetCnt - 1].hitPosition;
                        vector1 = fp.hitsData[ricochetCnt - 1].outVelocity * fixedDeltaTime;

                        DDrawSphereToAdmin(player, _drawTime, Color.red, hitPoint, 0.2f);

                        float single5 = vector1.magnitude * single4;
                        Vector3 vector3 = fp.hitsData[ricochetCnt - 1].outVelocity.normalized * single5;

                        vector1 += vector2;
                        vector1 -= (vector1 * single1);
                        DDrawArrowToAdmin(player, _drawTime, Color.green, position, position + vector3, 0.1f);
                        position += vector3;
                        DDrawArrowToAdmin(player, _drawTime, lineColor, position, position + vector1, 0.1f);
                        dist += single3 + single5;

                        if (fp.hitsData.Count > ricochetCnt)
                            ricochetCnt++;
                        else
                            isRicochet = false;
                    }
                    else
                        DDrawArrowToAdmin(player, _drawTime, lineColor, position, position + vector1, 0.1f);
                else
                    DDrawArrowToAdmin(player, _drawTime, lineColor, position, position + vector1, 0.1f);

                if ((distance - dist) <= (vector1.magnitude))
                    break;

                dist += vector1.magnitude;
                position += vector1;

                vector1 += vector2;
                vector1 -= (vector1 * single1);
            }
        }

		private bool IsRicochet(List<TrajectorySegment> trajectorySegments, List<TrajectorySegment> trajectorySegmentsRev, out HitData hitData1, out HitData hitData2, float physicsSteps)
        {
			Vector3 intersectPoint1 = new Vector3();
			Vector3 intersectPoint2 = new Vector3();
			
			hitData1 = new HitData();
			hitData2 = new HitData();
			
			for (int j = 0; j < trajectorySegments.Count; j++)
			{
				for (int i = 0; i < trajectorySegmentsRev.Count; i++)
				{
					if (ClosestPointsOnTwoLines(out intersectPoint1, out intersectPoint2, trajectorySegments[j].pointStart, trajectorySegments[j].pointEnd - trajectorySegments[j].pointStart, trajectorySegmentsRev[i].pointStart, trajectorySegmentsRev[i].pointEnd - trajectorySegmentsRev[i].pointStart))
					{
						if (Vector3.Distance(trajectorySegments[j].pointStart, intersectPoint1) <= Vector3.Distance(trajectorySegments[j].pointStart, trajectorySegments[j].pointEnd) && Vector3.Distance(trajectorySegmentsRev[i].pointStart, intersectPoint2) <= Vector3.Distance(trajectorySegmentsRev[i].pointStart, trajectorySegmentsRev[i].pointEnd) && Vector3.Distance(intersectPoint1, intersectPoint2) <= _config.projectileTrajectoryForgiveness)
						{
							hitData1.hitPositionWorld = intersectPoint1;
							hitData1.hitPointStart = trajectorySegments[j].pointStart;		
							hitData1.hitPointEnd = trajectorySegments[j].pointEnd;
							hitData2.hitData.inVelocity = (intersectPoint1 - trajectorySegments[j].pointStart) * physicsSteps;
							hitData2.hitData.inVelocity = (trajectorySegments[j].pointEnd - trajectorySegments[j].pointStart) * physicsSteps;
							
							hitData2.startProjectilePosition = intersectPoint1;
							hitData2.startProjectileVelocity = (trajectorySegmentsRev[i].pointStart - trajectorySegmentsRev[i].pointEnd) * physicsSteps;	
							hitData2.hitData.outVelocity = (trajectorySegmentsRev[i].pointStart - trajectorySegmentsRev[i].pointEnd) * physicsSteps;	
							hitData2.hitData.hitPosition = intersectPoint1;	
							return true;							
						}
					}
				}
			}			
			return false;
		}
        #endregion Functions
    }
}