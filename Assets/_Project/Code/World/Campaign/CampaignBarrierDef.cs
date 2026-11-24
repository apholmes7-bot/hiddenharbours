using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// An authored campaign barrier at one edge of one region: a local storm that stands until a
    /// campaign step is done and turns the player's boat back. One asset per wall (ADR 0003), loaded
    /// from <c>Resources/CampaignBarriers</c> by <see cref="CampaignBarrierDirector"/>; nothing is
    /// placed in the region's scene.
    ///
    /// <para>It is NOT the world's weather. The deterministic sim is not read for it, changed by it
    /// or saved with it; the wall is a function of where the boat is and one saved flag.</para>
    ///
    /// <para>Distances are metres INSIDE the edge, measured along <see cref="InwardNormal"/>. They
    /// run inward in this order: the limit line, the full-storm line, the start line.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Campaign Barrier", fileName = "CampaignBarrier")]
    public sealed class CampaignBarrierDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (campaign_barrier.snake_case).")]
        public string Id = "campaign_barrier.";
        [TextArea] public string Description = "";

        [Header("Where")]
        [Tooltip("The region the wall stands in (region.snake_case).")]
        public string RegionId = "region.";
        [Tooltip("A point on the region's edge, the region's own metres.")]
        public Vector2 EdgePoint;
        [Tooltip("Unit direction from the edge back into the region. The boat is pushed this way.")]
        public Vector2 InwardNormal = Vector2.up;
        [Tooltip("Half the wall's length along the edge, metres, centred on Edge Point. 0 = the whole edge.")]
        [Min(0f)] public float HalfLengthMetres;

        [Header("When it lifts")]
        [Tooltip("The saved campaign flag that lifts the wall for good. While it is unset the wall stands.")]
        public string LiftedByFlag = "";

        [Header("How far in it reaches (metres inside the edge)")]
        [Tooltip("Where the weather first turns and the push begins.")]
        [Min(0f)] public float StartDistanceMetres = 170f;
        [Tooltip("Where the storm is at full strength.")]
        [Min(0f)] public float FullDistanceMetres = 90f;
        [Tooltip("The line no boat gets past. It must lie inside any exit door on this edge.")]
        [Min(0f)] public float LimitDistanceMetres = 60f;

        [Header("How hard it pushes")]
        [Tooltip("Push at full storm, m/s² on the hull whatever she weighs: a dory and a trawler are " +
                 "turned back alike.")]
        [Min(0f)] public float PushAcceleration = 1.2f;
        [Tooltip("Past the limit line the push grows by this much per metre (m/s² per m).")]
        [Min(0f)] public float LimitStiffness = 2f;
        [Tooltip("Past the limit line, speed toward the edge is braked by this much per m/s (1/s).")]
        [Min(0f)] public float LimitDamping = 2f;
        [Tooltip("The most the wall ever pushes, m/s². It must beat every hull's own drive; it is also " +
                 "what keeps a boat found far past the limit from being thrown.")]
        [Min(0f)] public float MaxPushAcceleration = 8f;

        [Header("How it looks and sounds at full strength")]
        [Tooltip("Wind laid over what the player sees and hears, m/s, blowing along Inward Normal.")]
        [Min(0f)] public float StormWindSpeed = 22f;
        [Tooltip("Visibility in the storm (0 thick .. 1 clear).")]
        [Range(0f, 1f)] public float StormVisibility01 = 0.2f;
        [Tooltip("Sea state in the storm (0 glassy .. 1 storm).")]
        [Range(0f, 1f)] public float StormSeaState01 = 0.9f;

        [Tooltip("Metres inside the edge where the look and sound are at full strength. 0 = at the full " +
                 "line. Set it further in than the full line (where a rowed boat is brought to a stop) so " +
                 "the player meets the whole storm, not a share of it.")]
        [Min(0f)] public float LookFullDistanceMetres;
        [Tooltip("Rain the storm brings at full strength, whatever the sky is doing (0 dry .. 1 a downpour).")]
        [Range(0f, 1f)] public float StormRain01;
        [Tooltip("How much wider (x) and longer (y) a streak of that rain is drawn than ordinary rain. " +
                 "(1, 1) = ordinary.")]
        public Vector2 StormRainStreakScale = Vector2.one;

        [Header("The dark front seen from a boat before the weather")]
        [Tooltip("Metres inside the edge where the dark front first shows at the side of the view. " +
                 "0 = no warning: the dark comes with the weather itself.")]
        [Min(0f)] public float SightDistanceMetres;
        [Tooltip("How dark the sky is under the front (0 = draw no front at all .. 1 black).")]
        [Range(0f, 1f)] public float SquallDarkness01;
        [Tooltip("How soft the front's leading edge is, as a share of the view (0 hard .. 1 the whole view).")]
        [Range(0f, 1f)] public float SquallFeather01 = 0.5f;
        [Tooltip("The colour of the dark.")]
        public Color SquallTint = new Color(0.05f, 0.07f, 0.1f, 1f);

        [Header("The line she thinks")]
        [TextArea(2, 4)]
        [Tooltip("One plain line telling the player why. Empty = say nothing.")]
        public string Line = "";
        [Tooltip("How far into the storm (0..1) she is when she thinks it.")]
        [Range(0f, 1f)] public float LineAtStorm01 = 0.35f;
        [Tooltip("It is thought once each time the boat comes into the weather, and never again while " +
                 "she stays in it. This is the least seconds between two of those times.")]
        [Min(0f)] public float LineCooldownSeconds = 60f;

        /// <summary>The share of the front that has crossed the view when the boat reaches the start
        /// line: the front's soft edge is then overhead, and the rest comes on with the weather.</summary>
        public const float LoomShareAtTheStartLine = 0.5f;

        private Vector2 Normal => InwardNormal.sqrMagnitude > 0f ? InwardNormal.normalized : Vector2.up;

        /// <summary>The unit direction the boat is pushed: back into the region.</summary>
        public Vector2 PushDirection => Normal;

        /// <summary>Metres inside the edge. Negative = beyond it.</summary>
        public float DistanceInside(Vector2 position) => Vector2.Dot(position - EdgePoint, Normal);

        /// <summary>True when <paramref name="position"/> is abreast of the wall along its edge.</summary>
        public bool Abreast(Vector2 position)
        {
            if (HalfLengthMetres <= 0f) return true;
            Vector2 n = Normal;
            float along = Vector2.Dot(position - EdgePoint, new Vector2(n.y, -n.x));
            return Mathf.Abs(along) <= HalfLengthMetres;
        }

        /// <summary>How much of the storm stands at <paramref name="position"/>: 0 at the start line
        /// and further in, 1 from the full line out to the edge and beyond. Smooth between.</summary>
        public float Storm01At(Vector2 position)
        {
            if (!Abreast(position)) return 0f;
            float d = DistanceInside(position);
            if (StartDistanceMetres <= FullDistanceMetres) return d <= FullDistanceMetres ? 1f : 0f;
            float t = Mathf.InverseLerp(StartDistanceMetres, FullDistanceMetres, d);
            return t * t * (3f - 2f * t);
        }

        /// <summary>How much of the storm is DRAWN and HEARD at <paramref name="position"/>: 0 at the
        /// start line and further in, 1 from <see cref="LookFullDistanceMetres"/> (or the full line)
        /// out to the edge. Never less than <see cref="Storm01At"/>.</summary>
        public float Look01At(Vector2 position)
        {
            if (!Abreast(position)) return 0f;
            float full = Mathf.Max(LookFullDistanceMetres, FullDistanceMetres);
            float d = DistanceInside(position);
            if (StartDistanceMetres <= full) return d <= full ? 1f : 0f;
            float t = Mathf.InverseLerp(StartDistanceMetres, full, d);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// How much of the dark front is across the view of a boat at <paramref name="position"/>:
        /// 0 at <see cref="SightDistanceMetres"/> and further in, <see cref="LoomShareAtTheStartLine"/>
        /// at the start line, 1 where the look is full. Smooth all the way. With no sight distance
        /// (or one inside the start line) it is the look itself.
        /// </summary>
        public float Loom01At(Vector2 position)
        {
            float look = Look01At(position);
            if (SightDistanceMetres <= StartDistanceMetres || !Abreast(position)) return look;
            float t = Mathf.InverseLerp(SightDistanceMetres, StartDistanceMetres, DistanceInside(position));
            float seen = t * t * (3f - 2f * t);
            return LoomShareAtTheStartLine * seen + (1f - LoomShareAtTheStartLine) * look;
        }

        /// <summary>
        /// The push on a hull at <paramref name="position"/>, m/s² along <see cref="PushDirection"/>;
        /// never negative. It is the storm's own push, plus (past the limit line) a push that grows
        /// with every metre and a brake on whatever speed she still carries toward the edge, up to
        /// <see cref="MaxPushAcceleration"/>.
        /// </summary>
        public float PushAccelerationAt(Vector2 position, Vector2 velocity)
        {
            float storm = Storm01At(position);
            if (storm <= 0f) return 0f;
            float push = PushAcceleration * storm;
            float past = LimitDistanceMetres - DistanceInside(position);
            if (past > 0f)
            {
                push += LimitStiffness * past;
                float outward = -Vector2.Dot(velocity, Normal);
                if (outward > 0f) push += LimitDamping * outward;
            }
            return Mathf.Min(push, MaxPushAcceleration);
        }

        /// <summary>The wind the storm lays over the look at full strength, m/s.</summary>
        public Vector2 StormWind => Normal * StormWindSpeed;
    }
}
