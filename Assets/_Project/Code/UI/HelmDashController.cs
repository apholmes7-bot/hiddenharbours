using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The composed skiff dash's LAYER COMPOSITOR + per-instrument interaction (ADR 0025 S2a),
    /// owned by <see cref="HelmOverlayHost"/> — a plain class so the repaint keys and the wheel
    /// session are testable without a canvas. Composition follows the helm rig's own contract
    /// (console-helm/README.md): the dash CHROME is the background
    /// (<see cref="ConsoleDashRender"/>/<see cref="SportDashRender"/>), the dome compass paints at
    /// the crown box, the grabbable wheel (<see cref="WheelRigRender"/>) mounts hub-on-point at the
    /// dash's wheel centre, and the LEVER composites last, on top. The brow sounder cutout stays
    /// EMPTY until S2's DepthRig port mounts there (never a fake instrument).
    ///
    /// <para><b>Repaint discipline (rule 7):</b> one surface per moving instrument, each behind its
    /// own quantised change key — drive at the lever's 1/48 (which also gates rpm + gear), the wheel
    /// at the rig's own half-degree, the compass at whole degrees — and ONE 600×510 card composite +
    /// texture upload only when some layer actually changed. Zero steady-state allocation.</para>
    ///
    /// <para><b>One steer owner:</b> the wheel MIRRORS <see cref="IHelmControl.Steer"/> whenever no
    /// steer session is live; a focused rim grab opens the session (<see cref="IHelmControl.DragSteer"/>)
    /// and the {deg, vel} spin state here is only the input DEVICE's transient state. The key layer
    /// breaking the session (<see cref="IHelmControl.SteerDragActive"/> going false) drops the grab
    /// the same frame — the wheel snaps back to mirroring.</para>
    /// </summary>
    public sealed class HelmDashController
    {
        // ---- layer surfaces (allocated once) ------------------------------------------------------
        private DrawSurface _chrome, _card, _wheelCell, _leverCell, _compassCell;

        // ---- shown keys ---------------------------------------------------------------------------
        private ConsoleRigKind _shownRig = ConsoleRigKind.None;
        private bool _shownRunning;
        private int _shownDriveKey = int.MinValue;          // gates lever + rpm needle + gear lamps
        private HelmLeverFinish _shownFinish = (HelmLeverFinish)(-1);
        private int _shownWheelKey = int.MinValue;
        private HelmWheelRim _shownRim = (HelmWheelRim)(-1);
        private int _shownHeadingKey = int.MinValue;
        private CompassMount _shownCompass = (CompassMount)(-1);
        private int _shownBrowKey = int.MinValue;           // which brow mounts the chrome drew

        // The rig the surfaces are sized for, and the one interaction hit-tests against. The two
        // helm families use DIFFERENT canvases (600×510 skiff, 600×548 pilothouse) and different
        // control positions, so both must follow the live fit.
        private ConsoleRigKind _rig = ConsoleRigKind.None;

        // ---- wheel device state (transient input state — never saved, rule 5) ----------------------
        private WheelRigGeometry.SpinState _spin;
        private bool _steerSession;
        private bool _wheelGrabbed;
        private double _lastPointerAngle;
        private bool _leverDragging;

        /// <summary>The wheel angle currently rendered (test seam).</summary>
        public double WheelDeg => _spin.Deg;

        /// <summary>True while this controller holds the steer session (test seam).</summary>
        public bool SteerSession => _steerSession;

        /// <summary>Forget everything shown (hull swap / style change) so the next paint is full.</summary>
        public void Invalidate()
        {
            _shownRig = ConsoleRigKind.None;
            _shownDriveKey = int.MinValue;
            _shownWheelKey = int.MinValue;
            _shownHeadingKey = int.MinValue;
            _shownCompass = (CompassMount)(-1);
            _shownBrowKey = int.MinValue;
        }

        /// <summary>End every live interaction (unfocus / Esc / helm lost).</summary>
        public void Deactivate(IHelmControl helm)
        {
            if (_leverDragging && helm != null) helm.EndDrag();
            _leverDragging = false;
            if (_steerSession && helm != null) helm.EndSteerDrag();
            _steerSession = false;
            _wheelGrabbed = false;
        }

        // ---- painting ------------------------------------------------------------------------------

        /// <summary>
        /// Advance the wheel device (mirror or live session), repaint any changed layer, and
        /// recompose the card. Returns true when the texture needs a re-upload.
        /// </summary>
        public bool UpdateAndPaint(IHelmControl helm, HelmFit fit, float headingDeg, float dt,
                                   bool focused, ref Texture2D texture, RawImage image)
        {
            _rig = fit.Rig;
            EnsureSurfaces(fit.Rig);
            HelmWheelSettings wheelCfg = GameServices.HelmWheel;

            // The key layer may have broken the steer session under us (a real key press wins).
            if (_steerSession && !helm.SteerDragActive)
            {
                _steerSession = false;
                _wheelGrabbed = false;
            }

            // Wheel device: coast under a live session (the harness loop, dt clamped), else mirror.
            if (_steerSession && !_wheelGrabbed)
            {
                _spin = WheelRigGeometry.Step(_spin, System.Math.Min(WheelRigGeometry.StepDtMax, dt),
                                              wheelCfg.Turns, wheelCfg.Friction, wheelCfg.SelfCentre, out _);
                helm.DragSteer((float)WheelRigGeometry.SteerFromDeg(_spin.Deg, wheelCfg.Turns));
            }
            else if (!_steerSession)
            {
                _spin.Deg = WheelRigGeometry.DegFromSteer(helm.Steer, wheelCfg.Turns);
                _spin.Vel = 0.0;
            }
            if (!focused && _steerSession)
            {
                // Focus left while coasting — the session ends with the focus (keys resume).
                Deactivate(helm);
            }

            // ---- change keys ----
            float drive = helm.Drive;
            bool running = true;                        // manned helm = running (S1 precedent)
            const float fuel01 = 1f;                    // no fuel sim yet — needle pins at F (PR note)
            float rpm01 = running ? Mathf.Clamp01(0.11f + 0.89f * Mathf.Abs(drive)) : 0f;   // js:337
            int driveKey = LeverRigGeometry.QuantizeKey(drive);
            int wheelKey = WheelRigGeometry.QuantizeKey(_spin.Deg);
            int headingKey = DrawSurface.JsRound(CompassRigRender.Norm(headingDeg));   // whole degrees
            HelmLeverFinish finish = helm.LeverFinish;
            HelmWheelRim rim = helm.WheelRim;

            // The pilothouse brow's mounts follow the FIT (which slots are drawn, which are blanked,
            // and whether the sounder mount is the tall portrait box) — so the chrome tracks it too.
            int browKey = (((int)fit.Sounder * 3 + (int)fit.Compass) << 2)
                        | (fit.Radar ? 2 : 0) | (fit.Gps ? 1 : 0);

            bool chromeDirty = fit.Rig != _shownRig || running != _shownRunning || driveKey != _shownDriveKey
                            || browKey != _shownBrowKey;
            bool leverDirty = chromeDirty || finish != _shownFinish;
            bool wheelDirty = fit.Rig != _shownRig || wheelKey != _shownWheelKey || rim != _shownRim;
            bool compassDirty = fit.Rig != _shownRig || headingKey != _shownHeadingKey
                             || fit.Compass != _shownCompass;
            if (!chromeDirty && !leverDirty && !wheelDirty && !compassDirty) return false;

            if (chromeDirty)
            {
                switch (fit.Rig)
                {
                    case ConsoleRigKind.Sport: SportDashRender.Render(_chrome, running, drive, rpm01, fuel01); break;
                    case ConsoleRigKind.Novi: NoviDashRender.Render(_chrome, in fit, running, rpm01, fuel01); break;
                    case ConsoleRigKind.Cape: CapeDashRender.Render(_chrome, in fit, running, rpm01, fuel01); break;
                    default: ConsoleDashRender.Render(_chrome, running, drive, rpm01, fuel01); break;
                }
            }
            if (leverDirty) LeverRigRender.Render(_leverCell, drive, finish);
            if (wheelDirty) WheelRigRender.Render(_wheelCell, _spin.Deg, rim);
            if (compassDirty)
            {
                _compassCell.Clear();
                if (fit.Compass != CompassMount.None)
                {
                    // Dome and flush are different SIZES in different PLACES; each paints at the cell's
                    // origin and the composite lands it on its own box. (The flush unit renders for the
                    // first time here: the two pilothouse hulls are the first that can mount one.)
                    HelmDashGeometry.CompassBoxOnCard(fit.Rig, fit.Compass, out _, out _, out int cw, out int ch);
                    CompassRigRender.PaintInto(_compassCell, 0, 0, cw, ch, fit.Compass, headingKey, night: false);
                }
            }

            // ---- compose: chrome → compass → wheel (+ painted cap on the console) → lever ----
            System.Array.Copy(_chrome.Pixels, _card.Pixels, _chrome.Pixels.Length);
            HelmDashGeometry.CompassBoxOnCard(fit.Rig, fit.Compass, out int dbx, out int dby, out _, out _);
            RigDrawUtil.CompositeAt(_card, _compassCell, dbx, dby);
            HelmDashGeometry.WheelCellOrigin(fit.Rig, out int wx, out int wy);
            RigDrawUtil.CompositeAt(_card, _wheelCell, wx, wy);
            if (fit.Rig == ConsoleRigKind.Console)
                ConsoleDashRender.PaintWheelCap(_card);     // the cove-painted cap rides the hub (js:449-454)
            HelmDashGeometry.LeverCellOrigin(fit.Rig, out int lx, out int ly);
            RigDrawUtil.CompositeAt(_card, _leverCell, lx, ly);

            _card.ToTexture(ref texture);
            image.texture = texture;

            _shownRig = fit.Rig;
            _shownRunning = running;
            _shownDriveKey = driveKey;
            _shownFinish = finish;
            _shownWheelKey = wheelKey;
            _shownRim = rim;
            _shownHeadingKey = headingKey;
            _shownCompass = fit.Compass;
            _shownBrowKey = browKey;
            return true;
        }

        /// <summary>
        /// Allocate (or RE-allocate) the layer surfaces for a rig. The two helm families need
        /// different canvases — 600×510 for the skiffs, 600×548 for the pilothouse — and different
        /// compass cells, so swapping hulls across the families rebuilds them. That is the only
        /// allocation on the path, and it happens on a hull change, never in the steady state (rule 7).
        /// </summary>
        private void EnsureSurfaces(ConsoleRigKind rig)
        {
            int w = HelmDashGeometry.CanvasW(rig), h = HelmDashGeometry.CanvasH(rig);
            if (_chrome == null || _chrome.Width != w || _chrome.Height != h)
            {
                _chrome = new DrawSurface(w, h);
                _card = new DrawSurface(w, h);
            }
            // The compass cell holds whichever unit is fitted; the dome box is the larger of the two.
            HelmDashGeometry.DomeBoxOnCard(rig, out _, out _, out int cw, out int ch);
            if (_compassCell == null || _compassCell.Width != cw || _compassCell.Height != ch)
                _compassCell = new DrawSurface(cw, ch);
            if (_wheelCell == null) _wheelCell = new DrawSurface(WheelRigRender.W, WheelRigRender.H);
            if (_leverCell == null) _leverCell = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
        }

        // ---- interaction (FOCUSED state; card-space rig px, y down) --------------------------------

        /// <summary>A press landed on the focused dash: route it to the instrument under it —
        /// wheel rim grab, lever grip drag, or the binnacle travel-guide jump (the S1 semantics).</summary>
        public void Press(IHelmControl helm, Vector2 cardPx, in HelmOverlaySettings cfg)
        {
            HelmWheelSettings wheelCfg = GameServices.HelmWheel;
            if (HelmDashGeometry.IsOnWheel(_rig, cardPx, wheelCfg.RimGrabPadPx))
            {
                _wheelGrabbed = true;
                _steerSession = true;
                _lastPointerAngle = HelmDashGeometry.WheelPointerAngleDeg(_rig, cardPx);
                helm.DragSteer(helm.Steer);      // open the session at the current steer (no jump)
                return;
            }
            if (HelmDashGeometry.IsInBinnacle(_rig, cardPx))
            {
                float sig = HelmDashGeometry.DashLeverSigAt(_rig, cardPx);
                if (HelmDashGeometry.IsOnDashLeverGrip(_rig, cardPx, helm.Drive, cfg.GrabRadiusPx))
                {
                    _leverDragging = true;       // continuous drag, live while held
                    helm.DragDrive(sig);
                }
                else
                {
                    helm.DragDrive(sig);         // travel-guide click: jump-to-sig, then hold
                    helm.EndDrag();
                }
            }
        }

        /// <summary>Continue live drags. Called every focused frame with the pointer state.</summary>
        public void Track(IHelmControl helm, Vector2 cardPx, bool held, float dt)
        {
            if (_wheelGrabbed)
            {
                if (held && _steerSession)
                {
                    HelmWheelSettings wheelCfg = GameServices.HelmWheel;
                    double ang = HelmDashGeometry.WheelPointerAngleDeg(_rig, cardPx);
                    double delta = ang - _lastPointerAngle;
                    _lastPointerAngle = ang;
                    _spin = WheelRigGeometry.Drag(_spin, delta, dt, wheelCfg.Turns, out _);
                    helm.DragSteer((float)WheelRigGeometry.SteerFromDeg(_spin.Deg, wheelCfg.Turns));
                }
                else
                {
                    _wheelGrabbed = false;       // released: the spin coasts under the live session
                }
                return;
            }
            if (_leverDragging)
            {
                if (held)
                {
                    helm.DragDrive(HelmDashGeometry.DashLeverSigAt(_rig, cardPx));
                }
                else
                {
                    helm.EndDrag();
                    _leverDragging = false;
                }
            }
        }

        /// <summary>True while any dash drag session is live (the host keeps routing to Track).</summary>
        public bool Interacting => _wheelGrabbed || _leverDragging;
    }
}
