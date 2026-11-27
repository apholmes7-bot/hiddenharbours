using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One wharf face's fender run</b>: where its fender strings hang, so fender placement is data and
    /// not a literal in a scene builder (rules 2 and 6).
    ///
    /// <para><b>What a string covers.</b> Each string is a timber fender pile bolted down the face from the
    /// deck lip to below the region's lowest water, with tyres lashed to it across the rub band (lowest
    /// water to highest water plus <see cref="FreeboardMetres"/>). The art is baked per deck height
    /// (<see cref="ArtKey"/>); the band is checked against the region's tide, which is recomputed, not
    /// stored here.</para>
    ///
    /// <para><b>Where it hangs.</b> <see cref="HangerXs"/> are world x positions along the face; every
    /// string's pivot sits on the drawn deck lip (<see cref="LipWorldY"/>, at
    /// <see cref="DeckElevationMetres"/>), so the tidal-face cut puts its lower tyres under water at high
    /// tide and dries them at low.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/WharfFenderRunDef", fileName = "FenderRun")]
    public sealed class WharfFenderRunDef : ScriptableObject
    {
        [Tooltip("Stable, append-only id (fender_run.<region>_<face>).")]
        public string Id = "fender_run.unnamed";

        [Tooltip("The region scene this face stands in (its scene name).")]
        public string RegionScene = "";

        [Tooltip("The baked fender-string art for this deck height (the SetPieceDef key, " +
                 "Data/SetPieces/SetPiece_<key>.asset).")]
        public string ArtKey = "";

        [Tooltip("World y of the drawn deck lip the strings hang from.")]
        public float LipWorldY;

        [Tooltip("Elevation of that deck lip above datum, in metres.")]
        public float DeckElevationMetres;

        [Tooltip("How far above the region's highest water the tyres must reach, in metres: a hull rides " +
                 "this much above the waterline, so its rubbing strake meets the fenders at spring high.")]
        [Min(0f)] public float FreeboardMetres = 1f;

        [Tooltip("World x of every string on this face, west to east.")]
        public float[] HangerXs = new float[0];
    }
}
