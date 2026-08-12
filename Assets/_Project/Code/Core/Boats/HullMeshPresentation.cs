using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The Core seam between posing a mesh hull and drawing one (ADR 0022 phase 4).</b> Boats
    /// decides WHERE the hull points and how it rocks; Art owns the facet URP pass that draws it —
    /// and rule 4 forbids either referencing the other's concrete classes. This interface is the
    /// hull-drawer as Boats is allowed to see it: four pose channels in RIG units, a configured
    /// check, and sorting.
    ///
    /// <para>Implemented by Art's <c>IsoFacetHullRenderer</c>; installed through
    /// <see cref="HullMeshPresentation.Service"/>. All pose setters are cheap and idempotent (the
    /// renderer dirty-checks), safe to write every LateUpdate with no allocation (rule 7).</para>
    /// </summary>
    public interface IHullMeshRenderer
    {
        /// <summary>Heading in RIG dir units (1 = 45°, fractional allowed — continuous is the point).
        /// Map a compass heading through <see cref="HullMeshMath.HeadingToDirUnits"/>.</summary>
        float HeadingDirUnits { get; set; }

        /// <summary>Roll in degrees, the rig's own convention (+ = the rig's rockMotion roll).</summary>
        float RollDegrees { get; set; }

        /// <summary>Pitch in degrees, the rig's own convention.</summary>
        float PitchDegrees { get; set; }

        /// <summary>Heave in rig PIXELS (world metres = px / <see cref="HullMeshDef.PxPerMetre"/>).
        /// The TOTAL screen lift — the rig's own rock heave plus the displaced ride below.</summary>
        float HeavePixels { get; set; }

        /// <summary>
        /// How much of <see cref="HeavePixels"/> is WORLD RIDE rather than the rig's own rock: the
        /// metre-scale displaced-sea lift (ADR 0023 phase 3 step 2), in the same rig pixels.
        ///
        /// <para><b>The two look alike and are not.</b> The rig's rock heave is an ANIMATION INSIDE
        /// THE BAKED CELL — the rig subtracts it from screen y after projecting, so it clips at the
        /// cell edge and the cell was authored with margin for it (1.0–1.6 px across the fleet). The
        /// displaced ride is the whole BOAT moving through the world, and it arrives in metres ×
        /// PxPerMetre, 20–100× that budget. A mesh hull's in-scene face is a fixed cell-sized
        /// compositing window, so the drawer needs to know which is which: the rock must stay inside
        /// the window (that is the golden master against the rig's own sheets), and the window must
        /// travel with the ride, or the hull's image slides out of a window that stayed behind and
        /// the sea shows through the gap.</para>
        ///
        /// <para>0 — the default, and always while the flat sea is up — leaves the render
        /// byte-identical to before this channel existed (the A/B contract).</para>
        /// </summary>
        float RidePixels { get; set; }

        /// <summary>True when a hull setup is loaded and the renderer can draw.</summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Where this hull sorts against the scene's sprites — whole-object, exactly as a baked
        /// sprite would (ADR 0022 "Unchanged"). Sets the SortingGroup the overlay quad sorts under.
        /// </summary>
        void SetSorting(int sortingLayerId, int sortingOrder);

        /// <summary>
        /// <b>Somebody is standing on this deck, HERE</b> — their feet, in the hull's own rig metres
        /// (+X starboard, +Y toward the bow, +Z up from the keel: the frame the deck polygons and
        /// every fitting pivot already speak). <paramref name="active"/> false = nobody.
        ///
        /// <para><b>Why the drawer has to know.</b> A figure on deck is a sprite sorted above the
        /// hull's whole-object slot, so a wheelhouse in front of them can never cover them — sorting
        /// is per OBJECT and the question is per PIXEL (owner playtest 2026-08-07: "sprites visible
        /// THROUGH closed cabins"). The hull is the only thing that can answer it, because only her
        /// facet pass holds her depth. Told where the figure stands, she marks the geometry NEARER
        /// the camera than that point with a second id, and the figure's own shader discards there.
        /// So the boat occludes her crew because she is genuinely in front of them, at any heading,
        /// with no authored cabin footprint and no sorting hack.</para>
        ///
        /// <para>Cheap and idempotent, safe to write every LateUpdate with no allocation (rule 7).
        /// Never called = nobody aboard = byte-identical to before this existed.</para>
        /// </summary>
        void SetDeckOccupant(Vector3 rigLocalMeters, bool active);

        /// <summary>
        /// The id an occludable sprite must discard against to be hidden by this hull, already
        /// divided by 255 for the shader — and <b>0 whenever there is nothing to hide behind</b>
        /// (no occupant set, or the hull is not live). One value, so no consumer downstream has to
        /// re-derive the question from two fields.
        ///
        /// <para>⚠️ This and <see cref="SetDeckOccupant"/> are the SINGLE-OCCUPANT shim, kept because
        /// one figure on a deck is the common case and reads better as two calls than as a claim.
        /// They are implemented on top of <see cref="DeckOccupants"/> — a hull carrying more than one
        /// thing (gear, pots, a second hand) must claim its own slots there, and pair this id with
        /// <see cref="IDeckOccupantSlots.OccluderIdTop"/>, because the sprite's discard is a range.</para>
        /// </summary>
        float DeckOccluderId { get; }

        /// <summary>
        /// <b>Everything standing on this deck</b> — the fixed slot array behind the per-pixel
        /// occlusion (<see cref="IDeckOccupantSlots"/>). Never null: a hull that cannot split her own
        /// image hands back <see cref="NoDeckOccupantSlots.Instance"/>, whose every claim is refused
        /// and every id 0, so a caller written against the real thing simply draws un-occluded.
        /// </summary>
        IDeckOccupantSlots DeckOccupants { get; }
    }

    /// <summary>
    /// <b>One articulated fitting on a mesh hull</b> — an oar, an outboard (ADR 0022 phase 7).
    ///
    /// <para><b>The whole contract is a local rotation, and that is the point.</b> An oar sweeps and
    /// dips about its oarlock; an outboard swivels and tilts about its clamp; the twin does both, at
    /// two lateral offsets. None of that means anything to a renderer. Boats already owns the
    /// arithmetic (<c>DoryOarMath</c>, <c>OutboardMotorMath</c>) and converts it to a rotation about
    /// the fitting's <see cref="HullPropMeshDef.PivotLocalMeters"/>; Art applies it. So two fittings
    /// that articulate nothing alike ride one seam, and adding a third (a rudder, a hauler) needs no
    /// new interface — which is exactly what rule 4 is for.</para>
    ///
    /// <para>The fitting inherits the hull's heading, rock and heave by being parented to it — those
    /// are deliberately NOT settable here. A fitting that could be posed independently of its hull is
    /// a fitting that can shear off it, and the sprite path's layers did precisely that whenever a
    /// rock frame and a steer column disagreed.</para>
    /// </summary>
    public interface IHullPropRenderer
    {
        /// <summary>Rotation about the def's pivot, in the hull's local frame. Identity = the pose the
        /// fitting was baked at (dead ahead, untilted; the catch of the stroke).</summary>
        Quaternion LocalRotation { get; set; }

        /// <summary>Lateral clamp offset in METRES along the hull's beam — the twin outboard's ±0.34.
        /// 0 for anything on the centreline.</summary>
        float LateralOffsetMeters { get; set; }

        /// <summary>
        /// <b>The BORROWED-fitting shift</b>, in the hull's local metres: how far to move this whole
        /// fitting — pivot included — to hang it on a boat it was not baked for.
        ///
        /// <para><see cref="Vector3.zero"/>, the default and the case for every fitting on its own
        /// boat, leaves the render byte-identical to before this channel existed. A fitting is baked
        /// in its hull's rig space with that rig's mount already injected
        /// (<see cref="HullPropMeshDef.Mesh"/>), so lending one to a second hull means saying where
        /// that hull's clamp is — and nothing else: the part still swivels about its own pivot after
        /// the shift, so a borrowed engine stays on its bracket at every helm angle.</para>
        ///
        /// <para>Authored per boat as <c>BoatVisualDef.MotorMeshFitmentOffsetMeters</c> and written
        /// once at install, not per frame — it is a fact of the fit, not of the tick.</para>
        /// </summary>
        Vector3 FitmentOffsetMeters { get; set; }

        /// <summary>Drawn or not, without tearing the fitting down: a shipped oar and a tilted-clear
        /// engine are states the boat passes through constantly, and rebuilding a renderer per state
        /// would allocate every time the owner trims his engine (rule 7).</summary>
        bool Visible { get; set; }

        /// <summary>True when a fitting setup is loaded and the renderer can draw.</summary>
        bool IsConfigured { get; }
    }

    /// <summary>Installs / removes the Art-side mesh-hull renderer on a host GameObject.</summary>
    public interface IHullMeshPresentationService
    {
        /// <summary>
        /// Install (or re-configure in place) a mesh-hull renderer on <paramref name="host"/> from a
        /// baked def. Returns null — with a logged reason — when the def is unusable, so the caller
        /// can fall back to the sprite path rather than field an invisible boat.
        ///
        /// <para><paramref name="scheme"/> repaints her: a baked ramp table that stands in for the
        /// def's own, leaving the mesh, the silhouette and the draw call untouched (see
        /// <see cref="HullPaintSchemeDef"/>). <b>Null is the contract, not an omission</b> — no
        /// scheme means the def's own ramps, byte for byte, so an unpainted boat renders exactly as
        /// she did before paint existed. An unusable scheme is REFUSED with a log and the def's ramps
        /// stand; a wrong repaint must never cost the hull.</para>
        ///
        /// <para>Defaulted rather than overloaded so the many callers that have no opinion about
        /// paint keep their two-argument call, and the one that does (the skinner, from the boat's
        /// own visual) passes a third.</para>
        /// </summary>
        IHullMeshRenderer Install(GameObject host, HullMeshDef def, HullPaintSchemeDef scheme = null);

        /// <summary>
        /// Bolt an articulated fitting onto a hull already installed on <paramref name="host"/>.
        /// <paramref name="slot"/> names the instance ("OarPort", "MotorB"), so a re-install
        /// re-configures in place rather than accumulating engines.
        ///
        /// <para>Returns null — with a logged reason — when the def is unusable or no hull is
        /// installed. A refused fitting is the honest outcome: the caller keeps the sprite path,
        /// where the oars at least exist.</para>
        /// </summary>
        IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot);

        /// <summary>Remove every fitting attached to <paramref name="host"/>. Safe when none are.</summary>
        void DetachProps(GameObject host);

        /// <summary>
        /// Remove just the fitting in <paramref name="slot"/>. Safe when none is there.
        ///
        /// <para>Needed because a hull's fittings are decided INDEPENDENTLY — a boat may take her
        /// oars as meshes and wear no engine, or the reverse — so "this hull has no outboard" must
        /// not be able to unbolt her oars on the way past. <see cref="DetachProps"/> stays for the
        /// wholesale clear when a hull leaves the mesh path entirely.</para>
        /// </summary>
        void DetachProp(GameObject host, string slot);

        /// <summary>Remove a previously installed renderer (and everything it owns) from
        /// <paramref name="host"/>. Safe when none is present.</summary>
        void Remove(GameObject host);
    }

    /// <summary>
    /// The service locator for <see cref="IHullMeshPresentationService"/>. Deliberately NOT a
    /// <c>GameServices</c> member: <c>GameServices.Reset()</c> clears game-STATE services between
    /// tests/scenes, and this is stateless presentation wiring that must survive those resets. Art
    /// self-registers at runtime load; EditMode tests and editor tooling register explicitly.
    /// Consumers null-check — a null service means "no mesh path here", and the skinner's sprite
    /// fallback stands.
    /// </summary>
    public static class HullMeshPresentation
    {
        public static IHullMeshPresentationService Service { get; set; }
    }
}
