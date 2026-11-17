// Built into Compat/Harmony3StrictCompat.UsefulMarks.dll, outside any Assemblies folder: the classes derive from a
// Useful Marks type, and RimWorld checks every type of a mod's assemblies while that mod loads, which is before
// Useful Marks is loaded (this mod loads early). The main DLL loads this one at startup (LoadUsefulMarksAddon).
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UsefulMarks;
using Verse;

namespace Harmony3StrictCompat.UsefulMarksCompat
{
	// ---- Useful Marks: "Considered guilty" mark condition ----
	// Useful Marks has no condition that reads Pawn_GuiltTracker, so a guilty prisoner or colonist could only be
	// marked by hand, and such a mark outlives the guilt.
	public class MarkerConditionGuilty : MarkerConditionNode
	{
		public override string Label => "ConsideredGuiltyNoTimer".Translate();
		public override bool HasParams => false;
		public override bool InternalSolveFor(Pawn pawn) => pawn.guilt != null && pawn.guilt.IsGuilty;
	}

	// ---- Useful Marks: "Missing body part" mark condition ----
	// "Specific hediffs" with Missing body part also matches every pawn with a bionic arm, leg, etc.: vanilla
	// Hediff_AddedPart.PostAdd gives each child part of the replaced part (hand, fingers, foot, toes) a
	// MissingBodyPart hediff, which the health tab hides. This one only counts parts with no added part on them or
	// on any part above them: a lost eye, or an arm with no prosthetic, the losses the health tab shows.
	public class MarkerConditionMissingPart : MarkerConditionNode
	{
		public override string Label => HediffDefOf.MissingBodyPart.LabelCap;
		public override bool HasParams => false;

		public override bool InternalSolveFor(Pawn pawn)
		{
			var set = pawn.health?.hediffSet;
			if (set == null) return false;
			foreach (var hediff in set.hediffs)
				if (hediff is Hediff_MissingPart && hediff.Part != null && hediff.Visible
					&& !set.PartOrAnyAncestorHasDirectlyAddedParts(hediff.Part))
					return true;
			return false;
		}
	}

	public static class RegisterConditions
	{
		// Useful Marks stores a condition by its index in Assets.markerConditionNodeTypes and reads every index past the
		// first skill entry as a skill. So new types go at the very end, where no existing index moves, and a prefix on
		// CreateInstanceCondition builds them for those indexes. Only the menu entries sit next to their relatives.
		// List order is the saved index: Guilty stays first, new conditions go after it.
		static readonly List<(Type type, Func<MarkerConditionNode> make, string tree, Type after)> added = new()
		{
			(typeof(MarkerConditionGuilty), () => new MarkerConditionGuilty(), null, typeof(MarkerConditionUnrecruitable)),
			(typeof(MarkerConditionMissingPart), () => new MarkerConditionMissingPart(), "HediffSubMenuConditions", typeof(MarkerConditionExactHediff)),
		};
		static int first = -1;

		public static void Run()
		{
			try
			{
				var create = AccessTools.Method(typeof(Assets), "CreateInstanceCondition");
				var types = Assets.markerConditionNodeTypes;
				var labels = Assets.markerConditionNodeLabels;
				if (create == null || types == null || labels == null)
				{
					Log.Warning("[Harmony3StrictCompat] Useful Marks changed: extra mark conditions not added");
					return;
				}
				new Harmony("local.harmony3strictcompat.usefulmarks").Patch(create,
					prefix: new HarmonyMethod(typeof(RegisterConditions), nameof(Create)));
				first = types.Count;
				foreach (var (type, make, tree, after) in added)
				{
					types.Add((type, tree));
					int at = labels.FindIndex(l => types[l.Item2].Item1 == after);
					labels.Insert(at < 0 ? labels.Count : at + 1, (make().Label, types.Count - 1));
				}
			}
			catch (Exception e)
			{
				Log.Warning("[Harmony3StrictCompat] extra Useful Marks conditions not added: " + e);
			}
		}

		static bool Create(int index, ref MarkerConditionNode __result)
		{
			int i = index - first;
			if (first < 0 || i < 0 || i >= added.Count) return true;
			__result = added[i].make();
			return false;
		}
	}
}
