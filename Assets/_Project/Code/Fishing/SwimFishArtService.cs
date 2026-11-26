using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The Fishing side of Core's <see cref="ISwimFishArt"/>: a swimming fish's cell out of the same
    /// <see cref="FishSwimSpriteLibrary"/> the schools draw with, snapped to the rig's eight headings
    /// (<see cref="ShoalMath.DirOf"/>) and stepped at the rig's swim rate (<see cref="ShoalMath.SwimFrameMs"/>).
    /// Self-registered before the first scene loads; a service already set (a test's) is left alone.
    /// Stateless presentation wiring, never saved.
    /// </summary>
    public sealed class SwimFishArtService : ISwimFishArt
    {
        private FishSwimSpriteLibrary _library;
        private bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (SwimFishArt.Service == null) SwimFishArt.Service = new SwimFishArtService();
        }

        /// <summary>For tests: draw from <paramref name="library"/> rather than the Resources copy.</summary>
        public SwimFishArtService(FishSwimSpriteLibrary library = null)
        {
            _library = library;
            _loaded = library != null;
        }

        public bool TrySwimCell(string speciesId, float headingRadians, double seconds, out Sprite cell)
        {
            cell = null;
            if (!_loaded)
            {
                _library = Resources.Load<FishSwimSpriteLibrary>(FishSwimSpriteLibrary.ResourcesPath);
                _loaded = true;
            }
            if (_library == null || !_library.TrySwimKindFor(speciesId, out string kind)) return false;
            int frames = _library.FramesFor(kind, FishSwimSpriteLibrary.AnimSwim);
            if (frames <= 0) return false;
            int frame = (int)((long)System.Math.Floor(seconds * 1000.0 / ShoalMath.SwimFrameMs) % frames);
            cell = _library.Cell(kind, FishSwimSpriteLibrary.AnimSwim, ShoalMath.DirOf(headingRadians), frame);
            return cell != null;
        }
    }
}
