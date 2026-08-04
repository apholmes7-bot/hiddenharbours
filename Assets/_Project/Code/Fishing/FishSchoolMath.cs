using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE PURE MATHS OF WHERE THE FISH ARE (ADR 0025 S3, the owner's ruling of 2026-08-03) — every
    /// decision that turns <c>(worldSeed, gameTime, place, weather, season)</c> into a
    /// <see cref="FishSchool"/>, with nothing on the other side of it.
    ///
    /// <para><b>Recomputed, never saved</b> (rule 5) — the tide's own discipline, for the same reason. A
    /// school is not a spawned entity with a lifetime and a timer; it is a <i>question you can ask of any
    /// place at any time</i> and get the same answer to, this run and every future run, on any machine,
    /// with nothing stored between. Two players on one seed standing in the same water at the same instant
    /// see the same fish. That is what makes the whole thing EditMode-testable with no scene, no
    /// <c>Update</c> and no spawner — and it is why there is no <c>SaveData</c> field naming a school and
    /// no DTO for one.</para>
    ///
    /// <para><b>The lattice.</b> The world is diced into square CELLS
    /// (<see cref="FishSchoolSettings.CellSizeMetres"/>) and time into SLOTS
    /// (<see cref="FishSchoolSettings.SlotHours"/>). Each <c>(cell, slot)</c> pair is one independent
    /// coin-flip: does this patch of water hold fish during this stretch of the day? Everything about the
    /// school that answers "yes" — where in the cell it sits, how wide, how deep, how many, which species,
    /// and the exact window inside the slot — is drawn from ONE hash of
    /// <c>(worldSeed, regionId, cellX, cellY, slot)</c> via independent numbered streams. No
    /// <c>System.Random</c>, no <c>UnityEngine.Random</c>, no accumulator: hash in, school out (the
    /// <c>WeatherModel</c> rule).</para>
    ///
    /// <para><b>Two invariants the search relies on</b>, both enforced by <see cref="Sanitized"/> rather
    /// than trusted:
    /// <list type="number">
    /// <item>a school's radius never exceeds one cell, so a query only ever has to look at the 3×3
    /// neighbourhood around its own cell — a bigger school would be invisible from its own outer ring;</item>
    /// <item>a school's window never leaves its slot, so a query only ever has to look at ONE slot. Both
    /// keep the read O(9) and allocation-free (rule 7).</item>
    /// </list></para>
    ///
    /// <para><b>Engine-light</b> (<see cref="Vector2"/>/<see cref="Mathf"/> only) and side-effect free, the
    /// <see cref="DepthDropMath"/>/<c>RodFightMath</c> lane discipline: no <c>Time</c>, no scene, no RNG,
    /// nothing saved, every constant a passed parameter backed by
    /// <see cref="FishSchoolSettings"/> (rule 6).</para>
    /// </summary>
    public static class FishSchoolMath
    {
        // ---- the hash streams ------------------------------------------------------------------------
        // One key per (cell, slot); each decision reads its OWN numbered stream off it, so adding a new
        // decision later cannot shift the values every existing decision draws (which would silently
        // reshuffle every school in every save). Append new stream ids at the END, never renumber.
        private const int StreamPresence   = 0;
        private const int StreamCentreX    = 1;
        private const int StreamCentreY    = 2;
        private const int StreamRadius     = 3;
        private const int StreamDepth      = 4;
        private const int StreamMarks      = 5;
        private const int StreamWindowLen  = 6;
        private const int StreamWindowOff  = 7;
        private const int StreamSpeciesN   = 8;
        private const int StreamSpeciesPick = 9;

        /// <summary>How many cells the query has to look at: the 3×3 neighbourhood the one-cell radius cap
        /// makes sufficient. Also the most schools one query can ever report, which is what lets the model
        /// pre-allocate its buffers once and never allocate on the read path (rule 7).</summary>
        public const int SearchCells = 9;

        // ---- the lattice ------------------------------------------------------------------------------

        /// <summary>Which cell a world coordinate falls in. Floor (not truncate) so the lattice is
        /// continuous through the origin — truncation would make the cells either side of 0 double-wide
        /// and put a seam down the middle of every region authored around 0,0. NaN-safe.</summary>
        public static int CellIndex(float coordinate, float cellSizeMetres)
        {
            float size = Mathf.Max(0.0001f, Safe(cellSizeMetres));
            return Mathf.FloorToInt(Safe(coordinate) / size);
        }

        /// <summary>Which slot a game time falls in. <c>long</c> because <c>gameSeconds</c> is a
        /// <c>double</c> that grows for the life of a save (rule: <c>gameTime</c> is never a float).
        /// Negative time (before a new game) floors to a negative slot rather than aliasing onto slot 0.</summary>
        public static long SlotIndex(double gameSeconds, double slotSeconds)
        {
            double period = slotSeconds > 1e-6 ? slotSeconds : 1e-6;
            return (long)System.Math.Floor(gameSeconds / period);
        }

        /// <summary>Game time the given slot opens at.</summary>
        public static double SlotStartSeconds(long slot, double slotSeconds)
            => slot * (slotSeconds > 1e-6 ? slotSeconds : 1e-6);

        /// <summary>
        /// The one hash every decision about a <c>(cell, slot)</c> is drawn from — process-stable FNV-1a
        /// (<see cref="StableHash"/>), never <c>string.GetHashCode()</c>, which is randomized per process
        /// and would give the same water different fish on a reload.
        ///
        /// <para>The REGION id is folded in so two regions never mirror each other's shoals just because
        /// their world rectangles overlap in coordinates, and so re-seeding one region's fishing is a
        /// content act rather than a world-seed change.</para>
        /// </summary>
        public static uint CellSlotKey(int worldSeed, string regionId, int cellX, int cellY, long slot)
        {
            unchecked
            {
                uint h = StableHash.Fold(2166136261u, worldSeed);
                h = StableHash.Fold(h, regionId);
                h = StableHash.Fold(h, cellX);
                h = StableHash.Fold(h, cellY);
                h = StableHash.Fold(h, slot);
                return StableHash.Finalize(h);
            }
        }

        /// <summary>A stable value in [0, 1) from one key and one stream id.</summary>
        public static float Rand01(uint key, int stream)
            => (float)(StableHash.Finalize(StableHash.Fold(key, stream)) / 4294967296.0);

        /// <summary>A stable value in [lo, hi) — or exactly <c>lo</c> when the range is empty/inverted.</summary>
        public static float Range(uint key, int stream, float lo, float hi)
        {
            float a = Safe(lo);
            float b = Safe(hi);
            if (b <= a) return a;
            return a + Rand01(key, stream) * (b - a);
        }

        /// <summary>A stable integer in [lo, hi] INCLUSIVE (so authoring min == max gives that value, not
        /// an empty range).</summary>
        public static int RangeInt(uint key, int stream, int lo, int hi)
        {
            if (hi <= lo) return lo;
            int span = hi - lo + 1;
            return lo + Mathf.Min(span - 1, Mathf.FloorToInt(Rand01(key, stream) * span));
        }

        // ---- the appearance gate (the owner: location, weather and date decide when fish appear) ------

        /// <summary>
        /// The chance this cell holds a school in this slot — the owner's three gates, multiplied out:
        /// <b>LOCATION</b> (water deep enough to hold fish at all —
        /// <see cref="FishSchoolSettings.MinWaterColumnMetres"/>; a drying flat is barred outright),
        /// <b>WEATHER</b> (<see cref="FishSchoolSettings.SeaStateAppearanceBias"/> — broken water
        /// emboldens fish and shows more of them, the same direction
        /// <see cref="SeaFishingSettings.SeaBoldness01"/> already established) and <b>DATE</b> (the
        /// per-season multiplier).
        ///
        /// <para>Returns 0..1. A base chance of 0 is an empty sea no matter what the other two say — the
        /// A/B off switch — and the location gate is a hard bar, not a weight, because "there are fish on
        /// the beach" is not a rarity, it is a bug.</para>
        /// </summary>
        public static float AppearanceChance01(float waterColumnMetres, float seaState01, Season season,
                                               in FishSchoolSettings s)
        {
            if (Safe(waterColumnMetres) < Mathf.Max(0f, Safe(s.MinWaterColumnMetres))) return 0f;
            return AppearanceChance01(seaState01, season, in s);
        }

        /// <summary>
        /// The WEATHER × DATE half of the gate, without the location bar. Split out because it is the same
        /// for every cell in one query — hoisting it out of the search loop lets the presence draw be
        /// judged BEFORE the water column is sampled, and the location bar can only ever take the chance to
        /// zero, never raise it. So a cell that was never going to hold fish costs one hash instead of a
        /// bathymetry read (rule 7).
        /// </summary>
        public static float AppearanceChance01(float seaState01, Season season, in FishSchoolSettings s)
        {
            float baseChance = Mathf.Clamp01(Safe(s.BaseAppearanceChance01));
            if (baseChance <= 0f) return 0f;

            float weather = Mathf.Clamp(Safe(s.SeaStateAppearanceBias), -1f, 1f) * Mathf.Clamp01(Safe(seaState01));
            return Mathf.Clamp01((baseChance + weather) * SeasonAppearance(season, in s));
        }

        /// <summary>The DATE half of the gate: this season's multiplier on the appearance chance. Negative
        /// authoring heals to 0 (a closed season), never to a negative chance.</summary>
        public static float SeasonAppearance(Season season, in FishSchoolSettings s)
        {
            float m = season switch
            {
                Season.EarlySpring => s.EarlySpringAppearance,
                Season.HighSummer  => s.HighSummerAppearance,
                Season.TheTurn     => s.TheTurnAppearance,
                _                  => s.HardWinterAppearance
            };
            return Mathf.Max(0f, Safe(m));
        }

        /// <summary>Does this cell/slot hold a school at all? (The presence draw, judged against
        /// <see cref="AppearanceChance01"/>.) A chance of exactly 1 always passes; a chance of 0 never
        /// does.</summary>
        public static bool IsPresent(uint key, float chance01)
            => chance01 > 0f && (chance01 >= 1f || PresenceRoll01(key) < chance01);

        /// <summary>This cell/slot's presence draw on its own — so the search can reject a cell before
        /// paying for a bathymetry sample (see <see cref="AppearanceChance01(float, Season, in FishSchoolSettings)"/>).</summary>
        public static float PresenceRoll01(uint key) => Rand01(key, StreamPresence);

        // ---- the school's own shape -------------------------------------------------------------------

        /// <summary>Where in the cell the school sits (world m) — anywhere inside it, uniformly.</summary>
        public static Vector2 CentreFor(uint key, int cellX, int cellY, float cellSizeMetres)
        {
            float size = Mathf.Max(0.0001f, Safe(cellSizeMetres));
            return new Vector2(cellX * size + Rand01(key, StreamCentreX) * size,
                               cellY * size + Rand01(key, StreamCentreY) * size);
        }

        /// <summary>How wide the school's area is (radius, m). Already capped to one cell by
        /// <see cref="Sanitized"/> — see the type doc's search invariant.</summary>
        public static float RadiusFor(uint key, in FishSchoolSettings s)
            => Range(key, StreamRadius, s.MinRadiusMetres, s.MaxRadiusMetres);

        /// <summary>
        /// How deep the school sits (m below the surface): a FRACTION of the water column here, pushed
        /// down by the weather (<see cref="FishSchoolSettings.SeaStateDepthBias01"/> — fish go deep when
        /// it blows). Expressed as a fraction so one dial works over a 2 m flat and a 60 m hole, and so
        /// the mark always sits above the finder's own bottom contour rather than under it.
        ///
        /// <para>This is also the second way WEATHER decides WHICH species you find: the depth picks the
        /// band (<see cref="DepthDropMath.ZoneForDepth"/>), and the band picks the fish.</para>
        /// </summary>
        public static float DepthFor(uint key, float waterColumnMetres, float seaState01,
                                     in FishSchoolSettings s)
        {
            float column = Mathf.Max(0f, Safe(waterColumnMetres));
            float lo = Mathf.Clamp01(Safe(s.MinDepthFraction01));
            float hi = Mathf.Max(lo, Mathf.Clamp01(Safe(s.MaxDepthFraction01)));
            float fraction = Range(key, StreamDepth, lo, hi)
                           + Mathf.Clamp01(Safe(s.SeaStateDepthBias01)) * Mathf.Clamp01(Safe(seaState01));
            return column * Mathf.Clamp01(fraction);
        }

        /// <summary>How many fish are showing — the owner's density ruling in one number: both the marks
        /// the glass draws and the expected bite rate (<see cref="FishSchool.MarkCount"/>).</summary>
        public static int MarkCountFor(uint key, in FishSchoolSettings s)
            => RangeInt(key, StreamMarks, Mathf.Max(1, s.MinMarks), Mathf.Max(1, s.MaxMarks));

        /// <summary>
        /// The window this school is up for — the owner's "finding one opens a window of time during which
        /// bites happen". A length drawn from the authored range, placed at a hashed offset inside the
        /// slot, and <b>always contained by the slot</b> so a query never has to look at more than one
        /// (see the type doc's second invariant). Half-open <c>[start, end)</c>, matching
        /// <see cref="FishSchool.IsActiveAt"/>.
        /// </summary>
        /// <param name="s">Settings that have been through <see cref="Sanitized"/> — the containment
        /// invariant is enforced there (<c>MinWindowHours ≤ MaxWindowHours ≤ SlotHours</c>), not
        /// re-derived here.</param>
        public static void WindowFor(uint key, long slot, double secondsPerHour, in FishSchoolSettings s,
                                     out double startSeconds, out double endSeconds)
        {
            double sph = secondsPerHour > 1e-6 ? secondsPerHour : 1e-6;
            double period = SlotSeconds(in s, sph);
            double minLen = s.MinWindowHours * sph;
            double maxLen = s.MaxWindowHours * sph;
            double length = System.Math.Min(period, minLen + Rand01(key, StreamWindowLen) * (maxLen - minLen));
            double offset = Rand01(key, StreamWindowOff) * (period - length);
            startSeconds = SlotStartSeconds(slot, period) + offset;
            endSeconds = startSeconds + length;
        }

        /// <summary>The slot period in game seconds — <c>SlotHours</c> in the clock's own frame.</summary>
        public static double SlotSeconds(in FishSchoolSettings s, double secondsPerHour)
            => System.Math.Max(1e-6, Safe(s.SlotHours) * System.Math.Max(1e-6, secondsPerHour));

        // ---- species (the owner: the same three decide WHICH species are found there) ------------------

        /// <summary>How many species this school holds, before the candidate pool caps it.</summary>
        public static int SpeciesCountFor(uint key, in FishSchoolSettings s)
            => RangeInt(key, StreamSpeciesN, Mathf.Max(1, s.MinSpecies), Mathf.Max(1, s.MaxSpecies));

        /// <summary>
        /// This species' draw for this school, 0..1 — the highest scorers are the ones down there.
        ///
        /// <para><b>Keyed by the species ID, deliberately not by its index in the region pool.</b> An index
        /// would make "content author adds a fish to St Peters" silently re-pick the species of every
        /// school in the region, in every existing save. Hashing the stable id means a new species only
        /// competes; it does not reshuffle its neighbours.</para>
        /// </summary>
        public static float SpeciesScore(uint key, string speciesId)
            => (float)(StableHash.Finalize(StableHash.Fold(StableHash.Fold(key, StreamSpeciesPick), speciesId))
                       / 4294967296.0);

        // ---- density → bite rate (the one place the picture becomes the fishing) ------------------------

        /// <summary>
        /// The bite-rate multiplier a given weight of marks is worth: <c>1 + marks × per-mark</c>, capped.
        /// The wait for a bite is DIVIDED by this, so 1 is "no school here, fish exactly as before".
        ///
        /// <para><b>This is the honesty invariant's arithmetic.</b> The marks fed in are the same
        /// <see cref="FishSchool.MarkCount"/> the glass draws, scaled by the same
        /// <see cref="FishSchool.SignalAt"/> the glass dims them with — there is no separate bite table to
        /// disagree with the picture. Never below 1: a school can only ever make fishing better, so being
        /// over fish is never a punishment.</para>
        /// </summary>
        public static float BiteRateMultiplier(float effectiveMarks, in FishSchoolSettings s)
        {
            float cap = Mathf.Max(1f, Safe(s.MaxBiteRateMultiplier));
            float m = 1f + Mathf.Max(0f, Safe(effectiveMarks)) * Mathf.Max(0f, Safe(s.BiteRatePerMark));
            return Mathf.Clamp(m, 1f, cap);
        }

        /// <summary>
        /// How much of a school you are getting for holding the rig where you are holding it, 0..1 — the
        /// owner's "bites happen at approximately the depth shown", made a soft weight rather than a wall.
        ///
        /// <para><b>It reuses <see cref="DepthDropMath.ZoneForDepth"/></b> rather than inventing a second
        /// "within ±N metres" rule: the game already has one set of fishing depth bands, the species
        /// already weight off them, and a second notion of "about the right depth" would be a second thing
        /// to tune apart. Same band as the school → the whole school; a different band →
        /// <see cref="FishSchoolSettings.OffDepthMatch01"/>, never 0.</para>
        ///
        /// <para><b>A cast with no depth game</b> (the bobber/legacy branch, <c>heldDepthM &lt; 0</c>) gets
        /// <see cref="FishSchoolSettings.DepthlessMatch01"/> — 1 by default, so this feature never makes
        /// the old way of fishing worse. That is deliberately NOT the same as the depth-drop's neutral ×1,
        /// which means "no bonus and no penalty": here the school's presence is the bonus and the depth
        /// read is the extra skill on top, so a player with no depth to judge simply isn't judged.</para>
        ///
        /// <para><b>The fall-count read is untouched</b> (<see cref="DepthDropMath"/> decision #4, owner
        /// 2026-08-03): the finder tells you a spot and roughly how deep; judging the drop is still the
        /// player's own skill, and nothing here reads or shortcuts it.</para>
        /// </summary>
        public static float DepthMatch01(float heldDepthM, float schoolDepthM,
                                         in DepthDropSettings depth, in FishSchoolSettings s)
        {
            if (float.IsNaN(heldDepthM) || heldDepthM < 0f)
                return Mathf.Clamp01(Safe(s.DepthlessMatch01));

            FishDepthBand held = DepthDropMath.ZoneForDepth(heldDepthM, depth.TidepoolMaxMeters,
                                                           depth.ShallowsMaxMeters, depth.InshoreMaxMeters,
                                                           depth.MidwaterMaxMeters, depth.DeepMaxMeters);
            FishDepthBand school = DepthDropMath.ZoneForDepth(schoolDepthM, depth.TidepoolMaxMeters,
                                                             depth.ShallowsMaxMeters, depth.InshoreMaxMeters,
                                                             depth.MidwaterMaxMeters, depth.DeepMaxMeters);
            return held == school ? 1f : Mathf.Clamp01(Safe(s.OffDepthMatch01));
        }

        // ---- guards ------------------------------------------------------------------------------------

        /// <summary>
        /// The settings with every self-consistency rule the search relies on ENFORCED rather than trusted
        /// — the owner can type anything into the Inspector and the sim still holds:
        /// <list type="bullet">
        /// <item>the radius is capped to one cell (the 3×3 search invariant);</item>
        /// <item>the window is capped to the slot (the one-slot search invariant);</item>
        /// <item>every min/max pair is ordered, every fraction clamped, every count ≥ 1.</item>
        /// </list>
        /// Call once per query and pass the result down — the maths above then needs no defensive
        /// re-clamping and every test can state the invariant instead of the clamp.
        /// </summary>
        public static FishSchoolSettings Sanitized(in FishSchoolSettings s)
        {
            FishSchoolSettings o = s;

            o.CellSizeMetres = Mathf.Max(0.0001f, Safe(s.CellSizeMetres));
            o.SlotHours = Mathf.Max(0.0001f, Safe(s.SlotHours));

            o.BaseAppearanceChance01 = Mathf.Clamp01(Safe(s.BaseAppearanceChance01));
            o.MinWaterColumnMetres = Mathf.Max(0f, Safe(s.MinWaterColumnMetres));
            o.SeaStateAppearanceBias = Mathf.Clamp(Safe(s.SeaStateAppearanceBias), -1f, 1f);
            o.EarlySpringAppearance = Mathf.Max(0f, Safe(s.EarlySpringAppearance));
            o.HighSummerAppearance = Mathf.Max(0f, Safe(s.HighSummerAppearance));
            o.TheTurnAppearance = Mathf.Max(0f, Safe(s.TheTurnAppearance));
            o.HardWinterAppearance = Mathf.Max(0f, Safe(s.HardWinterAppearance));

            o.MinWindowHours = Mathf.Clamp(Safe(s.MinWindowHours), 0.0001f, o.SlotHours);
            o.MaxWindowHours = Mathf.Clamp(Safe(s.MaxWindowHours), o.MinWindowHours, o.SlotHours);

            o.MinRadiusMetres = Mathf.Clamp(Safe(s.MinRadiusMetres), 0.0001f, o.CellSizeMetres);
            o.MaxRadiusMetres = Mathf.Clamp(Safe(s.MaxRadiusMetres), o.MinRadiusMetres, o.CellSizeMetres);

            o.MinDepthFraction01 = Mathf.Clamp01(Safe(s.MinDepthFraction01));
            o.MaxDepthFraction01 = Mathf.Clamp(Safe(s.MaxDepthFraction01), o.MinDepthFraction01, 1f);
            o.SeaStateDepthBias01 = Mathf.Clamp01(Safe(s.SeaStateDepthBias01));
            o.OpenWaterColumnMetres = Mathf.Max(0.0001f, Safe(s.OpenWaterColumnMetres));

            o.MinMarks = Mathf.Max(1, s.MinMarks);
            o.MaxMarks = Mathf.Max(o.MinMarks, s.MaxMarks);
            o.BiteRatePerMark = Mathf.Max(0f, Safe(s.BiteRatePerMark));
            o.MaxBiteRateMultiplier = Mathf.Max(1f, Safe(s.MaxBiteRateMultiplier));

            o.OffDepthMatch01 = Mathf.Clamp01(Safe(s.OffDepthMatch01));
            o.DepthlessMatch01 = Mathf.Clamp01(Safe(s.DepthlessMatch01));

            o.MinSpecies = Mathf.Max(1, s.MinSpecies);
            o.MaxSpecies = Mathf.Max(o.MinSpecies, s.MaxSpecies);
            o.SchoolSpeciesBoost = Mathf.Max(1f, Safe(s.SchoolSpeciesBoost));
            o.OffSchoolSpeciesDamp01 = Mathf.Clamp01(Safe(s.OffSchoolSpeciesDamp01));

            return o;
        }

        /// <summary>NaN → 0 (the safe, neutral value) — Unity's <c>Mathf.Clamp</c> passes NaN through, so
        /// inputs are sanitized first (the <see cref="DepthDropMath"/> guard).</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
