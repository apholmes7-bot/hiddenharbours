using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Every wharf ladder is sized for the person who climbs it, meets its deck, reaches below the lowest
    /// water its region's tide can reach, and stands clear of the fenders and piles on its face.
    /// </summary>
    public sealed class WharfLadderReachTests
    {
        const string KindPath = "Assets/_Project/Data/Regions/Ladders/WharfLadder_Galvanised.asset";
        const string KindGuid = "f4001b876e974e7c8f6a70a9d783f657";
        const string ScenesFolder = "Assets/_Project/Scenes";
        const string RegionsFolder = "Assets/_Project/Data/Regions";

        // Nine Mile Creek's ladders are rebuilt on top of the open water-side wharf rework; until that
        // lands they predate the kinds. Remove a region from here when its ladders are brought in.
        static readonly string[] PendingRegions = { "NineMileCreek" };

        static WharfLadderDef Kind() => AssetDatabase.LoadAssetAtPath<WharfLadderDef>(KindPath);

        static TideProfile Tide(float mean, float amplitude) =>
            new TideProfile { MeanLevel = mean, Amplitude = amplitude, PhaseHours = 0f };

        // ---- the pure rule ---------------------------------------------------------------------------

        [Test]
        public void LowestAndHighestWater_AreTheMeanLessAndPlusTheSpringAmplitude()
        {
            Assert.AreEqual(-2.2f, WharfLadderReach.LowestWater(Tide(0f, 2.2f)), 1e-5f);
            Assert.AreEqual(2.2f, WharfLadderReach.HighestWater(Tide(0f, 2.2f)), 1e-5f);
            Assert.AreEqual(-0.5f, WharfLadderReach.LowestWater(Tide(1f, 1.5f)), 1e-5f);
        }

        [Test]
        public void RequiredFoot_IsTheKindsMarginBelowLowestWater()
        {
            var kind = ScriptableObject.CreateInstance<WharfLadderDef>();
            try
            {
                kind.BelowLowestWaterMetres = 0.5f;
                Assert.AreEqual(-2.7f, WharfLadderReach.RequiredFootElevation(Tide(0f, 2.2f), kind), 1e-5f);
                Assert.AreEqual(8.054f, WharfLadderReach.RequiredLengthMetres(5.354f, Tide(0f, 2.2f), kind), 1e-4f);
                Assert.IsTrue(WharfLadderReach.ReachesLowestWater(5.354f, 8.1f, Tide(0f, 2.2f), kind));
                Assert.IsFalse(WharfLadderReach.ReachesLowestWater(5.354f, 0.63f, Tide(0f, 2.2f), kind),
                               "the kit's 0.63 m slice stops metres above the water");
            }
            finally { Object.DestroyImmediate(kind); }
        }

        [Test]
        public void ExposedLength_FollowsTheWater_AndNeverGoesNegative()
        {
            Assert.AreEqual(3.154f, WharfLadderReach.ExposedMetres(5.354f, 8.1f, 2.2f), 1e-4f);
            Assert.AreEqual(7.554f, WharfLadderReach.ExposedMetres(5.354f, 8.1f, -2.2f), 1e-4f);
            Assert.AreEqual(8.1f, WharfLadderReach.ExposedMetres(5.354f, 8.1f, -9f), 1e-4f, "water below the foot");
            Assert.AreEqual(0f, WharfLadderReach.ExposedMetres(5.354f, 8.1f, 9f), "water over the deck");
        }

        [Test]
        public void TheGalvanisedKind_IsSizedForA175Climber()
        {
            WharfLadderDef kind = Kind();
            Assert.IsNotNull(kind, KindPath);
            Assert.AreEqual("ladder.galvanised_wharf", kind.Id);
            Assert.AreEqual(LadderBoardingMath.RigRungMetres, kind.RungMetres, 1e-5f,
                            "the clip's soles were authored on this rung spacing");
            Assert.AreEqual(LadderBoardingMath.RigStandoffMetres, kind.StandoffMetres, 1e-5f);
            Assert.IsTrue(WharfLadderReach.RungInReach(kind.RungMetres, kind));
            Assert.Less(kind.MinRungPitchMetres, kind.MaxRungPitchMetres);
            Assert.Greater(kind.BelowLowestWaterMetres, 0f);
        }

        // ---- the placed ladders ----------------------------------------------------------------------

        sealed class PlacedLadder
        {
            public string Scene, Id, KindGuid;
            public float Top, Length, Rung, Standoff;
            public Vector2 Position;
        }

        static readonly Regex DocSplit = new Regex(@"(?m)(?=^--- !u!)");

        static string[] Docs(string scenePath) => DocSplit.Split(File.ReadAllText(scenePath).Replace("\r\n", "\n"));

        static float F(string doc, string field)
        {
            Match m = Regex.Match(doc, @"(?m)^  " + field + @": (.+)$");
            return m.Success ? float.Parse(m.Groups[1].Value.Trim(), CultureInfo.InvariantCulture) : 0f;
        }

        static Vector2 PositionOf(string[] docs, string goId)
        {
            string t = docs.Single(d => d.StartsWith("--- !u!4 ") && d.Contains("m_GameObject: {fileID: " + goId + "}"));
            Match m = Regex.Match(t, @"m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z");
            return new Vector2(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                               float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        static string LadderScriptGuid() =>
            Regex.Match(File.ReadAllText("Assets/_Project/Code/World/WharfLadder.cs.meta"), @"(?m)^guid: (\w+)").Groups[1].Value;

        static List<PlacedLadder> LaddersIn(string scenePath)
        {
            string guid = LadderScriptGuid();
            string[] docs = Docs(scenePath);
            var list = new List<PlacedLadder>();
            foreach (string d in docs.Where(d => d.Contains("guid: " + guid + ",")))
            {
                string go = Regex.Match(d, @"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;
                list.Add(new PlacedLadder
                {
                    Scene = Path.GetFileNameWithoutExtension(scenePath),
                    Id = Regex.Match(d, @"(?m)^  _id: (.+)$").Groups[1].Value.Trim(),
                    KindGuid = Regex.Match(d, @"(?m)^  _kind: \{fileID: 11400000, guid: (\w+)").Groups[1].Value,
                    Top = F(d, "_topElevationMeters"),
                    Length = F(d, "_lengthMetres"),
                    Rung = F(d, "_rungMetres"),
                    Standoff = F(d, "_standoffMetres"),
                    Position = PositionOf(docs, go),
                });
            }
            return list;
        }

        static IEnumerable<string> RegionScenes() =>
            Directory.GetFiles(ScenesFolder, "*.unity").Select(p => p.Replace('\\', '/')).OrderBy(p => p);

        static TideProfile RegionTide(string sceneName)
        {
            foreach (string path in Directory.GetFiles(RegionsFolder, "*.asset"))
            {
                var region = AssetDatabase.LoadAssetAtPath<RegionDef>(path.Replace('\\', '/'));
                if (region == null) continue;
                if (Path.GetFileNameWithoutExtension(path) != sceneName) continue;
                return Tide(region.TideMeanLevel, region.TideAmplitude);
            }
            Assert.Fail("no RegionDef named " + sceneName);
            return default;
        }

        [Test]
        public void EveryLadderOutsideThePendingRegions_IsBuiltToAKind_AndReachesBelowLowestWater()
        {
            WharfLadderDef kind = Kind();
            var failures = new List<string>();
            int checkedCount = 0;
            foreach (string scene in RegionScenes())
            {
                string name = Path.GetFileNameWithoutExtension(scene);
                if (PendingRegions.Contains(name)) continue;
                List<PlacedLadder> ladders = LaddersIn(scene);
                if (ladders.Count == 0) continue;
                TideProfile tide = RegionTide(name);
                foreach (PlacedLadder l in ladders)
                {
                    checkedCount++;
                    if (l.KindGuid != KindGuid) failures.Add($"{name}/{l.Id}: no ladder kind");
                    if (!WharfLadderReach.RungInReach(l.Rung, kind)) failures.Add($"{name}/{l.Id}: rung {l.Rung} out of reach");
                    if (Mathf.Abs(l.Rung - kind.RungMetres) > 1e-4f) failures.Add($"{name}/{l.Id}: rung {l.Rung} is not the kind's");
                    if (Mathf.Abs(l.Standoff - kind.StandoffMetres) > 1e-4f) failures.Add($"{name}/{l.Id}: standoff {l.Standoff}");
                    if (!WharfLadderReach.ReachesLowestWater(l.Top, l.Length, tide, kind))
                        failures.Add($"{name}/{l.Id}: foot {l.Top - l.Length:0.###} m is above the required " +
                                     $"{WharfLadderReach.RequiredFootElevation(tide, kind):0.###} m");
                }
            }
            Assert.Greater(checkedCount, 0, "St Peters carries a ladder");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void ThePendingList_HoldsOnlyRegionsThatStillHaveLaddersWithoutAKind()
        {
            foreach (string name in PendingRegions)
            {
                List<PlacedLadder> ladders = LaddersIn($"{ScenesFolder}/{name}.unity");
                Assert.IsTrue(ladders.Any(l => l.KindGuid != KindGuid),
                              $"{name}'s ladders are all built to a kind now: take it off the pending list");
            }
        }

        [Test]
        public void TheStPetersLadder_MeetsItsDeck_AndStandsClearOfTheFendersAndPiles()
        {
            WharfLadderDef kind = Kind();
            const string scene = ScenesFolder + "/StPeters.unity";
            string[] docs = Docs(scene);
            string deck = docs.Single(d => d.Contains("\n  _id: wharf.st_peters\n"));
            float deckElevation = F(deck, "_deckElevation");
            PlacedLadder ladder = LaddersIn(scene).Single();
            Assert.AreEqual(deckElevation, ladder.Top, kind.TopToDeckToleranceMetres, "the top is bolted to the deck");

            var gear = new List<(string name, Vector2 at)>();
            foreach (string d in docs.Where(d => d.StartsWith("--- !u!1 ")))
            {
                Match n = Regex.Match(d, @"(?m)^  m_Name: (Fitting_(tyre|pilehead|ladder))$");
                if (!n.Success) continue;
                string go = Regex.Match(d, @"^--- !u!1 &(\d+)").Groups[1].Value;
                gear.Add((n.Groups[1].Value, PositionOf(docs, go)));
            }
            Assert.AreEqual(1, gear.Count(g => g.name == "Fitting_ladder"), "one ladder picture");
            Assert.AreEqual(ladder.Position, gear.Single(g => g.name == "Fitting_ladder").at,
                            "the ladder you see is the ladder you climb");
            foreach (var g in gear.Where(g => g.name != "Fitting_ladder"))
                Assert.GreaterOrEqual(Vector2.Distance(g.at, ladder.Position), kind.FaceClearanceMetres,
                                      $"{g.name} at {g.at} crowds the ladder at {ladder.Position}");
        }

        [Test]
        public void TheStPetersLadderPicture_IsTheFullLengthArt_CutAtTheWaterline()
        {
            const string scene = ScenesFolder + "/StPeters.unity";
            string[] docs = Docs(scene);
            PlacedLadder ladder = LaddersIn(scene).Single();
            string go = docs.Where(d => d.StartsWith("--- !u!1 ") && d.Contains("\n  m_Name: Fitting_ladder\n"))
                            .Select(d => Regex.Match(d, @"^--- !u!1 &(\d+)").Groups[1].Value).Single();

            var art = AssetDatabase.LoadAssetAtPath<SetPieceDef>(HiddenHarbours.App.Editor.StPetersWharf.LandingLadderArtPath);
            Assert.IsNotNull(art);
            Sprite frame = art.SpriteFor(HiddenHarbours.App.Editor.StPetersWharf.LandingLadderFacingFrame);
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(frame, out string spriteGuid, out long spriteId));
            string renderer = docs.Single(d => d.StartsWith("--- !u!212 ") && d.Contains("m_GameObject: {fileID: " + go + "}"));
            StringAssert.Contains($"m_Sprite: {{fileID: {spriteId}, guid: {spriteGuid}, type: 3}}", renderer,
                                  "the visible ladder is the full-length landing art, not the 0.63 m kit slice");

            string faceGuid = Regex.Match(File.ReadAllText("Assets/_Project/Code/Art/TidalFaceWaterline.cs.meta"),
                                          @"(?m)^guid: (\w+)").Groups[1].Value;
            string face = docs.Single(d => d.Contains("m_GameObject: {fileID: " + go + "}") && d.Contains("guid: " + faceGuid + ","));
            Assert.AreEqual(ladder.Top, F(face, "_lipElevation"), 1e-4f, "the waterline is measured from the deck");
            Assert.AreEqual(ladder.Position.y, F(face, "_lipWorldY"), 1e-4f);
            StringAssert.Contains("_configured: 1", face);

            Assert.AreEqual(HiddenHarbours.App.Editor.StPetersWharf.FullLengthLadderMetres(), ladder.Length, 1e-4f,
                            "the climb's length is the drawn ladder's");
            StringAssert.Contains($"\"top\":{ladder.Top.ToString(CultureInfo.InvariantCulture)}", art.BakedOptionsJson,
                                  "the art was baked from this deck");
        }
    }
}
