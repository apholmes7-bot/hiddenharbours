using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The Core seam for SMALL foam deposits into the sea's one foam sheet (rule 4): an oar blade going
    /// in and coming out, and anything else that breaks the water for a moment without being a hull.
    /// A feature lays a capsule here for THIS frame; the Art side's foam buffer gathers it beside the
    /// hulls' own deposits and draws it through the same buffer, lace, bands and ageing as the wake
    /// (the owner, 2026-11-19: "wake foam should be the same as any foam, not a drawn special piece").
    ///
    /// <para>The points are DRAWN world points, where the foam should be seen; the buffer side takes the
    /// surface's drawing lift off them, exactly as it does for a hull's transom.</para>
    ///
    /// <para><b>Cost (rule 7).</b> A fixed array of <see cref="Capacity"/> deposits, refilled per frame:
    /// nothing allocates after load. A deposit is kept for the frame it was laid on only, so a feature
    /// that stops laying leaves nothing standing. Over the cap the extra deposits are dropped for the
    /// frame and counted (<see cref="DroppedThisFrame"/>).</para>
    ///
    /// <para>Presentation only: no simulation reads it and nothing is saved (rule 5).</para>
    /// </summary>
    public static class FoamSplashes
    {
        /// <summary>One deposit: the capsule from <see cref="From"/> to <see cref="To"/>, of half-width
        /// <see cref="Radius"/>, adding <see cref="Amount"/> coverage at its centre this frame (already
        /// dt-scaled), and marking the water fresh when <see cref="Vigour"/> is above zero.</summary>
        public struct Deposit
        {
            public Vector2 From, To;
            public float Radius, Amount, Vigour;
        }

        /// <summary>Deposits held per frame. Small on purpose: the buffer has eight slots for every foam
        /// source in view, and a rowed boat lays at most one deposit per oar.</summary>
        public const int Capacity = 4;

        private static readonly Deposit[] s_Items = new Deposit[Capacity];
        private static int s_Count;
        private static int s_Frame = -1;
        private static int s_Dropped;
        private static int s_Version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        /// <summary>Forget every deposit (scene loads, tests).</summary>
        public static void Clear()
        {
            s_Count = 0;
            s_Dropped = 0;
            s_Frame = -1;
            s_Version++;
        }

        /// <summary>Bumped on every change, so a reader that caches per frame can tell a refill within one frame.</summary>
        public static int Version => s_Version;

        /// <summary>The frame the held deposits belong to (<c>Time.frameCount</c>).</summary>
        public static int Frame => s_Frame;

        /// <summary>Deposits laid on <paramref name="frame"/>; 0 for any other frame.</summary>
        public static int CountFor(int frame) => frame == s_Frame ? s_Count : 0;

        /// <summary>Deposits laid this frame.</summary>
        public static int Count => CountFor(Time.frameCount);

        /// <summary>Deposits dropped this frame because the cap was full.</summary>
        public static int DroppedThisFrame => s_Frame == Time.frameCount ? s_Dropped : 0;

        /// <summary>Deposit <paramref name="index"/> of this frame (0 .. <see cref="Count"/>-1).</summary>
        public static Deposit Get(int index) => s_Items[Mathf.Clamp(index, 0, Capacity - 1)];

        /// <summary>Lay a deposit for this frame. Returns false when there is nothing to lay or the cap
        /// is full.</summary>
        public static bool Add(Vector2 from, Vector2 to, float radius, float amount, float vigour)
            => AddOn(Time.frameCount, from, to, radius, amount, vigour);

        /// <summary><see cref="Add"/> on an explicit frame (tests, which run inside one editor frame).</summary>
        public static bool AddOn(int frame, Vector2 from, Vector2 to, float radius, float amount, float vigour)
        {
            if (frame != s_Frame)
            {
                s_Frame = frame;
                s_Count = 0;
                s_Dropped = 0;
                s_Version++;
            }
            if (!(radius > 0f) || !(amount > 0f) || !IsFinite(from) || !IsFinite(to)) return false;
            if (s_Count >= Capacity) { s_Dropped++; return false; }
            s_Version++;
            s_Items[s_Count++] = new Deposit
            {
                From = from,
                To = to,
                Radius = radius,
                Amount = Mathf.Clamp01(amount),
                Vigour = Mathf.Clamp01(vigour),
            };
            return true;
        }

        private static bool IsFinite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y)
                                                   && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
    }
}
