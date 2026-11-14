// Built into Compat/Harmony3StrictCompat.UsefulMarks.dll, outside any Assemblies folder: the class derives from a
// Useful Marks type, and RimWorld checks every type of a mod's assemblies while that mod loads, which is before
// Useful Marks is loaded (this mod loads early). The main DLL loads this one at startup (LoadUsefulMarksAddon).
using System;
using System.Collections.Generic;
using HarmonyLib;
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

	public static class RegisterGuilty
	{
		static int index = -1;

		// Useful Marks stores a condition by its index in Assets.markerConditionNodeTypes and reads every index past the
		// first skill entry as a skill. So the new type goes at the very end, where no existing index moves, and a prefix
		// on CreateInstanceCondition builds it for that one index. Only the menu entry sits next to its relatives.
		public static void Run()
		{
			try
			{
				var create = AccessTools.Method(typeof(Assets), "CreateInstanceCondition");
				var types = Assets.markerConditionNodeTypes;
				var labels = Assets.markerConditionNodeLabels;
				if (create == null || types == null || labels == null)
				{
					Log.Warning("[Harmony3StrictCompat] Useful Marks changed: 'Considered guilty' mark condition not added");
					return;
				}
				new Harmony("local.harmony3strictcompat.usefulmarks").Patch(create,
					prefix: new HarmonyMethod(typeof(RegisterGuilty), nameof(CreateGuilty)));
				types.Add((typeof(MarkerConditionGuilty), null));
				index = types.Count - 1;
				int after = labels.FindIndex(l => types[l.Item2].Item1 ==typeof(MarkerConditionUnrecruitable));
				labels.Insert(after < 0 ? labels.Count : after + 1, (new MarkerConditionGuilty().Label, index));
			}
			catch (Exception e)
			{
				Log.Warning("[Harmony3StrictCompat] 'Considered guilty' mark condition not added: " + e);
			}
		}

		static bool CreateGuilty(int index, ref MarkerConditionNode __result)
		{
			if (index != RegisterGuilty.index) return true;
			__result = new MarkerConditionGuilty();
			return false;
		}
	}
}
