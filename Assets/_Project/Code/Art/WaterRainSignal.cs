using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The rain at any world time, past ones included (ADR 0027 #7, gains 2): the rings' own law,
    /// <see cref="AmbientParticleMath.RainIntensity"/>, over that time's visibility and sea state, both asked
    /// of Core seams (<see cref="IVisibilitySource"/> and <see cref="IWaveWindSource"/>). So the silt a water
    /// type remembers and the rings on the water agree, and Art never reaches into Environment. Bound on the
    /// sample tick; it keeps no history and allocates nothing per tap.
    /// </summary>
    public sealed class WaterRainSignal : IWorldTimeSignal
    {
        private IVisibilitySource _visibility;
        private IWaveWindSource _wind;
        private float _baseline;
        private float _seaStateWeight;
        private float _visOnset;
        private float _visFull;
        private float _seaOnset;

        /// <summary>True when both seams are bound, so a past time's rain can be read.</summary>
        public bool IsBound => _visibility != null && _wind != null;

        /// <summary>Bind the seams and the rings' five rain dials (the same values the rings read).</summary>
        public void Bind(IVisibilitySource visibility, IWaveWindSource wind, float baseline, float seaStateWeight,
                         float visOnset, float visFull, float seaOnset)
        {
            _visibility = visibility;
            _wind = wind;
            _baseline = baseline;
            _seaStateWeight = seaStateWeight;
            _visOnset = visOnset;
            _visFull = visFull;
            _seaOnset = seaOnset;
        }

        /// <summary>0..1 rain at <paramref name="gameTimeSeconds"/>; 0 (dry) while unbound.</summary>
        public float ValueAt(double gameTimeSeconds)
        {
            if (_visibility == null || _wind == null) return 0f;
            return AmbientParticleMath.RainIntensity(_visibility.VisibilityAt(gameTimeSeconds),
                _wind.WaveWindAt(gameTimeSeconds).SeaState01,
                _baseline, _seaStateWeight, _visOnset, _visFull, _seaOnset);
        }
    }
}
