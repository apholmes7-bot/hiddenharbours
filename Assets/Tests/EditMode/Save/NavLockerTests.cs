using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The chartplotter's DATA rules (ADR 0025 S6): what the nav locker will and will not store, the
    /// track's ring, the range ladder, and the recorder's one promise — that the track accrues whether
    /// or not any plotter exists.
    ///
    /// <para>Pure Core throughout: no scene, no canvas, no instrument. That is deliberate and is the
    /// point of the arrangement — if these rules needed the glass to be testable, the glass would be
    /// load-bearing for the data, and the honesty invariant would be a comment rather than a fact.</para>
    /// </summary>
    public class NavLockerTests
    {
        private const string StP = "region.st_peters";
        private const string Coddle = "region.coddle_cove";

        private static ChartplotterSettings Cfg => ChartplotterSettings.Default;

        private static SaveData FreshSave() => new SaveData { SchemaVersion = SaveMigration.CurrentVersion };

        // ---- waypoints -------------------------------------------------------------------------------

        [Test]
        public void Waypoints_ReadBackOnlyInTheirOwnRegion()
        {
            SaveData s = FreshSave();
            NavLocker.AddWaypoint(s, new NavWaypoint(StP, "HOME", new Vector2(10f, 10f),
                                                     NavWaypointKind.Anchor), Cfg);
            NavLocker.AddWaypoint(s, new NavWaypoint(Coddle, "AWAY", new Vector2(20f, 20f),
                                                     NavWaypointKind.Mark), Cfg);

            var into = new List<NavWaypoint>();
            NavLocker.WaypointsIn(s, StP, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("HOME", into[0].Name,
                "the same (x, y) is different water in another region — a chart must never draw the " +
                "other region's marks");
        }

        [Test]
        public void MarkingAtTheCap_IsRefused_NotSilentlyEvicting()
        {
            SaveData s = FreshSave();
            for (int i = 0; i < Cfg.MaxWaypoints; i++)
                Assert.IsTrue(NavLocker.AddWaypoint(s,
                    new NavWaypoint(StP, "W" + i, new Vector2(i, 0f), NavWaypointKind.Mark), Cfg));

            Assert.IsFalse(NavLocker.AddWaypoint(s,
                new NavWaypoint(StP, "ONE MORE", new Vector2(999f, 0f), NavWaypointKind.Mark), Cfg),
                "a waypoint is a deliberate act — at the cap the chart says no");
            Assert.AreEqual(Cfg.MaxWaypoints, NavLocker.WaypointCount(s));
            Assert.AreEqual("W0", s.NavWaypoints[0].Name, "and nothing the player already had was lost");
        }

        [Test]
        public void AnUnusableWaypoint_IsNeverStored()
        {
            SaveData s = FreshSave();
            Assert.IsFalse(NavLocker.AddWaypoint(s,
                new NavWaypoint(StP, "NAN", new Vector2(float.NaN, 1f), NavWaypointKind.Mark), Cfg));
            Assert.IsFalse(NavLocker.AddWaypoint(s,
                new NavWaypoint("", "NOWHERE", new Vector2(1f, 1f), NavWaypointKind.Mark), Cfg));
            Assert.AreEqual(0, NavLocker.WaypointCount(s), "garbage never reaches the save in the first place");
        }

        [Test]
        public void RemoveNear_TakesTheNearestInsideTheRadius_AndOnlyInThisRegion()
        {
            SaveData s = FreshSave();
            NavLocker.AddWaypoint(s, new NavWaypoint(StP, "NEAR", new Vector2(0f, 0f),
                                                     NavWaypointKind.Mark), Cfg);
            NavLocker.AddWaypoint(s, new NavWaypoint(StP, "FAR", new Vector2(100f, 0f),
                                                     NavWaypointKind.Mark), Cfg);
            NavLocker.AddWaypoint(s, new NavWaypoint(Coddle, "ELSEWHERE", new Vector2(1f, 0f),
                                                      NavWaypointKind.Mark), Cfg);

            Assert.IsFalse(NavLocker.RemoveWaypointNear(s, StP, new Vector2(50f, 0f), 10f),
                "nothing within the radius — a near-miss removes nothing");
            Assert.IsTrue(NavLocker.RemoveWaypointNear(s, StP, new Vector2(3f, 0f), 10f));
            Assert.AreEqual(2, NavLocker.WaypointCount(s));

            var into = new List<NavWaypoint>();
            NavLocker.WaypointsIn(s, StP, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("FAR", into[0].Name);
            NavLocker.WaypointsIn(s, Coddle, into);
            Assert.AreEqual(1, into.Count, "the other region's mark was never a candidate");
        }

        // ---- the route -------------------------------------------------------------------------------

        [Test]
        public void ARouteInAnotherRegion_IsReplaced_NotMixed()
        {
            SaveData s = FreshSave();
            NavLocker.AddRoutePoint(s, StP, new Vector2(0f, 0f), Cfg);
            NavLocker.AddRoutePoint(s, StP, new Vector2(50f, 0f), Cfg);
            NavLocker.AddRoutePoint(s, Coddle, new Vector2(5f, 5f), Cfg);

            var into = new List<Vector2>();
            NavLocker.RouteIn(s, StP, into);
            Assert.AreEqual(0, into.Count, "planning in a new region abandons the old passage");
            NavLocker.RouteIn(s, Coddle, into);
            Assert.AreEqual(1, into.Count, "and starts the new one cleanly");
        }

        [Test]
        public void TheRoute_StopsAtTheLegCap()
        {
            SaveData s = FreshSave();
            for (int i = 0; i <= Cfg.MaxRouteLegs; i++)          // N legs = N+1 points
                Assert.IsTrue(NavLocker.AddRoutePoint(s, StP, new Vector2(i * 10f, 0f), Cfg));
            Assert.IsFalse(NavLocker.AddRoutePoint(s, StP, new Vector2(9999f, 0f), Cfg));

            var into = new List<Vector2>();
            NavLocker.RouteIn(s, StP, into);
            Assert.AreEqual(Cfg.MaxRouteLegs + 1, into.Count);
        }

        // ---- the track -------------------------------------------------------------------------------

        [Test]
        public void TheTrack_TakesACrumbOnlyAfterTheBoatHasActuallyMoved()
        {
            SaveData s = FreshSave();
            float step = Cfg.TrackMinSpacingMetres;

            Assert.IsTrue(NavLocker.RecordTrack(s, StP, new Vector2(0f, 0f), Cfg), "the first crumb always lands");
            Assert.IsFalse(NavLocker.RecordTrack(s, StP, new Vector2(step * 0.5f, 0f), Cfg),
                "half a spacing is not a move — a boat at the wharf must not fill the ring");
            Assert.IsTrue(NavLocker.RecordTrack(s, StP, new Vector2(step * 1.1f, 0f), Cfg));
            Assert.AreEqual(2, s.NavTrack.Count);
        }

        [Test]
        public void TheTrack_IsARing_AndDropsItsOldestCrumb()
        {
            SaveData s = FreshSave();
            float step = Cfg.TrackMinSpacingMetres * 1.5f;
            for (int i = 0; i < Cfg.MaxTrackPoints + 10; i++)
                NavLocker.RecordTrack(s, StP, new Vector2(i * step, 0f), Cfg);

            Assert.AreEqual(Cfg.MaxTrackPoints, s.NavTrack.Count, "bounded absolutely (§3.1)");
            Assert.AreEqual(10f * step, s.NavTrack[0].X, 1e-3f,
                "the first ten crumbs are what a full ring gives up");
        }

        [Test]
        public void TheSpacingGate_IsMeasuredAgainstThisRegionsOwnLastCrumb()
        {
            // Sail in St Peters, travel to Coddle Cove, come home: the homecoming crumb must be gated
            // against where we LEFT St Peters, not against a position in another region's frame.
            SaveData s = FreshSave();
            NavLocker.RecordTrack(s, StP, new Vector2(0f, 0f), Cfg);
            NavLocker.RecordTrack(s, Coddle, new Vector2(500f, 500f), Cfg);

            Assert.IsFalse(NavLocker.RecordTrack(s, StP, new Vector2(1f, 0f), Cfg),
                "one metre from where we left St Peters is still not a move, however far away we went");
            Assert.IsTrue(NavLocker.RecordTrack(s, StP,
                new Vector2(Cfg.TrackMinSpacingMetres * 1.2f, 0f), Cfg));
        }

        // ---- the range ladder ------------------------------------------------------------------------

        [Test]
        public void TheRangeLadder_FitsTheWorldTheGameActuallyHas()
        {
            // The finding this slice turned up: navRig.js ships RANGE_STEPS=[1,2,4,6,10,16] NM against a
            // fictional 22x17 NM chart, but St Peters is WorldSizeMeters {760, 520} = 0.41 x 0.28 NM.
            // The shipped ladder must be able to FRAME that region and to get closer than it.
            ChartplotterSettings cfg = Cfg;
            const float regionWidthMetres = 760f;
            float regionNM = NavMath.MetresToNM(regionWidthMetres);

            float closest = cfg.RangeNMAt(0);
            Assert.Less(closest, regionNM * 0.5f,
                "the closest range must show detail well inside a region, not the whole thing at once");

            float widest = cfg.RangeNMAt(cfg.SafeRangeStepCount - 1);
            Assert.Greater(widest, regionNM,
                "and the widest must contain a whole region with sea around it");
            Assert.Less(widest, 4f,
                "but not be an ocean chart — the rig's own 16 NM would draw a region 40x smaller than " +
                "the glass and nothing else");

            // Each rung doubles, so the ladder is even.
            for (int i = 1; i < cfg.SafeRangeStepCount; i++)
                Assert.AreEqual(cfg.RangeNMAt(i - 1) * 2f, cfg.RangeNMAt(i), 1e-6f);
        }

        [Test]
        public void TheRangeLadder_HealsAConfigBlockThatNeverGotWritten()
        {
            // The trap that shipped three features inert on 2026-08-05: a struct member absent from the
            // wired GameConfig.asset block deserializes to ZERO, not to Default. A zero MinRangeNM would
            // make the chart's world-to-screen scale divide by zero and draw nothing at all.
            var zeroed = default(ChartplotterSettings);
            Assert.AreEqual(0f, zeroed.MinRangeNM, "this is what an unwritten block really looks like");

            Assert.Greater(zeroed.RangeNMAt(0), 0f, "the read heals it rather than returning the zero");
            Assert.AreEqual(ChartplotterSettings.Default.RangeNMAt(0), zeroed.RangeNMAt(0), 1e-9f);
            Assert.AreEqual(ChartplotterSettings.Default.RangeStepCount, zeroed.SafeRangeStepCount);
            Assert.AreEqual(0, zeroed.SafeDefaultRangeStep, "and the start rung stays inside the ladder");
        }

        [Test]
        public void RangeSteps_ClampRatherThanRunOffEitherEnd()
        {
            ChartplotterSettings cfg = Cfg;
            Assert.AreEqual(cfg.RangeNMAt(0), cfg.RangeNMAt(-5), 1e-9f);
            Assert.AreEqual(cfg.RangeNMAt(cfg.SafeRangeStepCount - 1), cfg.RangeNMAt(999), 1e-9f);
        }

        // ---- nav maths -------------------------------------------------------------------------------

        [Test]
        public void Bearings_AreTheProjectsOneCompassConvention_NotASecondOne()
        {
            // NavMath forwards to BoatKinematics rather than porting the rig's atan2(dx, -dy), which is
            // written for a canvas whose Y points DOWN. Porting it literally would have put a silently
            // flipped bearing in the codebase.
            Vector2 o = new Vector2(100f, 100f);
            Assert.AreEqual(0f, NavMath.BearingDegrees(o, o + Vector2.up), 1e-3f, "north");
            Assert.AreEqual(90f, NavMath.BearingDegrees(o, o + Vector2.right), 1e-3f, "east");
            Assert.AreEqual(180f, NavMath.BearingDegrees(o, o + Vector2.down), 1e-3f, "south");
            Assert.AreEqual(270f, NavMath.BearingDegrees(o, o + Vector2.left), 1e-3f, "west");

            foreach (Vector2 d in new[] { Vector2.up, Vector2.right, new Vector2(3f, -7f) })
                Assert.AreEqual(BoatKinematics.BearingDegrees(d), NavMath.BearingDegrees(o, o + d), 1e-6f,
                    "and it IS that function, not a copy that could drift from it");
        }

        [Test]
        public void DistancesAndRouteLengths_ConvertMetresToNauticalMiles()
        {
            Assert.AreEqual(1f, NavMath.MetresToNM(1852f), 1e-6f);
            Assert.AreEqual(1852f, NavMath.NMToMetres(1f), 1e-3f);

            var route = new List<Vector2> { new(0f, 0f), new(300f, 0f), new(300f, 400f) };
            Assert.AreEqual(700f, NavMath.RouteLengthMetres(route), 1e-3f);
            Assert.AreEqual(0f, NavMath.RouteLengthMetres(new List<Vector2> { new(1f, 1f) }), 1e-6f,
                "one point is not a passage");
            Assert.AreEqual(0f, NavMath.RouteLengthMetres(null), 1e-6f);
        }

        [Test]
        public void AStandstill_HasNoETA_RatherThanAnInventedOne()
        {
            Assert.IsTrue(float.IsPositiveInfinity(NavMath.MinutesToGo(1000f, 0f)));
            Assert.AreEqual(60f, NavMath.MinutesToGo(NavMath.NMToMetres(6f), 6f), 1e-3f);
        }

        // ---- the recorder's one promise --------------------------------------------------------------

        private sealed class FakeSave : ISaveService
        {
            public SaveData Current { get; set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        /// <summary>An instruments seam with a position and NO plotter fitted — the state the honesty
        /// invariant is about.</summary>
        /// <summary>Put a fake glass in the Core slot the way the real relay reaches it: REGISTER it
        /// against a hull, then declare that hull piloted. The slot is arbitrated by occupancy
        /// (<see cref="HelmSlot"/>) rather than assigned, so an instrument seam nobody is standing at
        /// reads empty — which is the correct answer, and no longer something a test can bypass. The
        /// fake stands in for its own hull; the arbiter only ever asks whether two tokens are the same
        /// object.</summary>
        private static void Pilot(IHelmInstruments glass)
        {
            GameServices.Helm.Register(glass, null, glass);
            GameServices.Helm.SetPilotedHull(glass);
        }

        private sealed class FakeInstruments : IHelmInstruments
        {
            public Vector2 Pos;
            public bool HasPos = true;
            public string HullId => "boat.novi";
            public HelmFit Fit => HelmFit.None;                       // nothing fitted at all
            public bool TryReadDepth(out float metres) { metres = 0f; return false; }
            public bool TryReadPosition(out Vector2 worldPos) { worldPos = Pos; return HasPos; }
            public SounderPrefs SounderPrefs => SounderPrefs.FromDefaults(DepthSounderSettings.Default);
            public void SetSounderPrefs(in SounderPrefs prefs) { }
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            NavTrackRecorder.Reset();
        }

        [Test]
        public void TheTrackAccrues_WithNoPlotterFitted_AndNoneImplemented()
        {
            // THE honesty invariant (§3.4): if the glass were deleted tomorrow the breadcrumb would still
            // be recorded, because "where I have been" is a fact about the voyage, not about equipment.
            var save = new FakeSave { Current = FreshSave() };
            var inst = new FakeInstruments { Pos = new Vector2(0f, 0f) };
            GameServices.Save = save;
            Pilot(inst);
            GameServices.CurrentRegionId = StP;
            NavTrackRecorder.Reset();

            Assert.AreEqual(SounderKind.None, inst.Fit.Sounder, "nothing is fitted…");
            Assert.IsFalse(inst.Fit.Gps, "…and emphatically no chartplotter");

            Assert.IsTrue(NavTrackRecorder.Tick(), "the first crumb still lands");
            inst.Pos = new Vector2(Cfg.TrackMinSpacingMetres * 1.5f, 0f);
            Assert.IsTrue(NavTrackRecorder.Tick());
            Assert.AreEqual(2, save.Current.NavTrack.Count);
        }

        [Test]
        public void TheRecorder_IsInertWithNothingWired_RatherThanThrowing()
        {
            NavTrackRecorder.Reset();
            Assert.DoesNotThrow(() => Assert.IsFalse(NavTrackRecorder.Tick()), "no save, no region, no boat");

            GameServices.Save = new FakeSave { Current = FreshSave() };
            Assert.IsFalse(NavTrackRecorder.Tick(), "a save but no region");

            GameServices.CurrentRegionId = StP;
            Assert.IsFalse(NavTrackRecorder.Tick(), "a region but no boat to read a position from");

            Pilot(new FakeInstruments { HasPos = false });
            Assert.IsFalse(NavTrackRecorder.Tick(), "a boat that cannot say where it is records nothing");
        }

        [Test]
        public void TheRecorder_WritesToMemoryOnly_NeverToDisk()
        {
            // A crumb every few metres is a save-file write every few seconds. The track reaches disk on
            // the game's normal cadence, like every other piece of accrued state.
            var save = new CountingSave { Current = FreshSave() };
            GameServices.Save = save;
            Pilot(new FakeInstruments { Pos = Vector2.zero });
            GameServices.CurrentRegionId = StP;
            NavTrackRecorder.Reset();

            NavTrackRecorder.Tick();
            Assert.AreEqual(1, save.Current.NavTrack.Count, "the crumb is in memory…");
            Assert.AreEqual(0, save.SaveCalls, "…and the disk was not touched for it");
        }

        private sealed class CountingSave : ISaveService
        {
            public int SaveCalls;
            public SaveData Current { get; set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => SaveCalls++;
        }

        // ---- the manager column's two writers (S6 PR 4) -----------------------------------------------

        /// <summary>Lay one region's marks with another region's in between, so every index assertion
        /// below is actually about the REGION's list rather than the save's flat one.</summary>
        private static SaveData SaveWithInterleavedRegions()
        {
            SaveData s = FreshSave();
            NavLocker.AddWaypoint(s, new NavWaypoint(StP, "A", new Vector2(1f, 1f),
                                                     NavWaypointKind.Mark), Cfg);
            NavLocker.AddWaypoint(s, new NavWaypoint(Coddle, "OTHER", new Vector2(9f, 9f),
                                                     NavWaypointKind.Hazard), Cfg);
            NavLocker.AddWaypoint(s, new NavWaypoint(StP, "B", new Vector2(2f, 2f),
                                                     NavWaypointKind.Anchor), Cfg);
            return s;
        }

        [Test]
        public void RenameWaypoint_WritesTheNameAtTheREGIONSIndex_AndMovesNothingElse()
        {
            SaveData s = SaveWithInterleavedRegions();

            Assert.IsTrue(NavLocker.RenameWaypoint(s, StP, 1, "WRECK"),
                "index 1 of ST PETERS is 'B' — the row between them belongs to another region and " +
                "must not be counted");

            var into = new List<NavWaypoint>();
            NavLocker.WaypointsIn(s, StP, into);
            Assert.AreEqual("A", into[0].Name);
            Assert.AreEqual("WRECK", into[1].Name);
            Assert.AreEqual(new Vector2(2f, 2f), into[1].Pos, "a rename moves no mark");
            Assert.AreEqual(NavWaypointKind.Anchor, into[1].Kind, "…and changes no symbol");

            NavLocker.WaypointsIn(s, Coddle, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("OTHER", into[0].Name, "the other region's row is untouched");
        }

        [Test]
        public void RenameWaypoint_ReportsNoChangeForTheSameName_SoACommitCostsNoIO()
        {
            SaveData s = SaveWithInterleavedRegions();
            Assert.IsTrue(NavLocker.RenameWaypoint(s, StP, 0, "HOME"));
            Assert.IsFalse(NavLocker.RenameWaypoint(s, StP, 0, "HOME"),
                "re-committing an unchanged name must report no change — the InstrumentLocker rule");
        }

        [Test]
        public void RenameWaypoint_RefusesAnIndexTheRegionDoesNotHave()
        {
            SaveData s = SaveWithInterleavedRegions();
            Assert.IsFalse(NavLocker.RenameWaypoint(s, StP, 2, "X"), "St Peters has two, not three");
            Assert.IsFalse(NavLocker.RenameWaypoint(s, StP, -1, "X"));
            Assert.IsFalse(NavLocker.RenameWaypoint(s, "region.nowhere", 0, "X"));
            Assert.IsFalse(NavLocker.RenameWaypoint(null, StP, 0, "X"), "a null save is 'no navigation'");

            var into = new List<NavWaypoint>();
            NavLocker.WaypointsIn(s, StP, into);
            Assert.AreEqual("A", into[0].Name, "…and a refusal writes nothing at all");
            Assert.AreEqual("B", into[1].Name);
        }

        [Test]
        public void RemoveWaypointAt_TakesTheROWTheColumnShowed_NotTheNearestOne()
        {
            SaveData s = SaveWithInterleavedRegions();
            Assert.IsTrue(NavLocker.RemoveWaypointAt(s, StP, 0));

            var into = new List<NavWaypoint>();
            NavLocker.WaypointsIn(s, StP, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("B", into[0].Name, "the row above it went, and only that one");

            NavLocker.WaypointsIn(s, Coddle, into);
            Assert.AreEqual(1, into.Count, "another region's mark is not collateral");
            Assert.AreEqual(2, NavLocker.WaypointCount(s), "…and the flat list lost exactly one row");
        }

        [Test]
        public void RemoveWaypointAt_RefusesAnIndexTheRegionDoesNotHave()
        {
            SaveData s = SaveWithInterleavedRegions();
            Assert.IsFalse(NavLocker.RemoveWaypointAt(s, StP, 2));
            Assert.IsFalse(NavLocker.RemoveWaypointAt(s, StP, -1));
            Assert.IsFalse(NavLocker.RemoveWaypointAt(null, StP, 0));
            Assert.AreEqual(3, NavLocker.WaypointCount(s), "nothing was removed by a refusal");
        }

        [Test]
        public void BothWritersShareOneIndexSpace_TheOneWaypointsInProduces()
        {
            // The manager renames a row and then deletes it. If the two writers walked the list
            // differently, the second call would take a DIFFERENT mark than the one just renamed —
            // a silent, data-losing off-by-one that no single-writer test can see.
            SaveData s = SaveWithInterleavedRegions();
            const int row = 1;
            Assert.IsTrue(NavLocker.RenameWaypoint(s, StP, row, "DOOMED"));
            Assert.IsTrue(NavLocker.RemoveWaypointAt(s, StP, row));

            var into = new List<NavWaypoint>();
            NavLocker.WaypointsIn(s, StP, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("A", into[0].Name, "the row that went is the row that was named");
        }

        // ---- NEGATIVE CONTROL ------------------------------------------------------------------------

        [Test]
        public void NegativeControl_TheSpacingGateAndTheCap_CanBothActuallyRefuse()
        {
            // Every assertion above is about a writer SUCCEEDING or REFUSING. Prove each probe can
            // register the opposite of what it asserts elsewhere, or they would be vacuous.
            SaveData s = FreshSave();
            Assert.IsTrue(NavLocker.RecordTrack(s, StP, Vector2.zero, Cfg), "the gate CAN pass…");
            Assert.IsFalse(NavLocker.RecordTrack(s, StP, Vector2.zero, Cfg), "…and CAN refuse");

            SaveData t = FreshSave();
            Assert.IsTrue(NavLocker.AddWaypoint(t, new NavWaypoint(StP, "A", Vector2.zero,
                                                                   NavWaypointKind.Mark), Cfg),
                "the cap CAN pass…");
            var tiny = Cfg;
            tiny.MaxWaypoints = 1;
            Assert.IsFalse(NavLocker.AddWaypoint(t, new NavWaypoint(StP, "B", Vector2.one,
                                                                    NavWaypointKind.Mark), tiny),
                "…and CAN refuse at a cap of one");
        }
    }
}
