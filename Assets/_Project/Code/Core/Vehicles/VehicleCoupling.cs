using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A tractor's fifth wheel, as her own sidecar publishes it.</b>
    ///
    /// <para>Every number here is read off <c>TOW.fifth_wheel</c> at bake time. Nothing about
    /// coupling is a tuned radius: the pack draws a slot with a mouth, a depth and a pair of jaws,
    /// and <see cref="VehicleCouplingMath"/> derives the whole capture test from those three.</para>
    /// </summary>
    [Serializable]
    public struct VehicleFifthWheel
    {
        [Tooltip("False on anything that does not tow — every machine but the two semis. A consumer " +
                 "must check it rather than reading a zeroed struct as a plate at the origin.")]
        public bool Published;

        [Tooltip("Where the kingpin seats, in rig metres. The z is the COUPLING PLANE (1.18 on both " +
                 "semis), and the trailers are baked at that same deck height — so a coupled pair " +
                 "needs no z shift at all.")]
        public Vector3 CouplingPointLocal;

        [Tooltip("Half the slot's throat, in metres (0.06). The lateral half of the capture test: a " +
                 "kingpin further off the centreline than this cannot be in the slot.")]
        public float SlotHalfWidthMeters;

        [Tooltip("Where the slot opens (aft, more negative) and where it seats (forward). The " +
                 "longitudinal capture window, and its depth is what the heading tolerance is " +
                 "derived from.")]
        public float SlotMouthY, SlotSeatY;

        [Tooltip("The aft end of the approach ramps. They angle down aft, so a kingpin backed under " +
                 "the nose from here rides UP into the slot — which is why capture begins at the " +
                 "ramp rather than at the slot mouth.")]
        public float RampMouthY;

        [Tooltip("Where the driver stands to pull the release, rig metres (x, y) — street side. " +
                 "⚠️ Coupling has no handle: it is an act of BACKING. Only uncoupling is worked.")]
        public Vector2 ReleaseHandleLocal;

        [Tooltip("Distance from the seated kingpin forward to the nearest thing a swinging trailer " +
                 "could hit — the cab back wall (1.52 m on both). What the jackknife cap is solved " +
                 "against.")]
        public float CabClearanceMeters;
    }

    /// <summary>
    /// <b>A towed body's kingpin and the numbers her follow needs</b>, off <c>KINGPIN</c> in the
    /// trailer sidecar — per body, because one document serves four lengths.
    /// </summary>
    [Serializable]
    public struct VehicleKingpin
    {
        [Tooltip("False on anything that is not towed. Checked, never inferred from zeros.")]
        public bool Published;

        [Tooltip("Where her kingpin sits in her own frame, rig metres. +y is her nose, so this is " +
                 "positive and large — 3.365 on a pup, 7.175 on a 53.")]
        public Vector3 CouplingPointLocal;

        [Tooltip("How far her front corner reaches from the kingpin (1.516 m on every body in the " +
                 "set — sqrt(1.22² + 0.90²)). Against the tractor's cab clearance this decides " +
                 "whether she can jackknife at all.")]
        public float NoseSwingRadiusMeters;

        [Tooltip("Kingpin to the centre of her axle group (6.265 pup / 13.275 fifty-three). The " +
                 "length scale her whole follow is solved on — the trailer's 'wheelbase'.")]
        public float KingpinToAxleCentreMeters;

        [Tooltip("How far her tail sweeps about the kingpin (7.63 / 15.25) — for swept-path checks. " +
                 "Not used by the follow; published so a placement can ask.")]
        public float TailSwingRadiusMeters;

        [Tooltip("Half her width and how far her kingpin sits back from her nose (1.22 and 0.90 on " +
                 "every body in the set) — the two numbers the nose swing is BUILT from, and the " +
                 "two the jackknife cap needs.\n\n" +
                 "⚠️ The radius alone is not enough. Her front corner does not point straight " +
                 "ahead: it sits at atan2(halfWidth, kingpinSet) off her centreline, so its forward " +
                 "reach peaks when the pair is folded to THAT angle rather than when it is straight. " +
                 "A cap solved from the radius alone gets the direction backwards — it loosens as " +
                 "the trailer grows.")]
        public float NoseHalfWidthMeters, KingpinSetMeters;

        [Tooltip("The pin's own radius (0.045) — it is a 90 mm shaft, not a point.\n\n" +
                 "⭐ This is what makes the capture test survive being asked in world space. The " +
                 "slot's reach is a CLOSED range and a fully seated pin sits exactly on its fore " +
                 "end, so a point-containment test there is a coin toss decided by the last bit of " +
                 "a float — which is not a thing a driver can drive.")]
        public float PinRadiusMeters;
    }

    /// <summary>
    /// <b>The coupling, in arithmetic</b> — capture, articulation limit, and how a towed body
    /// follows.
    ///
    /// <para><b>Everything is derived from the art's published geometry.</b> There is no tuned
    /// capture radius and no hand-set jackknife angle: the pack draws a slot with a throat and a
    /// depth, and a trailer with a nose that swings a known distance, and those decide. That is the
    /// whole point — the day the art widens a trailer or moves a plate, the numbers move with it and
    /// nobody has to remember to re-tune anything.</para>
    ///
    /// <para>Pure and deterministic (rule 5): no time, no randomness, no Unity state. Every method
    /// is a function of its arguments, which is what lets the whole coupling be tested in EditMode
    /// without a scene.</para>
    /// </summary>
    public static class VehicleCouplingMath
    {
        /// <summary>
        /// ⭐ <b>How far off the tractor's heading a trailer may sit and still be captured</b>,
        /// degrees — <c>atan(halfWidth / reachDepth)</c>.
        ///
        /// <para><b>Why that is the honest bound.</b> The kingpin enters at the slot's mouth and has
        /// to travel the reach depth to seat. Coming in on the centreline, it may deviate laterally
        /// by at most the throat's half-width over that run before it fouls a jaw — so the widest
        /// admissible approach angle is the one whose tangent is exactly that ratio. On both semis
        /// the slot is 0.06 m by 0.40 m, giving <b>8.53°</b>: tight enough that you have to line the
        /// truck up, loose enough that you are not hunting for a pixel.</para>
        ///
        /// <para>⚠️ Not a feel number and not in config. It is the shape of the slot the art drew,
        /// and if the art redraws it this answer changes on its own.</para>
        /// </summary>
        public static float CaptureHeadingToleranceDegrees(in VehicleFifthWheel wheel)
        {
            float depth = Mathf.Abs(wheel.SlotSeatY - wheel.SlotMouthY);
            if (depth <= 1e-6f || wheel.SlotHalfWidthMeters <= 0f) return 0f;
            return Mathf.Atan2(wheel.SlotHalfWidthMeters, depth) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// ⭐ <b>The articulation cap</b>, degrees — how far the pair may fold before the game holds
        /// it there.
        ///
        /// <para><b>Solved from the corner's real path, not from a radius.</b> The trailer's front
        /// corner sits at <c>α = atan2(halfWidth, kingpinSet)</c> off her centreline — 53.6° on every
        /// body in this set — so as the pair folds by φ the corner's reach along the tractor's axis
        /// is <c>r·cos(φ − α)</c>. That peaks at <b>φ = α</b>, where it equals the full swing radius,
        /// and NOT at φ = 0. So:</para>
        ///
        /// <list type="bullet">
        ///   <item>if the swing radius is smaller than the clearance, the corner can never reach the
        ///   cab at ANY fold, and the cap is the KINEMATIC one: past 90° the trailer lies abeam and a
        ///   tractor can no longer push her anywhere useful;</item>
        ///   <item>otherwise contact begins at <c>α − acos(d/r)</c>, the first fold at which the
        ///   corner arrives at the cab.</item>
        /// </list>
        ///
        /// <para>⚠️ <b>The direction of that second case is the whole point, and it is easy to get
        /// backwards.</b> Written as <c>acos(d/r)</c> alone it GROWS with the radius — a longer nose
        /// would be allowed to fold further, which is exactly wrong. A test caught that here before
        /// it shipped; the α term is what makes a bigger trailer fold less.</para>
        ///
        /// <para>This pack clears at every angle (1.516 against 1.520, by 4 mm) so today it returns
        /// 90 — but it returns it BECAUSE the geometry says so. Widen a trailer and the cap closes on
        /// its own, which is what her sidecar asks for: <i>"the 0.90 m set and the 2.44 m width are
        /// LOAD-BEARING numbers: widen either and the tractors' published margin is gone."</i></para>
        ///
        /// <para>⚠️ <b>A CAP, not a collision.</b> The pair is clamped here; nothing is pushed and
        /// nothing reports a hit. A jackknife you cannot go further into reads as a truck that has
        /// run out of room, which is what it is.</para>
        /// </summary>
        public static float JackknifeCapDegrees(float noseHalfWidth, float kingpinSet,
                                                float cabClearance)
        {
            const float Kinematic = 90f;
            if (noseHalfWidth <= 0f || kingpinSet <= 0f || cabClearance <= 0f) return Kinematic;

            float radius = Mathf.Sqrt(noseHalfWidth * noseHalfWidth + kingpinSet * kingpinSet);
            if (radius < cabClearance) return Kinematic;

            float cornerBearing = Mathf.Atan2(noseHalfWidth, kingpinSet) * Mathf.Rad2Deg;
            float reachAngle = Mathf.Acos(Mathf.Clamp01(cabClearance / radius)) * Mathf.Rad2Deg;
            return Mathf.Clamp(cornerBearing - reachAngle, 0f, Kinematic);
        }

        /// <summary>The cap for a published pair — the same solve, asked of the two structs so a
        /// caller cannot pair the wrong numbers by hand.</summary>
        public static float JackknifeCapDegrees(in VehicleKingpin pin, in VehicleFifthWheel wheel) =>
            JackknifeCapDegrees(pin.NoseHalfWidthMeters, pin.KingpinSetMeters,
                                wheel.CabClearanceMeters);

        /// <summary>
        /// <b>Is this kingpin in the slot?</b> — the whole capture test, in the tractor's own frame.
        /// </summary>
        /// <param name="wheel">the tractor's published plate.</param>
        /// <param name="pin">the trailer's published kingpin — here for its RADIUS, because the
        /// thing being located has a size.</param>
        /// <param name="kingpinLocal">the trailer's kingpin, expressed in the tractor's frame
        /// (metres, +x curb, +y nose).</param>
        /// <param name="headingDeltaDegrees">the trailer's heading less the tractor's, signed.</param>
        /// <remarks>
        /// Three conditions, each from one published number:
        /// <list type="bullet">
        ///   <item>LATERAL — within the throat's half-width of the slot's centreline;</item>
        ///   <item>LONGITUDINAL — between the ramp's aft mouth and the seat. Capture begins at the
        ///   RAMP and not at the slot, because the ramps angle down aft and a pin backed onto them
        ///   rides up into the throat: that is the manoeuvre the art drew;</item>
        ///   <item>HEADING — within <see cref="CaptureHeadingToleranceDegrees"/>, so a trailer sat
        ///   across the yard is not hooked by standing near her.</item>
        /// </list>
        ///
        /// <para>⭐ <b>Both windows carry the pin's own radius, and that is not a fudge factor.</b>
        /// The question is whether there is PIN METAL in the slot, not whether a dimensionless point
        /// is inside a closed interval — and the difference is the whole test at the seat, where a
        /// fully coupled pin sits exactly ON the fore boundary. Asked of a point, "seated" comes out
        /// true or false depending on the last bit of a float: the same trailer captured on one
        /// tractor and not on another whose slot happened to seat 100 mm further aft, which is how
        /// this surfaced — the EditMode fixture asserts the seat is captured and it passed, because
        /// there the number is exact; the PlayMode journey asks the same question after a world
        /// round-trip and the classic tractor said no.</para>
        ///
        /// <para><b>Clear air on both sides, which is the point of deriving it rather than picking
        /// it.</b> The radius is 45 mm: some four orders above the ~1e-7 m the round-trip costs at
        /// yard coordinates, and a fourteenth of the slot's own 0.62 m depth — so it cannot swallow
        /// a miss any driver could make. <c>VehicleCouplingTests</c> pins both ends of that.</para>
        /// </remarks>
        public static bool IsCaptured(in VehicleFifthWheel wheel, in VehicleKingpin pin,
                                      Vector2 kingpinLocal, float headingDeltaDegrees)
        {
            if (!wheel.Published) return false;

            // The pin is a shaft, so it is in the slot when any of it is. A trailer whose kingpin the
            // art never sized falls back to the point test, which is the strict reading.
            float body = Mathf.Max(0f, pin.PinRadiusMeters);

            if (Mathf.Abs(kingpinLocal.x - wheel.CouplingPointLocal.x)
                > wheel.SlotHalfWidthMeters + body)
                return false;

            float aft = Mathf.Min(wheel.RampMouthY, wheel.SlotSeatY) - body;
            float fore = Mathf.Max(wheel.RampMouthY, wheel.SlotSeatY) + body;
            if (kingpinLocal.y < aft || kingpinLocal.y > fore) return false;

            return Mathf.Abs(Mathf.DeltaAngle(0f, headingDeltaDegrees))
                   <= CaptureHeadingToleranceDegrees(wheel);
        }

        /// <summary>
        /// ⭐ <b>How a towed body's heading changes as she is pulled</b> — the one-trailer kinematic
        /// model, in degrees.
        ///
        /// <para>The kingpin is carried by the tractor and the axle group trails it, so the trailer
        /// swings toward the line of travel at a rate set by how far she is folded and how long she
        /// is: <c>dθ/dt = v·sin(φ) / L</c>, with φ the articulation and L the kingpin-to-axle-centre
        /// distance her own sidecar publishes as the follow's input.</para>
        ///
        /// <para><b>Which is why a 53 tracks so differently from a pup</b>, with no special case: at
        /// 13.275 m she straightens less than half as fast as the 6.265 m pup for the same metre
        /// travelled, so she cuts the corner further and her tail sweeps wider. The off-tracking in
        /// the picture is this one line.</para>
        ///
        /// <para>Distance-based rather than time-based on purpose: a trailer's path is a fact about
        /// the ground covered, not about how long it took, so the same manoeuvre driven slowly and
        /// quickly draws the same curve (rule 5).</para>
        /// </summary>
        /// <param name="articulationDegrees">tractor heading less trailer heading, signed.</param>
        /// <param name="distanceMeters">signed distance the coupling travelled; negative astern.</param>
        /// <param name="kingpinToAxleCentre">the trailer's own length scale, metres.</param>
        public static float TrailerYawDeltaDegrees(float articulationDegrees, float distanceMeters,
                                                   float kingpinToAxleCentre)
        {
            if (kingpinToAxleCentre <= 1e-4f) return 0f;
            float radians = distanceMeters * Mathf.Sin(articulationDegrees * Mathf.Deg2Rad)
                            / kingpinToAxleCentre;
            return radians * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Where a coupled trailer's ORIGIN sits, given where her kingpin is and which way she
        /// points. Her kingpin is well forward of her origin (3.365 m on a pup), so a coupled pair
        /// drawn from the pin alone would put her a whole nose too far ahead.
        /// </summary>
        public static Vector2 BodyOriginFromKingpin(Vector2 kingpinWorld, float trailerHeadingDegrees,
                                                    in VehicleKingpin pin)
        {
            float rad = trailerHeadingDegrees * Mathf.Deg2Rad;
            float s = Mathf.Sin(rad), c = Mathf.Cos(rad);
            // The pin sits at (x, y) in her frame; rotate that into the world and step back from it.
            Vector2 offset = new Vector2(
                pin.CouplingPointLocal.x * c - pin.CouplingPointLocal.y * s,
                pin.CouplingPointLocal.x * s + pin.CouplingPointLocal.y * c);
            return kingpinWorld - offset;
        }

        /// <summary>Clamp an articulation to the pair's cap, keeping its sign. The cap is a limit on
        /// how far the pair may fold, so a fold past it is held AT it rather than refused or
        /// bounced.</summary>
        public static float ClampArticulation(float articulationDegrees, float capDegrees) =>
            Mathf.Clamp(Mathf.DeltaAngle(0f, articulationDegrees), -capDegrees, capDegrees);
    }
}
