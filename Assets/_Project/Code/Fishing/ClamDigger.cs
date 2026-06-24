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
    ///
    /// <para><b>The player-position beacon.</b> The digger is the one Fishing-lane component that holds the
    /// on-foot player, so it publishes that position to a static <see cref="TryGetPlayerPosition"/> beacon each
    /// frame. The per-hole <see cref="ClamHoleVisual"/> reads it to run the "skittish clam" proximity-escape
    /// timer WITHOUT referencing the Player module — the same in-lane indirection the dig itself uses. Cosmetic,
    /// real-time only: the beacon is never saved and feeds no sim path (rule 5).</para>
    /// </summary>
    public class ClamDigger : MonoBehaviour
    {
        [Tooltip("The on-foot player whose position decides which hole is in reach. Defaults to this " +
                 "object's transform when unset (the digger sits on the player).")]
        [SerializeField] private Transform _player;

        private Transform Player => _player != null ? _player : transform;

        // The latest on-foot player position, published for the per-hole proximity-escape timer. Static so a
        // hole reads it without a Player reference; real-time cosmetic only (never saved, no sim path).
        private static bool s_hasPlayer;
        private static Vector2 s_playerPos;

        /// <summary>The most recent on-foot player position seen by a live digger this frame. Returns false
        /// (no position) before any digger has ticked or after they're all gone — callers must null-check, so
        /// a missing beacon never throws. Cosmetic real-time only (the proximity-escape cue), never sim.</summary>
        public static bool TryGetPlayerPosition(out Vector2 pos)
        {
            pos = s_playerPos;
            return s_hasPlayer;
        }

        private void OnDisable()
        {
            // Drop the beacon when no digger is live (scene teardown / tests) so a stale position can't fire an
            // escape against a phantom player. The next live digger's Update re-publishes it.
            s_hasPlayer = false;
        }

        /// <summary>Publish this digger's player position to the static escape-timer beacon. Called every
        /// frame by <see cref="Update"/>; public so EditMode tests can prime the beacon without the game loop.
        /// Cosmetic real-time only (never saved, no sim path).</summary>
        public void PublishPlayerPosition()
        {
            s_playerPos = Player.position;
            s_hasPlayer = true;
        }

        /// <summary>Clear the static escape-timer beacon (test teardown), so a stale position can't fire an
        /// escape across tests.</summary>
        public static void ClearPlayerPosition() => s_hasPlayer = false;

        private void Update()
        {
            // Publish the player position for the per-hole proximity-escape timer (cosmetic, real-time).
            PublishPlayerPosition();

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
