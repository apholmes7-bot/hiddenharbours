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
    /// <para><b>Litres, and fuel-units are the same thing.</b> <c>boats-and-navigation.md</c> measures a
    /// boat's tank in FUEL-UNITS (FU) and the shipped container Defs measure capacity in LITRES. That is
    /// settled, not pending: the two are ONE number, stated in full on
    /// <c>BoatHullDef.FuelCapacityLitres</c> (<c>fuel-and-refuelling.md</c> §9.1). So this seam speaks
    /// litres and there is no conversion anywhere — if you are ever tempted to add one, read that field
    /// first.</para>
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
        /// </summary>
        void Deliver(float litres);

        /// <summary>
        /// Take fuel OUT, up to <paramref name="litres"/>, and answer <b>how much was actually given</b> —
        /// which is never more than <see cref="Litres"/> and never negative. The mirror of
        /// <see cref="Deliver"/>, and the half that makes a container a container rather than a sink.
        ///
        /// <para><b>Why it returns rather than states.</b> <see cref="Deliver"/> can be told and not asked
        /// because the pump behind it has an infinite supply. Drawing is the other way round: only the
        /// vessel knows what it has, so only the vessel can say what it gave, and a caller that assumed it
        /// got what it asked for would quietly invent fuel. The return value IS the contract — move
        /// exactly that much and no more.</para>
        ///
        /// <para><b>This is the seam POUR stands on</b> (<c>fuel-and-refuelling.md</c> §9.4). It was
        /// deliberately absent while the boat had no tank, because it would have been a seam with nothing
        /// on the far side of it. The boat has a tank now. Every future vessel wants it too — a wharf
        /// bowser, a shed drum, a boat pumping into another boat under tow.</para>
        ///
        /// <para><b>⚠ Draw does NOT model burning.</b> An engine consuming fuel is the boat's own business
        /// and goes through its own tank state, not through this. This is TRANSFER: fuel leaving one
        /// vessel to arrive in another, conserved. See <see cref="FuelTransfer"/>, which is the only
        /// thing that should be calling it.</para>
        /// </summary>
        float Draw(float litres);
    }

    /// <summary>
    /// The one volume TOLERANCE the fuel seam works to, stated once so the pump and the pour agree about
    /// when a vessel counts as full or empty.
    ///
    /// <para><b>A tolerance, NOT a tunable</b> (rule 6 is untouched by it): nothing about the game changes
    /// if it moves, only whether a transfer of a ten-thousandth of a litre counts as a transfer. It lives
    /// in Core rather than beside the pump because <see cref="FuelTransfer"/> needs the same answer and
    /// cannot see the Economy module — and two constants for one quantity is exactly how "the can is
    /// full" and "the can is empty" end up disagreeing at the last bit of a float.</para>
    /// </summary>
    public static class FuelVolumes
    {
        /// <summary>The smallest quantity that counts as fuel, in litres — a tenth of a millilitre.</summary>
        public const float MinLitres = 1e-4f;
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
