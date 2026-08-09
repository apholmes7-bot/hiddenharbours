#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The small plan a water region hands the template.</b> One of these per open-water scene — the
    /// west water, then the mid-bay, then the east water (world-map-plan §2.2) — carrying everything
    /// <see cref="WaterSceneTemplate"/> needs and nothing it can work out for itself.
    ///
    /// <para><b>Authored vs derived is the whole discipline here.</b> The fields are AUTHORED: they are
    /// the region's facts, and a region's plan class is the one place they are written down. The
    /// properties are DERIVED, so nothing downstream can carry a second copy of a number that has to
    /// agree — the shader's bake grid, the elevation range it maps across and the passage bands are all
    /// computed from the extent and the bed rather than typed beside them. That is the lesson St Peters
    /// learned the expensive way, when <c>new Vector2(160f, 120f)</c> lived in six places and resizing
    /// the region meant finding all six (scene-sizing §6.2).</para>
    ///
    /// <para><b>A plan does not invent a seam elevation.</b> Where a water region touches a neighbour,
    /// the bed it exposes there has to be the neighbour's own authored number — reached for, not
    /// re-typed. That happens in the region's plan class, which is why the neighbours' constants are
    /// public and why this type takes a finished <see cref="Zones"/> array rather than describing shoals
    /// itself.</para>
    /// </summary>
    public sealed class WaterScenePlan
    {
        // ---- identity ----------------------------------------------------------------------------

        /// <summary>Stable region id, append-only (ADR 0009) — e.g. <c>region.west_water</c>. Save and
        /// unlock state key off it, so it outlives whatever the place ends up being CALLED.</summary>
        public string RegionId;

        /// <summary>File name (no extension) of the committed <see cref="RegionDef"/> under
        /// <c>Data/Regions</c>.</summary>
        public string RegionAssetName;

        /// <summary>Player-facing name. A display seam, not an id (ADR 0009) — free to change the day the
        /// owner names the place.</summary>
        public string DisplayName;

        /// <summary>Scene asset name (no path/extension), as registered in Build Settings.</summary>
        public string SceneName;

        public string Description;

        /// <summary>Optional unlock flag id; empty = always reachable.</summary>
        public string UnlockFlag = "";

        // ---- extent ------------------------------------------------------------------------------

        public Vector2 WorldCenter = Vector2.zero;

        /// <summary>The region's world rectangle in metres — sized by TIME TO CROSS in the region's
        /// gating boat (scene-sizing §1.3), never by feel.</summary>
        public Vector2 WorldSize;

        /// <summary>
        /// Height-data resolution in texels per metre — the ruled 1 px/m offshore, 2 px/m inshore
        /// (scene-sizing §6.1).
        ///
        /// <para>An open-water scene paints NO seabed (world-map-plan §2.2: deep enough that the bottom
        /// is never visible), so this is not a paint budget here. It is still authored, because it is the
        /// figure <see cref="HeightBakeResolution"/> derives the shader's bake grid from and the one the
        /// paint tool would honour if a later scene ever did want paint over part of itself.</para>
        /// </summary>
        public float SeabedPixelsPerMetre = 1f;

        // ---- tide --------------------------------------------------------------------------------
        // ⚠ Adjacent regions that share terrain MUST share a tide profile (world-map-plan §4.3 — the bar
        // taught this the hard way: two tides either side of a seam means the crossing is dry on one
        // side and flooded on the other at the same instant). A water region touches its neighbours, so
        // its plan takes their profile rather than choosing one.

        public float TideMean;
        public float TideAmplitude;
        public float TidePhaseHours;

        /// <summary>Nominal depth (m, positive) for the region's flavour fields. Open water is never a
        /// harbour, so this is atmospheric rather than a gate.</summary>
        public float NominalDepthMetres = 6f;

        // ---- the bed -----------------------------------------------------------------------------

        /// <summary>The floor everywhere no shoal reaches, in metres above chart datum.</summary>
        public float DeepElevation;

        /// <summary>The shoals: flat inside their rectangle, smoothstepping to <see cref="DeepElevation"/>
        /// across their falloff, max-composing. Empty is legal — a featureless bed is a bed.</summary>
        public RectTidalTerrain.LandZone[] Zones = System.Array.Empty<RectTidalTerrain.LandZone>();

        // ---- presentation ------------------------------------------------------------------------

        /// <summary>What the standalone-review camera frames, in metres of visible world height. A water
        /// scene is reviewed from the region's gating boat, never on foot — the Dory's
        /// <see cref="CameraFollow.DefaultWorldHeightMeters"/> is the sheltered-water default.</summary>
        public float CameraWorldHeightMetres = CameraFollow.DefaultWorldHeightMeters;

        /// <summary>Camera clear colour, and the tint of the fallback sea plane on a checkout that has
        /// not imported the water art yet.</summary>
        public Color BackdropColor = new Color(0.12f, 0.22f, 0.30f);

        /// <summary>Spawn-table fish ids (by id — never direct refs). The catch itself is gated by each
        /// species' own <c>RegionIds</c>; this is region metadata.</summary>
        public string[] SpawnFishIds = new string[0];

        /// <summary>How deep (m) a passage band is ACROSS the way you travel through it — enough that a
        /// hull cannot pass through between two physics steps. The land builders use the same 6 m.</summary>
        public float EdgeBandDepthMetres = 6f;

        // ---- derived -----------------------------------------------------------------------------

        /// <summary>
        /// A band spanning the FULL height of the region — because in open water the region's edge IS the
        /// door. Nothing steers a boat out here the way a bar or a channel steers one inshore, so a
        /// narrow band would be a door you can miss by sailing past it, and a crossing that silently does
        /// not fire is the worst thing a seam can do. Generous is safe: <c>RegionPassage</c>'s
        /// leave-then-enter latch + cooldown are what make a wide band cost nothing.
        /// </summary>
        public Vector2 EdgeBandSize => new Vector2(EdgeBandDepthMetres, WorldSize.y);

        /// <summary>The lowest elevation the bed reaches — the bake's R channel must bracket the whole
        /// field or it clips, and the component's −4 default would flatten an offshore floor entirely.</summary>
        public float HeightMin
        {
            get
            {
                float m = DeepElevation;
                if (Zones != null)
                    for (int i = 0; i < Zones.Length; i++)
                        if (Zones[i].Elevation < m) m = Zones[i].Elevation;
                return m;
            }
        }

        /// <summary>The highest elevation the bed reaches. Derived rather than given headroom: an R8 bake
        /// over the range the field actually occupies is the finest the field can be read at.</summary>
        public float HeightMax
        {
            get
            {
                float m = DeepElevation;
                if (Zones != null)
                    for (int i = 0; i < Zones.Length; i++)
                        if (Zones[i].Elevation > m) m = Zones[i].Elevation;
                return m;
            }
        }

        /// <summary>The height-bake grid the water shader uses — derived from the extent and the authored
        /// texels-per-metre, never a literal. Square, because <c>WaterSurface</c> bakes a square texture.
        /// (The surface clamps again to its own [16, 256] range; this is the region's ASK.)</summary>
        public int HeightBakeResolution =>
            Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Max(WorldSize.x, WorldSize.y) * Mathf.Max(SeabedPixelsPerMetre, 0f)),
                64, RegionDef.MaxSeabedTexels);

        /// <summary>The region's world rectangle, for "is this inside the scene?" checks.</summary>
        public Rect WorldBounds => new Rect(WorldCenter - WorldSize * 0.5f, WorldSize);

        // ---- the bed, as a function --------------------------------------------------------------

        /// <summary>
        /// The seabed elevation this plan authors at a world position — the SAME pure composition
        /// <see cref="RectTidalTerrain"/> evaluates at run time, so a test can assert the bed the scene
        /// actually has without a scene (the <c>ConfigureTidalTerrain</c> convention: one source of truth,
        /// not a builder with a comment and a test with a literal).
        /// </summary>
        public float ElevationAt(Vector2 worldPos) =>
            RectTidalTerrain.ElevationAtZones(worldPos, DeepElevation, Zones);

        /// <summary>Push the authored bed onto a terrain component. Every path that stands this region's
        /// height field up goes through here.</summary>
        public void ConfigureTerrain(RectTidalTerrain terrain)
        {
            if (terrain == null) return;
            terrain.Configure(DeepElevation, Zones);
        }
    }
}
#endif
