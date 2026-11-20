using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>The two ground questions the print layer asks. Live: <see cref="LiveGroundPrintGround"/>;
    /// tests hand in a fake.</summary>
    public interface IGroundPrintGround
    {
        /// <summary>The print surface at <paramref name="worldPos"/>; <see cref="GroundSurface.None"/>
        /// where the ground takes no print (rock, planks, a deck, no ground known).</summary>
        GroundSurface SurfaceAt(Vector2 worldPos);

        /// <summary>Is the water over <paramref name="worldPos"/> at game time <paramref name="now"/>?</summary>
        bool IsWashed(Vector2 worldPos, double now);
    }

    /// <summary>
    /// The live answers: the zone the world registered (<see cref="GroundZoneSources"/>) mapped through
    /// <see cref="GroundPrintSettings.Zones"/>; a built deck (<see cref="StandableSurfaces"/>) takes no
    /// print; the water is the active region's tide over its authored terrain.
    /// </summary>
    public sealed class LiveGroundPrintGround : IGroundPrintGround
    {
        public GroundSurface SurfaceAt(Vector2 worldPos)
        {
            if (StandableSurfaces.TryGetDeckElevationNow(worldPos, out _)) return GroundSurface.None;
            if (!GroundZoneSources.TryGetZoneNow(worldPos, out string zone)) return GroundSurface.None;
            return GroundPrintSettings.Current.SurfaceForZone(zone);
        }

        public bool IsWashed(Vector2 worldPos, double now)
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            IEnvironmentService environment = GameServices.Environment;
            if (terrain == null || environment == null) return false;
            return GroundPrintMath.IsWashed(environment.WaterLevelAt(now), terrain.ElevationAt(worldPos));
        }
    }

    /// <summary>
    /// <b>The print layer's rules, engine-light.</b> Accepts signals onto the ring (refusing ground that
    /// takes no print and ground under water), and sweeps the ring a slice at a time: expired prints
    /// and prints the tide has covered go dead. The renderer only draws what this keeps.
    /// </summary>
    public sealed class GroundPrintLayer
    {
        private readonly GroundPrintRing _ring;
        private int _sweepCursor;

        public GroundPrintLayer(int capacity) { _ring = new GroundPrintRing(capacity); }

        public GroundPrintRing Ring => _ring;

        public void Clear() { _ring.Clear(); _sweepCursor = 0; }

        /// <summary>Put a signal's mark(s) on the ring. Returns how many were kept (0, 1, or 2 for a tyre pair).</summary>
        public int Accept(in GroundPrintMade m, IGroundPrintGround ground)
        {
            if (m.Kind == GroundPrintKind.TirePair)
            {
                Vector2 right = new Vector2(m.Heading.y, -m.Heading.x) * (m.GaugeMetres * 0.5f);
                return AcceptOne(m.Position - right, m, ground) + AcceptOne(m.Position + right, m, ground);
            }
            return AcceptOne(m.Position, m, ground);
        }

        private int AcceptOne(Vector2 at, in GroundPrintMade m, IGroundPrintGround ground)
        {
            GroundSurface surface = ground.SurfaceAt(at);
            if (surface == GroundSurface.None) return 0;
            if (ground.IsWashed(at, m.GameTime)) return 0;   // wading: the water takes no print
            _ring.Add(new GroundPrint
            {
                Position = at,
                Heading = m.Heading,
                LengthMetres = m.LengthMetres,
                WidthMetres = m.WidthMetres,
                Kind = m.Kind,
                Surface = surface,
                MadeAt = m.GameTime,
            });
            return 1;
        }

        /// <summary>
        /// Look at up to <paramref name="budget"/> slots (round robin) and kill any print that has faded
        /// out, cannot exist yet, or is under water now. Returns how many were killed.
        /// </summary>
        public int Sweep(double now, double lifetimeSeconds, IGroundPrintGround ground, int budget)
        {
            int written = _ring.Written;
            if (written == 0) return 0;
            int killed = 0;
            int looks = budget < written ? budget : written;
            for (int k = 0; k < looks; k++)
            {
                if (_sweepCursor >= written) _sweepCursor = 0;
                ref GroundPrint p = ref _ring[_sweepCursor];
                if (p.Alive &&
                    (GroundPrintMath.IsGone(p.MadeAt, now, lifetimeSeconds) || ground.IsWashed(p.Position, now)))
                {
                    p.Alive = false;
                    killed++;
                }
                _sweepCursor++;
            }
            return killed;
        }
    }
}
