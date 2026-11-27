using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    public sealed partial class BuildingInterior : IRoomReachSource
    {
        // These are the existing architectural groups written by the interior builders.
        readonly List<PolygonCollider2D> _reachWalls = new List<PolygonCollider2D>();
        readonly List<Vector2> _reachPath = new List<Vector2>();
        readonly List<Collider2D> _reachShapes = new List<Collider2D>();

        public bool CanReach(Transform fixture, Vector2 actor, Vector2 target)
        {
            if (!isActiveAndEnabled || !RoomReach.Finite(actor) || !RoomReach.Finite(target)) return false;
            int floor = FurnishedLevelOf(fixture);
            if (!RoomReach.SameFloor(Level, floor)) return false;
            return ClearReachWalls(floor, actor, target);
        }

        public bool CanReachExterior(Vector2 actor, Vector2 target)
        {
            if (!RoomReach.Finite(actor) || !RoomReach.Finite(target)) return false;
            if (Level > 0 && FootprintFor(Level).Contains(actor, 0f)) return false;
            InteriorFootprint fp = Footprint;
            // A root fixture inside this floor has no trustworthy floor ownership.
            if (fp.Contains(target, 0f)) return false;
            Vector2 a = fp.WorldToModel(actor), b = fp.WorldToModel(target);
            if (Mathf.Min(a.x, b.x) > fp.WidthMetres * 0.5f ||
                Mathf.Max(a.x, b.x) < -fp.WidthMetres * 0.5f ||
                Mathf.Min(a.y, b.y) > fp.LengthMetres * 0.5f ||
                Mathf.Max(a.y, b.y) < -fp.LengthMetres * 0.5f) return true;
            return ClearReachWalls(0, actor, target);
        }

        bool ClearReachWalls(int floor, Vector2 actor, Vector2 target)
        {
            Transform outer = floor == 0 ? (_groundWalls != null ? _groundWalls : transform.Find("Walls"))
                                         : _upperWalls;
            if (outer == null) return false;
            _reachWalls.Clear();
            if (!ReadReachWalls(outer)) return false;
            if (floor == 0 && _props != null)
            {
                Transform partitions = _props.Find("WallsGround");
                if (partitions != null && !ReadReachWalls(partitions)) return false;
            }
            if (_reachWalls.Count == 0) return false;

            foreach (PolygonCollider2D wall in _reachWalls)
            {
                if (!wall.enabled || wall.isTrigger) continue;
                // Floor ownership selects the walls, independently of art hiding the room outside.
                Vector2 a = (Vector2)wall.transform.InverseTransformPoint(actor) - wall.offset;
                Vector2 b = (Vector2)wall.transform.InverseTransformPoint(target) - wall.offset;
                for (int path = 0; path < wall.pathCount; path++)
                {
                    wall.GetPath(path, _reachPath);
                    if (!RoomReach.Clear(a, b, _reachPath)) return false;
                }
            }
            return true;
        }

        bool ReadReachWalls(Transform root)
        {
            root.GetComponentsInChildren(true, _reachShapes);
            foreach (Collider2D shape in _reachShapes)
            {
                if (shape == null || !shape.enabled || shape.isTrigger) continue;
                if (!(shape is PolygonCollider2D polygon)) return false;
                _reachWalls.Add(polygon);
            }
            return true;
        }
    }
}
