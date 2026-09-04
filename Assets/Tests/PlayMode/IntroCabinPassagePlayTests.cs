using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE OPENING, END TO END, FROM BELOW HIS DECK.</b> A new game starts the player inside
    /// Armand's cape islander while he runs the marks; she walks about his cabin, comes up through his aft
    /// door, watches him come alongside, and steps onto the planks with the controls.
    ///
    /// <para><b>Why the whole journey and not four unit tests.</b> Every interesting claim here is about a
    /// SEAM between two things that each work: the cabin swap and the arrival's seating; the door's cue and
    /// the passenger's pose; the cutaway gate and the helm arbiter. A fixture that entered the cabin and
    /// stopped would prove none of them. This is the shape <c>BoatCabinJourneyPlayTests</c> established for
    /// the player's OWN boat, pointed at the one boat she does not own.</para>
    ///
    /// <para><b>⚠ Driven through component APIs, never through keys.</b> A virtual keypress is
    /// undeliverable to the New Input System from a test in this project, so <c>TryBegin</c>,
    /// <c>WalkTheCabin</c>, <c>WorkTheCabinDoor</c> and <c>StepAshore</c> are the way in — and each is the
    /// same call the shipping path makes (the shell's phase change, the walk keys, the interact verb).
    /// </para>
    ///
    /// <para><b>⚠ Every wait is on a STATE with a wall-clock ceiling.</b> Frames are not time; a fixture
    /// that yields sixty times has waited for whatever sixty frames cost on that machine.</para>
    /// </summary>
    public class IntroCabinPassagePlayTests
    {
        /// <summary>Sized as <c>ArrivalOpeningPlayTests</c>' is, plus the cabin beat: she enters at 5 m/s
        /// over a 60 m fixture, sheds to the berthing speed, runs the gate and her own length alongside.
        /// Doubled, so a loaded machine does not produce a red that is really a stopwatch.</summary>
        private const float TimeoutSeconds = 90f;

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public int Saves;
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() => Saves++;
        }

        private GameObject _root;
        private GameObject _player;
        private ArrivalOpening _opening;
        private BoatOwnerDef _skipper;

        private readonly List<CabinEntered> _entered = new List<CabinEntered>();
        private readonly List<CabinLeft> _left = new List<CabinLeft>();

        // Open water, well clear of anything — this fixture is about the passage, not the coast.
        private static readonly Vector2 Start = new Vector2(0f, 60f);
        private static readonly Vector2 Berth = new Vector2(0f, 0f);
        private const float BerthHeading = 180f;
        private static readonly Vector2 Ashore = new Vector2(3f, 0f);

        /// <summary>The shipped come-alongside at tightened numbers, so a manoeuvre that takes half a
        /// minute at the region's tuning takes a few seconds here. Copied from
        /// <c>ArrivalOpeningPlayTests</c> deliberately: what is under test is the CABIN, and the approach
        /// should behave exactly as the fixture that owns it says it does.</summary>
        private static BerthPilot.Settings FixtureAlongside()
        {
            BerthPilot.Settings s = BerthPilot.Settings.Default;
            s.BerthingSpeedMetresPerSecond = 2f;
            s.SetRateMetresPerSecond = 0.8f;
            s.GateStandoffMetres = 1.5f;
            s.GateCaptureMetres = 8f;
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            _entered.Clear();
            _left.Clear();
            // ⚠ Subscribed BEFORE anything is built: the arrival goes below inside its own spawn, so the
            // first CabinEntered is published during TryBegin and a listener attached afterwards would
            // miss the one edge this fixture exists to see.
            EventBus.Subscribe<CabinEntered>(OnEntered);
            EventBus.Subscribe<CabinLeft>(OnLeft);

            _root = new GameObject("IntroCabinFixture");
            _root.AddComponent<AudioListener>();   // one listener, or a full suite logs on every frame

            _player = new GameObject("Player");
            _player.transform.SetParent(_root.transform);
            _player.transform.position = new Vector3(999f, 999f, 0f);
            GameServices.PlayerTransform = _player.transform;

            _skipper = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                "Assets/_Project/Data/Boats/Skippers/StPetersArrivalSkipper.asset");

            // The bollard her line goes over, set back so the honest scope is not clamped to slack — the
            // reasoning is ArrivalOpeningPlayTests', kept because this fixture runs the same tie-up.
            MooringCleats.Clear();
            var bollard = new GameObject("Bollard");
            bollard.transform.SetParent(_root.transform);
            bollard.transform.position = new Vector3(Ashore.x + 2f, Ashore.y, 0f);
            bollard.AddComponent<ShoreCleat>().Configure("fixture.bollard", elevationMeters: 1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            // ⚠ Unsubscribed by hand rather than through a Clear: EventBus.Clear<T>() takes every OTHER
            // listener down with it, which in a full suite is somebody else's fixture.
            EventBus.Unsubscribe<CabinEntered>(OnEntered);
            EventBus.Unsubscribe<CabinLeft>(OnLeft);

            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            MooringCleats.Clear();
            Interactables.Clear();
            GameServices.Save = null;
            GameServices.PlayerTransform = null;
            GameServices.Reset();
        }

        private void OnEntered(CabinEntered e) => _entered.Add(e);
        private void OnLeft(CabinLeft e) => _left.Add(e);

        private ArrivalOpening Build() => Build(new[] { Start, Berth });

        /// <summary>The same opening on an authored route. The straight run above is what every claim
        /// about the CABIN wants; a claim about riding a TURNING hull needs a corner in the water, and a
        /// fixture that asserted the ride on the straight run would be asserting it of a boat that never
        /// came about — a dead control.</summary>
        private ArrivalOpening Build(Vector2[] route)
        {
            GameServices.Save = new FakeSave();

            var go = new GameObject("ArrivalOpening");
            go.transform.SetParent(_root.transform);
            go.SetActive(false);
            var opening = go.AddComponent<ArrivalOpening>();
            opening.Configure(_skipper, route, Berth, BerthHeading, Ashore,
                              channelBedElevation: -4f);
            opening.ConfigurePilot(ArrivalPilot.Settings.Default);
            opening.ConfigureAlongside(FixtureAlongside());
            go.SetActive(true);
            _opening = opening;
            return opening;
        }

        private IEnumerator Until(Func<bool> reached, string what)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!reached() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(reached(), $"the passage never {what} within {TimeoutSeconds:F0} s. " + Where());
        }

        private string Where()
        {
            if (_opening == null) return "There is no opening.";
            string boat = _opening.Boat == null
                ? "no boat"
                : $"she is at ({_opening.Boat.transform.position.x:F1}, " +
                  $"{_opening.Boat.transform.position.y:F1}) making " +
                  $"{_opening.Boat.Velocity.magnitude:F2} m/s";
            return $"[{_opening.Current}/{_opening.Pilotage}] {boat}; the player is " +
                   (_opening.IsBelowDecks
                        ? $"BELOW at sole {_opening.CabinLocalPosition}"
                        : "ON DECK") +
                   $" at {_player.transform.position}.";
        }

        /// <summary>The hull's entity id — what the cabin signals name, so a fixture can tell HER cabin
        /// from a sister ship's.</summary>
        private EntityId HullId => _opening.Boat.gameObject.GetEntityId();

        /// <summary>⭐ The claim that has to hold on EVERY frame of the passage, not just at its ends:
        /// Armand keeps his wheel. A passenger who took the helm would close the cutaway (the occupancy
        /// law) and would be steering the boat that is supposed to be steering itself.</summary>
        private void AssertArmandKeepsTheHelm()
        {
            Assert.IsNull(GameServices.HelmControl,
                "the helm slot has been granted to somebody during the arrival — the passenger is not " +
                "piloting anything and nothing should have declared her to be. " + Where());
            Assert.IsFalse(ReferenceEquals(GameServices.Helm.PilotedHull, _opening.Boat),
                "the arrival hull has been declared as the PILOTED hull. " + Where());
        }

        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>THE JOURNEY.</b> New game → below on his def → walk his cabin → up the aft door → on
        /// deck → the come-alongside finishes → ashore with the controls.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOpening_StartsBelow_ComesUpThroughHerAftDoor_AndFinishesAlongside()
        {
            Assert.IsNotNull(_skipper, "the arrival skipper Def must exist for this to mean anything");
            ArrivalOpening opening = Build();

            // ---- 1. she starts below --------------------------------------------------------------
            Assert.IsTrue(opening.TryBegin(), "a fresh save must be brought in");
            Assert.IsNotNull(opening.Boat, "…and there is a boat under her");
            Assert.IsNotNull(opening.Cabin,
                "the cape islander carries a measured interior — if this is null the opening has " +
                "silently fallen back to the on-deck passage and nothing below is being tested");

            Assert.IsTrue(opening.IsBelowDecks, "the game did not open below decks. " + Where());
            Assert.AreEqual(1, _entered.Count, "the cabin publishes exactly one entry on the way in");
            Assert.AreEqual(HullId, _entered[0].HullId,
                "CabinEntered named a different hull — a listener would open the wrong boat's house");
            Assert.AreEqual(opening.Cabin.Level, _entered[0].Level,
                "the signal's level disagrees with the cabin's own");

            // ⭐ THE GATE, read off the component that owns the ruling. She is below and nobody has
            // declared her at a wheel, so the cutaway is asking to open this hull's house.
            Assert.IsNotNull(opening.CabinCutaway,
                "she is a mesh hull, so she must carry a cutaway to be asked");
            Assert.IsTrue(opening.CabinCutaway.OccupantIsBelow,
                "the cutaway does not think anybody is below on this hull — the gate is not engaged");
            AssertArmandKeepsTheHelm();

            // ---- 2. she can move about the cabin --------------------------------------------------
            Vector2 startSole = opening.CabinLocalPosition;
            Vector3 startWorldRelative = _player.transform.position - opening.Boat.transform.position;

            // ⚠ All four directions, not one. She starts AT the doorway, which is on the aft edge of the
            // sole by construction — so whichever screen direction happens to point aft at this heading is
            // walking her into the bulkhead she is standing against, and a fixture that pressed only that
            // one would call a working clamp a broken walk. (It did, first run: bow south, "up-screen" is
            // aft, 3 mm of travel.) What is under test is that she can move ABOUT the cabin.
            float furthestOnTheSole = 0f, furthestOnScreen = 0f;
            foreach (Vector2 dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                yield return WalkFor(dir, 0.35f);
                furthestOnTheSole = Mathf.Max(furthestOnTheSole,
                                              Vector2.Distance(opening.CabinLocalPosition, startSole));
                furthestOnScreen = Mathf.Max(
                    furthestOnScreen,
                    Vector3.Distance(_player.transform.position - opening.Boat.transform.position,
                                     startWorldRelative));
            }

            Assert.Greater(furthestOnTheSole, 0.15f,
                $"she never left {startSole} in any direction (now {opening.CabinLocalPosition}). " +
                Where());
            Assert.Greater(furthestOnScreen, 0.05f,
                "her sole position changed but her place on screen did not — the projection is not " +
                "reaching her transform");
            Assert.IsTrue(opening.IsBelowDecks, "walking about the cabin took her out of it. " + Where());
            AssertArmandKeepsTheHelm();

            // ⚠ …and the step ashore is refused from inside. Not a lock on the door: the offer simply is
            // not a true statement while she is below, and it is waiting the moment she comes up.
            Assert.IsFalse(opening.CanStepAshore, "the wharf is being offered to somebody inside a cabin");

            // ---- 3. up on deck, through his aft door ----------------------------------------------
            yield return WalkToTheDoor(opening);

            float reach = opening.CabinDoor.ReachMeters;
            float gap = Vector2.Distance(_player.transform.position,
                                         opening.CabinDoor.transform.position);
            Assert.LessOrEqual(gap, reach + 0.05f,
                $"she could not walk within reach of her own doorway: {gap:F2} m against {reach:F2}. " +
                Where());

            Assert.IsTrue(opening.WorkTheCabinDoor(), "the door refused the press. " + Where());

            // ⛔ THE ABSENCE OF A TELEPORT, MEASURED. Crossing the threshold changes which FRAME she is
            // placed in — sole to deck — and the two must join without a step. Sampled every frame from
            // the press until she is out, and the largest single-frame move is the claim.
            float worstJump = 0f;
            yield return WatchForTheDoorToResolve(opening, j => worstJump = Mathf.Max(worstJump, j));

            Assert.IsFalse(opening.IsBelowDecks, "the cue finished and she is still below. " + Where());
            Assert.AreEqual(1, _left.Count, "the cabin publishes exactly one exit on the way out");
            Assert.AreEqual(HullId, _left[0].HullId, "CabinLeft named a different hull");
            Assert.IsFalse(opening.CabinCutaway.OccupantIsBelow,
                "the cutaway still thinks she is below — the gate did not close with her level");

            Assert.Less(worstJump, 0.5f,
                $"she moved {worstJump:F2} m in one frame coming out of the cabin. Both intro teleports " +
                "died with #661 and continuity is the law: the deck seat is SEEDED from where she is " +
                "standing, so crossing the threshold must move nobody. " + Where());
            AssertArmandKeepsTheHelm();

            // ---- 4. …and S1 takes over untouched from there ----------------------------------------
            yield return Until(() => opening.Current == ArrivalOpening.Phase.Moored, "tied up");
            yield return Until(() => opening.CanStepAshore, "settled on her lines and offered the planks");
            AssertArmandKeepsTheHelm();

            Assert.IsTrue(opening.StepAshore(), "the offer was standing but the press was refused");
            yield return Until(() => opening.Current == ArrivalOpening.Phase.HandedOver,
                               "landed and handed the controls back");

            Assert.IsTrue(((FakeSave)GameServices.Save).GetFlag(ArrivalOpening.ArrivedFlagKey),
                "the arrival finished without recording that this player has been landed");
        }

        /// <summary>
        /// ⚠ <b>The cabin's state survives a region hop, and so must the cut.</b> Root-toggling IS how a
        /// region boundary is crossed, and a cabin that reset there would put the player back on deck
        /// mid-passage. Nothing new is published on the way back, either — a listener that re-ran its
        /// entry every boundary is the defect <c>CabinEntered</c>'s own remarks forbid.
        /// </summary>
        /// <summary>
        /// THE RETIREMENT MEASUREMENT ON THE INTRO (ADR 0041, fleet rollout PR 0). The cape's room has
        /// been geometry since #690, and the intro is the one place every player sees a cabin from the
        /// inside. Below decks in the shipped opening, how many things draw his cabin? Counted off the
        /// live objects and logged either way, and a plate of what a camera on the hull sees is written
        /// to the temporary cache (never into the repo) for the owner's eye.
        /// </summary>
        [UnityTest]
        public IEnumerator BelowDecks_TheIntroDrawsHisCabinFromExactlyOneSource_AndWritesAPlate()
        {
            Assert.IsNotNull(_skipper);
            ArrivalOpening opening = Build();
            Assert.IsTrue(opening.TryBegin());
            Assert.IsTrue(opening.IsBelowDecks, "the game did not open below decks. " + Where());
            yield return null;   // the cabin's LateUpdate has shown its cell; the cutaway has re-asserted
            yield return null;

            Transform boat = opening.Boat.transform;
            BelowDecksDrawSources.Count drawn = BelowDecksDrawSources.Measure(boat);
            Debug.Log($"[mesh-interiors-retirement] cape (the intro), below decks: {drawn}");

            // The plate first, so a red assertion still leaves the picture behind.
            BelowDecksDrawSources.WritePlate(boat, "intro-below-decks.png", metresTall: 16f, pxPerMetre: 48);

            Assert.IsTrue(drawn.MeshRoom,
                "his house is not cut open while she is below — the mesh room is not being shown. " + drawn);
            Assert.AreEqual(1, drawn.Total,
                "a converted hull must draw her cabin from ONE source; two means the sprite room is still " +
                "wired under the mesh room. " + drawn);
        }

        [UnityTest]
        public IEnumerator TogglingTheRootWhileSheIsBelow_LeavesHerBelow_AndSaysNothingNew()
        {
            ArrivalOpening opening = Build();
            Assert.IsTrue(opening.TryBegin());
            Assert.IsTrue(opening.IsBelowDecks);
            yield return null;

            int entered = _entered.Count, left = _left.Count;

            opening.Boat.gameObject.SetActive(false);
            yield return null;
            opening.Boat.gameObject.SetActive(true);
            yield return null;

            Assert.IsTrue(opening.IsBelowDecks, "a region hop put her back on deck. " + Where());
            Assert.IsTrue(opening.CabinCutaway.OccupantIsBelow,
                "the cutaway closed her house around a player who never left it");
            Assert.AreEqual(entered, _entered.Count, "a re-assert published a second CabinEntered");
            Assert.AreEqual(left, _left.Count, "a re-assert published a CabinLeft");
        }

        /// <summary>
        /// The other half of the ruling, and the one that cannot be read off the cape: a passenger below
        /// gets the cut BECAUSE she is not at the wheel. Declare her piloting this hull and the cutaway
        /// must close the house again without anybody moving.
        /// </summary>
        [UnityTest]
        public IEnumerator IfThePassengerTookThisWheel_TheHouseCloses_ThoughSheIsStillBelow()
        {
            ArrivalOpening opening = Build();
            Assert.IsTrue(opening.TryBegin());
            yield return null;

            Assert.IsTrue(opening.CabinCutaway.OccupantIsBelow);

            GameServices.Helm.SetPilotedHull(opening.Boat);
            EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
            yield return null;

            Assert.IsTrue(opening.IsBelowDecks, "declaring a helm must not move her out of the cabin");
            Assert.IsFalse(opening.CabinCutaway.RequestedCut.Opens,
                "the house is still cut open for somebody who is steering her — the occupancy law (#642) " +
                "says at the helm is exterior only");

            GameServices.Helm.SetPilotedHull(null);
            EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));
        }

        /// <summary>
        /// ⭐ <b>THE SEAM, ON THE ARRIVAL</b> (ADR 0043, PR 0). The journey above walks her by calling
        /// <c>WalkTheCabin</c> itself — the component API, because a keypress is undeliverable here. This
        /// walks her the way the KEYS do: a <see cref="HeldDeckIntents"/> is handed to the arrival and
        /// its own <c>Update</c> reads it and steps the cabin. If the seam is not consulted she does not
        /// move, and <see cref="HeldIntents{T}.Reads"/> says whether it was asked at all.
        /// </summary>
        [UnityTest]
        public IEnumerator BelowDecks_AHeldIntentWalksHer_ThroughTheArrivalsOwnUpdate()
        {
            Assert.IsNotNull(_skipper, "the arrival skipper Def must exist for this to mean anything");
            ArrivalOpening opening = Build();
            var held = new HeldDeckIntents();
            opening.ConfigureWalkInput(held);

            Assert.IsTrue(opening.TryBegin(), "a fresh save must be brought in");
            Assert.IsTrue(opening.IsBelowDecks, "the game did not open below decks. " + Where());
            yield return null;   // the read is in Update: the first idle frame lands nothing

            Vector2 startSole = opening.CabinLocalPosition;
            float furthestOnTheSole = 0f;
            foreach (Vector2 dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                held.Walk(dir);
                float until = Time.realtimeSinceStartup + 0.35f;
                while (Time.realtimeSinceStartup < until) yield return null;
                furthestOnTheSole = Mathf.Max(furthestOnTheSole,
                                              Vector2.Distance(opening.CabinLocalPosition, startSole));
            }

            Assert.Greater(held.Reads, 0, "the arrival never asked its source — the seam is not wired into Update.");
            Assert.Greater(furthestOnTheSole, 0.15f,
                $"a held intent never moved her off {startSole} in any direction (now " +
                $"{opening.CabinLocalPosition}) — the arrival read the source but did not walk on it. " + Where());
            Assert.IsTrue(opening.IsBelowDecks, "walking about the cabin took her out of it. " + Where());
            AssertArmandKeepsTheHelm();

            // Released, she stands: a released intent is a zero, the same as a released key.
            held.Release();
            yield return null;
            Vector2 rest = opening.CabinLocalPosition;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0f, Vector2.Distance(rest, opening.CabinLocalPosition), 1e-4f,
                "she kept walking after the intent was released. " + Where());
        }

        // =============================================================================================
        //  driving her
        // =============================================================================================

        /// <summary>Hold a walk direction for <paramref name="seconds"/> of wall clock, handing the
        /// component the input the keys would. ⚠ Real seconds and not frames: the step is metres per
        /// SECOND, so a frame count would walk a different distance on every machine.</summary>
        private IEnumerator WalkFor(Vector2 input, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                _opening.WalkTheCabin(input, Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>Walk her to the threshold, steering by the door's LIVE position — it follows the hull
        /// round as she turns, so a direction taken once would be stale within a second.</summary>
        private IEnumerator WalkToTheDoor(ArrivalOpening opening)
        {
            BoatCabinDoor door = opening.CabinDoor;
            Assert.IsNotNull(door, "her cabin has no door to come out of");

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Vector2 toDoor = (Vector2)door.transform.position - (Vector2)_player.transform.position;
                if (toDoor.magnitude <= door.ReachMeters * 0.8f) yield break;
                opening.WalkTheCabin(toDoor.normalized, Time.deltaTime);
                yield return null;
            }
            // Not a failure by itself — the caller asserts the reach, and says what it measured.
        }

        /// <summary>Let the leaf run, sampling her per-frame travel so the caller can assert that crossing
        /// the threshold moved nobody.</summary>
        private IEnumerator WatchForTheDoorToResolve(ArrivalOpening opening, Action<float> jump)
        {
            Vector3 last = _player.transform.position;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (opening.IsBelowDecks && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                jump(Vector3.Distance(_player.transform.position, last));
                last = _player.transform.position;
            }
            // One more frame after the swap: the seat changes frame in LateUpdate, and the step it could
            // introduce is the one this is here to catch.
            yield return null;
            jump(Vector3.Distance(_player.transform.position, last));
        }

        // =============================================================================================
        //  ⭐⭐ ON DECK — the owner's blocker, playtest 2026-09-04
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>SHE WALKS HIS DECK, UNDER WAY.</b> The owner's second sentence: <i>"the player is unable
        /// to walk on the boat deck in the new intro, going outside locks them in place."</i>
        ///
        /// <para>Nothing was broken — the walk was never built. The arrival ran a walk BELOW and none
        /// ABOVE, and her place on deck was written once, at the threshold, and only read afterwards.
        /// This is the twin of the cabin walk above, one deck up, and it is deliberately taken while she
        /// is still <b>making way</b>: a passenger clamped to a hull that is standing still proves nothing
        /// about the frame she is clamped in.</para>
        ///
        /// <para><b>⚠ The travel is asserted in the DECK frame, not on screen.</b> A boat doing five knots
        /// carries her a long way without her taking a step, and her boat-relative screen offset moves as
        /// the hull TURNS under her too — so both of those measure his passage, not her walk. The deck
        /// frame is heading-independent by construction, which is exactly what makes it hers.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator OnDeck_SheWalksHisDeckUnderWay_AndTheWalkIsClampedToHisPlanking()
        {
            Assert.IsNotNull(_skipper, "the arrival skipper Def must exist for this to mean anything");
            ArrivalOpening opening = Build();

            Assert.IsTrue(opening.TryBegin(), "a fresh save must be brought in");
            Assert.IsTrue(opening.IsBelowDecks, "the game did not open below decks. " + Where());
            yield return ComeUpOnDeck(opening);

            Assert.IsTrue(opening.CanWalkTheDeck,
                "the cape islander carries measured deck polygons (Data/Boats/Decks/CapeIslanderIso), so " +
                "this must be true the moment she is skinned — false means her deck never reached the " +
                "walk and nothing below is testing one. " + Where());
            Assert.AreNotEqual(ArrivalOpening.Phase.Moored, opening.Current,
                "she is tied up already, so this run says nothing about walking a hull under way. " +
                Where());

            Vector2 startOnDeck = opening.DeckLocalPosition;
            Vector2 startOnHim = HerPlaceOnHisPicture(opening);
            float fastest = 0f, furthestOnDeck = 0f, furthestOnHim = 0f;

            // ⚠ All four directions, and for the cabin walk's own reason: she comes out AT the doorway,
            // which is on the aft edge of the cockpit by construction, so whichever screen direction
            // happens to point aft at this heading walks her into the bulkhead she is standing against.
            // A fixture that pressed only that one would call a working clamp a broken walk.
            foreach (Vector2 dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                yield return WalkTheDeckFor(dir, 0.35f, s => fastest = Mathf.Max(fastest, s));
                furthestOnDeck = Mathf.Max(furthestOnDeck,
                                           Vector2.Distance(opening.DeckLocalPosition, startOnDeck));
                furthestOnHim = Mathf.Max(furthestOnHim,
                                          Vector2.Distance(HerPlaceOnHisPicture(opening), startOnHim));
                AssertSheIsOnHisPlanking(opening);
            }

            Assert.Greater(fastest, 0.5f,
                "the hull never made half a metre a second while she walked, so this did not test a " +
                "MOVING deck — the claim is about a walk under way. " + Where());
            Assert.Greater(furthestOnDeck, 0.3f,
                $"she never left {startOnDeck} on his deck in any direction (now " +
                $"{opening.DeckLocalPosition}). That is the owner's defect exactly: outside, she is " +
                "locked in place. " + Where());
            Assert.Greater(furthestOnHim, 0.2f,
                "her deck position changed and her place on his PICTURE did not — the projection is not " +
                "reaching her transform, so the seat is still placing her from somewhere else. " + Where());
            AssertArmandKeepsTheHelm();
        }

        /// <summary>
        /// ⭐ <b>THE SEAM, ON DECK</b> (ADR 0043). The twin of the cabin's seam test: the walk above calls
        /// <c>WalkTheDeck</c> itself, this one hands the arrival a <see cref="HeldDeckIntents"/> and lets
        /// its own <c>Update</c> find it. The keys that walk her about his cabin must be the keys that
        /// walk her about his deck — they are one walk with a different floor under it, and that is what
        /// the owner pressed when he came up through the door.
        /// </summary>
        [UnityTest]
        public IEnumerator OnDeck_AHeldIntentWalksHer_ThroughTheArrivalsOwnUpdate()
        {
            Assert.IsNotNull(_skipper, "the arrival skipper Def must exist for this to mean anything");
            ArrivalOpening opening = Build();
            var held = new HeldDeckIntents();
            opening.ConfigureWalkInput(held);

            Assert.IsTrue(opening.TryBegin(), "a fresh save must be brought in");
            yield return ComeUpOnDeck(opening);
            Assert.IsTrue(opening.CanWalkTheDeck, "she has no measured deck to walk. " + Where());

            int readsBelow = held.Reads;
            Vector2 start = opening.DeckLocalPosition;
            float furthest = 0f;
            foreach (Vector2 dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                held.Walk(dir);
                float until = Time.realtimeSinceStartup + 0.35f;
                while (Time.realtimeSinceStartup < until) yield return null;
                furthest = Mathf.Max(furthest, Vector2.Distance(opening.DeckLocalPosition, start));
            }

            Assert.Greater(held.Reads, readsBelow,
                "the arrival stopped asking its source the moment she came up — the seam ends at the " +
                "cabin door, which is the asymmetry this PR exists to remove. " + Where());
            Assert.Greater(furthest, 0.3f,
                $"a held intent never moved her off {start} in any direction (now " +
                $"{opening.DeckLocalPosition}) — the arrival read the source but did not walk on it. " +
                Where());
            AssertSheIsOnHisPlanking(opening);

            // Released, she stands: a released intent is a zero, the same as a released key. ⚠ And the
            // hull is still turning and drifting under her while this is asserted, which is the point —
            // her DECK point may not move because HIS does.
            held.Release();
            yield return null;
            Vector2 rest = opening.DeckLocalPosition;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0f, Vector2.Distance(rest, opening.DeckLocalPosition), 1e-4f,
                "she kept walking his deck after the intent was released. " + Where());
        }

        /// <summary>
        /// ⭐⭐ <b>HIS TURN CARRIES HER; IT DOES NOT SLIDE HER.</b> The passenger presses nothing for the
        /// whole of a ninety-degree turn: her place on his DECK must not move by a centimetre, and her
        /// place in the WORLD must move by tens of metres. Those two together are what "riding a hull"
        /// means, and only the deck frame can state them without one hiding the other.
        ///
        /// <para><b>⚠ Run on a DOG-LEG route and on the ON-DECK opening.</b> The straight run the cabin
        /// fixtures use never turns more than a few degrees — a ride test on a hull that does not come
        /// about is a dead control, and the assertion below says so out loud if she never turns. Starting
        /// on deck rather than below also exercises the other seed: the AUTHORED offset, clamped onto his
        /// planking, which is what places a passenger on a hull whose opening has no cabin.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator OnDeck_TurningHisHullCarriesHer_ItDoesNotSlideHer()
        {
            Assert.IsNotNull(_skipper, "the arrival skipper Def must exist for this to mean anything");
            ArrivalOpening opening = Build(new[] { new Vector2(-60f, 60f), new Vector2(0f, 60f), Berth });
            opening.ConfigureCabin(false);

            Assert.IsTrue(opening.TryBegin(), "a fresh save must be brought in");
            Assert.IsFalse(opening.IsBelowDecks,
                "this fixture asked for the on-deck opening and was given the cabin. " + Where());
            yield return null;

            Assert.IsTrue(opening.CanWalkTheDeck, "she has no measured deck to stand on. " + Where());
            AssertSheIsOnHisPlanking(opening);

            // ⛔ WALK HER OFF THE AUTHORED SPOT FIRST, and this is not decoration — it is what stops the
            // assertion below from being a DEAD CONTROL. A deck walk that had never been seeded would
            // report her at the hull's origin, which on this hull is INSIDE the cockpit polygon: the
            // clamp would be happy, the drift would be exactly zero, and the test would go green on a
            // walk that does not exist. A point she demonstrably walked to cannot be produced that way.
            Vector2 authored = opening.DeckLocalPosition;
            float walked = 0f;
            foreach (Vector2 dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                yield return WalkTheDeckFor(dir, 0.3f);
                walked = Mathf.Max(walked, Vector2.Distance(opening.DeckLocalPosition, authored));
            }
            Assert.Greater(walked, 0.2f,
                $"she never moved off {authored} in any direction, so the point this test is about is " +
                "not one she walked to and the drift below would prove nothing. " + Where());

            Vector2 stood = opening.DeckLocalPosition;
            Vector3 worldAtStart = _player.transform.position;
            float rotAtStart = opening.Boat.transform.eulerAngles.z;

            float turned = 0f, rode = 0f, worstDrift = 0f;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (turned < 60f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;      // ⚠ NOTHING is pressed. Every metre covered here is his.
                worstDrift = Mathf.Max(worstDrift,
                                       Vector2.Distance(opening.DeckLocalPosition, stood));
                turned = Mathf.Abs(Mathf.DeltaAngle(rotAtStart, opening.Boat.transform.eulerAngles.z));
                rode = Mathf.Max(rode, Vector3.Distance(_player.transform.position, worldAtStart));
            }

            Assert.Greater(turned, 20f,
                $"the hull only came about {turned:F1}\u00b0 in {TimeoutSeconds:F0} s, so nothing here was " +
                "tested against a turn — this is a dead control, not a green. " + Where());
            Assert.Greater(rode, 5f,
                "she never moved with the boat at all: she is not riding his hull, she is standing in " +
                "the sea where he left her. " + Where());
            Assert.Less(worstDrift, 0.02f,
                $"he came about {turned:F0}\u00b0 under a passenger who pressed nothing and it slid her " +
                $"{worstDrift:F2} m across his deck. Her place is HULL-LOCAL; a turn must carry it, not " +
                "move it. " + Where());
            AssertSheIsOnHisPlanking(opening);
        }

        // =============================================================================================
        //  walking her about his deck
        // =============================================================================================

        /// <summary>Come up the way the player does: walk to his aft door, press it, and let its own cue
        /// run out. One frame more at the end, so the deck seat has been seeded and taken.</summary>
        private IEnumerator ComeUpOnDeck(ArrivalOpening opening)
        {
            yield return WalkToTheDoor(opening);
            Assert.IsTrue(opening.WorkTheCabinDoor(), "the door refused the press. " + Where());
            yield return Until(() => !opening.IsBelowDecks, "came up on deck through his aft door");
            yield return null;
        }

        /// <summary>Hold a walk direction on his DECK for <paramref name="seconds"/> of wall clock,
        /// handing the component the input the keys would, and report the hull's speed while she does —
        /// so a run on a boat that had already stopped cannot pass as a walk under way. ⚠ Real seconds
        /// and not frames: the step is metres per SECOND.</summary>
        private IEnumerator WalkTheDeckFor(Vector2 input, float seconds, Action<float> sawHullSpeed = null)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                _opening.WalkTheDeck(input, Time.deltaTime);
                if (sawHullSpeed != null && _opening.Boat != null)
                    sawHullSpeed(_opening.Boat.Velocity.magnitude);
                yield return null;
            }
        }

        /// <summary>
        /// ⛔ <b>SHE IS ON HIS PLANKING, ASKED OF THE HULL.</b> Her deck point is handed back to the
        /// def's own <c>ClampToWalkable</c>: a point already on a walkable area comes back unchanged, and
        /// one that is not comes back moved to the nearest outline. So "the clamp has nothing to do" IS
        /// the claim that she has not walked off the boat, stated by the same authority that would have
        /// stopped her.
        /// </summary>
        private void AssertSheIsOnHisPlanking(ArrivalOpening opening)
        {
            BoatDeckDef deck = BoatDeckAreas.Resolve(opening.Boat.gameObject);
            Assert.IsNotNull(deck, "his hull carries no deck areas to be clamped to. " + Where());

            int hint = -1;
            Vector2 her = opening.DeckLocalPosition;
            Vector2 onIt = deck.ClampToWalkable(her, ref hint, out float _);
            Assert.AreEqual(0f, Vector2.Distance(her, onIt), 1e-3f,
                $"she is standing at {her}, which his own clamp pulls back to {onIt} — she has walked " +
                "off his deck. " + Where());
        }

        /// <summary>
        /// Where she is standing on his PICTURE, in his deck frame — her boat-relative screen offset with
        /// the hull's drawn heading taken back out (<c>DeckWalkController.WorldToDeckFrame</c>).
        ///
        /// <para><b>⚠ Why not the raw boat-relative offset.</b> A turning hull sweeps that offset round
        /// under a passenger who has not moved a muscle, so a fixture measuring it would credit his helm
        /// with her walking. Taking the rotation out leaves only what her transform did on his deck.</para>
        /// </summary>
        private Vector2 HerPlaceOnHisPicture(ArrivalOpening opening)
        {
            var host = opening.Boat.GetComponent<BoatHullPresenterHost>();
            IBoatHullPresenter hull = host != null ? host.Presenter : null;
            if (hull == null) hull = BoatHullPresenterHost.Resolve(opening.Boat.gameObject);
            float heading = hull != null
                ? hull.DrawnHeadingDegrees()
                : DirectionalBoatSprite.HeadingDegreesFromBow(opening.Boat.transform.up);
            Vector2 relative = (Vector2)_player.transform.position
                             - (Vector2)opening.Boat.transform.position;
            return DeckWalkController.WorldToDeckFrame(relative, heading);
        }
    }
}
