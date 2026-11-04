using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Harmony3StrictCompat
{
	// Local stop-gap fixes for RimWorld 1.7 (Harmony 3, Unity replaced by UnityStubs).
	// Every entry is temporary: remove it once the affected mod ships its own fix.
	public class Harmony3StrictCompatMod : Mod
	{
		public Harmony3StrictCompatMod(ModContentPack content) : base(content)
		{
			var harmony = new Harmony(content.PackageId);
			var ctor = AccessTools.Constructor(typeof(Patch), [typeof(HarmonyMethod), typeof(int), typeof(string)]);
			harmony.Patch(ctor, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PatchCtorPrefix)));
			harmony.Patch(AccessTools.Method(typeof(PatchOperation), nameof(PatchOperation.Apply)),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(FixXpathPrefix)));
			harmony.Patch(AccessTools.Method(typeof(PatchOperation), nameof(PatchOperation.Apply)),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SkipConditionalPrefix)));
			var sbo = Target("SensibleBedOwnership.Patch_ReservationManager:Prefix");
			if (sbo != null)
				harmony.Patch(sbo, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SboUnspawnedBedPrefix)));
			// GitHub build (2026-10-28+) picks leaders in a Prefix and charges their cost; older builds add them in the Postfix.
			var slPick = Target("SimpleLeadership.PawnGroupMakerUtility_GeneratePawns_Patch:Prefix", optional: true);
			var slLeader = Target("SimpleLeadership.PawnGroupMakerUtility_GeneratePawns_Patch:Postfix", optional: slPick != null);
			if (slPick != null)
				harmony.Patch(slPick, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SlLeaderBudget)));
			else if (slLeader != null)
				harmony.Patch(slLeader, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SlLeaderBefore)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SlLeaderAfter)));
			harmony.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker), "ShouldHaveNeed"),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(NoNeedsBeforeWorld)) { priority = Priority.First });
			harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
				postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(DropWorldCopiesOfShipPawns)));
			var btApply = Target("TraitTracker.TraitHediffApplier:ApplyHediffsToPawnsWithTraits");
			if (btApply != null)
				harmony.Patch(btApply, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(DropLogMessages)));
			var trWindow = AccessTools.TypeByName("TriumphantResearch.Window_ResearchComplete");
			var trCtor = trWindow == null ? null : AccessTools.DeclaredConstructor(trWindow, [typeof(ResearchProjectDef)]);
			if (trCtor != null)
			{
				Fingerprint("TriumphantResearch.Window_ResearchComplete:.ctor", trCtor);
				harmony.Patch(trCtor, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(TrFillEmptyCarousel)));
			}
			// Not patched here (its binding is relaxed in PatchCtorPrefix), but fingerprinted all the same.
			Target("SkilledRefueling.HarmonyPatches:JobDriverRefuelAtomic_MakeNewToils_Postfix");
			var mvcfStat = Target("MVCF.Utilities.VerbStatsUtility:GetStatValue");
			if (mvcfStat != null)
			{
				harmony.Patch(mvcfStat, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(MvcfUnsetVerbStat)));
				var mvcfRow = Target("MVCF.Utilities.VerbStatsUtility:CreateAverageStat",
					args: [typeof(List<VerbProperties>), typeof(StatDef), typeof(ThingDef), typeof(StatCategoryDef)]);
				if (mvcfRow != null)
					harmony.Patch(mvcfRow, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(MvcfHideUnsetStatRow)));
				var mvcfRows = Target("MVCF.Utilities.VerbStatsUtility:DisplayStatsForVerbs");
				if (mvcfRows != null)
				{
					// The stub only builds the iterator; the Range bug lives in MoveNext, so fingerprint that too.
					Fingerprint("MVCF.Utilities.VerbStatsUtility:DisplayStatsForVerbs/MoveNext", AccessTools.EnumeratorMoveNext(mvcfRows));
					harmony.Patch(mvcfRows, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(MvcfFixRange)));
				}
			}
			var guDismount = Target("GiddyUp.MountUtility:Dismount");
			if (guDismount != null)
				harmony.Patch(guDismount, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(GuKeepTravelFocus)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(GuRestoreTravelFocus)));
			harmony.Patch(AccessTools.Method(typeof(TraderCaravanUtility), nameof(TraderCaravanUtility.GetTraderCaravanRole)),
				postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(TraderRoleByTraderKind)));
			var hospSleep = Target("Hospitality.JobGiver_Sleep:TryIssueJobPackage");
			guestBedField = AccessTools.Field(AccessTools.TypeByName("Hospitality.CompGuest"), "bed");
			if (hospSleep != null && guestBedField != null)
				harmony.Patch(hospSleep, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(GuestFloorSleepProbe)));
			var epDispatch = Target("FinitePopulationVeterans.WorldPopulationManager:IsPawnAvailableForDispatch");
			if (epDispatch != null)
				harmony.Patch(epDispatch, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(EpNoLeaderVeterans)));
			var hospRegear = Target("Hospitality.Utilities.SpawnGroupUtility:GenerateNewGearFor");
			if (hospRegear != null)
				harmony.Patch(hospRegear, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HospKeepWarcasketGear)));
			var hospPocket = Target("Hospitality.Utilities.ItemUtility:PocketHeadgear");
			if (hospPocket != null)
				harmony.Patch(hospPocket, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HospKeepCasketHelmet)));
			var epMatch = Target("FinitePopulationVeterans.WorldPopulationManager:PawnMatchesRequest");
			if (epMatch != null)
				harmony.Patch(epMatch, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(EpViolenceCheck)));
			var mssSave = Target("MSSFP.Utils.PawnGraphicUtils+<>c__DisplayClass4_0:<SaveTextureToFile>b__0");
			if (mssSave != null)
				harmony.Patch(mssSave, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(MssFlipHauntPng)));
			var cfuLabel = Target("Caveworld_Flora_Unleashed.FruitingBody:get_LabelMouseover");
			if (cfuLabel != null)
				harmony.Patch(cfuLabel, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CfuGrowthInLabel)));
			cfuFruit = AccessTools.TypeByName("Caveworld_Flora_Unleashed.FruitingBody");
			if (cfuFruit != null)
			{
				// Self-retiring: once the mod handles removal itself, a second decrement would be wrong.
				if (AccessTools.DeclaredMethod(cfuFruit, "DeSpawn") != null || AccessTools.DeclaredMethod(cfuFruit, "Destroy") != null)
					Log.Error("[Harmony3StrictCompat] Caveworld Flora FruitingBody now overrides DeSpawn/Destroy: its mod updated, colony regrow fix skipped, remove it");
				else
				{
					Target("Caveworld_Flora_Unleashed.GenCaveFungusReproduction:TryGrowMycelium");
					cfuMyceliumOf = AccessTools.FieldRefAccess<object>(cfuFruit, "Mycelium");
					var myc = AccessTools.TypeByName("Caveworld_Flora_Unleashed.Mycelium");
					cfuActualSize = AccessTools.FieldRefAccess<int>(myc, "actualSize");
					harmony.Patch(AccessTools.DeclaredMethod(typeof(Plant), nameof(Plant.DeSpawn)),
						prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CfuFruitRemoved)));
					harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
						postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CfuRecountColonies)));
				}
			}
			var lpInfo = Target("ilyvion.LoadingProgress.StartupImpact.ModInfoList:GetModInfoFor");
			if (lpInfo != null)
				harmony.Patch(lpInfo, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LpLockEnter)),
					finalizer: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LpLockExit)));
			var bwmFilters = Target("ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawFilters");
			if (bwmFilters != null)
				harmony.Patch(bwmFilters, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(BwmClearStyleButton)));
			var lcBwm = Target("Inventory.BWM_DoWindowContentsPatch_Patch:Transpiler");
			lcCountAway = Target("Inventory.BWM_DoWindowContentsPatch_Patch:ShouldCountAway");
			if (lcBwm != null && lcCountAway != null && bwmFilters != null)
				harmony.Patch(lcBwm, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LcBwmCountAway)));
		}

		// ---- Better Workbench Management: bill options overlap the Ideology style button -------------------------
		// BWM draws its block (output filter, count away, restrict workbench) in the bill dialog's left column at
		// inRect.height - 50 - 100, about 99 px tall, so it ends ~51 px above the bottom. Vanilla puts the "Use global
		// style" dropdown (or "No styles available") at height - 90 .. -60 in the same column when Ideology is in classic
		// mode and the recipe makes a thing; Style Change Anytime draws its own dropdown in the same spot for every
		// recipe that makes a thing, classic or not. "Restrict workbench" sits on top of it. Lift BWM's block by 50 px
		// whenever Ideology is active and the recipe makes a thing (offset 100 becomes 150).
		static readonly AccessTools.FieldRef<Dialog_BillConfig, Bill_Production> BillOf =
			AccessTools.FieldRefAccess<Dialog_BillConfig, Bill_Production>("bill");

		static float BwmBottomOffset(Dialog_BillConfig dialog) =>
			ModsConfig.IdeologyActive && BillOf(dialog)?.recipe?.ProducedThingDef != null ? 150f : 100f;

		static IEnumerable<CodeInstruction> BwmClearStyleButton(IEnumerable<CodeInstruction> instructions)
		{
			var list = instructions.ToList();
			int hits = 0;
			for (int i = 2; i < list.Count; i++)
				if (list[i].opcode == OpCodes.Ldc_R4 && list[i].operand is float f && f == 100f
					&& list[i - 1].opcode == OpCodes.Sub && list[i - 2].opcode == OpCodes.Ldc_R4 && list[i - 2].operand is float g && g == 50f)
				{
					list[i] = new CodeInstruction(OpCodes.Ldarg_0).WithLabels(list[i].labels).WithBlocks(list[i].blocks);
					list.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(BwmBottomOffset))));
					hits++;
				}
			if (hits != 1)
				Log.Error($"[Harmony3StrictCompat] BWM style-button overlap: expected 1 `height - 50 - 100`, found {hits}");
			return list;
		}

		// ---- Loadout Compositing x Better Workbench Management: "count away" option missing -----------------------
		// BWM's bill dialog shows the "count pawns away / on caravan" checkbox only when repeatMode == TargetCount.
		// Loadout Compositing transpiles BillConfig_DoWindowContents_Patch.DrawFilters so its per-tag mode gets it too,
		// matching `ldloc.0; ldfld repeatMode; ldsfld TargetCount; bne.un`. The BWM 1.7 Waylith build of 2026-11-03 is a
		// Debug build that compiles the test to `...; ceq; br.s`, so the match fails ("Error in transpiler ... off map")
		// and per-tag bills lose the option. Swap the `ldfld; ldsfld; ceq` for LC's own ShouldCountAway(bill). With no
		// ceq form (a Release build again) LC's own transpiler runs unchanged.
		static MethodInfo lcCountAway;
		static bool LcBwmCountAway(IEnumerable<CodeInstruction> instructions, ref IEnumerable<CodeInstruction> __result)
		{
			var repeatMode = AccessTools.Field(typeof(Bill_Production), nameof(Bill_Production.repeatMode));
			var targetCount = AccessTools.Field(typeof(BillRepeatModeDefOf), nameof(BillRepeatModeDefOf.TargetCount));
			var list = instructions.ToList();
			int hits = 0;
			for (int i = 1; i + 2 < list.Count; i++)
				if (list[i - 1].opcode == OpCodes.Ldloc_0 && list[i].LoadsField(repeatMode) && list[i + 1].LoadsField(targetCount) && list[i + 2].opcode == OpCodes.Ceq)
				{
					list[i] = new CodeInstruction(OpCodes.Call, lcCountAway).WithLabels(list[i].labels).WithBlocks(list[i].blocks);
					list.RemoveRange(i + 1, 2);
					hits++;
				}
			if (hits == 0) return true;
			if (hits != 1) Log.Error($"[Harmony3StrictCompat] Loadout Compositing x BWM count-away: expected 1 repeatMode test, patched {hits}");
			__result = list;
			return false;
		}

		// ---- Loading Progress: race in its startup-impact profiler ----------------------------------------------
		// ModInfoList.GetModInfoFor does Dictionary.TryGetValue then Add with no lock, and is called from the parallel
		// per-mod XML loading. Two threads for the same mod both miss and both Add: "Could not load defs for mod
		// Ludeon.RimWorld.Anomaly: An item with the same key has already been added", so that mod's defs are skipped.
		// 2026-11-03 it hit Anomaly; a recipe then referenced a missing Anomaly item, RecipeDefGenerator null-reffed,
		// the game reset ModsConfig.xml to Core + DLC and reloaded with thousands of errors. Serialize the call.
		static void LpLockEnter(object __instance, out bool __state)
		{
			__state = false;
			System.Threading.Monitor.Enter(__instance, ref __state);
		}

		static void LpLockExit(object __instance, bool __state)
		{
			if (__state) System.Threading.Monitor.Exit(__instance);
		}

		// ---- Biomes! Caverns (Caveworld Flora Unleashed): wild fungus colonies never regrow ---------------------
		// A Mycelium counts its fruiting bodies in actualSize and only grows new ones (GenCaveFungusReproduction.
		// TryGrowMycelium) while actualSize < desiredSize. Mycelium.NotifyPlantRemoved should lower the count, but nothing
		// calls it (1.5, 1.6 and 1.7 builds alike), so a colony that once filled up stays "full" with no mushrooms forever
		// (13 of 22 colonies in "The Robracam Treaty fix teh shrooms", 2026-11-03). Decrement on each fruiting body
		// despawn (harvest, rot, fire all go through Plant.DeSpawn), floored at 0: the mod's own method would destroy the
		// colony at 0, so harvesting every mushroom would wipe it. FinalizeInit resets every count to the live bodies.
		static Type cfuFruit;
		static AccessTools.FieldRef<object, object> cfuMyceliumOf;
		static AccessTools.FieldRef<object, int> cfuActualSize;

		static void CfuFruitRemoved(Plant __instance)
		{
			if (!cfuFruit.IsInstanceOfType(__instance) || cfuMyceliumOf(__instance) is not Thing myc || myc.Destroyed) return;
			ref int size = ref cfuActualSize(myc);
			if (size > 0) size--;
		}

		static void CfuRecountColonies()
		{
			int fixedCount = 0;
			foreach (var map in Find.Maps)
			{
				var live = new Dictionary<Thing, int>();
				foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
					if (cfuFruit.IsInstanceOfType(t) && cfuMyceliumOf(t) is Thing myc)
						live[myc] = live.GetValueOrDefault(myc) + 1;
				foreach (var myc in map.listerThings.ThingsOfDef(ThingDef.Named("BMT_Mycelium")))
				{
					ref int size = ref cfuActualSize(myc);
					int n = live.GetValueOrDefault(myc);
					if (size != n) { size = n; fixedCount++; }
				}
			}
			if (fixedCount > 0)
				Log.Message($"[Harmony3StrictCompat] Caveworld Flora: reset {fixedCount} fungus colony count(s) to their live mushrooms, they regrow now");
		}

		// ---- Biomes! Caverns (Caveworld Flora Unleashed): wild fungi mouseover without growth ------------------
		// FruitingBody.LabelMouseover (every wild cave fungus, Gleamcap subclass included) copies vanilla Plant's
		// getter but lost the `" (" + PercentGrowth` line, so the readout shows "Brightbells)" with no growth.
		// Re-insert it right after the label; the result goes back into the mod's own cache (cleared each TickLong).
		static void CfuGrowthInLabel(Plant __instance, ref string __result, ref string ___cachedLabelMouseover)
		{
			string label = __instance.def.LabelCap;
			if (__result == null || !__result.StartsWith(label) || __result.StartsWith(label + " (")) return;
			__result = label + " (" + "PercentGrowth".Translate((Mathf.Floor((__instance.Growth + 0.0001f) * 100f) / 100f).ToStringPercent()) + __result.Substring(label.Length);
			___cachedLabelMouseover = __result;
		}

		// ---- MSS Tweaks and Fun: haunt portraits saved upside down ---------------------------------------
		// PawnGraphicUtils.SaveTextureToFile snapshots a pawn portrait for its haunt ghost: Blit to a temporary RT,
		// AsyncGPUReadback, EncodeArrayToPNG into Config/Haunts/<guid>.png. On 1.7 (Vulkan) the readback rows come
		// back top-down while EncodeArrayToPNG expects bottom-up, so every new haunt PNG is stored and drawn
		// upside down (verified 2026-11-02 on both files). Swap the encode call for one that flips the rows first.
		static readonly MethodInfo EncodePng = AccessTools.Method(typeof(ImageConversion), nameof(ImageConversion.EncodeArrayToPNG),
			[typeof(Array), typeof(UnityEngine.Experimental.Rendering.GraphicsFormat), typeof(uint), typeof(uint), typeof(uint)]);
		static IEnumerable<CodeInstruction> MssFlipHauntPng(IEnumerable<CodeInstruction> instructions)
		{
			int swapped = 0;
			foreach (var ins in instructions)
			{
				if (ins.Calls(EncodePng))
				{
					swapped++;
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(EncodeFlippedPng)))
						.WithLabels(ins.labels).WithBlocks(ins.blocks);
				}
				else yield return ins;
			}
			if (swapped != 1)
				Log.Error($"[Harmony3StrictCompat] MSS haunt PNG flip: expected 1 EncodeArrayToPNG call, found {swapped}");
		}

		static byte[] EncodeFlippedPng(Array data, UnityEngine.Experimental.Rendering.GraphicsFormat format, uint width, uint height, uint rowBytes)
		{
			if (data is byte[] bytes && height > 1 && bytes.Length % height == 0)
			{
				int row = bytes.Length / (int)height;
				var flipped = new byte[bytes.Length];
				for (int y = 0; y < height; y++)
					Buffer.BlockCopy(bytes, y * row, flipped, ((int)height - 1 - y) * row, row);
				data = flipped;
			}
			return ImageConversion.EncodeArrayToPNG(data, format, width, height, rowBytes);
		}

		// ---- Eternal Pawns: faction leaders recycled as ordinary raiders/visitors ------------------------------
		// Eternal Pawns (helldan.eternalpawns, 1.6 build) prefixes PawnGenerator.GeneratePawn at priority 2000 and, with
		// veteranRecallChance (default 0.5), hands back a pawn from its per-faction veteran pool instead of generating one,
		// then stores request.KindDef straight into pawn.kindDef. Its pool filter (IsPawnAvailableForDispatch) never checks
		// for faction leaders, so Nelle (VFEP_General, leader of the Mercenaries) came back as a VFEP_Private visitor;
		// Hospitality's spawn-time RedressPawn then regenerated Private gear, destroying her warcasket (VEF traitsOnUnequip
		// added VFEP_Shellcasket). Traced with a tripwire 2026-11-02. Keep faction leaders, leader-kind pawns and pawns
		// wearing a warcasket out of the veteran draw. PawnMatchesRequest also skips request.MustBeCapableOfViolence
		// (reported on the workshop as a Hospitality retry loop / freeze), so honour that flag too.
		static void EpNoLeaderVeterans(Pawn p, ref bool __result)
		{
			if (!__result || p == null) return;
			string why = PawnUtility.IsFactionLeader(p) ? "is a faction leader"
				: p.kindDef?.factionLeader == true ? $"has the leader kind {p.kindDef.defName}"
				: p.apparel?.WornApparel.Any(a => a.GetType().Name == "Apparel_Warcasket") == true ? "wears a warcasket"
				: null;
			if (why == null) return;
			__result = false;
			Log.Message($"[Harmony3StrictCompat] Eternal Pawns: kept {p.LabelShort} ({p.Faction?.Name}) out of the veteran draw, {why}");
		}

		// ---- Hospitality: visitors in warcaskets re-geared on arrival ---------------------------------------
		// SpawnGroupUtility.GenerateNewGearFor runs PawnGenerator.RedressPawn on every visitor, which destroys all worn
		// apparel and rolls a fresh set from the pawn kind. For a warcasket kind that yields casket pieces plus whatever
		// the vanilla generator adds (free warm layer, headgear), without VFE Pirates' GeneratePawn postfix that strips
		// non-casket apparel and locks the set: Nelle arrived in a casket drawn under a vest and helmet, face on top.
		// A pawn already wearing a warcasket keeps its gear and inventory, and its casket pieces are locked again (an
		// earlier Hospitality re-gear left Nelle's pieces unlocked); everyone else is re-geared as before.
		static bool HospKeepWarcasketGear(Pawn pawn)
		{
			if (!WearsCasket(pawn)) return true;
			foreach (var a in pawn.apparel.WornApparel.Where(IsCasketPiece).ToList())
				if (!pawn.apparel.IsLocked(a)) pawn.apparel.Lock(a);
			Log.Message($"[Harmony3StrictCompat] Hospitality: {pawn.LabelShort} keeps its warcasket, skipped the arrival re-gear");
			return false;
		}

		// Hospitality ItemUtility.PocketHeadgear moves every head-covering item of an arriving guest into its inventory
		// (WearHeadgear puts it back on departure) and ignores apparel locks, so a warcasket helmet came off on arrival.
		// VFE Pirates strips all non-casket apparel from casket wearers, so skipping the whole call loses nothing.
		static bool HospKeepCasketHelmet(Pawn pawn) => !WearsCasket(pawn);

		static bool IsCasketPiece(Apparel a) => a?.GetType().Name == "Apparel_Warcasket";
		static bool WearsCasket(Pawn p) => p?.apparel?.WornApparel.Any(IsCasketPiece) == true;

		static void EpViolenceCheck(Pawn p, PawnGenerationRequest req, ref bool __result)
		{
			if (__result && req.MustBeCapableOfViolence && p != null && p.WorkTagIsDisabled(WorkTags.Violent))
				__result = false;
		}

		// ---- Diagnostic: Hospitality guest sleeping on the floor next to a bed it owns ----------------------
		// JobGiver_Sleep puts a guest on the floor when RestUtility.IsValidBedFor rejects its claimed guest bed (or
		// it owns none), and logs nothing. Seen 2026-11-01 (Bladeskarthak, VRE Insector); the save afterwards showed
		// no blocker, so the cause was momentary. Re-run the checks the moment the floor job is handed out and log
		// which fail. Diagnostic only: changes nothing. Remove once the cause is known.
		static FieldInfo guestBedField;
		static readonly HashSet<int> probedJobs = new();

		static void GuestFloorSleepProbe(Pawn __0, ThinkResult __result)
		{
			var job = __result.Job;
			if (job == null || job.def != JobDefOf.LayDown || job.targetA.HasThing || __0?.Map == null) return;
			var comp = __0.AllComps?.FirstOrDefault(c => c.GetType() == guestBedField.DeclaringType);
			if (comp == null || guestBedField.GetValue(comp) is not Building_Bed bed || !probedJobs.Add(job.loadID)) return;
			try
			{
				Log.Warning($"[Harmony3StrictCompat] guest {__0.LabelShort} sleeps on the floor at {job.targetA.Cell} although it claimed {bed.LabelShort} at {bed.Position}: {string.Join("; ", BedProblems(__0, bed))}");
			}
			catch (Exception e)
			{
				Log.Warning($"[Harmony3StrictCompat] guest floor-sleep probe for {__0.LabelShort} failed: {e.Message}");
			}
		}

		static IEnumerable<string> BedProblems(Pawn p, Building_Bed bed)
		{
			if (!bed.Spawned || bed.Map != p.Map) { yield return "bed not spawned on the guest's map"; yield break; }
			bool found = false;
			if (!bed.OwnersForReading.Contains(p)) { found = true; yield return $"not among the bed's owners ({Names(bed.OwnersForReading)}), so Hospitality looked for another bed and found none"; }
			if (!RestUtility.CanUseBedEver(p, bed.def)) { found = true; yield return $"CanUseBedEver false (body size {p.BodySize:F2}, max {bed.def.building.bed_maxBodySize:F2})"; }
			if (bed.CompAssignableToPawn.IdeoligionForbids(p)) { found = true; yield return "ideoligion forbids this bed"; }
			if (!bed.AnyUnoccupiedSleepingSlot) { found = true; yield return $"no free sleeping slot, occupants: {Names(bed.CurOccupants)}"; }
			if (bed.ForPrisoners || bed.ForSlaves || bed.Medical) { found = true; yield return $"bed set for prisoners={bed.ForPrisoners} slaves={bed.ForSlaves} medical={bed.Medical}"; }
			if (!RestUtility.CanUseBedNow(bed, p, false)) { found = true; yield return "RestUtility.CanUseBedNow false"; }
			if (!p.CanReach(bed, PathEndMode.OnCell, Danger.Some))
			{
				found = true;
				yield return p.CanReach(bed, PathEndMode.OnCell, Danger.Deadly)
					? "path to bed counts as dangerous (reachable only at Danger.Deadly: temperature or hazard on the way)"
					: "bed unreachable";
			}
			var reservers = new HashSet<Pawn>();
			bed.Map.reservationManager.ReserversOf(bed, reservers);
			reservers.Remove(p);
			if (!p.CanReserve(bed, bed.SleepingSlotsCount, 0))
			{
				found = true;
				yield return $"cannot reserve; reserved by {string.Join(", ", reservers.Select(r => $"{r.LabelShort} ({r.CurJobDef?.defName ?? "no job"})"))}";
			}
			if (bed.IsForbidden(p)) { found = true; yield return "bed forbidden for the guest (guest area?)"; }
			if (bed.Faction != p.Faction && bed.Faction != p.HostFaction) { found = true; yield return $"bed faction {bed.Faction?.Name} is neither the guest's nor its host's ({p.HostFaction?.Name})"; }
			if (!found)
				yield return RestUtility.IsValidBedFor(bed, p, p, false)
					? "bed is valid again already: whatever blocked it passed within the same tick"
					: "vanilla checks pass, so a mod postfix on IsValidBedFor rejected it";
		}

		static string Names(IEnumerable<Pawn> pawns) => pawns == null || !pawns.Any() ? "none" : string.Join(", ", pawns.Select(x => x.LabelShort));

		// ---- Giddy-Up 2: caravan mounts forget the caravan's destination ------------------------------------
		// MountUtility.Dismount sets a non-player animal's duty focus to the cell it stands on. For a trade caravan
		// still in LordToil_Travel that duty is TravelOrLeave to the chill spot, so the animal parks where its rider
		// stepped off (seen 2026-11-01: two Patreons muffalos at a wall 55 cells short). Vanilla only advances the
		// caravan once every member is near the chill spot, so it never arrives, never trades, never leaves.
		// The same overwrite turns a caravan animal's Follow/Escort-the-trader duty into "follow a cell".
		// Keep the focus across Dismount for lord animals when it is a TravelOrLeave destination or a pawn/thing to
		// follow; other cell foci (visitors' Defend spot etc.) still get Giddy-Up's parking behaviour.
		static void GuKeepTravelFocus(Pawn __1, out LocalTargetInfo __state)
		{
			var duty = __1?.mindState?.duty;
			__state = duty != null && __1.GetLord() != null && (duty.def == DutyDefOf.TravelOrLeave || duty.focus.HasThing)
				? duty.focus : LocalTargetInfo.Invalid;
		}

		// Dismount only rewrites the focus of the duty object already there, so putting the old focus back is exact.
		static void GuRestoreTravelFocus(Pawn __1, LocalTargetInfo __state)
		{
			var duty = __1?.mindState?.duty;
			if (__state.IsValid && duty != null)
				duty.focus = __state;
		}

		// ---- Trade caravan led by a non-trader pawn kind --------------------------------------------------
		// GetTraderCaravanRole calls a pawn the Trader only when kindDef.trader is set. Some caravans carry their
		// trader role (Pawn_TraderTracker.traderKind) on a pawn whose kind is not a trader kind (a Patreons
		// Mercenary_Gunner, 2026-11-01; origin unknown). That pawn then counts as a Guard: FindTrader returns null,
		// so LordJob_TradeWithColony never sees mindState.traderDismissed, and once leaving,
		// LordToil_ExitMapAndEscortCarriers gives the trader an Escort duty on itself, so the caravan never exits.
		// A guard that actually holds a trader kind is the trader.
		static void TraderRoleByTraderKind(Pawn p, ref TraderCaravanRole __result)
		{
			if (__result == TraderCaravanRole.Guard && p?.TraderKind != null)
				__result = TraderCaravanRole.Trader;
		}

		// ---- Patched-mod change detection ---------------------------------------------------------------
		// Every foreign method this mod touches is fingerprinted (FNV-1a 64 of its IL bytes; Tools/ilhash.py
		// prints the same number). A changed or vanished method means that mod updated, so the fix it covers
		// must be re-checked: red error in the log, but the fix still applies until someone removes it.
		static readonly Dictionary<string, ulong> Fingerprints = new()
		{
			["SensibleBedOwnership.Patch_ReservationManager:Prefix"] = 0xC02F4F892C656903,                       // 2026-10-28
			["SimpleLeadership.PawnGroupMakerUtility_GeneratePawns_Patch:Postfix"] = 0x931CDA2DA9F203ED,          // Steam 2026-10-27
			["TraitTracker.TraitHediffApplier:ApplyHediffsToPawnsWithTraits"] = 0x67BDC9BB976E9B72,                // 2026-10-22
			["SkilledRefueling.HarmonyPatches:JobDriverRefuelAtomic_MakeNewToils_Postfix"] = 0x80FDB19E16DDBC54,
			// VEF 1.7 MVCF.dll, 2026-10-31
			["MVCF.Utilities.VerbStatsUtility:GetStatValue"] = 0x691C1FF55EB56CE6,
			["MVCF.Utilities.VerbStatsUtility:CreateAverageStat"] = 0x00F6026D0950F156,                           // StatDef overload
			["MVCF.Utilities.VerbStatsUtility:DisplayStatsForVerbs"] = 0x20DD3E5059E47E19,
			["MVCF.Utilities.VerbStatsUtility:DisplayStatsForVerbs/MoveNext"] = 0xE4C1A1BF7F4C9435,
			["VEF.Weapons.VerbUtility+VerbProperties_AdjustedRange_Patch:Prefix"] = 0xD224B52B0AFE0FE4,       // VEF 1.7, 2026-11-03 (NaN __state sentinel; still mutates shared range)
			["VEF.Weapons.VerbUtility+VerbProperties_AdjustedRange_Patch:Finalizer"] = 0x5828134280ED3E2B,
			["TriumphantResearch.Window_ResearchComplete:.ctor"] = 0x55438039A644309A,                            // 1.6 build, 2026-11-01
			["GiddyUp.MountUtility:Dismount"] = 0x6A5F245D923DC418,                                                // Giddy-Up 2 Continued 1.7, 2026-11-01
			["Hospitality.JobGiver_Sleep:TryIssueJobPackage"] = 0x1CEFA35951771534,                              // Hospitality 1.7, 2026-11-01 (diagnostic only)
			["Hospitality.Utilities.SpawnGroupUtility:GenerateNewGearFor"] = 0x9968B91075E1A216,                  // Hospitality 1.7, 2026-11-02
			["Hospitality.Utilities.ItemUtility:PocketHeadgear"] = 0x5500E1A13797F143,                       // Hospitality 1.7, 2026-11-02
			["FinitePopulationVeterans.WorldPopulationManager:IsPawnAvailableForDispatch"] = 0x5A8A111FF41E04D8, // Eternal Pawns 1.6 build, 2026-04-13
			["FinitePopulationVeterans.WorldPopulationManager:PawnMatchesRequest"] = 0xAF54B4F3884BC77E, // Eternal Pawns 1.6 build, 2026-04-13
			["MSSFP.Utils.PawnGraphicUtils+<>c__DisplayClass4_0:<SaveTextureToFile>b__0"] = 0x3C2DDE1F39E984E8,  // MSS Tweaks and Fun 1.7, 2026-10-19
			["Caveworld_Flora_Unleashed.FruitingBody:get_LabelMouseover"] = 0xB098C8B9BC3CE626,                 // Biomes! Caverns 1.7, 2026-10-20
			["Caveworld_Flora_Unleashed.GenCaveFungusReproduction:TryGrowMycelium"] = 0x614612774F447421,         // Biomes! Caverns 1.7, 2026-10-20
			["ilyvion.LoadingProgress.StartupImpact.ModInfoList:GetModInfoFor"] = 0x357F68C7E142FD4F,          // Loading Progress 1.7, 2026-10-21
			["Inventory.BWM_DoWindowContentsPatch_Patch:Transpiler"] = 0x1208A46933A47D41,                     // Loadout Compositing 1.7, 2026-10-28
			["Inventory.BWM_DoWindowContentsPatch_Patch:ShouldCountAway"] = 0xA22693055B6314E7,                // Loadout Compositing 1.7, 2026-10-28
			["ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawFilters"] = 0x5FFF926429D5F4D6,        // BWM 1.7 Waylith (Debug build), 2026-11-03
			["VanillaRacesExpandedSanguophage.JobGiver_GetCorpseHemogen:TryGiveJob"] = 0x00285EA300E503C6,   // VRE Sanguophage 1.6 build, 2026-10-01
		};

		// "Namespace.Type:Method". Null and silent when the type (i.e. the mod) is absent. A missing method is an
		// error unless optional (a build that never had it). An unrecorded fingerprint is a warning with the value.
		// args picks one overload.
		internal static MethodInfo Target(string name, bool optional = false, Type[] args = null)
		{
			int colon = name.IndexOf(':');
			var type = AccessTools.TypeByName(name.Substring(0, colon));
			if (type == null) return null;
			var method = AccessTools.DeclaredMethod(type, name.Substring(colon + 1), args);
			if (method == null)
			{
				if (!optional) Log.Error($"[Harmony3StrictCompat] {name} no longer exists: its mod updated, remove or redo this fix");
				return null;
			}
			Fingerprint(name, method);
			return method;
		}

		static void Fingerprint(string name, MethodBase method)
		{
			if (method == null)
			{
				Log.Error($"[Harmony3StrictCompat] {name} no longer exists: its mod updated, remove or redo this fix");
				return;
			}
			ulong hash = IlHash(method);
			if (!Fingerprints.TryGetValue(name, out var expected))
				Log.Warning($"[Harmony3StrictCompat] {name} has no recorded fingerprint; add 0x{hash:X16}");
			else if (hash != expected)
				Log.Error($"[Harmony3StrictCompat] {name} changed (IL 0x{hash:X16}, recorded 0x{expected:X16}): its mod updated, check whether this fix is still needed");
		}

		static ulong IlHash(MethodBase method)
		{
			var il = method.GetMethodBody()?.GetILAsByteArray();
			if (il == null) return 0;
			ulong hash = 14695981039346656037;
			foreach (var b in il) hash = (hash ^ b) * 1099511628211;
			return hash;
		}

		// ---- MVCF per-verb weapon stats ----------------------------------------------------------------
		// MVCF (shipped inside VEF) gives a weapon with 2+ verbs per-verb base accuracy and cooldown: its
		// VerbProperties.GetStatValue returns the verb's accuracyTouch..Long / defaultCooldownTime, and its
		// AdjustedAccuracy / AdjustedCooldown transpilers force that as the stat's base value in combat. Most such
		// weapons (every VWE Coilguns gun, for one) never set those verb fields, so the defaults win: 100% base
		// accuracy at every range and a 1 s cooldown instead of the gun's statBases. Treat a verb field still at its
		// VerbProperties default as unset, so MVCF falls back to the weapon's own stat. A verb that sets its own value
		// keeps it (only a verb deliberately set to exactly 1.0 accuracy / 0 cooldown is lost, which nothing does).
		internal static bool VerbStatUnset(VerbProperties verb, StatDef stat) =>
			stat == StatDefOf.RangedWeapon_Cooldown ? verb.defaultCooldownTime == 0f
			: stat == StatDefOf.AccuracyTouch ? verb.accuracyTouch == 1f
			: stat == StatDefOf.AccuracyShort ? verb.accuracyShort == 1f
			: stat == StatDefOf.AccuracyMedium ? verb.accuracyMedium == 1f
			: stat == StatDefOf.AccuracyLong && verb.accuracyLong == 1f;

		static void MvcfUnsetVerbStat(VerbProperties __0, StatDef __1, ref float? __result)
		{
			if (__result.HasValue && __0 != null && VerbStatUnset(__0, __1)) __result = null;
		}

		// MVCF's info card then lists accuracy/cooldown once more per weapon, computed from the bare def (no
		// quality, no traits), right next to vanilla's real per-thing rows. Drop its row when no verb overrides it.
		static void MvcfHideUnsetStatRow(List<VerbProperties> __0, StatDef __1, ref StatDrawEntry __result)
		{
			if (__result != null && __0 != null && __0.All(v => VerbStatUnset(v, __1))) __result = null;
		}

		// MVCF's Range row averages verb.burstShotCount instead of verb.range (so a 1-shot gun reads "Range 1").
		// Rebuild it like vanilla: verb range x RangedWeapon_RangeMultiplier, one value per distinct verb range.
		// ponytail: no weather range cap (vanilla clamps the row in fog/smoke); add it if the card must match exactly.
		static IEnumerable<StatDrawEntry> MvcfFixRange(IEnumerable<StatDrawEntry> entries, List<VerbProperties> __0,
			StatCategoryDef __1, StatRequest __2)
		{
			string label = ((string)"Range".Translate()).CapitalizeFirst();
			foreach (var entry in entries)
				yield return entry.LabelCap == label ? RangeEntry(__0, __1, __2) ?? entry : entry;
		}

		static StatDrawEntry RangeEntry(List<VerbProperties> verbs, StatCategoryDef category, StatRequest req)
		{
			var ranged = verbs?.Where(v => v != null && v.Ranged).ToList();
			if (ranged == null || ranged.Count == 0) return null;
			float mult = req.HasThing ? req.Thing.GetStatValue(StatDefOf.RangedWeapon_RangeMultiplier) : 1f;
			var sb = new StringBuilder("Stat_Thing_Weapon_Range_Desc".Translate());
			sb.AppendLine().AppendLine();
			foreach (var v in ranged)
				sb.AppendLine($"{(v.label ?? v.verbClass?.Name).CapitalizeFirst()}: {v.range * mult:F0}");
			if (!Mathf.Approximately(mult, 1f))
				sb.AppendLine().AppendLine("Stat_Thing_Weapon_Range_Multiplier".Translate() + ": x" + mult.ToStringPercent());
			string value = string.Join(" / ", ranged.Select(v => (v.range * mult).ToString("F0")).Distinct());
			return new StatDrawEntry(category, "Range".Translate(), value, sb.ToString(), 5390);
		}

		// ---- Basic Traits debug log ----------------------------------------------------------------------
		// TraitHediffApplier.ApplyHediffsToPawnsWithTraits runs every 600 ticks and Log.Message()s two
		// "Processing pawn ..." lines per alive pawn. The author acknowledged it (workshop, 2026-10-24) but has
		// not shipped a fix. Drop just its Log.Message(string) calls; its warnings and the hediff logic stay.
		static readonly MethodInfo LogMessage = AccessTools.Method(typeof(Log), nameof(Log.Message), [typeof(string)]);
		static IEnumerable<CodeInstruction> DropLogMessages(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var ins in instructions)
				yield return ins.Calls(LogMessage) ? new CodeInstruction(OpCodes.Pop).WithLabels(ins.labels).WithBlocks(ins.blocks) : ins;
		}

		// ---- Simple Leadership raid leaders ------------------------------------------------------------
		// New build: its Prefix subtracts each chosen leader's combatPower from parms.points but never checks the
		// result, so a 600-point leader in a 121-point raid leaves -479 points: vanilla logs "Cannot generate pawns"
		// and the leader arrives alone. Drop chosen leaders (last first) and refund their cost until the remaining
		// budget can generate a vanilla group again. __0 = parms, __1 = its __state list of chosen leaders.
		static void SlLeaderBudget(PawnGroupMakerParms __0, ref List<Pawn> __1)
		{
			if (__0?.faction == null || __0.groupKind == null || __1 == null || __1.Count == 0) return;
			float min = __0.faction.def.MinPointsToGeneratePawnGroup(__0.groupKind, __0);
			for (int i = __1.Count - 1; i >= 0 && __0.points < min; i--)
			{
				var leader = __1[i];
				__0.points += leader.kindDef?.combatPower ?? 0f;
				__1.RemoveAt(i);
				Log.Message($"[Harmony3StrictCompat] kept Simple Leadership leader {leader.LabelShort} out of a {__0.points:F0}-point {__0.faction.Name} group (leader costs more than the raid)");
			}
		}

		// Old build: its GeneratePawns postfix adds a faction/base leader to raids on a dice roll that ignores
		// raid points, so a 600-point warcasket leader can join a 121-point first raid. Wrap that postfix: snapshot
		// the group before it runs, then drop any pawn it added whose kind costs more than the whole raid budget.
		// The vanilla result is a lazy iterator; materialise it once here so nothing generates the group twice.
		// Simple Leadership's letter only names the leader if it is in the final pawn list, so it stays accurate.
		static void SlLeaderBefore(ref IEnumerable<Pawn> __0, out HashSet<Pawn> __state)
		{
			var list = __0?.ToList() ?? new List<Pawn>();
			__0 = list;
			__state = new HashSet<Pawn>(list);
		}

		static void SlLeaderAfter(ref IEnumerable<Pawn> __0, PawnGroupMakerParms __1, HashSet<Pawn> __state)
		{
			if (__0 == null || __1 == null || __state == null) return;
			var list = __0.ToList();
			int removed = list.RemoveAll(p => p != null && !__state.Contains(p) && p.kindDef != null && p.kindDef.combatPower > __1.points);
			if (removed == 0) return;
			__0 = list;
			Log.Message($"[Harmony3StrictCompat] kept {removed} Simple Leadership leader(s) out of a {__1.points:F0}-point {__1.faction?.Name} group (leader costs more than the raid)");
		}

		// ---- Needs check before the world exists ---------------------------------------------------------
		// Game.LoadGame scribes the quest manager before the world. A pawn deep-saved inside a QuestPart (e.g. the two
		// VGE_Astropede in a ShipChunk_Mech quest) is constructed then; Pawn_NeedsTracker's ctor runs
		// AddOrRemoveNeedsAsAppropriate, whose MechEnergy branch reads Faction.OfPlayer -> Find.FactionManager ->
		// Find.World.factionManager with Find.World still null ("Error while determining if X should have Need
		// MechEnergy"). The ctor's result is thrown away anyway: ExposeData loads the saved needs list right after.
		// Give no needs until a world (or the world being generated) exists; every later call sees one.
		static bool NoNeedsBeforeWorld(ref bool __result)
		{
			if (Current.Game?.World != null || Current.CreatingWorld != null) return true;
			__result = false;
			return false;
		}

		// ---- Trade ship pawns duplicated into world pawns ------------------------------------------------
		// Seen 2026-11-01, three times in one session: a slave for sale aboard an orbital trade ship is also in
		// WorldPawns (mothballed). Both are deep-saved, so every load creates two objects with the same thingID
		// ("Exception registering ... already been added" for the pawn, its gear, hediffs, genes, MVCF verbs).
		// The ship's copy is the one trade uses; cross-refs resolved to whichever loaded first. Source mod unknown
		// yet: this drops the world copy after load and logs the ship, so the pattern stays visible.
		static void DropWorldCopiesOfShipPawns()
		{
			var world = Find.WorldPawns;
			foreach (var map in Find.Maps)
				foreach (var ship in map.passingShipManager.passingShips)
				{
					if (ship is not TradeShip trader) continue;
					foreach (var pawn in trader.Goods.OfType<Pawn>().ToList())
					{
						var worldCopy = world.AllPawnsAliveOrDead.FirstOrDefault(p => p != pawn && p.thingIDNumber == pawn.thingIDNumber);
						if (worldCopy == null) continue;
						world.RemovePawn(worldCopy);
						Log.Warning($"[Harmony3StrictCompat] {pawn.LabelShort} ({pawn.ThingID}) was both aboard {trader.name} and a world pawn; dropped the world copy");
					}
				}
		}

		// ---- Triumphant Research: empty carousel --------------------------------------------------------
		// Its FinishProject prefix opens Window_ResearchComplete whenever project.UnlockedDefs is non-empty, but the
		// window's ctor keeps only unlock groups whose every prerequisite is finished. A thing that several mods'
		// recipes make under different research (MSS Fun and Tweaks' neutroamine recipe, 2026-11-01) lands in a group
		// with unfinished prerequisites, so the list ends up empty and DrawBottom indexes unlockedDefs[0]: red
		// error every frame and a blank window. Fall back to the unfiltered list the opener already checked.
		static void TrFillEmptyCarousel(ResearchProjectDef __0, List<Def> ___unlockedDefs)
		{
			if (___unlockedDefs.Count == 0 && __0?.UnlockedDefs != null)
				___unlockedDefs.AddRange(__0.UnlockedDefs);
		}

		// ---- Sensible Bed Ownership reservation prefix ---------------------------------------------------
		// SBO's ReservationManager.Reserve prefix reads target.Thing.Map.reservationManager without a null
		// check; a bed that was just despawned (e.g. replaced via Replace Stuff) has no map, so it throws and
		// the pawn's tick errors out. For an unspawned bed, skip SBO's prefix and let vanilla Reserve decide.
		// __1 is SBO's "target" parameter; __result is SBO's own return value (true = run vanilla Reserve).
		static bool SboUnspawnedBedPrefix(LocalTargetInfo __1, ref bool __result)
		{
			if (__1.HasThing && __1.Thing is Building_Bed && __1.Thing.Map == null)
			{
				__result = true;
				return false;
			}
			return true;
		}

		// ---- Harmony 3 __instance type validation -------------------------------------------------------
		// Harmony 3 rejects a patch whose __instance parameter type doesn't match the patched class, aborting
		// the owning mod's whole PatchAll. These patches never dereference __instance beyond a base class both
		// types share, so Harmony 3's own uncheckedReferenceBinding restores the 1.6 behaviour safely.
		// key: "<assembly>|<declaring type name>|<method name>"
		static readonly HashSet<string> Unchecked = new()
		{
			"Skilled Refueling|HarmonyPatches|JobDriverRefuelAtomic_MakeNewToils_Postfix", // only JobDriver.pawn/job
		};

		static string Key(MethodInfo m) => $"{m.Module.Assembly.GetName().Name}|{m.DeclaringType?.Name}|{m.Name}";

		static void PatchCtorPrefix(HarmonyMethod method)
		{
			if (method?.method is null) return;
			var key = Key(method.method);
			if (Unchecked.Contains(key))
			{
				method.uncheckedReferenceBinding = true;
				Log.Message($"[Harmony3StrictCompat] unchecked binding for {key}");
			}
		}

		// ---- Too Many Mods xpath case -------------------------------------------------------------------
		// TMM (wara.toomanymods, last updated 2026-05) writes "Defs/thingDef[" in 22 xpaths, all aimed at
		// Rimsenal: Murder diversified items. XPath is case-sensitive, so they match nothing; required ones fail
		// with red errors. No def element is named "thingDef", so correcting the case only makes such a patch do
		// what its author meant.
		const string BrokenXpathPrefix = "Defs/thingDef[", FixedXpathPrefix = "Defs/ThingDef[";
		static int xpathFixCount;
		static readonly AccessTools.FieldRef<PatchOperationPathed, string> xpathRef =
			AccessTools.FieldRefAccess<PatchOperationPathed, string>("xpath");
		// ---- Exact xpath typos ----------------------------------------------------------------------------
		//  - MSS Tweaks and Fun (3379574408) VFE Pirates compat, MSSFP_SuperiorBuckoneerCulture.xml: meant to swap
		//    the VFEP_Mercenaries faction's Kriminul culture for MSS_BuckoneerCulture, but wrote the faction name as a
		//    child node of VFEP_Junkers, so the required replace fails with a red error.
		static readonly Dictionary<string, string> XpathTypos = new()
		{
			["Defs/FactionDef[defName=\"VFEP_Junkers\"]/VFEP_Mercenaries/li[text()=\"Kriminul\"]"] =
				"Defs/FactionDef[defName=\"VFEP_Mercenaries\"]/allowedCultures/li[text()=\"Kriminul\"]",
		};
		static void FixXpathPrefix(PatchOperation __instance)
		{
			if (__instance is not PatchOperationPathed pathed) return;
			ref var xpath = ref xpathRef(pathed);
			if (xpath == null) return;
			if (XpathTypos.TryGetValue(xpath.Trim(), out var fixedXpath))
			{
				Log.Message($"[Harmony3StrictCompat] fixed xpath typo: {fixedXpath}");
				xpath = fixedXpath;
				return;
			}
			var trimmed = xpath.TrimStart('/', ' ', '\t', '\r', '\n');
			if (!trimmed.StartsWith(BrokenXpathPrefix, StringComparison.Ordinal)) return;
			xpath = FixedXpathPrefix + trimmed.Substring(BrokenXpathPrefix.Length);
			if (++xpathFixCount <= 3 || xpathFixCount % 10 == 0)
				Log.Message($"[Harmony3StrictCompat] fixed xpath case ({xpathFixCount}): {xpath}");
		}

		// ---- Conditional patches skipped entirely (reported as succeeded) --------------------------------
	//  - Rimsenal Faction Pack - Spacer (3086137468) adds CompProperties_Styleable to the mech commander helmet,
	//    Dresser and EndTable whenever the def has a <comps> node, without checking for an existing style comp.
	//    1.7's ApparelBase already supplies one for the helmet, and every styling mod (VIE Memes, Alpha Memes,
	//    Sophian Style, Medieval 2, Dune) guards its own furniture add. Two comps save sourcePrecept/styleDef twice
	//    ("Tried to register the same load ID twice" on every load; 7 EndTable + 6 Dresser, 2026-10-29).
	//    Key: conditional xpath -> substring the patch's sourceFile must contain. The other mods' guarded adds use
	//    the very same inner xpath, so the file check is what keeps them alive (v20 emptied them all: no comp,
	//    styles lost).
	static readonly Dictionary<string, string> SkippedConditionals = new()
	{
		["/Defs/ThingDef[defName = \"Apparel_ArmorHelmetMechCommander\"]/comps"] = "3086137468",
		["/Defs/ThingDef[defName = \"Dresser\"]/comps"] = "3086137468",
		["/Defs/ThingDef[defName = \"EndTable\"]/comps"] = "3086137468",
	};
	// The operation still runs normally (never skip Apply itself: XML Extensions tracks every Apply with a
		// prefix/finalizer pair, and an early return desyncs its error stack so it then reports every optional
		// patch failure). Emptying both branches makes it a no-op; the mod's <success>Always</success> keeps it quiet.
		static readonly AccessTools.FieldRef<PatchOperationConditional, PatchOperation> matchRef =
			AccessTools.FieldRefAccess<PatchOperationConditional, PatchOperation>("match");
		static readonly AccessTools.FieldRef<PatchOperationConditional, PatchOperation> nomatchRef =
			AccessTools.FieldRefAccess<PatchOperationConditional, PatchOperation>("nomatch");
		static void SkipConditionalPrefix(PatchOperation __instance)
		{
			if (__instance is not PatchOperationConditional cond) return;
			var xpath = xpathRef(cond);
			if (xpath == null || !SkippedConditionals.TryGetValue(xpath.Trim(), out var fileMarker)) return;
			if (cond.sourceFile == null || !cond.sourceFile.Contains(fileMarker)) return;
			if (matchRef(cond) == null && nomatchRef(cond) == null) return;
			matchRef(cond) = null;
			nomatchRef(cond) = null;
			Log.Message($"[Harmony3StrictCompat] emptied duplicate-comp patch: {xpath.Trim()} ({cond.sourceFile})");
		}
	}

	// ---- MVCF per-verb stats: startup report ----------------------------------------------------------------
	// Lists the weapons the MvcfUnsetVerbStat fallback changes (2+ verbs, some ranged verb leaves accuracy or
	// cooldown at its default), so the fix is visibly active and its reach is known.
	[StaticConstructorOnStartup]
	static class ReportMvcfVerbStats
	{
		static readonly StatDef[] Stats = [StatDefOf.RangedWeapon_Cooldown, StatDefOf.AccuracyTouch,
			StatDefOf.AccuracyShort, StatDefOf.AccuracyMedium, StatDefOf.AccuracyLong];

		static ReportMvcfVerbStats()
		{
			if (AccessTools.TypeByName("MVCF.Utilities.VerbStatsUtility") == null) return;
			var names = DefDatabase<ThingDef>.AllDefs
				.Where(d => d.IsWeapon && d.Verbs != null && d.Verbs.Count > 1
					&& d.Verbs.Any(v => v.Ranged && Stats.Any(s => Harmony3StrictCompatMod.VerbStatUnset(v, s))))
				.Select(d => d.defName).ToList();
			if (names.Count > 0)
				Log.Message($"[Harmony3StrictCompat] MVCF multi-verb weapons now using their own accuracy/cooldown: {names.Count} ({string.Join(", ", names.Take(12))}{(names.Count > 12 ? ", ..." : "")})");
		}
	}

	// ---- VEF verb range factor without shared-state mutation ---------------------------------------------
	// VEF scales ranged verbs by the pawn's VEF_VerbRangeFactor with a Prefix on VerbProperties.AdjustedRange that
	// multiplies the def-shared verbProps.range field in place and a Finalizer that restores it. Two overlapping
	// calls (1.7 runs more off the main thread) leave the field stuck at the multiplied value: a unique gauss
	// revolver's low-power verb sat at 29.85 (19.9 x 1.5) on 2026-10-31, for every copy of that def, until restart.
	// Replace the pair with a postfix that scales the return value; same number, nothing shared written.
	// Runs after loading: VEF patches in its Mod constructor, which comes later in load order than this mod.
	[StaticConstructorOnStartup]
	static class VefRangeFactor
	{
		static StatDef rangeFactor;

		static VefRangeFactor()
		{
			LongEventHandler.ExecuteWhenFinished(Apply);
		}

		static void Apply()
		{
			const string cls = "VEF.Weapons.VerbUtility+VerbProperties_AdjustedRange_Patch";
			var prefix = Harmony3StrictCompatMod.Target(cls + ":Prefix");
			var finalizer = Harmony3StrictCompatMod.Target(cls + ":Finalizer");
			rangeFactor = DefDatabase<StatDef>.GetNamedSilentFail("VEF_VerbRangeFactor");
			if (prefix == null || finalizer == null || rangeFactor == null) return;
			var original = AccessTools.Method(typeof(VerbProperties), nameof(VerbProperties.AdjustedRange));
			var harmony = new Harmony("sammael.harmony3strictcompat");
			harmony.Unpatch(original, prefix);
			harmony.Unpatch(original, finalizer);
			harmony.Patch(original, postfix: new HarmonyMethod(typeof(VefRangeFactor), nameof(ScaleResult)));
			Log.Message("[Harmony3StrictCompat] VEF verb range factor now applied to the result instead of the shared verb def");
		}

		// Mirrors VEF: only ranged verbs, only pawn attackers; the weather cap vanilla applied still bounds it unless
		// the weapon is a unique one that ignores accuracy maluses (the same exemption vanilla uses for the cap).
		static void ScaleResult(VerbProperties __instance, Verb ownerVerb, Thing attacker, ref float __result)
		{
			if (!__instance.Ranged || attacker is not Pawn pawn) return;
			float factor;
			try { factor = pawn.GetStatValue(rangeFactor, true, 60); }
			catch { return; }
			if (Mathf.Approximately(factor, 1f)) return;
			__result *= factor;
			if (ownerVerb?.EquipmentSource != null && ownerVerb.EquipmentSource.TryGetComp<CompUniqueWeapon>(out var unique) && unique.IgnoreAccuracyMaluses) return;
			var map = attacker.MapHeld;
			if (map != null && map.weatherManager.CurWeatherMaxRangeCap >= 0f)
				__result = Mathf.Min(__result, map.weatherManager.CurWeatherMaxRangeCap);
		}
	}

	// ---- Duplicate biome wild plants ----------------------------------------------------------------------
	// ReGrowth 2 and its plant patches can list the same plant twice in a biome's wildPlants. 1.7's
	// BiomeDef.CachePlantCommonalitiesIfShould does Dictionary.Add per record, so the duplicate throws during
	// world generation and aborts the whole Landmarks and Mutators steps. Keep the first record of each plant.
	[StaticConstructorOnStartup]
	static class DedupeBiomeWildPlants
	{
		static readonly AccessTools.FieldRef<BiomeDef, List<BiomePlantRecord>> wildPlantsRef =
			AccessTools.FieldRefAccess<BiomeDef, List<BiomePlantRecord>>("wildPlants");

		static DedupeBiomeWildPlants()
		{
			foreach (var biome in DefDatabase<BiomeDef>.AllDefs)
			{
				var list = wildPlantsRef(biome);
				if (list == null) continue;
				var seen = new HashSet<ThingDef>();
				int removed = list.RemoveAll(r => r?.plant != null && !seen.Add(r.plant));
				if (removed == 0) continue;
				// Drop anything cached from the broken list; the getters rebuild it.
				AccessTools.Field(typeof(BiomeDef), "cachedPlantCommonalities").SetValue(biome, null);
				AccessTools.Field(typeof(BiomeDef), "cachedWildPlants").SetValue(biome, null);
				Log.Message($"[Harmony3StrictCompat] {biome.defName}: removed {removed} duplicate wild plant record(s)");
			}
		}
	}

	// ---- VRE Sanguophage: animalfeeders never drink animal hemogen packs on their own -----------------------
	// VRE gives corpsefeeders their own job giver (JobGiver_GetCorpseHemogen, drinks VRE_HemogenPack_Corpse), but
	// nothing does the same for VRE_HemogenPack_Animal: vanilla JobGiver_GetHemogen only looks for HemogenPack. So a
	// strigoi carrying animal packs lets hemogen hit 0. When the vanilla giver finds nothing (no prisoner to feed on, no
	// regular pack), an animalfeeder drinks animal packs the vanilla way: carried, then inventory, then the nearest
	// allowed one on the map, as many as fit below max hemogen. Regular packs and prisoners still come first.
	// VRE's corpse job giver is fingerprinted: if VRE reworks feeding, recheck whether this is still needed.
	[StaticConstructorOnStartup]
	static class AnimalfeederHemogenPacks
	{
		static readonly GeneDef animalFeeder = DefDatabase<GeneDef>.GetNamedSilentFail("VRE_AnimalFeeder");
		static readonly ThingDef animalPack = DefDatabase<ThingDef>.GetNamedSilentFail("VRE_HemogenPack_Animal");
		static readonly float packGain = animalPack?.ingestible?.outcomeDoers?.OfType<IngestionOutcomeDoer_OffsetHemogen>().FirstOrDefault()?.offset ?? 0f;

		static AnimalfeederHemogenPacks()
		{
			if (animalFeeder == null || animalPack == null || packGain <= 0f) return;
			Harmony3StrictCompatMod.Target("VanillaRacesExpandedSanguophage.JobGiver_GetCorpseHemogen:TryGiveJob");
			new Harmony("sammael.harmony3strictcompat").Patch(AccessTools.Method(typeof(JobGiver_GetHemogen), "TryGiveJob"),
				postfix: new HarmonyMethod(typeof(AnimalfeederHemogenPacks), nameof(DrinkAnimalPack)));
		}

		static void DrinkAnimalPack(Pawn pawn, ref Job __result)
		{
			if (__result != null || pawn.genes == null || !pawn.genes.HasActiveGene(animalFeeder)) return;
			var gene = pawn.genes.GetFirstGeneOfType<Gene_Hemogen>();
			if (gene == null || !gene.hemogenPacksAllowed || !gene.ShouldConsumeHemogenNow()) return;
			int want = Mathf.FloorToInt((gene.Max - gene.Value) / packGain);
			if (want <= 0) return;
			var pack = FindPack(pawn);
			if (pack == null) return;
			__result = JobMaker.MakeJob(JobDefOf.Ingest, pack);
			__result.count = Mathf.Min(pack.stackCount, want);
			__result.ingestTotalCount = true;
		}

		static Thing FindPack(Pawn pawn)
		{
			var carried = pawn.carryTracker.CarriedThing;
			if (carried != null && carried.def == animalPack) return carried;
			foreach (var t in pawn.inventory.innerContainer)
				if (t.def == animalPack) return t;
			return GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.listerThings.ThingsOfDef(animalPack),
				PathEndMode.OnCell, TraverseParms.For(pawn), 9999f, t => pawn.CanReserve(t) && !t.IsForbidden(pawn));
		}
	}
}
