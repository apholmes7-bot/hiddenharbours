using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The old grass tufts are mainly gone</b> (owner, 2026-11-15) — held against what SHIPS: the scene
    /// file as text, the thinning Def as imported, and the region's ground decoded straight from its PNG.
    ///
    /// <para>The census these tests reproduce, measured offline on main 77be3b85be: Nine Mile Creek's one
    /// <see cref="GrassField"/> (<c>CreekFields</c>) grew 33,921 tufts, 5,474 of them on ground below −0.4 m
    /// (water and wet strand) and 2,000 more on the bare-sand band; no other region scene carries a grass
    /// field at all. Through the rule: about 1% survive, every one beside a tree or shrub and on dry ground.</para>
    /// </summary>
    public class NineMileCreekGrassThinningContentTests
    {
        const string DefPath = "Assets/_Project/Data/Regions/NineMileCreek/GrassThinning.asset";
        const string ScenesFolder = "Assets/_Project/Scenes";
        const string GrassFieldClass = "HiddenHarbours.Art.GrassField";
        const string SplatClass = "HiddenHarbours.Art.TerrainSplatSurface";
        const int GameObjectClassId = 1;

        /// <summary>The most of the baked meadow the thinning may leave standing. "Mainly gone" as a number.</summary>
        const float MostThatMaySurvive = 0.03f;

        static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.CultureInvariant);

        GrassThinningDef Def() =>
            AssetDatabase.LoadAssetAtPath<GrassThinningDef>(DefPath)
            ?? throw new AssertionException($"{DefPath} is missing — the region's thinning rule is not shipped.");

        static Doc CreekField(SceneYaml scene) =>
            scene.Docs.Single(d => d.IsScript(GrassFieldClass));

        [Test]
        public void TheRuleIsWiredOntoTheCreekFieldAndSortsWithTheVegetation()
        {
            Doc field = CreekField(NineMileCreekLiveGround.Scene);
            string guid = AssetDatabase.AssetPathToGUID(DefPath);
            Assert.IsNotEmpty(guid, DefPath);
            Assert.AreEqual(guid, NineMileCreekLiveGround.RefGuid(field.Field("_thinning")),
                "CreekFields must draw through the Nine Mile Creek thinning rule");
            Assert.AreEqual("1", field.Field("_rowOrderSteps"),
                "the survivors are few, so the field sorts at one order step per row — bit-identical to the " +
                "Y-sorted trees and shrubs they stand among, instead of up to half a metre off");
        }

        [Test]
        public void TheDefNamesTreesAndShrubsTheRegionGroundAndADryFloor()
        {
            var def = Def();
            Assert.AreEqual("grassthin.nine_mile_creek", def.Id);
            Assert.That(def.KeepFraction, Is.GreaterThan(0f).And.LessThan(1f), "a density cap, not all-or-nothing");
            Assert.IsNotNull(def.Ground, "the dry-ground test needs the region's height map");
            Assert.AreEqual(NineMileCreekGround.MapPath, AssetDatabase.GetAssetPath(def.GroundAsset));

            var names = def.Anchors.Select(a => AssetDatabase.GetAssetPath(a.Material)).ToList();
            CollectionAssert.AreEquivalent(
                new[] { "Assets/_Project/Art/Materials/Tree.mat", "Assets/_Project/Art/Materials/LitShrub.mat" }, names,
                "trees and shrubs only — shore plants stand on the strand, which is where the grass must go");
            foreach (var a in def.Anchors)
                Assert.That(a.RadiusMetres, Is.GreaterThan(0f).And.LessThanOrEqualTo(4f),
                    $"{a.Name}: a shelter wider than a canopy is open ground again");

            // The floor must sit at or above the top of the bare-sand band the region's ground shader paints.
            Doc splat = NineMileCreekLiveGround.Scene.Docs.Single(d => d.IsScript(SplatClass));
            float marram = NineMileCreekLiveGround.Number(splat, "_floorMarram");
            Assert.That(def.MinElevationMetres, Is.GreaterThanOrEqualTo(marram),
                $"tufts may stand down to {def.MinElevationMetres} m, but the ground paints bare sand up to {marram} m");
        }

        [Test]
        public void EveryRegionGrassFieldDrawsThroughARule()
        {
            foreach (string path in Directory.GetFiles(ScenesFolder, "*.unity"))
            {
                string text = File.ReadAllText(path);
                if (!text.Contains(GrassFieldClass)) continue;
                var scene = SceneYaml.Parse(text);
                foreach (Doc field in scene.Docs.Where(d => d.IsScript(GrassFieldClass)))
                {
                    Match guid = GuidRx.Match(field.Field("_thinning") ?? "");
                    Assert.IsTrue(guid.Success,
                        $"{Path.GetFileName(path)} carries a grass field with no thinning rule — the old tufts " +
                        "would be back over its sand and water");
                }
            }
        }

        [Test]
        public void NoSurvivingTuftStandsOnSandOrWaterAndMostAreGone()
        {
            var def = Def();
            SceneYaml scene = NineMileCreekLiveGround.Scene;
            Doc field = CreekField(scene);

            var layout = new GrassFieldLayout
            {
                OriginX = NineMileCreekLiveGround.Vector(field, "_originWorld").x,
                OriginY = NineMileCreekLiveGround.Vector(field, "_originWorld").y,
                CellSize = NineMileCreekLiveGround.Number(field, "_cellSize"),
                JitterMetres = NineMileCreekLiveGround.Number(field, "_jitterMetres"),
                SpreadMetres = NineMileCreekLiveGround.Number(field, "_spreadMetres"),
                CellsX = int.Parse(field.Field("_cellsX"), CultureInfo.InvariantCulture),
                CellsY = int.Parse(field.Field("_cellsY"), CultureInfo.InvariantCulture),
                Slots = int.Parse(field.Field("_slots"), CultureInfo.InvariantCulture),
                Seed = int.Parse(field.Field("_seed"), CultureInfo.InvariantCulture),
            };
            byte[] sites = GrassFieldCodec.Decode(field.Field("_sitePayload"));
            Assert.AreEqual(layout.CellsX * layout.CellsY * layout.Slots, sites.Length);

            var anchors = new VegetationAnchorIndex(Anchors(scene, def));
            Assert.That(anchors.Count, Is.GreaterThan(100), "the region's trees and shrubs were not found");

            ITidalTerrain ground = def.Ground;
            ITidalTerrain plan = NineMileCreekLiveGround.Plan;   // the same ground, decoded from the PNG itself
            int raw = 0, kept = 0;
            var offenders = new List<string>();
            for (int ix = 0; ix < layout.CellsX; ix++)
            for (int iy = 0; iy < layout.CellsY; iy++)
            for (int s = 0; s < layout.Slots; s++)
            {
                if (GrassFieldScatter.IsEmpty(sites[(iy * layout.CellsX + ix) * layout.Slots + s])) continue;
                raw++;
                Vector2 p = GrassFieldScatter.SlotPosition(layout, ix, iy, s);
                if (!GrassThinning.Keeps(p, GrassFieldScatter.KeepRoll(layout, ix, iy, s), def.KeepFraction,
                                         anchors, ground, def.MinElevationMetres)) continue;
                kept++;
                float e = plan.ElevationAt(p);
                if (e < def.MinElevationMetres - PlanTolerance && offenders.Count < 10)
                    offenders.Add($"({p.x:F1}, {p.y:F1}) at {e:F2} m");
            }

            TestContext.WriteLine($"[GrassThinning] Nine Mile Creek: {raw:N0} baked tufts, {kept:N0} drawn " +
                                  $"({100f * kept / Mathf.Max(raw, 1):F2}%), {anchors.Count} trees and shrubs.");
            Assert.That(raw, Is.GreaterThan(30000), "the baked field itself changed — re-measure the census");
            Assert.That(kept, Is.GreaterThan(0), "every tuft went — the rule was meant to keep some among the trees");
            Assert.That(kept, Is.LessThan(raw * MostThatMaySurvive), "the old grass is not mainly gone");
            Assert.IsEmpty(offenders, "surviving tufts stand on sand or water: " + string.Join(", ", offenders));
        }

        /// <summary>How far the imported texture's decode may sit from the PNG's own (one R16 code is
        /// 0.0002 m; this is generous for a re-import at a different precision).</summary>
        const float PlanTolerance = 0.05f;

        /// <summary>Every enabled SpriteRenderer on an active GameObject whose first material is one of the
        /// rule's anchors, at its world pivot — what <see cref="GrassField.GatherAnchors"/> finds at load.</summary>
        static List<VegetationAnchor> Anchors(SceneYaml scene, GrassThinningDef def)
        {
            var radiusByGuid = new Dictionary<string, float>();
            foreach (var a in def.Anchors)
                radiusByGuid[AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(a.Material))] = a.RadiusMetres;

            var list = new List<VegetationAnchor>();
            foreach (Doc sr in scene.Docs)
            {
                if (sr.ClassId != NineMileCreekLiveGround.SpriteRendererClass || sr.Stripped) continue;
                if (sr.Field("m_Enabled") != "1") continue;
                string mat = FirstMaterialGuid(sr);
                if (mat == null || !radiusByGuid.TryGetValue(mat, out float radius)) continue;
                Doc go = scene.Get(sr.FieldRef("m_GameObject"));
                // ToWorld and Live read NineMileCreekLiveGround.Scene, which is the scene passed in here.
                if (go == null || go.ClassId != GameObjectClassId || !NineMileCreekLiveGround.Live(go)) continue;
                list.Add(new VegetationAnchor(NineMileCreekLiveGround.ToWorld(go, Vector2.zero), radius));
            }
            return list;
        }

        static string FirstMaterialGuid(Doc sr)
        {
            string[] lines = sr.Lines;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != "  m_Materials:") continue;
                if (i + 1 < lines.Length && lines[i + 1].StartsWith("  - ", StringComparison.Ordinal))
                {
                    Match m = GuidRx.Match(lines[i + 1]);
                    return m.Success ? m.Groups[1].Value : null;
                }
                return null;
            }
            return null;
        }
    }
}
