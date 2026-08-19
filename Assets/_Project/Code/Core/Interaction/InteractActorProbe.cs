namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Who is reaching, and from where</b> — the live <see cref="InteractActor"/>, published once a
    /// frame by the Player lane so that any other lane can ask the same question without naming a Player
    /// type (rule 4).
    ///
    /// <para><b>Why it had to exist.</b> The fisher's four-way facing lives on
    /// <c>PlayerWalkController</c>, in <c>HiddenHarbours.Player</c>. <c>WorldInteractor</c> lives in
    /// <c>HiddenHarbours.World</c>, whose asmdef references Core and nothing else — so when the
    /// plain-text-UI consolidation gave conversations a FACING filter, "which way is she looking?" became
    /// a question that could only be asked through Core. Same shape and same discipline as
    /// <see cref="GameServices.PlayerTransform"/> and <see cref="GameServices.Hands"/>: the producer
    /// publishes itself, and this is where every consumer resolves it.</para>
    ///
    /// <para><b>Why the whole actor and not just a facing.</b> <c>ControlSwitcher</c> already composes an
    /// <see cref="InteractActor"/> every frame for <see cref="InteractVerb.PublishCandidate"/> — position,
    /// facing, and which of the four places the player is in, with the mode→context mapping written in
    /// exactly one place. Publishing that same value costs nothing and means the conversation filter and
    /// the interact verb cannot end up disagreeing about where the player is standing.</para>
    ///
    /// <para><b>Absent is a valid answer, and it fails OPEN.</b> Nothing is published in EditMode, in a
    /// bare art scene, or in a region scene played directly with no persistent rig. A consumer that finds
    /// <see cref="Has"/> false must behave as it did before facing existed — <see cref="Current"/> is then
    /// <c>default</c>, whose <see cref="InteractActor.Facing"/> is <see cref="UnityEngine.Vector2.zero"/>,
    /// which the contract already defines as "unknown, every direction is forward". So the degraded mode
    /// is exactly the old proximity-only behaviour rather than a fixture nobody can reach.</para>
    ///
    /// <para><b>The one-frame edge, stated rather than hidden.</b> Producer and consumers are separate
    /// <c>Update</c>s in undefined order, so on the frame the player turns, a consumer may read the
    /// previous frame's facing — the same exposure <see cref="InteractActionClaim"/> documents, and for
    /// the same reason it is acceptable: in any settled position, which is every position a player presses
    /// a key from, the value is already correct. A facing that is one frame stale is a popup that is 16 ms
    /// late, which nobody can see.</para>
    ///
    /// <para>FLAG lead-architect: new Core contract (the actor-publication seam).</para>
    /// </summary>
    public static class InteractActorProbe
    {
        /// <summary>True once somebody has published an actor this session. False means "no player out
        /// there", which consumers must treat as carry-on — never as a fault.</summary>
        public static bool Has { get; private set; }

        /// <summary>Where the player is, which way they face, and which place they are in — as of the last
        /// <see cref="Set"/>. <c>default</c> (facing unknown) when <see cref="Has"/> is false.</summary>
        public static InteractActor Current { get; private set; }

        /// <summary>Publish this frame's actor. Called by the Player lane's control switcher, which is the
        /// one component that already composes one.</summary>
        public static void Set(in InteractActor actor)
        {
            Current = actor;
            Has = true;
        }

        /// <summary>Stop reporting an actor — the switcher on disable, and scene teardown. A torn-down rig
        /// must not leave a stale position standing in for a player who is no longer anywhere.</summary>
        public static void Clear()
        {
            Current = default;
            Has = false;
        }

        /// <summary>Clear the probe (tests / teardown). Spelled as its own name so a test reads like the
        /// rest of the interaction statics, which all offer <c>Reset</c>.</summary>
        public static void Reset() => Clear();
    }
}
