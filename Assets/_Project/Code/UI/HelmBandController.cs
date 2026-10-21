using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>What the band shows of the sounder: the pilot bands' depth digits (the brief :142).
    /// Gathered by <see cref="HelmOverlayHost"/> from the Core instrument seam, so the controller is
    /// testable without a service.</summary>
    public readonly struct HelmBandGlance
    {
        /// <summary>True when the transducer has a reading (<see cref="IHelmInstruments.TryReadDepth"/>).</summary>
        public readonly bool HasDepth;

        /// <summary>The reading, metres (the sounder's own throttled read, never the raster).</summary>
        public readonly float DepthMetres;

        /// <summary>The hull's sounder units (<see cref="SounderPrefs.Feet"/>).</summary>
        public readonly bool Feet;

        /// <summary>The shallow alarm is sounding AND its blink is in the lit half
        /// (<see cref="DepthSounder.ShallowAlarm"/>, <see cref="DepthSounder.BlinkPhase"/>).</summary>
        public readonly bool AlarmLit;

        public HelmBandGlance(bool hasDepth, float depthMetres, bool feet, bool alarmLit)
        {
            HasDepth = hasDepth; DepthMetres = depthMetres; Feet = feet; AlarmLit = alarmLit;
        }

        /// <summary>No reading: the plates carry their names only.</summary>
        public static HelmBandGlance None => default;
    }

    /// <summary>
    /// The helm BAND's painter and controls (H2, the owner's 2026-09-24 ruling), owned by
    /// <see cref="HelmOverlayHost"/>: a plain class, so the repaint keys, the wheel session and the
    /// hit routing are testable without a canvas. It replaces the composed dash card (S2a/S4): the
    /// same chrome, wheel, lever and compass renders, laid out as a full-width band of pieces
    /// (<see cref="HelmBandGeometry"/>, <see cref="HelmBandLayout"/>) instead of one card.
    ///
    /// <para><b>Repaint discipline (rule 7):</b> every piece has its own surface, texture and change
    /// key, and is uploaded only when its key changes: the tach on the lever's 1/48 drive step, the
    /// wheel on its half degree, the compass on the whole degree (and only when one is fitted), the
    /// drive on its step, finish and gear, a plate on its name, digits and blink. The dash chrome is
    /// re-rastered only when one of its three cut pieces needs it, and each cut is a row copy. Nothing
    /// is allocated in the steady state; a depth string is formatted only when the reading or the
    /// units change.</para>
    ///
    /// <para><b>No focus.</b> The band's controls are always live: the wheel's top half steers, the
    /// lever drags in its window, the anchor switch flicks, and a fitted housing raises its card
    /// (<see cref="HelmInstrumentExpansion"/>). The pointer is read by the host and handed in, so this
    /// file reads no device (ADR 0043's burn-down cannot grow).</para>
    ///
    /// <para><b>One steer owner:</b> the wheel MIRRORS <see cref="IHelmControl.Steer"/> whenever no
    /// steer session is live; a rim grab opens the session (<see cref="IHelmControl.DragSteer"/>) and
    /// the {deg, vel} spin state here is only the input device's transient state. A key press breaking
    /// the session (<see cref="IHelmControl.SteerDragActive"/> going false) drops the grab the same
    /// frame: keys win, as on the dash.</para>
    ///
    /// <para><b>The anchor switch</b> (ADR 0039) is drawn from <see cref="IHelmControl.AnchorState"/>
    /// in the switches piece, and a press on it is <see cref="IHelmControl.ToggleAnchor"/>, the same
    /// toggle the anchor key presses, gated on the hull carrying a hook.</para>
    /// </summary>
    public sealed class HelmBandController
    {
        public const string SounderLabel = "SOUNDER", FinderLabel = "FINDER";
        public const string RadarLabel = "RADAR", PlotterLabel = "PLOTTER";
        public const string MetresUnit = "M", FeetUnit = "FT";
        public const string GearForward = "F", GearNeutral = "N", GearReverse = "R";

        private const int Max = HelmBandGeometry.MaxPieces;

        // ---- surfaces and textures (allocated on a rig change only) --------------------------------
        private DrawSurface _chrome, _wheelCell, _leverCell;
        private readonly DrawSurface[] _surfaces = new DrawSurface[Max];
        private readonly Texture2D[] _textures = new Texture2D[Max];

        // ---- layout --------------------------------------------------------------------------------
        private ConsoleRigKind _rig = ConsoleRigKind.None;
        private int _count;
        private int _k = 1;
        private int _screenW = -1, _screenH = -1;
        private readonly Rect[] _rects = new Rect[Max];

        // ---- shown keys ----------------------------------------------------------------------------
        private ConsoleRigKind _shownRig = ConsoleRigKind.None;
        private bool _shownRunning;
        private int _shownTachKey = int.MinValue;          // the drive step the tach shows
        private int _shownBrowKey = int.MinValue;          // the fit the chrome was rastered for
        private bool _shownNight;                          // the chrome's panel lights
        private bool _shownAnchorFitted;
        private bool _shownAnchorDown;
        private int _shownWheelKey = int.MinValue;
        private HelmWheelRim _shownRim = (HelmWheelRim)(-1);
        private int _shownLeverKey = int.MinValue;
        private HelmLeverFinish _shownFinish = (HelmLeverFinish)(-1);
        private int _shownGear = int.MinValue;
        private bool _shownDriveNight;
        private int _shownHeadingKey = int.MinValue;
        private CompassMount _shownCompass = (CompassMount)(-1);
        private bool _shownCompassNight;
        private readonly string[] _shownLabel = new string[Max];
        private readonly string[] _shownDigits = new string[Max];
        private readonly bool[] _shownLit = new bool[Max];
        private readonly bool[] _shownPlateNight = new bool[Max];

        // The pilot bands' depth string, formatted only when the reading or the units change.
        private bool _fmtHas;
        private float _fmtMetres = float.NaN;
        private bool _fmtFeet;
        private string _fmtDigits;

        // ---- wheel and lever device state (transient input state, never saved: rule 5) -------------
        private WheelRigGeometry.SpinState _spin;
        private bool _steerSession;
        private bool _wheelGrabbed;
        private double _lastPointerAngle;
        private bool _leverDragging;

        // ---- seams ---------------------------------------------------------------------------------

        /// <summary>The rig this band is laid out for.</summary>
        public ConsoleRigKind Rig => _rig;

        /// <summary>How many pieces the band has (0 with no rig).</summary>
        public int PieceCount => _count;

        /// <summary>The band's whole-number scale.</summary>
        public int K => _k;

        /// <summary>The <paramref name="i"/>th piece.</summary>
        public HelmBandPiece Piece(int i) => HelmBandGeometry.Piece(_rig, i);

        /// <summary>The <paramref name="i"/>th piece's screen rect (after <see cref="Layout"/>).</summary>
        public Rect PieceRect(int i) => _rects[i];

        /// <summary>The <paramref name="i"/>th piece's texture (null for the caps, and until painted).</summary>
        public Texture2D TextureOf(int i) => _textures[i];

        /// <summary>The <paramref name="i"/>th piece's surface (test seam; null for the caps).</summary>
        public DrawSurface SurfaceOf(int i) => _surfaces[i];

        /// <summary>Texture uploads since construction (test seam: rule 7's "only on a change").</summary>
        public int UploadCount { get; private set; }

        /// <summary>The wheel angle currently rendered (test seam).</summary>
        public double WheelDeg => _spin.Deg;

        /// <summary>True while this band holds the steer session (test seam).</summary>
        public bool SteerSession => _steerSession;

        /// <summary>Is the anchor switch DRAWN thrown (what the band shows, not what the tackle does)?</summary>
        public bool AnchorShown => _shownAnchorDown;

        /// <summary>Is an anchor switch drawn at all (false on a hull that carries no hook)?</summary>
        public bool AnchorSwitchShown => _shownAnchorFitted;

        /// <summary>Are the panel lights on in what is DRAWN (the host tints the panel and caps by it)?</summary>
        public bool NightShown => _shownNight;

        /// <summary>The gear letter drawn (F, N or R; null before the first paint).</summary>
        public string GearShown => _shownGear == int.MinValue ? null
            : (_shownGear > 0 ? GearForward : (_shownGear < 0 ? GearReverse : GearNeutral));

        /// <summary>What the <paramref name="i"/>th piece's plate says (null when blank or not a plate).</summary>
        public string PlateLabelShown(int i) => _shownLabel[i];

        /// <summary>The depth digits the <paramref name="i"/>th plate shows (null for none).</summary>
        public string PlateDigitsShown(int i) => _shownDigits[i];

        /// <summary>Is the <paramref name="i"/>th plate's depth drawn in the alarm's red?</summary>
        public bool PlateAlarmShown(int i) => _shownLit[i];

        /// <summary>True while a wheel or lever drag is live (the host keeps routing to <see cref="Track"/>).</summary>
        public bool Interacting => _wheelGrabbed || _leverDragging;

        // ---- layout --------------------------------------------------------------------------------

        /// <summary>Lay the band out for a rig on a screen. Cheap when nothing changed (two int
        /// compares); a rig change rebuilds the surfaces, the only allocation on the path.</summary>
        public void Layout(ConsoleRigKind rig, int screenW, int screenH)
        {
            if (rig != _rig) SetRig(rig);
            if (screenW == _screenW && screenH == _screenH) return;
            _screenW = screenW;
            _screenH = screenH;
            _k = HelmBandLayout.K(screenH);
            for (int i = 0; i < _count; i++) _rects[i] = HelmBandLayout.PieceRect(_rig, i, screenW, screenH);
        }

        /// <summary>Is this screen point on the band (its full width, 144·k up from the bottom)?</summary>
        public bool Contains(Vector2 screen)
            => _count > 0 && _screenW > 0 && screen.x >= 0f && screen.x < _screenW
               && screen.y >= 0f && screen.y < HelmBandGeometry.H * _k;

        private void SetRig(ConsoleRigKind rig)
        {
            _rig = rig;
            _count = HelmBandGeometry.PieceCount(rig);
            _screenW = _screenH = -1;      // the rects follow on the next Layout
            Invalidate();
            if (_count == 0) return;
            for (int i = 0; i < _count; i++)
            {
                HelmBandPiece p = HelmBandGeometry.Piece(rig, i);
                if (p.Kind == HelmBandPieceKind.Cap) { _surfaces[i] = null; continue; }
                DrawSurface s = _surfaces[i];
                if (s == null || s.Width != p.W || s.Height != HelmBandGeometry.H)
                    _surfaces[i] = new DrawSurface(p.W, HelmBandGeometry.H);
            }
            for (int i = _count; i < Max; i++) _surfaces[i] = null;
            int cw = HelmDashGeometry.CanvasW(rig), ch = HelmDashGeometry.CanvasH(rig);
            if (_chrome == null || _chrome.Width != cw || _chrome.Height != ch) _chrome = new DrawSurface(cw, ch);
            if (_wheelCell == null) _wheelCell = new DrawSurface(WheelRigRender.W, WheelRigRender.H);
            if (_leverCell == null) _leverCell = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
        }

        /// <summary>Forget everything shown (hull swap, style change) so the next paint is full.</summary>
        public void Invalidate()
        {
            _shownRig = ConsoleRigKind.None;
            _shownTachKey = int.MinValue;
            _shownBrowKey = int.MinValue;
            _shownWheelKey = int.MinValue;
            _shownLeverKey = int.MinValue;
            _shownGear = int.MinValue;
            _shownHeadingKey = int.MinValue;
            _shownCompass = (CompassMount)(-1);
            for (int i = 0; i < Max; i++)
            {
                _shownLabel[i] = null;
                _shownDigits[i] = null;
                _shownLit[i] = false;
            }
        }

        /// <summary>End every live interaction (helm lost, band hidden).</summary>
        public void Deactivate(IHelmControl helm)
        {
            if (_leverDragging && helm != null) helm.EndDrag();
            _leverDragging = false;
            if (_steerSession && helm != null) helm.EndSteerDrag();
            _steerSession = false;
            _wheelGrabbed = false;
        }

        /// <summary>Destroy the piece textures (the host's teardown, or a test's).</summary>
        public void ReleaseTextures()
        {
            for (int i = 0; i < Max; i++)
            {
                if (_textures[i] != null) DestroyTexture(_textures[i]);
                _textures[i] = null;
            }
            Invalidate();
        }

        // ---- painting ------------------------------------------------------------------------------

        /// <summary>
        /// Advance the wheel device (mirror, or coast under a live session), repaint and upload every
        /// piece whose key changed, and nothing else. Returns true when any texture was uploaded.
        /// </summary>
        public bool UpdateAndPaint(IHelmControl helm, in HelmFit fit, float headingDeg, float dt,
                                   in HelmBandGlance glance)
        {
            if (helm == null) return false;
            if (fit.Rig != _rig) SetRig(fit.Rig);
            if (_count == 0) return false;
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

            // ---- change keys (the dash's, piece by piece) ----
            float drive = helm.Drive;
            bool running = true;                        // manned helm = running (S1 precedent)
            const float fuel01 = 1f;                    // no fuel sim yet: the needle pins at F
            float rpm01 = running ? Mathf.Clamp01(0.11f + 0.89f * Mathf.Abs(drive)) : 0f;
            int driveKey = LeverRigGeometry.QuantizeKey(drive);
            int wheelKey = WheelRigGeometry.QuantizeKey(_spin.Deg);
            int headingKey = DrawSurface.JsRound(CompassRigRender.Norm(headingDeg));
            HelmLeverFinish finish = helm.LeverFinish;
            HelmWheelRim rim = helm.WheelRim;
            bool night = HelmPanelLight.IsNight();
            bool anchorFitted = helm.HasAnchor;
            bool anchorDown = helm.AnchorState != AnchorState.Stowed;
            int browKey = (((int)fit.Sounder * 3 + (int)fit.Compass) << 2)
                        | (fit.Radar ? 2 : 0) | (fit.Gps ? 1 : 0);
            // The gear letter is the drive's sign: the neutral snap makes 0 exact.
            int gear = drive > 0f ? 1 : (drive < 0f ? -1 : 0);

            int uploadsBefore = UploadCount;
            bool all = _rig != _shownRig;

            // ---- the three chrome cuts: one raster, then only the cuts whose own state moved ----
            bool chromeFrame = all || running != _shownRunning || night != _shownNight || browKey != _shownBrowKey;
            bool tachDirty = chromeFrame || driveKey != _shownTachKey;
            bool switchDirty = chromeFrame || anchorFitted != _shownAnchorFitted || anchorDown != _shownAnchorDown;
            bool fuelDirty = chromeFrame;
            if (tachDirty || switchDirty || fuelDirty)
            {
                RenderChrome(in fit, running, drive, rpm01, fuel01, night, anchorFitted, anchorDown);
                int toppad = HelmDashGeometry.Toppad(_rig);
                for (int i = 0; i < _count; i++)
                {
                    HelmBandPiece p = HelmBandGeometry.Piece(_rig, i);
                    bool dirty = (p.Kind == HelmBandPieceKind.Tach && tachDirty)
                                 || (p.Kind == HelmBandPieceKind.Switches && switchDirty)
                                 || (p.Kind == HelmBandPieceKind.Fuel && fuelDirty);
                    if (!dirty) continue;
                    CopyRegion(_chrome, p.CropX, p.CropY + toppad, _surfaces[i]);
                    Upload(i);
                }
                _shownRunning = running;
                _shownNight = night;
                _shownBrowKey = browKey;
                _shownTachKey = driveKey;
                _shownAnchorFitted = anchorFitted;
                _shownAnchorDown = anchorDown;
            }

            for (int i = 0; i < _count; i++)
            {
                HelmBandPiece p = HelmBandGeometry.Piece(_rig, i);
                switch (p.Kind)
                {
                    case HelmBandPieceKind.Wheel:
                        if (!all && wheelKey == _shownWheelKey && rim == _shownRim) break;
                        PaintWheel(_surfaces[i], rim);
                        Upload(i);
                        _shownWheelKey = wheelKey;
                        _shownRim = rim;
                        break;
                    case HelmBandPieceKind.Drive:
                        if (!all && driveKey == _shownLeverKey && finish == _shownFinish && gear == _shownGear
                            && night == _shownDriveNight) break;
                        PaintDrive(_surfaces[i], drive, finish, gear, night);
                        Upload(i);
                        _shownLeverKey = driveKey;
                        _shownFinish = finish;
                        _shownGear = gear;
                        _shownDriveNight = night;
                        break;
                    case HelmBandPieceKind.Compass:
                        // The heading keys the picture only while a compass is drawn: a blank mount
                        // costs nothing however the hull turns.
                        bool headingMoved = fit.Compass != CompassMount.None && headingKey != _shownHeadingKey;
                        if (!all && fit.Compass == _shownCompass && !headingMoved && night == _shownCompassNight) break;
                        PaintCompass(_surfaces[i], fit.Compass, headingKey, night);
                        Upload(i);
                        _shownCompass = fit.Compass;
                        _shownHeadingKey = headingKey;
                        _shownCompassNight = night;
                        break;
                    case HelmBandPieceKind.SounderPlate:
                    case HelmBandPieceKind.RadarPlate:
                    case HelmBandPieceKind.GpsPlate:
                        PaintPlateIfChanged(i, p.Kind, in fit, in glance, night, all);
                        break;
                }
            }

            _shownRig = _rig;
            return UploadCount != uploadsBefore;
        }

        private void RenderChrome(in HelmFit fit, bool running, float drive, float rpm01, float fuel01,
                                  bool night, bool anchorFitted, bool anchorDown)
        {
            switch (fit.Rig)
            {
                case ConsoleRigKind.Sport:
                    SportDashRender.Render(_chrome, running, drive, rpm01, fuel01, night,
                                           anchorFitted: anchorFitted, anchorDown: anchorDown);
                    break;
                case ConsoleRigKind.Novi:
                    NoviDashRender.Render(_chrome, in fit, running, rpm01, fuel01, night, anchorDown: anchorDown);
                    break;
                case ConsoleRigKind.Cape:
                    CapeDashRender.Render(_chrome, in fit, running, rpm01, fuel01, night, anchorDown: anchorDown);
                    break;
                default:
                    ConsoleDashRender.Render(_chrome, running, drive, rpm01, fuel01, night,
                                             anchorFitted: anchorFitted, anchorDown: anchorDown);
                    break;
            }
        }

        // The wheel cell's top half, its hub on the piece's bottom edge; the rows above it are clear
        // so the panel shows through. (The console dash's painted cap is a dash-card detail and is
        // not drawn on the band.)
        private void PaintWheel(DrawSurface piece, HelmWheelRim rim)
        {
            WheelRigRender.Render(_wheelCell, _spin.Deg, rim);
            piece.Clear();
            int rows = WheelRigGeometry.PVY;
            System.Array.Copy(_wheelCell.Pixels, 0, piece.Pixels, HelmBandGeometry.WheelTopPad * piece.Width,
                              rows * _wheelCell.Width);
        }

        private void PaintDrive(DrawSurface piece, float drive, HelmLeverFinish finish, int gear, bool night)
        {
            LeverRigRender.Render(_leverCell, drive, finish);
            piece.Clear();
            for (int y = 0; y < piece.Height; y++)
                System.Array.Copy(_leverCell.Pixels,
                                  (HelmBandGeometry.DriveWindowY + y) * _leverCell.Width + HelmBandGeometry.DriveWindowX,
                                  piece.Pixels, y * piece.Width, HelmBandGeometry.DriveWindowW);
            string letter = gear > 0 ? GearForward : (gear < 0 ? GearReverse : GearNeutral);
            HelmBandPlateRender.GearLetter(piece, HelmBandGeometry.GearX, HelmBandGeometry.GearW, letter, night);
        }

        private static void PaintCompass(DrawSurface piece, CompassMount mount, int headingKey, bool night)
        {
            if (mount == CompassMount.Dome)
            {
                piece.Clear();
                CompassRigRender.PaintInto(piece, 0, 0, piece.Width, piece.Height, mount, headingKey, night);
                return;
            }
            HelmBandPlateRender.Blank(piece, night);
            if (mount == CompassMount.Flush)
                CompassRigRender.PaintInto(piece, HelmBandGeometry.FlushX, HelmBandGeometry.FlushY,
                                           HelmBandGeometry.FlushW, HelmBandGeometry.FlushH, mount, headingKey, night);
        }

        private void PaintPlateIfChanged(int i, HelmBandPieceKind kind, in HelmFit fit, in HelmBandGlance glance,
                                         bool night, bool all)
        {
            string label = PlateLabel(kind, in fit);
            string digits = null, unit = null;
            bool lit = false;
            // The pilot bands carry the depth in the sounder's housing (the brief :142); the skiffs'
            // housing is a name only.
            if (kind == HelmBandPieceKind.SounderPlate && label != null && HelmDashGeometry.IsPilothouse(_rig)
                && glance.HasDepth)
            {
                digits = DepthDigits(glance.DepthMetres, glance.Feet);
                unit = glance.Feet ? FeetUnit : MetresUnit;
                lit = glance.AlarmLit;
            }
            if (!all && label == _shownLabel[i] && ReferenceEquals(digits, _shownDigits[i]) && lit == _shownLit[i]
                && night == _shownPlateNight[i] && _textures[i] != null)
                return;
            DrawSurface s = _surfaces[i];
            if (label == null) HelmBandPlateRender.Blank(s, night);
            else if (digits == null) HelmBandPlateRender.Named(s, label, night);
            else HelmBandPlateRender.NamedWithDepth(s, label, digits, unit, lit, night);
            Upload(i);
            _shownLabel[i] = label;
            _shownDigits[i] = digits;
            _shownLit[i] = lit;
            _shownPlateNight[i] = night;
        }

        // Formats only when the reading or the units moved, so a steady reading reuses one string.
        private string DepthDigits(float metres, bool feet)
        {
            if (_fmtHas && metres == _fmtMetres && feet == _fmtFeet) return _fmtDigits;
            string next = DepthRigGeometry.FmtDepth(metres, feet);
            // Keep the old reference when the digits read the same, so the plate's key holds still.
            if (!(_fmtHas && next == _fmtDigits)) _fmtDigits = next;
            _fmtHas = true;
            _fmtMetres = metres;
            _fmtFeet = feet;
            return _fmtDigits;
        }

        /// <summary>What a housing says for this fit: the instrument's name, or null (blank) when the
        /// mount is empty.</summary>
        public static string PlateLabel(HelmBandPieceKind kind, in HelmFit fit)
        {
            switch (kind)
            {
                case HelmBandPieceKind.SounderPlate:
                    return fit.Sounder == SounderKind.Depth ? SounderLabel
                         : (fit.Sounder == SounderKind.Fish ? FinderLabel : null);
                case HelmBandPieceKind.RadarPlate:
                    return fit.Radar ? RadarLabel : null;
                case HelmBandPieceKind.GpsPlate:
                    return fit.Gps ? PlotterLabel : null;
            }
            return null;
        }

        /// <summary>Which instrument a housing raises for this fit (None for an empty mount).</summary>
        public static HelmInstrumentSlot SlotOf(HelmBandPieceKind kind, in HelmFit fit)
        {
            switch (kind)
            {
                case HelmBandPieceKind.SounderPlate: return HelmInstrumentExpansion.SlotFor(fit.Sounder);
                case HelmBandPieceKind.RadarPlate: return fit.Radar ? HelmInstrumentSlot.Radar : HelmInstrumentSlot.None;
                case HelmBandPieceKind.GpsPlate: return fit.Gps ? HelmInstrumentSlot.Chartplotter : HelmInstrumentSlot.None;
            }
            return HelmInstrumentSlot.None;
        }

        private static void CopyRegion(DrawSurface src, int sx, int sy, DrawSurface dst)
        {
            for (int y = 0; y < dst.Height; y++)
                System.Array.Copy(src.Pixels, (sy + y) * src.Width + sx, dst.Pixels, y * dst.Width, dst.Width);
        }

        private void Upload(int i)
        {
            DrawSurface s = _surfaces[i];
            Texture2D t = _textures[i];
            if (t != null && (t.width != s.Width || t.height != s.Height))
            {
                DestroyTexture(t);
                _textures[i] = null;
            }
            s.ToTexture(ref _textures[i]);
            UploadCount++;
        }

        private static void DestroyTexture(Texture2D t)
        {
            if (Application.isPlaying) Object.Destroy(t);
            else Object.DestroyImmediate(t);
        }

        // ---- interaction (screen px in; each piece maps it into its own px) -------------------------

        /// <summary>The instrument a press at <paramref name="screen"/> would raise: the fitted housing
        /// under it, or None (an empty housing, or no housing at all).</summary>
        public HelmInstrumentSlot PlateAt(in HelmFit fit, Vector2 screen)
        {
            for (int i = 0; i < _count; i++)
            {
                HelmBandPiece p = HelmBandGeometry.Piece(_rig, i);
                if (!p.IsPlate || !_rects[i].Contains(screen)) continue;
                return SlotOf(p.Kind, in fit);
            }
            return HelmInstrumentSlot.None;
        }

        /// <summary>
        /// A press on the band's controls: the anchor switch's discrete flick first (the smallest,
        /// box-bounded control goes ahead of the two bounded by a radius), then a wheel rim grab, then
        /// the lever (a grip drag, or a travel-guide jump). True when a control took the press.
        /// </summary>
        public bool PressControls(IHelmControl helm, Vector2 screen, in HelmOverlaySettings cfg)
        {
            if (helm == null || _count == 0) return false;
            int si = HelmBandGeometry.IndexOf(_rig, HelmBandPieceKind.Switches);
            if (si >= 0 && helm.HasAnchor && HelmBandLayout.ToPieceLocal(screen, _rects[si], _k, out Vector2 sw)
                && HelmBandGeometry.IsOnAnchorSwitch(_rig, sw))
            {
                helm.ToggleAnchor();
                return true;
            }

            int wi = HelmBandGeometry.WheelIndex(_rig);
            HelmWheelSettings wheelCfg = GameServices.HelmWheel;
            if (wi >= 0 && HelmBandLayout.ToPieceLocal(screen, _rects[wi], _k, out Vector2 wl)
                && HelmBandGeometry.IsOnWheel(wl, wheelCfg.RimGrabPadPx))
            {
                _wheelGrabbed = true;
                _steerSession = true;
                _lastPointerAngle = HelmBandGeometry.WheelPointerAngleDeg(wl);
                helm.DragSteer(helm.Steer);      // open the session at the current steer (no jump)
                return true;
            }

            int di = HelmBandGeometry.IndexOf(_rig, HelmBandPieceKind.Drive);
            if (di >= 0 && HelmBandLayout.ToPieceLocal(screen, _rects[di], _k, out Vector2 dl)
                && HelmBandGeometry.IsInDriveWindow(dl))
            {
                Vector2 cell = HelmBandGeometry.DriveToLeverCell(dl);
                float sig = HelmOverlayLayout.LeverSigAt(cell);
                if (HelmOverlayLayout.IsOnGrip(cell, helm.Drive, cfg.GrabRadiusPx))
                {
                    _leverDragging = true;       // continuous drag, live while held
                    helm.DragDrive(sig);
                }
                else
                {
                    helm.DragDrive(sig);         // travel-guide click: jump-to-sig, then hold
                    helm.EndDrag();
                }
                return true;
            }
            return false;
        }

        /// <summary>Continue a live drag with the pointer's screen position. A drag that leaves its
        /// piece still tracks (the dash's precedent).</summary>
        public void Track(IHelmControl helm, Vector2 screen, bool held, float dt)
        {
            if (helm == null) return;
            if (_wheelGrabbed)
            {
                int wi = HelmBandGeometry.WheelIndex(_rig);
                if (held && _steerSession && wi >= 0)
                {
                    HelmWheelSettings wheelCfg = GameServices.HelmWheel;
                    HelmBandLayout.ToPieceLocal(screen, _rects[wi], _k, out Vector2 wl);
                    double ang = HelmBandGeometry.WheelPointerAngleDeg(wl);
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
                int di = HelmBandGeometry.IndexOf(_rig, HelmBandPieceKind.Drive);
                if (held && di >= 0)
                {
                    HelmBandLayout.ToPieceLocal(screen, _rects[di], _k, out Vector2 dl);
                    helm.DragDrive(HelmOverlayLayout.LeverSigAt(HelmBandGeometry.DriveToLeverCell(dl)));
                }
                else
                {
                    helm.EndDrag();
                    _leverDragging = false;
                }
            }
        }
    }
}
