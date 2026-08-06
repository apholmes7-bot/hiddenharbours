using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The deck a rider stands on is the deck the player SEES.</b> The hull publishes the rock it is
    /// drawing (<see cref="BoatWaveMotion.RockPhaseDegrees"/>) so a character on her deck can ride it; this
    /// drives the REAL production chain — <see cref="BoatWaveMotion"/> → <see cref="SpriteHullPresenter"/>
    /// → <see cref="DirectionalBoatSprite"/> — against a scripted clock and sea, and asserts the published
    /// phase is the very frame the sprite was handed, on every one of 600 ticks.
    ///
    /// <para><b>Why that identity is the whole point.</b> A sprite hull's rock is QUANTISED: the picture
    /// steps in 45° jumps with 8° of hysteresis while the continuous swell phase behind it sweeps
    /// smoothly. Publishing the smooth phase would be more "accurate" and completely wrong — the fisher
    /// would lean past the deck between frame steps and slide back. So the contract is not "close to the
    /// swell", it is "exactly the drawn frame", and that is what is measured here.</para>
    ///
    /// <para>Deliberately shares the harness shape (and the scripted world) of
    /// <see cref="SpriteRockFrameCycleTests"/>, which already proves this chain CYCLES; this adds the
    /// question that one does not ask — what a passenger is told about it. Headless-safe (null sprites:
    /// frame selection is array-length and index logic, never pixels) and deterministic (rule 5).</para>
    /// </summary>
    public class BoatRockPhasePublishTests
    {
        const float Dt = 1f / 60f;
        const int Ticks = 600;                        // 10 s of sail
        const int FrameCount = 8;                     // the DoryIsoRock sheet, the production default

        static readonly Vector2 Wind = new Vector2(6f, 3f);
        const float RoughSea = 0.40f;

        sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public void Advance(double dt) => TotalSeconds += dt;
            public GameTime Now => new GameTime(TotalSeconds);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        sealed class ScriptedSea : IEnvironmentService
        {
            public float SeaState01 = RoughSea;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Vector2.zero, tideHeight: 0f, HiddenHarbours.Core.SeaState.Moderate,
                visibility: 1f, seaState01: SeaState01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        private GameObject _root;

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        /// <summary>The production sprite chain on a boat root, plus the scripted world it sails in.</summary>
        (BoatWaveMotion wave, DirectionalBoatSprite hull, ScriptedClock clock) Rig(float seaState01)
        {
            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedSea { SeaState01 = seaState01 };

            _root = new GameObject("Boat");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(_root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();

            var directional = _root.AddComponent<DirectionalBoatSprite>();
            directional.Configure(new Sprite[FrameCount], sr);
            directional.ConfigureRock(new Sprite[FrameCount * FrameCount], FrameCount);
            Assert.IsTrue(directional.HasRockGrid, "harness: the rock grid must gate ON");

            var wave = _root.AddComponent<BoatWaveMotion>();
            wave.Configure(visual.transform, new SpriteHullPresenter(directional));

            // Warm-up, as SpriteRockFrameCycleTests: the first tick has no previous clock reading and the
            // animator snaps to its targets — right on wake, but not what "while sailing" asks.
            for (int w = 0; w < 5; w++) { clock.Advance(Dt); wave.Tick(); }
            return (wave, directional, clock);
        }

        // ---- the identity: the published phase IS the drawn frame ------------------------------------

        [Test]
        public void ThePublishedPhase_IsExactlyTheFrameTheHullIsDRAWING_EveryTick()
        {
            var (wave, hull, clock) = Rig(RoughSea);

            int rockingTicks = 0;
            for (int t = 0; t < Ticks; t++)
            {
                clock.Advance(Dt);
                wave.Tick();

                if (hull.RockFrame < 0)
                {
                    Assert.IsFalse(wave.IsRocking, $"tick {t}: a level hull must report a level deck");
                    continue;
                }
                rockingTicks++;
                Assert.IsTrue(wave.IsRocking, $"tick {t}: drawing frame {hull.RockFrame} but reporting level");
                Assert.AreEqual(DeckRideMath.PhaseForRockFrame(hull.RockFrame, FrameCount),
                                wave.RockPhaseDegrees, 1e-3f,
                                $"tick {t}: the passenger must ride frame {hull.RockFrame}, not the " +
                                "continuous swell behind it");
            }

            Assert.Greater(rockingTicks, Ticks / 2,
                "harness check: a sea state of 0.40 must actually rock her for most of the sail");
        }

        [Test]
        public void ThePublishedPhase_StaysInsideOneCycle_SoARiderCanNeverBeHandedNonsense()
        {
            var (wave, _, clock) = Rig(RoughSea);
            for (int t = 0; t < Ticks; t++)
            {
                clock.Advance(Dt);
                wave.Tick();
                float phase = wave.RockPhaseDegrees;
                if (!wave.IsRocking)
                {
                    Assert.AreEqual(DeckRideMath.LevelPhaseDegrees, phase, 1e-4f, $"tick {t}");
                    continue;
                }
                Assert.GreaterOrEqual(phase, 0f, $"tick {t}");
                Assert.Less(phase, 360f, $"tick {t}");
                Assert.IsFalse(float.IsNaN(phase), $"tick {t}");
            }
        }

        // ---- the level cases: nothing frozen, nothing phantom ----------------------------------------

        [Test]
        public void GlassCalm_ReportsALevelDeck_NoPhantomRockingAtAnchor()
        {
            var (wave, hull, clock) = Rig(0f);            // sea state 0 = the field's amplitudes are exactly 0
            for (int t = 0; t < 60; t++) { clock.Advance(Dt); wave.Tick(); }

            Assert.AreEqual(-1, hull.RockFrame, "harness: glass must hold the static level hull");
            Assert.IsFalse(wave.IsRocking, "…and so must the deck her passengers stand on");
            Assert.AreEqual(DeckRideMath.LevelPhaseDegrees, wave.RockPhaseDegrees, 1e-4f);
        }

        [Test]
        public void MasterStrengthZero_LevelsTheDeckToo_TheOwnersABStaysWholeAcrossTheHull()
        {
            var (wave, _, clock) = Rig(RoughSea);
            for (int t = 0; t < 30; t++) { clock.Advance(Dt); wave.Tick(); }
            Assert.IsTrue(wave.IsRocking, "harness: she is rocking before the switch is thrown");

            wave.MasterStrength = 0f;
            clock.Advance(Dt);
            wave.Tick();

            Assert.IsFalse(wave.IsRocking, "the rock switched off must not leave a passenger riding one");
            Assert.AreEqual(DeckRideMath.LevelPhaseDegrees, wave.RockPhaseDegrees, 1e-4f);
        }

        // NOTE: "a DISABLED hull reports a level deck" is deliberately NOT tested here. It is an OnDisable
        // claim, and the editor does not run the enable/disable callbacks for runtime scripts outside play
        // mode — an EditMode test of it passes or fails on the harness, not on the code. It lives in
        // DeckRiderPlayTests.DisablingTheHullsWaveMotion_PutsHerPassengerBackSquare, where the callback
        // genuinely fires and the claim is end-to-end (the FISHER goes level, not just a field).

        [Test]
        public void AHullWithNoRockGrid_ReportsLevel_RatherThanInventingACycleFromTheSurface()
        {
            // The legacy TRANSFORM rock draws no cycle, so there is no honest phase to publish. (The
            // atan2 reconstruction that could fake one is the non-monotone read ADR 0022 phase 5 retired;
            // a rider fed it would judder.) A deck rider on such a hull reads her applied tilt instead.
            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedSea { SeaState01 = RoughSea };

            _root = new GameObject("Boat");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(_root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();

            var directional = _root.AddComponent<DirectionalBoatSprite>();
            directional.Configure(new Sprite[FrameCount], sr);      // facings only — NO rock grid
            Assert.IsFalse(directional.HasRockGrid, "harness: this hull must fall to the transform rock");

            var wave = _root.AddComponent<BoatWaveMotion>();
            wave.Configure(visual.transform, new SpriteHullPresenter(directional));

            // Sample the whole sail rather than one tick: the transform roll is a sine through the swell,
            // so any single tick may legitimately catch it crossing zero.
            float peakTilt = 0f;
            for (int t = 0; t < 240; t++)
            {
                clock.Advance(Dt);
                wave.Tick();
                Assert.IsFalse(wave.IsRocking, $"tick {t}: no cycle drawn, so no cycle published");
                peakTilt = Mathf.Max(peakTilt, Mathf.Abs(directional.VisualTiltDegrees));
            }
            Assert.Greater(peakTilt, 0.1f,
                "…but she IS leaning, which is the fallback a rider on her reads");
        }
    }
}
