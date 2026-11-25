using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// HOW BIG SHE FEELS (owner's ask 2026-11-13: "rods should bend and react to fish, size … i want
    /// player to get excited woah its something big!!"). Pins the curves in <see cref="FishFeelMath"/>
    /// — bend, tip pulse, run pull, line — against fish SIZE and against her MOVEMENT, the "big one"
    /// threshold, the published <c>FishingState.Size01</c>, and the landing line's text. (The size-aware
    /// strain voice is pinned in <c>Audio/FishFeelAudioTests</c> — this assembly cannot see Audio.)
    ///
    /// <para>Everything is asserted against <see cref="FishFeelSettings.Default"/> or a locally edited
    /// copy of it, so the tests hold the SHAPE of each curve and the dials stay the owner's.</para>
    /// </summary>
    public class FishFeelTests
    {
        private const float Eps = 1e-4f;
        private static FishFeelSettings D => FishFeelSettings.Default;

        private readonly List<Object> _spawned = new();
        /// <summary>Raw thoughts on the ambient channel. With no World director on the bus there must
        /// be none: Fishing names the moment (<see cref="InnerVoiceCue"/>) and never speaks itself.</summary>
        private readonly List<string> _thoughts = new();
        private readonly List<string> _cues = new();
        private void OnSpeech(AmbientSpeechRequested e)
        {
            if (e.Kind == AmbientSpeechKind.InnerVoice) _thoughts.Add(e.Line);
        }
        private void OnCue(InnerVoiceCue e) => _cues.Add(e.CueId);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            _thoughts.Clear();
            _cues.Clear();
            EventBus.Clear<AmbientSpeechRequested>();
            EventBus.Clear<InnerVoiceCue>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Subscribe<AmbientSpeechRequested>(OnSpeech);
            EventBus.Subscribe<InnerVoiceCue>(OnCue);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AmbientSpeechRequested>(OnSpeech);
            EventBus.Unsubscribe<InnerVoiceCue>(OnCue);
            EventBus.Clear<AmbientSpeechRequested>();
            EventBus.Clear<InnerVoiceCue>();
            EventBus.Clear<FishingStateChanged>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
        }

        // ---- size and the big-one threshold ---------------------------------------------------------

        [Test]
        public void Size01_is_her_place_in_her_own_species_range()
        {
            Assert.AreEqual(0f, FishFeelMath.Size01(2f, 2f, 12f), Eps, "her smallest");
            Assert.AreEqual(1f, FishFeelMath.Size01(12f, 2f, 12f), Eps, "her heaviest");
            Assert.AreEqual(0.5f, FishFeelMath.Size01(7f, 2f, 12f), Eps);
            Assert.AreEqual(0f, FishFeelMath.Size01(1f, 2f, 12f), Eps, "clamped below");
            Assert.AreEqual(1f, FishFeelMath.Size01(40f, 2f, 12f), Eps, "clamped above");
            // A 0.9 kg mackerel is as big FOR A MACKEREL as an 11 kg cod is for a cod.
            Assert.AreEqual(FishFeelMath.Size01(11f, 2f, 12f), FishFeelMath.Size01(0.92f, 0.2f, 1f), Eps);
        }

        [Test]
        public void Size01_of_a_degenerate_range_or_NaN_is_zero_never_a_false_big_one()
        {
            Assert.AreEqual(0f, FishFeelMath.Size01(5f, 5f, 5f));
            Assert.AreEqual(0f, FishFeelMath.Size01(5f, 8f, 2f));
            Assert.AreEqual(0f, FishFeelMath.Size01(float.NaN, 2f, 12f));
            Assert.IsFalse(FishFeelMath.IsBigOne(FishFeelMath.Size01(5f, 0f, 0f), D));
        }

        [Test]
        public void The_big_one_threshold_is_data()
        {
            FishFeelSettings s = D;
            Assert.IsFalse(FishFeelMath.IsBigOne(s.BigOneThreshold01 - 0.01f, in s));
            Assert.IsTrue(FishFeelMath.IsBigOne(s.BigOneThreshold01, in s), "the threshold itself is in the band");
            Assert.IsTrue(FishFeelMath.IsBigOne(1f, in s));

            s.BigOneThreshold01 = 0.5f;
            Assert.IsTrue(FishFeelMath.IsBigOne(0.6f, in s), "moving the dial moves the band");
            s.BigOneThreshold01 = 0.95f;
            Assert.IsFalse(FishFeelMath.IsBigOne(0.9f, in s));
        }

        // ---- the rod: bend versus weight --------------------------------------------------------------

        [Test]
        public void A_small_fish_nods_the_tip_and_a_heavy_one_bows_the_rod_deep()
        {
            FishFeelSettings s = D;
            float small = FishFeelMath.RodLoad01(1f, 0f, false, in s);
            float heavy = FishFeelMath.RodLoad01(1f, 1f, false, in s);
            Assert.AreEqual(s.BendSmallScale01, small, Eps, "her smallest only gets part of the load onto the rod");
            Assert.AreEqual(1f, heavy, Eps, "her heaviest gets all of it");

            float prev = -1f;
            for (int i = 0; i <= 10; i++)
            {
                float v = FishFeelMath.RodLoad01(0.6f, i / 10f, false, in s);
                Assert.GreaterOrEqual(v, prev, "bend never falls as she gets heavier");
                Assert.LessOrEqual(v, 1f);
                prev = v;
            }
        }

        [Test]
        public void A_heavy_fish_HOLDS_the_rod_down_between_runs_and_a_small_one_lets_it_up()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(0f, FishFeelMath.RodLoad01(0f, 0f, false, in s), Eps, "no load, small fish: a straight rod");
            Assert.AreEqual(s.BendHeavyFloor01, FishFeelMath.RodLoad01(0f, 1f, false, in s), Eps,
                "no load, heavy fish: the rod stays bowed at the floor");
            Assert.AreEqual(s.BendHeavyFloor01 * 0.5f, FishFeelMath.RodLoad01(0f, 0.5f, false, in s), Eps,
                "the held bow scales with size");
        }

        [Test]
        public void The_slack_tell_still_reads_on_a_heavy_fishs_rod()
        {
            FishFeelSettings s = D;
            float held = FishFeelMath.RodLoad01(0f, 1f, false, in s);
            float slack = FishFeelMath.RodLoad01(0f, 1f, true, in s);
            Assert.Less(slack, held, "she gave: the rod comes up");
            Assert.AreEqual(s.BendHeavyFloor01 * s.BendSlackKeep01, slack, Eps);
        }

        [Test]
        public void The_lean_ceiling_grows_with_her_weight()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(14f, FishFeelMath.MaxLeanDegrees(14f, 0f, in s), Eps);
            Assert.AreEqual(14f * s.BendHeavyLeanScale, FishFeelMath.MaxLeanDegrees(14f, 1f, in s), Eps);
            Assert.AreEqual(0f, FishFeelMath.MaxLeanDegrees(0f, 1f, in s), Eps, "the presenter's off switch survives");
        }

        // ---- the rod: her movement ----------------------------------------------------------------------

        private static float PeakPulse(float size01, float effort01, in FishFeelSettings s, out int pulses)
        {
            float peak = 0f;
            pulses = 0;
            bool inPulse = false;
            for (int i = 0; i < 4000; i++)   // 4 s at 1 ms
            {
                float v = FishFeelMath.TipPulseDegrees(i * 0.001f, size01, effort01, 0.25f, in s);
                Assert.GreaterOrEqual(v, 0f, "a pulse only ever pulls toward her");
                if (v > peak) peak = v;
                bool on = v > 1e-3f;
                if (on && !inPulse) pulses++;
                inPulse = on;
            }
            return peak;
        }

        [Test]
        public void Head_shakes_are_quick_and_light_for_a_small_fish_slow_and_hard_for_a_heavy_one()
        {
            FishFeelSettings s = D;
            float small = PeakPulse(0f, 1f, in s, out int smallPulses);
            float heavy = PeakPulse(1f, 1f, in s, out int heavyPulses);
            Assert.AreEqual(s.ShakeDegreesSmall, small, 0.05f);
            Assert.AreEqual(s.ShakeDegreesHeavy, heavy, 0.05f);
            Assert.Greater(heavy, small, "the heavy fish thumps the tip harder");
            Assert.Greater(smallPulses, heavyPulses, "the small fish rattles it faster");
        }

        [Test]
        public void Head_shakes_follow_her_effort_and_stop_when_she_is_slack()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(0f, PeakPulse(1f, 0f, in s, out int none), Eps, "slack: no shake at all");
            Assert.AreEqual(0, none);
            float half = PeakPulse(1f, 0.5f, in s, out _);
            float full = PeakPulse(1f, 1f, in s, out _);
            Assert.AreEqual(full * 0.5f, half, 0.05f, "a harder run is a harder shake");
        }

        [Test]
        public void Head_shakes_are_sharp_pulses_with_rest_between_and_replay_exactly()
        {
            FishFeelSettings s = D;
            int resting = 0, total = 2000;
            for (int i = 0; i < total; i++)
            {
                float t = i * 0.002f;
                float a = FishFeelMath.TipPulseDegrees(t, 0.8f, 1f, 0.6f, in s);
                float b = FishFeelMath.TipPulseDegrees(t, 0.8f, 1f, 0.6f, in s);
                Assert.AreEqual(a, b, "closed-form: the same clock gives the same pulse (rule 5)");
                if (a < 0.1f * s.ShakeDegreesHeavy) resting++;
            }
            Assert.Greater(resting, total / 2, "more rest than pulse: a jab, not a wobble");
        }

        [Test]
        public void A_run_drags_the_tip_further_for_a_heavier_fish()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(s.RunPullMetres * s.RunPullSmall01, FishFeelMath.RunPullMetres(0f, in s), Eps);
            Assert.AreEqual(s.RunPullMetres, FishFeelMath.RunPullMetres(1f, in s), Eps);
            Assert.Greater(FishFeelMath.RunPullMetres(0.7f, in s), FishFeelMath.RunPullMetres(0.3f, in s));
        }

        [Test]
        public void A_turn_is_a_real_change_of_run_direction_not_a_start_or_a_stop()
        {
            FishFeelSettings s = D;
            Assert.IsTrue(FishFeelMath.IsTurn(1f, 0f, -1f, 0f, in s), "a reversal (the thrasher's flip)");
            Assert.IsTrue(FishFeelMath.IsTurn(1f, 0f, 0f, 1f, in s), "a right-angle dart");
            Assert.IsFalse(FishFeelMath.IsTurn(1f, 0f, 0.95f, 0.1f, in s), "holding her line");
            Assert.IsFalse(FishFeelMath.IsTurn(0f, 0f, 1f, 0f, in s), "a run from rest is not a turn");
            Assert.IsFalse(FishFeelMath.IsTurn(1f, 0f, 0f, 0f, in s), "nor is stopping");
        }

        [Test]
        public void A_turn_kicks_the_tip_harder_for_a_heavy_fish_and_settles()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(s.TurnKickDegrees, FishFeelMath.TurnKickDegrees(0f, 1f, in s), Eps);
            Assert.Less(FishFeelMath.TurnKickDegrees(0f, 0f, in s), FishFeelMath.TurnKickDegrees(0f, 1f, in s));
            Assert.Less(FishFeelMath.TurnKickDegrees(s.TurnKickSeconds * 0.5f, 1f, in s), s.TurnKickDegrees);
            Assert.AreEqual(0f, FishFeelMath.TurnKickDegrees(s.TurnKickSeconds, 1f, in s), Eps);
            Assert.AreEqual(0f, FishFeelMath.TurnKickDegrees(float.PositiveInfinity, 1f, in s), Eps,
                "no turn yet this fight: no kick");
        }

        // ---- the hook-set ------------------------------------------------------------------------------

        [Test]
        public void The_hook_set_slams_harder_for_a_big_one_and_springs_back()
        {
            FishFeelSettings s = D;
            float below = FishFeelMath.HookSlamDegrees(0f, s.BigOneThreshold01 - 0.01f, in s);
            float big = FishFeelMath.HookSlamDegrees(0f, s.BigOneThreshold01, in s);
            float small = FishFeelMath.HookSlamDegrees(0f, 0f, in s);
            Assert.AreEqual(s.HookSlamDegrees * s.HookSlamSmall01, small, Eps);
            Assert.Greater(below, small, "the slam grows with size");
            Assert.Greater(big, below * 1.3f, "crossing the big-one line is a step, not a slope");
            Assert.AreEqual(s.HookSlamDegrees * s.BigOneSlamScale, FishFeelMath.HookSlamDegrees(0f, 1f, in s), Eps);
            Assert.AreEqual(0f, FishFeelMath.HookSlamDegrees(s.HookSlamSeconds, 1f, in s), Eps, "it is over");
            Assert.AreEqual(0f, FishFeelMath.HookSlamDegrees(5f, 1f, in s), Eps);
        }

        [Test]
        public void The_hook_set_camera_kick_scales_with_weight_and_again_for_a_big_one()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(s.HookKickSmallScale, FishFeelMath.HookKickScale(0f, in s), Eps);
            Assert.AreEqual(s.HookKickHeavyScale * s.BigOneKickScale, FishFeelMath.HookKickScale(1f, in s), Eps);
            float prev = 0f;
            for (int i = 0; i <= 20; i++)
            {
                float v = FishFeelMath.HookKickScale(i / 20f, in s);
                Assert.GreaterOrEqual(v, prev, "a heavier fish never kicks less");
                prev = v;
            }
        }

        // ---- the line ----------------------------------------------------------------------------------

        [Test]
        public void A_heavy_fish_keeps_the_line_tight_but_the_slack_tell_passes_through()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(0.4f, FishFeelMath.LineTaut01(0.4f, 0f, false, in s), Eps, "a small fish: the fight's own taut");
            Assert.AreEqual(s.LineHeavyTautFloor01, FishFeelMath.LineTaut01(0.4f, 1f, false, in s), Eps);
            Assert.AreEqual(0.95f, FishFeelMath.LineTaut01(0.95f, 1f, false, in s), Eps, "never slacker than the fight says");
            Assert.AreEqual(0f, FishFeelMath.LineTaut01(0f, 1f, true, in s), Eps, "slack is the instruction: untouched");
        }

        [Test]
        public void A_heavy_fish_hums_through_the_line_harder_when_she_runs_and_not_at_all_when_slack()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(0f, FishFeelMath.LineShudder01(0f, 0f, 1f, false, in s), Eps, "a small fish adds no hum");
            float idle = FishFeelMath.LineShudder01(0f, 1f, 0f, false, in s);
            float running = FishFeelMath.LineShudder01(0f, 1f, 1f, false, in s);
            Assert.AreEqual(s.LineHumHeavy01 * s.LineHumIdle01, idle, Eps);
            Assert.AreEqual(s.LineHumHeavy01, running, Eps);
            Assert.Greater(running, idle);
            Assert.AreEqual(0f, FishFeelMath.LineShudder01(0f, 1f, 1f, true, in s), Eps, "slack: the line goes quiet");
            Assert.AreEqual(1f, FishFeelMath.LineShudder01(0.9f, 1f, 1f, false, in s), Eps, "clamped at full shudder");
            Assert.GreaterOrEqual(FishFeelMath.LineShudder01(0.5f, 0.5f, 0.5f, false, in s), 0.5f,
                "the strain shudder is never reduced");
        }

        [Test]
        public void A_heavy_fish_brightens_the_resting_line_but_white_still_means_tension()
        {
            FishFeelSettings s = D;
            Assert.AreEqual(0f, FishFeelMath.LineWhiten01(0f, 0f, in s), Eps);
            Assert.AreEqual(s.LineWhitenHeavy01, FishFeelMath.LineWhiten01(0f, 1f, in s), Eps);
            Assert.AreEqual(1f, FishFeelMath.LineWhiten01(1f, 1f, in s), Eps);
            float prev = -1f;
            for (int i = 0; i <= 10; i++)
            {
                float v = FishFeelMath.LineWhiten01(i / 10f, 1f, in s);
                Assert.Greater(v, prev, "the tension read stays strictly rising for a heavy fish");
                prev = v;
            }
        }

        // ---- the published size --------------------------------------------------------------------------

        private static FishingState Fight(FishingPhase phase, float tension, float size01, bool slack = false)
            => new FishingState(phase, tension, 0.2f, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 5f,
                                depth01: 0f, slackWindowOpen: slack, rodBend01: 0f,
                                fishOffsetX: 1f, fishOffsetY: 0f,
                                castCharge01: 0f, castAimX: 0f, castAimY: 0f, rigDepthM: 0f,
                                fishRunDirX: 0f, fishRunDirY: 0f, size01: size01);

        [Test]
        public void FishingState_carries_her_size_and_every_older_constructor_reads_neutral()
        {
            Assert.AreEqual(0.9f, Fight(FishingPhase.FightDeep, 0f, 0.9f).Size01, Eps);
            var older = new FishingState(FishingPhase.Fighting, 0.3f, 0.1f, "fish.cod", "Cod",
                                         FishCategory.InshoreGroundfish, 5f);
            Assert.AreEqual(0f, older.Size01, "append-only: an earlier caller publishes neutral size");
            Assert.AreEqual(0f, FishingState.Idle.Size01);
        }

        // ---- the landing line --------------------------------------------------------------------------

        private sealed class FakeSizes : IFishSizeFacts
        {
            public bool TryWeightRangeKg(string fishId, out float minKg, out float maxKg)
            {
                minKg = 2f; maxKg = 11f;
                return fishId == "fish.atlantic_cod";
            }
        }

        private static CatchItem Cod(float kg)
            => new CatchItem("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, kg, 12, 0.2f);

        [Test]
        public void The_landing_line_gives_the_weight_and_a_size_word()
        {
            FishFeelSettings s = D;
            Assert.AreEqual("Landed a 9.4 kg atlantic cod — a heavy one.", LiftLine.For(Cod(9.4f), 2f, 11f, in s));
            Assert.AreEqual("Landed a 10.9 kg atlantic cod — a real monster.", LiftLine.For(Cod(10.9f), 2f, 11f, in s));
            Assert.AreEqual("Landed a 7.5 kg atlantic cod — a good one.", LiftLine.For(Cod(7.5f), 2f, 11f, in s));
            Assert.AreEqual("Landed a 5.0 kg atlantic cod — a fair one.", LiftLine.For(Cod(5f), 2f, 11f, in s));
            Assert.AreEqual("Landed a 2.3 kg atlantic cod — a small one.", LiftLine.For(Cod(2.3f), 2f, 11f, in s));
        }

        [Test]
        public void The_landing_line_says_AN_before_eight_eleven_and_eighteen()
        {
            FishFeelSettings s = D;
            StringAssert.StartsWith("Landed an 8.2 kg ", LiftLine.For(Cod(8.2f), 2f, 30f, in s));
            StringAssert.StartsWith("Landed an 11.0 kg ", LiftLine.For(Cod(11f), 2f, 30f, in s));
            StringAssert.StartsWith("Landed an 18.5 kg ", LiftLine.For(Cod(18.5f), 2f, 30f, in s));
            StringAssert.StartsWith("Landed a 12.0 kg ", LiftLine.For(Cod(12f), 2f, 30f, in s));
            StringAssert.StartsWith("Landed a 1.8 kg ", LiftLine.For(Cod(1.8f), 1f, 30f, in s));
        }

        [Test]
        public void The_fish_that_made_her_say_woah_is_always_named_heavy_or_better()
        {
            FishFeelSettings s = D;
            for (int i = 0; i <= 100; i++)
            {
                float z = i / 100f;
                FishSizeBand band = FishFeelMath.BandFor(z, in s);
                Assert.AreEqual(FishFeelMath.IsBigOne(z, in s), band >= FishSizeBand.Heavy,
                    $"size {z}: the big-one moment and the heavy word must be the same fish");
            }
            s.BigOneThreshold01 = 0.6f;
            Assert.AreEqual(FishSizeBand.Heavy, FishFeelMath.BandFor(0.7f, in s), "the word follows the dial");
        }

        [Test]
        public void The_landing_line_reads_the_species_range_through_the_Core_seam()
        {
            GameServices.FishSizes = new FakeSizes();
            Assert.AreEqual("Landed a 9.4 kg atlantic cod — a heavy one.", LiftLine.For(Cod(9.4f)));

            var stranger = new CatchItem("fish.unknown", "Sculpin", FishCategory.InshoreGroundfish, 1f, 1, 0.5f);
            Assert.AreEqual("Landed a sculpin — it's in your hand.", LiftLine.For(stranger),
                "no range known: the original sentence, unchanged");

            GameServices.FishSizes = null;
            Assert.AreEqual("Landed an atlantic cod — it's in your hand.", LiftLine.For(Cod(9.4f)),
                "no seam at all: the original sentence, unchanged");
        }

        [Test]
        public void The_species_registry_publishes_the_size_seam()
        {
            var cod = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _spawned.Add(cod);
            cod.Id = "fish.atlantic_cod";
            cod.MinWeightKg = 2f;
            cod.MaxWeightKg = 11f;
            FishSpeciesRegistry.Register(cod);

            Assert.IsNotNull(GameServices.FishSizes, "registering a species publishes the seam");
            Assert.IsTrue(GameServices.FishSizes.TryWeightRangeKg("fish.atlantic_cod", out float lo, out float hi));
            Assert.AreEqual(2f, lo);
            Assert.AreEqual(11f, hi);
            Assert.IsFalse(GameServices.FishSizes.TryWeightRangeKg("fish.nobody", out _, out _));
        }

        // ---- the controller publishes her size, and says woah for a big one ------------------------------

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 6;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        [Test]
        public void The_hook_set_publishes_her_size_and_says_woah_only_for_a_big_one()
        {
            int fought = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var cod = ScriptableObject.CreateInstance<FishSpeciesDef>();
                _spawned.Add(cod);
                cod.Id = "fish.cod"; cod.DisplayName = "Cod"; cod.Category = FishCategory.InshoreGroundfish;
                cod.RegionIds = new[] { "region.coddle_cove" };
                cod.AllowedGear = Gear.Handline | Gear.Longline;
                cod.Seasons = SeasonMask.AllYear;
                cod.MinTide = -10f; cod.MaxTide = 10f; cod.StartHour = 0f; cod.EndHour = 24f;
                cod.MinWeightKg = 1f; cod.MaxWeightKg = 6f;
                cod.BaseValue = 12; cod.SupplyElasticity = 0.2f; cod.SpawnWeight = 1f;

                var go = new GameObject("Fisher");
                _spawned.Add(go);
                var c = go.AddComponent<FishingController>();
                c.Configure(new FakeHold(), new[] { cod }, "region.coddle_cove", Gear.Handline, seed);

                _thoughts.Clear();
                _cues.Clear();
                FlickGestures.CastLine(c);
                for (float t = 0f; c.Phase != FishingPhase.Fighting && t < 30f; t += 0.05f)
                    c.Tick(0.05f, false);
                if (c.Phase != FishingPhase.Fighting) continue;
                fought++;

                FishingState s = c.State;
                Assert.AreEqual(FishFeelMath.Size01(s.WeightKg, 1f, 6f), s.Size01, Eps,
                    $"seed {seed}: the published size is her weight's place in her species' range");

                bool big = FishFeelMath.IsBigOne(s.Size01, D);
                Assert.AreEqual(big ? 1 : 0, _cues.Count,
                    $"seed {seed}: size {s.Size01:F2} — one 'woah' cue for a big one, silence otherwise");
                if (big) Assert.AreEqual(InnerVoiceCues.BigOneHooked, _cues[0]);
                Assert.AreEqual(0, _thoughts.Count,
                    $"seed {seed}: the rod loop names the moment and never speaks the words itself");

                // The fight going on must not ask again: once per big hook, at the hook-set.
                for (int i = 0; i < 10 && c.Phase == FishingPhase.Fighting; i++) c.Tick(0.05f, false);
                Assert.AreEqual(big ? 1 : 0, _cues.Count, $"seed {seed}: the cue is raised once per hook");
            }
            Assert.Greater(fought, 0, "the rig must reach a fight for this test to mean anything");
        }

        [Test]
        public void The_cue_id_is_the_stable_one_a_line_asset_names()
        {
            // Append-only: the line asset's SubjectId is this string, so renaming it silences the line.
            Assert.AreEqual("cue.fishing.big_one_hooked", InnerVoiceCues.BigOneHooked);
            Assert.AreNotEqual(InnerVoiceCues.FeedingSea, InnerVoiceCues.BigOneHooked);
        }

        [Test]
        public void TheShippedLibrary_HasALineForTheBigOne()
        {
            var lib = Resources.Load<InnerVoiceLibrary>(InnerVoiceLibrary.ResourcesPath);
            Assert.IsNotNull(lib, "the inner-voice library must load from Resources");

            InnerVoiceLineDef found = null;
            foreach (InnerVoiceLineDef d in lib.Lines)
                if (d != null && d.WantsCue(InnerVoiceCues.BigOneHooked)) { found = d; break; }

            Assert.IsNotNull(found,
                $"no line in the library answers '{InnerVoiceCues.BigOneHooked}', so the rod loop's cue " +
                "would be raised and never heard");
            Assert.IsFalse(string.IsNullOrWhiteSpace(found.Line));
            Assert.IsFalse(found.OnceOnly,
                "the rod loop says it once per big hook; a once-ever line would go silent after the first big fish");
        }

        // ---- the shipped defaults ----------------------------------------------------------------------

        [Test]
        public void The_default_dials_are_ordered_so_every_curve_points_the_right_way()
        {
            FishFeelSettings s = D;
            Assert.Less(s.SizeSmallBelow01, s.SizeGoodFrom01);
            Assert.Less(s.SizeGoodFrom01, s.BigOneThreshold01);
            Assert.LessOrEqual(s.BigOneThreshold01, s.SizeTrophyFrom01);
            Assert.Greater(s.ShakeHzSmall, s.ShakeHzHeavy, "small fish shake faster");
            Assert.Less(s.ShakeDegreesSmall, s.ShakeDegreesHeavy, "heavy fish shake harder");
            Assert.Less(s.HookKickSmallScale, s.HookKickHeavyScale);
            Assert.Less(s.LineWhitenHeavy01, 0.5f, "white must stay the tension read, not the size read");
        }
    }
}
