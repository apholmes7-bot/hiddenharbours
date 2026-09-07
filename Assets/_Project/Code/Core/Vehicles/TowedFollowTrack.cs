using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ⭐⭐ <b>A TRAILER'S WAY DOWN ONE ROAD, SOLVED ONCE AND THEN READ OFF.</b>
    ///
    /// <para>A scheduled trip POSES its machine (<see cref="VehicleTripPlan"/>): at an hour of the day
    /// she is a point on a polyline and a tangent, and nothing is integrated. A trailer cannot be posed
    /// that way — where she lies is not a function of where the tractor IS, it is a function of
    /// everywhere the tractor has BEEN, which is the whole of off-tracking. So the road is walked once
    /// at build time, her heading carried along it through
    /// <see cref="VehicleCouplingMath.FollowStep"/>, and the answers kept.</para>
    ///
    /// <para><b>That keeps rule 5 intact.</b> The walk happens in <see cref="Build"/>, not per frame:
    /// afterwards a sample is two array reads and one rotation, so the plan is still a pure function of
    /// the hour with nothing ticked, nothing accumulated and nothing saved. A save taken mid-trip
    /// re-derives her exactly, because the table re-derives exactly.</para>
    ///
    /// <para>⭐ <b>The same arithmetic the player's tow runs.</b> Every step here is
    /// <see cref="VehicleCouplingMath.FollowStep"/> — the function <c>Vehicles.TowedBody.FollowKingpin</c>
    /// calls, on the same signed-distance semantics (the TRACTOR's travel, which is what her odometer
    /// hands the hitch). There is no second kinematic model in this game, and
    /// <c>TowedFollowTrackTests</c> drives a live <c>TowedBody</c> over this track's own stations and
    /// asks for the same answer to the ULP.</para>
    /// </summary>
    public sealed class TowedFollowTrack
    {
        /// <summary>
        /// How finely the road is walked, as a fraction of the trailer's OWN length scale.
        ///
        /// <para>Not a feel number and not in config: it is the resolution of a numerical integration,
        /// and the honest way to pick one is against the thing being integrated. The follow's
        /// characteristic length is <see cref="VehicleKingpin.KingpinToAxleCentreMeters"/> — a fully
        /// folded trailer swings one radian per that many metres travelled — so a step of L/32 bounds
        /// the yaw per step at about 1.8°, and its truncation error far below anything a 32 px/m
        /// picture can show. <c>TowedFollowTrackTests.HalvingTheStepDoesNotMoveHer</c> measures that
        /// rather than asserting it: over a 145 m road with two bends, refining the step again moves a
        /// 53-footer by two hundredths of a degree — half a centimetre at her tail.</para>
        /// </summary>
        public const int StepsPerLengthScale = 32;

        private readonly Vector2[] _route;
        private readonly int _start, _count;
        private readonly Vector2 _plateLocal;
        private readonly VehicleKingpin _pin;

        /// <summary>Her heading at station <c>i</c>, degrees — station <c>i</c> sitting
        /// <c>i × <see cref="StepMetres"/></c> along the road (the last one clamped to its end).</summary>
        private readonly float[] _headings;

        /// <summary>How far apart the stations are, metres.</summary>
        public float StepMetres { get; }

        /// <summary>The road's walked length, metres.</summary>
        public float LengthMetres { get; }

        /// <summary>How many stations were solved — the table's size, for a fixture that wants to walk
        /// exactly the steps this walked.</summary>
        public int StationCount => _headings.Length;

        /// <summary>Where she is pointing before the tractor moves at all.</summary>
        public float StartHeadingDegrees => _headings[0];

        /// <summary>Where she is pointing when the tractor has finished the road — the heading she is
        /// left standing on, and the input to whatever she does next.</summary>
        public float EndHeadingDegrees => _headings[_headings.Length - 1];

        private TowedFollowTrack(Vector2[] route, int start, int count, Vector2 plateLocal,
                                 in VehicleKingpin pin, float[] headings, float stepMetres,
                                 float lengthMetres)
        {
            _route = route;
            _start = start;
            _count = count;
            _plateLocal = plateLocal;
            _pin = pin;
            _headings = headings;
            StepMetres = stepMetres;
            LengthMetres = lengthMetres;
        }

        /// <summary>
        /// Walk the road once, carrying her heading.
        ///
        /// <para><paramref name="route"/> is kept BY REFERENCE and must not be mutated afterwards — it
        /// is the plan's own flattened waypoint array, which never changes after
        /// <c>ScheduledLegs.Build</c>.</para>
        /// </summary>
        /// <param name="plateLocal">where the fifth wheel sits in the TRACTOR's frame (x curb, y nose).</param>
        /// <param name="pin">the trailer's published kingpin.</param>
        /// <param name="capDegrees">the pair's articulation cap.</param>
        /// <param name="startHeadingDegrees">where she is lying before the tractor moves.</param>
        public static TowedFollowTrack Build(Vector2[] route, int start, int count, Vector2 plateLocal,
                                             in VehicleKingpin pin, float capDegrees,
                                             float startHeadingDegrees)
        {
            float length = Polyline.Length(route, start, count);
            float step = StepFor(pin, length);
            int stations = Mathf.Max(1, Mathf.CeilToInt(length / step)) + 1;

            var headings = new float[stations];
            headings[0] = startHeadingDegrees;

            float heading = startHeadingDegrees;
            for (int i = 1; i < stations; i++)
            {
                StationStep(route, start, count, plateLocal, pin, capDegrees, step, length, i,
                            ref heading);
                headings[i] = heading;
            }

            return new TowedFollowTrack(route, start, count, plateLocal, pin, headings, step, length);
        }

        /// <summary>
        /// The heading the road leaves her on, without keeping the table — the same walk
        /// <see cref="Build"/> makes, for a caller that only needs the far end.
        ///
        /// <para><see cref="VehicleTripPlan"/> uses it to SETTLE THE DAY: a trailer's resting pose is
        /// the fixed point of "drive out, then drive home", and finding it means running that pair of
        /// walks a few times over. Doing that with the tables would allocate a dozen arrays to throw
        /// away.</para>
        /// </summary>
        public static float WalkHeading(Vector2[] route, int start, int count, Vector2 plateLocal,
                                        in VehicleKingpin pin, float capDegrees,
                                        float startHeadingDegrees)
        {
            float length = Polyline.Length(route, start, count);
            float step = StepFor(pin, length);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / step));

            float heading = startHeadingDegrees;
            for (int i = 1; i <= steps; i++)
                StationStep(route, start, count, plateLocal, pin, capDegrees, step, length, i,
                            ref heading);
            return heading;
        }

        /// <summary>
        /// Where she is and which way she points once the tractor has covered
        /// <paramref name="distance"/> metres of this road.
        ///
        /// <para>⭐ <b>Her heading is read off the table; her POSITION is derived from the plate at the
        /// exact distance asked.</b> Not from the nearest station — a pin that only landed on stations
        /// would let the trailer float off the coupling by up to half a step between them, and the one
        /// thing that has to be true of a coupled pair in every single frame is that her pin is on the
        /// plate.</para>
        /// </summary>
        public void SampleAt(float distance, out Vector2 origin, out float headingDegrees)
        {
            headingDegrees = HeadingAt(distance);
            origin = VehicleCouplingMath.BodyOriginFromKingpin(PlateAt(distance), headingDegrees, _pin);
        }

        /// <summary>Her heading <paramref name="distance"/> metres along, interpolated between stations
        /// the short way round — a lerp between two directions passes through zero, and a zero heading
        /// is a trailer pointing north.</summary>
        public float HeadingAt(float distance)
        {
            if (_headings.Length == 1 || distance <= 0f) return _headings[0];
            if (distance >= LengthMetres) return _headings[_headings.Length - 1];

            float t = distance / StepMetres;
            int i = Mathf.Clamp(Mathf.FloorToInt(t), 0, _headings.Length - 2);
            return Mathf.LerpAngle(_headings[i], _headings[i + 1], Mathf.Clamp01(t - i));
        }

        /// <summary>Where the tractor's plate is <paramref name="distance"/> metres along — the point
        /// the trailer's pin is welded to. Public so a fixture can ask the track the same question the
        /// hitch asks a transform.</summary>
        public Vector2 PlateAt(float distance)
            => PlateAt(_route, _start, _count, _plateLocal, distance);

        /// <summary>The tractor's own heading <paramref name="distance"/> metres along, degrees.</summary>
        public float TractorHeadingAt(float distance)
            => TractorHeadingAt(_route, _start, _count, distance);

        /// <summary>How far along the road station <paramref name="index"/> sits, metres — so a fixture
        /// can drive a live <c>TowedBody</c> over exactly the steps this walked.</summary>
        public float StationDistance(int index) => Mathf.Min(index * StepMetres, LengthMetres);

        /// <summary>The heading solved AT station <paramref name="index"/>, unsmoothed — the table's raw
        /// entry, so a fixture comparing this walk against the player's own tow compares two answers
        /// rather than one answer and an interpolation of it.</summary>
        public float StationHeading(int index) => _headings[Mathf.Clamp(index, 0, _headings.Length - 1)];

        // ---- the walk ---------------------------------------------------------------------------------

        /// <summary>One station of the walk, shared by <see cref="Build"/> and
        /// <see cref="WalkHeading"/> so the table and the settling can never disagree.</summary>
        private static void StationStep(Vector2[] route, int start, int count, Vector2 plateLocal,
                                        in VehicleKingpin pin, float capDegrees, float step,
                                        float length, int station, ref float heading)
        {
            float distance = Mathf.Min(station * step, length);
            float previous = Mathf.Min((station - 1) * step, length);

            Vector2 plate = PlateAt(route, start, count, plateLocal, distance);
            float tractor = TractorHeadingAt(route, start, count, distance);
            VehicleCouplingMath.FollowStep(heading, plate, tractor, distance - previous, capDegrees,
                                           pin, out heading, out _);
        }

        /// <summary>⚠️ The step comes off the trailer, but a road shorter than one step still gets
        /// walked: an unpublished pin has no length scale to divide by, and a 3 m shunt would otherwise
        /// be a single jump.</summary>
        private static float StepFor(in VehicleKingpin pin, float lengthMetres)
        {
            float scale = pin.Published && pin.KingpinToAxleCentreMeters > 0f
                ? pin.KingpinToAxleCentreMeters
                : Mathf.Max(1f, lengthMetres);
            return Mathf.Max(1e-3f, scale / StepsPerLengthScale);
        }

        private static Vector2 PlateAt(Vector2[] route, int start, int count, Vector2 plateLocal,
                                       float distance)
        {
            Vector2 at = Polyline.PointAlong(route, start, count, distance);
            float heading = TractorHeadingAt(route, start, count, distance);
            return at + VehicleCouplingMath.LocalOffsetToWorld(plateLocal, heading);
        }

        /// <summary>The tractor's heading as a compass bearing — the frame the coupling and the picture
        /// both read (<c>BearingDegrees(transform.up)</c>). A road with no length at all leaves her
        /// pointing north, which is only reachable by a plan with no geometry.</summary>
        private static float TractorHeadingAt(Vector2[] route, int start, int count, float distance)
        {
            Vector2 tangent = Polyline.TangentAlong(route, start, count, distance);
            return tangent == Vector2.zero ? 0f : BoatKinematics.BearingDegrees(tangent);
        }
    }
}
