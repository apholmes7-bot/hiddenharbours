using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// ONE water light's per-frame state — everything the water shader needs to light the sea with it. A pure
    /// value type: no scene, no GPU, no time-of-call state, so the packing into shader vectors is unit-tested
    /// headless (CLAUDE.md rule 5).
    ///
    /// <para><b>Why <see cref="LampHeightMeters"/> exists.</b> The beam's WAVE RELIEF (the owner's 2026-08-28
    /// mandate) needs to know how high the lamp sits, because that is the whole difference between a lamp and
    /// the sun: a high lamp shines nearly straight down and flattens the sea toward a disc, a low one rakes it
    /// and separates crest from trough hard. It is the lever behind his "unless the proper light angle exposes
    /// them". A height of 0 means "not known", and the shader then skips the relief entirely and draws the flat
    /// ADR 0016 cone exactly as it shipped.</para>
    /// </summary>
    public struct WaterLightState
    {
        /// <summary>Lamp world position on the ground plane (metres).</summary>
        public Vector2 LampWorld;

        /// <summary>Lamp height above mean water (metres). 0 = unknown, so the shader skips the wave relief.</summary>
        public float LampHeightMeters;

        /// <summary>Beam axis in world space (~unit length).</summary>
        public Vector2 BeamDir;

        /// <summary>Beam colour.</summary>
        public Color Color;

        /// <summary>Effective intensity; <c>0</c> or less is the shader's "no beam".</summary>
        public float Intensity;

        /// <summary>Throw distance (metres).</summary>
        public float Range;

        /// <summary>Cosine of the cone's outer half-angle.</summary>
        public float CosHalfAngle;

        /// <summary>Cosine of the fully-lit inner cone angle (at least <see cref="CosHalfAngle"/>).</summary>
        public float CosInnerAngle;

        /// <summary>Radial edge softness (0 hard disc .. 1 soft halo).</summary>
        public float EdgeSoftness;

        /// <summary>Night-gate threshold, softness, and cycle-off fallback (see <see cref="LightMath.NightGate"/>).</summary>
        public float GateThreshold, GateSoftness, GateFallback;

        /// <summary>A light with no intensity lights nothing, and must not consume one of the few slots.</summary>
        public bool IsLive => Intensity > 0f;
    }

    /// <summary>Anything that lights the WATER. Implemented by <see cref="BoatSpotlight"/> today.</summary>
    public interface IWaterLightEmitter
    {
        /// <summary>This emitter's light for this frame; <c>false</c> when it is dark or not lighting water.</summary>
        bool TryGetWaterLight(out WaterLightState state);
    }

    /// <summary>
    /// Publishes the water's LIGHTS to the global shader uniforms the water fragment reads — the array the
    /// ADR 0016 single-light note reserved ("the clean extension to many is to publish ARRAYS + a count and
    /// loop"). Self-installing on the <see cref="WaveFieldBridge"/> / <c>GrassWindBridge</c> pattern: one
    /// hidden <c>[DontDestroyOnLoad]</c> host spawned before the first scene, so every scene's water reads
    /// every live beam with no wiring and no builder re-run.
    ///
    /// <para><b>ONE owner of the array.</b> Emitters register and are ASKED for their state here, so they
    /// cannot race each other writing it. (The single-light <c>_BoatLight*</c> globals are still written by
    /// <see cref="BoatSpotlight"/> itself, and those DO still resolve last-writer-wins — which is exactly the
    /// behaviour they have shipped with, deliberately left alone by this change.)</para>
    ///
    /// <para><b>Why the legacy singleton stays.</b> <c>_BoatLight*</c> is read by a SECOND lit path:
    /// <c>SpriteLitDecor.hlsl</c> lights trees, shrubs and shore plants from that one lamp. Two lit paths are
    /// deliberate architecture, so this publishes the array ALONGSIDE the singleton and changes neither the
    /// singleton's contract nor the decor path. The water shader sums the ARRAY when the count is live and
    /// falls back to the singleton when it is 0 — never both, or the primary lamp would be counted twice.</para>
    ///
    /// <para><b>Budget (rule 7).</b> <see cref="MaxLights"/> = 4 slots, so the per-pixel cost of the water's
    /// beam term is bounded at four cone evaluations however many lamps the scene grows. The shader loop is
    /// <c>[unroll]</c>ed over that FIXED bound with the count masking inside (the shape
    /// <c>WaveFieldSample</c> already uses for its eight train slots), and every slot early-outs on intensity,
    /// so a scene with one searchlight pays for one. Nearest-to-camera wins the slots, so the lights that
    /// matter on screen are the ones that get them.</para>
    ///
    /// <para><b>Determinism and seams (rules 4, 5).</b> Visual-only: it sets render globals, reads no sim and
    /// saves nothing. It references no other feature module — emitters reach it through
    /// <see cref="IWaterLightEmitter"/>, which lives here in Art beside it.</para>
    /// </summary>
    public sealed class WaterLightBridge : MonoBehaviour
    {
        /// <summary>
        /// How many water lights the shader can carry at once. MUST equal <c>WATER_MAX_LIGHTS</c> in
        /// HiddenHarboursWater.shader — pinned by a source assertion in the tests so the two cannot drift.
        /// </summary>
        public const int MaxLights = 4;

        // The live emitters. Static so a spotlight can register from its own OnEnable without finding the host.
        private static readonly List<IWaterLightEmitter> Emitters = new List<IWaterLightEmitter>(8);

        // The chosen slots for this frame and their camera distances (parallel, insertion-sorted).
        private static readonly WaterLightState[] Chosen = new WaterLightState[MaxLights];
        private static readonly float[] ChosenDistSq = new float[MaxLights];

        // The packed shader arrays — allocated ONCE and rewritten in place (rule 7: no per-frame allocation).
        private static readonly Vector4[] PosArray = new Vector4[MaxLights];
        private static readonly Vector4[] DirArray = new Vector4[MaxLights];
        private static readonly Vector4[] ColorArray = new Vector4[MaxLights];
        private static readonly Vector4[] ParamsArray = new Vector4[MaxLights];
        private static readonly Vector4[] Params2Array = new Vector4[MaxLights];

        private static readonly int IdPos = Shader.PropertyToID("_WaterLightPos");
        private static readonly int IdDir = Shader.PropertyToID("_WaterLightDir");
        private static readonly int IdColor = Shader.PropertyToID("_WaterLightColor");
        private static readonly int IdParams = Shader.PropertyToID("_WaterLightParams");
        private static readonly int IdParams2 = Shader.PropertyToID("_WaterLightParams2");
        private static readonly int IdCount = Shader.PropertyToID("_WaterLightCount");

        private Camera _camera;

        /// <summary>Register a water-light emitter (from its <c>OnEnable</c>). Ignores nulls and duplicates.</summary>
        public static void Register(IWaterLightEmitter emitter)
        {
            if (emitter == null || Emitters.Contains(emitter)) return;
            Emitters.Add(emitter);
        }

        /// <summary>Unregister (from its <c>OnDisable</c>), so a dead lamp cannot hold a slot.</summary>
        public static void Unregister(IWaterLightEmitter emitter)
        {
            if (emitter == null) return;
            Emitters.Remove(emitter);
        }

        /// <summary>How many emitters are registered right now. For tests and diagnostics only.</summary>
        public static int RegisteredCount => Emitters.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // A domain reload clears statics, but "Enter Play Mode without domain reload" does not: start every
            // session from an empty registry so a previous session's destroyed emitters cannot hold slots.
            Emitters.Clear();
            var host = new GameObject("WaterLightBridge") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<WaterLightBridge>();
        }

        /// <summary>
        /// Publish AFTER the emitters have moved and re-aimed for the frame (they run in <c>Update</c>), so the
        /// beam the water lights itself with is this frame's beam and not last frame's.
        /// </summary>
        private void LateUpdate() => PublishFromRegistry();

        /// <summary>A stopped play session must not leave a stale beam burning on the editor's globals.</summary>
        private void OnDisable() => PublishSlots(0);

        /// <summary>
        /// Gather the live emitters, keep the <see cref="MaxLights"/> nearest the camera, and publish them.
        /// Public so a fixture can drive one deterministic frame without a play session.
        /// </summary>
        public void PublishFromRegistry()
        {
            if (_camera == null) _camera = Camera.main;
            // No camera (a bare harness): distance ordering is meaningless, so registration order decides.
            Vector2 eye = _camera != null ? (Vector2)_camera.transform.position : Vector2.zero;

            int count = 0;
            for (int i = 0; i < Emitters.Count; i++)
            {
                IWaterLightEmitter emitter = Emitters[i];
                if (emitter == null) continue;
                if (!emitter.TryGetWaterLight(out WaterLightState state)) continue;
                if (!state.IsLive) continue;                        // a dark lamp must not hold a slot
                count = Insert(state, (state.LampWorld - eye).sqrMagnitude, count);
            }

            for (int i = 0; i < count; i++) Pack(i, Chosen[i]);
            PublishSlots(count);
        }

        /// <summary>
        /// Insertion-sort <paramref name="state"/> into the nearest-N slots by <paramref name="distSq"/> and
        /// return the new live count. O(N) with N = <see cref="MaxLights"/> and allocation-free, so it stays
        /// cheap however many emitters register.
        /// </summary>
        private static int Insert(WaterLightState state, float distSq, int count)
        {
            int at = count;
            while (at > 0 && ChosenDistSq[at - 1] > distSq) at--;    // the slot this light belongs in
            if (at >= MaxLights) return count;                       // farther than every kept light -> dropped

            for (int j = Mathf.Min(count, MaxLights - 1); j > at; j--)
            {
                Chosen[j] = Chosen[j - 1];
                ChosenDistSq[j] = ChosenDistSq[j - 1];
            }
            Chosen[at] = state;
            ChosenDistSq[at] = distSq;
            return Mathf.Min(count + 1, MaxLights);
        }

        /// <summary>
        /// Pack one light into the shader vectors. The layout MIRRORS the single-light <c>_BoatLight*</c>
        /// globals exactly (so the shader can read both through one function), with the ONE addition the wave
        /// relief needs: the lamp HEIGHT in <c>pos.z</c>, which the legacy globals leave at 0.
        /// </summary>
        private static void Pack(int slot, WaterLightState s)
        {
            PosArray[slot] = new Vector4(s.LampWorld.x, s.LampWorld.y, Mathf.Max(0f, s.LampHeightMeters), 0f);
            DirArray[slot] = new Vector4(s.BeamDir.x, s.BeamDir.y, 0f, 0f);
            ColorArray[slot] = new Vector4(s.Color.r, s.Color.g, s.Color.b, s.Color.a);
            ParamsArray[slot] = new Vector4(Mathf.Max(0f, s.Intensity), Mathf.Max(0.01f, s.Range),
                                            s.CosHalfAngle, s.CosInnerAngle);
            Params2Array[slot] = new Vector4(Mathf.Clamp01(s.EdgeSoftness), Mathf.Clamp01(s.GateThreshold),
                                             Mathf.Clamp01(s.GateSoftness), Mathf.Clamp01(s.GateFallback));
        }

        /// <summary>
        /// Push the arrays and the live count. The arrays are ALWAYS sent at full <see cref="MaxLights"/>
        /// length (Unity fixes a global array's length at its first set) and the count masks the live slots —
        /// the same fixed-bound-plus-count discipline the wave field's eight train slots use. Slots at or above
        /// the count keep whatever they last held and are never read, so they are not cleared.
        /// </summary>
        private static void PublishSlots(int count)
        {
            Shader.SetGlobalVectorArray(IdPos, PosArray);
            Shader.SetGlobalVectorArray(IdDir, DirArray);
            Shader.SetGlobalVectorArray(IdColor, ColorArray);
            Shader.SetGlobalVectorArray(IdParams, ParamsArray);
            Shader.SetGlobalVectorArray(IdParams2, Params2Array);
            Shader.SetGlobalFloat(IdCount, Mathf.Clamp(count, 0, MaxLights));
        }
    }
}
