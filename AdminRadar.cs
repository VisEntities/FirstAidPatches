//#define DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Game.Rust.Libraries;
using Oxide.Plugins.AdminRadarExtensionMethods;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Radar", "nivex", "5.2.3")]
    [Description("Radar tool for Admins and Developers.")]
    class AdminRadar : RustPlugin
    {
        private class Cache
        {
            public readonly List<BaseEntity> Animals = new List<BaseEntity>();
            public readonly List<BaseEntity> CargoPlanes = new List<BaseEntity>();
            public readonly List<DroppedItemContainer> Backpacks = new List<DroppedItemContainer>();
            public readonly List<BaseEntity> Bags = new List<BaseEntity>();
            public readonly List<BaseEntity> Boats = new List<BaseEntity>();
            public readonly List<BradleyAPC> BradleyAPCs = new List<BradleyAPC>();
            public readonly List<BaseEntity> CargoShips = new List<BaseEntity>();
            public readonly List<BaseEntity> Cars = new List<BaseEntity>();
            public readonly List<CCTV_RC> CCTV = new List<CCTV_RC>();
            public readonly List<BaseEntity> CH47 = new List<BaseEntity>();
            public readonly List<BuildingPrivlidge> Cupboards = new List<BuildingPrivlidge>();
            public readonly Dictionary<Vector3, CachedInfo> Collectibles = new Dictionary<Vector3, CachedInfo>();
            public readonly List<StorageContainer> Containers = new List<StorageContainer>();
            public readonly Dictionary<PlayerCorpse, CachedInfo> Corpses = new Dictionary<PlayerCorpse, CachedInfo>();
            public readonly List<BaseHelicopter> Helicopters = new List<BaseHelicopter>();
            public readonly List<BaseEntity> MiniCopter = new List<BaseEntity>();
            public readonly List<BasePlayer> NPCPlayers = new List<BasePlayer>();
            public readonly Dictionary<Vector3, CachedInfo> Ores = new Dictionary<Vector3, CachedInfo>();
            public readonly List<BaseEntity> RHIB = new List<BaseEntity>();
            public readonly List<BaseEntity> RidableHorse = new List<BaseEntity>();
            public readonly List<SupplyDrop> SupplyDrops = new List<SupplyDrop>();
            public readonly List<AutoTurret> Turrets = new List<AutoTurret>();
            public readonly List<Zombie> Zombies = new List<Zombie>();
            public readonly List<BaseEntity> MLRS = new List<BaseEntity>();
        }

        [PluginReference] Plugin DiscordMessages, Clans, Backpacks;

        private const string permAllowed = "adminradar.allowed";
        private const string permBypass = "adminradar.bypass";
        private const string permAuto = "adminradar.auto";
        private const string permBypassOverride = "adminradar.bypass.override";
        private const string permList = "adminradar.list";
        private const float flickerDelay = 0.05f;
        private StoredData storedData = new StoredData();
        private bool isUnloading;
        private float startupTime;
        private const bool True = true;
        private const bool False = false;

        private List<string> tags = new List<string>
            {"ore", "cluster", "1", "2", "3", "4", "5", "6", "_", ".", "-", "deployed", "wooden", "large", "pile", "prefab", "collectable", "loot", "small"}; // strip these from names to reduce the size of the text and make it more readable

        private readonly HashSet<ulong> blocked = new HashSet<ulong>();
        private readonly List<BasePlayer> accessList = new List<BasePlayer>();
        private readonly Dictionary<ulong, Timer> voices = new Dictionary<ulong, Timer>();
        private readonly List<Radar> activeRadars = new List<Radar>();
        private readonly List<string> warnings = new List<string>();
        private readonly Dictionary<ulong, float> cooldowns = new Dictionary<ulong, float>();
        private Cache cache = new Cache();
        private Coroutine _coroutine;

        private class StoredData
        {
            public readonly List<string> Extended = new List<string>();
            public readonly Dictionary<string, List<string>> Filters = new Dictionary<string, List<string>>();
            public readonly List<string> Hidden = new List<string>();
            public readonly List<string> OnlineBoxes = new List<string>();
            public readonly List<string> Visions = new List<string>();
            public readonly List<string> Active = new List<string>();
            public StoredData() { }
        }

        private class CachedInfo
        {
            public object Info;
            public string Name;
            public float Size;
        }

        public enum EntityType
        {
            Active,
            Airdrops,
            Animals,
            Bags,
            Backpacks,
            Boats,
            Bradley,
            Cars,
            CargoPlanes,
            CargoShips,
            CCTV,
            CH47Helicopters,
            Containers,
            Collectibles,
            Cupboards,
            CupboardsArrow,
            Dead,
            GroupLimit,
            Heli,
            MiniCopter,
            MLRS,
            Npc,
            Ore,
            RidableHorses,
            RigidHullInflatableBoats,
            Sleepers,
            Source,
            Turrets,
            Zombies
        }

        private void AdminRadarDiscordMessage(string playerName, string playerId, bool state, Vector3 position)
        {
            if (!_sendDiscordMessages || DiscordMessages == null || !DiscordMessages.IsLoaded)
            {
                return;
            }

            if (Time.realtimeSinceStartup - startupTime < 5f || isUnloading)
            {
                return;
            }

            string text = state ? _discordMessageToggleOn : _discordMessageToggleOff;
            string grid = PhoneController.PositionToGridCoord(position);
            string message = $"[{DateTime.Now}] {playerName} ({playerId} @ {grid}): {text}";

            var chatEntry = new ConVar.Chat.ChatEntry
            {
                Message = message,
                UserId = playerId,
                Username = playerName,
                Time = Facepunch.Math.Epoch.Current
            };

            LogToFile("toggles", message, this, False);
            RCon.Broadcast(RCon.LogType.Chat, chatEntry);

            string steam = $"[{playerName}](https://steamcommunity.com/profiles/{playerId})";
            string server = $"steam://connect/{ConVar.Server.ip}:{ConVar.Server.port}";

            object fields = new[]
            {
                new
                {
                    name = _embedMessagePlayer,
                    value = steam,
                    inline = True
                },
                new
                {
                    name = _embedMessageMessage,
                    value = text,
                    inline = False
                },
                new
                {
                    name = _embedMessageServer,
                    value = server,
                    inline = False
                },
                new
                {
                    name = _embedMessageLocation,
                    value = grid,
                    inline = False
                }
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(fields);

            Interface.CallHook("API_SendFancyMessage", _webhookUrl, _embedMessageTitle, _messageColor, json, null, this);
        }

        private class Radar : FacepunchBehaviour
        {
            private EntityType entityType;
            private int _inactiveSeconds;
            private int activeSeconds;
            public float invokeTime { get; set; }
            public float maxDistance;
            public BasePlayer player;
            private Vector3 position;
            private float currDistance;
            private int checks;

            private bool setSource = True;
            public bool showBags;
            public bool showBoats;
            public bool showBox;
            public bool showBradley;
            public bool showCars;
            public bool showCCTV;
            public bool showCargoPlanes;
            public bool showCargoShips;
            public bool showCH47;
            public bool showCollectible;
            public bool showDead;
            public bool showHeli;
            public bool showHT;
            public bool showLoot;
            public bool showMiniCopter;
            public bool showMLRS;
            public bool showNPC;
            public bool showOre;
            public bool showRidableHorses;
            public bool showRHIB;
            public bool showSleepers;
            public bool showStash;
            public bool showTC;
            public bool showTCArrow;
            public bool showTurrets;
            public bool showAll;
            private BaseEntity source;
            public bool barebonesMode;
            private AdminRadar instance;
            private StringBuilder rsb = new StringBuilder();

            private class PrivInfo
            {
                public float ProtectedMinutes;
                public float LastTime = float.MaxValue;
                public PrivInfo() { }
            }

            private Dictionary<BuildingPrivlidge, PrivInfo> tcs = new Dictionary<BuildingPrivlidge, PrivInfo>();
            private Dictionary<int, List<BasePlayer>> groupedPlayers = new Dictionary<int, List<BasePlayer>>();
            private List<BasePlayer> nearbyPlayers = new List<BasePlayer>();
            private List<BasePlayer> distantPlayers = new List<BasePlayer>();
            private List<BasePlayer> removePlayers = new List<BasePlayer>();
            private bool limitFlag;
            private int limitIndex;
            public string playerName;
            private string playerId;
            private Vector3 lastPosition;
            private bool canBypassOverride;
            private bool hasPermAllowed;
            private Cache cache => instance.cache;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                source = player;
                isAdmin = player.IsAdmin;
                position = player.transform.position;
                playerName = player.displayName;
                playerId = player.UserIDString;
                lastPosition = position;
            }

            public void Init(AdminRadar ins)
            {
                this.instance = ins;
                ins.activeRadars.Add(this);

                if (ins.activeRadars.Count == 1 && ins.blockAny)
                {
                    ins.Subscribe(nameof(OnEntityTakeDamage));
                }

                canBypassOverride = ins.permission.UserHasPermission(playerId, permBypassOverride);
                hasPermAllowed = ins.permission.UserHasPermission(playerId, permAllowed);

                if (ins.inactiveSeconds > 0f || ins.inactiveMinutes > 0)
                {
                    InvokeRepeating(Activity, 0f, 1f);
                }

                ins.AdminRadarDiscordMessage(player.displayName, player.UserIDString, True, player.transform.position);
                Interface.CallHook("OnRadarActivated", player);
            }

            public void StopAll()
            {
                if (_routine != null)
                {
                    StopCoroutine(_routine);
                    _routine = null;
                }

                CancelInvoke();
            }

            private void OnDestroy()
            {
                Interface.CallHook("AdminRadarDiscordMessage", playerName, playerId, False, lastPosition);
                Interface.CallHook("OnRadarDeactivated", player);

                StopAll();
                nearbyPlayers.Clear();
                groupedPlayers.Clear();
                distantPlayers.Clear();
                tcs.Clear();

                if (instance == null)
                {
                    Destroy(this);
                    return;
                }

                if (instance.radarUI != null && instance.radarUI.Contains(player.UserIDString))
                    instance.DestroyUI(player);

                instance.activeRadars.Remove(this);

                if (instance.activeRadars.Count == 0 && !instance.isUnloading) instance.Unsubscribe(nameof(OnEntityTakeDamage));

                if (player != null && player.IsConnected)
                {
                    if (instance.coolDown > 0f)
                    {
                        if (!instance.cooldowns.ContainsKey(player.userID))
                            instance.cooldowns.Add(player.userID, Time.realtimeSinceStartup + instance.coolDown);
                        else instance.cooldowns[player.userID] = Time.realtimeSinceStartup + instance.coolDown;
                    }

                    if (instance.showToggleMessage)
                    {
                        instance.Message(player, instance.msg("Deactivated", player.UserIDString));
                    }
                }

                Destroy(this);
            }

            public bool GetBool(string value)
            {
                switch (value)
                {
                    case "All":
                        return showAll;
                    case "Bags":
                        return showBags;
                    case "Boats":
                        return showBoats;
                    case "Box":
                        return showBox;
                    case "Bradley":
                        return showBradley;
                    case "CargoPlanes":
                        return showCargoPlanes;
                    case "CargoShips":
                        return showCargoShips;
                    case "Cars":
                        return showCars;
                    case "CCTV":
                        return showCCTV;
                    case "CH47":
                        return showCH47;
                    case "Collectibles":
                        return showCollectible;
                    case "Dead":
                        return showDead;
                    case "Heli":
                        return showHeli;
                    case "Horse":
                    case "Horses":
                    case "RidableHorses":
                        return showRidableHorses;
                    case "Loot":
                        return showLoot;
                    case "MiniCopter":
                        return showMiniCopter;
                    case "MLRS":
                        return showMLRS;
                    case "NPC":
                        return showNPC;
                    case "Ore":
                        return showOre;
                    case "RHIB":
                        return showRHIB;
                    case "Sleepers":
                        return showSleepers;
                    case "Stash":
                        return showStash;
                    case "TCArrows":
                        return showTCArrow;
                    case "TC":
                        return showTC;
                    case "Turrets":
                        return showTurrets;
                    default:
                        return False;
                }
            }

            private void Activity()
            {
                if (source != player)
                {
                    _inactiveSeconds = 0;
                    return;
                }

                _inactiveSeconds = position == player.transform.position ? _inactiveSeconds + 1 : 0;
                position = player.transform.position;

                if (instance.inactiveMinutes > 0 && ++activeSeconds / 60 > instance.inactiveMinutes)
                {
                    Destroy(this);
                }
                else if (instance.inactiveSeconds > 0 && _inactiveSeconds > instance.inactiveSeconds)
                {
                    Destroy(this);
                }
            }

            Coroutine _routine;

            public void Start()
            {
                if (_routine != null)
                {
                    StopCoroutine(_routine);
                    _routine = null;
                }

                _routine = StartCoroutine(barebonesMode ? DoBareRadarRoutine() : DoRadarRoutine());
            }

            bool isAdmin { get; set; }

            IEnumerator DoBareRadarRoutine()
            {
                do
                {
                    if (player == null || !player.IsConnected || instance == null || instance.isUnloading)
                    {
                        Destroy(this);
                        yield break;
                    }

                    if (!isAdmin && hasPermAllowed)
                    {
                        instance.blocked.Add(player.userID);
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, True);
                        player.SendNetworkUpdateImmediate();
                    }

                    if (!SetSource())
                    {
                        yield return CoroutineEx.waitForSeconds(0.1f);
                        continue;
                    }

                    lastPosition = source.transform.position;

                    if (ShowActive() >= 50)
                    {
                        checks = 0;
                        yield return null;
                    }

                    ShowSleepers();
                    RemoveAdmin(player);
                    checks = 0;
                    yield return CoroutineEx.waitForSeconds(invokeTime);
                } while (player.IsValid() && player.IsConnected);
            }

            IEnumerator DoRadarRoutine()
            {
                do
                {
                    if (player == null || !player.IsConnected || instance == null || instance.isUnloading)
                    {
                        Destroy(this);
                        yield break;
                    }

                    if (!isAdmin && hasPermAllowed)
                    {
                        instance.blocked.Add(player.userID);
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, True);
                        player.SendNetworkUpdateImmediate();
                    }

                    if (!SetSource())
                    {
                        yield return CoroutineEx.waitForSeconds(0.1f);
                        continue;
                    }

                    if (ShowActive() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowSleepers() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.MLRS, showMLRS, "MLRS", cache.MLRS) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.Cars, showCars, "C", cache.Cars) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.CargoPlanes, showCargoPlanes, "CP", cache.CargoPlanes) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.CargoShips, showCargoShips, "CS", cache.CargoShips) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowHeli() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowBradley() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowLimits() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowTC() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowContainers() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowBags() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowTurrets() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowDead() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowNPC() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowOre() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowCCTV() >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.CH47Helicopters, showCH47, "CH47", cache.CH47) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.RigidHullInflatableBoats, showRHIB, "RHIB", cache.RHIB) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.Boats, showBoats, "RB", cache.Boats) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.MiniCopter, showMiniCopter, "MC", cache.MiniCopter) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    if (ShowEntity(EntityType.RidableHorses, showRidableHorses, "RH", cache.RidableHorse) >= 300)
                    {
                        checks = 0;
                        yield return null;
                    }

                    ShowCollectables();

                    RemoveAdmin(player);

                    checks = 0;
                    yield return CoroutineEx.waitForSeconds(invokeTime);
                } while (player.IsValid() && player.IsConnected);
            }

            private void RemoveAdmin(BasePlayer player)
            {
                if (player != null && !isAdmin && player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, False);
                    player.SendNetworkUpdateImmediate();
                    instance.blocked.Remove(player.userID);
                }
            }

            private void HandleException(Exception ex)
            {
                RemoveAdmin(player);
                instance.Puts("Error @{0}: {1} --- {2}", Enum.GetName(typeof(EntityType), entityType), ex.Message, ex.StackTrace);
                instance.Message(player, instance.msg("Exception", player.UserIDString));

                switch (entityType)
                {
                    case EntityType.Active:
                        {
                            instance.trackActive = False;
                        }
                        break;
                    case EntityType.Airdrops:
                        {
                            instance.trackSupplyDrops = False;
                            cache.SupplyDrops.Clear();
                        }
                        break;
                    case EntityType.Animals:
                    case EntityType.Npc:
                    case EntityType.Zombies:
                        {
                            instance.trackNPC = False;
                            showNPC = False;
                            instance.uiBtnNPC = False;
                            cache.Animals.Clear();
                            cache.Zombies.Clear();
                            cache.NPCPlayers.Clear();
                        }
                        break;
                    case EntityType.Bags:
                        {
                            instance.trackBags = False;
                            showBags = False;
                            instance.uiBtnBags = False;
                            cache.Bags.Clear();
                        }
                        break;
                    case EntityType.Backpacks:
                        {
                            instance.trackLoot = False;
                            showLoot = False;
                            instance.uiBtnLoot = False;
                            cache.Backpacks.Clear();
                        }
                        break;
                    case EntityType.Bradley:
                        {
                            showBradley = False;
                            instance.uiBtnBradley = False;
                            instance.trackBradley = False;
                            cache.BradleyAPCs.Clear();
                        }
                        break;
                    case EntityType.Cars:
                        {
                            showCars = False;
                            instance.uiBtnCars = False;
                            instance.trackCars = False;
                            cache.Cars.Clear();
                        }
                        break;
                    case EntityType.CargoPlanes:
                        {
                            instance.uiBtnCargoPlanes = False;
                            instance.trackCargoPlanes = False;
                            showCargoPlanes = False;
                            cache.CargoPlanes.Clear();
                        }
                        break;
                    case EntityType.CargoShips:
                        {
                            instance.trackCargoShips = False;
                            showCargoShips = False;
                            instance.uiBtnCargoShips = False;
                            cache.CargoShips.Clear();
                        }
                        break;
                    case EntityType.CCTV:
                        {
                            instance.trackCCTV = False;
                            showCCTV = False;
                            instance.uiBtnCCTV = False;
                            cache.CCTV.Clear();
                        }
                        break;
                    case EntityType.CH47Helicopters:
                        {
                            showCH47 = False;
                            instance.uiBtnCH47 = False;
                            instance.trackCH47 = False;
                            cache.CH47.Clear();
                        }
                        break;
                    case EntityType.Containers:
                        {
                            showBox = False;
                            showLoot = False;
                            showStash = False;
                            instance.uiBtnBox = False;
                            instance.uiBtnLoot = False;
                            instance.uiBtnStash = False;
                            instance.trackBox = False;
                            instance.trackLoot = False;
                            instance.trackStash = False;
                            cache.Containers.Clear();
                        }
                        break;
                    case EntityType.Collectibles:
                        {
                            instance.trackCollectibles = False;
                            showCollectible = False;
                            instance.uiBtnCollectible = False;
                            cache.Collectibles.Clear();
                        }
                        break;
                    case EntityType.Cupboards:
                        {
                            instance.trackTC = False;
                            showTC = False;
                            instance.uiBtnTC = False;
                            cache.Cupboards.Clear();
                        }
                        break;
                    case EntityType.CupboardsArrow:
                        {
                            showTCArrow = False;
                            instance.uiBtnTCArrow = False;
                        }
                        break;
                    case EntityType.Dead:
                        {
                            instance.trackDead = False;
                            showDead = False;
                            instance.uiBtnDead = False;
                            cache.Corpses.Clear();
                        }
                        break;
                    case EntityType.GroupLimit:
                        {
                            instance.drawX = False;
                        }
                        break;
                    case EntityType.Heli:
                        {
                            showHeli = False;
                            instance.uiBtnHeli = False;
                            instance.trackHeli = False;
                            cache.Helicopters.Clear();
                        }
                        break;
                    case EntityType.MiniCopter:
                        {
                            showMiniCopter = False;
                            instance.uiBtnMiniCopter = False;
                            instance.trackMiniCopter = False;
                            cache.MiniCopter.Clear();
                        }
                        break;
                    case EntityType.MLRS:
                        {
                            showMLRS = False;
                            instance.uiBtnMLRS = False;
                            instance.trackMLRS = False;
                            cache.MLRS.Clear();
                        }
                        break;
                    case EntityType.Ore:
                        {
                            instance.trackOre = False;
                            showOre = False;
                            instance.uiBtnOre = False;
                            cache.Ores.Clear();
                        }
                        break;
                    case EntityType.RidableHorses:
                        {
                            showRidableHorses = False;
                            instance.uiBtnRidableHorses = False;
                            instance.trackRidableHorses = False;
                            cache.RidableHorse.Clear();
                        }
                        break;
                    case EntityType.RigidHullInflatableBoats:
                        {
                            showRHIB = False;
                            instance.uiBtnRHIB = False;
                            instance.trackRigidHullInflatableBoats = False;
                            cache.RHIB.Clear();
                        }
                        break;
                    case EntityType.Boats:
                        {
                            showBoats = False;
                            instance.uiBtnBoats = False;
                            instance.trackBoats = False;
                            cache.Boats.Clear();
                        }
                        break;
                    case EntityType.Sleepers:
                        {
                            instance.trackSleepers = False;
                            showSleepers = False;
                            instance.uiBtnSleepers = False;
                        }
                        break;
                    case EntityType.Source:
                        {
                            setSource = False;
                        }
                        break;
                    case EntityType.Turrets:
                        {
                            instance.trackTurrets = False;
                            showTurrets = False;
                            instance.uiBtnTurrets = False;
                            cache.Turrets.Clear();
                        }
                        break;
                }

                instance.uiBtnNames = new string[0];
                instance.uiButtons = null;
            }

            private Dictionary<ulong, bool> _backpacks = new Dictionary<ulong, bool>();

            private bool API_GetExistingBackpacks(ulong playerId)
            {
                if (!instance.trackBackpacks)
                {
                    return False;
                }

                if (_backpacks.ContainsKey(playerId))
                {
                    return _backpacks[playerId];
                }

                var backpacks = instance.Backpacks?.Call("API_GetExistingBackpacks") as Dictionary<ulong, ItemContainer>;

                if (backpacks == null)
                {
                    return False;
                }

                instance.timer.Once(60f, () => _backpacks.Remove(playerId));

                ItemContainer container;
                if (backpacks.TryGetValue(playerId, out container) && !container.IsEmpty())
                {
                    _backpacks.Add(playerId, True);

                    return True;
                }

                _backpacks.Add(playerId, False);

                return False;
            }

            private Vector3 GetNearestCupboard(BasePlayer target)
            {
                var positions = new List<Vector3>();
                float distance = 0f;

                foreach (var tc in cache.Cupboards)
                {
                    if (!tc.IsKilled() && tc.IsAuthed(target))
                    {
                        distance = (target.transform.position - tc.transform.position).magnitude;

                        if (distance >= 5f && distance <= instance.tcArrowsDistance)
                        {
                            positions.Add(tc.transform.position);
                        }
                    }
                }

                if (positions.Count == 0)
                {
                    return Vector3.zero;
                }

                if (positions.Count > 1)
                {
                    positions.Sort((x, y) => ((x - target.transform.position).magnitude).CompareTo(((y - target.transform.position).magnitude)));
                }

                return positions[0];
            }

            private bool SetSource()
            {
                if (!setSource)
                {
                    source = player;
                    return True;
                }

                entityType = EntityType.Source;
                source = player;

                if (player.IsSpectating())
                {
                    var parentEntity = player.GetParentEntity();

                    if (parentEntity as BasePlayer != null)
                    {
                        var target = parentEntity as BasePlayer;

                        if (target.IsDead() && !target.IsConnected)
                            player.StopSpectating();
                        else source = parentEntity;
                    }
                }

                if (player == source && (player.IsDead() || player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)))
                {
                    RemoveAdmin(player);
                    return False;
                }

                return True;
            }

            private void DrawVision(BasePlayer target)
            {
                RaycastHit hit;
                if (Physics.Raycast(target.eyes.HeadRay(), out hit, Mathf.Infinity))
                {
                    DrawArrow(Color.red, target.eyes.position + new Vector3(0f, 0.115f, 0f), hit.point, 0.15f, True);
                }
                //else DrawArrow(Color.black, target.eyes.position + target.eyes.HeadForward(), target.eyes.position + target.eyes.HeadForward() * 30f, 0.15f, True);
            }

            private void DrawArrow(Color color, Vector3 from, Vector3 to, float size, bool @override)
            {
                if (instance.drawArrows || @override) player.SendConsoleCommand("ddraw.arrow", invokeTime + flickerDelay, color, from, to, size);
            }

            private void DrawPlayerText(Color color, Vector3 position, string prefix, string text, bool @override = False)
            {
                if (instance.drawText || @override) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, color, position, $"<size={instance.playerPrefixSize}>{prefix}</size> <size={instance.playerTextSize}>{text}</size>");
            }

            private void DrawText(Color color, Vector3 position, string prefix, string text, bool @override = False)
            {
                if (instance.drawText || @override) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, color, position, $"<size={instance.prefixSize}>{prefix}</size> <size={instance.textSize}>{text}</size>");
            }

            private void DrawBox(Color color, Vector3 position, float size, bool @override = False)
            {
                if (instance.drawBox || @override) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, color, position, size);
            }

            private int ShowActive()
            {
                if (!instance.trackActive)
                    return checks;

                entityType = EntityType.Active;
                Color color;

                List<ulong> list;
                if (!instance.trackers.TryGetValue(player, out list))
                {
                    list = new List<ulong>();
                }

                try
                {
                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        if (target == null || target.transform == null || target.IPlayer == null || !target.IsConnected || list.Contains(target.userID))
                        {
                            continue;
                        }

                        currDistance = (target.transform.position - source.transform.position).magnitude;

                        if (player.userID == target.userID || currDistance > maxDistance)
                        {
                            continue;
                        }

                        if (instance.permission.UserHasPermission(target.UserIDString, permBypass) && !canBypassOverride)
                        {
                            continue;
                        }

                        var isFlying = !target.IsOnGround() || target.IsFlying;
                        var isUnderground = !isFlying && target.transform.position.y + 1f < TerrainMeta.HeightMap.GetHeight(target.transform.position);

                        color = __(isFlying ? instance.activeFlyingCC : isUnderground ? instance.activeUndergroundCC : target.health > 0f ? instance.activeCC : instance.activeDeadCC);

                        if (canBypassOverride && (target.IsAdmin || instance.permission.UserHasPermission(target.UserIDString, "fauxadmin.allowed")))
                        {
                            color = Color.magenta;
                        }

                        if (currDistance < instance.playerDistance)
                        {
                            if (instance.storedData.Extended.Contains(player.UserIDString) && target.svActiveItemID.Value != 0u)
                            {
                                Item item = target.GetActiveItem();

                                if (item != null)
                                {
                                    rsb.Append(item.info.displayName.translated);
                                    var itemList = item.contents?.itemList;

                                    if (itemList?.Count > 0)
                                    {
                                        rsb.Append(" (");

                                        for (int index = 0; index < itemList.Count; index++)
                                        {
                                            rsb.Append(itemList[index].info.displayName.translated);
                                            rsb.Append("|");
                                        }

                                        rsb.Replace("Weapon ", "");
                                        rsb.Replace("Simple Handmade ", "");
                                        rsb.Replace("Muzzle ", "");
                                        rsb.Replace("4x Zoom Scope", "4x");
                                        rsb.Append(")");
                                    }
                                }
                            }

                            if (instance.averagePingInterval > 0)
                            {
                                rsb.Append(" ");
                                rsb.Append(target.IPlayer.Ping);
                                rsb.Append("ms");
                            }

                            if (API_GetExistingBackpacks(target.userID))
                            {
                                rsb.Append("*");
                            }

                            string clanCC;
                            if (instance._clanColors.TryGetValue(instance.GetClanOf(target.userID), out clanCC))
                            {
                                if (!instance.ctNameColor)
                                {
                                    clanCC = $" <color={clanCC}>C</color>";
                                }
                            }
                            else clanCC = null;

                            string teamCC;
                            if (instance._teamColors.TryGetValue(target.currentTeam, out teamCC))
                            {
                                if (!instance.ctNameColor)
                                {
                                    teamCC = $"<color={teamCC}>T</color>";
                                }
                            }
                            else teamCC = null;

                            string cc = clanCC ?? teamCC;

                            if (instance.permission.UserHasGroup(target.UserIDString, "new"))
                            {
                                rsb.Append("<color=#00FF00>*</color>");
                            }

                            string cheats = GetCheats(target);

                            if (instance.storedData.Visions.Contains(player.UserIDString) && currDistance <= 150f) DrawVision(target);
                            DrawArrow(__(instance.colorDrawArrows), target.transform.position + new Vector3(0f, target.transform.position.y + 10), target.transform.position, 1, False);
                            if (instance.ctNameColor && !string.IsNullOrEmpty(cc))
                            {
                                string displayName = string.Format("<color={0}>{1}</color>", cc, instance.RemoveFormatting(target));
                                string health = showHT && target.metabolism != null ? string.Format("{0} {1}:{2}", Math.Floor(target.health), target.metabolism.calories.value.ToString("#0"), target.metabolism.hydration.value.ToString("#0")) : Math.Floor(target.health).ToString("#0");

                                DrawPlayerText(color, target.transform.position + new Vector3(0f, 2f, 0f), $"{cheats}{displayName}", $"<color={cc}>{health} {currDistance:0.0} {rsb}</color>");
                            }
                            else
                            {
                                string health = showHT && target.metabolism != null ? string.Format("{0} <color=#FFA500>{1}</color>:<color=#FFADD8E6>{2}</color>", Math.Floor(target.health), target.metabolism.calories.value.ToString("#0"), target.metabolism.hydration.value.ToString("#0")) : Math.Floor(target.health).ToString("#0");

                                DrawPlayerText(color, target.transform.position + new Vector3(0f, 2f, 0f), $"{cheats}{instance.RemoveFormatting(target)}", string.Format("<color={0}>{1}</color> <color={2}>{3}</color> {4}{5}{6}", instance.healthCC, health, instance.distCC, currDistance.ToString("0"), rsb.ToString(), clanCC, teamCC));
                                rsb.Clear();
                            }
                            DrawBox(color, target.transform.position + new Vector3(0f, 1f, 0f), target.GetHeight(target.modelState.ducked));
                            if (instance.voices.ContainsKey(target.userID) && (target.transform.position - player.transform.position).magnitude <= instance.voiceDistance)
                            {
                                DrawArrow(Color.yellow, target.transform.position + new Vector3(0f, 5f, 0f), target.transform.position + new Vector3(0f, 2.5f, 0f), 0.5f, True);
                            }
                            ShowCupboardArrows(target, EntityType.Active);
                        }
                        else if (instance.drawX)
                            distantPlayers.Add(target);
                        else
                            DrawBox(color, target.transform.position + new Vector3(0f, 1f, 0f), 5f, True);

                        checks++;
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private List<string> cheats = new List<string>();

            private string GetCheats(BasePlayer target)
            {
                cheats.Clear();
                if (instance.ShowRadar && instance.IsRadar(target.UserIDString)) cheats.Add(instance.ShowRadarText);
                if (instance.ShowGod && target.IsGod()) cheats.Add(instance.ShowGodText);
                if (instance.ShowGodPlugin && target.metabolism?.calories?.min == 500) cheats.Add(instance.ShowGodPluginText);
                if (instance.ShowVanish && target.limitNetworking) cheats.Add(instance.ShowVanishText);
                if (instance.ShowNoClip && target.IsFlying) cheats.Add(instance.ShowNoClipText);
                if (cheats.Count > 0) return $"({string.Join("|", cheats.ToArray())}) ";
                return "";
            }

            private int ShowSleepers()
            {
                if (!showSleepers || !instance.trackSleepers)
                    return checks;

                entityType = EntityType.Sleepers;
                Color color;

                List<ulong> list;
                if (!instance.trackers.TryGetValue(player, out list))
                {
                    list = new List<ulong>();
                }

                try
                {
                    foreach (var target in BasePlayer.sleepingPlayerList)
                    {
                        if (target == null || target.transform == null || list.Contains(target.userID))
                            continue;

                        currDistance = (target.transform.position - source.transform.position).magnitude;

                        if (currDistance > maxDistance)
                            continue;

                        if (currDistance < instance.playerDistance)
                        {
                            color = __(target.IsAlive() ? instance.sleeperCC : instance.sleeperDeadCC);
                            string teamCC;

                            string clanCC;
                            if (instance._clanColors.TryGetValue(instance.GetClanOf(target.userID), out clanCC))
                            {
                                if (!instance.ctNameColor)
                                {
                                    clanCC = $" <color={clanCC}>C</color>";
                                }
                            }

                            if (instance._teamColors.TryGetValue(target.currentTeam, out teamCC))
                            {
                                if (!instance.ctNameColor)
                                {
                                    teamCC = $"<color={teamCC}>T</color>";
                                }
                            }

                            string cc = clanCC ?? teamCC;

                            DrawArrow(__(instance.colorDrawArrows), target.transform.position + new Vector3(0f, target.transform.position.y + 10), target.transform.position, 1, False);
                            if (instance.ctNameColor && !string.IsNullOrEmpty(cc))
                            {
                                string displayName = string.Format("<color={0}>{1}</color>", cc, instance.RemoveFormatting(target));
                                string health = showHT && target.metabolism != null ? string.Format("{0} {1}:{2}", Math.Floor(target.health), target.metabolism.calories.value.ToString("#0"), target.metabolism.hydration.value.ToString("#0")) : Math.Floor(target.health).ToString("#0");

                                DrawPlayerText(color, target.transform.position + new Vector3(0f, 2f, 0f), displayName, $"<color={cc}>{health} {currDistance:0.0}</color>");
                            }
                            else
                            {
                                string health = showHT && target.metabolism != null ? string.Format("{0} <color=#FFA500>{1}</color>:<color=#FFADD8E6>{2}</color>", Math.Floor(target.health), target.metabolism.calories.value.ToString("#0"), target.metabolism.hydration.value.ToString("#0")) : Math.Floor(target.health).ToString("#0");

                                DrawPlayerText(color, target.transform.position, instance.RemoveFormatting(target) ?? target.userID.ToString(), string.Format("<color={0}>{1}</color> <color={2}>{3}</color>{4}{5}", instance.healthCC, health, instance.distCC, currDistance.ToString("0"), clanCC, teamCC));
                            }
                            DrawPlayerText(color, target.transform.position + new Vector3(0f, 1f, 0f), "X", string.Empty, instance.drawX);
                            if (!instance.drawX && instance.drawBox) DrawBox(color, target.transform.position, GetScale(currDistance));
                            ShowCupboardArrows(target, EntityType.Sleepers);
                        }
                        else DrawBox(Color.cyan, target.transform.position + new Vector3(0f, 1f, 0f), 5f, True);

                        checks++;
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private void ShowCupboardArrows(BasePlayer target, EntityType lastType)
            {
                try
                {
                    if (showTCArrow && instance.uiBtnTCArrow && instance.uiBtnTC)
                    {
                        entityType = EntityType.CupboardsArrow;
                        var nearest = GetNearestCupboard(target);

                        if (nearest != Vector3.zero)
                        {
                            DrawArrow(__(instance.tcCC), target.transform.position + new Vector3(0f, 0.115f, 0f), nearest, 0.25f, True);
                        }

                        entityType = lastType;
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }
            }

            private int ShowHeli()
            {
                if (!showHeli || (!instance.trackHeli && !instance.uiBtnHeli))
                    return checks;

                entityType = EntityType.Heli;

                try
                {
                    if (cache.Helicopters.Count > 0)
                    {
                        foreach (var heli in cache.Helicopters)
                        {
                            if (heli == null || heli.transform == null || heli.IsDestroyed)
                                continue;

                            currDistance = (heli.transform.position - source.transform.position).magnitude;
                            string heliHealth = heli.health > 1000 ? Math.Floor(heli.health).ToString("#,##0,K", CultureInfo.InvariantCulture) : Math.Floor(heli.health).ToString("#0");
                            string info = instance.showHeliRotorHealth ? string.Format("<color={0}>{1}</color> (<color=#FFFF00>{2}</color>/<color=#FFFF00>{3}</color>)", instance.healthCC, heliHealth, Math.Floor(heli.weakspots[0].health), Math.Floor(heli.weakspots[1].health)) : string.Format("<color={0}>{1}</color>", instance.healthCC, heliHealth);

                            DrawText(__(instance.heliCC), heli.transform.position + new Vector3(0f, 2f, 0f), "H", string.Format("{0} <color={1}>{2}</color>", info, instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(instance.heliCC), heli.transform.position + new Vector3(0f, 1f, 0f), GetScale(currDistance));
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowBradley()
            {
                if (!showBradley || (!instance.uiBtnBradley && !instance.trackBradley))
                    return checks;

                entityType = EntityType.Bradley;

                try
                {
                    if (cache.BradleyAPCs.Count > 0)
                    {
                        foreach (var bradley in cache.BradleyAPCs)
                        {
                            if (bradley == null || bradley.transform == null || bradley.IsDestroyed)
                                continue;

                            currDistance = (bradley.transform.position - source.transform.position).magnitude;
                            string info = string.Format("<color={0}>{1}</color>", instance.healthCC, bradley.health > 1000 ? Math.Floor(bradley.health).ToString("#,##0,K", CultureInfo.InvariantCulture) : Math.Floor(bradley.health).ToString());

                            DrawText(__(instance.bradleyCC), bradley.transform.position + new Vector3(0f, 2f, 0f), "B", string.Format("{0} <color={1}>{2}</color>", info, instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(instance.bradleyCC), bradley.transform.position + new Vector3(0f, 1f, 0f), GetScale(currDistance));
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowLimits()
            {
                entityType = EntityType.GroupLimit;

                if (!instance.drawX)
                {
                    return checks;
                }

                try
                {
                    distantPlayers.RemoveAll(p => p == null || p.IsDestroyed);
                    checks++;

                    if (distantPlayers.Count == 0)
                    {
                        return checks;
                    }

                    limitIndex = 0;
                    limitFlag = False;

                    foreach (var p in distantPlayers)
                    {
                        nearbyPlayers.Clear();

                        for (int j = 0; j < distantPlayers.Count; j++)
                        {
                            if ((distantPlayers[j].transform.position - p.transform.position).magnitude < instance.groupRange)
                            {
                                limitFlag = False;

                                for (int i = 0; i < groupedPlayers.Values.Count; i++)
                                {
                                    if (groupedPlayers[i].Contains(distantPlayers[j]))
                                    {
                                        limitFlag = True;
                                        break;
                                    }
                                }

                                if (!limitFlag)
                                {
                                    nearbyPlayers.Add(distantPlayers[j]);
                                }

                            }
                        }

                        if (nearbyPlayers.Count > instance.groupLimit)
                        {
                            while (groupedPlayers.ContainsKey(limitIndex)) limitIndex++;
                            groupedPlayers.Add(limitIndex, new List<BasePlayer>(nearbyPlayers));
                            removePlayers.AddRange(nearbyPlayers);
                        }
                    }

                    distantPlayers.RemoveAll(player => removePlayers.Contains(player));

                    for (int j = 0; j < distantPlayers.Count; j++)
                    {
                        DrawText(distantPlayers[j].IsAlive() ? Color.green : Color.red, distantPlayers[j].transform.position + new Vector3(0f, 1f, 0f), "X", string.Empty, True);
                        checks++;
                    }

                    for (int j = 0; j < groupedPlayers.Count; j++)
                    {
                        for (int k = 0; k < groupedPlayers[j].Count; k++)
                        {
                            if (instance.groupCountHeight != 0f && k == 0)
                            {
                                DrawText(Color.magenta, groupedPlayers[j][k].transform.position + new Vector3(0f, instance.groupCountHeight, 0f), groupedPlayers[j].Count.ToString(), string.Empty, True);
                            }

                            DrawText(__(groupedPlayers[j][k].IsAlive() ? instance.GetGroupColor(j) : instance.groupColorDead), groupedPlayers[j][k].transform.position + new Vector3(0f, 1f, 0f), "X", string.Empty, True);
                            checks++;
                        }
                    }

                    nearbyPlayers.Clear();
                    groupedPlayers.Clear();
                    distantPlayers.Clear();
                    removePlayers.Clear();
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowTC()
            {
                if (!showTC || !instance.trackTC)
                    return checks;

                entityType = EntityType.Cupboards;
                int bags;
                float time;

                try
                {
                    foreach (var tc in cache.Cupboards)
                    {
                        if (tc == null || tc.transform == null || tc.IsDestroyed)
                        {
                            tcs.Remove(tc);
                            continue;
                        }

                        currDistance = (tc.transform.position - source.transform.position).magnitude;

                        if (currDistance < instance.tcDistance && currDistance < maxDistance)
                        {
                            if (instance.drawText)
                            {
                                bags = 0;

                                if (instance.showTCBagCount)
                                {
                                    var building = tc.GetBuilding();

                                    if (building != null)
                                    {
                                        foreach (var entity in building.decayEntities)
                                        {
                                            if (entity is SleepingBag)
                                            {
                                                bags++;
                                            }
                                        }
                                    }
                                }

                                if (!tcs.ContainsKey(tc))
                                {
                                    tcs.Add(tc, new PrivInfo());
                                }

                                time = Time.realtimeSinceStartup;

                                if (tcs[tc].LastTime == float.MaxValue || time - tcs[tc].LastTime > 60)
                                {
                                    tcs[tc].ProtectedMinutes = tc.GetProtectedMinutes();
                                    tcs[tc].LastTime = time;
                                }

                                string text = string.Empty;

                                if (bags > 0 && instance.showTCAuthedCount) text = string.Format("<color={0}>{1}</color> <color={2}>{3}</color> <color={4}>{5}</color> <color={0}>{6}</color>", instance.distCC, currDistance, instance.bagCC, bags, instance.tcCC, tc.authorizedPlayers.Count, tcs[tc].ProtectedMinutes);
                                else if (bags > 0) text = string.Format("<color={0}>{1}</color> <color={2}>{3}</color> <color={0}>{4}</color>", instance.distCC, currDistance, instance.bagCC, bags, tcs[tc].ProtectedMinutes); // tc <distance> <sleeping bags in this building> <authed players on cupboard> <protected minutes left>
                                else if (instance.showTCAuthedCount) text = string.Format("<color={0}>{1}</color> <color={2}>{3}</color> <color={0}>{4}</color>", instance.distCC, currDistance, instance.tcCC, tc.authorizedPlayers.Count, tcs[tc].ProtectedMinutes);
                                else text = string.Format("<color={0}>{1} {2}</color>", instance.distCC, currDistance, tcs[tc].ProtectedMinutes);

                                DrawText(__(instance.tcCC), tc.transform.position + new Vector3(0f, 0.5f, 0f), "TC", text, True);
                            }

                            DrawBox(__(instance.tcCC), tc.transform.position + new Vector3(0f, 0.5f, 0f), 3f);
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowContainers()
            {
                if (!showBox && !showLoot && !showStash)
                {
                    return checks;
                }

                bool isBox;
                bool isLoot;

                try
                {
                    if (showLoot)
                    {
                        entityType = EntityType.Backpacks;

                        foreach (var backpack in cache.Backpacks)
                        {
                            if (backpack == null || backpack.transform == null || backpack.IsDestroyed)
                                continue;

                            currDistance = (backpack.transform.position - source.transform.position).magnitude;

                            if (currDistance > maxDistance || currDistance > instance.lootDistance)
                                continue;

                            DrawText(__(instance.backpackCC), backpack.transform.position + new Vector3(0f, 0.5f, 0f), string.IsNullOrEmpty(backpack._playerName) ? instance.msg("backpack", player.UserIDString) : backpack._playerName, string.Format("{0}<color={1}>{2}</color>", instance.GetContents(backpack), instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(instance.backpackCC), backpack.transform.position + new Vector3(0f, 0.5f, 0f), GetScale(currDistance));
                            checks++;
                        }
                    }

                    if (showBox && instance.trackSupplyDrops)
                    {
                        entityType = EntityType.Airdrops;

                        foreach (var drop in cache.SupplyDrops)
                        {
                            if (drop == null || drop.transform == null || drop.IsDestroyed)
                                continue;

                            currDistance = (drop.transform.position - source.transform.position).magnitude;

                            if (currDistance > maxDistance || currDistance > instance.adDistance)
                                continue;

                            string text = instance.showAirdropContents && drop.inventory.itemList.Count > 0 ? instance.GetContents(drop.inventory.itemList) : string.Format("({0}) ", drop.inventory.itemList.Count);

                            DrawText(__(instance.airdropCC), drop.transform.position + new Vector3(0f, 0.5f, 0f), instance._(drop.ShortPrefabName), string.Format("<color={0}>{1}</color><color={2}>{3}</color>", instance.lootCC, text, instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(instance.airdropCC), drop.transform.position + new Vector3(0f, 0.5f, 0f), GetScale(currDistance));
                            checks++;
                        }
                    }

                    entityType = EntityType.Containers;

                    foreach (var container in cache.Containers)
                    {
                        if (container == null || container.transform == null || container.IsDestroyed)
                            continue;

                        currDistance = (container.transform.position - source.transform.position).magnitude;

                        if (currDistance > maxDistance)
                            continue;

                        isBox = instance.IsBox(container.ShortPrefabName);
                        isLoot = instance.IsLoot(container.ShortPrefabName);

                        if (container is StashContainer)
                        {
                            if (!showStash || currDistance > instance.stashDistance || !instance.trackStash)
                                continue;
                        }
                        else if (isBox)
                        {
                            if (!showBox || currDistance > instance.boxDistance || !instance.trackBox)
                                continue;
                            if (container.prefabID == 186002280 && currDistance > instance.vmDistance)
                                continue;
                        }
                        else if (isLoot)
                        {
                            if (!showLoot || currDistance > instance.lootDistance || !instance.trackLoot)
                                continue;
                        }

                        string colorHex = container is LockedByEntCrate || container is VendingMachine ? instance.heliCC : isBox ? instance.boxCC : isLoot ? instance.lootCC : instance.stashCC;

                        if (instance.storedData.OnlineBoxes.Contains(player.UserIDString) && container.OwnerID.IsSteamId() && (container.name.Contains("box") || container.name.Contains("coffin")))
                        {
                            var owner = BasePlayer.FindByID(container.OwnerID);

                            if (owner == null || !owner.IsConnected)
                            {
                                continue;
                            }
                        }

                        string text = container.inventory?.itemList?.Count > 0 ? (isLoot && instance.showLootContents || container is StashContainer && instance.showStashContents ? instance.GetContents(container.inventory.itemList) : string.Format("({0}) ", container.inventory.itemList.Count)) : string.Empty;
                        //if (container.OwnerID == 0) text = instance.GetContents(container.inventory.itemList, True);
                        if (text.Length == 0 && !instance.drawEmptyContainers) continue;

                        string shortname = instance._(container.ShortPrefabName).Replace("coffinstorage", "coffin").Replace("vendingmachine", "VM");
                        DrawText(__(colorHex), container.transform.position + new Vector3(0f, 0.5f, 0f), shortname, string.Format("{0}<color={1}>{2}</color>", text, instance.distCC, currDistance.ToString("0")));
                        DrawBox(__(colorHex), container.transform.position + new Vector3(0f, 0.5f, 0f), GetScale(currDistance));
                        checks++;
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowBags()
            {
                if (!showBags || !instance.trackBags)
                    return checks;

                entityType = EntityType.Bags;

                try
                {
                    foreach (var bag in cache.Bags)
                    {
                        if (bag == null || bag.IsDestroyed) continue;

                        currDistance = (bag.transform.position - source.transform.position).magnitude;

                        if (currDistance < instance.bagDistance && currDistance < maxDistance)
                        {
                            DrawText(__(instance.bagCC), bag.transform.position, "bag", string.Format("<color={0}>{1}</color>", instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(instance.bagCC), bag.transform.position, 0.5f);
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowTurrets()
            {
                if (!showTurrets || !instance.trackTurrets)
                    return checks;

                entityType = EntityType.Turrets;

                try
                {
                    foreach (var turret in cache.Turrets)
                    {
                        currDistance = (turret.transform.position - source.transform.position).magnitude;

                        if (currDistance < instance.turretDistance && currDistance < maxDistance)
                        {
                            DrawText(__(instance.atCC), turret.transform.position + new Vector3(0f, 0.5f, 0f), "AT", string.Format("({0}) <color={1}>{2}</color>", turret.inventory?.itemList?.Count ?? -1, instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(instance.atCC), turret.transform.position + new Vector3(0f, 0.5f, 0f), 1f);
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowDead()
            {
                if (!showDead || !instance.trackDead)
                    return checks;

                entityType = EntityType.Dead;

                try
                {
                    foreach (var corpse in cache.Corpses)
                    {
                        if (corpse.Key == null || corpse.Key.transform == null || corpse.Key.IsDestroyed)
                            continue;

                        currDistance = (corpse.Key.transform.position - source.transform.position).magnitude;

                        if (currDistance < instance.corpseDistance && currDistance < maxDistance)
                        {
                            DrawText(__(instance.corpseCC), corpse.Key.transform.position + new Vector3(0f, 0.25f, 0f), corpse.Value.Name, string.Format("({0})", corpse.Value.Info));
                            DrawBox(__(instance.corpseCC), corpse.Key.transform.position, GetScale(currDistance));
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowNPC()
            {
                if (!showNPC || !instance.trackNPC)
                    return checks;

                entityType = EntityType.Zombies;

                try
                {
                    float j, k = TerrainMeta.HeightMap.GetHeight(source.transform.position);

                    foreach (var zombie in cache.Zombies)
                    {
                        if (zombie == null || zombie.transform == null || zombie.IsDestroyed)
                            continue;

                        currDistance = (zombie.transform.position - source.transform.position).magnitude;

                        if (currDistance > maxDistance)
                            continue;

                        if (currDistance < instance.playerDistance)
                        {
                            DrawArrow(__(instance.zombieCC), zombie.transform.position + new Vector3(0f, zombie.transform.position.y + 10), zombie.transform.position, 1, False);
                            DrawText(__(instance.zombieCC), zombie.transform.position + new Vector3(0f, 2f, 0f), instance.msg("Zombie", player.UserIDString), string.Format("<color={0}>{1}</color> <color={2}>{3}</color>", instance.healthCC, Math.Floor(zombie.health), instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(instance.zombieCC), zombie.transform.position + new Vector3(0f, 1f, 0f), GetScale(currDistance));
                        }
                        else DrawBox(__(instance.zombieCC), zombie.transform.position + new Vector3(0f, 1f, 0f), 5f, True);

                        checks++;
                    }

                    entityType = EntityType.Npc;

                    foreach (var target in cache.NPCPlayers)
                    {
                        if (target == null || target.IsDestroyed || target.transform == null)
                            continue;

                        currDistance = (target.transform.position - source.transform.position).magnitude;

#if DEBUG
                        if (currDistance > maxDistance) distantPlayers.Add(target);
#endif

                        if (player == target || currDistance > maxDistance)
                            continue;

                        if (instance.skipUnderworld)
                        {
                            j = TerrainMeta.HeightMap.GetHeight(target.transform.position);

                            if (j > target.transform.position.y)
                            {
                                if (source.transform.position.y > k)
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                if (source.transform.position.y < k)
                                {
                                    continue;
                                }
                            }
                        }

                        string npcColor = target.ShortPrefabName.Contains("peacekeeper") ? instance.peacekeeperCC : target.name.Contains("scientist") ? instance.scientistCC : target.ShortPrefabName == "murderer" ? instance.murdererCC : instance.npcCC;

                        var heldEntity = target.GetHeldEntity();

                        if (heldEntity != null && !(heldEntity is BaseProjectile) && heldEntity.hostile) npcColor = instance.murdererCC;

                        if (currDistance < instance.npcPlayerDistance)
                        {
                            string id = player.UserIDString;
                            string displayName;
                            if (!string.IsNullOrEmpty(target.displayName) && target.displayName != target.UserIDString)
                            {
                                displayName = target.displayName;
                            }
                            else displayName = displayName = target.ShortPrefabName == "scarecrow" ? instance.msg("scarecrow", id) : target.PrefabName.Contains("scientist") ? instance.msg("scientist", id) : instance.msg(target.ShortPrefabName, id);

                            if (instance.drawTargetsVictim)
                            {
                                var victim = GetVictim(target);

                                if (!string.IsNullOrEmpty(victim?.displayName))
                                {
                                    DrawText(__(npcColor), target.transform.position + new Vector3(0f, 2f + currDistance * 0.03f, 0f), "T:", string.Format("<color={0}>{1}</color>", victim.IsSleeping() ? instance.sleeperCC : victim.IsAlive() ? "#00ff00" : instance.activeDeadCC, victim.displayName));
                                }
                            }

                            //var isFlying = !target.IsOnGround() || target.IsFlying;
                            //var isUnderground = target.transform.position.y + 1f < TerrainMeta.HeightMap.GetHeight(target.transform.position);
                            //npcColor = isFlying ? activeFlyingCC : isUnderground ? activeUndergroundCC : activeCC;

                            DrawArrow(__(npcColor), target.transform.position + new Vector3(0f, target.transform.position.y + 10), target.transform.position, 1, False);
                            DrawText(__(npcColor), target.transform.position + new Vector3(0f, 2f, 0f), displayName, string.Format("<color={0}>{1}</color> <color={2}>{3}</color>", instance.healthCC, Math.Floor(target.health), instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(npcColor), target.transform.position + new Vector3(0f, 1f, 0f), target.GetHeight(target.modelState.ducked));
                        }
                        else DrawBox(__(npcColor), target.transform.position + new Vector3(0f, 1f, 0f), 5f, True);

                        checks++;
                    }

                    entityType = EntityType.Animals;
                    foreach (var npc in cache.Animals)
                    {
                        if (npc == null || npc.transform == null || npc.IsDestroyed)
                            continue;

                        currDistance = (npc.transform.position - source.transform.position).magnitude;

                        if (currDistance < instance.npcDistance && currDistance < maxDistance)
                        {
                            if (instance.drawTargetsVictim)
                            {
                                var victim = GetVictim(npc);

                                if (!string.IsNullOrEmpty(victim?.displayName))
                                {
                                    DrawText(Color.yellow, npc.transform.position + new Vector3(0f, 2f + currDistance * 0.03f, 0f), "T:", string.Format("<color={0}>{1}</color>", victim.IsSleeping() ? instance.sleeperCC : victim.IsAlive() ? "#00ff00" : instance.activeDeadCC, victim.displayName));
                                }
                            }

                            DrawText(__(instance.npcCC), npc.transform.position + new Vector3(0f, 1f, 0f), instance.msg(npc.ShortPrefabName), string.Format("<color={0}>{1}</color> <color={2}>{3}</color>", instance.healthCC, Math.Floor(npc.Health()), instance.distCC, currDistance.ToString("0")));
                            DrawBox(__(instance.npcCC), npc.transform.position + new Vector3(0f, 1f, 0f), npc.bounds.size.y);
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private BasePlayer GetVictim(BaseEntity entity)
            {
                if (entity is global::HumanNPC)
                {
                    var npc = entity as global::HumanNPC;

                    return npc.GetBestTarget() as BasePlayer;
                }
                else if (entity is BaseAnimalNPC) // brain must be exposed
                {
                    var npc = entity as BaseAnimalNPC;

                    foreach (var target in npc.brain.Senses.Players)
                    {
                        if (target is BasePlayer)
                        {
                            return target as BasePlayer;
                        }
                    }
                }

                return null;
            }

            private int ShowOre()
            {
                if (!showOre || !instance.trackOre)
                    return checks;

                entityType = EntityType.Ore;

                try
                {
                    foreach (var ore in cache.Ores)
                    {
                        currDistance = (ore.Key - source.transform.position).magnitude;

                        if (currDistance < instance.oreDistance && currDistance < maxDistance)
                        {
                            string info = instance.showResourceAmounts ? string.Format("({0})", ore.Value.Info) : string.Format("<color={0}>{1}</color>", instance.distCC, currDistance.ToString("0"));
                            DrawText(__(instance.resourceCC), ore.Key + new Vector3(0f, 1f, 0f), ore.Value.Name, info);
                            DrawBox(__(instance.resourceCC), ore.Key + new Vector3(0f, 1f, 0f), GetScale(currDistance));
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowCCTV()
            {
                if (!showCCTV || !instance.trackCCTV)
                    return checks;

                entityType = EntityType.CCTV;

                try
                {
                    foreach (var cctv in cache.CCTV)
                    {
                        currDistance = (cctv.transform.position - source.transform.position).magnitude;

                        if (currDistance < instance.cctvDistance && currDistance < maxDistance)
                        {
                            string info = string.Format("<color={0}>{1}</color> {2}", instance.distCC, currDistance.ToString("0"), cctv.ViewerCount);
                            var color = cctv.HasFlag(BaseEntity.Flags.Reserved5) ? Color.green : CanControl(cctv) ? Color.cyan : Color.red;
                            DrawText(color, cctv.transform.position + new Vector3(0f, 0.3f, 0f), "CCTV", info);
                            DrawBox(color, cctv.transform.position, 0.25f);
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private bool CanControl(PoweredRemoteControlEntity entity)
            {
                if (!entity.IsPowered())
                {
                    return entity.IsStatic();
                }
                return True;
            }

            private int ShowCollectables()
            {
                if (!showCollectible || !instance.trackCollectibles)
                    return checks;

                entityType = EntityType.Collectibles;

                try
                {
                    foreach (var col in cache.Collectibles)
                    {
                        currDistance = (col.Key - source.transform.position).magnitude;

                        if (currDistance < instance.colDistance && currDistance < maxDistance)
                        {
                            string info = instance.showResourceAmounts ? string.Format("({0})", col.Value.Info) : string.Format("<color={0}>{1}</color>", instance.distCC, currDistance.ToString("0"));
                            DrawText(__(instance.colCC), col.Key + new Vector3(0f, 1f, 0f), instance._(col.Value.Name), info);
                            DrawBox(__(instance.colCC), col.Key + new Vector3(0f, 1f, 0f), col.Value.Size);
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private int ShowEntity(EntityType entityType, bool track, string entityName, List<BaseEntity> entities)
            {
                if (!track)
                    return checks;

                this.entityType = entityType;

                try
                {
                    if (entities.Count > 0)
                    {
                        foreach (var e in entities)
                        {
                            if (e == null || e.transform == null || e.IsDestroyed)
                                continue;

                            currDistance = (e.transform.position - source.transform.position).magnitude;

                            if (entityType == EntityType.Boats)
                            {
                                if (currDistance > instance.boatDistance) continue;
                                if (!instance.trackBoats && !instance.uiBtnBoats) continue;
                            }
                            else if (entityType == EntityType.RigidHullInflatableBoats)
                            {
                                if (currDistance > instance.boatDistance) continue;
                                if (!instance.trackRigidHullInflatableBoats && !instance.uiBtnRHIB) continue;
                            }
                            else if (entityType == EntityType.Cars)
                            {
                                if (currDistance > instance.carDistance) continue;
                                if (!instance.trackCars && !instance.uiBtnCars) continue;
                            }
                            else if (entityType == EntityType.MiniCopter)
                            {
                                if (currDistance > instance.mcDistance) continue;
                                if (!instance.trackMiniCopter && !instance.uiBtnMiniCopter) continue;
                            }
                            else if (entityType == EntityType.MLRS)
                            {
                                if (currDistance > instance.mlrsDistance) continue;
                                if (!instance.trackMLRS && !instance.uiBtnMLRS) continue;
                            }
                            else if (entityType == EntityType.RidableHorses)
                            {
                                if (currDistance > instance.rhDistance) continue;
                                if (!instance.trackRidableHorses && !instance.uiBtnRidableHorses) continue;
                            }
                            else if (entityType == EntityType.CH47Helicopters && !instance.trackCH47 && !instance.uiBtnCH47) continue;

                            if (e is ScrapTransportHelicopter) entityName = "STH";
                            else if (e is BaseSubmarine) entityName = "SUB";
                            string info = e.Health() <= 0 ? entityName : string.Format("{0} <color={1}>{2}</color>", entityName, instance.healthCC, e.Health() > 1000 ? Math.Floor(e.Health()).ToString("#,##0,K", CultureInfo.InvariantCulture) : Math.Floor(e.Health()).ToString("#0"));
                            Color color = e is ScrapTransportHelicopter ? __(instance.scrapCC) : e is MiniCopter ? __(instance.miniCC) : e is ModularCar ? Color.magenta : __(instance.bradleyCC);

                            DrawText(color, e.transform.position + new Vector3(0f, 2f, 0f), info, string.Format("<color={0}>{1}</color>", instance.distCC, currDistance.ToString("0")));
                            DrawBox(color, e.transform.position + new Vector3(0f, 1f, 0f), GetScale(currDistance));
                            checks++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }

                return checks;
            }

            private static float GetScale(float value)
            {
                return value * 0.02f;
            }
        }

        private bool IsRadar(string id)
        {
            foreach (var radar in activeRadars)
            {
                if (radar.player.UserIDString == id)
                {
                    return True;
                }
            }

            return False;
        }

        private string GetContents(List<Item> itemList)
        {
            return $"({string.Join(", ", itemList.Select(item => string.Format("{0} ({1})", item.info.displayName.english, item.amount)))}) ";
        }

        public string GetContents(ItemContainer[] containers)
        {
            if (containers == null)
            {
                return string.Empty;
            }

            var list = new List<string>();
            int count = 0;
            int amount = 0;

            foreach (var container in containers)
            {
                if (container == null || container.itemList == null) continue;

                count += container.itemList.Count;

                foreach (var item in container.itemList)
                {
                    list.Add(string.Format("{0} ({1})", item.info.displayName.translated.ToLower(), item.amount));

                    if (++amount >= corpseContentAmount)
                    {
                        break;
                    }
                }
            }

            if (corpseContentAmount > 0 && list.Count > 0)
            {
                return string.Format("{0} ({1})", string.Join(", ", list.ToArray()), count.ToString());
            }

            return count.ToString();
        }

        public string GetContents(DroppedItemContainer backpack)
        {
            if (backpack?.inventory?.itemList == null)
            {
                return string.Empty;
            }

            if (backpackContentAmount > 0 && backpack.inventory.itemList.Count > 0)
            {
                var list = new List<string>();
                int amount = 0;

                foreach (var item in backpack.inventory.itemList)
                {
                    list.Add(string.Format("{0} ({1})", item.info.displayName.translated.ToLower(), item.amount));

                    if (++amount >= backpackContentAmount)
                    {
                        break;
                    }
                }

                return string.Format("({0}) ({1}) ", string.Join(", ", list.ToArray()), backpack.inventory.itemList.Count.ToString());
            }

            return backpack.inventory.itemList.Count.ToString();
        }

        private void Init()
        {
            isUnloading = False;
            startupTime = Time.realtimeSinceStartup;
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnPlayerVoice));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            permission.RegisterPermission(permAllowed, this);
            permission.RegisterPermission(permBypass, this);
            permission.RegisterPermission(permAuto, this);
            permission.RegisterPermission(permBypassOverride, this);
            permission.RegisterPermission(permList, this);
        }

        private void OnServerInitialized()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch { }

            if (storedData == null)
                storedData = new StoredData();

            LoadVariables();

            if (!drawBox && !drawText && !drawArrows)
            {
                Puts("Configuration does not have a chosen drawing method. Setting drawing method to text.");
                Config.Set("Drawing Methods", "Draw Text", True);
                Config.Save();
                drawText = True;
            }

            if (useVoiceDetection)
            {
                Subscribe(nameof(OnPlayerVoice));
            }

            Subscribe(nameof(OnPlayerDisconnected));
            Subscribe(nameof(OnPlayerConnected));

            if (barebonesMode)
            {
                return;
            }

            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnPlayerSleepEnded));

            _coroutine = ServerMgr.Instance.StartCoroutine(FillCache());

            float time = 0f;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (storedData.Active.Contains(player.UserIDString))
                {
                    player.Invoke(() =>
                    {
                        RadarCommandX(player, "radar", storedData.Filters.ContainsKey(player.UserIDString) ? storedData.Filters[player.UserIDString].ToArray() : new string[0]);
                    }, time += 0.1f);
                }
            }

            SetupClanTeamColors();
        }

        public void AdminCommand(BasePlayer player, Action action)
        {
            bool isAdmin = player.IsAdmin;

            if (!isAdmin)
            {
                blocked.Add(player.userID);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, True);
                player.SendNetworkUpdateImmediate();
            }
            try
            {
                action();
            }
            finally
            {
                if (!isAdmin)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, False);
                    player.SendNetworkUpdateImmediate();
                    blocked.Remove(player.userID);
                }
            }
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin || !blocked.Contains(player.userID)) return null;
            return True;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            return OnPlayerCommand(arg.Player(), string.Empty, new string[0]);
        }

        private Dictionary<ulong, string> _teamColors = new Dictionary<ulong, string>();
        private Dictionary<string, string> _clanColors = new Dictionary<string, string>();
        private Dictionary<ulong, string> _clans = new Dictionary<ulong, string>();

        private string GetClanColor(ulong targetId)
        {
            string clan = GetClanOf(targetId);

            if (string.IsNullOrEmpty(clan) || !_clanColors.ContainsKey(clan))
            {
                return null;
            }

            return _clanColors[clan];
        }

        private Dictionary<string, string> GetAllClanColors() => _clanColors;

        private string GetTeamColor(ulong teamId)
        {
            if (teamId.IsSteamId())
            {
                RelationshipManager.PlayerTeam team;
                if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(teamId, out team))
                {
                    return null;
                }

                teamId = team.teamID;
            }

            if (!_teamColors.ContainsKey(teamId))
            {
                return null;
            }

            return _teamColors[teamId];
        }

        private Dictionary<ulong, string> GetAllTeamColors() => _teamColors;

        private string GetClanOf(ulong playerId)
        {
            string clan;
            if (_clans.TryGetValue(playerId, out clan))
            {
                return clan;
            }

            clan = Clans?.Call("GetClanOf", playerId) as string;

            if (string.IsNullOrEmpty(clan))
            {
                return string.Empty;
            }

            _clans[playerId] = clan;

            return clan;
        }

        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
        {
            string hex = $"#{Core.Random.Range(0x1000000):X6}";
            _teamColors[team.teamID] = hex;
            Interface.CallHook("OnTeamCreatedColor", team.teamID, hex);
        }

        private void OnClanCreate(string tag)
        {
            string hex = $"#{Core.Random.Range(0x1000000):X6}";
            _clanColors[tag] = hex;
            Interface.CallHook("OnClanCreateColor", tag, hex);
        }

        private void OnClanUpdate(string tag) => UpdateClans(tag);

        private void OnClanDestroy(string tag) => UpdateClans(tag);

        private void OnClanDisbanded(string tag) => UpdateClans(tag);

        private void UpdateClans(string tag)
        {
            var clans = new Dictionary<ulong, string>();

            foreach (var clan in _clans)
            {
                if (clan.Value != tag)
                {
                    clans[clan.Key] = clan.Value;
                }
            }

            _clans = clans;
        }

        private void SetupClanTeamColors()
        {
            foreach (var team in RelationshipManager.ServerInstance.teams)
            {
                _teamColors[team.Key] = $"#{Core.Random.Range(0x1000000):X6}";
            }

            Interface.CallHook("OnTeamColorsInitialized", _teamColors);

            var clans = Clans?.Call("GetAllClans");

            if (clans is JArray)
            {
                foreach (var token in (JArray)clans)
                {
                    _clanColors[token.ToString()] = $"#{Core.Random.Range(0x1000000):X6}";
                }
            }

            Interface.CallHook("OnClanColorsInitialized", _clanColors);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            accessList.RemoveAll(p => p == null || p == player || !p.IsConnected);

            if (player.IsAdmin || HasAccess(player))
            {
                accessList.Add(player);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!HasAccess(player))
            {
                return;
            }

            if (player.IsValid() && player.IsConnected && player.GetComponent<Radar>() == null && permission.UserHasPermission(player.UserIDString, permAuto))
            {
                RadarCommandX(player, "radar", new string[0]);
            }

            if (showUI && !barebonesMode)
            {
                if (radarUI.Contains(player.UserIDString))
                {
                    DestroyUI(player);
                }

                if (!storedData.Hidden.Contains(player.UserIDString))
                {
                    foreach (var radar in activeRadars)
                    {
                        if (radar.player == player)
                        {
                            CreateUI(player, radar, radar.showAll);
                            break;
                        }
                    }
                }
            }
        }

        void OnPlayerVoice(BasePlayer player, byte[] data)
        {
            ulong userId = player.userID;

            if (voices.ContainsKey(userId))
                voices[userId].Reset();
            else voices.Add(userId, timer.Once(VoiceDelay, () => voices.Remove(userId)));
        }

        float VoiceDelay
        {
            get
            {
                float delay = defaultInvokeTime;

                foreach (var radar in activeRadars)
                {
                    delay = Mathf.Max(radar.invokeTime, delay);
                }

                return voiceInterval + delay + flickerDelay;
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (useVoiceDetection && voices.ContainsKey(player.userID))
            {
                voices[player.userID]?.Destroy();
                voices.Remove(player.userID);
            }

            accessList.RemoveAll(p => p == null || p == player || !p.IsConnected);
        }

        private void OnPlayerTrackStarted(BasePlayer player, ulong targetId)
        {
            List<ulong> list;
            if (!trackers.TryGetValue(player, out list))
            {
                trackers[player] = list = new List<ulong>();
            }

            if (!list.Contains(targetId))
            {
                list.Add(targetId);
            }
        }

        private void OnPlayerTrackEnded(BasePlayer player, ulong targetId)
        {
            List<ulong> list;
            if (!trackers.TryGetValue(player, out list))
            {
                return;
            }

            list.Remove(targetId);
        }

        private Dictionary<BasePlayer, List<ulong>> trackers = new Dictionary<BasePlayer, List<ulong>>();

        private void Unload()
        {
            isUnloading = True;
            StopFillCache();
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

            foreach (var radar in activeRadars.ToList())
            {
                radar.StopAll();
                UnityEngine.Object.Destroy(radar);
            }

            tags.Clear();
            voices.Clear();
            activeRadars.Clear();
            uiBtnNames = new string[0];
            authorized.Clear();
            itemExceptions.Clear();
            groupColors.Clear();
            cooldowns.Clear();
        }

        private object BlockDamage(HitInfo hitInfo, BasePlayer player, bool flag, string key)
        {
            if (!flag)
            {
                return null;
            }

            if (!warnings.Contains(player.UserIDString))
            {
                string playerId = player.UserIDString;

                warnings.Add(playerId);
                Message(player, msg(key, playerId));
                timer.Once(5f, () => warnings.Remove(playerId));
            }

            hitInfo.damageTypes = new DamageTypeList();
            hitInfo.DidHit = False;
            hitInfo.HitEntity = null;
            hitInfo.Initiator = null;
            hitInfo.DoHitEffects = False;
            hitInfo.HitMaterial = 0;

            return True;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!entity.IsValid() || hitInfo == null)
            {
                return null;
            }

            var attacker = hitInfo?.Initiator as BasePlayer;

            if (!attacker || !IsRadar(attacker.UserIDString) || !attacker.IsConnected)
            {
                return null;
            }

            if (entity is BaseNpc)
            {
                return BlockDamage(hitInfo, attacker, blockDamageAnimals, "CantHurtAnimals");
            }
            else if (entity is BuildingBlock)
            {
                return BlockDamage(hitInfo, attacker, blockDamageBuildings, "CantDamageBuilds");
            }
            else if (entity is BasePlayer)
            {
                return BlockDamage(hitInfo, attacker, blockDamagePlayers && entity.ToPlayer().userID.IsSteamId(), "CantHurtPlayers");
            }
            else if (entity.IsNpc)
            {
                return BlockDamage(hitInfo, attacker, blockDamageNpcs, "CantHurtNpcs");
            }
            else if (blockDamageOther)
            {
                return BlockDamage(hitInfo, attacker, blockDamageOther, "CantHurtOther");
            }

            return null;
        }

        private void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            RemoveFromCache(entity as BaseEntity);
        }

        private void OnEntityKill(BaseEntity entity)
        {
            RemoveFromCache(entity);
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            AddToCache(entity);
        }

        private static bool IsHex(string value)
        {
            foreach (char c in value)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return False;
                }
            }

            return True;
        }

        private static Color __(string value)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(IsHex(value) ? "#" + value : value, out color))
            {
                color = Color.white;
            }

            return color;
        }

        StringBuilder sb;

        private string _(string value)
        {
            sb = new StringBuilder(value);

            foreach (string str in tags)
            {
                sb.Replace(str, string.Empty);
            }

            return sb.ToString();
        }

        private void StopFillCache()
        {
            if (_coroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(_coroutine);
                _coroutine = null;
            }
        }

        private IEnumerator FillCache()
        {
            var tick = DateTime.Now;
            int cached = 0, total = 0;

            foreach (var e in BaseNetworkable.serverEntities)
            {
                if (AddToCache(e as BaseEntity))
                {
                    cached++;
                }

                if (++total % 50 == 0)
                {
                    yield return CoroutineEx.waitForFixedUpdate;
                }
            }

            Puts("Cached {0}/{1} entities in {2} seconds!", cached, total, (DateTime.Now - tick).TotalSeconds);
            _coroutine = null;
        }

        private bool AddToCache(BaseEntity entity)
        {
            if (entity == null || entity.transform == null || entity.IsDestroyed)
                return False;

            var position = entity.transform.position;

            if (trackMLRS && entity is MLRSRocket)
            {
                cache.MLRS.Add(entity);
                return True;
            }

            if (trackNPC)
            {
                if (trackRidableHorses && entity is RidableHorse)
                {
                    if (!cache.RidableHorse.Contains(entity))
                    {
                        cache.RidableHorse.Add(entity);
                        return True;
                    }
                }
                else if (entity is BaseNpc || entity is SimpleShark)
                {
                    if (!cache.Animals.Contains(entity))
                    {
                        cache.Animals.Add(entity);
                        return True;
                    }
                }
                else if (entity is BasePlayer)
                {
                    var player = entity as BasePlayer;

                    player.Invoke(() =>
                    {
                        if (!player.IsDestroyed && !player.userID.IsSteamId() && !cache.NPCPlayers.Contains(player))
                        {
                            cache.NPCPlayers.Add(player);
                        }
                    }, 0.1f);

                    return True;
                }
                else if (entity is Zombie && !cache.Zombies.Contains(entity as Zombie))
                {
                    cache.Zombies.Add(entity as Zombie);
                    return True;
                }
            }

            if (trackCCTV && entity is CCTV_RC)
            {
                if (!cache.CCTV.Contains(entity as CCTV_RC))
                {
                    cache.CCTV.Add(entity as CCTV_RC);
                    return True;
                }
            }
            else if (trackTC && entity is BuildingPrivlidge)
            {
                if (!cache.Cupboards.Contains(entity as BuildingPrivlidge))
                {
                    cache.Cupboards.Add(entity as BuildingPrivlidge);
                    return True;
                }
            }
            else if (entity is StorageContainer)
            {
                if (trackSupplyDrops && entity is SupplyDrop && !cache.SupplyDrops.Contains(entity as SupplyDrop))
                {
                    cache.SupplyDrops.Add(entity as SupplyDrop);
                    return True;
                }
                else if (IsBox(entity.ShortPrefabName) || IsLoot(entity.ShortPrefabName) || entity is LockedByEntCrate || entity.prefabID == 186002280) // vendingmachine.deployed
                {
                    if (!cache.Containers.Contains(entity as StorageContainer))
                    {
                        cache.Containers.Add(entity as StorageContainer);
                        return True;
                    }
                }
                return False;
            }
            else if (trackLoot && entity is DroppedItemContainer)
            {
                if (!cache.Backpacks.Contains(entity as DroppedItemContainer))
                {
                    cache.Backpacks.Add(entity as DroppedItemContainer);
                    return True;
                }
            }
            else if (trackCollectibles && entity is CollectibleEntity)
            {
                if (!cache.Collectibles.ContainsKey(position))
                {
                    var itemList = entity.GetComponent<CollectibleEntity>().itemList;
                    int sum = 0;

                    if (itemList != null)
                    {
                        foreach (var item in itemList)
                        {
                            sum += (int)item.amount;
                        }
                    }

                    cache.Collectibles.Add(position, new CachedInfo { Name = _(entity.ShortPrefabName), Size = 0.5f, Info = sum });
                    return True;
                }

                return False;
            }
            else if (trackOre && entity is OreResourceEntity)
            {
                if (!cache.Ores.ContainsKey(position))
                {
                    float amount = 0;

                    foreach (var item in entity.GetComponent<ResourceDispenser>().containedItems)
                    {
                        amount += item.amount;
                    }

                    cache.Ores.Add(position, new CachedInfo { Name = _(entity.ShortPrefabName), Info = amount });
                    return True;
                }

                return False;
            }
            else if (trackDead && entity is PlayerCorpse)
            {
                var corpse = entity as PlayerCorpse;

                if (!cache.Corpses.ContainsKey(corpse) && corpse.playerSteamID.IsSteamId())
                {
                    string contents = GetContents(corpse.containers);
                    cache.Corpses.Add(corpse, new CachedInfo { Name = corpse.parentEnt?.ToString() ?? corpse.playerSteamID.ToString(), Info = contents });
                    return True;
                }

                return False;
            }
            else if (trackBags && entity is SleepingBag) // && !(entity is SleepingBagCamper))
            {
                cache.Bags.RemoveAll(bag => bag == null || bag.IsDestroyed);

                if (!cache.Bags.Contains(entity) && !cache.Bags.Exists(bag => bag.Distance(entity) < 0.2f))
                {
                    cache.Bags.Add(entity);
                    return True;
                }

                return False;
            }
            else if (trackCargoPlanes && (entity is CargoPlane || entity.gameObject.name == "CrashPlane"))
            {
                cache.CargoPlanes.Remove(entity);
                cache.CargoPlanes.Add(entity);
                return True;
            }
            else if (trackHeli && entity is BaseHelicopter)
            {
                if (!cache.Helicopters.Contains(entity as BaseHelicopter))
                {
                    cache.Helicopters.Add(entity as BaseHelicopter);
                    return True;
                }
            }
            else if (trackBradley && entity is BradleyAPC)
            {
                if (!cache.BradleyAPCs.Contains(entity as BradleyAPC))
                {
                    cache.BradleyAPCs.Add(entity as BradleyAPC);
                    return True;
                }
            }
            else if (trackRigidHullInflatableBoats && entity is RHIB)
            {
                if (!cache.RHIB.Contains(entity))
                {
                    cache.RHIB.Add(entity);
                    return True;
                }
            }
            else if (trackBoats && ((entity is BaseBoat && !(entity is RHIB)) || entity is BaseSubmarine))
            {
                if (!cache.Boats.Contains(entity))
                {
                    cache.Boats.Add(entity);
                    return True;
                }
            }
            else if (entity is MiniCopter || entity is Drone)
            {
                if (!cache.MiniCopter.Contains(entity))
                {
                    cache.MiniCopter.Add(entity);
                    return True;
                }
            }
            else if (trackCH47 && entity is CH47Helicopter)
            {
                if (!cache.CH47.Contains(entity))
                {
                    cache.CH47.Add(entity);
                    return True;
                }
            }
            if (trackCargoShips && entity is CargoShip)
            {
                if (!cache.CargoShips.Contains(entity))
                {
                    cache.CargoShips.Add(entity);
                    return True;
                }
            }
            else if (trackCars && (entity is BasicCar || entity is ModularCar))
            {
                if (!cache.Cars.Contains(entity))
                {
                    cache.Cars.Add(entity);
                    return True;
                }
            }
            else if (trackTurrets && entity is AutoTurret)
            {
                if (!cache.Turrets.Contains(entity as AutoTurret))
                {
                    cache.Turrets.Add(entity as AutoTurret);
                    return True;
                }
            }

            return False;
        }

        private bool IsBox(string str)
        {
            if (trackBox)
            {
                return str.Contains("box") || str.Equals("heli_crate") || str.Contains("coffin") || str.Contains("stash") || str.Contains("vendingmachine.deployed");
            }

            return False;
        }

        private bool IsLoot(string str)
        {
            if (trackLoot)
            {
                return str.Contains("loot") || str.Contains("crate_") || str.Contains("trash") || str.Contains("hackable") || str.Contains("oil") || str.Contains("campfire") || str.Contains("_parts");
            }

            return False;
        }

        private void RemoveFromCache(BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (entity is MLRSRocket)
            {
                cache.MLRS.Remove(entity);
            }

            var position = entity.transform?.position ?? Vector3.zero;

            if (cache.Ores.ContainsKey(position))
                cache.Ores.Remove(position);
            else if (entity is StorageContainer)
                cache.Containers.Remove(entity as StorageContainer);
            else if (cache.Collectibles.ContainsKey(position))
                cache.Collectibles.Remove(position);
            else if (entity is BaseNpc || entity is SimpleShark)
                cache.Animals.Remove(entity);
            else if (entity is PlayerCorpse)
                cache.Corpses.Remove(entity as PlayerCorpse);
            else if (cache.Bags.Contains(entity))
                cache.Bags.Remove(entity);
            else if (entity is DroppedItemContainer)
                cache.Backpacks.Remove(entity as DroppedItemContainer);
            else if (entity is BaseHelicopter)
                cache.Helicopters.Remove(entity as BaseHelicopter);
            else if (cache.Turrets.Contains(entity as AutoTurret))
                cache.Turrets.Remove(entity as AutoTurret);
            else if (entity is Zombie)
                cache.Zombies.Remove(entity as Zombie);
            else if (entity is CargoShip)
                cache.CargoShips.Remove(entity);
            else if (entity is BasicCar || entity is ModularCar)
                cache.Cars.Remove(entity);
            else if (entity is CH47Helicopter)
                cache.CH47.Remove(entity);
            else if (entity is RHIB)
                cache.RHIB.Remove(entity);
            else if (entity is BaseBoat || entity is BaseSubmarine)
                cache.Boats.Remove(entity);
            else if (entity is RidableHorse)
                cache.RidableHorse.Remove(entity);
            else if (entity is MiniCopter || entity is Drone)
                cache.MiniCopter.Remove(entity);
            else if (entity is CargoPlane)
                cache.CargoPlanes.Remove(entity);
            else if (entity is CCTV_RC)
                cache.CCTV.Remove(entity as CCTV_RC);
        }

        private bool HasAccess(BasePlayer player)
        {
            if (player == null)
                return False;

            if (DeveloperList.Contains(player.userID))
                return True;

            if (authorized.Count > 0)
                return authorized.Contains(player.UserIDString);

            if (permission.UserHasPermission(player.UserIDString, permAllowed))
                return True;

            if (player.IsConnected && player.net.connection.authLevel >= authLevel)
                return True;

            return False;
        }

        [ConsoleCommand("espgui")]
        private void ccmdESPGUI(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
                return;

            var player = arg.Player();

            if (!player || !HasAccess(player))
                return;

            RadarCommandX(player, "espgui", arg.Args);
        }

        private void RadarCommand(IPlayer user, string command, string[] args)
        {
            var player = user.Object as BasePlayer;

            if (!player || !HasAccess(player))
            {
                return;
            }

            RadarCommandX(player, command, args);
        }

        private bool DestroyRadar(BasePlayer player)
        {
            foreach (var x in activeRadars)
            {
                if (x.player == player)
                {
                    storedData.Active.Remove(player.UserIDString);
                    UnityEngine.Object.Destroy(x);
                    return True;
                }
            }
            return False;
        }

        private void RadarCommandX(BasePlayer player, string command, string[] args)
        {
            var list = args.ToLower(x => x != "True");
            bool hasAccess = HasAccess(player);

            args = list.ToArray();

            if (args.Contains("list") && (hasAccess || permission.UserHasPermission(player.UserIDString, permList)))
            {
                Message(player, msg("ActiveRadars", player.UserIDString, string.Join(", ", activeRadars.Select(x => x.playerName))));
            }

            if (!hasAccess)
            {
                if (player.Connection.authLevel > 0) Message(player, msg("NotAllowed", player.UserIDString));
                else Message(player, $"Unknown command: {command}");
                return;
            }

            if (args.Length == 0 && DestroyRadar(player))
            {
                return;
            }

            if (args.Length >= 1)
            {
                switch (args[0])
                {
                    case "drops":
                        {
                            DrawDrops(player);
                        }
                        return;
                    case "online":
                        {
                            if (!storedData.OnlineBoxes.Remove(player.UserIDString))
                                storedData.OnlineBoxes.Add(player.UserIDString);

                            Message(player, msg(storedData.OnlineBoxes.Contains(player.UserIDString) ? "BoxesOnlineOnly" : "BoxesAll", player.UserIDString));
                        }
                        return;
                    case "vision":
                        {
                            if (!storedData.Visions.Remove(player.UserIDString))
                                storedData.Visions.Add(player.UserIDString);

                            Message(player, msg(storedData.Visions.Contains(player.UserIDString) ? "VisionOn" : "VisionOff", player.UserIDString));
                        }
                        return;
                    case "train":
                        {
                            if (player.IsAdmin)
                            {
                                foreach (TrainCar train in BaseNetworkable.serverEntities.OfType<TrainCar>())
                                {
                                    timer.Repeat(1f, 60, () =>
                                    {
                                        player.SendConsoleCommand("ddraw.text", 1f, Color.red, train.transform.position, train.GetType().Name);
                                    });
                                }
                            }
                        }
                        return;
                    case "buildings":
                        {
                            DrawBuildings(player, args.Contains("raid"), args.Contains("twig"));
                        }
                        return;
                    case "findbyid":
                        {
                            FindByID(player, args);
                        }
                        return;
                    case "ext":
                    case "extend":
                    case "extended":
                        {
                            if (!storedData.Extended.Remove(player.UserIDString))
                                storedData.Extended.Add(player.UserIDString);

                            Message(player, msg(storedData.Extended.Contains(player.UserIDString) ? "ExtendedPlayersOn" : "ExtendedPlayersOff", player.UserIDString));
                        }
                        return;
                    case "setanchormin":
                        {
                            if (args.Length >= 1)
                            {
                                if (args.Length == 3)
                                {
                                    anchorMin = $"{args[1]} {args[2]}";

                                    foreach (var x in activeRadars)
                                    {
                                        if (x.player == player)
                                        {
                                            DestroyUI(player);
                                            CreateUI(player, x, isArg(args, "all"));
                                        }
                                    }
                                }

                                Message(player, anchorMin);
                            }
                        }
                        return;
                    case "setanchormax":
                        {
                            if (args.Length >= 1)
                            {
                                if (args.Length == 3)
                                {
                                    anchorMax = $"{args[1]} {args[2]}";

                                    foreach (var x in activeRadars)
                                    {
                                        if (x.player == player)
                                        {
                                            DestroyUI(player);
                                            CreateUI(player, x, isArg(args, "all"));
                                        }
                                    }
                                }

                                Message(player, anchorMax);
                            }
                        }
                        return;
                    case "anchors_save":
                        {
                            Config.Set("GUI", "Anchor Min", anchorMin);
                            Config.Set("GUI", "Anchor Max", anchorMax);
                            Config.Save();
                            Message(player, $"Saved: {anchorMin} {anchorMax}");
                        }
                        return;
                    case "anchors_reset":
                        {
                            anchorMin = anchorMinDefault;
                            anchorMax = anchorMaxDefault;
                            Config.Set("GUI", "Anchor Min", anchorMin);
                            Config.Set("GUI", "Anchor Max", anchorMax);
                            Config.Save();
                            Message(player, $"Reset: {anchorMin} {anchorMax}");
                        }
                        return;
                    case "tracker":
                        {
                            Message(player, "Feature removed. Use Player Tracker plugin.");
                        }
                        return;
                    case "help":
                        {
                            Message(player, msg("Help1", player.UserIDString, string.Join(", ", GetButtonNames) + ", HT"));
                            Message(player, msg("Help2", player.UserIDString, command, "online"));
                            Message(player, msg("Help3", player.UserIDString, command, "ui"));
                            Message(player, msg("Help7", player.UserIDString, command, "vision"));
                            Message(player, msg("Help8", player.UserIDString, command, "ext"));
                            Message(player, msg("Help9", player.UserIDString, command, lootDistance));
                            Message(player, msg("Help5", player.UserIDString, command));
                            Message(player, msg("Help6", player.UserIDString, command));
                            Message(player, msg("PreviousFilter", player.UserIDString, command));
                        }
                        return;
                    case "find":
                        if (args.Length > 1)
                        {
                            DrawObjects(player, args[1]);
                            return;
                        }
                        break;
                }
            }

            if (!storedData.Filters.ContainsKey(player.UserIDString))
                storedData.Filters.Add(player.UserIDString, list);

            if (args.Length >= 1)
            {
                if (args[0].Contains("ui"))
                {
                    if (storedData.Filters[player.UserIDString].Contains(args[0]))
                        storedData.Filters[player.UserIDString].Remove(args[0]);

                    if (storedData.Hidden.Contains(player.UserIDString))
                    {
                        storedData.Hidden.Remove(player.UserIDString);
                        Message(player, msg("GUIShown", player.UserIDString));
                    }
                    else
                    {
                        storedData.Hidden.Add(player.UserIDString);
                        Message(player, msg("GUIHidden", player.UserIDString));
                    }

                    args = storedData.Filters[player.UserIDString].ToArray();
                }
                else if (args[0] == "f")
                {
                    args = storedData.Filters[player.UserIDString].ToArray();
                }
            }

            if (command == "espgui")
            {
                string filter = storedData.Filters[player.UserIDString].Find(f => f.Equals(args[0])) ?? storedData.Filters[player.UserIDString].Find(f => f.Contains(args[0]) || args[0].Contains(f)) ?? args[0];

                if (storedData.Filters[player.UserIDString].Contains(filter))
                    storedData.Filters[player.UserIDString].Remove(filter);
                else
                    storedData.Filters[player.UserIDString].Add(filter);

                args = storedData.Filters[player.UserIDString].ToArray();
            }
            else
            {
                if (coolDown > 0f)
                {
                    float time = Time.realtimeSinceStartup;

                    if (cooldowns.ContainsKey(player.userID))
                    {
                        float cooldown = cooldowns[player.userID] - time;

                        if (cooldown > 0)
                        {
                            Message(player, msg("WaitCooldown", player.UserIDString, cooldown));
                            return;
                        }
                        else cooldowns.Remove(player.userID);
                    }
                }

                storedData.Filters[player.UserIDString] = list;
            }

            Radar radar = player.GetComponent<Radar>();

            if (radar == null)
            {
                radar = player.gameObject.AddComponent<Radar>();

                radar.Init(this);
            }

            float invokeTime, maxDistance, outTime, outDistance;

            radar.barebonesMode = barebonesMode;

            if (args.Length > 0 && float.TryParse(args[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out outTime))
                invokeTime = outTime < 0.1f ? 0.1f : outTime;
            else
                invokeTime = defaultInvokeTime;

            if (args.Length > 1 && float.TryParse(args[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out outDistance))
                maxDistance = outDistance <= 0f ? defaultMaxDistance : outDistance;
            else
                maxDistance = defaultMaxDistance;

            bool showAll = isArg(args, "all");
            radar.showAll = showAll;
            radar.showBags = isArg(args, "bag") || showAll;
            radar.showBoats = isArg(args, "boats") || showAll || (!uiBtnBoats && trackBoats);
            radar.showBox = isArg(args, "box") || showAll;
            radar.showBradley = isArg(args, "bradley") || showAll || (!uiBtnBradley && trackBradley);
            radar.showCargoPlanes = isArg(args, "cargoplane") || showAll || (!uiBtnCargoPlanes && trackCargoPlanes);
            radar.showCargoShips = isArg(args, "cargoship") || showAll || (!uiBtnCargoShips && trackCargoShips);
            radar.showCars = isArg(args, "cars") || showAll || (!uiBtnCars && trackCars);
            radar.showCCTV = isArg(args, "cctv") || showAll || (!uiBtnCCTV && trackCCTV);
            radar.showCH47 = isArg(args, "ch47") || showAll || (!uiBtnCH47 && trackCH47);
            radar.showCollectible = isArg(args, "col") || showAll;
            radar.showDead = isArg(args, "dead") || showAll;
            radar.showHeli = isArg(args, "heli") || showAll || (!uiBtnHeli && trackHeli);
            radar.showLoot = isArg(args, "loot") || showAll;
            radar.showMiniCopter = isArg(args, "mini") || showAll || (!uiBtnMiniCopter && trackMiniCopter);
            radar.showMLRS = isArg(args, "mlrs") || showAll || (!uiBtnMLRS && trackMLRS);
            radar.showNPC = isArg(args, "npc") || showAll;
            radar.showOre = isArg(args, "ore") || showAll;
            radar.showRidableHorses = isArg(args, "horse") || showAll || (!uiBtnRidableHorses && trackRidableHorses);
            radar.showRHIB = isArg(args, "rhib") || showAll || (!uiBtnRHIB && trackRigidHullInflatableBoats);
            radar.showSleepers = isArg(args, "sleeper") || showAll;
            radar.showStash = isArg(args, "stash") || showAll;
            radar.showTC = isArg(args, "tc", True) || showAll;
            radar.showTCArrow = isArg(args, "tcarrows", True) || showAll;
            radar.showTurrets = isArg(args, "turret") || showAll;
            radar.showHT = isArg(args, "ht");

            if (showUI && !barebonesMode)
            {
                if (radarUI.Contains(player.UserIDString))
                {
                    DestroyUI(player);
                }

                if (!storedData.Hidden.Contains(player.UserIDString))
                {
                    CreateUI(player, radar, showAll);
                }
            }

            radar.invokeTime = invokeTime;
            radar.maxDistance = maxDistance;
            radar.Start();

            if (!storedData.Active.Contains(player.UserIDString))
                storedData.Active.Add(player.UserIDString);

            if (command == "espgui" || !showToggleMessage)
                return;

            Message(player, msg("Activated", player.UserIDString, invokeTime, maxDistance, command));
        }

        private bool isArg(string[] args, string arg, bool equalTo = False)
        {
            for (int j = 0; j < args.Length; j++)
            {
                if (equalTo)
                {
                    if (args[j].Equals(arg))
                    {
                        return True;
                    }
                }
                else if (args[j].Contains(arg) || arg.Contains(args[j]))
                {
                    return True;
                }
            }

            return False;
        }

        private void FindByID(BasePlayer player, string[] args)
        {
            if (args.Length != 2)
            {
                Player.Message(player, "/radar findbyid id");
                return;
            }

            ulong userID;
            if (!ulong.TryParse(args[1], out userID))
            {
                Player.Message(player, $"Invalid steam id: {userID}");
                return;
            }

            AdminCommand(player, () =>
            {
                foreach (var e in BaseNetworkable.serverEntities.OfType<BaseEntity>())
                {
                    if (e is BuildingPrivlidge && (e as BuildingPrivlidge).IsAuthed(userID))
                    {
                        player.SendConsoleCommand("ddraw.text", 180f, Color.cyan, e.transform.position, userID);
                    }
                    else if (e?.OwnerID == userID || e is CodeLock && (e as CodeLock).whitelistPlayers.Contains(userID))
                    {
                        player.SendConsoleCommand("ddraw.text", 180f, Color.red, e.transform.position, userID);
                    }
                    else if (e is SleepingBag && (e as SleepingBag).deployerUserID == userID)
                    {
                        player.SendConsoleCommand("ddraw.text", 180f, Color.green, e.transform.position, userID);
                    }
                    else if (e is AutoTurret && (e as AutoTurret).IsAuthed(userID))
                    {
                        player.SendConsoleCommand("ddraw.text", 180f, Color.blue, e.transform.position, userID);
                    }
                }
            });
        }

        private void DrawBuildings(BasePlayer player, bool showNonPlayerBases, bool showTwigOnly)
        {
            AdminCommand(player, () =>
            {
                foreach (var building in BuildingManager.server.buildingDictionary.Values)
                {
                    if (!building.HasBuildingBlocks()) continue;
                    foreach (var block in building.buildingBlocks)
                    {
                        if (showNonPlayerBases && block.OwnerID.IsSteamId()) continue;
                        if (!showNonPlayerBases && !block.OwnerID.IsSteamId()) continue;
                        if (showTwigOnly && block.grade != BuildingGrade.Enum.Twigs) continue;
                        var targetName = covalence.Players.FindPlayerById(block.OwnerID.ToString())?.Name;
                        if (string.IsNullOrEmpty(targetName)) targetName = block.OwnerID.IsSteamId() ? block.OwnerID.ToString() : "No owner";
                        player.SendConsoleCommand("ddraw.text", 180f, Color.red, block.transform.position, $"<size={playerTextSize}>{targetName}</size>");
                        break;
                    }
                }
            });
        }

        private List<ulong> drawingObjectsFor = new List<ulong>();
        private const int MAX_ALLOWED_REQUESTS = 1;

        private void DrawObjects(BasePlayer player, string value)
        {
            if (drawingObjectsFor.Count >= MAX_ALLOWED_REQUESTS)
            {
                Message(player, msg("ProcessPendingAlready", player.UserIDString, MAX_ALLOWED_REQUESTS));
                return;
            }

            drawingObjectsFor.Add(player.userID);

            ServerMgr.Instance.StartCoroutine(DrawObjectsCoroutine(player, value));
        }

        private IEnumerator DrawObjectsCoroutine(BasePlayer player, string value)
        {
            Message(player, msg("ProcessRequest", player.UserIDString));
            int checks = 0;
            var objects = Pool.GetList<object[]>();
            foreach (var e in BaseNetworkable.serverEntities)
            {
                if (e == null || e.IsDestroyed)
                {
                    continue;
                }
                if (e.ShortPrefabName.Contains(value, CompareOptions.OrdinalIgnoreCase) || value == "electrical" && e is IOEntity)
                {
                    objects.Add(new object[3] { e.transform.position, e.ShortPrefabName, Mathf.FloorToInt((e.transform.position - player.transform.position).magnitude) });
                }
                if (++checks % 100 == 0)
                {
                    yield return CoroutineEx.waitForSeconds(0.0025f);
                }
            }
            AdminCommand(player, () =>
            {
                foreach (var obj in objects)
                {
                    player.SendConsoleCommand("ddraw.text", 60f, Color.red, obj[0], $"<size={playerTextSize}>{obj[1]} {obj[2]}</size>");
                }
            });
            Message(player, msg("ProcessRequestFinished", player.UserIDString, objects.Count));
            Pool.FreeList(ref objects);
            drawingObjectsFor.Remove(player.userID);
        }

        private void DrawDrops(BasePlayer player)
        {
            bool hasDrops = False;
            DroppedItem drop;
            double currDistance;

            AdminCommand(player, () =>
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (entity is DroppedItem || entity is Landmine || entity is BearTrap || entity is DroppedItemContainer)
                    {
                        if (entity == null || entity.IsDestroyed)
                        {
                            continue;
                        }

                        drop = entity as DroppedItem;
                        string shortname = drop?.item?.info.shortname ?? entity.ShortPrefabName;
                        currDistance = (entity.transform.position - player.transform.position).magnitude;

                        if (currDistance <= dropsDistance)
                        {
                            if (drawText) player.SendConsoleCommand("ddraw.text", 30f, Color.red, entity.transform.position, string.Format("{0} <color=#FFFF00>{1}</color>", shortname, currDistance.ToString("0")));
                            if (drawBox) player.SendConsoleCommand("ddraw.box", 30f, Color.red, entity.transform.position, 0.25f);
                            hasDrops = True;
                        }
                    }
                }

                if (!hasDrops)
                {
                    Message(player, msg("NoDrops", player.UserIDString, lootDistance));
                }
            });
        }

        #region UI

        private string[] uiBtnNames = new string[0];
        private Dictionary<int, UIButton> uiButtons;
        private List<string> radarUI = new List<string>();
        private const string UI_PanelName = "AdminRadar_UI";

        public void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_PanelName);
            radarUI.Remove(player.UserIDString);
        }

        private void CreateUI(BasePlayer player, Radar esp, bool showAll)
        {
            var buttonNames = GetButtonNames;
            var buttons = CreateButtons;
            string aMin = anchorMin;
            string aMax = anchorMax;

            if (buttons.Count > 12)
            {
                double anchorMinX;
                if (double.TryParse(anchorMin.Split(' ')[0], out anchorMinX))
                {
                    double anchorMinY;
                    if (double.TryParse(anchorMin.Split(' ')[1], out anchorMinY))
                    {
                        double anchorMaxX;
                        if (double.TryParse(anchorMax.Split(' ')[0], out anchorMaxX))
                        {
                            double anchorMaxY;
                            if (double.TryParse(anchorMax.Split(' ')[1], out anchorMaxY))
                            {
                                if (buttons.Count >= 13 && buttons.Count <= 16)
                                {
                                    anchorMinX += 0.010;
                                    anchorMinY += 0.0215;
                                    anchorMaxX -= 0.0135;
                                    anchorMaxY -= 0.0135;

                                    aMin = string.Format("{0} {1}", anchorMinX, anchorMinY);
                                    aMax = string.Format("{0} {1}", anchorMaxX, anchorMaxY);
                                }
                                else if (buttons.Count >= 17)
                                {
                                    anchorMinX -= 0.024;
                                    anchorMinY += 0.0175;
                                    anchorMaxX -= 0.0305;
                                    anchorMaxY -= 0.0305;

                                    aMin = string.Format("{0} {1}", anchorMinX, anchorMinY);
                                    aMax = string.Format("{0} {1}", anchorMaxX, anchorMaxY);
                                }
                            }
                        }
                    }
                }
            }

            var element = UI.CreateElementContainer(UI_PanelName, "0 0 0 0.0", aMin, aMax, False);
            int fontSize = buttons.Count > 12 ? 8 : 10;

            for (int x = 0; x < buttonNames.Length; x++)
            {
                UI.CreateButton(ref element, UI_PanelName, esp.GetBool(buttonNames[x]) ? uiColorOn : uiColorOff, msg(buttonNames[x], player.UserIDString), fontSize, buttons[x].Anchor, buttons[x].Offset, "espgui " + buttonNames[x]);
            }

            if (element == null || element.Count == 0)
            {
                return;
            }

            radarUI.Add(player.UserIDString);
            CuiHelper.AddUi(player, element);
        }

        public string[] GetButtonNames
        {
            get
            {
                var list = new List<string>() { "All" };

                if (uiBtnBags) list.Add("Bags");
                if (uiBtnBoats) list.Add("Boats");
                if (uiBtnBox) list.Add("Box");
                if (uiBtnBradley) list.Add("Bradley");
                if (uiBtnCargoPlanes) list.Add("CargoPlanes");
                if (uiBtnCargoShips) list.Add("CargoShips");
                if (uiBtnCars) list.Add("Cars");
                if (uiBtnCCTV) list.Add("CCTV");
                if (uiBtnCH47) list.Add("CH47");
                if (uiBtnCollectible) list.Add("Collectibles");
                if (uiBtnDead) list.Add("Dead");
                if (uiBtnHeli) list.Add("Heli");
                if (uiBtnLoot) list.Add("Loot");
                if (uiBtnMiniCopter) list.Add("MiniCopter");
                if (uiBtnMLRS) list.Add("MLRS");
                if (uiBtnNPC) list.Add("NPC");
                if (uiBtnOre) list.Add("Ore");
                if (uiBtnRidableHorses) list.Add("Horses");
                if (uiBtnRHIB) list.Add("RHIB");
                if (uiBtnSleepers) list.Add("Sleepers");
                if (uiBtnStash) list.Add("Stash");
                if (uiBtnTC) list.Add("TC");
                if (uiBtnTCArrow) list.Add("TCArrows");
                if (uiBtnTurrets) list.Add("Turrets");

                return uiBtnNames = list.ToArray();
            }
        }

        public class UIButton
        {
            public string Anchor { get; set; }
            public string Offset { get; set; }
        }

        public Dictionary<int, UIButton> CreateButtons
        {
            get
            {
                uiButtons = new Dictionary<int, UIButton>();

                int amount = uiBtnNames.Length;
                double anchorMin = amount > 12 ? 0.011 : 0.017;
                double anchorMax = amount > 12 ? 0.675 : 0.739;
                double offsetMin = amount > 12 ? 0.275 : 0.331;
                double offsetMax = amount > 12 ? 0.957 : 0.957;
                double defaultAnchorMax = anchorMax;
                double defaultOffsetMax = offsetMax;
                int rowMax = 4;

                for (int count = 0; count < amount; count++)
                {
                    if (count > 0 && count % rowMax == 0)
                    {
                        anchorMax = defaultAnchorMax;
                        offsetMax = defaultOffsetMax;
                        anchorMin += (amount > 12 ? 0.280 : 0.326);
                        offsetMin += (amount > 12 ? 0.280 : 0.326);
                    }

                    uiButtons[count] = new UIButton
                    {
                        Anchor = $"{anchorMin} {anchorMax}",
                        Offset = $"{offsetMin} {offsetMax}",
                    };

                    anchorMax -= (amount > 12 ? 0.329 : 0.239);
                    offsetMax -= (amount > 12 ? 0.329 : 0.239);
                }

                return uiButtons;
            }
        }

        public class UI // Credit: Absolut
        {
            public static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = False, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image =
                            {
                                Color = color
                            },
                            RectTransform =
                            {
                                AnchorMin = aMin,
                                AnchorMax = aMax
                            },
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }

            public static void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, string labelColor = "")
            {
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Color = color,
                        Command = command,
                        FadeIn = 1.0f
                    },
                    RectTransform =
                    {
                        AnchorMin = aMin,
                        AnchorMax = aMax
                    },
                    Text =
                    {
                        Text = text,
                        FontSize = size,
                        Align = align,
                        Color = labelColor
                    }
                }, panel);
            }
        }

        #endregion

        #region Config

        private bool Changed;
        private bool barebonesMode;
        private int averagePingInterval;
        private bool drawText = True;
        private bool drawBox;
        private bool drawArrows;
        private string colorDrawArrows;
        private bool drawX;
        private int authLevel;
        private float defaultInvokeTime;
        private float defaultMaxDistance;
        private int prefixSize;
        private int textSize;
        private int playerPrefixSize;
        private int playerTextSize;
        private bool drawTargetsVictim;
        private bool ctNameColor;
        private float mcDistance;
        private float mlrsDistance;
        private float carDistance;
        private float boatDistance;
        private float adDistance;
        private float boxDistance;
        private float playerDistance;
        private float npcPlayerDistance;
        private float tcDistance;
        private float tcArrowsDistance;
        private float stashDistance;
        private float corpseDistance;
        private float oreDistance;
        private float rhDistance;
        private float lootDistance;
        private float colDistance;
        private float cctvDistance;
        private float bagDistance;
        private float npcDistance;
        private float turretDistance;
        private float vmDistance;
        private float dropsDistance;
        private bool showLootContents;
        private bool showAirdropContents;
        private bool showStashContents;
        private bool drawEmptyContainers;
        private bool showResourceAmounts;
        private bool showHeliRotorHealth;
        private int backpackContentAmount;
        private int corpseContentAmount;
        private int groupLimit;
        private float groupRange;
        private float groupCountHeight;
        private int inactiveSeconds;
        private int inactiveMinutes;
        private bool showUI;
        private bool showTCAuthedCount;
        private bool showTCBagCount;

        private string distCC;
        private string heliCC;
        private string bradleyCC;
        private string miniCC;
        private string scrapCC;
        private string activeCC;
        private string activeUndergroundCC;
        private string activeFlyingCC;
        private string activeDeadCC;
        private string corpseCC;
        private string sleeperCC;
        private string sleeperDeadCC;
        private string healthCC;
        private string backpackCC;
        private string zombieCC;
        private string scientistCC;
        private string peacekeeperCC;
        private string murdererCC;
        private string npcCC;
        private string resourceCC;
        private string colCC;
        private string tcCC;
        private string bagCC;
        private string airdropCC;
        private string atCC;
        private string boxCC;
        private string lootCC;
        private string stashCC;
        private string groupColorDead;
        private string groupColorBasic;
        private string uiColorOn;
        private string uiColorOff;

        private string szRadarCommand;
        private string szSecondaryCommand;
        private List<object> authorized = new List<object>();
        private List<string> itemExceptions = new List<string>();

        private bool trackActive = True; // default tracking
        private bool trackBags = True;
        private bool trackBox = True;
        private bool trackCollectibles = True;
        private bool trackCCTV = True;
        private bool trackDead = True;
        private bool trackLoot = True;
        private bool trackNPC = True;
        private bool trackOre = True;
        private bool trackSleepers = True;
        private bool trackStash = True;
        private bool trackSupplyDrops = True;
        private bool trackTC = True;
        private bool trackTurrets = True;

        private bool trackMiniCopter; // additional tracking
        private bool trackHeli;
        private bool trackBradley;
        private bool trackCars;
        private bool trackCargoPlanes;
        private bool trackCargoShips;
        private bool trackCH47;
        private bool trackMLRS;
        private bool trackRidableHorses;
        private bool trackRigidHullInflatableBoats;
        private bool trackBoats;

        //top left
        //"Anchor Max": "0.156 0.996",
        //"Anchor Min": "0.017 0.847",
        //top right
        //"Anchor Max": "0.969 1.025",
        //"Anchor Min": "0.821 0.887",
        //bottom right
        private const string anchorMinDefault = "0.667 0.020";
        private const string anchorMaxDefault = "0.810 0.148";
        private string anchorMin;
        private string anchorMax;
        private bool uiBtnBags;
        private bool uiBtnBoats;
        private bool uiBtnBox;
        private bool uiBtnBradley;
        private bool uiBtnCars;
        private bool uiBtnCCTV;
        private bool uiBtnCargoPlanes;
        private bool uiBtnCargoShips;
        private bool uiBtnCH47;
        private bool uiBtnCollectible;
        private bool uiBtnDead;
        private bool uiBtnHeli;
        private bool uiBtnLoot;
        private bool uiBtnMiniCopter;
        private bool uiBtnMLRS;
        private bool uiBtnNPC;
        private bool uiBtnOre;
        private bool uiBtnRidableHorses;
        private bool uiBtnRHIB;
        private bool uiBtnSleepers;
        private bool uiBtnStash;
        private bool uiBtnTC;
        private bool uiBtnTCArrow;
        private bool uiBtnTurrets;

        private bool blockAny => blockDamageAnimals || blockDamageBuildings || blockDamageNpcs || blockDamagePlayers || blockDamageOther;

        //string voiceSymbol;
        private bool useVoiceDetection;
        private int voiceInterval;
        private float voiceDistance;
        private bool skipUnderworld;
        private bool blockDamageBuildings;
        private bool blockDamageAnimals;
        private bool blockDamagePlayers;
        private bool blockDamageNpcs;
        private bool blockDamageOther;
        private float coolDown;
        private string _webhookUrl;
        private int _messageColor;
        private bool _sendDiscordMessages;
        private string _embedMessageTitle;
        private string _embedMessagePlayer;
        private string _embedMessageMessage;
        private string _embedMessageLocation;
        private string _embedMessageServer;
        private string _discordMessageToggleOn;
        private string _discordMessageToggleOff;
        private bool showToggleMessage;
        private bool trackBackpacks;
        private bool ShowRadar;
        private bool ShowGod;
        private bool ShowGodPlugin;
        private bool ShowVanish;
        private bool ShowNoClip;
        private string ShowRadarText;
        private string ShowGodText;
        private string ShowGodPluginText;
        private string ShowVanishText;
        private string ShowNoClipText;

        private List<object> ItemExceptions
        {
            get
            {
                return new List<object> { "bottle", "planner", "rock", "torch", "can.", "arrow." };
            }
        }

        private bool useGroupColors;
        private readonly Dictionary<int, string> groupColors = new Dictionary<int, string>();

        private string GetGroupColor(int index)
        {
            if (useGroupColors && groupColors.ContainsKey(index))
                return groupColors[index];

            return groupColorBasic;
        }

        private void SetupGroupColors(List<object> list)
        {
            if (list != null && list.Count > 0)
            {
                groupColors.Clear();

                Dictionary<string, object> dict;
                string value;
                int key;

                foreach (var entry in list)
                {
                    if (entry is Dictionary<string, object>)
                    {
                        dict = (Dictionary<string, object>)entry;

                        foreach (var kvp in dict)
                        {
                            key = 0;
                            if (int.TryParse(kvp.Key, out key))
                            {
                                value = kvp.Value.ToString();

                                if (__(value) == Color.red)
                                {
                                    if (__(activeDeadCC) == Color.red || __(sleeperDeadCC) == Color.red)
                                    {
                                        groupColors[key] = "#FF00FF"; // magenta
                                        continue;
                                    }
                                }

                                if (IsHex(value))
                                {
                                    value = "#" + value;
                                }

                                groupColors[key] = value;
                            }
                        }
                    }
                }
            }
        }

        private List<object> DefaultGroupColors
        {
            get
            {
                return new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["0"] = "#FF00FF", // magenta
                        ["1"] = "#008000", // green
                        ["2"] = "#0000FF", // blue
                        ["3"] = "#FFA500", // orange
                        ["4"] = "#FFFF00" // yellow
                    }
                };
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You are not allowed to use this command.",
                ["PreviousFilter"] = "To use your previous filter type <color=#FFA500>/{0} f</color>",
                ["Activated"] = "ESP Activated - {0}s refresh - {1}m distance. Use <color=#FFA500>/{2} help</color> for help.",
                ["Deactivated"] = "ESP Deactivated.",
                ["Exception"] = "ESP Tool: An error occured. Please check the server console.",
                ["GUIShown"] = "GUI will be shown",
                ["GUIHidden"] = "GUI will now be hidden",
                ["InvalidID"] = "{0} is not a valid steam id. Entry removed.",
                ["BoxesAll"] = "Now showing all boxes.",
                ["BoxesOnlineOnly"] = "Now showing online player boxes only.",
                ["Help1"] = "<color=#FFA500>Available Filters</color>: {0}",
                ["Help2"] = "<color=#FFA500>/{0} {1}</color> - Toggles showing online players boxes only when using the <color=#FF0000>box</color> filter.",
                ["Help3"] = "<color=#FFA500>/{0} {1}</color> - Toggles quick toggle UI on/off",
                ["Help5"] = "e.g: <color=#FFA500>/{0} 1 1000 box loot stash</color>",
                ["Help6"] = "e.g: <color=#FFA500>/{0} 0.5 400 all</color>",
                ["VisionOn"] = "You will now see where players are looking.",
                ["VisionOff"] = "You will no longer see where players are looking.",
                ["ExtendedPlayersOn"] = "Extended information for players is now on.",
                ["ExtendedPlayersOff"] = "Extended information for players is now off.",
                ["Help7"] = "<color=#FFA500>/{0} {1}</color> - Toggles showing where players are looking.",
                ["Help8"] = "<color=#FFA500>/{0} {1}</color> - Toggles extended information for players.",
                ["backpack"] = "backpack",
                ["scientist"] = "scientist",
                ["NoDrops"] = "No item drops found within {0}m",
                ["Help9"] = "<color=#FFA500>/{0} drops</color> - Show all dropped items within {1}m.",
                ["Zombie"] = "<color=#FF0000>Zombie</color>",
                ["NoActiveRadars"] = "No one is using Radar at the moment.",
                ["ActiveRadars"] = "Active radar users: {0}",
                ["All"] = "All",
                ["Bags"] = "Bags",
                ["Box"] = "Box",
                ["Collectibles"] = "Collectibles",
                ["Dead"] = "Dead",
                ["Loot"] = "Loot",
                ["Ore"] = "Ore",
                ["Sleepers"] = "Sleepers",
                ["Stash"] = "Stash",
                ["TC"] = "TC",
                ["Turrets"] = "Turrets",
                ["Bear"] = "Bear",
                ["Boar"] = "Boar",
                ["Chicken"] = "Chicken",
                ["Wolf"] = "Wolf",
                ["Stag"] = "Stag",
                ["Horse"] = "Horse",
                ["My Base"] = "My Base",
                ["scarecrow"] = "scarecrow",
                ["murderer"] = "murderer",
                ["CantDamageBuilds"] = "You can't damage buildings while using radar",
                ["CantHurtAnimals"] = "You can't hurt animals while using radar",
                ["CantHurtPlayers"] = "You can't hurt players while using radar",
                ["CantHurtNpcs"] = "You can't hurt npcs while using radar",
                ["CantHurtOther"] = "You can't hurt this while using radar",
                ["WaitCooldown"] = "You must wait {0} seconds to use this command again.",
                ["missionprovider_stables_a"] = "missions",
                ["missionprovider_stables_b"] = "missions",
                ["missionprovider_outpost_a"] = "missions",
                ["missionprovider_outpost_b"] = "missions",
                ["missionprovider_fishing_a"] = "missions",
                ["missionprovider_fishing_b"] = "missions",
                ["missionprovider_bandit_a"] = "missions",
                ["missionprovider_bandit_b"] = "missions",
                ["simpleshark"] = "shark",
                ["stables_shopkeeper"] = "shopkeeper",
                ["npc_underwaterdweller"] = "dweller",
                ["boat_shopkeeper"] = "shopkeeper",
                ["bandit_shopkeeper"] = "shopkeeper",
                ["outpost_shopkeeper"] = "shopkeeper",
                ["npc_bandit_guard"] = "guard",
                ["bandit_conversationalist"] = "vendor",
                ["ProcessRequest"] = "Processing request; this will take several seconds...",
                ["ProcessPendingAlready"] = "The max amount ({0}) of requests are pending already!",
                ["ProcessRequestFinished"] = "{0} entities were found.",
            }, this);
        }

        private void LoadVariables()
        {
            barebonesMode = Convert.ToBoolean(GetConfig("Settings", "Barebones Performance Mode", False));
            authorized = GetConfig("Settings", "Restrict Access To Steam64 IDs", new List<object>()) as List<object>;

            foreach (var auth in authorized)
            {
                if (auth == null || !auth.ToString().IsSteamId())
                {
                    PrintWarning(msg("InvalidID", null, auth == null ? "null" : auth.ToString()));
                }
            }

            authLevel = authorized.Count == 0 ? Convert.ToInt32(GetConfig("Settings", "Restrict Access To Auth Level", 1)) : int.MaxValue;
            defaultMaxDistance = Convert.ToSingle(GetConfig("Settings", "Default Distance", 500.0));
            defaultInvokeTime = Convert.ToSingle(GetConfig("Settings", "Default Refresh Time", 5.0));
            //latencyMs = Convert.ToInt32(GetConfig("Settings", "Latency Cap In Milliseconds (0 = no cap)", 1000.0));
            //objectsLimit = Convert.ToInt32(GetConfig("Settings", "Objects Drawn Limit (0 = unlimited)", 250));
            var exceptions = GetConfig("Settings", "Dropped Item Exceptions", ItemExceptions) as List<object>;

            foreach (var exception in exceptions)
            {
                itemExceptions.Add(exception.ToString());
            }

            inactiveSeconds = Convert.ToInt32(GetConfig("Settings", "Deactivate Radar After X Seconds Inactive", 300));
            inactiveMinutes = Convert.ToInt32(GetConfig("Settings", "Deactivate Radar After X Minutes", 0));
            showUI = Convert.ToBoolean(GetConfig("Settings", "User Interface Enabled", True));
            averagePingInterval = Convert.ToInt32(GetConfig("Settings", "Show Average Ping Every X Seconds [0 = disabled]", 0));
            coolDown = Convert.ToSingle(GetConfig("Settings", "Re-use Cooldown, Seconds", 0f));
            showToggleMessage = Convert.ToBoolean(GetConfig("Settings", "Show Radar Activated/Deactivated Messages", True));
            playerPrefixSize = Convert.ToInt32(GetConfig("Settings", "Player Name Text Size", 24));
            playerTextSize = Convert.ToInt32(GetConfig("Settings", "Player Information Text Size", 24));
            prefixSize = Convert.ToInt32(GetConfig("Settings", "Entity Name Text Size", 24));
            textSize = Convert.ToInt32(GetConfig("Settings", "Entity Information Text Size", 24));
            ctNameColor = Convert.ToBoolean(GetConfig("Settings", "Unique Clan/Team Color Applies To Entire Player Text", False));

            blockDamageAnimals = Convert.ToBoolean(GetConfig("When Radar Is Active", "Block Damage To Animals", False));
            blockDamageBuildings = Convert.ToBoolean(GetConfig("When Radar Is Active", "Block Damage To Buildings", False));
            blockDamageNpcs = Convert.ToBoolean(GetConfig("When Radar Is Active", "Block Damage To Npcs", False));
            blockDamagePlayers = Convert.ToBoolean(GetConfig("When Radar Is Active", "Block Damage To Players", False));
            blockDamageOther = Convert.ToBoolean(GetConfig("When Radar Is Active", "Block Damage To Everything Else", False));

            showLootContents = Convert.ToBoolean(GetConfig("Options", "Show Barrel And Crate Contents", False));
            showAirdropContents = Convert.ToBoolean(GetConfig("Options", "Show Airdrop Contents", False));
            showStashContents = Convert.ToBoolean(GetConfig("Options", "Show Stash Contents", False));
            drawEmptyContainers = Convert.ToBoolean(GetConfig("Options", "Draw Empty Containers", True));
            showResourceAmounts = Convert.ToBoolean(GetConfig("Options", "Show Resource Amounts", True));
            backpackContentAmount = Convert.ToInt32(GetConfig("Options", "Show X Items In Backpacks [0 = amount only]", 3));
            corpseContentAmount = Convert.ToInt32(GetConfig("Options", "Show X Items On Corpses [0 = amount only]", 0));
            skipUnderworld = Convert.ToBoolean(GetConfig("Options", "Only Show NPCPlayers At World View", False));
            showTCAuthedCount = Convert.ToBoolean(GetConfig("Options", "Show Authed Count On Cupboards", True));
            showTCBagCount = Convert.ToBoolean(GetConfig("Options", "Show Bag Count On Cupboards", True));
            drawTargetsVictim = Convert.ToBoolean(GetConfig("Options", "Show Npc Player Target", False));

            drawArrows = Convert.ToBoolean(GetConfig("Drawing Methods", "Draw Arrows On Players", False));
            drawBox = Convert.ToBoolean(GetConfig("Drawing Methods", "Draw Boxes", False));
            drawText = Convert.ToBoolean(GetConfig("Drawing Methods", "Draw Text", True));

            drawX = Convert.ToBoolean(GetConfig("Group Limit", "Draw Distant Players With X", True));
            groupLimit = Convert.ToInt32(GetConfig("Group Limit", "Limit", 4));
            groupRange = Convert.ToSingle(GetConfig("Group Limit", "Range", 50f));
            groupCountHeight = Convert.ToSingle(GetConfig("Group Limit", "Height Offset [0.0 = disabled]", 40f));

            mcDistance = Convert.ToSingle(GetConfig("Drawing Distances", "MiniCopter", 200f));
            mlrsDistance = Convert.ToSingle(GetConfig("Drawing Distances", "MLRS", 5000f));
            boatDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Boats", 150f));
            carDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Cars", 500f));
            adDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Airdrop Crates", 400f));
            npcDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Animals", 200));
            bagDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Sleeping Bags", 250));
            boxDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Boxes", 100));
            colDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Collectibles", 100));
            cctvDistance = Convert.ToSingle(GetConfig("Drawing Distances", "CCTV", 500));
            corpseDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Player Corpses", 200));
            playerDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Players", 500));
            npcPlayerDistance = Convert.ToSingle(GetConfig("Drawing Distances", "NPC Players", 300));
            lootDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Loot Containers", 150));
            oreDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Resources (Ore)", 200));
            rhDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Ridable Horses", 250));
            stashDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Stashes", 250));
            tcDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Tool Cupboards", 150));
            tcArrowsDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Tool Cupboard Arrows", 250));
            turretDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Turrets", 100));
            vmDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Vending Machines", 250));
            dropsDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Radar Drops Command", 150f));

            trackBradley = Convert.ToBoolean(GetConfig("Additional Tracking", "Bradley APC", True));
            trackCars = Convert.ToBoolean(GetConfig("Additional Tracking", "Cars", False));
            trackCargoPlanes = Convert.ToBoolean(GetConfig("Additional Tracking", "CargoPlanes", False));
            trackCargoShips = Convert.ToBoolean(GetConfig("Additional Tracking", "CargoShips", False));
            trackMiniCopter = Convert.ToBoolean(GetConfig("Additional Tracking", "MiniCopter", False));
            trackMLRS = Convert.ToBoolean(GetConfig("Additional Tracking", "MLRS", False));
            trackHeli = Convert.ToBoolean(GetConfig("Additional Tracking", "Helicopters", True));
            showHeliRotorHealth = Convert.ToBoolean(GetConfig("Additional Tracking", "Helicopter Rotor Health", False));
            trackCH47 = Convert.ToBoolean(GetConfig("Additional Tracking", "CH47", False));
            trackRidableHorses = Convert.ToBoolean(GetConfig("Additional Tracking", "Ridable Horses", False));
            trackRigidHullInflatableBoats = Convert.ToBoolean(GetConfig("Additional Tracking", "RHIB", False));
            trackBoats = Convert.ToBoolean(GetConfig("Additional Tracking", "Boats", False));
            trackCCTV = Convert.ToBoolean(GetConfig("Additional Tracking", "CCTV", False));
            trackBackpacks = Convert.ToBoolean(GetConfig("Additional Tracking", "Backpacks Plugin", False));

            colorDrawArrows = Convert.ToString(GetConfig("Color-Hex Codes", "Player Arrows", "#000000"));
            distCC = Convert.ToString(GetConfig("Color-Hex Codes", "Distance", "#ffa500"));
            heliCC = Convert.ToString(GetConfig("Color-Hex Codes", "Helicopters", "#ff00ff"));
            bradleyCC = Convert.ToString(GetConfig("Color-Hex Codes", "Bradley", "#ff00ff"));
            miniCC = Convert.ToString(GetConfig("Color-Hex Codes", "MiniCopter", "#ff00ff"));
            scrapCC = Convert.ToString(GetConfig("Color-Hex Codes", "MiniCopter (ScrapTransportHelicopter)", "#ff00ff"));
            activeCC = Convert.ToString(GetConfig("Color-Hex Codes", "Online Player", "#ffffff"));
            activeUndergroundCC = Convert.ToString(GetConfig("Color-Hex Codes", "Online Player (Underground)", "#ffffff"));
            activeFlyingCC = Convert.ToString(GetConfig("Color-Hex Codes", "Online Player (Flying)", "#ffffff"));
            activeDeadCC = Convert.ToString(GetConfig("Color-Hex Codes", "Online Dead Player", "#ff0000"));
            sleeperCC = Convert.ToString(GetConfig("Color-Hex Codes", "Sleeping Player", "#00ffff"));
            sleeperDeadCC = Convert.ToString(GetConfig("Color-Hex Codes", "Sleeping Dead Player", "#ff0000"));
            healthCC = Convert.ToString(GetConfig("Color-Hex Codes", "Health", "#ff0000"));
            backpackCC = Convert.ToString(GetConfig("Color-Hex Codes", "Backpacks", "#c0c0c0"));
            zombieCC = Convert.ToString(GetConfig("Color-Hex Codes", "Zombies", "#ff0000"));
            scientistCC = Convert.ToString(GetConfig("Color-Hex Codes", "Scientists", "#ffff00"));
            peacekeeperCC = Convert.ToString(GetConfig("Color-Hex Codes", "Scientist Peacekeeper", "#ffff00"));
            murdererCC = Convert.ToString(GetConfig("Color-Hex Codes", "Murderers", "#000000"));
            npcCC = Convert.ToString(GetConfig("Color-Hex Codes", "Animals", "#0000ff"));
            resourceCC = Convert.ToString(GetConfig("Color-Hex Codes", "Resources", "#ffff00"));
            colCC = Convert.ToString(GetConfig("Color-Hex Codes", "Collectibles", "#ffff00"));
            tcCC = Convert.ToString(GetConfig("Color-Hex Codes", "Tool Cupboards", "#000000"));
            bagCC = Convert.ToString(GetConfig("Color-Hex Codes", "Sleeping Bags", "#ff00ff"));
            airdropCC = Convert.ToString(GetConfig("Color-Hex Codes", "Airdrops", "#ff00ff"));
            atCC = Convert.ToString(GetConfig("Color-Hex Codes", "AutoTurrets", "#ffff00"));
            corpseCC = Convert.ToString(GetConfig("Color-Hex Codes", "Corpses", "#ffff00"));
            boxCC = Convert.ToString(GetConfig("Color-Hex Codes", "Box", "#ff00ff"));
            lootCC = Convert.ToString(GetConfig("Color-Hex Codes", "Loot", "#ffff00"));
            stashCC = Convert.ToString(GetConfig("Color-Hex Codes", "Stash", "#ffffff"));

            anchorMin = Convert.ToString(GetConfig("GUI", "Anchor Min", "0.667 0.020"));
            anchorMax = Convert.ToString(GetConfig("GUI", "Anchor Max", "0.810 0.148"));
            uiColorOn = Convert.ToString(GetConfig("GUI", "Color On", "0.69 0.49 0.29 0.5"));
            uiColorOff = Convert.ToString(GetConfig("GUI", "Color Off", "0.29 0.49 0.69 0.5"));
            uiBtnBags = Convert.ToBoolean(GetConfig("GUI", "Show Button - Bags", True));
            uiBtnBoats = Convert.ToBoolean(GetConfig("GUI", "Show Button - Boats", False));
            uiBtnBradley = Convert.ToBoolean(GetConfig("GUI", "Show Button - Bradley", False));
            uiBtnBox = Convert.ToBoolean(GetConfig("GUI", "Show Button - Box", True));
            uiBtnCars = Convert.ToBoolean(GetConfig("GUI", "Show Button - Cars", False));
            uiBtnCCTV = Convert.ToBoolean(GetConfig("GUI", "Show Button - CCTV", True));
            uiBtnCargoPlanes = Convert.ToBoolean(GetConfig("GUI", "Show Button - CargoPlanes", False));
            uiBtnCargoShips = Convert.ToBoolean(GetConfig("GUI", "Show Button - CargoShips", False));
            uiBtnCH47 = Convert.ToBoolean(GetConfig("GUI", "Show Button - CH47", False));
            uiBtnCollectible = Convert.ToBoolean(GetConfig("GUI", "Show Button - Collectibles", True));
            uiBtnDead = Convert.ToBoolean(GetConfig("GUI", "Show Button - Dead", True));
            uiBtnHeli = Convert.ToBoolean(GetConfig("GUI", "Show Button - Heli", False));
            uiBtnLoot = Convert.ToBoolean(GetConfig("GUI", "Show Button - Loot", True));
            uiBtnMiniCopter = Convert.ToBoolean(GetConfig("GUI", "Show Button - MiniCopter", False));
            uiBtnMLRS = Convert.ToBoolean(GetConfig("GUI", "Show Button - MLRS", True));
            uiBtnNPC = Convert.ToBoolean(GetConfig("GUI", "Show Button - NPC", True));
            uiBtnOre = Convert.ToBoolean(GetConfig("GUI", "Show Button - Ore", True));
            uiBtnRidableHorses = Convert.ToBoolean(GetConfig("GUI", "Show Button - Ridable Horses", False));
            uiBtnRHIB = Convert.ToBoolean(GetConfig("GUI", "Show Button - RigidHullInflatableBoats", False));
            uiBtnSleepers = Convert.ToBoolean(GetConfig("GUI", "Show Button - Sleepers", True));
            uiBtnStash = Convert.ToBoolean(GetConfig("GUI", "Show Button - Stash", True));
            uiBtnTC = Convert.ToBoolean(GetConfig("GUI", "Show Button - TC", True));
            uiBtnTCArrow = Convert.ToBoolean(GetConfig("GUI", "Show Button - TC Arrow", True));
            uiBtnTurrets = Convert.ToBoolean(GetConfig("GUI", "Show Button - Turrets", True));

            if (!anchorMin.Contains(" ")) anchorMin = anchorMinDefault;
            if (!anchorMax.Contains(" ")) anchorMax = anchorMaxDefault;
            if (uiBtnBoats) trackBoats = True;
            if (uiBtnBradley) trackBradley = True;
            if (uiBtnCars) trackCars = True;
            if (uiBtnCCTV) trackCCTV = True;
            if (uiBtnCargoPlanes) trackCargoPlanes = True;
            if (uiBtnCargoShips) trackCargoShips = True;
            if (uiBtnCH47) trackCH47 = True;
            if (uiBtnHeli) trackHeli = True;
            if (uiBtnMiniCopter) trackMiniCopter = True;
            if (uiBtnMLRS) trackMLRS = True;
            if (uiBtnRidableHorses) trackRidableHorses = True;
            if (uiBtnRHIB) trackRigidHullInflatableBoats = True;

            useGroupColors = Convert.ToBoolean(GetConfig("Group Limit", "Use Group Colors Configuration", True));
            groupColorDead = Convert.ToString(GetConfig("Group Limit", "Dead Color", "#ff0000"));
            groupColorBasic = Convert.ToString(GetConfig("Group Limit", "Group Color Basic", "#ffff00"));

            var list = GetConfig("Group Limit", "Group Colors", DefaultGroupColors) as List<object>;

            if (list != null && list.Count > 0)
            {
                SetupGroupColors(list);
            }

            szRadarCommand = Convert.ToString(GetConfig("Settings", "Chat Command", "radar"));
            szSecondaryCommand = Convert.ToString(GetConfig("Settings", "Second Command", "radar"));

            if (!string.IsNullOrEmpty(szRadarCommand))
            {
                AddCovalenceCommand(szRadarCommand, nameof(RadarCommand));
            }

            if (!string.IsNullOrEmpty(szSecondaryCommand) && szRadarCommand != szSecondaryCommand)
            {
                AddCovalenceCommand(szSecondaryCommand, nameof(RadarCommand));
            }

            //voiceSymbol = Convert.ToString(GetConfig("Voice Detection", "Voice Symbol", "🔊"));
            useVoiceDetection = Convert.ToBoolean(GetConfig("Voice Detection", "Enabled", True));
            voiceInterval = Convert.ToInt32(GetConfig("Voice Detection", "Timeout After X Seconds", 3));
            voiceDistance = Convert.ToSingle(GetConfig("Voice Detection", "Detection Radius", 30f));

            if (voiceInterval < 3)
                voiceInterval = 3;

            _messageColor = Convert.ToInt32(GetConfig("DiscordMessages", "Message - Embed Color (DECIMAL)", 3329330));
            _webhookUrl = Convert.ToString(GetConfig("DiscordMessages", "Message - Webhook URL", "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks"));
            _sendDiscordMessages = _webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
            if (string.IsNullOrEmpty(_webhookUrl)) _sendDiscordMessages = False;

            _embedMessageServer = Convert.ToString(GetConfig("DiscordMessages", "Embed_MessageServer", "Server"));
            _embedMessageLocation = Convert.ToString(GetConfig("DiscordMessages", "Embed_MessageLocation", "Location"));
            _embedMessageTitle = Convert.ToString(GetConfig("DiscordMessages", "Embed_MessageTitle", "Player Message"));
            _embedMessagePlayer = Convert.ToString(GetConfig("DiscordMessages", "Embed_MessagePlayer", "Player"));
            _embedMessageMessage = Convert.ToString(GetConfig("DiscordMessages", "Embed_MessageMessage", "Message"));
            _discordMessageToggleOff = Convert.ToString(GetConfig("DiscordMessages", "Off", "Radar turned off."));
            _discordMessageToggleOn = Convert.ToString(GetConfig("DiscordMessages", "On", "Radar turned on."));

            ShowRadar = Convert.ToBoolean(GetConfig("Track Admin Status", "Radar", False));
            ShowRadarText = Convert.ToString(GetConfig("Track Admin Status", "Radar Text", "<color=#00FF00>R</color>"));
            ShowGod = Convert.ToBoolean(GetConfig("Track Admin Status", "Console Godmode", False));
            ShowGodText = Convert.ToString(GetConfig("Track Admin Status", "Console Godmode Text", "<color=#89CFF0>G</color>"));
            ShowGodPlugin = Convert.ToBoolean(GetConfig("Track Admin Status", "Plugin Godmode", False));
            ShowGodPluginText = Convert.ToString(GetConfig("Track Admin Status", "Plugin Godmode Text", "<color=#0000CD>G</color>"));
            ShowVanish = Convert.ToBoolean(GetConfig("Track Admin Status", "Vanish", True));
            ShowVanishText = Convert.ToString(GetConfig("Track Admin Status", "Vanish Text", "<color=#FF00FF>V</color>"));
            ShowNoClip = Convert.ToBoolean(GetConfig("Track Admin Status", "NOCLIP", False));
            ShowNoClipText = Convert.ToString(GetConfig("Track Admin Status", "NOCLIP Text", "<color=#FFFF00>F</color>"));

            if (Changed)
            {
                SaveConfig();
                Changed = False;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = True;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = True;
            }

            return value;
        }

        private string msg(string key, string id = null, params object[] args)
        {
            var message = id == null || id == "server_console" ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private string RemoveFormatting(BasePlayer target)
        {
            return RemoveFormatting(target.displayName);
        }

        private string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        private void Message(BasePlayer target, string message)
        {
            //if (target.IsValid() && target.IsConnected && Network.Net.sv.IsConnected())
            //{
            //    Network.NetWrite netWrite = Network.Net.sv.StartWrite();
            //    netWrite.PacketID(Network.Message.Type.ConsoleCommand);
            //    netWrite.String(ConsoleSystem.BuildCommand("chat.add", 2, 0, message));
            //    netWrite.Send(new Network.SendInfo(target.Connection));
            //}
            Player.Message(target, message);
        }

        #endregion

    }
}

namespace Oxide.Plugins.AdminRadarExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool All<T>(this IEnumerable<T> a, Func<T, bool> b) { foreach (T c in a) { if (!b(c)) { return false; } } return true; }
        public static List<T> ToList<T>(this IEnumerable<T> a, Func<T, bool> b = null) { var c = new List<T>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b == null || b(d.Current)) { c.Add(d.Current); } } } return c; }
        public static List<string> ToLower(this IEnumerable<string> a, Func<string, bool> b = null) { var c = new List<string>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b == null || b(d.Current)) { c.Add(d.Current.ToLower()); } } } return c; }
        public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b) { var c = new List<V>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { c.Add(b(d.Current)); } } return c; }
        public static IEnumerable<T> OfType<T>(this IEnumerable<object> a) { foreach (object b in a) { if (b is T) { yield return (T)b; } } }
        public static bool IsKilled(this BaseNetworkable a) { return (object)a == null || a.IsDestroyed; }
    }
}