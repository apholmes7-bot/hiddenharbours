using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The beached trawler at Rice Point: one wreck, as data, lying on her side on the shore above
    /// the low-tide line, with a line of text when the player looks at her and a promise that she
    /// can be repaired later. These tests also hold the line on what she is NOT: nothing reads the
    /// promise yet, so there is no repair loop, no campaign step and no owner.
    /// </summary>
    public sealed class RicePointBeachedTrawlerTests
    {
        const string WreckPath = "Assets/_Project/Data/Wrecks/RicePointSternTrawler.asset";
        const string OldLayoutHullId = "boat.stern_trawler";
        const float MinimumHeelDegrees = 30f, SampleStepMeters = 0.5f;

        static WreckDef Wreck() => AssetDatabase.LoadAssetAtPath<WreckDef>(WreckPath);
        static float LowestTide(LandScenePlanDef plan) => plan.Region.TideMeanLevel - plan.Region.TideAmplitude;
        static float HighestTide(LandScenePlanDef plan) => plan.Region.TideMeanLevel + plan.Region.TideAmplitude;

        [Test]
        public void ThereIsOneWreckAtRicePoint_WithAStableId_AndSheIsTheOldLayoutSternTrawler()
        {
            var plan = RicePointBuilder.LoadPlan();
            var wrecks = RicePointWrecks.For(plan.Region.Id);
            Assert.That(wrecks.Select(w => w.Id), Is.EqualTo(new[] { "wreck.rpt_stern_trawler" }));
            var wreck = Wreck();
            Assert.That(wreck, Is.SameAs(wrecks[0]));
            Assert.That(wreck.IsPresentable, Is.True, "she needs a hull with a picture");
            Assert.That(wreck.Hull.Id, Is.EqualTo(OldLayoutHullId));
            Assert.That(wreck.DisplayName, Is.Not.Empty);
        }

        [Test]
        public void SheLiesOnHerSide()
        {
            Assert.That(Mathf.Abs(Wreck().HeelDegrees), Is.GreaterThanOrEqualTo(MinimumHeelDegrees),
                "a wreck on her side is heeled well over, not sitting upright");
        }

        [Test]
        public void SheLiesOnTheShore_AboveTheLowTideLine_AlongHerWholeLength()
        {
            var plan = RicePointBuilder.LoadPlan();
            var wreck = Wreck();
            var field = plan.Ground.Field;
            Vector2 along = wreck.Along, across = new Vector2(along.y, -along.x);
            Vector2 half = BeachedWreck.SolidSize(wreck) * 0.5f;
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            for (float a = -half.y; a <= half.y; a += SampleStepMeters)
                for (float c = -half.x; c <= half.x; c += SampleStepMeters)
                {
                    float ground = field.ElevationAt(wreck.Position + along * a + across * c);
                    lowest = Mathf.Min(lowest, ground);
                    highest = Mathf.Max(highest, ground);
                }
            Assert.That(lowest, Is.GreaterThan(LowestTide(plan)),
                $"the lowest ground under her ({lowest:F2} m) must be dry at the lowest tide ({LowestTide(plan):F2} m)");
            Assert.That(highest, Is.LessThan(HighestTide(plan)),
                $"she is on the shore the sea reaches, not up on the land ({highest:F2} m is above the highest tide)");
        }

        [Test]
        public void SheIsClearOfTheTrawlerBerths_AndTheWayIn()
        {
            var wreck = Wreck();
            wreck.KeelLine(out Vector2 stern, out Vector2 bow);
            float reach = BeachedWreck.SolidSize(wreck).x * 0.5f;
            foreach (string guid in AssetDatabase.FindAssets("t:BerthDef"))
            {
                var berth = AssetDatabase.LoadAssetAtPath<BerthDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (berth.RegionId != wreck.RegionId) continue;
                var line = new[] { berth.Stern, berth.Bow }.Concat(berth.Approach).ToArray();
                float clear = Mathf.Max(berth.BeamMeters * 0.5f, berth.ApproachHalfWidthMeters) + reach;
                foreach (Vector2 p in line)
                    Assert.That(DistanceToSegment(p, stern, bow), Is.GreaterThan(clear), berth.Id + " is fouled by the wreck");
            }
        }

        [Test]
        public void ThePromiseIsOnlyData_SheCanBeRepairedLater_AndNothingReadsItYet()
        {
            var wreck = Wreck();
            Assert.That(wreck.RepairableLater, Is.True);
            Assert.That(wreck.Promise, Is.Not.Empty);
            var readers = Directory.GetFiles("Assets/_Project/Code", "*.cs", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f) != "WreckDef.cs" && File.ReadAllText(f).Contains(nameof(WreckDef.RepairableLater)))
                .ToArray();
            Assert.That(readers, Is.Empty,
                "no repair loop, campaign step or ownership is built yet; when one is, this test changes with it on purpose");
        }

        [Test]
        public void LookingAtHer_SpeaksALine_FromData()
        {
            var npc = RicePointWrecks.LookNpc(Wreck());
            Assert.That(npc, Is.Not.Null, "the wreck names a thing-to-read that exists");
            Assert.That(npc.Kind, Is.EqualTo(InteractKind.Read));
            Assert.That(npc.Dialogue, Is.Not.Null);
            Assert.That(npc.Dialogue.FirstLines, Is.Not.Empty);
            Assert.That(npc.Dialogue.FirstLines.All(l => !string.IsNullOrWhiteSpace(l)), Is.True);
            Assert.That(npc.CompletionFlag, Is.Empty, "looking at her sets nothing: no quest or campaign step hangs off her yet");
        }

        [Test]
        public void TheSceneHoldsHerOnce_WhereTheDefSays_SolidWithAPlaceToLookFromEachSide()
        {
            var plan = RicePointBuilder.LoadPlan();
            var wreck = Wreck();
            var scene = EditorSceneManager.OpenScene(plan.ScenePath, OpenSceneMode.Single);
            var placed = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BeachedWreck>(true)).ToArray();
            Assert.That(placed.Length, Is.EqualTo(1));
            var she = placed[0];
            Assert.That(she.Wreck, Is.SameAs(wreck));
            Assert.That(she.name, Is.EqualTo(wreck.Id));
            Assert.That((Vector2)she.transform.position, Is.EqualTo(wreck.Position));
            Assert.That(Vector2.Angle(she.transform.up, wreck.Along), Is.LessThan(0.01f), "her bow lies on the Def's bearing");
            var solid = she.GetComponent<BoxCollider2D>();
            Assert.That(solid, Is.Not.Null);
            Assert.That(solid.size, Is.EqualTo(BeachedWreck.SolidSize(wreck)));
            Assert.That(solid.isTrigger, Is.False);

            var looks = she.GetComponentsInChildren<Interactable>(true);
            Assert.That(looks.Length, Is.EqualTo(2));
            var npc = RicePointWrecks.LookNpc(wreck);
            var field = plan.Ground.Field;
            foreach (var look in looks)
            {
                Assert.That(look.Npc, Is.SameAs(npc));
                Vector2 local = she.transform.InverseTransformPoint(look.transform.position);
                Assert.That(Mathf.Abs(local.x), Is.GreaterThan(solid.size.x * 0.5f), "you stand outside her to look");
                Assert.That(Mathf.Abs(local.x) - solid.size.x * 0.5f, Is.LessThan(RicePointWrecks.LookRadiusMetres),
                    "and near enough that the look reaches from her side");
                Assert.That(field.ElevationAt(look.transform.position), Is.GreaterThan(LowestTide(plan)), "on ground that dries");
            }
            var interactor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldInteractor>(true)).Single();
            var known = new SerializedObject(interactor).FindProperty("_interactables");
            var listed = Enumerable.Range(0, known.arraySize).Select(i => known.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            Assert.That(looks.All(l => listed.Contains(l)), Is.True, "the region's interactor knows both look points");
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
