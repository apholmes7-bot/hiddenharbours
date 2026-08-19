using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// What a fisher in the swim band is DOING this frame — the two attitudes the rig bakes for water.
    /// </summary>
    public enum SwimPose
    {
        /// <summary>Not in the swim band at all — the walk sprite owns the renderer, wading included.</summary>
        None = 0,
        /// <summary>Making way: the rig's prone <c>swim</c> clip.</summary>
        Swim = 1,
        /// <summary>Holding station: the rig's upright <c>tread</c> clip.</summary>
        Tread = 2,
    }

    /// <summary>
    /// The PURE maths behind the swimmer's animation — water state + speed → pose. Split out (the
    /// <c>PlayerSubmergeMath</c> / <c>PlayerHaulAnimMath</c> pattern) so the whole mapping is
    /// EditMode-testable headless. No engine state, no <c>Time</c>, no RNG.
    /// </summary>
    public static class PlayerSwimAnimMath
    {
        /// <summary>
        /// The pose for a water state and a speed. Only <see cref="OnFootWaterState.Swim"/> poses at all:
        /// <see cref="OnFootWaterState.Wade"/> keeps the ordinary walk — a fisher in the wade band has
        /// their feet on the bottom and walks, which is the whole distinction the three-band model draws.
        ///
        /// <para><b>Why this needs its own threshold and cannot reuse the gait's.</b> The obvious move is
        /// to call the character Idle-gaited "not moving" and be done. It is wrong here: the swim band
        /// runs at <b>0.18–0.60 m/s</b> (the rig's own note) while
        /// <see cref="CharacterVisualDef.WalkSpeedThreshold"/> is 0.35, so a genuinely swimming fisher
        /// would read as Idle for the lower half of their speed range and tread water while visibly
        /// crossing the bay. The threshold below sits under the whole swim range instead.</para>
        /// </summary>
        /// <param name="state">The band the player's feet are in.</param>
        /// <param name="speed">Current speed (m/s).</param>
        /// <param name="moveThreshold">Speed above which the swimmer counts as making way (m/s).</param>
        public static SwimPose PoseFor(OnFootWaterState state, float speed, float moveThreshold)
        {
            if (state != OnFootWaterState.Swim) return SwimPose.None;
            if (float.IsNaN(speed)) return SwimPose.Tread;
            return speed > Mathf.Max(0f, moveThreshold) ? SwimPose.Swim : SwimPose.Tread;
        }

        /// <summary>The clip a pose draws with — <see cref="CharacterClip.None"/> for
        /// <see cref="SwimPose.None"/>, so a caller can hand the renderer straight back.</summary>
        public static CharacterClip ClipFor(SwimPose pose) => pose switch
        {
            SwimPose.Swim => CharacterClip.Swim,
            SwimPose.Tread => CharacterClip.Tread,
            _ => CharacterClip.None,
        };
    }

    /// <summary>
    /// <b>Draws the fisher swimming.</b> Watches the on-foot water state and, in the swim band only,
    /// puts the rig-6.5 <c>swim</c> / <c>tread</c> clip on the renderer in place of the walk sprite.
    ///
    /// <para><b>Wading is deliberately untouched.</b> The three-band model (#572) is the authority on
    /// where the bands sit and how fast the player moves in each; this only listens and changes nothing
    /// about it. In the wade band the fisher keeps walking with <c>WadeSplashEmitter</c> doing its work,
    /// exactly as before — the only thing that changes anywhere is which sheet is drawn while
    /// <b>Swim</b> is the state.</para>
    ///
    /// <para><b>Why the EVENT and not the controller.</b> <c>OnFootWaterStateChanged</c> is the seam the
    /// wading model publishes for exactly this — <c>WadeSplashEmitter</c> already rides it — so
    /// listening costs no reference to the Player module's internals and puts the band's authority in one
    /// place. It fires on TRANSITIONS only, never per frame, so the current band is held here between
    /// edges the same way the splash emitter holds it.</para>
    ///
    /// <para><b>The heading is READ, never re-derived.</b> <see cref="IsoCharacterSprite"/> keeps
    /// resolving its ground bearing while suspended (its suspend check guards the DRAW, not the
    /// heading), so the live value is already the ADR 0034 answer with the held-heading rules applied.
    /// Computing a second bearing here would be the same quantity twice, and the two would disagree the
    /// first time either rule moved.</para>
    ///
    /// <para><b>Play once per pose, seek never.</b> <see cref="CharacterClipPlayer.Play"/> restarts the
    /// clip, so calling it every frame would freeze the stroke on frame 0. It is called only on a pose
    /// CHANGE; the heading is pushed every frame with <see cref="CharacterClipPlayer.SetHeading"/>.</para>
    ///
    /// <para><b>Degrades whole.</b> No clip player, no art, or a def whose swim/tread sheets are missing
    /// → <see cref="CharacterClipPlayer.Play"/> returns false and this component does nothing at all;
    /// the walk sprite keeps the renderer, which is always a legal picture. That is the same
    /// all-or-nothing gate every other clip family sits behind.</para>
    ///
    /// <para>The sheets are baked <b>DRY</b> — no water pixel. Where the sea cuts the body is
    /// <c>PlayerSubmergeVisual</c>'s job, from the mount table; this component only chooses the pose.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSwimAnimator : MonoBehaviour
    {
        [Header("Wiring (auto-resolved from this GameObject when left empty)")]
        [Tooltip("The iso skin — read for the live speed and ground bearing. Never written here.")]
        [SerializeField] private IsoCharacterSprite _isoSkin;

        [Tooltip("The shared clip seam. It claims the renderer from the iso skin itself, so this " +
                 "component never suspends anything by hand.")]
        [SerializeField] private CharacterClipPlayer _clipPlayer;

        [Header("Feel")]
        [Tooltip("Speed (m/s) above which a swimmer counts as MAKING WAY and shows the prone stroke; at " +
                 "or below it they tread. ⚠️ Must sit UNDER the swim band's own 0.18–0.60 m/s range — " +
                 "this is why the gait's WalkSpeedThreshold (0.35) cannot be reused: it falls inside " +
                 "that range and would tread-water a fisher who is visibly crossing the bay.")]
        [Min(0f)] [SerializeField] private float _swimMoveThreshold = 0.05f;

        private SwimPose _pose = SwimPose.None;
        private OnFootWaterState _state = OnFootWaterState.Dry;

        /// <summary>What is on the renderer this frame — <see cref="SwimPose.None"/> when the walk
        /// sprite has it. Public so a test (and the dev HUD) can read the pose without a screen grab.</summary>
        public SwimPose Pose => _pose;

        /// <summary>The band last heard on the bus. Held between edges: the signal fires on TRANSITIONS
        /// only, so there is nothing to poll and nothing arrives while the player stays in one band.</summary>
        public OnFootWaterState WaterState => _state;

        private void Awake()
        {
            if (_isoSkin == null) _isoSkin = GetComponentInChildren<IsoCharacterSprite>();
            if (_clipPlayer == null) _clipPlayer = GetComponent<CharacterClipPlayer>();
        }

        private void OnEnable() => EventBus.Subscribe<OnFootWaterStateChanged>(OnWaterStateChanged);

        private void OnDisable()
        {
            EventBus.Unsubscribe<OnFootWaterStateChanged>(OnWaterStateChanged);
            Stop();
        }

        private void OnWaterStateChanged(OnFootWaterStateChanged e) => _state = e.State;

        // LateUpdate, not Update: the controller has moved and the iso skin has resolved its heading and
        // smoothed its speed by now, so both reads are this frame's answers rather than last frame's.
        private void LateUpdate() => Tick();

        /// <summary>The whole behaviour, exposed so a PlayMode test can step it without waiting on the
        /// engine's frame loop (frames are not time).</summary>
        public void Tick()
        {
            if (_clipPlayer == null) { Stop(); return; }

            float speed = _isoSkin != null ? _isoSkin.Speed : 0f;
            SwimPose wanted = PlayerSwimAnimMath.PoseFor(_state, speed, _swimMoveThreshold);

            if (wanted == SwimPose.None) { Stop(); return; }

            float heading = _isoSkin != null ? _isoSkin.HeadingDegrees : 0f;

            if (wanted != _pose)
            {
                // Play RESTARTS the clip, so this must happen on the pose edge only. A refusal (no art)
                // leaves the renderer with whatever had it and clears our pose, so we retry next frame
                // rather than latching a pose we are not actually drawing.
                if (!_clipPlayer.Play(PlayerSwimAnimMath.ClipFor(wanted), heading))
                {
                    _pose = SwimPose.None;
                    return;
                }

                _pose = wanted;
                return;
            }

            // Same pose, still running: only the heading moves. A swimmer turns.
            _clipPlayer.SetHeading(heading);
        }

        private void Stop()
        {
            if (_pose == SwimPose.None) return;
            _pose = SwimPose.None;

            // Only stop a clip that is still OURS. Something else (a haul, a boarding vault) may have
            // taken the renderer in the meantime, and stopping its clip would be a visible glitch.
            if (_clipPlayer == null) return;
            if (_clipPlayer.Clip == CharacterClip.Swim || _clipPlayer.Clip == CharacterClip.Tread)
                _clipPlayer.Stop();
        }
    }
}
