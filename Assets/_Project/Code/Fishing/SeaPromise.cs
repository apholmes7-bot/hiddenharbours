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
    /// <para><b>Asked is not said.</b> <see cref="OnCast"/> only says the line is WANTED. The blow is
    /// spent by <see cref="Kept"/>, which the caller invokes when the line really played. If the line
    /// was refused (a conversation was up, it was cooling down), the next cast in the same blow asks
    /// again, so a refusal never leaves a whole blow silent.</para>
    ///
    /// <para>A POCO with two bools of state: no clock, no randomness, no allocation. The answer is a
    /// pure function of the sea samples and acknowledgements it was shown, in order (rule 5).</para>
    /// </summary>
    public sealed class SeaPromise
    {
        private bool _promised;   // this blow's line has been SAID
        private bool _inABlow;    // the last cast was into a sea that helps

        /// <summary>Has this blow's line actually been said? Cleared when the sea drops back.</summary>
        public bool Promised => _promised;

        /// <summary>Is the sea up enough, and the reward switched on, for a rough sea to fish better?</summary>
        public static bool SeaHelps(float seaState01, in SeaFishingSettings settings)
        {
            if (float.IsNaN(seaState01)) return false;
            bool rewardOn = settings.SeaBoldness01 > 0f || settings.SeaBigFishBias01 > 0f;
            return rewardOn && seaState01 > SeaFightMath.CozySeaCeiling01;
        }

        /// <summary>A line has gone in the water. True when the promise should be OFFERED: the sea
        /// helps and this blow's line has not been said yet. It stays true on later casts until
        /// <see cref="Kept"/> confirms the line played. A cast in everyday weather re-arms it.</summary>
        public bool OnCast(float seaState01, in SeaFishingSettings settings)
        {
            _inABlow = SeaHelps(seaState01, in settings);
            if (!_inABlow)
            {
                _promised = false;
                return false;
            }
            return !_promised;
        }

        /// <summary>The line was really said. Spends this blow; ignored outside one, so a stray
        /// acknowledgement in calm weather cannot silence the next blow.</summary>
        public void Kept()
        {
            if (_inABlow) _promised = true;
        }

        /// <summary>Forget the current blow (a new game, a fixture).</summary>
        public void Reset()
        {
            _promised = false;
            _inABlow = false;
        }
    }
}
