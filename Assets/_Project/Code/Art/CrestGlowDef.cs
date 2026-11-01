using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>A review arm for light through a crest. No material or shader default is changed.
    /// Select one on a water type only after reviewing the plates; null leaves today's sea untouched.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Crest Glow", fileName = "CrestGlow")]
    public sealed class CrestGlowDef : ScriptableObject
    {
        [Tooltip("Stable id, crest_glow.snake_case.")]
        public string Id;
        [Tooltip("Envelope-relative onset. Positive heights only; troughs never glow.")]
        [Range(0f, 1f)] public float Onset;
        [Tooltip("Metres of water crossed by the light, using the water type's own absorption.")]
        [Range(0f, 4f)] public float ThicknessMeters;
        [Tooltip("Posterize levels. The first threshold is 1/(2*bands). Sent through the property block; " +
                 "the shader inspector's range does not limit this data value.")]
        [Min(1)] public int Bands;
        [Tooltip("Add strength. Zero is an exact passthrough; maximum matches the shader's strength dial.")]
        [Range(0f, 2f)] public float Strength;

        public bool IsValid() => !string.IsNullOrWhiteSpace(Id)
            && Id.StartsWith("crest_glow.", StringComparison.Ordinal)
            && Onset > 0f && Onset <= CrestGlow.MaxTopOnset
            && ThicknessMeters >= 0f && ThicknessMeters <= 4f
            && Bands >= 1 && Strength >= 0f && Strength <= 2f;
    }
}
