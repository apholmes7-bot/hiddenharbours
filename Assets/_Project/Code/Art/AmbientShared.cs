using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Shared, allocation-free plumbing for the living-coast ambient particle effects: reading the SAME global
    /// wind the grass/water read (<c>_WindWorld</c>), reading the global day/night MULTIPLY tint
    /// (<c>_DayNightTint</c>), and building the soft point-filtered sprites every effect draws. Centralised so
    /// sea mist, smoke, gulls and motes all read the EXACT same shared signals (cohesion) and share one tiny
    /// crisp-pixel sprite per shape (batched — rule 7). All READ-ONLY of the globals (rule 4); these systems
    /// drive no sim and save nothing (rule 5).
    /// </summary>
    public static class AmbientGlobals
    {
        private static readonly int IdWindWorld   = Shader.PropertyToID("_WindWorld");
        private static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");

        /// <summary>
        /// The shared scene wind as the grass/water see it — the global <c>_WindWorld</c> (direction · 0..1
        /// strength) published by <see cref="GrassWindBridge"/> (or the dev test-wind). READ ONLY. When no one
        /// has published it yet this is <see cref="Vector2.zero"/> (dead calm) — the ambient effects then just
        /// creep on their own base-drift, never NaN.
        /// </summary>
        public static Vector2 Wind
        {
            get
            {
                Vector4 w = Shader.GetGlobalVector(IdWindWorld);
                return new Vector2(w.x, w.y);
            }
        }

        /// <summary>
        /// The global day/night MULTIPLY tint published by <see cref="DayNightController"/> — READ ONLY. When
        /// the controller hasn't pushed it yet (EditMode / pre-boot) the global is unset; we treat an
        /// all-zero/near-black read as "no cycle yet → plain daylight" (white) so a bare scene doesn't render
        /// the ambient particles as if it were the dead of night.
        /// </summary>
        public static Color DayNightTint
        {
            get
            {
                Color c = Shader.GetGlobalColor(IdDayNightTint);
                // Unset global reads as (0,0,0,0). The real tint is never fully black, so treat a near-zero
                // read as "no cycle installed" → daylight, not midnight.
                if (c.r + c.g + c.b <= 1e-4f) return Color.white;
                return c;
            }
        }

        /// <summary>The current day/night brightness 0..1 (luminance of the tint) — convenience.</summary>
        public static float Brightness => AmbientParticleMath.DayNightBrightness(DayNightTint);

        /// <summary>
        /// Resolve the active camera (MainCamera, else the first enabled camera). The ambient emitters keep
        /// their particle field centred on the camera so a few dozen particles always cover what the player
        /// sees, scene-wide, without populating the whole region. Null when there is no camera.
        /// </summary>
        public static Camera ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            var all = Camera.allCameras;
            return (all != null && all.Length > 0) ? all[0] : null;
        }

        // ==== procedural soft sprites (point-filtered, crisp pixel-art; one shared sprite per shape) =======

        /// <summary>
        /// A soft round puff (used by mist + smoke): opaque-ish core feathered to a transparent rim, on a
        /// point-filtered power-of-two texture so it stays crisp pixel-art when scaled. One shared instance per
        /// effect → every particle batches. <paramref name="softness"/> in 0..1 widens the feather (mist is
        /// softer than smoke).
        /// </summary>
        public static Sprite BuildSoftPuff(string name, int size, int ppu, float softness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f;
            float soft = Mathf.Clamp01(softness);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / r, dy = (y - c) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);     // 0 centre .. 1 edge
                float a = Mathf.Clamp01(1f - d);
                // Soften: a higher softness keeps the falloff gentle (mist), lower tightens the core (smoke).
                a = Mathf.Pow(a, Mathf.Lerp(2f, 0.8f, soft));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
        }

        /// <summary>
        /// A tiny crisp dot (used by dust motes): a small solid round speck, point-filtered. One shared
        /// instance → every mote batches.
        /// </summary>
        public static Sprite BuildDot(string name, int size, int ppu)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / r, dy = (y - c) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)(d <= 0.85f ? 255 : (d <= 1f ? 128 : 0));
                px[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
        }

        /// <summary>
        /// A small "M"-silhouette gull (the classic two-arc seagull glyph), point-filtered, drawn pointing
        /// +x (east) so <see cref="GullFlock"/> can rotate it to its heading. One shared sprite → the flock
        /// batches. Pixel-art faithful: a few solid pixels, no anti-aliasing beyond the shape.
        /// </summary>
        public static Sprite BuildGull(string name, int ppu)
        {
            const int W = 16, H = 8;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[W * H];
            // start transparent
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);

            // Two gently-arced wings meeting at the centre (a shallow "M" / gull silhouette).
            // Plot from the centre out along each wing with a slight upward then downward arc.
            void Plot(int x, int y)
            {
                if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = new Color32(255, 255, 255, 255);
            }
            int cx = W / 2;
            int baseY = 3;
            for (int k = 0; k <= 6; k++)
            {
                // arc height: rises then dips toward the wingtip
                int dy = Mathf.RoundToInt(Mathf.Sin(k / 6f * Mathf.PI) * 2.2f);
                Plot(cx + k, baseY + dy);
                Plot(cx - k, baseY + dy);
                // a touch of thickness near the body
                if (k <= 2) { Plot(cx + k, baseY + dy - 1); Plot(cx - k, baseY + dy - 1); }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            // Pointing +x: the sprite is symmetric, but heading-rotation still reads as "facing travel".
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), ppu);
        }
    }
}
