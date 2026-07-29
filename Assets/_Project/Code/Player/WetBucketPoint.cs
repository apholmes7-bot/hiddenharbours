using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// THE WET BUCKET (M1 §7.3) — keeping shellfish ALIVE: time, free, but species-limited. A
    /// seawater spot at the shore; press the key with your pail and the SHELLFISH in it are wetted
    /// (<see cref="StorageMode.Live"/> — arrested at the M1 policy). Press again to tip the water
    /// out (back to ambient). Finfish are never affected — a wet bucket keeps a clam alive, it does
    /// nothing for a mackerel; that species limit is exactly what makes ice worth buying (§7.3's
    /// table of what each cold buys).
    ///
    /// <para>Mode changes settle first (<see cref="Freshness.WithMode"/>), so the walk to the water
    /// is banked, never undone. Proximity + on-foot gating is the stall pattern
    /// (<see cref="StallReach"/>); New Input System only; <see cref="DevNotice"/> narrates.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WetBucketPoint : MonoBehaviour
    {
        [Tooltip("Interact key (proximity-disjoint from the freezer's — the WorldInteractor precedent).")]
        [SerializeField] private Key _interactKey = Key.F;

        [Tooltip("On-foot + in-range gate (the DevSellInput stall pattern).")]
        [SerializeField] private StallReach _reach = new StallReach();

        private ClamBucket _bucket;

        private void OnEnable() => _reach.Enable();
        private void OnDisable() => _reach.Disable();

        private void Update()
        {
            var kb = Keyboard.current;   // New Input System only
            if (kb == null || !kb[_interactKey].wasPressedThisFrame) return;
            if (!_reach.CanInteract(transform.position)) return;
            Toggle();
        }

        /// <summary>Wet the shellfish (Live) or tip the water out (Ambient) — whichever applies.
        /// Public so tests drive it without input.</summary>
        public void Toggle()
        {
            _bucket ??= FindAnyObjectByType<ClamBucket>();
            if (_bucket == null || _bucket.UsedUnits == 0)
            {
                EventBus.Publish(new DevNotice("Nothing in the bucket to keep."));
                return;
            }

            // Wet if ANY shellfish is still dry (ambient); otherwise tip the water out.
            bool anyDryShellfish = false;
            var items = _bucket.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].Category == FishCategory.Shellfish &&
                    items[i].Freshness.Mode == StorageMode.Ambient) { anyDryShellfish = true; break; }

            SpoilContext spoil = SpoilContext.Capture();
            StorageMode target = anyDryShellfish ? StorageMode.Live : StorageMode.Ambient;

            var next = new List<CatchItem>(items.Count);
            int changed = 0;
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                bool eligible = it.Category == FishCategory.Shellfish && it.Freshness.Mode != target
                                // only the wet-bucket's OWN modes flip back; frozen/iced aren't ours to touch
                                && (target == StorageMode.Live
                                    ? it.Freshness.Mode == StorageMode.Ambient
                                    : it.Freshness.Mode == StorageMode.Live);
                if (eligible)
                {
                    next.Add(it.WithFreshness(Freshness.WithMode(it.Freshness, spoil.NowGameSeconds,
                                                                 it.SpoilPerDay, spoil.SecondsPerDay,
                                                                 spoil.Policy, target)));
                    changed++;
                }
                else next.Add(it);
            }

            if (changed == 0)
            {
                EventBus.Publish(new DevNotice("Only shellfish keep in a wet bucket."));
                return;
            }

            _bucket.Clear();
            for (int i = 0; i < next.Count; i++) _bucket.TryAdd(next[i]);

            EventBus.Publish(new DevNotice(target == StorageMode.Live
                ? $"You wet the bucket — {changed} shellfish will keep alive."
                : $"You tip the seawater out — {changed} shellfish back on the clock."));
        }
    }
}
