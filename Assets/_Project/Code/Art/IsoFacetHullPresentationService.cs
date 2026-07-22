using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Art's side of the mesh-hull seam (ADR 0022 phase 4)</b> — the
    /// <see cref="IHullMeshPresentationService"/> that installs an <see cref="IsoFacetHullRenderer"/>
    /// on a host GameObject from a committed <see cref="HullMeshDef"/>. Boats calls it through
    /// <see cref="HullMeshPresentation.Service"/> and never sees a URP type (rule 4).
    ///
    /// <para><b>Self-registering at runtime</b> (<see cref="RuntimeInitializeOnLoadMethod"/>, before
    /// the first scene — the same pattern as the ambient Art hosts), so a player build and PlayMode
    /// tests get the mesh path with no wiring. EditMode tests and editor tooling call
    /// <see cref="EnsureRegistered"/> explicitly; edit-time scene BUILDERS deliberately do not, so a
    /// built scene serialises the sprite rig and the mesh path is chosen live, per run, by the
    /// skinner (builder-generated scenes must not bake a renderer whose setup is runtime-owned).</para>
    /// </summary>
    public sealed class IsoFacetHullPresentationService : IHullMeshPresentationService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAtLoad() => EnsureRegistered();

        /// <summary>Idempotent registration. Never replaces a live service (a test double stays).</summary>
        public static void EnsureRegistered()
        {
            HullMeshPresentation.Service ??= new IsoFacetHullPresentationService();
        }

        /// <inheritdoc/>
        public IHullMeshRenderer Install(GameObject host, HullMeshDef def)
        {
            if (host == null) return null;
            if (def == null || !def.IsUsable())
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': hull mesh def " +
                               $"'{(def != null ? def.Id : "<null>")}' is unusable (missing mesh/ramps/" +
                               "bayer or bad cell geometry). No mesh renderer installed — the caller " +
                               "should fall back to the sprite path.");
                return null;
            }

            var renderer = host.GetComponent<IsoFacetHullRenderer>();
            if (renderer == null) renderer = host.AddComponent<IsoFacetHullRenderer>();
            renderer.Configure(ToSetup(def));
            return renderer;
        }

        /// <inheritdoc/>
        public void Remove(GameObject host)
        {
            if (host == null) return;
            var renderer = host.GetComponent<IsoFacetHullRenderer>();
            if (renderer != null) Destroy(renderer);
            // The renderer adds a SortingGroup for the sprite-sorting workaround; a host going back
            // to the sprite path must not keep sorting as a group.
            var group = host.GetComponent<SortingGroup>();
            if (group != null) Destroy(group);
        }

        /// <summary>The def, converted to the renderer's runtime setup — plain copies, no rescaling.</summary>
        public static IsoFacetHullSetup ToSetup(HullMeshDef def)
        {
            var ramps = new Color32[def.Ramps.Length][];
            var offsets = new int[def.Ramps.Length];
            for (int m = 0; m < def.Ramps.Length; m++)
            {
                ramps[m] = def.Ramps[m].Colors;
                offsets[m] = def.Ramps[m].Offset;
            }

            return new IsoFacetHullSetup
            {
                Mesh = def.Mesh,
                Ramps = ramps,
                RampOffsets = offsets,
                LightN = def.LightN,
                Gain = def.Gain,
                Bias = def.Bias,
                Bayer16 = def.Bayer16,
                Keyline = def.Keyline,
                PivotPx = def.PivotPx,
                PxPerMetre = def.PxPerMetre,
                CellW = def.CellW,
                CellH = def.CellH,
                ElevationDeg = def.ElevationDeg,
            };
        }

        // Editor-safe destroy: the A/B toggle runs in play mode, but tests and tooling call Remove
        // outside it, where Object.Destroy throws.
        static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
