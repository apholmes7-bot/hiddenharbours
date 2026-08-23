using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Feeds a container's PICTURE from a cargo hold — THROUGH CORE ONLY (rule 4): the hold is resolved as
    /// the <see cref="IHold"/> Core interface on this object or a parent (the same
    /// <c>GetComponent&lt;IHold&gt;()</c> resolution <c>WharfSellPoint</c> uses against the provider/proxy
    /// pattern), and refreshes ride the Core bus edges the deck tray already established —
    /// <see cref="FishCaught"/>, <see cref="CatchSold"/>, <see cref="CatchDumped"/>,
    /// <see cref="GameLoaded"/>, <see cref="BoatPurchased"/>. No reference to Boats, Fishing or Economy
    /// concrete classes.
    ///
    /// <para><b>⭐ THE FILL FIX (and what was actually broken).</b> This bridge used to be welded to
    /// <see cref="CatchFillRenderer"/> by a <c>RequireComponent</c>, and to push everything through it —
    /// including the species→kind mapping, which it read off <c>_renderer.Library</c> and gave up on when
    /// that was null. Two consequences, both silent:
    /// <list type="number">
    ///   <item><b>A pail could never fill.</b> The tote renderer draws item sprites on the TOTE rig's
    ///   measured slot points; the bucket rig publishes no such points, because its fills are baked whole
    ///   states. So a bucket wired here resolved a geometry it does not have, drew nothing, and the fish
    ///   you had just pressed E to store was invisible while the hold said 1/20.</item>
    ///   <item><b>An unwired art table stopped the bridge dead</b> — <c>SetContents(null, 0)</c> and an
    ///   early return — so a container whose Library had not been assigned looked identical to an empty
    ///   one, with nothing logged.</item>
    /// </list>
    /// Both are fixed the same way: the bridge now talks to <see cref="ICatchFillTarget"/>, feeds EVERY
    /// such target on the object, and says out loud (once) when it has a hold but nothing to draw with.</para>
    ///
    /// <para><b>Seam note for lead-architect (flagged in the storage PR):</b> Core today has no
    /// service-locator read for "the player's hold" (<c>GameServices</c> carries Wallet/Licenses/
    /// ActiveBoat but not IHold), so this bridge requires an IHold provider in its own hierarchy — fine
    /// for deck containers, the wharf's <c>PersistentHoldProxy</c> object and a carried pail that IS its
    /// own hold, but a free-standing dock tote would need either that proxy alongside it or a minimal
    /// <c>GameServices.Hold</c>-style accessor. If scene layouts start wanting the latter, that accessor
    /// is the right Core addition; this component would then prefer it over the hierarchy walk.</para>
    ///
    /// <para>Event-driven only: no per-frame polling; the species→kind mapping reuses the target's
    /// <see cref="CatchItemLibrary"/> (one data asset, one truth). The kind buffer is reused, so a
    /// refresh allocates nothing once warm.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoldCatchFillSource : MonoBehaviour
    {
        private readonly List<ICatchFillTarget> _targets = new List<ICatchFillTarget>();
        private IHold _hold;
        private readonly List<string> _kinds = new List<string>();
        private bool _warnedNoArt;

        // The freshness clock's VISUAL tick (M1 §7.3): spoil accrues continuously (settle-on-read), so
        // the tint can't be purely event-driven — a slow realtime cadence re-reads the worst item and
        // feeds SetSpoil (which dirty-checks, so an unchanged read costs nothing). Display only — the
        // cadence is presentation, never sim (rule 5 untouched); throttled, no per-frame work (rule 7).
        private const float SpoilTickSeconds = 2f;
        private float _nextSpoilTick;

        private void Awake() => ResolveTargets();

        private void OnEnable()
        {
            // The Core edges a hold's contents can change on — the same set the Boats-lane deck tray
            // subscribes to (landed catch, sale, dumped rubbish, load-restore, bought hull).
            EventBus.Subscribe<FishCaught>(OnFishCaught);
            EventBus.Subscribe<CatchSold>(OnCatchSold);
            EventBus.Subscribe<CatchDumped>(OnCatchDumped);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            EventBus.Subscribe<BoatPurchased>(OnBoatPurchased);
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<CatchSold>(OnCatchSold);
            EventBus.Unsubscribe<CatchDumped>(OnCatchDumped);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            EventBus.Unsubscribe<BoatPurchased>(OnBoatPurchased);
        }

        private void OnFishCaught(FishCaught _) => Refresh();
        private void OnCatchSold(CatchSold _) => Refresh();
        private void OnCatchDumped(CatchDumped _) => Refresh();   // dumped rubbish leaves the tote too
        private void OnGameLoaded(GameLoaded _) => Refresh();
        private void OnBoatPurchased(BoatPurchased _) => Refresh();

        /// <summary>
        /// Re-read the hold and hand every target plain data. Public so tools/tests can force a re-read;
        /// event-time only otherwise.
        ///
        /// <para><b>The fullness fraction is a fraction of CAPACITY and the target decides what to do with
        /// it</b> — the tote turns it into a slot count, the pail into a band. What it must never do is
        /// hide a catch that is really there: a hold with items but no declared capacity (an
        /// ownership-gated pail, a proxy that has not resolved a hull yet) would divide to zero and read
        /// EMPTY with fish in it, so a hold that holds something reports at least a sliver of fill.</para>
        /// </summary>
        public void Refresh()
        {
            ResolveTargets();

            // Re-resolve when missing: the hold provider can spawn after us (scene load order).
            _hold ??= GetComponentInParent<IHold>();
            if (_hold == null || _targets.Count == 0)
            {
                Push(null, 0f);
                return;
            }

            IReadOnlyList<CatchItem> items = _hold.Items;
            int used = items != null ? items.Count : 0;

            // ⚠️ NO ART TABLE IS NOT NO CATCH. This used to push null and return, so a container whose
            // Library had not been assigned looked identical to an empty one with nothing logged. Now the
            // species id itself stands in as the kind: the COUNT and the fill band are then exactly right
            // (a pail still reads as holding three things), and only the chosen picture degrades — the
            // tote skips a kind it has no sprite for, and the pail falls back to the rig's default catch
            // group. Loud once, because it IS a wiring mistake; never silent, and never empty.
            CatchItemLibrary library = ResolveLibrary();
            if (library == null && !_warnedNoArt && used > 0)
            {
                _warnedNoArt = true;
                Debug.LogWarning($"[{nameof(HoldCatchFillSource)}] '{name}' has {used} catch in its hold " +
                                 "and no CatchItemLibrary on any fill target, so the species id is " +
                                 "standing in for the visual kind — the container fills, but with the " +
                                 "wrong picture. Wire the art table on the renderer/presenter (the " +
                                 "builders do this; a hand-made object must too).", this);
            }

            _kinds.Clear();
            for (int i = 0; i < used; i++)
                _kinds.Add(library != null ? library.KindFor(items[i].SpeciesId) : items[i].SpeciesId);

            // ⚠️ The MIN is the fix, not decoration. UsedUnits/CapacityUnits is 0/0 for a hold whose
            // capacity is gated (the pail before gear.bucket is owned) or not yet resolved, and a 0
            // fraction with a non-empty item list draws NOTHING at either target — the container is full
            // and looks empty, which is precisely the defect this whole path was reported for.
            float fill01 = _hold.CapacityUnits > 0
                ? Mathf.Clamp01((float)_hold.UsedUnits / _hold.CapacityUnits)
                : (used > 0 ? MinVisibleFill : 0f);

            Push(_kinds, fill01);
            PushSpoil();
        }

        /// <summary>The fill a hold with contents but no declared capacity reports. Small enough to read
        /// as the least-full state every target has (the tote's one item, the pail's FEW band), large
        /// enough that no rounding can take it back to zero.</summary>
        private const float MinVisibleFill = 0.05f;

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextSpoilTick) return;
            _nextSpoilTick = Time.unscaledTime + SpoilTickSeconds;
            PushSpoil();
        }

        /// <summary>Feed every target the WORST item's settled spoil — the container telegraphs its most
        /// urgent problem (a fresh tote with one rotten fish should read wrong, not fine).</summary>
        private void PushSpoil()
        {
            if (_targets.Count == 0) return;
            if (_hold == null) { PushSpoilValue(0f); return; }

            SpoilContext spoil = SpoilContext.Capture();
            float worst = 0f;
            IReadOnlyList<CatchItem> items = _hold.Items;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                float s = spoil.SpoilOf(items[i]);
                if (s > worst) worst = s;
            }
            PushSpoilValue(worst);
        }

        private void Push(IReadOnlyList<string> kinds, float fill01)
        {
            for (int i = 0; i < _targets.Count; i++) _targets[i]?.SetContents(kinds, fill01);
        }

        private void PushSpoilValue(float spoil)
        {
            for (int i = 0; i < _targets.Count; i++) _targets[i]?.SetSpoil(spoil);
        }

        /// <summary>The art table, from whichever target carries one. They are meant to share a single
        /// asset; the first non-null wins, and a disagreement would be a wiring mistake rather than a
        /// case to resolve here.</summary>
        private CatchItemLibrary ResolveLibrary()
        {
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i]?.Library != null) return _targets[i].Library;
            return null;
        }

        /// <summary>
        /// Find the picture(s) on this object. Re-run on every refresh because a presenter can be added
        /// after this component (a builder that spawns the pail, then dresses it) — and because
        /// <c>Awake</c> does not run at all in EditMode, where the tests live.
        ///
        /// <para>The list is REBUILT rather than appended to, so a target destroyed under us leaves no
        /// corpse to push into. <see cref="GetComponents{T}"/> with a list allocates nothing once the
        /// list has grown.</para>
        /// </summary>
        private void ResolveTargets() => GetComponents(_targets);
    }
}
