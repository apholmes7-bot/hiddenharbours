namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The player's soap, counted</b> (owner ask 2026-11-17: "add soap as a new inventory item to purchase
    /// and to be needed to reach 100% cleanliness"). Bars are a supply like ice or bread: the store sells
    /// them into the <see cref="SupplyLocker"/> under <see cref="CleanlinessSettings.SoapSupplyId"/>, and the
    /// book's "You have N" counts the unopened ones.
    ///
    /// <para><b>One bar, several washes.</b> A soaped wash at a shower or a bath uses one wash of
    /// <see cref="CleanlinessSettings.WashesPerSoapBar"/>. The first wash on a new bar takes the bar out of
    /// the locker and the washes it has left are counted in the same locker under
    /// <see cref="OpenBarId"/> (the soap id plus <see cref="OpenBarSuffix"/>), so the open bar is saved with
    /// the rest of the supplies and nothing new is added to the save. That entry has no Def: it is never
    /// listed or sold, only spent here.</para>
    ///
    /// <para>Static and null-safe like the locker: a null save holds no soap and spends nothing. No roll,
    /// no clock: a wash either has soap or it does not (rule 5).</para>
    /// </summary>
    public static class SoapStock
    {
        /// <summary>Appended to the soap id for the open bar's washes-left entry.</summary>
        public const string OpenBarSuffix = ".washes_left";

        /// <summary>The locker id the open bar's washes left are counted under, or null with the rule off.</summary>
        public static string OpenBarId(in CleanlinessSettings settings)
            => settings.SoapRuleOn ? settings.SoapSupplyId + OpenBarSuffix : null;

        /// <summary>Washes per bar, never below one.</summary>
        public static int WashesPerBar(in CleanlinessSettings settings)
            => settings.WashesPerSoapBar > 1 ? settings.WashesPerSoapBar : 1;

        /// <summary>Unopened bars in the locker.</summary>
        public static int Bars(SaveData save, in CleanlinessSettings settings)
            => settings.SoapRuleOn ? SupplyLocker.Count(save, settings.SoapSupplyId) : 0;

        /// <summary>Washes left on the open bar (0 when none is open).</summary>
        public static int OpenBarWashes(SaveData save, in CleanlinessSettings settings)
            => settings.SoapRuleOn ? SupplyLocker.Count(save, OpenBarId(settings)) : 0;

        /// <summary>Every soaped wash the player has left: the open bar's, plus a full bar's for each unopened one.</summary>
        public static int WashesLeft(SaveData save, in CleanlinessSettings settings)
            => OpenBarWashes(save, settings) + Bars(save, settings) * WashesPerBar(settings);

        /// <summary>True when there is soap for at least one more wash.</summary>
        public static bool Has(SaveData save, in CleanlinessSettings settings)
            => OpenBarWashes(save, settings) > 0 || Bars(save, settings) > 0;

        /// <summary>
        /// Use soap for ONE wash: a wash off the open bar, or else open a new bar (one bar out of the locker,
        /// its other washes counted as the open bar's). False, and nothing changed, with no soap, no save or
        /// the rule off.
        /// </summary>
        public static bool TryUseWash(SaveData save, in CleanlinessSettings settings)
        {
            if (save == null || !settings.SoapRuleOn) return false;
            string open = OpenBarId(settings);
            if (SupplyLocker.TrySpend(save, open)) return true;
            if (!SupplyLocker.TrySpend(save, settings.SoapSupplyId)) return false;
            SupplyLocker.Add(save, open, WashesPerBar(settings) - 1);   // a count of 0 adds nothing
            return true;
        }
    }
}
