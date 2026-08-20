using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One thing a forecourt is assembled from, as data (ADR 0003) — a specific piece at a specific
    /// size. One asset per piece, one stable id.
    ///
    /// <para><b>There is no "gas station" object, and that is the kit's design rather than a gap.</b>
    /// A station is a LIST OF GRADE POSITIONS plus the pieces that stand on it: the valance stripes,
    /// the boot cups, the price-sign rows and the apron fill caps are all generated from that one
    /// list, so a station selling three grades cannot draw four of anything. What a scene places is
    /// these pieces; what binds them together is the grade list they were baked with.</para>
    ///
    /// <para><b>Why this is not a <c>SupplyDef</c>.</b> Same division as
    /// <see cref="FuelContainerDef"/>: <c>HiddenHarbours.Economy</c>'s <c>SupplyDef</c> is the
    /// ECONOMIC half of a consumable — id, display name, price. This is the PHYSICAL half: a thing
    /// with metres, facings, blockers and reach points. Putting metres and sprites on an economy Def
    /// would couple art to the market in the direction CLAUDE.md rule 4 forbids.</para>
    ///
    /// <para>Everything except <see cref="Id"/> and <see cref="DisplayName"/> is DERIVED from the
    /// bake contract and the rig's gameplay sidecar by <c>StationPieceDefBuilder</c>, so a re-bake
    /// updates these assets rather than leaving them stale.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Station Piece", fileName = "StationPiece")]
    public class StationPieceDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only id, e.g. \"station.dispenser_stwin\". Never reuse or rename.")]
        public string Id = "station.dispenser_stwin";

        [Tooltip("Player-facing name. Flavour only; the id is canonical.")]
        public string DisplayName = "Twin Slab Pump";

        [Header("What it is (from the rig)")]
        [Tooltip("Piece family: dispenser · island · canopy · sign · store · fillport · interior.")]
        public string PieceType = "dispenser";

        [Tooltip("The rig's own size key, verbatim — e.g. \"sTwin\". ⚠️ This is what a re-bake matches " +
                 "against, so it keeps the rig's camelCase; the id lowercases it because CLAUDE.md " +
                 "requires type.snake_case, and an id is an identifier rather than a lookup key.")]
        public string SizeKey = "sTwin";

        [Tooltip("The rig's own label, e.g. \"TWIN SLAB\".")]
        public string Label = "TWIN SLAB";

        [Tooltip("True for the sales floor that paints INSIDE a storefront's own cell. Its sprite " +
                 "goes UNDER the shell's at the same world position; they share a pivot, not a cell " +
                 "corner, which is why each crops to its own ink.")]
        public bool IsInterior;

        [Header("Baked art")]
        [Tooltip("Facings baked. Eight for most pieces; FOUR for the 180°-symmetric island and " +
                 "canopy, where d and d+4 are the same picture.")]
        public int Facings = 8;

        [Tooltip("⚠️ True when the four baked cells cover all EIGHT facings by dir % 4 — a symmetry " +
                 "FOLD, not a decimation. Use Frame(dir), never Frames[dir].")]
        public bool Fold180;

        [Tooltip("One sprite per baked cell, in dir order.")]
        public Sprite[] Frames = Array.Empty<Sprite>();

        [Tooltip("The grade list this art was baked with. NOT decoration: the rig generates hose " +
                 "count, sign rows and apron caps from it, so a piece baked for three grades is a " +
                 "different picture from one baked for four.")]
        public string[] BakedGrades = Array.Empty<string>();

        [Header("Physical (metres)")]
        [Min(0)] public float WidthMeters;
        [Min(0)] public float DepthMeters;
        [Min(0)] public float HeightMeters;

        [Header("Dispenser (zero on every other piece)")]
        [Tooltip("Grade positions this machine can carry — one hose each.")]
        [Min(0)] public int MaxHoses;

        [Tooltip("Lanes served: 1 or 2.")]
        [Min(0)] public int MaxSides;

        [Tooltip("How far the nozzle reaches, in metres. The hose itself is NEVER baked — " +
                 "StationIso.serve() returns a runtime polyline and honest metres.")]
        [Min(0)] public float HoseReachMeters;

        [Min(0)] public float LitresPerMinute;

        [Header("Island (zero on every other piece)")]
        [Tooltip("Bolt-down plates. Any dispenser variety mounts at any slot, at its own pivot.")]
        [Min(0)] public int Slots;

        [Min(0)] public float LengthMeters;

        [Tooltip("Slot pitch in metres — 3.30 across the whole kit.")]
        [Min(0)] public float SlotPitchMeters;

        [Header("Canopy (zero on every other piece)")]
        [Min(0)] public int Bays;

        [Tooltip("Underside clearance in metres. A truck that is taller than this does not fit.")]
        [Min(0)] public float ClearHeightMeters;

        [Header("What it does to a walker")]
        [Tooltip("Carried verbatim from the sidecar. The RUNTIME rules on these, not this Def — the " +
                 "same treatment the boat interiors use.")]
        public StationBlocker[] Blockers = Array.Empty<StationBlocker>();

        [Tooltip("Standable surfaces the piece publishes — an island kerb, a walkway, a roof deck.")]
        public StationSurface[] Walkables = Array.Empty<StationSurface>();

        [Header("What it offers")]
        public StationInteractable[] Interactables = Array.Empty<StationInteractable>();

        [Header("Ways in (storefronts only)")]
        public StationDoorway Entry = new StationDoorway();

        public StationDoorway ServiceDoor = new StationDoorway();

        /// <summary>
        /// The sprite for a facing, honouring the fold.
        ///
        /// <para>⚠️ Use this rather than indexing <see cref="Frames"/>. A folded piece has FOUR frames
        /// covering eight facings, so <c>Frames[dir]</c> throws for half the compass — and a caller
        /// who "fixes" that by clamping shows the wrong side instead.</para>
        /// </summary>
        public Sprite Frame(int dir)
        {
            if (Frames == null || Frames.Length == 0) return null;
            int n = Frames.Length;
            return Frames[((dir % n) + n) % n];
        }

        /// <summary>True when the piece publishes at least one baked frame.</summary>
        public bool HasArt => Frames != null && Frames.Length > 0 && Frames[0] != null;
    }

    /// <summary>
    /// Something on a piece that a walker has to deal with.
    ///
    /// <para><b>Carried, not subtracted</b> — the same ruling the deck and boat-interior sidecars
    /// follow: obstructions are listed BESIDE the walkable polygon rather than cut out of it, which
    /// is why a 0.98 m counter can be something you see over rather than a hole in the floor.</para>
    /// </summary>
    [Serializable]
    public sealed class StationBlocker
    {
        [Tooltip("The sidecar's own kind — cabinet · plinth · kerb · bollard · post · pole · base · " +
                 "a_frame · building · ice · propane_cage · ladder_cage · plant · flue · risers.")]
        public string Kind = "";

        [Tooltip("Which surface this stands on: apron · sales_floor · service_stoop · roof.")]
        public string Level = "";

        [Tooltip("Plan footprint in piece-local metres, origin at the ground centre of the piece's " +
                 "own footprint. Empty when the sidecar gave a circle instead — see Radius.")]
        public Vector2[] Footprint = Array.Empty<Vector2>();

        [Tooltip("Centre for a circular blocker (a bollard, a flue). Ignored when Footprint is set.")]
        public Vector2 Center;

        [Tooltip("Radius in metres for a circular blocker; 0 for a polygon one.")]
        [Min(0)] public float Radius;

        [Tooltip("How far this stands above ITS OWN LEVEL, not above the ground — a roof plant's " +
                 "0.45 m is above the deck.")]
        public float HeightAboveGradeMeters;

        [Tooltip("The sidecar's own word for what it does to a walker: 'wall' (blocks and hides), " +
                 "'waist_block' (blocks, seen over), 'step_over' (crossed, not blocked), 'flat' " +
                 "(nothing). Carried verbatim — the runtime rules on it, not this.")]
        public string Treatment = "";

        /// <summary>True when the sidecar described a circle rather than a polygon.</summary>
        public bool IsCircle => Radius > 0f && (Footprint == null || Footprint.Length < 3);

        /// <summary>
        /// Whether this stops a walker. <c>wall</c> and <c>waist_block</c> do; <c>step_over</c> and
        /// <c>flat</c> do not — a 0.165 m island kerb is crossed, not climbed.
        /// </summary>
        public bool Blocks => Treatment == "wall" || Treatment == "waist_block";
    }

    /// <summary>A surface a walker can stand on — the sidecar's <c>WALK</c>.</summary>
    [Serializable]
    public sealed class StationSurface
    {
        [Tooltip("apron · walkway · service_stoop · roof · pad · kerb.")]
        public string Id = "";

        [Tooltip("Height above grade in metres. A flush pad is 0; the island kerb is 0.165.")]
        public float HeightMeters;

        [Tooltip("Plan outline in piece-local metres. No holes — see StationBlocker.")]
        public Vector2[] Outline = Array.Empty<Vector2>();

        [Tooltip("The sidecar's treatment for getting onto it — 'flat' or 'step_over'.")]
        public string Treatment = "";
    }

    /// <summary>
    /// Something the player can use, with a standing spot that was TESTED rather than requested.
    ///
    /// <para>The rig's <c>auditReach()</c> marches a 0.22 m body out along the fitting→point line in
    /// 6 cm steps until it clears every blocker at that level, and publishes the verdict. A point it
    /// could not clear is published as <b>null with a reason</b> rather than as a plausible lie — so
    /// <see cref="HasReachPoint"/> false means "nowhere to stand", not "not measured".</para>
    /// </summary>
    [Serializable]
    public sealed class StationInteractable
    {
        [Tooltip("The sidecar's own id — till · cooler · coffee · snacks · hotcase · atm · ice_box …")]
        public string Id = "";

        [Tooltip("Player-facing label, e.g. \"ICE\".")]
        public string Label = "";

        [Tooltip("The verb the sidecar names, e.g. \"buy_ice\". NOTHING reads this yet — the interact " +
                 "verb it needs does not exist. Carried so the economy lane has it when it does.")]
        public string Action = "";

        [Tooltip("Where the fitting is, in piece-local metres, +z up.")]
        public Vector3 Position;

        [Tooltip("Where a body stands to use it. TESTED against this piece's own blockers.")]
        public Vector3 ReachPoint;

        [Tooltip("True when the audit found a clear spot. FALSE means the rig refused to invent one.")]
        public bool HasReachPoint = true;

        [Tooltip("The audit's own word: ok · moved · flipped · relocated · no_clear_spot.")]
        public string ReachVerdict = "";

        [Tooltip("Which surface the body stands on.")]
        public string Level = "";

        [Tooltip("Facings this reads from, computed with the rasteriser's own camera-facing test.")]
        public string[] VisibleFacings = Array.Empty<string>();

        /// <summary>
        /// ⚠️ The audit's scope is ONE PIECE. A dispenser is not told the island is under it, so a
        /// forecourt laid out by <c>StationIso.plan()</c> still needs checking as a whole.
        /// </summary>
        public bool ReachTestedAgainstThisPieceOnly => true;
    }

    /// <summary>
    /// A way in. <b>Doors default SHUT</b> (owner ruling, 2026-08-19).
    ///
    /// <para>⚠️ The two doors on a storefront are not the same shape of problem, and the difference is
    /// the collider. The customer entry is a <c>slide_bipart</c>: no swept arc, so
    /// <see cref="HasKeepClear"/> is FALSE <i>by design</i> and the collider is the LEAF, which
    /// travels inside the wall line. The service door is a single leaf swinging OUT — it does sweep,
    /// and it owns a keep-clear.</para>
    /// </summary>
    [Serializable]
    public sealed class StationDoorway
    {
        [Tooltip("slide_bipart · swing_single_out. Empty when the piece has no such door.")]
        public string Kind = "";

        [Min(0)] public float ClearWidthMeters;
        [Min(0)] public float ClearHeightMeters;

        [Tooltip("Threshold line midpoint in piece-local metres.")]
        public Vector3 Threshold;

        [Tooltip("One leaf's width. For the bipart slider this is the collider that matters.")]
        [Min(0)] public float LeafWidthMeters;

        [Min(0)] public float TravelMeters;

        [Tooltip("How far open before 0.80 m of body fits through.")]
        [Range(0f, 1f)] public float ClearAtOpenFraction;

        [Tooltip("Step up over the sill, in metres.")]
        public float SillStepMeters;

        [Tooltip("Treatment for the sill itself — 'step_over' on both doors here.")]
        public string Treatment = "";

        [Tooltip("FALSE on the bipart slider BY DESIGN — a slider sweeps nothing, so there is no " +
                 "volume to keep clear and the collider is the leaf. Do not read a false here as " +
                 "missing data.")]
        public bool HasKeepClear;

        [Tooltip("The swept volume a swinging leaf needs, when it has one.")]
        public Vector2[] KeepClear = Array.Empty<Vector2>();

        /// <summary>True when the piece publishes this door at all.</summary>
        public bool Exists => !string.IsNullOrEmpty(Kind);
    }
}
