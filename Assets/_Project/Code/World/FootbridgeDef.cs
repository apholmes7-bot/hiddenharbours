using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A plank footbridge: a short deck a path takes over a brook. Its place, its size, which way it runs and how
    /// high its planks stand over the ground are here; the region's builder step stands it in the scene from this
    /// and nothing is set by hand. One bridge, one asset, one stable id.
    ///
    /// <para>The deck runs along <see cref="AxisDegrees"/> (anticlockwise from the world's +x), set square to the
    /// brook it crosses, the way a bridge is built. The path is laid to meet each end along that same line, so a
    /// walker crosses straight in the direction the planks visibly go.</para>
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

        [Tooltip("Which way the deck runs, bank to bank: degrees anticlockwise from the world's +x. Square to the " +
                 "brook under it. The planks are drawn for this angle (tools/rig-recipes/gap-footbridge.mjs).")]
        [Range(-90f, 90f)] public float AxisDegrees;

        [Header("The deck")]
        [Tooltip("Bank to bank, metres along the deck's axis.")]
        [Min(0.5f)] public float SpanMetres = 3f;

        [Tooltip("Across the walk, metres.")]
        [Min(0.5f)] public float WidthMetres = 1.15f;

        [Tooltip("The ground its footings stand on, metres above chart datum (the key scene's ground ask).")]
        public float GroundMetres;

        [Tooltip("How far the planks stand over that ground, metres.")]
        [Min(0f)] public float DeckRiseMetres = 0.38f;

        [Header("The rails")]
        [Tooltip("A low rail along the seaward (south) edge. A walker on the planks is behind it; it cannot be walked through.")]
        public bool SeawardRail = true;

        [Tooltip("A low rail along the landward (north) edge. A walker on the planks is in front of it; it cannot be walked through.")]
        public bool LandwardRail = true;

        [Tooltip("How far behind its own line the landward rail sorts, metres. A walker is sorted by his body, which is " +
                 "drawn above his feet; without this the rail behind him would be drawn over his legs.")]
        [Min(0f)] public float LandwardRailSortsBehindMetres = 0.8f;

        [Header("Standing")]
        [Tooltip("How far landward (world +y) of the planks the deck still counts as stood on, metres. The game reads a " +
                 "walker's ground at his body, which is drawn above his feet; without this a walker whose feet are on " +
                 "the planks would be read as standing in the brook behind them.")]
        [Min(0f)] public float StandingReachMetres = 0.8f;

        [Tooltip("How thick a rail's line is on the ground, metres.")]
        [Min(0.02f)] public float RailThicknessMetres = 0.12f;

        [Header("Crossing")]
        [Tooltip("While his feet are on the planks (or on the path just off either end), a walker heading within this " +
                 "many degrees of the deck's axis goes straight along it at his full pace, so a keyboard's eight headings " +
                 "all cross the way the bridge runs instead of meeting a rail end or sliding along a rail. A wider " +
                 "heading is left as it is.")]
        [Range(0f, 90f)] public float GuideConeDegrees = 50f;

        [Tooltip("How far off each end of the planks, along the axis, the guide already holds, metres: the walk's last " +
                 "stretch onto the deck, so a walker whose heading is a little off the axis is set straight before his " +
                 "feet reach the rail ends rather than catching on one.")]
        [Min(0f)] public float GuideApproachMetres = 0.8f;

        /// <summary>The deck's axis, bank to bank: a unit vector in the world plane.</summary>
        public Vector2 Axis => AxisOf(AxisDegrees);

        /// <summary>Across the deck, landward (its north side): the axis turned a quarter anticlockwise.</summary>
        public Vector2 Across => new Vector2(-Axis.y, Axis.x);

        /// <summary>The planks' four corners, world metres: south-west end's seaward corner first, then round
        /// anticlockwise (east end seaward, east end landward, west end landward).</summary>
        public Vector2[] Corners => CornersOf(At, AxisDegrees, SpanMetres, WidthMetres);

        /// <summary>The height stood at on the planks, metres above chart datum.</summary>
        public float DeckMetres => GroundMetres + DeckRiseMetres;

        /// <summary>A unit vector <paramref name="degrees"/> anticlockwise from the world's +x.</summary>
        public static Vector2 AxisOf(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        /// <summary>A deck's four plank corners, in the order <see cref="Corners"/> gives.</summary>
        public static Vector2[] CornersOf(Vector2 at, float axisDegrees, float span, float width)
        {
            Vector2 a = AxisOf(axisDegrees) * (span * 0.5f), n = new Vector2(-AxisOf(axisDegrees).y, AxisOf(axisDegrees).x) * (width * 0.5f);
            return new[] { at - a - n, at + a - n, at + a + n, at - a + n };
        }
    }
}
