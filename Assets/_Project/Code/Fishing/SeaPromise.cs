using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>The sea says when it is worth fishing.</b> A blow already makes fish bite sooner and run
    /// bigger (<see cref="SeaFightMath.BiteDelayScale"/>, <see cref="SeaFightMath.WeightBias01"/>), and
    /// until now nothing told the player. This is the WHEN of one inner-voice line: the first cast of a
    /// sea that has risen past the everyday ceiling, and not again until the weather has changed.
    ///
    /// <para><b>No new threshold.</b> The line is <see cref="SeaFightMath.CozySeaCeiling01"/>, the one
    /// the fight already draws between everyday weather and a sea that means it. Above it the trade is
    /// real on both sides, so that is where the promise is honest. If the owner has switched both
    /// rewards off (<see cref="SeaFishingSettings.SeaBoldness01"/> and
    /// <see cref="SeaFishingSettings.SeaBigFishBias01"/> at zero) the sea helps nobody and this stays
    /// silent.</para>
    ///
    /// <para><b>"The weather changed" means the sea came back down.</b> One blow is one promise, however
    /// many casts are made in it and however it gusts; the next promise needs the sea to have dropped
    /// to the everyday side of the line first. The memory is this object's alone: it is not saved, so a
    /// fresh session in a standing blow says the line once more.</para>
    ///
    /// <para>A POCO with one bool of state: no clock, no randomness, no allocation. The answer is a pure
    /// function of the sea samples it was shown, in order (rule 5).</para>
    /// </summary>
    public sealed class SeaPromise
    {
        private bool _promised;

        /// <summary>Has this blow already been announced? Cleared when the sea drops back.</summary>
        public bool Promised => _promised;

        /// <summary>Is the sea up enough, and the reward switched on, for a rough sea to fish better?</summary>
        public static bool SeaHelps(float seaState01, in SeaFishingSettings settings)
        {
            if (float.IsNaN(seaState01)) return false;
            bool rewardOn = settings.SeaBoldness01 > 0f || settings.SeaBigFishBias01 > 0f;
            return rewardOn && seaState01 > SeaFightMath.CozySeaCeiling01;
        }

        /// <summary>A line has gone in the water. True exactly once per blow: on the first cast after
        /// the sea has risen past the line. A cast in everyday weather re-arms it.</summary>
        public bool OnCast(float seaState01, in SeaFishingSettings settings)
        {
            if (!SeaHelps(seaState01, in settings))
            {
                _promised = false;
                return false;
            }
            if (_promised) return false;
            _promised = true;
            return true;
        }

        /// <summary>Forget the current blow (a new game, a fixture).</summary>
        public void Reset() => _promised = false;
    }
}
