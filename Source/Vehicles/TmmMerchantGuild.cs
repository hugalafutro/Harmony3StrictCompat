// Built into Compat/Harmony3StrictCompat.Vehicles.dll, outside any Assemblies folder: it uses Vehicle Framework,
// VEF and VFE Medieval 2 types, and this mod loads before all three. The main DLL loads it at startup
// (LoadVehiclesAddon), only when Too Many Mods' own Merchant Guild patch class is present.
using System.Collections.Generic;
using HarmonyLib;
using RimWorld.Planet;
using Verse;
using Vehicles;
using Vehicles.Compatibility;
using Vehicles.World;
using VFEMedieval;

namespace Harmony3StrictCompat.VehiclesCompat
{
	// ---- Too Many Mods: aerial vehicles and the VFE Medieval 2 Merchant Guild ----
	// TMM's VFEM2_VF.dll (built for Vehicle Framework 1.6) lets an aerial vehicle treat a Merchant Guild as an empty
	// tile: land beside it as a caravan, and only enter it while the guild stands still. Vehicle Framework 1.7 moved
	// ArrivalOption and AerialVehicleCompatibility to new namespaces, so TMM's PatchAll throws at startup ("token
	// resolves to an open generic type") and both patches are lost. The main DLL skips TMM's static constructor; this
	// is the same behaviour against the 1.7 API.
	public static class TmmMerchantGuild
	{
		public static void Run()
		{
			// TMM prefixed CanLandIn; 1.7 has a registration API for exactly that.
			AerialVehicleCompatibility.RegisterWorldObjectType(typeof(MerchantGuild),
				new AerialVehicleCompatibility.Settings(false, false) { canLandInValidator = GuildStandsStill });
			new Harmony("sammael.harmony3strictcompat.vehicles").Patch(
				AccessTools.Method(typeof(LaunchProtocol), nameof(LaunchProtocol.GetArrivalOptions)),
				postfix: new HarmonyMethod(typeof(TmmMerchantGuild), nameof(GuildAsEmptyTile)));
		}

		static bool GuildStandsStill(MapParent mapParent) =>
			mapParent is MerchantGuild guild && guild.pather != null && !guild.pather.MovingNow;

		static void GuildAsEmptyTile(ref IEnumerable<ArrivalOption> __result, LaunchProtocol __instance, GlobalTargetInfo target)
		{
			if (target.WorldObject is not MerchantGuild) return;
			var vehicle = __instance.Vehicle;
			if (vehicle == null) return;
			var options = new List<ArrivalOption>();
			if (WorldVehiclePathGrid.Instance.Passable(target.Tile, vehicle.VehicleDef))
				options.Add(new ArrivalOption("FormCaravanHere".Translate(), new ArrivalAction_LandToCaravan(vehicle)));
			__result = options;
		}
	}
}
