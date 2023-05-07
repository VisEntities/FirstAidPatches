using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Network;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("Police Sedan", "NotBad", "1.0.10")]
    [Description("Spawn a police sedan with police lights")]
    public class PoliceSedan : RustPlugin
    {
        #region Variables
        private StoredData storedData;

        public HashSet<ulong> LightsON = new HashSet<ulong>();

        public string bluelight = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab";
        public string redlight = "assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab";
        public string orangelight = "assets/prefabs/io/electric/lights/sirenlightorange.prefab";
        public string strobelight = "assets/content/props/strobe light/strobelight.prefab";
        public string hornSound = "assets/prefabs/tools/pager/effects/beep.prefab";
        public string buttonPrefab = "assets/prefabs/deployable/playerioents/button/button.prefab";

        #endregion

        #region Initialization

        private void Init()
        {
            permission.RegisterPermission("policesedan.spawn", this);
            //permission.RegisterPermission("policesedan.nocooldown", this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }


        #endregion

        #region Hooks

        void OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button == null || player == null)
            {
                return;
            }

            var result = player.GetMounted();

            if (result == null)
            {
                return;
            }

            if (result.ShortPrefabName == "driverseat")
            {
                var mountedveh = player.GetMountedVehicle();

                if (LightsON.Contains(mountedveh.net.ID.Value))
                {
                    SirenTurnONorOFF(player, mountedveh, false);
                    LightsON.Remove(mountedveh.net.ID.Value);
                    return;
                }
                else
                {
                    SirenTurnONorOFF(player, mountedveh, true);
                    LightsON.Add(mountedveh.net.ID.Value);
                    return;
                }
            }
        }

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (entity == null || player == null)
            {
                return;
            }

            var mounted = player.GetMounted();

            if (mounted == null)
            {
                return;
            }

            if (mounted.ShortPrefabName != "driverseat")
            {
                return;
            }

            if (storedData.sedan.ContainsKey(player.UserIDString))
            {
                return;
            }

            //if (!permission.UserHasPermission(player.UserIDString, "policesedan.nocooldown"))
            //{
            //    storedData.cooldown.Add(player.UserIDString, DateTime.Now);
            //}

            if (permission.UserHasPermission(player.UserIDString, "policesedan.spawn"))
            {
                CreateSiren(player);
            }
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null)
            {
                return;
            }

            var policesedan = entity.GetComponent<BaseVehicle>();

            if (policesedan == null)
            {
                return;
            }

            if (storedData.sedan.ContainsValue(policesedan.net.ID.Value))
            {
                string key = storedData.sedan.FirstOrDefault(x => x.Value == policesedan.net.ID.Value).Key;

                ulong result;
                ulong.TryParse(key, out result);
                BasePlayer player = BasePlayer.FindByID(result);

                if (player != null)
                {
                    player.ChatMessage(lang.GetMessage("SedanDestroyed", this, player.UserIDString));
                }

                storedData.sedan.Remove(key);

                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }

        #endregion

        #region Helpers

        public void CreateSiren(BasePlayer player)
        {
            var mountedveh = player.GetMountedVehicle();

            MakeSiren(mountedveh, buttonPrefab, config.ButtonPosition);
            MakeSiren(mountedveh, bluelight, config.BlueLightPosition);
            MakeSiren(mountedveh, bluelight, config.BlueLightPosition2);
            if (config.CreateRedLight == true)
            {
                MakeSiren(mountedveh, redlight, config.RedLightPosition);
            }
            MakeSiren(mountedveh, strobelight, config.StrobeLightPosition);
            MakeSiren(mountedveh, strobelight, config.StrobeLightPosition2);
            if (config.CreateOrangeLight == true)
            {
                MakeSiren(mountedveh, orangelight, config.OrangeLightPosition);
            }

            storedData.sedan.Add(player.UserIDString, mountedveh.net.ID.Value);

            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        void MakeSiren(BaseVehicle vehicle, string entityToSpawn, Vector3 position)
        {
            BaseEntity entity = GameManager.server.CreateEntity(entityToSpawn, vehicle.transform.position);
            if (entity == null) return;
            entity.transform.localPosition = position;
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
            entity.SetParent(vehicle);
            entity.transform.localPosition = position;
            entity.Spawn();
            vehicle.AddChild(entity);
        }

        void SirenTurnONorOFF(BasePlayer player, BaseVehicle vehicle, bool turnON)
        {
            Effect.server.Run(hornSound, player.transform.position);

            foreach (var childern in vehicle.children)
            {
                if (childern.name == strobelight)
                {
                    childern.SetFlag(BaseEntity.Flags.On, turnON);
                }
                if (childern.name == bluelight || childern.name == redlight || childern.name == orangelight)
                {
                    childern.SetFlag(BaseEntity.Flags.Reserved8, turnON);
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand("policesedan")]
        private void CommandSpawnSedan(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "policesedan.spawn"))
            {
                player.ChatMessage(lang.GetMessage("NotAllowed", this, player.UserIDString));
                return;
            }

            if (storedData.sedan.ContainsKey(player.UserIDString))
            {
                player.ChatMessage(lang.GetMessage("AlreadyHaveCar", this, player.UserIDString));
                return;
            }

            BaseVehicle car = (BaseVehicle)GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", player.transform.position);
            if (car == null)
            {
                return;
            }

            car.Spawn();

            foreach (var mount in car.mountPoints)
            {
                if (mount.isDriver)
                {
                    mount.mountable.AttemptMount(player);
                }
            }
        }

        [ChatCommand("destroyps")]
        private void CommandDestroySedan(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "policesedan.spawn"))
            {
                player.ChatMessage(lang.GetMessage("NotAllowed", this, player.UserIDString));
                return;
            }

            if (storedData.sedan.ContainsKey(player.UserIDString))
            {
                ulong value = storedData.sedan.FirstOrDefault(x => x.Key == player.UserIDString).Value;                
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(value));
                if (entity == null)
                {
                    storedData.sedan.Remove(value.ToString());
                }
                else
                {
                    entity.Kill();
                }
            }
        }

        #endregion

        #region Helpers
        private TimeSpan CeilingTimeSpan(TimeSpan timeSpan) =>
            new TimeSpan((long)Math.Ceiling(1.0 * timeSpan.Ticks / 10000000) * 10000000);
        #endregion

        #region Data
        public class StoredData
        {
            public Dictionary<string, ulong> sedan = new Dictionary<string, ulong>();
            //public Dictionary<string, DateTime> cooldown = new Dictionary<string, DateTime>();
        }

        #endregion

        #region Configuration

        private ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Create Red Light")]
            public bool CreateRedLight = true;

            [JsonProperty(PropertyName = "Create Orange Light")]
            public bool CreateOrangeLight = true;

            [JsonProperty(PropertyName = "Blue Light Position")]
            public Vector3 BlueLightPosition = new Vector3(-0.4f, 1.65f, 0.5f);

            [JsonProperty(PropertyName = "Blue Light Position 2")]
            public Vector3 BlueLightPosition2 = new Vector3(0.4f, 1.65f, 0.5f);

            [JsonProperty(PropertyName = "Red Light Position")]
            public Vector3 RedLightPosition = new Vector3(0f, 0.25f, 0.28f);

            [JsonProperty(PropertyName = "Strobe Light Position")]
            public Vector3 StrobeLightPosition = new Vector3(0.9f, 0.72f, 3.305f);

            [JsonProperty(PropertyName = "Strobe Light Position 2")]
            public Vector3 StrobeLightPosition2 = new Vector3(-0.9f, 0.72f, 3.305f);

            [JsonProperty(PropertyName = "Orange Light Position")]
            public Vector3 OrangeLightPosition = new Vector3(0.6f, 1.05f, -2.0f);

            [JsonProperty(PropertyName = "Button Position")]
            public Vector3 ButtonPosition = new Vector3(-0.4f, -0.3f, 1.32f);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You do not have permission to use this command!",
                ["OnCoolDown"] = "You are on cooldown!",
                ["AlreadyHaveCar"] = "You already have a police sedan. To destroy it type /destroyps",
                ["SedanDestroyed"] = "Your police sedan have been destroyed.",
                ["DoNotOwnSedan"] = "You do not own police sedan."
            }, this, "en");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "Nemas opravneni na to, aby si mohl pouzit tento prikaz!",
                ["OnCoolDown"] = "Musis pockat jeste ",
                ["AlreadyHaveCar"] = "Jiz vlastnis policejni auto. Muzes ho znicit pomoci /destroyps",
                ["SedanDestroyed"] = "Tve policejni vozidlo bylo zniceno.",
                ["DoNotOwnSedan"] = "Nevlastnis policejni vozidlo."
            }, this, "cs");
        }

        #endregion
    }
}