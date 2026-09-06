#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;    // NavLightCharacter / NavLightPhasePlan
using HiddenHarbours.Art;     // NavLight
using HiddenHarbours.Boats;   // NavBuoyDef / NavBuoyVisual

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Give the marks already standing in a scene their light phases — without rebuilding the
    /// region.</b>
    ///
    /// <para><b>Why this is a command and not part of the region build.</b> A full Build re-places
    /// every mark and hands out the phases on the way (<see cref="NavMarkPlacer"/>), which is the
    /// right thing for a region being built. But twenty-five marks are already standing in Nine Mile
    /// Creek and St Peters from an earlier build: they are on the right spots wearing the right
    /// buoys, and all they want is the one number that says where in her period each of them sits.
    /// Rebuilding two whole regions to deliver it would rewrite both scenes wholesale, and those
    /// scenes are worked on by other lanes.</para>
    ///
    /// <para><b>What it does NOT touch:</b> position, size rung, facing, art, mooring — nothing but
    /// the chart id and the phase fraction on each <see cref="NavBuoyVisual"/>, and the presence of a
    /// <see cref="NavLight"/>. Idempotent: run it twice and the second run reports no change.</para>
    ///
    /// <para><b>A mark that has never been phased still lights.</b> She falls back to a hash of her
    /// own id, which is right on its own and merely carries no guarantee about her neighbours — so
    /// this command improves a picture rather than repairing a broken one.</para>
    /// </summary>
    public static class NavLightPhaser
    {
        private const string MenuPath = "Hidden Harbours/Art/Phase Nav Lights in Open Scene";
        private const string NamePrefix = "NavMark_";

        [MenuItem(MenuPath)]
        public static void PhaseOpenScene()
        {
            int changed = PhaseLoadedMarks(NineMileCreekNavMarks.DefFolder, out string report);
            Debug.Log(report);

            if (changed > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Phase every <see cref="NavBuoyVisual"/> currently loaded. Returns how many marks changed
        /// and a report fit for the console.
        /// </summary>
        public static int PhaseLoadedMarks(string defFolder, out string report)
        {
            Dictionary<string, NavBuoyDef> defs = NavMarkPlacer.LoadDefsByMarkType(defFolder);
            NavBuoyVisual[] marks = Object.FindObjectsByType<NavBuoyVisual>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (marks.Length == 0)
            {
                report = "[NavLight] no nav marks in the open scene — nothing to phase.";
                return 0;
            }

            // The id a mark answers to. It must match the one the PLANNER would use, or a later Build
            // would re-phase everything: the placer names each object "NavMark_<chart id>", so
            // stripping that prefix recovers exactly the id the plan knows her by.
            var ids = new Dictionary<NavBuoyVisual, string>();
            var forSpread = new List<(string Id, string Character)>();

            foreach (NavBuoyVisual mark in marks)
            {
                string id = !string.IsNullOrEmpty(mark.PhaseId) && mark.PhaseId != mark.name
                    ? mark.PhaseId
                    : StripPrefix(mark.name);
                ids[mark] = id;
                forSpread.Add((id, mark.Def != null ? mark.Def.LightText : null));
            }

            Dictionary<string, float> phases = NavLightPhasePlan.Spread(forSpread);

            int changed = 0, lit = 0, lamps = 0;
            foreach (NavBuoyVisual mark in marks)
            {
                string id = ids[mark];
                bool isLit = phases.TryGetValue(id, out float fraction);
                if (isLit) lit++;

                float want = isLit ? fraction : -1f;
                if (!Mathf.Approximately(mark.PhaseFraction, want) || mark.PhaseId != id)
                {
                    Undo.RecordObject(mark, "Phase nav light");
                    mark.AssignPhase(id, want);
                    EditorUtility.SetDirty(mark);
                    changed++;
                }

                // A mark placed before the lamp existed carries no NavLight of her own; the prefab
                // gives her one, but an instance whose prefab link was broken would not get it.
                if (mark.GetComponent<NavLight>() == null)
                {
                    Undo.AddComponent<NavLight>(mark.gameObject);
                    lamps++;
                }
            }

            report = $"[NavLight] phased {changed} of {marks.Length} marks ({lit} lit); " +
                     $"added {lamps} missing lamp component(s). Re-run is a no-op.";
            return changed + lamps;
        }

        private static string StripPrefix(string name) =>
            name != null && name.StartsWith(NamePrefix, System.StringComparison.Ordinal)
                ? name.Substring(NamePrefix.Length)
                : name;
    }
}
#endif
