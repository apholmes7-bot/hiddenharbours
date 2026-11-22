using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Art's side of the dwelling-mesh seam (ADR 0060, PR 3)</b>: the
    /// <see cref="IDwellingMeshPresentationService"/> that installs an <see cref="IsoFacetHullRenderer"/>
    /// on a host GameObject from a committed <see cref="DwellingMeshDef"/>. World calls it through
    /// <see cref="DwellingMeshPresentation.Service"/> and never sees a URP type (rule 4).
    ///
    /// <para><b>The third door onto the same room.</b> Hulls (<see cref="IsoFacetHullPresentationService"/>)
    /// and road vehicles (<see cref="IsoFacetVehiclePresentationService"/>) already share this renderer;
    /// a parked home is baked through the same camera and packs the same face constants, so the facet
    /// pass draws it without knowing what it is. Shell and room are one mesh and one draw.</para>
    ///
    /// <para><b>What it deliberately does not do.</b> No <c>FoamInjector</c> (a camper on gravel churns
    /// no wake), no <c>ReflectiveObject</c> (it stands nowhere near the harbour), no deck caster, no
    /// lamps or windows (a dwelling def carries none), and the waterline clamp stays at 0 ("clamp off").
    /// The pose is <b>still</b>: roll, pitch, heave and ride are written to zero on every install, and
    /// nothing in this module writes them again, because a home does not move.</para>
    ///
    /// <para><b>What the facet-id budget pays (ADR 0056).</b> The renderer takes its one id on enable
    /// and its deck-occupant block lazily, on the first <see cref="IHullMeshRenderer.DeckOccupants"/>
    /// claim, giving it back on the last release. So an empty dwelling holds a single id, and an
    /// occupied one holds a block only while somebody is inside: the rule ADR 0060 section 5 asks for,
    /// carried by the shared renderer rather than re-implemented here.</para>
    ///
    /// <para><b>Self-registering at runtime</b>, before the first scene, like the hull and vehicle
    /// services. EditMode tests and editor tooling call <see cref="EnsureRegistered"/> explicitly;
    /// edit-time scene builders deliberately do not, so a built scene serialises no renderer.</para>
    /// </summary>
    public sealed class IsoFacetDwellingPresentationService : IDwellingMeshPresentationService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAtLoad() => EnsureRegistered();

        /// <summary>Idempotent registration. Never replaces a live service (a test double stays).</summary>
        public static void EnsureRegistered()
        {
            DwellingMeshPresentation.Service ??= new IsoFacetDwellingPresentationService();
        }

        /// <inheritdoc/>
        public IHullMeshRenderer Install(GameObject host, DwellingMeshDef def)
        {
            if (host == null) return null;
            string problem = def != null ? def.FirstProblem() : "the def is null";
            if (problem != null)
            {
                Debug.LogError($"[IsoFacetDwellingPresentationService] '{host.name}': dwelling mesh def " +
                               $"'{(def != null ? def.Id : "<null>")}' is unusable ({problem}). No renderer " +
                               "installed; the caller should refuse to place an invisible home.");
                return null;
            }

            var renderer = host.GetComponent<IsoFacetHullRenderer>();
            if (renderer == null) renderer = host.AddComponent<IsoFacetHullRenderer>();
            renderer.Configure(ToSetup(def));
            HoldStill(renderer);
            HullSunShadowCaster.Fit(host);
            HullLampShadowCaster.Fit(host);
            return renderer;
        }

        /// <summary>
        /// The still pose: no rock, no heave, no ride. Written on every install so a host re-used from a
        /// boat or a stale serialised value cannot leave a home leaning. The heading is the caller's (the
        /// site's ground bearing, once) and is left as it is.
        /// </summary>
        static void HoldStill(IHullMeshRenderer renderer)
        {
            renderer.RollDegrees = 0f;
            renderer.PitchDegrees = 0f;
            renderer.HeavePixels = 0f;
            renderer.RidePixels = 0f;
        }

        /// <inheritdoc/>
        public IHullPropRenderer AttachPart(GameObject host, HullPropMeshDef def, string slot)
        {
            if (host == null) return null;
            if (string.IsNullOrEmpty(slot))
            {
                Debug.LogError($"[IsoFacetDwellingPresentationService] '{host.name}': a part needs a slot " +
                               "name, so a re-install reconfigures it in place. Not attached.");
                return null;
            }
            if (def == null || !def.IsUsable())
            {
                Debug.LogError($"[IsoFacetDwellingPresentationService] '{host.name}': part def " +
                               $"'{(def != null ? def.Id : "<null>")}' is unusable. Not attached; the room " +
                               "mesh still draws the fixture's fixed shape.");
                return null;
            }

            var dwelling = host.GetComponent<IsoFacetHullRenderer>();
            Transform posed = dwelling != null ? dwelling.PosedMesh : null;
            if (posed == null)
            {
                Debug.LogError($"[IsoFacetDwellingPresentationService] '{host.name}': no dwelling mesh is " +
                               $"installed, so part '{def.Id}' has nothing to hang from. Install first.");
                return null;
            }

            // Parented to the posed child, not the host: the posed mesh carries the site's heading, and a
            // freezer lid or a door leaf must turn with the home it belongs to.
            Transform existing = posed.Find(slot);
            var prop = existing != null ? existing.GetComponent<IsoFacetPropRenderer>() : null;
            if (prop == null)
            {
                var go = new GameObject(slot) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(posed, false);
                prop = go.AddComponent<IsoFacetPropRenderer>();
            }
            prop.Configure(IsoFacetHullPresentationService.ToPropSetup(def));
            return prop;
        }

        /// <inheritdoc/>
        public void DetachPart(GameObject host, string slot)
        {
            if (host == null || string.IsNullOrEmpty(slot)) return;
            var dwelling = host.GetComponent<IsoFacetHullRenderer>();
            Transform posed = dwelling != null ? dwelling.PosedMesh : null;
            Transform existing = posed != null ? posed.Find(slot) : null;
            if (existing != null && existing.GetComponent<IsoFacetPropRenderer>() != null)
                Destroy(existing.gameObject);
        }

        /// <inheritdoc/>
        public void Remove(GameObject host)
        {
            if (host == null) return;
            var dwelling = host.GetComponent<IsoFacetHullRenderer>();
            Transform posed = dwelling != null ? dwelling.PosedMesh : null;
            if (posed != null)
            {
                for (int i = posed.childCount - 1; i >= 0; i--)
                {
                    var prop = posed.GetChild(i).GetComponent<IsoFacetPropRenderer>();
                    if (prop != null) Destroy(prop.gameObject);
                }
            }

            // The casters go with the renderer they cast from: a host with no facet mesh has no id in the
            // screen texture to cast with.
            var sun = host.GetComponent<HullSunShadowCaster>();
            if (sun != null) Destroy(sun);
            var lamp = host.GetComponent<HullLampShadowCaster>();
            if (lamp != null) Destroy(lamp);
            if (dwelling != null) Destroy(dwelling);
            // The renderer adds a SortingGroup for its sprite-sorting workaround; a host with no dwelling
            // drawn must not keep sorting as a group.
            var group = host.GetComponent<SortingGroup>();
            if (group != null) Destroy(group);
        }

        /// <summary>
        /// The def, converted to the renderer's runtime setup: plain copies, no rescaling. The room's
        /// palette goes in its own table (the renderer's interior ramps), exactly as a converted hull's
        /// does. The watertight fields are 0 ("clamp off"), there are no caustics, no lamps, no panes and
        /// no baked door-leaf poses: a moving fixture part is attached with <see cref="AttachPart"/>.
        /// </summary>
        public static IsoFacetHullSetup ToSetup(DwellingMeshDef def)
        {
            Copy(def.Ramps, out var ramps, out var offsets, out var dither);
            Copy(def.InteriorRamps, out var interiorRamps, out var interiorOffsets, out var interiorDither);

            return new IsoFacetHullSetup
            {
                Mesh = def.Mesh,
                ReceivesWaterCaustics = false,
                Ramps = ramps,
                RampOffsets = offsets,
                RampDitherWeights = dither,
                InteriorRamps = interiorRamps,
                InteriorRampOffsets = interiorOffsets,
                InteriorRampDitherWeights = interiorDither,
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
                WatertightDeckHeightMeters = 0f,
                WatertightHalfBeamMeters = 0f,
            };
        }

        static void Copy(HullMeshDef.Ramp[] table, out Color32[][] ramps, out int[] offsets, out float[] dither)
        {
            table ??= System.Array.Empty<HullMeshDef.Ramp>();
            ramps = new Color32[table.Length][];
            offsets = new int[table.Length];
            dither = new float[table.Length];
            for (int m = 0; m < table.Length; m++)
            {
                ramps[m] = table[m].Colors;
                offsets[m] = table[m].Offset;
                dither[m] = table[m].DitherWeight;
            }
        }

        // Editor-safe destroy: tests and tooling call Remove outside play mode, where Object.Destroy throws.
        static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
