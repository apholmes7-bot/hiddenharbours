using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public class PainterRestraintPlayTests
    {
        private readonly List<Object> _objects = new();
        private sealed class Coast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.x < 0f ? -4f : 5f;
        }
        private sealed class Sea : IEnvironmentService
        {
            public int WorldSeed => 17;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double time) => 0f;
            public float WaterLevelAt(double time) => 0f;
        }

        private (BoatMooring line, Rigidbody2D body, PlayerWalkController walk, HeldWalkIntents input) MakeRig()
        {
            GameServices.Reset();
            GameServices.Environment = new Sea();
            GameServices.TidalTerrain = new Coast();
            var hullGo = new GameObject("Hull");
            _objects.Add(hullGo);
            hullGo.transform.position = new Vector3(-2f, 0f);
            var boat = hullGo.AddComponent<BoatController>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _objects.Add(hull);
            hull.PainterLengthMeters = 4f;
            boat.SetHull(hull);
            boat.enabled = false;
            hullGo.GetComponent<Collider2D>().enabled = false;

            var player = new GameObject("Walker");
            _objects.Add(player);
            var walk = player.AddComponent<PlayerWalkController>();
            var input = new HeldWalkIntents();
            walk.ConfigureWalkInput(input);
            var line = hullGo.GetComponent<BoatMooring>();
            var body = hullGo.GetComponent<Rigidbody2D>();
            line.Hold(player.transform);
            return (line, body, walk, input);
        }

        [UnityTest]
        public IEnumerator InlandWalk_HoldsAtThePainter_EveryTick_ThenDropFreesThePlayer()
        {
            var (line, body, walk, input) = MakeRig();
            Transform player = walk.transform;
            input.Walk(Vector2.right);
            yield return null;

            float elapsed = 0f;
            while (elapsed < 5f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                Assert.LessOrEqual(Vector2.Distance(player.position, body.position), 4.0001f);
                Assert.Less(body.position.x, 0f, "the hull stays in water on every physics tick");
            }
            Assert.AreEqual(2f, player.position.x, 0.001f, "the inland walk stops at four metres");
            Assert.AreEqual(-2f, body.position.x, 0.001f, "walking does not tow the boat");

            line.Root(player.position);
            elapsed = 0f;
            while (elapsed < 2f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
            Assert.Greater(player.position.x, 7f, "dropping the painter frees the walk");
            Assert.Less(body.position.x, 0f);
        }

        [UnityTest]
        public IEnumerator AShorewardShove_CannotPushTheHeldHullAboveTheWaterline()
        {
            var (line, body, walk, input) = MakeRig();
            yield return null;
            float elapsed = 0f;
            while (elapsed < 2f)
            {
                body.linearVelocity = new Vector2(20f, 0f);
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                Assert.Less(body.position.x, 0f, "check the hull after every live physics step");
                Assert.LessOrEqual(Vector2.Distance(walk.transform.position, body.position), 4.0001f);
            }
            Assert.Greater(body.position.x, -1f, "the shove moved her before the shoreline checked her");
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _objects) if (o != null) Object.Destroy(o);
            _objects.Clear();
        }
    }
}
