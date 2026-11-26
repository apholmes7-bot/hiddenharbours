using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Fenders protect the band a hull actually rubs (spring low to spring high plus freeboard), every tyre
    /// hangs on a timber that reaches below spring low, the strings stand inside the alongside gap, every
    /// ladder is the full-length picture and stands clear of the strings, and the scene steps that hang them
    /// have nothing left to change on the committed scenes.
    /// </summary>
    public sealed class WharfFenderRunTests
    {
        const string RunsFolder = "Assets/_Project/Data/Regions/Fenders";
        const string RegionsFolder = "Assets/_Project/Data/Regions";
        const string ScenesFolder = "Assets/_Project/Scenes";
        const string KindPath = "Assets/_Project/Data/Regions/Ladders/WharfLadder_Galvanised.asset";
        const string WaterlineType = "HiddenHarbours.Art.TidalFaceWaterline";
        const string LadderType = "HiddenHarbours.World.WharfLadder";
        /// <summary>The gap a hull keeps from the face when it lies alongside (the rig's MAX_STANDOUT).</summary>
        const float AlongsideGapMetres = 0.40f;

        [Serializable] sealed class StringContract
        {
            public float top, lowWater, highWater, freeboard, timberFoot, standOut, outsideDiameter;
            public float[] tyreCentres, rubBandCovered;
        }
        [Serializable] sealed class BakeOptions { public float top, height, lowWater, highWater; }

        static WharfFenderRunDef[] Runs() => Directory.GetFiles(RunsFolder, "*.asset")
            .Select(p => AssetDatabase.LoadAssetAtPath<WharfFenderRunDef>(p.Replace('\\', '/'))).Where(r => r != null)
            .OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();

        static RegionDef Region(string scene) => AssetDatabase.LoadAssetAtPath<RegionDef>($"{RegionsFolder}/{scene}.asset");
        static float Low(RegionDef r) => r.TideMeanLevel - r.TideAmplitude;
        static float High(RegionDef r) => r.TideMeanLevel + r.TideAmplitude;
        static WharfLadderDef Kind() => AssetDatabase.LoadAssetAtPath<WharfLadderDef>(KindPath);
        static SetPieceDef Art(string key) => AssetDatabase.LoadAssetAtPath<SetPieceDef>(WharfFenderBaker.DefPath(key));
        static float Num(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        static readonly Dictionary<string, SceneYaml> Scenes = new Dictionary<string, SceneYaml>();
        static SceneYaml Scene(string name) =>
            Scenes.TryGetValue(name, out var s) ? s : Scenes[name] = SceneYaml.Parse(File.ReadAllText($"{ScenesFolder}/{name}.unity"));

        static Vector2 WorldOf(SceneYaml scene, Doc go)
        {
            Vector2 at = Vector2.zero;
            for (Doc t = scene.TransformOf(go); t != null; t = scene.Get(t.FieldRef("m_Father")))
                at += (Vector2)ParseVec3(t.Field("m_LocalPosition"));
            return at;
        }

        /// <summary>The objects a run hangs in its scene: St Peters keeps the fitting name the dock places by.</summary>
        static List<Doc> StringsOf(WharfFenderRunDef run)
        {
            SceneYaml scene = Scene(run.RegionScene);
            string prefix = run.RegionScene == "StPeters" ? "Fitting_tyre" : "Fender_" + run.Id.Substring(run.Id.IndexOf('.') + 1) + "_";
            return scene.Docs.Where(d => d.ClassId == 1 && !d.Stripped && (d.Field("m_Name") ?? "").StartsWith(prefix, StringComparison.Ordinal))
                .Where(d => Mathf.Abs(WorldOf(scene, d).y - run.LipWorldY) < 1e-3f).ToList();
        }

        [Test]
        public void EveryRegionWithAWharfLadder_HasAFenderRun_AndEveryRunHasAStableIdAndItsRegionsTide()
        {
            var runs = Runs();
            Assert.That(runs.Select(r => r.Id), Is.Unique);
            foreach (var run in runs)
            {
                StringAssert.StartsWith("fender_run.", run.Id);
                Assert.IsNotNull(Region(run.RegionScene), run.Id + " names a region");
                Assert.IsNotEmpty(run.HangerXs, run.Id + " hangs strings");
            }
            foreach (string path in Directory.GetFiles(ScenesFolder, "*.unity"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (Region(name) == null) continue;
                bool hasLadder = File.ReadAllText(path).Contains("m_EditorClassIdentifier: HiddenHarbours.World::" + LadderType + "\n");
                if (hasLadder) Assert.IsTrue(runs.Any(r => r.RegionScene == name), name + " has a wharf ladder and no fender run");
            }
        }

        [Test]
        public void EveryRunsArt_IsBakedForItsDeck_AndItsRegionsTide()
        {
            foreach (var run in Runs())
            {
                var art = Art(run.ArtKey);
                Assert.IsNotNull(art, run.ArtKey);
                var o = JsonUtility.FromJson<BakeOptions>(art.BakedOptionsJson);
                RegionDef region = Region(run.RegionScene);
                Assert.AreEqual(run.DeckElevationMetres, o.top, 1e-4f, run.Id + ": the art is baked from this deck");
                Assert.AreEqual(Low(region), o.lowWater, 1e-4f, run.Id + ": spring low is the region's");
                Assert.AreEqual(High(region), o.highWater, 1e-4f, run.Id + ": spring high is the region's");
                Assert.AreEqual("wall", art.Mount, run.Id);
                Assert.IsTrue(art.Anchors.Any(a => a.Name == "hanger" && a.Position == Vector3.zero), run.Id + " hangs from its top");
            }
        }

        [Test]
        public void EveryString_CoversTheRubBand_OnATimberBelowSpringLow_InsideTheAlongsideGap()
        {
            WharfLadderDef kind = Kind();
            foreach (var run in Runs())
            {
                var c = JsonUtility.FromJson<StringContract>(Art(run.ArtKey).GameplayJson);
                RegionDef region = Region(run.RegionScene);
                float low = Low(region), high = High(region);
                Assert.LessOrEqual(c.rubBandCovered[0], low + 1e-4f, run.Id + ": the lowest tyre reaches spring low");
                Assert.GreaterOrEqual(c.rubBandCovered[1], Mathf.Min(high + run.FreeboardMetres, run.DeckElevationMetres - 0.15f) - 1e-4f,
                                      run.Id + ": the top tyre reaches spring high plus freeboard");
                Assert.AreEqual(run.FreeboardMetres, c.freeboard, 1e-4f, run.Id + ": baked for this freeboard");
                for (int i = 1; i < c.tyreCentres.Length; i++)
                    Assert.LessOrEqual(c.tyreCentres[i] - c.tyreCentres[i - 1], c.outsideDiameter * 1.6f + 1e-4f, run.Id + ": no bare band");
                Assert.LessOrEqual(c.timberFoot, low - kind.BelowLowestWaterMetres + 1e-4f, run.Id + ": the timber reaches below spring low");
                Assert.GreaterOrEqual(c.tyreCentres[0] - c.outsideDiameter / 2f, c.timberFoot - 1e-4f, run.Id + ": no tyre dangles past its timber");
                Assert.LessOrEqual(c.standOut, AlongsideGapMetres + 1e-4f, run.Id + ": the string fits the alongside gap");
            }
        }

        [Test]
        public void EveryRun_IsHungInItsScene_OneStringPerHanger_CutAtTheTideByItsOwnHeight()
        {
            foreach (var run in Runs())
            {
                SceneYaml scene = Scene(run.RegionScene);
                var strings = StringsOf(run);
                CollectionAssert.AreEquivalent(run.HangerXs.Select(x => Math.Round(x, 3)),
                    strings.Select(s => Math.Round(WorldOf(scene, s).x, 3)), run.Id + ": one string per hanger");
                var sprite = Art(run.ArtKey).SpriteFor(WharfFenderBaker.FaceFacingFrame);
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id));
                foreach (Doc s in strings)
                {
                    var components = scene.ComponentsOf(s);
                    Doc renderer = components.Single(d => d.ClassId == 212);
                    StringAssert.Contains($"m_Sprite: {{fileID: {id}, guid: {guid}, type: 3}}", renderer.Text, run.Id + ": the string art");
                    Doc face = components.Single(d => d.IsScript(WaterlineType));
                    Assert.AreEqual(run.DeckElevationMetres, Num(face.Field("_lipElevation")), 1e-4f);
                    Assert.AreEqual(run.LipWorldY, Num(face.Field("_lipWorldY")), 1e-4f);
                    StringAssert.Contains("_configured: 1", face.Text);
                    StringAssert.DoesNotContain("_heightTexture: {fileID: 0}", face.Text, run.Id + ": cut by its own height map");
                    Assert.AreEqual(-run.DeckElevationMetres, ParseVec3(face.Field("_heightDecode")).z, 1e-4f, run.Id + ": heights read from the deck");
                }
            }
        }

        [Test]
        public void TheKitTyresThatHungAtTheLip_AreGone()
        {
            SceneYaml nmc = Scene("NineMileCreek");
            Assert.IsFalse(nmc.Docs.Any(d => d.ClassId == 1 && (d.Field("m_Name") == "Fitting_tyre" || d.Field("m_Name") == "Fitting_ladder_berth0")),
                           "Nine Mile Creek's cap tyres and the berth-0 kit ladder are retired");
            string kitTyre = "fileID: 194222916, guid: 1296edfe2a78aeb4182e57d0a78e122c";
            foreach (string scene in new[] { "StPeters", "NineMileCreek" })
                Assert.IsFalse(Scene(scene).Docs.Any(d => d.ClassId == 212 && d.Text.Contains(kitTyre)), scene + " draws no kit lip tyre");
        }

        static IEnumerable<(string id, Vector2 at, float climbTop, Doc ladder)> LaddersIn(string sceneName)
        {
            SceneYaml scene = Scene(sceneName);
            foreach (Doc d in scene.Docs.Where(d => d.IsScript(LadderType)))
            {
                Doc go = scene.Require(d.FieldRef("m_GameObject"), "ladder object");
                float climb = d.Field("_climbTopScreenOffset") is string c ? Num(c) : 0f;
                yield return (d.Field("_id"), WorldOf(scene, go), climb, d);
            }
        }

        [Test]
        public void EveryLadder_StandsClearOfEveryFenderStringOnItsFace()
        {
            WharfLadderDef kind = Kind();
            int checkedCount = 0;
            foreach (var run in Runs())
            {
                SceneYaml scene = Scene(run.RegionScene);
                var xs = StringsOf(run).Select(s => WorldOf(scene, s).x).ToArray();
                foreach (var l in LaddersIn(run.RegionScene).Where(l => Mathf.Abs(l.at.y + l.climbTop - run.LipWorldY) < 0.01f))
                {
                    checkedCount++;
                    foreach (float x in xs)
                        Assert.GreaterOrEqual(Mathf.Abs(x - l.at.x), kind.FaceClearanceMetres, $"{run.Id}: the string at {x} crowds {l.id}");
                }
            }
            Assert.AreEqual(10, checkedCount, "St Peters' one ladder and Nine Mile Creek's nine");
        }

        [Test]
        public void EveryNineMileCreekLadder_IsTheFullLengthPicture_ItsKindAndItsDrawnLength()
        {
            SceneYaml scene = Scene("NineMileCreek");
            var art = Art(NineMileCreekWharfLandSide.LadderArtKey);
            Assert.IsNotNull(art);
            var o = JsonUtility.FromJson<BakeOptions>(art.BakedOptionsJson);
            Sprite sprite = art.SpriteFor(WharfFenderBaker.FaceFacingFrame);
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id));
            string kindGuid = AssetDatabase.AssetPathToGUID(KindPath);
            RegionDef region = Region("NineMileCreek");
            var ladders = LaddersIn("NineMileCreek").ToList();
            Assert.AreEqual(9, ladders.Count);
            foreach (var l in ladders)
            {
                StringAssert.Contains("_kind: {fileID: 11400000, guid: " + kindGuid + ", type: 2}", l.ladder.Text, l.id);
                Assert.AreEqual(o.height, Num(l.ladder.Field("_lengthMetres")), 1e-4f, l.id + ": the climb's length is the drawn ladder's");
                Assert.AreEqual(Num(l.ladder.Field("_topElevationMeters")), o.top, 1e-4f, l.id + ": the art was baked from this deck");
                Assert.LessOrEqual(o.top - o.height, Low(region) - Kind().BelowLowestWaterMetres + 1e-4f, l.id + ": the foot is under spring low");
                Doc picture = scene.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == "Ladder_" + l.id);
                Vector2 at = WorldOf(scene, picture);
                Assert.AreEqual(l.at.x, at.x, 1e-3f, l.id + ": the ladder you see is the ladder you climb");
                Assert.AreEqual(l.at.y + l.climbTop, at.y, 1e-3f, l.id + ": hung where the climb starts");
                var components = scene.ComponentsOf(picture);
                StringAssert.Contains($"m_Sprite: {{fileID: {id}, guid: {guid}, type: 3}}", components.Single(d => d.ClassId == 212).Text);
                Doc face = components.Single(d => d.IsScript(WaterlineType));
                StringAssert.DoesNotContain("_heightTexture: {fileID: 0}", face.Text);
                Assert.AreEqual(o.top, Num(face.Field("_lipElevation")), 1e-4f);
            }
        }

        [Test]
        public void FenderAndLadderPictures_SortOverTheCutFace_AndUnderEveryHull()
        {
            Assert.That(NineMileCreekWharfLandSide.FenderSortingOrder, Is.GreaterThan(NineMileCreekWharf.FixedWaterSideSortingOrder));
            Assert.That(NineMileCreekWharfLandSide.FenderSortingOrder, Is.LessThan(SortingBands.DecorFloor),
                        "a hull alongside sorts in the decor band, over the strings");
            SceneYaml nmc = Scene("NineMileCreek");
            Doc node = nmc.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == NineMileCreekWharfLandSide.FendersNodeName);
            foreach (long tr in nmc.TransformOf(node).FieldRefs("m_Children"))
            {
                Doc go = nmc.Require(nmc.Require(tr, "child").FieldRef("m_GameObject"), "child object");
                Doc sr = nmc.ComponentsOf(go).Single(d => d.ClassId == 212);
                Assert.AreEqual(NineMileCreekWharfLandSide.FenderSortingOrder.ToString(CultureInfo.InvariantCulture), sr.Field("m_SortingOrder"), go.Field("m_Name"));
            }
        }

        [Test]
        public void TheFenderSteps_HaveNothingLeftToChange_OnTheCommittedScenes()
        {
            Assert.IsTrue(LandingFenders(Scene("StPeters")).IsEmpty, "St Peters' fender step");
            foreach (var (step, plan) in NineMileCreekWharfLandSide.FenderPlanners())
            {
                var patch = plan(Scene("NineMileCreek"));
                Assert.IsTrue(patch.IsEmpty, $"{step} on {patch.Root}: " + string.Join(", ", patch.Ops.Select(o => o.Kind + " " + o.Name)));
            }
        }
    }
}
