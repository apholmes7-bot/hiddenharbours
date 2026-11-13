using System;
using UnityEngine;

namespace HiddenHarbours.World.Golf
{
    [Serializable]
    public sealed class GolfArea
    {
        public string Id;
        public Vector2[] Polygon = Array.Empty<Vector2>();
        public GolfSurfaceDef Surface;
    }

    [CreateAssetMenu(menuName = "Hidden Harbours/Golf/Hole")]
    public sealed class GolfHoleDef : ScriptableObject
    {
        public string Id = "hole.example";
        public string CourseId = "course.cmb";
        public string RegionId = "region.cumberland";
        [Min(1)] public int Number = 1;
        [Min(1)] public int Par = 4;
        public Vector2 TeePosition;
        public Vector2 CupPosition;
        public Vector2[] Boundary = Array.Empty<Vector2>();
        public GolfArea Tee = new GolfArea();
        public GolfArea Fairway = new GolfArea();
        public GolfArea Green = new GolfArea();
        public GolfSurfaceDef Rough;
        public GolfArea[] Hazards = Array.Empty<GolfArea>();
        [Tooltip("Logic-only plane in ground metres: height = elevation + dot(slope, position). Not terrain art.")]
        public float Elevation;
        public Vector2 Slope;

        public GolfHole Build()
        {
            GolfCheck.Id(Id, "hole."); GolfCheck.Id(CourseId, "course."); GolfCheck.Id(RegionId, "region.");
            if (Number < 1 || Par < 1) throw new ArgumentException("Hole number and par must be positive.");
            GolfCheck.Point(TeePosition); GolfCheck.Point(CupPosition); GolfCheck.Point(Slope);
            GolfCheck.Finite(Elevation, nameof(Elevation));
            return new GolfHole(this);
        }
    }

    public sealed class GolfHole
    {
        public string Id { get; }
        public string CourseId { get; }
        public string RegionId { get; }
        public int Number { get; }
        public int Par { get; }
        public Vector2 TeePosition { get; }
        public Vector2 CupPosition { get; }
        public Vector2 Slope { get; }
        readonly float _elevation;
        readonly GolfPolygon _boundary, _tee, _fairway, _green;
        readonly GolfPolygon[] _hazards;
        readonly GolfSurface _rough;

        internal GolfHole(GolfHoleDef d)
        {
            Id = d.Id; CourseId = d.CourseId; RegionId = d.RegionId; Number = d.Number; Par = d.Par;
            TeePosition = d.TeePosition; CupPosition = d.CupPosition; Slope = d.Slope; _elevation = d.Elevation;
            _boundary = new GolfPolygon(d.Boundary);
            _tee = Area(d.Tee, GolfLie.Tee); _fairway = Area(d.Fairway, GolfLie.Fairway);
            _green = Area(d.Green, GolfLie.Green);
            if (d.Rough == null) throw new ArgumentException("Missing rough surface.");
            _rough = d.Rough.Build();
            if (_rough.Lie != GolfLie.Rough) throw new ArgumentException("Default surface must be rough.");
            if (d.Hazards == null) throw new ArgumentException("Missing hazards array.");
            _hazards = new GolfPolygon[d.Hazards.Length];
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _hazards.Length; i++)
            {
                GolfArea a = d.Hazards[i];
                if (a == null || string.IsNullOrWhiteSpace(a.Id) || !ids.Add(a.Id))
                    throw new ArgumentException("Hazards need unique source ids.");
                _hazards[i] = Area(a);
                if (_hazards[i].Surface.Lie != GolfLie.Water && _hazards[i].Surface.Lie != GolfLie.Bunker)
                    throw new ArgumentException("Hazard must be water or bunker.");
            }
            if (LieAt(TeePosition) != GolfLie.Tee || LieAt(CupPosition) != GolfLie.Green)
                throw new ArgumentException("Tee and cup must be in their playable areas, within the boundary.");
        }

        static GolfPolygon Area(GolfArea area, GolfLie? required = null)
        {
            if (area == null || area.Surface == null) throw new ArgumentException("Missing area or surface.");
            GolfSurface surface = area.Surface.Build();
            if (required.HasValue && surface.Lie != required.Value) throw new ArgumentException("Wrong area lie.");
            return new GolfPolygon(area.Polygon, surface);
        }

        public float HeightAt(Vector2 position) => _elevation + Vector2.Dot(Slope, position);
        public GolfLie LieAt(Vector2 p) => !_boundary.Contains(p) ? GolfLie.OutOfBounds : SurfaceAt(p).Lie;
        internal GolfSurface SurfaceAt(Vector2 p)
        {
            // Water wins overlaps, then bunker; array ordering cannot make a pond playable.
            foreach (GolfPolygon h in _hazards) if (h.Surface.Lie == GolfLie.Water && h.Contains(p)) return h.Surface;
            foreach (GolfPolygon h in _hazards) if (h.Contains(p)) return h.Surface;
            if (_green.Contains(p)) return _green.Surface;
            if (_tee.Contains(p)) return _tee.Surface;
            return _fairway.Contains(p) ? _fairway.Surface : _rough;
        }

        /// <summary>Split rolling motion at every surface edge, even a hazard narrower than the step.</summary>
        internal float NextEdge(Vector2 a, Vector2 b)
        {
            float t = _boundary.FirstCrossing(a, b);
            t = Math.Min(t, _tee.FirstCrossing(a, b)); t = Math.Min(t, _green.FirstCrossing(a, b));
            t = Math.Min(t, _fairway.FirstCrossing(a, b));
            foreach (GolfPolygon h in _hazards) t = Math.Min(t, h.FirstCrossing(a, b));
            return t;
        }
    }
}
