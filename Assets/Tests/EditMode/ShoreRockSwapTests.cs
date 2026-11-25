using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE NINE SWAPS CHANGE NINE SPRITES AND NOTHING ELSE</b> (terrain pass 9, PR 4b). Subjects:
    /// <see cref="StPetersLayerRefresh"/> (its Shoreline step's swaps) and <see cref="ShoreRockDef"/>.
    ///
    /// <para>Part 2 §4.4 names nine of today's shore rocks that keep their places and take a Rock Px
    /// form. Each is written here as a <see cref="ShoreRockDef"/> from the table's own values, and the
    /// Shoreline step runs on the committed scene's TEXT with and without them. The nine keep their
    /// GameObject, Transform and YSortSprite byte for byte (place, rotation, scale); their
    /// SpriteRenderer changes in its sprite and size lines only; every other document is unchanged.</para>
    ///
    /// <para>The Rock Px sprites are the test's own: a made-up reference per cell, whose GUID is no
    /// asset (checked). The test asks the kit for no GUID. The ShoreIso sprites are the ones the
    /// committed rocks already wear, read from the scene text.</para>
    ///
    /// <para><b>Since terrain PR 5w the committed scene carries the nine swaps</b>, so the nine wear the kit's
    /// Rock Px sprites.</para>
    ///
    /// <para><b>Since the shaded-rocks PR every St Peters rock has a swap</b>: the nine plus 24 more
    /// (<see cref="ShoreRockSwapChecks.Rest"/>), so no rock wears the old flat ShoreIso sprite. No committed
    /// rock is left to read a key's ShoreIso sprite from, so the test reads it from the sheet's own
    /// <c>.meta</c> (its sprite's internal id and rect, as the editor loads it). Without swaps the step hands
    /// all 33 back their key's ShoreIso sprite: on the committed rocks it changes their sprite and size and
    /// nothing else (checked first). The table is written out by hand, and the committed Def assets are held
    /// to it row for row.</para>
    /// </summary>
    public class ShoreRockSwapTests
    {
        readonly List<ShoreRockDef> _defs = new List<ShoreRockDef>();
        GameObject _terrainGo;
        string _sceneText;

        [SetUp]
        public void SetUp()
        {
            _sceneText = File.ReadAllText(StPetersLayerRefreshTests.ScenePath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ShoreRockDef d in _defs)
                if (d != null) Object.DestroyImmediate(d);
            _defs.Clear();
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
        }

        ShoreRockDef Def(ShoreRockSwapChecks.Row row)
        {
            var def = ScriptableObject.CreateInstance<ShoreRockDef>();
            _defs.Add(def);
            row.WriteTo(def);
            return def;
        }

        List<ShoreRockDef> TheNine() => ShoreRockSwapChecks.Nine.Select(Def).ToList();

        List<ShoreRockDef> TheSwaps() => ShoreRockSwapChecks.All.Select(Def).ToList();

        // =============================================================================================
        //  the swap
        // =============================================================================================

        [Test]
        public void TheSwaps_ChangeOnlyTheirSprites_OnTheCommittedRocks()
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(_sceneText);
            ShoreRockSwapChecks.SwapChangesOnlyTheNamed(_sceneText, StPetersLayerRefresh.RocksInScene(scene),
                                                        ShoreRockSwapChecks.All, TheSwaps(), placementIsTheCommittedRocks: true);
        }

        [Test]
        public void TheSwaps_ChangeOnlyTheirSprites_OnThePaintersPlacementToday()
        {
            _terrainGo = new GameObject("TidalTerrain_ShoreRockSwapTest");
            var terrain = _terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);   // the same zones the scene is built with
            ShoreRockSwapChecks.SwapChangesOnlyTheNamed(_sceneText, StPetersLayerRefresh.PainterPlacement(terrain),
                                                        ShoreRockSwapChecks.PaintedToday,
                                                        ShoreRockSwapChecks.PaintedToday.Select(Def).ToList(),
                                                        placementIsTheCommittedRocks: false);
        }

        [Test]
        public void TheThirteenRocksThePainterNoLongerPlaces_StopTheRefresh_RatherThanBeDeleted()
        {
            _terrainGo = new GameObject("TidalTerrain_ShoreRockSwapTest");
            var terrain = _terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);
            List<StPetersLayerRefresh.PlacedRock> today = StPetersLayerRefresh.PainterPlacement(terrain);
            Assert.AreEqual(13, ShoreRockSwapChecks.NotPaintedToday.Length);
            foreach (string id in ShoreRockSwapChecks.NotPaintedToday)
            {
                ShoreRockSwapChecks.Row row = ShoreRockSwapChecks.All.Single(r => r.Id == id);
                var at = new Vector2(row.X, row.Y);
                Assert.IsFalse(today.Any(p => p.Name == row.Today && Vector2.Distance(p.Position, at) <= 0.01f),
                               id + " is painted today: move it back to PaintedToday");
            }
            var scene = StPetersLayerRefresh.SceneYaml.Parse(_sceneText);
            var refusal = Assert.Throws<StPetersLayerRefresh.Refusal>(() => StPetersLayerRefresh.Shoreline(
                scene, today, new ShoreRockSwapChecks.TestShoreRockSprites(), TheSwaps()));
            StringAssert.Contains("0 painted rocks match it", refusal.Message);
        }

        [Test]
        public void TheNineSwapsAlone_ChangeOnlyTheNineSprites_OnThePaintersPlacementToday()
        {
            _terrainGo = new GameObject("TidalTerrain_ShoreRockSwapTest");
            var terrain = _terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);
            ShoreRockSwapChecks.SwapChangesOnlyTheNamed(_sceneText, StPetersLayerRefresh.PainterPlacement(terrain),
                                                        ShoreRockSwapChecks.Nine, TheNine(), placementIsTheCommittedRocks: false);
        }

        [Test]
        public void EveryCommittedRock_IsNamedByExactlyOneRow()
        {
            var t = new LayerRefreshSceneText(_sceneText);
            long rocks = t.ChildNamed(t.RootGameObject(StPetersShorePainter.RootName), StPetersShorePainter.RocksRootName);
            List<long> gos = t.ChildGameObjects(rocks);
            Assert.AreEqual(ShoreRockSwapChecks.All.Length, gos.Count, "the committed rocks, one row each");
            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (long go in gos)
            {
                List<ShoreRockSwapChecks.Row> rows = ShoreRockSwapChecks.RowsNaming(t, go);
                Assert.AreEqual(1, rows.Count, $"rows naming the {t.Field(go, "m_Name")} at {t.Vec3(t.TransformOf(go), "m_LocalPosition")}");
                Assert.IsTrue(named.Add(rows[0].Id), $"{rows[0].Id} names two rocks");
            }
        }

        [Test]
        public void TheCommittedDefs_AreTheTable()
        {
            List<ShoreRockDef> defs = StPetersLayerRefresh.LoadStPetersShoreRockDefs();
            CollectionAssert.AreEquivalent(ShoreRockSwapChecks.All.Select(r => r.Id).ToList(), defs.Select(d => d.Id).ToList(),
                                           "St Peters' ShoreRockDef ids against the table");
            foreach (ShoreRockDef d in defs)
            {
                ShoreRockSwapChecks.Row row = ShoreRockSwapChecks.All.Single(r => r.Id == d.Id);
                Assert.AreEqual(row.Today, d.Today, d.Id + ": Today");
                Assert.That(Vector2.Distance(new Vector2(row.X, row.Y), d.At), Is.LessThan(1e-4f), d.Id + ": At");
                Assert.AreEqual(row.Form, d.Form, d.Id + ": Form");
                Assert.AreEqual(row.Stone, d.Stone, d.Id + ": Stone");
                Assert.AreEqual(row.State, d.State, d.Id + ": State");
                Assert.AreEqual("bare", d.Dress, d.Id + ": Dress");
                Assert.AreEqual(row.Variant, d.Variant, d.Id + ": Variant");
                Assert.AreEqual(row.Mirrored, d.Mirrored, d.Id + ": Mirrored");
            }
        }

        [Test]
        public void TheTestsRockPxSprites_AreNoAsset()
        {
            foreach (ShoreRockSwapChecks.Row row in ShoreRockSwapChecks.All)
            {
                string guid = ShoreRockSwapChecks.TestGuid(row.Cell);
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Null.Or.Empty,
                            $"{row.Id}: the test's sprite for {row.Cell} ({guid}) is an asset in this project");
            }
        }

        // =============================================================================================
        //  the swap refuses rather than guess
        // =============================================================================================

        [Test]
        public void ASwapFiveCentimetresFromItsRock_IsRefused()
        {
            List<ShoreRockDef> nine = TheNine();
            nine[0].At += new Vector2(0.05f, 0f);
            ShoreRockSwapChecks.Refused(_sceneText, nine, "0 painted rocks match it");
        }

        [Test]
        public void ASwapNamingAnotherKindOfRock_IsRefused()
        {
            List<ShoreRockDef> nine = TheNine();
            nine[0].Today = "Rock_bm";   // rock.stp_strand_block stands on a Rock_bs
            ShoreRockSwapChecks.Refused(_sceneText, nine, "0 painted rocks match it");
        }

        [Test]
        public void TwoSwapsNamingOneRock_AreRefused()
        {
            List<ShoreRockDef> nine = TheNine();
            ShoreRockDef twin = Def(ShoreRockSwapChecks.Nine[0]);
            twin.Id = "rock.stp_strand_block_twin";
            nine.Add(twin);
            ShoreRockSwapChecks.Refused(_sceneText, nine, "name the same rock");
        }

        [Test]
        public void TwoSwapsSharingAnId_AreRefused()
        {
            List<ShoreRockDef> nine = TheNine();
            nine[1].Id = nine[0].Id;
            ShoreRockSwapChecks.Refused(_sceneText, nine, "share the id");
        }

        [Test]
        public void ASwapWithoutARockId_IsRefused()
        {
            List<ShoreRockDef> nine = TheNine();
            nine[0].Id = "stp_strand_block";
            ShoreRockSwapChecks.Refused(_sceneText, nine, "is not a rock.snake_case id");
        }

        [Test]
        public void ASwapWhoseCellTheKitLacks_IsRefused()
        {
            ShoreRockSwapChecks.Refused(_sceneText, TheNine(), ": no sprite '", haveRockPx: false);
        }

        // =============================================================================================
        //  ShoreRockDef names the kit's cell and sheet
        // =============================================================================================

        [Test]
        public void TheRows_NameTheKitsCellsAndSheets()
        {
            List<ShoreRockDef> all = TheSwaps();
            for (int i = 0; i < all.Count; i++)
                ShoreRockSwapChecks.NamesItsCell(ShoreRockSwapChecks.All[i], all[i]);
        }

        [Test]
        public void AVariantMirrorAndDress_NameTheirColumnRowAndSheet()
        {
            ShoreRockDef def = Def(ShoreRockSwapChecks.Nine[0]);
            def.Variant = 2;
            def.Mirrored = true;
            Assert.AreEqual("Block_sandstone_c6_r0", StPetersLayerRefresh.RockPxSpriteName(def), "variant C, mirrored, dry");
            def.Variant = 3;
            def.Mirrored = false;
            def.State = "awash";
            Assert.AreEqual("Block_sandstone_c3_r2", StPetersLayerRefresh.RockPxSpriteName(def), "variant D, awash");
            def.Dress = "weeded";
            Assert.AreEqual("Block_sandstone_weeded_c3_r2", StPetersLayerRefresh.RockPxSpriteName(def), "variant D, awash, weeded");
            Assert.AreEqual("Assets/_Project/Art/Sprites/Shore/RockPx/tex/Block_sandstone_weeded.png",
                            StPetersLayerRefresh.RockPxSheetPath(def), "the weeded dress is its own sheet");
        }

        [Test]
        public void AStateOffTheKitsTides_IsRefused()
        {
            ShoreRockDef def = Def(ShoreRockSwapChecks.Nine[0]);
            def.State = "damp";
            var refusal = Assert.Throws<StPetersLayerRefresh.Refusal>(() => StPetersLayerRefresh.RockPxSpriteName(def));
            StringAssert.Contains("is not one of the kit's tides", refusal.Message);
        }
    }

    /// <summary>
    /// The swap's checks, as plain functions of the scene text and the Defs, so the same checks can be
    /// run against a deliberately broken step outside Unity.
    /// </summary>
    internal static class ShoreRockSwapChecks
    {
        internal sealed class Row
        {
            public string Id, Today, Form, Stone, State, Cell, Sheet;
            public float X, Y;
            public int Variant;
            public bool Mirrored;

            /// <summary>The table's columns. Dress is left at the Def's own default (bare); the nine's
            /// variant and mirror are too (part 2 names none, so variant A, unmirrored).</summary>
            public void WriteTo(ShoreRockDef def)
            {
                def.Id = Id;
                def.Today = Today;
                def.At = new Vector2(X, Y);
                def.Form = Form;
                def.Stone = Stone;
                def.State = State;
                def.Variant = Variant;
                def.Mirrored = Mirrored;
            }
        }

        static Row R(string id, string today, float x, float y, string form, string stone, string state, string cell, string sheet) =>
            new Row { Id = id, Today = today, X = x, Y = y, Form = form, Stone = stone, State = state, Cell = cell, Sheet = sheet };

        static Row R(string id, string today, float x, float y, string form, string stone, string state, int variant, bool mirrored,
                     string cell, string sheet) =>
            new Row { Id = id, Today = today, X = x, Y = y, Form = form, Stone = stone, State = state, Variant = variant,
                      Mirrored = mirrored, Cell = cell, Sheet = sheet };

        /// <summary>
        /// Part 2 §4.4's table, as it is written there. <c>Cell</c> is the kit's sprite for the row
        /// (variant A unmirrored is column 0; dry is row 0, wet row 1) and <c>Sheet</c> its bare
        /// albedo sheet, both written out by hand.
        /// </summary>
        internal static readonly Row[] Nine =
        {
            R("rock.stp_strand_block",     "Rock_bs",      189.82f,  35.82f, "block",   "sandstone", "dry", "Block_sandstone_c0_r0",   "Block_sandstone"),
            R("rock.stp_strand_knuckle",   "Rock_bm",      199.60f,  21.98f, "knuckle", "sandstone", "wet", "Knuckle_sandstone_c0_r1", "Knuckle_sandstone"),
            R("rock.stp_landing_knuckle",  "Rock_m",       217.52f,  15.40f, "knuckle", "sandstone", "wet", "Knuckle_sandstone_c0_r1", "Knuckle_sandstone"),
            R("rock.stp_landing_skerry",   "Rock_reef",    219.75f,  16.60f, "skerry",  "sandstone", "wet", "Skerry_sandstone_c0_r1",  "Skerry_sandstone"),
            R("rock.stp_east_gap_skerry",  "Rock_reef",    176.35f, -55.98f, "skerry",  "sandstone", "wet", "Skerry_sandstone_c0_r1",  "Skerry_sandstone"),
            R("rock.stp_ledge_block",      "Rock_bs",      114.98f, -69.63f, "block",   "sandstone", "wet", "Block_sandstone_c0_r1",   "Block_sandstone"),
            R("rock.stp_field_erratic_1",  "FieldRock_bs", 104.34f, -56.54f, "erratic", "granite",   "dry", "Erratic_granite_c0_r0",   "Erratic_granite"),
            R("rock.stp_field_erratic_2",  "FieldRock_bs", 122.01f, -47.86f, "erratic", "granite",   "dry", "Erratic_granite_c0_r0",   "Erratic_granite"),
            R("rock.stp_field_erratic_3",  "FieldRock_bs",   4.58f, -45.33f, "erratic", "granite",   "dry", "Erratic_granite_c0_r0",   "Erratic_granite"),
        };

        /// <summary>
        /// The shaded-rocks PR: every St Peters rock the nine leave on the old flat ShoreIso sprite takes the
        /// closest-footprint kit form (shore rocks wet sandstone, field rocks dry granite erratics, the reef
        /// the smallest skerry), alternate rocks of a kind mirrored. Written out by hand; the committed Defs
        /// are held to it (<c>TheCommittedDefs_AreTheTable</c>).
        /// </summary>
        internal static readonly Row[] Rest =
        {
            R("rock.stp_field_bl_1",  "FieldRock_bl", 123.93f,  56.40f, "erratic", "granite",   "dry", 0, false, "Erratic_granite_c0_r0",   "Erratic_granite"),
            R("rock.stp_field_bm_1",  "FieldRock_bm",  84.26f,  49.81f, "erratic", "granite",   "dry", 2, false, "Erratic_granite_c2_r0",   "Erratic_granite"),
            R("rock.stp_field_bs_1",  "FieldRock_bs", 115.62f,  57.12f, "erratic", "granite",   "dry", 2, false, "Erratic_granite_c2_r0",   "Erratic_granite"),
            R("rock.stp_field_bs_2",  "FieldRock_bs",  58.73f,  37.89f, "erratic", "granite",   "dry", 2, true,  "Erratic_granite_c6_r0",   "Erratic_granite"),
            R("rock.stp_bl_1",        "Rock_bl",      156.27f, -59.20f, "knuckle", "sandstone", "wet", 1, false, "Knuckle_sandstone_c1_r1", "Knuckle_sandstone"),
            R("rock.stp_bl_2",        "Rock_bl",       14.86f, -74.40f, "knuckle", "sandstone", "wet", 1, true,  "Knuckle_sandstone_c5_r1", "Knuckle_sandstone"),
            R("rock.stp_bm_1",        "Rock_bm",      -16.30f, -62.60f, "block",   "sandstone", "wet", 2, false, "Block_sandstone_c2_r1",   "Block_sandstone"),
            R("rock.stp_bm_2",        "Rock_bm",       82.97f, -78.49f, "block",   "sandstone", "wet", 2, true,  "Block_sandstone_c6_r1",   "Block_sandstone"),
            R("rock.stp_bs_1",        "Rock_bs",      179.58f, -44.53f, "erratic", "sandstone", "wet", 2, false, "Erratic_sandstone_c2_r1", "Erratic_sandstone"),
            R("rock.stp_m_1",         "Rock_m",       -59.07f,  45.64f, "fin",     "sandstone", "wet", 2, false, "Fin_sandstone_c2_r1",     "Fin_sandstone"),
            R("rock.stp_m_2",         "Rock_m",       192.06f, -44.93f, "fin",     "sandstone", "wet", 2, true,  "Fin_sandstone_c6_r1",     "Fin_sandstone"),
            R("rock.stp_m_3",         "Rock_m",       142.43f, -83.20f, "fin",     "sandstone", "wet", 2, false, "Fin_sandstone_c2_r1",     "Fin_sandstone"),
            R("rock.stp_m_4",         "Rock_m",        32.11f, -91.01f, "fin",     "sandstone", "wet", 2, true,  "Fin_sandstone_c6_r1",     "Fin_sandstone"),
            R("rock.stp_reef_1",      "Rock_reef",     38.00f,  90.76f, "skerry",  "sandstone", "wet", 2, false, "Skerry_sandstone_c2_r1",  "Skerry_sandstone"),
            R("rock.stp_reef_2",      "Rock_reef",      2.78f,  81.71f, "skerry",  "sandstone", "wet", 2, true,  "Skerry_sandstone_c6_r1",  "Skerry_sandstone"),
            R("rock.stp_reef_3",      "Rock_reef",    139.68f,  76.68f, "skerry",  "sandstone", "wet", 2, false, "Skerry_sandstone_c2_r1",  "Skerry_sandstone"),
            R("rock.stp_reef_4",      "Rock_reef",    -33.39f,  73.90f, "skerry",  "sandstone", "wet", 2, true,  "Skerry_sandstone_c6_r1",  "Skerry_sandstone"),
            R("rock.stp_reef_5",      "Rock_reef",    -69.33f, -40.21f, "skerry",  "sandstone", "wet", 2, false, "Skerry_sandstone_c2_r1",  "Skerry_sandstone"),
            R("rock.stp_reef_6",      "Rock_reef",    -19.64f, -73.96f, "skerry",  "sandstone", "wet", 2, true,  "Skerry_sandstone_c6_r1",  "Skerry_sandstone"),
            R("rock.stp_reef_7",      "Rock_reef",    131.78f, -76.43f, "skerry",  "sandstone", "wet", 2, false, "Skerry_sandstone_c2_r1",  "Skerry_sandstone"),
            R("rock.stp_reef_8",      "Rock_reef",     39.11f, -84.05f, "skerry",  "sandstone", "wet", 2, true,  "Skerry_sandstone_c6_r1",  "Skerry_sandstone"),
            R("rock.stp_s_1",         "Rock_s",        19.11f,  83.76f, "wedge",   "sandstone", "wet", 2, false, "Wedge_sandstone_c2_r1",   "Wedge_sandstone"),
            R("rock.stp_s_2",         "Rock_s",       146.46f,  81.71f, "wedge",   "sandstone", "wet", 2, true,  "Wedge_sandstone_c6_r1",   "Wedge_sandstone"),
            R("rock.stp_s_3",         "Rock_s",       -67.45f, -44.64f, "wedge",   "sandstone", "wet", 2, false, "Wedge_sandstone_c2_r1",   "Wedge_sandstone"),
        };

        /// <summary>Every St Peters swap: the nine, then the rest.</summary>
        internal static readonly Row[] All = Nine.Concat(Rest).ToArray();

        /// <summary>
        /// The thirteen committed rocks the shore painter no longer places: its scatter has moved on
        /// since the scene was painted (today it places 21 rocks and the scene keeps 33). A Shoreline
        /// refresh would have deleted these rocks. Their Defs now stop it with a refusal instead, until
        /// someone decides whether the painter or the scene is right.
        /// </summary>
        internal static readonly string[] NotPaintedToday =
        {
            "rock.stp_bl_1", "rock.stp_bl_2", "rock.stp_bm_1", "rock.stp_bm_2", "rock.stp_bs_1",
            "rock.stp_m_2", "rock.stp_m_3", "rock.stp_m_4",
            "rock.stp_reef_5", "rock.stp_reef_6", "rock.stp_reef_7", "rock.stp_reef_8", "rock.stp_s_3",
        };

        /// <summary>Every swap whose rock the painter still places today.</summary>
        internal static readonly Row[] PaintedToday = All.Where(r => !NotPaintedToday.Contains(r.Id)).ToArray();

        /// <summary>"Within a hundredth of a metre of At" (ShoreRockDef's own contract).</summary>
        const float MatchMetres = 0.01f;

        /// <summary>The rows of <see cref="All"/> that name this rock: its name, within a hundredth of a metre of its place.</summary>
        internal static List<Row> RowsNaming(LayerRefreshSceneText t, long go)
        {
            string name = t.Field(go, "m_Name");
            Vector2 at = t.Vec3(t.TransformOf(go), "m_LocalPosition");
            return All.Where(r => r.Today == name && Vector2.Distance(at, new Vector2(r.X, r.Y)) <= MatchMetres).ToList();
        }

        internal const long TestFileId = 21300000;
        internal static readonly Vector2 TestSize = new Vector2(1.25f, 0.75f);
        const string TestSizeYaml = "{x: 1.25, y: 0.75}";

        /// <summary>The test's own GUID for a cell: a hash of the cell's name, which no asset holds.</summary>
        internal static string TestGuid(string cell)
        {
            using (MD5 md5 = MD5.Create())
                return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes("ShoreRockSwapTests:" + cell))
                                        .Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        static string TestSpriteYaml(string cell) => $"{{fileID: {TestFileId.ToString(CultureInfo.InvariantCulture)}, guid: {TestGuid(cell)}, type: 3}}";

        /// <summary>
        /// The sprites the step is given: for the painter's keys, the ShoreIso sheet's own sprite for the
        /// key (its GUID, its internal id and its rect over its pixels per unit, read from the sheet's
        /// <c>.meta</c>: since the shaded-rocks PR no committed rock wears one); for a swap, the test's own
        /// sprite for the cell it names, and a record of every cell asked for.
        /// </summary>
        internal sealed class TestShoreRockSprites : StPetersLayerRefresh.IShoreRockSprites
        {
            static readonly Regex GuidRx = new Regex(@"^guid: ([0-9a-f]{32})", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            static readonly Regex PpuRx = new Regex(@"spritePixelsToUnits: ([0-9.]+)", RegexOptions.CultureInvariant);

            readonly Dictionary<string, StPetersLayerRefresh.SpriteRef> _iso =
                new Dictionary<string, StPetersLayerRefresh.SpriteRef>(StringComparer.Ordinal);
            readonly bool _haveRockPx;
            public readonly List<string> Asked = new List<string>();

            public TestShoreRockSprites(bool haveRockPx = true)
            {
                _haveRockPx = haveRockPx;
                string meta = File.ReadAllText(StPetersShorePainter.RockSheetPath + ".meta");
                string guid = GuidRx.Match(meta).Groups[1].Value;
                float ppu = float.Parse(PpuRx.Match(meta).Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                foreach (string key in ShorelineIsoCatalog.RockSprites)
                {
                    string name = "ShoreIsoSprites_" + key;
                    Match id = Regex.Match(meta, @"^\s+" + Regex.Escape(name) + @": (-?\d+)\s*$", RegexOptions.Multiline);
                    Match rect = Regex.Match(meta, @"name: " + Regex.Escape(name) + @"\s*\n\s+rect:\s*\n\s+serializedVersion: 2\s*\n" +
                                                   @"\s+x: [-0-9.]+\s*\n\s+y: [-0-9.]+\s*\n\s+width: ([0-9.]+)\s*\n\s+height: ([0-9.]+)");
                    if (!id.Success || !rect.Success) continue;   // the sheet has no sprite for that key, and the painter skips it too
                    var size = new Vector2(float.Parse(rect.Groups[1].Value, CultureInfo.InvariantCulture) / ppu,
                                           float.Parse(rect.Groups[2].Value, CultureInfo.InvariantCulture) / ppu);
                    _iso[key] = new StPetersLayerRefresh.SpriteRef(
                        new StPetersLayerRefresh.ObjRef(long.Parse(id.Groups[1].Value, CultureInfo.InvariantCulture), guid, 3), size);
                }
                Assert.IsNotEmpty(_iso, "the test read no ShoreIso rock sprite from " + StPetersShorePainter.RockSheetPath + ".meta");
            }

            public bool TryShoreIso(string spriteKey, out StPetersLayerRefresh.SpriteRef sprite) =>
                _iso.TryGetValue(spriteKey ?? "", out sprite);

            public bool TryRockPx(ShoreRockDef def, out StPetersLayerRefresh.SpriteRef sprite)
            {
                string cell = StPetersLayerRefresh.RockPxSpriteName(def);
                Asked.Add(cell);
                sprite = new StPetersLayerRefresh.SpriteRef(new StPetersLayerRefresh.ObjRef(TestFileId, TestGuid(cell), 3), TestSize);
                return _haveRockPx;
            }
        }

        /// <summary>
        /// The Shoreline step with the rows' swaps against the same step without them, on one placement:
        /// the documents, their order and every byte agree except the named rocks' SpriteRenderers, and
        /// those differ only in the sprite (the test's own for the row's cell) and its size.
        /// </summary>
        internal static void SwapChangesOnlyTheNamed(string sceneText, IReadOnlyList<StPetersLayerRefresh.PlacedRock> placement,
                                                     IReadOnlyList<Row> rowsSwapped, IReadOnlyList<ShoreRockDef> swaps,
                                                     bool placementIsTheCommittedRocks)
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(sceneText);
            var sprites = new TestShoreRockSprites();

            string plain = StPetersLayerRefresh.Shoreline(scene, placement, sprites, Array.Empty<ShoreRockDef>()).ApplyTo(sceneText);
            Assert.IsEmpty(sprites.Asked, "without swaps, the step asked for a Rock Px sprite");

            string swapped = StPetersLayerRefresh.Shoreline(scene, placement, sprites, swaps).ApplyTo(sceneText);
            CollectionAssert.AreEquivalent(rowsSwapped.Select(r => r.Cell).ToList(), sprites.Asked, "the cells the swaps asked the kit for");

            var p = new LayerRefreshSceneText(plain);
            var s = new LayerRefreshSceneText(swapped);
            CollectionAssert.AreEqual(p.Order, s.Order, "the swaps added, removed or reordered a document");

            // The named rocks, found by this test's own search of the rocks: the rock of the row's name within a
            // hundredth of a metre of its place, exactly one for each row, a different rock for each row.
            var partOf = new Dictionary<long, (Row Row, string Part)>();
            var renderers = new Dictionary<long, Row>();
            long rocks = p.ChildNamed(p.RootGameObject(StPetersShorePainter.RootName), StPetersShorePainter.RocksRootName);
            List<long> rockGos = p.ChildGameObjects(rocks);
            foreach (Row row in rowsSwapped)
            {
                List<long> hits = rockGos.Where(go => p.Field(go, "m_Name") == row.Today &&
                                                      Vector2.Distance(p.Vec3(p.TransformOf(go), "m_LocalPosition"),
                                                                       new Vector2(row.X, row.Y)) <= MatchMetres).ToList();
                Assert.AreEqual(1, hits.Count, $"{row.Id}: rocks named {row.Today} within {MatchMetres} m of ({row.X}, {row.Y})");
                long go = hits[0];
                Assert.IsFalse(partOf.ContainsKey(go), $"{row.Id} names a rock another row names");
                partOf[go] = (row, "GameObject");
                foreach (long c in p.Refs(go, "m_Component"))
                    partOf[c] = (row, p.ClassOf(c) == 4 ? "Transform (its place, rotation and scale)" : p.ClassOf(c) == 212 ? "SpriteRenderer" : "YSortSprite");
                renderers[p.ComponentOfClass(go, 212)] = row;
            }

            // Since the shaded-rocks PR every committed rock wears its swap: without swaps, on the committed rocks,
            // the step hands each named rock back its key's ShoreIso sprite and changes nothing else.
            if (placementIsTheCommittedRocks)
            {
                var committed = new LayerRefreshSceneText(sceneText);
                CollectionAssert.AreEqual(committed.Order, p.Order,
                                          "without swaps, on the committed rocks, the step added, removed or reordered a document");
                foreach (long id in committed.Order)
                {
                    if (!renderers.TryGetValue(id, out Row row))
                    {
                        Assert.IsTrue(committed.Text(id) == p.Text(id),
                                      "without swaps, with the committed rocks as the placement and the sprites they wear, " +
                                      $"the step changed {committed.ObjectName(id)} (&{id})");
                        continue;
                    }
                    string[] was = committed.Text(id).Split('\n'), now = p.Text(id).Split('\n');
                    Assert.AreEqual(was.Length, now.Length, $"{row.Id}: without swaps, the step changed the SpriteRenderer's shape");
                    for (int i = 0; i < was.Length; i++)
                        if (was[i] != now[i])
                            Assert.IsTrue(was[i].StartsWith("  m_Sprite: ", StringComparison.Ordinal) || was[i].StartsWith("  m_Size: ", StringComparison.Ordinal),
                                          $"{row.Id}: without swaps, the step changed '{was[i]}' to '{now[i]}'; only the sprite and its size may change");
                }
            }

            foreach (long id in p.Order)
            {
                if (!renderers.TryGetValue(id, out Row row))
                {
                    if (p.Text(id) == s.Text(id)) continue;
                    Assert.Fail(partOf.TryGetValue(id, out (Row Row, string Part) part)
                                    ? $"{part.Row.Id}: the swap changed its rock's {part.Part}"
                                    : $"{p.ObjectName(id)} (&{id}) is no row's rock, and the swaps changed it");
                }

                string[] a = p.Text(id).Split('\n'), b = s.Text(id).Split('\n');
                Assert.AreEqual(a.Length, b.Length, $"{row.Id}: the swap changed the SpriteRenderer's shape");
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i])
                        Assert.IsTrue(a[i].StartsWith("  m_Sprite: ", StringComparison.Ordinal) || a[i].StartsWith("  m_Size: ", StringComparison.Ordinal),
                                      $"{row.Id}: the swap changed '{a[i]}' to '{b[i]}'; only the sprite and its size may change");
                Assert.AreEqual(TestSpriteYaml(row.Cell), s.Field(id, "m_Sprite"), $"{row.Id}: the sprite it wears");
                Assert.AreEqual(TestSizeYaml, s.Field(id, "m_Size"), $"{row.Id}: the size its sprite gives it");
            }
        }

        /// <summary>The Shoreline step, on the committed rocks, refuses these swaps with this message.</summary>
        internal static void Refused(string sceneText, IReadOnlyList<ShoreRockDef> swaps, string messagePart, bool haveRockPx = true)
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(sceneText);
            var sprites = new TestShoreRockSprites(haveRockPx);
            var refusal = Assert.Throws<StPetersLayerRefresh.Refusal>(() =>
                StPetersLayerRefresh.Shoreline(scene, StPetersLayerRefresh.RocksInScene(scene), sprites, swaps));
            StringAssert.Contains(messagePart, refusal.Message);
        }

        /// <summary>
        /// A row's Def names the row's cell, and the kit's own slicer reads that name back as the Def's
        /// variant, mirror and tide; the sheet is the row's bare albedo sheet, and it is in the kit.
        /// </summary>
        internal static void NamesItsCell(Row row, ShoreRockDef def)
        {
            string cell = StPetersLayerRefresh.RockPxSpriteName(def);
            Assert.AreEqual(row.Cell, cell, $"{row.Id}: its cell");
            int col = RockPxSheetSlicer.ColumnOf(cell, row.Sheet), tide = RockPxSheetSlicer.RowOf(cell, row.Sheet);
            Assert.That(col, Is.GreaterThanOrEqualTo(0), $"{row.Id}: the slicer reads no column from {cell}");
            Assert.That(tide, Is.GreaterThanOrEqualTo(0), $"{row.Id}: the slicer reads no row from {cell}");
            Assert.AreEqual(def.Variant, RockPxCatalog.VariantForColumn(col), $"{row.Id}: the variant its column draws");
            Assert.AreEqual(def.Mirrored, RockPxCatalog.IsMirroredColumn(col), $"{row.Id}: whether its column is a mirror");
            Assert.AreEqual(row.State, RockPxCatalog.Tides[tide], $"{row.Id}: the tide its row draws");

            string sheet = StPetersLayerRefresh.RockPxSheetPath(def);
            Assert.AreEqual("Assets/_Project/Art/Sprites/Shore/RockPx/tex/" + row.Sheet + ".png", sheet, $"{row.Id}: its sheet");
            Assert.IsTrue(File.Exists(sheet), $"{row.Id}: {sheet} is not in the kit");
        }
    }
}
