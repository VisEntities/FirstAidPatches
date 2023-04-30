using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Inventory Viewer", "Whispers88", "4.0.6")]
    [Description("Allows players with permission assigned to view anyone's inventory")]
    public class InventoryViewer : CovalencePlugin
    {
        #region Init
        private static List<string> _registeredhooks = new List<string> { "OnLootEntityEnd", "CanMoveItem", "OnEntityDeath" };
        private const string permuse = "inventoryviewer.allowed";
        private const string permunlock = "inventoryviewer.unlock";
        private void OnServerInitialized()
        {
            permission.RegisterPermission(permuse, this);
            permission.RegisterPermission(permunlock, this);

            AddCovalenceCommand(new[] { "viewinv", "viewinventory", "inspect" }, "ViewInvCmd");

            UnSubscribeFromHooks();
        }

        private void Unload()
        {
            for (var i = _viewingcorpse.Count - 1; i >= 0; i--)
            {
                if (_viewingcorpse[i] == null) continue;
                _viewingcorpse[i].Kill();
            }
        }

        #endregion Init

        #region Configuration
        private Configuration config;
        public class Configuration
        {
            [JsonProperty("View inventory raycast distance")]
            public float raycastdist = 10;

            [JsonProperty("View inventory timeout (seconds) set to 0 to disable")]
            public float timeout = 60;

            [JsonProperty("Use console logging")]
            public bool consolelogging = false;

            [JsonProperty("Use discord logging")]
            public bool discordlogging = false;

            [JsonProperty("Webhook URL")]
            public string discordwebhook = "";

            [JsonProperty("Discord name")]
            public string discordname = "Inventory Viewer";

            [JsonProperty("Discord avatar URL")]
            public string discordavatarurl = "https://i.imgur.com/BLoVcpz.png";
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
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

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerms"] = "You don't have permissions to use this command",
                ["NoPlayersFound"] = "No players were found by the identifier of {0}",
                ["NoPlayersFoundRayCast"] = "No players were found",
                ["ViewingPLayer"] = "Viewing <color=orange>{0}'s</color> inventory"

            }, this);
        }

        #endregion Localization

        #region Commands
        private void ViewInvCmd(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permuse))
            {
                ChatMessage(iplayer, GetLang("NoPerms"));
                return;
            }

            if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
            {
                RaycastHit hitinfo;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hitinfo, 3f, (int)Layers.Server.Players))
                {
                    ChatMessage(iplayer, "NoPlayersFoundRayCast");
                    return;
                }
                BasePlayer targetplayerhit = hitinfo.GetEntity().ToPlayer();
                if (targetplayerhit == null)
                {
                    ChatMessage(iplayer, "NoPlayersFoundRayCast");
                    return;
                }
                ChatMessage(iplayer, "ViewingPLayer", targetplayerhit.displayName);
                ViewInventory(player, targetplayerhit);
                return;
            }
            IPlayer target = FindPlayer(args[0]);
            if (target == null)
            {
                ChatMessage(iplayer, "NoPlayersFound", args[0]);
                return;
            }
            BasePlayer targetplayer = target.Object as BasePlayer;
            if (targetplayer == null)
            {
                ChatMessage(iplayer, "NoPlayersFound", args[0]);
                return;
            }
            ChatMessage(iplayer, "ViewingPLayer", targetplayer.displayName);
            ViewInventory(player, targetplayer);
        }

        #endregion Commands

        #region Methods
        private List<LootableCorpse> _viewingcorpse = new List<LootableCorpse>();
        private void ViewInventory(BasePlayer player, BasePlayer targetplayer)
        {
            if (_viewingcorpse.Count == 0)
                SubscribeToHooks();

            player.EndLooting();

            LootableCorpse corpse = GameManager.server.CreateEntity(StringPool.Get(2604534927), Vector3.zero) as LootableCorpse;
            if (config.timeout != 0)
                timer.Once(config.timeout, ()=> OnLootEntityEnd(player, corpse));
            corpse.syncPosition = false;
            corpse.limitNetworking = true;
            corpse.playerName = targetplayer.displayName;
            corpse.playerSteamID = 0;
            corpse.enableSaving = false;
            corpse.Spawn();
            corpse.CancelInvoke(corpse.RemoveCorpse);
            corpse.SetFlag(BaseEntity.Flags.Locked, true);
            Buoyancy bouyancy;
            if (corpse.TryGetComponent<Buoyancy>(out bouyancy))
            {
                UnityEngine.Object.Destroy(bouyancy);
            }
            Rigidbody ridgidbody;
            if (corpse.TryGetComponent<Rigidbody>(out ridgidbody))
            {
                UnityEngine.Object.Destroy(ridgidbody);
            }
            corpse.SendAsSnapshot(player.Connection);

            timer.Once(0.3f, () =>
            {
                StartLooting(player, targetplayer, corpse);
            });
        }

        private void StartLooting(BasePlayer player, BasePlayer targetplayer, LootableCorpse corpse)
        {
            player.inventory.loot.AddContainer(targetplayer.inventory.containerMain);
            player.inventory.loot.AddContainer(targetplayer.inventory.containerWear);
            player.inventory.loot.AddContainer(targetplayer.inventory.containerBelt);
            player.inventory.loot.entitySource = corpse;
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.MarkDirty();
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer<string>(null, player, "RPC_OpenLootPanel", "player_corpse");
            _viewingcorpse.Add(corpse);
            if (config.consolelogging)
                LogWarning($"{player.displayName}({player.userID}) is viewing the inventory of {targetplayer.displayName}({targetplayer.userID})");
        }

        #endregion Methods

        #region Hooks
        private void OnLootEntityEnd(BasePlayer player, LootableCorpse corpse)
        {
            if (!_viewingcorpse.Contains(corpse)) return;

            _viewingcorpse.Remove(corpse);
            if (corpse != null)
                corpse.Kill();

            if (config.discordlogging)
                player.StartCoroutine(LogToDiscord(player, player.inventory.loot.containers[0].playerOwner, corpse));

            if (_viewingcorpse.Count == 0)
                UnSubscribeFromHooks();

        }

        private Dictionary<LootableCorpse, List<Item>> _logtaken = new Dictionary<LootableCorpse, List<Item>>();
        private Dictionary<LootableCorpse, List<Item>> _loggiven = new Dictionary<LootableCorpse, List<Item>>();
        private object CanMoveItem(Item item, PlayerInventory playerInventory, ItemContainerId targetContainerId, int targetSlot, int amount)
        {
            BasePlayer player = playerInventory.baseEntity;
            if (player == null) return null;
            LootableCorpse corpse = (player.inventory.loot?.entitySource) as LootableCorpse;
            if (corpse == null) return null;
            if (!_viewingcorpse.Contains(corpse)) return null;
            if (corpse.HasFlag(BaseEntity.Flags.Locked) && !HasPerm(player.UserIDString, permunlock)) return false;
            if (config.discordlogging)
            {
                ItemContainer targetcon;
                if (targetContainerId == null)
                {
                    List<Item> takenlist;
                    if (_logtaken.TryGetValue(corpse, out takenlist))
                    {
                        takenlist.Add(item);
                    }
                    else
                    {
                        _logtaken[corpse] = new List<Item>() { item };
                    }
                    return null;
                }
                targetcon = player.inventory.FindContainer(targetContainerId);
                if (targetcon.GetOwnerPlayer() == player && item.parent.playerOwner != player)
                {
                    List<Item> takenlist;
                    if (_logtaken.TryGetValue(corpse, out takenlist))
                    {
                        takenlist.Add(item);
                    }
                    else
                    {
                        _logtaken[corpse] = new List<Item>() { item };
                    }
                    Item targetitem = targetcon.GetSlot(targetSlot);
                    if (targetitem != null)
                    {
                        List<Item> givenlist;
                        if (_loggiven.TryGetValue(corpse, out givenlist))
                        {
                            givenlist.Add(targetitem);
                        }
                        else
                        {
                            _loggiven[corpse] = new List<Item>() { targetitem };
                        }
                    }
                }
                else
                {
                    if (item.parent.playerOwner == player)
                    {
                        List<Item> givenlist;
                        if (_loggiven.TryGetValue(corpse, out givenlist))
                        {
                            givenlist.Add(item);
                        }
                        else
                        {
                            _loggiven[corpse] = new List<Item>() { item };
                        }
                        Item targetitem = targetcon.GetSlot(targetSlot);
                        if (targetitem != null)
                        {
                            List<Item> takenlist;
                            if (_logtaken.TryGetValue(corpse, out takenlist))
                            {
                                takenlist.Add(targetitem);
                            }
                            else
                            {
                                _logtaken[corpse] = new List<Item>() { targetitem };
                            }
                        }
                    }
                }
            }
            return null;
        }

        void OnEntityDeath(LootableCorpse corpse, HitInfo info)
        {
            if (!_viewingcorpse.Contains(corpse)) return;
            _viewingcorpse.Remove(corpse);
            if(corpse != null)
                corpse.Kill();
            if (_viewingcorpse.Count == 0)
                UnSubscribeFromHooks();
        }
        #endregion Hooks

        #region Helpers

        private IPlayer FindPlayer(string nameOrId)
        {
            IPlayer[] foundPlayers = players.FindPlayers(nameOrId).Where(p => p.IsConnected).ToArray();
            if (foundPlayers.Length > 1)
            {
                return null;
            }
            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                return null;
            }
            return target;
        }

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private string GetLang(string langKey, string playerId = null, params object[] args) => string.Format(lang.GetMessage(langKey, this, playerId), args);
        private void ChatMessage(IPlayer player, string langKey, params object[] args)
        {
            if (player.IsConnected) player.Message(GetLang(langKey, player.Id, args));
        }

        private void UnSubscribeFromHooks()
        {
            foreach (var hook in _registeredhooks)
                Unsubscribe(hook);
        }

        private void SubscribeToHooks()
        {
            foreach (var hook in _registeredhooks)
                Subscribe(hook);
        }
        #endregion Helpers

        #region Public Helpers
        public void _ViewInventory(BasePlayer basePlayer, BasePlayer targetPlayer)
        {
            if (!HasPerm(basePlayer.UserIDString, permuse)) return;
            ViewInventory(basePlayer, targetPlayer);
        }
        #endregion

        #region Old Methods
        private void ViewInventoryCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player.UserIDString, permuse)) return;
            IPlayer target = FindPlayer(args[0]);
            if (target == null)
            {
                ChatMessage(player.IPlayer, "NoPlayersFound", args[0]);
                return;
            }
            BasePlayer targetplayer = target.Object as BasePlayer;
            ViewInventory(player, targetplayer);
        }
        #endregion Old Methods

        #region Discord

        private IEnumerator LogToDiscord(BasePlayer viewer, BasePlayer viewing, LootableCorpse corpse)
        {
            var msg = DiscordMessage(viewer, viewing, corpse);
            string jsonmsg = JsonConvert.SerializeObject(msg);
            UnityWebRequest wwwpost = new UnityWebRequest(config.discordwebhook, "POST");
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonmsg.ToString());
            wwwpost.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
            wwwpost.SetRequestHeader("Content-Type", "application/json");
            yield return wwwpost.SendWebRequest();

            if (wwwpost.isNetworkError || wwwpost.isHttpError)
            {
                LogWarning(wwwpost.error);
                yield break;
            }
            wwwpost.Dispose();
        }

        private Message DiscordMessage(BasePlayer viewer, BasePlayer viewing, LootableCorpse corpse)
        {
            var fields = new List<Message.Fields>()
                {
                    new Message.Fields("Viewer: ", $"{viewer.displayName}({viewer.userID})", true),
                    new Message.Fields("Viewing: ", $"{viewing.displayName}({viewing.userID})", true),
                };
            string given = "";
            List<Item> givenlist;
            if (_loggiven.TryGetValue(corpse, out givenlist))
            {
                foreach(var i in givenlist)
                {
                    given += $"{i.amount} x {i.info.name}, ";
                }
                fields.Add(new Message.Fields("Items given: ", $"{given}", false));
                _loggiven.Remove(corpse);
            }
            string taken = "";
            List<Item> takenlist;
            if (_logtaken.TryGetValue(corpse, out takenlist))
            {
                foreach (var j in takenlist)
                {
                    taken += $"{j.amount} x {j.info.name}, ";
                }
                fields.Add(new Message.Fields("Items taken: ", $"{taken}", false));
                _logtaken.Remove(corpse);
            }
            var footer = new Message.Footer($"Logged @{DateTime.UtcNow:dd/MM/yy HH:mm:ss}");
            var embeds = new List<Message.Embeds>()
                {
                    new Message.Embeds("Server - " + ConVar.Server.hostname, "Inventory viewer log" , fields, footer)
                };
            Message msg = new Message(config.discordname, config.discordavatarurl, embeds);
            return msg;
        }

        public class Message
        {
            public string username { get; set; }
            public string avatar_url { get; set; }
            public List<Embeds> embeds { get; set; }

            public class Fields
            {
                public string name { get; set; }
                public string value { get; set; }
                public bool inline { get; set; }
                public Fields(string name, string value, bool inline)
                {
                    this.name = name;
                    this.value = value;
                    this.inline = inline;
                }
            }

            public class Footer
            {
                public string text { get; set; }
                public Footer(string text)
                {
                    this.text = text;
                }
            }

            public class Embeds
            {
                public string title { get; set; }
                public string description { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Embeds(string title, string description, List<Fields> fields, Footer footer)
                {
                    this.title = title;
                    this.description = description;
                    this.fields = fields;
                    this.footer = footer;
                }
            }

            public Message(string username, string avatar_url, List<Embeds> embeds)
            {
                this.username = username;
                this.avatar_url = avatar_url;
                this.embeds = embeds;
            }
        }

        #endregion Discord
    }
}