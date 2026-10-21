using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>ST PETERS' 1 M CUT DRAWS THE FACE ITS WALLS HAD (terrain PR 5w; the coordinator's rulings 1 and 2, 10-05).</b>
    /// The cut sets stations between whole ones and splits walls into pieces so each sorts honestly; it may not change the
    /// picture. <see cref="CliffWallSurface"/> lays a cut wall on its whole stations and slices it (its class note), and
    /// this holds the result against St Peters' walls as the scene held them before the cut (<see cref="FixturePath"/>):
    ///
    /// <list type="bullet">
    /// <item><b>Untouched walls, to the bit:</b> a wall the cut left as it was (no station added, none moved, not split;
    /// 31 of St Peters') lays the mesh it laid before, every vertex, channel and index. So does every Nine Mile Creek wall,
    /// which the cut never reaches.</item>
    /// <item><b>Cut walls, slice by slice:</b> every triangle a cut wall or one of its pieces draws lies inside one the wall
    /// drew before, with the same texture mapping (the rock, its elevation and its sea plan are where they were at its
    /// uv), and each old triangle is covered once: no hole, no overlap. Each piece wears its wall's face.</item>
    /// <item><b>The four tops (item 4):</b> only 061 k3, 061 k4, 064 k9 and 171 k10 moved, and the texture still runs
    /// where it ran (<c>_textureBrowPlan</c>), so the wall either side of them slides no texture.</item>
    /// <item><b>Plate S5:</b> wall 070 keeps its texture placement, station for station.</item>
    /// </list>
    ///
    /// <para>Both meshes are laid with one stand-in profile (<see cref="Displacement"/>): a slice mixes the vertices the
    /// whole face laid, wherever the profile put them, so the claim does not hang on the kit's picture. The face as it was
    /// laid before is frozen here (<see cref="BeforeFace"/>, <see cref="BeforeDecal"/>), verbatim but for the fields it
    /// read, which it is handed.</para>
    /// </summary>
    public class StPetersWallSlicesTests
    {
        /// <summary>St Peters' 109 live walls as the scene held them at 98044649, before the cut, every float as its bits.</summary>
        internal const string FixturePath = "Assets/Tests/EditMode/Fixtures/StPetersWallsBeforeTheRecut.json";

        /// <summary>Nine Mile Creek's scene: the cut is St Peters' alone.</summary>
        internal const string NineMileCreekScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";

        /// <summary>The four tops the cut moved out (item 4), by the wall that held them and its station.</summary>
        static readonly string[] MovedTops = { "061 k3", "061 k4", "064 k9", "171 k10" };

        /// <summary>The walls that hold a moved top, and so carry where their texture runs: 061, 171, and 247, the piece
        /// of 064 that holds its k9.</summary>
        static readonly string[] TextureBrowWalls = { "061", "171", "247" };

        /// <summary>Plate S5's wall (ruling 2).</summary>
        const string PlateS5Wall = "070";

        /// <summary>St Peters' walls the cut left as they were.</summary>
        const int LeftWhole = 31;

        /// <summary>How far a slice may sit off the face it was cut from: a millimetre, where a texel is 31 mm.</summary>
        const float MetresWithin = 1e-3f;

        /// <summary>How far a texture may slide, in tiles of the 12 m face: 0.12 mm.</summary>
        const float TilesWithin = 1e-5f;

        /// <summary>How far outside an old triangle a new vertex may fall, as a share of that triangle.</summary>
        const double BaryWithin = 1e-3;

        /// <summary>A triangle this small in uv (tiles²) draws nothing, and is not judged.</summary>
        const double DegenerateTiles2 = 1e-9;

        /// <summary>The profile read both meshes are laid with, (u, t) → metres: any smooth field will do.</summary>
        static readonly Func<float, float, float> Displacement = (u, t) =>
            0.6f * Mathf.Sin(2f * Mathf.PI * (3.7f * u + 1.3f * t)) + 0.3f * Mathf.Cos(2f * Mathf.PI * (11.1f * u - 2.9f * t));

        /// <summary>The fixture as written: the face's globals and the walls, every float as its bits.</summary>
        [Serializable]
        public sealed class Fixture
        {
            public string Note;
            public int FaceMetresS, FaceMetresT, SubdivideMetres, StripMetresT, BrowLineAt;
            public FixtureWall[] Walls;
        }

        /// <summary>One wall of the fixture: its <see cref="CliffWallSurface"/>'s fields, its position, and its Def's
        /// root and stations.</summary>
        [Serializable]
        public sealed class FixtureWall
        {
            public string Name, RealId, Root;
            public int[] Brow, Toe, Drop, ToeZ, Position, BandStart, BandEnd, K;
            public int Along, RowsBasis, Azimuth, Batter;
            public bool HasBrowDecal, HasToeDecal;
        }

        /// <summary>One wall as its mesh is laid: what <see cref="CliffWallSurface"/> reads to lay it, and where its
        /// stations stand on the wall they are numbered on.</summary>
        sealed class Wall
        {
            public string Name, RealId, Root;
            public float[] Stations;
            public CliffWallSample[] Samples;
            public Vector2[] TextureBrow = new Vector2[0];
            public float[] SliceAt = new float[0];
            public CliffWallSliceEnd[] SliceBefore = new CliffWallSliceEnd[0], SliceAfter = new CliffWallSliceEnd[0];
            public float Along, RowsBasis, FaceS, FaceT, Subdivide, Strip, BrowLine, ElevationValid;
            public Vector3 Origin;
            public float[] BandStart, BandEnd;
            public bool BrowDecal, ToeDecal;

            public bool IsCut(int j) => SliceAt.Length > 0 && SliceAt[j] > 0f;
            public bool AnyCut => SliceAt.Any(f => f > 0f);

            public int Rows => CliffWallGeometry.RowsFor(
                RowsBasis > 0f ? RowsBasis : CliffWallGeometry.RowsBasisSurfaceMetres(Samples), Subdivide);

            public string At(int i) => RealId + " k" + Stations[i].ToString(CultureInfo.InvariantCulture);
        }

        Fixture _fixture;
        List<Wall> _before, _now, _nineMileCreek;
        Dictionary<string, Wall> _beforeById, _ownerOf;
        Dictionary<string, List<Wall>> _pieces;

        [OneTimeSetUp]
        public void ReadTheWalls()
        {
            _fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(FixturePath));
            Read(StPetersCliffWalls.LoadDefs());
        }

        /// <summary>The walls before (the fixture), now (the scene, numbered by their Defs) and Nine Mile Creek's, and
        /// which wall before each wall now was cut from: the one of its root whose stations hold its own.</summary>
        void Read(List<CliffWallDef> defs)
        {
            Assert.IsNotNull(_fixture?.Walls, $"no walls in {FixturePath}");
            _before = _fixture.Walls.Select(f => FromFixture(_fixture, f)).ToList();
            _beforeById = _before.ToDictionary(w => w.RealId);
            _now = FromScene(StPetersLayerRefreshTests.ScenePath);
            _nineMileCreek = FromScene(NineMileCreekScenePath);

            Dictionary<string, CliffWallDef> live = defs.Where(d => d.IsLive).ToDictionary(d => d.RealId);
            _ownerOf = new Dictionary<string, Wall>();
            _pieces = _before.ToDictionary(w => w.RealId, w => new List<Wall>());
            foreach (Wall w in _now)
            {
                Assert.IsTrue(live.TryGetValue(w.RealId, out CliffWallDef d), $"{w.Name} has no live Def");
                w.Root = string.IsNullOrEmpty(d.SplitFrom) ? d.RealId : d.SplitFrom.Substring(d.SplitFrom.Length - 3);
                w.Stations = d.Stations;
                Assert.AreEqual(w.Samples.Length, w.Stations.Length,
                    $"{w.Name} holds {w.Samples.Length} stations, and its Def numbers {w.Stations.Length}");
                List<Wall> owners = _before.Where(o => o.Root == w.Root && o.Stations[0] <= w.Stations[0] &&
                                                       w.Stations[w.Stations.Length - 1] <= o.Stations[o.Stations.Length - 1])
                                           .ToList();
                Assert.AreEqual(1, owners.Count,
                    $"{w.Name}'s stations ({w.Root} k{w.Stations[0]} to k{w.Stations[w.Stations.Length - 1]}) are held by " +
                    $"{owners.Count} walls before the cut, not one");
                _ownerOf.Add(w.RealId, owners[0]);
                _pieces[owners[0].RealId].Add(w);
            }
            foreach (List<Wall> pieces in _pieces.Values) pieces.Sort((a, b) => a.Stations[0].CompareTo(b.Stations[0]));
        }

        // =============================================================================================
        //  the walls the cut left as they were, and Nine Mile Creek's: to the bit
        // =============================================================================================

        /// <summary>⭐ A wall the cut left as it was (no station added, none moved, not split) lays the mesh it laid before
        /// the cut, to the bit: every vertex, uv, waterline channel and index (ruling 1).</summary>
        [Test]
        public void TheWallsTheCutLeftWhole_LayTheMeshTheyLaid_ToTheBit()
        {
            int whole = 0;
            foreach (Wall old in _before)
            {
                List<Wall> pieces = _pieces[old.RealId];
                if (pieces.Count != 1) continue;
                Wall w = pieces[0];
                if (w.AnyCut || w.TextureBrow.Length > 0 || !w.Stations.SequenceEqual(old.Stations)) continue;
                whole++;
                Assert.AreEqual(old.Name, w.Name, $"{old.Name} was not cut, and is now {w.Name}");
                List<CliffWallMeshData> before = BeforeMeshes(old), now = NowMeshes(w);
                Assert.AreEqual(before.Count, now.Count, $"{w.Name} lays {now.Count} meshes, and laid {before.Count}");
                for (int m = 0; m < now.Count; m++) AssertSameBits(before[m], now[m], $"{w.Name}'s {MeshName(w, m)}");
            }
            Assert.AreEqual(LeftWhole, whole, $"the cut left {whole} of St Peters' walls as they were, not {LeftWhole}");
        }

        /// <summary>⭐ Every Nine Mile Creek wall holds no slice and lays the mesh it laid before the cut, to the bit
        /// (ruling 1): the slicing in <see cref="CliffWallSurface"/> changes nothing for a wall with no cut station.</summary>
        [Test]
        public void NineMileCreeksWalls_LayTheMeshTheyLaid_ToTheBit()
        {
            Assert.IsNotEmpty(_nineMileCreek, "no walls in Nine Mile Creek's scene");
            foreach (Wall w in _nineMileCreek)
            {
                Assert.IsTrue(w.SliceAt.Length == 0 && w.TextureBrow.Length == 0 && w.SliceBefore.Length == 0 &&
                              w.SliceAfter.Length == 0, $"{w.Name} holds slices; the cut is St Peters' alone");
                List<CliffWallMeshData> before = BeforeMeshes(w), now = NowMeshes(w);
                Assert.AreEqual(before.Count, now.Count, $"{w.Name} lays {now.Count} meshes, and laid {before.Count}");
                for (int m = 0; m < now.Count; m++) AssertSameBits(before[m], now[m], $"{w.Name}'s {MeshName(w, m)}");
            }
        }

        /// <summary>Every wall's slice data describes a slice: a wall whose data did not would draw its stations
        /// unsliced, and say so.</summary>
        [Test]
        public void EveryWallsSlices_Lay()
        {
            var refused = new List<string>();
            foreach (Wall w in _now.Concat(_nineMileCreek))
                if (!CliffWallSurface.TryColumns(w.Samples, w.TextureBrow, w.SliceAt, w.SliceBefore, w.SliceAfter,
                                                 out _, out string problem))
                    refused.Add($"{w.Name}: {problem}");
            Assert.IsEmpty(refused, "these walls' slices do not lay, and would draw unsliced:\n" + string.Join("\n", refused));
            Assert.IsTrue(_now.Any(w => w.AnyCut), "no St Peters wall is cut between its whole stations");
        }

        // =============================================================================================
        //  the cut walls: slices of the face they had
        // =============================================================================================

        /// <summary>
        /// ⭐ Every triangle a cut wall or piece draws lies inside one its wall drew before the cut, with the same texture
        /// mapping: at each of its corners the rock, its elevation and its sea plan are where the old triangle had them at
        /// that uv. And each old triangle is covered by them once, no hole and no overlap, so the cut draws the face the
        /// wall had (ruling 1). The pieces run their wall's stations end to end, and each wears its face: its class,
        /// aspect and batter, its bands, decals and rows. Beside the four moved tops (item 4) the face moved by ruling, and
        /// is held by <see cref="OnlyTheFourTopsMoved_AndTheTextureRunsWhereItRan"/> instead.
        /// </summary>
        [Test]
        public void EveryCutWall_DrawsSlicesOfTheFaceItHad()
        {
            int judged = 0, pieces = 0;
            var worst = new double[2];          // the farthest corner from the old face, metres: in plan, in height
            foreach (Wall old in _before)
            {
                List<Wall> cut = _pieces[old.RealId];
                Assert.IsNotEmpty(cut, $"nothing stands where {old.Name} stood");
                Assert.AreEqual(old.Stations[0], cut[0].Stations[0], $"{old.Name}'s first piece starts at {cut[0].At(0)}");
                for (int p = 1; p < cut.Count; p++)
                    Assert.AreEqual(cut[p - 1].Stations[cut[p - 1].Stations.Length - 1], cut[p].Stations[0],
                        $"{cut[p].Name} does not start where {cut[p - 1].Name} ends");
                Wall last = cut[cut.Count - 1];
                Assert.AreEqual(old.Stations[old.Stations.Length - 1], last.Stations[last.Stations.Length - 1],
                    $"{old.Name}'s last piece ends at {last.At(last.Stations.Length - 1)}");

                bool[] moved = MovedGaps(old);
                List<CliffWallMeshData> before = BeforeMeshes(old);
                List<double[]> covered = before.Select(m => new double[m.Tris.Length / 3]).ToList();
                foreach (Wall w in cut)
                {
                    pieces++;
                    AssertSameFace(old, w);
                    List<CliffWallMeshData> now = NowMeshes(w);
                    Assert.AreEqual(before.Count, now.Count, $"{w.Name} lays {now.Count} meshes, and {old.Name} laid {before.Count}");
                    for (int m = 0; m < now.Count; m++)
                        judged += Inside(old, before[m], w, now[m], moved, covered[m], worst, $"{w.Name}'s {MeshName(w, m)}");
                }
                for (int m = 0; m < before.Count; m++) AssertCovered(old, before[m], moved, covered[m], MeshName(old, m));
            }
            Assert.AreEqual(_now.Count, pieces, "a wall stands now that was not cut from a wall that stood");
            Debug.Log($"[wall-slices] {judged} triangles of St Peters' {pieces} walls lie inside the face of the " +
                      $"{_before.Count} walls they were cut from, and cover it once; the farthest corner stands " +
                      $"{worst[0] * 1e6:F2} µm off it in plan and {worst[1] * 1e6:F2} µm in height (bar {MetresWithin * 1e3:F0} mm).");
        }

        /// <summary>
        /// ⭐ The cut moved four tops out (item 4) and nothing else: of every whole station a wall draws from (its own, and
        /// the whole one past a cut end), only 061 k3, 061 k4, 064 k9 and 171 k10 stand anywhere but where they stood, and
        /// only their brows moved, each with the drop the ground gives it where it stands now; every toe and toe height
        /// stayed. Their texture runs where it ran: the walls that hold them carry their old brows as the
        /// brows the texture runs along (<c>_textureBrowPlan</c>; ruling 2, the texture pinned), so u at a moved top is
        /// the u it had, and no wall downstream slides. Every other station's texture runs along its own brow.
        /// </summary>
        [Test]
        public void OnlyTheFourTopsMoved_AndTheTextureRunsWhereItRan()
        {
            var moved = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Wall w in _now)
            {
                Wall old = _ownerOf[w.RealId];
                List<CliffWallMeshData> now = null, before = null;
                for (int j = 0; j < w.Stations.Length; j++)
                {
                    if (w.IsCut(j)) continue;       // a slice of the stations either side: the test above holds it
                    int i = StationOf(old, w.Stations[j], w.Name);
                    Vector2 texture = w.TextureBrow.Length > 0 ? w.TextureBrow[j] : w.Samples[j].BrowPlan;
                    if (!Moved(old, i, w.Samples[j], texture, w.Name, moved)) continue;
                    now = now ?? NowMeshes(w);
                    before = before ?? BeforeMeshes(old);
                    float was = before[0].Uvs[i * (old.Rows + 1)].x, isNow = now[0].Uvs[j * (w.Rows + 1)].x;
                    Assert.AreEqual(was, isNow, TilesWithin,
                        $"{old.At(i)} moved out, and its texture slid {(isNow - was) * old.FaceS * 1000f:F2} mm along the shore");
                }
                if (w.SliceBefore.Length > 0)
                {
                    int i = StationOf(old, Mathf.Floor(w.Stations[0]), w.Name);
                    Moved(old, i, w.SliceBefore[0].Sample, w.SliceBefore[0].TextureBrowPlan, w.Name, moved);
                }
                if (w.SliceAfter.Length > 0)
                {
                    int i = StationOf(old, Mathf.Ceil(w.Stations[w.Stations.Length - 1]), w.Name);
                    Moved(old, i, w.SliceAfter[0].Sample, w.SliceAfter[0].TextureBrowPlan, w.Name, moved);
                }
            }
            CollectionAssert.AreEquivalent(MovedTops, moved,
                $"the tops that moved are {string.Join(", ", moved)}, not {string.Join(", ", MovedTops)}");
            CollectionAssert.AreEquivalent(TextureBrowWalls, _now.Where(w => w.TextureBrow.Length > 0).Select(w => w.RealId),
                "the walls that carry where their texture runs are not the ones that hold a moved top");
        }

        /// <summary>
        /// ⭐ Plate S5's wall 070 keeps its texture placement (ruling 2): at each of its stations, in whichever piece holds
        /// it now, its texture's u is the u it had, within a hundred-thousandth of the face; and its first piece starts
        /// where 070 started along its run. The four tops upstream of it moved, and their texture did not (item 4).
        /// </summary>
        [Test]
        public void PlateS5sWall070_KeepsItsTexturePlacement()
        {
            Wall old = _beforeById[PlateS5Wall];
            List<Wall> pieces = _pieces[PlateS5Wall];
            CliffWallMeshData before = BeforeMeshes(old)[0];
            var held = new HashSet<float>();
            foreach (Wall w in pieces)
            {
                CliffWallMeshData now = NowMeshes(w)[0];
                for (int j = 0; j < w.Stations.Length; j++)
                {
                    if (w.IsCut(j)) continue;
                    int i = StationOf(old, w.Stations[j], w.Name);
                    float was = before.Uvs[i * (old.Rows + 1)].x, isNow = now.Uvs[j * (w.Rows + 1)].x;
                    Assert.AreEqual(was, isNow, TilesWithin,
                        $"plate S5: {w.Name} wears {old.At(i)}'s texture {(isNow - was) * old.FaceS * 1000f:F3} mm along " +
                        "the shore from where 070 wore it");
                    held.Add(w.Stations[j]);
                }
            }
            CollectionAssert.AreEquivalent(old.Stations, held, "070's pieces do not hold each of its stations whole");
            Assert.AreEqual(old.Along, pieces[0].Along, TilesWithin * old.FaceS,
                $"plate S5: {pieces[0].Name} starts {pieces[0].Along:F5} m along its run, and 070 started at {old.Along:F5} m");
        }

        // =============================================================================================
        //  checks
        // =============================================================================================

        /// <summary>Each of <paramref name="now"/>'s triangles (but those beside a moved top) inside one of
        /// <paramref name="before"/>'s, with the same mapping, its uv area added to that one's cover; the farthest corner
        /// kept in <paramref name="worst"/>. Returns how many.</summary>
        static int Inside(Wall old, CliffWallMeshData before, Wall w, CliffWallMeshData now, bool[] moved, double[] covered,
                          double[] worst, string what)
        {
            int columns = old.Samples.Length, rowVerts = before.Verts.Length / columns, rows = rowVerts - 1;
            var u = new float[columns];
            for (int c = 0; c < columns; c++) u[c] = before.Uvs[c * rowVerts].x;
            int judged = 0;
            for (int n = 0; n < now.Tris.Length; n += 3)
            {
                int i0 = now.Tris[n], i1 = now.Tris[n + 1], i2 = now.Tris[n + 2];
                double area = Area(now.Uvs[i0], now.Uvs[i1], now.Uvs[i2]);
                if (area < DegenerateTiles2) continue;
                double cu = ((double)now.Uvs[i0].x + now.Uvs[i1].x + now.Uvs[i2].x) / 3.0;
                double cv = ((double)now.Uvs[i0].y + now.Uvs[i1].y + now.Uvs[i2].y) / 3.0;
                int gap = 0;
                while (gap < columns - 2 && u[gap + 1] <= cu) gap++;
                if (moved[gap]) continue;

                int best = -1;
                double inside = double.NegativeInfinity;
                for (int t = gap * rows * 2; t < (gap + 1) * rows * 2; t++)
                {
                    int a = before.Tris[3 * t], b = before.Tris[3 * t + 1], c = before.Tris[3 * t + 2];
                    if (Area(before.Uvs[a], before.Uvs[b], before.Uvs[c]) < DegenerateTiles2) continue;
                    Bary(cu, cv, before.Uvs[a], before.Uvs[b], before.Uvs[c], out double la, out double lb, out double lc);
                    double least = Math.Min(la, Math.Min(lb, lc));
                    if (least > inside) { inside = least; best = t; }
                }
                // (Each check builds its message only when it fails: this runs for every corner of every piece.)
                if (best < 0 || inside < -BaryWithin)
                    Assert.Fail($"{what}: a triangle at uv ({cu:F5}, {cv:F5}) lies in none {old.Name} drew");

                int oa = before.Tris[3 * best], ob = before.Tris[3 * best + 1], oc = before.Tris[3 * best + 2];
                foreach (int k in new[] { i0, i1, i2 })
                {
                    Vector2 uv = now.Uvs[k];
                    Bary(uv.x, uv.y, before.Uvs[oa], before.Uvs[ob], before.Uvs[oc], out double la, out double lb, out double lc);
                    if (Math.Min(la, Math.Min(lb, lc)) < -BaryWithin)
                        Assert.Fail($"{what}: a corner at uv {uv} falls outside the triangle of {old.Name} its triangle lies in");
                    double x = la * before.Verts[oa].x + lb * before.Verts[ob].x + lc * before.Verts[oc].x + old.Origin.x;
                    double y = la * before.Verts[oa].y + lb * before.Verts[ob].y + lc * before.Verts[oc].y + old.Origin.y;
                    double off = Math.Sqrt(Sq(x - (now.Verts[k].x + (double)w.Origin.x)) +
                                           Sq(y - (now.Verts[k].y + (double)w.Origin.y)));
                    if (!(off <= MetresWithin))
                        Assert.Fail($"{what}: the rock at uv {uv} stands {off * 1000:F2} mm from where {old.Name} drew it");
                    worst[0] = Math.Max(worst[0], off);
                    if (now.Verts[k].z != 0f) Assert.Fail($"{what}: a vertex left the wall's plane");
                    double elevation = la * before.ElevationUv[oa].x + lb * before.ElevationUv[ob].x + lc * before.ElevationUv[oc].x;
                    double valid = la * before.ElevationUv[oa].y + lb * before.ElevationUv[ob].y + lc * before.ElevationUv[oc].y;
                    if (!(Math.Abs(elevation - now.ElevationUv[k].x) <= MetresWithin))
                        Assert.Fail($"{what}: the rock at uv {uv} stands at {now.ElevationUv[k].x:F4} m, and {old.Name} had it " +
                                    $"at {elevation:F4} m");
                    worst[1] = Math.Max(worst[1], Math.Abs(elevation - now.ElevationUv[k].x));
                    if (!(Math.Abs(valid - now.ElevationUv[k].y) <= 1e-4))
                        Assert.Fail($"{what}: the waterline's flag at uv {uv} is not {old.Name}'s");
                    double seaX = la * before.SeaPlanUv[oa].x + lb * before.SeaPlanUv[ob].x + lc * before.SeaPlanUv[oc].x;
                    double seaY = la * before.SeaPlanUv[oa].y + lb * before.SeaPlanUv[ob].y + lc * before.SeaPlanUv[oc].y;
                    double sea = Math.Sqrt(Sq(seaX - now.SeaPlanUv[k].x) + Sq(seaY - now.SeaPlanUv[k].y));
                    if (!(sea <= MetresWithin))
                        Assert.Fail($"{what}: the sea at uv {uv} is read {sea * 1000:F2} mm from where {old.Name} read it");
                }
                covered[best] += area;
                judged++;
            }
            return judged;
        }

        /// <summary>Each of <paramref name="before"/>'s triangles (but those beside a moved top, and those that draw
        /// nothing) covered once by the pieces' triangles that lie in it.</summary>
        static void AssertCovered(Wall old, CliffWallMeshData before, bool[] moved, double[] covered, string what)
        {
            int rows = before.Verts.Length / old.Samples.Length - 1;
            for (int t = 0; t < covered.Length; t++)
            {
                int gap = t / (rows * 2);
                if (moved[gap]) continue;
                double area = Area(before.Uvs[before.Tris[3 * t]], before.Uvs[before.Tris[3 * t + 1]], before.Uvs[before.Tris[3 * t + 2]]);
                if (area < DegenerateTiles2) continue;
                if (!(Math.Abs(covered[t] - area) <= 1e-3 * area + DegenerateTiles2))
                    Assert.Fail($"{old.Name}'s {what}: the triangle between {old.At(gap)} and {old.At(gap + 1)}, row " +
                                $"{t / 2 % rows}, is covered {covered[t] / area:P3} by the pieces cut from it: a hole or an overlap");
            }
        }

        /// <summary>A piece wears its wall's face: its class, aspect and batter (by name), its bands, decals, rows and the
        /// face's own metres.</summary>
        static void AssertSameFace(Wall old, Wall w)
        {
            string[] a = old.Name.Split('_'), b = w.Name.Split('_');
            Assert.AreEqual(string.Join("_", a, 1, 3), string.Join("_", b, 1, 3),
                $"{w.Name} does not wear {old.Name}'s face (its class, aspect and batter)");
            Assert.IsTrue(SameBits(old.BandStart, w.BandStart) && SameBits(old.BandEnd, w.BandEnd),
                $"{w.Name}'s bands are not {old.Name}'s");
            Assert.IsTrue(old.BrowDecal == w.BrowDecal && old.ToeDecal == w.ToeDecal, $"{w.Name}'s decals are not {old.Name}'s");
            Assert.IsTrue(SameBits(new[] { old.FaceS, old.FaceT, old.Subdivide, old.Strip, old.BrowLine, old.ElevationValid },
                                   new[] { w.FaceS, w.FaceT, w.Subdivide, w.Strip, w.BrowLine, w.ElevationValid }),
                $"{w.Name}'s face is not laid at {old.Name}'s metres");
            Assert.AreEqual(old.Rows, w.Rows, $"{w.Name} is laid on {w.Rows} rows, and {old.Name} on {old.Rows}");
        }

        /// <summary>Whether whole station <paramref name="i"/> of <paramref name="old"/> stands now anywhere but where it
        /// stood, named into <paramref name="moved"/>; and its texture runs where it ran, either way. A top that moved
        /// out stands on other ground, so its drop moved with it; its toe and toe height did not.</summary>
        static bool Moved(Wall old, int i, CliffWallSample now, Vector2 texture, string by, ISet<string> moved)
        {
            CliffWallSample was = old.Samples[i];
            bool brow = !SameBits(was.BrowPlan, now.BrowPlan);
            if (!SameBits(was.ToePlan, now.ToePlan) || !SameBits(was.ToeElevation, now.ToeElevation))
                Assert.Fail($"{by} holds {old.At(i)} with its toe or toe height moved");
            if (!brow && !SameBits(was.DropMetres, now.DropMetres))
                Assert.Fail($"{by} holds {old.At(i)} with its drop moved, and its brow where it stood");
            if (!SameBits(was.BrowPlan, texture))
                Assert.Fail($"{by}'s texture runs along {texture} at {old.At(i)}, not where it ran ({was.BrowPlan})");
            if (brow) moved.Add(old.At(i));
            return brow;
        }

        /// <summary>The gaps between <paramref name="old"/>'s whole stations either side of a moved top.</summary>
        static bool[] MovedGaps(Wall old)
        {
            var gaps = new bool[old.Samples.Length - 1];
            for (int i = 0; i < old.Samples.Length; i++)
            {
                if (Array.IndexOf(MovedTops, old.At(i)) < 0) continue;
                if (i > 0) gaps[i - 1] = true;
                if (i < gaps.Length) gaps[i] = true;
            }
            return gaps;
        }

        static int StationOf(Wall old, float station, string by)
        {
            int i = Array.IndexOf(old.Stations, station);
            if (i < 0) Assert.Fail($"{by} holds k{station} whole, and it is not one of {old.Name}'s stations");
            return i;
        }

        static void AssertSameBits(CliffWallMeshData a, CliffWallMeshData b, string what)
        {
            Assert.AreEqual(a.Verts.Length, b.Verts.Length, $"{what}: {b.Verts.Length} vertices, and it laid {a.Verts.Length}");
            for (int i = 0; i < a.Verts.Length; i++)
            {
                if (!SameBits(a.Verts[i], b.Verts[i]))
                    Assert.Fail($"{what}: vertex {i} is at {Bits(b.Verts[i])}, and was at {Bits(a.Verts[i])}");
                if (!SameBits(a.Uvs[i], b.Uvs[i]))
                    Assert.Fail($"{what}: vertex {i}'s uv is {Bits(b.Uvs[i])}, and was {Bits(a.Uvs[i])}");
                if (!SameBits(a.ElevationUv[i], b.ElevationUv[i]))
                    Assert.Fail($"{what}: vertex {i}'s elevation is {Bits(b.ElevationUv[i])}, and was {Bits(a.ElevationUv[i])}");
                if (!SameBits(a.SeaPlanUv[i], b.SeaPlanUv[i]))
                    Assert.Fail($"{what}: vertex {i}'s sea plan is {Bits(b.SeaPlanUv[i])}, and was {Bits(a.SeaPlanUv[i])}");
            }
            CollectionAssert.AreEqual(a.Tris, b.Tris, $"{what}: its triangles are not the ones it laid");
        }

        static string Bits(Vector3 v) => $"({R(v.x)}, {R(v.y)}, {R(v.z)})";

        static string Bits(Vector2 v) => $"({R(v.x)}, {R(v.y)})";

        static string R(float f) => f.ToString("R", CultureInfo.InvariantCulture);

        // =============================================================================================
        //  the walls, as each side lays them
        // =============================================================================================

        /// <summary>A wall's meshes as <see cref="CliffWallSurface"/> lays them now: its bands, then its brow and toe decals.</summary>
        static List<CliffWallMeshData> NowMeshes(Wall w)
        {
            Assert.IsTrue(CliffWallSurface.TryColumns(w.Samples, w.TextureBrow, w.SliceAt, w.SliceBefore, w.SliceAfter,
                                                      out CliffWallColumns columns, out string problem),
                          $"{w.Name}'s slices do not lay: {problem}");
            int rows = w.Rows;
            var meshes = new List<CliffWallMeshData>();
            for (int b = 0; b < w.BandStart.Length; b++)
                meshes.Add(CliffWallSurface.FaceMesh(columns, w.BandStart[b], w.BandEnd[b], rows, w.Along, w.FaceS, w.FaceT,
                                                     Displacement, w.ElevationValid, w.Origin));
            if (w.BrowDecal)
                meshes.Add(CliffWallSurface.DecalMesh(columns, true, w.Strip, w.BrowLine, w.Subdivide, w.Along, w.FaceS,
                                                      Displacement, w.ElevationValid, w.Origin));
            if (w.ToeDecal)
                meshes.Add(CliffWallSurface.DecalMesh(columns, false, w.Strip, w.BrowLine, w.Subdivide, w.Along, w.FaceS,
                                                      Displacement, w.ElevationValid, w.Origin));
            return meshes;
        }

        /// <summary>A wall's meshes as <see cref="CliffWallSurface"/> laid them before the cut, in the same order.</summary>
        static List<CliffWallMeshData> BeforeMeshes(Wall w)
        {
            int rows = w.Rows;
            var meshes = new List<CliffWallMeshData>();
            for (int b = 0; b < w.BandStart.Length; b++)
                meshes.Add(BeforeFace(w.Samples, w.BandStart[b], w.BandEnd[b], rows, w.Along, w.FaceS, w.FaceT,
                                      Displacement, w.ElevationValid, w.Origin));
            if (w.BrowDecal)
                meshes.Add(BeforeDecal(w.Samples, true, w.Strip, w.BrowLine, w.Subdivide, w.Along, w.FaceS,
                                       Displacement, w.ElevationValid, w.Origin));
            if (w.ToeDecal)
                meshes.Add(BeforeDecal(w.Samples, false, w.Strip, w.BrowLine, w.Subdivide, w.Along, w.FaceS,
                                       Displacement, w.ElevationValid, w.Origin));
            return meshes;
        }

        static string MeshName(Wall w, int m) =>
            m < w.BandStart.Length ? $"band {m}" : m == w.BandStart.Length && w.BrowDecal ? "brow decal" : "toe decal";

        static Wall FromFixture(Fixture fx, FixtureWall f)
        {
            int n = f.K.Length;
            var samples = new CliffWallSample[n];
            for (int i = 0; i < n; i++)
                samples[i] = new CliffWallSample(V2(f.Brow, i), V2(f.Toe, i), F(f.Drop[i]), F(f.ToeZ[i]));
            return new Wall
            {
                Name = f.Name, RealId = f.RealId, Root = f.Root,
                Stations = f.K.Select(k => (float)k).ToArray(),
                Samples = samples,
                Along = F(f.Along), RowsBasis = F(f.RowsBasis),
                FaceS = F(fx.FaceMetresS), FaceT = F(fx.FaceMetresT), Subdivide = F(fx.SubdivideMetres),
                Strip = F(fx.StripMetresT), BrowLine = F(fx.BrowLineAt),
                ElevationValid = f.ToeZ.Length >= n ? 1f : 0f,
                Origin = new Vector3(F(f.Position[0]), F(f.Position[1]), F(f.Position[2])),
                BandStart = f.BandStart.Select(F).ToArray(), BandEnd = f.BandEnd.Select(F).ToArray(),
                BrowDecal = f.HasBrowDecal, ToeDecal = f.HasToeDecal,
            };
        }

        /// <summary>The walls under a scene's cliff root, read as text (<see cref="StPetersCliffWalls.WallsUnder"/>), with
        /// the face fields the component lays them by.</summary>
        static List<Wall> FromScene(string path)
        {
            StPetersLayerRefresh.SceneYaml scene = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(path));
            var walls = new List<Wall>();
            foreach (StPetersCliffWalls.SceneWall s in StPetersCliffWalls.WallsUnder(scene))
            {
                StPetersLayerRefresh.Doc surface = scene.Require(s.Surface, $"{s.Name}'s CliffWallSurface");
                var w = new Wall
                {
                    Name = s.Name, RealId = s.RealId,
                    Samples = CliffWallSurface.BuildSamples(s.Brow, s.Toe, s.DropMetres, s.ToeElevations),
                    TextureBrow = s.TextureBrow, SliceAt = s.SliceAt, SliceBefore = s.SliceBefore, SliceAfter = s.SliceAfter,
                    Along = s.AlongOffsetMetres, RowsBasis = s.RowsBasisSurfaceMetres, Origin = s.Position,
                    FaceS = Float(surface, "_faceMetresS"), FaceT = Float(surface, "_faceMetresT"),
                    Subdivide = Float(surface, "_subdivideMetres"), Strip = Float(surface, "_stripMetresT"),
                    BrowLine = Float(surface, "_browLineAt"),
                    ElevationValid = s.ToeElevations.Length >= s.Brow.Length ? 1f : 0f,
                    BrowDecal = Holds(surface.Field("_browDecal")), ToeDecal = Holds(surface.Field("_toeDecal")),
                };
                ReadBands(surface, w);
                walls.Add(w);
            }
            return walls;
        }

        /// <summary>The bands a surface draws, as the YAML lists them: those with all three channels.</summary>
        static void ReadBands(StPetersLayerRefresh.Doc surface, Wall w)
        {
            var items = new List<Dictionary<string, string>>();
            bool on = false;
            foreach (string line in surface.Lines)
            {
                if (line == "  _bands:") { on = true; continue; }
                if (!on) continue;
                if (line.StartsWith("  - ", StringComparison.Ordinal)) items.Add(new Dictionary<string, string>());
                else if (!line.StartsWith("    ", StringComparison.Ordinal)) break;
                string field = line.Substring(4);
                int colon = field.IndexOf(": ", StringComparison.Ordinal);
                if (colon > 0) items[items.Count - 1][field.Substring(0, colon)] = field.Substring(colon + 2);
            }
            List<Dictionary<string, string>> drawn =
                items.Where(b => Holds(Value(b, "Unlit")) && Holds(Value(b, "Normal")) && Holds(Value(b, "Mask"))).ToList();
            w.BandStart = drawn.Select(b => StPetersLayerRefresh.ParseFloat(b["StartSurfaceMetres"])).ToArray();
            w.BandEnd = drawn.Select(b => StPetersLayerRefresh.ParseFloat(b["EndSurfaceMetres"])).ToArray();
        }

        static string Value(Dictionary<string, string> d, string key) => d.TryGetValue(key, out string v) ? v : null;

        static bool Holds(string reference) =>
            reference != null && !reference.StartsWith("{fileID: 0}", StringComparison.Ordinal);

        static float Float(StPetersLayerRefresh.Doc d, string key) => StPetersLayerRefresh.ParseFloat(d.Field(key));

        static float F(int bits) => BitConverter.Int32BitsToSingle(bits);

        static Vector2 V2(int[] bits, int i) => new Vector2(F(bits[2 * i]), F(bits[2 * i + 1]));

        static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

        static bool SameBits(Vector2 a, Vector2 b) => SameBits(a.x, b.x) && SameBits(a.y, b.y);

        static bool SameBits(Vector3 a, Vector3 b) => SameBits(a.x, b.x) && SameBits(a.y, b.y) && SameBits(a.z, b.z);

        static bool SameBits(float[] a, float[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (!SameBits(a[i], b[i])) return false;
            return true;
        }

        static double Sq(double x) => x * x;

        static double Area(Vector2 a, Vector2 b, Vector2 c) =>
            Math.Abs(((double)b.x - a.x) * ((double)c.y - a.y) - ((double)c.x - a.x) * ((double)b.y - a.y)) * 0.5;

        static void Bary(double px, double py, Vector2 a, Vector2 b, Vector2 c, out double la, out double lb, out double lc)
        {
            double v0x = (double)b.x - a.x, v0y = (double)b.y - a.y;
            double v1x = (double)c.x - a.x, v1y = (double)c.y - a.y;
            double v2x = px - a.x, v2y = py - a.y;
            double den = v0x * v1y - v1x * v0y;
            lb = (v2x * v1y - v1x * v2y) / den;
            lc = (v0x * v2y - v2x * v0y) / den;
            la = 1.0 - lb - lc;
        }

        // =============================================================================================
        //  the face before the cut: CliffWallSurface's band and decal as 98044649 laid them (frozen)
        // =============================================================================================

        /// <summary>One band's mesh as the component laid it before the cut: a column per station, along the brows.</summary>
        static CliffWallMeshData BeforeFace(CliffWallSample[] samples, float startSurfaceMetres, float endSurfaceMetres,
                                            int rows, float alongOffsetMetres, float faceMetresS, float faceMetresT,
                                            Func<float, float, float> displacement, float elevationValid, Vector3 origin)
        {
            int stations = samples.Length;
            var verts = new Vector3[stations * (rows + 1)];
            var uvs = new Vector2[verts.Length];
            var elevationUv = new Vector2[verts.Length];
            var seaPlanUv = new Vector2[verts.Length];
            var tris = new int[(stations - 1) * rows * 6];
            float along = 0f;

            for (int c = 0; c < stations; c++)
            {
                if (c > 0) along += Vector2.Distance(samples[c - 1].BrowPlan, samples[c].BrowPlan);

                CliffWallSample s = samples[c];
                Vector2 brow = s.BrowPlan;
                Vector2 toe = CliffWallGeometry.ToeScreen(in s);
                Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                float surface = Mathf.Max(1e-4f, CliffWallGeometry.SurfaceLengthMetres(in s));
                float u = CliffWallGeometry.TileU(alongOffsetMetres + along, faceMetresS);

                float t0 = Mathf.Clamp01(startSurfaceMetres / surface);
                float t1 = endSurfaceMetres > startSurfaceMetres
                         ? Mathf.Clamp01(endSurfaceMetres / surface)
                         : 1f;
                if (t1 < t0) t1 = t0;

                for (int r = 0; r <= rows; r++)
                {
                    float t = Mathf.Lerp(t0, t1, (float)r / rows);
                    Vector2 p = Vector2.Lerp(brow, toe, t) + outward * displacement(u, t);

                    int idx = c * (rows + 1) + r;
                    verts[idx] = new Vector3(p.x - origin.x, p.y - origin.y, 0f);
                    uvs[idx] = new Vector2(
                        u, CliffWallGeometry.TileV(surface * t - startSurfaceMetres, faceMetresT));
                    elevationUv[idx] = new Vector2(
                        CliffWaterlineMath.ElevationAt(s.ToeElevation + s.DropMetres, s.DropMetres, t),
                        elevationValid);
                    seaPlanUv[idx] = s.ToePlan;
                }
            }

            BeforeTriangles(tris, stations, rows);
            return new CliffWallMeshData { Verts = verts, Uvs = uvs, ElevationUv = elevationUv, SeaPlanUv = seaPlanUv, Tris = tris };
        }

        /// <summary>A brow or toe strip's mesh as the component laid it before the cut.</summary>
        static CliffWallMeshData BeforeDecal(CliffWallSample[] samples, bool brow, float stripMetresT, float browLineAt,
                                             float subdivideMetres, float alongOffsetMetres, float faceMetresS,
                                             Func<float, float, float> displacement, float elevationValid, Vector3 origin)
        {
            int stations = samples.Length;
            int rows = Mathf.Max(2, Mathf.CeilToInt(stripMetresT / Mathf.Max(0.01f, subdivideMetres)));

            float inland = CliffWallGeometry.BrowDecalInlandMetres(stripMetresT, browLineAt);
            float browFace = CliffWallGeometry.BrowDecalFaceMetres(stripMetresT, browLineAt);
            float toeFace = CliffWallGeometry.ToeDecalFaceMetres(stripMetresT);
            float line = Mathf.Clamp(browLineAt, 1e-3f, 1f - 1e-3f);

            var verts = new Vector3[stations * (rows + 1)];
            var uvs = new Vector2[verts.Length];
            var elevationUv = new Vector2[verts.Length];
            var seaPlanUv = new Vector2[verts.Length];
            var tris = new int[(stations - 1) * rows * 6];
            float along = 0f;

            for (int c = 0; c < stations; c++)
            {
                if (c > 0) along += Vector2.Distance(samples[c - 1].BrowPlan, samples[c].BrowPlan);

                CliffWallSample s = samples[c];
                Vector2 browP = s.BrowPlan;
                Vector2 toeP = CliffWallGeometry.ToeScreen(in s);
                Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                float surface = Mathf.Max(1e-4f, CliffWallGeometry.SurfaceLengthMetres(in s));
                float u = CliffWallGeometry.TileU(alongOffsetMetres + along, faceMetresS);
                float atBrow = displacement(u, 0f);

                for (int r = 0; r <= rows; r++)
                {
                    float v = (float)r / rows;
                    Vector2 p;
                    float faceT = 0f;

                    if (brow && v <= line)
                    {
                        p = browP - outward * (inland * (1f - v / line)) + outward * atBrow;
                    }
                    else
                    {
                        float depth = brow
                            ? (v - line) / (1f - line) * browFace
                            : surface - (1f - v) * toeFace;
                        faceT = Mathf.Clamp01(depth / surface);
                        p = Vector2.Lerp(browP, toeP, faceT) + outward * displacement(u, faceT);
                    }

                    int idx = c * (rows + 1) + r;
                    verts[idx] = new Vector3(p.x - origin.x, p.y - origin.y, 0f);
                    uvs[idx] = new Vector2(u, v);
                    elevationUv[idx] = new Vector2(
                        CliffWaterlineMath.ElevationAt(s.ToeElevation + s.DropMetres, s.DropMetres, faceT),
                        elevationValid);
                    seaPlanUv[idx] = s.ToePlan;
                }
            }

            BeforeTriangles(tris, stations, rows);
            return new CliffWallMeshData { Verts = verts, Uvs = uvs, ElevationUv = elevationUv, SeaPlanUv = seaPlanUv, Tris = tris };
        }

        static void BeforeTriangles(int[] tris, int stations, int rows)
        {
            int w = 0;
            for (int c = 0; c < stations - 1; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    int a = c * (rows + 1) + r;
                    int b = a + 1;
                    int d = (c + 1) * (rows + 1) + r;
                    int e = d + 1;
                    tris[w++] = a; tris[w++] = d; tris[w++] = b;
                    tris[w++] = b; tris[w++] = d; tris[w++] = e;
                }
            }
        }
    }
}
