namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Something that holds a volume of one grade of fuel</b> — a jerry can in your hands, a drum on
    /// the wharf, and one day a boat's tank. The read-and-pour half of a fuel container, stated in Core so
    /// the economy can sell fuel into it without referencing the lane that draws it (rule 4).
    ///
    /// <para><b>Why an interface here rather than a field on <see cref="ICarriable"/>.</b> Most carriables
    /// hold nothing (a shovel, a pail of clams), and the things that will most want this are not carriable
    /// at all — a bulk tank, a hull's tank. "Can be lifted" and "holds fuel" are independent facts about a
    /// thing, so they are independent interfaces, and a pump asks for this one by
    /// <c>GetComponent</c> on whatever it is being offered. That is the same split
    /// <see cref="ICarryAnchored"/> draws for the same reason.</para>
    ///
    /// <para><b>The level is ONE number, and it lives with the implementor.</b> <see cref="Litres"/> is
    /// derived from whatever the implementor already stores (the container art stores a fill FRACTION,
    /// because that is what picks the baked frame), and <see cref="Deliver"/> writes back through the same
    /// quantity. Nothing here caches a second copy — a litre count beside a fill fraction is two numbers
    /// for one fact, and they drift.</para>
    ///
    /// <para><b>⚠️ Litres, not fuel-units.</b> <c>boats-and-navigation.md</c> measures a boat's tank in
    /// FUEL-UNITS (FU) and the shipped container Defs measure capacity in LITRES. Nothing reconciles the
    /// two yet because no tank exists to reconcile with; when the burn model lands, one of the two has to
    /// give, and the cheap answer is FU = litre. Until then this seam speaks litres, which is what the
    /// shipped data actually holds.</para>
    /// </summary>
    public interface IFuelVessel
    {
        /// <summary>
        /// Which fuel this vessel is FOR — one of <see cref="FuelGrades"/>. A can's grade is its identity
        /// (it is baked into the Def id, <c>fuelstore.gas_jerry_s20</c>), so it is what it holds, not a
        /// preference: there is nowhere to record "this gas can currently has diesel in it".
        /// </summary>
        string Grade { get; }

        /// <summary>How much it holds when brim-full, in litres. 0 for a vessel that holds nothing at all
        /// (a nozzle, a dispenser head) — callers must answer that without dividing by it.</summary>
        float CapacityLitres { get; }

        /// <summary>How much is in it right now, in litres. Read live.</summary>
        float Litres { get; }

        /// <summary>
        /// Pour <paramref name="litres"/> in. The implementor clamps to <see cref="CapacityLitres"/> and
        /// ignores a non-positive amount — a pump states what it delivered and does not check, exactly as
        /// <see cref="ICarriable.ShowFacing"/> is stated and not checked.
        ///
        /// <para>There is deliberately no <c>Drain</c>. Burning fuel is the boat's business and the boat
        /// has no tank yet; adding the other half now would be a seam with nothing on the far side of it.</para>
        /// </summary>
        void Deliver(float litres);
    }

    /// <summary>
    /// The fuel grades the world sells, stated once so no call site spells one as a literal.
    ///
    /// <para>This is a <b>fixed contract shared with the art lane</b>: the fuel rig bakes a colourway per
    /// grade and <c>FuelContainerDef.Grade</c> carries the same strings. Adding a grade means art as well
    /// as data, so the set is not casually extensible.</para>
    /// </summary>
    public static class FuelGrades
    {
        /// <summary>Petrol. Every outboard burns it, from Ned's two-stroke up.</summary>
        public const string Gas = "gas";

        /// <summary>Diesel. The bigger hulls' inboard engines, and nothing smaller.</summary>
        public const string Diesel = "diesel";

        /// <summary>Two-stroke premix — petrol with oil already in it. ⚠️ Whether Ned's motor actually
        /// REQUIRES this is an open owner question (<c>fuel-and-refuelling.md</c> §7 Q4); the grade is
        /// sellable either way.</summary>
        public const string Mixed = "mixed";

        /// <summary>Motor and gearcase oil, sold by the litre.</summary>
        public const string Oil = "oil";

        /// <summary>Furnace/stove oil — what the island's houses BURN (<c>municipal-infrastructure.md</c>:
        /// the island is off the wire and on oil). A purchasable grade; no delivery loop is modelled.</summary>
        public const string StoveOil = "stove_oil";

        /// <summary>Every grade, in the order they are authored on a station. Iteration order is stable so
        /// a validation sweep and a UI list agree.</summary>
        public static readonly string[] All = { Gas, Diesel, Mixed, Oil, StoveOil };

        /// <summary>True iff <paramref name="grade"/> is one of <see cref="All"/>. An unknown grade is an
        /// authoring error, not a new grade.</summary>
        public static bool IsKnown(string grade)
        {
            if (string.IsNullOrEmpty(grade)) return false;
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i], grade, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// The grade in words, for a verb label or a notice. Loc-seam literals, same convention as
        /// <c>HudStrings</c>: centralised now, routed to loc tables when they land. An unknown grade
        /// answers with itself rather than blank, so a mis-authored station still says something.
        /// </summary>
        public static string Display(string grade)
        {
            switch (grade)
            {
                case Gas: return "gas";
                case Diesel: return "diesel";
                case Mixed: return "two-stroke mix";
                case Oil: return "oil";
                case StoveOil: return "stove oil";
                default: return string.IsNullOrEmpty(grade) ? "fuel" : grade;
            }
        }
    }
}
