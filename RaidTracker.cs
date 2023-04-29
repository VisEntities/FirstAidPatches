using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Raid Tracker", "Clearshot", "2.1.1")]
    [Description("Track raids by explosives, weapons, and ammo with detailed on-screen visuals")]
    class RaidTracker : CovalencePlugin
    {
        private bool _dev = false; // force certain config options
        private bool _debug = false; // respect config, detailed output
        private float _debugDrawDuration = 15f;
        private static RaidTracker _instance;
        private PluginConfig _config;
        private bool _isConfigValid = true;
        private StringBuilder _sb = new StringBuilder();
        private Game.Rust.Libraries.Player _rustPlayer = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Player>("Player");

        private DiscordWebhookManager _discordWebhookManager = new DiscordWebhookManager();
        private readonly int _collisionLayerMask = LayerMask.GetMask("Construction", "Deployed");
        private Dictionary<Vector3, ulong> _MLRSRocketOwners = new Dictionary<Vector3, ulong>();
        private Dictionary<ulong, bool> _verboseMode = new Dictionary<ulong, bool>();
        private Dictionary<ulong, string[]> _lastViewCommand = new Dictionary<ulong, string[]>();
        private Dictionary<string, DecayEntityIgnoreOptions> _decayEntityIgnoreList = new Dictionary<string, DecayEntityIgnoreOptions>();
        private Dictionary<string, string> _prefabToItem = new Dictionary<string, string>();
        private Dictionary<string, string> _buildingBlockPrettyNames = new Dictionary<string, string>();
        private Dictionary<ulong, float> _notificationCooldown = new Dictionary<ulong, float>();

        private bool _wipeData;
        private List<RaidEvent> _raidEventLog = new List<RaidEvent>();
        private string _raidEventLogFilename;
        private int _raidEventLogCount;

        private string[] _ignoredTimedExplosives = new string[] {
            "firecrackers.deployed",
            "flare.deployed",
            "maincannonshell",
            "rocket_heli",
            "rocket_heli_napalm"
        };

        private string[] _uniqueHexColors = new string[] {
            "#01FFFE", "#FFA6FE", "#FFDB66", "#006401", "#010067",
            "#95003A", "#007DB5", "#FF00F6", "#FFEEE8", "#774D00",
            "#90FB92", "#0076FF", "#D5FF00", "#FF937E", "#6A826C",
            "#FF029D", "#FE8900", "#7A4782", "#7E2DD2", "#85A900",
            "#FF0056", "#A42400", "#00AE7E", "#683D3B", "#BDC6FF",
            "#263400", "#BDD393", "#00B917", "#9E008E", "#001544",
            "#C28C9F", "#FF74A3", "#01D0FF", "#004754", "#E56FFE",
            "#788231", "#0E4CA1", "#91D0CB", "#BE9970", "#968AE8",
            "#BB8800", "#43002C", "#DEFF74", "#00FFC6", "#FFE502",
            "#620E00", "#008F9C", "#98FF52", "#7544B1", "#B500FF",
            "#00FF78", "#FF6E41", "#005F39", "#6B6882", "#5FAD4E",
            "#A75740", "#A5FFD2", "#FFB167", "#009BFF", "#E85EBE",
            "#00FF00", "#0000FF", "#FF0000", "#000000"
        };
        private List<Color> _uniqueColors = new List<Color>();
        private Dictionary<ulong, Color> _teamColors = new Dictionary<ulong, Color>();
        private int _currentTeamColorIdx = 0;

        [PluginReference]
        Plugin AbandonedBases, Clans, RaidableBases;

        private const string PERM_WIPE = "raidtracker.wipe";
        private const string PERM_PX = "raidtracker.px";

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PERM_WIPE, this);
            permission.RegisterPermission(PERM_PX, this);

            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnMlrsFired));
            Unsubscribe(nameof(OnMlrsFiringEnded));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnServerSave));
        }

        private void OnServerInitialized()
        {
            _debug = _debug || _config.debug;
            if (_dev)
            {
                _debug = true;
                Puts("[DEV] Dev mode enabled!");
            }

            PrintDebug("Debug mode enabled!");

            _raidEventLogFilename = $"{Name}\\RaidEventLog";
            _raidEventLog = Interface.Oxide.DataFileSystem.ReadObject<List<RaidEvent>>(_raidEventLogFilename);
            _raidEventLogCount = _raidEventLog.Count;

            foreach (string hexColor in _uniqueHexColors)
            {
                Color color;
                if (ColorUtility.TryParseHtmlString(hexColor, out color))
                    _uniqueColors.Add(color);
            }

            foreach (var item in ItemManager.itemList.OrderBy(x => x.shortname))
            {
                ItemModEntity itemModEnt = item.GetComponent<ItemModEntity>();
                if (itemModEnt != null)
                {
                    var gameObjRef = itemModEnt.entityPrefab;
                    if (string.IsNullOrEmpty(gameObjRef.guid)) continue;

                    AddPrefabToItem(gameObjRef.resourcePath, item.shortname, nameof(ItemModEntity));
                }

                ItemModDeployable itemModDeploy = item.GetComponent<ItemModDeployable>();
                if (itemModDeploy != null)
                {
                    var gameObjRef = itemModDeploy.entityPrefab;
                    if (string.IsNullOrEmpty(gameObjRef.guid)) continue;

                    AddPrefabToItem(gameObjRef.resourcePath, item.shortname, nameof(ItemModDeployable));
                }

                ItemModProjectile itemModProj = item.GetComponent<ItemModProjectile>();
                if (itemModProj != null)
                {
                    var gameObjRef = itemModProj.projectileObject;
                    if (string.IsNullOrEmpty(gameObjRef.guid)) continue;

                    AddPrefabToItem(gameObjRef.resourcePath, item.shortname, nameof(ItemModProjectile));
                }
            }

            if (_wipeData && _config.deleteDataOnWipe)
            {
                Puts($"Wipe detected! Removing {_raidEventLog.Count} raid events.");
                _wipeData = false;
                _raidEventLog = new List<RaidEvent>();
                SaveRaidEventLog();
            }

            if (_raidEventLog.Count > 0)
            {
                int removed = 0;
                DateTime currentDateTime = DateTime.Now;
                for (int i = _raidEventLog.Count - 1; i >= 0; i--)
                {
                    RaidEvent raidEvent = _raidEventLog[i];
                    if (currentDateTime.Subtract(raidEvent.timestamp).TotalDays >= _config.daysBeforeDelete)
                    {
                        _raidEventLog.RemoveAt(i);
                        removed++;
                    }
                }

                if (removed > 0)
                {
                    Puts($"Removed {removed} raid events older than {_config.daysBeforeDelete} days");
                    SaveRaidEventLog();
                }
            }

            var saveList = false;
            var decayEntityListFilename = $"{Name}\\DecayEntityIgnoreList";
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(decayEntityListFilename))
            {
                Puts($"Generating DecayEntityIgnoreList, any items enabled in this list will be ignored by the plugin");
                Puts($"Saving DecayEntityIgnoreList to /oxide/data/{Utility.CleanPath(decayEntityListFilename)}.json");
            }

            _decayEntityIgnoreList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, DecayEntityIgnoreOptions>>(decayEntityListFilename);

            Dictionary<BuildingGrade.Enum, string> buildingGradeNames = new Dictionary<BuildingGrade.Enum, string> {
                { BuildingGrade.Enum.Twigs, "Twig" },
                { BuildingGrade.Enum.TopTier, "HQM" }
            };

            foreach (var prefab in GameManifest.Current.entities)
            {
                var gameObj = GameManager.server.FindPrefab(prefab);
                if (gameObj == null) continue;

                var thrownWep = gameObj.GetComponent<ThrownWeapon>();
                if (thrownWep != null)
                {
                    var itemShortname = GetItemFromPrefabShortname(thrownWep.ShortPrefabName); // get item shortname from held entity
                    AddPrefabToItem(thrownWep.prefabToThrow.resourcePath, itemShortname, nameof(ThrownWeapon)); // assign deployed entity to same item shortname

                    PrintDebug($"  {thrownWep.ShortPrefabName}[{thrownWep.GetType()}] -> {GetPrefabShortname(thrownWep.prefabToThrow.resourcePath)} -> {itemShortname}");
                }

                var ent = gameObj.GetComponent<DecayEntity>();
                if (ent != null)
                {
                    var itemShortname = GetItemFromPrefabShortname(ent.ShortPrefabName);
                    if (!_decayEntityIgnoreList.ContainsKey(ent.PrefabName))
                    {
                        var item = ItemManager.FindItemDefinition(itemShortname);
                        var itemName = item != null ? $"{item?.displayName?.english ?? "Unknown"} ({itemShortname})" : "";
                        _decayEntityIgnoreList[ent.PrefabName] = new DecayEntityIgnoreOptions {
                            name = itemName,
                            ignore = false,
                            ignoreDiscord = false
                        };
                        LogToSingleFile("decay_entity_log", $"added {ent.PrefabName} {(!string.IsNullOrEmpty(itemName) ? $"[ITEM: {itemName}]" : "")}");
                        saveList = true;
                    }

                    if (ent is BuildingBlock)
                    {
                        PrintDebug($"BuildingBlock - {ent.ShortPrefabName}");

                        Type gradeType = typeof(BuildingGrade.Enum);
                        foreach(var grade in Enum.GetNames(gradeType))
                        {
                            var e = (BuildingGrade.Enum)Enum.Parse(gradeType, grade);
                            if (e == BuildingGrade.Enum.None || e == BuildingGrade.Enum.Count) continue;

                            var buildingBlockItemShortname = $"{ent.ShortPrefabName}.{grade.ToLower()}";
                            var gradeName = buildingGradeNames.ContainsKey(e) ? buildingGradeNames[e] : grade;
                            var prettyName = $"{gradeName} {ent.prefabAttribute.Find<Construction>(StringPool.Get(ent.PrefabName))?.info.name?.english ?? "Unknown"}";
                            _buildingBlockPrettyNames[buildingBlockItemShortname] = prettyName;

                            PrintDebug($"   BuildingGrade[{grade}] - {prettyName}[{buildingBlockItemShortname}]");
                        }
                    }
                }
            }

            if (saveList)
            {
                var sorted = _decayEntityIgnoreList
                    .OrderBy(x => string.IsNullOrWhiteSpace(x.Value.name))
                    .ThenBy(x => x.Value.name)
                    .ToDictionary(x => x.Key, x => x.Value);

                Interface.Oxide.DataFileSystem.WriteObject(decayEntityListFilename, sorted);
            }

            var saveCfg = false;
            foreach (var trackerList in _config.trackers)
            {
                foreach (var weaponCfg in trackerList.Value)
                {
                    if (!string.IsNullOrEmpty(weaponCfg.Value.name)) continue;

                    var shortname = GetItemFromPrefabShortname(weaponCfg.Key);
                    weaponCfg.Value.name = GetPrettyItemName(shortname);
                    saveCfg = true;
                }
            }

            // Don't overwrite the config if invalid since the user will lose their config!
            if (_isConfigValid && saveCfg)
                SaveConfig();

            Subscribe(nameof(OnPlayerDisconnected));
            Subscribe(nameof(OnMlrsFired));
            Subscribe(nameof(OnMlrsFiringEnded));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(OnServerSave));
        }

        private void Unload()
        {
            var trackers = UnityEngine.Object.FindObjectsOfType<ExplosiveTracker>();
            if (trackers != null && trackers.Length > 0)
            {
                Puts($"Destroying {trackers.Length} explosive trackers");
                foreach (var t in trackers)
                {
                    t.logEvent = false;
                    UnityEngine.Object.Destroy(t);
                }
            }

            _instance = null;
            SaveRaidEventLog();
        }

        private void OnPlayerDisconnected(BasePlayer pl, string reason)
        {
            _verboseMode.Remove(pl.userID);
            _lastViewCommand.Remove(pl.userID);
            _notificationCooldown.Remove(pl.userID);
        }

        private void OnMlrsFired(MLRS mlrs, BasePlayer driver)
        {
            _MLRSRocketOwners[mlrs.transform.position] = driver.userID;
        }

        private void OnMlrsFiringEnded(MLRS mlrs)
        {
            _MLRSRocketOwners.Remove(mlrs.transform.position);
        }

        private void OnEntitySpawned(TimedExplosive ent)
        {
            if (ent == null || _ignoredTimedExplosives.Contains(ent.ShortPrefabName)) return;

            var trackerCategory = "entity_collision";
            if (!AddOrFindWeaponConfig(trackerCategory, ent.ShortPrefabName).enabled) return;

            if (ent is MLRSRocket && _MLRSRocketOwners.Count > 0) // fix MLRS rocket creatorEntity being null when there is no driver in the MLRS truck
            {
                var mlrsOwnerID = _MLRSRocketOwners.First(x => Vector3.Distance(x.Key, ent.transform.position) < 25f).Value;
                var mlrsOwner = BasePlayer.FindByID(mlrsOwnerID);
                if (mlrsOwner != null)
                {
                    ent.creatorEntity = mlrsOwner;
                    ent.OwnerID = mlrsOwner.userID;
                }
            }

            var tracker = ent.gameObject.AddComponent<ExplosiveTracker>();
            if (tracker != null)
            {
                tracker.Init(trackerCategory);
            }

            if (_debug)
            {
                var pl = ent.creatorEntity as BasePlayer;
                PrintDebug($"Added explosive tracker to {ent.ShortPrefabName} spawned by {pl?.displayName ?? "Unknown"}[{pl?.userID ?? 0}]");
            }
        }

        private void OnEntityDeath(DecayEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return;

            var initiator = info.Initiator;
            if (initiator is FireBall) // Log fireballs spawned from inc ammo, fire arrows, etc
            {
                var fireballTrackerCategory = "entity_death_fire";
                var fireballWeaponCfg = AddOrFindWeaponConfig(fireballTrackerCategory, initiator.ShortPrefabName);
                if (!fireballWeaponCfg.enabled || IsDecayEntityIgnored(entity))
                    return;

                var creator = initiator.creatorEntity as BasePlayer;
                RaidEvent raidEventFireball = new RaidEvent {
                    attackerName = creator?.displayName ?? fireballWeaponCfg.name,
                    attackerSteamID = creator?.userID ?? 0,
                    attackerTeamID = creator?.Team?.teamID ?? 0,
                    victimSteamID = entity.OwnerID,
                    weapon = $"{initiator.ShortPrefabName}[{fireballTrackerCategory}]",
                    hitEntity = $"EVENT.BURNT {GetDecayEntityShortname(entity)}",
                    startPos = entity.transform.position + new Vector3(0, .2f, 0),
                    endPos = entity.transform.position,
                    timestamp = DateTime.Now
                };
                _raidEventLog.Add(raidEventFireball);
                raidEventFireball.Notify(entity);

                PrintDebug($"OnEntityDeath ({initiator.ShortPrefabName}) - WeaponPrefab: {info?.WeaponPrefab?.ShortPrefabName ?? "NULL"}, ProjectilePrefab: {info?.ProjectilePrefab?.name ?? "NULL"}");
                PrintDebug($"{initiator.ShortPrefabName} ({initiator.creatorEntity}) BURNT {GetDecayEntityShortname(entity)}[{entity?.net?.ID.Value ?? 0}]");
                return;
            }

            var attacker = info.InitiatorPlayer;
            if (attacker == null || IsDecayEntityOrAttackerIgnored(entity, attacker))
                return;

            if (info?.WeaponPrefab == null && info?.damageTypes?.GetMajorityDamageType() == Rust.DamageType.Heat) // Log fires spawned from flame throwers or other weapons
            {
                var fireTrackerCategory = "entity_death_fire";
                var fireWeapon = "fire_damage";
                var fireWeaponCfg = AddOrFindWeaponConfig(fireTrackerCategory, fireWeapon);
                if (!fireWeaponCfg.enabled)
                    return;

                RaidEvent raidEventFire = new RaidEvent {
                    attackerName = attacker?.displayName ?? "Unknown",
                    attackerSteamID = attacker?.userID ?? 0,
                    attackerTeamID = attacker?.Team?.teamID ?? 0,
                    victimSteamID = entity.OwnerID,
                    weapon = $"{fireWeapon}[{fireTrackerCategory}]",
                    hitEntity = $"EVENT.BURNT {GetDecayEntityShortname(entity)}",
                    startPos = entity.transform.position + new Vector3(0, .2f, 0),
                    endPos = entity.transform.position,
                    timestamp = DateTime.Now
                };
                _raidEventLog.Add(raidEventFire);
                raidEventFire.Notify(entity);

                PrintDebug($"OnEntityDeath ({fireWeapon}) - WeaponPrefab: {info?.WeaponPrefab?.ShortPrefabName ?? "NULL"}, ProjectilePrefab: {info?.ProjectilePrefab?.name ?? "NULL"}");
                PrintDebug($"{fireWeapon} BURNT {GetDecayEntityShortname(entity)}[{entity?.net?.ID.Value ?? 0}]");
                return;
            }

            var weaponPrefabShortname = info?.WeaponPrefab?.ShortPrefabName;
            var projectilePrefabShortname = info?.ProjectilePrefab?.name;
            var projectileItemShortname = projectilePrefabShortname != null ? GetItemFromPrefabShortname(projectilePrefabShortname) : null;

            var heldEntity = attacker.GetHeldEntity();
            if (heldEntity is AttackEntity)
            {
                if (info.WeaponPrefab == null)
                {
                    weaponPrefabShortname = heldEntity.ShortPrefabName;

                    PrintDebug($"OnEntityDeath - WeaponPrefab is NULL! Using HeldEntity: {heldEntity.ShortPrefabName ?? "NULL"}");
                }

                var projectile = heldEntity?.GetComponent<BaseProjectile>();
                var heldProjectileItemShortname = projectile?.primaryMagazine?.ammoType?.shortname ?? null;
                if ((info.WeaponPrefab == null && info.ProjectilePrefab == null)
                    || (projectileItemShortname != null && heldProjectileItemShortname != null && projectileItemShortname != heldProjectileItemShortname)) // certain projectiles from HitInfo do not match the projectile in the players gun Ex: ammo.pistol.hv, rifle.ammmo.hv, ammo.shotgun
                {
                    if (_debug)
                    {
                        if (projectileItemShortname != heldProjectileItemShortname)
                            PrintDebug($"OnEntityDeath - ProjectileItemShortname ({projectileItemShortname ?? "NULL"}) != HeldProjectileItemShortname ({heldProjectileItemShortname ?? "NULL"})! Using HeldProjectileItemShortname: {heldProjectileItemShortname ?? "NULL"}");
                        else
                            PrintDebug($"OnEntityDeath - WeaponPrefab + ProjectilePrefab are NULL! Using HeldEntityProjectile: {projectileItemShortname ?? "NULL"}");
                    }

                    projectileItemShortname = heldProjectileItemShortname;
                }
            }

            var weaponTrackerCategory = "entity_death_weapon";
            var weaponItemShortname = weaponPrefabShortname != null ? GetItemFromPrefabShortname(weaponPrefabShortname) : null;
            var weaponEnabled = weaponItemShortname != null ? AddOrFindWeaponConfig(weaponTrackerCategory, weaponItemShortname).enabled : false;

            var projectileTrackerCategory = "entity_death_ammo";
            var projectileEnabled = projectileItemShortname != null && weaponItemShortname != projectileItemShortname ? AddOrFindWeaponConfig(projectileTrackerCategory, projectileItemShortname).enabled : false;

            PrintDebug($"OnEntityDeath - WeaponPrefab: {info?.WeaponPrefab?.ShortPrefabName ?? "NULL"} ({weaponItemShortname ?? "NULL"}), ProjectilePrefab: {info?.ProjectilePrefab?.name ?? "NULL"} ({projectileItemShortname ?? "NULL"})");

            if (!weaponEnabled && !projectileEnabled) return;

            string weapon;
            if (weaponEnabled && projectileItemShortname != null && weaponItemShortname != projectileItemShortname)
                weapon = $"{weaponItemShortname}[{weaponTrackerCategory}];{projectileItemShortname}[{projectileTrackerCategory}]";
            else if (projectileEnabled && weaponItemShortname != null && weaponItemShortname != projectileItemShortname)
                weapon = $"{projectileItemShortname}[{projectileTrackerCategory}];{weaponItemShortname}[{weaponTrackerCategory}]";
            else if (projectileEnabled)
                weapon = $"{projectileItemShortname}[{projectileTrackerCategory}]";
            else
                weapon = $"{weaponItemShortname}[{weaponTrackerCategory}]";

            var startPos = attacker.transform.position;
                startPos.y += attacker.GetHeight() - .5f;
            var endPos = info.HitPositionWorld != Vector3.zero && info.HitPositionWorld != entity.transform.position ? info.HitPositionWorld : entity.WorldSpaceBounds().ToBounds().center;

            RaidEvent raidEvent = new RaidEvent {
                attackerName = attacker?.displayName ?? "Unknown",
                attackerSteamID = attacker?.userID ?? 0,
                attackerTeamID = attacker?.Team?.teamID ?? 0,
                victimSteamID = entity.OwnerID,
                weapon = weapon,
                hitEntity = $"EVENT.DESTROYED {GetDecayEntityShortname(entity)}",
                startPos = startPos,
                endPos = endPos,
                timestamp = DateTime.Now
            };
            _raidEventLog.Add(raidEvent);
            raidEvent.Notify(entity);

            PrintDebug($"{weapon} DESTROYED {GetDecayEntityShortname(entity)}[{entity?.net?.ID.Value ?? 0}]");
        }

        private void OnServerSave()
        {
            SaveRaidEventLog();
        }

        private void OnNewSave(string filename)
        {
            _wipeData = true;
        }

        #endregion

        #region Commands

        [Command("x")]
        private void ViewExplosionsCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null || !pl.IsAdmin) return;

            if (args.Length > 0 && (args[0].ToLower() == "v" || args[0].ToLower() == "extra"))
            {
                _verboseMode[pl.userID] = _verboseMode.ContainsKey(pl.userID) ? !_verboseMode[pl.userID] : true;
                SendChatMsg(pl, lang.GetMessage(_verboseMode[pl.userID] ? "ViewEventsCommand.ExtraModeEnabled" : "ViewEventsCommand.ExtraModeDisabled", this, pl.UserIDString));
                return;
            }

            float radius = _config.searchRadius > 0f ? _config.searchRadius : 50f;
            string filterType = args.Length > 0 ? args[0].ToLower() : "";
            string filter = "";
            bool verbose = _verboseMode.ContainsKey(pl.userID) && _verboseMode[pl.userID];

            Vector3 pos = pl.transform.position;
                    pos.y += pl.GetHeight() - .5f;

            float tempRadius;
            IEnumerable<IGrouping<RaidFilter, RaidEvent>> groupedRaidsNearMe;
            switch (filterType)
            {
                case "l":
                case "last":
                    string[] lastArgs;
                    if (_lastViewCommand.TryGetValue(pl.userID, out lastArgs))
                        ViewExplosionsCommand(player, command, lastArgs);
                    return;
                case "help":
                    _sb.Clear();
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpHeader", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpDefault", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpExtraMode", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpWipe", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpLast", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpFilter", this, pl.UserIDString));
                    _sb.Append("<indent=6>");
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpFilterTime", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpFilterWeapon", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpFilterEntity", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpFilterTeam", this, pl.UserIDString));
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpFilterPlayer", this, pl.UserIDString));
                    _sb.Append("<indent=0>");
                    _sb.AppendLine(lang.GetMessage("ViewEventsCommand.HelpPrintRaidEvent", this, pl.UserIDString));
                    SendChatMsg(pl, _sb.ToString(), "");

                    return;
                case "wipe":
                    if (!permission.UserHasPermission(pl.UserIDString, PERM_WIPE))
                    {
                        SendChatMsg(pl, lang.GetMessage("ViewEventsCommand.WipePermission", this, pl.UserIDString));
                        return;
                    }

                    if (args.Length > 1 && float.TryParse(args[1], out tempRadius))
                        radius = tempRadius;

                    var raidEventsToWipe = FindRaidEventsInSphere(pos, radius).ToList();
                    if (raidEventsToWipe.Count == 0)
                    {
                        SendChatMsg(pl, string.Format(lang.GetMessage("ViewEventsCommand.NotFoundRadius", this, pl.UserIDString), radius));
                        return;
                    }

                    foreach(var raidEvent in raidEventsToWipe)
                    {
                        int idx = raidEvent.GetIndex();
                        if (idx > -1)
                            _raidEventLog.RemoveAt(idx);
                    }

                    var playerPos = pl.transform.position;
                    var gridPos = PhoneController.PositionToGridCoord(playerPos);
                    var filename = $"{Name}\\WipedRaidEvents\\{string.Format("{0:yyyy-MM-dd}", DateTime.Now)}\\{pl.userID}\\{gridPos}_{string.Format("{0:h-mm-tt}", DateTime.Now)}";
                    LogToFile("wiped_raid_events", $"{pl.displayName}[{pl.userID}] wiped {raidEventsToWipe.Count} raid events in {gridPos} ({FormatPosition(playerPos)})", this);
                    Interface.Oxide.DataFileSystem.WriteObject(filename, raidEventsToWipe);
                    SaveRaidEventLog();

                    SendChatMsg(pl, string.Format(lang.GetMessage("ViewEventsCommand.WipedRaidEventsRadius", this, pl.UserIDString), raidEventsToWipe.Count, radius, gridPos));
                    return;
                case "time":
                    filter = args.Length > 1 ? args[1].ToLower() : "";

                    if (args.Length > 2 && float.TryParse(args[2], out tempRadius))
                        radius = tempRadius;

                    double hours;
                    if (!double.TryParse(filter, out hours))
                        hours = 24;

                    groupedRaidsNearMe = FindRaidEventsInSphere(pos, radius)
                        .Where(x => DateTime.Now.Subtract(x.timestamp).TotalHours <= hours)
                        .GroupBy(x => new RaidFilter { filter = $"{x.attackerName}[{x.attackerSteamID}]", filterType = filterType });
                    break;
                case "weapon":
                    filter = args.Length > 1 ? args[1].ToLower() : "";

                    if (args.Length > 2 && float.TryParse(args[2], out tempRadius))
                        radius = tempRadius;

                    groupedRaidsNearMe = FindRaidEventsInSphere(pos, radius)
                        .Where(x => x.weapon.Contains(filter) || FindWeaponConfig(x.GetTrackerCategory(), x.GetPrimaryWeaponShortname()).name.ToLower().Contains(filter))
                        .GroupBy(x => new RaidFilter { filter = FindWeaponConfig(x.GetTrackerCategory(), x.GetPrimaryWeaponShortname()).name, filterType = filterType });
                    break;
                case "entity":
                    filter = args.Length > 1 ? args[1].ToLower() : "";

                    if (args.Length > 2 && float.TryParse(args[2], out tempRadius))
                        radius = tempRadius;

                    groupedRaidsNearMe = FindRaidEventsInSphere(pos, radius)
                        .Where(x => x.hitEntity.Contains(filter))
                        .GroupBy(x => new RaidFilter { filter = x.GetHitEntityShortname(), filterType = filterType });
                    break;
                case "team":
                    filter = args.Length > 1 ? args[1].ToLower() : "";

                    if (args.Length > 2 && float.TryParse(args[2], out tempRadius))
                        radius = tempRadius;

                    IEnumerable<RaidEvent> tempRaidEvents = FindRaidEventsInSphere(pos, radius);
                    if (!string.IsNullOrEmpty(filter))
                        tempRaidEvents = tempRaidEvents.Where(x => x.attackerTeamID.ToString() == filter);

                    groupedRaidsNearMe = tempRaidEvents.GroupBy(x => {
                        var team = RelationshipManager.ServerInstance.FindTeam(x.attackerTeamID);
                        return new RaidFilter { filter = team != null ? $"{team.GetLeader()?.displayName ?? "UNKNOWN LEADER"}'s Team (ID: {x.attackerTeamID})" : x.attackerTeamID.ToString(), filterType = filterType };
                    });
                    break;
                case "player":
                    filter = args.Length > 1 ? args[1].ToLower() : "";

                    if (args.Length > 2 && float.TryParse(args[2], out tempRadius))
                        radius = tempRadius;

                    groupedRaidsNearMe = FindRaidEventsInSphere(pos, radius)
                        .Where(x => x.attackerSteamID.ToString() == filter || x.attackerName.ToLower().Contains(filter))
                        .GroupBy(x => new RaidFilter { filter = $"{x.attackerName}[{x.attackerSteamID}]", filterType = filterType });
                    break;
                default:
                    if (args.Length > 0 && float.TryParse(args[0], out tempRadius))
                        radius = tempRadius;

                    groupedRaidsNearMe = FindRaidEventsInSphere(pos, radius)
                        .GroupBy(x => new RaidFilter { filter = $"{x.attackerName}[{x.attackerSteamID}]", filterType = "default" });
                    break;
            }

            DrawRaidEvents(pl, groupedRaidsNearMe, filterType, filter, radius);
            _lastViewCommand[pl.userID] = args;
        }

        [Command("px")]
        private void PlayerViewExplosionsCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            bool isAdmin = player.IsAdmin;
            if (!isAdmin && !permission.UserHasPermission(pl.UserIDString, PERM_PX))
            {
                SendChatMsg(pl, string.Format(lang.GetMessage("ViewEventsCommand.NoPermission", this, pl.UserIDString), $"/{command}"));
                return;
            }

            try
            {
                if (!isAdmin)
                {
                    pl.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    pl.SendNetworkUpdateImmediate();
                }

                float radius = _config.searchRadius > 0f ? _config.searchRadius : 50f;
                float tempRadius;
                if (args.Length > 0 && float.TryParse(args[0], out tempRadius))
                    radius = Mathf.Clamp(tempRadius, 0f, 100f);

                Vector3 pos = pl.transform.position;
                        pos.y += pl.GetHeight() - .5f;

                var groupedRaidsNearMe = FindRaidEventsInSphere(pos, radius)
                    .Where(x => x.victimSteamID == pl.userID || (pl.Team != null && pl.Team.members.Contains(x.victimSteamID)) || FindWeaponConfig(x.GetTrackerCategory(), x.GetPrimaryWeaponShortname()).alwaysLog)
                    .Where(x => DateTime.Now.Subtract(x.timestamp).Minutes > _config.playerViewExplosionsCommand.ignoreRaidEventsLessThanMinutes)
                    .GroupBy(x => new RaidFilter { filter = $"{x.attackerSteamID.GetHashCode()}", filterType = "victim" });

                DrawRaidEvents(pl, groupedRaidsNearMe, "victim", pl.UserIDString, radius, _config.playerViewExplosionsCommand.drawAttackerName);
            }
            catch (Exception ex)
            {
                LogToSingleFile("px_error_log", $"error drawing player explosions for {pl.userID}[{pl?.displayName ?? "Unknown"}]\n\nException: {ex.Message}");
            }

            if (!isAdmin)
            {
                pl.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                pl.SendNetworkUpdateImmediate();
            }
        }

        [Command("re")]
        private void RaidEventDetailsCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null || !pl.IsAdmin) return;

            int index = -1;
            int i;
            if (args.Length > 0 && int.TryParse(args[0], out i))
                index = i;

            if (index < _raidEventLog.Count && _raidEventLog[index] != null)
            {
                RaidEvent raidEvent = _raidEventLog[index];
                SendChatMsg(pl, $"\n\nRaid Event {index}:\n{JsonConvert.SerializeObject(raidEvent, Formatting.Indented)}");
            }
            else
                SendChatMsg(pl, $"Raid event {index} not found!");
        }

        [Command("rt.debug")]
        private void DebugCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer) return;

            _debug = !_debug;
            Puts($"debug: {_debug}");
        }

        [Command("rt.weapon_colors")]
        private void WeaponColorsCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null || !pl.IsAdmin) return;

            _sb.Clear();
            var category = args.Length > 0 && _config.trackers.ContainsKey(args[0].ToLower()) ? args[0].ToLower() : _config.trackers.Keys.First();
            foreach (var weaponCfg in _config.trackers[category])
            {
                var val = weaponCfg.Value;
                _sb.AppendLine($"<color={val.hexColor}>• {val.name} ({category})</color> - Enabled: {val.enabled}");
            }

            SendChatMsg(pl, $"\n\n{_sb}");
        }

        #endregion

        #region Helpers

        private void DrawRaidEvents(BasePlayer pl, IEnumerable<IGrouping<RaidFilter, RaidEvent>> groupedRaidsNearMe, string filterType, string filter, float radius, bool drawAttackerName = true)
        {
            bool verbose = _verboseMode.ContainsKey(pl.userID) && _verboseMode[pl.userID];
            int raidCount = groupedRaidsNearMe.Sum(x => x.Count());
            _sb.Clear();
            _sb.AppendLine(string.Format(lang.GetMessage("ViewEventsCommand.Header", this, pl.UserIDString), raidCount, radius));

            if (!string.IsNullOrEmpty(filterType))
                _sb.AppendLine(string.Format(lang.GetMessage("ViewEventsCommand.Filter", this, pl.UserIDString), filterType, filter));

            var groupCloseNames = new Dictionary<ulong, List<Vector3>>();
            foreach (var grouping in groupedRaidsNearMe)
            {
                var groupingColor = GetRandomColor();
                var groupingColorHex = GetHexColor(groupingColor);
                foreach (RaidEvent raidEvent in grouping)
                {
                    if (raidEvent.attackerTeamID > 0 && !_teamColors.ContainsKey(raidEvent.attackerTeamID))
                    {
                        _teamColors[raidEvent.attackerTeamID] = _uniqueColors[_currentTeamColorIdx];
                        _currentTeamColorIdx = _currentTeamColorIdx < _uniqueColors.Count() ? _currentTeamColorIdx : -1;
                        _currentTeamColorIdx++;
                    }

                    var teamColor = raidEvent.attackerTeamID > 0 ? _teamColors[raidEvent.attackerTeamID] : GetRandomColor();
                    var teamColorHex = GetHexColor(teamColor);
                    var weaponCfg = FindWeaponConfig(raidEvent.GetTrackerCategory(), raidEvent.GetPrimaryWeaponShortname());
                    var weaponColor = weaponCfg.GetWeaponColor();

                    if (filterType == "weapon")
                    {
                        groupingColor = weaponColor;
                        groupingColorHex = GetHexColor(groupingColor);
                    }
                    else if (filterType == "team")
                    {
                        groupingColor = teamColor;
                        groupingColorHex = GetHexColor(groupingColor);
                    }

                    string startText, endText;
                    var startPos = raidEvent.startPos;
                    var endPos = raidEvent.endPos;

                    if (weaponCfg.shortArrow)
                        startPos = raidEvent.endPos + new Vector3(0, .2f, 0);

                    string attackerTeam = raidEvent.attackerTeamID > 0 ? string.Format(lang.GetMessage("ViewEventsCommand.Team", this, pl.UserIDString), teamColorHex, raidEvent.attackerTeamID) : "";
                    if (verbose)
                    {
                        startText = string.Format(lang.GetMessage("ViewEventsCommand.StartTextExtra", this, pl.UserIDString), drawAttackerName ? raidEvent.attackerName : "X", raidEvent.attackerSteamID, attackerTeam);
                        endText = string.Format(lang.GetMessage("ViewEventsCommand.EndTextExtra", this, pl.UserIDString), groupingColorHex, raidEvent.GetIndex(), raidEvent.GetWeaponName(), raidEvent.hitEntity.Replace(raidEvent.GetEventType(), raidEvent.GetPrettyEventType()));
                    }
                    else
                    {
                        startText = string.Format(lang.GetMessage("ViewEventsCommand.StartText", this, pl.UserIDString), drawAttackerName ? raidEvent.attackerName : "X", attackerTeam);
                        endText = string.Format(lang.GetMessage("ViewEventsCommand.EndText", this, pl.UserIDString), groupingColorHex, raidEvent.GetWeaponName());
                    }

                    if (raidEvent.hitEntity.Contains("EVENT.ATTACHED"))
                        Box(pl, endPos, .05f, groupingColor, _config.drawDuration);
                    else if (raidEvent.hitEntity.Contains("EVENT.HIT"))
                        Sphere(pl, endPos, .05f, groupingColor, _config.drawDuration);

                    if (!groupCloseNames.ContainsKey(raidEvent.attackerSteamID))
                        groupCloseNames[raidEvent.attackerSteamID] = new List<Vector3>();

                    if (!groupCloseNames[raidEvent.attackerSteamID].Any(x => Vector3.Distance(x, startPos) < .1f))
                    {
                        groupCloseNames[raidEvent.attackerSteamID].Add(startPos);
                        Text(pl, startPos, startText, groupingColor, _config.drawDuration);
                    }

                    Arrow(pl, startPos, endPos, .05f, groupingColor, _config.drawDuration);
                    Text(pl, endPos + new Vector3(0, .05f, 0), endText, weaponColor, _config.drawDuration);
                }
                _sb.AppendLine(string.Format(lang.GetMessage("ViewEventsCommand.GroupingCount", this, pl.UserIDString), groupingColorHex, grouping.Count(), grouping.Key.filterType, grouping.Key.filter));

                var weaponCounts = grouping
                    .GroupBy(x => new { weapon = x.GetPrimaryWeaponShortname(), trackerCategory = x.GetTrackerCategory() })
                    .Select(x => new { data = x.Key, count = x.Count() });

                _sb.Append("<indent=6>");
                foreach (var x in weaponCounts)
                {
                    var weaponCfg = FindWeaponConfig(x.data.trackerCategory, x.data.weapon);
                    _sb.Append(string.Format(lang.GetMessage("ViewEventsCommand.WeaponCount", this, pl.UserIDString), weaponCfg.hexColor, x.count, weaponCfg.name, x.data.trackerCategory));
                }
                _sb.Append("<indent=0>");
                _sb.AppendLine($"\n");
            }

            if (raidCount < 1)
                _sb.AppendLine(lang.GetMessage("ViewEventsCommand.NotFound", this, pl.UserIDString));

            SendChatMsg(pl, _sb.ToString().TrimEnd(), "");
        }

        private void LogToSingleFile(string filename, string text) =>
            LogToFile(filename, string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}", DateTime.Now, text), this, false);

        private void PrintDebug(string msg)
        {
            if (_debug) Puts($"[DEBUG] {msg}");
        }

        private void SendChatMsg(BasePlayer pl, string msg, string prefix = null)
        {
            var p = prefix != null ? prefix : lang.GetMessage("ChatPrefix", this, pl.UserIDString);
            _rustPlayer.Message(pl, msg, p, _config.chatIconID, Array.Empty<object>());

            if (_config.printToClientConsole)
                pl.ConsoleMessage($"{(!string.IsNullOrEmpty(p) ? $"{p} " : "")}{msg}");
        }

        public void Arrow(BasePlayer player, Vector3 from, Vector3 to, float headSize, Color color, float duration) =>
            player.SendConsoleCommand("ddraw.arrow", duration, color, from, to, headSize);

        public void Sphere(BasePlayer player, Vector3 pos, float radius, Color color, float duration) =>
            player.SendConsoleCommand("ddraw.sphere", duration, color, pos, radius);

        public void Box(BasePlayer player, Vector3 pos, float size, Color color, float duration) =>
            player.SendConsoleCommand("ddraw.box", duration, color, pos, size);

        public void Text(BasePlayer player, Vector3 pos, string text, Color color, float duration) =>
            player.SendConsoleCommand("ddraw.text", duration, color, pos, text);

        private void AddPrefabToItem(string prefab, string itemShortname, string prefabSource)
        {
            var prefabShortname = GetPrefabShortname(prefab);
            if (_prefabToItem.ContainsKey(prefabShortname)) return;

            _prefabToItem[prefabShortname] = itemShortname;
            PrintDebug($"prefabToItem - {prefabSource}: {prefabShortname} -> {itemShortname}");
        }

        private string GetItemFromPrefabShortname(string prefabShortname) => 
            _prefabToItem.ContainsKey(prefabShortname) ? _prefabToItem[prefabShortname] : prefabShortname;

        private string GetPrefabShortname(string prefab) =>
            prefab.Substring(prefab.LastIndexOf('/') + 1).Replace(".prefab", "");

        private string GetPrettyItemName(string itemShortname)
        {
            string buildingBlockName;
            if (_buildingBlockPrettyNames.TryGetValue(itemShortname, out buildingBlockName))
                return buildingBlockName;

            return ItemManager.FindItemDefinition(itemShortname)?.displayName?.english ?? itemShortname;
        }

        private IEnumerable<RaidEvent> FindRaidEventsInSphere(Vector3 pos, float r) =>
            _raidEventLog.Where(x => Vector3.Distance(pos, x.startPos) < r || Vector3.Distance(pos, x.endPos) < r);

        private Color GetRandomColor() => UnityEngine.Random.ColorHSV(0f, 1f, .4f, .8f, .5f, 1f);

        private string GetHexColor(Color color) => $"#{ColorUtility.ToHtmlStringRGB(color)}";

        private PluginConfig.WeaponConfig AddOrFindWeaponConfig(string category, string shortname)
        {
            if (!_config.trackers.ContainsKey(category))
                _config.trackers[category] = new SortedDictionary<string, PluginConfig.WeaponConfig>();

            if (!_config.trackers[category].ContainsKey(shortname))
            {
                var weaponCfg = new PluginConfig.WeaponConfig {
                    enabled = _config.enableNewTrackers,
                    name = GetPrettyItemName(GetItemFromPrefabShortname(shortname)),
                    hexColor = GetHexColor(GetRandomColor())
                };
                _config.trackers[category][shortname] = weaponCfg;

                LogToSingleFile("weapon_config_log", $"added {weaponCfg.name} ({category} / {shortname}), enabled: {weaponCfg.enabled}");
                SaveConfig();
            }

            return FindWeaponConfig(category, shortname);
        }

        private PluginConfig.WeaponConfig FindWeaponConfig(string category, string shortname)
        {
            PluginConfig.WeaponConfig weaponCfg;
            if (_config.trackers.ContainsKey(category) && _config.trackers[category].TryGetValue(shortname, out weaponCfg))
            {
                if (_dev)
                {
                    return new PluginConfig.WeaponConfig {
                        enabled = true,
                        name = weaponCfg.name,
                        hexColor = weaponCfg.hexColor,
                        alwaysLog = weaponCfg.alwaysLog,
                        shortArrow = weaponCfg.shortArrow,
                        discordIcon = weaponCfg.discordIcon,
                        notifyConsole = true,
                        notifyAdmin = true,
                        notifyDiscord = true,
                        logToFile = true
                    };
                }

                PluginConfig.WeaponConfig globalWeaponCfg = null;
                if (_config.trackers.ContainsKey("_global") && _config.trackers["_global"].ContainsKey("*"))
                    globalWeaponCfg = _config.trackers["_global"]["*"];
                    
                if ((globalWeaponCfg == null || !globalWeaponCfg.enabled) && _config.trackers[category].ContainsKey("*"))
                    globalWeaponCfg = _config.trackers[category]["*"];

                if (!weaponCfg.enabled && globalWeaponCfg.enabled) {
                    return new PluginConfig.WeaponConfig {
                        enabled = globalWeaponCfg.enabled,
                        name = weaponCfg.name,
                        hexColor = weaponCfg.hexColor,
                        alwaysLog = weaponCfg.alwaysLog,
                        shortArrow = weaponCfg.shortArrow,
                        discordIcon = weaponCfg.discordIcon,
                        notifyConsole = globalWeaponCfg.notifyConsole,
                        notifyAdmin = globalWeaponCfg.notifyAdmin,
                        notifyDiscord = globalWeaponCfg.notifyDiscord,
                        logToFile = globalWeaponCfg.logToFile
                    };
                }
                return weaponCfg;
            }
            throw new Exception($"THIS SHOULD NEVER HAPPEN! Unable to find weapon config [{category} / {shortname}]");
        }

        private string GetDecayEntityShortname(DecayEntity entity)
        {
            var buildingBlock = entity as BuildingBlock;
            return $"{entity.ShortPrefabName}{(buildingBlock != null ? $".{buildingBlock.grade.ToString().ToLower()}" : "")}";
        }

        private bool IsDecayEntityIgnored(DecayEntity entity)
        {
            if (entity is LootContainer || entity.OwnerID == 0)
                return true;

            if (!_dev && _decayEntityIgnoreList.ContainsKey(entity.PrefabName) && _decayEntityIgnoreList[entity.PrefabName].ignore)
                return true;

            var buildingBlock = entity as BuildingBlock;
            bool ignoreGrade;
            if (!_dev && buildingBlock != null && _config.ignoreBuildingGrades.TryGetValue(buildingBlock.grade, out ignoreGrade) && ignoreGrade)
                return true;

            if (Convert.ToBoolean(RaidableBases?.Call("EventTerritory", entity.transform.position)))
                return true;

            if (Convert.ToBoolean(AbandonedBases?.Call("EventTerritory", entity.transform.position)))
                return true;

            return false;
        }

        private bool IsDecayEntityOrAttackerIgnored(DecayEntity entity, BasePlayer attacker)
        {
            if (IsDecayEntityIgnored(entity))
                return true;

            if (attacker != null && attacker.IsAdmin)
                return false;

            if (!_dev && _config.ignoreSameOwner && attacker != null && attacker.userID == entity.OwnerID)
                return true;

            if (!_dev && _config.ignoreTeamMember && attacker != null && attacker.Team != null && attacker.Team.members.Contains(entity.OwnerID))
                return true;

            if (!_dev && _config.ignoreClanMemberOrAlly && attacker != null && Convert.ToBoolean(Clans?.Call("IsMemberOrAlly", attacker.UserIDString, entity.OwnerID.ToString())))
                return true;

            return false;
        }

        private string FormatPosition(Vector3 pos)
        {
            return string.Format("{0:F1},{1:F1},{2:F1}", new object[] {
                pos.x,
                pos.y,
                pos.z
            });
        }

        private string StringReplaceKeys(string str, Dictionary<string, string> kv)
        {
            foreach (var x in kv)
                str = str.Replace($"{{{x.Key}}}", x.Value);
            return str;
        }

        private void SaveRaidEventLog()
        {
            if (_raidEventLog.Count == _raidEventLogCount) return;

            Interface.Oxide.DataFileSystem.WriteObject(_raidEventLogFilename, _raidEventLog);
            _raidEventLogCount = _raidEventLog.Count;
        }

        #endregion

        #region Config

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["ChatPrefix"] = $"<color=#00a7fe>[{Title}]</color>",
                ["RaidEvent.Message"] = "{attackerName}[{attackerSteamID}] is raiding {victimName}[{victimSteamID}] ~ {weaponName} -> {hitEntity} @ {gridPos} (teleportpos {teleportPos})",
                ["RaidEvent.PrettyMessage"] = "<color=#f5646c>{attackerName}[{attackerSteamID}]</color> is raiding <color=#52bf6f>{victimName}[{victimSteamID}]</color> ~ <color={weaponColor}>{weaponName}</color> {raidEventType} <color=#00a7fe>{entityItemName}</color> ({entityShortname}) @ <color=#00a7fe>{gridPos}</color>",
                ["ViewEventsCommand.HelpHeader"] = $"<size=16><color=#00a7fe>{Title}</color> Help</size>\n",
                ["ViewEventsCommand.HelpDefault"] = "<size=12><color=#00a7fe>/x <radius></color> - Show all raid events within X radius (default 50m)</size>",
                ["ViewEventsCommand.HelpExtraMode"] = "<size=12><color=#00a7fe>/x extra</color> - Toggle extra info mode</size>",
                ["ViewEventsCommand.HelpWipe"] = "<size=12><color=#00a7fe>/x wipe <radius></color> - Wipe all raid events within <radius></size>",
                ["ViewEventsCommand.HelpLast"] = "<size=12><color=#00a7fe>/x last</color> - Re-run last command</size>",
                ["ViewEventsCommand.HelpFilter"] = "<size=12><color=#00a7fe>/x <filterType> <filter> <radius></color></size>",
                ["ViewEventsCommand.HelpFilterTime"] = "<size=12><color=#00a7fe>/x time <hrs> <radius></color> - Show all raid events near by over the past <hrs> within <radius></size>",
                ["ViewEventsCommand.HelpFilterWeapon"] = "<size=12><color=#00a7fe>/x weapon <partial name or item name> <radius></color> - Show all raid events within <radius> filtered by weapon</size>",
                ["ViewEventsCommand.HelpFilterEntity"] = "<size=12><color=#00a7fe>/x entity <partial entity shortname> <radius></color> - Show all raid events within <radius> filtered by entity</size>",
                ["ViewEventsCommand.HelpFilterTeam"] = "<size=12><color=#00a7fe>/x team <team id> <radius></color> - Show all raid events within <radius> filtered by team</size>",
                ["ViewEventsCommand.HelpFilterPlayer"] = "<size=12><color=#00a7fe>/x player <steam id or partial name> <radius></color> - Show all raid events within <radius> filtered by player</size>",
                ["ViewEventsCommand.HelpPrintRaidEvent"] = "<size=12><color=#00a7fe>/re <event id></color> - Print info about a raid event by event id</size>",
                ["ViewEventsCommand.ExtraModeEnabled"] = "<color=#52bf6f>Extra info mode enabled</color>",
                ["ViewEventsCommand.ExtraModeDisabled"] = "<color=#f5646c>Extra info mode disabled</color>",
                ["ViewEventsCommand.WipePermission"] = "You do not have permission to wipe raid events!",
                ["ViewEventsCommand.NotFoundRadius"] = "No raid events found within <color=#00a7fe>{0}m</color>!",
                ["ViewEventsCommand.WipedRaidEventsRadius"] = "Wiped <color=#00a7fe>{0}</color> raid events within <color=#00a7fe>{1}m</color> at <color=#00a7fe>{2}</color>",
                ["ViewEventsCommand.Header"] = $"<size=16><color=#00a7fe>{Title}</color> ~ {{0}} raid event(s) within {{1}}m</size>\n",
                ["ViewEventsCommand.Filter"] = "<color=#00a7fe>filter:</color> [{0}, {1}]\n",
                ["ViewEventsCommand.Team"] = "<color={0}> T:{1}</color>",
                ["ViewEventsCommand.StartTextExtra"] = "<size=12>{0}[{1}]{2}</size>",
                ["ViewEventsCommand.EndTextExtra"] = "<size=12><color={0}>X</color> (RE:{1}) {2} <color={0}>~</color> {3}</size>",
                ["ViewEventsCommand.StartText"] = "<size=12>{0}{1}</size>",
                ["ViewEventsCommand.EndText"] = "<size=12><color={0}>X</color> {1}</size>",
                ["ViewEventsCommand.GroupingCount"] = "<color={0}>{1} raid event(s) [{2}, {3}]</color>",
                ["ViewEventsCommand.WeaponCount"] = "<color={0}>• {1}x {2} <size=10>({3})</size> </color>",
                ["ViewEventsCommand.NotFound"] = "no raid events found!",
                ["ViewEventsCommand.NoPermission"] = "You do not have permission to use <color=#00a7fe>{0}</color>!"
            }, this);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Log($"Loading default configuration");
            _config = GetDefaultConfig();
        }

        protected override void LoadConfig()
        {
            _instance = this;
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    var backupConfigFilename = $"{Name}\\ConfigBackup\\{Name}_{DateTime.Now:yyyy-M-dd_HH-mm-ss}";
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    LogWarning($"Saving configuration backup to /oxide/data/{Utility.CleanPath(backupConfigFilename)}.json");
                    Interface.Oxide.DataFileSystem.WriteObject(backupConfigFilename, _config);
                    SaveConfig();
                }
                else
                    Log("Configuration is up to date");

                var webhookURL = _config.discord.webhookURL;
                Puts($"Discord webhook {(!string.IsNullOrEmpty(webhookURL) && !webhookURL.Contains("Intro-to-Webhooks") ? "enabled" : "disabled")}!");
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                _isConfigValid = false;
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        private class PluginConfig : SerializableConfiguration
        {
            public bool debug = false;
            public ulong chatIconID = 76561199278762587;
            public bool deleteDataOnWipe = true;
            public float daysBeforeDelete = 7f;
            public float searchRadius = 50f;
            public float drawDuration = 30f;

            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<BuildingGrade.Enum, bool> ignoreBuildingGrades = new Dictionary<BuildingGrade.Enum, bool> {
                { BuildingGrade.Enum.Twigs, true },
                { BuildingGrade.Enum.Wood, false },
                { BuildingGrade.Enum.Stone, false },
                { BuildingGrade.Enum.Metal, false },
                { BuildingGrade.Enum.TopTier, false }
            };

            public bool ignoreSameOwner = true;
            public bool ignoreTeamMember = true;
            public bool ignoreClanMemberOrAlly = true;
            public bool enableNewTrackers = true;
            public bool printToClientConsole = true;
            public PlayerViewExplosionsCommand playerViewExplosionsCommand = new PlayerViewExplosionsCommand {
                drawAttackerName = false,
                ignoreRaidEventsLessThanMinutes = 30f
            };
            public NotificationCooldown notificationCooldown = new NotificationCooldown {
                enabled = false,
                cooldown = 300f
            };

            public DiscordConfig discord = new DiscordConfig {
                webhookURL = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                simpleMessage = new DiscordSimpleMessage {
                    enabled = false,
                    message = "{attackerName}[{attackerSteamID}] is raiding {victimName}[{victimSteamID}] ~ {weaponName} -> {raidEventType} {entityItemName} ({entityShortname}) @ {gridPos} (teleportpos {teleportPos})"
                },
                embed = new DiscordEmbed {
                    title = "{attackerName} is raiding {victimName} @ {gridPos}",
                    thumbnail = new DiscordEmbedThumbnail {
                        url = "https://www.rustedit.io/images/imagelibrary/{weaponItemShortname}.png"
                    },
                    fields = new List<DiscordEmbedField> {
                        {
                            new DiscordEmbedField {
                                name = "Weapon",
                                value = "{weaponName} ({raidTrackerCategory} / {weaponShortname})",
                                inline = false
                            }
                        },
                        {
                            new DiscordEmbedField {
                                name = "Entity",
                                value = "{raidEventType} {entityItemName} ({entityShortname})",
                                inline = false
                            }
                        },
                        {
                            new DiscordEmbedField {
                                name = "Attacker",
                                value = "{attackerName} \n[Steam Profile](https://steamcommunity.com/profiles/{attackerSteamID}) ({attackerSteamID})\n[SteamID.uk](https://steamid.uk/profile/{attackerSteamID})\n\n**Attacker Team**\n{attackerTeamName}",
                                inline = true
                            }
                        },
                        {
                            new DiscordEmbedField {
                                name = "Victim",
                                value = "{victimName} \n[Steam Profile](https://steamcommunity.com/profiles/{victimSteamID}) ({victimSteamID})\n[SteamID.uk](https://steamid.uk/profile/{victimSteamID})\n\n**Victim Team**\n{victimTeamName}",
                                inline = true
                            }
                        },
                        {
                            new DiscordEmbedField {
                                name = "Location",
                                value = "{gridPos} - teleportpos {teleportPos}",
                                inline = false
                            }
                        }
                    },
                    footer = new DiscordEmbedFooter {
                        text = $"{_instance.Title} {{{0}}} by {_instance.Author}",
                        icon_url = "https://i.imgur.com/DluJ5X5.png"
                    }
                }
            };

            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SortedDictionary<string, SortedDictionary<string, WeaponConfig>> trackers = new SortedDictionary<string, SortedDictionary<string, WeaponConfig>> {
                {
                    "_global",
                    new SortedDictionary<string, WeaponConfig> {
                        { "*", new WeaponConfig { enabled = false, name = "Enable all trackers in every category" } }
                    }
                },
                {
                    "entity_collision",
                    new SortedDictionary<string, WeaponConfig> {
                        { "*", new WeaponConfig { enabled = false, name = "Enable all 'entity_collision' trackers" } },
                        { "40mm_grenade_he", new WeaponConfig { enabled = true, hexColor = "#FF5764" } },
                        { "40mm_grenade_smoke", new WeaponConfig { enabled = false, hexColor = "#4B4B4B" } },
                        { "explosive.satchel.deployed", new WeaponConfig { enabled = true, hexColor = "#BA9500" } },
                        { "explosive.timed.deployed", new WeaponConfig { enabled = true, name = "C4", hexColor = "#FF5764" } },
                        { "grenade.beancan.deployed", new WeaponConfig { enabled = false, hexColor = "#BA9500" } },
                        { "grenade.f1.deployed", new WeaponConfig { enabled = false, hexColor = "#538C4F" } },
                        { "grenade.flashbang.deployed", new WeaponConfig { enabled = false, hexColor = "#4B4B4B" } },
                        { "grenade.molotov.deployed", new WeaponConfig { enabled = true, hexColor = "#FF8C24" } },
                        { "grenade.smoke.deployed", new WeaponConfig { enabled = false, hexColor = "#4B4B4B" } },
                        { "grenade.supplysignal.deployed", new WeaponConfig { enabled = true, hexColor = "#B867FF", alwaysLog = true, shortArrow = true } },
                        { "rocket_basic", new WeaponConfig { enabled = true, hexColor = "#B867FF" } },
                        { "rocket_fire", new WeaponConfig { enabled = true, hexColor = "#FF8C24" } },
                        { "rocket_hv", new WeaponConfig { enabled = true, hexColor = "#528EFF" } },
                        { "rocket_mlrs", new WeaponConfig { enabled = true, hexColor = "#FF5764", alwaysLog = true } },
                        { "survey_charge.deployed", new WeaponConfig { enabled = false, hexColor = "#212121" } }
                    }
                },
                {
                    "entity_death_ammo",
                    new SortedDictionary<string, WeaponConfig> {
                        { "*", new WeaponConfig { enabled = false, name = "Enable all 'entity_death_ammo' trackers" } },
                        { "ammo.grenadelauncher.buckshot", new WeaponConfig { enabled = true, hexColor = "#FF5764" } },
                        { "ammo.handmade.shell", new WeaponConfig { enabled = true, hexColor = "#FFC880" } },
                        { "ammo.nailgun.nails", new WeaponConfig { enabled = true, hexColor = "#528EFF" } },
                        { "ammo.pistol", new WeaponConfig { enabled = true, name = "Pistol Ammo", hexColor = "#FFC880" } },
                        { "ammo.pistol.fire", new WeaponConfig { enabled = true, name = "Inc Pistol Ammo", hexColor = "#FF8C24" } },
                        { "ammo.pistol.hv", new WeaponConfig { enabled = true, name = "HV Pistol Ammo", hexColor = "#528EFF" } },
                        { "ammo.rifle", new WeaponConfig { enabled = true, name = "Rifle Ammo", hexColor = "#FFC880" } },
                        { "ammo.rifle.explosive", new WeaponConfig { enabled = true, name = "Exp Rifle Ammo", hexColor = "#FF5764" } },
                        { "ammo.rifle.hv", new WeaponConfig { enabled = true, name = "HV Rifle Ammo", hexColor = "#528EFF" } },
                        { "ammo.rifle.incendiary", new WeaponConfig { enabled = true, name = "Inc Rifle Ammo", hexColor = "#FF8C24" } },
                        { "ammo.shotgun", new WeaponConfig { enabled = true, hexColor = "#FF5764" } },
                        { "ammo.shotgun.fire", new WeaponConfig { enabled = true, name = "12 Gauge Inc Shell", hexColor = "#FF8C24" } },
                        { "ammo.shotgun.slug", new WeaponConfig { enabled = true, hexColor = "#3DBF39" } },
                        { "ammo.snowballgun", new WeaponConfig { enabled = true, name = "Snowball", hexColor = "#3E8E91" } },
                        { "arrow.bone", new WeaponConfig { enabled = true, hexColor = "#212121" } },
                        { "arrow.fire", new WeaponConfig { enabled = true, hexColor = "#FF8C24" } },
                        { "arrow.hv", new WeaponConfig { enabled = true, hexColor = "#528EFF" } },
                        { "arrow.wooden", new WeaponConfig { enabled = true, hexColor = "#FFC880" } },
                        { "speargun.spear", new WeaponConfig { enabled = false, hexColor = "#7E7E7E" } }
                    }
                },
                {
                    "entity_death_fire",
                    new SortedDictionary<string, WeaponConfig> {
                        { "*", new WeaponConfig { enabled = false, name = "Enable all 'entity_death_fire' trackers" } },
                        { "fire_damage", new WeaponConfig { enabled = true, name = "Fire", hexColor = "#FF8C24", discordIcon = "https://i.imgur.com/dBqgQv9.png" } },
                        { "fireball", new WeaponConfig { enabled = true, name = "Fireball", hexColor = "#FF8C24", discordIcon = "https://i.imgur.com/dBqgQv9.png" } },
                        { "fireball_small", new WeaponConfig { enabled = true, name = "Small Fireball", hexColor = "#FF8C24", discordIcon = "https://i.imgur.com/dBqgQv9.png" } },
                        { "fireball_small_arrow", new WeaponConfig { enabled = true, name = "Arrow Fireball", hexColor = "#FF8C24", discordIcon = "https://i.imgur.com/dBqgQv9.png" } },
                        { "fireball_small_shotgun", new WeaponConfig { enabled = true, name = "Shotgun Fireball", hexColor = "#FF8C24", discordIcon = "https://i.imgur.com/dBqgQv9.png" } },
                        { "flameturret_fireball", new WeaponConfig { enabled = true, name = "Flame Turret Fireball", hexColor = "#FF8C24", discordIcon = "https://i.imgur.com/dBqgQv9.png" } }
                    }
                },
                {
                    "entity_death_weapon",
                    new SortedDictionary<string, WeaponConfig> {
                        { "*", new WeaponConfig { enabled = false, name = "Enable all 'entity_death_weapon' trackers" } },
                        { "ammo.grenadelauncher.he", new WeaponConfig { enabled = false, hexColor = "#FF5764", shortArrow = true } },
                        { "ammo.rocket.basic", new WeaponConfig { enabled = false, hexColor = "#B867FF", shortArrow = true } },
                        { "ammo.rocket.fire", new WeaponConfig { enabled = false, hexColor = "#FF8C24", shortArrow = true } },
                        { "ammo.rocket.hv", new WeaponConfig { enabled = false, hexColor = "#528EFF", shortArrow = true } },
                        { "ammo.rocket.mlrs", new WeaponConfig { enabled = false, hexColor = "#FF5764", shortArrow = true } },
                        { "axe.salvaged", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "bone.club", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "bow.compound", new WeaponConfig { enabled = false, hexColor = "#B867FF" } },
                        { "bow.hunting", new WeaponConfig { enabled = false, hexColor = "#B867FF" } },
                        { "candycaneclub", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "chainsaw", new WeaponConfig { enabled = true, hexColor = "#FF5764" } },
                        { "concretehatchet", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "concretepickaxe", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "crossbow", new WeaponConfig { enabled = false, hexColor = "#B867FF" } },
                        { "explosive.satchel", new WeaponConfig { enabled = false, hexColor = "#BA9500", shortArrow = true } },
                        { "explosive.timed", new WeaponConfig { enabled = false, name = "C4", hexColor = "#FF5764", shortArrow = true } },
                        { "flamethrower", new WeaponConfig { enabled = true, hexColor = "#FF8C24" } },
                        { "flashlight.held", new WeaponConfig { enabled = true, hexColor = "#212121" } },
                        { "grenade.beancan", new WeaponConfig { enabled = true, hexColor = "#BA9500", shortArrow = true } },
                        { "grenade.f1", new WeaponConfig { enabled = true, hexColor = "#538C4F", shortArrow = true } },
                        { "grenade.flashbang", new WeaponConfig { enabled = true, hexColor = "#4B4B4B", shortArrow = true } },
                        { "grenade.molotov", new WeaponConfig { enabled = true, hexColor = "#FF8C24", shortArrow = true } },
                        { "hammer.salvaged", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "hatchet", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "hmlmg", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "icepick.salvaged", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "jackhammer", new WeaponConfig { enabled = true, hexColor = "#FF5764" } },
                        { "knife.bone", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "knife.butcher", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "knife.combat", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "lmg.m249", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "longsword", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "lumberjack.hatchet", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "lumberjack.pickaxe", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "mace", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "mace.baseballbat", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "machete", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "multiplegrenadelauncher", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "paddle", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "pickaxe", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "pistol.eoka", new WeaponConfig { enabled = true, hexColor = "#3E8E91" } },
                        { "pistol.m92", new WeaponConfig { enabled = false, hexColor = "#3E8E91" } },
                        { "pistol.nailgun", new WeaponConfig { enabled = true, hexColor = "#3E8E91" } },
                        { "pistol.prototype17", new WeaponConfig { enabled = true, hexColor = "#3E8E91" } },
                        { "pistol.python", new WeaponConfig { enabled = false, hexColor = "#3E8E91" } },
                        { "pistol.revolver", new WeaponConfig { enabled = false, hexColor = "#3E8E91" } },
                        { "pistol.semiauto", new WeaponConfig { enabled = false, hexColor = "#3E8E91" } },
                        { "pitchfork", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "rifle.ak", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "rifle.ak.ice", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "rifle.bolt", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "rifle.l96", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "rifle.lr300", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "rifle.m39", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "rifle.semiauto", new WeaponConfig { enabled = false, hexColor = "#FF5764" } },
                        { "rock", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "salvaged.cleaver", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "salvaged.sword", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "shotgun.double", new WeaponConfig { enabled = false, hexColor = "#3DBF39" } },
                        { "shotgun.pump", new WeaponConfig { enabled = false, hexColor = "#3DBF39" } },
                        { "shotgun.spas12", new WeaponConfig { enabled = false, hexColor = "#3DBF39" } },
                        { "shotgun.waterpipe", new WeaponConfig { enabled = false, hexColor = "#3DBF39" } },
                        { "sickle", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "smg.2", new WeaponConfig { enabled = false, hexColor = "#365EEF" } },
                        { "smg.mp5", new WeaponConfig { enabled = false, hexColor = "#365EEF" } },
                        { "smg.thompson", new WeaponConfig { enabled = false, hexColor = "#365EEF" } },
                        { "snowballgun", new WeaponConfig { enabled = false, hexColor = "#3E8E91" } },
                        { "spear.stone", new WeaponConfig { enabled = true, hexColor = "#CB60DB" } },
                        { "spear.wooden", new WeaponConfig { enabled = true, hexColor = "#CB60DB" } },
                        { "speargun", new WeaponConfig { enabled = true, hexColor = "#CB60DB" } },
                        { "stone.pickaxe", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "stonehatchet", new WeaponConfig { enabled = true, hexColor = "#4DBEFF" } },
                        { "surveycharge", new WeaponConfig { enabled = false, hexColor = "#212121", shortArrow = true } },
                        { "torch", new WeaponConfig { enabled = true, hexColor = "#FF8C24" } },
                        { "torch.torch.skull", new WeaponConfig { enabled = true, hexColor = "#FF8C24" } }
                    }
                }
            };

            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SortedDictionary<string, string> eventTypes = new SortedDictionary<string, string>{
                { "EVENT.ATTACHED", "attached to"},
                { "EVENT.BURNT", "burnt" },
                { "EVENT.DESTROYED", "destroyed" },
                { "EVENT.HIT", "hit" },
                { "EVENT.NO_HIT", "no hit" }
            };

            public class WeaponConfig
            {
                public bool enabled;
                public string name;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
                public string hexColor;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
                public bool alwaysLog;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
                public bool shortArrow;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
                public string discordIcon;

                public bool notifyConsole;
                public bool notifyAdmin;
                public bool notifyDiscord;
                public bool logToFile;

                public Color GetWeaponColor()
                {
                    Color weaponColor;
                    if (ColorUtility.TryParseHtmlString(this.hexColor, out weaponColor))
                        return weaponColor;
                    return _instance.GetRandomColor();
                }
            }

            public class DiscordConfig
            {
                public string webhookURL;
                public DiscordSimpleMessage simpleMessage;
                public DiscordEmbed embed = new DiscordEmbed();
            }

            public class DiscordSimpleMessage
            {
                public bool enabled;
                public string message;
            }

            public class DiscordEmbed
            {
                public string title;
                public DiscordEmbedThumbnail thumbnail = new DiscordEmbedThumbnail();

                [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<DiscordEmbedField> fields = new List<DiscordEmbedField>();

                public DiscordEmbedFooter footer = new DiscordEmbedFooter();
            }

            public class DiscordEmbedThumbnail
            {
                public string url;
            }

            public class DiscordEmbedField
            {
                public string name;
                public string value;
                public bool inline;
            }

            public class DiscordEmbedFooter
            {
                public string text;
                public string icon_url;
            }

            public class PlayerViewExplosionsCommand
            {
                public bool drawAttackerName;
                public float ignoreRaidEventsLessThanMinutes;
            }

            public class NotificationCooldown
            {
                public bool enabled;
                public float cooldown;
            }
        }

        public class DecayEntityIgnoreOptions
        {
            public string name;
            public bool ignore;
            public bool ignoreDiscord;
        }

        #endregion

        #region Config Helpers

        private class SerializableConfiguration
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
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

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

        #region Classes

        private class DiscordWebhookManager
        {
            private float _timeout = 200f;
            private float _busy = Time.realtimeSinceStartup;
            private bool _rateLimited = false;
            private Queue<RaidEvent> _queue = new Queue<RaidEvent>();
            private readonly Dictionary<string, string> _headers = new Dictionary<string, string> {
                ["Content-Type"] = "application/json"
            };

            public void Enqueue(RaidEvent raidEvent) => _queue.Enqueue(raidEvent);

            private void SendNextRequest()
            {
                if (_queue.Count == 0)
                    return;

                SendWebhook(_queue.Dequeue());
            }

            public void SendWebhook(RaidEvent raidEvent)
            {
                var url = _instance._config.discord.webhookURL;
                if (string.IsNullOrEmpty(url) || url.Contains("Intro-to-Webhooks"))
                    return;

                var timeSinceStartup = Time.realtimeSinceStartup;
                if (_busy > timeSinceStartup || _rateLimited)
                {
                   _instance.PrintDebug($"(RaidEvent: {raidEvent.GetIndex()}) Discord SendWebhook QUEUE busy: {_busy > timeSinceStartup}, rateLimited: {_rateLimited}");

                    Enqueue(raidEvent);
                    return;
                }

                _busy = timeSinceStartup + _timeout;

                string gridPos = PhoneController.PositionToGridCoord(raidEvent.endPos);
                string entityShortname = raidEvent.GetHitEntityShortname();
                string entityItemShortname = _instance.GetItemFromPrefabShortname(entityShortname);
                string weaponShortname = raidEvent.GetPrimaryWeaponShortname();
                string weaponItemShortname = _instance.GetItemFromPrefabShortname(weaponShortname);
                var weaponCfg = _instance.FindWeaponConfig(raidEvent.GetTrackerCategory(), weaponShortname);

                var attackerTeam = RelationshipManager.ServerInstance.FindTeam(raidEvent.attackerTeamID);
                string attackerTeamName = attackerTeam != null ? $"{attackerTeam.GetLeader()?.displayName ?? "Unknown Leader"}'s Team (ID: {raidEvent.attackerTeamID})" : "No Team";

                BasePlayer victim = RelationshipManager.FindByID(raidEvent.victimSteamID);
                string victimName = victim?.displayName ?? "Unknown";
                ulong victimTeamID = victim?.Team != null ? victim.Team.teamID : 0;
                string victimTeamName = victim?.Team != null ? $"{victim.Team.GetLeader()?.displayName ?? "Unknown Leader"}'s Team (ID: {victimTeamID})" : "No Team";

                if (victim == null)
                {
                    IPlayer victim2 = _instance.covalence.Players.FindPlayerById(raidEvent.victimSteamID.ToString());
                    victimName = victim2?.Name ?? "Unknown";
                }

                Dictionary<string, string> keyValues = new Dictionary<string, string> {
                    { "attackerName", raidEvent.attackerName },
                    { "attackerSteamID", raidEvent.attackerSteamID.ToString() },
                    { "attackerTeamName", attackerTeamName },
                    { "victimName", victimName },
                    { "victimSteamID", raidEvent.victimSteamID.ToString() },
                    { "victimTeamName", victimTeamName },
                    { "weaponName", raidEvent.GetWeaponName() },
                    { "weaponShortname", weaponShortname },
                    { "weaponItemShortname", weaponItemShortname },
                    { "entityItemName", _instance.GetPrettyItemName(entityItemShortname) },
                    { "entityShortname", entityShortname },
                    { "entityItemShortname", entityItemShortname },
                    { "raidEventIndex", raidEvent.GetIndex().ToString() },
                    { "raidTrackerCategory", raidEvent.GetTrackerCategory() },
                    { "raidEventType", raidEvent.GetPrettyEventType() },
                    { "gridPos", gridPos },
                    { "teleportPos", _instance.FormatPosition(raidEvent.endPos) }
                };

                object payload;
                if (_instance._config.discord.simpleMessage.enabled)
                {
                    payload = new {
                        content = _instance.StringReplaceKeys(_instance._config.discord.simpleMessage.message, keyValues)
                    };
                }
                else
                {
                    var fields = _instance._config.discord.embed.fields
                        .Select(x => new {
                            name = _instance.StringReplaceKeys(x.name, keyValues),
                            value = _instance.StringReplaceKeys(x.value, keyValues),
                            inline = x.inline
                        })
                        .ToArray();

                    int decimalColor;
                    if (!int.TryParse(weaponCfg.hexColor.Replace("#", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out decimalColor))
                        decimalColor = 5548284;

                    payload = new {
                        embeds = new[] {
                            new {
                                title = _instance.StringReplaceKeys(_instance._config.discord.embed.title, keyValues),
                                thumbnail = new {
                                    url = !string.IsNullOrEmpty(weaponCfg.discordIcon) ? weaponCfg.discordIcon : _instance.StringReplaceKeys(_instance._config.discord.embed.thumbnail.url, keyValues)
                                },
                                fields = fields,
                                footer = new {
                                    text = string.Format(_instance._config.discord.embed.footer.text ?? "", _instance.Version),
                                    icon_url = _instance._config.discord.embed.footer.icon_url ?? ""
                                },
                                color = decimalColor,
                                timestamp = raidEvent.timestamp
                            }
                        }
                    };
                }

                _instance.webrequest.Enqueue(url, JsonConvert.SerializeObject(payload), (code, response) => {
                    var reIdx = raidEvent.GetIndex();
                    _instance.PrintDebug($"(RaidEvent: {reIdx}) Discord SendWebhook [{code}] {response}");

                    if (code == 429 && response.Length > 0)
                    {
                        JObject jsonRes = JObject.Parse(response);
                        float retryAfter = Math.Max(1f, jsonRes["retry_after"].Value<int>() / 1000f);

                        _instance.PrintDebug($"(RaidEvent: {reIdx}) Discord SendWebhook rate limited [retry_after: {retryAfter}s]");

                        _rateLimited = true;
                        _instance.timer.In(retryAfter, () => {
                            _rateLimited = false;
                            _busy = Time.realtimeSinceStartup;
                            SendWebhook(raidEvent);
                        });
                    }
                    else if (code != 200 && code != 204)
                    {
                        _busy = Time.realtimeSinceStartup;
                        _instance.Puts($"(RaidEvent: {reIdx}) Discord SendWebhook failed! Response: [{code}]\n{response}");
                    }
                    else
                    {
                        _instance.timer.In(2f, () => { //discord webhooks are limited to 30 req/min, delay requests to reduce rate limit chance
                            _busy = Time.realtimeSinceStartup;
                            SendNextRequest();
                        });
                    }
                }, _instance, Core.Libraries.RequestMethod.POST, _headers, _timeout);
            }
        }

        public class Vector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value.ToString());
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                string s = (string)reader.Value;
                string[] temp = s.Substring(1, s.Length - 2).Split(',');
                return new Vector3(float.Parse(temp[0]), float.Parse(temp[1]), float.Parse(temp[2]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }

        private class RaidEvent
        {
            public string attackerName;
            public ulong attackerSteamID;
            public ulong attackerTeamID;
            public ulong victimSteamID;
            public string weapon; // "primaryWeapon[tracker_category]" OR "primaryWeapon[tracker_category];secondaryWeapon[tracker_category]"
            public string hitEntity; // "event_type entity_name"
            [JsonConverter(typeof(Vector3Converter))]
            public Vector3 startPos;
            [JsonConverter(typeof(Vector3Converter))]
            public Vector3 endPos;
            public DateTime timestamp;

            public int GetIndex()
            {
                return _instance._raidEventLog.IndexOf(this);
            }

            public string GetPrimaryWeapon()
            {
                if (weapon.Contains(";"))
                    return weapon.Substring(0, weapon.LastIndexOf(';'));
                return weapon;
            }

            public string GetPrimaryWeaponShortname()
            {
                return GetWeaponShortname(GetPrimaryWeapon());
            }

            public string GetSecondaryWeapon()
            {
                if (weapon.Contains(";"))
                    return weapon.Substring(weapon.LastIndexOf(';') + 1);
                return null;
            }

            public string GetSecondaryWeaponShortname()
            {
                return GetWeaponShortname(GetSecondaryWeapon());
            }

            private string GetWeaponShortname(string w)
            {
                return w.Substring(0, w.LastIndexOf('['));
            }

            public string GetTrackerCategory()
            {
                if (weapon.Contains(";"))
                    return GetTrackerCategory(GetPrimaryWeapon());
                return GetTrackerCategory(weapon);
            }

            private string GetTrackerCategory(string w)
            {
                var frm = w.IndexOf('[') + 1;
                return w.Substring(frm, w.LastIndexOf(']') - frm);
            }

            public string GetWeaponName()
            {
                if (weapon.Contains(";"))
                    return $"{_instance.FindWeaponConfig(GetTrackerCategory(), GetPrimaryWeaponShortname()).name} [{_instance.FindWeaponConfig(GetTrackerCategory(GetSecondaryWeapon()), GetSecondaryWeaponShortname()).name}]";
                return _instance.FindWeaponConfig(GetTrackerCategory(), GetPrimaryWeaponShortname()).name;
            }

            public string GetEventType()
            {
                return hitEntity.Substring(0, hitEntity.LastIndexOf(' '));
            }

            public string GetPrettyEventType()
            {
                var evt = GetEventType();
                return _instance._config.eventTypes.ContainsKey(evt) ? _instance._config.eventTypes[evt] : evt;
            }

            public string GetHitEntityShortname()
            {
                return hitEntity.Substring(hitEntity.LastIndexOf(' ') + 1);
            }

            public string GetMessage() 
            {
                IPlayer victim = _instance.covalence.Players.FindPlayerById(victimSteamID.ToString());
                return _instance.StringReplaceKeys(
                    _instance.lang.GetMessage("RaidEvent.Message", _instance),
                    new Dictionary<string, string> {
                        { "raidEventIndex", GetIndex().ToString() },
                        { "attackerName", attackerName },
                        { "attackerSteamID", attackerSteamID.ToString() },
                        { "victimName", victim?.Name ?? "Unknown" },
                        { "victimSteamID", victimSteamID.ToString() },
                        { "weaponName", GetWeaponName() },
                        { "hitEntity", hitEntity.Replace(GetEventType(), GetPrettyEventType()) },
                        { "gridPos", PhoneController.PositionToGridCoord(endPos) },
                        { "teleportPos", _instance.FormatPosition(endPos) }
                    });
            }

            public string GetPrettyMessage(BasePlayer pl)
            {
                var weaponCfg = _instance.FindWeaponConfig(GetTrackerCategory(), GetPrimaryWeaponShortname());
                IPlayer victim = _instance.covalence.Players.FindPlayerById(victimSteamID.ToString());
                string entityShortname = GetHitEntityShortname();
                string entityItemShortname = _instance.GetItemFromPrefabShortname(entityShortname);
                string entityItemName = _instance.GetPrettyItemName(entityItemShortname);
                return _instance.StringReplaceKeys(
                    _instance.lang.GetMessage("RaidEvent.PrettyMessage", _instance, pl?.UserIDString),
                    new Dictionary<string, string> {
                        { "raidEventIndex", GetIndex().ToString() },
                        { "attackerName", attackerName },
                        { "attackerSteamID", attackerSteamID.ToString() },
                        { "victimName", victim?.Name ?? "Unknown" },
                        { "victimSteamID", victimSteamID.ToString() },
                        { "weaponName", GetWeaponName() },
                        { "weaponColor", weaponCfg.hexColor },
                        { "raidEventType", GetPrettyEventType() },
                        { "entityItemName", entityItemName },
                        { "entityShortname", entityShortname },
                        { "gridPos", PhoneController.PositionToGridCoord(endPos) },
                        { "teleportPos", _instance.FormatPosition(endPos) }
                    });
            }

            public void Notify(BaseEntity entity, string damagedEntityPrefab = null)
            {
                var weaponCfg = _instance.FindWeaponConfig(GetTrackerCategory(), GetPrimaryWeaponShortname());
                if (weaponCfg.logToFile || _instance._dev)
                    _instance.LogToFile("raid_events", GetMessage(), _instance);

                if (_instance._config.notificationCooldown.enabled && !_instance._dev)
                {
                    float cooldown;
                    if (_instance._notificationCooldown.TryGetValue(attackerSteamID, out cooldown) && cooldown > Time.realtimeSinceStartup)
                        return;

                    _instance._notificationCooldown[attackerSteamID] = Time.realtimeSinceStartup + _instance._config.notificationCooldown.cooldown;
                }
                
                if (weaponCfg.notifyConsole)
                    _instance.Puts(GetMessage());

                if (weaponCfg.notifyAdmin || _instance._dev)
                    NotifyAdmin();
                
                if (weaponCfg.notifyDiscord || _instance._dev)
                {
                    if (string.IsNullOrEmpty(damagedEntityPrefab) && entity != null)
                        damagedEntityPrefab = entity.PrefabName;

                    DecayEntityIgnoreOptions decayEntOptions;
                    if (!_instance._dev && _instance._decayEntityIgnoreList.TryGetValue(damagedEntityPrefab, out decayEntOptions) && decayEntOptions.ignoreDiscord)
                        return;

                    _instance._discordWebhookManager.SendWebhook(this);
                }

                if (_instance._debug && entity != null)
                    DebugNotify(entity);
            }

            private void NotifyAdmin()
            {
                foreach (var adminPlayer in BasePlayer.activePlayerList)
                {
                    if (adminPlayer == null || !adminPlayer.IsAdmin || !adminPlayer.IsAlive()) continue;
                    _instance.SendChatMsg(adminPlayer, GetPrettyMessage(adminPlayer));
                }
            }

            private void DebugNotify(BaseEntity entity)
            {
                string msg = GetMessage();
                foreach (var adminPlayer in BasePlayer.activePlayerList)
                {
                    if (adminPlayer == null || !adminPlayer.IsAdmin || !adminPlayer.IsAlive()) continue;

                    _instance.Arrow(adminPlayer, endPos, endPos + entity.transform.forward, 0.05f, Color.magenta, _instance._debugDrawDuration);
                    _instance.Sphere(adminPlayer, endPos, (entity as TimedExplosive)?.explosionRadius ?? 2f, Color.cyan, _instance._debugDrawDuration);

                    _instance.Sphere(adminPlayer, endPos, 0.05f, Color.red, _instance._debugDrawDuration);
                    _instance.Text(adminPlayer, endPos, $"<size=14>[DEBUG] RAID DETECTED\n{msg}</size>", Color.magenta, _instance._debugDrawDuration);
                }

                _instance.PrintDebug($"RAID: {msg}");
            }
        }

        private class RaidFilter
        {
            public string filter;
            public string filterType;

            public override bool Equals(object other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                if (this.GetType() != other.GetType()) return false;
                return Equals((RaidFilter)other);
            }

            public override int GetHashCode()
            {
                return filter.GetHashCode() + filterType.GetHashCode();
            }

            public bool Equals(RaidFilter other)
            {
                return other.filter == filter && other.filterType == filterType;
            }

            public static bool operator ==(RaidFilter lhs, RaidFilter rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(RaidFilter lhs, RaidFilter rhs)
            {
                return !lhs.Equals(rhs);
            }
        }

        public class ExplosiveTracker : MonoBehaviour
        {
            private float startTime;
            private double millisecondsTaken;

            private string hitEntityPrefab;
            private BaseEntity explosiveEntity;
            private DecayEntity parentEntity;
            private BasePlayer attacker;
            private Vector3 position;
            private RaidEvent raidEvent;

            public string trackerCategory;
            public bool logEvent = true;

            public void Init(string category)
            {
                explosiveEntity = GetComponent<BaseEntity>();
                attacker = explosiveEntity?.creatorEntity as BasePlayer;

                if (explosiveEntity == null || attacker == null || attacker.IsNpc)
                {
                    logEvent = false;
                    Destroy(this);
                    return;
                }

                trackerCategory = category;
                position = explosiveEntity.transform.position;
                if (_instance._debug)
                    startTime = Time.realtimeSinceStartup;

                var startPos = attacker.transform.position;
                    startPos.y += attacker.GetHeight() - .5f;

                raidEvent = new RaidEvent {
                    attackerName = attacker?.displayName ?? "Unknown",
                    attackerSteamID = attacker?.userID ?? 0,
                    attackerTeamID = attacker?.Team?.teamID ?? 0,
                    victimSteamID = 0,
                    weapon = $"{explosiveEntity.ShortPrefabName}[{trackerCategory}]",
                    startPos = startPos,
                    endPos = Vector3.zero,
                    timestamp = DateTime.Now
                };
            }

            private void FixedUpdate()
            {
                if (explosiveEntity == null)
                {
                    logEvent = false;
                    Destroy(this);
                    return;
                }

                var tick = DateTime.Now;
                var dudTimedExp = explosiveEntity as DudTimedExplosive;
                if (dudTimedExp != null)
                {
                    // ignore logging if explosive is dud
                    logEvent = dudTimedExp.HasFlag(BaseEntity.Flags.On);
                    if (!logEvent) return;
                }

                var newPos = explosiveEntity.transform.position;
                if (parentEntity != null)
                {
                    if (string.IsNullOrEmpty(raidEvent.hitEntity))
                    {
                        if (_instance.IsDecayEntityOrAttackerIgnored(parentEntity, attacker))
                        {
                            logEvent = false;
                            Destroy(this);
                            return;
                        }

                        var entityShortname = _instance.GetDecayEntityShortname(parentEntity);
                        hitEntityPrefab = parentEntity.PrefabName;
                        raidEvent.hitEntity = $"EVENT.ATTACHED {entityShortname}";
                        raidEvent.victimSteamID = parentEntity.OwnerID;

                       _instance.PrintDebug($"{explosiveEntity.ShortPrefabName} ATTACHED TO PARENT {entityShortname}[{parentEntity?.net?.ID.Value ?? 0}]");
                    }
                }
                else
                    parentEntity = explosiveEntity.GetParentEntity() as DecayEntity;

                if (newPos != position && Vector3.Distance(newPos, Vector3.zero) > 5f)
                    position = newPos;

                if (_instance._debug)
                    millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;
            }

            private void OnDestroy()
            {
                if (!logEvent) return;

                var tick = DateTime.Now;

                // not parented to an entity, check for collisions
                if (string.IsNullOrEmpty(raidEvent.hitEntity)) 
                {
                    Ray ray = new Ray(position, explosiveEntity.transform.forward);
                    float radius = (explosiveEntity as TimedExplosive)?.explosionRadius ?? 2f;
                    float maxDistance = 1f;
                    List<RaycastHit> hits = Facepunch.Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAllUnordered(ray, radius, hits, maxDistance, _instance._collisionLayerMask, QueryTriggerInteraction.Ignore);
                    
                    if (hits.Count > 0)
                    {
                        var sortedHits = hits.Where(x => {
                                var decayEnt = x.GetEntity() as DecayEntity;
                                return decayEnt != null && decayEnt.IsVisible(position, float.PositiveInfinity);
                            })
                            .OrderBy(x => (x.transform.position - position).sqrMagnitude)
                            .ToList();

                        if (_instance._debug)
                        {
                            foreach (var hit in hits)
                            {
                                foreach (var adminPlayer in BasePlayer.activePlayerList)
                                {
                                    if (adminPlayer == null || !adminPlayer.IsAdmin || !adminPlayer.IsAlive()) continue;

                                    var traceHitEnt = hit.GetEntity() as DecayEntity;
                                    var color = Color.red;
                                    if (sortedHits.IndexOf(hit) == 0)
                                        color = Color.green;
                                    else if (sortedHits.Contains(hit))
                                        color = Color.yellow;

                                    _instance.Arrow(adminPlayer, position, hit.transform.position, 0.05f, color, _instance._debugDrawDuration);
                                    _instance.Box(adminPlayer, hit.transform.position, 0.1f, color, _instance._debugDrawDuration);
                                    _instance.Text(adminPlayer, hit.transform.position, $"<size=10>{(sortedHits.Contains(hit) ? $"{sortedHits.IndexOf(hit)}" : "")} {traceHitEnt?.ShortPrefabName ?? "Unknown"}</size>", color, _instance._debugDrawDuration);
                                }
                            }
                        }

                        if (sortedHits.Count > 0)
                        {
                            DecayEntity hitEntity = sortedHits[0].GetEntity() as DecayEntity;
                            if (hitEntity != null)
                            {
                                if (_instance.IsDecayEntityOrAttackerIgnored(hitEntity, attacker))
                                    return;

                                var entityShortname = _instance.GetDecayEntityShortname(hitEntity);
                                hitEntityPrefab = hitEntity.PrefabName;
                                raidEvent.hitEntity = $"EVENT.HIT {entityShortname}";
                                raidEvent.victimSteamID = hitEntity.OwnerID;

                                _instance.PrintDebug($"{explosiveEntity.ShortPrefabName} COLLIDED WITH {entityShortname}[{hitEntity?.net?.ID.Value ?? 0}]");
                            }
                        }
                    }

                    Facepunch.Pool.FreeList(ref hits);
                }

                // parent entity, collision or always logged weapon detected. log raid event.
                if (!string.IsNullOrEmpty(raidEvent.hitEntity) || _instance.FindWeaponConfig(trackerCategory, raidEvent.GetPrimaryWeaponShortname()).alwaysLog)
                {
                    raidEvent.hitEntity = raidEvent.hitEntity ?? $"EVENT.NO_HIT unknown_entity";
                    raidEvent.endPos = position;

                    if (explosiveEntity is MLRSRocket)
                    {
                        var pos = (raidEvent.startPos + raidEvent.endPos) / 2;
                            pos.y += 700f;
                        raidEvent.startPos = pos;
                    }

                    _instance._raidEventLog.Add(raidEvent);
                    raidEvent.Notify(explosiveEntity, hitEntityPrefab);
                }

                if (_instance._debug)
                {
                    millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;
                    _instance.PrintDebug($"{raidEvent.weapon} explosive tracker took {millisecondsTaken}ms and was alive for {Time.realtimeSinceStartup - startTime}s");
                }
            }
        }
    }

    #endregion
}
