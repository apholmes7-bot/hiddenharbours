using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Slice 3 of the minigame lane — the STRIKE gesture, both §10.2 candidates behind tunables
    /// ("pull back and press maybe?" — the owner picks in play). Pins the pure yank maths, the
    /// accumulator's one-committed-motion contract, and the controller integration: each candidate
    /// hooks with the other disabled, and the gesture is judged by the SAME sim the tells render
    /// from — a yank on a tease is exactly as early as a press on one.
    /// </summary>
    public class StrikeGestureTests
    {
        private const float Dt = 0.05f;

        // ---- the pure maths -------------------------------------------------------------------

        [Test]
        public void BackwardSpeed_IsPositiveHaulingAway_NegativeTowardTheWater()
        {
            Vector2 angler = Vector2.zero, farEnd = new Vector2(10f, 0f);   // bobber out to the right

            float back = StrikeGestureMath.BackwardSpeed(new Vector2(5f, 0f), new Vector2(4.5f, 0f),
                                                         0.05f, angler, farEnd);
            Assert.AreEqual(10f, back, 1e-3f, "0.5 m away from the bobber in 0.05 s = 10 m/s of haul");

            float fwd = StrikeGestureMath.BackwardSpeed(new Vector2(5f, 0f), new Vector2(5.5f, 0f),
                                                        0.05f, angler, farEnd);
            Assert.AreEqual(-10f, fwd, 1e-3f, "toward the water is the cast's direction, not a strike");
        }

        [Test]
        public void BackwardSpeed_DegenerateSpans_ReadZeroNeverNaN()
        {
            Vector2 p = new Vector2(3f, 1f);
            Assert.AreEqual(0f, StrikeGestureMath.BackwardSpeed(p, p + Vector2.one, 0f, Vector2.zero, Vector2.right));
            Assert.AreEqual(0f, StrikeGestureMath.BackwardSpeed(p, p + Vector2.one, 0.05f, Vector2.zero, Vector2.zero),
                "angler standing on the far end has no axis — 0, never NaN");
        }

        // ---- the accumulator ------------------------------------------------------------------

        private static StrikeSettings YankOnly => new StrikeSettings
        {
            PressStrikes = false,
            PullBackStrikes = true,
            YankMinSpeedMps = 6f,
            YankMinMetres = 0.8f,
            YankStallFraction01 = 0.25f,
        };

        private static bool Drag(StrikeYank yank, Vector2 from, Vector2 step, int ticks, in StrikeSettings s,
                                 Vector2 farEnd)
        {
            bool fired = false;
            Vector2 p = from;
            yank.Tick(Dt, p, true, Vector2.zero, farEnd, in s);   // establish prev
            for (int i = 0; i < ticks; i++)
            {
                p += step;
                fired |= yank.Tick(Dt, p, true, Vector2.zero, farEnd, in s);
            }
            return fired;
        }

        [Test]
        public void ACommittedHaul_Fires_ASlowDriftNever()
        {
            var s = YankOnly;
            Vector2 farEnd = new Vector2(10f, 0f);

            // 0.5 m per 0.05 s tick = 10 m/s backward: 2 ticks bank 1.0 m ≥ 0.8 → fires.
            Assert.IsTrue(Drag(new StrikeYank(), new Vector2(6f, 0f), new Vector2(-0.5f, 0f), 3, s, farEnd));

            // 0.1 m per tick = 2 m/s: under the 6 m/s gate — banked nothing, never fires.
            Assert.IsFalse(Drag(new StrikeYank(), new Vector2(6f, 0f), new Vector2(-0.1f, 0f), 30, s, farEnd));
        }

        [Test]
        public void AStall_ForfeitsTheBankedTravel()
        {
            var s = YankOnly;
            Vector2 farEnd = new Vector2(10f, 0f);
            var yank = new StrikeYank();
            Vector2 p = new Vector2(6f, 0f);
            yank.Tick(Dt, p, true, Vector2.zero, farEnd, in s);

            p += new Vector2(-0.5f, 0f);   // one qualifying step: 0.5 m banked (under the 0.8 m bar)
            Assert.IsFalse(yank.Tick(Dt, p, true, Vector2.zero, farEnd, in s));
            Assert.IsFalse(yank.Tick(Dt, p, true, Vector2.zero, farEnd, in s), "a dead stop stalls it");

            p += new Vector2(-0.5f, 0f);   // hauling again — but the bank was forfeit, 0.5 < 0.8
            Assert.IsFalse(yank.Tick(Dt, p, true, Vector2.zero, farEnd, in s),
                "a yank is ONE committed motion — a stall means starting over");
        }

        // ---- the controller integration ---------------------------------------------------------

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 6;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishingStateChanged>();
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishingStateChanged>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishingController Rig(bool press, bool pullBack, int seed)
        {
            var bite = ScriptableObject.CreateInstance<BiteDef>();
            bite.Id = "bite.test";
            bite.FalseNibblesMin = 0; bite.FalseNibblesMax = 0;
            bite.NibbleGapMinSeconds = 0.5f; bite.NibbleGapMaxSeconds = 1f;
            bite.NibbleDipSeconds = 0.3f;
            bite.TakeWindowSeconds = 3f;           // roomy — these tests judge the gesture, not the clock
            bite.ReturnChance01 = 1f; bite.ReturnDelayMinSeconds = 0.5f; bite.ReturnDelayMaxSeconds = 1f;
            bite.MaxPasses = 3;
            _spawned.Add(bite);

            var fish = ScriptableObject.CreateInstance<FishSpeciesDef>();
            fish.Id = "fish.cod"; fish.DisplayName = "Cod"; fish.Category = FishCategory.InshoreGroundfish;
            fish.RegionIds = new[] { "region.coddle_cove" };
            fish.AllowedGear = Gear.Handline | Gear.Longline;
            fish.Seasons = SeasonMask.AllYear;
            fish.MinTide = -10f; fish.MaxTide = 10f; fish.StartHour = 0f; fish.EndHour = 24f;
            fish.MinWeightKg = 1f; fish.MaxWeightKg = 6f;
            fish.BaseValue = 12; fish.SupplyElasticity = 0.2f; fish.SpawnWeight = 1f;
            fish.Bite = bite;
            _spawned.Add(fish);

            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.Strike = new StrikeSettings
            {
                PressStrikes = press,
                PullBackStrikes = pullBack,
                YankMinSpeedMps = 6f,
                YankMinMetres = 0.8f,
                YankStallFraction01 = 0.25f,
            };
            _spawned.Add(config);

            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(new FakeHold(), new[] { fish }, "region.coddle_cove", Gear.Handline, seed,
                        null, config);
            return c;
        }

        private static void TickUntil(FishingController c, FishingPhase phase, float maxSeconds = 60f)
        {
            float t = 0f;
            while (c.Phase != phase && t < maxSeconds) { c.Tick(Dt, false); t += Dt; }
            Assert.AreEqual(phase, c.Phase, $"never reached {phase} (sat in {c.Phase})");
        }

        /// <summary>Haul the pointer from the bobber back past the angler at ~10 m/s.</summary>
        private static void YankPointer(FishingController c)
        {
            Vector2 angler = Vector2.zero;   // the rig's GameObject sits at the origin
            Vector2 castAim = new Vector2(c.State.CastAimX, c.State.CastAimY);
            Vector2 dir = castAim.sqrMagnitude > 1e-4f ? castAim.normalized : Vector2.right;

            Vector2 p = angler + dir * 2f;   // start out toward the bobber
            c.Tick(Dt, false, p, true);      // establish the gesture's prev sample
            for (int i = 0; i < 5 && c.Phase == FishingPhase.Bite; i++)
            {
                p -= dir * 0.5f;             // 0.5 m per 0.05 s tick = 10 m/s of haul
                c.Tick(Dt, false, p, true);
            }
        }

        [Test]
        public void PullBackAlone_TheYankHooks_ThePressDoesNot()
        {
            var c = Rig(press: false, pullBack: true, seed: 42);
            FlickGestures.CastLine(c);
            TickUntil(c, FishingPhase.Bite);

            c.Tick(Dt, actionHeld: true);   // the press candidate is OFF — this must not hook
            Assert.AreEqual(FishingPhase.Bite, c.Phase, "the press is disabled; the take stays open");

            c.Tick(Dt, false);              // release the action so the edge logic is clean
            YankPointer(c);
            Assert.AreEqual(FishingPhase.Fighting, c.Phase, "the pull-back sets the hook");
        }

        [Test]
        public void PressAlone_ThePressHooks_TheYankDoesNot()
        {
            var c = Rig(press: true, pullBack: false, seed: 42);
            FlickGestures.CastLine(c);
            TickUntil(c, FishingPhase.Bite);

            YankPointer(c);
            Assert.AreEqual(FishingPhase.Bite, c.Phase, "the yank is disabled; hauling does nothing");

            c.Tick(Dt, actionHeld: true);
            Assert.AreEqual(FishingPhase.Fighting, c.Phase, "the press sets the hook");
        }
    }
}
