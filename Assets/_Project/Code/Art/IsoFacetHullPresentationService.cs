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

        /// <summary>The child that carries a hull's heading, rock and heave.</summary>
        const string PosedMeshChild = "FacetMesh";

        /// <inheritdoc/>
        public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot)
        {
            if (host == null) return null;
            if (def == null || !def.IsUsable())
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': fitting def " +
                               $"'{(def != null ? def.Id : "<null>")}' is unusable. Not attached — " +
                               "the caller should keep the sprite path, where the fitting exists.");
                return null;
            }

            var hull = host.GetComponent<IsoFacetHullRenderer>();
            if (hull == null)
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': no mesh hull is " +
                               $"installed, so fitting '{def.Id}' has nothing to bolt to. Install the " +
                               "hull first — a fitting posed against no hull would sit at the world " +
                               "origin rocking on its own.");
                return null;
            }

            // ⚠️ PARENT TO THE POSED CHILD, not to the host. The host does not turn; "FacetMesh"
            // carries heading, rock and heave, and a fitting must inherit all three or it shears off
            // the boat the instant she moves — which is exactly the failure the sprite path's layers
            // had whenever a rock frame and a steer column disagreed.
            Transform posed = hull.transform.Find(PosedMeshChild);
            if (posed == null)
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': the hull renderer has " +
                               $"no '{PosedMeshChild}' child to hang '{def.Id}' from. The renderer's " +
                               "child layout changed and this attach point must be re-aimed.");
                return null;
            }

            Transform existing = posed.Find(slot);
            var prop = existing != null
                ? existing.GetComponent<IsoFacetPropRenderer>()
                : null;
            if (prop == null)
            {
                var go = new GameObject(slot) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(posed, false);
                prop = go.AddComponent<IsoFacetPropRenderer>();
            }
            prop.Configure(ToPropSetup(def));
            return prop;
        }

        /// <inheritdoc/>
        public void DetachProps(GameObject host)
        {
            if (host == null) return;
            var hull = host.GetComponent<IsoFacetHullRenderer>();
            if (hull == null) return;
            Transform posed = hull.transform.Find(PosedMeshChild);
            if (posed == null) return;

            for (int i = posed.childCount - 1; i >= 0; i--)
            {
                var prop = posed.GetChild(i).GetComponent<IsoFacetPropRenderer>();
                if (prop != null) Destroy(prop.gameObject);
            }
        }

        /// <inheritdoc/>
        public void DetachProp(GameObject host, string slot)
        {
            if (host == null || string.IsNullOrEmpty(slot)) return;
            var hull = host.GetComponent<IsoFacetHullRenderer>();
            Transform posed = hull != null ? hull.transform.Find(PosedMeshChild) : null;
            Transform existing = posed != null ? posed.Find(slot) : null;
            if (existing != null && existing.GetComponent<IsoFacetPropRenderer>() != null)
                Destroy(existing.gameObject);
        }

        /// <summary>The fitting def, converted to the renderer's runtime setup — plain copies.</summary>
        public static IsoFacetPropSetup ToPropSetup(HullPropMeshDef def)
        {
            var ramps = new Color32[def.Ramps.Length][];
            var offsets = new int[def.Ramps.Length];
            for (int m = 0; m < def.Ramps.Length; m++)
            {
                ramps[m] = def.Ramps[m].Colors;
                offsets[m] = def.Ramps[m].Offset;
            }

            return new IsoFacetPropSetup
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
                PivotLocalMeters = def.PivotLocalMeters,
            };
        }

        /// <inheritdoc/>
        public void Remove(GameObject host)
        {
            if (host == null) return;
            DetachProps(host);
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
                WatertightDeckHeightMeters = def.WatertightDeckHeightMeters,
                WatertightHalfBeamMeters = def.WatertightHalfBeamMeters,
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
