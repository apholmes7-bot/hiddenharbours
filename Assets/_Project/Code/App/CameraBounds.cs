using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The camera's world rectangle and the clamp that keeps the view inside it — the bounds rig
    /// <see cref="CameraFollow"/>'s own comment said did not exist yet (scene-sizing §6 item 4).
    ///
    /// <para><b>Why now.</b> At the greybox's 160 m nobody noticed; St Peters is authored at
    /// <b>760 × 520 m</b> and the camera would sail straight off the painted map — past the seabed
    /// bake, past the sea mesh, into whatever the backdrop happens to clear to. That lane is building
    /// at that size now.</para>
    ///
    /// <para><b>One extent, not a second one.</b> The rectangle is <c>RegionDef.WorldCenter</c> /
    /// <c>WorldSizeMeters</c> — the same authored numbers the tiled sea sprite, the flat backdrop, the
    /// shader's height bake and the displaced mesh already read. It reaches this component the way it
    /// reaches those: the region builder passes it in (<see cref="CameraFollow.ConfigureBounds"/>),
    /// exactly as <c>WaterSurface.ConfigurePaintedHeightMap</c> receives the same pair. Inventing a
    /// separately-authored camera rectangle would be the eight-copies mistake all over again — two
    /// numbers that agree until someone edits one.</para>
    ///
    /// <para>Pure and engine-light on purpose: <see cref="Clamp"/> is a function of
    /// (rect, half-extents, desired position), so the whole rig is provable headless without a camera,
    /// a scene, or play mode — the <see cref="CameraZoomPolicy"/> convention.</para>
    /// </summary>
    public readonly struct CameraBounds
    {
        /// <summary>Centre of the allowed world rectangle (m).</summary>
        public readonly Vector2 Center;

        /// <summary>Full size of the allowed world rectangle (m). Either axis ≤ 0 means UNBOUNDED —
        /// the "not configured" state, under which <see cref="Clamp"/> is the identity and every
        /// existing scene behaves exactly as it did before this rig existed.</summary>
        public readonly Vector2 SizeMeters;

        public CameraBounds(Vector2 center, Vector2 sizeMeters)
        {
            Center = center;
            SizeMeters = new Vector2(Mathf.Max(0f, sizeMeters.x), Mathf.Max(0f, sizeMeters.y));
        }

        /// <summary>True when a real rectangle was configured on BOTH axes.</summary>
        public bool IsBounded => SizeMeters.x > 0f && SizeMeters.y > 0f;

        /// <summary>The unbounded default — <c>default(CameraBounds)</c>, size zero.</summary>
        public static readonly CameraBounds Unbounded = default;

        /// <summary>
        /// Clamp a desired camera centre so the visible rectangle stays inside the world rectangle.
        ///
        /// <para><paramref name="halfWidthMeters"/> / <paramref name="halfHeightMeters"/> are the
        /// camera's half-extents <b>at the current zoom</b> — for an orthographic camera,
        /// <c>halfHeight = orthographicSize</c> and <c>halfWidth = orthographicSize × aspect</c>. They
        /// are passed in rather than derived here so the clamp stays pure, and so it is automatically
        /// correct across the discrete pixel-perfect zoom tiers and <em>during</em> an upgrade/deck
        /// zoom tween: a smaller view is allowed nearer the edge, and the allowance shrinks and grows
        /// with the step. Nothing here knows about tiers, which is precisely why nothing here can go
        /// stale when one is added.</para>
        ///
        /// <para><b>A region narrower than the view CENTRES.</b> When the visible width exceeds the
        /// world width there is no valid range to clamp into — the naive
        /// <c>Clamp(x, min + half, max - half)</c> has its bounds inverted, and `Mathf.Clamp` with
        /// min &gt; max returns whichever endpoint it tests last, so the camera would snap to one edge
        /// and jitter to the other as the target crossed. Centring is the only stable answer, and it
        /// is what the player expects: a small region sits in the middle of the screen.</para>
        /// </summary>
        public static Vector2 Clamp(Vector2 desired, in CameraBounds bounds,
                                    float halfWidthMeters, float halfHeightMeters)
        {
            if (!bounds.IsBounded) return desired;
            return new Vector2(
                ClampAxis(desired.x, bounds.Center.x, bounds.SizeMeters.x, halfWidthMeters),
                ClampAxis(desired.y, bounds.Center.y, bounds.SizeMeters.y, halfHeightMeters));
        }

        /// <summary>One axis of <see cref="Clamp"/>. Half-extent ≥ half the world size ⇒ centre.</summary>
        private static float ClampAxis(float desired, float center, float worldSize, float halfExtent)
        {
            float half = Mathf.Max(0f, halfExtent);
            float halfWorld = worldSize * 0.5f;
            if (half >= halfWorld) return center;          // the view is wider than the world: centre it
            return Mathf.Clamp(desired, center - halfWorld + half, center + halfWorld - half);
        }
    }
}
