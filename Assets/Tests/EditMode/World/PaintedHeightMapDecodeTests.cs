using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// (ADR 0014) The TEXTURE → field decode path that the pure <see cref="PaintedHeightFieldTests"/> can't
    /// reach: it builds a REAL CPU-readable <see cref="Texture2D"/>, runs it through
    /// <see cref="PaintedHeightMap"/>'s decode (via <c>Field</c> / <see cref="PaintedHeightMap.Rebuild"/>),
    /// and pins the two invariants the painted-seabed P1 rule (render == sim) depends on:
    /// <list type="bullet">
    /// <item><description><b>F4 — y-orientation.</b> <see cref="Texture2D.GetPixels"/> row order ↔ the
    /// field's index ↔ world-Y must agree, so what the owner paints LOW on the map bares LOW in the world
    /// (and the shader, sampling the same texture with the same world→uv mapping, draws it there too). A
    /// silent row-flip would make paint ≠ sail.</description></item>
    /// <item><description><b>F5 — negative path.</b> A null OR non-readable texture must decode to a null
    /// <c>Field</c> (never throw), and <see cref="PaintedTidalTerrain"/> must then return its
    /// <c>_fallbackElevation</c> — guarding the CPU-readability gotcha against a future import regression CI
    /// can't catch from a scene.</description></item>
    /// </list>
    /// </summary>
    public class PaintedHeightMapDecodeTests
    {
        private const float Eps = 1e-3f;

        // A 192×120 m rect centred at origin (the canon St Peters bake frame) so low-Y ≈ -60, high-Y ≈ +60.
        private static readonly Vector2 Center = Vector2.zero;
        private static readonly Vector2 Size = new Vector2(160f, 120f);
        private const float MinElev = -4f, MaxElev = 6f;

        /// <summary>
        /// Build a CPU-readable R8 height texture with an ASYMMETRIC top/bottom: the BOTTOM row R=0 (→ min
        /// elevation) and the TOP row R=1 (→ max), so a vertical (y) flip in the decode would be observable.
        /// GetPixels is row-major y-outer with row 0 at the BOTTOM, matching PaintedHeightField's index.
        /// </summary>
        private static Texture2D MakeAsymmetricTopBottom(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.R8, false, true) { name = "TestHeight" };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float r = y == 0 ? 0f : (y == h - 1 ? 1f : (float)y / (h - 1));
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(r, r, r, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(false, false);   // keepReadable = true
            return tex;
        }

        private static PaintedHeightMap MakeMap(Texture2D tex)
        {
            var map = ScriptableObject.CreateInstance<PaintedHeightMap>();
            var so = new UnityEditor.SerializedObject(map);
            so.FindProperty("_heightTexture").objectReferenceValue = tex;
            so.FindProperty("_worldCenter").vector2Value = Center;
            so.FindProperty("_worldSize").vector2Value = Size;
            so.FindProperty("_minElevation").floatValue = MinElev;
            so.FindProperty("_maxElevation").floatValue = MaxElev;
            so.ApplyModifiedPropertiesWithoutUndo();
            map.Rebuild();
            return map;
        }

        // ---- F4: paint==sail y-orientation — low-Y reads min, high-Y reads max ----------------------------

        [Test]
        public void Decode_YOrientation_LowYReadsBottomRow_HighYReadsTopRow()
        {
            var tex = MakeAsymmetricTopBottom(8, 8);
            var map = MakeMap(tex);
            try
            {
                Assert.IsNotNull(map.Field, "a readable texture decodes to a non-null field");

                // Centre column; low-Y near the bottom edge, high-Y near the top edge (inside the rect).
                float yLow = Center.y - Size.y * 0.5f + 1f;   // ≈ -59
                float yHigh = Center.y + Size.y * 0.5f - 1f;  // ≈ +59

                float low = map.Field.ElevationAt(new Vector2(0f, yLow));
                float high = map.Field.ElevationAt(new Vector2(0f, yHigh));

                Assert.Less(low, high, "low-Y must read SHALLOWER/deeper-min than high-Y (no row flip)");
                Assert.AreEqual(MinElev, low, 0.5f, "bottom row (R=0) → ~min elevation at low world-Y");
                Assert.AreEqual(MaxElev, high, 0.5f, "top row (R=1) → ~max elevation at high world-Y");
            }
            finally
            {
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void Decode_RowIndex_MatchesFieldIndex_MonotonicInY()
        {
            // Stronger pin: sample up the centre column and assert elevation is non-decreasing with world-Y
            // (the texture rises bottom→top), so the GetPixels row order and the field index can't disagree.
            var tex = MakeAsymmetricTopBottom(16, 16);
            var map = MakeMap(tex);
            try
            {
                float prev = float.NegativeInfinity;
                for (int i = 0; i <= 10; i++)
                {
                    float wy = Center.y - Size.y * 0.5f + 2f + i * (Size.y - 4f) / 10f;
                    float e = map.Field.ElevationAt(new Vector2(0f, wy));
                    Assert.GreaterOrEqual(e, prev - Eps, $"elevation must rise with world-Y (y={wy})");
                    prev = e;
                }
            }
            finally
            {
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(map);
            }
        }

        // ---- F5: negative path — null / non-readable texture → null Field, fallback elevation -------------

        [Test]
        public void Decode_NullTexture_FieldIsNull()
        {
            var map = ScriptableObject.CreateInstance<PaintedHeightMap>();   // no texture wired
            try
            {
                Assert.IsNull(map.Field, "no texture → null decoded field (no throw)");
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void PaintedTidalTerrain_WithNullMap_ReturnsFallbackElevation()
        {
            var go = new GameObject("PaintedTidalTerrainTest");
            var terrain = go.AddComponent<PaintedTidalTerrain>();   // Map left null
            try
            {
                // Default _fallbackElevation is well below tide (open/deep water) — read it back to compare.
                var so = new UnityEditor.SerializedObject(terrain);
                float fallback = so.FindProperty("_fallbackElevation").floatValue;
                Assert.AreEqual(fallback, terrain.ElevationAt(new Vector2(3f, -7f)), Eps,
                    "null map → terrain reports the fallback elevation, never throws");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PaintedTidalTerrain_WithNonReadableTextureMap_ReturnsFallbackElevation()
        {
            // A GPU-only (non-readable) texture: PaintedHeightMap.Decode logs + returns null, so the terrain
            // must fall back rather than sample garbage — guards the CPU-readability import gotcha.
            var gpuTex = new Texture2D(8, 8, TextureFormat.R8, false, true);
            gpuTex.Apply(true, true);   // makeNoLongerReadable = true → isReadable == false

            var map = MakeMap(gpuTex);
            var go = new GameObject("PaintedTidalTerrainNonReadable");
            var terrain = go.AddComponent<PaintedTidalTerrain>();
            terrain.Map = map;
            map.Rebuild();
            try
            {
                Assert.IsFalse(gpuTex.isReadable, "precondition: the texture is non-readable");
                Assert.IsNull(map.Field, "non-readable texture → null field (treated as open water)");

                var so = new UnityEditor.SerializedObject(terrain);
                float fallback = so.FindProperty("_fallbackElevation").floatValue;
                Assert.AreEqual(fallback, terrain.ElevationAt(Vector2.zero), Eps,
                    "non-readable map → terrain reports the fallback elevation");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(map);
                Object.DestroyImmediate(gpuTex);
            }
        }
    }
}
