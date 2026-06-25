using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Re-applies a loaded <see cref="SaveData"/> to the LIVE game so a save resumes exactly where it was
    /// saved (VS-08 load-restore). The save layer (<see cref="SaveService"/>) loads and migrates the blob;
    /// THIS is the inverse of <c>SaveService.SnapshotLiveState</c> — it pushes that blob back into the
    /// running services. The two are deliberately symmetric: snapshot reads the clock/wallet/fleet into the
    /// blob, restore writes them back out.
    ///
    /// <para><b>What gets restored, and how — all through Core seams (CLAUDE.md rule 4).</b></para>
    /// <list type="bullet">
    ///   <item><b>Clock</b> → <see cref="IGameClock.SeekTo"/> to the saved <see cref="SaveData.GameTimeSeconds"/>.
    ///   This is the determinism anchor: once the clock is at the saved instant, tide/wind/weather are
    ///   <em>recomputed</em> from <c>(worldSeed, gameTime)</c> — they are never in the save (rule 5).</item>
    ///   <item><b>Money</b> → brought to the saved <see cref="SaveData.Money"/> via the existing
    ///   <see cref="IWallet"/> API (an <see cref="IWallet.Add"/> of the delta), so the HUD's MoneyChanged
    ///   listeners update the same way a normal earn/spend would. There is no wallet "setter" to reach past.</item>
    ///   <item><b>Licences</b> → granted through <see cref="ILicenseService.Grant"/> (idempotent), the
    ///   same call the vendor uses. (Economy's LicenseService also self-seeds from the save on boot; this
    ///   makes the restore explicit and order-independent.)</item>
    ///   <item><b>Owned boats, repaired-boat state, gear</b> → these are read LIVE off
    ///   <see cref="ISaveService.Current"/> by their owning lanes (OwnedFleet re-grants its hulls off the
    ///   <see cref="GameLoaded"/> edge; <c>RepairLedger</c>/<c>PlayerGear</c> query the save directly), so
    ///   the act of loading the blob is itself the restore. <see cref="GameLoaded"/> is the single edge
    ///   those lanes re-sync on.</item>
    /// </list>
    ///
    /// <para>Static + dependency-injected (services passed in) so the mapping is fully EditMode-testable
    /// headless, with no scene and no <see cref="GameServices"/> globals required. The composition root
    /// (GameRoot, App) calls <see cref="ApplyToLiveServices"/> after it has wired the services.</para>
    /// </summary>
    public static class SaveRestore
    {
        /// <summary>
        /// Apply a loaded blob to the live services and announce it. Pass the services explicitly (the
        /// composition root has them in hand) so this is testable without the <see cref="GameServices"/>
        /// locator. Any argument may be null — a service that isn't present in this context (e.g. no wallet
        /// in the greybox, no licence service in EditMode) is simply skipped; the rest still restore.
        ///
        /// <para>Order: scalar state first (clock, money, licences), THEN publish <see cref="GameLoaded"/>
        /// last, so a subscriber that reads the clock/wallet while handling the signal sees the restored
        /// values. Publishing is the final step even if <paramref name="data"/> is null (a brand-new game
        /// still announces "loaded" so subscribers have one code path) — but with null data nothing is
        /// written, the fresh-game defaults stand.</para>
        /// </summary>
        public static void ApplyToLiveServices(
            SaveData data,
            IGameClock clock,
            IWallet wallet,
            ILicenseService licenses,
            bool publishLoaded = true)
        {
            if (data != null)
            {
                RestoreClock(data, clock);
                RestoreMoney(data, wallet);
                RestoreLicenses(data, licenses);
                // Boats / gear / repairs are restored by their owning lanes off the GameLoaded edge below
                // (OwnedFleet) or read live from the save (RepairLedger / PlayerGear) — nothing to push here.
            }

            if (publishLoaded)
                EventBus.Publish(new GameLoaded());
        }

        /// <summary>Seek the clock to the saved instant. Inverse of snapshotting <c>clock.TotalSeconds</c>.</summary>
        public static void RestoreClock(SaveData data, IGameClock clock)
        {
            if (data == null || clock == null) return;
            clock.SeekTo(data.GameTimeSeconds);
        }

        /// <summary>Bring the wallet to the saved balance via the public <see cref="IWallet"/> API (add the
        /// signed delta), so MoneyChanged fires for the HUD exactly as a normal transaction would. A no-op
        /// when already at the saved balance.</summary>
        public static void RestoreMoney(SaveData data, IWallet wallet)
        {
            if (data == null || wallet == null) return;
            int delta = data.Money - wallet.Money;
            if (delta != 0) wallet.Add(delta);
        }

        /// <summary>Grant every saved licence through the idempotent <see cref="ILicenseService.Grant"/> —
        /// the same call the vendor makes — so the live wallet matches the save. Idempotent and order-safe.</summary>
        public static void RestoreLicenses(SaveData data, ILicenseService licenses)
        {
            if (data == null || licenses == null) return;
            List<string> owned = data.OwnedLicenses;
            if (owned == null) return;
            for (int i = 0; i < owned.Count; i++)
            {
                string id = owned[i];
                if (!string.IsNullOrEmpty(id)) licenses.Grant(id);
            }
        }
    }
}
