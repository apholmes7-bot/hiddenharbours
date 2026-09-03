using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The lamps a hull actually burns</b> (ADR 0016) — her port and starboard sidelights, her
    /// stern light, her masthead, and the warm spill out of her wheelhouse — drawn as the project's
    /// ordinary night-gated additive glows, each parked at the point on her that her own rig says it
    /// is bolted to.
    ///
    /// <para><b>Self-installing off the def, like every other ambient effect here.</b> The mesh-hull
    /// presentation service adds this component when — and only when — the hull's
    /// <see cref="HullMeshDef.Lamps"/> is non-empty, so a boat with lamps carries them everywhere she
    /// is built with no scene wiring, and a boat without simply has none. That is the owner's ratified
    /// lighting principle of 2026-07-05 applied to the fleet: the darkening is automatic for
    /// everything, and the exception is a light SOURCE, which the object comes preconfigured with.</para>
    ///
    /// <para><b>Why the lamps hang off the DRAWN child and not the root.</b> The boat root carries her
    /// physics yaw and nothing else; her "FacetMesh" child carries heading, roll, pitch and heave as a
    /// real transform (<see cref="IsoFacetHullRenderer.PosedMesh"/>), which is why every fitting is
    /// posed against it. One boat-local triple pushed through that transform therefore lands where the
    /// art draws that point at ANY heading — including the intermediate ones a mesh hull genuinely
    /// sails at, which a per-facing pixel table could not answer — and rides every wave for free. This
    /// is deliberately UNLIKE <c>HullLocalAnchor</c>, which poses gameplay points at the LEVEL pose
    /// precisely so a doorway is not harder to hit at the crest than in the trough. A lamp is the
    /// opposite case: it is a picture, and the sea may move a picture.</para>
    ///
    /// <para><b>Positioned, not parented.</b> The posed child carries the rig-to-world MIRROR
    /// (<c>IsoFacetMath.HullScale</c> is <c>(1,1,-1)</c>) and a full 3-D rotation; a light quad
    /// parented into that frame would inherit both and be drawn edge-on or inside out. The world POINT
    /// is all a lamp wants, so each light node reads it every LateUpdate and keeps its own identity
    /// pose. One <c>TransformPoint</c> per lamp per frame, no allocation (rule 7).</para>
    ///
    /// <para><b>Determinism / seams.</b> Visual-only: it drives no simulation, saves nothing, and every
    /// value is a pure function of the def, the preset and the published day/night tint (rule 5). It
    /// reads no Boats type — the hull is reached through its Art renderer and the cabin through Core's
    /// own <see cref="CabinEntered"/>/<see cref="CabinLeft"/> bus (rule 4).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]   // after the hull poses herself (-110), before SceneLight's own LateUpdate
    public sealed class BoatLamps : MonoBehaviour
    {
        [Tooltip("How much brighter the CABIN glow burns while somebody is actually below on this " +
                 "hull, over the preset it shows when the room is merely lit (1 = no change). The " +
                 "door opening and the lamp being turned up for whoever came in are the same event " +
                 "to everyone outside.")]
        [Min(0f)] [SerializeField] private float _cabinOccupiedBoost = 1.5f;

        [Tooltip("Turn this hull's GLOWS off without unwiring anything — the master switch a boat " +
                 "that is laid up, or a scene that wants her dark, can throw. It governs the lamps " +
                 "this component owns (sidelights, stern, masthead, cabin); her SEARCHLIGHT is a " +
                 "separate component with its own switch and is not affected.")]
        [SerializeField] private bool _lampsOn = true;

        private IsoFacetHullRenderer _hull;
        private Transform _posed;
        private HullLamp[] _lamps;
        private SceneLight[] _lights;
        private bool _subscribed;
        private bool _cabinOccupied;

        /// <summary>The lamps this hull is currently showing — empty until she has been skinned.</summary>
        public HullLamp[] Lamps => _lamps ?? System.Array.Empty<HullLamp>();

        /// <summary>The built lights, in the same order as <see cref="Lamps"/>. Null before the first
        /// build; a slot is null for a lamp whose light could not be made.</summary>
        public SceneLight[] Lights => _lights;

        /// <summary>Is somebody below on this hull right now (what drives the cabin-glow boost)? Public
        /// so a test can read the state the bus put the lamps in without a second source of truth.</summary>
        public bool CabinOccupied => _cabinOccupied;

        /// <summary>
        /// <b>The boat ROOT that a node somewhere on a hull belongs to.</b>
        ///
        /// <para>The mesh renderer is installed on the hull's VISUAL CHILD, not on the boat — so
        /// anything hanging off it that needs to speak about "this boat" (which hull the cabin bus is
        /// talking about; which transform a searchlight aims along) has to climb first, and the child
        /// itself is stomped back to world-identity every LateUpdate, so its own pose says nothing
        /// about where she is pointing.</para>
        ///
        /// <para><b>The rigidbody is the mark, not <c>transform.root</c>.</b> A boat is not always the
        /// top of her own hierarchy — the arrival parents the whole hull under the opening's node, so
        /// <c>transform.root</c> there is a director, not a boat. Her PHYSICS body is the thing that
        /// is hers by construction, and this is the same climb the light menu already uses to find a
        /// boat from a selected child. Falls back to the parent, then to self, so a hull built without
        /// a body still resolves to something sane rather than to null.</para>
        /// </summary>
        public static Transform BoatRootOf(Transform node)
        {
            if (node == null) return null;
            var body = node.GetComponentInParent<Rigidbody2D>();
            if (body != null) return body.transform;
            return node.parent != null ? node.parent : node;
        }

        /// <summary>Master switch for the glows this component owns — quads disabled, nothing drawn.
        /// The searchlight is <see cref="BoatSpotlight"/>'s and keeps its own switch.</summary>
        public bool LampsOn
        {
            get => _lampsOn;
            set { _lampsOn = value; ApplyEnabled(); }
        }

        private void OnEnable()
        {
            // A hop or a hull swap may have replaced the renderer under us, so nothing is cached
            // across an enable — the same reason HullLocalAnchor clears its own resolution here.
            _hull = null;
            _posed = null;
            Subscribe();
            Rebuild();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ApplyEnabled();
        }

        private void LateUpdate()
        {
            if (!Resolve()) return;
            if (_lights == null) return;

            for (int i = 0; i < _lights.Length; i++)
            {
                SceneLight light = _lights[i];
                if (light == null) continue;

                // The lamp's world point, straight through the frame the hull is DRAWN in. z is
                // pinned back to the root's own depth deliberately: the posed child's z carries the
                // displaced-water depth bias and the ADR 0033 shear compensation, which are answers
                // to a z-buffer question and mean nothing to a light. SceneLight re-pins the quad to
                // the camera anyway; this only keeps the node itself somewhere sane to inspect.
                Vector3 p = _posed.TransformPoint(_lamps[i].RigLocalMetres);
                p.z = transform.position.z;
                light.transform.position = p;
            }
        }

        // -------------------------------------------------------------------------------------------
        //  building the lights
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// Find this hull's renderer and her lamp table, rebuilding the lights if the table changed.
        /// Returns false while there is nothing to pose against — a hull mid-swap, or a sprite hull —
        /// both of which are ordinary states and neither of which is an error.
        /// </summary>
        private bool Resolve()
        {
            if (_hull == null)
            {
                _hull = GetComponent<IsoFacetHullRenderer>();
                _posed = null;
            }
            if (_hull == null) return false;

            if (_posed == null) _posed = _hull.PosedMesh;
            if (_posed == null) return false;

            HullLamp[] wanted = _hull.Lamps;
            if (!ReferenceEquals(wanted, _lamps)) { _lamps = wanted; Build(); }

            return _lamps != null && _lamps.Length > 0;
        }

        private void Rebuild()
        {
            _lamps = null;      // force Resolve to rebuild against whatever the renderer now carries
            Resolve();
        }

        /// <summary>
        /// Make one child light per lamp, stamped with its kind's preset and this placement's trim.
        /// Children are named for their kind so a hierarchy at 6 a.m. reads as a boat rather than a
        /// pile of numbered quads.
        /// </summary>
        private void Build()
        {
            DestroyLights();
            if (_lamps == null || _lamps.Length == 0) { _lights = null; return; }

            _lights = new SceneLight[_lamps.Length];
            for (int i = 0; i < _lamps.Length; i++)
            {
                HullLamp lamp = _lamps[i];

                // The SEARCHLIGHT is not one of ours. It is a steerable beam that follows the bow and
                // lights the sea from inside the water's own shader, so BoatSpotlight owns it end to
                // end — mounted by the presentation service off the same declaration. Its slot stays
                // null and the pose loop skips it, which is why that loop tests for null at all.
                if (lamp.Kind == HullLampKind.Spotlight) continue;

                var go = new GameObject("Lamp_" + lamp.Kind) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, worldPositionStays: false);

                var light = go.AddComponent<SceneLight>();
                BoatLampPresets.Apply(light, lamp.Kind, lamp.SafeIntensityScale);
                // The lamp's HEIGHT for the shadows it throws (ADR 0016, lights PR B): her rig's own z,
                // metres up from the KEEL. The keel is the nearest thing her data offers to the plane
                // her casters stand on — she floats about a metre above it, so a sidelight's rake is
                // read a little steeper than truth. Stated, not hidden; the def carries no waterline.
                light.LampHeightMeters = Mathf.Max(0f, lamp.RigLocalMetres.z);
                _lights[i] = light;
            }

            ApplyCabinBoost();
            ApplyEnabled();
        }

        private void DestroyLights()
        {
            if (_lights == null) return;
            for (int i = 0; i < _lights.Length; i++)
            {
                SceneLight l = _lights[i];
                if (l == null) continue;
                if (Application.isPlaying) Destroy(l.gameObject);
                else DestroyImmediate(l.gameObject);
            }
            _lights = null;
        }

        private void OnDestroy() => DestroyLights();

        /// <summary>Every lamp follows the master switch and this component's own enabled state. The
        /// lights are DISABLED rather than destroyed — SceneLight pools its quad across an enable
        /// cycle, so a boat that goes dark and lights up again allocates nothing.</summary>
        private void ApplyEnabled()
        {
            if (_lights == null) return;
            bool on = _lampsOn && isActiveAndEnabled;
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i] != null) _lights[i].enabled = on;
        }

        // -------------------------------------------------------------------------------------------
        //  the cabin glow
        // -------------------------------------------------------------------------------------------

        private void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<CabinEntered>(OnCabinEntered);
            EventBus.Subscribe<CabinLeft>(OnCabinLeft);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<CabinEntered>(OnCabinEntered);
            EventBus.Unsubscribe<CabinLeft>(OnCabinLeft);
            _subscribed = false;
        }

        // A cabin on ANOTHER boat is not this boat's business — the same trap BoatCutaway records.
        // Eighteen lobster boats can lie in one creek, most of them the same def; a hull that took
        // every CabinEntered as her own would light her wheelhouse because somebody went below on a
        // sister ship two berths down.
        private void OnCabinEntered(CabinEntered e)
        {
            if (e.HullId != HullId) return;
            _cabinOccupied = true;
            ApplyCabinBoost();
        }

        private void OnCabinLeft(CabinLeft e)
        {
            if (e.HullId != HullId) return;
            _cabinOccupied = false;
            ApplyCabinBoost();
        }

        /// <summary>
        /// The id the cabin bus speaks in — the boat ROOT's, exactly as <c>BoatInterior</c> publishes
        /// it and <c>BoatCutaway</c> compares it. This component lives on her VISUAL CHILD (that is
        /// where the mesh renderer is installed), whose own id is a different number, so comparing
        /// against it would match nothing at all and the cabin would simply never brighten — a silent
        /// failure with no error to find it by.
        /// </summary>
        private EntityId HullId
        {
            get
            {
                Transform root = BoatRootOf(transform);
                return root != null ? root.gameObject.GetEntityId() : gameObject.GetEntityId();
            }
        }

        /// <summary>
        /// Re-stamp the cabin lamps at whichever brightness the room is at. Written from the PRESET
        /// every time rather than multiplied onto the live value, so entering and leaving a hundred
        /// times cannot ratchet the glow up or drift it down — the standing lesson that a value
        /// derived by repeated scaling is not a state.
        /// </summary>
        private void ApplyCabinBoost()
        {
            if (_lights == null || _lamps == null) return;
            for (int i = 0; i < _lights.Length && i < _lamps.Length; i++)
            {
                if (_lamps[i].Kind != HullLampKind.CabinGlow || _lights[i] == null) continue;
                float boost = _cabinOccupied ? Mathf.Max(0f, _cabinOccupiedBoost) : 1f;
                BoatLampPresets.Apply(_lights[i], HullLampKind.CabinGlow,
                                      _lamps[i].SafeIntensityScale * boost);
            }
        }
    }
}
