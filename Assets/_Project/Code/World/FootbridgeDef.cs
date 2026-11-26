using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A plank footbridge: a short deck a path takes over a brook. Its place, its size and how high its planks
    /// stand over the ground are here; the region's builder step stands it in the scene from this and nothing is
    /// set by hand. One bridge, one asset, one stable id.
    ///
    /// <para>The deck lies along the world's x. That is the standable deck's own rule (an upright rectangle of
    /// ground), so the path is laid to cross the water that way rather than the bridge being turned.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Footbridge", fileName = "Footbridge_")]
    public sealed class FootbridgeDef : ScriptableObject
    {
        [Tooltip("Stable id, bridge.snake_case. Append-only.")]
        public string Id = "bridge.example";

        [Tooltip("The name on the chart.")]
        public string DisplayName = "";

        [Tooltip("The key scene whose ground this bridge belongs to.")]
        public string KeySceneId = "";

        [Tooltip("The key scene's piece this bridge is (its id in the key scene's rows).")]
        public string PieceId = "";

        [Tooltip("The middle of its deck, world metres.")]
        public Vector2 At;

        [Header("The deck")]
        [Tooltip("Bank to bank, metres along the world's x.")]
        [Min(0.5f)] public float SpanMetres = 3f;

        [Tooltip("Across the walk, metres.")]
        [Min(0.5f)] public float WidthMetres = 1.15f;

        [Tooltip("The ground its footings stand on, metres above chart datum (the key scene's ground ask).")]
        public float GroundMetres;

        [Tooltip("How far the planks stand over that ground, metres.")]
        [Min(0f)] public float DeckRiseMetres = 0.38f;

        [Header("The rail")]
        [Tooltip("A rail along the seaward (south) edge. It is walked behind and cannot be walked through.")]
        public bool SeawardRail = true;

        [Tooltip("How thick the rail's line is on the ground, metres.")]
        [Min(0.02f)] public float RailThicknessMetres = 0.12f;

        /// <summary>The deck as ground: the rectangle that can be stood on.</summary>
        public Rect Footprint => new Rect(At.x - SpanMetres * 0.5f, At.y - WidthMetres * 0.5f, SpanMetres, WidthMetres);

        /// <summary>The height stood at on the planks, metres above chart datum.</summary>
        public float DeckMetres => GroundMetres + DeckRiseMetres;
    }
}
