using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using G = HiddenHarbours.App.Editor.NineMileCreekGroundPlan;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>NINE MILE CREEK'S WALLS STAND ON THE GROUND THE GAME HAS (NMC H3).</b> The walls are laid from their Defs
    /// (<see cref="CliffWallDef"/>, one per wall by real id, <c>wall.nmc_NNN</c>), read off the third-issue ground (G3) by
    /// <c>tools/nmc-walls</c>, and written into the scene by text in N1 step <see cref="NineMileCreekGroundSteps.WallsStep"/>.
    /// This holds the three together: the Defs, the walls that step writes and G3. St Peters' twin is
    /// <c>StPetersWallsAgreeWithGroundTests</c>.
    ///
    /// <list type="bullet">
    /// <item><b>The ground, station by station:</b> a station end read off G3 sits on it within 2 cm; an end that kept the
    /// scene's own height is within 10 cm of G3 (the kept rule), but a banked wall's toe, which stands as built where G3's
    /// face is one the default bake does not draw (plan B).</item>
    /// <item><b>The scene:</b> a kept end is the committed scene's own, to the bit.</item>
    /// <item><b>The faces:</b> no wall stands where its face is under the builder's minimum; each is a class the plan calls
    /// rock, wears a face the aspect law allows and sorts over no more world Y than the cut allows.</item>
    /// <item><b>The ids:</b> 000 to 117 with no gap, each live, retired or held; each live id one wall in the scene step 11
    /// writes, named by its Def; a retired or held id none.</item>
    /// <item><b>The round trip:</b> the walls the Defs make are the ones step 11 writes, to the bit, and planning it again
    /// is empty; it deletes only the retired walls, four documents each, and keeps every standing wall's ids.</item>
    /// <item><b>The openings:</b> the ramp's eight (the chunks step 07 stood down) are retired and no wall stands in their
    /// place; no wall is drawn over the berth, the gangway or Wharf Road.</item>
    /// </list>
    ///
    /// <para><b>It needs no faces.</b> Every claim reads the Defs, the scene's text and G3; no baked PNG is loaded and no
    /// wall is built, so it holds on a CI runner, which has none. The committed scene is read and step 11 applied to it in
    /// memory: the file is not written.</para>
    /// </summary>
    public class NineMileCreekWallsAgreeWithGroundTests
    {
        /// <summary>A station end read off G3 sits on it within this (m; St Peters' measured rule).</summary>
        const float MeasuredWithinMetres = 0.02f;

        /// <summary>An end that kept the scene's height stands within this of G3 (m; the kept rule).</summary>
        const float KeptWithinMetres = 0.10f;

        /// <summary>The walls by real id after H3's Phase A: the scene's 86 (000 to 085) by their own ids, 49 standing in
        /// 49 Defs. Three were split (021 to 023 each give a new id, 086 to 088); 046 to 085 retire, the ramp's eight among
        /// them; 089 to 117 are held, the faces G3 asks for that the scene does not wear yet (the south shore's bluff
        /// first).</summary>
        const int DefCount = 118, LiveCount = 49, SceneCount = 86, FirstNew = 86;
        const int FirstRetired = 46, LastRetired = 85, FirstHeld = 89, LastHeld = 117;

        /// <summary>The banked walls, standing as built (plan B): G3's face under their toes is 42 to 55°.</summary>
        static readonly int[] Banked = Enumerable.Range(24, 22).Append(88).ToArray();

        /// <summary>The joins where two standing walls share a brow station but not its toe, so the second follows none and
        /// starts its own run (its <c>u</c> from 0): the committed scene's toe line already jumped at each.</summary>
        static readonly string[] BrowOnlyJoins = { "016-017", "035-036" };

        /// <summary>G3's extent, either side of its centre at the origin (m).</summary>
        static readonly Vector2 HalfExtent = new Vector2(380f, 280f);

        string _committed;
        List<CliffWallDef> _defs;
        List<CliffWallDef> _live;
        Dictionary<string, StPetersCliffWalls.SceneWall> _before;
        LayerPatch _step;
        string _after;
        List<StPetersCliffWalls.SceneWall> _walls;
        PaintedHeightField _ground;

        [OneTimeSetUp]
        public void ReadTheDefsTheSceneAndG3() => Read(NineMileCreekCliffWalls.LoadDefs());

        void Read(List<CliffWallDef> defs)
        {
            _defs = defs;
            _live = _defs.Where(d => d.IsLive).ToList();
            _committed = File.ReadAllText(NineMileCreekLayerRefresh.ScenePath);
            _before = StPetersCliffWalls.WallsInScene(_committed).ToDictionary(w => w.RealId);
            _step = CliffWalls(SceneYaml.Parse(_committed), _defs, NineMileCreekCliffWalls.ChunkToeSpanMetres,
                               NineMileCreekGroundSteps.WallsStep);
            _after = _step.ApplyTo(_committed);
            _walls = StPetersCliffWalls.WallsInScene(_after);
            _ground = NineMileCreekGround.Load().Field;
        }

        // =========================================================================================
        //  1. the ground, station by station
        // =========================================================================================

        /// <summary>⭐ A station end read off G3 sits on G3 within 2 cm, live or held: its brow is its toe's height plus its
        /// drop, and G3 is read as the game reads it (<see cref="PaintedHeightField.ElevationAt"/>).</summary>
        [Test]
        public void AMeasuredStation_SitsOnG3_WithinTwoCentimetres()
        {
            int ends = 0;
            float worst = 0f;
            string worstAt = "";
            var over = new List<string>();
            foreach (CliffWallDef d in _defs.Where(d => d.Status != CliffWallStatus.Retired))
                for (int i = 0; i < d.Brow.Length; i++)
                {
                    if (d.BrowFrom[i] == CliffStationSource.Measured)
                        Check(d, i, "brow", _ground.ElevationAt(d.Brow[i]) - (d.ToeElevations[i] + d.DropMetres[i]));
                    if (d.ToeFrom[i] == CliffStationSource.Measured)
                        Check(d, i, "toe", _ground.ElevationAt(d.Toe[i]) - d.ToeElevations[i]);
                }

            void Check(CliffWallDef d, int i, string end, float off)
            {
                ends++;
                if (Mathf.Abs(off) > worst) { worst = Mathf.Abs(off); worstAt = $"{At(d, i)} {end}"; }
                if (Mathf.Abs(off) > MeasuredWithinMetres) over.Add($"{At(d, i)} {end} {off:F4} m");
            }

            Assert.Greater(ends, 0, "no station end was read off G3: this asserts nothing");
            Assert.IsEmpty(over, $"{over.Count} measured station ends sit off G3 by more than {MeasuredWithinMetres} m: " +
                                 string.Join("; ", over.Take(12)));
            Debug.Log($"[nmc-walls-on-g3] {ends} measured ends on G3, worst {worst:F4} m at {worstAt}");
        }

        /// <summary>⭐ An end that kept the scene's height stands within 10 cm of G3 — but a banked wall's toe, which stands
        /// as built (plan B), and is logged.</summary>
        [Test]
        public void AKeptStation_StandsWithinTenCentimetresOfG3_ButABankedWallsToe()
        {
            int ends = 0;
            float worst = 0f, bankedLeast = float.MaxValue, bankedMost = 0f;
            string worstAt = "";
            var over = new List<string>();
            foreach (CliffWallDef d in _live)
                for (int i = 0; i < d.Brow.Length; i++)
                {
                    if (d.BrowFrom[i] == CliffStationSource.Kept)
                        Check(d, i, "brow", _ground.ElevationAt(d.Brow[i]) - (d.ToeElevations[i] + d.DropMetres[i]));
                    if (d.ToeFrom[i] != CliffStationSource.Kept) continue;
                    float toe = Mathf.Abs(_ground.ElevationAt(d.Toe[i]) - d.ToeElevations[i]);
                    if (d.Status == CliffWallStatus.Banked)
                    {
                        bankedLeast = Mathf.Min(bankedLeast, toe);
                        bankedMost = Mathf.Max(bankedMost, toe);
                    }
                    else Check(d, i, "toe", toe);
                }

            void Check(CliffWallDef d, int i, string end, float off)
            {
                ends++;
                if (Mathf.Abs(off) > worst) { worst = Mathf.Abs(off); worstAt = $"{At(d, i)} {end}"; }
                if (Mathf.Abs(off) > KeptWithinMetres) over.Add($"{At(d, i)} {end} {off:F4} m");
            }

            Assert.Greater(ends, 0, "no station end kept the scene's height: this asserts nothing");
            Assert.IsEmpty(over, $"{over.Count} kept station ends stand more than {KeptWithinMetres} m off G3: " +
                                 string.Join("; ", over.Take(12)));
            CollectionAssert.AreEquivalent(Banked.Select(n => n.ToString("D3")),
                _live.Where(d => d.Status == CliffWallStatus.Banked).Select(d => d.RealId), "the banked walls");
            Debug.Log($"[nmc-walls-on-g3] {ends} kept ends within {KeptWithinMetres} m of G3, worst {worst:F4} m at {worstAt}; " +
                      $"the {Banked.Length} banked walls' toes stand {bankedLeast:F2} to {bankedMost:F2} m off it, as built");
        }

        /// <summary>⭐ A kept end is the committed scene's own, to the bit: the station its k names on the wall it came from
        /// (its own, or the one it was cut from). Where both ends kept, so did its drop and its toe's height.</summary>
        [Test]
        public void AKeptEnd_IsTheCommittedScenesOwn_ToTheBit()
        {
            int ends = 0;
            var off = new List<string>();
            foreach (CliffWallDef d in _live)
            {
                string from = string.IsNullOrEmpty(d.SplitFrom) ? d.RealId : d.SplitFrom.Substring(d.SplitFrom.Length - 3);
                Assert.IsTrue(_before.TryGetValue(from, out StPetersCliffWalls.SceneWall w), $"{d.Id}: the scene has no wall {from}");
                for (int i = 0; i < d.Brow.Length; i++)
                {
                    float k = d.Stations[i];
                    Assert.AreEqual(Mathf.Round(k), k, $"{At(d, i)}: a station between two whole ones");
                    int s = (int)k;
                    bool brow = d.BrowFrom[i] == CliffStationSource.Kept, toe = d.ToeFrom[i] == CliffStationSource.Kept;
                    if (brow) { ends++; if (!Bit(d.Brow[i], w.Brow[s])) off.Add($"{At(d, i)} brow"); }
                    if (toe) { ends++; if (!Bit(d.Toe[i], w.Toe[s])) off.Add($"{At(d, i)} toe"); }
                    if (brow && toe && (d.DropMetres[i] != w.DropMetres[s] || d.ToeElevations[i] != w.ToeElevations[s]))
                        off.Add($"{At(d, i)} heights");
                }
            }
            Assert.Greater(ends, 0, "no kept end: this asserts nothing");
            Assert.IsEmpty(off, $"kept ends that are not the scene's: {string.Join("; ", off.Take(20))}");
        }

        /// <summary>Every station stands on G3's map, live, retired or held: its brow and its toe inside G3's extent. An
        /// open run's walls stand on the ground, never off its edge.</summary>
        [Test]
        public void EveryStation_StandsInsideG3sExtent()
        {
            var outside = new List<string>();
            foreach (CliffWallDef d in _defs)
                for (int i = 0; i < d.Brow.Length; i++)
                    foreach ((string end, Vector2 p) in new[] { ("brow", d.Brow[i]), ("toe", d.Toe[i]) })
                        if (Mathf.Abs(p.x) > HalfExtent.x || Mathf.Abs(p.y) > HalfExtent.y) outside.Add($"{At(d, i)} {end} {p}");
            Assert.IsEmpty(outside, $"stations off G3's {HalfExtent * 2f} m map: {string.Join("; ", outside.Take(12))}");
        }

        // =========================================================================================
        //  2. the faces
        // =========================================================================================

        /// <summary>No wall stands where its face is under the builder's minimum: every station drops at least
        /// <see cref="NineMileCreekCliffWalls.MinFaceDropMetres"/> and is battered at least
        /// <see cref="NineMileCreekCliffWalls.MinBatterDegrees"/>.</summary>
        [Test]
        public void NoWallStandsWhereItsFaceIsUnderTheBuildersMinimum()
        {
            float leastDrop = float.MaxValue, leastBatter = float.MaxValue;
            string dropAt = "", batterAt = "";
            var under = new List<string>();
            foreach (CliffWallDef d in _live)
                for (int i = 0; i < d.Brow.Length; i++)
                {
                    var s = new CliffWallSample(d.Brow[i], d.Toe[i], d.DropMetres[i], d.ToeElevations[i]);
                    float batter = CliffWallGeometry.BatterDegrees(in s);
                    if (s.DropMetres < leastDrop) { leastDrop = s.DropMetres; dropAt = At(d, i); }
                    if (batter < leastBatter) { leastBatter = batter; batterAt = At(d, i); }
                    if (s.DropMetres < NineMileCreekCliffWalls.MinFaceDropMetres - 1e-3f ||
                        batter < NineMileCreekCliffWalls.MinBatterDegrees - 1e-3f)
                        under.Add($"{At(d, i)} ({s.DropMetres:F3} m, {batter:F2}°)");
                }

            Assert.IsEmpty(under, $"{under.Count} stations stand with a face under the builder's minimum " +
                                  $"({NineMileCreekCliffWalls.MinFaceDropMetres} m, {NineMileCreekCliffWalls.MinBatterDegrees}°): " +
                                  string.Join("; ", under.Take(12)));
            Debug.Log($"[nmc-walls-on-g3] least drop {leastDrop:F3} m at {dropAt}; least batter {leastBatter:F2}° at {batterAt}");
        }

        /// <summary>Each live wall is a class the plan calls rock, and wears a face the aspect law allows: its true azimuth
        /// within <see cref="CoastPlan.AspectSnapToleranceDegrees"/> of an authored facing. (Replaces the premise's
        /// <c>EveryCliffChunkStandsOnASectorThePlanCallsRock</c> and <c>EveryChunkWearsAFaceTheAspectLawAllows</c>.)</summary>
        [Test]
        public void EveryWall_IsAClassThePlanCallsRock_AndWearsAFaceTheAspectLawAllows()
        {
            List<StPetersCliffWalls.Chunk> chunks = Chunks(out List<CliffWallDef> owners);
            var wrong = new List<string>();
            for (int k = 0; k < chunks.Count; k++)
            {
                if (!CoastPlan.IsCliff(chunks[k].Class)) wrong.Add($"{owners[k].RealId} is {chunks[k].Class}");
                float error = CoastPlan.AspectSnapError(chunks[k].WallAzimuth);
                if (error > CoastPlan.AspectSnapToleranceDegrees + 0.01f)
                    wrong.Add($"{owners[k].RealId} faces {chunks[k].WallAzimuth:F1}°, {error:F1}° from a facing");
                if (CliffCatalog.Aspects[chunks[k].AspectIndex] != owners[k].Aspect)
                    wrong.Add($"{owners[k].RealId} wears {CliffCatalog.Aspects[chunks[k].AspectIndex]}, not its Def's {owners[k].Aspect}");
            }
            Assert.IsEmpty(wrong, string.Join("; ", wrong));
        }

        /// <summary>No wall sorts over more world Y than one character is tall: a chunk has one sorting order, and its foot
        /// wanders at most <see cref="NineMileCreekCliffWalls.ChunkToeSpanMetres"/> (ADR 0032). (Replaces the premise's
        /// <c>NoChunkSortsOverMoreWorldYThanOneCharacterIsTall</c>.)</summary>
        [Test]
        public void NoWallSortsOverMoreWorldYThanOneCharacterIsTall()
        {
            float worst = 0f;
            string worstId = "";
            List<StPetersCliffWalls.Chunk> chunks = Chunks(out List<CliffWallDef> owners);
            for (int k = 0; k < chunks.Count; k++)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (CliffWallSample s in chunks[k].Samples)
                {
                    float y = CliffWallGeometry.ToeScreen(s).y;
                    lo = Mathf.Min(lo, y);
                    hi = Mathf.Max(hi, y);
                }
                if (hi - lo > worst) { worst = hi - lo; worstId = owners[k].RealId; }
            }
            Assert.That(worst, Is.LessThanOrEqualTo(NineMileCreekCliffWalls.ChunkToeSpanMetres + 0.01f),
                $"wall {worstId}'s foot wanders {worst:F2} m of world Y");
            Debug.Log($"[nmc-walls-on-g3] the most any wall's foot wanders: {worst:F2} m ({worstId})");
        }

        /// <summary>The walls the Defs make are cut the same twice: the chunk math is a pure function of the Defs (rule 5).
        /// (Replaces the premise's <c>TheCliffWallsResolveIdenticallyTwice</c>.)</summary>
        [Test]
        public void TheDefsCutTheSameWallsTwice()
        {
            List<StPetersCliffWalls.Chunk> a = Chunks(out _), b = Chunks(out _);
            Assert.AreEqual(LiveCount, a.Count, "one chunk per live Def");
            Assert.AreEqual(a.Count, b.Count, "two cuts must make the same chunks");
            for (int k = 0; k < a.Count; k++)
            {
                StPetersCliffWalls.ChunkFields fa = StPetersCliffWalls.FieldsOf(a[k]), fb = StPetersCliffWalls.FieldsOf(b[k]);
                Assert.IsTrue(Same(fa.BrowPlan, fb.BrowPlan) && Same(fa.ToePlan, fb.ToePlan) && Same(fa.DropMetres, fb.DropMetres),
                    $"chunk {k} moved");
                Assert.AreEqual(fa.AlongOffsetMetres, fb.AlongOffsetMetres, $"chunk {k} changed its u origin");
                Assert.AreEqual(fa.RowsBasisSurfaceMetres, fb.RowsBasisSurfaceMetres, $"chunk {k} changed its rows");
            }
        }

        // =========================================================================================
        //  3. the ids and the round trip
        // =========================================================================================

        /// <summary>⭐ The ids run from 000 to 117 with no gap, each live, retired or held: the 49 live, the three new each
        /// cut from a wall (086 to 088, from 021 to 023), the retired 046 to 085 and the held 089 to 117. A retired id is
        /// never a live wall's run or source.</summary>
        [Test]
        public void TheIdsRunWithNoGap_EachLiveRetiredOrHeld()
        {
            Assert.AreEqual(DefCount, _defs.Count, "the walls' Defs");
            for (int n = 0; n < _defs.Count; n++)
                Assert.AreEqual($"wall.nmc_{n:D3}", _defs[n].Id, $"the ids skip or repeat at {n:D3}");

            Assert.AreEqual(LiveCount, _live.Count, "the live walls");
            var retired = new HashSet<string>();
            foreach (CliffWallDef d in _defs)
            {
                int n = int.Parse(d.RealId);
                bool shouldRetire = n >= FirstRetired && n <= LastRetired, shouldHold = n >= FirstHeld && n <= LastHeld;
                Assert.AreEqual(shouldRetire, d.Status == CliffWallStatus.Retired, $"{d.Id} is {d.Status}");
                Assert.AreEqual(shouldHold, d.Status == CliffWallStatus.Held, $"{d.Id} is {d.Status}");
                Assert.AreEqual(n >= FirstNew && n < FirstHeld, !string.IsNullOrEmpty(d.SplitFrom),
                    $"{d.Id}: a new id is cut from a wall, and only a new live id is ({d.SplitFrom})");
                Assert.AreEqual(n < SceneCount, _before.ContainsKey(d.RealId),
                    $"{d.Id}: the committed scene's walls are 000 to {SceneCount - 1:D3}, by their own ids");
                if (d.Status == CliffWallStatus.Retired) retired.Add(d.Id);
            }
            Assert.AreEqual(SceneCount, _before.Count, "the committed scene's walls");
            foreach (CliffWallDef d in _live)
            {
                Assert.IsFalse(retired.Contains(d.Follows), $"{d.Id} follows {d.Follows}, which is retired");
                Assert.IsFalse(retired.Contains(d.SplitFrom), $"{d.Id} is cut from {d.SplitFrom}, which is retired");
            }
        }

        /// <summary>⭐ Each live id has one wall in the scene step 11 writes, named by its Def; a retired or held id has
        /// none.</summary>
        [Test]
        public void EachLiveIdHasOneWall_InTheSceneStep11Writes_NamedByItsDef_AndNoRetiredOrHeldIdHasOne()
        {
            Dictionary<string, StPetersCliffWalls.SceneWall> byId = _walls.ToDictionary(w => w.RealId);
            Assert.AreEqual(_live.Count, _walls.Count, "the walls step 11 writes against the live Defs");
            foreach (CliffWallDef d in _defs)
            {
                bool stands = byId.TryGetValue(d.RealId, out StPetersCliffWalls.SceneWall w);
                Assert.AreEqual(d.IsLive, stands, $"{d.Id} is {d.Status}, and step 11 {(stands ? "writes" : "writes no")} wall {d.RealId}");
                if (stands) Assert.AreEqual(StPetersCliffWalls.WallName(d), w.Name, $"wall {d.RealId} is not named by its Def");
            }
        }

        /// <summary>⭐ The round trip: the walls the Defs make (<see cref="StPetersCliffWalls.ChunksOfDefs"/>, as the builder
        /// lays them, at <see cref="NineMileCreekCliffWalls.ChunkToeSpanMetres"/>) are the walls step 11 writes, station for
        /// station and field for field, to the bit.</summary>
        [Test]
        public void TheDefsAndTheWallsStep11Writes_AgreeToTheBit()
        {
            List<StPetersCliffWalls.Chunk> chunks = Chunks(out List<CliffWallDef> owners);
            Assert.AreEqual(_live.Count, chunks.Count, "one chunk per live Def");
            Dictionary<string, StPetersCliffWalls.SceneWall> byId = _walls.ToDictionary(w => w.RealId);
            var off = new List<string>();
            for (int k = 0; k < chunks.Count; k++)
            {
                CliffWallDef d = owners[k];
                StPetersCliffWalls.ChunkFields f = StPetersCliffWalls.FieldsOf(chunks[k]);
                Assert.IsTrue(byId.TryGetValue(d.RealId, out StPetersCliffWalls.SceneWall w), $"no wall for {d.Id}");
                if (!Same(f.BrowPlan, w.Brow)) off.Add($"{d.RealId} brow");
                if (!Same(f.ToePlan, w.Toe)) off.Add($"{d.RealId} toe");
                if (!Same(f.DropMetres, w.DropMetres)) off.Add($"{d.RealId} drop");
                if (!Same(f.ToeElevations, w.ToeElevations)) off.Add($"{d.RealId} toe heights");
                if (!Same(f.TextureBrowPlan, w.TextureBrow)) off.Add($"{d.RealId} texture brow");
                if (!Same(f.SliceAt, w.SliceAt)) off.Add($"{d.RealId} slices");
                if (f.AlongOffsetMetres != w.AlongOffsetMetres) off.Add($"{d.RealId} along {f.AlongOffsetMetres:R} vs {w.AlongOffsetMetres:R}");
                if (f.RowsBasisSurfaceMetres != w.RowsBasisSurfaceMetres) off.Add($"{d.RealId} rows basis {f.RowsBasisSurfaceMetres:R} vs {w.RowsBasisSurfaceMetres:R}");
                if (f.WallAzimuth != w.WallAzimuth) off.Add($"{d.RealId} azimuth {f.WallAzimuth:R} vs {w.WallAzimuth:R}");
                if (f.Batter != w.Batter) off.Add($"{d.RealId} batter {f.Batter:R} vs {w.Batter:R}");
            }
            Assert.IsEmpty(off, $"the Defs' walls are not the ones step 11 writes: {string.Join("; ", off.Take(20))}");

            // The run number is not written: a run is its along and its rows basis on each face, held above to the bit.
            // The scene's reader chains walls by brow alone (WallsInScene), the Defs by the whole station (Follows), so
            // they count runs apart only where two walls share a brow and not its toe. Those joins are named.
            var browOnly = new List<string>();
            foreach (CliffWallDef a in _live)
            foreach (CliffWallDef b in _live)
                if (a != b && a.Brow[a.Brow.Length - 1].Equals(b.Brow[0]) && b.Follows != a.Id)
                    browOnly.Add($"{a.RealId}-{b.RealId}");
            CollectionAssert.AreEquivalent(BrowOnlyJoins, browOnly, "the walls that share a brow and follow none");
        }

        /// <summary>⭐ Step 11 deletes only the retired walls, each one's four documents, and keeps every standing wall's
        /// ids (a split's new wall takes new ones); planned again on what it writes, it is empty.</summary>
        [Test]
        public void Step11_DeletesOnlyTheRetiredWalls_KeepsTheStandingWallsIds_AndPlannedAgainIsEmpty()
        {
            var after = _walls.ToDictionary(w => w.RealId);
            SceneYaml scene = SceneYaml.Parse(_after);
            foreach (CliffWallDef d in _defs.Where(d => _before.ContainsKey(d.RealId)))
            {
                StPetersCliffWalls.SceneWall was = _before[d.RealId];
                long[] ids = { was.GameObject, was.Transform, was.SortingGroup, was.Surface };
                if (d.Status == CliffWallStatus.Retired)
                    Assert.IsTrue(ids.All(id => scene.Get(id) == null), $"{d.Id} is retired, and step 11 keeps a document of its wall");
                else
                    Assert.AreEqual(ids, new[] { after[d.RealId].GameObject, after[d.RealId].Transform,
                                                 after[d.RealId].SortingGroup, after[d.RealId].Surface }, $"{d.Id} lost its ids");
            }
            var kept = new HashSet<long>(_before.Values.SelectMany(w => new[] { w.GameObject, w.Transform, w.SortingGroup, w.Surface }));
            foreach (CliffWallDef d in _live.Where(d => !_before.ContainsKey(d.RealId)))
            {
                StPetersCliffWalls.SceneWall w = after[d.RealId];
                Assert.IsFalse(new[] { w.GameObject, w.Transform, w.SortingGroup, w.Surface }.Any(kept.Contains),
                    $"{d.Id} is new, and takes an id the scene's walls had");
            }

            LayerPatch again = CliffWalls(SceneYaml.Parse(_after), _defs, NineMileCreekCliffWalls.ChunkToeSpanMetres,
                                          NineMileCreekGroundSteps.WallsStep);
            Assert.IsTrue(again.IsEmpty, "step 11 planned on what it writes is not empty:\n" + again.Summary());
            Debug.Log("[nmc-walls-on-g3] " + _step.Summary().Split('\n')[0]);
        }

        // =========================================================================================
        //  4. the openings
        // =========================================================================================

        /// <summary>⭐ The ramp's opening: the eight chunks step 07 stood down across Wharf Road's ramp are retired, step 11
        /// writes no wall of theirs, and no live wall stands where they stood (their footprints, brow to toe, as their Defs
        /// keep them). (Takes over <c>NineMileCreekSceneRoutesTests.OnlyRow9sEightCliffFacesStandDown_TheirIdsAndMaterialKept</c>
        /// once step 11 is applied.)</summary>
        [Test]
        public void TheRampsOpening_TheEightStep07StoodDownAreRetired_AndNoWallStandsWhereTheyStood()
        {
            Assert.AreEqual(8, NineMileCreekGroundSteps.Pr2CliffChunks.Length, "the ramp's eight");
            var names = new HashSet<string>(_walls.Select(w => w.Name));
            var footprints = new List<Vector2[]>();
            foreach (string chunk in NineMileCreekGroundSteps.Pr2CliffChunks)
            {
                string realId = chunk.Substring(chunk.Length - 3);
                CliffWallDef d = _defs.Single(x => x.RealId == realId);
                Assert.AreEqual(CliffWallStatus.Retired, d.Status, $"{chunk} stood down for the ramp, and its Def is {d.Status}");
                Assert.IsFalse(names.Contains(chunk), $"step 11 writes {chunk}");
                footprints.Add(Footprint(d.Brow, d.Toe));
            }
            var inside = new List<string>();
            foreach (CliffWallDef d in _live)
                for (int i = 0; i < d.Brow.Length; i++)
                    if (footprints.Any(f => Inside(d.Brow[i], f) || Inside(d.Toe[i], f))) inside.Add(At(d, i));
            Assert.IsEmpty(inside, $"walls stand in the ramp's opening: {string.Join("; ", inside.Take(12))}");
        }

        /// <summary>No wall is drawn over the loop's way ashore: the berth's arrival, dock zone, disembark point and ladder,
        /// the gangway's shore end and Wharf Road. A wall is a picture (<see cref="CliffWallSurface"/> walks nothing), so it
        /// cannot close a route; one drawn across it would read as if it did.</summary>
        [Test]
        public void NoWallIsDrawnOverTheBerthTheGangwayOrWharfRoad()
        {
            var points = new (string, Vector2)[]
            {
                ("the arrival", G.ArrivalPos), ("the dock zone", G.DockZonePos), ("the disembark point", G.DisembarkPos),
                ("the berth's ladder", G.BerthLadderPos), ("the gangway's shore end", G.GangwayShoreEnd),
            };
            var over = new List<string>();
            float nearest = float.MaxValue;
            string nearestId = "";
            foreach (CliffWallDef d in _live)
            {
                Vector2[] foot = Footprint(d.Brow, d.Toe);
                foreach ((string what, Vector2 p) in points)
                {
                    if (Inside(p, foot)) over.Add($"{d.RealId} over {what}");
                    float r = ToPolygonEdge(p, foot);
                    if (r < nearest) { nearest = r; nearestId = $"{d.RealId} ({what})"; }
                }
                for (int j = 0; j + 1 < G.WharfRoad.Length; j++)
                    if (Crosses(foot, G.WharfRoad[j], G.WharfRoad[j + 1]) || Inside(G.WharfRoad[j], foot))
                        over.Add($"{d.RealId} over Wharf Road");
            }
            Assert.IsEmpty(over, string.Join("; ", over.Distinct()));
            Debug.Log($"[nmc-walls-on-g3] the nearest wall to the berth's points: {nearestId}, {nearest:F2} m");
        }

        /// <summary>A held wall stands in no scene and names the face it needs: a forecast for the faces G3 asks for that
        /// the scene does not wear yet.</summary>
        [Test]
        public void AHeldWall_StandsInNoScene_AndNamesTheFaceItNeeds()
        {
            List<CliffWallDef> held = _defs.Where(d => d.Status == CliffWallStatus.Held).ToList();
            Assert.AreEqual(LastHeld - FirstHeld + 1, held.Count, "the held walls");
            var names = new HashSet<string>(_walls.Select(w => w.RealId));
            foreach (CliffWallDef d in held)
            {
                Assert.IsFalse(names.Contains(d.RealId), $"{d.Id} is held, and step 11 writes it");
                Assert.IsFalse(string.IsNullOrEmpty(d.FaceNeeded), $"{d.Id} is held, and names no face");
                Assert.Greater(d.Brow.Length, 1, $"{d.Id} is held, and carries no lines");
            }
        }

        // =========================================================================================
        //  helpers
        // =========================================================================================

        List<StPetersCliffWalls.Chunk> Chunks(out List<CliffWallDef> owners) =>
            StPetersCliffWalls.ChunksOfDefs(_defs, NineMileCreekCliffWalls.ChunkToeSpanMetres, out owners);

        static string At(CliffWallDef d, int i) => $"{d.RealId} k{d.Stations[i].ToString(CultureInfo.InvariantCulture)}";

        /// <summary>A wall's footprint: its brow, then its toe back.</summary>
        static Vector2[] Footprint(Vector2[] brow, Vector2[] toe) => brow.Concat(Enumerable.Reverse(toe)).ToArray();

        /// <summary>Even-odd: inside the closed polygon.</summary>
        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Any edge of the closed polygon crosses the segment.</summary>
        static bool Crosses(Vector2[] poly, Vector2 q, Vector2 q2)
        {
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (SegmentsCross(poly[j], poly[i], q, q2)) return true;
            return false;
        }

        static bool SegmentsCross(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
        {
            float d1 = Cross(q2 - q, p - q), d2 = Cross(q2 - q, p2 - q), d3 = Cross(p2 - p, q - p), d4 = Cross(p2 - p, q2 - p);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 v = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, v) / Vector2.Dot(v, v));
            return Vector2.Distance(p, a + v * t);
        }

        static float ToPolygonEdge(Vector2 p, Vector2[] poly)
        {
            float d = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) d = Mathf.Min(d, ToSegment(p, poly[j], poly[i]));
            return d;
        }

        /// <summary>The same point, to the bit (Vector2's == is approximate).</summary>
        static bool Bit(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;

        static bool Same(Vector2[] a, Vector2[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i].x != b[i].x || a[i].y != b[i].y) return false;
            return true;
        }

        static bool Same(float[] a, float[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
