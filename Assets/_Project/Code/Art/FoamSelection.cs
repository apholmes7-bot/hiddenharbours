using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Plain inputs to the camera's eight-slot selection; SourceIndex is only for reporting.</summary>
    internal readonly struct FoamCandidate
    {
        public readonly FoamInjection Injection;
        public readonly string StableKey;
        public readonly int SourceIndex;

        public FoamCandidate(in FoamInjection injection, string stableKey, int sourceIndex)
        {
            Injection = injection;
            StableKey = stableKey;
            SourceIndex = sourceIndex;
        }
    }

    internal readonly struct FoamSelectionSlot
    {
        public readonly FoamCandidate Candidate;
        public readonly float Distance;

        public FoamSelectionSlot(in FoamCandidate candidate, float distance)
        {
            Candidate = candidate;
            Distance = distance;
        }
    }

    /// <summary>
    /// Pure camera-window selection. One scan, at most eight insertions/comparisons per candidate,
    /// caller-owned storage, no growing sort or allocations. No tunable weights or reach factors:
    /// geometry comes from the very deposit the shader receives.
    /// </summary>
    internal static class FoamSelection
    {
        internal static int Select(FoamCandidate[] candidates, int candidateCount, Rect window,
                                   FoamSelectionSlot[] into, out int eligible)
        {
            eligible = 0;
            if (candidates == null || into == null || into.Length == 0) return 0;
            int count = 0;
            int capacity = Mathf.Min(into.Length, FoamBuffer.MaxInjectors);
            for (int i = 0; i < Mathf.Min(candidateCount, candidates.Length); i++)
            {
                FoamCandidate candidate = candidates[i];
                if (!Footprint(candidate.Injection, window, out float distance)) continue;
                eligible++;
                int slot = 0;
                while (slot < count && (into[slot].Distance < distance ||
                    (into[slot].Distance == distance && string.CompareOrdinal(
                        into[slot].Candidate.StableKey, candidate.StableKey) <= 0))) slot++;
                if (slot >= capacity) continue;
                for (int j = Mathf.Min(count, capacity - 1); j > slot; j--)
                    into[j] = into[j - 1];
                into[slot] = new FoamSelectionSlot(candidate, distance);
                count = Mathf.Min(count + 1, capacity);
            }
            return count;
        }

        internal static bool Footprint(in FoamInjection injection, Rect window, out float distance)
        {
            bool overlaps = Capsule(injection.From, injection.To, injection.Radius, window,
                                    out distance);
            FoamDispersal d = injection.Dispersal;
            if (!d.IsActive) return overlaps;
            // The shader's envelope is zero beyond MaxHalfWidth, even when EdgeWidth is larger.
            // Include the whole curved track, not an extension of the current heading. This is a
            // conservative reach envelope, not a promise that every enclosed texel gets a deposit.
            for (int i = 0; i < FoamDispersal.Nodes - 1; i++)
            {
                bool reaches = Capsule(Node(d, i), Node(d, i + 1), d.MaxHalfWidth, window,
                                       out float trackDistance);
                overlaps |= reaches;
                distance = Mathf.Min(distance, trackDistance);
            }
            return overlaps;
        }

        static Vector2 Node(in FoamDispersal d, int index)
        {
            switch (index)
            {
                case 0: return d.Node0;
                case 1: return d.Node1;
                case 2: return d.Node2;
                case 3: return d.Node3;
                case 4: return d.Node4;
                default: return d.Node5;
            }
        }

        static bool Capsule(Vector2 from, Vector2 to, float radius, Rect window, out float distance)
        {
            radius = Mathf.Max(0, radius);
            distance = Mathf.Max(0, Mathf.Sqrt(PointSegmentSquared(window.center, from, to)) - radius);
            // Exact capsule/rectangle overlap, including corner misses that an expanded AABB admits.
            Vector2 delta = to - from;
            float enter = 0, leave = 1;
            if (Slab(from.x, delta.x, window.xMin, window.xMax, ref enter, ref leave) &&
                Slab(from.y, delta.y, window.yMin, window.yMax, ref enter, ref leave)) return true;
            float nearest = Mathf.Min(PointRectSquared(from, window), PointRectSquared(to, window));
            nearest = Mathf.Min(nearest, PointSegmentSquared(window.min, from, to));
            nearest = Mathf.Min(nearest, PointSegmentSquared(window.max, from, to));
            nearest = Mathf.Min(nearest, PointSegmentSquared(new Vector2(window.xMin, window.yMax), from, to));
            nearest = Mathf.Min(nearest, PointSegmentSquared(new Vector2(window.xMax, window.yMin), from, to));
            return nearest <= radius * radius;
        }

        static bool Slab(float start, float delta, float min, float max, ref float enter, ref float leave)
        {
            if (delta == 0) return start >= min && start <= max;
            float a = (min - start) / delta;
            float b = (max - start) / delta;
            enter = Mathf.Max(enter, Mathf.Min(a, b));
            leave = Mathf.Min(leave, Mathf.Max(a, b));
            return enter <= leave;
        }

        static float PointRectSquared(Vector2 point, Rect window)
        {
            float x = Mathf.Max(Mathf.Max(window.xMin - point.x, 0), point.x - window.xMax);
            float y = Mathf.Max(Mathf.Max(window.yMin - point.y, 0), point.y - window.yMax);
            return x * x + y * y;
        }

        static float PointSegmentSquared(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float lengthSquared = delta.sqrMagnitude;
            float t = lengthSquared > 0 ? Mathf.Clamp01(Vector2.Dot(point - from, delta) / lengthSquared) : 0;
            return (point - (from + t * delta)).sqrMagnitude;
        }
    }
}
