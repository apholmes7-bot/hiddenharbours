using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// <b>The helm band's controls and repaint</b> (H2), without a canvas: the wheel's top half steers
    /// about the band's own hub exactly as the dash's wheel did; the lever drags through its window;
    /// the gear letter is the drive's sign; the anchor switch is live through the band's crop on every
    /// rig; a fitted housing raises its card and a second press lowers it; every piece repaints only
    /// on its own change and a steady band allocates nothing (rule 7); and the pilot bands' sounder
    /// housing shows the depth it is handed, with the shallow blink. The press and drag tests touch no
    /// texture and run anywhere; the paint tests upload, so they need the editor.
    /// </summary>
    public class HelmBandControllerTests
    {
        private static HelmOverlaySettings Cfg => HelmOverlaySettings.Default;

        private static readonly ConsoleRigKind[] Rigs =
        {
            ConsoleRigKind.Console, ConsoleRigKind.Sport, ConsoleRigKind.Novi, ConsoleRigKind.Cape,
        };

        private static readonly Vector2Int[] TwoScales = { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160) };

        private static object _sink;

        [SetUp]
        public void SetUp() => HelmInstrumentExpansion.Collapse();

        [TearDown]
        public void TearDown() => HelmInstrumentExpansion.Collapse();

        // ---- doubles -------------------------------------------------------------------------------

        /// <summary>A helm that records what the band asked of it, with the key layer's one move: a
        /// real key press ends the steer session.</summary>
        private sealed class BandHelm : IHelmControl
        {
            public int Toggles, DriveWrites, EndDrags, SteerWrites, EndSteers;
            public float LastSig = float.NaN, LastSteer = float.NaN;

            public bool HasHelm => true;
            public bool IsPlayerHelm => true;
            public HelmControlStyle Style => HelmControlStyle.Lever;
            public HelmLeverFinish LeverFinish => HelmLeverFinish.Graphite;
            public HelmWheelRim WheelRim => HelmWheelRim.Rubber;
            public HelmFit Fit { get; set; }
            public float Drive { get; set; }
            public float Steer { get; set; }

            public bool HasAnchor { get; set; } = true;
            public AnchorState AnchorState { get; set; } = AnchorState.Stowed;
            public void ToggleAnchor() => Toggles++;

            public void StepAhead() { }
            public void StepAstern() { }
            public void SetNeutral() { }
            public void DragDrive(float sig) { LastSig = sig; DriveWrites++; }
            public void EndDrag() => EndDrags++;
            public bool SteerDragActive { get; private set; }
            public void DragSteer(float steer) { LastSteer = steer; Steer = steer; SteerDragActive = true; SteerWrites++; }
            public void EndSteerDrag() { SteerDragActive = false; EndSteers++; }

            /// <summary>What the key layer does on a real steer key: the session ends, keys win.</summary>
            public void KeyPress() => SteerDragActive = false;
        }

        private static HelmFit Bare(ConsoleRigKind rig) => new HelmFit(rig, SounderKind.None, CompassMount.None, false, false);

        private static bool Paint(HelmBandController ctl, BandHelm helm, float heading, HelmBandGlance glance)
            => ctl.UpdateAndPaint(helm, helm.Fit, heading, 0.016f, in glance);

        /// <summary>A point in a piece's own px (y down) on screen, at the band's scale.</summary>
        private static Vector2 OnScreen(Rect piece, int k, Vector2 local)
            => new Vector2(piece.xMin + local.x * k, piece.yMax - local.y * k);

        // ---- the wheel -----------------------------------------------------------------------------

        [Test]
        public void TheWheelArc_SteersAsTheWheelDoes_AboutTheBandsHub()
        {
            double turns = GameServices.HelmWheel.Turns;
            foreach (Vector2Int sc in TwoScales)
            {
                foreach (ConsoleRigKind rig in Rigs)
                {
                    string at = $"{rig} at {sc.x}x{sc.y}";
                    var helm = new BandHelm { Fit = Bare(rig) };
                    var ctl = new HelmBandController();
                    ctl.Layout(rig, sc.x, sc.y);
                    int k = ctl.K;
                    Rect wheel = ctl.PieceRect(HelmBandGeometry.WheelIndex(rig));
                    Vector2 hub = OnScreen(wheel, k, new Vector2(HelmBandGeometry.HubX, HelmBandGeometry.HubY));
                    Assert.AreEqual(sc.x / 2f, hub.x, 1e-3f, at + ": the hub is centred");
                    Assert.AreEqual(0f, hub.y, 1e-3f, at + ": …on the band's bottom edge, so only the top half shows");

                    // A point at a canvas angle (y down, the rig's convention) and radius about the hub.
                    Vector2 At(double deg, double r)
                    {
                        double rad = deg * Math.PI / 180.0;
                        return hub + new Vector2((float)(Math.Cos(rad) * r * k), (float)(-Math.Sin(rad) * r * k));
                    }

                    Assert.IsTrue(ctl.PressControls(helm, At(-90.0, WheelRigGeometry.R), Cfg), at + ": the rim's top takes the press");
                    Assert.IsTrue(ctl.SteerSession && ctl.Interacting, at + ": a grab opens the steer session");
                    Assert.AreEqual(1, helm.SteerWrites, at + ": …once");
                    Assert.AreEqual(0f, helm.LastSteer, 1e-6f, at + ": …at the steer the hull already had: no jump");

                    ctl.Track(helm, At(-60.0, WheelRigGeometry.R), true, 0.016f);
                    Assert.AreEqual(WheelRigGeometry.SteerFromDeg(30.0, turns), helm.LastSteer, 1e-4,
                        at + ": 30 degrees clockwise along the rim is 30 degrees of wheel");
                    ctl.Track(helm, At(-120.0, 60.0), true, 0.016f);
                    Assert.AreEqual(WheelRigGeometry.SteerFromDeg(-30.0, turns), helm.LastSteer, 1e-4,
                        at + ": the angle is about the hub, whatever the radius: 60 back is -30 of wheel");

                    ctl.Track(helm, At(-120.0, 60.0), false, 0.016f);
                    Assert.IsFalse(ctl.Interacting, at + ": the release lets go of the rim");
                    Assert.IsTrue(ctl.SteerSession, at + ": …and the spin coasts under the live session");
                    Assert.AreEqual(0, helm.EndSteers, at + ": nothing ends it but a key or losing the helm");

                    ctl.Deactivate(helm);
                    Assert.AreEqual(1, helm.EndSteers, at + ": losing the helm ends the session");
                    Assert.IsFalse(ctl.SteerSession, at);

                    // The rest of the piece is panel: its top corners are no grab.
                    var wheelOnly = new BandHelm { Fit = Bare(rig) };
                    Assert.IsFalse(ctl.PressControls(wheelOnly, OnScreen(wheel, k, new Vector2(2f, 2f)), Cfg),
                        at + ": the piece's corner, outside the ring, is not the wheel");
                    Assert.AreEqual(0, wheelOnly.SteerWrites, at);
                }
            }
        }

        [Test]
        public void ARealKeyPress_EndsTheBandsSteerSession_KeysWin()
        {
            var helm = new BandHelm { Fit = Bare(ConsoleRigKind.Cape) };
            var ctl = new HelmBandController();
            try
            {
                ctl.Layout(ConsoleRigKind.Cape, 1920, 1080);
                Paint(ctl, helm, 0f, HelmBandGlance.None);
                Rect wheel = ctl.PieceRect(HelmBandGeometry.WheelIndex(ConsoleRigKind.Cape));
                var rimTop = new Vector2(HelmBandGeometry.HubX, HelmBandGeometry.HubY - WheelRigGeometry.R);
                Assert.IsTrue(ctl.PressControls(helm, OnScreen(wheel, 1, rimTop), Cfg));
                Assert.IsTrue(ctl.SteerSession);

                helm.KeyPress();
                helm.Steer = 0.25f;
                Paint(ctl, helm, 0f, HelmBandGlance.None);
                Assert.IsFalse(ctl.SteerSession, "the key layer broke the session: the band drops it the same frame");
                Assert.IsFalse(ctl.Interacting, "…grab and all");
                Assert.AreEqual(WheelRigGeometry.DegFromSteer(0.25, GameServices.HelmWheel.Turns), ctl.WheelDeg, 1e-6,
                    "…and the wheel mirrors the steer the keys set, never a second state");
            }
            finally
            {
                ctl.ReleaseTextures();
            }
        }

        // ---- the drive -----------------------------------------------------------------------------

        [Test]
        public void TheDrive_TracksTheLeverThroughItsWindow()
        {
            foreach (Vector2Int sc in TwoScales)
            {
                foreach (ConsoleRigKind rig in Rigs)
                {
                    string at = $"{rig} at {sc.x}x{sc.y}";
                    var helm = new BandHelm { Fit = Bare(rig) };
                    var ctl = new HelmBandController();
                    ctl.Layout(rig, sc.x, sc.y);
                    int k = ctl.K;
                    Rect drive = ctl.PieceRect(HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.Drive));

                    Vector2 GripAt(float d)
                    {
                        LeverRigGeometry.HandleOffset(d, out double gx, out double gy);
                        return OnScreen(drive, k, new Vector2(LeverRigGeometry.PX + (float)gx - HelmBandGeometry.DriveWindowX,
                                                              LeverRigGeometry.PY + (float)gy - HelmBandGeometry.DriveWindowY));
                    }

                    Assert.IsTrue(ctl.PressControls(helm, GripAt(0f), Cfg), at + ": the grip takes the press");
                    Assert.IsTrue(ctl.Interacting, at + ": …and holds a drag");
                    Assert.AreEqual(0f, helm.LastSig, 0.02f, at + ": …at the drive it shows");
                    foreach (float d in new[] { 0.5f, 1f, -0.5f, -1f })
                    {
                        ctl.Track(helm, GripAt(d), true, 0.016f);
                        Assert.AreEqual(d, helm.LastSig, 0.02f, at + $": dragged to drive {d}'s grip, the lever follows");
                    }
                    ctl.Track(helm, GripAt(-1f), false, 0.016f);
                    Assert.AreEqual(1, helm.EndDrags, at + ": the release ends the drag");
                    Assert.IsFalse(ctl.Interacting, at);

                    // Off the grip, inside the window: the travel guide's jump, with no drag left open.
                    var jump = new BandHelm { Fit = Bare(rig), Drive = 0f };
                    Assert.IsTrue(ctl.PressControls(jump, GripAt(-1f), Cfg), at + ": the window takes a press off the grip");
                    Assert.AreEqual(-1f, jump.LastSig, 0.02f, at + ": …the lever jumps there");
                    Assert.AreEqual(1, jump.EndDrags, at + ": …and holds");
                    Assert.IsFalse(ctl.Interacting, at);

                    // The gear letter's column is no control.
                    var gear = new BandHelm { Fit = Bare(rig) };
                    Assert.IsFalse(ctl.PressControls(gear, OnScreen(drive, k, new Vector2(HelmBandGeometry.GearX + 4f, HelmBandGeometry.H / 2f)), Cfg),
                        at + ": a press on the gear letter does nothing");
                    Assert.AreEqual(0, gear.DriveWrites, at);
                }
            }
        }

        [Test]
        public void TheGearLetter_IsForwardNeutralOrReverse_ByTheDrivesSign()
        {
            foreach (ConsoleRigKind rig in Rigs)
            {
                var helm = new BandHelm { Fit = Bare(rig) };
                var ctl = new HelmBandController();
                try
                {
                    ctl.Layout(rig, 1920, 1080);
                    Assert.IsNull(ctl.GearShown, $"{rig}: nothing is drawn before the first paint");
                    Paint(ctl, helm, 0f, HelmBandGlance.None);
                    Assert.AreEqual(HelmBandController.GearNeutral, ctl.GearShown, $"{rig}: neutral (the snap makes 0 exact)");
                    helm.Drive = 0.6f;
                    Paint(ctl, helm, 0f, HelmBandGlance.None);
                    Assert.AreEqual(HelmBandController.GearForward, ctl.GearShown, $"{rig}: ahead");
                    helm.Drive = -0.4f;
                    Paint(ctl, helm, 0f, HelmBandGlance.None);
                    Assert.AreEqual(HelmBandController.GearReverse, ctl.GearShown, $"{rig}: astern");

                    // The letter is the sign, not the lever's step: inside neutral's own step it still
                    // says which way, and only the drive piece repaints for it.
                    helm.Drive = 0f;
                    Paint(ctl, helm, 0f, HelmBandGlance.None);
                    Assert.AreEqual(LeverRigGeometry.QuantizeKey(0f), LeverRigGeometry.QuantizeKey(0.004f), "premise: the same lever step");
                    int before = ctl.UploadCount;
                    helm.Drive = 0.004f;
                    Assert.IsTrue(Paint(ctl, helm, 0f, HelmBandGlance.None), $"{rig}: the letter flips");
                    Assert.AreEqual(HelmBandController.GearForward, ctl.GearShown, $"{rig}: …to ahead");
                    Assert.AreEqual(1, ctl.UploadCount - before, $"{rig}: …and the drive piece alone repaints: the tach's step held");
                }
                finally
                {
                    ctl.ReleaseTextures();
                }
            }
        }

        // ---- the anchor switch ---------------------------------------------------------------------

        [Test]
        public void TheAnchorSwitch_IsLiveThroughTheBandsCrop_OnEveryRig()
        {
            foreach (ConsoleRigKind rig in Rigs)
            {
                Assert.IsTrue(HelmBandGeometry.TryAnchorHitBox(rig, out RectInt b), $"{rig}: a band switch");
                Assert.IsTrue(HelmDashGeometry.TryAnchorSwitchBox(rig, out int x, out int y, out int w, out int h), $"{rig}: a dash switch");
                int si = HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.Switches);
                HelmBandPiece sw = HelmBandGeometry.Piece(rig, si);
                // The crop maps the dash's own switch onto the band's: the same centre, through the cut.
                Assert.AreEqual(x + w / 2f, b.x + b.width / 2f + sw.CropX, 0.5f, $"{rig}: the same switch across");
                Assert.AreEqual(y + h / 2f, b.y + b.height / 2f + sw.CropY + HelmDashGeometry.Toppad(rig), 0.5f,
                    $"{rig}: …and down, the raster's top pad included");

                foreach (Vector2Int sc in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(3840, 2160) })
                {
                    string at = $"{rig} at {sc.x}x{sc.y}";
                    var helm = new BandHelm { Fit = Bare(rig) };
                    var ctl = new HelmBandController();
                    ctl.Layout(rig, sc.x, sc.y);
                    int k = ctl.K;
                    Rect piece = ctl.PieceRect(si);
                    Vector2 centre = OnScreen(piece, k, new Vector2(b.x + b.width / 2f, b.y + b.height / 2f));
                    Assert.IsTrue(ctl.PressControls(helm, centre, Cfg), at + ": the switch takes the press");
                    Assert.AreEqual(1, helm.Toggles, at + ": one flick, one toggle");
                    Assert.AreEqual(0, helm.DriveWrites, at + ": not the lever");
                    Assert.AreEqual(0, helm.SteerWrites, at + ": nor the wheel");
                    Assert.IsFalse(ctl.Interacting, at + ": a flick leaves no drag behind");

                    ctl.PressControls(helm, OnScreen(piece, k, new Vector2(b.xMax + 1f, b.y + b.height / 2f)), Cfg);
                    Assert.AreEqual(1, helm.Toggles, at + ": beside the switch is not the switch");

                    helm.HasAnchor = false;
                    Assert.IsFalse(ctl.PressControls(helm, centre, Cfg), at + ": no hook, no switch to flick");
                    Assert.AreEqual(1, helm.Toggles, at);
                }
            }
        }

        // ---- the housings --------------------------------------------------------------------------

        [Test]
        public void APlatePress_RaisesItsInstrument_AndASecondPressLowersIt()
        {
            foreach (Vector2Int sc in TwoScales)
            {
                var fit = new HelmFit(ConsoleRigKind.Cape, SounderKind.Depth, CompassMount.None, true, true);
                var ctl = new HelmBandController();
                ctl.Layout(ConsoleRigKind.Cape, sc.x, sc.y);
                Vector2 Plate(HelmBandPieceKind kind) => ctl.PieceRect(HelmBandGeometry.IndexOf(ConsoleRigKind.Cape, kind)).center;

                // The host's routing for a press on the band: a fitted housing clicks its slot.
                void Press(in HelmFit f, Vector2 at)
                {
                    HelmInstrumentSlot slot = ctl.PlateAt(in f, at);
                    if (slot != HelmInstrumentSlot.None) HelmInstrumentExpansion.Click(slot);
                }

                Press(in fit, Plate(HelmBandPieceKind.SounderPlate));
                Assert.AreEqual(HelmInstrumentSlot.Sounder, HelmInstrumentExpansion.Expanded, $"{sc}: the sounder's housing raises it");
                Press(in fit, Plate(HelmBandPieceKind.SounderPlate));
                Assert.AreEqual(HelmInstrumentSlot.None, HelmInstrumentExpansion.Expanded, $"{sc}: a second press lowers it");

                Press(in fit, Plate(HelmBandPieceKind.RadarPlate));
                Assert.AreEqual(HelmInstrumentSlot.Radar, HelmInstrumentExpansion.Expanded, $"{sc}: the radar's");
                Press(in fit, Plate(HelmBandPieceKind.GpsPlate));
                Assert.AreEqual(HelmInstrumentSlot.Chartplotter, HelmInstrumentExpansion.Expanded,
                    $"{sc}: one card at a time: the plotter takes over from the radar");
                Press(in fit, Plate(HelmBandPieceKind.GpsPlate));
                Assert.AreEqual(HelmInstrumentSlot.None, HelmInstrumentExpansion.Expanded, $"{sc}: and lowers again");

                var fish = new HelmFit(ConsoleRigKind.Cape, SounderKind.Fish, CompassMount.None, false, false);
                Assert.AreEqual(HelmInstrumentSlot.FishFinder, ctl.PlateAt(in fish, Plate(HelmBandPieceKind.SounderPlate)),
                    $"{sc}: the finder rises from the same housing");
                Assert.AreEqual(HelmInstrumentSlot.None, ctl.PlateAt(in fish, Plate(HelmBandPieceKind.RadarPlate)),
                    $"{sc}: an empty housing raises nothing");
                for (int i = 0; i < ctl.PieceCount; i++)
                    if (!ctl.Piece(i).IsPlate)
                        Assert.AreEqual(HelmInstrumentSlot.None, ctl.PlateAt(in fit, ctl.PieceRect(i).center),
                            $"{sc}, piece {i} ({ctl.Piece(i).Kind}): only a housing raises a card");
            }

            var skiff = new HelmBandController();
            skiff.Layout(ConsoleRigKind.Console, 1280, 720);
            var depth = new HelmFit(ConsoleRigKind.Console, SounderKind.Depth, CompassMount.Dome, false, false);
            Assert.AreEqual(HelmInstrumentSlot.Sounder,
                skiff.PlateAt(in depth, skiff.PieceRect(HelmBandGeometry.IndexOf(ConsoleRigKind.Console, HelmBandPieceKind.SounderPlate)).center),
                "a skiff's sounder housing raises the sounder too");
        }

        // ---- repaint (rule 7) ----------------------------------------------------------------------

        [Test]
        public void TheBand_RepaintsOnlyOnAChange_AndAllocatesNothingInTheSteadyState()
        {
            var helm = new BandHelm { Fit = new HelmFit(ConsoleRigKind.Cape, SounderKind.Depth, CompassMount.Flush, true, true) };
            var ctl = new HelmBandController();
            try
            {
                ctl.Layout(ConsoleRigKind.Cape, 1920, 1080);
                var glance = new HelmBandGlance(true, 12f, false, false);
                Assert.IsTrue(Paint(ctl, helm, 0f, glance), "the first paint draws");
                int pieces = 0;
                for (int i = 0; i < ctl.PieceCount; i++)
                {
                    bool cap = ctl.Piece(i).Kind == HelmBandPieceKind.Cap;
                    if (!cap) pieces++;
                    Assert.AreEqual(cap, ctl.TextureOf(i) == null, $"piece {i}: textured, unless it is a tinted cap");
                }
                Assert.AreEqual(pieces, ctl.UploadCount, "…every piece once, the caps never");
                Assert.IsFalse(Paint(ctl, helm, 0f, glance), "a steady band uploads nothing");

                int before = ctl.UploadCount;
                Assert.IsFalse(Paint(ctl, helm, 0.4f, glance), "the compass is keyed on the whole degree");
                Assert.IsTrue(Paint(ctl, helm, 1f, glance));
                Assert.AreEqual(1, ctl.UploadCount - before, "a degree of heading is the compass alone");

                double turns = GameServices.HelmWheel.Turns;
                before = ctl.UploadCount;
                helm.Steer = (float)(0.2 / WheelRigGeometry.LockDeg(turns));
                Assert.IsFalse(Paint(ctl, helm, 1f, glance), "the wheel is keyed on the half degree");
                helm.Steer = (float)(10.0 / WheelRigGeometry.LockDeg(turns));
                Assert.IsTrue(Paint(ctl, helm, 1f, glance));
                Assert.AreEqual(1, ctl.UploadCount - before, "ten degrees of wheel is the wheel alone");

                before = ctl.UploadCount;
                helm.Drive = 0.5f;
                Assert.IsTrue(Paint(ctl, helm, 1f, glance));
                Assert.AreEqual(2, ctl.UploadCount - before, "a lever step is the tach and the drive: one re-cut, one lever");

                before = ctl.UploadCount;
                glance = new HelmBandGlance(true, 20f, false, false);
                Assert.IsTrue(Paint(ctl, helm, 1f, glance));
                Assert.AreEqual(1, ctl.UploadCount - before, "a new depth is the sounder's housing alone");
                glance = new HelmBandGlance(true, 20.01f, false, false);
                Assert.IsFalse(Paint(ctl, helm, 1f, glance), "a reading that reads the same digits costs nothing");

                // The steady state, laid out every frame as the host does: no allocation.
                Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                    "harness: the GC.Alloc recorder saw no allocation at all");
                HelmBandGlance steady = glance;
                Assert.That(() =>
                {
                    for (int f = 0; f < 120; f++)
                    {
                        ctl.Layout(ConsoleRigKind.Cape, 1920, 1080);
                        ctl.UpdateAndPaint(helm, helm.Fit, 1f, 0.016f, in steady);
                    }
                }, Is.Not.AllocatingGCMemory(), "a steady band allocated (rule 7: nothing per frame)");
                Assert.IsFalse(Paint(ctl, helm, 1f, steady), "the loop measured a band at rest, not one still drawing");
            }
            finally
            {
                ctl.ReleaseTextures();
            }
        }

        // ---- the pilot bands' depth ----------------------------------------------------------------

        [Test]
        public void ThePilotBandsDepth_IsTheSoundersReading_WithItsShallowBlink()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                var helm = new BandHelm { Fit = new HelmFit(rig, SounderKind.Depth, CompassMount.None, false, false) };
                var ctl = new HelmBandController();
                try
                {
                    ctl.Layout(rig, 1920, 1080);
                    int si = HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.SounderPlate);
                    Paint(ctl, helm, 0f, HelmBandGlance.None);
                    Assert.AreEqual(HelmBandController.SounderLabel, ctl.PlateLabelShown(si), $"{rig}: the housing names it");
                    Assert.IsNull(ctl.PlateDigitsShown(si), $"{rig}: no reading yet, the name alone");

                    Paint(ctl, helm, 0f, new HelmBandGlance(true, 7.25f, false, false));
                    Assert.AreEqual(DepthRigGeometry.FmtDepth(7.25, false), ctl.PlateDigitsShown(si),
                        $"{rig}: the depth in the sounder's own format");
                    Assert.IsFalse(ctl.PlateAlarmShown(si), $"{rig}: no alarm");

                    int before = ctl.UploadCount;
                    Paint(ctl, helm, 0f, new HelmBandGlance(true, 7.25f, false, true));
                    Assert.IsTrue(ctl.PlateAlarmShown(si), $"{rig}: the shallow blink's lit phase is drawn");
                    Assert.AreEqual(1, ctl.UploadCount - before, $"{rig}: …by the housing alone");
                    before = ctl.UploadCount;
                    Paint(ctl, helm, 0f, new HelmBandGlance(true, 7.25f, false, false));
                    Assert.IsFalse(ctl.PlateAlarmShown(si), $"{rig}: …and its dark phase");
                    Assert.AreEqual(1, ctl.UploadCount - before, $"{rig}: …again the housing alone");
                    Assert.IsFalse(Paint(ctl, helm, 0f, new HelmBandGlance(true, 7.25f, false, false)), $"{rig}: then nothing");

                    Paint(ctl, helm, 0f, new HelmBandGlance(true, 7.25f, true, false));
                    Assert.AreEqual(DepthRigGeometry.FmtDepth(7.25, true), ctl.PlateDigitsShown(si), $"{rig}: in the hull's units");

                    helm.Fit = new HelmFit(rig, SounderKind.Fish, CompassMount.None, false, false);
                    Paint(ctl, helm, 0f, new HelmBandGlance(true, 7.25f, false, false));
                    Assert.AreEqual(HelmBandController.FinderLabel, ctl.PlateLabelShown(si), $"{rig}: the finder's housing");
                    Assert.AreEqual(DepthRigGeometry.FmtDepth(7.25, false), ctl.PlateDigitsShown(si), $"{rig}: …carries the depth too");
                }
                finally
                {
                    ctl.ReleaseTextures();
                }
            }

            // The skiffs' housing is a name only, whatever it is handed.
            var skiffHelm = new BandHelm { Fit = new HelmFit(ConsoleRigKind.Console, SounderKind.Depth, CompassMount.Dome, false, false) };
            var skiff = new HelmBandController();
            try
            {
                skiff.Layout(ConsoleRigKind.Console, 1920, 1080);
                Paint(skiff, skiffHelm, 0f, new HelmBandGlance(true, 7.25f, false, true));
                int si = HelmBandGeometry.IndexOf(ConsoleRigKind.Console, HelmBandPieceKind.SounderPlate);
                Assert.AreEqual(HelmBandController.SounderLabel, skiff.PlateLabelShown(si));
                Assert.IsNull(skiff.PlateDigitsShown(si), "a skiff's housing carries no depth (the brief)");
            }
            finally
            {
                skiff.ReleaseTextures();
            }
        }
    }
}
