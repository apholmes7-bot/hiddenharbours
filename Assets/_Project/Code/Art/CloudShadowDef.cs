using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Cloud Shadows", fileName = "CloudShadowDef")]
    public sealed class CloudShadowDef : ScriptableObject
    {
        public string Id = "cloud_shadows.default";
        public bool Enabled = true;
        public Shader Shader;
        public AnimationCurve Strength = new AnimationCurve(new Keyframe(0, .12f), new Keyframe(.25f, .18f),
            new Keyframe(.5f, .28f), new Keyframe(.85f, .08f), new Keyframe(1, 0));
        public AnimationCurve Coverage = new AnimationCurve(new Keyframe(0, .4f), new Keyframe(.25f, .5f),
            new Keyframe(.5f, .65f), new Keyframe(.85f, .9f), new Keyframe(1, 1));
        [Range(0, 1)] public float FullSunElevation = .25f;
        [Min(2)] public int NoiseSize = 64;
        [Min(0)] public float ScaleMetres = 1024;
        [Min(0)] public float DriftMetresPerGameSecondPerMps = .001f;
        [Min(0)] public float SecondaryScale = 1.73f;
        [Min(0)] public float SecondaryRate = .71f;
        [Range(0, 1)] public float SecondaryWeight = .35f;
        [Range(0, 1)] public float Softness = .075f;
        [Tooltip("Ground plane used to reconstruct the camera's world XY.")]
        public float WorldPlaneZ;
        [Min(1)] public float CullingOversize = 1.1f;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Id) && Id.StartsWith("cloud_shadows.", StringComparison.Ordinal)
                && Shader != null && CurveValid(Strength) && CurveValid(Coverage)
                && Positive(FullSunElevation) && FullSunElevation <= 1
                && NoiseSize >= 2 && NoiseSize <= 256 && (NoiseSize & (NoiseSize - 1)) == 0
                && Positive(ScaleMetres) && Finite(DriftMetresPerGameSecondPerMps) && DriftMetresPerGameSecondPerMps >= 0
                && Positive(SecondaryScale) && Positive(SecondaryRate)
                && Finite(SecondaryWeight) && SecondaryWeight >= 0 && SecondaryWeight <= 1
                && Positive(Softness) && Softness <= 1 && Finite(WorldPlaneZ)
                && Finite(CullingOversize) && CullingOversize >= 1;
        }

        static bool Positive(float x) => Finite(x) && x > 0;
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static bool CurveValid(AnimationCurve curve)
        {
            if (curve == null || curve.length < 2 || curve[0].time != 0 || curve[curve.length - 1].time != 1) return false;
            for (int i = 0; i < curve.length; i++)
            {
                var key = curve[i];
                if (!Finite(key.time) || !Finite(key.value) || !Finite(key.inTangent) || !Finite(key.outTangent)
                    || key.time < 0 || key.time > 1 || key.value < 0 || key.value > 1
                    || (i > 0 && key.time <= curve[i - 1].time)) return false;
            }
            return true;
        }
    }
}
