namespace HiddenHarbours.Core
{
    /// <summary>
    /// Why an autopilot last handed the helm back (or refused to take it). In Core so the UI and audio
    /// can name the reason without the Boats module (rule 4). <b>Append-only</b>: the values are
    /// explicit and a new reason takes the next number.
    /// </summary>
    public enum AutopilotDisengage
    {
        None = 0,
        /// <summary>The player flicked it off.</summary>
        Toggled = 1,
        /// <summary>A steer key, the stick or the wheel took the helm back.</summary>
        SteerInput = 2,
        /// <summary>Nobody is at the helm any more (left it, stopped, region change).</summary>
        LeftHelm = 3,
        /// <summary>Her keel is on the bottom.</summary>
        Aground = 4,
        /// <summary>She hit something harder than a fender touch.</summary>
        Collision = 5,
        /// <summary>Too little way ahead for the rudder to bite (or going astern).</summary>
        NoWay = 6,
        /// <summary>This hull carries no autopilot.</summary>
        NotFitted = 7,
        /// <summary>The hull under the controller changed.</summary>
        HullChanged = 8,
    }

    /// <summary>
    /// Raised once on each autopilot transition: <see cref="Engaged"/> true when it takes the helm
    /// (<see cref="Reason"/> is <see cref="AutopilotDisengage.None"/>), false when it hands the helm
    /// back, with the rule that ended the hold. A refused press is not a transition and raises nothing,
    /// nor does a hand-back while already off. A lamp, a click or a chime listens here, never to the
    /// boat's controller.
    /// </summary>
    public readonly struct AutopilotStateChanged
    {
        public readonly object Boat;               // boxed reference to the boat (kept engine-light in Core)
        public readonly bool Engaged;
        public readonly AutopilotDisengage Reason;

        public AutopilotStateChanged(object boat, bool engaged, AutopilotDisengage reason)
        {
            Boat = boat;
            Engaged = engaged;
            Reason = reason;
        }
    }
}