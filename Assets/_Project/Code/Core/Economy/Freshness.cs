namespace HiddenHarbours.Core
{
    /// <summary>How a landed catch is being held right now — the thing that decides whether it rots.</summary>
    public enum StorageMode
    {
        /// <summary>In the open: a bucket on the wharf, a hold at sea. It spoils.</summary>
        Ambient = 0,
        /// <summary>Kept ALIVE by hydration — shellfish in a wet bucket, later a live well. Arrested.</summary>
        Live = 1,
        /// <summary>FROZEN — Aunt Ginny's freezer, later ice. Arrested.</summary>
        Frozen = 2,
    }

    /// <summary>
    /// The spoil policy: how fast each storage mode rots a catch, and how far rot can cut its value.
    /// Tunables live here (and later on <c>GameConfig</c>), never as literals in the maths — CLAUDE.md
    /// rule 6. Mirrors the <c>SeakeepingSettings.Default</c> pattern.
    /// </summary>
    public readonly struct SpoilPolicy
    {
        /// <summary>Rate scale while <see cref="StorageMode.Ambient"/>. 1 = the species' own rate.</summary>
        public readonly float AmbientRateMultiplier;
        /// <summary>Rate scale while <see cref="StorageMode.Live"/>. 0 = fully arrested.</summary>
        public readonly float LiveRateMultiplier;
        /// <summary>Rate scale while <see cref="StorageMode.Frozen"/>. 0 = fully arrested.</summary>
        public readonly float FrozenRateMultiplier;
        /// <summary>
        /// The spoil at which a buyer will no longer take it at any price. At and above this the catch is
        /// <b>rubbish</b>: worth nothing, sellable to no one, and still taking up hold space until it is
        /// dumped. The single dial for "how far gone before the fishmonger waves you off" — set it to 1 to
        /// let a catch stay sellable right up to the instant it is gone.
        /// </summary>
        public readonly float UnsellableSpoil;

        public SpoilPolicy(float ambientRateMultiplier, float liveRateMultiplier,
                           float frozenRateMultiplier, float unsellableSpoil)
        {
            AmbientRateMultiplier = ambientRateMultiplier;
            LiveRateMultiplier    = liveRateMultiplier;
            FrozenRateMultiplier  = frozenRateMultiplier;
            UnsellableSpoil       = unsellableSpoil;
        }

        /// <summary>
        /// The slice default. <see cref="StorageMode.Live"/> and <see cref="StorageMode.Frozen"/> both fully
        /// arrest for M1 — a slow live-holding attrition is a dial we can turn later without touching the
        /// maths. Refusal at 0.9 because a fishmonger turns away visibly-off fish well before it is liquid,
        /// and because a dead zone where you sell rot for pennies teaches nothing.
        /// </summary>
        public static SpoilPolicy Default => new SpoilPolicy(1f, 0f, 0f, 0.9f);
    }

    /// <summary>
    /// The freshness clock for one landed catch (M1 §7.3), as a <b>settle-on-read</b> accumulator:
    /// spoil already banked, the instant it was banked at, and the mode it has been held in since.
    ///
    /// <para><b>Why not a countdown.</b> A per-frame timer would drift, would not survive a save, and would
    /// be wrong across a sleep-skip. Storing <i>(accrued, lastSettle, mode)</i> makes spoil a pure function
    /// of <c>(state, now)</c> — so a reload, a sleep to next morning, and a fast-forward all land on exactly
    /// the same number, and the sim keeps its determinism guarantee (CLAUDE.md rule 5). Changing mode
    /// SETTLES first (see <see cref="Freshness.WithMode"/>), so an hour in the sun before the freezer is
    /// remembered forever.</para>
    /// </summary>
    public readonly struct FreshnessState
    {
        /// <summary>Spoil banked as of <see cref="LastSettleGameSeconds"/>, 0 (fresh) .. 1 (gone).</summary>
        public readonly float SpoilAccrued;
        /// <summary>The clock instant that <see cref="SpoilAccrued"/> is correct at.</summary>
        public readonly double LastSettleGameSeconds;
        /// <summary>How it has been held since <see cref="LastSettleGameSeconds"/>.</summary>
        public readonly StorageMode Mode;

        public FreshnessState(float spoilAccrued, double lastSettleGameSeconds, StorageMode mode)
        {
            SpoilAccrued = Freshness.Clamp01(spoilAccrued);
            LastSettleGameSeconds = lastSettleGameSeconds;
            Mode = mode;
        }
    }

    /// <summary>
    /// Pure freshness maths — no Unity types, no clock reads, no RNG, so it is fully EditMode-testable and
    /// bit-stable for the same inputs. Callers pass the instant and the tunables in; this decides nothing
    /// about where a catch is stored, only what that storage does to it.
    ///
    /// <para><b>Scope (first slice).</b> This is the contract + the arithmetic. Stamping catches at landing,
    /// the species' own perishability, the sell-price consequence and the buyer's REFUSAL gate, the
    /// freezer/live-bucket interactables, the <b>dispose verb</b> that empties rubbish out of a hold, the rot
    /// VISUAL (<c>CatchSpoilMath</c> already draws it and says "who sets spoil: nobody yet"), and persisting
    /// hold contents across a save all land in the slices after this one.</para>
    ///
    /// <para><b>Telegraphing is not optional.</b> Because a catch can become worth nothing, the player must
    /// be able to SEE it going — the rot tint on the item, a freshness read on the hold — well before it
    /// crosses <see cref="SpoilPolicy.UnsellableSpoil"/>. Losing a bucket you watched rot is a lesson;
    /// losing one that looked fine until the buyer refused it is a bug.</para>
    /// </summary>
    public static class Freshness
    {
        /// <summary>A catch just landed: nothing banked, the clock stamped, held however it starts.</summary>
        public static FreshnessState Landed(double nowGameSeconds, StorageMode mode = StorageMode.Ambient)
            => new FreshnessState(0f, nowGameSeconds, mode);

        /// <summary>The rate scale for a mode under a policy.</summary>
        public static float RateMultiplier(StorageMode mode, in SpoilPolicy policy)
        {
            switch (mode)
            {
                case StorageMode.Live:   return policy.LiveRateMultiplier;
                case StorageMode.Frozen: return policy.FrozenRateMultiplier;
                default:                 return policy.AmbientRateMultiplier;
            }
        }

        /// <summary>
        /// Spoil 0..1 at <paramref name="nowGameSeconds"/>, without banking it.
        /// <paramref name="spoilPerDay"/> is the species' ambient rate (1 = ruined in one in-game day);
        /// <paramref name="secondsPerDay"/> is <c>GameConfig.SecondsPerDay</c>, so no day length is
        /// hard-coded here.
        /// </summary>
        public static float SpoilAt(in FreshnessState state, double nowGameSeconds,
                                    float spoilPerDay, float secondsPerDay, in SpoilPolicy policy)
        {
            if (secondsPerDay <= 0f) return Clamp01(state.SpoilAccrued);

            // Clock never runs backwards in play; a stale/rewound stamp banks nothing rather than un-rotting.
            double elapsed = nowGameSeconds - state.LastSettleGameSeconds;
            if (elapsed <= 0d) return Clamp01(state.SpoilAccrued);

            double days = elapsed / secondsPerDay;
            double added = days * spoilPerDay * RateMultiplier(state.Mode, policy);
            return Clamp01((float)(state.SpoilAccrued + added));
        }

        /// <summary>Bank the spoil accrued up to <paramref name="nowGameSeconds"/> and restamp. Idempotent
        /// at a fixed instant, so calling it twice changes nothing.</summary>
        public static FreshnessState Settle(in FreshnessState state, double nowGameSeconds,
                                            float spoilPerDay, float secondsPerDay, in SpoilPolicy policy)
            => new FreshnessState(SpoilAt(state, nowGameSeconds, spoilPerDay, secondsPerDay, policy),
                                  nowGameSeconds > state.LastSettleGameSeconds
                                      ? nowGameSeconds : state.LastSettleGameSeconds,
                                  state.Mode);

        /// <summary>
        /// Move a catch into another storage mode. Settles FIRST, so time already spent in the old mode is
        /// banked and can never be undone by putting it in the freezer late — the beat the arc depends on.
        /// </summary>
        public static FreshnessState WithMode(in FreshnessState state, double nowGameSeconds,
                                              float spoilPerDay, float secondsPerDay,
                                              in SpoilPolicy policy, StorageMode mode)
        {
            FreshnessState settled = Settle(state, nowGameSeconds, spoilPerDay, secondsPerDay, policy);
            return new FreshnessState(settled.SpoilAccrued, settled.LastSettleGameSeconds, mode);
        }

        /// <summary>
        /// What the catch is worth as a fraction of its fresh value: 1 straight out of the water, falling
        /// linearly to <b>nothing</b>. Fresh fish is worth more money; rotten fish is worth no money.
        ///
        /// <para><b>Zero is not the whole story — see <see cref="IsSellable"/>.</b> This multiplier alone
        /// cannot make a catch worthless at the till, because <c>SellPricing.UnitPrice</c> floors every unit
        /// at 1₲. Refusal has to be a hard gate in front of the pricing maths, not a price that trends to
        /// zero. Anything that fails <see cref="IsSellable"/> must never reach the sell path at all.</para>
        /// </summary>
        public static float ValueMultiplier(float spoil, in SpoilPolicy policy)
            => 1f - Clamp01(spoil);

        /// <summary>
        /// True while a buyer will still take it. Below the policy's threshold it sells at a reduced price;
        /// at or above it, no one will have it at any price.
        /// </summary>
        public static bool IsSellable(float spoil, in SpoilPolicy policy)
            => Clamp01(spoil) < Clamp01(policy.UnsellableSpoil);

        /// <summary>
        /// True once it is rubbish: unsellable, unusable, and <b>still occupying hold space</b> until the
        /// player empties the bucket or dumps it over the side. That chore is the consequence — the loss is
        /// coin and a trip you wasted, never a ruined save (P5: teeth, telegraphed, survivable).
        /// </summary>
        public static bool IsSpoiled(float spoil, in SpoilPolicy policy) => !IsSellable(spoil, policy);

        /// <summary>Freshness as the player would read it: 1 = straight out of the water, 0 = gone.</summary>
        public static float Remaining(float spoil) => 1f - Clamp01(spoil);

        internal static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
