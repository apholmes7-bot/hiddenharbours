using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The lamp on a lit navigation mark, showing her character.</b> Drop it on a nav buoy beside
    /// whatever answers <see cref="INavLightSource"/> and the mark flashes her published rhythm off
    /// the master clock — <c>Fl G 4s</c> on a port hand, <c>Q(6) + LFl 15s</c> on a south cardinal.
    ///
    /// <para><b>It owns the LAMP; it does not own the RHYTHM.</b> When the light burns is
    /// <see cref="NavLightCharacter.IsOn"/>, a pure function of <see cref="IGameClock.TotalSeconds"/>
    /// and this mark's own phase, living in Core where the data and the drawing can both reach it
    /// (rule 4). This component is the wiring: mint one pooled <see cref="SceneLight"/> at the
    /// lantern, colour it, and switch it. There is no accumulator here and nothing is saved (rule 5),
    /// so a reload cannot land a mark mid-flash and two marks cannot drift apart.</para>
    ///
    /// <para><b>⭐ The flash is an ENABLE, not an intensity ramp — and that is what makes it a
    /// character rather than a stutter.</b> <see cref="SceneLight"/> pushes its material block on a
    /// THROTTLED tick (20 Hz shipped), so driving <c>Intensity</c> between 0 and full would quantise
    /// every edge of a half-second flash to the nearest 50 ms — a ±10% wobble on a quick flash, which
    /// is exactly the thing a skipper counts. Toggling the component's own <c>enabled</c> instead
    /// lands the edge on the frame the character asks for, because <c>SceneLight.OnEnable</c> ticks
    /// immediately and <c>OnDisable</c> drops the quad the same frame. It is also strictly cheaper:
    /// a dark mark costs NO quad at all, so twenty-three marks at an eighth duty cost about three.
    /// The state is toggled only when it CHANGES — twice a period, not sixty times a second.</para>
    ///
    /// <para><b>An unlit mark costs nothing.</b> A mooring buoy, or anything whose def carries no
    /// character, gets no <see cref="SceneLight"/>, no quad, no shadow pair and no allocation — one
    /// branch in <see cref="Update"/> and that is all. Absence is data, exactly as it is for a hull
    /// with no lamps.</para>
    ///
    /// <para><b>Visual only.</b> It drives no simulation, feeds no water light bridge (that has four
    /// slots and they belong to the boats' searchlights — a harbour full of flashing marks would
    /// evict the beam the player is steering by), and saves nothing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavLight : MonoBehaviour
    {
        [Tooltip("Master switch for this mark's lamp. Off = an unlit mark, whatever her def says.")]
        [SerializeField] private bool _lampOn = true;

        private INavLightSource _source;
        private SceneLight _light;
        private NavLightCharacter _character;
        private float _phaseSeconds;
        private IGameClock _clock;
        private bool _installed;
        private bool _lit;
        private bool _burning;

        /// <summary>The pooled lamp, or null for an unlit mark. Fixtures read it.</summary>
        public SceneLight Lamp => _light;

        /// <summary>The character this mark is showing. Default when she is unlit.</summary>
        public NavLightCharacter Character => _character;

        /// <summary>This mark's offset into her period, seconds — her own, from her chart id.</summary>
        public float PhaseSeconds => _phaseSeconds;

        /// <summary>Is the lamp burning this instant? What the picture actually shows.</summary>
        public bool IsBurning => _burning;

        /// <summary>Does this mark carry a light at all?</summary>
        public bool IsLit => _lit;

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            // Pooled, not destroyed — SceneLight keeps its quad across an off/on, which is the whole
            // point of switching rather than rebuilding on every flash.
            if (_light != null) _light.enabled = false;
            _burning = false;
        }

        private void OnDestroy() => DestroyLamp();

        /// <summary>
        /// Re-read the mark and (re)build her lamp. Idempotent, and safe to call after a mark has
        /// been retargeted at a different def — which is how a placer or a fixture that configures
        /// the buoy AFTER adding this component gets a light at all.
        /// </summary>
        public void Rebuild()
        {
            _source = GetComponent<INavLightSource>();
            _installed = true;

            NavLightCharacter character = _source != null ? _source.Character : default;
            _lit = _lampOn && character.IsLit;
            _character = character;

            if (!_lit)
            {
                DestroyLamp();
                _burning = false;
                return;
            }

            // Her own offset into the period. How she came by it — a placement plan that shared the
            // period out, or a hash of her id — is her business, not the lamp's.
            _phaseSeconds = _source.PhaseSeconds;

            Transform mount = _source.LanternMount != null ? _source.LanternMount : transform;
            EnsureLamp(mount);
            NavLightPresets.Apply(_light, character.Colour, _source.LanternHeightMetres);

            // Start DARK and let Update decide on its own terms. Lighting her here would put every
            // mark in a region on at the instant the scene loads, which is one frame of the exact
            // picture this whole component exists to avoid.
            _burning = false;
            _light.enabled = false;
        }

        private void EnsureLamp(Transform mount)
        {
            if (_light != null)
            {
                if (_light.transform.parent != mount)
                    _light.transform.SetParent(mount, worldPositionStays: false);
            }
            else
            {
                var go = new GameObject("NavLantern") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(mount, worldPositionStays: false);
                _light = go.AddComponent<SceneLight>();
            }

            // ⚠️ The lantern rides the BOB (it is parented to the visual the wave field offsets), so
            // this is a fixed LOCAL lift off the mark's own waterline pivot, not a world height. A
            // world height would hold the light still while the can heaved underneath it.
            _light.transform.localPosition = new Vector3(0f, _source.LanternHeightMetres, 0f);
            _light.enabled = false;
        }

        private void DestroyLamp()
        {
            if (_light == null) return;
            if (Application.isPlaying) Destroy(_light.gameObject);
            else DestroyImmediate(_light.gameObject);
            _light = null;
        }

        private void Update()
        {
            if (!_installed) Rebuild();
            if (!_lit || _light == null) return;

            bool on = _character.IsOn(ClockSeconds(), _phaseSeconds);
            if (on == _burning) return;   // twice a period, not sixty times a second

            _burning = on;
            _light.enabled = on;
        }

        /// <summary>
        /// The master clock, or the frame clock when none is running.
        ///
        /// <para>The fallback is for the bare art scene and the editor preview — a mark that stood
        /// dead still there would read as a broken light rather than an unbuilt world, exactly as
        /// <see cref="SceneLight"/>'s own gate fallback shows a lamp when no day/night cycle is
        /// running. In the game the clock is always there and it is the only thing read; the light
        /// drives no simulation, so the fallback cannot make anything non-deterministic (rule 5).</para>
        /// </summary>
        private double ClockSeconds()
        {
            if (_clock == null) _clock = GameServices.Clock;   // may register after this wakes
            return _clock?.TotalSeconds ?? Time.timeAsDouble;
        }
    }
}
