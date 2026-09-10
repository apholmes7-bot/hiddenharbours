using System;
using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    /// <summary>The thirteen states the seagull sheet is baked for, by their sidecar / sheet ids.
    /// <see cref="Order"/> is the canonical index order this module drives them in; the sidecar
    /// supplies the NUMBERS for each one and a test asserts the two sets agree exactly, so a rig
    /// re-export that adds or drops a state fails loudly instead of silently doing nothing.</summary>
    public static class SeagullStates
    {
        public const string Fly = "fly";
        public const string Glide = "glide";
        public const string Swoop = "swoop";
        public const string Dive = "dive";
        public const string Splash = "splash";
        public const string Float = "float";
        public const string Preen = "preen";
        public const string Peck = "peck";
        public const string Land = "land";
        public const string Stand = "stand";
        public const string Walk = "walk";
        public const string Perch = "perch";
        public const string Takeoff = "takeoff";

        /// <summary>Canonical order — also the order the contract lays the sheet's columns out in.</summary>
        public static readonly string[] Order =
        {
            Fly, Glide, Swoop, Dive, Splash, Float, Preen, Peck, Land, Stand, Walk, Perch, Takeoff
        };

        /// <summary>Index in <see cref="Order"/>, or −1.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < Order.Length; i++)
                if (string.Equals(Order[i], id, StringComparison.Ordinal)) return i;
            return -1;
        }
    }

    /// <summary>What a state stands the bird ON. Airborne states have <see cref="None"/> — including
    /// <c>land</c> and <c>splash</c>, which are the ARRIVALS onto a surface rather than the rest on
    /// it.</summary>
    public enum SeagullSurface
    {
        /// <summary>Airborne (or on the way down).</summary>
        None = 0,
        /// <summary>Standing on the elevation raster — the terrain under the pivot.</summary>
        Ground = 1,
        /// <summary>On the water surface — the pivot sits AT the waterline (<c>float_body_z</c> is
        /// baked into the pose, not added here).</summary>
        Water = 2,
        /// <summary>Gripping a prop or a hull. ⚠️ PR 4 — nothing in this PR puts a bird here.</summary>
        Perch = 3,
    }

    /// <summary>One row of the sidecar's <c>STATES</c> table, engine-light.</summary>
    public readonly struct SeagullStateRow
    {
        public readonly string Id;
        public readonly int Frames;
        public readonly double FrameMilliseconds;
        public readonly double SpeedMetresPerSecond;
        public readonly double ClimbMetresPerSecond;
        /// <summary>The sidecar's <c>alt[0]</c>, raw. See <see cref="SeagullBehaviour.EntryAltitude"/>
        /// for how the pair is read.</summary>
        public readonly double AltitudeA;
        /// <summary>The sidecar's <c>alt[1]</c>, raw.</summary>
        public readonly double AltitudeB;
        public readonly bool Loop;
        /// <summary>The oneshot chain target (<c>next</c>), or null.</summary>
        public readonly string NextId;
        public readonly double TravelMetres;
        /// <summary>Loops to run before the state hands back, from <c>cycles</c>; 0 when the state
        /// loops forever.</summary>
        public readonly int CyclesMin, CyclesMax;

        public SeagullStateRow(string id, int frames, double frameMilliseconds,
                               double speedMetresPerSecond, double climbMetresPerSecond,
                               double altitudeA, double altitudeB, bool loop, string nextId,
                               double travelMetres, int cyclesMin, int cyclesMax)
        {
            Id = id; Frames = frames; FrameMilliseconds = frameMilliseconds;
            SpeedMetresPerSecond = speedMetresPerSecond; ClimbMetresPerSecond = climbMetresPerSecond;
            AltitudeA = altitudeA; AltitudeB = altitudeB; Loop = loop; NextId = nextId;
            TravelMetres = travelMetres; CyclesMin = cyclesMin; CyclesMax = cyclesMax;
        }

        /// <summary>Milliseconds one pass of the state takes.</summary>
        public double DurationMilliseconds => Frames * FrameMilliseconds;
    }

    /// <summary>A legal edge of the sidecar's <c>TRANSITIONS</c> table.</summary>
    public readonly struct SeagullEdge
    {
        public readonly string From, To;
        public SeagullEdge(string from, string to) { From = from; To = to; }
    }

    /// <summary>The sidecar's <c>LAND</c> block — what a patch of the world must offer before a bird
    /// will come down on it.</summary>
    public readonly struct SeagullLandRules
    {
        /// <summary>Clear box the bird needs, metres: across × along. 1.4 is the WINGSPAN — a gull
        /// lands with its wings open, so the box is far wider than the bird that ends up standing in
        /// it.</summary>
        public readonly double ClearX, ClearY;
        /// <summary>True when the approach must be flown into the wind (it is; the shared
        /// <c>_WindWorld</c> supplies it).</summary>
        public readonly bool ApproachIntoWind;
        /// <summary>Metres of flat the pivot needs under it.</summary>
        public readonly double MinFlatMetres;

        public SeagullLandRules(double clearX, double clearY, bool approachIntoWind, double minFlatMetres)
        { ClearX = clearX; ClearY = clearY; ApproachIntoWind = approachIntoWind; MinFlatMetres = minFlatMetres; }
    }

    /// <summary>The sidecar's <c>WATER</c> block, plus the ROCK cap its note names.</summary>
    public readonly struct SeagullWaterRules
    {
        public readonly double FloatBodyZMetres;
        public readonly int SplashBurstFrame;
        public readonly bool DriftWithCurrent;
        /// <summary>The fleet ROCK pose, capped for a bird: "roll_deg 2.4 / heave_px 1 max — a gull
        /// rides higher than a hull".</summary>
        public readonly double RockRollDegreesMax, RockHeavePixelsMax;

        public SeagullWaterRules(double floatBodyZMetres, int splashBurstFrame, bool driftWithCurrent,
                                 double rockRollDegreesMax, double rockHeavePixelsMax)
        {
            FloatBodyZMetres = floatBodyZMetres; SplashBurstFrame = splashBurstFrame;
            DriftWithCurrent = driftWithCurrent;
            RockRollDegreesMax = rockRollDegreesMax; RockHeavePixelsMax = rockHeavePixelsMax;
        }
    }

    /// <summary>The sidecar's <c>FLOCK</c> block — the numbers <c>flock()</c> itself reads, plus the
    /// gameplay radii this PR only carries (the attractor list and the steal rules land in PR 3).</summary>
    public readonly struct SeagullFlockRules
    {
        public readonly int SizeMin, SizeMax;
        public readonly double RadiusMetres;
        public readonly double AltitudeMinMetres, AltitudeMaxMetres;
        public readonly double PeriodMinSeconds, PeriodMaxSeconds;
        public readonly double GlideDuty;
        public readonly double SwoopEveryMinSeconds, SwoopEveryMaxSeconds;
        public readonly double SpacingMetres, AttractRadiusMetres, FleeRadiusMetres;
        public readonly double SettleAfterSeconds, RegroupSeconds;

        public SeagullFlockRules(int sizeMin, int sizeMax, double radiusMetres,
                                 double altitudeMinMetres, double altitudeMaxMetres,
                                 double periodMinSeconds, double periodMaxSeconds, double glideDuty,
                                 double swoopEveryMinSeconds, double swoopEveryMaxSeconds,
                                 double spacingMetres, double attractRadiusMetres, double fleeRadiusMetres,
                                 double settleAfterSeconds, double regroupSeconds)
        {
            SizeMin = sizeMin; SizeMax = sizeMax; RadiusMetres = radiusMetres;
            AltitudeMinMetres = altitudeMinMetres; AltitudeMaxMetres = altitudeMaxMetres;
            PeriodMinSeconds = periodMinSeconds; PeriodMaxSeconds = periodMaxSeconds;
            GlideDuty = glideDuty;
            SwoopEveryMinSeconds = swoopEveryMinSeconds; SwoopEveryMaxSeconds = swoopEveryMaxSeconds;
            SpacingMetres = spacingMetres; AttractRadiusMetres = attractRadiusMetres;
            FleeRadiusMetres = fleeRadiusMetres;
            SettleAfterSeconds = settleAfterSeconds; RegroupSeconds = regroupSeconds;
        }
    }

    /// <summary>
    /// <b>THE STATE TABLE, IMMUTABLE AND ENGINE-LIGHT.</b>
    ///
    /// <para>Thirteen states and twenty-six legal edges, exactly as
    /// <c>docs/art/rigs/gameplay/seagullIsoRig.gameplay.json</c> declares them. Nothing here decides
    /// anything — it only answers questions about what the rig permits, so
    /// <see cref="SeagullStateMachine"/> can be a pure function and <see cref="GullFlock"/> can be a
    /// presenter.</para>
    ///
    /// <para><b>The three structural facts the charter names are DERIVED, not typed.</b> "Only takeoff
    /// leaves a surface, only land and splash arrive on one" is computed from the edge table
    /// (<see cref="LeavesSurface"/>, <see cref="ArrivesOnSurface"/>) and asserted by a test. If a rig
    /// re-export adds an edge that breaks the claim, the test reddens — which is the point of writing
    /// it this way rather than hard-coding three ids.</para>
    ///
    /// <para><b>Reading the <c>alt</c> pair.</b> The sidecar uses the same two-number field two ways:
    /// a cruise BAND for looping states (<c>fly [6,25]</c>) and an entry→exit RAMP for oneshots
    /// (<c>land [0.6,0]</c>, <c>takeoff [0,1.2]</c>, <c>dive [0.3,8]</c> — note that last one is written
    /// low-first even though a dive plainly goes down). One rule covers all four without a special
    /// case: the pair is a band, and the sign of <c>vz</c> says which end the bird ENTERS at. See
    /// <see cref="EntryAltitude"/>.</para>
    /// </summary>
    public sealed class SeagullBehaviour
    {
        private readonly SeagullStateRow[] _rows;
        private readonly int[] _next;              // oneshot chain target, or -1
        private readonly uint[] _edges;            // bit j of _edges[i] = "i -> j is legal"
        private readonly SeagullSurface[] _surface;
        private readonly bool[] _leavesSurface;
        private readonly bool[] _arrivesOnSurface;
        private readonly int _edgeCount;

        public readonly SeagullLandRules Land;
        public readonly SeagullWaterRules Water;
        public readonly SeagullFlockRules Flock;

        /// <summary>States, in <see cref="SeagullStates.Order"/>.</summary>
        public int StateCount => _rows.Length;

        /// <summary>Legal edges declared. The drop declares 26.</summary>
        public int EdgeCount => _edgeCount;

        /// <summary>
        /// Builds the table. <paramref name="rows"/> may arrive in any order — they are placed by id
        /// into <see cref="SeagullStates.Order"/>, and a missing or unknown id throws, because a
        /// half-populated state table is worse than no birds at all.
        /// </summary>
        public SeagullBehaviour(IReadOnlyList<SeagullStateRow> rows, IReadOnlyList<SeagullEdge> edges,
                                in SeagullLandRules land, in SeagullWaterRules water,
                                in SeagullFlockRules flock)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (edges == null) throw new ArgumentNullException(nameof(edges));

            int n = SeagullStates.Order.Length;
            _rows = new SeagullStateRow[n];
            var seen = new bool[n];

            for (int i = 0; i < rows.Count; i++)
            {
                int k = SeagullStates.IndexOf(rows[i].Id);
                if (k < 0)
                    throw new ArgumentException(
                        $"SeagullBehaviour: state '{rows[i].Id}' is not one of the {n} the sheet is baked for. " +
                        "Re-bake the sheet before teaching the runtime a new state.");
                if (seen[k])
                    throw new ArgumentException($"SeagullBehaviour: state '{rows[i].Id}' declared twice.");
                _rows[k] = rows[i];
                seen[k] = true;
            }
            for (int i = 0; i < n; i++)
                if (!seen[i])
                    throw new ArgumentException(
                        $"SeagullBehaviour: the sidecar declares no '{SeagullStates.Order[i]}' state.");

            _next = new int[n];
            _surface = new SeagullSurface[n];
            for (int i = 0; i < n; i++)
            {
                _next[i] = string.IsNullOrEmpty(_rows[i].NextId) ? -1 : SeagullStates.IndexOf(_rows[i].NextId);
                _surface[i] = SurfaceForId(_rows[i].Id);
            }

            _edges = new uint[n];
            int count = 0;
            for (int e = 0; e < edges.Count; e++)
            {
                int from = SeagullStates.IndexOf(edges[e].From);
                int to = SeagullStates.IndexOf(edges[e].To);
                if (from < 0 || to < 0)
                    throw new ArgumentException(
                        $"SeagullBehaviour: transition '{edges[e].From}' -> '{edges[e].To}' names a state the sheet has no frames for.");
                uint bit = 1u << to;
                if ((_edges[from] & bit) == 0u) { _edges[from] |= bit; count++; }
            }
            _edgeCount = count;

            // Derived, not typed — see the class remarks.
            _leavesSurface = new bool[n];
            _arrivesOnSurface = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (_surface[i] == SeagullSurface.None)
                {
                    for (int f = 0; f < n; f++)
                        if (_surface[f] != SeagullSurface.None && CanGo(f, i)) { _leavesSurface[i] = true; break; }
                }
                _arrivesOnSurface[i] = !_rows[i].Loop && _next[i] >= 0 &&
                                       _surface[_next[i]] != SeagullSurface.None;
            }

            Land = land; Water = water; Flock = flock;
        }

        /// <summary>Which surface a state rests the bird on. Hard-coded on purpose: it is a fact about
        /// what the ARTIST drew (a float pose sits in water; a stand pose does not), not a number the
        /// sidecar carries.</summary>
        private static SeagullSurface SurfaceForId(string id)
        {
            switch (id)
            {
                case SeagullStates.Stand:
                case SeagullStates.Walk:
                    return SeagullSurface.Ground;
                case SeagullStates.Float:
                case SeagullStates.Preen:
                case SeagullStates.Peck:
                    return SeagullSurface.Water;
                case SeagullStates.Perch:
                    return SeagullSurface.Perch;
                default:
                    return SeagullSurface.None;
            }
        }

        public int IndexOf(string id) => SeagullStates.IndexOf(id);
        public string IdOf(int state) => _rows[state].Id;
        public SeagullStateRow Row(int state) => _rows[state];

        /// <summary>True when <paramref name="from"/> → <paramref name="to"/> is one of the declared
        /// edges. Out-of-range is false, never an exception — a presenter asking about a state it has
        /// lost track of should get "no", not a crash mid-frame.</summary>
        public bool CanGo(int from, int to)
        {
            if (from < 0 || from >= _rows.Length || to < 0 || to >= _rows.Length) return false;
            return (_edges[from] & (1u << to)) != 0u;
        }

        /// <summary>The oneshot chain target, or −1 for a looping state.</summary>
        public int Next(int state) =>
            state < 0 || state >= _rows.Length ? -1 : _next[state];

        public bool IsLoop(int state) => state >= 0 && state < _rows.Length && _rows[state].Loop;

        /// <summary>Derived: an airborne state reachable from a surface state. <c>takeoff</c> is the
        /// only one in the drop, and a test says so.</summary>
        public bool LeavesSurface(int state) =>
            state >= 0 && state < _rows.Length && _leavesSurface[state];

        /// <summary>Derived: a oneshot whose chain target rests on a surface. <c>land</c> and
        /// <c>splash</c> are the only two in the drop, and a test says so.</summary>
        public bool ArrivesOnSurface(int state) =>
            state >= 0 && state < _rows.Length && _arrivesOnSurface[state];

        /// <summary>Where <see cref="ArrivesOnSurface"/> puts the bird — <see cref="SeagullSurface.Ground"/>
        /// for <c>land</c>, <see cref="SeagullSurface.Water"/> for <c>splash</c>.</summary>
        public SeagullSurface ArrivalSurface(int state) =>
            _arrivesOnSurface[state] ? _surface[_next[state]] : SeagullSurface.None;

        public SeagullSurface SurfaceOf(int state) =>
            state < 0 || state >= _rows.Length ? SeagullSurface.None : _surface[state];

        /// <summary>True for the three states <c>flock()</c> itself drives. Their position AND altitude
        /// come from <see cref="SeagullFlockMath"/> while the bird is flock-bound; every other state
        /// integrates its own.</summary>
        public bool IsFlockDriven(int state) =>
            state == SeagullStates.IndexOf(SeagullStates.Fly) ||
            state == SeagullStates.IndexOf(SeagullStates.Glide) ||
            state == SeagullStates.IndexOf(SeagullStates.Swoop);

        /// <summary>Lowest altitude the state's band allows.</summary>
        public double AltitudeMin(int state) => Math.Min(_rows[state].AltitudeA, _rows[state].AltitudeB);

        /// <summary>Highest altitude the state's band allows.</summary>
        public double AltitudeMax(int state) => Math.Max(_rows[state].AltitudeA, _rows[state].AltitudeB);

        /// <summary>
        /// The end of the band the bird ENTERS at, read off the sign of <c>vz</c>: a descending state
        /// enters high, a climbing state enters low. That single rule reads all four of the drop's
        /// oneshots correctly — <c>land</c> 0.6→0, <c>dive</c> 8→0.3, <c>takeoff</c> 0→1.2,
        /// <c>splash</c> 0→0 — without knowing any of their names.
        /// </summary>
        public double EntryAltitude(int state) =>
            _rows[state].ClimbMetresPerSecond < 0.0 ? AltitudeMax(state) : AltitudeMin(state);

        /// <summary>The other end of the band — where the state leaves the bird.</summary>
        public double ExitAltitude(int state) =>
            _rows[state].ClimbMetresPerSecond < 0.0 ? AltitudeMin(state) : AltitudeMax(state);

        /// <summary>Everything <see cref="SeagullFlockMath.Flock"/> needs, assembled from the FLOCK
        /// block and the three airborne rows so the port never carries its own copy of a tunable.</summary>
        public SeagullFlockMath.SeagullFlockTuning FlockTuning
        {
            get
            {
                int fly = SeagullStates.IndexOf(SeagullStates.Fly);
                int glide = SeagullStates.IndexOf(SeagullStates.Glide);
                int swoop = SeagullStates.IndexOf(SeagullStates.Swoop);
                return new SeagullFlockMath.SeagullFlockTuning(
                    Flock.RadiusMetres,
                    Flock.PeriodMinSeconds, Flock.PeriodMaxSeconds,
                    Flock.AltitudeMinMetres, Flock.AltitudeMaxMetres,
                    Flock.SwoopEveryMinSeconds, Flock.SwoopEveryMaxSeconds,
                    Flock.GlideDuty,
                    _rows[fly].Frames, _rows[fly].FrameMilliseconds,
                    _rows[glide].Frames, _rows[glide].FrameMilliseconds,
                    _rows[swoop].Frames, _rows[swoop].FrameMilliseconds);
            }
        }

        /// <summary>The state index a <see cref="SeagullFlockMath.SeagullFlockAnim"/> names.</summary>
        public int StateOf(SeagullFlockMath.SeagullFlockAnim anim) =>
            SeagullStates.IndexOf(SeagullFlockMath.AnimId(anim));
    }
}
