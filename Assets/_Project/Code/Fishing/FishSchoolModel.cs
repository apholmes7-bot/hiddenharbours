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
    public sealed class FishSchoolModel : IFishSchools
    {
        private readonly IFishSchoolWorld _world;
        private readonly IReadOnlyList<FishSpeciesDef> _pool;

        // Live settings (an owner dragging a slider in Play moves the fish) or a fixed set for a rig with
        // no config. Exactly one of these is in play; _liveWorld is non-null only in the live case.
        private readonly LiveFishSchoolWorld _liveWorld;
        private readonly FishSchoolSettings _fixedSettings;
        private readonly DepthDropSettings _fixedDepth;
        private readonly double _fixedSecondsPerHour;

        // The read-path buffers — sized once, never grown (rule 7).
        private readonly FishSchool[] _found = new FishSchool[FishSchoolMath.SearchCells];
        private readonly List<string>[] _species = new List<string>[FishSchoolMath.SearchCells];
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

            FishSchoolSettings s = Settings;
            if (s.BaseAppearanceChance01 <= 0f) return 0;      // the owner's off switch: an empty sea
            if (_world == null) return 0;

            double secondsPerHour = SecondsPerHour;
            double slotSeconds = FishSchoolMath.SlotSeconds(in s, secondsPerHour);
            long slot = FishSchoolMath.SlotIndex(gameSeconds, slotSeconds);
            double slotStart = FishSchoolMath.SlotStartSeconds(slot, slotSeconds);

            // The world is asked about the moment the schools FORMED, not about now — see IFishSchoolWorld.
            float seaState01 = _world.SeaState01At(slotStart);
            Season season = _world.SeasonAt(slotStart);
            int seed = _world.WorldSeed;
            string regionId = _world.RegionId;

            // Weather × date is the same for all nine cells, so it is hoisted here and the presence draw is
            // judged against it BEFORE any bathymetry is sampled — the location bar can only take the
            // chance to zero, never raise it.
            float chance = FishSchoolMath.AppearanceChance01(seaState01, season, in s);
            if (chance <= 0f) return 0;

            int cx = FishSchoolMath.CellIndex(worldPos.x, s.CellSizeMetres);
            int cy = FishSchoolMath.CellIndex(worldPos.y, s.CellSizeMetres);

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!TryBuild(seed, regionId, cx + dx, cy + dy, slot, slotStart, secondsPerHour,
                              seaState01, chance, in s, out FishSchool school, out uint key)) continue;
                if (!school.Contains(worldPos) || !school.IsActiveAt(gameSeconds)) continue;

                // Only a school we are actually ON pays for its species list.
                List<string> ids = _species[_foundCount];
                FillSpecies(key, school.DepthMetres, regionId, season, in s, ids);
                _found[_foundCount++] = new FishSchool(school.Centre, school.RadiusMetres,
                                                       school.DepthMetres, school.MarkCount, ids,
                                                       school.StartSeconds, school.EndSeconds);
            }

            return _foundCount;
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
        private void FillSpecies(uint key, float schoolDepthM, string regionId, Season season,
                                 in FishSchoolSettings s, List<string> into)
        {
            into.Clear();
            if (_pool == null || _pool.Count == 0) return;

            DepthDropSettings dd = DepthSettings;
            FishDepthBand band = DepthDropMath.ZoneForDepth(schoolDepthM, dd.TidepoolMaxMeters,
                                                           dd.ShallowsMaxMeters, dd.InshoreMaxMeters,
                                                           dd.MidwaterMaxMeters, dd.DeepMaxMeters);

            int wanted = FishSchoolMath.SpeciesCountFor(key, in s);

            // Pass 1: region + season + depth band. Pass 2 (only if pass 1 found nothing): drop the band.
            if (!Select(key, regionId, season, band, requireBand: true, wanted, into))
                Select(key, regionId, season, band, requireBand: false, wanted, into);
        }

        /// <summary>Take the <paramref name="wanted"/> highest-scoring candidates that pass the filters.
        /// Returns true if anything was written.</summary>
        private bool Select(uint key, string regionId, Season season, FishDepthBand band,
                            bool requireBand, int wanted, List<string> into)
        {
            for (int picked = 0; picked < wanted; picked++)
            {
                string best = null;
                float bestScore = -1f;

                for (int i = 0; i < _pool.Count; i++)
                {
                    FishSpeciesDef f = _pool[i];
                    if (f == null || string.IsNullOrEmpty(f.Id)) continue;
                    if (!f.RegionAllowed(regionId)) continue;
                    if (!f.SeasonAllowed(season)) continue;
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
