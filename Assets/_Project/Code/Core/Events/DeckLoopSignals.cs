using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The pot the deck is working, as the deck needs to DRAW her</b> — her cell, and the three facts
    /// about how she piles.
    ///
    /// <para><b>Why the pot's facts travel with the beat.</b> How far the next pot up sits is a property
    /// of the POT, not of the boat under it — cones NEST at 0.52 m where a wooden bow-top needs 0.40 — so
    /// it lives on the trap's own Def, which Fishing owns. The deck that draws the stack is a Boats
    /// component, and Boats may not reference Fishing (rule 4). So the pot describes herself across the
    /// Core seam, alongside the beat she is being worked on.</para>
    ///
    /// <para><see cref="Slew"/> is the Def's OWN array, passed by reference and never written to — no
    /// copy, so publishing a beat allocates nothing (rule 7).</para>
    /// </summary>
    public readonly struct DeckPotLook
    {
        /// <summary>The pot's drawn cell, or null while her art is unwired — in which case the deck draws
        /// its greybox stand-in at exactly the same measured position.</summary>
        public readonly Sprite Cell;

        /// <summary>How far the next pot UP sits, metres (<c>TrapIso.STACK[build].step_m</c>). 0 or less
        /// means "unknown", and the deck keeps the last good step rather than collapsing the stack.</summary>
        public readonly float StackStepMeters;

        /// <summary>The per-tier lateral jog pattern (<c>TrapIso.STACK[build].slew</c>), repeating when a
        /// stack outgrows it. Null or empty is a dead-straight column, which is what a cone stack is.</summary>
        public readonly int[] Slew;

        /// <summary>What one slew step is worth in metres.</summary>
        public readonly float SlewMeters;

        public DeckPotLook(Sprite cell, float stackStepMeters, int[] slew, float slewMeters)
        {
            Cell = cell;
            StackStepMeters = stackStepMeters;
            Slew = slew;
            SlewMeters = slewMeters;
        }

        /// <summary>No pot described — the deck keeps whatever it last knew.</summary>
        public static DeckPotLook Unknown => new DeckPotLook(null, 0f, null, -1f);
    }

    /// <summary>
    /// <b>What the stern deck is working right now</b> — the presentation-facing snapshot of the gear
    /// cycle, so a drawer can show the beat without referencing the lane that owns it.
    ///
    /// <para><b>Why this exists.</b> Fishing owns the loop's state machine and its payout; Boats owns the
    /// hull the gear stands on and the slots it is hidden by. Neither may reference the other (rule 4), so
    /// the beat travels between them as a Core value struct — exactly the shape
    /// <see cref="TrapHaulStateChanged"/> already uses to get the haul to the fisher's hands without
    /// Player ever naming a trap.</para>
    ///
    /// <para><b>It carries no sim state and never travels back.</b> Nothing on this struct decides what is
    /// caught, what it is worth or whether a pot may be set — a listener that wrote to any of those would
    /// be writing sim from a presentation event (rule 5). It is a picture, published on a CHANGE of beat,
    /// never per frame (rule 7).</para>
    /// </summary>
    public readonly struct DeckLoopState
    {
        /// <summary>Which beat of the cycle the deck is on.</summary>
        public readonly SternDeckStage Stage;

        /// <summary>How far through the current beat the WORK has carried it, 0..1 — line hauled while
        /// hauling, animals worked over animals aboard while emptying. The quantity a driven clip is
        /// seeked from, and deliberately not a duration (the #469 rule).</summary>
        public readonly float Worked01;

        /// <summary>How many pots are stacked on the deck right now, across every surface — what the
        /// drawer builds its stacks from.</summary>
        public readonly int StackedPots;

        /// <summary>True while a pot is aboard being worked (as opposed to stacked or gone). The pot in
        /// play holds an occupant slot of its own.</summary>
        public readonly bool PotInPlay;

        /// <summary>How the pot being worked draws and stacks. <see cref="DeckPotLook.Unknown"/> on a beat
        /// that has no pot to describe — a drawer keeps the last pot it was told about, so the stack does
        /// not lose its art the moment the cycle goes idle.</summary>
        public readonly DeckPotLook Pot;

        public DeckLoopState(SternDeckStage stage, float worked01, int stackedPots, bool potInPlay)
            : this(stage, worked01, stackedPots, potInPlay, DeckPotLook.Unknown)
        {
        }

        public DeckLoopState(SternDeckStage stage, float worked01, int stackedPots, bool potInPlay,
                             DeckPotLook pot)
        {
            Stage = stage;
            Worked01 = worked01;
            StackedPots = stackedPots;
            PotInPlay = potInPlay;
            Pot = pot;
        }

        /// <summary>The idle snapshot — nothing worked, nothing in play. What a deck publishes when the
        /// cycle ends, so a drawer never has to infer "stopped" from silence.</summary>
        public static DeckLoopState Idle => new DeckLoopState(SternDeckStage.None, 0f, 0, false);
    }

    /// <summary>Raised when the stern deck's beat CHANGES (a stage transition, or a quantized step of the
    /// work within one) so the deck's gear presenter and the worker's clips can follow the cycle. Payload
    /// is a value struct (no GC). The twin of <see cref="TrapHaulStateChanged"/> for the deck half of the
    /// loop.</summary>
    public readonly struct DeckLoopStateChanged
    {
        public readonly DeckLoopState State;
        public DeckLoopStateChanged(DeckLoopState state) { State = state; }
    }
}
