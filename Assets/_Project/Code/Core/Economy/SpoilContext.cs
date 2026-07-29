namespace HiddenHarbours.Core
{
    /// <summary>
    /// Everything needed to read a <see cref="CatchItem"/>'s spoil AT ONE INSTANT — the clock reading,
    /// the day length, and the policy — bundled so every consumer (the sell gate, the dispose verb, the
    /// hold read) evaluates the same catch to the same number in the same frame. The impure bridge
    /// between the pure <see cref="Freshness"/> maths and the live services: <see cref="Capture"/> reads
    /// <see cref="GameServices.Clock"/> / <see cref="GameServices.Config"/> once; everything after is a
    /// pure function of this struct, so tests construct one directly and never wire services.
    ///
    /// <para>An item created by the legacy <see cref="CatchItem"/> constructor has
    /// <see cref="CatchItem.SpoilPerDay"/> = 0, so under ANY context it reads spoil 0, multiplier 1,
    /// sellable — existing callers and their exact-payout tests are bit-identical.</para>
    /// </summary>
    public readonly struct SpoilContext
    {
        /// <summary>The instant this context reads spoil at (in-game seconds).</summary>
        public readonly double NowGameSeconds;
        /// <summary>In-game seconds per day (<c>GameConfig.SecondsPerDay</c>) — passed through to the
        /// maths so no day length is hard-coded (rule 6).</summary>
        public readonly float SecondsPerDay;
        /// <summary>The spoil policy in force (<c>GameConfig.Freshness.Policy</c>).</summary>
        public readonly SpoilPolicy Policy;

        public SpoilContext(double nowGameSeconds, float secondsPerDay, in SpoilPolicy policy)
        {
            NowGameSeconds = nowGameSeconds;
            SecondsPerDay = secondsPerDay;
            Policy = policy;
        }

        /// <summary>
        /// The live context: now from the Core clock, day length + policy from the shared config.
        /// Null-safe for EditMode / pre-boot — no clock reads as t = 0 and no config falls back to
        /// <see cref="FreshnessSettings.Default"/> / <see cref="GameConfig.DefaultSecondsPerDay"/>
        /// (pinned equal to the shipped asset), so a bare test rig prices exactly like the game.
        /// </summary>
        public static SpoilContext Capture()
        {
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            GameConfig config = GameServices.Config;
            float secondsPerDay = config != null ? config.SecondsPerDay : GameConfig.DefaultSecondsPerDay;
            SpoilPolicy policy = (config != null ? config.Freshness : FreshnessSettings.Default).Policy;
            return new SpoilContext(now, secondsPerDay, policy);
        }

        /// <summary>This item's spoil 0..1 at this context's instant (settle-on-read; nothing banked).</summary>
        public float SpoilOf(in CatchItem item)
            => Freshness.SpoilAt(item.Freshness, NowGameSeconds, item.SpoilPerDay, SecondsPerDay, Policy);

        /// <summary>True while a buyer will still take this item (the HARD gate in front of pricing —
        /// the value multiplier alone can never zero a price past the 1₲ floor).</summary>
        public bool IsSellable(in CatchItem item) => Freshness.IsSellable(SpoilOf(item), Policy);

        /// <summary>True once this item is rubbish: unsellable, still occupying hold space until dumped.</summary>
        public bool IsSpoiled(in CatchItem item) => Freshness.IsSpoiled(SpoilOf(item), Policy);

        /// <summary>What this item is worth as a fraction of its fresh value (1 → 0, no floor).</summary>
        public float ValueMultiplier(in CatchItem item)
            => Freshness.ValueMultiplier(SpoilOf(item), Policy);
    }
}
