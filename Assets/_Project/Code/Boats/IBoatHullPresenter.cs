using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The seam a hull is DRAWN through (ADR 0022 phase 1).</b> Everything that layers onto, anchors to,
    /// or reads the pose of the drawn hull — the wake, the oars, the outboard, the deck props, the wave
    /// rider, the deck-walk clamp — needs the same five facts, and today it gets them by holding a concrete
    /// <see cref="DirectionalBoatSprite"/>. That reference is the only reason a mesh hull cannot be dropped
    /// in beside a sprite one. This interface is that reference, named.
    ///
    /// <para><b>Phase 1 adds NO behaviour.</b> The one implementation is
    /// <see cref="SpriteHullPresenter"/>, a thin adapter over the shipped
    /// <see cref="DirectionalBoatSprite"/>; every existing consumer keeps its concrete field and is
    /// untouched. <see cref="BoatHullSkinner.Rig.Presenter"/> hands one back so the seam has a real
    /// production wiring point, but nothing reads it yet. Repointing the consumers is phase 4, when there is
    /// a second implementation to justify the churn.</para>
    ///
    /// <para><b>Designed from what the sprite path actually does, not from what a mesh might want.</b>
    /// Members that look sprite-flavoured are here because a consumer genuinely needs them:</para>
    /// <list type="bullet">
    ///   <item><see cref="FacingCellIndex"/> / <see cref="FacingCount"/> — the oar and motor layers pick
    ///   their OWN sheet row by cell, and <see cref="MountedRockPoseMath.Project"/> takes the rig's
    ///   turntable angle in CELL space, not the compass heading. A mesh reports <see cref="FacingCount"/>
    ///   0 and <see cref="FacingCellIndex"/> 0: it has no cells, and 0 headings is the documented
    ///   "unquantised" signal that <see cref="DirectionalBoatSprite.SnapHeadingDegrees"/> already
    ///   understands.</item>
    ///   <item><see cref="FacingsAreCounterClockwise"/> — <b>per-artwork and load-bearing</b> for sprite
    ///   sheets (boats true, characters false; see ADR 0021 and the mirrored-art regression). Meaningless
    ///   for a mesh, which simply returns false. It is NOT cleaned up.</item>
    ///   <item><see cref="BakeElevationDegrees"/> — an ART FACT of the sheets that every on-hull anchor
    ///   reads. A mesh hull's own projection has the same elevation baked into its object transform, so it
    ///   reports the same number rather than a special case.</item>
    /// </list>
    ///
    /// <para><b>Invariant this does not change:</b> the presenter lives on (or is driven from) the PHYSICS
    /// ROOT, and the visual child's world rotation is stomped every LateUpdate. Anything that must follow
    /// the bow still rides the root; additive rotation still routes through
    /// <see cref="VisualTiltDegrees"/>. A presenter is not a licence to move a heading consumer onto the
    /// visual — that once ate the boat spotlight.</para>
    /// </summary>
    public interface IBoatHullPresenter
    {
        /// <summary>Which rendering path is behind this presenter. Lets a consumer branch without a cast.</summary>
        BoatHullVariant Variant { get; }

        /// <summary>
        /// The compass heading (degrees, 0 = North, CW) of the hull picture CURRENTLY on screen —
        /// quantised to the facing grid for a sprite hull, continuous for a mesh. This is the heading
        /// anything pinned to the DRAWN hull must use, never the physics body's true heading.
        /// A method, not a property, because it is computed from the live transform each call — exactly as
        /// <see cref="DirectionalBoatSprite.DrawnHeadingDegrees"/> is today.
        /// </summary>
        float DrawnHeadingDegrees();

        /// <summary>
        /// The facing cell currently drawn, in [0, <see cref="FacingCount"/>). <b>Cell space, not heading
        /// space</b> — for counter-clockwise art this is the mirrored cell, which is the whole point (the
        /// overlay sheets are baked by the same rig and must mirror WITH the hull). 0 for a mesh hull.
        /// </summary>
        int FacingCellIndex { get; }

        /// <summary>How many facing cells this hull is drawn for. <b>0 for a mesh hull</b> — no quantisation.</summary>
        int FacingCount { get; }

        /// <summary>
        /// True when this skin's cells run COUNTER-CLOCKWISE (cell i depicts −step·i). Per-ARTWORK and
        /// load-bearing for sprite sheets; always false for a mesh. See
        /// <see cref="BoatVisualDef.FacingsAreCounterClockwise"/>.
        /// </summary>
        bool FacingsAreCounterClockwise { get; }

        /// <summary>
        /// Elevation (degrees above the horizon) of the camera the hull is presented at — 40 for the iso
        /// rigs, 90 (plan view, no foreshortening) for hand-drawn compasses. Everything anchored to a point
        /// ON the hull reads this to squash honest world metres onto the screen.
        /// </summary>
        float BakeElevationDegrees { get; }

        /// <summary>
        /// <b>Where the sea stands on this hull when she floats at rest</b> — metres above the art's own
        /// origin (owner playtest 2026-08-07: <i>"generally they should level out at the boats water
        /// line"</i>). Mesh: the def's <c>RestingDraftMeters</c>, measured against the rig's keel-bottom
        /// pivot. Sprite: <see cref="BoatVisualDef.DesignWaterlineMeters"/> — 0 for art already drawn at
        /// her waterline, which is every shipped compass.
        ///
        /// <para>It rides the seam for the reason the seam exists at all: <see cref="BoatWaveMotion"/>
        /// computes the ride and knows the hull ONLY as this interface, so this is its only route to
        /// the datum when it must sink a SPRITE hull's transform itself. Composed with
        /// <see cref="BakeElevationDegrees"/> through <see cref="HullSettleMath"/>; a hull with no datum
        /// (0) sinks by exactly 0 and renders byte-identically to before the settle fix.</para>
        ///
        /// <para>⚠️ <b>A MESH hull is sunk by <see cref="MeshHullDriver"/>, not by the caller</b> —
        /// deliberately, so a mesh hull with no wave motion wired at all still sits at her waterline.
        /// <see cref="SetDisplacedHeaveMeters"/> therefore carries the SEA'S LIFT and never a settled
        /// ride; sinking it on the way in as well would sink her twice.</para>
        /// </summary>
        float DesignWaterlineMeters { get; }

        /// <summary>
        /// True when this hull can present a distinct pose per rock frame. Sprite: a complete heading×frame
        /// rock grid is wired. Mesh: always true, because rock is a transform and costs no memory (ADR 0022).
        /// </summary>
        bool HasRockGrid { get; }

        /// <summary>
        /// The wave-driven rock frame to present; <b>−1 = the calm/level pose</b>.
        /// <see cref="BoatWaveMotion"/> writes it each LateUpdate from the wave phase under the hull.
        /// Ignored when <see cref="HasRockGrid"/> is false. A presenter with
        /// <see cref="SupportsContinuousRock"/> treats −1 as "level" too; its canonical rock input is
        /// <see cref="SetRockPhaseDegrees"/>.
        /// </summary>
        int RockFrame { get; set; }

        /// <summary>
        /// True when this presenter can pose rock CONTINUOUSLY from a wave phase instead of snapping
        /// to baked frames — mesh hulls (rock is a transform and costs no memory, ADR 0022). False
        /// for sprite hulls, whose rock IS the baked frame grid. When true,
        /// <see cref="BoatWaveMotion"/> writes <see cref="SetRockPhaseDegrees"/> instead of
        /// quantising to <see cref="RockFrame"/>.
        /// </summary>
        bool SupportsContinuousRock { get; }

        /// <summary>
        /// Pose the hull's rock from a reconstructed wave phase (degrees; crest = 90°, trough = 270°
        /// — <see cref="DoryRockMath.PhaseDegrees"/>' convention, the same number that picks a sprite
        /// hull's frame). Only meaningful when <see cref="SupportsContinuousRock"/>; a sprite
        /// presenter ignores it (its rock arrives as <see cref="RockFrame"/>). Writing
        /// <see cref="RockFrame"/> = −1 afterwards levels the hull.
        /// </summary>
        void SetRockPhaseDegrees(float phaseDegrees);

        /// <summary>
        /// Additive visual tilt (degrees, +CCW about z) composed AFTER the presenter's own rotation policy.
        /// The presenter force-resets the visual's rotation every LateUpdate, so an external rotation write
        /// is silently eaten — this is the supported hook. 0 = no tilt.
        /// </summary>
        float VisualTiltDegrees { get; set; }

        /// <summary>
        /// The metre-scale vertical RIDE of the displaced sea under this hull (ADR 0023 phase 3
        /// step 2 — the shared heave): <c>ShoreFadeMath.DisplacedHeight</c> of the wave height at
        /// the hull, written by <see cref="BoatWaveMotion"/> every tick while the displaced sea is
        /// active, 0 otherwise. Only meaningful for a presenter with
        /// <see cref="SupportsContinuousRock"/>: the MESH path routes it through the renderer's
        /// heave-pixels channel, so the screen lift and the calibrated waterline z
        /// (<c>DisplacedWaterMath.HullDepthBias</c>) ride together by construction — the waterline
        /// stays truthful for free. A SPRITE presenter ignores it (deliberately, like
        /// <see cref="SetRockPhaseDegrees"/>): sprite hulls have no waterline clipping, so their
        /// ride is a plain visual-transform lift that <see cref="BoatWaveMotion"/> applies itself.
        /// </summary>
        void SetDisplacedHeaveMeters(float heaveMeters);

        /// <summary>
        /// The STORM ROCK channel (ADR 0018 B2.5), written by <see cref="BoatWaveMotion"/> every tick:
        /// <paramref name="amplitudeScale"/> multiplies the hull's own canned rock amplitudes (1 =
        /// the tuned calm cycle, exactly), and the extras are REAL additional attitude degrees from
        /// the decomposed wave slope under the hull — a head sea pitches the bow, a beam sea rolls
        /// the deck, growing with the sea state where the fixed def amplitudes cannot. Only
        /// meaningful for a presenter with <see cref="SupportsContinuousRock"/> (attitude is a free
        /// transform there); a SPRITE presenter ignores it (deliberately, like
        /// <see cref="SetRockPhaseDegrees"/>): its rock IS the baked frame grid, drawn at one baked
        /// amplitude — a storm read for sprite hulls arrives as the offset/squash
        /// <see cref="BoatWaveMotion"/> layers under the frames, and a steeper BAKE is an
        /// art-director call, not a runtime rescale. (1, 0, 0) is the exact neutral.
        /// </summary>
        void SetStormRock(float amplitudeScale, float extraRollDegrees, float extraPitchDegrees);

        /// <summary>
        /// <b>Somebody is standing on this deck, HERE</b> — their feet, in the hull's own rig metres
        /// (+X starboard, +Y toward the bow, +Z up from the keel: the frame the authored deck
        /// polygons and every fitting pivot already speak). <paramref name="active"/> false = nobody.
        ///
        /// <para><b>Why a hull is told this at all</b> (owner playtest 2026-08-07: "rider/player
        /// sprites visible THROUGH closed cabins"). A figure on deck is a sprite Y-sorted above the
        /// hull's whole-object sorting slot, so no ordering can put a wheelhouse in front of them:
        /// sorting is per OBJECT, and "is the boat between the camera and the fisher?" is per PIXEL.
        /// A MESH hull can answer it, because her facet pass owns a private z-buffer — told where the
        /// figure stands, she marks the geometry nearer the camera than that point so the figure's
        /// own shader discards behind it. At any heading, with no authored cabin footprint.</para>
        ///
        /// <para>A SPRITE hull ignores it, deliberately and for the same reason she ignores
        /// <see cref="SetRockPhaseDegrees"/>: her image is one flat sheet with no depth in it, and
        /// there is nothing honest she could do with the question. The whole pilotable fleet is mesh
        /// (ADR 0022), so the fix reaches every hull the player can actually stand on.</para>
        /// </summary>
        void SetDeckOccupant(Vector3 rigLocalMeters, bool active);

        /// <summary>
        /// The id an occludable sprite must discard against to be hidden by this hull, already
        /// divided by 255 for the shader — <b>0 whenever there is nothing to hide behind</b>: no
        /// occupant set, a sprite hull, a hull not live. Read every tick by whoever draws the
        /// figure; 0 leaves their shader inert.
        ///
        /// <para>⚠️ The SINGLE-OCCUPANT shim, with <see cref="SetDeckOccupant"/>. More than one thing
        /// on a deck claims its own slot through <see cref="DeckOccupants"/> instead.</para>
        /// </summary>
        float DeckOccluderId { get; }

        /// <summary>
        /// <b>Everything standing on this deck</b> — the fixed slot array behind the per-pixel
        /// occlusion, so gear, pots and a second hand can each be hidden at their OWN depth rather
        /// than sharing one split plane (<see cref="HiddenHarbours.Core.IDeckOccupantSlots"/>).
        ///
        /// <para>Never null. A sprite hull hands back
        /// <see cref="HiddenHarbours.Core.NoDeckOccupantSlots.Instance"/> for the same reason she
        /// ignores <see cref="SetDeckOccupant"/> — a flat sheet has no depth to split — so a caller
        /// needs no branch: every claim is refused and every id is 0, which draws exactly as before
        /// any of this existed.</para>
        /// </summary>
        HiddenHarbours.Core.IDeckOccupantSlots DeckOccupants { get; }

        /// <summary>The visual child the hull is drawn into. Overlays parent here; nothing may re-parent it.</summary>
        Transform Visual { get; }

        /// <summary>
        /// Where points ON the drawn hull are, for the pose currently presented. Never null.
        /// See <see cref="IBoatHullAnchors"/>.
        /// </summary>
        IBoatHullAnchors Anchors { get; }
    }

    /// <summary>
    /// The named points on the art director's rigs that gameplay hangs things off — the exact set
    /// <c>RigBaker.AnchorFunctions</c> bakes, so the enum and the bake cannot drift.
    /// <b>Append-only</b>: these are persisted nowhere today, but they name art, and art ids are stable.
    /// </summary>
    public enum BoatAnchorId
    {
        /// <summary>Where the outboard's clamp hangs on the transom.</summary>
        MotorMount = 0,
        /// <summary>The bait/catch tub positions on deck (MULTI-point).</summary>
        TubMounts = 1,
        /// <summary>Where the helmsman stands/sits at the wheel.</summary>
        HelmSeat = 2,
        /// <summary>Where a tiller-steered hull's hand goes.</summary>
        TillerGrip = 3,
        /// <summary>Where the pot hauler is bolted.</summary>
        HaulerMount = 4,
        /// <summary>Mast/aerial/navigation-light positions (MULTI-point).</summary>
        NavMounts = 5,
        /// <summary>Where a standing pilot's feet are.</summary>
        PilotStand = 6,
    }

    /// <summary>
    /// <b>The anchor contract (ADR 0022 phase 1).</b> "Where is the hauler on the boat I am drawing, right
    /// now?" — asked in a way that both hull kinds can answer, because they answer it very differently:
    ///
    /// <list type="bullet">
    ///   <item><b>Sprite hull</b> — the anchor is a fixed boat-local point projected through the rig camera
    ///   for the drawn CELL (<see cref="MountedRockPoseMath.Project"/>). The baker also writes the same
    ///   points out per cell as <c>*Anchors.json</c>, so a table-backed implementation is a legal drop-in
    ///   for the same interface (see <see cref="SpriteHullAnchors"/> remarks — the runtime does not read
    ///   that JSON today).</item>
    ///   <item><b>Mesh hull</b> — the anchor is the same rig point pushed through the hull's live object
    ///   transform. Continuous, and free.</item>
    /// </list>
    ///
    /// <para><b>Result frame:</b> screen-metre offsets from the hull's cell PIVOT, +X right / +Y up — the
    /// frame <see cref="MountedRockPoseMath.Project"/> already returns and every overlay already consumes,
    /// so an anchor can be assigned straight to a child's <c>localPosition</c>. NOT pixels, and not
    /// boat-local metres.</para>
    ///
    /// <para><b>Allocation-free by construction (rule 7):</b> the caller owns the list and the callee only
    /// ever appends. These are read every LateUpdate by things that must not churn the heap.</para>
    /// </summary>
    public interface IBoatHullAnchors
    {
        /// <summary>True when this hull defines <paramref name="id"/> at all. Cheap; no projection.</summary>
        bool Has(BoatAnchorId id);

        /// <summary>
        /// Append the world-screen offsets (metres from the cell pivot) of every point of
        /// <paramref name="id"/>, for the pose currently presented, to <paramref name="into"/>.
        ///
        /// <para>Returns true when the anchor is defined and at least one point was appended. Returns false
        /// and <b>leaves <paramref name="into"/> exactly as it was</b> when the anchor is undefined or
        /// <paramref name="into"/> is null — a caller that ignores the bool must not silently consume a
        /// half-written list. Single-point anchors append exactly one element; multi-point anchors
        /// (<see cref="BoatAnchorId.TubMounts"/>, <see cref="BoatAnchorId.NavMounts"/>) append their whole
        /// set in the rig's own order.</para>
        /// </summary>
        bool TryGetPoints(BoatAnchorId id, List<Vector2> into);
    }
}
