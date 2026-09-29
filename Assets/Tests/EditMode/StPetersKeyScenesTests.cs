using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.World;
using Object = UnityEngine.Object;
using KeyScenePlacement = HiddenHarbours.App.Editor.StPetersLayerRefresh.KeyScenePlacement;
using KeySceneSetPiece = HiddenHarbours.App.Editor.StPetersLayerRefresh.KeySceneSetPiece;
using LayerPatch = HiddenHarbours.App.Editor.StPetersLayerRefresh.LayerPatch;
using ObjRef = HiddenHarbours.App.Editor.StPetersLayerRefresh.ObjRef;
using Op = HiddenHarbours.App.Editor.StPetersLayerRefresh.Op;
using OpKind = HiddenHarbours.App.Editor.StPetersLayerRefresh.OpKind;
using Refusal = HiddenHarbours.App.Editor.StPetersLayerRefresh.Refusal;
using ScriptRef = HiddenHarbours.App.Editor.StPetersLayerRefresh.ScriptRef;
using SpriteRef = HiddenHarbours.App.Editor.StPetersLayerRefresh.SpriteRef;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE KEY SCENES STEP, ON A SCENE OF ITS OWN</b> (the key scenes' Landing placement). Subject:
    /// <see cref="StPetersLayerRefresh.KeyScenePieces"/> and the pure parts it is made of, planned against a
    /// small synthetic scene with made-up art, so every claim here holds without the AssetDatabase: which cell
    /// each kit turns to CD's facing, where a piece sorts, the walls it gets, the row a tide board's sea is cut
    /// at, which way the harbour vane points; and that the patch adds one root, joins the scene's roots by one
    /// named edit, writes once, names what it deletes, and refuses what it cannot place truly.
    /// </summary>
    public class StPetersKeyScenesStepTests
    {
        const string SetPiecesKit = StPetersLayerRefresh.SetPiecesKit;
        const string DecorKit = StPetersLayerRefresh.DecorKit;
        const string FindsKit = StPetersLayerRefresh.FindsKit;
        const string Quay = "keyscene.test_quay", Yard = "keyscene.test_yard";

        static readonly string CopyPath =
            Path.Combine(Path.GetTempPath(), "hh-st-peters-key-scenes-tests", "Synthetic.copy.unity");

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            KeySceneTestData.Destroy(_made);
            _made.Clear();
            if (File.Exists(CopyPath)) File.Delete(CopyPath);
        }

        KeySceneDef Scene(string id, params KeyScenePiece[] pieces)
        {
            KeySceneDef ks = KeySceneTestData.NewKeyScene(id, pieces);
            _made.Add(ks);
            return ks;
        }

        static KeyScenePiece Piece(string id, string kit, string piece, float x, float y, int dir, string state = "") =>
            KeySceneTestData.Piece(id, kit, piece, x, y, dir, state);

        static LayerPatch Plan(string text, IReadOnlyList<KeySceneDef> scenes, FakeKeySceneAssets assets) =>
            StPetersLayerRefresh.KeyScenePieces(StPetersLayerRefresh.SceneYaml.Parse(text), scenes, assets);

        /// <summary>
        /// Two small key scenes that hold one of every kind of piece the step writes: a tide board (walls and a
        /// waterline), a vane, a lit piece, an iso decor prop, a floor piece, a find, a piece with CD's own sort
        /// line, and a piece that stands in a later variant only.
        /// </summary>
        (List<KeySceneDef> Scenes, FakeKeySceneAssets Assets) TwoScenes()
        {
            var assets = new FakeKeySceneAssets();
            assets.Add("board", FakeKeySceneAssets.TideBoard("board", "prop.test_board"));
            assets.Add("vane", FakeKeySceneAssets.WindVane("vane", "prop.test_vane"));
            assets.Add("lantern", FakeKeySceneAssets.SetPiece("lantern", "prop.test_lantern")).HasLight = true;
            assets.Add("rails", FakeKeySceneAssets.SetPiece("rails", "structure.test_rails", StPetersLayerRefresh.FloorLayer));
            assets.Add("stack", FakeKeySceneAssets.SetPiece("stack", "structure.test_stack"));

            KeyScenePiece restored = Piece("prop.test_restored", DecorKit, "toteStack", 13f, 2.5f, 0);
            restored.Variants = new[] { "restored" };
            KeyScenePiece stack = Piece("structure.test_stack", SetPiecesKit, "stack", 24f, 8f, 7);
            stack.HasSortY = true;
            stack.SortY = 9.5f;
            var scenes = new List<KeySceneDef>
            {
                Scene(Quay,
                      Piece("prop.test_board", SetPiecesKit, "board", 10f, -2f, 4),
                      Piece("prop.test_vane", SetPiecesKit, "vane", 12f, 1f, 0),
                      Piece("prop.test_lantern", SetPiecesKit, "lantern", 14f, 3f, 2),
                      Piece("prop.test_traps", DecorKit, "trapStack", 11f, 2.5f, 4),
                      restored),
                Scene(Yard,
                      Piece("structure.test_rails", SetPiecesKit, "rails", 20f, 5f, 0),
                      Piece("prop.test_plank", FindsKit, "DriftPlank", 22f, 7f, 3, "dry"),
                      stack),
            };
            return (scenes, assets);
        }

        static readonly string[] TodaysPieces =
        {
            "prop.test_board", "prop.test_vane", "prop.test_lantern", "prop.test_traps",
            "structure.test_rails", "prop.test_plank", "structure.test_stack",
        };

        static int Order(float y) => YSortSprite.OrderFor(y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                                          SortingBands.DecorFloor, SortingBands.DecorCeiling);

        // =============================================================================================
        //  the pure parts
        // =============================================================================================

        [TestCase(0, 0f), TestCase(1, 315f), TestCase(2, 270f), TestCase(3, 225f), TestCase(4, 180f),
         TestCase(5, 135f), TestCase(6, 90f), TestCase(7, 45f), TestCase(8, 0f), TestCase(-1, 45f)]
        public void KitDirs_TurnCounterclockwiseFromNorth(int dir, float heading) =>
            Assert.AreEqual(heading, StPetersLayerRefresh.KitDirHeading(dir), 0f,
                            $"kit dir {dir} faces the wrong compass heading (the kits' dirs turn counterclockwise: 1 NW, 2 W, 6 E)");

        [Test]
        public void SortLine_IsCdsOwnLine_WhereCdGivesOne()
        {
            KeyScenePiece plain = Piece("prop.test_plain", SetPiecesKit, "plain", 10f, 5f, 0);
            KeyScenePiece lined = Piece("prop.test_lined", SetPiecesKit, "lined", 10f, 5f, 0);
            lined.HasSortY = true;
            lined.SortY = 7.5f;
            Assert.AreEqual(0f, StPetersLayerRefresh.SortYOffsetFor(plain), "a piece CD gives no line sorts at its ground point");
            Assert.AreEqual(2.5f, StPetersLayerRefresh.SortYOffsetFor(lined), 1e-6f, "a piece CD gives a line sorts on that line");
        }

        [Test]
        public void TideLip_IsWhereTheMarksMeetTheirFoot()
        {
            KeySceneSetPiece board = FakeKeySceneAssets.TideBoard("board", "prop.test_board");
            var at = new Vector2(10f, -2f);
            Assert.IsTrue(StPetersLayerRefresh.TryTideLip(board, 4, at, out float lipY, out float lipZ), "a board with marks has a lip");
            Assert.AreEqual(FakeKeySceneAssets.BoardFoot, lipZ, 1e-5f, "the lip is the height the marks were painted up from");
            // Turned to dir 4 the face looks south: its 0.2 m plan offset lies 0.2 m south of the ground point,
            // which draws 0.2·sin 40° lower on screen.
            Assert.AreEqual(at.y - 0.2f * Mathf.Sin(40f * Mathf.Deg2Rad), lipY, 1e-5f, "the lip's row");

            // …so a sea standing at any mark's height draws its edge on that mark's own row.
            foreach (SetPieceTideMark m in board.TideMarks)
            {
                float markRow = at.y + (float)(SetPieceDef.ToWorld(new Vector2(m.At.x, m.At.y), 4).y * Math.Sin(40.0 * Math.PI / 180.0))
                                + m.At.z * Mathf.Cos(40f * Mathf.Deg2Rad);
                Assert.AreEqual(markRow, TidalFaceWaterline.WaterlineWorldY(lipY, lipZ, m.Z), 0.5f / 32f,
                                $"the sea at {m.Z} m does not meet the mark painted for it");
            }

            Assert.IsFalse(StPetersLayerRefresh.TryTideLip(FakeKeySceneAssets.SetPiece("post", "prop.test_post"), 4, at, out _, out _),
                           "a piece with no marks has no lip");
        }

        [TestCase("foot"), TestCase("face")]
        public void TideLip_RefusesMarksThatDisagree(string where)
        {
            KeySceneSetPiece board = FakeKeySceneAssets.TideBoard("board", "prop.test_board");
            SetPieceTideMark last = board.TideMarks[board.TideMarks.Length - 1];
            if (where == "foot") last.At = new Vector3(last.At.x, last.At.y, last.At.z + 0.05f);
            else last.At = new Vector3(last.At.x, last.At.y + 0.05f, last.At.z);
            var refusal = Assert.Throws<Refusal>(() => StPetersLayerRefresh.TryTideLip(board, 4, Vector2.zero, out _, out _),
                                                 $"marks that disagree about their {where} were read as one waterline");
            StringAssert.Contains("disagree", refusal.Message);
        }

        [Test]
        public void TideLip_ReadsAMarkWithinHalfAPixel_AtWhatTheMarksAgreeOn()
        {
            // The kit paints its spring-high mark a centimetre up the post from the others' foot: a quarter of a
            // pixel at 32 px/m, which no sea's edge can show.
            KeySceneSetPiece board = FakeKeySceneAssets.TideBoard("board", "prop.test_board");
            SetPieceTideMark last = board.TideMarks[board.TideMarks.Length - 1];
            last.At = new Vector3(last.At.x, last.At.y, last.At.z + 0.01f);
            Assert.IsTrue(StPetersLayerRefresh.TryTideLip(board, 4, Vector2.zero, out _, out float lipZ),
                          "a centimetre's rounding refused the board");
            Assert.AreEqual(FakeKeySceneAssets.BoardFoot, lipZ, 1e-5f, "the lip is the foot most marks agree on, not the odd one's");
        }

        [TestCase(0, 0.5f, -0.25f), TestCase(1, 0.1767767f, -0.5303301f), TestCase(2, -0.25f, -0.5f),
         TestCase(4, -0.5f, 0.25f), TestCase(6, 0.25f, 0.5f)]
        public void Walls_AreWalkPolygons_TurnedToTheFrame(int frame, float x, float y)
        {
            // A frame shows rig dir (8 − frame) mod 8, and a dir turns the footprint counterclockwise by 45° a step.
            List<Vector2[]> walls = StPetersLayerRefresh.WallsOf(new[] { FakeKeySceneAssets.Wall(0.5f, 0.25f) }, frame, "a test wall");
            Assert.AreEqual(1, walls.Count, "one collider, one wall");
            Assert.AreEqual(4, walls[0].Length, "every corner kept");
            Assert.AreEqual(x, walls[0][1].x, 1e-5f, $"frame {frame}: the footprint's (0.5, −0.25) corner, x");
            Assert.AreEqual(y, walls[0][1].y, 1e-5f, $"frame {frame}: the footprint's (0.5, −0.25) corner, y");
        }

        [TestCase("circle"), TestCase("sight"), TestCase("two points"), TestCase("empty")]
        public void Walls_RefuseWhatTheStepDoesNotWrite(string fault)
        {
            SetPieceCollider c = FakeKeySceneAssets.Wall(0.5f, 0.25f);
            if (fault == "circle") c.Type = "circle";
            else if (fault == "sight") c.Blocks = "sight";
            else if (fault == "two points") c.Points = c.Points.Take(2).ToArray();
            else c = null;
            Assert.Throws<Refusal>(() => StPetersLayerRefresh.WallsOf(new[] { c }, 0, "a test wall"),
                                   $"a collider with {fault} was written as a walk-blocking polygon");
        }

        [Test]
        public void Place_SortsRaisedByItsLine_AndFloorUnderFigures()
        {
            var (scenes, assets) = TwoScenes();
            List<KeyScenePlacement> placed = StPetersLayerRefresh.PlaceKeyScenes(scenes, assets);
            CollectionAssert.AreEqual(TodaysPieces, placed.Select(p => p.Id).ToList(),
                                      "today's pieces in scene then CD order (the restored piece stands in a later variant only)");

            KeyScenePlacement rails = placed.Single(p => p.Id == "structure.test_rails");
            Assert.IsTrue(rails.Floor, "a floor piece is placed as floor");
            Assert.AreEqual(SortingBands.DecorFloor, rails.SortingOrder, "a floor piece sits under every figure");

            KeyScenePlacement stack = placed.Single(p => p.Id == "structure.test_stack");
            Assert.IsFalse(stack.Floor);
            Assert.AreEqual(1.5f, stack.SortYOffset, 1e-6f, "a raised piece sorts on CD's line, 1.5 m north of its ground point");
            Assert.AreEqual(Order(9.5f), stack.SortingOrder, "…at that line's order");

            KeyScenePlacement board = placed.Single(p => p.Id == "prop.test_board");
            KeyScenePlacement vane = placed.Single(p => p.Id == "prop.test_vane");
            Assert.AreEqual(Order(-2f), board.SortingOrder, "a raised piece CD gives no line sorts at its ground point");
            Assert.Greater(board.SortingOrder, vane.SortingOrder, "a piece further south draws in front");
        }

        [Test]
        public void Place_TurnsEachKitToCdsFacing()
        {
            var (scenes, assets) = TwoScenes();
            Dictionary<string, KeyScenePlacement> placed = StPetersLayerRefresh.PlaceKeyScenes(scenes, assets).ToDictionary(p => p.Id);

            // A set piece's frames go clockwise and CD's dirs counterclockwise: dir d shows frame (8 − d) mod 8.
            AssertSprite("board/frame4", placed["prop.test_board"], "dir 4 shows frame 4");
            AssertSprite("vane/frame0", placed["prop.test_vane"], "dir 0 shows frame 0");
            AssertSprite("lantern/frame6", placed["prop.test_lantern"], "dir 2 shows frame 6");
            AssertSprite("stack/frame1", placed["structure.test_stack"], "dir 7 shows frame 1");

            // An iso prop shows the cell its kit's own convention gives for CD's dir, never the dir itself.
            AssertSprite(FakeKeySceneAssets.IsoKey(DecorKit, "trapStack", FakeKeySceneAssets.FacingOf(4)), placed["prop.test_traps"],
                         "the decor kit's cell for dir 4");
            // A find lies as CD laid it, in the state CD gave, from the kit's first variant row.
            AssertSprite(FakeKeySceneAssets.FindKey("DriftPlank", "dry", 3, StPetersLayerRefresh.FindVariant), placed["prop.test_plank"],
                         "the dry plank at lie 3");
        }

        static void AssertSprite(string key, KeyScenePlacement p, string what) =>
            Assert.AreEqual(FakeKeySceneAssets.SpriteOf(key).Sprite.Yaml, p.Sprite.Sprite.Yaml, $"{p.Id}: {what}");

        static readonly string[] Unplaceable =
        {
            "a Def under another id", "a wall mount", "seven frames", "an unknown layer", "an unknown kit",
            "a find with no state", "a vane turned from its headings", "a vane on another input", "a piece placed twice",
            "no Def in the kit", "a cell the kit lacks", "a cell that is no sprite", "a tide board with no scale",
        };

        [TestCaseSource(nameof(Unplaceable))]
        public void Place_RefusesWhatItCannotPlaceTruly(string fault)
        {
            var assets = new FakeKeySceneAssets();
            KeySceneSetPiece sp = assets.Add("piece", FakeKeySceneAssets.SetPiece("piece", "prop.test_piece"));
            KeyScenePiece piece = Piece("prop.test_piece", SetPiecesKit, "piece", 5f, 5f, 0);
            var scenes = new List<KeySceneDef> { Scene(Quay, piece) };
            string expect;
            switch (fault)
            {
                case "a Def under another id": sp.Id = "prop.test_other"; expect = "placed under its Def's id"; break;
                case "a wall mount": sp.Mount = "wall"; expect = "mounted on 'wall'"; break;
                case "seven frames": sp.Frames = sp.Frames.Take(7).ToArray(); expect = "7 frames"; break;
                case "an unknown layer": sp.Layer = "overhead"; expect = "layer 'overhead'"; break;
                case "an unknown kit": piece.Kit = "wharfBuilding2"; expect = "places from"; break;
                case "a find with no state": piece.Kit = FindsKit; piece.Piece = "Driftwood"; expect = "needs its state"; break;
                case "a vane turned from its headings":
                    sp.IsAnimated = true; sp.AnimatedInput = StPetersLayerRefresh.WindFromInput; sp.AnimatedRigDir = 2;
                    expect = "rendered at dir 2";
                    break;
                case "a vane on another input": sp.IsAnimated = true; sp.AnimatedInput = "tideHeight"; expect = "turns with 'tideHeight'"; break;
                case "a piece placed twice": scenes.Add(Scene(Yard, KeySceneTestData.Copy(piece))); expect = "placed twice"; break;
                case "no Def in the kit": piece.Piece = "missing"; expect = "no SetPieceDef 'missing'"; break;
                case "a cell the kit lacks":
                    piece.Kit = DecorKit; piece.Piece = "trapStack";
                    assets.Missing.Add(FakeKeySceneAssets.IsoKey(DecorKit, "trapStack", FakeKeySceneAssets.FacingOf(0)));
                    expect = "has no cell";
                    break;
                case "a cell that is no sprite":
                    piece.Kit = DecorKit; piece.Piece = "trapStack";
                    assets.NotSprites.Add(FakeKeySceneAssets.IsoKey(DecorKit, "trapStack", FakeKeySceneAssets.FacingOf(0)));
                    expect = "not a sprite asset";
                    break;
                case "a tide board with no scale":
                    sp.TideMarks = FakeKeySceneAssets.TideBoard("piece", "prop.test_piece").TideMarks;
                    sp.PixelsPerMetre = 0f;
                    expect = "no scale";
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
            }
            var refusal = Assert.Throws<Refusal>(() => StPetersLayerRefresh.PlaceKeyScenes(scenes, assets),
                                                 $"{fault} was placed instead of refused");
            StringAssert.Contains(expect, refusal.Message);
            Assert.Throws<Refusal>(() => Plan(KeySceneSyntheticScene.Text(), scenes, assets), $"the step wrote {fault}");
        }

        [Test]
        public void Place_LeavesTheLightOut_AndSaysSo()
        {
            var (scenes, assets) = TwoScenes();
            KeyScenePlacement lantern = StPetersLayerRefresh.PlaceKeyScenes(scenes, assets).Single(p => p.Id == "prop.test_lantern");
            StringAssert.Contains("light", lantern.LeftOut, "a lit piece's light is left out by name");

            LayerPatch patch = Plan(KeySceneSyntheticScene.Text(), scenes, assets);
            Assert.IsTrue(patch.Changes.Any(c => c.Object == "prop.test_lantern" && c.What == "left out"),
                          "the patch does not say the light was left out");

            // Nothing it adds is a light: its only scripts are the sorter, the waterline and the vane.
            var scripts = new HashSet<string> { assets.Sorter.ClassIdentifier, assets.Waterline.ClassIdentifier, assets.Vane.ClassIdentifier };
            foreach (Op op in patch.Ops.Where(o => o.Kind == OpKind.Add))
            {
                var d = new StPetersLayerRefresh.Doc(op.After);
                Assert.IsTrue(KeySceneTestData.PropClasses.Contains(d.ClassId), $"{op.Name} is a class {d.ClassId} document");
                if (d.ClassId == KeySceneTestData.MonoBehaviourClass)
                    Assert.IsTrue(scripts.Contains(d.Field("m_EditorClassIdentifier")), $"{op.Name} runs {d.Field("m_EditorClassIdentifier")}");
            }
        }

        // =============================================================================================
        //  the step
        // =============================================================================================

        [Test]
        public void Step_AddsItsRoot_AndJoinsSceneRootsByOneNamedEdit()
        {
            var (scenes, assets) = TwoScenes();
            string before = KeySceneSyntheticScene.Text();
            LayerPatch patch = Plan(before, scenes, assets);

            Assert.AreEqual(StPetersLayerRefresh.KeyScenesRootName, patch.Root, "the root the patch is for");
            Assert.IsFalse(patch.Deletions.Any(), "a first run deletes nothing");
            List<Op> edits = patch.Ops.Where(o => o.Kind == OpKind.Edit).ToList();
            Assert.AreEqual(1, edits.Count, "a new root edits one document outside it");
            Assert.AreEqual(KeySceneSyntheticScene.SceneRoots, edits[0].FileId, "…the scene's SceneRoots list");
            StringAssert.Contains("SceneRoots", edits[0].Name, "…by name");

            var a = new LayerRefreshSceneText(before);
            var t = new LayerRefreshSceneText(patch.ApplyTo(before));
            long root = t.RootGameObject(StPetersLayerRefresh.KeyScenesRootName);
            Assert.AreEqual(patch.RootGameObject, root, "the root the patch names is the one it wrote");
            CollectionAssert.AreEqual(new[] { KeySceneSyntheticScene.OtherTransform, t.TransformOf(root) },
                                      t.Refs(KeySceneSyntheticScene.SceneRoots, "m_Roots"), "the root joins the end of the scene's roots");
            Assert.IsTrue(a.Text(KeySceneSyntheticScene.OtherGameObject) == t.Text(KeySceneSyntheticScene.OtherGameObject) &&
                          a.Text(KeySceneSyntheticScene.OtherTransform) == t.Text(KeySceneSyntheticScene.OtherTransform),
                          "somebody else's object changed");
            Assert.AreEqual(KeySceneSyntheticScene.SceneRoots, t.Order.Last(), "new documents go in just before SceneRoots");

            HashSet<long> mine = t.Subtree(root);
            foreach (Op op in patch.Ops.Where(o => o.Kind == OpKind.Add))
                Assert.IsTrue(mine.Contains(op.FileId), $"the added {op.Name} does not hang under the root");

            CollectionAssert.AreEqual(new[] { Quay, Yard }, t.ChildGameObjects(root).Select(g => t.Field(g, "m_Name")).ToList(),
                                      "one group per key scene, in order");
            CollectionAssert.AreEqual(TodaysPieces, t.ChildGameObjects(root).SelectMany(g => t.ChildGameObjects(g)).Select(g => t.Field(g, "m_Name")).ToList(),
                                      "each group holds its scene's pieces for today, in CD's order");
        }

        [Test]
        public void Step_WritesEachPieceWhole_AsTheKitDrawsIt()
        {
            var (scenes, assets) = TwoScenes();
            string text = Plan(KeySceneSyntheticScene.Text(), scenes, assets).ApplyTo(KeySceneSyntheticScene.Text());
            var t = new LayerRefreshSceneText(text);
            long root = t.RootGameObject(StPetersLayerRefresh.KeyScenesRootName);

            foreach (KeyScenePlacement p in StPetersLayerRefresh.PlaceKeyScenes(scenes, assets))
            {
                long go = t.ChildNamed(t.ChildNamed(root, p.Scene), p.Id);
                Vector3 at = t.Vec3(t.TransformOf(go), "m_LocalPosition");
                Assert.IsTrue(at.x == p.At.x && at.y == p.At.y && at.z == 0f, $"{p.Id} stands at {at}, not at its place");

                long sr = t.ComponentOfClass(go, KeySceneTestData.SpriteRendererClass);
                Assert.AreEqual(p.Sprite.Sprite.Yaml, t.Field(sr, "m_Sprite"), $"{p.Id}'s cell");
                Assert.AreEqual(p.Sprite.Size, t.Vec2(sr, "m_Size"), $"{p.Id}'s drawn size");
                Assert.AreEqual("0", t.Field(sr, "m_SortingLayerID"), $"{p.Id} sorts in the default layer");
                Assert.AreEqual(p.SortingOrder.ToString(CultureInfo.InvariantCulture), t.Field(sr, "m_SortingOrder"), $"{p.Id}'s order");
                Assert.AreEqual("0", t.Field(sr, "m_CastShadows"), $"{p.Id} casts no shadow");
                ObjRef material = p.Id == "prop.test_board" ? assets.FaceMaterial : assets.SpriteMaterial;
                Assert.AreEqual("  - " + material.Yaml, MaterialLine(t, sr), $"{p.Id}'s material");

                int expected = 2;
                if (p.Floor) Assert.IsFalse(HasScript(t, go, nameof(YSortSprite)), $"{p.Id} lies on the floor, and has a sorter");
                else
                {
                    long sorter = t.Script(go, nameof(YSortSprite));
                    Assert.AreEqual("0", t.Field(sorter, "_dynamic"), $"{p.Id} does not move, so it sorts once");
                    Assert.AreEqual(p.SortYOffset, Num(t.Field(sorter, "_pivotYOffset")), $"{p.Id}'s sort line");
                    expected++;
                }

                if (p.Id == "prop.test_board")
                {
                    List<Vector2[]> paths = PathsOf(t, t.ComponentOfClass(go, KeySceneTestData.PolygonCollider2DClass));
                    Assert.AreEqual(1, paths.Count, "the board's post is one wall");
                    // dir 4 turns the post's 0.4 × 0.42 m footprint half round: its first corner comes to (0.2, 0.21).
                    Assert.AreEqual(0.2f, paths[0][0].x, 1e-5f);
                    Assert.AreEqual(0.21f, paths[0][0].y, 1e-5f);
                    CollectionAssert.AreEqual(p.Walls[0], paths[0], "the wall as placed");

                    long face = t.Script(go, nameof(TidalFaceWaterline));
                    Assert.AreEqual(p.LipWorldY, Num(t.Field(face, "_lipWorldY")), "the board's lip row");
                    Assert.AreEqual(FakeKeySceneAssets.BoardFoot, Num(t.Field(face, "_lipElevation")), 1e-5f, "the board's lip height");
                    Assert.AreEqual("1", t.Field(face, "_configured"), "the waterline is configured as written");
                    expected += 2;
                }
                else Assert.IsFalse(t.Refs(go, "m_Component").Any(c => t.ClassOf(c) == KeySceneTestData.PolygonCollider2DClass),
                                    $"{p.Id} has no footprint in the kit, so no wall");

                if (p.Id == "prop.test_vane")
                {
                    long vane = t.Script(go, nameof(SetPieceWindVane));
                    Assert.AreEqual(assets.SetPieces["vane"].Def.Yaml, t.Field(vane, "_def"), "the vane knows its Def, to read its headings");
                    Assert.AreEqual(SetPieceWindVane.DefaultCalmMetresPerSecond, Num(t.Field(vane, "_calmMetresPerSecond")));
                    expected++;
                }
                Assert.AreEqual(expected, t.Refs(go, "m_Component").Count, $"{p.Id}'s components");
            }
        }

        [Test]
        public void Step_WrittenTwice_ChangesNothing_AndHasNothingLeftToDo()
        {
            var (scenes, assets) = TwoScenes();
            string before = KeySceneSyntheticScene.Text();
            LayerPatch patch = Plan(before, scenes, assets);
            string once = patch.ApplyTo(before);
            Assert.IsTrue(patch.ApplyTo(once) == once, "written a second time, the patch changed the scene again");
            LayerPatch again = Plan(once, scenes, assets);
            Assert.IsTrue(again.IsEmpty, "planned again on its own output, the step still has work to do:\n" + again.Summary());
        }

        [Test]
        public void Step_ChangedOnPurpose_StaysInItsRoot_NamesEveryDeletion_AndWritesOnce()
        {
            var (scenes, assets) = TwoScenes();
            string today = Plan(KeySceneSyntheticScene.Text(), scenes, assets).ApplyTo(KeySceneSyntheticScene.Text());
            Directory.CreateDirectory(Path.GetDirectoryName(CopyPath));
            File.WriteAllText(CopyPath, today, new UTF8Encoding(false));

            // The quay's traps dropped, its vane moved a quarter metre east, and a second lantern on the yard.
            KeySceneDef quay = scenes[0], yard = scenes[1];
            List<KeyScenePiece> quayPieces = quay.Pieces.Where(p => p.Id != "prop.test_traps").Select(KeySceneTestData.Copy).ToList();
            quayPieces.Single(p => p.Id == "prop.test_vane").At += new Vector2(0.25f, 0f);
            assets.Add("lantern2", FakeKeySceneAssets.SetPiece("lantern2", "prop.test_second_lantern"));
            KeyScenePiece second = Piece("prop.test_second_lantern", SetPiecesKit, "lantern2", 26f, 9f, 2);
            var changed = new List<KeySceneDef>
            {
                Scene(Quay, quayPieces.ToArray()),
                Scene(Yard, yard.Pieces.Select(KeySceneTestData.Copy).Append(second).ToArray()),
            };

            LayerRefreshPatchChecks.PatchHoldsToItsRoot(CopyPath, StPetersLayerRefresh.KeyScenesRootName, positioned: true,
                text => Plan(text, changed, assets));
        }

        [Test]
        public void Step_RefusesARootMadeByHand()
        {
            var (scenes, assets) = TwoScenes();
            var refusal = Assert.Throws<Refusal>(() => Plan(KeySceneSyntheticScene.WithHandMadeRoot(), scenes, assets),
                                                 "the step wrote into a root it did not make");
            StringAssert.Contains("made by hand", refusal.Message);
        }

        // =============================================================================================
        //  the harbour vane
        // =============================================================================================

        static IEnumerable<int> Headings => Enumerable.Range(0, 16);

        [TestCaseSource(nameof(Headings))]
        public void Vane_PointsIntoTheWind_OnEveryHeading(int heading)
        {
            var part = new SetPieceAnimatedPart { Input = StPetersLayerRefresh.WindFromInput, Steps = 16 };
            float from = heading * part.StepDeg;
            foreach (float off in new[] { 0f, part.StepDeg * 0.5f - 0.5f, -(part.StepDeg * 0.5f - 0.5f) })
            {
                // The sim's wind points DOWNWIND: a wind from bearing b blows toward b + 180°.
                float b = (from + off) * Mathf.Deg2Rad;
                Vector2 wind = -new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * 6f;
                Assert.AreEqual(heading, SetPieceWindVane.HeadingFor(wind, part, SetPieceWindVane.DefaultCalmMetresPerSecond, -1),
                                $"a wind from {from + off}° shows the wrong heading");
            }
        }

        [Test]
        public void Vane_HoldsItsHeading_InACalm_AtTheHudsCalm()
        {
            Assert.AreEqual(ApparentWindReadout.CalmSpeedMps, SetPieceWindVane.DefaultCalmMetresPerSecond,
                            "the vane's calm is the HUD's: a vane that turns while the HUD reads 'calm' shows a wind the player is told is not there");
            var part = new SetPieceAnimatedPart { Input = StPetersLayerRefresh.WindFromInput, Steps = 16 };
            float calm = SetPieceWindVane.DefaultCalmMetresPerSecond;
            Vector2 east = new Vector2(-1f, 0f);   // blowing west: a wind from the east, heading 4
            Assert.AreEqual(7, SetPieceWindVane.HeadingFor(east * (calm * 0.5f), part, calm, 7), "in a calm the vane holds what it showed");
            Assert.AreEqual(-1, SetPieceWindVane.HeadingFor(east * (calm * 0.5f), part, calm, -1), "…or keeps the scene's baked frame");
            Assert.AreEqual(4, SetPieceWindVane.HeadingFor(east * (calm * 2f), part, calm, 7), "out of the calm it points into the wind");
        }

        // =============================================================================================
        //  reading the written scene back
        // =============================================================================================

        static readonly Regex XyRx = new Regex(@"^\{x: ([^,]+), y: ([^}]+)\}$", RegexOptions.CultureInvariant);

        static float Num(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        static bool HasScript(LayerRefreshSceneText t, long go, string className) =>
            t.Refs(go, "m_Component").Any(c => t.ClassOf(c) == KeySceneTestData.MonoBehaviourClass &&
                                               (t.Field(c, "m_EditorClassIdentifier") ?? "").EndsWith("." + className, StringComparison.Ordinal));

        static string MaterialLine(LayerRefreshSceneText t, long sr)
        {
            string[] lines = t.Text(sr).Split('\n');
            int at = Array.IndexOf(lines, "  m_Materials:");
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "the renderer lists no materials");
            return lines[at + 1];
        }

        /// <summary>A PolygonCollider2D's paths, as the scene writes them.</summary>
        internal static List<Vector2[]> PathsOf(LayerRefreshSceneText t, long collider)
        {
            string[] lines = t.Text(collider).Split('\n');
            int at = Array.IndexOf(lines, "    m_Paths:");
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "the collider has no paths");
            var paths = new List<List<Vector2>>();
            for (int i = at + 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("    - - ", StringComparison.Ordinal)) paths.Add(new List<Vector2>());
                else if (!lines[i].StartsWith("      - ", StringComparison.Ordinal) || paths.Count == 0) break;
                Match m = XyRx.Match(lines[i].Substring(8));
                Assert.IsTrue(m.Success, $"not a point: '{lines[i]}'");
                paths[paths.Count - 1].Add(new Vector2(Num(m.Groups[1].Value), Num(m.Groups[2].Value)));
            }
            return paths.Select(p => p.ToArray()).ToList();
        }
    }

    /// <summary>
    /// <b>THE KEY SCENES' PATCH, ON A COPY OF THE COMMITTED SCENE.</b> Subject: the step planned with the
    /// committed key scenes and the kits' real art. The committed scene must BE the step's output: taken back
    /// to the scene before the step first ran (its root and its SceneRoots entry out), the step's patch adds
    /// the root, joins SceneRoots by one named edit, writes once, and gives back the committed text to the
    /// byte. Changed on purpose, it holds to its root and names every deletion (the shared checks). And it
    /// writes props only: no ground anywhere, no light, no board's words. The committed scene is never
    /// written: it is hashed before each test and after.
    /// </summary>
    public class StPetersKeyScenesPatchTests
    {
        static readonly string CopyPath =
            Path.Combine(Path.GetTempPath(), "hh-st-peters-key-scenes-tests", "StPeters.copy.unity");

        string _committedHash;
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _committedHash = LayerRefreshPatchChecks.Sha256(StPetersLayerRefreshTests.ScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(CopyPath));
            File.Copy(StPetersLayerRefreshTests.ScenePath, CopyPath, true);
        }

        [TearDown]
        public void TearDown()
        {
            KeySceneTestData.Destroy(_made);
            _made.Clear();
            if (File.Exists(CopyPath)) File.Delete(CopyPath);
        }

        void CommittedSceneUnchanged() =>
            Assert.AreEqual(_committedHash, LayerRefreshPatchChecks.Sha256(StPetersLayerRefreshTests.ScenePath),
                            "the committed scene changed; the tests write only to their copy");

        static LayerPatch Plan(string text, IReadOnlyList<KeySceneDef> scenes) =>
            StPetersLayerRefresh.KeyScenePieces(StPetersLayerRefresh.SceneYaml.Parse(text), scenes,
                                                new StPetersLayerRefresh.EditorKeySceneAssets());

        /// <summary>The scene as it stood before the step first ran: the step's root and everything under it
        /// out, and its entry in the SceneRoots list; every other byte as committed.</summary>
        internal static string WithoutTheRoot(string text)
        {
            var t = new LayerRefreshSceneText(text);
            long root = t.Order.FirstOrDefault(id => t.ClassOf(id) == 1 && !t.IsStripped(id) &&
                                                     t.Field(id, "m_Name") == StPetersLayerRefresh.KeyScenesRootName);
            if (root == 0) return text;
            string entry = $"\n  - {{fileID: {t.TransformOf(root).ToString(CultureInfo.InvariantCulture)}}}";
            HashSet<long> mine = t.Subtree(root);
            var sb = new StringBuilder(text.Substring(0, text.IndexOf("--- !u!", StringComparison.Ordinal)));
            foreach (long id in t.Order.Where(i => !mine.Contains(i)))
                sb.Append(t.ClassOf(id) == KeySceneTestData.SceneRootsClass ? t.Text(id).Replace(entry, "") : t.Text(id)).Append('\n');
            return sb.ToString();
        }

        [Test]
        public void KeyScenesPatch_AddsItsRoot_JoinsSceneRoots_AndIsTheCommittedScene()
        {
            List<KeySceneDef> scenes = KeySceneTestData.StPeters();
            string committed = File.ReadAllText(CopyPath);
            string before = WithoutTheRoot(committed);
            LayerPatch patch = Plan(before, scenes);

            Assert.IsFalse(patch.Deletions.Any(), "a first run deletes nothing");
            List<Op> edits = patch.Ops.Where(o => o.Kind == OpKind.Edit).ToList();
            Assert.AreEqual(1, edits.Count, "a new root edits one document outside it");
            StringAssert.Contains("SceneRoots", edits[0].Name, "…the scene's SceneRoots list, by name");

            string once = patch.ApplyTo(before);
            Assert.IsTrue(patch.ApplyTo(once) == once, "written a second time, the patch changed the scene again");
            LayerPatch again = Plan(once, scenes);
            Assert.IsTrue(again.IsEmpty, "planned again on its own output, the step still has work to do:\n" + again.Summary());
            Assert.IsTrue(once == committed,
                          "the committed StPeters.unity is not the key scenes step's output (" + FirstDifference(committed, once) +
                          "). Close the scene and run Hidden Harbours ▸ World ▸ St Peters Layer Refresh ▸ Apply the Key Scenes Patch to StPeters.unity.");
            CommittedSceneUnchanged();
        }

        [Test]
        public void KeyScenesPatch_ChangedOnPurpose_StaysInItsRoot_NamesEveryDeletion_AndWritesOnce()
        {
            List<KeySceneDef> today = KeySceneTestData.StPeters();
            string committed = File.ReadAllText(CopyPath);
            File.WriteAllText(CopyPath, Plan(committed, today).ApplyTo(committed), new UTF8Encoding(false));

            List<KeySceneDef> changed = ChangedOnPurpose(today);
            LayerRefreshPatchChecks.PatchHoldsToItsRoot(CopyPath, StPetersLayerRefresh.KeyScenesRootName, positioned: true,
                text => Plan(text, changed));
            CommittedSceneUnchanged();
        }

        /// <summary>Today's key scenes with the Landing's rope dropped, its bench moved a quarter metre east, and
        /// a second tote stack on the deck. Copies: the committed assets are never touched.</summary>
        List<KeySceneDef> ChangedOnPurpose(List<KeySceneDef> today)
        {
            var scenes = new List<KeySceneDef>();
            foreach (KeySceneDef ks in today)
            {
                List<KeyScenePiece> pieces = ks.Pieces.Select(KeySceneTestData.Copy).ToList();
                if (ks.Id == KeySceneTestData.LandingId)
                {
                    Assert.AreEqual(1, pieces.RemoveAll(p => p.Id == "prop.stp_landing_rope"), "the Landing places no rope to drop");
                    pieces.Single(p => p.Id == "prop.stp_strand_bench").At += new Vector2(0.25f, 0f);
                    KeyScenePiece totes = KeySceneTestData.Copy(pieces.Single(p => p.Id == "prop.stp_landing_totes"));
                    totes.Id = "prop.stp_test_second_totes";
                    totes.At = new Vector2(196.5f, 2.2f);
                    pieces.Add(totes);
                }
                KeySceneDef copy = KeySceneTestData.NewKeyScene(ks.Id, pieces.ToArray());
                _made.Add(copy);
                scenes.Add(copy);
            }
            return scenes;
        }

        [Test]
        public void KeyScenesPatch_WritesPropsOnly_NoGroundAnywhere()
        {
            List<KeySceneDef> scenes = KeySceneTestData.StPeters();
            LayerPatch patch = Plan(WithoutTheRoot(File.ReadAllText(CopyPath)), scenes);

            Assert.IsFalse(patch.Deletions.Any(), "the step deleted something");
            Assert.AreEqual(1, patch.Ops.Count(o => o.Kind == OpKind.Edit), "the step edits the SceneRoots list and nothing else");
            var scripts = new HashSet<string>(new[] { typeof(YSortSprite), typeof(TidalFaceWaterline), typeof(SetPieceWindVane) }
                                                  .Select(ty => ty.Assembly.GetName().Name + "::" + ty.FullName));
            foreach (Op op in patch.Ops.Where(o => o.Kind == OpKind.Add))
            {
                var d = new StPetersLayerRefresh.Doc(op.After);
                Assert.IsTrue(KeySceneTestData.PropClasses.Contains(d.ClassId),
                              $"{op.Name} is a class {d.ClassId} document; the step writes props, never ground");
                if (d.ClassId == KeySceneTestData.MonoBehaviourClass)
                    Assert.IsTrue(scripts.Contains(d.Field("m_EditorClassIdentifier")), $"{op.Name} runs {d.Field("m_EditorClassIdentifier")}");
            }

            // Every piece is one of the three prop kits', and none is ground paint.
            var kits = new[] { StPetersLayerRefresh.SetPiecesKit, StPetersLayerRefresh.DecorKit, StPetersLayerRefresh.FindsKit };
            foreach (KeyScenePiece p in scenes.SelectMany(s => s.Pieces))
            {
                CollectionAssert.Contains(kits, p.Kit, $"{p.Id} comes from '{p.Kit}'");
                Assert.IsFalse(p.Id.StartsWith("path.", StringComparison.Ordinal), $"{p.Id} is ground paint");
            }

            // The boards' words stay ids: no entry's text is written into the scene.
            string yaml = patch.ToYaml();
            foreach (WordsEntry e in KeySceneTestData.Words().Entries)
                Assert.IsFalse(yaml.IndexOf(e.Text, StringComparison.Ordinal) >= 0, $"'{e.Text}' ({e.Id}) is written into the scene");
            CommittedSceneUnchanged();
        }

        static string FirstDifference(string a, string b)
        {
            string[] x = a.Split('\n'), y = b.Split('\n');
            for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
                if (x[i] != y[i]) return $"line {i + 1}: committed '{x[i]}', the step's '{y[i]}'";
            return x.Length == y.Length ? "no line differs" : $"the committed scene has {x.Length} lines, the step's {y.Length}";
        }
    }

    /// <summary>
    /// <b>THE KEY SCENES' DATA HOLDS TO RETURN 2, AND TO THE GAME.</b> Subject: the committed
    /// <see cref="KeySceneDef"/>s and <see cref="WordsTableDef"/>. They hold return 2's today pieces as the art
    /// desk recorded them; every piece resolves to its kit; every height reads the game's own ground or deck
    /// (#890) within five centimetres; the tide board's marks read true against the game's sea; nothing stands
    /// in a frozen area (the arrival route, the berth pocket, the berths, the wharf's fittings and lamps, the
    /// deck's walking lane), all proved from the game's constants; the vane turns on sixteen headings; and the
    /// boards' words are ids in the owner's table, their text written nowhere else.
    /// </summary>
    public class StPetersKeyScenesContentTests
    {
        const string SeabedPath = "Assets/_Project/Data/Terrain/StPetersSeabed.asset";
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string TideBoardId = "prop.stp_slip_tide_board";

        /// <summary>How far a recorded height may sit from the game's before the game's is used and listed (handoff §2).</summary>
        const float HeightToleranceMetres = 0.05f;

        /// <summary>CD's frozen-rule distances (<c>checks/check-scenes.cjs</c>, deck_dressing): a deck piece keeps
        /// 0.6 m off every fitting and lamp, and out of the 2 m walking lane down the deck's centre-line.</summary>
        const float FittingClearanceMetres = 0.6f, LaneHalfWidthMetres = 1f;

        /// <summary>Props only within 12 m of the cannery (handoff §3): no ground changes inside its reach.</summary>
        const float CanneryPropsReachMetres = 12f;

        /// <summary>The tide board's site, the owner's ruling (handoff §6.2), on the berth pocket's shoulder.</summary>
        static readonly Vector2 RuledTideBoardSite = new Vector2(200.9f, -3.62f);

        /// <summary>CD's depth over the tide board's foot at a spring high: a boat's water (scene.json, tide).</summary>
        const float CdDepthAtSpringHigh = 3.95f;

        // ---- return 2's record ------------------------------------------------------------------------

        sealed class Row
        {
            public readonly string Scene, Id, Kit, Piece, State, StandsOn;
            public readonly Vector2 At;
            public readonly float Z;
            public readonly int Dir;
            public readonly float? SortY;
            public readonly string[] Words;

            public Row(string scene, string id, string kit, string piece, string state, float x, float y, float z, string standsOn,
                       int dir, float? sortY = null, params string[] words)
            {
                Scene = scene; Id = id; Kit = kit; Piece = piece; State = state; At = new Vector2(x, y); Z = z; StandsOn = standsOn;
                Dir = dir; SortY = sortY; Words = words;
            }
        }

        const string L = KeySceneTestData.LandingId, C = KeySceneTestData.CanneryId;
        const string Sp = StPetersLayerRefresh.SetPiecesKit, Dk = StPetersLayerRefresh.DecorKit, Fk = StPetersLayerRefresh.FindsKit;
        const string Ground = KeySceneDef.StandsOnGround, Deck = KeySceneDef.StandsOnDeck;

        /// <summary>Return 2's today pieces (<c>scenes/&lt;id&gt;/scene.json</c>, hashed in each scene's Source), in
        /// CD's order: the rest of each file (the bait store, the frozen lanterns and cannery, the restored
        /// pieces, the path paint, the gulls, the door notice) is left out on purpose.</summary>
        static readonly Row[] Record =
        {
            new Row(L, TideBoardId, Sp, "slipTideBoard", "", 200.9f, -3.62f, -1.75f, Ground, 4),
            new Row(L, "prop.stp_harbour_vane", Sp, "harbourVane", "", 183.6f, -4.4f, 5.96f, Ground, 0),
            new Row(L, "prop.stp_landing_traps", Dk, "trapStack", "", 187.3f, 2.05f, 5.35f, Deck, 4),
            new Row(L, "prop.stp_landing_totes", Dk, "toteStack", "", 190.1f, 2.2f, 5.35f, Deck, 4),
            new Row(L, "prop.stp_landing_cart", Dk, "dockCart", "", 193.2f, 1.5f, 5.35f, Deck, 4),
            new Row(L, "prop.stp_landing_bait_barrel", Dk, "baitBarrel", "", 205.9f, 2f, 5.35f, Deck, 4),
            new Row(L, "prop.stp_landing_rope", Dk, "ropeCoil", "", 206.5f, -2f, 5.35f, Deck, 4),
            new Row(L, "prop.stp_strand_bench", Dk, "bench", "", 186f, 17.2f, 6f, Ground, 4),
            new Row(L, "prop.stp_wrack_driftwood", Fk, "Driftwood", "bleached", 198.45f, 12.4f, 2.39f, Ground, 4),
            new Row(L, "prop.stp_wrack_plank", Fk, "DriftPlank", "dry", 197.1f, 16.4f, 2.41f, Ground, 3),
            new Row(L, "prop.stp_wrack_float", Fk, "NetFloat", "dry", 198.95f, 10.4f, 2.42f, Ground, 4),
            new Row(L, "prop.stp_wrack_rope", Fk, "RopeScrap", "dry", 196.2f, 18.6f, 2.41f, Ground, 4),
            new Row(L, "prop.stp_wrack_lath", Fk, "TrapLath", "bleached", 197.9f, 14.3f, 2.38f, Ground, 5),
            new Row(C, "structure.stp_cannery_boiler_stack", Sp, "boilerStack", "", 173.32f, 22.15f, 6f, Ground, 7, 16.4f),
            new Row(C, "structure.stp_cannery_trolley_line", Sp, "trolleyLine", "", 175.59f, 9.57f, 6f, Ground, 0, 12f),
            new Row(C, "prop.stp_cannery_trolley", Sp, "trolley", "", 178f, 6.9f, 6f, Ground, 7),
            new Row(C, "prop.stp_cannery_conveyor", Sp, "conveyor", "", 168.3f, 9.21f, 6f, Ground, 3),
            new Row(C, "prop.stp_cannery_fallen_sign", Sp, "fallenSign", "", 166.6f, 5.4f, 6f, Ground, 4, null, "words.cannery_sign"),
            new Row(C, "prop.stp_cannery_net_rack", Dk, "netFrame", "", 180.2f, 12.6f, 6f, Ground, 4),
            new Row(C, "prop.stp_cannery_broken_boxes", Dk, "crateStack", "", 172.6f, 8.4f, 6f, Ground, 4),
            new Row(C, "prop.stp_cannery_drum", Dk, "oilDrum", "", 163.9f, 9.6f, 6f, Ground, 4),
        };

        static IEnumerable<string> RecordedIds => Record.Select(r => r.Id);

        static List<KeyScenePiece> Pieces(List<KeySceneDef> scenes) => scenes.SelectMany(s => s.Pieces).ToList();

        static PaintedHeightField Seabed()
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(SeabedPath);
            Assert.IsNotNull(map, SeabedPath + " is missing: the heights are held to the AUTHORED ground, not a fake");
            Assert.IsNotNull(map.Field, "the painted seabed did not decode");
            return map.Field;
        }

        /// <summary>The painted ground as the wharf reads its deck off it.</summary>
        sealed class PaintedGround : ITidalTerrain
        {
            readonly PaintedHeightField _field;
            public PaintedGround(PaintedHeightField field) { _field = field; }
            public float ElevationAt(Vector2 worldPos) => _field.ElevationAt(worldPos);
        }

        static SetPieceDef DefOf(StPetersLayerRefresh.IKeySceneAssets assets, string rigKey)
        {
            Assert.IsTrue(assets.TrySetPiece(rigKey, out KeySceneSetPiece sp), $"the kit has no SetPieceDef '{rigKey}'");
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(AssetDatabase.GUIDToAssetPath(sp.Def.Guid));
            Assert.IsNotNull(def, $"'{rigKey}'s Def does not load");
            return def;
        }

        static string M(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        // ---- the record ---------------------------------------------------------------------------------

        [Test]
        public void TheKeyScenes_HoldExactlyTheRecordedPieces_InCdsOrder()
        {
            List<KeySceneDef> scenes = KeySceneTestData.StPeters();
            CollectionAssert.AreEqual(new[] { C, L }, scenes.Select(s => s.Id).ToList(), "St Peters' key scenes");
            foreach (KeySceneDef ks in scenes)
            {
                CollectionAssert.AreEqual(Record.Where(r => r.Scene == ks.Id).Select(r => r.Id).ToList(), ks.Pieces.Select(p => p.Id).ToList(),
                                          $"{ks.Id}'s pieces");
                StringAssert.Contains("return 2", ks.Source, $"{ks.Id} does not say where its placements came from");
                Assert.That(ks.Source, Does.Match(@"sha256 [0-9a-f]{64}$"), $"{ks.Id}'s source is not pinned by its hash");
                Assert.IsTrue(ks.Pieces.All(p => p.Variants.SequenceEqual(new[] { KeySceneDef.Today })), $"{ks.Id} holds a piece for a later variant");
            }
        }

        [TestCaseSource(nameof(RecordedIds))]
        public void EachPiece_IsReturnTwosRecord(string id)
        {
            Row r = Record.Single(x => x.Id == id);
            KeySceneDef ks = KeySceneTestData.StPeters().Single(s => s.Id == r.Scene);
            KeyScenePiece p = ks.Pieces.Single(x => x.Id == id);
            Assert.AreEqual(r.Kit, p.Kit, "kit");
            Assert.AreEqual(r.Piece, p.Piece, "piece");
            Assert.AreEqual(r.State, p.State, "state");
            Assert.AreEqual(r.At, p.At, "where it stands");
            Assert.AreEqual(r.Z, p.Z, "its height");
            Assert.AreEqual(r.StandsOn, p.StandsOn, "what it stands on");
            Assert.AreEqual(r.Dir, p.Dir, "its facing");
            Assert.AreEqual(r.SortY.HasValue, p.HasSortY, "whether CD gives it a sort line");
            if (r.SortY.HasValue) Assert.AreEqual(r.SortY.Value, p.SortY, "its sort line");
            CollectionAssert.AreEqual(r.Words, p.Words, "its words' ids");
        }

        [Test]
        public void EveryPlacedPiece_ResolvesToItsKit()
        {
            var assets = new StPetersLayerRefresh.EditorKeySceneAssets();
            List<KeyScenePlacement> placed = StPetersLayerRefresh.PlaceKeyScenes(KeySceneTestData.StPeters(), assets);
            CollectionAssert.AreEquivalent(RecordedIds.ToList(), placed.Select(p => p.Id).ToList(), "every recorded piece is placed");

            foreach (KeyScenePlacement p in placed)
            {
                string path = AssetDatabase.GUIDToAssetPath(p.Sprite.Sprite.Guid);
                Assert.IsNotEmpty(path, $"{p.Id}'s cell ({p.Cell}) is no asset");
                Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                    .FirstOrDefault(s => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string _, out long local) &&
                                         local == p.Sprite.Sprite.FileId);
                Assert.IsNotNull(sprite, $"{p.Id}'s cell ({p.Cell}) is no sprite in {path}");
                if (p.Kit != StPetersLayerRefresh.SetPiecesKit) continue;
                SetPieceDef def = DefOf(assets, p.Piece);
                Assert.AreEqual(p.Id, def.Id, $"{p.Id} is placed under another Def's id");
                Assert.IsTrue(def.Frames.Any(f => f.Sprite == sprite), $"{p.Id}'s cell is not one of its Def's frames");
            }
        }

        // ---- the heights ------------------------------------------------------------------------------

        [Test]
        public void EveryHeight_ReadsTheGamesGroundOrDeck_WithinFiveCentimetres()
        {
            PaintedHeightField ground = Seabed();
            float deck = StPetersWharf.DeckElevationFrom(new PaintedGround(ground));
            Rect planks = StPetersWharf.DeckFootprint();
            var table = new StringBuilder("piece | stands on | recorded z | game's z | difference\n");
            var off = new List<string>();
            foreach (KeyScenePiece p in Pieces(KeySceneTestData.StPeters()))
            {
                float game;
                if (p.StandsOn == KeySceneDef.StandsOnDeck)
                {
                    Assert.IsTrue(planks.Contains(p.At), $"{p.Id} reads the deck but stands off its planks");
                    game = deck;
                }
                else if (p.StandsOn == KeySceneDef.StandsOnGround)
                {
                    Assert.IsFalse(planks.Contains(p.At), $"{p.Id} reads the ground under the deck");
                    game = ground.ElevationAt(p.At);
                }
                else
                {
                    Assert.Fail($"{p.Id} stands on '{p.StandsOn}': a mounted piece reads its mount, and none is placed yet");
                    return;
                }
                table.Append($"{p.Id} | {p.StandsOn} | {M(p.Z)} | {M(game)} | {M(p.Z - game)}\n");
                if (Mathf.Abs(p.Z - game) > HeightToleranceMetres) off.Add($"{p.Id}: {M(p.Z)} recorded, {M(game)} in the game");
            }
            TestContext.WriteLine(table.ToString());
            Assert.IsEmpty(off, "heights more than 5 cm off the game's own (use the game's and list them)");
        }

        [Test]
        public void TheTideBoard_ReadsTrue_AtEveryMark_AndStandsInTheIntertidal()
        {
            List<KeySceneDef> scenes = KeySceneTestData.StPeters();
            var assets = new StPetersLayerRefresh.EditorKeySceneAssets();
            KeyScenePlacement board = StPetersLayerRefresh.PlaceKeyScenes(scenes, assets).Single(p => p.Id == TideBoardId);
            KeyScenePiece piece = Pieces(scenes).Single(p => p.Id == TideBoardId);
            SetPieceDef def = DefOf(assets, board.Piece);
            Assert.IsTrue(board.RidesTheTide, "the tide board is not cut by the sea");
            Assert.That(def.TideMarks.Length, Is.GreaterThan(1), "the board has no marks to read");

            int rigDir = SetPieceDef.RigDirForFrame(SetPieceDef.FrameForRigDir(piece.Dir));
            foreach (SetPieceTideMark m in def.TideMarks)
            {
                float row = board.At.y + def.ScreenOffsetPx(m.At, rigDir).y / def.PixelsPerMetre;
                Assert.AreEqual(row, TidalFaceWaterline.WaterlineWorldY(board.LipWorldY, board.LipElevation, m.Z),
                                0.5f / def.PixelsPerMetre, $"a sea at {m.Z} m misses the mark painted for it ('{m.Label}') by more than half a pixel");
            }

            // CD's tide band: intertidal, bared at a spring low and covered at a spring high, when it is a boat's water.
            float at = Seabed().ElevationAt(piece.At);
            RegionValidation.TideSwing springs = RegionValidation.SwingOf(StPetersBuilder.TideMean, StPetersBuilder.TideAmplitude);
            float springHigh = springs.High;
            Assert.That(at, Is.GreaterThan(springs.Low).And.LessThan(springHigh), "the board's foot is not in the intertidal");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            float depth = springHigh - at;
            Assert.AreEqual(DepthBand.Deep, TidalExposure.BandForDepth(depth, config.WadeDepth, config.SwimLimit),
                            $"at a spring high the board stands in {M(depth)} m of water, which CD reads as a boat's");
            Assert.AreEqual(CdDepthAtSpringHigh, depth, HeightToleranceMetres, "CD's depth at a spring high");
            TestContext.WriteLine($"tide board: ground {M(at)} m, marks painted up from {M(board.LipElevation)} m, " +
                                  $"{M(depth)} m of water at a spring high");
        }

        // ---- the frozen rules -------------------------------------------------------------------------

        static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static float ToLine(Vector2 p, IReadOnlyList<Vector2> line)
        {
            float d = float.MaxValue;
            for (int i = 1; i < line.Count; i++) d = Mathf.Min(d, ToSegment(p, line[i - 1], line[i]));
            return d;
        }

        static bool InBox(Vector2 p, Vector2 centre, float halfX, float halfY) =>
            Mathf.Abs(p.x - centre.x) <= halfX && Mathf.Abs(p.y - centre.y) <= halfY;

        [Test]
        public void NoPiece_StandsInAFrozenArea()
        {
            NavChannel route = StPetersNavMarks.Entrance;
            Vector2 hull = (Vector2)StPetersBuilder.DockZonePos, dory = (Vector2)StPetersBuilder.DoryMooredPos;
            Vector2 pocketFrom = StPetersBuilder.BerthPocketFrom, pocketTo = StPetersBuilder.BerthPocketTo;
            List<(string Name, Vector2 Position)> fixtures = StPetersWharf.Fittings().Select(f => (f.Name, f.Position))
                .Concat(StPetersWharf.LampPostSites().Select(s => ("lamp post", s.Position))).ToList();
            float lane = StPetersWharf.DeckFootprint().center.y;

            var table = new StringBuilder("piece | to the route | to the pocket | to the nearest fitting\n");
            var broken = new List<string>();
            foreach (KeyScenePiece p in Pieces(KeySceneTestData.StPeters()))
            {
                bool onDeck = p.StandsOn == KeySceneDef.StandsOnDeck;
                float toRoute = ToLine(p.At, route.Waypoints), pocket = ToSegment(p.At, pocketFrom, pocketTo);
                (string Name, Vector2 Position) nearest = fixtures.OrderBy(f => Vector2.Distance(f.Position, p.At)).First();
                float fitting = Vector2.Distance(nearest.Position, p.At);
                table.Append($"{p.Id} | {M(toRoute)}{(onDeck ? " (on the deck)" : "")} | {M(pocket)} | {M(fitting)} ({nearest.Name})\n");

                // The arrival route: a piece on the deck stands above it; everything else keeps out of its width.
                if (!onDeck && toRoute < route.HalfWidthMetres)
                    broken.Add($"{p.Id} is {M(toRoute)} m from the arrival route, inside its {M(route.HalfWidthMetres)} m");
                // The berths: the arrival hull's alongside the wharf, and the dory's.
                if (InBox(p.At, hull, StPetersBuilder.ArrivalHullLengthMetres * 0.5f, StPetersBuilder.ArrivalHullHalfBeamMetres))
                    broken.Add($"{p.Id} stands in the arrival hull's berth");
                if (InBox(p.At, dory, StPetersBuilder.DoryLengthMetres * 0.5f, StPetersBuilder.DoryHalfBeamMetres))
                    broken.Add($"{p.Id} stands in the dory's berth");
                // The berth pocket: nothing in its flat; on its shoulder only the tide board, where the owner ruled it.
                if (!onDeck && pocket < StPetersBuilder.BerthPocketThalwegHalfWidth)
                    broken.Add($"{p.Id} stands in the berth pocket's flat ({M(pocket)} m from its line)");
                else if (!onDeck && pocket < StPetersBuilder.BerthPocketHalfWidth && !(p.Id == TideBoardId && p.At == RuledTideBoardSite))
                    broken.Add($"{p.Id} stands on the berth pocket's shoulder ({M(pocket)} m from its line)");
                // The wharf's fittings and lamps, and the deck's walking lane.
                if (fitting < FittingClearanceMetres) broken.Add($"{p.Id} is {M(fitting)} m from the {nearest.Name}");
                if (onDeck && Mathf.Abs(p.At.y - lane) < LaneHalfWidthMetres) broken.Add($"{p.Id} stands in the deck's walking lane");
            }
            TestContext.WriteLine(table.ToString());
            Assert.IsEmpty(broken, "pieces in a frozen area");
        }

        [Test]
        public void TheCanneryScene_IsPropsOnly_AndStandsWithinTheCannerysReach()
        {
            float building = StPetersCannery.FootprintRadiusMetres;
            if (building <= 0f) building = StPetersCannery.UnbakedFootprintRadiusMetres;
            float reach = building + CanneryPropsReachMetres;
            var table = new StringBuilder($"piece | from the cannery (its reach {M(reach)} m)\n");
            var kits = new[] { StPetersLayerRefresh.SetPiecesKit, StPetersLayerRefresh.DecorKit, StPetersLayerRefresh.FindsKit };
            foreach (KeyScenePiece p in KeySceneTestData.StPeters().Single(s => s.Id == C).Pieces)
            {
                float d = Vector2.Distance(p.At, StPetersCannery.Site);
                table.Append($"{p.Id} | {M(d)}\n");
                Assert.That(d, Is.LessThanOrEqualTo(reach), $"{p.Id} stands outside the cannery's reach");
                CollectionAssert.Contains(kits, p.Kit, $"{p.Id} comes from '{p.Kit}': only the prop kits stand by the cannery");
                Assert.AreEqual(KeySceneDef.StandsOnGround, p.StandsOn, $"{p.Id} stands on the ground it finds, which the step never changes");
            }
            TestContext.WriteLine(table.ToString());
        }

        // ---- the words and the vane ---------------------------------------------------------------------

        [Test]
        public void WordsAreIds_InTheTable_AndTheirTextIsWrittenNowhereElse()
        {
            WordsTableDef words = KeySceneTestData.Words();
            Assert.IsNotNull(words, KeySceneTestData.WordsTablePath + " is missing");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WordsEntry e in words.Entries)
            {
                Assert.IsTrue(ids.Add(e.Id), $"two entries are '{e.Id}'");
                Assert.That(e.Id, Does.Match(@"^words\.[a-z0-9_]+$"), "a words id is words.snake_case");
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Text), $"{e.Id} has no text");
                if (e.BodyId.Length > 0) Assert.That(e.BodyId, Does.Match(@"^[a-z]+\.[a-z0-9_]+$"), $"{e.Id}'s body is not an id");
            }

            // The three the owner ruled (handoff §6.3), and the notice opens onto its body.
            foreach (string id in new[] { "words.bait_store_name", "words.cannery_sign", "words.cannery_notice" })
                Assert.IsTrue(words.TryGet(id, out _), $"the words table has no {id}");
            words.TryGet("words.cannery_notice", out WordsEntry notice);
            Assert.AreEqual("notice.cannery_for_sale", notice.BodyId, "the cannery notice's body");

            // Every id a placed piece shows resolves here…
            List<KeySceneDef> scenes = KeySceneTestData.StPeters();
            foreach (KeyScenePiece p in Pieces(scenes))
                foreach (string w in p.Words)
                    Assert.IsTrue(words.TryGet(w, out _), $"{p.Id} shows {w}, which the words table does not hold");

            // …and no text is written anywhere but the table: not in the key scenes' data, nor in the code that
            // places them.
            var files = new List<string>(Directory.GetFiles(StPetersLayerRefresh.KeySceneFolder, "*.asset"));
            foreach (Type ty in new[] { typeof(KeySceneDef), typeof(WordsTableDef), typeof(SetPieceWindVane) })
                files.Add(ScriptPath(ty));
            files.Add("Assets/_Project/Code/App/Editor/StPetersLayerRefresh.KeyScenePieces.cs");
            foreach (string path in files)
            {
                string text = File.ReadAllText(path);
                foreach (WordsEntry e in words.Entries)
                    Assert.IsFalse(text.IndexOf(e.Text, StringComparison.Ordinal) >= 0,
                                   $"'{e.Text}' ({e.Id}) is written into {path}; a board carries its id, and its text lives in the words table");
            }
        }

        static string ScriptPath(Type ty)
        {
            foreach (string g in AssetDatabase.FindAssets("t:MonoScript " + ty.Name))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                if (ms != null && ms.GetClass() == ty) return AssetDatabase.GUIDToAssetPath(g);
            }
            Assert.Fail($"no script for {ty.FullName}");
            return null;
        }

        [Test]
        public void EverySignsWords_AreItsDefsAnchors()
        {
            var assets = new StPetersLayerRefresh.EditorKeySceneAssets();
            var unlettered = new List<string>();
            foreach (KeyScenePiece p in Pieces(KeySceneTestData.StPeters()).Where(x => x.Kit == StPetersLayerRefresh.SetPiecesKit))
            {
                SetPieceDef def = DefOf(assets, p.Piece);
                var anchors = new HashSet<string>(def.Words.Select(w => w.Id), StringComparer.Ordinal);
                foreach (string w in p.Words)
                    Assert.IsTrue(anchors.Contains(w), $"{p.Id} shows {w}, which its Def has no board for");
                unlettered.AddRange(anchors.Where(a => !p.Words.Contains(a)).Select(a => $"{p.Id}: {a}"));
            }
            // Boards bake blank; lettering them is a follow-up. These the Def has a board for and no piece shows yet.
            TestContext.WriteLine("boards no piece letters yet:\n" + string.Join("\n", unlettered));
        }

        [Test]
        public void TheHarbourVane_TurnsOnSixteenHeadings_WithTheWind()
        {
            List<KeySceneDef> scenes = KeySceneTestData.StPeters();
            var assets = new StPetersLayerRefresh.EditorKeySceneAssets();
            KeyScenePlacement vane = StPetersLayerRefresh.PlaceKeyScenes(scenes, assets).Single(p => p.TurnsWithWind);
            Assert.AreEqual("prop.stp_harbour_vane", vane.Id, "the one piece that turns with the wind");
            SetPieceDef def = DefOf(assets, vane.Piece);
            Assert.AreEqual(AssetDatabase.GetAssetPath(def), AssetDatabase.GUIDToAssetPath(vane.Def.Guid), "the vane is written with its own Def");

            SetPieceAnimatedPart part = def.Animated;
            Assert.IsTrue(def.IsAnimated, "the vane's Def does not turn");
            Assert.AreEqual(StPetersLayerRefresh.WindFromInput, part.Input, "the vane turns with the wind's bearing");
            Assert.AreEqual(16, part.Steps, "the vane shows sixteen headings");
            Assert.AreEqual(part.Steps, part.Headings.Length, "a heading has no picture");
            Assert.IsTrue(part.Headings.All(h => h != null && h.Sprite != null), "a heading has no sprite");
            Assert.AreEqual(part.Steps, part.Headings.Select(h => h.Sprite).Distinct().Count(), "two headings show one picture");

            KeyScenePiece piece = Pieces(scenes).Single(p => p.Id == vane.Id);
            Assert.AreEqual(part.RigDir, SetPieceDef.RigDirForFrame(SetPieceDef.FrameForRigDir(piece.Dir)),
                            "the vane is placed at the facing its headings were rendered at");
            for (int i = 0; i < part.Steps; i++)
            {
                float b = i * part.StepDeg * Mathf.Deg2Rad;
                Vector2 wind = -new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * 6f;
                Assert.AreEqual(i, SetPieceWindVane.HeadingFor(wind, part, SetPieceWindVane.DefaultCalmMetresPerSecond, -1),
                                $"a wind from {i * part.StepDeg}° does not show heading {i}");
            }
        }
    }

    // =================================================================================================
    //  the test kit
    // =================================================================================================

    /// <summary>The key scenes' test data: the committed assets, and key scenes made for a test. Each is the
    /// one place a test reaches the engine for it.</summary>
    internal static class KeySceneTestData
    {
        internal const string LandingId = "keyscene.stp_landing", CanneryId = "keyscene.stp_cannery";
        internal const string WordsTablePath = "Assets/_Project/Data/Words/WordsTable_StPeters.asset";

        internal const int GameObjectClass = 1, TransformClass = 4, MonoBehaviourClass = 114, SpriteRendererClass = 212,
                           PolygonCollider2DClass = 60, SceneRootsClass = 1660057539;

        /// <summary>Everything a prop is made of. No tilemap, no terrain, no light.</summary>
        internal static readonly HashSet<int> PropClasses = new HashSet<int>
        {
            GameObjectClass, TransformClass, SpriteRendererClass, MonoBehaviourClass, PolygonCollider2DClass,
        };

        /// <summary>St Peters' committed key scenes, as the step loads them.</summary>
        internal static List<KeySceneDef> StPeters() => StPetersLayerRefresh.LoadStPetersKeyScenes();

        /// <summary>The words table the boards' ids resolve in.</summary>
        internal static WordsTableDef Words() => AssetDatabase.LoadAssetAtPath<WordsTableDef>(WordsTablePath);

        /// <summary>A key scene made for a test. The caller destroys it.</summary>
        internal static KeySceneDef NewKeyScene(string id, params KeyScenePiece[] pieces)
        {
            var ks = ScriptableObject.CreateInstance<KeySceneDef>();
            ks.Id = id;
            ks.RegionId = StPetersLayerRefresh.StPetersRegionId;
            ks.Pieces = pieces;
            return ks;
        }

        internal static void Destroy(IEnumerable<Object> made)
        {
            foreach (Object o in made)
                if (o != null) Object.DestroyImmediate(o);
        }

        internal static KeyScenePiece Piece(string id, string kit, string piece, float x, float y, int dir, string state = "") =>
            new KeyScenePiece { Id = id, Kit = kit, Piece = piece, State = state, At = new Vector2(x, y), Dir = dir };

        /// <summary>A piece copied field by field, so a test can change it without touching an asset.</summary>
        internal static KeyScenePiece Copy(KeyScenePiece p) => new KeyScenePiece
        {
            Id = p.Id, Kit = p.Kit, Piece = p.Piece, State = p.State, At = p.At, Z = p.Z, StandsOn = p.StandsOn, Dir = p.Dir,
            HasSortY = p.HasSortY, SortY = p.SortY, Variants = (string[])p.Variants.Clone(), Words = (string[])p.Words.Clone(),
            Options = p.Options.Select(o => new KeySceneOption { Key = o.Key, Value = o.Value }).ToArray(),
        };

        static ulong Fnv(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>A made-up asset's local id and guid, stable for its key.</summary>
        internal static long FileIdOf(string key) => (long)(Fnv(key) >> 2) + 1;

        internal static string GuidOf(string key) =>
            Fnv("guid:" + key).ToString("x16", CultureInfo.InvariantCulture) + Fnv("more:" + key).ToString("x16", CultureInfo.InvariantCulture);
    }

    /// <summary>Made-up art for the key scenes step: every cell is a sprite named by its key, so a test can say
    /// which cell a piece must show without the AssetDatabase.</summary>
    internal sealed class FakeKeySceneAssets : StPetersLayerRefresh.IKeySceneAssets
    {
        /// <summary>The height the test tide board's marks are painted up from.</summary>
        internal const float BoardFoot = -1.5f;

        internal readonly Dictionary<string, KeySceneSetPiece> SetPieces = new Dictionary<string, KeySceneSetPiece>(StringComparer.Ordinal);

        /// <summary>Cells the kit lacks, and cells that are no sprite asset.</summary>
        internal readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> NotSprites = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The test kits turn a dir to a cell by a rule of their own, deliberately not the dir itself,
        /// so a step that skipped the kit's convention would show the wrong cell.</summary>
        internal static int FacingOf(int dir) => (((dir + 5) % 8) + 8) % 8;

        internal KeySceneSetPiece Add(string rigKey, KeySceneSetPiece piece)
        {
            SetPieces[rigKey] = piece;
            return piece;
        }

        public int IsoFacing(string kit, int dir) => FacingOf(dir);

        public bool TryIsoCell(string kit, string piece, int facing, out SpriteRef sprite) => Cell(IsoKey(kit, piece, facing), out sprite);

        public bool TryFind(string find, string state, int lie, int variant, out SpriteRef sprite) =>
            Cell(FindKey(find, state, lie, variant), out sprite);

        public bool TrySetPiece(string rigKey, out KeySceneSetPiece piece) => SetPieces.TryGetValue(rigKey, out piece);

        public ObjRef SpriteMaterial => new ObjRef(2100000, KeySceneTestData.GuidOf("material/sprite"), 2);
        public ObjRef FaceMaterial => new ObjRef(2100000, KeySceneTestData.GuidOf("material/face"), 2);
        public ScriptRef Sorter => Script(nameof(YSortSprite));
        public ScriptRef Waterline => Script(nameof(TidalFaceWaterline));
        public ScriptRef Vane => Script(nameof(SetPieceWindVane));

        internal static string IsoKey(string kit, string piece, int facing) =>
            $"{kit}/{piece}/{facing.ToString(CultureInfo.InvariantCulture)}";

        internal static string FindKey(string find, string state, int lie, int variant) =>
            $"finds/{find}_{state}/{lie.ToString(CultureInfo.InvariantCulture)}/{variant.ToString(CultureInfo.InvariantCulture)}";

        bool Cell(string key, out SpriteRef sprite)
        {
            sprite = NotSprites.Contains(key) ? new SpriteRef(new ObjRef(0, null, 0), Vector2.one) : SpriteOf(key);
            return !Missing.Contains(key);
        }

        internal static SpriteRef SpriteOf(string key) =>
            new SpriteRef(new ObjRef(KeySceneTestData.FileIdOf(key), KeySceneTestData.GuidOf(key), 3), new Vector2(1.5f, 2.25f));

        static ScriptRef Script(string className) =>
            new ScriptRef(new ObjRef(11500000, KeySceneTestData.GuidOf("script/" + className), 3),
                          "HiddenHarbours.Art::HiddenHarbours.Art." + className);

        /// <summary>A set piece as the kit bakes one: eight frames, standing on the ground.</summary>
        internal static KeySceneSetPiece SetPiece(string rigKey, string id, string layer = StPetersLayerRefresh.RaisedLayer) =>
            new KeySceneSetPiece
            {
                Id = id, Layer = layer, Mount = StPetersLayerRefresh.GroundMount,
                Def = new ObjRef(11400000, KeySceneTestData.GuidOf("def/" + rigKey), 2),
                Frames = Enumerable.Range(0, SetPieceDef.Facings)
                                   .Select(f => SpriteOf($"{rigKey}/frame{f.ToString(CultureInfo.InvariantCulture)}")).ToArray(),
                ElevationDeg = 40f, PixelsPerMetre = 32f,
            };

        /// <summary>A tide board: three marks a metre apart, painted up its post from a foot at
        /// <see cref="BoardFoot"/> on a face 0.2 m in front of its ground point, and a 0.4 × 0.42 m post that
        /// blocks the walk.</summary>
        internal static KeySceneSetPiece TideBoard(string rigKey, string id)
        {
            KeySceneSetPiece sp = SetPiece(rigKey, id);
            sp.TideMarks = new[] { Mark(-1.2f, 0f), Mark(-0.2f, 0.21f), Mark(0.8f, 0f) };
            sp.Colliders = new[] { Wall(0.2f, 0.21f) };
            return sp;
        }

        /// <summary>A wind vane: its headings rendered at dir 0.</summary>
        internal static KeySceneSetPiece WindVane(string rigKey, string id)
        {
            KeySceneSetPiece sp = SetPiece(rigKey, id);
            sp.IsAnimated = true;
            sp.AnimatedInput = StPetersLayerRefresh.WindFromInput;
            sp.AnimatedRigDir = 0;
            return sp;
        }

        static SetPieceTideMark Mark(float z, float x) => new SetPieceTideMark { Z = z, At = new Vector3(x, 0.2f, z - BoardFoot) };

        internal static SetPieceCollider Wall(float halfX, float halfY) => new SetPieceCollider
        {
            Type = StPetersLayerRefresh.PolygonShape, Blocks = StPetersLayerRefresh.BlocksWalk,
            Points = new[] { new Vector2(-halfX, -halfY), new Vector2(halfX, -halfY), new Vector2(halfX, halfY), new Vector2(-halfX, halfY) },
        };
    }

    /// <summary>A scene of its own for the step: one object of somebody else's and the SceneRoots list, as
    /// Unity writes them.</summary>
    internal static class KeySceneSyntheticScene
    {
        internal const long OtherGameObject = 100, OtherTransform = 101, SceneRoots = 9223372036854775807;

        internal static string Text() => Join(Other(), Roots(OtherTransform));

        /// <summary>…with a root under the step's name that the step did not write.</summary>
        internal static string WithHandMadeRoot() =>
            Join(Other(), GameObject(500, StPetersLayerRefresh.KeyScenesRootName, 501), Transform(501, 500, 0f, 0f),
                 Roots(OtherTransform, 501));

        static string Other() => GameObject(OtherGameObject, "Other", OtherTransform) + "\n" + Transform(OtherTransform, OtherGameObject, 3f, 4f);

        static string I(long v) => v.ToString(CultureInfo.InvariantCulture);
        static string I(float v) => v.ToString(CultureInfo.InvariantCulture);

        static string GameObject(long id, string name, long transform) => string.Join("\n",
            $"--- !u!1 &{I(id)}", "GameObject:", "  m_ObjectHideFlags: 0", "  serializedVersion: 6", "  m_Component:",
            $"  - component: {{fileID: {I(transform)}}}", "  m_Layer: 0", "  m_Name: " + name, "  m_IsActive: 1");

        static string Transform(long id, long go, float x, float y) => string.Join("\n",
            $"--- !u!4 &{I(id)}", "Transform:", "  m_ObjectHideFlags: 0", $"  m_GameObject: {{fileID: {I(go)}}}", "  serializedVersion: 2",
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", $"  m_LocalPosition: {{x: {I(x)}, y: {I(y)}, z: 0}}",
            "  m_LocalScale: {x: 1, y: 1, z: 1}", "  m_Children: []", "  m_Father: {fileID: 0}");

        static string Roots(params long[] transforms) => string.Join("\n",
            new[] { $"--- !u!1660057539 &{I(SceneRoots)}", "SceneRoots:", "  m_ObjectHideFlags: 0", "  m_Roots:" }
                .Concat(transforms.Select(t => $"  - {{fileID: {I(t)}}}")));

        static string Join(params string[] docs) => "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + string.Join("\n", docs) + "\n";
    }
}
