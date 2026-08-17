using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>What the conversation overlay is doing to a villager right now.</summary>
    public enum ConversationHoldPhase
    {
        /// <summary>Nothing. The villager is exactly where the clock says, which is the only state the
        /// simulation knows about.</summary>
        Idle = 0,
        /// <summary>Standing still, facing the player, mid-conversation.</summary>
        Holding = 1,
        /// <summary>The talk is over and they are hurrying back onto the day they should be having.</summary>
        CatchingUp = 2,
    }

    /// <summary>
    /// <b>A villager stopping to talk — as an OVERLAY, never as a mutation of their day.</b>
    ///
    /// <para><b>The load-bearing fact.</b> A routine is a PURE FUNCTION of the clock
    /// (<see cref="RoutinePlan.SampleAt"/>) and CLAUDE.md rule 5 says it stays one. So a conversation
    /// does not pause a schedule, does not shift a departure, does not write anything down and does not
    /// touch the save. It sits ON TOP: while you are talking to her she holds the spot she was on and
    /// turns to face you, and when you are done the engine asks the clock where she SHOULD be by now and
    /// she hurries back onto it. Keep her twenty minutes and she is visibly late and moving fast — which
    /// is correct, and charming, and costs the simulation nothing, because at the moment she arrives the
    /// overlay ends and she is a pure function of the clock again.</para>
    ///
    /// <para><b>Pure and engine-light</b> — a POCO fed positions and deltas, so the whole behaviour
    /// (including "an uninterruptible block refuses the stop" and "she resumes to where the clock says,
    /// not to where she was standing") is an EditMode assertion rather than something to watch for in
    /// play. <see cref="VillagerRoutine"/> owns one and does nothing but feed it.</para>
    ///
    /// <para><b>Facing is DERIVED, on GROUND bearings</b> (ADR 0034): the world XY plane is the SQUASHED
    /// ground plane, so the bearing toward the player goes through <see cref="IsoGround"/> and not
    /// through an <c>atan2</c> of its own. A cardinal-only test cannot see the difference — the error is
    /// zero on the cardinals and peaks at 12.56° near the diagonals — which is exactly why the diagonals
    /// are what <c>ConversationHoldTests</c> checks.</para>
    /// </summary>
    public sealed class ConversationHold
    {
        /// <summary>How close to the clock-derived spot counts as caught up, in metres. Convergence
        /// plumbing, not owner feel — the same class of number as
        /// <see cref="VillagerRoutine.ShelterCheckSeconds"/>. Small enough that nobody sees a gap, large
        /// enough that a target moving at walking pace is actually reachable rather than chased forever.</summary>
        public const float ArrivedMetres = 0.12f;

        /// <summary>How long a catch-up may run before it gives up and simply resumes, in seconds of
        /// real time. The backstop for the pathological case — the clock jumped a day, or the player
        /// held somebody through half their schedule — where the honest answer is that she is not
        /// hurrying anywhere, she is just where she is now.</summary>
        public const float CatchUpGiveUpSeconds = 20f;

        private Vector2 _position;
        private float _catchUpSeconds;

        /// <summary>What the overlay is doing.</summary>
        public ConversationHoldPhase Phase { get; private set; } = ConversationHoldPhase.Idle;

        /// <summary>True while the overlay has anything to say about where this villager is.</summary>
        public bool IsActive => Phase != ConversationHoldPhase.Idle;

        /// <summary>True while the villager is standing still talking to somebody.</summary>
        public bool IsHolding => Phase == ConversationHoldPhase.Holding;

        /// <summary>The ground bearing they are turned to while holding (degrees, 0 = North, CW). Only
        /// meaningful while <see cref="IsHolding"/>.</summary>
        public float HeadingDegrees { get; private set; }

        /// <summary>
        /// The shelter answer captured when the conversation began. While the overlay is active the
        /// villager is where the OVERLAY says, not where the clock says, so the clock's shelter answer is
        /// about somebody else's position — freezing it is what stops a villager blinking out of
        /// existence mid-sentence because her schedule has meanwhile taken her through a door.
        /// </summary>
        public RoutineShelter Shelter { get; private set; }

        /// <summary>The block <see cref="Shelter"/> was captured in. Frozen with it, because "which
        /// building is she behind the threshold of" is answered by indexing that block's interior — so a
        /// shelter answer from one block read against another block's index is a question about the
        /// wrong house, which is exactly the kind of quietly-wrong that only shows up when somebody is
        /// held across a departure.</summary>
        public int ShelterBlockIndex { get; private set; }

        /// <summary>
        /// Try to stop this villager where they stand and turn them to the player.
        ///
        /// <para>Returns FALSE when the block they are in is not interruptible — the authored exception
        /// (a timed beat, a crossing they must not stop in the middle of). A refused stop is not a
        /// refused conversation: they keep walking and the bubble travels with them, which reads exactly
        /// as "sorry, I'm on my way to the wharf" and is the shape the owner's optional brush-off line
        /// drops straight into later.</para>
        /// </summary>
        public bool TryBegin(Vector2 npcPosition, Vector2 playerPosition, bool interruptible,
                             RoutineShelter shelter, int shelterBlockIndex)
        {
            if (!interruptible) return false;

            _position = npcPosition;
            _catchUpSeconds = 0f;
            Shelter = shelter;
            ShelterBlockIndex = shelterBlockIndex;
            Phase = ConversationHoldPhase.Holding;
            FaceToward(playerPosition);
            return true;
        }

        /// <summary>Turn to the player again — called every frame while holding, so somebody who walks
        /// around a villager mid-conversation is followed rather than talked past.</summary>
        public void FaceToward(Vector2 playerPosition)
        {
            if (Phase != ConversationHoldPhase.Holding) return;
            // ⚠️ Through IsoGround, never a bare atan2: the rows are ground bearings (ADR 0034).
            // A zero-length delta (the player standing exactly on her) answers 0 and is left alone.
            Vector2 delta = playerPosition - _position;
            if (delta.sqrMagnitude > 1e-6f) HeadingDegrees = IsoGround.BearingDegrees(delta);
        }

        /// <summary>The conversation ended: stop holding and start hurrying back onto the day.</summary>
        public void Release()
        {
            if (Phase != ConversationHoldPhase.Holding) return;
            Phase = ConversationHoldPhase.CatchingUp;
            _catchUpSeconds = 0f;
        }

        /// <summary>Drop the overlay entirely and hand the villager straight back to the clock. For
        /// teardown, and for the moment the clock takes her behind a threshold, where a catch-up walk
        /// would be a walk through a wall.</summary>
        public void Cancel()
        {
            Phase = ConversationHoldPhase.Idle;
            _catchUpSeconds = 0f;
        }

        /// <summary>
        /// Where the villager should be drawn this frame. Pass the position the CLOCK derives
        /// (<see cref="RoutinePose.Position"/>) and get back either that same point (the overlay has
        /// nothing to say) or the overlay's answer.
        ///
        /// <list type="bullet">
        ///   <item><b>Idle</b> — the clock's own answer, unchanged, allocation-free.</item>
        ///   <item><b>Holding</b> — the spot they stopped on. They do not drift while you talk.</item>
        ///   <item><b>CatchingUp</b> — walking toward the clock's answer at
        ///   <paramref name="catchUpSpeed"/>. The target is itself moving, which is why the speed has to
        ///   be the hurrying one and not the strolling one; the overlay ends the moment they arrive, and
        ///   from that frame on they are the pure function again.</item>
        /// </list>
        /// </summary>
        public Vector2 Step(Vector2 clockPosition, float deltaSeconds, float catchUpSpeed)
        {
            switch (Phase)
            {
                case ConversationHoldPhase.Holding:
                    return _position;

                case ConversationHoldPhase.CatchingUp:
                {
                    _catchUpSeconds += Mathf.Max(0f, deltaSeconds);

                    float step = Mathf.Max(0f, catchUpSpeed) * Mathf.Max(0f, deltaSeconds);
                    Vector2 toTarget = clockPosition - _position;
                    float distance = toTarget.magnitude;

                    if (distance <= ArrivedMetres || step >= distance ||
                        _catchUpSeconds >= CatchUpGiveUpSeconds)
                    {
                        Phase = ConversationHoldPhase.Idle;
                        return clockPosition;
                    }

                    _position += toTarget / distance * step;
                    return _position;
                }

                default:
                    return clockPosition;
            }
        }
    }
}
