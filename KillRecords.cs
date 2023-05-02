using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Database;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Kill Records", "MACHIN3", "1.3.6")]
	[Description("Records number of kills by each player for selected entities")]
	public class KillRecords : RustPlugin
	{
		#region Update Log

		/*****************************************************
		【 𝓜𝓐𝓒𝓗𝓘𝓝𝓔 】
        Website: https://www.rustlevels.com/
        Discord: http://discord.rustlevels.com/
        *****************************************************/
		#region 1.3.5
		/*
		✯ update 1.3.5
		✯ Updated for OnCollectiblePickup hook changes
		*/
		#endregion
		#region 1.3.4
		/*
		✯ update 1.3.4
		✯ Fixed OnEntityDeath error from turrets
		*/
		#endregion
		#region 1.3.3
		/*
		✯ update 1.3.3
		✯ Added Scare Crow tracking
		✯ Added Turret Tracking
		✯ Code cleanup
		*/
		#endregion
		#region 1.3.2
		/*
		✯ update 1.3.2
		✯ Fixed non players being added to data records
		✯ Will automatically remove any non valid steam players from records
		*/
		#endregion
		#region 1.3.1
		/*
		✯ update 1.3.1
		✯ Fixed SQL issues with adding Polar Bear tracking
		✯ All chat commands are now configurable
		✯ Added harvest API for XPerience and XPerienceAddon
        ✯ New SQL update method for creating new columns in existing SQL database

		New Harvest Tracking:
		✯ Trees cut
		✯ Ore mined
		✯ Wood pickup
		✯ Ore pickup
		✯ Berries
		✯ Pumpkins
		✯ Potatos
		✯ Corn
		✯ Mushrrom
		✯ Hemp
		✯ Seeds

		Harvest tracking will only count the number of harvests NOT total resources gathered! XPerience will track total amount harvested.
		*/
		#endregion
		#region 1.3.0
		/*
		✯ update 1.3.0
		✯ Fixed horse kills not counting
		✯ Added PolarBear Tracking
		*/
		#endregion
		#region 1.2.9
		/*
		✯ update 1.2.9
		✯ Fixed scientists kills not counting
		*/
		#endregion
		#region 1.2.8
		/*
		✯ update 1.2.8
		✯ Cleaned up coding
		✯ Added API needed for XPerienceAddon
		*/
		#endregion
		#region 1.2.7
		/*
		✯ update 1.2.7
		✯ Fixed SQL update issues
		*/
		#endregion
		#region 1.2.6
		/*
		✯ update 1.2.6
		✯ Improved code performance
		*/
		#endregion
		#region 1.2.5
		/*
		✯ update 1.2.5
		✯ Added BradleyAPC and Patrol Helicopter tracking
		*/
		#endregion
		#region 1.2.4
		/*
		✯ update 1.2.4
		✯ Added UINotify Support
		✯ Complete rewrite of config file and settings
		✯ Changed OnFishCaught to OnFishCatch so fish tracking works again
		*/
		#endregion
		#region 1.2.3
		/*
		✯ update 1.2.3
		✯ Added Fish tracking 
		*/
		#endregion
		#region 1.2.2
		/*
		✯ update 1.2.2
		✯ fixed SQL errors with player displayname 
		✯ fixed players not being able to disable chat messages when given permission
		✯ fix for onplayerdeath error
		*/
		#endregion
		#region 1.2.1
		/*
		✯ this update 1.2.1
		✯ fixed possible player search issues when using SQL
		✯ fixed issues with SQL not tracking stats
		✯ added support for ImageLibrary for player avatars
		✯ added new leaderboards UI
		✯ linked all UIs with buttons
		✯ names in all UIs now clickable to player stats
		✯ added option to manually order each entity in UIs
		✯ added command to clear all player data
		*/
		#endregion
		#region 1.2.0
		/*
		✯ update 1.2.0
		✯ fixed top ten list showing players with 0
		✯ new global kill stats
		✯ option to disable UI
		✯ rewrote top list with selection menu
		✯ SQL support
		✯ New commands (datafile save, sql save, etc..)
		✯ Added horse kill tracking
		*/
		#endregion
		#region 1.1.9
		/*
		✯ update 1.1.9
		✯ cleaned coding
		✯ fixed killchat permission
		✯ More chat argument definitions
		✯ Added animal/corpse harvest to top lists
		*/
		#endregion
		#region 1.1.8
		/*
		✯ update 1.1.8
		✯ Changed UI locations
		✯ rewrote webrequest & labels
		✯ Changed star to arrow on top list
		✯ Rewrote language and chat messages
		✯ Full language support
		✯ Option to limit kill messages
		✯ Permissions to disable kill messages
		✯ Option for players to disable kill messages
		✯ More chat argument definitions
		*/
		#endregion
		#endregion

		#region References
		[PluginReference]
		private readonly Plugin ImageLibrary, UINotify;
		#endregion

		#region Fields
		public const string version = "1.3.4";
		private PlayData _playData;
		private TempData _tempData;
		private DynamicConfigFile _KillData;
		private DynamicConfigFile _EntityData;
		private Dictionary<string, Record> _recordCache;
		private Dictionary<ulong, Data> _dataCache;
		private Configuration config;
		private const string KRUIName = "KillRecordsUI";
		private const string KRUIMainName = "KillRecordsUIMain";
		private const string KRUISelection = "KillRecordsUISelection";
		private const string Admin = "killrecords.admin";
		private const string Killchat = "killrecords.killchat";
		#endregion

		#region Config
		private class Configuration : SerializableConfiguration
		{
			[JsonProperty("Tracking Options")]
			public TrackingOptions trackingoptions = new TrackingOptions();

			[JsonProperty("Player Chat Commands")]
			public PlayerChatCommands playerChatCommands = new PlayerChatCommands();

			[JsonProperty("Admin Chat Commands")]
			public AdminChatCommands adminChatCommands = new AdminChatCommands();

			[JsonProperty("Harvest Options")]
			public HarvestOptions harvestoptions = new HarvestOptions();

			[JsonProperty("Order Options")]
			public OrderOptions orderoptions = new OrderOptions();

			[JsonProperty("Chat & UI Options")]
			public ChatUI chatui = new ChatUI();

			[JsonProperty("Web Request")]
			public WebRequest webrequest = new WebRequest();

			[JsonProperty("SQL")]
			public KRSQL krsql = new KRSQL();
		}
		public class PlayerChatCommands
		{
			public string krhelp = "krhelp";
			public string pkills = "pkills";
			public string pkillschat = "pkillschat";
			public string topkills = "topkills";
			public string topkillschat = "topkillschat";
			public string totalkills = "totalkills";
			public string totalkillschat = "totalkillschat";
			public string leadkills = "leadkills";
			public string pstats = "pstats";
			public string pstatschat = "pstatschat";
			public string topstats = "topstats";
			public string totalstats = "totalstats";
			public string totalstatschat = "totalstatschat";
			public string killchat = "killchat";
		}
		public class AdminChatCommands
		{
			public string krhelpadmin = "krhelpadmin";
			public string krweb = "krweb";
			public string krsql = "krsql";
			public string krbackup = "krbackup";
			public string krreset = "krreset";
		}
		private class TrackingOptions
        {
			public bool Trackchicken = true;
			public bool Trackboar = true;
			public bool Trackstag = true;
			public bool Trackwolf = true;
			public bool Trackbear = true;
			public bool Trackpolarbear = true;
			public bool Trackshark = true;
			public bool Trackhorse = true;
			public bool Trackfish = true;
			public bool TrackPlayer = true;
			public bool Trackscientist = true;
			public bool Trackscarecrow = true;
			public bool Trackdweller = true;
			public bool Tracklootcontainer = true;
			public bool Trackunderwaterlootcontainer = true;
			public bool Trackbradhelicrate = true;
			public bool Trackhackablecrate = true;
			public bool Trackdeaths = true;
			public bool Tracksuicides = true;
			public bool TrackAnimalHarvest = true;
			public bool TrackCorpseHarvest = true;
			public bool TrackBradley = true;
			public bool TrackHeli = true;
			public bool Trackturret = true;
		}
		private class HarvestOptions
		{
			public bool treescut = true;
			public bool oremined= true;
			public bool cactuscut= false;
			public bool woodpickup= true;
			public bool orepickup= true;
			public bool berriespickup= true;
			public bool pumpkinpickup= true;
			public bool potatopickup= true;
			public bool cornpickup= true;
			public bool mushroompickup= true;
			public bool hemppickup= true;
			public bool seedpickup= true;
        }
		private class OrderOptions
        {
			public int chickenpos = 1;
			public int boarpos = 2;
			public int stagpos = 3;
			public int wolfpos = 4;
			public int bearpos = 5;
			public int polarbearpos = 22;
			public int sharkpos = 6;
			public int horsepos = 7;
			public int fishpos = 19;
			public int playerpos = 8;
			public int scientistpos = 9;
			public int dwellerpos = 10;
			public int lootpos = 11;
			public int unlootpos = 12;
			public int bradhelicratepos = 13;
			public int hackablecratepos = 14;
			public int deathpos = 15;
			public int suicidepos = 16;
			public int corpsepos = 17;
			public int pcorpsepos = 18;
			public int bradleypos = 20;
			public int helipos = 21;
			public int scarecrowpos = 23;
			public int turretpos = 24;
		}
		private class ChatUI
        {
			public bool enableui = true;
			public bool UseImageLibrary = false;
			public bool ShowKillMessages = true;
			public int KillMessageInterval = 1;
			public int KillMessageLimit = 5000;
			public bool enableuinotify = false;
			public bool disablechats = false;
			public int uinotifytype = 0;
		}
		private class WebRequest
        {
			public bool UseWebrequests = false;
			public string DataURL = "URL";
			public string SecretKey = "SecretKey";
		}
		private class KRSQL
        {
			public bool UseSQL = false;
			public int FileType = 0;
			public string SQLhost = "localhost";
			public int SQLport = 3306;
			public string SQLdatabase = "databasename";
			public string SQLusername = "username";
			public string SQLpassword = "password";
		}
		protected override void LoadDefaultConfig() => config = new Configuration();
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null)
				{
					throw new JsonException();
				}
				if (MaybeUpdateConfig(config))
				{
					PrintWarning("Configuration appears to be outdated; updating and saving");
					SaveConfig();
				}
			}
			catch
			{
				PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
				LoadDefaultConfig();
			}
		}
		protected override void SaveConfig()
		{
			PrintWarning($"Configuration changes saved to {Name}.json");
			Config.WriteObject(config, true);
		}
        #endregion

        #region UpdateChecker
        internal class SerializableConfiguration
		{
			public string ToJson() => JsonConvert.SerializeObject(this);

			public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
		}
		private static class JsonHelper
		{
			public static object Deserialize(string json) => ToObject(JToken.Parse(json));

			private static object ToObject(JToken token)
			{
				switch (token.Type)
				{
					case JTokenType.Object:
						return token.Children<JProperty>().ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
					case JTokenType.Array:
						return token.Select(ToObject).ToList();

					default:
						return ((JValue)token).Value;
				}
			}
		}
		private bool MaybeUpdateConfig(SerializableConfiguration config)
		{
			var currentWithDefaults = config.ToDictionary();
			var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
			return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
		}
		private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
		{
			bool changed = false;

			foreach (var key in currentWithDefaults.Keys)
			{
				object currentRawValue;
				if (currentRaw.TryGetValue(key, out currentRawValue))
				{
					var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
					var currentDictValue = currentRawValue as Dictionary<string, object>;

					if (defaultDictValue != null)
					{
						if (currentDictValue == null)
						{
							currentRaw[key] = currentWithDefaults[key];
							changed = true;
						}
						else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
							changed = true;
					}
				}
				else
				{
					currentRaw[key] = currentWithDefaults[key];
					changed = true;
				}
			}

			return changed;
		}
		#endregion

		#region Storage
		private void SaveData()
		{
			if (_playData != null)
			{
				_playData.KillRecords = _recordCache;
				_KillData.WriteObject(_playData);
			}
		}
		private void SaveTemp()
		{
			_EntityData = Interface.Oxide.DataFileSystem.GetFile(nameof(KillRecords) + "/KillRecordsEntityData");
			_dataCache.Clear();
			if (_tempData != null)
			{
				_tempData.EntityRecords = _dataCache;
				_EntityData.WriteObject(_tempData);
			}
		}
		private void LoadData()
		{
			try
			{
				_playData = _KillData.ReadObject<PlayData>();
				_recordCache = _playData.KillRecords;
			}
			catch
			{
				_playData = new PlayData();
			}

			try
			{
				_tempData = _EntityData.ReadObject<TempData>();
				_dataCache = _tempData.EntityRecords;
			}
			catch
			{
				_tempData = new TempData();
			}
		}
		private class TempData
		{
			public Dictionary<ulong, Data> EntityRecords = new Dictionary<ulong, Data>();
		}
		private class Data
		{
			public uint entity;
			public List<string> id;
		}
		private class PlayData
		{
			public Dictionary<string, Record> KillRecords = new Dictionary<string, Record>();
		}
		private class Record
		{
			public int chicken;
			public int boar;
			public int stag;
			public int wolf;
			public int bear;
			public int polarbear;
			public int shark;
			public int horse;
			public int fish;
			public int scientistnpcnew;
			public int scarecrow;
			public int dweller;
			public int baseplayer;
			public int basecorpse;
			public int npcplayercorpse;
			public int deaths;
			public int suicides;
			public int lootcontainer;
			public int underwaterlootcontainer;
			public int bradhelicrate;
			public int hackablecrate;
			public int bradley;
			public int heli;
			public int turret;
			public string displayname;
			public string id;
			public bool killchat;
			public int treescut;
			public int oremined;
			public int cactuscut;
			public int woodpickup;
			public int orepickup;
			public int berriespickup;
			public int pumpkinpickup;
			public int potatopickup;
			public int cornpickup;
			public int mushroompickup;
			public int hemppickup;
			public int seedpickup;
		}
		#endregion

		#region SQL
		private readonly Core.MySql.Libraries.MySql sqlLibrary = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
		Connection sqlConnection;
		//SQL Table
		private void CreatSQLTable()
		{
			sqlLibrary.Insert(Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS KillRecords (" +
				$" `id` BIGINT(255) NOT NULL AUTO_INCREMENT," +
				$" `steamid` BIGINT(255) NOT NULL," +
				$" `displayname` VARCHAR(255) NOT NULL," +
				$" `chicken` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `boar` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `stag` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `wolf` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `bear` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `polarbear` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `shark` BIGINT(255) NOT NULL DEFAULT '0'," +
                $" `horse` BIGINT(255) NOT NULL DEFAULT '0'," +
                $" `fish` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `scientistnpcnew` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `scarecrow` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `dweller` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `baseplayer` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `basecorpse` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `npcplayercorpse` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `deaths` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `suicides` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `lootcontainer` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `bradhelicrate` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `hackablecrate` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `bradley` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `heli` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `turret` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `underwaterlootcontainer` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `killchat` VARCHAR(255) NOT NULL DEFAULT 'True'," +
				$" `treescut` BIGINT(255) NOT NULL DEFAULT '0'," +
				$" `oremined` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `cactuscut` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `woodpickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `orepickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `berriespickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `pumpkinpickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `potatopickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `cornpickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `mushroompickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `hemppickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$" `seedpickup` VARCHAR(255) NOT NULL DEFAULT '0'," +
				$"PRIMARY KEY (id)" +
				$" );"), sqlConnection);
		}
		private void UpdateSQLTable()
		{
			try
			{
				bool treescut = false;
				bool oremined = false;
				bool cactuscut = false;
				bool woodpickup = false;
				bool orepickup = false;
				bool berriespickup = false;
				bool pumpkinpickup = false;
				bool potatopickup = false;
				bool cornpickup = false;
				bool mushroompickup = false;
				bool hemppickup = false;
				bool seedpickup = false;
				bool polarbear = false;
				bool bradley = false;
				bool heli = false;
				bool scarecrow = false;
				bool turret = false;
				sqlLibrary.Query(Sql.Builder.Append($"SELECT * FROM KillRecords"), sqlConnection, list =>
				{
					foreach (var entry in list)
					{
						if (!entry.ContainsKey("treescut"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `treescut` BIGINT(255) NOT NULL DEFAULT '0' AFTER killchat"), sqlConnection); 
							treescut = true;
						}					
						if (!entry.ContainsKey("oremined"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `oremined` BIGINT(255) NOT NULL DEFAULT '0' AFTER treescut"), sqlConnection);
							oremined = true;
						}
						if (!entry.ContainsKey("cactuscut"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `cactuscut` BIGINT(255) NOT NULL DEFAULT '0' AFTER oremined"), sqlConnection);
							cactuscut = true;
						}
						if (!entry.ContainsKey("woodpickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `woodpickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER cactuscut"), sqlConnection);
							woodpickup = true;
						}
						if (!entry.ContainsKey("orepickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `orepickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER woodpickup"), sqlConnection);
							orepickup = true;
						}
						if (!entry.ContainsKey("berriespickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `berriespickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER orepickup"), sqlConnection);
							berriespickup = true;
						}
						if (!entry.ContainsKey("pumpkinpickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `pumpkinpickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER berriespickup"), sqlConnection);
							pumpkinpickup = true;
						}
						if (!entry.ContainsKey("potatopickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `potatopickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER pumpkinpickup"), sqlConnection);
							potatopickup = true;
						}
						if (!entry.ContainsKey("cornpickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `cornpickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER potatopickup"), sqlConnection);
							cornpickup = true;
						}
						if (!entry.ContainsKey("mushroompickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `mushroompickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER cornpickup"), sqlConnection);
							mushroompickup = true;
						}
						if (!entry.ContainsKey("hemppickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `hemppickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER mushroompickup"), sqlConnection);
							hemppickup = true;
						}
						if (!entry.ContainsKey("seedpickup"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `seedpickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER hemppickup"), sqlConnection);
							seedpickup = true;
						}
						if (!entry.ContainsKey("polarbear"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `polarbear` BIGINT(255) NOT NULL DEFAULT '0' AFTER bear"), sqlConnection);
							polarbear = true;
						}
						if (!entry.ContainsKey("bradley"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `bradley` BIGINT(255) NOT NULL DEFAULT '0' AFTER hackablecrate"), sqlConnection);
							bradley = true;
						}
						if (!entry.ContainsKey("heli"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `heli` BIGINT(255) NOT NULL DEFAULT '0' AFTER bradley"), sqlConnection);
							heli = true;
						}
						if (!entry.ContainsKey("scarecrow"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `scarecrow` BIGINT(255) NOT NULL DEFAULT '0' AFTER scientistnpcnew"), sqlConnection);
							scarecrow = true;
						}
						if (!entry.ContainsKey("turret"))
						{
							//sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `turret` BIGINT(255) NOT NULL DEFAULT '0' AFTER heli"), sqlConnection);
							turret = true;
						}
					}
					if (treescut)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `treescut` BIGINT(255) NOT NULL DEFAULT '0' AFTER killchat"), sqlConnection);
					}
					if (oremined)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `oremined` BIGINT(255) NOT NULL DEFAULT '0' AFTER treescut"), sqlConnection);
					}
					if (cactuscut)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `cactuscut` BIGINT(255) NOT NULL DEFAULT '0' AFTER oremined"), sqlConnection);
					}
					if (woodpickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `woodpickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER cactuscut"), sqlConnection);
					}
					if (orepickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `orepickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER woodpickup"), sqlConnection);
					}
					if (berriespickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `berriespickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER orepickup"), sqlConnection);
					}
					if (pumpkinpickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `pumpkinpickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER berriespickup"), sqlConnection);
					}
					if (potatopickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `potatopickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER pumpkinpickup"), sqlConnection);
					}
					if (cornpickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `cornpickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER potatopickup"), sqlConnection);
					}
					if (mushroompickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `mushroompickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER cornpickup"), sqlConnection);
					}
					if (hemppickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `hemppickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER mushroompickup"), sqlConnection);
					}
					if (seedpickup)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `seedpickup` BIGINT(255) NOT NULL DEFAULT '0' AFTER hemppickup"), sqlConnection);
					}
					if (polarbear)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `polarbear` BIGINT(255) NOT NULL DEFAULT '0' AFTER bear"), sqlConnection);
					}
					if (bradley)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `bradley` BIGINT(255) NOT NULL DEFAULT '0' AFTER hackablecrate"), sqlConnection);
					}
					if (heli)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `heli` BIGINT(255) NOT NULL DEFAULT '0' AFTER bradley"), sqlConnection);
					}
					if (scarecrow)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `scarecrow` BIGINT(255) NOT NULL DEFAULT '0' AFTER scientistnpcnew"), sqlConnection);
					}
					if (turret)
                    {
						sqlLibrary.Insert(Sql.Builder.Append($"ALTER TABLE KillRecords ADD COLUMN `turret` BIGINT(255) NOT NULL DEFAULT '0' AFTER heli"), sqlConnection);
					}
				});
			}
			catch (MySqlException e)
			{
				PrintError("Failed to Update Table (" + e.Message + ")");
			}
		}
		//SQL Player Create/Update
		private void CreatePlayerDataSQL(BasePlayer player)
		{
			Record record = GetRecord(player);
			// Remove special characters in names to prevent injection
			string removespecials = "(['^$.|?*+()&{}\\\\])";
			string replacename = "\\$1";
			Regex rgx = new Regex(removespecials);
			var playername = rgx.Replace(record.displayname, replacename);
			sqlLibrary.Insert(Sql.Builder.Append($"INSERT KillRecords (steamid, displayname, chicken, boar, stag, wolf, bear, polarbear, shark, horse, fish, scientistnpcnew, scarecrow, dweller, baseplayer, basecorpse, npcplayercorpse, deaths, suicides, lootcontainer, bradhelicrate, hackablecrate, bradley, heli, turret, underwaterlootcontainer, killchat, treescut, oremined, cactuscut, woodpickup, orepickup, berriespickup, pumpkinpickup, potatopickup, cornpickup, mushroompickup, hemppickup, seedpickup) " +
			$"VALUES ('" +
			$"{record.id}', " +
			$"'{playername}', " +
			$"'{record.chicken}', " +
			$"'{record.boar}', " +
			$"'{record.stag}', " +
			$"'{record.wolf}', " +
			$"'{record.bear}', " +
			$"'{record.polarbear}', " +
			$"'{record.shark}', " +
            $"'{record.horse}', " +
            $"'{record.fish}', " +
			$"'{record.scientistnpcnew}', " +
			$"'{record.scarecrow}', " +
			$"'{record.dweller}', " +
			$"'{record.baseplayer}', " +
			$"'{record.basecorpse}', " +
			$"'{record.npcplayercorpse}', " +
			$"'{record.deaths}', " +
			$"'{record.suicides}', " +
			$"'{record.lootcontainer}', " +
			$"'{record.bradhelicrate}', " +
			$"'{record.hackablecrate}', " +
			$"'{record.bradley}', " +
			$"'{record.heli}', " +
			$"'{record.turret}', " +
			$"'{record.underwaterlootcontainer}', " +
			$"'{record.killchat}', " +
			$"'{record.treescut}', " +
			$"'{record.oremined}', " +
			$"'{record.cactuscut}', " +
			$"'{record.woodpickup}', " +
			$"'{record.orepickup}', " +
			$"'{record.berriespickup}', " +
			$"'{record.pumpkinpickup}', " +
			$"'{record.potatopickup}', " +
			$"'{record.cornpickup}', " +
			$"'{record.mushroompickup}', " +
			$"'{record.hemppickup}', " +
			$"'{record.seedpickup}'" +
			$");"), sqlConnection);
		}
		private void UpdatePlayersSQL()
		{
			bool newplayer = false;
			sqlLibrary.Query(Sql.Builder.Append($"SELECT steamid FROM KillRecords"), sqlConnection, list =>
			{
				if (list.IsEmpty())
				{
					newplayer = true;
				}
				if (newplayer)
				{
					foreach (var r in _recordCache)
					{
						// Remove special characters in names to prevent injection
						string removespecials = "(['^$.|?*+()&{}\\\\])";
						string replacename = "\\$1";
						Regex rgx = new Regex(removespecials);
						var playername = rgx.Replace(r.Value.displayname, replacename);
						sqlLibrary.Insert(Sql.Builder.Append($"INSERT KillRecords (steamid, displayname, chicken, boar, stag, wolf, bear, polarbear, shark, horse, fish, scientistnpcnew, scarecrow, dweller, baseplayer, basecorpse, npcplayercorpse, deaths, suicides, lootcontainer, bradhelicrate, hackablecrate, bradley, heli, turret, underwaterlootcontainer, killchat, treescut, oremined, cactuscut, woodpickup, orepickup, berriespickup, pumpkinpickup, potatopickup, cornpickup, mushroompickup, hemppickup, seedpickup) " +
						$"VALUES ('" +
						$"{r.Value.id}', " +
						$"'{playername}', " +
						$"'{r.Value.chicken}', " +
						$"'{r.Value.boar}', " +
						$"'{r.Value.stag}', " +
						$"'{r.Value.wolf}', " +
						$"'{r.Value.bear}', " +
						$"'{r.Value.polarbear}', " +
						$"'{r.Value.shark}', " +
                        $"'{r.Value.horse}', " +
                        $"'{r.Value.fish}', " +
						$"'{r.Value.scientistnpcnew}', " +
						$"'{r.Value.scarecrow}', " +
						$"'{r.Value.dweller}', " +
						$"'{r.Value.baseplayer}', " +
						$"'{r.Value.basecorpse}', " +
						$"'{r.Value.npcplayercorpse}', " +
						$"'{r.Value.deaths}', " +
						$"'{r.Value.suicides}', " +
						$"'{r.Value.lootcontainer}', " +
						$"'{r.Value.bradhelicrate}', " +
						$"'{r.Value.hackablecrate}', " +
						$"'{r.Value.bradley}', " +
						$"'{r.Value.heli}', " +
						$"'{r.Value.turret}', " +
						$"'{r.Value.underwaterlootcontainer}', " +
						$"'{r.Value.killchat}', " +
						$"'{r.Value.treescut}', " +
						$"'{r.Value.oremined}', " +
						$"'{r.Value.cactuscut}', " +
						$"'{r.Value.woodpickup}', " +
						$"'{r.Value.orepickup}', " +
						$"'{r.Value.berriespickup}', " +
						$"'{r.Value.pumpkinpickup}', " +
						$"'{r.Value.potatopickup}', " +
						$"'{r.Value.cornpickup}', " +
						$"'{r.Value.mushroompickup}', " +
						$"'{r.Value.hemppickup}', " +
						$"'{r.Value.seedpickup}' " +
						$");"), sqlConnection);
					}
				}
			});

			sqlLibrary.Query(Sql.Builder.Append($"SELECT steamid FROM KillRecords"), sqlConnection, list =>
			{
				foreach (var entry in list)
				{
					foreach (var r in _recordCache)
					{
						if (r.Key == entry["steamid"].ToString())
						{
							// Remove special characters in names to prevent injection
							string removespecials = "(['^$.|?*+()&{}\\\\])";
							string replacename = "\\$1";
							Regex rgx = new Regex(removespecials);
							var playername = rgx.Replace(r.Value.displayname, replacename);
							sqlLibrary.Update(Sql.Builder.Append($"UPDATE KillRecords SET " +
							$"steamid='{r.Value.id}', " +
							$"displayname='{playername}', " +
							$"chicken='{r.Value.chicken}', " +
							$"boar='{r.Value.boar}', " +
							$"stag='{r.Value.stag}', " +
							$"wolf='{r.Value.wolf}', " +
							$"bear='{r.Value.bear}', " +
							$"polarbear='{r.Value.polarbear}', " +
							$"shark='{r.Value.shark}', " +
							$"horse='{r.Value.horse}', " +
							$"fish='{r.Value.fish}', " +
							$"scientistnpcnew='{r.Value.scientistnpcnew}', " +
							$"scarecrow='{r.Value.scarecrow}', " +
							$"dweller='{r.Value.dweller}', " +
							$"baseplayer='{r.Value.baseplayer}', " +
							$"basecorpse='{r.Value.basecorpse}', " +
							$"npcplayercorpse='{r.Value.npcplayercorpse}', " +
							$"deaths='{r.Value.deaths}', " +
							$"suicides='{r.Value.suicides}', " +
							$"lootcontainer='{r.Value.lootcontainer}', " +
							$"bradhelicrate='{r.Value.bradhelicrate}', " +
							$"hackablecrate='{r.Value.hackablecrate}', " +
							$"bradley='{r.Value.bradley}', " +
							$"heli='{r.Value.heli}', " +
							$"turret='{r.Value.turret}', " +
							$"underwaterlootcontainer='{r.Value.underwaterlootcontainer}', " +
							$"killchat='{r.Value.killchat}', " +
							$"treescut='{r.Value.treescut}', " +
							$"oremined='{r.Value.oremined}', " +
							$"cactuscut='{r.Value.cactuscut}', " +
							$"woodpickup='{r.Value.woodpickup}', " +
							$"orepickup='{r.Value.orepickup}', " +
							$"berriespickup='{r.Value.berriespickup}', " +
							$"pumpkinpickup='{r.Value.pumpkinpickup}', " +
							$"potatopickup='{r.Value.potatopickup}', " +
							$"cornpickup='{r.Value.cornpickup}', " +
							$"mushroompickup='{r.Value.mushroompickup}', " +
							$"hemppickup='{r.Value.hemppickup}', " +
							$"seedpickup='{r.Value.seedpickup}' " +
							$"WHERE steamid = '{r.Key}';"), sqlConnection);
						}
					}
				}
			});
		}
		private void UpdatePlayersDataSQL()
		{
			foreach (var r in _recordCache)
			{
				// Remove special characters in names to prevent injection
				string removespecials = "(['^$.|?*+()&{}\\\\])";
				string replacename = "\\$1";
				Regex rgx = new Regex(removespecials);
				var playername = rgx.Replace(r.Value.displayname, replacename);
				sqlLibrary.Update(Sql.Builder.Append($"UPDATE KillRecords SET " +
					$"steamid='{r.Value.id}', " +
					$"displayname='{playername}', " +
					$"chicken='{r.Value.chicken}', " +
					$"boar='{r.Value.boar}', " +
					$"stag='{r.Value.stag}', " +
					$"wolf='{r.Value.wolf}', " +
					$"bear='{r.Value.bear}', " +
					$"polarbear='{r.Value.polarbear}', " +
					$"shark='{r.Value.shark}', " +
                    $"horse='{r.Value.horse}', " +
                    $"fish='{r.Value.fish}', " +
					$"scientistnpcnew='{r.Value.scientistnpcnew}', " +
					$"scarecrow='{r.Value.scarecrow}', " +
					$"dweller='{r.Value.dweller}', " +
					$"baseplayer='{r.Value.baseplayer}', " +
					$"basecorpse='{r.Value.basecorpse}', " +
					$"npcplayercorpse='{r.Value.npcplayercorpse}', " +
					$"deaths='{r.Value.deaths}', " +
					$"suicides='{r.Value.suicides}', " +
					$"lootcontainer='{r.Value.lootcontainer}', " +
					$"bradhelicrate='{r.Value.bradhelicrate}', " +
					$"hackablecrate='{r.Value.hackablecrate}', " +
					$"bradley='{r.Value.bradley}', " +
					$"heli='{r.Value.heli}', " +
					$"turret='{r.Value.turret}', " +
					$"underwaterlootcontainer='{r.Value.underwaterlootcontainer}', " +
					$"killchat='{r.Value.killchat}', " +
					$"treescut='{r.Value.treescut}', " +
					$"oremined='{r.Value.oremined}', " +
					$"cactuscut='{r.Value.cactuscut}', " +
					$"woodpickup='{r.Value.woodpickup}', " +
					$"orepickup='{r.Value.orepickup}', " +
					$"berriespickup='{r.Value.berriespickup}', " +
					$"pumpkinpickup='{r.Value.pumpkinpickup}', " +
					$"potatopickup='{r.Value.potatopickup}', " +
					$"cornpickup='{r.Value.cornpickup}', " +
					$"mushroompickup='{r.Value.mushroompickup}', " +
					$"hemppickup='{r.Value.hemppickup}', " +
					$"seedpickup='{r.Value.seedpickup}' " +
					$"WHERE steamid = '{r.Key}';"), sqlConnection);
			}
		}		
		private void UpdatePlayerDataSQL(BasePlayer player)
		{
			Record record = GetRecord(player);
			// Remove special characters in names to prevent injection
			string removespecials = "(['^$.|?*+()&{}\\\\])";
			string replacename = "\\$1";
			Regex rgx = new Regex(removespecials);
			var playername = rgx.Replace(record.displayname, replacename);
			sqlLibrary.Update(Sql.Builder.Append($"UPDATE KillRecords SET " +
			$"steamid='{record.id}', " +
			$"displayname='{playername}', " +
			$"chicken='{record.chicken}', " +
			$"boar='{record.boar}', " +
			$"stag='{record.stag}', " +
			$"wolf='{record.wolf}', " +
			$"bear='{record.bear}', " +
			$"polarbear='{record.polarbear}', " +
			$"shark='{record.shark}', " +
            $"horse='{record.horse}', " +
            $"fish='{record.fish}', " +
			$"scientistnpcnew='{record.scientistnpcnew}', " +
			$"scarecrow='{record.scarecrow}', " +
			$"dweller='{record.dweller}', " +
			$"baseplayer='{record.baseplayer}', " +
			$"basecorpse='{record.basecorpse}', " +
			$"npcplayercorpse='{record.npcplayercorpse}', " +
			$"deaths='{record.deaths}', " +
			$"suicides='{record.suicides}', " +
			$"lootcontainer='{record.lootcontainer}', " +
			$"bradhelicrate='{record.bradhelicrate}', " +
			$"hackablecrate='{record.hackablecrate}', " +
			$"bradley='{record.bradley}', " +
			$"heli='{record.heli}', " +
			$"turret='{record.turret}', " +
			$"underwaterlootcontainer='{record.underwaterlootcontainer}', " +
			$"killchat='{record.killchat}', " +
			$"treescut='{record.treescut}', " +
			$"oremined='{record.oremined}', " +
			$"cactuscut='{record.cactuscut}', " +
			$"woodpickup='{record.woodpickup}', " +
			$"orepickup='{record.orepickup}', " +
			$"berriespickup='{record.berriespickup}', " +
			$"pumpkinpickup='{record.pumpkinpickup}', " +
			$"potatopickup='{record.potatopickup}', " +
			$"cornpickup='{record.cornpickup}', " +
			$"mushroompickup='{record.mushroompickup}', " +
			$"hemppickup='{record.hemppickup}', " +
			$"seedpickup='{record.seedpickup}' " +
			$"WHERE steamid = '{player.UserIDString}';"), sqlConnection);
		}
		private void CheckPlayerDataSQL(BasePlayer player)
		{
			bool newplayer = true;
			sqlLibrary.Query(Sql.Builder.Append($"SELECT steamid FROM KillRecords"), sqlConnection, list =>
			{
				foreach (var entry in list)
				{
					if (entry["steamid"].ToString() == player.UserIDString)
					{
						UpdatePlayerDataSQL(player);
						newplayer = false;
					}
				}
				if (newplayer)
				{
					CreatePlayerDataSQL(player);
				}
			});

		}
		//SQL Load Data
		private void LoadSQL()
		{
			sqlLibrary.Query(Sql.Builder.Append($"SELECT * FROM KillRecords"), sqlConnection, list =>
			{
				foreach (var tsqlplayer in list)
				{
					Record record;
					if (!_recordCache.TryGetValue(tsqlplayer["steamid"].ToString(), out record))
					{
						_recordCache[tsqlplayer["steamid"].ToString()] = record = new Record
						{
                            chicken = Convert.ToInt32(tsqlplayer["chicken"]),
							boar = Convert.ToInt32(tsqlplayer["boar"]),
							stag = Convert.ToInt32(tsqlplayer["stag"]),
							wolf = Convert.ToInt32(tsqlplayer["wolf"]),
							bear = Convert.ToInt32(tsqlplayer["bear"]),
							shark = Convert.ToInt32(tsqlplayer["shark"]),
							horse = Convert.ToInt32(tsqlplayer["horse"]),
							fish = Convert.ToInt32(tsqlplayer["fish"]),
							scientistnpcnew = Convert.ToInt32(tsqlplayer["scientistnpcnew"]),
							dweller = Convert.ToInt32(tsqlplayer["dweller"]),
							baseplayer = Convert.ToInt32(tsqlplayer["baseplayer"]),
							basecorpse = Convert.ToInt32(tsqlplayer["basecorpse"]),
							npcplayercorpse = Convert.ToInt32(tsqlplayer["npcplayercorpse"]),
							deaths = Convert.ToInt32(tsqlplayer["deaths"]),
							suicides = Convert.ToInt32(tsqlplayer["suicides"]),
							lootcontainer = Convert.ToInt32(tsqlplayer["lootcontainer"]),
							bradhelicrate = Convert.ToInt32(tsqlplayer["bradhelicrate"]),
							hackablecrate = Convert.ToInt32(tsqlplayer["hackablecrate"]),
							bradley = Convert.ToInt32(tsqlplayer["bradley"]),
							heli = Convert.ToInt32(tsqlplayer["heli"]),
							underwaterlootcontainer = Convert.ToInt32(tsqlplayer["underwaterlootcontainer"]),
							killchat = Convert.ToBoolean(tsqlplayer["killchat"]),
							treescut = Convert.ToInt32(tsqlplayer["treescut"]),
							oremined = Convert.ToInt32(tsqlplayer["oremined"]),
							cactuscut = Convert.ToInt32(tsqlplayer["cactuscut"]),
							woodpickup = Convert.ToInt32(tsqlplayer["heliwoodpickup"]),
							orepickup = Convert.ToInt32(tsqlplayer["orepickup"]),
							berriespickup = Convert.ToInt32(tsqlplayer["berriespickup"]),
							pumpkinpickup = Convert.ToInt32(tsqlplayer["pumpkinpickup"]),
							potatopickup = Convert.ToInt32(tsqlplayer["potatopickup"]),
							cornpickup = Convert.ToInt32(tsqlplayer["cornpickup"]),
							mushroompickup = Convert.ToInt32(tsqlplayer["mushroompickup"]),
							hemppickup = Convert.ToInt32(tsqlplayer["hemppickup"]),
							seedpickup = Convert.ToInt32(tsqlplayer["seedpickup"])
						};
						record.id = tsqlplayer["steamid"].ToString();
						record.displayname = tsqlplayer["displayname"].ToString();
					}
				}
			});
		}
		//SQL Delete Data
		private void DeleteSQL()
		{
			sqlLibrary.Delete(Sql.Builder.Append($"DELETE FROM KillRecords;"), sqlConnection);
		}
		#endregion

		#region Load/Save
		private void Init()
		{
			_recordCache = new Dictionary<string, Record>();
			_dataCache = new Dictionary<ulong, Data>();
		}
		private void OnServerInitialized()
		{
			cmd.AddChatCommand(config.playerChatCommands.krhelp, this, KRHelp);
			cmd.AddChatCommand(config.playerChatCommands.pkills, this, PKills);
			cmd.AddChatCommand(config.playerChatCommands.pkillschat, this, PKillsChat);
			cmd.AddChatCommand(config.playerChatCommands.topkills, this, TopKills);
			cmd.AddChatCommand(config.playerChatCommands.topkillschat, this, TopKillsChat);
			cmd.AddChatCommand(config.playerChatCommands.totalkills, this, TotalKills);
			cmd.AddChatCommand(config.playerChatCommands.totalkillschat, this, TotalKillsChat);
			cmd.AddChatCommand(config.playerChatCommands.leadkills, this, LeadKills);
			cmd.AddChatCommand(config.playerChatCommands.pstats, this, PStats);
			cmd.AddChatCommand(config.playerChatCommands.pstatschat, this, PStatsChat);
			cmd.AddChatCommand(config.playerChatCommands.topstats, this, TopStats);
			cmd.AddChatCommand(config.playerChatCommands.totalstats, this, TotalStats);
			cmd.AddChatCommand(config.playerChatCommands.totalstatschat, this, TotalStatsChat);
			cmd.AddChatCommand(config.playerChatCommands.killchat, this, KillChat);
			cmd.AddChatCommand(config.adminChatCommands.krhelpadmin, this, AdminKRHelp);
			cmd.AddChatCommand(config.adminChatCommands.krweb, this, AdminKRWeb);
			cmd.AddChatCommand(config.adminChatCommands.krsql, this, AdminKRsql);
			cmd.AddChatCommand(config.adminChatCommands.krbackup, this, AdminKRBackup);
			cmd.AddChatCommand(config.adminChatCommands.krreset, this, AdminKRReset);

			if (config.krsql.FileType == 1 && config.krsql.UseSQL == false)
			{
				PrintError("Config Options Not Properly Set! Data Type cannot = 1 While Use SQL Database = false");
				Interface.Oxide.UnloadPlugin("KillRecords");
			}			
			if (!config.trackingoptions.Trackdeaths)
			{
				Unsubscribe(nameof(OnPlayerDeath));
			}
			permission.RegisterPermission(Admin, this);
			permission.RegisterPermission(Killchat, this);
			if (config.krsql.FileType == 0)
			{
				_KillData = Interface.Oxide.DataFileSystem.GetFile(nameof(KillRecords) + "/KillRecords");
				foreach (var krrecord in _KillData)
				{
					if (!krrecord.Key.IsSteamId())
					{
						_KillData.Remove(krrecord.Key);
					}
				}
				LoadData();
				SaveData();
			}
			if (config.krsql.FileType == 0 && config.krsql.UseSQL)
			{
				sqlConnection = sqlLibrary.OpenDb(config.krsql.SQLhost, config.krsql.SQLport, config.krsql.SQLdatabase, config.krsql.SQLusername, config.krsql.SQLpassword, this);
				CreatSQLTable();
				UpdateSQLTable();
			}
			if (config.krsql.FileType == 1)
			{
				sqlConnection = sqlLibrary.OpenDb(config.krsql.SQLhost, config.krsql.SQLport, config.krsql.SQLdatabase, config.krsql.SQLusername, config.krsql.SQLpassword, this);
				CreatSQLTable();
				UpdateSQLTable();
				LoadData();
				LoadSQL();
				if (_recordCache != null)
				{
					_playData.KillRecords = _recordCache;
					_recordCache = _playData.KillRecords;
				}
			}
			SaveTemp();
			if (config.krsql.FileType == 0 && config.krsql.UseSQL)
			{
				UpdatePlayersSQL();
			}
			foreach (var player in BasePlayer.activePlayerList)
			{
				GetRecord(player);
			}
		}
		private void Unload()
		{
			if (config.krsql.FileType == 0)
			{
				SaveData();
			}
			foreach (var player in BasePlayer.activePlayerList)
			{
				DestroyUi(player, KRUIName);
			}
			if (config.krsql.FileType == 0 && config.krsql.UseSQL)
			{
				UpdatePlayersDataSQL();
				sqlLibrary.CloseDb(sqlConnection);
			}
			if (config.krsql.FileType == 1)
			{
				UpdatePlayersDataSQL();
				sqlLibrary.CloseDb(sqlConnection);
				_KillData = Interface.Oxide.DataFileSystem.GetFile(nameof(KillRecords) + "/KillRecords");
				SaveData();
			}
		}
		private void OnServerShutDown()
		{
			if (config.krsql.FileType == 0)
			{
				SaveData();
			}
			if (config.krsql.FileType == 0 && config.krsql.UseSQL)
			{
				UpdatePlayersDataSQL();
				sqlLibrary.CloseDb(sqlConnection);
			}
			if (config.krsql.FileType == 1)
			{
				UpdatePlayersDataSQL();
				sqlLibrary.CloseDb(sqlConnection);
				_KillData = Interface.Oxide.DataFileSystem.GetFile(nameof(KillRecords) + "/KillRecords");
				SaveData();
			}
		}
		private void OnServerSave()
		{
			if (config.krsql.FileType == 0)
			{
				SaveData();
			}

			if (config.krsql.UseSQL)
			{
				foreach (var player in BasePlayer.activePlayerList)
				{
					CheckPlayerDataSQL(player);
				}
				UpdatePlayersDataSQL();
			}
		}
		private void OnPlayerConnected(BasePlayer player)
		{
			GetRecord(player);
			if (config.krsql.UseSQL || config.krsql.FileType == 1)
			{
				CheckPlayerDataSQL(player);
			}
		}
		private void OnPlayerDisconnected(BasePlayer player)
		{
			DestroyUi(player, KRUIName);
			if (config.krsql.UseSQL || config.krsql.FileType == 1) { CheckPlayerDataSQL(player); }
			if (config.webrequest.UseWebrequests) { SaveKillRecordWeb(player); }
		}
		#endregion

		#region PlayerData
		private void AddData(BasePlayer player, BaseEntity entity)
		{
			Data data;
			if (!_dataCache.TryGetValue(entity.net.ID.Value, out data))
			{
				_dataCache.Add(entity.net.ID.Value, data = new Data
				{
					id = new List<string>(),
				});
			}

			if (!data.id.Contains(player.UserIDString))
			{
				data.id.Add(player.UserIDString);
			}
		}
		private void UpdateData(BasePlayer player)
		{
			GetRecord(player);
			if (config.krsql.FileType == 0) { SaveData(); }
			if (config.krsql.FileType == 1) { UpdatePlayersDataSQL(); }
		}
		private Record GetRecord(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return null;
			Record record;
			if (_recordCache.TryGetValue(player.UserIDString, out record))
			{
				return record;
			}

			if (!_recordCache.TryGetValue(player.UserIDString, out record))
			{
				_recordCache[player.UserIDString] = record = new Record
				{
					chicken = 0,
					boar = 0,
					stag = 0,
					wolf = 0,
					bear = 0,
					polarbear = 0,
					shark = 0,
					horse = 0,
					fish = 0,
					scientistnpcnew = 0,
					scarecrow = 0,
					dweller = 0,
					baseplayer = 0,
					basecorpse = 0,
					npcplayercorpse = 0,
					deaths = 0,
					suicides = 0,
					lootcontainer = 0,
					bradhelicrate = 0,
					hackablecrate = 0,
					bradley = 0,
					heli = 0,
					turret = 0,
					underwaterlootcontainer = 0,
					killchat = true,
					treescut = 0,
					oremined = 0,
					cactuscut = 0,
					woodpickup = 0,
					orepickup = 0,
					berriespickup = 0,
					pumpkinpickup = 0,
					potatopickup = 0,
					cornpickup = 0,
					mushroompickup = 0,
					hemppickup = 0,
					seedpickup = 0
				};
				record.id = player.UserIDString;
				record.displayname = player.displayName;
			}

			if (config.krsql.FileType == 0)
            {
				return record;
			}
			
			if (config.krsql.FileType == 1)
			{
				sqlLibrary.Query(Sql.Builder.Append($"SELECT * FROM KillRecords"), sqlConnection, list =>
				{
					foreach (var sqlplayer in list)
					{
						if (player.UserIDString == sqlplayer["steamid"].ToString())
						{
							_recordCache[player.UserIDString] = record = new Record
							{
								chicken = Convert.ToInt32(sqlplayer["chicken"]),
								boar = Convert.ToInt32(sqlplayer["boar"]),
								stag = Convert.ToInt32(sqlplayer["stag"]),
								wolf = Convert.ToInt32(sqlplayer["wolf"]),
								bear = Convert.ToInt32(sqlplayer["bear"]),
								polarbear = Convert.ToInt32(sqlplayer["polarbear"]),
								shark = Convert.ToInt32(sqlplayer["shark"]),
								horse = Convert.ToInt32(sqlplayer["horse"]),
								fish = Convert.ToInt32(sqlplayer["fish"]),
								scientistnpcnew = Convert.ToInt32(sqlplayer["scientistnpcnew"]),
								scarecrow = Convert.ToInt32(sqlplayer["scarecrow"]),
								dweller = Convert.ToInt32(sqlplayer["dweller"]),
								baseplayer = Convert.ToInt32(sqlplayer["baseplayer"]),
								basecorpse = Convert.ToInt32(sqlplayer["basecorpse"]),
								npcplayercorpse = Convert.ToInt32(sqlplayer["npcplayercorpse"]),
								deaths = Convert.ToInt32(sqlplayer["deaths"]),
								suicides = Convert.ToInt32(sqlplayer["suicides"]),
								lootcontainer = Convert.ToInt32(sqlplayer["lootcontainer"]),
								bradhelicrate = Convert.ToInt32(sqlplayer["bradhelicrate"]),
								hackablecrate = Convert.ToInt32(sqlplayer["hackablecrate"]),
								bradley = Convert.ToInt32(sqlplayer["bradley"]),
								heli = Convert.ToInt32(sqlplayer["heli"]),
								turret = Convert.ToInt32(sqlplayer["turret"]),
								underwaterlootcontainer = Convert.ToInt32(sqlplayer["underwaterlootcontainer"]),
								killchat = Convert.ToBoolean(sqlplayer["killchat"]),
								treescut = Convert.ToInt32(sqlplayer["treescut"]),
								oremined = Convert.ToInt32(sqlplayer["oremined"]),
								cactuscut = Convert.ToInt32(sqlplayer["cactuscut"]),
								woodpickup = Convert.ToInt32(sqlplayer["heliwoodpickup"]),
								orepickup = Convert.ToInt32(sqlplayer["orepickup"]),
								berriespickup = Convert.ToInt32(sqlplayer["berriespickup"]),
								pumpkinpickup = Convert.ToInt32(sqlplayer["pumpkinpickup"]),
								potatopickup = Convert.ToInt32(sqlplayer["potatopickup"]),
								cornpickup = Convert.ToInt32(sqlplayer["cornpickup"]),
								mushroompickup = Convert.ToInt32(sqlplayer["mushroompickup"]),
								hemppickup = Convert.ToInt32(sqlplayer["hemppickup"]),
								seedpickup = Convert.ToInt32(sqlplayer["seedpickup"])
							};
							record.id = player.UserIDString;
							record.displayname = player.displayName;
						}
					}
				});
			}
			return record;
		}
		private static BasePlayer FindPlayer(string playerid)
		{
			foreach (var activePlayer in BasePlayer.activePlayerList)
			{
				if (activePlayer.UserIDString == playerid)
					return activePlayer;
			}
			foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
			{
				if (sleepingPlayer.UserIDString == playerid)
					return sleepingPlayer;
			}
			return null;
		}
		#endregion

		#region RecordKills & Loot
		private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
		{
			// Check for null or npcs
			if (entity == null) return;
			if (hitInfo == null) return;
			if (hitInfo.Initiator == null) return;
			// Turret Kills
			if (hitInfo.Initiator is AutoTurret && config.trackingoptions.Trackturret)
			{
				var turret = hitInfo.Initiator as AutoTurret;
				var turretowner = FindPlayer(turret.OwnerID.ToString());
				if (!turret.OwnerID.IsSteamId() || turretowner == null) return;
				Record records = GetRecord(turretowner);
				KRKillMessages(turretowner, "turret", ++records.turret, records.killchat);
				return;
			}
			// Count Player Suicide Separately If Enabled
			if (config.trackingoptions.Tracksuicides && entity == hitInfo.Initiator)
			{
				var suicider = entity as BasePlayer;
				if (suicider == null || suicider.IsNpc || !suicider.userID.IsSteamId()) return;
				var r = GetRecord(suicider);
				KRKillMessages(suicider, "suicide", ++r.suicides, r.killchat);
				return;
			}
			// Ignore Player Suicide If Disabled and Count as Death
			if (entity == hitInfo.Initiator) return;
			// Get Killer Info
			var attacker = hitInfo.Initiator as BasePlayer;
			if (attacker == null || attacker.IsNpc || !attacker.userID.IsSteamId()) return;
			var KillType = entity?.GetType().Name.ToLower();
			Record record = GetRecord(attacker);
			// Update DataCache On Kill
			switch (KillType)
			{
				case "chicken":
					if (config.trackingoptions.Trackchicken)
					KRKillMessages(attacker, KillType, ++record.chicken, record.killchat);					
					break;
				case "boar":
					if (config.trackingoptions.Trackboar)
					KRKillMessages(attacker, KillType, ++record.boar, record.killchat);
					break;
				case "stag":
					if (config.trackingoptions.Trackstag)
					KRKillMessages(attacker, KillType, ++record.stag, record.killchat);
					break;
				case "wolf":
					if (config.trackingoptions.Trackwolf)
					KRKillMessages(attacker, KillType, ++record.wolf, record.killchat);
					break;
				case "bear":
					if (config.trackingoptions.Trackbear)
					KRKillMessages(attacker, KillType, ++record.bear, record.killchat);
					break;
				case "polarbear":
					if (config.trackingoptions.Trackpolarbear)
					KRKillMessages(attacker, KillType, ++record.polarbear, record.killchat);
					break;
				case "simpleshark":
					if (config.trackingoptions.Trackshark)
					KRKillMessages(attacker, KillType, ++record.shark, record.killchat);
					break;
				case "horse":
				case "rideablehorse":
				case "ridablehorse":
					if (config.trackingoptions.Trackhorse)
					KRKillMessages(attacker, "horse", ++record.horse, record.killchat);
					break;
				case "scientist":
				case "scientistnpcnew":
				case "scientistnpc":
					if (config.trackingoptions.Trackscientist)
					KRKillMessages(attacker, "scientists", ++record.scientistnpcnew, record.killchat);
					break;
				case "tunneldweller":
				case "underwaterdweller":
					if (config.trackingoptions.Trackdweller)
					KRKillMessages(attacker, "dweller", ++record.dweller, record.killchat);
					break;
				case "baseplayer":
					if (config.trackingoptions.TrackPlayer)
					KRKillMessages(attacker, "players", ++record.baseplayer, record.killchat);
					break;
				case "lootcontainer":
					if (config.trackingoptions.Tracklootcontainer)
					KRKillMessages(attacker, "loot", ++record.lootcontainer, record.killchat);
					break;
				case "basecorpse":
					if (config.trackingoptions.TrackAnimalHarvest)
					KRKillMessages(attacker, "corpse", ++record.basecorpse, record.killchat);
					break;
				case "npcplayercorpse":
					if (config.trackingoptions.TrackCorpseHarvest)
					KRKillMessages(attacker, "pcorpse", ++record.npcplayercorpse, record.killchat);
					break;
				case "bradleyapc":
					if (config.trackingoptions.TrackBradley)
					KRKillMessages(attacker, KillType, ++record.bradley, record.killchat);
					break;
				case "patrolhelicopter":
					if (config.trackingoptions.TrackHeli)
					KRKillMessages(attacker, KillType, ++record.heli, record.killchat);
					break;
				case "scarecrownpc":
					if (config.trackingoptions.Trackscarecrow)
					KRKillMessages(attacker, "scarecrow", ++record.scarecrow, record.killchat);
					break;
			}
		}
		private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
		{
			if (config.trackingoptions.Trackdeaths)
			{
				// Check for null or NPC
				if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
				// If Suicide Tracking Enabled Ingnore Death 
				if (config.trackingoptions.Tracksuicides && player == hitInfo?.Initiator) return;
				// Check For Non Attack Death Types
				Record record = GetRecord(player);
				if (record == null) return;
				if (player.lastDamage == DamageType.Thirst || player.lastDamage == DamageType.Hunger || player.lastDamage == DamageType.Cold || player.lastDamage == DamageType.Heat || player.lastDamage == DamageType.Fall)
				{
					KRKillMessages(player, "deaths", ++record.deaths, record.killchat);
					return;
				}
				// If Attack Type Not Detected Remove Error
				if (hitInfo == null) return;
				// Update Player Data On Other deaths
				KRKillMessages(player, "deaths", ++record.deaths, record.killchat);
			}
		}
		private void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			if (player == null || !entity.IsValid()) return;
			var loot = entity.GetType().Name.ToLower();
			if (_dataCache.ContainsKey(entity.net.ID.Value) && _dataCache[entity.net.ID.Value].id.Contains(player.UserIDString))
			{
				return;
			}
			AddData(player, entity);
			Record record = GetRecord(player);
			switch (loot)
            {
				case "freeablelootcontainer":
					if (config.trackingoptions.Trackunderwaterlootcontainer)
					KRKillMessages(player, "unloot", ++record.underwaterlootcontainer, record.killchat);
					break;
				case "lootcontainer":
					if (config.trackingoptions.Tracklootcontainer)
					KRKillMessages(player, "loot", ++record.lootcontainer, record.killchat);
					break;
				case "lockedbyentcrate":
					if (config.trackingoptions.Trackbradhelicrate)
					KRKillMessages(player, "bradheliloot", ++record.bradhelicrate, record.killchat);
					break;
				case "hackablelockedcrate":
					if (config.trackingoptions.Trackhackablecrate)
					KRKillMessages(player, "hackloot", ++record.hackablecrate, record.killchat);
					break;
			}
		}
		private void OnFishCatch(Item fish, BasePlayer player)
		{
			if (player == null || fish == null) return;
			Record record = GetRecord(player);
			if (config.trackingoptions.Trackfish)
			{
				var fishname = fish.info.shortname;
				if (fishname.Contains("anchovy") || fishname.Contains("catfish") || fishname.Contains("herring") || fishname.Contains("minnow") || fishname.Contains("roughy") || fishname.Contains("salmon") || fishname.Contains("sardine") || fishname.Contains("shark") || fishname.Contains("trout") || fishname.Contains("Perch"))
				{
					KRKillMessages(player, "fish", ++record.fish, record.killchat);
				}
			}
		}
		private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
			if (player == null || !player.userID.IsSteamId() || dispenser == null || item == null) return;
			Record record = GetRecord(player);
			var gatherType = dispenser.gatherType;
			if (gatherType == ResourceDispenser.GatherType.Tree)
			{
				KRKillMessages(player, "treecut", ++record.treescut, record.killchat);
			}
			else if (gatherType == ResourceDispenser.GatherType.Ore)
			{
				KRKillMessages(player, "oremined", ++record.oremined, record.killchat);

			}
			else if (item.info.shortname == "cactusflesh")
			{
				KRKillMessages(player, "cactuscut", ++record.cactuscut, record.killchat);

			}
		}
		private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId() || collectible == null) return;
			Record record = GetRecord(player);
			foreach (var itemAmount in collectible.itemList)
			{
				var name = itemAmount.itemDef.shortname;
				if (name.Contains("wood"))
				{
					KRKillMessages(player, "woodpickup", ++record.woodpickup, record.killchat);
				}
				else if (name.Contains("ore") || name.Contains("stone"))
				{
					KRKillMessages(player, "orepickup", ++record.orepickup, record.killchat);
				}
				else if (name.Contains("berry") && !name.Contains("seed"))
				{
					KRKillMessages(player, "berries", ++record.berriespickup, record.killchat);
				}
				else if (name.Contains("seed"))
				{
					KRKillMessages(player, "seeds", ++record.seedpickup, record.killchat);
				}
				else if (name == "mushroom")
				{
					KRKillMessages(player, "mushroom", ++record.mushroompickup, record.killchat);
				}
				else if (name == "cloth")
				{
					KRKillMessages(player, "hemp", ++record.hemppickup, record.killchat);
				}
				else if (name == "pumpkin")
				{
					KRKillMessages(player, "pumpkin", ++record.pumpkinpickup, record.killchat);
				}
				else if (name == "corn")
				{
					KRKillMessages(player, "corn", ++record.cornpickup, record.killchat);
				}
				else if (name == "potato")
				{
					KRKillMessages(player, "potato", ++record.potatopickup, record.killchat);
				}
			}
		}
		private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId() || growable == null || item == null) return;
			Record record = GetRecord(player);
			var name = item.info.shortname;
			if (name.Contains("wood"))
			{
				KRKillMessages(player, "woodpickup", ++record.woodpickup, record.killchat);
			}
			else if (name.Contains("berry") && !name.Contains("seed"))
			{
				KRKillMessages(player, "berries", ++record.berriespickup, record.killchat);
			}
			else if (name.Contains("seed"))
			{
				KRKillMessages(player, "seeds", ++record.seedpickup, record.killchat);
			}
			else if (name == "mushroom")
			{
				KRKillMessages(player, "mushroom", ++record.mushroompickup, record.killchat);
			}
			else if (name == "cloth")
			{
				KRKillMessages(player, "hemp", ++record.hemppickup, record.killchat);
			}
			else if (name == "pumpkin")
			{
				KRKillMessages(player, "pumpkin", ++record.pumpkinpickup, record.killchat);
			}
			else if (name == "corn")
			{
				KRKillMessages(player, "corn", ++record.cornpickup, record.killchat);
			}
			else if (name == "potato")
			{
				KRKillMessages(player, "potato", ++record.potatopickup, record.killchat);
			}
		}
		#endregion

		#region Commands
		//Player Commands
		private void KRHelp(BasePlayer player, string command, string[] args)
		{
			player.ChatMessage(KRLang("KRHelp", player.UserIDString));
		}
		private void PKills(BasePlayer player, string command, string[] args)
		{
			string playerinfo;
			if (args.Length == 0)
			{
				playerinfo = player.UserIDString;
			}
			else
			{
				playerinfo = args[0].ToLower();
			}
			if (config.chatui.enableui)
			{
				KRUIplayers(player, playerinfo);
			}
			else
			{
				PlayerKillsChat(player, playerinfo);
			}
		}
		private void PKillsChat(BasePlayer player, string command, string[] args)
		{
			string playerinfo;
			if (args.Length == 0)
			{
				playerinfo = player.UserIDString;
			}
			else
			{
				playerinfo = args[0].ToLower();
			}
			PlayerKillsChat(player, playerinfo);
		}
		private void TopKills(BasePlayer player, string command, string[] args)
		{
			if (config.chatui.enableui)
			{
				if (args.Length == 0)
				{
					var KillType = KillTypesEnabled();
					KRUITop(player, KillType);
					return;
				}
				var cmdArg = args[0].ToLower();
				KRUITop(player, cmdArg);
			}
			else
			{
				if (args.Length == 0)
				{
					player.ChatMessage(KRLang("KRHelp", player.UserIDString));
					return;
				}
				var cmdArg = args[0].ToLower();
				TopKillsChat(player, cmdArg);
			}
		}
		private void TopKillsChat(BasePlayer player, string command, string[] args)
		{
			if (args.Length == 0)
			{
				player.ChatMessage(KRLang("KRHelp", player.UserIDString));
				return;
			}
			var cmdArg = args[0].ToLower();
			TopKillsChat(player, cmdArg);
		}
		private void TotalKills(BasePlayer player, string command, string[] args)
		{
			if (config.chatui.enableui)
			{
				KRUITotal(player);
			}
			else
			{
				TotalKillsChat(player);
			}
		}		
		private void TotalKillsChat(BasePlayer player, string command, string[] args)
		{
			TotalKillsChat(player);
		}
		private void LeadKills(BasePlayer player, string command, string[] args)
		{
			KRUITopAll(player);
		}
		private void PStats(BasePlayer player, string command, string[] args)
		{
			string playerinfo;
			if (args.Length == 0)
			{
				playerinfo = player.UserIDString;
			}
			else
			{
				playerinfo = args[0].ToLower();
			}
			if (config.chatui.enableui)
			{
				KRUIStatsplayers(player, playerinfo);
			}
			else
			{
				PlayerStatsChat(player, playerinfo);
			}
		}
		private void PStatsChat(BasePlayer player, string command, string[] args)
		{
			string playerinfo;
			if (args.Length == 0)
			{
				playerinfo = player.UserIDString;
			}
			else
			{
				playerinfo = args[0].ToLower();
			}
			PlayerStatsChat(player, playerinfo);
		}
		private void TopStats(BasePlayer player, string command, string[] args)
		{
			if (config.chatui.enableui)
			{
				if (args.Length == 0)
				{
					var KillType = KillTypesEnabled();
					KRUIStatsTop(player, KillType);
					return;
				}
				var cmdArg = args[0].ToLower();
				KRUIStatsTop(player, cmdArg);
			}
		}
		private void TotalStats(BasePlayer player, string command, string[] args)
		{
			if (config.chatui.enableui)
			{
				KRUIStatsTotal(player);
			}
			else
			{
				TotalStatsChat(player);
			}
		}	
		private void TotalStatsChat(BasePlayer player, string command, string[] args)
		{
			TotalStatsChat(player);
		}
		private void KillChat(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, Killchat)) return;
			if (args.Length == 0)
			{
				player.ChatMessage(KRLang("killchat", player.UserIDString, _recordCache[player.UserIDString].killchat));
				return;
			}

			var cmdArg = args[0].ToLower();
			if (cmdArg == "true")
			{
				_recordCache[player.UserIDString].killchat = true;
				player.ChatMessage(KRLang("killchat", player.UserIDString, cmdArg));
				return;
			}

			if (cmdArg == "false")
			{
				_recordCache[player.UserIDString].killchat = false;
				player.ChatMessage(KRLang("killchat", player.UserIDString, cmdArg));
			}
		}
		//Admin Commands
		private void AdminKRHelp(BasePlayer player, string command, string[] args)
		{
			if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
			player.ChatMessage(KRLang("KRHelpadmin", player.UserIDString));
		}
		private void AdminKRWeb(BasePlayer player, string command, string[] args)
		{
			if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
			if (config.webrequest.UseWebrequests)
			{
				SaveKillRecordWeb(player);
				player.ChatMessage(KRLang("webrequestgood", player.UserIDString));
			}
			else
			{
				player.ChatMessage(KRLang("webrequestdisabled", player.UserIDString));
			}
		}
		private void AdminKRsql(BasePlayer player, string command, string[] args)
		{
			if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
			if (!config.krsql.UseSQL) return;
			var cmdArg = args[0].ToLower();
			if (config.krsql.UseSQL && cmdArg == "update")
			{
				UpdatePlayerDataSQL(player);
				player.ChatMessage(KRLang("sqlupdate", player.UserIDString));
			}
			if (config.krsql.UseSQL && cmdArg == "check")
			{
				CheckPlayerDataSQL(player);
				player.ChatMessage(KRLang("sqlcheck", player.UserIDString));
			}
			if (config.krsql.UseSQL && cmdArg == "checkall")
			{
				UpdatePlayersSQL();
				UpdatePlayersDataSQL();
				player.ChatMessage(KRLang("sqlcheckall", player.UserIDString));
			}
		}
		private void AdminKRBackup(BasePlayer player, string command, string[] args)
		{
			if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
			_KillData = Interface.Oxide.DataFileSystem.GetFile(nameof(KillRecords) + "/KillRecords");
			SaveData();
			player.ChatMessage(KRLang("datafilebackup", player.UserIDString));
		}
		private void AdminKRReset(BasePlayer player, string command, string[] args)
		{
			if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Admin)) return;
			if (config.krsql.UseSQL)
			{
				DeleteSQL();
			}

			_recordCache.Clear();

			if (config.krsql.FileType == 0)
			{
				_KillData.Clear();
			}

			Interface.Oxide.ReloadPlugin("KillRecords");
			player.ChatMessage(KRLang("resetkills", player.UserIDString));
		}
		#endregion

		#region CommandHandlers & TopStats
		//UI Command Handler
		[ConsoleCommand("kr.topkills")]
		private void Cmdtopkills(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();			
			if (player == null) return;
			var killtype = arg.GetString(0);
			DestroyUi(player, KRUIName);
			KRUITop(player, killtype);
		}
		[ConsoleCommand("kr.topstats")]
		private void Cmdtopstats(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();			
			if (player == null) return;
			var killtype = arg.GetString(0);
			DestroyUi(player, KRUIName);
			KRUIStatsTop(player, killtype);
		}
		[ConsoleCommand("kr.topplayers")]
		private void Cmdtopplayers(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			var playername = arg.GetString(0);
			DestroyUi(player, KRUIName);
			KRUIplayers(player, playername.ToLower());
		}
		[ConsoleCommand("kr.topplayersstats")]
		private void Cmdtopplayersstats(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			var playername = arg.GetString(0);
			DestroyUi(player, KRUIName);
			KRUIStatsplayers(player, playername.ToLower());
		}
		[ConsoleCommand("kr.leaderboard")]
		private void Cmdleaderboard(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			DestroyUi(player, KRUIName);
			KRUITopAll(player);
		}
		[ConsoleCommand("kr.totalkills")]
		private void Cmdtotalkills(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			DestroyUi(player, KRUIName);
			KRUITotal(player);
		}
		// Hooks
		private IEnumerable<Record> GetTopKills(int page, int takeCount, string KillType)
		{
			IEnumerable<Record> data = null;
			if (KillType == "chicken")
			{
				data = _recordCache.Values.OrderByDescending(i => i.chicken);
			}
			else if (KillType == "boar")
			{
				data = _recordCache.Values.OrderByDescending(i => i.boar);
			}
			else if (KillType == "stag")
			{
				data = _recordCache.Values.OrderByDescending(i => i.stag);
			}
			else if (KillType == "wolf")
			{
				data = _recordCache.Values.OrderByDescending(i => i.wolf);
			}
			else if (KillType == "bear")
			{
				data = _recordCache.Values.OrderByDescending(i => i.bear);
			}
			else if (KillType == "polarbear")
			{
				data = _recordCache.Values.OrderByDescending(i => i.polarbear);
			}
			else if (KillType == "shark")
			{
				data = _recordCache.Values.OrderByDescending(i => i.shark);
			}
			else if (KillType == "horse")
			{
				data = _recordCache.Values.OrderByDescending(i => i.horse);
			}
			else if (KillType == "fish")
			{
				data = _recordCache.Values.OrderByDescending(i => i.fish);
			}
			else if (KillType == "scientist")
			{
				data = _recordCache.Values.OrderByDescending(i => i.scientistnpcnew);
			}
			else if (KillType == "dweller")
			{
				data = _recordCache.Values.OrderByDescending(i => i.dweller);
			}
			else if (KillType == "player")
			{
				data = _recordCache.Values.OrderByDescending(i => i.baseplayer);
			}
			else if (KillType == "loot")
			{
				data = _recordCache.Values.OrderByDescending(i => i.lootcontainer);
			}
			else if (KillType == "underwaterloot")
			{
				data = _recordCache.Values.OrderByDescending(i => i.underwaterlootcontainer);
			}
			else if (KillType == "bradhelicrate")
			{
				data = _recordCache.Values.OrderByDescending(i => i.bradhelicrate);
			}
			else if (KillType == "hackablecrate")
			{
				data = _recordCache.Values.OrderByDescending(i => i.hackablecrate);
			}
			else if (KillType == "bradleyapc")
			{
				data = _recordCache.Values.OrderByDescending(i => i.bradley);
			}
			else if (KillType == "patrolhelicopter")
			{
				data = _recordCache.Values.OrderByDescending(i => i.heli);
			}
			else if (KillType == "death")
			{
				data = _recordCache.Values.OrderByDescending(i => i.deaths);
			}
			else if (KillType == "suicide")
			{
				data = _recordCache.Values.OrderByDescending(i => i.suicides);
			}
			else if (KillType == "corpse" || KillType == "animals" || KillType == "animalsharvested")
			{
				data = _recordCache.Values.OrderByDescending(i => i.basecorpse);
			}
			else if (KillType == "pcorpse" || KillType == "bodies" || KillType == "bodiesharvested")
			{
				data = _recordCache.Values.OrderByDescending(i => i.npcplayercorpse);
			}
			else if (KillType == "trees")
			{
				data = _recordCache.Values.OrderByDescending(i => i.treescut);
			}
			else if (KillType == "oremined")
			{
				data = _recordCache.Values.OrderByDescending(i => i.oremined);
			}
			else if (KillType == "cactus")
			{
				data = _recordCache.Values.OrderByDescending(i => i.cactuscut);
			}
			else if (KillType == "wood")
			{
				data = _recordCache.Values.OrderByDescending(i => i.woodpickup);
			}
			else if (KillType == "ore")
			{
				data = _recordCache.Values.OrderByDescending(i => i.orepickup);
			}
			else if (KillType == "berries")
			{
				data = _recordCache.Values.OrderByDescending(i => i.berriespickup);
			}
			else if (KillType == "seeds")
			{
				data = _recordCache.Values.OrderByDescending(i => i.seedpickup);
			}
			else if (KillType == "mushroom")
			{
				data = _recordCache.Values.OrderByDescending(i => i.mushroompickup);
			}
			else if (KillType == "hemp")
			{
				data = _recordCache.Values.OrderByDescending(i => i.hemppickup);
			}
			else if (KillType == "corn")
			{
				data = _recordCache.Values.OrderByDescending(i => i.cornpickup);
			}
			else if (KillType == "potato")
			{
				data = _recordCache.Values.OrderByDescending(i => i.potatopickup);
			}
			else if (KillType == "pumpkin")
			{
				data = _recordCache.Values.OrderByDescending(i => i.pumpkinpickup);
			}
			else if (KillType == "turret")
			{
				data = _recordCache.Values.OrderByDescending(i => i.turret);
			}
			else if (KillType == "scarecrow")
			{
				data = _recordCache.Values.OrderByDescending(i => i.scarecrow);
			}

			return data?
			.Skip((page - 1) * takeCount)
			.Take(takeCount);
		}
		private int GetTotalKills(string KillType)
		{
			int totalkills = 0;
			foreach (var x in _recordCache.Values)
			{
				if (KillType == "chicken")
				{
					totalkills += x.chicken;
				}
				if (KillType == "boar")
				{
					totalkills += x.boar;
				}
				if (KillType == "stag")
				{
					totalkills += x.stag;
				}
				if (KillType == "wolf")
				{
					totalkills += x.wolf;
				}
				if (KillType == "bear")
				{
					totalkills += x.bear;
				}
				if (KillType == "polarbear")
				{
					totalkills += x.polarbear;
				}
				if (KillType == "shark")
				{
					totalkills += x.shark;
				}
				if (KillType == "horse")
				{
					totalkills += x.horse;
				}
				if (KillType == "fish")
				{
					totalkills += x.fish;
				}
				if (KillType == "scientist")
				{
					totalkills += x.scientistnpcnew;
				}
				if (KillType == "dweller")
				{
					totalkills += x.dweller;
				}
				if (KillType == "player")
				{
					totalkills += x.baseplayer;
				}
				if (KillType == "corpse")
				{
					totalkills += x.basecorpse;
				}
				if (KillType == "pcorpse")
				{
					totalkills += x.npcplayercorpse;
				}
				if (KillType == "deaths")
				{
					totalkills += x.deaths;
				}
				if (KillType == "suicides")
				{
					totalkills += x.suicides;
				}
				if (KillType == "lootcontainer")
				{
					totalkills += x.lootcontainer;
				}
				if (KillType == "bradhelicrate")
				{
					totalkills += x.bradhelicrate;
				}
				if (KillType == "hackablecrate")
				{
					totalkills += x.hackablecrate;
				}
				if (KillType == "bradleyapc")
				{
					totalkills += x.bradley;
				}
				if (KillType == "patrolhelicopter")
				{
					totalkills += x.heli;
				}
				if (KillType == "underwaterloot")
				{
					totalkills += x.underwaterlootcontainer;
				}
				if (KillType == "trees")
				{
					totalkills += x.treescut;
				}
				if (KillType == "oremined")
				{
					totalkills += x.oremined;
				}
				if (KillType == "cactus")
				{
					totalkills += x.cactuscut;
				}
				if (KillType == "wood")
				{
					totalkills += x.woodpickup;
				}
				if (KillType == "ore")
				{
					totalkills += x.orepickup;
				}
				if (KillType == "berries")
				{
					totalkills += x.berriespickup;
				}
				if (KillType == "seeds")
				{
					totalkills += x.seedpickup;
				}
				if (KillType == "mushroom")
				{
					totalkills += x.mushroompickup;
				}
				if (KillType == "potato")
				{
					totalkills += x.potatopickup;
				}
				if (KillType == "corn")
				{
					totalkills += x.cornpickup;
				}
				if (KillType == "pumpkin")
				{
					totalkills += x.pumpkinpickup;
				}
				if (KillType == "hemp")
				{
					totalkills += x.hemppickup;
				}
				if (KillType == "scarecrow")
				{
					totalkills += x.scarecrow;
				}
				if (KillType == "turret")
				{
					totalkills += x.turret;
				}
			}
			return totalkills;
		}
		private void KRKillMessages(BasePlayer player, string languagetype, int newkills, bool killchat)
		{
			if (!config.chatui.ShowKillMessages || !killchat) return;
			int Killinterval = config.chatui.KillMessageInterval;
			int KillMessageLimit = config.chatui.KillMessageLimit;
			if (newkills == Killinterval)
			{
				// UINotify
				if (UINotify != null && config.chatui.enableuinotify)
				{
					UINotify.Call("SendNotify", player, config.chatui.uinotifytype, KRLang(languagetype, player.UserIDString, newkills));
				}
				if (!config.chatui.disablechats)
				{
					player.ChatMessage(KRLang(languagetype, player.UserIDString, newkills));
				}
			}
			else
			{
				double Killintervals = Killinterval;
				for (int k = 0; k < KillMessageLimit; ++k)
				{
					Killintervals += Killinterval + k / KillMessageLimit;

					if (newkills == Killintervals)
					{
						if (UINotify != null && config.chatui.enableuinotify)
						{
							UINotify.Call("SendNotify", player, config.chatui.uinotifytype, KRLang(languagetype, player.UserIDString, newkills));
						}
						if (!config.chatui.disablechats)
						{
							player.ChatMessage(KRLang(languagetype, player.UserIDString, newkills));
						}
					}
				}
			}
		}
		private void PlayerKillsChat(BasePlayer player, string playerinfo)
		{
			if (playerinfo == null) return;
			var user = _recordCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(playerinfo));
			if (playerinfo == player.UserIDString)
			{
				user = _recordCache.ToList().FirstOrDefault(x => x.Value.id.ToString().Contains(playerinfo));
			}
			else
			{
				user = _recordCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(playerinfo));
			}
			if (user.Value == null)
			{
				player.ChatMessage(KRLang("noplayer", player.UserIDString, playerinfo));
				return;
			}
			var textTable = new TextTable();

			textTable.AddColumn($"{user.Value.displayname} \n");
			textTable.AddColumn("---------------------- \n");

			if (config.trackingoptions.Trackchicken)
			{ 
				textTable.AddColumn($"{KRLang("chicken", user.Value.id, user.Value.chicken)} \n");
			}
			if (config.trackingoptions.Trackboar) 
			{ 
				textTable.AddColumn($"{KRLang("boar", user.Value.id, user.Value.boar)} \n");
			}
			if (config.trackingoptions.Trackstag) 
			{ 
				textTable.AddColumn($"{KRLang("stag", user.Value.id, user.Value.stag)} \n");
			}
			if (config.trackingoptions.Trackwolf) 
			{ 
				textTable.AddColumn($"{KRLang("wolf", user.Value.id, user.Value.wolf)} \n");
			}
			if (config.trackingoptions.Trackbear)
			{ 
				textTable.AddColumn($"{KRLang("bear", user.Value.id, user.Value.bear)} \n"); 
			}
			if (config.trackingoptions.Trackpolarbear)
			{ 
				textTable.AddColumn($"{KRLang("polarbear", user.Value.id, user.Value.polarbear)} \n"); 
			}
			if (config.trackingoptions.Trackshark)
			{ 
				textTable.AddColumn($"{KRLang("simpleshark", user.Value.id, user.Value.shark)} \n"); 
			}
			if (config.trackingoptions.Trackhorse)
			{ 
				textTable.AddColumn($"{KRLang("horse", user.Value.id, user.Value.horse)} \n"); 
			}
			if (config.trackingoptions.Trackfish)
			{ 
				textTable.AddColumn($"{KRLang("fish", user.Value.id, user.Value.fish)} \n"); 
			}
			if (config.trackingoptions.Trackscientist) 
			{ 
				textTable.AddColumn($"{KRLang("scientists", user.Value.id, user.Value.scientistnpcnew)} \n");
			}
			if (config.trackingoptions.Trackscarecrow) 
			{ 
				textTable.AddColumn($"{KRLang("scarecrow", user.Value.id, user.Value.scarecrow)} \n");
			}
			if (config.trackingoptions.Trackdweller) 
			{ 
				textTable.AddColumn($"{KRLang("dweller", user.Value.id, user.Value.dweller)} \n"); 
			}
			if (config.trackingoptions.TrackPlayer) 
			{ 
				textTable.AddColumn($"{KRLang("players", user.Value.id, user.Value.baseplayer)} \n");
			}
			if (config.trackingoptions.Trackdeaths) 
			{ 
				textTable.AddColumn($"{KRLang("deaths", user.Value.id, user.Value.deaths)} \n"); 
			}
			if (config.trackingoptions.Tracksuicides) 
			{ 
				textTable.AddColumn($"{KRLang("suicide", user.Value.id, user.Value.suicides)} \n"); 
			}
			if (config.trackingoptions.Tracklootcontainer)
			{ 
				textTable.AddColumn($"{KRLang("loot", user.Value.id, user.Value.lootcontainer)} \n");
			}
			if (config.trackingoptions.Trackunderwaterlootcontainer)
			{ 
				textTable.AddColumn($"{KRLang("unloot", user.Value.id, user.Value.underwaterlootcontainer)} \n");
			}
			if (config.trackingoptions.Trackbradhelicrate)
			{ 
				textTable.AddColumn($"{KRLang("bradheliloot", user.Value.id, user.Value.bradhelicrate)} \n");
			}
			if (config.trackingoptions.Trackhackablecrate)
			{ 
				textTable.AddColumn($"{KRLang("hackloot", user.Value.id, user.Value.hackablecrate)} \n");
			}
			if (config.trackingoptions.TrackBradley)
			{ 
				textTable.AddColumn($"{KRLang("bradley", user.Value.id, user.Value.bradley)} \n");
			}
			if (config.trackingoptions.TrackHeli)
			{ 
				textTable.AddColumn($"{KRLang("heli", user.Value.id, user.Value.heli)} \n");
			}
			if (config.trackingoptions.Trackturret)
			{ 
				textTable.AddColumn($"{KRLang("turret", user.Value.id, user.Value.turret)} \n");
			}
			if (config.trackingoptions.TrackAnimalHarvest) 
			{ 
				textTable.AddColumn($"{KRLang("corpse", user.Value.id, user.Value.basecorpse)} \n");
			}
			if (config.trackingoptions.TrackCorpseHarvest) 
			{ 
				textTable.AddColumn($"{KRLang("pcorpse", user.Value.id, user.Value.npcplayercorpse)} \n");
			}
	
			player.ChatMessage($"Kill Records:\n\n {textTable}");
		}
		private void PlayerStatsChat(BasePlayer player, string playerinfo)
		{
			if (playerinfo == null) return;
			var user = _recordCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(playerinfo));
			if (playerinfo == player.UserIDString)
			{
				user = _recordCache.ToList().FirstOrDefault(x => x.Value.id.ToString().Contains(playerinfo));
			}
			else
			{
				user = _recordCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(playerinfo));
			}
			if (user.Value == null)
			{
				player.ChatMessage(KRLang("noplayer", player.UserIDString, playerinfo));
				return;
			}
			var textTable = new TextTable();

			textTable.AddColumn($"{user.Value.displayname} \n");
			textTable.AddColumn("---------------------- \n");

			if (config.harvestoptions.treescut)
			{ 
				textTable.AddColumn($"{KRLang("treecut", user.Value.id, user.Value.treescut)} \n");
			}
			if (config.harvestoptions.oremined)
			{ 
				textTable.AddColumn($"{KRLang("oremined", user.Value.id, user.Value.oremined)} \n");
			}
			if (config.harvestoptions.woodpickup)
			{ 
				textTable.AddColumn($"{KRLang("woodpickup", user.Value.id, user.Value.woodpickup)} \n");
			}
			if (config.harvestoptions.cactuscut)
			{ 
				textTable.AddColumn($"{KRLang("catuscut", user.Value.id, user.Value.cactuscut)} \n");
			}
			if (config.harvestoptions.orepickup)
			{ 
				textTable.AddColumn($"{KRLang("orepickup", user.Value.id, user.Value.orepickup)} \n");
			}
			if (config.harvestoptions.berriespickup)
			{ 
				textTable.AddColumn($"{KRLang("berries", user.Value.id, user.Value.berriespickup)} \n");
			}
			if (config.harvestoptions.mushroompickup)
			{ 
				textTable.AddColumn($"{KRLang("mushroom", user.Value.id, user.Value.mushroompickup)} \n");
			}
			if (config.harvestoptions.hemppickup)
			{ 
				textTable.AddColumn($"{KRLang("hemp", user.Value.id, user.Value.hemppickup)} \n");
			}
			if (config.harvestoptions.potatopickup)
			{ 
				textTable.AddColumn($"{KRLang("potato", user.Value.id, user.Value.potatopickup)} \n");
			}
			if (config.harvestoptions.pumpkinpickup)
			{ 
				textTable.AddColumn($"{KRLang("pumpkin", user.Value.id, user.Value.pumpkinpickup)} \n");
			}
			if (config.harvestoptions.seedpickup)
			{ 
				textTable.AddColumn($"{KRLang("seeds", user.Value.id, user.Value.seedpickup)} \n");
			}
			if (config.harvestoptions.cornpickup)
			{ 
				textTable.AddColumn($"{KRLang("corn", user.Value.id, user.Value.cornpickup)} \n");
			}
	
			player.ChatMessage($"Harvest Records:\n\n {textTable}");
		}
		private void TopKillsChat(BasePlayer player, string KillType)
		{
			var textTable = new TextTable();
			var vals = GetTopKills(0, 10, KillType);
			var index = 0;
			int n = 0;
			for (int i = 0; i < 10; i++)
			{
				n++;
				if (vals.ElementAtOrDefault(index) == null)
				{
					continue;
				}
				var playerdata = vals.ElementAtOrDefault(index);
				if (playerdata == null) continue;
				if (config.trackingoptions.Trackchicken && KillType == "chicken" && playerdata.chicken != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.chicken} \n");
				}
				if (config.trackingoptions.Trackboar && KillType == "boar" && playerdata.boar != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.boar} \n");
				}
				if (config.trackingoptions.Trackstag && KillType == "stag" && playerdata.stag != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.stag} \n");
				}
				if (config.trackingoptions.Trackwolf && KillType == "wolf" && playerdata.wolf != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.wolf} \n");
				}
				if (config.trackingoptions.Trackbear && KillType == "bear" && playerdata.bear != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.bear} \n");
				}
				if (config.trackingoptions.Trackpolarbear && KillType == "polarbear" && playerdata.polarbear != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.polarbear} \n");
				}
				if (config.trackingoptions.Trackshark && KillType == "shark" && playerdata.shark != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.shark} \n");
				}
				if (config.trackingoptions.Trackhorse && (KillType == "horse" || KillType == "ridablehorse") && playerdata.horse != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.horse} \n");
				}
				if (config.trackingoptions.Trackfish && KillType == "fish" && playerdata.fish != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.fish} \n");
				}
				if (config.trackingoptions.Trackscientist && KillType == "scientist" && playerdata.scientistnpcnew != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.scientistnpcnew} \n");
				}
				if (config.trackingoptions.Trackscarecrow && KillType == "scarecrow" && playerdata.scarecrow != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.scarecrow} \n");
				}
				if (config.trackingoptions.Trackdweller && KillType == "dweller" && playerdata.dweller != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.dweller} \n");
				}
				if (config.trackingoptions.TrackPlayer && KillType == "player" && playerdata.baseplayer != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.baseplayer} \n");
				}
				if (config.trackingoptions.Tracklootcontainer && (KillType == "loot" || KillType == "lootcontainer") && playerdata.lootcontainer != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.lootcontainer} \n");
				}
				if (config.trackingoptions.Trackunderwaterlootcontainer && (KillType == "underwaterloot" || KillType == "underwaterlootcontainer") && playerdata.underwaterlootcontainer != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.underwaterlootcontainer} \n");
				}
				if (config.trackingoptions.Trackbradhelicrate && (KillType == "bradhelicrate") && playerdata.bradhelicrate != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.bradhelicrate} \n");
				}
				if (config.trackingoptions.Trackhackablecrate && KillType == "hackablecrate" && playerdata.hackablecrate != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.hackablecrate} \n");
				}
				if (config.trackingoptions.TrackBradley && (KillType == "bradleyapc") && playerdata.bradley != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.bradley} \n");
				}
				if (config.trackingoptions.TrackHeli && KillType == "patrolhelicopter" && playerdata.heli != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.heli} \n");
				}
				if (config.trackingoptions.Trackturret && KillType == "turret" && playerdata.turret != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.turret} \n");
				}
				if (config.trackingoptions.Trackdeaths && KillType == "death" && playerdata.deaths != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.deaths} \n");
				}
				if (config.trackingoptions.Tracksuicides && KillType == "suicide" && playerdata.suicides != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.suicides} \n");
				}
				if (config.trackingoptions.Tracksuicides && (KillType == "corpse" || KillType == "animals" || KillType == "animalsharvested") && playerdata.basecorpse != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.basecorpse} \n");
				}
				if (config.trackingoptions.Tracksuicides && (KillType == "pcorpse" || KillType == "bodies" || KillType == "bodiesharvested") && playerdata.npcplayercorpse != 0)
				{
					textTable.AddColumn($"{n}. {playerdata.displayname}: {playerdata.npcplayercorpse} \n");
				}
				index++;
			}
			player.ChatMessage($"Kill Records Top {UILabels(player, KillType)}:\n\n {textTable}");
		}
		private void TotalKillsChat(BasePlayer player)
		{
			var textTable = new TextTable();
			textTable.AddColumn($"{KRLang("totalkills", player.UserIDString)} \n");
			textTable.AddColumn("---------------------- \n");

			if (config.trackingoptions.Trackchicken)
			{ 
				textTable.AddColumn($"{KRLang("chicken", player.UserIDString, GetTotalKills("chicken"))} \n");
			}
			if (config.trackingoptions.Trackboar) 
			{ 
				textTable.AddColumn($"{KRLang("boar", player.UserIDString, GetTotalKills("boar"))} \n");
			}
			if (config.trackingoptions.Trackstag) 
			{ 
				textTable.AddColumn($"{KRLang("stag", player.UserIDString, GetTotalKills("stag"))} \n");
			}
			if (config.trackingoptions.Trackwolf) 
			{ 
				textTable.AddColumn($"{KRLang("wolf", player.UserIDString, GetTotalKills("wolf"))} \n");
			}
			if (config.trackingoptions.Trackbear)
			{ 
				textTable.AddColumn($"{KRLang("bear", player.UserIDString, GetTotalKills("bear"))} \n"); 
			}
			if (config.trackingoptions.Trackpolarbear)
			{ 
				textTable.AddColumn($"{KRLang("polarbear", player.UserIDString, GetTotalKills("polarbear"))} \n"); 
			}
			if (config.trackingoptions.Trackshark)
			{ 
				textTable.AddColumn($"{KRLang("simpleshark", player.UserIDString, GetTotalKills("shark"))} \n"); 
			}
			if (config.trackingoptions.Trackfish)
			{ 
				textTable.AddColumn($"{KRLang("fish", player.UserIDString, GetTotalKills("fish"))} \n"); 
			}
			if (config.trackingoptions.Trackscientist) 
			{ 
				textTable.AddColumn($"{KRLang("scientists", player.UserIDString, GetTotalKills("scientist"))} \n");
			}
			if (config.trackingoptions.Trackscarecrow) 
			{ 
				textTable.AddColumn($"{KRLang("scarecrow", player.UserIDString, GetTotalKills("scarecrow"))} \n");
			}
			if (config.trackingoptions.Trackdweller) 
			{ 
				textTable.AddColumn($"{KRLang("dweller", player.UserIDString, GetTotalKills("dweller"))} \n"); 
			}
			if (config.trackingoptions.TrackPlayer) 
			{ 
				textTable.AddColumn($"{KRLang("players", player.UserIDString, GetTotalKills("player"))} \n");
			}
			if (config.trackingoptions.Trackdeaths) 
			{ 
				textTable.AddColumn($"{KRLang("deaths", player.UserIDString, GetTotalKills("deaths"))} \n"); 
			}
			if (config.trackingoptions.Tracksuicides) 
			{ 
				textTable.AddColumn($"{KRLang("suicide", player.UserIDString, GetTotalKills("suicides"))} \n"); 
			}
			if (config.trackingoptions.Tracklootcontainer)
			{ 
				textTable.AddColumn($"{KRLang("loot", player.UserIDString, GetTotalKills("lootcontainer"))} \n");
			}
			if (config.trackingoptions.Trackunderwaterlootcontainer)
			{ 
				textTable.AddColumn($"{KRLang("unloot", player.UserIDString, GetTotalKills("underwaterloot"))} \n");
			}
			if (config.trackingoptions.Trackbradhelicrate)
			{ 
				textTable.AddColumn($"{KRLang("bradheliloot", player.UserIDString, GetTotalKills("bradhelicrate"))} \n");
			}
			if (config.trackingoptions.Trackhackablecrate)
			{ 
				textTable.AddColumn($"{KRLang("hackloot", player.UserIDString, GetTotalKills("hackablecrate"))} \n");
			}
			if (config.trackingoptions.TrackBradley)
			{ 
				textTable.AddColumn($"{KRLang("bradley", player.UserIDString, GetTotalKills("bradleyapc"))} \n");
			}
			if (config.trackingoptions.TrackHeli)
			{ 
				textTable.AddColumn($"{KRLang("heli", player.UserIDString, GetTotalKills("patrolhelicopter"))} \n");
			}
			if (config.trackingoptions.TrackAnimalHarvest) 
			{ 
				textTable.AddColumn($"{KRLang("corpse", player.UserIDString, GetTotalKills("corpse"))} \n");
			}
			if (config.trackingoptions.TrackCorpseHarvest) 
			{ 
				textTable.AddColumn($"{KRLang("pcorpse", player.UserIDString, GetTotalKills("pcorpse"))} \n");
			}
	
			player.ChatMessage($"Kill Records:\n\n {textTable}");
		}
		private void TotalStatsChat(BasePlayer player)
		{
			var textTable = new TextTable();
			textTable.AddColumn($"{KRLang("totalstats", player.UserIDString)} \n");
			textTable.AddColumn("---------------------- \n");

			if (config.harvestoptions.treescut)
			{ 
				textTable.AddColumn($"{KRLang("treecut", player.UserIDString, GetTotalKills("tree"))} \n");
			}
			if (config.harvestoptions.oremined)
			{ 
				textTable.AddColumn($"{KRLang("oremined", player.UserIDString, GetTotalKills("oremined"))} \n");
			}
			if (config.harvestoptions.cactuscut)
			{ 
				textTable.AddColumn($"{KRLang("cactuscut", player.UserIDString, GetTotalKills("cactus"))} \n");
			}
			if (config.harvestoptions.woodpickup)
			{ 
				textTable.AddColumn($"{KRLang("woodpickup", player.UserIDString, GetTotalKills("wood"))} \n");
			}
			if (config.harvestoptions.orepickup)
			{ 
				textTable.AddColumn($"{KRLang("orepickup", player.UserIDString, GetTotalKills("ore"))} \n");
			}
			if (config.harvestoptions.berriespickup)
			{ 
				textTable.AddColumn($"{KRLang("berries", player.UserIDString, GetTotalKills("berries"))} \n");
			}
			if (config.harvestoptions.seedpickup)
			{ 
				textTable.AddColumn($"{KRLang("seeds", player.UserIDString, GetTotalKills("seeds"))} \n");
			}
			if (config.harvestoptions.mushroompickup)
			{ 
				textTable.AddColumn($"{KRLang("mushroom", player.UserIDString, GetTotalKills("mushrrom"))} \n");
			}
			if (config.harvestoptions.potatopickup)
			{ 
				textTable.AddColumn($"{KRLang("potatos", player.UserIDString, GetTotalKills("potatos"))} \n");
			}
			if (config.harvestoptions.pumpkinpickup)
			{ 
				textTable.AddColumn($"{KRLang("pumpkin", player.UserIDString, GetTotalKills("pumpkin"))} \n");
			}
			if (config.harvestoptions.cornpickup)
			{ 
				textTable.AddColumn($"{KRLang("corn", player.UserIDString, GetTotalKills("corn"))} \n");
			}
			if (config.harvestoptions.hemppickup)
			{ 
				textTable.AddColumn($"{KRLang("hemp", player.UserIDString, GetTotalKills("hemp"))} \n");
			}
	
			player.ChatMessage($"Harvest Records:\n\n {textTable}");
		}
		private string KillTypesEnabled()
		{
			string KillType = "";
			if (config.trackingoptions.Trackchicken)
			{ KillType = "chicken"; }
			else if (config.trackingoptions.Trackboar)
			{ KillType = "boar"; }
			else if(config.trackingoptions.Trackstag)
			{ KillType = "stag"; }
			else if (config.trackingoptions.Trackwolf)
			{ KillType = "wolf"; }
			else if (config.trackingoptions.Trackbear)
			{ KillType = "bear"; }
			else if (config.trackingoptions.Trackpolarbear)
			{ KillType = "polarbear"; }
			else if (config.trackingoptions.Trackshark)
			{ KillType = "shark"; }
			else if (config.trackingoptions.Trackhorse)
			{ KillType = "horse"; }
			else if (config.trackingoptions.Trackfish)
			{ KillType = "fish"; }
			else if (config.trackingoptions.TrackPlayer)
			{ KillType = "player"; }
			else if (config.trackingoptions.Trackscientist)
			{ KillType = "scientist"; }
			else if (config.trackingoptions.Trackscarecrow)
			{ KillType = "scarecrow"; }
			else if (config.trackingoptions.Trackdweller)
			{ KillType = "dweller"; }
			else if (config.trackingoptions.TrackAnimalHarvest)
			{ KillType = "corpse"; }
			else if (config.trackingoptions.TrackCorpseHarvest)
			{ KillType = "pcorpse"; }
			else if (config.trackingoptions.Tracklootcontainer)
			{ KillType = "loot"; }
			else if (config.trackingoptions.Trackunderwaterlootcontainer)
			{ KillType = "underwaterloot"; }
			else if (config.trackingoptions.Trackbradhelicrate)
			{ KillType = "bradhelicrate"; }
			else if (config.trackingoptions.Trackhackablecrate)
			{ KillType = "hackablecrate"; }
			else if (config.trackingoptions.Trackbradhelicrate)
			{ KillType = "bradleyapc"; }
			else if (config.trackingoptions.TrackBradley)
			{ KillType = "patrolhelicopter"; }
			else if (config.trackingoptions.TrackHeli)
			{ KillType = "deaths"; }
			else if (config.trackingoptions.Trackturret)
			{ KillType = "turret"; }
			else if (config.trackingoptions.Tracksuicides)
			{ KillType = "suicides"; }
			return KillType;
		}
		private string GatherTypesEnabled()
		{
			string KillType = "";
			if (config.harvestoptions.treescut)
			{ KillType = "trees"; }
			else if (config.harvestoptions.oremined)
			{ KillType = "oremined"; }
			else if (config.harvestoptions.cactuscut)
			{ KillType = "catus"; }
			else if (config.harvestoptions.woodpickup)
			{ KillType = "wood"; }
			else if (config.harvestoptions.orepickup)
			{ KillType = "ore"; }
			else if (config.harvestoptions.berriespickup)
			{ KillType = "berries"; }
			else if (config.harvestoptions.seedpickup)
			{ KillType = "seeds"; }
			else if (config.harvestoptions.mushroompickup)
			{ KillType = "mushroom"; }
			else if (config.harvestoptions.cornpickup)
			{ KillType = "corn"; }
			else if (config.harvestoptions.potatopickup)
			{ KillType = "potato"; }
			else if (config.harvestoptions.pumpkinpickup)
			{ KillType = "pumpkin"; }
			else if (config.harvestoptions.hemppickup)
			{ KillType = "hemp"; }
			return KillType;
		}
		private string LeaderboardPositionLB(int position)
        {
			var LB = "";
			// Row 1
			if (position == 1)
			{
				LB = "0.005 0.715";
			}
			if (position == 2)
			{
				LB = "0.15 0.715";
			}
			if (position == 3)
			{
				LB = "0.295 0.715";
			}
			if (position == 4)
			{
				LB = "0.435 0.715";
			}
			if (position == 5)
			{
				LB = "0.575 0.715";
			}
			if (position == 6)
			{
				LB = "0.715 0.715";
			}
			if (position == 7)
			{
				LB = "0.855 0.715";
			}
			// Row 2
			if (position == 8)
			{
				LB = "0.005 0.47";
			}
			if (position == 9)
			{
				LB = "0.15 0.47";
			}
			if (position == 10)
			{
				LB = "0.295 0.47";
			}
			if (position == 11)
			{
				LB = "0.435 0.47";
			}
			if (position == 12)
			{
				LB = "0.575 0.47";
			}
			if (position == 13)
			{
				LB = "0.715 0.47";
			}
			if (position == 14)
			{
				LB = "0.855 0.47";
			}
			// Row 3
			if (position == 15)
			{
				LB = "0.005 0.235";
			}
			if (position == 16)
			{
				LB = "0.15 0.235";
			}
			if (position == 17)
			{
				LB = "0.295 0.235";
			}
			if (position == 18)
			{
				LB = "0.435 0.235";
			}
			if (position == 19)
			{
				LB = "0.575 0.235";
			}
			if (position == 20)
			{
				LB = "0.715 0.235";
			}
			if (position == 21)
			{
				LB = "0.855 0.235";
			}
			// Row 4
			if (position == 22)
			{
				LB = "0.005 0.01";
			}
			if (position == 23)
			{
				LB = "0.15 0.01";
			}
			if (position == 24)
			{
				LB = "0.295 0.01";
			}
			// Return Position

			return LB;
        }
		private string LeaderboardPositionRT(int position)
        {
			var RT = "";
			// Row 1
			if (position == 1)
			{
				RT = "0.145 0.94";
			}
			if (position == 2)
			{
				RT = "0.29 0.94";
			}
			if (position == 3)
			{
				RT = "0.43 0.94";
			}
			if (position == 4)
			{
				RT = "0.57 0.94";
			}
			if (position == 5)
			{
				RT = "0.71 0.94";
			}
			if (position == 6)
			{
				RT = "0.85 0.94";
			}
			if (position == 7)
			{
				RT = "0.99 0.94";
			}
			// Row 2
			if (position == 8)
			{
				RT = "0.145 0.705";
			}
			if (position == 9)
			{
				RT = "0.29 0.705";
			}
			if (position == 10)
			{
				RT = "0.43 0.705";
			}
			if (position == 11)
			{
				RT = "0.57 0.705";
			}
			if (position == 12)
			{
				RT = "0.71 0.705";
			}
			if (position == 13)
			{
				RT = "0.85 0.705";
			}
			if (position == 14)
			{
				RT = "0.99 0.705";
			}
			//Row 3
			if (position == 15)
			{
				RT = "0.145 0.46";
			}
			if (position == 16)
			{
				RT = "0.29 0.46";
			}
			if (position == 17)
			{
				RT = "0.43 0.46";
			}
			if (position == 18)
			{
				RT = "0.57 0.46";
			}
			if (position == 19)
			{
				RT = "0.71 0.46";
			}
			if (position == 20)
			{
				RT = "0.85 0.46";
			}
			if (position == 21)
			{
				RT = "0.99 0.46";
			}
			// Row 4
			if (position == 22)
			{
				RT = "0.145 0.225";
			}
			if (position == 23)
			{
				RT = "0.29 0.225";
			}
			if (position == 24)
			{
				RT = "0.43 0.225";
			}
			// Return Position
			return RT;
        }
		#endregion

		#region KRUI
		private object UILabels(BasePlayer player, string KillType)
        {
			if(KillType == "chicken")
            {
				return KRLang("chickenui", player.UserIDString);
			}
			if(KillType == "boar")
            {
				return KRLang("boarui", player.UserIDString);
			}
			if(KillType == "stag")
            {
				return KRLang("stagui", player.UserIDString);
			}
			if(KillType == "wolf")
            {
				return KRLang("wolfui", player.UserIDString);
			}
			if(KillType == "bear")
            {
				return KRLang("bearui", player.UserIDString);
			}
			if(KillType == "polarbear")
            {
				return KRLang("polarbearui", player.UserIDString);
			}
			if(KillType == "shark")
            {
				return KRLang("sharkui", player.UserIDString);
			}
			if(KillType == "horse")
            {
				return KRLang("horseui", player.UserIDString);
			}
			if(KillType == "fish")
            {
				return KRLang("fishui", player.UserIDString);
			}
			if(KillType == "scientist")
            {
				return KRLang("scientistui", player.UserIDString);
			}
			if(KillType == "scarecrow")
            {
				return KRLang("scarecrowui", player.UserIDString);
			}
			if(KillType == "dweller")
            {
				return KRLang("dwellerui", player.UserIDString);
			}
			if(KillType == "loot" || KillType == "lootcontainer")
            {
				return KRLang("lootui", player.UserIDString);
			}
			if(KillType == "bradhelicrate")
            {
				return KRLang("bradheliui", player.UserIDString);
			}
			if(KillType == "hackablecrate")
            {
				return KRLang("hackableui", player.UserIDString);
			}
			if(KillType == "bradleyapc")
            {
				return KRLang("bradleyui", player.UserIDString);
			}
			if(KillType == "patrolhelicopter")
            {
				return KRLang("patrolhelicopterui", player.UserIDString);
			}
			if(KillType == "turret")
            {
				return KRLang("turretui", player.UserIDString);
			}
			if(KillType == "underwaterloot" || KillType == "underwaterlootcontainer")
            {
				return KRLang("wlootui", player.UserIDString);
			}
			if(KillType == "death")
            {
				return KRLang("deathui", player.UserIDString);
			}
			if(KillType == "suicide")
            {
				return KRLang("suicideui", player.UserIDString);
			}
			if(KillType == "player")
            {
				return KRLang("playerui", player.UserIDString);
			}
			if(KillType == "corpse" || KillType == "animals" || KillType == "animalsharvested")
            {
				return KRLang("corpseui", player.UserIDString);
			}
			if(KillType == "pcorpse" || KillType == "bodies" || KillType == "bodiesharvested")
            {
				return KRLang("pcorpseui", player.UserIDString);
			}
			if (KillType == "trees")
			{
				return KRLang("treeui", player.UserIDString);
			}
			if (KillType == "oremined")
			{
				return KRLang("oreminedui", player.UserIDString);
			}
			if (KillType == "cactus")
			{
				return KRLang("cactusui", player.UserIDString);
			}
			if (KillType == "wood")
			{
				return KRLang("woodui", player.UserIDString);
			}
			if (KillType == "ore")
			{
				return KRLang("oreui", player.UserIDString);
			}
			if (KillType == "berries")
			{
				return KRLang("berriesui", player.UserIDString);
			}
			if (KillType == "seeds")
			{
				return KRLang("seedsui", player.UserIDString);
			}
			if (KillType == "potato")
			{
				return KRLang("potatoui", player.UserIDString);
			}
			if (KillType == "pumpkin")
			{
				return KRLang("pumpkinui", player.UserIDString);
			}
			if (KillType == "corn")
			{
				return KRLang("cornui", player.UserIDString);
			}
			if (KillType == "hemp")
			{
				return KRLang("hempui", player.UserIDString);
			}
			if (KillType == "mushroom")
			{
				return KRLang("mushroomui", player.UserIDString);
			}

			return null;
        }
		private CuiPanel KRUIPanel(string anchorMin, string anchorMax, string color = "0 0 0 0")
		{
			return new CuiPanel
			{
				Image =
				{
					Color = color
				},
				RectTransform =
				{
					AnchorMin = anchorMin,
					AnchorMax = anchorMax
				}
			};
		}
		private CuiLabel KRUILabel(string text, int i, float height, TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 13, string xMin = "0", string xMax = "1", string color = "1.0 1.0 1.0 1.0")
		{
			return new CuiLabel
			{
				Text =
				{
					Text = text,
					FontSize = fontSize,
					Align = align,
					Color = color
				},
				RectTransform =
				{
					AnchorMin = $"{xMin} {1 - height*i + i * .002f}",
					AnchorMax = $"{xMax} {1 - height*(i-1) + i * .002f}"
				}
			};
		}
		private CuiButton KRUIButton(string command, int i, float rowHeight, int fontSize = 11, string color = "1.0 0.0 0.0 0.7", string content = "+", string xMin = "0", string xMax = "1", TextAnchor align = TextAnchor.MiddleLeft)
		{
			return new CuiButton
			{
					Button =
				{
					Command = command,
					Color = $"{color}"
				},
					RectTransform =
				{
					AnchorMin = $"{xMin} {1 - rowHeight*i + i * .002f}",
					AnchorMax = $"{xMax} {1 - rowHeight*(i-1) + i * .002f}"
				},
					Text =
				{
					Text = content,
					FontSize = fontSize,
					Align = align,
				}
			};
		}
		private void KRUIplayers(BasePlayer player, string playerinfo)
		{
			if (playerinfo == null) return;
			var user = _recordCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(playerinfo));
			if (playerinfo == player.UserIDString)
			{
				user = _recordCache.ToList().FirstOrDefault(x => x.Value.id.ToString().Contains(playerinfo));
			}
			else
			{
				user = _recordCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(playerinfo));
			}
			if (user.Value == null)
			{
				player.ChatMessage(KRLang("noplayer", player.UserIDString, playerinfo));
				return;
			}
			var KRUIelements = new CuiElementContainer();
			var height = 0.041f;
			// Main UI
			KRUIelements.Add(new CuiPanel 
			{ 
				Image = 
				{ 
					Color = "0.0 0.0 0.0 0.95" 
				}, 
				RectTransform = 
				{ 
					AnchorMin = $"0.83 0.27", 
					AnchorMax = $"0.997 0.95"
				}, 
				CursorEnabled = true 
			}, "Overlay", KRUIName);
			// Close Button
			KRUIelements.Add(new CuiButton 
			{ 
				Button = 
				{ 
					Close = KRUIName, 
					Color = "0.0 0.0 0.0 0.0" 
				}, 
				RectTransform = 
				{ 
					AnchorMin = "0.87 0.94", 
					AnchorMax = "1.0 1.002" 
				}, 
				Text = 
				{
					Text = "ⓧ",
					FontSize = 20,
					Color = "1.0 0.0 0.0",
					Align = TextAnchor.MiddleCenter 
				} 
			}, KRUIName);
			// Main UI Label
			KRUIelements.Add(KRUILabel("☠ Kill Records:", 1, 0.043f, TextAnchor.MiddleCenter, 17, "0.03", "0.85", "1.0 0.0 0.0 1.0"), KRUIName);
			// Top Kills Button
			var KillType = KillTypesEnabled();
			KRUIelements.Add(new CuiButton
			{
				Button =
				{
					Command = $"kr.topkills {KillType} {player.UserIDString}",
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.00 0.91",
					AnchorMax = "0.07 0.99"
				},
				Text =
				{
					Text = "⋘",
					FontSize = 23,
					Color = "0.0 0.0 1.0",
					Align = TextAnchor.MiddleCenter
				}
			}, KRUIName);
			// Main UI
			KRUIelements.Add(KRUIPanel("0.04 0.02", "0.98 0.94"), KRUIName, KRUIMainName);
			KRUIelements.Add(KRUILabel($"〖 {user.Value.displayname} 〗", 1, height, TextAnchor.MiddleCenter, 17), KRUIMainName);
			//i++;
			KRUIelements.Add(KRUILabel(("────────────────────────"), 2, height, TextAnchor.MiddleCenter), KRUIMainName);
			int i = 2;
			
			if (config.trackingoptions.Trackchicken)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("chicken", user.Value.id, user.Value.chicken)}"), config.orderoptions.chickenpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackboar) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("boar", user.Value.id, user.Value.boar)}"), config.orderoptions.boarpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackstag) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("stag", user.Value.id, user.Value.stag)}"), config.orderoptions.stagpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackwolf) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("wolf", user.Value.id, user.Value.wolf)}"), config.orderoptions.wolfpos +i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackbear) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("bear", user.Value.id, user.Value.bear)}"), config.orderoptions.bearpos + i, height), KRUIMainName);
			}

			if (config.trackingoptions.Trackpolarbear) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("polarbear", user.Value.id, user.Value.polarbear)}"), config.orderoptions.polarbearpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackshark) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("simpleshark", user.Value.id, user.Value.shark)}"), config.orderoptions.sharkpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackhorse) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("horse", user.Value.id, user.Value.horse)}"), config.orderoptions.horsepos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackfish) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("fish", user.Value.id, user.Value.fish)}"), config.orderoptions.fishpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackscientist) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("scientists", user.Value.id, user.Value.scientistnpcnew)}"), config.orderoptions.scientistpos + i, height), KRUIMainName);
			}

			if (config.trackingoptions.Trackscarecrow) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("scarecrow", user.Value.id, user.Value.scarecrow)}"), config.orderoptions.scarecrowpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackdweller) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("dweller", user.Value.id, user.Value.dweller)}"), config.orderoptions.dwellerpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.TrackPlayer) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("players", user.Value.id, user.Value.baseplayer)}"), config.orderoptions.playerpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackdeaths) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("deaths", user.Value.id, user.Value.deaths)}"), config.orderoptions.deathpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Tracksuicides) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("suicide", user.Value.id, user.Value.suicides)}"), config.orderoptions.suicidepos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Tracklootcontainer) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("loot", user.Value.id, user.Value.lootcontainer)}"), config.orderoptions.lootpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackunderwaterlootcontainer) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("unloot", user.Value.id, user.Value.underwaterlootcontainer)}"), config.orderoptions.unlootpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackbradhelicrate) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("bradheliloot", user.Value.id, user.Value.bradhelicrate)}"), config.orderoptions.bradhelicratepos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackhackablecrate) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("hackloot", user.Value.id, user.Value.hackablecrate)}"), config.orderoptions.hackablecratepos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.TrackBradley) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("bradley", user.Value.id, user.Value.bradley)}"), config.orderoptions.bradleypos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.TrackHeli) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("heli", user.Value.id, user.Value.heli)}"), config.orderoptions.helipos + i, height), KRUIMainName); 
			}

			if (config.trackingoptions.Trackturret) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("turret", user.Value.id, user.Value.turret)}"), config.orderoptions.turretpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.TrackAnimalHarvest)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("corpse", user.Value.id, user.Value.basecorpse)}"), config.orderoptions.corpsepos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.TrackCorpseHarvest)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("pcorpse", user.Value.id, user.Value.npcplayercorpse)}"), config.orderoptions.pcorpsepos + i, height), KRUIMainName);
			}
			
			CuiHelper.AddUi(player, KRUIelements);
			return;
		}
		private void KRUIStatsplayers(BasePlayer player, string playerinfo)
		{
			if (playerinfo == null) return;
			var user = _recordCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(playerinfo));
			if (playerinfo == player.UserIDString)
			{
				user = _recordCache.ToList().FirstOrDefault(x => x.Value.id.ToString().Contains(playerinfo));
			}
			else
			{
				user = _recordCache.ToList().FirstOrDefault(x => x.Value.displayname.ToString().ToLower().Contains(playerinfo));
			}
			if (user.Value == null)
			{
				player.ChatMessage(KRLang("noplayer", player.UserIDString, playerinfo));
				return;
			}
			var KRUIelements = new CuiElementContainer();
			var height = 0.043f;
			// Main UI
			KRUIelements.Add(new CuiPanel 
			{ 
				Image = 
				{ 
					Color = "0.0 0.0 0.0 0.95" 
				}, 
				RectTransform = 
				{ 
					AnchorMin = $"0.83 0.27", 
					AnchorMax = $"0.997 0.95"
				}, 
				CursorEnabled = true 
			}, "Overlay", KRUIName);
			// Close Button
			KRUIelements.Add(new CuiButton 
			{ 
				Button = 
				{ 
					Close = KRUIName, 
					Color = "0.0 0.0 0.0 0.0" 
				}, 
				RectTransform = 
				{ 
					AnchorMin = "0.87 0.94", 
					AnchorMax = "1.0 1.002" 
				}, 
				Text = 
				{
					Text = "ⓧ",
					FontSize = 20,
					Color = "1.0 0.0 0.0",
					Align = TextAnchor.MiddleCenter 
				} 
			}, KRUIName);
			// Main UI Label
			KRUIelements.Add(KRUILabel("❀ Harvest Records:", 1, height, TextAnchor.MiddleCenter, 19, "0.03", "0.85", "0 1 0 1"), KRUIName);
			// Top Kills Button
			var KillType = GatherTypesEnabled();
			KRUIelements.Add(new CuiButton
			{
				Button =
				{
					Command = $"kr.topstats {KillType} {player.UserIDString}",
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.00 0.91",
					AnchorMax = "0.07 0.99"
				},
				Text =
				{
					Text = "⋘",
					FontSize = 23,
					Color = "0.0 0.0 1.0",
					Align = TextAnchor.MiddleCenter
				}
			}, KRUIName);	
			// Main UI
			KRUIelements.Add(KRUIPanel("0.04 0.02", "0.98 0.94"), KRUIName, KRUIMainName);
			KRUIelements.Add(KRUILabel($"〖 {user.Value.displayname} 〗", 1, height, TextAnchor.MiddleCenter, 17), KRUIMainName);
			//i++;
			KRUIelements.Add(KRUILabel(("────────────────────────"), 2, height, TextAnchor.MiddleCenter), KRUIMainName);
			int i = 3;			
			if (config.harvestoptions.treescut)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("treecut", user.Value.id, user.Value.treescut)}"), i, height), KRUIMainName);
				i++;
			}		
			if (config.harvestoptions.oremined)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("oremined", user.Value.id, user.Value.oremined)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.woodpickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("woodpickup", user.Value.id, user.Value.woodpickup)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.orepickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("orepickup", user.Value.id, user.Value.orepickup)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.berriespickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("berries", user.Value.id, user.Value.berriespickup)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.mushroompickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("mushroom", user.Value.id, user.Value.mushroompickup)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.potatopickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("potato", user.Value.id, user.Value.potatopickup)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.pumpkinpickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("pumpkin", user.Value.id, user.Value.pumpkinpickup)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.cornpickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("corn", user.Value.id, user.Value.cornpickup)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.hemppickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("hemp", user.Value.id, user.Value.hemppickup)}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.seedpickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("seeds", user.Value.id, user.Value.seedpickup)}"), i, height), KRUIMainName);
				i++;
			}
			CuiHelper.AddUi(player, KRUIelements);
			return;
		}
		private void KRUITop(BasePlayer player, string KillType)
		{
			if (player == null) return;
			if (KillType == null)
			{
				KillType = KillTypesEnabled();
			}
			var KRUIelements = new CuiElementContainer();
			var height = 0.075f;
			var selectionheight = 0.043f;
			var vals = GetTopKills(0, 10, KillType);
			if (vals == null) { return; }
			var index = 0;
			// Main UI
			KRUIelements.Add(new CuiPanel{
				Image = 
				{
					Color = "0.0 0.0 0.0 0.9"
				},
				RectTransform = 
				{
					AnchorMin = "0.75 0.25",
					AnchorMax = "0.995 0.90"
				},
				CursorEnabled = true
			},"Overlay", KRUIName);
			// Close Button
			KRUIelements.Add(new CuiButton
			{
				Button =
				{
					Close = KRUIName,
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.87 0.93",
					AnchorMax = "1.0 1.002"
				},
				Text =
				{
					Text = "ⓧ",
					FontSize = 20,
					Color = "1.0 0.0 0.0",
					Align = TextAnchor.MiddleCenter
				}
			}, KRUIName);
			// Main UI Label
			KRUIelements.Add(KRUILabel("☠ Top 10 Kill Records", 1, height, TextAnchor.MiddleCenter, 19, "0.03", "0.85", "1.0 0.0 0.0 1.0"), KRUIName);
			// Leaderboard Button
			KRUIelements.Add(new CuiButton
			{
				Button =
				{
					Command = $"kr.leaderboard {player.UserIDString}",
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.00 0.91",
					AnchorMax = "0.07 0.99"
				},
				Text =
				{
					Text = "⋘",
					FontSize = 23,
					Color = "0.0 0.0 1.0",
					Align = TextAnchor.MiddleCenter
				}
			}, KRUIName);
			// Selections UI
			KRUIelements.Add(KRUIPanel("0.01 0.00", "0.35 0.9"), KRUIName, KRUISelection);
			var selected = "➤";
			var dcolor = "0.0 0.0 0.0 0.7";
			var scolor = "1.0 0.0 0.0 0.7";
			if (config.trackingoptions.Trackchicken)
			{				
				if (KillType == "chicken")
				{
					KRUIelements.Add(KRUIButton("kr.topkills chicken", config.orderoptions.chickenpos, selectionheight, 11, scolor, $"{selected} {KRLang("chickenui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills chicken", config.orderoptions.chickenpos, selectionheight, 11, dcolor, $" {KRLang("chickenui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackboar)
			{
				if (KillType == "boar")
				{
					KRUIelements.Add(KRUIButton("kr.topkills boar", config.orderoptions.boarpos, selectionheight, 11, scolor, $"{selected} {KRLang("boarui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills boar", config.orderoptions.boarpos, selectionheight, 11, dcolor, $" {KRLang("boarui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackstag)
			{				
				if (KillType == "stag")
				{
					KRUIelements.Add(KRUIButton("kr.topkills stag", config.orderoptions.stagpos, selectionheight, 11, scolor, $"{selected} {KRLang("stagui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills stag", config.orderoptions.stagpos, selectionheight, 11, dcolor, $" {KRLang("stagui", player.UserIDString)}"), KRUISelection);
				}
			}			
			if (config.trackingoptions.Trackwolf)
			{
				if (KillType == "wolf")
				{
					KRUIelements.Add(KRUIButton("kr.topkills wolf", config.orderoptions.wolfpos, selectionheight, 11, scolor, $"{selected} {KRLang("wolfui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills wolf", config.orderoptions.wolfpos, selectionheight, 11, dcolor, $" {KRLang("wolfui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackbear)
			{
				if (KillType == "bear")
				{
					KRUIelements.Add(KRUIButton("kr.topkills bear", config.orderoptions.bearpos, selectionheight, 11, scolor, $"{selected} {KRLang("bearui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills bear", config.orderoptions.bearpos, selectionheight, 11, dcolor, $" {KRLang("bearui", player.UserIDString)}"), KRUISelection);
				}
			}			
			if (config.trackingoptions.Trackpolarbear)
			{
				if (KillType == "polarbear")
				{
					KRUIelements.Add(KRUIButton("kr.topkills polarbear", config.orderoptions.polarbearpos, selectionheight, 11, scolor, $"{selected} {KRLang("polarbearui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills polarbear", config.orderoptions.polarbearpos, selectionheight, 11, dcolor, $" {KRLang("polarbearui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackshark)
			{
				if (KillType == "shark")
				{
					KRUIelements.Add(KRUIButton("kr.topkills shark", config.orderoptions.sharkpos, selectionheight, 11, scolor, $"{selected} {KRLang("sharkui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills shark", config.orderoptions.sharkpos, selectionheight, 11, dcolor, $" {KRLang("sharkui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackhorse)
			{
				if (KillType == "horse")
				{
					KRUIelements.Add(KRUIButton("kr.topkills horse", config.orderoptions.horsepos, selectionheight, 11, scolor, $"{selected} {KRLang("horseui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills horse", config.orderoptions.horsepos, selectionheight, 11, dcolor, $" {KRLang("horseui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackfish)
			{
				if (KillType == "fish")
				{
					KRUIelements.Add(KRUIButton("kr.topkills fish", config.orderoptions.fishpos, selectionheight, 11, scolor, $"{selected} {KRLang("fishui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills fish", config.orderoptions.fishpos, selectionheight, 11, dcolor, $" {KRLang("fishui", player.UserIDString)}"), KRUISelection);
				}
			}			
			if (config.trackingoptions.Trackscientist)
			{
				if (KillType == "scientist")
				{
					KRUIelements.Add(KRUIButton("kr.topkills scientist", config.orderoptions.scientistpos, selectionheight, 11, scolor, $"{selected} {KRLang("scientistui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills scientist", config.orderoptions.scientistpos, selectionheight, 11, dcolor, $" {KRLang("scientistui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackscarecrow)
			{
				if (KillType == "scarecrow")
				{
					KRUIelements.Add(KRUIButton("kr.topkills scarecrow", config.orderoptions.scarecrowpos, selectionheight, 11, scolor, $"{selected} {KRLang("scarecrowui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills scarecrow", config.orderoptions.scarecrowpos, selectionheight, 11, dcolor, $" {KRLang("scarecrowui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackdweller)
			{
				if (KillType == "dweller")
				{
					KRUIelements.Add(KRUIButton("kr.topkills dweller", config.orderoptions.dwellerpos, selectionheight, 11, scolor, $"{selected} {KRLang("dwellerui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills dweller", config.orderoptions.dwellerpos, selectionheight, 11, dcolor, $" {KRLang("dwellerui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.TrackPlayer)
			{
				if (KillType == "player")
				{
					KRUIelements.Add(KRUIButton("kr.topkills player", config.orderoptions.playerpos, selectionheight, 11, scolor, $"{selected} {KRLang("playerui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills player", config.orderoptions.playerpos, selectionheight, 11, dcolor, $" {KRLang("playerui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Tracklootcontainer)
			{
				if (KillType == "loot")
				{
					KRUIelements.Add(KRUIButton("kr.topkills loot", config.orderoptions.lootpos, selectionheight, 11, scolor, $"{selected} {KRLang("lootui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills loot", config.orderoptions.lootpos, selectionheight, 11, dcolor, $" {KRLang("lootui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackunderwaterlootcontainer)
			{
				if (KillType == "underwaterloot")
				{
					KRUIelements.Add(KRUIButton("kr.topkills underwaterloot", config.orderoptions.unlootpos, selectionheight, 11, scolor, $"{selected} {KRLang("wlootui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills underwaterloot", config.orderoptions.unlootpos, selectionheight, 11, dcolor, $" {KRLang("wlootui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackbradhelicrate)
			{
				if (KillType == "bradhelicrate")
				{
					KRUIelements.Add(KRUIButton("kr.topkills bradhelicrate", config.orderoptions.bradhelicratepos, selectionheight, 11, scolor, $"{selected} {KRLang("bradheliui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills bradhelicrate", config.orderoptions.bradhelicratepos, selectionheight, 11, dcolor, $" {KRLang("bradheliui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackhackablecrate)
			{
				if (KillType == "hackablecrate")
				{
					KRUIelements.Add(KRUIButton("kr.topkills hackablecrate", config.orderoptions.hackablecratepos, selectionheight, 11, scolor, $"{selected} {KRLang("hackableui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills hackablecrate", config.orderoptions.hackablecratepos, selectionheight, 11, dcolor, $" {KRLang("hackableui", player.UserIDString)}"), KRUISelection);
				}
			}			
			if (config.trackingoptions.TrackBradley)
			{
				if (KillType == "bradleyapc")
				{
					KRUIelements.Add(KRUIButton("kr.topkills bradleyapc", config.orderoptions.bradleypos, selectionheight, 11, scolor, $"{selected} {KRLang("bradleyui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills bradleyapc", config.orderoptions.bradleypos, selectionheight, 11, dcolor, $" {KRLang("bradleyui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.TrackHeli)
			{
				if (KillType == "patrolhelicopter")
				{
					KRUIelements.Add(KRUIButton("kr.topkills patrolhelicopter", config.orderoptions.helipos, selectionheight, 11, scolor, $"{selected} {KRLang("patrolhelicopterui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills patrolhelicopter", config.orderoptions.helipos, selectionheight, 11, dcolor, $" {KRLang("patrolhelicopterui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackturret)
			{
				if (KillType == "turret")
				{
					KRUIelements.Add(KRUIButton("kr.topkills turret", config.orderoptions.turretpos, selectionheight, 11, scolor, $"{selected} {KRLang("turretui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills turret", config.orderoptions.turretpos, selectionheight, 11, dcolor, $" {KRLang("turretui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Trackdeaths)
			{
				if (KillType == "death")
				{
					KRUIelements.Add(KRUIButton("kr.topkills death", config.orderoptions.deathpos, selectionheight, 11, scolor, $"{selected} {KRLang("deathui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills death", config.orderoptions.deathpos, selectionheight, 11, dcolor, $" {KRLang("deathui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.Tracksuicides)
			{
				if (KillType == "suicide")
				{
					KRUIelements.Add(KRUIButton("kr.topkills suicide", config.orderoptions.suicidepos, selectionheight, 11, scolor, $"{selected} {KRLang("suicideui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills suicide", config.orderoptions.suicidepos, selectionheight, 11, dcolor, $" {KRLang("suicideui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.TrackAnimalHarvest)
			{
				if (KillType == "corpse")
				{
					KRUIelements.Add(KRUIButton("kr.topkills corpse", config.orderoptions.corpsepos, selectionheight, 11, scolor, $"{selected} {KRLang("corpseui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills corpse", config.orderoptions.corpsepos, selectionheight, 11, dcolor, $" {KRLang("corpseui", player.UserIDString)}"), KRUISelection);
				}
			}
			if (config.trackingoptions.TrackCorpseHarvest)
			{
				if (KillType == "pcorpse")
				{
					KRUIelements.Add(KRUIButton("kr.topkills pcorpse", config.orderoptions.pcorpsepos, selectionheight, 11, scolor, $"{selected} {KRLang("pcorpseui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topkills pcorpse", config.orderoptions.pcorpsepos, selectionheight, 11, dcolor, $" {KRLang("pcorpseui", player.UserIDString)}"), KRUISelection);
				}
			}
			// List UI
			KRUIelements.Add(KRUIPanel("0.40 0.09", "0.98 0.9"), KRUIName, KRUIMainName);
			// Inner UI Labels
			KRUIelements.Add(KRUILabel($"〖 {UILabels(player, KillType)} 〗", 1, height, TextAnchor.MiddleCenter, 16), KRUIMainName);
			KRUIelements.Add(KRUILabel(("﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌"), 2, height, TextAnchor.MiddleCenter), KRUIMainName);
			int n = 0;
			for (int i = 3; i < 13; i++)
			{
				n++;
				if (vals.ElementAtOrDefault(index) == null)
				{ 
					continue; 
				}
				var playerdata = vals.ElementAtOrDefault(index);
				if (playerdata == null) continue;
				if (KillType == "chicken" && playerdata?.chicken != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.06", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.chicken}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "boar" && playerdata.boar != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.boar}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "stag" && playerdata.stag != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.stag}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "wolf" && playerdata.wolf != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.wolf}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "bear" && playerdata.bear != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.bear}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "polarbear" && playerdata.polarbear != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.polarbear}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "shark" && playerdata.shark != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.shark}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "horse" && playerdata.horse != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.horse}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "fish" && playerdata.fish != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.fish}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "scientist" && playerdata.scientistnpcnew != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.scientistnpcnew}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "scarecrow" && playerdata.scarecrow != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.scarecrow}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "dweller" && playerdata.dweller != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.dweller}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "player" && playerdata.baseplayer != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.baseplayer}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "loot" && playerdata.lootcontainer != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.lootcontainer}", "0.03", "1"), KRUIMainName);
				}
				else if ((KillType == "underwaterloot" || KillType == "underwaterlootcontainer") && playerdata.underwaterlootcontainer != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.underwaterlootcontainer}", "0.03", "1"), KRUIMainName);
				}
				else if ((KillType == "bradhelicrate") && playerdata.bradhelicrate != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.bradhelicrate}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "hackablecrate" && playerdata.hackablecrate != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.hackablecrate}", "0.03", "1"), KRUIMainName);
				}
				else if ((KillType == "bradleyapc") && playerdata.bradley != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.bradley}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "patrolhelicopter" && playerdata.heli != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.heli}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "turret" && playerdata.turret != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.turret}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "death" && playerdata.deaths != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.deaths}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "suicide" && playerdata.suicides != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.suicides}", "0.03", "1"), KRUIMainName);
				}
				else if ((KillType == "corpse" || KillType == "animals" || KillType == "animalsharvested") && playerdata.basecorpse != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.basecorpse}", "0.03", "1"), KRUIMainName);
				}
				else if ((KillType == "pcorpse" || KillType == "bodies" || KillType == "bodiesharvested") && playerdata.npcplayercorpse != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.npcplayercorpse}", "0.03", "1"), KRUIMainName);
				} 
				index++;
			}
				CuiHelper.AddUi(player, KRUIelements);
		}
		private void KRUIStatsTop(BasePlayer player, string KillType)
		{
			if (player == null) return;
			if (KillType == null)
			{
				KillType = GatherTypesEnabled();
			}
			var KRUIelements = new CuiElementContainer();
			var height = 0.075f;
			var selectionheight = 0.045f;
			int r = 1;
			var vals = GetTopKills(0, 10, KillType);
			if (vals == null) { return; }
			var index = 0;
			// Main UI
			KRUIelements.Add(new CuiPanel{
				Image = 
				{
					Color = "0.0 0.0 0.0 0.9"
				},
				RectTransform = 
				{
					AnchorMin = "0.75 0.25",
					AnchorMax = "0.995 0.90"
				},
				CursorEnabled = true
			},"Overlay", KRUIName);
			// Close Button
			KRUIelements.Add(new CuiButton
			{
				Button =
				{
					Close = KRUIName,
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.87 0.93",
					AnchorMax = "1.0 1.002"
				},
				Text =
				{
					Text = "ⓧ",
					FontSize = 20,
					Color = "1.0 0.0 0.0",
					Align = TextAnchor.MiddleCenter
				}
			}, KRUIName);
			// Main UI Label
			KRUIelements.Add(KRUILabel("❀ Top 10 Harvest Records", 1, height, TextAnchor.MiddleCenter, 19, "0.03", "0.85", "0 1 0 1"), KRUIName);
			// Selections UI
			KRUIelements.Add(KRUIPanel("0.01 0.00", "0.35 0.9"), KRUIName, KRUISelection);
			var selected = "➤";
			var dcolor = "0.0 0.0 0.0 0.7";
			var scolor = "1.0 0.0 0.0 0.7";
			if (config.harvestoptions.treescut)
			{				
				if (KillType == "trees")
				{
					KRUIelements.Add(KRUIButton("kr.topstats trees", r, selectionheight, 11, scolor, $"{selected} {KRLang("treeui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats trees", r, selectionheight, 11, dcolor, $" {KRLang("treeui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.oremined)
			{				
				if (KillType == "oremined")
				{
					KRUIelements.Add(KRUIButton("kr.topstats oremined", r, selectionheight, 11, scolor, $"{selected} {KRLang("oreminedui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats oremined", r, selectionheight, 11, dcolor, $" {KRLang("oreminedui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.cactuscut)
			{				
				if (KillType == "cactus")
				{
					KRUIelements.Add(KRUIButton("kr.topstats cactus", r, selectionheight, 11, scolor, $"{selected} {KRLang("cactusui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats cactus", r, selectionheight, 11, dcolor, $" {KRLang("cactusui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.woodpickup)
			{				
				if (KillType == "wood")
				{
					KRUIelements.Add(KRUIButton("kr.topstats wood", r, selectionheight, 11, scolor, $"{selected} {KRLang("woodui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats wood", r, selectionheight, 11, dcolor, $" {KRLang("woodui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.orepickup)
			{				
				if (KillType == "ore")
				{
					KRUIelements.Add(KRUIButton("kr.topstats ore", r, selectionheight, 11, scolor, $"{selected} {KRLang("oreui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats ore", r, selectionheight, 11, dcolor, $" {KRLang("oreui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.berriespickup)
			{				
				if (KillType == "berries")
				{
					KRUIelements.Add(KRUIButton("kr.topstats berries", r, selectionheight, 11, scolor, $"{selected} {KRLang("berriesui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats berries", r, selectionheight, 11, dcolor, $" {KRLang("berriesui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.seedpickup)
			{				
				if (KillType == "seeds")
				{
					KRUIelements.Add(KRUIButton("kr.topstats seeds", r, selectionheight, 11, scolor, $"{selected} {KRLang("seedsui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats seeds", r, selectionheight, 11, dcolor, $" {KRLang("seedsui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.mushroompickup)
			{				
				if (KillType == "mushroom")
				{
					KRUIelements.Add(KRUIButton("kr.topstats mushroom", r, selectionheight, 11, scolor, $"{selected} {KRLang("mushroomui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats mushroom", r, selectionheight, 11, dcolor, $" {KRLang("mushroomui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.potatopickup)
			{				
				if (KillType == "potato")
				{
					KRUIelements.Add(KRUIButton("kr.topstats potato", r, selectionheight, 11, scolor, $"{selected} {KRLang("potatoui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats potato", r, selectionheight, 11, dcolor, $" {KRLang("potatoui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.pumpkinpickup)
			{				
				if (KillType == "pumpkin")
				{
					KRUIelements.Add(KRUIButton("kr.topstats pumpkin", r, selectionheight, 11, scolor, $"{selected} {KRLang("pumpkinui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats pumpkin", r, selectionheight, 11, dcolor, $" {KRLang("pumpkinui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.cornpickup)
			{				
				if (KillType == "corn")
				{
					KRUIelements.Add(KRUIButton("kr.topstats corn", r, selectionheight, 11, scolor, $"{selected} {KRLang("cornui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats corn", r, selectionheight, 11, dcolor, $" {KRLang("cornui", player.UserIDString)}"), KRUISelection);
				}
				r++;
			}
			if (config.harvestoptions.hemppickup)
			{				
				if (KillType == "hemp")
				{
					KRUIelements.Add(KRUIButton("kr.topstats hemp", r, selectionheight, 11, scolor, $"{selected} {KRLang("hempui", player.UserIDString)}"), KRUISelection);
				}
				else
				{
					KRUIelements.Add(KRUIButton("kr.topstats hemp", r, selectionheight, 11, dcolor, $" {KRLang("hempui", player.UserIDString)}"), KRUISelection);
				}
			}
			// List UI
			KRUIelements.Add(KRUIPanel("0.40 0.09", "0.98 0.9"), KRUIName, KRUIMainName);
			// Inner UI Labels
			KRUIelements.Add(KRUILabel($"〖 {UILabels(player, KillType)} 〗", 1, height, TextAnchor.MiddleCenter, 16), KRUIMainName);
			KRUIelements.Add(KRUILabel(("﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌"), 2, height, TextAnchor.MiddleCenter), KRUIMainName);
			int n = 0;
			for (int i = 3; i < 13; i++)
			{
				n++;
				if (vals.ElementAtOrDefault(index) == null)
				{ 
					continue; 
				}
				var playerdata = vals.ElementAtOrDefault(index);
				if (playerdata == null) continue;

				if (KillType == "trees" && playerdata?.treescut != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.06", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.treescut}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "oremined" && playerdata.oremined != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.oremined}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "catus" && playerdata.cactuscut != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.cactuscut}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "wood" && playerdata.woodpickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.woodpickup}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "ore" && playerdata.orepickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.orepickup}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "berries" && playerdata.berriespickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.berriespickup}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "seeds" && playerdata.seedpickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.seedpickup}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "mushroom" && playerdata.mushroompickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.mushroompickup}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "potato" && playerdata.potatopickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.potatopickup}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "pumpkin" && playerdata.pumpkinpickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.pumpkinpickup}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "corn" && playerdata.cornpickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.cornpickup}", "0.03", "1"), KRUIMainName);
				}
				else if (KillType == "hemp" && playerdata.hemppickup != 0)
				{
					if (playerdata.displayname == _recordCache[player.UserIDString].displayname)
					{
						KRUIelements.Add(KRUILabel(("➤"), i, height, TextAnchor.MiddleLeft, 15, "-0.050", "1", "1 0.92 0.016 1"), KRUIMainName);
					}
					KRUIelements.Add(KRUIButton($"kr.topplayersstats {playerdata.displayname}", i, height, 15, "0.0 0.0 0.0 0.0", $"{n}. {playerdata.displayname}: {playerdata.hemppickup}", "0.03", "1"), KRUIMainName);
				}
				index++;
			}
				CuiHelper.AddUi(player, KRUIelements);
		}
		private void KRUITopAll(BasePlayer player)
        {
			if (player == null) return;
			var KRUIelements = new CuiElementContainer();
			var height = 0.053f;
			// Main UI
			KRUIelements.Add(new CuiPanel
			{
				Image =
				{
					Color = "0.0 0.0 0.0 0.95"
				},
				RectTransform =
				{
					AnchorMin = $"0.10 0.20",
					AnchorMax = $"0.90 0.90"
				},
				CursorEnabled = true
			}, "Overlay", KRUIName);
			// Close Button
			KRUIelements.Add(new CuiButton
			{
				Button =
				{
					Close = KRUIName,
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.95 0.94",
					AnchorMax = "1.0 1.0"
				},
				Text =
				{
					Text = "ⓧ",
					FontSize = 20,
					Color = "1.0 0.0 0.0",
					Align = TextAnchor.MiddleCenter
				}
			}, KRUIName);
			KRUIelements.Add(KRUILabel("☠ Kill Records Top Players:", 1, height, TextAnchor.MiddleCenter, 15, "0.00", "0.90", "1.0 0.0 0.0 1.0"), KRUIName);
			// TotalKills Button
            KRUIelements.Add(new CuiButton
			{
				Button =
				{
					Command = $"kr.totalkills {player.UserIDString}",
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.009 0.95",
					AnchorMax = "0.05 0.99"
				},
				Text =
				{
					Text = "◄＃",
					FontSize = 13,
					Color = "1.0 0.0 0.0",
					Align = TextAnchor.MiddleLeft
				}
			}, KRUIName);
			var t = "";
			var f = 0;
			var c = "0.0 0.0 0.0 0.0";
			if (!config.chatui.UseImageLibrary)
			{
				t = "웃";
				f = 25;
				c = "0.0 0.0 1.0 0.5";
			}
			int position;
			if (config.trackingoptions.Trackchicken)
			{
				position = config.orderoptions.chickenpos;
				string KillType = "chicken";		
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills chicken", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("chickenui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.chicken != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.chicken})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills chicken",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills chicken", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}		
			if (config.trackingoptions.Trackboar)
			{
				position = config.orderoptions.boarpos;
				string KillType = "boar";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills boar", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("boarui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.boar != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
						{
							rawImage,
							new CuiRectTransformComponent
							{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
							}
						}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
					{
						Command = $"kr.topplayers {playerdata.displayname}",
						Color = $"{c}"
					},
						RectTransform =
					{
							AnchorMin = "0.30 0.40",
							AnchorMax = "0.70 0.75"
					},
						Text =
					{
						Text = $"{t}",
						FontSize = f,
						Color = "0.0 0.0 0.0",
						Align = TextAnchor.MiddleCenter
					}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.boar})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills boar",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills boar", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackstag)
			{
				position = config.orderoptions.stagpos;
				string KillType = "stag";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills stag", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("stagui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.stag != 0)
				{
					if (config.chatui.UseImageLibrary)
				{
					CuiRawImageComponent rawImage = new CuiRawImageComponent();
					if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
					}
					else
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
					}
					KRUIelements.Add(new CuiElement
					{
						Parent = KRUIMainName,
						Components =
						{
							rawImage,
							new CuiRectTransformComponent
							{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
							}
						}
					});
				}

					KRUIelements.Add(new CuiButton
				{
					Button =
					{
						Command = $"kr.topplayers {playerdata.displayname}",
						Color = $"{c}"
					},
					RectTransform =
					{
							AnchorMin = "0.30 0.40",
							AnchorMax = "0.70 0.75"
					},
					Text =
					{
						Text = $"{t}",
						FontSize = f,
						Color = "0.0 0.0 0.0",
						Align = TextAnchor.MiddleCenter
					}
				}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.stag})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills stag",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills stag", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackwolf)
			{
				position = config.orderoptions.wolfpos;
				string KillType = "wolf";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills wolf", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("wolfui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.wolf != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.wolf})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills wolf",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills wolf", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackbear)
			{
				position = config.orderoptions.bearpos;
				string KillType = "bear";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills bear", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("bearui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.bear != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.bear})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills bear",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills bear", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}	
			if (config.trackingoptions.Trackpolarbear)
			{
				position = config.orderoptions.polarbearpos;
				string KillType = "polarbear";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills polarbear", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("polarbearui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.polarbear != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.polarbear})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills polarbear",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills polarbear", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}			
			if (config.trackingoptions.Trackshark)
			{
				position = config.orderoptions.sharkpos;
				string KillType = "shark";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills shark", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("sharkui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.shark != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.shark})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills shark",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills shark", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}				
			if (config.trackingoptions.Trackhorse)
			{
				position = config.orderoptions.horsepos;
				string KillType = "horse";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills horse", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("horseui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.horse != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.horse})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills horse",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills horse", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}			
			if (config.trackingoptions.Trackfish)
			{
				position = config.orderoptions.fishpos;
				string KillType = "fish";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills fish", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("fishui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.fish != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.fish})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills fish",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills fish", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.TrackPlayer)
			{
				position = config.orderoptions.playerpos;
				string KillType = "player";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills player", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("playerui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.baseplayer != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.baseplayer})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills player",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills player", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackscientist)
			{
				position = config.orderoptions.scientistpos;
				string KillType = "scientist";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills scientist", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("scientistui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.scientistnpcnew != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.scientistnpcnew})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills scientist",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills scientist", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackscarecrow)
			{
				position = config.orderoptions.scarecrowpos;
				string KillType = "scarecrow";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills scarecrow", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("scarecrowui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.scarecrow != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);
					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.scarecrow})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills scarecrow",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills scarecrow", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackdweller)
			{
				position = config.orderoptions.dwellerpos;
				string KillType = "dweller";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills dweller", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("dwellerui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.dweller != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.dweller})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills dweller",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills dweller", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Tracklootcontainer)
			{
				position = config.orderoptions.lootpos;
				string KillType = "loot";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills loot", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("lootui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.lootcontainer != 0)
				{
					if (config.chatui.UseImageLibrary)
				{
					CuiRawImageComponent rawImage = new CuiRawImageComponent();
					if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
					}
					else
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
					}
					KRUIelements.Add(new CuiElement
					{
						Parent = KRUIMainName,
						Components =
						{
							rawImage,
							new CuiRectTransformComponent
							{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
							}
						}
					});
				}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.lootcontainer})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
							{
								Command = "kr.topkills loot",
								Color = "0.0 0.0 0.0 0.0"
							},
						RectTransform =
							{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
							},
						Text =
							{
								Text = "☠",
								FontSize = 30,
								Color = "1.0 0.0 0.0",
								Align = TextAnchor.MiddleCenter
							}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills loot", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackunderwaterlootcontainer)
			{
				position = config.orderoptions.unlootpos;
				string KillType = "underwaterloot";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills underwaterloot", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("wlootui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.underwaterlootcontainer != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.underwaterlootcontainer})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills underwaterloot",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills underwaterloot", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackbradhelicrate)
			{
				position = config.orderoptions.bradhelicratepos;
				string KillType = "bradhelicrate";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills bradhelicrate", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("bradheliui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.bradhelicrate != 0)
				{
					if (config.chatui.UseImageLibrary)
				{
					CuiRawImageComponent rawImage = new CuiRawImageComponent();
					if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
					}
					else
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
					}
					KRUIelements.Add(new CuiElement
					{
						Parent = KRUIMainName,
						Components =
						{
							rawImage,
							new CuiRectTransformComponent
							{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
							}
						}
					});
				}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.bradhelicrate})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills bradhelicrate",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills bradhelicrate", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackhackablecrate)
			{
				position = config.orderoptions.hackablecratepos;
				string KillType = "hackablecrate";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills hackablecrate", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("hackableui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.hackablecrate != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.hackablecrate})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills hackablecrate",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills hackablecrate", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.TrackBradley)
			{
				position = config.orderoptions.bradleypos;
				string KillType = "bradleyapc";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills bradleyapc", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("bradleyui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.bradley != 0)
				{
					if (config.chatui.UseImageLibrary)
				{
					CuiRawImageComponent rawImage = new CuiRawImageComponent();
					if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
					}
					else
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
					}
					KRUIelements.Add(new CuiElement
					{
						Parent = KRUIMainName,
						Components =
						{
							rawImage,
							new CuiRectTransformComponent
							{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
							}
						}
					});
				}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.bradley})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills bradleyapc",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills bradleyapc", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.TrackHeli)
			{
				position = config.orderoptions.helipos;
				string KillType = "patrolhelicopter";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills patrolhelicopter", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("patrolhelicopterui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.heli != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.heli})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills patrolhelicopter",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills patrolhelicopter", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackturret)
			{
				position = config.orderoptions.turretpos;
				string KillType = "turret";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills turret", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("turretui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.turret != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.turret})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills turret",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills turret", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Trackdeaths)
			{
				position = config.orderoptions.deathpos;
				string KillType = "death";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills death", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("deathui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.deaths != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.deaths})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills death",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills death", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.Tracksuicides)
			{
				position = config.orderoptions.suicidepos;
				string KillType = "suicide";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills suicide", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("suicideui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.suicides != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.suicides})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills suicide",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills suicide", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.TrackAnimalHarvest)
			{
				position = config.orderoptions.corpsepos;
				string KillType = "corpse";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills corpse", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("corpseui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.basecorpse != 0)
				{
					if (config.chatui.UseImageLibrary)
					{
						CuiRawImageComponent rawImage = new CuiRawImageComponent();
						if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
						}
						else
						{
							rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
						}
						KRUIelements.Add(new CuiElement
						{
							Parent = KRUIMainName,
							Components =
							{
								rawImage,
								new CuiRectTransformComponent
								{
									AnchorMin = "0.30 0.40",
									AnchorMax = "0.70 0.75"
								}
							}
						});
					}

					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = $"kr.topplayers {playerdata.displayname}",
							Color = $"{c}"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = $"{t}",
							FontSize = f,
							Color = "0.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.basecorpse})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills corpse",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills corpse", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
				//position++;
			}
			if (config.trackingoptions.TrackCorpseHarvest)
			{
				position = config.orderoptions.pcorpsepos;
				string KillType = "pcorpse";
				var vals = GetTopKills(0, 1, KillType);
				var playerdata = vals.ElementAtOrDefault(0);
				KRUIelements.Add(KRUIPanel($"{LeaderboardPositionLB(position)}", $"{LeaderboardPositionRT(position)}", "0.1 0.1 0.1 0.95"), KRUIName, KRUIMainName);
				KRUIelements.Add(KRUIButton("kr.topkills pcorpse", 1, 0.22f, 13, "0.0 0.0 0.0 0.5", $"〖{KRLang("pcorpseui", player.UserIDString)}〗", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				if (playerdata?.npcplayercorpse != 0)
				{
					if (config.chatui.UseImageLibrary)
				{
					CuiRawImageComponent rawImage = new CuiRawImageComponent();
					if ((bool)(ImageLibrary?.Call("HasImage", playerdata.id) ?? false))
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", playerdata.id);
					}
					else
					{
						rawImage.Png = (string)ImageLibrary?.Call("GetImage", null);
					}
					KRUIelements.Add(new CuiElement
					{
						Parent = KRUIMainName,
						Components =
						{
							rawImage,
							new CuiRectTransformComponent
							{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
							}
						}
					});
				}

					KRUIelements.Add(new CuiButton
				{
					Button =
					{
						Command = $"kr.topplayers {playerdata.displayname}",
						Color = $"{c}"
					},
					RectTransform =
					{
							AnchorMin = "0.30 0.40",
							AnchorMax = "0.70 0.75"
					},
					Text =
					{
						Text = $"{t}",
						FontSize = f,
						Color = "0.0 0.0 0.0",
						Align = TextAnchor.MiddleCenter
					}
				}, KRUIMainName);

					KRUIelements.Add(KRUIButton($"kr.topplayers {playerdata.displayname}", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", $"{playerdata.displayname}", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
					KRUIelements.Add(KRUILabel($"({playerdata.npcplayercorpse})", 6, 0.17f, TextAnchor.MiddleCenter, 10, "0.00", "0.99", "1.0 1.0 1.0 1.0"), KRUIMainName);
				}
				else
				{
					KRUIelements.Add(new CuiButton
					{
						Button =
						{
							Command = "kr.topkills pcorpse",
							Color = "0.0 0.0 0.0 0.0"
						},
						RectTransform =
						{
								AnchorMin = "0.30 0.40",
								AnchorMax = "0.70 0.75"
						},
						Text =
						{
							Text = "☠",
							FontSize = 30,
							Color = "1.0 0.0 0.0",
							Align = TextAnchor.MiddleCenter
						}
					}, KRUIMainName);

					KRUIelements.Add(KRUIButton("kr.topkills pcorpse", 5, 0.17f, 11, "0.0 0.0 0.0 0.5", "(No Top Player)", "0", "1", TextAnchor.MiddleCenter), KRUIMainName);
				}
			}
			CuiHelper.AddUi(player, KRUIelements);
			return;
		}
		private void KRUITotal(BasePlayer player)
		{
			var KRUIelements = new CuiElementContainer();
			var height = 0.041f;
			// Main UI
			KRUIelements.Add(new CuiPanel 
			{ 
				Image = 
				{ 
					Color = "0.0 0.0 0.0 0.9" 
				}, 
				RectTransform = 
				{ 
					AnchorMin = $"0.83 0.27", 
					AnchorMax = $"0.997 0.95"
				}, 
				CursorEnabled = true 
			}, "Overlay", KRUIName);
			// Close Button
			KRUIelements.Add(new CuiButton 
			{ 
				Button = 
				{ 
					Close = KRUIName, 
					Color = "0.0 0.0 0.0 0.0" 
				}, 
				RectTransform = 
				{ 
					AnchorMin = "0.87 0.94", 
					AnchorMax = "1.0 1.002" 
				}, 
				Text = 
				{
					Text = "ⓧ",
					FontSize = 20,
					Color = "1.0 0.0 0.0",
					Align = TextAnchor.MiddleCenter 
				} 
			}, KRUIName);
			// Main UI Label
			KRUIelements.Add(KRUILabel("☠ Kill Records:", 1, 0.043f, TextAnchor.MiddleCenter, 17, "0.03", "0.85", "1.0 0.0 0.0 1.0"), KRUIName);
			// Leaderboard Button
			KRUIelements.Add(new CuiButton
			{
				Button =
				{
					Command = $"kr.leaderboard {player.UserIDString}",
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.00 0.91",
					AnchorMax = "0.07 0.99"
				},
				Text =
				{
					Text = "⋘",
					FontSize = 23,
					Color = "0.0 0.0 1.0",
					Align = TextAnchor.MiddleCenter
				}
			}, KRUIName);
			// Inner UI
			KRUIelements.Add(KRUIPanel("0.04 0.02", "0.98 0.94"), KRUIName, KRUIMainName);
			KRUIelements.Add(KRUILabel($"{KRLang("totalkills", player.UserIDString)}", 1, height, TextAnchor.MiddleCenter, 17), KRUIMainName);
			KRUIelements.Add(KRUILabel(("﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌"), 2, height, TextAnchor.MiddleCenter), KRUIMainName);
			int i = 2;
			
			if (config.trackingoptions.Trackchicken)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("chicken", player.UserIDString, GetTotalKills("chicken"))}"), config.orderoptions.chickenpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackboar) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("boar", player.UserIDString, GetTotalKills("boar"))}"), config.orderoptions.boarpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackstag) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("stag", player.UserIDString, GetTotalKills("stag"))}"), config.orderoptions.stagpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackwolf) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("wolf", player.UserIDString, GetTotalKills("wolf"))}"), config.orderoptions.wolfpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackbear) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("bear", player.UserIDString, GetTotalKills("bear"))}"), config.orderoptions.bearpos + i, height), KRUIMainName);
			}

			if (config.trackingoptions.Trackpolarbear) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("polarbear", player.UserIDString, GetTotalKills("polarbear"))}"), config.orderoptions.polarbearpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackshark) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("simpleshark", player.UserIDString, GetTotalKills("shark"))}"), config.orderoptions.sharkpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackhorse) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("horse", player.UserIDString, GetTotalKills("horse"))}"), config.orderoptions.horsepos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackfish) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("fish", player.UserIDString, GetTotalKills("fish"))}"), config.orderoptions.fishpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackscientist) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("scientists", player.UserIDString, GetTotalKills("scientist"))}"), config.orderoptions.scientistpos + i, height), KRUIMainName);
			}

			if (config.trackingoptions.Trackscarecrow) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("scarecrow", player.UserIDString, GetTotalKills("scarecrow"))}"), config.orderoptions.scarecrowpos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Trackdweller) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("dweller", player.UserIDString, GetTotalKills("dweller"))}"), config.orderoptions.dwellerpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.TrackPlayer) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("players", player.UserIDString, GetTotalKills("player"))}"), config.orderoptions.playerpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackdeaths) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("deaths", player.UserIDString, GetTotalKills("deaths"))}"), config.orderoptions.deathpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Tracksuicides) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("suicide", player.UserIDString, GetTotalKills("suicides"))}"), config.orderoptions.suicidepos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.Tracklootcontainer) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("loot", player.UserIDString, GetTotalKills("lootcontainer"))}"), config.orderoptions.lootpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackunderwaterlootcontainer) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("unloot", player.UserIDString, GetTotalKills("underwaterloot"))}"), config.orderoptions.unlootpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackbradhelicrate) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("bradheliloot", player.UserIDString, GetTotalKills("bradhelicrate"))}"), config.orderoptions.bradhelicratepos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.Trackhackablecrate) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("hackloot", player.UserIDString, GetTotalKills("hackablecrate"))}"), config.orderoptions.hackablecratepos + i, height), KRUIMainName); 
			}
				
			if (config.trackingoptions.TrackBradley) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("bradley", player.UserIDString, GetTotalKills("bradleyapc"))}"), config.orderoptions.bradleypos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.TrackHeli) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("heli", player.UserIDString, GetTotalKills("patrolhelicopter"))}"), config.orderoptions.helipos + i, height), KRUIMainName); 
			}

			if (config.trackingoptions.Trackturret) 
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("turret", player.UserIDString, GetTotalKills("turret"))}"), config.orderoptions.turretpos + i, height), KRUIMainName); 
			}
			
			if (config.trackingoptions.TrackAnimalHarvest)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("corpse", player.UserIDString, GetTotalKills("corpse"))}"), config.orderoptions.corpsepos + i, height), KRUIMainName);
			}
			
			if (config.trackingoptions.TrackCorpseHarvest)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("pcorpse", player.UserIDString, GetTotalKills("pcorpse"))}"), config.orderoptions.pcorpsepos + i, height), KRUIMainName);
			}
			
			CuiHelper.AddUi(player, KRUIelements);
			return;
		}
		private void KRUIStatsTotal(BasePlayer player)
		{
			var KRUIelements = new CuiElementContainer();
			var height = 0.043f;
			// Main UI
			KRUIelements.Add(new CuiPanel 
			{ 
				Image = 
				{ 
					Color = "0.0 0.0 0.0 0.9" 
				}, 
				RectTransform = 
				{ 
					AnchorMin = $"0.83 0.27", 
					AnchorMax = $"0.997 0.95"
				}, 
				CursorEnabled = true 
			}, "Overlay", KRUIName);
			// Close Button
			KRUIelements.Add(new CuiButton 
			{ 
				Button = 
				{ 
					Close = KRUIName, 
					Color = "0.0 0.0 0.0 0.0" 
				}, 
				RectTransform = 
				{ 
					AnchorMin = "0.87 0.94", 
					AnchorMax = "1.0 1.002" 
				}, 
				Text = 
				{
					Text = "ⓧ",
					FontSize = 20,
					Color = "1.0 0.0 0.0",
					Align = TextAnchor.MiddleCenter 
				} 
			}, KRUIName);
			// Main UI Label
			KRUIelements.Add(KRUILabel("❀ Harvest Records:", 1, height, TextAnchor.MiddleCenter, 19, "0.03", "0.85", "0 1 0 1"), KRUIName);
			// Inner UI
			KRUIelements.Add(KRUIPanel("0.04 0.02", "0.98 0.94"), KRUIName, KRUIMainName);
			KRUIelements.Add(KRUILabel($"{KRLang("totalstats", player.UserIDString)}", 1, height, TextAnchor.MiddleCenter, 17), KRUIMainName);
			KRUIelements.Add(KRUILabel(("﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌﹌"), 2, height, TextAnchor.MiddleCenter), KRUIMainName);
			int i = 3;		
			if (config.harvestoptions.treescut)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("treecut", player.UserIDString, GetTotalKills("trees"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.oremined)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("oremined", player.UserIDString, GetTotalKills("oremined"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.cactuscut)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("cactuscut", player.UserIDString, GetTotalKills("cactus"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.woodpickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("woodpickup", player.UserIDString, GetTotalKills("wood"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.orepickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("orepickup", player.UserIDString, GetTotalKills("ore"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.berriespickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("berries", player.UserIDString, GetTotalKills("berries"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.seedpickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("seeds", player.UserIDString, GetTotalKills("seeds"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.mushroompickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("mushroom", player.UserIDString, GetTotalKills("mushroom"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.cornpickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("corn", player.UserIDString, GetTotalKills("corn"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.potatopickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("potato", player.UserIDString, GetTotalKills("potato"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.pumpkinpickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("pumpkin", player.UserIDString, GetTotalKills("pumpkin"))}"), i, height), KRUIMainName);
				i++;
			}
			if (config.harvestoptions.hemppickup)
			{
				KRUIelements.Add(KRUILabel(($"{KRLang("hemp", player.UserIDString, GetTotalKills("hemp"))}"), i, height), KRUIMainName);
			}			
			CuiHelper.AddUi(player, KRUIelements);
			return;
		}
		private void DestroyUi(BasePlayer player, string name)
		{
			CuiHelper.DestroyUi(player, name);
		}
		#endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
	        lang.RegisterMessages(new Dictionary<string, string>
	        {
		        ["players"] = "Players: {0}",
				["noplayer"] = "Kill Records:\n No player found with: {0}",
				["chicken"] = "Chickens: {0}",
				["boar"] = "Boars: {0}",
				["stag"] = "Stags: {0}",
				["wolf"] = "Wolves: {0}",
				["bear"] = "Bears: {0}",
				["polarbear"] = "PolarBears: {0}",
				["simpleshark"] = "Sharks: {0}",
				["horse"] = "Horses: {0}",
				["fish"] = "Fish: {0}",
				["treecut"] = "Trees: {0}",
				["oremined"] = "Ore Mined: {0}",
				["cactuscut"] = "Cactus Cut: {0}",
				["woodpickup"] = "Wood Pickup: {0}",
				["orepickup"] = "Ore Pickup: {0}",
				["berries"] = "Berries: {0}",
				["seeds"] = "Seeds: {0}",
				["mushroom"] = "Mushroom: {0}",
				["corn"] = "Corn: {0}",
				["potato"] = "Potato: {0}",
				["pumpkin"] = "Pumpkin: {0}",
				["hemp"] = "Hemp: {0}",
				["dweller"] = "Dwellers: {0}",
		        ["corpse"] = "Animals Harvested: {0}",
		        ["pcorpse"] = "Bodies Harvested: {0}",
		        ["loot"] = "Loot Containers: {0}",
				["unloot"] = "Underwater Loot Containers: {0}",
				["bradheliloot"] = "Brad/Heli Crates: {0}",
				["hackloot"] = "Hackable Crates: {0}",
				["bradley"] = "Bradley: {0}",
				["heli"] = "Patrol Helicopter: {0}",
				["turret"] = "Turret: {0}",
				["bradleyapc"] = "Bradley: {0}",
				["patrolhelicopter"] = "Patrol Helicopter: {0}",
		        ["scientists"] = "Scientist: {0}",
		        ["scarecrow"] = "Scare Crow: {0}",
		        ["deaths"] = "Deaths: {0}",
		        ["suicide"] = "Suicides: {0}",
				["killchat"] = "Show chat kill messages {0}",
				["chickenui"] = "Chickens",
				["boarui"] = "Boars",
				["stagui"] = "Stags",
				["wolfui"] = "Wolves",
				["bearui"] = "Bears",
				["polarbearui"] = "PolarBears",
				["sharkui"] = "Sharks",
				["horseui"] = "Horses",
				["fishui"] = "Fish",
				["playerui"] = "Players",
				["scientistui"] = "Scientists",
				["scarecrowui"] = "Scare Crow",
				["dwellerui"] = "Dwellers",
				["deathui"] = "Deaths",
				["suicideui"] = "Suicides",
				["lootui"] = "Loot Containters",
				["wlootui"] = "Underwater Loots",
				["bradheliui"] = "Brad/Heli Crates",
				["hackableui"] = "Hackable Crates",
				["bradleyui"] = "Bradley",
				["turretui"] = "Turret",
				["treeui"] = "Trees",
				["oreminedui"] = "Ore Mined",
				["cactusui"] = "Cactus Cut",
				["woodui"] = "Wood Picked Up",
				["oreui"] = "Ore Picked Up",
				["mushroomui"] = "Mushrooms",
				["potatoui"] = "Potatos",
				["pumpkinui"] = "Pumpkins",
				["hempui"] = "Hemp",
				["berriesui"] = "Berries",
				["seedsui"] = "Seeds",
				["cornui"] = "Corn",
				["patrolhelicopterui"] = "Patrol Helicopter",
				["corpseui"] = "Animals Harvested",
				["pcorpseui"] = "Bodies Harvested",
		        ["webrequestgood"] = "Kill Record Data Sent to Website:",
		        ["webrequestbad"] = "Couldn't get an answer from Website!",
		        ["webrequestdisabled"] = "WebRequest Disabled - Enable in Config file",
				["totalkills"] = "Total Kills All Players",		
				["totalstats"] = "Total Harvests All Players",		
				["sqlupdate"] = "Your records have been manually updated in the database",	
				["sqlcheck"] = "Your records have been checked and updated in database",	
				["sqlcheckall"] = "All players have been checked and updated in database",
				["datafilebackup"] = "Records have been manually saved to data file",
				["datafilenotinuse"] = "DataFile not in use, config is set to SQL only",
				["resetkills"] = "All Kill Records have been reset and plugin reloaded",
				["KRHelp"] = 
					"Kill Records by MACHIN3 \n" +
					"/pkills - Open Kill Records UI \n" +
					"/pkillschat - Show kill Records in chat \n" +
					"/pkills (playername) - Open players Kill Records UI \n" +
					"/pkillschat (playername) - Show players Kill Records in chat \n" +
					"/topkills - Open top players UI \n" +
					"/topkillschat (type) - Show top players list in chat \n" +
					"/leadkills - Opens leaderboards UI \n" +
                    "/totalkills - Show global kill count for all entities \n" +
                    "/totalkillschat - Show global kill count in chat \n" +
					"/killchat true/false - Enable/Disable Kill messages",
				["KRHelpadmin"] =
					"Kill Records by MACHIN3 \n" +
					"/krbackup - Manually saves records to datafile \n" +
					"/krweb - Manually sends records over webrequest if webrequest enabled \n" +
					"/krsql update - Manually updates your records to SQL if SQL enabled \n" +
					"/krsql check - Checks SQL to see if your records exist, if not will create if SQL enabled \n" +
					"/krsql checkall - Checks SQL to see if all records exist, if not will create if SQL enabled \n" +
					"/resetkillrecords - Clears all kill record data for all players"

			}, this);
        }
        private string KRLang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
		#endregion

		#region WebRequest
		private void SaveKillRecordWeb(BasePlayer player)
        {
	        if (config.webrequest.UseWebrequests)
	        {
				// Get display name for regex
				Record record = GetRecord(player);
				// Remove special characters in names to prevent injection
		        string removespecials = "(['^$.|?*+()&{}\\\\])";
		        string replacename = "\\$1";
		        Regex rgx = new Regex(removespecials);
		        var playername = rgx.Replace(player.displayName, replacename);

				webrequest.Enqueue(
					$"{config.webrequest.DataURL}?" +
                    $"secretKey={config.webrequest.SecretKey}&" +
                    $"steamid={record.id}&" +
                    $"displayname={playername}&" +
                    $"chicken={record.chicken}&" +
                    $"boar={record.boar}&" +
                    $"wolf={record.wolf}&" +
                    $"stag={record.stag}&" +
                    $"bear={record.bear}&" +
                    $"polarbear={record.polarbear}&" +
                    $"shark={record.shark}&" +
                    $"horse={record.horse}&" +
                    $"fish={record.fish}&" +
                    $"scientist={record.scientistnpcnew}&" +
                    $"scarecrow={record.scarecrow}&" +
                    $"dweller={record.dweller}&" +
                    $"baseplayer={record.baseplayer}&" +
                    $"lootcontainer={record.lootcontainer}&" +
                    $"underwaterlootcontainer={record.underwaterlootcontainer}&" +
                    $"treescut={record.treescut}&" +
                    $"oremined={record.oremined}&" +
                    $"cactuscut={record.cactuscut}&" +
                    $"woodpickup={record.woodpickup}&" +
                    $"orepickup={record.orepickup}&" +
                    $"berriespickup={record.berriespickup}&" +
                    $"mushroompickup={record.mushroompickup}&" +
                    $"hemppickup={record.hemppickup}&" +
                    $"cornpickup={record.cornpickup}&" +
                    $"seedpickup={record.seedpickup}&" +
                    $"potatopickup={record.potatopickup}&" +
                    $"pumpkinpickup={record.pumpkinpickup}&" +
                    $"bradhelicrate={record.bradhelicrate}&" +
                    $"hackablecrate={record.hackablecrate}&" +
                    $"bradley={record.bradley}&" +
                    $"heli={record.heli}&" +
                    $"turret={record.turret}&" +
                    $"animalharvest={record.basecorpse}&" +
                    $"bodiesharvest={record.npcplayercorpse}&" +
                    $"deaths={record.deaths}&" +
                    $"suicides={record.suicides}"
					, null, (code, response) =>
		        {
			        if (code != 200 || response == null)
			        {
				        Puts($"Couldn't get an answer from Website!");
				        return;
			        }
			        Puts($"Kill Record Data Sent to Website: {response}");
		        }, this, RequestMethod.POST);
	        }
        }
		#endregion

		#region API
		private object GetKillRecord(string playerid, string KillType)
		{
			Record record;
			if (!_recordCache.TryGetValue(playerid, out record)) return 0;
			if (KillType == "chicken")
			{
				return record.chicken;
			}
			else if (KillType == "boar")
			{ 
				return record.boar;
			}
			else if (KillType == "stag")
			{ 
				return record.stag; 
			}
			else if (KillType == "wolf")
			{
				return record.wolf; 
			}
			else if (KillType == "bear")
			{ 
				return record.bear;
			}
			else if (KillType == "polarbear")
			{ 
				return record.polarbear;
			}
			else if (KillType == "shark")
			{ 
				return record.shark;
			}
			else if (KillType == "horse" || KillType == "ridablehorse")
			{ 
				return record.horse;
			}
			else if (KillType == "fish")
			{ 
				return record.fish;
			}
			else if (KillType == "scientistnpcnew" || KillType == "scientist") 
			{
				return record.scientistnpcnew;
			}
			else if (KillType == "scarecrow") 
			{
				return record.scarecrow;
			}
			else if (KillType == "dweller" || KillType == "tunneldweller" || KillType == "underwaterdweller") 
			{ 
				return record.dweller; 
			}
			else if (KillType == "baseplayer")
			{
				return record.baseplayer;
			}
			else if (KillType == "lootcontainer") 
			{ 
				return record.lootcontainer; 
			}
			else if (KillType == "underwaterlootcontainer") 
			{ 
				return record.underwaterlootcontainer; 
			}
			else if (KillType == "lockedbyentcrate") 
			{ 
				return record.bradhelicrate; 
			}
			else if (KillType == "hackablelockedcrate") 
			{ 
				return record.hackablecrate; 
			}
			else if (KillType == "bradleyapc") 
			{ 
				return record.bradley; 
			}
			else if (KillType == "patrolhelicopter") 
			{ 
				return record.heli; 
			}
			else if (KillType == "turret") 
			{ 
				return record.turret; 
			}
			else if (KillType == "basecorpse")
			{ 
				return record.basecorpse;
			}
			else if (KillType == "npcplayercorpse") 
			{
				return record.npcplayercorpse;
			}
			else if (KillType == "death")
			{
				return record.deaths;
			}
			else if (KillType == "suicide")
			{
				return record.suicides;
			}
			else if (KillType == "trees")
			{
				return record.treescut;
			}
			else if (KillType == "oremined")
			{
				return record.oremined;
			}
			else if (KillType == "catus")
			{
				return record.cactuscut;
			}
			else if (KillType == "wood")
			{
				return record.woodpickup;
			}
			else if (KillType == "orepickup")
			{
				return record.orepickup;
			}
			else if (KillType == "berries")
			{
				return record.berriespickup;
			}
			else if (KillType == "seed")
			{
				return record.seedpickup;
			}
			else if (KillType == "hemp")
			{
				return record.hemppickup;
			}
			else if (KillType == "potato")
			{
				return record.potatopickup;
			}
			else if (KillType == "pumpkin")
			{
				return record.pumpkinpickup;
			}
			else if (KillType == "mushroom")
			{
				return record.mushroompickup;
			}
			else if (KillType == "corn")
			{
				return record.cornpickup;
			}
			// Return all player data in Json if requested
			if (KillType == "all")
			{
				return JsonConvert.SerializeObject(record);
			}
			return 0;
		}
		private object GetCach()
		{
			return _recordCache;
		}
		private string GetVersion()
		{
			return version;
		}
		#endregion
	}
}