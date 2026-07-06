using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// GREYBOX TEST PLACEMENT for the wave-driven buoy (Build 1 of the trap arc) — drops a few
    /// <see cref="BuoyWaveVisual"/> props in visible open water off the St Peters south slip, in a
    /// spot the player sails past early, so the owner can go look at the bob / partial-submerge /
    /// vanish-under-a-crest. <b>Test scaffold only</b>: no gameplay, no traps, no placement mechanic —
    /// just the visual, dropped where it's easy to see.
    ///
    /// <para><b>Least-invasive by design (ADR 0011 painted-layer boundary).</b> Self-installing like
    /// the project's other visual components (<c>WaveFieldBridge</c> / <c>PlayerSubmergeVisual</c> /
    /// <c>BuyPointInstaller</c>): a <see cref="RuntimeInitializeOnLoadMethod"/> host waits for the St
    /// Peters scene and spawns the buoys at fixed WORLD offsets under its own plain root — it NEVER
    /// touches the scene file, the builder, the owner's authored/painted content, or the
    /// <c>--LOGIC--</c> root. Trivially removable: delete this one file and the buoys are gone (and
    /// swapping in the real <c>LobsterBuoy</c> art / a placement system later replaces it wholesale).</para>
    ///
    /// <para>The greybox buoy sprite is generated in code (the <c>BoatWakeEmitter</c>/<c>AmbientShared</c>
    /// convention for greybox props — no asset dependency, no import order to trip over) with a
    /// BOTTOM-CENTRE pivot so the reused PlayerSubmerge shader's <c>uv.y = 0</c> waterline sits at the
    /// buoy's base and the resting draught lands right. Visual-only; drives no sim, saves nothing.</para>
    /// </summary>
    public static class BuoyGreyboxSpawner
    {
        // Only spawn in the opening/start region — the buoys are a look-at for the St Peters sail-past.
        private const string TargetSceneName = "StPeters";

        // Test buoy positions: OPEN WATER off the island's SOUTH coast, around the moored-dory slip at
        // (-40, -26) — deep harbour (elevation -4, always well underwater), the water the player sails as
        // soon as the dory is theirs. Spread a little so a passing crest hits them out of phase. Kept clear
        // of the dock zone itself (radius 3.5 at (-40,-26)) so they don't clutter the board/disembark spot.
        private static readonly Vector3[] BuoyPositions =
        {
            new Vector3(-33f, -24f, 0f),   // east of the slip
            new Vector3(-40f, -31f, 0f),   // further out to sea, dead ahead as you leave
            new Vector3(-47f, -25f, 0f),   // west of the slip
        };

        // Sort ABOVE the Sea plane (St Peters' Sea is sortingOrder -5) and match the existing
        // LobsterBuoy decor prefab (order 3) so the buoy draws ON the water, in front of the surface —
        // the URP-2D sprite-vs-sprite sorting the other water-surface props use.
        private const int BuoySortingOrder = 3;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawnFor(SceneManager.GetActiveScene());   // the first scene is already up at AfterSceneLoad
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TrySpawnFor(scene);

        private static bool _spawned;

        private static void TrySpawnFor(Scene scene)
        {
            if (_spawned) return;                          // one greybox set for the session
            if (scene.name != TargetSceneName) return;     // only the St Peters sail-past
            _spawned = true;

            var root = new GameObject("GreyboxTestBuoys [visual test — removable]");
            // Do NOT parent under any authored/painted root — a plain scene-root object (ADR 0011).

            Sprite sprite = BuildGreyboxBuoySprite();
            for (int i = 0; i < BuoyPositions.Length; i++)
                SpawnBuoy(root.transform, sprite, BuoyPositions[i], i);
        }

        private static void SpawnBuoy(Transform parent, Sprite sprite, Vector3 position, int index)
        {
            var go = new GameObject("GreyboxBuoy_" + index);
            go.transform.SetParent(parent, worldPositionStays: true);
            go.transform.position = position;

            // A CHILD visual the buoy BOBS (world +Y), so the field is always sampled at the fixed root —
            // the sample point must not ride the bob (BoatWaveMotion's separation of body vs visual).
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(go.transform, worldPositionStays: false);

            var sr = visualGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = BuoySortingOrder;

            var buoy = go.AddComponent<BuoyWaveVisual>();
            buoy.Configure(sr, visualGo.transform);
        }

        /// <summary>
        /// A tiny greybox lobster-buoy silhouette in code (16×32, 32 PPU ⇒ ~0.5×1 m): a coloured float
        /// on a thin spar, BOTTOM-CENTRE pivot so the reused PlayerSubmerge shader clips the waterline
        /// from the base (uv.y = 0) up. Point-filtered, one shared sprite → the buoys batch (rule 7).
        /// Purely a stand-in for the LobsterBuoy art until the trap-fishing arc wires the real prop.
        /// </summary>
        private static Sprite BuildGreyboxBuoySprite()
        {
            const int W = 16, H = 32, ppu = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true)
            {
                name = "GreyboxBuoy",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);   // transparent

            var floatTop = new Color32(220, 70, 60, 255);     // the classic red/orange buoy float
            var floatBand = new Color32(240, 235, 220, 255);  // a pale band (readable at a glance)
            var spar = new Color32(120, 95, 70, 255);         // the wooden spar below

            void Plot(int x, int y, Color32 c)
            {
                if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = c;
            }

            int cx = W / 2;
            // Float: a rounded body in the upper half (y 14..30), tapering top and bottom.
            for (int y = 14; y <= 30; y++)
            {
                float t = Mathf.InverseLerp(14f, 30f, y);           // 0 base of float .. 1 top
                int half = Mathf.RoundToInt(Mathf.Lerp(5f, 2f, Mathf.Abs(t - 0.45f) * 1.8f));
                half = Mathf.Clamp(half, 1, 5);
                Color32 c = (y >= 21 && y <= 23) ? floatBand : floatTop;   // the pale band across the middle
                for (int x = cx - half; x < cx + half; x++) Plot(x, y, c);
            }
            // Spar: a thin post from the water base (y 0) up into the float (y 16).
            for (int y = 0; y <= 16; y++)
            {
                Plot(cx - 1, y, spar);
                Plot(cx, y, spar);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            // BOTTOM-CENTRE pivot (0.5, 0): the shader anchors the waterline at uv.y = 0 (sprite base),
            // and a floating prop should sit ON the water from its base — so base-pivot reads right.
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), ppu);
        }
    }
}
