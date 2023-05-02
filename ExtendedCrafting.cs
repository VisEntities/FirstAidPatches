using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Extended Crafting", "nivex & judess69er", "0.3.6")]
    [Description("Allows players to deploy additional entities.")]
    class ExtendedCrafting : RustPlugin
    {
		#region Engine
        private static Dictionary<ulong, ExtendedController> _controllers = new Dictionary<ulong, ExtendedController>();
        private const int _layerMask = Layers.Mask.Terrain | Layers.Mask.World | Layers.Mask.Construction;
        private List<int> _layers = new List<int> { (int)Layer.World, (int)Layer.Terrain };
        private const string permAllow = "extendedcrafting.allow";
        private const string permFree = "extendedcrafting.free";
        private Dictionary<ulong, ExtendedInfo> _skins = new Dictionary<ulong, ExtendedInfo>();
        private static ExtendedCrafting Instance;
        private RaycastHit _hit;

        public class ExtendedController : FacepunchBehaviour
        {
            internal BaseCombatEntity entity;
            internal RaycastHit hit;
            internal ExtendedInfo ei;
            internal ulong uid;

            private void Awake()
            {
                entity = GetComponent<BaseCombatEntity>();
                _controllers[uid = entity.net.ID.Value] = this;
                ei = Instance._skins[entity.skinID];
                if (ei.destroy.ground) InvokeRepeating(CheckGround, 5f, 5f);
            }

            private void CheckGround()
            {
                var position = entity.transform.position;

                if (!Physics.Raycast(position + new Vector3(0f, 0.1f, 0f), Vector3.down, out hit, 4f, _layerMask) || hit.distance > 0.2f)
                {
                    if (ei.destroy.give)
                    {
                        ei.GiveItem(position);
                    }

                    foreach (var effect in ei.destroy.effects)
                    {
                        Effect.server.Run(effect, position);
                    }

                    CancelInvoke(CheckGround);
                    entity.Kill();
                }
            }

            public void TryPickup(BasePlayer player)
            {
                if (!ei.pickup.enabled)
                {
                    Message(player, "Disabled");
                    return;
                }

                if (ei.pickup.privilege && !player.CanBuild())
                {
                    Message(player, "Build");
                    return;
                }

                if (ei.pickup.owner && entity.OwnerID != player.userID)
                {
                    Message(player, "Owner");
                    return;
                }

                if (entity.SecondsSinceDealtDamage < 30f)
                {
                    Message(player, "Damaged");
                    return;
                }

                var ice = entity as IItemContainerEntity;

                if (ice?.inventory != null)
                {
                    DropUtil.DropItems(ice.inventory, entity.transform.position + Vector3.up);
                }

                entity.Invoke(entity.KillMessage, 0.01f);
                ei.GiveItem(player, true);
            }

            private void OnDestroy()
            {
                _controllers?.Remove(uid);
                Destroy(this);
            }
        }

        #region Hooks

        private void Init()
        {
            Instance = this;
            Unsubscribe(nameof(CanBuild));
            permission.RegisterPermission(permAllow, this);
            permission.RegisterPermission(permFree, this);
            _controllers = new Dictionary<ulong, ExtendedController>();
        }

        private void OnServerInitialized(bool isStartup)
        {
            config.entities.Values.ToList().ForEach(ei =>
            {
                _skins[ei.item.skin] = ei;

                var ci = ei.craft.items.FirstOrDefault(x => x.definition == null);

                if (ci != null)
                {
                    ei.craft.enabled = false;

                    Puts("Invalid item {0} configured for '{1}'", ci.shortname, ei.itemname);
                }

                if (ei.land || ei.distance > 0)
                {
                    Subscribe(nameof(CanBuild));
                }
            });

            foreach (var entity in BaseNetworkable.serverEntities.OfType<BaseEntity>())
            {
                if (entity.OwnerID != 0uL && _skins.ContainsKey(entity.skinID))
                {
                    entity.gameObject.AddComponent<ExtendedController>();
                }
            }
        }

        private void Unload()
        {
            foreach (var controller in _controllers.ToList())
            {
                UnityEngine.Object.Destroy(controller.Value);
            }
			
            Instance = null;
            _controllers = null;
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var entity = go.ToBaseEntity();

            config.entities.Values.FirstOrDefault(x => x.item.skin == entity.skinID)?.Spawn(entity);
        }

        private void OnHammerHit(BasePlayer player, HitInfo hitInfo)
        {
            var entity = hitInfo.HitEntity;

            if (entity == null || entity.IsDestroyed || !_skins.ContainsKey(entity.skinID))
            {
                return;
            }

            ExtendedController controller;
            if (_controllers.TryGetValue(entity.net.ID.Value, out controller) && !controller.IsInvoking("TryPickup"))
            {
                controller.Invoke(() => controller.TryPickup(player), 0.25f);
            }
        }

        private object CanBuild(Planner planner, Construction construction, Construction.Target target)
        {
            Item item = planner.GetItem();
            ExtendedInfo ei;

            if (!_skins.TryGetValue(item.skin, out ei))
            {
                return null;
            }

            var buildPos = target.entity && target.entity.transform && target.socket ? target.GetWorldPosition() : target.position;

            if (ei.land && (!Physics.Raycast(buildPos + Vector3.up, Vector3.down, out _hit, 4f) || !_layers.Contains(_hit.collider.gameObject.layer)))
            {
                return false;
            }
            else if (ei.distance > 0 && Vector3.Distance(buildPos, planner.transform.position) < ei.distance)
            {
                return false;
            }

            return null;
        }

        #endregion Hooks

        #region Helpers

        private void CommandExtended(IPlayer p, string command, string[] args)
        {
            var player = p.Object as BasePlayer;

            if (args.Length == 2 && (p.IsServer || player.IsAdmin))
            {
                ExtendedInfo.GiveItem(p, command, args);
                return;
            }

            if (p.IsServer)
            {
                p.Reply($"{command} <name> <steamid>");
                return;
            }

            var ei = config.entities.Values.FirstOrDefault(m => m.command == command);

            if (ei.CanCraft(player))
            {
                ei.GiveItem(player);
            }
        }

        #endregion Helpers
		
		#endregion Engine	
        #region Configuration

        private static void Message(BasePlayer player, string key, params object[] args)
        {
            Instance.Player.Message(player, _(key, player.UserIDString, args), Instance.config.chatid);
        }

        private static string _(string key, string id = null, params object[] args)
        {
            return string.Format(Instance.lang.GetMessage(key, Instance, id), args);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have permission to use this command!",
                ["ItemAmount"] = "You are missing {0}",
                ["Pickup"] = "You picked up {0}!",
                ["Received"] = "You received {0}!",
                ["Disabled"] = "Pickup disabled!",
                ["Build"] = "You cannot do this while building blocked!",
                ["Damaged"] = "You cannot pick this up yet!",
                ["NoCraft"] = "You cannot craft this item.",
                ["Owner"] = "You are not allowed to pick this up!",
            }, this);
        }

        public class CraftItem
        {
            public string shortname;
            public int amount;
            public ulong skin;
            internal ItemDefinition _definition;

            internal int itemid => definition.itemid;
            internal string displayName => definition.displayName.english;

            public CraftItem(string shortname, int amount, ulong skin = 0uL)
            {
                this.shortname = shortname;
                this.amount = amount;
                this.skin = skin;
            }

            internal ItemDefinition definition
            {
                get
                {
                    if (_definition == null)
                    {
                        _definition = ItemManager.FindItemDefinition(shortname);
                    }

                    return _definition;
                }
            }
        }

        public class CraftSettings
        {
            [JsonProperty("items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CraftItem> items;
            public bool enabled;

            public CraftSettings(bool enabled)
            {
                this.enabled = enabled;
            }
        }

        public class DestroySettings
        {
            [JsonProperty("effects", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> effects;
            public bool give;
            public bool ground;

            public DestroySettings(bool give, bool ground)
            {
                this.give = give;
                this.ground = ground;
            }
        }

        public class PickupSettings
        {
            public bool enabled;
            public bool owner;
            public bool privilege;

            public PickupSettings(bool enabled, bool owner, bool privilege)
            {
                this.enabled = enabled;
                this.owner = owner;
                this.privilege = privilege;
            }
        }

        public class ExtendedInfo
        {
            public CraftSettings craft;
            public DestroySettings destroy;
            public PickupSettings pickup;
            public CraftItem item;
            public string prefab;
            public string itemname;
            public string command;
            public bool land;
            public float distance;

            public Item CreateItem()
            {
                Item item = ItemManager.CreateByName(this.item.shortname, this.item.amount, this.item.skin);

                if (!string.IsNullOrEmpty(itemname))
                {
                    item.name = itemname;
                }

                return item;
            }

            public static void GiveItem(IPlayer server, string command, string[] args)
            {
                var ei = Instance.config.entities.Values.FirstOrDefault(x => x.item.shortname.Equals(args[0], StringComparison.OrdinalIgnoreCase));

                if (ei == null)
                {
                    server.Reply($"{args[0]} does not exist in config");
                    server.Reply($"{command} <shortname> <steamid>");
                }

                var target = RustCore.FindPlayer(args[1]);

                if (target == null)
                {
                    server.Reply($"{args[1]} player not found");
                }
                else ei.GiveItem(target);
            }

            public void GiveItem(BasePlayer player, bool pickup = false)
            {
                Item item = CreateItem();

                player.inventory.GiveItem(item);

                Message(player, pickup ? "Pickup" : "Received", item.info.displayName.english);
            }

            public void GiveItem(Vector3 position)
            {
                CreateItem().DropAndTossUpwards(position);
            }

            public bool CanCraft(BasePlayer player)
            {
                if (!craft.enabled)
                {
                    return false;
                }

                if (!player.IPlayer.HasPermission(permAllow))
                {
                    Message(player, "NoPermission");
                    return false;
                }

                if (player.IPlayer.HasPermission(permFree))
                {
                    return true;
                }

                bool flag = true;

                foreach (var ci in craft.items)
                {
                    int amount = player.inventory.GetAmount(ci.itemid);

                    if (amount < ci.amount)
                    {
                        Message(player, _("ItemAmount", player.UserIDString, $"{ci.displayName} x{ci.amount - amount}"));
                        flag = false;
                    }
                }

                if (flag)
                {
                    craft.items.ForEach(ci => player.inventory.Take(null, ci.itemid, ci.amount));
                }

                return flag;
            }

            public void Spawn(BaseEntity other)
            {
                var entity = GameManager.server.CreateEntity(prefab, other.transform.position, other.transform.rotation);

                if (entity == null)
                {
                    return;
                }

                entity.skinID = item.skin;
                entity.OwnerID = other.OwnerID;                
                entity.Spawn();
                entity.gameObject.AddComponent<ExtendedController>();

                other.transform.position = new Vector3(0f, -500f, 0f);
                other.TransformChanged();
                other.Invoke(other.KillMessage, 0.1f);
            }
        }
        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Entities", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, ExtendedInfo> entities { get; set; } = new Dictionary<string, ExtendedInfo>();

            [JsonProperty(PropertyName = "Steam ChatID")]
            public ulong chatid { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                SaveConfig();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                LoadDefaultConfig();
            }
            config.entities.Values.ToList().ForEach(ei =>
            {
                AddCovalenceCommand(ei.command, nameof(CommandExtended));
            });
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }
		#endregion Configuration
        #region Item Tables (226)
        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                chatid = 0,
                entities = new Dictionary<string, ExtendedInfo>
                {
                    #region Deployables (20)
					
					#region Large (13)
					
                    #region Recycler
                    ["Recycler Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 500),
                                new CraftItem("metal.fragments", 5000),
                                new CraftItem("metal.refined", 50),
                                new CraftItem("gears", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("box.repair.bench", 1, 1594245394),
                        command = "recycler.craft",
                        land = false,
                        itemname = "Personal Recycler",
                        prefab = "assets/bundled/prefabs/static/recycler_static.prefab",
                    },
                    #endregion Recycler
                    #region Indoor Large Furnace
                    ["Indoor Furnace Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 500),
                                new CraftItem("stones", 500),
                                new CraftItem("wood", 600),
                                new CraftItem("lowgradefuel", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("furnace", 1, 1992717673),
                        command = "furnace.craft",
                        land = false,
                        itemname = "Indoor Large Furnace",
                        prefab = "assets/prefabs/deployable/furnace.large/furnace.large.prefab",
                    },
                    #endregion Indoor Large Furnace
                    #region Indoor Oil Refinery
                    ["Indoor Oil Refinery Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 500),
                                new CraftItem("stones", 500),
                                new CraftItem("wood", 600),
                                new CraftItem("lowgradefuel", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("furnace", 1, 1293296287),
                        command = "refinery.craft",
                        land = false,
                        itemname = "Indoor Oil Refinery",
                        prefab = "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab",
                    },
                    #endregion Indoor Oil Refinery
                    #region Advanced Water Pump
                    ["Advanced Water Pump Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 500),
                                new CraftItem("wood", 250),
                                new CraftItem("metal.fragments", 200),
                                new CraftItem("gears", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, false),
                        item = new CraftItem("electric.fuelgenerator.small", 1, 1284169891),
                        command = "waterpump.craft",
                        land = false,
                        itemname = "Advanced Water Pump",
                        prefab = "assets/prefabs/deployable/playerioents/waterpump/water.pump.deployed.prefab",
                    },
                    #endregion Advanced Water Pump
                    #region Waterwell
                    ["Waterwell Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 5000),
                                new CraftItem("metal.fragments", 1000),
                                new CraftItem("scrap", 500),
                                new CraftItem("metal.refined", 100),
                                new CraftItem("sheetmetal", 50),
                                new CraftItem("bucket.water", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("water.catcher.small", 1, 204391461),
                        command = "waterwell.craft",
                        land = false,
                        itemname = "Waterwell",
                        prefab = "assets/prefabs/deployable/water well/waterwellstatic.prefab",
                    },
                    #endregion Waterwell
                    #region Small Modular Reactor
                    ["Small Modular Reactor Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 7500),
                                new CraftItem("metal.refined", 1000),
                                new CraftItem("techparts", 250),
                                new CraftItem("sheetmetal", 150),
                                new CraftItem("metalpipe", 50),
                                new CraftItem("targeting.computer", 10),
                                new CraftItem("propanetank", 5),
                                new CraftItem("fridge", 5),
                                new CraftItem("waterpump", 3),
                                new CraftItem("computerstation", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("electric.generator.small", 1, 295829489),
                        command = "smr.craft",
                        land = false,
                        itemname = "Small Modular Reactor",
                        prefab = "assets/prefabs/deployable/playerioents/generators/generator.small.prefab",
                    },
                    #endregion Small Modular Reactor
                    #region One Sided Town Sign Post
                    ["One Sided Town Sign Post Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sign.post.town", 1, 1832422579),
                        command = "townsign1.craft",
                        land = false,
                        itemname = "One Sided Town Sign Post",
                        prefab = "assets/prefabs/deployable/signs/sign.post.town.prefab",
                    },
                    #endregion One Sided Town Sign Post
                    #region Two Sided Town Sign Post
                    ["Two Sided Town Sign Post Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                                new CraftItem("metal.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sign.post.town.roof", 1, 826309791),
                        command = "townsign2.craft",
                        land = false,
                        itemname = "Two Sided Town Sign Post",
                        prefab = "assets/prefabs/deployable/signs/sign.post.town.roof.prefab",
                    },
                    #endregion Two Sided Town Sign Post
                    #region Hobobarrel
                    ["Hobobarrel Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 150),
                                new CraftItem("metal.fragments", 100),
                                new CraftItem("sheetmetal", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hobobarrel", 1, 1442559428),
                        command = "hobobarrel.craft",
                        land = false,
                        itemname = "Hobobarrel",
                        prefab = "assets/prefabs/misc/twitch/hobobarrel/hobobarrel.deployed.prefab",
                    },
                    #endregion Hobobarrel
                    #region Car Shredder
                    ["Car Shredder Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 50000),
                                new CraftItem("scrap", 10000),
                                new CraftItem("crude.oil", 5000),
                                new CraftItem("metal.refined", 2500),
                                new CraftItem("sheetmetal", 500),
                                new CraftItem("metalblade", 500),
                                new CraftItem("metalspring", 100),
                                new CraftItem("metalpipe", 100),
                                new CraftItem("gears", 100),
                                new CraftItem("sparkplug3", 40),
                                new CraftItem("piston3", 20),
                                new CraftItem("valve3", 20),
                                new CraftItem("crankshaft3", 10),
                                new CraftItem("carburetor3", 5),
                                new CraftItem("vehicle.1mod.engine", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("abovegroundpool", 1, 352130972),
                        command = "shredder.craft",
                        land = false,
                        itemname = "Car Shredder - Rotation is 1 rotation behind",
                        prefab = "assets/content/structures/carshredder/carshredder.entity.prefab",
                    },
                    #endregion Car Shredder
                    #region Pumpjack
                    ["Pumpjack Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 25000),
                                new CraftItem("metal.fragments", 10000),
                                new CraftItem("scrap", 5000),
                                new CraftItem("metal.refined", 1500),
                                new CraftItem("sheetmetal", 100),
                                new CraftItem("gears", 75),
                                new CraftItem("metalpipe", 50),
                                new CraftItem("metalspring", 25),
                                new CraftItem("piston3", 5),
                                new CraftItem("valve3", 5),
                                new CraftItem("carburetor3", 3),
                                new CraftItem("crankshaft3", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("water.catcher.large", 1, 1130709577),
                        command = "pumpjack.craft",
                        land = false,
                        itemname = "Pumpjack",
                        prefab = "assets/bundled/prefabs/static/pumpjack-static.prefab",
                    },
                    #endregion Pumpjack
                    #region Mining Quarry
                    ["Mining Quarry Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25000),
                                new CraftItem("wood", 15000),
                                new CraftItem("scrap", 5000),
                                new CraftItem("metal.refined", 1500),
                                new CraftItem("sheetmetal", 100),
                                new CraftItem("gears", 75),
                                new CraftItem("metalpipe", 50),
                                new CraftItem("metalspring", 25),
                                new CraftItem("piston3", 5),
                                new CraftItem("valve3", 5),
                                new CraftItem("carburetor3", 3),
                                new CraftItem("crankshaft3", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("water.catcher.large", 1, 1052926200),
                        command = "quarry.craft",
                        land = false,
                        itemname = "Mining Quarry",
                        prefab = "assets/bundled/prefabs/static/miningquarry_static.prefab",
                    },
                    #endregion Mining Quarry
                    #region Clan Banner
                    ["Clan Banner Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                                new CraftItem("metal.fragments", 50),
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sign.pole.banner.large", 1, 2070189026),
                        command = "clanbanner.craft",
                        land = false,
                        itemname = "Clan Banner",
                        prefab = "assets/prefabs/deployable/signs/sign.pole.banner.large.prefab",
                    },
                    #endregion Clan Banner

					#endregion Large
                    #region Animals (6)
					
                    #region Chicken
                    ["Chicken Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("rustige_egg_d", 1, 9999999998),
                        command = "chicken.craft",
                        land = false,
                        itemname = "Chicken",
                        prefab = "assets/rust.ai/agents/chicken/chicken.prefab",
                    },
                    #endregion Chicken
                    #region Boar
                    ["Boar Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("rustige_egg_e", 1, 9999999997),
                        command = "boar.craft",
                        land = false,
                        itemname = "Boar",
                        prefab = "assets/rust.ai/agents/boar/boar.prefab",
                    },
                    #endregion Boar
                    #region Bear
                    ["Bear Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 125),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("rustige_egg_a", 1, 9999999996),
                        command = "bear.craft",
                        land = false,
                        itemname = "Bear",
                        prefab = "assets/rust.ai/agents/bear/bear.prefab",
                    },
                    #endregion Bear
                    #region PolarBear
                    ["PolarBear Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 150),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("rustige_egg_c", 1, 9999999995),
                        command = "polarbear.craft",
                        land = false,
                        itemname = "PolarBear",
                        prefab = "assets/rust.ai/agents/bear/polarbear.prefab",
                    },
                    #endregion PolarBear
                    #region Stag
                    ["Stag Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("rustige_egg_e", 1, 9999999994),
                        command = "stag.craft",
                        land = false,
                        itemname = "Stag",
                        prefab = "assets/rust.ai/agents/stag/stag.prefab",
                    },
                    #endregion Stag
                    #region Wolf
                    ["Wolf Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("rustige_egg_b", 1, 9999999993),
                        command = "wolf.craft",
                        land = false,
                        itemname = "Wolf",
                        prefab = "assets/rust.ai/agents/wolf/wolf.prefab",
                    },
                    #endregion Wolf
					
					
                    #endregion Animals
                    #region Vehicles (1)

                    #region M270 Multiple Launch Rocket System
                    ["M270 Multiple Launch Rocket System Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 7500),
                                new CraftItem("scrap", 1250),
                                new CraftItem("metal.refined", 500),
								new CraftItem("techparts", 100),
								new CraftItem("samsite", 2),
								new CraftItem("targeting.computer", 1),
								new CraftItem("computerstation", 1),
								new CraftItem("vehicle.1mod.cockpit.armored", 1),
                                new CraftItem("vehicle.1mod.passengers.armored", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("water.catcher.large", 1, 1449152644),
                        command = "mlrs.craft",
                        land = false,
                        itemname = "M270 Multiple Launch Rocket System",
                        prefab = "assets/content/vehicles/mlrs/mlrs.entity.prefab",
                    },
                    #endregion M270 Multiple Launch Rocket System0
					
                    #endregion Vehicles
					
                    #endregion Deployables
                    #region Items (66)
                    #region Attire (14)
					
                    #region Diving Mask
                    ["Diving Mask Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("diving.mask", 1, 113413047),
                        command = "divingmask.craft",
                        land = false,
                        itemname = "Diving Mask",
                        prefab = null,
                    },
                    #endregion Diving Mask
                    #region Wetsuit
                    ["Wetsuit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("diving.wetsuit", 1, 1101924344),
                        command = "wetsuit.craft",
                        land = false,
                        itemname = "Wetsuit",
                        prefab = null,
                    },
                    #endregion Wetsuit
                    #region Diving Tank
                    ["Diving Tank Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("diving.tank", 1, 2022172587),
                        command = "divingtank.craft",
                        land = false,
                        itemname = "Diving Tank",
                        prefab = null,
                    },
                    #endregion Diving Tank
                    #region Diving Fins
                    ["Diving Fins Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("diving.fins", 1, 296519935),
                        command = "divingfins.craft",
                        land = false,
                        itemname = "Diving Fins",
                        prefab = null,
                    },
                    #endregion Diving Fins
                    #region Space Suit
                    ["Space suit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hazmatsuit.spacesuit", 1, 560304835),
                        command = "spacesuit.craft",
                        land = false,
                        itemname = "Space Suit",
                        prefab = null,
                    },
                    #endregion Space Suit
                    #region Scientist Suit
                    ["Scientist Suit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hazmatsuit_scientist", 1, 253079493),
                        command = "scientistsuit.craft",
                        land = false,
                        itemname = "Scientist Suit",
                        prefab = null,
                    },
                    #endregion Scientist Suit
                    #region Peacekeeper Suit
                    ["Peacekeeper Suit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hazmatsuit_scientist_peacekeeper", 1, 1958316066),
                        command = "peacekeepersuit.craft",
                        land = false,
                        itemname = "Peacekeeper Suit",
                        prefab = null,
                    },
                    #endregion Peacekeeper Suit
                    #region Heavy Scientist Suit
                    ["Heavy Scientist Suit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 300),
                                new CraftItem("tarp", 30),
                                new CraftItem("sewingkit", 15),
                                new CraftItem("propanetank", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("scientistsuit_heavy", 1, 1772746857),
                        command = "heavyscientistsuit.craft",
                        land = false,
                        itemname = "Heavy Scientist Suit",
                        prefab = null,
                    },
                    #endregion Heavyhazmat Suit
                    #region Clatter Helmet
                    ["Clatter Helmet Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 35),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("clatter.helmet", 1, 968019378),
                        command = "clatterhelmet.craft",
                        land = false,
                        itemname = "Clatter Helmet",
                        prefab = null,
                    },
                    #endregion Clatter Helmet
                    #region Tactical Gloves
                    ["Tactical Gloves Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 2),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("tactical.gloves", 1, 1108136649),
                        command = "tacgloves.craft",
                        land = false,
                        itemname = "Tactical Gloves",
                        prefab = null,
                    },
                    #endregion Tactical Gloves
                    #region Frog Boots
                    ["Frog Boots Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 50),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("boots.frog", 1, 1000573653),
                        command = "frogboots.craft",
                        land = false,
                        itemname = "Frog Boots",
                        prefab = null,
                    },
                    #endregion Frog Boots
                    #region Party Hat
                    ["Party Hat Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("partyhat", 1, 575744869),
                        command = "partyhat.craft",
                        land = false,
                        itemname = "Party Hat",
                        prefab = null,
                    },
                    #endregion Party Hat
                    #region Nomad Suit
                    ["Nomad Suit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hazmatsuit.nomadsuit", 1, 491263800),
                        command = "nomadsuit.craft",
                        land = false,
                        itemname = "Nomad Suit",
                        prefab = null,
                    },
                    #endregion Nomad Suit
                    #region Arctic Suit
                    ["Arctic Suit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 10),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hazmatsuit.arcticsuit", 1, 470439097),
                        command = "arcticsuit.craft",
                        land = false,
                        itemname = "Arctic Suit",
                        prefab = "assets/prefabs/clothes/suit.hazmat/arctic/hazmat_suit_arctic.prefab",
                    },
                    #endregion Arctic Suit
					
                    #endregion Attire
                    #region Weapons (5)
					
                    #region L96 Rifle
                    ["L96 Rifle Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 100),
                                new CraftItem("metal.refined", 40),
                                new CraftItem("metalspring", 4),
                                new CraftItem("metalpipe", 4),
                                new CraftItem("gears", 2),
								new CraftItem("riflebody", 1),
								new CraftItem("semibody", 1),
								
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rifle.l96", 1, 778367295),
                        command = "l96.craft",
                        land = false,
                        itemname = "L96 Rifle",
                        prefab = null,
                    },
                    #endregion L96 Rifle
                    #region M39 Rifle
                    ["M39 Rifle Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 200),
                                new CraftItem("metal.refined", 50),
                                new CraftItem("metalspring", 4),
                                new CraftItem("metalpipe", 4),
                                new CraftItem("gears", 2),
								new CraftItem("riflebody", 1),
								new CraftItem("semibody", 1),
								
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rifle.m39", 1, 28201841),
                        command = "m39.craft",
                        land = false,
                        itemname = "M39 Rifle",
                        prefab = null,
                    },
                    #endregion M39 Rifle
                    #region LR-300 Assault Rifle
                    ["LR-300 Assault Rifle Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 250),
                                new CraftItem("metal.refined", 75),
                                new CraftItem("metalspring", 4),
                                new CraftItem("metalpipe", 4),
                                new CraftItem("gears", 3),
								new CraftItem("riflebody", 1),
								new CraftItem("smgbody", 1),
								
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rifle.lr300", 1, 1812555177),
                        command = "lr300.craft",
                        land = false,
                        itemname = "LR-300 Assault Rifle",
                        prefab = null,
                    },
                    #endregion LR-300 Assault Rifle
                    #region M249
                    ["M249 Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 500),
                                new CraftItem("metal.refined", 100),
                                new CraftItem("metalspring", 5),
                                new CraftItem("metalpipe", 5),
                                new CraftItem("gears", 5),
								new CraftItem("riflebody", 1),
								new CraftItem("smgbody", 1),
								new CraftItem("semibody", 1),
								
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("lmg.m249", 1, 2069578888),
                        command = "m249.craft",
                        land = false,
                        itemname = "M249",
                        prefab = null,
                    },
                    #endregion M249
                    #region Multiple Grenade Launcher
                    ["Multiple Grenade Launcher Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
								
                                new CraftItem("scrap", 1000),
                                new CraftItem("metal.refined", 100),
                                new CraftItem("metalspring", 5),
                                new CraftItem("metalpipe", 5),
                                new CraftItem("gears", 5),
								new CraftItem("riflebody", 1),
								new CraftItem("smgbody", 1),
								new CraftItem("semibody", 1),
								
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("multiplegrenadelauncher", 1, 1123473824),
                        command = "launcher.craft",
                        land = false,
                        itemname = "Multiple Grenade Launcher",
                        prefab = null,
                    },
                    #endregion Multiple Grenade Launcher
					
                    #endregion Weapons
                    #region Components/Resources (6)
					
                    #region CCTV Camera
                    ["CCTV Camera Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                                new CraftItem("techparts", 10),
                                new CraftItem("metal.refined", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("cctv.camera", 1, 634478325),
                        command = "cctv.craft",
                        land = false,
                        itemname = "CCTV Camera",
                        prefab = null,
                    },
                    #endregion CCTV Camera
                    #region Diesel Fuel
                    ["Diesel Fuel Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("lowgradefuel", 250),
                                new CraftItem("crude.oil", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("diesel_barrel", 1, 1568388703),
                        command = "diesel.craft",
                        land = false,
                        itemname = "Diesel Fuel",
                        prefab = "assets/prefabs/resource/diesel barrel/diesel_barrel_world.prefab",
                    },
                    #endregion Diesel Fuel
                    #region Rope
                    ["Rope Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 30),
                                new CraftItem("plantfiber", 6),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rope", 1, 1414245522),
                        command = "rope.craft",
                        land = false,
                        itemname = "Rope",
                        prefab = null,
                    },
                    #endregion Rope
                    #region Sheet Metal
                    ["Sheet Metal Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 200),
                                new CraftItem("scrap", 16),
                                new CraftItem("metal.refined", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sheetmetal", 1, 1994909036),
                        command = "sheetmetal.craft",
                        land = false,
                        itemname = "Sheet Metal",
                        prefab = null,
                    },
                    #endregion Sheet Metal
                    #region Targeting Computer
                    ["Targeting Computer Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 125),
                                new CraftItem("techparts", 20),
                                new CraftItem("metal.refined", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("targeting.computer", 1, 1523195708),
                        command = "targetingcomputer.craft",
                        land = false,
                        itemname = "Targeting Computer",
                        prefab = null,
                    },
                    #endregion Targeting Computer
                    #region Tarp
                    ["Tarp Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("tarp", 1, 2019042823),
                        command = "tarp.craft",
                        land = false,
                        itemname = "Tarp",
                        prefab = null,
                    },
                    #endregion Tarp
					
                    #endregion Components/Resources
					#region Admin Stuff (10)
					
                    #region APC
                    ["APC Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_a", 1, 9999999992),
                        command = "apc.craft",
                        land = false,
                        itemname = "Bradley APC",
                        prefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab",
                    },
                    #endregion APC
                    #region Plane
                    ["Plane Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_e", 1, 9999999991),
                        command = "plane.craft",
                        land = false,
                        itemname = "Cargo Plane",
                        prefab = "assets/prefabs/npc/cargo plane/cargo_plane.prefab",
                    },
                    #endregion Plane
                    #region CH47
                    ["CH47 Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_e", 1, 9999999990),
                        command = "ch47.craft",
                        land = false,
                        itemname = "CH47",
                        prefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab",
                    },
                    #endregion CH47
                    #region Patrol Heli
                    ["Patrol Heli Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_e", 1, 9999999989),
                        command = "patrolheli.craft",
                        land = false,
                        itemname = "Patrol Heli",
                        prefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab",
                    },
                    #endregion Patrol Heli
                    #region Ship
                    ["Ship Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("kayak", 1, 9999999988),
                        command = "ship.craft",
                        land = false,
                        itemname = "Cargo Ship",
                        prefab = "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab",
                    },
                    #endregion Ship
                    #region Santa's Sleigh
                    ["Santa's Sleigh Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_d", 1, 9999999987),
                        command = "claus.craft",
                        land = false,
                        itemname = "Santa's Sleigh",
                        prefab = "assets/prefabs/misc/xmas/sleigh/santasleigh.prefab",
                    },
                    #endregion Santa's Sleigh
                    #region Admin Car
                    ["Admin Car Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("electric.generator.small", 1, 1742652663),
                        command = "car.craft",
                        land = false,
                        itemname = "Admin Car",
                        prefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab",
                    },
                    #endregion Admin Car
                    #region Admin Heli
                    ["Admin Heli Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("electric.generator.small", 1, 1771792500),
                        command = "heli.craft",
                        land = false,
                        itemname = "Admin Heli",
                        prefab = "assets/prefabs/npc/ch47/ch47.entity.prefab",
                    },
                    #endregion Admin Heli
                    #region Admin Sentry
                    ["Admin Sentry Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("electric.generator.small", 1, 9999999986),
                        command = "sentry.craft",
                        land = false,
                        itemname = "Admin Sentry",
                        prefab = "assets/content/props/sentry_scientists/sentry.scientist.static.prefab",
                    },
                    #endregion Admin Sentry
                    #region Shark
                    ["Shark Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 999999999),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("kayak", 1, 9999999999),
                        command = "shark.craft",
                        land = false,
                        itemname = "Shark",
                        prefab = "assets/rust.ai/agents/fish/simpleshark.prefab",
                    },
                    #endregion Shark
					
					#endregion Admin Stuff
                    #region Jokes/Gags/Misc (13)
					
                    #region Chippy Arcade Game
                    ["Chippy Arcade Game Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.refined", 10),
                                new CraftItem("gears", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("arcade.machine.chippy", 1, 359723196),
                        command = "arcade.craft",
                        land = false,
                        itemname = "Chippy Arcade Game",
                        prefab = "assets/prefabs/misc/chippy arcade/chippyarcademachine.prefab",
                    },
                    #endregion Chippy Arcade Game
                    #region FHC
                    ["FHC Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("glue", 1, 1899491405),
                        command = "fhc.craft",
                        land = false,
                        itemname = "Fermented Horse Cum",
                        prefab = null,
                    },
                    #endregion FHC
                    #region AWK
                    ["AWK Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("ducttape", 1, 1401987718),
                        command = "awk.craft",
                        land = false,
                        itemname = "Apocalypse Waxing Kit",
                        prefab = null,
                    },
                    #endregion AWK
                    #region Bleach
                    ["Bleach Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("bleach", 1, 1553078977),
                        command = "bleach.craft",
                        land = false,
                        itemname = "Bleach",
                        prefab = null,
                    },
                    #endregion Bleach
                    #region Industrial Wall Light
                    ["Industrial Wall Light Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("industrial.wall.light", 1, 1623701499),
                        command = "whitelight.craft",
                        land = false,
                        itemname = "Industrial Wall Light",
                        prefab = "assets/prefabs/misc/permstore/industriallight/industrial.wall.lamp.deployed.prefab",
                    },
                    #endregion Industrial Wall Light
                    #region Red Industrial Wall Light
                    ["Red Industrial Wall Light Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("industrial.wall.light.red", 1, 1160621614),
                        command = "redlight.craft",
                        land = false,
                        itemname = "Red Industrial Wall Light",
                        prefab = "assets/prefabs/misc/permstore/industriallight/industrial.wall.lamp.red.deployed.prefab",
                    },
                    #endregion Red Industrial Wall Light
                    #region Green Industrial Wall Light
                    ["Green Industrial Wall Light Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("industrial.wall.light.green", 1, 1268178466),
                        command = "greenlight.craft",
                        land = false,
                        itemname = "Green Industrial Wall Light",
                        prefab = "assets/prefabs/misc/permstore/industriallight/industrial.wall.lamp.green.deployed.prefab",
                    },
                    #endregion Green Industrial Wall Light
                    #region Simple Light
                    ["Simple Light Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                                new CraftItem("metal.fragments", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("electric.simplelight", 1, 282113991),
                        command = "simplelight.craft",
                        land = false,
                        itemname = "Simple Light",
                        prefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab",
                    },
                    #endregion Simple Light
                    #region Slot Machine
                    ["Slot Machine Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 500),
                                new CraftItem("wood", 250),
                                new CraftItem("wiretool", 10),
                                new CraftItem("gears", 6),
                                new CraftItem("metalspring", 5),
                                new CraftItem("techparts", 4),
                                new CraftItem("electrical.memorycell", 3),
                                new CraftItem("electric.counter", 2),
                                new CraftItem("targetingcomputer", 1),
                                new CraftItem("storage.monitor", 1),
                                new CraftItem("electric.sirenlight", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("arcade.machine.chippy", 1, 9999999985),
                        command = "slots.craft",
                        land = false,
                        itemname = "Slot Machine",
                        prefab = "assets/prefabs/misc/casino/slotmachine/slotmachine.prefab",
                    },
                    #endregion Slot Machine
                    #region Card Table
                    ["Card Table Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 400),
                                new CraftItem("metal.fragments", 400),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("cardtable", 1, 1081921512),
                        command = "cardtable.craft",
                        land = false,
                        itemname = "Card Table",
                        prefab = "assets/prefabs/deployable/card table/cardtable.deployed.prefab",
                    },
                    #endregion Card Table
                    #region Sofa
                    ["Sofa Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 200),
                                new CraftItem("cloth", 60),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sofa", 1, 555122905),
                        command = "sofa.craft",
                        land = false,
                        itemname = "Sofa",
                        prefab = "assets/prefabs/deployable/sofa/sofa.deployed.prefab",
                    },
                    #endregion Sofa
                    #region Pattern Sofa
                    ["Pattern Sofa Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 200),
                                new CraftItem("cloth", 60),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sofa.pattern", 1, 782422285),
                        command = "patternsofa.craft",
                        land = false,
                        itemname = "Pattern Sofa",
                        prefab = "assets/prefabs/deployable/sofa/sofa.pattern.deployed.prefab",
                    },
                    #endregion Pattern Sofa
                    #region Secret Lab Chair
                    ["Secret Lab Chair Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 125),
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("secretlabchair", 1, 567871954),
                        command = "labchair.craft",
                        land = false,
                        itemname = "Secret Lab Chair",
                        prefab = "assets/prefabs/deployable/secretlab chair/secretlabchair.deployed.prefab",
                    },
                    #endregion Secret Lab Chair
					
                    #endregion Jokes/Gags/Misc
					#region Musical Instruments (10)
					
                    #region Canbourine
                    ["Canbourine Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.tambourine", 1, 1379036069),
                        command = "canbourine.craft",
                        land = false,
                        itemname = "Canbourine",
                        prefab = "assets/prefabs/instruments/tambourine/tambourine.weapon.prefab",
                    },
                    #endregion Canbourine
                    #region Cowbell
                    ["Cowbell Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 35),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.cowbell", 1, 1049881973),
                        command = "cowbell.craft",
                        land = false,
                        itemname = "Cowbell",
                        prefab = "assets/prefabs/instruments/cowbell/cowbell.weapon.prefab",
                    },
                    #endregion Cowbell
                    #region Junkyard Drum Kit
                    ["Junkyard Drum Kit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 200),
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("drumkit", 1, 1330640246),
                        command = "drumkit.craft",
                        land = false,
                        itemname = "Junkyard Drum Kit",
                        prefab = "assets/prefabs/instruments/drumkit/drumkit.deployed.prefab",
                    },
                    #endregion Junkyard Drum Kit
                    #region Jerry Can Guitar
                    ["Jerry Can Guitar Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 50),
                                new CraftItem("wood", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.jerrycanguitar", 1, 979951147),
                        command = "jerrycanguitar.craft",
                        land = false,
                        itemname = "Jerry Can Guitar",
                        prefab = "assets/prefabs/instruments/jerrycanguitar/jerrycanguitar.weapon.prefab",
                    },
                    #endregion Jerry Can Guitar
                    #region Pan Flute
                    ["Pan Flute Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 20),
                                new CraftItem("cloth", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.flute", 1, 2040817543),
                        command = "flute.craft",
                        land = false,
                        itemname = "Pan Flute",
                        prefab = "assets/prefabs/instruments/flute/flute.weapon.prefab",
                    },
                    #endregion Pan Flute
                    #region Plumber's Trumpet
                    ["Plumber's Trumpet Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.trumpet", 1, 273172220),
                        command = "trumpet.craft",
                        land = false,
                        itemname = "Plumber's Trumpet",
                        prefab = "assets/prefabs/instruments/trumpet/trumpet.weapon.prefab",
                    },
                    #endregion Plumber's Trumpet
                    #region Sousaphone
                    ["Sousaphone Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.tuba", 1, 1784406797),
                        command = "tuba.craft",
                        land = false,
                        itemname = "Sousaphone",
                        prefab = "assets/prefabs/instruments/tuba/tuba.weapon.prefab",
                    },
                    #endregion Sousaphone
                    #region Shovel Bass
                    ["Shovel Bass Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                                new CraftItem("wood", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.bass", 1, 2107018088),
                        command = "bass.craft",
                        land = false,
                        itemname = "Shovel Bass",
                        prefab = "assets/prefabs/instruments/bass/bass.weapon.prefab",
                    },
                    #endregion Shovel Bass
                    #region Wheelbarrow Piano
                    ["Wheelbarrow Piano Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 200),
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("piano", 1, 1272430949),
                        command = "piano.craft",
                        land = false,
                        itemname = "Wheelbarrow Piano",
                        prefab = "assets/prefabs/instruments/piano/piano.deployed.prefab",
                    },
                    #endregion Wheelbarrow Piano
                    #region Xylobone
                    ["Xylobone Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("bone.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xylophone", 1, 211235948),
                        command = "xylobone.craft",
                        land = false,
                        itemname = "Xylobone",
                        prefab = "assets/prefabs/instruments/xylophone/xylophone.deployed.prefab",
                    },
                    #endregion Xylobone
					
					#endregion Musical Instruments
					#region VoicePropsPack (15)
					
                    #region Cassette Recorder
                    ["Cassette Recorder Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.casetterecorder", 1, 1530414568),
                        command = "recorder.craft",
                        land = false,
                        itemname = "Cassette Recorder",
                        prefab = "assets/prefabs/voiceaudio/cassetterecorder/cassetterecorder.weapon.prefab",
                    },
                    #endregion Cassette Recorder
                    #region Cassette - Short
                    ["Cassette - Short Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("cassette.short", 1, 1523403414),
                        command = "cassette1.craft",
                        land = false,
                        itemname = "Cassette - Short",
                        prefab = "assets/prefabs/voiceaudio/cassette/cassette.short.entity.prefab",
                    },
                    #endregion Cassette - Short
                    #region Cassette - Medium
                    ["Cassette - Medium Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("cassette.medium", 1, 912398867),
                        command = "cassette2.craft",
                        land = false,
                        itemname = "Cassette - Medium",
                        prefab = "assets/prefabs/voiceaudio/cassette/cassette.medium.entity.prefab",
                    },
                    #endregion Cassette - Medium
                    #region Cassette - Long
                    ["Cassette - Long Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("cassette", 1, 476066818),
                        command = "cassette3.craft",
                        land = false,
                        itemname = "Cassette - Long",
                        prefab = "assets/prefabs/voiceaudio/cassette/cassette.entity.prefab",
                    },
                    #endregion Cassette - Long
                    #region Mobile Phone
                    ["Mobile Phone Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 125),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("mobilephone", 1, 20045316),
                        command = "mobilephone.craft",
                        land = false,
                        itemname = "Mobile Phone",
                        prefab = "assets/prefabs/voiceaudio/mobilephone/mobilephone.weapon.prefab",
                    },
                    #endregion Mobile Phone
                    #region Boom Box
                    ["Boom Box Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 200),
                                new CraftItem("metal.fragments", 100),
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("boombox", 1, 1113501606),
                        command = "boombox1.craft",
                        land = false,
                        itemname = "Boom Box",
                        prefab = "assets/prefabs/voiceaudio/boombox/boombox.deployed.prefab",
                    },
                    #endregion Boom Box
                    #region Portable Boom Box
                    ["Portable Boom Box Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 125),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("fun.boomboxportable", 1, 576509618),
                        command = "boombox2.craft",
                        land = false,
                        itemname = "Portable Boom Box",
                        prefab = "assets/prefabs/voiceaudio/boomboxportable/boomboxportable.weapon.prefab",
                    },
                    #endregion Portable Boom Box
                    #region Connected Speaker
                    ["Connected Speaker Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("connected.speaker", 1, 968421290),
                        command = "connectedspeaker.craft",
                        land = false,
                        itemname = "Connected Speaker",
                        prefab = "assets/prefabs/voiceaudio/hornspeaker/connectedspeaker.deployed.prefab",
                    },
                    #endregion Connected Speaker
                    #region Megaphone
                    ["Megaphone Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                                new CraftItem("wood", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("megaphone", 1, 583379016),
                        command = "megaphone.craft",
                        land = false,
                        itemname = "Megaphone",
                        prefab = "assets/prefabs/voiceaudio/megaphone/megaphone.weapon.prefab",
                    },
                    #endregion Megaphone
                    #region Microphone Stand
                    ["Microphone Stand Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("microphonestand", 1, 39600618),
                        command = "microphonestand.craft",
                        land = false,
                        itemname = "Microphone Stand",
                        prefab = "assets/prefabs/voiceaudio/microphonestand/microphonestand.deployed.prefab",
                    },
                    #endregion Microphone Stand
                    #region Sound Light
                    ["Sound Light Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("soundlight", 1, 343857907),
                        command = "soundlight.craft",
                        land = false,
                        itemname = "Sound Light",
                        prefab = "assets/prefabs/voiceaudio/soundlight/soundlight.deployed.prefab",
                    },
                    #endregion Sound Light
                    #region Laser Light
                    ["Laser Light Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("laserlight", 1, 853471967),
                        command = "laserlight.craft",
                        land = false,
                        itemname = "Laser Light",
                        prefab = "assets/prefabs/voiceaudio/laserlight/laserlight.deployed.prefab",
                    },
                    #endregion Laser Light
                    #region Disco Floor
                    ["Disco Floor Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("discofloor", 1, 286648290),
                        command = "discofloor.craft",
                        land = false,
                        itemname = "Disco Floor",
                        prefab = "assets/prefabs/voiceaudio/discofloor/discofloor.deployed.prefab",
                    },
                    #endregion Disco Floor
                    #region Disco Floor Large Tiles
                    ["Disco Floor Large Tiles Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("discofloor.largetiles", 1, 1735402444),
                        command = "discofloorlarge.craft",
                        land = false,
                        itemname = "Disco Floor Large Tiles",
                        prefab = "assets/prefabs/voiceaudio/discofloor/skins/discofloor.largetiles.deployed.prefab",
                    },
                    #endregion Disco Floor Large Tiles
                    #region Disco Ball
                    ["Disco Ball Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("discoball", 1, 1895235349),
                        command = "discoball.craft",
                        land = false,
                        itemname = "Disco Ball",
                        prefab = "assets/prefabs/voiceaudio/discoball/discoball.deployed.prefab",
                    },
                    #endregion Disco Ball
					
					#endregion VoicePropsPack
					
                    #endregion Items
                    #region Seasonal (136)
					
                    #region Summer (15)
					
                    #region Water Pistol
                    ["Water Pistol Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("pistol.water", 1, 1815301988),
                        command = "waterpistol.craft",
                        land = false,
                        itemname = "Water Pistol",
                        prefab = "assets/prefabs/misc/summer_dlc/waterpistol/waterpistol.entity.prefab",
                    },
                    #endregion Water Pistol
                    #region Water Gun
                    ["Water Gun Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 125),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("gun.water", 1, 722955039),
                        command = "watergun.craft",
                        land = false,
                        itemname = "Water Gun",
                        prefab = "assets/prefabs/misc/summer_dlc/watergun/watergun.entity.prefab",
                    },
                    #endregion Water Gun
                    #region Paddling Pool
                    ["Paddling Pool Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("paddlingpool", 1, 733625651),
                        command = "pool1.craft",
                        land = false,
                        itemname = "Paddling Pool",
                        prefab = "assets/prefabs/misc/summer_dlc/paddling_pool/paddlingpool.deployed.prefab",
                    },
                    #endregion Paddling Pool
                    #region Above Ground Pool
                    ["Above Ground Pool Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 500),
                                new CraftItem("metal.fragments", 200),
                                new CraftItem("tarp", 3),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("abovegroundpool", 1, 1840570710),
                        command = "pool2.craft",
                        land = false,
                        itemname = "Above Ground Pool",
                        prefab = "assets/prefabs/misc/summer_dlc/abovegroundpool/abovegroundpool.deployed.prefab",
                    },
                    #endregion Above Ground Pool
                    #region Beach Chair
                    ["Beach Chair Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 50),
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("beachchair", 1, 321431890),
                        command = "beachchair.craft",
                        land = false,
                        itemname = "Beach Chair",
                        prefab = "assets/prefabs/misc/summer_dlc/beach_chair/beachchair.deployed.prefab",
                    },
                    #endregion Beach Chair
                    #region Beach Parasol
                    ["Beach Parasol Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 20),
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("beachparasol", 1, 1621539785),
                        command = "beachparasol.craft",
                        land = false,
                        itemname = "Beach Parasol",
                        prefab = "assets/prefabs/misc/summer_dlc/beach_chair/beachparasol.deployed.prefab",
                    },
                    #endregion Beach Parasol
                    #region Beach Table
                    ["Beach Table Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 200),
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("beachtable", 1, 657352755),
                        command = "beachtable.craft",
                        land = false,
                        itemname = "Beach Table",
                        prefab = "assets/prefabs/misc/summer_dlc/beach_chair/beachtable.deployed.prefab",
                    },
                    #endregion Beach Table
                    #region Beach Towel
                    ["Beach Towel Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("beachtowel", 1, 8312704),
                        command = "beachtowel.craft",
                        land = false,
                        itemname = "Beach Towel",
                        prefab = "assets/prefabs/misc/summer_dlc/beach_towel/beachtowel.deployed.prefab",
                    },
                    #endregion Beach Towel
                    #region Boogie Board
                    ["Boogie Board Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                                new CraftItem("tarp", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("boogieboard", 1, 1478094705),
                        command = "boogieboard.craft",
                        land = false,
                        itemname = "Boogie Board",
                        prefab = "assets/prefabs/misc/summer_dlc/boogie_board/boogieboard.deployed.prefab",
                    },
                    #endregion Boogie Board
                    #region Inner Tube
                    ["Inner Tube Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                                new CraftItem("tarp", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("innertube", 1, 697981032),
                        command = "innertube.craft",
                        land = false,
                        itemname = "Inner Tube",
                        prefab = "assets/prefabs/misc/summer_dlc/inner_tube/innertube.deployed.prefab",
                    },
                    #endregion Inner Tube
                    #region Sunglasses
                    ["Sunglasses Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 60),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sunglasses", 1, 352321488),
                        command = "sunglasses.craft",
                        land = false,
                        itemname = "Sunglasses",
                        prefab = null,
                    },
                    #endregion Sunglasses
                    #region Instant Camera
                    ["Instant Camera Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 75),
                                new CraftItem("gears", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("tool.instant_camera", 1, 2001260025),
                        command = "instantcamera.craft",
                        land = false,
                        itemname = "Instant Camera",
                        prefab = "assets/prefabs/misc/summer_dlc/instantcamera/instant_camera.entity.prefab",
                    },
                    #endregion Instant Camera
                    #region Portrait Photo Frame
                    ["Portrait Photo Frame Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("photoframe.portrait", 1, 1729712564),
                        command = "photoportrait.craft",
                        land = false,
                        itemname = "Portrait Photo Frame",
                        prefab = "assets/prefabs/misc/summer_dlc/photoframe/photoframe.portrait.prefab",
                    },
                    #endregion Portrait Photo Frame
                    #region Landscape Photo Frame
                    ["Landscape Photo Frame Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("photoframe.landscape", 1, 1697996440),
                        command = "photolandscape.craft",
                        land = false,
                        itemname = "Landscape Photo Frame",
                        prefab = "assets/prefabs/misc/summer_dlc/photoframe/photoframe.landscape.prefab",
                    },
                    #endregion Landscape Photo Frame
                    #region Large Photo Frame
                    ["Large Photo Frame Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("photoframe.large", 1, 1205084994),
                        command = "photolarge.craft",
                        land = false,
                        itemname = "Large Photo Frame",
                        prefab = "assets/prefabs/misc/summer_dlc/photoframe/photoframe.landscape.prefab",
                    },
                    #endregion Large Photo Frame
					
                    #endregion Summer
                    #region Halloween (42)
					
                    #region A Barrel Costume
                    ["A Barrel Costume Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("water.barrel", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("barrelcostume", 1, 1215166612),
                        command = "barrelcostume.craft",
                        land = false,
                        itemname = "A Barrel Costume",
                        prefab = null,
                    },
                    #endregion A Barrel Costume
                    #region Scarecrow Wrap
                    ["Scarecrow Wrap Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 75),
                                new CraftItem("sewingkit", 3),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("scarecrowhead", 1, 809942731),
                        command = "scarecrowhead.craft",
                        land = false,
                        itemname = "Scarecrow Wrap",
                        prefab = null,
                    },
                    #endregion Scarecrow Wrap
                    #region Scarecrow Suit
                    ["Scarecrow Suit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 125),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("scarecrow.suit", 1, 273951840),
                        command = "scarecrowsuit.craft",
                        land = false,
                        itemname = "Scarecrow Suit",
                        prefab = null,
                    },
                    #endregion Scarecrow Suit
                    #region Coffin
                    ["Coffin Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 500),
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("coffin.storage", 1, 573676040),
                        command = "coffin.craft",
                        land = false,
                        itemname = "Coffin",
                        prefab = "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab",
                    },
                    #endregion Coffin
                    #region Cursed Cauldron
                    ["Cursed Cauldron Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("cursedcauldron", 1, 1242522330),
                        command = "cursedcauldron.craft",
                        land = false,
                        itemname = "Cursed Cauldron",
                        prefab = "assets/prefabs/misc/halloween/cursed_cauldron/cursedcauldron.deployed.prefab",
                    },
                    #endregion Cursed Cauldron
                    #region Fogger-3000
                    ["Fogger-3000 Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                                new CraftItem("lowgradefuel", 30),
                                new CraftItem("metalpipe", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("fogmachine", 1, 1973785141),
                        command = "fogger.craft",
                        land = false,
                        itemname = "Fogger-3000",
                        prefab = "assets/content/props/fog machine/fogmachine.prefab",
                    },
                    #endregion Fogger-3000
                    #region Gravestone
                    ["Gravestone Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("stones", 250),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("gravestone", 1, 809199956),
                        command = "gravestone.craft",
                        land = false,
                        itemname = "Gravestone",
                        prefab = "assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab",
                    },
                    #endregion Gravestone
                    #region Graveyard Fence
                    ["Graveyard Fence Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("stones", 250),
                                new CraftItem("metal.fragments", 75),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("wall.graveyard.fence", 1, 1679267738),
                        command = "graveyardfence.craft",
                        land = false,
                        itemname = "Graveyard Fence",
                        prefab = "assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab",
                    },
                    #endregion Graveyard Fence
                    #region Large Candle Set
                    ["Large Candle Set Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("fat.animal", 35),
                                new CraftItem("cloth", 8),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(false, true, true),
                        item = new CraftItem("largecandles", 1, 489848205),
                        command = "largecandles.craft",
                        land = false,
                        itemname = "Large Candle Set",
                        prefab = "assets/prefabs/misc/halloween/candles/largecandleset.prefab",
                    },
                    #endregion Large Candle Set
                    #region Small Candle Set
                    ["Small Candle Set Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("fat.animal", 20),
                                new CraftItem("cloth", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("smallcandles", 1, 2058362263),
                        command = "smallcandles.craft",
                        land = false,
                        itemname = "Small Candle Set",
                        prefab = "assets/prefabs/misc/halloween/candles/smallcandleset.prefab",
                    },
                    #endregion Small Candle Set
                    #region Spider Webs
                    ["Spider Webs Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("spiderweb", 1, 882559853),
                        command = "spiderweb.craft",
                        land = false,
                        itemname = "Spider Webs",
                        prefab = "assets/prefabs/misc/halloween/spiderweb/spiderweba.prefab",
                    },
                    #endregion Spider Webs
                    #region Spooky Speaker
                    ["Spooky Speaker Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 400),
                                new CraftItem("metal.fragments", 100),
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("spookyspeaker", 1, 1885488976),
                        command = "spookyspeaker.craft",
                        land = false,
                        itemname = "Spooky Speaker",
                        prefab = "assets/prefabs/misc/halloween/spookyspeaker/spookyspeaker.prefab",
                    },
                    #endregion Spooky Speaker
                    #region Strobe Light
                    ["Strobe Light Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                                new CraftItem("metal.refined", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("strobelight", 1, 2104517339),
                        command = "strobelight.craft",
                        land = false,
                        itemname = "Strobe Light",
                        prefab = "assets/content/props/strobe light/strobelight.prefab",
                    },
                    #endregion Strobe Light
                    #region Wooden Cross
                    ["Wooden Cross Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 250),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("woodcross", 1, 699075597),
                        command = "woodcross.craft",
                        land = false,
                        itemname = "Wooden Cross",
                        prefab = "assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab",
                    },
                    #endregion Wooden Cross
                    #region Butcher Knife
                    ["Butcher Knife Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 150),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("knife.butcher", 1, 194509282),
                        command = "butcherknife.craft",
                        land = false,
                        itemname = "Butcher Knife",
                        prefab = "assets/prefabs/weapons/halloween/butcher knife/butcherknife.entity.prefab",
                    },
                    #endregion Butcher Knife
                    #region Pitchfork
                    ["Pitchfork Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 600),
                                new CraftItem("metal.fragments", 300),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("pitchfork", 1, 1090916276),
                        command = "pitchfork.craft",
                        land = false,
                        itemname = "Pitchfork",
                        prefab = "assets/prefabs/weapons/halloween/pitchfork/pitchfork.entity.prefab",
                    },
                    #endregion Pitchfork
                    #region Sickle
                    ["Sickle Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 250),
                                new CraftItem("metal.fragments", 150),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sickle", 1, 1368584029),
                        command = "sickle.craft",
                        land = false,
                        itemname = "Sickle",
                        prefab = "assets/prefabs/weapons/halloween/sickle/sickle.entity.prefab",
                    },
                    #endregion Sickle
                    #region Skull Trophy
                    ["Skull Trophy Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                                new CraftItem("metal.fragments", 25),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("skull.trophy", 1, 769647921),
                        command = "skulltrophy.craft",
                        land = false,
                        itemname = "Skull Trophy",
                        prefab = "assets/prefabs/misc/halloween/trophy skulls/skulltrophy.deployed.prefab",
                    },
                    #endregion Skull Trophy
                    #region Skull Spikes
                    ["Skull Spikes Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 150),
                                new CraftItem("skull.human", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("skullspikes", 1, 1073015016),
                        command = "skullspikes.craft",
                        land = false,
                        itemname = "Skull Spikes",
                        prefab = "assets/prefabs/misc/halloween/skull spikes/skullspikes.deployed.prefab",
                    },
                    #endregion Skull Spikes
                    #region Mummy Suit
                    ["Mummy Suit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                                new CraftItem("tarp", 5),
                                new CraftItem("sewingkit", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("halloween.mummysuit", 1, 277730763),
                        command = "mummysuit.craft",
                        land = false,
                        itemname = "Mummy Suit",
                        prefab = null,
                    },
                    #endregion Mummy Suit
                    #region Ghost Costume
                    ["Ghost Costume Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 200),
                                new CraftItem("sewingkit", 4),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("ghostsheet", 1, 1043618880),
                        command = "ghostsheet.craft",
                        land = false,
                        itemname = "Ghost Costume",
                        prefab = null,
                    },
                    #endregion Ghost Costume
                    #region Crate Costume
                    ["Crate Costume Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 500),
                                new CraftItem("metal.fragments", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("cratecostume", 1, 1189981699),
                        command = "cratecostume.craft",
                        land = false,
                        itemname = "Crate Costume",
                        prefab = null,
                    },
                    #endregion Crate Costume
                    #region Skull Door Knocker
                    ["Skull Door Knocker Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("skulldoorknocker", 1, 216116642),
                        command = "skullknocker.craft",
                        land = false,
                        itemname = "Skull Door Knocker",
                        prefab = "assets/prefabs/misc/halloween/skull_door_knocker/skull_door_knocker.deployed.prefab",
                    },
                    #endregion Skull Door Knocker
                    #region Glowing Eyes
                    ["Glowing Eyes Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 500),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("gloweyes", 1, 690276911),
                        command = "gloweyes.craft",
                        land = false,
                        itemname = "Glowing Eyes",
                        prefab = null,
                    },
                    #endregion Glowing Eyes
                    #region Scarecrow
                    ["Scarecrow Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 200),
                                new CraftItem("cloth", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("scarecrow", 1, 177226991),
                        command = "scarecrow.craft",
                        land = false,
                        itemname = "Scarecrow",
                        prefab = "assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab",
                    },
                    #endregion Scarecrow
                    #region Skull Fire Pit
                    ["Skull Fire Pit Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                                new CraftItem("skull.human", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("skull_fire_pit", 1, 553887414),
                        command = "skullfirepit.craft",
                        land = false,
                        itemname = "Skull Fire Pit",
                        prefab = "assets/prefabs/misc/halloween/skull_fire_pit/skull_fire_pit.prefab",
                    },
                    #endregion Skull Fire Pit
                    #region Surgeon Scrubs
                    ["Surgeon Scrubs Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 60),
                                new CraftItem("sewingkit", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("halloween.surgeonsuit", 1, 1785231475),
                        command = "surgeonsuit.craft",
                        land = false,
                        itemname = "Surgeon Scrubs",
                        prefab = null,
                    },
                    #endregion Surgeon Scrubs
                    #region Halloween Candy
                    ["Halloween Candy Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("halloween.candy", 1, 888153050),
                        command = "halloweencandy.craft",
                        land = false,
                        itemname = "Halloween Candy",
                        prefab = null,
                    },
                    #endregion Halloween Candy
                    #region Pumpkin Bucket
                    ["Pumpkin Bucket Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                                new CraftItem("scrap", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("pumpkinbasket", 1, 1346158228),
                        command = "pumpkinbasket.craft",
                        land = false,
                        itemname = "Pumpkin Bucket",
                        prefab = "assets/prefabs/misc/halloween/pumpkin_bucket/pumpkin_basket.entity.prefab",
                    },
                    #endregion Pumpkin Bucket
                    #region Carvable Pumpkin
                    ["Carvable Pumpkin Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("pumpkin", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("carvable.pumpkin", 1, 1524980732),
                        command = "pumpkinlantern.craft",
                        land = false,
                        itemname = "Carvable Pumpkin",
                        prefab = "assets/prefabs/misc/halloween/carvablepumpkin/carvable.pumpkin.prefab",
                    },
                    #endregion Carvable Pumpkin
                    #region Jack O Lantern Angry
                    ["Jack O Lantern Angry Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("pumpkin", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("jackolantern.angry", 1, 1242482355),
                        command = "lanternangry.craft",
                        land = false,
                        itemname = "Jack O Lantern Angry",
                        prefab = "assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab",
                    },
                    #endregion Jack O Lantern Angry
                    #region Jack O Lantern Happy
                    ["Jack O Lantern Happy Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("pumpkin", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("jackolantern.happy", 1, 1824943010),
                        command = "lanternhappy.craft",
                        land = false,
                        itemname = "Jack O Lantern Happy",
                        prefab = "assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab",
                    },
                    #endregion Jack O Lantern Happy
                    #region Frankenstein Table
                    ["Frankenstein Table Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 200),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteintable", 1, 1575635062),
                        command = "frankensteintable.craft",
                        land = false,
                        itemname = "Frankenstein Table",
                        prefab = "assets/prefabs/deployable/frankensteintable/frankensteintable.deployed.prefab",
                    },
                    #endregion Frankenstein Table
                    #region Light Frankenstein Head
                    ["Light Frankenstein Head Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 5),
                                new CraftItem("fat.animal", 10),
								new CraftItem("bone.fragments", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.01.head", 1, 134959124),
                        command = "lightfrankhead.craft",
                        land = false,
                        itemname = "Light Frankenstein Head",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_01/frankensteins_monster_01_head.prefab",
                    },
                    #endregion Light Frankenstein Head
                    #region Light Frankenstein Torso
                    ["Light Frankenstein Torso Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 5),
                                new CraftItem("fat.animal", 10),
								new CraftItem("bone.fragments", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.01.torso", 1, 1624770297),
                        command = "lightfranktorso.craft",
                        land = false,
                        itemname = "Light Frankenstein Torso",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_01/frankensteins_monster_01_torso.prefab",
                    },
                    #endregion Light Frankenstein Torso
                    #region Light Frankenstein Legs
                    ["Light Frankenstein Legs Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 5),
                                new CraftItem("fat.animal", 10),
								new CraftItem("bone.fragments", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.01.legs", 1, 106959911),
                        command = "lightfranklegs.craft",
                        land = false,
                        itemname = "Light Frankenstein Legs",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_01/frankensteins_monster_01_legs.prefab",
                    },
                    #endregion Light Frankenstein Legs
                    #region Medium Frankenstein Head
                    ["Medium Frankenstein Head Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 7),
                                new CraftItem("fat.animal", 15),
								new CraftItem("bone.fragments", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.02.head", 1, 1732475823),
                        command = "mediumfrankhead.craft",
                        land = false,
                        itemname = "Medium Frankenstein Head",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_02/frankensteins_monster_02_head.prefab",
                    },
                    #endregion Medium Frankenstein Head
                    #region Medium Frankenstein Torso
                    ["Medium Frankenstein Torso Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 7),
                                new CraftItem("fat.animal", 15),
								new CraftItem("bone.fragments", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.02.torso", 1, 1491753484),
                        command = "mediumfranktorso.craft",
                        land = false,
                        itemname = "Medium Frankenstein Torso",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_02/frankensteins_monster_02_torso.prefab",
                    },
                    #endregion Medium Frankenstein Torso
                    #region Medium Frankenstein Legs
                    ["Medium Frankenstein Legs Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 7),
                                new CraftItem("fat.animal", 15),
								new CraftItem("bone.fragments", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.02.legs", 1, 835042040),
                        command = "mediumfranklegs.craft",
                        land = false,
                        itemname = "Medium Frankenstein Legs",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_02/frankensteins_monster_02_legs.prefab",
                    },
                    #endregion Medium Frankenstein Legs
                    #region Heavy Frankenstein Head
                    ["Heavy Frankenstein Head Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 10),
                                new CraftItem("fat.animal", 25),
								new CraftItem("bone.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.03.head", 1, 297099594),
                        command = "heavyfrankhead.craft",
                        land = false,
                        itemname = "Heavy Frankenstein Head",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_03/frankensteins_monster_03_head.prefab",
                    },
                    #endregion Heavy Frankenstein Head
                    #region Heavy Frankenstein Torso
                    ["Heavy Frankenstein Torso Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 10),
                                new CraftItem("fat.animal", 25),
								new CraftItem("bone.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.03.torso", 1, 1614528785),
                        command = "heavyfranktorso.craft",
                        land = false,
                        itemname = "Heavy Frankenstein Torso",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_03/frankensteins_monster_03_torso.prefab",
                    },
                    #endregion Heavy Frankenstein Torso
                    #region Heavy Frankenstein Legs
                    ["Heavy Frankenstein Legs Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("humanmeat.raw", 10),
                                new CraftItem("fat.animal", 25),
								new CraftItem("bone.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("frankensteins.monster.03.legs", 1, 2024549027),
                        command = "heavyfranklegs.craft",
                        land = false,
                        itemname = "Heavy Frankenstein Legs",
                        prefab = "assets/prefabs/misc/halloween/frankensteins_monster_03/frankensteins_monster_03_legs.prefab",
                    },
                    #endregion Heavy Frankenstein Legs
					
                    #endregion Halloween
                    #region Easter (12)
					
                    #region Bunny Ears
                    ["Bunny Ears Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("attire.bunnyears", 1, 1004426654),
                        command = "bunnyears.craft",
                        land = false,
                        itemname = "Bunny Ears",
                        prefab = null,
                    },
                    #endregion Bunny Ears
                    #region Bunny Onesie
                    ["Bunny Onesie Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("attire.bunny.onesie", 1, 1266045928),
                        command = "bunnyonesie.craft",
                        land = false,
                        itemname = "Bunny Onesie",
                        prefab = null,
                    },
                    #endregion Bunny Onesie
                    #region Rustigé Egg - Red
                    ["Rustigé Egg - Red Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.refined", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_a", 1, 173268129),
                        command = "eggred.craft",
                        land = false,
                        itemname = "Rustigé Egg - Red",
                        prefab = "assets/prefabs/misc/easter/faberge_egg_a/rustigeegg_a.deployed.prefab",
                    },
                    #endregion Rustigé Egg - Red
                    #region Rustigé Egg - Blue
                    ["Rustigé Egg - Blue Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.refined", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_b", 1, 173268132),
                        command = "eggblue.craft",
                        land = false,
                        itemname = "Rustigé Egg - Blue",
                        prefab = "assets/prefabs/misc/easter/faberge_egg_b/rustigeegg_b.deployed.prefab",
                    },
                    #endregion Rustigé Egg - Blue
                    #region Rustigé Egg - Purple
                    ["Rustigé Egg - Purple Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.refined", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_c", 1, 173268131),
                        command = "eggpurple.craft",
                        land = false,
                        itemname = "Rustigé Egg - Purple",
                        prefab = "assets/prefabs/misc/easter/faberge_egg_c/rustigeegg_c.deployed.prefab",
                    },
                    #endregion Rustigé Egg - Purple
                    #region Rustigé Egg - Ivory
                    ["Rustigé Egg - Ivory Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.refined", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_d", 1, 173268126),
                        command = "eggivory.craft",
                        land = false,
                        itemname = "Rustigé Egg - Ivory",
                        prefab = "assets/prefabs/misc/easter/faberge_egg_d/rustigeegg_d.deployed.prefab",
                    },
                    #endregion Rustigé Egg - Ivory
                    #region Rustigé Egg - Green
                    ["Rustigé Egg - Green Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.refined", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("rustige_egg_e", 1, 173268125),
                        command = "egggreen.craft",
                        land = false,
                        itemname = "Rustigé Egg - Green",
                        prefab = "assets/prefabs/misc/easter/faberge_egg_e/rustigeegg_e.deployed.prefab",
                    },
                    #endregion Rustigé Egg - Green
                    #region Easter Door Wreath
                    ["Easter Door Wreath Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("easterdoorwreath", 1, 979302481),
                        command = "easterwreath.craft",
                        land = false,
                        itemname = "Easter Door Wreath",
                        prefab = "assets/prefabs/misc/easter/door_wreath/easter_door_wreath_deployed.prefab",
                    },
                    #endregion Easter Door Wreath
                    #region Nest Hat
                    ["Nest Hat Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("skull.wolf", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("attire.nesthat", 1, 1081315464),
                        command = "nesthat.craft",
                        land = false,
                        itemname = "Nest Hat",
                        prefab = null,
                    },
                    #endregion Nest Hat
                    #region Bunny Hat
                    ["Bunny Hat Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hat.bunnyhat", 1, 23391694),
                        command = "bunnyhat.craft",
                        land = false,
                        itemname = "Bunny Hat",
                        prefab = null,
                    },
                    #endregion Bunny Hat
                    #region Painted Egg
                    ["Painted Egg Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("easter.paintedeggs", 1, 126305173),
                        command = "eastereggs.craft",
                        land = false,
                        itemname = "Painted Egg",
                        prefab = null,
                    },
                    #endregion Painted Egg
                    #region Egg Basket
                    ["Egg Basket Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                                new CraftItem("scrap", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("easterbasket", 1, 1856217390),
                        command = "easterbasket.craft",
                        land = false,
                        itemname = "Egg Basket",
                        prefab = "assets/prefabs/misc/easter/easter basket/easter_basket.entity.prefab",
                    },
                    #endregion Egg Basket
					
                    #endregion Easter
                    #region Lunar New Year (26)
					
                    #region Blue Boomer
                    ["Blue Boomer Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("gunpowder", 30),
                                new CraftItem("lowgradefuel", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.boomer.blue", 1, 1744298439),
                        command = "blueboomer.craft",
                        land = false,
                        itemname = "Blue Boomer",
                        prefab = "assets/prefabs/deployable/fireworks/mortarblue.prefab",
                    },
                    #endregion Blue Boomer
                    #region Green Boomer
                    ["Green Boomer Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("gunpowder", 30),
                                new CraftItem("lowgradefuel", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.boomer.green", 1, 656349006),
                        command = "greenboomer.craft",
                        land = false,
                        itemname = "Green Boomer",
                        prefab = "assets/prefabs/deployable/fireworks/mortargreen.prefab",
                    },
                    #endregion Green Boomer
                    #region Red Boomer
                    ["Red Boomer Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("gunpowder", 30),
                                new CraftItem("lowgradefuel", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.boomer.red", 1, 1553999294),
                        command = "redboomer.craft",
                        land = false,
                        itemname = "Red Boomer",
                        prefab = "assets/prefabs/deployable/fireworks/mortarred.prefab",
                    },
                    #endregion Red Boomer
                    #region Orange Boomer
                    ["Orange Boomer Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("gunpowder", 30),
                                new CraftItem("lowgradefuel", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.boomer.orange", 1, 7270019),
                        command = "orangeboomer.craft",
                        land = false,
                        itemname = "Orange Boomer",
                        prefab = "assets/prefabs/deployable/fireworks/mortarorange.prefab",
                    },
                    #endregion Orange Boomer
                    #region Violet Boomer
                    ["Violet Boomer Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("gunpowder", 30),
                                new CraftItem("lowgradefuel", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.boomer.violet", 1, 280223496),
                        command = "violetboomer.craft",
                        land = false,
                        itemname = "Violet Boomer",
                        prefab = "assets/prefabs/deployable/fireworks/mortarviolet.prefab",
                    },
                    #endregion Violet Boomer
                    #region Champagne Boomer
                    ["Champagne Boomer Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 30),
                                new CraftItem("gunpowder", 75),
                                new CraftItem("lowgradefuel", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.boomer.champagne", 1, 1324203999),
                        command = "champagneboomer.craft",
                        land = false,
                        itemname = "Champagne Boomer",
                        prefab = "assets/prefabs/deployable/fireworks/mortarchampagne.prefab",
                    },
                    #endregion Champagne Boomer
                    #region Red Roman Candle
                    ["Red Roman Candle Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("lowgradefuel", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.romancandle.red", 1, 1486461488),
                        command = "redromancandle.craft",
                        land = false,
                        itemname = "Red Roman Candle",
                        prefab = "assets/prefabs/deployable/fireworks/romancandle.prefab",
                    },
                    #endregion Red Roman Candle
                    #region Blue Roman Candle
                    ["Blue Roman Candle Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("lowgradefuel", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.romancandle.blue", 1, 515830359),
                        command = "blueromancandle.craft",
                        land = false,
                        itemname = "Blue Roman Candle",
                        prefab = "assets/prefabs/deployable/fireworks/romancandle-blue.prefab",
                    },
                    #endregion Blue Roman Candle
                    #region Green Roman Candle
                    ["Green Roman Candle Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("lowgradefuel", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.romancandle.green", 1, 1306288356),
                        command = "greenromancandle.craft",
                        land = false,
                        itemname = "Green Roman Candle",
                        prefab = "assets/prefabs/deployable/fireworks/romancandle-green.prefab",
                    },
                    #endregion Green Roman Candle
                    #region Violet Roman Candle
                    ["Violet Roman Candle Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("lowgradefuel", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.romancandle.violet", 1, 99886070),
                        command = "violetromancandle.craft",
                        land = false,
                        itemname = "Violet Roman Candle",
                        prefab = "assets/prefabs/deployable/fireworks/romancandle-violet.prefab",
                    },
                    #endregion Violet Roman Candle
                    #region White Volcano Firework
                    ["White Volcano Firework Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 20),
                                new CraftItem("gunpowder", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.volcano", 1, 261913429),
                        command = "whitevolcano.craft",
                        land = false,
                        itemname = "White Volcano Firework",
                        prefab = "assets/prefabs/deployable/fireworks/volcanofirework.prefab",
                    },
                    #endregion White Volcano Firework
                    #region Red Volcano Firework
                    ["Red Volcano Firework Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 20),
                                new CraftItem("gunpowder", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.volcano.red", 1, 454370658),
                        command = "redvolcano.craft",
                        land = false,
                        itemname = "Red Volcano Firework",
                        prefab = "assets/prefabs/deployable/fireworks/volcanofirework-red.prefab",
                    },
                    #endregion Red Volcano Firework
                    #region Violet Volcano Firework
                    ["Violet Volcano Firework Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 20),
                                new CraftItem("gunpowder", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.volcano.violet", 1, 1538109120),
                        command = "violetvolcano.craft",
                        land = false,
                        itemname = "Violet Volcano Firework",
                        prefab = "assets/prefabs/deployable/fireworks/volcanofirework-violet.prefab",
                    },
                    #endregion Violet Volcano Firework
                    #region Firecracker String
                    ["Firecracker String Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                 new CraftItem("metal.fragments", 10),
                                new CraftItem("gunpowder", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("lunar.firecrackers", 1, 1961560162),
                        command = "firecracker.craft",
                        land = false,
                        itemname = "Firecracker String",
                        prefab = null,
                    },
                    #endregion Firecracker String
                    #region Rat Mask
                    ["Rat Mask Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("skull.wolf", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hat.ratmask", 1, 271048478),
                        command = "ratmask.craft",
                        land = false,
                        itemname = "Rat Mask",
                        prefab = null,
                    },
                    #endregion Rat Mask
                    #region Ox Mask
                    ["Ox Mask Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("skull.wolf", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hat.oxmask", 1, 1315082560),
                        command = "oxmask.craft",
                        land = false,
                        itemname = "Ox Mask",
                        prefab = null,
                    },
                    #endregion Ox Mask
                    #region Dragon Mask
                    ["Dragon Mask Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("skull.wolf", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hat.dragonmask", 1, 1081315464),
                        command = "dragonmask.craft",
                        land = false,
                        itemname = "Dragon Mask",
                        prefab = null,
                    },
                    #endregion Dragon Mask
                    #region New Year Gong
                    ["New Year Gong Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                                new CraftItem("metal.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("newyeargong", 1, 961457160),
                        command = "gong.craft",
                        land = false,
                        itemname = "New Year Gong",
                        prefab = "assets/prefabs/misc/chinesenewyear/newyeargong/newyeargong.deployed.prefab",
                    },
                    #endregion New Year Gong
                    #region Dragon Door Knocker
                    ["Dragon Door Knocker Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("dragondoorknocker", 1, 854270928),
                        command = "dragonknocker.craft",
                        land = false,
                        itemname = "Dragon Door Knocker",
                        prefab = "assets/prefabs/misc/chinesenewyear/dragondoorknocker/dragondoorknocker.deployed.prefab",
                    },
                    #endregion Dragon Door Knocker
                    #region Chinese Lantern
                    ["Chinese Lantern Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 25),
                                new CraftItem("lowgradefuel", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("chineselantern", 1, 1916473915),
                        command = "chineselantern.craft",
                        land = false,
                        itemname = "Chinese Lantern",
                        prefab = "assets/prefabs/misc/chinesenewyear/chineselantern/chineselantern.deployed.prefab",
                    },
                    #endregion Chinese Lantern
                    #region Sky Lantern
                    ["Sky Lantern Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("lowgradefuel", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("skylantern", 1, 1819863051),
                        command = "skylantern.craft",
                        land = false,
                        itemname = "Sky Lantern",
                        prefab = "assets/prefabs/misc/chinesenewyear/sky_lantern/sky_lantern.prefab",
                    },
                    #endregion Sky Lantern
                    #region Sky Lantern - Green
                    ["Sky Lantern - Green Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("lowgradefuel", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem(".skylantern.green", 1, 1770889433),
                        command = "skylanterngreen.craft",
                        land = false,
                        itemname = "Sky Lantern - Green",
                        prefab = "assets/prefabs/misc/chinesenewyear/sky_lantern/skylantern.skylantern.green.prefab",
                    },
                    #endregion Sky Lantern - Green
                    #region Sky Lantern - Orange
                    ["Sky Lantern - Orange Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("lowgradefuel", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem(".skylantern.orange", 1, 1824770114),
                        command = "skylanternorange.craft",
                        land = false,
                        itemname = "Sky Lantern - Orange",
                        prefab = "assets/prefabs/misc/chinesenewyear/sky_lantern/skylantern.skylantern.orange.prefab",
                    },
                    #endregion Sky Lantern - Orange
                    #region Sky Lantern - Purple
                    ["Sky Lantern - Purple Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("lowgradefuel", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem(".skylantern.purple", 1, 831955134),
                        command = "skylanternpurple.craft",
                        land = false,
                        itemname = "Sky Lantern - Purple",
                        prefab = "assets/prefabs/misc/chinesenewyear/sky_lantern/skylantern.skylantern.purple.prefab",
                    },
                    #endregion Sky Lantern - Purple
                    #region Sky Lantern - Red
                    ["Sky Lantern - Red Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("lowgradefuel", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem(".skylantern.red", 1, 1433390281),
                        command = "skylanternred.craft",
                        land = false,
                        itemname = "Sky Lantern - Red",
                        prefab = "assets/prefabs/misc/chinesenewyear/sky_lantern/skylantern.skylantern.red.prefab",
                    },
                    #endregion Sky Lantern - Red
                    #region Tiger Mask
                    ["Tiger Mask Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                                new CraftItem("skull.wolf", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("hat.tigermask", 1, 709206314),
                        command = "tigermask.craft",
                        land = false,
                        itemname = "Tiger Mask",
                        prefab = null,
                    },
                    #endregion Tiger Mask
					
                    #endregion Lunar New Year
                    #region Christmas (41)
					
                    #region Snowball
                    ["Snowball Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("snowball", 1, 363689972),
                        command = "snowball.craft",
                        land = false,
                        itemname = "Snowball",
                        prefab = "assets/prefabs/misc/xmas/snowball/snowball.entity.prefab",
                    },
                    #endregion Snowball
                    #region Snowball Gun
                    ["Snowball Gun Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("snowballgun", 1, 1103488722),
                        command = "snowballgun.craft",
                        land = false,
                        itemname = "Snowball Gun",
                        prefab = "assets/prefabs/misc/xmas/snowballgun/snowballgun.entity.prefab",
                    },
                    #endregion Snowball Gun
                    #region Small Stocking
                    ["Small Stocking Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("stocking.small", 1, 1668858301),
                        command = "smallstocking.craft",
                        land = false,
                        itemname = "Small Stocking",
                        prefab = "assets/prefabs/misc/xmas/stockings/stocking_small_deployed.prefab",
                    },
                    #endregion Small Stocking
                    #region Super Stocking
                    ["Super Stocking Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("stocking.large", 1, 465682601),
                        command = "superstocking.craft",
                        land = false,
                        itemname = "Super Stocking",
                        prefab = "assets/prefabs/misc/xmas/stockings/stocking_large_deployed.prefab",
                    },
                    #endregion Super Stocking
                    #region Xmas Sled
                    ["Sled Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 1500),
                                new CraftItem("metal.refined", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sled.xmas", 1, 135252633),
                        command = "sled.craft",
                        land = false,
                        itemname = "Xmas Sled",
                        prefab = "assets/prefabs/misc/xmas/sled/sled.deployed.prefab",
                    },
                    #endregion Xmas Sled
                    #region Small Neon Sign
                    ["Small Neon Sign Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 150),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sign.neon.125x125", 1, 1305578813),
                        command = "smallneon.craft",
                        land = false,
                        itemname = "Small Neon Sign",
                        prefab = "assets/prefabs/misc/xmas/neon_sign/sign.neon.125×125.prefab",
                    },
                    #endregion Small Neon Sign
                    #region Medium Neon Sign
                    ["Medium Neon Sign Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 200),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sign.neon.125x215", 1, 1423304443),
                        command = "mediumneon1.craft",
                        land = false,
                        itemname = "Medium Neon Sign",
                        prefab = "assets/prefabs/misc/xmas/neon_sign/sign.neon.125×215.prefab",
                    },
                    #endregion Medium Neon Sign
                    #region Large Neon Sign
                    ["Large Neon Sign Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 250),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sign.neon.xl", 1, 866332017),
                        command = "largeneon1.craft",
                        land = false,
                        itemname = "Large Neon Sign",
                        prefab = "assets/prefabs/misc/xmas/neon_sign/sign.neon.xl.prefab",
                    },
                    #endregion Large Neon Sign
                    #region Medium Animated Neon Sign
                    ["Medium Animated Neon Sign Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 300),
                                new CraftItem("metal.refined", 2),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sign.neon.125x215.animated", 1, 42535890),
                        command = "mediumneon2.craft",
                        land = false,
                        itemname = "Medium Animated Neon Sign",
                        prefab = "assets/prefabs/misc/xmas/neon_sign/sign.neon.125×215.animated.prefab",
                    },
                    #endregion Medium Animated Neon Sign
                    #region Large Animated Neon Sign
                    ["Large Animated Neon Sign Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 350),
                                new CraftItem("metal.refined", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("sign.neon.xl.animated", 1, 1643667218),
                        command = "largeneon2.craft",
                        land = false,
                        itemname = "Large Animated Neon Sign",
                        prefab = "assets/prefabs/misc/xmas/neon_sign/sign.neon.xl.animated.prefab",
                    },
                    #endregion Large Animated Neon Sign
                    #region Short Ice Wall
                    ["Short Ice Wall Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("stones", 300),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("wall.ice.wall", 1, 1327005675),
                        command = "icewall1.craft",
                        land = false,
                        itemname = "Short Ice Wall",
                        prefab = "assets/prefabs/misc/xmas/icewalls/icewall.prefab",
                    },
                    #endregion Short Ice Wall
                    #region High Ice Wall
                    ["High Ice Wall Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("stones", 1500),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("wall.external.high.ice", 1, 985781766),
                        command = "icewall2.craft",
                        land = false,
                        itemname = "High Ice Wall",
                        prefab = "assets/prefabs/misc/xmas/icewalls/wall.external.high.ice.prefab",
                    },
                    #endregion High Ice Wall
                    #region Snow Machine
                    ["Snow Machine Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 125),
                                new CraftItem("lowgradefuel", 30),
                                new CraftItem("metalpipe", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("snowmachine", 1, 1358643074),
                        command = "snowmachine.craft",
                        land = false,
                        itemname = "Snow Machine",
                        prefab = "assets/prefabs/misc/xmas/snow_machine/models/snowmachine.prefab",
                    },
                    #endregion Snow Machine
                    #region Christmas Door Wreath
                    ["Christmas Door Wreath Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmasdoorwreath", 1, 2009734114),
                        command = "xmaswreath.craft",
                        land = false,
                        itemname = "Christmas Door Wreath",
                        prefab = "assets/prefabs/misc/xmas/wreath/christmas_door_wreath_deployed.prefab",
                    },
                    #endregion Christmas Door Wreath
                    #region Christmas Lights
                    ["Christmas Lights Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 25),
                                new CraftItem("lowgradefuel", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.lightstring", 1, 1058261682),
                        command = "xmaslights1.craft",
                        land = false,
                        itemname = "Christmas Lights",
                        prefab = "assets/prefabs/misc/xmas/christmas_lights/xmas.lightstring.deployed.prefab",
                    },
                    #endregion Christmas Lights
                    #region Deluxe Christmas Lights
                    ["Deluxe Christmas Lights Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.lightstring.advanced", 1, 151387974),
                        command = "xmaslights2.craft",
                        land = false,
                        itemname = "Deluxe Christmas Lights",
                        prefab = "assets/prefabs/misc/xmas/poweredlights/xmas.advanced.lights.deployed.prefab",
                    },
                    #endregion Deluxe Christmas Lights
                    #region Tree Lights
                    ["Tree Lights Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 150),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.decoration.lights", 1, 1723747470),
                        command = "treelights.craft",
                        land = false,
                        itemname = "Tree Lights",
                        prefab = null,
                    },
                    #endregion Tree Lights
                    #region Decorative Baubels
                    ["Decorative Baubels Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 100),
                                new CraftItem("scrap", 4),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.decoration.baubels", 1, 1667224349),
                        command = "baubels.craft",
                        land = false,
                        itemname = "Decorative Baubels",
                        prefab = null,
                    },
                    #endregion Decorative Baubels
                    #region Decorative Gingerbread Men
                    ["Decorative Gingerbread Men Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.decoration.gingerbreadmen", 1, 1686524871),
                        command = "gingerbreadman.craft",
                        land = false,
                        itemname = "Decorative Gingerbread Men",
                        prefab = null,
                    },
                    #endregion Decorative Gingerbread Men
                    #region Decorative Pinecones
                    ["Decorative Pinecones Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.decoration.pinecone", 1, 129230242),
                        command = "pinecones.craft",
                        land = false,
                        itemname = "Decorative Pinecones",
                        prefab = null,
                    },
                    #endregion Decorative Pinecones
                    #region Decorative Plastic Candy Canes
                    ["Decorative Plastic Candy Canes Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.decoration.candycanes", 1, 209869746),
                        command = "plasticcanes.craft",
                        land = false,
                        itemname = "Decorative Plastic Candy Canes",
                        prefab = null,
                    },
                    #endregion Decorative Plastic Candy Canes
                    #region Decorative Tinsel
                    ["Decorative Tinsel Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                                new CraftItem("scrap", 6),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.decoration.tinsel", 1, 2106561762),
                        command = "tinsel.craft",
                        land = false,
                        itemname = "Decorative Tinsel",
                        prefab = null,
                    },
                    #endregion Decorative Tinsel
                    #region Star Tree Topper
                    ["Star Tree Topper Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 150),
                                new CraftItem("scrap", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.decoration.star", 1, 1331212963),
                        command = "xmasstar.craft",
                        land = false,
                        itemname = "Star Tree Topper",
                        prefab = null,
                    },
                    #endregion Star Tree Topper
                    #region Festive Doorway Garland
                    ["Festive Doorway Garland Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.door.garland", 1, 674734128),
                        command = "garland1.craft",
                        land = false,
                        itemname = "Festive Doorway Garland",
                        prefab = "assets/prefabs/misc/xmas/doorgarland/doorgarland.deployed.prefab",
                    },
                    #endregion Festive Doorway Garland
                    #region Festive Window Garland
                    ["Festive Window Garland Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.window.garland", 1, 1379835144),
                        command = "garland2.craft",
                        land = false,
                        itemname = "Festive Window Garland",
                        prefab = "assets/prefabs/misc/xmas/windowgarland/windowgarland.deployed.prefab",
                    },
                    #endregion Festive Window Garland
                    #region Santa Beard
                    ["Santa Beard Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("santabeard", 1, 2126889441),
                        command = "santabeard.craft",
                        land = false,
                        itemname = "Santa Beard",
                        prefab = null,
                    },
                    #endregion Santa Beard
                    #region Santa Hat
                    ["Santa Hat Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("santahat", 1, 575483084),
                        command = "santahat.craft",
                        land = false,
                        itemname = "Santa Hat",
                        prefab = null,
                    },
                    #endregion Santa Hat
                    #region Reindeer Antlers
                    ["Reindeer Antlers Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("attire.reindeer.headband", 1, 324675402),
                        command = "antlers.craft",
                        land = false,
                        itemname = "Reindeer Antlers",
                        prefab = null,
                    },
                    #endregion Reindeer Antlers
                    #region Candy Cane
                    ["Candy Cane Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 5),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("candycane", 1, 1121925526),
                        command = "candycane.craft",
                        land = false,
                        itemname = "Candy Cane",
                        prefab = null,
                    },
                    #endregion Candy Cane
                    #region Candy Cane Club
                    ["Candy Cane Club Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("candycaneclub", 1, 1789825282),
                        command = "candyclub.craft",
                        land = false,
                        itemname = "Candy Cane Club",
                        prefab = null,
                    },
                    #endregion Candy Cane Club
                    #region Giant Candy Decor
                    ["Giant Candy Decor Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 30),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("giantcandycanedecor", 1, 695124222),
                        command = "giantcandycane.craft",
                        land = false,
                        itemname = "Giant Candy Decor",
                        prefab = "assets/prefabs/misc/xmas/giant_candy_cane/giantcandycane.deployed.prefab",
                    },
                    #endregion Giant Candy Decor
                    #region Giant Lollipop Decor
                    ["Giant Lollipop Decor Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("giantlollipops", 1, 282103175),
                        command = "giantlollipop.craft",
                        land = false,
                        itemname = "Giant Lollipop Decor",
                        prefab = "assets/prefabs/misc/xmas/lollipop_bundle/giantlollipops.deployed.prefab",
                    },
                    #endregion Giant Lollipop Decor
                    #region Snowman
                    ["Snowman Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("charcoal", 50),
                                new CraftItem("cloth", 20),
                                new CraftItem("metal.fragments", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("snowman", 1, 1629293099),
                        command = "snowman.craft",
                        land = false,
                        itemname = "Snowman",
                        prefab = "assets/prefabs/misc/xmas/snowman/snowman.deployed.prefab",
                    },
                    #endregion Snowman
                    #region Moustache
                    ["Moustache Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("movembermoustache", 1, 2047081330),
                        command = "moustache.craft",
                        land = false,
                        itemname = "Moustache",
                        prefab = null,
                    },
                    #endregion Moustache
                    #region Fake Moustache
                    ["Fake Moustache Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 10),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("movembermoustachecard", 1, 3380160),
                        command = "fakemoustache.craft",
                        land = false,
                        itemname = "Fake Moustache",
                        prefab = null,
                    },
                    #endregion Fake Moustache
                    #region Pookie Bear
                    ["Pookie Bear Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 500),
                                new CraftItem("cloth", 250),
                                new CraftItem("sewingkit", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("pookie.bear", 1, 1651220691),
                        command = "pookiebear.craft",
                        land = false,
                        itemname = "Pookie Bear",
                        prefab = "assets/prefabs/misc/xmas/pookie/pookie_deployed.prefab",
                    },
                    #endregion Pookie Bear
                    #region Wrapping Paper
                    ["Wrapping Paper Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("rope", 1),
                                new CraftItem("tarp", 1),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("wrappingpaper", 1, 1094293920),
                        command = "wrappingpaper.craft",
                        land = false,
                        itemname = "Wrapping Paper",
                        prefab = null,
                    },
                    #endregion Wrapping Paper
                    #region Christmas Tree
                    ["Christmas Tree Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("scrap", 50),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.tree", 1, 794443127),
                        command = "xmastree.craft",
                        land = false,
                        itemname = "Christmas Tree",
                        prefab = "assets/prefabs/misc/xmas/xmastree/xmas_tree.deployed.prefab",
                    },
                    #endregion Christmas Tree
                    #region Snowman Helmet
                    ["Snowman Helmet Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("attire.snowman.helmet", 1, 842267147),
                        command = "snowmanhat.craft",
                        land = false,
                        itemname = "Snowman Helmet",
                        prefab = "assets/prefabs/misc/xmas/wearable/snowman_helmet/snowman_helmet.prefab",
                    },
                    #endregion Snowman Helmet
                    #region Pattern Boomer
                    ["Pattern Boomer Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("metal.fragments", 50),
                                new CraftItem("gunpowder", 30),
                                new CraftItem("lowgradefuel", 15),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("firework.boomer.pattern", 1, 379734527),
                        command = "patternboomer.craft",
                        land = false,
                        itemname = "Pattern Boomer",
                        prefab = "assets/prefabs/deployable/fireworks/boomer.pattern.item.prefab",
                    },
                    #endregion Pattern Boomer
                    #region Advent calendar
                    ["Advent calendar Config Settings"] = new ExtendedInfo
                    {
                        craft = new CraftSettings(true)
                        {
                            items = new List<CraftItem>
                            {
                                new CraftItem("wood", 100),
                                new CraftItem("cloth", 20),
                            }
                        },
                        destroy = new DestroySettings(true, false)
                        {
                            effects = new List<string>
                            {
                                "assets/bundled/prefabs/fx/item_break.prefab",
                                "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                            }
                        },
                        pickup = new PickupSettings(true, true, true),
                        item = new CraftItem("xmas.advent", 1, 2027793839),
                        command = "calendar.craft",
                        land = false,
                        itemname = "Advent calendar",
                        prefab = "assets/prefabs/misc/xmas/advent_calendar/advendcalendar.deployed.prefab",
                    }
                    #endregion Advent calendar
                    #endregion Christmas
					
                    #endregion Seasonal
					
                }
            };
        }

        #endregion Item Tables
    }
}