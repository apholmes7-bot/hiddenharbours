using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Helm instruments plan, slice 2: the dash's fuel gauge reads the hull's real tank. The chain is
    /// proven link by link with a real <see cref="BoatFuelTank"/> at its head: the tank's fraction
    /// (<see cref="HelmControlRelay.FuelOf"/>), across the Core instrument seam
    /// (<see cref="IHelmInstruments.TryReadFuel"/>), into the band's glance
    /// (<see cref="HelmOverlayHost.WithFuel"/>), onto the needle (<see cref="HelmDashGeometry.FuelNeedle01"/>),
    /// and through the band's repaint key.
    ///
    /// <para>Top-level rather than in the UI folder because the head of the chain is the Boats module's
    /// tank. What it cannot reach in EditMode is the relay's own guard (a driven, enabled hull); that
    /// line is two null checks in front of <see cref="HelmControlRelay.FuelOf"/>.</para>
    /// </summary>
    public class HelmFuelGaugeReadsTheTankTests
    {
        private const float Tol = 1e-4f;
        private const float TankLitres = 200f;   // a test tank, not tuning
        private const int ScreenW = 1920, ScreenH = 1080;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private BoatFuelTank Tank(float capacityLitres, float fraction01)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.fuel_gauge_fixture";
            hull.FuelCapacityLitres = capacityLitres;
            hull.FuelGrade = capacityLitres > 0f ? FuelGrades.Diesel : "";
            _spawned.Add(hull);

            var go = new GameObject("Fuel gauge fixture");
            _spawned.Add(go);
            var tank = go.AddComponent<BoatFuelTank>();
            tank.Configure(hull);
            tank.SetLitres(capacityLitres * fraction01);
            return tank;
        }

        /// <summary>The Core seam with only the fuel read behind it, answered by a real tank exactly as
        /// the relay answers it.</summary>
        private sealed class TankInstruments : IHelmInstruments
        {
            private readonly BoatFuelTank _tank;
            public TankInstruments(BoatFuelTank tank) { _tank = tank; }

            public string HullId => "boat.fuel_gauge_fixture";
            public HelmFit Fit => HelmFit.None;
            public bool TryReadDepth(out float metres) { metres = 0f; return false; }
            public bool TryReadPosition(out Vector2 worldPos) { worldPos = Vector2.zero; return false; }
            public bool TryReadFuel(out float fraction01) => HelmControlRelay.FuelOf(_tank, out fraction01);
            public SounderPrefs SounderPrefs => default;
            public void SetSounderPrefs(in SounderPrefs prefs) { }
        }

        /// <summary>An instrument seam written before the fuel read existed: it answers through the
        /// interface's own default.</summary>
        private sealed class OlderInstruments : IHelmInstruments
        {
            public string HullId => "";
            public HelmFit Fit => HelmFit.None;
            public bool TryReadDepth(out float metres) { metres = 0f; return false; }
            public bool TryReadPosition(out Vector2 worldPos) { worldPos = Vector2.zero; return false; }
            public SounderPrefs SounderPrefs => default;
            public void SetSounderPrefs(in SounderPrefs prefs) { }
        }

        private sealed class SteadyHelm : IHelmControl
        {
            public bool HasHelm => true;
            public bool IsPlayerHelm => true;
            public HelmControlStyle Style => HelmControlStyle.Lever;
            public HelmLeverFinish LeverFinish => HelmLeverFinish.Graphite;
            public HelmWheelRim WheelRim => HelmWheelRim.Rubber;
            public HelmFit Fit { get; set; }
            public float Drive => 0f;
            public float Steer => 0f;
            public bool HasAnchor => false;
            public AnchorState AnchorState => AnchorState.Stowed;
            public void ToggleAnchor() { }
            public void StepAhead() { }
            public void StepAstern() { }
            public void SetNeutral() { }
            public void DragDrive(float sig) { }
            public void EndDrag() { }
            public bool SteerDragActive => false;
            public void DragSteer(float steer) { }
            public void EndSteerDrag() { }
        }

        private float NeedleFor(float tankFraction01)
        {
            var seam = new TankInstruments(Tank(TankLitres, tankFraction01));
            HelmBandGlance glance = HelmOverlayHost.WithFuel(HelmBandGlance.None, seam);
            Assert.IsTrue(glance.HasFuel, "a hull with a tank has a reading");
            return HelmDashGeometry.FuelNeedle01(glance.HasFuel, glance.Fuel01);
        }

        // ---- the needle follows the tank ---------------------------------------------------------------

        [TestCase(1f)]
        [TestCase(0.5f)]
        [TestCase(0f)]
        public void TheNeedle_ShowsTheTanksFraction_FullHalfAndEmpty(float tankFraction01)
        {
            Assert.That(NeedleFor(tankFraction01), Is.EqualTo(tankFraction01).Within(Tol));
        }

        [Test]
        public void TheNeedle_IsNoLongerPinnedAtFull_TheBugThisFixes()
        {
            // The negative control: before this change every dash drew 1 whatever the tank held.
            Assert.That(NeedleFor(0.25f), Is.LessThan(NeedleFor(1f)));
            Assert.That(HelmDashGeometry.FuelPhiDeg(NeedleFor(0f)),
                        Is.EqualTo(-HelmDashGeometry.FuelSweepDeg / 2.0).Within(Tol),
                        "an empty tank lays the needle on E, the bottom of its sweep");
            Assert.That(HelmDashGeometry.FuelPhiDeg(NeedleFor(1f)),
                        Is.EqualTo(HelmDashGeometry.FuelSweepDeg / 2.0).Within(Tol),
                        "a full tank lays it on F");
        }

        [Test]
        public void TheNeedle_FollowsFuelDrawnOutAndPouredIn()
        {
            BoatFuelTank tank = Tank(TankLitres, 1f);
            var seam = new TankInstruments(tank);

            tank.Draw(TankLitres / 2f);
            Assert.IsTrue(seam.TryReadFuel(out float half));
            Assert.That(half, Is.EqualTo(0.5f).Within(Tol), "half the tank gone, half on the gauge");

            tank.Deliver(TankLitres / 4f);
            Assert.IsTrue(seam.TryReadFuel(out float threeQuarters));
            Assert.That(threeQuarters, Is.EqualTo(0.75f).Within(Tol), "…and it comes back up at the pump");
        }

        // ---- no sender is not an empty tank ------------------------------------------------------------

        [Test]
        public void AHullWithNoTank_HasNoReading_AndTheGaugeShowsItsUnreadFace()
        {
            Assert.IsFalse(HelmControlRelay.FuelOf(Tank(0f, 0f), out float none), "no tank, no sender");
            Assert.AreEqual(0f, none);
            Assert.IsFalse(HelmControlRelay.FuelOf(null, out _), "no tank component at all");

            HelmBandGlance glance = HelmOverlayHost.WithFuel(HelmBandGlance.None, new TankInstruments(Tank(0f, 0f)));
            Assert.IsFalse(glance.HasFuel);
            Assert.That(HelmDashGeometry.FuelNeedle01(glance.HasFuel, glance.Fuel01),
                        Is.EqualTo(HelmDashGeometry.FuelUnread01));
        }

        [Test]
        public void NoSeam_OrASeamThatPredatesTheRead_IsUnread_NotEmpty()
        {
            Assert.IsFalse(HelmOverlayHost.WithFuel(HelmBandGlance.None, null).HasFuel, "on foot: no seam");
            Assert.IsFalse(HelmOverlayHost.WithFuel(HelmBandGlance.None, new OlderInstruments()).HasFuel,
                           "the interface's default answers 'no sender'");
        }

        [Test]
        public void LayingTheFuelOver_KeepsTheDepthTheGlanceAlreadyCarried()
        {
            var depth = new HelmBandGlance(true, 7.5f, true, true);
            HelmBandGlance both = HelmOverlayHost.WithFuel(in depth, new TankInstruments(Tank(TankLitres, 0.5f)));

            Assert.IsTrue(both.HasDepth);
            Assert.AreEqual(7.5f, both.DepthMetres);
            Assert.IsTrue(both.Feet);
            Assert.IsTrue(both.AlarmLit);
            Assert.That(both.Fuel01, Is.EqualTo(0.5f).Within(Tol));
        }

        // ---- the band repaints the fuel piece on a step, and only then (rule 7) -------------------------

        [Test]
        public void TheBand_RecutsTheFuelPieceAlone_WhenTheTankCrossesAStep_AndThenCostsNothing(
            [Values(ConsoleRigKind.Console, ConsoleRigKind.Sport, ConsoleRigKind.Novi, ConsoleRigKind.Cape)]
            ConsoleRigKind rig)
        {
            var helm = new SteadyHelm
            {
                Fit = new HelmFit(rig, SounderKind.None, CompassMount.None, false, false),
            };
            var ctl = new HelmBandController();
            try
            {
                ctl.Layout(rig, ScreenW, ScreenH);
                int fuelPieces = 0, fuelIndex = -1;
                for (int i = 0; i < ctl.PieceCount; i++)
                    if (HelmBandGeometry.Piece(rig, i).Kind == HelmBandPieceKind.Fuel) { fuelPieces++; fuelIndex = i; }
                Assert.That(fuelPieces, Is.EqualTo(1), $"premise: the {rig} band carries one fuel gauge");

                HelmBandGlance full = HelmBandGlance.None.WithFuel(true, 1f);
                Assert.IsTrue(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, in full), "the first paint always draws");
                Assert.IsFalse(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, in full),
                               "a steady tank repaints nothing");
                Color32[] atFull = (Color32[])ctl.SurfaceOf(fuelIndex).Pixels.Clone();

                HelmBandGlance half = HelmBandGlance.None.WithFuel(true, 0.5f);
                int before = ctl.UploadCount;
                Assert.IsTrue(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, in half), "the tank moved a step");
                Assert.That(ctl.UploadCount - before, Is.EqualTo(1),
                            "the band re-cuts its fuel piece alone, not the tach or the switches beside it");
                Assert.IsFalse(SamePixels(atFull, ctl.SurfaceOf(fuelIndex).Pixels),
                               "and the gauge that is drawn has moved off full");

                float withinTheStep = 0.5f + 0.25f / HelmDashGeometry.FuelKeySteps;
                HelmBandGlance nudge = HelmBandGlance.None.WithFuel(true, withinTheStep);
                Assert.IsFalse(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, in nudge),
                               "fuel burned inside one needle step costs no raster and no upload");

                HelmBandGlance empty = HelmBandGlance.None.WithFuel(true, 0f);
                Assert.IsTrue(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, in empty), "running dry is a step too");
            }
            finally
            {
                ctl.ReleaseTextures();
            }
        }

        [Test]
        public void TheFuelKey_SnapsToItsSteps_AndRoundTrips()
        {
            Assert.That(HelmDashGeometry.FuelKey(0f), Is.EqualTo(0));
            Assert.That(HelmDashGeometry.FuelKey(1f), Is.EqualTo(HelmDashGeometry.FuelKeySteps));
            Assert.That(HelmDashGeometry.FuelKey(7f), Is.EqualTo(HelmDashGeometry.FuelKeySteps), "clamped above");
            Assert.That(HelmDashGeometry.FuelKey(-1f), Is.EqualTo(0), "clamped below");
            Assert.That(HelmDashGeometry.FuelFromKey(HelmDashGeometry.FuelKeySteps), Is.EqualTo(1f),
                        "a full tank draws exactly full");
            for (int key = 0; key <= HelmDashGeometry.FuelKeySteps; key++)
                Assert.That(HelmDashGeometry.FuelKey(HelmDashGeometry.FuelFromKey(key)), Is.EqualTo(key));
        }

        private static bool SamePixels(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a) return false;
            return true;
        }
    }
}
