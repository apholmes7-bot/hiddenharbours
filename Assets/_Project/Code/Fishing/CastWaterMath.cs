using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The PURE maths of the <b>cast-must-land-in-water</b> rule (dock/shore fishing — Rod Fishing v2 is
    /// DOCK-FIRST, the owner's locked decision). On foot the flick can point anywhere, including inland;
    /// a line that would land on dry ground is COZY-clamped: it lands at the FARTHEST point along the
    /// cast arc that is still water (the longest cast that still splashes), and if no point of the arc
    /// is wet at all the gesture resolves to "no water that way" — a short-cast reset, no penalty, no
    /// stuck state (the caller returns the FSM to Idle).
    ///
    /// <para>Pure and deterministic like its siblings (<see cref="FlickCastMath"/> /
    /// <see cref="DepthDropMath"/>): no <c>Time</c>, no RNG, no scene — the water truth arrives as an
    /// injected probe (the controller feeds the live bathymetry composition, tests feed a lambda), so
    /// the clamp is fully EditMode-testable (rule 5). NaN-safe throughout.</para>
    /// </summary>
    public static class CastWaterMath
    {
        /// <summary>Is the given world point water right now? (The bathymetry/tide composition —
        /// <c>TidalExposure.WaterDepth &gt; 0</c> — in play; any predicate in tests.)</summary>
        public delegate bool WaterAt(Vector2 worldPoint);

        /// <summary>
        /// How many points of the cast arc are probed when the landing point is dry — a RESOLUTION /
        /// performance bound (like <c>FishingController.GestureCapacity</c>), not a feel dial: at the
        /// starter rod's 12 m cap the clamp resolves to ~0.4 m, well under a boat length. Probes run
        /// once per released cast, never per frame.
        /// </summary>
        public const int DefaultProbeSamples = 32;

        /// <summary>
        /// Clamp a cast of <paramref name="castDistanceM"/> along <paramref name="direction"/> from
        /// <paramref name="anchor"/> to the farthest point of the arc that is water. Probes the full
        /// distance first (the unclamped cast), then walks inward in <paramref name="samples"/> even
        /// steps. True = <paramref name="waterDistanceM"/> holds the landing distance (equal to the
        /// cast distance when it already landed wet); false = no water anywhere on the arc (the cozy
        /// "no water that way" reset). Null probe / non-positive distance / NaN inputs → false.
        /// </summary>
        public static bool TryClampToWater(Vector2 anchor, Vector2 direction, float castDistanceM,
                                           int samples, WaterAt isWater, out float waterDistanceM)
        {
            waterDistanceM = 0f;
            if (isWater == null) return false;
            if (float.IsNaN(anchor.x) || float.IsNaN(anchor.y)) return false;
            if (float.IsNaN(direction.x) || float.IsNaN(direction.y)) return false;
            if (float.IsNaN(castDistanceM) || castDistanceM <= 0f) return false;

            samples = Mathf.Max(1, samples);
            for (int i = samples; i >= 1; i--)
            {
                float d = castDistanceM * i / samples;
                if (!isWater(anchor + direction * d)) continue;
                waterDistanceM = d;
                return true;
            }
            return false;
        }
    }
}
