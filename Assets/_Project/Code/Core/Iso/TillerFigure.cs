using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One figure's way to its boat's tiller.</b> The sibling of <see cref="RowingFigureReader"/>:
    /// finds the <see cref="ITillerStation"/> on or above the hull the figure stands on, caches it per
    /// hull, and hands the solver the seated body's targets in the figure's own frame. It searches only
    /// while the figure stands at a helm, and again only when the hull changes or the station it held
    /// has gone; the search fills a reused list (no allocation once warm).
    /// </summary>
    public sealed class TillerFigureReader
    {
        private const int SearchCapacity = 4;

        /// <summary>Frames between searches of a hull that had no tiller station (a re-skin may add one).</summary>
        public const int RetryFrames = 30;

        private int _searchedFrame = int.MinValue / 2;

        private readonly List<ITillerStation> _found = new List<ITillerStation>(SearchCapacity);
        private Transform _hull;
        private ITillerStation _station;

        /// <summary>The station last found (null when none). Exposed for tests and the inspector.</summary>
        public ITillerStation Source => _station;

        /// <summary>
        /// The seated tiller pose for the figure on <paramref name="stand"/>, in the FIGURE's own frame,
        /// or false when he is not at a helm or the hull has no wired tiller (the figure then draws its
        /// stance as before). <paramref name="figureLocalMetres"/> and <paramref name="figureYawDegrees"/>
        /// are where the figure's root sits on the hull this frame; <paramref name="nowSeconds"/> is the
        /// presentation clock his breath runs on.
        /// </summary>
        public bool TryTargets(ICharacterFigureStand stand, CharacterStance stance, Vector3 figureLocalMetres,
                               float figureYawDegrees, out RowingPoseSolver.Targets targets, double nowSeconds = 0d)
        {
            targets = default;
            if (stand == null || stance != CharacterStance.Helm) return false;
            ITillerStation station = StationUnder(stand.FigureHull);
            if (station == null || !station.TryGetTillerTargets(out RowingTargets onHull)) return false;
            onHull.RestWeight = 1f;
            onHull.RestBreath = RowingFigureReader.LetGoBreath(nowSeconds, onHull.RestBreathSeconds);
            targets = RowingPoseSolver.ToFigure(onHull, figureLocalMetres, figureYawDegrees, 0f);
            return true;
        }

        /// <summary>The <see cref="ITillerStation"/> on <paramref name="hull"/> or one of its parents.</summary>
        public ITillerStation StationUnder(Transform hull)
        {
            if (hull == null)
            {
                Forget();
                return null;
            }
            bool sameHull = ReferenceEquals(hull, _hull);
            bool lost = _station is Object unityObject && unityObject == null;
            if (sameHull && _station != null && !lost) return _station;
            // Every powered hull's pilot stands at a helm and most have no tiller: a hull searched
            // and found bare is searched again only every RetryFrames, not every frame.
            int frame = Time.frameCount;
            if (sameHull && _station == null && frame - _searchedFrame < RetryFrames) return null;

            _searchedFrame = frame;
            _hull = hull;
            _station = null;
            hull.GetComponentsInParent(false, _found);
            if (_found.Count > 0) _station = _found[0];
            _found.Clear();
            return _station;
        }

        /// <summary>Drops the cached hull and station.</summary>
        public void Forget()
        {
            _hull = null;
            _station = null;
        }
    }
}
