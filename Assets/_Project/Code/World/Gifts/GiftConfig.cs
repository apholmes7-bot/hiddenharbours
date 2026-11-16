using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The gift tunables and the shared words</b> (gifting slice 1, rule 6): how many gifts a person
    /// counts in a game day, and the generic line set every person falls back to for a reaction they have
    /// no words of their own for. One asset, <c>Data/Resources/Gifts/GiftConfig.asset</c>, loaded by
    /// <see cref="Load"/>; every number and every line is the owner's.
    ///
    /// <para>The relationship deltas a gift moves are NOT here: they are rows on the relationship config
    /// (<c>RelationshipEventKind.GiftLoved</c> ... <c>GiftDisliked</c>), beside every other event's price.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character/Gift Config", fileName = "GiftConfig")]
    public sealed class GiftConfig : ScriptableObject
    {
        /// <summary>The asset's Resources path.</summary>
        public const string ResourcesPath = "Gifts/GiftConfig";

        /// <summary>The limit used when no asset loads (the owner's lean: one gift per person per day).</summary>
        public const int DefaultGiftsPerNpcPerDay = 1;

        [Tooltip("Gifts a person counts per game day. Further gifts that day are declined kindly and the " +
                 "item stays in the hand. The owner's lean is 1. Zero would mean no limit.")]
        [Min(0)] public int GiftsPerNpcPerDay = DefaultGiftsPerNpcPerDay;

        [Tooltip("The shared, generic words for each reaction, for anyone with no line of their own.")]
        public GiftLineSet Lines = new GiftLineSet();

        private static GiftConfig _cached;

        /// <summary>The shipped config, loaded on first need (a press, never a frame); null when none ships
        /// (a bare test rig). Callers then use <see cref="DefaultGiftsPerNpcPerDay"/> and say nothing for a
        /// reaction with no line.</summary>
        public static GiftConfig Load()
        {
            if (_cached == null) _cached = Resources.Load<GiftConfig>(ResourcesPath);
            return _cached;
        }
    }
}
