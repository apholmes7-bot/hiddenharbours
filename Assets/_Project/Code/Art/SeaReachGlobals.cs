using HiddenHarbours.Core;
using Unity.Collections;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// THE SEA'S REACH, published (owner, 2026-11-19: "once they arent attached to the waves they should
    /// remain still pools of water"). Builds the region's <see cref="SeaReachField"/> ONCE from the seabed
    /// the water is drawing, hands it to the Core seam (<see cref="SeaReach"/>: the CPU swell readers) and
    /// binds the same sills as shader globals for <c>Include/SeaReach.hlsl</c> (the water and the foam
    /// buffer's deposit), so the GPU and the CPU judge one map.
    ///
    /// <para>ONE publisher, <see cref="WaterSurface"/>, at the moment it publishes its seabed
    /// (<see cref="SeabedGlobals"/>), so the map is always the bed the water is drawing. The flood runs at
    /// that moment only, and again only if the seabed, its rect or the settings change (a cache key), never
    /// per frame and never at a tide step: the tide moves, the sills do not. Unset binds a 1x1 fallback with
    /// the bound flag 0, and every shader factor is exactly 1 (the older picture).</para>
    ///
    /// <para>Cost: the flood is O(cells + buckets) at load (a few arrays per cell); afterwards the field holds
    /// two floats per cell on the CPU (sill, floor) and two halves per cell on the GPU, and a read is four
    /// cell lookups.</para>
    /// </summary>
    public static class SeaReachGlobals
    {
        public static readonly int Tex = Shader.PropertyToID("_HHSeaReachTex");
        public static readonly int Rect = Shader.PropertyToID("_HHSeaReachRect");
        public static readonly int Params = Shader.PropertyToID("_HHSeaReachParams");
        public static readonly int Wet = Shader.PropertyToID("_HHSeaReachWet");

        private static Texture2D s_Fallback;
        private static Texture2D s_Texture;
        private static object s_Owner;
        private static SeaReachField s_Field;

        // The cache key of the published map: the same seabed, rect and settings never re-flood.
        private static int s_KeySource;
        private static int s_KeyRevision;
        private static Vector4 s_KeyRect;
        private static Vector2 s_KeyRange;
        private static SeaReachSettings s_KeySettings;

        /// <summary>True while a sea-reach map is bound.</summary>
        public static bool IsBound => s_Field != null;

        /// <summary>The field currently bound (null when unset); for diagnostics and tests.</summary>
        public static SeaReachField Field => s_Field;

        /// <summary>
        /// Publish from the painted seabed texture <paramref name="heightTex"/> (decoded
        /// <c>lerp(min, max, r)</c>, the water's own decode) over the world rect. A texture the CPU cannot
        /// read, or a seabed with no open-sea seed, releases this owner's map instead (the older look).
        /// </summary>
        public static void PublishPainted(object owner, Texture heightTex, Vector2 worldMin, Vector2 worldSize,
                                          float minElevation, float maxElevation)
        {
            if (owner == null) return;
            var settings = GameServices.SeaReachConfig;
            var t = heightTex as Texture2D;
            if (t == null || !t.isReadable || !settings.IsUsable) { Release(owner); return; }

            var rect = new Vector4(worldMin.x, worldMin.y, worldSize.x, worldSize.y);
            var range = new Vector2(minElevation, maxElevation);
            int source = t.GetHashCode();   // the texture object's identity (with its updateCount below)
            int revision = (int)t.updateCount;
            if (IsCached(owner, source, revision, rect, range, settings)) { Rebind(owner); return; }

            float[] elevations = Decode(t, minElevation, maxElevation);
            if (elevations == null) { Release(owner); return; }
            Bind(owner, SeaReachField.Build(elevations, t.width, t.height, worldMin, worldSize, settings), settings,
                 source, revision, rect, range);
        }

        /// <summary>
        /// Publish from a baked elevation grid (metres, row-major, row 0 at the bottom of the rect). The grid
        /// must be real terrain heights: a distance-to-land stand-in has no sills and must not be passed.
        /// </summary>
        public static void PublishElevations(object owner, float[] elevations, int width, int height,
                                             Vector2 worldMin, Vector2 worldSize)
        {
            if (owner == null) return;
            var settings = GameServices.SeaReachConfig;
            if (elevations == null || !settings.IsUsable) { Release(owner); return; }
            var rect = new Vector4(worldMin.x, worldMin.y, worldSize.x, worldSize.y);
            // A baked grid has no asset identity: its content is the key (a cheap order-sensitive hash).
            int source = HashElevations(elevations, width, height);
            if (IsCached(owner, source, 0, rect, Vector2.zero, settings)) { Rebind(owner); return; }
            Bind(owner, SeaReachField.Build(elevations, width, height, worldMin, worldSize, settings), settings,
                 source, 0, rect, Vector2.zero);
        }

        /// <summary>Re-read the blend from the config (a tuning edit in Play); no flood, no upload.</summary>
        public static void RefreshBlend(object owner)
        {
            if (s_Field == null || !ReferenceEquals(s_Owner, owner)) return;
            float blend = GameServices.SeaReachConfig.ReconnectBlendMeters;
            SeaReach.SetBlend(owner, blend);
            Shader.SetGlobalVector(Params, new Vector4(blend, 1f, s_Field.Width, s_Field.Height));
        }

        /// <summary>Unbind this owner's map (a later publisher's map is left alone).</summary>
        public static void Release(object owner)
        {
            if (s_Owner != null && !ReferenceEquals(s_Owner, owner)) return;
            SeaReach.Clear(owner);
            s_Owner = null;
            s_Field = null;
            s_KeySource = 0;
            if (s_Texture != null)
            {
                Object.DestroyImmediate(s_Texture);
                s_Texture = null;
            }
            BindUnset();
        }

        private static bool IsCached(object owner, int source, int revision, Vector4 rect, Vector2 range,
                                     in SeaReachSettings settings)
            => s_Field != null && s_Texture != null && s_KeySource == source && s_KeyRevision == revision
               && s_KeyRect == rect && s_KeyRange == range && SameSettings(s_KeySettings, settings)
               && (s_Owner == null || ReferenceEquals(s_Owner, owner) || s_Owner is Object o && o == null);

        private static bool SameSettings(in SeaReachSettings a, in SeaReachSettings b)
            => a.Enabled == b.Enabled && a.SeedMaxElevationMeters == b.SeedMaxElevationMeters
               && a.MinPoolDepthMeters == b.MinPoolDepthMeters && a.QuantumMeters == b.QuantumMeters
               && a.WetBandMeters == b.WetBandMeters;

        private static void Rebind(object owner)
        {
            s_Owner = owner;
            float blend = GameServices.SeaReachConfig.ReconnectBlendMeters;
            SeaReach.Publish(owner, s_Field, blend);
            BindGlobals(blend);
        }

        private static void Bind(object owner, SeaReachField field, in SeaReachSettings settings,
                                 int source, int revision, Vector4 rect, Vector2 range)
        {
            if (field == null) { Release(owner); return; }
            if (s_Texture != null) Object.DestroyImmediate(s_Texture);
            s_Texture = Upload(field);
            s_Field = field;
            s_Owner = owner;
            s_KeySource = source;
            s_KeyRevision = revision;
            s_KeyRect = rect;
            s_KeyRange = range;
            s_KeySettings = settings;
            SeaReach.Publish(owner, field, settings.ReconnectBlendMeters);
            BindGlobals(settings.ReconnectBlendMeters);
        }

        private static void BindGlobals(float blend)
        {
            Shader.SetGlobalTexture(Tex, s_Texture);
            Shader.SetGlobalVector(Rect, new Vector4(s_Field.WorldMin.x, s_Field.WorldMin.y,
                                                     s_Field.WorldSize.x, s_Field.WorldSize.y));
            Shader.SetGlobalVector(Params, new Vector4(blend, 1f, s_Field.Width, s_Field.Height));
            Shader.SetGlobalVector(Wet, new Vector4(s_Field.WetBandMeters, 0f, 0f, 0f));
        }

        private static void BindUnset()
        {
            if (s_Fallback == null)
            {
                s_Fallback = new Texture2D(1, 1, TextureFormat.RGHalf, false, true)
                    { name = "HH Sea reach (unset)", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                s_Fallback.SetPixel(0, 0, new Color(SeaReachMath.TextureAlwaysOpen, 0f, 0f, 0f));
                s_Fallback.Apply(false, true);
            }
            Shader.SetGlobalTexture(Tex, s_Fallback);
            Shader.SetGlobalVector(Rect, Vector4.zero);
            Shader.SetGlobalVector(Params, Vector4.zero);
            Shader.SetGlobalVector(Wet, Vector4.zero);
        }

        // A point-sampled, unfiltered, mip-free half-float map: R the sill (sentinels made finite), G the
        // seabed height (the smoothed read's wetness). A half holds a sill to about a millimetre near the
        // datum and a floor to under a centimetre at the deepest bed; RGFloat stands in where RGHalf cannot.
        private static Texture2D Upload(SeaReachField field)
        {
            bool half = SystemInfo.SupportsTextureFormat(TextureFormat.RGHalf);
            var tex = new Texture2D(field.Width, field.Height, half ? TextureFormat.RGHalf : TextureFormat.RGFloat, false, true)
            {
                name = "HH Sea reach",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };
            int n = field.CellCount;
            if (half)
            {
                NativeArray<ushort> data = tex.GetRawTextureData<ushort>();
                for (int i = 0; i < n; i++)
                {
                    data[2 * i] = Mathf.FloatToHalf(SeaReachMath.ToTexture(field.SillAtIndex(i)));
                    data[2 * i + 1] = Mathf.FloatToHalf(field.FloorAtIndex(i));
                }
            }
            else
            {
                NativeArray<float> data = tex.GetRawTextureData<float>();
                for (int i = 0; i < n; i++)
                {
                    data[2 * i] = SeaReachMath.ToTexture(field.SillAtIndex(i));
                    data[2 * i + 1] = field.FloorAtIndex(i);
                }
            }
            tex.Apply(false, true);
            return tex;
        }

        // The painted seabed's elevations, the water shader's decode (lerp(min, max, r)), row 0 at the bottom.
        private static float[] Decode(Texture2D t, float lo, float hi)
        {
            int n = t.width * t.height;
            var e = new float[n];
            switch (t.format)
            {
                case TextureFormat.R16:
                {
                    NativeArray<ushort> w = t.GetRawTextureData<ushort>();
                    if (w.Length < n) return null;
                    for (int i = 0; i < n; i++) e[i] = Mathf.Lerp(lo, hi, w[i] / 65535f);
                    return e;
                }
                case TextureFormat.R8:
                case TextureFormat.Alpha8:
                {
                    NativeArray<byte> b = t.GetRawTextureData<byte>();
                    if (b.Length < n) return null;
                    for (int i = 0; i < n; i++) e[i] = Mathf.Lerp(lo, hi, b[i] / 255f);
                    return e;
                }
                case TextureFormat.RGBA32:
                {
                    NativeArray<byte> b = t.GetRawTextureData<byte>();
                    if (b.Length < n * 4) return null;
                    for (int i = 0; i < n; i++) e[i] = Mathf.Lerp(lo, hi, b[i * 4] / 255f);
                    return e;
                }
                default:
                {
                    Color[] c = t.GetPixels();
                    if (c.Length < n) return null;
                    for (int i = 0; i < n; i++) e[i] = Mathf.Lerp(lo, hi, c[i].r);
                    return e;
                }
            }
        }

        private static int HashElevations(float[] e, int width, int height)
        {
            unchecked
            {
                int h = (int)2166136261 ^ width * 31 ^ height;
                for (int i = 0; i < e.Length; i++) h = (h ^ e[i].GetHashCode()) * 16777619;
                return h == 0 ? 1 : h;
            }
        }
    }
}
