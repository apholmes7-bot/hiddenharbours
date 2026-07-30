using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// How a whole container of catch is doing, at one instant — the thing a player glancing at the
    /// tote needs to know, reduced from every item in it to the one that matters.
    /// </summary>
    public readonly struct HoldFreshnessSummary
    {
        /// <summary>How many items are in the hold. 0 means there is nothing to read.</summary>
        public readonly int ItemCount;

        /// <summary>Worst spoil in the hold, 0 (all fresh) .. 1 (gone). The hold reads as its worst
        /// item, because that is the one about to cost the player something.</summary>
        public readonly float WorstSpoil;

        /// <summary>How many items a buyer would already refuse (<see cref="Freshness.IsSpoiled"/>).</summary>
        public readonly int SpoiledCount;

        /// <summary>
        /// In-game seconds until the worst item crosses <see cref="SpoilPolicy.UnsellableSpoil"/> and a
        /// buyer stops taking it — the read that actually changes a decision ("can I finish the string,
        /// or do I run for the wharf now?"). Negative means <b>no countdown</b>: either the hold is
        /// empty, or the catch is arrested (frozen, alive, on ice) and is not going anywhere.
        /// </summary>
        public readonly double SecondsToRefusal;

        /// <summary>How the worst item is being held — what the player would change to buy time.</summary>
        public readonly StorageMode WorstMode;

        public HoldFreshnessSummary(int itemCount, float worstSpoil, int spoiledCount,
                                    double secondsToRefusal, StorageMode worstMode)
        {
            ItemCount = itemCount;
            WorstSpoil = worstSpoil;
            SpoiledCount = spoiledCount;
            SecondsToRefusal = secondsToRefusal;
            WorstMode = worstMode;
        }

        /// <summary>True when there is a real countdown to show (something is actively going off).</summary>
        public bool HasCountdown => SecondsToRefusal >= 0.0;

        /// <summary>True once something in the hold is already rubbish.</summary>
        public bool HasSpoiled => SpoiledCount > 0;

        /// <summary>Nothing in the hold — the read stays quiet.</summary>
        public static HoldFreshnessSummary Empty
            => new HoldFreshnessSummary(0, 0f, 0, -1.0, StorageMode.Ambient);
    }

    /// <summary>
    /// The container-level freshness READ (M1 §7.3). <see cref="Freshness"/>'s own notes call for it by
    /// name: <i>"the player must be able to SEE it going — the rot tint on the item, a freshness read on
    /// the hold — well before it crosses <see cref="SpoilPolicy.UnsellableSpoil"/>. Losing a bucket you
    /// watched rot is a lesson; losing one that looked fine until the buyer refused it is a bug."</i>
    ///
    /// <para><b>A read, not a sim.</b> Nothing here stamps, banks, mutates or stores anything. It is a
    /// pure fold over <see cref="IHold.Items"/> and a <see cref="SpoilContext"/>, both of which already
    /// exist — the same settle-on-read numbers the sell gate and the rot tint use, so the tote can never
    /// tell the player one thing and the fishmonger another.</para>
    ///
    /// <para><b>Why time-to-refusal and not time-since-landing.</b> The obvious read is "how long has
    /// this been sitting", but <see cref="FreshnessState"/> deliberately does not publish a landing
    /// instant — <see cref="FreshnessState.LastSettleGameSeconds"/> moves every time the catch changes
    /// storage mode, which is exactly what makes settle-on-read survive a save and a sleep-skip. Elapsed
    /// sitting time is therefore not recoverable from published state, and reaching past the contract to
    /// get it would break rule 4 for a worse number anyway: what changes a decision is not how long the
    /// fish has been there, it is how long until nobody will buy it. That IS derivable from published
    /// state, so that is what this returns. (Seam note flagged in the PR.)</para>
    ///
    /// <para>Pure, engine-free and allocation-free, so it is EditMode-testable without wiring services.</para>
    /// </summary>
    public static class HoldFreshness
    {
        /// <summary>Summarise a hold at the context's instant. A null or empty hold reads as
        /// <see cref="HoldFreshnessSummary.Empty"/>.</summary>
        public static HoldFreshnessSummary Summarise(IHold hold, in SpoilContext spoil)
            => hold == null ? HoldFreshnessSummary.Empty : Summarise(hold.Items, spoil);

        /// <summary>Summarise an item list directly — the seam tests use, and any presenter that
        /// already holds the list.</summary>
        public static HoldFreshnessSummary Summarise(IReadOnlyList<CatchItem> items, in SpoilContext spoil)
        {
            if (items == null || items.Count == 0) return HoldFreshnessSummary.Empty;

            float worstSpoil = -1f;
            int worstIndex = -1;
            int spoiledCount = 0;

            for (int i = 0; i < items.Count; i++)
            {
                CatchItem item = items[i];
                float s = spoil.SpoilOf(item);
                if (Freshness.IsSpoiled(s, spoil.Policy)) spoiledCount++;
                if (s > worstSpoil)
                {
                    worstSpoil = s;
                    worstIndex = i;
                }
            }

            CatchItem worst = items[worstIndex];
            return new HoldFreshnessSummary(
                items.Count,
                worstSpoil,
                spoiledCount,
                SecondsToRefusal(worst, worstSpoil, spoil),
                worst.Freshness.Mode);
        }

        /// <summary>
        /// In-game seconds until <paramref name="item"/> crosses the policy's refusal threshold, or -1
        /// when there is no countdown — it is already refused, or its storage mode has arrested it (a
        /// frozen or living catch is not on a clock).
        /// </summary>
        public static double SecondsToRefusal(in CatchItem item, float spoilNow, in SpoilContext spoil)
        {
            float threshold = Freshness.Clamp01(spoil.Policy.UnsellableSpoil);
            if (spoilNow >= threshold) return -1.0;              // already rubbish — nothing to count down
            if (spoil.SecondsPerDay <= 0f) return -1.0;

            float rateMultiplier = Freshness.RateMultiplier(item.Freshness.Mode, spoil.Policy);
            double perSecond = item.SpoilPerDay * rateMultiplier / spoil.SecondsPerDay;
            if (perSecond <= 0.0) return -1.0;                   // arrested: frozen, alive, or on ice

            return (threshold - spoilNow) / perSecond;
        }

        /// <summary>
        /// In-game seconds until <paramref name="item"/> is refused, reading spoil at the context's
        /// instant. The convenience overload for a caller that has not already computed spoil.
        /// </summary>
        public static double SecondsToRefusal(in CatchItem item, in SpoilContext spoil)
            => SecondsToRefusal(item, spoil.SpoilOf(item), spoil);
    }

    /// <summary>
    /// The few words the hold's freshness read shows. Centralised here for the same reason
    /// <c>HudStrings</c> centralises the HUD's: there is no runtime localization system yet, and this is
    /// the seam for one — when loc tables land these accessors route to them and no call site changes.
    ///
    /// <para>They live in Core rather than in <c>HiddenHarbours.UI</c> because the reader is the deck
    /// container, which is a Boats-lane presenter, and Boats cannot reference the UI assembly. Core is
    /// the one place both lanes can see — the same argument that moved <see cref="RotTint"/> here so
    /// every hold-reading presenter tints from one formula.</para>
    /// </summary>
    public static class HoldReadStrings
    {
        /// <summary>The mark that something is going off — a warning triangle, not colour alone (§8).</summary>
        public const string TurningGlyph = "⚠";

        /// <summary>"⚠ 4h" / "⚠ 40m" — how long before a buyer stops taking the worst of the catch.
        /// Rounded down, because a read that says an hour when you have fifty minutes is a trap.</summary>
        public static string Countdown(double inGameHours)
        {
            if (inGameHours < 0.0) inGameHours = 0.0;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (inGameHours >= 1.0)
                return TurningGlyph + " " + ((int)inGameHours).ToString(inv) + "h";
            int minutes = (int)(inGameHours * 60.0);
            return TurningGlyph + " " + minutes.ToString(inv) + "m";
        }

        /// <summary>Turning, but arrested — the catch is held and off the clock for now.</summary>
        public static string Held(StorageMode mode) => mode switch
        {
            StorageMode.Frozen => TurningGlyph + " frozen",
            StorageMode.Iced   => TurningGlyph + " on ice",
            StorageMode.Live   => TurningGlyph + " alive",
            _                  => TurningGlyph,
        };

        /// <summary>Past saving: how many of them nobody will buy.</summary>
        public static string Rubbish(int count)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return count > 1
                ? TurningGlyph + " " + count.ToString(inv) + " spoiled"
                : TurningGlyph + " spoiled";
        }
    }
}
