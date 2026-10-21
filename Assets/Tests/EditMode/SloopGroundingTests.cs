using System.Reflection;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class SloopGroundingTests
    {
        sealed class Terrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.x < 0f ? -4f : -1f;
        }
        sealed class Sea : IEnvironmentService
        {
            public int WorldSeed => 12345;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double t) => 0.75f;
            public float WaterLevelAt(double t) => 0.75f;
        }

        [Test]
        public void RealDepth_FloatsEightyEightInFourPointSevenFiveMetres()
        {
            float depth = BoatCrossing.GroundingDepthAt(new Terrain(), new Sea(), 0,
                new Vector2(-1f, 0f), 3f, 0.75f);
            Assert.That(depth, Is.EqualTo(4.75f));
            Assert.That(depth >= 4.6f, Is.True, "the old flat 3.75 m falsely grounds the 4.6 m keel");
        }

        [Test]
        public void MovingOntoShoal_ChangesDepthOnTheNextRead()
        {
            var terrain = new Terrain(); var sea = new Sea();
            Assert.That(BoatCrossing.GroundingDepthAt(terrain, sea, 0, new Vector2(-1, 0), 3, .75f), Is.EqualTo(4.75f));
            Assert.That(BoatCrossing.GroundingDepthAt(terrain, sea, 0, new Vector2(1, 0), 3, .75f), Is.EqualTo(1.75f));
        }

        [Test]
        public void NoTerrain_PreservesArrivalFallbackAndTide()
        {
            Assert.That(BoatCrossing.GroundingDepthAt(null, new Sea(), 0, Vector2.zero, 4f, -.5f), Is.EqualTo(3.5f));
            Assert.That(BoatCrossing.GroundingDepthAt(new Terrain(), null, 0, Vector2.zero, 3f, .75f), Is.EqualTo(3.75f));
        }

        [Test]
        public void Controller_UsesRealDepthThenGroundsOnShoal()
        {
            var oldTerrain = GameServices.TidalTerrain; var oldSea = GameServices.Environment;
            var go = new GameObject("grounding probe");
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            try
            {
                GameServices.TidalTerrain = new Terrain(); GameServices.Environment = new Sea();
                hull.DraughtMeters = 4.6f;
                var boat = go.AddComponent<BoatController>(); boat.SetHull(hull);
                boat.enabled = false;
                var body = go.GetComponent<Rigidbody2D>(); body.position = new Vector2(-1f, 0f);
                boat.TickUnmannedDrift();
                Assert.That(boat.IsAground, Is.False, "e9a4ba47 reads the flat bed and falsely grounds here");
                body.position = new Vector2(1f, 0f); boat.TickUnmannedDrift();
                Assert.That(boat.IsAground, Is.True);
                GameServices.TidalTerrain = null; boat.SetLocalSeabedDepth(6f); boat.TickUnmannedDrift();
                Assert.That(boat.IsAground, Is.False, "the no-terrain runtime setter still works");
            }
            finally
            {
                GameServices.TidalTerrain = oldTerrain; GameServices.Environment = oldSea;
                Object.DestroyImmediate(go); Object.DestroyImmediate(hull);
            }
        }
    }
}
