using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// WHERE THE FISH ARE — the deterministic school sim behind the Core <see cref="IFishSchools"/> seam
    /// (ADR 0025 S3, the owner's ruling of 2026-08-03). The fish finder READS this to draw its marks and
    /// the fishing path reads the SAME instance to raise the bite rate and weight the species roll. One
    /// model, two readers: that is the owner's honesty invariant, and this class is the single record it
    /// rests on.
    ///
    /// <para><b>A pure function of the world, not a spawner.</b> There is no <c>Update</c>, no timer, no
    /// list of live fish and nothing in <c>SaveData</c>. A school is recomputed from
    /// <c>(worldSeed, gameTime, place, weather, season)</c> every time it is asked for, exactly as the
    /// tide and the wind are (rule 5) — so two players on one seed see the same fish, a save/load lands
    /// you back among the same fish, and the whole sim is EditMode-testable with no scene by handing it a
    /// fake <see cref="IFishSchoolWorld"/>. The maths is <see cref="FishSchoolMath"/>; this class is the
    /// composition of it with the region's species pool.</para>
    ///
    /// <para><b><see cref="MarksAt"/> cannot disagree with <see cref="SchoolsAt"/> — structurally.</b>
    /// Both call the same private <c>Gather</c> and the marks are its schools put through
    /// <see cref="FishSchool.ToMark"/>. There is no second query path, no second density number and no
    /// mark-only state, so "the glass shows fish that aren't biting" is not a bug that can be introduced
    /// here; it would have to be introduced by deleting this class.</para>
    ///
    /// <para><b>Allocation-free on the read path</b> (rule 7): the schools and their species lists live in
    /// buffers sized once at construction (<see cref="FishSchoolMath.SearchCells"/> — the 3×3 cell
    /// neighbourhood is provably the whole search), and both calls fill a caller-owned list.
    /// ⚠ <b>The species lists are owned by this model and are rewritten by the next query.</b> A caller
    /// that needs them to outlive the call copies them (<see cref="SchoolInfluence"/> does exactly
    /// that).</para>
    /// </summary>
    public sealed class FishSchoolModel : IFishSchools, IFishSchoolView
    {
        private readonly IFishSchoolWorld _world;
        private readonly IReadOnlyList<FishSpeciesDef> _pool;

        // Live settings (an owner dragging a slider in Play moves the fish) or a fixed set for a rig with
        // no config. Exactly one of these is in play; _liveWorld is non-null only in the live case.
        private readonly LiveFishSchoolWorld _liveWorld;
        private readonly FishSchoolSettings _fixedSettings;
        private readonly DepthDropSettings _fixedDepth;
        private readonly double _fixedSecondsPerHour;

        /// <summary>
        /// How many schools one <see cref="SchoolsInView"/> read may return. The point query is provably
        /// O(9) (<see cref="FishSchoolMath.SearchCells"/> — a school's radius is capped to one cell), but a
        /// VIEW spans as many cells as the camera plus one ring of neighbours covers, and the owner can
        /// tune <c>CellSizeMetres</c> down. At the shipped 120 m cells a 16:9 camera plus the 55 m radius
        /// ring spans 3x3, so this 5x5 budget is roomy; past it the read is capped rather than allowed to
        /// grow without bound (rule 7, and the cap the seam documents).
        /// </summary>
        public const int ViewCells = 25;

        // The read-path buffers — sized once, never grown (rule 7). Sized for the WIDER of the two reads,
        // so the point query keeps using the first nine slots exactly as before.
        private readonly FishSchool[] _found = new FishSchool[ViewCells];
        private readonly List<string>[] _species = new List<string>[ViewCells];
        private int _foundCount;

        /// <summary>
        /// The LIVE model: settings, depth bands and the clock's pace all read off the owner's config each
        /// query through <paramref name="world"/>, so tuning moves the sea without a restart.
        /// </summary>
        /// <param name="world">The live world adapter (services + config).</param>
        /// <param name="regionSpecies">The region's authored species pool. <b>Deliberately the same array
        /// the catch resolver rolls from</b> — a school can only ever hold fish that could actually bite
        /// here, which is the honesty invariant made structural rather than asserted.</param>
        public FishSchoolModel(LiveFishSchoolWorld world, IReadOnlyList<FishSpeciesDef> regionSpecies)
        {
            _world = world;
            _liveWorld = world;
            _pool = regionSpecies;
            AllocateBuffers();
        }

        /// <summary>
        /// The FIXED-tuning model: for a rig with no <see cref="GameConfig"/> and for EditMode tests, which
        /// need to state the settings and the clock pace explicitly rather than discover them.
        /// </summary>
        public FishSchoolModel(IFishSchoolWorld world, IReadOnlyList<FishSpeciesDef> regionSpecies,
                               in FishSchoolSettings settings, in DepthDropSettings depth,
                               double secondsPerHour)
        {
            _world = world;
            _liveWorld = null;
            _pool = regionSpecies;
            _fixedSettings = settings;
            _fixedDepth = depth;
            _fixedSecondsPerHour = secondsPerHour;
            AllocateBuffers();
        }

        private void AllocateBuffers()
        {
            for (int i = 0; i < _species.Length; i++) _species[i] = new List<string>(4);
        }

        /// <summary>The owner's school tuning for this query, already through
        /// <see cref="FishSchoolMath.Sanitized"/> — every search invariant enforced, so nothing below has
        /// to re-clamp.</summary>
        public FishSchoolSettings Settings
            => FishSchoolMath.Sanitized(_liveWorld != null ? _liveWorld.Settings : _fixedSettings);

        /// <summary>The depth bands the school sim shares with the depth drop (there is only ever one set
        /// of fishing depth zones — see <see cref="FishSchoolMath.DepthMatch01"/>).</summary>
        public DepthDropSettings DepthSettings
            => _liveWorld != null ? _liveWorld.DepthSettings : _fixedDepth;

        /// <summary>In-game seconds per in-game hour — the frame the school windows are authored in.</summary>
        public double SecondsPerHour
            => _liveWorld != null ? _liveWorld.SecondsPerHour : _fixedSecondsPerHour;

        // ---- the seam ---------------------------------------------------------------------------------

        /// <inheritdoc/>
        public int SchoolsAt(Vector2 worldPos, double gameSeconds, List<FishSchool> into)
        {
            into?.Clear();
            int n = Gather(worldPos, gameSeconds);
            if (into != null)
                for (int i = 0; i < n; i++) into.Add(_found[i]);
            return n;
        }

        /// <inheritdoc/>
        public int MarksAt(Vector2 worldPos, double gameSeconds, List<FishMark> into)
        {
            into?.Clear();
            // ⚠ The SAME gather, put through the school's own ToMark. Never a second query, never a second
            // density number — this line is where the owner's honesty invariant is kept.
            int n = Gather(worldPos, gameSeconds);
            if (into != null)
                for (int i = 0; i < n; i++) into.Add(_found[i].ToMark(worldPos));
            return n;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The SAME per-<c>(cell, slot)</c> build as <see cref="SchoolsAt"/>, over the cells the rect
        /// covers instead of the nine around a point, and accepted on OVERLAP instead of containment.
        /// There is no second density number, no second species pick and no view-only state — a school
        /// drawn here is the school the rod finds when you cast onto it. See
        /// <see cref="IFishSchoolView"/> for why that identity is the whole point of the seam.
        /// </remarks>
        public int SchoolsInView(Rect viewWorld, double gameSeconds, List<FishSchool> into)
        {
            into?.Clear();
            int n = GatherView(viewWorld, gameSeconds);
            if (into != null)
                for (int i = 0; i < n; i++) into.Add(_found[i]);
            return n;
        }

        // ---- the search --------------------------------------------------------------------------------

        /// <summary>
        /// Fill <see cref="_found"/> with every school whose area contains <paramref name="worldPos"/> and
        /// whose window is open at <paramref name="gameSeconds"/>, and return how many.
        ///
        /// <para>Only the 3×3 cell neighbourhood and only the CURRENT slot are searched — both are
        /// sufficient by construction, because <see cref="FishSchoolMath.Sanitized"/> caps a school's
        /// radius to one cell and its window to its own slot. That is what keeps this O(9) rather than
        /// O(the sea).</para>
        /// </summary>
        private int Gather(Vector2 worldPos, double gameSeconds)
        {
            _foundCount = 0;
            if (!Prepare(gameSeconds, out Query q)) return 0;

            int cx = FishSchoolMath.CellIndex(worldPos.x, q.Settings.CellSizeMetres);
            int cy = FishSchoolMath.CellIndex(worldPos.y, q.Settings.CellSizeMetres);

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                Accept(cx + dx, cy + dy, gameSeconds, in q, worldPos, useRect: false, default);

            return _foundCount;
        }

        /// <summary>
        /// Fill <see cref="_found"/> with every school whose disc OVERLAPS <paramref name="viewWorld"/>
        /// and whose window is open — <see cref="Gather"/>'s twin, and deliberately built out of the very
        /// same <see cref="Prepare"/> and <see cref="Accept"/> so the two reads cannot drift apart.
        ///
        /// <para>The cell span is the rect GROWN by the largest radius a school may have, because a school
        /// centred outside the view can still reach into it. <see cref="FishSchoolMath.Sanitized"/> caps
        /// that radius to one cell, so one ring of neighbours is provably enough and the span stays
        /// finite. It is then clamped to <see cref="ViewCells"/> — a presentation budget, never a claim
        /// about how many fish are there (see <see cref="IFishSchoolView.SchoolsInView"/>).</para>
        /// </summary>
        private int GatherView(Rect viewWorld, double gameSeconds)
        {
            _foundCount = 0;
            if (!Prepare(gameSeconds, out Query q)) return 0;

            float cell = q.Settings.CellSizeMetres;
            float reach = q.Settings.MaxRadiusMetres;      // already clamped to one cell by Sanitized

            int x0 = FishSchoolMath.CellIndex(viewWorld.xMin - reach, cell);
            int x1 = FishSchoolMath.CellIndex(viewWorld.xMax + reach, cell);
            int y0 = FishSchoolMath.CellIndex(viewWorld.yMin - reach, cell);
            int y1 = FishSchoolMath.CellIndex(viewWorld.yMax + reach, cell);

            for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                if (_foundCount >= _found.Length) return _foundCount;   // the documented cap
                Accept(cx, cy, gameSeconds, in q, default, useRect: true, viewWorld);
            }

            return _foundCount;
        }

        /// <summary>Everything a query hoists before it touches a single cell — weather, date, seed and
        /// the slot — so the two gathers agree about the world by construction, not by repetition.</summary>
        private readonly struct Query
        {
            public readonly FishSchoolSettings Settings;
            public readonly double SecondsPerHour;
            public readonly long Slot;
            public readonly double SlotStart;
            public readonly float SeaState01;
            public readonly Season Season;
            public readonly int Seed;
            public readonly string RegionId;
            public readonly float Chance01;
            public readonly float HourOfDay;
            public readonly float TideRateMetresPerHour;

            public Query(in FishSchoolSettings settings, double secondsPerHour, long slot, double slotStart,
                         float seaState01, Season season, int seed, string regionId, float chance01,
                         float hourOfDay, float tideRateMetresPerHour)
            {
                HourOfDay = hourOfDay;
                TideRateMetresPerHour = tideRateMetresPerHour;
                Settings = settings;
                SecondsPerHour = secondsPerHour;
                Slot = slot;
                SlotStart = slotStart;
                SeaState01 = seaState01;
                Season = season;
                Seed = seed;
                RegionId = regionId;
                Chance01 = chance01;
            }
        }

        /// <summary>
        /// Hoist the whole-query terms once. False means the sea is empty for this instant and not one
        /// cell need be visited — the owner's off switch, or a weather/date the fish do not show in.
        /// </summary>
        private bool Prepare(double gameSeconds, out Query q)
        {
            q = default;

            FishSchoolSettings s = Settings;
            if (s.BaseAppearanceChance01 <= 0f) return false;   // the owner's off switch: an empty sea
            if (_world == null) return false;

            double secondsPerHour = SecondsPerHour;
            double slotSeconds = FishSchoolMath.SlotSeconds(in s, secondsPerHour);
            long slot = FishSchoolMath.SlotIndex(gameSeconds, slotSeconds);
            double slotStart = FishSchoolMath.SlotStartSeconds(slot, slotSeconds);

            // The world is asked about the moment the schools FORMED, not about now — see IFishSchoolWorld.
            float seaState01 = _world.SeaState01At(slotStart);
            Season season = _world.SeasonAt(slotStart);

            // Weather x date is the same for every cell, so it is hoisted here and the presence draw is
            // judged against it BEFORE any bathymetry is sampled — the location bar can only take the
            // chance to zero, never raise it.
            float chance = FishSchoolMath.AppearanceChance01(seaState01, season, in s);
            if (chance <= 0f) return false;

            q = new Query(in s, secondsPerHour, slot, slotStart, seaState01, season,
                          _world.WorldSeed, _world.RegionId, chance,
                          _world.HourOfDayAt(slotStart), _world.TideRateMetresPerHourAt(slotStart));
            return true;
        }

        /// <summary>
        /// Build the one school a <c>(cell, slot)</c> holds and keep it if it passes the query's spatial
        /// test and its own window. <b>The single place a school is admitted to a result</b>, for both
        /// reads — which is what makes "the fish you see are the fish you catch" structural.
        /// </summary>
        private void Accept(int cellX, int cellY, double gameSeconds, in Query q,
                            Vector2 worldPos, bool useRect, Rect viewWorld)
        {
            if (_foundCount >= _found.Length) return;

            if (!TryBuild(q.Seed, q.RegionId, cellX, cellY, q.Slot, q.SlotStart, q.SecondsPerHour,
                          q.SeaState01, q.Chance01, in q.Settings, out FishSchool school, out uint key))
                return;

            if (!school.IsActiveAt(gameSeconds)) return;
            if (useRect ? !OverlapsRect(school, viewWorld) : !school.Contains(worldPos)) return;

            // Only a school the query actually keeps pays for its species list.
            List<string> ids = _species[_foundCount];
            FillSpecies(key, school.DepthMetres, in q, ids);

            // ⚠ DENSITY IS RESOLVED AFTER THE SPECIES, and only here (owner's ruling 2026-09-06: a
            // herring shoal is not a flounder). TryBuild's count is provisional and never escapes — it
            // cannot know which fish it holds. This is the ONE place FishSchool.MarkCount is decided,
            // which is what keeps the number the glass draws, the number the water swims and the number
            // the bite rate uses the SAME number.
            int marks = MarkCountForSpecies(key, ids, in q.Settings);

            _found[_foundCount++] = new FishSchool(school.Centre, school.RadiusMetres,
                                                   school.DepthMetres, marks, ids,
                                                   school.StartSeconds, school.EndSeconds);
        }

        /// <summary>
        /// The school's density, from its PRIMARY species' own authored range — the first stated id,
        /// which is also the one the water draws the school as, so the count and the picture can never
        /// come from two different fish.
        ///
        /// <para>Falls back to the owner's global range when the school states no species, or when that
        /// species leaves its range unstated — so a region with nothing authored behaves exactly as it
        /// did before the field existed.</para>
        /// </summary>
        private int MarkCountForSpecies(uint key, List<string> ids, in FishSchoolSettings s)
        {
            if (ids == null || ids.Count == 0 || _pool == null)
                return FishSchoolMath.MarkCountFor(key, in s);

            string primary = ids[0];
            for (int i = 0; i < _pool.Count; i++)
            {
                FishSpeciesDef f = _pool[i];
                if (f == null || !string.Equals(f.Id, primary, System.StringComparison.Ordinal)) continue;
                return FishSchoolMath.MarkCountFor(key, f.MinSchoolMarks, f.MaxSchoolMarks, in s);
            }

            return FishSchoolMath.MarkCountFor(key, in s);
        }

        /// <summary>Does the school's disc touch the rect? Closest-point test — a school whose centre is
        /// off screen still counts while any of its water is on it.</summary>
        private static bool OverlapsRect(in FishSchool school, Rect r)
        {
            if (school.RadiusMetres <= 0f) return false;
            float nx = Mathf.Clamp(school.Centre.x, r.xMin, r.xMax);
            float ny = Mathf.Clamp(school.Centre.y, r.yMin, r.yMax);
            float dx = school.Centre.x - nx, dy = school.Centre.y - ny;
            return dx * dx + dy * dy <= school.RadiusMetres * school.RadiusMetres;
        }

        /// <summary>
        /// Build the school one <c>(cell, slot)</c> holds, or report that it holds none. The species set is
        /// left unstated here and filled by <see cref="FillSpecies"/> only for the schools the query is
        /// actually standing on.
        /// </summary>
        private bool TryBuild(int seed, string regionId, int cellX, int cellY, long slot, double slotStart,
                              double secondsPerHour, float seaState01, float weatherAndDateChance01,
                              in FishSchoolSettings s, out FishSchool school, out uint key)
        {
            school = default;
            key = FishSchoolMath.CellSlotKey(seed, regionId, cellX, cellY, slot);
            if (FishSchoolMath.PresenceRoll01(key) >= weatherAndDateChance01) return false;

            // The centre is resolved before the water is read because the LOCATION gate and the depth are
            // both read at the school's OWN centre — "is there water where the fish would be", not "where
            // the boat happens to be".
            Vector2 centre = FishSchoolMath.CentreFor(key, cellX, cellY, s.CellSizeMetres);
            float column = _world.WaterColumnAt(centre, slotStart);
            if (column < s.MinWaterColumnMetres) return false;      // the hard location bar

            FishSchoolMath.WindowFor(key, slot, secondsPerHour, in s,
                                     out double startSeconds, out double endSeconds);

            school = new FishSchool(centre,
                                    FishSchoolMath.RadiusFor(key, in s),
                                    FishSchoolMath.DepthFor(key, column, seaState01, in s),
                                    FishSchoolMath.MarkCountFor(key, in s),
                                    null,
                                    startSeconds, endSeconds);
            return true;
        }

        /// <summary>
        /// WHICH FISH ARE DOWN THERE — the owner's "the same three decide which species are found there",
        /// resolved against the region's authored pool: <b>location</b> (the species must be authored for
        /// this region, and must live at this school's depth band), <b>date</b> (its own season window) and
        /// <b>weather</b> (which moved the depth, and so the band — <see cref="FishSchoolMath.DepthFor"/>).
        ///
        /// <para><b>The depth-band filter relaxes rather than starving.</b> If nothing in the region lives
        /// at this band, the band is dropped and the season/region survivors stand — a deep hole in a
        /// region that only authors inshore fish holds inshore fish rather than an unstated school that
        /// silently means "no opinion". If even that is empty the set is left EMPTY, which the seam defines
        /// as "no species stated": the school still raises the bite rate and the roll falls back to its
        /// normal pool (<see cref="FishSchool.SpeciesIds"/>).</para>
        ///
        /// <para>Picking is by hashed score over the STABLE ID (<see cref="FishSchoolMath.SpeciesScore"/>),
        /// so adding a species to a region competes rather than reshuffling every school in every existing
        /// save. Allocation-free: the caller's list is reused and the selection is an O(n·m) scan over a
        /// handful of candidates.</para>
        /// </summary>
        private void FillSpecies(uint key, float schoolDepthM, in Query q, List<string> into)
        {
            string regionId = q.RegionId;
            Season season = q.Season;
            FishSchoolSettings s = q.Settings;

            into.Clear();
            if (_pool == null || _pool.Count == 0) return;

            DepthDropSettings dd = DepthSettings;
            FishDepthBand band = DepthDropMath.ZoneForDepth(schoolDepthM, dd.TidepoolMaxMeters,
                                                           dd.ShallowsMaxMeters, dd.InshoreMaxMeters,
                                                           dd.MidwaterMaxMeters, dd.DeepMaxMeters);

            int wanted = FishSchoolMath.SpeciesCountFor(key, in s);

            // Pass 1: region + season + depth band. Pass 2 (only if pass 1 found nothing): drop the band.
            if (!Select(key, in q, band, requireBand: true, wanted, into))
                Select(key, in q, band, requireBand: false, wanted, into);
        }

        /// <summary>Take the <paramref name="wanted"/> highest-scoring candidates that pass the filters.
        /// Returns true if anything was written.</summary>
        private bool Select(uint key, in Query q, FishDepthBand band,
                            bool requireBand, int wanted, List<string> into)
        {
            string regionId = q.RegionId;
            for (int picked = 0; picked < wanted; picked++)
            {
                string best = null;
                float bestScore = -1f;

                for (int i = 0; i < _pool.Count; i++)
                {
                    FishSpeciesDef f = _pool[i];
                    if (f == null || string.IsNullOrEmpty(f.Id)) continue;
                    if (!f.RegionAllowed(regionId)) continue;
                    if (!f.SeasonAllowed(q.Season)) continue;

                    // ⚠ The SAME two gates the catch resolver applies, read off the SAME Def fields —
                    // a species the rod could not catch here and now must never be put in a school for
                    // the water to draw. Both no-op when the world cannot sample them (a rig, a fixture),
                    // so nothing that worked before this existed starts starving.
                    if (q.HourOfDay >= 0f && !f.TimeAllowed(q.HourOfDay)) continue;
                    if (!f.MovingWaterAllowed(q.TideRateMetresPerHour,
                                              q.Settings.MovingWaterMetresPerHour)) continue;
                    if (requireBand && f.DepthBands != FishDepthBand.None && (f.DepthBands & band) == 0) continue;
                    if (Contains(into, f.Id)) continue;

                    float score = FishSchoolMath.SpeciesScore(key, f.Id);
                    if (score > bestScore) { bestScore = score; best = f.Id; }
                }

                if (best == null) break;      // the candidates ran out before the quota did
                into.Add(best);
            }
            return into.Count > 0;
        }

        private static bool Contains(List<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], id, System.StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
