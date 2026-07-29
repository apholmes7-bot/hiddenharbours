using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A region of the coast, as data (ADR 0003): one asset per region under <c>Data/Regions</c>,
    /// keyed by a stable <see cref="Id"/> (e.g. <c>region.nine_mile_creek</c>). The world is "scene per
    /// region, loaded additively" (CLAUDE.md §3), so a region's content lives in its own scene and
    /// this def is the lightweight, code-readable handle to it: which scene to load, how to gate it,
    /// and the region's flavour/tunables. (Schema: docs/architecture/data-model.md — id, display name,
    /// unlock gate, scene ref, tide profile, depth, spawn tables, …)
    ///
    /// Minimal for VS-22 (Nine Mile Creek is the first authored region besides the greybox cove). Spawn
    /// tables / hazards / NPC lists / palette grade are listed by id or left empty here and filled as
    /// those systems land — never hard-coded against a region in C#.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Region", fileName = "Region")]
    public class RegionDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (e.g. region.nine_mile_creek). Save/unlock state keys off it.")]
        public string Id = "region.nine_mile_creek";
        public string DisplayName = "Nine Mile Creek";
        [TextArea] public string Description;

        [Header("Scene (loaded additively)")]
        [Tooltip("Name of the scene asset for this region (no path/extension), as registered in Build " +
                 "Settings — e.g. \"Nine Mile Creek\". The scene-load path loads it additively.")]
        public string SceneName = "NineMileCreek";

        [Header("Gating")]
        [Tooltip("Optional unlock flag id; empty = always reachable. Nine Mile Creek unlocks early via story.")]
        public string UnlockFlag = "";

        [Header("Extent — the region's world rectangle (authored ONCE, read by everything)")]
        [Tooltip("World-space CENTRE of the region's rectangle, in metres. Same frame as the water " +
                 "shader and the painted height map.")]
        public Vector2 WorldCenter = Vector2.zero;
        [Tooltip("World-space SIZE (width × height) in metres. This is the number the sea plane, the " +
                 "painted seabed and the camera bounds must all agree on — size a region by " +
                 "TIME-TO-CROSS, not by feel (docs/design/scene-sizing-and-world-scale.md §3).")]
        public Vector2 WorldSizeMeters = new Vector2(160f, 120f);
        [Tooltip("Painted-seabed resolution in TEXELS PER METRE. 2 px/m inshore (crisp enough for " +
                 "coves and cave mouths), 1 px/m offshore. The texel grid is derived from this and " +
                 "WorldSizeMeters, so a non-square region gets a non-square map — never a square one " +
                 "stretched over it (scene-sizing §6.1).")]
        public float SeabedPixelsPerMetre = 2f;

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
        [Tooltip("Spawn-table fish ids (data-model: by id, never direct refs). Nine Mile Creek is a services " +
                 "region, not a fishing ground, so this is usually empty/marginal.")]
        public string[] SpawnFishIds = new string[0];

        /// <summary>True if this region names a scene to load (a real, reachable region).</summary>
        public bool HasScene => !string.IsNullOrWhiteSpace(SceneName);

        /// <summary>True if entering requires an unlock flag to be set.</summary>
        public bool RequiresUnlock => !string.IsNullOrEmpty(UnlockFlag);

        // ---- extent, derived ---------------------------------------------------------------------

        /// <summary>
        /// The biggest painted-seabed texture any region may ask for, on either axis. Not a sprite —
        /// this is a data texture, so the 2048 sprite cap does not apply — but a region that wants
        /// more than this has almost certainly mistyped its pixels-per-metre rather than genuinely
        /// needing 16 MB of R8. St Peters at 760 × 520 m and 2 px/m is 1520 × 1040, well inside it.
        /// </summary>
        public const int MaxSeabedTexels = 4096;

        /// <summary>
        /// The painted-seabed texel grid this region's extent implies: size × pixels-per-metre,
        /// rounded up, at least 1 on each axis. <b>Derived, never authored</b> — the old paint tool
        /// hard-coded a SQUARE 192² over a 160 × 120 m rect, which sampled x and y at different
        /// densities (1.2 vs 1.6 px/m) for no reason anybody chose.
        /// </summary>
        public Vector2Int SeabedTexels
        {
            get
            {
                float ppm = Mathf.Max(0f, SeabedPixelsPerMetre);
                return new Vector2Int(
                    Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(WorldSizeMeters.x) * ppm)),
                    Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(WorldSizeMeters.y) * ppm)));
            }
        }

        /// <summary>True if the region's extent describes a real rectangle at a usable resolution.
        /// A tool should refuse rather than mint a 1 × 1 map or a 16 MB one.</summary>
        public bool HasUsableExtent =>
            WorldSizeMeters.x > 0f && WorldSizeMeters.y > 0f && SeabedPixelsPerMetre > 0f &&
            SeabedTexels.x <= MaxSeabedTexels && SeabedTexels.y <= MaxSeabedTexels;

        /// <summary>Uncompressed R8 bytes the painted seabed costs at this extent — the number the
        /// resolution decision is actually made on.</summary>
        public int SeabedBytes => SeabedTexels.x * SeabedTexels.y;
    }
}
