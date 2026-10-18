using System;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Four numbers, one per season, in the clock's order: Early Spring, High Summer, The Turn, Hard
    /// Winter (<c>Season</c> 0..3).
    /// </summary>
    public struct GullSeasons
    {
        public double EarlySpring, HighSummer, TheTurn, HardWinter;

        public GullSeasons(double earlySpring, double highSummer, double theTurn, double hardWinter)
        {
            EarlySpring = earlySpring;
            HighSummer = highSummer;
            TheTurn = theTurn;
            HardWinter = hardWinter;
        }

        /// <summary>The weight for a season index, wrapped into 0..3.</summary>
        public double this[int season]
        {
            get
            {
                switch (((season % 4) + 4) % 4)
                {
                    case 0: return EarlySpring;
                    case 1: return HighSummer;
                    case 2: return TheTurn;
                    default: return HardWinter;
                }
            }
        }

        /// <summary>The largest of the four.</summary>
        public double Max => Math.Max(Math.Max(EarlySpring, HighSummer), Math.Max(TheTurn, HardWinter));
    }

    /// <summary>
    /// The global knobs of the gull schedule as plain numbers (built from
    /// <see cref="GullScheduleTuning"/>, plus the sky's sunrise and sunset). Every field is the
    /// owner's to tune; the meanings are on <see cref="GullScheduleTuning"/>.
    /// </summary>
    public struct GullTuningSpec
    {
        public double SunriseHour, SunsetHour;
        public double DawnLeadHours, DawnFullHours;
        public double MorningEndHour, MiddayStartHour, MiddayEndHour;
        public double AfternoonLeadHours, EveningTailHours;
        public double CalmStormMax, GaleStorm, ShelterKeep, GaleTellHours;
        public double FogClearVisibility, FogThickVisibility, FogKeep;
        public double RoostLoafShare, FogRoostShare;
        public double PopulationHeadroom, DayJitter;
        public double YoungShare, YoungFromProgress, YoungLeaveByEnd;
        public double SchoolBirds;
        public GullSeasons SchoolSeason;
        public double SchoolMiddayLevel, SchoolAfternoonLevel;
    }

    /// <summary>
    /// One place as the schedule reads it: plain numbers and a hashed id, nothing from the engine. An
    /// authored place comes from <c>BirdHauntDef.ToSpec</c> (the place's fields plus ONE species
    /// entry); a surface school is a place the flock makes up while the school shows.
    /// </summary>
    public struct GullHauntSpec
    {
        /// <summary><see cref="GullHash.Id"/> of the place's id: every hash about the place is keyed by
        /// it, so its birds and spots never depend on where it sits in a list.</summary>
        public uint IdHash;
        public BirdHauntKind Kind;

        /// <summary>A surface school standing in for a place for as long as it shows.</summary>
        public bool IsSchool;

        /// <summary>A fixture's pinned place: always near, always holding all its birds.</summary>
        public bool AlwaysOn;

        /// <summary>The box on the ground, metres: centre ± half-size.</summary>
        public double CentreX, CentreY, HalfX, HalfY;

        /// <summary>The roost's index in the same place array, or −1.</summary>
        public int RoostIndex;

        public bool Sheltered;
        public double BusyFromHour, BusyToHour, BusyBoost;
        public bool BusyOnFineDaysOnly;
        public double MarketDayBoost;
        public bool HasTide;
        public double TideOpensBelowMetres, TideRampMetres, HighWaterKeep;
        public double MiddayLevel, AfternoonLevel;

        /// <summary>The way out of the region (a unit vector): a roost's own heading, and for a haunt or
        /// a school its roost's.</summary>
        public double AwayX, AwayY;

        // ── the species entry ───────────────────────────────────────────────────────────────────
        public int Capacity;
        public double Typical;
        public GullSeasons SeasonWeights;
        public GullSeasons Residents;
        public int Room;

        /// <summary>A school's window, game seconds: it shows from Start until End.</summary>
        public double SchoolStartSeconds, SchoolEndSeconds;

        public bool IsRoost => Kind == BirdHauntKind.Roost && !IsSchool;
    }

    /// <summary>The world at one moment, as the schedule reads it.</summary>
    public struct GullScheduleInputs
    {
        public int Seed;

        /// <summary>Game seconds (the master clock's frame).</summary>
        public double Seconds;

        /// <summary>Days since the start, 0-based: keys the per-day hashes.</summary>
        public int Day;

        /// <summary>0..24.</summary>
        public double Hour;

        /// <summary>0..3, Early Spring to Hard Winter.</summary>
        public int Season;

        /// <summary>How far through the season, 0..1: (DayOfSeason − 1) / days per season.</summary>
        public double Progress;

        public bool MarketDay;

        /// <summary>Sea state 0..1, already read ahead by the gale's tell.</summary>
        public double Storm;

        /// <summary>0..1.</summary>
        public double Visibility;

        /// <summary>Metres about the region's mean.</summary>
        public double Tide;
    }

    /// <summary>
    /// The weather at an arbitrary game time, for the schedule: the presenter answers it from the
    /// environment service, a test from a table. Both values are pure functions of the time.
    /// </summary>
    public interface IGullWeather
    {
        /// <summary>Sea state 0..1 at this game time.</summary>
        double SeaStateAt(double gameSeconds);

        /// <summary>Tide height, metres, at this game time.</summary>
        double TideAt(double gameSeconds);
    }

    /// <summary>
    /// The calendar at "now", plus what the schedule needs to roll it to a nearby time: a flight looks
    /// back a minute or two, and a minute before midnight is yesterday.
    /// </summary>
    public struct GullCalendar
    {
        public int Seed;
        public double Seconds;
        public double SecondsPerHour;
        public double Hour;
        public int Day;
        public int Season;

        /// <summary>1-based, like the clock's.</summary>
        public int DayOfSeason;
        public int DaysPerSeason;

        /// <summary>0-based, like the clock's.</summary>
        public int Weekday;
        public int DaysPerWeek;
        public int MarketDayIndex;

        /// <summary>Held at now for every time asked about: there is no visibility-at-a-time read, and
        /// fog moves slowly against a flight.</summary>
        public double Visibility;
    }

    /// <summary>
    /// THE GULL SCHEDULE — how many gulls each place holds at a moment, from the hour, the season, the
    /// weather, the tide and the place's own working hours. Pure arithmetic: no engine, no clock, no
    /// draws. The formulas are the Phase A reference's (<c>schedule-reference.mjs</c>), line for line.
    ///
    /// <para><b>Expected birds at a haunt</b> =
    /// min(Population, Typical × J × DayShape × Season × Young × Wind × Fog × Tide × Busy × Market),
    /// and <b>Population</b> = min(Capacity, Typical × J × Season × Young × Headroom × Market). J is
    /// the place's day jitter. A roost holds its residents plus the share of absent birds that has gone
    /// home, up to its room.</para>
    /// </summary>
    public static class GullSchedule
    {
        public static double Clamp01(double x) => x < 0.0 ? 0.0 : x > 1.0 ? 1.0 : x;

        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>The smoothstep of the reference: 0 below 0, 1 above 1, eased between.</summary>
        public static double Smooth(double x)
        {
            double t = Clamp01(x);
            return t * t * (3.0 - 2.0 * t);
        }

        // ── the factors ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The day's shape: a straight-line curve through (sunrise − lead, 0), (sunrise + full, 1),
        /// (morning end, 1), (midday start, midday), (midday end, midday), (sunset − afternoon lead,
        /// afternoon), (sunset + tail, 0); 0 outside them, so at night every bird is at a roost.
        /// </summary>
        public static double DayShape(in GullTuningSpec t, double hour, double middayLevel, double afternoonLevel)
        {
            double h0 = t.SunriseHour - t.DawnLeadHours;
            double h6 = t.SunsetHour + t.EveningTailHours;
            if (hour <= h0 || hour >= h6) return 0.0;

            double h1 = t.SunriseHour + t.DawnFullHours;
            double h2 = t.MorningEndHour;
            double h3 = t.MiddayStartHour;
            double h4 = t.MiddayEndHour;
            double h5 = t.SunsetHour - t.AfternoonLeadHours;

            if (hour <= h1) return Segment(h0, 0.0, h1, 1.0, hour);
            if (hour <= h2) return Segment(h1, 1.0, h2, 1.0, hour);
            if (hour <= h3) return Segment(h2, 1.0, h3, middayLevel, hour);
            if (hour <= h4) return Segment(h3, middayLevel, h4, middayLevel, hour);
            if (hour <= h5) return Segment(h4, middayLevel, h5, afternoonLevel, hour);
            return Segment(h5, afternoonLevel, h6, 0.0, hour);
        }

        private static double Segment(double h0, double v0, double h1, double v1, double hour)
        {
            double span = h1 - h0;
            return span <= 0.0 ? v1 : Lerp(v0, v1, (hour - h0) / span);
        }

        /// <summary>The young of the year: +YoungShare from halfway through High Summer, half of them
        /// gone by the end of The Turn; 1 in the other two seasons.</summary>
        public static double Young(in GullTuningSpec t, int season, double progress)
        {
            if (season == 1)
            {
                double span = 1.0 - t.YoungFromProgress;
                double rise = span <= 0.0 ? (progress >= t.YoungFromProgress ? 1.0 : 0.0)
                                          : Clamp01((progress - t.YoungFromProgress) / span);
                return 1.0 + t.YoungShare * rise;
            }
            if (season == 2) return 1.0 + t.YoungShare * (1.0 - t.YoungLeaveByEnd * progress);
            return 1.0;
        }

        /// <summary>1 up to the calm limit, falling straight to the keep at a gale: 0 for an exposed
        /// place, ShelterKeep for a sheltered one.</summary>
        public static double WindFactor(in GullTuningSpec t, double storm, bool sheltered)
        {
            double keep = sheltered ? t.ShelterKeep : 0.0;
            if (storm <= t.CalmStormMax) return 1.0;
            if (storm >= t.GaleStorm) return keep;
            return Lerp(1.0, keep, (storm - t.CalmStormMax) / (t.GaleStorm - t.CalmStormMax));
        }

        /// <summary>1 in clear air, falling straight to FogKeep in thick fog.</summary>
        public static double FogFactor(in GullTuningSpec t, double visibility)
        {
            if (visibility >= t.FogClearVisibility) return 1.0;
            if (visibility <= t.FogThickVisibility) return t.FogKeep;
            return Lerp(t.FogKeep, 1.0,
                        (visibility - t.FogThickVisibility) / (t.FogClearVisibility - t.FogThickVisibility));
        }

        /// <summary>How far into a gale the storm level is, 0..1.</summary>
        public static double WindDepth(in GullTuningSpec t, double storm)
        {
            double span = t.GaleStorm - t.CalmStormMax;
            return span <= 0.0 ? (storm >= t.GaleStorm ? 1.0 : 0.0) : Clamp01((storm - t.CalmStormMax) / span);
        }

        /// <summary>How thick the fog is, 0 (clear) to 1 (thick).</summary>
        public static double FogDepth(in GullTuningSpec t, double visibility)
        {
            double span = t.FogClearVisibility - t.FogThickVisibility;
            return span <= 0.0 ? (visibility <= t.FogThickVisibility ? 1.0 : 0.0)
                               : 1.0 - Clamp01((visibility - t.FogThickVisibility) / span);
        }

        /// <summary>Tidal places open as the tide falls: HighWaterKeep at high water, eased up to 1 once
        /// the tide is Ramp below the opening height.</summary>
        public static double TideFactor(in GullHauntSpec p, double tide)
        {
            if (!p.HasTide) return 1.0;
            double opened = p.TideRampMetres > 0.0
                ? Smooth((p.TideOpensBelowMetres - tide) / p.TideRampMetres)
                : (tide <= p.TideOpensBelowMetres ? 1.0 : 0.0);
            return Lerp(p.HighWaterKeep, 1.0, opened);
        }

        /// <summary>A fine day: calm enough and clear enough.</summary>
        public static bool IsFine(in GullTuningSpec t, double storm, double visibility)
            => storm <= t.CalmStormMax && visibility >= t.FogClearVisibility;

        /// <summary>Inside the place's working hours, on a day it works (a fine-days-only place does not
        /// work in wind or fog).</summary>
        public static bool IsBusy(in GullTuningSpec t, in GullHauntSpec p, double hour, double storm, double visibility)
        {
            if (!(p.BusyBoost > 0.0) || hour < p.BusyFromHour || hour >= p.BusyToHour) return false;
            return !p.BusyOnFineDaysOnly || IsFine(t, storm, visibility);
        }

        /// <summary>BusyBoost inside the working hours, 1 outside them.</summary>
        public static double BusyFactor(in GullTuningSpec t, in GullHauntSpec p, double hour, double storm, double visibility)
            => IsBusy(t, p, hour, storm, visibility) ? p.BusyBoost : 1.0;

        /// <summary>MarketDayBoost on market day, 1 otherwise (and 1 for a place with no boost).</summary>
        public static double MarketFactor(in GullHauntSpec p, bool marketDay)
            => marketDay && p.MarketDayBoost > 0.0 ? p.MarketDayBoost : 1.0;

        /// <summary>
        /// J, the place's day jitter: 1 ± DayJitter, hashed from (seed, place, day). Some days the plant
        /// draws a crowd and some days fewer; the owner's table is read at J = 1.
        /// </summary>
        public static double Jitter(in GullTuningSpec t, int seed, uint placeHash, int day)
            => 1.0 + t.DayJitter * (2.0 * GullHash.Unit(seed, placeHash, day, 0, GullHash.TagJitter) - 1.0);

        /// <summary>
        /// The count at which slot <paramref name="slot"/> of a place is filled that day: slot + r, with
        /// r in [0, 1) hashed from (seed, place, slot, day). A slot is filled while the count is above
        /// its threshold. The thresholds rise with the slot, so the filled slots are always the first
        /// few: as a count rises the birds arrive one at a time, and as it falls they leave in the
        /// reverse order.
        /// </summary>
        public static double SlotThreshold(int seed, uint placeHash, int slot, int day)
            => slot + GullHash.Unit(seed, placeHash, slot, day, GullHash.TagArrive);

        // ── the counts ──────────────────────────────────────────────────────────────────────────

        /// <summary>Today's population of a haunt: every bird that uses it today, wherever it is at
        /// this hour. A roost has none of its own (see <see cref="ResidentsExpected"/>); a school has
        /// no population off the water.</summary>
        public static double Population(in GullTuningSpec t, in GullHauntSpec p, in GullScheduleInputs x)
        {
            if (p.IsRoost || p.IsSchool) return 0.0;
            if (p.AlwaysOn) return p.Capacity;
            double j = Jitter(t, x.Seed, p.IdHash, x.Day);
            double n = p.Typical * j * p.SeasonWeights[x.Season] * Young(t, x.Season, x.Progress)
                     * t.PopulationHeadroom * MarketFactor(p, x.MarketDay);
            return Math.Min(p.Capacity, n);
        }

        /// <summary>Expected birds AT the place at this moment (a roost's are <see cref="RoostCount"/>).</summary>
        public static double Expected(in GullTuningSpec t, in GullHauntSpec p, in GullScheduleInputs x)
        {
            if (p.IsRoost) return 0.0;
            if (p.AlwaysOn) return p.Capacity;
            if (p.IsSchool) return SchoolBirds(t, p, x);

            double j = Jitter(t, x.Seed, p.IdHash, x.Day);
            double e = p.Typical * j * DayShape(t, x.Hour, p.MiddayLevel, p.AfternoonLevel)
                     * p.SeasonWeights[x.Season] * Young(t, x.Season, x.Progress)
                     * WindFactor(t, x.Storm, p.Sheltered) * FogFactor(t, x.Visibility) * TideFactor(p, x.Tide)
                     * BusyFactor(t, p, x.Hour, x.Storm, x.Visibility) * MarketFactor(p, x.MarketDay);
            return Math.Min(Population(t, p, x), e);
        }

        /// <summary>A surface school's birds: SchoolBirds × DayShape (open water) × SchoolSeason ×
        /// Wind (exposed) × Fog, and none outside the school's window.</summary>
        public static double SchoolBirds(in GullTuningSpec t, in GullHauntSpec p, in GullScheduleInputs x)
        {
            if (x.Seconds < p.SchoolStartSeconds || x.Seconds >= p.SchoolEndSeconds) return 0.0;
            return SchoolBirds(t, x.Season, x.Hour, x.Storm, x.Visibility);
        }

        /// <summary>The same, for a school that is showing.</summary>
        public static double SchoolBirds(in GullTuningSpec t, int season, double hour, double storm, double visibility)
            => t.SchoolBirds * DayShape(t, hour, t.SchoolMiddayLevel, t.SchoolAfternoonLevel)
             * t.SchoolSeason[season] * WindFactor(t, storm, false) * FogFactor(t, visibility);

        /// <summary>Does a school draw birds? Only one at the surface: no deeper than the surface depth,
        /// the depth the player's own eyes stop at. The gulls work the fish anyone can see.</summary>
        public static bool SchoolShows(double depthMetres, double surfaceDepthMetres) => depthMetres <= surfaceDepthMetres;

        /// <summary>
        /// The share of a place's birds that sits: the fine-day share, raised toward the fog's as the air
        /// thickens and toward the gale's as the wind rises. With the gale's share at 1, a full gale has
        /// every bird on its feet: nobody wheels.
        /// </summary>
        public static double SettledShare(in GullTuningSpec t, double calmShare, double fogShare, double galeShare,
                                          double storm, double visibility)
        {
            double calm = Clamp01(calmShare);
            double fog = Lerp(calm, Clamp01(fogShare), FogDepth(t, visibility));
            double gale = Lerp(calm, Clamp01(galeShare), WindDepth(t, storm));
            return Math.Max(calm, Math.Max(fog, gale));
        }

        /// <summary>
        /// Of the birds away from their haunt, the share that is AT THE ROOST: 1 at night, falling to
        /// RoostLoafShare over the dawn hour, climbing back through the evening return; toward 1 as
        /// the wind rises and toward FogRoostShare in fog.
        /// </summary>
        public static double RoostShare(in GullTuningSpec t, double hour, double storm, double visibility)
        {
            double s = t.RoostLoafShare;
            double dawnFrom = t.SunriseHour, dawnTo = t.SunriseHour + t.DawnFullHours;
            double duskFrom = t.SunsetHour - t.AfternoonLeadHours, duskTo = t.SunsetHour + t.EveningTailHours;

            double day;
            if (hour <= dawnFrom || hour >= duskTo) day = 1.0;
            else if (hour < dawnTo) day = Lerp(1.0, s, (hour - dawnFrom) / (dawnTo - dawnFrom));
            else if (hour <= duskFrom) day = s;
            else day = Lerp(s, 1.0, (hour - duskFrom) / (duskTo - duskFrom));

            double wind = Lerp(s, 1.0, WindDepth(t, storm));
            double fog = Lerp(s, t.FogRoostShare, FogDepth(t, visibility));
            return Math.Max(day, Math.Max(wind, fog));
        }

        /// <summary>The roost's own colony: its residents' share of its haunts' population, up to its
        /// room.</summary>
        public static double ResidentsExpected(in GullTuningSpec t, GullHauntSpec[] places, int count,
                                               int roost, in GullScheduleInputs x)
        {
            if (places == null || roost < 0 || roost >= count || !places[roost].IsRoost) return 0.0;
            double pop = 0.0;
            for (int i = 0; i < count; i++)
                if (places[i].RoostIndex == roost && !places[i].IsRoost) pop += Population(t, places[i], x);
            return Math.Min(places[roost].Room, places[roost].Residents[x.Season] * pop);
        }

        /// <summary>
        /// The birds at a roost: min(Room, Residents × ΣPopulation + Σ(Population − Expected) ×
        /// RoostShare), over the haunts that roost there. Past its room, birds roost off the map.
        /// </summary>
        public static double RoostCount(in GullTuningSpec t, GullHauntSpec[] places, int count,
                                        int roost, in GullScheduleInputs x)
        {
            if (places == null || roost < 0 || roost >= count || !places[roost].IsRoost) return 0.0;
            double pop = 0.0, away = 0.0;
            for (int i = 0; i < count; i++)
            {
                if (places[i].RoostIndex != roost || places[i].IsRoost) continue;
                double n = Population(t, places[i], x);
                pop += n;
                away += Math.Max(0.0, n - Expected(t, places[i], x));
            }
            double share = RoostShare(t, x.Hour, x.Storm, x.Visibility);
            return Math.Min(places[roost].Room, places[roost].Residents[x.Season] * pop + away * share);
        }

        // ── the calendar at a nearby time ───────────────────────────────────────────────────────

        /// <summary>
        /// The schedule's inputs at <paramref name="seconds"/>: the calendar rolled from now's by whole
        /// days, the storm read ahead by the gale's tell, the tide at that time, the visibility held at
        /// now's. Pure: the same calendar, weather and time give the same inputs.
        /// </summary>
        public static GullScheduleInputs InputsAt(in GullTuningSpec t, in GullCalendar c, IGullWeather weather,
                                                  double seconds)
        {
            double secondsPerHour = c.SecondsPerHour > 0.0 ? c.SecondsPerHour : 1.0;
            double hours = c.Hour + (seconds - c.Seconds) / secondsPerHour;
            int dayOffset = (int)Math.Floor(hours / 24.0);
            double hour = hours - 24.0 * dayOffset;
            if (hour < 0.0) hour = 0.0;
            if (hour >= 24.0) hour = 0.0;

            int daysPerSeason = c.DaysPerSeason > 0 ? c.DaysPerSeason : 1;
            int intoSeason = c.DayOfSeason - 1 + dayOffset;
            int seasonsOn = FloorDiv(intoSeason, daysPerSeason);
            int dayOfSeason = intoSeason - seasonsOn * daysPerSeason + 1;
            int daysPerWeek = c.DaysPerWeek > 0 ? c.DaysPerWeek : 1;

            var x = new GullScheduleInputs
            {
                Seed = c.Seed,
                Seconds = seconds,
                Day = c.Day + dayOffset,
                Hour = hour,
                Season = Mod(c.Season + seasonsOn, 4),
                Progress = (dayOfSeason - 1) / (double)daysPerSeason,
                MarketDay = Mod(c.Weekday + dayOffset, daysPerWeek) == c.MarketDayIndex,
                Visibility = c.Visibility,
            };

            if (weather != null)
            {
                double tell = t.GaleTellHours * secondsPerHour;
                x.Storm = Clamp01(Math.Max(weather.SeaStateAt(seconds), weather.SeaStateAt(seconds + tell)));
                x.Tide = weather.TideAt(seconds);
            }
            return x;
        }

        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        public static int Mod(int a, int n) => ((a % n) + n) % n;
    }

    /// <summary>
    /// The schedule's hashes: FNV-1a over the ids and the integers, finished by the rig's own
    /// mulberry32 step (<see cref="SeagullFlockMath.Mulberry32"/>). A hash is a pure function of what
    /// it is keyed by — the same place, slot and day give the same number on every machine, every run.
    /// </summary>
    public static class GullHash
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        // One stream per question, so no two questions about the same bird share a number.
        public const int TagJitter = 1;
        public const int TagArrive = 2;
        public const int TagHome = 3;
        public const int TagSpotU = 4;
        public const int TagSpotV = 5;
        public const int TagResident = 6;
        public const int TagRoostSpotU = 7;
        public const int TagRoostSpotV = 8;
        public const int TagAlternateU = 9;
        public const int TagAlternateV = 10;
        public const int TagSchoolSpotR = 11;
        public const int TagSchoolSpotA = 12;
        public const int TagResidentSpotU = 13;
        public const int TagResidentSpotV = 14;

        /// <summary>FNV-1a of an id string (its UTF-16 code units; ids are ASCII). Null hashes as empty.</summary>
        public static uint Id(string id)
        {
            unchecked
            {
                uint h = FnvOffset;
                if (id != null)
                    for (int i = 0; i < id.Length; i++)
                    {
                        h ^= id[i];
                        h *= FnvPrime;
                    }
                return h;
            }
        }

        /// <summary>Folds one integer into a running FNV-1a hash, a byte at a time.</summary>
        public static uint Mix(uint h, int value)
        {
            unchecked
            {
                uint v = (uint)value;
                h ^= v & 0xFF; h *= FnvPrime;
                h ^= (v >> 8) & 0xFF; h *= FnvPrime;
                h ^= (v >> 16) & 0xFF; h *= FnvPrime;
                h ^= v >> 24; h *= FnvPrime;
                return h;
            }
        }

        /// <summary>A number in [0, 1) keyed by the seed, a place, two integers and a stream tag.</summary>
        public static double Unit(int seed, uint placeHash, int a, int b, int tag)
        {
            unchecked
            {
                uint h = Mix(FnvOffset, seed);
                h = Mix(h, (int)placeHash);
                h = Mix(h, a);
                h = Mix(h, b);
                h = Mix(h, tag);
                return SeagullFlockMath.Mulberry32.FromState(h).Next();
            }
        }
    }
}
