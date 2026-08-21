namespace HiddenHarbours.Core
{
    /// <summary>Why a pour did not happen — one reason per sentence the verb can say back (P5: a refusal
    /// earns words, it does not fail silently). The sibling of <c>FuelRefusal</c>, and deliberately a
    /// SEPARATE enum: a pour has no money and no station, so half of that one could never apply here and
    /// a shared enum would make every call site handle cases it cannot reach.</summary>
    public enum PourRefusal
    {
        /// <summary>Nothing wrong — the pour goes through.</summary>
        None,
        /// <summary>You are not holding anything that holds fuel.</summary>
        NoSource,
        /// <summary>There is nothing here to pour INTO — no tank, or a hull that has none.</summary>
        NoDestination,
        /// <summary>The can in your hands is for a different fuel. A can's grade is its identity — see
        /// <see cref="IFuelVessel.Grade"/> — so this is a fact about the can, not a preference.</summary>
        GradeMismatch,
        /// <summary>The can is empty.</summary>
        SourceEmpty,
        /// <summary>Her tank is already brim-full.</summary>
        DestinationFull,
    }

    /// <summary>
    /// What a pour WOULD move, or why not. Pure value type — the sibling of <c>FuelQuote</c>, minus the
    /// money, because <b>a pour costs nothing</b>: you already paid at the pump or over the counter.
    /// </summary>
    public readonly struct PourQuote
    {
        /// <summary>Why not, or <see cref="PourRefusal.None"/>.</summary>
        public readonly PourRefusal Refusal;

        /// <summary>Litres that would move. 0 on any refusal.</summary>
        public readonly float Litres;

        /// <summary>True iff the pour should happen.</summary>
        public bool CanPour => Refusal == PourRefusal.None && Litres > 0f;

        public PourQuote(PourRefusal refusal, float litres)
        {
            Refusal = refusal; Litres = litres;
        }

        /// <summary>A refusal with nothing moved.</summary>
        public static PourQuote No(PourRefusal why) => new PourQuote(why, 0f);
    }

    /// <summary>
    /// <b>Fuel moving from one vessel to another</b> — the whole of the POUR verb's arithmetic, as one
    /// pure static class. <c>docs/design/fuel-and-refuelling.md</c> §9.4.
    ///
    /// <para><b>Written as <c>FuelPricing</c>'s sibling, on purpose.</b> Same shape: a
    /// <see cref="Quote"/> that decides, a numbers-only overload the tests drive, and a
    /// <see cref="Message"/> that puts the refusal in words. A reader who knows one knows both, and the
    /// two verbs behave the same way when they say no.</para>
    ///
    /// <para><b>What makes it a different verb and not a cheaper pump:</b> no money, no station, no
    /// posted price — and a SOURCE that can run out. A pump has an infinite supply, so filling is only
    /// ever limited by the vessel and the purse; a can holds what it holds, so a pour is limited by three
    /// things and the interesting one is the can.</para>
    ///
    /// <para><b>⭐ Two verbs is not tidiness, it is the geography.</b> St Peters has no pump — Ginny sells
    /// gas in a can over a counter. So at home you can only ever POUR, and FILL is something you go to
    /// Nine Mile Creek to do. The verb split <i>is</i> §3.</para>
    ///
    /// <para><b>Conserved, always.</b> <see cref="Pour"/> draws first and delivers exactly what it got.
    /// Fuel is never created by a rounding, and never destroyed by one either — which is why the source's
    /// own answer, not the quote, decides what arrives.</para>
    /// </summary>
    public static class FuelTransfer
    {
        /// <summary>
        /// Quote a pour from <paramref name="from"/> into <paramref name="to"/>.
        ///
        /// <para><paramref name="maxLitres"/> is what was ASKED for; pass 0 or less for "empty the can
        /// into her", which is what a press with no dial means and the only thing that can be asked
        /// today.</para>
        ///
        /// <para>Null-safe on both sides, so an empty-handed press and a boat with no tank take the same
        /// path as every other refusal rather than throwing.</para>
        /// </summary>
        public static PourQuote Quote(IFuelVessel from, IFuelVessel to, float maxLitres = 0f)
        {
            if (from == null) return PourQuote.No(PourRefusal.NoSource);
            if (to == null) return PourQuote.No(PourRefusal.NoDestination);
            return Quote(from.Grade, from.Litres, to.Grade, to.CapacityLitres, to.Litres, maxLitres);
        }

        /// <summary>
        /// The seam every rule actually lives on: no interfaces, no Unity, just the numbers. Tests drive
        /// this one; the <see cref="IFuelVessel"/> overload is the null-safe adapter over it.
        /// </summary>
        public static PourQuote Quote(string fromGrade, float fromLitres,
                                      string toGrade, float toCapacityLitres, float toLitres,
                                      float maxLitres = 0f)
        {
            // A destination that holds nothing is not a destination — a hull with FuelCapacityLitres 0
            // has no tank at all, which is the correct reading for the rowed dory and the state every
            // un-authored hull is in.
            if (!(toCapacityLitres > FuelVolumes.MinLitres)) return PourQuote.No(PourRefusal.NoDestination);

            // ⚠ The grade check comes FIRST, before empty and before full, for the reason §8.3 gives:
            // standing over the filler with the wrong can, the useful sentence is "that's a gas can", not
            // "it's empty". A can's grade IS what it holds — there is nowhere to record that a gas can
            // currently has diesel in it — so this refusal is about the object, not about its level.
            if (!string.Equals(fromGrade, toGrade, System.StringComparison.Ordinal))
                return PourQuote.No(PourRefusal.GradeMismatch);

            float available = fromLitres > 0f ? fromLitres : 0f;
            if (available <= FuelVolumes.MinLitres) return PourQuote.No(PourRefusal.SourceEmpty);

            float room = toCapacityLitres - (toLitres > 0f ? toLitres : 0f);
            if (room <= FuelVolumes.MinLitres) return PourQuote.No(PourRefusal.DestinationFull);

            float want = maxLitres > 0f ? maxLitres : available;
            float moved = want < available ? want : available;
            if (moved > room) moved = room;
            if (moved <= FuelVolumes.MinLitres) return PourQuote.No(PourRefusal.SourceEmpty);

            return new PourQuote(PourRefusal.None, moved);
        }

        /// <summary>
        /// Do it: quote, draw, deliver. Answers the litres that actually moved — 0 on any refusal.
        ///
        /// <para><b>The source's answer wins, not the quote's.</b> <see cref="IFuelVessel.Draw"/> returns
        /// what it really gave, and that — not the quoted figure — is what gets delivered. A vessel whose
        /// level is derived from something coarser (the shipped cans store a fill FRACTION and round to a
        /// drawn frame) can honestly hand over slightly less than asked, and conservation must survive
        /// that. Deliver the quote instead and a can that gave 4.99 L fills the tank by 5.00.</para>
        ///
        /// <para>The caller states the refusal; this returns the number. Same split as
        /// <c>FuelPump.TryFill</c>, so the verb decides what to SAY and the maths never publishes.</para>
        /// </summary>
        public static float Pour(IFuelVessel from, IFuelVessel to, float maxLitres = 0f)
        {
            PourQuote quote = Quote(from, to, maxLitres);
            if (!quote.CanPour) return 0f;

            float drawn = from.Draw(quote.Litres);
            if (drawn <= FuelVolumes.MinLitres) return 0f;

            to.Deliver(drawn);
            return drawn;
        }

        /// <summary>
        /// The refusal in words. Loc-seam literals, the <c>FuelPricing.Message</c> / <c>HudStrings</c>
        /// convention: centralised now, routed to loc tables when they land. <paramref name="grade"/> lets
        /// the sentence name the fuel, which is the whole warning in the mismatch case.
        /// </summary>
        public static string Message(PourRefusal refusal, string grade)
        {
            string fuel = FuelGrades.Display(grade);
            switch (refusal)
            {
                case PourRefusal.None:            return "";
                case PourRefusal.NoSource:        return "You've nothing in your hands to pour.";
                case PourRefusal.NoDestination:   return "There's no tank here to pour it into.";
                case PourRefusal.GradeMismatch:   return "She doesn't take " + fuel + ".";
                case PourRefusal.SourceEmpty:     return "The can's empty.";
                case PourRefusal.DestinationFull: return "Her tank's full to the brim already.";
                default:                          return "";
            }
        }
    }
}
