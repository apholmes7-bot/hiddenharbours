using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The lantern of a light station, turning.</b> It owns no light of its own: the beams and the flash are
    /// lamps already standing in the scene, and this turns the one and switches the other. Where the lens points
    /// is <see cref="LightSweep"/>, a pure function of <see cref="IGameClock.FlowSeconds"/> and the station's
    /// <see cref="LightStationDef"/>; there is no accumulator and nothing is saved (rule 5). The lamps gate
    /// themselves on the dark, so by day this only turns two transforms nobody can see.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightStationLamp : MonoBehaviour
    {
        [Tooltip("The station's light list entry: character, turn, beams.")]
        [SerializeField] private LightStationDef _def;

        [Tooltip("The beam lamps, one per beam. Each throws along its own local up.")]
        [SerializeField] private Transform[] _beams = new Transform[0];

        [Tooltip("The lamp shown only while a beam crosses the viewer: the flash a skipper counts.")]
        [SerializeField] private Behaviour _flash;

        private IGameClock _clock;
        private bool _flashing;
        private bool _primed;

        /// <summary>The station this lantern belongs to.</summary>
        public LightStationDef Def => _def;

        /// <summary>The first beam's ground bearing this frame, degrees.</summary>
        public float BearingDegrees { get; private set; }

        /// <summary>Is the flash showing this instant?</summary>
        public bool IsFlashing => _flashing;

        /// <summary>The beam transforms, for fixtures.</summary>
        public Transform[] BeamTransforms => _beams;

        private void OnEnable()
        {
            _primed = false;
            Turn();
        }

        private void Update() => Turn();

        /// <summary>Point the beams and set the flash for the clock's second. Safe to call from a fixture.</summary>
        public void Turn()
        {
            if (_def == null) return;
            BearingDegrees = LightSweep.BearingDegrees(ClockSeconds(), _def.TurnSeconds, _def.PhaseSeconds);

            int count = _beams != null ? _beams.Length : 0;
            float spacing = count > 0 ? 360f / count : 0f;
            for (int i = 0; i < count; i++)
            {
                Transform beam = _beams[i];
                if (beam == null) continue;
                float drawn = LightSweep.DrawnDegrees(BearingDegrees + spacing * i, IsoGround.GroundDepthScale);
                // A lamp throws along its local up, which is 90 degrees round from screen right.
                beam.localRotation = Quaternion.Euler(0f, 0f, drawn - 90f);
            }

            bool flashing = LightSweep.IsFlashing(BearingDegrees, Mathf.Max(1, count), _def.FlashHalfAngleDegrees);
            if (_primed && flashing == _flashing) return;   // twice a flash, not sixty times a second
            _primed = true;
            _flashing = flashing;
            if (_flash != null) _flash.enabled = flashing;
        }

        // The master clock, or the frame clock in a bare scene with none (the light drives no simulation).
        private double ClockSeconds()
        {
            if (_clock == null) _clock = GameServices.Clock;
            return _clock?.FlowSeconds ?? Time.timeAsDouble;
        }
    }
}
