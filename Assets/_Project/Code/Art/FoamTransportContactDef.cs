using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Authored wetted solid, NOT a walking deck. F2 visual mask only; never used by physics.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Art/Foam Transport Contact", fileName = "FoamTransportContact")]
    public sealed class FoamTransportContactDef : ScriptableObject
    {
        public enum Outline { Box, Circle }
        public string Id = "foam_contact.example";
        public Outline Shape = Outline.Box;
        [Tooltip("Centre in the component's local XY metres.")]
        public Vector2 Centre;
        [Tooltip("Box half extents; circle uses X as its radius. Author actual supports, not the deck above them.")]
        public Vector2 HalfSize = Vector2.one;
        [Tooltip("Bottom of the solid's contact interval in metres above chart datum.")]
        public float MinLevel;
        [Tooltip("Top of the solid's contact interval in metres above chart datum.")]
        public float MaxLevel = 1;
    }
}
