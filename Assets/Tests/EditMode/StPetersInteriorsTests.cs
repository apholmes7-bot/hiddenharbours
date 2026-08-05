using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the PLACEMENT half of the interiors pilot — the pass that stands a baked room under a
    /// placed village building, walls it, furnishes it and hands it a player.
    ///
    /// <para>Everything here runs without a baked sheet on disk. That is deliberate: the bake is the
    /// owner's menu click, and the geometry, the furnishing table and the re-run behaviour are all
    /// checkable before a single pixel exists. What is NOT checkable here is what the room looks like —
    /// see the PR's Play-mode checklist.</para>
    /// </summary>
    public class StPetersInteriorsTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Nothing is baked in a test run, so anything reaching the contract logs "no contract at …" —
        /// which is the CORRECT behaviour (the bake is the owner's menu click) and would otherwise fail
        /// the test for saying so. Called only by the tests that deliberately walk the missing-art path.
        /// </summary>
        static void AllowTheMissingContractError() => LogAssert.ignoreFailingMessages = true;

        // =============================================================================
        //  the constant that cannot be shared across an assembly boundary
        // =============================================================================

        [Test]
        public void TheRuntimeSquashMatchesTheSharedBakeCamera()
        {
            // BuildingInterior lives in World, which does not reference Art, so it cannot read
            // SpriteLightMath.GroundDepthScale directly and carries a serialized default instead. This
            // test is the mechanism that stops the two drifting: a default that no longer equals the
            // camera would make every interior's collision quietly deeper or shallower than its art.
            var go = Spawn("interior");
            var interior = go.AddComponent<BuildingInterior>();

            Assert.AreEqual(SpriteLightMath.GroundDepthScale, interior.Footprint.DepthScale, 1e-5f,
                            "BuildingInterior's default ground-depth scale must equal sin(40°), the " +
                            "shared bake camera's elevation term");
        }

        [Test]
        public void TheWallThicknessTheColliderUsesIsTheOneTheInsideTestUses()
        {
            // Two numbers that have to agree is the bug; the builder writes its own constants into the
            // component so there is only ever one of each.
            var go = Spawn("interior");
            var interior = go.AddComponent<BuildingInterior>();
            interior.Configure(null, null, null, 6.6f, 8.05f, 4, 8,
                               SpriteLightMath.GroundDepthScale,
                               StPetersInteriors.WallThicknessMetres,
                               StPetersInteriors.DoorwayWidthMetres);

            Assert.AreEqual(StPetersInteriors.WallThicknessMetres, interior.WallThicknessMetres, 1e-6f);
            Assert.AreEqual(StPetersInteriors.DoorwayWidthMetres, interior.DoorwayWidthMetres, 1e-6f);
        }

        [Test]
        public void TheWallIsThickEnoughNotToBeTunnelledAtSprintSpeed()
        {
            // 5.5 m/s sprint at a 0.02 s fixed step is 0.11 m per step. The rig draws a 0.16 m wall,
            // which is inside a factor of 1.5 of that — a fast diagonal into a corner is exactly the
            // input that finds it, so the collider is deliberately thicker than the art.
            const float sprintMetresPerStep = 5.5f * 0.02f;
            Assert.Greater(StPetersInteriors.WallThicknessMetres, sprintMetresPerStep * 2f,
                           "a wall thinner than twice the per-step travel can be walked through");
        }

        // =============================================================================
        //  enter / exit
        // =============================================================================

        [Test]
        public void TheShellAndTheRoomAreNeverBothVisible()
        {
            var (interior, shell, room, props) = MakeInterior(facing: 0, centre: Vector2.zero);
            var occupant = Spawn("player");
            interior.SetOccupant(occupant.transform);

            // OUTSIDE: shell only. (Enable is what OnEnable would have done; EditMode does not run it —
            // the component was added to an already-active object.)
            occupant.transform.position = new Vector3(0f, -40f, 0f);
            Tick(interior);
            Assert.IsTrue(shell.enabled, "outside: the shell is what you see");
            Assert.IsFalse(room.enabled, "…and the room is not, or you would see the furniture through " +
                                         "the walls");
            Assert.IsFalse(props.activeSelf);

            // INSIDE: room only.
            occupant.transform.position = Vector3.zero;
            Tick(interior);
            Assert.IsTrue(interior.IsInside);
            Assert.IsFalse(shell.enabled, "inside: the shell yields");
            Assert.IsTrue(room.enabled);
            Assert.IsTrue(props.activeSelf);

            // BACK OUT.
            occupant.transform.position = new Vector3(0f, -40f, 0f);
            Tick(interior);
            Assert.IsFalse(interior.IsInside);
            Assert.IsTrue(shell.enabled);
            Assert.IsFalse(room.enabled);
        }

        [Test]
        public void StandingInTheWallIsNotInside()
        {
            var (interior, _, _, _) = MakeInterior(facing: 0, centre: Vector2.zero);
            var occupant = Spawn("player");
            interior.SetOccupant(occupant.transform);

            // Just inside the footprint but still in the front wall.
            InteriorFootprint f = interior.Footprint;
            Vector2 inWall = f.ModelToWorld(new Vector2(0f, -8.05f * 0.5f + 0.1f));
            occupant.transform.position = inWall;
            Tick(interior);

            Assert.IsFalse(interior.IsInside,
                           "you are inside once you are PAST the wall — the same moment the doorway " +
                           "lets you through");
        }

        [Test]
        public void ARoomWithNoOccupantStaysShut()
        {
            var (interior, shell, room, _) = MakeInterior(facing: 0, centre: Vector2.zero);
            Tick(interior);

            Assert.IsFalse(interior.IsInside);
            Assert.IsTrue(shell.enabled);
            Assert.IsFalse(room.enabled);
        }

        // =============================================================================
        //  the furnishing table
        // =============================================================================

        [Test]
        public void EveryFurnishingNamesAPropTheKitBakes()
        {
            var baked = new HashSet<string>();
            foreach (var b in InteriorKit.PropSet) baked.Add(b.Key);

            foreach (var room in InteriorKit.RoomSet)
                foreach (var f in StPetersInteriors.FurnishingsFor(room.Key))
                    CollectionAssert.Contains(baked, f.PropKey,
                                              $"'{room.Key}' asks for a '{f.PropKey}' the kit does not " +
                                              "bake — it would be skipped, leaving a hole in the " +
                                              "collision where a solid object should be");
        }

        [Test]
        public void NoFurnishingStandsInsideAWallOrTheDoorway()
        {
            const float halfWidth = 6.6f * 0.5f, halfLength = 8.05f * 0.5f;
            float wall = StPetersInteriors.WallThicknessMetres;
            float halfDoor = StPetersInteriors.DoorwayWidthMetres * 0.5f;

            foreach (var f in StPetersInteriors.FurnishingsFor("sageCottage"))
            {
                Assert.Less(Mathf.Abs(f.RoomMetres.x), halfWidth - wall,
                            $"'{f.PropKey}' is inside a side wall");
                Assert.Less(Mathf.Abs(f.RoomMetres.y), halfLength - wall,
                            $"'{f.PropKey}' is inside the front or back wall");

                // ⭐ The doorway lane. A prop parked in the threshold is a prop you cannot get past, and
                // its collider closes the ONE gap in the house — you would be locked out of your own
                // cottage by a chair.
                bool inDoorLane = Mathf.Abs(f.RoomMetres.x) < halfDoor + 0.5f &&
                                  f.RoomMetres.y < -halfLength + 2.0f;
                Assert.IsFalse(inDoorLane, $"'{f.PropKey}' is parked in the doorway");
            }
        }

        [Test]
        public void TheFurnishingsDoNotAllPileUpOnOnePoint()
        {
            var seen = new List<Vector2>();
            foreach (var f in StPetersInteriors.FurnishingsFor("sageCottage"))
            {
                foreach (Vector2 other in seen)
                    Assert.Greater(Vector2.Distance(f.RoomMetres, other), 0.3f,
                                   "two pieces of furniture on top of each other read as one, and " +
                                   "their colliders merge into a blob");
                seen.Add(f.RoomMetres);
            }
            Assert.GreaterOrEqual(seen.Count, 4, "the pilot furnishes the room honestly");
        }

        [Test]
        public void AnUnfurnishedRoomKeyReturnsAnEmptyTableRatherThanThrowing()
        {
            CollectionAssert.IsEmpty(StPetersInteriors.FurnishingsFor("noSuchRoom"));
        }

        // =============================================================================
        //  re-runnable
        // =============================================================================

        [Test]
        public void StandingTwiceLeavesOneRoom_NotTwo()
        {
            // The builder re-runs on every region build, and the whole scene is authored from it. A
            // pass that appended would leave the owner with two rooms, two sets of furniture and two
            // sets of wall colliders inside one cottage — and the second set is invisible.
            AllowTheMissingContractError();
            var building = Spawn("sageCottage");
            var shell = building.AddComponent<SpriteRenderer>();

            // No sheet is baked in a test run, so Stand() declines — which is exactly the partial-art
            // path the builder has to survive. What it must NOT do is leave debris behind.
            StPetersInteriors.Stand(building, shell, "sageCottage", 4, null);
            StPetersInteriors.Stand(building, shell, "sageCottage", 4, null);

            Assert.LessOrEqual(CountChildren(building, StPetersInteriors.RoomChildName), 1);
            Assert.LessOrEqual(CountChildren(building, StPetersInteriors.PropsChildName), 1);
            Assert.LessOrEqual(CountChildren(building, StPetersInteriors.WallsChildName), 1);
            Assert.LessOrEqual(building.GetComponents<BuildingInterior>().Length, 1);
        }

        [Test]
        public void StandingOnABuildingWithNoBakedRoomChangesNothing()
        {
            AllowTheMissingContractError();
            var building = Spawn("school");
            var shell = building.AddComponent<SpriteRenderer>();

            bool stood = StPetersInteriors.Stand(building, shell, "school", 4, null);

            Assert.IsFalse(stood, "four of the five village buildings have no room baked for them");
            Assert.AreEqual(0, building.transform.childCount);
            Assert.IsTrue(shell.enabled, "and their shells are left exactly as the village placed them");
        }

        // =============================================================================
        //  helpers
        // =============================================================================

        /// <summary>A BuildingInterior wired to real renderers, at the pilot cottage's footprint.</summary>
        (BuildingInterior, SpriteRenderer, SpriteRenderer, GameObject) MakeInterior(int facing,
                                                                                    Vector2 centre)
        {
            var building = Spawn("building");
            building.transform.position = centre;
            var shell = building.AddComponent<SpriteRenderer>();

            var roomGo = new GameObject("Interior");
            roomGo.transform.SetParent(building.transform, false);
            var room = roomGo.AddComponent<SpriteRenderer>();
            room.enabled = false;

            var propsGo = new GameObject("Furniture");
            propsGo.transform.SetParent(building.transform, false);
            propsGo.SetActive(false);

            var interior = building.AddComponent<BuildingInterior>();
            interior.Configure(shell, room, propsGo.transform, 6.6f, 8.05f, facing, 8,
                               SpriteLightMath.GroundDepthScale,
                               StPetersInteriors.WallThicknessMetres,
                               StPetersInteriors.DoorwayWidthMetres);

            return (interior, shell, room, propsGo);
        }

        /// <summary>Drive one frame of the component. EditMode runs no game loop, so <c>Update</c> is
        /// invoked directly — the whole point of deciding "inside" with a pure function is that this
        /// works at all. Via reflection, NOT <c>SendMessage</c>: in edit mode the engine gates magic
        /// methods behind its internal <c>ShouldRunBehaviour()</c> check, and SendMessage("Update") on
        /// a plain MonoBehaviour trips that assert as an [Assert] log the test framework then fails —
        /// which is what turned every Tick-driven test red on CI while the logic itself was correct.</summary>
        static void Tick(BuildingInterior interior) =>
            typeof(BuildingInterior)
                .GetMethod("Update", System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic)
                .Invoke(interior, null);

        static int CountChildren(GameObject go, string name)
        {
            int n = 0;
            for (int i = 0; i < go.transform.childCount; i++)
                if (go.transform.GetChild(i).name == name) n++;
            return n;
        }
    }
}
