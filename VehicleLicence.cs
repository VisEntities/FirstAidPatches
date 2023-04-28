// #define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facepunch;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Rust;
using Rust.Modular;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Vehicle Licence", "Sorrow/TheDoc/Arainrr", "1.7.42")]
    [Description("Allows players to buy vehicles and then spawn or store it")]
    public class VehicleLicence : RustPlugin
    {
        #region Fields

        [PluginReference]
        private readonly Plugin Economics, ServerRewards, Friends, Clans, NoEscape, LandOnCargoShip, RustTranslationAPI;

        private const string PERMISSION_USE = "vehiclelicence.use";
        private const string PERMISSION_ALL = "vehiclelicence.all";
        private const string PERMISSION_ADMIN = "vehiclelicence.admin";

        private const string PERMISSION_BYPASS_COST = "vehiclelicence.bypasscost";

        private const int ITEMID_FUEL = -946369541;
        private const string PREFAB_ITEM_DROP = "assets/prefabs/misc/item drop/item_drop.prefab";

        private const string PREFAB_ROWBOAT = "assets/content/vehicles/boats/rowboat/rowboat.prefab";
        private const string PREFAB_RHIB = "assets/content/vehicles/boats/rhib/rhib.prefab";
        private const string PREFAB_SEDAN = "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        private const string PREFAB_HOTAIRBALLOON = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab";
        private const string PREFAB_MINICOPTER = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private const string PREFAB_TRANSPORTCOPTER = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";
        private const string PREFAB_CHINOOK = "assets/prefabs/npc/ch47/ch47.entity.prefab";
        private const string PREFAB_RIDABLEHORSE = "assets/rust.ai/nextai/testridablehorse.prefab";
        private const string PREFAB_WORKCART = "assets/content/vehicles/trains/workcart/workcart.entity.prefab";
        private const string PREFAB_SEDANRAIL = "assets/content/vehicles/sedan_a/sedanrail.entity.prefab";
        private const string PREFAB_MAGNET_CRANE = "assets/content/vehicles/crane_magnet/magnetcrane.entity.prefab";
        private const string PREFAB_SUBMARINE_DUO = "assets/content/vehicles/submarine/submarineduo.entity.prefab";
        private const string PREFAB_SUBMARINE_SOLO = "assets/content/vehicles/submarine/submarinesolo.entity.prefab";

        private const string PREFAB_CHASSIS_SMALL = "assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab";
        private const string PREFAB_CHASSIS_MEDIUM = "assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab";
        private const string PREFAB_CHASSIS_LARGE = "assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab";

        private const string PREFAB_SNOWMOBILE = "assets/content/vehicles/snowmobiles/snowmobile.prefab";
        private const string PREFAB_SNOWMOBILE_TOMAHA = "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab";

        // Train Engine
        private const string PREFAB_TRAINENGINE = "assets/content/vehicles/trains/workcart/workcart_aboveground.entity.prefab";
        private const string PREFAB_TRAINENGINE_COVERED = "assets/content/vehicles/trains/workcart/workcart_aboveground2.entity.prefab";
        private const string PREFAB_TRAINENGINE_LOCOMOTIVE = "assets/content/vehicles/trains/locomotive/locomotive.entity.prefab";

        // Train Car
        private const string PREFAB_TRAINWAGON_A = "assets/content/vehicles/trains/wagons/trainwagona.entity.prefab";
        private const string PREFAB_TRAINWAGON_B = "assets/content/vehicles/trains/wagons/trainwagonb.entity.prefab";
        private const string PREFAB_TRAINWAGON_C = "assets/content/vehicles/trains/wagons/trainwagonc.entity.prefab";
        private const string PREFAB_TRAINWAGON_UNLOADABLE = "assets/content/vehicles/trains/wagons/trainwagonunloadable.entity.prefab";
        private const string PREFAB_TRAINWAGON_UNLOADABLE_FUEL = "assets/content/vehicles/trains/wagons/trainwagonunloadablefuel.entity.prefab";
        private const string PREFAB_TRAINWAGON_UNLOADABLE_LOOT = "assets/content/vehicles/trains/wagons/trainwagonunloadableloot.entity.prefab";
        private const string PREFAB_CABOOSE = "assets/content/vehicles/trains/caboose/traincaboose.entity.prefab";

        private const int LAYER_GROUND = Layers.Solid | Layers.Mask.Water;

        private readonly object _false = false;

        public static VehicleLicence Instance { get; private set; }

        public readonly Dictionary<BaseEntity, Vehicle> vehiclesCache = new Dictionary<BaseEntity, Vehicle>();
        public readonly Dictionary<string, BaseVehicleSettings> allVehicleSettings = new Dictionary<string, BaseVehicleSettings>();
        public readonly Dictionary<string, string> commandToVehicleType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public enum NormalVehicleType
        {
            Rowboat,
            RHIB,
            Sedan,
            HotAirBalloon,
            MiniCopter,
            TransportHelicopter,
            Chinook,
            RidableHorse,
            WorkCart,
            SedanRail,
            MagnetCrane,
            SubmarineSolo,
            SubmarineDuo,
            Snowmobile,
            TomahaSnowmobile
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ChassisType
        {
            Small,
            Medium,
            Large
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum TrainComponentType
        {
            Engine,
            CoveredEngine,
            Locomotive,
            WagonA,
            WagonB,
            WagonC,
            Unloadable,
            UnloadableLoot,
            UnloadableFuel,
            Caboose,
        }

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            LoadData();
            Instance = this;
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_ALL, this);
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            permission.RegisterPermission(PERMISSION_BYPASS_COST, this);

            foreach (NormalVehicleType value in Enum.GetValues(typeof(NormalVehicleType)))
            {
                allVehicleSettings.Add(value.ToString(), GetBaseVehicleSettings(value));
            }
            foreach (var entry in configData.modularVehicles)
            {
                allVehicleSettings.Add(entry.Key, entry.Value);
            }
            foreach (var entry in configData.trainVehicles)
            {
                allVehicleSettings.Add(entry.Key, entry.Value);
            }
            foreach (var entry in allVehicleSettings)
            {
                var settings = entry.Value;
                if (settings.UsePermission && !string.IsNullOrEmpty(settings.Permission))
                {
                    if (!permission.PermissionExists(settings.Permission, this))
                    {
                        permission.RegisterPermission(settings.Permission, this);
                    }
                }

                foreach (var perm in settings.CooldownPermissions.Keys)
                {
                    if (!permission.PermissionExists(perm, this))
                    {
                        permission.RegisterPermission(perm, this);
                    }
                }

                foreach (var command in settings.Commands)
                {
                    if (string.IsNullOrEmpty(command))
                    {
                        continue;
                    }
                    if (!commandToVehicleType.ContainsKey(command))
                    {
                        commandToVehicleType.Add(command, entry.Key);
                    }
                    else
                    {
                        PrintError($"You have the same two commands({command}).");
                    }
                    if (configData.chat.useUniversalCommand)
                    {
                        cmd.AddChatCommand(command, this, nameof(CmdUniversal));
                    }
                    if (!string.IsNullOrEmpty(configData.chat.customKillCommandPrefix))
                    {
                        cmd.AddChatCommand(configData.chat.customKillCommandPrefix + command, this, nameof(CmdCustomKill));
                    }
                }
            }

            cmd.AddChatCommand(configData.chat.helpCommand, this, nameof(CmdLicenseHelp));
            cmd.AddChatCommand(configData.chat.buyCommand, this, nameof(CmdBuyVehicle));
            cmd.AddChatCommand(configData.chat.spawnCommand, this, nameof(CmdSpawnVehicle));
            cmd.AddChatCommand(configData.chat.recallCommand, this, nameof(CmdRecallVehicle));
            cmd.AddChatCommand(configData.chat.killCommand, this, nameof(CmdKillVehicle));

            Unsubscribe(nameof(CanMountEntity));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnEntityDismounted));
            Unsubscribe(nameof(OnEntityEnter));
            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnRidableAnimalClaimed));
        }

        private void OnServerInitialized()
        {
            var currentTimestamp = TimeEx.currentTimestamp;
            foreach (var playerData in storedData.playerData)
            {
                foreach (var entry in playerData.Value)
                {
                    entry.Value.PlayerId = playerData.Key;
                    entry.Value.VehicleType = entry.Key;
                    if (configData.global.storeVehicle)
                    {
                        entry.Value.LastRecall = entry.Value.LastDismount = currentTimestamp;
                        if (entry.Value.EntityId == 0)
                        {
                            continue;
                        }
                        NetworkableId id = new NetworkableId(entry.Value.EntityId);
                        entry.Value.Entity = BaseNetworkable.serverEntities.Find(id) as BaseEntity;
                        if (entry.Value.Entity == null || entry.Value.Entity.IsDestroyed)
                        {
                            entry.Value.EntityId = 0;
                        }
                        else
                        {
                            vehiclesCache.Add(entry.Value.Entity, entry.Value);
                        }
                    }
                }
            }
            if (configData.global.preventMounting)
            {
                Subscribe(nameof(CanMountEntity));
            }
            if (configData.global.noDecay)
            {
                Subscribe(nameof(OnEntityTakeDamage));
            }
            if (configData.global.preventDamagePlayer)
            {
                Subscribe(nameof(OnEntityEnter));
            }
            if (configData.global.preventLooting)
            {
                Subscribe(nameof(CanLootEntity));
            }
            if (configData.global.autoClaimFromVendor)
            {
                Subscribe(nameof(OnEntitySpawned));
                Subscribe(nameof(OnRidableAnimalClaimed));
            }
            if (configData.global.checkVehiclesInterval > 0 && allVehicleSettings.Any(x => x.Value.WipeTime > 0))
            {
                Subscribe(nameof(OnEntityDismounted));
                timer.Every(configData.global.checkVehiclesInterval, CheckVehicles);
            }
        }

        private void Unload()
        {
            if (!configData.global.storeVehicle)
            {
                foreach (var entry in vehiclesCache.ToArray())
                {
                    if (entry.Key != null && !entry.Key.IsDestroyed)
                    {
                        RefundVehicleItems(entry.Value, isUnload: true);
                        entry.Key.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                    entry.Value.EntityId = 0;
                }
            }
            SaveData();
            Instance = null;
        }

        private void OnServerSave()
        {
            timer.Once(Random.Range(0f, 60f), SaveData);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId())
            {
                return;
            }
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS_COST))
            {
                PurchaseAllVehicles(player.userID);
            }
        }

        private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (entity == null)
            {
                return;
            }
            var vehicleParent = entity.VehicleParent();
            if (vehicleParent == null || vehicleParent.IsDestroyed)
            {
                return;
            }
            Vehicle vehicle;
            if (!vehiclesCache.TryGetValue(vehicleParent, out vehicle))
            {
                return;
            }
            vehicle.OnDismount();
        }

        #region Mount

        private object CanMountEntity(BasePlayer friend, BaseMountable entity)
        {
            if (friend == null || entity == null)
            {
                return null;
            }
            var vehicleParent = entity.VehicleParent();
            if (vehicleParent == null || vehicleParent.IsDestroyed)
            {
                return null;
            }
            Vehicle vehicle;
            if (!vehiclesCache.TryGetValue(vehicleParent, out vehicle))
            {
                return null;
            }
            if (AreFriends(vehicle.PlayerId, friend.userID))
            {
                return null;
            }
            if (configData.global.preventDriverSeat && vehicleParent.HasMountPoints())
            {
                foreach (var mountPointInfo in vehicleParent.allMountPoints)
                {
                    if (mountPointInfo != null && mountPointInfo.mountable == entity)
                    {
                        if (!mountPointInfo.isDriver)
                        {
                            return null;
                        }
                        break;
                    }
                }
            }
            if (HasAdminPermission(friend))
            {
                return null;
            }
            SendCantUseMessage(friend, vehicle);
            return _false;
        }

        #endregion Mount

        #region Loot

        private object CanLootEntity(BasePlayer friend, RidableHorse horse)
        {
            if (friend == null || horse == null)
            {
                return null;
            }
            return CanLootEntityInternal(friend, horse);
        }

        private object CanLootEntity(BasePlayer friend, StorageContainer container)
        {
            if (friend == null || container == null)
            {
                return null;
            }
            var parentEntity = container.GetParentEntity();
            if (parentEntity == null)
            {
                return null;
            }
            return CanLootEntityInternal(friend, parentEntity);
        }

        private object CanLootEntityInternal(BasePlayer friend, BaseEntity parentEntity)
        {
            Vehicle vehicle;
            if (!TryGetVehicle(parentEntity, out vehicle))
            {
                return null;
            }
            if (AreFriends(vehicle.PlayerId, friend.userID))
            {
                return null;
            }
            if (HasAdminPermission(friend))
            {
                return null;
            }
            SendCantUseMessage(friend, vehicle);
            return _false;
        }

        #endregion Loot

        #region Decay

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo?.damageTypes == null)
            {
                return;
            }
            if (hitInfo.damageTypes.Has(DamageType.Decay))
            {
                Vehicle vehicle;
                if (!TryGetVehicle(entity, out vehicle))
                {
                    return;
                }
                hitInfo.damageTypes.Scale(DamageType.Decay, 0);
            }
        }

        #endregion Decay

        #region Claim

        private void OnEntitySpawned(BaseSubmarine baseSubmarine)
        {
            TryClaimVehicle(baseSubmarine);
        }

        private void OnEntitySpawned(MotorRowboat motorRowboat)
        {
            TryClaimVehicle(motorRowboat);
        }

        private void OnEntitySpawned(MiniCopter miniCopter)
        {
            TryClaimVehicle(miniCopter);
        }

        private void OnRidableAnimalClaimed(BaseRidableAnimal ridableAnimal, BasePlayer player)
        {
            TryClaimVehicle(ridableAnimal, player);
        }

        #endregion Claim

        #region Damage

        // ScrapTransportHelicopter / ModularCar / TrainEngine / MagnetCrane
        private object OnEntityEnter(TriggerHurtNotChild triggerHurtNotChild, BasePlayer player)
        {
            if (triggerHurtNotChild == null || player == null || triggerHurtNotChild.SourceEntity == null)
            {
                return null;
            }
            var sourceEntity = triggerHurtNotChild.SourceEntity;
            if (vehiclesCache.ContainsKey(sourceEntity))
            {
                var baseVehicle = sourceEntity as BaseVehicle;
                if (baseVehicle != null && player.userID.IsSteamId())
                {
                    if (baseVehicle is TrainEngine)
                    {
                        var transform = triggerHurtNotChild.transform;
                        MoveToPosition(player, transform.position + (Random.value >= 0.5f ? -transform.right : transform.right) * 2.5f);
                        return _false;
                    }
                    Vector3 pos;
                    if (GetDismountPosition(baseVehicle, player, out pos))
                    {
                        MoveToPosition(player, pos);
                    }
                }
                //triggerHurtNotChild.enabled = false;
                return _false;
            }
            return null;
        }

        // HotAirBalloon
        private object OnEntityEnter(TriggerHurt triggerHurt, BasePlayer player)
        {
            if (triggerHurt == null || player == null)
            {
                return null;
            }
            var sourceEntity = triggerHurt.gameObject.ToBaseEntity();
            if (sourceEntity == null)
            {
                return null;
            }
            if (vehiclesCache.ContainsKey(sourceEntity))
            {
                if (player.userID.IsSteamId())
                {
                    MoveToPosition(player, sourceEntity.CenterPoint() + Vector3.down);
                }
                //triggerHurt.enabled = false;
                return _false;
            }
            return null;
        }

        #endregion Damage

        #region Destroy

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            OnEntityDeathOrKill(entity, true);
        }

        private void OnEntityKill(BaseCombatEntity entity)
        {
            OnEntityDeathOrKill(entity);
        }

        #endregion Destroy

        #region Reskin

        private object OnEntityReskin(BaseEntity entity, ItemSkinDirectory.Skin skin, BasePlayer player)
        {
            if (entity == null || player == null)
            {
                return null;
            }
            Vehicle vehicle;
            if (TryGetVehicle(entity, out vehicle))
            {
                return _false;
            }
            return null;
        }

        #endregion Reskin

        #endregion Oxide Hooks

        #region Methods

        #region Message

        private void SendCantUseMessage(BasePlayer friend, Vehicle vehicle)
        {
            var settings = GetBaseVehicleSettings(vehicle.VehicleType);
            if (settings != null)
            {
                var player = RustCore.FindPlayerById(vehicle.PlayerId);
                var playerName = player?.displayName ?? ServerMgr.Instance.persistance.GetPlayerName(vehicle.PlayerId) ?? "Unknown";
                Print(friend, Lang("CantUse", friend.UserIDString, settings.DisplayName, $"<color=#{(player != null && player.IsConnected ? "69D214" : "FF6347")}>{playerName}</color>"));
            }
        }

        #endregion Message

        #region CheckEntity

        private void OnEntityDeathOrKill(BaseCombatEntity entity, bool isCrash = false)
        {
            if (entity == null)
            {
                return;
            }
            Vehicle vehicle;
            if (!vehiclesCache.TryGetValue(entity, out vehicle))
            {
                return;
            }

            RefundVehicleItems(vehicle, isCrash);

            var settings = GetBaseVehicleSettings(vehicle.VehicleType);
            if (isCrash && settings.RemoveLicenseOnceCrash)
            {
                RemoveVehicleLicense(vehicle.PlayerId, vehicle.VehicleType);
            }

            vehicle.OnDeath();
            vehiclesCache.Remove(entity);
        }

        #endregion CheckEntity

        #region CheckVehicles

        private void CheckVehicles()
        {
            var currentTimestamp = TimeEx.currentTimestamp;
            foreach (var entry in vehiclesCache.ToArray())
            {
                if (entry.Key == null || entry.Key.IsDestroyed)
                {
                    continue;
                }
                if (VehicleIsActive(entry.Key, entry.Value, currentTimestamp))
                {
                    continue;
                }

                if (VehicleAnyMounted(entry.Key))
                {
                    continue;
                }
                entry.Key.Kill(BaseNetworkable.DestroyMode.Gib);
            }
        }

        private bool VehicleIsActive(BaseEntity entity, Vehicle vehicle, double currentTimestamp)
        {
            var settings = GetBaseVehicleSettings(vehicle.VehicleType);
            if (settings.WipeTime <= 0)
            {
                return true;
            }
            if (settings.ExcludeCupboard && entity.GetBuildingPrivilege() != null)
            {
                return true;
            }
            return currentTimestamp - vehicle.LastDismount < settings.WipeTime;
        }

        #endregion CheckVehicles

        #region Refund

        private void RefundVehicleItems(Vehicle vehicle, bool isCrash = false, bool isUnload = false)
        {
            var entity = vehicle.Entity;
            if (entity == null || entity.IsDestroyed)
            {
                return;
            }

            var settings = GetBaseVehicleSettings(vehicle.VehicleType);
            settings.RefundVehicleItems(vehicle, isCrash, isUnload);
        }

        private static void DropItemContainer(BaseEntity entity, ulong playerId, List<Item> collect)
        {
            var droppedItemContainer = GameManager.server.CreateEntity(PREFAB_ITEM_DROP, entity.GetDropPosition(), entity.transform.rotation) as DroppedItemContainer;
            if (droppedItemContainer != null)
            {
                droppedItemContainer.inventory = new ItemContainer();
                droppedItemContainer.inventory.ServerInitialize(null, Mathf.Min(collect.Count, droppedItemContainer.maxItemCount));
                droppedItemContainer.inventory.GiveUID();
                droppedItemContainer.inventory.entityOwner = droppedItemContainer;
                droppedItemContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                for (var i = collect.Count - 1; i >= 0; i--)
                {
                    var item = collect[i];
                    if (!item.MoveToContainer(droppedItemContainer.inventory))
                    {
                        item.DropAndTossUpwards(droppedItemContainer.transform.position);
                    }
                }

                droppedItemContainer.OwnerID = playerId;
                droppedItemContainer.Spawn();
            }
        }

        #endregion Refund

        #region TryPay

        private bool TryPay(BasePlayer player, Dictionary<string, PriceInfo> prices, out string resources)
        {
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS_COST))
            {
                resources = null;
                return true;
            }

            if (!CanPay(player, prices, out resources))
            {
                return false;
            }

            var collect = Pool.GetList<Item>();
            foreach (var entry in prices)
            {
                if (entry.Value.amount <= 0)
                {
                    continue;
                }
                var itemDefinition = ItemManager.FindItemDefinition(entry.Key);
                if (itemDefinition != null)
                {
                    player.inventory.Take(collect, itemDefinition.itemid, entry.Value.amount);
                    player.Command("note.inv", itemDefinition.itemid, -entry.Value.amount);
                    continue;
                }
                switch (entry.Key.ToLower())
                {
                    case "economics":
                        Economics?.Call("Withdraw", player.userID, (double)entry.Value.amount);
                        continue;

                    case "serverrewards":
                        ServerRewards?.Call("TakePoints", player.userID, entry.Value.amount);
                        continue;
                }
            }

            foreach (var item in collect)
            {
                item.Remove();
            }
            Pool.FreeList(ref collect);
            resources = null;
            return true;
        }

        private bool CanPay(BasePlayer player, Dictionary<string, PriceInfo> prices, out string resources)
        {
            var entries = new Hash<string, int>();
            var language = RustTranslationAPI != null ? lang.GetLanguage(player.UserIDString) : null;
            foreach (var entry in prices)
            {
                if (entry.Value.amount <= 0)
                {
                    continue;
                }
                int missingAmount;
                var itemDefinition = ItemManager.FindItemDefinition(entry.Key);
                if (itemDefinition != null)
                {
                    missingAmount = entry.Value.amount - player.inventory.GetAmount(itemDefinition.itemid);
                }
                else
                {
                    missingAmount = CheckBalance(entry.Key, entry.Value.amount, player.userID);
                }

                if (missingAmount <= 0)
                {
                    continue;
                }
                var displayName = GetItemDisplayName(language, entry.Key, entry.Value.displayName);
                entries[displayName] += missingAmount;
            }
            if (entries.Count > 0)
            {
                var stringBuilder = new StringBuilder();
                foreach (var entry in entries)
                {
                    stringBuilder.AppendLine($"* {Lang("PriceFormat", player.UserIDString, entry.Key, entry.Value)}");
                }
                resources = stringBuilder.ToString();
                return false;
            }
            resources = null;
            return true;
        }

        private int CheckBalance(string key, int price, ulong playerId)
        {
            switch (key.ToLower())
            {
                case "economics":
                    var balance = Economics?.Call("Balance", playerId);
                    if (balance is double)
                    {
                        var n = price - (double)balance;
                        return n <= 0 ? 0 : (int)Math.Ceiling(n);
                    }
                    return price;

                case "serverrewards":
                    var points = ServerRewards?.Call("CheckPoints", playerId);
                    if (points is int)
                    {
                        var n = price - (int)points;
                        return n <= 0 ? 0 : n;
                    }
                    return price;

                default:
                    PrintError($"Unknown Currency Type '{key}'");
                    return price;
            }
        }

        #endregion TryPay

        #region AreFriends

        private bool AreFriends(ulong playerId, ulong friendId)
        {
            if (playerId == friendId)
            {
                return true;
            }
            if (configData.global.useTeams && SameTeam(playerId, friendId))
            {
                return true;
            }

            if (configData.global.useFriends && HasFriend(playerId, friendId))
            {
                return true;
            }
            if (configData.global.useClans && SameClan(playerId, friendId))
            {
                return true;
            }
            return false;
        }

        private static bool SameTeam(ulong playerId, ulong friendId)
        {
            if (!RelationshipManager.TeamsEnabled())
            {
                return false;
            }
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerId);
            if (playerTeam == null)
            {
                return false;
            }
            var friendTeam = RelationshipManager.ServerInstance.FindPlayersTeam(friendId);
            if (friendTeam == null)
            {
                return false;
            }
            return playerTeam == friendTeam;
        }

        private bool HasFriend(ulong playerId, ulong friendId)
        {
            if (Friends == null)
            {
                return false;
            }
            return (bool)Friends.Call("HasFriend", playerId, friendId);
        }

        private bool SameClan(ulong playerId, ulong friendId)
        {
            if (Clans == null)
            {
                return false;
            }
            //Clans
            var isMember = Clans.Call("IsClanMember", playerId.ToString(), friendId.ToString());
            if (isMember != null)
            {
                return (bool)isMember;
            }
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerId);
            if (playerClan == null)
            {
                return false;
            }
            var friendClan = Clans.Call("GetClanOf", friendId);
            if (friendClan == null)
            {
                return false;
            }
            return playerClan == friendClan;
        }

        #endregion AreFriends

        #region IsPlayerBlocked

        private bool IsPlayerBlocked(BasePlayer player)
        {
            if (NoEscape == null)
            {
                return false;
            }
            if (configData.global.useRaidBlocker && IsRaidBlocked(player.UserIDString))
            {
                Print(player, Lang("RaidBlocked", player.UserIDString));
                return true;
            }
            if (configData.global.useCombatBlocker && IsCombatBlocked(player.UserIDString))
            {
                Print(player, Lang("CombatBlocked", player.UserIDString));
                return true;
            }
            return false;
        }

        private bool IsRaidBlocked(string playerId)
        {
            return (bool)NoEscape.Call("IsRaidBlocked", playerId);
        }

        private bool IsCombatBlocked(string playerId)
        {
            return (bool)NoEscape.Call("IsCombatBlocked", playerId);
        }

        #endregion IsPlayerBlocked

        #region GetSettings

        private BaseVehicleSettings GetBaseVehicleSettings(string vehicleType)
        {
            BaseVehicleSettings settings;
            return allVehicleSettings.TryGetValue(vehicleType, out settings) ? settings : null;
        }

        private BaseVehicleSettings GetBaseVehicleSettings(NormalVehicleType normalVehicleType)
        {
            switch (normalVehicleType)
            {
                case NormalVehicleType.Rowboat:
                    return configData.normalVehicles.rowboat;
                case NormalVehicleType.RHIB:
                    return configData.normalVehicles.rhib;
                case NormalVehicleType.Sedan:
                    return configData.normalVehicles.sedan;
                case NormalVehicleType.HotAirBalloon:
                    return configData.normalVehicles.hotAirBalloon;
                case NormalVehicleType.MiniCopter:
                    return configData.normalVehicles.miniCopter;
                case NormalVehicleType.TransportHelicopter:
                    return configData.normalVehicles.transportHelicopter;
                case NormalVehicleType.Chinook:
                    return configData.normalVehicles.chinook;
                case NormalVehicleType.RidableHorse:
                    return configData.normalVehicles.ridableHorse;
                case NormalVehicleType.WorkCart:
                    return configData.normalVehicles.workCart;
                case NormalVehicleType.SedanRail:
                    return configData.normalVehicles.sedanRail;
                case NormalVehicleType.MagnetCrane:
                    return configData.normalVehicles.magnetCrane;
                case NormalVehicleType.SubmarineSolo:
                    return configData.normalVehicles.submarineSolo;
                case NormalVehicleType.SubmarineDuo:
                    return configData.normalVehicles.submarineDuo;
                case NormalVehicleType.Snowmobile:
                    return configData.normalVehicles.snowmobile;
                case NormalVehicleType.TomahaSnowmobile:
                    return configData.normalVehicles.tomahaSnowmobile;
                default:
                    return null;
            }
        }

        #endregion GetSettings

        #region Permission

        private bool HasAdminPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN);
        }

        private bool CanViewVehicleInfo(BasePlayer player, string vehicleType, BaseVehicleSettings settings)
        {
            if (settings.Purchasable && settings.Commands.Count > 0)
            {
                return HasVehiclePermission(player, vehicleType);
            }
            return false;
        }

        private bool HasVehiclePermission(BasePlayer player, string vehicleType)
        {
            var settings = GetBaseVehicleSettings(vehicleType);
            if (!settings.UsePermission || string.IsNullOrEmpty(settings.Permission))
            {
                return true;
            }
            return permission.UserHasPermission(player.UserIDString, PERMISSION_ALL) ||
                    permission.UserHasPermission(player.UserIDString, settings.Permission);
        }

        #endregion Permission

        #region Claim

        private void TryClaimVehicle(BaseVehicle baseVehicle)
        {
            NextTick(() =>
            {
                if (baseVehicle == null)
                {
                    return;
                }
                var player = baseVehicle.creatorEntity as BasePlayer;
                if (player == null || !player.userID.IsSteamId() || !baseVehicle.OnlyOwnerAccessible())
                {
                    return;
                }
                var vehicleType = GetClaimableVehicleType(baseVehicle);
                if (vehicleType.HasValue)
                {
                    TryClaimVehicle(player, baseVehicle, vehicleType.Value.ToString());
                }
            });
        }

        private void TryClaimVehicle(BaseVehicle baseVehicle, BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId())
            {
                return;
            }
            var vehicleType = GetClaimableVehicleType(baseVehicle);
            if (vehicleType.HasValue)
            {
                TryClaimVehicle(player, baseVehicle, vehicleType.Value.ToString());
            }
        }

        private bool TryClaimVehicle(BasePlayer player, BaseEntity entity, string vehicleType)
        {
            Vehicle vehicle;
            if (!storedData.IsVehiclePurchased(player.userID, vehicleType, out vehicle))
            {
                if (!configData.global.autoUnlockFromVendor)
                {
                    return false;
                }
                storedData.AddVehicleLicense(player.userID, vehicleType);
                vehicle = storedData.GetVehicleLicense(player.userID, vehicleType);
            }
            if (vehicle.Entity == null || vehicle.Entity.IsDestroyed)
            {
                var settings = GetBaseVehicleSettings(vehicle.VehicleType);
                if (settings != null)
                {
                    settings.PreSetupVehicle(entity, vehicle, player);
                    settings.SetupVehicle(entity, vehicle, player, false);
                }
                CacheVehicleEntity(entity, vehicle, player);
                return true;
            }
            return false;
        }

        #endregion Claim

        private bool TryGetVehicle(BaseEntity entity, out Vehicle vehicle)
        {
            if (!vehiclesCache.TryGetValue(entity, out vehicle))
            {
                var vehicleModule = entity as BaseVehicleModule;
                if (vehicleModule == null)
                {
                    return false;
                }
                var parent = vehicleModule.Vehicle;
                if (parent == null || !vehiclesCache.TryGetValue(parent, out vehicle))
                {
                    return false;
                }
            }
            return true;
        }

        #region Helpers

        private static NormalVehicleType? GetClaimableVehicleType(BaseVehicle baseVehicle)
        {
            if (baseVehicle is BaseRidableAnimal)
            {
                return NormalVehicleType.RidableHorse;
            }
            if (baseVehicle is ScrapTransportHelicopter)
            {
                return NormalVehicleType.TransportHelicopter;
            }
            if (baseVehicle is MiniCopter)
            {
                return NormalVehicleType.MiniCopter;
            }
            if (baseVehicle is RHIB)
            {
                return NormalVehicleType.RHIB;
            }
            if (baseVehicle is MotorRowboat)
            {
                return NormalVehicleType.Rowboat;
            }
            if (baseVehicle is SubmarineDuo)
            {
                return NormalVehicleType.SubmarineDuo;
            }
            if (baseVehicle is BaseSubmarine)
            {
                return NormalVehicleType.SubmarineSolo;
            }
            return null;
        }

        private static bool GetDismountPosition(BaseVehicle baseVehicle, BasePlayer player, out Vector3 result)
        {
            var parentVehicle = baseVehicle.VehicleParent();
            if (parentVehicle != null)
            {
                return GetDismountPosition(parentVehicle, player, out result);
            }
            var list = Pool.GetList<Vector3>();
            foreach (var transform in baseVehicle.dismountPositions)
            {
                if (baseVehicle.ValidDismountPosition(player, transform.position))
                {
                    list.Add(transform.position);
                    if (baseVehicle.dismountStyle == BaseVehicle.DismountStyle.Ordered)
                    {
                        break;
                    }
                }
            }
            if (list.Count == 0)
            {
                result = Vector3.zero;
                Pool.FreeList(ref list);
                return false;
            }
            var pos = player.transform.position;
            list.Sort((a, b) => Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos)));
            result = list[0];
            Pool.FreeList(ref list);
            return true;
        }

        private static bool VehicleAnyMounted(BaseEntity entity)
        {
            var baseVehicle = entity as BaseVehicle;
            if (baseVehicle != null && baseVehicle.AnyMounted())
            {
                return true;
            }
            return entity.GetComponentsInChildren<BasePlayer>()?.Length > 0;
        }

        private static void DismountAllPlayers(BaseEntity entity)
        {
            var baseVehicle = entity as BaseVehicle;
            if (baseVehicle != null)
            {
                //(vehicle as BaseVehicle).DismountAllPlayers();
                foreach (var mountPointInfo in baseVehicle.allMountPoints)
                {
                    if (mountPointInfo != null && mountPointInfo.mountable != null)
                    {
                        var mounted = mountPointInfo.mountable.GetMounted();
                        if (mounted != null)
                        {
                            mountPointInfo.mountable.DismountPlayer(mounted);
                        }
                    }
                }
            }
            var players = entity.GetComponentsInChildren<BasePlayer>();
            foreach (var player in players)
            {
                player.SetParent(null, true, true);
            }
        }

        private static Vector3 GetGroundPositionLookingAt(BasePlayer player, float distance, bool needUp = true)
        {
            RaycastHit hitInfo;
            var headRay = player.eyes.HeadRay();
            if (Physics.Raycast(headRay, out hitInfo, distance, LAYER_GROUND))
            {
                return hitInfo.point;
            }
            return GetGroundPosition(headRay.origin + headRay.direction * distance, needUp);
        }

        private static Vector3 GetGroundPosition(Vector3 position, bool needUp = true)
        {
            RaycastHit hitInfo;
            position.y = Physics.Raycast(needUp ? position + Vector3.up * 250 : position, Vector3.down, out hitInfo, needUp ? 400f : 50f, LAYER_GROUND)
                    ? hitInfo.point.y
                    : TerrainMeta.HeightMap.GetHeight(position);
            return position;
        }

        private static bool IsInWater(Vector3 position)
        {
            var colliders = Pool.GetList<Collider>();
            Vis.Colliders(position, 0.5f, colliders);
            var flag = colliders.Any(x => x.gameObject.layer == (int)Layer.Water);
            Pool.FreeList(ref colliders);
            return flag || WaterLevel.Test(position);
        }

        private static void MoveToPosition(BasePlayer player, Vector3 position)
        {
            player.Teleport(position);
            player.ForceUpdateTriggers();
            //if (player.HasParent()) player.SetParent(null, true, true);
            player.SendNetworkUpdateImmediate();
        }

        #region Train Car

        #endregion

        #endregion Helpers

        #endregion Methods

        #region API

        [HookMethod(nameof(IsLicensedVehicle))]
        public bool IsLicensedVehicle(BaseEntity entity)
        {
            return vehiclesCache.ContainsKey(entity);
        }

        [HookMethod(nameof(GetLicensedVehicle))]
        public BaseEntity GetLicensedVehicle(ulong playerId, string license)
        {
            return storedData.GetVehicleLicense(playerId, license)?.Entity;
        }

        [HookMethod(nameof(HasVehicleLicense))]
        public bool HasVehicleLicense(ulong playerId, string license)
        {
            return storedData.HasVehicleLicense(playerId, license);
        }

        [HookMethod(nameof(RemoveVehicleLicense))]
        public bool RemoveVehicleLicense(ulong playerId, string license)
        {
            return storedData.RemoveVehicleLicense(playerId, license);
        }

        [HookMethod(nameof(AddVehicleLicense))]
        public bool AddVehicleLicense(ulong playerId, string license)
        {
            return storedData.AddVehicleLicense(playerId, license);
        }

        [HookMethod(nameof(GetVehicleLicenses))]
        public List<string> GetVehicleLicenses(ulong playerId)
        {
            return storedData.GetVehicleLicenseNames(playerId);
        }

        [HookMethod(nameof(PurchaseAllVehicles))]
        public void PurchaseAllVehicles(ulong playerId)
        {
            storedData.PurchaseAllVehicles(playerId);
        }

        #endregion API

        #region Commands

        #region Universal Command

        private void CmdUniversal(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }

            string vehicleType;
            if (IsValidOption(player, command, out vehicleType))
            {
                var bypassCooldown = args.Length > 0 && IsValidBypassCooldownOption(args[0]);
                HandleUniversalCmd(player, vehicleType, bypassCooldown, command);
            }
        }

        private void HandleUniversalCmd(BasePlayer player, string vehicleType, bool bypassCooldown, string command)
        {
            Vehicle vehicle;
            if (storedData.IsVehiclePurchased(player.userID, vehicleType, out vehicle))
            {
                string reason;
                var position = Vector3.zero;
                var rotation = Quaternion.identity;
                if (vehicle.Entity != null && !vehicle.Entity.IsDestroyed)
                {
                    //recall
                    if (CanRecall(player, vehicle, bypassCooldown, command, out reason, ref position, ref rotation))
                    {
                        RecallVehicle(player, vehicle, position, rotation);
                        return;
                    }
                }
                else
                {
                    //spawn
                    if (CanSpawn(player, vehicle, bypassCooldown, command, out reason, ref position, ref rotation))
                    {
                        SpawnVehicle(player, vehicle, position, rotation);
                        return;
                    }
                }
                Print(player, reason);
                return;
            }
            //buy
            BuyVehicle(player, vehicleType);
        }

        #endregion Universal Command

        #region Custom Kill Command

        private void CmdCustomKill(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            command = command.Remove(0, configData.chat.customKillCommandPrefix.Length);
            HandleKillCmd(player, command);
        }

        #endregion Custom Kill Command

        #region Help Command

        private void CmdLicenseHelp(BasePlayer player, string command, string[] args)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(Lang("Help", player.UserIDString));
            stringBuilder.AppendLine(Lang("HelpLicence1", player.UserIDString, configData.chat.buyCommand));
            stringBuilder.AppendLine(Lang("HelpLicence2", player.UserIDString, configData.chat.spawnCommand));
            stringBuilder.AppendLine(Lang("HelpLicence3", player.UserIDString, configData.chat.recallCommand));
            stringBuilder.AppendLine(Lang("HelpLicence4", player.UserIDString, configData.chat.killCommand));

            foreach (var entry in allVehicleSettings)
            {
                if (CanViewVehicleInfo(player, entry.Key, entry.Value))
                {
                    if (configData.chat.useUniversalCommand)
                    {
                        var firstCmd = entry.Value.Commands[0];
                        stringBuilder.AppendLine(Lang("HelpLicence5", player.UserIDString, firstCmd, entry.Value.DisplayName));
                    }
                }
            }
            Print(player, stringBuilder.ToString());
        }

        #endregion Help Command

        #region Remove Command

        [ConsoleCommand("vl.remove")]
        private void CCmdRemoveVehicle(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin && arg.Args != null && arg.Args.Length == 2)
            {
                var option = arg.Args[0];
                string vehicleType;
                if (!IsValidVehicleType(option, out vehicleType))
                {
                    Print(arg, $"{option} is not a valid vehicle type");
                    return;
                }
                switch (arg.Args[1].ToLower())
                {
                    case "*":
                    case "all":
                    {
                        storedData.RemoveLicenseForAllPlayers(vehicleType);
                        var vehicleName = GetBaseVehicleSettings(vehicleType).DisplayName;
                        Print(arg, $"You successfully removed the vehicle({vehicleName}) of all players");
                    }
                        return;

                    default:
                    {
                        var target = RustCore.FindPlayer(arg.Args[1]);
                        if (target == null)
                        {
                            Print(arg, $"Player '{arg.Args[1]}' not found");
                            return;
                        }

                        var vehicleName = GetBaseVehicleSettings(vehicleType).DisplayName;
                        if (RemoveVehicleLicense(target.userID, vehicleType))
                        {
                            Print(arg, $"You successfully removed the vehicle({vehicleName}) of {target.displayName}");
                            return;
                        }

                        Print(arg, $"{target.displayName} has not purchased vehicle({vehicleName}) and cannot be removed");
                    }
                        return;
                }
            }
        }

        [ConsoleCommand("vl.cleardata")]
        private void CCmdClearVehicle(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin)
            {
                foreach (var vehicle in vehiclesCache.Keys.ToArray())
                {
                    vehicle.Kill(BaseNetworkable.DestroyMode.Gib);
                }
                vehiclesCache.Clear();
                ClearData();
                Print(arg, "You successfully cleaned up all vehicle data");
            }
        }

        #endregion Remove Command

        #region Buy Command

        [ConsoleCommand("vl.buy")]
        private void CCmdBuyVehicle(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin && arg.Args != null && arg.Args.Length == 2)
            {
                var option = arg.Args[0];
                string vehicleType;
                if (!IsValidVehicleType(option, out vehicleType))
                {
                    Print(arg, $"{option} is not a valid vehicle type");
                    return;
                }
                switch (arg.Args[1].ToLower())
                {
                    case "*":
                    case "all":
                    {
                        storedData.AddLicenseForAllPlayers(vehicleType);
                        var vehicleName = GetBaseVehicleSettings(vehicleType).DisplayName;
                        Print(arg, $"You successfully purchased the vehicle({vehicleName}) for all players");
                    }
                        return;

                    default:
                    {
                        var target = RustCore.FindPlayer(arg.Args[1]);
                        if (target == null)
                        {
                            Print(arg, $"Player '{arg.Args[1]}' not found");
                            return;
                        }

                        var vehicleName = GetBaseVehicleSettings(vehicleType).DisplayName;
                        if (AddVehicleLicense(target.userID, vehicleType))
                        {
                            Print(arg, $"You successfully purchased the vehicle({vehicleName}) for {target.displayName}");
                            return;
                        }

                        Print(arg, $"{target.displayName} has purchased vehicle({vehicleName})");
                    }
                        return;
                }
            }
            var player = arg.Player();
            if (player == null)
            {
                Print(arg, $"The server console cannot use the '{arg.cmd.FullName}' command");
            }
            else
            {
                CmdBuyVehicle(player, arg.cmd.FullName, arg.Args);
            }
        }

        private void CmdBuyVehicle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (args == null || args.Length < 1)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(Lang("Help", player.UserIDString));
                foreach (var entry in allVehicleSettings)
                {
                    if (CanViewVehicleInfo(player, entry.Key, entry.Value))
                    {
                        var firstCmd = entry.Value.Commands[0];
                        if (entry.Value.PurchasePrices.Count > 0)
                        {
                            var prices = FormatPriceInfo(player, entry.Value.PurchasePrices);
                            stringBuilder.AppendLine(Lang("HelpBuyPrice", player.UserIDString, configData.chat.buyCommand, firstCmd, entry.Value.DisplayName, prices));
                        }
                        else
                        {
                            stringBuilder.AppendLine(Lang("HelpBuy", player.UserIDString, configData.chat.buyCommand, firstCmd, entry.Value.DisplayName));
                        }
                    }
                }
                Print(player, stringBuilder.ToString());
                return;
            }
            string vehicleType;
            if (IsValidOption(player, args[0], out vehicleType))
            {
                BuyVehicle(player, vehicleType);
            }
        }

        private bool BuyVehicle(BasePlayer player, string vehicleType)
        {
            var settings = GetBaseVehicleSettings(vehicleType);
            if (!settings.Purchasable)
            {
                Print(player, Lang("VehicleCannotBeBought", player.UserIDString, settings.DisplayName));
                return false;
            }
            var vehicles = storedData.GetPlayerVehicles(player.userID, false);
            if (vehicles.ContainsKey(vehicleType))
            {
                Print(player, Lang("VehicleAlreadyPurchased", player.UserIDString, settings.DisplayName));
                return false;
            }
            string resources;
            if (settings.PurchasePrices.Count > 0 && !TryPay(player, settings.PurchasePrices, out resources))
            {
                Print(player, Lang("NoResourcesToPurchaseVehicle", player.UserIDString, settings.DisplayName, resources));
                return false;
            }
            vehicles.Add(vehicleType, Vehicle.Create(player.userID, vehicleType));
            SaveData();
            Print(player, Lang("VehiclePurchased", player.UserIDString, settings.DisplayName, configData.chat.spawnCommand));
            return true;
        }

        #endregion Buy Command

        #region Spawn Command

        [ConsoleCommand("vl.spawn")]
        private void CCmdSpawnVehicle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                Print(arg, $"The server console cannot use the '{arg.cmd.FullName}' command");
            }
            else
            {
                CmdSpawnVehicle(player, arg.cmd.FullName, arg.Args);
            }
        }

        private void CmdSpawnVehicle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (args == null || args.Length < 1)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(Lang("Help", player.UserIDString));
                foreach (var entry in allVehicleSettings)
                {
                    if (CanViewVehicleInfo(player, entry.Key, entry.Value))
                    {
                        var firstCmd = entry.Value.Commands[0];
                        if (entry.Value.SpawnPrices.Count > 0)
                        {
                            var prices = FormatPriceInfo(player, entry.Value.SpawnPrices);
                            stringBuilder.AppendLine(Lang("HelpSpawnPrice", player.UserIDString, configData.chat.spawnCommand, firstCmd, entry.Value.DisplayName, prices));
                        }
                        else
                        {
                            stringBuilder.AppendLine(Lang("HelpSpawn", player.UserIDString, configData.chat.spawnCommand, firstCmd, entry.Value.DisplayName));
                        }
                    }
                }
                Print(player, stringBuilder.ToString());
                return;
            }
            string vehicleType;
            if (IsValidOption(player, args[0], out vehicleType))
            {
                var bypassCooldown = args.Length > 1 && IsValidBypassCooldownOption(args[1]);
                SpawnVehicle(player, vehicleType, bypassCooldown, command + " " + args[0]);
            }
        }

        private bool SpawnVehicle(BasePlayer player, string vehicleType, bool bypassCooldown, string command)
        {
            var settings = GetBaseVehicleSettings(vehicleType);
            Vehicle vehicle;
            if (!storedData.IsVehiclePurchased(player.userID, vehicleType, out vehicle))
            {
                Print(player, Lang("VehicleNotYetPurchased", player.UserIDString, settings.DisplayName, configData.chat.buyCommand));
                return false;
            }
            if (vehicle.Entity != null && !vehicle.Entity.IsDestroyed)
            {
                Print(player, Lang("AlreadyVehicleOut", player.UserIDString, settings.DisplayName, configData.chat.recallCommand));
                return false;
            }
            string reason;
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            if (CanSpawn(player, vehicle, bypassCooldown, command, out reason, ref position, ref rotation))
            {
                SpawnVehicle(player, vehicle, position, rotation);
                return false;
            }
            Print(player, reason);
            return true;
        }

        private bool CanSpawn(BasePlayer player, Vehicle vehicle, bool bypassCooldown, string command, out string reason, ref Vector3 position, ref Quaternion rotation)
        {
            var settings = GetBaseVehicleSettings(vehicle.VehicleType);
            BaseEntity randomVehicle = null;
            if (configData.global.limitVehicles > 0)
            {
                var activeVehicles = storedData.ActiveVehicles(player.userID);
                var count = activeVehicles.Count();
                if (count >= configData.global.limitVehicles)
                {
                    if (configData.global.killVehicleLimited)
                    {
                        randomVehicle = activeVehicles.ElementAt(Random.Range(0, count));
                    }
                    else
                    {
                        reason = Lang("VehiclesLimit", player.UserIDString, configData.global.limitVehicles);
                        return false;
                    }
                }
            }
            if (!CanPlayerAction(player, vehicle, settings, out reason, ref position, ref rotation))
            {
                return false;
            }
            var obj = Interface.CallHook("CanLicensedVehicleSpawn", player, vehicle.VehicleType, position, rotation);
            if (obj != null)
            {
                var s = obj as string;
                reason = s ?? Lang("SpawnWasBlocked", player.UserIDString, settings.DisplayName);
                return false;
            }

#if DEBUG
            if (player.IsAdmin)
            {
                reason = null;
                return true;
            }
#endif
            if (!CheckCooldown(player, vehicle, settings, bypassCooldown, true, command, out reason))
            {
                return false;
            }

            string resources;
            if (settings.SpawnPrices.Count > 0 && !TryPay(player, settings.SpawnPrices, out resources))
            {
                reason = Lang("NoResourcesToSpawnVehicle", player.UserIDString, settings.DisplayName, resources);
                return false;
            }

            if (randomVehicle != null)
            {
                randomVehicle.Kill(BaseNetworkable.DestroyMode.Gib);
            }
            reason = null;
            return true;
        }

        private void SpawnVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
        {
            var settings = GetBaseVehicleSettings(vehicle.VehicleType);
            var entity = settings.SpawnVehicle(player, vehicle, position, rotation);
            if (entity == null)
            {
                return;
            }
            Interface.CallHook("OnLicensedVehicleSpawned", entity, player, vehicle.VehicleType);
            Print(player, Lang("VehicleSpawned", player.UserIDString, settings.DisplayName));
        }

        private void CacheVehicleEntity(BaseEntity entity, Vehicle vehicle, BasePlayer player)
        {
            vehicle.PlayerId = player.userID;
            vehicle.VehicleType = vehicle.VehicleType;
            vehicle.Entity = entity;
            vehicle.EntityId = entity.net.ID.Value;
            vehicle.LastDismount = vehicle.LastRecall = TimeEx.currentTimestamp;
            vehiclesCache.Add(entity, vehicle);
        }

        #endregion Spawn Command

        #region Recall Command

        [ConsoleCommand("vl.recall")]
        private void CCmdRecallVehicle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                Print(arg, $"The server console cannot use the '{arg.cmd.FullName}' command");
            }
            else
            {
                CmdRecallVehicle(player, arg.cmd.FullName, arg.Args);
            }
        }

        private void CmdRecallVehicle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (args == null || args.Length < 1)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(Lang("Help", player.UserIDString));
                foreach (var entry in allVehicleSettings)
                {
                    if (CanViewVehicleInfo(player, entry.Key, entry.Value))
                    {
                        var firstCmd = entry.Value.Commands[0];
                        if (entry.Value.RecallPrices.Count > 0)
                        {
                            var prices = FormatPriceInfo(player, entry.Value.RecallPrices);
                            stringBuilder.AppendLine(Lang("HelpRecallPrice", player.UserIDString, configData.chat.recallCommand, firstCmd, entry.Value.DisplayName, prices));
                        }
                        else
                        {
                            stringBuilder.AppendLine(Lang("HelpRecall", player.UserIDString, configData.chat.recallCommand, firstCmd, entry.Value.DisplayName));
                        }
                    }
                }
                Print(player, stringBuilder.ToString());
                return;
            }
            string vehicleType;
            if (IsValidOption(player, args[0], out vehicleType))
            {
                var bypassCooldown = args.Length > 1 && IsValidBypassCooldownOption(args[1]);
                RecallVehicle(player, vehicleType, bypassCooldown, command + " " + args[0]);
            }
        }

        private bool RecallVehicle(BasePlayer player, string vehicleType, bool bypassCooldown, string command)
        {
            var settings = GetBaseVehicleSettings(vehicleType);
            Vehicle vehicle;
            if (!storedData.IsVehiclePurchased(player.userID, vehicleType, out vehicle))
            {
                Print(player, Lang("VehicleNotYetPurchased", player.UserIDString, settings.DisplayName, configData.chat.buyCommand));
                return false;
            }
            if (vehicle.Entity != null && !vehicle.Entity.IsDestroyed)
            {
                string reason;
                var position = Vector3.zero;
                var rotation = Quaternion.identity;
                if (CanRecall(player, vehicle, bypassCooldown, command, out reason, ref position, ref rotation))
                {
                    RecallVehicle(player, vehicle, position, rotation);
                    return true;
                }
                Print(player, reason);
                return false;
            }
            Print(player, Lang("VehicleNotOut", player.UserIDString, settings.DisplayName, configData.chat.spawnCommand));
            return false;
        }

        private bool CanRecall(BasePlayer player, Vehicle vehicle, bool bypassCooldown, string command, out string reason, ref Vector3 position, ref Quaternion rotation)
        {
            var settings = GetBaseVehicleSettings(vehicle.VehicleType);
            if (settings.RecallMaxDistance > 0 && Vector3.Distance(player.transform.position, vehicle.Entity.transform.position) > settings.RecallMaxDistance)
            {
                reason = Lang("RecallTooFar", player.UserIDString, settings.RecallMaxDistance, settings.DisplayName);
                return false;
            }
            if (configData.global.anyMountedRecall && VehicleAnyMounted(vehicle.Entity))
            {
                reason = Lang("PlayerMountedOnVehicle", player.UserIDString, settings.DisplayName);
                return false;
            }
            if (!CanPlayerAction(player, vehicle, settings, out reason, ref position, ref rotation))
            {
                return false;
            }

            var obj = Interface.CallHook("CanLicensedVehicleRecall", vehicle.Entity, player, vehicle.VehicleType, position, rotation);
            if (obj != null)
            {
                var s = obj as string;
                reason = s ?? Lang("RecallWasBlocked", player.UserIDString, settings.DisplayName);
                return false;
            }
#if DEBUG
            if (player.IsAdmin)
            {
                reason = null;
                return true;
            }
#endif
            if (!CheckCooldown(player, vehicle, settings, bypassCooldown, false, command, out reason))
            {
                return false;
            }
            string resources;
            if (settings.RecallPrices.Count > 0 && !TryPay(player, settings.RecallPrices, out resources))
            {
                reason = Lang("NoResourcesToRecallVehicle", player.UserIDString, settings.DisplayName, resources);
                return false;
            }
            reason = null;
            return true;
        }

        private void RecallVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
        {
            var settings = GetBaseVehicleSettings(vehicle.VehicleType);

            settings.PreRecallVehicle(player, vehicle, position, rotation);
            vehicle.Entity.transform.SetPositionAndRotation(position, rotation);
            vehicle.Entity.transform.hasChanged = true;
            settings.PostRecallVehicle(player, vehicle, position, rotation);

            vehicle.OnRecall();

            if (vehicle.Entity == null || vehicle.Entity.IsDestroyed)
            {
                Print(player, Lang("NotSpawnedOrRecalled", player.UserIDString, settings.DisplayName));
                return;
            }

            Interface.CallHook("OnLicensedVehicleRecalled", vehicle.Entity, player, vehicle.VehicleType);
            Print(player, Lang("VehicleRecalled", player.UserIDString, settings.DisplayName));
        }

        #endregion Recall Command

        #region Kill Command

        [ConsoleCommand("vl.kill")]
        private void CCmdKillVehicle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                Print(arg, $"The server console cannot use the '{arg.cmd.FullName}' command");
            }
            else
            {
                CmdKillVehicle(player, arg.cmd.FullName, arg.Args);
            }
        }

        private void CmdKillVehicle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (args == null || args.Length < 1)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(Lang("Help", player.UserIDString));
                foreach (var entry in allVehicleSettings)
                {
                    if (CanViewVehicleInfo(player, entry.Key, entry.Value))
                    {
                        var firstCmd = entry.Value.Commands[0];
                        if (!string.IsNullOrEmpty(configData.chat.customKillCommandPrefix))
                        {
                            stringBuilder.AppendLine(Lang("HelpKillCustom", player.UserIDString, configData.chat.killCommand, firstCmd, configData.chat.customKillCommandPrefix + firstCmd, entry.Value.DisplayName));
                        }
                        else
                        {
                            stringBuilder.AppendLine(Lang("HelpKill", player.UserIDString, configData.chat.killCommand, firstCmd, entry.Value.DisplayName));
                        }
                    }
                }
                Print(player, stringBuilder.ToString());
                return;
            }

            HandleKillCmd(player, args[0]);
        }

        private void HandleKillCmd(BasePlayer player, string option)
        {
            string vehicleType;
            if (IsValidOption(player, option, out vehicleType))
            {
                KillVehicle(player, vehicleType);
            }
        }

        private bool KillVehicle(BasePlayer player, string vehicleType)
        {
            var settings = GetBaseVehicleSettings(vehicleType);
            Vehicle vehicle;
            if (!storedData.IsVehiclePurchased(player.userID, vehicleType, out vehicle))
            {
                Print(player, Lang("VehicleNotYetPurchased", player.UserIDString, settings.DisplayName, configData.chat.buyCommand));
                return false;
            }
            if (vehicle.Entity != null && !vehicle.Entity.IsDestroyed)
            {
                if (!CanKill(player, vehicle, settings))
                {
                    return false;
                }
                vehicle.Entity.Kill(BaseNetworkable.DestroyMode.Gib);
                Print(player, Lang("VehicleKilled", player.UserIDString, settings.DisplayName));
                return true;
            }
            Print(player, Lang("VehicleNotOut", player.UserIDString, settings.DisplayName, configData.chat.spawnCommand));
            return false;
        }

        private bool CanKill(BasePlayer player, Vehicle vehicle, BaseVehicleSettings settings)
        {
            if (configData.global.anyMountedKill && VehicleAnyMounted(vehicle.Entity))
            {
                Print(player, Lang("PlayerMountedOnVehicle", player.UserIDString, settings.DisplayName));
                return false;
            }
            if (settings.KillMaxDistance > 0 && Vector3.Distance(player.transform.position, vehicle.Entity.transform.position) > settings.KillMaxDistance)
            {
                Print(player, Lang("KillTooFar", player.UserIDString, settings.KillMaxDistance, settings.DisplayName));
                return false;
            }

            return true;
        }

        #endregion Kill Command

        #region Command Helpers

        private bool IsValidBypassCooldownOption(string option)
        {
            return !string.IsNullOrEmpty(configData.chat.bypassCooldownCommand) &&
                    string.Equals(option, configData.chat.bypassCooldownCommand, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsValidOption(BasePlayer player, string option, out string vehicleType)
        {
            if (!commandToVehicleType.TryGetValue(option, out vehicleType))
            {
                Print(player, Lang("OptionNotFound", player.UserIDString, option));
                return false;
            }
            if (!HasVehiclePermission(player, vehicleType))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                vehicleType = null;
                return false;
            }
            if (IsPlayerBlocked(player))
            {
                vehicleType = null;
                return false;
            }
            return true;
        }

        private bool IsValidVehicleType(string option, out string vehicleType)
        {
            foreach (var entry in allVehicleSettings)
            {
                if (string.Equals(entry.Key, option, StringComparison.OrdinalIgnoreCase))
                {
                    vehicleType = entry.Key;
                    return true;
                }
            }

            vehicleType = null;
            return false;
        }

        private string FormatPriceInfo(BasePlayer player, Dictionary<string, PriceInfo> prices)
        {
            var language = RustTranslationAPI != null ? lang.GetLanguage(player.UserIDString) : null;
            return string.Join(", ", from p in prices
                               select Lang("PriceFormat", player.UserIDString, GetItemDisplayName(language, p.Key, p.Value.displayName), p.Value.amount));
        }

        private bool CanPlayerAction(BasePlayer player, Vehicle vehicle, BaseVehicleSettings settings, out string reason, ref Vector3 position, ref Quaternion rotation)
        {
            if (configData.global.preventBuildingBlocked && player.IsBuildingBlocked())
            {
                reason = Lang("BuildingBlocked", player.UserIDString, settings.DisplayName);
                return false;
            }
            if (configData.global.preventSafeZone && player.InSafeZone())
            {
                reason = Lang("PlayerInSafeZone", player.UserIDString, settings.DisplayName);
                return false;
            }
            if (configData.global.preventMountedOrParented && HasMountedOrParented(player, settings))
            {
                reason = Lang("MountedOrParented", player.UserIDString, settings.DisplayName);
                return false;
            }
            if (!settings.TryGetVehicleParams(player, vehicle, out reason, ref position, ref rotation))
            {
                return false;
            }
            reason = null;
            return true;
        }

        private bool HasMountedOrParented(BasePlayer player, BaseVehicleSettings settings)
        {
            if (player.GetMountedVehicle() != null)
            {
                return true;
            }
            var parentEntity = player.GetParentEntity();
            if (parentEntity != null)
            {
                if (configData.global.spawnLookingAt)
                {
                    if (LandOnCargoShip != null && parentEntity is CargoShip && settings.IsFightVehicle)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private bool CheckCooldown(BasePlayer player, Vehicle vehicle, BaseVehicleSettings settings, bool bypassCooldown, bool isSpawnCooldown, string command, out string reason)
        {
            var cooldown = settings.GetCooldown(player, isSpawnCooldown);
            if (cooldown > 0)
            {
                var timeLeft = Math.Ceiling(cooldown - (TimeEx.currentTimestamp - (isSpawnCooldown ? vehicle.LastDeath : vehicle.LastRecall)));
                if (timeLeft > 0)
                {
                    var bypassPrices = isSpawnCooldown ? settings.BypassSpawnCooldownPrices : settings.BypassRecallCooldownPrices;
                    if (bypassCooldown && bypassPrices.Count > 0)
                    {
                        string resources;
                        if (!TryPay(player, bypassPrices, out resources))
                        {
                            reason = Lang(isSpawnCooldown ? "NoResourcesToSpawnVehicleBypass" : "NoResourcesToRecallVehicleBypass", player.UserIDString, settings.DisplayName, resources);
                            return false;
                        }

                        if (isSpawnCooldown)
                        {
                            vehicle.LastDeath = 0;
                        }
                        else
                        {
                            vehicle.LastRecall = 0;
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(configData.chat.bypassCooldownCommand) || bypassPrices.Count <= 0)
                        {
                            reason = Lang(isSpawnCooldown ? "VehicleOnSpawnCooldown" : "VehicleOnRecallCooldown", player.UserIDString, timeLeft, settings.DisplayName);
                        }
                        else
                        {
                            reason = Lang(isSpawnCooldown ? "VehicleOnSpawnCooldownPay" : "VehicleOnRecallCooldownPay", player.UserIDString, timeLeft, settings.DisplayName,
                                          command + " " + configData.chat.bypassCooldownCommand,
                                          FormatPriceInfo(player, isSpawnCooldown ? settings.BypassSpawnCooldownPrices : settings.BypassRecallCooldownPrices));
                        }
                        return false;
                    }
                }
            }
            reason = null;
            return true;
        }

        #endregion Command Helpers

        #endregion Commands

        #region RustTranslationAPI

        private string GetItemTranslationByShortName(string language, string itemShortName)
        {
            return (string)RustTranslationAPI.Call("GetItemTranslationByShortName", language, itemShortName);
        }

        private string GetItemDisplayName(string language, string itemShortName, string displayName)
        {
            if (RustTranslationAPI != null)
            {
                var displayName1 = GetItemTranslationByShortName(language, itemShortName);
                if (!string.IsNullOrEmpty(displayName1))
                {
                    return displayName1;
                }
            }
            return displayName;
        }

        #endregion RustTranslationAPI

        #region ConfigurationFile

        public ConfigData configData { get; private set; }

        public class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public GlobalSettings global = new GlobalSettings();

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings chat = new ChatSettings();

            [JsonProperty(PropertyName = "Normal Vehicle Settings")]
            public NormalVehicleSettings normalVehicles = new NormalVehicleSettings();

            [JsonProperty(PropertyName = "Modular Vehicle Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, ModularVehicleSettings> modularVehicles = new Dictionary<string, ModularVehicleSettings>
            {
                ["SmallCar"] = new ModularVehicleSettings
                {
                    Purchasable = true,
                    DisplayName = "Small Modular Car",
                    Distance = 5,
                    MinDistanceForPlayers = 3,
                    UsePermission = true,
                    Permission = "vehiclelicence.smallmodularcar",
                    Commands = new List<string> { "small", "smallcar" },
                    PurchasePrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 1600, displayName = "Scrap" }
                    },
                    SpawnPrices = new Dictionary<string, PriceInfo>
                    {
                        ["metal.refined"] = new PriceInfo { amount = 10, displayName = "High Quality Metal" }
                    },
                    RecallPrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 5, displayName = "Scrap" }
                    },
                    SpawnCooldown = 7200,
                    RecallCooldown = 30,
                    CooldownPermissions = new Dictionary<string, CooldownPermission>
                    {
                        ["vehiclelicence.vip"] = new CooldownPermission
                        {
                            spawnCooldown = 3600,
                            recallCooldown = 10
                        }
                    },
                    ChassisType = ChassisType.Small,
                    ModuleItems = new List<ModuleItem>
                    {
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.cockpit.with.engine", healthPercentage = 50f
                        },
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.storage", healthPercentage = 50f
                        }
                    },
                    EngineItems = new List<EngineItem>
                    {
                        new EngineItem
                        {
                            shortName = "carburetor1", conditionPercentage = 20f
                        },
                        new EngineItem
                        {
                            shortName = "crankshaft1", conditionPercentage = 20f
                        },
                        new EngineItem
                        {
                            shortName = "piston1", conditionPercentage = 20f
                        },
                        new EngineItem
                        {
                            shortName = "sparkplug1", conditionPercentage = 20f
                        },
                        new EngineItem
                        {
                            shortName = "valve1", conditionPercentage = 20f
                        }
                    }
                },
                ["MediumCar"] = new ModularVehicleSettings
                {
                    Purchasable = true,
                    DisplayName = "Medium Modular Car",
                    Distance = 5,
                    MinDistanceForPlayers = 3,
                    UsePermission = true,
                    Permission = "vehiclelicence.mediumodularcar",
                    Commands = new List<string> { "medium", "mediumcar" },
                    PurchasePrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 2400, displayName = "Scrap" }
                    },
                    SpawnPrices = new Dictionary<string, PriceInfo>
                    {
                        ["metal.refined"] = new PriceInfo { amount = 50, displayName = "High Quality Metal" }
                    },
                    RecallPrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 8, displayName = "Scrap" }
                    },
                    SpawnCooldown = 9000,
                    RecallCooldown = 30,
                    CooldownPermissions = new Dictionary<string, CooldownPermission>
                    {
                        ["vehiclelicence.vip"] = new CooldownPermission
                        {
                            spawnCooldown = 4500,
                            recallCooldown = 10
                        }
                    },
                    ChassisType = ChassisType.Medium,
                    ModuleItems = new List<ModuleItem>
                    {
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.cockpit.with.engine", healthPercentage = 50f
                        },
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.rear.seats", healthPercentage = 50f
                        },
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.flatbed", healthPercentage = 50f
                        }
                    },
                    EngineItems = new List<EngineItem>
                    {
                        new EngineItem
                        {
                            shortName = "carburetor2", conditionPercentage = 20f
                        },
                        new EngineItem
                        {
                            shortName = "crankshaft2", conditionPercentage = 20f
                        },
                        new EngineItem
                        {
                            shortName = "piston2", conditionPercentage = 20f
                        },
                        new EngineItem
                        {
                            shortName = "sparkplug2", conditionPercentage = 20f
                        },
                        new EngineItem
                        {
                            shortName = "valve2", conditionPercentage = 20f
                        }
                    }
                },
                ["LargeCar"] = new ModularVehicleSettings
                {
                    Purchasable = true,
                    DisplayName = "Large Modular Car",
                    Distance = 6,
                    MinDistanceForPlayers = 3,
                    UsePermission = true,
                    Permission = "vehiclelicence.largemodularcar",
                    Commands = new List<string> { "large", "largecar" },
                    PurchasePrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 3000, displayName = "Scrap" }
                    },
                    SpawnPrices = new Dictionary<string, PriceInfo>
                    {
                        ["metal.refined"] = new PriceInfo { amount = 100, displayName = "High Quality Metal" }
                    },
                    RecallPrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 10, displayName = "Scrap" }
                    },
                    SpawnCooldown = 10800,
                    RecallCooldown = 30,
                    CooldownPermissions = new Dictionary<string, CooldownPermission>
                    {
                        ["vehiclelicence.vip"] = new CooldownPermission
                        {
                            spawnCooldown = 5400,
                            recallCooldown = 10
                        }
                    },
                    ChassisType = ChassisType.Large,
                    ModuleItems = new List<ModuleItem>
                    {
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.engine", healthPercentage = 50f
                        },
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.cockpit.armored", healthPercentage = 50f
                        },
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.passengers.armored", healthPercentage = 50f
                        },
                        new ModuleItem
                        {
                            shortName = "vehicle.1mod.storage", healthPercentage = 50f
                        }
                    },
                    EngineItems = new List<EngineItem>
                    {
                        new EngineItem
                        {
                            shortName = "carburetor3", conditionPercentage = 10f
                        },
                        new EngineItem
                        {
                            shortName = "crankshaft3", conditionPercentage = 10f
                        },
                        new EngineItem
                        {
                            shortName = "piston3", conditionPercentage = 10f
                        },
                        new EngineItem
                        {
                            shortName = "piston3", conditionPercentage = 10f
                        },
                        new EngineItem
                        {
                            shortName = "sparkplug3", conditionPercentage = 10f
                        },
                        new EngineItem
                        {
                            shortName = "sparkplug3", conditionPercentage = 10f
                        },
                        new EngineItem
                        {
                            shortName = "valve3", conditionPercentage = 10f
                        },
                        new EngineItem
                        {
                            shortName = "valve3", conditionPercentage = 10f
                        }
                    }
                }
            };

            [JsonProperty(PropertyName = "Train Vehicle Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, TrainVehicleSettings> trainVehicles = new Dictionary<string, TrainVehicleSettings>
            {
                ["WorkCartAboveGround"] = new TrainVehicleSettings
                {
                    Purchasable = true,
                    DisplayName = "Work Cart Above Ground",
                    Distance = 12,
                    MinDistanceForPlayers = 6,
                    UsePermission = true,
                    Permission = "vehiclelicence.workcartaboveground",
                    Commands = new List<string>
                    {
                        "cartground", "workcartground"
                    },
                    PurchasePrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 2000, displayName = "Scrap" }
                    },
                    SpawnCooldown = 1800,
                    RecallCooldown = 30,
                    CooldownPermissions = new Dictionary<string, CooldownPermission>
                    {
                        ["vehiclelicence.vip"] = new CooldownPermission
                        {
                            spawnCooldown = 900,
                            recallCooldown = 10
                        }
                    },
                    TrainComponents = new List<TrainComponent>
                    {
                        new TrainComponent { type = TrainComponentType.Engine }
                    }
                },
                ["WorkCartCovered"] = new TrainVehicleSettings
                {
                    Purchasable = true,
                    DisplayName = "Covered Work Cart",
                    Distance = 12,
                    MinDistanceForPlayers = 6,
                    UsePermission = true,
                    Permission = "vehiclelicence.coveredworkcart",
                    Commands = new List<string>
                    {
                        "cartcovered", "coveredworkcart"
                    },
                    PurchasePrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 2000, displayName = "Scrap" }
                    },
                    SpawnCooldown = 1800,
                    RecallCooldown = 30,
                    CooldownPermissions = new Dictionary<string, CooldownPermission>
                    {
                        ["vehiclelicence.vip"] = new CooldownPermission
                        {
                            spawnCooldown = 900,
                            recallCooldown = 10
                        }
                    },
                    TrainComponents = new List<TrainComponent>
                    {
                        new TrainComponent { type = TrainComponentType.CoveredEngine }
                    }
                },
                ["CompleteTrain"] = new TrainVehicleSettings
                {
                    Purchasable = true,
                    DisplayName = "Complete Train",
                    Distance = 12,
                    MinDistanceForPlayers = 6,
                    UsePermission = true,
                    Permission = "vehiclelicence.completetrain",
                    Commands = new List<string>
                    {
                        "ctrain", "completetrain"
                    },
                    PurchasePrices = new Dictionary<string, PriceInfo>
                    {
                        ["scrap"] = new PriceInfo { amount = 6000, displayName = "Scrap" }
                    },
                    SpawnCooldown = 3600,
                    RecallCooldown = 60,
                    CooldownPermissions = new Dictionary<string, CooldownPermission>
                    {
                        ["vehiclelicence.vip"] = new CooldownPermission
                        {
                            spawnCooldown = 900,
                            recallCooldown = 10
                        }
                    },
                    TrainComponents = new List<TrainComponent>
                    {
                        new TrainComponent
                        {
                            type = TrainComponentType.Engine
                        },
                        new TrainComponent
                        {
                            type = TrainComponentType.WagonA
                        },
                        new TrainComponent
                        {
                            type = TrainComponentType.WagonB
                        },
                        new TrainComponent
                        {
                            type = TrainComponentType.WagonC
                        },
                        new TrainComponent
                        {
                            type = TrainComponentType.Unloadable
                        },
                        new TrainComponent
                        {
                            type = TrainComponentType.UnloadableLoot
                        }
                    }
                }
            };

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber version;
        }

        public class ChatSettings
        {
            [JsonProperty(PropertyName = "Use Universal Chat Command")]
            public bool useUniversalCommand = true;

            [JsonProperty(PropertyName = "Help Chat Command")]
            public string helpCommand = "license";

            [JsonProperty(PropertyName = "Buy Chat Command")]
            public string buyCommand = "buy";

            [JsonProperty(PropertyName = "Spawn Chat Command")]
            public string spawnCommand = "spawn";

            [JsonProperty(PropertyName = "Recall Chat Command")]
            public string recallCommand = "recall";

            [JsonProperty(PropertyName = "Kill Chat Command")]
            public string killCommand = "kill";

            [JsonProperty(PropertyName = "Custom Kill Chat Command Prefix")]
            public string customKillCommandPrefix = "no";

            [JsonProperty(PropertyName = "Bypass Cooldown Command")]
            public string bypassCooldownCommand = "pay";

            [JsonProperty(PropertyName = "Chat Prefix")]
            public string prefix = "<color=#00FFFF>[VehicleLicense]</color>: ";

            [JsonProperty(PropertyName = "Chat SteamID Icon")]
            public ulong steamIDIcon = 76561198924840872;
        }

        public class GlobalSettings
        {
            [JsonProperty(PropertyName = "Store Vehicle On Plugin Unloaded / Server Restart")]
            public bool storeVehicle = true;

            [JsonProperty(PropertyName = "Clear Vehicle Data On Map Wipe")]
            public bool clearVehicleOnWipe;

            [JsonProperty(PropertyName = "Interval to check vehicle for wipe (Seconds)")]
            public float checkVehiclesInterval = 300;

            [JsonProperty(PropertyName = "Spawn vehicle in the direction you are looking at")]
            public bool spawnLookingAt = true;

            [JsonProperty(PropertyName = "Automatically claim vehicles purchased from vehicle vendors")]
            public bool autoClaimFromVendor;

            [JsonProperty(PropertyName = "Vehicle vendor purchases will unlock the license for the player")]
            public bool autoUnlockFromVendor;

            [JsonProperty(PropertyName = "Limit the number of vehicles at a time")]
            public int limitVehicles;

            [JsonProperty(PropertyName = "Kill a random vehicle when the number of vehicles is limited")]
            public bool killVehicleLimited;

            [JsonProperty(PropertyName = "Prevent vehicles from damaging players")]
            public bool preventDamagePlayer = true;

            [JsonProperty(PropertyName = "Prevent vehicles from shattering")]
            public bool preventShattering = true;

            [JsonProperty(PropertyName = "Prevent vehicles from spawning or recalling in safe zone")]
            public bool preventSafeZone = true;

            [JsonProperty(PropertyName = "Prevent vehicles from spawning or recalling when the player are building blocked")]
            public bool preventBuildingBlocked = true;

            [JsonProperty(PropertyName = "Prevent vehicles from spawning or recalling when the player is mounted or parented")]
            public bool preventMountedOrParented = true;

            [JsonProperty(PropertyName = "Check if any player mounted when recalling a vehicle")]
            public bool anyMountedRecall = true;

            [JsonProperty(PropertyName = "Check if any player mounted when killing a vehicle")]
            public bool anyMountedKill;

            [JsonProperty(PropertyName = "Dismount all players when a vehicle is recalled")]
            public bool dismountAllPlayersRecall = true;

            [JsonProperty(PropertyName = "Prevent other players from mounting vehicle")]
            public bool preventMounting = true;

            [JsonProperty(PropertyName = "Prevent mounting on driver's seat only")]
            public bool preventDriverSeat = true;

            [JsonProperty(PropertyName = "Prevent other players from looting fuel container and inventory")]
            public bool preventLooting = true;

            [JsonProperty(PropertyName = "Use Teams")]
            public bool useTeams;

            [JsonProperty(PropertyName = "Use Clans")]
            public bool useClans = true;

            [JsonProperty(PropertyName = "Use Friends")]
            public bool useFriends = true;

            [JsonProperty(PropertyName = "Vehicle No Decay")]
            public bool noDecay;

            [JsonProperty(PropertyName = "Vehicle No Fire Ball")]
            public bool noFireBall = true;

            [JsonProperty(PropertyName = "Vehicle No Server Gibs")]
            public bool noServerGibs = true;

            [JsonProperty(PropertyName = "Chinook No Map Marker")]
            public bool noMapMarker = true;

            [JsonProperty(PropertyName = "Use Raid Blocker (Need NoEscape Plugin)")]
            public bool useRaidBlocker;

            [JsonProperty(PropertyName = "Use Combat Blocker (Need NoEscape Plugin)")]
            public bool useCombatBlocker;
        }

        public class NormalVehicleSettings
        {
            [JsonProperty(PropertyName = "Sedan Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SedanSettings sedan = new SedanSettings
            {
                Purchasable = true,
                DisplayName = "Sedan",
                Distance = 5,
                MinDistanceForPlayers = 3,
                UsePermission = true,
                Permission = "vehiclelicence.sedan",
                Commands = new List<string> { "car", "sedan" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 300, displayName = "Scrap" }
                },
                SpawnCooldown = 300,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 150,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Chinook Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public ChinookSettings chinook = new ChinookSettings
            {
                Purchasable = true,
                DisplayName = "Chinook",
                Distance = 15,
                MinDistanceForPlayers = 6,
                UsePermission = true,
                Permission = "vehiclelicence.chinook",
                Commands = new List<string> { "ch47", "chinook" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 3000, displayName = "Scrap" }
                },
                SpawnCooldown = 3000,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 1500,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Rowboat Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public RowboatSettings rowboat = new RowboatSettings
            {
                Purchasable = true,
                DisplayName = "Row Boat",
                Distance = 5,
                MinDistanceForPlayers = 2,
                UsePermission = true,
                Permission = "vehiclelicence.rowboat",
                Commands = new List<string> { "row", "rowboat" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 500, displayName = "Scrap" }
                },
                SpawnCooldown = 300,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 150,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "RHIB Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public RhibSettings rhib = new RhibSettings
            {
                Purchasable = true,
                DisplayName = "Rigid Hulled Inflatable Boat",
                Distance = 10,
                MinDistanceForPlayers = 3,
                UsePermission = true,
                Permission = "vehiclelicence.rhib",
                Commands = new List<string> { "rhib" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 1000, displayName = "Scrap" }
                },
                SpawnCooldown = 450,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 225,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Hot Air Balloon Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public HotAirBalloonSettings hotAirBalloon = new HotAirBalloonSettings
            {
                Purchasable = true,
                DisplayName = "Hot Air Balloon",
                Distance = 20,
                MinDistanceForPlayers = 5,
                UsePermission = true,
                Permission = "vehiclelicence.hotairballoon",
                Commands = new List<string> { "hab", "hotairballoon" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 500, displayName = "Scrap" }
                },
                SpawnCooldown = 900,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 450,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Ridable Horse Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public RidableHorseSettings ridableHorse = new RidableHorseSettings
            {
                Purchasable = true,
                DisplayName = "Ridable Horse",
                Distance = 6,
                MinDistanceForPlayers = 1,
                UsePermission = true,
                Permission = "vehiclelicence.ridablehorse",
                Commands = new List<string> { "horse", "ridablehorse" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 700, displayName = "Scrap" }
                },
                SpawnCooldown = 3000,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 1500,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Mini Copter Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public MiniCopterSettings miniCopter = new MiniCopterSettings
            {
                Purchasable = true,
                DisplayName = "Mini Copter",
                Distance = 8,
                MinDistanceForPlayers = 2,
                UsePermission = true,
                Permission = "vehiclelicence.minicopter",
                Commands = new List<string> { "mini", "minicopter" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 4000, displayName = "Scrap" }
                },
                SpawnCooldown = 1800,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 900,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Transport Helicopter Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public TransportHelicopterSettings transportHelicopter = new TransportHelicopterSettings
            {
                Purchasable = true,
                DisplayName = "Transport Copter",
                Distance = 10,
                MinDistanceForPlayers = 4,
                UsePermission = true,
                Permission = "vehiclelicence.transportcopter",
                Commands = new List<string>
                {
                    "tcop", "transportcopter"
                },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 5000, displayName = "Scrap" }
                },
                SpawnCooldown = 2400,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 1200,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Work Cart Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public WorkCartSettings workCart = new WorkCartSettings
            {
                Purchasable = true,
                DisplayName = "Work Cart",
                Distance = 12,
                MinDistanceForPlayers = 6,
                UsePermission = true,
                Permission = "vehiclelicence.workcart",
                Commands = new List<string>
                {
                    "cart", "workcart"
                },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 2000, displayName = "Scrap" }
                },
                SpawnCooldown = 1800,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 900,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Sedan Rail Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public WorkCartSettings sedanRail = new WorkCartSettings
            {
                Purchasable = true,
                DisplayName = "Sedan Rail",
                Distance = 6,
                MinDistanceForPlayers = 3,
                UsePermission = true,
                Permission = "vehiclelicence.sedanrail",
                Commands = new List<string>
                {
                    "carrail", "sedanrail"
                },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 500, displayName = "Scrap" }
                },
                SpawnCooldown = 600,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 300,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Magnet Crane Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public MagnetCraneSettings magnetCrane = new MagnetCraneSettings
            {
                Purchasable = true,
                DisplayName = "Magnet Crane",
                Distance = 16,
                MinDistanceForPlayers = 8,
                UsePermission = true,
                Permission = "vehiclelicence.magnetcrane",
                Commands = new List<string>
                {
                    "crane", "magnetcrane"
                },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 2000, displayName = "Scrap" }
                },
                SpawnCooldown = 600,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 300,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Submarine Solo Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SubmarineSoloSettings submarineSolo = new SubmarineSoloSettings
            {
                Purchasable = true,
                DisplayName = "Submarine Solo",
                Distance = 5,
                MinDistanceForPlayers = 2,
                UsePermission = true,
                Permission = "vehiclelicence.submarinesolo",
                Commands = new List<string> { "subsolo", "solo" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 600, displayName = "Scrap" }
                },
                SpawnCooldown = 300,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 150,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Submarine Duo Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SubmarineDuoSettings submarineDuo = new SubmarineDuoSettings
            {
                Purchasable = true,
                DisplayName = "Submarine Duo",
                Distance = 5,
                MinDistanceForPlayers = 2,
                UsePermission = true,
                Permission = "vehiclelicence.submarineduo",
                Commands = new List<string> { "subduo", "duo" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 1000, displayName = "Scrap" }
                },
                SpawnCooldown = 300,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 150,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Snowmobile Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SnowmobileSettings snowmobile = new SnowmobileSettings
            {
                Purchasable = true,
                DisplayName = "Snowmobile",
                Distance = 5,
                MinDistanceForPlayers = 2,
                UsePermission = true,
                Permission = "vehiclelicence.snowmobile",
                Commands = new List<string> { "snow", "snowmobile" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 1000, displayName = "Scrap" }
                },
                SpawnCooldown = 300,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 150,
                        recallCooldown = 10
                    }
                }
            };

            [JsonProperty(PropertyName = "Tomaha Snowmobile Vehicle", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SnowmobileSettings tomahaSnowmobile = new SnowmobileSettings
            {
                Purchasable = true,
                DisplayName = "Tomaha Snowmobile",
                Distance = 5,
                MinDistanceForPlayers = 2,
                UsePermission = true,
                Permission = "vehiclelicence.tomahasnowmobile",
                Commands = new List<string> { "tsnow", "tsnowmobile" },
                PurchasePrices = new Dictionary<string, PriceInfo>
                {
                    ["scrap"] = new PriceInfo { amount = 1000, displayName = "Scrap" }
                },
                SpawnCooldown = 300,
                RecallCooldown = 30,
                CooldownPermissions = new Dictionary<string, CooldownPermission>
                {
                    ["vehiclelicence.vip"] = new CooldownPermission
                    {
                        spawnCooldown = 150,
                        recallCooldown = 10
                    }
                }
            };
        }

        #region BaseSettings

        [JsonObject(MemberSerialization.OptIn)]
        public abstract class BaseVehicleSettings
        {
            #region Properties

            [JsonProperty(PropertyName = "Purchasable")]
            public bool Purchasable { get; set; }

            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName { get; set; }

            [JsonProperty(PropertyName = "Use Permission")]
            public bool UsePermission { get; set; }

            [JsonProperty(PropertyName = "Permission")]
            public string Permission { get; set; }

            [JsonProperty(PropertyName = "Distance To Spawn")]
            public float Distance { get; set; }

            [JsonProperty(PropertyName = "Time Before Vehicle Wipe (Seconds)")]
            public double WipeTime { get; set; }

            [JsonProperty(PropertyName = "Exclude cupboard zones when wiping")]
            public bool ExcludeCupboard { get; set; }

            [JsonProperty(PropertyName = "Maximum Health")]
            public float MaxHealth { get; set; }

            [JsonProperty(PropertyName = "Can Recall Maximum Distance")]
            public float RecallMaxDistance { get; set; }

            [JsonProperty(PropertyName = "Can Kill Maximum Distance")]
            public float KillMaxDistance { get; set; }

            [JsonProperty(PropertyName = "Minimum distance from player to recall or spawn")]
            public float MinDistanceForPlayers { get; set; } = 3f;

            [JsonProperty(PropertyName = "Remove License Once Crashed")]
            public bool RemoveLicenseOnceCrash { get; set; }

            [JsonProperty(PropertyName = "Commands")]
            public List<string> Commands { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Purchase Prices")]
            public Dictionary<string, PriceInfo> PurchasePrices { get; set; } = new Dictionary<string, PriceInfo>();

            [JsonProperty(PropertyName = "Spawn Prices")]
            public Dictionary<string, PriceInfo> SpawnPrices { get; set; } = new Dictionary<string, PriceInfo>();

            [JsonProperty(PropertyName = "Recall Prices")]
            public Dictionary<string, PriceInfo> RecallPrices { get; set; } = new Dictionary<string, PriceInfo>();

            [JsonProperty(PropertyName = "Recall Cooldown Bypass Prices")]
            public Dictionary<string, PriceInfo> BypassRecallCooldownPrices { get; set; } = new Dictionary<string, PriceInfo>();

            [JsonProperty(PropertyName = "Spawn Cooldown Bypass Prices")]
            public Dictionary<string, PriceInfo> BypassSpawnCooldownPrices { get; set; } = new Dictionary<string, PriceInfo>();

            [JsonProperty(PropertyName = "Spawn Cooldown (Seconds)")]
            public double SpawnCooldown { get; set; }

            [JsonProperty(PropertyName = "Recall Cooldown (Seconds)")]
            public double RecallCooldown { get; set; }

            [JsonProperty(PropertyName = "Cooldown Permissions")]
            public Dictionary<string, CooldownPermission> CooldownPermissions { get; set; } = new Dictionary<string, CooldownPermission>();

            #endregion Properties

            protected ConfigData configData => Instance.configData;

            public virtual bool IsWaterVehicle => false;
            public virtual bool IsTrainVehicle => false;
            public virtual bool IsNormalVehicle => true;
            public virtual bool IsFightVehicle => false;
            public virtual bool IsModularVehicle => false;
            public virtual bool IsConnectableVehicle => false;

            protected virtual EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return null;
            }

            protected virtual IEnumerable<ItemContainer> GetInventories(BaseEntity entity)
            {
                yield break;
            }

            #region Spawn

            protected virtual string GetVehiclePrefab(string vehicleType)
            {
                NormalVehicleType normalVehicleType;
                if (Enum.TryParse(vehicleType, out normalVehicleType) && Enum.IsDefined(typeof(NormalVehicleType), normalVehicleType))
                {
                    switch (normalVehicleType)
                    {
                        case NormalVehicleType.Rowboat:
                            return PREFAB_ROWBOAT;
                        case NormalVehicleType.RHIB:
                            return PREFAB_RHIB;
                        case NormalVehicleType.Sedan:
                            return PREFAB_SEDAN;
                        case NormalVehicleType.HotAirBalloon:
                            return PREFAB_HOTAIRBALLOON;
                        case NormalVehicleType.MiniCopter:
                            return PREFAB_MINICOPTER;
                        case NormalVehicleType.TransportHelicopter:
                            return PREFAB_TRANSPORTCOPTER;
                        case NormalVehicleType.Chinook:
                            return PREFAB_CHINOOK;
                        case NormalVehicleType.RidableHorse:
                            return PREFAB_RIDABLEHORSE;
                        case NormalVehicleType.WorkCart:
                            return PREFAB_WORKCART;
                        case NormalVehicleType.SedanRail:
                            return PREFAB_SEDANRAIL;
                        case NormalVehicleType.MagnetCrane:
                            return PREFAB_MAGNET_CRANE;
                        case NormalVehicleType.SubmarineSolo:
                            return PREFAB_SUBMARINE_SOLO;
                        case NormalVehicleType.SubmarineDuo:
                            return PREFAB_SUBMARINE_DUO;
                        case NormalVehicleType.Snowmobile:
                            return PREFAB_SNOWMOBILE;
                        case NormalVehicleType.TomahaSnowmobile:
                            return PREFAB_SNOWMOBILE_TOMAHA;
                        default:
                            return null;
                    }
                }
                return null;
            }

            public virtual BaseEntity SpawnVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                var prefab = GetVehiclePrefab(vehicle.VehicleType);
                if (string.IsNullOrEmpty(prefab))
                {
                    throw new ArgumentException($"Prefab not found for {vehicle.VehicleType}");
                }
                var entity = GameManager.server.CreateEntity(prefab, position, rotation);
                if (entity == null)
                {
                    return null;
                }
                PreSetupVehicle(entity, vehicle, player);
                entity.Spawn();
                SetupVehicle(entity, vehicle, player);
                if (!entity.IsDestroyed)
                {
                    Instance.CacheVehicleEntity(entity, vehicle, player);
                }
                else
                {
                    Instance.Print(player, Instance.Lang("NotSpawnedOrRecalled", player.UserIDString, DisplayName));
                    return null;
                }

                return entity;
            }

            #region Setup

            public virtual void PreSetupVehicle(BaseEntity entity, Vehicle vehicle, BasePlayer player)
            {
                entity.enableSaving = configData.global.storeVehicle;
                entity.OwnerID = player.userID;
            }

            public virtual void SetupVehicle(BaseEntity entity, Vehicle vehicle, BasePlayer player, bool justCreated = true)
            {
                if (MaxHealth > 0 && Math.Abs(MaxHealth - entity.MaxHealth()) > 0f)
                {
                    (entity as BaseCombatEntity)?.InitializeHealth(MaxHealth, MaxHealth);
                }
                var helicopterVehicle = entity as BaseHelicopterVehicle;
                if (helicopterVehicle != null)
                {
                    if (configData.global.noServerGibs)
                    {
                        helicopterVehicle.serverGibs.guid = string.Empty;
                    }
                    if (configData.global.noFireBall)
                    {
                        helicopterVehicle.fireBall.guid = string.Empty;
                    }
                    if (configData.global.noMapMarker)
                    {
                        var ch47Helicopter = entity as CH47Helicopter;
                        if (ch47Helicopter != null)
                        {
                            if (ch47Helicopter.mapMarkerInstance)
                            {
                                ch47Helicopter.mapMarkerInstance.Kill();
                            }
                            ch47Helicopter.mapMarkerEntityPrefab.guid = string.Empty;
                        }
                    }
                }

                if (configData.global.preventShattering)
                {
                    var magnetLiftable = entity.GetComponent<MagnetLiftable>();
                    if (magnetLiftable != null)
                    {
                        UnityEngine.Object.Destroy(magnetLiftable);
                    }
                }
            }

            #endregion Setup

            #endregion Spawn

            #region Recall

            public virtual void PreRecallVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                if (configData.global.dismountAllPlayersRecall)
                {
                    DismountAllPlayers(vehicle.Entity);
                }

                if (CanDropInventory())
                {
                    TryDropVehicleInventory(player, vehicle);
                }

                if (vehicle.Entity.HasParent())
                {
                    vehicle.Entity.SetParent(null, true, true);
                }
            }

            public virtual void PostRecallVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
            }

            #region DropInventory

            protected virtual bool CanDropInventory()
            {
                return false;
            }

            private void TryDropVehicleInventory(BasePlayer player, Vehicle vehicle)
            {
                var droppedItemContainer = DropVehicleInventory(player, vehicle);
                if (droppedItemContainer != null)
                {
                    Instance.Print(player, Instance.Lang("VehicleInventoryDropped", player.UserIDString, DisplayName));
                }
            }

            protected virtual DroppedItemContainer DropVehicleInventory(BasePlayer player, Vehicle vehicle)
            {
                var inventories = GetInventories(vehicle.Entity);
                foreach (var inventory in inventories)
                {
                    if (inventory != null)
                    {
                        return inventory.Drop(PREFAB_ITEM_DROP, vehicle.Entity.GetDropPosition(), vehicle.Entity.transform.rotation, 0);
                    }
                }
                return null;
            }

            #endregion DropInventory

            #region Train Car

            protected bool TryGetTrainCarPositionAndRotation(BasePlayer player, Vehicle vehicle, ref string reason, ref Vector3 original, ref Quaternion rotation)
            {
                float distResult;
                TrainTrackSpline splineResult;
                if (!TrainTrackSpline.TryFindTrackNear(original, Distance, out splineResult, out distResult))
                {
                    reason = Instance.Lang("TooFarTrainTrack", player.UserIDString);
                    return false;
                }

                var position = splineResult.GetPosition(distResult);
                if (!SpaceIsClearForTrainTrack(vehicle, position, rotation))
                {
                    reason = Instance.Lang("TooCloseTrainBarricadeOrWorkCart", player.UserIDString);
                    return false;
                }

                original = position;
                reason = null;
                return true;
            }

            protected bool TryMoveToTrainTrackNear(TrainCar trainCar)
            {
                float distResult;
                TrainTrackSpline splineResult;
                if (TrainTrackSpline.TryFindTrackNear(trainCar.GetFrontWheelPos(), 2f, out splineResult, out distResult))
                {
                    trainCar.FrontWheelSplineDist = distResult;
                    Vector3 tangent;
                    var positionAndTangent = splineResult.GetPositionAndTangent(trainCar.FrontWheelSplineDist, trainCar.transform.forward, out tangent);
                    trainCar.SetTheRestFromFrontWheelData(ref splineResult, positionAndTangent, tangent, trainCar.localTrackSelection, null, true);
                    trainCar.FrontTrackSection = splineResult;
                    if (trainCar.SpaceIsClear())
                    {
                        return true;
                    }
                }
                return false;
            }

            protected bool SpaceIsClearForTrainTrack(Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                var colliders = Pool.GetList<Collider>();
                if (vehicle.Entity == null)
                {
                    var prefab = GetVehiclePrefab(vehicle.VehicleType);
                    if (!string.IsNullOrEmpty(prefab))
                    {
                        var trainEngine = GameManager.server.FindPrefab(prefab)?.GetComponent<TrainEngine>();
                        if (trainEngine != null)
                        {
                            GamePhysics.OverlapOBB(new OBB(position, trainEngine.transform.lossyScale, rotation, trainEngine.bounds), colliders, Layers.Mask.Vehicle_World);
                        }
                    }
                }
                else
                {
                    GamePhysics.OverlapOBB(new OBB(position, vehicle.Entity.transform.lossyScale, rotation, vehicle.Entity.bounds), colliders, Layers.Mask.Vehicle_World);
                }
                var free = true;
                foreach (var item in colliders)
                {
                    var baseEntity = item.ToBaseEntity();
                    if (baseEntity == vehicle.Entity)
                    {
                        continue;
                    }
                    free = false;
                    break;
                }
                Pool.FreeList(ref colliders);
                return free;
            }

            #endregion

            #endregion Recall

            #region Refund

            protected virtual bool CanRefundFuel(bool isCrash, bool isUnload)
            {
                return false;
            }

            protected virtual bool CanRefundInventory(bool isCrash, bool isUnload)
            {
                return false;
            }

            protected virtual void CollectVehicleItems(List<Item> items, Vehicle vehicle, bool isCrash, bool isUnload)
            {
                if (CanRefundFuel(isCrash, isUnload))
                {
                    var fuelSystem = GetFuelSystem(vehicle.Entity);
                    if (fuelSystem != null)
                    {
                        var fuelContainer = fuelSystem.GetFuelContainer();
                        if (fuelContainer != null && fuelContainer.inventory != null)
                        {
                            items.AddRange(fuelContainer.inventory.itemList);
                        }
                    }
                }
                if (CanRefundInventory(isCrash, isUnload))
                {
                    var inventories = GetInventories(vehicle.Entity);
                    foreach (var inventory in inventories)
                    {
                        items.AddRange(inventory.itemList);
                    }
                }
            }

            public void RefundVehicleItems(Vehicle vehicle, bool isCrash, bool isUnload)
            {
                var collect = Pool.GetList<Item>();

                CollectVehicleItems(collect, vehicle, isCrash, isUnload);

                if (collect.Count > 0)
                {
                    var player = RustCore.FindPlayerById(vehicle.PlayerId);
                    if (player == null)
                    {
                        DropItemContainer(vehicle.Entity, vehicle.PlayerId, collect);
                    }
                    else
                    {
                        for (var i = collect.Count - 1; i >= 0; i--)
                        {
                            var item = collect[i];
                            player.GiveItem(item);
                        }

                        if (player.IsConnected)
                        {
                            Instance.Print(player, Instance.Lang("RefundedVehicleItems", player.UserIDString, DisplayName));
                        }
                    }
                }
                Pool.FreeList(ref collect);
            }

            #endregion Refund

            #region GiveFuel

            protected void TryGiveFuel(BaseEntity entity, IFuelVehicle iFuelVehicle)
            {
                if (iFuelVehicle == null || iFuelVehicle.SpawnFuelAmount <= 0)
                {
                    return;
                }
                var fuelSystem = GetFuelSystem(entity);
                if (fuelSystem != null)
                {
                    var fuelContainer = fuelSystem.GetFuelContainer();
                    if (fuelContainer != null && fuelContainer.inventory != null)
                    {
                        var fuelItem = ItemManager.CreateByItemID(ITEMID_FUEL, iFuelVehicle.SpawnFuelAmount);
                        if (!fuelItem.MoveToContainer(fuelContainer.inventory))
                        {
                            fuelItem.Remove();
                        }
                    }
                }
            }

            #endregion GiveFuel

            #region Permission

            public double GetCooldown(BasePlayer player, bool isSpawn)
            {
                var cooldown = isSpawn ? SpawnCooldown : RecallCooldown;
                foreach (var entry in CooldownPermissions)
                {
                    var currentCooldown = isSpawn ? entry.Value.spawnCooldown : entry.Value.recallCooldown;
                    if (cooldown > currentCooldown && Instance.permission.UserHasPermission(player.UserIDString, entry.Key))
                    {
                        cooldown = currentCooldown;
                    }
                }
                return cooldown;
            }

            #endregion Permission

            #region TryGetVehicleParams

            public virtual bool TryGetVehicleParams(BasePlayer player, Vehicle vehicle, out string reason, ref Vector3 spawnPos, ref Quaternion spawnRot)
            {
                Vector3 original;
                Quaternion rotation;
                if (!TryGetPositionAndRotation(player, vehicle, out reason, out original, out rotation))
                {
                    return false;
                }

                CorrectPositionAndRotation(player, vehicle, original, rotation, out spawnPos, out spawnRot);
                return true;
            }

            protected virtual float GetSpawnRotationAngle()
            {
                return 90f;
            }

            protected virtual Vector3 GetOriginalPosition(BasePlayer player)
            {
                if (configData.global.spawnLookingAt || IsWaterVehicle || IsTrainVehicle)
                {
                    return GetGroundPositionLookingAt(player, Distance, !IsTrainVehicle);
                }

                return player.transform.position;
            }

            protected virtual bool TryGetPositionAndRotation(BasePlayer player, Vehicle vehicle, out string reason, out Vector3 original, out Quaternion rotation)
            {
                original = GetOriginalPosition(player);
                rotation = Quaternion.identity;
                if (MinDistanceForPlayers > 0)
                {
                    var nearbyPlayers = Pool.GetList<BasePlayer>();
                    Vis.Entities(original, MinDistanceForPlayers, nearbyPlayers, Layers.Mask.Player_Server);
                    var flag = nearbyPlayers.Any(x => x.userID.IsSteamId() && x != player);
                    Pool.FreeList(ref nearbyPlayers);
                    if (flag)
                    {
                        reason = Instance.Lang("PlayersOnNearby", player.UserIDString, DisplayName);
                        return false;
                    }
                }
                if (IsWaterVehicle && !IsInWater(original))
                {
                    reason = Instance.Lang("NotLookingAtWater", player.UserIDString, DisplayName);
                    return false;
                }
                reason = null;
                return true;
            }

            protected virtual void CorrectPositionAndRotation(BasePlayer player, Vehicle vehicle, Vector3 original, Quaternion rotation, out Vector3 spawnPos, out Quaternion spawnRot)
            {
                spawnPos = original;
                if (IsTrainVehicle)
                {
                    var forward = player.eyes.HeadForward().WithY(0);
                    spawnRot = forward != Vector3.zero ? Quaternion.LookRotation(forward) : Quaternion.identity;
                    return;
                }
                if (configData.global.spawnLookingAt)
                {
                    var needGetGround = true;
                    if (IsWaterVehicle)
                    {
                        RaycastHit hit;
                        if (Physics.Raycast(spawnPos, Vector3.up, out hit, 100, LAYER_GROUND) && hit.GetEntity() is StabilityEntity)
                        {
                            needGetGround = false; //At the dock
                        }
                    }
                    else
                    {
                        if (TryGetCenterOfFloorNearby(ref spawnPos))
                        {
                            needGetGround = false; //At the floor
                        }
                    }
                    if (needGetGround)
                    {
                        spawnPos = GetGroundPosition(spawnPos);
                    }
                }
                else
                {
                    GetPositionWithNoPlayersNearby(player, ref spawnPos);
                }

                var normalized = (spawnPos - player.transform.position).normalized;
                var angle = normalized != Vector3.zero ? Quaternion.LookRotation(normalized).eulerAngles.y : Random.Range(0f, 360f);
                var rotationAngle = GetSpawnRotationAngle();
                spawnRot = Quaternion.Euler(Vector3.up * (angle + rotationAngle));
            }

            private void GetPositionWithNoPlayersNearby(BasePlayer player, ref Vector3 spawnPos)
            {
                var minDistance = Mathf.Min(MinDistanceForPlayers, 2.5f);
                var maxDistance = Mathf.Max(Distance, minDistance);

                var players = new BasePlayer[1];
                var sourcePos = spawnPos;
                for (var i = 0; i < 10; i++)
                {
                    spawnPos.x = sourcePos.x + Random.Range(minDistance, maxDistance) * (Random.value >= 0.5f ? 1 : -1);
                    spawnPos.z = sourcePos.z + Random.Range(minDistance, maxDistance) * (Random.value >= 0.5f ? 1 : -1);

                    if (BaseEntity.Query.Server.GetPlayersInSphere(spawnPos, minDistance, players, p => p.userID.IsSteamId() && p != player) == 0)
                    {
                        break;
                    }
                }
                spawnPos = GetGroundPosition(spawnPos);
            }

            private bool TryGetCenterOfFloorNearby(ref Vector3 spawnPos)
            {
                var buildingBlocks = Pool.GetList<BuildingBlock>();
                Vis.Entities(spawnPos, 2f, buildingBlocks, Layers.Mask.Construction);
                if (buildingBlocks.Count > 0)
                {
                    var position = spawnPos;
                    var closestBuildingBlock = buildingBlocks
                            .Where(x => !x.ShortPrefabName.Contains("wall"))
                            .OrderBy(x => (x.transform.position - position).magnitude).FirstOrDefault();
                    if (closestBuildingBlock != null)
                    {
                        var worldSpaceBounds = closestBuildingBlock.WorldSpaceBounds();
                        spawnPos = worldSpaceBounds.position;
                        spawnPos.y += worldSpaceBounds.extents.y;
                        Pool.FreeList(ref buildingBlocks);
                        return true;
                    }
                }
                Pool.FreeList(ref buildingBlocks);
                return false;
            }

            #endregion TryGetVehicleParams
        }

        public abstract class FuelVehicleSettings : BaseVehicleSettings, IFuelVehicle
        {
            public int SpawnFuelAmount { get; set; }
            public bool RefundFuelOnKill { get; set; } = true;
            public bool RefundFuelOnCrash { get; set; } = true;

            public override void SetupVehicle(BaseEntity entity, Vehicle vehicle, BasePlayer player, bool justCreated = true)
            {
                if (justCreated)
                {
                    TryGiveFuel(entity, this);
                }
                base.SetupVehicle(entity, vehicle, player, justCreated);
            }

            protected override bool CanRefundFuel(bool isCrash, bool isUnload)
            {
                return isUnload || (isCrash ? RefundFuelOnCrash : RefundFuelOnKill);
            }
        }

        public abstract class InventoryVehicleSettings : BaseVehicleSettings, IInventoryVehicle
        {
            public bool RefundInventoryOnKill { get; set; } = true;
            public bool RefundInventoryOnCrash { get; set; } = true;
            public bool DropInventoryOnRecall { get; set; }

            protected override bool CanDropInventory()
            {
                return DropInventoryOnRecall;
            }

            protected override bool CanRefundInventory(bool isCrash, bool isUnload)
            {
                return isUnload || (isCrash ? RefundInventoryOnCrash : RefundInventoryOnKill);
            }
        }

        public abstract class InvFuelVehicleSettings : BaseVehicleSettings, IFuelVehicle, IInventoryVehicle
        {
            public int SpawnFuelAmount { get; set; }
            public bool RefundFuelOnKill { get; set; } = true;
            public bool RefundFuelOnCrash { get; set; } = true;
            public bool RefundInventoryOnKill { get; set; } = true;
            public bool RefundInventoryOnCrash { get; set; } = true;
            public bool DropInventoryOnRecall { get; set; }

            public override void SetupVehicle(BaseEntity entity, Vehicle vehicle, BasePlayer player, bool justCreated = true)
            {
                if (justCreated)
                {
                    TryGiveFuel(entity, this);
                }
                base.SetupVehicle(entity, vehicle, player, justCreated);
            }

            protected override bool CanDropInventory()
            {
                return DropInventoryOnRecall;
            }

            protected override bool CanRefundInventory(bool isCrash, bool isUnload)
            {
                return isUnload || (isCrash ? RefundInventoryOnCrash : RefundInventoryOnKill);
            }

            protected override bool CanRefundFuel(bool isCrash, bool isUnload)
            {
                return isUnload || (isCrash ? RefundFuelOnCrash : RefundFuelOnKill);
            }
        }

        #endregion BaseSettings

        #region Interface

        public interface IFuelVehicle
        {
            [JsonProperty(PropertyName = "Amount Of Fuel To Spawn", Order = 20)]
            int SpawnFuelAmount { get; set; }

            [JsonProperty(PropertyName = "Refund Fuel On Kill", Order = 21)]
            bool RefundFuelOnKill { get; set; }

            [JsonProperty(PropertyName = "Refund Fuel On Crash", Order = 22)]
            bool RefundFuelOnCrash { get; set; }
        }

        public interface IInventoryVehicle
        {
            [JsonProperty(PropertyName = "Refund Inventory On Kill", Order = 30)]
            bool RefundInventoryOnKill { get; set; }

            [JsonProperty(PropertyName = "Refund Inventory On Crash", Order = 31)]
            bool RefundInventoryOnCrash { get; set; }

            [JsonProperty(PropertyName = "Drop Inventory Items When Vehicle Recall", Order = 49)]
            bool DropInventoryOnRecall { get; set; }
        }

        public interface IModularVehicle
        {
            [JsonProperty(PropertyName = "Refund Engine Items On Kill", Order = 40)]
            bool RefundEngineOnKill { get; set; }

            [JsonProperty(PropertyName = "Refund Engine Items On Crash", Order = 41)]
            bool RefundEngineOnCrash { get; set; }

            [JsonProperty(PropertyName = "Refund Module Items On Kill", Order = 42)]
            bool RefundModuleOnKill { get; set; }

            [JsonProperty(PropertyName = "Refund Module Items On Crash", Order = 43)]
            bool RefundModuleOnCrash { get; set; }
        }

        public interface IAmmoVehicle
        {
            [JsonProperty(PropertyName = "Amount Of Ammo To Spawn", Order = 20)]
            int SpawnAmmoAmount { get; set; }
        }

        public interface ITrainVehicle
        {
        }

        #endregion Interface

        #region Struct

        public struct CooldownPermission
        {
            public double spawnCooldown;
            public double recallCooldown;
        }

        public struct ModuleItem
        {
            public string shortName;
            public float healthPercentage;
        }

        public struct EngineItem
        {
            public string shortName;
            public float conditionPercentage;
        }

        public struct PriceInfo
        {
            public int amount;
            public string displayName;
        }

        public struct TrainComponent
        {
            public TrainComponentType type;
        }

        #endregion Struct

        #region VehicleSettings

        public class SedanSettings : BaseVehicleSettings
        {
        }

        public class ChinookSettings : BaseVehicleSettings
        {
        }

        public class RowboatSettings : InvFuelVehicleSettings
        {
            public override bool IsWaterVehicle => true;

            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as MotorRowboat)?.GetFuelSystem();
            }

            protected override IEnumerable<ItemContainer> GetInventories(BaseEntity entity)
            {
                yield return (entity as MotorRowboat)?.storageUnitInstance.Get(true)?.inventory;
            }
        }

        public class RhibSettings : RowboatSettings
        {
        }

        public class HotAirBalloonSettings : InvFuelVehicleSettings
        {
            protected override float GetSpawnRotationAngle()
            {
                return 180f;
            }

            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as HotAirBalloon)?.fuelSystem;
            }

            protected override IEnumerable<ItemContainer> GetInventories(BaseEntity entity)
            {
                yield return (entity as HotAirBalloon)?.storageUnitInstance.Get(true)?.inventory;
            }
        }

        public class MiniCopterSettings : FuelVehicleSettings
        {
            public override bool IsFightVehicle => true;

            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as MiniCopter)?.GetFuelSystem();
            }
        }

        public class TransportHelicopterSettings : MiniCopterSettings
        {
        }

        public class RidableHorseSettings : InventoryVehicleSettings
        {
            protected override IEnumerable<ItemContainer> GetInventories(BaseEntity entity)
            {
                yield return (entity as RidableHorse)?.inventory;
            }

            public override void PostRecallVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                base.PostRecallVehicle(player, vehicle, position, rotation);

                var ridableHorse = vehicle.Entity as RidableHorse;
                if (ridableHorse != null)
                {
                    ridableHorse.TryLeaveHitch();
                    ridableHorse.DropToGround(ridableHorse.transform.position, true); //ridableHorse.UpdateDropToGroundForDuration(2f);
                }
            }

            protected override void CorrectPositionAndRotation(BasePlayer player, Vehicle vehicle, Vector3 original, Quaternion rotation, out Vector3 spawnPos, out Quaternion spawnRot)
            {
                base.CorrectPositionAndRotation(player, vehicle, original, rotation, out spawnPos, out spawnRot);
                spawnPos += Vector3.up * 0.3f;
            }
        }

        // Only work cart (TrainEngine)
        public class WorkCartSettings : FuelVehicleSettings
        {
            public override bool IsTrainVehicle => true;

            public bool IsConnectableEngine(TrainEngine trainEngine)
            {
                return trainEngine.frontCoupling != null && trainEngine.rearCoupling != null;
            }

            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as TrainEngine)?.GetFuelSystem();
            }

            public override void PostRecallVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                base.PostRecallVehicle(player, vehicle, position, rotation);
                var trainEngine = vehicle.Entity as TrainEngine;
                if (trainEngine != null)
                {
                    TryMoveToTrainTrackNear(trainEngine);
                }
            }

            protected override bool TryGetPositionAndRotation(BasePlayer player, Vehicle vehicle, out string reason, out Vector3 original, out Quaternion rotation)
            {
                if (base.TryGetPositionAndRotation(player, vehicle, out reason, out original, out rotation))
                {
                    if (!TryGetTrainCarPositionAndRotation(player, vehicle, ref reason, ref original, ref rotation))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public class MagnetCraneSettings : FuelVehicleSettings
        {
            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as MagnetCrane)?.GetFuelSystem();
            }
        }

        public class SubmarineSoloSettings : InvFuelVehicleSettings, IAmmoVehicle
        {
            private const int AMMO_ITEM_ID = -1671551935;

            public int SpawnAmmoAmount { get; set; }
            public override bool IsWaterVehicle => true;

            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as BaseSubmarine)?.GetFuelSystem();
            }

            protected override IEnumerable<ItemContainer> GetInventories(BaseEntity entity)
            {
                yield return (entity as BaseSubmarine)?.GetItemContainer()?.inventory;
                yield return (entity as BaseSubmarine)?.GetTorpedoContainer()?.inventory;
            }

            public override void SetupVehicle(BaseEntity entity, Vehicle vehicle, BasePlayer player, bool justCreated = true)
            {
                if (justCreated)
                {
                    TryGiveAmmo(entity);
                }
                base.SetupVehicle(entity, vehicle, player, justCreated);
            }

            private void TryGiveAmmo(BaseEntity entity)
            {
                if (entity == null || SpawnAmmoAmount <= 0)
                {
                    return;
                }
                var ammoContainer = (entity as BaseSubmarine)?.GetTorpedoContainer();
                if (ammoContainer != null && ammoContainer.inventory != null)
                {
                    var ammoItem = ItemManager.CreateByItemID(AMMO_ITEM_ID, SpawnAmmoAmount);
                    if (!ammoItem.MoveToContainer(ammoContainer.inventory))
                    {
                        ammoItem.Remove();
                    }
                }
            }
        }

        public class SubmarineDuoSettings : SubmarineSoloSettings
        {
        }

        public class SnowmobileSettings : InvFuelVehicleSettings
        {
            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as Snowmobile)?.GetFuelSystem();
            }

            protected override IEnumerable<ItemContainer> GetInventories(BaseEntity entity)
            {
                yield return (entity as Snowmobile)?.GetItemContainer()?.inventory;
            }
        }

        public class ModularVehicleSettings : InvFuelVehicleSettings, IModularVehicle
        {
            #region Properties

            public bool RefundEngineOnKill { get; set; } = true;
            public bool RefundEngineOnCrash { get; set; } = true;
            public bool RefundModuleOnKill { get; set; } = true;
            public bool RefundModuleOnCrash { get; set; } = true;

            [JsonProperty(PropertyName = "Chassis Type (Small, Medium, Large)", Order = 50)]
            public ChassisType ChassisType { get; set; } = ChassisType.Small;

            [JsonProperty(PropertyName = "Vehicle Module Items", Order = 51)]
            public List<ModuleItem> ModuleItems { get; set; } = new List<ModuleItem>();

            [JsonProperty(PropertyName = "Vehicle Engine Items", Order = 52)]
            public List<EngineItem> EngineItems { get; set; } = new List<EngineItem>();

            #endregion Properties

            #region ModuleItems

            private List<ModuleItem> _validModuleItems;

            public IEnumerable<ModuleItem> ValidModuleItems
            {
                get
                {
                    if (_validModuleItems == null)
                    {
                        _validModuleItems = new List<ModuleItem>();
                        foreach (var modularItem in ModuleItems)
                        {
                            var itemDefinition = ItemManager.FindItemDefinition(modularItem.shortName);
                            if (itemDefinition != null)
                            {
                                var itemModVehicleModule = itemDefinition.GetComponent<ItemModVehicleModule>();
                                if (itemModVehicleModule == null || !itemModVehicleModule.entityPrefab.isValid)
                                {
                                    Instance.PrintError($"'{modularItem}' is not a valid vehicle module");
                                    continue;
                                }
                                _validModuleItems.Add(modularItem);
                            }
                        }
                    }
                    return _validModuleItems;
                }
            }

            public IEnumerable<Item> CreateModuleItems()
            {
                foreach (var moduleItem in ValidModuleItems)
                {
                    var item = ItemManager.CreateByName(moduleItem.shortName);
                    if (item != null)
                    {
                        item.condition = item.maxCondition * (moduleItem.healthPercentage / 100f);
                        item.MarkDirty();
                        yield return item;
                    }
                }
            }

            #endregion ModuleItems

            #region EngineItems

            private List<EngineItem> _validEngineItems;

            public IEnumerable<EngineItem> ValidEngineItems
            {
                get
                {
                    if (_validEngineItems == null)
                    {
                        _validEngineItems = new List<EngineItem>();
                        foreach (var modularItem in EngineItems)
                        {
                            var itemDefinition = ItemManager.FindItemDefinition(modularItem.shortName);
                            if (itemDefinition != null)
                            {
                                var itemModEngineItem = itemDefinition.GetComponent<ItemModEngineItem>();
                                if (itemModEngineItem == null)
                                {
                                    Instance.PrintError($"'{modularItem}' is not a valid engine item");
                                    continue;
                                }
                                _validEngineItems.Add(modularItem);
                            }
                        }
                    }
                    return _validEngineItems;
                }
            }

            public IEnumerable<Item> CreateEngineItems()
            {
                foreach (var engineItem in ValidEngineItems)
                {
                    var item = ItemManager.CreateByName(engineItem.shortName);
                    if (item != null)
                    {
                        item.condition = item.maxCondition * (engineItem.conditionPercentage / 100f);
                        item.MarkDirty();
                        yield return item;
                    }
                }
            }

            #endregion EngineItems

            public override bool IsNormalVehicle => false;
            public override bool IsModularVehicle => true;

            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as ModularCar)?.GetFuelSystem();
            }

            #region Spawn

            protected override string GetVehiclePrefab(string vehicleType)
            {
                switch (ChassisType)
                {
                    case ChassisType.Small:
                        return PREFAB_CHASSIS_SMALL;
                    case ChassisType.Medium:
                        return PREFAB_CHASSIS_MEDIUM;
                    case ChassisType.Large:
                        return PREFAB_CHASSIS_LARGE;
                    default:
                        return null;
                }
            }

            #region Setup

            public override void SetupVehicle(BaseEntity entity, Vehicle vehicle, BasePlayer player, bool justCreated = true)
            {
                var modularCar = entity as ModularCar;
                if (modularCar != null)
                {
                    if (ValidModuleItems.Any())
                    {
                        AttacheVehicleModules(modularCar, vehicle);
                    }
                    if (ValidEngineItems.Any())
                    {
                        Instance.NextTick(() =>
                        {
                            AddItemsToVehicleEngine(modularCar, vehicle);
                        });
                    }
                }
                base.SetupVehicle(entity, vehicle, player, justCreated);
            }

            #endregion Setup

            #endregion Spawn

            #region Recall

            public override void PreRecallVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                base.PreRecallVehicle(player, vehicle, position, rotation);

                if (vehicle.Entity is ModularCar)
                {
                    var modularCarGarages = Pool.GetList<ModularCarGarage>();
                    Vis.Entities(vehicle.Entity.transform.position, 3f, modularCarGarages, Layers.Mask.Deployed | Layers.Mask.Default);
                    var modularCarGarage = modularCarGarages.FirstOrDefault(x => x.carOccupant == vehicle.Entity);
                    Pool.FreeList(ref modularCarGarages);
                    if (modularCarGarage != null)
                    {
                        modularCarGarage.enabled = false;
                        modularCarGarage.ReleaseOccupant();
                        modularCarGarage.Invoke(() => modularCarGarage.enabled = true, 0.25f);
                    }
                }
            }

            #region DropInventory

            protected override DroppedItemContainer DropVehicleInventory(BasePlayer player, Vehicle vehicle)
            {
                var modularCar = vehicle.Entity as ModularCar;
                if (modularCar != null)
                {
                    foreach (var moduleEntity in modularCar.AttachedModuleEntities)
                    {
                        if (moduleEntity is VehicleModuleEngine)
                        {
                            continue;
                        }
                        var moduleStorage = moduleEntity as VehicleModuleStorage;
                        if (moduleStorage != null)
                        {
                            return moduleStorage.GetContainer()?.inventory?.Drop(PREFAB_ITEM_DROP, vehicle.Entity.GetDropPosition(), vehicle.Entity.transform.rotation, 0);
                        }
                    }
                }
                return null;
            }

            #endregion DropInventory

            #endregion Recall

            #region Refund

            private void GetRefundStatus(bool isCrash, bool isUnload, out bool refundFuel, out bool refundInventory, out bool refundEngine, out bool refundModule)
            {
                if (isUnload)
                {
                    refundFuel = refundInventory = refundEngine = refundModule = true;
                    return;
                }
                refundFuel = isCrash ? RefundFuelOnCrash : RefundFuelOnKill;
                refundInventory = isCrash ? RefundInventoryOnCrash : RefundInventoryOnKill;
                refundEngine = isCrash ? RefundEngineOnCrash : RefundEngineOnKill;
                refundModule = isCrash ? RefundModuleOnCrash : RefundModuleOnKill;
            }

            protected override void CollectVehicleItems(List<Item> items, Vehicle vehicle, bool isCrash, bool isUnload)
            {
                var modularCar = vehicle.Entity as ModularCar;
                if (modularCar != null)
                {
                    bool refundFuel, refundInventory, refundEngine, refundModule;
                    GetRefundStatus(isCrash, isUnload, out refundFuel, out refundInventory, out refundEngine, out refundModule);

                    foreach (var moduleEntity in modularCar.AttachedModuleEntities)
                    {
                        if (refundEngine)
                        {
                            var moduleEngine = moduleEntity as VehicleModuleEngine;
                            if (moduleEngine != null)
                            {
                                var engineContainer = moduleEngine.GetContainer()?.inventory;
                                if (engineContainer != null)
                                {
                                    items.AddRange(engineContainer.itemList);
                                }
                                continue;
                            }
                        }
                        if (refundInventory)
                        {
                            var moduleStorage = moduleEntity as VehicleModuleStorage;
                            if (moduleStorage != null && !(moduleEntity is VehicleModuleEngine))
                            {
                                var storageContainer = moduleStorage.GetContainer()?.inventory;
                                if (storageContainer != null)
                                {
                                    items.AddRange(storageContainer.itemList);
                                }
                            }
                        }
                    }
                    if (refundFuel)
                    {
                        var fuelSystem = GetFuelSystem(modularCar);
                        if (fuelSystem != null)
                        {
                            var fuelContainer = fuelSystem.GetFuelContainer()?.inventory;
                            if (fuelContainer != null)
                            {
                                items.AddRange(fuelContainer.itemList);
                            }
                        }
                    }
                    if (refundModule)
                    {
                        var moduleContainer = modularCar.Inventory?.ModuleContainer;
                        if (moduleContainer != null)
                        {
                            items.AddRange(moduleContainer.itemList);
                        }
                    }
                    //var chassisContainer = modularCar.Inventory?.ChassisContainer;
                    //if (chassisContainer != null)
                    //{
                    //    collect.AddRange(chassisContainer.itemList);
                    //}
                }
            }

            #endregion Refund

            #region VehicleModules

            private void AttacheVehicleModules(ModularCar modularCar, Vehicle vehicle)
            {
                foreach (var moduleItem in CreateModuleItems())
                {
                    if (!modularCar.TryAddModule(moduleItem))
                    {
                        Instance?.PrintError($"Module item '{moduleItem.info.shortname}' in '{vehicle.VehicleType}' cannot be attached to the vehicle");
                        moduleItem.Remove();
                    }
                }
            }

            private void AddItemsToVehicleEngine(ModularCar modularCar, Vehicle vehicle)
            {
                if (modularCar == null || modularCar.IsDestroyed)
                {
                    return;
                }
                foreach (var moduleEntity in modularCar.AttachedModuleEntities)
                {
                    var vehicleModuleEngine = moduleEntity as VehicleModuleEngine;
                    if (vehicleModuleEngine != null)
                    {
                        var engineInventory = vehicleModuleEngine.GetContainer()?.inventory;
                        if (engineInventory != null)
                        {
                            foreach (var engineItem in CreateEngineItems())
                            {
                                var moved = false;
                                for (var i = 0; i < engineInventory.capacity; i++)
                                {
                                    if (engineItem.MoveToContainer(engineInventory, i, false))
                                    {
                                        moved = true;
                                        break;
                                    }
                                }
                                if (!moved)
                                {
                                    Instance?.PrintError($"Engine item '{engineItem.info.shortname}' in '{vehicle.VehicleType}' cannot be move to the vehicle engine inventory");
                                    engineItem.Remove();
                                    engineItem.DoRemove();
                                }
                            }
                        }
                    }
                }
            }

            #endregion VehicleModules
        }

        public class TrainVehicleSettings : FuelVehicleSettings, ITrainVehicle
        {
            #region Properties

            [JsonProperty(PropertyName = "Train Components", Order = 50)]
            public List<TrainComponent> TrainComponents { get; set; } = new List<TrainComponent>();

            #endregion Properties

            public override bool IsNormalVehicle => false;
            public override bool IsTrainVehicle => true;
            public override bool IsConnectableVehicle => true;

            protected override EntityFuelSystem GetFuelSystem(BaseEntity entity)
            {
                return (entity as TrainCar)?.GetFuelSystem();
            }

            protected override string GetVehiclePrefab(string vehicleType)
            {
                return TrainComponents.Count > 0 ? GetTrainVehiclePrefab(TrainComponents[0].type) : base.GetVehiclePrefab(vehicleType);
            }

            #region Spawn

            private static string GetTrainVehiclePrefab(TrainComponentType componentType)
            {
                switch (componentType)
                {
                    case TrainComponentType.Engine:
                        return PREFAB_TRAINENGINE;
                    case TrainComponentType.CoveredEngine:
                        return PREFAB_TRAINENGINE_COVERED;
                    case TrainComponentType.Locomotive:
                        return PREFAB_TRAINENGINE_LOCOMOTIVE;
                    case TrainComponentType.WagonA:
                        return PREFAB_TRAINWAGON_A;
                    case TrainComponentType.WagonB:
                        return PREFAB_TRAINWAGON_B;
                    case TrainComponentType.WagonC:
                        return PREFAB_TRAINWAGON_C;
                    case TrainComponentType.Unloadable:
                        return PREFAB_TRAINWAGON_UNLOADABLE;
                    case TrainComponentType.UnloadableLoot:
                        return PREFAB_TRAINWAGON_UNLOADABLE_LOOT;
                    case TrainComponentType.UnloadableFuel:
                        return PREFAB_TRAINWAGON_UNLOADABLE_FUEL;
                    case TrainComponentType.Caboose:
                        return PREFAB_CABOOSE;
                    default:
                        return null;
                }
            }

            public override BaseEntity SpawnVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                TrainCar prevTrainCar = null, primaryTrainCar = null;
                foreach (var component in TrainComponents)
                {
                    var prefab = GetTrainVehiclePrefab(component.type);
                    if (string.IsNullOrEmpty(prefab))
                    {
                        throw new ArgumentException($"Prefab not found for {vehicle.VehicleType}({component.type})");
                    }
                    float distResult;
                    TrainTrackSpline splineResult;
                    if (prevTrainCar == null)
                    {
                        if (TrainTrackSpline.TryFindTrackNear(position, 20f, out splineResult, out distResult))
                        {
                            position = splineResult.GetPosition(distResult);
                            prevTrainCar = GameManager.server.CreateEntity(prefab, position, rotation) as TrainCar;
                            if (prevTrainCar == null)
                            {
                                continue;
                            }
                            PreSetupVehicle(prevTrainCar, vehicle, player);
                            prevTrainCar.Spawn();
                            prevTrainCar.CancelInvoke(prevTrainCar.KillMessage);
                            SetupVehicle(prevTrainCar, vehicle, player);
                        }
                    }
                    else
                    {
                        var newTrainCar = GameManager.server.CreateEntity(prefab, prevTrainCar.transform.position, prevTrainCar.transform.rotation) as TrainCar;
                        if (newTrainCar == null)
                        {
                            continue;
                        }

                        position += prevTrainCar.transform.rotation * (newTrainCar.bounds.center - Vector3.forward * (newTrainCar.bounds.extents.z + prevTrainCar.bounds.extents.z));
                        if (TrainTrackSpline.TryFindTrackNear(position, 20f, out splineResult, out distResult))
                        {
                            position = splineResult.GetPosition(distResult);
                            newTrainCar.transform.position = position;

                            PreSetupVehicle(newTrainCar, vehicle, player);
                            newTrainCar.Spawn();
                            newTrainCar.CancelInvoke(newTrainCar.KillMessage);
                            SetupVehicle(newTrainCar, vehicle, player);

                            float minSplineDist;
                            var distance = prevTrainCar.RearTrackSection.GetDistance(position, 1f, out minSplineDist);
                            var preferredAltTrack = prevTrainCar.RearTrackSection != prevTrainCar.FrontTrackSection ? prevTrainCar.RearTrackSection : null;
                            newTrainCar.MoveFrontWheelsAlongTrackSpline(prevTrainCar.RearTrackSection, minSplineDist, distance, preferredAltTrack, TrainTrackSpline.TrackSelection.Default);

                            newTrainCar.coupling.frontCoupling.TryCouple(prevTrainCar.coupling.rearCoupling, true);
                            prevTrainCar = newTrainCar;
                        }
                    }
                    if (primaryTrainCar == null)
                    {
                        primaryTrainCar = prevTrainCar;
                    }
                }
                if (primaryTrainCar == null || primaryTrainCar.IsDestroyed)
                {
                    Instance.Print(player, Instance.Lang("NotSpawnedOrRecalled", player.UserIDString, DisplayName));
                    return null;
                }
                Instance.CacheVehicleEntity(primaryTrainCar, vehicle, player);
                return primaryTrainCar;
            }

            #endregion Spawn

            #region Recall

            public override void PreRecallVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                base.PreRecallVehicle(player, vehicle, position, rotation);
                var trainCar = vehicle.Entity as TrainCar;
                if (trainCar != null)
                {
                    trainCar.coupling.Uncouple(true);
                    trainCar.coupling.Uncouple(false);
                }
            }

            public override void PostRecallVehicle(BasePlayer player, Vehicle vehicle, Vector3 position, Quaternion rotation)
            {
                base.PostRecallVehicle(player, vehicle, position, rotation);
                var trainCar = vehicle.Entity as TrainCar;
                if (trainCar != null)
                {
                    TryMoveToTrainTrackNear(trainCar);
                }
            }

            #endregion Recall

            #region Refund

            protected override void CollectVehicleItems(List<Item> items, Vehicle vehicle, bool isCrash, bool isUnload)
            {
                // Refund primary engine fuel only
                if (CanRefundFuel(isCrash, isUnload))
                {
                    var trainCar = vehicle.Entity as TrainCar;
                    if (trainCar != null)
                    {
                        var fuelSystem = GetFuelSystem(trainCar);
                        if (fuelSystem != null)
                        {
                            var fuelContainer = fuelSystem.GetFuelContainer()?.inventory;
                            if (fuelContainer != null)
                            {
                                items.AddRange(fuelContainer.itemList);
                            }
                        }
                    }
                }
            }

            #endregion Refund

            #region TryGetVehicleParams

            protected override bool TryGetPositionAndRotation(BasePlayer player, Vehicle vehicle, out string reason, out Vector3 original, out Quaternion rotation)
            {
                if (base.TryGetPositionAndRotation(player, vehicle, out reason, out original, out rotation))
                {
                    if (!TryGetTrainCarPositionAndRotation(player, vehicle, ref reason, ref original, ref rotation))
                    {
                        return false;
                    }
                }
                return true;
            }

            // protected override void CorrectPositionAndRotation(BasePlayer player, Vehicle vehicle, Vector3 original, Quaternion rotation, out Vector3 spawnPos, out Quaternion spawnRot)
            // {
            //     base.CorrectPositionAndRotation(player, vehicle, original, rotation, out spawnPos, out spawnRot);
            //     // No rotation on recall
            //     if (vehicle.Entity != null)
            //     {
            //         spawnRot = vehicle.Entity.transform.rotation;
            //     } 
            // }

            #endregion TryGetVehicleParams
        }

        #endregion VehicleSettings

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                PreprocessOldConfig();
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                }
                else
                {
                    UpdateConfigValues();
                }
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted. \n{ex}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
            configData.version = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData);
        }

        private void UpdateConfigValues()
        {
            if (configData.version < Version)
            {
                if (configData.version <= default(VersionNumber))
                {
                    string prefix, prefixColor;
                    if (GetConfigValue(out prefix, "Chat Settings", "Chat Prefix") && GetConfigValue(out prefixColor, "Chat Settings", "Chat Prefix Color"))
                    {
                        configData.chat.prefix = $"<color={prefixColor}>{prefix}</color>: ";
                    }
                }
                if (configData.version <= new VersionNumber(1, 7, 3))
                {
                    configData.normalVehicles.sedan.MinDistanceForPlayers = 3f;
                    configData.normalVehicles.chinook.MinDistanceForPlayers = 5f;
                    configData.normalVehicles.rowboat.MinDistanceForPlayers = 2f;
                    configData.normalVehicles.rhib.MinDistanceForPlayers = 3f;
                    configData.normalVehicles.hotAirBalloon.MinDistanceForPlayers = 4f;
                    configData.normalVehicles.ridableHorse.MinDistanceForPlayers = 1f;
                    configData.normalVehicles.miniCopter.MinDistanceForPlayers = 2f;
                    configData.normalVehicles.transportHelicopter.MinDistanceForPlayers = 4f;
                    foreach (var entry in configData.modularVehicles)
                    {
                        switch (entry.Value.ChassisType)
                        {
                            case ChassisType.Small:
                                entry.Value.MinDistanceForPlayers = 2f;
                                break;

                            case ChassisType.Medium:
                                entry.Value.MinDistanceForPlayers = 2.5f;
                                break;

                            case ChassisType.Large:
                                entry.Value.MinDistanceForPlayers = 3f;
                                break;

                            default:
                                continue;
                        }
                    }
                }
                if (configData.version >= new VersionNumber(1, 7, 17) && configData.version <= new VersionNumber(1, 7, 18))
                {
                    LoadData();
                    foreach (var data in storedData.playerData)
                    {
                        Vehicle vehicle;
                        if (data.Value.TryGetValue("SubmarineDouble", out vehicle))
                        {
                            data.Value.Remove("SubmarineDouble");
                            data.Value.Add(nameof(NormalVehicleType.SubmarineDuo), vehicle);
                        }
                    }
                    SaveData();
                }
                configData.version = Version;
            }
        }

        private bool GetConfigValue<T>(out T value, params string[] path)
        {
            var configValue = Config.Get(path);
            if (configValue != null)
            {
                if (configValue is T)
                {
                    value = (T)configValue;
                    return true;
                }
                try
                {
                    value = Config.ConvertValue<T>(configValue);
                    return true;
                }
                catch (Exception ex)
                {
                    PrintError($"GetConfigValue ERROR: path: {string.Join("\\", path)}\n{ex}");
                }
            }

            value = default(T);
            return false;
        }

        private void SetConfigValue(params object[] pathAndTrailingValue)
        {
            Config.Set(pathAndTrailingValue);
        }

        #region Preprocess Old Config

        private void PreprocessOldConfig()
        {
            var config = Config.ReadObject<JObject>();
            if (config == null)
            {
                return;
            }
            //Interface.Oxide.DataFileSystem.WriteObject(Name + "_old", jObject);
            VersionNumber oldVersion;
            if (GetConfigVersionPre(config, out oldVersion))
            {
                if (oldVersion < Version)
                {
                    if (oldVersion < new VersionNumber(1, 7, 35))
                    {
                        try
                        {
                            if (config["Train Vehicle Settings"] == null)
                            {
                                config["Train Vehicle Settings"] = JObject.FromObject(new ConfigData().trainVehicles);
                            }
                            var workCartAboveGround = GetConfigValue(config, "Normal Vehicle Settings", "Work Cart Above Ground Vehicle");
                            if (workCartAboveGround != null)
                            {
                                var settings = workCartAboveGround.ToObject<TrainVehicleSettings>();
                                settings.TrainComponents = new List<TrainComponent>
                                {
                                    new TrainComponent
                                    {
                                        type = TrainComponentType.Engine
                                    }
                                };
                                config["Train Vehicle Settings"]["WorkCartAboveGround"] = JObject.FromObject(settings);
                                ;
                            }
                            var coveredWorkCart = GetConfigValue(config, "Normal Vehicle Settings", "Covered Work Cart Vehicle");
                            if (coveredWorkCart != null)
                            {
                                var settings = coveredWorkCart.ToObject<TrainVehicleSettings>();
                                settings.TrainComponents = new List<TrainComponent>
                                {
                                    new TrainComponent
                                    {
                                        type = TrainComponentType.CoveredEngine
                                    }
                                };
                                config["Train Vehicle Settings"]["WorkCartCovered"] = JObject.FromObject(settings);
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    Config.WriteObject(config);
                    // Interface.Oxide.DataFileSystem.WriteObject(Name + "_new", jObject);
                }
            }
        }

        private JObject GetConfigValue(JObject config, params string[] path)
        {
            if (path.Length < 1)
            {
                throw new ArgumentException("path is empty");
            }

            try
            {
                JToken jToken;
                if (!config.TryGetValue(path[0], out jToken))
                {
                    return null;
                }

                for (var i = 1; i < path.Length; i++)
                {
                    var jObject = jToken as JObject;
                    if (jObject == null || !jObject.TryGetValue(path[i], out jToken))
                    {
                        return null;
                    }
                }
                return jToken as JObject;
            }
            catch (Exception ex)
            {
                PrintError($"GetConfigValue ERROR: path: {string.Join("\\", path)}\n{ex}");
            }
            return null;
        }

        private bool GetConfigValuePre<T>(JObject config, out T value, params string[] path)
        {
            if (path.Length < 1)
            {
                throw new ArgumentException("path is empty");
            }

            try
            {
                JToken jToken;
                if (!config.TryGetValue(path[0], out jToken))
                {
                    value = default(T);
                    return false;
                }

                for (var i = 1; i < path.Length; i++)
                {
                    var jObject = jToken.ToObject<JObject>();
                    if (jObject == null || !jObject.TryGetValue(path[i], out jToken))
                    {
                        value = default(T);
                        return false;
                    }
                }
                value = jToken.ToObject<T>();
                return true;
            }
            catch (Exception ex)
            {
                PrintError($"GetConfigValuePre ERROR: path: {string.Join("\\", path)}\n{ex}");
            }
            value = default(T);
            return false;
        }

        private void SetConfigValuePre(JObject config, object value, params string[] path)
        {
            if (path.Length < 1)
            {
                throw new ArgumentException("path is empty");
            }

            try
            {
                JToken jToken;
                if (!config.TryGetValue(path[0], out jToken))
                {
                    if (path.Length == 1)
                    {
                        jToken = JToken.FromObject(value);
                        config.Add(path[0], jToken);
                        return;
                    }
                    jToken = new JObject();
                    config.Add(path[0], jToken);
                }

                for (var i = 1; i < path.Length - 1; i++)
                {
                    var jObject = jToken as JObject;
                    if (jObject == null || !jObject.TryGetValue(path[i], out jToken))
                    {
                        jToken = new JObject();
                        jObject?.Add(path[i], jToken);
                    }
                }
                var targetToken = jToken as JObject;
                if (targetToken != null)
                {
                    targetToken[path[path.Length - 1]] = JToken.FromObject(value);
                }
                // (jToken as JObject)?.TryAdd(path[path.Length - 1], JToken.FromObject(value));
            }
            catch (Exception ex)
            {
                PrintError($"SetConfigValuePre ERROR: value: {value} path: {string.Join("\\", path)}\n{ex}");
            }
        }

        private bool GetConfigVersionPre(JObject config, out VersionNumber version)
        {
            try
            {
                JToken jToken;
                if (config.TryGetValue("Version", out jToken))
                {
                    version = jToken.ToObject<VersionNumber>();
                    return true;
                }
            }
            catch
            {
                // ignored
            }
            version = default(VersionNumber);
            return false;
        }

        #endregion Preprocess Old Config

        #endregion ConfigurationFile

        #region DataFile

        public StoredData storedData { get; private set; }

        public class StoredData
        {
            public readonly Dictionary<ulong, Dictionary<string, Vehicle>> playerData = new Dictionary<ulong, Dictionary<string, Vehicle>>();

            public IEnumerable<BaseEntity> ActiveVehicles(ulong playerId)
            {
                Dictionary<string, Vehicle> vehicles;
                if (!playerData.TryGetValue(playerId, out vehicles))
                {
                    yield break;
                }

                foreach (var vehicle in vehicles.Values)
                {
                    if (vehicle.Entity != null && !vehicle.Entity.IsDestroyed)
                    {
                        yield return vehicle.Entity;
                    }
                }
            }

            public Dictionary<string, Vehicle> GetPlayerVehicles(ulong playerId, bool readOnly = true)
            {
                Dictionary<string, Vehicle> vehicles;
                if (!playerData.TryGetValue(playerId, out vehicles))
                {
                    if (!readOnly)
                    {
                        vehicles = new Dictionary<string, Vehicle>();
                        playerData.Add(playerId, vehicles);
                        return vehicles;
                    }
                    return null;
                }
                return vehicles;
            }

            public bool IsVehiclePurchased(ulong playerId, string vehicleType, out Vehicle vehicle)
            {
                vehicle = GetVehicleLicense(playerId, vehicleType);
                if (vehicle == null)
                {
                    return false;
                }
                return true;
            }

            public Vehicle GetVehicleLicense(ulong playerId, string vehicleType)
            {
                Dictionary<string, Vehicle> vehicles;
                if (!playerData.TryGetValue(playerId, out vehicles))
                {
                    return null;
                }
                Vehicle vehicle;
                if (!vehicles.TryGetValue(vehicleType, out vehicle))
                {
                    return null;
                }
                return vehicle;
            }

            public bool HasVehicleLicense(ulong playerId, string vehicleType)
            {
                Dictionary<string, Vehicle> vehicles;
                if (!playerData.TryGetValue(playerId, out vehicles))
                {
                    return false;
                }
                return vehicles.ContainsKey(vehicleType);
            }

            public bool AddVehicleLicense(ulong playerId, string vehicleType)
            {
                Dictionary<string, Vehicle> vehicles;
                if (!playerData.TryGetValue(playerId, out vehicles))
                {
                    vehicles = new Dictionary<string, Vehicle>();
                    playerData.Add(playerId, vehicles);
                }
                if (vehicles.ContainsKey(vehicleType))
                {
                    return false;
                }
                vehicles.Add(vehicleType, Vehicle.Create(playerId, vehicleType));
                Instance.SaveData();
                return true;
            }

            public bool RemoveVehicleLicense(ulong playerId, string vehicleType)
            {
                Dictionary<string, Vehicle> vehicles;
                if (!playerData.TryGetValue(playerId, out vehicles))
                {
                    return false;
                }

                if (!vehicles.Remove(vehicleType))
                {
                    return false;
                }
                Instance.SaveData();
                return true;
            }

            public List<string> GetVehicleLicenseNames(ulong playerId)
            {
                Dictionary<string, Vehicle> vehicles;
                if (!playerData.TryGetValue(playerId, out vehicles))
                {
                    return new List<string>();
                }
                return vehicles.Keys.ToList();
            }

            public void PurchaseAllVehicles(ulong playerId)
            {
                var changed = false;
                Dictionary<string, Vehicle> vehicles;
                if (!playerData.TryGetValue(playerId, out vehicles))
                {
                    vehicles = new Dictionary<string, Vehicle>();
                    playerData.Add(playerId, vehicles);
                }
                foreach (var vehicleType in Instance.allVehicleSettings.Keys)
                {
                    if (!vehicles.ContainsKey(vehicleType))
                    {
                        vehicles.Add(vehicleType, Vehicle.Create(playerId, vehicleType));
                        changed = true;
                    }
                }

                if (changed)
                {
                    Instance.SaveData();
                }
            }

            public void AddLicenseForAllPlayers(string vehicleType)
            {
                foreach (var entry in playerData)
                {
                    if (!entry.Value.ContainsKey(vehicleType))
                    {
                        entry.Value.Add(vehicleType, Vehicle.Create(entry.Key, vehicleType));
                    }
                }
            }

            public void RemoveLicenseForAllPlayers(string vehicleType)
            {
                foreach (var entry in playerData)
                {
                    entry.Value.Remove(vehicleType);
                }
            }

            public void ResetPlayerData()
            {
                foreach (var vehicleEntries in playerData)
                {
                    foreach (var vehicleEntry in vehicleEntries.Value)
                    {
                        vehicleEntry.Value.Reset();
                    }
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class Vehicle
        {
            [JsonProperty("entityID")]
            public ulong EntityId { get; set; }

            [JsonProperty("lastDeath")]
            public double LastDeath { get; set; }

            public ulong PlayerId { get; set; }
            public BaseEntity Entity { get; set; }
            public string VehicleType { get; set; }
            public double LastRecall { get; set; }
            public double LastDismount { get; set; }

            public void OnDismount()
            {
                LastDismount = TimeEx.currentTimestamp;
            }

            public void OnRecall()
            {
                LastRecall = TimeEx.currentTimestamp;
            }

            public void OnDeath()
            {
                Entity = null;
                EntityId = 0;
                LastDeath = TimeEx.currentTimestamp;
            }

            public void Reset()
            {
                EntityId = 0;
                LastDeath = 0;
            }

            public static Vehicle Create(ulong playerId, string vehicleType)
            {
                var vehicle = new Vehicle();
                vehicle.VehicleType = vehicleType;
                vehicle.PlayerId = playerId;
                return vehicle;
            }
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                storedData = null;
            }
            if (storedData == null)
            {
                ClearData();
            }
        }

        private void ClearData()
        {
            storedData = new StoredData();
            SaveData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void OnNewSave()
        {
            if (configData.global.clearVehicleOnWipe)
            {
                ClearData();
            }
            else
            {
                storedData.ResetPlayerData();
                SaveData();
            }
        }

        #endregion DataFile

        #region LanguageFile

        private void Print(BasePlayer player, string message)
        {
            Player.Message(player, message, configData.chat.prefix, configData.chat.steamIDIcon);
        }

        private void Print(ConsoleSystem.Arg arg, string message)
        {
            var player = arg.Player();
            if (player == null)
            {
                Puts(message);
            }
            else
            {
                PrintToConsole(player, message);
            }
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, id), args);
            }
            catch (Exception)
            {
                PrintError($"Error in the language formatting of '{key}'. (userid: {id}. lang: {lang.GetLanguage(id)}. args: {string.Join(" ,", args)})");
                throw;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help"] = "These are the available commands:",
                ["HelpLicence1"] = "<color=#4DFF4D>/{0}</color> -- To buy a vehicle",
                ["HelpLicence2"] = "<color=#4DFF4D>/{0}</color> -- To spawn a vehicle",
                ["HelpLicence3"] = "<color=#4DFF4D>/{0}</color> -- To recall a vehicle",
                ["HelpLicence4"] = "<color=#4DFF4D>/{0}</color> -- To kill a vehicle",
                ["HelpLicence5"] = "<color=#4DFF4D>/{0}</color> -- To buy, spawn or recall a <color=#009EFF>{1}</color>",

                ["PriceFormat"] = "<color=#FF1919>{0}</color> x{1}",
                ["HelpBuy"] = "<color=#4DFF4D>/{0} {1}</color> -- To buy a <color=#009EFF>{2}</color>",
                ["HelpBuyPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- To buy a <color=#009EFF>{2}</color>. Price: {3}",
                ["HelpSpawn"] = "<color=#4DFF4D>/{0} {1}</color> -- To spawn a <color=#009EFF>{2}</color>",
                ["HelpSpawnPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- To spawn a <color=#009EFF>{2}</color>. Price: {3}",
                ["HelpRecall"] = "<color=#4DFF4D>/{0} {1}</color> -- To recall a <color=#009EFF>{2}</color>",
                ["HelpRecallPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- To recall a <color=#009EFF>{2}</color>. Price: {3}",
                ["HelpKill"] = "<color=#4DFF4D>/{0} {1}</color> -- To kill a <color=#009EFF>{2}</color>",
                ["HelpKillCustom"] = "<color=#4DFF4D>/{0} {1}</color> or <color=#4DFF4D>/{2}</color>  -- To kill a <color=#009EFF>{3}</color>",

                ["NotAllowed"] = "You do not have permission to use this command.",
                ["RaidBlocked"] = "<color=#FF1919>You may not do that while raid blocked</color>.",
                ["CombatBlocked"] = "<color=#FF1919>You may not do that while combat blocked</color>.",
                ["OptionNotFound"] = "This <color=#009EFF>{0}</color> option doesn't exist.",
                ["VehiclePurchased"] = "You have purchased a <color=#009EFF>{0}</color>, type <color=#4DFF4D>/{1}</color> for more information.",
                ["VehicleAlreadyPurchased"] = "You have already purchased <color=#009EFF>{0}</color>.",
                ["VehicleCannotBeBought"] = "<color=#009EFF>{0}</color> is unpurchasable",
                ["VehicleNotOut"] = "<color=#009EFF>{0}</color> is not out, type <color=#4DFF4D>/{1}</color> for more information.",
                ["AlreadyVehicleOut"] = "You already have a <color=#009EFF>{0}</color> outside, type <color=#4DFF4D>/{1}</color> for more information.",
                ["VehicleNotYetPurchased"] = "You have not yet purchased a <color=#009EFF>{0}</color>, type <color=#4DFF4D>/{1}</color> for more information.",
                ["VehicleSpawned"] = "You spawned your <color=#009EFF>{0}</color>.",
                ["VehicleRecalled"] = "You recalled your <color=#009EFF>{0}</color>.",
                ["VehicleKilled"] = "You killed your <color=#009EFF>{0}</color>.",
                ["VehicleOnSpawnCooldown"] = "You must wait <color=#FF1919>{0}</color> seconds before you can spawn your <color=#009EFF>{1}</color>.",
                ["VehicleOnRecallCooldown"] = "You must wait <color=#FF1919>{0}</color> seconds before you can recall your <color=#009EFF>{1}</color>.",
                ["VehicleOnSpawnCooldownPay"] = "You must wait <color=#FF1919>{0}</color> seconds before you can spawn your <color=#009EFF>{1}</color>. You can bypass this cooldown by using the <color=#FF1919>/{2}</color> command to pay <color=#009EFF>{3}</color>",
                ["VehicleOnRecallCooldownPay"] = "You must wait <color=#FF1919>{0}</color> seconds before you can recall your <color=#009EFF>{1}</color>. You can bypass this cooldown by using the <color=#FF1919>/{2}</color> command to pay <color=#009EFF>{3}</color>",
                ["NotLookingAtWater"] = "You must be looking at water to spawn or recall a <color=#009EFF>{0}</color>.",
                ["BuildingBlocked"] = "You can't spawn a <color=#009EFF>{0}</color> if you don't have the building privileges.",
                ["RefundedVehicleItems"] = "Your <color=#009EFF>{0}</color> vehicle items was refunded to your inventory.",
                ["PlayerMountedOnVehicle"] = "It cannot be recalled or killed when players mounted on your <color=#009EFF>{0}</color>.",
                ["PlayerInSafeZone"] = "You cannot spawn or recall your <color=#009EFF>{0}</color> in the safe zone.",
                ["VehicleInventoryDropped"] = "Your <color=#009EFF>{0}</color> vehicle inventory cannot be recalled, it have dropped to the ground.",
                ["NoResourcesToPurchaseVehicle"] = "You don't have enough resources to buy a <color=#009EFF>{0}</color>. You are missing: \n{1}",
                ["NoResourcesToSpawnVehicle"] = "You don't have enough resources to spawn a <color=#009EFF>{0}</color>. You are missing: \n{1}",
                ["NoResourcesToSpawnVehicleBypass"] = "You don't have enough resources to bypass the cooldown to spawn a <color=#009EFF>{0}</color>. You are missing: \n{1}",
                ["NoResourcesToRecallVehicle"] = "You don't have enough resources to recall a <color=#009EFF>{0}</color>. You are missing: \n{1}",
                ["NoResourcesToRecallVehicleBypass"] = "You don't have enough resources to bypass the cooldown to recall a <color=#009EFF>{0}</color>. You are missing: \n{1}",
                ["MountedOrParented"] = "You cannot spawn or recall a <color=#009EFF>{0}</color> when mounted or parented.",
                ["RecallTooFar"] = "You must be within <color=#FF1919>{0}</color> meters of <color=#009EFF>{1}</color> to recall.",
                ["KillTooFar"] = "You must be within <color=#FF1919>{0}</color> meters of <color=#009EFF>{1}</color> to kill.",
                ["PlayersOnNearby"] = "You cannot spawn or recall a <color=#009EFF>{0}</color> when there are players near the position you are looking at.",
                ["RecallWasBlocked"] = "An external plugin blocked you from recalling a <color=#009EFF>{0}</color>.",
                ["SpawnWasBlocked"] = "An external plugin blocked you from spawning a <color=#009EFF>{0}</color>.",
                ["VehiclesLimit"] = "You can have up to <color=#009EFF>{0}</color> vehicles at a time.",
                ["TooFarTrainTrack"] = "You are too far from the train track.",
                ["TooCloseTrainBarricadeOrWorkCart"] = "You are too close to the train barricade or work cart.",
                ["NotSpawnedOrRecalled"] = "For some reason, your <color=#009EFF>{0}</color> vehicle was not spawned/recalled",

                ["CantUse"] = "Sorry! This {0} belongs to {1}.You cannot use it."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help"] = "可用命令列表:",
                ["HelpLicence1"] = "<color=#4DFF4D>/{0}</color> -- 购买一辆载具",
                ["HelpLicence2"] = "<color=#4DFF4D>/{0}</color> -- 生成一辆载具",
                ["HelpLicence3"] = "<color=#4DFF4D>/{0}</color> -- 召回一辆载具",
                ["HelpLicence4"] = "<color=#4DFF4D>/{0}</color> -- 摧毁一辆载具",
                ["HelpLicence5"] = "<color=#4DFF4D>/{0}</color> -- 购买，生成，召回一辆 <color=#009EFF>{1}</color>",

                ["PriceFormat"] = "<color=#FF1919>{0}</color> x{1}",
                ["HelpBuy"] = "<color=#4DFF4D>/{0} {1}</color> -- 购买一辆 <color=#009EFF>{2}</color>",
                ["HelpBuyPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- 购买一辆 <color=#009EFF>{2}</color>，价格: {3}",
                ["HelpSpawn"] = "<color=#4DFF4D>/{0} {1}</color> -- 生成一辆 <color=#009EFF>{2}</color>",
                ["HelpSpawnPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- 生成一辆 <color=#009EFF>{2}</color>，价格: {3}",
                ["HelpRecall"] = "<color=#4DFF4D>/{0} {1}</color> -- 召回一辆 <color=#009EFF>{2}</color>",
                ["HelpRecallPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- 召回一辆 <color=#009EFF>{2}</color>，价格: {3}",
                ["HelpKill"] = "<color=#4DFF4D>/{0} {1}</color> -- 摧毁一辆 <color=#009EFF>{2}</color>",
                ["HelpKillCustom"] = "<color=#4DFF4D>/{0} {1}</color> 或者 <color=#4DFF4D>/{2}</color>  -- 摧毁一辆 <color=#009EFF>{3}</color>",

                ["NotAllowed"] = "您没有权限使用该命令",
                ["RaidBlocked"] = "<color=#FF1919>您被突袭阻止了，不能使用该命令</color>",
                ["CombatBlocked"] = "<color=#FF1919>您被战斗阻止了，不能使用该命令</color>",
                ["OptionNotFound"] = "选项 <color=#009EFF>{0}</color> 不存在",
                ["VehiclePurchased"] = "您购买了 <color=#009EFF>{0}</color>, 输入 <color=#4DFF4D>/{1}</color> 了解更多信息",
                ["VehicleAlreadyPurchased"] = "您已经购买了 <color=#009EFF>{0}</color>",
                ["VehicleCannotBeBought"] = "<color=#009EFF>{0}</color> 是不可购买的",
                ["VehicleNotOut"] = "您还没有生成您的 <color=#009EFF>{0}</color>, 输入 <color=#4DFF4D>/{1}</color> 了解更多信息",
                ["AlreadyVehicleOut"] = "您已经生成了您的 <color=#009EFF>{0}</color>, 输入 <color=#4DFF4D>/{1}</color> 了解更多信息",
                ["VehicleNotYetPurchased"] = "您还没有购买 <color=#009EFF>{0}</color>, 输入 <color=#4DFF4D>/{1}</color> 了解更多信息",
                ["VehicleSpawned"] = "您生成了您的 <color=#009EFF>{0}</color>",
                ["VehicleRecalled"] = "您召回了您的 <color=#009EFF>{0}</color>",
                ["VehicleKilled"] = "您摧毁了您的 <color=#009EFF>{0}</color>",
                ["VehicleOnSpawnCooldown"] = "您必须等待 <color=#FF1919>{0}</color> 秒，才能生成您的 <color=#009EFF>{1}</color>",
                ["VehicleOnRecallCooldown"] = "您必须等待 <color=#FF1919>{0}</color> 秒，才能召回您的 <color=#009EFF>{1}</color>",
                ["VehicleOnSpawnCooldownPay"] = "您必须等待 <color=#FF1919>{0}</color> 秒，才能生成您的 <color=#009EFF>{1}</color>。你可以使用 <color=#FF1919>/{2}</color> 命令支付 <color=#009EFF>{3}</color> 来绕过这个冷却时间",
                ["VehicleOnRecallCooldownPay"] = "您必须等待 <color=#FF1919>{0}</color> 秒，才能召回您的 <color=#009EFF>{1}</color>。你可以使用 <color=#FF1919>/{2}</color> 命令支付 <color=#009EFF>{3}</color> 来绕过这个冷却时间",
                ["NotLookingAtWater"] = "您必须看着水面才能生成您的 <color=#009EFF>{0}</color>",
                ["BuildingBlocked"] = "您没有领地柜权限，无法生成您的 <color=#009EFF>{0}</color>",
                ["RefundedVehicleItems"] = "您的 <color=#009EFF>{0}</color> 载具物品已经归还回您的库存",
                ["PlayerMountedOnVehicle"] = "您的 <color=#009EFF>{0}</color> 上坐着玩家，无法被召回或摧毁",
                ["PlayerInSafeZone"] = "您不能在安全区域内生成或召回您的 <color=#009EFF>{0}</color>",
                ["VehicleInventoryDropped"] = "您的 <color=#009EFF>{0}</color> 载具物品不能召回，它已经掉落在地上了",
                ["NoResourcesToPurchaseVehicle"] = "您没有足够的资源购买 <color=#009EFF>{0}</color>，还需要: \n{1}",
                ["NoResourcesToSpawnVehicle"] = "您没有足够的资源生成 <color=#009EFF>{0}</color>，还需要: \n{1}",
                ["NoResourcesToSpawnVehicleBypass"] = "您没有足够的资源绕过冷却时间来生成 <color=#009EFF>{0}</color>，还需要: \n{1}",
                ["NoResourcesToRecallVehicle"] = "您没有足够的资源召回 <color=#009EFF>{0}</color>，还需要: \n{1}",
                ["NoResourcesToRecallVehicleBypass"] = "您没有足够的资源绕过冷却时间来召回 <color=#009EFF>{0}</color>，还需要: \n{1}",
                ["MountedOrParented"] = "当您坐着或者在附着在实体上时无法生成或召回 <color=#009EFF>{0}</color>",
                ["RecallTooFar"] = "您必须在 <color=#FF1919>{0}</color> 米内才能召回您的 <color=#009EFF>{1}</color>",
                ["KillTooFar"] = "您必须在 <color=#FF1919>{0}</color> 米内才能摧毁您的 <color=#009EFF>{1}</color>",
                ["PlayersOnNearby"] = "您正在看着的位置附近有玩家时无法生成或召回 <color=#009EFF>{0}</color>",
                ["RecallWasBlocked"] = "有其他插件阻止您召回 <color=#009EFF>{0}</color>.",
                ["SpawnWasBlocked"] = "有其他插件阻止您生成 <color=#009EFF>{0}</color>.",
                ["VehiclesLimit"] = "您在同一时间内最多可以拥有 <color=#009EFF>{0}</color> 辆载具",
                ["TooFarTrainTrack"] = "您距离铁路轨道太远了",
                ["TooCloseTrainBarricadeOrWorkCart"] = "您距离铁轨障碍物或其它火车太近了",
                ["NotSpawnedOrRecalled"] = "由于某些原因，您的 <color=#009EFF>{0}</color> 载具无法生成或召回",

                ["CantUse"] = "您不能使用它，这个 {0} 属于 {1}"
            }, this, "zh-CN");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help"] = "Список доступных команд:",
                ["HelpLicence1"] = "<color=#4DFF4D>/{0}</color> -- Купить транспорт",
                ["HelpLicence2"] = "<color=#4DFF4D>/{0}</color> -- Создать транспорт",
                ["HelpLicence3"] = "<color=#4DFF4D>/{0}</color> -- Вызвать транспорт",
                ["HelpLicence4"] = "<color=#4DFF4D>/{0}</color> -- Уничтожить транспорт",
                ["HelpLicence5"] = "<color=#4DFF4D>/{0}</color> -- Купить, создать, или вызвать <color=#009EFF>{1}</color>",

                ["PriceFormat"] = "<color=#FF1919>{0}</color> x{1}",
                ["HelpBuy"] = "<color=#4DFF4D>/{0} {1}</color> -- Купить <color=#009EFF>{2}</color>.",
                ["HelpBuyPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- Купить <color=#009EFF>{2}</color>. Цена: {3}",
                ["HelpSpawn"] = "<color=#4DFF4D>/{0} {1}</color> -- Создать <color=#009EFF>{2}</color>",
                ["HelpSpawnPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- Вызывать <color=#009EFF>{2}</color>. Цена: {3}",
                ["HelpRecall"] = "<color=#4DFF4D>/{0} {1}</color> -- Вызвать <color=#009EFF>{2}</color>",
                ["HelpRecallPrice"] = "<color=#4DFF4D>/{0} {1}</color> -- Вызвать <color=#009EFF>{2}</color>. Цена: {3}",
                ["HelpKill"] = "<color=#4DFF4D>/{0} {1}</color> -- Уничтожить <color=#009EFF>{2}</color>",
                ["HelpKillCustom"] = "<color=#4DFF4D>/{0} {1}</color> или же <color=#4DFF4D>/{2}</color>  -- Уничтожить <color=#009EFF>{3}</color>",

                ["NotAllowed"] = "У вас нет разрешения для использования данной команды.",
                ["RaidBlocked"] = "<color=#FF1919>Вы не можете это сделать из-за блокировки (рейд)</color>.",
                ["CombatBlocked"] = "<color=#FF1919>Вы не можете это сделать из-за блокировки (бой)</color>.",
                ["OptionNotFound"] = "Опция <color=#009EFF>{0}</color> не существует.",
                ["VehiclePurchased"] = "Вы приобрели <color=#009EFF>{0}</color>, напишите <color=#4DFF4D>/{1}</color> для получения дополнительной информации.",
                ["VehicleAlreadyPurchased"] = "Вы уже приобрели <color=#009EFF>{0}</color>.",
                ["VehicleCannotBeBought"] = "<color=#009EFF>{0}</color> приобрести невозможно",
                ["VehicleNotOut"] = "<color=#009EFF>{0}</color> отсутствует. Напишите <color=#4DFF4D>/{1}</color> для получения дополнительной информации.",
                ["AlreadyVehicleOut"] = "У вас уже есть <color=#009EFF>{0}</color>, напишите <color=#4DFF4D>/{1}</color>  для получения дополнительной информации.",
                ["VehicleNotYetPurchased"] = "Вы ещё не приобрели <color=#009EFF>{0}</color>. Напишите <color=#4DFF4D>/{1}</color> для получения дополнительной информации.",
                ["VehicleSpawned"] = "Вы создали ваш <color=#009EFF>{0}</color>.",
                ["VehicleRecalled"] = "Вы вызвали ваш <color=#009EFF>{0}</color>.",
                ["VehicleKilled"] = "Вы уничтожили ваш <color=#009EFF>{0}</color>.",
                ["VehicleOnSpawnCooldown"] = "Вам необходимо подождать <color=#FF1919>{0}</color> секунд прежде, чем создать свой <color=#009EFF>{1}</color>.",
                ["VehicleOnRecallCooldown"] = "Вам необходимо подождать <color=#FF1919>{0}</color> секунд прежде, чем вызвать свой <color=#009EFF>{1}</color>.",
                ["VehicleOnSpawnCooldownPay"] = "Вам необходимо подождать <color=#FF1919>{0}</color> секунд прежде, чем создать свой <color=#009EFF>{1}</color>. Вы можете обойти это время восстановления, используя команду <color=#FF1919>/{2}</color>, чтобы заплатить <color=#009EFF>{3}</color>",
                ["VehicleOnRecallCooldownPay"] = "Вам необходимо подождать <color=#FF1919>{0}</color> секунд прежде, чем вызвать свой <color=#009EFF>{1}</color>. Вы можете обойти это время восстановления, используя команду <color=#FF1919>/{2}</color>, чтобы заплатить <color=#009EFF>{3}</color>",
                ["NotLookingAtWater"] = "Вы должны смотреть на воду, чтобы создать или вызвать <color=#009EFF>{0}</color>.",
                ["BuildingBlocked"] = "Вы не можете создать <color=#009EFF>{0}</color> если отсутствует право строительства.",
                ["RefundedVehicleItems"] = "Запчасти от вашего <color=#009EFF>{0}</color> были возвращены в ваш инвентарь.",
                ["PlayerMountedOnVehicle"] = "Нельзя вызвать, когда игрок находится в вашем <color=#009EFF>{0}</color>.",
                ["PlayerInSafeZone"] = "Вы не можете создать, или вызвать ваш <color=#009EFF>{0}</color> в безопасной зоне.",
                ["VehicleInventoryDropped"] = "Инвентарь из вашего <color=#009EFF>{0}</color> не может быть вызван, он выброшен на землю.",
                ["NoResourcesToPurchaseVehicle"] = "У вас недостаточно ресурсов для покупки <color=#009EFF>{0}</color>. Вам не хватает: \n{1}",
                ["NoResourcesToSpawnVehicle"] = "У вас недостаточно ресурсов для покупки <color=#009EFF>{0}</color>. Вам не хватает: \n{1}",
                ["NoResourcesToSpawnVehicleBypass"] = "У вас недостаточно ресурсов для покупки <color=#009EFF>{0}</color>. Вам не хватает: \n{1}",
                ["NoResourcesToRecallVehicle"] = "У вас недостаточно ресурсов для покупки <color=#009EFF>{0}</color>. Вам не хватает: \n{1}",
                ["NoResourcesToRecallVehicleBypass"] = "У вас недостаточно ресурсов для покупки <color=#009EFF>{0}</color>. Вам не хватает: \n{1}",
                ["MountedOrParented"] = "Вы не можете создать <color=#009EFF>{0}</color> когда сидите или привязаны к объекту.",
                ["RecallTooFar"] = "Вы должны быть в пределах <color=#FF1919>{0}</color> метров от <color=#009EFF>{1}</color>, чтобы вызывать.",
                ["KillTooFar"] = "Вы должны быть в пределах <color=#FF1919>{0}</color> метров от <color=#009EFF>{1}</color>, уничтожить.",
                ["PlayersOnNearby"] = "Вы не можете создать <color=#009EFF>{0}</color> когда рядом с той позицией, на которую вы смотрите, есть игроки.",
                ["RecallWasBlocked"] = "Внешний плагин заблокировал вам вызвать <color=#009EFF>{0}</color>.",
                ["SpawnWasBlocked"] = "Внешний плагин заблокировал вам создать <color=#009EFF>{0}</color>.",
                ["VehiclesLimit"] = "У вас может быть до <color=#009EFF>{0}</color> автомобилей одновременно",
                ["TooFarTrainTrack"] = "Вы слишком далеко от железнодорожных путей",
                ["TooCloseTrainBarricadeOrWorkCart"] = "Вы слишком близко к железнодорожной баррикаде или рабочей тележке",
                ["NotSpawnedOrRecalled"] = "По какой-то причине ваш <color=#009EFF>{0}</color>  автомобилей не был вызван / отозван",

                ["CantUse"] = "Простите! Этот {0} принадлежит {1}. Вы не можете его использовать."
            }, this, "ru");
        }

        #endregion LanguageFile
    }
}