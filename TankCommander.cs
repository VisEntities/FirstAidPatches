using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Globalization;
using Rust;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Tank Commander", "k1lly0u", "0.2.3")]
    [Description("Drive tanks, shoot stuff")]
    class TankCommander : RustPlugin
    {
        #region Fields
        [PluginReference]
        private Plugin Friends, Clans, Godmode;
       
        private static TankCommander ins;

        private List<APCController> controllers = new List<APCController>();
        
        private Dictionary<CommandType, BUTTON> controlButtons;

        private int rocketId;
        private int mgId;

        private const string APC_PREFAB = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        private const string CHAIR_PREFAB = "assets/prefabs/vehicle/seats/miniheliseat.prefab";
        private const string UI_HEALTH = "TCUI_Health";
        private const string UI_AMMO_MG = "TCUI_Ammo_MG";
        private const string UI_AMMO_ROCKET = "TCUI_Ammo_Rocket";

        private const int TARGET_LAYERS = ~(1 << 10 | 1 << 13 | 1 << 17 | 1 << 18 | 1 << 20 | 1 << 28 | 1 << 29);
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("tankcommander.admin", this);
            permission.RegisterPermission("tankcommander.use", this);
        }

        private void OnServerInitialized()
        {
            ins = this;

            ConvertControlButtons();

            rocketId = ItemManager.itemList.Find(x => x.shortname == configData.Weapons.Cannon.Type)?.itemid ?? 0;
            mgId = ItemManager.itemList.Find(x => x.shortname == configData.Weapons.MG.Type)?.itemid ?? 0;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC)
            {
                APCController controller = entity.GetComponent<APCController>();
                if (controller != null)
                {
                    controller.ManageDamage(info);
                    return null;
                }
            }

            if (entity is BasePlayer)
            {
                if (IsOnboardAPC(entity as BasePlayer))
                    return true;

                if (info.Initiator is BradleyAPC)
                {
                    APCController controller = (info.Initiator as BradleyAPC).GetComponent<APCController>();
                    if (controller != null)
                    {
                        if (!controller.HasCommander)
                            return true;
                    }
                }
            }
            else if (entity is BaseMountable)
            {
                if (entity.GetComponentInParent<APCController>())
                    return true;
            }

            return null;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || IsOnboardAPC(player) || !HasPermission(player, "tankcommander.use"))
                return;

            if (configData.Inventory.Enabled && input.WasJustPressed(controlButtons[CommandType.Inventory]))
            {
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {
                    APCController controller = hit.GetEntity()?.GetComponent<APCController>();
                    if (controller != null && !controller.HasCommander)
                        OpenTankInventory(player, controller);
                }
                return;
            }

            if (input.WasJustPressed(controlButtons[CommandType.EnterExit]))
            {
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {
                    APCController controller = hit.GetEntity()?.GetComponent<APCController>();
                    if (controller != null)
                    {                        
                        if (!controller.HasCommander)
                        {
                            controller.MountPlayer(player);
                        }
                        else
                        {
                            if (configData.Passengers.Enabled && controller.CanMountPlayer())
                            {
                                BasePlayer commander = controller.Commander;

                                if (!configData.Passengers.UseFriends && !configData.Passengers.UseClans)
                                {
                                    controller.MountPlayer(player);
                                    return;
                                }

                                if (configData.Passengers.UseFriends && AreFriends(commander.userID, player.userID))
                                {
                                    controller.MountPlayer(player);
                                    return;
                                }

                                if (configData.Passengers.UseClans && IsClanmate(commander.userID, player.userID))
                                {
                                    controller.MountPlayer(player);
                                    return;
                                }

                                player.ChatMessage(msg("not_friend", player.UserIDString));
                            }
                            else player.ChatMessage(msg("in_use", player.UserIDString));
                        }
                    }
                }
            }
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable mountable)
        {
            APCController controller = mountable?.GetComponentInParent<APCController>();
            if (controller != null)
            {
                controller.DismountPlayer(player);
                return false;
            }

            return null;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            APCController controller;
            if (IsOnboardAPC(player, out controller))
            {
                controller.DismountPlayer(player);
            }
        }

        private object OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity entity)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null || !IsOnboardAPC(player))
                return null;

            if (Godmode && (bool)Godmode.Call("IsGod", player.UserIDString))
                return null;

            return true;
        }

        private void Unload()
        {
            foreach (APCController controller in controllers)
                UnityEngine.Object.Destroy(controller);

            APCController[] objects = UnityEngine.Object.FindObjectsOfType<APCController>();
            if (objects != null)
            {
                foreach(APCController obj in objects)
                    UnityEngine.Object.Destroy(obj);
            }

            controllers.Clear();

            configData = null;
            ins = null;
        }
        #endregion

        #region Functions
        private bool IsOnboardAPC(BasePlayer player) => player?.GetMounted()?.GetComponentInParent<APCController>() != null;

        private bool IsOnboardAPC(BasePlayer player, out APCController controller)
        {
            controller = player?.GetMounted()?.GetComponentInParent<APCController>();
            return controller != null;
        }

        private T ParseType<T>(string type) => (T)Enum.Parse(typeof(T), type, true);

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm) || permission.UserHasPermission(player.UserIDString, "tankcommander.admin");

        private void ConvertControlButtons()
        {
            controlButtons = new Dictionary<CommandType, BUTTON>
            {
                [CommandType.EnterExit] = ParseType<BUTTON>(configData.Buttons.Enter),
                [CommandType.Lights] = ParseType<BUTTON>(configData.Buttons.Lights),
                [CommandType.Inventory] = ParseType<BUTTON>(configData.Buttons.Inventory),
                [CommandType.Boost] = ParseType<BUTTON>(configData.Buttons.Boost),
                [CommandType.Cannon] = ParseType<BUTTON>(configData.Buttons.Cannon),
                [CommandType.Coax] = ParseType<BUTTON>(configData.Buttons.Coax),
                [CommandType.MG] = ParseType<BUTTON>(configData.Buttons.MG)
            };
        }

        private void OpenTankInventory(BasePlayer player, APCController controller)
        {
            player.inventory.loot.Clear();
            player.inventory.loot.entitySource = controller.entity;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.AddContainer(controller.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic");
            player.SendNetworkUpdate();
        }
        #endregion

        #region Component
        private enum CommandType { EnterExit, Lights, Inventory, Boost, Cannon, Coax, MG }
        
        private class APCController : MonoBehaviour
        {
            public BradleyAPC entity;
            private Rigidbody rigidBody;
            private BaseMountable[] mountables;

            private WheelCollider[] leftWheels;
            private WheelCollider[] rightWheels;

            public ItemContainer inventory;

            private float accelTimeTaken;
            private float accelTimeToTake = 3f;

            private float forwardTorque = 2000f;
            private float maxBrakeTorque = 50f;
            private float turnTorque = 2000f;

            private float lastFireCannon;
            private float lastFireMG;
            private float lastFireCoax;

            private bool isDying = false;
            private RaycastHit eyeRay;

            private Vector3 mouseInput = Vector3.zero;
            private Vector3 aimVector = Vector3.forward;
            private Vector3 aimVectorTop = Vector3.forward;

            private Dictionary<CommandType, BUTTON> controlButtons;
            private ConfigData.WeaponOptions.WeaponSystem cannon;
            private ConfigData.WeaponOptions.WeaponSystem mg;
            private ConfigData.WeaponOptions.WeaponSystem coax;
            private ConfigData.CrushableTypes crushables;

            public bool HasCommander
            {
                get
                {
                    return mountables[0].GetMounted() != null;
                }
            }

            public BasePlayer Commander { get; private set; }

            private void Awake()
            {
                entity = GetComponent<BradleyAPC>();

                entity.enabled = false;

                entity.CancelInvoke(entity.UpdateTargetList);
                entity.CancelInvoke(entity.UpdateTargetVisibilities);

                rigidBody = entity.myRigidBody;
                leftWheels = entity.leftWheels;
                rightWheels = entity.rightWheels;

                forwardTorque = configData.Movement.ForwardTorque;
                turnTorque = configData.Movement.TurnTorque;
                maxBrakeTorque = configData.Movement.BrakeTorque;
                accelTimeToTake = configData.Movement.Acceleration;

                controlButtons = ins.controlButtons;
                cannon = configData.Weapons.Cannon;
                mg = configData.Weapons.MG;
                coax = configData.Weapons.Coax;
                crushables = configData.Crushables;

                if (configData.Inventory.Enabled)
                {
                    inventory = new ItemContainer();
                    inventory.ServerInitialize(null, configData.Inventory.Size);
                    if (inventory.uid.Value == 0)
                        inventory.GiveUID();
                }
                
                CreateMountPoints();
                SetInitialAimDirection();
            }

            private void OnDestroy()
            {
                DismountAll();

                for (int i = 0; i < mountables.Length; i++)
                {
                    BaseMountable mountable = mountables[i];
                    if (mountable != null && !mountable.IsDestroyed)
                        mountable.Kill();
                }                

                if (entity != null && !entity.IsDestroyed)
                    entity.Kill();
            }

            private void Update()
            {
                if (Commander != null)
                {                   
                    DoMovementControls();
                    AdjustAiming();
                    DoWeaponControls();

                    entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
            }

            private void LateUpdate()
            {
                mouseInput = Commander?.serverInput?.current?.mouseDelta ?? Vector2.zero;
            }

            private void OnCollisionEnter(Collision collision)
            {
                if (!HasCommander) return;

                GameObject gObject = collision.gameObject;
                if (gObject == null)
                    return;

                if (crushables.Players)
                {
                    BasePlayer triggerPlayer = gObject.GetComponentInParent<BasePlayer>();
                    if (triggerPlayer != null && !IsPassenger(triggerPlayer))
                    {
                        triggerPlayer.Die(new HitInfo(Commander, triggerPlayer, DamageType.Blunt, 200f));
                        return;
                    }
                }

                if (crushables.Animals)
                {
                    BaseNpc npc = gObject.GetComponentInParent<BaseNpc>();
                    if (npc != null)
                    {
                        npc.Die(new HitInfo(Commander, npc, DamageType.Blunt, 200f));
                        return;
                    }
                }

                float impactForce = CalculateImpactForce(collision);

                if (crushables.Buildings)
                {
                    BuildingBlock buildingBlock = gObject.GetComponentInParent<BuildingBlock>();
                    if (buildingBlock != null && impactForce >= crushables.GradeForce[buildingBlock.grade.ToString()])
                    {
                        buildingBlock.Die(new HitInfo(Commander, buildingBlock, DamageType.Blunt, 1000f));
                        return;
                    }

                    SimpleBuildingBlock simpleBlock = gObject.GetComponentInParent<SimpleBuildingBlock>();
                    if (simpleBlock != null && impactForce >= crushables.WallForce)
                    {
                        simpleBlock.Die(new HitInfo(Commander, simpleBlock, DamageType.Blunt, 1500));
                        return;
                    }
                }

                if (crushables.Loot)
                {
                    LootContainer loot = gObject.GetComponentInParent<LootContainer>();
                    if (loot != null)
                    {
                        loot.Die(new HitInfo(Commander, loot, DamageType.Blunt, 200f));
                        return;
                    }
                }

                if (crushables.Resources)
                {
                    ResourceEntity resource = gObject.GetComponentInParent<ResourceEntity>();
                    if (resource != null && impactForce >= crushables.ResourceForce)
                    {
                        resource.Kill(BaseNetworkable.DestroyMode.None);
                        return;
                    }
                }
            }

            private float CalculateImpactForce(Collision col)
            {
                float impactVelocityX = rigidBody.velocity.x;
                impactVelocityX *= Mathf.Sign(impactVelocityX);

                float impactVelocityY = rigidBody.velocity.y;
                impactVelocityY *= Mathf.Sign(impactVelocityY);

                float impactVelocity = impactVelocityX + impactVelocityY;
                float impactForce = impactVelocity * rigidBody.mass;
                impactForce *= Mathf.Sign(impactForce);

                return impactForce;
            }

            private bool IsPassenger(BasePlayer player)
            {
                BaseMountable mountable = player.GetMounted();
                if (mountable == null)
                    return false;

                return mountables.Contains(mountable);
            }

            #region Mounting
            private List<KeyValuePair<Vector3, Vector3>> mountOffsets = new List<KeyValuePair<Vector3, Vector3>>()
            {
                new KeyValuePair<Vector3, Vector3>(new Vector3(0.6f, 1.9f, -0.6f), Vector3.zero),
                new KeyValuePair<Vector3, Vector3>(new Vector3(-0.6f, 0.4f, -1.3f), new Vector3(0f, 90f, 0f)),
                new KeyValuePair<Vector3, Vector3>(new Vector3(-0.6f, 0.4f, -2f), new Vector3(0f, 90f, 0f)),
                new KeyValuePair<Vector3, Vector3>(new Vector3(0.5f, 0.4f, -1.3f), new Vector3(0f, -90f, 0f)),
                new KeyValuePair<Vector3, Vector3>(new Vector3(0.5f, 0.4f, -2f), new Vector3(0f, -90f, 0f)),
            };

            private void CreateMountPoints()
            {
                int passengers = Mathf.Clamp(configData.Passengers.Max, 0, 4) + 1;
                mountables = new BaseMountable[passengers];

                for (int i = 0; i < passengers; i++)
                {
                    CreateMountPoint(i);
                }
            }

            private void CreateMountPoint(int index)
            {
                KeyValuePair<Vector3, Vector3> offset = mountOffsets[index];

                BaseMountable mountable = GameManager.server.CreateEntity(CHAIR_PREFAB, entity.transform.position) as BaseMountable;
                mountable.enableSaving = false;                
                mountable.Spawn();

                Destroy(mountable.GetComponent<DestroyOnGroundMissing>());
                Destroy(mountable.GetComponent<GroundWatch>());

                mountable.SetParent(entity, index == 0 ? (uint)4239370974 : (uint)0, false, true);
                mountable.transform.localPosition = offset.Key;
                mountable.transform.localRotation = Quaternion.Euler(offset.Value);
                
                GameObject tr = new GameObject("Seat Transform");
                tr.transform.SetParent(mountable.transform);
                tr.transform.localPosition = index > 0 ? new Vector3(0, 0, -3f) : new Vector3(3f, 0, 0);

                mountable.dismountPositions = new Transform[] { tr.transform };

                mountables[index] = mountable;
            }

            public bool CanMountPlayer()
            {
                for (int i = 0; i < mountables.Length; i++)
                {
                    BaseMountable mountable = mountables[i];
                    if (!mountable.IsMounted())
                        return true;
                }

                return false;
            }

            public void MountPlayer(BasePlayer player)
            {
                for (int i = 0; i < mountables.Length; i++)
                {                    
                    BaseMountable mountable = mountables[i];
                    if (mountable.IsMounted())
                        continue;

                    player.EnsureDismounted();
                    mountable._mounted = player;
                    player.MountObject(mountable, 0);
                    player.MovePosition(mountable.mountAnchor.transform.position);
                    player.transform.rotation = mountable.mountAnchor.transform.rotation;
                    player.ServerRotation = mountable.mountAnchor.transform.rotation;
                    player.OverrideViewAngles(mountable.mountAnchor.transform.rotation.eulerAngles);
                    player.eyes.NetworkUpdate(mountable.mountAnchor.transform.rotation);
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
                    mountable.SetFlag(BaseEntity.Flags.Busy, true, false);

                    if (i > 0)
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);

                    OnEntityMounted(player, i == 0);
                    return;
                }
            }

            public void DismountPlayer(BasePlayer player)
            {
                if (player == null)
                    return;

                BaseMountable mountable = player.GetMounted();
                if (mountable == null)
                    return;

                player.PauseFlyHackDetection(1f);

                Vector3 dismountPosition = mountable.dismountPositions[0].position;
               
                if (TerrainMeta.HeightMap.GetHeight(dismountPosition) > dismountPosition.y)
                    dismountPosition.y = TerrainMeta.HeightMap.GetHeight(dismountPosition) + 0.5f;

                player.DismountObject();
                player.transform.rotation = Quaternion.identity;
                player.MovePosition(dismountPosition);
                player.eyes.NetworkUpdate(Quaternion.identity);

                player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);

                player.SendNetworkUpdateImmediate(false);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", dismountPosition);
                mountable._mounted = null;

                if (player == Commander)
                {
                    entity.CancelInvoke(DrawTargeting);
                    Commander = null;
                }

                DestroyAllUI(player);
            }

            private void DismountAll()
            {
                for (int i = 0; i < mountables.Length; i++)
                {
                    BasePlayer player = mountables[i].GetMounted();
                    if (player != null)
                        DismountPlayer(player);
                }

                Commander = null;
            }

            private void OnEntityMounted(BasePlayer player, bool isOperator)
            {
                if (isOperator)
                {
                    string ctrlStr = msg("controls", player.UserIDString);
                    if (configData.Weapons.Cannon.Enabled)
                        ctrlStr += $"\n{string.Format(msg("fire_cannon", player.UserIDString), configData.Buttons.Cannon)}";
                    if (configData.Weapons.Coax.Enabled)
                        ctrlStr += $"\n{string.Format(msg("fire_coax", player.UserIDString), configData.Buttons.Coax)}";
                    if (configData.Weapons.MG.Enabled)
                        ctrlStr += $"\n{string.Format(msg("fire_mg", player.UserIDString), configData.Buttons.MG)}";
                    ctrlStr += $"\n{string.Format(msg("speed_boost", player.UserIDString), configData.Buttons.Boost)}";
                    ctrlStr += $"\n{string.Format(msg("enter_exit", player.UserIDString), configData.Buttons.Enter)}";
                    ctrlStr += $"\n{string.Format(msg("toggle_lights", player.UserIDString), configData.Buttons.Lights)}";
                    if (configData.Inventory.Enabled)
                        ctrlStr += $"\n{string.Format(msg("access_inventory", player.UserIDString), configData.Buttons.Inventory)}";
                    player.ChatMessage(ctrlStr);

                    CreateHealthUI(player, this);
                    CreateMGAmmoUI(player, this);
                    CreateRocketAmmoUI(player, this);

                    Commander = player;
                    entity.InvokeRepeating(DrawTargeting, DDRAW_UPDATE_TIME, DDRAW_UPDATE_TIME);
                }
                else
                {
                    player.ChatMessage(string.Format(msg("enter_exit", player.UserIDString), configData.Buttons.Enter));
                    if (configData.Inventory.Enabled)
                        player.ChatMessage(string.Format(msg("access_inventory", player.UserIDString), configData.Buttons.Inventory));
                    CreateHealthUI(player, this);
                }
            }
            #endregion

            #region Weapons
            private void SetInitialAimDirection()
            {
                aimVector = aimVectorTop = entity.transform.forward.normalized;

                entity.AimWeaponAt(entity.mainTurret, entity.coaxPitch, aimVector, -90f, 7f, 360f, null);
                entity.AimWeaponAt(entity.mainTurret, entity.CannonPitch, aimVector, -90f, 7f, 360f, null);
                entity.AimWeaponAt(entity.topTurretYaw, entity.topTurretPitch, aimVectorTop, -360f, 360f, 360f, entity.mainTurret);

                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }

            private void DoWeaponControls()
            {                
                if (Commander.serverInput.WasJustPressed(controlButtons[CommandType.Lights]))
                    ToggleLights();

                if (Commander.serverInput.WasJustPressed(controlButtons[CommandType.Cannon]))
                    FireCannon();

                if (Commander.serverInput.IsDown(controlButtons[CommandType.MG]) || Commander.serverInput.WasJustPressed(controlButtons[CommandType.MG]))
                    FireMG();

                if (Commander.serverInput.IsDown(controlButtons[CommandType.Coax]) || Commander.serverInput.WasJustPressed(controlButtons[CommandType.Coax]))
                    FireCoax();
            }

            private void AdjustAiming()
            {                
                if (Mathf.Abs(mouseInput.x) < 0.5f)
                    mouseInput.x = 0;

                if (Mathf.Abs(mouseInput.y) < 0.5f)
                    mouseInput.y = 0;

                if (mouseInput == Vector3.zero)
                    return;

                Vector3 direction = Quaternion.Euler(entity.mainTurret.transform.eulerAngles + (new Vector3(-mouseInput.y, mouseInput.x, 0f) * 5f)) * Vector3.forward;
                
                Ray ray = new Ray(Commander.eyes.transform.position, direction);

                Vector3 hitPoint;

                if (Physics.Raycast(ray, out eyeRay, 5000f, TARGET_LAYERS))
                    hitPoint = eyeRay.point;                
                else hitPoint = ray.GetPoint(100);
                
                Vector3 desiredAim = (hitPoint - Commander.eyes.transform.position).normalized;
                Vector3 desiredAimTop = (hitPoint - entity.topTurretEyePos.transform.position).normalized;

                aimVector = entity.turretAimVector = Vector3.Lerp(aimVector, desiredAim, Time.deltaTime * 3f);
                aimVectorTop = entity.topTurretAimVector = Vector3.Lerp(aimVectorTop, desiredAimTop, Time.deltaTime * 3f);

                entity.AimWeaponAt(entity.mainTurret, entity.coaxPitch, aimVector, -90f, 7f, 360f, null);
                entity.AimWeaponAt(entity.mainTurret, entity.CannonPitch, aimVector, -90f, 7f, 360f, null);

                entity.AimWeaponAt(entity.topTurretYaw, entity.topTurretPitch, aimVectorTop, -360f, 360f, 360f, entity.mainTurret);
            }

            private const float DDRAW_UPDATE_TIME = 0.02f;

            private void DrawTargeting()
            {
                if (!configData.Weapons.EnableCrosshair || Commander == null)
                    return;

                Vector3 start = entity.CannonMuzzle.transform.position;
                Vector3 end = Vector3.zero;                  

                RaycastHit rayHit;
                if (Physics.Raycast(start, entity.CannonMuzzle.forward, out rayHit, 300, 1219701521, QueryTriggerInteraction.Ignore))                
                    end = rayHit.point;                
                else end = start + (entity.CannonMuzzle.forward * 300);
                
                if (Commander.IsAdmin)
                {
                    Commander.SendConsoleCommand("ddraw.text", DDRAW_UPDATE_TIME + 0.01f, configData.Weapons.CrosshairColor.Color, end, $"<size={configData.Weapons.CrosshairSize}>⊕</size>");
                }
                else
                {
                    Commander.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    Commander.SendNetworkUpdateImmediate();
                    Commander.SendConsoleCommand("ddraw.text", DDRAW_UPDATE_TIME + 0.01f, configData.Weapons.CrosshairColor.Color, end, $"<size={configData.Weapons.CrosshairSize}>⊕</size>");
                    Commander.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                }            
            }

            private void FireCannon()
            {
                if (cannon.RequireAmmo)
                {
                    if (inventory.itemList.Find(x => x.info.shortname == cannon.Type) == null)
                    {
                        if (ItemManager.itemDictionaryByName.ContainsKey(cannon.Type))
                            Commander.ChatMessage(string.Format(msg("no_ammo_cannon", Commander.UserIDString), ItemManager.itemDictionaryByName[cannon.Type].displayName.english));
                        else print($"Invalid ammo type for the cannon set in config: {cannon.Type}");
                        return;
                    }
                }
                if (Time.realtimeSinceStartup >= lastFireCannon)
                {
                    Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(cannon.Accuracy, entity.CannonMuzzle.rotation * Vector3.forward, true);
                    Vector3 cannonPitch = (entity.CannonPitch.transform.rotation * Vector3.back) + (entity.transform.up * -1f);

                    entity.myRigidBody.AddForceAtPosition(cannonPitch.normalized * entity.recoilScale, entity.CannonPitch.transform.position, ForceMode.Impulse);

                    Effect.server.Run(entity.mainCannonMuzzleFlash.resourcePath, entity, StringPool.Get(entity.CannonMuzzle.gameObject.name), Vector3.zero, Vector3.zero, null, false);

                    BaseEntity rocket = GameManager.server.CreateEntity(entity.mainCannonProjectile.resourcePath, entity.CannonMuzzle.transform.position, Quaternion.LookRotation(modifiedAimConeDirection), true);
                    ServerProjectile projectile = rocket.GetComponent<ServerProjectile>();
                    projectile.InitializeVelocity(modifiedAimConeDirection.normalized * projectile.speed);
                    rocket.Spawn();

                    TimedExplosive explosive = rocket.GetComponent<TimedExplosive>();
                    if (explosive != null)
                        explosive.damageTypes.Add(new DamageTypeEntry { amount = cannon.Damage, type = DamageType.Explosion });

                    lastFireCannon = Time.realtimeSinceStartup + cannon.Interval;

                    if (cannon.RequireAmmo)
                        inventory.itemList.Find(x => x.info.shortname == cannon.Type)?.UseItem(1);
                    CreateRocketAmmoUI(Commander, this);
                }
            }

            private void FireCoax()
            {
                if (coax.RequireAmmo)
                {
                    if (inventory.itemList.Find(x => x.info.shortname == coax.Type) == null)
                    {
                        if (ItemManager.itemDictionaryByName.ContainsKey(coax.Type))
                            Commander.ChatMessage(string.Format(msg("no_ammo_coax", Commander.UserIDString), ItemManager.itemDictionaryByName[coax.Type].displayName.english));
                        else print($"Invalid ammo type for the coaxial gun set in config: {coax.Type}");
                        return;
                    }
                }
                if (Time.realtimeSinceStartup >= lastFireCoax)
                {
                    Vector3 vector3 = entity.coaxMuzzle.transform.position - (entity.coaxMuzzle.forward * 0.25f);

                    Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(coax.Accuracy, entity.coaxMuzzle.transform.forward, true);
                    Vector3 targetPos = vector3 + (modifiedAimConeDirection * 300f);

                    List<RaycastHit> list = Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAll(new Ray(vector3, modifiedAimConeDirection), 0f, list, 300f, 1219701521, QueryTriggerInteraction.UseGlobal);
                    for (int i = 0; i < list.Count; i++)
                    {
                        RaycastHit hit = list[i];
                        BaseEntity hitEntity = hit.GetEntity();
                        if (hitEntity != null && hitEntity != this.entity)
                        {
                            BaseCombatEntity baseCombatEntity = hitEntity as BaseCombatEntity;
                            if (baseCombatEntity != null)
                                ApplyDamage(baseCombatEntity, coax.Damage, hit.point, modifiedAimConeDirection);

                            if (hitEntity.ShouldBlockProjectiles())
                            {
                                targetPos = hit.point;
                                break;
                            }
                        }
                    }

                    entity.ClientRPC(null, "CLIENT_FireGun", true, targetPos);
                    Pool.FreeList<RaycastHit>(ref list);

                    lastFireCoax = Time.realtimeSinceStartup + coax.Interval;

                    if (coax.RequireAmmo)
                        inventory.itemList.Find(x => x.info.shortname == coax.Type)?.UseItem(1);
                    CreateMGAmmoUI(Commander, this);
                }
            }

            private void FireMG()
            {
                if (mg.RequireAmmo)
                {
                    if (inventory.itemList.Find(x => x.info.shortname == mg.Type) == null)
                    {
                        if (ItemManager.itemDictionaryByName.ContainsKey(mg.Type))
                            Commander.ChatMessage(string.Format(msg("no_ammo_mg", Commander.UserIDString), ItemManager.itemDictionaryByName[mg.Type].displayName.english));
                        else print($"Invalid ammo type for the machine gun set in config: {mg.Type}");
                        return;
                    }
                }
                if (Time.realtimeSinceStartup >= lastFireMG)
                {
                    Vector3 firePosition = entity.topTurretMuzzle.transform.position - (entity.topTurretMuzzle.forward * 0.25f);

                    Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(mg.Accuracy, entity.topTurretMuzzle.transform.forward, true);
                    Vector3 targetPos = firePosition + modifiedAimConeDirection;

                    List<RaycastHit> list = Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAll(new Ray(firePosition, modifiedAimConeDirection), 0f, list, 300f, 1219701521, QueryTriggerInteraction.UseGlobal);
                    for (int i = 0; i < list.Count; i++)
                    {
                        RaycastHit hit = list[i];
                        BaseEntity hitEntity = hit.GetEntity();
                        if (hitEntity != null && !hitEntity == this.entity)
                        {
                            BaseCombatEntity baseCombatEntity = hitEntity as BaseCombatEntity;
                            if (baseCombatEntity != null)
                                ApplyDamage(baseCombatEntity, mg.Damage, hit.point, modifiedAimConeDirection);

                            if (hitEntity.ShouldBlockProjectiles())
                            {
                                targetPos = hit.point;
                                break;
                            }
                        }
                    }

                    entity.ClientRPC(null, "CLIENT_FireGun", false, targetPos);
                    Pool.FreeList<RaycastHit>(ref list);

                    lastFireMG = Time.realtimeSinceStartup + mg.Interval;

                    if (mg.RequireAmmo)
                        inventory.itemList.Find(x => x.info.shortname == mg.Type)?.UseItem(1);
                    CreateMGAmmoUI(Commander, this);
                }
            }

            private void FireSideGuns()
            {

            }

            private void ApplyDamage(BaseCombatEntity hitEntity, float damage, Vector3 point, Vector3 normal)
            {
                float single = damage * UnityEngine.Random.Range(0.9f, 1.1f);
                hitEntity.OnAttacked(new HitInfo(this.entity, hitEntity, DamageType.Bullet, single, point));
                if (hitEntity is BasePlayer || hitEntity is BaseNpc)
                {
                    HitInfo hitInfo = new HitInfo()
                    {
                        HitPositionWorld = point,
                        HitNormalWorld = -normal,
                        HitMaterial = StringPool.Get("Flesh")
                    };
                    Effect.server.ImpactEffect(hitInfo);
                }
            }
            #endregion

            #region Movement
            private void DoMovementControls()
            {
                float accelerate = Commander.serverInput.IsDown(BUTTON.FORWARD) ? 1f : Commander.serverInput.IsDown(BUTTON.BACKWARD) ? -1f : 0f;
                float steer = Commander.serverInput.IsDown(BUTTON.RIGHT) ? 1f : Commander.serverInput.IsDown(BUTTON.LEFT) ? -1f : 0f;

                bool boost = Commander.serverInput.IsDown(controlButtons[CommandType.Boost]);

                SetThrottleSpeed(accelerate, steer, boost);
            }

            private void SetThrottleSpeed(float acceleration, float steering, bool boost)
            {
                if (acceleration == 0 && steering == 0)
                {
                    ApplyBrakes(0.5f);

                    if (accelTimeTaken > 0)
                        accelTimeTaken = Mathf.Clamp(accelTimeTaken -= (Time.deltaTime * 2), 0, accelTimeToTake);
                }
                else
                {
                    ApplyBrakes(0f);

                    accelTimeTaken += Time.deltaTime;
                    float engineRpm = Mathf.InverseLerp(0f, accelTimeToTake, accelTimeTaken);

                    float throttle = Mathf.InverseLerp(0f, 1f, engineRpm);

                    float leftTrack = 0;
                    float rightTrack = 0;
                    float torque = 0;

                    if (acceleration > 0)
                    {
                        torque = forwardTorque;
                        leftTrack = 1f;
                        rightTrack = 1f;
                    }
                    else if (acceleration < 0)
                    {
                        torque = forwardTorque;
                        leftTrack = -1f;
                        rightTrack = -1f;
                    }
                    if (steering > 0)
                    {
                        if (acceleration == 0)
                        {
                            torque = turnTorque;
                            leftTrack = 1f;
                            rightTrack = -1f;
                        }
                        else
                        {
                            torque = (forwardTorque + turnTorque) * 0.75f;
                            rightTrack *= 0.5f;
                        }
                    }
                    else if (steering < 0)
                    {
                        if (acceleration == 0)
                        {
                            torque = turnTorque;
                            leftTrack = -1f;
                            rightTrack = 1f;
                        }
                        else
                        {
                            torque = (forwardTorque + turnTorque) * 0.75f;
                            leftTrack *= 0.5f;
                        }
                    }

                    if (boost)
                    {
                        if (torque > 0)
                            torque += configData.Movement.BoostTorque;
                        if (torque < 0)
                            torque -= configData.Movement.BoostTorque;
                    }

                    float sidewaysVelocity = Mathf.InverseLerp(5f, 1.5f, rigidBody.velocity.magnitude * Mathf.Abs(Vector3.Dot(rigidBody.velocity.normalized, entity.transform.forward)));
                    entity.ScaleSidewaysFriction(1f - sidewaysVelocity);

                    ApplyMotorTorque(Mathf.Clamp(leftTrack * throttle, -1f, 1f) * torque, false);
                    ApplyMotorTorque(Mathf.Clamp(rightTrack * throttle, -1f, 1f) * torque, true);
                }
            }

            private void ApplyBrakes(float amount)
            {
                amount = Mathf.Clamp(maxBrakeTorque * amount, 0, maxBrakeTorque);
                ApplyBrakeTorque(amount, true);
                ApplyBrakeTorque(amount, false);
            }

            private void ApplyBrakeTorque(float amount, bool rightSide)
            {
                WheelCollider[] wheelColliderArray = (!rightSide ? leftWheels : rightWheels);

                for (int i = 0; i < wheelColliderArray.Length; i++)
                    wheelColliderArray[i].brakeTorque = maxBrakeTorque * amount;
            }

            private void ApplyMotorTorque(float torque, bool rightSide)
            {
                WheelCollider[] wheelColliderArray = (!rightSide ? leftWheels : rightWheels);

                for (int i = 0; i < wheelColliderArray.Length; i++)
                    wheelColliderArray[i].motorTorque = torque;
            }
            #endregion
            
            private void ToggleLights() => entity.SetFlag(BaseEntity.Flags.Reserved5, !entity.HasFlag(BaseEntity.Flags.Reserved5), false);

            public void ManageDamage(HitInfo info)
            {
                if (isDying)
                    return;

                if (info.damageTypes.Total() >= entity.health)
                {
                    info.damageTypes = new DamageTypeList();
                    info.HitEntity = null;
                    info.HitMaterial = 0;
                    info.PointStart = Vector3.zero;

                    OnDeath();
                }
                else
                {
                    ins.NextTick(() =>
                    {
                        if (Commander != null)
                            CreateHealthUI(Commander, this);

                        for (int i = 1; i < mountables.Length; i++)
                        {
                            BasePlayer player = mountables[i]?.GetMounted();
                            if (player != null)
                                CreateHealthUI(player, this);
                        }
                    });
                }
            }

            private void OnDeath()
            {
                isDying = true;

                DismountAll();

                Effect.server.Run(entity.explosionEffect.resourcePath, entity.transform.position, Vector3.up, null, true);

                List<ServerGib> serverGibs = ServerGib.CreateGibs(entity.servergibs.resourcePath, entity.gameObject, entity.servergibs.Get().GetComponent<ServerGib>()._gibSource, Vector3.zero, 3f);
                for (int i = 0; i < 12 - entity.maxCratesToSpawn; i++)
                {
                    BaseEntity fireBall = GameManager.server.CreateEntity(entity.fireBall.resourcePath, entity.transform.position, entity.transform.rotation, true);
                    if (fireBall)
                    {
                        Vector3 onSphere = UnityEngine.Random.onUnitSphere;
                        fireBall.transform.position = (entity.transform.position + new Vector3(0f, 1.5f, 0f)) + (onSphere * UnityEngine.Random.Range(-4f, 4f));
                        Collider collider = fireBall.GetComponent<Collider>();
                        fireBall.Spawn();
                        fireBall.SetVelocity(Vector3.zero + (onSphere * UnityEngine.Random.Range(3, 10)));
                        foreach (ServerGib serverGib in serverGibs)
                            Physics.IgnoreCollision(collider, serverGib.GetCollider(), true);
                    }
                }

                if (configData.Inventory.DropInv)
                {
                    inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab", (entity.transform.position + new Vector3(0f, 1.5f, 0f)) + (UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 3f)), new Quaternion(), 0f);
                }
                
                if (configData.Inventory.DropLoot)
                {
                    for (int j = 0; j < entity.maxCratesToSpawn; j++)
                    {
                        Vector3 onSphere = UnityEngine.Random.onUnitSphere;
                        BaseEntity lootCrate = GameManager.server.CreateEntity(entity.crateToDrop.resourcePath, (entity.transform.position + new Vector3(0f, 1.5f, 0f)) + (onSphere * UnityEngine.Random.Range(2f, 3f)), Quaternion.LookRotation(onSphere), true);
                        lootCrate.Spawn();

                        LootContainer lootContainer = lootCrate as LootContainer;
                        if (lootContainer)
                            lootContainer.Invoke(new Action(lootContainer.RemoveMe), 1800f);

                        Collider collider = lootCrate.GetComponent<Collider>();
                        Rigidbody rigidbody = lootCrate.gameObject.AddComponent<Rigidbody>();
                        rigidbody.useGravity = true;
                        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rigidbody.mass = 2f;
                        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                        rigidbody.velocity = Vector3.zero + (onSphere * UnityEngine.Random.Range(1f, 3f));
                        rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                        rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                        rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);

                        FireBall fireBall = GameManager.server.CreateEntity(entity.fireBall.resourcePath, lootCrate.transform.position, new Quaternion(), true) as FireBall;
                        if (fireBall)
                        {
                            fireBall.transform.position = lootCrate.transform.position;
                            fireBall.Spawn();
                            fireBall.GetComponent<Rigidbody>().isKinematic = true;
                            fireBall.GetComponent<Collider>().enabled = false;
                            fireBall.transform.parent = lootCrate.transform;
                        }
                        lootCrate.SendMessage("SetLockingEnt", fireBall.gameObject, SendMessageOptions.DontRequireReceiver);

                        foreach (ServerGib serverGib1 in serverGibs)
                            Physics.IgnoreCollision(collider, serverGib1.GetCollider(), true);
                    }
                }
                if (entity != null && !entity.IsDestroyed)
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("spawntank")]
        void cmdTank(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "tankcommander.admin")) return;

            Vector3 position = player.eyes.position + (player.eyes.MovementForward() * 5f);

            BaseEntity entity = GameManager.server.CreateEntity(APC_PREFAB, position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0));
            entity.enableSaving = false;
            entity.Spawn();

            controllers.Add(entity.gameObject.AddComponent<APCController>());
        }

        [ConsoleCommand("spawntank")]
        void ccmdSpawnTank(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            if (arg.Args.Length == 1)
            {
                BasePlayer player = covalence.Players.Connected.FirstOrDefault(x => x.Id == arg.GetString(0))?.Object as BasePlayer;
                if (player != null)
                {
                    Vector3 position = player.eyes.position + (player.eyes.MovementForward() * 5f);

                    BaseEntity entity = GameManager.server.CreateEntity(APC_PREFAB, position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0));
                    entity.enableSaving = false;
                    entity.Spawn();

                    controllers.Add(entity.gameObject.AddComponent<APCController>());
                }
                return;
            }
            if (arg.Args.Length == 3)
            {
                float x;
                float y;
                float z;

                if (float.TryParse(arg.GetString(0), out x))
                {
                    if (float.TryParse(arg.GetString(1), out y))
                    {
                        if (float.TryParse(arg.GetString(2), out z))
                        {
                            BaseEntity entity = GameManager.server.CreateEntity(APC_PREFAB, new Vector3(x, y, z));
                            entity.enableSaving = false;
                            entity.Spawn();

                            controllers.Add(entity.gameObject.AddComponent<APCController>());
                            return;
                        }
                    }
                }
                PrintError($"Invalid arguments supplied to spawn a tank at position : (x = {arg.GetString(0)}, y = {arg.GetString(1)}, z = {arg.GetString(2)})");
            }
        }
        #endregion

        #region Friends
        private bool AreFriends(ulong playerId, ulong friendId)
        {
            if (Friends && configData.Passengers.UseFriends)
                return (bool)Friends?.Call("AreFriendsS", playerId.ToString(), friendId.ToString());
            return true;
        }
        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (Clans && configData.Passengers.UseClans)
            {
                object playerTag = Clans?.Call("GetClanOf", playerId);
                object friendTag = Clans?.Call("GetClanOf", friendId);
                if (playerTag is string && friendTag is string)
                {
                    if (!string.IsNullOrEmpty((string)playerTag) && !string.IsNullOrEmpty((string)friendTag) && (playerTag == friendTag))
                        return true;
                }
                return false;
            }
            return true;
        }
        #endregion

        #region UI
        #region UI Elements
        public static class UI
        {
            static public CuiElementContainer ElementContainer(string panelName, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }
            static public void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "droidsansmono.ttf" },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);

            }
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        public class UI4
        {
            public float xMin, yMin, xMax, yMax;
            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }
            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";
        }
        #endregion

        #region UI Creation
        private static void CreateHealthUI(BasePlayer player, APCController controller)
        {
            if (player == null)
                return;

            CuiElementContainer container = UI.ElementContainer(UI_HEALTH, "0.95 0.95 0.95 0.05", new UI4(0.69f, 0.1f, 0.83f, 0.135f));
            UI.Label(ref container, UI_HEALTH, $"HLTH: ", 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
            double percentHealth = System.Convert.ToDouble((float)controller.entity.health / (float)controller.entity.MaxHealth());
            float yMaxHealth = 0.25f + (0.73f * (float)percentHealth);
            UI.Panel(ref container, UI_HEALTH, UI.Color("#ce422b", 0.6f), new UI4(0.25f, 0.1f, yMaxHealth, 0.9f));
            DestroyUI(player, UI_HEALTH);
            CuiHelper.AddUi(player, container);
        }

        private static void CreateMGAmmoUI(BasePlayer player, APCController controller)
        {
            if (player == null)
                return;

            if (configData.Weapons.MG.Enabled && configData.Weapons.MG.RequireAmmo)
            {
                CuiElementContainer container = UI.ElementContainer(UI_AMMO_MG, "0.95 0.95 0.95 0.05", new UI4(0.69f, 0.060f, 0.83f, 0.096f));
                UI.Label(ref container, UI_AMMO_MG, $"MGUN: <color=#ce422b>{controller.inventory.GetAmount(ins.mgId, false)}</color>", 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
                DestroyUI(player, UI_AMMO_MG);
                CuiHelper.AddUi(player, container);
            }
        }

        private static void CreateRocketAmmoUI(BasePlayer player, APCController controller)
        {
            if (player == null)
                return;

            if (configData.Weapons.Cannon.Enabled && configData.Weapons.Cannon.RequireAmmo)
            {
                CuiElementContainer container = UI.ElementContainer(UI_AMMO_ROCKET, "0.95 0.95 0.95 0.05", new UI4(0.69f, 0.021f, 0.83f, 0.056f));
                UI.Label(ref container, UI_AMMO_ROCKET, $"CNON: <color=#ce422b>{controller.inventory.GetAmount(ins.rocketId, false)}</color>", 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
                DestroyUI(player, UI_AMMO_ROCKET);
                CuiHelper.AddUi(player, container);
            }
        }

        private static void DestroyUI(BasePlayer player, string panel) => CuiHelper.DestroyUi(player, panel);

        private static void DestroyAllUI(BasePlayer player)
        {
            DestroyUI(player, UI_HEALTH);
            DestroyUI(player, UI_AMMO_MG);
            DestroyUI(player, UI_AMMO_ROCKET);
        }
        #endregion
        #endregion

        #region Config
        private static ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Movement Settings")]
            public MovementSettings Movement { get; set; }

            [JsonProperty(PropertyName = "Button Configuration")]
            public ButtonConfiguration Buttons { get; set; }

            [JsonProperty(PropertyName = "Crushable Types")]
            public CrushableTypes Crushables { get; set; }

            [JsonProperty(PropertyName = "Passenger Options")]
            public PassengerOptions Passengers { get; set; }

            [JsonProperty(PropertyName = "Inventory Options")]
            public InventoryOptions Inventory { get; set; }

            [JsonProperty(PropertyName = "Weapon Options")]
            public WeaponOptions Weapons { get; set; }

            public class CrushableTypes
            {
                [JsonProperty(PropertyName = "Can crush buildings")]
                public bool Buildings { get; set; }

                [JsonProperty(PropertyName = "Can crush resources")]
                public bool Resources { get; set; }

                [JsonProperty(PropertyName = "Can crush loot containers")]
                public bool Loot { get; set; }

                [JsonProperty(PropertyName = "Can crush animals")]
                public bool Animals { get; set; }

                [JsonProperty(PropertyName = "Can crush players")]
                public bool Players { get; set; }

                [JsonProperty(PropertyName = "Amount of force required to crush various building grades")]
                public Dictionary<string, float> GradeForce { get; set; }

                [JsonProperty(PropertyName = "Amount of force required to crush external walls")]
                public float WallForce { get; set; }

                [JsonProperty(PropertyName = "Amount of force required to crush resources")]
                public float ResourceForce { get; set; }
            }

            public class ButtonConfiguration
            {
                [JsonProperty(PropertyName = "Enter/Exit vehicle")]
                public string Enter { get; set; }

                [JsonProperty(PropertyName = "Toggle light")]
                public string Lights { get; set; }

                [JsonProperty(PropertyName = "Open inventory")]
                public string Inventory { get; set; }

                [JsonProperty(PropertyName = "Speed boost")]
                public string Boost { get; set; }

                [JsonProperty(PropertyName = "Fire Cannon")]
                public string Cannon { get; set; }

                [JsonProperty(PropertyName = "Fire Coaxial Gun")]
                public string Coax { get; set; }

                [JsonProperty(PropertyName = "Fire MG")]
                public string MG { get; set; }
            }

            public class MovementSettings
            {
                [JsonProperty(PropertyName = "Forward torque (nm)")]
                public float ForwardTorque { get; set; }

                [JsonProperty(PropertyName = "Rotation torque (nm)")]
                public float TurnTorque { get; set; }

                [JsonProperty(PropertyName = "Brake torque (nm)")]
                public float BrakeTorque { get; set; }

                [JsonProperty(PropertyName = "Time to reach maximum acceleration (seconds)")]
                public float Acceleration { get; set; }

                [JsonProperty(PropertyName = "Boost torque (nm)")]
                public float BoostTorque { get; set; }
            }
            public class PassengerOptions
            {
                [JsonProperty(PropertyName = "Allow passengers")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Number of allowed passengers (Max 4)")]
                public int Max { get; set; }

                [JsonProperty(PropertyName = "Require passenger to be a friend (FriendsAPI)")]
                public bool UseFriends { get; set; }

                [JsonProperty(PropertyName = "Require passenger to be a clan mate (Clans)")]
                public bool UseClans { get; set; }
            }

            public class InventoryOptions
            {
                [JsonProperty(PropertyName = "Enable inventory system")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Drop inventory on death")]
                public bool DropInv { get; set; }

                [JsonProperty(PropertyName = "Drop loot on death")]
                public bool DropLoot { get; set; }

                [JsonProperty(PropertyName = "Inventory size (max 36)")]
                public int Size { get; set; }
            }

            public class WeaponOptions
            {
                [JsonProperty(PropertyName = "Cannon")]
                public WeaponSystem Cannon { get; set; }

                [JsonProperty(PropertyName = "Coaxial")]
                public WeaponSystem Coax { get; set; }

                [JsonProperty(PropertyName = "Machine Gun")]
                public WeaponSystem MG { get; set; }

                [JsonProperty(PropertyName = "Enable Crosshair")]
                public bool EnableCrosshair { get; set; }

                [JsonProperty(PropertyName = "Crosshair Color")]
                public SerializedColor CrosshairColor { get; set; }

                [JsonProperty(PropertyName = "Crosshair Size")]
                public int CrosshairSize { get; set; }

                public class SerializedColor
                {
                    public float R { get; set; }
                    public float G { get; set; }
                    public float B { get; set; }
                    public float A { get; set; }

                    private Color _color;
                    private bool _isInit;

                    public SerializedColor(float r, float g, float b, float a)
                    {
                        R = r;
                        G = g;
                        B = b;
                        A = a;
                    }

                    [JsonIgnore]
                    public Color Color
                    {
                        get
                        {
                            if (!_isInit)
                            {
                                _color = new Color(R, G, B, A);
                                _isInit = true;
                            }
                            return _color;
                        }
                    }
                }

                public class WeaponSystem
                {
                    [JsonProperty(PropertyName = "Enable weapon system")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Require ammunition in inventory")]
                    public bool RequireAmmo { get; set; }

                    [JsonProperty(PropertyName = "Ammunition type (item shortname)")]
                    public string Type { get; set; }

                    [JsonProperty(PropertyName = "Fire rate (seconds)")]
                    public float Interval { get; set; }

                    [JsonProperty(PropertyName = "Aim cone (smaller number is more accurate)")]
                    public float Accuracy { get; set; }

                    [JsonProperty(PropertyName = "Damage")]
                    public float Damage { get; set; }
                }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Buttons = new ConfigData.ButtonConfiguration
                {
                    Enter = "USE",
                    Lights = "RELOAD",
                    Inventory = "RELOAD",
                    Boost = "SPRINT",
                    Cannon = "FIRE_PRIMARY",
                    Coax = "FIRE_SECONDARY",
                    MG = "FIRE_THIRD"
                },
                Crushables = new ConfigData.CrushableTypes
                {
                    Animals = true,
                    Buildings = true,
                    Loot = true,
                    Players = true,
                    Resources = true,
                    GradeForce = new Dictionary<string, float>
                    {
                        [BuildingGrade.Enum.Twigs.ToString()] = 1000f,
                        [BuildingGrade.Enum.Wood.ToString()] = 2000f,
                        [BuildingGrade.Enum.Stone.ToString()] = 3000f,
                        [BuildingGrade.Enum.Metal.ToString()] = 5000f,
                        [BuildingGrade.Enum.TopTier.ToString()] = 7000f,
                    },
                    ResourceForce = 1500f,
                    WallForce = 3000f
                },
                Movement = new ConfigData.MovementSettings
                {
                    Acceleration = 3f,
                    BrakeTorque = 50f,
                    ForwardTorque = 1500f,
                    TurnTorque = 1800f,
                    BoostTorque = 300f
                },
                Passengers = new ConfigData.PassengerOptions
                {
                    Enabled = true,
                    Max = 4,
                    UseClans = true,
                    UseFriends = true
                },
                Inventory = new ConfigData.InventoryOptions
                {
                    Enabled = true,
                    Size = 36,
                    DropInv = true,
                    DropLoot = false
                },
                Weapons = new ConfigData.WeaponOptions
                {
                    EnableCrosshair = true,
                    CrosshairColor = new ConfigData.WeaponOptions.SerializedColor(0.75f, 0.75f, 0.75f, 0.75f),
                    CrosshairSize = 40,
                    Cannon = new ConfigData.WeaponOptions.WeaponSystem
                    {
                        Accuracy = 0.025f,
                        Damage = 90f,
                        Enabled = true,
                        Interval = 1.75f,
                        RequireAmmo = false,
                        Type = "ammo.rocket.hv"
                    },
                    Coax = new ConfigData.WeaponOptions.WeaponSystem
                    {
                        Accuracy = 0.75f,
                        Damage = 10f,
                        Enabled = true,
                        Interval = 0.06667f,
                        RequireAmmo = false,
                        Type = "ammo.rifle.hv"
                    },
                    MG = new ConfigData.WeaponOptions.WeaponSystem
                    {
                        Accuracy = 1.25f,
                        Damage = 10f,
                        Enabled = true,
                        Interval = 0.1f,
                        RequireAmmo = false,
                        Type = "ammo.rifle.hv"
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(0, 2, 0))
                configData = baseConfig;

            if (configData.Version < new VersionNumber(0, 2, 2))
            {
                configData.Weapons.EnableCrosshair = true;
                configData.Weapons.CrosshairColor = new ConfigData.WeaponOptions.SerializedColor(0.75f, 0.75f, 0.75f, 0.75f);
                configData.Weapons.CrosshairSize = 40;
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization
        private static string msg(string key, string playerId = null) => ins.lang.GetMessage(key, ins, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["in_use"] = "<color=#D3D3D3>This tank is already in use</color>",
            ["not_friend"] = "<color=#D3D3D3>You must be a friend or clanmate with the operator</color>",
            ["passenger_enter"] = "<color=#D3D3D3>You have entered the tank as a passenger</color>",
            ["controls"] = "<color=#ce422b>Tank Controls:</color>",
            ["fire_cannon"] = "<color=#D3D3D3>Fire Cannon </color><color=#ce422b>{0}</color>",
            ["fire_coax"] = "<color=#D3D3D3>Fire Coaxial Gun </color><color=#ce422b>{0}</color>",
            ["fire_mg"] = "<color=#D3D3D3>Fire MG </color><color=#ce422b>{0}</color>",
            ["speed_boost"] = "<color=#D3D3D3>Speed Boost </color><color=#ce422b>{0}</color>",
            ["enter_exit"] = "<color=#D3D3D3>Enter/Exit Vehicle </color><color=#ce422b>{0}</color>",
            ["toggle_lights"] = "<color=#D3D3D3>Toggle Lights </color><color=#ce422b>{0}</color>",
            ["access_inventory"] = "<color=#D3D3D3>Access Inventory (from outside of the vehicle) </color><color=#ce422b>{0}</color>",
            ["no_ammo_cannon"] = "<color=#D3D3D3>You do not have ammunition to fire the cannon. It requires </color><color=#ce422b>{0}</color>",
            ["no_ammo_mg"] = "<color=#D3D3D3>You do not have ammunition to fire the machine gun. It requires </color><color=#ce422b>{0}</color>",
            ["no_ammo_coax"] = "<color=#D3D3D3>You do not have ammunition to fire the coaxial gun. It requires </color><color=#ce422b>{0}</color>",
        };
        #endregion
    }
}