// Requires: ZoneManager

using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
	[Info("Zone Vending", "rostov114", "0.3.5")]
	[Description("Changing the logic of work of vending machines inside the zone.")]
	class ZoneVending : RustPlugin
	{
		#region References
		[PluginReference] private Plugin ZoneManager;
		#endregion

		#region Variables
		private Dictionary<ulong, bool> vendingMachineCache = new Dictionary<ulong, bool>();
		private static Configuration configuration = new Configuration();
		#endregion

		#region Configuration
		public class Configuration
		{
			[JsonProperty("Remove payment item")]
			public bool RemovePayment = true;

			[JsonProperty("List zones")]
			public List<string> Zones = new List<string>();
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				ZoneVending.configuration = Config.ReadObject<Configuration>();
			}
			catch
			{
				PrintError("Error reading config, please check!");

				Unsubscribe(nameof(OnGiveSoldItem));
				Unsubscribe(nameof(OnTakeCurrencyItem));
				Unsubscribe(nameof(OnEntityKill));
			}
		}

		protected override void LoadDefaultConfig()
		{
			ZoneVending.configuration = new Configuration();
		}
		#endregion

		#region OxideHooks
		private void OnServerInitialized()
		{
			if (!ZoneVending.configuration.RemovePayment)
			{
				Unsubscribe(nameof(OnTakeCurrencyItem));
			}
		}

		private void Unload()
		{
			ZoneVending.configuration = null;
		}

		private void OnGiveSoldItem(VendingMachine vending, Item soldItem, BasePlayer buyer)
		{
			if (this.CheckZone(vending) && Interface.Oxide.CallHook("CanGiveSoldItem", vending, soldItem, buyer) == null)
			{
				Item item = ItemManager.Create(soldItem.info, soldItem.amount, soldItem.skin);
				item.OnVirginSpawn();

				vending.transactionActive = true;
				if (!item.MoveToContainer(vending.inventory, -1, true))
				{
					PrintWarning(string.Concat(new string[]
					{
						"Vending machine unable to refill item :",
						soldItem.info.shortname,
						" buyer :",
						buyer.displayName
					}));
					item.Remove(0f);
				}
				vending.transactionActive = false;
			}
		}

		private object OnTakeCurrencyItem(VendingMachine vending, Item takenCurrencyItem)
		{
			if (this.CheckZone(vending) && Interface.Oxide.CallHook("CanTakeCurrencyItem", vending, takenCurrencyItem) == null)
			{
				takenCurrencyItem.MoveToContainer(vending.inventory, -1, true);
				takenCurrencyItem.RemoveFromContainer();
				takenCurrencyItem.Remove(0f);

				return true;
			}

			return null;
		}

		private void OnEntityKill(BaseNetworkable entity)
		{
			if (entity?.net?.ID != null && entity is VendingMachine && this.vendingMachineCache.ContainsKey(entity.net.ID.Value))
				this.vendingMachineCache.Remove(entity.net.ID.Value);
		}
		#endregion

		#region Helpers
		private bool CheckZone(VendingMachine vending)
		{
			if (vending?.net?.ID == null)
				return false;

			if (this.vendingMachineCache.ContainsKey(vending.net.ID.Value))
				return this.vendingMachineCache[vending.net.ID.Value];

			if (ZoneVending.configuration.Zones.Count > 0)
			{
				string[] zmloc = ZoneManager?.Call<string[]>("GetEntityZoneIDs", (vending as BaseEntity));
				if (zmloc != null && zmloc.Length > 0)
				{
					foreach (string zone in zmloc)
					{
						if (ZoneVending.configuration.Zones.Contains(zone))
						{
							this.vendingMachineCache.Add(vending.net.ID.Value, true);
							return true;
						}
					}
				}
			}

			this.vendingMachineCache.Add(vending.net.ID.Value, false);
			return false;
		}
		#endregion
	}
}