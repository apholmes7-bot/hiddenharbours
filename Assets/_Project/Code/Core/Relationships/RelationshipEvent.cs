using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What happened between two people because of the player</b> (relationships slice 1). Each kind is
    /// one row of the rule table (<see cref="RelationshipRule"/>), so its weight and cap are data (rule 6).
    ///
    /// <para><b>The owner's three examples</b> (2026-11-14), and who raises them once their systems exist:
    /// two hires on one boat → <see cref="WorkedTogether"/> (the crew system, M3); the cannery restored →
    /// <see cref="SharedEmployerRestored"/> for each pair of its hands (the cannery restart, M3-30); buying
    /// mostly from one merchant → <see cref="CustomGiven"/> (player to that merchant) plus
    /// <see cref="CustomDiverted"/> (the favoured merchant and the one passed over). Slice 1 raises none of
    /// them: nothing in the game records an event yet.</para>
    ///
    /// <para><b>Append-only, explicit values</b>: a config asset stores the number, never the name.</para>
    /// </summary>
    public enum RelationshipEventKind
    {
        /// <summary>Two people worked the same boat. <c>Units</c> = days worked together.</summary>
        WorkedTogether = 0,

        /// <summary>Two people's shared workplace was brought back (the cannery). <c>Units</c> = 1 per
        /// restoration milestone.</summary>
        SharedEmployerRestored = 1,

        /// <summary>The player gave a merchant their custom. A is <see cref="RelationshipGraph.PlayerId"/>,
        /// B the merchant; <c>Units</c> = purchases. A merchant whose tie with the player stays low against
        /// their rivals is the one who later looks less busy (a later slice reads it; never shown as a
        /// number).</summary>
        CustomGiven = 2,

        /// <summary>The player bought from one merchant where another sells the same. A is the favoured
        /// merchant, B the one passed over; <c>Units</c> = purchases. Its rule is negative: the passed-over
        /// merchant cools toward the favoured one.</summary>
        CustomDiverted = 3,

        /// <summary>The player introduced two people ("Have you met ...?"). <c>Units</c> = 1.</summary>
        Introduced = 4,

        /// <summary>The player gave a person something they LOVE (gifting slice 1, 2026-11-15). A is
        /// <see cref="RelationshipGraph.PlayerId"/>, B the person; <c>Units</c> = 1 per gift. Raised by the
        /// world layer's gift act, one of the four gift kinds by <see cref="GiftReaction"/>.</summary>
        GiftLoved = 5,

        /// <summary>The player gave a person something they like. As <see cref="GiftLoved"/>.</summary>
        GiftLiked = 6,

        /// <summary>The player gave a person something they took politely. As <see cref="GiftLoved"/>.</summary>
        GiftNeutral = 7,

        /// <summary>The player gave a person something they dislike. As <see cref="GiftLoved"/>; its rule
        /// is negative.</summary>
        GiftDisliked = 8,
    }

    /// <summary>
    /// <b>One recorded event</b> between two people (any order: the pair is symmetric). An event with a
    /// null, empty or repeated id, or with no positive <see cref="Units"/>, is ignored, never a crash.
    /// </summary>
    public readonly struct RelationshipEvent
    {
        public readonly RelationshipEventKind Kind;
        public readonly string A;
        public readonly string B;
        public readonly int Units;

        public RelationshipEvent(RelationshipEventKind kind, string a, string b, int units = 1)
        {
            Kind = kind;
            A = a;
            B = b;
            Units = units;
        }
    }

    /// <summary>
    /// <b>One row of the rule table</b>: how far one unit of an event kind moves a pair, how far all of
    /// that kind may move one pair in a day, and whether it may push a pair into rivalry. Every number is
    /// the owner's, on the config asset (rule 6).
    /// </summary>
    [Serializable]
    public struct RelationshipRule
    {
        [Tooltip("The event kind this row prices. One row per kind.")]
        public RelationshipEventKind Kind;

        [Tooltip("Strength change per unit of the event. Positive warms the pair, negative cools it. Never 0.")]
        public int PointsPerUnit;

        [Tooltip("The most this kind may move one pair in one day, either way. Positive.")]
        public int CapPerPairPerDay;

        [Tooltip("Off: a cooling event stops at the strained edge and never makes rivals. On: it may.")]
        public bool CanMakeRival;

        public RelationshipRule(RelationshipEventKind kind, int pointsPerUnit, int capPerPairPerDay, bool canMakeRival)
        {
            Kind = kind;
            PointsPerUnit = pointsPerUnit;
            CapPerPairPerDay = capPerPairPerDay;
            CanMakeRival = canMakeRival;
        }

        /// <summary>A non-zero step and a positive cap.</summary>
        public bool IsValid => PointsPerUnit != 0 && CapPerPairPerDay > 0;
    }
}
