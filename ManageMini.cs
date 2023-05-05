using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System.Security;
using System.CodeDom.Compiler;

namespace Oxide.Plugins
{
    [Info("Manage Mini", "DMB7", "0.3.4"), Description("Manage, customize and own your minicopter!")]
    class ManageMini : RustPlugin
    {
        #region  Global Variables
        private MiniData miniData;
        private ConfigFile configFile;
        private List<string> miniChatCommands;
        private System.Random rand = new System.Random();
        private int batteryMaxDrain = 25;

        #region Permissions
        // Chat Commands
        private string miniHelpPerm = "ManageMini.miniHelp";
        private string makeMiniPerm = "ManageMini.makeMini";
        private string takeMiniPerm = "ManageMini.takeMini";
        private string getMiniPerm = "ManageMini.getMini";
        private string ownMiniPerm = "ManageMini.ownMini";
        private string authPilotPerm = "ManageMini.authPilot";
        private string unauthPilotPerm = "ManageMini.unauthPilot";
        private string miniDetailsPerm = "ManageMini.miniDetails";
        private string upgradeMiniPerm = "ManageMini.upgradeMini";
        private string upgradeTurretPerm = "ManageMini.upgradeMiniTurret";

        // Fuel Usage Rate
        private string unlimitedFuel = "ManageMini.UnlimitedFuel";
        private string randomFuelRate = "ManageMini.RandomFuelRate";

        #endregion

        #endregion

        #region Hooks
        private void Init()
        {
            // Global Variable Initialization
            configFile = Config.ReadObject<ConfigFile>();
            miniChatCommands = new List<string>();
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("ManageMini"))
            {
                Interface.Oxide.DataFileSystem.GetDatafile("ManageMini").Save();
            }
            miniData = Interface.Oxide.DataFileSystem.ReadObject<MiniData>("ManageMini");

            // Register Permissions            
            registerPerms();

            // Set Chat Commands for the help system
            setChatHelp();
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ManageMini", miniData);
        }

        void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ManageMini", miniData);
        }

        void onEntityKill(MiniCopter mini)
        {
            if (mini != null)
            {
                if (!mini.OwnerID.ToString().Equals("0"))
                {
                    miniData.ownerMini.Remove(mini.OwnerID);
                    chatMessage(getPlayer(mini.OwnerID), "Mini_DestroyedNow", null);
                }
            }
        }

        object CanMountEntity(BasePlayer player, BaseMountable mini)
        {
            if (player != null && mini != null)
            {

                if (mini.ShortPrefabName.ToString().Equals("minihelipassenger"))
                {
                    return null;
                }
                else if (mini.ShortPrefabName.ToString().Equals("miniheliseat"))
                {
                    BasePlayer owner = getPlayer(mini.VehicleParent().OwnerID);
                    if (owner == null && !miniData.ownerMini.ContainsKey(owner.userID))
                    {
                        return null;
                    }
                    else if (miniData.ownerMini[owner.userID].getAuthPilots().Contains(player.userID.ToString()))
                    {
                        fixUpgradeMini(owner);
                        if (miniData.ownerMini[owner.userID].hasAutoTurret()
                             && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as IOEntity) != null)
                        {
                            disableWire(owner);
                            setTurretRange(owner, 600);
                        }
                        if (miniData.ownerMini[owner.userID].hasElectricEngine()
                             && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 4))) as IOEntity) != null)
                        {
                            disableWire(owner);
                            setFuelRate(getMinicopter(owner), miniData.ownerMini[owner.userID].getFuelPerSec());
                            setFuelAmount(getMinicopter(owner) as BaseVehicle, getTotalPower(getBatteries(owner)));
                        }
                        if (miniData.ownerMini[owner.userID].hasBatteries()
                             && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 2))) as IOEntity) != null
                              && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 3))) as IOEntity) != null)
                        {
                            disableWire(owner);
                        }
                        resetFuelRate(owner, miniData.ownerMini[owner.userID].getFuelPerSec());
                        return null;
                    }
                    else
                    {
                        string[] obj = { player.displayName, owner.displayName };
                        chatMessageArray(player, "Unauth_PilotAttempt", obj);
                        chatMessage(owner, "Owner_AlertMini", player.displayName);
                        return false;
                    }
                }
                else
                {
                    return null;
                }

            }
            else
            {
                return null;
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player != null)
            {
                if (miniData.ownerMini.ContainsKey(player.userID))
                {

                    if (miniData.ownerMini[player.userID].hasElectricEngine())
                    {
                        resetFuelRate(player, miniData.ownerMini[player.userID].getFuelPerSec());
                        getMinicopter(player).fuelGaugeMax = 450f;
                    }
                    else if (checkPermissions(player, unlimitedFuel))
                    {
                        resetFuelRate(player, configFile.fuelPerSecUnlimited);
                    }
                    else if (checkPermissions(player, randomFuelRate))
                    {
                        resetFuelRate(player, (float)Math.Round((0.15 + rand.NextDouble() * 0.50), 2));
                    }
                    else
                    {
                        resetFuelRate(player, configFile.fuelPerSec);
                    }
                    resetBatteriesDrain(player);
                }
            }
        }

        void OnEntityDismounted(BaseMountable mini, BasePlayer player)
        {
            if (player != null && mini != null)
            {
                if (mini.ShortPrefabName.ToString().Equals("miniheliseat"))
                {
                    BasePlayer owner = getPlayer(mini.VehicleParent().OwnerID);
                    if (owner == null)
                    {
                        return;
                    }
                    if (miniData.ownerMini.ContainsKey(owner.userID))
                    {
                        if (miniData.ownerMini[owner.userID].hasElectricEngine())
                        {
                            setTotalPower(owner, getFuelAmount(mini.VehicleParent() as BaseVehicle));
                        }
                    }
                }

            }
        }
        #endregion

        #region Base ManageMini Chat Commands
        [ChatCommand("minihelp")]
        private void miniHelp(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            else
            {
                string message = lang.GetMessage("ManageMini_Help", this, player.UserIDString) + "\n";
                foreach (var chatCommand in miniChatCommands)
                {
                    message = message + "\n" + chatCommand + lang.GetMessage(chatCommand, this, player.UserIDString);
                }
                chatMessage(player, message, null);
            }
        }

        [ChatCommand("makemini")]
        private void makeMini(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            else if (!checkPermissions(player, makeMiniPerm))
            {
                chatMessage(player, "PermissionCheck_failed", player.displayName);
            }
            else if (miniData.ownerMini.ContainsKey(player.userID))
            {
                chatMessage(player, "Already_CreatedMini", player.displayName);
            }
            else if (checkWaitList(player))
            {
                // checkWaitList() handles the output to player
            }
            else if (!miniData.ownerMini.ContainsKey(player.userID))
            {
                createMini(player);
                chatMessage(player, "Make_Mini", player.displayName);
            }
            else
            {
                miniData.errorLog.Add(player.userID, "make mini has failed for unknown reason");
                chatMessage(player, "Error_ManageMini", null);
            }
        }

        [ChatCommand("takemini")]
        private void takeMini(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            else if (!checkPermissions(player, takeMiniPerm))
            {
                chatMessage(player, "PermissionCheck_failed", player.displayName);
            }
            else if (!miniData.ownerMini.ContainsKey(player.userID))
            {
                chatMessage(player, "mini_NotFound", player.displayName);
            }
            else if (miniData.ownerMini.ContainsKey(player.userID))
            {
                takeBackMini(player);
            }
            else
            {
                miniData.errorLog.Add(player.userID, "takemini has failed for unknown reason");
                chatMessage(player, "Error_ManageMini", null);
            }
        }

        [ChatCommand("getmini")]
        private void getMini(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            else if (!checkPermissions(player, getMiniPerm))
            {
                chatMessage(player, "PermissionCheck_failed", player.displayName);
            }
            else if (!miniData.ownerMini.ContainsKey(player.userID))
            {
                chatMessage(player, "mini_NotFound", player.displayName);
            }
            else if (miniData.ownerMini.ContainsKey(player.userID))
            {
                if (setMiniLocation(player))
                {
                    chatMessage(player, "Get_Mini", null);
                }
                else
                {
                    chatMessage(player, "Error_ManageMini", null);
                }
            }
            else
            {
                miniData.errorLog.Add(player.userID, "getMini has failed for unknown reason");
                chatMessage(player, "Error_ManageMini", null);
            }
        }

        [ChatCommand("ownMini")]
        private void ownMini(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            else if (!checkPermissions(player, ownMiniPerm))
            {
                chatMessage(player, "PermissionCheck_failed", player.displayName);
            }
            else if (miniData.ownerMini.ContainsKey(player.userID))
            {
                chatMessage(player, "Already_CreatedMini", player.displayName);
            }
            else if (!player.isMounted)
            {
                chatMessage(player, "Player_NotSeatedMini", null);
            }
            else if (player.isMounted)
            {
                MiniCopter mini = player.GetMountedVehicle() as MiniCopter;
                if (mini == null)
                {
                    chatMessage(player, "Player_NotSeatedMini", null);
                }
                else if (mini.ShortPrefabName == "minicopter.entity")
                {
                    if (mini.OwnerID.ToString() == "0" || mini.OwnerID == player.userID)
                    {
                        mini.OwnerID = player.userID;
                        takeOwnershipMini(player, mini);
                        chatMessage(player, "Took_OwnershipMini", player.displayName);
                    }
                    else
                    {
                        chatMessage(player, "FailedTake_OwnershipMini", getPlayer(mini.OwnerID).displayName);
                    }
                }
                else
                {
                    chatMessage(player, "Player_NotSeatedMini", null);
                }
            }
            else
            {
                miniData.errorLog.Add(player.userID, "getMini has failed for unknown reason");
                chatMessage(player, "Error_ManageMini", null);
            }


        }

        [ChatCommand("authPilot")]
        private void authPilot(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            else if (!checkPermissions(player, makeMiniPerm))
            {
                chatMessage(player, "PermissionCheck_failed", player.displayName);
            }
            else if (!miniData.ownerMini.ContainsKey(player.userID))
            {
                chatMessage(player, "mini_NotFound", player.displayName);
            }
            else if (miniData.ownerMini.ContainsKey(player.userID))
            {
                if (args.Length == 0)
                {
                    chatMessage(player, "No_Args", "/authPilot <playerName>");
                }
                else
                {
                    if (addAuthPilot(player, args[0]))
                    {
                        chatMessage(player, "Auth_Pilot", (getPlayer(args[0]).displayName));
                    }
                    else
                    {
                        chatMessage(player, "No_PlayerFound", args[0]);
                    }
                }
            }
            else
            {
                miniData.errorLog.Add(player.userID, "authPilot has failed for unknown reason");
                chatMessage(player, "Error_ManageMini", null);
            }
        }

        [ChatCommand("unauthPilot")]
        private void unauthPilot(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            else if (!checkPermissions(player, makeMiniPerm))
            {
                chatMessage(player, "PermissionCheck_failed", player.displayName);
            }
            else if (!miniData.ownerMini.ContainsKey(player.userID))
            {
                chatMessage(player, "mini_NotFound", player.displayName);
            }
            else if (miniData.ownerMini.ContainsKey(player.userID))
            {
                if (args.Length == 0)
                {
                    chatMessage(player, "No_Args", "/authPilot <playerName");
                }
                else
                {
                    if (removeAuthPilot(player, args[0]))
                    {
                        chatMessage(player, "Unauth_Pilot", getPlayer(args[0]).displayName);
                    }
                    else
                    {
                        chatMessage(player, "No_PlayerFound", args[0]);
                    }
                }
            }
            else
            {
                miniData.errorLog.Add(player.userID, "unauthPilot has failed for unknown reason");
                chatMessage(player, "Error_ManageMini", null);
            }
        }

        [ChatCommand("miniDetails")]
        private void miniDetails(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            else if (!checkPermissions(player, makeMiniPerm))
            {
                chatMessage(player, "PermissionCheck_failed", player.displayName);
            }
            else if (!miniData.ownerMini.ContainsKey(player.userID))
            {
                chatMessage(player, "mini_NotFound", player.displayName);
            }
            else if (miniData.ownerMini.ContainsKey(player.userID))
            {
                string message = chatAuthPilotList(player);
                message = message + "\n\n" + string.Format(lang.GetMessage("Mini_FuelRate", this, player.UserIDString), getMinicopter(player).fuelPerSec.ToString());
                message = message + "\n\n" + chatMinicopterAge(player);
                chatMessage(player, null, message);
            }
            else
            {
                miniData.errorLog.Add(player.userID, "miniDetails has failed for unknown reason");
                chatMessage(player, "Error_ManageMini", null);
            }
        }

        #endregion

        #region Upgrade Mini Chat Commands
        [ChatCommand("upgradeMini")]
        private void upgradeMini(BasePlayer player, string command, string[] args)
        {
            string message =
                "/upgradeMini batteries add \n /upgradeMini batteries remove" +
                "\n\n /upgradeMini eFuel add \n /upgradeMini eFuel remove" +
                "\n\n /upgradeMini turret add \n /upgradeMini turret on \n /upgradeMini turret off \n /upgrademini turret remove" +
            "";
            if (player == null) { return; }
            else if (!checkPermissions(player, upgradeMiniPerm))
            {
                chatMessage(player, "PermissionCheck_failed", player.displayName);
            }
            else if (args.Length < 1 || args.Length > 2)
            {
                chatMessage(player, "No_Args", message);
            }
            else if (!miniData.ownerMini.ContainsKey(player.userID))
            {
                chatMessage(player, "mini_NotFound", player.displayName);
            }
            else if (miniData.ownerMini.ContainsKey(player.userID))
            {
                if (args[0].Equals("batteries", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (args.Length == 1)
                    {
                        chatMessage(player, "No_Args", message);
                    }
                    else if (args[1].Equals("add", StringComparison.InvariantCultureIgnoreCase))
                    {
                        addBatteriesMini(player, getMinicopter(player) as BaseVehicle);
                        chatMessage(player, "Upgrade_Mini", player.displayName);
                    }
                    else if (args[1].Equals("remove", StringComparison.InvariantCultureIgnoreCase)
                        && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 2))) as IOEntity) != null
                         && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 3))) as IOEntity) != null)
                    {
                        setMiniRotation(getMinicopter(player) as BaseVehicle);
                        removeBatteriesMini(player);
                    }
                    else
                    {
                        chatMessage(player, "No_Args", message);
                    }

                }
                else if (args[0].Equals("eFuel", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (args.Length == 1)
                    {
                        chatMessage(player, "No_Args", message);
                    }
                    else if (args[1].Equals("add", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!miniData.ownerMini[player.userID].hasBatteries())
                        {
                            chatMessage(player, "Batteries_NotIncluded", player.displayName);
                        }
                        else
                        {
                            upgradeElectricMini(player, getMinicopter(player) as BaseVehicle);
                            chatMessage(player, "Upgrade_Mini", player.displayName);
                            if (miniData.ownerMini[player.userID].hasAutoTurret())
                            {
                                setFuelRate(getMinicopter(player), 0.20f);
                            }
                            else
                            {
                                setFuelRate(getMinicopter(player), 0.10f);
                            }
                        }
                    }
                    else if (args[1].Equals("remove", StringComparison.InvariantCultureIgnoreCase)
                        && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 4))) as IOEntity) != null)
                    {
                        setMiniRotation(getMinicopter(player) as BaseVehicle);
                        setFuelRate(getMinicopter(player), 0.25f);
                        removeElectricMini(player);
                    }
                    else
                    {
                        chatMessage(player, "No_Args", message);
                    }

                }
                else if (args[0].Equals("turret", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!checkPermissions(player, upgradeTurretPerm))
                    {
                        chatMessage(player, "PermissionCheck_failed", player.displayName);
                    }
                    else if (args.Length == 1)
                    {
                        chatMessage(player, "No_Args", message);
                    }
                    else if (args[1].Equals("add", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!miniData.ownerMini[player.userID].hasBatteries())
                        {
                            chatMessage(player, "Batteries_NotIncluded", player.displayName);
                        }
                        else
                        {
                            removeTurretMini(player);
                            setMiniData(player, addTurretMini(getMinicopter(player) as BaseVehicle), 5);
                            setWireScheme(player);
                            setTurretRange(player, 50);
                            chatMessage(player, "Upgrade_Mini", player.displayName);
                            if (miniData.ownerMini[player.userID].hasAutoTurret())
                            {
                                setFuelRate(getMinicopter(player), (float)(getMinicopter(player).fuelPerSec + 0.1f));
                            }
                        }
                    }
                    else if (args[1].Equals("on", StringComparison.InvariantCultureIgnoreCase)
                        && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as IOEntity) != null)
                    {
                        if (miniData.ownerMini[player.userID].hasElectricEngine())
                        {

                            setWire((BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 4))) as IOEntity), 0,
                                (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as IOEntity), 0);
                        }
                        else
                        {
                            setWire((BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 3))) as IOEntity), 0,
                                 (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as IOEntity), 0);
                        }
                        (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as AutoTurret).InitiateStartup();
                    }
                    else if (args[1].Equals("off", StringComparison.InvariantCultureIgnoreCase)
                        && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as IOEntity) != null)
                    {
                        (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as AutoTurret).InitiateShutdown();
                        (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as IOEntity).ClearConnections();
                    }
                    else if (args[1].Equals("remove", StringComparison.InvariantCultureIgnoreCase)
                        && (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as IOEntity) != null)
                    {
                        setMiniRotation(getMinicopter(player) as BaseVehicle);
                        removeTurretMini(player);
                        if (miniData.ownerMini[player.userID].hasElectricEngine())
                        {
                            setFuelRate(getMinicopter(player), (float)(getMinicopter(player).fuelPerSec - 0.1f));
                        }
                    }
                    else
                    {
                        chatMessage(player, "No_Args", message);
                    }
                }
                else
                {
                    chatMessage(player, "No_Args", message);
                }
            }
            else
            {
                miniData.errorLog.Add(player.userID, "upgrademini has failed for unknown reason");
                chatMessage(player, "Error_ManageMini", null);
            }
        }


        #endregion

        #region Base ManageMini Functions
        private void registerPerms()
        {
            // Permission Registration
            permission.RegisterPermission(miniHelpPerm, this);
            permission.RegisterPermission(makeMiniPerm, this);
            permission.RegisterPermission(takeMiniPerm, this);
            permission.RegisterPermission(getMiniPerm, this);
            permission.RegisterPermission(authPilotPerm, this);
            permission.RegisterPermission(ownMiniPerm, this);
            permission.RegisterPermission(unauthPilotPerm, this);
            permission.RegisterPermission(miniDetailsPerm, this);
            permission.RegisterPermission(upgradeTurretPerm, this);
            permission.RegisterPermission(upgradeMiniPerm, this);

            // Fuel Rate Permission Registration
            permission.RegisterPermission(unlimitedFuel, this);
            permission.RegisterPermission(randomFuelRate, this);

        }

        private void registerWaitTimes()
        {
            foreach (var perm in configFile.waitTimes)
            {
                permission.RegisterPermission(perm.Key, this);
            }
        }

        private bool checkPermissions(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        private void setChatHelp()
        {
            miniChatCommands.Add("/miniHelp");
            miniChatCommands.Add("/makeMini");
            miniChatCommands.Add("/takeMini");
            miniChatCommands.Add("/getMini");
            miniChatCommands.Add("/ownMini");
            miniChatCommands.Add("/authPilot");
            miniChatCommands.Add("/unauthPilot");
            miniChatCommands.Add("/miniDetails");
            miniChatCommands.Add("/upgrademini");
        }

        private void createMini(BasePlayer player)
        {
            Vector3 location = new Vector3(player.transform.position.x + (float)(rand.NextDouble() * configFile.miniSpawnDistance), player.transform.position.y + 2f, player.transform.position.z + (float)(rand.NextDouble() * configFile.miniSpawnDistance));
            BaseVehicle mini = (BaseVehicle)GameManager.server.CreateEntity(configFile.assetPrefab, location, new Quaternion());
            if (mini == null)
            {
                miniData.errorLog.Add(player.userID, "create mini has failed to create entity");
                chatMessage(player, "Error_ManageMini", null);
                return;
            }
            mini.OwnerID = player.userID;
            mini.health = configFile.miniStartHealth;
            mini.Spawn();

            if (checkPermissions(player, unlimitedFuel))
            {

                StorageContainer fuelTank = mini.GetFuelSystem().GetFuelContainer();
                ItemManager.CreateByItemID(-946369541, configFile.fuelAmountUnlimited)?.MoveToContainer(fuelTank.inventory);
                fuelTank.SetFlag(BaseEntity.Flags.Locked, true);
                chatMessage(player, "No_FuelRate", null);
                setFuelRate(mini as MiniCopter, configFile.fuelPerSecUnlimited);
            }
            else if (checkPermissions(player, randomFuelRate))
            {
                setFuelRate(mini as MiniCopter, (float)Math.Round((0.10 + rand.NextDouble() * 0.40), 2));
            }
            else
            {
                setFuelRate(mini as MiniCopter, configFile.fuelPerSec);
            }

            takeOwnershipMini(player, mini as MiniCopter);
        }

        private void setFuelRate(MiniCopter mini, float fuelPerSec)
        {
            if (mini != null)
            {
                mini.fuelPerSec = fuelPerSec;
            }
        }

        private void resetFuelRate(BasePlayer player, float fuelPerSec)
        {
            MiniCopter mini = getMinicopter(player);
            if (mini != null)
            {
                setFuelRate(mini, fuelPerSec);
            }
        }

        private void takeOwnershipMini(BasePlayer player, MiniCopter mini)
        {
            OwnerMiniData ownerMiniData = new OwnerMiniData();
            ownerMiniData.setMiniEntityID(mini.net.ID.Value);
            ownerMiniData.setFuelPerSec(mini.fuelPerSec);
            ownerMiniData.setManufactureData(DateTime.Now);
            ownerMiniData.setAuthPilots(new List<string> { player.userID.ToString() });
            miniData.ownerMini.Add(player.userID, ownerMiniData);
        }

        private bool checkWaitList(BasePlayer player)
        {
            if (miniData.ownerMini.ContainsKey(player.userID))
            {
                DateTime miniCreatedAt = miniData.ownerMini[player.userID].getManufactureData();

                float timeToWait = 0.0f;

                foreach (var time in configFile.waitTimes)
                {
                    if (checkPermissions(player, time.Key))
                    {
                        timeToWait = time.Value;
                        break;
                    }
                }

                double diff = (miniCreatedAt.AddSeconds(timeToWait) - DateTime.Now).TotalSeconds;
                if (diff > 0)
                {
                    string[] obj = { player.displayName, Math.Round(diff / 60.0, 2).ToString() };
                    chatMessageArray(player, "Mini_WaitList", obj);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private MiniCopter getMinicopter(BasePlayer player)
        {
            if (miniData.ownerMini.ContainsKey(player.userID))
            {
                return BaseNetworkable.serverEntities.Find(new NetworkableId(miniData.ownerMini[player.userID].getMiniEntityID())) as MiniCopter;
            }
            else
            {
                return null;
            }
        }

        private bool setMiniLocation(BasePlayer player)
        {
            Vector3 dest = new Vector3(player.transform.position.x + 2f, player.transform.position.y + 1.0f, player.transform.position.z + 2f);
            MiniCopter mini = getMinicopter(player);
            if (mini != null)
            {
                mini.transform.position = dest;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void takeBackMini(BasePlayer player)
        {
            // miniData.ownerMini.containsKey(player.userID) verified in chat command region
            BaseNetworkable.serverEntities.Find(new NetworkableId(miniData.ownerMini[player.userID].getMiniEntityID()))?.Kill();
            miniData.ownerMini.Remove(player.userID);
            chatMessage(player, "Take_Mini", null);
        }

        #endregion

        #region get player ulong/string
        private BasePlayer getPlayer(ulong id)
        {
            BasePlayer owner;
            if (BasePlayer.FindByID(id) == null)
            { owner = BasePlayer.FindSleeping(id); }
            else if (BasePlayer.FindSleeping(id) == null)
            { owner = BasePlayer.FindByID(id); }
            else
            { return null; }
            return owner;
        }

        private BasePlayer getPlayer(string name)
        {
            BasePlayer player;
            if (BasePlayer.Find(name) == null)
            { player = BasePlayer.FindSleeping(name); }
            else if (BasePlayer.FindSleeping(name) == null)
            { player = BasePlayer.Find(name); }
            else
            { return null; }
            return player;
        }
        #endregion

        #region chat messages to player
        private void chatMessageArray(BasePlayer player, string msgType, string[] obj)
        {
            if (obj == null)
            {
                player.ChatMessage(lang.GetMessage(msgType, this, player.UserIDString));
            }
            else
            {
                player.ChatMessage(String.Format(lang.GetMessage(msgType, this, player.UserIDString), obj));
            }
        }

        private void chatMessage(BasePlayer player, string msgType, string message)
        {
            string[] obj = { message };
            if (obj == null && msgType != null)
            {
                player.ChatMessage(lang.GetMessage(msgType, this, player.UserIDString));
            }
            else if (msgType == null)
            {
                player.ChatMessage(message);
            }
            else
            {
                player.ChatMessage(String.Format(lang.GetMessage(msgType, this, player.UserIDString), obj));
            }

        }
        private string chatAuthPilotList(BasePlayer player)
        {
            string message = lang.GetMessage("Auth_PilotHeader", this, player.UserIDString);
            List<string> authPilots = getPilotsList(player);
            foreach (var pilot in authPilots)
            {
                message = message + "\n" + getPlayer(pilot).displayName;
            }
            return message;
        }

        private string chatMinicopterAge(BasePlayer player)
        {
            if (miniData.ownerMini.ContainsKey(player.userID))
            {
                DateTime miniCreatedAt = miniData.ownerMini[player.userID].getManufactureData();
                return string.Format(lang.GetMessage("MiniCopter_AgeDHMS", this, player.UserIDString), getTimeDHMS((int)Math.Truncate((DateTime.Now - miniCreatedAt).TotalSeconds)));
            }
            else
            {
                return "";
            }
        }

        private string[] getTimeDHMS(double var)
        {
            double totalDays = Math.Truncate(var / 86400.0);
            var = var - totalDays * 86400;
            double extraHours = Math.Truncate(var / 3600.0);
            var = var - extraHours * 3600;
            double extraMinutes = Math.Truncate(var / 60.0);
            var = var - extraMinutes * 60;
            double extraSeconds = var;

            string[] message = { totalDays.ToString(), extraHours.ToString(), extraMinutes.ToString(), extraSeconds.ToString() };
            return message;
        }
        #endregion

        #region Data Management Functions

        #region Set / Get Entity IDs 
        private void setMiniData(BasePlayer player, ulong num, int index)
        {
            if (!miniData.ownerMini.ContainsKey(player.userID)) { return; }

            OwnerMiniData ownerMiniData = miniData.ownerMini[player.userID];
            // Entity ID storage
            if (index == 0) { ownerMiniData.setMiniEntityID(num); }
            else if (index == 1) { ownerMiniData.setBranchID(num); }
            else if (index == 2) { ownerMiniData.setBatteryLeftID(num); }
            else if (index == 3) { ownerMiniData.setBatteryRightID(num); }
            else if (index == 4) { ownerMiniData.setBatteryEngineID(num); }
            else if (index == 5) { ownerMiniData.setAutoTurretID(num); }
            else { return; }
            miniData.ownerMini[player.userID] = ownerMiniData;
        }

        private ulong getMiniData(BasePlayer player, int index)
        {
            if (!miniData.ownerMini.ContainsKey(player.userID)) { return 0; }

            OwnerMiniData ownerMiniData = miniData.ownerMini[player.userID];
            // Entity ID retrieval
            if (index == 0) { return ownerMiniData.getMiniEntityID(); }
            else if (index == 1) { return ownerMiniData.getBranchID(); }
            else if (index == 2) { return ownerMiniData.getBatteryLeftID(); }
            else if (index == 3) { return ownerMiniData.getBatteryRightID(); }
            else if (index == 4) { return ownerMiniData.getBatteryEngineID(); }
            else if (index == 5) { return ownerMiniData.getAutoTurretID(); }
            else { return 0; }
        }
        #endregion

        #region Authorized Pilot Data Get/ Add/ Remove
        private List<string> getPilotsList(BasePlayer player)
        {
            if (!miniData.ownerMini.ContainsKey(player.userID)) { return null; }
            else { return miniData.ownerMini[player.userID].getAuthPilots(); }
        }

        private bool addAuthPilot(BasePlayer player, string name)
        {
            BasePlayer pilot = getPlayer(name);
            if (!miniData.ownerMini.ContainsKey(player.userID) || (pilot == null)) { return false; }
            else
            {
                OwnerMiniData ownerMiniData = miniData.ownerMini[player.userID];
                List<string> authPilots = ownerMiniData.getAuthPilots();
                if (authPilots.Contains(pilot.userID.ToString())) { return true; }
                authPilots.Add(pilot.userID.ToString());
                ownerMiniData.setAuthPilots(authPilots);
                miniData.ownerMini[player.userID] = ownerMiniData;
                return true;
            }
        }

        private bool removeAuthPilot(BasePlayer player, string name)
        {
            BasePlayer pilot = getPlayer(name);
            if (!miniData.ownerMini.ContainsKey(player.userID) || (pilot == null)) { return false; }
            else
            {
                OwnerMiniData ownerMiniData = miniData.ownerMini[player.userID];
                List<string> authPilots = ownerMiniData.getAuthPilots();
                if (!authPilots.Contains(pilot.userID.ToString())) { return false; }
                authPilots.Remove(pilot.userID.ToString());
                ownerMiniData.setAuthPilots(authPilots);
                miniData.ownerMini[player.userID] = ownerMiniData;
                return true;
            }
        }
        #endregion

        #region  Fuel Rate Storage
        private bool setFuelPerSec(BasePlayer player, float rate)
        {
            if (!miniData.ownerMini.ContainsKey(player.userID)) { return false; }
            else
            {
                OwnerMiniData ownerMiniData = miniData.ownerMini[player.userID];
                ownerMiniData.setFuelPerSec(rate);
                miniData.ownerMini[player.userID] = ownerMiniData;
                return true;
            }
        }
        #endregion

        #endregion

        #region Upgrade Mini Functions
        // TODO: research, plan and implement code to allow player to add more upgrades to their minicopter
        private void upgradeElectricMini(BasePlayer player, BaseVehicle mini)
        {
            if (mini == null)
            {
                return;
            }
            removeElectricMini(player);
            setMiniRotation(mini);
            setMiniData(player, addBatteries(mini, -0.05f, 1.3f, -0.35f, 175f, 180f, 0), 4);
            ElectricBattery bat = (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 4))) as ElectricBattery);
            if (bat != null)
            {
                setFuelAmount(mini, getTotalPower(getBatteries(player)));
                lockFuelTank(mini.GetFuelSystem().GetFuelContainer(), true);
                bat.maxOutput = (batteryMaxDrain - 10);
                bat.rustWattSeconds = (float)(150 * 60.0);
                setWireScheme(player);
                (mini as MiniCopter).fuelGaugeMax = 450f;
            }
        }

        private void removeElectricMini(BasePlayer player)
        {
            if (miniData.ownerMini.ContainsKey(player.userID))
            {
                BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 4)))?.Kill();
                setMiniData(player, 0, 4);
                setWireScheme(player);

                if (checkPermissions(player, unlimitedFuel))
                {
                    setFuelAmount(getMinicopter(player) as BaseVehicle, 100);
                    setFuelRate(getMinicopter(player), configFile.fuelPerSecUnlimited);
                    lockFuelTank((getMinicopter(player) as BaseVehicle).GetFuelSystem().GetFuelContainer(), true);
                }
                else
                {
                    setFuelAmount(getMinicopter(player) as BaseVehicle, 0);
                    lockFuelTank((getMinicopter(player) as BaseVehicle).GetFuelSystem().GetFuelContainer(), false);
                }
                getMinicopter(player).fuelGaugeMax = 100f;
            }
        }

        private void addBatteriesMini(BasePlayer player, BaseVehicle mini)
        {
            if (mini == null)
            {
                return;
            }
            removeBatteriesMini(player);
            setMiniRotation(mini);
            setMiniData(player, setElectricInput(mini, -0.75f, 0.43f, -0.75f, 0, 180f, 180f), 1);
            setMiniData(player, addBatteries(mini, -0.7f, 0.62f, -0.55f, 180f, 180f, 0), 2);
            setMiniData(player, addBatteries(mini, 0.7f, 0.62f, -0.55f, 180f, 0, 0), 3);
            ElectricBattery bat = (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 3))) as ElectricBattery);
            if (bat != null)
            {
                bat.maxOutput = (batteryMaxDrain - 5);
                bat.rustWattSeconds = (float)(75 * 60.0);
            }
            setWireScheme(player);
        }

        private void removeBatteriesMini(BasePlayer player)
        {
            ulong num = 0;
            for (int i = 1; i < 4; i++)
            {
                num = getMiniData(player, i);
                if (num != 0)
                {
                    setMiniData(player, 0, i);
                    removeUpgrade(num);
                }
            }
            if (miniData.ownerMini[player.userID].hasElectricEngine())
            {
                removeElectricMini(player);
            }
            setWireScheme(player);
        }

        private void removeTurretMini(BasePlayer player)
        {
            if (miniData.ownerMini.ContainsKey(player.userID))
            {
                ulong num = getMiniData(player, 5);
                if (num == 0)
                {
                    return;
                }
                removeUpgrade(num);
                setMiniData(player, 0, 5);
                setWireScheme(player);
            }
        }

        private void removeUpgrade(ulong num)
        {
            if (BaseNetworkable.serverEntities.Find(new NetworkableId(num)) != null)
            {
                BaseNetworkable.serverEntities.Find(new NetworkableId(num))?.Kill();
            }
        }

        private ulong addBatteries(BaseVehicle mini, float pos_x, float pos_y, float pos_z, float rot_x, float rot_y, float rot_z)
        {
            if (mini == null)
            {
                return 0;
            }
            string prefab = "assets/prefabs/deployable/playerioents/batteries/smallrechargablebattery.deployed.prefab";
            return setItemUpgrade<ElectricBattery>(prefab, mini, mini.transform.position, pos_x, pos_y, pos_z, rot_x, rot_y, rot_z);
        }

        private ulong setElectricInput(BaseVehicle mini, float pos_x, float pos_y, float pos_z, float rot_x, float rot_y, float rot_z)
        {
            if (mini == null)
            {
                return 0;
            }
            string prefab = "assets/prefabs/deployable/playerioents/gates/branch/electrical.branch.deployed.prefab";
            return setItemUpgrade<ElectricalBranch>(prefab, mini, mini.transform.position, pos_x, pos_y, pos_z, rot_x, rot_y, rot_z);
        }

        private ulong addTurretMini(BaseVehicle mini)
        {
            if (mini == null)
            {
                return 0;
            }
            setMiniRotation(mini);
            string prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
            return setItemUpgrade<AutoTurret>(prefab, mini, mini.transform.position, 0, 0.17f, 2.285f, 0, 0, 0);
        }

        private void setTurretRange(BasePlayer player, float range)
        {
            if (getMiniData(player, 5) == 0)
            {
                return;
            }
            AutoTurret turret = BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as AutoTurret;
            if (turret != null)
            {
                turret.sightRange = range;
            }
        }

        private void setMiniRotation(BaseVehicle mini)
        {
            if (mini == null)
            {
                return;
            }
            mini.transform.localPosition = new Vector3(mini.transform.position.x, mini.transform.position.y + 0.5f, mini.transform.position.z);
            mini.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            disableWire(getPlayer(mini.OwnerID));
        }

        private ulong setItemUpgrade<Type>(string prefab, BaseVehicle mini, Vector3 location, float pos_x, float pos_y, float pos_z, float rot_x, float rot_y, float rot_z)
            where Type : BaseCombatEntity
        {
            Type item = GameManager.server.CreateEntity(prefab, location, new Quaternion()) as Type;
            if (item == null)
            {
                return 0;
            }
            disableGroundReq(item);
            item.Spawn();
            item.pickup.enabled = false;
            item.SetParent(mini);
            if (prefab.Contains("batteries") && item != null)
            {
                (item as ElectricBattery).maxOutput = batteryMaxDrain;
            }
            item.transform.localPosition = new Vector3(pos_x, pos_y, pos_z);
            item.transform.localRotation = Quaternion.Euler(rot_x, rot_y, rot_z);
            item.OwnerID = mini.OwnerID;
            return item.net.ID.Value;
        }             

        private List<ElectricBattery> getBatteries(BasePlayer player)
        {
            List<ElectricBattery> e_Bat = new List<ElectricBattery>();
            ulong num = 0;
            for (int i = 2; i < 5; i++)
            {
                num = getMiniData(player, i);
                if (num != 0)
                {
                    e_Bat.Add(BaseNetworkable.serverEntities.Find(new NetworkableId(num)) as ElectricBattery);
                }
            }
            return e_Bat;
        }

        private int getTotalPower(List<ElectricBattery> batteries)
        {
            if (batteries == null)
            {
                return 0;
            }
            int power = 0;
            foreach (var bat in batteries)
            {
                if (bat != null)
                {
                    power = power + (int)Math.Round(bat.rustWattSeconds / 60, 0);
                }
            }
            return power;
        }

        private void setTotalPower(BasePlayer player, int power)
        {
            List<ElectricBattery> batteries = getBatteries(player);
            for (int bat = (batteries.Count() - 1); bat > -1; bat--)
            {
                if (power >= 150)
                {
                    batteries[bat].rustWattSeconds = (float)(60.0 * 150);
                    power = power - 150;
                }
                else if (power > 0 && power < 150)
                {
                    batteries[bat].rustWattSeconds = (float)(60.0 * power);
                    power = 0;
                }
                else
                {
                    batteries[bat].rustWattSeconds = (float)(0.0 * power);
                }
                batteries[bat].SendNetworkUpdate();
                // chatMessage(player, "No_Args", ("Battery " + batteries[bat].net.ID.Value.ToString() + "   " + (batteries[bat].rustWattSeconds/60.0).ToString()));
            }
            // return null;
        }

        private void setWireScheme(BasePlayer player)
        {
            bool first = true;
            ulong j = 0;
            IOEntity prev = new IOEntity();
            for (int i = 1; i < 6; i++)
            {
                j = getMiniData(player, i);
                if (j != 0 && first)
                {
                    prev = BaseNetworkable.serverEntities.Find(new NetworkableId(j)) as IOEntity;
                    first = false;
                }
                else if (j != 0)
                {
                    setWire(prev, 0, BaseNetworkable.serverEntities.Find(new NetworkableId(j)) as IOEntity, 0);
                    prev = BaseNetworkable.serverEntities.Find(new NetworkableId(j)) as IOEntity;
                }
            }
        }

        private void setWire(IOEntity output, int pin_O, IOEntity input, int pin_I)
        {
            if (output == null || input == null)
            {
                return;
            }
            input.inputs[pin_I].connectedTo.Set(output);
            output.outputs[pin_O].connectedTo.Set(input);

            input.inputs[pin_I].connectedToSlot = pin_O;
            output.outputs[pin_O].connectedToSlot = pin_I;

            input.inputs[pin_I].connectedTo.Init();
            output.outputs[pin_O].connectedTo.Init();

            output.MarkDirtyForceUpdateOutputs();

            output.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            input.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        private void disableWire(BasePlayer player)
        {
            ulong j = 0;
            IOEntity item = new IOEntity();
            for (int i = 1; i < 6; i++)
            {
                j = getMiniData(player, i);
                if (j != 0)
                {
                    item = (BaseNetworkable.serverEntities.Find(new NetworkableId(j)) as IOEntity);
                    if (item != null)
                    {
                        item.ClearConnections();
                    }
                }
            }
            setWireScheme(player);
        }

        private void disableGroundReq(BaseEntity entity)
        {
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
        }

        private int getFuelAmount(BaseVehicle mini)
        {
            StorageContainer fuelTank = mini.GetFuelSystem().GetFuelContainer();
            Item slot = fuelTank.inventory.GetSlot(0);
            if (slot != null)
            {
                return slot.amount;
            }
            else
            {
                return 0;
            }
        }

        private void setFuelAmount(BaseVehicle mini, int amount)
        {
            if (mini == null)
            {
                return;
            }
            StorageContainer fuelTank = mini.GetFuelSystem().GetFuelContainer();
            Item slot = fuelTank.inventory.GetSlot(0);
            if (slot != null)
            {
                fuelTank.inventory.Remove(slot);
            }
            if (amount > 0)
            {
                ItemManager.CreateByItemID(-946369541, amount)?.MoveToContainer(fuelTank.inventory);
            }
        }

        private void lockFuelTank(StorageContainer fuelTank, bool state)
        {
            fuelTank.SetFlag(BaseEntity.Flags.Locked, state);
        }

        private void resetBatteriesDrain(BasePlayer player)
        {
            if (player==null) { return; }
            if (miniData.ownerMini.ContainsKey(player.userID))
            {
                ulong[] nums = { getMiniData(player, 2), getMiniData(player, 3), getMiniData(player, 4) };
                for (int i = 0; i < nums.Length; i++)
                {
                    if (nums[i] != 0)
                    {
                        ElectricBattery bat = BaseNetworkable.serverEntities.Find(new NetworkableId(nums[i])) as ElectricBattery;
                        if (bat != null)
                        {
                            bat.maxOutput = (batteryMaxDrain - 5 * i);                             
                        }
                    }                    
                }
                 
            }
        }

        private void fixUpgradeMini(BasePlayer player)
        {
            OwnerMiniData data = miniData.ownerMini[player.userID];
            if (((BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 1))) as IOEntity) == null
                || (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 2))) as IOEntity) == null
                || (BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 3))) as IOEntity) == null)
                && data.hasBatteries())
            {
                removeBatteriesMini(player);
                setMiniRotation(getMinicopter(player) as BaseVehicle);
                addBatteriesMini(player, getMinicopter(player) as BaseVehicle);
                chatMessage(player, "Attempt_FixMini", "Batteries");
            }
            if ((BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 4))) as IOEntity) == null
                && data.hasElectricEngine())
            {
                setMiniRotation(getMinicopter(player) as BaseVehicle);
                setFuelRate(getMinicopter(player), data.getFuelPerSec());
                removeElectricMini(player);                
                upgradeElectricMini(player, getMinicopter(player) as BaseVehicle);
                if (data.hasAutoTurret())
                {
                    setFuelRate(getMinicopter(player), 0.20f);
                }
                else
                {
                    setFuelRate(getMinicopter(player), 0.10f);
                }                
                chatMessage(player, "Attempt_FixMini", "Efuel");
            }
            if ((BaseNetworkable.serverEntities.Find(new NetworkableId(getMiniData(player, 5))) as IOEntity) == null
                && data.hasAutoTurret())
            {
                setMiniRotation(getMinicopter(player) as BaseVehicle);
                removeTurretMini(player);
                setMiniData(player, addTurretMini(getMinicopter(player) as BaseVehicle), 5);
                setWireScheme(player);
                setTurretRange(player, 50);
                chatMessage(player, "Attempt_FixMini", "Turret");
            }
        }
        #endregion

        #region Classes
        private class MiniData
        {
            public Dictionary<ulong, OwnerMiniData> ownerMini = new Dictionary<ulong, OwnerMiniData>();
            public Dictionary<ulong, string> errorLog = new Dictionary<ulong, string>();
        }
    
        [Serializable]
        private class OwnerMiniData
        {
            #region class variables
            [JsonProperty]
            private ulong miniEntityID = 0;

            [JsonProperty]
            private DateTime manufactureData = new DateTime();

            [JsonProperty]
            private ulong batteryLeftID = 0;

            [JsonProperty]
            private ulong batteryRightID = 0;

            [JsonProperty]
            private ulong batteryEngineID = 0;

            [JsonProperty]
            private ulong branchID = 0;

            [JsonProperty]
            private ulong autoTurretID = 0;

            [JsonProperty]
            private float fuelPerSec = 0.25f;

            [JsonProperty]
            private List<string> authPilots = new List<string>();
            #endregion

            #region Get Variables Functions
            public ulong getMiniEntityID()  { return miniEntityID; }
            public DateTime getManufactureData() { return manufactureData;  }
            public ulong getBranchID() { return branchID; }
            public ulong getBatteryLeftID() { return batteryLeftID; }
            public ulong getBatteryRightID() { return batteryRightID; }
            public ulong getBatteryEngineID() { return batteryEngineID; }            
            public ulong getAutoTurretID() { return autoTurretID; }
            public float getFuelPerSec() { return fuelPerSec; }
            public List<string> getAuthPilots() { return authPilots; }
            #endregion

            #region Set Variables Functions
            public void setMiniEntityID(ulong entityID) { miniEntityID = entityID; }
            public void setManufactureData(DateTime data) { manufactureData = data; }
            public void setBranchID(ulong entityID) { branchID = entityID; }
            public void setBatteryLeftID(ulong entityID) { batteryLeftID = entityID; }
            public void setBatteryRightID(ulong entityID) { batteryRightID = entityID; }
            public void setBatteryEngineID(ulong entityID) { batteryEngineID = entityID; }            
            public void setAutoTurretID(ulong entityID) { autoTurretID = entityID; }
            public void setFuelPerSec(float rate) { fuelPerSec = rate; }
            public void setAuthPilots(List<string> pilots) { authPilots = pilots; }
            #endregion

            #region check if player has <blank>
            public bool hasMiniCopter() { return (miniEntityID != 0); }
            public bool hasBatteries() { return (batteryLeftID != 0 && batteryRightID != 0); }
            public bool hasElectricEngine() { return (batteryEngineID != 0); }
            public bool hasAutoTurret() { return (autoTurretID != 0); }
            #endregion
        }

        private class ConfigFile
        {
            [JsonProperty("miniSpawnDistance")]
            public float miniSpawnDistance { get; set; }

            [JsonProperty("miniStartHealth")]
            public float miniStartHealth { get; set; }

            [JsonProperty("AssetPrefab")]
            public string assetPrefab { get; set; }

            [JsonProperty("CanSpawnBuildingBlocked")]
            public bool canSpawnBuildingBlocked { get; set; }

            [JsonProperty("WaitTimes")]
            public Dictionary<string, float> waitTimes { get; set; }

            [JsonProperty("FuelRate")]
            public float fuelPerSec { get; set; }

            [JsonProperty("FuelRateUnlimited")]
            public float fuelPerSecUnlimited { get; set; }

            [JsonProperty("FuelAmountUnlimited")]
            public int fuelAmountUnlimited { get; set; }            
        }

        private ConfigFile GetDefaults()
        {
            return new ConfigFile
            {
                miniSpawnDistance = 5f,
                miniStartHealth = 750f,
                assetPrefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                canSpawnBuildingBlocked = false,
                waitTimes = new Dictionary<string, float>()
                {
                    ["ManageMini.noWT"] = 10f,
                    ["ManageMini.WaitTime2"] = 21600f,
                    ["ManageMini.WaitTime1"] = 43200f,
                    ["ManageMini.defaultWT"] = 86400f
                },
                fuelPerSec = 0.25f,
                fuelPerSecUnlimited = 0.0001f,
                fuelAmountUnlimited = 100
            };
        }
        #endregion

        #region Overides
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaults(), true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PermissionCheck_failed"] = "Sorry, but you aren't authorized to use this command.  See admin!",
                ["Mini_WaitList"] = "{0}, you have to wait another {1} minutes before you can use that command!",
                ["Unauth_PilotAttempt"] = "{0}, you are forbidden from piloting {1}'s mini. Request authorization from them!",
                ["Owner_AlertMini"] = "{0} attempted to pilot your mini without authorization!",
                ["Mini_DestroyedNow"] = "Your minicopter has been destroyed.  Better luck next time!",
                ["Make_Mini"] = "{0}, here's your minicopter!",
                ["Take_Mini"] = "Your mini has been taken back!",
                ["Get_Mini"] = "Your mini has been teleported to you!",
                ["Took_OwnershipMini"] = "You are now the owner of this mini!",
                ["FailedTake_OwnershipMini"] = "This mini is already owned by {0}!",
                ["Player_NotSeatedMini"] = "Please sit in one of the seats of a Minicopter.",
                ["Auth_Pilot"] = "You have authorized {0} to pilot your mini",
                ["Unauth_Pilot"] = "{0} authorization has been revoked!",
                ["Auth_PilotHeader"] = "Authorized Pilots: ",
                ["Auth_Pilots"] = "{0} can pilot your mini!",
                ["mini_NotFound"] = "I am sorry {0}, I don't think you have created a mini yet! /makeMini",
                ["Already_CreatedMini"] = "You already own a mini.",
                ["No_Args"] = "{0}",
                ["No_PlayerFound"] = "{0} is not an active player on this server.",
                ["No_FuelRate"] = "Your minicopter will never need to stop at the fuel station!",
                ["Mini_FuelRate"] = "Your mini uses {0} low grade per sec.",
                ["MiniCopter_AgeDHMS"] = "Your mini is {0} days {1} hours {2} minutes {3} seconds old.",
                ["Upgrade_Mini"] = "{0}, as you've requested, your minicopter has been upgraded!",
                ["Batteries_NotIncluded"] = "{0}, you must add batteries first",
                ["ManageMini_Help"] = "ManageMini Help",
                ["/miniHelp"] = "  <-- displays this help",
                ["/makeMini"] = "  <-- server creates and spawns a minicopter near player",
                ["/takeMini"] = "  <-- server takes back the minicopter",
                ["/getMini"] = "   <-- transfers the minicopter to you",
                ["/ownMini"] = "   <-- Attempts to take Ownership of a mini",
                ["/authPilot"] = "  <playerName>    <-- Authorizes player to pilot",
                ["/unauthPilot"] = "  <playerName>   <-- revokes player authorization",
                ["/miniDetails"] = "   <-- displays Authorized Pilots, Fuel Rate, Age",
                ["/upgrademini"] = "  <-- Add batteries, convert engine to electric, turret to front, ....",
                ["Attempt_FixMini"] = "{0} Not Found, attempting to fix!",
                ["Error_ManageMini"] = "ManageMini has failed due to an unknown error! Has been logged in the data file"
            }, this, "en");
        }
        #endregion

    }
}
 