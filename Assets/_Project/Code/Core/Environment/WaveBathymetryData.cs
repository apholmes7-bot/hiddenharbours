using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Imported CPU pixels and layout. Stage D part a has no scene or service consumer.</summary>
    public sealed class WaveBathymetryData : ScriptableObject
    {
        public string Id;
        public int FormatVersion, Width, Height, CellSizeMm, MinXmm, MinYmm;
        public int[] DirectionX, DirectionY, WaterLevelsMm, EntryDepthsMm;
        // Little-endian RGBAFloat, slice = level * binCount + bin, bottom-up rows.
        // RG: quantized unit direction; B: height factor; A: optical distance in metres.
        public byte[] Pixels;
    }
}
