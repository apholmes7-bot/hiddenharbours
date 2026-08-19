using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// THE WET BUCKET (M1 §7.3) — keeping shellfish ALIVE: time, free, but species-limited. A
    /// seawater spot at the shore; interact with your pail and the SHELLFISH in it are wetted
    /// (<see cref="StorageMode.Live"/> — arrested at the M1 policy). Interact again to tip the water
    /// out (back to ambient). Finfish are never affected — a wet bucket keeps a clam alive, it does
    /// nothing for a mackerel; that species limit is exactly what makes ice worth buying (§7.3's
    /// table of what each cold buys).
    ///
    /// <para>Mode changes settle first (<see cref="Freshness.WithMode"/>), so the walk to the water
    /// is banked, never undone. <see cref="DevNotice"/> narrates.</para>
    ///
    /// <para><b>The first citizen of the one interact verb (M2-39).</b> This used to own a private
    /// <c>Update()</c> reading <c>F</c>, with a comment conceding the key was shared with the freezer and
    /// only safe because "the ranges don't overlap" — an invariant asserted in prose and checked by
    /// nobody. It now registers as an <see cref="IInteractable"/> and the press arrives through
    /// <see cref="InteractVerb"/> instead: no keyboard read here, no key of its own, and the
    /// don't-overlap question answered by <see cref="InteractResolver"/> rather than by hand.</para>
    ///
    /// <para><b>What that changed, exactly.</b> The gate is the same rule it always was — ON FOOT and
    /// within 4 m, inclusive — because <see cref="InteractContext.OnFoot"/> is what <c>StallReach</c>'s
    /// on-foot test was, and <see cref="ReachMeters"/> is what its range was. Facing is deliberately NOT
    /// required (<see cref="_requiresFacing"/> defaults off), so a spot you operate by standing at it stays
    /// operable from any angle, as before. The one real difference is the binding: <c>F</c> is gone and the
    /// verb rides the interact key, which the switcher answers first — so with a boardable boat inside its
    /// board reach, that press boards instead. Step a metre off the boat and the spot is yours again.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WetBucketPoint : MonoBehaviour, IInteractable
    {
        [Tooltip("Stable id — the art highlight matches on it, and it is the resolver's last tie-break. " +
                 "Must be unique among live interactables (a second seawater spot needs its own).")]
        [SerializeField] private string _id = "fixture.st_peters.wet_bucket";

        [Tooltip("How close (m) the on-foot player must stand to work the spot. Was StallGate's 4 m " +
                 "default and stays it — a forgiving step-up distance (P5 cozy).")]
        [SerializeField, Min(0f)] private float _reachMeters = StallGate.DefaultRange;

        [Tooltip("Require the player to be FACING the spot. Off (the pre-verb behaviour): being in reach " +
                 "is enough, which is right for something you operate by standing at it.")]
        [SerializeField] private bool _requiresFacing;

        private ClamBucket _bucket;

        // ---- the interact seam (M2-39) -------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => _requiresFacing;

        /// <summary>Always available. An empty pail does NOT make the spot un-interactable: pressing with
        /// nothing to keep answers "Nothing in the bucket to keep." and that cozy refusal (P5) is exactly
        /// what the pre-verb F-key path did. See <see cref="IInteractable.IsAvailable"/>.</summary>
        public bool IsAvailable => true;

        /// <summary>
        /// The spot's own name for itself, and NOT a reading of what the bucket holds.
        ///
        /// <para><b>Why it does not say "Wet the bucket" / "Tip it out" the way <see cref="Toggle"/>
        /// branches.</b> Working out which way the toggle will go means walking every item in the bucket,
        /// and <see cref="IInteractable.VerbLabel"/> is read whenever the offer changes — a per-item scan
        /// behind a label is a rule-7 trap waiting for a full bucket. The press answers out loud either
        /// way, which is where that detail belongs.</para>
        /// </summary>
        public string VerbLabel => "Use the seawater";

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => Toggle();

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

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
