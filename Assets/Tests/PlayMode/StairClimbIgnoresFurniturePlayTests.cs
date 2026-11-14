using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Furniture can never cancel or deflect a stair climb</b> (owner rule, 2026-11-14), proved with
    /// REAL physics: a dynamic walker body, static furniture colliders standing on the climb path, and the
    /// fixed-step solver running for the whole walk.
    ///
    /// <para><b>The fault these guard.</b> <see cref="BuildingInterior.TryGoToLevel"/> swaps the storey —
    /// and with it the destination floor's furniture colliders — BEFORE the body is carried. The climb
    /// writes the transform; a dynamic body written into a chair is depenetrated by the next physics step;
    /// the climb's ownership test reads that push as somebody else moving the fisher and abandons the
    /// walk. In St Peters' sageCottage the descent stopped ~1.6 m short of its landing (the census on PR
    /// 1198). Every assertion here would fail against that code: the body would end partway.</para>
    ///
    /// <para><b>A tiny synthetic house</b>, built from this file's own numbers the way
    /// <see cref="CabinUpstairsPlayTests"/> builds one: the house walls, a storey above, and furniture
    /// placed exactly where the climb must pass. No baked art, no region scene.</para>
    /// </summary>
    public class StairClimbIgnoresFurniturePlayTests
    {
        const string SceneName = "StairClimbFurniture_Test";

        const float FloorWidth = 6.6f;
        const float FloorLength = 8.05f;
        const float WallThickness = 0.3f;
        const float DoorwayWidth = 1.4f;
        const float GroundDepthScale = 0.6427876f;
        const float StoreyHeightMetres = 3.1025f;

        /// <summary>Where the stairwell stands, in the room's model frame — clear of every wall.</summary>
        static readonly Vector2 StairPoint = new Vector2(1.5f, -0.9f);

        /// <summary>The walker's own collider — a fisher-sized circle.</summary>
        const float BodyRadius = 0.25f;

        /// <summary>A chair across the climb path: wide, shallow, centred on the path's midpoint.</summary>
        static readonly Vector2 PathChairSize = new Vector2(1.2f, 0.6f);

        static readonly Vector3 CottageGround = new Vector3(-40f, 30f, 0f);

        Scene _origin, _scene;
        GameObject _cottage, _player, _groundProps, _upperProps;
        BuildingInterior _interior;
        Rigidbody2D _body;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _origin = SceneManager.GetActiveScene();
            _scene = SceneManager.CreateScene(SceneName);
            SceneManager.SetActiveScene(_scene);
            Interactables.Clear();
            MoveActionClaim.Reset();

            _player = new GameObject("walker");
            SceneManager.MoveGameObjectToScene(_player, _scene);
            _player.transform.position = CottageGround + new Vector3(0f, -20f, 0f);
            _body = _player.AddComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _player.AddComponent<CircleCollider2D>().radius = BodyRadius;

            StandCottage();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Interactables.Clear();
            MoveActionClaim.Reset();
            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            GameServices.Reset();
        }

        void StandCottage()
        {
            _cottage = new GameObject("cottage");
            _cottage.SetActive(false);
            SceneManager.MoveGameObjectToScene(_cottage, _scene);
            _cottage.transform.position = CottageGround;

            var shell = ChildGo("Shell").AddComponent<SpriteRenderer>();
            var ground = ChildGo("Interior").AddComponent<SpriteRenderer>();
            var upper = ChildGo("InteriorUpper").AddComponent<SpriteRenderer>();
            _groundProps = ChildGo("Furniture");
            _upperProps = ChildGo("FurnitureUpper");
            var upperWalls = ChildGo("WallsUpper");

            _interior = _cottage.AddComponent<BuildingInterior>();
            _interior.Configure(shell, ground, _groundProps.transform, FloorWidth, FloorLength, 0, 8,
                                GroundDepthScale, WallThickness, DoorwayWidth);
            _interior.SetOccupant(_player.transform);

            var walls = ChildGo("Walls");
            foreach (Vector2[] quad in _interior.Footprint.WallQuads(WallThickness, DoorwayWidth))
            {
                var c = walls.AddComponent<PolygonCollider2D>();
                c.pathCount = 1;
                var local = new Vector2[quad.Length];
                for (int i = 0; i < quad.Length; i++) local[i] = quad[i] - (Vector2)walls.transform.position;
                c.SetPath(0, local);
            }

            _interior.ConfigureUpperLevel(upper, _upperProps.transform, upperWalls.transform,
                                          InteriorLevelLayout.UpperLevelY(StoreyHeightMetres));
            _cottage.SetActive(true);
        }

        GameObject ChildGo(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_cottage.transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        /// <summary>A static piece of furniture: a box collider and nothing else, parented under one
        /// storey's furniture root so it is solid exactly when that storey is.</summary>
        static GameObject Chair(GameObject storeyProps, Vector2 world, Vector2 size)
        {
            var go = new GameObject("chair");
            go.transform.SetParent(storeyProps.transform, worldPositionStays: false);
            go.transform.position = new Vector3(world.x, world.y, 0f);
            go.AddComponent<BoxCollider2D>().size = size;
            return go;
        }

        Vector2 Foot => _interior.Footprint.ModelToWorld(StairPoint);
        Vector2 Rise => new Vector2(0f, _interior.UpperLevelY);

        IEnumerator StandAt(Vector2 world)
        {
            _player.transform.position = new Vector3(world.x, world.y, 0f);
            _body.position = world;
            _body.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        IEnumerator WalkInToTheFoot()
        {
            yield return StandAt(_interior.Footprint.ModelToWorld(Vector2.zero));
            Assert.IsTrue(_interior.IsInside, "precondition: indoors");
            yield return StandAt(Foot);
            Assert.IsTrue(_interior.IsInside, "precondition: still indoors at the foot of the stairs");
        }

        /// <summary>Bounded by the clock, never by a frame count (see CabinUpstairsPlayTests).</summary>
        IEnumerator WaitForTheClimb()
        {
            float deadline = Time.time + GameServices.StairClimbSeconds * 4f + 1f;
            while (_interior.IsClimbing && Time.time < deadline) yield return null;
            Assert.IsFalse(_interior.IsClimbing, "the climb finishes on its own");
            yield return new WaitForFixedUpdate();   // the restored dynamic body gets a physics step
            yield return null;
        }

        void AssertBodyRestored(string when)
        {
            Assert.AreEqual(RigidbodyType2D.Dynamic, _body.bodyType, $"{when}: the body is dynamic again");
            Assert.IsTrue(_body.simulated, $"{when}: and still simulated");
            Assert.AreEqual(Vector2.zero, _body.linearVelocity, $"{when}: and carries no velocity");
            Assert.IsFalse(MoveActionClaim.IsClaimed, $"{when}: and the move keys are the player's");
        }

        int SolidOverlaps()
        {
            Physics2D.SyncTransforms();
            var hits = new List<Collider2D>();
            _body.Overlap(new ContactFilter2D { useTriggers = false }, hits);
            hits.RemoveAll(c => c == null || c.attachedRigidbody == _body);
            return hits.Count;
        }

        // =============================================================================

        [UnityTest]
        public IEnumerator FurnitureOnThePath_NeitherCancelsNorDeflects_TheClimb_UpOrDown()
        {
            yield return WalkInToTheFoot();
            Vector2 foot = _body.position;

            // One chair on EACH storey, both standing on the midpoint of the climb. The upstairs one is
            // solid during the ascent (the storey swaps first), the downstairs one during the descent —
            // which is the sageCottage case exactly.
            Vector2 midpoint = foot + Rise * 0.5f;
            Chair(_upperProps, midpoint, PathChairSize);
            Chair(_groundProps, midpoint, PathChairSize);

            Assert.IsTrue(_interior.TryGoToLevel(InteriorLevelLayout.UpperLevel), "the climb starts");
            Assert.AreEqual(RigidbodyType2D.Kinematic, _body.bodyType,
                "for the walk the body is out of the physics argument — nothing can push it");
            yield return WaitForTheClimb();

            Assert.AreEqual(InteriorLevelLayout.UpperLevel, _interior.Level);
            Assert.AreEqual(foot.x, _body.position.x, 0.01f, "the ascent was not deflected sideways");
            Assert.AreEqual(foot.y + Rise.y, _body.position.y, 0.01f,
                "and arrived exactly one storey up, through a chair on the path");
            AssertBodyRestored("after the ascent");

            Assert.IsTrue(_interior.TryGoToLevel(InteriorLevelLayout.GroundLevel), "the descent starts");
            yield return WaitForTheClimb();

            Assert.AreEqual(InteriorLevelLayout.GroundLevel, _interior.Level);
            Assert.AreEqual(foot.x, _body.position.x, 0.01f, "the descent was not deflected sideways");
            Assert.AreEqual(foot.y, _body.position.y, 0.01f,
                "and reached its landing — the sageCottage descent stopped ~1.6 m short here");
            Assert.IsTrue(_interior.IsInside, "still inside the house");
            AssertBodyRestored("after the descent");
        }

        [UnityTest]
        public IEnumerator ALandingInsideFurniture_ResolvesToTheNearestClearPoint_AndDoesNotTrapThePlayer()
        {
            yield return WalkInToTheFoot();
            Vector2 foot = _body.position;

            Assert.IsTrue(_interior.TryGoToLevel(InteriorLevelLayout.UpperLevel));
            yield return WaitForTheClimb();
            Assert.AreEqual(InteriorLevelLayout.UpperLevel, _interior.Level, "precondition: upstairs");

            // While they are upstairs, a chair is pushed onto the foot of the stairs: wide, shallow, and
            // standing a little ABOVE the landing, so the nearest way out is straight down — 0.35 m
            // (the body's top at +0.25 against the chair's underside at −0.10), against 0.55 m up and
            // 0.85 m to either side.
            var chairSize = new Vector2(1.2f, 0.4f);
            Vector2 chairCentre = foot + new Vector2(0f, 0.1f);
            Chair(_groundProps, chairCentre, chairSize);
            float expectedPush = BodyRadius + (chairSize.y * 0.5f - 0.1f);

            Assert.IsTrue(_interior.TryGoToLevel(InteriorLevelLayout.GroundLevel));
            yield return WaitForTheClimb();

            Assert.AreEqual(InteriorLevelLayout.GroundLevel, _interior.Level);
            Assert.AreEqual(0, SolidOverlaps(), "the landing is clear of every solid collider");
            Assert.AreEqual(foot.x, _body.position.x, 0.01f, "pushed straight out, not sideways");
            Assert.AreEqual(foot.y - expectedPush - Physics2D.defaultContactOffset, _body.position.y, 0.02f,
                "to the NEAREST clear point — the shallowest way out of the chair");
            AssertBodyRestored("after an obstructed landing");
        }

        [UnityTest]
        public IEnumerator AClimbInterruptedByTheHouseBeingDisabled_RestoresTheBody()
        {
            yield return WalkInToTheFoot();

            Assert.IsTrue(_interior.TryGoToLevel(InteriorLevelLayout.UpperLevel));
            yield return null;
            Assert.IsTrue(_interior.IsClimbing, "precondition: mid-climb");
            Assert.AreEqual(RigidbodyType2D.Kinematic, _body.bodyType, "precondition: the climb holds the body");

            _interior.enabled = false;   // OnDisable — a house torn down mid-climb

            Assert.IsFalse(_interior.IsClimbing);
            Assert.AreEqual(RigidbodyType2D.Dynamic, _body.bodyType, "the body's own type is put back at once");
            Assert.AreEqual(Vector2.zero, _body.linearVelocity);
            Assert.IsFalse(MoveActionClaim.IsClaimed);
        }

        [UnityTest]
        public IEnumerator AClimbInterruptedByATeleport_RestoresTheBody()
        {
            yield return WalkInToTheFoot();

            Assert.IsTrue(_interior.TryGoToLevel(InteriorLevelLayout.UpperLevel));
            yield return null;
            Assert.IsTrue(_interior.IsClimbing, "precondition: mid-climb");

            // A spawn, a region hop or a dev teleport: somebody else is placing this fisher now.
            Vector2 away = (Vector2)CottageGround + new Vector2(0f, -25f);
            _player.transform.position = new Vector3(away.x, away.y, 0f);
            yield return null;
            yield return null;

            Assert.IsFalse(_interior.IsClimbing, "the walk hands the body back");
            Assert.AreEqual(RigidbodyType2D.Dynamic, _body.bodyType, "with its own body type");
            // The TRANSFORM, not the body: no fixed step need have run since the teleport, so the body
            // pose can still be the pre-teleport one.
            Assert.AreEqual(away.x, _player.transform.position.x, 0.01f, "and the climb did not drag it back");
            Assert.AreEqual(away.y, _player.transform.position.y, 0.01f);
            Assert.IsFalse(MoveActionClaim.IsClaimed);
        }
    }
}
