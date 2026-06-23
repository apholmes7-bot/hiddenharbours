using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters BOAT-CROSS gating (P1/P5): a hull may pass only where the water is deeper than its
    /// draught, so the sandbar channel is crossable at higher tide and closes as the tide falls — the
    /// inverse of the on-foot walkability gate, over the SAME water level + ground elevation. Non-punishing:
    /// it eases the hull to a stop at the shallows (no grounding/damage). Pure logic over fakes — no scene.
    /// </summary>
    public class BoatCrossingTests
    {
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        [Test]
        public void DepthAt_IsWaterLevelMinusGround()
        {
            var terrain = new FlatTerrain { Elevation = 2f };
            var env = new FlatEnv { Level = 5f };
            Assert.AreEqual(3f, BoatCrossing.DepthAt(terrain, env, 0.0, Vector2.zero), 1e-5f);
        }

        [Test]
        public void CanFloat_ChannelOpensAtHighTide_ClosesAsItFalls()
        {
            var terrain = new FlatTerrain { Elevation = 2f };  // a channel sill at 2.0 m above datum
            var env = new FlatEnv();
            const float draught = 0.3f;                        // the dory

            env.Level = 2.5f;   // 0.5 m over the sill ≥ 0.3 draught → crossable
            Assert.IsTrue(BoatCrossing.CanFloat(terrain, env, 0.0, Vector2.zero, draught),
                "at higher water the channel is deep enough to float the dory");

            env.Level = 2.1f;   // only 0.1 m over the sill < 0.3 draught → too shallow
            Assert.IsFalse(BoatCrossing.CanFloat(terrain, env, 0.0, Vector2.zero, draught),
                "as the tide falls the channel closes to the boat (inverse of the walk path opening)");
        }

        [Test]
        public void NoTerrain_IsOpenWater_AlwaysFloats()
        {
            var env = new FlatEnv { Level = 0f };
            Assert.IsTrue(float.IsPositiveInfinity(BoatCrossing.DepthAt(null, env, 0.0, Vector2.zero)),
                "no height map → open water → infinite depth");
            Assert.IsTrue(BoatCrossing.CanFloat(null, env, 0.0, Vector2.zero, 6.5f),
                "even the deepest hull passes in open water (no shallows to gate)");
        }

        // ---- the non-punishing shallows hold (pure force) -------------------------------------

        [Test]
        public void ShallowsHold_OpposesOnlyIntoShallowsVelocity()
        {
            // Heading +X into shallow water ahead. The hold pushes back (-X).
            var into = BoatController.ShallowsHoldForce(new Vector2(2f, 0f), aheadShallow: true,
                towardShallow: new Vector2(1f, 0f), holdDrag: 100f);
            Assert.Less(into.x, 0f, "a boat driving into the shallows is held back");
            Assert.AreEqual(0f, into.y, 1e-5f, "the hold acts only along the into-shallows axis");
        }

        [Test]
        public void ShallowsHold_DoesNotImpedeRetreat()
        {
            // Moving AWAY from the shallows (-X) while the shallows are ahead (+X): no opposing force.
            var retreat = BoatController.ShallowsHoldForce(new Vector2(-2f, 0f), aheadShallow: true,
                towardShallow: new Vector2(1f, 0f), holdDrag: 100f);
            Assert.AreEqual(Vector2.zero, retreat, "you can always back out of the shallows (P5 forgiving)");
        }

        [Test]
        public void ShallowsHold_DeepWater_NoForce()
        {
            var deep = BoatController.ShallowsHoldForce(new Vector2(2f, 0f), aheadShallow: false,
                towardShallow: new Vector2(1f, 0f), holdDrag: 100f);
            Assert.AreEqual(Vector2.zero, deep, "deep enough ahead → no hold, the boat passes freely");
        }
    }
}
