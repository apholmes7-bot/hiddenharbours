using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>How a gull came to be on the water — the <c>kind</c> half of
    /// <see cref="GullSplashed"/>.</summary>
    public enum GullSplashKind
    {
        /// <summary>She came down and settled: the rig's <c>splash</c> arrival at the end of a commanded
        /// alight. The only kind the game emits today.</summary>
        Alight = 0,

        /// <summary>She came down at the shoal: the rig's <c>dive</c> stooping into <c>splash</c>. The rig
        /// bakes the stoop and the sidecar prices its yield; nothing in the game commands it yet, so this
        /// is the vocabulary waiting for the lane that does — not a promise that it fires.</summary>
        Dive = 1
    }

    /// <summary>
    /// <b>A GULL HIT THE WATER</b> — the owner's ask of 2026-09-09, <i>"fish react to it landing"</i>,
    /// as one Core signal (rule 4).
    ///
    /// <para>The gull module publishes it; the fishing module listens. Neither names a type of the
    /// other's: the bird knows nothing about schools, and the fish know nothing about seagulls — they
    /// know that something heavy landed at a place, which is all a fish could know.</para>
    ///
    /// <para><b>It carries no time.</b> A signal is heard only by a listener that already exists, and
    /// <see cref="EventBus"/> publishes synchronously, so the listener stamps the arrival off
    /// <see cref="GameServices.Clock"/> the moment it takes delivery. That keeps ONE clock in the loop —
    /// the same <c>TotalSeconds</c> the fish are drawn on — instead of a publisher's stamp and a
    /// consumer's clock that can disagree by a frame. It also keeps the flock clock-free: the gulls run
    /// on the milliseconds they are handed and ask the engine nothing (PR 2's invariant).</para>
    ///
    /// <para><b>It carries no catch either.</b> The sidecar's dive yield is a PICTURE the bird makes —
    /// she leaves with a fish instead of settling to float — and no listener needs to be told about it.
    /// There is no inventory, no market and no counter on this signal, by ruling.</para>
    /// </summary>
    public readonly struct GullSplashed
    {
        /// <summary>Where she hit, in world metres on the sea plane.</summary>
        public readonly Vector2 WorldPosition;

        /// <summary>Settling, or striking.</summary>
        public readonly GullSplashKind Kind;

        public GullSplashed(Vector2 worldPosition, GullSplashKind kind)
        {
            WorldPosition = worldPosition; Kind = kind;
        }

        public override string ToString() => "GullSplashed(" + Kind + " @ " + WorldPosition + ")";
    }
}
