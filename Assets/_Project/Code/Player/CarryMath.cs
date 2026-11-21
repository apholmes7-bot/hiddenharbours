using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// Why a pick-up or a set-down did not happen — so the rule that decides is separable from the
    /// component that acts on it, and every refusal can be pinned headless.
    ///
    /// <para>Each value is a COZY refusal (P5), not an error: the press was heard, and the game says why
    /// in the player's language. <see cref="CarryMath.Message"/> is where the words live. Nothing here
    /// throws and nothing here logs — a refusal is an ordinary outcome of pressing a key.</para>
    /// </summary>
    public enum CarryRefusal
    {
        /// <summary>It happened.</summary>
        None = 0,
        /// <summary>The vessel is too big to lift — <c>FuelContainerDef.Carriable</c> is false.</summary>
        NotCarriable,
        /// <summary>Already carrying something. One thing at a time.</summary>
        HandsFull,
        /// <summary>Asked to set down with nothing in hand.</summary>
        NothingCarried,
        /// <summary>Nowhere to stand it — the spot underfoot is not dry ground right now.</summary>
        NoGround,
        /// <summary>A hand already holds one of these. One rod, one pail, one fish — the sharing rule's
        /// "same kind twice" arm (<see cref="HandSlots.MayShare"/>).</summary>
        OneOfThoseAlready,
        /// <summary>It takes BOTH hands (a fish past
        /// <see cref="GameConfig.MaxOneHandFishLengthMeters"/>) and at least one of them is not free.
        /// Deliberately NOT "hands full": you can lift this, just not while holding something.</summary>
        NeedsBothHands,
    }

    /// <summary>
    /// <b>The whole carry rule, as pure functions</b> — what may be picked up, what may be set down, which
    /// baked facing the thing in your hands is drawn at, and which side of you it draws on.
    ///
    /// <para><b>Why this is separate from the components.</b> Same reason
    /// <see cref="InteractResolver"/> is separate from <see cref="InteractVerb"/>: a rule with no scene, no
    /// components and no input device can be proved headless in EditMode, which is the only place an agent
    /// with no editor can prove anything. The components below it (<see cref="CarryHands"/>,
    /// <see cref="CarriableFuelContainer"/>) hold state and touch transforms; this holds neither and does
    /// not.</para>
    ///
    /// <para><b>Nothing here is new maths.</b> The facing derivation COMPOSES two Core helpers that already
    /// exist — <see cref="BoatKinematics.BearingDegrees"/> and <see cref="IsoFacing.HeadingToFacingIndex"/>
    /// — rather than restating an <c>atan2</c> or a bucket-rounding rule. What this file adds is the one
    /// thing those two do not know: <b>which convention the fuel kit was baked in</b>. That is a fact about
    /// an art drop, it is stated once here, and it is named rather than passed as two bare literals at
    /// every call site (rule 6).</para>
    /// </summary>
    public static class CarryMath
    {
        // ---- the fuel kit's bake convention, stated once ------------------------------------------

        /// <summary>
        /// The heading that fuel-kit facing cell 0 depicts: NORTH.
        ///
        /// <para>Straight off the bake — <c>FuelContainerDefBuilder</c> writes the rig's native cell index
        /// into <c>FuelContainerDef.Frames</c> untouched (row-major, <c>fill × facings + facing</c>), so a
        /// Def's facing index IS the rig's cell index and the rig's own convention is the one that
        /// applies.</para>
        /// </summary>
        public const float FuelKitZeroHeadingDegrees = 0f;

        /// <summary>
        /// The fuel kit bakes <b>CLOCKWISE</b> — cell <c>i</c> depicts heading <c>+step·i</c>.
        ///
        /// <para><b>This is the unusual case and it is deliberate, so do not "fix" it to match the
        /// boats.</b> Nineteen of the twenty-one directional rigs bake counter-clockwise while labelling
        /// themselves clockwise (see <see cref="IsoFacing.HeadingToFacingIndex"/>'s own note, and
        /// <c>BoatVisualDef.FacingsAreCounterClockwise</c>). The fuel kit genuinely is clockwise: it is
        /// registered <c>AzimuthConvention.Clockwise</c> in <c>RigCatalog.FuelKit</c> on a MEASUREMENT — the
        /// un-squashed ground-plane bearing of +X, −45.000°/dir against a counter-clockwise reference's
        /// +45.000°/dir — and <c>FuelRegistrationProbe</c> re-measures it at every bake and refuses the bake
        /// on a disagreement. So this constant cannot silently rot: the probe is what holds it, not this
        /// comment.</para>
        /// </summary>
        public const bool FuelKitFacingsAreCounterClockwise = false;

        // ---- facing: DERIVED from the body, never tracked ----------------------------------------

        /// <summary>
        /// Which baked facing cell a carried container is drawn at, given the heading the CARRIER is drawn
        /// at.
        ///
        /// <para><b>Derived, never independently tracked</b> — the rider-orientation rule (owner playtest
        /// 2026-08-07, and the reason <see cref="DeckRiderFacingMath"/> exists). A carried object has no
        /// heading of its own to lose reference to: it is held, so it points where the holder points. The
        /// moment it kept its own copy of a facing, that copy could drift from the drawn body and no test
        /// would notice, which is precisely the failure the deck rider was rebuilt to close.</para>
        ///
        /// <para>The heading passed in must be the body's <b>DRAWN</b> heading
        /// (<see cref="IsoCharacterSprite.HeadingDegrees"/>), not a velocity or an input direction: on
        /// snap-directional art the picture lags the body by up to half a facing step, and matching the
        /// picture is the whole point. <paramref name="facings"/> ≤ 0 (an unbuilt Def) returns 0, which is
        /// the same null-safe answer <c>FuelContainerDef.Frame</c> gives.</para>
        /// </summary>
        public static int BakedFacingIndex(float drawnHeadingDegrees, int facings)
            => facings <= 0
                ? 0
                : IsoFacing.HeadingToFacingIndex(drawnHeadingDegrees, facings, FuelKitZeroHeadingDegrees,
                                                 FuelKitFacingsAreCounterClockwise);

        /// <summary>
        /// The compass heading of a direction vector — the <see cref="BakedFacingIndex"/> input for a
        /// carrier whose facing is only available as a vector (the four-way <c>PlayerWalkController</c>
        /// fallback, when the iso skin is absent).
        ///
        /// <para>Delegates to <see cref="BoatKinematics.BearingDegrees"/> rather than restating
        /// <c>atan2(x, y)</c>, for the reason <see cref="DeckRiderFacingMath.DeckBearing"/> gives about
        /// calling <c>HeadingFor</c>: two copies of "which way is north" are two chances to disagree.</para>
        /// </summary>
        public static float HeadingFor(Vector2 facing) => BoatKinematics.BearingDegrees(facing);

        // ---- the two gates -----------------------------------------------------------------------

        /// <summary>
        /// May this be lifted? <see cref="CarryRefusal.None"/> means yes.
        ///
        /// <para><b>Hands full outranks not-carriable</b>, and the order matters: standing over a bulk tank
        /// with a jerry can already in your hands, "Your hands are full." is the useful sentence — it names
        /// the thing you can do something about. Telling you the ten-thousand-litre tank is too heavy is
        /// true, unhelpful, and you knew.</para>
        ///
        /// <para><see cref="CarryRefusal.NotCarriable"/> is belt-and-braces: a non-carriable container does
        /// not register as a candidate at all (see <see cref="CarriableFuelContainer"/>), so in a correctly
        /// wired scene the verb can never reach one. It is checked anyway because the alternative to a
        /// cheap redundant test is a hole that opens the day someone registers by hand.</para>
        /// </summary>
        public static CarryRefusal CanPickUp(bool carriable, bool handsFull)
        {
            if (handsFull) return CarryRefusal.HandsFull;
            if (!carriable) return CarryRefusal.NotCarriable;
            return CarryRefusal.None;
        }

        /// <summary>
        /// One <see cref="HandSlotRefusal"/> as the carry verb's own refusal, so the two vocabularies meet
        /// in exactly one place and every call site keeps printing <see cref="Message(CarryRefusal)"/>.
        ///
        /// <para><see cref="HandSlotRefusal.NothingToHold"/> maps to
        /// <see cref="CarryRefusal.NotCarriable"/>: from the press's point of view, a null or destroyed
        /// candidate and an unliftable one are the same answer — there is nothing here you can pick
        /// up.</para>
        /// </summary>
        public static CarryRefusal From(HandSlotRefusal refusal) => refusal switch
        {
            HandSlotRefusal.None => CarryRefusal.None,
            HandSlotRefusal.NothingToHold => CarryRefusal.NotCarriable,
            HandSlotRefusal.HandsFull => CarryRefusal.HandsFull,
            HandSlotRefusal.OneOfThoseAtATime => CarryRefusal.OneOfThoseAlready,
            HandSlotRefusal.NeedsBothHands => CarryRefusal.NeedsBothHands,
            _ => CarryRefusal.HandsFull,
        };

        /// <summary>
        /// May what is in your hands be set down here? <see cref="CarryRefusal.None"/> means yes.
        ///
        /// <para><paramref name="groundIsValid"/> is the caller's live read of the spot underfoot —
        /// <see cref="TidalWalkability.IsWalkableNow"/> in the runtime. That read <b>fails open</b>: a
        /// region with no height map or no tide service is not tide-gated, so everywhere is valid and a can
        /// can always be put down. Failing open is right here for the same reason it is right for the
        /// walker — the alternative is a player standing in a perfectly ordinary spot, holding a can, told
        /// "no" by a service that simply is not installed.</para>
        /// </summary>
        public static CarryRefusal CanPlace(bool carrying, bool groundIsValid)
        {
            if (!carrying) return CarryRefusal.NothingCarried;
            if (!groundIsValid) return CarryRefusal.NoGround;
            return CarryRefusal.None;
        }

        // ---- presentation: which side of the body it draws on -------------------------------------

        /// <summary>
        /// How many sorting orders AHEAD of the carrier the carried sprite draws — positive toward the
        /// camera, negative behind the body.
        ///
        /// <para><b>Why this is a signed offset from the body's order and not an order of its own.</b> The
        /// carried sprite has to sit inside the decor band with everything else (ADR 0032) and it must not
        /// carry an authored <c>sortingOrder</c> constant, because a parked constant is exactly what the
        /// band-clamp bug taught us to stop writing. So the carrier's own live band output — which
        /// <c>YSortSprite</c> has already computed from world Y this frame — is the datum, and this is the
        /// only thing added to it. Zero authored orders; one derived sign.</para>
        ///
        /// <para><b>The sign is the body's, not a guess.</b> A held object hangs on the side of the body the
        /// fisher is facing, so when she looks TOWARD the camera (a southward heading) the can is between
        /// her and the lens and draws in front; when she looks AWAY (northward) it is behind her shoulder
        /// and draws behind. Dead abeam (due east or west) the can is beside her and either answer reads
        /// the same — it resolves to IN FRONT rather than to a tie, because a tie sorts by draw order and
        /// draw order is not stable, which is a flicker with no owner.</para>
        /// </summary>
        /// <param name="drawnHeadingDegrees">The carrier's drawn compass heading (0 = N, CW).</param>
        /// <param name="aheadOrders">Magnitude of the step, in sorting orders — the caller's tunable.</param>
        public static int AheadOrdersFor(float drawnHeadingDegrees, int aheadOrders)
        {
            // cos(heading) is +1 due north (facing away) and −1 due south (facing the camera).
            //
            // ⚠️ The epsilon is not decoration. At exactly ±90° the true cosine is zero, but 90°·Deg2Rad
            // is not exactly π/2 in float, so Mathf.Cos returns a few times 1e-8 whose SIGN is an artefact
            // of the rounding — a bare "> 0f" would put a can beside the fisher in front or behind
            // depending on which way that lands, which is not a decision anyone made. Anything under the
            // epsilon is abeam, and abeam draws in front.
            bool facingAway = Mathf.Cos(drawnHeadingDegrees * Mathf.Deg2Rad) > AbeamEpsilon;
            return facingAway ? -aheadOrders : aheadOrders;
        }

        // Far above float's error at ±90° (~1e-7) and far below the cosine at one degree off it (~0.017),
        // so it separates "abeam" from any heading a fisher is actually drawn at.
        private const float AbeamEpsilon = 1e-4f;

        /// <summary>
        /// <b>How much nearer the camera a socket on the mesh figure is than a body bone</b>, in world
        /// units: positive = the socket is on the NEAR side of his body, negative = the far side.
        ///
        /// <para>Both points are taken down to the figure's ground plane (z = 0 in its frame) before they
        /// are projected, so only where the hand IS around his body counts, never how high it hangs: in
        /// this elevated view a lower point projects farther from the camera, and a hanging hand is
        /// lower than the hips, which would push every hand toward "far".</para>
        ///
        /// <para><paramref name="facetToWorld"/> is the figure's facet frame
        /// (<c>IsoCharacterFigureRenderer.FacetTransform.localToWorldMatrix</c>), heading, ashore frame
        /// and mirror included; the camera looks down +z, so the smaller world z is the nearer.</para>
        /// </summary>
        public static float TowardCameraMetres(in Matrix4x4 facetToWorld, Vector3 socketFacet, Vector3 bodyFacet)
        {
            Vector3 socket = facetToWorld.MultiplyPoint3x4(new Vector3(socketFacet.x, socketFacet.y, 0f));
            Vector3 body = facetToWorld.MultiplyPoint3x4(new Vector3(bodyFacet.x, bodyFacet.y, 0f));
            return body.z - socket.z;
        }

        /// <summary>
        /// <b>How far a thing hung from a hand swings, and which way</b>: back from the way the hand is moving
        /// (a pail lags its bail), <paramref name="degPerMetrePerSecond"/> degrees per metre per second of the
        /// hand's horizontal speed, at most <paramref name="maxDeg"/>. A turn about the grip in the figure's
        /// frame (+z up); the identity when the hand is still or either number is zero. Pure, no allocation.
        /// </summary>
        public static Quaternion HungSwing(Vector3 handVelocity, float degPerMetrePerSecond, float maxDeg)
        {
            var flat = new Vector3(handVelocity.x, handVelocity.y, 0f);
            float speed = flat.magnitude;
            if (speed < 1e-5f || degPerMetrePerSecond <= 0f || maxDeg <= 0f) return Quaternion.identity;
            float deg = Mathf.Min(maxDeg, degPerMetrePerSecond * speed);
            // About up x motion: the hanging end (−z) moves against the motion.
            Vector3 axis = Vector3.Cross(Vector3.forward, flat / speed);
            return Quaternion.AngleAxis(deg, axis);
        }

        /// <summary>
        /// <b>How fast a socket moves against his body on one frame of a clip</b>, metres per second in the
        /// figure's frame: the socket's place on the frames either side (wrapped on a looping clip, held at
        /// the ends of one that is not) over the time between them. Read off the clip itself, so the same
        /// frame always gives the same answer and nothing is carried between frames. <paramref name="world"/>
        /// is a caller's buffer of at least the bone count. False when the clip cannot be read.
        /// </summary>
        public static bool TryClipSocketVelocity(in CharacterSkinDef.SkinClip clip, CharacterSkinDef.Bone[] bones,
                                                 int bone, int frame, Matrix4x4[] world, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            int n = clip.FrameCount;
            if (bones == null || world == null || world.Length < bones.Length || bone < 0 || bone >= bones.Length
                || n < 2 || clip.FramesPerSecond <= 0f)
                return false;
            int f = Mathf.Clamp(frame, 0, n - 1);
            int before = clip.Loop ? (f - 1 + n) % n : Mathf.Max(0, f - 1);
            int after = clip.Loop ? (f + 1) % n : Mathf.Min(n - 1, f + 1);
            int steps = clip.Loop ? 2 : after - before;
            if (steps <= 0) return true;
            CharacterSkinPose.ComposeWorld(clip, before, bones, world);
            Vector3 a = world[bone].MultiplyPoint3x4(Vector3.zero);
            CharacterSkinPose.ComposeWorld(clip, after, bones, world);
            Vector3 b = world[bone].MultiplyPoint3x4(Vector3.zero);
            velocity = (b - a) * (clip.FramesPerSecond / steps);
            return true;
        }

        /// <summary>
        /// <b>Which side of the mesh figure a thing hanging from one hand draws on</b>: ahead of him when
        /// the hand is on his near side, behind him when it is on his far side. A pail in the far hand is
        /// hidden by his legs; one in the near hand hides them. Within
        /// <paramref name="deadbandMetres"/> of the body the hand is beside him (he faces straight toward
        /// or away from the camera) and the facing decides, exactly as <see cref="AheadOrdersFor"/> does
        /// for a thing with no hand to measure.
        /// </summary>
        /// <param name="towardCameraMetres"><see cref="TowardCameraMetres"/> for the hand's socket.</param>
        /// <param name="deadbandMetres">The beside-him band, from the carry row (negative reads as 0).</param>
        /// <param name="drawnHeadingDegrees">The body's drawn compass heading, for the beside-him case.</param>
        /// <param name="aheadOrders">Magnitude of the step, in sorting orders.</param>
        public static int SideOrdersFor(float towardCameraMetres, float deadbandMetres, float drawnHeadingDegrees,
                                        int aheadOrders)
        {
            float band = Mathf.Max(0f, deadbandMetres);
            if (towardCameraMetres > band) return aheadOrders;
            if (towardCameraMetres < -band) return -aheadOrders;
            return AheadOrdersFor(drawnHeadingDegrees, aheadOrders);
        }

        // ---- the words ---------------------------------------------------------------------------

        /// <summary>
        /// What the game SAYS when it refuses — one sentence, in the player's language, no HUD.
        ///
        /// <para>Greybox copy on the <see cref="DevNotice"/> channel, in the register the trap loop and the
        /// wet bucket already use ("Nothing in the bucket to keep."). It lives beside the enum so a test can
        /// assert that every refusal has something to say, rather than discovering an empty toast in play.
        /// <see cref="CarryRefusal.None"/> has no message because nothing was refused.</para>
        /// </summary>
        public static string Message(CarryRefusal refusal) => refusal switch
        {
            CarryRefusal.NotCarriable => "That's more than you can lift.",
            CarryRefusal.HandsFull => "Your hands are full.",
            CarryRefusal.NothingCarried => "You're not carrying anything.",
            CarryRefusal.NoGround => "Nowhere dry to set it down.",
            CarryRefusal.OneOfThoseAlready => "You've got one of those already.",
            CarryRefusal.NeedsBothHands => "That one needs both hands.",
            _ => null,
        };
    }
}
