using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A hold that is <b>part of a boat lying somewhere in the world</b> — the one extra fact a buyer
    /// ashore needs before it can take a catch it cannot see: where she is.
    ///
    /// <para><b>Why it is a contract in Core.</b> The hold is the boat's (<c>ShipHold</c>, in Boats) and
    /// the buyer is the market's (Economy), and neither may name the other's classes (rule 4). The
    /// plain <see cref="IHold"/> already crosses that line for a hold somebody WIRED to a counter; this
    /// is the same crossing for a hold nobody wired, found through <see cref="BoatHolds"/> instead.</para>
    ///
    /// <para><b>A position and nothing else.</b> Whether she counts as berthed, and how near a wharf
    /// that takes, is the buyer's policy and the owner's number — not something the hold decides about
    /// itself. So this says where, and the asker does the measuring.</para>
    /// </summary>
    public interface IBoatHold : IHold
    {
        /// <summary>Where the boat that carries this hold lies right now, in world metres (her root).
        /// Read LIVE — she drifts, swings on her line and rides the tide.</summary>
        Vector2 WorldPosition { get; }
    }
}
