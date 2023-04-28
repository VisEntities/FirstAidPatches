using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Inbound", "Substrata", "0.6.2")]
    [Description("Broadcasts notifications when patrol helicopters, supply drops, cargo ships, etc. are inbound")]

    class Inbound : RustPlugin
    {
        [PluginReference]
        Plugin DiscordMessages, PopupNotifications, UINotify,
        // Compatibility
        AirdropPrecision, FancyDrop;

        float worldSize; int xGridNum; int zGridNum; float gridBottom; float gridTop; float gridLeft; float gridRight;
        bool hasOilRig; bool hasLargeRig; Vector3 oilRigPos; Vector3 largeRigPos; bool hasExcavator; Vector3 excavatorPos;
        ulong chatIconID; string webhookURL;

        void OnServerInitialized(bool initial) => InitVariables();

        void OnEntitySpawned(BaseHelicopter heli)
        {
            NextTick(() =>
            {
                if (heli == null || heli.IsDestroyed) return;
                SendInboundMessage(Lang("PatrolHeli", null, Location(heli.transform.position, null), Destination(heli.myAI.destination)), configData.alerts.patrolHeli);
            });
        }

        void OnEntitySpawned(CargoShip ship)
        {
            timer.Once(2f, () =>
            {
                if (ship == null || ship.IsDestroyed) return;
                SendInboundMessage(Lang("CargoShip_", null, Location(ship.transform.position, null), Destination(TerrainMeta.Path.OceanPatrolFar[ship.GetClosestNodeToUs()])), configData.alerts.cargoShip);
            });
        }

        void OnEntitySpawned(CH47HelicopterAIController ch47)
        {
            timer.Once(2f, () =>
            {
                if (ch47 == null || ch47.IsDestroyed) return;
                SendInboundMessage(Lang("CH47", null, Location(ch47.transform.position, null), Destination(ch47.GetMoveTarget())), configData.alerts.ch47 && (!configData.misc.hideRigCrates || !ch47.ShouldLand()));
            });
        }

        void OnBradleyApcInitialize(BradleyAPC apc)
        {
            NextTick(() =>
            {
                if (apc == null || apc.IsDestroyed) return;
                SendInboundMessage(Lang("BradleyAPC", null, Location(apc.transform.position, null)), configData.alerts.bradleyAPC);
            });
        }

        void OnExcavatorResourceSet(ExcavatorArm arm, string resourceName, BasePlayer player)
        {
            if (arm == null || arm.IsOn()) return;
            NextTick(() =>
            {
                if (player == null || arm == null || arm.IsDestroyed || !arm.IsOn()) return;
                SendInboundMessage(Lang("Excavator_", null, player.displayName, Location(arm.transform.position, null, true)), configData.alerts.excavator);
            });
        }

        void OnEntitySpawned(HackableLockedCrate crate)
        {
            NextTick(() =>
            {
                if (crate == null || crate.IsDestroyed) return;
                SendInboundMessage(Lang("HackableCrateSpawned", null, Location(crate.transform.position, crate)), configData.alerts.hackableCrateSpawn && !HideCrateAlert(crate));
            });
        }

        void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            NextTick(() =>
            {
                if (player == null || crate == null || crate.IsDestroyed || !crate.IsBeingHacked()) return;
                SendInboundMessage(Lang("HackingCrate", null, player.displayName, Location(crate.transform.position, crate)), configData.alerts.hackingCrate && !HideCrateAlert(crate));
            });
        }

        // Supply Drops
        private HashSet<CalledDrop> calledDrops = new HashSet<CalledDrop>();
        private class CalledDrop
        {
            public IPlayer _iplayer = null;
            public SupplySignal _signal = null;
            public CargoPlane _plane = null;
            public SupplyDrop _drop = null;
        }

        void OnExplosiveThrown(BasePlayer player, SupplySignal signal)
        {
            NextTick(() =>
            {
                if (player == null || signal == null) return;
                if (SupplyPlayerCompatible())
                    calledDrops.Add(new CalledDrop() { _iplayer = player.IPlayer, _signal = signal });
                SendInboundMessage(Lang("SupplySignal", null, player.displayName, Location(player.transform.position, player)), configData.alerts.supplySignal);
            });
        }

        void OnExplosiveDropped(BasePlayer player, SupplySignal signal) => OnExplosiveThrown(player, signal);

        void OnCargoPlaneSignaled(CargoPlane plane, SupplySignal signal)
        {
            if (plane == null || signal == null) return;
            foreach (var calledDrop in calledDrops)
            {
                if (calledDrop._signal == signal)
                {
                    calledDrop._plane = plane;
                    break;
                }
            }
        }

        void OnExcavatorSuppliesRequested(ExcavatorSignalComputer computer, BasePlayer player, CargoPlane plane)
        {
            NextTick(() =>
            {
                if (player == null || plane == null) return;
                if (SupplyPlayerCompatible())
                    calledDrops.Add(new CalledDrop() { _iplayer = player.IPlayer, _plane = plane });
                SendInboundMessage(Lang("ExcavatorSupplyRequest", null, player.displayName, Location(player.transform.position, null)), configData.alerts.excavatorSupply);
            });
        }

        void OnAirdrop(CargoPlane plane, Vector3 dest)
        {
            timer.Once(2f, () =>
            {
                if (plane == null) return;
                CalledDrop calledDrop = GetCalledDrop(plane, null);
                SendInboundMessage(Lang("CargoPlane_", null, SupplyDropPlayer(calledDrop), Location(plane.transform.position, null), Destination(dest)), configData.alerts.cargoPlane && !HideSupplyAlert(calledDrop));
            });
        }

        private HashSet<uint> droppedDrops = new HashSet<uint>();
        void OnSupplyDropDropped(SupplyDrop drop, CargoPlane plane)
        {
            NextTick(() =>
            {
                if (drop == null || droppedDrops.Contains(drop.net.ID)) return;
                droppedDrops.Add(drop.net.ID);
                CalledDrop calledDrop = GetCalledDrop(plane, null);
                if (calledDrop != null) calledDrop._drop = drop;
                SendInboundMessage(Lang("SupplyDropDropped", null, SupplyDropPlayer(calledDrop), Location(drop.transform.position, null)), configData.alerts.supplyDrop && !HideSupplyAlert(calledDrop));
            });
        }

        void OnEntitySpawned(SupplyDrop drop) => NextTick(() => OnSupplyDropDropped(drop, null));

        private HashSet<uint> landedDrops = new HashSet<uint>();
        void OnSupplyDropLanded(SupplyDrop drop)
        {
            if (drop == null || landedDrops.Contains(drop.net.ID)) return;
            landedDrops.Add(drop.net.ID);
            CalledDrop calledDrop = GetCalledDrop(null, drop);
            SendInboundMessage(Lang("SupplyDropLanded_", null, SupplyDropPlayer(calledDrop), Location(drop.transform.position, null)), configData.alerts.supplyDropLand && !HideSupplyAlert(calledDrop));
        }

        void OnEntityKill(SupplyDrop drop)
        {
            if (drop == null) return;
            CalledDrop calledDrop = GetCalledDrop(null, drop);
            if (calledDrop != null) calledDrops.Remove(calledDrop);
            droppedDrops.Remove(drop.net.ID);
            landedDrops.Remove(drop.net.ID);
        }

        #region Messages
        void SendInboundMessage(string message, bool alert)
        {
            if (string.IsNullOrEmpty(message)) return;

            string msg = Regex.Replace(message, filterTags, string.Empty);

            if (alert)
            {
                if (configData.notifications.chat)
                    Server.Broadcast(message, null, chatIconID);

                if (configData.notifications.popup && PopupNotifications)
                    PopupNotifications.Call("CreatePopupNotification", msg);

                if (configData.uiNotify.enabled && UINotify)
                    SendUINotify(msg);

                if (configData.discordMessages.enabled && DiscordMessages && webhookURL.Contains("/api/webhooks/"))
                    SendDiscordMessage(msg);
            }
            if (alert || configData.logging.allEvents)
                LogInboundMessage(msg);
        }

        void SendUINotify(string msg)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "uinotify.see"))
                    UINotify.Call("SendNotify", player, configData.uiNotify.type, msg);
            }
        }

        void SendDiscordMessage(string msg)
        {
            string dMsg = Lang("DiscordMessage_", null, msg);
            if (configData.discordMessages.embedded)
            {
                object fields = new[]
                {
                    new {
                        name = configData.discordMessages.embedTitle, value = dMsg, inline = false
                    }
                };
                string json = JsonConvert.SerializeObject(fields);
                DiscordMessages.Call("API_SendFancyMessage", webhookURL, string.Empty, configData.discordMessages.embedColor, json);
            }
            else
                DiscordMessages.Call("API_SendTextMessage", webhookURL, dMsg);
        }

        void LogInboundMessage(string msg)
        {
            if (configData.logging.console)
                Puts(msg);
            
            if (configData.logging.file)
                LogToFile("log", $"[{DateTime.Now.ToString("HH:mm:ss")}] {msg}", this);
        }
        #endregion

        #region Helpers
        private string Location(Vector3 pos, BaseEntity entity, bool hideExc = false)
        {
            string location = GetLocation(pos, entity, hideExc);
            return !string.IsNullOrEmpty(location) ? Lang("Location", null, location) : string.Empty;
        }

        private string Destination(Vector3 pos)
        {
            string destination = GetLocation(pos, null);
            return !string.IsNullOrEmpty(destination) ? Lang("Destination", null, destination) : string.Empty;
        }

        private string GetLocation(Vector3 pos, BaseEntity entity, bool hideExc = false)
        {
            var sb = new StringBuilder();

            if (configData.location.showCargoShip)
                if (IsAtCargoShip(entity)) sb.Append("Cargo Ship");

            if (configData.location.showOilRigs && sb.Length == 0)
            {
                if (IsAtOilRig(pos)) sb.Append("Oil Rig");
                else if (IsAtLargeRig(pos)) sb.Append("Large Oil Rig");
            }

            if (configData.location.showExcavator && !hideExc && sb.Length == 0)
                if (IsAtExcavator(pos)) sb.Append("The Excavator");

            if (configData.location.showGrid && sb.Length == 0)
            {
                if (pos.x > gridLeft && (!configData.location.hideOffGrid || !IsOffGrid(pos)))
                    sb.Append(GetGrid(pos));
            }

            if (configData.location.showCoords)
            {
                bool hideDecimals = configData.location.hideCoordDecimals;
                string x = hideDecimals ? Mathf.Round(pos.x).ToString()+"," : pos.x.ToString("0.##")+",";
                string y = hideDecimals ? Mathf.Round(pos.y).ToString()+"," : pos.y.ToString("0.##")+",";
                string z = hideDecimals ? Mathf.Round(pos.z).ToString() : pos.z.ToString("0.##");
                if (configData.location.hideYCoord) y = string.Empty;

                sb.Append(sb.Length == 0 ? x+y+z : $" ({x}{y}{z})");
            }

            return sb.ToString();
        }

        string GetGrid(Vector3 pos) // Credit: yetzt & JakeRich
        {
            float x = Mathf.Floor((pos.x+(worldSize/2)) / 146.3f);
            float zGrids = configData.location.gridOffset ? Mathf.Floor(worldSize/146.3f) : Mathf.Floor(worldSize/146.3f)-1;
            float z = zGrids-Mathf.Floor((pos.z+(worldSize/2)) / 146.3f);

            int num = (int)x;
            int num2 = Mathf.FloorToInt((float)(num / 26));
            int num3 = num % 26;
            string text = string.Empty;
            if (num2 > 0)
            {
                for (int i = 0; i < num2; i++)
                    text += Convert.ToChar(65 + i);
            }
            return (text + Convert.ToChar(65 + num3))+z;
        }

        private CalledDrop GetCalledDrop(CargoPlane plane, SupplyDrop drop)
        {
            foreach (var calledDrop in calledDrops)
            {
                if ((plane != null && calledDrop._plane == plane) || (drop != null && calledDrop._drop == drop))
                    return calledDrop;
            }
            return null;
        }

        private string SupplyDropPlayer(CalledDrop calledDrop)
        {
            IPlayer iplayer = calledDrop != null ? calledDrop._iplayer : null;
            return configData.misc.showSupplyPlayer && iplayer != null ? Lang("SupplyDropPlayer", null, iplayer.Name) : string.Empty;
        }

        private bool HideSupplyAlert(CalledDrop calledDrop)
        {
            bool playerCalled = calledDrop != null;
            return SupplyPlayerCompatible() && ((configData.misc.hideCalledSupply && playerCalled) || (configData.misc.hideRandomSupply && !playerCalled));
        }

        private bool HideCrateAlert(HackableLockedCrate crate)
        {
            Vector3 pos = crate.transform.position;
            return (configData.misc.hideCargoCrates && IsAtCargoShip(crate)) || (configData.misc.hideRigCrates && (IsAtOilRig(pos) || IsAtLargeRig(pos)));
        }

        private string filterTags = @"(?i)<\/?(align|alpha|color|cspace|indent|line-height|line-indent|margin|mark|mspace|pos|size|space|voffset).*?>|<\/?(b|i|lowercase|uppercase|smallcaps|s|u|sup|sub)>";
        private bool IsAtOilRig(Vector3 pos) => hasOilRig && Vector3Ex.Distance2D(oilRigPos, pos) <= 60f;
        private bool IsAtLargeRig(Vector3 pos) => hasLargeRig && Vector3Ex.Distance2D(largeRigPos, pos) <= 75f;
        private bool IsAtCargoShip(BaseEntity entity) => entity?.GetComponentInParent<CargoShip>();
        private bool IsAtExcavator(Vector3 pos) => hasExcavator && Vector3Ex.Distance2D(excavatorPos, pos) <= 145f;
        private bool IsOffGrid(Vector3 pos) => pos.x < gridLeft || pos.x > gridRight || pos.z < gridBottom || pos.z > gridTop;
        private bool SupplyPlayerCompatible() => !FancyDrop && !AirdropPrecision;

        void InitVariables()
        {
            // Grid
            worldSize = TerrainMeta.Size.x;
            xGridNum = (int)Mathf.Ceil(worldSize / 146.3f);
            zGridNum = (int)Mathf.Floor(worldSize / 146.3f);
            if (configData.location.gridOffset) zGridNum += 1;
            gridBottom = TerrainMeta.Position.z;
            gridTop = gridBottom + (146.3f * zGridNum);
            gridLeft = TerrainMeta.Position.x;
            gridRight = gridLeft + (146.3f * xGridNum);

            // Monuments
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                var name = monument.name;
                if (name == "assets/bundled/prefabs/autospawn/monument/large/excavator_1.prefab")
                {
                    hasExcavator = true;
                    excavatorPos = monument.transform.localToWorldMatrix.MultiplyPoint3x4(new Vector3(20f,0,-30f));
                    continue;
                }
                if (name == "OilrigAI")
                {
                    hasOilRig = true;
                    oilRigPos = monument.transform.position;
                    continue;
                }
                if (name == "OilrigAI2")
                {
                    hasLargeRig = true;
                    largeRigPos = monument.transform.position;
                    continue;
                }
            }

            // General & plugins
            if (configData.notifications.chat && configData.misc.chatIcon != 0)
            {
                if (!configData.misc.chatIcon.IsSteamId())
                    PrintWarning("Chat Icon is not set to a valid SteamID64.");
                else
                    chatIconID = configData.misc.chatIcon;
            }

            if (configData.notifications.popup && !PopupNotifications)
                PrintWarning("The 'Popup Notifications' plugin could not be found.");

            if (configData.discordMessages.enabled)
            {
                webhookURL = configData.discordMessages.webhookURL;
                if (!DiscordMessages)
                    PrintWarning("The 'Discord Messages' plugin could not be found.");
                else if (!webhookURL.Contains("/api/webhooks/"))
                    PrintWarning("The 'Discord Messages' Webhook URL is missing or invalid.");
            }

            if (configData.uiNotify.enabled && !UINotify)
                PrintWarning("The 'UI Notify' plugin could not be found.");

            if (!SupplyPlayerCompatible() && (configData.misc.showSupplyPlayer || configData.misc.hideCalledSupply || configData.misc.hideRandomSupply))
                PrintWarning("The 'Supply Drop Player' options are not currently compatible with Fancy Drop or Aidrop Precision. Using defaults (false).");
        }
        #endregion

        #region Configuration
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Notifications")]
            public Notifications notifications { get; set; }
            [JsonProperty(PropertyName = "Discord Messages")]
            public DiscordMessages discordMessages { get; set; }
            [JsonProperty(PropertyName = "UI Notify")]
            public UINotify uiNotify { get; set; }
            [JsonProperty(PropertyName = "Alerts")]
            public Alerts alerts { get; set; }
            [JsonProperty(PropertyName = "Location")]
            public Location location { get; set; }
            [JsonProperty(PropertyName = "Misc")]
            public Misc misc { get; set; }
            [JsonProperty(PropertyName = "Logging")]
            public Logging logging { get; set; }

            public class Notifications
            {
                [JsonProperty(PropertyName = "Chat Notifications")]
                public bool chat { get; set; }
                [JsonProperty(PropertyName = "Popup Notifications")]
                public bool popup { get; set; }
            }

            public class DiscordMessages
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool enabled { get; set; }
                [JsonProperty(PropertyName = "Webhook URL")]
                public string webhookURL { get; set; }
                [JsonProperty(PropertyName = "Embedded Messages")]
                public bool embedded { get; set; }
                [JsonProperty(PropertyName = "Embed Color")]
                public int embedColor { get; set; }
                [JsonProperty(PropertyName = "Embed Title")]
                public string embedTitle { get; set; }
            }

            public class UINotify
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool enabled { get; set; }
                [JsonProperty(PropertyName = "Notification Type")]
                public int type { get; set; }
            }

            public class Alerts
            {
                [JsonProperty(PropertyName = "Patrol Helicopter Alerts")]
                public bool patrolHeli { get; set; }
                [JsonProperty(PropertyName = "Cargo Ship Alerts")]
                public bool cargoShip { get; set; }
                [JsonProperty(PropertyName = "Cargo Plane Alerts")]
                public bool cargoPlane { get; set; }
                [JsonProperty(PropertyName = "CH47 Chinook Alerts")]
                public bool ch47 { get; set; }
                [JsonProperty(PropertyName = "Bradley APC Alerts")]
                public bool bradleyAPC { get; set; }
                [JsonProperty(PropertyName = "Excavator Activated Alerts")]
                public bool excavator { get; set; }
                [JsonProperty(PropertyName = "Excavator Supply Request Alerts")]
                public bool excavatorSupply { get; set; }
                [JsonProperty(PropertyName = "Hackable Crate Alerts")]
                public bool hackableCrateSpawn { get; set; }
                [JsonProperty(PropertyName = "Player Hacking Crate Alerts")]
                public bool hackingCrate { get; set; }
                [JsonProperty(PropertyName = "Supply Signal Alerts")]
                public bool supplySignal { get; set; }
                [JsonProperty(PropertyName = "Supply Drop Alerts")]
                public bool supplyDrop { get; set; }
                [JsonProperty(PropertyName = "Supply Drop Landed Alerts")]
                public bool supplyDropLand { get; set; }
            }

            public class Location
            {
                [JsonProperty(PropertyName = "Show Grid")]
                public bool showGrid { get; set; }
                [JsonProperty(PropertyName = "Show 'Oil Rig' Labels")]
                public bool showOilRigs { get; set; }
                [JsonProperty(PropertyName = "Show 'Cargo Ship' Label")]
                public bool showCargoShip { get; set; }
                [JsonProperty(PropertyName = "Show 'Excavator' Label")]
                public bool showExcavator { get; set; }
                [JsonProperty(PropertyName = "Hide Unmarked Grids")]
                public bool hideOffGrid { get; set; }
                [JsonProperty(PropertyName = "Grid Offset")]
                public bool gridOffset { get; set; }
                [JsonProperty(PropertyName = "Show Coordinates")]
                public bool showCoords { get; set; }
                [JsonProperty(PropertyName = "Hide Y Coordinate")]
                public bool hideYCoord { get; set; }
                [JsonProperty(PropertyName = "Hide Coordinate Decimals")]
                public bool hideCoordDecimals { get; set; }
            }

            public class Misc
            {
                [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
                public ulong chatIcon { get; set; }
                [JsonProperty(PropertyName = "Hide Cargo Ship Crate Messages")]
                public bool hideCargoCrates { get; set; }
                [JsonProperty(PropertyName = "Hide Oil Rig Crate & Chinook Messages")]
                public bool hideRigCrates { get; set; }
                [JsonProperty(PropertyName = "Show Supply Drop Player")]
                public bool showSupplyPlayer { get; set; }
                [JsonProperty(PropertyName = "Hide Player-Called Supply Drop Messages")]
                public bool hideCalledSupply { get; set; }
                [JsonProperty(PropertyName = "Hide Random Supply Drop Messages")]
                public bool hideRandomSupply { get; set; }
            }

            public class Logging
            {
                [JsonProperty(PropertyName = "Log To Console")]
                public bool console { get; set; }
                [JsonProperty(PropertyName = "Log To File")]
                public bool file { get; set; }
                [JsonProperty(PropertyName = "Log All Events")]
                public bool allEvents { get; set; }
            }

            [JsonProperty(PropertyName = "Version (Do not modify)")]
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null) throw new Exception();

                if (configData.Version < Version)
                    UpdateConfigValues();

                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                notifications = new ConfigData.Notifications
                {
                    chat = true,
                    popup = false
                },
                discordMessages = new ConfigData.DiscordMessages
                {
                    enabled = false,
                    webhookURL = string.Empty,
                    embedded = true,
                    embedColor = 3447003,
                    embedTitle = ":arrow_lower_right:  Inbound"
                },
                uiNotify = new ConfigData.UINotify
                {
                    enabled = false,
                    type = 0
                },
                alerts = new ConfigData.Alerts
                {
                    patrolHeli = true,
                    cargoShip = true,
                    cargoPlane = true,
                    ch47 = true,
                    bradleyAPC = true,
                    excavator = true,
                    excavatorSupply = true,
                    hackableCrateSpawn = true,
                    hackingCrate = true,
                    supplySignal = true,
                    supplyDrop = true,
                    supplyDropLand = true
                },
                location = new ConfigData.Location
                {
                    showGrid = true,
                    showOilRigs = true,
                    showCargoShip = true,
                    showExcavator = true,
                    hideOffGrid = true,
                    gridOffset = false,
                    showCoords = false,
                    hideYCoord = false,
                    hideCoordDecimals = false
                },
                misc = new ConfigData.Misc
                {
                    chatIcon = 0,
                    hideCargoCrates = false,
                    hideRigCrates = false,
                    showSupplyPlayer = false,
                    hideCalledSupply = false,
                    hideRandomSupply = false
                },
                logging = new ConfigData.Logging
                {
                    console = false,
                    file = false,
                    allEvents = false
                },
                Version = Version
            };
        }

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");
            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new Core.VersionNumber(0, 6, 0))
            {
                configData = baseConfig;
                configData.notifications.chat = Convert.ToBoolean(GetConfig("Notifications (true/false)", "Chat Notifications", true));
                configData.notifications.popup = Convert.ToBoolean(GetConfig("Notifications (true/false)", "Popup Notifications", false));
                configData.discordMessages.enabled = Convert.ToBoolean(GetConfig("Discord Messages", "Enabled (true/false)", false));
                configData.discordMessages.webhookURL = Convert.ToString(GetConfig("Discord Messages", "Webhook URL", string.Empty));
                configData.alerts.bradleyAPC = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Bradley APC Alerts", true));
                configData.alerts.cargoPlane = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Cargo Plane Alerts", true));
                configData.alerts.cargoShip = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Cargo Ship Alerts", true));
                configData.alerts.ch47 = Convert.ToBoolean(GetConfig("Alerts (true/false)", "CH47 Chinook Alerts", true));
                configData.alerts.excavator = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Excavator Alerts", true));
                configData.alerts.hackableCrateSpawn = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Hackable Crate Alerts", true));
                configData.alerts.patrolHeli = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Patrol Helicopter Alerts", true));
                configData.alerts.hackingCrate = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Player Hacking Crate Alerts", true));
                configData.alerts.supplySignal = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Player Supply Signal Alerts", true));
                configData.alerts.supplyDrop = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Supply Drop Alerts", true));
                configData.alerts.supplyDropLand = Convert.ToBoolean(GetConfig("Alerts (true/false)", "Supply Drop Landed Alerts", true));
                configData.location.showGrid = Convert.ToBoolean(GetConfig("Grid (true/false)", "Show Grid", true));
                configData.location.showOilRigs = Convert.ToBoolean(GetConfig("Grid (true/false)", "Show Oil Rig / Cargo Ship Labels", true));
                configData.location.showCargoShip = Convert.ToBoolean(GetConfig("Grid (true/false)", "Show Oil Rig / Cargo Ship Labels", true));
                configData.location.showCoords = Convert.ToBoolean(GetConfig("Coordinates (true/false)", "Show Coordinates", false));
                configData.misc.chatIcon = Convert.ToUInt64(GetConfig("Chat Icon", "Steam ID", 0));
                configData.misc.hideCargoCrates = Convert.ToBoolean(GetConfig("Misc (true/false)", "Hide Cargo Ship Crate Messages", false));
                configData.misc.hideRigCrates = Convert.ToBoolean(GetConfig("Misc (true/false)", "Hide Oil Rig Crate Messages", false));
                configData.logging.console = Convert.ToBoolean(GetConfig("Misc (true/false)", "Log To Console", false));
                configData.logging.file = Convert.ToBoolean(GetConfig("Misc (true/false)", "Log To File", false));

                BackupLang();
            }
            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        object GetConfig(string menu, string dataValue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(dataValue, out value))
            {
                value = defaultValue;
                data[dataValue] = value;
            }
            return value;
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();
        protected override void SaveConfig() => Config.WriteObject(configData, true);
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PatrolHeli"] = "Patrol Helicopter inbound{0}{1}",
                ["CargoShip_"] = "Cargo Ship inbound{0}{1}",
                ["CargoPlane_"] = "{0}Cargo Plane inbound{1}{2}",
                ["CH47"] = "Chinook inbound{0}{1}",
                ["BradleyAPC"] = "Bradley APC inbound{0}",
                ["Excavator_"] = "{0} has activated The Excavator{1}",
                ["ExcavatorSupplyRequest"] = "{0} has requested a supply drop{1}",
                ["HackableCrateSpawned"] = "Hackable Crate has spawned{0}",
                ["HackingCrate"] = "{0} is hacking a locked crate{1}",
                ["SupplySignal"] = "{0} has deployed a supply signal{1}",
                ["SupplyDropDropped"] = "{0}Supply Drop has dropped{1}",
                ["SupplyDropLanded_"] = "{0}Supply Drop has landed{1}",
                ["SupplyDropPlayer"] = "{0}'s ",
                ["Location"] = " at {0}",
                ["Destination"] = " and headed to {0}",
                ["DiscordMessage_"] = "{0}"
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void BackupLang()
        {
            PrintWarning("Backing up language file to /oxide/logs/Inbound");
            Dictionary<string, string> langFile = lang.GetMessages(lang.GetServerLanguage(), this);
            string langJson = JsonConvert.SerializeObject(langFile, Formatting.Indented);
            LogToFile($"lang_backup_{DateTime.Now.ToString("HH-mm-ss")}", langJson, this);
        }
        #endregion
    }
}