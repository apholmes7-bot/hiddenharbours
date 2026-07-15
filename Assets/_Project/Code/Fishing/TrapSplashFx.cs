using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The pooled one-shot <b>splash burst</b> for the trap loop — the owner's painted haul-break flipbook
    /// (trap-kit art) played once at a world position: when a pot breaks the surface at haul-end
    /// (<see cref="TrapHaulController"/>) and when a fresh set hits the water
    /// (<see cref="PlacedTrapService"/>). WHAT plays is data on the trap
    /// (<see cref="TrapDef.SplashBurstFrames"/> / <see cref="TrapDef.SplashBurstFps"/> — rules 2+6);
    /// this is only the pooled player.
    ///
    /// <para><b>Pattern.</b> The <c>WadeSplashEmitter</c> pooled-burst convention, minus the signal
    /// subscription: both trigger moments live in THIS module (Fishing), so the publishers call
    /// <see cref="Play"/> directly — no new Core signal, no cross-module reach. The host self-creates on
    /// first use (hidden, DDOL — the visual-scaffold rule: drives no sim, saves nothing, authored scenes
    /// untouched). Rule 7: a fixed round-robin pool of renderers built once; <see cref="Update"/>
    /// early-outs when nothing is live and allocates nothing.</para>
    ///
    /// <para><b>Sorting.</b> A SortingGroup ("sort as 2D") + a small camera-ward z nudge so the sprites
    /// reliably clear the water MeshRenderer (the #134 quirk the wade splashes solved); order sits just
    /// above the trap buoy (order 3) so the burst wraps the buoy it breaks around.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Purely cosmetic: frame stepping uses render time via the pure
    /// <see cref="FlipbookMath"/>, and nothing here reads or writes sim/saved state.</para>
    /// </summary>
    public sealed class TrapSplashFx : MonoBehaviour
    {
        // How many bursts can be airborne at once (round-robin; the oldest is stolen past this). A pool
        // bound, not a balance number — haul-end and a re-set are seconds apart, so 4 is generous.
        private const int PoolSize = 4;
        // Draw just ABOVE the trap buoy (TrapBuoyPresenter draws at 3) so the burst breaks around it.
        private const int SplashSortingOrder = 4;
        // Metres toward the camera (−z) so the sprite clears the water plane's MeshRenderer — kept small,
        // the WadeSplashEmitter convention. A render-layering constant, not a balance number.
        private const float CameraZOffset = 0.25f;

        private struct Burst
        {
            public bool Alive;
            public Sprite[] Frames;
            public float Fps;
            public float Age;
        }

        private static TrapSplashFx _instance;

        private readonly Burst[] _pool = new Burst[PoolSize];
        private SpriteRenderer[] _renderers;
        private int _cursor;
        private int _liveCount;

        /// <summary>Live bursts right now (tests / diagnostics).</summary>
        public int ActiveCount => _liveCount;

        /// <summary>
        /// Play a one-shot splash flipbook at <paramref name="at"/>. Null/empty <paramref name="frames"/>
        /// or a non-positive <paramref name="fps"/> is a silent no-op — the greybox (no splash authored)
        /// behaviour, the empty-art-slot rule everywhere in the trap arc. Event-time only, never per frame.
        /// </summary>
        public static void Play(Sprite[] frames, float fps, Vector2 at)
        {
            if (frames == null || frames.Length == 0 || fps <= 0f) return;
            if (!Application.isPlaying) return;   // EditMode tests build Defs; no FX host outside play

            if (_instance == null) Install();
            _instance.Emit(frames, fps, at);
        }

        private static void Install()
        {
            var host = new GameObject("[TrapSplashFx]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<TrapSplashFx>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            // "Sort as 2D" so the pool sorts by sortingOrder against sprite content instead of world-z —
            // without it a MeshRenderer water plane wins (the URP-2D quirk the wade splashes document).
            var group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = SplashSortingOrder;

            _renderers = new SpriteRenderer[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("splash");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SplashSortingOrder;
                sr.enabled = false;
                _renderers[i] = sr;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Emit(Sprite[] frames, float fps, Vector2 at)
        {
            int slot = _cursor;
            _cursor = (_cursor + 1) % PoolSize;

            if (!_pool[slot].Alive) _liveCount++;
            _pool[slot] = new Burst { Alive = true, Frames = frames, Fps = fps, Age = 0f };

            SpriteRenderer sr = _renderers[slot];
            sr.transform.position = new Vector3(at.x, at.y, -CameraZOffset);
            sr.sprite = frames[0];
            sr.enabled = true;
        }

        private void Update()
        {
            if (_liveCount == 0) return;   // idle pool costs one branch (rule 7)

            float dt = Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!_pool[i].Alive) continue;
                _pool[i].Age += dt;

                int frame = FlipbookMath.OneShotFrame(_pool[i].Age, _pool[i].Fps, _pool[i].Frames.Length);
                if (frame < 0)
                {
                    _pool[i].Alive = false;
                    _pool[i].Frames = null;
                    _liveCount--;
                    _renderers[i].enabled = false;
                }
                else
                {
                    _renderers[i].sprite = _pool[i].Frames[frame];
                }
            }
        }
    }
}
