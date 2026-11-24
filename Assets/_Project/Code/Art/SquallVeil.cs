using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The dark front of an authored local squall (Core's <see cref="LocalWeather.Squall"/>): a soft-edged
    /// dark veil that stands on the side of the view the weather lies on and comes across it as the
    /// player closes with it, until the whole view is under it. It is the "black sky that way" a
    /// top-down camera has no horizon to show.
    ///
    /// <para><b>Purely drawn.</b> It reads one Core struct and the camera, and writes two sprites. It
    /// draws no wall in the world, touches no sim, saves nothing; with no squall set both sprites are
    /// off. How dark, how soft and what colour are the squall's own data, authored on the effect that
    /// set it; this class only holds where it sorts.</para>
    ///
    /// <para><b>Self-installing</b> like <see cref="RainEmitter"/>: one hidden persistent host, two
    /// pooled sprites, no per-frame allocation. The sprites are placed again for each game camera as
    /// it renders, so the front sits on the frame the camera draws.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquallVeil : MonoBehaviour
    {
        [Tooltip("Sorting order: over the water, the boats and all decor, under the falling rain.")]
        [SerializeField] private int _sortingOrder = SortingBands.AboveDecor - 1;

        [Tooltip("Metres toward the camera the veil is nudged, so it draws in front of the water mesh. " +
                 "Less than the rain's, so the streaks fall in front of it.")]
        [SerializeField] private float _cameraZOffset = 0.5f;

        [Tooltip("Metres the veil overhangs the view on every side, so a moving camera never shows its end.")]
        [Min(0f)] [SerializeField] private float _overscanMetres = 6f;

        [Tooltip("Texels across the front's soft edge.")]
        [Min(2)] [SerializeField] private int _rampTexels = 64;

        private static bool _installed;

        public static SquallVeil Instance { get; private set; }

        private SpriteRenderer _ramp, _solid;
        // The sprites this veil made (each on a texture it made), kept so they can be discarded with it.
        private Sprite _rampSprite, _solidSprite;

        /// <summary>True while the veil is drawn.</summary>
        public bool Showing { get; private set; }

        /// <summary>How much of the front is drawn (0..1), as last placed.</summary>
        public float ShownLoom01 { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("SquallVeil") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<SquallVeil>();
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            var group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = _sortingOrder;
            _rampSprite = BuildRamp(Mathf.Max(2, _rampTexels));
            _solidSprite = BuildSolid();
            _ramp = Part("front", _rampSprite);
            _solid = Part("body", _solidSprite);
        }

        private SpriteRenderer Part(string partName, Sprite sprite)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = _sortingOrder;
            sr.enabled = false;
            return sr;
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnCamera;

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnCamera;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // The two sprites and their textures were made here, so they go with the veil.
            if (_ramp != null) _ramp.sprite = null;
            if (_solid != null) _solid.sprite = null;
            _ramp = null;
            _solid = null;
            Release(ref _rampSprite);
            Release(ref _solidSprite);
        }

        private static void Release(ref Sprite made)
        {
            Sprite sprite = made;
            made = null;
            if (sprite == null) return;
            Texture2D texture = sprite.texture;
            Discard(sprite);
            if (texture != null) Discard(texture);
        }

        private static void Discard(Object made)
        {
            if (Application.isPlaying) Destroy(made);
            else DestroyImmediate(made);
        }

        private void LateUpdate() => Place(AmbientGlobals.ResolveCamera());

        private void OnCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != null && cam.cameraType == CameraType.Game) Place(cam);
        }

        /// <summary>Lay the veil over what <paramref name="cam"/> sees, from the squall now set.</summary>
        public void Place(Camera cam)
        {
            LocalSquall squall = LocalWeather.Squall;
            if (_ramp == null || cam == null || !cam.orthographic || squall.Loom01 <= 0f || squall.Darkness01 <= 0f)
            {
                Hide();
                return;
            }

            Vector2 toward = squall.Toward.sqrMagnitude > 0f ? squall.Toward.normalized : Vector2.right;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float along = HalfExtentAlong(toward, halfWidth, halfHeight);
            float across = HalfExtentAlong(new Vector2(-toward.y, toward.x), halfWidth, halfHeight);
            float feather = FeatherMetres(squall.Feather01, along);
            float front = FrontOffsetMetres(squall.Loom01, along, feather);

            Vector2 centre = cam.transform.position;
            Quaternion turn = Quaternion.Euler(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg);
            float breadth = 2f * (across + _overscanMetres);
            Color tint = squall.Tint;
            tint.a = squall.Darkness01;

            Vector2 rampAt = centre + toward * front;
            Transform ramp = _ramp.transform;
            ramp.SetPositionAndRotation(new Vector3(rampAt.x, rampAt.y, -_cameraZOffset), turn);
            ramp.localScale = new Vector3(feather, breadth, 1f);
            _ramp.color = tint;

            Vector2 solidAt = rampAt + toward * feather;
            Transform solid = _solid.transform;
            solid.SetPositionAndRotation(new Vector3(solidAt.x, solidAt.y, -_cameraZOffset), turn);
            solid.localScale = new Vector3(2f * along + _overscanMetres, breadth, 1f);
            _solid.color = tint;

            if (!_ramp.enabled) _ramp.enabled = true;
            if (!_solid.enabled) _solid.enabled = true;
            Showing = true;
            ShownLoom01 = squall.Loom01;
        }

        private void Hide()
        {
            if (_ramp != null && _ramp.enabled) _ramp.enabled = false;
            if (_solid != null && _solid.enabled) _solid.enabled = false;
            Showing = false;
            ShownLoom01 = 0f;
        }

        // ==== the geometry, pure (pinned by tests) =========================================================

        /// <summary>Half the view's reach along <paramref name="direction"/> (a unit vector), metres.</summary>
        public static float HalfExtentAlong(Vector2 direction, float halfWidth, float halfHeight) =>
            Mathf.Abs(direction.x) * halfWidth + Mathf.Abs(direction.y) * halfHeight;

        /// <summary>The length of the front's soft edge, metres: a share of the view along the way to
        /// the weather. Never zero, so the front is never a drawn line.</summary>
        public static float FeatherMetres(float feather01, float halfExtentAlong) =>
            Mathf.Max(0.01f, Mathf.Clamp01(feather01) * 2f * halfExtentAlong);

        /// <summary>
        /// Where the front's clear end stands, metres from the view's centre toward the weather. At loom
        /// 0 it is the view's far side (nothing is under the veil); at loom 1 the whole soft edge has
        /// passed the near side (everything is under it). Linear between, so the front crosses the view
        /// at an even pace.
        /// </summary>
        public static float FrontOffsetMetres(float loom01, float halfExtentAlong, float featherMetres) =>
            Mathf.Lerp(halfExtentAlong, -halfExtentAlong - featherMetres, Mathf.Clamp01(loom01));

        // ==== the two sprites (built once, one metre square each) ==========================================

        private static Sprite BuildRamp(int texels)
        {
            var tex = new Texture2D(texels, texels, TextureFormat.RGBA32, false, true)
            {
                name = "Squall.Front",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[texels * texels];
            for (int x = 0; x < texels; x++)
            {
                float t = x / (float)(texels - 1);
                byte a = (byte)Mathf.RoundToInt(t * t * (3f - 2f * t) * 255f);   // clear -> full, eased both ends
                for (int y = 0; y < texels; y++) px[y * texels + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, texels, texels), new Vector2(0f, 0.5f), texels, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildSolid()
        {
            const int Texels = 4;
            var tex = new Texture2D(Texels, Texels, TextureFormat.RGBA32, false, true)
            {
                name = "Squall.Body",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[Texels * Texels];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Texels, Texels), new Vector2(0f, 0.5f), Texels, 0, SpriteMeshType.FullRect);
        }
    }
}
