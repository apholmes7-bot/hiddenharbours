using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>No tree or shrub stands on the beach, in the water or on a road</b> (owner, 2026-11-15: "Did trees get
    /// placed on the beach in that screenshot?"). Held against what SHIPS, in every region scene: each live tree and
    /// shrub (a SpriteRenderer drawn with <c>Tree.mat</c> or <c>LitShrub.mat</c>) at its world pivot, over the
    /// ground the region's own <c>TerrainSplatSurface</c> paints, decoded straight from the committed height PNG.
    ///
    /// <para><b>The floor is the region's own number</b>: the splat surface's <c>_floorMarram</c>, the height below
    /// which the ground shader paints bare sand (and below that the wet strand and the water). A region drawn by
    /// rectangle zones (<see cref="RectTidalTerrain"/>) has no painted sand line; there a plant must stand on a zone's
    /// flat top, never on the falloff that IS its beach or quay edge.</para>
    ///
    /// <para><b>Roads</b>: Nine Mile Creek's road network file (every route and corner flare at its carriageway,
    /// <see cref="NineMileCreekRoadClearings.Inside"/>, the planter's own clearing rule), and wherever a splat
    /// surface carries a path channel (<c>_splatF</c> red, St Peters), the painted path.</para>
    ///
    /// <para>The census these tests hold, measured offline on main 77be3b85be: Nine Mile Creek had 46 trees and 149
    /// shrubs below the floor (the y −192 and y +192 field-boundary hedgerows running out over the strand, a road hedge
    /// marching 80 m into the sea at x −222, and the north wood lot's creek edge). They were retired whole, each
    /// GameObject with every document under it, as the layer refresh's step 12 retires planting on a carriageway.
    /// St Peters (259 trees, 32 shrubs) and Greywick (11) had none.</para>
    /// </summary>
    public class RegionTreePlacementContentTests
    {
        const string ScenesFolder = "Assets/_Project/Scenes";
        const string TreeMaterial = "Assets/_Project/Art/Materials/Tree.mat";
        const string ShrubMaterial = "Assets/_Project/Art/Materials/LitShrub.mat";
        const string SplatClass = "HiddenHarbours.Art.TerrainSplatSurface";
        const string RectTerrainClass = "HiddenHarbours.World.RectTidalTerrain";
        const string NineMileCreekScene = "NineMileCreek";
        const int GameObjectClass = 1, SpriteRendererClass = 212;

        /// <summary>A path texel counts as path at half its channel or more (8-bit red).</summary>
        const int PathChannelThreshold = 128;

        static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.CultureInvariant);
        static readonly Regex NumRx = new Regex(@"(\w+): (-?[\d.eE+-]+)", RegexOptions.CultureInvariant);

        public static IEnumerable<string> RegionScenes() =>
            Directory.GetFiles(ScenesFolder, "*.unity").Select(Path.GetFileNameWithoutExtension).OrderBy(s => s, StringComparer.Ordinal);

        // =============================================================================================================
        //  THE RULE, PER REGION
        // =============================================================================================================

        [TestCaseSource(nameof(RegionScenes))]
        public void NoTreeOrShrubStandsBelowTheDryGroundFloorOrOnARoad(string region)
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{region}.unity"));
            List<Plant> plants = Plants(scene);
            if (plants.Count == 0) { Assert.Pass($"{region}: no trees or shrubs."); return; }

            Ground ground = Ground.Of(scene, region);
            Assert.IsNotNull(ground, $"{region} carries {plants.Count} trees and shrubs but no ground this test can read " +
                                     "(a TerrainSplatSurface or a RectTidalTerrain) — so nothing proves they stand on land");
            var roads = region == NineMileCreekScene ? NineMileCreekRoadNetwork.Load() : null;

            var offenders = new List<string>();
            foreach (Plant p in plants)
            {
                string why = ground.WhyNot(p.At);
                if (why == null && roads != null)
                {
                    foreach (var r in roads.Routes.Concat(roads.Flares))
                        if (NineMileCreekRoadClearings.Inside(r, p.At)) { why = $"on {r}'s carriageway"; break; }
                }
                if (why != null) offenders.Add($"{p.Name} &{p.FileId} at ({p.At.x:F1}, {p.At.y:F1}): {why}");
            }
            TestContext.WriteLine($"[TreePlacement] {region}: {plants.Count(p => p.Tree)} trees, {plants.Count(p => !p.Tree)} shrubs, " +
                                  $"{offenders.Count} off the dry ground.");
            Assert.IsEmpty(offenders, $"{region}: {offenders.Count} trees or shrubs stand on the beach, in the water or " +
                                      "on a road:\n  " + string.Join("\n  ", offenders.Take(25)));
        }

        // =============================================================================================================
        //  THE PREMISES — so the rule above cannot pass by reading nothing
        // =============================================================================================================

        /// <summary>The owner's strand shot (camera −344, −190) is beach by this test's own reading, and a tree deep in
        /// the middle wood lot is dry ground: the sampler sees the ground the player sees.</summary>
        [Test]
        public void TheOwnersStrandSpotReadsAsBeachAndTheWoodLotAsDryGround()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{NineMileCreekScene}.unity"));
            Ground ground = Ground.Of(scene, NineMileCreekScene);
            Assert.IsNotNull(ground);
            Assert.IsNotNull(ground.WhyNot(new Vector2(-344f, -192f)), "the strand under the owner's shot reads as dry ground");
            Assert.IsNull(ground.WhyNot(new Vector2(-75f, 5f)), "the middle wood lot's heart reads as beach or water");
            Assert.IsNotNull(ground.WhyNot(new Vector2(-230f, -270f)), "open water south of the strand reads as dry ground");
        }

        [Test]
        public void EveryRegionWithPlantingIsFound()
        {
            var counts = RegionScenes().ToDictionary(r => r,
                r => Plants(SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{r}.unity"))).Count);
            TestContext.WriteLine("[TreePlacement] " + string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value)));
            Assert.That(counts[NineMileCreekScene], Is.GreaterThan(500), "Nine Mile Creek's planting was not found");
            Assert.That(counts["StPeters"], Is.GreaterThan(200), "St Peters' woods were not found");
        }

        // =============================================================================================================
        //  PLANTS
        // =============================================================================================================

        sealed class Plant
        {
            public string Name; public long FileId; public Vector2 At; public bool Tree;
        }

        static List<Plant> Plants(SceneYaml scene)
        {
            string tree = AssetDatabase.AssetPathToGUID(TreeMaterial), shrub = AssetDatabase.AssetPathToGUID(ShrubMaterial);
            Assert.IsNotEmpty(tree, TreeMaterial); Assert.IsNotEmpty(shrub, ShrubMaterial);
            var list = new List<Plant>();
            foreach (Doc sr in scene.Docs)
            {
                if (sr.ClassId != SpriteRendererClass || sr.Stripped || sr.Field("m_Enabled") != "1") continue;
                string mat = FirstMaterialGuid(sr);
                if (mat != tree && mat != shrub) continue;
                Doc go = scene.Get(sr.FieldRef("m_GameObject"));
                if (go == null || go.ClassId != GameObjectClass || !LiveInHierarchy(scene, go)) continue;
                list.Add(new Plant { Name = go.Field("m_Name"), FileId = go.FileId, At = WorldPivot(scene, go), Tree = mat == tree });
            }
            return list;
        }

        static string FirstMaterialGuid(Doc sr)
        {
            string[] lines = sr.Lines;
            for (int i = 0; i + 1 < lines.Length; i++)
            {
                if (lines[i] != "  m_Materials:") continue;
                if (!lines[i + 1].StartsWith("  - ", StringComparison.Ordinal)) return null;
                Match m = GuidRx.Match(lines[i + 1]);
                return m.Success ? m.Groups[1].Value : null;
            }
            return null;
        }

        static bool LiveInHierarchy(SceneYaml scene, Doc go)
        {
            Doc tr = scene.TransformOf(go);
            while (tr != null)
            {
                Doc owner = scene.Get(tr.FieldRef("m_GameObject"));
                if (owner != null && owner.Field("m_IsActive") != "1") return false;
                long father = tr.FieldRef("m_Father");
                tr = father == 0 ? null : scene.Get(father);
            }
            return true;
        }

        /// <summary>The GameObject's pivot in world metres: its own local position through every ancestor's scale,
        /// z-rotation and position.</summary>
        static Vector2 WorldPivot(SceneYaml scene, Doc go)
        {
            Doc tr = scene.TransformOf(go);
            Vector2 p = Xy(tr.Field("m_LocalPosition"));
            long father = tr.FieldRef("m_Father");
            while (father != 0)
            {
                Doc f = scene.Require(father, "a plant's ancestor");
                var s = Nums(f.Field("m_LocalScale"));
                var q = Nums(f.Field("m_LocalRotation"));
                float angle = 2f * Mathf.Atan2(Get(q, "z", 0f), Get(q, "w", 1f));
                var scaled = new Vector2(p.x * Get(s, "x", 1f), p.y * Get(s, "y", 1f));
                float c = Mathf.Cos(angle), n = Mathf.Sin(angle);
                p = Xy(f.Field("m_LocalPosition")) + new Vector2(c * scaled.x - n * scaled.y, n * scaled.x + c * scaled.y);
                father = f.FieldRef("m_Father");
            }
            return p;
        }

        static Dictionary<string, float> Nums(string yaml)
        {
            var d = new Dictionary<string, float>();
            if (yaml == null) return d;
            foreach (Match m in NumRx.Matches(yaml)) d[m.Groups[1].Value] = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return d;
        }

        static float Get(Dictionary<string, float> d, string k, float fallback) => d.TryGetValue(k, out float v) ? v : fallback;
        static Vector2 Xy(string yaml) { var d = Nums(yaml); return new Vector2(Get(d, "x", 0f), Get(d, "y", 0f)); }

        // =============================================================================================================
        //  GROUND
        // =============================================================================================================

        abstract class Ground
        {
            /// <summary>Why a plant may not stand at <paramref name="p"/>, or null when it is dry land.</summary>
            public abstract string WhyNot(Vector2 p);

            public static Ground Of(SceneYaml scene, string region)
            {
                Doc splat = scene.Docs.FirstOrDefault(d => d.IsScript(SplatClass));
                if (splat != null) return new PaintedGround(splat, region);
                Doc rect = scene.Docs.FirstOrDefault(d => d.IsScript(RectTerrainClass));
                if (rect != null) return new ZoneGround(rect);
                return null;
            }
        }

        /// <summary>The splat surface's height PNG, sampled as the GPU samples it (bilinear on texel centres), with
        /// its sand line and its path channel.</summary>
        sealed class PaintedGround : Ground
        {
            readonly Vector2 _centre, _size;
            readonly float _min, _max, _floor;
            readonly ushort[] _codes; readonly int _w, _h;
            readonly byte[] _path; readonly int _pw, _ph;

            public PaintedGround(Doc splat, string region)
            {
                _centre = Xy(splat.Field("_worldCenter")); _size = Xy(splat.Field("_worldSize"));
                _min = ParseFloat(splat.Field("_heightMin")); _max = ParseFloat(splat.Field("_heightMax"));
                _floor = ParseFloat(splat.Field("_floorMarram"));
                var height = TerrainPlanPng.ReadFile(PathOf(splat, "_heightTexture", region));
                Assert.AreEqual(16, height.BitDepth, $"{region}: the height map is not 16-bit");
                Assert.AreEqual(1, height.Channels, $"{region}: the height map is not greyscale");
                _codes = height.Samples; _w = height.Width; _h = height.Height;
                string f = splat.Field("_splatF");
                if (f != null && GuidRx.IsMatch(f))
                {
                    var path = TerrainPlanPng.ReadFile(PathOf(splat, "_splatF", region));
                    Assert.AreEqual(8, path.BitDepth, $"{region}: the path splat is not 8-bit");
                    _path = path.Channel8(0); _pw = path.Width; _ph = path.Height;
                }
            }

            static string PathOf(Doc splat, string key, string region)
            {
                Match m = GuidRx.Match(splat.Field(key) ?? "");
                Assert.IsTrue(m.Success, $"{region}: the splat surface names no {key}");
                string asset = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                Assert.IsNotEmpty(asset, $"{region}: {key} names a missing asset");
                return Path.GetFullPath(asset);
            }

            float Code(int x, int y)   // y counts from the bottom row, as the texture's v does
            {
                x = Mathf.Clamp(x, 0, _w - 1); y = Mathf.Clamp(y, 0, _h - 1);
                return _codes[(_h - 1 - y) * _w + x] / 65535f;
            }

            public float ElevationAt(Vector2 p)
            {
                float u = (p.x - (_centre.x - _size.x * 0.5f)) / _size.x, v = (p.y - (_centre.y - _size.y * 0.5f)) / _size.y;
                float fx = u * _w - 0.5f, fy = v * _h - 0.5f;
                int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0, ty = fy - y0;
                float r = Mathf.Lerp(Mathf.Lerp(Code(x0, y0), Code(x0 + 1, y0), tx), Mathf.Lerp(Code(x0, y0 + 1), Code(x0 + 1, y0 + 1), tx), ty);
                return _min + (_max - _min) * r;
            }

            public override string WhyNot(Vector2 p)
            {
                float e = ElevationAt(p);
                if (!(e >= _floor)) return $"ground at {e:F2} m, below the {_floor} m sand line (beach, strand or water)";
                if (_path != null)
                {
                    float u = (p.x - (_centre.x - _size.x * 0.5f)) / _size.x, v = (p.y - (_centre.y - _size.y * 0.5f)) / _size.y;
                    int x = Mathf.Clamp((int)(u * _pw), 0, _pw - 1), y = Mathf.Clamp((int)(v * _ph), 0, _ph - 1);
                    if (_path[(_ph - 1 - y) * _pw + x] >= PathChannelThreshold) return "on a painted path";
                }
                return null;
            }
        }

        /// <summary>A rectangle-zone region: land is a zone's flat top; its falloff is the beach or quay edge.</summary>
        sealed class ZoneGround : Ground
        {
            readonly RectTidalTerrain.LandZone[] _zones;

            public ZoneGround(Doc rect)
            {
                var zones = new List<RectTidalTerrain.LandZone>();
                string[] lines = rect.Lines;
                for (int i = 0; i + 3 < lines.Length; i++)
                {
                    if (!lines[i].StartsWith("  - Center:", StringComparison.Ordinal)) continue;
                    zones.Add(new RectTidalTerrain.LandZone(Xy(lines[i].Substring(lines[i].IndexOf(':') + 1)),
                        Xy(lines[i + 1].Substring(lines[i + 1].IndexOf(':') + 1)),
                        ParseFloat(lines[i + 2].Substring(lines[i + 2].IndexOf(':') + 1).Trim()),
                        ParseFloat(lines[i + 3].Substring(lines[i + 3].IndexOf(':') + 1).Trim())));
                }
                Assert.IsNotEmpty(zones, "a RectTidalTerrain with no zones");
                _zones = zones.ToArray();
            }

            public override string WhyNot(Vector2 p)
            {
                foreach (var z in _zones)
                    if (Mathf.Abs(p.x - z.Center.x) <= z.HalfSize.x && Mathf.Abs(p.y - z.Center.y) <= z.HalfSize.y) return null;
                return "off every land zone's flat top (on its beach or quay falloff, or in the water)";
            }
        }
    }
}
