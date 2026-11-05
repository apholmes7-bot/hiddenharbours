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

        // The shared sky owns dawn and dusk. Legacy per-cottage hours are no longer deserialized.

        private void Reset() => _renderer = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            if (_renderer == null || GameServices.Clock == null) return;

            Sprite want = WatchFaceState.NightAt(GameServices.Clock.HourOfDay) ? _nightSprite : _daySprite;
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
