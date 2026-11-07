using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
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
			harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
				postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RepairMissingLeaders)));
			var rpDismiss = Target("RimPacts.WorldComponent_RimPacts:DismissExilePawn");
			if (rpDismiss != null)
				harmony.Patch(rpDismiss, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpReplaceDismissedLeader)));
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
			guestBedField = AccessTools.Field(AccessTools.TypeByName("Hospitality.CompGuest"), "bed");
			var hospSleep = AccessTools.Method("Hospitality.JobGiver_Sleep:TryIssueJobPackage");
			if (guestBedField != null && hospSleep != null)
			{
				harmony.Patch(hospSleep, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SleepProbeStart)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SleepProbeEnd)));
				var isValidBed = AccessTools.Method(typeof(RestUtility), nameof(RestUtility.IsValidBedFor));
				harmony.Patch(isValidBed, postfix: new HarmonyMethod(AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(SleepProbeVanillaResult))) { priority = Priority.First });
				harmony.Patch(isValidBed, postfix: new HarmonyMethod(AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(SleepProbeFinalResult))) { priority = Priority.Last });
			}
			var epDispatch = Target("FinitePopulationVeterans.WorldPopulationManager:IsPawnAvailableForDispatch");
			if (epDispatch != null)
				harmony.Patch(epDispatch, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(EpNoLeaderVeterans)));
			var hospRegear = Target("Hospitality.Utilities.SpawnGroupUtility:GenerateNewGearFor");
			if (hospRegear != null)
				harmony.Patch(hospRegear, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HospKeepWarcasketGear)));
			var hcSlots = Target("HospitalityCasino.JobGiver_PlaySlotMachines:TryGiveJob");
			if (hcSlots != null)
				harmony.Patch(hcSlots, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HcSlotsOnlyIdleVisitors)));
			var hcInsert = Target("HospitalityCasino.VendingMachineJobHelper:InsertCoin");
			hcCompType = AccessTools.TypeByName("HospitalityCasino.CompVendingMachine");
			if (hcInsert != null && hcCompType != null)
			{
				hcInsertCoin = (Func<Pawn, Thing, bool>)hcInsert.CreateDelegate(typeof(Func<Pawn, Thing, bool>));
				harmony.Patch(AccessTools.Method(typeof(JoyUtility), nameof(JoyUtility.JoyTickCheckEnd)),
					prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HcChargeOtherTables)));
			}
			var hcInspect = Target("HospitalityCasino.CompVendingMachine:CompInspectStringExtra");
			if (hcInspect != null && hcCompType != null)
			{
				hcPricing = AccessTools.FieldRefAccess<int>(hcCompType, "pricing");
				hcItemPrice = AccessTools.Method(hcCompType, "GetSingleItemPrice");
				harmony.Patch(hcInspect, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HcPricingLine)));
			}
			var hospPocket = Target("Hospitality.Utilities.ItemUtility:PocketHeadgear");
			if (hospPocket != null)
				harmony.Patch(hospPocket, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HospKeepCasketHelmet)));
			var epIssue = Target("FinitePopulationVeterans.WorldPopulationManager:TryGetVeteran");
			if (epIssue != null)
				harmony.Patch(epIssue, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(EpIssuedLeavesWorld)));
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
			foreach (var msfSlider in new[] { "ModSettingsFramework.PatchOperationSliderFloat:ApplyWorker", "ModSettingsFramework.PatchOperationSliderInt:ApplyWorker" })
				if (Target(msfSlider) is { } msfApply)
					harmony.Patch(msfApply, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(MsfSliderAllNodes)));
			var lpInfo = Target("ilyvion.LoadingProgress.StartupImpact.ModInfoList:GetModInfoFor");
			if (lpInfo != null)
				harmony.Patch(lpInfo, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LpLockEnter)),
					finalizer: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LpLockExit)));
			var bwmFilters = Target("ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawFilters");
			if (bwmFilters != null)
				harmony.Patch(bwmFilters, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(BwmClearStyleButton)));
			var bwmNav = Target("ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawWorkTableNavigation");
			if (bwmNav != null)
				harmony.Patch(bwmNav, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(BwmNavArrowsUp)));
			var lcBwm = Target("Inventory.BWM_DoWindowContentsPatch_Patch:Transpiler");
			lcCountAway = Target("Inventory.BWM_DoWindowContentsPatch_Patch:ShouldCountAway");
			if (lcBwm != null && lcCountAway != null && bwmFilters != null)
				harmony.Patch(lcBwm, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LcBwmCountAway)));
			var bwUpper = Target("Busywork.Patch_ColonistBarColonistDrawer_DrawColonist:Postfix");
			var ltoDraw = Target("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:DrawColonist");
			if (bwUpper != null && ltoDraw != null)
			{
				bwUpperMark = (Action<ColonistBarColonistDrawer, Rect, Pawn>)bwUpper.CreateDelegate(typeof(Action<ColonistBarColonistDrawer, Rect, Pawn>));
				harmony.Patch(ltoDraw, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LtoBusyworkUpperMark)));
			}
			// Vanilla bug, found through Modern Needs Tab; its prefix is fingerprinted so an MNT update gets re-checked.
			Target("ModernNeedsTab.Patch_Pawn_JobTracker_EndCurrentJob:Prefix");
			harmony.Patch(AccessTools.PropertyGetter(typeof(MinifiedThing), nameof(MinifiedThing.LabelNoCount)),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(EmptyMinifiedLabel)));
			foreach (var csClean in new[] { "CommonSense.Utility+<>c__DisplayClass21_0:<CleanFilthToil>b__0", "CommonSense.Utility+<>c__DisplayClass21_0:<CleanFilthToil>b__1" })
				if (Target(csClean) is { } csToil)
					harmony.Patch(csToil, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CsFilthGone)));
			var prClean = Target("PrisonerRealism.JobGiver_CleanCell:ShouldClean");
			var carArea = Target("CleaningAreaRelaunched.CleaningAreaRelaunched_Util:GetCleanArea");
			if (prClean != null && carArea != null)
			{
				carCleanArea = (Func<Map, Area>)carArea.CreateDelegate(typeof(Func<Map, Area>));
				harmony.Patch(prClean, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PrCleanOnlyInCleanArea)));
			}
			var hfmHunt = Target("aRandomKiwi.HFM.JobGiver_AnimalHunt:TryGiveJob");
			if (hfmHunt != null)
				harmony.Patch(hfmHunt, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HfmPlayerAnimalsOnly)));
			var hdInit = Target("HaulersDream.CompositableLoadoutsCompat:InitKeepApi");
			var hdWanted = Target("HaulersDream.CompositableLoadoutsCompat:LoadoutWantedUnits");
			hdItemDefs = AccessTools.PropertyGetter(AccessTools.TypeByName("Inventory.Item"), "Defs");
			if (hdInit != null && hdWanted != null && hdItemDefs != null)
			{
				harmony.Patch(hdInit, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdBridgeKeepApi)));
				harmony.Patch(hdWanted, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdWantedUnits)));
			}
			var dsStructure = Target("Merthsoft.DesignatorShapes.Shapes.FloodFill:IsStructure");
			if (dsStructure != null)
				harmony.Patch(dsStructure, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(DsDoorIsStructure)));
			var flAnchored = Target("Flourish.Everyday.JobAnimationWorker_Anchored:Evaluate");
			var flDefType = AccessTools.TypeByName("Flourish.Everyday.JobAnimationDef");
			flAnchor = flDefType == null ? null : AccessTools.Field(flDefType, "anchor");
			flAnchorFacing = flDefType == null ? null : AccessTools.Field(flDefType, "anchorFacing");
			if (flAnchored != null && flAnchor != null && flAnchorFacing != null)
				harmony.Patch(flAnchored, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(FlMirrorAnchorTranspiler)));
			var flDataFor = Target("YayoAnimation.HarmonyPatches.PawnPatches.ParallelGetPreRenderResultsPatch:DataFor");
			var flDataType = AccessTools.TypeByName("YayoAnimation.Data.PawnDrawData");
			flPddDef = flDataType == null ? null : AccessTools.Field(flDataType, "def");
			flPddTimelineStart = flDataType == null ? null : AccessTools.Field(flDataType, "timelineStart");
			flDefWorkerClass = flDefType == null ? null : AccessTools.Field(flDefType, "workerClass");
			flAnchoredWorker = flAnchored?.DeclaringType;
			if (flDataFor != null && flPddDef != null && flPddTimelineStart != null && flDefWorkerClass != null && flAnchoredWorker != null)
			{
				flDataForCall = (Func<Pawn, object>)flDataFor.CreateDelegate(typeof(Func<Pawn, object>));
				harmony.Patch(AccessTools.Method(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(FlDrawAnchorTargetOverPawn)));
			}
			var vfetButton = Target("VFETribals.FactionUIUtility_DoWindowContents_Patch:Postfix");
			var uirThemed = Target("UIRethemed.Factions.FactionsTab:Themed");
			var uirButton = Target("UIRethemed.Core.CoreWidgets:Button");
			var uirIcon = Target("UIRethemed.Core.CoreWidgets:Icon");
			var uirBlue = AccessTools.PropertyGetter(AccessTools.TypeByName("UIRethemed.Core.Theme"), "HighlightBlue");
			vfetWindow = AccessTools.TypeByName("VFETribals.Window_CustomizeCornerstones");
			if (vfetButton != null && uirThemed != null && uirButton != null && uirIcon != null && uirBlue != null && vfetWindow != null)
			{
				uirFactionsThemed = (Func<Window, bool>)uirThemed.CreateDelegate(typeof(Func<Window, bool>));
				uirThemedButton = (Func<Rect, string, Texture, Color?, bool, string, GameFont, string, bool, bool, bool>)uirButton.CreateDelegate(
					typeof(Func<Rect, string, Texture, Color?, bool, string, GameFont, string, bool, bool, bool>));
				uirIconFor = (Func<string, Texture2D>)uirIcon.CreateDelegate(typeof(Func<string, Texture2D>));
				uirBlueColor = (Func<Color>)uirBlue.CreateDelegate(typeof(Func<Color>));
				harmony.Patch(vfetButton, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(VfetThemedCornerstones)));
				if (Target("UIRethemed.Core.CoreWidgets:ButtonWidth") is { } uirWidth)
					uirButtonWidth = (Func<string, bool, GameFont, float>)uirWidth.CreateDelegate(typeof(Func<string, bool, GameFont, float>));
				Target("UIRethemed.Factions.ModCompat:get_HeaderButtonsWidth");
				uirHeaderWidthCache = AccessTools.Field(AccessTools.TypeByName("UIRethemed.Factions.ModCompat"), "headerButtonsWidth");
				uirSpaceDefType = AccessTools.TypeByName("UIRethemed.Factions.TitleBarSpaceDef");
				uirSpaceWidth = uirSpaceDefType == null ? null : AccessTools.Field(uirSpaceDefType, "width");
			}
			var uirFace = Target("UIRethemed.MainBar.MenuWindow:Face");
			if (uirIconFor == null && AccessTools.Method("UIRethemed.Core.CoreWidgets:Icon") is { } iconFor)
				uirIconFor = (Func<string, Texture2D>)iconFor.CreateDelegate(typeof(Func<string, Texture2D>));
			if (uirFace != null && uirIconFor != null)
				harmony.Patch(uirFace, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirMenuModButtonIcon)));
			var uirRowDraw = Target("UIRethemed.Notifications.StatusRow:Draw");
			var uirRowWidth = Target("UIRethemed.Notifications.StatusRow:WidthFor");
			var uirColorOf = Target("UIRethemed.Notifications.NotificationStyles:ColorOf");
			var uirColumnX = Target("UIRethemed.Notifications.Placement:ColumnX");
			var uirPosition = Target("UIRethemed.Notifications.Placement:get_Position");
			var uirHover = Target("UIRethemed.Notifications.Motion:Hover");
			Target("UIRethemed.Notifications.Patch_Messages_MessagesDoGUI:Transpiler");
			var uirStyle = AccessTools.TypeByName("UIRethemed.Notifications.NotificationStyle");
			var uirWidgets = AccessTools.TypeByName("UIRethemed.Notifications.NotificationsWidgets");
			if (uirRowDraw != null && uirRowWidth != null && uirColorOf != null && uirColumnX != null && uirPosition != null && uirHover != null
				&& uirStyle != null && uirWidgets != null)
			{
				uirRowDrawCall = uirRowDraw;
				uirRowWidthCall = (Func<string, float>)uirRowWidth.CreateDelegate(typeof(Func<string, float>));
				uirColumnXCall = (Func<float, float>)uirColumnX.CreateDelegate(typeof(Func<float, float>));
				uirHoverCall = (Func<Rect, int, bool, float>)uirHover.CreateDelegate(typeof(Func<Rect, int, bool, float>));
				uirPositionCall = uirPosition;
				uirBottomRight = (int)Enum.Parse(uirPosition.ReturnType, "BottomRight");
				if (Target("UIRethemed.Notifications.HoverCard:Region") is { } uirHoverCard)
					uirHoverRegion = (Action<Rect, string, string, string, Color?>)uirHoverCard.CreateDelegate(typeof(Action<Rect, string, string, string, Color?>));
				uirPlacedDraw = Target("UIRethemed.Notifications.HoverCard:DrawPlaced");
				var uirPlacedSize = Target("UIRethemed.Notifications.HoverCard:PlacedSize");
				var uirBeside = Target("UIRethemed.Notifications.Placement:BesideX");
				var uirPartType = AccessTools.TypeByName("UIRethemed.Notifications.NotificationPart");
				if (uirPlacedDraw != null && uirPlacedSize != null && uirBeside != null && uirPartType != null)
				{
					uirPlacedSizeCall = (Func<string, string, Vector2>)uirPlacedSize.CreateDelegate(typeof(Func<string, string, Vector2>));
					uirBesideCall = (Func<Rect, float, float, float>)uirBeside.CreateDelegate(typeof(Func<Rect, float, float, float>));
					uirAlertsPart = Enum.Parse(uirPartType, "Alerts");
				}
				else uirPlacedDraw = null;
				uirStyleType = uirStyle; uirWidgetsType = uirWidgets; uirColorOfCall = uirColorOf;
				harmony.Patch(AccessTools.Method(typeof(Messages), nameof(Messages.MessagesDoGUI)),
					prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirMessagesInStack)));
				harmony.Patch(AccessTools.PropertyGetter(typeof(Message), "TimeLeft"),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirLongerMessages)));
			}
			var acInject = Target("AutoCastSpecialistCommands.AutoCastToilInjector:Inject");
			Target("AutoCastSpecialistCommands.Patch_JobDriver_Mine_MakeNewToils:Postfix");
			acSettings = AccessTools.Field(AccessTools.TypeByName("AutoCastSpecialistCommands.AutoCastSpecialistCommands"), "settings");
			acMiningOn = AccessTools.Field(AccessTools.TypeByName("AutoCastSpecialistCommands.AutoCastSpecialistCommandsSettings"), "enableAutoCastMiningCommand");
			if (acInject != null && acSettings != null && acMiningOn != null)
			{
				acInjectCall = (AcInject)acInject.CreateDelegate(typeof(AcInject));
				foreach (var driver in new[] { typeof(JobDriver_Mine), typeof(JobDriver_ConstructFinishFrame) })
					harmony.Patch(AccessTools.DeclaredMethod(driver, "MakeNewToils"),
						postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AcStonecallWhenWorking)));
				Target("AutoCastSpecialistCommands.Patch_Pawn_GetGizmos:GizmoOnGUIPrefix");
				Target("AutoCastSpecialistCommands.Patch_Pawn_GetGizmos:GizmoOnGUIPostfix");
				acAutoTex = AccessTools.Field(AccessTools.TypeByName("AutoCastSpecialistCommands.Patch_Pawn_GetGizmos"), "AutoCastTex");
				var gizmoGui = AccessTools.Method(typeof(Command), "GizmoOnGUIInt");
				harmony.Patch(gizmoGui, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AcStonecallRightClick)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AcStonecallAutoIcon)));
				harmony.Patch(AccessTools.Method(typeof(Command_Ability), nameof(Command_Ability.ProcessInput)),
					prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AcStonecallNoRightClickCast)));
			}
		}

		// ---- Auto-Cast Specialist Commands + Det's Xenotypes - Stoneborn: auto-cast Stonecall when mining or building ----
		// Auto-Cast only knows the vanilla/SO/VME command AbilityDefs by name. Stonecall (DV_Stonecall, Stoneborn gene
		// ability) is a self-only +30% mining/construction speed buff, so on a mine job hand it to Auto-Cast's own
		// public toil injector, gated by its "Auto-Cast Mining Command" setting (shared, not a separate switch). The same goes for finishing a construction
		// frame (the job ConstructionSpeed drives). The injector casts on the pawn itself
		// when the ability is off cooldown.
		delegate void AcInject(JobDriver driver, ref IEnumerable<Toil> result, AbilityDef def, bool enabled);
		static AcInject acInjectCall;
		static FieldInfo acSettings, acMiningOn;
		static AbilityDef acStonecall;
		static bool acStonecallLooked;

		static AbilityDef Stonecall
		{
			get
			{
				if (!acStonecallLooked) { acStonecall = DefDatabase<AbilityDef>.GetNamedSilentFail("DV_Stonecall"); acStonecallLooked = true; }
				return acStonecall;
			}
		}

		static void AcStonecallWhenWorking(JobDriver __instance, ref IEnumerable<Toil> __result)
		{
			var settings = acSettings.GetValue(null);
			if (Stonecall != null && settings != null) acInjectCall(__instance, ref __result, Stonecall, (bool)acMiningOn.GetValue(settings));
		}

		// Gizmo, as Auto-Cast does for its own commands (its checks list only those defs): right-click on the Stonecall
		// gizmo toggles the shared Mining Command setting instead of casting, and the setting's state shows as
		// Auto-Cast's own check icon in the gizmo's top-right corner.
		static FieldInfo acAutoTex;

		static bool IsStonecall(Command command) => command is Command_Ability ca && ca.Ability?.def != null && ca.Ability.def == Stonecall;

		static bool AcStonecallRightClick(Command __instance, Rect butRect, ref GizmoResult __result)
		{
			var ev = Event.current;
			if (ev == null || ev.button != 1 || ev.type != EventType.MouseDown || !Mouse.IsOver(butRect) || !IsStonecall(__instance)) return true;
			var settings = acSettings.GetValue(null);
			if (settings == null) return true;
			acMiningOn.SetValue(settings, !(bool)acMiningOn.GetValue(settings));
			Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_Tiny);
			__result = new GizmoResult(GizmoState.Clear);
			return false;
		}

		static void AcStonecallAutoIcon(Command __instance, Rect butRect, GizmoRenderParms parms)
		{
			if (!IsStonecall(__instance)) return;
			var settings = acSettings.GetValue(null);
			if (settings == null || !(bool)acMiningOn.GetValue(settings) || acAutoTex?.GetValue(null) is not Texture2D tex) return;
			float size = parms.shrunk ? 12f : 24f;
			GUI.DrawTexture(new Rect(butRect.xMax - size, butRect.y, size, size), tex);
		}

		static bool AcStonecallNoRightClickCast(Command_Ability __instance, Event ev)
			=> !((ev?.button == 1 || Event.current?.button == 1) && __instance.Ability?.def != null && __instance.Ability.def == Stonecall);

		// ---- VFE Tribals + UI Rethemed - Factions: Cornerstones button in the theme's style ----
		// VFE Tribals draws its Cornerstones button from a postfix on FactionUIUtility.DoWindowContents as a vanilla
		// Widgets.ButtonText at (0, 10) of the Factions window (135 further right with VFE Classical, whose own button
		// takes that spot). UIR Factions keeps a 135 px gap for it in its title bar (TitleBarSpaceDef) but leaves the
		// vanilla look. While the tab is themed, draw it with UIR's own action button (blue, raised, as its faction
		// action buttons) centred in the 40 px title bar that starts 6 px into the window; otherwise VFE's postfix runs.
		static Func<Window, bool> uirFactionsThemed;
		static Func<Rect, string, Texture, Color?, bool, string, GameFont, string, bool, bool, bool> uirThemedButton;
		static Func<string, Texture2D> uirIconFor;
		static Func<Color> uirBlueColor;
		static object vfetIcon;   // object, not Texture: the game's dev-mode check flags Texture statics without StaticConstructorOnStartup
		static Type vfetWindow;
		// Button sized to its label (UIR's ButtonWidth) and the title bar gap resized to match: the def's width is
		// summed once into ModCompat.headerButtonsWidth, so that cache is cleared after the change.
		static Func<string, bool, GameFont, float> uirButtonWidth;
		static FieldInfo uirHeaderWidthCache, uirSpaceWidth;
		static Type uirSpaceDefType;
		static float vfetWidth;

		static bool VfetThemedCornerstones()
		{
			var tab = Find.WindowStack.WindowOfType<MainTabWindow_Factions>();
			if (tab == null || !uirFactionsThemed(tab)) return true;
			vfetIcon ??= uirIconFor("campfire");
			string label = "VFET.Cornerstones".Translate();
			if (vfetWidth == 0f)
			{
				vfetWidth = uirButtonWidth?.Invoke(label, true, GameFont.Small) ?? 120f;
				var space = uirSpaceDefType == null ? null : GenDefDatabase.GetDefSilentFail(uirSpaceDefType, "UIRethemedFactions_VFETribals_Cornerstones", false);
				if (space != null && uirSpaceWidth != null && uirHeaderWidthCache != null)
				{
					uirSpaceWidth.SetValue(space, vfetWidth + 15f);
					uirHeaderWidthCache.SetValue(null, null);
				}
				else vfetWidth = 120f;
			}
			float x = (ModsConfig.IsActive("OskarPotocki.VFE.Classical") ? 135f : 0f) + 12f;
			if (uirThemedButton(new Rect(x, 12f, vfetWidth, 28f), label, (Texture)vfetIcon, uirBlueColor(), true,
				"VFET.CustomizeCornerstones".Translate(), GameFont.Small, null, false, true))
				Find.WindowStack.Add((Window)Activator.CreateInstance(vfetWindow));
			return false;
		}

		// ---- Show Mod Updates + UI Rethemed - Main Bar: icon on the "N updated mods" menu button ----
		// UIR's menu gives icons only to the vanilla options it knows (KnownOptions); other mods' buttons are drawn by
		// MenuWindow.Face with no icon. Match Show Mod Updates' label ("{0} updated mod(s)", in the active language)
		// and hand Face a Phosphor icon from UIR Core.
		static System.Text.RegularExpressions.Regex smuLabel;
		static LoadedLanguage smuLanguage;
		static object smuIcon;

		static void UirMenuModButtonIcon(string text, ref Texture2D iconTex)
		{
			if (iconTex != null || text.NullOrEmpty()) return;
			if (smuLanguage != LanguageDatabase.activeLanguage)
			{
				smuLanguage = LanguageDatabase.activeLanguage;
				var forms = new List<string>();
				foreach (var key in new[] { "SMU.CurrentUpdate", "SMU.CurrentUpdates" })
					if (smuLanguage.TryGetTextFromKey(key, out var form))
						forms.Add(System.Text.RegularExpressions.Regex.Escape(form.RawText).Replace(@"\{0}", @"\d+"));
				smuLabel = forms.Count == 0 ? null : new System.Text.RegularExpressions.Regex("^(?:" + string.Join("|", forms) + ")$");
			}
			if (smuLabel == null || !smuLabel.IsMatch(text)) return;
			smuIcon ??= uirIconFor("arrow-clockwise");
			iconTex = (Texture2D)smuIcon;
		}

		// ---- UI Rethemed - Notifications: the top-left messages drawn as rows in its notification stack ----
		// UIR restyles letters, conditions and alerts into one column but leaves the game's transient messages
		// (Messages.MessagesDoGUI) as plain text in the top-left; its own patch there only shifts them sideways when
		// its column sits top-left. While a game is loaded and the column is bottom-right (its default), draw each live
		// message as a StatusRow, the widget its alert rows use, stacked upward from the top of the alerts
		// (LetterStack.LastTopY - AlertsReadout.AlertsHeight, which UIR keeps vanilla in that position), newest nearest
		// the stack. Colour and icon come from UIR's own four kinds, so they match the alert rows below. Behaviour is
		// vanilla Message.Draw: left-click jumps or opens the quest, right-click dismisses (with UIR's card click sound), hover highlights the target
		// and shows UIR's own hover card as its alerts do (HoverCard.DrawPlaced beside the column, at once, no tooltip
		// delay: a header, its rule, then the body) in the row's colour, the header naming the message type (English,
		// message types carry no label) and the body the full text; a Flash() reads as the row's hover glow. ponytail: no 0.6 s fade-out (StatusRow sets its own colours), rows
		// vanish at expiry as UIR's alerts do. Any other column position leaves the game's messages alone.
		// Vanilla keeps a message 13 s; in a stack of five the last ones were gone before they were read, so add
		// 17 s (30 s total), and a hovered row restarts its timer so it never vanishes under the cursor.
		const float UirRowHeight = 24f, UirRowStep = 26f, UirStackGap = 10f, UirMessageExtraLife = 17f;
		static MethodInfo uirRowDrawCall, uirPositionCall;
		static Func<string, float> uirRowWidthCall;
		static Func<float, float> uirColumnXCall;
		static Func<Rect, int, bool, float> uirHoverCall;
		static int uirBottomRight;
		// Styles are built on first draw (main thread, defs loaded): touching NotificationsWidgets' texture fields or
		// NotificationColorDefOf from the mod constructor runs UIR's texture loading off the main thread (and sees null
		// DefOfs).
		static Type uirStyleType, uirWidgetsType;
		static MethodInfo uirColorOfCall;
		static object[] uirMessageStyles;
		static readonly Color?[] uirMessageAccents = new Color?[4];
		static Action<Rect, string, string, string, Color?> uirHoverRegion;
		static MethodInfo uirPlacedDraw;
		static Func<string, string, Vector2> uirPlacedSizeCall;
		static Func<Rect, float, float, float> uirBesideCall;
		static object uirAlertsPart;
		const int UirMessageCardWindow = 0x5A1722;
		static Dictionary<MessageTypeDef, string> uirMessageHeaders;

		static string UirHeaderOf(MessageTypeDef def)
		{
			if (uirMessageHeaders == null)
			{
				uirMessageHeaders = new Dictionary<MessageTypeDef, string>();
				void Name(string header, params MessageTypeDef[] defs) { foreach (var d in defs) if (d != null) uirMessageHeaders[d] = header; }
				Name("Major threat", MessageTypeDefOf.ThreatBig);
				Name("Threat", MessageTypeDefOf.ThreatSmall);
				Name("Death", MessageTypeDefOf.PawnDeath);
				Name("Health", MessageTypeDefOf.NegativeHealthEvent);
				Name("Setback", MessageTypeDefOf.NegativeEvent);
				Name("Notice", MessageTypeDefOf.NeutralEvent, MessageTypeDefOf.SilentInput);
				Name("Task completed", MessageTypeDefOf.TaskCompletion);
				Name("Good news", MessageTypeDefOf.PositiveEvent);
				Name("Resolved", MessageTypeDefOf.SituationResolved);
				Name("Not possible", MessageTypeDefOf.RejectInput);
				Name("Caution", MessageTypeDefOf.CautionInput);
			}
			return def != null && uirMessageHeaders.TryGetValue(def, out var h) ? h : "Message";
		}
		static Dictionary<MessageTypeDef, int> uirMessageKinds;
		static readonly AccessTools.FieldRef<List<Message>> liveMessages = AccessTools.StaticFieldRefAccess<List<Message>>(AccessTools.Field(typeof(Messages), "liveMessages"));
		static readonly AccessTools.FieldRef<Message, float> messageStart = AccessTools.FieldRefAccess<Message, float>("startingTime");
		static readonly AccessTools.FieldRef<Message, float> messageFlash = AccessTools.FieldRefAccess<Message, float>("flashTime");
		static readonly AccessTools.FieldRef<Message, int> messageId = AccessTools.FieldRefAccess<Message, int>("ID");

		static int UirKindOf(MessageTypeDef def)
		{
			if (uirMessageKinds == null)
			{
				uirMessageKinds = new Dictionary<MessageTypeDef, int>();
				void Kind(int kind, params MessageTypeDef[] defs) { foreach (var d in defs) if (d != null) uirMessageKinds[d] = kind; }
				Kind(3, MessageTypeDefOf.ThreatBig, MessageTypeDefOf.PawnDeath);
				Kind(2, MessageTypeDefOf.ThreatSmall, MessageTypeDefOf.NegativeEvent, MessageTypeDefOf.NegativeHealthEvent, MessageTypeDefOf.CautionInput, MessageTypeDefOf.RejectInput);
				Kind(1, MessageTypeDefOf.PositiveEvent, MessageTypeDefOf.TaskCompletion, MessageTypeDefOf.SituationResolved);
			}
			return def != null && uirMessageKinds.TryGetValue(def, out var k) ? k : 0;
		}

		static object[] UirStyles()
		{
			if (uirMessageStyles != null) return uirMessageStyles;
			var styles = new object[4];
			var texNames = new[] { "NeutralTex", "GoodTex", "WarningTex", "BadTex" };
			for (int kind = 0; kind < 4; kind++)
			{
				var style = Activator.CreateInstance(uirStyleType);
				AccessTools.Field(uirStyleType, "color").SetValue(style, uirColorOfCall.Invoke(null, new object[] { kind }));
				AccessTools.Field(uirStyleType, "icon").SetValue(style, AccessTools.Field(uirWidgetsType, texNames[kind]).GetValue(null));
				AccessTools.Field(uirStyleType, "tintIcon").SetValue(style, true);
				styles[kind] = style;
				var colorDef = AccessTools.Field(uirStyleType, "color").GetValue(style);
				uirMessageAccents[kind] = colorDef == null ? null : (Color?)AccessTools.Field(colorDef.GetType(), "accent").GetValue(colorDef);
			}
			return uirMessageStyles = styles;
		}

		static bool UirMessagesInStack()
		{
			if (Current.ProgramState != ProgramState.Playing || Find.LetterStack == null || Find.Alerts == null
				|| (int)uirPositionCall.Invoke(null, null) != uirBottomRight)
				return true;
			var messages = liveMessages();
			if (messages.Count == 0) return false;
			var styles = UirStyles();
			float width = 0f;
			foreach (var msg in messages) width = Mathf.Max(width, uirRowWidthCall(msg.text));
			float x = uirColumnXCall(width);
			float y = Find.LetterStack.LastTopY - Find.Alerts.AlertsHeight - UirStackGap;
			for (int i = messages.Count - 1; i >= 0; i--, y -= UirRowStep)
			{
				var msg = messages[i];
				var rect = new Rect(x, Mathf.Round(y - UirRowHeight), width, UirRowHeight);
				msg.lastDrawRect = rect;
				bool over = Mouse.IsOver(rect);
				float hover = uirHoverCall(rect, Gen.HashCombineInt(messageId(msg), 0x5A17), over);
				if (messageFlash(msg) + 0.6f > RealTime.LastRealTime) hover = 1f;
				var target = msg.lookTargets != null ? msg.lookTargets.TryGetPrimaryTarget() : GlobalTargetInfo.Invalid;
				bool canJump = CameraJumper.CanJump(target), hasQuest = msg.quest != null && !msg.quest.hidden;
				if (Event.current.type == EventType.Repaint)
				{
					uirRowDrawCall.Invoke(null, new object[] { rect, styles[UirKindOf(msg.def)], msg.text, hover, default(Color), -1f, false });
					if (canJump || hasQuest) UIHighlighter.HighlightOpportunity(rect, "Messages");
				}
				if (over)
				{
					msg.ResetTimer();
					Messages.Notify_Mouseover(msg);
					if (uirPlacedDraw != null)
					{
						if (Event.current.type == EventType.Repaint)
						{
							string header = UirHeaderOf(msg.def);
							var size = uirPlacedSizeCall(header, msg.text);
							float cardX = uirBesideCall(new Rect(x, 0f, width, 0f), size.x, 8f);
							float cardY = Mathf.Round(Mathf.Clamp(Event.current.mousePosition.y, 0f, Mathf.Max(0f, UI.screenHeight - size.y)));
							uirPlacedDraw.Invoke(null, new object[] { UirMessageCardWindow, new Vector2(cardX, cardY), header, msg.text,
								uirMessageAccents[UirKindOf(msg.def)] ?? Color.white, uirAlertsPart });
						}
					}
					else if (uirHoverRegion != null) uirHoverRegion(rect, UirHeaderOf(msg.def), msg.text, null, uirMessageAccents[UirKindOf(msg.def)]);
					else
					{
						Text.Font = GameFont.Small;
						if (Text.CalcSize(msg.text).x > width - 38f) TooltipHandler.TipRegion(rect, msg.text);
					}
				}
				if (!Widgets.ButtonInvisible(rect)) continue;
				if (Event.current.button == 1)
				{
					messageStart(msg) = -99999f;
					Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Click);   // what UIR's LetterCards.DrawButtonAt plays
				}
				else if (canJump)
				{
					CameraJumper.TryJumpAndSelect(target);
					PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.ClickingMessages, KnowledgeAmount.Total);
				}
				else if (hasQuest)
				{
					if (Find.MainTabsRoot.OpenTab == MainButtonDefOf.Quests) Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Click);
					else Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Quests);
					((MainTabWindow_Quests)MainButtonDefOf.Quests.TabWindow).Select(msg.quest);
				}
			}
			return false;
		}

		static void UirLongerMessages(ref float __result) => __result += UirMessageExtraLife;

		// ---- Flourish - Everyday Animations: telescope drawn under the posed pawn, not between body and hands ----
		// A building always draws below pawns, so in the anchored telescope pose the pawn's body covers the telescope
		// and the hands hold nothing visible. Flourish draws the hands from a postfix on
		// PawnRenderUtility.DrawEquipmentAndApparelExtras, whose drawPos is the body at pawn layer 90 (above every
		// body/head/apparel node), at +0.001..0.0015. While the anchored pose is running (pose reached, not the walk
		// there), draw the pose target's graphic again at +0.0005: over the pawn, under the hands. Mesh and material
		// only, so the building's shadow is not repeated on top of the pawn. The real building still draws below.
		static FieldInfo flPddDef, flPddTimelineStart, flDefWorkerClass;
		static Type flAnchoredWorker;
		static Func<Pawn, object> flDataForCall;

		static void FlDrawAnchorTargetOverPawn(Pawn pawn, Vector3 drawPos, Rot4 facing, PawnRenderFlags flags)
		{
			if (pawn == null || !facing.IsHorizontal || (flags & (PawnRenderFlags.Portrait | PawnRenderFlags.Cache | PawnRenderFlags.Invisible | PawnRenderFlags.Statue)) != 0) return;
			var thing = pawn.CurJob?.targetA.Thing;
			if (thing == null || !thing.Spawned || thing.Map != pawn.Map || pawn.pather?.Moving == true) return;
			var data = flDataForCall(pawn);
			if (data == null || (int)flPddTimelineStart.GetValue(data) == int.MinValue) return;
			var def = flPddDef.GetValue(data);
			if (def == null || !flAnchoredWorker.IsAssignableFrom(flDefWorkerClass.GetValue(def) as Type)) return;
			var graphic = thing.Graphic;
			if (graphic == null) return;
			var rot = thing.Rotation;
			var loc = thing.DrawPos + graphic.DrawOffset(rot);
			loc.y = drawPos.y + 0.0005f;
			GenDraw.DrawMeshNowOrLater(graphic.MeshAt(rot), loc, graphic.QuatFromRot(rot), graphic.MatAt(rot, thing), drawNow: false);
		}

		// ---- Flourish - Everyday Animations: anchored pose ignores the target's mirrored art ---------------------
		// JobAnimationWorker_Anchored (only Flourish_Watch_Telescope uses it) draws the pawn at the target's
		// TrueCenter + def.anchor, facing def.anchorFacing, both fixed world values. The vanilla telescope is
		// Graphic_Single with drawRotated false, so vanilla mirrors its art when it is rotated West (Graphic.WestFlipped)
		// and the eyepiece swaps sides. Flourish's values fit the unmirrored art (pawn east of it, facing West, on the
		// interaction-cell side); when the target is drawn mirrored, Evaluate's reads of def.anchor.x and
		// def.anchorFacing get the mirror image (x negated, East <-> West).
		// The body/hand tracks are facing-relative, so they follow. Swaps the reads instead of writing the shared def,
		// because pawn rendering can run on several threads.
		static FieldInfo flAnchor, flAnchorFacing;

		static IEnumerable<CodeInstruction> FlMirrorAnchorTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var x = AccessTools.Field(typeof(Vector2), nameof(Vector2.x));
			var y = AccessTools.Field(typeof(Vector2), nameof(Vector2.y));
			int anchors = 0, facings = 0;
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ldflda && Equals(codes[i].operand, flAnchor) && i + 1 < codes.Count
					&& codes[i + 1].opcode == OpCodes.Ldfld && (Equals(codes[i + 1].operand, x) || Equals(codes[i + 1].operand, y)))
				{
					// stack: def -> def, pawn -> float
					var getter = Equals(codes[i + 1].operand, x) ? nameof(FlAnchorX) : nameof(FlAnchorY);
					codes[i] = new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(codes[i]);
					codes[i + 1] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), getter)).MoveLabelsFrom(codes[i + 1]);
					anchors++;
				}
				else if (codes[i].opcode == OpCodes.Ldfld && Equals(codes[i].operand, flAnchorFacing))
				{
					codes[i] = new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(codes[i]);
					codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(FlAnchorFacing))));
					facings++;
				}
			}
			if (anchors != 2 || facings != 2)
			{
				Log.Error($"[Harmony3StrictCompat] Flourish anchored pose: expected 2 anchor and 2 facing reads, found {anchors}/{facings}; telescope pose left unmirrored");
				return instructions;
			}
			return codes;
		}

		static bool FlTargetMirrored(Pawn pawn)
		{
			var thing = pawn?.CurJob?.targetA.Thing;
			if (thing == null) return false;
			var rot = thing.Rotation;
			var graphic = thing.Graphic;
			return graphic != null && ((rot == Rot4.West && graphic.WestFlipped) || (rot == Rot4.East && graphic.EastFlipped));
		}

		static float FlAnchorX(object def, Pawn pawn)
		{
			float ax = ((Vector2)flAnchor.GetValue(def)).x;
			return FlTargetMirrored(pawn) ? -ax : ax;
		}

		static float FlAnchorY(object def, Pawn pawn) => ((Vector2)flAnchor.GetValue(def)).y;

		static Rot4 FlAnchorFacing(object def, Pawn pawn)
		{
			var facing = (Rot4)flAnchorFacing.GetValue(def);
			return FlTargetMirrored(pawn) && facing.IsHorizontal ? facing.Opposite : facing;
		}

		// ---- Designator Shapes flood fill runs through doors -----------------------------------------------------
		// Shapes' flood fill stops at a thing (or blueprint of one) that covers the floor or whose architect category
		// is literally "Structure" (private FloodFill.IsStructure). Better Architect Menu moves DoorBase to
		// Ferny_Doors, so doors stop counting and a floor fill leaks through every door. Walls still stop it because
		// they cover the floor. Count doors as structure again: Building_Door subclasses, plus anything BAM files under
		// Ferny_Doors. IsStructure is private and only Shapes' flood fill calls it, so nothing else sees this.
		static void DsDoorIsStructure(Def d, ref bool __result)
		{
			if (!__result && d is ThingDef td && (td.IsDoor || td.designationCategory?.defName == "Ferny_Doors"))
				__result = true;
		}

		// ---- Hauler's Dream x Compositable Loadouts: loadout items not kept during surplus unload ---------------
		// HD keeps a pawn's loadout items out of "adopt all surplus" by reading CL's Item.Def and Item.Quantity. CL's
		// 1.7 Item holds several alternative defs (members: Defs, RepresentativeDef) and has no Def any more, so HD's
		// InitKeepApi logs "per-pawn loadout keep API ... did not fully resolve" and turns the shield off: HD may
		// unload what CL fetches again, in a loop. When only Item.Def is missing, switch the shield back on and count
		// every loadout item whose Defs include the def (an item with alternatives keeps each of them, the safe side).
		// HD's own warning still prints once at startup; the bridge logs right after it.
		static MethodInfo hdItemDefs;

		static void HdBridgeKeepApi(ref bool ___keepApiOk, MethodInfo ___loadoutGetter, MethodInfo ___itemsGetter,
			MethodInfo ___itemDefGetter, MethodInfo ___itemQtyGetter)
		{
			if (___keepApiOk || ___itemDefGetter != null || ___loadoutGetter == null || ___itemsGetter == null || ___itemQtyGetter == null) return;
			___keepApiOk = true;
			Log.Message("[Harmony3StrictCompat] Hauler's Dream: Compositable Loadouts keep API bridged to Item.Defs, loadout items are kept during surplus unload again");
		}

		static bool HdWantedUnits(ThingComp comp, ThingDef def, ref int __result, MethodInfo ___loadoutGetter,
			MethodInfo ___itemsGetter, MethodInfo ___itemDefGetter, MethodInfo ___itemQtyGetter)
		{
			if (___itemDefGetter != null) return true;   // HD resolved Item.Def itself: its own code runs
			__result = 0;
			if (___loadoutGetter.Invoke(comp, null) is not { } loadout || ___itemsGetter.Invoke(loadout, null) is not System.Collections.IEnumerable items)
				return false;
			foreach (var item in items)
				if (item != null && hdItemDefs.Invoke(item, null) is IEnumerable<ThingDef> defs && defs.Contains(def))
					__result += Math.Max((int)___itemQtyGetter.Invoke(item, null), 0);
			return false;
		}

		// ---- Prisoner Realism x Cleaning Area Relaunched: prisoner cleaning loop -----------------------------------
		// Prisoner Realism's JobGiver_CleanCell queues filth in the prisoner's room that lies in the Home area. Cleaning
		// Area Relaunched replaces the Clean job driver, and that driver skips every target outside its own Clean area.
		// With all targets skipped the job succeeds on its first tick and the giver hands out the same job again: the
		// prisoner stands still forever. Seen with Miriam: dirt under a 2-tile door (hidden by the door sprite) in Home
		// but not in Clean. Only queue filth that the driver will actually clean.
		static Func<Map, Area> carCleanArea;

		static void PrCleanOnlyInCleanArea(Thing t, ref bool __result)
		{
			if (__result && carCleanArea(t.Map) is { } area && !area[t.Position]) __result = false;
		}

		// ---- Hunt For Me: released animals hunt themselves ------------------------------------------------------
		// HFM's think tree (Animal_PreWander) only checks that HuntingTraining is learned, not the faction, and a
		// released animal keeps its training. Its supervised mode (the default) picks prey from the player's Hunt
		// designations with no "prey is not me" check, so a released hydra marked for hunting got a HuntTrained job
		// on itself and bit itself down. Vanilla never lets a Hunt designation sit on a player animal, so allowing
		// only player animals to run the job giver also rules out self-prey. HFM's own gizmo and pack logic already
		// assume player animals (Pawn_MindState patch checks Faction.OfPlayer).
		static bool HfmPlayerAnimalsOnly(Pawn pawn) => pawn?.Faction == Faction.OfPlayer;

		// ---- Common Sense: clean-before-eating crashes when the filth vanishes -----------------------------------
		// Utility.CleanFilthToil's init and tick actions read the target filth's def, Position and Map with no check.
		// When another pawn cleans that filth first (or it is destroyed otherwise), the next tick null-refs on its Map
		// and the whole eating job ends with an error. Every other caller adds JumpIfDespawnedOrNullOrForbidden; the
		// tool-user ingest path (JobDriver_PrepareToIngestToils_ToolUser) does not. Move on to the next toil (the
		// next filth in the queue, then the meal) when the target is gone.
		static bool CsFilthGone(Toil ___toil, TargetIndex ___Ind)
		{
			var actor = ___toil?.actor;
			if (actor?.jobs?.curJob == null || actor.jobs.curJob.GetTarget(___Ind).Thing is Filth { Spawned: true }) return true;
			actor.jobs.curDriver.ReadyForNextToil();
			return false;
		}

		// ---- Vanilla: label of an emptied minified thing --------------------------------------------------------
		// MinifiedThing.LabelNoCount returns InnerThing.LabelNoCount, but InnerThing is null once the contents were
		// installed (a HaulToContainer delivering a minified building into its frame). JobDriver_HaulToContainer.
		// GetReport reads that label, so anything asking for the job report then null-refs: Modern Needs Tab's
		// EndCurrentJob prefix did, and turned its job-history capture off for the session. Use the def's label.
		static bool EmptyMinifiedLabel(MinifiedThing __instance, ref string __result)
		{
			if (__instance.InnerThing != null) return true;
			__result = __instance.def.label;
			return false;
		}

		// ---- Busywork "Upper" job icon on LTO Colony Groups portraits -------------------------------------------
		// In Upper mode Busywork stops feeding its job icon to Useful Marks (so "Useful Marks in Colony Groups" has
		// nothing to draw) and draws it itself: over the pawn's head on the map, and in the portrait's top-left corner
		// via a postfix on vanilla ColonistBarColonistDrawer.DrawColonist. LTO Colony Groups replaces the colonist
		// bar with its own drawer, so that postfix never runs. Run Busywork's own postfix from LTO's DrawColonist; it
		// never touches its drawer argument, so null is fine, and the Upper-mode check, position and colours stay its own.
		static Action<ColonistBarColonistDrawer, Rect, Pawn> bwUpperMark;

		static void LtoBusyworkUpperMark(Rect rect, Pawn colonist) => bwUpperMark(null, rect, colonist);

		// ---- Better Workbench Management: bill options overlap the Ideology style button -------------------------
		// Personal tweak: BWM's previous/next bill arrows sit at inRect.yMin + 4 and overlap the top of the ingredient
		// filter by a few pixels. inRect is only used for that rect, so shift it 7 px up.
		static void BwmNavArrowsUp(ref Rect inRect) => inRect.y -= 7f;

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

		// ---- Mod Settings Framework: slider settings only reach the first matching node ---------------------------
		// PatchOperationSliderFloat/Int.ApplyWorker write the slider value with xml.SelectSingleNode(xpath), so an xpath
		// that matches several nodes only changes the first. ReBuild: Doors and Corners' "Grate tile speed penalty"
		// targets `TerrainDef[@Name="RB_TileGrateMetalBase" or defName="RB_SterileGrateTile"]/pathCost`: the metal grate
		// base got the slider value, the sterile grate tile kept pathCost 10 (57% walk speed). After the original runs,
		// copy the first node's new value to every other match. Skipped when the option would not run (CanRun false or
		// no settings container), exactly when the original leaves the first node alone.
		static void MsfSliderAllNodes(PatchOperation __instance, System.Xml.XmlDocument xml)
		{
			var type = __instance.GetType();
			if (AccessTools.Method(type, "CanRun")?.Invoke(__instance, null) is not true
				|| AccessTools.PropertyGetter(type, "SettingsContainer")?.Invoke(__instance, null) == null
				|| AccessTools.Field(type, "xpath")?.GetValue(__instance) is not string xpath)
				return;
			var nodes = xml.SelectNodes(xpath);
			if (nodes == null || nodes.Count < 2) return;
			for (int i = 1; i < nodes.Count; i++)
				nodes[i].InnerText = nodes[0].InnerText;
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

		// TryGetVeteran hands a pawn out but leaves it a Free world pawn until it spawns. Vanilla's own redress
		// (GenerateOrRedressPawnInternal) takes from that same Free list and removes what it takes, so in the next
		// GeneratePawn of the same group (Eternal Pawns' 50% roll failing) vanilla could redress the issued veteran
		// again: ChangeKind + new gear, and the same Pawn object twice in the group. Seen twice with The Patreons'
		// caravan: trader Patch (Town_Trader) re-picked as a Mercenary_Gunner guard, so the trader became a "gunner";
		// "Tried to spawn X but it's already spawned" and "Lord tried to add X whom it already controls" for 3 pawns;
		// Giddy-Up then gave Staunch two pack muffalos, the second waited for him forever at the map edge and the
		// lord never left LordToil_Travel (caravan starved). Do what vanilla does for a redressed pawn: take it out
		// of WorldPawns. Eternal Pawns' mission cleanup passes a veteran back to the world if it never reaches a map.
		static void EpIssuedLeavesWorld(Pawn __result)
		{
			if (__result != null && !__result.Spawned && Find.WorldPawns.Contains(__result))
				Find.WorldPawns.RemovePawn(__result);
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

		// ---- Hospitality: Casino: visitors leave their lord duty to play slot machines ----------------------------
		// HC_SlotMachineVisitor inserts JobGiver_PlaySlotMachines at Humanlike_PostMentalState, above the lord duty
		// node, for every friendly non-player pawn; it only skips Hospitality guests and LordToil_ExitMap. So it
		// overrides any lord job: the bestower left the bestowing ceremony to gamble, guards boarding the shuttle
		// (LordToil_EnterShuttleOrLeave) were pulled off it and the shuttle left pawns behind / stayed, and leaving
		// trade caravans (LordToil_ExitMapAndEscortCarriers) ran back across the map. Only casino visitors and trade
		// caravans waiting at their chill spot (LordToil_DefendTraderCaravan) may gamble now.
		static bool HcSlotsOnlyIdleVisitors(Pawn pawn)
		{
			var lord = pawn?.GetLord();
			return lord?.LordJob?.GetType().FullName == "HospitalityCasino.LordJob_CasinoVisit"
				|| lord?.LordJob is LordJob_TradeWithColony && lord.CurLordToil?.GetType().Name == "LordToil_DefendTraderCaravan";
		}

		// ---- Hospitality: Casino: charge for table games that keep their own job driver ---------------------------
		// Casino charges only from its own drivers (poker, VFE roulette, slots): they call InsertCoin when play starts.
		// LocalTweaks gives other tables Casino's CompVendingMachine (Det's Boglegs tongo table, vanilla billiards)
		// but keeps their drivers: the tongo driver draws the tongo cards, billiards loops 600-tick shots (a per-toil
		// charge would bill every shot). So charge once per job on the first joy tick at any building that has the
		// comp, unless the driver is Casino's own. InsertCoin itself only charges pawns of another faction.
		static Type hcCompType;
		static Func<Pawn, Thing, bool> hcInsertCoin;
		static readonly Dictionary<ThingDef, bool> hcHasComp = new();
		static readonly Dictionary<int, int> hcChargedJob = new();   // pawn thingIDNumber -> job loadID

		static void HcChargeOtherTables(Pawn pawn, Building joySource)
		{
			if (joySource == null || pawn?.CurJob is not { } job) return;
			if (!hcHasComp.TryGetValue(joySource.def, out bool has))
				hcHasComp[joySource.def] = has = joySource.def.comps.Any(p => p.compClass == hcCompType);
			if (!has || pawn.jobs.curDriver?.GetType().Namespace == "HospitalityCasino") return;
			if (hcChargedJob.TryGetValue(pawn.thingIDNumber, out int charged) && charged == job.loadID) return;
			hcChargedJob[pawn.thingIDNumber] = job.loadID;
			hcInsertCoin(pawn, joySource);
		}

		// ---- Hospitality: Casino: show the pricing setting in the inspect pane ------------------------------------
		// The pricing gizmo label is the only place the setting shows. Add it under "Contains" / "Lifetime earnings",
		// with the silver InsertCoin charges per game (ceil of defaultPrice x 0 / 0.5 / 1 / 2). Payouts don't scale.
		static AccessTools.FieldRef<object, int> hcPricing;
		static MethodInfo hcItemPrice;
		static readonly string[] hcPricingKeys = { "HospitalityCasino_Free", "HospitalityCasino_Low", "HospitalityCasino_Medium", "HospitalityCasino_High" };

		static void HcPricingLine(ThingComp __instance, ref string __result)
		{
			if (string.IsNullOrEmpty(__result)) return;   // Casino shows nothing while the machine is switched off
			int pricing = hcPricing(__instance);
			if (pricing < 0 || pricing >= hcPricingKeys.Length) return;
			string line = "Pricing: " + hcPricingKeys[pricing].Translate();
			if (pricing > 0)
				line += ", " + ((float)Mathf.CeilToInt((float)hcItemPrice.Invoke(__instance, new object[] { null }))).ToStringMoney("F0") + " per game";
			__result += "\n" + line;
		}

		static bool IsCasketPiece(Apparel a) => a?.GetType().Name == "Apparel_Warcasket";
		static bool WearsCasket(Pawn p) => p?.apparel?.WornApparel.Any(IsCasketPiece) == true;

		static void EpViolenceCheck(Pawn p, PawnGenerationRequest req, ref bool __result)
		{
			if (__result && req.MustBeCapableOfViolence && p != null && p.WorkTagIsDisabled(WorkTags.Violent))
				__result = false;
		}

		// ---- Diagnostic: Hospitality guest sleeping on the floor next to a bed it owns ----------------------
		// Seen 2026-11-01 (Bladeskarthak) and 2026-11-04 (Hindman, Rova, Vay, Allen, Muddy): guests with a claimed
		// guest bed sleep on the floor far from it. v34-v52 probed Hospitality.JobGiver_Sleep, but that node returns the
		// pawn's CURRENT job unchanged when it has one, so it only re-reported floor jobs some other node had started
		// (every report said the bed was valid). Now probe Pawn_JobTracker.StartJob instead: when a guest with a
		// claimed bed starts a floor LayDown, log which think node issued it (with its parent chain and the duty) and
		// what is wrong with the bed right then. Diagnostic only: changes nothing. Remove once the cause is known.
		// v67: the v53 StartJob probe caught 4 guests on 2026-11-05 22:23: Hospitality.JobGiver_Sleep itself issues the
		// floor LayDown (its fallback when CompGuest.HasBed is false or IsValidBedFor(its bed) is false), yet every bed
		// check passed again at StartJob. So check at decision time instead: while JobGiver_Sleep decides for a guest
		// that owns a bed, a first-priority postfix on RestUtility.IsValidBedFor re-runs the vanilla sub-checks the
		// moment vanilla says no, and a last-priority postfix notes when vanilla said yes but a mod postfix said no.
		// When the decision is a floor LayDown, the JobGiver postfix logs what was seen.
		static FieldInfo guestBedField;
		[ThreadStatic] static Pawn sleepProbePawn;
		[ThreadStatic] static Building_Bed sleepProbeBed;
		[ThreadStatic] static bool sleepProbeVanilla;
		[ThreadStatic] static List<string> sleepProbeNotes;
		static bool sleepProbeOwnersLogged;

		static Building_Bed GuestBedOf(Pawn pawn)
		{
			var comp = pawn.AllComps?.FirstOrDefault(c => c.GetType() == guestBedField.DeclaringType);
			return comp == null ? null : guestBedField.GetValue(comp) as Building_Bed;
		}

		static void SleepProbeStart(Pawn pawn)
		{
			sleepProbePawn = null;
			if (pawn?.Map == null || pawn.CurJob != null || GuestBedOf(pawn) is not Building_Bed bed) return;
			sleepProbePawn = pawn;
			sleepProbeBed = bed;
			(sleepProbeNotes ??= new()).Clear();
		}

		static void SleepProbeVanillaResult(Thing bedThing, Pawn sleeper, bool __result)
		{
			if (sleeper == null || sleeper != sleepProbePawn || bedThing != sleepProbeBed) return;
			sleepProbeVanilla = __result;
			if (__result) return;
			sleepProbePawn = null; // BedProblems calls IsValidBedFor itself
			try { sleepProbeNotes.Add($"vanilla IsValidBedFor said no; checks run at that moment: {string.Join("; ", BedProblems(sleeper, sleepProbeBed))}"); }
			catch (Exception e) { sleepProbeNotes.Add($"check failed: {e.Message}"); }
			finally { sleepProbePawn = sleeper; }
		}

		static void SleepProbeFinalResult(Thing bedThing, Pawn sleeper, bool __result)
		{
			if (sleeper == null || sleeper != sleepProbePawn || bedThing != sleepProbeBed) return;
			if (sleepProbeVanilla && !__result)
				sleepProbeNotes.Add($"vanilla IsValidBedFor said yes, a mod postfix turned it to no (postfix owners: {PatchOwners(AccessTools.Method(typeof(RestUtility), nameof(RestUtility.IsValidBedFor)))})");
			else if (__result)
				sleepProbeNotes.Add("IsValidBedFor said yes for the bed");
		}

		static void SleepProbeEnd(Pawn pawn, ThinkResult __result)
		{
			if (pawn == null || pawn != sleepProbePawn) return;
			sleepProbePawn = null;
			var job = __result.Job;
			if (job == null || job.def != JobDefOf.LayDown || job.targetA.HasThing) return;
			try
			{
				var bed = sleepProbeBed;
				string why = sleepProbeNotes.Count > 0 ? string.Join(" | ", sleepProbeNotes)
					: $"IsValidBedFor was never asked about the bed, so CompGuest.HasBed was false (spawned {bed.Spawned}, " +
					  $"assigned ({Names(bed.CompAssignableToPawn?.AssignedPawnsForReading)}), owners ({Names(bed.OwnersForReading)})) and FindBedFor found no other bed";
				string owners = sleepProbeOwnersLogged ? "" :
					$" Patch owners: IsValidBedFor [{PatchOwners(AccessTools.Method(typeof(RestUtility), nameof(RestUtility.IsValidBedFor)))}], " +
					$"CanUseBedNow [{PatchOwners(AccessTools.Method(typeof(RestUtility), nameof(RestUtility.CanUseBedNow)))}], " +
					$"JobGiver_Sleep [{PatchOwners(AccessTools.Method("Hospitality.JobGiver_Sleep:TryIssueJobPackage"))}].";
				sleepProbeOwnersLogged = true;
				Log.Warning($"[Harmony3StrictCompat] guest {pawn.LabelShort} told to sleep on the floor at {job.targetA.Cell} although it claimed " +
					$"{bed.LabelShort} at {bed.Position} (tick {Find.TickManager.TicksGame}, hour {GenLocalDate.HourOfDay(pawn)}, rest {pawn.needs?.rest?.CurLevelPercentage:P0}, " +
					$"duty {pawn.mindState?.duty?.def?.defName ?? "none"}, position {pawn.Position}): {why}.{owners}");
			}
			catch (Exception e)
			{
				Log.Warning($"[Harmony3StrictCompat] guest floor-sleep probe failed: {e.Message}");
			}
		}

		static string PatchOwners(MethodBase m)
		{
			var info = m == null ? null : Harmony.GetPatchInfo(m);
			return info == null ? "none" : string.Join(", ", info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).Select(p => p.owner).Distinct());
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
			["Hospitality.Utilities.SpawnGroupUtility:GenerateNewGearFor"] = 0x9968B91075E1A216,                  // Hospitality 1.7, 2026-11-02
			["Hospitality.Utilities.ItemUtility:PocketHeadgear"] = 0x5500E1A13797F143,                       // Hospitality 1.7, 2026-11-02
			["HospitalityCasino.JobGiver_PlaySlotMachines:TryGiveJob"] = 0x886CDC4608E3C02D,                 // Casino 1.7, 2026-11-05
			["HospitalityCasino.VendingMachineJobHelper:InsertCoin"] = 0xDD3ACDA377763174,                   // Casino 1.7, 2026-11-06
			["HospitalityCasino.CompVendingMachine:CompInspectStringExtra"] = 0x4C16897C23044EC3,            // Casino 1.7, 2026-11-06
			["FinitePopulationVeterans.WorldPopulationManager:IsPawnAvailableForDispatch"] = 0x5A8A111FF41E04D8, // Eternal Pawns 1.6 build, 2026-04-13
			["FinitePopulationVeterans.WorldPopulationManager:PawnMatchesRequest"] = 0xAF54B4F3884BC77E, // Eternal Pawns 1.6 build, 2026-04-13
			["FinitePopulationVeterans.WorldPopulationManager:TryGetVeteran"] = 0xEEC0813511A23153,      // Eternal Pawns 1.6 build, 2026-04-13
			["MSSFP.Utils.PawnGraphicUtils+<>c__DisplayClass4_0:<SaveTextureToFile>b__0"] = 0x3C2DDE1F39E984E8,  // MSS Tweaks and Fun 1.7, 2026-10-19
			["Caveworld_Flora_Unleashed.FruitingBody:get_LabelMouseover"] = 0xB098C8B9BC3CE626,                 // Biomes! Caverns 1.7, 2026-10-20
			["Caveworld_Flora_Unleashed.GenCaveFungusReproduction:TryGrowMycelium"] = 0x614612774F447421,         // Biomes! Caverns 1.7, 2026-10-20
			["ilyvion.LoadingProgress.StartupImpact.ModInfoList:GetModInfoFor"] = 0x357F68C7E142FD4F,          // Loading Progress 1.7, 2026-10-21
			["Inventory.BWM_DoWindowContentsPatch_Patch:Transpiler"] = 0x1208A46933A47D41,                     // Loadout Compositing 1.7, 2026-10-28
			["Inventory.BWM_DoWindowContentsPatch_Patch:ShouldCountAway"] = 0xA22693055B6314E7,                // Loadout Compositing 1.7, 2026-10-28
			["ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawFilters"] = 0x5FFF926429D5F4D6,        // BWM 1.7 Waylith (Debug build), 2026-11-03
			["ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawWorkTableNavigation"] = 0x638B64048A13DB3E,
			["VanillaRacesExpandedSanguophage.JobGiver_GetCorpseHemogen:TryGiveJob"] = 0x00285EA300E503C6,   // VRE Sanguophage 1.6 build, 2026-10-01
			["ModSettingsFramework.PatchOperationSliderFloat:ApplyWorker"] = 0x7D3F07C2C28B20A5,              // VEF 1.7 0ModSettingsFramework.dll, 2026-10-13
			["ModSettingsFramework.PatchOperationSliderInt:ApplyWorker"] = 0x03DFA38BA62A830B,
			["Busywork.Patch_ColonistBarColonistDrawer_DrawColonist:Postfix"] = 0x8671BDCBF4F254FC,          // Busywork 1.7, 2026-11-05
			["TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:DrawColonist"] = 0x092B4F83C851CE01,     // LTO Colony Groups 1.6 build, 2026-11-05
			["ModernNeedsTab.Patch_Pawn_JobTracker_EndCurrentJob:Prefix"] = 0xDD06E10633A7F9FC,             // Modern Needs Tab, 2026-11-05
			["CommonSense.ThingMaker_MakeThing_CommonSensePatch:Postfix"] = 0xE351865A0EF38D39,             // Common Sense 1.6 build, 2026-11-05
			["CommonSense.Utility+<>c__DisplayClass21_0:<CleanFilthToil>b__0"] = 0x3736DE64D224E604,
			["CommonSense.Utility+<>c__DisplayClass21_0:<CleanFilthToil>b__1"] = 0x61D455F6E62CAD49,
			["HaulersDream.CompositableLoadoutsCompat:InitKeepApi"] = 0x3F15140E9ED2D991,                    // Hauler's Dream 1.6 build, 2026-11-05
			["HaulersDream.CompositableLoadoutsCompat:LoadoutWantedUnits"] = 0x89C4B3F40C058F43,
			["HaulersDream.StorageCommitments:get_InsideSpaceScan"] = 0x212C16560ED8173E,                     // Hauler's Dream 1.6 build, 2026-11-06
			["HaulersDream.JobDriver_UnloadHauledInventory:FindTargetOrDrop"] = 0x124B6AF8B6F91AFF,
			["HaulersDream.JobDriver_UnloadHauledInventory+<>c__DisplayClass15_0:<FindTargetOrDrop>b__0"] = 0xF8A1275757B89E49,
			["KanbanStockpileContinue.KanbanStockpileAPI:TryGetSettings"] = 0xFB4378E38CFA29D3,               // Kanban Stockpile Continue 1.6 build, 2025-08-12
			["SensibleBedOwnership.Patch_Pawn_Ownership_get_OwnedBed:Prefix"] = 0x696E7432D36527D8,       // SBO 1.6 build, 2026-11-05
			["SensibleBedOwnership.Utility:GetMainBed"] = 0xAE5863A6DF81EAD6,                                // (Pawn) overload
			["SensibleBedOwnership.Utility:AllAssignedBedsAndDeathrestCaskets"] = 0x8560CE17C9797D18,
			["VEF.Buildings.CompRandomBuildingGraphic:ChangeGraphic"] = 0xC22ADB911FDB5E90,                  // VEF 1.7, 2026-11-04
			["AdaptiveStorage.StorageGraphicWorker:GetGraphicFor"] = 0x2899906440FC5118,                     // ASF 1.6 build, 2025-11-12
			["RimPacts.WorldComponent_RimPacts:DismissExilePawn"] = 0xEC9FEB84EB993051,                        // RimPacts 1.7, 2026-11-06
			["PrisonerRealism.JobGiver_CleanCell:ShouldClean"] = 0xD2061DA02821F6FC,                          // Prisoner Realism 1.7, 2026-11-06
			["CleaningAreaRelaunched.CleaningAreaRelaunched_Util:GetCleanArea"] = 0xD9B02D52DE61B82C,          // CAR 1.6 build, 2025-11-12
			["aRandomKiwi.HFM.JobGiver_AnimalHunt:TryGiveJob"] = 0x865CBA567579063A,                            // Hunt For Me 1.6 build, 2025-12-25
			["Merthsoft.DesignatorShapes.Shapes.FloodFill:IsStructure"] = 0x01F3F2D215BA16DB,                 // Designator Shapes 1.7, 2026-11-02
			["YayoAnimation.HarmonyPatches.PawnPatches.ParallelGetPreRenderResultsPatch:DataFor"] = 0x70EEFE43592C82C3, // Flourish Everyday 1.7, 2026-10-25
			["Flourish.Everyday.JobAnimationWorker_Anchored:Evaluate"] = 0x817B489F5395FF01,                // Flourish Everyday 1.7, 2026-10-25
			["VFETribals.FactionUIUtility_DoWindowContents_Patch:Postfix"] = 0x8824F2B542A8AC6E,             // VFE Tribals 1.6 build, 2026-10-14
			["UIRethemed.Factions.FactionsTab:Themed"] = 0x6464010BFAB662C3,                               // UI Rethemed 1.7, 2026-11-06
			["UIRethemed.Core.CoreWidgets:Button"] = 0x96B9EA4E631E845B,
			["UIRethemed.Core.CoreWidgets:Icon"] = 0x2094A97723860CBE,
			["UIRethemed.Core.CoreWidgets:ButtonWidth"] = 0xFEC5735778B040A7,
			["UIRethemed.Factions.ModCompat:get_HeaderButtonsWidth"] = 0xC62AB6FF16C5404C,
			["UIRethemed.MainBar.MenuWindow:Face"] = 0x7970824AD7079EAA,                               // UI Rethemed - Main Bar 1.7, 2026-11-07
			["UIRethemed.Notifications.StatusRow:Draw"] = 0xE09CE1569F50CDDA,                               // UI Rethemed - Notifications 1.7, 2026-11-06
			["UIRethemed.Notifications.StatusRow:WidthFor"] = 0xBBB2A3AD2402186E,
			["UIRethemed.Notifications.NotificationStyles:ColorOf"] = 0x095FCC7B0F8047CE,
			["UIRethemed.Notifications.Placement:ColumnX"] = 0xD7D40FE538C34D02,
			["UIRethemed.Notifications.Placement:get_Position"] = 0x7B404D78F1D65665,
			["UIRethemed.Notifications.Motion:Hover"] = 0xFB7ED9A581BF4F1B,
			["UIRethemed.Notifications.Patch_Messages_MessagesDoGUI:Transpiler"] = 0x8F948D82A4AB2A5A,
			["UIRethemed.Notifications.HoverCard:Region"] = 0xD0A0905F849E38FF,
			["UIRethemed.Notifications.HoverCard:DrawPlaced"] = 0x3EF123CABED59F7F,
			["UIRethemed.Notifications.HoverCard:PlacedSize"] = 0x45BA184243D45930,
			["UIRethemed.Notifications.Placement:BesideX"] = 0x2A748DD83598C679,
			["AutoCastSpecialistCommands.AutoCastToilInjector:Inject"] = 0x68EDEC90F35FF797,             // Auto-Cast Specialist Commands 1.7, 2026-10-12
			["AutoCastSpecialistCommands.Patch_JobDriver_Mine_MakeNewToils:Postfix"] = 0x9FAC641C9EE82C39,
			["AutoCastSpecialistCommands.Patch_Pawn_GetGizmos:GizmoOnGUIPrefix"] = 0xDA6444EC5E5D0564,
			["AutoCastSpecialistCommands.Patch_Pawn_GetGizmos:GizmoOnGUIPostfix"] = 0xEAEA3D4FF6046A94,
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

		// ---- Factions left without a leader -----------------------------------------------------------------
		// RimPacts' government in exile takes a fallen faction's leader in. When the exile leaves (15 days without a
		// restoration) or is dismissed, DismissExilePawn discards the pawn but never tells its faction, so the
		// faction keeps a discarded leader, saved as null: vanilla FactionTick then logs "Faction leader for X is
		// null" (Iremar). Vanilla picks a successor whenever a leader dies or leaves; do the same, silently: right
		// after RimPacts discards a leader, and after every load for any faction that should have one but doesn't.
		static bool NeedsLeader(Faction f) =>
			!f.IsPlayer && !f.Hidden && !f.temporary && f.def.humanlikeFaction && (f.leader == null || f.leader.Discarded);

		static void RepairMissingLeaders()
		{
			foreach (var f in Find.FactionManager.AllFactionsListForReading)
				if (NeedsLeader(f) && f.TryGenerateNewLeader())
					Log.Message($"[Harmony3StrictCompat] {f.Name} had no leader, new leader {f.leader.LabelShort}");
		}

		static void RpReplaceDismissedLeader(Pawn p)
		{
			if (p == null) return;
			foreach (var f in Find.FactionManager.AllFactionsListForReading)
				if (f.leader == p && f.TryGenerateNewLeader())
					Log.Message($"[Harmony3StrictCompat] RimPacts discarded exiled leader {p.LabelShort}; {f.Name}'s new leader is {f.leader.LabelShort}");
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
	//    Dresser, EndTable and FloodLight whenever the def has a <comps> node, without checking for an existing style
	//    comp. 1.7's ApparelBase already supplies one for the helmet, and every styling mod (VIE Memes, Alpha Memes,
	//    Sophian Style, Medieval 2, Dune) guards its own furniture add. Two comps save sourcePrecept/styleDef twice
	//    ("Tried to register the same load ID twice" on every load; 7 EndTable + 6 Dresser, 2026-10-29; an Alpha
	//    Memes Mesoamerican flood light, 2026-11-05).
	//    Key: conditional xpath -> substring the patch's sourceFile must contain. The other mods' guarded adds use
	//    the very same inner xpath, so the file check is what keeps them alive (v20 emptied them all: no comp,
	//    styles lost).
	static readonly Dictionary<string, string> SkippedConditionals = new()
	{
		["/Defs/ThingDef[defName = \"Apparel_ArmorHelmetMechCommander\"]/comps"] = "3086137468",
		["/Defs/ThingDef[defName = \"Dresser\"]/comps"] = "3086137468",
		["/Defs/ThingDef[defName = \"EndTable\"]/comps"] = "3086137468",
		["/Defs/ThingDef[defName = \"FloodLight\"]/comps"] = "3086137468",
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

	// ---- Sensible Bed Ownership: OwnedBed rescans every bed on every call ----------------------------------------
	// SBO replaces Pawn_Ownership.OwnedBed with Utility.GetMainBed(pawn), which filters every colonist bed on every
	// map for this owner (bedCache.Where(OwnersForReading.Contains).ToList()) and weighs them when there are several.
	// UI drawing, alerts and dozens of mods read OwnedBed: Circinus 2026-11-05 counted 200k-520k calls a minute,
	// 2.3-5.1% of frame, single calls up to 79 ms (150 beds: 123 colony, 27 Hospitality guest beds).
	// Remember SBO's answer per pawn, and only hand it back while nothing it depends on can have changed:
	//  - ownership epoch unchanged (bumped by every vanilla/mod assign, unassign, claim and unclaim method);
	//  - SBO's bed list is the same object and not marked dirty (bed built, destroyed, faction change);
	//  - same preferred bed, same map, the bed still spawned and still listing the pawn as owner;
	//  - at most 250 ticks old (catches anything else, e.g. substructure under a bed).
	// Every 64th hit is recomputed and compared; a mismatch is logged (first one in full) and the entry dropped.
	// A one-time line reports hits/misses/mismatches.
	// Threads: the parallel pawn pre-render jobs read OwnedBed too (Capture Expansion's LayingFacing/GetBodyPos
	// postfixes call IsRestrained -> OwnedBed for every pawn lying in a bed: 95 sleeping animals in animal beds cost
	// ~16 ms a frame, 2026-11-05), so the cache is a ConcurrentDictionary used from any thread. SBO's own bed list
	// (a HashSet rebuilt in place when dirty) is not thread-safe either: a worker reading it while another rebuilds
	// it threw "Collection was modified" inside ParallelGetPreRenderResults. AllAssignedBedsAndDeathrestCaskets now
	// runs under a lock.
	[StaticConstructorOnStartup]
	static class SboOwnedBedCache
	{
		struct Entry
		{
			public Building_Bed bed, preferred;
			public int epoch, tick;
			public Map map;
			public HashSet<Building_Bed> bedList;
		}

		static readonly System.Collections.Concurrent.ConcurrentDictionary<Pawn, Entry> cache = new();
		static readonly object bedListLock = new();
		static int epoch, hits, misses, mismatches, checks;
		static bool reported;
		[ThreadStatic] static bool bypass;
		static Func<Pawn, Building_Bed> mainBed, preferredBed;
		static AccessTools.FieldRef<HashSet<Building_Bed>> bedList;
		static AccessTools.FieldRef<bool> bedListDirty;

		static SboOwnedBedCache()
		{
			var utility = AccessTools.TypeByName("SensibleBedOwnership.Utility");
			if (utility == null) return;
			Harmony3StrictCompatMod.Target("SensibleBedOwnership.Patch_Pawn_Ownership_get_OwnedBed:Prefix");
			var allBeds = Harmony3StrictCompatMod.Target("SensibleBedOwnership.Utility:AllAssignedBedsAndDeathrestCaskets");
			var getMain = Harmony3StrictCompatMod.Target("SensibleBedOwnership.Utility:GetMainBed", args: [typeof(Pawn)]);
			var getPreferred = AccessTools.DeclaredMethod(utility, "GetPreferredBed", [typeof(Pawn)]);
			if (allBeds == null || getMain == null || getPreferred == null || AccessTools.DeclaredField(utility, "bedCache")?.FieldType != typeof(HashSet<Building_Bed>)
				|| AccessTools.DeclaredField(utility, "bedCacheDirty")?.FieldType != typeof(bool))
			{
				Log.Error("[Harmony3StrictCompat] Sensible Bed Ownership changed its bed lookup: OwnedBed cache not applied, recheck it");
				return;
			}
			mainBed = AccessTools.MethodDelegate<Func<Pawn, Building_Bed>>(getMain);
			preferredBed = AccessTools.MethodDelegate<Func<Pawn, Building_Bed>>(getPreferred);
			bedList = AccessTools.StaticFieldRefAccess<HashSet<Building_Bed>>(AccessTools.DeclaredField(utility, "bedCache"));
			bedListDirty = AccessTools.StaticFieldRefAccess<bool>(AccessTools.DeclaredField(utility, "bedCacheDirty"));

			var harmony = new Harmony("sammael.harmony3strictcompat");
			var bump = new HarmonyMethod(typeof(SboOwnedBedCache), nameof(Bump));
			var compMutators = new HashSet<string> { "ForceAddPawn", "ForceRemovePawn", "TryAssignPawn", "TryUnassignPawn", "SortAssignedPawns", "PostSpawnSetup", "PostDeSpawn", "PostSwapMap" };
			int hooked = 0;
			foreach (var type in typeof(CompAssignableToPawn).AllSubclasses().Prepend(typeof(CompAssignableToPawn)))
				foreach (var m in AccessTools.GetDeclaredMethods(type))
					if (compMutators.Contains(m.Name) && !m.IsAbstract && !m.ContainsGenericParameters && m.GetMethodBody() != null)
					{
						harmony.Patch(m, postfix: bump);
						hooked++;
					}
			foreach (var name in new[] { "ClaimBedIfNonMedical", "UnclaimBed", "UnclaimAll", "ClaimDeathrestCasket", "UnclaimDeathrestCasket", "Notify_ChangedGuestStatus" })
				if (AccessTools.DeclaredMethod(typeof(Pawn_Ownership), name) is { } m)
				{
					harmony.Patch(m, postfix: bump);
					hooked++;
				}
			harmony.Patch(AccessTools.Method(typeof(Verse.Profile.MemoryUtility), nameof(Verse.Profile.MemoryUtility.ClearAllMapsAndWorld)),
				postfix: new HarmonyMethod(typeof(SboOwnedBedCache), nameof(Reset)));
			harmony.Patch(allBeds,
				prefix: new HarmonyMethod(typeof(SboOwnedBedCache), nameof(LockBedList)), finalizer: new HarmonyMethod(typeof(SboOwnedBedCache), nameof(UnlockBedList)));
			harmony.Patch(getMain, prefix: new HarmonyMethod(typeof(SboOwnedBedCache), nameof(Lookup)),
				postfix: new HarmonyMethod(typeof(SboOwnedBedCache), nameof(Store)));
			Log.Message($"[Harmony3StrictCompat] Sensible Bed Ownership: OwnedBed cache on ({hooked} ownership methods watched)");
		}

		static void Bump() => System.Threading.Interlocked.Increment(ref epoch);

		static void LockBedList(out bool __state)
		{
			__state = false;
			System.Threading.Monitor.Enter(bedListLock, ref __state);
		}

		static void UnlockBedList(bool __state)
		{
			if (__state) System.Threading.Monitor.Exit(bedListLock);
		}

		static void Reset()
		{
			cache.Clear();
			Bump();
		}

		static bool Lookup(Pawn pawn, ref Building_Bed __result, out bool __state)
		{
			__state = false;
			if (bypass || pawn == null || Current.Game == null) return true;
			__state = true;
			if (!cache.TryGetValue(pawn, out var e) || !Valid(pawn, e)) { misses++; return true; }
			if (++checks % 64 == 0 && !Verify(pawn, e.bed)) { misses++; return true; }
			hits++;
			__result = e.bed;
			__state = false;
			return false;
		}

		static void Store(Pawn pawn, Building_Bed __result, bool __state)
		{
			if (!__state) return;
			if (cache.Count > 4096) cache.Clear();
			cache[pawn] = new Entry
			{
				bed = __result, preferred = preferredBed(pawn), epoch = epoch, tick = Find.TickManager.TicksGame,
				map = pawn.MapHeld, bedList = bedListDirty() ? null : bedList(),
			};
			if (!reported && hits + misses >= 200000)
			{
				reported = true;
				Log.Message($"[Harmony3StrictCompat] Sensible Bed Ownership OwnedBed cache: {hits} hits, {misses} misses, {mismatches} mismatches in {checks / 64} spot checks");
			}
		}

		static bool Valid(Pawn pawn, Entry e)
		{
			int age = Find.TickManager.TicksGame - e.tick;
			return e.epoch == epoch && age >= 0 && age < 250 && e.bedList != null && !bedListDirty() && e.bedList == bedList()
				&& e.map == pawn.MapHeld && e.preferred == preferredBed(pawn)
				&& (e.bed == null || (e.bed.Spawned && e.bed.OwnersForReading.Contains(pawn)));
		}

		// Recompute through SBO (bypassing the cache) and compare with what the cache would have returned.
		static bool Verify(Pawn pawn, Building_Bed cached)
		{
			Building_Bed fresh;
			bypass = true;
			try { fresh = mainBed(pawn); }
			finally { bypass = false; }
			if (fresh == cached) return true;
			cache.TryRemove(pawn, out _);
			if (++mismatches == 1)
				Log.Warning($"[Harmony3StrictCompat] Sensible Bed Ownership OwnedBed cache was stale for {pawn.LabelShort}: cached {cached?.ThingID ?? "no bed"}, " +
					$"SBO now says {fresh?.ThingID ?? "no bed"}. Something changed bed ownership without a watched method; entry dropped. Please report what just happened.");
			return false;
		}
	}

	// ---- Common Sense: "Getting random element from empty collection" on uncraftable food ---------------------
	// With "add meal ingredients" on, its ThingMaker.MakeThing postfix gives a food item with an empty ingredient
	// list the ingredients of a random recipe that makes it. For food no recipe makes, the pick is from an empty
	// list: a warning, and since the miss is never cached, a fresh scan of every recipe plus another warning each
	// time such a stack is split (hauling). Skip the postfix for defs no ingredient recipe produces, which is exactly
	// when it would find nothing. Patched once defs are final, so the product set includes generated recipes.
	[StaticConstructorOnStartup]
	static class CommonSenseUncraftableFood
	{
		static readonly HashSet<ThingDef> products = new();

		static CommonSenseUncraftableFood()
		{
			var postfix = Harmony3StrictCompatMod.Target("CommonSense.ThingMaker_MakeThing_CommonSensePatch:Postfix");
			if (postfix == null) return;
			foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
				if (!recipe.ingredients.NullOrEmpty() && recipe.products != null)
					foreach (var p in recipe.products)
						if (p?.thingDef != null) products.Add(p.thingDef);
			new Harmony("sammael.harmony3strictcompat").Patch(postfix,
				prefix: new HarmonyMethod(typeof(CommonSenseUncraftableFood), nameof(HasRecipe)));
		}

		static bool HasRecipe(ThingDef def) => def == null || products.Contains(def);
	}

	// ---- Surgery menu icons (personal tweak, not a fix) -------------------------------------------------------
	// The operations float menu (HealthCardUtility.GenerateSurgeryOption) shows recipe.UIIconThing for everything
	// except drugs and body part removal, and that is only set when the recipe declares uiIconThing or makes exactly
	// one product. Install and implant removal recipes almost never do, so they get vanilla's grey placeholder.
	// Fill the icon once at startup: the non-medicine fixed ingredient (the implant being installed), else the
	// item the removed/added hediff drops. Recipes that already have an icon are left alone.
	[StaticConstructorOnStartup]
	static class SurgeryIcons
	{
		static SurgeryIcons()
		{
			var iconThing = AccessTools.FieldRefAccess<RecipeDef, ThingDef>("uiIconThing");
			int filled = 0;
			foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
			{
				if (!recipe.IsSurgery || recipe.UIIconThing != null || recipe.UIIcon != null || recipe.workerClass == null) continue;
				if (typeof(Recipe_AdministerIngestible).IsAssignableFrom(recipe.workerClass)
					|| typeof(Recipe_RemoveBodyPart).IsAssignableFrom(recipe.workerClass)) continue;   // own icon source
				var icon = recipe.ingredients?.FirstOrDefault(i => i.IsFixedIngredient && !i.FixedIngredient.IsMedicine)?.FixedIngredient
					?? recipe.removesHediff?.spawnThingOnRemoved ?? recipe.addsHediff?.spawnThingOnRemoved;
				if (icon == null) continue;
				iconThing(recipe) = icon;
				filled++;
			}
			Log.Message($"[Harmony3StrictCompat] Surgery icons filled for {filled} recipes");
		}
	}

	// ---- Hospitality vending machine works as a fridge (personal tweak, not a fix) ---------------------------
	// 1.7/Patches/VendingMachine_Fridge.xml raises its draw to 100 W. While it has power, items stored in it read
	// the same temperature as in [sbz] Fridge's fridges (ambient - 100, floored at -10 C), so food freezes and stops
	// rotting. Done here instead of turning it into an Adaptive Storage thing because already-built machines keep
	// their saved class and would need rebuilding.
	[StaticConstructorOnStartup]
	static class VendingFridge
	{
		static readonly ThingDef machine = DefDatabase<ThingDef>.GetNamedSilentFail("VendingMachine");

		static VendingFridge()
		{
			if (machine == null) return;
			new Harmony("sammael.harmony3strictcompat").Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.AmbientTemperature)),
				postfix: new HarmonyMethod(typeof(VendingFridge), nameof(Chill)));
		}

		static void Chill(Thing __instance, ref float __result)
		{
			if (__result <= -10f || __instance.def.category != ThingCategory.Item || !__instance.Spawned) return;
			if (__instance.Map.edificeGrid[__instance.Position] is not Building b || b.def != machine) return;
			if (b.GetComp<CompPowerTrader>() is { PowerOn: false }) return;
			__result = Mathf.Max(__result - 100f, -10f);
		}
	}

	// ---- Hauler's Dream x Kanban Stockpile Continue: HD overfills Kanban's max stack size --------------------
	// Kanban caps a storage's stacks (max stack size) by clamping vanilla HaulToCellStorageJob / HaulToStorageJob and
	// by refusing to absorb into a stack already at the cap. HD plans its bulk pick-up from vanilla
	// GetItemStackSpaceLeftFor (a full stackLimit per slot) and its inventory unload drops the whole carried stack on
	// the cell it found, so vending machines capped at 25 held stacks of 248 and 48 insect cheese. Kanban's "remove
	// excess" then hauls the surplus out and HD hauls it back whenever there is room: churn, and HD's net-zero
	// warnings. While HD measures storage space, each slot of a capped storage counts at the cap. When HD unloads onto
	// a capped cell it drops only what that cell may still take; the rest stays in inventory and HD's unload loop
	// finds it the next cell. Storage without a Kanban max stack size is untouched.
	[StaticConstructorOnStartup]
	static class HdKanbanStackCap
	{
		delegate bool KanbanGet(IStoreSettingsParent owner, out int srt, out int ssl, out int mss);

		static readonly KanbanGet kanbanGet;
		static readonly Func<bool> hdInsideSpaceScan;
		static readonly FieldInfo countToDrop;

		static HdKanbanStackCap()
		{
			var get = Harmony3StrictCompatMod.Target("KanbanStockpileContinue.KanbanStockpileAPI:TryGetSettings");
			var scan = Harmony3StrictCompatMod.Target("HaulersDream.StorageCommitments:get_InsideSpaceScan");
			var find = Harmony3StrictCompatMod.Target("HaulersDream.JobDriver_UnloadHauledInventory:FindTargetOrDrop");
			Harmony3StrictCompatMod.Target("HaulersDream.JobDriver_UnloadHauledInventory+<>c__DisplayClass15_0:<FindTargetOrDrop>b__0"); // fingerprint only: the toil body wrapped below
			if (get == null || scan == null || find == null) return;
			countToDrop = AccessTools.Field(find.DeclaringType, "countToDrop");
			if (countToDrop == null)
			{
				Log.Error("[Harmony3StrictCompat] HaulersDream.JobDriver_UnloadHauledInventory.countToDrop no longer exists: its mod updated, remove or redo this fix");
				return;
			}
			kanbanGet = (KanbanGet)get.CreateDelegate(typeof(KanbanGet));
			hdInsideSpaceScan = (Func<bool>)scan.CreateDelegate(typeof(Func<bool>));
			var harmony = new Harmony("sammael.harmony3strictcompat");
			harmony.Patch(AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetItemStackSpaceLeftFor)),
				postfix: new HarmonyMethod(typeof(HdKanbanStackCap), nameof(CapSpace)));
			harmony.Patch(find, postfix: new HarmonyMethod(typeof(HdKanbanStackCap), nameof(WrapFindToil)));
		}

		// Same sum as vanilla (room in same-def stacks + free slots), with mss in place of stackLimit. New stacks per
		// cell are also capped at the similar stack limit; Kanban's own cell check enforces it group-wide.
		static void CapSpace(IntVec3 c, Map map, ThingDef itemDef, ref int __result)
		{
			if (__result <= 0 || !hdInsideSpaceScan() || map.haulDestinationManager.SlotGroupAt(c) is not { } group
				|| !kanbanGet(group.Settings.owner, out _, out int ssl, out int mss) || mss <= 0 || mss >= itemDef.stackLimit) return;
			int room = 0, items = 0;
			foreach (var t in map.thingGrid.ThingsListAt(c))
			{
				if (t.def.category == ThingCategory.Item) items++;
				if (t.def == itemDef) room += Math.Max(mss - t.stackCount, 0);
			}
			int newStacks = Math.Max(c.GetMaxItemsAllowedInCell(map) - items, 0);
			if (ssl > 0) newStacks = Math.Min(newStacks, ssl);
			__result = Math.Min(__result, room + newStacks * mss);
		}

		static void WrapFindToil(JobDriver __instance, Toil __result)
		{
			var find = __result.initAction;
			__result.initAction = () =>
			{
				find();
				CapDrop(__instance);
			};
		}

		// The drop lands in the first stack below the cap (Kanban refuses to absorb into one at the cap), else as a
		// new stack.
		static void CapDrop(JobDriver driver)
		{
			var job = driver.job;
			if (driver.ended || job.targetB.HasThing || !job.targetB.Cell.IsValid || job.targetA.Thing is not { } thing) return;
			var map = driver.pawn.Map;
			int count = (int)countToDrop.GetValue(driver);
			if (count <= 0 || map?.haulDestinationManager.SlotGroupAt(job.targetB.Cell) is not { } group
				|| !kanbanGet(group.Settings.owner, out _, out _, out int mss) || mss <= 0) return;
			int cap = Math.Min(thing.def.stackLimit, mss), room = cap;
			foreach (var t in map.thingGrid.ThingsListAt(job.targetB.Cell))
				if (t != thing && t.CanStackWith(thing) && t.stackCount < cap)
				{
					room = cap - t.stackCount;
					break;
				}
			if (room < count) countToDrop.SetValue(driver, room);
		}
	}

	// ---- VTE Variations "choose graphic" on Adaptive Storage furniture ---------------------------------------
	// Reel's Expanded Storage rebuilds VFE furniture (wardrobe, royal dresser) as Adaptive Storage buildings. ASF
	// hides the base graphic, draws the texture from the def's GraphicsDef instead and caches it until its renderer
	// is dirtied. VEF's CompRandomBuildingGraphic (VTE Variations' choose / randomize graphic) only swaps
	// Thing.graphicInt, so nothing changes on screen, not even after reload. For ASF defs that carry the VEF comp: a
	// GraphicsDef layer whose texture is one of the comp's variants draws the chosen variant (same graphic class,
	// shader, size and colours), and ChangeGraphic marks the renderer dirty. Every other building is untouched.
	[StaticConstructorOnStartup]
	static class VefGraphicOnAdaptiveStorage
	{
		static readonly HashSet<ThingDef> defs = new();
		static Type compType;
		static MethodInfo rendererOf, parentOf, setDirty;
		static AccessTools.FieldRef<object, string> multiPath, singlePath;
		static AccessTools.FieldRef<object, List<string>> variants;

		static VefGraphicOnAdaptiveStorage()
		{
			var change = Harmony3StrictCompatMod.Target("VEF.Buildings.CompRandomBuildingGraphic:ChangeGraphic");
			var getGraphic = Harmony3StrictCompatMod.Target("AdaptiveStorage.StorageGraphicWorker:GetGraphicFor");
			if (change == null || getGraphic == null) return;
			compType = change.DeclaringType;
			var asfThing = AccessTools.TypeByName("AdaptiveStorage.ThingClass");
			var renderer = AccessTools.TypeByName("AdaptiveStorage.StorageRenderer");
			var propsType = AccessTools.TypeByName("VEF.Buildings.CompProperties_RandomBuildingGraphic");
			rendererOf = asfThing == null ? null : AccessTools.PropertyGetter(asfThing, "Renderer");
			parentOf = renderer == null ? null : AccessTools.PropertyGetter(renderer, "Parent");
			setDirty = renderer == null ? null : AccessTools.Method(renderer, "SetAllPrintDatasDirty");
			if (rendererOf == null || parentOf == null || setDirty == null || propsType == null
				|| AccessTools.Field(compType, "newGraphicPath") == null || AccessTools.Field(compType, "newGraphicSinglePath") == null
				|| AccessTools.Field(propsType, "randomGraphics") == null)
			{
				Log.Error("[Harmony3StrictCompat] Adaptive Storage or VEF members renamed: its mod updated, VTE Variations graphic fix for storage furniture skipped");
				return;
			}
			multiPath = AccessTools.FieldRefAccess<string>(compType, "newGraphicPath");
			singlePath = AccessTools.FieldRefAccess<string>(compType, "newGraphicSinglePath");
			variants = AccessTools.FieldRefAccess<List<string>>(propsType, "randomGraphics");
			foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
				if (def.thingClass != null && asfThing.IsAssignableFrom(def.thingClass) && def.comps.Any(propsType.IsInstanceOfType))
					defs.Add(def);
			if (defs.Count == 0) return;
			var harmony = new Harmony("sammael.harmony3strictcompat");
			harmony.Patch(getGraphic, postfix: new HarmonyMethod(typeof(VefGraphicOnAdaptiveStorage), nameof(UseChosenVariant)) { uncheckedReferenceBinding = true });
			harmony.Patch(change, postfix: new HarmonyMethod(typeof(VefGraphicOnAdaptiveStorage), nameof(Redraw)));
		}

		static ThingComp CompOf(ThingWithComps thing)
		{
			foreach (var comp in thing.AllComps)
				if (compType.IsInstanceOfType(comp)) return comp;
			return null;
		}

		// __1 is the StorageRenderer, typed object because this assembly does not reference ASF.
		static void UseChosenVariant(object __1, ref Graphic __result)
		{
			if (__result == null || __1 == null) return;
			if (parentOf.Invoke(__1, null) is not ThingWithComps parent || !defs.Contains(parent.def)) return;
			if (CompOf(parent) is not { } comp) return;
			var chosen = multiPath(comp);
			if (chosen.NullOrEmpty()) chosen = singlePath(comp);
			if (chosen.NullOrEmpty() || chosen == __result.path) return;
			var options = variants(comp.props);
			if (options == null || !options.Contains(__result.path) || !options.Contains(chosen)) return;
			__result = GraphicDatabase.Get(__result.GetType(), chosen, __result.Shader, __result.drawSize, __result.color,
				__result.colorTwo, __result.data, __result.data?.shaderParameters);
		}

		static void Redraw(ThingComp __instance)
		{
			var parent = __instance.parent;
			if (parent == null || !parent.Spawned || !defs.Contains(parent.def)) return;
			if (rendererOf.Invoke(parent, null) is not { } renderer) return;
			setDirty.Invoke(renderer, null);
			parent.DirtyMapMesh(parent.Map);
		}
	}

	// ---- More Phytokin Xenotypes: Modules/Anomaly shadowed by Modules/Odyssey ------------------------------
	// With Anomaly + Odyssey + Starjack active, MPX (3813857865, 0.1.1) loads both module folders; six Defs files
	// share the same relative path in both and the game keeps one file per path (last load folder wins), so the
	// carrionkin / harbingerkin defs never load. LocalTweaks/MorePhytokinXenotypes (its LoadFolders.xml, same condition)
	// ships byte copies of those files under other names, see the patch there (LocalTweaks because ParentName resolves
	// only from mods loaded earlier, and this mod loads before Core). The copies go stale silently, so compare
	// them against the upstream files at startup: a changed or missing upstream file is a red error.
	[StaticConstructorOnStartup]
	static class MpxAnomalyCopies
	{
		static MpxAnomalyCopies()
		{
			var mpx = LoadedModManager.RunningMods.FirstOrDefault(m => m.PackageId == "pumpkin.morephytokinxenotypes");
			if (mpx == null || !ModsConfig.AnomalyActive || !ModsConfig.OdysseyActive || !ModsConfig.IsActive("vanillaracesexpanded.starjack")) return;
			var tweaks = LoadedModManager.RunningMods.FirstOrDefault(m => m.PackageId == "sammael.localtweaks");
			if (tweaks == null) { Log.Error("[Harmony3StrictCompat] More Phytokin Xenotypes needs LocalTweaks (MorePhytokinXenotypes def copies), not active"); return; }
			var copies = Path.Combine(tweaks.RootDir, "MorePhytokinXenotypes", "Defs");
			foreach (var f in new[] { "AbilityDefs/Abilities", "GeneDefs/Genes", "IncidentDefs/Incidents", "ThingDefs/Things", "ThoughtDefs/Thoughts", "XenotypeDefs/Xenotypes" })
			{
				var theirs = Path.Combine(mpx.RootDir, "Modules", "Anomaly", "Defs", f + ".xml");
				var ours = Path.Combine(copies, Path.GetDirectoryName(f), "MPX_Anomaly_" + Path.GetFileName(f) + ".xml");
				if (!File.Exists(theirs))
					Log.Error($"[Harmony3StrictCompat] More Phytokin Xenotypes no longer ships Modules/Anomaly/Defs/{f}.xml: its mod updated, remove LocalTweaks/MorePhytokinXenotypes and its LoadFolders.xml entry");
				else if (!File.ReadAllBytes(theirs).SequenceEqual(File.ReadAllBytes(ours)))
					Log.Error($"[Harmony3StrictCompat] More Phytokin Xenotypes changed Modules/Anomaly/Defs/{f}.xml: its mod updated, refresh the copy {ours}");
			}
		}
	}
}
