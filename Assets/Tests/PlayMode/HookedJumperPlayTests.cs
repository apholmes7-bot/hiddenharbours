using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// A HOOKED JUMPER JUMPS, AND A COD NEVER DOES (owner's ruling 2026-09-06) — the half of PR 2b that
    /// only exists once frames are running.
    ///
    /// <para><b>Why PlayMode.</b> The jump is scheduled off the clock the presenter accumulates in
    /// <c>LateUpdate</c> while she is at the surface, and drawn out of the rung's baked jump sheet. Both
    /// of those are frame-driven, so an EditMode test of them would pass for the wrong reason. The jump
    /// LAW — the arc, the travel, the cadence's determinism and non-overlap — is pinned in EditMode
    /// (<c>HookedJumperTests</c>), where it belongs; this file is only about the wiring and the ruling.</para>
    ///
    /// <para><b>The negative control is the point.</b> A test that only watched a bass jump would pass
    /// just as happily if the presenter jumped for everything. The cod case is what makes the bass case
    /// mean something, and both run against the same fixture with only the species changed.</para>
    ///
    /// <para><b>Frames are not time</b> — the presenter's clock advances by <c>Time.deltaTime</c>, so the
    /// fixture waits on the CLOCK (a real-seconds budget) rather than counting frames, and authors a
    /// short cadence in the Def so the wait stays under a second.</para>
    /// </summary>
    public class HookedJumperPlayTests
    {
        // Comfortably longer than one jump (FishJumpArc.DurationSeconds = 0.570 s) so the schedule is
        // legal, and short enough that the test waits well under a second for one.
        private const float ShortPeriodSeconds = 0.75f;

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- fixture ----------------------------------------------------------------------------

        private T Track<T>(T o) where T : Object
        {
            _spawned.Add(o);
            return o;
        }

        /// <summary>A 2x2 white sprite — the presenter only ever asks whether a cell EXISTS, so the art
        /// need only be real, not pretty.</summary>
        private Sprite Cell()
        {
            var tex = Track(new Texture2D(2, 2));
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            return Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f));
        }

        private Sprite[] Sheet(int dirs, int framesPerDir)
        {
            var s = new Sprite[dirs * framesPerDir];
            for (int i = 0; i < s.Length; i++) s[i] = Cell();
            return s;
        }

        /// <summary>One species' fight art, with every sheet the surfaced fight can reach for — including
        /// the JUMP sheet catch pass 2 baked and left for this lane.</summary>
        private FishSpeciesVisual Visual(string fishId)
        {
            const int dirs = 8;
            var rung = new FishRungVisual
            {
                Kg = 100f,                       // one rung that covers every weight the test publishes
                DartFrames = Sheet(dirs, 3), DartFramesPerDir = 3,
                ThrashFrames = Sheet(dirs, 4), ThrashFramesPerDir = 4,
                JumpFrames = Sheet(dirs, FishJumpArc.Frames), JumpFramesPerDir = FishJumpArc.Frames,
            };
            return new FishSpeciesVisual { FishId = fishId, Rungs = new[] { rung } };
        }

        private FishSpeciesDef Def(string id, FishFlags flags, float period)
        {
            var d = Track(ScriptableObject.CreateInstance<FishSpeciesDef>());
            d.Id = id;
            d.BehaviorFlags = flags;
            d.FightJumpPeriodSeconds = period;
            return d;
        }

        private RodFightPresenter Presenter(string fishId)
        {
            var go = Track(new GameObject("RodFightPresenter"));
            var p = go.AddComponent<RodFightPresenter>();
            // Only the fish art is wired: the rod, the bobber and the hands are not what is under test,
            // and the presenter is built to draw what it has rather than refuse what it hasn't.
            p.Configure(null, null, null, new[] { Visual(fishId) }, null, null, 0,
                        new[] { Cell() }, Cell());
            return p;
        }

        private static FishingState Surfaced(string fishId, float weightKg)
            => new FishingState(FishingPhase.FightSurface, 0.4f, 0.2f, fishId, fishId,
                                FishCategory.InshoreGroundfish, weightKg,
                                depth01: 0f, slackWindowOpen: false, rodBend01: 0.3f,
                                fishOffsetX: 1.5f, fishOffsetY: 0.5f);

        /// <summary>Run the fight for a real-seconds budget, reporting whether she ever left the water.
        /// Waits on the CLOCK because the presenter's schedule does.</summary>
        private static IEnumerator Fight(RodFightPresenter p, string fishId, float seconds)
        {
            p.OnFishingState(new FishingStateChanged(Surfaced(fishId, 4f)));

            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                t += Time.deltaTime;
            }
        }

        // ---- the ruling, both directions ----------------------------------------------------------

        /// <summary>A hooked BASS leaves the water at least once inside a couple of her periods.</summary>
        [UnityTest]
        public IEnumerator AHookedBass_Jumps()
        {
            const string bass = "fish.striped_bass";
            FishSpeciesRegistry.Register(Def(bass, FishFlags.Jumps, ShortPeriodSeconds));

            RodFightPresenter p = Presenter(bass);
            yield return Fight(p, bass, ShortPeriodSeconds * 3f);

            Assert.GreaterOrEqual(p.JumpsDrawn, 1,
                "the owner ruled a hooked bass clears the water — she did not jump in three of her " +
                $"own periods ({ShortPeriodSeconds * 3f:F2} s)");
        }

        /// <summary>THE NEGATIVE CONTROL. A hooked COD never leaves the water, for the same fixture, the
        /// same art and the same run of frames — only the species changed.</summary>
        [UnityTest]
        public IEnumerator AHookedCod_NeverJumps()
        {
            const string cod = "fish.atlantic_cod";
            // Authored with a cadence ON PURPOSE: the FLAG must be what decides, not the period. If the
            // presenter ever starts reading the cadence alone, this is the test that goes red.
            FishSpeciesRegistry.Register(Def(cod, FishFlags.Bottom, ShortPeriodSeconds));

            RodFightPresenter p = Presenter(cod);
            yield return Fight(p, cod, ShortPeriodSeconds * 3f);

            Assert.AreEqual(0, p.JumpsDrawn,
                "a cod does not clear the water — the Jumps flag decides, never the cadence");
            Assert.IsFalse(p.FishAirborne, "and she is certainly not in the air at the end of it");
        }

        /// <summary>
        /// With NO species registered at all — a scene with no library — the seam reads as empty and
        /// nothing jumps, rather than throwing at the top of a fight. The degrade-don't-refuse posture,
        /// proved with frames running.
        /// </summary>
        [UnityTest]
        public IEnumerator WithNoSpeciesLibrary_TheFightStillRuns()
        {
            const string unknown = "fish.not_registered";
            RodFightPresenter p = Presenter(unknown);
            yield return Fight(p, unknown, 0.5f);

            Assert.AreEqual(0, p.JumpsDrawn, "an unknown species does not jump");
            Assert.IsFalse(p.FishAirborne);
        }

        /// <summary>The count is per FIGHT: landing one fish and hooking the next starts from zero, so a
        /// plate or a telemetry read of it can never be a session total.</summary>
        [UnityTest]
        public IEnumerator TheJumpCount_IsPerFight()
        {
            const string bass = "fish.striped_bass";
            FishSpeciesRegistry.Register(Def(bass, FishFlags.Jumps, ShortPeriodSeconds));
            FishSpeciesRegistry.Register(Def("fish.atlantic_cod", FishFlags.Bottom, 0f));

            RodFightPresenter p = Presenter(bass);
            yield return Fight(p, bass, ShortPeriodSeconds * 3f);
            Assert.GreaterOrEqual(p.JumpsDrawn, 1, "the bass fight jumped");

            // A different species on the line is a different fight.
            p.OnFishingState(new FishingStateChanged(Surfaced("fish.atlantic_cod", 4f)));
            Assert.AreEqual(0, p.JumpsDrawn, "the next fight starts its own count");
        }
    }
}
