using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// ICE + LID on the deck container (M1 §7.3 — the portable cold chain). The freezer is free but
    /// only at home; this is cold that TRAVELS: ice is a consumable that arrests rot for a DURATION,
    /// a lid stretches that duration, and when the ice runs out the catch is ambient again. What ice
    /// sells the player is TIME AT SEA — stay out, fill the hold, make one trip where you'd have made
    /// two.
    ///
    /// <para><b>The maths contract.</b> Protection is piecewise-constant: this component owns the
    /// melt (how much ice, how the LID stretches it) and computes the ONE instant protection ends
    /// (<see cref="ProtectionEndsAtGameSeconds"/>); the settle hands that instant to
    /// <see cref="Freshness.SettleThroughProtection"/>, which knows nothing about lids or quantity.
    /// Sleeping straight through the ice running out therefore lands on exactly the number an
    /// unbroken session would (the §7.3 exit criterion). Tunables live on
    /// <see cref="DeckContainerDef"/> (rule 6); time comes from the Core clock via
    /// <see cref="SpoilContext.Capture"/>, never <c>Time.deltaTime</c> (rule 5).</para>
    ///
    /// <para><b>Wiring.</b> Runtime-spawned by its same-module sibling <see cref="ShipHold"/> (the
    /// DeckContainerPresenter pattern — no builder re-run). The island store selling ice and lids is
    /// economy-sim's (§7.5); until it lands, the DEV keys below are the only source — greybox
    /// scaffolding the shop replaces, exactly like DevSellInput.</para>
    ///
    /// <para><b>Known limit (flagged in the PR):</b> remaining ice/lid state is NOT in the save (the
    /// v5 shape carries hold contents only), so a quit-and-reload forfeits unspent ice: on load any
    /// still-Iced item is settled at the loaded instant and returned to ambient — freshness itself
    /// survives exactly; only the unspent duration is lost. Needs a coordinator call on a tiny v6
    /// addition (one double + one bool) if the loss reads as unfair in play.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeckIceBox : MonoBehaviour
    {
        [Tooltip("DEV: add one unit of ice (the store is §7.5's; this is greybox scaffolding).")]
        [SerializeField] private Key _devAddIceKey = Key.I;
        [Tooltip("DEV: toggle the lid.")]
        [SerializeField] private Key _devLidKey = Key.L;
        [Tooltip("Enable the dev keys above (off for release builds / when the store lands).")]
        [SerializeField] private bool _devKeys = true;

        private ShipHold _hold;                    // same-module sibling on the physics root
        private double _protectionEndsAt;          // game-seconds instant the ice gives out; ≤ now = none
        private bool _lidOn;

        /// <summary>True while the container is actively holding the catch on ice.</summary>
        public bool ProtectionActive
            => _protectionEndsAt > 0d && SpoilContext.Capture().NowGameSeconds < _protectionEndsAt;

        /// <summary>The one instant the current protection ends (game seconds); 0 when no ice.</summary>
        public double ProtectionEndsAtGameSeconds => _protectionEndsAt;

        /// <summary>Whether the lid is on (stretches the remaining ice).</summary>
        public bool LidOn => _lidOn;

        private DeckContainerDef Def => _hold != null && _hold.Hull != null ? _hold.Hull.DeckContainer : null;

        private void Awake() => _hold = GetComponent<ShipHold>();

        private void OnEnable() => EventBus.Subscribe<FishCaught>(OnFishCaught);
        private void OnDisable() => EventBus.Unsubscribe<FishCaught>(OnFishCaught);

        // A catch landed while the ice holds goes straight onto it — otherwise a fresh fish would rot
        // ambient inside an iced tote.
        private void OnFishCaught(FishCaught _) { if (ProtectionActive) ReMode(StorageMode.Iced); }

        private void Update()
        {
            // The one event of the piecewise model: the ice giving out. A per-frame double compare
            // (rule 7 — no allocation, no maths until it fires), and because the settle runs THROUGH
            // the protection instant, a sleep-skip that jumps past it lands on the exact number.
            if (_protectionEndsAt > 0d)
            {
                SpoilContext spoil = SpoilContext.Capture();
                if (spoil.NowGameSeconds >= _protectionEndsAt)
                    SettleThroughExpiry(in spoil);
            }

            if (!_devKeys) return;
            var kb = Keyboard.current;   // New Input System only — legacy Input throws here
            if (kb == null) return;
            if (kb[_devAddIceKey].wasPressedThisFrame)
            {
                if (TryAddIce(1))
                    EventBus.Publish(new DevNotice($"Packed a unit of ice ({DescribeRemaining()})."));
                else
                    EventBus.Publish(new DevNotice("The tote can't take more ice."));
            }
            if (kb[_devLidKey].wasPressedThisFrame)
            {
                SetLid(!_lidOn);
                EventBus.Publish(new DevNotice(_lidOn
                    ? $"Lid ON — the ice stretches ({DescribeRemaining()})."
                    : $"Lid OFF ({DescribeRemaining()})."));
            }
        }

        /// <summary>
        /// Pack <paramref name="units"/> of ice: extends the protection instant by the Def's hours per
        /// unit (lid-modified), capped at the Def's max banked units, and moves the catch onto ice.
        /// Returns false when the container takes no ice or is already fully banked.
        /// </summary>
        public bool TryAddIce(int units)
        {
            DeckContainerDef def = Def;
            if (def == null || def.MaxIceUnits <= 0 || units <= 0) return false;

            SpoilContext spoil = SpoilContext.Capture();
            double perUnit = SecondsPerUnit(def, _lidOn, spoil.SecondsPerDay);
            double cap = def.MaxIceUnits * perUnit;
            double remaining = System.Math.Max(0d, _protectionEndsAt - spoil.NowGameSeconds);
            if (remaining >= cap) return false;   // fully banked already

            remaining = System.Math.Min(cap, remaining + units * perUnit);
            _protectionEndsAt = spoil.NowGameSeconds + remaining;
            ReMode(StorageMode.Iced);
            return true;
        }

        /// <summary>Put the lid on / take it off. The REMAINING duration scales by the Def's lid
        /// factor (a lid stretches what's left; taking it off shrinks it back) — still one computable
        /// end instant, so the piecewise/sleep-skip contract is untouched.</summary>
        public void SetLid(bool on)
        {
            if (on == _lidOn) return;
            DeckContainerDef def = Def;
            SpoilContext spoil = SpoilContext.Capture();
            if (def != null && _protectionEndsAt > spoil.NowGameSeconds)
            {
                double remaining = _protectionEndsAt - spoil.NowGameSeconds;
                float factor = Mathf.Max(1f, def.LidMeltFactor);
                remaining = on ? remaining * factor : remaining / factor;
                _protectionEndsAt = spoil.NowGameSeconds + remaining;
            }
            _lidOn = on;
        }

        // ---- the two item rewrites (settle first, always — time already spent is banked) ----------

        /// <summary>Move every held item into <paramref name="mode"/>, settling each first
        /// (<see cref="Freshness.WithMode"/> — an hour in the sun before the ice is remembered).</summary>
        private void ReMode(StorageMode mode)
        {
            if (_hold == null || _hold.UsedUnits == 0) return;
            SpoilContext spoil = SpoilContext.Capture();
            var items = _hold.Items;
            var next = new List<CatchItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                next.Add(it.WithFreshness(Freshness.WithMode(it.Freshness, spoil.NowGameSeconds,
                                                             it.SpoilPerDay, spoil.SecondsPerDay,
                                                             spoil.Policy, mode)));
            }
            _hold.ReplaceAll(next);
        }

        /// <summary>The ice gave out: settle every iced item THROUGH the protection instant (banked
        /// protected leg + ambient leg, left ambient) and drop the protection.</summary>
        private void SettleThroughExpiry(in SpoilContext spoil)
        {
            double endedAt = _protectionEndsAt;
            _protectionEndsAt = 0d;
            if (_hold == null || _hold.UsedUnits == 0) return;

            var items = _hold.Items;
            var next = new List<CatchItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                next.Add(it.Freshness.Mode == StorageMode.Iced
                    ? it.WithFreshness(Freshness.SettleThroughProtection(it.Freshness,
                          spoil.NowGameSeconds, endedAt, it.SpoilPerDay, spoil.SecondsPerDay, spoil.Policy))
                    : it);
            }
            _hold.ReplaceAll(next);
            EventBus.Publish(new DevNotice("The ice has given out — the catch is on the clock again."));
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static double SecondsPerUnit(DeckContainerDef def, bool lidOn, float secondsPerDay)
        {
            double secondsPerHour = secondsPerDay / 24.0;
            return def.IceHoursPerUnit * secondsPerHour * (lidOn ? Mathf.Max(1f, def.LidMeltFactor) : 1f);
        }

        private string DescribeRemaining()
        {
            SpoilContext spoil = SpoilContext.Capture();
            double remaining = System.Math.Max(0d, _protectionEndsAt - spoil.NowGameSeconds);
            double hours = remaining / (spoil.SecondsPerDay / 24.0);
            return hours <= 0d ? "no ice" : $"~{hours:0.0} h of ice left";
        }

        /// <summary>Wire the sibling hold directly (tests — Awake ordering without a scene).</summary>
        public void Configure(ShipHold hold) => _hold = hold;
    }
}
