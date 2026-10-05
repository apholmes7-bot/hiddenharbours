using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>THE HARBOUR VANE POINTS INTO THE WIND THE GAME IS BLOWING.</b> A set piece whose
    /// <see cref="SetPieceDef.Animated"/> part turns with <c>windFromDeg</c> (the harbour vane's cod) shows
    /// the nearest of its baked headings to the wind the sim blows now: it reads
    /// <see cref="IEnvironmentService.Sample"/>'s <see cref="EnvironmentSample.WindVector"/>, which points
    /// DOWNWIND, so the vane points up it, into the wind's way in.
    ///
    /// <para><b>It reads the sim and adds nothing.</b> No randomness and no state beyond the heading it
    /// shows: the same wind shows the same heading, on every machine. Before the boot (no environment
    /// service: the editor, a bare scene) it keeps the frame the scene holds, the one baked at the Def's
    /// <see cref="SetPieceAnimatedPart.BakedAtDeg"/>. In a calm there is no direction to read, so it holds
    /// the heading it last showed, as a real vane does.</para>
    ///
    /// <para><b>Cheap (rule 7).</b> The wind is slow, so it is read on a throttled tick, and the sprite is
    /// swapped only when the heading changes. No allocation per frame.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SetPieceWindVane : MonoBehaviour
    {
        /// <summary>The defaults a placed vane is written with (the key scenes step writes them).</summary>
        public const float DefaultRefreshHz = 2f;
        public const float DefaultCalmMetresPerSecond = 0.05f;

        [Tooltip("The set piece this vane is: its animated part's headings are the frames it shows.")]
        [SerializeField] private SetPieceDef _def;

        [Tooltip("How often (Hz) the wind is read. The wind is slow; a couple of times a second is plenty.")]
        [Min(0.1f)] [SerializeField] private float _refreshHz = DefaultRefreshHz;

        [Tooltip("Below this wind speed (m/s) there is no direction to point into, and the vane holds. The " +
                 "HUD's own calm (ApparentWindReadout.CalmSpeedMps); a test holds the two equal.")]
        [Min(0f)] [SerializeField] private float _calmMetresPerSecond = DefaultCalmMetresPerSecond;

        private SpriteRenderer _renderer;
        private float _timer;
        private int _shown = -1;

        /// <summary>The heading index the vane shows, or −1 while it keeps the scene's baked frame.</summary>
        public int ShownHeading => _shown;

        public SetPieceDef Def => _def;
        public float CalmMetresPerSecond => _calmMetresPerSecond;

        private void OnEnable()
        {
            _timer = 0f;
            Refresh();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 1f / Mathf.Max(_refreshHz, 0.1f);
            Refresh();
        }

        /// <summary>Read the wind now and show its heading. Nothing happens without an environment service.</summary>
        public void Refresh()
        {
            IEnvironmentService env = GameServices.Environment;
            if (env == null || _def == null || !_def.IsAnimated) return;

            int heading = HeadingFor(env.Sample().WindVector, _def.Animated, _calmMetresPerSecond, _shown);
            if (heading < 0 || heading == _shown) return;
            SetPieceFrame[] headings = _def.Animated.Headings;
            Sprite sprite = headings != null && heading < headings.Length ? headings[heading]?.Sprite : null;
            if (sprite == null) return;

            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = sprite;
            _shown = heading;
        }

        // ==== PURE mapping (testable headless) ===========================================================

        /// <summary>
        /// The compass bearing (0 N, clockwise) the wind blows FROM: the bearing of the vector against the
        /// downwind <see cref="EnvironmentSample.WindVector"/>.
        /// </summary>
        public static float WindFromDegrees(Vector2 windVector) => BoatKinematics.BearingDegrees(-windVector);

        /// <summary>
        /// The heading a vane shows for a wind: the nearest of the part's steps to the bearing the wind blows
        /// from, or <paramref name="held"/> when the wind is below <paramref name="calmMetresPerSecond"/>.
        /// </summary>
        public static int HeadingFor(Vector2 windVector, SetPieceAnimatedPart part, float calmMetresPerSecond, int held)
        {
            if (part == null || part.Steps <= 0) return held;
            if (windVector.magnitude < calmMetresPerSecond) return held;
            return part.HeadingFor(WindFromDegrees(windVector));
        }
    }
}
