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
        /// <summary>What a fully-spoiled catch is still worth, as a fraction of its fresh value.
        /// Deliberately &gt; 0: rot costs you coin, it never destroys the catch (P5 — cozy).</summary>
        public readonly float SpoiledValueFloor;

        public SpoilPolicy(float ambientRateMultiplier, float liveRateMultiplier,
                           float frozenRateMultiplier, float spoiledValueFloor)
        {
            AmbientRateMultiplier = ambientRateMultiplier;
            LiveRateMultiplier    = liveRateMultiplier;
            FrozenRateMultiplier  = frozenRateMultiplier;
            SpoiledValueFloor     = spoiledValueFloor;
        }

        /// <summary>
        /// The slice default. <see cref="StorageMode.Live"/> and <see cref="StorageMode.Frozen"/> both fully
        /// arrest for M1 — a slow live-holding attrition is a dial we can turn later without touching the
        /// maths. The 0.25 floor means a forgotten bucket still sells for a quarter: a real, visible loss,
        /// never a ruined day.
        /// </summary>
        public static SpoilPolicy Default => new SpoilPolicy(1f, 0f, 0f, 0.25f);
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
    /// the species' own perishability, the sell-price consequence, the freezer/live-bucket interactables,
    /// the rot VISUAL (<c>CatchSpoilMath</c> already draws it and says "who sets spoil: nobody yet"), and
    /// persisting hold contents across a save all land in the slices after this one.</para>
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
        /// What the catch is worth as a fraction of its fresh value: 1 when fresh, falling linearly to
        /// <see cref="SpoilPolicy.SpoiledValueFloor"/> when gone. Never zero, never negative — rot is a
        /// cost, not a punishment (P5).
        /// </summary>
        public static float ValueMultiplier(float spoil, in SpoilPolicy policy)
        {
            float t = Clamp01(spoil);
            float floor = Clamp01(policy.SpoiledValueFloor);
            return floor + (1f - floor) * (1f - t);
        }

        /// <summary>Freshness as the player would read it: 1 = straight out of the water, 0 = gone.</summary>
        public static float Remaining(float spoil) => 1f - Clamp01(spoil);

        internal static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
