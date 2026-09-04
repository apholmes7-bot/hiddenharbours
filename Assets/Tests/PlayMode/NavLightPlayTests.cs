using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE MARK ACTUALLY FLASHES — the wiring, end to end, in a running frame loop.</b>
    ///
    /// <para>The character's arithmetic is pinned headlessly in <c>NavLightCharacterTests</c>; what
    /// only a running game can show is that the Core seam, the component and the pooled quad are
    /// joined up — that a placed mark mints a lamp at all, that the lamp's state follows the
    /// character frame by frame, and that an unlit mark costs nothing.</para>
    ///
    /// <para><b>⚠️ Set the clock, THEN yield, THEN read.</b> Components read the clock in
    /// <c>Update</c> and a test coroutine resumes after it, so a value written and read either side
    /// of a single yield is one frame stale. Every measurement here writes the clock, waits a frame
    /// and only then looks at the lamp.</para>
    /// </summary>
    public class NavLightPlayTests
    {
        private ScriptedClock _clock;
        private GameObject _mark;
        private GameObject _other;

        [SetUp]
        public void SetUp()
        {
            _clock = new ScriptedClock();
            GameServices.Clock = _clock;
        }

        [TearDown]
        public void TearDown()
        {
            if (_mark != null) Object.DestroyImmediate(_mark);
            if (_other != null) Object.DestroyImmediate(_other);
            GameServices.Reset();
        }

        // =============================================================================================
        //  The fixtures
        // =============================================================================================

        /// <summary>A mark built the way the placer builds one, wearing a character we choose.</summary>
        private static GameObject BuildMark(string name, string lightText, string lightId, string markId,
                                            out NavLight lamp, float phaseFraction = -1f)
        {
            var def = ScriptableObject.CreateInstance<NavBuoyDef>();
            def.Id = "buoy.test_" + name;
            def.MarkType = "PortCan";
            def.LightCharacter = lightId ?? "";
            def.LightText = lightText ?? "";
            def.Sizes.Add(new NavBuoyDef.SizeEntry
            {
                SizeId = "s18",
                DiameterMeters = 1.75f,
                SpriteHeightMeters = 2.84375f,
                FloatLineFraction = 0.36263737f,
                SlopeFollow = 0.2f,
                CollisionRadiusMeters = 0.875f,
                MooredMassKg = 300f,
                WatchRadiusMeters = 3f,
                Facings = new Sprite[8],
            });
            def.DefaultSizeIndex = 0;

            var root = new GameObject(name);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, worldPositionStays: false);
            var sr = visual.AddComponent<SpriteRenderer>();

            var bob = root.AddComponent<BuoyWaveVisual>();
            bob.Configure(sr, visual.transform);
            root.AddComponent<NavBuoyMooring>();

            var mark = root.AddComponent<NavBuoyVisual>();
            mark.Configure(def, "s18", 0, markId, phaseFraction);

            // Added LAST, exactly as the placer does — so it reads a mark that is already dressed.
            lamp = root.AddComponent<NavLight>();
            return root;
        }

        // =============================================================================================
        //  1. A LIT MARK MINTS A LAMP AND SHOWS HER CHARACTER
        // =============================================================================================

        /// <summary>
        /// A port hand shows <c>Fl G 4s</c>: one second lit in every four. Measured across a whole
        /// period by counting the frames the pooled quad is actually enabled on — not by asking the
        /// character, which is the thing under test.
        /// </summary>
        [UnityTest]
        public IEnumerator APortHandIsLitAQuarterOfHerPeriod()
        {
            _mark = BuildMark("PortHand", "Fl G 4s", "FlG4", "test.port_a", out NavLight lamp);
            yield return null;

            Assert.That(lamp.IsLit, Is.True, "the mark did not read her own character");
            Assert.That(lamp.Lamp, Is.Not.Null, "a lit mark minted no lamp");
            Assert.That(lamp.Character.PeriodSeconds, Is.EqualTo(4f).Within(1e-3f));

            // Phase her to zero so the window is where the character says it is, then walk a period.
            const double step = 0.05d;
            const int steps = 80;                      // 4.0 s
            double phase = lamp.PhaseSeconds;
            int litFrames = 0;

            for (int i = 0; i < steps; i++)
            {
                // Sample the MIDDLE of each cell and cancel her own phase, so no sample lands on a
                // flash boundary where a hair of float error decides the answer.
                _clock.SeekTo(step * 0.5d + i * step - phase);
                yield return null;
                if (lamp.Lamp.enabled) litFrames++;
            }

            Assert.That(litFrames, Is.EqualTo(20),
                        $"the lamp burned on {litFrames} of {steps} frames across her 4 s period; " +
                        "Fl G 4s is one second in four, which is 20.");
        }

        /// <summary>
        /// ⭐ The south cardinal, which is the composite: six quick flashes and then a long one. The
        /// lamp must go out five times between the quicks and stay lit for two full seconds at the
        /// end — a mark that showed seven quicks would keep the count and the period and still be
        /// the wrong cardinal.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSouthCardinalShowsSixQuicksAndThenALongFlash()
        {
            _mark = BuildMark("SouthCardinal", "Q(6) + LFl 15s", "Q6LFl", "test.south_a", out NavLight lamp);
            yield return null;

            Assert.That(lamp.IsLit, Is.True);
            double phase = lamp.PhaseSeconds;

            // Walk the first 9 s of the period at 50 ms and count the separate bursts.
            int bursts = 0;
            bool wasOn = false;
            int longestRun = 0, run = 0;
            const double step = 0.05d;

            for (int i = 0; i < 200; i++)             // 10 s, which covers the six quicks and the long
            {
                _clock.SeekTo(step * 0.5d + i * step - phase);
                yield return null;

                bool on = lamp.Lamp.enabled;
                if (on && !wasOn) bursts++;
                run = on ? run + 1 : 0;
                if (run > longestRun) longestRun = run;
                wasOn = on;
            }

            Assert.That(bursts, Is.EqualTo(7),
                        $"counted {bursts} separate flashes in the first 10 s of a Q(6)+LFl; " +
                        "there are seven — six quicks and one long.");
            Assert.That(longestRun, Is.EqualTo(40),
                        $"the longest unbroken burst was {longestRun} frames of 50 ms " +
                        $"({longestRun * 0.05:0.##} s); the long flash is 2 s, which is 40. A south " +
                        "cardinal whose tail is as short as her quicks is a west cardinal cut off.");
        }

        // =============================================================================================
        //  2. AN UNLIT MARK COSTS NOTHING
        // =============================================================================================

        /// <summary>
        /// A mooring buoy carries no character, so she must mint NO <see cref="SceneLight"/> — no
        /// quad, no shadow registration, nothing to draw. Absence is data, exactly as it is for a
        /// hull with no lamps.
        /// </summary>
        [UnityTest]
        public IEnumerator AnUnlitMarkMintsNoLampAtAll()
        {
            _mark = BuildMark("Mooring", "", "", "test.mooring_a", out NavLight lamp);
            yield return null;
            yield return null;

            Assert.That(lamp.IsLit, Is.False, "an unlit mark thinks she is lit");
            Assert.That(lamp.Lamp, Is.Null, "an unlit mark minted a lamp");
            Assert.That(lamp.IsBurning, Is.False);
            Assert.That(_mark.GetComponentsInChildren<SceneLight>(true).Length, Is.Zero,
                        "an unlit mark put a SceneLight somewhere in her hierarchy");

            // …and she stays that way as the clock runs.
            for (int i = 0; i < 10; i++)
            {
                _clock.Advance(0.5d);
                yield return null;
                Assert.That(_mark.GetComponentsInChildren<SceneLight>(true).Length, Is.Zero);
            }
        }

        // =============================================================================================
        //  3. TWO MARKS OF ONE CHARACTER DO NOT WINK TOGETHER
        // =============================================================================================

        /// <summary>
        /// Two port-hand cans wearing the SAME character must not flash as one. This is the
        /// running-game half of the claim the content test makes about the placed marks: not merely
        /// that their phases differ as numbers, but that the two lamps are observably out of step
        /// frame by frame.
        ///
        /// <para>The phases come from <see cref="NavLightPhasePlan.Spread"/> — the same call the
        /// region placer makes — rather than from numbers typed here, so this exercises the path the
        /// game actually takes.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TwoMarksOfOneCharacterAreVisiblyOutOfStep()
        {
            var fractions = NavLightPhasePlan.Spread(new[]
            {
                ("channel.nmc_entrance.p0", "Fl G 4s"),
                ("channel.nmc_entrance.p2", "Fl G 4s"),
            });

            _mark  = BuildMark("PortA", "Fl G 4s", "FlG4", "channel.nmc_entrance.p0", out NavLight a,
                               fractions["channel.nmc_entrance.p0"]);
            _other = BuildMark("PortB", "Fl G 4s", "FlG4", "channel.nmc_entrance.p2", out NavLight b,
                               fractions["channel.nmc_entrance.p2"]);
            yield return null;

            Assert.That(a.PhaseSeconds, Is.Not.EqualTo(b.PhaseSeconds),
                        "two marks with different chart ids landed on the same phase");

            int together = 0, apart = 0, bothLit = 0;
            const double step = 0.05d;

            for (int i = 0; i < 80; i++)              // one full period
            {
                _clock.SeekTo(step * 0.5d + i * step);
                yield return null;
                if (a.Lamp.enabled && b.Lamp.enabled) bothLit++;
                if (a.Lamp.enabled == b.Lamp.enabled) together++; else apart++;
            }

            Assert.That(apart, Is.GreaterThan(0),
                        "the two marks were in the same state on every frame of a period — they are " +
                        "flashing in unison, which reads as one light in the wrong place.");
            Assert.That(bothLit, Is.Zero,
                        $"the two marks were lit together on {bothLit} frames; two Fl G 4s cans a " +
                        "slot apart should never overlap at all, since each burns for a quarter of " +
                        "the period and they are half a period apart.");
            Debug.Log($"[NavLight] two Fl G 4s marks: phases {a.PhaseSeconds:0.###}s / " +
                      $"{b.PhaseSeconds:0.###}s, disagreeing on {apart} of {together + apart} frames, " +
                      $"never lit together.");
        }

        // =============================================================================================
        //  4. THE LANTERN IS WHERE THE MARK IS
        // =============================================================================================

        /// <summary>
        /// ⭐ The lamp hangs off the BOBBED visual, at the top of the mark's painted structure. A
        /// light parented to the root would burn at a fixed height while the can it is bolted to
        /// heaved half a metre underneath it — so the test moves the visual and requires the light
        /// to come with it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLanternRidesTheBobAtTheTopOfTheMark()
        {
            _mark = BuildMark("Rider", "Fl G 4s", "FlG4", "test.rider", out NavLight lamp);
            yield return null;

            Transform visual = _mark.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(lamp.Lamp.transform.IsChildOf(visual), Is.True,
                        "the lantern is not parented to the bobbed visual — it would hang still " +
                        "while the buoy heaves underneath it");

            // The height is DERIVED from the bake: sprite height x (1 - waterline fraction).
            const float expected = 2.84375f * (1f - 0.36263737f);
            Assert.That(lamp.Lamp.transform.localPosition.y, Is.EqualTo(expected).Within(1e-3f),
                        "the lantern is not at the top of the mark's painted structure");

            // ⚠️ BuoyWaveVisual OWNS this transform: every LateUpdate it writes
            // `_visual.localPosition = _baseLocalPosition` and then adds the wave offset, so a value
            // poked in from outside is gone by the next frame — the first version of this arm moved
            // the visual, watched the bob put it back, and reported that the lantern had not
            // followed. Stand the owner down and the transform is the test's to move; what is being
            // asserted is that the light is RIGIDLY ATTACHED to it, which is what makes it ride
            // whatever the bob does with it.
            _mark.GetComponent<BuoyWaveVisual>().enabled = false;
            yield return null;

            float before = lamp.Lamp.transform.position.y;
            visual.localPosition = visual.localPosition + new Vector3(0f, 0.5f, 0f);
            yield return null;

            Assert.That(lamp.Lamp.transform.position.y, Is.EqualTo(before + 0.5f).Within(1e-3f),
                        "the lantern did not follow the transform it hangs off — it would burn at a " +
                        "fixed height while the buoy heaves underneath it");

            // …and the ROOT moving carries the whole assembly too (the mark being knocked off station).
            float rooted = lamp.Lamp.transform.position.y;
            _mark.transform.position += new Vector3(0f, 1.25f, 0f);
            yield return null;
            Assert.That(lamp.Lamp.transform.position.y, Is.EqualTo(rooted + 1.25f).Within(1e-3f),
                        "the lantern did not follow the mark's own root");
        }

        // =============================================================================================

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
            public float HourOfDay => 2f;              // the small hours, when a mark is worth having
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }
    }
}
