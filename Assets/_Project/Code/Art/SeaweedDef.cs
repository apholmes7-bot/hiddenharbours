using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Data definition for one region's <b>drifting seaweed bed</b> (owner ask 2026-07-08: "seaweed
    /// clumps that can get stuck on things and group together from the waves" — P1 the sea moves
    /// things, P3 the working coast wears its wrack). Content is data, not code (ADR 0003): a region
    /// gets weed by authoring one of these assets (Data/Decor) and listing it in the Resources
    /// <see cref="SeaweedLibrary"/> — no code change, no scene/builder wiring.
    ///
    /// <para><b>Decor tier, never saved (rule 5).</b> The weed drives NO gameplay: it is recreated
    /// per session from seeded placement, drifts on the deterministic shared signals (sim current,
    /// shared wind, the ONE shared wave field), and nothing about it enters the save or the sim. The
    /// behaviour maths is the pure <see cref="SeaweedMath"/>; the runtime shell is the self-installing
    /// <see cref="SeaweedPresenter"/>.</para>
    ///
    /// <para><b>Owner tunables (rule 6).</b> Every number an owner might want to feel-tune lives
    /// here: how much weed, where, how it rides the sea, when clumps merge, what they snag on, when
    /// the tide refloats a beached clump, and the look. Greybox blobs are generated in code until the
    /// painted weed lands — <see cref="TierSprites"/> is the slot it drops into.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Seaweed Bed", fileName = "SeaweedBed")]
    public class SeaweedDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case.")]
        public string Id = "decor.seaweed_st_peters";
        [Tooltip("Scene name of the region this bed lives in (e.g. StPeters). The presenter activates " +
                 "the bed only while this scene is active. Blank = any region that registers a tidal terrain.")]
        public string RegionSceneName = "StPeters";

        [Header("The bed (where the weed lives and how much of it)")]
        [Tooltip("Centre of the bed rectangle (world units) pieces are seeded across.")]
        public Vector2 BedCenter = new Vector2(5f, -30f);
        [Tooltip("Size of the bed rectangle (world units).")]
        public Vector2 BedSize = new Vector2(90f, 26f);
        [Tooltip("How many weed pieces the bed pools (rule 7: this bounds the sprite count — merged " +
                 "pieces go dormant and respawn, the pool never grows).")]
        [Range(1, 64)] public int PieceCount = 18;
        [Tooltip("Minimum water depth (m) at the CURRENT tide for a piece to seed/respawn there — keeps " +
                 "fresh weed off the drying flats (it can still DRIFT ashore and strand; that's the point).")]
        [Min(0f)] public float MinSpawnDepthMeters = 0.5f;
        [Tooltip("Seeded candidate spots tried per respawn before giving up until the next slow tick.")]
        [Range(1, 32)] public int MaxSpawnTries = 12;
        [Tooltip("Clock seconds an absorbed/recycled piece stays dormant before respawning as a fresh " +
                 "small clump somewhere in the bed.")]
        [Min(1f)] public float RespawnSeconds = 120f;
        [Tooltip("How far (m) outside the bed rect a piece may drift before it is quietly recycled — " +
                 "the bed never bleeds its whole stock out of the region.")]
        [Min(0f)] public float DriftBoundsPaddingMeters = 12f;

        [Header("Drift (the sea moves it — current + shared wind + wave convergence, never a random walk)")]
        [Tooltip("How much of the sim's tidal current (CurrentVector, m/s) the weed rides. 1 = carried " +
                 "at the full set, like a proper drifter.")]
        [Min(0f)] public float FlowResponse = 1f;
        [Tooltip("Drift (m/s per unit of the shared 0..1 _WindWorld) from windage — weed rides LOW, so " +
                 "keep this well under the mist's response.")]
        [Min(0f)] public float WindResponse = 0.12f;
        [Tooltip("How hard the weed slides DOWN the local wave slope toward the troughs (m/s per unit " +
                 "slope) — the term that visibly GROUPS the clumps where the water converges.")]
        [Min(0f)] public float TroughSeek = 0.6f;
        [Tooltip("Hard cap (m/s) on the summed drift so a gale can't fling the wrack across the harbour.")]
        [Min(0.01f)] public float MaxDriftSpeedMetersPerSecond = 0.4f;

        [Header("Clumping (nearby pieces merge into a bigger clump — the waves grouped them)")]
        [Tooltip("Distance (m) inside which two pieces merge on the slow tick: the absorbed piece goes " +
                 "dormant, the absorber grows a size tier.")]
        [Min(0.05f)] public float MergeRadiusMeters = 0.8f;

        [Header("Snagging (clumps that drift onto something STICK)")]
        [Tooltip("Distance (m) from a player trap buoy at which drifting weed fouls on its line (read " +
                 "off the Core TrapPlaced signal — never the Fishing module).")]
        [Min(0f)] public float BuoySnagRadiusMeters = 0.9f;
        [Tooltip("Where a snagged piece comes to rest: on this radius (m) around the buoy, along the " +
                 "side it drifted in on. Keep at or under the snag radius.")]
        [Min(0f)] public float BuoyRestRadiusMeters = 0.35f;
        [Tooltip("Clock seconds a snagged piece holds on before the waves break it up and wash it away " +
                 "(it recycles and respawns fresh elsewhere). 0 = it sticks until the buoy is hauled.")]
        [Min(0f)] public float SnagReleaseSeconds = 0f;
        [Tooltip("Water depth (m) at/below which a drifting piece STRANDS on the ground the tide has " +
                 "bared — it beaches at the waterline and stays put.")]
        [Min(0f)] public float StrandDepthMeters = 0.08f;
        [Tooltip("Water depth (m) the returning tide must reach to REFLOAT a stranded piece. Keep it " +
                 "ABOVE the strand depth — the gap is the hysteresis that stops waterline flicker.")]
        [Min(0f)] public float RefloatDepthMeters = 0.25f;

        [Header("Riding the sea (the same shared wave field the buoys bob on)")]
        [Tooltip("Screen-vertical lift (world units) per metre of wave height under the clump. Weed " +
                 "lies IN the surface, so keep it gentler than the buoy's 0.35.")]
        [Min(0f)] public float BobPerMeter = 0.2f;
        [Tooltip("Hard cap (world units) on the bob.")]
        [Min(0f)] public float MaxBobMeters = 0.35f;
        [Tooltip("Max rocking (degrees) as the swell works the clump — scales with local wave height, " +
                 "0 on dead glass.")]
        [Range(0f, 45f)] public float WobbleMaxDegrees = 9f;

        [Header("Look (greybox blobs in the KTC water tones until the painted weed lands)")]
        [Tooltip("Clump footprint (m, sprite width) per size tier, small → big. Merges climb this " +
                 "ladder; 2-3 sizes read well.")]
        public float[] TierSizesMeters = { 0.45f, 0.75f, 1.15f };
        [Tooltip("Painted weed sprites per size tier, same order as TierSizesMeters — the owner's art " +
                 "slots in HERE when it lands. Any empty/short slot falls back to the generated greybox " +
                 "blob, so partial art never breaks the bed.")]
        public Sprite[] TierSprites = System.Array.Empty<Sprite>();
        [Tooltip("Weed tones cycled per piece (KTC palette — dark olive/kelp browns sampled against the " +
                 "North Atlantic water look).")]
        public Color[] Palette =
        {
            new Color(0.23f, 0.29f, 0.16f),   // olive
            new Color(0.18f, 0.24f, 0.15f),   // dark sea-green
            new Color(0.30f, 0.26f, 0.13f),   // kelp brown
            new Color(0.16f, 0.21f, 0.17f),   // bottle dark
        };
        [Tooltip("Peak sprite alpha once faded in.")]
        [Range(0f, 1f)] public float MaxAlpha = 0.95f;
        [Tooltip("Seconds a fresh piece fades in over (respawns never pop).")]
        [Min(0f)] public float FadeInSeconds = 1.5f;
        [Tooltip("SpriteRenderer sortingOrder: the weed floats ON the water (Sea plane is -5) but UNDER " +
                 "the hulls (0), buoys (3) and the mist haze (-2). -3 slots it between water and everything afloat.")]
        public int SortingOrder = -3;
        [Tooltip("Metres toward the camera (-z) each piece is nudged so its sprite reliably clears the " +
                 "water MeshRenderer (the mesh-vs-sprite sorting quirk — kept small, like the spray's).")]
        [Min(0f)] public float CameraZOffset = 0.2f;

        /// <summary>The top size tier merges can grow a clump to (the last rung of
        /// <see cref="TierSizesMeters"/>; 0 when the ladder is missing/empty).</summary>
        public int MaxTier => TierSizesMeters == null || TierSizesMeters.Length == 0
            ? 0 : TierSizesMeters.Length - 1;
    }
}
