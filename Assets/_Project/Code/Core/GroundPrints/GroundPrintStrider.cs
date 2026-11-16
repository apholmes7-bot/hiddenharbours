using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Spaces prints by distance travelled, not by frame.</b> The anchor is the last print; once the
    /// mover is <c>spacing</c> metres from it, the next print lands exactly <c>spacing</c> along the way
    /// and becomes the anchor. Call <see cref="Advance"/> in a loop: a fast mover (or a fast clock)
    /// gets every print it earned, evenly spaced, whatever the frame rate. A jump beyond
    /// <c>teleportMetres</c> restarts the trail where the mover landed instead of drawing a line.
    /// </summary>
    public sealed class GroundPrintStrider
    {
        private Vector2 _anchor;
        private bool _hasAnchor;

        /// <summary>Forget the trail; the next position seen becomes the new anchor.</summary>
        public void Reset() => _hasAnchor = false;

        /// <summary>
        /// True when a print is due: <paramref name="at"/> is where it lands, <paramref name="heading"/>
        /// the unit direction from the previous print. False when not yet a full spacing away (or on the
        /// first sight / a teleport, which only sets the anchor).
        /// </summary>
        public bool Advance(Vector2 position, float spacing, float teleportMetres,
                            out Vector2 at, out Vector2 heading)
        {
            at = position;
            heading = Vector2.up;
            if (!_hasAnchor || spacing <= 0f)
            {
                _anchor = position;
                _hasAnchor = true;
                return false;
            }

            Vector2 d = position - _anchor;
            float distance = d.magnitude;
            if (distance > teleportMetres)
            {
                _anchor = position;
                return false;
            }
            if (distance < spacing) return false;

            heading = d / distance;
            at = _anchor + heading * spacing;
            _anchor = at;
            return true;
        }
    }

    /// <summary>
    /// <b>A walker's boot prints.</b> Hold one per walker and call <see cref="Track"/> with the walker's
    /// ground position each update; it lays alternating left/right prints a stride apart and publishes
    /// <see cref="GroundPrintMade"/>. Allocation-free.
    /// </summary>
    public sealed class FootPrintTrail
    {
        private readonly GroundPrintStrider _strider = new GroundPrintStrider();
        private bool _left;

        public void Reset() => _strider.Reset();

        public void Track(Vector2 position)
        {
            GroundPrintSettings s = GroundPrintSettings.Current;
            IGameClock clock = GameServices.Clock;
            if (!s.Enabled || clock == null) { _strider.Reset(); return; }
            Track(position, clock.TotalSeconds, s);
        }

        /// <summary>The engine-light core of <see cref="Track(Vector2)"/>: returns how many prints were laid.</summary>
        public int Track(Vector2 position, double gameTime, in GroundPrintSettings s)
        {
            int laid = 0;
            while (laid < s.MaxPrintsPerUpdate &&
                   _strider.Advance(position, s.StrideMetres, s.TeleportMetres, out Vector2 at, out Vector2 heading))
            {
                Vector2 right = new Vector2(heading.y, -heading.x);
                Vector2 centre = at + right * (_left ? -s.FootSideOffsetMetres : s.FootSideOffsetMetres);
                EventBus.Publish(new GroundPrintMade(centre, heading,
                    _left ? GroundPrintKind.FootLeft : GroundPrintKind.FootRight,
                    s.FootLengthMetres, s.FootWidthMetres, 0f, gameTime));
                _left = !_left;
                laid++;
            }
            return laid;
        }
    }

    /// <summary>
    /// <b>A vehicle's tyre marks.</b> Hold one per vehicle and call <see cref="Track"/> with the centre of
    /// the axle that marks the ground, its track gauge and tyre width (all from the vehicle's data). It
    /// lays the run in <see cref="GroundPrintSettings.TireSegmentMetres"/> pieces, each a tyre PAIR.
    /// A new machine (a tractor) needs only its gauge and tyre width to leave marks.
    /// </summary>
    public sealed class TirePrintTrail
    {
        private readonly GroundPrintStrider _strider = new GroundPrintStrider();

        public void Reset() => _strider.Reset();

        public void Track(Vector2 axleCentre, float gaugeMetres, float tireWidthMetres)
        {
            GroundPrintSettings s = GroundPrintSettings.Current;
            IGameClock clock = GameServices.Clock;
            if (!s.Enabled || clock == null) { _strider.Reset(); return; }
            Track(axleCentre, gaugeMetres, tireWidthMetres, clock.TotalSeconds, s);
        }

        /// <summary>The engine-light core of <see cref="Track(Vector2,float,float)"/>.</summary>
        public int Track(Vector2 axleCentre, float gaugeMetres, float tireWidthMetres, double gameTime,
                         in GroundPrintSettings s)
        {
            if (gaugeMetres <= 0f || tireWidthMetres <= 0f) return 0;
            int laid = 0;
            float length = s.TireSegmentMetres;
            while (laid < s.MaxPrintsPerUpdate &&
                   _strider.Advance(axleCentre, length, s.TeleportMetres, out Vector2 at, out Vector2 heading))
            {
                EventBus.Publish(new GroundPrintMade(at - heading * (length * 0.5f), heading,
                    GroundPrintKind.TirePair, length, tireWidthMetres, gaugeMetres, gameTime));
                laid++;
            }
            return laid;
        }
    }
}
