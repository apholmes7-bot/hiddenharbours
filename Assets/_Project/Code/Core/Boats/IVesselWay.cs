namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Is this vessel UNDER WAY, or is she lying still?</b> — the one fact the rule of the road
    /// needs before it can say which lamps a hull is allowed to burn, asked of the boat ROOT.
    ///
    /// <para><b>Why this is a seam in Core and not a field on a boat.</b> The lamps are drawn by the
    /// Art lane and the answer is owned by the Boats and Player lanes: a moored hull knows she is
    /// made fast, a driven hull knows somebody is steering her, and Art may reference neither
    /// (rule 4). So the question is declared here, in the module both sides already speak, and Art
    /// asks it of whatever component happens to be sitting on the root. Nothing is registered, so a
    /// hull that answers nothing is not an error — see <see cref="VesselWay.UnderWay"/>.</para>
    ///
    /// <para><b>A state, not an event.</b> It is read when the lamps are (re)built and whenever the
    /// hull says her state changed — never polled per frame — and it is a pure read of something the
    /// implementor already knows, so it saves nothing and adds no simulation (rule 5).</para>
    /// </summary>
    public interface IVesselWay
    {
        /// <summary>How she is lying, right now.</summary>
        VesselWay Way { get; }
    }

    /// <summary>
    /// <b>Tell me when the boat I am hanging off changes her mind.</b> Implemented by anything that
    /// caches a hull's <see cref="VesselWay"/> — the lamps, today — and called by whoever declares it.
    ///
    /// <para><b>Why a push rather than a poll.</b> The way is a STATE, and states change rarely: a boat
    /// is let go, or her lines go fast, and then nothing happens for an hour. Asking sixty times a
    /// second for an answer that changes twice in a voyage is the wrong shape, and the alternative —
    /// only reading it when the lamps happen to rebuild — is how a boat ties up and keeps her
    /// sidelights burning until something unrelated re-skins her.</para>
    ///
    /// <para><b>And it lives HERE for the same reason <see cref="IVesselWay"/> does.</b> The declarer
    /// is in the Boats lane and the listener is in Art, which may not reference each other (rule 4).
    /// Core is the module both already speak.</para>
    /// </summary>
    public interface IVesselWayListener
    {
        /// <summary>She is now lying this way. Called on the frame it changes, not deferred.</summary>
        void OnVesselWayChanged(VesselWay way);
    }

    /// <summary>
    /// The two ways a hull can be lying, as far as her lamps are concerned.
    ///
    /// <para><b>Deliberately two, not three.</b> A seaman would separate "made fast alongside" (which
    /// strictly shows no lights at all) from "at anchor" (one all-round white). Both are collapsed
    /// into <see cref="Moored"/> and both show the anchor light, and that is a PICTURE decision made
    /// on purpose: a wharf of seven working boats showing nothing whatever is a black hole in the
    /// middle of the harbour at two in the morning, and the owner's brief for the night is a harbour
    /// you can read. The lie is small — a light where a real boat would show none — and it is the
    /// opposite of the dangerous one, which is showing sidelights while lying still.</para>
    /// </summary>
    public enum VesselWay
    {
        /// <summary>
        /// She is making way: sidelights, stern light, masthead — the full set that tells a lookout
        /// her aspect.
        ///
        /// <para><b>This is the default, and that is load-bearing.</b> A hull whose root carries no
        /// <see cref="IVesselWay"/> at all is under way, because that is exactly what every lamp-
        /// bearing hull in the game did before the regime existed (the arrival's Cape Islander among
        /// them, and she is the shipped control). Absence must therefore mean "no change", never
        /// "dark".</para>
        /// </summary>
        UnderWay = 0,

        /// <summary>She is lying still — made fast alongside, or brought up to her anchor. Anchor
        /// light only, plus whatever glows out of a room somebody is in.</summary>
        Moored = 1,
    }
}
