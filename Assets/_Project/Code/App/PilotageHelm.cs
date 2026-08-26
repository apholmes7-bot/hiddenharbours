using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>THE FIVE PHASES OF A PILOTED PASSAGE</b> (design/npc-pilotage.md §2.1). One machine per boat;
    /// each phase names what it commands, what makes it <i>hold</i> (stay here with the way off) and what
    /// makes it <i>abort</i> (fall back a phase and re-present). <b>Nothing ever advances on a timer
    /// alone.</b>
    ///
    /// <para>S1 builds the whole ladder for ONE boat — the intro skipper at St Peters — and nothing else:
    /// no traffic, no timetable, no rules of the road. Those are S3/S4 and they ride this same enum.</para>
    /// </summary>
    public enum PilotagePhase
    {
        /// <summary>Seek the next route mark at cruise. The long legs, outside the wharf line.</summary>
        Passage = 0,
        /// <summary>The fairway inbound at HARBOUR speed, easing onto the approach gate.</summary>
        Approach,
        /// <summary>Come to the approach gate — parallel to the berth, one hull-length off it.</summary>
        Gate,
        /// <summary>Close laterally at the set rate while holding the berth heading; astern takes off the
        /// last of the way.</summary>
        Alongside,
        /// <summary>Helm dead. <b>The lines hold her</b> (<c>MooringLineMath</c>).</summary>
        Moored,
    }

    /// <summary>
    /// <b>THE ABSTRACT HELM the phase machine commands</b> — four reads and one write, and deliberately
    /// not one word more.
    ///
    /// <para><b>⭐ Why the machine does not simply hold a <see cref="BoatController"/>.</b>
    /// design/npc-pilotage.md §2.3 rules two BACKENDS under one machine, for two reasons that are not
    /// preference: ten rigidbodies plus the player is a fixed-step bill the budget has never been asked to
    /// carry (rule 7), and a rigidbody can be <i>pushed</i>, so a determinism claim over ten shoveable
    /// hulls is unholdable (rule 5). S1 ships the <b>HELMED</b> backend only —
    /// <see cref="HelmedBoat"/>, a real hull taking real helm — and this seam is what lets S4 add the
    /// kinematic one without touching a line of the phase machine.</para>
    ///
    /// <para><b>It is an App-local interface, not a new Core seam.</b> §8's S1 row says the slice needs no
    /// new Core contract and everything it touches is <c>App</c> and <c>Boats</c>; <c>App</c> already
    /// references <c>Boats</c>, so widening Core for an interface only this lane implements would be a
    /// seam bought before it is needed (rule 4 protects module boundaries, it does not ask for
    /// ceremony).</para>
    /// </summary>
    public interface IPilotageHelm
    {
        /// <summary>Where she is, world XY, this instant.</summary>
        Vector2 Position { get; }

        /// <summary>Where her bow points, COMPASS degrees (0 = north, clockwise) — the one frame
        /// <see cref="ArrivalPilot.CompassOf"/> defines, so nothing has to convert.</summary>
        float HeadingDegrees { get; }

        /// <summary>Her velocity over the GROUND, world m/s. ⚠ Never bow-relative way: a boat with the
        /// helm over crabs, and that distinction is the difference between docking and orbiting
        /// (<see cref="ArrivalPilot.WayToAccountFor"/>).</summary>
        Vector2 Velocity { get; }

        /// <summary>Work the helm: throttle and steer, each −1..1, exactly the pair the player is handed.</summary>
        void SetControl(float throttle, float steer);
    }

    /// <summary>
    /// <b>The HELMED backend</b> (§2.3) — the phase machine's commands go to a real
    /// <see cref="BoatController"/>, so the boat heels, loses her rudder as she slows, feels the same wave
    /// field every other hull does, and answers astern at her own <c>DefaultAsternFactor</c>. Used for the
    /// intro skipper and, from S4, for any boat inside hand-over range of the player.
    ///
    /// <para>A pure adapter: it holds no state of its own and decides nothing. Position and heading come
    /// off the ROOT transform rather than off the controller, because the root is what the pilot turns and
    /// what every presentation path already reads.</para>
    /// </summary>
    public sealed class HelmedBoat : IPilotageHelm
    {
        private readonly BoatController _boat;
        private readonly Transform _root;

        public HelmedBoat(BoatController boat, Transform root)
        {
            _boat = boat;
            _root = root;
        }

        /// <summary>True while both ends of the adapter are still alive. ⚠ Written as explicit
        /// <c>!= null</c> comparisons and never as <c>??</c>: a destroyed <c>UnityEngine.Object</c> is
        /// fake-null, which the null-coalescing operators do not see.</summary>
        public bool IsAlive => _boat != null && _root != null;

        /// <inheritdoc/>
        public Vector2 Position => _root != null ? (Vector2)_root.position : Vector2.zero;

        /// <inheritdoc/>
        public float HeadingDegrees => ArrivalPilot.HeadingOf(_root);

        /// <inheritdoc/>
        public Vector2 Velocity => _boat != null ? _boat.Velocity : Vector2.zero;

        /// <inheritdoc/>
        public void SetControl(float throttle, float steer)
        {
            if (_boat != null) _boat.SetControl(throttle, steer);
        }
    }
}
