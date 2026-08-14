// No UnityEngine.InputSystem and no HiddenHarbours.Core here any more — this component reads no key and
// consults no gate. Both went with the E poll when the dig moved onto the interact verb; if either
// comes back, so has the double-reader ambiguity the move removed (see the class remarks).
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The on-foot player's <b>clam-hole beacon</b> (St Peters opening).
    ///
    /// <para><b>⚠️ THIS COMPONENT NO LONGER OWNS A KEY, AND THAT IS THE POINT (2026-08-13, the tools-in-hand
    /// ruling).</b> It used to read E directly and dig the nearest in-range exposed hole. That was correct
    /// when nothing else on foot answered E — but the moment a TOOL can be in your hands, a second
    /// independent reader of the same key is an ambiguity nobody arbitrates: standing at a hole with the
    /// shovel held, one reader wants to dig and the carry verb wants to put the shovel down, and which one
    /// wins is decided by <c>Update</c> order. So the dig moved onto the one interact verb, where a single
    /// pure function decides. Each <see cref="ClamDig"/> is now an <c>IInteractable</c> in its own right;
    /// the resolver picks the nearest qualifying hole, which is precisely what
    /// <see cref="NearestDiggable"/> did by hand — with the tie-break made deterministic (by id) instead
    /// of by scan order. <b>Do not give this component a key again.</b></para>
    ///
    /// <para><b>What it still does, and why it still exists.</b> It is the one Fishing-lane component that
    /// holds the on-foot player, so it publishes that position to the static
    /// <see cref="TryGetPlayerPosition"/> beacon each frame. The per-hole <see cref="ClamHoleVisual"/>
    /// reads it to run the "skittish clam" proximity-escape timer WITHOUT referencing the Player module.
    /// Cosmetic, real-time only: never saved, feeds no sim path (rule 5).</para>
    ///
    /// <para><b><see cref="TryDigNearest"/> and <see cref="NearestDiggable"/> are kept</b> — they are no
    /// longer on the input path, but they are the honest expression of "which hole would a press dig", and
    /// a dozen EditMode tests drive the dig through them without standing up a resolver. They are now a
    /// TEST/tooling entry point, not the runtime one.</para>
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
            // ⚠️ THIS IS ALL THAT IS LEFT, and the removal is the point — see the class remarks.
            PublishPlayerPosition();
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
            var holes = Object.FindObjectsByType<ClamDig>(FindObjectsInactive.Exclude);
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
