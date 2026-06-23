using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The on-foot player's single <b>clam digger</b> (St Peters opening) — it owns the Interact key for
    /// digging so a press is always exactly ONE clam from the hole you're standing on. It sits on the player
    /// and, on Interact, picks the <em>nearest in-range, exposed</em> <see cref="ClamDig"/> hole and digs only
    /// that one.
    ///
    /// <para><b>Why a digger, not per-hole input.</b> Each <see cref="ClamDig"/> used to listen for E itself,
    /// so a press fired EVERY exposed hole on the bar at once — you dug clams from anywhere and a press or two
    /// filled the 20-clam bucket. Centralising the key here makes the proximity gate real (you must be at a
    /// hole) and the yield exactly one clam per press (the nearest hole), which is the cozy hand-gather feel
    /// the design asks for. Candidate holes are gathered on a press (not per frame) with a single
    /// <c>FindObjectsByType</c> scan — fine for a one-shot keypress, and it needs no enable-time registry
    /// bookkeeping (which doesn't run in EditMode tests anyway). No per-frame allocation (rule 7).</para>
    ///
    /// <para><b>Seam discipline.</b> Holds only a <see cref="Transform"/> for the player position and reads the
    /// holes through <see cref="ClamDig"/> in this same lane — no Player/World concrete classes referenced. A
    /// modal dialogue owns Interact while up (the Core <see cref="InteractionGate"/>), so the digger stands
    /// down under it. Input is dev-keyed (E) for the greybox; an InputService/interaction prompt replaces it
    /// later (ui-ux). Real-time only — it touches no sim state, so determinism is unaffected (rule 5).</para>
    /// </summary>
    public class ClamDigger : MonoBehaviour
    {
        [Tooltip("The on-foot player whose position decides which hole is in reach. Defaults to this " +
                 "object's transform when unset (the digger sits on the player).")]
        [SerializeField] private Transform _player;

        private Transform Player => _player != null ? _player : transform;

        private void Update()
        {
            // A modal dialogue (world-content) owns the shared Interact key while it's up — don't dig under it.
            if (InteractionGate.IsBlocked) return;
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) TryDigNearest();
        }

        /// <summary>
        /// Dig the nearest in-range, exposed hole — one clam, or nothing if you're not standing at a diggable
        /// hole. Public + returns true iff a clam was dug so EditMode tests can drive it without input.
        /// </summary>
        public bool TryDigNearest()
        {
            ClamDig best = NearestDiggable(Player.position);
            return best != null && best.TryDig();
        }

        /// <summary>
        /// The nearest hole to <paramref name="playerPos"/> that is BOTH within its reach radius and exposed
        /// right now — the one a press would dig. Null when no hole qualifies (you're not at a bared hole).
        /// Scans the active <see cref="ClamDig"/> holes once (a press, not a frame).
        /// </summary>
        public static ClamDig NearestDiggable(Vector2 playerPos)
        {
            var holes = Object.FindObjectsByType<ClamDig>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            ClamDig best = null;
            float bestSqr = float.MaxValue;
            foreach (var dig in holes)
            {
                if (dig == null || !dig.WithinReach(playerPos) || !dig.IsExposedNow()) continue;
                float sqr = ((Vector2)dig.SpotPos - playerPos).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = dig; }
            }
            return best;
        }

        /// <summary>Wire the digger in one call (tests / editor).</summary>
        public void Configure(Transform player) => _player = player;
    }
}
