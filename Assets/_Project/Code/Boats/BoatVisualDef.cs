using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>How a hull LOOKS — as data, not as a const (ADR 0003, rule 2).</b> One of these describes a
    /// complete directional boat skin: the compass of pre-drawn hull facings, the optional wave-coupled
    /// rock grid, and the optional per-side oar overlays. A <see cref="BoatHullDef"/> points at one via
    /// <see cref="BoatHullDef.Visual"/>; <see cref="BoatHullSkinner"/> is the ONE place that installs it.
    ///
    /// <para>Before this existed, the player's skin was a <c>const bool</c> plus a fistful of
    /// <c>const string</c> art paths inside an editor-only builder — so a hull could not say what it
    /// looked like, and <see cref="OwnedFleet"/>'s hull swap could not re-skin the boat (it wrote
    /// <c>.sprite</c> onto a renderer the skin had disabled: you bought the Punt and kept the dory's
    /// picture). Adding a hull's look is now authoring an asset, not editing C#.</para>
    ///
    /// <para><b>Conventions this asset must honour</b> (they are load-bearing across the whole boat rig):
    /// bow = <c>transform.up</c>; heading 0 = North, clockwise; <see cref="Facings"/> element 0 is the
    /// NORTH-facing picture; sheet slice index is <c>heading·frameCount + frame</c>, row-major from the
    /// top-left, sprites named <c>&lt;Stem&gt;_&lt;index&gt;</c>. PPU 32 everywhere. Every layer shares
    /// the hull's cell + waterline pivot so an overlay at the same localPosition registers pixel-perfect.</para>
    ///
    /// <para><b>All-or-nothing, by design.</b> Each block gates on being COMPLETE
    /// (<see cref="HasFullCompass"/> / <see cref="HasRockGrid"/> / <see cref="HasOarSheets"/>). A partial
    /// set never half-ships — one missing facing would snap the boat into a stale picture mid-turn — so
    /// anything short of a full block falls back to the block below it, ending at the plain
    /// <see cref="BoatHullDef.Sprite"/> rotating hull.</para>
    ///
    /// <para><b>Extending this (read before adding a layer).</b> A new overlay binds by adding its own
    /// append-only block of fields here and installing it from <see cref="BoatHullSkinner.Apply"/>, which
    /// hands back a <see cref="BoatHullSkinner.Rig"/> carrying the visual child, the hull renderer and the
    /// <see cref="DirectionalBoatSprite"/> to layer onto. Do NOT add art paths to a builder — bind sheets
    /// here. The MOTOR block below is that append, done: upper/lower sheets, 9 steer columns × 8 headings,
    /// two paint builds and a twin-engine fit, and it needed no change to the skinner's shape.</para>
    ///
    /// <para><b>Overlays can collide — check before adding one.</b> The oar and motor blocks are mutually
    /// exclusive (<see cref="HasConflictingOverlays"/>) because their sorting bands overlap. A third overlay
    /// must either take a band above them or declare its own exclusion; it must not quietly share one.</para>
    ///
    /// Create via Assets &gt; Create &gt; Hidden Harbours &gt; Boat Visual, save in Data/Boats/Visuals.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Boat Visual", fileName = "BoatVisual")]
    public class BoatVisualDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case, e.g. visual.dory_iso.")]
        public string Id = "visual.dory_iso";

        [Header("Hull compass (REQUIRED — element 0 = North, then CLOCKWISE)")]
        [Tooltip("The pre-drawn hull facings in CLOCKWISE order from the zero heading: for the 8-way set " +
                 "N, NE, E, SE, S, SW, W, NW. The snap math is generalised to ANY count, so a 16-way set " +
                 "drops in with no code change. Empty (or any slot unassigned) = this hull has no skin and " +
                 "renders the plain rotating BoatHullDef.Sprite exactly as it did before skins existed.")]
        public Sprite[] Facings = System.Array.Empty<Sprite>();

        [Tooltip("The compass heading (degrees, 0 = North/up, CW) that Facings[0] is drawn for. 0 because " +
                 "element 0 is the North-facing picture — the project's bearing convention. Leave at 0 " +
                 "unless the art is genuinely drawn off-axis.")]
        public float ZeroHeadingDegrees = 0f;

        [Tooltip("Tick ONLY for art whose cells run COUNTER-CLOCKWISE — i.e. cell i depicts heading −45°·i, " +
                 "not +45°·i. The 3D rigs that bake the iso kits (dory, punt, skiffs) rotate the model CCW " +
                 "but LABEL the cells clockwise (N, NE, E...), so their sheets are mirrored: their 'E' cell " +
                 "is really a boat pointing West. This flag is the un-mirror, and it lives here — per " +
                 "artwork — because the two art lineages genuinely disagree: the iso sheets are CCW, while " +
                 "the older FishingBoat_* compass (8 separate hand-drawn files) is CW and CORRECT. A blanket " +
                 "fix in the code would have fixed the first and broken the second. Default false = the CW " +
                 "convention, so no existing skin silently flips. Only the ART is affected: the boat's true " +
                 "heading, the wake and the spotlight always rode the real heading and were never wrong.")]
        public bool FacingsAreCounterClockwise = false;

        [Tooltip("SpriteRenderer sortingOrder for the hull picture. 1 draws it above the hidden base hull " +
                 "renderer at 0 and below the on-foot player at 10. Overlays take the orders above this.")]
        public int SortingOrder = 1;

        [Header("Rock grid (OPTIONAL — wave-coupled rock, drawn not faked)")]
        [Tooltip("Heading×frame rock sheet, element [heading·RockFrameCount + frame]. When COMPLETE " +
                 "(length == Facings.Length × RockFrameCount), BoatWaveMotion drives the visible rock BY " +
                 "FRAME from the wave phase under the hull (crest → frame 2, trough → 6) and the fake " +
                 "transform rock is retired for this hull. Empty = the static facings + the legacy " +
                 "transform rock, exactly as before.")]
        public Sprite[] RockGrid = System.Array.Empty<Sprite>();

        [Tooltip("Rock frames per heading in the grid (the DoryIsoRock sheet ships 8).")]
        [Min(1)] public int RockFrameCount = 8;

        [Header("Oar overlays (OPTIONAL — the independent baked oars)")]
        [Tooltip("Port-side oar sheet, element [heading·OarColumnCount + column]. Columns 0..7 = the row-" +
                 "stroke cycle, 8 = resting/shipped, 9 = trailing. Animated from the boat's REAL per-oar " +
                 "state (DoryOarLayer), never a free-running loop. Empty = no oars are drawn for this hull.")]
        public Sprite[] OarPort = System.Array.Empty<Sprite>();

        [Tooltip("Starboard-side oar sheet — same layout as OarPort.")]
        public Sprite[] OarStar = System.Array.Empty<Sprite>();

        [Tooltip("Oar columns per heading row (the DoryOar* sheets ship 10).")]
        [Min(1)] public int OarColumnCount = 10;

        [Header("Outboard motor (OPTIONAL — the hull's engine, drawn)")]
        [Tooltip("Motor LOWER sheet (leg + plate + skeg + prop), element [heading·MotorColumnCount + col]. " +
                 "Col 0 = full port, the middle col = dead ahead, the last = full starboard. When COMPLETE " +
                 "alongside MotorUpper, OutboardMotorLayer swivels the engine from the boat's REAL helm. Empty " +
                 "= this hull has no engine drawn (the dory and the fishing boat).")]
        public Sprite[] MotorLower = System.Array.Empty<Sprite>();

        [Tooltip("Motor UPPER sheet (clamp bracket + cowl) — same layout as MotorLower.")]
        public Sprite[] MotorUpper = System.Array.Empty<Sprite>();

        [Tooltip("Steer columns per heading row (every shipped motor sheet — skiff and punt alike — has 9).")]
        [Min(1)] public int MotorColumnCount = OutboardMotorMath.SteerColumns;

        [Tooltip("Steer authority at the sheet's extremes, in DEGREES either side of dead ahead. An ART FACT " +
                 "of the sheets bound above — NOT a feel knob (it never touches how the boat handles, only " +
                 "how far the drawn engine swings). The skiffs bake ±30 across their 9 columns = 7.5° steps; " +
                 "the punt bakes ±32 = 8° steps. Sensible range 5–45; 0 falls back to the layer's default.")]
        [Min(0f)] public float MotorMaxSteerDegrees = OutboardMotorMath.MaxSteerDegrees;

        [Tooltip("Which paint build of the outboard this hull carries — Work (graphite cowl, the console " +
                 "workboat), Sport (white cowl + teal flash, the sport skiff), or the punt's tiller engine as " +
                 "Basic (weathered grey/black starter) / Upgraded (domed cowl, gloss pan, red wrap stripe). " +
                 "Identity only: the sheets above are what actually get drawn.")]
        public OutboardMotorLayer.MotorVariant MotorVariant = OutboardMotorLayer.MotorVariant.Work;

        [Tooltip("How many engines hang on the transom: Single (one, on the centreline — the console workboat " +
                 "and the punt) or Twin (two at ±0.34 m, steering together off the one wheel — the SAME " +
                 "sheets blitted twice). Twin is the sport skiff's upgrade and needs no extra art; no other " +
                 "kit ships one.")]
        public OutboardMotorLayer.MotorFit MotorFit = OutboardMotorLayer.MotorFit.Single;

        [Header("Motor rock coupling (the motor cells are baked LEVEL — these lean them onto the wave)")]
        [Tooltip("Degrees of lean at the peak of the ROLL (the art rigs' rollA). Console 3.4 (heavier hull, " +
                 "stiffer), Sport 3.8 (light glass hull, livelier), Punt 4.2 (beamier than the dory, so a " +
                 "stiffer roll than her); the dory reference is 5. This poses the LEVEL-baked engine onto the " +
                 "hull's rock — it is NOT the hull's own rock, which is baked into its frames. Do not " +
                 "double-rock.")]
        public float MotorRockRollDegrees = 3.4f;

        [Tooltip("Screen-vertical METRES at the peak of the PITCH. The art rigs give pitchA in DEGREES " +
                 "(console 1.9, sport 2.2, punt 2.4, dory 3.0) and the dory reads its 3.0 as 0.02 m of " +
                 "vertical travel in the ¾ view — 0.00667 m per degree. The same conversion puts the console " +
                 "at 0.0127, the sport at 0.0147 and the punt at 0.016. Keep small; this is a screen offset, " +
                 "not a rotation.")]
        public float MotorRockPitchOffsetMeters = 0.0127f;

        [Tooltip("Baked HEAVE amplitude in pixels (the rigs' heaveA). Console 1.3, Sport 1.5, Punt 1.5.")]
        public float MotorRockHeavePixels = 1.3f;

        // ---- the all-or-nothing gates (pure; EditMode-testable without a scene) --------------------

        /// <summary>How many hull headings this skin is drawn for (the compass size). 0 = no compass.</summary>
        public int HeadingCount => Facings != null ? Facings.Length : 0;

        /// <summary>
        /// True when <see cref="Facings"/> is a COMPLETE compass (non-empty, every slot assigned) — the
        /// gate every consumer renders the directional hull behind. False means "this hull has no skin":
        /// the plain rotating <see cref="BoatHullDef.Sprite"/> stands, untouched.
        /// </summary>
        public bool HasFullCompass() => IsComplete(Facings);

        /// <summary>
        /// True when a full heading×frame rock grid is wired (length == compass × <see cref="RockFrameCount"/>).
        /// Requires a full compass — a rock grid without headings to index is meaningless.
        /// </summary>
        public bool HasRockGrid() =>
            HasFullCompass() && RockFrameCount > 0 &&
            IsComplete(RockGrid) && RockGrid.Length == HeadingCount * RockFrameCount;

        /// <summary>
        /// True when BOTH oar sheets give their full heading×column set. Both-or-neither: one oar drawn
        /// and one missing is worse than a rowboat with no oars drawn, and a partial sheet would index a
        /// stale cell.
        /// </summary>
        public bool HasOarSheets()
        {
            if (!HasFullCompass() || OarColumnCount <= 0) return false;
            int expected = HeadingCount * OarColumnCount;
            return IsComplete(OarPort) && OarPort.Length == expected &&
                   IsComplete(OarStar) && OarStar.Length == expected;
        }

        /// <summary>
        /// True when BOTH motor sheets give their full heading×column grid — the gate
        /// <see cref="BoatHullSkinner"/> hangs the outboard behind. Both-or-neither: a leg with no cowl (or
        /// a cowl with no leg) is a broken engine, and a partial sheet would index a stale cell. Requires a
        /// full compass, since the motor picks the hull's heading row.
        /// </summary>
        public bool HasMotor()
        {
            if (!HasFullCompass() || MotorColumnCount <= 0) return false;
            int expected = HeadingCount * MotorColumnCount;
            return IsComplete(MotorLower) && MotorLower.Length == expected &&
                   IsComplete(MotorUpper) && MotorUpper.Length == expected;
        }

        /// <summary>
        /// <b>Oars and an outboard are mutually exclusive</b>, and this is the assertion that says so out
        /// loud. The two overlays' sorting bands OVERLAP by construction: the oars take hull+1 (port) and
        /// hull+2 (starboard), while the motor's lower layer takes hull+1/+2 whenever it draws OVER the hull
        /// (<see cref="OutboardMotorMath.SortingOrder"/>). A hull wearing both would have its port oar and its
        /// engine leg fighting for the same order — a z-flicker that changes with heading.
        ///
        /// <para>Re-basing a band would be the fix if a hull ever genuinely needed both (an auxiliary
        /// outboard on a rowing hull). Nothing does: rowing hulls row, powered hulls have engines. So this
        /// stays a checked invariant (the content validator asserts it across every authored visual) rather
        /// than a band rebase bought on speculation.</para>
        /// </summary>
        public bool HasConflictingOverlays() => HasOarSheets() && HasMotor();

        private static bool IsComplete(Sprite[] set)
        {
            if (set == null || set.Length == 0) return false;
            for (int i = 0; i < set.Length; i++)
                if (set[i] == null) return false;
            return true;
        }

        /// <summary>
        /// Build a throwaway skin binding in memory from a bare facing compass — the adapter for callers
        /// that already carry their own facings as data and have no asset to point at (the ambient fleet's
        /// <c>AmbientFleetDef.HullFacings</c>, the rotation-test harness). Lets those call sites share the
        /// ONE <see cref="BoatHullSkinner"/> install path instead of re-implementing the rig. No rock grid,
        /// no oars — exactly what those callers rendered before.
        /// </summary>
        public static BoatVisualDef CreateRuntime(Sprite[] facings, int sortingOrder = 1,
                                                  float zeroHeadingDegrees = 0f)
        {
            var def = CreateInstance<BoatVisualDef>();
            def.Id = "visual.runtime";
            def.Facings = facings ?? System.Array.Empty<Sprite>();
            def.SortingOrder = sortingOrder;
            def.ZeroHeadingDegrees = zeroHeadingDegrees;
            def.RockGrid = System.Array.Empty<Sprite>();
            def.OarPort = System.Array.Empty<Sprite>();
            def.OarStar = System.Array.Empty<Sprite>();
            def.MotorLower = System.Array.Empty<Sprite>();
            def.MotorUpper = System.Array.Empty<Sprite>();
            return def;
        }
    }
}
