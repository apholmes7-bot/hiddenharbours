using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE GROUND HE WALKS ON IS THE GROUND THE GAME DRAWS.</b> A region whose water is drawn from a painted
    /// height map (<see cref="WaterSurface.DepthSource.PaintedHeightMap"/>) must also walk, swim and sail on that
    /// map: its one enabled terrain is a <see cref="PaintedTidalTerrain"/> reading a <see cref="PaintedHeightMap"/>
    /// over the SAME texture, and no analytic terrain is left enabled to win the
    /// <c>GameServices.TidalTerrain</c> slot. St Peters drew its water and ground from its seabed raster while
    /// the walk gate read an analytic ellipse, so the drawn shore and the walked shore sat about 25 m apart at
    /// the north beach, and he swam on drawn sand at the old Weather Cliff stretch.
    ///
    /// <para>The guard reads every committed scene as text (no scene is opened or written), so it runs in a
    /// second and catches a new region the day it is committed.</para>
    /// </summary>
    public sealed class RegionWalksOnDrawnGroundTests
    {
        const string ScenesDir = "Assets/_Project/Scenes";
        const string StPetersScene = ScenesDir + "/StPeters.unity";
        const string StPetersSeabed = "Assets/_Project/Data/Terrain/StPetersSeabed.asset";

        sealed class Block
        {
            public int ClassId; public string FileId; public string Body;
            public string Field(string name)
            {
                var m = Regex.Match(Body, "\\n  " + name + ": (.*)");
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }
            public string Guid(string name)
            {
                var v = Field(name);
                if (v == null) return null;
                var m = Regex.Match(v, "guid: ([0-9a-f]{32})");
                return m.Success ? m.Groups[1].Value : null;
            }
            public string Ref(string name)
            {
                var v = Field(name);
                if (v == null) return null;
                var m = Regex.Match(v, "fileID: (-?\\d+)");
                return m.Success ? m.Groups[1].Value : null;
            }
        }

        static List<Block> Parse(string path)
        {
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            var blocks = new List<Block>();
            foreach (Match m in Regex.Matches(text, "--- !u!(\\d+) &(-?\\d+)[^\\n]*\\n(.*?)(?=\\n--- !u!|\\z)", RegexOptions.Singleline))
                blocks.Add(new Block { ClassId = int.Parse(m.Groups[1].Value), FileId = m.Groups[2].Value, Body = "\n" + m.Groups[3].Value });
            return blocks;
        }

        static string ScriptGuid(System.Type type)
        {
            foreach (var g in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                if (script != null && script.GetClass() == type) return g;
            }
            Assert.Fail("no MonoScript found for " + type.FullName);
            return null;
        }

        sealed class SceneTerrains
        {
            public List<string> PaintedWaterTextures = new List<string>();
            public List<string> PaintedTerrainMaps = new List<string>();
            public List<string> LiveAnalytic = new List<string>();
        }

        static SceneTerrains Read(string scenePath)
        {
            string water = ScriptGuid(typeof(WaterSurface));
            string painted = ScriptGuid(typeof(PaintedTidalTerrain));
            var analytic = new Dictionary<string, string>
            {
                [ScriptGuid(typeof(TidalTerrain))] = nameof(TidalTerrain),
                [ScriptGuid(typeof(RectTidalTerrain))] = nameof(RectTidalTerrain),
                [ScriptGuid(typeof(MainlandTidalTerrain))] = nameof(MainlandTidalTerrain),
            };
            var blocks = Parse(scenePath);
            var activeGo = new HashSet<string>(blocks.Where(b => b.ClassId == 1 && b.Field("m_IsActive") == "1").Select(b => b.FileId));
            var result = new SceneTerrains();
            int paintedSource = (int)WaterSurface.DepthSource.PaintedHeightMap;
            foreach (var b in blocks.Where(b => b.ClassId == 114))
            {
                if (b.Field("m_Enabled") != "1" || !activeGo.Contains(b.Ref("m_GameObject") ?? "")) continue;
                string script = b.Guid("m_Script");
                if (script == water && b.Field("_depthSource") == paintedSource.ToString())
                    result.PaintedWaterTextures.Add(b.Guid("_paintedHeightTex"));
                else if (script == painted)
                    result.PaintedTerrainMaps.Add(b.Guid("_map"));
                else if (script != null && analytic.TryGetValue(script, out var name))
                    result.LiveAnalytic.Add(name);
            }
            return result;
        }

        static IEnumerable<string> Scenes() =>
            Directory.GetFiles(ScenesDir, "*.unity", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p);

        [Test]
        public void EveryRegionThatDrawsWaterFromAPaintedMap_WalksOnThatMap()
        {
            var faults = new List<string>();
            int checkedRegions = 0;
            foreach (var scene in Scenes())
            {
                var t = Read(scene);
                if (t.PaintedWaterTextures.Count == 0) continue;
                checkedRegions++;
                if (t.LiveAnalytic.Count > 0)
                    faults.Add($"{scene}: an analytic terrain is still enabled ({string.Join(", ", t.LiveAnalytic)}); the walk would read it, not the drawn map");
                if (t.PaintedTerrainMaps.Count != 1)
                {
                    faults.Add($"{scene}: draws water from a painted map but has {t.PaintedTerrainMaps.Count} enabled PaintedTidalTerrain (needs exactly one)");
                    continue;
                }
                var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(AssetDatabase.GUIDToAssetPath(t.PaintedTerrainMaps[0] ?? ""));
                if (map == null || map.HeightTexture == null) { faults.Add($"{scene}: the PaintedTidalTerrain has no map or no height texture"); continue; }
                string walked = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map.HeightTexture));
                foreach (var drawn in t.PaintedWaterTextures.Distinct())
                    if (drawn != walked)
                        faults.Add($"{scene}: the water draws {AssetDatabase.GUIDToAssetPath(drawn ?? "")} but the walk reads {AssetDatabase.GetAssetPath(map.HeightTexture)}");
            }
            Assert.That(checkedRegions, Is.GreaterThanOrEqualTo(4), "expected at least the four painted regions (St Peters, Cumberland, Nine Mile Creek, Rice Point)");
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        [Test]
        public void StPeters_WalksOnItsSeabed_AndTheGroundDrawsTheSameTexture()
        {
            var t = Read(StPetersScene);
            Assert.That(t.LiveAnalytic, Is.Empty, "St Peters' analytic TidalTerrain must stay disabled");
            Assert.That(t.PaintedTerrainMaps, Has.Count.EqualTo(1));
            Assert.That(AssetDatabase.GUIDToAssetPath(t.PaintedTerrainMaps[0]), Is.EqualTo(StPetersSeabed));

            // the splat ground draws from the same texture as the water and the walk
            var seabed = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(StPetersSeabed);
            string tex = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(seabed.HeightTexture));
            string splatScript = ScriptGuid(typeof(TerrainSplatSurface));
            var splats = Parse(StPetersScene).Where(b => b.ClassId == 114 && b.Guid("m_Script") == splatScript).ToList();
            Assert.That(splats, Is.Not.Empty, "St Peters has no TerrainSplatSurface");
            foreach (var s in splats) Assert.That(s.Guid("_heightTexture"), Is.EqualTo(tex), "the splat ground must draw the seabed the walk reads");
        }

        // The places the tide game is built on must read the same on the drawn ground as on the analytic plan
        // they were tuned on, so walking on the drawn ground moves no berth and no crossing window.
        static readonly Vector2[] PlayCriticalPoints = BuildPoints();

        static Vector2[] BuildPoints()
        {
            var pts = new List<Vector2>();
            for (float x = 205.5f; x < 218f; x += 1f) pts.Add(new Vector2(x, -5.8f));   // the alongside berth pocket
            for (float x = 211f; x < 215.5f; x += 1f) pts.Add(new Vector2(x, 7.5f));     // the dory's north pocket
            for (float x = 206f; x < 256f; x += 4f) pts.Add(new Vector2(x, 0f));         // the approach
            return pts.ToArray();
        }

        [Test]
        public void StPeters_DrawnGround_KeepsTheBerthsAndTheBarChannelOfTheAnalyticPlan()
        {
            var seabed = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(StPetersSeabed);
            seabed.Rebuild();
            var field = seabed.Field;
            Assert.That(field, Is.Not.Null, "the seabed must decode (CPU-readable)");
            var go = new GameObject("analytic plan (test)");
            try
            {
                var plan = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(plan);
                foreach (var p in PlayCriticalPoints)
                    Assert.That(field.ElevationAt(p), Is.EqualTo(plan.ElevationAt(p)).Within(0.05f), $"ground at {p}");

                // the bar crossing is set by its lowest point, the channel gut: same depth on both
                float drawnLow = float.MaxValue, planLow = float.MaxValue;
                for (float x = StPetersBuilder.SandbarTo.x + 6f; x < StPetersBuilder.SandbarFrom.x; x += 0.5f)
                {
                    drawnLow = Mathf.Min(drawnLow, field.ElevationAt(new Vector2(x, 0f)));
                    planLow = Mathf.Min(planLow, plan.ElevationAt(new Vector2(x, 0f)));
                }
                Assert.That(planLow, Is.EqualTo(StPetersBuilder.ChannelBedElevation).Within(0.05f));
                Assert.That(drawnLow, Is.EqualTo(planLow).Within(0.05f), "the bar's channel low point sets the crossing window");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
