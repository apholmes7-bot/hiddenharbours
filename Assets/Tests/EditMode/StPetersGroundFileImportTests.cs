using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ST PETERS' GROUND IS ITS GROUND FILE, IMPORTED</b> (terrain PR 5 B; amendment 1 §4.1 and §5, amendment 2 §5).
    /// <see cref="GroundFileImport"/> reads <c>ground.stp_island</c> by the rule the file states: "E(x, y) is the last
    /// ask whose patch covers (x, y), bilinear between its samples; elsewhere pass 9", and then the game's own asks. Three
    /// groups:
    /// <list type="number">
    /// <item><b>The rule, on made-up maps</b> (pure, no asset): the u16 decode in the file's order of operations, the
    /// big-endian pixel hash and the STOP on a mismatch, the last covering ask winning bilinearly between its samples in
    /// either order, and a package's ask after the game's refused.</item>
    /// <item><b>The file:</b> the package's eight asks in the file's order, then the game's three, fix 4 among them;
    /// the base's pixels hashing as the file says, and each patch's as its ask says.</item>
    /// <item><b>The import:</b> every cell the base's decode or the last covering patch's bilinear, over the whole
    /// map; two imports identical; the committed height map a fresh import's codes, at the plan's range, and the
    /// manifest recording the file, its base's and patches' hashes and those codes.</item>
    /// </list>
    ///
    /// <para><b>When it reds.</b> A changed ground file or ask is re-imported and the maps written again (Hidden
    /// Harbours ▸ World ▸ Terrain Plan… ▸ Write maps), then committed with it; a hash that no longer matches its file is a
    /// file that changed by accident, and the import refuses it rather than reading it. Never re-hash a file to pass.</para>
    /// </summary>
    public class StPetersGroundFileImportTests
    {
        const string FileId = "ground.stp_island";

        /// <summary>The package's asks, in the file's order (amendment 1 §4.1 item 3); the cannery hold is a rule.</summary>
        static readonly string[] PackageAsks =
        {
            "ground.stp_fen_pool_dry", "ground.stp_alder_fall_cut", "ground.stp_ne_head", "ground.stp_gap_bridge",
            "ground.stp_west_ledges_broken", "ground.stp_east_ledges_broken", "ground.stp_main_beach", "ground.stp_cannery_hold",
        };

        /// <summary>The game's own asks after them: fix 2 (amendment 1 §4.4), fix 3 (§4.5) and fix 4 (amendment 2 §4.3).</summary>
        static readonly string[] GameAsks = { "ground.stp_main_beach_west_blend", "ground.stp_bluff_channel_hold", "ground.stp_ne_neck_hollow_fill" };

        const string Fix4Id = "ground.stp_ne_neck_hollow_fill", CanneryHoldId = "ground.stp_cannery_hold";

        private RegionTerrainPlanDef _plan;
        private GroundFileDef _file;
        private GroundFileImport.Result _first, _second;

        [OneTimeSetUp]
        public void ImportTwiceFromScratch()
        {
            var sw = Stopwatch.StartNew();
            _plan = StPetersTerrainPlan.LoadPlan();
            _file = _plan.Ground;
            Assert.IsNotNull(_file, $"the plan names no ground file (it should name {FileId})");
            _first = StPetersTerrainPlan.Import(_plan);
            _second = StPetersTerrainPlan.Import(_plan);
            TestContext.WriteLine($"{_file.Id}: codes {_first.CodesSha256} and {_second.CodesSha256}; base pixels {_first.BasePixelsSha256} " +
                                  $"({sw.Elapsed.TotalSeconds:0.0} s for both)");
        }

        // ---- the rule, on made-up maps --------------------------------------------------------------------------------

        [Test]
        public void TheBase_AndAPatch_DecodeInTheFilesOrderOfOperations()
        {
            ushort[] codes = { 0, 1, 32767, 32768, 65534, 65535 };
            var b = new GroundFileImport.BaseMap { Width = 3, Height = 2, Lo = -4, Hi = 7, Codes = codes };
            var e = GroundFileImport.DecodeBase(b);
            for (int i = 0; i < codes.Length; i++)
                Assert.AreEqual(-4.0 + 11.0 * codes[i] / 65535.0, e[i], 0.0, $"the base's code {codes[i]}: lo + (hi − lo) × code / 65535");
            Assert.AreEqual(-4.0, e[0], 0.0, "code 0 is the base's lo");
            Assert.AreEqual(7.0, e[5], 0.0, "code 65535 is the base's hi");

            var a = new GroundFileImport.Ask { Id = "ground.test_patch", Lo = -4, Hi = 12, Codes = codes };
            var v = GroundFileImport.DecodePatch(a);
            for (int i = 0; i < codes.Length; i++)
                Assert.AreEqual(-4.0 + 16.0 * codes[i] / 65535.0, v[i], 0.0, $"a patch's code {codes[i]}: -4 + 16 × code / 65535");
            Assert.AreEqual(12.0, v[5], 0.0, "a patch's code 65535 is +12");
        }

        [Test]
        public void ThePixelHash_IsBigEndianInTheFilesOrder_AndAMismatchStopsTheImport()
        {
            ushort[] codes = { 0x0102, 0xA0B0, 0xFFFF, 0x0000 };
            byte[] bigEndian = { 0x01, 0x02, 0xA0, 0xB0, 0xFF, 0xFF, 0x00, 0x00 };
            string want = Sha256(bigEndian);
            Assert.AreEqual(want, GroundFileImport.Sha256BigEndian(codes).ToLowerInvariant(), "the samples' hash is not SHA-256 over u16 big-endian");

            var b = Base(2, 2, codes);
            b.PixelsSha256 = want;
            Assert.DoesNotThrow(() => GroundFileImport.Run(b, new List<GroundFileImport.Ask>(), -2.2, 0f, 65535f), "the base's own hash");

            b.PixelsSha256 = Sha256(new byte[] { 0x02, 0x01, 0xB0, 0xA0, 0xFF, 0xFF, 0x00, 0x00 });   // the little-endian read's hash
            var ex = Assert.Throws<InvalidDataException>(() => GroundFileImport.Run(b, new List<GroundFileImport.Ask>(), -2.2, 0f, 65535f));
            StringAssert.Contains("STOP", ex.Message, "a base whose pixels do not hash as the file says must stop the import");
        }

        /// <summary>A made-up patch: its box and step, and samples linear in their indices (code = c0 + ci·i + cj·j, 1 m a code).</summary>
        sealed class Spec
        {
            public string Id;
            public double X0, Y0, X1, Y1, Step, C0, Ci, Cj;
            public int W => (int)Math.Round((X1 - X0) / Step) + 1;
            public int H => (int)Math.Round((Y1 - Y0) / Step) + 1;
            public bool Covers(double x, double y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;
            /// <summary>Bilinear between linear samples is the linear function itself: the oracle needs no bilinear of its own.</summary>
            public double At(double x, double y) => C0 + Ci * (x - X0) / Step + Cj * (y - Y0) / Step;
        }

        static GroundFileImport.Ask Patch(Spec s, GroundAskSource source = GroundAskSource.Package)
        {
            var codes = new ushort[s.W * s.H];
            for (int j = 0; j < s.H; j++)
                for (int i = 0; i < s.W; i++) codes[j * s.W + i] = (ushort)(s.C0 + s.Ci * i + s.Cj * j);   // row 0 at the box's south edge
            return new GroundFileImport.Ask
            {
                Id = s.Id, Source = source, Kind = GroundAskKind.Patch, BoxX0 = s.X0, BoxY0 = s.Y0, BoxX1 = s.X1, BoxY1 = s.Y1, Step = s.Step,
                Width = s.W, Height = s.H, Lo = 0, Hi = 65535, Codes = codes,
            };
        }

        /// <summary>A made-up base over (0, 0)..(w, h) at one unit a texel, 1 m a code, row 0 north; its hash its own.</summary>
        static GroundFileImport.BaseMap Base(int w, int h, ushort[] codes)
        {
            return new GroundFileImport.BaseMap
            {
                Width = w, Height = h, Lo = 0, Hi = 65535, X0 = 0, Y0 = 0, X1 = w, Y1 = h, Codes = codes,
                PixelsSha256 = GroundFileImport.Sha256BigEndian(codes),
            };
        }

        [Test]
        public void TheLastCoveringAsk_Wins_BilinearBetweenItsSamples_InEitherOrder()
        {
            const int w = 8, h = 6;
            var codes = new ushort[w * h];
            for (int row = 0; row < h; row++)
                for (int c = 0; c < w; c++) codes[row * w + c] = (ushort)(1 + c + 10 * row);
            var b = Base(w, h, codes);

            // A at a step of 1 off the cell centres by a quarter; B at 0.5 over A's east half; C whose east edge runs
            // through a column of centres (they read its edge samples). Cell centres sit at x = c + 0.5, y = 5.5 − row.
            var specs = new[]
            {
                new Spec { Id = "ground.test_a", X0 = 1.25, Y0 = 1.25, X1 = 5.25, Y1 = 4.25, Step = 1, C0 = 5000, Ci = 11, Cj = 13 },
                new Spec { Id = "ground.test_b", X0 = 3.1, Y0 = 2.2, X1 = 6.1, Y1 = 4.2, Step = 0.5, C0 = 20000, Ci = 101, Cj = 1003 },
                new Spec { Id = "ground.test_c", X0 = 0.5, Y0 = 0.5, X1 = 2.5, Y1 = 1.5, Step = 1, C0 = 40000, Ci = 7, Cj = 3 },
            };
            foreach (var order in new[] { new[] { 0, 1, 2 }, new[] { 2, 1, 0 } })
            {
                var asks = order.Select(k => Patch(specs[k])).ToList();
                var r = GroundFileImport.Run(b, asks, -2.2, 0f, 65535f);
                var g = r.Grid;
                Assert.AreEqual(w * h, g.Count, "the base's grid");
                int overlaps = 0, between = 0, edges = 0;
                for (int i = 0; i < g.Count; i++)
                {
                    double x = g.XG(i % g.W), y = g.YG(i / g.W);
                    double want = codes[i];
                    int owner = -1, covering = 0;
                    for (int k = asks.Count - 1; k >= 0; k--)
                    {
                        var s = specs[order[k]];
                        if (!s.Covers(x, y)) continue;
                        covering++;
                        if (owner >= 0) continue;
                        owner = k;
                        want = s.At(x, y);
                        if ((x - s.X0) / s.Step % 1 != 0 || (y - s.Y0) / s.Step % 1 != 0) between++;
                        if (x == s.X1 || y == s.Y1) edges++;
                    }
                    if (covering > 1) overlaps++;
                    string at = $"order {string.Join(", ", order.Select(k => specs[k].Id))}, cell ({x}, {y})";
                    Assert.AreEqual((double)codes[i], r.Base[i], 0.0, at + ": the base's own decode");
                    Assert.AreEqual(want, r.E[i], 1e-9, at + ": not the last covering ask's bilinear (or the base where none covers)");
                    Assert.AreEqual(owner, r.Owner[i], at + ": laid by the wrong ask");
                }
                Assert.Greater(overlaps, 0, "the made-up asks must overlap, or the order is not tested");
                Assert.Greater(between, 0, "some cells must fall between samples, or the bilinear is not tested");
                Assert.Greater(edges, 0, "some cells must sit on a north or east edge, or its clamp is not tested");
            }
        }

        [Test]
        public void APackagesAsk_AfterTheGamesOwn_IsRefused()
        {
            var b = Base(4, 4, new ushort[16]);
            var package = Patch(new Spec { Id = "ground.test_package", X0 = 0.5, Y0 = 0.5, X1 = 2.5, Y1 = 2.5, Step = 1, C0 = 3 });
            var game = new GroundFileImport.Ask
            {
                Id = "ground.test_game_rule", Source = GroundAskSource.Game, Kind = GroundAskKind.Rule, CentreX = 2, CentreY = 2, Reach = 1,
            };
            Assert.DoesNotThrow(() => GroundFileImport.Run(b, new List<GroundFileImport.Ask> { package, game }, -2.2, 0f, 65535f),
                                "the game's ask after the package's");
            var ex = Assert.Throws<InvalidDataException>(() => GroundFileImport.Run(b, new List<GroundFileImport.Ask> { game, package }, -2.2, 0f, 65535f));
            StringAssert.Contains("the game's come last", ex.Message);
        }

        // ---- the file -----------------------------------------------------------------------------------------------------

        [Test]
        public void TheFile_ListsThePackagesAsksInItsOrder_ThenTheGamesOwn_Fix4AmongThem()
        {
            Assert.AreEqual(FileId, _file.Id);
            var ids = _file.Asks.Select(d => d == null ? "(empty)" : d.Id).ToArray();
            CollectionAssert.AreEqual(PackageAsks.Concat(GameAsks).ToArray(), ids, "the file's asks, in order");
            for (int k = 0; k < _file.Asks.Length; k++)
            {
                var d = _file.Asks[k];
                Assert.IsFalse(d.Retired, d.Id + " is retired");
                Assert.AreEqual(k < PackageAsks.Length ? GroundAskSource.Package : GroundAskSource.Game, d.Source, d.Id + "'s source");
            }

            var fix4 = _file.Asks.Single(d => d.Id == Fix4Id);
            Assert.AreEqual(GroundAskKind.HollowFill, fix4.Kind, "fix 4 fills a hollow");
            Assert.AreEqual(CanneryHoldId, fix4.KeepOutOf, "fix 4 keeps out of the cannery's circle");
            Assert.Greater(Array.IndexOf(ids, Fix4Id), Array.IndexOf(ids, PackageAsks.Last()), "fix 4 comes after every package ask");

            CollectionAssert.AreEqual(ids, _first.Asks.Select(a => a.Id).ToArray(), "the import laid the asks in another order");
            Assert.AreEqual(GroundAskKind.HollowFill, _first.Asks.Single(a => a.Id == Fix4Id).Kind);
        }

        [Test]
        public void TheBase_AndEachPatch_HashAsTheFileSays()
        {
            string basePath = AssetDatabase.GetAssetPath(_file.Base);
            var img = TerrainPlanPng.ReadFile(basePath);
            Assert.AreEqual(16, img.BitDepth, basePath + " is not 16-bit");
            Assert.AreEqual(1, img.Channels, basePath + " is not grey");
            Assert.AreEqual(1520, img.Width, basePath + "'s width");
            Assert.AreEqual(1040, img.Height, basePath + "'s height");
            Assert.AreEqual(_file.BasePixelsSha256.ToLowerInvariant(), GroundFileImport.Sha256BigEndian(img.Channel(0)).ToLowerInvariant(),
                            basePath + "'s pixels are not the file's pixelsSha256");
            Assert.AreEqual(_file.BasePixelsSha256.ToLowerInvariant(), _first.BasePixelsSha256.ToLowerInvariant(), "the import read another base");

            int patches = 0;
            foreach (var d in _file.Asks.Where(d => !d.Retired && d.Kind == GroundAskKind.Patch))
            {
                var codes = GroundFileIntake.PatchOf(d);
                Assert.AreEqual(d.Width * d.Height, codes.Length, d.Id + "'s samples");
                Assert.IsNotEmpty(d.PatchSha256, d.Id + " carries no hash");
                Assert.AreEqual(d.PatchSha256.ToLowerInvariant(), GroundFileImport.Sha256BigEndian(codes).ToLowerInvariant(),
                                d.Id + "'s samples (row 0 at the box's south edge) are not its ask's hash");
                patches++;
            }
            Assert.AreEqual(7, patches, "the package's seven patches");
        }

        // ---- the import --------------------------------------------------------------------------------------------------

        [Test]
        public void EveryCell_IsTheBase_OrTheLastPatchCoveringIt()
        {
            var img = TerrainPlanPng.ReadFile(AssetDatabase.GetAssetPath(_file.Base));
            var b = GroundFileImport.BaseOf(_file, img.Channel(0), img.Width, img.Height);
            var asks = GroundFileImport.AsksOf(_file, GroundFileIntake.PatchOf).Where(a => a.Source == GroundAskSource.Package).ToList();
            var r = GroundFileImport.Run(b, asks, -TerrainPlanMath.Num(_plan.SpringM), _plan.HeightRange.x, _plan.HeightRange.y);
            var values = asks.Select(a => a.Kind == GroundAskKind.Patch ? GroundFileImport.DecodePatch(a) : null).ToArray();

            var g = r.Grid;
            var owned = new int[asks.Count];
            int bad = 0, first = -1;
            double firstWant = 0;
            for (int i = 0; i < g.Count; i++)
            {
                double x = g.XG(i % g.W), y = g.YG(i / g.W);
                double want = b.Lo + (b.Hi - b.Lo) * b.Codes[i] / 65535.0;
                int owner = -1;
                for (int k = asks.Count - 1; k >= 0 && owner < 0; k--)
                    if (values[k] != null && GroundFileImport.PatchAt(asks[k], values[k], x, y, out double z)) { want = z; owner = k; }
                if (owner >= 0) owned[owner]++;
                if (r.E[i] == want && r.Owner[i] == owner) continue;
                if (bad++ == 0) { first = i; firstWant = want; }
            }
            TestContext.WriteLine("cells each package ask holds at the end: " +
                                  string.Join(", ", asks.Select((a, k) => a.Id + " " + owned[k])) + $"; the base {g.Count - owned.Sum()}");
            Assert.AreEqual(0, bad, bad == 0 ? "" :
                $"{bad} cells are not the base or the last patch covering them; the first ({g.XG(first % g.W)}, {g.YG(first / g.W)}) " +
                $"reads {r.E[first]} by ask {r.Owner[first]}, not {firstWant}");
            Assert.AreEqual(0, owned[asks.FindIndex(a => a.Id == CanneryHoldId)], "the cannery hold is a rule: it lays nothing");
        }

        [Test]
        public void TwoImports_AreIdentical()
        {
            Assert.AreEqual(_first.CodesSha256, _second.CodesSha256, "two imports' codes");
            Assert.AreEqual(_first.E.Length, _second.E.Length);
            int at = -1;
            for (int i = 0; i < _first.E.Length && at < 0; i++)
                if (!_first.E[i].Equals(_second.E[i]) || _first.Owner[i] != _second.Owner[i]) at = i;
            Assert.AreEqual(-1, at, "two imports part at a cell");
            Assert.AreEqual(_first.Asks.Count, _second.Asks.Count);
            for (int k = 0; k < _first.Asks.Count; k++)
            {
                var a = _first.Asks[k];
                var b = _second.Asks[k];
                string what = a.Id + "'s record";
                Assert.AreEqual(a.Id, b.Id, what);
                Assert.AreEqual(a.Cells, b.Cells, what + ": cells");
                Assert.AreEqual(a.Held, b.Held, what + ": held");
                Assert.AreEqual((a.MaxRaiseAt, a.MaxCutAt), (b.MaxRaiseAt, b.MaxCutAt), what + ": where it raised and cut most");
                Assert.IsTrue(a.MaxRaise.Equals(b.MaxRaise) && a.MaxCut.Equals(b.MaxCut) && a.Level.Equals(b.Level), what + ": its raise, cut or level");
                Assert.IsTrue(a.X0.Equals(b.X0) && a.Y0.Equals(b.Y0) && a.X1.Equals(b.X1) && a.Y1.Equals(b.Y1), what + ": its reach");
                CollectionAssert.AreEqual(a.Changed, b.Changed, what + ": changed cells");
                CollectionAssert.AreEqual(a.Before, b.Before, what + ": the heights before");
            }
        }

        [Test]
        public void TheCommittedHeightMap_IsAFreshImportsCodes_AndTheManifestRecordsItsFile()
        {
            var map = StPetersTerrainPlan.LoadSeabed();
            Assert.AreEqual(_plan.HeightRange.x, map.MinElevation, "the seabed asset's range is not the plan's");
            Assert.AreEqual(_plan.HeightRange.y, map.MaxElevation, "the seabed asset's range is not the plan's");
            string path = AssetDatabase.GetAssetPath(map.HeightTexture);
            var img = TerrainPlanPng.ReadFile(path);
            Assert.AreEqual(16, img.BitDepth, path + " is not 16-bit");
            Assert.AreEqual(1, img.Channels, path + " is not grey");
            var g = _first.Grid;
            Assert.AreEqual(g.W, img.Width, path + "'s width");
            Assert.AreEqual(g.H, img.Height, path + "'s height");
            var committed = TerrainPlanPng.FlipRows(img.Channel(0), img.Width, img.Height);   // Unity's order, row 0 south
            int off = 0, first = -1;
            for (int i = 0; i < committed.Length; i++)
                if (committed[i] != _first.Codes[i]) { off++; if (first < 0) first = i; }
            Assert.AreEqual(0, off, off == 0 ? "" :
                $"{path} is not a fresh import: {off} codes differ, the first at pixel {first} ({committed[first]} for {_first.Codes[first]}); write the maps again");

            Assert.IsTrue(File.Exists(StPetersTerrainPlan.ManifestPath), "no manifest at " + StPetersTerrainPlan.ManifestPath);
            string text = File.ReadAllText(StPetersTerrainPlan.ManifestPath);
            var man = new TerrainPlanManifest();
            TerrainPlanMapWriter.RecordGround(man, _file, _first);
            StringAssert.Contains(man.GroundLine(), text, "the manifest does not record this ground file, its hashes and its import's codes");
            Assert.AreEqual(_first.CodesSha256, TerrainPlanManifest.ValuesOf(text, TerrainPlanMapWriter.HeightRole), "the manifest's height values");
        }

        static string Sha256(byte[] bytes)
        {
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
