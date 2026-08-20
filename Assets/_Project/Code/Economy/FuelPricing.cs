using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Why a fill did not happen — one reason per sentence the pump can say back (P5: a refusal
    /// earns words, it does not fail silently).</summary>
    public enum FuelRefusal
    {
        /// <summary>Nothing wrong — the fill goes through.</summary>
        None,
        /// <summary>The pump is not wired to a station, so nothing has a price. Authoring error.</summary>
        NoStation,
        /// <summary>This site does not sell that grade. The whole point of the geography (diesel is not
        /// sold at St Peters; the wharf pumps do not stock everything the road station does).</summary>
        GradeNotSold,
        /// <summary>You are not holding anything that holds fuel.</summary>
        NoVessel,
        /// <summary>The can in your hands is for a different fuel. A can's grade is its identity — see
        /// <see cref="IFuelVessel.Grade"/>.</summary>
        GradeMismatch,
        /// <summary>It is already brim-full.</summary>
        VesselFull,
        /// <summary>You cannot pay for even a splash.</summary>
        CannotAfford
    }

    /// <summary>
    /// What a fill WOULD do: how much fuel, for how much money, or why not. Pure value type — no Unity
    /// state, fully EditMode-testable, and the same struct the pump uses to decide and (later) a UI uses
    /// to show a price before the player commits.
    /// </summary>
    public readonly struct FuelQuote
    {
        /// <summary>Why not, or <see cref="FuelRefusal.None"/>.</summary>
        public readonly FuelRefusal Refusal;

        /// <summary>Litres that would be delivered. 0 on any refusal.</summary>
        public readonly float Litres;

        /// <summary>Whole money that would be charged. 0 on any refusal — and legitimately 0 on a free pump.</summary>
        public readonly int Price;

        /// <summary>True when the WALLET, not the vessel, decided how much: the player asked to fill up and
        /// got what they could pay for. The pump says so, so a half-full can is never a mystery.</summary>
        public readonly bool LimitedByMoney;

        /// <summary>True iff the pump should pour. A quote with no refusal but no litres cannot happen —
        /// <see cref="FuelPricing"/> turns that into <see cref="FuelRefusal.CannotAfford"/> — but the check
        /// is stated rather than assumed.</summary>
        public bool CanBuy => Refusal == FuelRefusal.None && Litres > 0f;

        public FuelQuote(FuelRefusal refusal, float litres, int price, bool limitedByMoney = false)
        {
            Refusal = refusal; Litres = litres; Price = price; LimitedByMoney = limitedByMoney;
        }

        /// <summary>A refusal with nothing delivered and nothing charged.</summary>
        public static FuelQuote No(FuelRefusal why) => new FuelQuote(why, 0f, 0);
    }

    /// <summary>
    /// <b>What a pump charges, and how much it pours</b> — the whole of fuel retail's arithmetic, as one
    /// pure static function. No Unity, no randomness, no state: the same inputs give the same quote
    /// forever, which is what makes it EditMode-testable and what rule 5 asks of anything the sim touches.
    ///
    /// <para><b>Every number that decides the outcome arrives as an argument.</b> Prices come from
    /// <see cref="FuelStationDef"/>, capacity and level from the <see cref="IFuelVessel"/>. The only
    /// constant in this file is <see cref="MinLitres"/>, and it is a float TOLERANCE, not a tunable —
    /// nothing about the game changes if it moves, only whether a fill of a ten-thousandth of a litre
    /// counts as a fill.</para>
    ///
    /// <para><b>Two rules worth stating out loud, because they are choices and not arithmetic:</b></para>
    /// <list type="number">
    /// <item><description><b>The charge rounds UP to whole money, once.</b> Money is an <c>int</c> and fuel
    /// is priced per litre, so something must round. Up, at the till, on the final figure — so no fill is
    /// ever free, and the player is never charged twice for the same rounding. (1.95 a litre into a 20 L
    /// can is 39, not twenty separate roundings of 2.)</description></item>
    /// <item><description><b>Short of money, the pump pours what you CAN pay for</b> rather than refusing.
    /// A pump that gives you nothing because you cannot afford a full tank is a worse pump than a real one,
    /// and it can strand a player who is one bad day from broke. The quote flags this as
    /// <see cref="FuelQuote.LimitedByMoney"/> so the pump can say it out loud. Asking for a fill you cannot
    /// afford therefore spends everything you have — deliberate, and the owner's to overrule (see
    /// <c>fuel-and-refuelling.md</c>).</description></item>
    /// </list>
    /// </summary>
    public static class FuelPricing
    {
        /// <summary>
        /// The smallest quantity that counts as fuel, in litres — a tenth of a millilitre. A float
        /// tolerance, NOT a balance tunable: it exists so that "the can is full" and "you can afford a
        /// splash" have crisp answers instead of turning on the last bit of a float.
        /// </summary>
        public const float MinLitres = 1e-4f;

        /// <summary>
        /// Quote a fill of <paramref name="vessel"/> from <paramref name="offer"/>.
        ///
        /// <para><paramref name="maxLitres"/> is what the player ASKED for; pass 0 or less for "fill her
        /// up", which is what a press with no dial means and is the only thing that can be asked today. A
        /// future UI that offers "ten litres" passes 10 and nothing else changes.</para>
        ///
        /// <para>Null-safe on the vessel (answers <see cref="FuelRefusal.NoVessel"/>) so a pump pressed
        /// with empty hands takes the same path as every other refusal.</para>
        /// </summary>
        public static FuelQuote Quote(in FuelGradeOffer offer, IFuelVessel vessel, int money, float maxLitres = 0f)
        {
            if (vessel == null) return FuelQuote.No(FuelRefusal.NoVessel);
            return Quote(offer, vessel.Grade, vessel.CapacityLitres, vessel.Litres, money, maxLitres);
        }

        /// <summary>
        /// The seam every rule actually lives on: no interfaces, no Unity, just the numbers. Tests drive
        /// this one; the <see cref="IFuelVessel"/> overload is the null-safe adapter over it.
        /// </summary>
        public static FuelQuote Quote(in FuelGradeOffer offer, string vesselGrade, float capacityLitres,
            float litresInVessel, int money, float maxLitres = 0f)
        {
            // A vessel that holds nothing is not a vessel — a nozzle or a dispenser head has
            // CapacityLitres 0, and every branch below would divide by it.
            if (capacityLitres <= 0f) return FuelQuote.No(FuelRefusal.NoVessel);

            if (!offer.Available) return FuelQuote.No(FuelRefusal.GradeNotSold);

            // The grade check comes BEFORE the full check on purpose: standing at the diesel pump with a
            // brim-full gas can, the useful sentence is "that is a gas can", not "it is full".
            if (!string.Equals(offer.Grade, vesselGrade, StringComparison.Ordinal))
                return FuelQuote.No(FuelRefusal.GradeMismatch);

            float room = capacityLitres - Mathf.Max(0f, litresInVessel);
            if (room <= MinLitres) return FuelQuote.No(FuelRefusal.VesselFull);

            float want = maxLitres > 0f ? Mathf.Min(maxLitres, room) : room;
            if (want <= MinLitres) return FuelQuote.No(FuelRefusal.CannotAfford);

            float perLitre = Mathf.Max(0f, offer.PricePerLitre);

            // A free pump is legal (a friend's drum, a story beat) and must not fall into the
            // money-limited branch below, where it would divide by zero.
            if (perLitre <= 0f) return new FuelQuote(FuelRefusal.None, want, 0);

            int fullPrice = Mathf.CeilToInt(want * perLitre);
            if (fullPrice <= money) return new FuelQuote(FuelRefusal.None, want, fullPrice);

            if (money <= 0) return FuelQuote.No(FuelRefusal.CannotAfford);

            // Money-limited: buy exactly what the purse covers. The price is the purse, NOT a re-round of
            // the litres — rounding a figure that was itself derived from the money is how you end up
            // charging one more than the player has.
            float affordable = Mathf.Min(money / perLitre, want);
            if (affordable <= MinLitres) return FuelQuote.No(FuelRefusal.CannotAfford);

            return new FuelQuote(FuelRefusal.None, affordable, money, limitedByMoney: true);
        }

        /// <summary>
        /// The refusal in words. Loc-seam literals, same convention as <c>HudStrings</c> and
        /// <c>CarryMath.Message</c>: centralised now, routed to loc tables when they land.
        /// <paramref name="grade"/> lets the sentence name the fuel, which is the whole warning in the
        /// mismatch and not-sold cases.
        /// </summary>
        public static string Message(FuelRefusal refusal, string grade)
        {
            string fuel = FuelGrades.Display(grade);
            switch (refusal)
            {
                case FuelRefusal.None:          return "";
                case FuelRefusal.NoStation:     return "This pump isn't hooked up to anything.";
                case FuelRefusal.GradeNotSold:  return "They don't sell " + fuel + " here.";
                case FuelRefusal.NoVessel:      return "You've nothing to put " + fuel + " in.";
                case FuelRefusal.GradeMismatch: return "That's not a " + fuel + " can.";
                case FuelRefusal.VesselFull:    return "It's full to the brim already.";
                case FuelRefusal.CannotAfford:  return "You can't pay for a drop.";
                default:                        return "";
            }
        }
    }
}
