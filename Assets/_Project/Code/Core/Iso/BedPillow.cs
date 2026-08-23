using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which end of a bed the pillow is on.</b> A DECLARATION, in the same family as a house's door
    /// anchor and a boat's threshold side: something content STATES about a piece of furniture, never
    /// something the game works out by looking at the picture.
    ///
    /// <para><b>Why it cannot be inferred.</b> The kit's bed is symmetric to every measurement the game
    /// holds — <c>propFootprintWidth</c> × <c>propFootprintDepth</c> is a 1.50 × 2.05 m rectangle at
    /// both ends. What makes one end the head is a tall headboard, a short footboard and a pillow, and
    /// all three are PIXELS: reading them back would mean sampling a sprite the room is drawn from at
    /// eight facings, in eight different silhouettes, against a quilt of the same fabric. The rig knows
    /// the answer for free (<c>interiorPropRig.js</c> draws the pillow at the <c>−Y</c> end and says so
    /// in its own comment), so the answer is carried, not recovered. This is the same line
    /// <c>VillageBuildingKit.DrawsDoorOnGable</c> holds for a doorway.</para>
    ///
    /// <para><b>The frame is whoever is asking.</b> A side is meaningless without one: the interior-prop
    /// def states the pillow in the PROP's own model frame (what the rig drew), and a placed bed states
    /// it in the ROOM's (what the owner reads off the room picture). <see cref="BedPillow.Turned"/> is
    /// the one conversion between them, and the content test is what stops the two ever disagreeing.</para>
    ///
    /// <para><b><see cref="Undeclared"/> is the trip-wire, and it is deliberately the default.</b> A bed
    /// that never says which way round it is is a bed the sleeper will lie the wrong way on — which is
    /// the exact defect this type exists to end, and which draws perfectly while being wrong. So it is
    /// zero: a new bed row that forgets to say fails content validation rather than shipping a sleeper
    /// with their feet on the pillow.</para>
    /// </summary>
    public enum PillowSide
    {
        /// <summary>Nobody has said. Fails content validation for a bed; at runtime a caller keeps
        /// whatever fallback it already had rather than guessing (see <see cref="BedPillow"/>).</summary>
        Undeclared = 0,

        /// <summary>The pillow is at the <c>−y</c> end. The kit's own bed, whose rig comment reads
        /// "pillow (head end, −Y front)" and which puts the tall headboard on the same wall.</summary>
        MinusY = 1,

        /// <summary>The pillow is at the <c>+y</c> end.</summary>
        PlusY = 2,

        /// <summary>The pillow is at the <c>−x</c> end.</summary>
        MinusX = 3,

        /// <summary>The pillow is at the <c>+x</c> end.</summary>
        PlusX = 4,
    }

    /// <summary>
    /// <b>The head-to-pillow maths</b> — one declared side plus the frame the bed stands in, turned into
    /// the two things a sleeping picture needs: the compass heading the sleeper lies along, and where
    /// their head ends up.
    ///
    /// <para><b>Pure, static, allocation-free.</b> No engine state, no <c>Time</c>, no scene — the whole
    /// rule is EditMode-testable headless, which it has to be, because every way it can be wrong draws
    /// perfectly. A sleeper laid out along the wrong axis is a picture, not an exception.</para>
    ///
    /// <para><b>⭐ HEADING MEANS THE DIRECTION THE HEAD POINTS.</b> The rig bakes <c>sleep</c> lying, the
    /// same attitude as <c>swim</c> — whose own contract says it in one line: "PRONE: the body lies along
    /// its heading", and whose heading <c>PlayerSwimAnimator</c> feeds straight from the swimmer's
    /// direction of TRAVEL, which a person swims head-first. So the sleeper's heading IS the
    /// head-to-pillow bearing, and the pose is finished the moment that bearing is right.</para>
    ///
    /// <para><b>The corroboration, and what to change if the owner's eye disagrees.</b> The pose shipped
    /// with one hard-coded 180°, and 180° is exactly what this returns for the kit bed (head end
    /// <c>−Y</c>) in an UNTURNED room — so at facing 0 the picture is unchanged, and this is a
    /// generalisation of the shipped behaviour rather than a re-aiming of it. If a play-through ever
    /// shows the fisher reversed <i>in an unturned room</i>, the rig's rows are feet-along-heading and
    /// the single fix is to add 180° in <see cref="TryHeadingDegrees"/> — one place, because nothing
    /// else in this file knows which end of the body the bearing means.</para>
    ///
    /// <para><b>⚠ Bearings here are GROUND bearings, not screen ones</b> (0° = north/<c>+y</c>, growing
    /// CLOCKWISE — ADR 0034, and <see cref="IsoGround"/> is the definition it left behind). The world's depth axis is foreshortened by
    /// the ¾ camera, so a direction taken off world XY is out by up to 12.5° and lands in the neighbouring
    /// facing row for about a fifth of the compass. This resolves the direction on the GROUND, where the
    /// bed's own yaw lives, and only squashes at the last step — in <see cref="HeadOffsetWorld"/>, whose
    /// answer is a world offset and says so.</para>
    /// </summary>
    public static class BedPillow
    {
        /// <summary>The four sides in CLOCKWISE ground order — the order a quarter-turn walks. Kept as
        /// one array so <see cref="Turned"/> is a rotation rather than a switch nobody can check.</summary>
        static readonly PillowSide[] Clockwise =
        {
            PillowSide.MinusY, PillowSide.MinusX, PillowSide.PlusY, PillowSide.PlusX,
        };

        /// <summary>Has anyone said which end the pillow is on? The single test — no caller writes
        /// <c>!= PillowSide.Undeclared</c> itself, so "declared" can never come to mean two things.</summary>
        public static bool IsDeclared(PillowSide side) => side != PillowSide.Undeclared;

        /// <summary>
        /// The unit vector from the middle of the bed toward the pillow, in the frame the side was
        /// declared in. <see cref="Vector2.zero"/> for <see cref="PillowSide.Undeclared"/> — a caller
        /// that acts on a zero direction has skipped <see cref="IsDeclared"/>, and a zero is the one
        /// answer that cannot be mistaken for a real heading.
        /// </summary>
        public static Vector2 Direction(PillowSide side) => side switch
        {
            PillowSide.MinusY => new Vector2(0f, -1f),
            PillowSide.PlusY => new Vector2(0f, 1f),
            PillowSide.MinusX => new Vector2(-1f, 0f),
            PillowSide.PlusX => new Vector2(1f, 0f),
            _ => Vector2.zero,
        };

        /// <summary>
        /// The same side seen from a frame turned <paramref name="quarterTurns"/> quarter-turns CLOCKWISE
        /// on the ground — the one conversion between the PROP's frame (what the rig drew) and the ROOM's
        /// (what a placement declares). Negative and over-large counts wrap.
        ///
        /// <para>A quarter-turn is TWO facings on this project's 8-facing kits, and that is the caller's
        /// arithmetic, not this function's: an ODD facing offset stands a bed on the diagonal, where no
        /// cardinal side is the true answer and content validation refuses it rather than rounding.</para>
        /// </summary>
        public static PillowSide Turned(PillowSide side, int quarterTurns)
        {
            if (!IsDeclared(side)) return PillowSide.Undeclared;

            int at = System.Array.IndexOf(Clockwise, side);
            if (at < 0) return PillowSide.Undeclared;

            int turned = ((at + quarterTurns) % Clockwise.Length + Clockwise.Length) % Clockwise.Length;
            return Clockwise[turned];
        }

        /// <summary>
        /// The ground yaw (degrees) of a room or prop showing facing <paramref name="facing"/> out of
        /// <paramref name="facings"/>. Negative because of how the rigs turn: cell <c>i</c> renders the
        /// model rotated <c>−45°·i</c> under a fixed camera, so walking the cells turns the thing
        /// CLOCKWISE on the ground. The same definition as <c>InteriorFootprint.YawDegrees</c>, restated
        /// here only because World is not a dependency of Core — the two are pinned by a test.
        /// </summary>
        public static float YawDegreesFor(int facing, int facings) =>
            facings <= 0 ? 0f : -360f * facing / facings;

        /// <summary>
        /// The declared direction turned into a GROUND direction by the bed's own yaw. Unit length, or
        /// <see cref="Vector2.zero"/> when nothing was declared.
        /// </summary>
        public static Vector2 GroundDirection(PillowSide side, float yawDegrees)
        {
            Vector2 model = Direction(side);
            if (model == Vector2.zero) return Vector2.zero;

            float rad = yawDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            // Rotate on the GROUND first; the squash is somebody else's last step. Same order, and the
            // same reason, as InteriorFootprint.ModelToWorld — the other order shears the wrong way and
            // is invisible at the orthogonal facings, which are the only ones anyone eyeballs.
            return new Vector2(model.x * c - model.y * s, model.x * s + model.y * c);
        }

        /// <summary>
        /// <b>The heading the sleeper lies along, head first</b> — the answer the sleep pose plays.
        /// Compass degrees, 0 = north, clockwise.
        ///
        /// <para>Returns false for an undeclared side and leaves <paramref name="headingDegrees"/> at 0,
        /// so a caller keeps its own fallback rather than being handed a plausible number. That refusal
        /// is the point: a bed nobody has declared is exactly the bed a guess gets wrong.</para>
        /// </summary>
        public static bool TryHeadingDegrees(PillowSide side, float yawDegrees, out float headingDegrees)
        {
            headingDegrees = 0f;

            Vector2 ground = GroundDirection(side, yawDegrees);
            if (ground == Vector2.zero) return false;

            // Atan2(x, y): 0 = +Y (north), growing clockwise toward +X (east) — IsoGround's convention,
            // applied to a direction that is already on the ground, so there is nothing to un-squash.
            headingDegrees = Mathf.Atan2(ground.x, ground.y) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>
        /// Where the sleeper's head ends up relative to the middle of the bed, in WORLD units —
        /// <paramref name="pillowReachMetres"/> of ground travel toward the pillow, with the depth axis
        /// foreshortened by <paramref name="depthScale"/> on the way out because the world's y axis is.
        ///
        /// <para><see cref="Vector2.zero"/> for an undeclared side, which lands the head in the middle of
        /// the bed — visibly not on a pillow, and the honest picture of "nobody said".</para>
        /// </summary>
        public static Vector2 HeadOffsetWorld(PillowSide side, float yawDegrees,
                                              float pillowReachMetres, float depthScale)
        {
            Vector2 ground = GroundDirection(side, yawDegrees);
            if (ground == Vector2.zero) return Vector2.zero;

            float reach = Mathf.Max(0f, pillowReachMetres);
            return new Vector2(ground.x * reach, ground.y * reach * depthScale);
        }

        /// <summary>Where the sleeper's head ends up, in world units, for a bed whose middle is at
        /// <paramref name="bedWorld"/>. The one call a test or a gizmo wants.</summary>
        public static Vector2 HeadWorld(Vector2 bedWorld, PillowSide side, float yawDegrees,
                                        float pillowReachMetres, float depthScale) =>
            bedWorld + HeadOffsetWorld(side, yawDegrees, pillowReachMetres, depthScale);

        /// <summary>
        /// How far the pillow's centre is from the middle of a bed <paramref name="depthMetres"/> long
        /// whose pillow sits <paramref name="pillowInsetMetres"/> in from the head end — the reach the
        /// two calls above take.
        ///
        /// <para>Derived rather than authored, and that is the point (rule 6): the depth is the BAKE's own
        /// <c>propFootprintDepth</c> and the inset is the RIG's own number, so a re-bake at another size
        /// moves the pillow with the bed instead of leaving a tuned constant pointing at where it used to
        /// be. Clamped at 0 so a nonsense pair puts the head in the middle rather than off the foot.</para>
        /// </summary>
        public static float PillowReachMetres(float depthMetres, float pillowInsetMetres) =>
            Mathf.Max(0f, depthMetres * 0.5f - Mathf.Max(0f, pillowInsetMetres));
    }

    /// <summary>
    /// <b>A bed's pillow, as one travelling fact</b> — the declared side plus the frame it has to be read
    /// in. The runtime companion of <see cref="PillowSide"/>, and what a bed hands to anything that has
    /// to lay a person on it.
    ///
    /// <para><b>Why the frame travels with the side.</b> "The pillow is at <c>−y</c>" is not a direction
    /// until somebody says which <c>−y</c>: a room drawn at facing 3 has its own <c>−y</c> pointing
    /// somewhere quite different from a room at facing 0, and the two rooms are the SAME building on two
    /// builder runs. This is the same lesson <see cref="RestAnchor"/> already carries for a position — a
    /// bare (x, y) is meaningless outside the region and storey it was taken in — applied to a direction.
    /// Passing the side alone and letting each consumer find the yaw for itself is how one bed comes to
    /// have two orientations.</para>
    ///
    /// <para><b><see cref="None"/> is the default value on purpose.</b> A bed nobody has declared, a beat
    /// published by an old caller, a <c>default</c> struct: all three answer "undeclared", and every
    /// consumer keeps the fallback it already had. There is no null to guard and no plausible-looking
    /// wrong heading to unpick later.</para>
    /// </summary>
    public readonly struct PillowAnchor
    {
        /// <summary>Which end of the bed the pillow is on, in the frame <see cref="BedYawDegrees"/>
        /// names. <see cref="PillowSide.Undeclared"/> for <see cref="None"/>.</summary>
        public readonly PillowSide Side;

        /// <summary>The ground yaw (degrees) of the frame <see cref="Side"/> was declared in — for a
        /// placed bed, the ROOM's own yaw, because a placement declares what the owner reads off the room
        /// picture.</summary>
        public readonly float BedYawDegrees;

        /// <summary>How far the pillow's centre is from the middle of the bed, ground metres. Comes from
        /// <see cref="BedPillow.PillowReachMetres"/> — the bake's depth and the rig's inset — never from a
        /// number typed into a placement.</summary>
        public readonly float ReachMetres;

        /// <summary>How far up the screen one metre of northward GROUND travel draws. Non-positive means
        /// "use the shared bake camera's own term" (see <see cref="EffectiveDepthScale"/>), so a caller
        /// with no footprint in hand still gets a head on the pillow rather than one flattened onto the
        /// bed's own line.</summary>
        public readonly float DepthScale;

        public PillowAnchor(PillowSide side, float bedYawDegrees, float reachMetres, float depthScale)
        {
            Side = side;
            BedYawDegrees = bedYawDegrees;
            ReachMetres = reachMetres;
            DepthScale = depthScale;
        }

        /// <summary>"Nobody has said which way round this bed is." The default value, deliberately — so a
        /// <c>default(PillowAnchor)</c> and a bed that was never declared agree without anyone having to
        /// remember to construct this. The same courtesy <see cref="RestAnchor.None"/> extends.</summary>
        public static PillowAnchor None => default;

        /// <summary>Is there a side here to honour? The one test, so a consumer never spells it out
        /// itself.</summary>
        public bool IsDeclared => BedPillow.IsDeclared(Side);

        /// <summary>The squash actually used: this anchor's own, or the shared bake camera's when none was
        /// supplied. Never zero, so the head can never collapse onto the bed's own east–west line.</summary>
        public float EffectiveDepthScale => DepthScale > 0f ? DepthScale : IsoGround.GroundDepthScale;

        /// <summary>The compass heading (0 = north, clockwise) the sleeper lies along, head first. False
        /// for an undeclared bed — see <see cref="BedPillow.TryHeadingDegrees"/> for why that is a refusal
        /// and not a default.</summary>
        public bool TryHeadingDegrees(out float headingDegrees) =>
            BedPillow.TryHeadingDegrees(Side, BedYawDegrees, out headingDegrees);

        /// <summary>Where the head sits relative to the middle of the bed, world units.</summary>
        public Vector2 HeadOffsetWorld =>
            BedPillow.HeadOffsetWorld(Side, BedYawDegrees, ReachMetres, EffectiveDepthScale);

        /// <summary>Where the head sits for a bed whose middle is at <paramref name="bedWorld"/>, world
        /// units. Equal to <paramref name="bedWorld"/> itself when nothing was declared.</summary>
        public Vector2 HeadWorld(Vector2 bedWorld) => bedWorld + HeadOffsetWorld;

        public override string ToString() =>
            IsDeclared
                ? $"pillow {Side} at yaw {BedYawDegrees:0.#}deg, {ReachMetres:0.##} m out"
                : "(no pillow declared)";
    }
}
