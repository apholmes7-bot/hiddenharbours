using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Cleanliness slice 1, the pure maths (owner ask 2026-11-15). Every number is the test's own, so the
    /// owner retuning <see cref="GameConfig.Cleanliness"/> or a <see cref="WashStationDef"/> never breaks
    /// these. One game hour is <see cref="Hour"/> clock seconds. Dirt is 0 clean .. 1 filthy.
    /// </summary>
    public class CleanlinessMathTests
    {
        private const double Hour = 100d;
        private const float Eps = 1e-5f;

        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        /// <summary>Passive 0.02/h; hard work +0.08/h (bump 0), fish +0.1/h (bump 0.05); window 2 h.</summary>
        internal static CleanlinessSettings Settings() => new CleanlinessSettings
        {
            Curve = new NeedCurve { RisePerGameHour = 0.02f, MildAt = 0.5f, StrongAt = 0.75f },
            Sources = new[]
            {
                new DirtSourceRate(DirtActivity.HardWork, 0.08f, 0f),
                new DirtSourceRate(DirtActivity.FishHandling, 0.1f, 0.05f),
            },
            MaxStretchGameMinutes = 120f,
            SeaStationId = CleanlinessSettings.DefaultSeaStationId,
        };

        internal static WashStationDef Station(List<Object> made, string id, WashStationKind kind, float rate,
                                               float ceiling, bool bothWays = false, bool sitOnly = false)
        {
            var def = ScriptableObject.CreateInstance<WashStationDef>();
            def.Id = id;
            def.Kind = kind;
            def.CleanRatePerGameHour = rate;
            def.Ceiling = ceiling;
            def.PullsBothWays = bothWays;
            def.SitOnly = sitOnly;
            made.Add(def);
            return def;
        }

        private WashStationDef Sea() => Station(_made, "washstation.sea", WashStationKind.Sea, 1f, 0.5f, bothWays: true);
        private WashStationDef Sink() => Station(_made, "washstation.sink", WashStationKind.Sink, 1f, 0.8f);
        private WashStationDef Shower() => Station(_made, "washstation.shower", WashStationKind.Shower, 4f, 1f);

        private static PlayerCleanlinessDto At(float dirt, double since, DirtActivity activity = DirtActivity.None,
                                               string washing = "", double stretchSince = -1d)
            => new PlayerCleanlinessDto
            {
                Dirt = dirt, SinceGameSeconds = since, Activity = (int)activity,
                WashStationId = washing, StretchSinceGameSeconds = stretchSince,
            };

        // ---- getting dirty -------------------------------------------------------------------------

        [Test]
        public void Passive_RisesAtTheCurveRate()
        {
            var s = Settings();
            Assert.AreEqual(0.2f, CleanlinessMath.DirtAt(At(0f, 0d), s, default, 10d * Hour, Hour), Eps);
            Assert.AreEqual(1f, CleanlinessMath.DirtAt(At(0.9f, 0d), s, default, 100d * Hour, Hour), "clamped at 1");
        }

        [Test]
        public void ActivityStretch_AddsItsRateOnTopOfPassive_ForTheWindowOnly()
        {
            var s = Settings();
            var state = At(0f, 0d, DirtActivity.HardWork, stretchSince: 0d);
            Assert.AreEqual(0.1f, CleanlinessMath.DirtAt(state, s, default, 1d * Hour, Hour), Eps, "0.02 + 0.08 per hour");
            // 2 h window: 0.2 by then, then passive only: +0.02 * 3 h.
            Assert.AreEqual(0.26f, CleanlinessMath.DirtAt(state, s, default, 5d * Hour, Hour), Eps);
        }

        [Test]
        public void EachSource_RaisesDirtAtItsStatedRate()
        {
            var s = CleanlinessSettings.Default;
            foreach (DirtSourceRate row in s.Sources)
            {
                var state = At(0f, 0d, row.Kind, stretchSince: 0d);
                float expected = s.Curve.RisePerGameHour + row.RisePerGameHour;
                Assert.AreEqual(expected, CleanlinessMath.DirtAt(state, s, default, 1d * Hour, Hour), Eps, row.Kind.ToString());
                Assert.AreEqual(row.PerAction, CleanlinessMath.BumpOf(row.Kind, s), Eps, row.Kind + " bump");
            }
            Assert.AreEqual(0f, CleanlinessMath.RateOf(DirtActivity.None, s));
        }

        // ---- the three ceilings --------------------------------------------------------------------

        [Test]
        public void Sink_NeverPassesItsCeiling_HoweverLong()
        {
            WashStationDef sink = Sink();
            foreach (double hours in new[] { 0.25d, 1d, 10d, 1000d, 1e6d })
            {
                float d = CleanlinessMath.AfterWash(1f, sink, hours);
                Assert.GreaterOrEqual(d, 0.2f - Eps, $"{hours} h at a sink");
            }
            Assert.AreEqual(0.2f, CleanlinessMath.AfterWash(1f, sink, 1e6d), Eps, "it gets there and stops");
            Assert.AreEqual(0.1f, CleanlinessMath.AfterWash(0.1f, sink, 5d), Eps, "and never dirties a cleaner player");
        }

        [Test]
        public void Shower_ReachesFullyClean()
        {
            Assert.AreEqual(0f, CleanlinessMath.AfterWash(1f, Shower(), 0.25d), Eps);
        }

        [Test]
        public void Sea_ConvergesToItsBase_FromAboveAndFromBelow()
        {
            WashStationDef sea = Sea();
            Assert.AreEqual(0.7f, CleanlinessMath.AfterWash(0.9f, sea, 0.2d), Eps, "rinses down");
            Assert.AreEqual(0.5f, CleanlinessMath.AfterWash(0.9f, sea, 50d), Eps, "to the base, not past");
            Assert.AreEqual(0.2f, CleanlinessMath.AfterWash(0f, sea, 0.2d), Eps, "salts a clean player up");
            Assert.AreEqual(0.5f, CleanlinessMath.AfterWash(0f, sea, 50d), Eps, "to the base, not past");
            Assert.AreEqual(0.5f, CleanlinessMath.AfterWash(0.5f, sea, 3d), Eps, "at the base it holds");
        }

        [Test]
        public void Toilet_DoesNothing()
        {
            WashStationDef toilet = Station(_made, "washstation.toilet", WashStationKind.Toilet, 5f, 1f, sitOnly: true);
            Assert.IsFalse(CleanlinessMath.PullOf(toilet).Acts);
            Assert.AreEqual(0.6f, CleanlinessMath.AfterWash(0.6f, toilet, 10d));
        }

        [Test]
        public void Washing_ReplacesPassive_ForTheWindow_ThenPassiveResumes()
        {
            var s = Settings();
            WashStationDef shower = Shower();
            var state = At(0.8f, 0d, washing: shower.Id, stretchSince: 0d);
            WashPull pull = CleanlinessMath.PullOf(shower);
            Assert.AreEqual(0f, CleanlinessMath.DirtAt(state, s, pull, 1d * Hour, Hour), Eps);
            // window 2 h, then +0.02/h for 1 h
            Assert.AreEqual(0.02f, CleanlinessMath.DirtAt(state, s, pull, 3d * Hour, Hour), Eps);
        }

        // ---- determinism: stepping equals catch-up --------------------------------------------------

        [TestCase(DirtActivity.None, "")]
        [TestCase(DirtActivity.HardWork, "")]
        [TestCase(DirtActivity.None, "washstation.sea")]
        [TestCase(DirtActivity.None, "washstation.sink")]
        public void Stepping_EqualsCatchUp(DirtActivity activity, string stationId)
        {
            var s = Settings();
            WashStationDef station = stationId == "washstation.sea" ? Sea()
                                   : stationId == "washstation.sink" ? Sink() : null;
            WashPull pull = CleanlinessMath.PullOf(station);
            float start = stationId == "washstation.sea" ? 0.1f : 0.9f;
            const double t0 = 1000d;

            var whole = At(start, t0, activity, stationId, t0);
            var stepped = At(start, t0, activity, stationId, t0);
            double[] steps = { 7d, 33.3d, 0.5d, 59d, 101d, 12.25d, 250d, 3d, 400d };
            double t = t0;
            foreach (double dt in steps)
            {
                t += dt;
                CleanlinessMath.Settle(stepped, s, pull, t, Hour);
            }
            float caught = CleanlinessMath.DirtAt(whole, s, pull, t, Hour);
            Assert.AreEqual(caught, stepped.Dirt, Eps, "settling at any instants lands on the one-read level");
            Assert.AreEqual(caught, CleanlinessMath.DirtAt(stepped, s, pull, t, Hour), Eps);
        }

        [Test]
        public void SameInputs_SameDirt()
        {
            var s = Settings();
            var a = At(0.3f, 50d, DirtActivity.FishHandling, stretchSince: 50d);
            var b = At(0.3f, 50d, DirtActivity.FishHandling, stretchSince: 50d);
            Assert.AreEqual(System.BitConverter.GetBytes(CleanlinessMath.DirtAt(a, s, default, 777d, Hour)),
                            System.BitConverter.GetBytes(CleanlinessMath.DirtAt(b, s, default, 777d, Hour)));
        }

        [Test]
        public void Unstamped_HoldsItsDirt()
        {
            var s = Settings();
            Assert.AreEqual(0f, CleanlinessMath.DirtAt(new PlayerCleanlinessDto(), s, default, 1e6d, Hour));
        }

        [Test]
        public void Rebase_KeepsDirt_EndsTheStretch()
        {
            var state = At(0.4f, 10d, DirtActivity.HardWork, stretchSince: 10d);
            CleanlinessMath.Rebase(state, 5000d);
            Assert.AreEqual(0.4f, state.Dirt);
            Assert.AreEqual(5000d, state.SinceGameSeconds);
            Assert.AreEqual(0, state.Activity);
            Assert.AreEqual("", state.WashStationId);
            Assert.Less(state.StretchSinceGameSeconds, 0d);
        }
    }
}
