using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What a fishing body is DOING this beat: which of the baked fight clips owns the figure.
    /// Derived purely from a <see cref="FishingState"/> (<see cref="FishingBeatMath"/>). Moved down from
    /// Player with the same names and values: the rod tables index by them, so they are append-only.</summary>
    public enum FishingPose
    {
        /// <summary>No pose: the normal iso walk skin owns the renderer.</summary>
        None = 0,
        /// <summary>A bite is on (the tell beat): the rod-tip two-tap sheet loops.</summary>
        Bite = 1,
        /// <summary>The hook was just set: the strike sheet plays ONCE as the fight opens.</summary>
        Strike = 2,
        /// <summary>The fight proper (deep or surface, or the legacy fight): the reel cycle loops.</summary>
        Reel = 3,
        /// <summary>She's aboard: the land sheet plays once and holds its last frame.</summary>
        Land = 4,

        // ---- appended by the presenter wave (the rod-out beats the kit always had sheets for) ------
        /// <summary>Line out, rod in hand (Waiting/Sinking): the hold cycle loops. Yields to the walk
        /// skin while the angler MOVES (the line stays out; the body walks).</summary>
        Hold = 5,
        /// <summary>The wind-back: the castBack sheet SCRUBBED by the wind-back charge
        /// (<c>FishingState.CastCharge01</c>), so the rod draws back exactly as far as the gesture has.</summary>
        CastBack = 6,
        /// <summary>The release: the castRelease sheet plays once as the line flies.</summary>
        CastRelease = 7,
    }

    /// <summary>
    /// <b>One mapping from a fishing phase to a body clip</b>, for the player and for every villager angler
    /// (ADR 0053 §11). Pure: no engine state, no <c>Time</c>, no RNG. Moved down from Player's
    /// <c>PlayerFishingAnimMath</c>, which keeps one-line forwarders, so World's angler stand and the
    /// player pick a clip with the same function.
    ///
    /// FLAG lead-architect: Core contract change (moved from Player).
    /// </summary>
    public static class FishingBeatMath
    {
        /// <summary>The skin clip a pose plays, as its state key; null for <see cref="FishingPose.None"/>.</summary>
        public static string StateKeyFor(FishingPose pose) => pose switch
        {
            FishingPose.Hold => "hold",
            FishingPose.CastBack => "castBack",
            FishingPose.CastRelease => "castRelease",
            FishingPose.Bite => "bite",
            FishingPose.Strike => "strike",
            FishingPose.Reel => "reel",
            FishingPose.Land => "land",
            _ => null,
        };

        /// <summary>
        /// The pose for a phase: <see cref="FishingPhase.WindBack"/> scrubs the castBack sheet,
        /// <see cref="FishingPhase.Cast"/> plays the release once, <see cref="FishingPhase.Waiting"/> and
        /// <see cref="FishingPhase.Sinking"/> hold the rod while the angler is STATIONARY (a moving angler
        /// hands the body back to the walk skin); <see cref="FishingPhase.Bite"/> and
        /// <see cref="FishingPhase.BiteNibble"/> play the bite tell; any rod FIGHT phase opens with the STRIKE
        /// (its first <paramref name="strikeSeconds"/>) then settles into the REEL loop;
        /// <see cref="FishingPhase.Landed"/> plays the land beat. <see cref="FishingPhase.Tending"/> and
        /// everything else is None. A villager angler passes <paramref name="stationary"/> true.
        /// </summary>
        /// <param name="secondsInFight">Seconds since a fight phase was entered (the caller's clock).</param>
        /// <param name="strikeSeconds">How long the strike beat owns the opening of the fight (s).</param>
        public static FishingPose PoseFor(FishingPhase phase, float secondsInFight, float strikeSeconds,
                                          bool stationary)
        {
            switch (phase)
            {
                case FishingPhase.WindBack:
                    return FishingPose.CastBack;
                case FishingPhase.Cast:
                    return FishingPose.CastRelease;
                case FishingPhase.Waiting:
                case FishingPhase.Sinking:
                    return stationary ? FishingPose.Hold : FishingPose.None;
                case FishingPhase.Bite:
                case FishingPhase.BiteNibble:
                    return FishingPose.Bite;
                case FishingPhase.Fighting:
                case FishingPhase.FightDeep:
                case FishingPhase.FightSurface:
                    return secondsInFight < Mathf.Max(0f, strikeSeconds) ? FishingPose.Strike : FishingPose.Reel;
                case FishingPhase.Landed:
                    return FishingPose.Land;
                default:
                    return FishingPose.None;
            }
        }
    }
}
