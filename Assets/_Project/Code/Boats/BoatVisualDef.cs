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
    /// <para><b>Extending this (read before adding a layer).</b> A new overlay — e.g. the coming MOTOR
    /// layer (upper/lower sheets, 9 steer columns × 8 headings, two paint builds, a twin-engine option)
    /// — binds by adding its own append-only block of fields here and installing it from
    /// <see cref="BoatHullSkinner.Apply"/>, which hands back a <see cref="BoatHullSkinner.Rig"/> carrying
    /// the visual child, the hull renderer and the <see cref="DirectionalBoatSprite"/> to layer onto. Do
    /// NOT add art paths to a builder — bind sheets here.</para>
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
            return def;
        }
    }
}
