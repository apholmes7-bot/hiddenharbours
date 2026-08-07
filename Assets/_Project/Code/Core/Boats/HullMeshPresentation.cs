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
        /// <b>Draw a person standing on this hull, INSIDE her own image</b> (ADR 0022 phase 3's deck
        /// contract; owner playtest 2026-08-07: "sprites visible THROUGH closed cabins").
        ///
        /// <para>Deliberately not a sorting request. A mesh hull composes through ONE overlay quad at
        /// one order, so a crew sprite sorted against her is wholly in front of the boat or wholly
        /// behind her — and behind her is invisible, since the sole they stand on is hull pixels too.
        /// The drawer instead composites the figure into the hull's own off-screen recording, where
        /// the private z-buffer settles it per pixel and the wheelhouse covers the fisher for the
        /// same reason it covers the far gunwale.</para>
        ///
        /// <para>Call every frame while someone is aboard; cheap and idempotent. Returns FALSE when
        /// this drawer cannot present a figure at all — the caller then draws them in-scene exactly
        /// as it always did, which is what keeps a hull with no depth buffer (a sprite compass, an
        /// unconfigured rig) behaving as it does today. Absence is data, not an error.</para>
        /// </summary>
        bool SetDeckOccupant(in HullDeckOccupantPose pose);

        /// <summary>Nobody is aboard any more — stop drawing a figure. Idempotent, and safe to call
        /// on a drawer that never drew one.</summary>
        void ClearDeckOccupant();
    }

    /// <summary>
    /// <b>A person standing on a mesh hull, as the hull's drawer needs them</b> — the payload of
    /// <see cref="IHullMeshRenderer.SetDeckOccupant"/>.
    ///
    /// <para>Everything here is a FINISHED presentation fact, because the character's look has exactly
    /// one owner (<c>IsoCharacterSprite</c> picks the cell, the deck rider leans it) and this seam
    /// must not become a second one. The drawer places what it is handed and decides nothing.</para>
    ///
    /// <para><b>Depth is the whole reason this struct exists.</b> Screen position cannot say whether
    /// the wheelhouse is between the camera and the fisher: under a ¾ bake screen height is
    /// <c>alongView·sin(elev) + height·cos(elev)</c> while distance is
    /// <c>alongView·cos(elev) − height·sin(elev)</c>, two independent combinations of one pair. So the
    /// caller states the boots' depth in the HULL's frame and the drawer adds its own frame offset.</para>
    /// </summary>
    public readonly struct HullDeckOccupantPose
    {
        /// <summary>The character cell to draw. Null means nobody is drawn.</summary>
        public readonly Sprite Sprite;

        /// <summary>Where the boots meet the deck, in world metres — the same screen position the
        /// in-scene sprite would have used, so nothing about WHERE the figure appears changes.</summary>
        public readonly Vector2 WorldFeet;

        /// <summary>The boots' depth in the hull's own iso frame, metres, BEFORE the hull's frame
        /// offset (<c>DeckAreaMath.DeckDepth</c>). Larger = further from the camera.</summary>
        public readonly float HullFrameDepth;

        /// <summary>How much nearer the camera one metre UP THE SCREEN is — <c>tan(elev)</c>. A
        /// standing body leans into the scene, so a wheelhouse that clears the boots may still cross
        /// the head; 0 draws the figure as a flat card at one distance.</summary>
        public readonly float DepthPerScreenRise;

        /// <summary>The figure's lean, degrees about the boots — the deck ride's roll.</summary>
        public readonly float RollDegrees;

        /// <summary>Mirror the sheet horizontally, as a <c>SpriteRenderer.flipX</c> would.</summary>
        public readonly bool FlipX;

        /// <summary>Colour multiplier, as a <c>SpriteRenderer.color</c> would be.</summary>
        public readonly Color Tint;

        public HullDeckOccupantPose(Sprite sprite, Vector2 worldFeet, float hullFrameDepth,
                                    float depthPerScreenRise, float rollDegrees, bool flipX, Color tint)
        {
            Sprite = sprite;
            WorldFeet = worldFeet;
            HullFrameDepth = hullFrameDepth;
            DepthPerScreenRise = depthPerScreenRise;
            RollDegrees = rollDegrees;
            FlipX = flipX;
            Tint = tint;
        }
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
        /// </summary>
        IHullMeshRenderer Install(GameObject host, HullMeshDef def);

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
