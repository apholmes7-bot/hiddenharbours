namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Where a hull's trim comes from</b> — the physics side of the boat publishing the bow angle
    /// her speed and her drive ask for, to the ONE place that draws her attitude
    /// (<see cref="MeshHullDriver"/>). Implemented by <see cref="BoatController"/> on the same root;
    /// the driver finds it with one GetComponent when it is configured, so a hull with no controller
    /// (the ambient fleet, a moored skin) simply has no trim.
    ///
    /// <para>A seam inside Boats, not a Core contract: both ends live in this module. It exists so the
    /// driver reads three numbers instead of reaching into the controller's physics.</para>
    /// </summary>
    public interface IHullTrimSource
    {
        /// <summary>The bow angle she is being asked for this physics step, degrees, + = bow up.
        /// Already clamped to her limits. 0 = level.</summary>
        float TrimTargetDegrees { get; }

        /// <summary>How long the drawn trim takes to follow the target (the time constant of one
        /// exponential lag, seconds). 0 = follow it exactly.</summary>
        float TrimResponseSeconds { get; }

        /// <summary>Bumped whenever she must be put back to level AT ONCE rather than eased there —
        /// a stop, a teleport, a hull swap or a load. The drawer snaps its trim to 0 when it sees a
        /// new value.</summary>
        int TrimRestSerial { get; }
    }
}
