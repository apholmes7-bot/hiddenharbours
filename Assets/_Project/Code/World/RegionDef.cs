using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A region of the coast, as data (ADR 0003): one asset per region under <c>Data/Regions</c>,
    /// keyed by a stable <see cref="Id"/> (e.g. <c>region.port_greywick</c>). The world is "scene per
    /// region, loaded additively" (CLAUDE.md §3), so a region's content lives in its own scene and
    /// this def is the lightweight, code-readable handle to it: which scene to load, how to gate it,
    /// and the region's flavour/tunables. (Schema: docs/architecture/data-model.md — id, display name,
    /// unlock gate, scene ref, tide profile, depth, spawn tables, …)
    ///
    /// Minimal for VS-22 (Port Greywick is the first authored region besides the greybox cove). Spawn
    /// tables / hazards / NPC lists / palette grade are listed by id or left empty here and filled as
    /// those systems land — never hard-coded against a region in C#.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Region", fileName = "Region")]
    public class RegionDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (e.g. region.port_greywick). Save/unlock state keys off it.")]
        public string Id = "region.port_greywick";
        public string DisplayName = "Port Greywick";
        [TextArea] public string Description;

        [Header("Scene (loaded additively)")]
        [Tooltip("Name of the scene asset for this region (no path/extension), as registered in Build " +
                 "Settings — e.g. \"Greywick\". The scene-load path loads it additively.")]
        public string SceneName = "Greywick";

        [Header("Gating")]
        [Tooltip("Optional unlock flag id; empty = always reachable. Greywick unlocks early via story.")]
        public string UnlockFlag = "";

        [Header("Harbour & tide (flavour / tunables)")]
        [Tooltip("A deep, dredged harbour stays workable across the tide — it never strands you (P5).")]
        public bool IsDeepHarbour = false;
        [Tooltip("Nominal harbour depth in metres (atmospheric/data; deep harbour ≈ 6 m).")]
        public float HarbourDepthMeters = 2f;
        [Tooltip("Tide profile for this region: mean level (m rel. datum).")]
        public float TideMeanLevel = 0f;
        [Tooltip("Tide amplitude (m). Sheltered/deep harbours run small + atmospheric.")]
        public float TideAmplitude = 1.6f;
        [Tooltip("Tide phase offset (h) so regions don't all peak together.")]
        public float TidePhaseHours = 0f;

        [Header("Content (by id)")]
        [Tooltip("Spawn-table fish ids (data-model: by id, never direct refs). Greywick is a services " +
                 "region, not a fishing ground, so this is usually empty/marginal.")]
        public string[] SpawnFishIds = new string[0];

        /// <summary>True if this region names a scene to load (a real, reachable region).</summary>
        public bool HasScene => !string.IsNullOrWhiteSpace(SceneName);

        /// <summary>True if entering requires an unlock flag to be set.</summary>
        public bool RequiresUnlock => !string.IsNullOrEmpty(UnlockFlag);
    }
}
