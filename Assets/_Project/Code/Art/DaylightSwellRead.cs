using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Arithmetic twin of the water shader's pre-grade swell read. The adoption switch is
    /// owner policy, not a blend. Inputs are the existing signed wave signal and calm gate;
    /// this reference never samples or changes the physical wave field.
    /// </summary>
    public static class DaylightSwellRead
    {
        /// <summary>
        /// Mirrors the fragment's guard, optional quantizer and switch. Switch values below
        /// 0.5 select the original absolute read; values at/above 0.5 select relative contrast.
        /// Strength zero returns the input exactly, while switch zero retains today's law.
        /// </summary>
        public static Vector3 Apply(Vector3 colour, float signedBand, float strength,
                                   float gate, float bands, float relativeSwitch)
        {
            if (!(strength > 0.001f && gate > 0.001f)) return colour;

            float readBand = signedBand;
            if (bands >= 1f)
            {
                float b01 = readBand * 0.5f + 0.5f;
                b01 = Mathf.Floor(b01 * bands + 0.5f) / bands;
                readBand = b01 * 2f - 1f;
            }

            if (relativeSwitch >= 0.5f)
                return Relative(colour, readBand, strength, gate);

            // Preserve the legacy multiplication order, including its unclamped strength/band.
            float amount = readBand * strength * gate * 0.25f;
            return colour + new Vector3(amount, amount, amount);
        }

        /// <summary>
        /// Twin of HHDaylightSwellReadRelative in Include/DaylightSwellRead.hlsl.
        /// For a positive input channel the read changes at most strength*gate of that channel.
        /// Gate is the caller's existing [0,1] smoothstep; a negative input is left alone.
        /// </summary>
        public static Vector3 Relative(Vector3 colour, float readBand, float strength, float gate)
        {
            if (strength <= 0.001f || gate <= 0.001f) return colour;
            float amount = Mathf.Clamp(readBand, -1f, 1f) * Mathf.Clamp01(strength) * gate;
            return colour + new Vector3(Mathf.Max(colour.x, 0f), Mathf.Max(colour.y, 0f),
                                        Mathf.Max(colour.z, 0f)) * amount;
        }
    }
}
