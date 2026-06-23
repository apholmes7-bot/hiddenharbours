using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// OPTIONAL dev/greybox aid: fast-forwards the live clock so a playtester can watch the WHOLE tide
    /// swing (high → low → high) in seconds instead of the real ~10 minutes. The shipping tide is slow BY
    /// DESIGN (canon: ~20 real min per game day) — this never changes that default; it only multiplies the
    /// runtime <see cref="IGameClock.TimeScale"/> while the <see cref="_enabled"/> box is ticked, and it is
    /// <b>OFF by default</b>.
    ///
    /// <para><b>How to use.</b> In Play mode, tick <c>Enabled</c> on this component in the Inspector — the
    /// tide visibly races; untick it to drop straight back to the shipping rate (TimeScale = 1). (Unity
    /// applies serialized-field edits live in Play, so no key binding is needed and this stays free of any
    /// input-system dependency — the Environment module is pure sim.)</para>
    ///
    /// <para><b>Not a sim change.</b> The tide stays a pure function of <c>(worldSeed, gameTime)</c> — this
    /// just advances <c>gameTime</c> faster, exactly like the sanctioned sleep/wait fast-forward
    /// (time-tides-weather.md §1.3). Determinism is untouched: the same gameTime always yields the same
    /// tide. For seeing the swing on demand WITHOUT any wait, the in-editor <b>Tide Scrubber</b>
    /// (Tools ▸ Tide Scrubber) remains the precise tool; this is the in-Play convenience.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevFastTide : MonoBehaviour
    {
        [Tooltip("OFF by default. Tick in Play mode to run the clock at FastScale so the tide visibly swings " +
                 "in seconds. Untick to return to the shipping rate. Never affects the shipping default — it " +
                 "only multiplies the live TimeScale while ticked.")]
        [SerializeField] private bool _enabled = false;

        [Tooltip("How much faster than real time to run the clock while enabled. 60 → a full game day in " +
                 "~20 real seconds, so a high→low tide takes only a few seconds.")]
        [Min(1f)] [SerializeField] private float _fastScale = 60f;

        private bool _applied;

        private void OnDisable() => Restore();

        private void Update()
        {
            var clock = GameServices.Clock;
            if (clock == null) return;

            if (_enabled)
            {
                clock.TimeScale = Mathf.Max(1f, _fastScale);
                _applied = true;
            }
            else if (_applied)
            {
                Restore();
            }
        }

        private void Restore()
        {
            var clock = GameServices.Clock;
            if (_applied && clock != null) clock.TimeScale = 1f;   // back to the shipping default
            _applied = false;
        }
    }
}
