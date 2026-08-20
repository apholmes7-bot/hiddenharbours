namespace HiddenHarbours.Core
{
    /// <summary>
    /// How a hull's tank is doing, as three named states — <c>docs/design/fuel-and-refuelling.md</c> §9.9.1.
    ///
    /// <para><b>⚠ These are FUEL states, not danger states.</b> <c>Adrift</c> and <c>Stranded</c> are
    /// deliberately NOT here: a boat with a dead engine may row home free or may need a tow, and which of
    /// those it is depends on the hull, the weather, the water she is in and whatever else is already
    /// wrong with her. That judgement belongs to the danger lane, which already owns breakdown, grounding,
    /// holing and a fouled prop, and which must keep ONE door into a stranding rather than gaining a
    /// second, parallel one for fuel. This enum says what the tank knows and stops there.</para>
    /// </summary>
    public enum BoatFuelState
    {
        /// <summary>She has fuel and no reason to think about it.</summary>
        Running = 0,

        /// <summary>At or below <c>GameConfig.Fuel.LowFuelFraction</c> of her tank. <b>A warning, not a
        /// limp mode</b> — she does everything she did a moment ago, and the player still has every
        /// choice, which is the entire point of warning at all (P5: teeth, but the escape is in your
        /// hands).</summary>
        LowFuel = 1,

        /// <summary>Dry. No engine. The helm and rudder still answer as she carries way, and the oars —
        /// on a hull that has them — never stop answering at all.</summary>
        Dry = 2,
    }

    /// <summary>
    /// Raised when one hull's fuel state CHANGES — <c>fuel-and-refuelling.md</c> §9.9.3. The one-way Core
    /// handoff this repo already uses: the economy lane publishes a fuel fact, and audio (the cough, the
    /// silence), the diegetic gauge and the danger systems pick it up without any of them referencing each
    /// other (rule 4). Payload is a value struct — no GC on a beat that can fire while the sea is working.
    ///
    /// <para><b>⚠ TRANSITIONS ONLY, and this is not a style preference.</b> The live level changes every
    /// fixed tick; an event per tick is a per-tick allocation on the hot path. Following
    /// <see cref="MooringLineChanged"/>'s stated convention exactly, and for the same reason: a consumer
    /// that wants the continuous read takes this beat to ARM itself and then reads the boat's own live
    /// value. The gauge does that. <b>The event does not carry the gauge.</b>
    /// <c>FuelStateEventsPlayTests</c> asserts ZERO events across a long steady run.</para>
    ///
    /// <para><b>⭐ <see cref="BoatFuelState.Dry"/> does NOT raise a stranding.</b> It raises a fuel fact,
    /// and <c>gameplay-systems</c> — which owns <c>FuelCheck</c>, <c>OnBreakdown</c> and <c>OnStranded</c>
    /// — decides what that means alongside grounding, holing and a fouled prop. Fuel must not open a
    /// second, parallel road to the same set-piece: there is one stranding system and it has one door.
    /// The economy lane raises this and prices the tow; it does not build the machine.</para>
    /// </summary>
    public readonly struct BoatFuelStateChanged
    {
        /// <summary>Stable hull id (<c>BoatHullDef.Id</c>), so a listener can tell whose tank this is
        /// without holding a reference to the boat.</summary>
        public readonly string HullId;

        /// <summary>What she was a moment ago.</summary>
        public readonly BoatFuelState From;

        /// <summary>What she is now. Never equal to <see cref="From"/> — that is what makes this a
        /// transition.</summary>
        public readonly BoatFuelState To;

        /// <summary>Litres aboard at the transition. A snapshot, not a subscription — read the vessel for
        /// the live value.</summary>
        public readonly float Litres;

        /// <summary>Litres over capacity at the transition, 0..1. Carried because the two consumers that
        /// arm on <see cref="BoatFuelState.LowFuel"/> both want to know how low, and neither should have
        /// to find the hull's Def to divide by it.</summary>
        public readonly float Fraction01;

        /// <summary><b>Can she get herself home with the engine dead?</b> — <c>BoatHullDef.CanRowHome</c>,
        /// carried on the beat so the danger lane can tell ADRIFT from STRANDED without reaching into the
        /// Boats module for the hull (rule 4). True on the dory and the punt; false on everything with an
        /// engine room.
        ///
        /// <para>⚠ Read this field, never <c>OarPower</c>. That field's DEFAULT is 300 and it was left
        /// untouched on six engine hulls including the 90-tonne side dragger, so inferring oars from it
        /// would let a dragger row home from a dead engine — and the bug would look like a physics bug
        /// for a week.</para></summary>
        public readonly bool CanRowHome;

        public BoatFuelStateChanged(string hullId, BoatFuelState from, BoatFuelState to,
                                    float litres, float fraction01, bool canRowHome)
        {
            HullId = hullId ?? "";
            From = from;
            To = to;
            Litres = litres;
            Fraction01 = fraction01;
            CanRowHome = canRowHome;
        }
    }

    /// <summary>
    /// Which state a level is in — the one classification, so the tank, a gauge and a test cannot
    /// disagree about where "low" begins.
    /// </summary>
    public static class BoatFuel
    {
        /// <summary>
        /// Classify a level against a capacity. Pure and static: no Unity, no clock, no state.
        ///
        /// <para>A hull with no tank answers <see cref="BoatFuelState.Running"/> — she is not dry, she
        /// simply has nothing to be dry ABOUT, and reporting her as <see cref="BoatFuelState.Dry"/> would
        /// hand the danger lane a breakdown for the rowed dory on the first tick of every game.</para>
        /// </summary>
        public static BoatFuelState Classify(float litres, float capacityLitres, float lowFraction)
        {
            if (!(capacityLitres > 0f)) return BoatFuelState.Running;
            if (!(litres > FuelVolumes.MinLitres)) return BoatFuelState.Dry;

            float low = lowFraction > 0f ? lowFraction : 0f;
            return litres <= capacityLitres * low ? BoatFuelState.LowFuel : BoatFuelState.Running;
        }
    }
}
