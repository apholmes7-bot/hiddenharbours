using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters falling-tide WALKABILITY (P1): the on-foot fisher may only stand on ground the tide has
    /// bared, and a move into the water stops gently at the wading edge. Pure logic over fake
    /// terrain/environment doubles — no scene. Covers: the exposure rule, the falling tide opening more
    /// ground (the emerging sandbar) and a rising tide re-submerging it, the per-axis wading-edge stop
    /// (you can slide ALONG a shoreline), the never-trap rule (already in water → free to wade back), and
    /// the gate self-disabling when no terrain/tide is wired.
    /// </summary>
    public class TidalWalkabilityTests
    {
        /// <summary>A sloped beach: ground elevation rises with X (out toward land), so for a fixed water
        /// level there's a single shoreline — left of it is wet, right is dry. Deterministic, no RNG.</summary>
        private sealed class SlopeTerrain : ITidalTerrain
        {
            // elevation(x) = x * Slope  → x metres east is x*Slope m above datum.
            public float Slope = 1f;
            public float ElevationAt(Vector2 worldPos) => worldPos.x * Slope;
        }

        /// <summary>A still environment whose water level is a fixed metres-above-datum value.</summary>
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
        public void IsWalkable_ExposedGround_IsWalkable_SubmergedIsNot()
        {
            var terrain = new SlopeTerrain { Slope = 1f };
            var env = new FlatEnv { Level = 5f };

            // x=6 → ground 6.0 m, above the 5.0 m surface → exposed/walkable.
            Assert.IsTrue(TidalWalkability.IsWalkable(terrain, env, 0.0, new Vector2(6f, 0f)));
            // x=4 → ground 4.0 m, under the surface → submerged/not walkable.
            Assert.IsFalse(TidalWalkability.IsWalkable(terrain, env, 0.0, new Vector2(4f, 0f)));
        }

        [Test]
        public void FallingTide_ExposesMoreGround_RisingResubmerges()
        {
            var terrain = new SlopeTerrain { Slope = 1f };
            var env = new FlatEnv();
            var bar = new Vector2(4.5f, 0f);   // ground 4.5 m above datum (a point on the sandbar)

            env.Level = 5.0f;   // high water — the bar is under
            Assert.IsFalse(TidalWalkability.IsWalkable(terrain, env, 0.0, bar), "submerged at high water");

            env.Level = 4.0f;   // the tide falls past the bar — it bares (the sandbar path emerges)
            Assert.IsTrue(TidalWalkability.IsWalkable(terrain, env, 0.0, bar), "exposed once the tide falls");

            env.Level = 5.0f;   // the tide floods back — the path re-submerges
            Assert.IsFalse(TidalWalkability.IsWalkable(terrain, env, 0.0, bar), "submerged again on the flood");
        }

        [Test]
        public void NoTerrainOrNoEnvironment_GateOff_AlwaysWalkable()
        {
            var env = new FlatEnv { Level = 99f };
            Assert.IsTrue(TidalWalkability.IsWalkable(null, env, 0.0, Vector2.zero),
                "no height map (open water / non-tidal scene) → gate off, walk anywhere");
            Assert.IsTrue(TidalWalkability.IsWalkable(new SlopeTerrain(), null, 0.0, Vector2.zero),
                "no environment service → gate off (never trap the walker)");
        }

        // ---- the wading-edge resolve (pure) ---------------------------------------------------

        [Test]
        public void WadingEdge_StopsStepIntoWater_AlongShoreStillMoves()
        {
            // Walkable east of x=5 (dry land), submerged to the west (water). Probe 1 m.
            System.Func<Vector2, bool> walkable = p => p.x >= 5f;
            var origin = new Vector2(5f, 0f);  // standing right at the wading edge, on dry ground

            // Pushing WEST (into the water) at the edge → that component is cut to 0.
            var west = PlayerWalkController.ApplyWadingEdge(new Vector2(-3f, 0f), origin, walkable, 1f);
            Assert.AreEqual(0f, west.x, 1e-5f, "a step into the water stops at the wading edge");

            // Pushing EAST (onto dry land) → unimpeded.
            var east = PlayerWalkController.ApplyWadingEdge(new Vector2(3f, 0f), origin, walkable, 1f);
            Assert.AreEqual(3f, east.x, 1e-5f, "stepping further onto exposed ground is free");

            // Pushing NORTH (along the shore) → the along-shore component survives (axes gated independently).
            var along = PlayerWalkController.ApplyWadingEdge(new Vector2(-3f, 2f), origin, walkable, 1f);
            Assert.AreEqual(0f, along.x, 1e-5f, "the into-water X is cut");
            Assert.AreEqual(2f, along.y, 1e-5f, "but you can still slide ALONG the shoreline");
        }

        [Test]
        public void WadingEdge_WhenAlreadyInWater_DoesNotTrap()
        {
            System.Func<Vector2, bool> walkable = p => p.x >= 5f;
            var inWater = new Vector2(3f, 0f);   // somehow standing on submerged ground (tide rose under you)

            // Any move is allowed so the player can wade back toward dry ground (P5 forgiving).
            var v = PlayerWalkController.ApplyWadingEdge(new Vector2(-2f, 0f), inWater, walkable, 1f);
            Assert.AreEqual(-2f, v.x, 1e-5f, "never trap a walker who's already in the water");
        }

        [Test]
        public void WadingEdge_NullProbe_PassesThrough()
        {
            var v = PlayerWalkController.ApplyWadingEdge(new Vector2(1f, 2f), Vector2.zero, null, 1f);
            Assert.AreEqual(new Vector2(1f, 2f), v, "a null walkability probe disables the gate");
        }
    }
}
