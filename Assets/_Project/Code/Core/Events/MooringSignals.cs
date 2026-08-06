namespace HiddenHarbours.Core
{
    /// <summary>What just happened to a mooring line — the beats worth voicing or showing.</summary>
    public enum MooringLineEvent
    {
        /// <summary>The thrown loop caught and the line is made fast between two cleats.</summary>
        MadeFast = 0,
        /// <summary>The player let the line go deliberately. She is free.</summary>
        CastOff = 1,
        /// <summary>Scope was tightened or slackened (the line is still made fast).</summary>
        ScopeChanged = 2,
        /// <summary>The toss missed — the loop fell in the water. The cozy fail: coil and try again.</summary>
        TossMissed = 3,
        /// <summary>The line was worked past its working load for too long and the loop SLIPPED off the
        /// cleat. The boat is adrift, undamaged — the tide out-ran the scope she was given.</summary>
        Slipped = 4,
    }

    /// <summary>
    /// Raised on each meaningful beat of the mooring-line loop (M2-38). The one-way Core handoff the rest
    /// of this repo already uses: gameplay publishes, and audio (rope creak, the slap of a missed loop, the
    /// clunk of a made-fast turn) and any diegetic read pick it up WITHOUT either side referencing the
    /// other. Payload is a value struct — no GC on a beat that can fire while the sea is working.
    ///
    /// <para><b>Transitions only.</b> The live tension of a made-fast line is not here on purpose: it
    /// changes every tick with the tide, and a per-tick event would be a per-tick allocation-shaped
    /// mistake. Consumers that want the continuous read (a straining rope's creak, a taut/slack visual)
    /// take <see cref="Load01"/> from this beat to arm themselves and then read the boat's own live value.
    /// </para>
    /// </summary>
    public readonly struct MooringLineChanged
    {
        /// <summary>Which beat this is.</summary>
        public readonly MooringLineEvent Event;

        /// <summary>Id of the hull-side cleat (<see cref="IMooringCleat.Id"/>); empty when the beat has no
        /// boat end — a missed toss never made one fast.</summary>
        public readonly string BoatCleatId;

        /// <summary>Id of the shore-side cleat; empty for the same reason.</summary>
        public readonly string ShoreCleatId;

        /// <summary>The line's scope (m) at this beat — what the player has paid out.</summary>
        public readonly float ScopeMetres;

        /// <summary>How hard the line was working at this beat: 0 slack, 1 bar-taut, &gt;1 overloaded
        /// (<see cref="MooringLineMath.Load01"/>). On <see cref="MooringLineEvent.Slipped"/> this is the
        /// load that finally lost her.</summary>
        public readonly float Load01;

        public MooringLineChanged(MooringLineEvent evt, string boatCleatId, string shoreCleatId,
                                  float scopeMetres, float load01)
        {
            Event = evt;
            BoatCleatId = boatCleatId ?? "";
            ShoreCleatId = shoreCleatId ?? "";
            ScopeMetres = scopeMetres;
            Load01 = load01;
        }
    }
}
