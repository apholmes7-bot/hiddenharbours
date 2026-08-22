namespace HiddenHarbours.UI
{
    /// <summary>One of the always-on band's conditions readouts — the four the diegetic ruling covers.</summary>
    public enum HudReadout
    {
        /// <summary>Height, direction and time-to-turn.</summary>
        Tide = 0,

        /// <summary>Direction, knots, Beaufort.</summary>
        Wind = 1,

        /// <summary>The sea-state word and its 0–7 number.</summary>
        Sea = 2,

        /// <summary>The balance, and the payout flash that rides with it.</summary>
        Money = 3,
    }

    /// <summary>
    /// What the player actually OWNS that could tell them a number. Nothing grants one yet — see
    /// <see cref="HudVisibilityPolicy.Owned"/> — so this is the shape the answer will arrive in, kept
    /// deliberately small: an instrument per readout, and no third state between "has one" and "does not".
    /// </summary>
    [System.Flags]
    public enum HudInstruments
    {
        None = 0,

        /// <summary>A tide clock — the dial that says how the water is standing without going to look.</summary>
        TideClock = 1 << 0,

        /// <summary>An anemometer — a wind gauge that reads in numbers rather than in the sound of it.</summary>
        Anemometer = 1 << 1,

        /// <summary>A sea-state gauge — the instrument that would put a 0–7 on what the boat is doing.</summary>
        SeaGauge = 1 << 2,
    }

    /// <summary>
    /// <b>May this readout be on screen?</b> — the owner's 2026-08-22 diegetic ruling, made in play and
    /// written down once.
    ///
    /// <para><b>The ruling.</b> The tide is not shown unless it comes from an instrument the player has.
    /// The money lives in the notebook. The sea state and the wind are not visible in normal play. All
    /// of them stay reachable for debugging. What the ruling is FOR: information in this game is earned
    /// — you read the water, or you buy the thing that reads it for you — and a band that hands you
    /// four numbers for free quietly deletes that.</para>
    ///
    /// <para><b>Why a policy and not four <c>if</c>s.</b> Each readout deciding for itself is how a band
    /// gets back the habit of showing things: one new label, one forgotten check, and the tide is on
    /// screen again with nobody having decided it should be.
    /// <see cref="HudController"/> asks this and does what it says. That also makes the rule provable
    /// without a canvas — the truth table below is the EditMode test's subject, in the same way
    /// <see cref="HelmHudSuppression"/>'s is.</para>
    ///
    /// <para><b>⚠ This governs the BAND, and nothing else.</b> The helm's instruments — the
    /// chartplotter, the dash compass, the apparent-wind read, the nav cluster — are not readouts under
    /// this policy and never pass through it. They are instruments the boat has, which is precisely the
    /// thing the ruling says a number should come from; suppressing them would be reading the ruling
    /// backwards. Where the nav cluster draws is still <see cref="HelmHudSuppression"/>'s answer alone.
    /// The clock/watch is likewise untouched: it is a hide-only window under the 2026-08-07 windowing
    /// ruling, and it IS an instrument — she is wearing it.</para>
    /// </summary>
    public static class HudVisibilityPolicy
    {
        /// <summary>
        /// The rule, as a pure function of what is owned and whether the dev override is up.
        ///
        /// <para><b>Money is not instrument-gated, and that is the point.</b> There is no gauge that
        /// could earn it back onto the band — the notebook is where a balance is kept, so outside the
        /// dev override this is <c>false</c> for every possible <paramref name="owned"/>. Written as its
        /// own arm rather than folded into a flag nobody sets, so the reason survives.</para>
        /// </summary>
        public static bool MayShow(HudReadout readout, HudInstruments owned, bool devShowAll)
        {
            if (devShowAll) return true;

            return readout switch
            {
                HudReadout.Tide  => (owned & HudInstruments.TideClock) != 0,
                HudReadout.Wind  => (owned & HudInstruments.Anemometer) != 0,
                HudReadout.Sea   => (owned & HudInstruments.SeaGauge) != 0,
                HudReadout.Money => false,   // the notebook's, always
                _ => false,                  // an unknown readout shows nothing: the ruling's default
            };
        }

        // ---- the live seam ----------------------------------------------------------------------
        // State, kept apart from the rule above so the rule stays pure and testable. Static for the
        // same reason BoatUiWindows and InteractAffordancePresenter.Variant are: the HUD has to read
        // it without a scene reference, and an editor menu has to set it without finding the HUD.

        /// <summary>
        /// What the player owns. <b>Nothing sets this yet</b>, and that is the shipped state, not an
        /// oversight: no tide clock, anemometer or sea gauge exists as content, so every band read is
        /// off in a new game exactly as the ruling asks. When those instruments are authored, the
        /// system that grants them writes here and the HUD follows with no further change.
        /// </summary>
        public static HudInstruments Owned { get; set; } = HudInstruments.None;

        /// <summary>
        /// The debugging override — every readout on, whatever is owned. Driven by the dev menu
        /// (<c>Hidden Harbours/Dev/HUD</c>); deliberately NOT a key, because the A–Z key ledger is
        /// exhausted and this is a developer's switch rather than an input the game is gaining.
        /// </summary>
        public static bool DevShowAll { get; set; }

        /// <summary>The rule against the live seam — what <see cref="HudController"/> actually calls.</summary>
        public static bool MayShow(HudReadout readout) => MayShow(readout, Owned, DevShowAll);

        /// <summary>Back to the shipped state. For tests and for a dev session that wants out of the
        /// override without hunting the menu it came from.</summary>
        public static void Reset()
        {
            Owned = HudInstruments.None;
            DevShowAll = false;
        }
    }
}
