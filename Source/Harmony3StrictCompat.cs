using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Profile;

namespace Harmony3StrictCompat
{
	// Local stop-gap fixes for RimWorld 1.7 (Harmony 3, Unity replaced by UnityStubs).
	// Every entry is temporary: remove it once the affected mod ships its own fix.
	public class Harmony3StrictCompatMod : Mod
	{
		public Harmony3StrictCompatMod(ModContentPack content) : base(content)
		{
			ArchivePreviousLogs();
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
			harmony.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker), "ShouldHaveNeed"),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(NoNeedsBeforeWorld)) { priority = Priority.First });
			harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
				postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(DropWorldCopiesOfShipPawns)));
			harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
				postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RepairMissingLeaders)));
			if (Target("TD_Enhancement_Pack.Alert_Deterioration:get_DeterioratingThings") is { } tdDeteriorating)
			{
				Target("TD_Enhancement_Pack.Alert_Deterioration+<get_DeterioratingThings>d__1:MoveNext"); // fingerprint only: the iterator body filtered below
				harmony.Patch(tdDeteriorating, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(TdDeteriorationNoCorpses)));
			}
			var tdSkillArrow = Target("TD_Enhancement_Pack.SkillLearningIndicator:LabelLearning");
			var uirCardSkill = Target("UIRethemed.ColonistCard.Glance:DrawSkill");
			if (tdSkillArrow != null && uirCardSkill != null)
			{
				tdLearningArrow = (Action<SkillRecord, Rect, float>)tdSkillArrow.CreateDelegate(typeof(Action<SkillRecord, Rect, float>));
				uirCardSettings = AccessTools.Field(AccessTools.TypeByName("UIRethemed.ColonistCard.UIRethemedColonistCardMod"), "Settings");
				uirSkillLevelLeft = AccessTools.Field(AccessTools.TypeByName("UIRethemed.ColonistCard.UIRethemedColonistCardSettings"), "skillLevelLeft");
				harmony.Patch(uirCardSkill, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirCardSkillArrow)));
			}
			foreach (var levelUpHook in new[] { Target("LevelUp.Patcher:DirtyAptitudesPostfix"), Target("LevelUp.Patcher:LearnPostfix") })
				if (levelUpHook != null)
					harmony.Patch(levelUpHook, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LevelUpOnlyPresentPawns)));
			if (Target("Verse.HediffComp_MessageBase:Message") is { } hediffMessage)
				harmony.Patch(hediffMessage, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HediffMessageNeedsText)));
			if (Target("KanbanStockpileContinue.WorkGiver_RemoveExcessFromKanban:HasJobOnThing") is { } ksHasJob
				&& Target("KanbanStockpileContinue.WorkGiver_RemoveExcessFromKanban:JobOnThing") != null
				&& Target("KanbanStockpileContinue.KanbanStockpileAPI:InvokeExcessRemoved") is { } ksExcessEvent)
			{
				harmony.Patch(ksHasJob, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(KsRemoveExcessHasRealJob)));
				harmony.Patch(ksExcessEvent, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(KsNoEventWhileProbing)));
			}
			if (Target("VEF.Weapons.TeslaProjectile:Impact") is { } teslaImpact)
				harmony.Patch(teslaImpact, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(TeslaFireVanillaRulesTranspiler)));
			if (AccessTools.TypeByName("PipeSystem.Building_Pipe") is { } pipeClass)
			{
				vefPipeClass = pipeClass;
				foreach (var monumentCheck in new[] { Target("RimWorld.PlaceWorker_MonumentMarker:AllowsPlacing"), Target("RimWorld.MonumentMarker:get_FirstDisallowedBuilding") })
					if (monumentCheck != null)
						harmony.Patch(monumentCheck, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(MonumentPipesLikeConduitsTranspiler)));
			}
			var vqeaDeliver = Target("VanillaQuestsExpandedAncients.Building_PneumaticTubeLaunchPort:Deliver");
			if (vqeaDeliver != null)
				harmony.Patch(vqeaDeliver, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(VqeaCapsuleContentsTranspiler)));
			var turretRadius = Target("RimWorld.PlaceWorker_ShowTurretRadius:AllowsPlacing");
			if (turretRadius != null)
				harmony.Patch(turretRadius, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(TurretRadiusAnyVerb)));
			if (AccessTools.TypeByName("AlphaBooks.StaticCollections") is { } abCollections
				&& AccessTools.TypeByName("AlphaBooks.AlphaBooks_JobDriver_Reading_ReadBook_Patch") == null
				&& Target("RimWorld.JobDriver_Reading:ReadBook") is { } readBook)
			{
				abRuinedBooks = AccessTools.Field(abCollections, "ruinedBooks");
				jobFinalizer = AccessTools.FieldRefAccess<JobDriver, Func<JobCondition, Job>>("finalizerJobFactory");
				harmony.Patch(readBook, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AbRuinedBookCrumbles)));
			}
			if (AccessTools.TypeByName("AlphaBooks.StaticCollections") is { } abCollectionsFamily
				&& Target("LivingBonds.JobDriver_FamilyReading:MakeNewToils") is { } familyReading)
			{
				abRuinedBooks ??= AccessTools.Field(abCollectionsFamily, "ruinedBooks");
				// The stub only builds the iterator; the toils live in MoveNext, so fingerprint that too.
				Fingerprint("LivingBonds.JobDriver_FamilyReading:MakeNewToils/MoveNext", AccessTools.EnumeratorMoveNext(familyReading));
				harmony.Patch(familyReading, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AbFamilyReadBookCrumbles)));
			}
			if (AccessTools.TypeByName("AlphaBooks.StaticCollections") is { } abCollectionsPolicy
				&& Target("AlphaBooks.AlphaBooks_Dialog_ManageReadingPolicies_PolicyGlobalFilter_Patch:ModifyPolicyFilter") != null
				&& Target("RimWorld.Dialog_ManageReadingPolicies:get_PolicyGlobalFilter") is { } policyGlobalFilter)
			{
				abRuinedBooks ??= AccessTools.Field(abCollectionsPolicy, "ruinedBooks");
				// Priority.Last: must run after Alpha Books' own postfix, which swaps in its filtered copy.
				harmony.Patch(policyGlobalFilter, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AbRuinedBooksInReadingPolicy)) { priority = Priority.Last });
			}
			if (AccessTools.TypeByName("AlphaBooks.StaticCollections") is { } abCollectionsPick
				&& Target("AlphaBooks.ReadingOutcomeDoer_Text:DoesProvidesOutcome") != null
				&& Target("Verse.BookUtility:TryGetRandomBookToRead") is { } randomBook)
			{
				abRuinedBooks ??= AccessTools.Field(abCollectionsPick, "ruinedBooks");
				harmony.Patch(randomBook, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AbRuinedBooksInRandomPoolTranspiler)));
			}
			foreach (var readStart in new[] { Target("RimWorld.JobDriver_Reading:Notify_Starting"), Target("AlphaBooks.JobDriver_ReadAlphaBook:Notify_Starting") })
				if (readStart != null)
					harmony.Patch(readStart, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(OrderedReadUnforbidsBook)));
			if (Target("RimWorld.PawnRelationWorker_Sibling:CreateRelation") is { } siblingRelation)
				harmony.Patch(siblingRelation, finalizer: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SiblingRelationGuard)));
			var normalCanGenerate = Target("RimWorld.PawnGroupKindWorker_Normal:CanGenerateFrom");
			if (normalCanGenerate != null)
				harmony.Patch(normalCanGenerate, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(NormalGroupYieldsPawns)));
			var srDuration = Target("SkilledRefueling.HarmonyPatches:GetRefuelDuration");
			var srSpeed = Target("SkilledRefueling.RefuelingUtility:RefuelingSpeedForPawn");
			if (srDuration != null && srSpeed != null)
			{
				srOwnerField = AccessTools.DeclaredField(srDuration.DeclaringType, "JD_RefuelField");
				if (srOwnerField == null)
					Log.Error("[Harmony3StrictCompat] SkilledRefueling.HarmonyPatches.JD_RefuelField no longer exists: its mod updated, remove or redo this fix");
				else
				{
					srRefuelSpeed = (Func<Pawn, float>)srSpeed.CreateDelegate(typeof(Func<Pawn, float>));
					harmony.Patch(srDuration, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SrRefuelDuration)));
				}
			}
			var rpDismiss = Target("RimPacts.WorldComponent_RimPacts:DismissExilePawn");
			if (rpDismiss != null)
				harmony.Patch(rpDismiss, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpReplaceDismissedLeader)));
			var rpFail = Target("RimPacts.WorldComponent_RimPacts:ApplyProtectionFailure");
			rpIsProtected = Target("RimPacts.WorldComponent_RimPacts:IsProtectedByTradePact");
			if (rpFail != null && rpIsProtected != null)
				harmony.Patch(rpFail, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpProtectionOnlyRoutes)));
			if (Target("RimPacts.WorldComponent_RimPacts:TryRevertConquered") is { } rpRevert
				&& Target("RimPacts.WorldComponent_RimPacts:OriginalOwnerOf") is { } rpOwner)
			{
				rpOriginalOwnerOf = rpOwner;
				rpAbsorbPlans = AccessTools.Field(rpRevert.DeclaringType, "absorbPlans");
				rpPlanLoser = AccessTools.Field(AccessTools.Inner(rpRevert.DeclaringType, "AbsorbPlan"), "loser");
				if (rpAbsorbPlans != null && rpPlanLoser != null)
					harmony.Patch(rpRevert, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpNoRevoltToAbsorbed)));
			}
			var rpTick = Target("RimPacts.WorldComponent_RimPacts:WorldComponentTick");
			rpDefAutoTick = Target("RimPacts.WorldComponent_RimPacts:DefAutoTick");
			Target("RimPacts.WorldComponent_RimPacts:DefAutoOne"); // fingerprint only: applies the pending wounds DefAutoTick reaches
			if (rpTick != null && rpDefAutoTick != null)
			{
				rpDefendMission = AccessTools.PropertyGetter(rpTick.DeclaringType, "DefendMission");
				rpDefAutoRecs = AccessTools.Field(rpTick.DeclaringType, "defAutoRecs");
				rpDefAutoSettled = AccessTools.Field(AccessTools.TypeByName("RimPacts.DefAutoRec"), "settled");
				if (rpDefendMission != null && rpDefAutoRecs != null && rpDefAutoSettled != null)
					harmony.Patch(rpTick, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpAutoDefenseFinishes)));
				else
					Log.Error("[Harmony3StrictCompat] RimPacts DefendMission/defAutoRecs changed: its mod updated, redo the auto-defense wounds fix");
			}
			if (Target("RimPacts.WorldComponent_RimPacts:WardLordPawns") is { } rpWard && Target("RimPacts.HediffComp_RptDropOnPlayerOwned:CompPostTick") is { } rpWardTick
				&& AccessTools.PropertyGetter("RimPacts.RimPactsMod:Settings") is { } rpSettingsGet && AccessTools.Field("RimPacts.RimPactsSettings:wardTraderTemp") is { } rpWardOn)
			{
				rpSettingsGetter = rpSettingsGet;
				rpWardTraderTemp = rpWardOn;
				harmony.Patch(rpWard, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpWardOnlyWhenEnabled)));
				harmony.Patch(rpWardTick, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpWardDropWhenDisabled)));
			}
			if (Target("RimPacts.WorldComponent_RimPacts:CaravanGuardStep") is { } rpGuardStep
				&& Target("DressForTheWeather.PawnsArriveUtilities:DressPawnAppropriately") is { } dftwDress
				&& Target("DressForTheWeather.PawnsArriveUtilities:InitializeApparelPairs") is { } dftwPairs)
			{
				Target("DressForTheWeather.IncidentWorker_NeutralGroup_SpawnPawns:Postfix"); // fingerprint only: the hook these guards miss
				dftwDressCall = (Action<Pawn, Map>)dftwDress.CreateDelegate(typeof(Action<Pawn, Map>));
				dftwPairsCall = (Action)dftwPairs.CreateDelegate(typeof(Action));
				harmony.Patch(rpGuardStep, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpGuardStepBegin)),
					finalizer: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpGuardStepEnd)));
				harmony.Patch(AccessTools.Method(typeof(Lord), nameof(Lord.AddPawn)), postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RpGuardDressForWeather)));
			}
			guiWrapRn = AccessTools.TypeByName("RimworldReadableNumbers.RnGuiBackend");
			guiWrapBsb = AccessTools.TypeByName("BetterScrollBar.ScrollBarBackend");
			if ((guiWrapRn != null || guiWrapBsb != null) && AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost") is { } imguiHost
				&& AccessTools.Inner(imguiHost, "Activation") is { } activation && AccessTools.DeclaredConstructor(activation, [imguiHost]) is { } activationCtor)
			{
				Fingerprint("RimWorldWaylith.Imgui.ImGuiHost+Activation:.ctor", activationCtor);
				harmony.Patch(activationCtor, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(GuiHostKeepModBackends)));
			}
			// Too Many Mods' Merchant Guild x Vehicle Framework patches fail on VF 1.7; Source/Vehicles replaces them.
			if (AccessTools.TypeByName("TooManyMods.Compat_MerchantGuild_TradeArrivalOption")?.TypeInitializer is { } tmmGuild)
			{
				Fingerprint("TooManyMods.Compat_MerchantGuild_TradeArrivalOption:.cctor", tmmGuild);
				harmony.Patch(tmmGuild, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SkipOriginal)));
				tmmGuildSkipped = true;
			}
			if (Target("Rachek128.RitualAttenuation.RitualExtendedDataRef:get_Comp") is { } raComp)
			{
				raLord = AccessTools.FieldRefAccess<Lord>(raComp.DeclaringType, "lord");
				raCompIndex = AccessTools.FieldRefAccess<int>(raComp.DeclaringType, "compIndex");
				harmony.Patch(raComp, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RaCompWithoutPrecept)));
			}
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
			var guMountable = Target("GiddyUp.IsMountableUtility:IsMountable");
			if (guMountable != null)
				harmony.Patch(guMountable, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(GuNoForbiddenMount)));
			var vsieTeach = Target("VanillaSocialInteractionsExpanded.InteractionWorker_Teaching:Interacted");
			if (vsieTeach != null)
			{
				vsieSkillToTeach = AccessTools.Method(vsieTeach.DeclaringType, "GetSkillDefToTeach");
				vsieManager = AccessTools.PropertyGetter(AccessTools.TypeByName("VanillaSocialInteractionsExpanded.VSIE_Utils"), "SocialInteractionsManager");
				vsiePupils = AccessTools.Field(AccessTools.TypeByName("VanillaSocialInteractionsExpanded.SocialInteractionsManager"), "teachersWithPupils");
				vsieTopic = AccessTools.Constructor(AccessTools.TypeByName("VanillaSocialInteractionsExpanded.TeachingTopic"), [typeof(Pawn), typeof(SkillDef)]);
				if (vsieSkillToTeach != null && vsieManager != null && vsiePupils != null && vsieTopic != null)
					harmony.Patch(vsieTeach, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(VsieTeachNeedsTopic)));
				else
					Log.Error("[Harmony3StrictCompat] VSIE teaching topic members no longer exist: its mod updated, remove or redo this fix");
			}
			var denDoors = Target("PositiveConnectionsNmSpc.RoomRoleWorker_Den:CountDoors");
			if (denDoors != null)
				harmony.Patch(denDoors, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(DenCountDoorsNullSafe)));
			var gojiParrot = Target("GojisMiscGenes.InteractionWorker_Interacted_Patch:Postfix");
			if (gojiParrot != null)
				harmony.Patch(gojiParrot, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(GojiParrotNeedsTracker)));
			harmony.Patch(AccessTools.Method(typeof(TraderCaravanUtility), nameof(TraderCaravanUtility.GetTraderCaravanRole)),
				postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(TraderRoleByTraderKind)));
			// Hospitality caches CompGuest per pawn (CompUtility.guestComps) and only drops entries on Pawn.DeSpawn.
			// 1.7 made Pawn.Equals/GetHashCode compare thingIDNumber (+ def), so after loading another save in the same
			// session the new pawn objects hit the old game's entries. Clear it whenever a game is thrown away.
			hospGuestComps = AccessTools.Field(AccessTools.TypeByName("Hospitality.Utilities.CompUtility"), "guestComps");
			if (hospGuestComps != null)
				harmony.Patch(AccessTools.Method(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(ClearHospitalityGuestComps)));
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
			if (Target("Hospitality.JoyGiver_BuyStuff:CanEat") is { } hospCanEat)
				harmony.Patch(hospCanEat, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HospTitleFood)));
			if (Target("Hospitality.JoyGiver_BuyStuff:Qualifies") is { } hospQualifies)
				harmony.Patch(hospQualifies, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HospTitleFoodQualifies)));
			var hospPocket = Target("Hospitality.Utilities.ItemUtility:PocketHeadgear");
			if (hospPocket != null)
				harmony.Patch(hospPocket, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HospKeepCasketHelmet)));
			var svPrefix = Target("SmarterVisitors.Trigger_TicksPassed_ActivateOn:Prefix");
			svStayTimer = AccessTools.TypeByName("Hospitality.Trigger_TicksPassedAndOkayToLeave");
			if (svPrefix != null && svStayTimer != null)
				harmony.Patch(svPrefix, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SvOnlyStayTimer)),
					finalizer: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SvAskDone)));
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
			Target("RimWorld.Selector:SelectUnderMouse"); // fingerprint only: its lambda numbering can shift on a game update
			if (Target("RimWorld.Selector+<>c:<SelectUnderMouse>b__45_0") is { } selectOrder)
				harmony.Patch(selectOrder, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PlantGrowerSelectedFirst)));
			if (Target("Verse.GizmoGridDrawer:DrawGizmoGrid") is { } gizmoGrid)
				harmony.Patch(gizmoGrid, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(GizmoHeightCoversShrunkRows)));
			// Vanilla: CompShuttle evicts quest escorts whose last despawn is years old; fingerprinted so a vanilla change gets re-checked.
			Target("RimWorld.CompShuttle:CompTick");
			RechargeBarInside.Init(harmony);
			GravshipLaunchKeptMapNoWarning.Init(harmony);
			FleetHoldLetsLeaversGo.Init(harmony);
			BasGravshipFlagForVge.Init(harmony);
			VqeArcComponentFindsComponent.Init(harmony);
			harmony.Patch(AccessTools.Method(typeof(CompTransporter), nameof(CompTransporter.Notify_ThingAdded)),
				postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(ShuttleClockStartsAtBoarding)));
			foreach (var csClean in new[] { "CommonSense.Utility+<>c__DisplayClass21_0:<CleanFilthToil>b__0", "CommonSense.Utility+<>c__DisplayClass21_0:<CleanFilthToil>b__1" })
				if (Target(csClean) is { } csToil)
					harmony.Patch(csToil, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CsFilthGone)));
			if (Target("CommonSense.OpportunisticTasks:Cleaning_Opportunity") is { } csOpportunity)
				harmony.Patch(csOpportunity, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CsNoSweepBeforeMedicalRest)));
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
			var hdDelivering = Target("HaulersDream.StorageCommitments:IsDelivering");
			if (hdDelivering != null)
				harmony.Patch(hdDelivering, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdInventoryIsDelivering)));
			if (Target("HaulersDream.Alert_CannotUnloadInventory:Evaluate") is { } hdUnloadAlert)
				harmony.Patch(hdUnloadAlert, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdNoUnloadAlertWhileImmobile)));
			if (AccessTools.TypeByName("UniqueMeleeWeapons.QuestPart_SpawnWarband") is { } umwWarband
				&& AccessTools.Field(umwWarband, "pawns") is { } umwPawns && umwPawns.FieldType == typeof(List<Pawn>))
			{
				umwWarbandPart = umwWarband;
				umwWarbandPawns = umwPawns;
				harmony.Patch(AccessTools.Method(typeof(Quest), nameof(Quest.CleanupQuestParts)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UmwDiscardWarband)));
				harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UmwDiscardEndedWarbands)));
			}
			// TEMPORARY probe (2026-11-18): what a job that restarts ten times a tick points at. Remove once the loops are solved.
			harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob)),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(JobLoopProbeRemember)));
			harmony.Patch(AccessTools.Method(typeof(JobUtility), nameof(JobUtility.TryStartErrorRecoverJob)),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(JobLoopProbe)));
			Target("HaulersDream.Patch_WorkGiver_DoBill_BatchRoute:Postfix"); // fingerprint only: the one route that can use another pawn's inventory stack
			var hdShare = Target("HaulersDream.InventoryShare:AddSharableStacksForBill");
			hdIsBatchBill = Target("HaulersDream.HaulersDreamGameComponent:IsBatchBill");
			hdCanPawnBatch = Target("HaulersDream.CraftBatchPlanner:CanPawnBatch");
			hdInstance = AccessTools.PropertyGetter(AccessTools.TypeByName("HaulersDream.HaulersDreamGameComponent"), "Instance");
			// Fingerprinted here, before this mod's own postfix below: the IL read back from a method that is
			// already patched differs on this runtime (VanillaWorkSearchScans relies on this method too).
			doBillJobOnThing = Target("RimWorld.WorkGiver_DoBill:JobOnThing");
			if (hdShare != null && hdIsBatchBill != null && hdCanPawnBatch != null && hdInstance != null && doBillJobOnThing != null)
			{
				harmony.Patch(hdShare, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdShareOnlyForBatchJobs)));
				harmony.Patch(doBillJobOnThing,
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdNoBillOnForeignInventory)) { priority = Priority.Last });
			}
			if (Target("RimWorld.IncidentWorker_Raid:TryGenerateRaidInfo") is { } raidInfo)
				harmony.Patch(raidInfo, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(FriendlyRaidDressedForCold)));
			// Smarter Visitors' leave dialog gets Event Image Popup's visitor picture; both mods fingerprinted.
			var eipAdd = Target("EventImagePopup.Patch_WindowStackAdd:Prefix");
			if (Target("StylishDialogs.CollageLayout:Decoration") != null && Target("StylishDialogs.DialogCollagePatch:Get") is { } sdGet)
			{
				sdGetDialog = sdGet;
				sdPortrait = AccessTools.Property(AccessTools.TypeByName("StylishDialogs.CollageDialog"), "Portrait");
				sdArt = AccessTools.Property(AccessTools.TypeByName("StylishDialogs.CollagePortrait"), "Art");
				if (sdPortrait != null && sdArt != null)
					harmony.Patch(AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition"),
						postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SdCenterPanel)));
				else
					Log.Error("[Harmony3StrictCompat] Stylish Dialogs CollageDialog.Portrait / CollagePortrait.Art changed: dialog panel not centred");
			}
			if (Target("VFEPirates.JobDriver_DoWelding:CollectIngredientsToils") is { } weldCollect
				&& Target("Shelf.HarmonyPatches:JumpIfTargetInisdeBillGiver") is { } shelfHook)
			{
				Fingerprint("VFEPirates.JobDriver_DoWelding:CollectIngredientsToils/MoveNext", AccessTools.EnumeratorMoveNext(weldCollect));
				shelfPickupHook = (Action<JobDriver_DoBill, Toil, Toil, TargetIndex, TargetIndex>)shelfHook.CreateDelegate(
					typeof(Action<JobDriver_DoBill, Toil, Toil, TargetIndex, TargetIndex>));
				harmony.Patch(weldCollect, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(WeldShelfPickup)));
			}
			var svLeave = svPrefix; // fingerprinted above, before its own patch (a second look after patching hashes differently)
			if (eipAdd != null && svLeave != null)
			{
				Target("EventImagePopup.ImageResolver:ResolveForIncident");
				var eipScope = AccessTools.TypeByName("EventImagePopup.Patch_IncidentScope");
				eipFrames = AccessTools.Field(eipScope, "Frames")?.GetValue(null);
				eipFrameType = eipScope?.GetNestedType("Frame");
				if (eipFrames != null && eipFrameType != null && AccessTools.Field(eipFrameType, "def") != null)
					harmony.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add)),
						prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SvLeaveDialogWithImage)) { priority = Priority.First },
						finalizer: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SvLeaveDialogDone)));
				else
					Log.Error("[Harmony3StrictCompat] EventImagePopup.Patch_IncidentScope changed: Smarter Visitors' leave dialog shows no picture");
			}
			VisitorQuestionLetter.Init(harmony);
			var hdInit = Target("HaulersDream.CompositableLoadoutsCompat:InitKeepApi");
			var hdWanted = Target("HaulersDream.CompositableLoadoutsCompat:LoadoutWantedUnits");
			hdItemDefs = AccessTools.PropertyGetter(AccessTools.TypeByName("Inventory.Item"), "Defs");
			if (hdInit != null && hdWanted != null && hdItemDefs != null)
			{
				harmony.Patch(hdInit, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdBridgeKeepApi)));
				harmony.Patch(hdWanted, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdWantedUnits)));
			}
			// UIR 2026-11-19 moved the hover card and Theme from Inspector Tabs into Core (every module's cards draw there)
			var hcLineHeight = Target("UIRethemed.Core.HoverCard:LineHeight");
			var hcDrawGroup = Target("UIRethemed.Core.HoverCard:DrawGroup");
			var hcStatLine = Target("UIRethemed.Core.Theme:StatLine");
			hcGroupBodyWidth = Target("UIRethemed.Core.HoverCard:GroupBodyWidth");
			var hcLineType = AccessTools.TypeByName("UIRethemed.Core.HoverCard+Line");
			var hcLabelMethod = AccessTools.Method("UIRethemed.Core.Theme:Label", [typeof(Rect), typeof(string), typeof(TextAnchor), typeof(Color), typeof(GameFont)]);
			var hcBrightGetter = AccessTools.PropertyGetter(AccessTools.TypeByName("UIRethemed.Core.Theme"), "TextBright");
			if (hcLineHeight != null && hcDrawGroup != null && hcStatLine != null && hcGroupBodyWidth != null && hcLineType != null && hcLabelMethod != null && hcBrightGetter != null)
			{
				hcThemeLabel = (Action<Rect, string, TextAnchor, Color, GameFont>)hcLabelMethod.CreateDelegate(typeof(Action<Rect, string, TextAnchor, Color, GameFont>));
				hcTextBright = (Func<Color>)hcBrightGetter.CreateDelegate(typeof(Func<Color>));
				if (Target("UIRethemed.Core.Theme:HoverGlow") is { } hcGlow)
					hcHoverGlow = (Func<Rect, float, Rect?, bool>)hcGlow.CreateDelegate(typeof(Func<Rect, float, Rect?, bool>));
				hcLabel = AccessTools.Field(hcLineType, "label");
				hcValue = AccessTools.Field(hcLineType, "value");
				hcIcon = AccessTools.Field(hcLineType, "icon");
				hcBar = AccessTools.Field(hcLineType, "bar");
				hcAccent = AccessTools.Field(hcLineType, "accent");
				harmony.Patch(hcLineHeight, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HcLineHeight)));
				harmony.Patch(hcDrawGroup, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HcGroupEnter)),
					finalizer: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HcGroupExit)));
				harmony.Patch(hcStatLine, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HcWrapStatLine)));
			}
			var vsieCured = Target("VanillaSocialInteractionsExpanded.Thought_CuredMyFriend:get_LabelCap");
			var vsieTale = AccessTools.TypeByName("VanillaSocialInteractionsExpanded.Tale_TriplePawn");
			var vsieNoticed = Target("VanillaSocialInteractionsExpanded.VSIE_Utils:HaveNoticedTale");
			if (vsieCured != null && vsieTale != null && vsieNoticed != null)
			{
				vsieTaleType = vsieTale;
				vsieFirst = AccessTools.Field(vsieTale, "firstPawnData");
				vsieSecond = AccessTools.Field(vsieTale, "secondPawnData");
				vsieThird = AccessTools.Field(vsieTale, "thirdPawnData");
				vsieHaveNoticed = (Func<Pawn, Tale, bool>)vsieNoticed.CreateDelegate(typeof(Func<Pawn, Tale, bool>));
				harmony.Patch(vsieCured, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(VsieCuredMyFriendLabel)));
			}
			var uirVacuum = Target("UIRethemed.InspectorTabs.GearTab:RefreshVacuumGauge");
			var uirGearTab = AccessTools.TypeByName("UIRethemed.InspectorTabs.GearTab");
			uirVacuumGauge = uirGearTab == null ? null : AccessTools.Field(uirGearTab, "vacuumGauge");
			uirVacuumLabelWidth = uirGearTab == null ? null : AccessTools.Field(uirGearTab, "vacuumLabelWidth");
			uirGaugeLabel = uirVacuumGauge == null ? null : AccessTools.Field(uirVacuumGauge.FieldType, "label");
			if (uirVacuum != null)
				harmony.Patch(uirVacuum, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirVacuumFontEnter)),
					finalizer: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirVacuumFontExit)));
			var uirSlotFor = Target("UIRethemed.InspectorTabs.GearTab:VanillaSlotFor");
			uirDollSlot = uirGearTab == null ? null : AccessTools.Inner(uirGearTab, "DollSlot");
			if (uirSlotFor != null && uirDollSlot != null && ModsConfig.IsActive("stevezero.FullArmorHandsFeet"))
				harmony.Patch(uirSlotFor, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirHandsFeetSlot)));
			var uirStatsPanel = Target("UIRethemed.InspectorTabs.GearTab:DrawStatsPanel");
			if (uirStatsPanel != null)
				harmony.Patch(uirStatsPanel, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirStatsPanelOrder)));
			foreach (var m in new[] { "Contents", "Row" })
				if (Target("UIRethemed.InspectorTabs.GearCompareWindow:" + m) is { } uirCompare)
					harmony.Patch(uirCompare, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirCompareSideColors)));
			var uirSlotLabel = Target("UIRethemed.InspectorTabs.GearTab:SlotLabel");
			if (uirSlotLabel != null)
				harmony.Patch(uirSlotLabel, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirShortSlotLabel)));
			var uirThingCard = Target("UIRethemed.InspectorTabs.GearTab:BuildThingCard");
			var uirCardType = AccessTools.TypeByName("UIRethemed.Core.HoverCard+Card");
			uirCardSubtitle = uirCardType == null ? null : AccessTools.Field(uirCardType, "subtitle");
			uirCardSubtitleColor = uirCardType == null ? null : AccessTools.Field(uirCardType, "subtitleColor");
			if (uirThingCard != null && uirCardSubtitle != null && uirCardSubtitleColor != null)
				harmony.Patch(uirThingCard, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirCardQualityOnlyColored)));
			else if (uirThingCard != null)
				Log.Error("[Harmony3StrictCompat] UIR Inspector Tabs HoverCard.Card subtitle fields changed: its mod updated, redo the quality-only colour tweak");
			var smStockUpFilter = Target("SmartMedicine.Dialog_StockUp:<DoWindowContents>b__10_0");
			if (smStockUpFilter != null)
				harmony.Patch(smStockUpFilter, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SmStockUpExtras)));
			var smStockingUp = Target("SmartMedicine.StockUpUtility:StockingUpOn", args: [typeof(Pawn), typeof(Thing)]);
			var dropUnused = Target("RimWorld.JobGiver_DropUnusedInventory:Drop");
			if (smStockingUp != null && dropUnused != null)
			{
				smIsStocked = (Func<Pawn, Thing, bool>)smStockingUp.CreateDelegate(typeof(Func<Pawn, Thing, bool>));
				harmony.Patch(dropUnused, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SmKeepStockedItem)));
			}
			if (Target("SmartMedicine.FindBestMedicine:Find") is { } smFind)
			{
				var evaluator = AccessTools.Inner(smFind.DeclaringType, "MedicineEvaluator");
				smEvalDistance = evaluator == null ? null : AccessTools.Field(evaluator, "distance");
				smEvalThing = evaluator == null ? null : AccessTools.Field(evaluator, "thing");
				smNearbySetting = AccessTools.Field(AccessTools.TypeByName("SmartMedicine.Settings"), "distanceToUseEqualOnGround");
				if (smEvalDistance != null && smEvalThing != null && smNearbySetting != null)
					harmony.Patch(smFind, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SmFindRemember)),
						transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SmNearbyInTiles)));
				else
					Log.Error("[Harmony3StrictCompat] Smart Medicine MedicineEvaluator/Settings fields changed: its mod updated, redo the nearby-medicine fix");
			}
			var uirqOf = Target("UIRethemed.Quests.RewardTiles:Of");
			uirqTile = AccessTools.TypeByName("UIRethemed.Quests.Tile");
			if (uirqOf != null && uirqTile != null)
				harmony.Patch(uirqOf, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirqRewardTiles)));
			var qcMod = AccessTools.TypeByName("QualityColorsMod"); // global namespace; only its settings class is in QualityColors
			qcSettings = qcMod == null ? null : AccessTools.Field(qcMod, "Settings");
			qcColors = qcSettings == null ? null : AccessTools.Field(qcSettings.FieldType, "Colors");
			if (qcColors != null)
			{
				var uirqLabel = Target("UIRethemed.Quests.Theme:LabelTruncated");
				var uirqRegion = Target("UIRethemed.Quests.HoverCard:Region");
				var uirqContent = Target("UIRethemed.Quests.HoverCard:ContentRegion");
				var uirqRegister = Target("UIRethemed.Quests.HoverCard:Register");
				if (uirqLabel != null && uirqRegion != null && uirqContent != null && uirqRegister != null)
				{
					harmony.Patch(uirqLabel, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirqLabelQuality)));
					harmony.Patch(uirqRegion, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirqTipTitle)));
					harmony.Patch(uirqContent, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirqTipTitle)));
					harmony.Patch(uirqRegister, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirqTipRegister)));
				}
			}
			var hdKeepCount = Target("HaulersDream.InventorySurplus:KeepCountOf");
			var smStockCount = Target("SmartMedicine.StockUpUtility:StockUpCount", args: [typeof(Pawn), typeof(ThingDef)]);
			if (hdKeepCount != null && smStockCount != null)
			{
				smStockCountOf = (Func<Pawn, ThingDef, int>)smStockCount.CreateDelegate(typeof(Func<Pawn, ThingDef, int>));
				harmony.Patch(hdKeepCount, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HdKeepStockUpCount)));
			}
			var canHaul = Target("Verse.AI.HaulAIUtility:PawnCanAutomaticallyHaul");
			if (canHaul != null && ModsConfig.IsActive("orion.hospitality"))
				harmony.Patch(canHaul, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(HaulAreaLabelNullSafe)));
			var unburnedWait = Target("Unburned.Pretty_Patch:Prefix");
			var vefVanillaMelee = Target("VEF.Weapons.MeleeReachCombatUtility:IsVanillaMeleeAttack");
			if (unburnedWait != null && vefVanillaMelee != null)
			{
				vefIsVanillaMelee = (Func<Verb, bool>)vefVanillaMelee.CreateDelegate(typeof(Func<Verb, bool>));
				harmony.Patch(unburnedWait, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UnburnedKeepReachAutoAttack)));
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
			var uirReadoutFace = Target("UIRethemed.MainBar.BarControls:ReadoutFace");
			uirReadoutCard = uirReadoutFace == null ? null : AccessTools.Field(uirReadoutFace.GetParameters()[1].ParameterType, "card");
			if (uirReadoutCard != null)
			{
				uirTemperatureCard = Enum.Parse(uirReadoutCard.FieldType, "Temperature");
				harmony.Patch(uirReadoutFace, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirTemperatureOverlayClick)));
			}
			var uirToggle = Target("UIRethemed.MainBar.BarControls:Toggle");
			var uirSetSpeed = Target("UIRethemed.MainBar.BarControls:SetSpeed");
			tdSettings = AccessTools.Field("TD_Enhancement_Pack.Mod:settings");
			tdStopSlowdown = tdSettings == null ? null : AccessTools.Field(tdSettings.FieldType, "stopForcedSlowdown");
			if (uirToggle != null && uirSetSpeed != null && tdStopSlowdown != null)
			{
				harmony.Patch(uirToggle, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirSpeedTileShiftClick)));
				harmony.Patch(uirSetSpeed, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirSpeedFlyoutShiftClick)));
			}
			if (Target("AdaptiveMealBill.AdaptiveRecipeSelection:RecoverRecipe") is { } ambRecover)
			{
				var ambDef = AccessTools.TypeByName("AdaptiveMealBill.AdaptiveRecipeDef");
				ambMixed = AccessTools.Field(ambDef, "mixedRecipe");
				ambMeat = AccessTools.Field(ambDef, "meatRecipe");
				ambVeg = AccessTools.Field(ambDef, "vegRecipe");
				if (ambMixed != null && ambMeat != null && ambVeg != null)
					harmony.Patch(ambRecover, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AmbRecoverByKind)));
			}
			var uirCareFor = Target("UIRethemed.InspectorTabs.CareSwitchDef:For");
			aegEnabled = AccessTools.Field("AutoExtractGenes.AutoExtractGenesComp:isEnabled");
			if (uirCareFor != null && aegEnabled != null && Target("AutoExtractGenes.Utils:GetAutoExtractGenesComponent") is { } aegGet)
			{
				aegComp = (Func<Pawn, ThingComp>)aegGet.CreateDelegate(typeof(Func<Pawn, ThingComp>));
				harmony.Patch(uirCareFor, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AegCareSwitchFor)));
			}
			if (Target("VFEF.CompScaresAnimals:ScareAnimals") is { } vfefScare)
				harmony.Patch(vfefScare, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(ScarecrowSpawnedOnly)));
			if (Target("VanillaFurnitureExpandedFactory.Building_Conveyor:DetermineGraphic") is { } vfeConveyorGraphic)
				harmony.Patch(vfeConveyorGraphic, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SplitterGraphicByFacing)));
			if (Target("VanillaFurnitureExpandedFactory.Building_Conveyor:TryDumpItem") is { } vfeConveyorDump
				&& Target("VanillaFurnitureExpandedFactory.Building_Conveyor:SelectNextOutput") is { } vfeSelectNext)
			{
				var conveyor = vfeConveyorDump.DeclaringType;
				vffIsSplitter = AccessTools.FieldRefAccess<bool>(conveyor, "cachedIsSplitter");
				vffSelectedOutput = AccessTools.FieldRefAccess<Rot4>(conveyor, "cachedSelectedOutput");
				vffState = AccessTools.Field(conveyor, "state");
				vffMoving = Enum.ToObject(vffState.FieldType, 1); // ConveyorState.Moving
				vffSelectNext = vfeSelectNext;
				harmony.Patch(vfeConveyorDump, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SplitterNoFrontDump)));
			}
			if (Target("VanillaFurnitureExpandedFactory.Building_Conveyor:CanTransferToStorage") is { } vfeCanStore
				&& Target("VanillaFurnitureExpandedFactory.Building_Conveyor:GetDumpCapability") is { } vfeDumpCapability
				&& Target("VanillaFurnitureExpandedFactory.Building_Conveyor:CompleteTransfer") is { } vfeCompleteTransfer
				&& Target("KanbanStockpileContinue.KanbanStockpileAPI:TryGetSettings") is { } ksGetSettings)
			{
				vffKanbanGet = (HdKanbanStackCap.KanbanGet)ksGetSettings.CreateDelegate(typeof(HdKanbanStackCap.KanbanGet));
				vffConveyor = vfeCanStore.DeclaringType;
				vffUnderground = AccessTools.TypeByName("VanillaFurnitureExpandedFactory.Building_UndergroundConveyorBase");
				vffEntrance = AccessTools.TypeByName("VanillaFurnitureExpandedFactory.Building_UndergroundConveyorEntrance");
				if (vffEntrance == null || vffUnderground == null || AccessTools.Field(vffUnderground, "linkedBuilding") == null)
					Log.Error("[Harmony3StrictCompat] VFE Factory underground conveyor types or linkedBuilding no longer exist: its mod updated, Kanban max stack fix not applied");
				else
				{
					vffLinked = AccessTools.FieldRefAccess<Thing>(vffUnderground, "linkedBuilding");
					var vfeBeltMerges = new[] { "TryAdd", "CanAcceptItem", "TransferItemTo", "CanTransferToTarget", "CanStackWithAny" }
						.Select(name => Target("VanillaFurnitureExpandedFactory.Building_Conveyor:" + name)).Where(method => method != null);
					foreach (var method in new[] { vfeCanStore, vfeDumpCapability, vfeCompleteTransfer }.Concat(vfeBeltMerges))
						harmony.Patch(method, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(ConveyorKanbanCap)));
					if (vffCapReads != 11 || vffCapPlaces != 2)
						Log.Error($"[Harmony3StrictCompat] VFE Factory conveyor shape changed ({vffCapReads} stack limit reads, {vffCapPlaces} placements; expected 11 and 2): check the Kanban max stack fix");
				}
			}
			if (Target("EPrimeReadouts.GameCounts:AccumulateMap") is { } eprAccumulate
				&& Target("EPrimeReadouts.Core.CountAccumulator:AddSearch") is { } eprAdd
				&& AccessTools.TypeByName("VanillaFurnitureExpandedFactory.Building_Conveyor") is { } eprBelt)
			{
				eprAddSearch = eprAdd;
				eprBeltType = eprBelt;
				harmony.Patch(eprAccumulate, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(EprCountBeltStock)));
			}
			// Personal tweak: no grey group frame behind colonist-bar groups. Vanilla and LTO Colony Groups draw one per
			// group as soon as a second group exists (e.g. a caravan leaves). Only the drawing is skipped; frame clicks stay.
			foreach (var groupFrame in new[] { Target("RimWorld.ColonistBarColonistDrawer:DrawGroupFrame"), Target("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:DrawGroupFrame") })
				if (groupFrame != null)
					harmony.Patch(groupFrame, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SkipOriginal)));
			if (Target("Impact.ArmorDeflection:Prefix") is { } impactArmor)
				harmony.Patch(impactArmor, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(ImpactBreachNaturalArmor)));
			if (Target("ColonyManagerRedux.Manager:FinalizeInit") is { } cmrInit)
				harmony.Patch(cmrInit, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CmrStartResearchOnce)));
			mfProps = AccessTools.TypeByName("MuzzleFlash.MuzzleFlashProps");
			if (mfProps != null && Target("Flourish.AnimEventWorkers:HasMuzzle") is { } flHasMuzzle)
				harmony.Patch(flHasMuzzle, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(FlSkipMuzzleFlashGuns)));
			if (AccessTools.TypeByName("Leo.Events.MineEntrance") != null && AccessTools.TypeByName("BiomesCore.DefModExtensions.BiomesMap") != null)
				harmony.Patch(AccessTools.Method(typeof(PocketMapUtility), nameof(PocketMapUtility.GeneratePocketMap)),
					prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LeoSeamsRandomCavern)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(LeoSeamsRoofLava)));
			if (Target("ReplaceLib.Utils:RecipeUsersAreSame") is { } rlUsersSame)
				harmony.Patch(rlUsersSame, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RlSameBenchesOnly)));
			Target("ReplaceLib.Utils:ProcessRecipes"); // fingerprint only: removes whatever RecipeUsersAreSame calls a duplicate
			if (Target("PipeSystem.CompResourceTrader:CompInspectStringExtra", optional: true) is { } pipeTraderInspect)
				harmony.Patch(pipeTraderInspect, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PipeTraderInspectUnspawned)));
			if (PipeTapsAndDrains.Init())
			{
				harmony.Patch(AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PipeTapDrainCell)));
				harmony.Patch(AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsValidStorageFor), [typeof(IntVec3), typeof(Map), typeof(Thing)]),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PipeTapDrainValidCell)));
				harmony.Patch(AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetItemStackSpaceLeftFor)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PipeDrainSpaceLeft)));
				harmony.Patch(AccessTools.Method(typeof(StorageSettings), nameof(StorageSettings.AllowedToAccept), [typeof(Thing)]),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PipeTapOverMaxUnstored)));
			}
			foreach (var alert in new[] { typeof(Alert_NeedResearchProject), typeof(Alert_NeedAnomalyProject) })
				harmony.Patch(AccessTools.Method(alert, "OnClick"), prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(ResearchAlertOpenTab)));
			if (Target("SmartDeconstruct.SmartDeconstructMod:getDesignation") is { } sdGetDesignation)
				harmony.Patch(sdGetDesignation, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SdVeinDesignation)));
			harmony.Patch(AccessTools.Method(typeof(WildAnimalSpawner), "CommonalityOfAnimalNow"),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(PocketMapAnimalCommonality)));
			tgTaskForce = AccessTools.Field("TacticalGroups.ColonistGroup:isTaskForce");
			if (tgTaskForce != null && Target("TacticalGroups.ColonistGroup:GetUniqueLoadID") is { } tgLoadId)
				harmony.Patch(tgLoadId, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(TgTaskForceLoadId)));
			harmony.Patch(AccessTools.Method(typeof(Precept_Ritual), nameof(Precept_Ritual.ExposeData)),
				prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RitualSaveCompletedOnce)));
			var uirRowDraw = Target("UIRethemed.Notifications.StatusRow:Draw");
			var uirRowWidth = Target("UIRethemed.Notifications.StatusRow:WidthFor");
			var uirResolve = Target("UIRethemed.Notifications.NotificationStyles:Resolve");
			var uirColumnX = Target("UIRethemed.Notifications.Placement:ColumnX");
			var uirPosition = Target("UIRethemed.Notifications.Placement:get_Position");
			var uirHover = Target("UIRethemed.Core.ThemeMotion:Hover");
			Target("UIRethemed.Notifications.Patch_Messages_MessagesDoGUI:Transpiler");
			if (uirPosition != null)
			{
				uirPositionCall = uirPosition;
				uirTopRight = (int)Enum.Parse(uirPosition.ReturnType, "TopRight");
				uirMinimapType = AccessTools.TypeByName("SatelliteMinimap.MinimapWindow");
				uirTileFeatures = AccessTools.TypeByName("AlphaBiomes.AlphaBiomes_WorldInterface_CheckOpenOrCloseInspectPane_Patch") != null;
				if ((uirMinimapType != null || uirTileFeatures) && Target("UIRethemed.Notifications.Placement:TopStart") is { } uirTopStart)
					harmony.Patch(uirTopStart, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirTopRightUnderWindows)));
			}
			if (Target("SatelliteMinimap.Feed:GatherCore") is { } smGather && Target("SatelliteMinimap.Feed:Add") is { } smAdd
				&& Target("SatelliteMinimap.MinimapWindow:OpenLayersMenu") is { } smMenu && Target("SatelliteMinimap.MinimapWindow:Toggle") is { } smToggle
				&& Target("SatelliteMinimap.MinimapSettings:ExposeData") is { } smExpose)
			{
				smToggleCall = smToggle;
				smCountsField = AccessTools.Field(smGather.DeclaringType, "Counts");
				harmony.Patch(smGather, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SmGatherWithoutMechs)));
				harmony.Patch(smAdd, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SmRaiseMarkerCap)));
				harmony.Patch(smMenu, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SmLayersMenuWithMechs)));
				harmony.Patch(smExpose, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(SmExposeShowMechs)));
			}
			if (Target("UIRethemed.Notifications.FoodReadout:Read") is { } uirFoodRead && Target("UIRethemed.Notifications.FoodReadout:Draw") is { } uirFoodDraw
				&& AccessTools.Field(uirFoodRead.DeclaringType, "nextRead") is { IsStatic: true } uirFoodNext && AccessTools.Field(uirFoodRead.DeclaringType, "shows") is { IsStatic: true } uirFoodShows)
			{
				uirFoodNextRead = uirFoodNext;
				uirFoodShowsField = uirFoodShows;
				harmony.Patch(uirFoodDraw, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirFoodNoteHover)));
				harmony.Patch(uirFoodRead, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirFoodHoldWhileHovered)),
					postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirFoodReadEvery5s)));
			}
			if (Target("UIRethemed.ColonistCard.InfoList:Defer") is { } uirInfoDefer && Target("UIRethemed.ColonistCard.InfoList:DrawRow") is { } uirInfoDrawRow)
			{
				harmony.Patch(uirInfoDefer, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirInfoStripStackNote)));
				harmony.Patch(uirInfoDrawRow, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirInfoRowHoverCard)));
			}
			if (uirRowDraw != null && uirRowWidth != null && uirResolve != null && uirColumnX != null && uirPosition != null && uirHover != null)
			{
				uirRowDrawCall = uirRowDraw;
				uirRowWidthCall = (Func<string, float>)uirRowWidth.CreateDelegate(typeof(Func<string, float>));
				uirColumnXCall = (Func<float, float>)uirColumnX.CreateDelegate(typeof(Func<float, float>));
				uirHoverCall = (Func<Rect, int, bool, float>)uirHover.CreateDelegate(typeof(Func<Rect, int, bool, float>));
				uirBottomRight = (int)Enum.Parse(uirPosition.ReturnType, "BottomRight");
				uirMiddleRight = (int)Enum.Parse(uirPosition.ReturnType, "MiddleRight");
				if (Target("UIRethemed.Core.HoverCard:Region", args: [typeof(Rect), typeof(string), typeof(string), typeof(string), typeof(Color?)]) is { } uirHoverCard)
					uirHoverRegion = (Action<Rect, string, string, string, Color?>)uirHoverCard.CreateDelegate(typeof(Action<Rect, string, string, string, Color?>));
				uirPlacedDraw = Target("UIRethemed.Core.HoverCard:DrawPlaced");
				var uirPlacedSize = Target("UIRethemed.Core.HoverCard:PlacedSize");
				var uirBeside = Target("UIRethemed.Notifications.Placement:BesideX");
				if (uirPlacedDraw != null && uirPlacedSize != null && uirBeside != null)
				{
					uirPlacedSizeCall = (Func<string, string, Vector2>)uirPlacedSize.CreateDelegate(typeof(Func<string, string, Vector2>));
					uirBesideCall = (Func<Rect, float, float, float>)uirBeside.CreateDelegate(typeof(Func<Rect, float, float, float>));
				}
				else uirPlacedDraw = null;
				uirResolveCall = uirResolve;
				UirMessagesSectionSetup(harmony);
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
			var umDialog = Target("UsefulMarks.Dialog_NamePawn_DoWindowContents_Patch:Postfix");
			if (umDialog != null)
			{
				umNames = AccessTools.Field(typeof(Dialog_NamePawn), "names");
				umTextboxName = AccessTools.Field(AccessTools.Inner(typeof(Dialog_NamePawn), "NameContext"), "textboxName");
				if (umNames != null && umTextboxName != null)
					harmony.Patch(umDialog, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UmMarksOnlyBesideTitleRow)));
			}
			var umInspect = Target("UsefulMarks.Patch_MainTabWindow_Inspect_DoInspectPaneButtons:Prefix");
			if (umInspect != null)
				harmony.Patch(umInspect, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UmInspectButtonsPawnOnly)));
			if (Target("AlteredCarbon.JobGiver_Reload_TryGiveJob_Patch:Postfix") is { } acReload)
				harmony.Patch(acReload, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(AcBeltReloadOnlyWithVefShield)));
			if (ModsConfig.IsActive("wowgag.RimPacts"))
				harmony.Patch(AccessTools.Method(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), [typeof(Letter), typeof(string), typeof(int), typeof(bool)]),
					prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(RptTitleCaseLetter)));
			var uirccTheming = Target("UIRethemed.ColonistCard.Card:get_ThemingHeader");
			var uirccSlot = Target("UIRethemed.ColonistCard.Card:NextHeaderSlot");
			if (uirccTheming != null && uirccSlot != null)
			{
				uirccThemingHeader = (Func<bool>)uirccTheming.CreateDelegate(typeof(Func<bool>));
				uirccNextSlot = (Func<Rect>)uirccSlot.CreateDelegate(typeof(Func<Rect>));
				foreach (var drawer in new[] { Target("RimWorld.MainTabWindow_Inspect:DoInspectPaneButtons"), umInspect })
					if (drawer != null)
						harmony.Patch(drawer, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirccGuiltyIconInSlot)));
				uirccHeaderSlots = AccessTools.Field(uirccSlot.DeclaringType, "headerSlots");
				uirccHeaderRowWidth = AccessTools.Field(uirccSlot.DeclaringType, "headerRowWidth");
				if (uirccHeaderSlots?.FieldType != typeof(int) || uirccHeaderRowWidth?.FieldType != typeof(float))
					Log.Error("[Harmony3StrictCompat] UI Rethemed - Colonist Card: Card.headerSlots / headerRowWidth are gone, header button tooltips not moved");
				else if (Target("TD_Enhancement_Pack.JumpToSelection:Postfix") is { } tdJump)
					harmony.Patch(tdJump, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirccTipInLastSlot)));
			}
			if (Target("UIRethemed.ColonistCard.StatusBar:Take") is { } uirccTake)
			{
				uirccEffects = AccessTools.Field(uirccTake.DeclaringType, "effects")?.GetValue(null) as IList;
				var effectType = AccessTools.Inner(uirccTake.DeclaringType, "Effect");
				uirccEffectDef = effectType == null ? null : AccessTools.Field(effectType, "def");
				uirccEffectLabel = effectType == null ? null : AccessTools.Field(effectType, "label");
				if (uirccEffects != null && uirccEffectDef != null && uirccEffectLabel != null)
					harmony.Patch(uirccTake, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirccNameInspiration)));
			}
			Target("UIRethemed.Animals.Motion:Key"); // fingerprint only: the key the patched Hover hands its rect to
			if (Target("UIRethemed.Animals.Motion:Hover") is { } uiraHover)
				harmony.Patch(uiraHover, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UiraHoverKeyOnScreen)));
			if (Target("UIRethemed.Architect.ArchitectData:ShownApart") is { } uirShownApart)
			{
				var entryType = AccessTools.TypeByName("UIRethemed.Architect.Entry");
				uirEntryCategory = AccessTools.Field(entryType, "category");
				uirEntryIsOrder = AccessTools.Field(entryType, "isOrder");
				if (uirEntryCategory != null && uirEntryIsOrder != null)
					harmony.Patch(uirShownApart, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirOrdersTogether)));
			}
			foreach (var inspectorPart in new[] { "OnGUI", "DrawRow", "DrawThingRow", "DrawHeader", "DrawDivider", "DrawMapInspector", "DrawWorldInspector" })
				if (Target("Verse.CellInspectorDrawer:" + inspectorPart) is { } cellInspector)
					harmony.Patch(cellInspector, transpiler: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(WiderCellInspector)),
						prefix: inspectorPart switch
						{
							"OnGUI" => new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CellInspectorResize)),
							"DrawRow" => new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CellInspectorMeasureRow)),
							"DrawHeader" => new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(CellInspectorMeasureHeader)),
							_ => null,
						});
			floorGraphicData = AccessTools.Field(AccessTools.TypeByName("VEF.Graphics.FloorGraphicExtension"), "graphicData");
			if (floorGraphicData != null && Target("Verse.Widgets:GetIconFor", args: [typeof(Thing), typeof(Vector2), typeof(Rot4?), typeof(bool),
					typeof(float).MakeByRefType(), typeof(float).MakeByRefType(), typeof(Vector2).MakeByRefType(), typeof(Color).MakeByRefType(),
					typeof(Material).MakeByRefType()]) is { } thingIcon)
				harmony.Patch(thingIcon, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(FloorGraphicIconScale)));
			BillCountCache.Init(harmony);
			VgeWarcomputerOnlyForGaussGuns.Init(harmony);
			QuietWondersSleepDimRooms.Init(harmony);
			VfeFactoryNoInsertHopperNotBetterStorage.Init(harmony);
			WppFightTargetCheapValidatorMethod.Init(harmony);
			ReachFloodMemo.Init(harmony);
			ModWorkGiverScans.Init(harmony);
			VanillaWorkSearchScans.Init(harmony);
			TickHotPaths.Init(harmony);
			DoorReachCacheWalk.Init(harmony);
			TickTrace.Init(harmony, content.RootDir);
			GravcoreSubquestTick.Init(harmony);
			GravshipStaleBoardOrLeave.Init(harmony);
			BbRequestOnStockedMap.Init(harmony);
			UirArchitectTwoLineNames.Init(harmony);
			AdaptedTextOncePerSecond.Init(harmony);
			NoSampleOnEndedSustainer.Init(harmony);
			NoZeroSeverityDamageHediffs.Init(harmony);
			AcNoBirthInSleeveCasket.Init(harmony);
			MssHoloNoBodyNeeds.Init(harmony);
			IpCanUseStyleNoLists.Init(harmony);
			CatalogButtonsInUir.Init(harmony);
			NewSettlementWantForSettlers.Init(harmony);
			KanbanOwnerlessSettings.Init(harmony);
			ImpactFloorByFireRate.Init(harmony, content.RootDir);
		}

		// ---- UI Rethemed - Architect + Vanilla Plants Expanded: Tilled soil among the orders ----
		// UIR moves a category's orders (designators that place nothing) to its side Orders column whenever that category
		// also has something to build (ArchitectData.ShownApart). VPE's Tilled soil is the only buildable in Orders, so
		// it got a one-card "Orders" group of its own while Cancel, Mine, Haul and the rest sat in the column. Keep
		// Orders' orders in the grid, one group with Tilled soil, as Zone shows its buttons.
		static FieldInfo uirEntryCategory, uirEntryIsOrder;

		static void UirOrdersTogether(object entry, ref bool __result)
		{
			if (__result && (bool)uirEntryIsOrder.GetValue(entry)
				&& (uirEntryCategory.GetValue(entry) as DesignationCategoryDef)?.defName == "Orders")
				__result = false;
		}

		// ---- UI Rethemed - Animals: hover highlight mirrored into other groups ----
		// Motion.Hover keeps each hover fade under a key hashed from the rect it is given. Grouped animal rows are drawn
		// inside Widgets.BeginGroup(group body), so the Nth row of every unfolded group has the same local rect and
		// shares one fade: hovering a row lit the same row (and its cells) in every group below. Hover only uses its rect
		// for that key, so a prefix hands it the screen rect (patching the small Key itself could be defeated by inlining); a scroll moves the key, so a row under a scrolling mouse fades in again (0.08 s).
		static void UiraHoverKeyOnScreen(ref Rect rect)
		{
			if (Event.current != null) rect = GUIUtility.GUIToScreenRect(rect);
		}

		// ---- Altered Carbon + Too Many Mods: Cuirassier belt reload NRE ----
		// Too Many Mods - Compats and Rebalances swaps the Cuirassier belt's VEF shield (CompShieldBubble) for the vanilla
		// CompShield. AC's JobGiver_Reload postfix still reads the VEF comp, gets null and throws on every job search of a
		// pawn wearing the belt. That postfix only adds "charge the belt at a station", which the self-recharging vanilla
		// shield never needs, so skip it while the belt def has no VEF shield (decided once, defs are loaded by then).
		static bool? acBeltVefShield;
		static bool AcBeltReloadOnlyWithVefShield()
		{
			acBeltVefShield ??= DefDatabase<ThingDef>.GetNamedSilentFail("AC_CuirassierBelt")?.comps?
				.Any(c => c.compClass?.FullName == "VEF.Apparels.CompShieldBubble") ?? false;
			return acBeltVefShield.Value;
		}

		// ---- UI Rethemed - Colonist Card: name the inspiration (visual tweak) ----
		// The status pill shows the status def's label, so every inspiration reads just "Inspired". Its Effect struct has
		// a label override (UIR fills it for bleeding); fill it with the pawn's inspiration ("Inspired creativity",
		// "Frenzy: shoot") right after Take adds the effect. The hover card still lists the full line with time left.
		static IList uirccEffects;
		static FieldInfo uirccEffectDef, uirccEffectLabel;

		static void UirccNameInspiration(Pawn pawn, bool __result)
		{
			if (!__result || pawn?.Inspiration?.def == null) return;
			for (int i = uirccEffects.Count - 1; i >= 0; i--)
			{
				object effect = uirccEffects[i];
				if ((uirccEffectDef.GetValue(effect) as Def)?.defName != "UIRethemedColonistCard_Inspired" || uirccEffectLabel.GetValue(effect) != null) continue;
				uirccEffectLabel.SetValue(effect, pawn.Inspiration.def.LabelCap.Resolve());
				uirccEffects[i] = effect;
			}
		}

		// ---- Log archive (personal tooling) ----
		// The game keeps only Player.log and Player-prev.log, so a problem from two runs ago (a bill dropped on load)
		// could not be traced. At startup, copy the previous run's logs into Logs/Archive, named by the time that run
		// ended, and keep the newest 30 of each. ponytail: plain copies (3-7 MB each); compress them if the folder grows.
		const int LogArchiveKeep = 30;

		static void ArchivePreviousLogs()
		{
			try
			{
				string dir = Path.GetDirectoryName(Application.consoleLogPath);
				if (dir.NullOrEmpty()) return;
				string archive = Path.Combine(dir, "Archive");
				Directory.CreateDirectory(archive);
				// Hauler's Dream rotates its own log in its constructor, which may run before or after this one, so take
				// both of its files when they were last written before this process started (same run = same file name).
				var started = System.Diagnostics.Process.GetCurrentProcess().StartTime;
				foreach (var (prev, stem) in new[] { ("Player-prev.log", "Player"), ("HaulersDream-debug.log", "HaulersDream"), ("HaulersDream-debug.prev.log", "HaulersDream") })
				{
					var src = new FileInfo(Path.Combine(dir, prev));
					if (!src.Exists || src.LastWriteTime >= started) continue;
					string dest = Path.Combine(archive, $"{stem}-{src.LastWriteTime:yyyy-MM-dd_HHmmss}.log");
					if (!File.Exists(dest)) src.CopyTo(dest);
					foreach (var old in new DirectoryInfo(archive).GetFiles(stem + "-*.log").OrderByDescending(f => f.Name).Skip(LogArchiveKeep))
						old.Delete();
				}
			}
			catch (Exception e)
			{
				Log.Warning("[Harmony3StrictCompat] log archive: " + e.Message);
			}
		}

		// ---- RimPacts: letter titles in title case (personal tweak) ----
		// RimPacts writes its letter titles in sentence case and fills them with lowercase def labels ("Treaty expiring:
		// trade agreement", "World economy: recession"). Any letter whose title matches one of RimPacts' own
		// Rpt_Letter*Label templates (English only) gets title case: every word capitalised except short joining words,
		// and the first word after ':' or a dash always. Def labels keep their lowercase for use inside sentences, and
		// words that do not start with a lowercase letter (tagged names, numbers) are left alone.
		static List<Regex> rptLetterTitles;
		static readonly HashSet<string> titleSmallWords = ["a", "an", "the", "and", "or", "of", "to", "in", "on", "at", "by", "for", "vs", "with", "from", "as"];

		static void RptTitleCaseLetter(Letter let)
		{
			if (let == null || LanguageDatabase.activeLanguage?.folderName != "English") return;
			rptLetterTitles ??= LanguageDatabase.activeLanguage.keyedReplacements
				.Where(kv => kv.Key.StartsWith("Rpt_Letter") && kv.Key.EndsWith("Label") && !kv.Value.value.NullOrEmpty())
				.Select(kv => new Regex("^" + Regex.Replace(Regex.Escape(kv.Value.value), @"\\\{\d+}", ".+") + "$", RegexOptions.Singleline))
				.ToList();
			string label = let.Label.RawText;
			if (label.NullOrEmpty() || !rptLetterTitles.Any(r => r.IsMatch(label))) return;
			var words = label.Split(' ');
			for (int i = 0; i < words.Length; i++)
			{
				string w = words[i];
				bool lead = i == 0 || words[i - 1].EndsWith(":") || words[i - 1] is "—" or "–" or "-";
				if (w.Length == 0 || !char.IsLower(w[0]) || (!lead && titleSmallWords.Contains(w))) continue;
				words[i] = char.ToUpper(w[0]) + w.Substring(1);
			}
			let.Label = string.Join(" ", words);
		}

		// ---- Useful Marks: second info card button on selected things ----
		// Its prefix on MainTabWindow_Inspect.DoInspectPaneButtons redraws vanilla's buttons (info card first) to add the
		// mark button, but only skips vanilla for pawns: for any other single selected thing it draws the info card
		// button and returns true, so vanilla draws another one at the same spot. Plain vanilla stacks them; UI Rethemed's
		// header gives each its own slot, so items and buildings showed two "i". Run Useful Marks' prefix for pawns only;
		// anything else gets vanilla's buttons alone.
		static bool UmInspectButtonsPawnOnly(ref bool __result)
		{
			if (Find.Selector.NumSelected == 1 && Find.Selector.SingleSelectedThing is Pawn) return true;
			__result = true;
			return false;
		}

		// ---- UI Rethemed - Colonist Card: guilty icon over a header button ----
		// The card header runs the pane's DoInspectPaneButtons and moves each button it draws (Widgets.ButtonImage,
		// the hostility response button) into its own 24 px slot from the right (Card.NextHeaderSlot). The guilty icon
		// is a plain GUI.DrawTexture, so it stayed at vanilla's spot and covered a slotted button (Useful Marks' marker).
		// In vanilla's DoInspectPaneButtons and Useful Marks' copy of it, the rect local handed to that DrawTexture (and
		// to its tooltip right after) is moved into the next slot while the card header is drawing.
		internal static Func<bool> uirccThemingHeader;
		internal static Func<Rect> uirccNextSlot;

		static Rect UirccGuiltySlot(Rect rect) => uirccThemingHeader() ? uirccNextSlot() : rect;

		// The rect reaches DrawTexture either as `newobj Rect; dup` (vanilla: the copy left on the stack feeds the
		// tooltip) or as `ldloc rect` (tooltip reloads the local). Swap it for the slot right there: before the dup, or
		// after the ldloc with the local written back.
		static IEnumerable<CodeInstruction> UirccGuiltyIconInSlot(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
		{
			var guilty = AccessTools.Field(typeof(TexUI), nameof(TexUI.GuiltyTex));
			var draw = AccessTools.Method(typeof(GUI), nameof(GUI.DrawTexture), new[] { typeof(Rect), typeof(Texture) });
			var slot = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(UirccGuiltySlot));
			var codes = instructions.ToList();
			int done = 0;
			for (int i = 1; i + 1 < codes.Count; i++)
			{
				if (!codes[i].LoadsField(guilty) || !codes[i + 1].Calls(draw)) continue;
				if (codes[i - 1].opcode == OpCodes.Dup)
				{
					codes.Insert(i - 1, new CodeInstruction(OpCodes.Call, slot).MoveLabelsFrom(codes[i - 1]));
					i++;
				}
				else if (codes[i - 1].IsLdloc())
				{
					codes.InsertRange(i, new[] { new CodeInstruction(OpCodes.Call, slot), new CodeInstruction(OpCodes.Dup), CodeInstruction.StoreLocal(codes[i - 1].LocalIndex()) });
					i += 3;
				}
				else continue;
				done++;
			}
			if (done != 1)
				Log.Error($"[Harmony3StrictCompat] guilty icon slot: expected one guilty icon draw in {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}, found {done}");
			return codes;
		}

		// ---- UI Rethemed - Colonist Card: tooltip of a slotted header button left at the game's spot ----
		// A mod button drawn from a DoInspectPaneButtons postfix as Widgets.ButtonImage(rect) + TooltipHandler.TipRegion(rect)
		// (TD Enhancement Pack's "jump to selection" eye) gets its icon moved into a header slot, but the tooltip region
		// stays at the game's coordinates: an invisible "Jump to this object" area over whichever button sits there.
		// The TipRegion call right after the button is sent to the slot handed out last. That slot is rebuilt from the
		// card's counters with the layout of Card.NextHeaderSlot (24 px slots, 4 px gaps, from the right), which is
		// fingerprinted above, so a layout change shows as a red line.
		static FieldInfo uirccHeaderSlots, uirccHeaderRowWidth;

		static void UirccTipRegion(Rect rect, TipSignal tip)
		{
			if (uirccThemingHeader() && (int)uirccHeaderSlots.GetValue(null) is var slots and > 0)
				rect = new Rect(Mathf.Round((float)uirccHeaderRowWidth.GetValue(null) - slots * 24f - (slots - 1) * 4f), 0f, 24f, 24f);
			TooltipHandler.TipRegion(rect, tip);
		}

		static IEnumerable<CodeInstruction> UirccTipInLastSlot(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
		{
			var tip = AccessTools.Method(typeof(TooltipHandler), nameof(TooltipHandler.TipRegion), [typeof(Rect), typeof(TipSignal)]);
			var codes = instructions.ToList();
			var calls = codes.Where(c => c.Calls(tip)).ToList();
			if (calls.Count != 1)
				Log.Error($"[Harmony3StrictCompat] header tooltip slot: expected one TipRegion call in {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}, found {calls.Count}");
			else
				calls[0].operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(UirccTipRegion));
			return codes;
		}

		// ---- Useful Marks: its two mark widgets (label-in-world checkbox, colour square) sit at a fixed offset from the
		// bottom of Dialog_NamePawn, which lands beside the Title row when renaming a colonist but on top of the last-name
		// "Suggested..." button in the newborn naming dialog (no Title row there). Drawn only when the bottom row is the Title row. ----
		static FieldInfo umNames, umTextboxName;

		static bool UmMarksOnlyBesideTitleRow([HarmonyArgument(2)] Dialog_NamePawn dialog)
		{
			if (dialog == null || umNames.GetValue(dialog) is not IList names || names.Count == 0) return true;
			return (string)umTextboxName.GetValue(names[names.Count - 1]) == "BackstoryTitle";
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
		// MenuWindow.Face with no icon. Match Show Mod Updates' label ("{0} updated mod(s)", with or without its
		// "RimWorld updated" second line, or that line alone; in the active language) and hand Face a Phosphor icon from UIR Core.
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
				// After a game update SMU appends Environment.NewLine + "SMU.GameUpdated", or shows that line alone
				var game = smuLanguage.TryGetTextFromKey("SMU.GameUpdated", out var gameForm) ? System.Text.RegularExpressions.Regex.Escape(gameForm.RawText) : null;
				var mods = forms.Count == 0 ? null : "(?:" + string.Join("|", forms) + ")" + (game == null ? "" : @"(?:\r?\n" + game + ")?");
				var any = mods == null ? game : game == null ? mods : mods + "|" + game;
				smuLabel = any == null ? null : new System.Text.RegularExpressions.Regex("^(?:" + any + ")$");
			}
			if (smuLabel == null || !smuLabel.IsMatch(text)) return;
			smuIcon ??= uirIconFor("arrow-clockwise");
			iconTex = (Texture2D)smuIcon;
		}

		// ---- UI Rethemed - Main Bar: click the temperature readout to toggle the temperature overlay ----
		// UIR's temperature readout only shows a hover card; its ReadoutFace swallows every mouse-down on the face
		// (MainBar.TakeClick). Before that, a left or right click on a Temperature readout (text or tile) flips the
		// vanilla overlay flag, the same one Heat Map's key binding and outdoor thermometer toggle, with the checkbox sounds.
		static FieldInfo uirReadoutCard;
		static object uirTemperatureCard;

		static void UirTemperatureOverlayClick(Rect face, object[] __args)
		{
			var ev = Event.current;
			if (ev.type != EventType.MouseDown || ev.button > 1 || !Mouse.IsOver(face) || Find.CurrentMap == null) return;
			if (!uirTemperatureCard.Equals(uirReadoutCard.GetValue(__args[1]))) return;
			var settings = Find.PlaySettings;
			settings.showTemperatureOverlay = !settings.showTemperatureOverlay;
			Verse.Sound.SoundStarter.PlayOneShotOnCamera(settings.showTemperatureOverlay ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff);
			ev.Use();
		}

		// ---- UI Rethemed - Main Bar + TD Enhancement Pack: Shift-click the time speed to end the forced 1x slowdown ----
		// TD's "Shift-click the time controls to stop the forced slowdown" transpiles vanilla TimeControls, which UIR only
		// draws offscreen. Same rule on UIR's controls, behind TD's own setting: Shift-click on the speed tile ends the
		// slowdown (instead of opening the flyout) while one is running; Shift-click on a flyout speed ends it and picks
		// that speed. Combat keeps re-signalling the slowdown, so it can come back, as with TD on vanilla controls.
		static FieldInfo tdSettings, tdStopSlowdown;
		static readonly AccessTools.FieldRef<TimeSlower, int> forceNormalSpeedUntil = AccessTools.FieldRefAccess<TimeSlower, int>("forceNormalSpeedUntil");
		static readonly Action<TimeSpeed> playSpeedSound = AccessTools.MethodDelegate<Action<TimeSpeed>>(AccessTools.Method(typeof(TimeControls), "PlaySoundOf"));

		static bool StopForcedSlowdown()
		{
			if (!Event.current.shift || !(bool)tdStopSlowdown.GetValue(tdSettings.GetValue(null))) return false;
			var tm = Find.TickManager;
			if (!tm.slower.ForcedNormalSpeed) return false;
			forceNormalSpeedUntil(tm.slower) = tm.TicksGame - 1;
			return true;
		}

		static bool UirSpeedTileShiftClick(object[] __args)
		{
			if (Convert.ToInt32(__args[0]) != 1 || !StopForcedSlowdown()) return true; // 1 = Flyout.Speed
			playSpeedSound(Find.TickManager.CurTimeSpeed);
			return false;
		}

		static void UirSpeedFlyoutShiftClick() => StopForcedSlowdown();

		// ---- AdaptiveMealBill + Hauler's Dream: batch-cooked adaptive meals pick their meal kind ----
		// Hauler's Dream swaps the DoBill job for its own batch job, so AMB's chosen sub-recipe (keyed on the old job) is
		// lost, and HD splits the batch by AMB's dummy recipe (any raw food), so a rep can be lopsided (mostly meat, a
		// little veg). AMB's RecoverRecipe then fits none of its three recipes by amount, logs "Could not recover the meal
		// recipe" and cooks the dummy's product. Fall back by kind instead: veg-only or meat-only when every ingredient fits
		// that recipe's filters, else mixed.
		static FieldInfo ambMixed, ambMeat, ambVeg;

		static void AmbRecoverByKind(RecipeDef adaptiveRecipe, List<Thing> ingredients, ref RecipeDef __result)
		{
			if (__result != null || ingredients.NullOrEmpty()) return;
			foreach (var field in new[] { ambVeg, ambMeat })
				if (field.GetValue(adaptiveRecipe) is RecipeDef only
					&& ingredients.All(t => only.ingredients.Any(i => i.filter.Allows(t))))
				{
					__result = only;
					return;
				}
			__result = ambMixed.GetValue(adaptiveRecipe) as RecipeDef;
		}

		// ---- UI Rethemed - Inspector Tabs + Auto Extract Genes: the switch in the Health tab's Care section ----
		// AEG postfixes vanilla HealthCardUtility.DrawOverviewTab, which UIR's themed Health tab replaces. UIR lets mods add
		// Care switches by def (CareSwitchDef: a pawn collection that is "on" and a setter); AEG keeps a per-pawn bool on
		// its comp instead, so these adapt it. The def itself is added by 1.7/Patches/UIRethemed_AutoExtractGenesCare.xml.
		// CareSwitchDef.For shows a switch for every pawn with player settings (animals too); limit this one to the pawns
		// AEG's own checkbox showed for.
		const string AegSwitchDef = "Harmony3StrictCompat_AutoExtractGenes";
		static FieldInfo aegEnabled;
		static Func<Pawn, ThingComp> aegComp;
		public static readonly ICollection<Pawn> AegPawnsOn = new AegEnabledPawns();

		public static void AegSet(Pawn pawn, bool flag)
		{
			if (aegComp?.Invoke(pawn) is { } comp) aegEnabled.SetValue(comp, flag);
		}

		// Colony Manager Redux finishes its starting research (managing spot/desk) in every map's FinalizeInit, on every
		// load, without checking IsFinished. Each call sends vanilla's "ResearchCompleted" signal, which MSS Tweaks counts
		// toward its "reformation points for tech levels" bonus: reloading a save gave free ideology points.
		static void CmrFinishUnlessDone(ResearchManager manager, ResearchProjectDef proj, bool doCompletionDialog, Pawn researcher, bool doCompletionLetter)
		{
			if (!proj.IsFinished)
				manager.FinishProject(proj, doCompletionDialog, researcher, doCompletionLetter);
		}

		// VFE Factory two-output splitter shows a red X ("Could not load Texture2D at ...ConveyorSplitter_NorthSouth_south"):
		// DetermineGraphic names its texture <outputs>_<side> with the side from ForwardCell, but a splitter's ForwardCell
		// is the output it is feeding right now (round-robin cachedSelectedOutput). The textures are named by facing
		// (NorthSouth only _east/_west, EastWest only _north/_south). The graphic is cached, so it is only wrong when it was
		// built while the splitter held an item: an end-of-line splitter facing east or west with exactly north + south
		// outputs on a backed-up line draws a red X, other pairs a wrong-facing picture. The splitter branch (the first
		// FromIntVec3 in the method) uses the splitter's Rotation instead; the merger branch is left as shipped.
		static IEnumerable<CodeInstruction> SplitterGraphicByFacing(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var fromVec = AccessTools.Method(typeof(Rot4), nameof(Rot4.FromIntVec3));
			int k = codes.FindIndex(c => c.Calls(fromVec));
			if (k < 5 || !(codes[k - 4].operand is MethodInfo fwd && fwd.Name == "get_ForwardCell")
				|| !(codes[k - 2].operand is FieldInfo pos && pos.Name == "cachedPos") || codes[k - 5].opcode != OpCodes.Ldarg_0)
			{
				Log.Error("[Harmony3StrictCompat] VFE Factory Building_Conveyor.DetermineGraphic shape changed, splitter picture fix not applied");
				return codes;
			}
			var labels = codes[k - 5].labels;
			codes.RemoveRange(k - 5, 6);
			codes.InsertRange(k - 5, [
				new CodeInstruction(OpCodes.Ldarg_0) { labels = labels },
				new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Rotation))),
			]);
			return codes;
		}

		// VFE Factory end-of-line splitter (belt in, outputs only to the sides) stops dead when the cell it faces holds a
		// building (a lamp): each item's output is reset after a transfer (cachedSelectedOutput = Invalid), so ForwardCell
		// falls back to the facing cell, which holds no conveyor, and ProcessMovement calls TryDumpItem(facing cell).
		// GetDumpCapability returns CannotDump for any non-refuelable building there, the belt goes Waiting, and the
		// round-robin pick (InitiateTransfer -> SelectNextOutput) is never reached. With an empty floor there it works
		// only because CanDump leads to InitiateTransfer, which picks a side belt; nothing is ever dumped on that cell.
		// A splitter's facing cell is never an output unless it holds a conveyor (IsValidOutput), so pick the next output
		// first and start moving, as the empty-floor path does; if no output can be picked, run the original.
		static AccessTools.FieldRef<object, bool> vffIsSplitter;
		static AccessTools.FieldRef<object, Rot4> vffSelectedOutput;
		static FieldInfo vffState;
		static object vffMoving;
		static MethodInfo vffSelectNext;

		static bool SplitterNoFrontDump(Building __instance)
		{
			if (!vffIsSplitter(__instance) || vffSelectedOutput(__instance).IsValid) return true;
			vffSelectNext.Invoke(__instance, null);
			if (!vffSelectedOutput(__instance).IsValid) return true;
			vffState.SetValue(__instance, vffMoving);
			return false;
		}

		// VFE Factory conveyors ignore Kanban Stockpile's max stack size. A belt that ends at a storage building (or at
		// a stockpile cell) tops the stack there up to the def's stack limit itself (`stackCount +=` in
		// CompleteTransfer; CanTransferToStorage and GetDumpCapability test the same limit), and drops a new stack whole.
		// Kanban only acts on hauling jobs and on Thing.TryAbsorbStack, so a hopper capped at 300 that a belt feeds
		// filled to the full stack (2,469 gold seen), and since a belt also empties the capped hopper it starts from,
		// haulers carried the whole stockpile in, 300 at a time. In those three methods every "def.stackLimit" of a
		// stack lying in storage now reads min(stack limit, Kanban max stack of that storage), and a new stack is
		// placed only up to the cap; the rest waits on the belt, as it already does at a full stack. Storage without a
		// Kanban max stack is untouched. The refill threshold is not applied: a belt tops up whenever there is room.
		// Belt pieces merge what they carry up to the stack limit as well (TryAdd, TransferItemTo and their checks), so
		// with only the storage capped the surplus piled up on the belt piece in front of it (3,639 gold on the
		// underground exit). A stack riding the belt piece that ends at capped storage gets that storage's cap too.
		// The cap carries back along the whole line: every belt piece takes the Kanban max stack of the storage its line
		// ends at (through turns, underground links and splitters; at a splitter the smallest cap of its branches), so
		// a long backed-up line holds one capped stack per piece instead of full stacks on the middle pieces.
		static HdKanbanStackCap.KanbanGet vffKanbanGet;
		static int vffCapReads, vffCapPlaces;
		static Type vffConveyor, vffUnderground, vffEntrance;
		static AccessTools.FieldRef<object, Thing> vffLinked;
		static readonly Dictionary<int, (int tick, int mss)> vffLineMss = new();
		static int vffWalkDepth;

		// Kanban max stack (0 = none) of the storage this belt piece's line ends at.
		// ponytail: answer kept 60 ticks per piece, so a rebuilt line or a changed max stack reaches the belts within a
		// second; hook Kanban's settings-changed event and the conveyor recache if that lag ever shows.
		static int LineMss(Building belt)
		{
			int now = Find.TickManager.TicksGame;
			if (vffLineMss.TryGetValue(belt.thingIDNumber, out var hit) && now - hit.tick is >= 0 and < 60) return hit.mss;
			if (vffWalkDepth >= 256) return 0; // looped line
			if (vffLineMss.Count > 4096) vffLineMss.Clear();
			vffWalkDepth++;
			int mss;
			try { mss = LineMssUncached(belt); }
			finally { vffWalkDepth--; }
			vffLineMss[belt.thingIDNumber] = (now, mss);
			return mss;
		}

		// Next pieces as the mod picks outputs (IsValidOutput): the belt ahead unless it points back at this piece, and
		// a belt to either side that runs across this one and away from it. No belt to go to: the faced cell's storage.
		static int LineMssUncached(Building belt)
		{
			var map = belt.Map;
			if (vffEntrance.IsInstanceOfType(belt))
				return vffLinked(belt) is Building { Spawned: true } exit && exit.Map == map ? LineMss(exit) : 0;
			var ahead = belt.Position + belt.Rotation.FacingCell;
			bool anyBelt = false;
			int best = 0;
			if (BeltAt(ahead, map) is { } next && next.Position + next.Rotation.FacingCell != belt.Position)
			{
				anyBelt = true;
				best = LineMss(next);
			}
			if (!vffUnderground.IsInstanceOfType(belt))
				foreach (var side in new[] { belt.Rotation.Rotated(RotationDirection.Clockwise), belt.Rotation.Rotated(RotationDirection.Counterclockwise) })
				{
					if (BeltAt(belt.Position + side.FacingCell, map) is not { } turn || vffUnderground.IsInstanceOfType(turn)
						|| turn.Rotation != side) continue;
					anyBelt = true;
					int mss = LineMss(turn);
					if (mss > 0 && (best == 0 || mss < best)) best = mss;
				}
			return anyBelt ? best : KanbanMssAt(ahead, map);
		}

		static Building BeltAt(IntVec3 cell, Map map)
		{
			if (!cell.InBounds(map)) return null;
			foreach (var thing in map.thingGrid.ThingsListAt(cell))
				if (vffConveyor.IsInstanceOfType(thing)) return (Building)thing;
			return null;
		}

		static IEnumerable<CodeInstruction> ConveyorKanbanCap(IEnumerable<CodeInstruction> instructions)
		{
			var def = AccessTools.Field(typeof(Thing), nameof(Thing.def));
			var limit = AccessTools.Field(typeof(ThingDef), nameof(ThingDef.stackLimit));
			var place = AccessTools.Method(typeof(GenPlace), nameof(GenPlace.TryPlaceThing), [typeof(Thing), typeof(IntVec3), typeof(Map),
				typeof(ThingPlaceMode), typeof(Action<Thing, int>), typeof(Predicate<IntVec3>), typeof(Rot4?), typeof(int)]);
			var codes = instructions.ToList();
			for (int i = 1; i < codes.Count; i++)
			{
				if (codes[i].LoadsField(limit) && codes[i - 1].LoadsField(def) && codes[i].labels.Count == 0)
				{
					codes[i - 1].opcode = OpCodes.Call;
					codes[i - 1].operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(KanbanStackCap));
					codes[i].opcode = OpCodes.Nop;
					codes[i].operand = null;
					vffCapReads++;
				}
				else if (codes[i].Calls(place))
				{
					codes[i].operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(KanbanCappedPlace));
					vffCapPlaces++;
				}
			}
			return codes;
		}

		// One short owner-key string per call inside Kanban's API; only reached for a cell that is storage.
		static int KanbanMssAt(IntVec3 cell, Map map) =>
			cell.InBounds(map) && map.haulDestinationManager.SlotGroupAt(cell)?.Settings?.owner is { } owner
			&& vffKanbanGet(owner, out _, out _, out int mss) ? mss : 0;

		// A stack lying in storage: that storage's cap. A stack riding a belt: the cap of the storage its line ends at.
		static int KanbanStackCap(Thing thing)
		{
			int limit = thing.def.stackLimit;
			int mss = thing.Spawned ? KanbanMssAt(thing.Position, thing.Map)
				: thing.holdingOwner?.Owner is Building { Spawned: true } belt && vffConveyor.IsInstanceOfType(belt) ? LineMss(belt) : 0;
			return mss > 0 ? Math.Min(limit, mss) : limit;
		}

		static bool KanbanCappedPlace(Thing thing, IntVec3 cell, Map map, ThingPlaceMode mode, Action<Thing, int> placedAction,
			Predicate<IntVec3> nearPlaceValidator, Rot4? rot, int squareRadius)
		{
			int cap = KanbanMssAt(cell, map) is > 0 and var mss ? Math.Min(thing.def.stackLimit, mss) : thing.def.stackLimit;
			if (thing.stackCount <= cap) return GenPlace.TryPlaceThing(thing, cell, map, mode, placedAction, nearPlaceValidator, rot, squareRadius);
			var piece = thing.SplitOff(cap);
			if (!GenPlace.TryPlaceThing(piece, cell, map, mode, placedAction, nearPlaceValidator, rot, squareRadius))
				thing.TryAbsorbStack(piece, false);
			return false; // the rest stays on the belt
		}

		// EPrime's Readouts does not show the vanilla resource counter: it scans spawned items and haul-source
		// containers itself (GameCounts.AccumulateMap) and shows that tally. A stack riding a VFE Factory conveyor is
		// neither (not spawned, and a conveyor is no haul source), so belt stock was missing from the readout (306 gold)
		// while the vanilla counter, which VFE Factory extends with belt contents, and Colony Manager had it. After
		// each scan, belt contents are added as stored, unforbidden stock, with VFE Factory's own rules (fresh, not
		// fogged). Stacks inside an underground tunnel stay uncounted, as in the vanilla counter.
		static MethodInfo eprAddSearch;
		static Type eprBeltType;
		static List<ThingDef> eprBeltDefs;
		static readonly Dictionary<ThingDef, int> eprBeltStock = new();

		static void EprCountBeltStock(Map map, object accumulator)
		{
			eprBeltDefs ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.thingClass != null && eprBeltType.IsAssignableFrom(def.thingClass)).ToList();
			foreach (var beltDef in eprBeltDefs)
				foreach (var belt in map.listerThings.ThingsOfDef(beltDef))
				{
					if (belt.Position.Fogged(map) || (belt as IThingHolder)?.GetDirectlyHeldThings() is not { } held) continue;
					for (int i = 0; i < held.Count; i++)
					{
						var thing = held[i].GetInnerIfMinified();
						if (!thing.def.CountAsResource || thing.IsNotFresh()) continue;
						eprBeltStock.TryGetValue(thing.def, out int count);
						eprBeltStock[thing.def] = count + thing.stackCount;
					}
				}
			// The scan runs every 204 ticks and belts carry a handful of defs, so a reflection call per def is cheap.
			foreach (var stock in eprBeltStock)
				eprAddSearch.Invoke(accumulator, [stock.Key.defName, (int)stock.Key.shortHash, stock.Value, true, false]);
			eprBeltStock.Clear();
		}

		// VFE Farming scarecrow: CompScaresAnimals.ScareAnimals walks map.mapPawns.AllPawns, which also lists pawns that
		// are not spawned (held in a building or container). For such a wild animal in range it asks for a flee cell on
		// its null map: NullReferenceException in CellFinderLoose.GetFleeDestAnimal ("Exception ticking VFE_Scarecrow"),
		// and the animals after it in the list are not scared. Walk only spawned pawns.
		static IEnumerable<CodeInstruction> ScarecrowSpawnedOnly(IEnumerable<CodeInstruction> instructions)
		{
			var all = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawns));
			var spawned = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned));
			int replaced = 0;
			foreach (var code in instructions)
			{
				if (code.Calls(all))
				{
					code.operand = spawned;
					replaced++;
				}
				yield return code;
			}
			if (replaced != 1)
				Log.Error($"[Harmony3StrictCompat] VFE Farming scarecrow: expected one AllPawns call, replaced {replaced}");
		}

		// ---- IMPACT - Combat Overhaul: natural armor of animals, insects and entities can never be breached -------
		// Impact's armor is deterministic: penetration / armor <= 0.5 always deflects. Its way out is the focal breach
		// (each deflect wears the armor on that body part down), but for natural armor the breach is only read and
		// only added when the target is a mechanoid (one `isMechanoid` local). A megaspider, an insect centipede or a
		// nociosphere therefore deflects a weak attacker forever (seen: a hunting animal on a downed insect). Every use
		// of that local that gates the breach now reads true; the two metal-armor effect flags keep the real value.
		static IEnumerable<CodeInstruction> ImpactBreachNaturalArmor(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var isMech = AccessTools.PropertyGetter(typeof(RaceProperties), nameof(RaceProperties.IsMechanoid));
			int at = codes.FindIndex(c => c.Calls(isMech));
			if (at < 0 || at + 1 >= codes.Count || !codes[at + 1].IsStloc() || codes.FindIndex(at + 1, c => c.Calls(isMech)) >= 0)
			{
				Log.Error("[Harmony3StrictCompat] Impact ArmorDeflection.Prefix: isMechanoid local not found: its mod updated, redo the natural-armor breach fix");
				return codes;
			}
			var local = codes[at + 1].operand;
			if (local == null) { Log.Error("[Harmony3StrictCompat] Impact ArmorDeflection.Prefix: isMechanoid local has no operand, natural-armor breach fix skipped"); return codes; }
			bool Loads(CodeInstruction c) => c.IsLdloc() && Equals(c.operand, local);
			int gates = 0, kept = 0, flags = 0;
			for (int i = at + 2; i < codes.Count; i++)
			{
				if (Loads(codes[i]))
				{
					if (i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Stind_I1) { kept++; continue; }   // deflectedByMetalArmor = isMechanoid
					codes[i].opcode = OpCodes.Ldc_I4_1;
					codes[i].operand = null;
					gates++;
				}
				else if (flags == 0 && codes[i].opcode == OpCodes.Ldc_I4_1 && i > 0 && codes[i - 1].IsLdarg() && i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Stind_I1)
				{
					codes[i].opcode = OpCodes.Ldloc_S;   // diminishedByMetalArmor = isMechanoid (was true inside the mech-only branch)
					codes[i].operand = local;
					flags++;
				}
			}
			if (gates != 3 || kept != 1 || flags != 1)
				Log.Error($"[Harmony3StrictCompat] Impact ArmorDeflection.Prefix: expected 3 breach gates, 1 kept flag and 1 diminished flag, got {gates}/{kept}/{flags}: its mod updated, check the natural-armor breach fix");
			return codes;
		}

		static IEnumerable<CodeInstruction> CmrStartResearchOnce(IEnumerable<CodeInstruction> instructions)
		{
			var finish = AccessTools.Method(typeof(ResearchManager), nameof(ResearchManager.FinishProject));
			int replaced = 0;
			foreach (var code in instructions)
			{
				if (code.Calls(finish))
				{
					code.opcode = OpCodes.Call;
					code.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(CmrFinishUnlessDone));
					replaced++;
				}
				yield return code;
			}
			if (replaced != 1)
				Log.Error($"[Harmony3StrictCompat] Colony Manager starting research: expected one FinishProject call, replaced {replaced}");
		}

		// Flourish + Muzzle Flash: both drew a flash on every shot. Muzzle Flash only covers guns that carry its
		// MuzzleFlashProps extension (vanilla and its add-on packs), with art made for each gun; Flourish's generic
		// bloom and flash now stay off for those guns and still cover every other gun.
		static Type mfProps;
		static void FlSkipMuzzleFlashGuns(Pawn pawn, ref bool __result)
		{
			if (__result && pawn?.equipment?.Primary?.def.modExtensions is { } extensions && extensions.Any(e => mfProps.IsInstanceOfType(e)))
				__result = false;
		}

		// VE pipe networks (PipeSystem): taps and drains are plain one-cell Building_Storage. A tap's "max" only caps what
		// it makes from the network, so haulers filled taps to the item's stack limit; Hauler's Dream "haul to stack" even
		// prefers the tap (always a partial stack) over the drain next to it. A drain only feeds the network while its
		// tanks have room, so with full tanks it became a pile. Now a tap accepts only its own output already on its cell,
		// up to the set max (a bigger stack gets hauled off, to the drain). A drain is storage only while its network
		// has free storage: when the tanks are full, nothing is hauled onto it and what sits on it is hauled off to
		// ordinary storage (even lower priority); once room frees up, that storage feeds the drain again.
		// Two checks, both per cell, so linked taps ("Link storage", where the settings belong to the StorageGroup and
		// not to a building) work too; a linked group otherwise shuttled one tap's output onto the other tap:
		//  - destination: StoreUtility.IsGoodStoreCell, which vanilla's search and Hauler's Dream ask for every
		//    candidate cell. A tap cell takes only the stack already on it, up to the max; a drain cell takes items once
		//    its network has room for the stack or at least DrainMinTrip items (no trickle of tiny hauls), and the haul
		//    is sized to that room (PipeDrainSpaceLeft below), so nothing is left on the drain to be hauled back off.
		//  - stored or not: StorageSettings.AllowedToAccept (behind IsInValidStorage). A stack on a tap above its max,
		//    or on a drain whose network is full, counts as unstored, so it gets hauled off; the search above then
		//    cannot pick the same cell again.
		//  - target check: StoreUtility.IsValidStorageFor, the same cell rule. Vanilla's merge job (WorkGiver_Merge) only
		//    asks this: with linked taps it carried one tap's output onto the other tap's (20 on one, refill on the
		//    other, the 20 off to the drain, and again). It is also the drop-time check of a haul.
		static void PipeTapDrainCell(IntVec3 c, Map map, Thing t, ref bool __result) => __result = PipeCellTakes(c, map, t, __result);
		static void PipeTapDrainValidCell(IntVec3 c, Map map, Thing storable, ref bool __result) => __result = PipeCellTakes(c, map, storable, __result);

		// ponytail: fixed minimum trip; half a stack limit was too big under Ogre Stack (a 663 chemfuel stack never fit 514 free).
		const int DrainMinTrip = 50;

		static bool PipeCellTakes(IntVec3 c, Map map, Thing t, bool vanilla)
		{
			if (!vanilla || t == null || c.GetEdifice(map) is not Building_Storage b || !PipeTapsAndDrains.Watched(b.def, out bool tap))
				return vanilla;
			bool onCell = t.Spawned && t.Position == c;
			return tap ? onCell && t.stackCount <= PipeTapsAndDrains.TapMax(b, t.def.stackLimit)
				: PipeTapsAndDrains.DrainHasRoom(b, onCell ? 1 : Math.Min(t.stackCount, DrainMinTrip));
		}

		// A drain cell offers only what its network can still absorb, so vanilla's haul count (HaulToCellStorageJob sums
		// GetItemStackSpaceLeftFor over the target cells) and Hauler's Dream's space count carry no more than that.
		static void PipeDrainSpaceLeft(IntVec3 c, Map map, ref int __result)
		{
			if (__result > 0 && c.GetEdifice(map) is Building_Storage b && PipeTapsAndDrains.Watched(b.def, out bool tap) && !tap)
				__result = Math.Min(__result, PipeTapsAndDrains.DrainFreeItems(b));
		}

		static void PipeTapOverMaxUnstored(StorageSettings __instance, Thing t, ref bool __result)
		{
			if (__result && t is { Spawned: true } && t.Position.GetEdifice(t.Map) is Building_Storage b && b.GetStoreSettings() == __instance
				&& PipeTapsAndDrains.Watched(b.def, out bool tap))
				__result = tap ? t.stackCount <= PipeTapsAndDrains.TapMax(b, t.def.stackLimit) : PipeTapsAndDrains.DrainHasRoom(b, 1);
		}

		// ---- Hospitality: nobles shop for food their title accepts ------------------------------------------------
		// JoyGiver_BuyFood qualifies any nutritious item (JoyGiver_BuyStuff.CanEat) and ranks by FoodOptimality, which
		// knows nothing of royal titles. A conceited Duke (lavish meals only) bought legendary cheese and simple meals,
		// would not eat them, and got GuestHasNoFood; GetRequiresFoodFactor counted that same food as "carrying
		// enough", so he stopped shopping for more. CanEat feeds both, so food vanilla calls inappropriate for the
		// title (FoodUtility.InappropriateForTitle; starving guests excepted) no longer counts as edible there.
		static void HospTitleFood(Thing thing, Pawn pawn, ref bool __result)
		{
			if (__result && FoodUtility.InappropriateForTitle(thing.def, pawn, allowIfStarving: true))
				__result = false;
		}

		// The pick itself never asks CanEat: both shopping JoyGiverDefs (BuyFood, BuyStuff) run the base
		// JoyGiver_BuyStuff, whose Qualifies is only the def's requestGroup, and Likey ranks food by FoodOptimality.
		// With the fix above a dominus carrying only cheese counts as having no food, so he kept buying food he would
		// not eat until his silver ran out (2026-11-24: 11 legendary cheese and 10 simple meals, 7 silver left,
		// GuestHasNoFood). Food the title rejects is no candidate at all; starving guests still may buy it.
		static void HospTitleFoodQualifies(Thing thing, Pawn pawn, ref bool __result)
		{
			if (__result && thing.def.IsNutritionGivingIngestible && FoodUtility.InappropriateForTitle(thing.def, pawn, allowIfStarving: true))
				__result = false;
		}

		// ---- ReplaceLib: recipes on different benches treated as duplicates ----
		// ReplaceLib removes "duplicate" recipes after every game load (Game.FinalizeInit postfix, Utils.ProcessRecipes):
		// same label, one product of the same thing and count, and RecipeUsersAreSame. That compares only recipeUsers, which
		// is null for any recipe its bench lists in its own <recipes>, so Fortifications' lathe "make component"
		// (FT_Make_ComponentIndustrial, 20 steel) counted as a duplicate of vanilla's fabrication bench "make component" and
		// was removed from the DefDatabase. Bills already loaded kept working, but saving and loading again in the same
		// session hit "Could not load reference to Verse.RecipeDef named FT_Make_ComponentIndustrial" and dropped the bill
		// (three lost lathe bills, 2026-11-13 to 11-17). Compare the benches that actually offer each recipe instead.
		static void RlSameBenchesOnly(RecipeDef recipeA, RecipeDef recipeB, ref bool __result)
		{
			if (__result && (recipeA.recipeUsers == null || recipeB.recipeUsers == null))
				__result = new HashSet<ThingDef>(recipeA.AllRecipeUsers).SetEquals(recipeB.AllRecipeUsers);
		}

		// VE PipeSystem (1.7 build): CompResourceTrader.CompInspectStringExtra formats its rate with PipeNet.def, and
		// PipeNet is null while the building is not on a map. Selecting a minified pipe consumer (gas wall lamp) threw
		// a NullReferenceException every frame. A minified building has no rate or network to report, so no line.
		static bool PipeTraderInspectUnspawned(ThingComp __instance, ref string __result)
		{
			if (__instance.parent.Spawned)
				return true;
			__result = "";
			return false;
		}

		// Leo - Events + Biomes! Caverns (personal tweak): the "unexpected mineral seams" cave is a pocket map whose
		// generator fixes the vanilla Underground biome. Pick one of the three Caverns biomes for each new cave, so
		// its terrain, fungi and plants, weather and wildlife come from that biome. The tile copies the biome at
		// generation, so an existing cave keeps its own. Caverns' stalagmite/crystal gen steps are added in XML.
		static readonly string[] cavernBiomes = ["BMT_FungalForest", "BMT_CrystalCaverns", "BMT_EarthenDepths"];
		static void LeoSeamsRandomCavern(MapGeneratorDef generatorDef)
		{
			if (generatorDef?.defName == "LE_MineralSeams" && generatorDef.pocketMapProperties != null
				&& cavernBiomes.Select(DefDatabase<BiomeDef>.GetNamedSilentFail).Where(b => b != null).TryRandomElement(out var biome))
				generatorDef.pocketMapProperties.biome = biome;
		}

		// Vanilla GenStep_Terrain deletes rock on lava (supportsRock false) and then strips roofs that would be
		// unsupported, which leaves the middle of a lava pool open to the sky. Right for a surface map, but the cave is
		// underground: the first Earthen Depths cave (2026-11-12) had 92 open cells, so its room read "Unroofed (92)".
		// Put the generator's rock roof back on every open cell.
		static void LeoSeamsRoofLava(MapGeneratorDef generatorDef, Map __result)
		{
			if (__result == null || generatorDef?.defName != "LE_MineralSeams") return;
			var roof = generatorDef.roofDef ?? RoofDefOf.RoofRockThick;
			foreach (var cell in __result.AllCells)
				if (!cell.Roofed(__result))
					__result.roofGrid.SetRoof(cell, roof);
		}

		// Vanilla WildAnimalSpawner.CommonalityOfAnimalNow reads Find.WorldGrid[map.Tile] (pollution, coast, tile
		// mutators). A pocket map has no world tile, so it threw "Index was out of range" on every spawn try once a cave
		// had a biome with wildlife (vanilla Underground has animal density 0; Caverns biomes do not). On pocket maps
		// weigh animals by the biome alone.
		static readonly AccessTools.FieldRef<WildAnimalSpawner, Map> spawnerMap = AccessTools.FieldRefAccess<WildAnimalSpawner, Map>("map");
		static bool PocketMapAnimalCommonality(WildAnimalSpawner __instance, PawnKindDef def, IntVec3 loc, ref float __result)
		{
			var map = spawnerMap(__instance);
			if (map == null || !map.IsPocketMap) return true;
			__result = def.RaceProps.waterSeeker && !map.terrainGrid.AnyWaterCells ? 0f : map.BiomeAt(loc).CommonalityOfAnimal(def);
			return false;
		}

		// The "need research project" alerts cast the research tab's window to MainTabWindow_Research to pick a tab.
		// Nice Research Tab replaces that window, so clicking the alert threw InvalidCastException (UI Rethemed -
		// Notifications then stopped drawing alerts under the gizmos until restart, 2026-11-12). With a replaced window
		// just open the research tab.
		static bool ResearchAlertOpenTab()
		{
			if (MainButtonDefOf.Research.TabWindow is MainTabWindow_Research) return true;
			Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
			return false;
		}

		// Smart Deconstruct cancels a mine job that would bring down a thick roof and removes the target's Mine
		// designation, but looks only for Mine. Ore marked with Odyssey's vein designation (MineVein, which also hands
		// out Mine jobs) kept its designation, so the work giver re-issued the job at once: "started 10 jobs in one tick"
		// and a "cannot mine" message per tick (compressed gold in Leo's cave, 2026-11-12). Fall back to MineVein.
		static void SdVeinDesignation(Thing b, DesignationDef def, Map map, ref Designation __result)
		{
			if (__result == null && def == DesignationDefOf.Mine && DesignationDefOf.MineVein != null && b != null && map != null)
				__result = map.designationManager.DesignationAt(b.Position, DesignationDefOf.MineVein);
		}

		// Colony Groups: the load ID is type name + groupID, but colony groups and task forces number separately (a
		// task force for a second map, e.g. Leo's cave, is "1" next to "Colony 1"), so loading logged "An item with the
		// same key has already been added. Key: TacticalGroups.ColonyGroup1". Nothing saves references to these groups,
		// so giving task forces their own ID changes no saved data.
		static FieldInfo tgTaskForce;
		static void TgTaskForceLoadId(object __instance, ref string __result)
		{
			if ((bool)tgTaskForce.GetValue(__instance))
				__result += "_TaskForce";
		}

		// A ritual's finished obligation can land in completedObligations twice (seen with VFE Tribals' advance-to-spacer
		// ritual, 2026-11-12). Both copies are deep-saved with the same ID, so every load logged "An item with the same
		// key has already been added. Key: RitualObligation_30". Save each finished obligation once.
		static void RitualSaveCompletedOnce(Precept_Ritual __instance)
		{
			if (Scribe.mode == LoadSaveMode.Saving && __instance.completedObligations is { Count: > 1 } done)
			{
				var seen = new HashSet<int>();
				done.RemoveAll(o => o != null && !seen.Add(o.ID));
			}
		}

		static void AegCareSwitchFor(Def __instance, Pawn pawn, ref bool __result)
		{
			if (__result && __instance.defName == AegSwitchDef) __result = aegComp(pawn) != null;
		}

		// UIR only asks Contains (CareSwitchDef.IsOn); nothing enumerates or edits it.
		sealed class AegEnabledPawns : ICollection<Pawn>
		{
			public bool Contains(Pawn pawn) => aegComp?.Invoke(pawn) is { } comp && (bool)aegEnabled.GetValue(comp);
			public int Count => 0;
			public bool IsReadOnly => true;
			public void Add(Pawn item) => throw new NotSupportedException();
			public void Clear() => throw new NotSupportedException();
			public bool Remove(Pawn item) => throw new NotSupportedException();
			public void CopyTo(Pawn[] array, int arrayIndex) { }
			public IEnumerator<Pawn> GetEnumerator() => Enumerable.Empty<Pawn>().GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		// ---- UI Rethemed - Notifications: the top-left messages drawn as rows in its notification stack ----
		// UIR restyles letters, conditions and alerts into one column but leaves the game's transient messages
		// (Messages.MessagesDoGUI) as plain text in the top-left; its own patch there only shifts them sideways when
		// its column sits top-left. While a game is loaded and the column is bottom-right, middle-right (the default
		// since 2026-11-13) or top-right, draw each live message as a StatusRow, the widget its alert rows use, next to the alerts:
		// bottom-right stacked upward from their top (LetterStack.LastTopY - AlertsReadout.AlertsHeight), middle-right and
		// top-right hanging below their bottom (LastTopY, set by UIR's Placement.EndLetters), newest nearest the alerts. Colour and icon come from UIR's own four kinds, so they match the alert rows below. Behaviour is
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
		static int uirBottomRight, uirMiddleRight, uirTopRight;
		static Type uirMinimapType;
		static bool uirTileFeatures;
		const int AbTileFeaturesWindowId = -32520; // Alpha Biomes' ImmediateWindow 32520 ("Features in this tile:"); WindowStack stores immediate ids negated

		// UIR's top-right column starts 12 px from the top (lower only under colonist portraits or the learning helper)
		// and knows nothing of windows in the top-right corner, which then cover its top: Satellite Minimap's window,
		// and on the world map Alpha Biomes' "Features in this tile:" panel (an immediate window at the screen's top
		// right while a tile with mutators is selected). Start below whichever is open. ponytail: assumes the minimap
		// sits in the top-right corner (the user's layout); a minimap moved elsewhere still pushes the column down by
		// its bottom edge.
		static void UirTopRightUnderWindows(ref float __result)
		{
			if ((int)uirPositionCall.Invoke(null, null) != uirTopRight) return;
			foreach (var window in Find.WindowStack.Windows)
				if (window.GetType() == uirMinimapType || uirTileFeatures && window.ID == AbTileFeaturesWindowId && window is ImmediateWindow)
					__result = Mathf.Max(__result, window.windowRect.yMax + 10f);
		}

		// Satellite Minimap files colony mechanoids under "Colonists" (Feed.GatherCore: IsColonist || IsColonyMech) and
		// Feed.Add draws at most 50 markers per class, so a colony with many mechs shows "Colonists (95)" and the pawns
		// past the 50th (in map spawn-list order) get no dot at all. Personal tweak: a "Mechanoids" row under Colonists
		// in the layers menu. Unchecked, GatherCore never sees colony mechs (no dot, not counted). The row is a
		// sub-filter: mechs still need Colonists checked. The setting is saved in the minimap's own settings file.
		// The per-class cap goes from 50 to 250 so that checked rows show every pawn they count.
		// GatherCore also files every predator animal under "Predators" whatever its faction, so the colony's own
		// hunting animals got the big outlined threat dot and could not be hidden apart from wild predators. A colony
		// predator now counts as a colony animal (colony animal colour); a manhunting one is still a hostile.
		// "Predators" is left with the wild and other factions' ones.
		// GatherCore shows colony animals and wildlife on one switch (showWildlife, read once per class). Colony
		// animals get their own: a "Colony animals" row under Colonists, independent of every other row, and the
		// "Wildlife & animals" row becomes "Wildlife" with the wild count alone.
		const int SmMarkerCap = 250;
		static bool smShowMechs = true, smShowColonyAnimals = true;
		static MethodInfo smToggleCall;
		static FieldInfo smCountsField;
		static readonly List<Pawn> smPawnsNoMechs = [];
		static void SmExposeShowMechs()
		{
			Scribe_Values.Look(ref smShowMechs, "h3sc_showMechs", true);
			Scribe_Values.Look(ref smShowColonyAnimals, "h3sc_showColonyAnimals", true);
		}
		static bool SmShowColonyAnimals(object minimapSettings) => smShowColonyAnimals;
		static IReadOnlyList<Pawn> SmPawnsToMark(MapPawns pawns)
		{
			var all = pawns.AllPawnsSpawned;
			if (smShowMechs) return all;
			smPawnsNoMechs.Clear();
			for (int i = 0; i < all.Count; i++)
				if (!all[i].IsColonyMech) smPawnsNoMechs.Add(all[i]);
			return smPawnsNoMechs;
		}
		static FloatMenu SmLayersMenu(List<FloatMenuOption> options)
		{
			string colonists = "SM_TglColonists".Translate().Resolve();
			int at = options.FindIndex(o => o.Label.Contains(colonists));
			if (at >= 0 && Find.CurrentMap is { } map)
			{
				var counts = (int[])smCountsField.GetValue(null); // 0 colonists (and shown mechs), 3 wildlife, 4 colony animals
				int mechs = map.mapPawns.SpawnedColonyMechs.Count;
				if (smShowMechs) // Counts[0] then holds colonists and mechs together: show the colonists alone
					options[at].Label = options[at].Label.Replace($"({counts[0]})", $"({Math.Max(0, counts[0] - mechs)})");
				options.Insert(at + 1, SmSubRow($"Mechanoids ({mechs})", smShowMechs, v => smShowMechs = v));
				options.Insert(at + 2, SmSubRow($"{"SM_ColColonyAnimal".Translate().Resolve()} ({counts[4]})", smShowColonyAnimals, v => smShowColonyAnimals = v));
				string wildlifeRow = $"{"SM_TglWildlife".Translate().Resolve()} ({counts[3] + counts[4]})";
				if (options.Find(o => o.Label.Contains(wildlifeRow)) is { } wildlife)
					wildlife.Label = wildlife.Label.Replace(wildlifeRow, $"{"SM_ColWildlife".Translate().Resolve()} ({counts[3]})");
			}
			return new FloatMenu(options);
		}
		// A row made by the minimap's own Toggle (flips the flag, writes its settings, refreshes the markers), indented.
		static FloatMenuOption SmSubRow(string label, bool on, Action<bool> set)
		{
			var row = (FloatMenuOption)smToggleCall.Invoke(null, [label, on, set]);
			row.Label = "    " + row.Label;
			return row;
		}
		static bool SmPredatorNotOurs(bool predator, Pawn pawn) => predator && pawn.Faction != Faction.OfPlayer;
		static IEnumerable<CodeInstruction> SmGatherWithoutMechs(IEnumerable<CodeInstruction> instructions) =>
			SmColonyPredatorsAsAnimals(SmSwap(instructions, "pawn list read", code => code.Calls(AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned))),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(SmPawnsToMark)))));
		// "raceProps.predator" becomes "SmPredatorNotOurs(raceProps.predator, pawn)"; the pawn is loaded the way
		// GatherCore loads it for its own "pawn.RaceProps" read just before.
		static IEnumerable<CodeInstruction> SmColonyPredatorsAsAnimals(IEnumerable<CodeInstruction> instructions)
		{
			var raceProps = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.RaceProps));
			var predator = AccessTools.Field(typeof(RaceProperties), nameof(RaceProperties.predator));
			var showWildlife = AccessTools.Field("SatelliteMinimap.MinimapSettings:showWildlife");
			CodeInstruction previous = null, loadPawn = null;
			int patched = 0, wildlifeReads = 0;
			foreach (var code in instructions)
			{
				if (code.Calls(raceProps) && previous != null && previous.IsLdloc()) loadPawn = previous;
				// the class switch reads showWildlife twice: first for wildlife (class 3), then for colony animals (class 4)
				if (code.LoadsField(showWildlife) && ++wildlifeReads == 2)
				{
					code.opcode = OpCodes.Call;
					code.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(SmShowColonyAnimals));
				}
				yield return code;
				if (loadPawn != null && code.LoadsField(predator))
				{
					yield return new CodeInstruction(loadPawn.opcode, loadPawn.operand);
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(SmPredatorNotOurs)));
					patched++;
				}
				previous = code;
			}
			if (patched != 1)
				Log.Error($"[Harmony3StrictCompat] Satellite Minimap: expected one predator check after the pawn's RaceProps read, patched {patched}");
			if (wildlifeReads != 2)
				Log.Error($"[Harmony3StrictCompat] Satellite Minimap: expected two showWildlife reads in the class switch, found {wildlifeReads}");
		}
		static IEnumerable<CodeInstruction> SmRaiseMarkerCap(IEnumerable<CodeInstruction> instructions) =>
			SmSwap(instructions, "marker cap of 50", code => code.LoadsConstant(50), new CodeInstruction(OpCodes.Ldc_I4, SmMarkerCap));
		static IEnumerable<CodeInstruction> SmLayersMenuWithMechs(IEnumerable<CodeInstruction> instructions) =>
			SmSwap(instructions, "FloatMenu construction", code => code.opcode == OpCodes.Newobj && Equals(code.operand, AccessTools.Constructor(typeof(FloatMenu), [typeof(List<FloatMenuOption>)])),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(SmLayersMenu))));
		static IEnumerable<CodeInstruction> SmSwap(IEnumerable<CodeInstruction> instructions, string what, Func<CodeInstruction, bool> match, CodeInstruction with)
		{
			int swapped = 0;
			foreach (var code in instructions)
			{
				if (match(code))
				{
					code.opcode = with.opcode;
					code.operand = with.operand;
					swapped++;
				}
				yield return code;
			}
			if (swapped != 1)
				Log.Error($"[Harmony3StrictCompat] Satellite Minimap: expected one {what}, replaced {swapped}");
		}

		// UIR Notifications "days of food" row: FoodReadout.Read re-counts every real second and rebuilds the hover card's
		// text, so its card restarts each second and can only be read while paused. Re-count every 5 s, and not at all
		// while the row is hovered (hover seen this frame or the last), so the card holds still until the mouse leaves.
		static FieldInfo uirFoodNextRead, uirFoodShowsField;
		static int uirFoodHoverFrame = -10;
		static void UirFoodNoteHover(Rect row)
		{
			if (Mouse.IsOver(row)) uirFoodHoverFrame = Time.frameCount;
		}
		static bool UirFoodHoldWhileHovered(out float __state)
		{
			__state = (float)uirFoodNextRead.GetValue(null);
			return !(Time.frameCount - uirFoodHoverFrame <= 1 && (bool)uirFoodShowsField.GetValue(null));
		}
		static void UirFoodReadEvery5s(float __state)
		{
			if ((float)uirFoodNextRead.GetValue(null) != __state)
				uirFoodNextRead.SetValue(null, Time.realtimeSinceStartup + 5f);
		}

		// UIR Colonist Card info rows: the meal "Ingredients" line ends with " (* only stacks with similar meals)", which
		// takes a third of the row. The row shows the line without that note; its tooltip shows the whole line.
		// The rows are drawn outside the inspect window's own pass, so Mouse.IsOver (and with it UIR's CoreWidgets.Tip)
		// says "blocked" for them: no row hover, no tooltip. In DrawRow the mouse test is the plain rect test, and the
		// tooltip is entered straight into TooltipHandler's active tips (what TipRegion does once IsOver passes); UIR Core
		// draws it as its hover card. Since UIR's 2026-11-18 build DrawRow only tips rows it had to truncate.
		static readonly Dictionary<string, string> uirInfoFullLines = [];
		static readonly AccessTools.FieldRef<Dictionary<int, ActiveTip>> tipActive = AccessTools.StaticFieldRefAccess<Dictionary<int, ActiveTip>>(AccessTools.Field(typeof(TooltipHandler), "activeTips"));
		static readonly AccessTools.FieldRef<int> tipFrame = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(typeof(TooltipHandler), "frame"));
		static string uirStackNote;
		static void UirInfoStripStackNote(ref string str)
		{
			if (str == null) return;
			uirStackNote ??= " (* " + "OnlyStacksWithCompatibleMeals".Translate().Resolve() + ")";
			if (!str.Contains(uirStackNote)) return;
			if (uirInfoFullLines.Count > 200) uirInfoFullLines.Clear();
			foreach (var line in str.Split('\n'))
				if (line.Contains(uirStackNote))
					uirInfoFullLines[line.Replace(uirStackNote, "").TrimEnd('\r')] = line.TrimEnd('\r');
			str = str.Replace(uirStackNote, "");
		}

		static bool UirInfoRowMouseIn(Rect rect) => rect.Contains(Event.current.mousePosition);

		// UIR's row tip: title is the row key (text before ": "), body the value; only called for truncated rows
		static void UirInfoRowCardTip(Rect rect, string title, string body, string key, Color? accent)
		{
			string line = title == null ? body : title + ": " + body;
			if (line != null) UirInfoRowTip(rect, new TipSignal(line));
		}

		static void UirInfoRowTip(Rect rect, TipSignal tip)
		{
			if (Event.current.type != EventType.Repaint || tip.text == null || !rect.Contains(Event.current.mousePosition)) return;
			string text = uirInfoFullLines.TryGetValue(tip.text, out var full) ? full : tip.text;
			var tips = tipActive();
			if (!tips.TryGetValue(tip.uniqueId, out var active))
			{
				tips[tip.uniqueId] = active = new ActiveTip(tip);
				active.firstTriggerTime = Time.realtimeSinceStartup;
			}
			active.lastTriggerFrame = tipFrame();
			active.signal.text = text;
		}

		static IEnumerable<CodeInstruction> UirInfoRowHoverCard(IEnumerable<CodeInstruction> instructions)
		{
			var tipRegion = AccessTools.Method("UIRethemed.Core.CoreWidgets:Tip", [typeof(Rect), typeof(string), typeof(string), typeof(string), typeof(Color?)]);
			var isOver = AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver), [typeof(Rect)]);
			int tips = 0, overs = 0;
			foreach (var code in instructions)
			{
				if (code.Calls(tipRegion))
				{
					code.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(UirInfoRowCardTip));
					tips++;
				}
				else if (code.Calls(isOver))
				{
					code.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(UirInfoRowMouseIn));
					overs++;
				}
				yield return code;
			}
			if (tips != 1 || overs == 0)
				Log.Error($"[Harmony3StrictCompat] UIR Colonist Card info row: expected one CoreWidgets.Tip and some Mouse.IsOver calls, replaced {tips} and {overs}");
		}
		// Styles are built on first draw (main thread, defs loaded): UIR's NotificationStyles.Resolve touches its texture
		// fields and NotificationColorDefOf, and from the mod constructor that runs UIR's texture loading off the main
		// thread (and sees null DefOfs).
		static MethodInfo uirResolveCall;
		static object[] uirMessageStyles;
		static readonly Color?[] uirMessageAccents = new Color?[4];
		static Action<Rect, string, string, string, Color?> uirHoverRegion;
		static MethodInfo uirPlacedDraw;
		static Func<string, string, Vector2> uirPlacedSizeCall;
		static Func<Rect, float, float, float> uirBesideCall;
		const string UirAlertsPart = "Notifications alerts";   // UIR's own Failsafe part name for its alert hover cards
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

		// Messages roll-up: UIR's column has folding section headings (Letters, Alerts, Events) drawn by
		// SectionHeading.Draw, which takes the HudSection enum and keeps the folded state by the enum's name in
		// UIR's settings. The message rows get a heading of their own by passing the unused value 3; NameOf/IconOf
		// prefixes give that value the name "Messages" and Phosphor's list icon. Folding hides the rows (messages
		// still expire). Off when UIR's "section headings" setting is off, or if UIR ever defines a 4th section.
		const int UirMessagesSectionValue = 3;
		static object uirMessagesSection;
		static MethodInfo uirHeadingShows, uirHeadingFolded, uirHeadingDraw, uirHeadingWidth;
		static Func<float> uirColumnShared;
		static readonly object[] uirMessageColorDefs = new object[4];

		static void UirMessagesSectionSetup(Harmony harmony)
		{
			var sectionType = AccessTools.TypeByName("UIRethemed.Notifications.HudSection");
			var shows = Target("UIRethemed.Notifications.SectionHeading:Shows");
			var folded = Target("UIRethemed.Notifications.SectionHeading:Folded");
			var draw = Target("UIRethemed.Notifications.SectionHeading:Draw");
			var widthFor = Target("UIRethemed.Notifications.SectionHeading:WidthFor");
			var nameOf = Target("UIRethemed.Notifications.SectionHeading:NameOf");
			var iconOf = Target("UIRethemed.Notifications.SectionHeading:IconOf");
			var shared = Target("UIRethemed.Notifications.HudColumn:get_Shared");
			var unseen = Target("UIRethemed.Notifications.SectionHeading:Unseen");
			var see = Target("UIRethemed.Notifications.SectionHeading:See");
			var widestName = Target("UIRethemed.Notifications.SectionHeading:get_WidestName");
			if (sectionType == null || shows == null || folded == null || draw == null || widthFor == null || nameOf == null || iconOf == null || shared == null
				|| unseen == null || see == null || widestName == null)
				return;
			if (Enum.IsDefined(sectionType, UirMessagesSectionValue))
			{
				Log.Error($"[Harmony3StrictCompat] UI Rethemed - Notifications now defines HudSection {Enum.ToObject(sectionType, UirMessagesSectionValue)}: Messages roll-up off, redo it");
				return;
			}
			uirMessagesSection = Enum.ToObject(sectionType, UirMessagesSectionValue);
			uirHeadingShows = shows; uirHeadingFolded = folded; uirHeadingDraw = draw; uirHeadingWidth = widthFor;
			uirColumnShared = (Func<float>)shared.CreateDelegate(typeof(Func<float>));
			harmony.Patch(shared, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirSharedWithMessages)));
			harmony.Patch(nameOf, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirMessagesSectionName)));
			harmony.Patch(iconOf, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirMessagesSectionIcon)));
			harmony.Patch(unseen, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirMessagesSectionUnseen)));
			harmony.Patch(see, prefix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirMessagesSectionSee)));
			harmony.Patch(widestName, postfix: new HarmonyMethod(typeof(Harmony3StrictCompatMod), nameof(UirWidestWithMessages)));
		}

		// Heading width (Static: every heading's width; Dynamic: the column width a heading asks for) is sized for the
		// widest name among UIR's own sections, so "Messages" got cut to "Mess...". Count it too, measured as UIR's
		// NotificationsWidgets.Width does (Small font).
		static void UirWidestWithMessages(ref float __result)
		{
			var font = Text.Font;
			Text.Font = GameFont.Small;
			__result = Mathf.Max(__result, Mathf.Ceil(Text.CalcSize("Messages").x));
			Text.Font = font;
		}

		// UIR (2026-11-13) counts items that arrived while a section was folded, in arrays sized to its 3 sections and
		// indexed by the section value, so value 3 would throw. The Messages heading opts out: no "new" count or glow.
		// ponytail: messages expire in 30 s anyway; give it its own seen set if the glow is wanted while folded.
		static bool UirMessagesSectionUnseen(object section, ref int __result)
		{
			if (Convert.ToInt32(section) != UirMessagesSectionValue) return true;
			__result = 0;
			return false;
		}

		static bool UirMessagesSectionSee(object section) => Convert.ToInt32(section) != UirMessagesSectionValue;

		// Messages' wanted column width from the last draw (0 when none show). UIR's sections read the shared width
		// before drawing, so they follow the messages one frame later.
		static float uirMessagesWanted;

		static void UirSharedWithMessages(ref float __result)
		{
			if (uirMessagesWanted > __result) __result = uirMessagesWanted;
		}

		static bool UirMessagesSectionName(object section, ref string __result)
		{
			if (Convert.ToInt32(section) != UirMessagesSectionValue) return true;
			__result = "Messages";
			return false;
		}

		static Texture2D uirMessagesIcon;

		static bool UirMessagesSectionIcon(object section, ref Texture2D __result)
		{
			if (Convert.ToInt32(section) != UirMessagesSectionValue) return true;
			__result = uirMessagesIcon ??= ContentFinder<Texture2D>.Get("UIRethemedCore/Phosphor/list");
			return false;
		}

		static object[] UirStyles()
		{
			if (uirMessageStyles != null) return uirMessageStyles;
			// Kinds 0-3 are UIR's NotificationKind (Neutral, Good, Warning, Bad); Resolve(kind) with no extensions is its
			// plain style for that kind. Accent is the colour after the user's theme override.
			var styles = new object[4];
			var kindType = uirResolveCall.GetParameters()[0].ParameterType;
			for (int kind = 0; kind < 4; kind++)
			{
				var style = uirResolveCall.Invoke(null, new object[] { Enum.ToObject(kindType, kind), null, null, null });
				styles[kind] = style;
				var colorDef = AccessTools.Field(style.GetType(), "color").GetValue(style);
				uirMessageColorDefs[kind] = colorDef;
				uirMessageAccents[kind] = colorDef == null ? null : (Color?)AccessTools.PropertyGetter(colorDef.GetType(), "Accent").Invoke(colorDef, null);
			}
			return uirMessageStyles = styles;
		}

		static bool UirMessagesInStack()
		{
			int position = (int)uirPositionCall.Invoke(null, null);
			bool hanging = position == uirMiddleRight || position == uirTopRight;
			if (Current.ProgramState != ProgramState.Playing || Find.LetterStack == null || Find.Alerts == null
				|| (position != uirBottomRight && !hanging))
			{
				uirMessagesWanted = 0f;
				return true;
			}
			var messages = liveMessages();
			if (messages.Count == 0)
			{
				uirMessagesWanted = 0f;
				return false;
			}
			var styles = UirStyles();
			bool heading = uirMessagesSection != null && (bool)uirHeadingShows.Invoke(null, new object[] { uirMessagesSection, messages.Count });
			bool folded = heading && (bool)uirHeadingFolded.Invoke(null, new object[] { uirMessagesSection });
			// Width as UIR's sections work it out (rows 154-320 px each, heading's own width), reported to its shared
			// column through the HudColumn.Shared postfix, so every section and the messages share one width.
			float wanted = 0f;
			foreach (var msg in messages) wanted = Mathf.Max(wanted, uirRowWidthCall(msg.text));
			if (heading) wanted = Mathf.Max(wanted, (float)uirHeadingWidth.Invoke(null, new object[] { uirMessagesSection, messages.Count }));
			uirMessagesWanted = wanted;
			float width = uirColumnShared != null ? uirColumnShared() : wanted;
			float x = uirColumnXCall(width);
			// Slots top-down: heading, then rows. Bottom-right: the block ends above the alerts, newest row lowest.
			// Middle-right and top-right: LastTopY is the alerts' bottom (UIR's EndLetters), the block hangs below, newest row highest.
			int slots = (heading ? 1 : 0) + (folded ? 0 : messages.Count);
			float y = hanging ? Find.LetterStack.LastTopY + UirStackGap
				: Find.LetterStack.LastTopY - Find.Alerts.AlertsHeight - UirStackGap - (slots - 1) * UirRowStep - UirRowHeight;
			if (heading)
			{
				// Its colour is the most pressing message (bad, then warning).
				int pressing = -1;
				foreach (var msg in messages)
				{
					int kind = UirKindOf(msg.def);
					if ((kind == 3 || kind == 2) && kind > pressing) pressing = kind;
				}
				uirHeadingDraw.Invoke(null, new object[] { new Rect(x, Mathf.Round(y), width, UirRowHeight),
					uirMessagesSection, messages.Count, pressing < 0 ? null : uirMessageColorDefs[pressing], float.MaxValue,
					(Func<string>)(() => string.Join("\n", messages.Select(m => m.text).Reverse())), (Func<IEnumerable<object>>)(() => messages), 0, null });
				y += UirRowStep;
			}
			if (folded) return false;
			for (int j = 0; j < messages.Count; j++, y += UirRowStep)
			{
				var msg = messages[hanging ? messages.Count - 1 - j : j];
				var rect = new Rect(x, Mathf.Round(y), width, UirRowHeight);
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
								uirMessageAccents[UirKindOf(msg.def)], UirAlertsPart });
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

		// ---- VSIE "cured my <relation>" thought shows an unresolved label --------------------------------------
		// ThoughtWorker_CuredMyFriend grants the thought when the patient is a relative by pawn.GetRelations, which
		// includes implied relations (sibling, cousin, grandparent...). Thought_CuredMyFriend.LabelCap only searches
		// DirectRelations, so for those it falls back to base.LabelCap: the raw "{SECONDPAWN_nameDef} cured my {0}"
		// template, a "Could not resolve symbol" error, and UI Rethemed then drops to the vanilla Social tab. It also
		// genders the relation word by the observer instead of the patient. Same tale lookup, vanilla relation pick.
		static Type vsieTaleType;
		static FieldInfo vsieFirst, vsieSecond, vsieThird;
		static Func<Pawn, Tale, bool> vsieHaveNoticed;

		static bool VsieCuredMyFriendLabel(Thought_SituationalSocial __instance, ref string __result)
		{
			Pawn pawn = __instance.pawn, other = __instance.otherPawn, second = null, third = null;
			Tale latest = null;
			foreach (var tale in Find.TaleManager.AllTalesListForReading)
			{
				if (tale.def != __instance.def.taleDef || !vsieTaleType.IsInstanceOfType(tale) || !vsieHaveNoticed(pawn, tale)) continue;
				if ((vsieFirst.GetValue(tale) as TaleData_Pawn)?.pawn != pawn) continue;
				var s = (vsieSecond.GetValue(tale) as TaleData_Pawn)?.pawn;
				if (s != other || (latest != null && tale.AgeTicks >= latest.AgeTicks)) continue;
				latest = tale; second = s; third = (vsieThird.GetValue(tale) as TaleData_Pawn)?.pawn;
			}
			if (third == null) return true;
			string relation = pawn.GetMostImportantRelation(third)?.GetGenderSpecificLabel(third) ?? "friend";
			__result = __instance.CurStage.label.Formatted(relation, second.Named("SECONDPAWN"), third.Named("THIRDPAWN"), pawn.Named("PAWN"));
			return false;
		}

		// ---- UI Rethemed hover card: long rows with a value overlap the next row ---------------------------------
		// HoverCard.DrawGroup draws a row with a value through Theme.StatLine: label and value share one 22 px rect
		// and LineHeight reserves a fixed 24, so a long label (VSIE's opinion thoughts are full sentences) wraps over
		// the row below and over its own value. Rows that fit keep UIR's own drawing. A row that does not fit gets its
		// label wrapped beside the value and a taller row. StatLine has other callers with fixed layouts, so only
		// calls made from DrawGroup change.
		static MethodInfo hcGroupBodyWidth; // private static float GroupBodyWidth(Card)
		static Action<Rect, string, TextAnchor, Color, GameFont> hcThemeLabel; // Theme.Label: theme fade (card appear) and colours
		static Func<Color> hcTextBright;
		static Func<Rect, float, Rect?, bool> hcHoverGlow; // Theme.HoverGlow: the row glow StatLine draws under every row
		static FieldInfo hcLabel, hcValue, hcIcon, hcBar, hcAccent;
		static bool hcInGroup;

		// UIR's Gear tab measures the vacuum gauge's label and value widths with whatever Text.Font the last drawer left
		// (its other gauges and the comfy-temperature texts force Small first), then draws them in Small. Measured in
		// Tiny, the label truncates to "Vacuum resist..." and "92%" to "9...", and the bar rect changes width, which
		// restarts its fill animation; the next 0.5 s rebuild may land on Small again, hence the flicker.
		static void UirVacuumFontEnter(out GameFont __state)
		{
			__state = Text.Font;
			Text.Font = GameFont.Small;
		}
		// Personal tweak on top: the stat's own "Vacuum resistance" label leaves the bar a few pixels wide, so the
		// gauge is relabelled "Vacuum res." and its label width remeasured (still in Small here).
		static void UirVacuumFontExit(GameFont __state)
		{
			if (uirGaugeLabel != null && uirVacuumGauge.GetValue(null) is { } gauge)
			{
				uirGaugeLabel.SetValue(gauge, "Vacuum res.");
				uirVacuumLabelWidth.SetValue(null, Text.CalcSize("Vacuum res.").x + 2f);
			}
			Text.Font = __state;
		}
		static FieldInfo uirVacuumGauge, uirVacuumLabelWidth, uirGaugeLabel;

		// Personal tweak: the Alt cell inspector is 336 wide with a 170 value column, so "Unbelievably impressive (170.3)"
		// is cut to "Unbelievably impressiv..." and the room stat numbers never show. The widths are inlined consts:
		// window/divider 336, row 312, value column and Truncate 170. Each gets + cellInspectorExtra in every method
		// that draws the window. The extra follows the content, the way vanilla sizes the height from last frame's
		// numLines: DrawRow and DrawHeader measure their text while drawing, and OnGUI turns the widest overflow of the
		// last fill into the next window's extra (0 when everything fits). Other mods' rows (Geological Landforms) go
		// through DrawRow and count too.
		const float CellInspectorMaxExtra = 300f; // never wider than 336 + this
		static float cellInspectorExtra, cellInspectorNeed = -1f; // -1: no fill since the last OnGUI, keep the old width

		static IEnumerable<CodeInstruction> WiderCellInspector(IEnumerable<CodeInstruction> instructions)
		{
			var extra = AccessTools.Field(typeof(Harmony3StrictCompatMod), nameof(cellInspectorExtra));
			foreach (var c in instructions)
			{
				yield return c;
				if (c.opcode == OpCodes.Ldc_R4 && c.operand is float f && (f == 336f || f == 312f || f == 170f))
				{
					yield return new CodeInstruction(OpCodes.Ldsfld, extra);
					yield return new CodeInstruction(OpCodes.Add);
				}
			}
		}

		static void CellInspectorResize()
		{
			if (cellInspectorNeed < 0f) return;
			cellInspectorExtra = Mathf.Min(Mathf.Ceil(cellInspectorNeed), CellInspectorMaxExtra);
			cellInspectorNeed = -1f;
		}

		// value column is 170 wide; LabelEllipses (Text.ClampTextWithEllipsis) cuts text wider than the column - 13
		static void CellInspectorMeasureRow(string info) =>
			cellInspectorNeed = Mathf.Max(cellInspectorNeed, Mathf.Max(0f, info.NullOrEmpty() ? 0f : Text.CalcSize(info).x + 14f - 170f));

		// heading (room role) is drawn centred in Medium font across the 312 row
		static void CellInspectorMeasureHeader(string text)
		{
			if (text.NullOrEmpty()) return;
			var font = Text.Font;
			Text.Font = GameFont.Medium;
			cellInspectorNeed = Mathf.Max(cellInspectorNeed, Mathf.Max(0f, Text.CalcSize(text).x + 8f - 312f));
			Text.Font = font;
		}

		// VEF ThingWithFloorGraphic (VFE Pirates warcasket weapons): on the map the thing shows the graphic of its
		// FloorGraphicExtension (the boxed weapon, drawSize 1), but Widgets.GetIconFor takes the icon scale from
		// GenUI.IconDrawScale(def), i.e. the weapon's own graphicData.drawSize (1.7). The box icon is drawn 1.7x and
		// spills out of its slot in the info card, inspect pane and storage tab. While the floor graphic is the icon,
		// the scale comes from the floor graphic's drawSize. Per def: 0 = no floor graphic.
		static FieldInfo floorGraphicData;
		static readonly Dictionary<ThingDef, float> floorIconScale = new();

		static void FloorGraphicIconScale(Thing thing, ref float scale)
		{
			// same test as ThingWithFloorGraphic.Graphic; a minified or carried thing has another holder
			if (thing?.ParentHolder is not Map ||!thing.def.uiIconPath.NullOrEmpty() || thing.UIIconOverride != null || thing.StyleDef?.UIIcon != null)
				return;
			var def = thing.def;
			if (!floorIconScale.TryGetValue(def, out float floorScale))
			{
				var extension = def.modExtensions?.Find(e => e.GetType() == floorGraphicData.DeclaringType);
				floorScale = extension != null && floorGraphicData.GetValue(extension) is GraphicData floor
					? def.uiIconScale * Mathf.Min(floor.drawSize.x / def.Size.x, floor.drawSize.y / def.Size.z) : 0f;
				floorIconScale[def] = floorScale;
			}
			if (floorScale > 0f)
				scale = floorScale;
		}

		// Personal tweak: the Compare gear window marks its two sides with the theme's blue (equipped) and yellow
		// (comparing): bars, slot frames, the "Equipped · Legendary" subtitle and the values. That blue is exactly UIR's
		// Excellent quality colour and the yellow is close to Legendary, so "Equipped · Legendary" reads as an Excellent
		// item and the other way round. The sides get teal and pink, hues no quality uses (red, brown, green, blue,
		// purple, orange). Swaps the HeldColor/OtherColor and Theme.TextBlue/TextYellow reads in Contents and Row.
		static readonly Color UirCompareHeld = new(0.21f, 0.77f, 0.75f), UirCompareOther = new(0.85f, 0.29f, 0.57f);
		static readonly Color UirCompareHeldText = new(0.60f, 0.91f, 0.89f), UirCompareOtherText = new(0.95f, 0.63f, 0.78f);
		static Color UirCompareHeldColor() => UirCompareHeld;
		static Color UirCompareOtherColor() => UirCompareOther;
		static Color UirCompareHeldTextColor() => UirCompareHeldText;
		static Color UirCompareOtherTextColor() => UirCompareOtherText;

		static IEnumerable<CodeInstruction> UirCompareSideColors(IEnumerable<CodeInstruction> instructions)
		{
			var swap = new Dictionary<string, string>
			{
				["get_HeldColor"] = nameof(UirCompareHeldColor),
				["get_OtherColor"] = nameof(UirCompareOtherColor),
				["get_TextBlue"] = nameof(UirCompareHeldTextColor),
				["get_TextYellow"] = nameof(UirCompareOtherTextColor),
			};
			int swapped = 0;
			foreach (var c in instructions)
			{
				if (c.opcode == OpCodes.Call && c.operand is MethodInfo m && swap.TryGetValue(m.Name, out var to)
					&& (m.DeclaringType.Name == "GearCompareWindow" || m.DeclaringType.FullName == "UIRethemed.Core.Theme"))
				{
					c.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), to);
					swapped++;
				}
				yield return c;
			}
			if (swapped == 0)
				Log.Error("[Harmony3StrictCompat] UI Rethemed GearCompareWindow shape changed, compare side colours not applied");
		}

		// Personal tweak: the Gear tab overview draws its cards as loadout, mass, stock-up, comfy temperature, combat.
		// Wanted: mass, combat, temperature, stock-up. The stock-up loop and the temperature card are inline, so the
		// `DrawGaugeCard(ref y, width)` call (ldloca y; ldloc width; call) is cut from the end and dropped in front of the
		// stock-up loop's init (`ldc.i4.0; stloc i; br COND`, COND found from `ldsfld stocks; callvirt get_Count`), and
		// the stock-up loop is cut from there and appended where the combat call was. Labels stay on the new first
		// instruction of each spot. Untouched when either shape is missing.
		static IEnumerable<CodeInstruction> UirStatsPanelOrder(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var gearTab = AccessTools.TypeByName("UIRethemed.InspectorTabs.GearTab");
			var gauge = AccessTools.DeclaredMethod(gearTab, "DrawGaugeCard");
			var stocks = AccessTools.DeclaredField(gearTab, "stocks");
			int call = codes.FindIndex(c => c.Calls(gauge));
			int count = codes.FindIndex(c => c.opcode == OpCodes.Ldsfld && c.operand is FieldInfo f && f == stocks
				&& codes[codes.IndexOf(c) + 1].opcode == OpCodes.Callvirt && ((MethodInfo)codes[codes.IndexOf(c) + 1].operand).Name == "get_Count");
			if (call < 2 || count < 1 || codes[call - 2].opcode != OpCodes.Ldloca_S || !codes[call - 1].IsLdloc())
			{
				Log.Error("[Harmony3StrictCompat] UIR DrawStatsPanel shape changed, card order left as shipped");
				return codes;
			}
			// loop condition starts one before `ldsfld stocks` (ldloc i) and carries the label the init's br jumps to
			int cond = count - 1;
			var condLabels = codes[cond].labels;
			int br = codes.FindIndex(c => (c.opcode == OpCodes.Br || c.opcode == OpCodes.Br_S) && c.operand is Label l && condLabels.Contains(l));
			// loop ends at the blt after get_Count (+1 for the callvirt, +1 for blt)
			int loopEnd = count + 2;
			if (condLabels.Count == 0 || br < 2 || br > cond || !(codes[loopEnd].opcode == OpCodes.Blt || codes[loopEnd].opcode == OpCodes.Blt_S) || loopEnd >= call - 2)
			{
				Log.Error("[Harmony3StrictCompat] UIR DrawStatsPanel stock loop shape changed, card order left as shipped");
				return codes;
			}
			int loopStart = br - 2; // ldc.i4.0; stloc i; br COND
			var gaugeCall = codes.GetRange(call - 2, 3);
			var loop = codes.GetRange(loopStart, loopEnd + 1 - loopStart);
			var afterCall = codes[call + 1];
			// labels: whatever pointed at the loop init now points at the gauge call; whatever pointed at the gauge call
			// now points at the moved loop; the loop's own internal labels travel with it
			var loopLabels = loop[0].labels.ToList(); loop[0].labels.Clear();
			var callLabels = gaugeCall[0].labels.ToList(); gaugeCall[0].labels.Clear();
			codes.RemoveRange(call - 2, 3);
			codes.RemoveRange(loopStart, loop.Count);
			int insertAt = call - 2 - loop.Count; // old call position after the loop was cut
			loop[0].labels.AddRange(callLabels);
			codes.InsertRange(insertAt, loop);
			gaugeCall[0].labels.AddRange(loopLabels);
			codes.InsertRange(loopStart, gaugeCall);
			return codes;
		}

		// Full Armor - Hands and Feet rewrites every gloves/boots def (covers only Hands/Feet) to the Overhead layer so
		// they never conflict with armor it extends to those parts. UIR's doll classifier reads the layer before the
		// body part, so such items fall into Extra ("headgear" covering no head part) and the Hands/Feet cells stay
		// empty. Items on Overhead/EyeCover that cover hands or feet and no head, torso or leg group go to those cells.
		static Type uirDollSlot;
		static bool UirHandsFeetSlot(Apparel ap, ref object __result)
		{
			var props = ap.def.apparel;
			var layers = props.layers;
			if (layers == null || !(layers.Contains(ApparelLayerDefOf.Overhead) || layers.Contains(ApparelLayerDefOf.EyeCover)))
				return true;
			bool feet = false, hands = false;
			foreach (var g in props.bodyPartGroups)
			{
				switch (g.defName)
				{
					case "Feet": feet = true; break;
					case "Hands": case "LeftHand": case "RightHand": hands = true; break;
					case "FullHead": case "UpperHead": case "Eyes": case "Teeth": case "Mouth": case "Jaw": case "Nose":
					case "Torso": case "Legs": case "Waist":
						return true;
				}
			}
			if (!feet && !hands) return true;
			__result = Enum.ToObject(uirDollSlot, feet ? 8 : 6); // DollSlot.Feet / DollSlot.Hands
			return false;
		}

		// Personal tweak: empty doll slots are captioned in Tiny with the layer's label, and modded layer labels
		// longer than ~9 letters (Packs Are Not Belts' "Mechanitor", "Exoskeleton", "Ammunition") wrap onto a second line.
		static void UirShortSlotLabel(ref string __result)
		{
			switch (__result)
			{
				case "Mechanitor": __result = "Mech."; break;
				case "Exoskeleton": __result = "Exo."; break;
				case "Ammunition": __result = "Ammo"; break;
			}
		}

		// Personal tweak: the gear hover card subtitle is one string, "Masterwork  ·  Hardleather  ·  x3", drawn in one
		// colour (the quality accent whenever the item has a quality), so the material looked quality-coloured too.
		// Only the quality word keeps the accent; the rest gets the card's default subtitle colour. The label is drawn
		// with GUI.color = subtitleColor, which tints rich-text colours, so both parts are tagged and the tint is white.
		static FieldInfo uirCardSubtitle, uirCardSubtitleColor;
		const string UirCardSeparator = "  ·  ";

		static void UirCardQualityOnlyColored(Thing thing, object __result)
		{
			if (__result == null || !thing.TryGetQuality(out _) || uirCardSubtitle.GetValue(__result) is not string subtitle)
				return;
			int cut = subtitle.IndexOf(UirCardSeparator, StringComparison.Ordinal);
			if (cut <= 0 || subtitle.IndexOf('<') >= 0) return;
			var accent = (Color)uirCardSubtitleColor.GetValue(__result);
			var plain = (Color)uirCardSubtitleColor.GetValue(Activator.CreateInstance(__result.GetType()));
			if (accent == plain) return;
			uirCardSubtitle.SetValue(__result, $"<color=#{QcHex(accent)}>{subtitle.Substring(0, cut)}</color><color=#{QcHex(plain)}>{subtitle.Substring(cut)}</color>");
			uirCardSubtitleColor.SetValue(__result, Color.white);
		}

		// Smart Medicine's stock-up picker lists drugs, medicine and vanilla HemogenPack only; other hemogen packs and
		// carried meals are plain Foods, so they never show. Postfix on the picker's filter lambda admits these defs.
		static readonly HashSet<string> SmStockUpDefs = new()
		{
			"VRE_HemogenPack_Animal", "VRE_HemogenPack_Corpse", "VRE_HemogenPack_Sanguophage",
			"MealSurvivalPack", "MealNutrientPackaged",
		};
		static void SmStockUpExtras(ThingDef t, ref bool __result) => __result |= SmStockUpDefs.Contains(t.defName);

		// JobGiver_DropUnusedInventory drops low-preferability food (hemogen packs are DesperateOnly) from a Home-area
		// pawn's inventory once it has not used raw food for 150000 ticks. Smart Medicine's keep check only covers the
		// drug loop, so stocked hemogen packs were dropped and fetched again. Drop is the one exit of both loops.
		static Func<Pawn, Thing, bool> smIsStocked;
		static bool SmKeepStockedItem(Pawn pawn, Thing thing) => !smIsStocked(pawn, thing);

		// ---- UI Rethemed - Quests x Quality Colors: reward tiles and their tooltip titles ----------------
		// Reward labels reach the tiles without Quality Colors' tag, and the hover card strips tags from its title. A
		// postfix on the tile builder finds each tile's item through its click closure and wraps the label in the
		// item's Quality Colors colour; a label carrying such a colour is then drawn whole in it, and the card title
		// gets the colour back after the strip. Other coloured labels are left alone.
		// The same postfix moves the market value from the pill beside the name (which took half the tile and cut the
		// name short) to the second line under it.
		static FieldInfo qcSettings, qcColors;
		static readonly Regex QcTag = new("<color=#([0-9A-Fa-f]{6})>", RegexOptions.Compiled);
		static string qcPendingHex;

		static Type uirqTile;
		static MethodInfo uirqTextDim;
		// The stack count shares the value line; it is drawn in the theme's dim text colour so the value stands out.
		static Color UirqDim() => (uirqTextDim ??= AccessTools.PropertyGetter(AccessTools.TypeByName("UIRethemed.Quests.Theme"), "TextDim"))
			?.Invoke(null, null) is Color dim ? dim : new Color(0.7f, 0.7f, 0.7f);

		static void UirqRewardTiles(IList tiles)
		{
			var label = AccessTools.Field(uirqTile, "label");
			var line = AccessTools.Field(uirqTile, "line");
			var lineColor = AccessTools.Field(uirqTile, "lineColor");
			var silver = AccessTools.Field(uirqTile, "silver");
			var onClick = AccessTools.Field(uirqTile, "onClick");
			for (int i = 0; i < tiles.Count; i++)
			{
				object tile = tiles[i];
				if (silver.GetValue(tile) is float worth)
				{
					var count = line.GetValue(tile) as string;
					line.SetValue(tile, count.NullOrEmpty() ? worth.ToStringMoney() : $"<color=#{QcHex(UirqDim())}>{count}</color>   {worth.ToStringMoney()}");
					if ((Color)lineColor.GetValue(tile) == default) lineColor.SetValue(tile, Color.white);
					silver.SetValue(tile, null);
				}
				if (qcColors != null && label.GetValue(tile) is string text && UirqQualityOf(onClick.GetValue(tile) as Delegate) is { } quality
					&& qcColors.GetValue(qcSettings.GetValue(null)) is Dictionary<QualityCategory, Color> colors && colors.TryGetValue(quality, out var c))
					label.SetValue(tile, $"<color=#{QcHex(c)}>{ColoredText.StripTags(text)}</color>");
				tiles[i] = tile;
			}
		}

		// The tile keeps no item reference; its click handler's closure does (a Thing, or the remembered def + quality).
		static QualityCategory? UirqQualityOf(Delegate click)
		{
			if (click?.Target is not { } closure) return null;
			foreach (var field in closure.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				var value = field.GetValue(closure);
				if (value is Thing thing && thing.TryGetQuality(out var quality)) return quality;
				if (value is ThingStuffPairWithQuality pair && pair.thing != null && pair.thing.HasComp(typeof(CompQuality))) return pair.Quality;
			}
			return null;
		}

		static string QcHex(Color c) => $"{Mathf.RoundToInt(c.r * 255f):X2}{Mathf.RoundToInt(c.g * 255f):X2}{Mathf.RoundToInt(c.b * 255f):X2}";

		static string QcQualityHex(string text)
		{
			if (text == null || text.IndexOf("<color=#", StringComparison.Ordinal) < 0) return null;
			if (qcColors.GetValue(qcSettings.GetValue(null)) is not Dictionary<QualityCategory, Color> colors) return null;
			foreach (Match m in QcTag.Matches(text))
				foreach (var c in colors.Values)
					if (string.Equals(m.Groups[1].Value, QcHex(c), StringComparison.OrdinalIgnoreCase))
						return m.Groups[1].Value;
			return null;
		}

		static void UirqLabelQuality(ref string text, ref Color color)
		{
			var hex = QcQualityHex(text);
			if (hex == null) return;
			color = new Color(Convert.ToInt32(hex.Substring(0, 2), 16) / 255f, Convert.ToInt32(hex.Substring(2, 2), 16) / 255f,
				Convert.ToInt32(hex.Substring(4, 2), 16) / 255f, color.a);
			text = ColoredText.StripTags(text);
		}

		// Region / ContentRegion strip the title and then call Register; the prefix order carries the colour across.
		static void UirqTipTitle(string title) => qcPendingHex = QcQualityHex(title);
		static void UirqTipRegister(ref string title)
		{
			if (qcPendingHex != null && !title.NullOrEmpty()) title = $"<color=#{qcPendingHex}>{title}</color>";
			qcPendingHex = null;
		}

		// Hauler's Dream shields Smart Medicine stock only while the stack is not tagged as its own haul. The tag spreads
		// to every inventory stack of a def the pawn hauls, so a stocked stack gets tagged, HD unloads part of it, Stock Up
		// fetches it back into the same tagged stack, and the stack's tag age ends in the "Cannot unload inventory"
		// alert. KeepCountOf is the keep count HD honours on tagged stacks too (drug policy, loadouts): count the stock
		// there. Max, not sum: Stock Up and the other sources each fill the same inventory up to their own count.
		static Func<Pawn, ThingDef, int> smStockCountOf;
		static void HdKeepStockUpCount(Pawn pawn, ThingDef def, ref int __result)
		{
			if (pawn?.inventory != null && def != null) __result = Math.Max(__result, smStockCountOf(pawn, def));
		}

		// ---- Hospitality guests: null area in PawnCanAutomaticallyHaul ------------------------------------
		// Hospitality makes guests care about forbidden cells and answers InAllowedArea from the guest area, but a guest's
		// playerSettings.allowedAreas stays empty. For food outside the guest area vanilla then builds its fail reason from
		// EffectiveAreaRestrictionInPawnCurrentMap.Label on null. CookFillHopper is the one work giver that reaches this
		// with forbidden things, because it walks every food item on the map without the scanner's forbidden check.
		static IEnumerable<CodeInstruction> HaulAreaLabelNullSafe(IEnumerable<CodeInstruction> instructions)
		{
			var label = AccessTools.PropertyGetter(typeof(Area), nameof(Area.Label));
			int replaced = 0;
			foreach (var code in instructions)
			{
				if (code.Calls(label))
				{
					code.opcode = OpCodes.Call;
					code.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(AreaLabelOrGuest));
					replaced++;
				}
				yield return code;
			}
			if (replaced != 1)
				Log.Error($"[Harmony3StrictCompat] PawnCanAutomaticallyHaul: expected one Area.Label call, replaced {replaced}");
		}

		static string AreaLabelOrGuest(Area area) => area?.Label ?? "guest area";

		// ---- Unburned + VEF: reach melee weapons never auto-attack while drafted --------------------------
		// Unburned's prefix is a full copy of JobDriver_Wait.CheckForAutoAttack that always skips the original, so VEF's
		// transpiler on the original never runs. Its copy still treats every melee verb as "not a shooter". Same test
		// VEF injects: a melee verb with more reach than adjacent goes on to the target search.
		static Func<Verb, bool> vefIsVanillaMelee;
		static bool VerbIsAdjacentMelee(Verb verb) => verb.verbProps.IsMeleeAttack && vefIsVanillaMelee(verb);

		static IEnumerable<CodeInstruction> UnburnedKeepReachAutoAttack(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var verbProps = AccessTools.Field(typeof(Verb), nameof(Verb.verbProps));
			var isMelee = AccessTools.PropertyGetter(typeof(VerbProperties), nameof(VerbProperties.IsMeleeAttack));
			int replaced = 0;
			for (int i = codes.Count - 2; i >= 0; i--)
			{
				if (codes[i].opcode == OpCodes.Ldfld && Equals(codes[i].operand, verbProps) && codes[i + 1].Calls(isMelee))
				{
					codes[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(VerbIsAdjacentMelee))).MoveLabelsFrom(codes[i]);
					codes.RemoveAt(i + 1);
					replaced++;
				}
			}
			if (replaced != 1)
				Log.Error($"[Harmony3StrictCompat] Unburned auto-attack: expected one melee verb check, replaced {replaced}");
			return codes;
		}

		static void HcGroupEnter() => hcInGroup = true;
		static void HcGroupExit() => hcInGroup = false;

		// Height to add to a 22 px row, 0 when the label fits on one line beside the value.
		static float HcExtraHeight(string label, string value, float width, out float labelWidth)
		{
			labelWidth = width;
			if (label.NullOrEmpty() || value.NullOrEmpty()) return 0f;
			var font = Text.Font;
			bool wrap = Text.WordWrap;
			Text.Font = GameFont.Small;
			Text.WordWrap = false;
			labelWidth = width - Text.CalcSize(value).x - 8f;
			float extra = 0f;
			if (Text.CalcSize(label).x > labelWidth)
			{
				Text.WordWrap = true;
				extra = Mathf.Max(0f, Text.CalcHeight(label, labelWidth) - 22f);
			}
			Text.WordWrap = wrap;
			Text.Font = font;
			return extra;
		}

		// LineHeight(Card c, Line line); Line is a struct, read boxed
		static void HcLineHeight(object[] __args, ref float __result)
		{
			var line = __args[1];
			if (hcIcon.GetValue(line) != null || hcBar.GetValue(line) != null || hcAccent.GetValue(line) != null) return;
			string label = (string)hcLabel.GetValue(line), value = (string)hcValue.GetValue(line);
			if (label.NullOrEmpty() || value.NullOrEmpty()) return;
			__result += HcExtraHeight(label, value, (float)hcGroupBodyWidth.Invoke(null, [__args[0]]), out _);
		}

		static bool HcWrapStatLine(ref float y, float x, float width, Texture icon, string label, string value, Color? valueColor, float? bar, Color? labelColor)
		{
			if (!hcInGroup || icon != null || bar.HasValue) return true;
			float extra = HcExtraHeight(label, value, width, out float labelWidth);
			if (extra <= 0f) return true;
			bool wrap = Text.WordWrap;
			Text.WordWrap = true;
			hcHoverGlow?.Invoke(new Rect(x, y, width, 22f + extra), 0.08f, new Rect(x - 6f, y, width + 12f, 22f + extra)); // StatLine's glow, over the taller row
			hcThemeLabel(new Rect(x, y, labelWidth, 22f + extra), label, TextAnchor.UpperLeft, labelColor ?? hcTextBright(), GameFont.Small);
			Text.WordWrap = false;
			hcThemeLabel(new Rect(x, y, width, 22f), value, TextAnchor.UpperRight, valueColor ?? Color.white, GameFont.Small);
			Text.WordWrap = wrap;
			y += 24f + extra; // StatLineSpan(false)
			return false;
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

		// ---- Hauler's Dream: a bulk-hauled load goes straight back where it came from (net-zero loop) ------------
		// HD books "N units of this def are on their way to that storage" per pawn, and its IsGoodStoreCell postfix
		// refuses a storage whose free room is used up by such claims. A pawn's own claim is meant to be ignored
		// while it carries the load (StorageCommitments.IsDelivering). For a load in the inventory that test compares
		// Thing.ParentHolder (the Pawn_InventoryTracker) with inventory.innerContainer (a ThingOwner): never equal, so
		// it is always false. A bulk haul that claims all the free room of its destination (the usual case with a
		// Kanban cap, a nearly full feeder, fridge, hopper or log pile) then finds that destination "full of its own
		// claim" when it unloads, and vanilla's search falls through to the next priority: the storage the load just
		// left. Pawns with only hauling work (Lifters) pick the same stack again at once: 5-6 round trips a second
		// until HD backs the stack off ("bulk-hauled 6 times ... net-zero", "yielded no actual job"); food meant for
		// the fridge stays in the cellar. Found with a probe on 2026-11-15: at unload hdFree=0 for the feeder, and
		// hdFree=259 (the load) right after HD moved the claim. Count an inventory load as delivering.
		static void HdInventoryIsDelivering(Pawn asker, Thing subject, ref bool __result)
		{
			if (!__result && asker != null && subject is { Spawned: false } && subject.ParentHolder is Pawn_InventoryTracker inv && inv.pawn == asker)
				__result = true;
		}

		// ---- Hauler's Dream: "Cannot unload inventory" for pawns that cannot get up ------------------------------
		// HD's alert (Alert_CannotUnloadInventory.Evaluate) skips only drafted pawns. A pawn under anesthetic or
		// otherwise downed, or one in deathrest (days), keeps its hauled items in bed and shows as stuck once the
		// tags pass the alert's hours. Skipping Evaluate leaves its result at the enum default, UnloadFault.None,
		// which also clears the pawn's grace clock, so once it is up the alert waits the usual grace hour again.
		static bool HdNoUnloadAlertWhileImmobile(Pawn p) => p == null || !(p.Downed || p.Deathresting);

		// ---- Unique Melee Weapons: an ended warband quest keeps its unspawned raiders in the save --------------------
		// QuestPart_SpawnWarband holds the pre-generated warband (leader + raiders, deep-saved) until the site map
		// generates. When the quest ends without a visit (30-day timeout) the part never drops them, while the game
		// removes their temporary faction and its ideoligion. The next load warns "Could not resolve reference to
		// object with loadID Ideo_N" (once: the next save writes null), and the save keeps ~580 KB of pawns that
		// can never spawn.
		// Discard them when the quest cleans up, and once after load for quests that ended before this fix.
		static Type umwWarbandPart;
		static FieldInfo umwWarbandPawns;

		static void UmwDiscardWarband(Quest __instance)
		{
			foreach (var part in __instance.PartsListForReading)
			{
				if (part.GetType() != umwWarbandPart || umwWarbandPawns.GetValue(part) is not List<Pawn> { Count: > 0 } pawns)
					continue;
				foreach (var pawn in pawns)
				{
					try
					{
						if (pawn is { Spawned: false, Destroyed: false, Discarded: false } && !pawn.IsWorldPawn())
							Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
					}
					catch (Exception e)
					{
						Log.Warning($"[Harmony3StrictCompat] Unique Melee Weapons warband: could not discard {pawn}: {e.Message}");
					}
				}
				pawns.Clear();
			}
		}

		static void UmwDiscardEndedWarbands() => LongEventHandler.ExecuteWhenFinished(() =>
		{
			foreach (var quest in Find.QuestManager.QuestsListForReading)
				if (quest.Historical)
					UmwDiscardWarband(quest);
		});

		// ---- Hauler's Dream: a bill job restarts ten times a tick while another pawn carries its ingredient ---------
		// "Share for crafting" adds tagged stacks from other player pawns' inventories (mechs included) to a bill's
		// ingredient candidates. Only HD's batch job can take such a stack, and its JobOnThing route makes one only for
		// a bill the player marked as batch and a pawn that may batch it. Otherwise the job stays a vanilla DoBill
		// pointing at an item that is not on the map; its go-to-ingredient toil fails at once and JobGiver_Work hands
		// out the same job again: "<pawn> started 10 jobs in one tick. newJob=DoBill". Seen at smelters and a brewery
		// on 2026-11-18; the probe below caught it: queueB0=RawHops x35 spawned=False holder=Pawn_InventoryTracker
		// (Agrihand 1), bill=Make_Wort, worker Dragon. (My first reading, "unstackable items only", was wrong.)
		// So: unless this bill and worker can get the batch job, take other pawns' inventory stacks back out of the
		// candidates, and vanilla picks ingredients that are on the map. Second line of defense for a batch plan that
		// turns out infeasible: a vanilla DoBill that still targets another pawn's inventory is not handed out.
		static MethodInfo hdIsBatchBill, hdCanPawnBatch, hdInstance;

		static bool HeldByOtherPawn(Thing thing, Pawn worker) =>
			thing is { Spawned: false } && thing.ParentHolder is Pawn_InventoryTracker inv && inv.pawn != worker;

		static void HdShareOnlyForBatchJobs(Pawn worker, Bill bill, List<Thing> outList)
		{
			if (outList == null || worker == null || bill == null) return;
			bool decided = false, batch = false;
			for (int i = outList.Count - 1; i >= 0; i--)
			{
				if (!HeldByOtherPawn(outList[i], worker)) continue;
				if (!decided)
				{
					decided = true;
					var hd = hdInstance.Invoke(null, null);
					batch = hd != null && (bool)hdIsBatchBill.Invoke(hd, new object[] { bill })
						&& (bool)hdCanPawnBatch.Invoke(null, new object[] { worker, bill });
					if (batch) return;
				}
				outList.RemoveAt(i);
			}
		}

		static void HdNoBillOnForeignInventory(ref Job __result, Pawn pawn)
		{
			if (__result?.def != JobDefOf.DoBill || __result.targetQueueB == null) return;
			foreach (var target in __result.targetQueueB)
				if (HeldByOtherPawn(target.Thing, pawn)) { __result = null; return; }
		}

		// ---- TEMPORARY probe: "<pawn> started 10 jobs in one tick" -------------------------------------------------
		// The vanilla error names the job and target A only. Log every target of the job that was about to start: where
		// it is, who holds it, whether it is forbidden for this pawn or outside its allowed area, whether it can be
		// reserved, plus the bill and bench state. One warning line per error, read by Tools/rwwatch.py.
		[ThreadStatic] static Job probeJob;
		[ThreadStatic] static Pawn probePawn;

		static void JobLoopProbeRemember(Job newJob, Pawn ___pawn)
		{
			probeJob = newJob;
			probePawn = ___pawn;
		}

		static void JobLoopProbe(Pawn pawn, string message)
		{
			if (message == null || !message.Contains("jobs in one tick") || probeJob == null || probePawn != pawn) return;
			try
			{
				var job = probeJob;
				var sb = new StringBuilder("[Harmony3StrictCompat] job-loop probe: ");
				sb.Append(pawn.LabelShort).Append(' ').Append(job.def.defName).Append(" at ").Append(pawn.Position)
					.Append(" area=").Append(pawn.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap?.Label ?? "none")
					.Append(" playerForced=").Append(job.playerForced).Append(" count=").Append(job.count);
				ProbeTarget(sb, pawn, "A", job.targetA);
				ProbeTarget(sb, pawn, "B", job.targetB);
				ProbeTarget(sb, pawn, "C", job.targetC);
				if (job.targetQueueB != null)
					for (int i = 0; i < job.targetQueueB.Count && i < 6; i++)
					{
						ProbeTarget(sb, pawn, "queueB" + i, job.targetQueueB[i]);
						if (job.countQueue != null && i < job.countQueue.Count) sb.Append(" want=").Append(job.countQueue[i]);
					}
				if (job.bill != null)
					sb.Append(" | bill=").Append(job.bill.recipe?.defName).Append(" shouldDoNow=").Append(job.bill.ShouldDoNow())
						.Append(" deleted=").Append(job.bill.DeletedOrDereferenced);
				if (job.targetA.Thing is IBillGiver giver)
					sb.Append(" benchUsable=").Append(giver.CurrentlyUsableForBills());
				Log.Warning(sb.ToString());
			}
			catch (Exception e)
			{
				Log.Warning("[Harmony3StrictCompat] job-loop probe failed: " + e.Message);
			}
		}

		static void ProbeTarget(StringBuilder sb, Pawn pawn, string tag, LocalTargetInfo target)
		{
			if (!target.IsValid) return;
			sb.Append(" | ").Append(tag).Append('=');
			if (target.Thing is not { } thing)
			{
				sb.Append(target.Cell).Append(" inArea=").Append(target.Cell.InAllowedArea(pawn));
				return;
			}
			sb.Append(thing.def.defName).Append(" x").Append(thing.stackCount).Append(' ').Append(thing.ThingID)
				.Append(" spawned=").Append(thing.Spawned).Append(" destroyed=").Append(thing.Destroyed);
			if (thing.ParentHolder is { } holder && holder is not Map)
				sb.Append(" holder=").Append(holder.GetType().Name).Append('(').Append((holder as Pawn_InventoryTracker)?.pawn?.LabelShort
					?? (holder as Pawn_CarryTracker)?.pawn?.LabelShort ?? holder.ToString()).Append(')');
			if (thing.ParentHolder is Pawn_InventoryTracker { pawn: { Spawned: true } carrier })
				sb.Append(" holderPos=").Append(carrier.Position).Append(" holderInArea=").Append(carrier.Position.InAllowedArea(pawn));
			if (thing.Spawned)
				sb.Append(" pos=").Append(thing.Position).Append(" inArea=").Append(thing.Position.InAllowedArea(pawn))
					.Append(" forbidden=").Append(thing.IsForbidden(pawn)).Append(" canReserve=").Append(pawn.CanReserve(thing))
					.Append(" reach=").Append(pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly));
		}

		// ---- Giddy-Up 2: Mount job restarts ten times a tick for an animal the rider may not touch -----------------
		// JobDriver_Mount fails on a forbidden target (for a pawn that includes standing outside the rider's allowed
		// area), but IsMountable never tests that, so auto-mount picks the same animal again the moment the Mount job
		// dies: "<pawn> started 10 jobs in one tick. newJob=Mount" (2026-11-18, a meadow ave). Refuse such an animal
		// with the test the job driver itself uses.
		static void GuNoForbiddenMount(Pawn animal, Pawn rider, bool checkState, ref bool __result)
		{
			if (__result && checkState && rider is { Spawned: true } && animal is { Spawned: true } && animal.IsForbidden(rider))
				__result = false;
		}

		// ---- Friendly raids arrive dressed for the cold ------------------------------------------------------------
		// Vanilla gives a generated pawn a parka and a hat when the tile is cold; at extreme cold (-76 C on 2026-11-18)
		// that is far from enough, and RimPacts' defense-pact reinforcements (IncidentWorker_RaidFriendly, 151 pawns
		// that day) stand around after the fight and freeze. Dress for the Weather only covers neutral groups unless
		// "raiders come prepared" is on, and its 100 random tries per pawn would stall a group this size. For friendly
		// raids only: per pawn, put on the warmest non-armor apparel its faction can make (tech level, allowed stuff)
		// wherever that replaces something colder, until the pawn is comfortable or six pieces went on. Armor and
		// locked apparel stay. Candidates come from vanilla's own apparel pair list, once per faction def.
		static readonly Dictionary<FactionDef, List<ThingStuffPair>> warmPairs = new();

		static void FriendlyRaidDressedForCold(IncidentParms parms, List<Pawn> pawns, bool debugTest, bool __result)
		{
			if (!__result || debugTest || pawns == null || parms?.target is not Map map || parms.faction == null
				|| parms.faction.HostileTo(Faction.OfPlayer)) return;
			float outdoor = map.mapTemperature.OutdoorTemp;
			if (outdoor > 5f) return;
			if (!warmPairs.TryGetValue(parms.faction.def, out var pairs))
			{
				pairs = new List<ThingStuffPair>();
				if (AccessTools.Field(typeof(PawnApparelGenerator), "allApparelPairs")?.GetValue(null) is List<ThingStuffPair> all)
					foreach (var pair in all)
					{
						var ap = pair.thing.apparel;
						if (ap == null || !ap.canBeGeneratedToSatisfyWarmth || ap.mechanitorApparel || ap.layers.Contains(ApparelLayerDefOf.Belt)
							|| pair.thing.techLevel > parms.faction.def.techLevel || pair.Commonality <= 0f || pair.InsulationCold <= 0f
							|| IsArmorApparel(pair.thing) || (pair.stuff != null && !parms.faction.def.CanUseStuffForApparel(pair.stuff))) continue;
						pairs.Add(pair);
					}
				pairs.SortByDescending(pair => pair.InsulationCold);
				if (pairs.Count > 300) pairs.RemoveRange(300, pairs.Count - 300);
				warmPairs[parms.faction.def] = pairs;
			}
			int dressed = 0;
			foreach (var pawn in pawns)
				if (DressForCold(pawn, pairs, outdoor)) dressed++;
			if (dressed > 0)
				Log.Message($"[Harmony3StrictCompat] Friendly raid ({parms.faction.Name}): {dressed} of {pawns.Count} pawns got warmer apparel for {outdoor:0} C");
		}

		static bool IsArmorApparel(ThingDef def) =>
			def.IsWithinCategory(ThingCategoryDefOf.ApparelArmor) || def.IsWithinCategory(ThingCategoryDefOf.ArmorHeadgear);

		static bool DressForCold(Pawn pawn, List<ThingStuffPair> pairs, float outdoor)
		{
			if (pawn?.apparel == null || pawn.Dead || !pawn.RaceProps.Humanlike) return false;
			int worn = 0;
			bool check = true;
			for (int i = 0; i < pairs.Count && worn < 6; i++)
			{
				if (check && pawn.GetStatValue(StatDefOf.ComfyTemperatureMin, true, -1) <= outdoor) break;
				check = false;
				var pair = pairs[i];
				if (!pair.thing.apparel.PawnCanWear(pawn) || !ApparelUtility.HasPartsToWear(pawn, pair.thing)) continue;
				float replaced = 0f;
				bool blocked = false;
				foreach (var have in pawn.apparel.WornApparel)
				{
					if (ApparelUtility.CanWearTogether(pair.thing, have.def, pawn.RaceProps.body)) continue;
					if (pawn.apparel.IsLocked(have) || IsArmorApparel(have.def)) { blocked = true; break; }
					replaced += have.GetStatValue(StatDefOf.Insulation_Cold);
				}
				if (blocked || replaced >= pair.InsulationCold) continue;
				var apparel = (Apparel)ThingMaker.MakeThing(pair.thing, pair.stuff);
				PawnGenerator.PostProcessGeneratedGear(apparel, pawn);
				pawn.apparel.Wear(apparel, false);
				worn++;
				check = true;
			}
			return worn > 0;
		}

		// ---- VFE Pirates warcasket foundry x Shelf Stack Logistics: welding ingredients ignore the pickup point -------
		// Shelf Stack Logistics moves a bill ingredient from a depot tile to the pickup point nearest the bench by
		// wrapping the vanilla JobDriver_DoBill toil that runs right after the next ingredient is taken from the queue
		// (JumpIfTargetInsideBillGiver). VFE Pirates' warcasket welding job has its own CollectIngredientsToils, a copy
		// of the vanilla one without that toil, so welders walked to the depot itself, often across the map. Insert a
		// toil after the extract toil and let Shelf's own postfix wrap it; Shelf never reads its __instance argument.
		// Probed 2026-11-21: every ingredient goes through the pickup point; the later ones are picked up in the same
		// tick they appear, because the welder already stands next to it.
		static Action<JobDriver_DoBill, Toil, Toil, TargetIndex, TargetIndex> shelfPickupHook;

		static void WeldShelfPickup(ref IEnumerable<Toil> __result, TargetIndex ingredientInd, TargetIndex billGiverInd) =>
			__result = WithShelfPickup(__result, ingredientInd, billGiverInd);

		static IEnumerable<Toil> WithShelfPickup(IEnumerable<Toil> toils, TargetIndex ingredientInd, TargetIndex billGiverInd)
		{
			bool first = true;
			foreach (var toil in toils)
			{
				yield return toil;
				if (!first) continue;
				first = false;
				var hook = new Toil { initAction = () => { } };
				shelfPickupHook(null, hook, toil, ingredientInd, billGiverInd);
				yield return hook;
			}
		}

		// ---- Stylish Dialogs: centre the panel, not the panel plus the storyteller --------------------------------
		// Personal tweak. Stylish Dialogs draws letters and node-tree dialogs as one window: the storyteller portrait
		// on the left, the text panel from x = portrait width - 28 to 12 px short of the right edge
		// (CollageLayout.Decoration, portrait width min(430, 0.42 * width)). The game centres that whole window, so the
		// panel sits about 195 px right of the screen centre. Shift the window left by half the difference between
		// the panel's left and right gaps; the portrait then hangs off the left of a centred panel. Only when the
		// dialog has a portrait (no portrait: the panel already fills the window).
		static MethodInfo sdGetDialog;
		static PropertyInfo sdPortrait, sdArt;

		static void SdCenterPanel(Window __instance)
		{
			var type = __instance.GetType();
			if (type != typeof(Dialog_NodeTree) && type != typeof(Dialog_NodeTreeWithFactionInfo)) return;
			var dialog = sdGetDialog.Invoke(null, [__instance]);
			if (sdArt.GetValue(sdPortrait.GetValue(dialog)) == null) return;
			float shift = Mathf.Round((Math.Min(430f, __instance.windowRect.width * 0.42f) - 28f - 12f) / 2f);
			__instance.windowRect.x = Math.Max(0f, __instance.windowRect.x - shift);
		}

		// ---- Smarter Visitors x Event Image Popup: "visitors are about to leave" has no picture --------------------
		// Event Image Popup only decorates a Dialog_NodeTree opened while a letter or an incident is in scope. Smarter
		// Visitors asks "they are afraid to leave (temperature ...), may they stay?" with a Dialog_MessageBox from a
		// lord trigger, so neither holds, while Hospitality's matching arrival question gets the visitor picture.
		// Rebuild that one message box (known by its third button, SV.WaitALittle) as a node tree with the same text
		// and buttons in the same order, and put a VisitorGroup incident frame in scope while the window is added, so
		// Event Image Popup's own prefix (it runs after this one) attaches the picture it uses for visitor groups.
		static object eipFrames;
		static Type eipFrameType;

		static bool SvLeaveDialogWithImage(ref Window window, out bool __state)
		{
			__state = false;
			if (window is not Dialog_MessageBox box || box.buttonCText.NullOrEmpty() || !"SV.WaitALittle".CanTranslate()
				|| box.buttonCText != "SV.WaitALittle".Translate().ToString()) return true;
			var incident = DefDatabase<IncidentDef>.GetNamedSilentFail("VisitorGroup");
			// Stylish Dialogs' "Notice" letters do not open by themselves: ask through a letter instead, sent with the
			// visitor incident in scope so Event Image Popup tags it with the visitor picture.
			if (VisitorQuestionLetter.AsLetter && svAskingLord is { } lord)
			{
				if (incident != null) PushEipFrame(incident);
				try { VisitorQuestionLetter.SendLeaving(box.title, box.text, lord); }
				finally { if (incident != null) eipFrames.GetType().GetMethod("Pop").Invoke(eipFrames, null); }
				return false;
			}
			if (incident == null) return true;
			var node = new DiaNode(box.text);
			foreach (var (label, action) in new[] { (box.buttonBText, box.buttonBAction), (box.buttonCText, box.buttonCAction), (box.buttonAText, box.buttonAAction) })
				if (!label.NullOrEmpty())
					node.options.Add(new DiaOption(label) { action = action, resolveTree = true });
			if (node.options.Count == 0) return true;
			window = new Dialog_NodeTree(node, true, false, box.title);
			PushEipFrame(incident);
			__state = true;
			return true;
		}

		static void PushEipFrame(IncidentDef incident)
		{
			var frame = Activator.CreateInstance(eipFrameType);
			AccessTools.Field(eipFrameType, "def").SetValue(frame, incident);
			AccessTools.Field(eipFrameType, "parms")?.SetValue(frame, new IncidentParms());
			eipFrames.GetType().GetMethod("Push").Invoke(eipFrames, new[] { frame });
		}

		static void SvLeaveDialogDone(bool __state)
		{
			if (__state) eipFrames.GetType().GetMethod("Pop").Invoke(eipFrames, null);
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

		// CompShuttle.CompTick (every 600 ticks) throws out any contained pawn whose despawnedTick is older than 60000
		// and calls Notify_PawnLost(LeftVoluntarily) on its lord. The field means "last time this pawn left a map",
		// not "time spent in the shuttle": quest escorts and bestowers are redressed world pawns who may have visited
		// the colony years earlier, so they are evicted on the first check after landing, drop out of the escort lord,
		// walk off the map as lordless non-colonists, and the shuttle then waits forever for them after the ceremony
		// (the loop also removes while indexing ContainedPawns, so it hits every other pawn). Start the clock when
		// the pawn enters the transporter, which is what the one-day eviction was written for.
		static void ShuttleClockStartsAtBoarding(Thing t)
		{
			if (t is Pawn pawn) pawn.despawnedTick = Find.TickManager.TicksGame;
		}

		// ---- Busywork "Upper" job icon on LTO Colony Groups portraits -------------------------------------------
		// In Upper mode Busywork stops feeding its job icon to Useful Marks (so "Useful Marks in Colony Groups" has
		// nothing to draw) and draws it itself: over the pawn's head on the map, and in the portrait's top-left corner
		// via a postfix on vanilla ColonistBarColonistDrawer.DrawColonist. LTO Colony Groups replaces the colonist
		// bar with its own drawer, so that postfix never runs. Run Busywork's own postfix from LTO's DrawColonist; it
		// never touches its drawer argument, so null is fine, and the Upper-mode check, position and colours stay its own.
		static Action<ColonistBarColonistDrawer, Rect, Pawn> bwUpperMark;

		static void LtoBusyworkUpperMark(Rect rect, Pawn colonist) => bwUpperMark(null, rect, colonist);

		// ---- TD Enhancement Pack skill gain arrows on the UI Rethemed colonist card ------------------------------
		// Personal tweak: TD only transpiles the arrow into vanilla SkillUI.DrawSkill, which the card does not use. Its
		// recording in SkillRecord.Learn runs regardless, so call TD's drawer for each card row. It draws at
		// x = levelLabelWidth + 70 in the current group: -70 inside a group over the bar's right end puts it at the origin.
		// UIR's "level number on the left" setting (on by default) runs the bar to the cell's right edge (4 px margin);
		// with it off, the 20 px level number and its 2 px margin sit at the right end and the arrow goes just before them.
		static Action<SkillRecord, Rect, float> tdLearningArrow;
		static FieldInfo uirCardSettings, uirSkillLevelLeft;

		static void UirCardSkillArrow(Rect cell, SkillRecord skill)
		{
			const float size = 14f;
			bool levelLeft = uirSkillLevelLeft != null && uirCardSettings?.GetValue(null) is { } settings && (bool)uirSkillLevelLeft.GetValue(settings);
			float right = levelLeft ? 2f : 22f;
			Widgets.BeginGroup(new Rect(cell.xMax - right - size, Mathf.Round(cell.center.y - size / 2f), size, size));
			tdLearningArrow(skill, new Rect(0f, 0f, size, size), -70f);
			Widgets.EndGroup();
		}

		// ---- Level Up!: no skill announcements for pawns that are not in play ------------------------------------
		// Level Up announces any IsFreeColonist pawn whose skill level moves. At conception Better Gene Inheritance
		// generates a throwaway player-faction Colonist, AddGene()s the rolled hybrid genes onto it and destroys it, so its
		// gene aptitudes popped "Baby reached level 9 in Animals!" with the pregnancy letter. Skips both Level Up hooks (the third
		// argument of each is the pawn) unless the pawn is on a map (held or carried counts) or in a caravan.
		static bool LevelUpOnlyPresentPawns(Pawn __2) => __2 == null || __2.SpawnedOrAnyParentSpawned || __2.IsCaravanMember();

		// ---- Hediff message comps without message text ----------------------------------------------------------
		// HediffComp_MessageBase.Message posts Props.message unconditionally. [FSF] Advanced Bionics Expansion adds a
		// HediffCompProperties_RemoveIfOtherHediff without <message> to vanilla BrainShock (EMP shield implant), so an
		// EMP hit posts a null message; Block Unwanted Minutiae's ContainsMessage then throws on the null dictionary
		// key, every tick until the hediff is gone. Skips posting when the comp has no message text.
		static bool HediffMessageNeedsText(HediffComp __instance) =>
			__instance.props is not HediffCompProperties_MessageBase props || !props.message.NullOrEmpty();

		// ---- Kanban Stockpile Continue: remove-excess HasJobOnThing and JobOnThing out of sync -------------------
		// HasJobOnThing only asks "is there excess, and does any storage accept it"; JobOnThing also clamps the count to
		// the room left under the destination's own Kanban max stack size and gives up on an SSL excess that cannot move
		// whole. A capped vending machine or fridge already at its cap then yields a null job and vanilla logs "provided
		// target but yielded no actual job" (cheese, 2026-11-18). HasJobOnThing now also requires JobOnThing to build a
		// job. The probe has no lasting effect: Hauler's Dream's HaulToCellStorageJob postfix only clamps the count (it
		// commits at reservation time), and Kanban's OnExcessRemoved event is held back while probing.
		[ThreadStatic] static bool ksProbing;

		static void KsRemoveExcessHasRealJob(WorkGiver_Scanner __instance, Pawn pawn, Thing t, bool forced, ref bool __result)
		{
			if (!__result || ksProbing) return;
			ksProbing = true;
			try { __result = __instance.JobOnThing(pawn, t, forced) != null; }
			finally { ksProbing = false; }
		}

		static bool KsNoEventWhileProbing() => !ksProbing;

		// ---- VEF tesla chain fire (Alpha Armoury "tesla coil" trait) -------------------------------------------
		// TeslaChainingProps.addFire makes TeslaProjectile.Impact spawn a Fire and AttachTo every hit thing with a
		// CompAttachBase, skipping vanilla's ignition rules: mechanoids (Flammability 0) burn, and every chain hit adds
		// another fire to an already burning target (3 on one Siegebreaker). The trait itself promises to stun mechs and
		// burn organics. The AttachTo call now goes through vanilla's CanEverAttachFire / HasAttachment(Fire) checks; a
		// fire that may not attach is removed again right after VEF spawned it.
		static IEnumerable<CodeInstruction> TeslaFireVanillaRulesTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var attachTo = AccessTools.Method(typeof(AttachableThing), nameof(AttachableThing.AttachTo));
			int replaced = 0;
			foreach (var code in instructions)
			{
				if (code.Calls(attachTo))
				{
					code.opcode = OpCodes.Call;
					code.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(TeslaAttachFireIfAllowed));
					replaced++;
				}
				yield return code;
			}
			if (replaced != 1)
				Log.Error($"[Harmony3StrictCompat] VEF TeslaProjectile.Impact has {replaced} AttachTo calls (expected 1): its mod updated, redo the tesla fire fix");
		}

		static void TeslaAttachFireIfAllowed(AttachableThing fire, Thing target)
		{
			if (target.CanEverAttachFire() && !target.HasAttachment(ThingDefOf.Fire))
				fire.AttachTo(target);
			else if (!fire.Destroyed)
				fire.Destroy();
		}

		// ---- Monument quests: VE pipes count as conduits ---------------------------------------------------------
		// Personal tweak: the monument marker lets only power conduits share its footprint or sit next to it (placement:
		// "Cannot place monument over ...", "Monument adjacent to ...") and stay on its floor once built (otherwise the
		// monument is destroyed after a day). VE Framework pipes (helixien, astrofuel, uranium, ...) are the same kind of
		// non-edifice conduit-layer building, visible and hidden alike, so every isPowerConduit read in those two methods
		// also accepts a non-edifice PipeSystem.Building_Pipe.
		static Type vefPipeClass;
		static HashSet<BuildingProperties> vefPipeProps;

		static IEnumerable<CodeInstruction> MonumentPipesLikeConduitsTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
		{
			var conduitField = AccessTools.Field(typeof(BuildingProperties), nameof(BuildingProperties.isPowerConduit));
			int replaced = 0;
			foreach (var code in instructions)
			{
				if (code.LoadsField(conduitField))
				{
					code.opcode = OpCodes.Call;
					code.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(ConduitOrVefPipe));
					replaced++;
				}
				yield return code;
			}
			if (replaced == 0)
				Log.Error($"[Harmony3StrictCompat] {__originalMethod.DeclaringType.Name}.{__originalMethod.Name} no longer reads isPowerConduit: the game updated, redo the monument pipe tweak");
		}

		static bool ConduitOrVefPipe(BuildingProperties building)
		{
			if (building == null) return false;
			if (building.isPowerConduit) return true;
			vefPipeProps ??= DefDatabase<ThingDef>.AllDefs
				.Where(d => d.building != null && !d.building.isEdifice && d.thingClass != null && vefPipeClass.IsAssignableFrom(d.thingClass))
				.Select(d => d.building).ToHashSet();
			return vefPipeProps.Contains(building);
		}

		// ---- VQE Ancients: name the pneumatic capsule's contents in its arrival message ---------------------------
		// Personal tweak: Deliver sends the message before it builds the local list of things to place, and pawns haul
		// them away quickly. Drops that message and sends it after the list is built, just before the placing loop.
		static IEnumerable<CodeInstruction> VqeaCapsuleContentsTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var getEnumerator = AccessTools.Method(typeof(List<Thing>), nameof(List<Thing>.GetEnumerator));
			int message = codes.FindIndex(c => c.operand is MethodInfo { Name: nameof(Messages.Message) } m && m.DeclaringType == typeof(Messages));
			int loop = codes.FindIndex(c => c.Calls(getEnumerator));
			if (message < 0 || loop < 1 || !codes[loop - 1].IsLdloc())
			{
				Log.Error("[Harmony3StrictCompat] VQE Ancients Deliver no longer has the expected message or loop: its mod updated, remove or redo this tweak");
				return codes;
			}
			var inserted = new List<CodeInstruction>
			{
				new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[loop - 1]).MoveBlocksFrom(codes[loop - 1]),
				new CodeInstruction(codes[loop - 1].opcode, codes[loop - 1].operand),
				CodeInstruction.Call(typeof(Harmony3StrictCompatMod), nameof(VqeaCapsuleMessage)),
			};
			codes.InsertRange(loop - 1, inserted);
			var call = codes[message];
			var pops = ((MethodInfo)call.operand).GetParameters().Select(_ => new CodeInstruction(OpCodes.Pop)).ToList();
			pops[0].MoveLabelsFrom(call).MoveBlocksFrom(call);
			codes.RemoveAt(message);
			codes.InsertRange(message, pops);
			return codes;
		}

		static void VqeaCapsuleMessage(Building port, List<Thing> things)
		{
			const int maxKinds = 6;
			string text = "VQEA_PneumaticCapsuleArrived".Translate();
			var kinds = things.GroupBy(t => (t.def, t.Stuff))
				.Select(g => (g.Key.def, g.Key.Stuff, count: g.Sum(t => t.stackCount)))
				.OrderByDescending(k => k.count).ToList();
			if (kinds.Count > 0)
			{
				text = text.TrimEnd('.') + ": " + string.Join(", ", kinds.Take(maxKinds).Select(k => GenLabel.ThingLabel(k.def, k.Stuff, k.count)));
				if (kinds.Count > maxKinds)
					text += $" +{kinds.Count - maxKinds}";
			}
			Messages.Message(text, port, MessageTypeDefOf.PositiveEvent);
		}

		// ---- Range ring for turrets whose gun has no Verb_Shoot / Verb_Spray ----------------------------------
		// Vanilla PlaceWorker_ShowTurretRadius takes the first verb that is Verb_Shoot or a Verb_Spray and null-refs
		// every frame while placing when there is none, e.g. Verb_ShootBeam (Rimsenal Security incinerator and graser
		// turrets; LocalTweaks gives those the placeworker). Such guns draw the ring of their first ranged verb instead;
		// everything vanilla handles runs the original.
		static bool TurretRadiusAnyVerb(BuildableDef checkingDef, IntVec3 loc, ref AcceptanceReport __result)
		{
			var verbs = (checkingDef as ThingDef)?.building?.turretGunDef?.Verbs;
			if (verbs == null || verbs.Any(v => v.verbClass == typeof(Verb_Shoot) || typeof(Verb_Spray).IsAssignableFrom(v.verbClass)))
				return true;
			var verb = verbs.FirstOrDefault(v => v.range > 0f);
			if (verb != null)
			{
				GenDraw.DrawRadiusRing(loc, verb.range);
				if (verb.minRange > 0f)
					GenDraw.DrawRadiusRing(loc, verb.minRange);
			}
			__result = true;
			return false;
		}

		// ---- Alpha Books: ruined and burned books crumble after reading ----------------------------------------
		// Their defs ask for BookReadResult_DestroyBook, but Alpha Books 1.6+ runs read results only inside its own
		// ABooks_Reading job, which only its Book_NotAutoReadable classes start. Ruined and burned books are plain
		// Book, so colonists read them with vanilla JobDriver_Reading and drop or store them afterwards (the 1.5 DLL
		// had a postfix on JobDriver_Reading.ReadBook for this; 1.6 dropped it). A read of a book in Alpha Books'
		// ruinedBooks set that ends Succeeded (full duration, recreation or learning need full) now destroys it in the
		// driver's finalizer, before vanilla hands out the haul-to-storage job. Interrupted reads keep the book.
		// Skipped when Alpha Books ships its own ReadBook patch again.
		static FieldInfo abRuinedBooks;
		static AccessTools.FieldRef<JobDriver, Func<JobCondition, Job>> jobFinalizer;

		static void AbRuinedBookCrumbles(JobDriver_Reading __instance)
		{
			var book = __instance.Book;
			if (book == null || !(abRuinedBooks.GetValue(null) is HashSet<ThingDef> ruined) || !ruined.Contains(book.def))
				return;
			var vanilla = jobFinalizer(__instance);
			jobFinalizer(__instance) = condition =>
			{
				if (condition == JobCondition.Succeeded && __instance.IsReading && !book.Destroyed)
				{
					book.Destroy();
					return null;
				}
				return vanilla?.Invoke(condition);
			};
		}

		// Living Bonds' "read book together" (JobDriver_FamilyReading) is a third reading driver: the initiator carries
		// the book to a seat, reads for 2000 ticks and places it in the cell. A ruined book was left on the floor.
		// The job's B target is the book only for the initiator; a job that ends Succeeded has run the whole read.
		static void AbFamilyReadBookCrumbles(JobDriver __instance)
		{
			if (!(__instance.job.GetTarget(TargetIndex.B).Thing is Book book)
				|| !(abRuinedBooks.GetValue(null) is HashSet<ThingDef> ruined) || !ruined.Contains(book.def))
				return;
			__instance.AddFinishAction(condition =>
			{
				if (condition == JobCondition.Succeeded && !book.Destroyed)
					book.Destroy();
			});
		}

		// ---- Alpha Books: ruined and burned books can be ticked in reading policies -----------------------------
		// Alpha Books hides every ABooks_* def from the reading policy dialog (its postfix on PolicyGlobalFilter), which
		// is right for its single-use books: they are Book_NotAutoReadable and never read on their own. Ruined and
		// burned books are plain Book that colonists are meant to read for recreation, and vanilla only picks a book
		// its reader's policy allows (BookUtility.IsValidBook). A policy that lost those two defs ("Allow all" only
		// restores what the dialog shows; a save older than the mod never had them) could not get them back, so the
		// books sat on the shelves unread. The two defs are added back to the list the dialog shows.
		static void AbRuinedBooksInReadingPolicy(ThingFilter __result)
		{
			if (__result == null || !(abRuinedBooks.GetValue(null) is HashSet<ThingDef> ruined))
				return;
			foreach (var def in ruined)
				__result.SetAllow(def, true);
		}

		// ---- Alpha Books: ruined and burned books share the random pick with novels ------------------------------
		// BookUtility.TryGetRandomBookToRead picks at random among the valid books that "provide an outcome" for the
		// reader and only falls back to the others when there are none. A novel always provides one; the ruined books'
		// doer (ReadingOutcomeDoer_Text) never does, so one readable novel on the map kept every ruined book unread.
		// The picker's only ProvidesOutcome call is swapped for one that also counts a book in Alpha Books' ruinedBooks
		// set. Reading policy, quality range and forbidden checks still apply (IsValidBook runs first).
		static IEnumerable<CodeInstruction> AbRuinedBooksInRandomPoolTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var providesOutcome = AccessTools.Method(typeof(Book), nameof(Book.ProvidesOutcome));
			int replaced = 0;
			foreach (var code in instructions)
			{
				if (code.Calls(providesOutcome))
				{
					code.opcode = OpCodes.Call;
					code.operand = AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(ProvidesOutcomeOrRuined));
					replaced++;
				}
				yield return code;
			}
			if (replaced != 1)
				Log.Error($"[Harmony3StrictCompat] BookUtility.TryGetRandomBookToRead calls Book.ProvidesOutcome {replaced} times, expected 1: the game updated, redo the ruined book pick fix");
		}

		static bool ProvidesOutcomeOrRuined(Book book, Pawn reader)
		{
			return book.ProvidesOutcome(reader) || (abRuinedBooks.GetValue(null) is HashSet<ThingDef> ruined && ruined.Contains(book.def));
		}

		// ---- Vanilla: "Read now" is dropped at once on a forbidden book or outside recreation time ----------------
		// The pawn picks the book up, carries it to a seat, and the read toil ends the job within a few ticks, so the
		// book is dropped (seen 2026-11-15 on Alpha Books' ruined library site):
		// - the float menu does not check forbidden, and the toil's FailOnDestroyedNullOrForbidden fires (the library
		//   loot spawns forbidden). Vanilla's Equip and Wear orders unforbid their item; an ordered read does too.
		// - JoyUtility.JoyTickCheckEnd ends any reading job InterruptForced when the pawn's schedule slot disallows
		//   recreation (Work) unless job.ignoreJoyTimeAssignment, which "Read now" never sets. Vanilla sets it for
		//   other jobs it starts outside recreation time (meditating in bed, visiting the sick); an ordered read now too.
		// Postfix on Notify_Starting of vanilla JobDriver_Reading and Alpha Books' JobDriver_ReadAlphaBook.
		static void OrderedReadUnforbidsBook(JobDriver __instance)
		{
			if (!__instance.job.playerForced || __instance.job.targetA.Thing is not Book book)
				return;
			book.SetForbidden(false, warnOnFail: false);
			__instance.job.ignoreJoyTimeAssignment = true;
		}

		// Vanilla bug: CanGenerateFrom checks options against the whole point total, but ChoosePawnGenOptionsByPoints
		// caps each pawn at MaxPawnCost (the faction curve). A group whose every option costs more than that cap
		// passes the gate and yields no pawns, e.g. VFE Pirates' melee-only Junker Hussar group below ~5800 points:
		// the quest "raid of roughly this size:" preview comes out blank. Reject such groups so another one is rolled.
		// Vanilla (seen with RimPacts defend waves): PawnRelationWorker_Sibling.CreateRelation generates the missing
		// parents of the existing sibling and then reads other.GetMother().story; when no mother is found afterwards it
		// throws a NullReferenceException, PawnGenerator rethrows and the whole pawn group fails. RimPacts' defend
		// mission retried every few seconds (20 s freezes, the wave never came). Swallow that exception: the new pawn
		// just gets no sibling. Logs what the parents looked like, once per pair, to find the real cause.
		static Exception SiblingRelationGuard(Exception __exception, Pawn generated, Pawn other)
		{
			if (__exception is not NullReferenceException) return __exception;
			static string Who(Pawn p) => p == null ? "none"
				: $"{p.LabelShort} ({p.def?.defName}, {p.kindDef?.defName}, {p.genes?.Xenotype?.defName ?? "no xenotype"}, {p.gender}, story {(p.story == null ? "null" : "ok")}, {p.Faction?.Name ?? "no faction"})";
			Log.WarningOnce($"[Harmony3StrictCompat] vanilla sibling relation failed and was skipped: new pawn {Who(generated)}, existing sibling {Who(other)}, its mother {Who(other?.GetMother())}, its father {Who(other?.GetFather())}. {__exception.Message}",
				(generated?.thingIDNumber ?? 0) ^ (other?.thingIDNumber ?? 0) ^ 0x5B1B);
			return null;
		}

		static void NormalGroupYieldsPawns(PawnGroupMakerParms parms, PawnGroupMaker groupMaker, ref bool __result)
		{
			if (!__result || parms.faction == null || groupMaker.kindDef.Worker.GetType() != typeof(PawnGroupKindWorker_Normal))
				return;
			__result = PawnGroupMakerUtility.GetOptions(parms, parms.faction.def, groupMaker.options, parms.points, parms.points, null)
				.Any(o => PawnGroupMakerUtility.PawnGenOptionValid(o.Option, parms));
		}

		// ---- Skilled Refueling: normal and atomic refuel jobs share one cached iterator field ---------------------
		// GetRefuelDuration caches the "<>4__this" FieldInfo from whichever iterator runs first, but JobDriver_Refuel and
		// JobDriver_RefuelAtomic have separate iterator classes, so the other job's GetValue throws ArgumentException.
		// It also casts to JobDriver_Refuel only, so atomic refuels never get the skill speed. Refresh the cache for the
		// current iterator and let its code handle normal refuels. Atomic refuels get the skill speed without the
		// per-stack scaling: fuel targets are still in a queue when the toils are built, so no single stack exists yet.
		static FieldInfo srOwnerField;
		static Func<Pawn, float> srRefuelSpeed;

		static bool SrRefuelDuration(object instance, ref int __result)
		{
			var owner = AccessTools.Field(instance.GetType(), "<>4__this");
			if (owner?.GetValue(instance) is JobDriver_RefuelAtomic atomic)
			{
				float speed = srRefuelSpeed(atomic.pawn);
				__result = speed > 0f ? Mathf.CeilToInt(240f / speed) : 240;
				return false;
			}
			srOwnerField.SetValue(null, owner);
			return true;
		}

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

		// ---- Hospitality: guests with a claimed bed sleep on the floor after a save is loaded --------------------
		// Seen 2026-11-01, 11-04, 11-08 (Vache, Katie, Drikus, Apaturf, Courtni): every guest that was already a guest
		// in the game loaded before. Hospitality's CompUtility.CompGuest(pawn) caches the comp in a Dictionary<Pawn,
		// CompGuest> that nothing clears on load. In 1.7 Pawn implements IEquatable<Pawn> by thingIDNumber (+ defName)
		// and Thing.GetHashCode is thingIDNumber, so the reloaded pawn gets the previous game's comp: its bed and owner
		// are the discarded objects. HasBed is true on those (the old bed still reports Spawned), IsValidBedFor fails
		// on the old bed, and JobGiver_Sleep falls through to a floor LayDown without ever looking for the real bed.
		// The dictionary also keeps the whole previous game reachable through its pawn keys.
		static FieldInfo hospGuestComps;

		static void ClearHospitalityGuestComps()
		{
			if (hospGuestComps.GetValue(null) is not IDictionary cache || cache.Count == 0) return;
			Log.Message($"[Harmony3StrictCompat] Hospitality: dropped {cache.Count} cached guest comp(s) of the previous game");
			cache.Clear();
		}

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

		// ---- Positive Connections: Den room role null-refs on a one-sided region link ----------------------------
		// RoomRoleWorker_Den.CountDoors walks region links without the null checks vanilla uses: a link with only one
		// region registered gives GetOtherRegion / RegionA / RegionB == null. The exception leaves Room.role null
		// (UpdateRoomStatsAndRole clears its dirty flag first), and null passes Gizmo_RoomStats' "Role != None" test,
		// so the room stats gizmo then throws for every building in that room and the whole gizmo grid stops drawing
		// (VVE garage, 2026-11-15). Same count, null regions skipped.
		static bool DenCountDoorsNullSafe(Room room, ref int __result)
		{
			var seen = new HashSet<Region>();
			var doors = new HashSet<Building_Door>();
			foreach (var region in room.Regions)
				foreach (var link in region.links)
				{
					var other = link.GetOtherRegion(region);
					if (other == null || other.type != RegionType.Portal || !seen.Add(other)) continue;
					var door = other.door;
					foreach (var l in other.links)
						if (LeadsOut(l.RegionA) || LeadsOut(l.RegionB)) { doors.Add(door); break; }
					bool LeadsOut(Region r) => r != null && r.Room != room && r != other && r.door != door;
				}
			__result = doors.Count;
			return false;
		}

		// ---- Goji's Misc Genes: Parroting null-refs on an unspawned recipient -----------------------------------
		// Its InteractionWorker.Interacted postfix makes a recipient with the Parroting gene answer with the same
		// interaction: recipient.interactions.TryInteractWith(...). A despawned pawn has no interactions tracker
		// (PawnComponentsUtility.RemoveComponentsOnDespawned nulls it), and Living Bonds' BabyBabbleName interacts with
		// a baby while it is carried: NullReferenceException every ~40 s from LivingBonds_Manager.GameComponentTick
		// (2026-11-15). No tracker, no parroting; the original interaction is already done at this point.
		static bool GojiParrotNeedsTracker(Pawn recipient) => recipient?.interactions != null;

		// ---- Vanilla Social Interactions Expanded: teaching without a stored topic throws ------------------------
		// VSIE stores a teacher's topic (pupil + skill) in RandomSelectionWeight and InteractionWorker_Teaching.Interacted
		// reads it with teachersWithPupils[initiator]: KeyNotFoundException when the interaction was started any other
		// way. Goji's Misc Genes' Parroting gene does that: the pupil answers with the same interaction, so the pupil
		// "teaches" back with no topic and the exception aborts the first teacher's tick ("Exception ticking Gao ...
		// The given key 'Sarmiento' was not present in the dictionary", 2026-11-15; logged once per pawn, then silent).
		// Without a topic, pick one the way VSIE does; if the initiator has nothing to teach, end quietly.
		static MethodInfo vsieSkillToTeach, vsieManager;
		static FieldInfo vsiePupils;
		static ConstructorInfo vsieTopic;

		static bool VsieTeachNeedsTopic(object __instance, Pawn initiator, Pawn recipient, ref string letterText, ref string letterLabel,
			ref LetterDef letterDef, ref LookTargets lookTargets)
		{
			if (vsiePupils.GetValue(vsieManager.Invoke(null, null)) is not IDictionary topics || topics.Contains(initiator)) return true;
			if (initiator.skills != null && recipient?.skills != null && vsieSkillToTeach.Invoke(__instance, [initiator, recipient]) is SkillDef skill)
			{
				topics[initiator] = vsieTopic.Invoke([recipient, skill]);
				return true;
			}
			letterText = null;
			letterLabel = null;
			letterDef = null;
			lookTargets = null;
			return false;
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
			["LevelUp.Patcher:DirtyAptitudesPostfix"] = 0xE6A4CB86C9A72804,                                   // Level Up! 1.6 DLL, 2026-11-17 (no announcements for unspawned pawns)
			["LevelUp.Patcher:LearnPostfix"] = 0x425B4856BEAB93EA,
			["Verse.HediffComp_MessageBase:Message"] = 0x4DC3B7073B69E6C6,                                     // game 1.7.5011, 2026-11-18 (no post without message text)
			["StylishDialogs.CollageLayout:Decoration"] = 0xDAE1569EF4A2CD37,                              // Stylish Dialogs 1.7, 2026-11-21 (centre the panel)
			["StylishDialogs.DialogCollagePatch:Get"] = 0xD912B11A385DBBC4,
			["VFEPirates.JobDriver_DoWelding:CollectIngredientsToils"] = 0x98754CD024471CA6,               // VFE Pirates 1.7 + Shelf Stack Logistics, 2026-11-21 (welding uses the pickup point)
			["VFEPirates.JobDriver_DoWelding:CollectIngredientsToils/MoveNext"] = 0x8E5A0D6D7B3FDABF,
			["Shelf.HarmonyPatches:JumpIfTargetInisdeBillGiver"] = 0x4F41E4AC2D5CEB22,
			["RimWorld.PlaceWorker_MonumentMarker:AllowsPlacing"] = 0x615326571B614842,                        // game 1.7.5011, 2026-11-18 (VE pipes count as conduits)
			["RimWorld.MonumentMarker:get_FirstDisallowedBuilding"] = 0x56B58DC4E8C9010E,
			["Rachek128.RitualAttenuation.RitualExtendedDataRef:get_Comp"] = 0x357FBBDFAE580E43,               // Ritual Size Attenuation 1.6 DLL, 2026-11-16
			["RimPacts.WorldComponent_RimPacts:TryRevertConquered"] = 0x20D53DF606F689C2,                  // RimPacts 1.7, 2026-11-17 16:55 (no revolt to an absorbed faction)
			["RimPacts.WorldComponent_RimPacts:OriginalOwnerOf"] = 0x7A2ADEAC3FD7BA63,
			["RimPacts.WorldComponent_RimPacts:WorldComponentTick"] = 0xF6CF51711840F579,                  // RimPacts 1.7, 2026-11-17 (auto-defense wounds)
			["RimPacts.WorldComponent_RimPacts:DefAutoTick"] = 0xE551C9A77C4672D8,
			["RimPacts.WorldComponent_RimPacts:DefAutoOne"] = 0xC4DB7EBE746E875E,
			["RimPacts.WorldComponent_RimPacts:WardLordPawns"] = 0x7724DA3409C9A731,                    // RimPacts 1.7, DLL of 2026-11-17 16:55
			["RimPacts.HediffComp_RptDropOnPlayerOwned:CompPostTick"] = 0x15979899F58581E4,
			["RimPacts.WorldComponent_RimPacts:CaravanGuardStep"] = 0x9AF75C085C4C990A,
			["RimWorldWaylith.Imgui.ImGuiHost+Activation:.ctor"] = 0x84FE454465EF6075,                   // game 1.7.5014 RimWorld.dll
			["DressForTheWeather.PawnsArriveUtilities:DressPawnAppropriately"] = 0xF6A3084C72CDCFB6,   // Dress for the Weather 1.7, DLL of 2026-10-27
			["DressForTheWeather.PawnsArriveUtilities:InitializeApparelPairs"] = 0x642A784DF0292B51,
			["DressForTheWeather.IncidentWorker_NeutralGroup_SpawnPawns:Postfix"] = 0xB1048B0DC4E3B6FC,
			["Impact.ArmorDeflection:Prefix"] = 0x31A4B4B031D77B6E,                                         // IMPACT - Combat Overhaul 1.7, DLL of 2026-10-29
			["RimWorld.IncidentWorker_Raid:TryGenerateRaidInfo"] = 0x09B77A52A9B691B2,                          // game 1.7.5013, 2026-11-18 (pawns are spawned by the time it returns)
			["EventImagePopup.Patch_WindowStackAdd:Prefix"] = 0xE2868FDECD3D475D,                              // Event Image Popup, DLL of 2026-11-23 (only Dialog_NodeTree inside a letter or incident scope)
			["EventImagePopup.ImageResolver:ResolveForIncident"] = 0xB0C8AE510BAB01F1,
			["RimWorld.CompShuttle:CompTick"] = 0x807AD3141AD4D8A6,
			["RimWorld.CompRechargeable:CompTick"] = 0x60A381C5B22A28EF,                                     // game 1.7.5014, 2026-11-24 (recharge bar inside a one-tile-tall machine)
			["RimWorld.GravshipUtility:PreLaunchConfirmation"] = 0x5BCE4E3819181DF8,                          // game 1.7.5014, 2026-11-24 (launch warning when VGE keeps the map)
			["RimWorld.RitualOutcomeEffectWorker_GravshipLaunch:Apply"] = 0x0EF46C19BD6D545A,
			["VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:Prefix"] = 0x26BB4AA240F19EA4,              // VGE 1.7, DLL of 2026-10-20 17:20 (keeps the map when a colonist stays)
			["VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:CanEverKeepThisMap"] = 0x479CE351B185A114,
			["VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:ShouldAlwaysKeepThisMap"] = 0x8AD816C302A0AD78,
			["GravshipFleet.FleetLaunchHold:Hold"] = 0x9D06C470CD36959F,                                     // Gravship Fleet 1.7, DLL of 2026-10-25 21:37 (lets LeaveShip visitors walk off)
			["VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch+<>c__DisplayClass0_0:<Prefix>g__AbandonTile|1"] = 0xC2441200ED47772A, // VGE 1.7, DLL of 2026-10-20 17:20 (BAS gravship flag)
			["BetterAbandonedSettlements.Patches:AbandonMapPrefix"] = 0xE694E2BEC4B5959B,                   // BAS 1.1.1, DLL of 2026-10-31 12:43
			["BetterAbandonedSettlements.Patches:AbandonMapFinalizer"] = 0x80BD445544A465A2,
			["VanillaQuestsExpandedTheGenerator.CompAbilityCraftAnARCComponent:Apply"] = 0xA99D1505A77AE8CC, // VQE The Generator 1.6 DLL of 2026-07-08 (finds the component in the cell)
			["RimWorld.Selector:SelectUnderMouse"] = 0x59EF35E3AE5B2B41,                                    // game 1.7.5013, 2026-11-18
			["RimWorld.Selector+<>c:<SelectUnderMouse>b__45_0"] = 0x98B740D7A8C6EA63,
			["Verse.GizmoGridDrawer:DrawGizmoGrid"] = 0x01194CA4A1ED54CD,
			["RimWorld.PawnRelationWorker_Sibling:CreateRelation"] = 0xCC2ADCE90DEE9FA6,                                              // game 1.7.5011, 2026-11-16
			["TraitTracker.TraitHediffApplier:ApplyHediffsToPawnsWithTraits"] = 0x67BDC9BB976E9B72,                // 2026-10-22
			// VEF 1.7 MVCF.dll, 2026-10-31
			["MVCF.Utilities.VerbStatsUtility:GetStatValue"] = 0x691C1FF55EB56CE6,
			["MVCF.Utilities.VerbStatsUtility:CreateAverageStat"] = 0x00F6026D0950F156,                           // StatDef overload
			["MVCF.Utilities.VerbStatsUtility:DisplayStatsForVerbs"] = 0x20DD3E5059E47E19,
			["MVCF.Utilities.VerbStatsUtility:DisplayStatsForVerbs/MoveNext"] = 0xE4C1A1BF7F4C9435,
			["VEF.Weapons.VerbUtility+VerbProperties_AdjustedRange_Patch:Prefix"] = 0xCD15538559B0F4FE,       // VEF 1.7, 2026-11-19 (NaN __state sentinel; still mutates shared range)
			["VEF.Weapons.VerbUtility+VerbProperties_AdjustedRange_Patch:Finalizer"] = 0xB2FA8EBB18C0127A,
			["TriumphantResearch.Window_ResearchComplete:.ctor"] = 0x55438039A644309A,                            // 1.6 build, 2026-11-01
			["GiddyUp.IsMountableUtility:IsMountable"] = 0x9158721D61502B63,                                   // Giddy-Up 2 Continued 1.7, 2026-11-18 (no forbidden / allowed-area test on the animal)
			["GiddyUp.MountUtility:Dismount"] = 0x4D071C5677D8DCC2,                                                // Giddy-Up 2 Continued 1.7, 2026-11-09 (Dismount still parks lord animals)
			["PositiveConnectionsNmSpc.RoomRoleWorker_Den:CountDoors"] = 0x3CD5612235092B15,                                    // Positive Connections 1.7, 2026-11-15
			["GojisMiscGenes.InteractionWorker_Interacted_Patch:Postfix"] = 0x0DC1938A4ABAED83,                       // Goji's Misc Genes 1.7, 2026-11-15
			["Hospitality.Utilities.SpawnGroupUtility:GenerateNewGearFor"] = 0xC7A47125AA159971,                  // Hospitality 1.7, DLL of 2026-11-20 21:09 (rebuild, body unchanged)
			["Hospitality.Utilities.ItemUtility:PocketHeadgear"] = 0xEC7B03B8570A7845,                       // Hospitality 1.7, DLL of 2026-11-20 21:09 (rebuild, body unchanged)
			["HospitalityCasino.JobGiver_PlaySlotMachines:TryGiveJob"] = 0x886CDC4608E3C02D,                 // Casino 1.7, 2026-11-05
			["HospitalityCasino.VendingMachineJobHelper:InsertCoin"] = 0xDD3ACDA377763174,                   // Casino 1.7, 2026-11-06
			["HospitalityCasino.CompVendingMachine:CompInspectStringExtra"] = 0x4C16897C23044EC3,            // Casino 1.7, 2026-11-06
			["MSSFP.Utils.PawnGraphicUtils+<>c__DisplayClass4_0:<SaveTextureToFile>b__0"] = 0x1918143CABB241A5,  // MSS Tweaks and Fun 1.7, 2026-11-15
			["Caveworld_Flora_Unleashed.FruitingBody:get_LabelMouseover"] = 0xB098C8B9BC3CE626,                 // Biomes! Caverns 1.7, 2026-10-20
			["Caveworld_Flora_Unleashed.GenCaveFungusReproduction:TryGrowMycelium"] = 0x614612774F447421,         // Biomes! Caverns 1.7, 2026-10-20
			["ilyvion.LoadingProgress.StartupImpact.ModInfoList:GetModInfoFor"] = 0x357F68C7E142FD4F,          // Loading Progress 1.7, 2026-10-21
			["Inventory.BWM_DoWindowContentsPatch_Patch:Transpiler"] = 0x1208A46933A47D41,                     // Loadout Compositing 1.7, 2026-10-28
			["Inventory.BWM_DoWindowContentsPatch_Patch:ShouldCountAway"] = 0xA22693055B6314E7,                // Loadout Compositing 1.7, 2026-10-28
			["ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawFilters"] = 0x9EC9048AEF0F485C,        // BWM 1.7 Waylith (Debug build), 2026-11-15
			["ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawWorkTableNavigation"] = 0xCD24E921595CA15E,
			["VanillaRacesExpandedSanguophage.JobGiver_GetCorpseHemogen:TryGiveJob"] = 0x00285EA300E503C6,   // VRE Sanguophage 1.6 build, 2026-10-01
			["ModSettingsFramework.PatchOperationSliderFloat:ApplyWorker"] = 0x7D3F07C2C28B20A5,              // VEF 1.7 0ModSettingsFramework.dll, 2026-10-13
			["ModSettingsFramework.PatchOperationSliderInt:ApplyWorker"] = 0x03DFA38BA62A830B,
			["Busywork.Patch_ColonistBarColonistDrawer_DrawColonist:Postfix"] = 0x8671BDCBF4F254FC,          // Busywork 1.7, 2026-11-05
			["TD_Enhancement_Pack.Alert_Deterioration:get_DeterioratingThings"] = 0x8774F1B773D667FE,       // TD Enhancement Pack 1.7, 2026-11-12
			["TD_Enhancement_Pack.Alert_Deterioration+<get_DeterioratingThings>d__1:MoveNext"] = 0x54FBB3FD87CA2948,
			["TD_Enhancement_Pack.SkillLearningIndicator:LabelLearning"] = 0xA1C6010BAEEDD973,             // TD Enhancement Pack 1.7, DLL of 2026-10-22
			["UIRethemed.ColonistCard.Glance:DrawSkill"] = 0x9C53FA4DFE3A0EC5,                             // UI Rethemed - Colonist Card 1.7, DLL of 2026-11-19 17:22
			["VanillaQuestsExpandedAncients.Building_PneumaticTubeLaunchPort:Deliver"] = 0x824DD22872D97BE7, // VQE Ancients 1.6 build, DLL of 2026-10-06
			["SkilledRefueling.HarmonyPatches:GetRefuelDuration"] = 0xFE59863C2B492B65,                    // Skilled Refueling 1.7, DLL of 2026-11-09
			["SkilledRefueling.RefuelingUtility:RefuelingSpeedForPawn"] = 0x302D7899D7E9DF65,
			["TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:DrawColonist"] = 0x092B4F83C851CE01,     // LTO Colony Groups 1.6 build, 2026-11-05
			["ModernNeedsTab.Patch_Pawn_JobTracker_EndCurrentJob:Prefix"] = 0xDD06E10633A7F9FC,             // Modern Needs Tab, 2026-11-05
			["CommonSense.ThingMaker_MakeThing_CommonSensePatch:Postfix"] = 0xE351865A0EF38D39,             // Common Sense 1.6 build, 2026-11-05
			["CommonSense.Utility+<>c__DisplayClass21_0:<CleanFilthToil>b__0"] = 0x3736DE64D224E604,
			["CommonSense.Utility+<>c__DisplayClass21_0:<CleanFilthToil>b__1"] = 0x61D455F6E62CAD49,
			["CommonSense.OpportunisticTasks:Cleaning_Opportunity"] = 0xC83F6DDEA821201F,
			["HaulersDream.CompositableLoadoutsCompat:InitKeepApi"] = 0x3F15140E9ED2D991,                    // Hauler's Dream 1.6 build, 2026-11-05
			["HaulersDream.CompositableLoadoutsCompat:LoadoutWantedUnits"] = 0x89C4B3F40C058F43,
			["HaulersDream.InventoryShare:AddSharableStacksForBill"] = 0x787053FD33B7A5CC,                  // Hauler's Dream 1.6 build, 2026-11-18 (shares other pawns' inventory stacks that only its batch job can take)
			["HaulersDream.Patch_WorkGiver_DoBill_BatchRoute:Postfix"] = 0x68D751AC01408D90,
			["HaulersDream.HaulersDreamGameComponent:IsBatchBill"] = 0xE9C4F6DDB9AA9D92,
			["HaulersDream.CraftBatchPlanner:CanPawnBatch"] = 0xDAC66B7918CEFF27,
			["HaulersDream.StorageCommitments:IsDelivering"] = 0xB2A8CC7128220022,                           // Hauler's Dream 1.24.0 (1.6 build), 2026-08-10
			["HaulersDream.Alert_CannotUnloadInventory:Evaluate"] = 0x976491A9A63822D1,                       // Hauler's Dream 1.6 build, 2026-11-18
			["UIRethemed.Quests.Theme:LabelTruncated"] = 0x100BC02BEE333A35,                               // UI Rethemed - Quests 1.7, 2026-11-15 11:06
			["UIRethemed.Quests.HoverCard:Region"] = 0xC63A1FE873265DBA,
			["UIRethemed.Quests.RewardTiles:Of"] = 0xB224BEE3491FB587,
			["UIRethemed.Quests.HoverCard:ContentRegion"] = 0x13E206A5930D6140,
			["UIRethemed.Quests.HoverCard:Register"] = 0x9CD716D3C79F0120,
			["HaulersDream.InventorySurplus:KeepCountOf"] = 0x04CE467AFB2AB712,                                 // Hauler's Dream 1.6 build, 2026-11-11
			["SmartMedicine.StockUpUtility:StockUpCount"] = 0x6CFF64A881BBB050,                                   // (Pawn, ThingDef) overload, Smart Medicine 1.7, 2026-11-11
			["HaulersDream.StorageCommitments:get_InsideSpaceScan"] = 0x212C16560ED8173E,                     // Hauler's Dream 1.6 build, 2026-11-06
			["HaulersDream.JobDriver_UnloadHauledInventory:FindTargetOrDrop"] = 0x124B6AF8B6F91AFF,
			["HaulersDream.JobDriver_UnloadHauledInventory+<>c__DisplayClass15_0:<FindTargetOrDrop>b__0"] = 0xF8A1275757B89E49,
			["KanbanStockpileContinue.KanbanStockpileAPI:TryGetSettings"] = 0xFB4378E38CFA29D3,               // Kanban Stockpile Continue 1.6 build, 2025-08-12
			["KanbanStockpileContinue.KSUtil:CountStoredSimilarStacks"] = 0x0D6509A3EE0A8A5F,
			["KanbanStockpileContinue.KSUtil:IsSimilarStack"] = 0xA9EA56B4765CDEAF,
			["KanbanStockpileContinue.KSUtil:BuildSslExcessSet"] = 0x3D125057D3A69CBC,
			["KanbanStockpileContinue.KSUtil:CanStackIntoExistingAt"] = 0xF87B869A158931DE,                   // Kanban Stockpile Continue, 2026-11-22 (no top-up while over the similar stack limit)
			["KanbanStockpileContinue.StoreUtility_NoStorageBlockersIn_Patch:Postfix"] = 0x7FD096833D7C8C63,
			["KanbanStockpileContinue.KSUtil:TryGetStackRefillThresholdDesired"] = 0x69314DD6A97896DF,        // Kanban Stockpile Continue, 2026-11-22 (top-up beside a stack at its stack size)
			["KanbanStockpileContinue.ThingUtility_TryAbsorbStackNumToTake_Patch:Prefix"] = 0xF7BD60C3EE8443FA,
			["KanbanStockpileContinue.WorkGiver_RemoveExcessFromKanban:HasJobOnThing"] = 0x2F571A215357C57E,   // 2026-11-18 (remove-excess checks in sync)
			["KanbanStockpileContinue.WorkGiver_RemoveExcessFromKanban:JobOnThing"] = 0x6513C113A9741404,
			["KanbanStockpileContinue.KanbanStockpileAPI:InvokeExcessRemoved"] = 0xDEEB48913A6ABFF4,
			["KanbanStockpileContinue.StorageSettings_CopyFrom_Patch:CopyFrom"] = 0x03124597F110B0CD,         // 2026-11-22 (ownerless storage settings no longer share one Kanban slot)
			["KanbanStockpileContinue.StorageSettings_ExposeData_Patch:Postfix"] = 0x1C02896D5C8FB951,
			["UIRethemed.Ideology.StyleTip+Content:get_Width"] = 0x5F8988FF18236D28,                          // UI Rethemed - Ideology 1.7, 2026-11-11 11:53 (rest: DLL of 2026-11-23 11:15)
			["UIRethemed.Ideology.StyleTip+Content:Height"] = 0xE355FF3E32750D41,
			["UIRethemed.Ideology.StyleTip+Content:CellWidth"] = 0x43C267056C65FF5A,
			["UIRethemed.Ideology.StyleTip+Content:SectionHeight"] = 0x3DD868E15EC87627,
			["UIRethemed.Ideology.StyleTip+Content:Draw"] = 0x0D3A57AEE905D2F2,
			["TooManyMods.Compat_MerchantGuild_TradeArrivalOption:.cctor"] = 0x8DD5B3B38F9ACDDF,              // TMM VFEM2_VF.dll, 2026-01-24 (built for Vehicle Framework 1.6)
			["RimPacts.WorldComponent_RimPacts:ApplyProtectionFailure"] = 0xB370425BA8483DE8,                  // RimPacts 1.7, 2026-11-17 (no route check)
			["RimPacts.WorldComponent_RimPacts:IsProtectedByTradePact"] = 0x786746778135B518,
			["RimPacts.WorldComponent_RimPacts:ForceExpoParty"] = 0xF258884A1E73FC6E,                          // RimPacts 1.7, 2026-11-17
			["SensibleBedOwnership.Patch_Pawn_Ownership_get_OwnedBed:Prefix"] = 0x696E7432D36527D8,       // SBO 1.6 build, 2026-11-05
			["SensibleBedOwnership.Utility:GetMainBed"] = 0xAE5863A6DF81EAD6,                                // (Pawn) overload
			["SensibleBedOwnership.Utility+<>c:<GetMainBed>b__16_0"] = 0xAF4D8C424B841E39,                   // its lambdas: b.Map.IsPlayerHome, b.Map.uniqueID x2
			["SensibleBedOwnership.Utility+<>c:<GetMainBed>b__16_1"] = 0x1D302CAA9DBAE5A2,
			["SensibleBedOwnership.Utility+<>c:<GetMainBed>b__16_2"] = 0x1D302CAA9DBAE5A2,
			["SensibleBedOwnership.Utility:AllAssignedBedsAndDeathrestCaskets"] = 0x8560CE17C9797D18,
			["VEF.Buildings.CompRandomBuildingGraphic:ChangeGraphic"] = 0xDF24C7D643DD9A27,                  // VEF 1.7, 2026-11-16
			["VEF.Weapons.TeslaProjectile:Impact"] = 0x83997EC72461477B,                                       // VEF 1.7, 2026-11-18 (tesla fire follows vanilla ignition rules)
			["AdaptiveStorage.StorageGraphicWorker:GetGraphicFor"] = 0x2899906440FC5118,                     // ASF 1.6 build, 2025-11-12
			["RimPacts.WorldComponent_RimPacts:DismissExilePawn"] = 0xDA6EB5C1167E2B11,                        // RimPacts 1.7, 2026-11-17 (ForcefullyKept/Spawned guards; still no successor)
			["PrisonerRealism.JobGiver_CleanCell:ShouldClean"] = 0x7B21CD085E9E96A1,                          // Prisoner Realism 1.7, 2026-11-12 23:37
			["CleaningAreaRelaunched.CleaningAreaRelaunched_Util:GetCleanArea"] = 0xD9B02D52DE61B82C,          // CAR 1.6 build, 2025-11-12
			["aRandomKiwi.HFM.JobGiver_AnimalHunt:TryGiveJob"] = 0x865CBA567579063A,                            // Hunt For Me 1.6 build, 2025-12-25
			["Merthsoft.DesignatorShapes.Shapes.FloodFill:IsStructure"] = 0x01F3F2D215BA16DB,                 // Designator Shapes 1.7, 2026-11-02
			["VanillaSocialInteractionsExpanded.Thought_CuredMyFriend:get_LabelCap"] = 0xF477C9F6906F455E,   // VSIE 1.7, 2026-11-06
			["VanillaSocialInteractionsExpanded.VSIE_Utils:HaveNoticedTale"] = 0x006E2ED82BADE12D,
			["VanillaSocialInteractionsExpanded.InteractionWorker_Teaching:Interacted"] = 0x44695C314070FC12,
			["YayoAnimation.HarmonyPatches.PawnPatches.ParallelGetPreRenderResultsPatch:DataFor"] = 0x70EEFE43592C82C3, // Flourish Everyday 1.7, 2026-10-25
			["Flourish.Everyday.JobAnimationWorker_Anchored:Evaluate"] = 0x817B489F5395FF01,                // Flourish Everyday 1.7, 2026-10-25
			["VFETribals.FactionUIUtility_DoWindowContents_Patch:Postfix"] = 0x8824F2B542A8AC6E,             // VFE Tribals 1.6 build, 2026-10-14
			["UIRethemed.Factions.FactionsTab:Themed"] = 0x2C28E9ECF64A340C,                               // UI Rethemed - Factions 1.7, DLL of 2026-11-23 10:30
			["UIRethemed.Core.CoreWidgets:Button"] = 0x8D2CC30DE866319B,                             // UI Rethemed - Core 1.7, DLL of 2026-11-23 10:30
			["UIRethemed.Core.CoreWidgets:Icon"] = 0x3284D417CE1B0964,
			["UIRethemed.Core.CoreWidgets:ButtonWidth"] = 0x2808B04166C1E5E1,
			["UIRethemed.Factions.ModCompat:get_HeaderButtonsWidth"] = 0x89EA6B5602E230B4,
			["UIRethemed.Core.HoverCard:LineHeight"] = 0x401B4C41E687FB0C,                        // UI Rethemed - Core 1.7, DLL of 2026-11-23 10:30 (moved from Inspector Tabs 2026-11-19)
			["UIRethemed.Core.HoverCard:DrawGroup"] = 0xA9681363C2E9EC96,
			["UIRethemed.Core.HoverCard:GroupBodyWidth"] = 0x933F092AD59F80F3,
			["UIRethemed.Core.Theme:StatLine"] = 0x1D1F11E79224ED5F,
			["UIRethemed.Core.Theme:HoverGlow"] = 0x8F6BAA595061D62C,
			["UIRethemed.InspectorTabs.GearTab:RefreshVacuumGauge"] = 0xEF12B322E43F9A10,                // UI Rethemed - Inspector Tabs 1.7, DLL of 2026-11-21 04:39
			["UIRethemed.InspectorTabs.GearTab:SlotLabel"] = 0xA9E4AB8D19BE3AC1,
			["UIRethemed.InspectorTabs.GearTab:BuildThingCard"] = 0x722260ADE510671B,                 // gear hover card subtitle, 2026-11-16
			["UIRethemed.InspectorTabs.GearTab:VanillaSlotFor"] = 0xFAB75D1773308F03,
			["UIRethemed.InspectorTabs.GearTab:DrawStatsPanel"] = 0x4E3BF926F976B02C,
			["UIRethemed.InspectorTabs.GearCompareWindow:Contents"] = 0xD39A6E8FA5A917F4,             // compare side colours, 2026-11-20
			["UIRethemed.InspectorTabs.GearCompareWindow:Row"] = 0xEF4F3B66700A114A,
			["SmartMedicine.Dialog_StockUp:<DoWindowContents>b__10_0"] = 0x287F2659618CDD5B,        // Smart Medicine - Continued 1.7, 2026-11-08 (DLL of 2026-11-02)
			["Verse.AI.HaulAIUtility:PawnCanAutomaticallyHaul"] = 0x75A234FF82E00239,                            // game 1.7.5011, 2026-11-16
			["RimWorld.JobGiver_DropUnusedInventory:Drop"] = 0x80ADDA655AAFAE64,                                  // game 1.7.5000, 2026-11-09
			["RimWorld.PawnGroupKindWorker_Normal:CanGenerateFrom"] = 0xDD823B1286E54BB2,                       // game 1.7.5011, 2026-11-16
			["RimWorld.PlaceWorker_ShowTurretRadius:AllowsPlacing"] = 0x9F7C0CE51CFDA9F8,                       // game 1.7.5013, 2026-11-18
			["RimWorld.JobDriver_Reading:ReadBook"] = 0x51FC04EE1528676B,                                         // game 1.7.5011, 2026-11-16
			["LivingBonds.JobDriver_FamilyReading:MakeNewToils"] = 0xE5AB03BF7DC271E0,                           // Living Bonds 1.7
			["LivingBonds.JobDriver_FamilyReading:MakeNewToils/MoveNext"] = 0xEDCA9D7D5DC3432B,                 // Living Bonds 1.7
			["RimWorld.JobDriver_Reading:Notify_Starting"] = 0x22429B5AB43DB060,                                  // game 1.7.5011, 2026-11-16
			["AlphaBooks.JobDriver_ReadAlphaBook:Notify_Starting"] = 0x8C0A0B7B7171326B,                          // Alpha Books 1.6 DLL of 2026-07-22
			["AlphaBooks.AlphaBooks_Dialog_ManageReadingPolicies_PolicyGlobalFilter_Patch:ModifyPolicyFilter"] = 0xE16A4D675CCAD785, // Alpha Books 1.6 DLL of 2026-07-22
			["RimWorld.Dialog_ManageReadingPolicies:get_PolicyGlobalFilter"] = 0x5BC2C5FC5225313D,                // game 1.7.5014, 2026-11-23
			["AlphaBooks.ReadingOutcomeDoer_Text:DoesProvidesOutcome"] = 0x246DB32A470C6230,                      // Alpha Books 1.6 DLL of 2026-07-22
			["Verse.BookUtility:TryGetRandomBookToRead"] = 0x208E2B58F339F9BE,                                    // game 1.7.5014, 2026-11-23
			["SmartMedicine.FindBestMedicine:Find"] = 0x5C207A8C7B0E5F6A,                                        // Smart Medicine - Continued 1.7, 2026-11-12
			["SmartMedicine.StockUpUtility:StockingUpOn"] = 0xE47440ED851F08C2,                                   // (Pawn, Thing) overload, Smart Medicine 1.7, 2026-11-09
			["Unburned.Pretty_Patch:Prefix"] = 0xE153E3D01BE0AC50,                                                // Unburned 1.7, 2026-11-09
			["VEF.Weapons.MeleeReachCombatUtility:IsVanillaMeleeAttack"] = 0xBA5177C916BA1BBD,                    // VEF 1.7, 2026-11-16
			["SmarterVisitors.Trigger_TicksPassed_ActivateOn:Prefix"] = 0xEA4FA1B085907595,     // Smarter Visitors 1.7.0, 2026-11-08
			["SmarterVisitors.SmarterVisitors:SetDelayValue"] = 0x08060C0803A87391,                 // Smarter Visitors 1.7.0 (visitor questions as letters)
			["Hospitality.IncidentWorker_VisitorGroup:AskForSafety"] = 0x892BC7E79158842B,            // Hospitality 1.7, DLL of 2026-11-20 21:09 (visitor questions as letters)
			["Hospitality.IncidentWorker_VisitorGroup:SpawnGroup"] = 0x7BE71034FF731D80,
			["Hospitality.IncidentWorker_VisitorGroup:MaybeRevisit"] = 0x23996E30E276751D,
			["Hospitality.IncidentWorker_VisitorGroup:GetSpawnDirection"] = 0x8A0B1B4B83F5FB7D,
			["Hospitality.Hospitality_MapComponent:RefuseGuestsUntilWeHaveBeds"] = 0x261DB3C7B0ACC899,
			["StylishDialogs.CollageSettings:OpensOnArrival"] = 0x058896B2F5255633,                  // Stylish Dialogs 1.7, DLL of 2026-11-19 03:43 (Notice category gates visitor question letters)
			["UIRethemed.MainBar.MenuWindow:Face"] = 0x7810B929223847DC,                               // UI Rethemed - Main Bar 1.7, 2026-11-19 14:17
			["UIRethemed.MainBar.BarControls:ReadoutFace"] = 0x128CF933C95699CA,
			["UIRethemed.MainBar.BarControls:Toggle"] = 0x8B359CF8796161A3,
			["UIRethemed.MainBar.BarControls:SetSpeed"] = 0x71E7452A5F9EBDD4,
			["AdaptiveMealBill.AdaptiveRecipeSelection:RecoverRecipe"] = 0x62FA21FEB38C0B91,             // AdaptiveMealBill 1.7, DLL of 2026-11-08
			["UIRethemed.InspectorTabs.CareSwitchDef:For"] = 0x5545AB5AC9F1FA75,                         // UI Rethemed - Inspector Tabs 1.7, DLL of 2026-11-21 04:39
			["AutoExtractGenes.Utils:GetAutoExtractGenesComponent"] = 0x41DC32D5B21682AC,                // AutoExtractGenes 1.7, DLL of 2026-11-08
			["ColonyManagerRedux.Manager:FinalizeInit"] = 0xF523FD8F4AA6A9E5,                           // Colony Manager Redux 1.7, DLL of 2026-11-16 10:44
			["VFEF.CompScaresAnimals:ScareAnimals"] = 0xA4407F9189E92650,                               // VFE Farming (1.6 DLL), 2026-11-13
			["VanillaFurnitureExpandedFactory.Building_Conveyor:DetermineGraphic"] = 0x2F791A57EA0AD116, // VFE Factory 1.7, DLL of 2026-11-18 17:23
			["VanillaFurnitureExpandedFactory.Building_Conveyor:TryDumpItem"] = 0x471514D91FC8D405,      // VFE Factory 1.7, DLL of 2026-11-18 17:23
			["RimWorld.ColonistBarColonistDrawer:DrawGroupFrame"] = 0x2E4FDEB5E441EDD1,                           // game 1.7.5014, 2026-11-21 (no group frame)
			["TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:DrawGroupFrame"] = 0x96ECDD94A5927A8F,      // LTO Colony Groups 1.6 build, 2026-11-21 (no group frame)
			["VanillaFurnitureExpandedFactory.Building_Conveyor:SelectNextOutput"] = 0x7647CECB91A642A7, // VFE Factory 1.7, DLL of 2026-11-18 17:23
			["VanillaFurnitureExpandedFactory.Building_Conveyor:CanTransferToStorage"] = 0x1FC90CACFD97663A, // VFE Factory 1.7, DLL of 2026-11-18 17:23 (Kanban max stack)
			["VanillaFurnitureExpandedFactory.Building_Conveyor:GetDumpCapability"] = 0xC18ADDF4A0F62508,
			["VanillaFurnitureExpandedFactory.Building_Conveyor:CompleteTransfer"] = 0x801B26B36B1A32BF,
			["VanillaFurnitureExpandedFactory.Building_Conveyor:TryAdd"] = 0x496F782ACCE0E38D,
			["VanillaFurnitureExpandedFactory.Building_Conveyor:CanAcceptItem"] = 0x5A27ECF8B10E3800,
			["VanillaFurnitureExpandedFactory.Building_Conveyor:TransferItemTo"] = 0x9021F8A2586BAA37,
			["VanillaFurnitureExpandedFactory.Building_Conveyor:CanTransferToTarget"] = 0xCCEABA6758BC961D,
			["VanillaFurnitureExpandedFactory.Building_Conveyor:CanStackWithAny"] = 0xBE40C866C491B523,
			["EPrimeReadouts.GameCounts:AccumulateMap"] = 0x63E37E8779538722,                           // EPrime's Readouts 1.7 DLLs, 2026-11-21 (belt stock)
			["EPrimeReadouts.Core.CountAccumulator:AddSearch"] = 0xEEAD82F6179AE79C,
			["Flourish.AnimEventWorkers:HasMuzzle"] = 0x09D80ABDB9276A61,                               // Flourish - Combat Animations 2.1.5
			["TacticalGroups.ColonistGroup:GetUniqueLoadID"] = 0x5F30AAACFB7C28AE,                      // Colony Groups 1.6 build
			["SmartDeconstruct.SmartDeconstructMod:getDesignation"] = 0x53C1CCCAD3316AC2,               // Smart Deconstruct 1.7
			["UIRethemed.Notifications.StatusRow:Draw"] = 0x359EFD8A8A62F930,                               // UI Rethemed - Notifications 1.7, 2026-11-17 12:21 (hover card and motion now in Core)
			["UIRethemed.Notifications.StatusRow:WidthFor"] = 0x1D202367370CFEFE,
			["UIRethemed.Notifications.NotificationStyles:Resolve"] = 0x37E4D5B9BC27B3D0,
			["UIRethemed.Notifications.Placement:ColumnX"] = 0xE160D086AD81C948,
			["UIRethemed.Notifications.Placement:get_Position"] = 0x773B4416EAD9CD6E,
			["UIRethemed.Core.ThemeMotion:Hover"] = 0xF1F16615426658AB,
			["UIRethemed.Notifications.Patch_Messages_MessagesDoGUI:Transpiler"] = 0x6975299B4CC464C6,
			["UIRethemed.Core.HoverCard:Region"] = 0xB70A9435DD72C664,
			["UIRethemed.Core.HoverCard:DrawPlaced"] = 0x721845F6E527F3F1,
			["UIRethemed.Core.HoverCard:PlacedSize"] = 0x177E07DB1E271A28,
			["UIRethemed.Notifications.Placement:BesideX"] = 0x47679F286AED5F91,
			["UIRethemed.Notifications.SectionHeading:Shows"] = 0xFD51782B2B3A5A4A,
			["UIRethemed.Notifications.SectionHeading:Folded"] = 0xAD16F82687E32083,
			["UIRethemed.Notifications.SectionHeading:Draw"] = 0x19A24F4943EB8021,
			["UIRethemed.Notifications.SectionHeading:WidthFor"] = 0x6E6E862345D32A2B,
			["UIRethemed.Notifications.SectionHeading:NameOf"] = 0xB218B69E22340CA3,
			["UIRethemed.Notifications.SectionHeading:IconOf"] = 0xCB908098D911CD2F,
			["UIRethemed.Notifications.HudColumn:get_Shared"] = 0xB58DF5E7BBA9245A,
			["UIRethemed.Notifications.SectionHeading:Unseen"] = 0xCC48E9C9A0AC495F,
			["UIRethemed.Notifications.SectionHeading:See"] = 0xCBD89DB3833FC309,
			["UIRethemed.Notifications.SectionHeading:get_WidestName"] = 0x4BAA03BAA0E9A46E,
			["UIRethemed.Notifications.Placement:TopStart"] = 0x57988FD227F60263,
			["UIRethemed.Notifications.FoodReadout:Read"] = 0x4AF3C373D4873712,                          // UIR Notifications 1.7, 2026-11-17
			["UIRethemed.Notifications.FoodReadout:Draw"] = 0xACA4A6451E388627,
			["UIRethemed.ColonistCard.InfoList:Defer"] = 0xDF989BEF57F21E79,                            // UIR Colonist Card 1.7, 2026-11-19 17:22
			["UIRethemed.ColonistCard.InfoList:DrawRow"] = 0x2CC9C2A346FE3B32,                          // UIR Colonist Card 1.7, 2026-11-19 17:22
			["AutoCastSpecialistCommands.AutoCastToilInjector:Inject"] = 0x68EDEC90F35FF797,             // Auto-Cast Specialist Commands 1.7, 2026-10-12
			["AutoCastSpecialistCommands.Patch_JobDriver_Mine_MakeNewToils:Postfix"] = 0x9FAC641C9EE82C39,
			["AutoCastSpecialistCommands.Patch_Pawn_GetGizmos:GizmoOnGUIPrefix"] = 0xDA6444EC5E5D0564,
			["AutoCastSpecialistCommands.Patch_Pawn_GetGizmos:GizmoOnGUIPostfix"] = 0xEAEA3D4FF6046A94,
			["ReplaceLib.Utils:RecipeUsersAreSame"] = 0xF3B4168D7D0AA822,                               // ReplaceLib 1.6 DLL (loaded on 1.7), 2025-11-23
			["ReplaceLib.Utils:ProcessRecipes"] = 0x40D2BD58300965B3,
			["UsefulMarks.Dialog_NamePawn_DoWindowContents_Patch:Postfix"] = 0xF33AD5F31EB56B3B,        // Useful Marks 1.7, DLL of 2026-10-22
			["UsefulMarks.Patch_MainTabWindow_Inspect_DoInspectPaneButtons:Prefix"] = 0xD2A3A440ED802D16,
			["AlteredCarbon.JobGiver_Reload_TryGiveJob_Patch:Postfix"] = 0xAA1E07B7E7346A11,             // Altered Carbon 2 1.6 DLL, 2026-10-29
			["IdeologyPatch.LookChangeDesiredFix.LookChangeDesiredUtility:CanUseStyle"] = 0x59A806A930DD6F06, // 1trickPwnyta's Ideology Patch 1.6 DLL, 2026-11-21
			["RimWorld.MainTabWindow_Inspect:DoInspectPaneButtons"] = 0x9ADB9BA182DCDD68,               // vanilla 1.7.5013, guilty icon slot
			["UIRethemed.ColonistCard.Card:get_ThemingHeader"] = 0xE0BB7E0F75774563,                     // UI Rethemed - Colonist Card 1.7, 2026-11-15 03:21
			["UIRethemed.ColonistCard.Card:NextHeaderSlot"] = 0x140F60EC9F696E2F,
			["TD_Enhancement_Pack.JumpToSelection:Postfix"] = 0xED9E022B9869D83B,                         // TD Enhancement Pack - Continued 1.7, DLL of 2026-10-22 (eye button tooltip)
			["DOORAJP.ColonyKit.InfoButtons:DrawInspect"] = 0xC5783460D410E954,                                   // Colony Tabs Framework 1.7, DLL of 2026-11-22 16:02 (Catalog "C" button)
			["DOORAJP.ColonyKit.InfoButtons:DrawCard"] = 0xE1B1120907D6711F,
			["UIRethemed.InfoCard.InfoCardWindow:TitleTools"] = 0x5F5F9D5CAFCF963F,                                // UI Rethemed - Info Card 1.7, DLL of 2026-11-22 16:52
			["UIRethemed.InfoCard.InfoCardWindow:Themed"] = 0xD26943316A2109F1,
			["UIRethemed.ColonistCard.StatusBar:Take"] = 0x57D442485564A584,                            // UI Rethemed - Colonist Card 1.7, DLL of 2026-11-19 17:22
			["UIRethemed.Animals.Motion:Key"] = 0xD8B1E64161CEBA55,                                     // UI Rethemed - Animals 1.7, DLL of 2026-11-09
			["UIRethemed.Animals.Motion:Hover"] = 0xCF658FE8AAEAECFD,
			["UIRethemed.Architect.ArchitectData:ShownApart"] = 0x5ACDBDDD5FF8FB5A,                  // UI Rethemed - Architect 1.7, DLL of 2026-11-20 12:28
			["UIRethemed.Architect.DetailsPanel:Header"] = 0xE960A861D26E369E,                         // UI Rethemed - Architect 1.7, DLL of 2026-11-20 12:28 (names on two lines)
			["UIRethemed.Architect.DesignatorGrid:get_CardHeight"] = 0xB8237D3366CA6C97,
			["UIRethemed.Architect.DesignatorGrid:LabelRect"] = 0xEA794A59EE25933A,
			["UIRethemed.Architect.DesignatorGrid:Card"] = 0xD4150E0907DE465B,
			["PipeSystem.CompResourceTrader:CompInspectStringExtra"] = 0x8799CC4254BA7002,           // VEF PipeSystem 1.7, DLL of 2026-11-20 12:28
			["Hospitality.JoyGiver_BuyStuff:CanEat"] = 0x304D1E10604FFCD2,                           // Hospitality 1.7
			["Hospitality.JoyGiver_BuyStuff:Qualifies"] = 0xB8921C6506227CE6,                        // Hospitality 1.7
			["RimWorld.Bill_Production:ShouldDoNow"] = 0x91426C66CA668FA7,                           // game 1.7.5014, 2026-11-20 (bill count cache)
			["Inventory.BillUtility:Satisfied"] = 0xAC4273F963D8D1D6,                                // Loadout Compositing 1.7, 2026-11-22 (bill count cache, per-tag bills)
			["Verse.Pawn:SpawnSetup"] = 0xEBD14AA4AA28AC29,                                           // game 1.7.5014, 2026-11-23 (bill count cache dropped when colonists arrive)
			["VanillaGravshipExpanded2.WarcomputerHelper:ApplyForcedMissRadiusBuffs"] = 0x7C5E4AA83043349A, // Vanilla Gravship Expanded - Chapter 2 1.7, 2026-11-22
			["QuietWonders.QWLife:FindAsleepRooms"] = 0x1C199B69873D2271,                                       // Quiet Wonders, 2026-11-24 (room checks for awake pawns)
			["VanillaFurnitureExpandedFactory.StoreUtility_IsGoodStoreCell_Patch:Postfix"] = 0xCE7EFB50ACFC94E1, // VFE Factory 1.7, 2026-11-24 (no-insert hopper in the haul list)
			["GUIPerformancePatch.GppFightTarget:Pre"] = 0x36D1CBE254914D43,                         // WOWGAG's Performance Patch 1.7, DLL of 2026-11-24 18:20 (validator method lookup)
			["GUIPerformancePatch.GppFightTarget:Post"] = 0x239D8B379E1FB455,
			["Verse.Reachability:CheckRegionBasedReachability"] = 0x0AE52A32FAF5BA8D,                // game 1.7.5014, 2026-11-22 (reach flood memo)
			["Verse.Reachability:QueueNewOpenRegion"] = 0xAA67D4BF0BC72D97,
			["Verse.Reachability:CanReach"] = 0x22585B275FCEF578,
			["MSSFP.AICore.WorkGiver_HaulToAICore:ShouldSkip"] = 0x0350B7DEC7D6189E,                 // MSS Tweaks and Fun 1.7 DLL, 2026-11-22 (work givers walking every building)
			["HospitalityCasino.VendingMachineJobHelper:GetActiveVendingMachines"] = 0xDBFC4DAEA453A8BB, // Hospitality: Casino 1.7, 2026-11-22
			["VendingMachines.VendingMachineJobHelper:GetActiveVendingMachines"] = 0xE1112F0CE73AFE00,   // Vending Machines 1.7, 2026-11-22
			["Verse.AI.HaulAIUtility:FindFixedIngredientCount"] = 0xDCBB4E72DB147D94,                // game 1.7.5014, 2026-11-22 (ingredient search with nothing on the map)
			["RimWorld.WorkGiver_Scanner:PotentialWorkThingsGlobal"] = 0x085B7607B50E566B,           // game 1.7.5014, 2026-11-22 (bill work givers search their own benches)
			["RimWorld.WorkGiver_DoBill:JobOnThing"] = 0xB0F3DD23EFC7C7D4,
			["RimWorld.JobGiver_Work:TryIssueJobPackage"] = 0x821C69E177FA6B60,
			["Verse.GenClosest:ClosestThingReachable"] = 0x7CD995008E1FDFFD,
			["RimWorld.WorkGiver_Merge:PotentialWorkThingsGlobal"] = 0x5AC841F79E5E990F,             // game 1.7.5014, 2026-11-22 (merge search: only stacks with a possible partner)
			["RimWorld.WorkGiver_Merge:JobOnThing"] = 0xD4A1FFB8CB803F0F,
			["Verse.GenTypes:IsSealedWithCache"] = 0x809310C4C09A3747,                           // game 1.7.5014, 2026-11-20 (tick hot paths)
			["Verse.ReachabilityCache:ClearForHostile"] = 0x5BED8EFCC91F582A,
			["Verse.ReachabilityCache:AddCachedResult"] = 0x52C17A50E704CDE7,                     // game 1.7, 2026-11-24 (door walk skipped when no cached pawn is hostile)
			["Verse.ReachabilityCache:Clear"] = 0x34E418474887367D,
			["Verse.ReachabilityCache:ClearFor"] = 0x6C9DA9A3AAD02814,
			["Verse.TickManager:TickManagerUpdate"] = 0xBD63B2C8DA3F0F13,
			["RimWorld.PregnancyUtility:IsUsableBedFor"] = 0xB20AF11F5CB7A925,                         // game 1.7.5014, 2026-11-20 (no birth in an Altered Carbon sleeve casket)
			["RimWorld.CompNeuralSupercharger:CanAutoUse"] = 0x8735971C2DE0F655,                       // game 1.7.5014, 2026-11-20 (MSS hologram: no supercharger, no body-need thoughts)
			["RimWorld.ThoughtWorker_NeedNeuralSupercharge:ShouldHaveThought"] = 0xD1F51B96044129D0,
			["RimWorld.ThoughtWorker_Precept_HasNoProsthetic:ShouldHaveThought"] = 0x0AF51DBC40387586,
			["RimWorld.ThoughtWorker_Precept_HasNoProsthetic_Social:ShouldHaveThought"] = 0xC00806208BA0013F,
			["RimWorld.StunHandler:Notify_DamageApplied"] = 0xC6A267835D6DFF3C,                        // game 1.7.5014, 2026-11-20 ("Adapted" text once a second)
			["Verse.Pawn_HealthTracker:PostApplyDamage"] = 0x7279655035C5644A,                        // game 1.7.5014, 2026-11-21 (no zero-severity damage hediffs)
			["Verse.Sound.SubSustainer:StartSample"] = 0xE01DA2EFCC466EB7,                             // game 1.7, 2026-11-24 (no sample on an ended sustainer)
			["RimWorld.QuestPart_SubquestGenerator_Gravcores:QuestPartTick"] = 0x57A1138EAA085FF4,     // game 1.7.5014, 2026-11-20 (cheap tests first)
			["RimWorld.QuestPart_SubquestGenerator_Gravcores:get_CanGenerateSubquest"] = 0xD22781A31626AD88,
			["RimWorld.JobDriver_BoardOrLeaveGravship:GetGotoToil"] = 0x47A90334C2BA15AE,               // game 1.7.5014, 2026-11-23 (stale leave/board ship job ends)
			["RimWorld.QuestGen.QuestGen_Get:GetMap"] = 0x6C6254498E3B236E,                             // game 1.7.5014, 2026-11-23 (Better Beggars: request on a stocked map)
			["BetterBeggars.QuestNode_Root_Beggars_WantThing_Vanilla:RunInt"] = 0x1471BE01935A9CDF,     // Better Beggars 1.7 DLL, 2026-11-23
			["BetterBeggars.QuestNode_Root_Beggars_WantThing_Vanilla:TestRunInt"] = 0x592895DAD3977993,
			["BetterBeggars.QuestNode_Root_Beggars_WantThing_Drugs:RunInt"] = 0x7D0E5489F47A7DD6,
			["BetterBeggars.QuestNode_Root_Beggars_WantThing_Drugs:TestRunInt"] = 0x55E5DD90B712052D,
			["Verse.CellInspectorDrawer:OnGUI"] = 0x61C79C22E0809E53,                                  // game 1.7.5014, 2026-11-22 (wider Alt cell inspector)
			["Verse.CellInspectorDrawer:DrawRow"] = 0xA3DBA1BA87FFE7BE,
			["Verse.CellInspectorDrawer:DrawThingRow"] = 0x2A68EEF385EC54DF,
			["Verse.CellInspectorDrawer:DrawHeader"] = 0x754A6C2ABA91690F,
			["Verse.CellInspectorDrawer:DrawDivider"] = 0x512EB3753CC82A88,
			["Verse.CellInspectorDrawer:DrawMapInspector"] = 0x5645A56FAD5DCE29,
			["Verse.CellInspectorDrawer:DrawWorldInspector"] = 0x0315662F65B51CB2,
			["Verse.Widgets:GetIconFor"] = 0x2EA01CE62364ED0C,                                         // (Thing, ...) overload, game 1.7.5014, 2026-11-22 (VEF floor graphic icon scale)
			["TD_Enhancement_Pack.WorkGiver_HaulUrgentlyRefill:PotentialWorkThingsGlobal"] = 0x666AFF20872A52D4, // TD Enhancement Pack - Continued 1.7, 2026-11-20
			["TD_Enhancement_Pack.Alert_NoBill:GetReport"] = 0x749F608E209682C4,
			["SatelliteMinimap.Feed:GatherCore"] = 0x8DE81F8EF7D9D5CA,                             // Satellite Minimap 1.1.2, DLL of 2026-10-09 (Mechanoids layer row, marker cap)
			["SatelliteMinimap.Feed:Add"] = 0x7B585F47FD6B42DC,
			["SatelliteMinimap.MinimapWindow:OpenLayersMenu"] = 0xE47E83E43EE95A27,
			["SatelliteMinimap.MinimapWindow:Toggle"] = 0x2C37E6475E92163E,
			["SatelliteMinimap.MinimapSettings:ExposeData"] = 0x75F6660846866D5A,
			["RimWorld.Planet.SettleUtility:AddNewHome"] = 0x69C44B511913CADB,                       // game 1.7.5014, 2026-11-22 ("live in a new settlement" want goes to the settlers)
			["WantsAndQuirks.SettleUtility_AddNewHome_Patch:Postfix"] = 0x8F77A64C6D0DB00F,          // Character Development, DLL of 2026-11-13
			["Impact.WeaponEqualizer:Run"] = 0xFC3B14BF7DB2BCE2,                                   // IMPACT - Combat Overhaul 1.0.1, 2026-11-23 (damage floor scaled by rate of fire)
			["Impact.WeaponEqualizer:Equalize"] = 0xEF0B66E9178B4A5A,
			["Impact.WeaponEqualizer:GetClamps"] = 0x55A423DD45A86ED9,
			["Impact.WeaponEqualizer:ShouldEqualize"] = 0xB7CD4110FA07356C,
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

		internal static bool tmmGuildSkipped;
		internal static MethodInfo doBillJobOnThing;
		static bool SkipOriginal() => false;

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

		// ---- Smarter Visitors + Hospitality --------------------------------------------------------------
		// Smarter Visitors prefixes Trigger_TicksPassed.ActivateOn for every ticks trigger of a VisitColony lord, not
		// only Hospitality's stay timer (Trigger_TicksPassedAndOkayToLeave): also vanilla's Trigger_TicksPassedWithoutHarm
		// (1200) that returns the group from LordToil_DefendPoint to its travel toil after a fight. With "Visitors will
		// not leave if needing medical rest" on, one hurt visitor pushes that deadline out by 2500 ticks every time it
		// comes up, so a group hurt while leaving stands at its defend point eating and sleeping and never exits.
		// Its prefix handles the stay timer only; every other ticks trigger runs vanilla.
		// The same prefix also holds a group whose "afraid to leave" question waits as a letter (VisitorQuestionLetter):
		// the stay timer reports "not yet" until the letter is answered, dismissed or timed out. svAskingLord names the
		// group while Smarter Visitors' prefix runs, so the leave question it opens can be turned into that letter.
		internal static Type svStayTimer;
		internal static Lord svAskingLord;

		static bool SvOnlyStayTimer(Trigger_TicksPassed __0, Lord __1, ref bool __3, ref bool __result)
		{
			if (!svStayTimer.IsInstanceOfType(__0))
			{
				__result = true; // run Trigger_TicksPassed.ActivateOn unchanged
				return false;
			}
			if (VisitorQuestionLetter.Holds(__1))
			{
				__3 = false;     // the stay timer has not run out
				__result = false; // skip Smarter Visitors' prefix and the original
				return false;
			}
			svAskingLord = __1;
			return true;
		}

		static void SvAskDone() => svAskingLord = null;

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
		// null" (Iremar). RimPacts 2026-11-07 added its own daily RptLeaderRepair, but it skips defeated factions, so
		// a fallen faction whose exiled leader was discarded still ends up leaderless. Vanilla picks a successor
		// whenever a leader dies or leaves; do the same, silently: right after RimPacts discards a leader, and
		// after every load for any faction that should have one but doesn't.
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

		// ---- RimPacts: trader weather tolerance for the pack animals only ---------------------------------------
		// "Protect ordinary traders from temperature" (wardTraderTemp) gives every caravan member Rpt_TradeTempWard
		// (comfy temperature -1000/+1000 for 5 days). It only gated the vanilla trader-arrival and new-lord hooks;
		// RimPacts' own caravans (treaty trade, called and grand caravans, their escorts) call WardLordPawns directly.
		// Personal rule: people dress for the weather (Dress for the Weather), so the ward is for animals only, and only
		// while the setting is on. WardLordPawns is replaced by that rule, and a ward on anyone else drops on the
		// hediff's next 250-tick check.
		static MethodInfo rpSettingsGetter;
		static FieldInfo rpWardTraderTemp;

		static bool RpWardEnabled() => rpSettingsGetter.Invoke(null, null) is not { } settings || (bool)rpWardTraderTemp.GetValue(settings);

		static bool RpWardAllowed(Pawn pawn) => pawn.RaceProps.Animal && RpWardEnabled();

		static bool RpWardOnlyWhenEnabled(Lord l)
		{
			if (l?.ownedPawns == null || !RpWardEnabled()) return false;
			var ward = DefDatabase<HediffDef>.GetNamed("Rpt_TradeTempWard");
			foreach (var pawn in l.ownedPawns)
				if (pawn != null && !pawn.Dead && pawn.health?.hediffSet != null && pawn.RaceProps.Animal && !pawn.health.hediffSet.HasHediff(ward))
					pawn.health.AddHediff(ward);
			return false;
		}

		static void RpWardDropWhenDisabled(HediffComp __instance)
		{
			var pawn = __instance.Pawn;
			if (pawn == null || !pawn.IsHashIntervalTick(250) || RpWardAllowed(pawn) || !pawn.health.hediffSet.hediffs.Contains(__instance.parent)) return;
			pawn.health.RemoveHediff(__instance.parent);
		}

		// ---- Readable Numbers + Better Scroll Bars: GUI backend wrappers bypassed by the ImGui host ---------------
		// Both mods wrap GuiBackend.Current (Readable Numbers once in its Mod constructor, Better Scroll Bars from an
		// OnGUI on the Camera object). The 1.7 ImGui host swaps its own backend in for every GUI pass
		// (ImGuiHost.Activation: Current = host.guiBackend) and restores the previous one afterwards, so neither wrapper
		// ever saw the frame: stock grey scroll bars, unformatted numbers. After the host's swap the same wrappers go
		// around the host backend, in the mods' own order (game, Readable Numbers, Better Scroll Bars), built once per
		// host backend.
		static Type guiWrapRn, guiWrapBsb;
		static UnityStubs.Gui.IGuiBackend guiWrapBase, guiWrapped;

		static void GuiHostKeepModBackends()
		{
			var current = UnityStubs.Gui.GuiBackend.Current;
			if (current == null || current == guiWrapped || guiWrapBsb?.IsInstanceOfType(current) == true || guiWrapRn?.IsInstanceOfType(current) == true) return;
			if (current != guiWrapBase)
			{
				// ponytail: one cached chain; rebuilt whenever a different host backend is activated
				var wrapped = current;
				if (guiWrapRn != null) wrapped = (UnityStubs.Gui.IGuiBackend)Activator.CreateInstance(guiWrapRn, wrapped);
				if (guiWrapBsb != null) wrapped = (UnityStubs.Gui.IGuiBackend)Activator.CreateInstance(guiWrapBsb, wrapped);
				guiWrapBase = current;
				guiWrapped = wrapped;
			}
			UnityStubs.Gui.GuiBackend.Current = guiWrapped;
		}

		// ---- RimPacts + Dress for the Weather: caravan guards spawned in batches ---------------------------------
		// RimPacts' own trade caravans (TrySpawnTradeCaravanRobust) skip the vanilla guard generation and spawn the
		// guards one per step later (CaravanGuardStep: GeneratePawn, GenSpawn.Spawn, lord.AddPawn). Dress for the
		// Weather only dresses pawns from IncidentWorker_NeutralGroup.SpawnPawns, so these guards arrived in whatever
		// their kind generated. Under Alpha Memes' "naked truth" (AM_Armour_Forbidden destroys all generated apparel)
		// that was nothing at -75 C, while the trader, who goes through SpawnPawns, got dressed. Each guard a step adds
		// to its lord now gets Dress for the Weather's treatment too.
		static Action<Pawn, Map> dftwDressCall;
		static Action dftwPairsCall;
		static bool rpGuardStepping;
		static Lord rpGuardPairsLord;

		static void RpGuardStepBegin() => rpGuardStepping = true;

		static Exception RpGuardStepEnd(Exception __exception)
		{
			rpGuardStepping = false;
			return __exception;
		}

		static void RpGuardDressForWeather(Lord __instance, Pawn p)
		{
			if (!rpGuardStepping || p?.apparel == null || !p.Spawned) return;
			try
			{
				// ponytail: apparel pairs rebuilt once per caravan lord, not per guard (same list DftW builds per incident)
				if (rpGuardPairsLord != __instance)
				{
					dftwPairsCall();
					rpGuardPairsLord = __instance;
				}
				dftwDressCall(p, p.Map);
			}
			catch (Exception e)
			{
				Log.ErrorOnce($"[Harmony3StrictCompat] Dress for the Weather on RimPacts caravan guard {p}: {e}", 0x5D7A61);
			}
		}

		// ---- RimPacts auto-resolved defense: wounds land weeks late ---------------------------------------
		// WorldComponentTick runs DefAutoTick (every 250 ticks) only while a defend mission exists. The mission ends
		// when the auto-defense is settled, but fighters who flew out get their "minor wounds" (bruises added directly,
		// no damage event) on the first DefAutoTick after they are back: that never comes, the record stays, and the
		// bruises (and the result letter) appear out of nowhere when the next defense plea arrives, seen 30 days later
		// on 2026-11-16. Run DefAutoTick on the same 250-tick beat while records are left and no mission is active.
		// Only when every record is settled: an unsettled one still needs its mission.
		static MethodInfo rpDefAutoTick, rpDefendMission;
		static FieldInfo rpDefAutoRecs, rpDefAutoSettled;

		static void RpAutoDefenseFinishes(object __instance)
		{
			int ticks = Find.TickManager.TicksGame;
			if (ticks % 250 != 0 || rpDefendMission.Invoke(__instance, null) != null) return;
			if (rpDefAutoRecs.GetValue(__instance) is not System.Collections.ICollection { Count: > 0 } recs) return;
			foreach (var rec in recs)
				if (rec == null || !(bool)rpDefAutoSettled.GetValue(rec)) return;
			rpDefAutoTick.Invoke(__instance, [ticks]);
		}

		// ---- RimPacts: no revolt back to a faction that is being absorbed -----------------------------------
		// AbsorbFaction sends "Faction absorbed" at once but moves every settlement after the first over the following
		// days (absorbPlans), and the loser is only marked defeated once the last one has moved. TryRevertConquered
		// (conquered settlements returning to their original owner) skips only defeated owners, so in that window a
		// settlement already handed over could revolt back: on 2026-11-17 "Invaders" returned to the absorbed Slayers,
		// who then outlive their own absorption. Skip the revolt while the original owner still has absorb plans.
		static MethodInfo rpOriginalOwnerOf;
		static FieldInfo rpAbsorbPlans, rpPlanLoser;

		static bool RpNoRevoltToAbsorbed(object __instance, Settlement s, ref string __result)
		{
			if (rpOriginalOwnerOf.Invoke(__instance, [s]) is not Faction owner || rpAbsorbPlans.GetValue(__instance) is not IList plans) return true;
			foreach (var plan in plans)
				if (rpPlanLoser.GetValue(plan) == owner)
				{
					__result = "original owner is being absorbed (Harmony3StrictCompat)";
					return false;
				}
			return true;
		}

		// ---- RimPacts trade protection pact: only route settlements count --------------------------------
		// The pact covers "settlements your trade routes depend on", and RimPacts only offers the defense battle
		// for a settlement one of the player's trade routes ends at (IsProtectedByTradePact). ApplyProtectionFailure
		// has no such check: any settlement of the pact faction falling tears up the pact and the trade agreement
		// (trust -25, "A promise unkept") without the player ever being asked to defend it. Skip the penalty for
		// settlements that were never protected.
		static MethodInfo rpIsProtected;

		static bool RpProtectionOnlyRoutes(object __instance, Settlement s) =>
			(bool)rpIsProtected.Invoke(__instance, new object[] { s });

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
		// ---- Vanilla: a click on a plant grower selects the grower first ---------------------------------------------
		// Personal tweak: Selector.SelectUnderMouse sorts what is under the mouse by this key (0 first) and a repeat
		// click on the same spot moves to the next entry. Vanilla gives 0 only to storage, so a click on a plant pot or a
		// hydroponics basin picked the plant. Plant growers (any Building_PlantGrower subclass) get 0 too: first click the
		// grower, second the plant.
		static void PlantGrowerSelectedFirst(object x, ref int __result)
		{
			if (x is Building_PlantGrower) __result = 0;
		}

		// ---- Vanilla (+ GUI Performance Patch + VEF abilities): tooltips on the top row of small gizmos ----
		// DrawGizmoGrid records how high the grid reaches (HeightDrawnRecently) but for shrunk commands it stores
		// Mathf.Min(heightDrawn, row top), so small-button rows stacked above the big ones are left out (and the
		// height even drops to the small row's bottom). WOWGAG's GUI Performance Patch reuses the previous frame's
		// gizmo objects while the mouse is outside that height; VEF's Command_Ability prepends its label to
		// defaultDesc on every shrunk draw, so a reused object's tooltip text grew each frame, TooltipHandler saw a
		// new tip every frame and its delay never ran out: no tooltips on the top small row. Store Mathf.Max instead.
		static IEnumerable<CodeInstruction> GizmoHeightCoversShrunkRows(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var min = AccessTools.Method(typeof(Mathf), nameof(Mathf.Min), [typeof(float), typeof(float)]);
			var max = AccessTools.Method(typeof(Mathf), nameof(Mathf.Max), [typeof(float), typeof(float)]);
			var height = AccessTools.Field(typeof(GizmoGridDrawer), "heightDrawn");
			var hits = new List<int>();
			for (int i = 0; i + 1 < codes.Count; i++)
				if (codes[i].Calls(min) && codes[i + 1].StoresField(height))
					hits.Add(i);
			if (hits.Count != 1 || height == null)
			{
				Log.Error($"[Harmony3StrictCompat] GizmoGridDrawer.DrawGizmoGrid shrunk-row height not found ({hits.Count} matches): the game updated, redo this fix");
				return codes;
			}
			codes[hits[0]].operand = max;
			return codes;
		}

		// ---- TD Enhancement Pack: deterioration alert without corpses ---------------------------------------------
		// Personal tweak: the "Deteriorating" alert already leaves out rotting corpses, but lists every fresh one left
		// outside (raid dead), burying real items in the list. Drop all corpses from it.
		static void TdDeteriorationNoCorpses(ref IEnumerable<Thing> __result)
		{
			if (__result != null) __result = __result.Where(t => t is not Corpse);
		}

		// ---- Smart Medicine: "use nearby medicine" measured in tiles ---------------------------------------------
		// Find swaps inventory medicine (doctor's, patient's) for equal ground medicine when the ground one's distance
		// <= minDistance + distanceToUseEqualOnGround * 2. minDistance is the doctor-patient Manhattan distance in
		// tiles, but the ground distance is the pathfinding cost (walking ticks, ~10-13 a tile) from the medicine to
		// the doctor plus from the medicine to the patient, so the "6 spaces" setting meant about one tile and a
		// cabinet 4 tiles from the bed never qualified. Swap that one read for the Manhattan detour in tiles; the
		// sort and the time-until-death limit keep Smart Medicine's own path cost.
		static FieldInfo smEvalDistance, smEvalThing, smNearbySetting;
		[ThreadStatic] static Pawn smFindHealer, smFindPatient;

		static void SmFindRemember(Pawn healer, Pawn patient)
		{
			smFindHealer = healer;
			smFindPatient = patient;
		}

		static int SmGroundTiles(Thing medicine)
		{
			if (medicine == null || smFindHealer == null || smFindPatient == null) return int.MaxValue;
			return (medicine.Position - smFindHealer.Position).LengthManhattan + (medicine.Position - smFindPatient.Position).LengthManhattan;
		}

		static IEnumerable<CodeInstruction> SmNearbyInTiles(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var hits = new List<int>();
			for (int i = 0; i < codes.Count; i++)
				if (codes[i].LoadsField(smEvalDistance) && codes.Skip(i + 1).Take(5).Any(c => c.LoadsField(smNearbySetting)))
					hits.Add(i);
			if (hits.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] Smart Medicine nearby-medicine check not found ({hits.Count} matches): its mod updated, redo this fix");
				return codes;
			}
			codes[hits[0]].operand = smEvalThing;
			codes.Insert(hits[0] + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony3StrictCompatMod), nameof(SmGroundTiles))));
			return codes;
		}

		// ---- Common Sense: no sweep before medical bed rest ------------------------------------------------------
		// Its "clean before working" StartJob prefix queues a Clean job (filth in the target's room) ahead of any job
		// with allowOpportunisticPrefix aimed at a building, vanilla LayDown on a bed included. It only skips pawns
		// that need a tend right now, so a tended flu patient on bed rest gets up to sweep each time it lies down
		// again, and a doctor's tend fails meanwhile (GoodLayingStatusForTend needs the patient in bed). Cleaning
		// before ordinary sleep stays. __0 = the job about to start, __1 = the pawn.
		static bool CsNoSweepBeforeMedicalRest(Job __0, Pawn __1, ref Job __result)
		{
			if (__0?.def != JobDefOf.LayDown || __1?.health == null || !HealthAIUtility.ShouldSeekMedicalRest(__1)) return true;
			__result = null;
			return false;
		}

		// Ritual Size Attenuation keeps quality data per lord ritual and reads its comp from the ritual's precept
		// (ritual.Ritual.outcomeEffect), or from the bestowing ceremony's outcome. VFE Empire's art exhibit, grand
		// ball and parade are LordJob_Ritual without a precept (Ritual null) and not bestowings, so the participant
		// count / quality postfixes throw a NullReferenceException inside the outcome's Apply. For the art exhibit
		// that aborts StopExhibit after exhibitFinished is set but before the "CeremonyFinished" memo, and the lord
		// stays in the show toil forever (guests and colonists kept awake, starving). Such a data ref has no comp
		// (its constructor already logged "Failed to find the ritual quality comp"), so answer null: every caller
		// of GetFor then skips it and the ritual uses its unattenuated quality.
		static AccessTools.FieldRef<object, Lord> raLord;
		static AccessTools.FieldRef<object, int> raCompIndex;

		static bool RaCompWithoutPrecept(object __instance, ref RitualOutcomeComp_Quality __result)
		{
			var ritual = raLord(__instance)?.LordJob as LordJob_Ritual;
			if (raCompIndex(__instance) >= 0 && (ritual is LordJob_BestowingCeremony || ritual?.Ritual != null)) return true;
			__result = null;
			return false;
		}

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

	// ---- Biosculpter Pod and Neural Supercharger Resize + Vanilla Textures Expanded: pod icon at its new size ----
	// The resizer swaps the pod's size and graphic and sets uiIcon to the new south texture from a callback queued in
	// its Mod constructor, which runs before the defs' own icon loading. VTE patches a uiIconPath (the full 3x2 menu
	// art) onto BiosculpterPod, so that later load put the wide icon back, and blueprints and the architect button drew
	// it squeezed into the new 1x2 proportions. Static constructors run after both: set the icon the resizer meant.
	// Only when the resizer's own graphic is in use (its texPath), so the standard size keeps VTE's icon.
	[StaticConstructorOnStartup]
	static class ResizedBiosculpterIcon
	{
		static ResizedBiosculpterIcon()
		{
			var pod = DefDatabase<ThingDef>.GetNamedSilentFail("BiosculpterPod");
			if (pod?.graphicData?.texPath == null || !pod.graphicData.texPath.StartsWith("BiosculpterPod/BiosculpterPod_") || pod.graphic == null)
				return;
			pod.uiIcon = (Texture2D)pod.graphic.MatSouth.mainTexture;
		}
	}

	// ---- Neural supercharger recharge bar below a one-tile-tall machine -------------------------------------------
	// CompRechargeable.CompTick puts the recharge progress bar mote 0.5 below the building's centre, which is on the
	// machine for the vanilla 1x3 supercharger. The resizer's 1x2 (or 1x1) supercharger turned east or west is only one
	// tile tall on screen, so the bar sat on its bottom edge, half outside. When the rotated footprint is one tile tall,
	// raise the bar to 0.3 below centre, inside the machine. Taller footprints keep vanilla's spot.
	static class RechargeBarInside
	{
		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("RimWorld.CompRechargeable:CompTick") is { } tick)
				harmony.Patch(tick, postfix: new HarmonyMethod(typeof(RechargeBarInside), nameof(Raise)));
		}

		static void Raise(CompRechargeable __instance, Effecter ___progressBar)
		{
			if (___progressBar?.children is { Count: > 0 } children && children[0] is SubEffecter_ProgressBar { mote: { } mote }
				&& __instance.parent.RotatedSize.z == 1)
				mote.offsetZ = -0.3f;
		}
	}

	// ---- VQE The Generator: "Craft an ARC component" only sees the first thing in the target cell ------------------
	// CompAbilityCraftAnARCComponent.Apply takes target.Cell.GetThingList(map).FirstOrFallback() and refuses unless
	// that one thing is an advanced component. A component lying on a shelf or in any storage building, or sharing
	// its cell with filth or a hidden conduit, comes after that thing in the list, so the cast ended with "needs to
	// target an advanced component" (seen 2026-11-24 with Seaborgue). Look for the advanced component in the cell
	// instead; with none there the original refusal still happens.
	static class VqeArcComponentFindsComponent
	{
		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("VanillaQuestsExpandedTheGenerator.CompAbilityCraftAnARCComponent:Apply") is { } apply)
				harmony.Patch(apply, transpiler: new HarmonyMethod(typeof(VqeArcComponentFindsComponent), nameof(UseComponent)));
		}

		static IEnumerable<CodeInstruction> UseComponent(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var first = codes.Where(c => c.opcode == OpCodes.Call && c.operand is MethodInfo m && m.Name == "FirstOrFallback").ToList();
			if (first.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] VQE CompAbilityCraftAnARCComponent.Apply changed ({first.Count} FirstOrFallback calls): ARC component cast left as it is");
				return codes;
			}
			first[0].operand = AccessTools.Method(typeof(VqeArcComponentFindsComponent), nameof(ComponentOrFirst));
			return codes;
		}

		// Same stack shape as GenCollection.FirstOrFallback<Thing>(IEnumerable<Thing>, Thing).
		static Thing ComponentOrFirst(IEnumerable<Thing> things, Thing fallback) =>
			things.FirstOrDefault(t => t.def == ThingDefOf.ComponentSpacer) ?? things.FirstOrFallback(fallback);
	}

	// ---- Gravship Fleet: launch hold stops visitors the launch told to leave ---------------------------------------
	// While a launch ritual runs, FleetLaunchHold (every 15 ticks) stops any pawn on the hull walking to a cell off
	// the ship and puts it on Wait_MaintainPosture; only ritual participants and drafted pawns are exempt. The ritual
	// itself sends non-hostile visitors off the ship (Building_GravEngine.pawnsToLeave, job LeaveShip), so those still
	// aboard were stopped a few ticks in; the LeaveShip goto then ended aboard, CheckFailedToBoardOrLeave said "X cannot
	// exit" and dropped them from the list, and they flew off with the ship. Seen 2026-11-24 with two Itundi caravan
	// guards (gravleave probe: LeaveShip ended by FleetLaunchHold.Hold at ritual tick 3). Leave LeaveShip alone.
	static class FleetHoldLetsLeaversGo
	{
		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("GravshipFleet.FleetLaunchHold:Hold") is { } hold)
				harmony.Patch(hold, prefix: new HarmonyMethod(typeof(FleetHoldLetsLeaversGo), nameof(SkipLeaving)));
		}

		static bool SkipLeaving(Pawn pawn) => pawn?.CurJobDef != JobDefOf.LeaveShip;
	}

	// ---- Better Abandoned Settlements + Vanilla Gravship Expanded: "Keep the maps a gravship leaves behind" ignored --
	// BAS tells a gravship launch from a plain abandon by a flag it raises only inside vanilla
	// GravshipUtility.AbandonMap (prefix + finalizer), and with keepGravship off it skips storing the map. VGE's
	// TakeoffEnded prefix sets mapHasGravAnchor, so vanilla never calls AbandonMap; "Discard map" (or VGE's no-dialog
	// branch) runs its own AbandonTile, which calls Settlement.Abandon(false) directly. BAS saw a normal abandon and
	// kept every landing site (three "The Flying Shark landing site" kept settlements by 2026-11-24). Run BAS's own
	// launch-flag prefix and finalizer around AbandonTile too.
	static class BasGravshipFlagForVge
	{
		internal static void Init(Harmony harmony)
		{
			var prefix = Harmony3StrictCompatMod.Target("BetterAbandonedSettlements.Patches:AbandonMapPrefix");
			var finalizer = Harmony3StrictCompatMod.Target("BetterAbandonedSettlements.Patches:AbandonMapFinalizer");
			if (prefix == null || finalizer == null) return;
			if (Harmony3StrictCompatMod.Target("VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch+<>c__DisplayClass0_0:<Prefix>g__AbandonTile|1") is { } abandonTile)
				harmony.Patch(abandonTile, prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
		}
	}

	// ---- Vanilla Gravship Expanded: launch warning lists pawns that stay safely behind (personal tweak) ----------
	// GravshipUtility.PreLaunchConfirmation always opens a confirmation and lists every player pawn and animal off the
	// gravship as "will be left behind". With VGE that loss does not happen when the map is kept: its TakeoffEnded
	// prefix keeps the map whenever a grav anchor is on it, or (unless the scenario forbids keeping the map) a colonist
	// stays (MapPawns.AnyPawnBlockingMapRemoval -> ShouldAlwaysKeepThisMap). Animals, prisoners or mechs alone do not
	// keep it: VGE then asks keep or discard, so their warning stays.
	// In that kept case the people and animal lists are dropped (the first two List<Pawn>.Count checks read 0), and if
	// nothing else is left to warn about (kidnapped pawns, buildings off the ship, substructure problems) the launch runs
	// without the dialog: the launch ritual window was already the confirmation. The only caller is the launch ritual's
	// RitualOutcomeEffectWorker_GravshipLaunch.Apply; the launch action is held until Apply has returned (after VGE's
	// postfix fills its ExtendedLaunchInfo), as a click on the dialog would. Called from anywhere else, the dialog shows.
	static class GravshipLaunchKeptMapNoWarning
	{
		static Func<MapParent, bool> canEverKeep;
		static Map launchMap;
		static bool hideLists, inRitual;
		static Action pending;

		internal static void Init(Harmony harmony)
		{
			var confirm = Harmony3StrictCompatMod.Target("RimWorld.GravshipUtility:PreLaunchConfirmation");
			var keep = Harmony3StrictCompatMod.Target("VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:CanEverKeepThisMap");
			var apply = Harmony3StrictCompatMod.Target("RimWorld.RitualOutcomeEffectWorker_GravshipLaunch:Apply");
			if (confirm == null || keep == null || apply == null
				|| Harmony3StrictCompatMod.Target("VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:Prefix") == null
				|| Harmony3StrictCompatMod.Target("VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:ShouldAlwaysKeepThisMap") == null)
				return;
			canEverKeep = (Func<MapParent, bool>)keep.CreateDelegate(typeof(Func<MapParent, bool>));
			harmony.Patch(confirm,
				prefix: new HarmonyMethod(typeof(GravshipLaunchKeptMapNoWarning), nameof(Begin)),
				transpiler: new HarmonyMethod(typeof(GravshipLaunchKeptMapNoWarning), nameof(Rewire)),
				finalizer: new HarmonyMethod(typeof(GravshipLaunchKeptMapNoWarning), nameof(End)));
			harmony.Patch(apply,
				prefix: new HarmonyMethod(typeof(GravshipLaunchKeptMapNoWarning), nameof(RitualBegin)),
				postfix: new HarmonyMethod(typeof(GravshipLaunchKeptMapNoWarning), nameof(RitualLaunch)) { priority = Priority.Last },
				finalizer: new HarmonyMethod(typeof(GravshipLaunchKeptMapNoWarning), nameof(RitualEnd)));
		}

		static void RitualBegin()
		{
			inRitual = true;
			pending = null;
		}

		static void RitualLaunch()
		{
			var launch = pending;
			pending = null;
			launch?.Invoke();
		}

		static void RitualEnd()
		{
			inRitual = false;
			pending = null;
		}

		static void Begin(Building_GravEngine engine)
		{
			launchMap = engine?.Map;
			hideLists = false;
		}

		static void End()
		{
			launchMap = null;
			hideLists = false;
		}

		static IEnumerable<CodeInstruction> Rewire(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var count = AccessTools.PropertyGetter(typeof(List<Pawn>), nameof(List<Pawn>.Count));
			var add = AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add));
			var counts = codes.Where(c => c.Calls(count)).ToList();
			var adds = codes.Where(c => c.Calls(add)).ToList();
			if (counts.Count != 3 || adds.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] GravshipUtility.PreLaunchConfirmation changed ({counts.Count} pawn list counts, {adds.Count} window adds): launch warning left as it is");
				return codes;
			}
			counts[0].opcode = OpCodes.Call;
			counts[0].operand = AccessTools.Method(typeof(GravshipLaunchKeptMapNoWarning), nameof(PeopleCount));
			counts[1].opcode = OpCodes.Call;
			counts[1].operand = AccessTools.Method(typeof(GravshipLaunchKeptMapNoWarning), nameof(AnimalCount));
			adds[0].opcode = OpCodes.Call;
			adds[0].operand = AccessTools.Method(typeof(GravshipLaunchKeptMapNoWarning), nameof(ShowOrLaunch));
			return codes;
		}

		// People left behind (player humanlikes off the ship, then prisoners): decides whether the map is kept.
		static int PeopleCount(List<Pawn> people)
		{
			hideLists = launchMap?.Parent != null && (launchMap.listerThings.AnyThingWithDef(ThingDefOf.GravAnchor)
				|| (canEverKeep(launchMap.Parent) && people.Any(p => p.IsColonist && !p.Dead)));
			return hideLists ? 0 : people.Count;
		}

		static int AnimalCount(List<Pawn> animals) => hideLists ? 0 : animals.Count;

		static void ShowOrLaunch(WindowStack stack, Window window)
		{
			if (hideLists && inRitual && window is Dialog_MessageBox box && box.buttonAAction != null
				&& box.text.RawText == "ConfirmGravEngineLaunch".Translate().RawText)
			{
				pending = box.buttonAAction;
				return;
			}
			stack.Add(window);
		}
	}

	// ---- Giddy-Up 2: animals carrying a downed pawn are picked as mounts ------------------------------------
	// MountUtility stops the chosen animal's job the moment a rider takes the Mount job, however far away the rider
	// is. IsMountable only refuses animals whose job is in its private BusyJobs set, and vanilla's Rescue (trained
	// animals, JobGiver_RescueNearby) is not in it: a rescuing animal dropped its bleeding patient and waited for a
	// rider three screens away (2026-11-15). Add Rescue to that set: every IsMountable caller (auto-mount, reserved
	// mount re-check, right-click menu, which then shows "busy") skips the animal until the rescue ends.
	[StaticConstructorOnStartup]
	static class GiddyUpRescueIsBusy
	{
		static GiddyUpRescueIsBusy()
		{
			var type = AccessTools.TypeByName("GiddyUp.IsMountableUtility");
			if (type == null) return;
			if (AccessTools.Field(type, "BusyJobs")?.GetValue(null) is HashSet<JobDef> busy)
				busy.Add(JobDefOf.Rescue);
			else
				Log.Error("[Harmony3StrictCompat] GiddyUp.IsMountableUtility.BusyJobs changed: rescuing animals can be picked as mounts again");
		}
	}

	// ---- Weapons with two equipment comps --------------------------------------------------------------------
	// A gun def that adds CompProperties_EquippableAbility(Reloadable) without <comps Inherit="False"> also inherits
	// BaseWeapon's plain CompEquippable (vanilla's Hellcat uses Inherit="False" for exactly this). Both comps save a
	// verbTracker with the same CompEquippable_<id>_N load IDs, so every such weapon in a save logs "Exception registering
	// ... in loaded object directory" on load, and GetComp<CompEquippable> returns the plain one. Seen: Pulse Weaponry
	// DV_PulseRifle (1.6 build). Drop the plain comp when a subclass is present; the extra saved verbTracker is ignored.
	// ---- Useful Marks: "Considered guilty" and "Missing body part" mark conditions (Source/UsefulMarks) ----
	// Those classes derive from a Useful Marks type, so they sit in a DLL of their own that RimWorld does not load itself.
	// Load it once every mod is in, and list it with this mod's assemblies so a saved mark finds the class by name.
	[StaticConstructorOnStartup]
	static class LoadUsefulMarksAddon
	{
		static LoadUsefulMarksAddon()
		{
			if (!ModsConfig.IsActive("Andromeda.UsefulMarks")) return;
			try
			{
				var pack = LoadedModManager.GetMod<Harmony3StrictCompatMod>().Content;
				var path = Path.Combine(pack.RootDir, "Compat", "Harmony3StrictCompat.UsefulMarks.dll");
				if (!File.Exists(path)) return;
				var addon = Assembly.Load(File.ReadAllBytes(path));
				pack.assemblies.loadedAssemblies.Add(addon);
				addon.GetType("Harmony3StrictCompat.UsefulMarksCompat.RegisterConditions").GetMethod("Run").Invoke(null, null);
			}
			catch (Exception e)
			{
				Log.Warning("[Harmony3StrictCompat] Useful Marks extra conditions not loaded: " + e);
			}
		}
	}

	// ---- UI Rethemed - Ideology: style hover card taller than the screen ----
	// Its style card (StyleTip.Content) is always 340 wide with 5 icon columns (8 for the faded "overridden by"
	// sections). A style that restyles ~95 things (e.g. a full Mesoamerican-type pack) makes a card about 1600 high,
	// so most of it runs off the screen. Widen the card in whole multiples of 340 (up to 4x, and never wider than
	// the screen) until it fits, and scale the column counts with the width so the icons keep their size.
	[StaticConstructorOnStartup]
	static class UirStyleCardFits
	{
		const float BaseWidth = 340f;
		static MethodInfo heightOf;
		static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, float[]> widths = new();

		static UirStyleCardFits()
		{
			var width = Harmony3StrictCompatMod.Target("UIRethemed.Ideology.StyleTip+Content:get_Width");
			if (width == null) return;
			heightOf = Harmony3StrictCompatMod.Target("UIRethemed.Ideology.StyleTip+Content:Height");
			var cellWidth = Harmony3StrictCompatMod.Target("UIRethemed.Ideology.StyleTip+Content:CellWidth");
			var sectionHeight = Harmony3StrictCompatMod.Target("UIRethemed.Ideology.StyleTip+Content:SectionHeight");
			var draw = Harmony3StrictCompatMod.Target("UIRethemed.Ideology.StyleTip+Content:Draw");
			if (heightOf == null || cellWidth == null || sectionHeight == null || draw == null) return;
			foreach (var m in new[] { cellWidth, sectionHeight, draw })
				if (!HasColumnConstants(m))
				{
					Log.Error($"[Harmony3StrictCompat] UI Rethemed - Ideology {m.Name}: column constants not found, style card fix skipped");
					return;
				}
			var harmony = new Harmony("sammael.harmony3strictcompat");
			harmony.Patch(cellWidth, transpiler: new HarmonyMethod(typeof(UirStyleCardFits), nameof(ColumnsFromWidthArg)));
			harmony.Patch(sectionHeight, transpiler: new HarmonyMethod(typeof(UirStyleCardFits), nameof(ColumnsFromWidthArg)));
			harmony.Patch(draw, transpiler: new HarmonyMethod(typeof(UirStyleCardFits), nameof(ColumnsFromRectArg)));
			harmony.Patch(width, postfix: new HarmonyMethod(typeof(UirStyleCardFits), nameof(WidthThatFits)));
		}

		// each of the three methods holds exactly one "faded ? 8 : 5"
		static bool HasColumnConstants(MethodInfo m)
		{
			var codes = PatchProcessor.GetOriginalInstructions(m);
			return codes.Count(c => c.opcode == OpCodes.Ldc_I4_5) == 1 && codes.Count(c => c.opcode == OpCodes.Ldc_I4_8) == 1;
		}

		static int Scale(float width) => Mathf.Max(1, Mathf.RoundToInt(width / BaseWidth));
		static int Columns(float width) => 5 * Scale(width);
		static int SmallColumns(float width) => 8 * Scale(width);

		// CellWidth(Section, float width) and SectionHeight(Section, float width): static, width is argument 1
		static IEnumerable<CodeInstruction> ColumnsFromWidthArg(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var code in instructions)
			{
				bool five = code.opcode == OpCodes.Ldc_I4_5, eight = code.opcode == OpCodes.Ldc_I4_8;
				if (!five && !eight) { yield return code; continue; }
				yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(code);
				yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UirStyleCardFits), five ? nameof(Columns) : nameof(SmallColumns)));
			}
		}

		// Draw(Rect rect, float alpha, float age): instance, rect is argument 1
		static IEnumerable<CodeInstruction> ColumnsFromRectArg(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var code in instructions)
			{
				bool five = code.opcode == OpCodes.Ldc_I4_5, eight = code.opcode == OpCodes.Ldc_I4_8;
				if (!five && !eight) { yield return code; continue; }
				yield return new CodeInstruction(OpCodes.Ldarga_S, (byte)1).MoveLabelsFrom(code);
				yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rect), nameof(Rect.width)));
				yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UirStyleCardFits), five ? nameof(Columns) : nameof(SmallColumns)));
			}
		}

		static void WidthThatFits(object __instance, ref float __result)
		{
			// the card adds a 30 high title and 16 of padding; keep a small margin to the screen edges
			float room = UI.screenHeight - 30f - 16f - 24f;
			int most = Mathf.Clamp(Mathf.FloorToInt((UI.screenWidth - 44f) / BaseWidth), 1, 4);
			if (!widths.TryGetValue(__instance, out var cached) || cached[0] != room || cached[1] != most)
			{
				float best = BaseWidth;
				for (int k = 1; k <= most; k++)
				{
					best = BaseWidth * k;
					if ((float)heightOf.Invoke(__instance, new object[] { best }) <= room) break;
				}
				widths.Remove(__instance);
				widths.Add(__instance, cached = new[] { room, most, best });
			}
			__result = cached[2];
		}
	}

	// ---- Vehicle Framework add-on (Source/Vehicles): Too Many Mods' Merchant Guild landing rules ----
	// Loaded only when TMM's failing patch class was found and skipped, which also means Vehicle Framework and
	// VFE Medieval 2 are active.
	[StaticConstructorOnStartup]
	static class LoadVehiclesAddon
	{
		static LoadVehiclesAddon()
		{
			if (!Harmony3StrictCompatMod.tmmGuildSkipped) return;
			try
			{
				var pack = LoadedModManager.GetMod<Harmony3StrictCompatMod>().Content;
				var path = Path.Combine(pack.RootDir, "Compat", "Harmony3StrictCompat.Vehicles.dll");
				var addon = Assembly.Load(File.ReadAllBytes(path));
				addon.GetType("Harmony3StrictCompat.VehiclesCompat.TmmMerchantGuild").GetMethod("Run").Invoke(null, null);
				Log.Message("[Harmony3StrictCompat] Too Many Mods Merchant Guild vehicle patches replaced for Vehicle Framework 1.7");
			}
			catch (Exception e)
			{
				Log.Error("[Harmony3StrictCompat] Too Many Mods Merchant Guild vehicle patches not replaced: " + e);
			}
		}
	}

	[StaticConstructorOnStartup]
	static class DedupeEquippableComps
	{
		static DedupeEquippableComps()
		{
			var fixedDefs = new List<string>();
			foreach (var def in DefDatabase<ThingDef>.AllDefs)
			{
				if (def.comps == null || !def.comps.Any(c => c.compClass != typeof(CompEquippable) && typeof(CompEquippable).IsAssignableFrom(c.compClass)))
					continue;
				if (def.comps.RemoveAll(c => c.compClass == typeof(CompEquippable)) > 0)
					fixedDefs.Add(def.defName);
			}
			if (fixedDefs.Count > 0)
				Log.Message($"[Harmony3StrictCompat] dropped duplicate plain CompEquippable from {fixedDefs.Count} weapon(s): {string.Join(", ", fixedDefs)}");
		}
	}

	// Reflection for Harmony3StrictCompatMod.PipeTapDrainCell / PipeTapOverMaxUnstored (VE PipeSystem taps and drains).
	static class PipeTapsAndDrains
	{
		static Type tapComp, drainComp;
		static AccessTools.FieldRef<object, int> tapMax;     // CompConvertToThing.maxHeldThingStackSize (the tap's +/- setting)
		static PropertyInfo netOf, netFree;                  // CompResource.PipeNet, PipeNet.AvailableCapacity
		static FieldInfo netStorages;                        // PipeNet.storages
		static FieldInfo drainRatio;                         // CompProperties_ConvertThingToResource.ratio (resource per item)
		static HashSet<ThingDef> taps, drains;               // filled once defs are loaded

		internal static bool Init()
		{
			tapComp = AccessTools.TypeByName("PipeSystem.CompConvertToThing");
			drainComp = AccessTools.TypeByName("PipeSystem.CompConvertToResource");
			var net = AccessTools.TypeByName("PipeSystem.PipeNet");
			if (tapComp == null || drainComp == null || net == null)
				return false;
			netOf = AccessTools.Property(AccessTools.TypeByName("PipeSystem.CompResource"), "PipeNet");
			netFree = AccessTools.Property(net, "AvailableCapacity");
			netStorages = AccessTools.Field(net, "storages");
			drainRatio = AccessTools.Field(AccessTools.TypeByName("PipeSystem.CompProperties_ConvertThingToResource"), "ratio");
			if (AccessTools.Field(tapComp, "maxHeldThingStackSize") == null || netOf == null || netFree == null || netStorages == null)
			{
				Log.Error("[Harmony3StrictCompat] PipeSystem changed (tap max or PipeNet members missing); tap and drain hauling fix not applied.");
				return false;
			}
			tapMax = AccessTools.FieldRefAccess<int>(tapComp, "maxHeldThingStackSize");
			LongEventHandler.ExecuteWhenFinished(() =>
			{
				bool Has(ThingDef d, Type comp) => d.comps?.Any(c => comp.IsAssignableFrom(c.compClass)) == true;
				var storage = DefDatabase<ThingDef>.AllDefs.Where(d => typeof(Building_Storage).IsAssignableFrom(d.thingClass)).ToList();
				drains = [.. storage.Where(d => Has(d, drainComp))];
				taps = [.. storage.Where(d => Has(d, tapComp))];
			});
			return true;
		}

		internal static bool Watched(ThingDef def, out bool tap)
		{
			tap = taps?.Contains(def) == true;
			return tap || drains?.Contains(def) == true;
		}

		static ThingComp Comp(Building_Storage b, Type type)
		{
			foreach (var c in b.AllComps)
				if (type.IsInstanceOfType(c))
					return c;
			return null;
		}

		internal static int TapMax(Building_Storage tap, int stackLimit) =>
			Comp(tap, tapComp) is { } c ? Math.Min(tapMax(c), stackLimit) : stackLimit;

		internal static bool DrainHasRoom(Building_Storage drain, int needed) => DrainFreeItems(drain) >= Math.Max(needed, 1);

		// How many items the drain's network can still absorb (free tank space / the drain's item ratio); 0 without tanks.
		internal static int DrainFreeItems(Building_Storage drain)
		{
			if (Comp(drain, drainComp) is not { } c || netOf.GetValue(c) is not { } net || netStorages.GetValue(net) is not ICollection { Count: > 0 })
				return 0;
			int ratio = drainRatio != null && c.props != null && drainRatio.DeclaringType.IsInstanceOfType(c.props) ? Math.Max((int)drainRatio.GetValue(c.props), 1) : 1;
			return (int)((float)netFree.GetValue(net) / ratio);
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
			// GetMainBed's fallback (no bed on the pawn's map) picks among all assigned beds with b.Map.IsPlayerHome and
			// b.Map.uniqueID, but an assigned bed can be on no map at all (gravship in flight, stored or minified), and
			// then it throws: "Exception while recalculating WantToSleepWithSpouseOrLover thought state", 10 a session.
			// The lambdas treat a bed with no map as not at home and as the last choice.
			if (Harmony3StrictCompatMod.Target("SensibleBedOwnership.Utility+<>c:<GetMainBed>b__16_0") is { } atHome)
				harmony.Patch(atHome, prefix: new HarmonyMethod(typeof(SboOwnedBedCache), nameof(NoMapNotHome)));
			foreach (var name in new[] { "<GetMainBed>b__16_1", "<GetMainBed>b__16_2" })
				if (Harmony3StrictCompatMod.Target("SensibleBedOwnership.Utility+<>c:" + name) is { } mapOrder)
					harmony.Patch(mapOrder, prefix: new HarmonyMethod(typeof(SboOwnedBedCache), nameof(NoMapLast)));
		}

		static bool NoMapNotHome(Building_Bed b, ref bool __result)
		{
			if (b?.Map != null) return true;
			__result = false;
			return false;
		}

		static bool NoMapLast(Building_Bed b, ref int __result)
		{
			if (b?.Map != null) return true;
			__result = int.MaxValue;
			return false;
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

	// ---- "Do until you have X" bills: count the products once per bill every 600 ticks -----------------------
	// Personal tweak, not a fix. Every time a pawn looks for work at a bench, Bill_Production.ShouldDoNow counts the
	// bill's products: every item of that def on the map for armor and weapons, and Better Workbench Management's
	// postfix adds every colonist's gear ("count equipped"), caravans ("count away") and its extra product filter on
	// top. Circinus 2026-11-20, 29 colonists: 0.45 counts a tick, 87 us each in BWM's postfix alone, 2.7% of frame
	// with Hauler's Dream's postfix and Compositable Loadouts' ShouldDoNow prefix. The answer only matters when it
	// crosses the bill's target, which is rare.
	// The count call inside ShouldDoNow goes through a per-bill cache: one real count (vanilla plus every mod's
	// patches, whatever they are), reused for 600 ticks. Finishing an iteration of any bill drops the entries for
	// that product, so a bill sees a freshly made item at once and nothing is overcrafted because of the cache.
	// What is late by up to 600 ticks: a bill restarting after its product was destroyed, sold, or taken off the
	// map, and a change of the bill's own count options. Bills that count through BWM's extra product filter can
	// make one item too many when a different bill made the counted item. The bill row ("3/5") and the unpause
	// check do not go through ShouldDoNow and stay live. A one-time line reports hits/misses.
	// Loadout Compositing's per-tag bills never reach that call: its ShouldDoNow prefix answers for them through
	// BillUtility.Satisfied, which counts on its own (sampling profile 2026-11-22: 1.8 s of a 91 s tick, all of it
	// the real count). The same call swap in Satisfied sends them through the same cache, with the same lateness;
	// the prefix is Satisfied's only caller. The report line counts those lookups apart.
	// Applied from the mod constructor like the other game-method fixes, not from a static constructor: read at
	// [StaticConstructorOnStartup] time (after other mods patched ShouldDoNow) the fingerprint came out as
	// 0xACA7CD6390EDB9E3 instead of the DLL's 0x91426C66CA668FA7 (2026-11-20; why is not established).
	static class BillCountCache
	{
		const int MaxAgeTicks = 600;

		struct Entry
		{
			public int count, tick;
			public ThingDef product;
		}

		static readonly System.Collections.Concurrent.ConcurrentDictionary<Bill_Production, Entry> cache = new();
		static int hits, misses, perTag;
		static bool reported;

		internal static void Init(Harmony harmony)
		{
			var shouldDoNow = Harmony3StrictCompatMod.Target("RimWorld.Bill_Production:ShouldDoNow");
			if (shouldDoNow == null) return;
			harmony.Patch(shouldDoNow, transpiler: new HarmonyMethod(typeof(BillCountCache), nameof(UseCache)));
			if (Harmony3StrictCompatMod.Target("Inventory.BillUtility:Satisfied") is { } satisfied)
			{
				harmony.Patch(satisfied, transpiler: new HarmonyMethod(typeof(BillCountCache), nameof(UseCache)),
					postfix: new HarmonyMethod(typeof(BillCountCache), nameof(ProbePerTagWanted)));
				pawnsFor = AccessTools.Method("Inventory.LoadoutManager:PawnsFor");
				desiredTarget = AccessTools.Method("Inventory.BillUtility:DesiredTargetCount");
			}
			harmony.Patch(AccessTools.Method(typeof(Bill_Production), nameof(Bill_Production.Notify_IterationCompleted)),
				postfix: new HarmonyMethod(typeof(BillCountCache), nameof(ProductMade)));
			if (Harmony3StrictCompatMod.Target("Verse.Pawn:SpawnSetup") is { } spawnSetup)
				harmony.Patch(spawnSetup, postfix: new HarmonyMethod(typeof(BillCountCache), nameof(ColonistArrived)));
			harmony.Patch(AccessTools.Method(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld)),
				postfix: new HarmonyMethod(typeof(BillCountCache), nameof(Reset)));
		}

		static IEnumerable<CodeInstruction> UseCache(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			var codes = instructions.ToList();
			var live = AccessTools.Method(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountProducts));
			var calls = codes.Where(c => c.Calls(live)).ToList();
			if (calls.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] {original.DeclaringType?.Name}.{original.Name} counts products {calls.Count} times instead of once, bill count cache not applied");
				return codes;
			}
			calls[0].opcode = OpCodes.Call;
			calls[0].operand = AccessTools.Method(typeof(BillCountCache),
				original.DeclaringType == typeof(Bill_Production) ? nameof(Count) : nameof(CountPerTag));
			return codes;
		}

		static int CountPerTag(RecipeWorkerCounter counter, Bill_Production bill)
		{
			perTag++;
			return Count(counter, bill);
		}

		// Same stack shape as the call it replaces: (counter, bill) -> count.
		static int Count(RecipeWorkerCounter counter, Bill_Production bill)
		{
			int now = Find.TickManager.TicksGame;
			if (cache.TryGetValue(bill, out var e) && (uint)(now - e.tick) < MaxAgeTicks)
			{
				hits++;
				return e.count;
			}
			misses++;
			int count = counter.CountProducts(bill);
			if (cache.Count > 4096) cache.Clear();
			cache[bill] = new Entry { count = count, tick = now, product = ProductOf(bill) };
			if (!reported && hits + misses >= 5000)
			{
				reported = true;
				Log.Message($"[Harmony3StrictCompat] Bill count cache: {hits} hits, {misses} real counts, {cache.Count} bills, {perTag} lookups from Loadout Compositing per-tag bills");
			}
			return count;
		}

		static ThingDef ProductOf(Bill_Production bill) => bill.recipe?.products is { Count: > 0 } p ? p[0].thingDef : null;

		static void ProductMade(Bill_Production __instance)
		{
			var product = ProductOf(__instance);
			foreach (var kv in cache)
				if (product == null || kv.Value.product == product)
					cache.TryRemove(kv.Key, out _);
			ProbePerTagMade(__instance);
		}

		static void Reset() => cache.Clear();

		// A colonist (or colony mech) arriving on a map changes what the bills there should count: a gravship crew or
		// a caravan back home brings its worn gear, and a Loadout Compositing per-tag bill's target grows with every
		// tagged pawn on the map at once. Every cached count is dropped so the next look counts live; without this a
		// count up to 600 ticks old could start a craft the returning pawns' own gear already covers (2026-11-23).
		static void ColonistArrived(Pawn __instance, bool respawningAfterLoad)
		{
			if (respawningAfterLoad || cache.IsEmpty || __instance.Faction is not { IsPlayer: true }) return;
			if (!__instance.IsColonist && !__instance.IsColonyMech) return;
			cache.Clear();
		}

		// ---- TEMPORARY probe (2026-11-23): per-tag bills overcrafted by 2-3 after a gravship trip ------------------
		// Logs, for a Loadout Compositing per-tag bill, every time it decides to craft (Satisfied false) with numbers
		// that differ from its last line, and every finished craft: the target, the count the bill used (through the
		// cache), the live count, and the tagged pawns on the bill's map. Remove (source, README row, About bullet)
		// once the overcraft is explained.
		static MethodInfo pawnsFor, desiredTarget;
		static readonly Dictionary<Bill_Production, string> probeLast = new();

		static bool IsPerTag(Bill_Production bill) => bill.repeatMode?.defName == "W_PerTag";

		static string ProbeState(Bill_Production bill)
		{
			int target = desiredTarget != null ? (int)desiredTarget.Invoke(null, [bill]) : -1;
			int live = bill.recipe.WorkerCounter.CountProducts(bill);
			string cached = cache.TryGetValue(bill, out var e) ? $"{e.count} ({Find.TickManager.TicksGame - e.tick} ticks old)" : "none";
			var pawns = pawnsFor?.Invoke(null, [bill]) is IEnumerable<Pawn> ps ? string.Join(", ", ps.Select(p => p.LabelShort)) : "?";
			return $"target {target}, cached count {cached}, live count {live}, tagged pawns on map [{pawns}]";
		}

		static readonly Dictionary<Bill_Production, int> probeTick = new();

		static void ProbePerTagWanted(Bill_Production productionBill, bool __result)
		{
			if (__result || productionBill.suspended || !IsPerTag(productionBill)) return;
			int now = Find.TickManager.TicksGame;
			if (probeTick.TryGetValue(productionBill, out int at) && (uint)(now - at) < 250) return; // the live count is the expensive part
			probeTick[productionBill] = now;
			string state = ProbeState(productionBill);
			if (probeLast.TryGetValue(productionBill, out var last) && last == state) return;
			probeLast[productionBill] = state;
			Log.Message($"[Harmony3StrictCompat] per-tag bill probe: {productionBill.recipe.defName} at {productionBill.billStack?.billGiver} wants to craft: {state}");
		}

		static void ProbePerTagMade(Bill_Production bill)
		{
			if (!IsPerTag(bill)) return;
			probeLast.Remove(bill);
			Log.Message($"[Harmony3StrictCompat] per-tag bill probe: {bill.recipe.defName} at {bill.billStack?.billGiver} finished one: {ProbeState(bill)}");
		}
	}

	// ---- Vanilla Gravship Expanded - Chapter 2: warcomputer search for every player turret ---------------------
	// Its Building_TurretGun.Tick postfix calls WarcomputerHelper.ApplyForcedMissRadiusBuffs for every player
	// turret once every 250 ticks. The method first walks the colony's building list for an active warcomputer and
	// only then looks at the turret: the answer is used for the gauss gun and the gauss howitzer and for nothing
	// else. Sampling profile 2026-11-22: 1.1 s of 5.1 s turret tick time in 3 minutes; 0 s in the next session
	// with this prefix.
	// Prefix: every other turret skips the call, which for it did nothing but compute the unused answer. The two
	// gauss turrets run the original unchanged. A one-time line reports how many calls were skipped.
	static class VgeWarcomputerOnlyForGaussGuns
	{
		static FieldInfo gunField, howitzerField;
		static ThingDef gun, howitzer;
		static bool resolved, reported;
		static int calls, skipped;

		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("VanillaGravshipExpanded2.WarcomputerHelper:ApplyForcedMissRadiusBuffs") is not { } apply) return;
			var defOf = AccessTools.TypeByName("VanillaGravshipExpanded2.InternalDefOf");
			gunField = defOf == null ? null : AccessTools.Field(defOf, "VGE_GaussGun");
			howitzerField = defOf == null ? null : AccessTools.Field(defOf, "VGE_GaussHowitzer");
			if (gunField == null || howitzerField == null)
			{
				Log.Error("[Harmony3StrictCompat] VanillaGravshipExpanded2.InternalDefOf no longer has VGE_GaussGun and VGE_GaussHowitzer: its mod updated, remove or redo this fix");
				return;
			}
			harmony.Patch(apply, prefix: new HarmonyMethod(typeof(VgeWarcomputerOnlyForGaussGuns), nameof(OnlyGaussGuns)));
		}

		static bool OnlyGaussGuns(Building_TurretGun turret)
		{
			if (!resolved)
			{
				// The mod's own def fields, the same objects the original compares against. Filled long before
				// the first turret ticks.
				gun = (ThingDef)gunField.GetValue(null);
				howitzer = (ThingDef)howitzerField.GetValue(null);
				resolved = true;
			}
			var def = turret?.def;
			bool run = def == null || def == gun || def == howitzer;
			calls++;
			if (!run) skipped++;
			if (!reported && calls >= 2000)
			{
				reported = true;
				Log.Message($"[Harmony3StrictCompat] Warcomputer check: skipped for {skipped} of {calls} turret calls (only gauss turrets use it)");
			}
			return run;
		}
	}

	// ---- Quiet Wonders: sleep dim reads the room type of every awake pawn's room -------------------------------
	// QWLife.FindAsleepRooms (setting sleepDim, every 60 ticks) reads room.Role for the room of every humanlike
	// pawn on the map, awake or asleep. Role on a dirty room runs Room.UpdateRoomStatsAndRole, and after a fight
	// the cleanup keeps rooms dirty. Sampling profile 2026-11-24 (262 pawns, hive cleanup): 1.0 s of 28 s main
	// thread, 4% of every frame, almost all of it in that stat update.
	// Prefix with the same result: awake pawns only feed awakeIds, and awakeIds is only subtracted from sleepIds,
	// which holds bedrooms and barracks only. So an awake pawn adds its room ID without the room checks; a pawn
	// asleep in bed still gets the full check. A one-time line reports how many room checks were left out.
	static class QuietWondersSleepDimRooms
	{
		static FieldInfo asleepField, awakeField, sleepField, roomRefField;
		static HashSet<int> asleepRooms, awakeIds, sleepIds;
		static Dictionary<int, Room> roomRef;
		static Func<bool> floorsOn;
		static Func<Thing, int> floorOf;
		static int scans, checks, pawnsSeen;
		static bool reported;

		// Only FieldInfos here: reading QWLife's static fields runs its static constructor, which creates
		// Materials and throws this early in startup (and then breaks QWLife for the whole session).
		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("QuietWonders.QWLife:FindAsleepRooms") is not { } find) return;
			var life = find.DeclaringType;
			asleepField = AccessTools.Field(life, "asleepRooms");
			awakeField = AccessTools.Field(life, "awakeIds");
			sleepField = AccessTools.Field(life, "sleepIds");
			roomRefField = AccessTools.Field(life, "roomRef");
			var floors = AccessTools.TypeByName("QuietWonders.QWLogicalFloors");
			var on = floors == null ? null : AccessTools.PropertyGetter(floors, "On");
			var of = floors == null ? null : AccessTools.Method(floors, "FloorOf", [typeof(Thing)]);
			if (asleepField?.FieldType != typeof(HashSet<int>) || awakeField?.FieldType != typeof(HashSet<int>)
				|| sleepField?.FieldType != typeof(HashSet<int>) || roomRefField?.FieldType != typeof(Dictionary<int, Room>) || on == null || of == null)
			{
				Log.Error("[Harmony3StrictCompat] Quiet Wonders QWLife room sets or QWLogicalFloors.On/FloorOf no longer exist: its mod updated, remove or redo this fix");
				return;
			}
			floorsOn = AccessTools.MethodDelegate<Func<bool>>(on);
			floorOf = AccessTools.MethodDelegate<Func<Thing, int>>(of);
			harmony.Patch(find, prefix: new HarmonyMethod(typeof(QuietWondersSleepDimRooms), nameof(FindAsleepRooms)));
		}

		// Same steps as the original, in the same order, except that the room checks run for sleepers only.
		static bool FindAsleepRooms(Map map)
		{
			if (roomRef == null)
			{
				// QWLife is initialized by now: this runs inside a call to its own static method.
				asleepRooms = (HashSet<int>)asleepField.GetValue(null);
				awakeIds = (HashSet<int>)awakeField.GetValue(null);
				sleepIds = (HashSet<int>)sleepField.GetValue(null);
				roomRef = (Dictionary<int, Room>)roomRefField.GetValue(null);
			}
			asleepRooms.Clear();
			awakeIds.Clear();
			sleepIds.Clear();
			var pawns = map.mapPawns.AllPawnsSpawned;
			for (int i = 0; i < pawns.Count; i++)
			{
				var pawn = pawns[i];
				if (!pawn.RaceProps.Humanlike || (floorsOn() && floorOf(pawn) != 0)) continue;
				pawnsSeen++;
				var room = RegionAndRoomQuery.GetRoom(pawn, RegionType.Set_All);
				if (room == null) continue;
				if (RestUtility.Awake(pawn) || !RestUtility.InBed(pawn))
				{
					awakeIds.Add(room.ID);
					continue;
				}
				checks++;
				if (!room.PsychologicallyOutdoors && (room.Role == RoomRoleDefOf.Bedroom || room.Role == RoomRoleDefOf.Barracks))
				{
					sleepIds.Add(room.ID);
					roomRef[room.ID] = room;
				}
			}
			foreach (int id in sleepIds)
				if (!awakeIds.Contains(id)) asleepRooms.Add(id);
			if (!reported && ++scans >= 300)
			{
				reported = true;
				Log.Message($"[Harmony3StrictCompat] Quiet Wonders sleep dim: {scans} scans checked the room type for {checks} sleeping pawns instead of all {pawnsSeen} humanlike pawns");
			}
			return false;
		}
	}

	// ---- Vanilla Furniture Expanded - Factory: a hopper with inserting off still counts as a better storage -------
	// Its postfix on StoreUtility.IsGoodStoreCell refuses the cell of a Building_FactoryHopper with allowInserting
	// off only when a pawn asks (carrier != null). The haulables list asks without a pawn
	// (StoreUtility.IsInValidBestStorage), so such a hopper keeps every lower-priority stack its filter accepts on
	// the list, and no pawn can ever get a job on them. Haul list probe 2026-11-24: two such hoppers at priority
	// Important kept 982 of 1,007 listed things without a job; every WorkGiver_HaulGeneral turn ran the store
	// search on all of them, 20-37 ms in one tick.
	// IsInValidBestStorage marks its own search; a postfix on IsGoodStoreCell (after the mod's) then refuses the
	// cell of a hopper with inserting off for that search too. Pawn searches and every other pawn-less caller are
	// untouched. A one-time line reports how many list checks it changed.
	static class VfeFactoryNoInsertHopperNotBetterStorage
	{
		[ThreadStatic] static int listChecks;
		static Type hopperType;
		static AccessTools.FieldRef<object, bool> allowInserting;
		static int refused;
		static bool reported;

		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("VanillaFurnitureExpandedFactory.StoreUtility_IsGoodStoreCell_Patch:Postfix") == null) return;
			hopperType = AccessTools.TypeByName("VanillaFurnitureExpandedFactory.Building_FactoryHopper");
			var field = hopperType == null ? null : AccessTools.Field(hopperType, "allowInserting");
			if (field == null || field.FieldType != typeof(bool))
			{
				Log.Error("[Harmony3StrictCompat] VanillaFurnitureExpandedFactory.Building_FactoryHopper.allowInserting no longer exists: its mod updated, remove or redo this fix");
				return;
			}
			allowInserting = AccessTools.FieldRefAccess<object, bool>(field);
			var me = typeof(VfeFactoryNoInsertHopperNotBetterStorage);
			harmony.Patch(AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsInValidBestStorage)),
				prefix: new HarmonyMethod(me, nameof(EnterListCheck)), finalizer: new HarmonyMethod(me, nameof(LeaveListCheck)));
			harmony.Patch(AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell)),
				postfix: new HarmonyMethod(me, nameof(NoInsertHopper)) { priority = Priority.Last });
		}

		// __state pairs the two: the finalizer only undoes a prefix that ran.
		static void EnterListCheck(out bool __state)
		{
			listChecks++;
			__state = true;
		}

		static void LeaveListCheck(bool __state)
		{
			if (__state) listChecks--;
		}

		static void NoInsertHopper(IntVec3 c, Map map, Pawn carrier, ref bool __result)
		{
			if (!__result || carrier != null || listChecks == 0) return;
			var parent = map.haulDestinationManager.SlotGroupParentAt(c);
			if (parent == null || !hopperType.IsInstanceOfType(parent) || allowInserting(parent)) return;
			__result = false;
			if (!reported && ++refused >= 200)
			{
				reported = true;
				Log.Message($"[Harmony3StrictCompat] Factory hoppers with inserting off: {refused} haul-list checks no longer count one as a better storage");
			}
		}
	}

	// ---- WOWGAG's Performance Patch: "fight target reuse" looks up the validator's method on every call ---------
	// Its prefix and postfix on AttackTargetFinder.BestAttackTarget (GppFightTarget.Pre / Post) remember each
	// searcher's last target and, to know whether a later call asks the same question, compare the validator
	// delegate's Method. A turret hands over a new delegate on every search, so the runtime resolves the method
	// from the code address again each time (Delegate.FindMethodHandle). Sampling profiles 2026-11-22: 2.5 s and
	// 3.1 s in 3 minutes, about half of all turret tick time and more than the search the reuse saves.
	// The one Delegate.Method call in each of the two methods goes through a table instead. What the runtime's
	// GetMethodImpl answer depends on is the key (code address, second address, target type, delegate type), so
	// the table returns the same MethodInfo the real call would. Left to the real call: combined delegates and
	// function-pointer wrappers (invocation count not 0), and methods in unloadable code, whose address can be
	// reused. Pre and Post only get here on the main thread (their own IsMain checks), so a plain dictionary is
	// enough. A one-time line reports real lookups against table hits.
	static class WppFightTargetCheapValidatorMethod
	{
		static AccessTools.FieldRef<Delegate, IntPtr> methodPtr, methodPtrAux;
		static AccessTools.FieldRef<MulticastDelegate, IntPtr> invocationCount;
		static readonly Dictionary<(IntPtr, IntPtr, Type, Type), MethodInfo> known = new();
		static int lookups, real;
		static bool reported;

		internal static void Init(Harmony harmony)
		{
			var pre = Harmony3StrictCompatMod.Target("GUIPerformancePatch.GppFightTarget:Pre");
			var post = Harmony3StrictCompatMod.Target("GUIPerformancePatch.GppFightTarget:Post");
			if (pre == null || post == null) return;
			if (AccessTools.Field(typeof(Delegate), "_methodPtr")?.FieldType != typeof(IntPtr)
				|| AccessTools.Field(typeof(Delegate), "_methodPtrAux")?.FieldType != typeof(IntPtr)
				|| AccessTools.Field(typeof(MulticastDelegate), "_invocationCount")?.FieldType != typeof(IntPtr))
			{
				Log.Error("[Harmony3StrictCompat] This runtime's Delegate has no _methodPtr / _methodPtrAux / _invocationCount of type IntPtr: WOWGAG fight target validator lookup left as it is");
				return;
			}
			methodPtr = AccessTools.FieldRefAccess<Delegate, IntPtr>("_methodPtr");
			methodPtrAux = AccessTools.FieldRefAccess<Delegate, IntPtr>("_methodPtrAux");
			invocationCount = AccessTools.FieldRefAccess<MulticastDelegate, IntPtr>("_invocationCount");
			var swap = new HarmonyMethod(typeof(WppFightTargetCheapValidatorMethod), nameof(UseTable));
			harmony.Patch(pre, transpiler: swap);
			harmony.Patch(post, transpiler: swap);
		}

		static IEnumerable<CodeInstruction> UseTable(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			var codes = instructions.ToList();
			var getMethod = AccessTools.PropertyGetter(typeof(Delegate), nameof(Delegate.Method));
			var calls = codes.Where(c => c.Calls(getMethod)).ToList();
			if (calls.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] GppFightTarget.{original.Name} reads Delegate.Method {calls.Count} times instead of once, validator lookup left as it is");
				return codes;
			}
			calls[0].opcode = OpCodes.Call;
			calls[0].operand = AccessTools.Method(typeof(WppFightTargetCheapValidatorMethod), nameof(MethodOf));
			return codes;
		}

		// Same stack shape as the call it replaces: (delegate) -> MethodInfo.
		static MethodInfo MethodOf(Delegate d)
		{
			if (d is not MulticastDelegate m || invocationCount(m) != IntPtr.Zero) return d.Method;
			var key = (methodPtr(d), methodPtrAux(d), d.Target?.GetType(), d.GetType());
			lookups++;
			if (!known.TryGetValue(key, out var method))
			{
				real++;
				method = d.Method;
				if (method.DeclaringType is { } type && !type.Assembly.IsCollectible) known[key] = method;
			}
			if (!reported && lookups >= 20000)
			{
				reported = true;
				Log.Message($"[Harmony3StrictCompat] Fight target validator method: {real} real lookups of {lookups}, {known.Count} validators known");
			}
			return method;
		}
	}

	// ---- Hospitality / Smarter Visitors: visitor questions wait as letters (personal tweak) ----------------------
	// Two questions open as modal windows that pause the game: Hospitality's "visitors are afraid to come (temperature
	// ...), let them in?" (IncidentWorker_VisitorGroup.AskForSafety -> ShowAskMayComeDialog, a Dialog_NodeTree) and
	// Smarter Visitors' "visitors are afraid to leave" (a Dialog_MessageBox from its stay-timer prefix). Stylish
	// Dialogs' per-category "open on arrival" settings only cover letters, so these ignored them. When Stylish
	// Dialogs' "Notice" category is set not to open by itself, both are sent as a letter of that category instead
	// (LetterDef H3SC_VisitorQuestion, classified Notice); clicking it opens the same question with the same buttons.
	// The game keeps running meanwhile:
	// - arriving: the group stays off the map until answered; answers call Hospitality's own SpawnGroup / MaybeRevisit
	//   / RefuseGuestsUntilWeHaveBeds / askForSafety, the same as its dialog's closures. Dismissed or timed out (one
	//   day): they do not come (no revisit roll).
	// - leaving: SvOnlyStayTimer holds the stay timer while the letter waits. Allow / Wait a little add 60000 / 2500
	//   ticks to the timer and to Smarter Visitors' delay record, as its buttons do. Deny (which also drops that
	//   record), dismiss or timeout end the hold, and the expired timer sends them home on the next tick.
	// The letter keeps what it needs (incident, parms, map / lord) in the save, so it survives a reload.
	public class VisitorQuestionLetter : ChoiceLetter
	{
		public bool leaving;
		public IncidentDef incident;
		public IncidentParms parms;
		public Map map;
		public Lord lord;

		static FieldInfo sdSettings;
		static FieldInfo sdOpenNotice;
		static MethodInfo spawnGroup, maybeRevisit, refuseUntilBeds, spawnDirection, setDelayValue;
		static Type guestBedType, hospMapComp;
		static FieldInfo askForSafety, svDelays;
		static Type svApproved;
		static AccessTools.FieldRef<Trigger_TicksPassed, int> duration = AccessTools.FieldRefAccess<Trigger_TicksPassed, int>("duration");

		internal static void Init(Harmony harmony)
		{
			var sdMod = AccessTools.TypeByName("StylishDialogs.StylishDialogsMod");
			if (sdMod == null || Harmony3StrictCompatMod.Target("StylishDialogs.CollageSettings:OpensOnArrival") == null) return;
			sdOpenNotice = AccessTools.Field(AccessTools.TypeByName("StylishDialogs.CollageSettings"), "openNotice");
			var settingsField = AccessTools.Field(sdMod, "Settings");
			if (sdOpenNotice == null || settingsField == null)
			{
				Log.Error("[Harmony3StrictCompat] Stylish Dialogs Settings / openNotice changed: visitor questions stay modal windows");
				return;
			}
			sdSettings = settingsField; // read on each check: the mod constructor replaces the instance
			var ask = Harmony3StrictCompatMod.Target("Hospitality.IncidentWorker_VisitorGroup:AskForSafety");
			spawnGroup = Harmony3StrictCompatMod.Target("Hospitality.IncidentWorker_VisitorGroup:SpawnGroup");
			maybeRevisit = Harmony3StrictCompatMod.Target("Hospitality.IncidentWorker_VisitorGroup:MaybeRevisit");
			spawnDirection = Harmony3StrictCompatMod.Target("Hospitality.IncidentWorker_VisitorGroup:GetSpawnDirection");
			refuseUntilBeds = Harmony3StrictCompatMod.Target("Hospitality.Hospitality_MapComponent:RefuseGuestsUntilWeHaveBeds");
			guestBedType = AccessTools.TypeByName("Hospitality.Building_GuestBed");
			hospMapComp = AccessTools.TypeByName("Hospitality.Hospitality_MapComponent");
			askForSafety = AccessTools.Field(hospMapComp, "askForSafety");
			if (ask != null && spawnGroup != null && maybeRevisit != null && spawnDirection != null && refuseUntilBeds != null
				&& guestBedType != null && askForSafety != null)
				harmony.Patch(ask, prefix: new HarmonyMethod(typeof(VisitorQuestionLetter), nameof(ArrivalAsLetter)));
			else if (ask != null)
				Log.Error("[Harmony3StrictCompat] Hospitality visitor group members changed: the arrival question stays a modal window");
			setDelayValue = Harmony3StrictCompatMod.Target("SmarterVisitors.SmarterVisitors:SetDelayValue");
			svApproved = AccessTools.TypeByName("SmarterVisitors.GameComponent_ApprovedLords");
			svDelays = AccessTools.Field(svApproved, "LordDelaysDictionary");
		}

		// Stylish Dialogs present and its "Notice" letters set not to open by themselves.
		internal static bool AsLetter => sdSettings?.GetValue(null) is { } s && !(bool)sdOpenNotice.GetValue(s);

		internal static bool Holds(Lord lord)
		{
			var letters = Find.LetterStack.LettersListForReading;
			for (int i = 0; i < letters.Count; i++)
				if (letters[i] is VisitorQuestionLetter q && q.leaving && q.lord == lord)
				{
					if (!q.TimeoutPassed) return true;
					Find.LetterStack.RemoveLetter(q);
					return false;
				}
			return false;
		}

		static bool ArrivalAsLetter(IncidentWorker __instance, IncidentParms parms, Map map, string reasons)
		{
			if (!AsLetter) return true;
			var dir = (Direction8Way)spawnDirection.Invoke(null, new object[] { map, parms.spawnCenter });
			string title = "VisitorsArrivedTitle".Translate(new NamedArgument(map.Parent, "WORLDOBJECT"), dir.LabelShort(), new NamedArgument(parms.faction, "FACTION"));
			var letter = Make(title, "VisitorsArrivedDesc".Translate(parms.faction, reasons), parms.faction, new LookTargets(parms.spawnCenter, map));
			letter.incident = __instance.def;
			letter.parms = parms;
			letter.map = map;
			letter.radioMode = true;
			Find.LetterStack.ReceiveLetter(letter);
			return false;
		}

		internal static void SendLeaving(string title, TaggedString text, Lord lord)
		{
			var letter = Make(title, text, lord.faction, new LookTargets(lord.ownedPawns));
			letter.leaving = true;
			letter.lord = lord;
			Find.LetterStack.ReceiveLetter(letter);
		}

		static VisitorQuestionLetter Make(string title, TaggedString text, Faction faction, LookTargets targets)
		{
			var def = DefDatabase<LetterDef>.GetNamed("H3SC_VisitorQuestion");
			var letter = (VisitorQuestionLetter)LetterMaker.MakeLetter(title, text, def, targets, faction);
			letter.title = title;
			letter.StartTimeout(GenDate.TicksPerDay);
			return letter;
		}

		// Vanilla opens a timed letter on its last tick; this one just goes away.
		public override bool ShouldAutomaticallyOpenLetter => false;

		public override IEnumerable<DiaOption> Choices
		{
			get
			{
				if (ArchivedOnly || TimeoutPassed)
				{
					yield return Option_Close;
					yield break;
				}
				if (leaving)
				{
					if (lord?.Map == null || !lord.Map.lordManager.lords.Contains(lord))
					{
						yield return Option_Close;
						yield break;
					}
					yield return Answer("SV.Deny".Translate(), ForgetDelays);
					yield return Answer("SV.WaitALittle".Translate(), () => Delay(2500));
					yield return Answer("SV.Allow".Translate(), () => Delay(60000));
				}
				else
				{
					if (incident?.Worker == null || parms?.faction == null || map == null || !Find.Maps.Contains(map)
						|| parms.faction.defeated || parms.faction.HostileTo(Faction.OfPlayer))
					{
						yield return Option_Close;
						yield break;
					}
					yield return Answer("VisitorsArrivedAccept".Translate(), Spawn);
					yield return Answer("VisitorsArrivedRefuse".Translate(), Refuse);
					if (!map.listerBuildings.allBuildingsColonist.Any(guestBedType.IsInstanceOfType))
						yield return Answer("VisitorsArrivedRefuseUntilBeds".Translate(), () =>
						{
							refuseUntilBeds.Invoke(null, new object[] { map });
							Refuse();
						});
					yield return Answer("VisitorsArrivedDontAskAgain".Translate(), () =>
					{
						Spawn();
						if (map.components.FirstOrDefault(hospMapComp.IsInstanceOfType) is { } comp) askForSafety.SetValue(comp, false);
					});
				}
				yield return Option_Postpone;
			}
		}

		DiaOption Answer(string label, Action action) => new DiaOption(label)
		{
			action = () =>
			{
				try { action?.Invoke(); }
				finally { Find.LetterStack.RemoveLetter(this); }
			},
			resolveTree = true
		};

		void Spawn() => spawnGroup.Invoke(incident.Worker, new object[] { parms, map });

		void Refuse() => maybeRevisit.Invoke(null, new object[] { parms, map, new FloatRange(2f, 5f) });

		// Smarter Visitors' Deny also drops the group's delay record, or the timer would grow by it again.
		void ForgetDelays()
		{
			if (svDelays != null && Current.Game.components.FirstOrDefault(svApproved.IsInstanceOfType) is { } comp)
				(svDelays.GetValue(comp) as IDictionary)?.Remove(lord);
		}

		void Delay(int ticks)
		{
			foreach (var transition in lord.Graph.transitions)
			{
				if (!transition.sources.Contains(lord.CurLordToil)) continue;
				foreach (var trigger in transition.triggers)
					if (Harmony3StrictCompatMod.svStayTimer.IsInstanceOfType(trigger))
					{
						duration((Trigger_TicksPassed)trigger) += ticks;
						setDelayValue?.Invoke(null, new object[] { lord, ticks });
						return;
					}
			}
		}

		// Answered or expired: the archived copy only offers Close, so drop the references (a lord or map that is gone
		// later would otherwise log unresolved references on every load).
		public override void Removed()
		{
			base.Removed();
			Forget();
		}

		void Forget()
		{
			parms = null;
			map = null;
			lord = null;
		}

		public override void ExposeData()
		{
			if (Scribe.mode == LoadSaveMode.Saving && TimeoutPassed) Forget();
			base.ExposeData();
			Scribe_Values.Look(ref leaving, "leaving");
			Scribe_Defs.Look(ref incident, "incident");
			Scribe_Deep.Look(ref parms, "parms");
			Scribe_References.Look(ref map, "map");
			Scribe_References.Look(ref lord, "lord");
		}
	}

	// ---- Vanilla: two hot paths in the tick, and tick pacing (personal tweak) ------------------------------------
	// Found with a sampling profile (dotnet-trace, 20 s at speed 3, 30 colonists, about 80 mechs and 200 animals,
	// 2026-11-20). Shares are of main-thread time; the tick as a whole was 76%.
	// 1. GenTypes.IsSealedWithCache, 29%: ThingWithComps.GetComp<T> calls it whenever a thing with three or more
	//    comps has no comp of exactly type T. It takes a lock around a dictionary on every call, and on this runtime
	//    that lock always goes through Monitor.Enter_Slowpath. Type.IsSealed is a flag read on CoreCLR (the cache
	//    dates from Mono), so the body becomes just that: same answer, no lock, no dictionary.
	// 2. ReachabilityCache.ClearForHostile, 27%: every door that opens or closes walks the whole reachability cache
	//    and asks pawn.HostileTo(door) once per cached entry, although many entries belong to the same pawn and
	//    many doors move in the same tick. For a door with a faction the answer depends only on the pawn and that
	//    faction, so it is asked once per pawn per faction per tick and remembered. A pawn that turns hostile in
	//    the middle of a tick is seen from the next tick on. Other callers and doors without a faction go to the
	//    real check. A one-time line reports lookups against real checks.
	// 3. Tick pacing. TickManagerUpdate runs at most "speed multiplier x 2" ticks in one frame (6 at 3x) and throws
	//    away whatever real time is left over, so ticks per second can never be more than 6 x frames per second:
	//    180 TPS needs 30 FPS in every single frame, and a frame that draws for 12 ms leaves 3.5 ms per tick. The
	//    limit becomes "x 4" (12 at 3x), so a slow frame is made up in the next one and frame rate is traded for
	//    tick rate. The 45 ms per-frame tick budget (the game's 22 FPS floor) is untouched. The first six 20 s
	//    windows at 3x in a session each log one line: TPS, FPS, mean tick time and how often each limit was hit.
	static class TickHotPaths
	{
		static readonly Dictionary<Thing, bool> hostile = new();
		static Faction hostileFaction;
		static int hostileTick = -1, lookups, checks;
		const float TicksPerFrameFactor = 4f;
		static readonly AccessTools.FieldRef<TickManager, System.Diagnostics.Stopwatch> tickClock =
			AccessTools.FieldRefAccess<TickManager, System.Diagnostics.Stopwatch>("clock");
		static readonly System.Diagnostics.Stopwatch window = new();
		static int frames, ticks, pastOldLimit, atNewLimit, pastBudget, linesLeft = 6;
		static double tickMs;
		static bool reported;

		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("Verse.GenTypes:IsSealedWithCache") is { } isSealed)
				harmony.Patch(isSealed, transpiler: new HarmonyMethod(typeof(TickHotPaths), nameof(NoLock)));
			if (Harmony3StrictCompatMod.Target("Verse.ReachabilityCache:ClearForHostile") is { } clear)
				harmony.Patch(clear, transpiler: new HarmonyMethod(typeof(TickHotPaths), nameof(AskOncePerPawn)));
			if (Harmony3StrictCompatMod.Target("Verse.TickManager:TickManagerUpdate") is { } update)
				harmony.Patch(update, postfix: new HarmonyMethod(typeof(TickHotPaths), nameof(Pacing)),
					transpiler: new HarmonyMethod(typeof(TickHotPaths), nameof(MoreTicksPerFrame)));
		}

		static IEnumerable<CodeInstruction> NoLock(IEnumerable<CodeInstruction> instructions) => new[]
		{
			new CodeInstruction(OpCodes.Ldarg_0),
			new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Type), nameof(Type.IsSealed))),
			new CodeInstruction(OpCodes.Ret),
		};

		static IEnumerable<CodeInstruction> AskOncePerPawn(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var live = AccessTools.Method(typeof(GenHostility), nameof(GenHostility.HostileTo), new[] { typeof(Thing), typeof(Thing) });
			var calls = codes.Where(c => c.Calls(live)).ToList();
			if (calls.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] ReachabilityCache.ClearForHostile checks hostility {calls.Count} times instead of once, door hostility memo not applied");
				return codes;
			}
			calls[0].operand = AccessTools.Method(typeof(TickHotPaths), nameof(Hostile));
			return codes;
		}

		static IEnumerable<CodeInstruction> MoreTicksPerFrame(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var twos = codes.Where(c => c.opcode == OpCodes.Ldc_R4 && c.operand is float f && f == 2f).ToList();
			if (twos.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] TickManager.TickManagerUpdate has {twos.Count} ticks-per-frame factors instead of one, tick pacing not changed");
				return codes;
			}
			twos[0].operand = TicksPerFrameFactor;
			return codes;
		}

		static void Pacing(TickManager __instance)
		{
			if (linesLeft <= 0) return;
			if (__instance.Paused || __instance.TickRateMultiplier != 3f)
			{
				window.Reset();
				return;
			}
			if (!window.IsRunning)
			{
				frames = ticks = pastOldLimit = atNewLimit = pastBudget = 0;
				tickMs = 0;
				window.Restart();
				return;
			}
			int n = __instance.TicksThisFrame;
			double ms = tickClock(__instance).Elapsed.TotalMilliseconds;
			frames++;
			ticks += n;
			tickMs += ms;
			if (n > 6) pastOldLimit++;
			if (n >= 3 * TicksPerFrameFactor) atNewLimit++;
			if (ms > 45.454544) pastBudget++;
			double s = window.Elapsed.TotalSeconds;
			if (s < 20) return;
			Log.Message($"[Harmony3StrictCompat] Tick pacing at 3x over {s:F0} s: {ticks / s:F0} TPS of 180, {frames / s:F0} FPS, mean tick {tickMs / Math.Max(1, ticks):F1} ms; frames past the old 6-tick limit {100.0 * pastOldLimit / frames:F0}%, at the new limit {100.0 * atNewLimit / frames:F0}%, past the 45 ms tick budget {100.0 * pastBudget / frames:F0}%");
			linesLeft--;
			window.Reset();
		}

		// Same stack shape as the call it replaces: (pawn, thing) -> hostile.
		internal static bool Hostile(Thing pawn, Thing to)
		{
			if (to is not Building_Door || to.Destroyed || to.Faction is not { } faction) return pawn.HostileTo(to);
			int now = Find.TickManager.TicksGame;
			if (now != hostileTick || faction != hostileFaction)
			{
				hostile.Clear();
				hostileTick = now;
				hostileFaction = faction;
			}
			lookups++;
			if (!hostile.TryGetValue(pawn, out bool result))
			{
				checks++;
				hostile[pawn] = result = pawn.HostileTo(to);
			}
			if (!reported && lookups >= 200000)
			{
				reported = true;
				Log.Message($"[Harmony3StrictCompat] Door hostility memo: {lookups} lookups, {checks} real checks");
			}
			return result;
		}
	}

	// ---- Vanilla: every door that opens or closes walks the whole reachability cache ---------------------------
	// ReachabilityCache.ClearForHostile visits every cached entry and drops those whose pawn is hostile to the
	// door. DPA counts 2026-11-24 (calm colony, 262 pawns): 383 door events in 1,200 ticks, about 51,500 entries
	// walked each, 0.15 ms per tick (3.6% of the tick) with the per-entry hostility memo above already in place.
	// The entries belong to a few hundred pawns at most. A count of entries per pawn is kept beside each cache
	// (AddCachedResult, Clear, ClearFor); a door event asks hostility once per pawn that has entries. None
	// hostile: the walk would remove nothing and is skipped. Some hostile (raid, hostile prisoners): the game's
	// own walk runs as before.
	// Cross-check: the first 300 skips and every 256th after still run the walk and compare the entry count; a
	// walk that removes more than the counts say turns the shortcut off for the session, one that removes fewer
	// (another mod took entries out directly) falls back to the full walk until the cache's next Clear.
	static class DoorReachCacheWalk
	{
		sealed class Index
		{
			public readonly Dictionary<Pawn, int> entries = new();
			public bool complete; // false: the cache holds entries that were never counted
		}

		static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ReachabilityCache, Index> indexes = new();
		static readonly List<Pawn> hostileNow = new();
		static bool enabled = true, reported;
		static int events, skipped, checkedSkips, recounts;
		static long entriesNotWalked, pawnsAsked;

		internal static void Init(Harmony harmony)
		{
			var clearHostile = AccessTools.DeclaredMethod(typeof(ReachabilityCache), nameof(ReachabilityCache.ClearForHostile));
			if (Harmony3StrictCompatMod.Target("Verse.ReachabilityCache:AddCachedResult") is not { } add
				|| Harmony3StrictCompatMod.Target("Verse.ReachabilityCache:Clear") is not { } clear
				|| Harmony3StrictCompatMod.Target("Verse.ReachabilityCache:ClearFor") is not { } clearFor || clearHostile == null) return;
			var me = typeof(DoorReachCacheWalk);
			harmony.Patch(add, prefix: new HarmonyMethod(me, nameof(BeforeAdd)), postfix: new HarmonyMethod(me, nameof(AfterAdd)));
			harmony.Patch(clear, postfix: new HarmonyMethod(me, nameof(AfterClear)));
			harmony.Patch(clearFor, postfix: new HarmonyMethod(me, nameof(AfterClearFor)));
			harmony.Patch(clearHostile, prefix: new HarmonyMethod(me, nameof(BeforeDoorWalk)), postfix: new HarmonyMethod(me, nameof(AfterDoorWalk)));
		}

		static void BeforeAdd(ReachabilityCache __instance, out int __state) => __state = __instance.Count;

		// The original adds only a key it does not hold yet, so the count tells whether an entry was added.
		static void AfterAdd(ReachabilityCache __instance, TraverseParms traverseParams, int __state)
		{
			if (!enabled || __instance.Count <= __state) return;
			if (!indexes.TryGetValue(__instance, out var index))
				indexes.Add(__instance, index = new Index { complete = __state == 0 });
			var pawn = traverseParams.pawn;
			if (pawn == null) return;
			index.entries.TryGetValue(pawn, out int n);
			index.entries[pawn] = n + 1;
		}

		static void AfterClear(ReachabilityCache __instance)
		{
			if (!indexes.TryGetValue(__instance, out var index)) return;
			index.entries.Clear();
			index.complete = true;
		}

		static void AfterClearFor(ReachabilityCache __instance, Pawn p)
		{
			if (p != null && indexes.TryGetValue(__instance, out var index)) index.entries.Remove(p);
		}

		// __state: the entry count the walk must leave behind, or -1 when there is nothing to compare.
		static bool BeforeDoorWalk(ReachabilityCache __instance, Thing hostileTo, out int __state)
		{
			__state = -1;
			hostileNow.Clear();
			if (!enabled || !indexes.TryGetValue(__instance, out var index) || !index.complete) return true;
			events++;
			int toRemove = 0;
			foreach (var kv in index.entries)
				if (TickHotPaths.Hostile(kv.Key, hostileTo))
				{
					hostileNow.Add(kv.Key);
					toRemove += kv.Value;
				}
			pawnsAsked += index.entries.Count;
			if (hostileNow.Count == 0)
			{
				skipped++;
				if (checkedSkips >= 300 && (skipped & 255) != 0)
				{
					entriesNotWalked += __instance.Count;
					if (!reported && events >= 300)
					{
						reported = true;
						Log.Message($"[Harmony3StrictCompat] Door reach-cache walk: {skipped} of {events} door events needed no walk (no cached pawn hostile to the door); {pawnsAsked} pawns asked, {entriesNotWalked} entries not walked so far, {checkedSkips} skips cross-checked against the full walk, {recounts} recounts");
					}
					return false;
				}
				checkedSkips++;
			}
			__state = __instance.Count - toRemove;
			return true;
		}

		static void AfterDoorWalk(ReachabilityCache __instance, int __state)
		{
			if (__state < 0 || !indexes.TryGetValue(__instance, out var index)) return;
			foreach (var pawn in hostileNow) index.entries.Remove(pawn);
			hostileNow.Clear();
			int left = __instance.Count;
			if (left == __state) return;
			if (left < __state)
			{
				enabled = false;
				Log.Error($"[Harmony3StrictCompat] Door reach-cache walk: the full walk removed {__state - left} entries more than the per-pawn counts hold, shortcut turned off for this session");
				return;
			}
			index.complete = false;
			if (recounts++ == 0)
				Log.Warning($"[Harmony3StrictCompat] Door reach-cache walk: the per-pawn counts were {left - __state} entries too high (something removed entries directly); full walk until the cache is cleared next");
		}
	}

	// ---- Diagnostic: exact time per phase of the pawn tick ----------------------------------------------------
	// Off unless a file named "ticktrace.on" sits in the mod folder at startup. Then every game method that
	// Pawn.Tick and Pawn.TickInterval call directly (property getters excluded), those two, DoSingleTick and
	// TickList.Tick get a timestamp at entry and before each return. After every 3600 ticks, three times per
	// session, one log entry lists microseconds and calls per game tick for each, largest first. A sampling
	// profile cannot give this: its samples land on safe points and smear time onto the next lock or copy.
	// The timestamps wrap the method body only, so prefixes and postfixes other mods put on a timed method count
	// toward its caller. Each timed call costs about 50 ns itself; multiply by the call count to subtract it.
	static class TickTrace
	{
		static readonly Dictionary<MethodBase, int> index = new();
		static readonly List<string> names = new();
		static long[] time, started;
		static int[] calls;
		static int ticksSeen, reportsLeft = 3;

		internal static void Init(Harmony harmony, string modRoot)
		{
			var flag = Path.Combine(modRoot, "ticktrace.on");
			if (!File.Exists(flag)) return;
			string mode = File.ReadAllText(flag);
			// "reach" = the work search probe below instead of the tick timing.
			if (mode.Contains("reach"))
			{
				ReachProbe.Init(harmony);
				return;
			}
			// "haul" = the haulables list probe below instead of the tick timing.
			if (mode.Contains("haul"))
			{
				HaulListProbe.Init(harmony);
				return;
			}
			var doSingleTick = AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick));
			// The file's text picks whose direct callees are timed: "stages" = the tick's stages (few calls a
			// tick, next to no overhead), anything else = the two pawn tick methods.
			MethodBase[] scan = mode.Contains("stages")
				? new MethodBase[] { doSingleTick, AccessTools.Method(typeof(Map), nameof(Map.MapPreTick)),
					AccessTools.Method(typeof(Map), nameof(Map.MapPostTick)), AccessTools.Method(typeof(World), nameof(World.WorldTick)) }
				: new MethodBase[] { AccessTools.Method(typeof(Pawn), "Tick"), AccessTools.Method(typeof(Pawn), "TickInterval") };
			var targets = scan.Append(doSingleTick).Append(AccessTools.Method(typeof(TickList), nameof(TickList.Tick))).Distinct().ToList();
			foreach (var root in scan)
				foreach (var c in PatchProcessor.GetOriginalInstructions(root))
					if (c.operand is MethodInfo m && m.DeclaringType?.Assembly == typeof(Pawn).Assembly && !m.IsSpecialName && !m.IsAbstract
						&& !m.IsGenericMethod && !m.DeclaringType.IsGenericType && m.GetMethodBody() != null && !targets.Contains(m))
						targets.Add(m);
			foreach (var m in targets)
			{
				index[m] = names.Count;
				names.Add($"{m.DeclaringType.Name}.{m.Name}");
			}
			time = new long[names.Count];
			started = new long[names.Count];
			calls = new int[names.Count];
			var wrap = new HarmonyMethod(typeof(TickTrace), nameof(Wrap));
			foreach (var m in targets)
			{
				try { harmony.Patch(m, transpiler: wrap); }
				catch (Exception e) { Log.Warning($"[Harmony3StrictCompat] tick trace: cannot time {names[index[m]]}: {e.Message}"); }
			}
			harmony.Patch(doSingleTick, postfix: new HarmonyMethod(typeof(TickTrace), nameof(Report)));
			Log.Message($"[Harmony3StrictCompat] tick trace on: {names.Count} methods timed (remove ticktrace.on from the mod folder to turn off)");
		}

		static IEnumerable<CodeInstruction> Wrap(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			int i = index[original];
			yield return new CodeInstruction(OpCodes.Ldc_I4, i);
			yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TickTrace), nameof(Enter)));
			foreach (var c in instructions)
			{
				if (c.opcode == OpCodes.Ret)
				{
					yield return new CodeInstruction(OpCodes.Ldc_I4, i) { labels = c.labels };
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TickTrace), nameof(Exit)));
					c.labels = new List<Label>();
				}
				yield return c;
			}
		}

		static void Enter(int i) => started[i] = System.Diagnostics.Stopwatch.GetTimestamp();

		static void Exit(int i)
		{
			time[i] += System.Diagnostics.Stopwatch.GetTimestamp() - started[i];
			calls[i]++;
		}

		static void Report()
		{
			if (reportsLeft <= 0 || ++ticksSeen < 3600) return;
			double us = 1e6 / System.Diagnostics.Stopwatch.Frequency / ticksSeen;
			var sb = new StringBuilder($"[Harmony3StrictCompat] tick trace over {ticksSeen} ticks (microseconds per tick, calls per tick):");
			foreach (int i in Enumerable.Range(0, names.Count).OrderByDescending(i => time[i]))
				if (calls[i] > 0) sb.Append($"\n  {time[i] * us,8:F0}  {(double)calls[i] / ticksSeen,8:F1}  {names[i]}");
			Log.Message(sb.ToString());
			Array.Clear(time, 0, time.Length);
			Array.Clear(calls, 0, calls.Length);
			ticksSeen = 0;
			reportsLeft--;
		}
	}

	// ---- Diagnostic: what sits in the haulables list and why a hauler gets no job on it ------------------------
	// Off unless "ticktrace.on" in the mod folder contains the word "haul" at startup (it then replaces the tick
	// timing above). DPA counts 2026-11-24: a WorkGiver_HaulGeneral turn ran the store search on 800-1,100 listed
	// things and nearly all failed, 20-37 ms per turn; which things and why was not visible.
	// 600 and 4200 ticks after startup, one log entry per player home map: the list grouped by def, with for each
	// thing whether it lies outside storage or in storage that refuses it, whether a destination exists without a
	// pawn (the test that put it on the list), whether a higher-priority storage accepts its kind at all, and what
	// the game's own checks answer for up to three colonists with hauling enabled (table = the first of them).
	// It calls HaulAIUtility.HaulToStorageJob like a work search does, so it costs about one failed haul search per
	// colonist per report and nothing in between.
	static class HaulListProbe
	{
		sealed class Row
		{
			public int count, loose, ownRefuses, job, noJob, cannotHaul, noDestNoPawn, higherAccepts, findFails, killedAfterFind, nonSlotDest;
			public string sample;
		}

		static int ticksSeen;

		internal static void Init(Harmony harmony)
		{
			harmony.Patch(AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick)), postfix: new HarmonyMethod(typeof(HaulListProbe), nameof(Tick)));
			Log.Message("[Harmony3StrictCompat] haul list probe on: reports 600 and 4200 ticks after startup (remove ticktrace.on from the mod folder to turn off)");
		}

		static void Tick()
		{
			int n = ++ticksSeen;
			if (n != 600 && n != 4200) return;
			try
			{
				foreach (var map in Find.Maps)
					if (map.IsPlayerHome) Report(map);
			}
			catch (Exception e) { Log.Warning($"[Harmony3StrictCompat] haul list probe failed: {e}"); }
		}

		static string Name(IHaulDestination dest) =>
			dest == null ? "none" : dest is ISlotGroupParent parent ? $"{parent.SlotYielderLabel()} [{dest.GetStoreSettings()?.Priority}]" : $"{dest} (no cells)";

		static void Report(Map map)
		{
			long start = System.Diagnostics.Stopwatch.GetTimestamp();
			var things = map.listerHaulables.ThingsPotentiallyNeedingHauling().ToList();
			var haulers = map.mapPawns.FreeColonistsSpawned
				.Where(p => !p.Downed && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)).Take(3).ToList();
			var groups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			var rows = new Dictionary<ThingDef, Row>();
			var perPawn = new int[haulers.Count, 3]; // job, no job, cannot haul
			foreach (var t in things)
			{
				if (!rows.TryGetValue(t.def, out var r)) rows[t.def] = r = new Row();
				r.count++;
				var own = t.GetSlotGroup();
				if (own == null) r.loose++;
				else if (!own.parent.Accepts(t)) r.ownRefuses++;
				var cur = StoreUtility.CurrentStoragePriorityOf(t);
				// The test that keeps the thing on the list: a better place exists when no pawn is asked.
				bool free = StoreUtility.TryFindBestBetterStorageFor(t, null, map, cur, Faction.OfPlayer, out var freeCell, out var freeDest, false);
				if (!free) r.noDestNoPawn++;
				else if (freeDest is not ISlotGroupParent) r.nonSlotDest++;
				foreach (var g in groups)
					if (g.Settings.Priority > cur && g.parent.Accepts(t)) { r.higherAccepts++; break; }
				for (int i = 0; i < haulers.Count; i++)
				{
					int outcome = !HaulAIUtility.PawnCanAutomaticallyHaulFast(haulers[i], t, false) ? 2
						: HaulAIUtility.HaulToStorageJob(haulers[i], t, false) != null ? 0 : 1;
					perPawn[i, outcome]++;
					if (i != 0) continue;
					if (outcome == 0) r.job++;
					else if (outcome == 2) r.cannotHaul++;
					else
					{
						r.noJob++;
						// Same search the job builder runs, with the pawn: no place found, or found and the job dropped after.
						bool found = StoreUtility.TryFindBestBetterStorageFor(t, haulers[0], map, cur, Faction.OfPlayer, out var cell, out var dest, true);
						if (found) r.killedAfterFind++; else r.findFails++;
						r.sample ??= $"x{t.stackCount} at {t.Position} in {(own == null ? "no storage" : Name(own.parent))}; without pawn: {(free ? $"{Name(freeDest)} {freeCell}" : "none")}; with pawn: {(found ? $"{Name(dest)} {cell}" : "none")}";
					}
				}
			}
			var sb = new StringBuilder($"[Harmony3StrictCompat] haul list probe, {map}: {things.Count} listed things of {rows.Count} kinds, {groups.Count} storage groups");
			for (int i = 0; i < haulers.Count; i++)
				sb.Append($"\n  {haulers[i].LabelShort}: job on {perPawn[i, 0]}, no job on {perPawn[i, 1]}, cannot haul (reserved, unreachable, forbidden) {perPawn[i, 2]}");
			sb.Append($"\n  no job for {haulers.FirstOrDefault()?.LabelShort}: search with pawn finds no place {rows.Values.Sum(r => r.findFails)}, place found but job dropped afterwards {rows.Values.Sum(r => r.killedAfterFind)}; place without pawn is not a cell storage for {rows.Values.Sum(r => r.nonSlotDest)}");
			sb.Append("\n  count  loose  ownRefuses  noDestWithoutPawn  higherAccepts  job  noJob  findFails  droppedAfterFind  cannotHaul  def | one no-job example");
			foreach (var kv in rows.OrderByDescending(kv => kv.Value.noJob + kv.Value.cannotHaul).ThenByDescending(kv => kv.Value.count).Take(30))
			{
				var r = kv.Value;
				sb.Append($"\n  {r.count,5}  {r.loose,5}  {r.ownRefuses,10}  {r.noDestNoPawn,17}  {r.higherAccepts,13}  {r.job,3}  {r.noJob,5}  {r.findFails,9}  {r.killedAfterFind,16}  {r.cannotHaul,10}  {kv.Key.defName} | {r.sample}");
			}
			sb.Append($"\n  this walk took {(System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F1} ms");
			Log.Message(sb.ToString());
		}
	}

	// ---- Diagnostic: which work givers pay for the work search and its reachability checks ---------------------
	// Off unless "ticktrace.on" in the mod folder contains the word "reach" at startup (it then replaces the tick
	// timing above). Sampling profile 2026-11-22: JobGiver_Work is 12 s of 3 minutes and 8 s of that is
	// Reachability.CanReach, but a sample cannot say for which work giver: the scanner is not on the stack while
	// GenClosest asks CanReach for each candidate.
	// JobGiver_Work asks PawnCanUseWorkGiver once at the start of every work giver's turn, so that call marks
	// whose turn it is. Counted per work giver, colonists and mechs apart: turns, stopwatch time of the whole
	// turn, CanReach calls and their time, region floods (CheckRegionBasedReachability, i.e. cache misses) and
	// regions opened per flood. After every 3600 ticks, three times per session, one log entry lists the 25
	// costliest. State is per thread, totals are merged under a lock, in case a work search runs off the main
	// thread. Each CanReach call inside a work search costs about 60 ns extra while the probe is on.
	// Each report ends with one line on the stacks HaulMerge searches (see MergeList).
	static class ReachProbe
	{
		sealed class Stat
		{
			public long ticks, reachTicks, regions;
			public int turns, reachCalls, floods;
		}

		static readonly Dictionary<(WorkGiverDef, bool), Stat> stats = new();
		static readonly long[] searchTicks = new long[2];
		static readonly int[] searches = new int[2];
		static readonly AccessTools.FieldRef<Reachability, int> regionsOpened = AccessTools.FieldRefAccess<Reachability, int>("numRegionsOpened");
		static int ticksSeen, reportsLeft = 3;

		[ThreadStatic] static bool inWork, mech;
		[ThreadStatic] static WorkGiverDef giver;
		[ThreadStatic] static long searchStart, turnStart, reachTicks, regions;
		[ThreadStatic] static int depth, reachCalls, floods;

		internal static void Init(Harmony harmony)
		{
			var me = typeof(ReachProbe);
			harmony.Patch(AccessTools.Method(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage)),
				prefix: new HarmonyMethod(me, nameof(Begin)), finalizer: new HarmonyMethod(me, nameof(End)));
			harmony.Patch(AccessTools.Method(typeof(JobGiver_Work), "PawnCanUseWorkGiver"), prefix: new HarmonyMethod(me, nameof(Turn)));
			harmony.Patch(AccessTools.Method(typeof(Reachability), nameof(Reachability.CanReach),
					[typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms)]),
				prefix: new HarmonyMethod(me, nameof(ReachIn)), postfix: new HarmonyMethod(me, nameof(ReachOut)));
			harmony.Patch(AccessTools.Method(typeof(Reachability), "CheckRegionBasedReachability"), postfix: new HarmonyMethod(me, nameof(Flood)));
			harmony.Patch(AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick)), postfix: new HarmonyMethod(me, nameof(Report)));
			Log.Message("[Harmony3StrictCompat] reach probe on: work search counted per work giver (remove ticktrace.on from the mod folder to turn off)");
		}

		static void Begin(Pawn pawn)
		{
			if (inWork || reportsLeft <= 0) return;
			inWork = true;
			mech = pawn.RaceProps.IsMechanoid;
			giver = null;
			depth = 0;
			searchStart = turnStart = System.Diagnostics.Stopwatch.GetTimestamp();
		}

		static void Turn(WorkGiver giver)
		{
			if (!inWork) return;
			CloseTurn();
			ReachProbe.giver = giver.def;
		}

		static void End()
		{
			if (!inWork) return;
			CloseTurn();
			inWork = false;
			lock (stats)
			{
				searches[mech ? 1 : 0]++;
				searchTicks[mech ? 1 : 0] += System.Diagnostics.Stopwatch.GetTimestamp() - searchStart;
			}
		}

		// Books the turn that just ended (giver null = the part of the search before the first work giver).
		static void CloseTurn()
		{
			long now = System.Diagnostics.Stopwatch.GetTimestamp();
			lock (stats)
			{
				if (!stats.TryGetValue((giver, mech), out var s)) stats[(giver, mech)] = s = new Stat();
				s.turns++;
				s.ticks += now - turnStart;
				s.reachCalls += reachCalls;
				s.reachTicks += reachTicks;
				s.floods += floods;
				s.regions += regions;
			}
			turnStart = now;
			reachCalls = floods = 0;
			reachTicks = regions = 0;
		}

		// CanReach calls itself for the "pass destroyable things" modes: only the outermost call is timed.
		static void ReachIn(out long __state) => __state = inWork && depth++ == 0 ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

		static void ReachOut(long __state)
		{
			if (!inWork || depth == 0) return;
			if (--depth != 0 || __state == 0) return;
			reachTicks += System.Diagnostics.Stopwatch.GetTimestamp() - __state;
			reachCalls++;
		}

		static void Flood(Reachability __instance)
		{
			if (!inWork) return;
			floods++;
			regions += regionsOpened(__instance);
		}

		static void Report()
		{
			if (reportsLeft <= 0 || ++ticksSeen < 3600) return;
			double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
			var sb = new StringBuilder();
			lock (stats)
			{
				sb.Append($"[Harmony3StrictCompat] reach probe over {ticksSeen} ticks: colonist work searches {searches[0]} ({searchTicks[0] * ms:F0} ms), mech work searches {searches[1]} ({searchTicks[1] * ms:F0} ms)");
				sb.Append("\n   turn ms    turns  reach calls  reach ms   floods  regions/flood  who       work giver");
				foreach (var kv in stats.OrderByDescending(kv => kv.Value.ticks).Take(25))
				{
					var s = kv.Value;
					sb.Append($"\n  {s.ticks * ms,8:F1} {s.turns,8} {s.reachCalls,12} {s.reachTicks * ms,9:F1} {s.floods,8} {(s.floods > 0 ? (double)s.regions / s.floods : 0),14:F0}  {(kv.Key.Item2 ? "mech    " : "colonist")}  {kv.Key.Item1?.defName ?? "(before the first work giver)"}");
				}
				stats.Clear();
				Array.Clear(searches, 0, 2);
				Array.Clear(searchTicks, 0, 2);
			}
			MergeList(sb, ms);
			Log.Message(sb.ToString());
			ticksSeen = 0;
			reportsLeft--;
		}

		// What HaulMerge searches at this moment: the game's list of stacks in storage below their def's stack
		// limit, split by the tests of WorkGiver_Merge.JobOnThing that do not depend on the pawn.
		static void MergeList(StringBuilder sb, double ms)
		{
			int listed = 0, wrongStorage = 0, alone = 0, refused = 0, mergeable = 0;
			long start = System.Diagnostics.Stopwatch.GetTimestamp();
			foreach (var map in Find.Maps)
			{
				var list = map.listerMergeables.ThingsPotentiallyNeedingMerging();
				listed += list.Count;
				for (int i = 0; i < list.Count; i++)
				{
					var t = list[i];
					ISlotGroup group = t.GetSlotGroup();
					if (group == null || !t.IsInValidStorage()) { wrongStorage++; continue; }
					ISlotGroup linked = group.StorageGroup;
					bool partner = false, accepted = false;
					foreach (var held in (linked ?? group).HeldThings)
					{
						if (held == t || !held.CanStackWith(t) || held.stackCount < t.stackCount || held.stackCount >= held.def.stackLimit) continue;
						partner = true;
						if (held.Position.IsValidStorageFor(map, t)) { accepted = true; break; }
					}
					if (accepted) mergeable++;
					else if (partner) refused++;
					else alone++;
				}
			}
			sb.Append($"\n  merge list now: {listed} stacks below their stack limit in storage; {wrongStorage} in storage that does not accept them, {alone} with no bigger-or-equal partial stack of their kind in their storage group, {refused} whose partner's cell takes no more (storage cap), {mergeable} mergeable; this walk took {(System.Diagnostics.Stopwatch.GetTimestamp() - start) * ms:F2} ms");
		}
	}

	// ---- Vanilla: one region flood per pawn and room instead of one per destination room (personal tweak) -------
	// Reach probe 2026-11-22: work searches cost 0.4 ms of a 3.6 ms tick and at least 40% of that is
	// Reachability.CanReach. A work giver's global search asks CanReach for every candidate (Refuel: 200 things,
	// every DoBills giver: all 240 bill benches). The game's cache stores one answer per (start district,
	// destination district, traverse parameters), and every miss floods the region graph from the pawn until it
	// meets the destination: 420-710 regions per flood, 30 and more floods in one Refuel turn, because each
	// destination room is asked separately and each room the pawn stands in starts over.
	// A flood that runs to the end has already seen everything reachable from there. On a cache miss with one
	// starting region the flood now always runs to the end and the districts it reached are kept per (pawn,
	// start district, traverse parameters). Later misses from the same place are answered from that list, and
	// the same entry the game would have written goes into its own cache. A district is reached as a whole or
	// not at all (the game's cache already assumes that: door, fence and danger tests are per district).
	// The lists are dropped whenever the game drops its cache (everything, one pawn, pawns hostile to a door)
	// and a list older than 600 ticks is not used, because door locks, temperature danger and the pawn's own
	// room change what a flood finds without clearing anything; the game's cached answers have the same blind
	// spot, one room pair at a time. The first 5000 answers from a list are checked against the game's own
	// flood: differences are logged, and above 0.2% the memo switches itself off for the session.
	// Left to the game: searches with more than one starting region (pawn on an impassable cell) and the two
	// traverse modes its cache does not serve.
	static class ReachFloodMemo
	{
		const int MaxAgeTicks = 600, MaxRecords = 8192, ShadowAnswers = 5000;

		readonly struct Key : IEquatable<Key>
		{
			readonly int district;
			readonly TraverseParms parms;
			public Key(int district, TraverseParms parms) { this.district = district; this.parms = parms; }
			public bool Equals(Key other) => district == other.district && parms == other.parms;
			public override bool Equals(object obj) => obj is Key other && Equals(other);
			public override int GetHashCode() => Gen.HashCombineInt(parms.GetHashCode(), district);
		}

		sealed class Record
		{
			public int[] reached;   // district IDs, sorted
			public int tick;
		}

		sealed class Table
		{
			public readonly Dictionary<Thing, Dictionary<Key, Record>> byPawn = new();
			public readonly Dictionary<Key, Record> noPawn = new();
			public int count;

			public Dictionary<Key, Record> For(Pawn pawn)
			{
				if (pawn == null) return noPawn;
				if (!byPawn.TryGetValue(pawn, out var records)) byPawn[pawn] = records = new();
				return records;
			}

			public void Clear()
			{
				byPawn.Clear();
				noPawn.Clear();
				count = 0;
			}
		}

		static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ReachabilityCache, Table> tables = new();
		static readonly AccessTools.FieldRef<Reachability, ReachabilityCache> cacheOf = AccessTools.FieldRefAccess<Reachability, ReachabilityCache>("cache");
		static readonly AccessTools.FieldRef<Reachability, Queue<Region>> openQueue = AccessTools.FieldRefAccess<Reachability, Queue<Region>>("openQueue");
		static readonly AccessTools.FieldRef<Reachability, List<Region>> startingRegions = AccessTools.FieldRefAccess<Reachability, List<Region>>("startingRegions");
		static readonly AccessTools.FieldRef<Reachability, List<Region>> destRegions = AccessTools.FieldRefAccess<Reachability, List<Region>>("destRegions");
		static readonly AccessTools.FieldRef<Reachability, uint> reachedIndex = AccessTools.FieldRefAccess<Reachability, uint>("reachedIndex");
		static readonly AccessTools.FieldRef<Reachability, int> numRegionsOpened = AccessTools.FieldRefAccess<Reachability, int>("numRegionsOpened");
		static Func<Reachability, TraverseParms, bool> gameFlood;
		[ThreadStatic] static bool inShadow;
		[ThreadStatic] static List<int> ids;
		[ThreadStatic] static List<Thing> gone;
		static int floods, answers, shadowed, differed;
		static bool off, reported;

		internal static void Init(Harmony harmony)
		{
			var flood = Harmony3StrictCompatMod.Target("Verse.Reachability:CheckRegionBasedReachability");
			if (flood == null || Harmony3StrictCompatMod.Target("Verse.Reachability:QueueNewOpenRegion") == null
				|| Harmony3StrictCompatMod.Target("Verse.Reachability:CanReach",
					args: [typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms)]) == null) return;
			var me = typeof(ReachFloodMemo);
			// Made before patching; a call still goes through the patched method, where inShadow lets the game's body run.
			gameFlood = AccessTools.MethodDelegate<Func<Reachability, TraverseParms, bool>>(flood);
			harmony.Patch(flood, prefix: new HarmonyMethod(me, nameof(Flood)));
			harmony.Patch(AccessTools.Method(typeof(Reachability), nameof(Reachability.ClearCache)), postfix: new HarmonyMethod(me, nameof(Cleared)));
			harmony.Patch(AccessTools.Method(typeof(Reachability), nameof(Reachability.ClearCacheFor)), postfix: new HarmonyMethod(me, nameof(ClearedFor)));
			harmony.Patch(AccessTools.Method(typeof(Reachability), nameof(Reachability.ClearCacheForHostile)), postfix: new HarmonyMethod(me, nameof(ClearedForHostile)));
		}

		static bool Flood(Reachability __instance, TraverseParms traverseParams, ref bool __result)
		{
			if (inShadow || off || traverseParams.mode == TraverseMode.PassAllDestroyableThingsNotWater
				|| traverseParams.mode == TraverseMode.NoPassClosedDoorsOrWater) return true;
			var starts = startingRegions(__instance);
			if (starts.Count != 1 || starts[0].District is not { } from) return true;
			var dests = destRegions(__instance);
			var cache = cacheOf(__instance);
			var table = tables.GetOrCreateValue(cache);
			lock (table)
			{
				int now = Find.TickManager.TicksGame;
				var key = new Key(from.ID, traverseParams);
				var records = table.For(traverseParams.pawn);
				if (records.TryGetValue(key, out var record) && (uint)(now - record.tick) < MaxAgeTicks)
				{
					Region found = null;
					for (int i = 0; i < dests.Count && found == null; i++)
						if (dests[i].District is { } district && Array.BinarySearch(record.reached, district.ID) >= 0) found = dests[i];
					if (shadowed < ShadowAnswers)
					{
						// The start region is still queued and marked, exactly what the game's flood expects.
						inShadow = true;
						try { __result = gameFlood(__instance, traverseParams); }
						finally { inShadow = false; }
						Compare(found != null, __result, traverseParams, from, now - record.tick);
						return false;
					}
					answers++;
					Book(cache, from, dests, found, traverseParams);
					__result = found != null;
					Report();
					return false;
				}

				// The game's flood, minus the early return at the destination.
				var list = ids ??= new List<int>();
				list.Clear();
				list.Add(from.ID);
				var queue = openQueue(__instance);
				uint mark = reachedIndex(__instance);
				ref int opened = ref numRegionsOpened(__instance);
				while (queue.Count > 0)
				{
					var links = queue.Dequeue().links;
					for (int i = 0; i < links.Count; i++)
						for (int j = 0; j < 2; j++)
						{
							var next = links[i].regions[j];
							if (next == null || next.reachedIndex == mark || !next.type.Passable() || !next.Allows(traverseParams, isDestination: false)) continue;
							queue.Enqueue(next);
							next.reachedIndex = mark;
							opened++;
							if (next.District is { } district) list.Add(district.ID);
						}
				}
				list.Sort();
				int n = 0;
				for (int i = 0; i < list.Count; i++)
					if (i == 0 || list[i] != list[i - 1]) list[n++] = list[i];
				var reached = new int[n];
				list.CopyTo(0, reached, 0, n);

				if (record == null)
				{
					if (table.count >= MaxRecords)
					{
						table.Clear();
						records = table.For(traverseParams.pawn);
					}
					table.count++;
				}
				records[key] = new Record { reached = reached, tick = now };

				Region hit = null;
				for (int i = 0; i < dests.Count && hit == null; i++)
					if (dests[i].reachedIndex == mark) hit = dests[i];
				floods++;
				Book(cache, from, dests, hit, traverseParams);
				__result = hit != null;
				Report();
				return false;
			}
		}

		// The entries the game's flood writes: the found pair as reachable, or every asked pair as unreachable.
		static void Book(ReachabilityCache cache, District from, List<Region> dests, Region found, TraverseParms parms)
		{
			if (found != null)
			{
				if (found.District is { } to) cache.AddCachedResult(from, to, parms, reachable: true);
				return;
			}
			for (int i = 0; i < dests.Count; i++)
				if (dests[i].District is { } to) cache.AddCachedResult(from, to, parms, reachable: false);
		}

		static void Compare(bool memo, bool game, TraverseParms parms, District from, int age)
		{
			shadowed++;
			if (memo != game && ++differed <= 5)
				Log.Warning($"[Harmony3StrictCompat] Reach flood memo differs from the game's flood: memo {memo}, game {game}, pawn {parms.pawn.ToStringSafe()}, mode {parms.mode}, max danger {parms.maxDanger}, from district {from.ID}, list {age} ticks old");
			if (shadowed != ShadowAnswers) return;
			off = differed * 500 > shadowed;
			if (off) Log.Error($"[Harmony3StrictCompat] Reach flood memo switched off: {differed} of {shadowed} checked answers differed from the game's flood");
			else Log.Message($"[Harmony3StrictCompat] Reach flood memo check: {differed} of {shadowed} answers differed from the game's flood, memo stays on");
		}

		static void Report()
		{
			if (reported || floods + answers < 50000) return;
			reported = true;
			Log.Message($"[Harmony3StrictCompat] Reach flood memo: {floods} floods, {answers} answers without a flood");
		}

		static void Cleared(Reachability __instance)
		{
			if (tables.TryGetValue(cacheOf(__instance), out var table))
				lock (table) table.Clear();
		}

		static void ClearedFor(Reachability __instance, Pawn pawn)
		{
			if (pawn == null || !tables.TryGetValue(cacheOf(__instance), out var table)) return;
			lock (table)
				if (table.byPawn.Remove(pawn, out var records)) table.count -= records.Count;
		}

		// Same rule and same per-tick hostility memo as the game's own cache clearing (TickHotPaths).
		static void ClearedForHostile(Reachability __instance, Thing hostileTo)
		{
			if (!tables.TryGetValue(cacheOf(__instance), out var table)) return;
			lock (table)
			{
				if (table.byPawn.Count == 0) return;
				var list = gone ??= new List<Thing>();
				list.Clear();
				foreach (var pawn in table.byPawn.Keys)
					if (TickHotPaths.Hostile(pawn, hostileTo)) list.Add(pawn);
				for (int i = 0; i < list.Count; i++)
					if (table.byPawn.Remove(list[i], out var records)) table.count -= records.Count;
				list.Clear();
			}
		}
	}

	// ---- Three mod work givers that walk every colony building on each turn ------------------------------------
	// Reach probe 2026-11-22: about 1 ms per turn each, without a single reachability call, together a tenth of
	// all work search time.
	// MSS Tweaks and Fun, WorkGiver_HaulToAICore.ShouldSkip: asks every colony building for its AI core comp,
	// also on a map without any AI core. Prefix: when no thing of a def that carries that comp is on the map,
	// the answer is "skip", which is what the walk would find; otherwise the original runs.
	// Hospitality: Casino and Vending Machines, VendingMachineJobHelper.GetActiveVendingMachines (same code in
	// both): filters every colony building through IsVendingMachine. Prefix: only the player's things of defs
	// that carry the mod's vending comp go through the mod's own IsVendingMachine. Same machines; the list is
	// ordered by def instead of by building order, which only decides between two machines at exactly the same
	// distance from the pawn.
	// Both rest on comps coming from the def's comp list, as these mods' comps do.
	static class ModWorkGiverScans
	{
		sealed class Vending
		{
			public Type comp;
			public Func<Building, bool> isMachine;
		}

		static readonly Dictionary<Type, ThingDef[]> defsWithComp = new();
		static readonly Dictionary<Type, Vending> vending = new();
		static Type coreComp;
		static int coreCalls, coreSkips, listCalls, listCandidates;
		static bool coreReported, listReported;

		internal static void Init(Harmony harmony)
		{
			var me = typeof(ModWorkGiverScans);
			if (Harmony3StrictCompatMod.Target("MSSFP.AICore.WorkGiver_HaulToAICore:ShouldSkip") is { } skip)
			{
				coreComp = AccessTools.TypeByName("MSSFP.Comps.CompTrueAICore");
				if (coreComp != null) harmony.Patch(skip, prefix: new HarmonyMethod(me, nameof(NoCoreOnMap)));
				else Log.Error("[Harmony3StrictCompat] MSSFP.Comps.CompTrueAICore no longer exists: its mod updated, remove or redo this fix");
			}
			foreach (var mod in new[] { "HospitalityCasino", "VendingMachines" })
			{
				if (Harmony3StrictCompatMod.Target($"{mod}.VendingMachineJobHelper:GetActiveVendingMachines") is not { } list) continue;
				var comp = AccessTools.TypeByName($"{mod}.CompVendingMachine");
				var isMachine = AccessTools.Method(list.DeclaringType, "IsVendingMachine", [typeof(Building)]);
				if (comp == null || isMachine == null)
				{
					Log.Error($"[Harmony3StrictCompat] {mod}.CompVendingMachine or VendingMachineJobHelper.IsVendingMachine no longer exists: its mod updated, remove or redo this fix");
					continue;
				}
				vending[list.DeclaringType] = new Vending { comp = comp, isMachine = AccessTools.MethodDelegate<Func<Building, bool>>(isMachine) };
				harmony.Patch(list, prefix: new HarmonyMethod(me, nameof(ActiveMachines)));
			}
		}

		// Defs whose comp list holds this comp class or a subclass. Asked in a running game only, so defs are final.
		static ThingDef[] DefsWith(Type compClass)
		{
			lock (defsWithComp)
			{
				if (!defsWithComp.TryGetValue(compClass, out var defs))
					defsWithComp[compClass] = defs = DefDatabase<ThingDef>.AllDefsListForReading
						.Where(d => d.comps != null && d.comps.Any(c => c.compClass != null && compClass.IsAssignableFrom(c.compClass))).ToArray();
				return defs;
			}
		}

		static bool NoCoreOnMap(Pawn pawn, ref bool __result)
		{
			var lister = pawn.Map.listerThings;
			bool any = false;
			foreach (var def in DefsWith(coreComp))
				if (lister.ThingsOfDef(def).Count > 0) { any = true; break; }
			coreCalls++;
			if (!any) coreSkips++;
			if (!coreReported && coreCalls >= 300)
			{
				coreReported = true;
				Log.Message($"[Harmony3StrictCompat] AI core haul check: {coreSkips} of {coreCalls} turns answered without walking the colony's buildings (no AI core on the map)");
			}
			if (any) return true;
			__result = true;
			return false;
		}

		static bool ActiveMachines(Map map, ref List<Building> __result, MethodBase __originalMethod)
		{
			if (!vending.TryGetValue(__originalMethod.DeclaringType, out var mod)) return true;
			var machines = new List<Building>();
			var lister = map.listerThings;
			var player = Faction.OfPlayer;
			foreach (var def in DefsWith(mod.comp))
			{
				var things = lister.ThingsOfDef(def);
				listCandidates += things.Count;
				for (int i = 0; i < things.Count; i++)
					if (things[i] is Building building && building.Faction == player && mod.isMachine(building)) machines.Add(building);
			}
			if (!listReported && ++listCalls >= 300)
			{
				listReported = true;
				Log.Message($"[Harmony3StrictCompat] Vending machine listing: {listCalls} turns looked at {listCandidates} machines instead of every colony building ({map.listerBuildings.allBuildingsColonist.Count} here)");
			}
			__result = machines;
			return false;
		}
	}

	// ---- Vanilla: three work searches that look at everything before the cheap test (personal tweak) ----------
	// Reach probe 2026-11-22, with the reach flood memo active.
	// HaulAIUtility.FindFixedIngredientCount: walks every reachable region and every haulable thing in it until
	// it has found the wanted count of one def. Vanilla Recycling Expanded's alloy splitter asks it on every
	// work turn for each splitter that is at most half full; with no loose alloypack on the map (the save had 4
	// splitters holding 5, 5, 7 and 8 of 10, and none outside them) each call walks the map for an empty list:
	// 0.9 ms per turn, 50-70 ms per 3600 ticks. Prefix: when no thing of that def is spawned on the map the
	// answer is the empty list the walk would return; otherwise the original runs.
	// WorkGiver_DoBill with more than one bench def (DoBillsCook, ButcherFlesh, Smelter, ...): the game only
	// narrows the candidate list when the work giver names exactly one def, so its global search gets every
	// thing whose def has recipes (251 on that map: about 190 pawns, animals included, plus every bench) and
	// GenClosest asks Reachability.CanReach for each before the work giver's own JobOnThing looks at the def.
	// JobGiver_Work passes a non-null PotentialWorkThingsGlobal as the global search set, so a postfix on
	// that (the scanner base returns null) hands back the things that pass the work giver's own
	// ThingIsUsableBillGiver, in the game's list order. JobOnThing refuses everything else on its first line,
	// so the same thing wins. Only for the exact class WorkGiver_DoBill: a subclass may accept other things.
	// A mod patch on JobOnThing that hands out a job for a thing ThingIsUsableBillGiver refuses would no
	// longer be reached by the global search (the nearby region search is untouched).
	// WorkGiver_Merge: searches every stack in storage that is below its def's stack limit (362-368 on that
	// map), each reach-checked and then run through JobOnThing, on every turn of every hauler. The probe's
	// merge line: 329-332 of them have no partial stack of their kind in their storage group, 29-30 have one
	// whose cell takes no more (a storage cap), 1-9 can merge. Postfix on its PotentialWorkThingsGlobal: only
	// the stacks for which JobOnThing's partner tests that do not depend on the pawn can pass (another stack in
	// the same storage group that stacks with it, is at least as big, below the limit, in a cell that is valid
	// storage for it). Worked out anew on every turn, so nothing is stale; every pawn test stays with the game.
	// A right-click order (FloatMenuMakerMap.makingFor set) gets the game's full list: forced merging drops
	// the "at least as big" rule.
	static class VanillaWorkSearchScans
	{
		static int ingredientCalls, ingredientSkips, benchTurns, benchListed, mergeTurns, mergeListed, mergeKept;
		static bool ingredientReported, benchReported, mergeReported;
		[ThreadStatic] static Dictionary<(ISlotGroup, ThingDef), List<Thing>> partialStacks;
		[ThreadStatic] static HashSet<ISlotGroup> groupsSeen;

		internal static void Init(Harmony harmony)
		{
			var me = typeof(VanillaWorkSearchScans);
			if (Harmony3StrictCompatMod.Target("Verse.AI.HaulAIUtility:FindFixedIngredientCount") is { } find)
				harmony.Patch(find, prefix: new HarmonyMethod(me, nameof(NoneOnMap)));
			// JobOnThing is only fingerprinted: the partner tests below are copied from it.
			if (Harmony3StrictCompatMod.Target("RimWorld.WorkGiver_Merge:PotentialWorkThingsGlobal") is { } mergeables
				&& Harmony3StrictCompatMod.Target("RimWorld.WorkGiver_Merge:JobOnThing") != null)
				harmony.Patch(mergeables, postfix: new HarmonyMethod(me, nameof(MergeablesWithPartner)));
			// The bench list rests on how these three use it; a changed fingerprint on any of them is the cue to re-read.
			var things = Harmony3StrictCompatMod.Target("RimWorld.WorkGiver_Scanner:PotentialWorkThingsGlobal");
			if (things == null || Harmony3StrictCompatMod.doBillJobOnThing == null
				|| Harmony3StrictCompatMod.Target("RimWorld.JobGiver_Work:TryIssueJobPackage") == null
				|| Harmony3StrictCompatMod.Target("Verse.GenClosest:ClosestThingReachable") == null) return;
			harmony.Patch(things, postfix: new HarmonyMethod(me, nameof(OwnBenchesOnly)));
		}

		static bool NoneOnMap(Pawn pawn, ThingDef def, ref List<Thing> __result)
		{
			bool none = pawn.Map.listerThings.ThingsOfDef(def).Count == 0;
			ingredientCalls++;
			if (none) ingredientSkips++;
			if (!ingredientReported && ingredientCalls >= 300)
			{
				ingredientReported = true;
				Log.Message($"[Harmony3StrictCompat] Ingredient search: {ingredientSkips} of {ingredientCalls} searches answered without walking the map (nothing of that def on it)");
			}
			if (!none) return true;
			__result = new List<Thing>();
			return false;
		}

		static void OwnBenchesOnly(WorkGiver_Scanner __instance, Pawn pawn, ref IEnumerable<Thing> __result)
		{
			if (__result != null || __instance.GetType() != typeof(WorkGiver_DoBill)) return;
			var giver = (WorkGiver_DoBill)__instance;
			if (giver.def.fixedBillGiverDefs is { Count: 1 }) return;   // the game already asks for that one def
			var all = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.PotentialBillGiver);
			var own = new List<Thing>();
			for (int i = 0; i < all.Count; i++)
				if (giver.ThingIsUsableBillGiver(all[i])) own.Add(all[i]);
			__result = own;
			benchListed += own.Count;
			if (!benchReported && ++benchTurns >= 300)
			{
				benchReported = true;
				Log.Message($"[Harmony3StrictCompat] Bill bench listing: {benchTurns} work turns searched {benchListed} bill givers of their own instead of all {all.Count} on the map each");
			}
		}

		// ponytail: the partial stacks of every storage group are bucketed anew on each turn (one pass over the
		// things held in storage). If the probe shows that pass matters, keep the result per map for some ticks
		// and drop entries that left storage before handing it out (JobOnThing assumes a spawned thing in a
		// slot group).
		static void MergeablesWithPartner(Pawn pawn, ref IEnumerable<Thing> __result)
		{
			if (FloatMenuMakerMap.makingFor != null || __result is not List<Thing> listed) return;
			var map = pawn.Map;
			var partials = partialStacks ??= new();
			var seen = groupsSeen ??= new();
			partials.Clear();
			seen.Clear();
			var kept = new List<Thing>();
			for (int i = 0; i < listed.Count; i++)
			{
				var t = listed[i];
				ISlotGroup group = t.GetSlotGroup();
				if (group == null) { kept.Add(t); continue; }   // not what the game's list promises: left to the game
				ISlotGroup linked = group.StorageGroup;
				group = linked ?? group;
				if (seen.Add(group))
					foreach (var held in group.HeldThings)
					{
						if (held.stackCount >= held.def.stackLimit) continue;
						if (!partials.TryGetValue((group, held.def), out var stacks)) partials[(group, held.def)] = stacks = new List<Thing>();
						stacks.Add(held);
					}
				if (!partials.TryGetValue((group, t.def), out var same)) continue;
				for (int j = 0; j < same.Count; j++)
				{
					var other = same[j];
					if (other == t || other.stackCount < t.stackCount || !other.CanStackWith(t) || !other.Position.IsValidStorageFor(map, t)) continue;
					kept.Add(t);
					break;
				}
			}
			partials.Clear();
			seen.Clear();
			__result = kept;
			mergeListed += listed.Count;
			mergeKept += kept.Count;
			if (!mergeReported && ++mergeTurns >= 100)
			{
				mergeReported = true;
				Log.Message($"[Harmony3StrictCompat] Merge search: {mergeTurns} work turns searched {mergeKept} stacks that have a possible partner instead of {mergeListed} listed");
			}
		}
	}

	// ---- TD Enhancement Pack: urgent refill scan when nothing is marked for refill ---------------------------
	// WorkGiver_HaulUrgentlyRefill.PotentialWorkThingsGlobal lists every haulable thing on the map and runs
	// StoreUtility.IsInValidBestStorage (a full better-storage search) on each before it looks at whether any
	// storage is marked "urgent refill" at all. With nothing marked the answer is always "no work", after about
	// 5% of the tick (tick trace + sampling profile, 30 colonists, 2026-11-20). Skip the scan while the map's
	// RefillZoneComp has no marked building and no marked stockpile, which is exactly when both of its lists
	// are empty. Marking any storage brings the original scan back.
	[StaticConstructorOnStartup]
	static class TdUrgentRefillIdle
	{
		static readonly Type compType = AccessTools.TypeByName("TD_Enhancement_Pack.RefillZoneComp");
		static readonly FieldInfo buildings = compType == null ? null : AccessTools.Field(compType, "markedForRefillBuilding");
		static readonly FieldInfo stockpiles = compType == null ? null : AccessTools.Field(compType, "markedForRefillStockpile");

		static TdUrgentRefillIdle()
		{
			var scan = Harmony3StrictCompatMod.Target("TD_Enhancement_Pack.WorkGiver_HaulUrgentlyRefill:PotentialWorkThingsGlobal");
			if (scan == null || buildings == null || stockpiles == null) return;
			new Harmony("sammael.harmony3strictcompat").Patch(scan, prefix: new HarmonyMethod(typeof(TdUrgentRefillIdle), nameof(NothingMarked)));
		}

		static bool NothingMarked(Pawn pawn, ref IEnumerable<Thing> __result)
		{
			var comp = pawn.Map?.GetComponent(compType);
			if (comp == null || buildings.GetValue(comp) is not HashSet<Building_Storage> { Count: 0 }
				|| stockpiles.GetValue(comp) is not HashSet<Zone_Stockpile> { Count: 0 }) return true;
			__result = Enumerable.Empty<Thing>();
			return false;
		}
	}

	// ---- Better Beggars: a beggar quest with nothing to ask for (seen 2026-11-23, colony split by a gravship) ----
	// Its "want thing" quests (items, drugs) choose the request on QuestGen_Get.GetMap(), which picks a random player
	// home map with a free colonist, and RunInt calls it again instead of reusing TestRunInt's map. With two such maps
	// (the old home and the gravship site) the can-fire test passed on the stocked home while RunInt landed on the
	// bare site: no item was found, RunInt went on anyway, the beggars arrived with an empty request ("ERR" in the
	// quest text), LordJob_BegForItems counted nothing asked as already given, and they left at once with the quest
	// marked a success. RunInt now first runs the node's own TestRunInt on each candidate map (random order) and is
	// built on the first one that passes; GetMap returns that map until RunInt ends. When no map passes, the test
	// already kept the quest from firing.
	// ponytail: the test uses 0.85 x points like Better Beggars' own TestRunInt; a request multiplier set above 0.85
	// can still find nothing in RunInt. Check the real value too if someone raises it.
	static class BbRequestOnStockedMap
	{
		static Map pinned;

		internal static void Init(Harmony harmony)
		{
			var getMap = Harmony3StrictCompatMod.Target("RimWorld.QuestGen.QuestGen_Get:GetMap");
			if (getMap == null) return;
			bool any = false;
			foreach (var node in new[] { "BetterBeggars.QuestNode_Root_Beggars_WantThing_Vanilla", "BetterBeggars.QuestNode_Root_Beggars_WantThing_Drugs" })
			{
				var run = Harmony3StrictCompatMod.Target(node + ":RunInt", optional: true);
				if (run == null || Harmony3StrictCompatMod.Target(node + ":TestRunInt", optional: true) == null) continue;
				harmony.Patch(run, prefix: new HarmonyMethod(typeof(BbRequestOnStockedMap), nameof(PinStockedMap)),
					finalizer: new HarmonyMethod(typeof(BbRequestOnStockedMap), nameof(Unpin)));
				any = true;
			}
			if (any) harmony.Patch(getMap, postfix: new HarmonyMethod(typeof(BbRequestOnStockedMap), nameof(PinnedMap)));
		}

		static void PinStockedMap(RimWorld.QuestGen.QuestNode __instance)
		{
			pinned = null;
			var test = AccessTools.Method(__instance.GetType(), "TestRunInt");
			var slate = RimWorld.QuestGen.QuestGen.slate;
			// Same candidates as vanilla GetMap(false, null, false): player homes off space layers with a free colonist.
			foreach (var map in Find.Maps.Where(m => m.IsPlayerHome && !m.Tile.LayerDef.isSpace && m.mapPawns.FreeColonists.Any()).InRandomOrder().ToList())
			{
				pinned = map;
				if (test != null && (bool)test.Invoke(__instance, [slate])) return;
			}
			pinned = null;
		}

		static Exception Unpin(Exception __exception)
		{
			pinned = null;
			return __exception;
		}

		static void PinnedMap(ref Map __result)
		{
			if (pinned != null) __result = pinned;
		}
	}

	// ---- Vanilla (Odyssey): a stale "leave ship" / "board ship" job ends once no engine asks for it -----------
	// The gravship launch ritual puts visitors into Building_GravEngine.pawnsToLeave (colony animals and mechs into
	// pawnsToBoard), and JobGiver_BoardOrLeaveGravship (replaced by Gravship Fleet's prefix, same jobs) then gives
	// LeaveShip / GotoShip. Both run JobDriver_BoardOrLeaveGravship, a JobDriver_StandAndBeSociallyActive: walk to
	// the spot, then stand with no end. The ritual's Cleanup nulls both lists and ends only the current job, so a
	// copy still in the job queue starts afterwards and the pawn stands until it collapses (seen 2026-11-23: a
	// Hospitality guest stood 1.5 days without food or sleep, 72 pemmican in her pack; her lost group then cost
	// -10 goodwill). The job now ends as soon as no grav engine on the pawn's map lists it. Engines are found per
	// def whose class is Building_GravEngine (vanilla plus Vanilla Gravship Expanded's), every one of them, as
	// Gravship Fleet does. The condition runs on the pawn's tick only while it has this job. Pawns in a ritual lord
	// are left alone: the launch ritual sends its own participants with GotoShip (JobGiver_GoToValidSubstructure)
	// without listing them, and the first build (bb89b53) ended those jobs at once, so colonists looped 10 jobs a
	// tick and could not board (2026-11-23 18:1x).
	static class GravshipStaleBoardOrLeave
	{
		static List<ThingDef> engineDefs;

		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("RimWorld.JobDriver_BoardOrLeaveGravship:GetGotoToil") is { } gotoToil)
				harmony.Patch(gotoToil, postfix: new HarmonyMethod(typeof(GravshipStaleBoardOrLeave), nameof(AddEndCondition)));
		}

		// GetGotoToil is called once per job while the driver builds its toils.
		static void AddEndCondition(JobDriver __instance)
		{
			var driver = __instance;
			driver.AddEndCondition(() => StillListed(driver) ? JobCondition.Ongoing : Ended(driver));
		}

		static bool StillListed(JobDriver driver)
		{
			var map = driver.pawn.MapHeld;
			if (map == null || driver.job == null) return true;
			// The launch ritual's own participants get GotoShip from JobGiver_GoToValidSubstructure (their ritual
			// duty), not from an engine list; the ritual ends those jobs itself. Only jobs outside a ritual are checked.
			if (driver.pawn.GetLord()?.LordJob is LordJob_Ritual) return true;
			engineDefs ??= DefDatabase<ThingDef>.AllDefsListForReading
				.Where(d => d.thingClass != null && typeof(Building_GravEngine).IsAssignableFrom(d.thingClass)).ToList();
			bool boarding = driver.job.def.boardingGravship;
			foreach (var def in engineDefs)
				foreach (var thing in map.listerThings.ThingsOfDef(def))
					if (thing is Building_GravEngine engine && (boarding ? engine.pawnsToBoard : engine.pawnsToLeave) is { } listed
						&& listed.Contains(driver.pawn))
						return true;
			return false;
		}

		static JobCondition Ended(JobDriver driver)
		{
			Log.Message($"[Harmony3StrictCompat] Gravship: {driver.pawn.LabelShort} stopped a stale {driver.job.def.defName} job; no grav engine on its map lists it any more");
			return JobCondition.Incompletable;
		}
	}

	// ---- Vanilla (Odyssey): gravcore subquest check on every tick (personal tweak) ----------------------------
	// QuestPart_SubquestGenerator_Gravcores.QuestPartTick asks CanGenerateSubquest every tick, and that getter first
	// counts the quest's pending and successful subquests by walking every quest in the game (LINQ, twice) before
	// the cheap tests: minimum time since the last subquest, then the random roll with a mean of 120,000 or
	// 300,000 ticks. Sampling profile: about 2.4% of main-thread time (2026-11-20). Same tests, cheap ones first:
	// the quest walk now runs only on a tick where the roll (or the maximum wait) says a subquest is due. The
	// roll is independent of the other tests, so the chance of a subquest on any tick is unchanged. One real
	// change: once the maximum wait has passed, the walk is retried every 250 ticks, not every tick.
	static class GravcoreSubquestTick
	{
		static AccessTools.FieldRef<QuestPart_SubquestGenerator_Gravcores, float> lastTick;
		static Func<QuestPart_SubquestGenerator_Gravcores, int> mtb, minTime, maxTime;
		static Func<QuestPart_SubquestGenerator_Gravcores, bool> canGenerate, tryGenerate;

		internal static void Init(Harmony harmony)
		{
			var tick = Harmony3StrictCompatMod.Target("RimWorld.QuestPart_SubquestGenerator_Gravcores:QuestPartTick");
			var can = Harmony3StrictCompatMod.Target("RimWorld.QuestPart_SubquestGenerator_Gravcores:get_CanGenerateSubquest");
			if (tick == null || can == null) return;
			var type = typeof(QuestPart_SubquestGenerator_Gravcores);
			lastTick = AccessTools.FieldRefAccess<QuestPart_SubquestGenerator_Gravcores, float>("lastSubquestTick");
			mtb = AccessTools.MethodDelegate<Func<QuestPart_SubquestGenerator_Gravcores, int>>(AccessTools.PropertyGetter(type, "MTB"));
			minTime = AccessTools.MethodDelegate<Func<QuestPart_SubquestGenerator_Gravcores, int>>(AccessTools.PropertyGetter(type, "MinTime"));
			maxTime = AccessTools.MethodDelegate<Func<QuestPart_SubquestGenerator_Gravcores, int>>(AccessTools.PropertyGetter(type, "MaxTime"));
			canGenerate = AccessTools.MethodDelegate<Func<QuestPart_SubquestGenerator_Gravcores, bool>>((MethodInfo)can);
			tryGenerate = AccessTools.MethodDelegate<Func<QuestPart_SubquestGenerator_Gravcores, bool>>(AccessTools.Method(type, "TryGenerateSubquest"));
			harmony.Patch(tick, prefix: new HarmonyMethod(typeof(GravcoreSubquestTick), nameof(Tick)));
		}

		static bool Tick(QuestPart_SubquestGenerator_Gravcores __instance)
		{
			float now = Find.TickManager.TicksGame, last = lastTick(__instance);
			if (__instance.subquestDefs.Count == 0 || Find.AnyPlayerHomeMap == null || now - last < minTime(__instance)) return false;
			// Past the maximum wait the original asks on every tick until a subquest can start (in the 30-colonist
			// save it had been asking for 16 million ticks: three subquests pending). Ask every 250 ticks instead.
			if (!Rand.MTBEventOccurs(mtb(__instance), 1f, 1f) && (now <= last + maxTime(__instance) || Find.TickManager.TicksGame % 250 != 0)) return false;
			if (canGenerate(__instance)) tryGenerate(__instance);
			return false;
		}
	}

	// ---- TD Enhancement Pack: "Empty Workbench" alert on benches that cannot have bills ----------------------
	// Alert_NoBill lists every work table that shows a Bills tab and has an empty bill stack. Altered Carbon's
	// neural editor (and its compact Gravship variant) is such a table, but no recipe names it: its bills are
	// created by its gizmos and are gone again when done, so the alert pointed at it permanently. Drop culprits
	// whose def has no recipes at all; nobody can add a bill to those by hand.
	[StaticConstructorOnStartup]
	static class TdEmptyWorkbenchNoRecipes
	{
		static TdEmptyWorkbenchNoRecipes()
		{
			var report = Harmony3StrictCompatMod.Target("TD_Enhancement_Pack.Alert_NoBill:GetReport");
			if (report == null) return;
			new Harmony("sammael.harmony3strictcompat").Patch(report, postfix: new HarmonyMethod(typeof(TdEmptyWorkbenchNoRecipes), nameof(DropBenchesWithoutRecipes)));
		}

		static void DropBenchesWithoutRecipes(ref AlertReport __result)
		{
			if (__result.culpritsThings is not { Count: > 0 } benches) return;
			benches.RemoveAll(bench => bench.def.AllRecipes.NullOrEmpty());
			if (benches.Count == 0) __result = AlertReport.Inactive;
		}
	}

	// ---- UI Rethemed - Architect: names on two lines (personal tweak) -----------------------------------------
	// Long names were cut to one line with "..." in two places:
	// 1. The cards of the designator grid. The label area under the icon is 31 px high, one line of the tiny
	//    font; DesignatorGrid.CardHeight gives it 30 px on top of the icon size. It now gives it room for two
	//    lines. UIR already wraps the label and cuts it to the area's height (GenText.TruncateHeight), so the
	//    second line still ends in "..." when the name is longer than that. Grid and orders column both size
	//    their rows from CardHeight.
	// 2. The name in the Details panel header (DetailsPanel.Header), a 24 px single line beside the info and
	//    favourite buttons. When the name does not fit, the header (78 px in the column layout) grows by one line,
	//    the category row and the chips under it move down by the same amount, and the name is drawn wrapped.
	//    Whether it fits is measured where the name is drawn and applied from the next frame on. The strip
	//    layout (details across the bottom), whose header height is not its own, keeps the single line.
	// 3. With two lines available, a single word wider than the card ("Matterweaver") wrapped in the middle of
	//    the word ("Matterweave" / "r"). DesignatorGrid.Card now gets its label from CardLabel: a word that does
	//    not fit the card's width is cut with "..." on its own, then the whole label is cut to the two lines.
	static class UirArchitectTwoLineNames
	{
		static Action<Rect, string, Color, GameFont, TextAnchor> oneLine;                 // CoreWidgets.Line
		static Action<Rect, string, TextAnchor, Color, GameFont> themeLabel;              // Theme.Label
		static float titleExtra, titlePending;
		static bool titleInColumn;

		internal static void Init(Harmony harmony)
		{
			var cardHeight = Harmony3StrictCompatMod.Target("UIRethemed.Architect.DesignatorGrid:get_CardHeight");
			var labelRect = Harmony3StrictCompatMod.Target("UIRethemed.Architect.DesignatorGrid:LabelRect"); // its 31 px is assumed below
			if (cardHeight != null && labelRect != null)
				harmony.Patch(cardHeight, transpiler: new HarmonyMethod(typeof(UirArchitectTwoLineNames), nameof(CardLabelRoom)));
			if (cardHeight != null && labelRect != null && Harmony3StrictCompatMod.Target("UIRethemed.Architect.DesignatorGrid:Card") is { } card)
				harmony.Patch(card, transpiler: new HarmonyMethod(typeof(UirArchitectTwoLineNames), nameof(CardLabelWholeWords)));
			var header = Harmony3StrictCompatMod.Target("UIRethemed.Architect.DetailsPanel:Header");
			var line = AccessTools.Method("UIRethemed.Core.CoreWidgets:Line", [typeof(Rect), typeof(string), typeof(Color), typeof(GameFont), typeof(TextAnchor)]);
			var label = AccessTools.Method("UIRethemed.Core.Theme:Label", [typeof(Rect), typeof(string), typeof(TextAnchor), typeof(Color), typeof(GameFont)]);
			if (header == null || line == null || label == null) return;
			oneLine = (Action<Rect, string, Color, GameFont, TextAnchor>)line.CreateDelegate(typeof(Action<Rect, string, Color, GameFont, TextAnchor>));
			themeLabel = (Action<Rect, string, TextAnchor, Color, GameFont>)label.CreateDelegate(typeof(Action<Rect, string, TextAnchor, Color, GameFont>));
			harmony.Patch(header, transpiler: new HarmonyMethod(typeof(UirArchitectTwoLineNames), nameof(HeaderTitleRoom)));
		}

		static IEnumerable<CodeInstruction> CardLabelRoom(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var thirty = codes.Where(c => c.opcode == OpCodes.Ldc_R4 && c.operand is float f && f == 30f).ToList();
			if (thirty.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] UIR Architect CardHeight has {thirty.Count} label heights instead of one, card names stay on one line");
				return codes;
			}
			thirty[0].opcode = OpCodes.Call;
			thirty[0].operand = AccessTools.Method(typeof(UirArchitectTwoLineNames), nameof(CardLabelHeight));
			return codes;
		}

		static float CardLabelHeight() => 30f + Mathf.Max(0f, Mathf.Ceil(2f * Text.LineHeightOf(GameFont.Tiny) - 31f));

		static IEnumerable<CodeInstruction> CardLabelWholeWords(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var truncate = AccessTools.Method(typeof(GenText), nameof(GenText.TruncateHeight), [typeof(string), typeof(float), typeof(float), typeof(Dictionary<string, string>)]);
			var calls = codes.Where(c => c.Calls(truncate)).ToList();
			if (calls.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] UIR Architect DesignatorGrid.Card cuts {calls.Count} labels instead of one, long words may still split");
				return codes;
			}
			calls[0].operand = AccessTools.Method(typeof(UirArchitectTwoLineNames), nameof(CardLabel));
			return codes;
		}

		// Same arguments and cache use as GenText.TruncateHeight; the caller has set the tiny font.
		static string CardLabel(string label, float width, float height, Dictionary<string, string> cache)
		{
			if (cache != null && cache.TryGetValue(label, out string cut)) return cut;
			string whole = label;
			if (Text.CalcSize(label).x > width)
			{
				var words = label.Split(' ');
				for (int i = 0; i < words.Length; i++)
					if (Text.CalcSize(words[i]).x > width) words[i] = words[i].Truncate(width);
				whole = string.Join(" ", words);
			}
			cut = whole.TruncateHeight(width, height);
			if (cache != null) cache[label] = cut;
			return cut;
		}

		static IEnumerable<CodeInstruction> HeaderTitleRoom(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var line = AccessTools.Method("UIRethemed.Core.CoreWidgets:Line", [typeof(Rect), typeof(string), typeof(Color), typeof(GameFont), typeof(TextAnchor)]);
			var height = codes.Where(c => c.opcode == OpCodes.Ldc_R4 && c.operand is float f && f == 78f).ToList();
			var below = codes.Where(c => c.opcode == OpCodes.Ldc_R4 && c.operand is float f && f == 26f).ToList();
			var lines = codes.Where(c => c.Calls(line)).ToList();
			if (height.Count != 1 || below.Count != 1 || lines.Count != 2)
			{
				Log.Error($"[Harmony3StrictCompat] UIR Architect DetailsPanel.Header: expected one 78, one 26 and two Line calls, found {height.Count}, {below.Count}, {lines.Count}; the name stays on one line");
				return codes;
			}
			lines[0].operand = AccessTools.Method(typeof(UirArchitectTwoLineNames), nameof(Title));
			var result = new List<CodeInstruction>(codes.Count + 2);
			foreach (var c in codes)
			{
				result.Add(c);
				if (c == height[0]) result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UirArchitectTwoLineNames), nameof(ColumnHeaderHeight))));
				if (c == below[0]) result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UirArchitectTwoLineNames), nameof(BelowTitle))));
			}
			return result;
		}

		// Runs only in the column layout (the strip layout takes its height from the strip), before the name is drawn.
		static float ColumnHeaderHeight(float height)
		{
			titleExtra = titlePending;
			titleInColumn = true;
			return height + titleExtra;
		}

		static float BelowTitle(float y) => y + titleExtra;

		// Same arguments as CoreWidgets.Line, which it replaces for the name only.
		static void Title(Rect rect, string text, Color color, GameFont font, TextAnchor anchor)
		{
			bool column = titleInColumn;
			titleInColumn = false;
			if (!column)
			{
				titleExtra = titlePending = 0f;
				oneLine(rect, text, color, font, anchor);
				return;
			}
			Text.Font = font;
			bool wraps = Text.CalcSize(text).x > rect.width;
			titlePending = wraps ? Text.LineHeightOf(font) : 0f;
			if (!wraps || titleExtra <= 0f)
			{
				Text.Font = GameFont.Small;
				oneLine(rect, text, color, font, anchor);
				return;
			}
			var area = new Rect(rect.x, rect.y, rect.width, rect.height + titleExtra);
			string cut = text.TruncateHeight(area.width, area.height);
			Text.Font = GameFont.Small;
			themeLabel(area, cut, TextAnchor.UpperLeft, color, font);
		}
	}

	// ---- Vanilla + Loading Progress: no sound sample on a sustainer that already ended --------------------------
	// A SubSustainer starts its first sample from a LongEventHandler.ExecuteWhenFinished action. Loading Progress
	// (InGameDeferredActionReplacement) runs that queue over several frames, so a sustainer created during a load
	// can end first: Sustainer.Cleanup has already destroyed its worldRootObject, and the queued StartSample then
	// asks the destroyed object for its transform. Waylith throws "Object is not tracked by the Unity world"
	// (3-4 errors on every load) and the sample GameObject made just before is left behind. Vanilla runs the queue
	// in one frame, where the destroy is still pending. An ended sustainer never plays again (SustainerUpdate is
	// skipped once Ended and its samples were already cleaned up), so starting no sample loses no sound.
	static class NoSampleOnEndedSustainer
	{
		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("Verse.Sound.SubSustainer:StartSample") is { } start)
				harmony.Patch(start, prefix: new HarmonyMethod(typeof(NoSampleOnEndedSustainer), nameof(NotEnded)));
		}

		static bool NotEnded(Verse.Sound.SubSustainer __instance) => __instance.parent is not { Ended: true };
	}

	// ---- Vanilla: one "Adapted" text per pawn per second (seen with Alpha Armoury's tesla coil trait) ----------
	// StunHandler.Notify_DamageApplied throws the floating "Adapted" text for every stun damage instance that
	// lands on a pawn that has adapted, with no limit. An EMP grenade is one instance. A tesla coil bolt is an EMP
	// bullet plus an EMP explosion of radius 1.9 at each of up to four impacts (three chain bounces), from a
	// burst weapon: a group of adapted mechanoids gets dozens of instances per burst, and the texts pile up into
	// a white blob on each of them. The damage is unchanged; the text shows at most once per second per cell.
	static class AdaptedTextOncePerSecond
	{
		const int Interval = 60;
		static readonly Dictionary<IntVec3, int> lastShown = new();

		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("RimWorld.StunHandler:Notify_DamageApplied") is { } applied)
				harmony.Patch(applied, transpiler: new HarmonyMethod(typeof(AdaptedTextOncePerSecond), nameof(Throttle)));
		}

		static IEnumerable<CodeInstruction> Throttle(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var throwText = AccessTools.Method(typeof(MoteMaker), nameof(MoteMaker.ThrowText), [typeof(Vector3), typeof(Map), typeof(string), typeof(Color), typeof(float)]);
			var calls = codes.Where(c => c.Calls(throwText)).ToList();
			if (calls.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] StunHandler.Notify_DamageApplied throws {calls.Count} texts instead of one, \"Adapted\" text not limited");
				return codes;
			}
			calls[0].operand = AccessTools.Method(typeof(AdaptedTextOncePerSecond), nameof(ThrowTextOncePerSecond));
			return codes;
		}

		// Same arguments as MoteMaker.ThrowText. The text is thrown one cell up and right of the pawn, so the cell
		// stands in for the pawn; a pawn that moved on gets its text again, which is rare enough.
		static void ThrowTextOncePerSecond(Vector3 loc, Map map, string text, Color color, float timeBeforeStartFadeout)
		{
			int now = Find.TickManager.TicksGame;
			var cell = new IntVec3((int)loc.x, map?.Index ?? 0, (int)loc.z);
			if (lastShown.TryGetValue(cell, out int shown) && (uint)(now - shown) < Interval) return;
			if (lastShown.Count > 512) lastShown.Clear();
			lastShown[cell] = now;
			MoteMaker.ThrowText(loc, map, text, color, timeBeforeStartFadeout);
		}
	}

	// ---- Vanilla: a blocked hit adds no disease (seen with Alpha Genes' plagued shamblers) -------------------
	// Pawn_HealthTracker.PostApplyDamage adds a damage def's additionalHediffs with severity damage dealt x
	// severityPerDamageDealt, scaled by the victim stat, and adds it when that is >= 0. PostApplyDamage runs for
	// every hit, also one the armor fully blocked (0 damage dealt) or one on a pawn with 100% of the scaling stat,
	// so the hediff is added at severity 0. Alpha Genes' "shambler plague" is discoverable: the add sends its
	// "Disease: Shambler plague" letter, and the next health tick removes the hediff (Severity <= 0), so the
	// pawn's health tab never shows it (seen 2026-11-21, two letters for an armored pawn). Only > 0 is added now.
	static class NoZeroSeverityDamageHediffs
	{
		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("Verse.Pawn_HealthTracker:PostApplyDamage") is { } post)
				harmony.Patch(post, transpiler: new HarmonyMethod(typeof(NoZeroSeverityDamageHediffs), nameof(OnlyPositive)));
		}

		// The test is "ldloc num; ldc.r4 0; blt.un skip", right before ldloc entry; ldfld entry.hediff.
		static IEnumerable<CodeInstruction> OnlyPositive(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var hediffField = AccessTools.Field(typeof(DamageDefAdditionalHediff), nameof(DamageDefAdditionalHediff.hediff));
			var tests = Enumerable.Range(2, Math.Max(0, codes.Count - 2))
				.Where(i => codes[i].LoadsField(hediffField) && (codes[i - 2].opcode == OpCodes.Blt_Un_S || codes[i - 2].opcode == OpCodes.Blt_Un)
					&& codes[i - 3].opcode == OpCodes.Ldc_R4 && (float)codes[i - 3].operand == 0f)
				.ToList();
			if (tests.Count != 1)
			{
				Log.Error($"[Harmony3StrictCompat] Pawn_HealthTracker.PostApplyDamage has {tests.Count} severity tests instead of one, zero-severity damage hediffs not skipped");
				return codes;
			}
			codes[tests[0] - 2].opcode = OpCodes.Ble_Un;
			return codes;
		}
	}

	// ---- Altered Carbon 2: childbirth is not held in a sleeve casket --------------------------------------------
	// The sleeve casket is a Building_Bed (a medical one, with the best tend bonus). Altered Carbon keeps it out of
	// ordinary bed searches by taking its def off RestUtility's sorted bed lists, but the childbirth ritual does
	// not use those lists: its targets, its "target still allowed" check and the mother role all ask
	// PregnancyUtility.IsUsableBedFor, which only runs RestUtility.IsValidBedFor, and that accepts the casket.
	// Started from the mother's own gizmo the ritual therefore picked a casket, and the attendant carried her
	// there (seen 2026-11-20, with "Could not reserve ... for job DeliverToBed ... AC_SleeveCasket" when a
	// doctor was rescuing her to a hospital bed at the same time). A sleeve casket is no bed for a birth.
	static class AcNoBirthInSleeveCasket
	{
		static Type casket;

		internal static void Init(Harmony harmony)
		{
			casket = AccessTools.TypeByName("AlteredCarbon.Building_SleeveCasket");
			if (casket != null && Harmony3StrictCompatMod.Target("RimWorld.PregnancyUtility:IsUsableBedFor") is { } usable)
				harmony.Patch(usable, postfix: new HarmonyMethod(typeof(AcNoBirthInSleeveCasket), nameof(NotACasket)));
		}

		static void NotACasket(Building_Bed bed, ref bool __result)
		{
			if (__result && casket.IsInstanceOfType(bed)) __result = false;
		}
	}

	// ---- MSS Flavour Pack: the Pondering Orb's hologram and things only a body can do --------------------------
	// The orb's hologram is a player-faction pawn with the colony's ideoligion. With the transhumanist meme it
	// gets "wants neural supercharge" and "wants a body modification" like any believer, but MSS filters the
	// hediffs a hologram can receive and no operation can be done on it. So it ran to the neural superchargers
	// over and over, emptied each charge for nothing (the supercharge hediff never sticks) and kept both mood
	// penalties for good. A hologram now never qualifies for supercharger auto-use and never gets those two
	// thoughts, and other believers do not hold its lack of a prosthetic against it. MSSFPHoloUtil.IsHolo is the mod's own test (a pawn with a live holo projector).
	static class MssHoloNoBodyNeeds
	{
		static Func<Pawn, bool> isHolo;

		internal static void Init(Harmony harmony)
		{
			var holo = AccessTools.Method("MSSFP.Holo.MSSFPHoloUtil:IsHolo");
			if (holo == null) return;
			isHolo = (Func<Pawn, bool>)holo.CreateDelegate(typeof(Func<Pawn, bool>));
			if (Harmony3StrictCompatMod.Target("RimWorld.CompNeuralSupercharger:CanAutoUse") is { } canUse)
				harmony.Patch(canUse, postfix: new HarmonyMethod(typeof(MssHoloNoBodyNeeds), nameof(NotForHolo)));
			foreach (string name in new[] { "RimWorld.ThoughtWorker_NeedNeuralSupercharge:ShouldHaveThought", "RimWorld.ThoughtWorker_Precept_HasNoProsthetic:ShouldHaveThought" })
				if (Harmony3StrictCompatMod.Target(name) is { } thought)
					harmony.Patch(thought, postfix: new HarmonyMethod(typeof(MssHoloNoBodyNeeds), nameof(NoThoughtForHolo)));
			if (Harmony3StrictCompatMod.Target("RimWorld.ThoughtWorker_Precept_HasNoProsthetic_Social:ShouldHaveThought") is { } social)
				harmony.Patch(social, postfix: new HarmonyMethod(typeof(MssHoloNoBodyNeeds), nameof(NoOpinionOfHolo)));
		}

		static void NotForHolo(Pawn pawn, ref bool __result)
		{
			if (__result && isHolo(pawn)) __result = false;
		}

		static void NoThoughtForHolo(Pawn p, ref ThoughtState __result)
		{
			if (__result.Active && isHolo(p)) __result = ThoughtState.Inactive;
		}

		// The social version: believers think less of anyone without a prosthetic. Not of a hologram.
		static void NoOpinionOfHolo(Pawn otherPawn, ref ThoughtState __result)
		{
			if (__result.Active && isHolo(otherPawn)) __result = ThoughtState.Inactive;
		}
	}

	// ---- 1trickPwnyta's Ideology Patch: pawn generation freeze from its "Look change desired" fix --------------
	// Its postfix on PawnStyleItemChooser.WantsToUseStyle asks LookChangeDesiredUtility.CanUseStyle, which (outside
	// the styling station) builds two lists over every hair, beard or tattoo def (genes allow it / the ideoligion
	// does not say Never) and intersects them, on every call. Vanilla RandomHairFor asks once per candidate hairstyle,
	// so each generated pawn costs (hair defs)^2 list work: a 207-shambler swarm froze the game 30 s, 27 s of it in
	// here (dotnet-trace, 2026-11-21). Same rule without lists: a style passes when the pawn's genes and ideoligion
	// both accept it; otherwise it passes only when no style of that kind satisfies both. The mod's styling-station
	// cache (one list shared by every pawn) is not kept; each answer is for the pawn asked.
	static class IpCanUseStyleNoLists
	{
		internal static void Init(Harmony harmony)
		{
			if (Harmony3StrictCompatMod.Target("IdeologyPatch.LookChangeDesiredFix.LookChangeDesiredUtility:CanUseStyle") is { } canUse)
				harmony.Patch(canUse, prefix: new HarmonyMethod(typeof(IpCanUseStyleNoLists), nameof(CanUseStyle)));
		}

		static bool CanUseStyle(Pawn pawn, StyleItemDef style, TattooType? tattooType, ref bool __result)
		{
			__result = style switch
			{
				HairDef => Allowed(pawn, style, DefDatabase<HairDef>.AllDefsListForReading, null),
				BeardDef => Allowed(pawn, style, DefDatabase<BeardDef>.AllDefsListForReading, null),
				TattooDef when tattooType.HasValue => Allowed(pawn, style, DefDatabase<TattooDef>.AllDefsListForReading, tattooType),
				_ => true,
			};
			return false;
		}

		static bool Allowed(Pawn pawn, StyleItemDef style, IReadOnlyList<StyleItemDef> all, TattooType? type)
		{
			if (Fits(pawn, style, type)) return true;
			for (int i = 0; i < all.Count; i++)
				if (Fits(pawn, all[i], type)) return false;
			return true;
		}

		static bool Fits(Pawn pawn, StyleItemDef s, TattooType? type) =>
			(type == null || ((TattooDef)s).tattooType == type.Value)
			&& (pawn.genes == null || pawn.genes.StyleItemAllowed(s))
			&& pawn.Ideo != null && pawn.Ideo.style.GetFrequency(s) != StyleItemFrequency.Never;
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
		internal delegate bool KanbanGet(IStoreSettingsParent owner, out int srt, out int ssl, out int mss);

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

		// Same sum as vanilla (room in same-def stacks + free slots), counted the way Kanban's cell check
		// (NoStorageBlockersIn) decides: a stack takes more only below the refill threshold (count*100 <= size*srt),
		// and new stacks only while the storage's similar stacks (each ceil(count/mss), over the building's slot group)
		// stay under the similar stack limit. Counting the limit per cell made HD see room in two linked animal feeders
		// that already held their one hay stack each: it picked cellar hay up, Kanban refused every feeder cell, and the
		// hay went straight back (6x net-zero, HD backoff, "yielded no actual job").
		static void CapSpace(IntVec3 c, Map map, ThingDef itemDef, ref int __result)
		{
			if (__result <= 0 || !hdInsideSpaceScan() || map.haulDestinationManager.SlotGroupAt(c) is not { } group
				|| !kanbanGet(group.Settings.owner, out int srt, out int ssl, out int mss)
				|| (mss <= 0 || mss >= itemDef.stackLimit) && ssl <= 0 && srt >= 100) return;
			int size = mss > 0 ? Math.Min(mss, itemDef.stackLimit) : itemDef.stackLimit;
			int room = 0, items = 0;
			foreach (var t in map.thingGrid.ThingsListAt(c))
			{
				if (t.def.category == ThingCategory.Item) items++;
				if (t.def == itemDef && t.stackCount * 100L <= (long)size * srt) room += Math.Max(size - t.stackCount, 0);
			}
			int newStacks = Math.Max(c.GetMaxItemsAllowedInCell(map) - items, 0);
			if (ssl > 0 && newStacks > 0)
			{
				int similar = 0;
				foreach (var cell in group.CellsList)
					foreach (var t in map.thingGrid.ThingsListAt(cell))
						if (t.def == itemDef)
							similar += mss > 0 ? (t.stackCount + mss - 1) / mss : 1;
				newStacks = Math.Min(newStacks, Math.Max(ssl - similar, 0));
			}
			__result = Math.Min(__result, room + newStacks * size);
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

	// ---- Kanban Stockpile Continue: similar stack limit shuttles unstackable meals in and out -----------------
	// Kanban's accept check counts "similar stacks" with CanStackWith; its "remove excess" job counts stacks per
	// def + stuff. Vanilla refuses to merge same-def meals whose ingredients differ in food kind or merge tags
	// (meat / vegetarian, insect meat ...). A storage with a similar stack limit therefore accepts a third fine
	// meal stack as "not similar", remove-excess carries it out as "over the limit", and the next haul brings it
	// back: a pawn runs the same meals between the capped fridge and the next storage. The accept check now counts
	// like the removal does: every other stack of the same def and stuff. Topping up a stack that can absorb the
	// item is a separate Kanban path and still works.
	// That top-up path had its own hole (2026-11-22, a steel shelf set to 1 stack holding 750 + 698): once a
	// storage holds more stacks than its limit, remove-excess hauls the surplus stack out one carry load at a
	// time, which leaves it partial, and the accept check lets any partial stack in the cell be topped up without
	// asking the limit. One pawn carries 75 out, the next brings 75 back: 71 deliveries to one shelf in 12
	// minutes, with free-space readings of 75, 73 and 52. Kanban's accept check and its haul count clamp both ask
	// KSUtil.CanStackIntoExistingAt first; that now answers "no" while the storage already holds more stacks of
	// the item's def and stuff than its limit, which sends the accept check to its similar stack count, where it
	// refuses. Remove-excess then drains the surplus and the storage settles. A thing that already lies in that
	// storage is exempt: moving or merging inside it adds no stack. How an extra stack gets in (two haulers
	// approved for the same partial stack) is not closed here; it is carried out once instead of forever.
	[StaticConstructorOnStartup]
	static class KanbanSimilarStackCount
	{
		static readonly HdKanbanStackCap.KanbanGet settings;
		static bool reported, toppedUp;

		static KanbanSimilarStackCount()
		{
			var count = Harmony3StrictCompatMod.Target("KanbanStockpileContinue.KSUtil:CountStoredSimilarStacks");
			Harmony3StrictCompatMod.Target("KanbanStockpileContinue.KSUtil:IsSimilarStack");    // fingerprint only: the two rules that disagree
			Harmony3StrictCompatMod.Target("KanbanStockpileContinue.KSUtil:BuildSslExcessSet");
			if (count == null) return;
			var harmony = new Harmony("sammael.harmony3strictcompat");
			harmony.Patch(count, postfix: new HarmonyMethod(typeof(KanbanSimilarStackCount), nameof(CountSameDef)));
			var topUp = Harmony3StrictCompatMod.Target("KanbanStockpileContinue.KSUtil:CanStackIntoExistingAt");
			var get = Harmony3StrictCompatMod.Target("KanbanStockpileContinue.KanbanStockpileAPI:TryGetSettings");
			Harmony3StrictCompatMod.Target("KanbanStockpileContinue.StoreUtility_NoStorageBlockersIn_Patch:Postfix");   // fingerprint only: the accept check that asks topUp first
			if (topUp == null || get == null) return;
			settings = (HdKanbanStackCap.KanbanGet)get.CreateDelegate(typeof(HdKanbanStackCap.KanbanGet));
			harmony.Patch(topUp, postfix: new HarmonyMethod(typeof(KanbanSimilarStackCount), nameof(NoTopUpOverLimit)));
			// The absorb prefix is only fingerprinted: it is why a stack at its Kanban size can be left out below.
			if (Harmony3StrictCompatMod.Target("KanbanStockpileContinue.KSUtil:TryGetStackRefillThresholdDesired") is { } wish
				&& Harmony3StrictCompatMod.Target("KanbanStockpileContinue.ThingUtility_TryAbsorbStackNumToTake_Patch:Prefix") != null)
				harmony.Patch(wish, postfix: new HarmonyMethod(typeof(KanbanSimilarStackCount), nameof(TopUpBesideFullStack)));
		}

		// Two or more stacks allowed with a stack size below the item's own stack limit (2026-11-22: steel, limit
		// 750, shelf set to 2 stacks of 500, stuck at 500 + 240 and 500 + 205). Kanban's top-up wish for a cell is
		// the smallest wish of every stack of that kind below the item's stack limit, because it cannot choose
		// which stack absorbs the delivery. A stack sitting at the Kanban size is below the item's limit, wishes 0
		// and wins, so the cell wants nothing and the later stacks keep only their first delivery. Kanban's own
		// absorb patch refuses to add to a stack at its size, so such a stack has no say: when the wish came out
		// as 0, it is taken again over the stacks still below their Kanban size. Refill threshold as in Kanban.
		static void TopUpBesideFullStack(IntVec3 cell, SlotGroup slotGroup, Map map, Thing thing, ref int numDesired, bool __result)
		{
			if (!__result || numDesired > 0 || slotGroup == null
				|| !settings(slotGroup.Settings.owner, out int srt, out _, out int mss) || mss <= 0 || mss >= thing.def.stackLimit) return;
			int wish = int.MaxValue;
			var things = map.thingGrid.ThingsListAt(cell);
			for (int i = 0; i < things.Count; i++)
			{
				var t = things[i];
				if (!t.def.EverStorable(false) || !t.CanStackWith(thing)) continue;
				int size = Math.Min(t.def.stackLimit, mss);
				if (t.stackCount >= size) continue;
				wish = Math.Min(wish, t.stackCount * 100L > (long)size * srt ? 0 : size - t.stackCount);
			}
			if (wish == int.MaxValue || wish <= 0) return;
			numDesired = wish;
			if (toppedUp) return;
			toppedUp = true;
			Log.Message($"[Harmony3StrictCompat] Kanban: {slotGroup.parent} may take {wish} more {thing.def.defName} into a partial stack beside one already at its stack size of {mss}");
		}

		static void NoTopUpOverLimit(IntVec3 c, Map map, Thing thing, ref bool __result)
		{
			if (!__result || map.haulDestinationManager.SlotGroupAt(c) is not { } group
				|| !settings(group.Settings.owner, out _, out int ssl, out _) || ssl <= 0) return;
			if (thing.Spawned && thing.Map == map && map.haulDestinationManager.SlotGroupAt(thing.Position) == group) return;
			int stacks = 0;
			var cells = group.CellsList;
			for (int i = 0; i < cells.Count; i++)
			{
				var things = map.thingGrid.ThingsListAt(cells[i]);
				for (int j = 0; j < things.Count; j++)
				{
					var t = things[j];
					if (t == thing || t.def != thing.def || t.Stuff != thing.Stuff || ++stacks <= ssl) continue;
					__result = false;
					if (!reported)
					{
						reported = true;
						Log.Message($"[Harmony3StrictCompat] Kanban: {group.parent} holds more than its {ssl} allowed stack(s) of {thing.def.defName}; nothing is added until the surplus is hauled out");
					}
					return;
				}
			}
		}

		static void CountSameDef(SlotGroup slotGroup, Map map, Thing thing, int max, ref int __result)
		{
			if (__result >= max || thing == null || !thing.def.EverStorable(false)) return;
			int stacks = 0;
			var cells = slotGroup.CellsList;
			for (int i = 0; i < cells.Count; i++)
			{
				var things = map.thingGrid.ThingsListAt(cells[i]);
				for (int j = 0; j < things.Count; j++)
				{
					var t = things[j];
					if (t == thing || t.def != thing.def || t.Stuff != thing.Stuff || ++stacks < max) continue;
					__result = stacks;
					return;
				}
			}
			if (stacks > __result) __result = stacks;
		}
	}

	// ---- Ritual sacrifice of an innocent prisoner is not an "execution of an innocent" ------------------------
	// Vanilla records ExecutedPrisoner / ExecutedPrisonerInnocent for a ritual sacrifice like for any execution, so
	// an ideoligion with a sacrifice ritual and "Execution: horrible if innocent" hates its own moral guide for
	// holding the ritual (-30 opinion from every member, mood hits). Alpha Memes' heart extraction also kills with
	// plain Cut damage instead of ExecutionCut, so the same death counts a second time as a prisoner who died
	// through neglect (InnocentPrisonerDied, another -15). While the killer is in a ritual: an innocent prisoner's
	// death records no execution events, and the death thoughts see execution damage, as vanilla sacrifice passes.
	// Guilty prisoners and executions outside rituals are unchanged. Only vanilla methods are patched; if Alpha
	// Memes switches to ExecutionCut the second half becomes a no-op.
	[StaticConstructorOnStartup]
	static class RitualSacrificeIsNoExecution
	{
		static RitualSacrificeIsNoExecution()
		{
			var harmony = new Harmony("sammael.harmony3strictcompat");
			harmony.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.GiveThoughtsForPawnExecuted)),
				prefix: new HarmonyMethod(typeof(RitualSacrificeIsNoExecution), nameof(SkipInnocentInRitual)));
			harmony.Patch(AccessTools.Method(typeof(PawnDiedOrDownedThoughtsUtility), nameof(PawnDiedOrDownedThoughtsUtility.TryGiveThoughts),
					new[] { typeof(Pawn), typeof(DamageInfo?), typeof(PawnDiedOrDownedThoughtsKind) }),
				prefix: new HarmonyMethod(typeof(RitualSacrificeIsNoExecution), nameof(RitualKillIsExecutionDamage)));
		}

		static bool InRitual(Pawn pawn) => pawn?.GetLord()?.LordJob is LordJob_Ritual;

		static bool SkipInnocentInRitual(Pawn victim, Pawn executioner)
		{
			if (!victim.RaceProps.Humanlike || !victim.IsPrisoner || victim.guilt.IsGuilty || !InRitual(executioner)) return true;
			if (executioner.Faction != null) executioner.Faction.lastExecutionTick = Find.TickManager.TicksGame;
			return false;
		}

		static void RitualKillIsExecutionDamage(Pawn victim, ref DamageInfo? dinfo, PawnDiedOrDownedThoughtsKind thoughtsKind)
		{
			if (thoughtsKind != PawnDiedOrDownedThoughtsKind.Died || dinfo is not { } d || d.Def.execution
				|| !victim.IsPrisonerOfColony || !InRitual(d.Instigator as Pawn)) return;
			d.Def = DamageDefOf.ExecutionCut;
			dinfo = d;
		}
	}

	// ---- Gatherings: RimPacts' expo party host, and why a gathering was called off ---------------------------
	// RimPacts' ForceExpoParty makes the first spawned colonist that is not downed or in a mental state the party
	// organizer. Vanilla calls a party off when its organizer leaves it, and a pawn whose schedule forbids
	// recreation (or who is asleep, starving, bleeding ...) leaves on its next think: "The party has been called
	// off." a moment after it started, with no reason. Pick the organizer by vanilla's own rules
	// (GatheringsUtility.FindRandomGatheringOrganizer), fall back to RimPacts' choice when nobody qualifies, let the
	// host of any gathering stay through its own schedule, and append the reason to every called-off message.
	[StaticConstructorOnStartup]
	static class GatheringCalledOffReason
	{
		static readonly AccessTools.FieldRef<LordJob_Joinable_Gathering, GatheringDef> gatheringDef =
			AccessTools.FieldRefAccess<LordJob_Joinable_Gathering, GatheringDef>("gatheringDef");

		static GatheringCalledOffReason()
		{
			var harmony = new Harmony("sammael.harmony3strictcompat");
			harmony.Patch(AccessTools.Method(typeof(TransitionAction_Message), nameof(TransitionAction_Message.DoAction)),
				prefix: new HarmonyMethod(typeof(GatheringCalledOffReason), nameof(AppendReason)));
			harmony.Patch(AccessTools.Method(typeof(LordJob_Joinable_Gathering), nameof(LordJob_Joinable_Gathering.VoluntaryJoinPriorityFor)),
				postfix: new HarmonyMethod(typeof(GatheringCalledOffReason), nameof(HostIgnoresSchedule)));
			var force = Harmony3StrictCompatMod.Target("RimPacts.WorldComponent_RimPacts:ForceExpoParty");
			if (force != null)
				harmony.Patch(force, transpiler: new HarmonyMethod(typeof(GatheringCalledOffReason), nameof(VanillaOrganizer)));
		}

		static IEnumerable<CodeInstruction> VanillaOrganizer(IEnumerable<CodeInstruction> instructions)
		{
			bool done = false;
			foreach (var code in instructions)
			{
				// the first FirstOrDefault(colonists, predicate) picks the organizer, the second one a guest
				if (!done && code.operand is MethodInfo { Name: "FirstOrDefault" } m && m.GetParameters().Length == 2
					&& m.GetParameters()[1].ParameterType == typeof(Predicate<Pawn>))
				{
					code.opcode = OpCodes.Call;
					code.operand = AccessTools.Method(typeof(GatheringCalledOffReason), nameof(PickOrganizer));
					done = true;
				}
				yield return code;
			}
			if (!done)
				Log.Error("[Harmony3StrictCompat] RimPacts ForceExpoParty: organizer pick not found, expo party host fix skipped");
		}

		static Pawn PickOrganizer(IEnumerable<Pawn> colonists, Predicate<Pawn> rimPacts)
		{
			var all = colonists.Where(p => rimPacts(p)).ToList();
			return all.Where(p => p.DevelopmentalStage.Adult() && !p.InBed() && p.GetLord() == null
					&& GatheringsUtility.ShouldPawnKeepGathering(p, GatheringDefOf.Party)
					&& GatheringsUtility.PawnCanStartOrContinueGathering(p)).RandomElementWithFallback()
				?? all.FirstOrDefault();
		}

		// A party lasts 2-6 hours, but the host's schedule is only checked when it starts: a host whose block turns
		// to work or sleep leaves, and vanilla calls the whole party off. The host keeps hosting through its own
		// schedule; every other reason to leave (asleep, starving, bleeding ...) still applies.
		static void HostIgnoresSchedule(LordJob_Joinable_Gathering __instance, Pawn p, ref float __result)
		{
			if (__result > 0f || p != __instance.Organizer || __instance.lord?.ownedPawns.Contains(p) != true) return;
			var def = gatheringDef(__instance);
			if (def == null || !def.respectTimetable || p.timetable == null || p.timetable.CurrentAssignment.allowJoy) return;
			if (!GatheringsUtility.ShouldGuestKeepAttendingGathering(p) || __instance.Spot.IsForbidden(p)) return;
			if (p.health.hediffSet.hediffs.Any(h => h.def.blocksSocialInteraction)) return;
			__result = VoluntarilyJoinableLordJobJoinPriorities.SocialGathering;
		}

		// The called-off transition ends the lord, so the changed text is never shown twice.
		static void AppendReason(TransitionAction_Message __instance, Transition trans)
		{
			if (__instance.type == MessageTypeDefOf.NegativeEvent && !__instance.message.NullOrEmpty()
				&& trans.target?.lord?.LordJob is LordJob_Joinable_Gathering job)
				__instance.message = __instance.message.TrimEnd('.') + ": " + Reason(job, trans.target.lord.Map) + ".";
		}

		// Mirrors LordJob_Joinable_Gathering.ShouldBeCalledOff and VoluntaryJoinPriorityFor, in their order.
		static string Reason(LordJob_Joinable_Gathering job, Map map)
		{
			if (map != null && !GatheringsUtility.AcceptableGameConditionsToContinueGathering(map)) return "the colony is in danger";
			var p = job.Organizer;
			if (p == null) return "someone attending died";
			string host = "the host " + p.LabelShort + " ";
			if (p.Dead) return host + "died";
			if (!p.Spawned) return host + "is gone";
			if (p.Downed) return host + "is downed";
			if (p.Drafted) return host + "was drafted";
			if (p.InMentalState) return host + "is in a mental state (" + p.MentalStateDef.label + ")";
			if (p.health.hediffSet.BleedRateTotal > 0f) return host + "is bleeding";
			if (!GatheringsUtility.PawnCanStartOrContinueGathering(p)) return host + "cannot host a gathering (prisoner, slave, blood loss ...)";
			var def = gatheringDef(job);
			if (def != null && def.respectTimetable && p.timetable != null && !p.timetable.CurrentAssignment.allowJoy)
				return host + "is scheduled for " + p.timetable.CurrentAssignment.label;
			if (p.needs?.food != null && p.needs.food.Starving) return host + "is starving";
			if (p.needs?.rest != null && (int)p.needs.rest.CurCategory >= 3) return host + "is exhausted";
			if (p.health.hediffSet.HasTendableNonInjuryNonMissingPartHediff()) return host + "needs treatment";
			if (p.health.hediffSet.InLabor()) return host + "is in labor";
			if (!p.Awake()) return host + "fell asleep";
			if (job.Spot.IsForbidden(p)) return "the spot is forbidden to the host " + p.LabelShort;
			var blocker = p.health.hediffSet.hediffs.FirstOrDefault(h => h.def.blocksSocialInteraction);
			if (blocker != null) return host + "has " + blocker.def.label;
			return host + "left for something more important, or someone attending died";
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

	// ---- Vanilla: stale hypothermia / heatstroke on reused world pawns --------------------------------------
	// Hypothermia and heatstroke have no comps, death chance or hediff givers, so WorldPawns mothballs a pawn that
	// carries them, and a mothballed pawn only ages (Pawn.TickMothballed). The hediff stays frozen until the pawn
	// is reused, possibly a year later: a trade caravan or visitor group then arrives with a member above 15% and
	// leaves at once "because of dangerous temperature" (Trigger_PawnExperiencingDangerousTemperatures), even at
	// a comfortable 16 C. Off the map vanilla treats a pawn as warm (HediffGiver_Hypothermia/Heat recover when it
	// is not spawned), so apply that same recovery for the mothballed time. Caravan members are never mothballed;
	// their hediffs keep ticking as before.
	[StaticConstructorOnStartup]
	static class MothballedTemperatureRecovery
	{
		static readonly HediffDef hypothermicSlowdown = DefDatabase<HediffDef>.GetNamedSilentFail("HypothermicSlowdown");

		static MothballedTemperatureRecovery()
		{
			new Harmony("sammael.harmony3strictcompat").Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.TickMothballed)),
				postfix: new HarmonyMethod(typeof(MothballedTemperatureRecovery), nameof(Recover)));
		}

		static void Recover(Pawn __instance, int interval)
		{
			var hediffs = __instance.health?.hediffSet?.hediffs;
			if (hediffs == null) return;
			for (int i = hediffs.Count - 1; i >= 0; i--)
			{
				var h = hediffs[i];
				if (h.def != HediffDefOf.Hypothermia && h.def != HediffDefOf.Heatstroke && h.def != hypothermicSlowdown) continue;
				// Vanilla off-map step, once per 60-tick giver interval.
				float severity = h.Severity;
				for (int n = interval / 60; n > 0 && severity > 0f; n--)
					severity -= Mathf.Clamp(severity * 0.027f, 0.0015f, 0.015f);
				if (severity <= 0f)
					__instance.health.RemoveHediff(h);
				else
					h.Severity = severity;
			}
		}
	}

	// ---- Catalog Tab (Colony Tabs Framework) + UI Rethemed: the "C" jump button ----
	// Colony Tabs Framework draws each registered info button (Catalog Tab's "C": open this thing in the catalog)
	// from two postfixes, as its own box at the game's coordinates:
	//  - Inspect pane (InfoButtons.DrawInspect, after DoInspectPaneButtons): UI Rethemed - Colonist Card runs that
	//    method for its card header and moves only Widgets.ButtonImage buttons into its 24 px slots, so the box
	//    stayed at the game's spot, between and under the slotted buttons.
	//  - Info card (InfoButtons.DrawCard, after Dialog_InfoCard.DoWindowContents): UI Rethemed - Info Card draws the
	//    whole window itself, so the box landed on the themed title text. That title bar is also a drag grip that
	//    uses the mouse-down (InfoCardWindow.Grip) before the postfix runs, so the click never reached the button.
	// While UI Rethemed is drawing, the framework's own draw is skipped and each shown button is drawn as a UI
	// Rethemed header button holding its letter: in the card header's next slot, and left of the info card's title
	// tools (Compare, materials, back), which are drawn before the grip. A click goes through the framework's own
	// InfoButtons.Press. With the theming off (setting, failsafe) the framework draws as before.
	static class CatalogButtonsInUir
	{
		struct Button
		{
			public string key, letter;
			public Func<ThingDef, bool> shows;
			public Func<ThingDef, string> tip;
		}

		static FieldInfo registered;
		static object readFrom;
		static Button[] buttons = [];
		static Func<string, ThingDef, bool, bool> press;
		static Func<Thing, ThingDef> defFor;
		static Func<Rect, Texture, Color, string, string, float, bool> headerButton;
		static Action<Rect, string, TextAnchor, Color, GameFont> label;
		static Func<Color> hoverFill, textDim;
		static Action<Action> later;
		static Func<Window, bool> cardThemed;
		static AccessTools.FieldRef<Dialog_InfoCard, Thing> cardThing;
		static AccessTools.FieldRef<Dialog_InfoCard, Def> cardDef;
		static Rect titleRoom;

		// A lookup that throws (an update adds an overload) must not end the mod constructor: on 2026-11-22 an
		// ambiguous HeaderButton lookup here did, and the add-on DLLs then failed to load too.
		internal static void Init(Harmony harmony)
		{
			try { Apply(harmony); }
			catch (Exception e) { Log.Error("[Harmony3StrictCompat] Catalog button in UI Rethemed not applied: " + e); }
		}

		static void Apply(Harmony harmony)
		{
			var kit = AccessTools.TypeByName("DOORAJP.ColonyKit.InfoButtons");
			var theme = AccessTools.TypeByName("UIRethemed.Core.Theme");
			if (kit == null || theme == null) return;
			registered = AccessTools.Field(kit, "buttons");
			press = Bind<Func<string, ThingDef, bool, bool>>(AccessTools.Method(kit, "Press", [typeof(string), typeof(ThingDef), typeof(bool)]));
			defFor = Bind<Func<Thing, ThingDef>>(AccessTools.Method(kit, "DefFor", [typeof(Thing)]));
			headerButton = Bind<Func<Rect, Texture, Color, string, string, float, bool>>(AccessTools.Method("UIRethemed.Core.CoreWidgets:HeaderButton", [typeof(Rect), typeof(Texture), typeof(Color), typeof(string), typeof(string), typeof(float)]));
			label = Bind<Action<Rect, string, TextAnchor, Color, GameFont>>(AccessTools.Method(theme, "Label", [typeof(Rect), typeof(string), typeof(TextAnchor), typeof(Color), typeof(GameFont)]));
			hoverFill = Bind<Func<Color>>(AccessTools.PropertyGetter(theme, "HighlightGray"));
			textDim = Bind<Func<Color>>(AccessTools.PropertyGetter(theme, "TextDim"));
			later = Bind<Action<Action>>(AccessTools.Method("UIRethemed.Core.Failsafe:Later", [typeof(Action)]));
			if (registered == null || press == null || defFor == null || headerButton == null || label == null || hoverFill == null || textDim == null || later == null)
			{
				Log.Error("[Harmony3StrictCompat] Catalog button in UI Rethemed: Colony Tabs Framework or UI Rethemed - Core no longer has what this fix calls, not applied");
				return;
			}
			if (Harmony3StrictCompatMod.uirccThemingHeader != null && Harmony3StrictCompatMod.Target("DOORAJP.ColonyKit.InfoButtons:DrawInspect") is { } drawInspect)
				harmony.Patch(drawInspect, prefix: new HarmonyMethod(typeof(CatalogButtonsInUir), nameof(InspectInHeaderSlots)));
			var themed = Harmony3StrictCompatMod.Target("UIRethemed.InfoCard.InfoCardWindow:Themed");
			var titleTools = Harmony3StrictCompatMod.Target("UIRethemed.InfoCard.InfoCardWindow:TitleTools");
			if (themed == null || titleTools == null || Harmony3StrictCompatMod.Target("DOORAJP.ColonyKit.InfoButtons:DrawCard") is not { } drawCard) return;
			cardThemed = Bind<Func<Window, bool>>(themed);
			cardThing = AccessTools.FieldRefAccess<Dialog_InfoCard, Thing>("thing");
			cardDef = AccessTools.FieldRefAccess<Dialog_InfoCard, Def>("def");
			if (cardThemed == null)
			{
				Log.Error("[Harmony3StrictCompat] Catalog button in UI Rethemed: InfoCardWindow.Themed changed its signature, info card part not applied");
				return;
			}
			harmony.Patch(drawCard, prefix: new HarmonyMethod(typeof(CatalogButtonsInUir), nameof(CardNotThemed)));
			harmony.Patch(titleTools, postfix: new HarmonyMethod(typeof(CatalogButtonsInUir), nameof(CardTitleTools)));
		}

		// Null when the method is gone or its signature changed.
		static T Bind<T>(MethodInfo method) where T : Delegate =>
			method == null ? null : Delegate.CreateDelegate(typeof(T), method, throwOnBindFailure: false) as T;

		// The framework replaces its button array on every Register, so the typed copy is rebuilt only then.
		static Button[] Buttons()
		{
			var now = registered.GetValue(null);
			if (now == readFrom) return buttons;
			readFrom = now;
			var list = new List<Button>();
			foreach (var b in (Array)now)
			{
				var t = b.GetType();
				list.Add(new Button
				{
					key = (string)AccessTools.Field(t, "key").GetValue(b),
					letter = (string)AccessTools.Field(t, "letter").GetValue(b),
					shows = (Func<ThingDef, bool>)AccessTools.Field(t, "shows").GetValue(b),
					tip = (Func<ThingDef, string>)AccessTools.Field(t, "tip").GetValue(b),
				});
			}
			return buttons = list.ToArray();
		}

		static void Draw(ThingDef def, bool fromCard, Func<Rect> slot)
		{
			if (def == null) return;
			foreach (var b in Buttons())
			{
				bool shown;
				try { shown = b.shows(def); }
				catch { shown = false; }
				if (!shown) continue;
				Rect rect = slot();
				bool over = Mouse.IsOver(rect);
				string tip = null;
				if (over && b.tip != null)
					try { tip = b.tip(def); }
					catch { }
				bool clicked = headerButton(rect, null, hoverFill(), null, tip, 16f);
				label(rect, b.letter, TextAnchor.MiddleCenter, over ? Color.white : textDim(), GameFont.Small);
				if (!clicked) continue;
				Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High);
				string key = b.key;
				later(() => press(key, def, fromCard));   // closes the card / switches the main tab: after the draw, as UI Rethemed does
			}
		}

		// Prefix on InfoButtons.DrawInspect.
		static bool InspectInHeaderSlots()
		{
			if (!Harmony3StrictCompatMod.uirccThemingHeader()) return true;
			if (Find.Selector.NumSelected == 1)
				Draw(defFor(Find.Selector.SingleSelectedThing), false, Harmony3StrictCompatMod.uirccNextSlot);
			return false;
		}

		// Prefix on InfoButtons.DrawCard.
		static bool CardNotThemed(Dialog_InfoCard card) => !cardThemed(card);

		// Postfix on InfoCardWindow.TitleTools. Its result is the room left of its own tools; they go right to left
		// from its right edge, 24 px each with a 4 px gap, and so do these.
		// ponytail: only checks that the first button fits; a second registered button could run under a long title.
		static void CardTitleTools(Dialog_InfoCard card, ref Rect __result)
		{
			if (__result.width < 28f) return;
			var thing = cardThing(card);
			titleRoom = __result;
			Draw(thing != null ? defFor(thing) : cardDef(card) as ThingDef, true, NextTitleSlot);
			__result = titleRoom;
		}

		static Rect NextTitleSlot()
		{
			var slot = new Rect(titleRoom.xMax - 24f, Mathf.Round(titleRoom.center.y - 12f), 24f, 24f);
			titleRoom.xMax = slot.x - 4f;
			return slot;
		}
	}

	// ---- Character Development: "live in a new settlement" is fulfilled for the pawns who settle ----------------
	// Its postfix on SettleUtility.AddNewHome completes the want for every player pawn spawned on any map and in
	// any caravan, wherever the new settlement is. A gravship landing on an empty tile calls AddNewHome while the
	// crew is still held inside the Gravship object (on no map, in no caravan), so the crew got nothing and the
	// pawns left on a kept map got the want (seen 2026-11-22: a child on the home map, which Vanilla Gravship
	// Expanded keeps after takeoff; any second colony does the same). The want now goes to the player pawns at
	// the new settlement's tile: on its map, in a caravan standing on it, or aboard a gravship landing on it.
	static class NewSettlementWantForSettlers
	{
		static Func<Pawn, bool> canHaveWants;
		static MethodInfo checkWants;
		static object newSettlement;

		internal static void Init(Harmony harmony)
		{
			var utility = AccessTools.TypeByName("WantsAndQuirks.WantsAndQuirksUtility");
			var context = AccessTools.TypeByName("WantsAndQuirks.WantWorkerContext");
			var trigger = AccessTools.TypeByName("WantsAndQuirks.WantTriggerType");
			if (utility == null || context == null || trigger == null) return;
			var canHave = AccessTools.Method(utility, "CanHaveWants", [typeof(Pawn)]);
			checkWants = AccessTools.Method(utility, "CheckWants", [typeof(Pawn), context]);
			var triggerType = AccessTools.Field(context, "triggerType");
			if (canHave == null || checkWants == null || triggerType == null || !Enum.IsDefined(trigger, "NewSettlement"))
			{
				Log.Error("[Harmony3StrictCompat] Character Development's want API changed: redo the new settlement want fix");
				return;
			}
			if (Harmony3StrictCompatMod.Target("WantsAndQuirks.SettleUtility_AddNewHome_Patch:Postfix") is not { } theirs
				|| Harmony3StrictCompatMod.Target("RimWorld.Planet.SettleUtility:AddNewHome") is not { } addNewHome) return;
			canHaveWants = (Func<Pawn, bool>)canHave.CreateDelegate(typeof(Func<Pawn, bool>));
			newSettlement = Activator.CreateInstance(context);
			triggerType.SetValue(newSettlement, Enum.Parse(trigger, "NewSettlement"));
			harmony.Patch(theirs, prefix: new HarmonyMethod(typeof(NewSettlementWantForSettlers), nameof(Skip)));
			harmony.Patch(addNewHome, postfix: new HarmonyMethod(typeof(NewSettlementWantForSettlers), nameof(Settled)));
		}

		static bool Skip() => false;

		static void Settled(PlanetTile tile, Faction faction)
		{
			if (faction != Faction.OfPlayer) return;
			foreach (var pawn in Settlers(tile))
				if (canHaveWants(pawn)) checkWants.Invoke(null, [pawn, newSettlement]);
		}

		// A list, not a lazy sequence: completing a want can spawn or change things.
		internal static List<Pawn> Settlers(PlanetTile tile)
		{
			var settlers = new List<Pawn>();
			foreach (var map in Find.Maps)
				if (map.Tile == tile) settlers.AddRange(map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer));
			foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
			{
				if (worldObject is Caravan caravan && caravan.Faction == Faction.OfPlayer && caravan.Tile == tile)
					settlers.AddRange(caravan.PawnsListForReading);
				else if (worldObject is Gravship flying && flying.destinationTile == tile)
					AddCrew(settlers, flying);
			}
			// A gravship that lands through the landing designator is no world object at that point.
			if (Current.Game.Gravship is { } gravship && gravship.destinationTile == tile)
				AddCrew(settlers, gravship);
			return settlers;
		}

		static void AddCrew(List<Pawn> settlers, Gravship gravship)
		{
			foreach (var pawn in gravship.Pawns)
				if (pawn.Faction == Faction.OfPlayer && !settlers.Contains(pawn)) settlers.Add(pawn);
		}
	}

	// ---- Kanban Stockpile Continue: storage settings without an owner no longer share one Kanban slot -----------
	// Kanban keeps its three numbers (refill threshold, similar stack limit, max stack size) per owner key, and
	// every StorageSettings object that has no owner falls back to the single key "___clipboard": the real
	// copy/paste clipboard, but also every def's default storage settings, a frame's settings, an auto-rebuild
	// blueprint's settings and a flying gravship's stockpiles and storage groups. A new storage blueprint copies
	// from its def's defaults, so it read that slot: after one Copy (or one frame built from a configured
	// blueprint) every storage built afterwards started with those numbers, e.g. 1 stack / 500 (seen 2026-11-22,
	// together with sarcophagi, biosculpter pods and mortars carrying Kanban numbers nobody set). The slot also
	// went into every ownerless settings node of the save, so it survived a reload, and gravship stockpiles all
	// took the numbers of the one copied last. Each ownerless object now gets a key of its own in both places
	// Kanban uses the fallback (its CopyFrom hook and its ExposeData postfix): def defaults carry no Kanban
	// numbers, and clipboard, frame and gravship copies each carry only what was copied into them.
	// Ceiling: Kanban's table keeps the entry of an ownerless object that is gone (a finished frame) until the next
	// game load; a few ints each. Give the key object a finalizer queue if that ever matters.
	static class KanbanOwnerlessSettings
	{
		static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StorageSettings, string> keys = new();
		static int next;

		internal static void Init(Harmony harmony)
		{
			foreach (string name in new[] { "KanbanStockpileContinue.StorageSettings_CopyFrom_Patch:CopyFrom", "KanbanStockpileContinue.StorageSettings_ExposeData_Patch:Postfix" })
				if (Harmony3StrictCompatMod.Target(name) is { } method)
					harmony.Patch(method, transpiler: new HarmonyMethod(typeof(KanbanOwnerlessSettings), nameof(OwnKeyPerObject)));
		}

		internal static string Key(StorageSettings settings) =>
			settings == null ? "___clipboard" : keys.GetValue(settings, _ => "___ownerless" + ++next);

		// Kanban writes: OwnerKey(settings?.owner) ?? "___clipboard". The string load becomes Key(settings), with
		// the same argument load that fed the owner read just before it.
		static IEnumerable<CodeInstruction> OwnKeyPerObject(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			var codes = instructions.ToList();
			var owner = AccessTools.Field(typeof(StorageSettings), "owner");
			int load = -1, done = 0;
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].LoadsField(owner)) load = i - 1;
				else if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand as string == "___clipboard" && load >= 0 && codes[load].IsLdarg())
				{
					codes[i].opcode = codes[load].opcode;
					codes[i].operand = codes[load].operand;
					codes.Insert(++i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(KanbanOwnerlessSettings), nameof(Key))));
					done++;
				}
			}
			int expected = original.Name == "CopyFrom" ? 2 : 1;
			if (done != expected)
				Log.Error($"[Harmony3StrictCompat] Kanban {original.DeclaringType?.Name}.{original.Name} shape changed ({done} clipboard key fallbacks, expected {expected}): ownerless storage settings still share one Kanban slot");
			return codes;
		}
	}

	// ---- IMPACT - Combat Overhaul: the per-shot damage floor is scaled by rate of fire ----------------------------
	// IMPACT's weapon equalization clamps every ranged weapon's damage per projectile onto a band per tech tier
	// (spacer 20 to 36) and looks at neither burst size nor cycle time. A weapon balanced as many weak hits is
	// lifted to the floor on every hit: Sparkling Worlds' Bulletstorm (3 damage, ten shots a second) became 20
	// damage and 200 damage per second; Apex Mechanoids' Beam Witherer (1 damage, 15-shot burst) became 20 x 15
	// and took a fully armored colonist to 1 torso hit point in one burst (seen 2026-11-23). The floor now stands
	// for damage per second of fire: a weapon firing more than ReferenceShotsPerSecond gets the floor divided by
	// its rate, never below the damage its own mod gave it. Weapons at or below that rate, the upper clamp, armor
	// penetration, range and projectile speed stay as IMPACT set them. Turret guns are skipped (IMPACT leaves
	// their damage alone). A projectile shared by several weapons takes the fastest of them.
	// A weapon whose own mod gave it a third of the tier floor or less is not lifted at all: damage is not what it
	// is for. That covers the non-lethal and utility guns (VWE Non-Lethal's dart gun 3, a tranquilizer blowgun 3,
	// Sparkling Worlds' pain gun 1, EMP and toxin dart guns, Eltex psycaster guns 6), which IMPACT turned into
	// 12 to 26 damage weapons, and the many-weak-hits designs above (Beam Witherer back to 1 per hit).
	// Rate = burst / (warmup + cooldown + time between burst shots), from the def; the shooter's aiming and
	// cooldown factors are not in it.
	// A file named "weaponsurvey.on" in the mod folder writes every equalized weapon to Logs/impact_weapons.tsv.
	static class ImpactFloorByFireRate
	{
		const float ReferenceShotsPerSecond = 1f;
		const int TokenDamageDivisor = 3;
		static readonly Dictionary<ProjectileProperties, int> original = new();
		static readonly AccessTools.FieldRef<ProjectileProperties, int> damageBase = AccessTools.FieldRefAccess<ProjectileProperties, int>("damageAmountBase");
		static MethodInfo shouldEqualize, getClamps, tierOf;
		static string modRoot;

		internal static void Init(Harmony harmony, string root)
		{
			if (Harmony3StrictCompatMod.Target("Impact.WeaponEqualizer:Run") is not { } run) return;
			Harmony3StrictCompatMod.Target("Impact.WeaponEqualizer:Equalize");
			shouldEqualize = Harmony3StrictCompatMod.Target("Impact.WeaponEqualizer:ShouldEqualize");
			getClamps = Harmony3StrictCompatMod.Target("Impact.WeaponEqualizer:GetClamps");
			tierOf = AccessTools.Method(run.DeclaringType, "TierOf");
			if (shouldEqualize == null || getClamps == null || tierOf == null || getClamps.GetParameters().Length != 7)
			{
				Log.Error("[Harmony3StrictCompat] IMPACT's weapon equalizer changed: redo the rate of fire floor");
				return;
			}
			modRoot = root;
			harmony.Patch(run, prefix: new HarmonyMethod(typeof(ImpactFloorByFireRate), nameof(Before)),
				postfix: new HarmonyMethod(typeof(ImpactFloorByFireRate), nameof(After)));
		}

		static IEnumerable<(ThingDef def, VerbProperties verb, ProjectileProperties projectile)> Weapons()
		{
			foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if (!def.IsRangedWeapon || def.Verbs.NullOrEmpty()) continue;
				var verb = def.Verbs.Find(v => v.isPrimary) ?? def.Verbs[0];
				if (verb.defaultProjectile?.projectile is { } projectile) yield return (def, verb, projectile);
			}
		}

		internal static float ShotsPerSecond(int burst, float warmup, float cooldown, int ticksBetweenBurstShots)
		{
			burst = Math.Max(burst, 1);
			return burst / Math.Max(warmup + cooldown + (burst - 1) * ticksBetweenBurstShots / 60f, 0.01f);
		}

		// The floor a weapon firing this fast gets; the result never drops below the weapon's own damage.
		internal static int ScaledFloor(int ownDamage, int floor, float shotsPerSecond) =>
			ownDamage * TokenDamageDivisor <= floor ? ownDamage
			: shotsPerSecond <= ReferenceShotsPerSecond ? Math.Max(ownDamage, floor)
			: Math.Max(ownDamage, Mathf.RoundToInt(floor * ReferenceShotsPerSecond / shotsPerSecond));

		static void Before()
		{
			original.Clear();
			foreach (var (_, _, projectile) in Weapons())
				if (!original.ContainsKey(projectile)) original[projectile] = projectile.GetDamageAmount(1f, null);
		}

		static void After()
		{
			try
			{
				Apply();
			}
			catch (Exception e)
			{
				Log.Error("[Harmony3StrictCompat] IMPACT rate of fire floor failed; weapons keep IMPACT's values: " + e);
			}
			original.Clear();
		}

		static void Apply()
		{
			// Cases as their mods ship them: fail loudly if the rule stops giving these.
			// Bulletstorm, Beam Witherer, dart gun (kept), a slow 9 damage spacer gun (lifted), a 12 damage 4 shots a second one.
			if (ScaledFloor(3, 20, ShotsPerSecond(1, 0.05f, 0.05f, 0)) != 3 || ScaledFloor(1, 20, ShotsPerSecond(15, 2f, 3f, 10)) != 1
				|| ScaledFloor(3, 12, ShotsPerSecond(1, 3f, 4.5f, 0)) != 3 || ScaledFloor(9, 20, ShotsPerSecond(1, 1f, 1.5f, 0)) != 20
				|| ScaledFloor(12, 20, 4f) != 12 || ScaledFloor(8, 20, 2f) != 10)
				throw new InvalidOperationException("rate of fire floor self-check failed");
			// Per projectile: floor of the first weapon using it (the one IMPACT clamped it for), fastest rate of all.
			var floors = new Dictionary<ProjectileProperties, int>();
			var rates = new Dictionary<ProjectileProperties, float>();
			var rows = new List<(ThingDef def, VerbProperties verb, ProjectileProperties projectile, object tier, float rate, int equalized)>();
			foreach (var (def, verb, projectile) in Weapons())
			{
				if (!(bool)shouldEqualize.Invoke(null, [verb]) || !original.ContainsKey(projectile)) continue;
				if (def.weaponTags != null && def.weaponTags.Contains("TurretGun")) continue;
				object tier = tierOf.Invoke(null, [def]);
				float rate = ShotsPerSecond(verb.burstShotCount, verb.warmupTime, def.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown), verb.ticksBetweenBurstShots);
				if (!floors.ContainsKey(projectile))
				{
					object[] clamps = [tier, 0, 0, 0f, 0f, 0f, 0f];
					getClamps.Invoke(null, clamps);
					floors[projectile] = (int)clamps[1];
				}
				rates[projectile] = rates.TryGetValue(projectile, out float fastest) ? Math.Max(fastest, rate) : rate;
				rows.Add((def, verb, projectile, tier, rate, projectile.GetDamageAmount(1f, null)));
			}
			int raised = 0, scaled = 0, token = 0;
			string worst = null;
			float worstFactor = 1f;
			foreach (var pair in floors)
			{
				var projectile = pair.Key;
				int own = original[projectile], now = projectile.GetDamageAmount(1f, null);
				if (now <= own) continue;
				raised++;
				int wanted = ScaledFloor(own, pair.Value, rates[projectile]);
				if (wanted >= now) continue;
				damageBase(projectile) = wanted;
				if (own * TokenDamageDivisor <= pair.Value) token++;
				else scaled++;
				if ((float)now / wanted > worstFactor)
				{
					worstFactor = (float)now / wanted;
					var row = rows.Find(r => r.projectile == projectile);
					worst = $"{row.def.label}: {own} of its own, IMPACT {now}, now {wanted} at {rates[projectile]:0.#} shots a second";
				}
			}
			Log.Message($"[Harmony3StrictCompat] IMPACT weapon floor: {raised} projectiles were lifted to their tier's damage floor, {token} with a third of the floor or less of their own keep their own damage, {scaled} fire faster than {ReferenceShotsPerSecond:0.#} shot a second and got the floor divided by their rate"
				+ (worst == null ? "" : $" (largest change: {worst})"));
			if (File.Exists(Path.Combine(modRoot, "weaponsurvey.on"))) WriteSurvey(rows);
		}

		static void WriteSurvey(List<(ThingDef def, VerbProperties verb, ProjectileProperties projectile, object tier, float rate, int equalized)> rows)
		{
			var text = new StringBuilder("defName\tlabel\tmod\ttier\tburst\twarmup\tcooldown\tticksBetween\tshotsPerSecond\townDamage\timpactDamage\tfinalDamage\townDps\timpactDps\tfinalDps\tpenetration\trange\n");
			var culture = System.Globalization.CultureInfo.InvariantCulture;
			foreach (var r in rows)
			{
				int own = original[r.projectile], final = r.projectile.GetDamageAmount(1f, null);
				text.Append(string.Join("\t", r.def.defName, r.def.label, r.def.modContentPack?.Name, r.tier, r.verb.burstShotCount,
					r.verb.warmupTime.ToString("0.##", culture), r.def.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown).ToString("0.##", culture),
					r.verb.ticksBetweenBurstShots, r.rate.ToString("0.##", culture), own, r.equalized, final,
					(own * r.rate).ToString("0.#", culture), (r.equalized * r.rate).ToString("0.#", culture), (final * r.rate).ToString("0.#", culture),
					r.projectile.GetArmorPenetration().ToString("0.##", culture), r.verb.range.ToString("0.#", culture))).Append('\n');
			}
			string path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Logs", "impact_weapons.tsv");
			File.WriteAllText(path, text.ToString());
			Log.Message($"[Harmony3StrictCompat] IMPACT weapon survey: {rows.Count} weapons written to {path} (remove weaponsurvey.on from the mod folder to turn off)");
		}
	}
}
