using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2, Wave 2 — the pure depth-drop maths (design/rod-fishing-v2-brainstorm.md §2.1/§2.3;
    /// <see cref="DepthDropMath"/>). Pins the properties the diegetic read rests on:
    ///  • the fall rate scales with rig weight, MONOTONICALLY — heavier reaches a band strictly sooner,
    ///    so counting the fall is a real depth read (owner decision #4);
    ///  • the reachable band clamps against bathymetry AND the line the reel carries;
    ///  • the bottom slack triggers EXACTLY at the floor (the "you felt the floor" pop);
    ///  • the off-floor sweet window excludes resting on the floor (the lift is the skill beat);
    ///  • the gear branch (§2.1's table): Jig/Longline always drop; a Handline only when weighted;
    ///  • the depth affinity weights the intended DIRECTION and is neutral whenever depth has nothing
    ///    to say; everything is NaN-safe.
    /// All pure statics — no scene, no clock, no RNG (rule 5).
    /// </summary>
    public class DepthDropMathTests
    {
        private static readonly DepthDropSettings S = DepthDropSettings.Default;

        // ---- the fall: weight → speed (count-the-fall is real) ------------------------------

        [Test]
        public void SinkSpeed_ScalesWithWeight_Monotonically()
        {
            float prev = -1f;
            for (float kg = 0f; kg <= 3f; kg += 0.05f)
            {
                float v = DepthDropMath.SinkSpeedMps(kg, S.SinkSpeedPerKgMps, S.MinSinkSpeedMps, S.MaxSinkSpeedMps);
                Assert.GreaterOrEqual(v, prev, $"sink speed must never fall as weight rises (at {kg:0.00} kg)");
                prev = v;
            }
        }

        [Test]
        public void SinkSpeed_ClampsToTheOwnerRange()
        {
            Assert.AreEqual(S.MinSinkSpeedMps,
                DepthDropMath.SinkSpeedMps(0f, S.SinkSpeedPerKgMps, S.MinSinkSpeedMps, S.MaxSinkSpeedMps),
                1e-5f, "a weightless rig still sinks at the minimum");
            Assert.AreEqual(S.MaxSinkSpeedMps,
                DepthDropMath.SinkSpeedMps(1000f, S.SinkSpeedPerKgMps, S.MinSinkSpeedMps, S.MaxSinkSpeedMps),
                1e-5f, "an absurd lead is capped at the maximum");
        }

        [Test]
        public void HeavierRig_ReachesABand_StrictlySooner()
        {
            // The tactical promise: the same 12 m band, two rigs — the heavy one arrives in fewer ticks.
            const float floor = 30f, targetDepth = 12f, dt = 0.1f;
            int TicksToReach(float kg)
            {
                float speed = DepthDropMath.SinkSpeedMps(kg, S.SinkSpeedPerKgMps, S.MinSinkSpeedMps, S.MaxSinkSpeedMps);
                float d = 0f; int ticks = 0;
                while (d < targetDepth && ticks < 10000) { d = DepthDropMath.FallStep(d, speed, dt, floor); ticks++; }
                return ticks;
            }
            Assert.Less(TicksToReach(1.0f), TicksToReach(0.2f),
                "the 1 kg jig must reach 12 m in strictly fewer ticks than the 0.2 kg rig — " +
                "the count-the-fall read depends on it");
        }

        [Test]
        public void FallStep_ClampsAtTheFloor_AndNeverRegresses()
        {
            Assert.AreEqual(8f, DepthDropMath.FallStep(7.9f, 5f, 1f, 8f), 1e-5f, "the fall stops AT the floor");
            Assert.AreEqual(3f, DepthDropMath.FallStep(3f, 2f, 0f, 8f), 1e-5f, "zero dt is a no-op");
            Assert.AreEqual(3f, DepthDropMath.FallStep(3f, 2f, -1f, 8f), 1e-5f, "negative dt never lifts the rig");
        }

        [Test]
        public void ReelStep_LiftsAndStopsAtTheSurface()
        {
            Assert.AreEqual(7.85f, DepthDropMath.ReelStep(8f, 1.5f, 0.1f), 1e-5f);
            Assert.AreEqual(0f, DepthDropMath.ReelStep(0.05f, 1.5f, 1f), 1e-5f, "reeling past the surface holds at 0");
        }

        // ---- the reachable band (bathymetry vs the line) ------------------------------------

        [Test]
        public void FloorMeters_IsTheShallowerOf_BathymetryAndLine()
        {
            Assert.AreEqual(8f, DepthDropMath.FloorMeters(8f, 60f), 1e-5f, "shallow water: the seabed is the floor");
            Assert.AreEqual(60f, DepthDropMath.FloorMeters(200f, 60f), 1e-5f, "deep water: the line runs out first");
            Assert.AreEqual(60f, DepthDropMath.FloorMeters(float.PositiveInfinity, 60f), 1e-5f,
                "no bathymetry (service absent) → line-length-capped only");
            Assert.AreEqual(0f, DepthDropMath.FloorMeters(-2f, 60f), 1e-5f, "a bared spot floors at 0");
        }

        // ---- the bottom tell (slack exactly at the floor) -----------------------------------

        [Test]
        public void Bottomed_TriggersExactlyAtTheFloor()
        {
            Assert.IsFalse(DepthDropMath.IsBottomed(7.999f, 8f), "a hair above the floor is NOT bottomed");
            Assert.IsTrue(DepthDropMath.IsBottomed(8f, 8f), "AT the floor the line goes slack — exact, not fuzzy");
            Assert.IsTrue(DepthDropMath.IsBottomed(0f, 0f), "a collapsed band is bottomed the moment it's wet");
        }

        [Test]
        public void FallIntoTheFloor_BottomsOnTheClampTick()
        {
            // The clamp in FallStep and the ≥ in IsBottomed must meet exactly — the tell fires the tick
            // the rig lands, not one late.
            float d = DepthDropMath.FallStep(7.9f, 5f, 1f, 8f);
            Assert.IsTrue(DepthDropMath.IsBottomed(d, 8f));
        }

        [Test]
        public void SweetWindow_IsJustOffTheFloor_NotOnIt()
        {
            const float floor = 20f;
            float w = S.BottomSweetWindowMeters;
            Assert.IsTrue(DepthDropMath.InBottomSweetWindow(floor - w * 0.5f, floor, w), "inside the lift window");
            Assert.IsTrue(DepthDropMath.InBottomSweetWindow(floor - w, floor, w), "the window's top edge counts");
            Assert.IsFalse(DepthDropMath.InBottomSweetWindow(floor, floor, w),
                "resting ON the floor is outside — bottom out THEN lift is the skill beat");
            Assert.IsFalse(DepthDropMath.InBottomSweetWindow(floor * 0.5f, floor, w), "mid-column is not the window");
        }

        [Test]
        public void Depth01_ReadsSurfaceToFloor()
        {
            Assert.AreEqual(0f, DepthDropMath.Depth01(0f, 40f), 1e-5f);
            Assert.AreEqual(0.5f, DepthDropMath.Depth01(20f, 40f), 1e-5f);
            Assert.AreEqual(1f, DepthDropMath.Depth01(40f, 40f), 1e-5f);
            Assert.AreEqual(1f, DepthDropMath.Depth01(0f, 0f), 1e-5f, "a collapsed band reads on-the-bottom");
        }

        // ---- the gear branch (§2.1's table) -------------------------------------------------

        [Test]
        public void GearBranch_JigAndLongline_AlwaysDrop()
        {
            Assert.IsTrue(DepthDropMath.IsWeightedRig(Gear.Jig, 0f, S.WeightedHandlineMinKg));
            Assert.IsTrue(DepthDropMath.IsWeightedRig(Gear.Longline, 0f, S.WeightedHandlineMinKg));
        }

        [Test]
        public void GearBranch_Handline_DropsOnlyWhenWeighted()
        {
            Assert.IsFalse(DepthDropMath.IsWeightedRig(Gear.Handline, S.WeightedHandlineMinKg * 0.5f, S.WeightedHandlineMinKg),
                "a light handline keeps the cast/bobber path");
            Assert.IsTrue(DepthDropMath.IsWeightedRig(Gear.Handline, S.WeightedHandlineMinKg, S.WeightedHandlineMinKg),
                "at the threshold the handline fishes the column");
        }

        [Test]
        public void GearBranch_OtherGear_NeverDrops()
        {
            Assert.IsFalse(DepthDropMath.IsWeightedRig(Gear.Net, 5f, S.WeightedHandlineMinKg));
            Assert.IsFalse(DepthDropMath.IsWeightedRig(Gear.Trap, 5f, S.WeightedHandlineMinKg));
            Assert.IsFalse(DepthDropMath.IsWeightedRig(Gear.ClamFork, 5f, S.WeightedHandlineMinKg));
            Assert.IsFalse(DepthDropMath.IsWeightedRig(Gear.None, 5f, S.WeightedHandlineMinKg));
        }

        // ---- the zones + the affinity direction ---------------------------------------------

        [Test]
        public void Zones_TileWithoutGaps_AtTheOwnerThresholds()
        {
            FishDepthBand At(float d) => DepthDropMath.ZoneForDepth(d, S.TidepoolMaxMeters, S.ShallowsMaxMeters,
                S.InshoreMaxMeters, S.MidwaterMaxMeters, S.DeepMaxMeters);
            Assert.AreEqual(FishDepthBand.Tidepool, At(0f));
            Assert.AreEqual(FishDepthBand.Tidepool, At(S.TidepoolMaxMeters));
            Assert.AreEqual(FishDepthBand.Shallows, At(S.TidepoolMaxMeters + 0.001f));
            Assert.AreEqual(FishDepthBand.Inshore, At(S.ShallowsMaxMeters + 0.001f));
            Assert.AreEqual(FishDepthBand.Midwater, At(S.InshoreMaxMeters + 0.001f));
            Assert.AreEqual(FishDepthBand.Deep, At(S.MidwaterMaxMeters + 0.001f));
            Assert.AreEqual(FishDepthBand.Abyssal, At(S.DeepMaxMeters + 0.001f));
        }

        [Test]
        public void Affinity_BoostsInBand_DampsOffBand()
        {
            float mid = (S.InshoreMaxMeters + S.MidwaterMaxMeters) * 0.5f;   // squarely Midwater
            float inBand = DepthDropMath.SpeciesDepthAffinity(FishDepthBand.Midwater, false, mid, 60f, in S);
            float offBand = DepthDropMath.SpeciesDepthAffinity(FishDepthBand.Deep, false, mid, 60f, in S);
            Assert.Greater(inBand, 1f, "holding in the species' zone weights it UP");
            Assert.Less(offBand, 1f, "holding outside its zones damps it");
            Assert.Greater(offBand, 0f, "…but never to zero — depth is a weight, not a wall");
        }

        [Test]
        public void Affinity_BottomSpecies_PaysInTheSweetWindow_NotOnTheFloor()
        {
            const float floor = 40f;
            float inWindow = floor - S.BottomSweetWindowMeters * 0.5f;
            float bottomInWindow = DepthDropMath.SpeciesDepthAffinity(FishDepthBand.None, true, inWindow, floor, in S);
            float bottomOnFloor = DepthDropMath.SpeciesDepthAffinity(FishDepthBand.None, true, floor, floor, in S);
            float bottomMidColumn = DepthDropMath.SpeciesDepthAffinity(FishDepthBand.None, true, floor * 0.5f, floor, in S);
            Assert.Greater(bottomInWindow, bottomMidColumn, "just off the floor targets the bottom fish");
            Assert.Greater(bottomInWindow, bottomOnFloor, "the lift beats leaving it on the mud");
            Assert.AreEqual(1f, bottomOnFloor, 1e-5f, "resting on the floor is merely neutral");
        }

        [Test]
        public void Affinity_IsNeutral_WhenDepthHasNothingToSay()
        {
            Assert.AreEqual(1f, DepthDropMath.SpeciesDepthAffinity(FishDepthBand.Midwater, true,
                CatchContext.NoDepth, 40f, in S), 1e-5f, "no depth game → exactly neutral");
            Assert.AreEqual(1f, DepthDropMath.SpeciesDepthAffinity(FishDepthBand.None, false,
                20f, 40f, in S), 1e-5f, "an unauthored species → exactly neutral at any depth");
        }

        // ---- NaN safety ---------------------------------------------------------------------

        [Test]
        public void NaN_Inputs_NeverPropagate()
        {
            float nan = float.NaN;
            Assert.IsFalse(float.IsNaN(DepthDropMath.SinkSpeedMps(nan, S.SinkSpeedPerKgMps, S.MinSinkSpeedMps, S.MaxSinkSpeedMps)));
            Assert.IsFalse(float.IsNaN(DepthDropMath.FallStep(nan, nan, nan, nan)));
            Assert.IsFalse(float.IsNaN(DepthDropMath.ReelStep(nan, nan, nan)));
            Assert.IsFalse(float.IsNaN(DepthDropMath.FloorMeters(nan, nan)));
            Assert.IsFalse(float.IsNaN(DepthDropMath.Depth01(nan, nan)));
            Assert.IsFalse(float.IsNaN(DepthDropMath.SpeciesDepthAffinity(FishDepthBand.Deep, true, nan, nan, in S)));
            Assert.DoesNotThrow(() => DepthDropMath.IsBottomed(nan, nan));
            Assert.DoesNotThrow(() => DepthDropMath.InBottomSweetWindow(nan, nan, nan));
            Assert.IsFalse(DepthDropMath.IsWeightedRig(Gear.Handline, nan, S.WeightedHandlineMinKg),
                "a NaN rig weight reads weightless → the cast branch");
        }
    }
}
