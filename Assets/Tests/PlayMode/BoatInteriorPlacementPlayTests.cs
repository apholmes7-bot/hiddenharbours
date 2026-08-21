using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A BOAT GROWS HER CABIN ON LOAD, AND HER DOOR STAYS SHUT</b> — the placement pass alive, which
    /// is the only place it can be tested: the installer mounts from <c>BoatController.Awake</c> in play
    /// mode only, and EditMode fires no <c>Awake</c> on a plain MonoBehaviour.
    ///
    /// <para><b>The headline assertion is a NEGATIVE, and it is the ruled behaviour.</b> With the
    /// exterior half of the swap unwired — which the S0 spike measured to be every hull in the fleet —
    /// the cabin is built, the door is registered, and the door offers <i>nothing</i>. Opening onto a
    /// half-wired swap would draw the interior and the exterior at once, posed differently, which is the
    /// co-visibility ADR 0038 forbids.</para>
    ///
    /// <para>⚠️ <b>The round trip therefore runs against a TEST-WIRED STAND-IN exterior</b>, and says so
    /// here rather than in a commit message: no shipped hull can complete the swap yet, so a round-trip
    /// test that used one would be asserting against a cabin nobody can open. The stand-in exercises the
    /// same code path the per-level face tags will feed — which is exactly what the argument was left in
    /// the signature for.</para>
    /// </summary>
    public class BoatInteriorPlacementPlayTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();

            // ⚠ An AudioListener, because a listener-less play scene logs a warning EVERY frame.
            var listener = new GameObject("Listener");
            listener.AddComponent<AudioListener>();
            _spawned.Add(listener);
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        // =====================================================================================
        //  THE RULED OUTCOME: built, registered, and silent
        // =====================================================================================

        [UnityTest]
        public IEnumerator AMeasuredHullGrowsHerCabinOnLoad()
        {
            Rig rig = NewBoat(withInterior: true);
            yield return null;

            Assert.IsTrue(rig.Installer.Built, "a hull whose visual def names an interior builds one");
            Assert.IsNotNull(rig.Installer.Interior);
            Assert.IsNotNull(rig.Installer.Door);

            Assert.IsNotNull(rig.Boat.transform.Find(BoatInteriorInstaller.RoomChildName),
                             "the room hangs off the boat ROOT, not off her rocked visual child");
            Assert.IsNotNull(rig.Boat.transform.Find(BoatInteriorInstaller.DoorChildName));
        }

        [UnityTest]
        public IEnumerator AnUnmeasuredHullBuildsNothing_AndThatIsNotAFault()
        {
            Rig rig = NewBoat(withInterior: false);
            yield return null;

            Assert.IsFalse(rig.Installer.Built);
            Assert.IsNull(rig.Installer.Interior);
            Assert.AreEqual(0, Interactables.Count, "…and she registers no door to press");
        }

        [UnityTest]
        public IEnumerator TheDoorIsRegisteredButSilentWhileTheSwapCannotComplete()
        {
            // THE RULED OUTCOME, asserted rather than described. Every shipped hull is here today.
            Rig rig = NewBoat(withInterior: true);
            yield return null;

            Assert.IsFalse(rig.Installer.Interior.SwapIsCompletable,
                           "the exterior half is null on a mesh hull until the per-level tags land");
            Assert.AreEqual(1, Interactables.Count, "the door IS registered — it simply declines");
            Assert.IsFalse(rig.Installer.Door.IsAvailable);

            InteractVerb.PublishCandidate(
                new InteractActor(rig.Installer.Door.transform.position, Vector2.zero,
                                  InteractContext.OnDeck), 90f);

            Assert.IsFalse(InteractOffer.Current.Has,
                           "…so the popup never names a cabin the player cannot be shown");

            Assert.IsFalse(rig.Installer.Door.TryUse());
            Assert.IsFalse(rig.Installer.Interior.IsInside);
        }

        // =====================================================================================
        //  THE ROUND TRIP — against a TEST-WIRED stand-in exterior, because no hull qualifies yet
        // =====================================================================================

        [UnityTest]
        public IEnumerator WithAStandInExteriorHandedIn_TheRoundTripWorksEndToEnd()
        {
            Rig rig = NewBoat(withInterior: true);
            yield return null;

            // The stand-in: what the per-level face tags will eventually supply. Handing it in is the
            // ONE argument the installer left in the signature for exactly this.
            var standIn = new GameObject("StandInExterior").AddComponent<SpriteRenderer>();
            standIn.transform.SetParent(rig.Boat.transform, false);
            HandInExterior(rig, standIn);
            yield return null;

            Assert.IsTrue(rig.Installer.Interior.SwapIsCompletable);
            Assert.IsTrue(rig.Installer.Door.IsAvailable, "the same door, now offering");

            InteractVerb.PublishCandidate(
                new InteractActor(rig.Installer.Door.transform.position, Vector2.zero,
                                  InteractContext.OnDeck), 90f);
            Assert.IsTrue(InteractOffer.Current.Has);
            Assert.AreEqual("Go below", InteractOffer.Current.Label);

            Assert.IsTrue(rig.Installer.Door.TryUse());
            yield return WaitForCue(rig.Installer.Door);

            Assert.IsTrue(rig.Installer.Interior.IsInside);
            Assert.IsTrue(rig.Installer.Interior.ExactlyOneLayerOn,
                          "exactly one layer on — the invariant the gate exists to protect");
            Assert.IsFalse(standIn.enabled);

            Assert.IsTrue(rig.Installer.Door.TryUse());
            yield return WaitForCue(rig.Installer.Door);

            Assert.IsFalse(rig.Installer.Interior.IsInside);
            Assert.IsTrue(standIn.enabled);
            Assert.AreEqual(0, rig.Installer.Interior.Level, "and the level resets on the way out");
        }

        [UnityTest]
        public IEnumerator TheRoomHangsOffTheRootSoItDoesNotTakeHerRideTwice()
        {
            // BoatInterior poses its target ABSOLUTELY from a cached base. Parented under the hull's
            // visual child — where the ride is applied — it would take her heave a second time. The
            // installer therefore parents it to the ROOT, and this pins that.
            Rig rig = NewBoat(withInterior: true);
            yield return null;

            Transform room = rig.Boat.transform.Find(BoatInteriorInstaller.RoomChildName);
            Assert.IsNotNull(room);
            Assert.AreSame(rig.Boat.transform, room.parent,
                           "the room's parent must be the boat root itself");
        }

        [UnityTest]
        public IEnumerator TheRoomStaysSquareToTheScreenWhileSheTurns()
        {
            Rig rig = NewBoat(withInterior: true);
            yield return null;

            Transform room = rig.Boat.transform.Find(BoatInteriorInstaller.RoomChildName);
            rig.Boat.transform.rotation = Quaternion.Euler(0f, 0f, 37f);
            yield return null;

            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, room.rotation.eulerAngles.z), 1e-3f,
                            "a pre-baked cell must not swing with the hull's yaw");
        }

        // =====================================================================================
        //  the fixture
        // =====================================================================================

        private struct Rig
        {
            public GameObject Boat;
            public BoatInteriorInstaller Installer;
            public BoatInteriorDef Def;
            public Sprite[] Cells;
        }

        private IEnumerator WaitForCue(BoatCabinDoor door)
        {
            float deadline = Time.time + door.CueSeconds + 0.2f;
            while (door.IsCueing && Time.time < deadline) yield return null;
            yield return null;
        }

        /// <summary>Hand the exterior half in, the way a later pass will: re-Configure the cabin with
        /// everything it already had plus the renderer.</summary>
        private static void HandInExterior(Rig rig, SpriteRenderer exterior)
        {
            BoatInterior cabin = rig.Installer.Interior;
            Transform room = rig.Boat.transform.Find(BoatInteriorInstaller.RoomChildName);
            cabin.Configure(rig.Def, exterior, room.GetComponent<SpriteRenderer>(), null,
                            room, rig.Boat.transform, rig.Cells, 8, true, 0f,
                            5f, 1.6f, 0.02f, cellRowForLevel: new[] { 0 });
        }

        private Rig NewBoat(bool withInterior)
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            def.Id = "interior.play_placement";
            def.PixelsPerMetre = 32;
            def.Levels = new[]
            {
                new BoatInteriorLevel
                {
                    Id = "house_sole",
                    SoleZMeters = 0.5f,
                    Outline = new[]
                    {
                        new Vector2(-1f, -2f), new Vector2(1f, -2f),
                        new Vector2(1f, 2f), new Vector2(-1f, 2f),
                    },
                },
            };
            def.Door = new BoatInteriorDoor
            {
                Id = "entry",
                ThresholdPoint = new Vector3(0f, -1.9f, 0.5f),
                ClearWidthMeters = 0.72f,
                CueFrames = 8,
                CueMillisecondsPerFrame = 70f,
                CueReversedOnExit = true,
            };
            _spawned.Add(def);

            var visual = ScriptableObject.CreateInstance<BoatVisualDef>();
            visual.Id = "visual.play_placement";
            // ⚠ The visual def carries the LINK and nothing else — the pixels moved to a Resources
            // asset so a hull stops dragging megabytes of cabin in on spawn. A rig has no such asset,
            // so the tests that need a picture hand cells in through Configure instead (see
            // HandInExterior); the rest never need one, which is itself the point.
            if (withInterior) visual.Interior = def;
            _spawned.Add(visual);

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Visual = visual;
            _spawned.Add(hull);

            var boat = new GameObject("PlacementTestBoat");
            _spawned.Add(boat);
            var controller = boat.AddComponent<BoatController>();   // brings its required components
            controller.SetHull(hull);

            // ⚠ BoatController.Awake ALREADY mounted the installer — that is the shipped wiring, and
            // AddComponent-ing a second would trip [DisallowMultipleComponent]. Take the one she grew,
            // which is also the one the game will use. Build() explicitly because Awake ran BEFORE
            // SetHull, so the installer's own Start found no hull to read; it is idempotent, so the
            // Start that follows is free.
            var installer = boat.GetComponent<BoatInteriorInstaller>();
            Assert.IsNotNull(installer, "BoatController.Awake should have mounted the installer itself");
            installer.Build();

            return new Rig { Boat = boat, Installer = installer, Def = def, Cells = Cells(8) };
        }

        /// <summary>One level's worth of throwaway cells. Real pixels are not the point here — the
        /// placement is; the sheets' own registration is pinned by BoatInteriorSheetTests.</summary>
        private Sprite[] Cells(int n)
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var cells = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                cells[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                cells[i].name = $"cell_{i}";
                _spawned.Add(cells[i]);
            }
            return cells;
        }
    }
}
