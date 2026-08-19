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
    /// <b>The storey above, driven rather than reasoned about.</b> A real two-level
    /// <see cref="BuildingInterior"/> in a real scene, entered by walking a transform across the
    /// threshold, climbed with the real interact verb, and slept in through the real Core save seam.
    ///
    /// <para><b>What this proves that EditMode cannot.</b> Three things, and they are the three that
    /// would otherwise be assumed. (1) <c>Update</c> runs for real, so the footprint test — not a test
    /// poking a setter — is what decides you are inside. (2) The PHYSICS is real, so the doorway plug
    /// and the partition are asked whether they actually block: a collider that exists in the hierarchy
    /// but never reaches the physics world looks identical from EditMode. (3) <c>OnEnable</c> and
    /// <c>OnDisable</c> run for real, which is the mechanism that un-registers the storey you are NOT on
    /// from <see cref="Interactables"/> — what makes the press unable to reach the wrong stair, or a bed
    /// through a ceiling.</para>
    ///
    /// <para><b>Blocking is proved by CASTING, not by shoving.</b> Pushing a rigidbody at a wall and
    /// asserting it did not get through is a timing test wearing a physics costume — how far it travels
    /// depends on the step count, and headless frames are nowhere near real ones. A
    /// <see cref="Physics2D.Linecast(Vector2, Vector2)"/> asks the physics world the question directly
    /// and gets the same answer on any machine.</para>
    ///
    /// <para><b>⚠ Why this file does NOT read Ginny's plan.</b> The plan lives in
    /// <c>StPetersInteriors</c>, which is an EDITOR assembly: a PlayMode test cannot reference it, and
    /// should not — the placement table is authored content and its coordinates are pinned in
    /// <c>CabinUpstairsTests</c>, where the editor assembly IS visible. What is under test here is the
    /// MECHANISM. So this house is built from this file's own numbers, and every collider it stands is
    /// derived from the same <see cref="InteriorFootprint"/> the assertions cast against — the two
    /// cannot drift apart, because there is only one of each.</para>
    ///
    /// <para><b>⚠ The save service is FAKED on purpose.</b> The real <c>SaveService</c> bootstraps itself
    /// into play mode and writes the developer's actual save file. A test that let a rest reach it would
    /// overwrite the owner's game. <see cref="GameServices.Save"/> is pointed at a fake for the duration
    /// and <see cref="GameServices.Reset"/> puts it back.</para>
    ///
    /// <para><b>The scene is created here and unloaded in teardown.</b> A scene left resident reds other
    /// files with failures that read like gameplay bugs.</para>
    /// </summary>
    public class CabinUpstairsPlayTests
    {
        const string SceneName = "CabinUpstairs_Test";

        // sageCottage's own numbers — the cottage Ginny's plan was authored against. Restated here
        // because App.Editor is not visible from a player assembly (see the class remarks).
        const float FloorWidth = 6.6f;
        const float FloorLength = 8.05f;
        const float WallThickness = 0.3f;
        const float DoorwayWidth = 1.4f;
        const int Facing = 0;
        const int Facings = 8;
        const float GroundDepthScale = 0.6427876f;   // sin 40° at the shared bake camera

        // The upstairs, in the room's model frame. A landing up the right-hand side, a bedroom either
        // side of an east–west partition. The same SHAPE as the shipped plan; the shipped plan's own
        // coordinates are pinned in EditMode.
        static readonly Vector2 StairPoint = new Vector2(2.50f, -0.90f);
        static readonly Vector2 PlayerBedPoint = new Vector2(-2.10f, -2.30f);
        static readonly Vector2 OwnerBedPoint = new Vector2(-2.10f, 2.30f);
        static readonly Vector2 WardrobePoint = new Vector2(0.35f, -0.60f);

        // x0, x1, y0, y1 — the partition between the two bedrooms, and the landing's own wall.
        static readonly Vector4[] Partitions =
        {
            new Vector4(0.90f, 1.20f, -4.025f, -3.10f),
            new Vector4(0.90f, 1.20f, -1.90f, 1.40f),
            new Vector4(0.90f, 1.20f, 2.60f, 4.025f),
            new Vector4(-3.30f, 1.20f, 0.40f, 0.70f),
        };

        const string OwnerName = "Ginny";

        static readonly Vector3 CottageGround = new Vector3(30f, 30f, 0f);

        sealed class FakeSaveService : ISaveService
        {
            public int Saves;
            public SaveData Current { get; } = new SaveData();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; LoadedExistingSave = true; }
        }

        Scene _origin, _scene;
        GameObject _cottage, _player;
        BuildingInterior _interior;
        SpriteRenderer _shell, _ground, _upper;
        GameObject _groundProps, _upperProps, _upperWalls;
        FakeSaveService _save;
        readonly List<GameSaved> _outcomes = new List<GameSaved>();
        readonly List<DevNotice> _notices = new List<DevNotice>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _origin = SceneManager.GetActiveScene();
            _scene = SceneManager.CreateScene(SceneName);
            SceneManager.SetActiveScene(_scene);

            Interactables.Clear();
            _outcomes.Clear();
            _notices.Clear();
            EventBus.Clear<GameSaved>();
            EventBus.Clear<DevNotice>();
            EventBus.Subscribe<GameSaved>(_outcomes.Add);
            EventBus.Subscribe<DevNotice>(_notices.Add);

            _save = new FakeSaveService();
            GameServices.Save = _save;
            RestSaveResponder.Install();

            _player = new GameObject("player");
            SceneManager.MoveGameObjectToScene(_player, _scene);
            _player.transform.position = CottageGround + new Vector3(0f, -20f, 0f);

            StandCottage();

            yield return null;   // let Update run once with the occupant outside
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EventBus.Clear<GameSaved>();
            EventBus.Clear<DevNotice>();
            Interactables.Clear();
            RestSaveResponder.Uninstall();

            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);

            GameServices.Reset();
        }

        /// <summary>
        /// A two-storey cottage, wired the way <c>StPetersInteriors.Stand</c> wires one — configured
        /// while INACTIVE and enabled afterwards, which is the order the builder uses and the order that
        /// keeps <c>OnEnable</c> from running against null references.
        /// </summary>
        void StandCottage()
        {
            _cottage = new GameObject("cottage");
            _cottage.SetActive(false);
            SceneManager.MoveGameObjectToScene(_cottage, _scene);
            _cottage.transform.position = CottageGround;

            _shell = Child<SpriteRenderer>("Shell");
            _ground = Child<SpriteRenderer>("Interior");
            _upper = Child<SpriteRenderer>("InteriorUpper");
            _groundProps = ChildGo("Furniture");
            _upperProps = ChildGo("FurnitureUpper");
            _upperWalls = ChildGo("WallsUpper");

            _interior = _cottage.AddComponent<BuildingInterior>();
            _interior.Configure(_shell, _ground, _groundProps.transform,
                                FloorWidth, FloorLength, Facing, Facings, GroundDepthScale,
                                WallThickness, DoorwayWidth);
            _interior.SetOccupant(_player.transform);

            InteriorFootprint footprint = _interior.Footprint;

            // The house's own walls — always on, both storeys, exactly as BuildWalls stands them.
            var wallsGo = ChildGo("Walls");
            foreach (Vector2[] quad in footprint.WallQuads(WallThickness, DoorwayWidth))
                AddQuad(wallsGo, quad);

            // What exists only upstairs: the partitions, and the plug over the front door.
            foreach (Vector4 r in Partitions)
                AddQuad(_upperWalls, footprint.Quad(r.x, r.y, r.z, r.w));
            AddQuad(_upperWalls, footprint.DoorwayPlugQuad(WallThickness, DoorwayWidth));

            // The stairwell, placed TWICE on one model point — the foot downstairs, the head upstairs.
            var up = Fixture(_groundProps, "stair up", footprint, StairPoint)
                .AddComponent<InteriorStair>();
            up.Configure(_interior, "fixture.play.stair_up", 0, 1, 1.2f, "up");

            var down = Fixture(_upperProps, "stair down", footprint, StairPoint)
                .AddComponent<InteriorStair>();
            down.Configure(_interior, "fixture.play.stair_down", 1, 0, 1.2f, "down");

            Fixture(_upperProps, "player bed", footprint, PlayerBedPoint)
                .AddComponent<InteriorBed>()
                .Configure("fixture.play.bed_player", true, "", "your bed at Ginny's", 1.4f);

            Fixture(_upperProps, "ginny bed", footprint, OwnerBedPoint)
                .AddComponent<InteriorBed>()
                .Configure("fixture.play.bed_owner", false, OwnerName, "", 1.4f);

            Fixture(_upperProps, "wardrobe", footprint, WardrobePoint)
                .AddComponent<InteriorWardrobe>()
                .Configure("fixture.play.wardrobe", 1.2f);

            _interior.ConfigureUpperLevel(_upper, _upperProps.transform, _upperWalls.transform);
            _cottage.SetActive(true);
        }

        static GameObject Fixture(GameObject root, string name, in InteriorFootprint footprint,
                                  Vector2 modelMetres)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, worldPositionStays: false);
            Vector2 world = footprint.ModelToWorld(modelMetres);
            go.transform.position = new Vector3(world.x, world.y, 0f);
            return go;
        }

        T Child<T>(string name) where T : Component => ChildGo(name).AddComponent<T>();

        GameObject ChildGo(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_cottage.transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        static void AddQuad(GameObject host, Vector2[] worldQuad)
        {
            var c = host.AddComponent<PolygonCollider2D>();
            c.pathCount = 1;
            var local = new Vector2[worldQuad.Length];
            Vector2 origin = host.transform.position;
            for (int i = 0; i < worldQuad.Length; i++) local[i] = worldQuad[i] - origin;
            c.SetPath(0, local);
        }

        /// <summary>Put the occupant somewhere and let one frame of <c>Update</c> see it.</summary>
        IEnumerator WalkTo(Vector2 world)
        {
            _player.transform.position = new Vector3(world.x, world.y, 0f);
            yield return null;
        }

        /// <summary>Press the interact verb where the player stands, exactly as the runtime driver does:
        /// resolve one candidate over the LIVE registry, then act on it.</summary>
        bool Press()
        {
            var actor = new InteractActor(_player.transform.position, Vector2.zero,
                                          InteractContext.OnFoot);
            if (!InteractResolver.TryResolveNow(actor, 120f, out IInteractable best)) return false;
            best.Interact(actor);
            return true;
        }

        /// <summary>Walk in, cross to the foot of the stairs, and go up. Asserts its own preconditions
        /// so a failure downstream is never a silent mis-setup.</summary>
        IEnumerator GoUpstairs()
        {
            InteriorFootprint f = _interior.Footprint;
            yield return WalkTo(f.ModelToWorld(Vector2.zero));
            Assert.IsTrue(_interior.IsInside, "precondition: indoors");
            yield return WalkTo(f.ModelToWorld(StairPoint));
            Assert.IsTrue(Press(), "precondition: the stairwell is the candidate at the stair");
            yield return null;
            Assert.AreEqual(1, _interior.Level, "precondition: upstairs");
        }

        // =============================================================================

        [UnityTest]
        public IEnumerator WalkingIn_ThenUpTheStairs_SwapsTheStoreyWithNoLoadAndNoTeleport()
        {
            InteriorFootprint f = _interior.Footprint;

            Assert.IsFalse(_interior.IsInside, "we start in the dooryard");
            Assert.IsTrue(_shell.enabled);

            yield return WalkTo(f.ModelToWorld(Vector2.zero));
            Assert.IsTrue(_interior.IsInside, "walking through the door is the whole of entering");
            Assert.AreEqual(0, _interior.Level);
            Assert.IsTrue(_ground.enabled);
            Assert.IsFalse(_upper.enabled);

            yield return WalkTo(f.ModelToWorld(StairPoint));
            Vector3 before = _player.transform.position;

            Assert.IsTrue(Press(), "the interact verb finds the stairwell");
            yield return null;

            Assert.AreEqual(1, _interior.Level, "you are upstairs");
            Assert.IsTrue(_upper.enabled);
            Assert.IsFalse(_ground.enabled, "and the ground floor is switched off, not sorted behind");
            Assert.IsTrue(_upperProps.activeSelf);
            Assert.AreEqual(before, _player.transform.position,
                            "NOTHING MOVED — the stairwell is the same point on both storeys, which is " +
                            "the whole reason a second level can be a layer swap");
            Assert.IsTrue(_interior.IsInside, "and you are still inside the same building");

            Assert.IsTrue(Press(), "the head of the stairs is now the candidate");
            yield return null;
            Assert.AreEqual(0, _interior.Level);
            Assert.IsTrue(_ground.enabled);
        }

        [UnityTest]
        public IEnumerator TheFrontDoorwayIsOpenDownstairsAndPluggedUpstairs()
        {
            InteriorFootprint f = _interior.Footprint;
            yield return WalkTo(f.ModelToWorld(Vector2.zero));

            // A line from just inside the doorway out through it, and a little beyond.
            Vector2 inside = f.ModelToWorld(new Vector2(0f, -FloorLength * 0.5f + 1.0f));
            Vector2 outside = f.ModelToWorld(new Vector2(0f, -FloorLength * 0.5f - 1.0f));

            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.IsNull(Physics2D.Linecast(inside, outside).collider,
                          "downstairs the doorway is a gap you can walk out of");

            yield return GoUpstairs();

            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.IsNotNull(Physics2D.Linecast(inside, outside).collider,
                             "upstairs the SAME doorway is solid — there is nothing outside a " +
                             "first-floor door but air, and the plug is what says so");
        }

        [UnityTest]
        public IEnumerator ThePartitionDividesTheTwoBedroomsUpstairsAndNotDownstairs()
        {
            InteriorFootprint f = _interior.Footprint;

            // From deep in the player's room to deep in Ginny's, crossing the partition well west of
            // the landing door.
            Vector2 south = f.ModelToWorld(PlayerBedPoint);
            Vector2 north = f.ModelToWorld(OwnerBedPoint);

            yield return WalkTo(f.ModelToWorld(Vector2.zero));
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.IsNull(Physics2D.Linecast(south, north).collider,
                          "downstairs it is one room and nothing divides it");

            yield return GoUpstairs();

            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.IsNotNull(Physics2D.Linecast(south, north).collider,
                             "upstairs the two bedrooms are separated — the bed you may sleep in is not " +
                             "in the same room as the one you may not");
        }

        [UnityTest]
        public IEnumerator SleepingInYourOwnBedSavesAndGinnysDoesNot()
        {
            InteriorFootprint f = _interior.Footprint;
            yield return GoUpstairs();

            // Ginny's bed first — a refusal must never write.
            yield return WalkTo(f.ModelToWorld(OwnerBedPoint));
            Assert.IsTrue(Press(), "her bed is still something you can walk up to and try");
            yield return null;
            Assert.AreEqual(0, _save.Saves, "and trying must not keep the day in somebody else's room");
            Assert.Greater(_notices.Count, 0, "you are told whose bed it is");
            StringAssert.Contains(OwnerName, _notices[_notices.Count - 1].Text);

            // The player's own.
            yield return WalkTo(f.ModelToWorld(PlayerBedPoint));
            Assert.IsTrue(Press());
            yield return null;

            Assert.AreEqual(1, _save.Saves,
                            "your own bed is the manual save — the first one the game has had");
            Assert.AreEqual(1, _outcomes.Count);
            Assert.IsTrue(_outcomes[0].Ok);
        }

        [UnityTest]
        public IEnumerator TheStoreyYouAreNotOnIsNotEvenACandidateForThePress()
        {
            InteriorFootprint f = _interior.Footprint;
            yield return WalkTo(f.ModelToWorld(Vector2.zero));

            foreach (IInteractable c in Interactables.Active)
                Assert.IsNotInstanceOf<InteriorBed>(
                    c, "no bed is reachable from the ground floor: the storey above is switched off, " +
                       "and OnDisable is what takes its fixtures out of the registry");

            yield return WalkTo(f.ModelToWorld(StairPoint));
            Assert.IsTrue(Press());
            yield return null;

            int beds = 0, stairs = 0;
            foreach (IInteractable c in Interactables.Active)
            {
                if (c is InteriorBed) beds++;
                if (c is InteriorStair) stairs++;
            }
            Assert.AreEqual(2, beds, "upstairs both beds are candidates");
            Assert.AreEqual(1, stairs,
                            "and exactly ONE half of the stairwell is in the room at a time — the other " +
                            "half's root is switched off, so OnDisable has taken it out of the registry " +
                            "altogether. This is the hook EditMode cannot see.");
        }

        [UnityTest]
        public IEnumerator BeingMovedOutOfTheHouseResetsTheStorey()
        {
            InteriorFootprint f = _interior.Footprint;
            yield return GoUpstairs();

            // A spawn, a region hop or a dev teleport can all put an occupant outside without using the
            // door. The storey must not survive that.
            yield return WalkTo(f.ModelToWorld(new Vector2(0f, -FloorLength * 0.5f - 20f)));

            Assert.IsFalse(_interior.IsInside);
            Assert.AreEqual(0, _interior.Level,
                            "the house opens on its ground floor next time, never on the bedroom");
            Assert.IsTrue(_shell.enabled);
            Assert.IsFalse(_upper.enabled);
            Assert.IsFalse(_upperWalls.activeSelf);
        }

        [UnityTest]
        public IEnumerator TheWardrobeAnswersUpstairsAndRaisesItsSignal()
        {
            var asked = new List<WardrobeRequested>();
            EventBus.Clear<WardrobeRequested>();
            EventBus.Subscribe<WardrobeRequested>(asked.Add);

            InteriorFootprint f = _interior.Footprint;
            yield return GoUpstairs();

            yield return WalkTo(f.ModelToWorld(WardrobePoint));
            Assert.IsTrue(Press(), "the wardrobe is a candidate in the player's room");
            yield return null;

            Assert.AreEqual(1, asked.Count, "the customization lane's seam fires from the real press");
            Assert.AreEqual(0, _save.Saves, "and opening a wardrobe is not a save");

            EventBus.Clear<WardrobeRequested>();
        }
    }
}
