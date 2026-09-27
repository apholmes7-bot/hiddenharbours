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
    /// asset (checked). No Rock Px GUID is read or written, because the kit's metas are not committed.
    /// The ShoreIso sprites are the ones the committed rocks already wear, read from the scene text, so
    /// that without swaps the step leaves the committed rocks exactly as they are (checked first).</para>
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

        // =============================================================================================
        //  the swap
        // =============================================================================================

        [Test]
        public void TheNineSwaps_ChangeOnlyTheNineSprites_OnTheCommittedRocks()
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(_sceneText);
            ShoreRockSwapChecks.SwapChangesOnlyTheNine(_sceneText, StPetersLayerRefresh.RocksInScene(scene), TheNine(),
                                                       placementIsTheCommittedRocks: true);
        }

        [Test]
        public void TheNineSwaps_ChangeOnlyTheNineSprites_OnThePaintersPlacementToday()
        {
            _terrainGo = new GameObject("TidalTerrain_ShoreRockSwapTest");
            var terrain = _terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);   // the same zones the scene is built with
            ShoreRockSwapChecks.SwapChangesOnlyTheNine(_sceneText, StPetersLayerRefresh.PainterPlacement(terrain), TheNine(),
                                                       placementIsTheCommittedRocks: false);
        }

        [Test]
        public void TheTestsRockPxSprites_AreNoAsset()
        {
            foreach (ShoreRockSwapChecks.Row row in ShoreRockSwapChecks.Nine)
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
        public void TheNine_NameTheKitsCellsAndSheets()
        {
            List<ShoreRockDef> nine = TheNine();
            for (int i = 0; i < nine.Count; i++)
                ShoreRockSwapChecks.NamesItsCell(ShoreRockSwapChecks.Nine[i], nine[i]);
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

            /// <summary>The table's six columns. Dress, variant and mirror are left at the Def's own
            /// defaults: part 2 names none, so the rock is bare, variant A, unmirrored.</summary>
            public void WriteTo(ShoreRockDef def)
            {
                def.Id = Id;
                def.Today = Today;
                def.At = new Vector2(X, Y);
                def.Form = Form;
                def.Stone = Stone;
                def.State = State;
            }
        }

        static Row R(string id, string today, float x, float y, string form, string stone, string state, string cell, string sheet) =>
            new Row { Id = id, Today = today, X = x, Y = y, Form = form, Stone = stone, State = state, Cell = cell, Sheet = sheet };

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

        /// <summary>"Within a hundredth of a metre of At" (ShoreRockDef's own contract).</summary>
        const float MatchMetres = 0.01f;

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
        /// The sprites the step is given: for the painter's keys, the ShoreIso sprite the committed
        /// rocks of that key already wear (read from the scene text); for a swap, the test's own
        /// sprite for the cell it names, and a record of every cell asked for.
        /// </summary>
        internal sealed class TestShoreRockSprites : StPetersLayerRefresh.IShoreRockSprites
        {
            static readonly Regex TypeRx = new Regex(@"type: (\d+)", RegexOptions.CultureInvariant);

            readonly Dictionary<string, StPetersLayerRefresh.SpriteRef> _worn =
                new Dictionary<string, StPetersLayerRefresh.SpriteRef>(StringComparer.Ordinal);
            readonly bool _haveRockPx;
            public readonly List<string> Asked = new List<string>();

            public TestShoreRockSprites(string committedSceneText, bool haveRockPx = true)
            {
                _haveRockPx = haveRockPx;
                var t = new LayerRefreshSceneText(committedSceneText);
                long rocks = t.ChildNamed(t.RootGameObject(StPetersShorePainter.RootName), StPetersShorePainter.RocksRootName);
                foreach (long go in t.ChildGameObjects(rocks))
                {
                    string name = t.Field(go, "m_Name");
                    string key = name.Substring(name.LastIndexOf('_') + 1);   // the painter names a rock <prefix>_<key>
                    long sr = t.ComponentOfClass(go, 212);
                    string sprite = t.Field(sr, "m_Sprite");
                    Match type = TypeRx.Match(sprite ?? "");
                    var worn = new StPetersLayerRefresh.SpriteRef(
                        new StPetersLayerRefresh.ObjRef(LayerRefreshSceneText.FileIdIn(sprite), LayerRefreshSceneText.GuidIn(sprite),
                                                        type.Success ? int.Parse(type.Groups[1].Value, CultureInfo.InvariantCulture) : 0),
                        t.Vec2(sr, "m_Size"));
                    if (!_worn.TryGetValue(key, out StPetersLayerRefresh.SpriteRef had))
                    {
                        _worn.Add(key, worn);
                        continue;
                    }
                    Assert.IsTrue(had.Sprite.FileId == worn.Sprite.FileId && had.Sprite.Guid == worn.Sprite.Guid &&
                                  had.Sprite.Type == worn.Sprite.Type && had.Size == worn.Size,
                                  $"the committed '{key}' rocks wear two different sprites, so the test cannot say which is the key's");
                }
            }

            public bool TryShoreIso(string spriteKey, out StPetersLayerRefresh.SpriteRef sprite) =>
                _worn.TryGetValue(spriteKey ?? "", out sprite);

            public bool TryRockPx(ShoreRockDef def, out StPetersLayerRefresh.SpriteRef sprite)
            {
                string cell = StPetersLayerRefresh.RockPxSpriteName(def);
                Asked.Add(cell);
                sprite = new StPetersLayerRefresh.SpriteRef(new StPetersLayerRefresh.ObjRef(TestFileId, TestGuid(cell), 3), TestSize);
                return _haveRockPx;
            }
        }

        /// <summary>
        /// The Shoreline step with the nine swaps against the same step without them, on one placement:
        /// the documents, their order and every byte agree except the nine rocks' SpriteRenderers, and
        /// those differ only in the sprite (the test's own for the row's cell) and its size.
        /// </summary>
        internal static void SwapChangesOnlyTheNine(string sceneText, IReadOnlyList<StPetersLayerRefresh.PlacedRock> placement,
                                                    IReadOnlyList<ShoreRockDef> nine, bool placementIsTheCommittedRocks)
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(sceneText);
            var sprites = new TestShoreRockSprites(sceneText);

            string plain = StPetersLayerRefresh.Shoreline(scene, placement, sprites, Array.Empty<ShoreRockDef>()).ApplyTo(sceneText);
            if (placementIsTheCommittedRocks)
                Assert.IsTrue(plain == sceneText,
                              "without swaps, with the committed rocks as the placement and the sprites they wear, the step changed the scene");
            Assert.IsEmpty(sprites.Asked, "without swaps, the step asked for a Rock Px sprite");

            string swapped = StPetersLayerRefresh.Shoreline(scene, placement, sprites, nine).ApplyTo(sceneText);
            CollectionAssert.AreEquivalent(Nine.Select(r => r.Cell).ToList(), sprites.Asked, "the cells the nine swaps asked the kit for");

            var p = new LayerRefreshSceneText(plain);
            var s = new LayerRefreshSceneText(swapped);
            CollectionAssert.AreEqual(p.Order, s.Order, "the swaps added, removed or reordered a document");

            // The nine, found by this test's own search of the rocks: the rock of the row's name within a
            // hundredth of a metre of its place, exactly one for each row, nine different rocks.
            var partOf = new Dictionary<long, (Row Row, string Part)>();
            var renderers = new Dictionary<long, Row>();
            long rocks = p.ChildNamed(p.RootGameObject(StPetersShorePainter.RootName), StPetersShorePainter.RocksRootName);
            List<long> rockGos = p.ChildGameObjects(rocks);
            foreach (Row row in Nine)
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

            foreach (long id in p.Order)
            {
                if (!renderers.TryGetValue(id, out Row row))
                {
                    if (p.Text(id) == s.Text(id)) continue;
                    Assert.Fail(partOf.TryGetValue(id, out (Row Row, string Part) part)
                                    ? $"{part.Row.Id}: the swap changed its rock's {part.Part}"
                                    : $"{p.ObjectName(id)} (&{id}) is not one of the nine, and the swaps changed it");
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
            var sprites = new TestShoreRockSprites(sceneText, haveRockPx);
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
