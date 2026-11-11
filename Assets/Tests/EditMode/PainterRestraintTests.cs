using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace HiddenHarbours.Tests.EditMode
{
    public class PainterRestraintTests
    {
        private readonly List<Object> _objects = new();

        private sealed class Coast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.x < 0f ? -1f : 5f;
        }

        private sealed class Sea : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 17;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double time) => Level;
            public float WaterLevelAt(double time) => Level;
        }

        private sealed class Cleat : IMooringCleat
        {
            public string Id => "cleat.test";
            public CleatSide Side { get; set; }
            public Vector2 WorldPosition => Vector2.zero;
            public float ElevationMeters => 0f;
        }

        private (BoatMooring line, BoatHullDef hull, Rigidbody2D body, PlayerWalkController walker) Rig()
        {
            var go = new GameObject("Hull");
            _objects.Add(go);
            go.transform.position = new Vector3(-2f, 0f);
            var boat = go.AddComponent<BoatController>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _objects.Add(hull);
            hull.PainterLengthMeters = 4f;
            boat.SetHull(hull);
            boat.enabled = false;
            var player = new GameObject("Walker");
            _objects.Add(player);
            var walker = player.AddComponent<PlayerWalkController>();
            var line = go.GetComponent<BoatMooring>();
            line.Hold(player.transform);
            return (line, hull, go.GetComponent<Rigidbody2D>(), walker);
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [TestCase(0f, 0f)]
        [TestCase(211f, 0f)]
        [TestCase(130f, 211f)]
        public void WalkingWithThePainter_StopsTheHandWithoutMovingTheHull(float x, float y)
        {
            var r = Rig();
            Vector2 start = r.body.position;
            Assert.AreSame(r.line, r.walker.MovementConstraint);
            Vector2 held = r.walker.MovementConstraint.ConstrainWalk(new Vector2(x, y));
            Assert.LessOrEqual(Vector2.Distance(start, held), 4.00001f);
            Assert.AreEqual(start, r.body.position);
            if (x == 211f && y == 0f) Assert.AreEqual(new Vector2(2f, 0f), held);
        }

        [Test]
        public void PainterLength_ComesFromTheHullDef()
        {
            var r = Rig();
            r.hull.PainterLengthMeters = 7f;
            Assert.AreEqual(new Vector2(5f, 0f), r.line.ConstrainWalk(new Vector2(211f, 0f)));
        }

        [Test]
        public void DroppingThePainter_FreesTheWalkerAndKeepsTheGroundTie()
        {
            var r = Rig();
            r.line.Root(r.walker.transform.position);
            Assert.IsNull(r.walker.MovementConstraint);
            Assert.AreEqual(MooringState.RootedToGround, r.line.State);
            Assert.AreEqual(Vector2.zero, r.line.TiePoint);
            Assert.AreEqual(new Vector2(211f, 0f), r.line.ConstrainWalk(new Vector2(211f, 0f)));
        }

        [Test]
        public void MakingFast_FreesTheWalkerAndRetainsTheCleatContract()
        {
            var r = Rig();
            var boat = new Cleat { Side = CleatSide.Boat };
            var shore = new Cleat { Side = CleatSide.Shore };
            Assert.IsTrue(r.line.MakeFast(boat, shore, 9f));
            Assert.IsNull(r.walker.MovementConstraint);
            Assert.AreSame(boat, r.line.BoatCleat);
            Assert.AreSame(shore, r.line.ShoreCleat);
            Assert.AreEqual(MooringState.MadeFastToCleat, r.line.State);
        }

        [Test]
        public void Reboarding_ReleasesTheWalker()
        {
            var r = Rig();
            r.line.Stow();
            Assert.IsNull(r.walker.MovementConstraint);
            Assert.AreEqual(MooringState.Stowed, r.line.State);
        }

        [TestCase("Assets/_Project/Scenes/StPeters.unity", 5)]
        // NMC H5 (#1039) retired main's 23 (14 bollards, 1 cleat, 8 on the one retired float) and authored 83: 19 cap
        // ShoreCleats (10 concrete, 6 crib, 3 west apron) + 64 FloatCleats (8 west float, 16+16 fingers A/B, 8 east rec
        // float, 16 bow/stern on the eight finger berths).
        [TestCase("Assets/_Project/Scenes/NineMileCreek.unity", 83)]
        public void EveryAuthoredWharfFitting_StillAcceptsAMadeFastLine(string path, int expected)
        {
            var r = Rig();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var fittings = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true))
                    .OfType<IMooringCleat>().Where(c => c.Side == CleatSide.Shore).ToArray();
                Assert.AreEqual(expected, fittings.Length, "exercise every authored fitting, including float cleats");
                foreach (var fitting in fittings)
                {
                    r.line.Hold(r.walker.transform);
                    Assert.IsTrue(r.line.MakeFast(new Cleat { Side = CleatSide.Boat }, fitting, 9f), fitting.Id);
                    Assert.AreSame(fitting, r.line.ShoreCleat, fitting.Id);
                    Assert.IsNull(r.walker.MovementConstraint, fitting.Id);
                    Assert.IsTrue(r.line.CastOff(), fitting.Id);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void AfloatHull_CannotMoveAboveTheWaterline()
        {
            Vector2 at = new(-1f, 0f);
            Assert.AreEqual(at, BoatMooring.ConstrainToWater(new Coast(), new Sea(), 0d,
                at, new Vector2(1f, 0f), 0.3f));
            Assert.AreEqual(new Vector2(-2f, 0f), BoatMooring.ConstrainToWater(new Coast(), new Sea(), 0d,
                at, new Vector2(-2f, 0f), 0.3f));
        }

        [Test]
        public void EbbMayLeaveHerAground_WithoutMovingHerOrAllowingHigherGround()
        {
            Vector2 at = new(-1f, 0f);
            var sea = new Sea { Level = -2f };
            Assert.AreEqual(at, BoatMooring.ConstrainToWater(new Coast(), sea, 0d, at, at, 0.3f));
            Assert.AreEqual(at, BoatMooring.ConstrainToWater(new Coast(), sea, 0d,
                at, new Vector2(1f, 0f), 0.3f));
            Assert.AreEqual(at, BoatMooring.ConstrainToWater(new Coast(), sea, 0d,
                at, new Vector2(-2f, 0f), 0.3f), "the tide leaves her in place, without sliding over dry flats");
        }
    }
}
