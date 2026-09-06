using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The ONE lamp the lit-decor path can see</b>, and the one place its five globals are written.
    ///
    /// <para><c>SpriteLitDecor.hlsl</c> lights every tree, shrub and shore plant in the game from a single
    /// published lamp — <c>_BoatLightPos</c> / <c>_BoatLightDir</c> / <c>_BoatLightColor</c> and two
    /// parameter vectors. It is a singleton for the reason ADR 0016 records: the decor path predates the
    /// four-slot <see cref="WaterLightBridge"/> and converting it wholesale would have dragged the whole
    /// lit-decor slice into a lighting PR.</para>
    ///
    /// <para><b>Why it is a class and not five <c>Shader.SetGlobal</c> calls at each site.</b> Two things
    /// now own a beam that lights decor — a boat's searchlight and the walker's headlamp
    /// (<see cref="Headlamp"/>) — and the world-lighting PR 3 charter's rule is <i>one publisher, never a
    /// second copy of the value</i>. Two independent copies of this packing would be two chances to pack
    /// the cone's cosines, the gate or the height differently, and the disagreement would show up as decor
    /// lit unlike the water by the same lamp. One writer, one packing, both callers.</para>
    ///
    /// <para><b>⚠️ The height rides in <c>pos.z</c> and that is SAFE:</b> <c>SpriteLitDecor.hlsl</c> reads
    /// <c>_BoatLightPos.xy</c> only — it takes its own elevation from the per-material
    /// <c>_LampElevation</c> — while the water's relief does read the height. A zero height reads as
    /// "unknown" and the relief is skipped.</para>
    /// </summary>
    public static class DecorLampGlobals
    {
        private static readonly int IdPos     = Shader.PropertyToID("_BoatLightPos");     // xy world, z height
        private static readonly int IdDir     = Shader.PropertyToID("_BoatLightDir");     // xy unit beam axis
        private static readonly int IdColor   = Shader.PropertyToID("_BoatLightColor");
        private static readonly int IdParams  = Shader.PropertyToID("_BoatLightParams");  // intensity, range, cosHalf, cosInner
        private static readonly int IdParams2 = Shader.PropertyToID("_BoatLightParams2"); // edge, gate threshold/softness/fallback

        /// <summary>
        /// Publish the one decor lamp. <paramref name="intensity"/> 0 is how a lamp says "I am not lit" —
        /// it is what an unlit boat writes, and it must keep working, so this never refuses a zero.
        /// </summary>
        public static void Publish(Vector2 lampWorld, float lampHeightMetres, Vector2 beamDir, Color color,
                                   float intensity, float range, float cosHalfAngle, float cosInnerAngle,
                                   float edgeSoftness, float gateThreshold, float gateSoftness,
                                   float gateFallback)
        {
            Shader.SetGlobalVector(IdPos, new Vector4(lampWorld.x, lampWorld.y, Mathf.Max(0f, lampHeightMetres), 0f));
            Shader.SetGlobalVector(IdDir, new Vector4(beamDir.x, beamDir.y, 0f, 0f));
            Shader.SetGlobalColor(IdColor, color);
            Shader.SetGlobalVector(IdParams,
                new Vector4(Mathf.Max(0f, intensity), Mathf.Max(0.01f, range), cosHalfAngle, cosInnerAngle));
            Shader.SetGlobalVector(IdParams2,
                new Vector4(Mathf.Clamp01(edgeSoftness), Mathf.Clamp01(gateThreshold),
                            Mathf.Clamp01(gateSoftness), Mathf.Clamp01(gateFallback)));
        }

        /// <summary>Publish a lamp straight off a <see cref="WaterLightState"/> — the packing the water
        /// bridge already composed, so a lamp cannot light decor unlike it lights the sea.</summary>
        public static void Publish(in WaterLightState s) =>
            Publish(s.LampWorld, s.LampHeightMeters, s.BeamDir, s.Color, s.Intensity, s.Range,
                    s.CosHalfAngle, s.CosInnerAngle, s.EdgeSoftness,
                    s.GateThreshold, s.GateSoftness, s.GateFallback);
    }
}
