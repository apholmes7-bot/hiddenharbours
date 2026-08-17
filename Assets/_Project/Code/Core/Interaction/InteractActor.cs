using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Who is reaching, and from where</b> — everything <see cref="InteractResolver"/> needs to know
    /// about the player, as a value.
    ///
    /// <para><b>Why a struct and not "the player".</b> The resolver is a pure function (rule 5) and Core
    /// may not name a Player-lane type (rule 4). Passing the actor as three plain facts keeps the whole
    /// selection rule EditMode-testable with no scene, no components and no input device — which is the
    /// only way a cloud agent can prove it at all.</para>
    /// </summary>
    public readonly struct InteractActor
    {
        /// <summary>Where the actor is standing, world metres.</summary>
        public readonly Vector2 Position;

        /// <summary>
        /// A unit vector for the way the actor is facing, or <see cref="Vector2.zero"/> for
        /// <b>facing unknown</b>.
        ///
        /// <para>Unknown is a real, expected state, not a bug: on deck the fisher is driven by the deck
        /// walker, which carries no facing, so there is nothing honest to report. The resolver treats
        /// unknown as "every direction is forward" and skips the arc test rather than guessing a heading —
        /// a guessed facing would silently make candidates unreachable from certain angles, which is far
        /// worse than a slightly generous arc.</para>
        /// </summary>
        public readonly Vector2 Facing;

        /// <summary>Which single place the actor is in (see <see cref="InteractContext"/>). Always exactly
        /// one bit — it is derived from <see cref="ControlMode"/>, and you can only be in one of them.</summary>
        public readonly InteractContext Context;

        public InteractActor(Vector2 position, Vector2 facing, InteractContext context)
        {
            Position = position;
            Facing = facing;
            Context = context;
        }

        /// <summary>The actor for a control mode — the one place the mode→context mapping is written, so
        /// the switcher and the registrants can never disagree about what "on deck" means.</summary>
        public static InteractActor For(Vector2 position, Vector2 facing, ControlMode mode)
            => new InteractActor(position, facing, ContextFor(mode));

        /// <summary>
        /// Which context a control mode is. <see cref="ControlMode.Aboard"/> is the HELM (the enum predates
        /// the deck state and kept its name for save compatibility) — see <see cref="ControlMode"/>.
        ///
        /// <para><b>Every mode is named, and the default arm THROWS rather than guessing.</b> This used to
        /// end in <c>_ =&gt; AtHelm</c>, which is a trap on an append-only enum: <see cref="ControlMode"/>
        /// is a Core contract that grows, and the next mode appended to it would have been silently
        /// reported as standing at a boat's helm — compile-clean, and wrong in a way no test asks about.
        /// (<see cref="ControlMode.Driving"/> was that next mode.) A mode with no context is an
        /// unfinished contract, so it fails loudly here instead of resolving candidates in the wrong
        /// place.</para>
        /// </summary>
        public static InteractContext ContextFor(ControlMode mode) => mode switch
        {
            ControlMode.OnFoot => InteractContext.OnFoot,
            ControlMode.OnDeck => InteractContext.OnDeck,
            ControlMode.Aboard => InteractContext.AtHelm,
            ControlMode.Driving => InteractContext.Driving,
            _ => throw new System.ArgumentOutOfRangeException(
                     nameof(mode), mode, "ControlMode has grown a member with no InteractContext."),
        };
    }
}
