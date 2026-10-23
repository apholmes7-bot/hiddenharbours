using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Authored glass basins, independent of tide and ocean bearing. Bounds are world metres:
    /// xy centre, zw half extents. Empty extents disable a basin. The outer feather is metres;
    /// zero gives a sharp boundary. Region data, separate from the shared ocean climate.</summary>
    [Serializable]
    public struct SwellShelterSettings
    {
        [Tooltip("Harbour glass bounds, world metres: xy centre, zw nonnegative half extents. Zero extents disable.")]
        public Vector4 Harbour;
        [Tooltip("Sheltered cove glass bounds, world metres: xy centre, zw nonnegative half extents. Zero extents disable.")]
        public Vector4 Cove;
        [Tooltip("Outer transition width, metres, 0..32. Zero gives a sharp edge; basins remain exactly glass inside.")]
        [Range(0f, 32f)] public float FeatherMeters;

        public float TransmissionAt(Vector2 position)
            => BasinTransmission(position, Harbour, FeatherMeters) * BasinTransmission(position, Cove, FeatherMeters);

        public static float BasinTransmission(Vector2 position, Vector4 bounds, float feather)
        {
            if (bounds.z <= 0f || bounds.w <= 0f) return 1f;
            float distance = Mathf.Max(Mathf.Abs(position.x - bounds.x) - bounds.z,
                                       Mathf.Abs(position.y - bounds.y) - bounds.w);
            return WaveFetch.SmoothstepEdge(0f, Mathf.Max(0f, feather), distance);
        }
    }
}
