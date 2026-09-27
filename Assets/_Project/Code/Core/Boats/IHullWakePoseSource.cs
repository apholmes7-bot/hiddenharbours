using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Current rendered transom, in screen-world metres. Consumers remove the water's
    /// drawing lift at birth; deposited history then remains in the water's world frame.</summary>
    public readonly struct HullWakePose
    {
        public readonly Vector2 DrawnStern;
        public readonly Vector2 Heading;
        public readonly float TideRise;

        public HullWakePose(Vector2 drawnStern, Vector2 heading, float tideRise)
        { DrawnStern = drawnStern; Heading = heading; TideRise = tideRise; }
    }

    public interface IHullWakePoseSource
    {
        bool TryGetWakePose(out HullWakePose pose);
    }
}
