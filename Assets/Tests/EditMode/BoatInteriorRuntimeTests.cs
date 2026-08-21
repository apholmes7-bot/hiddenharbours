using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The runtime half of ADR 0038</b> — the comfort clamp, the layer swap, the level rules and the
    /// door cue, held to the four behaviours the ADR ruled.
    ///
    /// <para><b>What is EditMode-able here and what is not.</b> The pose math is pure and belongs here
    /// entirely. The components are driven through their own API — <c>Configure</c> applies immediately
    /// precisely so this works — but their <c>OnEnable</c>/<c>OnDisable</c> hooks do NOT run in EditMode
    /// on a plain MonoBehaviour, so the self-registration on the interact seam and the survival of a
    /// root toggle are proved in <c>BoatInteriorPlayTests</c> and deliberately not asserted here: an
    /// EditMode assertion about <c>OnEnable</c> asserts something the shipped game never does, and the
    /// "nothing happened" direction would pass for exactly the wrong reason.</para>
    /// </summary>
    public class BoatInteriorRuntimeTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // The iso fleet's own baked rock, as DeckRiderVisual carries it — the numbers a hull's sheets
        // were drawn at, not a tuning of this feature.
        private const float DeckRoll = 5f;
        private const float HeavePixels = 1.6f;
        private const float PitchLift = 0.02f;
        private const float Ppu = 32f;
        private const float Crest = 90f;
        private const float Quarter = 45f;    // both sin and cos non-zero: every channel is live

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // =====================================================================================
        //  1 · MOTION — the comfort clamp (ADR 0038 proposal 1)
        // =====================================================================================

        [Test]
        public void ScaleZero_IsDeadFlat_NotMerelyNearlyLevel()
        {
            // The accessibility floor the ruling names. It has to be EXACT: "very nearly level" on a
            // picture that fills the frame is the nausea this setting exists to remove.
            DeckRidePose pose = BoatInteriorPoseMath.Ride(Quarter, 1.75f, DeckRoll, HeavePixels,
                                                          PitchLift, Ppu, 0f);

            Assert.AreEqual(0f, pose.RollDegrees, "scale 0 must not lean the cabin at all");
            Assert.AreEqual(0f, pose.LiftMeters, "…nor lift it — the displaced ride is scaled too");
        }

        [Test]
        public void ScaleZero_IsDeadFlatOnTheLegacyTiltPathToo()
        {
            DeckRidePose pose = BoatInteriorPoseMath.RideTilt(7.5f, 1.75f, 0f);

            Assert.AreEqual(0f, pose.RollDegrees);
            Assert.AreEqual(0f, pose.LiftMeters);
        }

        [Test]
        public void ScaleOne_IsExactlyTheHullsOwnPose()
        {
            // The full-fidelity ceiling: what the kit bakes. Compared against DeckRideMath directly with
            // footing 1 — this is the claim that the cabin takes the WHOLE lean because it is bolted to
            // the hull, and that nothing else has been folded in on the way.
            const float ride = 1.75f;
            DeckRidePose hull = DeckRideMath.RidingHull(
                DeckRideMath.Ride(Quarter, DeckRoll, HeavePixels, PitchLift, Ppu, 1f, 1f), ride, 1f);

            DeckRidePose cabin = BoatInteriorPoseMath.Ride(Quarter, ride, DeckRoll, HeavePixels,
                                                          PitchLift, Ppu, 1f);

            Assert.AreEqual(hull.RollDegrees, cabin.RollDegrees, 1e-6f);
            Assert.AreEqual(hull.LiftMeters, cabin.LiftMeters, 1e-6f);
        }

        [Test]
        public void TheCabinTakesTheWholeLean_ItDoesNotBrace()
        {
            // A fisher braces; a wheelhouse is part of the hull. If CabinFooting01 ever drifted off 1
            // the room would lean less than the boat it is inside, which is the registration failing
            // in the one way that still looks like a picture.
            Assert.AreEqual(1f, BoatInteriorPoseMath.CabinFooting01);

            DeckRidePose cabin = BoatInteriorPoseMath.Ride(Crest, 0f, DeckRoll, HeavePixels,
                                                          PitchLift, Ppu, 1f);
            Assert.AreEqual(DeckRoll, cabin.RollDegrees, 1e-5f,
                            "at the crest sin = 1, so the whole baked roll must arrive");
        }

        [Test]
        public void TheScaleShrinksTheMotion_ItDoesNotChangeItsShape()
        {
            // One scalar on every channel: the interior's motion is the hull's motion made smaller and
            // never a different motion. A per-channel cap would have changed the SHAPE of the ride and
            // broken the registration the sheets are baked for.
            const float s = 0.45f;   // the ruled default
            DeckRidePose full = BoatInteriorPoseMath.Ride(Quarter, 1.75f, DeckRoll, HeavePixels,
                                                          PitchLift, Ppu, 1f);
            DeckRidePose clamped = BoatInteriorPoseMath.Ride(Quarter, 1.75f, DeckRoll, HeavePixels,
                                                             PitchLift, Ppu, s);

            Assert.AreEqual(full.RollDegrees * s, clamped.RollDegrees, 1e-6f);
            Assert.AreEqual(full.LiftMeters * s, clamped.LiftMeters, 1e-6f);
        }

        [Test]
        public void ThePoseIsDeterministic_SameInputsSameBits()
        {
            // Asked twice, answered identically — no state, no clock, no accumulation. Bit equality is
            // the right bar HERE (one function, one transcription, same machine); it is only unreachable
            // across two independent transcriptions of a formula.
            for (float phase = 0f; phase < 360f; phase += 17f)
            {
                DeckRidePose a = BoatInteriorPoseMath.Ride(phase, 1.75f, DeckRoll, HeavePixels,
                                                          PitchLift, Ppu, 0.45f);
                DeckRidePose b = BoatInteriorPoseMath.Ride(phase, 1.75f, DeckRoll, HeavePixels,
                                                          PitchLift, Ppu, 0.45f);
                Assert.AreEqual(a.RollDegrees, b.RollDegrees);
                Assert.AreEqual(a.LiftMeters, b.LiftMeters);
            }
        }

        [Test]
        public void AnOutOfRangeScaleIsClamped_NotObeyed()
        {
            // [Range] is an inspector courtesy and a hand-edited YAML ignores it. A 2 would have the
            // cabin out-rocking the hull she lives in; a −1 would rock her against it.
            DeckRidePose one = BoatInteriorPoseMath.Ride(Quarter, 1.75f, DeckRoll, HeavePixels,
                                                         PitchLift, Ppu, 1f);
            DeckRidePose two = BoatInteriorPoseMath.Ride(Quarter, 1.75f, DeckRoll, HeavePixels,
                                                         PitchLift, Ppu, 2f);
            DeckRidePose neg = BoatInteriorPoseMath.Ride(Quarter, 1.75f, DeckRoll, HeavePixels,
                                                         PitchLift, Ppu, -1f);

            Assert.AreEqual(one.RollDegrees, two.RollDegrees, 1e-6f);
            Assert.AreEqual(one.LiftMeters, two.LiftMeters, 1e-6f);
            Assert.AreEqual(0f, neg.RollDegrees);
            Assert.AreEqual(0f, neg.LiftMeters);
        }

        [Test]
        public void ACalmHullGetsAStillCabin()
        {
            // No cycle to publish is a calm sea, a hull ashore, or the ride switched off — all of which
            // are a cabin standing still, not a cabin swaying on a phantom.
            DeckRidePose pose = BoatInteriorPoseMath.Ride(DeckRideMath.LevelPhaseDegrees, 0f, DeckRoll,
                                                          HeavePixels, PitchLift, Ppu, 1f);
            Assert.AreEqual(0f, pose.RollDegrees);
            Assert.AreEqual(0f, pose.LiftMeters);
        }

        [Test]
        public void ANonFiniteRideParksTheCabinRatherThanLaunchingIt()
        {
            DeckRidePose nan = BoatInteriorPoseMath.Ride(Quarter, float.NaN, DeckRoll, HeavePixels,
                                                         PitchLift, Ppu, 1f);
            DeckRidePose cycleOnly = BoatInteriorPoseMath.Ride(Quarter, 0f, DeckRoll, HeavePixels,
                                                               PitchLift, Ppu, 1f);
            Assert.AreEqual(cycleOnly.LiftMeters, nan.LiftMeters, 1e-6f,
                            "a NaN ride must drop out, leaving the rock cycle alone");
        }

        [Test]
        public void TheLegacyTiltPath_LeansByTheTiltTheHullIsApplying()
        {
            DeckRidePose pose = BoatInteriorPoseMath.RideTilt(8f, 0f, 0.5f);
            Assert.AreEqual(4f, pose.RollDegrees, 1e-6f);

            DeckRidePose still = BoatInteriorPoseMath.RideTilt(0f, 0f, 1f);
            Assert.AreEqual(0f, still.RollDegrees, "a hull drawing nothing gets a cabin drawing nothing");
        }

        // =====================================================================================
        //  THE TUNABLE — declared, defaulted, and PRESENT IN THE SHIPPED ASSET (#600's law)
        // =====================================================================================

        [Test]
        public void TheRuledDefaultIs045()
        {
            Assert.AreEqual(0.45f, GameConfig.DefaultInteriorRockScale, 1e-6f,
                            "ADR 0038 proposal 1 rules the default; changing it is an owner call");
        }

        [Test]
        public void TheShippedAssetCarriesTheRuledValue_NotJustTheKey()
        {
            // GameConfigAssetCoverageTests already proves the KEY is there (a declared field the YAML
            // omits reads as a silent zero — here, a permanently dead-flat cabin nobody can explain).
            // This pins the VALUE, which coverage by construction cannot: 0.45 is a ruling.
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                       ConfigAssetPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"{ConfigAssetPath} is missing");

            string key = null;
            foreach (string line in File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (!t.StartsWith("InteriorRockScale:", System.StringComparison.Ordinal)) continue;
                key = t.Substring("InteriorRockScale:".Length).Trim();
                break;
            }

            Assert.IsNotNull(key, $"{ConfigAssetPath} carries no InteriorRockScale key");
            Assert.IsTrue(float.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture,
                                         out float value), $"unparseable InteriorRockScale '{key}'");
            Assert.AreEqual(GameConfig.DefaultInteriorRockScale, value, 1e-6f);
        }

        [Test]
        public void GameServicesFallsBackToTheRuledDefault_AndClampsWhatItIsGiven()
        {
            GameServices.Config = null;
            Assert.AreEqual(GameConfig.DefaultInteriorRockScale, GameServices.InteriorRockScale, 1e-6f,
                            "an unwired rig must draw the cabin the shipped asset does");

            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            GameServices.Config = config;

            config.InteriorRockScale = 2f;
            Assert.AreEqual(1f, GameServices.InteriorRockScale, 1e-6f);

            config.InteriorRockScale = -3f;
            Assert.AreEqual(0f, GameServices.InteriorRockScale, 1e-6f);
        }

        // =====================================================================================
        //  3 · OCCLUSION — the layer swap (ADR 0038 proposal 3)
        // =====================================================================================

        [Test]
        public void OutsideHerSheDrawsTheExterior_InsideHerSheDrawsTheRoom()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f), ("below_sole", 0.55f)));

            Assert.IsFalse(rig.Interior.IsInside);
            Assert.IsTrue(rig.Interior.ExteriorDrawn, "a cabin nobody is in shows her outside");
            Assert.IsFalse(rig.Interior.InteriorDrawn);
            Assert.IsTrue(rig.Interior.ExactlyOneLayerOn);

            Assert.IsTrue(rig.Interior.TryEnter(0));
            Assert.IsFalse(rig.Interior.ExteriorDrawn, "the occluding draw goes OFF, it is not sorted behind");
            Assert.IsTrue(rig.Interior.InteriorDrawn);
            Assert.IsTrue(rig.Interior.ExactlyOneLayerOn);
        }

        [Test]
        public void TheTwoLayersAreNeverCoVisible_WhichIsWhatMakesTheScaleSafe()
        {
            // The coupling ADR 0038 ruled proposals 1 and 3 on together: the interior and the exterior
            // are posed differently, and that is only harmless because they are never both on screen.
            Rig rig = NewRig(Levels(("house_sole", 1.78f), ("below_sole", 0.55f)));

            foreach (int level in new[] { 0, 1, 0 })
            {
                rig.Interior.TryExit();
                Assert.IsTrue(rig.Interior.ExactlyOneLayerOn);
                rig.Interior.TryEnter(level);
                Assert.IsTrue(rig.Interior.ExactlyOneLayerOn);
                Assert.IsFalse(rig.Interior.ExteriorDrawn && rig.Interior.InteriorDrawn);
            }
        }

        [Test]
        public void TheFittingsRideTheInterior()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));

            Assert.IsFalse(rig.Fittings.activeSelf);
            rig.Interior.TryEnter(0);
            Assert.IsTrue(rig.Fittings.activeSelf);
            rig.Interior.TryExit();
            Assert.IsFalse(rig.Fittings.activeSelf);
        }

        // =====================================================================================
        //  LEVELS — a layer, not a place
        // =====================================================================================

        [Test]
        public void SteppingOutResetsTheLevel()
        {
            // ADR 0036's rule carried onto boats: a boat that remembered "below" would open on her
            // forepeak the next time you stepped aboard.
            Rig rig = NewRig(Levels(("house_sole", 1.78f), ("below_sole", 0.55f)));

            rig.Interior.TryEnter(0);
            Assert.IsTrue(rig.Interior.TryGoToLevel(1));
            Assert.AreEqual(1, rig.Interior.Level);

            Assert.IsTrue(rig.Interior.TryExit());
            Assert.AreEqual(0, rig.Interior.Level);

            rig.Interior.TryEnter(0);
            Assert.AreEqual(0, rig.Interior.Level, "she opens on the level you walked in onto");
        }

        [Test]
        public void ACompanionwayIsRefusedFromTheWharf()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f), ("below_sole", 0.55f)));
            Assert.IsFalse(rig.Interior.TryGoToLevel(1), "you cannot take a companionway from outside");
            Assert.AreEqual(0, rig.Interior.Level);
        }

        [Test]
        public void AnUnmeasuredHullIsInertRatherThanBroken()
        {
            // Absence is data: most of the fleet has never been measured, and a hull with no def must
            // simply have no inside — never an error, never a half-swapped picture.
            Rig rig = NewRig(null, def: false);

            Assert.IsFalse(rig.Interior.HasInterior);
            Assert.IsFalse(rig.Interior.TryEnter(0));
            Assert.IsFalse(rig.Interior.IsInside);
            Assert.IsTrue(rig.Interior.ExteriorDrawn);
        }

        [Test]
        public void AnUnusableLevelIsRefused()
        {
            // A sole with two points is a measurement that did not finish; entering it blind would draw
            // a room with no floor and look like a bug in the sheets.
            var levels = new[]
            {
                new BoatInteriorLevel { Id = "house_sole", SoleZMeters = 1.78f, Outline = Square() },
                new BoatInteriorLevel { Id = "stub", SoleZMeters = 0.55f, Outline = new Vector2[2] },
            };
            Rig rig = NewRig(levels);

            Assert.IsTrue(rig.Interior.IsUsableLevel(0));
            Assert.IsFalse(rig.Interior.IsUsableLevel(1));
            Assert.IsFalse(rig.Interior.TryEnter(1));
            Assert.IsFalse(rig.Interior.TryEnter(7), "and an index off the end is refused too");
        }

        [Test]
        public void TheSillNamesTheLevelYouWalkInOnto()
        {
            // The def carries the threshold's z precisely so this is DATA — the sport fishers' sill sits
            // at mezzanine height so you walk in level, and a constant 0 that happens to be right on the
            // lobster boats would put them in the bilge.
            Rig rig = NewRig(Levels(("below_sole", 0.55f), ("house_sole", 1.78f), ("helm_deck", 2.23f)));

            Assert.AreEqual(1, rig.Interior.LevelIndexAtHeight(1.78f));
            Assert.AreEqual(0, rig.Interior.LevelIndexAtHeight(0.6f));
            Assert.AreEqual(2, rig.Interior.LevelIndexAtHeight(2.3f));
            Assert.AreEqual(0, rig.Interior.LevelIndexAtHeight(-4f), "below every sole is the deepest one");
        }

        [Test]
        public void AHullWithNoLevelsNamesNone()
        {
            Rig rig = NewRig(null, def: false);
            Assert.AreEqual(-1, rig.Interior.LevelIndexAtHeight(1.78f));
        }

        // =====================================================================================
        //  THE DOOR — the cue is declared, not invented
        // =====================================================================================

        [Test]
        public void TheCueTimingComesFromTheDef()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));
            Assert.AreEqual(0.56f, rig.Door.CueSeconds, 1e-5f, "8 baked frames at 70 ms, as the kit bakes");
        }

        [Test]
        public void TheLeafMovesBeforeTheRoomAppears()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));

            Assert.IsTrue(rig.Door.TryUse());
            Assert.IsTrue(rig.Door.IsCueing);
            Assert.IsFalse(rig.Interior.IsInside, "the swap lands at the END of the cue, not under the hand");

            rig.Door.Tick(0.3f);
            Assert.IsTrue(rig.Door.IsCueing);
            Assert.IsFalse(rig.Interior.IsInside);

            rig.Door.Tick(0.3f);
            Assert.IsFalse(rig.Door.IsCueing);
            Assert.IsTrue(rig.Interior.IsInside);
            Assert.IsTrue(rig.Interior.InteriorDrawn);
        }

        [Test]
        public void ADoorAlreadyMovingRefusesASecondPress()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));

            Assert.IsTrue(rig.Door.TryUse());
            Assert.IsFalse(rig.Door.IsAvailable, "a doorway you can stutter ends up in both states at once");
            Assert.IsFalse(rig.Door.TryUse());
        }

        [Test]
        public void TheCloseIsTheOpenPlayedBackwards()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));

            rig.Door.TryUse();
            rig.Door.Tick(0.1f);
            int opening = rig.Door.CueFrame;
            rig.Door.Tick(1f);
            Assert.IsTrue(rig.Interior.IsInside);

            rig.Door.TryUse();
            rig.Door.Tick(0.1f);
            int closing = rig.Door.CueFrame;

            Assert.AreEqual(1, opening, "70 ms a frame: 100 ms in, the leaf is on frame 1");
            Assert.AreEqual(6, closing, "…and the close runs the same frames the other way (8 − 1 − 1)");
        }

        [Test]
        public void TheCueFrameIsMinusOneWhenNothingIsMoving()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));
            Assert.AreEqual(-1, rig.Door.CueFrame);
            Assert.AreEqual(0f, rig.Door.CueProgress01);
        }

        [Test]
        public void AnUnmeasuredThresholdOpensAtOnce()
        {
            // A door whose def states no cue is a threshold nobody has measured. "Press to enter" must
            // still be true — the feature cannot wait on art that may never come.
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));
            rig.Def.Door.CueFrames = 0;

            Assert.AreEqual(0f, rig.Door.CueSeconds);
            Assert.IsTrue(rig.Door.TryUse());
            Assert.IsFalse(rig.Door.IsCueing);
            Assert.IsTrue(rig.Interior.IsInside);
        }

        [Test]
        public void TheDoorSaysWhatThePressWillDo()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));

            Assert.IsTrue(rig.Door.WouldEnter);
            Assert.AreEqual("Go below", rig.Door.VerbLabel);

            rig.Door.TryUse();
            rig.Door.Tick(1f);

            Assert.IsFalse(rig.Door.WouldEnter);
            Assert.AreEqual("Come out", rig.Door.VerbLabel,
                            "the words are derived from the state, so they cannot drift from the action");
        }

        [Test]
        public void ADoorOnAnUnmeasuredHullIsNotACandidate()
        {
            Rig rig = NewRig(null, def: false);
            Assert.IsFalse(rig.Door.IsAvailable);
            Assert.IsFalse(rig.Door.TryUse());
        }

        [Test]
        public void ADoorIsWorkedFromTheDeck_AndByStandingAtIt()
        {
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));

            Assert.AreEqual(InteractContext.OnDeck, rig.Door.Contexts);
            Assert.IsFalse(rig.Door.RequiresFacing, "you open a door by standing at it, not by looking at it");
            Assert.AreEqual(InteractPriority.Fixture, rig.Door.Priority);
        }

        [Test]
        public void TheAdditionalDoorIsReachable_AndAMissingOneIsSimplyNoDoor()
        {
            // The 90's skylounge slider onto her aft deck: a second declared threshold on one hull.
            Rig rig = NewRig(Levels(("house_sole", 1.78f)));
            rig.Def.AdditionalDoors = new[]
            {
                new BoatInteriorDoor { Id = "sky_entry", CueFrames = 8, CueMillisecondsPerFrame = 70f },
            };

            rig.Door.Configure(rig.Interior, "fixture.boat.test.sky", 0, 1.2f, "Go in", "Come out");
            Assert.IsNotNull(rig.Door.Door);
            Assert.AreEqual("sky_entry", rig.Door.Door.Id);

            rig.Door.Configure(rig.Interior, "fixture.boat.test.sky", 4, 1.2f, "Go in", "Come out");
            Assert.IsNull(rig.Door.Door, "an index naming nothing is a hull with no such door");
            Assert.IsFalse(rig.Door.IsAvailable);
        }

        // =====================================================================================
        //  the fixture
        // =====================================================================================

        private struct Rig
        {
            public BoatInteriorDef Def;
            public BoatInterior Interior;
            public BoatCabinDoor Door;
            public SpriteRenderer Exterior;
            public SpriteRenderer Room;
            public GameObject Fittings;
        }


        /// <summary>Throwaway cells so the cabin has a picture without reaching Resources. Handing
        /// them to <c>Configure</c> is the documented EAGER path: a rig has no cells asset on disk,
        /// and an empty array now means "load your own", which would fail looking for one.</summary>
        private Sprite[] DummyCells(int n)
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

        private static Vector2[] Square() => new[]
        {
            new Vector2(-1f, -2f), new Vector2(1f, -2f), new Vector2(1f, 2f), new Vector2(-1f, 2f),
        };

        private static BoatInteriorLevel[] Levels(params (string id, float z)[] levels)
        {
            var made = new BoatInteriorLevel[levels.Length];
            for (int i = 0; i < levels.Length; i++)
                made[i] = new BoatInteriorLevel
                {
                    Id = levels[i].id,
                    SoleZMeters = levels[i].z,
                    Outline = Square(),
                };
            return made;
        }

        /// <summary>
        /// A cabin and her door, wired the way the placement pass will wire them. Uses the REAL
        /// components, never a stub: the swap and the level rules under test must be the shipped ones.
        /// </summary>
        private Rig NewRig(BoatInteriorLevel[] levels, bool def = true)
        {
            BoatInteriorDef made = null;
            if (def)
            {
                made = ScriptableObject.CreateInstance<BoatInteriorDef>();
                made.Id = "interior.test_hull";
                made.PixelsPerMetre = 32;
                made.Levels = levels ?? System.Array.Empty<BoatInteriorLevel>();
                made.Door = new BoatInteriorDoor
                {
                    Id = "entry",
                    Side = "aft",
                    ClearWidthMeters = 0.72f,
                    ClearHeightMeters = 1.85f,
                    ThresholdPoint = new Vector3(0f, -2.1f, 1.78f),
                    Mechanism = BoatInteriorDoorMechanism.Sliding,
                    CueFrames = 8,
                    CueMillisecondsPerFrame = 70f,
                    CueReversedOnExit = true,
                };
                _spawned.Add(made);
            }

            var root = new GameObject("TestHull");
            _spawned.Add(root);

            var exterior = new GameObject("House").AddComponent<SpriteRenderer>();
            exterior.transform.SetParent(root.transform, false);

            var room = new GameObject("Room").AddComponent<SpriteRenderer>();
            room.transform.SetParent(root.transform, false);

            var fittings = new GameObject("Fittings");
            fittings.transform.SetParent(root.transform, false);

            var interior = root.AddComponent<BoatInterior>();
            interior.Configure(made, exterior, room, fittings.transform, room.transform, root.transform,
                               DummyCells(8 * Mathf.Max(1, levels?.Length ?? 1)), 8, true, 0f,
                               DeckRoll, HeavePixels, PitchLift);

            var door = new GameObject("CabinDoor").AddComponent<BoatCabinDoor>();
            door.transform.SetParent(root.transform, false);
            door.Configure(interior, "fixture.boat.test.cabin_door", -1, 1.2f, "Go below", "Come out");

            return new Rig
            {
                Def = made,
                Interior = interior,
                Door = door,
                Exterior = exterior,
                Room = room,
                Fittings = fittings,
            };
        }
    }
}
