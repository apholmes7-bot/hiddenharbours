using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A boat for sale where she lies</b>: the broken dory on the St Peters slip, offered by the man who
    /// owns her. One asset per sale (ADR 0003), with a stable <see cref="Id"/>. Everything the sale says
    /// or charges lives here, so the owner tunes it without code: the price, who sells, which hull it
    /// grants, and the lines the player thinks when he tries.
    ///
    /// <para><b>The sale grants ownership only.</b> Buying adds <see cref="BoatId"/> to the save's owned
    /// boats and leaves the repaired list alone: she is his and still broken. Mending and launching are
    /// later slices.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Slip Boat Sale", fileName = "SlipBoatSale")]
    public sealed class SlipBoatSaleDef : ScriptableObject
    {
        /// <summary>The token a line or label writes where the price goes.</summary>
        public const string PriceToken = "{price}";

        [Tooltip("Stable id, type.snake_case. Append-only: a save or a test may name it.")]
        public string Id = "slipsale.example";

        [Tooltip("The hull the sale grants (a BoatHullDef id). Added to the owned boats, never to the repaired ones.")]
        public string BoatId = "";

        [Tooltip("The NPC who owns her and takes the money (an NpcDef id).")]
        public string SellerNpcId = "";

        [Tooltip("The region she lies in (a RegionDef id).")]
        public string RegionId = "";

        [Tooltip("The price in coin.")]
        [Min(0)] public int Price;

        [Tooltip("How far from her middle the player can be and still press on her, metres.")]
        [Min(0.5f)] public float ReachMeters = 2.6f;

        [Tooltip("The verb shown while she is for sale. Writes {price} where the price goes.")]
        public string BuyLabel = "";

        [Tooltip("The verb shown once she is his.")]
        public string OwnedLabel = "";

        [Tooltip("What he thinks when the purse is short. Writes {price} where the price goes.")]
        public string TooDearLine = "";

        [Tooltip("What he thinks the moment she is his.")]
        public string BoughtLine = "";

        [Tooltip("What he thinks pressing on her once she is his: she still needs mending.")]
        public string NeedsMendingLine = "";

        [Tooltip("The voice the thoughts are written at (a DialogueVoiceDef id).")]
        public string VoiceId = "";

        /// <summary>A line or label with the price written in. The money format is the game's one
        /// (<see cref="HiddenHarbours.Core.MoneyFormat.Balance"/>).</summary>
        public string WithPrice(string text)
            => string.IsNullOrEmpty(text) ? text
                : text.Replace(PriceToken, HiddenHarbours.Core.MoneyFormat.Balance(Price));
    }
}
