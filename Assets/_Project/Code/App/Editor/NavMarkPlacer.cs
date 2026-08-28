#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;   // NavBuoyDef / NavBuoyVisual
using HiddenHarbours.World;   // NavMarkPlanResult

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The one thing that actually stands a nav mark up in a scene</b> — shared by every region, so
    /// the arithmetic lives in <see cref="NavMarkPlan"/>, the geography lives in the region's own
    /// table, and the Unity work lives here exactly once.
    ///
    /// <para><b>⭐ IT INSTANTIATES THE KIT'S PREFAB, and that is not tidiness.</b>
    /// <c>NavBuoyDefBuilder.BuildPrefab</c> exists specifically so that "the root the wave field
    /// samples at" and "the child the bob moves" cannot be quietly collapsed into one object — a mark
    /// whose visual IS its root samples the field at a point that moves with the bob, which reads as
    /// jitter rather than as a bug. Building the hierarchy here by hand would fork that guarantee.</para>
    ///
    /// <para><b>⚠ EVERY REFUSAL IS LOGGED, EVERY BUILD.</b> The plan drops marks that would stand on
    /// ground that bares, and a harbour that dries produces a lot of them. That is the finding, not
    /// noise: it is how the owner learns that his basin cannot carry a floating mark, and it is the
    /// evidence behind the art ask for a perch. A silent drop would read as "the scheme is thin".</para>
    /// </summary>
    public static class NavMarkPlacer
    {
        /// <summary>
        /// Place a whole region's plan. Returns how many marks were stood up.
        /// </summary>
        /// <param name="rootName">Name of the GameObject the marks hang under.</param>
        /// <param name="defFolder">Where the <see cref="NavBuoyDef"/> assets live.</param>
        /// <param name="prefabPath">The kit's placement prefab.</param>
        /// <param name="plan">What <see cref="NavMarkPlan"/> derived from the seabed.</param>
        /// <param name="tag">Log prefix — the calling region's class name.</param>
        /// <param name="springLowWater">The region's lowest spring water, for the log line.</param>
        public static int Place(string rootName, string defFolder, string prefabPath,
                                NavMarkPlanResult plan, string tag, float springLowWater)
        {
            if (plan == null) return 0;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[{tag}] no nav-buoy prefab at {prefabPath} — the marks are not placed. Run " +
                    "'Hidden Harbours ▸ Art ▸ Build Nav Buoy Prefab' and rebuild the region. Nothing " +
                    "else in the scene is affected.");
                return 0;
            }

            Dictionary<string, NavBuoyDef> defs = LoadDefsByMarkType(defFolder);
            if (defs.Count == 0)
            {
                Debug.LogWarning(
                    $"[{tag}] no NavBuoyDef assets under {defFolder} — the marks are not placed. Run " +
                    "'Hidden Harbours ▸ Art ▸ Build Nav Buoy Defs' and rebuild the region.");
                return 0;
            }

            var root = new GameObject(rootName);
            int placed = 0;
            var missingTypes = new HashSet<string>();

            foreach (PlannedNavMark mark in plan.Marks)
            {
                if (!defs.TryGetValue(mark.MarkType, out NavBuoyDef def) || def == null)
                {
                    missingTypes.Add(mark.MarkType);
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                if (go == null) continue;

                go.name = $"NavMark_{mark.Id}";
                go.transform.position = new Vector3(mark.At.x, mark.At.y, 0f);

                var visual = go.GetComponent<NavBuoyVisual>();
                if (visual == null)
                {
                    Debug.LogWarning(
                        $"[{tag}] the prefab at {prefabPath} carries no NavBuoyVisual — '{mark.Id}' is " +
                        "an undressed instance. Re-run the prefab build.");
                    continue;
                }

                // ⭐ SHE IS MOORED BEFORE SHE IS DRESSED, and the order is load-bearing:
                // NavBuoyVisual.Configure reads the size rung and hands the mooring its
                // displacement, girth and scope, so the mooring has to EXIST by then or a mark
                // ends up wearing the component's defaults instead of her own rung's numbers.
                // (An older prefab carries no mooring at all; this is what promotes it.)
                var mooring = go.GetComponent<NavBuoyMooring>();
                if (mooring == null) mooring = go.AddComponent<NavBuoyMooring>();

                // Her anchor is the PLANNED position, stated rather than inferred. Left to Awake
                // she would moor at whatever her transform read then — the same number today, and
                // silently "wherever the last knock left her" the first time anything moves a
                // mark before it wakes.
                mooring.MoorAt(mark.At);

                // ⚠️ The facing is a PLACEMENT choice the plan derived, and the kit is CLOCKWISE
                // (cell i = heading +45°·i). Do not "correct" it against the fleet's sheets.
                visual.Configure(def, mark.SizeId, mark.Facing);
                placed++;
            }

            if (missingTypes.Count > 0)
            {
                Debug.LogWarning(
                    $"[{tag}] no def for mark type(s) {string.Join(", ", missingTypes)} under " +
                    $"{defFolder} — those marks are missing and the rest are placed. The kit bakes ten " +
                    "types; the four parked ones (SafeWater, Special, Regulatory, Spar) are one Build away.");
            }

            Debug.Log(Report(tag, plan, placed, springLowWater));
            return placed;
        }

        /// <summary>Every <see cref="NavBuoyDef"/> in the folder, keyed by its kit type. Ordinal — these
        /// are asset keys, not prose.</summary>
        public static Dictionary<string, NavBuoyDef> LoadDefsByMarkType(string defFolder)
        {
            var byType = new Dictionary<string, NavBuoyDef>(System.StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(defFolder)) return byType;

            foreach (string guid in AssetDatabase.FindAssets("t:NavBuoyDef", new[] { defFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<NavBuoyDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || string.IsNullOrEmpty(def.MarkType)) continue;
                byType[def.MarkType] = def;
            }
            return byType;
        }

        /// <summary>
        /// The build-log line. It names the shallowest mark in the region, because that is the one a
        /// tide change will drown first, and it enumerates every refusal so a dried-out harbour reports
        /// itself rather than looking under-buoyed.
        ///
        /// <para><b>Public because it is the deliverable.</b> This text is how the owner learns that
        /// his basin cannot carry a floating mark — the finding is IN the log, not in a comment — so a
        /// test asserts it says so rather than trusting that it does.</para>
        /// </summary>
        public static string Report(string tag, NavMarkPlanResult plan, int placed, float springLowWater)
        {
            var sb = new StringBuilder();
            sb.Append($"[{tag}] Placed {placed} nav mark(s) of {plan.Marks.Count} planned, IALA Region B " +
                      $"(red to starboard returning from seaward). Spring low water {springLowWater:0.##} m.");

            float shallowest = float.MaxValue;
            string shallowestId = null;
            foreach (PlannedNavMark m in plan.Marks)
                if (m.SpringLowDepthMetres < shallowest)
                {
                    shallowest = m.SpringLowDepthMetres;
                    shallowestId = m.Id;
                }
            if (shallowestId != null)
                sb.Append($" Shallowest: '{shallowestId}' floats in {shallowest:0.00} m at spring low.");

            foreach (NavChannelFairway f in plan.Fairways)
                sb.Append($"\n  · {f.ChannelId}: {f.Stations.Count} station(s) derived off the seabed.");

            if (plan.Refusals.Count == 0)
            {
                sb.Append("\n  · No refusals — every planned mark floats at every tide.");
                return sb.ToString();
            }

            sb.Append($"\n  ⚠ {plan.Refusals.Count} mark(s) REFUSED (not nudged — a mark in the wrong " +
                      "place is worse than a missing one):");
            foreach (NavMarkRefusal r in plan.Refusals)
                sb.Append($"\n      – {r.OwnerId} at ({r.At.x:0.#}, {r.At.y:0.#}): {r.Reason}");
            sb.Append("\n  ⭐ A refusal inside a harbour is a FINDING, not a gap: this coast's basins bare " +
                      "at spring low, so the inner channel wants a PERCH or a WITHY — a mark on a pole — " +
                      "which the kit does not bake. Logged as an art ask rather than improvised.");

            return sb.ToString();
        }
    }
}
#endif
