using System;
using System.IO;
using System.Linq;
using HiddenHarbours.World.Golf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.World.EditMode
{
    public class GolfContentTests
    {
        const string Root = "Assets/_Project/Data/Golf/";
        const string PlanPath = "Assets/_Project/Data/Terrain/CumberlandGround/plan.json";
        [Serializable] sealed class Plan { public Course course; public Region region; }
        [Serializable] sealed class Region { public string id; }
        [Serializable] sealed class Course { public string id; public HoleRow[] holes; }
        [Serializable] sealed class HoleRow { public string id; public int n, par; public float[] tee, green; public float green_ground; }

        [Test] public void EveryGolfAssetBuilds_AndInventoryIsExact()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root.TrimEnd('/') });
            Assert.That(guids.Length, Is.EqualTo(15));
            int holes = 0, clubs = 0, surfaces = 0, rules = 0;
            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                switch (asset)
                {
                    case GolfHoleDef hole: Assert.DoesNotThrow(() => hole.Build(), hole.name); holes++; break;
                    case GolfClubDef club: Assert.DoesNotThrow(() => club.Build(), club.name); clubs++; break;
                    case GolfSurfaceDef surface: Assert.DoesNotThrow(() => surface.Build(), surface.name); surfaces++; break;
                    case GolfRulesDef rule: Assert.DoesNotThrow(() => rule.Build(), rule.name); rules++; break;
                    default: Assert.Fail("Unexpected golf content type: " + asset); break;
                }
            }
            Assert.That(holes, Is.EqualTo(4)); Assert.That(clubs, Is.EqualTo(4));
            Assert.That(surfaces, Is.EqualTo(6)); Assert.That(rules, Is.EqualTo(1));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void CompleteCumberlandHolesReuseSourceIdentityAndGroundMetres(int number)
        {
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            var source = plan.course.holes.Single(h => h.n == number);
            var def = AssetDatabase.LoadAssetAtPath<GolfHoleDef>(Root + "CumberlandHole" + number.ToString("00") + ".asset");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.Id, Is.EqualTo(source.id)); Assert.That(def.CourseId, Is.EqualTo(plan.course.id));
            Assert.That(def.RegionId, Is.EqualTo(plan.region.id)); Assert.That(def.Number, Is.EqualTo(source.n));
            Assert.That(def.Par, Is.EqualTo(source.par));
            Assert.That(def.TeePosition, Is.EqualTo(new Vector2(source.tee[0], source.tee[1])));
            Assert.That(def.CupPosition, Is.EqualTo(new Vector2(source.green[0], source.green[1])));
            Assert.That(def.Elevation, Is.EqualTo(source.green_ground));
            Assert.That(def.Hazards.Length, Is.EqualTo(13));
            CollectionAssert.IsSubsetOf(new[] { "pond.cmb_brook_pond", "pond.cmb_course_west", "pond.cmb_course_east", "stream.cmb_brook" },
                def.Hazards.Select(h => h.Id).ToArray());
        }

        [Test] public void IncompleteSourceHolesAreNotInvented_AndPracticeRoundIsFourHoles()
        {
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, plan.course.holes.Where(h => h.tee != null && h.green != null).Select(h => h.n));
            Assert.That(AssetDatabase.LoadAssetAtPath<GolfHoleDef>(Root + "CumberlandHole05.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GolfHoleDef>(Root + "CumberlandHole18.asset"), Is.Null);
            GolfHole[] holes = Enumerable.Range(1, 4).Select(n =>
                AssetDatabase.LoadAssetAtPath<GolfHoleDef>(Root + "CumberlandHole" + n.ToString("00") + ".asset").Build()).ToArray();
            var rules = AssetDatabase.LoadAssetAtPath<GolfRulesDef>(Root + "PracticeRules.asset").Build();
            var round = new GolfRound(holes, rules);
            Assert.That(round.CurrentHole.Id, Is.EqualTo("hole.cmb_1"));
            Assert.That(round.BallPosition, Is.EqualTo(holes[0].TeePosition));
            Assert.That(round.TotalStrokes, Is.Zero); Assert.That(round.Complete, Is.False);
        }

        [Test] public void AuthoredClubsResolveAcrossAllCompleteHolesPowerLevelsAndWindSamples()
        {
            var rules = AssetDatabase.LoadAssetAtPath<GolfRulesDef>(Root + "PracticeRules.asset").Build();
            int executed = 0;
            foreach (int number in new[] { 1, 2, 3, 4 })
            {
                var hole = AssetDatabase.LoadAssetAtPath<GolfHoleDef>(Root + "CumberlandHole" + number.ToString("00") + ".asset").Build();
                foreach (string name in new[] { "Driver", "Iron", "Wedge", "Putter" })
                {
                    var club = AssetDatabase.LoadAssetAtPath<GolfClubDef>(Root + name + ".asset").Build();
                    foreach (float power in new[] { 0.25f, 0.5f, 1f })
                        foreach (Vector2 wind in new[] { Vector2.zero, new Vector2(-8, 0), new Vector2(0, 8) })
                        {
                            var result = GolfSimulation.Shoot(hole, club, rules, hole.TeePosition,
                                new GolfSwing(hole.CupPosition - hole.TeePosition, power), new GolfShotContext(27, 3600, 0, wind));
                            Assert.That(result.Status, Is.Not.EqualTo(GolfShotStatus.Unresolved), hole.Id + " " + name + " " + power + " " + wind);
                            Assert.That(float.IsNaN(result.Position.x) || float.IsNaN(result.Position.y), Is.False);
                            executed++;
                        }
                }
            }
            Assert.That(executed, Is.EqualTo(144));
        }
    }
}
