using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ⭐ <b>One frame of what the player on foot is asking for</b> — a move, a hurry, a press — as
    /// numbers and nothing else (ADR 0043).
    ///
    /// <para><see cref="Move"/> is the raw move axis: each component is −1, 0 or +1 on a keyboard and a
    /// stick's own value on a pad, NOT normalised here — the walk controller clamps the magnitude itself
    /// (<c>PlayerWalkController.VelocityFor</c>), exactly as it clamped the keys it used to read, so a
    /// diagonal on a keyboard arrives as (±1, ±1) and is clamped to unit length downstream and not twice.
    /// <see cref="Sprint"/> is held. <see cref="Interact"/> and <see cref="Cancel"/> are EDGES — true on
    /// the frame the control went down, false the frame after — the sense every shipped verb reads.</para>
    ///
    /// <para>An intent never carries a device: nothing downstream may ask which key or button produced it.
    /// The last-used device is a separate signal (<see cref="ActiveControlDevice"/>) for the one customer
    /// that legitimately cares — the glyph an affordance draws.</para>
    /// </summary>
    public readonly struct WalkIntents
    {
        public readonly Vector2 Move;
        public readonly bool Sprint;
        public readonly bool Interact;
        public readonly bool Cancel;

        public WalkIntents(Vector2 move, bool sprint, bool interact, bool cancel)
        {
            Move = move;
            Sprint = sprint;
            Interact = interact;
            Cancel = cancel;
        }

        /// <summary>Nothing asked — still, walking pace, no press. What a source with no device behind it
        /// answers, and what a stopped world answers for every device.</summary>
        public static WalkIntents None => default;
    }

    /// <summary>
    /// <b>One frame of what the player aboard, on her feet, is asking for</b> — the deck walk and the
    /// arrival's cabin walk. The same move axis as <see cref="WalkIntents"/> with no sprint (nobody runs
    /// on a deck) — a separate struct rather than a flag on the walk's because the two are separate
    /// CONTROL MODES with separate binding maps, and a pad may bind them differently one day without
    /// either reader changing.
    /// </summary>
    public readonly struct DeckIntents
    {
        public readonly Vector2 Move;
        public readonly bool Interact;

        public DeckIntents(Vector2 move, bool interact)
        {
            Move = move;
            Interact = interact;
        }

        public static DeckIntents None => default;
    }

    /// <summary>
    /// ⭐ <b>Where a control mode's intents come from</b> — the ONE seam between whatever is being
    /// controlled and whatever is controlling it, generalised from <see cref="IDriveInputSource"/>
    /// (ADR 0043; the shape was proved by the drive seam, ADR 0035 amendment 2026-09-02).
    ///
    /// <para><b>Contract.</b> <see cref="Read"/> is polled ONCE per frame by the component that owns the
    /// mode, in its <c>Update</c>, and answers the intents for THAT frame. A source is never told a frame
    /// was skipped and must not remember one (rule 5: no hidden state beyond the last read). The shipped
    /// source reads the bindings asset; a scripted driver, a replay, an NPC or a test hands in another
    /// implementation of this and nothing downstream changes.</para>
    ///
    /// <para><b>Latency is the honest shape.</b> Because the read is in <c>Update</c>, an intent set on a
    /// held source from a coroutine lands on the mode one frame LATER — and a frame runs its physics
    /// steps BEFORE its Update. A fixture that counts steps from the instant it sets an intent is off by
    /// a step; <c>yield return null</c> after the set, THEN count. The keyboard has the same latency.</para>
    /// </summary>
    public interface IControlIntentSource<TIntents> where TIntents : struct
    {
        TIntents Read();
    }

    /// <summary>
    /// <b>Intents held until they are changed</b> — the scripted source, one per mode below.
    ///
    /// <para>What a headless journey walks the fisher with: set a move and it is still set on the next
    /// frame and the thirty after. A PlayMode fixture cannot deliver a virtual keypress to the Input
    /// System in this project (memory <c>playmode-virtual-keypress-is-undeliverable</c>), so a held
    /// source is how every journey drives a mode through the REAL component rather than around it.</para>
    ///
    /// <para><see cref="Reads"/> counts how many frames the mode actually asked. It is the anti-vacuous
    /// number: a test proving the seam carries an intent must also prove the seam was consulted.</para>
    /// </summary>
    public abstract class HeldIntents<TIntents> : IControlIntentSource<TIntents> where TIntents : struct
    {
        private TIntents _held;

        /// <summary>How many frames the intents have been read — see the class note.</summary>
        public int Reads { get; private set; }

        /// <summary>What is being asked right now.</summary>
        public TIntents Current => _held;

        public void Set(in TIntents intents) => _held = intents;

        /// <summary>Let go of everything — the mode's <c>None</c>.</summary>
        public void Release() => _held = default;

        public TIntents Read()
        {
            Reads++;
            return _held;
        }
    }

    /// <summary>The scripted on-foot source. <see cref="Walk"/> is the one-liner a journey wants.</summary>
    public sealed class HeldWalkIntents : HeldIntents<WalkIntents>
    {
        /// <summary>Hold a direction, at a walk or a run. The move is handed over as given — a journey
        /// that wants a diagonal clamped hands in a unit vector, as a pad would.</summary>
        public void Walk(Vector2 move, bool sprint = false) => Set(new WalkIntents(move, sprint, false, false));
    }

    /// <summary>The scripted aboard-on-foot source.</summary>
    public sealed class HeldDeckIntents : HeldIntents<DeckIntents>
    {
        public void Walk(Vector2 move) => Set(new DeckIntents(move, false));
    }
}
