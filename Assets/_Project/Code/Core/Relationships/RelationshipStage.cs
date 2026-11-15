using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How two people stand with each other</b> (relationships slice 1, owner ruling 2026-11-14). Read
    /// from a pair's hidden strength through <see cref="RelationshipStageThresholds"/>; the player never sees
    /// the strength or the stage name as a bar, meter, number or heart (the ruling). They notice a stage only
    /// through changed talk, routines and opportunities.
    ///
    /// <para><b>Ordered warm to cold by value</b>, so "at least Friend" is <c>stage &gt;= Friend</c> and a
    /// rival is the coldest. <b>Append-only, explicit values</b>: a later save may store the number.
    /// <b>No romance stage</b> (owner ruling 2026-11-14: no romance yet); a test guards that.</para>
    /// </summary>
    public enum RelationshipStage
    {
        /// <summary>Cool, a little competitive. Never cruel, and can be won back.</summary>
        Rival = -2,

        /// <summary>Some friction: a short word, a cooler nod.</summary>
        Strained = -1,

        /// <summary>The default for any pair with no history: nods at the post office.</summary>
        Stranger = 0,

        /// <summary>Says hello by name.</summary>
        Acquaintance = 1,

        /// <summary>Mentions the other, eats lunch with them sometimes.</summary>
        Friend = 2,

        /// <summary>Family-close: visits, shares work, may go into business together.</summary>
        Close = 3,
    }

    /// <summary>
    /// <b>Where the stages begin</b> (rule 6): the bounds of a pair's strength and the edge of each stage.
    /// The owner tunes these on <c>Data/Resources/Relationships/RelationshipConfig.asset</c>;
    /// <see cref="Default"/> is only the fallback when no config ships (an EditMode test, a stripped build).
    ///
    /// <para>A strength at or below <see cref="RivalAtOrBelow"/> is <see cref="RelationshipStage.Rival"/>, at
    /// or below <see cref="StrainedAtOrBelow"/> is <see cref="RelationshipStage.Strained"/>, from
    /// <see cref="CloseFrom"/> up is <see cref="RelationshipStage.Close"/>, then Friend, then Acquaintance,
    /// and everything between is <see cref="RelationshipStage.Stranger"/>. <see cref="IsValid"/> says whether
    /// the edges are in order with 0 inside Stranger; the content validation test holds the shipped asset
    /// to it.</para>
    /// </summary>
    [Serializable]
    public struct RelationshipStageThresholds
    {
        [Tooltip("The coldest a pair's strength can go. Negative.")]
        public int MinStrength;

        [Tooltip("The warmest a pair's strength can go. Positive.")]
        public int MaxStrength;

        [Tooltip("At or below this strength the pair are rivals.")]
        public int RivalAtOrBelow;

        [Tooltip("At or below this strength (and above the rival edge) the pair are strained. Negative.")]
        public int StrainedAtOrBelow;

        [Tooltip("From this strength up the pair are acquaintances. Positive.")]
        public int AcquaintanceFrom;

        [Tooltip("From this strength up the pair are friends.")]
        public int FriendFrom;

        [Tooltip("From this strength up the pair are close.")]
        public int CloseFrom;

        /// <summary>The fallback edges, the plan's starting guesses (2026-11-14). The shipped config asset
        /// carries the owner's live numbers; this exists so a missing asset never means a crash.</summary>
        public static RelationshipStageThresholds Default => new RelationshipStageThresholds
        {
            MinStrength = DefaultMinStrength,
            MaxStrength = DefaultMaxStrength,
            RivalAtOrBelow = DefaultRivalAtOrBelow,
            StrainedAtOrBelow = DefaultStrainedAtOrBelow,
            AcquaintanceFrom = DefaultAcquaintanceFrom,
            FriendFrom = DefaultFriendFrom,
            CloseFrom = DefaultCloseFrom,
        };

        public const int DefaultMinStrength = -100;
        public const int DefaultMaxStrength = 100;
        public const int DefaultRivalAtOrBelow = -40;
        public const int DefaultStrainedAtOrBelow = -15;
        public const int DefaultAcquaintanceFrom = 10;
        public const int DefaultFriendFrom = 35;
        public const int DefaultCloseFrom = 70;

        /// <summary>True when Min &lt;= Rival &lt; Strained &lt; 0 &lt; Acquaintance &lt; Friend &lt; Close
        /// &lt;= Max: every stage is reachable and a pair with no history is a Stranger.</summary>
        public bool IsValid =>
            MinStrength <= RivalAtOrBelow
            && RivalAtOrBelow < StrainedAtOrBelow
            && StrainedAtOrBelow < 0
            && 0 < AcquaintanceFrom
            && AcquaintanceFrom < FriendFrom
            && FriendFrom < CloseFrom
            && CloseFrom <= MaxStrength;

        /// <summary>The stage <paramref name="strength"/> reads as.</summary>
        public RelationshipStage StageOf(int strength)
        {
            if (strength <= RivalAtOrBelow) return RelationshipStage.Rival;
            if (strength <= StrainedAtOrBelow) return RelationshipStage.Strained;
            if (strength >= CloseFrom) return RelationshipStage.Close;
            if (strength >= FriendFrom) return RelationshipStage.Friend;
            if (strength >= AcquaintanceFrom) return RelationshipStage.Acquaintance;
            return RelationshipStage.Stranger;
        }

        /// <summary><paramref name="strength"/> held inside [<see cref="MinStrength"/>,
        /// <see cref="MaxStrength"/>].</summary>
        public int Clamp(int strength) => Math.Max(MinStrength, Math.Min(MaxStrength, strength));
    }
}
