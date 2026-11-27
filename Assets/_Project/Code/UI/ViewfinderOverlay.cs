using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>The camera's viewfinder</b>: four corner brackets over the patch of world the picture takes in,
    /// the hint under it, the word about the last picture and the shutter's flash. It draws
    /// <see cref="PhotoViewfinder"/> (Core) and nothing else, so it never names the camera (rule 4).
    ///
    /// <para><b>No mixels</b> (owner 2026-11-26). The canvas is the pixel face's
    /// (<see cref="PaperUi.MakeScreen"/> with <c>pixelFace</c>): its scale is a whole number of screen
    /// pixels per kit pixel. Every bracket is one kit pixel thick, and every corner is snapped to a whole
    /// kit pixel on screen (<see cref="SnapToKit"/>), so a bracket is never a fractional line nor a
    /// different pixel size from the words under it. The words are the pixel face at one of its own sizes.</para>
    ///
    /// <para><b>Frame budget</b> (rule 7): a quiet frame is a few compares. Strings are assigned only when
    /// they change, and nothing here allocates per frame.</para>
    /// </summary>
    public sealed class ViewfinderOverlay : MonoBehaviour
    {
        /// <summary>Above the HUD and the world's popups, under the pause menu (310).</summary>
        public const int SortingOrder = 260;

        /// <summary>A bracket's arm, in kit pixels.</summary>
        public const int ArmKitPixels = 6;

        /// <summary>A bracket's thickness, in kit pixels: one, always.</summary>
        public const int ThickKitPixels = 1;

        /// <summary>The words' asked size (the face snaps it to one of its own).</summary>
        public const int TextSize = 24;

        /// <summary>Where the word about the last picture sits on the reference page (y down).</summary>
        public const float LineY = 600f;

        /// <summary>Where the hint sits on the reference page (y down).</summary>
        public const float HintY = 648f;

        /// <summary>One line's height on the reference page.</summary>
        public const float LineHeight = 48f;

        /// <summary>The shutter's flash: chalk, mostly see-through.</summary>
        public static readonly Color FlashInk = new Color(0.937f, 0.914f, 0.855f, 0.55f);

        private static ViewfinderOverlay _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("ViewfinderOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ViewfinderOverlay>();
        }

        /// <summary>The installed overlay, or null.</summary>
        public static ViewfinderOverlay Instance => _instance;

        /// <summary>A plate's switch: draw even while the world is stopped (a plate is shot from the title,
        /// where the shell holds the world). Never set by the game. Reset every play session.</summary>
        public static bool DrawWhileWorldStopped { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => DrawWhileWorldStopped = false;

        private Camera _through;

        /// <summary>
        /// A plate's view: draw into <paramref name="cam"/>'s own target (screen-space-camera) and project the
        /// frame through it; null puts it back on the screen overlay and <c>Camera.main</c>.
        /// </summary>
        public void DrawThrough(Camera cam)
        {
            _through = cam;
            _canvas.renderMode = cam != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = cam;
            if (cam == null)
            {
                _canvas.sortingLayerID = _baseLayer;
                return;
            }
            _canvas.planeDistance = cam.nearClipPlane + 1f;
            // A camera-space canvas sorts with the world: the topmost layer, so no prop covers the words.
            SortingLayer[] layers = SortingLayer.layers;
            if (layers.Length > 0) _canvas.sortingLayerID = layers[layers.Length - 1].id;
        }

        private int _baseLayer;

        private Canvas _canvas;
        private RectTransform _canvasRt;
        private readonly RectTransform[] _arms = new RectTransform[8];
        private Image _flash;
        private Text _line;
        private Text _hint;
        private string _paintedLine;
        private string _paintedHint;

        /// <summary>True while any of it is on screen (tests and plates).</summary>
        public bool Drawn => _canvas != null && _canvas.gameObject.activeSelf;

        /// <summary>True while the brackets are drawn.</summary>
        public bool FrameDrawn => _arms[0] != null && _arms[0].gameObject.activeSelf;

        /// <summary>
        /// <paramref name="screenPx"/> on the nearest whole kit pixel, where one kit pixel is
        /// <paramref name="kitPx"/> whole screen pixels (at least 1).
        /// </summary>
        public static float SnapToKit(float screenPx, int kitPx)
        {
            int k = kitPx < 1 ? 1 : kitPx;
            return Mathf.Round(screenPx / k) * k;
        }

        /// <summary>Whole screen pixels per kit pixel at canvas scale <paramref name="scaleFactor"/>.</summary>
        public static int KitPixels(float scaleFactor)
        {
            int k = Mathf.RoundToInt(scaleFactor * DialogueBubbleKit.ArtScale);
            return k < 1 ? 1 : k;
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            Build();
            _canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build()
        {
            RectTransform host = PaperUi.MakeScreen(transform, "Viewfinder_Canvas", SortingOrder, pixelFace: true);
            _canvasRt = (RectTransform)host.parent;
            _canvas = _canvasRt.GetComponent<Canvas>();
            _baseLayer = _canvas.sortingLayerID;
            var raycaster = _canvasRt.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;   // it never takes a click

            _flash = MakeFill(_canvasRt, "Flash", FlashInk);
            for (int i = 0; i < _arms.Length; i++) _arms[i] = MakeArm(_canvasRt, i);

            _line = PaperUi.MakeText(host, "", TextSize, TextAnchor.MiddleCenter, 0f, -LineY,
                                     PaperUi.ReferenceResolution.x, LineHeight, PaperUi.Chalk);
            _hint = PaperUi.MakeText(host, "", TextSize, TextAnchor.MiddleCenter, 0f, -HintY,
                                     PaperUi.ReferenceResolution.x, LineHeight, PaperUi.ChalkFaint);
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            bool stopped = ControlIntentGates.WorldStopped && !DrawWhileWorldStopped;
            bool up = PhotoViewfinder.IsUp && !stopped;
            bool lineOn = !stopped && now < PhotoViewfinder.LineUntil && !string.IsNullOrEmpty(PhotoViewfinder.Line);

            bool any = up || lineOn;
            if (_canvas.gameObject.activeSelf != any) _canvas.gameObject.SetActive(any);
            if (!any) return;

            SetActive(_flash.gameObject, up && now < PhotoViewfinder.FlashUntil);
            Paint(_line, ref _paintedLine, lineOn ? PhotoViewfinder.Line : "");
            Paint(_hint, ref _paintedHint, up ? PhotoViewfinder.Hint : "");

            bool framed = up && PlaceBrackets(PhotoViewfinder.Frame);
            for (int i = 0; i < _arms.Length; i++) SetActive(_arms[i].gameObject, framed);
        }

        /// <summary>Lay the eight arms over <paramref name="frame"/> (world metres). False when there is no
        /// camera to see it through, or it is behind the camera.</summary>
        private bool PlaceBrackets(Rect frame)
        {
            Camera cam = _through != null ? _through : Camera.main;
            if (cam == null) return false;
            Vector3 a = cam.WorldToScreenPoint(new Vector3(frame.xMin, frame.yMin, 0f));
            Vector3 b = cam.WorldToScreenPoint(new Vector3(frame.xMax, frame.yMax, 0f));
            if (a.z < 0f || b.z < 0f) return false;

            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            int k = KitPixels(scale);
            float x0 = SnapToKit(Mathf.Min(a.x, b.x), k), x1 = SnapToKit(Mathf.Max(a.x, b.x), k);
            float y0 = SnapToKit(Mathf.Min(a.y, b.y), k), y1 = SnapToKit(Mathf.Max(a.y, b.y), k);

            float arm = ArmKitPixels * k, thick = ThickKitPixels * k;
            // Bottom-left, bottom-right, top-left, top-right: a horizontal arm then a vertical one each.
            Place(0, x0, y0, arm, thick, scale);
            Place(1, x0, y0, thick, arm, scale);
            Place(2, x1 - arm, y0, arm, thick, scale);
            Place(3, x1 - thick, y0, thick, arm, scale);
            Place(4, x0, y1 - thick, arm, thick, scale);
            Place(5, x0, y1 - arm, thick, arm, scale);
            Place(6, x1 - arm, y1 - thick, arm, thick, scale);
            Place(7, x1 - thick, y1 - arm, thick, arm, scale);
            return true;
        }

        // Screen pixels in, canvas units out: a whole kit pixel is a whole number of screen pixels, so the
        // division never lands between two.
        private void Place(int i, float x, float y, float w, float h, float scale)
        {
            RectTransform rt = _arms[i];
            rt.anchoredPosition = new Vector2(x / scale, y / scale);
            rt.sizeDelta = new Vector2(w / scale, h / scale);
        }

        private static void Paint(Text t, ref string painted, string text)
        {
            if (ReferenceEquals(painted, text)) return;
            painted = text;
            t.text = text;
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go.activeSelf != on) go.SetActive(on);
        }

        private static RectTransform MakeArm(RectTransform parent, int i)
        {
            var go = new GameObject("Bracket" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = PaperUi.Chalk;
            img.raycastTarget = false;
            go.SetActive(false);
            return rt;
        }

        private static Image MakeFill(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.SetActive(false);
            return img;
        }
    }
}
