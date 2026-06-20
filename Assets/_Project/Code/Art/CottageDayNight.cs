using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Swaps the cottage between its unlit (day) and lit (night) sprite based on the shared clock
    /// (<c>GameServices.Clock.HourOfDay</c>). Reads time through the Core contract only — no cross-module
    /// references. Attach to the cottage's <see cref="SpriteRenderer"/>; world-content places it in the
    /// scene and assigns the day/night sprites (Sprites/Buildings/Cottage.png &amp; CottageNight.png).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CottageDayNight : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite _daySprite;
        [SerializeField] private Sprite _nightSprite;

        [Tooltip("Hour the windows light up (evening).")]
        [SerializeField, Range(0f, 24f)] private float _duskHour = 19f;
        [Tooltip("Hour the windows go dark (morning).")]
        [SerializeField, Range(0f, 24f)] private float _dawnHour = 6f;

        private void Reset() => _renderer = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            if (_renderer == null || GameServices.Clock == null) return;

            Sprite want = IsNight(GameServices.Clock.HourOfDay, _dawnHour, _duskHour) ? _nightSprite : _daySprite;
            if (want != null && _renderer.sprite != want) _renderer.sprite = want;
        }

        /// <summary>
        /// Pure (testable): is it night at <paramref name="hour"/> (0..24)? Night runs from dusk through
        /// dawn, wrapping midnight (assumes <paramref name="dawnHour"/> &lt; <paramref name="duskHour"/>).
        /// </summary>
        public static bool IsNight(float hour, float dawnHour, float duskHour)
            => hour < dawnHour || hour >= duskHour;
    }
}
