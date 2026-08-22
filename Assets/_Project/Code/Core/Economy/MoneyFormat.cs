using System.Globalization;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The game's money, written down — <b>one</b> copy of the glyph and the grouping, because the
    /// balance is now read in two modules rather than one.
    ///
    /// <para>It used to be a UI-only concern: <c>HudFormat.Money</c> owned the ₲ and the "N0", and the
    /// HUD band was the only thing that ever showed a balance. The 2026-08-22 diegetic ruling moved the
    /// money into the NOTEBOOK, which lives in <c>HiddenHarbours.World</c> — a module that cannot
    /// reference UI, and should not. Two spellings of a currency sign is exactly the drift nobody
    /// notices until a screenshot shows one surface reading "₲1,240" and another "G1240", so the
    /// formatting came down to Core where both can share it.</para>
    ///
    /// <para>These ALLOCATE (they build strings): call them when the value changes, never per frame —
    /// the change-detection stays at the call sites, where it can be seen. Invariant culture, because a
    /// balance is a number and not localised prose, and the test oracles compare it literally.</para>
    /// </summary>
    public static class MoneyFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The coin: ₲ (guarani sign), the game's currency mark.</summary>
        public const string Currency = "₲";

        /// <summary>A balance, like "₲1,240".</summary>
        public static string Balance(int money)
            => Currency + money.ToString("N0", Inv);

        /// <summary>
        /// A gain, like "+₲48". Non-positive reads a plain "₲0" rather than "+₲0" — the flash only ever
        /// celebrates a payout, so a zero is a guard, not a case worth a sign.
        /// </summary>
        public static string Payout(int amount)
            => amount <= 0 ? Currency + "0" : "+" + Currency + amount.ToString("N0", Inv);
    }
}
