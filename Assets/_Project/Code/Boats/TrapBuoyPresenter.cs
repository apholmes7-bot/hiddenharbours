using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The <b>visual</b> half of a placed trap (trap-fishing arc Build 3): it drops a wave-driven
    /// <see cref="BuoyWaveVisual"/> at every set trap and clears it when the trap is hauled, so a trap on
    /// the seabed reads as a bobbing buoy on the surface (the Build-1 prop, now driven by gameplay).
    ///
    /// <para><b>Why here, and one-way through Core.</b> The buoy is a Boats-lane visual; the logical trap
    /// (soak, catch, save) is a Fishing-lane runtime. Neither module references the other — Fishing
    /// publishes the Core <see cref="TrapPlaced"/>/<see cref="TrapRemoved"/> signals, this reacts, exactly
    /// the one-way handoff <c>OwnedFleet</c> uses off <see cref="BoatPurchased"/>. Core stays engine-light
    /// (the signal carries a position + a stable instance id, never a GameObject handle), and this presenter
    /// turns that into the buoy prop. Keyed by <see cref="TrapPlaced.InstanceId"/> so a haul removes exactly
    /// the right buoy.</para>
    ///
    /// <para><b>Self-installing, removable (the project's visual-scaffold convention, ADR 0011 —
    /// the retired Build-1 <c>BuoyGreyboxSpawner</c> decor established it; its three look-at demo buoys
    /// were deleted in Build 5 once real player-placed buoys existed, because the owner tried to haul
    /// them).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> host subscribes at boot; buoys live under its own plain
    /// root and never touch authored/painted content. Visual-only: drives no sim, saves nothing, so on a
    /// save/load the buoys are re-created from the restored traps' fresh <see cref="TrapPlaced"/> signals —
    /// no buoy state is persisted (rule 5). The greybox buoy sprite is generated in code (no asset
    /// dependency), a stand-in until the real LobsterBuoy art wires in.</para>
    /// </summary>
    public sealed class TrapBuoyPresenter : MonoBehaviour
    {
        // Draw ABOVE the Sea plane (St Peters' Sea is order -5), the water-surface-prop order the
        // LobsterBuoy decor used, so the buoy sits ON the water in front of the surface.
        private const int BuoySortingOrder = 3;

        private static TrapBuoyPresenter _instance;

        private readonly Dictionary<string, GameObject> _buoys = new();
        private Sprite _sprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("[TrapBuoyPresenter]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrapBuoyPresenter>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            EventBus.Subscribe<TrapPlaced>(OnTrapPlaced);
            EventBus.Subscribe<TrapRemoved>(OnTrapRemoved);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TrapPlaced>(OnTrapPlaced);
            EventBus.Unsubscribe<TrapRemoved>(OnTrapRemoved);
            if (_instance == this) _instance = null;
        }

        private void OnTrapPlaced(TrapPlaced e)
        {
            if (string.IsNullOrEmpty(e.InstanceId) || _buoys.ContainsKey(e.InstanceId)) return;

            if (_sprite == null) _sprite = BuildGreyboxBuoySprite();

            var go = new GameObject("TrapBuoy_" + e.InstanceId);
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = new Vector3(e.PosX, e.PosY, 0f);

            // A CHILD the buoy BOBS (world +Y) while the field samples at the fixed root — the
            // body-vs-visual separation BuoyWaveVisual/BoatWaveMotion require.
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(go.transform, worldPositionStays: false);

            var sr = visualGo.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingOrder = BuoySortingOrder;

            var buoy = go.AddComponent<BuoyWaveVisual>();
            buoy.Configure(sr, visualGo.transform);

            _buoys[e.InstanceId] = go;
        }

        private void OnTrapRemoved(TrapRemoved e)
        {
            if (string.IsNullOrEmpty(e.InstanceId)) return;
            if (_buoys.TryGetValue(e.InstanceId, out var go))
            {
                _buoys.Remove(e.InstanceId);
                if (go != null) Destroy(go);
            }
        }

        /// <summary>
        /// A tiny greybox lobster-buoy silhouette in code (16×32, 32 PPU ⇒ ~0.5×1 m), BOTTOM-CENTRE pivot so
        /// the reused PlayerSubmerge shader clips the waterline from the base up. One shared sprite → the
        /// buoys batch (rule 7). Replaced by the real LobsterBuoy art when the trap arc's art handoff lands.
        /// </summary>
        private static Sprite BuildGreyboxBuoySprite()
        {
            const int W = 16, H = 32, ppu = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true)
            {
                name = "TrapBuoyGreybox",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            var floatTop = new Color32(230, 200, 40, 255);    // a yellow float (reads distinct from the decor buoy)
            var floatBand = new Color32(30, 30, 40, 255);     // a dark band
            var spar = new Color32(120, 95, 70, 255);

            void Plot(int x, int y, Color32 c)
            {
                if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = c;
            }

            int cx = W / 2;
            for (int y = 14; y <= 30; y++)
            {
                float t = Mathf.InverseLerp(14f, 30f, y);
                int half = Mathf.RoundToInt(Mathf.Lerp(5f, 2f, Mathf.Abs(t - 0.45f) * 1.8f));
                half = Mathf.Clamp(half, 1, 5);
                Color32 c = (y >= 21 && y <= 23) ? floatBand : floatTop;
                for (int x = cx - half; x < cx + half; x++) Plot(x, y, c);
            }
            for (int y = 0; y <= 16; y++)
            {
                Plot(cx - 1, y, spar);
                Plot(cx, y, spar);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), ppu);
        }
    }
}
