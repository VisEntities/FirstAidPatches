using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Rust;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using WebSocketSharp;
using Physics = UnityEngine.Physics;

/********************************************************
 *  Follow the status on https://trello.com/b/BCm6PUwK/guishop   __φ(．．)

 *  Credits to Nogrod and Reneb for the original plugin. <Versions up to 1.4.6
 *  Thanks! to Default for maintaining and adding in feature updates over the years.  <versions 1.4.65 to 1.5.9

 *  Current Maintainer: 8/14/2020 Khan#8615 discord ID.  1.6.0  to Present.
 *  Imported Auto Config Updater by WhiteThunder! ◝(⁰▿⁰)◜
 *  Thanks to Baz/Hockeygel/Whispers/Mr.Blue
 * -----------------------------------------------------------

 *  TODO:
 * Add Theme Switcher
 * Add BLuePrint Generation option
 * Add TC or Backpack option to store custom currency (seeking alternative)..
 * Update all Language Translations
 * Add support for Prerequisite item
 * Add Vending Machine Support
 *******************************************************/

/*****
* This release Update 2.1.0
 * Added NPC Checks
 * Updated to latest kits support but hook reversed back to default so was pointless
 * Added missing message response
 * Added Discord Embed Logging for Buy & Sell + Error logging
 *
 * This Update 2.2.0
 * Added SkinID support to custom currency
 * Added Custom Name support to custom currency
 * Added Shop Permissions
 * Slight Load Balance tweak to fix possible reload issues
 * Added Per shop currency options/else uses global defaults.
 * Updated German Lang file (Delete only the old Germ Lang file optional)
 * Added GetCurrency Hook
 * Updated OpenShop Hook
 *
 * This Update 2.2.1
 * Added Per Shop color options for both Display Name and Shop Descriptions
 * Added GUIShop Display Button on Left side. (Feature Request)
 * Added Button Permission
 * Right side config setting option
 * "Offsets Min": "185 18",
 * "Offsets Max": "245 78",
 *
 * This Update 2.2.2
 * Converted GUIShop Button to RGBA format Hex is glitched..
 *
 * This Update 2.2.3
 * Fixed Custom Items with Skin ID issues not being able to be sold.
 *
 * This Update 2.2.31
 * Fixed Custom Commands Issues
 * Added Null checks for custom shop currency id's being blank or null to prevent item/money loss!
 *
 * This update 2.2.40
 * Fully repaired ImageLibrary bugs
 * SkinId support is working properly now for Imagelibrary
 * Added Blueprint support for buy only! Cannot sell.
 * Added manual update command to update the Image URL links accordingly.
 * Update Command Requires Admin permission. is in game command only. 
 * Updated All Lang files to fix the Coal item name typo..
 *
 * This update 2.2.41
 * Added null check for items that cannot be made a blueprint.
 *
 * This update 2.2.42
 * This update fixes ImageLibrary Error on server reboot.
 *
 * This update 2.2.43
 * Patches multi command 
 * Slightly changed discord embed messages to fix multi command issues
 * Added discord Logging feature update to show the spawn cords
 * Added Old config option back {1, 10, 100, 1000} now is a global default setting if none of the others are set.
 * Now supports decimal numbers for economics. 0.33 etc
 * Added Command and Custom Item Buy support as the same item with a true/false toggle.
 * Added Toggle for creating the custom items as there DisplayNames
 *
 * This update 2.2.44 beta update
 * Added a cooldown setting to prevent players from being able to shop right away until x time has passed after each server wipe occurs.
 * Separated buy/sell all buttons into individual config toggle options.
 *
 * This update 2.2.45 beta update
 * Added Limiter Feature for Buy/Sell
 * Both will be enabled by default so set to false if you don't want them on!
 * If you set a cooldown on the item and a limit the cooldown will run each time -
 - they buy an item same for the sell-side they are both separate
 
 * This update 2.2.47 released.
 * Added a cooldown setting to prevent players from being able to shop right away until x time has passed after each server wipe occurs.
 * Separated buy/sell all buttons into individual config toggle options.
 * Added Limiter Feature for Buy/Sell
 * Both will be enabled by default so set to false if you don't want them on!
 * If you set a cooldown on the item and a limit the cooldown will run each time -
 - they buy an item same for the sell-side they are both separate
 * Added Buy/Sell Limit Reset CoolDown options
 * Fixed BUY All button problem caused from update 2.0.10 ( 7 months ago ).
 * Added limits.clear Console Command Requires Admin permission ( for guishop ).
 * clears entire data file for limits ( includes coodlown timers )
 * Added transactions.clear Console Command Requires Admin permission ( for guishop ).
 * clears entire data file for buy/sell cooldowns
 
 * This update 2.2.48
 * Fixed Sell All button issues relating to custom items with skinid's
 * Added Support for multiple npc's to be assigned to shops.
 *
 * This update 2.3.0 / Major HotFix Update!
 * Fixed item leak when taking items from players
 * Fixed Reloading issues where ram usage would keep being taken up from Image Library calls.
 * No longer uses //requires ( due to issues with it )
 * Updated Currency Systems
 * Created Whole new Item Creation methods
 * Created Whole new Item Give methods
 * Updated/re-wrote Get and Take Methods to include new feature support + fix old feature support checks
 * Fixed issues with buying commands when inventory was full
 * Fixed issues with buying items when command strings were not null but empty
 * Fixed Custom Item names not being applied to items bought correctly
 * Updated permission name error response to catch other plugins that improperly code..
 * Preparations for the new HumanNPC plugin in development finished
 * Preparations for the Nomad Shop Plugin NPC done
 * Added Requested Hooks
 * Added Condition Level Support - Condition levels that default to 0 are not supported.
 * Added Allow Or Not Allow selling of used Items
 * All items that are broken are no longer counted for selling in guishop! 
 * All items are preset to max default condition values. ( changing these to 0 will result in items being bought as broken items )
 * Updated Updater Command please run it in-game and make sure you have the guishop.admin perm! /update
 * ( Updater command now only checks for any item condition set to 0 and will auto add the correct value for it if it supports conditions )
 * Added a /resetconditions command for those that need to reset only item config conditions back to defaults.
 * Added Missing Sunglasses to item generation list
 * Adding pooling for UI creation for better performance and faster page switching
 * Added LangAPI translation support for all 30 langs!
 * Added Russian support
 * Updated/fixed all 10 language translations
 * Fixed All Button Error Response issues / limit triggers
 * Added Full genetics support fer buyin' plants & seeds.
 * Added missing lang responses
 * Fixed GUIShop permission issues
 *
 * Update 2.3.1
 * Fixed Player Message Response not showing relating to npcs and shop permissions.
 * Fixed Transparency changer buttons when using NPC shops
 *
 * Update 2.3.2
 * Added New config option and increased Library check count to 15.
 * Fixed players currency money not showing correctly specific to custom currencies per shop.
 * performance update on get method /
 * Fixed message response for custom currencies
 *

 * From version 2.3.2 to 2.3.15, The following issues have been fixed.

Note: If you have not yet updated to 2.3.1 or higher you need to run the /update
* command in-game so that your configs get updated with the latest feature additions/requests.
* Fixed Button Feature update. ( Was missing a page sometimes )
* Substantially reduced Image Library load order times.
* Added a new page switching feature the forward/back buttons will only show 
if there is more than 1 page and if you are not on the last page.

* Fixed Economics bug when trying to use 0.01 as a currency amount for selling.
* Fixed an issue when buying if a player's inventory is full 
it would send the message response saying your inventory is full, not enough space.
But it would still take the money and not give them anything.
* Resolves Custom Stack Size issues
* when using Stack Size Controller ( would end up with more )
* when using stack modifier ( would end up with less sometimes ) etc
* Fixed Another Possible Item Leak problem / ( if an item was not null but failed )
* Fixed shops set to use "custom" and a player attempts to buy something 
with 0 scrap outputting wrong msg response.

* Updated /shop-specific command checks, in regards to permission-based shops.
* Code Cleanup.
* Fixed an issue that occurs specifically with odd server stack sizes.
* Relating to buying items in bulk, players would end up with 1 or two extra sometimes.
( IDK how this didn't get reported months ago as an issue )
* Fixed selling for items with 100% condition and only = 1
Complete list from 2.2.48 to latest release of changes/repairs can be found here
https://umod.org/community/gui-shop/40000-2248-to-2315-update-notes

 * This Update 2.3.16
 * Updated to ignore 3rd party plugin npcs trying to access the shops.
 *
 * Update 2.3.17
 * Added auto wipe for data files on new map saves. ( default is true )
 * Added Experimental Economics support for beta update pending.
 *
 * Update 2.3.18
 * Updated TryGiveChecks work around for fixing snowmobiles not spawning via commands
 *
 * update 2.3.191
 * Fixed limit count starting wrong
 * Fixed A Currency Bug
 * Fixed msg
 *
 * update 2.3.192
 * Fixed selling items that had condition values below 80% not being able to be sold when enabled true.
 *
 * Update 2.3.193
 * Fixed items with custom skin ids not updating / showing correctly inside the UI!
 *
 * Update 2.4.0
 * Now Using Native Facepunch logic for fetching default images + skins for shop items
 * Only using ImageLibrary for very limited things
 * Massive performance boost!
 * Fixed Shop Color Changing / Transparency Changing Bugs not remembering your last Shop TAB
 * Fixed Some Images Not updating in very special use cases
 * Fixed Image Library not loading the order properly after a server restarts
 * Improved Message response outputs in console when / if image library is being waited on/for.
 * Huge performance improvements for when you first load onto a server && when reloading the plugin
 * No longer causes performance/lag spikes on high pop servers when re-loading
 *
 * Update 2.4.2
 * Added /updateold ( in game chat command requires guishop.admin)
for really old configs that did not follow the old update notes from like 11months back??

 * Fixed Default shop to open not being displayed correctly, and just opening the first shop in the list to show player.
 *
 * Update 2.4.3
 * Updated UI Display for Limit Counts so players can see the counter values change now
 * Updated Config support scenarios to expand deeper scenario settings
 * Updated Limits code to support new features
 * Updated UI Systems for showing players by caching as much as possible
 * UI System no longer constantly free's up the pooling resulting in the pool having to be re-created each time increasing garbage collections etc.
 * Higher Pop server should notice some more performance gains the most.
 *
 * Update 2.4.4
 * Added New Experimental Config Options SwapLimitToQuantityBuyLimit & SwapLimitToQuantitySoldLimit
 * Toggling True results in buy quantities subtracting from the total Limit Count instead of subtracting 1
 * Updated all cooldown logic & limits again
 * Added Emergency Hotfix for F1 Commands exploit!
 *
 * Update 2.4.41
 * Now Double Checks Data file systems on Load & Unload
 * Now Properly Checks NPC Ranges for F1 commands on shops with only NPCs setup on them
 * Now Properly Handles F1 Commands and respects shop categories settings + shop item settings
 * F1 Buy / Sell All Functions are now blocked
 * F1 transaction amount support can be specified by the player & respects the currency values
 * F1 Transactions should properly reset cooldowns on items as well without having to open the UI
 * If all global shops are disabled & players have GUIShop.Button perm it will no longer show
 *
 * update 2.4.42
 * fixed /shop command erroring in console if all shop categories have been disabled.
 * Added New config toggle [JsonProperty("Shop - GUIShop Enable Background Image")]
public bool BackgroundImage = true;
 * Added up to 16 shop tabs in a single row now & supports 2 rows now totalling 32 shops
 *
 * Update 2.4.43
 * Fixed an issue with 2 config toggle settings relating to SR & E
 * Updated UI System to support 2 more shop fields & cleaned up generation code a bit
 * Added up to 17 shop tabs in a single row now & supports 2 rows now totalling 34! shops
 * Couple minor bug fixes where for the shop was null in a weird use case.
 * Fixed a msg response saying bought 0 even though you got the items & it took the money properly.
 *
 * Update 2.4.44
 * Fixed GUIShop button & updated some text.
*/

namespace Oxide.Plugins
{
    [Info("GUIShop", "Khan", "2.4.45")]
    [Description("GUI Shop Supports all known Currency, with NPC support - Re-Write Edition 2")]
    public class GUIShop : RustPlugin
    {
        #region References

        [PluginReference] Plugin Economics, Kits, ImageLibrary, ServerRewards, LangAPI;

        #endregion

        #region Fields

        private bool _isRestart = true;
        private bool _isShopReady;
        private bool _isLangAPIReady;
        private bool _isEconomicsLimits;
        private bool _isEconomicsDebt;
        private KeyValuePair<double, double> _balanceLimits;
        private Dictionary<string, string> _imageListGUIShop;
        private List<KeyValuePair<string, ulong>> _guishopItemIcons;

        private const string GUIShopOverlayName = "GUIShopOverlay";
        private const string GUIShopContentName = "GUIShopContent";
        private const string GUIShopDescOverlay = "GUIShopDescOverlay";
        private const string GUIShopColorPicker = "GUIShopColorPicker";
        private const string BlockAllow = "guishop.blockbypass";
        private const string Use = "guishop.use";
        private const string Admin = "guishop.admin";
        //private const string Vip = "guishop.vip";
        private const string Color = "guishop.color";
        private const string Button = "guishop.button";

        private int _imageLibraryCheck = 0;
        private Hash<ulong, int> _shopPage = new Hash<ulong, int>();
        private Dictionary<ulong, Dictionary<string, double>> _sellCoolDownData;
        private Dictionary<ulong, Dictionary<string, double>> _buyCooldownData;
        private Dictionary<ulong, Dictionary<string, double>> _buyLimitResetCoolDownData;
        private Dictionary<ulong, Dictionary<string, double>> _sellLimitResetCoolDownData;
        private Dictionary<string, ulong> _boughtData;
        private Dictionary<string, ulong> _soldData;
        private Dictionary<ulong, ItemLimit> _limitsData;
        readonly Dictionary<string, string> _headers = new Dictionary<string, string> {{"Content-Type", "application/json"}};
        private List<MonumentInfo> _monuments => TerrainMeta.Path.Monuments;
        private bool _configChanged;
        private int playersMask = LayerMask.GetMask("Player (Server)");

        //Caching Shop Images.
        private const string GUIShopWelcomeImage = "GUIShopWelcome";
        private const string GUIShopBackgroundImage = "GUIShopBackground";
        private const string GUIShopAmount1Image = "GUIShopAmount1";
        private const string GUIShopAmount2Image = "GUIShopAmount2";
        private const string GUIShopBuyImage = "GUIShopBuy";
        private const string GUIShopSellImage = "GUIShopSell";
        private const string GUIShopBackArrowImage = "GUIShopBackArrow";
        private const string GUIShopForwardArrowImage = "GUIShopForwardArrow";
        private const string GUIShopCloseImage = "GUIShopClose";
        private const string GUIShopShopButton = "GUIShopButton";

        //Auto Close
        private HashSet<string> playerGUIShopUIOpen = new HashSet<string>();

        private Dictionary<string, PlayerUISetting> _playerUIData;
        private string _uiSettingChange = "Text";
        private bool _imageChanger;
        private double Transparency = 0.95;
        private PluginConfig _config;
        private static GUIShop _instance;

        //Shop Button
        private HashSet<string> _playerDisabledButtonData;

        private readonly Dictionary<string, string> _corrections = new Dictionary<string, string>
        {
            {"sunglasses02black", "Sunglasses Style 2"},
            {"sunglasses02camo", "Sunglasses Camo"},
            {"sunglasses02red", "Sunglasses Red"},
            {"sunglasses03black", "Sunglasses Style 3"},
            {"sunglasses03chrome", "Sunglasses Chrome"},
            {"sunglasses03gold", "Sunglasses Gold"},
            {"twitchsunglasses", "Sunglasses Purple"},
            {"innertube", "Inner Tube"},
            {"innertube.horse", "Inner Tube Horse"},
            {"innertube.unicorn", "Inner Tube Unicorn"},
        };

        #endregion

        #region Config

        private readonly HashSet<string> _exclude = new HashSet<string>
        {
            "vehicle.chassis",
            "vehicle.module"
        };

        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Carefully Edit This")]
            public CarefullyEdit Time = new CarefullyEdit();
            
            [JsonProperty("Wipe GUIShop Data files on Map Changes / Server Wipes & if server save file is deleted")]
            public bool AutoWipe = true;

            [JsonProperty("Sets the ImageLibrary Counter Check ( Set higher if needed for server restarts )")]
            public int ImageLibraryCounter = 15;

            [JsonProperty("Enable Discord Buy Transaction Logging")]
            public bool EnableDiscordLogging;

            [JsonProperty("Enable Discord Sell Transaction Logging")]
            public bool EnableDiscordSellLogging;

            [JsonProperty("Discord Webhook URL")]
            public string DiscordWebHookURL = "";

            [JsonProperty("Discord Embed Color")]
            public string DiscordColor = "#483D8B";

            [JsonProperty("Discord Author Image")]
            public string DiscordAuthorImage = "https://assets.umod.org/images/icons/plugin/5f80fe12851f5.png";

            [JsonProperty("Discord Embed Icon")] 
            public string DiscordAuthorName = "GUIShop";

            [JsonProperty("Set Default Global Shop to open")]
            public string DefaultShop = "Commands";

            [JsonProperty("Sets shop command")] 
            public string shopcommand = "shop";

            [JsonProperty("Sets Vehicle Spawn Distance")]
            public float SpawnDistance = 15f;

            [JsonProperty("Switches to Economics as default curency")]
            public bool Economics = true;

            [JsonProperty("Switches to ServerRewards as default curency")]
            public bool ServerRewards = false;

            [JsonProperty("Switches to Custom as default curency")]
            public bool CustomCurrency = false;

            [JsonProperty("Allow Custom Currency Sell Of Used Items")]
            public bool CustomCurrencyAllowSellOfUsedItems;

            [JsonProperty("Custom Currency Item ID")]
            public int CustomCurrencyID = -932201673;

            [JsonProperty("Custom Currency Skin ID")]
            public ulong CustomCurrencySkinID = 0;

            [JsonProperty("Custom Currency Name")]
            public string CustomCurrencyName = "";

            [JsonProperty("Allows you to specify which containers you can sell items from")]
            public InventoryTypes AllowedSellContainers = InventoryTypes.ContainerAll;

            [JsonProperty("Enable Shop Buy All Button")]
            public bool BuyAllButton = true;

            [JsonProperty("Enable Shop Sell All Button")]
            public bool SellAllButton = true;

             [JsonProperty("Sets the buy/Sell button amounts + how many buttons")]
             public int[] steps = { 1, 10, 100, 1000 };

            [JsonProperty("Player UI display")] 
            public bool PersonalUI = false;

            [JsonProperty("Block Monuments")] 
            public bool BlockMonuments = false;

            [JsonProperty("If true = Images, If False = Text Labels")]
            public bool UIImageOption = false;

            [JsonProperty("NPC Distance Check")] 
            public float NPCDistanceCheck = 2.5f;

            [JsonProperty("Enable NPC Auto Open")] 
            public bool NPCAutoOpen = false;

            [JsonProperty("Enable GUIShop NPC Msg's")]
            public bool NPCLeaveResponse = false;

            [JsonProperty("GUI Shop - Welcome MSG")]
            public string WelcomeMsg = "WELCOME TO GUISHOP ◝(⁰▿⁰)◜";

            [JsonProperty("Shop - Buy Price Label")]
            public string BuyLabel = "Buy Price";

            [JsonProperty("Shop - Amount1 Label1")]
            public string AmountLabel = "Amount";

            [JsonProperty("Shop - Sell $ Label")] 
            public string SellLabel = "Sell $";

            [JsonProperty("Shop - Amount2 Label2")]
            public string AmountLabel2 = "Amount";

            [JsonProperty("Shop - Back Button Text")]
            public string BackButtonText = "<<";

            [JsonProperty("Shop - Forward Button Text")]
            public string ForwardButtonText = ">>";

            [JsonProperty("Shop - Close Label")] 
            public string CloseButtonlabel = "CLOSE";

            [JsonProperty("Shop - GUIShop Welcome Url")]
            public string GuiShopWelcomeUrl = "https://i.imgur.com/RcLdEly.png";
            
            [JsonProperty("Shop - GUIShop Enable Background Image")]
            public bool BackgroundImage = true;

            [JsonProperty("Shop - GUIShop Background Image Url")]
            public string BackgroundUrl = "https://i.imgur.com/Jej3cwR.png";

            [JsonProperty("Shop - Sets any shop items to this image if image link does not exist.")]
            public string IconUrl = "https://imgur.com/BPM9UR4.png";

            [JsonProperty("Shop - Shop Buy Icon Url")]
            public string BuyIconUrl = "https://imgur.com/oeVUwCy.png";

            [JsonProperty("Shop - Shop Amount Left Url")]
            public string AmountUrl = "https://imgur.com/EKtvylU.png";

            [JsonProperty("Shop - Shop Amount Right Url")]
            public string AmountUrl2 = "https://imgur.com/EKtvylU.png";

            [JsonProperty("Shop - Shop Sell Icon Url")]
            public string SellIconUrl = "https://imgur.com/jV3hEHy.png";

            [JsonProperty("Shop - Shop Back Arrow Url")]
            public string BackButtonUrl = "https://imgur.com/zNKprM1.png";

            [JsonProperty("Shop - Shop Forward Arrow Url")]
            public string ForwardButtonUrl = "https://imgur.com/qx9syT5.png";

            [JsonProperty("Shop - Close Image Url")]
            public string CloseButton = "https://imgur.com/IK5yVrW.png";

            [JsonProperty("Shop GUI Button")]
            public GUIButton GUI = new GUIButton();

            [JsonProperty("GUIShop Configurable UI colors (First 8 Colors!)")]
            public HashSet<string> ColorsUI = new HashSet<string>();

            [JsonProperty("Set Default Shop Buy Color")]
            public string BuyColor = "#FFFFFF";

            [JsonProperty("Set Default Shop Sell Color")]
            public string SellColor = "#FFFFFF";

            [JsonProperty("Set Default Shop Text Color")]
            public string TextColor = "#FFFFFF";

            [JsonProperty("Was Saved Don't Touch!")]
            public bool WasSaved;

            [JsonProperty("Shop - Shop Categories")]
            public Dictionary<string, ShopCategory> ShopCategories = new Dictionary<string, ShopCategory>();

            [JsonProperty("Shop - Shop List")] 
            public Dictionary<string, ShopItem> ShopItems = new Dictionary<string, ShopItem>();

            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public class ShopItem
        {
            public string DisplayName;
            public bool CraftAsDisplayName = false;
            public string Shortname;
            public int ItemId;
            public bool MakeBlueprint = false;
            public bool AllowSellOfUsedItems = false;
            public float Condition;
            public bool EnableBuy = true;
            public bool EnableSell = true;
            public string Image = "";
            public double SellPrice;
            public double BuyPrice;
            public int BuyCooldown;
            public int SellCooldown;
            public int[] BuyQuantity = {};
            public int[] SellQuantity = {};

            //public string Prerequisite;
            public int BuyLimit = 0;
            public int BuyLimitResetCoolDown = 0;
            public bool SwapLimitToQuantityBuyLimit;
            public int SellLimit = 0;
            public int SellLimitResetCoolDown = 0;
            public bool SwapLimitToQuantitySoldLimit;
            public string KitName = "";
            public List<string> Command = new List<string>();
            public bool RunCommandAndCustomShopItem = false;
            public List<char> GeneTypes = new List<char>();
            public ulong SkinId;

            public double GetSellPrice(string playerId="")
            {
                var hook = Interface.CallHook("GUIShopSellPrice", JObject.FromObject(this), (string)playerId);
                return hook != null ? (double)hook : SellPrice;
            }

            public double GetBuyPrice(string playerId="")
            {
                var hook = Interface.CallHook("GUIShopBuyPrice", JObject.FromObject(this), (string)playerId);
                return hook != null ? (double)hook : BuyPrice;
            }
        }

        public class ShopCategory
        {
            public string DisplayName;
            public string DisplayNameColor = null;
            public string Description;
            public string DescriptionColor = null;
            public string Permission = "";
            [JsonIgnore]
            public string PrefixPermission => "guishop." + Permission;
            public string Currency = "";
            public bool CustomCurrencyAllowSellOfUsedItems;
            public string CustomCurrencyNames = "";
            public int CustomCurrencyIDs = -0;
            public ulong CustomCurrencySkinIDs = 0;
            public bool EnabledCategory;

            //public bool BluePrints;
            public bool EnableNPC;
            public string NPCId = "";
            public HashSet<string> NpcIds = new HashSet<string>();
            public HashSet<string> Items = new HashSet<string>();
            [JsonIgnore] public HashSet<ShopItem> ShopItems;
        }

        public class ItemLimit
        {
            public Dictionary<string, int> BLimit = new Dictionary<string, int>();
            public Dictionary<string, int> SLimit = new Dictionary<string, int>();
            public bool CheckSellLimit(string item, int amount)
            {
                if (!SLimit.ContainsKey(item))
                {
                    SLimit[item] = 0;
                }

                return SLimit[item] >= amount;
            }
            public void IncrementSell(string item, int amount, bool toggle)
            {
                if (!toggle)
                    SLimit[item]++;
                else
                    SLimit[item] += amount;
            }

            public bool CheckBuyLimit(string item, int amount)
            {
                if (!BLimit.ContainsKey(item))
                {
                    BLimit[item] = 0;
                }

                return BLimit[item] >= amount;
            }
            public void IncrementBuy(string item, int amount, bool toggle)
            {
                if (!toggle)
                    BLimit[item]++;
                else
                    BLimit[item] += amount;
            }
        }

        private class PlayerUISetting
        {
            public double Transparency;
            public string SellBoxColors;
            public string BuyBoxColors;
            public string UITextColor;
            public double RangeValue;
            public bool ImageOrText;
            public string ShopKey;
        }

        public class GUIButton
        {
            [JsonProperty(PropertyName = "Image")]
            public string Image = "https://i.imgur.com/hc0qPet.png";

            [JsonProperty(PropertyName = "Background color (RGBA format)")]
            public string Color = "1 0.96 0.88 0.15";

            [JsonProperty(PropertyName = "GUI Button Position")]
            public Position GUIButtonPosition = new Position();
            public class Position
            {
                [JsonProperty(PropertyName = "Anchors Min")]
                public string AnchorsMin = "0.5 0.0";

                [JsonProperty(PropertyName = "Anchors Max")]
                public string AnchorsMax = "0.5 0.0";

                [JsonProperty(PropertyName = "Offsets Min")]
                public string OffsetsMin = "-265 18";

                [JsonProperty(PropertyName = "Offsets Max")]
                public string OffsetsMax = "-205 78";
            }
        }

        public class CarefullyEdit
        {
            public string WipeTime = DateTime.Now.ToString("u");
            public string LastWipe = DateTime.Now.ToString("u");
            [JsonProperty("Sets time before shops can be used after the server wipes")]
            public float CanShopIn = 300f;
        }

        private void CheckConfig()
        {
            if (!_config.ShopCategories.ContainsKey("Commands"))
            {
                _config.ShopCategories.Add("Commands", new ShopCategory
                {
                    DisplayName = "Commands",
                    DisplayNameColor = null,
                    Description = "You currently have {0} coins to spend in the commands shop",
                    DescriptionColor = null,
                    Permission = "",
                    Currency = "",
                    CustomCurrencyNames = "",
                    CustomCurrencyIDs = -0,
                    CustomCurrencySkinIDs = 0,
                    EnabledCategory = true,
                    //BluePrints = false
                });
                //_configChanged = true;
            }

            if (_config.ShopCategories.ContainsKey("Commands") && !_config.ShopItems.ContainsKey("Minicopter") &&
                !_config.ShopItems.ContainsKey("Sedan") && !_config.ShopItems.ContainsKey("Airdrop Call"))
            {
                _config.ShopItems.Add("Minicopter", new ShopItem
                {
                    DisplayName = "Minicopter",
                    Shortname = "minicopter",
                    CraftAsDisplayName = false,
                    MakeBlueprint = false,
                    EnableBuy = true,
                    EnableSell = false,
                    Image = "https://i.imgur.com/vI6LwCZ.png",
                    Condition = 0,
                    BuyPrice = 1.0,
                    SellPrice = 1.0,
                    BuyCooldown = 0,
                    SellCooldown = 0,
                    BuyQuantity = {},
                    SellQuantity = {},
                    BuyLimit = 0,
                    BuyLimitResetCoolDown = 0,
                    SellLimit = 0,
                    SellLimitResetCoolDown = 0,
                    KitName = "",
                    Command = new List<string> {"spawn minicopter \"$player.x $player.y $player.z\""},
                    RunCommandAndCustomShopItem = false,
                    SkinId = 0,
                });

                _config.ShopItems.Add("Sedan", new ShopItem
                {
                    DisplayName = "Sedan",
                    Shortname = "sedan",
                    CraftAsDisplayName = false,
                    MakeBlueprint = false,
                    EnableBuy = true,
                    EnableSell = false,
                    Image = "",
                    Condition = 0,
                    BuyPrice = 1.0,
                    SellPrice = 1.0,
                    BuyCooldown = 0,
                    SellCooldown = 0,
                    BuyQuantity = {},
                    SellQuantity = {},
                    BuyLimit = 0,
                    BuyLimitResetCoolDown = 0,
                    SellLimit = 0,
                    SellLimitResetCoolDown = 0,
                    KitName = "",
                    Command = new List<string> {"spawn sedan \"$player.x $player.y $player.z\""},
                    RunCommandAndCustomShopItem = false,
                    SkinId = 0,
                });

                _config.ShopItems.Add("Airdrop Call", new ShopItem
                {
                    DisplayName = "Airdrop Call",
                    Shortname = "airdrop.call",
                    ItemId = 1397052267,
                    CraftAsDisplayName = false,
                    MakeBlueprint = false,
                    EnableBuy = true,
                    EnableSell = false,
                    Image = "",
                    Condition = 0,
                    BuyPrice = 1.0,
                    SellPrice = 1.0,
                    BuyCooldown = 0,
                    SellCooldown = 0,
                    BuyQuantity = {},
                    SellQuantity = {},
                    BuyLimit = 0,
                    BuyLimitResetCoolDown = 0,
                    SellLimit = 0,
                    SellLimitResetCoolDown = 0,
                    KitName = "",
                    Command = new List<string> {"inventory.giveto $player.id supply.signal"},
                    RunCommandAndCustomShopItem = false,
                    SkinId = 0,
                });

                _config.ShopCategories["Commands"].Items.Add("Minicopter");
                _config.ShopCategories["Commands"].Items.Add("Sedan");
                _config.ShopCategories["Commands"].Items.Add("Airdrop Call");
            }

            foreach (var item in _config.ShopItems.Values)
            {
                if (item.ItemId == 0 && !string.IsNullOrEmpty(item.Shortname) && ItemManager.FindItemDefinition(item.Shortname) != null)
                {
                    item.ItemId = ItemManager.FindItemDefinition(item.Shortname).itemid;
                    _configChanged = true;
                }
            }

            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string categoryName = item.category.ToString();

                ShopCategory shopCategory;

                if (!_config.ShopCategories.TryGetValue(categoryName, out shopCategory))
                {
                    _config.ShopCategories[categoryName] = shopCategory = new ShopCategory
                    {
                        DisplayName = item.category.ToString(),
                        DisplayNameColor = null,
                        Description = "You currently have {0} coins to spend in the " + item.category + " shop",
                        DescriptionColor = null,
                        Permission = "",
                        Currency = "",
                        CustomCurrencyAllowSellOfUsedItems = false,
                        CustomCurrencyNames = "",
                        CustomCurrencyIDs = -0,
                        CustomCurrencySkinIDs = 0,
                        EnabledCategory = true,
                        //BluePrints = false
                    };

                    //_configChanged = true;
                }

                string displayname = _corrections.ContainsKey(item.shortname) ? _corrections[item.shortname] : item.displayName.english;

                if (_exclude.Contains(item.shortname)) continue;

                if (!shopCategory.Items.Contains(displayname) && !_config.WasSaved)
                {
                    shopCategory.Items.Add(displayname);

                    _configChanged = true;
                }

                if (_config.ShopItems.ContainsKey(displayname) && _config.ShopItems[displayname].ItemId == 0)
                {
                    _config.ShopItems[displayname].ItemId = item.itemid;
                    _configChanged = true;
                }

                if (!_config.ShopItems.ContainsKey(displayname))
                {
                    _config.ShopItems.Add(displayname, new ShopItem
                    {
                        DisplayName = displayname,
                        CraftAsDisplayName = false,
                        Shortname = item.shortname,
                        MakeBlueprint = false,
                        AllowSellOfUsedItems = false,
                        Condition = item.condition.max,
                        EnableBuy = true,
                        EnableSell = true,
                        Image = "",
                        BuyPrice = 1.0,
                        SellPrice = 1.0,
                        BuyCooldown = 0,
                        SellCooldown = 0,
                        BuyQuantity = {},
                        SellQuantity = {},
                        BuyLimit = 0,
                        BuyLimitResetCoolDown = 0,
                        SellLimit = 0,
                        SellLimitResetCoolDown = 0,
                        KitName = "",
                        Command = new List<string>(),
                        RunCommandAndCustomShopItem = false,
                        SkinId = 0,
                        // Image = "https://rustlabs.com/img/items180/" + item.shortname + ".png"
                    });
                    _configChanged = true;
                }
            }

            if (_config.ColorsUI.Count <= 0)
            {
                _config.ColorsUI = new HashSet<string> {"#A569BD", "#2ECC71", "#E67E22", "#3498DB", "#E74C3C", "#F1C40F", "#F4F6F7", "#00FFFF"};
            }

            foreach (var key in _config.ShopCategories.Values)
            {
                if (!string.IsNullOrEmpty(key.NPCId))
                {
                    if (!key.NpcIds.Contains(key.NPCId))
                        key.NpcIds.Add(key.NPCId);

                    PrintWarning($"Auto Moved, Warning! This config option is being removed soon do not use! NPCId");
                    key.NPCId = String.Empty;
                    _configChanged = true;
                }
            }

            if (_configChanged)
            {
                _config.WasSaved = true;
                SaveConfig();
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    PrintWarning($"Generating Config File for GUIShop");
                    PrintToConsole($"Generating Config File for GUIShop");
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
            }
        }

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #region Updater

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
                        return ((JValue) token).Value;
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

        #endregion

        #region Storage

        private void LoadData()
        {
            try
            {
                _buyCooldownData = _buyCoolDowns.ReadObject<Dictionary<ulong, Dictionary<string, double>>>();
            }
            catch
            {
                _buyCooldownData = new Dictionary<ulong, Dictionary<string, double>>();
            }

            try
            {
                _sellCoolDownData = _sellCoolDowns.ReadObject<Dictionary<ulong, Dictionary<string, double>>>();
            }
            catch
            {
                _sellCoolDownData = new Dictionary<ulong, Dictionary<string, double>>();
            }
            
            try
            {
                _buyLimitResetCoolDownData = _buyLimitResetCoolDowns.ReadObject<Dictionary<ulong, Dictionary<string, double>>>();
            }
            catch
            {
                _buyLimitResetCoolDownData = new Dictionary<ulong, Dictionary<string, double>>();
            }
            
            try
            {
                _sellLimitResetCoolDownData = _sellLimitResetCoolDowns.ReadObject<Dictionary<ulong, Dictionary<string, double>>>();
            }
            catch
            {
                _sellLimitResetCoolDownData = new Dictionary<ulong, Dictionary<string, double>>();
            }

            try
            {
                _boughtData = _bought.ReadObject<Dictionary<string, ulong>>();
            }
            catch
            {
                _boughtData = new Dictionary<string, ulong>();
            }

            try
            {
                _soldData = _sold.ReadObject<Dictionary<string, ulong>>();
            }
            catch
            {
                _soldData = new Dictionary<string, ulong>();
            }

            try
            {
                _limitsData = _limits.ReadObject<Dictionary<ulong, ItemLimit>>();
            }
            catch
            {
                _limitsData = new Dictionary<ulong, ItemLimit>();
            }

            try
            {
                _playerUIData = _playerData.ReadObject<Dictionary<string, PlayerUISetting>>();
            }
            catch
            {
                _playerUIData = new Dictionary<string, PlayerUISetting>();
            }

            try
            {
                _playerDisabledButtonData = _buttonData.ReadObject<HashSet<string>>();
            }
            catch
            {
                _playerDisabledButtonData = new HashSet<string>();
            }
        }

        private void SaveData()
        {
            _buyCoolDowns.WriteObject(_buyCooldownData);
            _sellCoolDowns.WriteObject(_sellCoolDownData); 
            _buyLimitResetCoolDowns.WriteObject(_buyLimitResetCoolDownData);
            _sellLimitResetCoolDowns.WriteObject(_sellLimitResetCoolDownData);
            _bought.WriteObject(_boughtData);
            _sold.WriteObject(_soldData);
            _limits.WriteObject(_limitsData);
            _playerData.WriteObject(_playerUIData);
            _buttonData.WriteObject(_playerDisabledButtonData);
        }

        #endregion

        #region Lang File Messages

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"ShopInvalid", "This is not a shop <color=#32CD32>{0}</color> update default shop setting!\nAvailable Shops are:\n{1}"},
                {"Economics", "GUIShop did not recieve a response from the Economics plugin. Please ensure the Economics plugin is installed correctly."},
                {"EconomicsMaxDebt", "Transaction declined you are already at max economics debt"},
                {"EconomicsExceedsDebt", "Transaction declined this would exceed max economics debt limit of {0}"},
                {"CustomInvalidID", "Custom currency ID is not valid please update config for this shop {0}"},
                {"MaxEconomicsBalance", "Transaction declined your already at max economics limit"},
                {"EconomicsExceedsLimit", "Transaction declined this would exceed max earnable economics limit of {0}"},
                {"ServerRewards", "GUIShop did not recieve a response from the ServerRewards plugin. Please ensure the ServerRewards plugin is installed correctly."},
                {"TakeCurrency", "GUIShop has failed to take this currency upon purchase {0}"},
                {"Bought", "You've successfully bought {0} {1}."},
                {"Sold", "You've successfully sold {0} {1}."},
                {"Cooldown", "You can only purchase this item every {0} seconds."},
                {"InventoryFull", "Your inventory is full."},
                {"InventorySlots", "You need at least {0} free inventory slots."},
                {"ErrorShop", "There is something wrong with this shop. Please contact an admin."},
                {"GlobalShopsDisabled", "Global Shops are disabled. This server uses NPC vendors!"},
                {"DeniedActionInShop", "You are not allowed to {0} in this shop"},
                {"ShopItemItemInvalid", "WARNING: It seems like this sell item you have is not a valid item! Please contact an Admin!"},
                {"ItemNotValidbuy", "WARNING: It seems like it's not a valid item to buy, Please contact an Admin!"},
                {"ItemNotValidsell", "WARNING: It seems like it's not a valid item to sell, Please contact an Admin!"},
                {"RedeemKitFail", "WARNING: There was an error while giving you this kit, Please contact an Admin!"},
                {"NotKit", "This is not a valid kit name assigned to this shop item: {0}"},
                {"BuyCmd", "Can't buy multiple of this item!"},
                {"SellCmd", "Can't sell multiple of this item!"},
                {"BuyPriceFail", "WARNING: No buy price was given by the admin, you can't buy this item"},
                {"SellPriceFail", "WARNING: No sell price was given by the admin, you can't sell this item"},
                {"NotEnoughMoney", "You need {0} coins to buy {1}x {2}"},
                {"NotEnoughMoneyCustom", "You need {0} currency to buy {1}x {2}"},
                {"CustomCurrencyFail", "WANRING Admin has this shop called {0} set as custom currency but has not set a valid currency ID {1}"},
                {"NotEnoughSell", "You don't have enough of this item."},
                {"NotNothingShopFail", "You cannot buy Zero of this item."},
                {"ItemNoExist", "WARNING: The item you are trying to buy doesn't seem to exist! Please contact an Admin!"},
                {"ItemNoExistTake", "The item you are trying to sell is not sellable at this time."},
                {"ItemIsNotBlueprintable", "This shop item {0} cannot be set as a Blueprint!"},
                {"BuildingBlocked", "You cannot shop while in a building blocked area. "},
                {"BlockedMonuments", "You may not use the shop while near a Monument!"},
                {"ItemNotEnabled", "The shop keeper has disabled this item."},
                {"ItemNotFound", "Item was not found"},
                {"CantSellCommands", "You can not sell Commands back to the shop."},
                {"CantSellKits", "You can not sell Kits back to the shop."},
                //{"CannotSellWhileEquiped", "You can not sell the item if you have it Equipt."},
                {"GUIShopResponse", "GUIShop is waiting for ImageLibrary & LangAPI downloads to finish please wait."},
                {"NPCResponseClose", "Thanks for shopping at {0} come again soon!"},
                {"NPCResponseOpen", "Welcome to the {0} what would you like to purchase? Press E to start shopping!"},
                {"NoPerm", "You do not have permission to shop at {0}"},
                {"WipeReady", "Dear {0}, all shops are closed for \n {1} minutes"},
                {"ImageLibraryFailure", "ImageLibrary appears to be missing or occupied by other plugins load orders. GUIShop is unusable. \n Reload GUIShop and increase the config counter check limit to higher than {0}."},
                {"NoPermUse", "You do not have permission {0}"},
                {"Commands", "Commands"},
                {"Attire", "Attire"},
                {"Misc", "Misc"},
                {"Items", "Items"},
                {"Ammunition", "Ammunition"},
                {"Construction", "Construction"},
                {"Component", "Component"},
                {"Traps", "Traps"},
                {"Electrical", "Electrical"},
                {"Fun", "Fun"},
                {"Food", "Food"},
                {"Resources", "Resources"},
                {"Tool", "Tool"},
                {"Weapon", "Weapon"},
                {"Medical", "Medical"},
                {"Minicopter", "Minicopter"},
                {"Sedan", "Sedan"},
                {"Airdrop Call", "Airdrop Call"},
            }, this); //en

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "GUIShop n'a pas reçu de réponse du plugin Economics. Veuillez vous assurer que le plugin Economics est correctement installé."},
                {"EconomicsMaxDebt", "Transaction refusée, vous avez déjà une dette économique maximale"},
                {"EconomicsExceedsDebt", "Transaction refusée, cela dépasserait la limite d'endettement économique maximale de {0}"},
                {"CustomInvalidID", "L'ID de devise personnalisé n'est pas valide, la configuration nécessite une mise à jour pour cette boutique {0}"},
                {"MaxEconomicsBalance", "Transaction refusée, vous avez déjà atteint la limite économique maximale"},
                {"EconomicsExceedsLimit", "Transaction refusée, cela dépasserait la limite économique maximale de {0}"},
                {"ServerRewards", "GUIShop n'a pas reçu de réponse du plugin ServerRewards. Veuillez vous assurer que le plugin ServerRewards est correctement installé."},
                {"TakeCurrency", "GUIShop n'a pas réussi à prendre cette devise lors de l'achat {0}"},
                {"Bought", "Vous avez acheté {0} x {1} avec succès."},
                {"Sold", "Vous avez vendu {0} x {1} avec succès."},
                {"Cooldown", "Vous ne pouvez acheter cet article que toutes les {0} secondes."},
                {"InventoryFull", "Votre inventaire est plein."},
                {"InventorySlots", "Vous avez besoin d'au moins {0} emplacements d'inventaire gratuits."},
                {"ErrorShop", "Il y a un problème avec cette boutique. Veuillez contacter un administrateur."},
                {"GlobalShopsDisabled", "Les boutiques globales sont désactivées. Ce serveur utilise des vendeurs de PNJ!"},
                {"DeniedActionInShop", "Vous n'êtes pas autorisé à {0} dans cette boutique"},
                {"ShopItemItemInvalid", "AVERTISSEMENT: il semble que cet article que vous possédez n'est pas un article valide! Veuillez contacter un administrateur!"},
                {"ItemNotValidbuy", "AVERTISSEMENT: Il semble que ce ne soit pas un article valide à acheter, veuillez contacter un administrateur!"},
                {"ItemNotValidsell", "AVERTISSEMENT: Il semble que ce ne soit pas un article valide à vendre, veuillez contacter un administrateur!"},
                {"RedeemKitFail", "AVERTISSEMENT: Une erreur s'est produite lors de la remise de ce kit, veuillez contacter un administrateur!"},
                {"NotKit", "Ce n'est pas un nom de kit valide attribué à cet article de la boutique {0}"},
                {"BuyCmd", "Impossible d'acheter plusieurs exemplaires de cet article!"},
                {"BuyPriceFail", "AVERTISSEMENT: aucun prix d'achat n'a été donné par l'administrateur, vous ne pouvez pas acheter cet article"},
                {"SellPriceFail", "AVERTISSEMENT: aucun prix de vente n'a été donné par l'administrateur, vous ne pouvez pas vendre cet article"},
                {"NotEnoughMoney", "Vous avez besoin de {0} pièces pour acheter {1} sur {2}"},
                {"NotEnoughMoneyCustom", "Vous avez besoin de {0} devise pour acheter {1} x {2}"},
                {"CustomCurrencyFail", "L'administrateur WANRING a défini cette boutique appelée {0} comme devise personnalisée mais n'a pas défini d'ID de devise valide {1}"},
                {"NotEnoughSell", "Vous n'avez pas assez de cet article."},
                {"NotNothingShopFail", "Vous ne pouvez pas acheter Zero de cet article."},
                {"ItemNoExist", "AVERTISSEMENT: l'article que vous essayez d'acheter ne semble pas exister! Veuillez contacter un administrateur!"},
                {"ItemNoExistTake", "L'article que vous essayez de vendre n'est pas vendable pour le moment."},
                {"ItemIsNotBlueprintable", "Cet article de la boutique {0} ne peut pas être défini comme plan !"},
                {"BuildingBlocked", "Vous ne pouvez pas faire vos achats dans une zone de bâtiment bloquée."},
                {"BlockedMonuments", "Vous ne pouvez pas utiliser la boutique à proximité d'un monument!"},
                {"ItemNotEnabled", "Le commerçant a désactivé cet article."},
                {"ItemNotFound", "L'élément n'a pas été trouvé"},
                {"CantSellCommands", "Vous ne pouvez pas revendre les commandes à la boutique."},
                {"CantSellKits", "Vous ne pouvez pas revendre les kits à la boutique."},
                //{"CannotSellWhileEquiped", "Vous ne pouvez pas vendre l'objet si vous l'avez équipé."},
                {"GUIShopResponse", "GUIShop attend la fin des téléchargements d'ImageLibrary & LangAPI, veuillez patienter."},
                {"NPCResponseClose", "Merci pour vos achats chez {0} revenez bientôt!"},
                {"NPCResponseOpen", "Bienvenue dans le {0} que souhaitez-vous acheter? Appuyez sur E pour commencer vos achats!"},
                {"NoPerm", "Vous n'êtes pas autorisé à faire des achats chez {0}"},
                {"WipeReady", "Chère {0}, tous les magasins sont fermés pour \n {1} minutes"},
                {"ImageLibraryFailure", "ImageLibrary semble être manquant ou occupé par d'autres ordres de chargement de plugins. GUIShop est inutilisable. \n Rechargez GUIShop et augmentez la limite de vérification du compteur de configuration à plus de {0}"},
                {"NoPermUse", "Tu n'as pas la permission {0}"},
                {"Commands", "Commandes"},
                {"Attire", "Tenue"},
                {"Misc", "Divers"},
                {"Items", "Articles"},
                {"Ammunition", "Munition"},
                {"Construction", "Construction"},
                {"Component", "Composant"},
                {"Traps", "Pièges"},
                {"Electrical", "Électrique"},
                {"Fun", "Amusement"},
                {"Food", "Nourriture"},
                {"Resources", "Ressources"},
                {"Tool", "Outil"},
                {"Weapon", "Arme"},
                {"Medical", "Médical"},
                {"Minicopter", "Minicopter"},
                {"Sedan", "Sedan"},
                {"Airdrop Call", "Fumigène de dètresse"},
            }, this, "fr"); //French

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "GUIShop fick inte svar från pluginet Economics. Se till att plugin för ekonomi är korrekt installerad." },
                {"EconomicsMaxDebt", "Transaktionen avvisad du är redan på max ekonomiskuld"},
                {"EconomicsExceedsDebt", "Transaktionen avvisades detta skulle överskrida den högsta ekonomiska skuldgränsen på {0}"},
                {"CustomInvalidID", "Anpassat valuta-ID är inte giltigt konfiguration kräver uppdatering för denna butik {0}"},
                {"MaxEconomicsBalance", "Transaktionen avvisad, du har redan nått den högsta ekonomiska gränsen"},
                {"EconomicsExceedsLimit", "Transaktionen avvisades detta skulle överskrida den högsta möjliga ekonomigränsen på {0}"},
                {"ServerRewards", "GUIShop fick inte svar från ServerRewards-plugin. Se till att ServerRewards-tillägget är korrekt installerat." },
                {"TakeCurrency", "GUIshop har misslyckats med att ta denna valuta vid köp {0}"},
                {"Bought", "Du har köpt {0} x {1}."},
                {"Sold", "Du har sålt {0} x {1}."},
                {"Cooldown", "Du kan bara köpa den här varan var {0} sekund."},
                {"InventoryFull", "Ditt lager är fullt."},
                {"InventorySlots", "Du behöver minst {0} lediga lagerplatser."},
                {"ErrorShop", "Det är något fel med denna butik. Kontakta en administratör."},
                {"GlobalShopsDisabled", "Globala butiker är inaktiverade. Denna server använder NPC-leverantörer!"},
                {"DeniedActionInShop", "Du får inte {0} i den här butiken"},
                {"ShopItemItemInvalid", "VARNING: Det verkar som att detta säljföremål du har inte är ett giltigt objekt! Vänligen kontakta en administratör!" },
                {"ItemNotValidbuy", "VARNING: Det verkar som om det inte är ett giltigt objekt att köpa. Kontakta en administratör!" },
                {"ItemNotValidsell", "VARNING: Det verkar som om det inte är ett giltigt objekt att sälja. Kontakta en administratör!" },
                {"RedeemKitFail", "VARNING: Det uppstod ett fel när du gav dig detta kit. Kontakta en administratör!"},
                {"NotKit", "Detta är inte ett giltigt kitnamn som har tilldelats denna butiksvara {0}"},
                {"BuyCmd", "Kan inte köpa flera av denna artikel!"},
                {"BuyPriceFail", "VARNING: Inget köppris gavs av administratören, du kan inte köpa denna artikel"},
                {"SellPriceFail", "VARNING: Inget försäljningspris gavs av administratören, du kan inte sälja denna artikel"},
                {"NotEnoughMoney", "Du behöver {0} mynt för att köpa {1} av {2}"},
                {"NotEnoughMoneyCustom", "Du behöver {0} valuta för att köpa {1} x {2}"},
                {"CustomCurrencyFail", "WANRING Admin har angett denna butik som heter {0} som anpassad valuta men har inte angett ett giltigt valuta-ID {1}"},
                {"NotEnoughSell", "Du har inte tillräckligt med det här objektet."},
                {"NotNothingShopFail", "Du kan inte köpa noll av denna artikel."},
                {"ItemNoExist", "VARNING: Varan du försöker köpa verkar inte existera! Vänligen kontakta en administratör!"},
                {"ItemNoExistTake", "Föremålet du försöker sälja kan inte säljas just nu."},
                {"ItemIsNotBlueprintable", "Denna butiksartikel {0} kan inte ställas in som en Blueprint! Kontakta Admin"},
                {"BuildingBlocked", "Du kan inte handla när du är i ett byggnadsspärrat område."},
                {"BlockedMonuments", "Du får inte använda butiken i närheten av ett monument!"},
                {"ItemNotEnabled", "Butiksinnehavaren har inaktiverat detta föremål."},
                {"ItemNotFound", "Objektet hittades inte"},
                {"CantSellCommands", "Du kan inte sälja kommandon tillbaka till butiken."},
                {"CantSellKits", "Du kan inte sälja kit tillbaka till butiken."},
                //{"CannotSellWhileEquiped", "Du kan inte sälja föremålet om du har det."},
                {"GUIShopResponse", "GUIShop väntar på att ImageLibrary & LangAPI-nedladdningar ska slutföras. Vänta."},
                {"NPCResponseClose", "Tack för att du handlar på {0} kom igen snart!"},
                {"NPCResponseOpen", "Välkommen till {0} vad vill du köpa? Tryck på E för att börja handla!"},
                {"NoPerm", "Du har inte behörighet att handla på {0}"},
                {"WipeReady", "{0}, alla butiker är stängda för \n {1} minuter"},
                {"ImageLibraryFailure", "ImageLibrary verkar saknas eller upptas av andra insticksladdningsorder. GUIshop är oanvändbart. \n Ladda om GUIshop och öka kontrollgränsen för konfigurationsräknaren till högre än {0}"},
                {"NoPermUse", "Du har inte tillåtelse {0}"},
                {"Commands", "Kommandon"},
                {"Attire", "Klädsel"},
                {"Misc", "Övrigt"},
                {"Items", "Objekt"},
                {"Ammunition", "Ammunition"},
                {"Construction", "Konstruktion"},
                {"Component", "Komponent"},
                {"Traps", "Fällor"},
                {"Electrical", "Elektrisk"},
                {"Fun", "Roligt"},
                {"Food", "Mat"},
                {"Resources", "Resurser"},
                {"Tool", "Verktyg"},
                {"Weapon", "Vapen"},
                {"Medical", "Medicinsk"},
                {"Minicopter", "Minikopter"},
                {"Sedan", "Sedan"},
                {"Airdrop Call", "Airdrop Call"},
            }, this, "sv-SE"); //Swedish

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "GUIShop heeft geen reactie ontvangen van de Economics-plug-in. Zorg ervoor dat de Economics-plug-in correct is geïnstalleerd."},
                {"EconomicsMaxDebt", "Transactie geweigerd, u heeft al een maximale economische schuld"},
                {"EconomicsExceedsDebt", "Transactie geweigerd, dit zou de maximale economische schuldlimiet van {0} overschrijden"},
                {"CustomInvalidID", "Aangepaste valuta-ID is niet geldig configuratie vereist update voor deze winkel {0}"},
                {"MaxEconomicsBalance", "Transactie geweigerd, u zit al op de maximale economische limiet"},
                {"EconomicsExceedsLimit", "Transactie geweigerd, dit zou de max. verdienbare economische limiet van {0} overschrijden"},
                {"ServerRewards", "GUIShop heeft geen antwoord ontvangen van de ServerRewards-plug-in. Zorg ervoor dat de ServerRewards-plug-in correct is geïnstalleerd."},
                {"TakeCurrency", "GUIShop heeft deze valuta niet ontvangen bij aankoop {0}"},
                {"Bought", "U heeft met succes {0} x {1} gekocht."},
                {"Sold", "U heeft met succes {0} x {1} verkocht."},
                {"Cooldown", "U kunt dit item slechts elke {0} seconden kopen."},
                {"InventoryFull", "Uw inventaris is vol."},
                {"InventorySlots", "U heeft minimaal {0} gratis voorraadvakken nodig."},
                {"ErrorShop", "Er is iets mis met deze winkel. Neem contact op met een admin."},
                {"GlobalShopsDisabled", "Global Shops zijn uitgeschakeld. Deze server gebruikt NPC-leveranciers!"},
                {"DeniedActionInShop", "Je mag niet {0} in deze winkel komen"},
                {"ShopItemItemInvalid", "WAARSCHUWING: het lijkt erop dat dit verkoopartikel dat u heeft geen geldig item is! Neem contact op met een beheerder!"},
                {"ItemNotValidbuy", "WAARSCHUWING: Het lijkt erop dat het geen geldig item is om te kopen. Neem contact op met een beheerder!"},
                {"ItemNotValidsell", "WAARSCHUWING: Het lijkt erop dat het geen geldig item is om te verkopen. Neem contact op met een beheerder!"},
                {"RedeemKitFail", "WAARSCHUWING: Er is een fout opgetreden bij het overhandigen van deze kit. Neem contact op met een beheerder!"},
                {"NotKit", "Dit is geen geldige kitnaam die is toegewezen aan dit winkelitem {0}"},
                {"BuyCmd", "Kan niet meerdere van dit item kopen!"},
                {"BuyPriceFail", "WAARSCHUWING: Er is geen koopprijs gegeven door de admin, u kunt dit item niet kopen"},
                {"SellPriceFail", "WAARSCHUWING: Er is geen verkoopprijs opgegeven door de beheerder, u kunt dit item niet verkopen"},
                {"NotEnoughMoney", "U heeft {0} munten nodig om {1} van {2} te kopen"},
                {"NotEnoughMoneyCustom", "U heeft {0} valuta nodig om {1} x {2} te kopen"},
                {"CustomCurrencyFail", "WANRING Beheerder heeft deze winkel met de naam {0} ingesteld als aangepaste valuta, maar heeft geen geldige valuta-ID ingesteld {1}"},
                {"NotEnoughSell", "Je hebt niet genoeg van dit item."},
                {"NotNothingShopFail", "U kunt geen nul van dit artikel kopen."},
                {"ItemNoExist", "WAARSCHUWING: Het artikel dat u probeert te kopen, lijkt niet te bestaan! Neem contact op met een beheerder!"},
                {"ItemNoExistTake", "Het item dat u probeert te verkopen, is op dit moment niet verkoopbaar."},
                {"ItemIsNotBlueprintable", "Dit winkelitem {0} kan niet worden ingesteld als blauwdruk! Neem contact op met beheerder"},
                {"BuildingBlocked", "U kunt niet winkelen in een gebied dat geblokkeerd is door gebouwen."},
                {"BlockedMonuments", "Je mag de winkel niet gebruiken in de buurt van een monument!"},
                {"ItemNotEnabled", "De winkelier heeft dit item uitgeschakeld."},
                {"ItemNotFound", "Item is niet gevonden"},
                {"CantSellCommands", "Je kunt geen commando's terug verkopen aan de winkel."},
                {"CantSellKits", "U kunt kits niet terug verkopen aan de winkel."},
                //{"CannotSellWhileEquiped", "Je kunt het item niet verkopen als je het hebt Equipt."},
                {"GUIShopResponse", "GUIShop wacht tot het downloaden van ImageLibrary & LangAPI is voltooid, even geduld."},
                {"NPCResponseClose", "Bedankt voor het winkelen bij {0}, kom snel weer!"},
                {"NPCResponseOpen", "Welkom bij de {0} wat wilt u kopen? Druk op E om te beginnen met winkelen!"},
                {"NoPerm", "U heeft geen toestemming om te winkelen bij {0}"},
                {"WipeReady", "{0}, alle winkels zijn gesloten voor \n {1} minuten"},
                {"ImageLibraryFailure", "ImageLibrary lijkt te ontbreken of wordt bezet door andere laadopdrachten voor plug-ins. GUIShop is onbruikbaar. \n Herlaad GUIShop en verhoog de controlelimiet van de configuratieteller naar hoger dan {0}"},
                {"NoPermUse", "Je hebt geen toestemming {0}"},
                {"Commands", "Commando's"},
                {"Attire", "Kleding"},
                {"Misc", "Diversen"},
                {"Items", "Artikelen"},
                {"Ammunition", "Munitie"},
                {"Construction", "Bouw"},
                {"Component", "Component"},
                {"Traps", "Vallen"},
                {"Electrical", "Elektrisch"},
                {"Fun", "Pret"},
                {"Food", "Voedsel"},
                {"Resources", "Middelen"},
                {"Tool", "Tool"},
                {"Weapon", "Wapen"},
                {"Medical", "Medisch"},
                {"Minicopter", "Minikopter"},
                {"Sedan", "Sinds"},
                {"Airdrop Call", "Airdrop-oproep"},
            }, this, "nl"); //Dutch

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "GUIShop이 Economics 플러그인에서 응답을받지 못했습니다. Economics 플러그인이 올바르게 설치되었는지 확인하십시오."},
                {"EconomicsMaxDebt", "거래가 거부되었습니다. 이미 최대 경제 부채에 도달했습니다."},
                {"EconomicsExceedsDebt", "거래가 거부되면 최대 경제 부채 한도인 {0}을(를) 초과합니다."},
                {"CustomInvalidID", "사용자 정의 통화 ID가 올바르지 않습니다. 이 상점 {0}에 대한 업데이트가 필요합니다."},
                {"MaxEconomicsBalance", "거래가 거부되었습니다. 이미 최대 경제 한도에 도달했습니다."},
                {"EconomicsExceedsLimit", "거래가 거부되면 최대 수익 가능 한도인 {0}을(를) 초과합니다."},
                {"ServerRewards", "GUIShop이 ServerRewards 플러그인에서 응답을받지 못했습니다. ServerRewards 플러그인이 올바르게 설치되었는지 확인하십시오."},
                {"TakeCurrency", "GUIShop은 구매 시 이 통화 {0}을(를) 사용하지 못했습니다."},
                {"Bought", "{0} x {1}을 (를) 성공적으로 구입했습니다."},
                {"Sold", "{0} x {1}을 (를) 성공적으로 판매했습니다."},
                {"Cooldown", "이 아이템은 {0} 초마다 구매할 수 있습니다."},
                {"InventoryFull", "재고가 가득 찼습니다."},
                {"InventorySlots", "최소 {0} 개의 인벤토리 자리가 필요합니다."},
                {"ErrorShop", "이 가게에 문제가 있습니다. 관리자에게 문의하십시오."},
                {"GlobalShopsDisabled", "글로벌 상점이 비활성화되었습니다. 이 서버는 NPC 상인을 사용합니다!"},
                {"DeniedActionInShop", "이 상점에서 {0} 할 수 없습니다."},
                {"ShopItemItemInvalid", "경고: 보유한 판매 항목이 유효한 항목이 아닌 것 같습니다! 관리자에게 문의하십시오!"},
                {"ItemNotValidbuy", "경고 : 구매할 수있는 유효한 항목이 아닌 것 같습니다. 관리자에게 문의하십시오!"},
                {"ItemNotValidsell", "경고 : 판매 할 수있는 유효한 항목이 아닌 것 같습니다. 관리자에게 문의하십시오!"},
                {"RedeemKitFail", "경고: 이 키트를 제공하는 동안 오류가 발생했습니다. 관리자에게 문의하십시오!"},
                {"NotKit", "이 상점 항목 {0}에 할당된 유효한 키트 이름이 아닙니다."},
                {"BuyCmd", "이 항목을 여러 개 구매할 수 없습니다!"},
                {"BuyPriceFail", "경고: 관리자가 제공 한 구매 가격이 없으므로이 항목을 구매할 수 없습니다."},
                {"SellPriceFail", "경고: 관리자가 제공 한 판매 가격이 없으므로이 항목을 판매 할 수 없습니다."},
                {"NotEnoughMoney", "{2} 개 중 {1} 개를 구매하려면 {0} 코인이 필요합니다."},
                {"NotEnoughMoneyCustom", "{1} x {2}을 (를) 구매하려면 {0} 통화가 필요합니다."},
                {"CustomCurrencyFail", "WANRING 관리자는 {0}이라는 상점을 맞춤 통화로 설정했지만 유효한 통화 ID {1}를 설정하지 않았습니다."},
                {"NotEnoughSell", "이 항목이 충분하지 않습니다."},
                {"NotNothingShopFail", "이 항목의 0을 구매할 수 없습니다."},
                {"ItemNoExist", "WARNING : 구매하려는 항목이 존재하지 않는 것 같습니다! 관리자에게 문의하십시오!"},
                {"ItemNoExistTake", "판매하려는 아이템은 현재 판매 할 수 없습니다."},
                {"ItemIsNotBlueprintable", "이 상점 항목 {0}은(는) 청사진으로 설정할 수 없습니다! 관리자에게 문의"},
                {"BuildingBlocked", "건물이 차단 된 구역에서는 쇼핑을 할 수 없습니다."},
                {"BlockedMonuments", "기념비 근처에서는 상점을 사용할 수 없습니다!"},
                {"ItemNotEnabled", "상점 주인이 항목을 비활성화했습니다."},
                {"ItemNotFound", "항목을 찾을 수 없습니다."},
                {"CantSellCommands", "명령어는 상점에 다시 판매 할 수 없습니다."},
                {"CantSellKits", "킷은 상점에 반납 할 수 없습니다."},
                {"GUIShopResponse", "GUIShop은 ImageLibrary & LangAPI 다운로드가 완료되기를 기다리고 있습니다. 잠시 기다려주세요."},
                {"NPCResponseClose", "{0}에서 쇼핑 해 주셔서 감사합니다. 곧 다시 오세요!"},
                {"NPCResponseOpen", "무엇을 구매 하시겠습니까? {0}에 오신 것을 환영합니다. 쇼핑을 시작하려면 E를 누르세요!"},
                {"NoPerm", "에서 쇼핑할 권한이 없습니다. {0}"},
                {"WipeReady", "{0} 모든 매장이 {1}분 동안 문을 닫습니다."},
                {"ImageLibraryFailure", "ImageLibrary가 누락되었거나 다른 플러그인 로드 순서에 의해 점유된 것 같습니다. GUIShop을 사용할 수 없습니다. \n 을 다시 GUIShop 로드하고 구성 카운터 확인 제한을 보다 높게 늘립니다 {0}"},
                {"NoPermUse", "당신은 권한이 없습니다 {0}"},
                {"Commands", "명령"},
                {"Attire", "복장"},
                {"Misc", "기타"},
                {"Items", "아이템"},
                {"Ammunition", "탄약"},
                {"Construction", "구성"},
                {"Component", "구성 요소"},
                {"Traps", "트랩"},
                {"Electrical", "전기 같은"},
                {"Fun", "장난"},
                {"Food", "음식"},
                {"Resources", "자원"},
                {"Tool", "수단"},
                {"Weapon", "무기"},
                {"Medical", "의료"},
                {"Minicopter", "미니 콥터"},
                {"Sedan", "이후"},
                {"Airdrop Call", "에어 드랍 콜"},
            }, this, "ko"); //Korean

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "GUIShop no va rebre cap resposta del connector Economics. Assegureu-vos que el connector d'Economia està instal·lat correctament." },
                {"EconomicsMaxDebt", "S'ha rebutjat la transacció, ja teniu el deute econòmic màxim"},
                {"EconomicsExceedsDebt", "La transacció ha rebutjat, això superaria el límit màxim de deute econòmic de {0}"},
                {"CustomInvalidID", "L'identificador de moneda personalitzat no és vàlid. La configuració requereix actualització per a aquesta botiga {0}"},
                {"MaxEconomicsBalance", "La transacció s'ha rebutjat, ja esteu al límit econòmic màxim"},
                {"EconomicsExceedsLimit", "La transacció ha rebutjat, això supera el límit econòmic màxim que es pot guanyar de {0}"},
                {"ServerRewards", "GUIShop no ha rebut cap resposta del connector ServerRewards. Assegureu-vos que el connector ServerRewards està instal·lat correctament." },
                {"TakeCurrency", "GUIShop no ha pogut acceptar aquesta moneda {0} en comprar-la"},
                {"Bought", "Heu comprat {0} x {1} correctament."},
                {"Sold", "Heu venut correctament {0} x {1}."},
                {"Cooldown", "Només podeu comprar aquest article cada {0} segons."},
                {"InventoryFull", "El vostre inventari està ple."},
                {"InventorySlots", "Necessiteu almenys {0} ranures d'inventari gratuïtes."},
                {"ErrorShop", "Hi ha algun problema amb aquesta botiga. Poseu-vos en contacte amb un administrador."},
                {"GlobalShopsDisabled", "Les botigues globals estan desactivades. Aquest servidor utilitza proveïdors de NPC."},
                {"DeniedActionInShop", "No podeu {0} en aquesta botiga"},
                {"ShopItemItemInvalid", "ADVERTÈNCIA: Sembla que aquest article de venda que teniu no és un article vàlid. Poseu-vos en contacte amb un administrador." },
                {"ItemNotValidbuy", "ADVERTÈNCIA: Sembla que no és un article vàlid per comprar, poseu-vos en contacte amb un administrador." },
                {"ItemNotValidsell", "ADVERTÈNCIA: Sembla que no és un article vàlid per vendre, poseu-vos en contacte amb un administrador." },
                {"RedeemKitFail", "ADVERTÈNCIA: S'ha produït un error en donar-vos aquest kit. Poseu-vos en contacte amb un administrador." },
                {"NotKit", "Aquest no és un nom de kit vàlid assignat a aquest article de botiga {0}"},
                {"BuyCmd", "No es poden comprar diversos elements."},
                {"BuyPriceFail", "AVÍS: l'administrador no va donar cap preu de compra, no podeu comprar aquest article"},
                {"SellPriceFail", "ADVERTÈNCIA: l'administrador no ha donat cap preu de venda, no es pot vendre aquest article"},
                {"NotEnoughMoney", "Necessiteu {0} monedes per comprar {1} de {2}"},
                {"NotEnoughMoneyCustom", "Necessiteu {0} moneda per comprar {1} x {2}"},
                {"CustomCurrencyFail", "WANRING L'administrador té aquesta botiga anomenada {0} establerta com a moneda personalitzada, però no ha establert un identificador de moneda vàlid {1}"},
                {"NotEnoughSell", "No en teniu prou amb aquest element."},
                {"NotNothingShopFail", "No podeu comprar zero d'aquest article."},
                {"ItemNoExist", "ADVERTÈNCIA: sembla que no existeix l'article que intenteu comprar. Poseu-vos en contacte amb un administrador." },
                {"ItemNoExistTake", "L'element que intenteu vendre no es pot vendre en aquest moment."},
                {"ItemIsNotBlueprintable", "Aquest article de la botiga {0} no es pot definir com a pla. Contacta amb l'administrador"},
                {"BuildingBlocked", "No es pot comprar mentre es troba en una zona bloquejada."},
                {"BlockedMonuments", "No podeu fer servir la botiga a prop d'un monument."},
                {"ItemNotEnabled", "El botiguer ha desactivat aquest article."},
                {"ItemNotFound", "No s'ha trobat l'element"},
                {"CantSellCommands", "No podeu vendre comandes a la botiga."},
                {"CantSellKits", "No es poden vendre els kits a la botiga."},
                //{"CannotSellWhileEquiped", "No podeu vendre l'article si el teniu equipat."},
                {"GUIShopResponse", "GUIShop espera que finalitzin les baixades de ImageLibrary & LangAPI, espereu."},
                {"NPCResponseClose", "Gràcies per comprar a {0} torna aviat."},
                {"NPCResponseOpen", "Us donem la benvinguda a {0} què voleu comprar? Premeu E per començar a comprar."},
                {"NoPerm", "No tens permís per comprar a {0}"},
                {"WipeReady", "Benvolgut {0}, totes les botigues estan tancades durant \n {1} minuts"},
                {"ImageLibraryFailure", "Sembla que la ImageLibrary falta o està ocupada per altres ordres de càrrega de connectors. GUIShop no es pot utilitzar. \n Torneu a carregar GUIShop i augmenteu el límit de comprovació del comptador de configuració a més de {0}"},
                {"NoPermUse", "No tens permís {0}"},
                {"Commands", "Ordres"},
                {"Attire", "Vestimenta"},
                {"Misc", "Misc"},
                {"Items", "Articles"},
                {"Ammunition", "Munició"},
                {"Construction", "Construcció"},
                {"Component", "Component"},
                {"Traps", "Paranys"},
                {"Electrical", "Elèctric"},
                {"Fun", "Diversió"},
                {"Food", "Menjar"},
                {"Resources", "Recursos"},
                {"Tool", "Eina"},
                {"Weapon", "Arma"},
                {"Medical", "Mèdic"},
                {"Minicopter", "Minicòpter"},
                {"Sedan", "Des de"},
                {"Airdrop Call", "Trucada Airdrop"},
            }, this, "ca"); // Catalan

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "GUIShop没有收到来自Economics插件的响应。请确保经济学插件已正确安装。"},
                {"EconomicsMaxDebt", "交易被拒绝 您已经处于最大经济债务状态"},
                {"EconomicsExceedsDebt", "交易被拒绝，这将超过经济债务上限 {0}"},
                {"CustomInvalidID", "自定义货币 ID 无效配置需要为此商店更新 {0}"},
                {"MaxEconomicsBalance", "交易被拒绝 您已经达到最大经济限制"},
                {"EconomicsExceedsLimit", "交易被拒绝，这将超过 {0} 的最大可赚取经济限制"},
                {"ServerRewards", "GUIShop没有收到ServerRewards插件的响应。请确保ServerRewards插件已正确安装。"},
                {"TakeCurrency", "GUIShop 在购买时未能使用此货币 {0}"},
                {"Bought", "您已成功购买{0} x {1}。"},
                {"Sold", "您已成功售出{0} x {1}。"},
                {"Cooldown", "您只能每{0}秒购买此商品。"},
                {"InventoryFull", "您的库存已满。"},
                {"InventorySlots", "您至少需要{0}个可用的广告位。"},
                {"ErrorShop", "这家商店有问题。请联系管理员。"},
                {"GlobalShopsDisabled", "禁用全球商店。该服务器使用NPC供应商！"},
                {"DeniedActionInShop", "此商店不允许您{0}"},
                {"ShopItemItemInvalid", "警告：看来您拥有的这个销售商品不是有效商品！请联系管理员！"},
                {"ItemNotValidbuy", "警告：看来这不是有效的商品，请与管理员联系！"},
                {"ItemNotValidsell", "警告：看来这不是有效的商品，请与管理员联系！"},
                {"RedeemKitFail", "警告：为您提供此工具包时出错，请与管理员联系！"},
                {"NotKit", "这不是分配给此商店商品的有效套件名称 {0}"},
                {"BuyCmd", "无法购买此商品的多个！"},
                {"BuyPriceFail", "警告：管理员未给出购买价格，您不能购买此商品"},
                {"SellPriceFail", "警告：管理员未给出出售价格，您不能出售该物品"},
                {"NotEnoughMoney", "您需要{0}硬币才能购买{2}中的{1}"},
                {"NotEnoughMoneyCustom", "您需要{0}货币才能购买{1} x {2}"},
                {"CustomCurrencyFail", "万灵 管理员已将此名为 {0} 的商店设置为自定义货币，但尚未设置有效的货币 ID {1}"},
                {"NotEnoughSell", "您没有足够的此项。"},
                {"NotNothingShopFail", "您不能购买此商品的零。"},
                {"ItemNoExist", "警告：您尝试购买的商品似乎不存在！请联系管理员！"},
                {"ItemNoExistTake", "您要出售的商品目前无法出售。"},
                {"ItemIsNotBlueprintable", "此商店商品 {0} 不能设置为蓝图！联系管理员"},
                {"BuildingBlocked", "您不能在建筑物受阻区域购物。"},
                {"BlockedMonuments", "您在纪念碑附近不能使用商店！"},
                {"ItemNotEnabled", "店主已禁用此项目。"},
                {"ItemNotFound", "找不到项目"},
                {"CantSellCommands", "您不能将Commands卖回商店。"},
                {"CantSellKits", "您不能将套件卖回商店。"},
                //{"CannotSellWhileEquiped", "如果拥有此设备，则无法出售。"},
                {"GUIShopResponse", "GUIShop正在等待ImageLibrary & LangAPI下载完成，请等待。"},
                {"NPCResponseClose", "感谢您在{0}购物，很快再来！"},
                {"NPCResponseOpen", "欢迎来到{0}，您想购买什么？按E键开始购物！"},
                {"NoPerm", "您无权在 购物 {0}"},
                {"WipeReady", "亲爱的{0}，所有商店都关门 \n {1} 分钟"},
                {"ImageLibraryFailure", "ImageLibrary 似乎丢失或被其他插件加载顺序占用。GUIShop 无法使用。 \n 重新加载 GUIShop 并将配置计数器检查限制增加到高于 {0}"},
                {"NoPermUse", "你没有许可 {0}"},
                {"Commands", "指令"},
                {"Attire", "服装"},
                {"Misc", "杂项"},
                {"Items", "物品"},
                {"Ammunition", "弹药"},
                {"Construction", "施工"},
                {"Component", "零件"},
                {"Traps", "陷阱"},
                {"Electrical", "电的"},
                {"Fun", "好玩"},
                {"Food", "餐饮"},
                {"Resources", "资源资源"},
                {"Tool", "工具"},
                {"Weapon", "武器"},
                {"Medical", "医疗类"},
                {"Minicopter", "微型直升机"},
                {"Sedan", "以来"},
                {"Airdrop Call", "空投电话"},
            }, this, "zh-CN"); //Simplified Chinese

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "O GUIShop não recebeu resposta do plugin Economics. Certifique-se de que o plugin Economics está instalado corretamente. " },
                {"EconomicsMaxDebt", "Transação recusada, você já está com dívida econômica máxima"},
                {"EconomicsExceedsDebt", "A transação foi recusada. Isso excederia o limite máximo de dívida econômica de {0}"},
                {"CustomInvalidID", "O ID de moeda personalizado não é válido A configuração requer atualização para esta loja {0}"},
                {"MaxEconomicsBalance", "Transação recusada, você já está no limite máximo de economia"},
                {"EconomicsExceedsLimit", "A transação foi recusada. Isso excederia o limite máximo de economia de ganhos de {0}"},
                {"ServerRewards", "GUIShop não recebeu uma resposta do plugin ServerRewards. Certifique-se de que o plugin ServerRewards esteja instalado corretamente. " },
                {"TakeCurrency", "GUIShop não conseguiu usar esta moeda {0} na compra"},
                {"Bought", "Você comprou com sucesso {0} {1}."},
                {"Sold", "Você vendeu com sucesso {0} {1}. "},
                {"Cooldown", "Você só pode comprar este item a cada {0} segundos."},
                {"InventoryFull", "Seu inventário está cheio."},
                {"InventorySlots", "Você precisa de pelo menos {0} slots de inventário gratuitos."},
                {"ErrorShop", "Há algo errado com esta loja. Entre em contato com um administrador."},
                {"GlobalShopsDisabled", "Lojas globais estão desativadas. Este servidor usa fornecedores NPC!"},
                {"DeniedActionInShop", "Você não tem permissão para {0} nesta loja"},
                {"ShopItemItemInvalid", "AVISO: Parece que este item de venda que você tem não é um item válido! Entre em contato com um administrador!" },
                {"ItemNotValidbuy", "AVISO: parece que não é um item válido para comprar, entre em contato com um administrador!"},
                {"ItemNotValidsell", "AVISO: parece que não é um item válido para vender, entre em contato com um administrador!"},
                {"RedeemKitFail", "AVISO: Ocorreu um erro ao fornecer este kit a você, entre em contato com um administrador!"},
                {"NotKit", "Este não é um nome de kit válido atribuído a este item da loja {0}"},
                {"BuyCmd", "Não é possível comprar vários deste item!"},
                {"BuyPriceFail", "AVISO: Nenhum preço de compra foi fornecido pelo administrador, você não pode comprar este item"},
                {"SellPriceFail", "AVISO: Nenhum preço de venda foi fornecido pelo administrador, você não pode vender este item"},
                {"NotEnoughMoney", "Você precisa de {0} moedas para comprar {1}x {2}"},
                {"NotEnoughMoneyCustom", "Você precisa de {0} moeda para comprar {1}x {2}"},
                {"CustomCurrencyFail", "O administrador do WANRING tem esta loja chamada {0} definida como moeda personalizada, mas não definiu um ID de moeda válido {1}"},
                {"NotEnoughSell", "Você não tem o suficiente deste item."},
                {"NotNothingShopFail", "Você não pode comprar Zero deste item."},
                {"ItemNoExist", "AVISO: o item que você está tentando comprar parece não existir! Entre em contato com um administrador!" },
                {"ItemNoExistTake", "O item que você está tentando vender não pode ser vendido no momento."},
                {"ItemIsNotBlueprintable", "Este item da loja {0} não pode ser definido como um projeto! Administrador de contato"},
                {"BuildingBlocked", "Você não pode fazer compras enquanto estiver em uma área de construção bloqueada. "},
                {"BlockedMonuments", "Você não pode usar a loja perto de um Monumento!"},
                {"ItemNotEnabled", "O lojista desativou este item."},
                {"ItemNotFound", "Item não encontrado"},
                {"CantSellCommands", "Você não pode vender comandos de volta para a loja."},
                {"CantSellKits", "Você não pode vender Kits de volta para a loja."},
                //{"CannotSellWhileEquiped", "Você não pode vender o item se o tiver equipado."},
                {"GUIShopResponse", "GUIShop está esperando o download da ImageLibrary & LangAPI terminar, por favor aguarde."},
                {"NPCResponseClose", "Obrigado por comprar em {0} volte em breve!"},
                {"NPCResponseOpen", "Bem-vindo ao {0} o que você gostaria de comprar? Pressione E para começar a comprar!"},
                {"NoPerm", "Você não tem permissão para comprar em {0}"},
                {"WipeReady", "Caro {0}, todas as lojas estão fechadas por \n {1} minutos"},
                {"ImageLibraryFailure", "ImageLibrary parece estar ausente ou ocupado por outros pedidos de carregamento de plug-ins. GUIShop está inutilizável. \n Recarregue o GUIShop e aumente o limite de verificação do contador de configuração para mais de {0}"},
                {"NoPermUse", "Você não tem permissão {0}"},
                {"Commands", "Comandos"},
                {"Attire", "Vestuário"},
                {"Misc", "Misc"},
                {"Items", "Items"},
                {"Ammunition", "Munição"},
                {"Construction", "Construção"},
                {"Component", "Componente"},
                {"Traps", "Traps"},
                {"Electrical", "Elétrico"},
                {"Fun", "Diversão"},
                {"Food", "Comida"},
                {"Resources", "Resources"},
                {"Tool", "Ferramenta"},
                {"Weapon", "Arma"},
                {"Medical", "Médico"},
                {"Minicopter", "Minicóptero"},
                {"Sedan", "Desde a"},
                {"Airdrop Call", "Airdrop Call"},
            }, this, "pt-BR"); //Portuguese Brazil

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "GUIShop hat keine Antwort vom Economics-Plugin erhalten. Bitte stellen Sie sicher, dass das Economics-Plugin korrekt installiert ist."},
                {"EconomicsMaxDebt", "Transaktion abgelehnt, Sie haben bereits die maximale wirtschaftliche Verschuldung erreicht"},
                {"EconomicsExceedsDebt", "Transaktion abgelehnt, dies würde das maximale Wirtschaftsschuldenlimit von {0} überschreiten"},
                {"CustomInvalidID", "Benutzerdefinierte Währungs-ID ist ungültig Konfiguration muss für diesen Shop aktualisiert werden {0}"},
                {"MaxEconomicsBalance", "Transaktion abgelehnt, Sie haben bereits das maximale Wirtschaftslimit erreicht"},
                {"EconomicsExceedsLimit", "Transaktion abgelehnt, dies würde das maximal erzielbare Wirtschaftslimit von {0} überschreiten"},
                {"ServerRewards", "GUIShop hat keine Antwort vom ServerRewards-Plugin erhalten. Bitte stellen Sie sicher, dass das ServerRewards-Plugin korrekt installiert ist."},
                {"TakeCurrency", "GUIShop hat es versäumt zu nehmen Währung beim Kauf {0}"},
                {"Bought", "Sie haben {0} x {1} erfolgreich gekauft."},
                {"Sold", "Sie haben {0} x {1} erfolgreich verkauft."},
                {"Cooldown", "Sie können diesen Artikel nur alle {0} Sekunden kaufen."},
                {"InventoryFull", "Ihr Inventar ist voll."},
                {"InventorySlots", "Sie benötigen mindestens {0} freie Inventarplätze."},
                {"ErrorShop", "Mit diesem Shop stimmt etwas nicht. Bitte wenden Sie sich an einen Administrator."},
                {"GlobalShopsDisabled", "Globale Shops sind deaktiviert. Dieser Server verwendet NPC-Anbieter!"},
                {"DeniedActionInShop", "Sie dürfen in diesem Shop nicht {0}"},
                {"ShopItemItemInvalid", "WARNUNG: Es scheint, dass dieser Verkaufsartikel, den Sie haben, kein gültiger Artikel ist! Bitte wenden Sie sich an einen Administrator!"},
                {"ItemNotValidbuy", "WARNUNG: Es scheint, als wäre es kein gültiger Artikel zum Kaufen. Bitte wenden Sie sich an einen Administrator!"},
                {"ItemNotValidsell", "WARNUNG: Es scheint, dass es sich nicht um einen gültigen Artikel handelt. Bitte wenden Sie sich an einen Administrator."},
                {"RedeemKitFail", "WARNUNG: Bei der Bereitstellung dieses Kits ist ein Fehler aufgetreten. Bitte wenden Sie sich an einen Administrator."},
                {"NotKit", "Dies ist kein gültiger Kit-Name, der diesem Shop-Artikel {0} zugewiesen ist."},
                {"BuyCmd", "Mehrere Artikel können nicht gekauft werden!"},
                {"BuyPriceFail", "WARNUNG: Der Administrator hat keinen Kaufpreis angegeben. Sie können diesen Artikel nicht kaufen"},
                {"SellPriceFail", "WARNUNG: Der Administrator hat keinen Verkaufspreis angegeben. Sie können diesen Artikel nicht verkaufen"},
                {"NotEnoughMoney", "Sie benötigen {0} Münzen, um {1} von {2} zu kaufen."},
                {"NotEnoughMoneyCustom", "Sie benötigen {0} Währung, um {1} x {2} zu kaufen."},
                {"CustomCurrencyFail", "WARNUNG Administrator; dieser Shop {0} ist auf eine benutzerdefinierte Währung eingestellt, hat aber keine gültige Währungs-ID {1}"},
                {"NotEnoughSell", "Sie haben nicht genug von diesem Artikel."},
                {"NotNothingShopFail", "Sie können Zero von diesem Artikel nicht kaufen."},
                {"ItemNoExist", "WARNUNG: Der Artikel, den Sie kaufen möchten, scheint nicht zu existieren! Bitte wenden Sie sich an einen Administrator!"},
                {"ItemNoExistTake", "Der Artikel, den Sie verkaufen möchten, ist derzeit nicht verfügbar."},
                {"ItemIsNotBlueprintable", "Dieser Shop-Artikel {0} kann nicht als Blaupause festgelegt werden!"},
                {"BuildingBlocked", "Sie können nicht in einem blockierten Bereich einkaufen."},
                {"BlockedMonuments", "Sie dürfen den Shop nicht in der Nähe eines Denkmals benutzen!"},
                {"ItemNotEnabled", "Der Ladenbesitzer hat diesen Artikel deaktiviert."},
                {"ItemNotFound", "Element wurde nicht gefunden"},
                {"CantSellCommands", "Sie können keine Befehle an den Shop zurückverkaufen."},
                {"CantSellKits", "Sie können Kits nicht an den Shop zurückverkaufen."},
                //{"CannotSellWhileEquiped", "Sie können den Artikel nicht verkaufen, wenn Sie über Equipt verfügen."},
                {"GUIShopResponse", "GUIShop wartet auf den Abschluss der ImageLibrary & LangAPI-Downloads. Bitte warten Sie."},
                {"NPCResponseClose", "Vielen Dank für Ihren Einkauf bei {0}. Kommen Sie bald wieder!"},
                {"NPCResponseOpen", "Willkommen bei der {0}, was möchten Sie kaufen? Drücken Sie E, um mit dem Einkaufen zu beginnen!"},
                {"NoPerm", "Sie sind nicht berechtigt, bei  einzukaufen {0}"},
                {"WipeReady", "Liebling {0}, alle Geschäfte sind geschlossen für \n {1} Minuten"},
                {"ImageLibraryFailure", "ImageLibrary scheint zu fehlen oder von anderen Plugin-Ladeaufträgen belegt zu sein. GUIShop ist unbrauchbar. \n Laden Sie GUIShop neu und erhöhen Sie das Prüflimit für den Konfigurationszähler auf mehr als {0}"},
                {"NoPermUse", "Sie haben keine Berechtigung {0}"},
                {"Commands", "Befehle"},
                {"Attire", "Kleidung"},
                {"Misc", "Sonstiges"},
                {"Items", "Artikel"},
                {"Ammunition", "Munition"},
                {"Construction", "Konstruktion"},
                {"Component", "Komponenten"},
                {"Traps", "Fallen"},
                {"Electrical", "Elektrik"},
                {"Fun", "Spaß"},
                {"Food", "Essen"},
                {"Resources", "Ressourcen"},
                {"Tool", "Werkzeuge"},
                {"Weapon", "Waffen"},
                {"Medical", "Medizin"},
                {"Minicopter", "Minikopter"},
                {"Sedan", "Limousine"},
                {"Airdrop Call", "Airdrop rufen"},
            }, this, "de"); // German

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Economics", "GUIShop не получает ответа от плагина Экономика. Пожалуйста убедитесь что плагин экономика установлен правильно." },
                {"EconomicsMaxDebt", "Транзакция отклонена, вы уже достигли максимального экономического долга"},
                {"EconomicsExceedsDebt", "Транзакция отклонена, это приведет к превышению максимального экономического долга {0}"},
                {"CustomInvalidID", "Идентификатор пользовательской валюты недействителен, требуется обновление конфигурации для этого магазина {0}"},
                {"MaxEconomicsBalance", "Транзакция отклонена, вы уже достигли максимального экономического предела"},
                {"EconomicsExceedsLimit", "Транзакция отклонена, это приведет к превышению максимального экономического предела заработка, равного {0}"},
                {"ServerRewards", "GUIShop не получает ответа от плагина ServerRewards плагин. Пожалуйста, убедитесь, что ServerRewards плагин установлен правильно." },
                {"TakeCurrency", "GUIShop не смог забрать эту валюту при покупке {0}"},
                {"Bought", "Вы успешно купили {0} {1}."},
                {"Sold", "Вы успешно продали {0} {1}."},
                {"Cooldown", "Вы можете купить этот товар один раз в {0} секунд."},
                {"InventoryFull", "Ваш инвентарь полностью заполнен."},
                {"InventorySlots", "У вас должно быть {0} свободных слота в инвентаре."},
                {"ErrorShop", "Что то произошло с магазином. Пожалуйста сообщите об этом администратору."},
                {"GlobalShopsDisabled", "Глобальные магазины отключены. На данном сервере используются торговцы НПС!"},
                {"DeniedActionInShop", "Вам не разрешено использовать {0} в этом магазине"},
                {"ShopItemItemInvalid", "ВНИМАНИЕ: Данный предмет недействителен! Пожалуйста сообщите об этом администратору!"},
                {"ItemNotValidbuy", "ВНИМАНИЕ: Этот предмет невозможно купить. Пожалуйста сообщите об этом администратору!"},
                {"ItemNotValidsell", "ВНИМАНИЕ: Этот предмет невозможно продать. Пожалуйста сообщите об этом администратору!"},
                {"RedeemKitFail", "ВНИМАНИЕ: При получении комплекта произошла ошибка. Пожалуйста сообщите об этом администратору!"},
                {"NotKit", "Ошибка в название комплекта {0}"},
                {"BuyCmd", "Нельзя купить сразу несколько единиц данного товара!"},
                {"BuyPriceFail", "ВНИМАНИЕ: Цена товара не указана администратором, покупка невозможна"},
                {"SellPriceFail", "ВНИМАНИЕ: Цена товара не указана администратором, продажа невозможна"},
                {"NotEnoughMoney", "Вам нужно {0} монет для покупки {1}x {2}"},
                {"NotEnoughMoneyCustom", "Вам нужно {0} валюты для покупки {1}x {2}"},
                {"CustomCurrencyFail", "ВНИМАНИЕ: Администратор установил магазин {0} для торговли специальной валютой, но не установил действительный идентификатор валюты {1}"},
                {"NotEnoughSell", "У вас недостаточно этого продукта для продажи"},
                {"NotNothingShopFail", "Ошибка, вы не можете купить 0 {0}. Связаться с разработчиком"},
                {"ItemNoExist", "ВНИМАНИЕ: Товар который вы приобретаете - не существует! Пожалуйста сообщите об этом администратору!"},
                {"ItemNoExistTake", "Товар, указанный в магазине, который вы пытаетесь продать, недействителен, свяжитесь с администратором"},
                {"ItemIsNotBlueprintable", "Ошибка Этот предмет {0} в магазине нельзя купить в качестве чертежа! Связаться с администратором"},
                {"BuildingBlocked", "Вы не можете делать покупки, находясь в зоне, заблокированной зданием."},
                {"BlockedMonuments", "Вы не можете искользовать магазин рядом с памятником!"},
                {"ItemNotEnabled", "Продавец отключил продажу этого предмета."},
                {"ItemNotFound", "Товар не найден"},
                {"CantSellCommands", "Вы не можете продать команду обратно в магазин."},
                {"CantSellKits", "Вы не можете продать комплект обратно в магазин."},
                //{"CannotSellWhileEquiped", "Вы не можете продать это оборудование."},
                {"GUIShopResponse", "GUIShop ожидает загрузки ImageLibrary и LangAPI, пожалуйста подождите."},
                {"NPCResponseClose", "Спасибо за покупку {0}. Приходите к нам ещё!"},
                {"NPCResponseOpen", "Добро пожаловать в {0} что хотите приобрести? Нажмите Е для начала торговли!"},
                {"NoPerm", "У вас нет разрешения делать покупки в {0}"},
                {"WipeReady", "Уважаемый {0}, все магазины закрыты на \n {1} минут"},
                {"ImageLibraryFailure", "ImageLibrary отсутствует или занята другими порядками загрузки плагинов. GUIShop непригоден для использования. \n Перезагрузите GUIShop и увеличьте лимит поверки счетчика конфигурации до значения, превышающего {0}"},
                {"NoPermUse", "У вас нет разрешения {0}"},
                {"Commands", "Команды"},
                {"Attire", "Одежда"},
                {"Misc", "Разное"},
                {"Items", "Предметы"},
                {"Ammunition", "Боеприпасы"},
                {"Construction", "Строительство"},
                {"Component", "Компоненты"},
                {"Traps", "Ловушки"},
                {"Electrical", "Электричество"},
                {"Fun", "Веселье"},
                {"Food", "Еда"},
                {"Resources", "Ресурсы"},
                {"Tool", "Инструмент"},
                {"Weapon", "Оружие"},
                {"Medical", "Медицина"},
                {"Minicopter", "Миникоптер"},
                {"Sedan", "Седан"},
                {"Airdrop Call", "Вызов Аирдропа"},
            }, this, "ru"); // Russian
        }
        private string Lang(string key, string id = null) => lang.GetMessage(key, this, id);
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion

        #region Queue

        RequestQueue _requestQueue;

        private void Enqueue(Action action) => _requestQueue.Enqueue(action);

        private class RequestQueue
        {
            readonly Queue<Action> _actions;
            readonly Timer _timer;

            public RequestQueue(float interval = 2f)
            {
                _actions = new Queue<Action>();

                _timer = _instance.timer.Every(interval, Check);
            }

            public void Enqueue(Action action)
            {
                if (action != null)
                {
                    _actions.Enqueue(action);
                }
            }

            public void Destroy()
            {
                _timer?.Destroy();
                _actions.Clear();
            }

            public void Check()
            {
                if (_actions.Count <= 0)
                {
                    return;
                }

                _actions.Dequeue()?.Invoke();
            }
        }

        #endregion

        #region Request

        private class Request
        {
            public Dictionary<string, string> Headers;
            public Message Message;
            public Plugin Plugin;
            public string Webhook;
            public int Retries;

            public Request()
            {
                //
            }

            public Request(string webhook, Message message, Plugin plugin, Dictionary<string, string> headers = null)
            {
                Webhook = webhook;
                Message = message;
                Plugin = plugin;
                Headers = headers;
            }

            public void SendRequest()
            {
                if (_instance._config.DiscordWebHookURL.IsNullOrEmpty()) return;
                _instance.webrequest.Enqueue(Webhook, Message.ToJson(), RequestCallback, Plugin, RequestMethod.POST,
                    Headers);
            }

            public void RequestCallback(int code, string message)
            {
                Response response = new Response
                {
                    Code = code,
                    Message = message,
                };

                if (Retries >= 3)
                {
                    return;
                }

                if (!response.IsLimited && response.IsOk)
                {
                    return;
                }

                Retries++;

                _instance.Enqueue(SendRequest);

                _instance.Puts($"Error sending message, {code}");
            }
        }

        private class Response
        {
            public int Code;
            public string Message;
            public bool IsLimited => Code == 429;
            public bool IsOk => Code == 200 || Code == 204;
        }

        #endregion

        #region Discord

        private class Message
        {
            [JsonProperty("content")] public string Content;

            [JsonProperty("embeds")] public List<Embed> Embeds;

            public Message()
            {
                Embeds = new List<Embed>();
            }

            public Message(string content)
            {
                Embeds = new List<Embed>();
                Content = content;
            }

            public Message AddContent(string content)
            {
                Content = content;

                return this;
            }

            public Message AddEmbed(Embed embed)
            {
                Embeds.Add(embed);

                return this;
            }

            public string ToJson() => JsonConvert.SerializeObject(this);
        }

        private class Author
        {
            [JsonProperty("icon_url")] public string IconUrl;

            [JsonProperty("name")] public string Name;

            [JsonProperty("url")] public string Url;
        }

        private class Embed
        {
            [JsonProperty("author")] public Author Author;

            [JsonProperty("title")] public string Title;

            [JsonProperty("description")] public string Description;

            [JsonProperty("color")] public int Color;

            [JsonProperty("fields")] public List<Field> Fields = new List<Field>();

            public Embed AddTitle(string title)
            {
                Title = title;

                return this;
            }

            public Embed AddDescription(string description)
            {
                Description = description;

                return this;
            }

            public Embed AddColor(string color)
            {
                Color = int.Parse(color.TrimStart('#'), NumberStyles.AllowHexSpecifier);

                return this;
            }

            public Embed AddField(string name, string value, bool inline = false)
            {
                Fields.Add(new Field(name, value, inline));

                return this;
            }

            public Embed AddAuthor(string name, string url, string iconUrl)
            {
                Author = new Author
                {
                    Name = name,
                    Url = url,
                    IconUrl = iconUrl
                };

                return this;
            }
        }

        private class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] public string Name;

            [JsonProperty("value")] public string Value;

            [JsonProperty("inline")] public bool Inline;
        }

        private void SendToDiscord(List<Embed> embeds)
        {
            if (_config.EnableDiscordLogging)
            {
                Message message = new Message();
                message.Embeds = embeds;
                _requestQueue.Enqueue(new Request(_config.DiscordWebHookURL, message, this, _headers).SendRequest);
            }
        }

        private Embed BuildMessage(BasePlayer player)
        {
            Embed embed = new Embed();
            embed.AddAuthor(nameof(GUIShop), "https://umod.org/plugins/gui-shop", _config.DiscordAuthorImage);
            embed.AddColor(_config.DiscordColor);
            embed.AddField("Steam ID", player.UserIDString, true);
            embed.AddField("Display Name", player.displayName.EscapeRichText(), true);
            return embed;
        }

        #endregion

        #region Oxide

        private bool _wipeReady = true;
        private void OnNewSave(string filename)
        {
            _wipeReady = false;
            if (_config.AutoWipe)
            {
                _sellCoolDowns.Clear();
                _sellCoolDownData.Clear();
                _sellLimitResetCoolDowns.Clear();
                _sellLimitResetCoolDownData.Clear();
                _buyCoolDowns.Clear();
                _buyCooldownData.Clear();
                _buyLimitResetCoolDowns.Clear();
                _buyLimitResetCoolDownData.Clear();

                _bought.Clear();
                _boughtData.Clear();
                _sold.Clear();
                _soldData.Clear();
                _limits.Clear();
                _limitsData.Clear();
            }

            _config.Time.LastWipe = _config.Time.WipeTime;
            _config.Time.WipeTime = DateTime.Now.ToString("u");
            SaveConfig();
            timer.Once(_config.Time.CanShopIn, () =>
            {
                PrintToChat("All Shops are open for business!");
                _wipeReady = true;
            });
        }

        readonly Dictionary<ulong, string> _customSpawnables = new Dictionary<ulong, string>
        {
            {
                2255658925, "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"
            }
        };

        private void OnUserConnected(IPlayer player) => GetPlayerData(player.Object as BasePlayer);

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseEntity entity = go.ToBaseEntity();
            if (entity == null || entity.skinID == 0UL)  //Fixed typo 
            {
                return;
            }

            if (_customSpawnables.ContainsKey(entity.skinID))
            {
                SpawnReplacementItem(entity, _customSpawnables[entity.skinID]);

                NextTick(() => entity.Kill());
            }
        }
        private void SpawnReplacementItem(BaseEntity entity, string prefabPath)
        {
            BaseEntity newEntity = GameManager.server.CreateEntity(prefabPath, entity.ServerPosition, entity.ServerRotation);
            if (newEntity == null) return;
            newEntity.Spawn();
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info) //Auto close feature
        {
            if (info == null)
                return;

            if (playerGUIShopUIOpen.Contains(player.UserIDString) && (info.IsProjectile() || 
                                                                      info.damageTypes.Has(DamageType.Bite) ||
                                                                      info.damageTypes.Has(DamageType.Blunt) ||
                                                                      info.damageTypes.Has(DamageType.Drowned) ||
                                                                      info.damageTypes.Has(DamageType.Explosion) ||
                                                                      info.damageTypes.Has(DamageType.Stab) ||
                                                                      info.damageTypes.Has(DamageType.Slash) ||
                                                                      info.damageTypes.Has(DamageType.Fun_Water)))
            {
                DestroyUi(player, true);
            }
        }

        private string Currency = String.Empty;
        private Dictionary<int, string[]> _categoryRects = new Dictionary<int, string[]>();
        private Dictionary<int, float[]> _itemRects = new Dictionary<int, float[]>();

        // button width is 0.05 , 0.525 0.05 0.525
        // 16 buttons max
        // gap ratio 0.006
        //_categoryRects[i] = new string[] { $"{(0.052 + (i * 0.056))} 0.78", $"{(0.1 + (i * 0.056))} 0.82" };
        private void CacheRects()
        {
            for (int i = 0; i < 7; i++)
            {
                float pos = 0.85f - 0.125f * i;

                _itemRects[i] = new float[] {pos + 0.125f, pos};
            }

            int count = 0;

            for (int i = 0; i < _config.ShopCategories.Count; i++)
            {
                if (i <= 16)
                    _categoryRects[i] = new string[] { $"{(0.024 + (i * 0.056))} 0.82", $"{(0.074 + (i * 0.056))} 0.86" };
                else
                {
                    _categoryRects[i] = new string[] { $"{(0.024 + (count * 0.056))} 0.765", $"{(0.074 + (count * 0.056))} 0.805" };
                    count++;
                }
            }
        }

        private void OnServerInitialized()
        {
            CheckConfig();
            CacheRects();
            permission.RegisterPermission(BlockAllow, this);
            permission.RegisterPermission(Use, this);
            permission.RegisterPermission(Admin, this);
            //permission.RegisterPermission(Vip, this);
            permission.RegisterPermission(Color, this);
            permission.RegisterPermission(Button, this);

            if (_config.Economics)
                Currency = "Economics";
            else if (_config.ServerRewards)
                Currency = "ServerRewards";
            else if (_config.CustomCurrency)
                Currency = "Custom";

            if (Economics != null && Economics.IsLoaded && Economics.Call("BalanceLimits") != null)
            {
                _balanceLimits = Economics.Call<KeyValuePair<double, double>>("BalanceLimits");
                if (_balanceLimits.Value != 0)
                    _isEconomicsLimits = true;

                if (_balanceLimits.Key != 0)
                    _isEconomicsDebt = true;
            }

            LibraryCheck();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || player.IsNpc || !player.userID.IsSteamId()) continue;
                GetPlayerLimit(player);
                GetPlayerData(player).ShopKey = String.Empty;
            }

            SaveData();

            if (LangAPI != null && LangAPI.IsLoaded)
                _isLangAPIReady = LangAPI.Call<bool>("IsReady");
        }

        private DynamicConfigFile _buyCoolDowns;
        private DynamicConfigFile _sellCoolDowns;
        private DynamicConfigFile _buyLimitResetCoolDowns;
        private DynamicConfigFile _sellLimitResetCoolDowns;
        private DynamicConfigFile _bought;
        private DynamicConfigFile _sold;
        private DynamicConfigFile _limits;
        private DynamicConfigFile _playerData;
        private DynamicConfigFile _buttonData;

        private void Loaded()
        {
            _instance = this;
            _requestQueue = new RequestQueue();

            cmd.AddChatCommand(_config.shopcommand, this, CmdGUIShop);
            _buyCoolDowns = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/BuyCooldowns");
            _sellCoolDowns = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/SellCooldowns");
            _buyLimitResetCoolDowns = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/BuyLimitResetCoolDowns");
            _sellLimitResetCoolDowns = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/SellLimitResetCoolDowns");
            _bought = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/Purchases");
            _sold = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/Sales");
            _limits = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/Limits");
            _playerData = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/GUIShopPlayerConfigs");
            _buttonData = Interface.Oxide.DataFileSystem.GetFile(nameof(GUIShop) + "/GUIShopButtonSettings");
            LoadData();

            foreach (var category in _config.ShopCategories.Values)
            {
                if (category.Permission.IsNullOrEmpty() || permission.PermissionExists(category.PrefixPermission, this)) continue;

                permission.RegisterPermission(category.PrefixPermission, this);
            }
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyUi(player, true);
                DestroyGUIShopButton(player);
                GetPlayerData(player).ShopKey = String.Empty;
            }
            SaveData();
            _itemRects = null;
            _guishopItemIcons = null;
            _imageListGUIShop = null;
            _instance = null;
        }

        private void OnGroupPermissionGranted(string group, string perm)
        {
            if (perm.Equals(Button))
            {
                foreach (IPlayer player in covalence.Players.Connected.Where(p => permission.UserHasGroup(p.Id, group)))
                {
                    CreateGUIShopButton(player.Object as BasePlayer);
                }
            }
        }

        private void OnGroupPermissionRevoked(string group, string perm)
        {
            if (perm.Equals(Button))
            {
                foreach (IPlayer player in covalence.Players.Connected.Where(p => permission.UserHasGroup(p.Id, group)))
                {
                    DestroyGUIShopButton(player.Object as BasePlayer);
                }
            }
        }

        private void OnUserPermissionGranted(string id, string permName)
        {
            UserData userData = permission.GetUserData(id);
            if (userData != null && permission.GroupsHavePermission(userData.Groups, permName))
            {
                Puts($"This user {id} is already in a group that has {permName} please remove duplication");
            }
            if (permName.Equals(Button))
                CreateGUIShopButton(BasePlayer.Find(id));
        }

        private void OnUserPermissionRevoked(string userId, string perm)
        {
            if (perm.Equals(Button))
                DestroyGUIShopButton(BasePlayer.Find(userId));
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!player.userID.IsSteamId()) return;
            CreateGUIShopButton(player);
            GetPlayerLimit(player);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            GetPlayerLimit(player);
        }

        private void OnPluginLoaded(Plugin name)
        {
            if (ImageLibrary != null && name.Name == ImageLibrary.Name && !_isRestart && ImageLibrary.Call<bool>("IsReady"))
            {
                PrintWarning("ImageLibrary has been detected and GUIShop Images are now being Processed");
                _imageLibraryCheck = 0;
                LibraryCheck();
            }

            if (Economics != null && name.Name == Economics.Name && Economics.Call("BalanceLimits") != null)
            {
                _balanceLimits = Economics.Call<KeyValuePair<double, double>>("BalanceLimits");
                if (_balanceLimits.Value != 0)
                    _isEconomicsLimits = true;

                if (_balanceLimits.Key != 0)
                    _isEconomicsDebt = true;
            }
        }

        private void OnServerSave()
        {
            SaveData();
        }

        #endregion

        #region ImageLibrary

        private void LibraryCheck()
        {
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                _imageLibraryCheck++;

                if (_imageLibraryCheck >= _config.ImageLibraryCounter)
                {
                    _isRestart = false;
                    PrintWarning(Lang("ImageLibraryFailure", null, _config.ImageLibraryCounter));
                    return;
                }

                timer.In(60, LibraryCheck);
                if (ImageLibrary == null)
                {
                    PrintWarning("ImageLibrary is not LOADED! please install");
                }
                else
                    PrintWarning("ImageLibrary appears to be occupied will check again in 1 minute");
                return;
            }

            LoadImages();
        }

        private void LoadImages()
        {
            _isRestart = false;
            _guishopItemIcons = new List<KeyValuePair<string, ulong>>();
            _imageListGUIShop = new Dictionary<string, string>();
            _imageListGUIShop.Add(GUIShopBackgroundImage, _config.BackgroundUrl);
            _imageListGUIShop.Add(GUIShopWelcomeImage, _config.GuiShopWelcomeUrl);
            _imageListGUIShop.Add(GUIShopAmount1Image, _config.AmountUrl);
            _imageListGUIShop.Add(GUIShopAmount2Image, _config.AmountUrl2);
            _imageListGUIShop.Add(GUIShopBuyImage, _config.BuyIconUrl);
            _imageListGUIShop.Add(GUIShopSellImage, _config.SellIconUrl);
            _imageListGUIShop.Add(GUIShopBackArrowImage, _config.BackButtonUrl);
            _imageListGUIShop.Add(GUIShopForwardArrowImage, _config.ForwardButtonUrl);
            _imageListGUIShop.Add(GUIShopCloseImage, _config.CloseButton);
            _imageListGUIShop.Add(GUIShopShopButton, _config.GUI.Image);
            _imageListGUIShop.Add(_config.IconUrl, _config.IconUrl);
   
            foreach (ShopItem shopItem in _config.ShopItems.Values)
            {
                if (!shopItem.Image.IsNullOrEmpty() && !_imageListGUIShop.ContainsKey(shopItem.Image))
                {
                    _imageListGUIShop.Add(shopItem.Image, shopItem.Image);
                }
            }

            ImageLibrary?.Call("ImportImageList", Name, _imageListGUIShop, 0UL, true, new Action(ShopReady));
        }
        private void ShopReady()
        {
            _isShopReady = true;
            _guishopItemIcons.Clear();
            _imageListGUIShop.Clear();
            _imageLibraryCheck = 0;

            int AnyShops = 0;
            foreach (var enabled in _config.ShopCategories.Values.Select(x => x.EnabledCategory))
                if (enabled)
                    AnyShops++;

            if (AnyShops > 0)
                foreach (var player in BasePlayer.activePlayerList) CreateGUIShopButton(player);
        }

        private void OnLangAPIFinished()
        {
            _isLangAPIReady = true;
        }

        #endregion

        #region Main GUIShop UI

        private CuiElementContainer CreateGUIShopOverlay(BasePlayer player, bool toggle)
        {
            CuiElementContainer container = new CuiElementContainer();

            string material = _config.BackgroundImage ? "" : "RobotoCondensed-Bold.ttf";

            container.Add(new CuiPanel //This is the background transparency slider!
                {
                    Image =
                    {
                        Color = $"0 0 0 {GetUITransparency(player)}",
                        Material = material
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0", 
                        AnchorMax = "1 1"
                    },
                    CursorEnabled = true
                },
                "Overlay", GUIShopOverlayName);

            if (_config.BackgroundImage)
                container.Add(new CuiElement //Background image fix
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopBackgroundImage) //updated 2.0.7
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });

            if (toggle)
            {
                container.Add(new CuiElement // GUIShop Welcome MSG
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopWelcomeImage)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.3 0.85",
                            AnchorMax = "0.7 0.95"
                        }
                    }
                });

                /*
                container.Add(new CuiElement // Limit Icon
                {
                    Parent = ShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = GetText("", "image", player) // // Adjust position/size
                            // Png = ImageLibrary?.Call<string>("GetImage", LimitUrl)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.28 0.6",
                            AnchorMax = "0.33 0.65"
                        }
                    }
                });
                */

                container.Add(new CuiElement // Amount Icon
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopAmount1Image) //2.0.7
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.53 0.6", 
                            AnchorMax = "0.58 0.65"
                        }
                    }
                });

                container.Add(new CuiElement // Buy Icon
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopBuyImage) //2.0.7
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.435 0.6", 
                            AnchorMax = "0.465 0.65"
                        }
                    }
                });

                container.Add(new CuiElement // Sell Icon
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopSellImage) //2.0.7
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.685 0.6", //Second digit = Hight Done. First Digit = Position on screen from left to right.
                            AnchorMax = "0.745 0.65" //Left to right size for msg
                        }
                    }
                });

                container.Add(new CuiElement // Amount Icon
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopAmount2Image) //2.0.7
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.8 0.6", 
                            AnchorMax = "0.85 0.65"
                        }
                    }
                });

                container.Add(new CuiElement //close button image
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopCloseImage) //2.0.7
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.45 0.14", 
                            AnchorMax = "0.55 0.19"
                        }
                    }
                });
            }

            container.Add(new CuiLabel //Welcome Msg
                {
                    Text =
                    {
                        Text = GetText(_config.WelcomeMsg, "label", player), //Updated to config output. https://i.imgur.com/Y9n5KgO.png
                        FontSize = 30,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.3 0.85", 
                        AnchorMax = "0.7 0.95"
                    }
                },
                GUIShopOverlayName);

            container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = GetText("", "label", player), //added Config output
                        FontSize = 20,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.2 0.6",
                        AnchorMax = "0.5 0.65" //"0.23 0.65" rebranded to Limit
                    }
                },
                GUIShopOverlayName);

            /*container.Add(new CuiLabel  //Adding missing Lable for limit function TODO
                {
                    Text = {
                        Text = "Limits",
                        FontSize = 20,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter,
                    },
                    RectTransform = {
                        AnchorMin = "0.18 0.6", //"0.2 0.6", Buy
                        AnchorMax = "0.5 0.65" //"0.7 0.65"  Buy
                    }
                },
                ShopOverlayName);  */

            container.Add(new CuiLabel // Amount Label
                {
                    Text =
                    {
                        Text = GetText(_config.AmountLabel, "label", player),
                        FontSize = 20,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.535 0.6", 
                        AnchorMax = "0.7 0.65"
                    }
                },
                GUIShopOverlayName);

            container.Add(new CuiLabel // Buy Price Label,
                {
                    Text =
                    {
                        Text = GetText(_config.BuyLabel, "label", player), //Updated
                        FontSize = 20,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.4 0.6", 
                        AnchorMax = "0.5 0.65"
                    }
                },
                GUIShopOverlayName);

            container.Add(new CuiLabel // Sell label
                {
                    Text =
                    {
                        Text = GetText(_config.SellLabel, "label", player), //Sell $
                        FontSize = 20,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.54 0.6", //Second digit = Hight Done.
                        AnchorMax = "0.89 0.65" //Left to right size for msg
                    }
                },
                GUIShopOverlayName);

            container.Add(new CuiLabel //Amount Label
                {
                    Text =
                    {
                        Text = GetText(_config.AmountLabel2, "label", player),
                        FontSize = 20,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.75 0.6", 
                        AnchorMax = "0.9 0.65"
                    }
                },
                GUIShopOverlayName);

            container.Add(new CuiButton //close button Label
                {
                    Button =
                    {
                        Close = GUIShopOverlayName,
                        Color = "0 0 0 0.40" //"1.4 1.4 1.4 0.14"  new
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.45 0.14", 
                        AnchorMax = "0.55 0.19"
                    },
                    Text =
                    {
                        Text = GetText(_config.CloseButtonlabel, "label", player),
                        FontSize = 20,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    }
                },
                GUIShopOverlayName);

            return container;
        }

        private readonly CuiLabel _guishopDescription = new CuiLabel
        {
            Text =
            {
                Text = "{shopdescription}",
                FontSize = 15,
                Align = TextAnchor.MiddleCenter
            },
            RectTransform =
            {
                AnchorMin = "0.2 0.7", 
                AnchorMax = "0.8 0.75"
            }
        };

        private CuiElementContainer CreateGUIShopItemEntry(ShopItem shopItem, float ymax, float ymin, string shopKey, string color, bool pricebuttons, bool cooldown, BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = $"{(pricebuttons ? shopItem.GetSellPrice(player.UserIDString) : shopItem.GetBuyPrice(player.UserIDString))}",
                        FontSize = 15,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{(pricebuttons ? 0.675 : 0.4)} {ymin}",
                        AnchorMax = $"{(pricebuttons ? 0.755 : 0.5)} {ymax}"
                    }
                },
                GUIShopContentName);

            bool isKitOrCommand = !shopItem.Command.IsNullOrEmpty() || !string.IsNullOrEmpty(shopItem.KitName);
            
            // TODO finish feature integrations
            //bool isPrerequisite = !shopItem.Prerequisite.IsNullOrEmpty();
            // itemCount % 7 == 0 ? itemCount / 7 - 1: itemCount / 7;
           // finish autofix function > for experimental feature int[] autoFix = pricebuttons ? shopItem.SwapLimitToQuantitySoldLimit && shopItem.SellQuantity.Length == 1 && shopItem.SellLimit > 0 && shopItem.SellLimit % shopItem.SellQuantity.FirstOrDefault() == 0 ? shopItem.SellQuantity :  : shopItem.SwapLimitToQuantityBuyLimit && shopItem.BuyQuantity.Length > 0 ? : ;
            int[] maxSteps = pricebuttons ? shopItem.SellLimit == 0 && shopItem.SellQuantity.Length == 0 ? _config.steps : shopItem.SellLimit == 0 ? shopItem.SellQuantity :  shopItem.SellQuantity.Length == 0 ? new[] {1} : shopItem.SellQuantity : shopItem.BuyLimit == 0 && shopItem.BuyQuantity.Length == 0 ? _config.steps : shopItem.BuyLimit == 0 ? shopItem.BuyQuantity :  shopItem.BuyQuantity.Length == 0 ? new[] {1} : shopItem.BuyQuantity;

            if (isKitOrCommand)
            {
                maxSteps = new[] {1};
            }

            if (cooldown)
            {
                return container;
            }

            for (var i = 0; i < maxSteps.Length; i++)
            {
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"shop.{(pricebuttons ? "Sell" : "buy")} {shopKey.Replace(" ", "_")} {shopItem.DisplayName.Replace(" ", "_")} {maxSteps[i]}",
                        Color = color
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{(pricebuttons ? 0.775 : 0.5) + i * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(pricebuttons ? 0.805 : 0.53) + i * 0.03 - 0.001} {ymax}"
                    },
                    Text =
                    {
                        Text = maxSteps[i].ToString(),
                        FontSize = 15,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    }
                }, GUIShopContentName);
            }

            if (!isKitOrCommand && !(!pricebuttons && shopItem.BuyCooldown > 0 || pricebuttons && shopItem.SellCooldown > 0) && !(!pricebuttons && shopItem.BuyLimitResetCoolDown > 0 || pricebuttons && shopItem.SellLimitResetCoolDown > 0))
            {
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"shop.{(pricebuttons ? "Sell" : "buy")} {shopKey.Replace(" ", "_")} {shopItem.DisplayName.Replace(" ", "_")} all",
                        Color = color
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{(pricebuttons ? _config.SellAllButton ? 0.775 : 0 : _config.BuyAllButton ? 0.5 : 0) + maxSteps.Length * 0.03 + 0.001} {ymin}",
                        AnchorMax = $"{(pricebuttons ? _config.SellAllButton ? 0.805 : 0 : _config.BuyAllButton ? 0.53 : 0) + maxSteps.Length * 0.03 - 0.001} {ymax}"
                    },
                    Text =
                    {
                        Text = "All",
                        FontSize = 15,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    }
                }, GUIShopContentName);
            }

            return container;
        }

        private void CreateGUIShopItemIcon(ref CuiElementContainer container, string item, float ymax, float ymin, ShopItem data, BasePlayer player)
        {
            var label = new CuiLabel
            {
                Text =
                {
                    Text = item,
                    FontSize = 15,
                    Color = GetUITextColor(player, _config.TextColor),
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform =
                {
                    AnchorMin = $"0.05 {ymin}", 
                    AnchorMax = $"0.34 {ymax}"
                } // 0.1 0.3
            };

            container.Add(label, GUIShopContentName);

            if (!string.IsNullOrEmpty(data.Image))
            {
                string image = null;

                if ((bool)(ImageLibrary?.Call("HasImage", data.Image, 0UL) ?? false))
                    image = (string)ImageLibrary?.Call("GetImage", data.Image, 0UL, false);
                else
                    image = (string) ImageLibrary?.Call("GetImage", _config.IconUrl);

                container.Add(new CuiElement
                {
                    Parent = GUIShopContentName,
                    Components =
                    {
                        new CuiRawImageComponent { Png = image },
                        new CuiRectTransformComponent { AnchorMin = $"0.01 {ymin}", AnchorMax = $"0.04 {ymax}" } // 0.05 0.08
                    }
                });
            }
            else
            {
               // Puts($"{data.DisplayName} ,{data.SkinId}");
                container.Add(new CuiElement
                {
                    Parent = GUIShopContentName,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            ItemId = data.ItemId,
                            SkinId = data.SkinId,
                        },
                        new CuiRectTransformComponent { AnchorMin = $"0.01 {ymin}", AnchorMax = $"0.04 {ymax}" } // 0.05 0.08
                    }
                });
            }

        }

        private void CreateGUIShopColorChanger(ref CuiElementContainer container, string shopKey, BasePlayer player, bool toggle)
        {
            container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Personal UI Settings",
                        FontSize = 15,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.18 0.11", 
                        AnchorMax = "0.33 0.15"
                    }
                },
                GUIShopOverlayName, "DisplayTag");

            container.Add(new CuiButton //set button 1 + color
                {
                    Button =
                    {
                        Command = $"shop.colorsetting Text {shopKey.Replace(" ", "_")}",
                        Close = GUIShopOverlayName,
                        Color = GetSettingTypeToChange("Text")
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.10 0.09", 
                        AnchorMax = "0.17 0.12"
                    },
                    Text =
                    {
                        Text = "Set Text Color",
                        FontSize = 15,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                GUIShopOverlayName,
                "SetTextColor");

            container.Add(new CuiButton //Toggle Botton (Has enable/disable config option)
                {
                    Button =
                    {
                        Command = $"shop.imageortext {shopKey.Replace(" ", "_")}",
                        Close = GUIShopOverlayName,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.06 0.09", 
                        AnchorMax = "0.10 0.12"
                    },
                    Text =
                    {
                        Text = "Toggle",
                        FontSize = 15,
                        Color = GetUITextColor(player, _config.TextColor),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                GUIShopOverlayName,
                "Toggle");

            container.Add(new CuiButton //set button 3
                {
                    Button =
                    {
                        Command = $"shop.colorsetting Sell {shopKey.Replace(" ", "_")}",
                        Close = GUIShopOverlayName,
                        Color = GetSettingTypeToChange("Sell")
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.10 0.05", 
                        AnchorMax = "0.17 0.08"
                    },
                    Text =
                    {
                        Text = "Sell Color",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter,
                        Color = GetUITextColor(player, _config.TextColor),
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                GUIShopOverlayName,
                "SellChanger");

            container.Add(new CuiButton //set button 2
                {
                    Button =
                    {
                        Command = $"shop.colorsetting Buy {shopKey.Replace(" ", "_")}",
                        Close = GUIShopOverlayName,
                        Color = GetSettingTypeToChange("Buy")
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.10 0.02", 
                        AnchorMax = "0.17 0.05"
                    },
                    Text =
                    {
                        Text = "Buy Color",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter,
                        Color = GetUITextColor(player, _config.TextColor),
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                GUIShopOverlayName,
                "BuyChanger");

            container.Add(new CuiLabel //Display Bar
                {
                    Text =
                    {
                        Text = "ⅢⅢⅢⅢⅢⅢⅢⅢ",
                        Color = GetUITextColor(player, _config.TextColor),
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.80 0.19", 
                        AnchorMax = $"{0.80 + AnchorBarMath(player)} 0.24"
                    }
                },
                GUIShopOverlayName, "ProgressBar");

            if (toggle)
            {
                container.Add(new CuiElement
                {
                    Parent = GUIShopOverlayName, Name = "toggled",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopForwardArrowImage)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.85 0.14",
                            AnchorMax = "0.90 0.19"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = GUIShopOverlayName, Name = "toggled2",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopBackArrowImage)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.80 0.14",
                            AnchorMax = "0.85 0.19"
                        }
                    }
                });
            }

            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"shop.transparency increase {shopKey.Replace(" ", "_")}",
                        Close = GUIShopOverlayName,
                        Color = "0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.85 0.14", 
                        AnchorMax = "0.90 0.19"
                    },
                    Text =
                    {
                        Text = GetText(_config.ForwardButtonText, "label", player), //_config.ForwardButtonText
                        Color = GetUITextColor(player, _config.TextColor),
                        FontSize = 30, Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                GUIShopOverlayName, "MoreTP");

            container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"shop.transparency decrease {shopKey.Replace(" ", "_")}",
                        Close = GUIShopOverlayName,
                        Color = "0 0 0 0.40"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.80 0.14", 
                        AnchorMax = "0.85 0.19"
                    },
                    Text =
                    {
                        Text = GetText(_config.BackButtonText, "label", player),
                        Color = GetUITextColor(player, _config.TextColor),
                        FontSize = 30, Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    }
                },
                GUIShopOverlayName, "LessTP");

            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = $"0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0.18 0.01", 
                    AnchorMax = "0.33 0.12"
                }
            }, GUIShopOverlayName, GUIShopColorPicker);

            int itemPos = 0;

            foreach (string color in _config.ColorsUI)
            {
                int numberPerRow = 4;

                float padding = 0.03f; // Space between each 
                float margin = (0.01f + padding); //left to right alignment adjuster

                //Puts("{0}", padding * (numberPerRow + 1) / numberPerRow);
                float width = ((0.975f - (padding * (numberPerRow + 1))) / numberPerRow);
                //Puts("{0}", width * 1.75f);
                float height = (width * 1.975f);

                int row = (int) Math.Floor((float) itemPos / numberPerRow);
                int col = (itemPos - (row * numberPerRow));
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"shop.uicolor {HexToColor(color)} {shopKey.Replace(" ", "_")}",
                        Close = GUIShopOverlayName,
                        Color = $"{HexToColor(color)} 0.9"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{margin + (width * col) + (padding * col)} {(0.975f - padding) - ((row + 1) * height) - (padding * row)}",
                        AnchorMax = $"{margin + (width * (col + 1)) + (padding * col)} {(0.93f - padding) - (row * height) - (padding * row)}"
                    },
                    Text =
                    {
                        Text = "",
                    }
                }, GUIShopColorPicker, $"ColorPicker_{color}");
                itemPos++;
            }
        }

        private void CreateGUIShopChangePage(ref CuiElementContainer container, string shopKey, int minus, int plus, BasePlayer player, bool toggle)
        {
            if (toggle)
            {
                container.Add(new CuiElement
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopBackArrowImage)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.345 0.14",
                            AnchorMax = "0.445 0.19"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = GUIShopOverlayName,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", GUIShopForwardArrowImage)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.555 0.14",
                            AnchorMax = "0.655 0.19"
                        }
                    }
                });
            }

            if (_shopPage[player.userID] != minus)
            {
                container.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"shop.show {shopKey.Replace(" ", "_")} {minus}",
                            Color = "0 0 0 0.40"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.345 0.14", 
                            AnchorMax = "0.445 0.19"
                        },
                        Text =
                        {
                            Text = GetText(_config.BackButtonText, "label", player),
                            Color = GetUITextColor(player, _config.TextColor),
                            FontSize = 30,
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    GUIShopOverlayName,
                    "ButtonBack");
            }

            if (_shopPage[player.userID] != plus)
            {
                container.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"shop.show {shopKey.Replace(" ", "_")} {plus}",
                            Color = "0 0 0 0.40"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.555 0.14",
                            AnchorMax = "0.655 0.19"
                        },
                        Text =
                        {
                            Text = GetText(_config.ForwardButtonText, "label", player),
                            Color = GetUITextColor(player, _config.TextColor),
                            FontSize = 30,
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    GUIShopOverlayName,
                    "ButtonForward");
            }
        }

        private void CreateShopButton(ref CuiElementContainer container, ShopCategory shopCategory, int minus, int rowPos, BasePlayer player) 
        {
            container.Add(new CuiButton
            {
                Button =
                {
                    Command = $"shop.show {shopCategory.DisplayName.Replace(" ", "_")} {minus}",
                    Color = "0.5 0.5 0.5 0.5" //"1.2 1.2 1.2 0.24" new
                },
                RectTransform =
                {
                    AnchorMin = _categoryRects[rowPos][0], // $"{(0.09 + (rowPos * 0.056))} 0.78", // * 0.056 = Margin for more buttons... less is better
                    AnchorMax = _categoryRects[rowPos][1] //$"{(0.14 + (rowPos * 0.056))} 0.82"
                },
                Text =
                {
                    Text = Lang(shopCategory.DisplayName, player.UserIDString),
                    Align = TextAnchor.MiddleCenter,
                    Color = GetUITextColor(player, shopCategory.DisplayNameColor ?? _config.TextColor),
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12
                }
            }, GUIShopOverlayName, shopCategory.DisplayName);
        }

        private void DestroyUi(BasePlayer player, bool full = false)
        {
            CuiHelper.DestroyUi(player, GUIShopContentName);
            CuiHelper.DestroyUi(player, "ButtonForward");
            CuiHelper.DestroyUi(player, "ButtonBack");
            if (!full) return;
            CuiHelper.DestroyUi(player, GUIShopDescOverlay);
            CuiHelper.DestroyUi(player, GUIShopOverlayName);
        }

        #endregion

        #region GUIShop Overlay Button

        private void CreateGUIShopButton(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId() || !player.IsAlive()) return;

            if (!permission.UserHasPermission(player.UserIDString, Button)) return;

            if (_playerDisabledButtonData.Contains(player.UserIDString))
            {
                DestroyGUIShopButton(player); 
                return;
            }

            CuiHelper.DestroyUi(player, Button);
            var elements = new CuiElementContainer();
            var shopButton = elements.Add(new CuiPanel
            {
                Image = { Color = _instance._config.GUI.Color }, //$"{HexToColor(_instance._config.GUI.Color)} 0.15" },
                RectTransform = {
                    AnchorMin = _config.GUI.GUIButtonPosition.AnchorsMin,
                    AnchorMax = _config.GUI.GUIButtonPosition.AnchorsMax,
                    OffsetMin = _config.GUI.GUIButtonPosition.OffsetsMin,
                    OffsetMax = _config.GUI.GUIButtonPosition.OffsetsMax
                },
                CursorEnabled = false
            }, "Overlay", Button);

            elements.Add(new CuiElement
            {
                Parent = Button,
                Components = {
                    new CuiRawImageComponent { Png = ImageLibrary?.Call<string>("GetImage", GUIShopShopButton) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = "shop.button", 
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0 0", 
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Text = ""
                }
            }, shopButton);

            CuiHelper.AddUi(player, elements);
        }

        private void DestroyGUIShopButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Button);
        }

        #endregion

        #region Currency

        [Flags]
        public enum InventoryTypes
        {
            ContainerMain,
            ContainerBelt,
            ContainerWear,
            ContainerAll = ContainerMain | ContainerBelt | ContainerWear
        }

        private int GetAmount(PlayerInventory inventory, InventoryTypes type, int itemid, string display, bool used, ulong skinId = 0UL)
        {
            if (itemid == 0)
                return 0;
            int num = 0;
            if (inventory.containerMain != null && type.HasFlag(InventoryTypes.ContainerMain))
                num += GetAmount(inventory.containerMain, itemid, display, used, skinId);
            if (inventory.containerBelt != null && type.HasFlag(InventoryTypes.ContainerBelt))
                num += GetAmount(inventory.containerBelt, itemid, display, used, skinId);
            if (inventory.containerWear != null && type.HasFlag(InventoryTypes.ContainerWear))
                num += GetAmount(inventory.containerWear, itemid, display, used, skinId);
            return num;
        }

        private int GetAmount(ItemContainer container, int itemid, string display, bool used, ulong skinId)
        {
            int num = 0;
            foreach (Item obj in container.itemList)
            {
                if (obj.isBroken || obj.hasCondition && !used && obj.amount == 1 && obj.condition < obj.info.condition.max) continue;

                if (!string.IsNullOrEmpty(display) ? 
                        obj.name == display && obj.info.itemid == itemid && obj.skin == skinId && (!obj.hasCondition || used && obj.hasCondition || obj.hasCondition && obj.condition == obj.info.condition.max || obj.hasCondition && !used && obj.amount != 1 && obj.condition < obj.info.condition.max) : 
                        string.IsNullOrEmpty(display) && obj.info.itemid == itemid && obj.skin == skinId && (!obj.hasCondition || used && obj.hasCondition || obj.hasCondition && obj.condition == obj.info.condition.max || obj.hasCondition && !used && obj.amount != 1 && obj.condition < obj.info.condition.max))
                {
                    if (!obj.hasCondition || used && obj.hasCondition || obj.hasCondition && obj.condition == obj.info.condition.max)
                        num += obj.amount;
                    else
                        num += obj.amount - 1;
                }
            }
            return num;
        }

        private double GetCurrency(ShopCategory shop, BasePlayer player)
        {
            if (shop?.Currency.Contains("custom") == true)
            {
                return GetAmount(player.inventory, _config.AllowedSellContainers, shop.CustomCurrencyIDs, shop.CustomCurrencyNames, shop.CustomCurrencyAllowSellOfUsedItems, shop.CustomCurrencySkinIDs);
            }

            if (Economics != null && (_config.Economics || shop?.Currency.Contains("economics") == true))
            {
                return Economics != null ? (double)Economics.Call("Balance", player.UserIDString) : 0;
            }

            if (ServerRewards != null && (_config.ServerRewards || shop?.Currency.Contains("serverrewards") == true))
            {
                return (int)ServerRewards.Call("CheckPoints", player.UserIDString);
            }

            if (_config.CustomCurrency)
            {
                return GetAmount(player.inventory, _config.AllowedSellContainers, _config.CustomCurrencyID, _config.CustomCurrencyName, _config.CustomCurrencyAllowSellOfUsedItems, _config.CustomCurrencySkinID);
            }

            return 0;
        }

        private int Take(PlayerInventory inventory, InventoryTypes type, List<Item> collect, int itemid, int amount, bool used, string display, ulong skinId = 0UL)
        {
            int num1 = 0;
            if (inventory.containerMain != null && type.HasFlag(InventoryTypes.ContainerMain))
            {
                int num2 = Take(inventory.containerMain, collect, itemid, amount, used, display, skinId);
                num1 += num2;
                amount -= num2;
            }

            if (amount <= 0)
                return num1;
            if (inventory.containerBelt != null && type.HasFlag(InventoryTypes.ContainerBelt))
            {
                int num2 = Take(inventory.containerBelt, collect, itemid, amount, used, display, skinId);
                num1 += num2;
                amount -= num2;
            }

            if (amount <= 0 || inventory.containerWear == null && type.HasFlag(InventoryTypes.ContainerWear))
                return num1;
            int num3 = Take(inventory.containerWear, collect, itemid, amount, used, display, skinId);
            num1 += num3;
            amount -= num3;
            return num1;
        }

        private int Take(ItemContainer container, List<Item> collect, int itemid, int iAmount, bool used, string display, ulong skinId)
        {
            int num1 = 0;
            if (iAmount == 0) return num1;
            List<Item> list = Facepunch.Pool.GetList<Item>();
            foreach (Item obj in container.itemList)
            {
                if (obj.isBroken || obj.hasCondition && used == false && obj.amount == 1 && obj.condition < obj.info.condition.max) continue;
                if (obj.info.itemid == itemid && obj.skin == skinId && (display.IsNullOrEmpty() ? used ? !obj.hasCondition || obj.hasCondition : !obj.hasCondition || obj.hasCondition && obj.condition == obj.maxCondition :
                        obj.name.IsNullOrEmpty() || !obj.name.IsNullOrEmpty() && obj.name == display && used ? !obj.hasCondition || obj.hasCondition : !obj.hasCondition || obj.hasCondition && obj.condition == obj.maxCondition))
                {
                    int num2 = iAmount - num1;
                    if (num2 > 0)
                    {
                        if (obj.amount > num2)
                        {
                            obj.MarkDirty();
                            obj.amount -= num2;
                            num1 += num2;
                            obj.name = display;
                            Item byItemId = ItemManager.CreateByItemID(itemid);
                            byItemId.amount = num2;
                            byItemId.CollectedForCrafting(container.playerOwner);
                            if (collect != null)
                            {
                                collect.Add(byItemId);
                                break;
                            }

                            break;
                        }

                        if (obj.amount <= num2)
                        {
                            num1 += obj.amount;
                            list.Add(obj);
                            collect?.Add(obj);
                        }

                        if (num1 == iAmount)
                            break;
                    }
                }
            }

            foreach (Item obj in list)
            {
                obj.RemoveFromContainer();
                obj.Remove();
            }
            Facepunch.Pool.FreeList<Item>(ref list);
            return num1;
        }

        private int GetAmountPrice(double amount)
        {
            if (amount <= 0.5)
            {
                return (int)Math.Ceiling(amount);
            }
        
            return (int)Math.Round(amount);
        }

        private bool TakeCurrency(ShopCategory shop, BasePlayer player, double amount)
        {
            if (shop?.Currency.Contains("custom") == true && GetAmount(player.inventory, _config.AllowedSellContainers, shop.CustomCurrencyIDs, shop.CustomCurrencyNames, shop.CustomCurrencyAllowSellOfUsedItems, shop.CustomCurrencySkinIDs) >= GetAmountPrice(amount))
            {
                if (string.IsNullOrEmpty(shop.CustomCurrencyNames) == false)
                {
                    Take(player.inventory, _config.AllowedSellContainers, null, shop.CustomCurrencyIDs, GetAmountPrice(amount), shop.CustomCurrencyAllowSellOfUsedItems, shop.CustomCurrencyNames, shop.CustomCurrencySkinIDs);
                    return true;
                }
                Take(player.inventory, _config.AllowedSellContainers, null, shop.CustomCurrencyIDs, GetAmountPrice(amount), shop.CustomCurrencyAllowSellOfUsedItems, null, shop.CustomCurrencySkinIDs);
                return true;
            }

            if (_config.Economics && Economics != null || Economics != null && shop?.Currency.Contains("economics") == true)
            {
                //Puts($"output response is {Economics.Call<bool>("Withdraw", player.userID, amount)}");
                return Economics.Call<bool>("Withdraw", player.userID, amount);
            }

            if (_config.ServerRewards && ServerRewards != null || ServerRewards != null && shop?.Currency.Contains("serverrewards") == true)
            {
                return ServerRewards.Call<object>("TakePoints", player.userID, GetAmountPrice(amount)) != null;
            }

            if (_config.CustomCurrency && GetAmount(player.inventory, _config.AllowedSellContainers, _config.CustomCurrencyID, _config.CustomCurrencyName, _config.CustomCurrencyAllowSellOfUsedItems, _config.CustomCurrencySkinID) >= GetAmountPrice(amount))
            {
                if (_config.CustomCurrencyName.IsNullOrEmpty() == false)
                {
                    Take(player.inventory, _config.AllowedSellContainers, null, _config.CustomCurrencyID, GetAmountPrice(amount), _config.CustomCurrencyAllowSellOfUsedItems, _config.CustomCurrencyName, _config.CustomCurrencySkinID);
                    return true;
                }
                Take(player.inventory, _config.AllowedSellContainers, null, _config.CustomCurrencyID, GetAmountPrice(amount), _config.CustomCurrencyAllowSellOfUsedItems, null, _config.CustomCurrencySkinID);
                return true;
            }

            return false;
        }

        private void AddCurrency(ShopCategory shop, BasePlayer player, double amount, string item) // Updated 2.2.49 Fixed Currency item not including names?
        {
            if (shop?.Currency.Contains("custom") == true)
            {
                Item currency = ItemManager.CreateByItemID(shop.CustomCurrencyIDs, GetAmountPrice(amount), shop.CustomCurrencySkinIDs);
                if (!shop.CustomCurrencyNames.IsNullOrEmpty())
                {
                    currency.name = shop.CustomCurrencyNames;
                    //currency.MarkDirty();
                }
                player.GiveItem(currency);
                return;
            }

            if (_config.Economics && Economics != null || Economics != null && shop?.Currency.Contains("economics") == true || Economics != null && item.Contains("economics") && _isEconomicsDebt)
            {
                Economics?.Call("Deposit", player.UserIDString, amount);
                return;
            }

            if (_config.ServerRewards && ServerRewards != null || ServerRewards != null && shop?.Currency.Contains("serverrewards") == true)
            {
                ServerRewards?.Call("AddPoints", player.UserIDString, GetAmountPrice(amount));
                return;
            }

            if (_config.CustomCurrency)
            {
                Item currency = ItemManager.CreateByItemID(_config.CustomCurrencyID, GetAmountPrice(amount), _config.CustomCurrencySkinID);
                if (!_config.CustomCurrencyName.IsNullOrEmpty())
                {
                    currency.name = _config.CustomCurrencyName;
                    //currency.MarkDirty();
                }

                player.GiveItem(currency);
            }
        }

        #endregion

        #region Shop

        private void ShowGUIShops(BasePlayer player, string shopKey, int from = 0, bool fullPaint = true, bool refreshFunds = false)
        {
            bool toggle = GetPlayerData(player).ImageOrText;
            _shopPage[player.userID] = from;

            ShopCategory shop;
            if (!_config.ShopCategories.TryGetValue(shopKey, out shop))
            {
                SendReply(player, Lang("ErrorShop", player.UserIDString));
                return;
            }

            if (_config.BlockMonuments && !shop.EnableNPC && IsNearMonument(player))
            {
                SendReply(player, Lang("BlockedMonuments", player.UserIDString));
                return;
            }

            ItemLimit itemLimit = GetPlayerLimit(player);
            double playerCoins = GetCurrency(shop, player);

            CuiElementContainer container;

            _guishopDescription.Text.Text = string.Format(shop.Description, playerCoins);
            _guishopDescription.Text.Color = GetUITextColor(player, shop.DescriptionColor ?? _config.TextColor);

            if (fullPaint)
            {
                CuiHelper.DestroyUi(player, GUIShopOverlayName);

                container = CreateGUIShopOverlay(player, toggle);

                container.Add(_guishopDescription, GUIShopOverlayName, GUIShopDescOverlay);

                if (permission.UserHasPermission(player.UserIDString, Color) || _config.PersonalUI)
                {
                    CreateGUIShopColorChanger(ref container, shopKey, player, toggle); //2.0.7 updating UI
                }

                int rowPos = 0;

                string vendor = String.Empty;
                foreach (string id in shop.NpcIds)
                {
                    if (NearNpc(id ,player))
                    {
                        vendor = id;
                    }
                }

                if (shop.EnableNPC && !string.IsNullOrEmpty(vendor))
                {
                    foreach (ShopCategory cat in _config.ShopCategories.Values.Where(i => i.EnableNPC && i.NpcIds.Contains(vendor)))
                    {
                        if (!string.IsNullOrEmpty(cat.Permission) && !permission.UserHasPermission(player.UserIDString, cat.PrefixPermission)) continue;
                        CreateShopButton(ref container, cat, from, rowPos, player);
                        rowPos++;
                    }
                }
                else
                {
                    foreach (ShopCategory cat in _config.ShopCategories.Values.Where(i => i.EnabledCategory))
                    {
                        if (!string.IsNullOrEmpty(cat.Permission) && !permission.UserHasPermission(player.UserIDString, cat.PrefixPermission)) continue;
                        CreateShopButton(ref container, cat, from, rowPos, player);
                        rowPos++;
                    }
                }
            }
            else
            {
                container = new CuiElementContainer();
            }

            CuiHelper.DestroyUi(player, GUIShopContentName);
            CuiHelper.DestroyUi(player, "ButtonBack");
            CuiHelper.DestroyUi(player, "ButtonForward");

            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"  //was 0 0 0 1
                },
                RectTransform =
                {
                    AnchorMin = "0 0.2", 
                    AnchorMax = "1 0.6"
                }
            }, GUIShopOverlayName, GUIShopContentName);

            if (refreshFunds)
            {
                CuiHelper.DestroyUi(player, GUIShopDescOverlay);
                container.Add(_guishopDescription, GUIShopOverlayName, GUIShopDescOverlay);
            }

            int itemIndex = 0;

            GetCategoryItems(shop);

            foreach (ShopItem data in shop.ShopItems.Skip(from * 7).Take(7))
            {
                float cachedRect = _itemRects[itemIndex][0];
                float cachedRect2 = _itemRects[itemIndex][1];
                string name = data.DisplayName; // TODO: Updated 12/12 2020
                
                itemLimit.CheckBuyLimit(data.Shortname, data.BuyLimit);
                itemLimit.CheckSellLimit(data.Shortname, data.SellLimit);

                int B = 0;
                int S = 0;

                if (data.BuyLimit > 0)
                {
                    B = itemLimit.BLimit[data.Shortname] == 0 ? data.BuyLimit : data.BuyLimit - itemLimit.BLimit[data.Shortname];
                }

                if (data.SellLimit > 0)
                {
                    S = itemLimit.SLimit[data.Shortname] == 0 ? data.SellLimit : data.SellLimit - itemLimit.SLimit[data.Shortname];
                }

                string cooldowndescription = string.Empty;
                string limitdescription = string.Empty;

                double sellCooldown;
                double buyCooldown;
                double buylimitcooldown;
                double selllimitcooldown;

                bool hasSellCooldown =
                    data.SellLimitResetCoolDown > 0 && HasSellLimitCoolDown(player.userID, data.DisplayName, out selllimitcooldown) ||
                    data.SellCooldown > 0 && HasSellCooldown(player.userID, data.DisplayName, out sellCooldown);
                
                bool hasBuyCooldown =
                    data.BuyLimitResetCoolDown > 0 && HasBuyLimitCoolDown(player.userID, data.DisplayName, out buylimitcooldown) ||
                    data.BuyCooldown > 0 && HasBuyCooldown(player.userID, data.DisplayName, out buyCooldown);

                bool cooldown = data.BuyCooldown > 0 || data.SellCooldown > 0 || data.BuyLimit > 0 || data.SellLimit > 0;

                if (data.BuyCooldown > 0)
                {
                    cooldowndescription += $" (Buy CoolDown  {FormatTime(data.BuyCooldown)})";
                }

                if (data.SellCooldown > 0)
                {
                    cooldowndescription += $" (Sell CoolDown  {FormatTime(data.SellCooldown)})"; //TODO: Add into lang file/multi support
                }

                if (data.BuyLimit > 0)
                {
                    if (data.BuyLimitResetCoolDown > 0 && HasBuyLimitCoolDown(player.userID, data.DisplayName, out buylimitcooldown))
                    {
                        limitdescription += $" (Out of Stock)";
                    }
                    else
                    {
                        limitdescription += $" (Buy Limit {B})";
                    }
                }

                if (data.SellLimit > 0)
                {
                    if (data.SellLimitResetCoolDown > 0 && HasSellLimitCoolDown(player.userID, data.DisplayName, out selllimitcooldown))
                    {
                        limitdescription += $" (Out of Stock)";
                    }
                    else
                    {
                        limitdescription += $" (Sell Limit {S})";
                    }
                }

                /*Lang(name, player.UserIDString)*/
                name = $"{GetItemDisplayName(data.Shortname, data.DisplayName, player.UserIDString)}<size=11>{(cooldown ? "\n" + cooldowndescription + limitdescription : "")}</size>"; //added Updated,  Creates new line for cooldowns under the Displayed Item Names.

                CreateGUIShopItemIcon(ref container, name, cachedRect, cachedRect2, data, player);

                if (data.EnableBuy)
                    container.AddRange(CreateGUIShopItemEntry(data, cachedRect, cachedRect2, shopKey, GetUIBuyBoxColor(player), false,
                        hasBuyCooldown, player));

                if (data.EnableSell)
                    container.AddRange(CreateGUIShopItemEntry(data, cachedRect, cachedRect2, shopKey, GetUISellBoxColor(player), true,
                        hasSellCooldown, player));

                itemIndex++;
            }

            int itemCount = shop.ShopItems.Count;
            int totalPages = itemCount == 7 ? 0 : itemCount % 7 == 0 ? itemCount / 7 - 1: itemCount / 7;

            int minfrom = from <= 0 ? 0 : from - 1;
            int maxfrom = itemIndex != 7 || totalPages == 0 || totalPages == from ? from : from + 1;

            CreateGUIShopChangePage(ref container, shopKey, minfrom, maxfrom, player, toggle);

            CuiHelper.AddUi(player, container);
        }

        private void GetCategoryItems(ShopCategory category)
        {
            if (category.ShopItems != null) return;

            category.ShopItems = new HashSet<ShopItem>();
            foreach (var shortname in category.Items)
            {
                if (!_config.ShopItems.ContainsKey(shortname)) continue;

                category.ShopItems.Add(_config.ShopItems[shortname]);
            }
        }

        private object CheckAction(BasePlayer player, string shopKey, string item, string ttype)
        {
            if (!_config.ShopCategories.ContainsKey(shopKey) || !_config.ShopCategories[shopKey].EnabledCategory && !_config.ShopCategories[shopKey].EnableNPC || !string.IsNullOrEmpty(_config.ShopCategories[shopKey].Permission) && !permission.UserHasPermission(player.UserIDString, _config.ShopCategories[shopKey].PrefixPermission))
            {
                return Lang("DeniedActionInShop", player.UserIDString, ttype);
            }

            if (!_config.ShopItems.ContainsKey(item) || !_config.ShopCategories[shopKey].Items.Contains(item))
            {
                return Lang("ItemNotFound", player.UserIDString);
            }

            if (!_config.ShopCategories[shopKey].EnabledCategory && _config.ShopCategories[shopKey].EnableNPC && _config.ShopCategories[shopKey].NpcIds.Count > 0)
            {
                string vendor = String.Empty;
                foreach (string id in _config.ShopCategories[shopKey].NpcIds)
                {
                    if (NearNpc(id ,player))
                    {
                        vendor = id;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(vendor))
                    return Lang("NoPerm", player.UserIDString, shopKey);
            }

            if (!_config.ShopItems[item].EnableBuy && ttype == "buy")
            {
                return Lang("ItemNotEnabled", player.UserIDString, ttype);
            }

            if (!_config.ShopItems[item].EnableSell && ttype == "Sell")
            {
                return Lang("ItemNotEnabled", player.UserIDString, ttype);
            }

            return true;
        }

        private object IsShop(BasePlayer player, string shopKey)
        {
            if (!_config.ShopCategories.ContainsKey(shopKey))
                return Lang("ErrorShop", player.UserIDString);

            return true;
        }

        #endregion

        #region Buy

        // Updated to support LangAPI + Fixes message responses to be more accurate when failing to take currencies.

        private int newamounts = 0;
        private object TryShopBuy(BasePlayer player, string shopKey, string item, int amount, bool isAll)
        {
            var slots = FreeSlots(player);
            if (isAll && _isEconomicsDebt)
            {
                ShopCategory shopCategory = _config.ShopCategories[shopKey];
                if (shopCategory != null && (shopCategory.Currency.Equals("economics") || string.IsNullOrEmpty(shopCategory.Currency) && _config.Economics))
                {
                    double balance = Economics.Call<double>("Balance", player.UserIDString);
                    double max = _balanceLimits.Key;
                    ShopItem datas = _config.ShopItems[item];
                    double price = datas.GetBuyPrice(player.UserIDString);
                    int newamount = 0;

                    if (balance.Equals(max))
                    {
                        return Lang("EconomicsMaxDebt", player.UserIDString);
                    }

                    //Puts($"{balance > max} {max < balance}");
                    while (balance > max)
                    {
                        balance -= price;
                        newamount++;
                    }

                    //Puts($"current {balance}, max {max}, new amount {newamount}, new balance{balance}");
                    amount = newamount;
                    newamounts = newamount;
                }
            }
            else if (amount <= 0)
            {
                return false;
            }

            object success = IsShop(player, shopKey);

            if (success is string)
            {
                return success;
            }

            success = CheckAction(player, shopKey, item, "buy");

            if (success is string)
            {
                return success;
            }

            success = CanBuy(player, _config.ShopCategories[shopKey], item, amount, isAll);

            if (success is string)
            {
                return success;
            }

            if (slots == 0)
            {
                //Puts("called here");
                return Lang("InventoryFull", player.UserIDString);
            }

            ShopItem data = _config.ShopItems[item];

            var purchase = data.GetBuyPrice(player.UserIDString) * amount;

            if (!TakeCurrency(_config.ShopCategories[shopKey], player, purchase))
            {
                ItemDefinition itemDef = ItemManager.FindItemDefinition(_config.ShopCategories[shopKey].CustomCurrencyIDs);
                string type = _config.ShopCategories[shopKey].Currency.Equals("custom") ? _isLangAPIReady ? GetItemDisplayName(itemDef.shortname, itemDef.displayName.english, player.UserIDString) : itemDef.displayName.english
                    : string.IsNullOrEmpty(_config.ShopCategories[shopKey].Currency) ? Currency : _config.ShopCategories[shopKey].Currency;
                return Lang("TakeCurrency", player.UserIDString, type);
            }

            success = TryGive(player, _config.ShopCategories[shopKey], item, amount);

            if (success is string)
            {
                return success;
            }

            if (success is bool && (bool)success == false)
            {
                AddCurrency(_config.ShopCategories[shopKey], player, purchase, "economics");
                return Lang("TryGiveFailed Contact Developer", player.UserIDString);
            }

            //Puts($"{shop == null} {amount * data.BuyPrice}");

            Interface.Call("OnGUIShopBuy", player.UserIDString, data.GetBuyPrice(player.UserIDString), amount, data.Shortname);

            if (data.BuyCooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;

                if (!_buyCooldownData.TryGetValue(player.userID, out itemCooldowns))
                    _buyCooldownData[player.userID] = itemCooldowns = new Dictionary<string, double>();
                
                itemCooldowns[item] = CurrentTime() + data.BuyCooldown /* *amount */;
            }

            if (!string.IsNullOrEmpty(data.Shortname))
            {
                ulong count;

                _boughtData.TryGetValue(data.Shortname, out count);

                _boughtData[data.Shortname] = count + (ulong) amount;
            }
            return false;
        }

        private object CanBuy(BasePlayer player, ShopCategory shopCategory, string item, int amount, bool isAll)
        {
            if (ServerRewards == null && (_config.ServerRewards && string.IsNullOrEmpty(shopCategory?.Currency) || shopCategory?.Currency.Contains("serverrewards") == true))
            {
                return Lang("ServerRewards", player.UserIDString);
            }

            if (Economics == null && (_config.Economics && string.IsNullOrEmpty(shopCategory?.Currency) || shopCategory?.Currency.Contains("economics") == true))
            {
                return Lang("Economics", player.UserIDString);
            }

            if (!_config.ShopItems.ContainsKey(item))
            {
                return Lang("ItemNotValidbuy", player.UserIDString);
            }

            var data = _config.ShopItems[item];
            if (data.GetBuyPrice(player.UserIDString) < 0)
            {
                return Lang("BuyPriceFail", player.UserIDString);
            }

            if (!data.Command.IsNullOrEmpty() && amount > 1)
            {
                return Lang("BuyCmd", player.UserIDString);
            }

            if ((_config.CustomCurrency && string.IsNullOrEmpty(shopCategory.Currency) && ItemManager.FindItemDefinition(_config.CustomCurrencyID) == null || shopCategory.Currency.Equals("custom")) && ItemManager.FindItemDefinition(shopCategory.CustomCurrencyIDs) == null)
            {
                return Lang("CustomInvalidID", player.UserIDString, shopCategory.DisplayName);
                //$"\n CustomCurrencyIDs was null or not a correct rust item ID for {shopKey} shop, config requires updating"
            }

            double buyprice = data.GetBuyPrice(player.UserIDString);

            //Puts($"buyprice {buyprice}, getcurrency {GetCurrency(shopCategory, player)}, less than {GetCurrency(shopCategory, player) < buyprice * amount}");

            if (_isEconomicsDebt)
            {
                if (shopCategory.Currency.Equals("economics") || string.IsNullOrEmpty(shopCategory.Currency) && _config.Economics)
                {
                    double balance = Economics.Call<double>("Balance", player.UserIDString);
                    double max = _balanceLimits.Key;
                    
                    if (balance.Equals(max))
                    {
                        return Lang("EconomicsMaxDebt", player.UserIDString);
                    }

                    if (!isAll)
                    {
                        if (balance - (buyprice * amount) > -max)
                        {
                            return Lang("EconomicsExceedsDebt", player.UserIDString, max);
                        }
                    }
                }
            }

            if (GetCurrency(shopCategory, player) < buyprice * amount) // Fixed Custom Currency Issues in regards to message responses
            {
                if (shopCategory?.Currency.Contains("custom") == true)
                {
                    ItemDefinition currency = ItemManager.FindItemDefinition(shopCategory.CustomCurrencyIDs);
                    if (currency == null)
                    {
                        return Lang("CustomCurrencyFail", player.UserIDString, shopCategory.DisplayName, shopCategory.CustomCurrencyIDs);
                    }
                    
                    if (GetCurrency(shopCategory, player) == 0)
                        return Lang("NotEnoughMoneyCustom", player.UserIDString, buyprice * amount, amount, item);
                }
                else if (_config.CustomCurrency)
                {
                    return Lang("NotEnoughMoneyCustom", player.UserIDString, buyprice * amount, amount, item);
                }

                if (_isEconomicsDebt)
                {
                    if (shopCategory.Currency.Equals("economics") || string.IsNullOrEmpty(shopCategory.Currency) && _config.Economics)
                    {
                        double balance = Economics.Call<double>("Balance", player.UserIDString);
                        double max = _balanceLimits.Key;
                    
                        if (balance.Equals(max))
                        {
                            return Lang("EconomicsMaxDebt", player.UserIDString);
                        }

                        if (!isAll)
                        {
                            if (balance - (buyprice * amount) > -max)
                            {
                                return Lang("EconomicsExceedsDebt", player.UserIDString, max);
                            }
                        }
                    }
                }
                else
                    return Lang("NotEnoughMoney", player.UserIDString, buyprice * amount, amount, item);
            }

            if (data.BuyCooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;
                double itemCooldown;

                if (_buyCooldownData.TryGetValue(player.userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime())
                {
                    return Lang("Cooldown", player.UserIDString, FormatTime((long) (itemCooldown - CurrentTime())));
                }
            }

            ItemLimit itemLimit = GetPlayerLimit(player);
            if (data.BuyLimit > 0 && itemLimit.BLimit[data.Shortname] >= data.BuyLimit)
            {
               // Puts($"{data.DisplayName}, {player.displayName}, {itemLimit.CheckBuyLimit(data.Shortname, amount)}");
               if (data.BuyLimitResetCoolDown > 0)
               {
                   Dictionary<string, double> itemCooldowns;
                   double itemCooldown;
                   if (_buyLimitResetCoolDownData.TryGetValue(player.userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime())
                   {
                       return Lang( $"Buy Limit of {data.BuyLimit} was Reached for {data.DisplayName} \n This limit resets in {FormatTime((long) (itemCooldown - CurrentTime()))} seconds", player.UserIDString);
                   }
               }
               else
                return Lang($"Buy Limit of {data.BuyLimit} Reached for {data.DisplayName}", player.UserIDString);
            }

            return true;
        }

        private int GetAmountBuy(BasePlayer player, ShopCategory shopCategory, string item)
        {
            ShopItem data = _config.ShopItems[item];
            ItemDefinition definition = ItemManager.FindItemDefinition(data.Shortname);
            if (definition == null) return 0;
            var freeSlots = FreeSlots(player);

            double amountcanbuy = GetCurrency(shopCategory, player) / data.GetBuyPrice(player.UserIDString);

            if (amountcanbuy >= freeSlots)
            {
                return freeSlots * definition.stackable;
            }
            return (int)amountcanbuy * definition.stackable;
        }

        private object TryGive(BasePlayer player, ShopCategory shopCategory, string item, int amount)
        {
            ShopItem data = _config.ShopItems[item];
            //var slots = FreeSlots(player);
            //Puts($"{data.RunCommandAndCustomShopItem == false} / { (!(data.Command?.Any() ?? false))}");

            bool passed = false;

            if (data.RunCommandAndCustomShopItem && !data.Command.IsNullOrEmpty())
            {
                /*if (slots == 0)
                {
                    return Lang("InventoryFull", player.UserIDString);
                }*/

                List<Embed> embeds = new List<Embed>();

                Vector3 pos = GetLookPoint(player);

                Embed embed = BuildMessage(player);

                embed.AddTitle("Command & Item Bought 1 of 2");

                foreach (var command in data.Command)
                {
                    var c = command
                            .Replace("$player.id", player.UserIDString)
                            .Replace("$player.name", player.displayName)
                            .Replace("$player.x", pos.x.ToString())
                            .Replace("$player.y", pos.y.ToString())
                            .Replace("$player.z", pos.z.ToString());
                        
                        if (c.StartsWith("shop.show close", StringComparison.OrdinalIgnoreCase))
                            NextTick(() => ConsoleSystem.Run(ConsoleSystem.Option.Server, c));
                        else
                            ConsoleSystem.Run(ConsoleSystem.Option.Server, c);
                }

                var d = string.Join("\n", data.Command);
                embed.AddField("Command Name", d);
                embed.AddField("Cost", $"{amount * data.GetBuyPrice(player.UserIDString)}", true)
                    .AddField("Remaining Balance", $"{GetCurrency(shopCategory, player) - amount * data.GetBuyPrice(player.UserIDString)}", true);
                embeds.Add(embed);

                Embed embed2 = BuildMessage(player);

                if (!string.IsNullOrEmpty(data.Shortname))
                {
                    //bool space = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity - player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count < 1 && targetItem.stackable <= Amount;
                    if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())
                    {
                        //Puts($"called here");
                        return Lang("InventoryFull", player.UserIDString);
                    }

                    object success = GiveItem(player, data, amount, shopCategory);

                    if (success is string)
                    {
                        return success;
                    }
                }

                embed2.AddTitle("Item Given 2 of 2").AddField("Item Name", data.Shortname).AddField("amount", $"{amount}", true);
                    //.AddField("Cost", $"{amount * data.BuyPrice}", true)
                    //.AddField("Balance", $"{GetCurrency(shopCategory, player) - amount * data.BuyPrice}", true);

                    embeds.Add(embed2);
                    SendToDiscord(embeds);

                    passed = true;
            }

            if (!data.RunCommandAndCustomShopItem && !data.Command.IsNullOrEmpty()) //updated/patched sorta 5/13/2021
            {
                /*if (slots == 0)
                {
                    return Lang("InventoryFull", player.UserIDString);
                }*/

                Vector3 pos = GetLookPoint(player);

                Embed embed = BuildMessage(player);

                embed.AddTitle("Command Bought");
                
                foreach (var command in data.Command)
                {
                    //embed.AddTitle("Command Bought");
                    var c = command
                        .Replace("$player.id", player.UserIDString)
                        .Replace("$player.name", player.displayName)
                        .Replace("$player.x", pos.x.ToString())
                        .Replace("$player.y", pos.y.ToString())
                        .Replace("$player.z", pos.z.ToString());

                    if (c.StartsWith("shop.show close", StringComparison.OrdinalIgnoreCase))
                        NextTick(() => ConsoleSystem.Run(ConsoleSystem.Option.Server, c));
                    else
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, c);
                }

                var d = string.Join("\n", data.Command);
                embed.AddField("Command Name", d);
                embed.AddField("Cost", $"{amount * data.GetBuyPrice(player.UserIDString)}", true)
                    .AddField("Remaining Balance", $"{GetCurrency(shopCategory, player) - amount * data.GetBuyPrice(player.UserIDString)}", true);
                SendToDiscord(new List<Embed>{embed});

                passed = true;
            }

            if (!string.IsNullOrEmpty(data.KitName))
            {
                object isKit = Kits?.Call<bool>("isKit", data.KitName);

                Embed embed = BuildMessage(player);

                if (!(isKit is bool) || !(bool) isKit)
                {

                    embed.AddTitle("Kit Purchase Failed").AddField("Kit Name", data.KitName)
                        .AddDescription($"WARNING: This is not a valid kit name assigned to this shop item: {data.Shortname}");
                    SendToDiscord(new List<Embed> {embed});
                    return Lang("NotKit", player.UserIDString, data.DisplayName);
                }

                object successkit = Kits.Call<object>("GiveKit", player, data.KitName);

                if (!(successkit is bool && (bool) successkit || successkit == null))
                {
                    embed.AddTitle("Kit Purchase Failed").AddField("Kit Name", data.KitName)
                        .AddDescription($"WARNING: There was an error while giving this player this kit {data.Shortname}");
                    SendToDiscord(new List<Embed> {embed});
                    return Lang("RedeemKitFail", player.UserIDString);
                }

                embed.AddTitle("Kit Bought").AddField("Kit Name", data.KitName)
                    .AddField("Cost", $"{amount * data.GetBuyPrice(player.UserIDString)}", true)
                    .AddField("Balance", $"{GetCurrency(shopCategory, player) - amount * data.GetBuyPrice(player.UserIDString)}", true);
                SendToDiscord(new List<Embed> {embed});

                passed = true;
            }

            if (!string.IsNullOrEmpty(data.Shortname) && data.Command.IsNullOrEmpty())
            {
                Embed embed = BuildMessage(player);
                /*if (slots == 0)
                {
                    return Lang("InventoryFull", player.UserIDString);
                }*/
                
                object success = GiveItem(player, data, amount, shopCategory);

                if (success is string)
                {
                    return success;
                }

                embed.AddTitle("Item Bought").AddField("Item Name", data.Shortname).AddField("amount", $"{amount}", true)
                    .AddField("Cost", $"{amount * data.GetBuyPrice(player.UserIDString)}", true)
                    .AddField("Balance", $"{GetCurrency(shopCategory, player) - amount * data.GetBuyPrice(player.UserIDString)}", true);
                SendToDiscord(new List<Embed> {embed});

                passed = true;
            }

            if (!passed)
            {
                return false;
            }

            return true;
        }

        private object GiveItem(BasePlayer player, ShopItem data, int amount, ShopCategory shopCategory)
        {
            if (amount <= 0)
            {
                return Lang("NotNothingShopFail", player.UserIDString);
            }

            ItemDefinition definition = ItemManager.FindItemDefinition(data.Shortname);
            if (definition == null)
            {
                return Lang("ItemNoExist", player.UserIDString);
            }

            var stack = GetStacks(definition, amount);
            var stacks = stack.Count;

            if (!data.MakeBlueprint && data.Condition != 0 && definition.condition.enabled && data.Condition < definition.condition.max)
            {
                //Puts("true");
                stacks = amount;
            }

            var slots = FreeSlots(player);
            if (slots < stacks)
            {
                AddCurrency(shopCategory, player, (data.GetSellPrice(player.UserIDString) * amount), "economics");
                return Lang("InventorySlots", player.UserIDString, stacks);
            }

            string name = string.Empty;
            if (data.CraftAsDisplayName)
            {
                name = data.DisplayName;
            }

            var results = Enumerable.Repeat(definition.stackable, amount / definition.stackable)
                .Concat(Enumerable.Repeat(amount % definition.stackable, 1))
                .Where(x => x > 0);

            foreach (var value in results)
            {
                var item = CreateByName(data.MakeBlueprint ? "blueprintbase" : data.Shortname, value, name, data.Condition, data.SkinId);
                if (data.GeneTypes.Count == 6 && item.GetHeldEntity() is Planner)
                {
                    for (int slot = 0; slot < 6; slot++)
                    {
                        Genes.Genes[slot].Set(CharToGeneType(data.GeneTypes[slot]), true);
                    }
                    EncodeGenesToItem(Genes, item);
                }

                item.MarkDirty();

                if (data.MakeBlueprint)
                {
                    if (ItemManager.FindBlueprint(definition) == null) return Lang("ItemIsNotBlueprintable", player.UserIDString, data.DisplayName);

                    if (item.instanceData == null)
                    {
                        item.instanceData = new ProtoBuf.Item.InstanceData();
                    }

                    item.instanceData.ShouldPool = false;
                    item.instanceData.blueprintAmount = 1;
                    item.instanceData.blueprintTarget = definition.itemid;
                    item.MarkDirty();
                }

                if (!GiveItem(player, item))
                {
                    item.Remove();
                    item.DoRemove();
                    AddCurrency(shopCategory, player, (data.GetSellPrice(player.UserIDString) * value), "economics");
                }
            }

            return true;
        }

        #region Creation_Checks

        public GrowableGenes Genes = new GrowableGenes();
        
        GrowableGenetics.GeneType CharToGeneType(char h)
        {
            switch (h)
            {
                case 'g': return GrowableGenetics.GeneType.GrowthSpeed;
                case 'y': return GrowableGenetics.GeneType.Yield;
                case 'h': return GrowableGenetics.GeneType.Hardiness;
                case 'x': return GrowableGenetics.GeneType.Empty;
                case 'w': return GrowableGenetics.GeneType.WaterRequirement;
                default: return GrowableGenetics.GeneType.Empty;
            }
        }

        public void EncodeGenesToItem(GrowableGenes sourceGrowable, Item targetItem)
        {
            GrowableGeneEncoding.EncodeGenesToItem(GrowableGeneEncoding.EncodeGenesToInt(sourceGrowable), targetItem);
        }

        private static Item CreateByName(string strName, int iAmount, string name, float con, ulong skin = 0)
        {
            ItemDefinition itemDefinition = ItemManager.itemList.Find(x => x.shortname == strName);
            return itemDefinition == null ? (Item) null : CreateByItemID(itemDefinition.itemid, iAmount, name, con, skin);
        }

        private static Item CreateByItemID(int itemID, int iAmount, string name, float con, ulong skin = 0)
        {
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemID);
            return itemDefinition == null ? (Item) null : Create(itemDefinition, iAmount, name, con, skin);
        }

        private static Item Create(ItemDefinition template, int iAmount, string name, float con, ulong skin = 0)
        {
            TrySkinChangeItem(ref template, ref skin);
            if (template == null)
            {
                Debug.LogWarning( "Creating invalid/missing item!");
                return null;
            }
            Item obj = new Item();
            obj.isServer = true;
            if (iAmount <= 0)
            {
                Debug.LogError( "Creating item with less than 1 amount! (" + template.displayName.english + ")");
                return null;
            }
            obj.info = template;
            obj.amount = iAmount;
            obj.skin = skin;
            if (!name.IsNullOrEmpty())
            {
                obj.name = name;
            }
            obj.uid = new ItemId(Network.Net.sv.TakeUID());
            if (obj.hasCondition)
            {
                obj.maxCondition = obj.info.condition.max;
                obj.condition = con;
            }
            obj.OnItemCreated();
            return obj;
        }

        private static void TrySkinChangeItem(ref ItemDefinition template, ref ulong skinId)
        {
            if (skinId == 0UL) return;
            ItemSkinDirectory.Skin inventoryDefinitionId = ItemSkinDirectory.FindByInventoryDefinitionId((int) skinId);
            if (inventoryDefinitionId.id == 0) return;
            ItemSkin invItem = inventoryDefinitionId.invItem as ItemSkin;
            if (invItem == null || invItem.Redirect == null) return;
            template = invItem.Redirect;
            skinId = 0UL;
        }

        #endregion

        #region Give_Checks

        private static bool GiveItem(BasePlayer player, Item item, ItemContainer container = null)
        {
            if (item == null)
                return false;
            int position = -1;
            GetIdealPickupContainer(player, item, ref container, ref position);
            return container != null && item.MoveToContainer(container, position) ||
                   item.MoveToContainer(player.inventory.containerMain) ||
                   item.MoveToContainer(player.inventory.containerBelt) ||
                   item.DropAndTossUpwards(player.inventory.containerMain.dropPosition);
        }
        
        protected static void GetIdealPickupContainer(BasePlayer player, Item item, ref ItemContainer container, ref int position)
        {
            if (item.info.stackable > 1)
            {
                if (player.inventory.containerBelt != null && player.inventory.containerBelt.FindItemByItemID(item.info.itemid) != null)
                {
                    container = player.inventory.containerBelt;
                    return;
                }
                if (player.inventory.containerMain != null && player.inventory.containerMain.FindItemByItemID(item.info.itemid) != null)
                {
                    container = player.inventory.containerMain;
                    return;
                }
            }
            if (!item.info.isUsable || item.info.HasFlag(ItemDefinition.Flag.NotStraightToBelt)) return;
            container = player.inventory.containerBelt;
        }
        
        private int FreeSlots(BasePlayer player)
        {
            var slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
            var taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
            return slots - taken;
        }

        private List<int> GetStacks(ItemDefinition items, int amount)
        {
            var list = new List<int>();
            var maxStack = items.stackable;

            while (amount > maxStack)
            {
                amount -= maxStack;
                list.Add(maxStack);
            }

            list.Add(amount);

            return list;
        }

        #endregion

        #endregion

        #region Sell

        private object TryShopSell(BasePlayer player, string shopKey, string item, int amount)
        {
            object success = IsShop(player, shopKey);
            if (success is string)
            {
                return success;
            }

            success = CheckAction(player, shopKey, item, "Sell");
            if (success is string)
            {
                return success;
            }

            success = CanSell(player, item, amount);
            if (success is string)
            {
                return success;
            }

            // Puts($"tryshopsell amount is {amount}");
            success = TrySell(player, shopKey, item, amount);
            if (success is string)
            {
                return success;
            }

            ShopItem data = _config.ShopItems[item];
            double cooldown = Convert.ToDouble(data.SellCooldown);

            if (cooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;

                if (!_sellCoolDownData.TryGetValue(player.userID, out itemCooldowns))
                {
                    _sellCoolDownData[player.userID] = itemCooldowns = new Dictionary<string, double>();
                }

                itemCooldowns[item] = CurrentTime() + cooldown;
            }

            AddCurrency(_config.ShopCategories[shopKey], player, (data.GetSellPrice(player.UserIDString) * amount), item);

            Interface.Call("OnGUIShopSell", player.UserIDString, data.GetSellPrice(player.UserIDString), amount, data.Shortname);

            if (!string.IsNullOrEmpty(data.Shortname))
            {
                ulong count;

                _soldData.TryGetValue(data.Shortname, out count);

                _soldData[data.Shortname] = count + (ulong)amount;
            }

            return true;
        }

        private object TrySell(BasePlayer player, string shopKey, string item, int amount)
        {
            Embed embed = BuildMessage(player);
            ShopItem data = _config.ShopItems[item];

            if (_isEconomicsLimits)
            {
                if (_config.Economics && string.IsNullOrEmpty(_config.ShopCategories[shopKey]?.Currency) || _config.ShopCategories[shopKey]?.Currency.Contains("economics") == true)
                {
                    double balance = Economics.Call<double>("Balance", player.UserIDString);
                    double max = _balanceLimits.Value;

                    if (balance.Equals(max))
                    {
                        return Lang("MaxEconomicsBalance", player.UserIDString);
                    }

                    if (balance + (data.GetSellPrice(player.UserIDString) * amount) > max)
                    {
                        return Lang("EconomicsExceedsLimit", player.UserIDString, max);
                    }
                }
            }

            if (string.IsNullOrEmpty(data.Shortname))
            {
                return Lang("ShopItemItemInvalid", player.UserIDString);
            }

            if (!data.Command.IsNullOrEmpty())
            {
                return Lang("CantSellCommands", player.UserIDString);
            }

            object iskit = Kits?.CallHook("isKit", data.Shortname);

            if (iskit is bool && (bool) iskit)
            {
                return Lang("CantSellKits", player.UserIDString);
            }

            // Puts($"trysell amount is {amount}");
            object success = TakeItem(player, _config.ShopItems[item], _config.ShopCategories[shopKey], amount);
            if (success is string)
            {
                return success;
            }

            if (_config.EnableDiscordSellLogging)
            {
                embed.AddTitle("Item Sold").AddField("Item Name", data.Shortname).AddField("amount", $"{amount}", true)
                    .AddField("Receive", $"{amount * data.GetSellPrice(player.UserIDString)}", true)
                    .AddField("Balance", $"{GetCurrency(_config.ShopCategories[shopKey], player) + amount * data.GetSellPrice(player.UserIDString)}", true);
                SendToDiscord(new List<Embed>{embed});
            }

            return true;
        }

        private int GetAmountSell(BasePlayer player, string item)
        {
            ShopItem data = _config.ShopItems[item];
            ItemDefinition definition = ItemManager.FindItemDefinition(data.Shortname);

            if (definition == null)
            {
                return 0;
            }
            
            string name = String.Empty;
            if (data.CraftAsDisplayName)
            {
                name = data.DisplayName;
            }

            return GetAmount(player.inventory, _config.AllowedSellContainers, definition.itemid, name, data.AllowSellOfUsedItems, data.SkinId);
        }

        private object TakeItem(BasePlayer player, ShopItem data, ShopCategory shopCategory, int amount)
        {
            if (amount <= 0)
            {
                return Lang("NotEnoughSell", player.UserIDString);
            }

            ItemDefinition definition = ItemManager.FindItemDefinition(data.Shortname);

            if (definition == null)
            {
                return Lang("ItemNoExistTake", player.UserIDString);
            }

            string name = String.Empty;
            if (data.CraftAsDisplayName)
            {
                //Puts($"{data.DisplayName}");
                name = data.DisplayName;
            }
            int pamount = GetAmount(player.inventory, _config.AllowedSellContainers, definition.itemid, name, data.AllowSellOfUsedItems, data.SkinId);

            if (pamount < amount)
            {
                //Puts($"TakeItem pamount < amount true ? {pamount}, {amount}");
                return Lang("NotEnoughSell", player.UserIDString);
            }

            if (shopCategory?.Currency.Contains("custom") == true)
            {
                Item currency = ItemManager.CreateByItemID(shopCategory.CustomCurrencyIDs, amount, shopCategory.CustomCurrencySkinIDs);
                if (currency == null)
                {
                    return Lang("CustomCurrencyFail", player.UserIDString, shopCategory.DisplayName, shopCategory.CustomCurrencyIDs);
                }
                currency.Remove();
                currency.DoRemove();
            }

            //Puts($"{data.CraftAsDisplayName} {data.DisplayName.IsNullOrEmpty()} hmm?");
            if (data.CraftAsDisplayName && !string.IsNullOrEmpty(data.DisplayName))
            {
                Take(player.inventory, _config.AllowedSellContainers, null, definition.itemid, amount, data.AllowSellOfUsedItems, data.DisplayName, data.SkinId);
                return true;
            }

            Take(player.inventory, _config.AllowedSellContainers, null, definition.itemid, amount, data.AllowSellOfUsedItems, null, data.SkinId);
            return true;
        }

        private object CanSell(BasePlayer player, string item, int amount)
        {
            if (!_config.ShopItems.ContainsKey(item))
            {
                return Lang("ItemNotValidsell", player.UserIDString);
            }

            ShopItem itemdata = _config.ShopItems[item];

            if (itemdata.Command.IsNullOrEmpty() && player.inventory.containerMain.FindItemsByItemName(itemdata.Shortname) == null && player.inventory.containerBelt.FindItemsByItemName(itemdata.Shortname) == null) //fixed..
            {
                //Puts($"Is inside CanSell dunno why.. {itemdata.Shortname}, {amount}"); //Fixed
                return Lang("NotEnoughSell", player.UserIDString);
            }

            if (itemdata.GetSellPrice(player.UserIDString) < 0)
            {
                return Lang("SellPriceFail", player.UserIDString);
            }

            if (itemdata.SellCooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;

                double itemCooldown;

                if (_sellCoolDownData.TryGetValue(player.userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime())
                {
                    return Lang("Cooldown", player.UserIDString, FormatTime((long) (itemCooldown - CurrentTime())));
                }
            }

            ItemLimit itemLimit = GetPlayerLimit(player);
            if (itemdata.SellLimit > 0 && itemLimit.SLimit[itemdata.Shortname] >= itemdata.SellLimit)
            {
                //Puts($"{itemdata.DisplayName}, {player.displayName}, {itemLimit.CheckSellLimit(itemdata.Shortname, amount)}");
                if (itemdata.SellLimitResetCoolDown > 0)
                {
                    Dictionary<string, double> itemCooldowns;
                    double itemCooldown;
                    if (_sellLimitResetCoolDownData.TryGetValue(player.userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime())
                    {
                        return Lang( $"Sell Limit of {itemdata.SellLimit} was Reached for {itemdata.DisplayName} \n This limit resets in {FormatTime((long) (itemCooldown - CurrentTime()))}", player.UserIDString);
                    }
                }
                else
                    return Lang($"Sell Limit of {itemdata.SellLimit} Reached for {itemdata.DisplayName}", player.UserIDString);
            }

            return true;
        }

        #endregion

        #region Chat

        public string StripTags(string input) => Regex.Replace(input, "<.*?>", String.Empty);
        private void CmdGUIShop(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Use))
            {
                SendReply(player, Lang("NoPermUse", player.UserIDString, Use));
                return;
            }

            if (_imageLibraryCheck >= _config.ImageLibraryCounter)
            {
                SendReply(player, Lang("ImageLibraryFailure", player.UserIDString, _config.ImageLibraryCounter));
                return;
            }

            if (!_isShopReady || LangAPI != null && !_isLangAPIReady)
            {
                SendReply(player, Lang("GUIShopResponse", player.UserIDString));
                return;
            }

            if (_wipeReady == false)
            {
                float timeresult = _config.Time.CanShopIn / 60;
                SendReply(player, Lang("WipeReady", player.UserIDString, player.displayName, Math.Round(timeresult)));
                return;
            }

            if (!_config.ShopCategories.ContainsKey(_config.DefaultShop) && !string.IsNullOrWhiteSpace(_config.DefaultShop))
            {
                StringBuilder sb = new StringBuilder();
                foreach (var name in _config.ShopCategories)
                {
                    if (name.Key == name.Value.DisplayName)
                        sb.Append($"<color=#32CD32>{name.Key}</color>\n");
                }
                PrintError($"This is not a shop {_config.DefaultShop} update default shop setting!\nAvailable Shops are:\n{StripTags(sb.ToString())}");
                player.ChatMessage(Lang("ShopInvalid", player.UserIDString, _config.DefaultShop, sb));
               return;
            }

            ShopCategory shop = null;
            _config.ShopCategories.TryGetValue(_config.DefaultShop, out shop);

            string shopKey = shop?.DisplayName;
            ShopCategory category;
            if (GetNearestVendor(player, out category))
            {
                shopKey = category.DisplayName;
            }
            if (category == null && String.IsNullOrEmpty(_config.DefaultShop))
            {
                SendReply(player, Lang("GlobalShopsDisabled", player.UserIDString));
                return;
            }

            if (category == null && shop.EnabledCategory == false || !string.IsNullOrEmpty(shop.Permission) && !permission.UserHasPermission(player.UserIDString, shop.PrefixPermission))
            {
                shopKey = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnabledCategory && (string.IsNullOrEmpty(i.Permission) || !string.IsNullOrEmpty(i.Permission) && permission.UserHasPermission(player.UserIDString, i.PrefixPermission)))?.DisplayName;
            }

            if (shopKey != null && !string.IsNullOrEmpty(_config.ShopCategories[shopKey].Permission) && !permission.UserHasPermission(player.UserIDString, _config.ShopCategories[shopKey].PrefixPermission))
            {
                SendReply(player, Lang("NoPerm", player.UserIDString, shopKey));
                return;
            }

            if (category == null && (string.IsNullOrEmpty(shopKey) || !_config.ShopCategories[shopKey].EnabledCategory))
            {
                SendReply(player, Lang("GlobalShopsDisabled", player.UserIDString));
                return;
            }

            if (!player.CanBuild())
            {
                if (permission.UserHasPermission(player.UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowGUIShops(player, shopKey);
                else
                    SendReply(player, Lang("BuildingBlocked", player.UserIDString));

                return;
            }

            GetPlayerData(player).ShopKey = shopKey;

            _imageChanger = _config.UIImageOption;
            playerGUIShopUIOpen.Add(player.UserIDString);
            ShowGUIShops(player, shopKey);
        }

        [ChatCommand("cleardata")]
        private void CmdGUIShopClearData(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, Admin))
            {
                _playerUIData.Clear();
                Puts($"{player.userID} has cleared the data in the GUI Shop file");
            }
        }

        [ChatCommand("shopgui")]
        private void CmdGUIShopToggleBackpackGUI(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Button))
                return;

            if (!_playerDisabledButtonData.Remove(player.UserIDString))
            {
                _playerDisabledButtonData.Add(player.UserIDString);
            }
            CreateGUIShopButton(player);

            SendReply(player, "{0} GUIShop Button", _playerDisabledButtonData.Contains(player.UserIDString) ? "Disabled" : "Enabled", this, player.UserIDString);
            SaveData();
        }

        [ChatCommand("update")]
        private void CmdGUIShopUpdate(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Admin))
            {
                return;
            }
            
            if (_imageLibraryCheck >= _config.ImageLibraryCounter)
            {
                SendReply(player, Lang("ImageLibraryFailure", player.UserIDString, _config.ImageLibraryCounter));
                return;
            }

            if (!_isShopReady || LangAPI != null && !_isLangAPIReady)
            {
                SendReply(player, Lang("GUIShopResponse", player.UserIDString));
                return;
            }

            foreach (var item in _config.ShopItems.Values)
            {
                if (item.Condition == 0 && item.Shortname != null)
                {
                    ItemDefinition update = ItemManager.FindItemDefinition(item.Shortname);
                    if (update != null && update.condition.enabled) 
                        item.Condition = update.condition.max;
                }
            }

            SaveConfig();
            SendReply(player, $"{player.displayName} has manually updated the GUIShop config.");
            Server.Command("o.reload GUIShop");
            Puts($"Command update was manually ran by {player.UserIDString}, {player.displayName}");
        }

        [ChatCommand("updateold")]
        private void Update(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Admin))
            {
                return;
            }

            foreach (var item in _config.ShopItems.Values)
            {
                if (!item.Image.IsNullOrEmpty() && item.Image.Equals("https://rustlabs.com/img/items180/" + item.Shortname + ".png"))
                {
                    item.Image = String.Empty;
                }
            }

            SaveConfig();
            SendReply(player, $"{player.displayName} has manually updated the GUIShop config.");
            Server.Command("o.reload GUIShop");
            Puts($"Command update was manually ran by {player.UserIDString}");
        }

        [ChatCommand("resetconditions")]
        private void CmdGUIShopResetConditions(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Admin))
            {
                return;
            }
            
            if (_imageLibraryCheck >= _config.ImageLibraryCounter)
            {
                SendReply(player, Lang("ImageLibraryFailure", player.UserIDString, _config.ImageLibraryCounter));
                return;
            }
            
            if (!_isShopReady || LangAPI != null && !_isLangAPIReady)
            {
                SendReply(player, Lang("GUIShopResponse", player.UserIDString));
                return;
            }

            foreach (var item in _config.ShopItems.Values)
            {
                ItemDefinition update = ItemManager.FindItemDefinition(item.Shortname);
                if (update != null && update.condition.enabled) 
                    item.Condition = update.condition.max;
            }

            SaveConfig();
            SendReply(player, $"{player.displayName} has manually reset item conditions for the GUIShop config.");
            //Server.Command("o.reload GUIShop");
            Puts($"Command resetconditions was manually ran by {player.UserIDString}, {player.displayName}");
        }

        #endregion

        #region Console

        [ConsoleCommand("limits.clear")]
        private void ConsoleGUIShopClearLimit(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin)) return;
            _limitsData.Clear();
            _buyLimitResetCoolDownData.Clear();
            _sellLimitResetCoolDownData.Clear();
        }

        [ConsoleCommand("transactions.clear")]
        private void ConsoleGUIShopClearTransactions(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, Admin)) return;
            _buyCooldownData.Clear();
            _sellCoolDownData.Clear();
        }

        [ConsoleCommand("shop.button")]
        private void ConsoleGUIShopButton(ConsoleSystem.Arg arg)
        {
            CmdGUIShop(arg.Player(), null, null);
        }

        [ConsoleCommand("shop.show")] //updated to fix spacing issues in name again.
        private void ConsoleGUIShopShow(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2))
            {
                return;
            }

            BasePlayer player = arg.Player();

            string shopid = arg.GetString(0).Replace("_", " ");

            if (shopid.Equals("close", StringComparison.OrdinalIgnoreCase))
            {
                BasePlayer targetPlayer = arg.GetPlayerOrSleeper(1);

                DestroyUi(targetPlayer, true);
                
                GetPlayerData(targetPlayer).ShopKey = String.Empty;

                _shopPage.Remove(targetPlayer.userID);
            }
            else
            {
                if (player == null || !permission.UserHasPermission(player.UserIDString, Use))
                {
                    return;
                }

                GetPlayerData(player).ShopKey = shopid;

                ShowGUIShops(player, shopid, arg.GetInt(1), false, true);   
            }
        }

        [ConsoleCommand("shop.buy")]
        private void ConsoleGUIShopBuy(ConsoleSystem.Arg arg, ShopCategory shopCategory)
        {
            if (!arg.HasArgs(3))
            {
                return;
            }

            BasePlayer player = arg.Player();

            if (player == null || !permission.UserHasPermission(player.UserIDString, Use))
            {
                return;
            }

            object success = Interface.Oxide.CallHook("canShop", player);

            // TODO: re-code
            if (success != null)
            {
                SendReply(player, success as string ?? "You are not allowed to shop at the moment");
                return;
            }

            string shopName = arg.GetString(0).Replace("_", " ");
            string item = arg.GetString(1).Replace("_", " ");
            bool isAll = arg.GetString(2).Equals("all") && !string.IsNullOrEmpty(GetPlayerData(player).ShopKey);
            int amount = arg.GetString(2).Equals("all") ? !string.IsNullOrEmpty(GetPlayerData(player).ShopKey) ? GetAmountBuy(player, _config.ShopCategories[shopName], item) : 0 : arg.GetInt(2);
            if (amount == 0)
            {
                if (!string.IsNullOrEmpty(GetPlayerData(player).ShopKey))
                    SendReply(player, Lang("InventoryFull", player.UserIDString));
                else
                    SendReply(player, Lang("BuyCmd", player.UserIDString));
                return;
            }
            //int amount = arg.Args[2].Equals("all") ? GetAmountBuy(player, item) : Convert.ToInt32(arg.Args[2]);

            ShopItem shopitem = _config.ShopItems.Values.FirstOrDefault(x => x.DisplayName == item);
            if (shopitem == null) return;

            ItemLimit itemLimit = GetPlayerLimit(player);
            itemLimit.CheckBuyLimit(shopitem.Shortname, shopitem.BuyLimit);

            double limitcooldown = Convert.ToDouble(shopitem.BuyLimitResetCoolDown);
            if (shopitem.BuyLimitResetCoolDown > 0 && itemLimit.BLimit[shopitem.Shortname] >= shopitem.BuyLimit)
            {
                Dictionary<string, double> itemCooldowns;
                if (!_buyLimitResetCoolDownData.TryGetValue(player.userID, out itemCooldowns))
                    _buyLimitResetCoolDownData[player.userID] = itemCooldowns = new Dictionary<string, double>();
                itemCooldowns[shopitem.DisplayName] = CurrentTime() + limitcooldown;
                itemLimit.BLimit[shopitem.Shortname] = 0;
            }

            success = TryShopBuy(player, shopName, item, amount, isAll);

            //Puts($"inside buy command {TryShopBuy(player, shopName, item, amount)}");

            //Puts($"{success}");
            /*
            if (success is bool && (bool)success == false)
            {
                SendReply(player, Lang("InventoryFull", player.UserIDString));
                return;
            }
            */

            if (success is string)
            {
                SendReply(player, (string) success);
                return;
            }

            if (isAll && newamounts != 0)
            {
                amount = newamounts;
            }

            if (shopitem.BuyLimit > 0)
            {
                itemLimit.IncrementBuy(shopitem.Shortname, amount, shopitem.SwapLimitToQuantityBuyLimit);
            }

            SendReply(player, Lang("Bought", player.UserIDString), amount, GetItemDisplayName(shopitem.Shortname, shopitem.DisplayName, player.UserIDString));

            if (!string.IsNullOrEmpty(GetPlayerData(player).ShopKey))
                ShowGUIShops(player, shopName, _shopPage[player.userID], false, true);

            newamounts = 0;
        }

        private string GetItemDisplayName(string shorname, string displayName, string userID)
        {
            if (LangAPI != null && LangAPI.Call<bool>("IsDefaultDisplayName", displayName))
            {
                return LangAPI.Call<string>("GetItemDisplayName", shorname, displayName, userID) ?? displayName;
            }
            return displayName;
        }

        [ConsoleCommand("shop.sell")]
        private void ConsoleGUIShopSell(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(3))
            {
                return;
            }

            BasePlayer player = arg.Player();

            if (player == null || !permission.UserHasPermission(player.UserIDString, Use))
            {
                return;
            }

            object success = Interface.Oxide.CallHook("canShop", player);

            if (success != null)
            {
                string message = "You are not allowed to shop at the moment";
                if (success is string)
                {
                    message = (string) success;
                }

                SendReply(player, message);
                return;
            }

            string shopName = arg.GetString(0).Replace("_", " ");
            string item = arg.GetString(1).Replace("_", " ");
            int amount = arg.GetString(2).Equals("all") ? !string.IsNullOrEmpty(GetPlayerData(player).ShopKey) ? GetAmountSell(player, item) : -1 : arg.GetInt(2);

            if (amount == -1)
            {
                SendReply(player, Lang("SellCmd", player.UserIDString));
                return;
            }

            ShopItem shopitem = _config.ShopItems.Values.FirstOrDefault(x => x.DisplayName == item);
            if (shopitem == null) return;

            ItemLimit itemLimit = GetPlayerLimit(player);
            itemLimit.CheckSellLimit(shopitem.Shortname, shopitem.SellLimit);
            double limitcooldown2 = Convert.ToDouble(shopitem.SellLimitResetCoolDown);
            if (shopitem.SellLimitResetCoolDown > 0 && itemLimit.SLimit[shopitem.Shortname] >= shopitem.SellLimit)
            {
                Dictionary<string, double> itemCooldowns2;
                if (!_sellLimitResetCoolDownData.TryGetValue(player.userID, out itemCooldowns2))
                    _sellLimitResetCoolDownData[player.userID] = itemCooldowns2 = new Dictionary<string, double>();
                itemCooldowns2[shopitem.DisplayName] = CurrentTime() + limitcooldown2;
                itemLimit.SLimit[shopitem.Shortname] = 0;
            }

            success = TryShopSell(player, shopName, item, amount);

            if (success is string)
            {
                SendReply(player, (string) success);
                return;
            }

            if (shopitem.SellLimit > 0)
            {
                itemLimit.IncrementSell(shopitem.Shortname, amount, shopitem.SwapLimitToQuantitySoldLimit);
            }
            //string broadcast = $"<size=10><color=orange>{player.displayName}</color> sold {amount}x {shopitem.DisplayName} for {(int)(shopitem.SellPrice * amount)} RP!</size>";
            //Server.Broadcast(broadcast);
            SendReply(player, Lang("Sold", player.UserIDString), amount, GetItemDisplayName(shopitem.Shortname, shopitem.DisplayName, player.UserIDString));

            if (!string.IsNullOrEmpty(GetPlayerData(player).ShopKey))
                ShowGUIShops(player, shopName, _shopPage[player.userID], false, true);
        }

        // Fixed ShopID issues when using inside NPC shops ( current bug with global shops ) requires re-implementation Fixed Since 2.4.4x
        [ConsoleCommand("shop.transparency")]
        private void ConsoleGUIShopTransparency(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!permission.UserHasPermission(player.UserIDString, Use))
            {
                return;
            }

            PlayerTransparencyChange(player, arg.Args[0]);

            string cat = GetPlayerData(player).ShopKey;

            if (!player.CanBuild())
            {
                if (permission.UserHasPermission(player.UserIDString, BlockAllow)) //Override.
                    ShowGUIShops(player, cat);
                else
                    SendReply(player, Lang("BuildingBlocked", player.UserIDString));
                return;
            }

            ShowGUIShops(player, cat);
        }

        [ConsoleCommand("shop.uicolor")]
        private void ConsoleGUIShopUIColor(ConsoleSystem.Arg arg)
        {
            if (arg.Args[0] == null || arg.Args[1] == null)
            {
                return;
            }

            PlayerColorTextChange(arg.Player(), arg.Args[0], arg.Args[1], arg.Args[2], _uiSettingChange);
            if (!permission.UserHasPermission(arg.Player().UserIDString, Use)) //added vip option.
            {
                return;
            }

            string cat = GetPlayerData(arg.Player()).ShopKey; // arg.Args[3].Replace("_", " ")

            if (!arg.Player().CanBuild())
            {
                if (permission.UserHasPermission(arg.Player().UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowGUIShops(arg.Player(),cat);
                else
                    SendReply(arg.Player(), Lang("BuildingBlocked", arg.Player().UserIDString));
                return;
            }

            ShowGUIShops(arg.Player(), cat);
        }

        [ConsoleCommand("shop.colorsetting")]
        private void ConsoleGUIShopUIColorSetting(ConsoleSystem.Arg arg)
        {
            //Puts("{0}", arg.GetString(1));
            if (!arg.HasArgs(2))
                return;

            _uiSettingChange = arg.Args[0];
            if (!permission.UserHasPermission(arg.Player().UserIDString, Use)) //added vip
                return;

            string cat = GetPlayerData(arg.Player()).ShopKey; //arg.Args[1].Replace("_", " ")

            if (!arg.Player().CanBuild())
            {
                if (permission.UserHasPermission(arg.Player().UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowGUIShops(arg.Player(), cat);
                else
                    SendReply(arg.Player(), Lang("BuildingBlocked", arg.Player().UserIDString));
                return;
            }

            GetSettingTypeToChange(_uiSettingChange);
            
            ShowGUIShops(arg.Player(), cat);
        }

        [ConsoleCommand("shop.imageortext")]
        private void ConsoleGUIShopUIImageOrText(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
            {
                return;
            }
            
            string cat = GetPlayerData(arg.Player()).ShopKey; //arg.Args[0].Replace("_", " ")

            if (_config.PersonalUI && !permission.UserHasPermission(arg.Player().UserIDString, Color))
            {
                ShowGUIShops(arg.Player(), cat);
                return;
            }

            SetImageOrText(arg.Player());

            if (!arg.Player().CanBuild())
            {
                if (permission.UserHasPermission(arg.Player().UserIDString, BlockAllow)) //Overrides being blocked.
                    ShowGUIShops(arg.Player(), _config.DefaultShop);
                else
                    SendReply(arg.Player(), Lang("BuildingBlocked", arg.Player().UserIDString));
                return;
            }

            ShowGUIShops(arg.Player(), cat);
        }

        [ConsoleCommand(("guishop.reset"))] /* Added to permanently assure zero config resets! */
        private void ConsoleGUIShopReset(ConsoleSystem.Arg arg)
        {
            if (!(arg.IsRcon || arg.IsAdmin)) return;
            PrintWarning("Loading GUIShop Defaults");
            LoadDefaultConfig();
            CheckConfig();
            SaveConfig();
            LibraryCheck();
        }

        #endregion

        #region CoolDowns

        private int CurrentTime() => Facepunch.Math.Epoch.Current;

        private bool HasBuyCooldown(ulong userID, string item, out double itemCooldown)
        {
            Dictionary<string, double> itemCooldowns;

            itemCooldown = 0.0;

            return _buyCooldownData.TryGetValue(userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime();
        }

        private bool HasSellCooldown(ulong userID, string item, out double itemCooldown)
        {
            Dictionary<string, double> itemCooldowns;

            itemCooldown = 0.0;

            return _sellCoolDownData.TryGetValue(userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime();
        }

        private bool HasBuyLimitCoolDown(ulong userID, string item, out double itemCooldown)
        {
            Dictionary<string, double> itemCooldowns;

            itemCooldown = 0.0;

            return _buyLimitResetCoolDownData.TryGetValue(userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime();
        }
        
        private bool HasSellLimitCoolDown(ulong userID, string item, out double itemCooldown)
        {
            Dictionary<string, double> itemCooldowns;

            itemCooldown = 0.0;

            return _sellLimitResetCoolDownData.TryGetValue(userID, out itemCooldowns) && itemCooldowns.TryGetValue(item, out itemCooldown) && itemCooldown > CurrentTime();
        }

        private string FormatTime(long seconds)
        {
            TimeSpan timespan = TimeSpan.FromSeconds(seconds);

            return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds,
                Math.Floor(timespan.TotalHours));
        }

        #endregion

        #region UI Colors

        private string GetSettingTypeToChange(string type)
        {
            return type == _uiSettingChange ? $"{HexToColor("#FFFFFF")} 0.2" : $"{HexToColor("#CCD1D1")} 0";
        }

        private void SetImageOrText(BasePlayer player)
        {
            PlayerUISetting playerUISetting = GetPlayerData(player);

            if (permission.UserHasPermission(player.UserIDString, Color))
            {
                playerUISetting.ImageOrText = !playerUISetting.ImageOrText;
                return;
            }

            if (_config.PersonalUI)
            {
                playerUISetting.ImageOrText = !playerUISetting.ImageOrText;
            }
        }

        private bool GetImageOrText(BasePlayer player)
        {
            PlayerUISetting playerUISetting = GetPlayerData(player);
            if (!_config.PersonalUI && !permission.UserHasPermission(player.UserIDString, Color))
            {
                playerUISetting.ImageOrText = false;
            }

            if (_config.UIImageOption && !permission.UserHasPermission(player.UserIDString, Color))
            {
                //Puts("UI Image option + no Color perm");
                playerUISetting.ImageOrText = true;
            }

            if (_config.PersonalUI && _config.UIImageOption && !permission.UserHasPermission(player.UserIDString, Color))
            {
                //Puts("personal UI + UI Image Option + Color permission");
                playerUISetting.ImageOrText = true;
            }

            _imageChanger = playerUISetting.ImageOrText;
            return _imageChanger;
        }

        private string GetText(string text, string type, BasePlayer player)
        {
            if (GetImageOrText(player))
            {
                switch (type)
                {
                    case "label":
                        return "";
                    case "image":
                        return text;
                }
            }
            else
            {
                switch (type)
                {
                    case "label":
                        return text;
                    case "image":
                        return "https://i.imgur.com/fL7N8Zf.png";
                }
            }

            return "";
        }

        private double AnchorBarMath(BasePlayer uiPlayer) => (GetUITransparency(uiPlayer) / 10 - (GetUITransparency(uiPlayer) / 10 - GetPlayerData(uiPlayer).RangeValue / 1000)) * 10;

        private PlayerUISetting GetPlayerData(BasePlayer player)
        {
            PlayerUISetting playerUISetting;

            if (!_playerUIData.TryGetValue(player.UserIDString, out playerUISetting))
            {
                _playerUIData[player.UserIDString] = playerUISetting = new PlayerUISetting
                {
                    Transparency = Transparency,
                    
                    SellBoxColors = $"{HexToColor(_config.SellColor)} 0.15",
                    BuyBoxColors = $"{HexToColor(_config.BuyColor)} 0.15",
                    RangeValue = (Transparency - 0.9) * 100,
                    ImageOrText = _config.UIImageOption,
                };
            }

            return playerUISetting;
        }

        private void PlayerTransparencyChange(BasePlayer uiPlayer, string action)
        {
            PlayerUISetting playerUISetting = GetPlayerData(uiPlayer);

            switch (action)
            {
                case "increase":
                    if (Math.Abs(playerUISetting.Transparency - 1) >= 1)
                    {
                        break;
                    }

                    playerUISetting.Transparency = playerUISetting.Transparency + 0.01;
                    playerUISetting.RangeValue = playerUISetting.RangeValue + 1;
                    break;
                case "decrease":
                    if (Math.Abs(playerUISetting.Transparency - 0.01) <= 0.9)
                    {
                        break;
                    }

                    playerUISetting.Transparency = playerUISetting.Transparency - 0.01;
                    playerUISetting.RangeValue = playerUISetting.RangeValue - 1;
                    break;
            }
        }

        private double GetUITransparency(BasePlayer uiPlayer) => GetPlayerData(uiPlayer).Transparency;

        private void PlayerColorTextChange(BasePlayer uiPlayer, string textColorRed, string textColorGreen, string textColorBlue, string uiSettingToChange)
        {
            PlayerUISetting playerUISetting = GetPlayerData(uiPlayer);

            switch (uiSettingToChange)
            {
                case "Text":
                    playerUISetting.UITextColor = $"{textColorRed} {textColorGreen} {textColorBlue} 1";
                    break;
                case "Buy":
                    playerUISetting.BuyBoxColors = $"{textColorRed} {textColorGreen} {textColorBlue} {GetUITransparency(uiPlayer) - 0.75}";
                    break;
                case "Sell":
                    playerUISetting.SellBoxColors = $"{textColorRed} {textColorGreen} {textColorBlue} {GetUITransparency(uiPlayer) - 0.75}";
                    break;
            }
        }

        private string GetUITextColor(BasePlayer uiPlayer, string defaultHex) => GetPlayerData(uiPlayer).UITextColor ?? $"{HexToColor(defaultHex)} 1";

        private string GetUISellBoxColor(BasePlayer uiPlayer) => GetPlayerData(uiPlayer).SellBoxColors;

        private string GetUIBuyBoxColor(BasePlayer uiPlayer) => GetPlayerData(uiPlayer).BuyBoxColors;

        private string HexToColor(string hexString)
        {
            if (hexString.IndexOf('#') != -1) hexString = hexString.Replace("#", "");

            int b = 0;
            int r = 0;
            int g = 0;

            if (hexString.Length == 6)
            {
                r = int.Parse(hexString.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                g = int.Parse(hexString.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                b = int.Parse(hexString.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            }

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255}";
        }

        #endregion

        #region Limits

        private ItemLimit GetPlayerLimit(BasePlayer player)
        {
            if (!player.userID.IsSteamId()) 
                return null;
            
            ItemLimit itemdata;

            if (!_limitsData.TryGetValue(player.userID, out itemdata))
                _limitsData[player.userID] = itemdata = new ItemLimit();

            return itemdata;
        }

        #endregion

        #region NPC

        private bool NearNpc(string npcId, BasePlayer player)
        {
            BasePlayer npc = GetNPC(npcId);

            if (npc == null)
            {
                return false;
            }
            object success = Interface.Call("isInTriggerHumanNpc", npc, player);
            if (success is bool) return (bool) success;
            
            return Vector3Ex.Distance2D(npc.ServerPosition, player.transform.position) <= _config.NPCDistanceCheck;
        }
        private BasePlayer GetNPC(string npcId)
        {
            foreach (BasePlayer npcPlayer in BaseNetworkable.serverEntities.OfType<BasePlayer>())
            {
                if (npcPlayer.UserIDString == npcId)
                {
                    return npcPlayer;
                }
            }

            return null;
        }

        bool GetNearestVendor(BasePlayer player, out ShopCategory shopCategory) //NPC helper reverted.
        {
            shopCategory = null;

            Collider[] colliders = Physics.OverlapSphere(player.ServerPosition, _config.NPCDistanceCheck, playersMask);
            
            if (!colliders.Any()) return false;

            BasePlayer npc = colliders.Select(col => col.GetComponent<BasePlayer>()).FirstOrDefault(x => !IsPlayer(x.userID));

            if (npc == null) return false;
            
            shopCategory = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NpcIds.Contains(npc.UserIDString) && (!string.IsNullOrEmpty(i.Permission) && permission.UserHasPermission(player.UserIDString, i.PrefixPermission) || string.IsNullOrEmpty(i.Permission)));

            if (shopCategory == null) return false;

            return true;
        }

        bool IsPlayer(ulong userID) => userID.IsSteamId();

        private void OnUseNPC(BasePlayer npc, BasePlayer player) //added 1.8.7 //updated 2.2.48
        {
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, Use))
            {
                SendReply(player, Lang("NoPermUse", player.UserIDString, Use));
                return;
            }

            if (_imageLibraryCheck >= _config.ImageLibraryCounter)
            {
                SendReply(player, Lang("ImageLibraryFailure", player.UserIDString, _config.ImageLibraryCounter));
                return;
            }

            if (!_isShopReady || LangAPI != null && !_isLangAPIReady)
            {
                SendReply(player, Lang("GUIShopResponse", player.UserIDString));
                return;
            }

            if (_wipeReady == false)
            {
                float timeresult = _config.Time.CanShopIn / 60;
                SendReply(player, Lang("WipeReady", player.UserIDString, player.displayName, Math.Round(timeresult)));
                return;
            }

            float distance = Vector3.Distance(npc.ServerPosition, player.ServerPosition);
            if (distance > _config.NPCDistanceCheck) return;

            ShopCategory category = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NpcIds.Contains(npc.UserIDString) && (!string.IsNullOrEmpty(i.Permission) && permission.UserHasPermission(player.UserIDString, i.PrefixPermission) || string.IsNullOrEmpty(i.Permission)));

            if (category == null)
            {
                category =  _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NpcIds.Contains(npc.UserIDString) && !string.IsNullOrEmpty(i.Permission) && !permission.UserHasPermission(player.UserIDString, i.PrefixPermission));
                if (category == null)
                    return;
                SendReply(player, Lang("NoPerm", player.UserIDString, category.DisplayName));
                return;
            }

            GetPlayerData(player).ShopKey = category.DisplayName;
            ShowGUIShops(player, category.DisplayName);
        }

        private void OnEnterNPC(BasePlayer npc, BasePlayer player) //added 2.0.0
        {
            //Puts($"npc id is {npc.UserIDString}, {player.UserIDString}");
            if (player == null || !player.userID.IsSteamId() || _config.NPCAutoOpen == false) return;

            if (!permission.UserHasPermission(player.UserIDString, Use))
            {
                SendReply(player, Lang("NoPermUse", player.UserIDString, Use));
                return;
            }

            if (_imageLibraryCheck >= _config.ImageLibraryCounter)
            {
                SendReply(player, Lang("ImageLibraryFailure", player.UserIDString, _config.ImageLibraryCounter));
                return;
            }

            if (!_isShopReady || LangAPI != null && !_isLangAPIReady)
            {
                SendReply(player, Lang("GUIShopResponse", player.UserIDString));
                return;
            }

            if (_wipeReady == false)
            {
                float timeresult = _config.Time.CanShopIn / 60;
                SendReply(player,Lang("WipeReady", player.UserIDString, player.displayName, Math.Round(timeresult)));
                return;
            }

            float distance = Vector3.Distance(npc.ServerPosition, player.ServerPosition);
            if (distance > _config.NPCDistanceCheck) return;
            ShopCategory category = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NpcIds.Contains(npc.UserIDString) && (!string.IsNullOrEmpty(i.Permission) && permission.UserHasPermission(player.UserIDString, i.PrefixPermission) || string.IsNullOrEmpty(i.Permission)));

            if (category == null)
            {
                category =  _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NpcIds.Contains(npc.UserIDString) && !string.IsNullOrEmpty(i.Permission) && !permission.UserHasPermission(player.UserIDString, i.PrefixPermission));
                if (category == null)
                    return;
                SendReply(player, Lang("NoPerm", player.UserIDString, category.DisplayName));
                return;
            }

            //Puts($"on enter npc cat is {category.DisplayName}, ");
            if (_config.NPCLeaveResponse) //added 2.0.0
            {
                SendReply(player, Lang("NPCResponseOpen", player.UserIDString), category.DisplayName);
            }

            GetPlayerData(player).ShopKey = category.DisplayName;
            ShowGUIShops(player, category.DisplayName);
        }

        private void OnLeaveNPC(BasePlayer npc, BasePlayer player) //added 1.8.7
        {
            if (player == null || !player.userID.IsSteamId() || !permission.UserHasPermission(player.UserIDString, Use)) return;

            float distance = Vector3.Distance(npc.ServerPosition, player.ServerPosition);
            if (distance <= _config.NPCDistanceCheck) return;

            ShopCategory category = _config.ShopCategories.Select(x => x.Value).FirstOrDefault(i => i.EnableNPC && i.NpcIds.Contains(npc.UserIDString) && (!string.IsNullOrEmpty(i.Permission) && permission.UserHasPermission(player.UserIDString, i.PrefixPermission) || string.IsNullOrEmpty(i.Permission)));
            if (category == null) return;

            CloseShop(player);
            GetPlayerData(player).ShopKey = String.Empty;

            if (_config.NPCLeaveResponse) //added 1.8.8
            {
                SendReply(player, Lang("NPCResponseClose", player.UserIDString), category.DisplayName);
            }
        }

        #endregion

        #region Helpers

        private Vector3 GetLookPoint(BasePlayer player)
        {
            RaycastHit raycastHit;
            if (!Physics.Raycast(new Ray(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward), out raycastHit, _config.SpawnDistance))
            {
                return player.ServerPosition;
            }

            return raycastHit.point;
        }

        private bool IsNearMonument(BasePlayer player)
        {
            foreach (var monumentInfo in _monuments)
            {
                float distance = Vector3Ex.Distance2D(monumentInfo.transform.position, player.ServerPosition);

                if (monumentInfo.name.Contains("sphere") && distance < 30f)
                {
                    return true;
                }

                if (monumentInfo.name.Contains("launch") && distance < 30f)
                {
                    return true;
                }

                if (!monumentInfo.IsInBounds(player.ServerPosition)) continue;

                return true;
            }

            return false;
        }

        #endregion

        #region API Hooks

        private double GetCurrency(BasePlayer player, string shopKey)
        {
            ShopCategory shop;
            if (!_config.ShopCategories.TryGetValue(shopKey, out shop))
            {
                return 0;
            }

            return GetCurrency(shop, player);
        }

        private void OpenShop(BasePlayer player, string shopKey, string npcID)
        {
            ShopCategory shop;
            if (!_config.ShopCategories.TryGetValue(shopKey, out shop) || !shop.EnableNPC || !shop.NpcIds.Contains(npcID))
            {
                return;
            }

            GetPlayerData(player).ShopKey = shopKey;
            ShowGUIShops(player, shopKey);
        }

        private void CloseShop(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            GetPlayerData(player).ShopKey = String.Empty;
            CuiHelper.DestroyUi(player, GUIShopOverlayName);
        }

        #endregion

    }
}