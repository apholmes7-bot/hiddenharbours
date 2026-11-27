using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The calm on the ground's rock at a cliff's foot (owner 11 27: the flat ledge where the cliffs meet the floor
    /// is hard to tell from the face; it has high contrast and saturation).
    ///
    /// <para>The ground's shelf, talus and ledge rock are drawn in the same red beds as the face above them, at full
    /// colour and with dark cracks, so at a glance the floor reads as more wall. The calm takes some of that rock's
    /// colour away and draws its light-to-dark spread in toward a pivot, so the face stands out as the stronger,
    /// warmer thing and the ledge lies down under it. A function of the texel's own colour alone: every pixel of a
    /// texel takes the same answer, so it adds no edge and no new pixel size. Strength 0 leaves the ground exactly as
    /// it was.</para>
    ///
    /// <para>Pure. Twin of the HiddenHarboursTerrainSplat shader's TerrainLedgeCalm (the region's numbers reach it as
    /// _ShoreLedgeCalm from <see cref="CliffFaceMixDef"/>).</para>
    /// </summary>
    public static class TerrainLedgeCalmMath
    {
        /// <summary>Rec. 709 luma weights, the shader's.</summary>
        public static readonly Vector3 Luma = new Vector3(0.2126f, 0.7152f, 0.0722f);

        /// <summary>The shader's _ShoreLedgeCalm: strength, saturation kept, contrast kept, pivot.</summary>
        public static Vector4 Shader(CliffFaceMixDef mix) => mix == null
            ? Vector4.zero
            : new Vector4(Mathf.Clamp01(mix.LedgeCalm), Mathf.Clamp01(mix.LedgeSaturationKeep),
                          Mathf.Clamp01(mix.LedgeContrastKeep), Mathf.Clamp01(mix.LedgeCalmPivot));

        /// <summary>The calmed colour of a ground texel whose rock weight (shelf, talus and ledge together) is
        /// <paramref name="rock"/>, under <paramref name="calm"/> (<see cref="Shader"/>).</summary>
        public static Vector3 Calm(Vector3 col, float rock, Vector4 calm)
        {
            float k = Mathf.Clamp01(calm.x) * Mathf.Clamp01(rock);
            if (k <= 0f) return col;
            float l = Vector3.Dot(col, Luma);
            Vector3 grey = new Vector3(l, l, l);
            Vector3 muted = Vector3.LerpUnclamped(grey, col, calm.y);
            float target = Mathf.LerpUnclamped(calm.w, l, calm.z);
            Vector3 drawn = l > 1e-4f ? muted * (target / l) : new Vector3(target, target, target);
            return Vector3.LerpUnclamped(col, drawn, k);
        }
    }
}
