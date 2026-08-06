using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// ONE echo, ready to draw — the rig's own target shape (<c>radarRig.js:137-145</c>
    /// <c>{brgTrue, rngNM, size, kind, crs, spd}</c>) in C#.
    ///
    /// <para><b>This is the SCOPE's frame, not the world's.</b> <see cref="RadarContact"/> crosses the
    /// Core seam in world metres, for the reasons that struct gives; this is what one contact becomes
    /// after the host has measured it from own ship through the one bearing convention. The conversion
    /// happens in exactly one place (<c>RadarOverlayHost</c>), which is what keeps a second bearing
    /// convention out of the codebase.</para>
    /// </summary>
    public readonly struct RadarBlip
    {
        /// <summary>TRUE bearing from own ship, 0 = N, clockwise — <see cref="NavMath.BearingDegrees"/>'s
        /// output, unconverted.</summary>
        public readonly float BearingTrue;

        /// <summary>Distance from own ship in nautical miles (the unit the scope reads in).</summary>
        public readonly float RangeNM;

        /// <summary>The rig's echo size multiplier (<see cref="RadarContact.Size"/>).</summary>
        public readonly float Size;

        /// <summary>Which painter draws it.</summary>
        public readonly RadarEchoKind Kind;

        /// <summary>The contact's course, 0 = N clockwise, or <see cref="float.NaN"/> for something not
        /// making way. Only a vessel with a course and a trail scale grows an afterglow.</summary>
        public readonly float CourseDeg;

        /// <summary>
        /// How long an afterglow trail this contact drags, in the rig's own units — its <c>spd</c>
        /// (radarRig.js:259, <c>step = r*0.05*spd</c> per trail segment, five segments).
        ///
        /// <para><b>Not knots, and not metres per second.</b> The rig's authored scene puts working
        /// craft at 0.8 and 1.1, which at five segments is a trail a fifth of the scope's radius —
        /// clearly a normalised drawing factor rather than a speed. The host converts m/s through
        /// <see cref="RadarRigRender.TrailReferenceSpeedMps"/>, which is calibrated so a punt at working
        /// speed lands on the rig's own 0.8.</para>
        /// </summary>
        public readonly float TrailScale;

        public RadarBlip(float bearingTrue, float rangeNM, float size, RadarEchoKind kind,
                         float courseDeg, float trailScale)
        {
            BearingTrue = bearingTrue;
            RangeNM = rangeNM;
            Size = size;
            Kind = kind;
            CourseDeg = courseDeg;
            TrailScale = trailScale;
        }

        /// <summary>True when this echo has a course and way on, so the rig draws its motion trail.</summary>
        public bool HasTrail => !float.IsNaN(CourseDeg) && TrailScale > 0f;
    }

    /// <summary>
    /// Everything the radar's glass draws this frame — the C# form of the options bag
    /// <c>radarRig.js:394-401</c> normalises. A pure value, so the whole picture is decided before a
    /// single pixel is touched and an EditMode test can compose one without a canvas.
    ///
    /// <para><b>The defaults are the RIG's defaults.</b> Where a field has an obvious "off", it is the
    /// value the rig's own normaliser picks, so a state built from <see cref="Default"/> paints what the
    /// rig's preview paints. Divergences (the range ladder, the clutter sources) are documented on
    /// <see cref="RadarSettings"/> and at the host.</para>
    /// </summary>
    public readonly struct RadarRigState
    {
        /// <summary>The range at the scope's RIM, nautical miles.</summary>
        public readonly float RangeNM;

        /// <summary>How many range rings between centre and rim.</summary>
        public readonly int Rings;

        /// <summary>Picture turned bow-up rather than north-up.</summary>
        public readonly bool HeadUp;

        /// <summary>Own ship's heading, 0 = N clockwise — the head-up rotation AND the heading marker.</summary>
        public readonly float HeadingDeg;

        /// <summary>Set gain 0..1 — scales every echo's brightness.</summary>
        public readonly float Gain;

        /// <summary>Sea-clutter strength 0..1 — the speckle near the centre.</summary>
        public readonly float Sea;

        /// <summary>Rain-clutter strength 0..1 — the haze band. Zero in this slice (see
        /// <see cref="RadarSettings"/>: precipitation returns are M4).</summary>
        public readonly float Rain;

        /// <summary>The aerial is turning and the set is radiating. False = STANDBY: no sweep, and every
        /// echo sits at the flat half-lit level <see cref="RadarRigGeometry.SweepBrightness"/> gives.</summary>
        public readonly bool Transmitting;

        /// <summary>Draw motion trails behind moving vessels.</summary>
        public readonly bool Trails;

        /// <summary>Amber night phosphor rather than the green day palette.</summary>
        public readonly bool Night;

        /// <summary>Where the sweep is right now, degrees in the scope's own frame.</summary>
        public readonly float SweepDeg;

        /// <summary>The guard alarm's flash phase (the rig blinks the rim while a target is in the
        /// zone). Meaningless while <see cref="GuardOn"/> is false.</summary>
        public readonly bool Blink;

        /// <summary>Electronic Bearing Line on, at <see cref="EblBearing"/>.</summary>
        public readonly bool EblOn;

        /// <summary>The EBL's true bearing.</summary>
        public readonly float EblBearing;

        /// <summary>Variable Range Marker on, at <see cref="VrmNM"/>.</summary>
        public readonly bool VrmOn;

        /// <summary>The VRM ring's range in nautical miles.</summary>
        public readonly float VrmNM;

        /// <summary>Guard zone armed. Nothing in this slice turns it on — no pusher exposes it and the
        /// alarm is another lane's — but the renderer transcribes the rig's drawing of it, and the
        /// renderer tests drive it directly so the path is exercised rather than dead.</summary>
        public readonly bool GuardOn;

        /// <summary>Guard sector, clockwise from <see cref="GuardFrom"/> to <see cref="GuardTo"/>.</summary>
        public readonly float GuardFrom, GuardTo;

        /// <summary>Guard sector's near and far edges, as fractions of the scope's range.</summary>
        public readonly float GuardNear, GuardFar;

        public RadarRigState(float rangeNM, int rings, bool headUp, float headingDeg,
                             float gain, float sea, float rain, bool transmitting, bool trails,
                             bool night, float sweepDeg, bool blink,
                             bool eblOn = false, float eblBearing = 45f,
                             bool vrmOn = false, float vrmNM = 2f,
                             bool guardOn = false, float guardFrom = 20f, float guardTo = 80f,
                             float guardNear = 0.55f, float guardFar = 0.85f)
        {
            RangeNM = rangeNM;
            Rings = rings < 1 ? 1 : rings;
            HeadUp = headUp;
            HeadingDeg = headingDeg;
            Gain = Clamp01(gain);
            Sea = Clamp01(sea);
            Rain = Clamp01(rain);
            Transmitting = transmitting;
            Trails = trails;
            Night = night;
            SweepDeg = NavMath.Norm360(sweepDeg);
            Blink = blink;
            EblOn = eblOn;
            EblBearing = eblBearing;
            VrmOn = vrmOn;
            VrmNM = vrmNM;
            GuardOn = guardOn;
            GuardFrom = guardFrom;
            GuardTo = guardTo;
            GuardNear = guardNear;
            GuardFar = guardFar;
        }

        /// <summary>The rig's own normalised defaults (<c>radarRig.js:394-401</c>) — a 6 NM scope, four
        /// rings, head-up, transmitting, trails on, day palette.</summary>
        public static RadarRigState Default =>
            new RadarRigState(6f, 4, true, 0f, 0.72f, 0.18f, 0.10f, true, true, false, 0f, false);

        /// <summary>This state with a different sweep angle — the one term that changes on the
        /// animation tick, so the host does not rebuild the whole struct for it.</summary>
        public RadarRigState WithSweep(float sweepDeg, bool blink)
            => new RadarRigState(RangeNM, Rings, HeadUp, HeadingDeg, Gain, Sea, Rain, Transmitting,
                                 Trails, Night, sweepDeg, blink, EblOn, EblBearing, VrmOn, VrmNM,
                                 GuardOn, GuardFrom, GuardTo, GuardNear, GuardFar);

        /// <summary>This state with the EBL/VRM cursor set (or cleared).</summary>
        public RadarRigState WithCursor(bool on, float bearing, float rangeNM)
            => new RadarRigState(RangeNM, Rings, HeadUp, HeadingDeg, Gain, Sea, Rain, Transmitting,
                                 Trails, Night, SweepDeg, Blink, on, bearing, on, rangeNM,
                                 GuardOn, GuardFrom, GuardTo, GuardNear, GuardFar);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
