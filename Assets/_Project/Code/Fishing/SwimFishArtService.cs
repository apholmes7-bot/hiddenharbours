using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The Fishing side of Core's <see cref="ISwimFishArt"/>: a swimming fish's cell out of the same
    /// <see cref="FishSwimSpriteLibrary"/> the schools draw with, snapped to the rig's eight drawn headings
    /// (<see cref="ShoalMath.DirOf"/>, never rotated) and stepped at the rig's own frame rates
    /// (<see cref="ShoalEventMath.AnimShape"/>). Self-registered before the first scene loads; a service already
    /// set (a test's) is left alone. Stateless presentation wiring, never saved.
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

        public bool TrySwimCell(string speciesId, SwimFishPose pose, float headingRadians, double seconds, out Sprite cell)
        {
            cell = null;
            if (!_loaded)
            {
                _library = Resources.Load<FishSwimSpriteLibrary>(FishSwimSpriteLibrary.ResourcesPath);
                _loaded = true;
            }
            if (_library == null || !_library.TrySwimKindFor(speciesId, out string kind)) return false;
            string anim = AnimFor(pose);
            int frames = _library.FramesFor(kind, anim);
            if (frames <= 0) return false;
            double frameMs = FrameMs(pose);
            long step = (long)System.Math.Floor(seconds * 1000.0 / frameMs);
            // A one-shot pose (the roll) holds its last frame; the loops wrap.
            int frame = pose == SwimFishPose.Roll
                ? (int)System.Math.Min(System.Math.Max(step, 0L), frames - 1)
                : (int)(step % frames);
            cell = _library.Cell(kind, anim, ShoalMath.DirOf(headingRadians), frame);
            return cell != null;
        }

        public float PoseSeconds(SwimFishPose pose)
        {
            ShoalEventMath.AnimShape(KindFor(pose), out int frames, out double frameMs);
            return (float)(frames * frameMs / 1000.0);
        }

        private static double FrameMs(SwimFishPose pose)
        {
            ShoalEventMath.AnimShape(KindFor(pose), out _, out double frameMs);
            return frameMs;
        }

        private static ShoalEventKind KindFor(SwimFishPose pose)
        {
            switch (pose)
            {
                case SwimFishPose.Roll: return ShoalEventKind.Roll;
                case SwimFishPose.Thrash: return ShoalEventKind.Thrash;
                default: return ShoalEventKind.None;
            }
        }

        private static string AnimFor(SwimFishPose pose)
        {
            switch (pose)
            {
                case SwimFishPose.Roll: return FishSwimSpriteLibrary.AnimRoll;
                case SwimFishPose.Shadow: return FishSwimSpriteLibrary.AnimShadow;
                case SwimFishPose.Thrash: return FishSwimSpriteLibrary.AnimThrash;
                default: return FishSwimSpriteLibrary.AnimSwim;
            }
        }
    }
}
