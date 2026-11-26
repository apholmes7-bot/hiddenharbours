using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Where the broken dory lies, checked against the island's own terrain and tide with no scene loaded:
    /// she is in the committed StPeters scene once, on the slip, dry at the highest water of the first
    /// week, and a walk from Ginny's door reaches her at that water. The PlayMode sibling checks the same
    /// in the loaded scene, colliders included.
    /// </summary>
    public class SlipDoryPlacementTests
    {
        const string ScenePath  = "Assets/_Project/Scenes/StPeters.unity";
        const string PrefabPath = "Assets/_Project/Prefabs/Boats/SlipDory.prefab";
        const string RegionPath = "Assets/_Project/Data/Regions/StPeters.asset";
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        /// <summary>The first week, scanned. A day past it covers a game begun late in the day.</summary>
        const int WeekDays = 8;
        /// <summary>Sample step for the scan, in game seconds: well under a minute of the clock.</summary>
        const double ScanStepSeconds = 5.0;
        /// <summary>Flood-fill cell, metres: finer than the dory's beam, so a gap she fills is not missed.</summary>
        const float Cell = 1f;

        GameObject _go;
        TidalTerrain _terrain;
        GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            StandableSurfaces.Clear();
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(_config, ConfigPath + " is missing");
            _go = new GameObject("StPetersTerrain_SlipDoryTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            StandableSurfaces.Clear();
            GameServices.Reset();
        }

        /// <summary>The highest water St Peters sees in the first week, from its own tide and the clock.</summary>
        public static float FirstWeekHighWater(GameConfig config, RegionDef region)
        {
            var profile = new TideProfile
            {
                MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours,
            };
            float high = float.MinValue;
            for (double t = 0; t <= config.SecondsPerDay * WeekDays; t += ScanStepSeconds)
                high = Mathf.Max(high, TideModel.Height(t, profile, config));
            return high;
        }

        /// <summary>The slip dory's position as the committed scene writes it.</summary>
        static Vector2 PlacedInScene()
        {
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Assert.IsFalse(string.IsNullOrEmpty(guid), PrefabPath + " is missing");
            string scene = File.ReadAllText(ScenePath).Replace("\r\n", "\n");
            var source = new Regex(@"  m_SourcePrefab: \{fileID: 100100000, guid: " + guid + @", type: 3\}");
            Assert.AreEqual(1, source.Matches(scene).Count, "the St Peters scene must hold the slip dory exactly once");

            float Axis(string axis)
            {
                var m = new Regex(@"- target: \{fileID: \d+, guid: " + guid + @", type: 3\}\n      propertyPath: m_LocalPosition\." + axis + @"\n      value: (\S+)").Match(scene);
                Assert.IsTrue(m.Success, "the slip dory's " + axis + " position is not in the scene");
                return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            return new Vector2(Axis("x"), Axis("y"));
        }

        /// <summary>The ground the hull covers where she lies: her collider's corners, edge middles and centre.</summary>
        static IEnumerable<Vector2> Footprint(Vector2 at)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var box = prefab.GetComponentInChildren<BoxCollider2D>(true);
            Assert.IsNotNull(box, "the slip dory has no collider to measure her footprint by");
            Vector2 half = box.size * 0.5f, mid = at + box.offset;
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
                yield return mid + new Vector2(i * half.x, j * half.y);
        }

        [Test]
        public void SheLiesOnTheSlip()
        {
            Vector2 at = PlacedInScene();
            Vector2 head = StPetersBuilder.BerthTo;   // the slip's shoreline end
            Assert.That(Mathf.Abs(at.y - head.y), Is.LessThanOrEqualTo(StPetersBuilder.BerthHalfWidth),
                $"she lies at {at}, outside the slip's width");
            Assert.That(at.x, Is.LessThanOrEqualTo(head.x), $"she lies at {at}, seaward of the slip's head");

            // Landward of the head the cut ramps up to the plateau; she must lie on that ramp, where the
            // slip's ground is still lower than the bank beside it.
            float bank = _terrain.ElevationAt(new Vector2(at.x, head.y + StPetersBuilder.BerthHalfWidth * 1.5f));
            Assert.Less(_terrain.ElevationAt(new Vector2(at.x, head.y)), bank,
                $"she lies at {at}, landward of where the slip's cut ends");
        }

        [Test]
        public void SheIsDry_AtTheHighestWaterOfTheFirstWeek()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(RegionPath);
            Assert.IsNotNull(region, RegionPath + " is missing");
            float high = FirstWeekHighWater(_config, region);
            Vector2 at = PlacedInScene();
            foreach (Vector2 p in Footprint(at))
            {
                float ground = _terrain.ElevationAt(p);
                Assert.Greater(ground, high,
                    $"the ground under her at {p} is {ground:F2} m and the first week's high water is {high:F2} m: she would float");
            }
        }

        [Test]
        public void AWalkFromGinnysDoor_ReachesHer_AtTheHighestWater()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(RegionPath);
            float high = FirstWeekHighWater(_config, region);
            Vector2 at = PlacedInScene();
            var sale = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<SlipDory>().Sale;

            HashSet<Vector2Int> reached = Fill(StPetersGinnyPlot.Dooryard, high, at, sale.ReachMeters + 1f);
            bool close = false;
            int r = Mathf.CeilToInt(sale.ReachMeters / Cell);
            var k = Key(at);
            for (int dx = -r; dx <= r && !close; dx++)
            for (int dy = -r; dy <= r && !close; dy++)
            {
                var c = new Vector2Int(k.x + dx, k.y + dy);
                close = reached.Contains(c) && Vector2.Distance(World(c), at) <= sale.ReachMeters;
            }
            Assert.IsTrue(close, $"no walkable ground within {sale.ReachMeters} m of her at {at} can be reached " +
                                 $"from Ginny's door at {StPetersGinnyPlot.Dooryard} with the water at {high:F2} m");
        }

        static Vector2Int Key(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x / Cell), Mathf.RoundToInt(p.y / Cell));
        static Vector2 World(Vector2Int k) => new Vector2(k.x * Cell, k.y * Cell);

        /// <summary>A flood fill over ground a person can stand on at this water, from a start point, stopping
        /// once it is within <paramref name="near"/> of the goal. Bounded to the island and its apron.</summary>
        HashSet<Vector2Int> Fill(Vector2 from, float water, Vector2 goal, float near)
        {
            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            Vector2Int start = Key(from);
            Assert.IsTrue(TidalExposure.IsWalkable(water, _terrain.ElevationAt(World(start)), _config.WadeDepth),
                "Ginny's door is not on walkable ground: the fill has nowhere to start");
            seen.Add(start); queue.Enqueue(start);
            float bound = StPetersBuilder.IslandRadius + StPetersBuilder.IslandFalloff + StPetersBuilder.ReefShelfWidth;
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                if (Vector2.Distance(World(c), goal) <= near) continue;   // close enough; no need to go past her
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (seen.Contains(n)) continue;
                    Vector2 p = World(n);
                    if (TidalTerrain.IslandDistance(p, StPetersBuilder.IslandCenter, StPetersBuilder.IslandRadius,
                            StPetersBuilder.IslandRadiusY) > bound) continue;
                    if (!TidalExposure.IsWalkable(water, _terrain.ElevationAt(p), _config.WadeDepth)) continue;
                    seen.Add(n); queue.Enqueue(n);
                }
            }
            return seen;
        }
    }
}
