using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>No contact shade off the shore.</b> St Peters' <c>Shoreline/ShoreContact</c> tilemap holds one
    /// <c>Contact_n</c> tile per rock, on the ground cell north of it (⌊x⌋, ⌊y⌋ + 1), which is how
    /// <see cref="StPetersShorePainter"/> seats a rock into the ground. A tile that is not under a rock, or one
    /// left on a cell the island no longer has ground at, is a dark smudge on open water or on bare grass.
    ///
    /// <para>The island the scene draws is the terrain plan's (the seabed map at
    /// <see cref="StPetersTerrainPlan.SeabedPath"/>), and <see cref="StPetersLayerRefresh.RunOnThePlanGround"/>
    /// places the rocks on it. So these checks read the committed scene FILE and hold it to that ground:</para>
    /// <list type="number">
    /// <item>every contact tile seats a committed rock, one per rock, and none seats nothing;</item>
    /// <item>every contact tile's cell is painted shore on the plan's ground (a material, not open water);</item>
    /// <item>the refresh's Shoreline step over the plan's ground has nothing to move, so the committed
    /// rocks and their contacts are the painter's own answer for today's coast. A seabed rewrite that moves
    /// the coast without a refresh fails here, not on a player's screen.</item>
    /// </list>
    /// <para>The scene is read, never opened or written.</para>
    /// </summary>
    public class ShoreContactOnTheShoreTests
    {
        const int TilemapClass = 1839735485;

        string _text;
        PaintedHeightMap _seabed;

        [SetUp]
        public void SetUp()
        {
            _text = File.ReadAllText(StPetersLayerRefreshTests.ScenePath);
            _seabed = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(StPetersTerrainPlan.SeabedPath);
            Assert.NotNull(_seabed, StPetersTerrainPlan.SeabedPath);
            _seabed.Rebuild();
        }

        static Vector3Int SeatOf(Vector2 rock) => new Vector3Int(Mathf.FloorToInt(rock.x), Mathf.FloorToInt(rock.y) + 1, 0);

        static List<Vector3Int> ContactCells(string text)
        {
            var s = new LayerRefreshSceneText(text);
            long map = s.ComponentOfClass(s.ChildNamed(s.RootGameObject(StPetersShorePainter.RootName),
                                                       StPetersShorePainter.ContactLayerName), TilemapClass);
            return s.TileCells(map);
        }

        [Test]
        public void EveryContactTile_SeatsACommittedRock_OnePerRock()
        {
            List<Vector3Int> cells = ContactCells(_text);
            List<StPetersLayerRefresh.PlacedRock> rocks =
                StPetersLayerRefresh.RocksInScene(StPetersLayerRefresh.SceneYaml.Parse(_text));
            Assert.That(rocks.Count, Is.GreaterThan(0), "the committed scene has no rocks, so the check would be empty");

            var seats = new HashSet<Vector3Int>(rocks.Select(r => SeatOf(r.Position)));
            List<Vector3Int> orphans = cells.Where(c => !seats.Contains(c)).ToList();
            Assert.IsEmpty(orphans, "ShoreContact tiles with no rock to seat: " +
                                    string.Join(" ", orphans.Select(c => $"({c.x}, {c.y})")));
            Assert.AreEqual(cells.Count, cells.Distinct().Count(), "one tile per cell");
            CollectionAssert.AreEquivalent(seats, cells, "every rock is seated by exactly its own contact tile");
        }

        [Test]
        public void EveryContactTile_IsOnPaintedShore_OverThePlansGround()
        {
            var ground = new StPetersLayerRefresh.PlanGround(_seabed.Field);
            var off = new List<string>();
            foreach (Vector3Int c in ContactCells(_text))
            {
                var centre = new Vector2(c.x + 0.5f, c.y + 0.5f);
                if (StPetersShoreMap.MaterialAt(ground, centre) == ShoreMaterial.None)
                    off.Add($"({c.x}, {c.y}) at {ground.ElevationAt(centre):0.00} m");
            }
            Assert.IsEmpty(off, "ShoreContact tiles off the island's painted shore: " + string.Join(", ", off));
        }

        [Test]
        public void TheShorelineRefresh_OverThePlansGround_HasNothingToMove()
        {
            var ground = new StPetersLayerRefresh.PlanGround(_seabed.Field);
            var scene = StPetersLayerRefresh.SceneYaml.Parse(_text);
            StPetersLayerRefresh.LayerPatch patch = StPetersLayerRefresh.Shoreline(
                scene, ground, new StPetersLayerRefresh.EditorShoreRockSprites(),
                StPetersLayerRefresh.LoadStPetersShoreRockDefs());
            string after = patch.ApplyTo(_text);

            CollectionAssert.AreEquivalent(ContactCells(_text), ContactCells(after),
                "the refresh over the plan's ground would move contact tiles - the committed shore is stale:\n" +
                patch.Summary());
            List<Vector2> had = StPetersLayerRefresh.RocksInScene(scene).Select(r => r.Position).ToList();
            List<Vector2> want = StPetersLayerRefresh.RocksInScene(StPetersLayerRefresh.SceneYaml.Parse(after))
                                                     .Select(r => r.Position).ToList();
            CollectionAssert.AreEqual(want, had,
                "the refresh over the plan's ground would move rocks - the committed shore is stale:\n" + patch.Summary());
        }
    }
}
