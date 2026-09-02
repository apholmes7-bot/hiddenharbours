using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The SEABED the sea is drawing, published as shader globals for consumers that are not the water
    /// material (ADR 0040 rev 3: the foam buffer's bore deposit; the <c>_HHSeaLevelWorld</c> waterline's
    /// sibling). ONE publisher — <see cref="WaterSurface"/>, at the moment it feeds its own
    /// <c>_HeightTex</c> — so no consumer can read a bed the water is not drawing, and the same decode
    /// (<c>lerp(min, max, r)</c>) on the same texture over the same rect.
    ///
    /// <para><c>_HHSeabedRange.w</c> is the "bound at all" flag; unset = everywhere deep, the water
    /// shader's own no-height-map contract, so an idle scene breaks nowhere. Additively loaded regions
    /// each publish on their own feed; the last one wins, which is the region whose water was set up
    /// last — the camera-windowed buffer only ever reads the one under the player.</para>
    /// </summary>
    public static class SeabedGlobals
    {
        public static readonly int Tex = Shader.PropertyToID("_HHSeabedTex");
        public static readonly int Rect = Shader.PropertyToID("_HHSeabedRect");
        public static readonly int Range = Shader.PropertyToID("_HHSeabedRange");

        private static Texture2D s_Fallback;

        /// <summary>The seabed most recently published, for C# readers that want the same rect.</summary>
        public static bool IsBound { get; private set; }

        public static void Publish(Texture heightTex, Vector2 worldMin, Vector2 worldSize,
                                   float minElevation, float maxElevation, float shoreSampleStep)
        {
            if (heightTex == null) { PublishUnset(); return; }
            Shader.SetGlobalTexture(Tex, heightTex);
            Shader.SetGlobalVector(Rect, new Vector4(worldMin.x, worldMin.y,
                                                     Mathf.Max(worldSize.x, 1e-3f), Mathf.Max(worldSize.y, 1e-3f)));
            Shader.SetGlobalVector(Range, new Vector4(minElevation, maxElevation, Mathf.Max(shoreSampleStep, 1e-3f), 1f));
            IsBound = true;
        }

        public static void PublishUnset()
        {
            if (s_Fallback == null)
            {
                s_Fallback = new Texture2D(1, 1, TextureFormat.R8, false, true) { name = "HH Seabed (unset)", hideFlags = HideFlags.HideAndDontSave };
                s_Fallback.SetPixel(0, 0, Color.black);
                s_Fallback.Apply(false, true);
            }
            Shader.SetGlobalTexture(Tex, s_Fallback);
            Shader.SetGlobalVector(Rect, Vector4.zero);
            Shader.SetGlobalVector(Range, Vector4.zero);
            IsBound = false;
        }
    }
}
