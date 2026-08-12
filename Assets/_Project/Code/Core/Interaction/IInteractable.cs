using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where the actor must be for a candidate to be workable.</b> A flags enum rather than a single
    /// <see cref="ControlMode"/> because most things are workable from exactly one place (a shore fixture
    /// wants you on your feet) but some will be workable from two (a deck bucket you can reach from the
    /// deck AND from the wharf you are standing on).
    ///
    /// <para>Values are <b>append-only</b> — this is a Core contract and a registrant's declared context is
    /// data. Add a new place by adding a bit; never renumber the ones already spent.</para>
    /// </summary>
    [Flags]
    public enum InteractContext
    {
        /// <summary>Nothing — a candidate declaring this can never resolve. Useful as an explicit "off".</summary>
        None = 0,
        /// <summary>On your own two feet, ashore or wading.</summary>
        OnFoot = 1 << 0,
        /// <summary>Standing on a boat's deck (<see cref="ControlMode.OnDeck"/>).</summary>
        OnDeck = 1 << 1,
        /// <summary>At the helm (<see cref="ControlMode.Aboard"/>). See <see cref="InteractVerb"/> for why
        /// nothing can resolve here yet — E at the helm means "step back onto the deck" and the switcher
        /// answers it before the verb is ever consulted.</summary>
        AtHelm = 1 << 2,
        /// <summary>Every place there is. Convenience only — prefer naming the places you mean.</summary>
        Anywhere = OnFoot | OnDeck | AtHelm,
    }

    /// <summary>
    /// The named rungs of the candidate ladder, so a registrant declares an <i>intent</i> rather than a
    /// magic number (rule 6). They are plain ints on purpose: the gaps are there so a later rung can land
    /// between two of these without renumbering anything a scene already serialized.
    /// </summary>
    public static class InteractPriority
    {
        /// <summary>A thing bolted to the world that you walk up to and work — the seawater spot, Ginny's
        /// freezer, a shop counter. The default rung.</summary>
        public const int Fixture = 0;

        /// <summary>A loose object lying about that you can pick up — a pail, a fuel can, a coil of rope.
        /// Outranks a fixture because if you are standing over both, the thing at your feet is the thing
        /// you meant.</summary>
        public const int Portable = 10;

        /// <summary>Something already in your hands, which you are putting down / using. Outranks both:
        /// what you are carrying is always the most specific answer to "act".</summary>
        public const int Held = 20;
    }

    /// <summary>
    /// <b>A thing the one interact verb can act on</b> — the Core seam behind M2-39.
    ///
    /// <para><b>What problem this actually solves.</b> The dev key ledger is spent: every letter A–Z is
    /// claimed across four binding styles, so a feature that wants a button has nowhere to go. Everything
    /// that has wanted one lately has instead bolted a private <c>Update()</c> onto whatever key looked
    /// free and hand-asserted that its range does not overlap anyone else's — see <c>GinnyFreezer</c> and
    /// <c>WetBucketPoint</c>, which share <c>F</c> "the WorldInteractor precedent, ranges don't overlap".
    /// That invariant is asserted in a comment and checked by nobody. This interface replaces it: a
    /// candidate declares where it can be worked, how near you must be and how specific it is, and
    /// <see cref="InteractResolver"/> — one pure function — decides which single one the press belongs to.
    /// <b>New features register a candidate; they do not bind a key.</b></para>
    ///
    /// <para><b>Why an interface and not a struct of positions.</b> The same reason
    /// <see cref="IMooringCleat"/> is one: a candidate MOVES (a pail on a rocking deck) and its
    /// availability changes (a freezer you have already emptied). Both are read fresh at the moment the
    /// verb resolves. Baking either would re-open the hand-transcribed-constant failure the deck sidecars
    /// were built to close.</para>
    ///
    /// <para><b>Registration is scene-scoped</b> (<see cref="Interactables"/>) — register on enable,
    /// relinquish on disable, exactly as <see cref="IStandableSurface"/> and <see cref="IMooringCleat"/>
    /// do. An empty registry means "nothing here to act on", which is a valid answer and not a fault: with
    /// nothing registered the verb resolves nothing and every pre-existing INTERACT behaviour is reached
    /// unchanged.</para>
    ///
    /// <para><b>Nothing here is presentation.</b> A candidate says what it IS, never how it looks. The
    /// highlight (M2-39's other half, art-pipeline's) rides <see cref="InteractCandidateChanged"/> and
    /// matches on <see cref="Id"/> — so the interaction lane never references a renderer and the art lane
    /// never references a registrant (rule 4).</para>
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Stable id, for diagnostics, for the art highlight to match on, and — load-bearing — as the LAST
        /// tie-break in <see cref="InteractResolver"/>, so that two candidates that are otherwise exactly
        /// equal are separated by something deterministic rather than by which one happened to enable
        /// first.
        ///
        /// <para><b>Ids must be unique among live registrants.</b> Two registrants sharing an id make the
        /// resolver's ordering non-total, and registration order decides between them — the one
        /// non-determinism in the whole seam, and it is an authoring error, not a design allowance. Follow
        /// the <see cref="IMooringCleat.Id"/> convention: <c>fixture.st_peters.wet_bucket</c>.</para>
        /// </summary>
        string Id { get; }

        /// <summary>Where the thing is right now, in world metres. Read LIVE — a pail on a deck moves with
        /// the hull under it.</summary>
        Vector2 WorldPosition { get; }

        /// <summary>How close the actor must be, in metres, measured horizontally from
        /// <see cref="WorldPosition"/>. This is the candidate's own reach, not a global one: a bench you
        /// lean over and a rope you must be standing on are not the same distance.</summary>
        float ReachMeters { get; }

        /// <summary>How specific this candidate is — see <see cref="InteractPriority"/>. Higher wins
        /// outright, before distance is even looked at.</summary>
        int Priority { get; }

        /// <summary>Where the actor must be for this to be workable at all (a flags set — see
        /// <see cref="InteractContext"/>).</summary>
        InteractContext Contexts { get; }

        /// <summary>
        /// True if the actor must be FACING this to work it (the spec's "nearest in a forward arc").
        ///
        /// <para>False means omnidirectional: in reach is enough. That is the right answer for anything you
        /// operate by standing at it rather than by looking at it, and it is what lets an existing
        /// proximity-only interaction migrate onto this seam with <b>no</b> behaviour change — which is
        /// exactly how <c>WetBucketPoint</c> came across.</para>
        /// </summary>
        bool RequiresFacing { get; }

        /// <summary>
        /// The candidate's own gate: false removes it from consideration entirely, so it is neither
        /// resolved nor highlighted.
        ///
        /// <para><b>This is not "would the action succeed?".</b> A freezer with nothing in it is still
        /// AVAILABLE — press it and it says so, which is the cozy refusal (P5) and is what the pre-seam
        /// F-key components already did. Return false only when the thing genuinely is not a thing to act
        /// on right now (a trap that is not aboard, a door that is not there).</para>
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>Do the thing. Called at most once per press, on exactly one candidate, by
        /// <see cref="InteractVerb"/>.</summary>
        void Interact(in InteractActor actor);
    }
}
