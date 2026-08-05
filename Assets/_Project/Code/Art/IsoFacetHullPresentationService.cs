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
            MakeReflective(renderer);
            MakeChurn(host, def);
            return renderer;
        }

        /// <summary>
        /// (ADR 0027 #6) Make this hull CHURN the advected foam buffer.
        ///
        /// <para><b>Why it lives here, beside <see cref="MakeReflective"/>.</b> Same argument, same
        /// place: the fleet is constructed at runtime from Defs rather than authored into a scene, so
        /// "wire it where the thing is made" lands in this service — and #383 shipped the buffer, the
        /// injector, the advect pass and the shader read with <b>nothing attached to anything</b>. No
        /// prefab and no scene carried a <see cref="FoamInjector"/>, so the buffer had no source at
        /// all; it was not merely dialled down, it was unsourced. That is half of why the owner still
        /// could not see foam that behaves like foam (2026-08-05). The other half was
        /// <c>_WakeFoamStrength</c> 0, now dialled in on all nine water materials.</para>
        ///
        /// <para><b>On the HOST, not the overlay</b> (the opposite of the reflector): the injector reads
        /// <c>transform.position</c> as the hull's position on the water and derives speed through the
        /// water from it. The host is the physics root that actually moves; the overlay quad
        /// counter-rotates and is the wrong transform to ask.</para>
        ///
        /// <para><b>The churned band is DATA, not a constant</b> (rule 6): its half-width comes from the
        /// def's <c>WatertightHalfBeamMeters</c> — the hull's own half-beam, already authored per hull
        /// and already the number the injector's tooltip describes ("roughly the hull's beam — a dory
        /// wants ~0.9"; the dory's def says 0.85). So a dory lays a narrow ribbon and a tanker a broad
        /// one, with no per-hull tuning table to keep in sync. A def that never had a half-beam
        /// authored (0) keeps the injector's own serialized default rather than collapsing the band to
        /// nothing.</para>
        ///
        /// <para>Zero-cost-when-idle survives: the injector unregisters the moment the hull is off the
        /// water, and <see cref="IsoFacetHullFeature"/> records no pass when nothing is registered.</para>
        /// </summary>
        static void MakeChurn(GameObject host, HullMeshDef def)
        {
            var injector = host.GetComponent<FoamInjector>();
            if (injector == null) injector = host.AddComponent<FoamInjector>();
            if (def != null && def.WatertightHalfBeamMeters > 0f)
                injector.ConfigureRadius(def.WatertightHalfBeamMeters);
        }

        /// <summary>
        /// (ADR 0027 #8) Make this hull reflect in the water.
        ///
        /// <para><b>On the OVERLAY quad, not the host.</b> The overlay is the renderer that carries
        /// <c>HiddenHarbours/IsoFacetOverlay</c> — the shader with the <c>HHReflect</c> pass — and it
        /// is also the thing whose pixels ARE the hull's visible image (re-composed from the keyline
        /// resolve). The host GameObject has no renderer at all, and the facet mesh child draws only
        /// into the off-screen MRT.</para>
        ///
        /// <para><b>Here rather than in a builder,</b> because this service is the one place a mesh
        /// hull is ever installed — the fleet is constructed at runtime from Defs, not authored into
        /// a scene, so "wire it where the thing is made" lands here. Every hull the game builds
        /// therefore reflects, and the water's own <c>_ObjectReflectStrength</c> is the single dial
        /// that decides whether that shows.</para>
        ///
        /// <para>No pivot override: the overlay quad is built around the hull's PIVOT, which the rig
        /// convention puts at her waterline contact (ADR 0026) — already the axis a reflection wants.</para>
        /// </summary>
        static void MakeReflective(IsoFacetHullRenderer hull)
        {
            MeshRenderer overlay = hull != null ? hull.OverlayRenderer : null;
            if (overlay == null) return;
            var reflector = overlay.GetComponent<ReflectiveObject>();
            if (reflector == null) reflector = overlay.gameObject.AddComponent<ReflectiveObject>();
            reflector.Refresh();
        }

        /// <summary>
        /// The child that carries a hull's heading, rock and heave — asked of the renderer, never
        /// found by name. See <see cref="IsoFacetHullRenderer.PosedMesh"/> for why the name lookup was
        /// a real defect: a hull swap leaves two children called "FacetMesh" alive for one frame, and
        /// <c>Find</c> returns the one that is about to be destroyed.
        /// </summary>
        static Transform PosedMeshOf(IsoFacetHullRenderer hull) => hull != null ? hull.PosedMesh : null;

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
            Transform posed = PosedMeshOf(hull);
            if (posed == null)
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': the hull renderer has " +
                               $"no posed mesh child to hang '{def.Id}' from. The renderer was never " +
                               "configured, or its child layout changed and this attach point must be " +
                               "re-aimed.");
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
            Transform posed = PosedMeshOf(hull);
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
            Transform posed = PosedMeshOf(hull);
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
                FixedMesh = def.FixedMesh,
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
