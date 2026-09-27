using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The region's STILL WATER (ADR 0046) published as shader globals, for the two shaders that draw a
    /// waterline: the water (its drawn edge) and the tidal faces (where a face stops being wet). The
    /// source is the Core seam's own map (<see cref="GameServices.StillWater"/> →
    /// <see cref="IStillWater.Map"/>) — the SAME texture, rect and range the sim decodes — so the ponds the
    /// water draws are the ponds the fisher wades.
    ///
    /// <para><b>Globals.</b> <c>_HHStillTex</c> (R = still level on the height scale, 0 = none),
    /// <c>_HHStillRect</c> (minX, minY, sizeX, sizeY) and <c>_HHStillRange</c> (min, max, bound, 0). Unset
    /// (<c>_HHStillRange.z</c> = 0) is "no still water anywhere", under which both shaders draw exactly
    /// what they drew before this seam: every region today.</para>
    ///
    /// <para><b>Held, not owned.</b> Every live <see cref="WaterSurface"/> holds the globals
    /// (<see cref="Hold"/> on enable, <see cref="Release"/> on disable); while anything holds them they
    /// follow <see cref="GameServices.StillWaterChanged"/>, and when the last holder lets go they are
    /// unset. So the hop order of two regions' seas cannot leave the globals stale or unset under a live
    /// sea, and a stopped play session cannot haunt the editor with a pond.</para>
    /// </summary>
    public static class StillWaterGlobals
    {
        public static readonly int Tex = Shader.PropertyToID("_HHStillTex");
        public static readonly int Rect = Shader.PropertyToID("_HHStillRect");
        public static readonly int Range = Shader.PropertyToID("_HHStillRange");

        private static readonly List<object> s_Holders = new List<object>(2);
        private static Texture2D s_Fallback;

        /// <summary>True while a still map is published (the last <see cref="Refresh"/> found one).</summary>
        public static bool IsBound { get; private set; }

        /// <summary>How many consumers hold the globals right now (diagnostics and tests).</summary>
        public static int HolderCount => s_Holders.Count;

        /// <summary>
        /// Start holding the globals (a consumer's <c>OnEnable</c>): publish the registered still water now
        /// and follow it until <see cref="Release"/>. Null and double holds are no-ops.
        /// </summary>
        public static void Hold(object holder)
        {
            if (holder == null || s_Holders.Contains(holder)) return;
            s_Holders.Add(holder);
            if (s_Holders.Count == 1) GameServices.StillWaterChanged += Refresh;
            Refresh();
        }

        /// <summary>
        /// Stop holding (a consumer's <c>OnDisable</c>). The last release unsets the globals and stops
        /// following the seam. A holder that never held is a no-op — a teardown never checks first.
        /// </summary>
        public static void Release(object holder)
        {
            if (holder == null || !s_Holders.Remove(holder)) return;
            if (s_Holders.Count == 0) GameServices.StillWaterChanged -= Refresh;
            Refresh();
        }

        /// <summary>
        /// Publish the registered still water's map while anything holds the globals; otherwise, or when
        /// the region has no still map, publish "unset".
        /// </summary>
        public static void Refresh()
        {
            StillWaterMap map = s_Holders.Count > 0 ? GameServices.StillWater.Map : default;
            if (!map.IsBound) { PublishUnset(); return; }

            Shader.SetGlobalTexture(Tex, map.Texture);
            Shader.SetGlobalVector(Rect, new Vector4(map.WorldMin.x, map.WorldMin.y,
                                                     Mathf.Max(map.WorldSize.x, 1e-3f),
                                                     Mathf.Max(map.WorldSize.y, 1e-3f)));
            Shader.SetGlobalVector(Range, new Vector4(map.MinLevel, map.MaxLevel, 1f, 0f));
            IsBound = true;
        }

        /// <summary>"No still water anywhere": a 1×1 code-0 texture, a zero rect, and the bound flag off.</summary>
        public static void PublishUnset()
        {
            if (s_Fallback == null)
            {
                s_Fallback = new Texture2D(1, 1, TextureFormat.R8, false, true) { name = "HH Still water (unset)", hideFlags = HideFlags.HideAndDontSave };
                s_Fallback.SetPixel(0, 0, Color.black);
                s_Fallback.Apply(false, true);
            }
            Shader.SetGlobalTexture(Tex, s_Fallback);
            Shader.SetGlobalVector(Rect, Vector4.zero);
            Shader.SetGlobalVector(Range, Vector4.zero);
            IsBound = false;
        }

        // Without a domain reload a play session would inherit the last one's holders (and a second
        // subscription on the next Hold); start every session empty.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            GameServices.StillWaterChanged -= Refresh;
            s_Holders.Clear();
            IsBound = false;
        }
    }
}
