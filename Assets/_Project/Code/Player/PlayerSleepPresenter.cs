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
    /// <para><b>⭐ WHICH WAY ROUND THE SLEEPER LIES IS THE BED'S TO SAY.</b> The heading comes from the
    /// beat's own <see cref="PillowAnchor"/> — the end the bed declares its pillow is on, turned into a
    /// ground bearing by the room's own facing — so the fisher is laid head-to-pillow on every bed at
    /// every facing. It used to be one serialized number, which is correct for a bed in an UNTURNED room
    /// and wrong for the same bed the moment the builder faced its building at anything else; the
    /// resulting picture is a person asleep with their head on the footboard, and it reads as bad art
    /// rather than as a bug. <see cref="_sleepHeadingDegrees"/> survives as the fallback for a bed that
    /// declares nothing, which content validation does not allow to ship.</para>
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

        [Tooltip("FALLBACK heading, used only by a bed that declares no pillow side. The real heading " +
                 "is the bed's own (SleepBeatRequested.Pillow) — head-to-pillow, resolved against the " +
                 "room's facing. 180 is what an undeclared bed used to get unconditionally: correct for " +
                 "a bed in an unturned room, and wrong for every other facing.")]
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

            StartCoroutine(Beat(e.BedPosition, HeadingFor(e.Pillow)));
        }

        /// <summary>
        /// The heading this beat plays at: the bed's own head-to-pillow bearing when it declared one, and
        /// the serialized fallback when it did not.
        ///
        /// <para>Pure and public so the whole rule is assertable without a coroutine, a clip or a scene —
        /// which matters, because the wrong answer here is a picture rather than an exception and nothing
        /// downstream can notice it.</para>
        /// </summary>
        public float HeadingFor(in PillowAnchor pillow) =>
            pillow.TryHeadingDegrees(out float heading) ? heading : _sleepHeadingDegrees;

        private IEnumerator Beat(Vector2 bedPosition, float headingDegrees)
        {
            _restorePosition = transform.position;

            if (!_clipPlayer.Play(CharacterClip.Sleep, headingDegrees, holdOnFinish: true))
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
