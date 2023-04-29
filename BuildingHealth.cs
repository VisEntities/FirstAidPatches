using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Building Health", "Judess69er/Orange", "1.1.3")]
    [Description("Allows you to change the maximum health of buildings")]
    public class BuildingHealth : RustPlugin
    {
        #region Vars

        private Dictionary<ulong, float> data = new Dictionary<ulong, float>();
        private List<string> changed = new List<string>();
        private static readonly string[] buildingBlocks = new string[]
        {
            "roof",
			"roof.triangle",
            "block.stair.ushape",
            "block.stair.lshape",
            "wall",
            "wall.low",
            "wall.half",
            "wall.frame",
            "wall.window",
            "wall.doorway",
            "floor",
            "floor.triangle",
            "floor.frame",
            "foundation",
            "foundation.steps",
            "foundation.triangle",
            "ramp",
            "floor.triangle.frame",
            "block.stair.spiral.triangle",
            "block.stair.spiral",
            "block.stair.lshape"
        };

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            Update(false);
        }

        private void Unload()
        {
            Update(true);
        }
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            CheckEntity(go.ToBaseEntity());
        }

        #endregion

        #region Helpers

        private void Update(bool unload)
        {
            UpdateDB();
            ResetMultiplier(unload);
            ResetHP();
        }

        private void CheckEntity(BaseEntity entity)
        {
            if (entity == null) {return;}
            var name = entity.ShortPrefabName;
            if (changed.Contains(name)) {return;}
            
            var percent = 0;
            if (config.percents.TryGetValue(entity.ShortPrefabName, out percent) == false || percent == 0)
            {
                return;
            }
            
            var block = entity.GetComponent<BuildingBlock>();
            if (block == null) {return;}
            var hp = block.health / block.MaxHealth();
            block.blockDefinition.healthMultiplier = percent / 100f;
            block.health = hp * block.MaxHealth();
            changed.Add(name);
        }

        private void UpdateDB()
        {
            data.Clear();
            
            foreach (var block in UnityEngine.Object.FindObjectsOfType<BuildingBlock>())
            {
                var id = block.net.ID.Value;
                var hp = block.health / block.MaxHealth();
                data.TryAdd(id, hp);
            }
        }
        
        private void ResetHP()
        {
            foreach (var block in UnityEngine.Object.FindObjectsOfType<BuildingBlock>())
            {
                var id = block.net.ID.Value;
                if(!data.ContainsKey(id)) {return;}
                block.health = data[id] * block.MaxHealth();
            }
        }

        private void ResetMultiplier(bool reset = false)
        {
            foreach (var block in UnityEngine.Object.FindObjectsOfType<BuildingBlock>())
            {
                var name = block.ShortPrefabName;
                var percent = 0;
                if (config.percents.TryGetValue(block.ShortPrefabName, out percent) == false || percent == 0)
                {
                    continue;
                }
                
                var multiplier = reset ? 1f : percent /100f;
                block.blockDefinition.healthMultiplier = multiplier;
                if (!changed.Contains(name)) {changed.Add(name);}
            }
        }

        #endregion

        #region Configuration | 24.05.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Building health in percents")]
            public Dictionary<string, int> percents = new Dictionary<string, int>();
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
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private static void ValidateConfig()
        {
            if (ConVar.Server.hostname.Contains("[DEBUG]") == true)
            {
                config = new ConfigData();
            }
            
            foreach (var value in buildingBlocks)
            {
                if (config.percents.ContainsKey(value) == false)
                {
                    config.percents.Add(value, 200);
                }
            }
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
    }
}