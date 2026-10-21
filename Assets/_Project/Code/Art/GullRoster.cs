using System;

namespace HiddenHarbours.Art
{
    /// <summary>Where the schedule puts a gull.</summary>
    public enum GullWhere : byte
    {
        /// <summary>Off the map: foraging farther afield, or roosting where the rock had no room. Never drawn.</summary>
        Away = 0,

        /// <summary>At its haunt (or over its school).</summary>
        AtHaunt = 1,

        /// <summary>At its roost.</summary>
        AtRoost = 2,

        /// <summary>In the air between two of those, on a straight line.</summary>
        Commuting = 3,
    }

    /// <summary>An axis-aligned rectangle on the ground, metres.</summary>
    public struct GullRect
    {
        public double MinX, MinY, MaxX, MaxY;

        public GullRect(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>The box centre ± half-size.</summary>
        public static GullRect Box(double centreX, double centreY, double halfX, double halfY)
            => new GullRect(centreX - halfX, centreY - halfY, centreX + halfX, centreY + halfY);

        public bool IsEmpty => !(MaxX > MinX) || !(MaxY > MinY);
        public double CentreX => 0.5 * (MinX + MaxX);
        public double CentreY => 0.5 * (MinY + MaxY);

        public bool Contains(double x, double y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

        public bool Overlaps(in GullRect other)
            => MinX <= other.MaxX && other.MinX <= MaxX && MinY <= other.MaxY && other.MinY <= MaxY;

        /// <summary>Grown by the same margin on every side.</summary>
        public GullRect Grown(double by) => new GullRect(MinX - by, MinY - by, MaxX + by, MaxY + by);

        /// <summary>The bottom edge lowered: a bird in the air is drawn UP the screen from its ground
        /// point, so one over the ground just below the rectangle can still be drawn inside it.</summary>
        public GullRect LoweredBottom(double by) => new GullRect(MinX, MinY - by, MaxX, MaxY);

        /// <summary>The smallest rectangle holding this one and a point.</summary>
        public GullRect With(double x, double y)
            => new GullRect(Math.Min(MinX, x), Math.Min(MinY, y), Math.Max(MaxX, x), Math.Max(MaxY, y));

        /// <summary>The smallest rectangle holding both.</summary>
        public GullRect With(in GullRect other)
            => new GullRect(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY),
                            Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));
    }

    /// <summary>The roster's knobs, from the flock's <see cref="GullConfig"/> and the region.</summary>
    public struct GullRosterSettings
    {
        /// <summary>How fast a commuting gull flies, metres per game second.</summary>
        public double CommuteSpeedMetresPerSecond;

        /// <summary>How far beyond the view a place still counts as near.</summary>
        public double ShowMarginMetres;

        /// <summary>How much farther a near place must be before it stops being near.</summary>
        public double ShowHysteresisMetres;

        /// <summary>How far up the screen a bird in the air can be drawn from its ground point, in
        /// ground metres. The near rectangle reaches this much farther DOWN, so the birds over a place
        /// just below the view, which are drawn up into it, count as near.</summary>
        public double LiftMetres;

        /// <summary>The region's bounds: a bird going away flies out to this edge.</summary>
        public GullRect Region;
    }

    /// <summary>One gull as the roster places it: its slot, where it is, and in flight the line it is on.</summary>
    public struct GullRosterBird
    {
        /// <summary>Stable for the bird while its region is loaded (a school's while the school shows).</summary>
        public int Key;

        /// <summary>The place the slot belongs to: a haunt or a school for a visitor, a roost for a resident.</summary>
        public int Place;

        /// <summary>The slot: k at a haunt, <see cref="GullRoster.ResidentBase"/> + j for a roost's resident.</summary>
        public int Slot;

        public GullWhere Where;

        /// <summary>The place it is at, or flying to; −1 for away.</summary>
        public int At;

        /// <summary>In flight, the place it left; −1 for away.</summary>
        public int From;

        /// <summary>Its ground point at the roster's time: its spot, or its point on the line.</summary>
        public double X, Y;

        /// <summary>The line it is on (placed birds: both ends are the spot).</summary>
        public double FromX, FromY, ToX, ToY;

        /// <summary>When it left and how long the flight is, game seconds (0 when placed).</summary>
        public double DepartSeconds, FlightSeconds;

        /// <summary>Squared distance from the view's centre: the near order.</summary>
        public double DistanceSq;

        /// <summary>How far along the line at a game time, 0..1 (1 when placed).</summary>
        public double ProgressAt(double gameSeconds)
            => FlightSeconds > 0.0 ? GullSchedule.Clamp01((gameSeconds - DepartSeconds) / FlightSeconds) : 1.0;

        /// <summary>Its point on the line at a game time (its spot when placed).</summary>
        public void PointAt(double gameSeconds, out double x, out double y)
        {
            if (Where != GullWhere.Commuting)
            {
                x = X;
                y = Y;
                return;
            }
            double u = ProgressAt(gameSeconds);
            x = FromX + (ToX - FromX) * u;
            y = FromY + (ToY - FromY) * u;
        }
    }

    /// <summary>
    /// THE GULL ROSTER — every gull's place at a game time, worked out from the schedule alone.
    ///
    /// <para><b>A bird belongs to a place, not to the view.</b> Each place has numbered slots. Slot k
    /// of a haunt is AT the haunt when the schedule's expected birds exceed k + r (r hashed from the
    /// seed, the place, the slot and the day), so as the count rises the birds arrive one at a time
    /// and as it falls they leave in the reverse order. An absent slot that is in today's population
    /// is at its roost when its hashed home number is under the roost share (scaled down when the
    /// roost's ledges are full), and away otherwise. A roost's residents come and go by the same rule
    /// against the colony's size. Spots are hashes too, so the same slot sits on the same spot.</para>
    ///
    /// <para><b>Commuting.</b> A slot's place only changes when a count crosses its threshold. The
    /// roster looks back over the longest flight the region allows, on a grid of absolute times, finds
    /// the most recent change and bisects it to the switch time; a bird still within its flight time
    /// of that switch is in the air on the straight line between its two spots. The grid is anchored
    /// to absolute times, so the same switch is found at the same instant on every refresh and a
    /// flying bird moves smoothly.</para>
    ///
    /// <para><b>The view only chooses which birds are drawn.</b> <see cref="Refresh"/> takes a view
    /// rectangle and lists the birds near it, nearest first, and binds them to a fixed pool; nothing
    /// about where any bird is depends on it. The roster keeps no history but the near flags (which
    /// places count as near) and the pool binding (which pool bird draws which slot).</para>
    ///
    /// <para><b>No allocation after construction</b> but a one-time growth of the look-back grid when a
    /// larger region is first configured (rule 7).</para>
    /// </summary>
    public sealed class GullRoster
    {
        /// <summary>A roost's residents are numbered from here, after any visitor slot.</summary>
        public const int ResidentBase = 512;

        /// <summary>The most slots a place can have: Capacity and Room stay below this (content test).</summary>
        public const int MaxSlotsPerPlace = 511;

        /// <summary>The most birds one school can hold.</summary>
        public const int MaxSchoolSlots = 63;

        /// <summary>The look-back grid's step, game seconds. Each switch is bisected inside one step.</summary>
        public const double LookbackStepSeconds = 10.0;

        /// <summary>The bisection's resolution, game seconds: under ten centimetres of flight, in about
        /// ten halvings of a grid step.</summary>
        public const double SwitchToleranceSeconds = 0.01;

        private const int SchoolKeyBit = 0x40000000;
        private const int AuthoredKeyShift = 10;

        /// <summary>The schedule at one moment, for every place in the region.</summary>
        private sealed class Moment
        {
            public double Seconds;
            public GullScheduleInputs Inputs;
            public double Share;
            public readonly double[] Expected;
            public readonly double[] Population;
            public readonly double[] CapShare;
            public readonly double[] Residents;

            public Moment(int places)
            {
                Expected = new double[places];
                Population = new double[places];
                CapShare = new double[places];
                Residents = new double[places];
            }
        }

        private readonly int _maxPlaces;
        private readonly Moment _now;
        private readonly Moment _probe;
        private Moment[] _grid;
        private int _gridCount;
        private readonly double[] _popSum;
        private readonly double[] _awaySum;
        private readonly bool[] _placeNear;

        private GullHauntSpec[] _places;
        private int _authored;
        private int _count;
        private GullTuningSpec _tuning;
        private GullRosterSettings _settings;
        private GullCalendar _calendar;
        private IGullWeather _weather;
        private double _longestFlightSeconds;
        private GullRect _view;
        private GullRect _nearRect;
        private GullRect _keepRect;
        private int _stored;

        /// <summary>The near birds of the last refresh, nearest the view's centre first
        /// (<see cref="StoredCount"/> of them).</summary>
        public readonly GullRosterBird[] Near;

        /// <summary>For each stored near bird, the pool bird drawing it, or −1 (a count only).</summary>
        public readonly int[] NearPool;

        /// <summary>For each pool bird, the key of the slot it draws, or −1 (free).</summary>
        public readonly int[] PoolKey;

        /// <summary>For each pool bird, its entry in <see cref="Near"/> this refresh, or −1.</summary>
        public readonly int[] PoolNear;

        /// <summary>Every near bird of the last refresh, drawn or only counted.</summary>
        public int NearCount { get; private set; }

        /// <summary>How many near birds <see cref="Near"/> holds (the nearest, up to its length).</summary>
        public int StoredCount => _stored;

        /// <summary>How many pool birds are drawing a slot.</summary>
        public int BoundCount { get; private set; }

        /// <summary>The longest flight in the configured region, game seconds.</summary>
        public double LongestFlightSeconds => _longestFlightSeconds;

        /// <summary>The view the last refresh was taken against.</summary>
        public GullRect View => _view;

        /// <summary>The view grown by the show margin, its bottom lowered by the lift: what counts as near.</summary>
        public GullRect NearRect => _nearRect;

        /// <summary>The schedule's inputs at the last refresh's time.</summary>
        public GullScheduleInputs NowInputs => _now.Inputs;

        /// <summary>The last refresh's game time.</summary>
        public double NowSeconds => _now.Seconds;

        public GullRoster(int maxPlaces, int maxNear, int poolSize)
        {
            _maxPlaces = Math.Max(1, maxPlaces);
            _now = new Moment(_maxPlaces);
            _probe = new Moment(_maxPlaces);
            _grid = new Moment[0];
            _popSum = new double[_maxPlaces];
            _awaySum = new double[_maxPlaces];
            _placeNear = new bool[_maxPlaces];

            Near = new GullRosterBird[Math.Max(1, maxNear)];
            NearPool = new int[Near.Length];
            PoolKey = new int[Math.Max(0, poolSize)];
            PoolNear = new int[PoolKey.Length];
            for (int i = 0; i < PoolKey.Length; i++)
            {
                PoolKey[i] = -1;
                PoolNear[i] = -1;
            }
        }

        /// <summary>Forgets which places were near (a region change), and frees the pool.</summary>
        public void ForgetNear()
        {
            Array.Clear(_placeNear, 0, _placeNear.Length);
            for (int i = 0; i < PoolKey.Length; i++)
            {
                PoolKey[i] = -1;
                PoolNear[i] = -1;
            }
            NearCount = 0;
            BoundCount = 0;
            _stored = 0;
        }

        // ── the refresh ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Places every bird at the calendar's time, lists the ones near the view nearest first, and
        /// binds them to the pool. <paramref name="places"/> holds the region's authored places first
        /// (<paramref name="authoredCount"/> of them, in a fixed order while the region is loaded), then
        /// any schools.
        /// </summary>
        public void Refresh(GullHauntSpec[] places, int authoredCount, int count, in GullTuningSpec tuning,
                            in GullRosterSettings settings, in GullCalendar calendar, IGullWeather weather,
                            in GullRect view)
        {
            Use(places, authoredCount, count, tuning, settings, calendar, weather);

            _view = view;
            _nearRect = view.Grown(Math.Max(0.0, settings.ShowMarginMetres)).LoweredBottom(Math.Max(0.0, settings.LiftMetres));
            _keepRect = _nearRect.Grown(Math.Max(0.0, settings.ShowHysteresisMetres));

            for (int p = 0; p < _count; p++)
            {
                GullRect box = BoxOf(p);
                bool was = p < _authored && _placeNear[p];
                _placeNear[p] = _places[p].AlwaysOn || box.Overlaps(_nearRect) || (was && box.Overlaps(_keepRect));
            }
            for (int p = _count; p < _placeNear.Length; p++) _placeNear[p] = false;

            NearCount = 0;
            _stored = 0;
            for (int p = 0; p < _count; p++)
            {
                if (!_places[p].AlwaysOn && !CorridorOf(p).Overlaps(_keepRect)) continue;
                SlotRange(p, out int first, out int end);
                for (int slot = first; slot < end; slot++)
                {
                    if (!Evaluate(p, slot, false, out GullRosterBird b)) continue;
                    bool near = b.Where == GullWhere.Commuting
                        ? _nearRect.Contains(b.X, b.Y) || (IsBound(b.Key) && _keepRect.Contains(b.X, b.Y))
                        : b.At >= 0 && _placeNear[b.At];
                    if (!near) continue;

                    double dx = b.X - _view.CentreX, dy = b.Y - _view.CentreY;
                    b.DistanceSq = dx * dx + dy * dy;
                    Insert(b);
                }
            }

            Bind();
        }

        /// <summary>
        /// One slot's state at the last refresh's time, worked out exactly, near or not: the test seam,
        /// and the proof that a bird's place does not depend on the view. False when it is away.
        /// </summary>
        public bool StateOf(int place, int slot, out GullRosterBird bird) => Evaluate(place, slot, true, out bird);

        /// <summary>The schedule's expected birds at a place at the last refresh's time.</summary>
        public double NowExpected(int place) => place >= 0 && place < _count ? _now.Expected[place] : 0.0;

        /// <summary>Today's population of a place at the last refresh's time.</summary>
        public double NowPopulation(int place) => place >= 0 && place < _count ? _now.Population[place] : 0.0;

        /// <summary>Whether a place counted as near at the last refresh.</summary>
        public bool PlaceNear(int place) => place >= 0 && place < _count && _placeNear[place];

        /// <summary>The slots a place has: [first, end).</summary>
        public void SlotRange(int place, out int first, out int end)
        {
            if (place < 0 || place >= _count) { first = end = 0; return; }
            ref readonly GullHauntSpec s = ref _places[place];
            if (s.IsRoost)
            {
                first = ResidentBase;
                end = ResidentBase + Math.Max(0, Math.Min(s.Room, MaxSlotsPerPlace));
                return;
            }
            first = 0;
            end = Math.Max(0, Math.Min(s.Capacity, s.IsSchool ? MaxSchoolSlots : MaxSlotsPerPlace));
        }

        // ── spots ───────────────────────────────────────────────────────────────────────────────

        /// <summary>A visitor's spot at its haunt (or in its school's disc) on a given day.</summary>
        public void HauntSpot(int place, int slot, int day, out double x, out double y)
        {
            bool school = _places[place].IsSchool;
            SpotIn(place, slot, day, school ? GullHash.TagSchoolSpotR : GullHash.TagSpotU,
                   school ? GullHash.TagSchoolSpotA : GullHash.TagSpotV, out x, out y);
        }

        /// <summary>Another spot for the same bird, when its own is no place to put down.
        /// <paramref name="slot"/> may be a whole key: it only feeds the hash, so it wraps.</summary>
        public void AlternateSpot(int place, int slot, int day, int attempt, out double x, out double y)
            => SpotIn(place, unchecked(slot * 8 + attempt), day, GullHash.TagAlternateU, GullHash.TagAlternateV,
                      out x, out y);

        private void SpotIn(int place, int slot, int day, int tagU, int tagV, out double x, out double y)
        {
            ref readonly GullHauntSpec s = ref _places[place];
            int seed = _calendar.Seed;
            // A place that is always on has its birds round the clock: no new spots at midnight.
            if (s.AlwaysOn) day = 0;
            double u = GullHash.Unit(seed, s.IdHash, slot, day, tagU);
            double v = GullHash.Unit(seed, s.IdHash, slot, day, tagV);
            if (s.IsSchool)
            {
                // A disc, not a box: the school is round.
                double r = s.HalfX * Math.Sqrt(u);
                double a = v * (Math.PI * 2.0);
                x = s.CentreX + r * Math.Sin(a);
                y = s.CentreY + r * Math.Cos(a);
                return;
            }
            x = s.CentreX + (2.0 * u - 1.0) * s.HalfX;
            y = s.CentreY + (2.0 * v - 1.0) * s.HalfY;
        }

        /// <summary>A visitor's spot at its roost: the same every night (no day in the key).</summary>
        public void RoostVisitorSpot(int roost, int place, int slot, out double x, out double y)
        {
            ref readonly GullHauntSpec r = ref _places[roost];
            int seed = _calendar.Seed;
            int from = unchecked((int)_places[place].IdHash);
            double u = GullHash.Unit(seed, r.IdHash, from, slot, GullHash.TagRoostSpotU);
            double v = GullHash.Unit(seed, r.IdHash, from, slot, GullHash.TagRoostSpotV);
            x = r.CentreX + (2.0 * u - 1.0) * r.HalfX;
            y = r.CentreY + (2.0 * v - 1.0) * r.HalfY;
        }

        /// <summary>A resident's spot at its roost.</summary>
        public void ResidentSpot(int roost, int resident, out double x, out double y)
        {
            ref readonly GullHauntSpec r = ref _places[roost];
            int seed = _calendar.Seed;
            double u = GullHash.Unit(seed, r.IdHash, resident, 0, GullHash.TagResidentSpotU);
            double v = GullHash.Unit(seed, r.IdHash, resident, 0, GullHash.TagResidentSpotV);
            x = r.CentreX + (2.0 * u - 1.0) * r.HalfX;
            y = r.CentreY + (2.0 * v - 1.0) * r.HalfY;
        }

        /// <summary>Where a bird leaving from a point crosses the region's edge on its place's way out.</summary>
        public void AwayExit(int place, double fromX, double fromY, out double x, out double y)
        {
            ref readonly GullHauntSpec s = ref _places[place];
            double length = ExitLength(fromX, fromY, s.AwayX, s.AwayY, _settings.Region);
            x = fromX + s.AwayX * length;
            y = fromY + s.AwayY * length;
        }

        private static double ExitLength(double x, double y, double dx, double dy, in GullRect region)
        {
            if (region.IsEmpty || (dx == 0.0 && dy == 0.0)) return 0.0;
            double tx = dx > 0.0 ? (region.MaxX - x) / dx : dx < 0.0 ? (region.MinX - x) / dx : double.MaxValue;
            double ty = dy > 0.0 ? (region.MaxY - y) / dy : dy < 0.0 ? (region.MinY - y) / dy : double.MaxValue;
            double t = Math.Min(tx, ty);
            return t > 0.0 && t < double.MaxValue ? t : 0.0;
        }

        // ── one slot ────────────────────────────────────────────────────────────────────────────

        private bool Evaluate(int p, int slot, bool exact, out GullRosterBird bird)
        {
            bird = default;
            if (_places == null || p < 0 || p >= _count) return false;

            bird.Key = KeyOf(p, slot);
            bird.Place = p;
            bird.Slot = slot;
            bird.From = -1;

            double now = _now.Seconds;
            GullWhere wNow = WhereAt(_now, p, slot);

            // The most recent change of place inside the longest flight, at grid resolution. The grid
            // times are absolute, so the cell a change falls in is the same on every refresh.
            Moment hi = _now;
            Moment lo = null;
            GullWhere wLo = wNow;
            for (int j = 0; j < _gridCount; j++)
            {
                if (now - hi.Seconds >= _longestFlightSeconds) break;   // anything older has landed
                Moment g = _grid[j];
                if (g.Seconds >= hi.Seconds) continue;
                GullWhere wg = WhereAt(g, p, slot);
                if (wg != wNow)
                {
                    lo = g;
                    wLo = wg;
                    break;
                }
                hi = g;
            }

            if (lo == null) return Place(ref bird, p, slot, wNow, _now.Inputs.Day);

            Endpoints(p, slot, wLo, lo.Inputs.Day, wNow, hi.Inputs.Day,
                      out double fx, out double fy, out double tx, out double ty, out int from, out int to);
            double speed = _settings.CommuteSpeedMetresPerSecond;
            double length = Math.Sqrt((tx - fx) * (tx - fx) + (ty - fy) * (ty - fy));
            double flight = speed > 0.0 ? length / speed : 0.0;
            if (!(flight > 0.0) || now - hi.Seconds >= flight) return Place(ref bird, p, slot, wNow, _now.Inputs.Day);

            // Not near on any reading (its line misses the view and it is not bound for a near place):
            // no need to know exactly where along the line it is.
            if (!exact && !(to >= 0 && _placeNear[to])
                && !new GullRect(Math.Min(fx, tx), Math.Min(fy, ty), Math.Max(fx, tx), Math.Max(fy, ty)).Overlaps(_keepRect))
                return Place(ref bird, p, slot, wNow, _now.Inputs.Day);

            double depart = Bisect(p, slot, wLo, lo.Seconds, hi.Seconds);
            if (now - depart >= flight) return Place(ref bird, p, slot, wNow, _now.Inputs.Day);

            double u = GullSchedule.Clamp01((now - depart) / flight);
            bird.Where = GullWhere.Commuting;
            bird.At = to;
            bird.From = from;
            bird.FromX = fx;
            bird.FromY = fy;
            bird.ToX = tx;
            bird.ToY = ty;
            bird.DepartSeconds = depart;
            bird.FlightSeconds = flight;
            bird.X = fx + (tx - fx) * u;
            bird.Y = fy + (ty - fy) * u;
            return true;
        }

        /// <summary>The first time inside (lo, hi] at which the slot is no longer where it was at lo.</summary>
        private double Bisect(int p, int slot, GullWhere wLo, double lo, double hi)
        {
            int guard = 64;
            while (hi - lo > SwitchToleranceSeconds && guard-- > 0)
            {
                double mid = 0.5 * (lo + hi);
                Compute(_probe, mid);
                if (WhereAt(_probe, p, slot) == wLo) lo = mid;
                else hi = mid;
            }
            return hi;
        }

        private bool Place(ref GullRosterBird bird, int p, int slot, GullWhere where, int day)
        {
            bird.Where = where;
            bird.DepartSeconds = 0.0;
            bird.FlightSeconds = 0.0;
            if (where == GullWhere.Away)
            {
                bird.At = -1;
                return false;
            }
            SpotOf(p, slot, where, day, out double x, out double y, out int at);
            bird.At = at;
            bird.X = bird.FromX = bird.ToX = x;
            bird.Y = bird.FromY = bird.ToY = y;
            return true;
        }

        private void SpotOf(int p, int slot, GullWhere where, int day, out double x, out double y, out int at)
        {
            ref readonly GullHauntSpec s = ref _places[p];
            if (where == GullWhere.AtHaunt)
            {
                HauntSpot(p, slot, day, out x, out y);
                at = p;
                return;
            }
            if (s.IsRoost)
            {
                ResidentSpot(p, slot - ResidentBase, out x, out y);
                at = p;
                return;
            }
            RoostVisitorSpot(s.RoostIndex, p, slot, out x, out y);
            at = s.RoostIndex;
        }

        private void Endpoints(int p, int slot, GullWhere wFrom, int dayFrom, GullWhere wTo, int dayTo,
                               out double fx, out double fy, out double tx, out double ty, out int from, out int to)
        {
            fx = fy = tx = ty = 0.0;
            from = to = -1;
            if (wFrom != GullWhere.Away) SpotOf(p, slot, wFrom, dayFrom, out fx, out fy, out from);
            if (wTo != GullWhere.Away) SpotOf(p, slot, wTo, dayTo, out tx, out ty, out to);
            // Going away it flies out along its place's way out; coming back it flies in along the same line.
            if (wFrom == GullWhere.Away) AwayExit(p, tx, ty, out fx, out fy);
            if (wTo == GullWhere.Away) AwayExit(p, fx, fy, out tx, out ty);
        }

        private GullWhere WhereAt(Moment m, int p, int slot)
        {
            ref readonly GullHauntSpec s = ref _places[p];
            int seed = _calendar.Seed;
            if (s.IsRoost)
            {
                int j = slot - ResidentBase;
                double rj = GullHash.Unit(seed, s.IdHash, j, 0, GullHash.TagResident);
                return m.Residents[p] > j + rj ? GullWhere.AtRoost : GullWhere.Away;
            }

            double threshold = GullSchedule.SlotThreshold(seed, s.IdHash, slot, m.Inputs.Day);
            if (m.Expected[p] > threshold) return GullWhere.AtHaunt;

            int roost = s.RoostIndex;
            if (roost >= 0 && roost < _count && m.Population[p] > threshold)
            {
                // No day in the key: which absent birds are home must not reshuffle at midnight, when
                // a full roost would otherwise trade half its sleepers.
                double home = GullHash.Unit(seed, s.IdHash, slot, 0, GullHash.TagHome);
                if (home < m.Share * m.CapShare[roost]) return GullWhere.AtRoost;
            }
            return GullWhere.Away;
        }

        // ── the schedule at a moment ────────────────────────────────────────────────────────────

        private void Use(GullHauntSpec[] places, int authoredCount, int count, in GullTuningSpec tuning,
                         in GullRosterSettings settings, in GullCalendar calendar, IGullWeather weather)
        {
            _places = places;
            _count = places == null ? 0 : Math.Max(0, Math.Min(Math.Min(count, places.Length), _maxPlaces));
            _authored = Math.Max(0, Math.Min(authoredCount, _count));
            _tuning = tuning;
            _settings = settings;
            _calendar = calendar;
            _weather = weather;

            GullRect region = settings.Region;
            double speed = settings.CommuteSpeedMetresPerSecond;
            double diagonal = region.IsEmpty
                ? 0.0
                : Math.Sqrt((region.MaxX - region.MinX) * (region.MaxX - region.MinX)
                            + (region.MaxY - region.MinY) * (region.MaxY - region.MinY));
            _longestFlightSeconds = speed > 0.0 ? diagonal / speed : 0.0;

            int need = _longestFlightSeconds > 0.0 ? (int)Math.Ceiling(_longestFlightSeconds / LookbackStepSeconds) + 2 : 0;
            if (_grid.Length < need)
            {
                var grown = new Moment[need];
                Array.Copy(_grid, grown, _grid.Length);
                for (int i = _grid.Length; i < need; i++) grown[i] = new Moment(_maxPlaces);
                _grid = grown;
            }
            _gridCount = need;

            double now = calendar.Seconds;
            Compute(_now, now);
            double g0 = Math.Floor(now / LookbackStepSeconds) * LookbackStepSeconds;
            for (int j = 0; j < _gridCount; j++) Compute(_grid[j], g0 - j * LookbackStepSeconds);
        }

        private void Compute(Moment m, double seconds)
        {
            m.Seconds = seconds;
            m.Inputs = GullSchedule.InputsAt(_tuning, _calendar, _weather, seconds);
            m.Share = GullSchedule.RoostShare(_tuning, m.Inputs.Hour, m.Inputs.Storm, m.Inputs.Visibility);

            for (int i = 0; i < _count; i++)
            {
                _popSum[i] = 0.0;
                _awaySum[i] = 0.0;
            }
            for (int i = 0; i < _count; i++)
            {
                ref readonly GullHauntSpec s = ref _places[i];
                double pop = GullSchedule.Population(_tuning, s, m.Inputs);
                double e = GullSchedule.Expected(_tuning, s, m.Inputs);
                m.Population[i] = pop;
                m.Expected[i] = e;
                int r = s.RoostIndex;
                if (!s.IsRoost && r >= 0 && r < _count)
                {
                    _popSum[r] += pop;
                    _awaySum[r] += Math.Max(0.0, pop - e);
                }
            }
            for (int i = 0; i < _count; i++)
            {
                ref readonly GullHauntSpec s = ref _places[i];
                if (!s.IsRoost)
                {
                    m.CapShare[i] = 1.0;
                    m.Residents[i] = 0.0;
                    continue;
                }
                // The colony first, then as many visitors as the ledges still hold: the expected
                // occupancy comes out at exactly min(Room, residents + visitors).
                double residents = Math.Min(s.Room, s.Residents[m.Inputs.Season] * _popSum[i]);
                double visitors = _awaySum[i] * m.Share;
                m.Residents[i] = residents;
                m.CapShare[i] = visitors > 0.0 ? GullSchedule.Clamp01((s.Room - residents) / visitors) : 1.0;
            }
        }

        // ── near and the pool ───────────────────────────────────────────────────────────────────

        private GullRect BoxOf(int p)
        {
            ref readonly GullHauntSpec s = ref _places[p];
            double hy = s.IsSchool ? s.HalfX : s.HalfY;
            return GullRect.Box(s.CentreX, s.CentreY, s.HalfX, hy);
        }

        /// <summary>Everywhere a place's birds can be: its box, its roost's box, and the way out.</summary>
        private GullRect CorridorOf(int p)
        {
            ref readonly GullHauntSpec s = ref _places[p];
            GullRect box = BoxOf(p);
            GullRect corridor = box;
            if (!s.IsRoost && s.RoostIndex >= 0 && s.RoostIndex < _count) corridor = corridor.With(BoxOf(s.RoostIndex));

            GullRect region = _settings.Region;
            if (!region.IsEmpty && (s.AwayX != 0.0 || s.AwayY != 0.0))
            {
                double reach = (region.MaxX - region.MinX) + (region.MaxY - region.MinY);
                corridor = corridor.With(box.MinX + s.AwayX * reach, box.MinY + s.AwayY * reach)
                                   .With(box.MaxX + s.AwayX * reach, box.MaxY + s.AwayY * reach);
                if (!s.IsRoost && s.RoostIndex >= 0 && s.RoostIndex < _count)
                {
                    GullRect roostBox = BoxOf(s.RoostIndex);
                    corridor = corridor.With(roostBox.MinX + s.AwayX * reach, roostBox.MinY + s.AwayY * reach)
                                       .With(roostBox.MaxX + s.AwayX * reach, roostBox.MaxY + s.AwayY * reach);
                }
            }
            return corridor;
        }

        private int KeyOf(int p, int slot)
        {
            ref readonly GullHauntSpec s = ref _places[p];
            if (s.IsSchool) return SchoolKeyBit | (int)((s.IdHash & 0xFFFFFu) << 6) | (slot & 63);
            return ((p + 1) << AuthoredKeyShift) | slot;
        }

        private bool IsBound(int key)
        {
            for (int i = 0; i < PoolKey.Length; i++)
                if (PoolKey[i] == key) return true;
            return false;
        }

        private static bool NearerThan(in GullRosterBird a, in GullRosterBird b)
            => a.DistanceSq < b.DistanceSq || (a.DistanceSq == b.DistanceSq && a.Key < b.Key);

        /// <summary>Keeps the nearest, in order; every near bird is counted whether it is kept or not.</summary>
        private void Insert(in GullRosterBird bird)
        {
            NearCount++;
            int n = _stored;
            if (n == Near.Length)
            {
                if (!NearerThan(bird, Near[n - 1])) return;
                n--;
            }
            else
            {
                _stored++;
            }
            int i = n;
            while (i > 0 && NearerThan(bird, Near[i - 1]))
            {
                Near[i] = Near[i - 1];
                i--;
            }
            Near[i] = bird;
        }

        /// <summary>
        /// Sticky binding: a pool bird keeps its slot for as long as the slot stays near; a newly near
        /// slot takes the lowest free pool bird, nearest first; the rest are counted, not drawn.
        /// </summary>
        private void Bind()
        {
            for (int i = 0; i < PoolNear.Length; i++) PoolNear[i] = -1;
            for (int n = 0; n < _stored; n++)
            {
                NearPool[n] = -1;
                int key = Near[n].Key;
                for (int i = 0; i < PoolKey.Length; i++)
                {
                    if (PoolKey[i] != key) continue;
                    PoolNear[i] = n;
                    NearPool[n] = i;
                    break;
                }
            }
            for (int i = 0; i < PoolKey.Length; i++)
                if (PoolNear[i] < 0) PoolKey[i] = -1;

            int free = 0;
            for (int n = 0; n < _stored; n++)
            {
                if (NearPool[n] >= 0) continue;
                while (free < PoolKey.Length && PoolKey[free] != -1) free++;
                if (free == PoolKey.Length) break;
                PoolKey[free] = Near[n].Key;
                PoolNear[free] = n;
                NearPool[n] = free;
            }

            int bound = 0;
            for (int i = 0; i < PoolKey.Length; i++)
                if (PoolKey[i] != -1) bound++;
            BoundCount = bound;
        }
    }
}
