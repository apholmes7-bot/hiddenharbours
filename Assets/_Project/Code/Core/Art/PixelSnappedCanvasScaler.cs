using UnityEngine;
using UnityEngine.UI;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A ScaleWithScreenSize canvas scaler whose scale is snapped so a kit pixel lands on whole screen
    /// pixels (<see cref="PixelCanvasScale"/>), and whose canvas draws on the pixel grid. Drop-in for
    /// <see cref="CanvasScaler"/>: the reference resolution, match and reference PPU are set exactly as
    /// before; only the scale it hands the canvas changes, and only on windows where the plain scaler
    /// would have made a kit pixel a fraction of a screen pixel (1920x1080 over a 1280x720 reference,
    /// 1366x768, and so on). At 1280x720 and 2560x1440 it is identical to the plain scaler.
    /// </summary>
    [AddComponentMenu("Layout/Pixel Snapped Canvas Scaler")]
    public class PixelSnappedCanvasScaler : CanvasScaler
    {
        [Tooltip("Reference units one kit pixel is drawn across (the bubble kit's art scale).")]
        [SerializeField] private int _kit = DialogueBubbleKit.ArtScale;

        private Canvas _canvasRef;

        /// <summary>Reference units per kit pixel. Change before the first frame.</summary>
        public int Kit
        {
            get => _kit;
            set => _kit = Mathf.Max(1, value);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _canvasRef = GetComponent<Canvas>();
            // Vertices on whole screen pixels: the snapped scale fixes the SIZE of a kit pixel, this
            // fixes WHERE it starts, so a point-filtered panel and the bitmap face never straddle two.
            if (_canvasRef != null) _canvasRef.pixelPerfect = true;
        }

        protected override void HandleScaleWithScreenSize()
        {
            if (_canvasRef == null) _canvasRef = GetComponent<Canvas>();
            Vector2 screen = _canvasRef != null ? _canvasRef.renderingDisplaySize
                                                : new Vector2(Screen.width, Screen.height);
            if (_canvasRef != null)
            {
                int display = _canvasRef.targetDisplay;
                if (display > 0 && display < Display.displays.Length)
                    screen = new Vector2(Display.displays[display].renderingWidth,
                                         Display.displays[display].renderingHeight);
            }

            float raw = PixelCanvasScale.MatchWidthOrHeight(screen.x, screen.y, referenceResolution.x,
                                                            referenceResolution.y, matchWidthOrHeight);
            SetScaleFactor(PixelCanvasScale.Snap(raw, _kit));
            SetReferencePixelsPerUnit(referencePixelsPerUnit);
        }
    }
}
