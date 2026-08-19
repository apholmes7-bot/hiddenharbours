using System.Collections;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>Shows the fisher asleep when they turn in.</b> On <see cref="SleepBeatRequested"/> it lays the
    /// player on the mattress, plays the rig-6.5 <c>sleep</c> clip for a held beat, and puts everything
    /// back.
    ///
    /// <para><b>It creates the beat; it did not exist before.</b> #580's rest was instantaneous — the bed
    /// published, the responder wrote, a notice appeared, and the player never stopped standing. There
    /// was no "rest moment" for a sleeping pose to occupy, so this makes one. ⚠️ <b>It changes nothing
    /// about the save.</b> <see cref="RestSaveRequested"/> is published and fulfilled exactly as before,
    /// on its own signal, before this beat starts and regardless of whether this component exists, has
    /// art, or is even in the scene. If the beat is interrupted the day is still kept.</para>
    ///
    /// <para><b>Why the player is MOVED rather than a second sleeper drawn.</b> Lying on a bed IS being
    /// at the bed, and moving the one renderer keeps every other system that dresses it — the wardrobe
    /// swap, the submersion material, the sorting — working with no second wiring. A duplicate sleeper
    /// object would have to be re-dressed from the same def and would drift from the player the first
    /// time either changed. The position is restored exactly, from a value captured here, and the WAKE
    /// spot the save recorded is untouched by any of it: that was read from the player before this ran.</para>
    ///
    /// <para><b>Degrades whole.</b> No clip player, no sleep art, or a beat already running →
    /// <see cref="CharacterClipPlayer.Play"/> answers false (or is never reached) and nothing happens at
    /// all: no move, no hold, no restore. The player keeps standing and the rest reads exactly as it did
    /// in #580, which is a complete and shipped behaviour.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSleepPresenter : MonoBehaviour
    {
        [Header("Wiring (auto-resolved from this GameObject when left empty)")]
        [Tooltip("The shared clip seam. It claims the renderer from the iso skin itself.")]
        [SerializeField] private CharacterClipPlayer _clipPlayer;

        [Tooltip("The walk controller, held still for the beat so a leaned-on key cannot walk the " +
                 "sleeper out of bed. Left alone entirely if it is already disabled — the ControlSwitcher " +
                 "owns when it may run, and this must not hand it back on.")]
        [SerializeField] private PlayerWalkController _walk;

        [Header("Feel")]
        [Tooltip("How long the sleeping pose holds, in seconds of real time. The clip itself loops at " +
                 "640 ms a frame (the rig's slowest — it is breathing), so this is how many breaths the " +
                 "player watches, not the clip's own length.")]
        [Min(0f)] [SerializeField] private float _beatSeconds = 1.6f;

        [Tooltip("The heading the sleeper is drawn at. The rig bakes sleep at all eight facings; a bed " +
                 "has one, and the clip is placed at the BED, so this is the bed's own axis rather than " +
                 "whichever way the player happened to walk in.")]
        [Range(0f, 360f)] [SerializeField] private float _sleepHeadingDegrees = 180f;

        private bool _running;
        private Vector3 _restorePosition;
        private bool _walkWasEnabled;

        /// <summary>True while the sleeping pose is on screen. Public so a test can watch the beat begin
        /// and end without a screen grab.</summary>
        public bool IsSleeping => _running;

        private void Awake()
        {
            if (_clipPlayer == null) _clipPlayer = GetComponent<CharacterClipPlayer>();
            if (_walk == null) _walk = GetComponent<PlayerWalkController>();
        }

        private void OnEnable() => EventBus.Subscribe<SleepBeatRequested>(OnSleepRequested);

        private void OnDisable()
        {
            EventBus.Unsubscribe<SleepBeatRequested>(OnSleepRequested);
            // Cut short rather than left mid-beat: a disabled presenter must not leave the player lying
            // in the mattress with their controller off.
            if (_running) EndBeat(completed: false);
        }

        private void OnSleepRequested(SleepBeatRequested e)
        {
            if (_running) return;                       // one beat at a time; a second request is ignored
            if (_clipPlayer == null) return;
            if (!_clipPlayer.CanPlay(CharacterClip.Sleep)) return;   // no art → the rest stays as it was

            StartCoroutine(Beat(e.BedPosition));
        }

        private IEnumerator Beat(Vector2 bedPosition)
        {
            _restorePosition = transform.position;

            if (!_clipPlayer.Play(CharacterClip.Sleep, _sleepHeadingDegrees, holdOnFinish: true))
                yield break;                            // refused: change nothing, not even the position

            _running = true;

            // Held still for the beat. Only re-enabled below if WE disabled it — the ControlSwitcher owns
            // whether the walk controller may run at all (it is off on the deck and at the helm), and
            // switching it back on here would hand the player controls the switcher had taken away.
            _walkWasEnabled = _walk != null && _walk.enabled;
            if (_walkWasEnabled) _walk.enabled = false;

            // Keep z: the iso sort band lives there and a 2D position assignment would flatten it.
            transform.position = new Vector3(bedPosition.x, bedPosition.y, _restorePosition.z);

            float elapsed = 0f;
            while (elapsed < _beatSeconds && _running)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_running) EndBeat(completed: true);
        }

        private void EndBeat(bool completed)
        {
            _running = false;

            transform.position = _restorePosition;
            if (_walkWasEnabled && _walk != null) _walk.enabled = true;
            _walkWasEnabled = false;

            // Only our own clip. Something else may have claimed the renderer mid-beat, and stopping its
            // clip would be a visible glitch.
            if (_clipPlayer != null && _clipPlayer.Clip == CharacterClip.Sleep) _clipPlayer.Stop();

            EventBus.Publish(new SleepBeatEnded(completed));
        }
    }
}
