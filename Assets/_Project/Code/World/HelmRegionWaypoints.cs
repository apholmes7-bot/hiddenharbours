using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Setup-time projection of existing region content into Core waypoint values. No discovery on tick,
    /// travel activation, save, authored positions or scene changes. Positions remain in the source region.
    /// </summary>
    public sealed class HelmRegionWaypoints : IHelmWaypoints
    {
        private readonly HelmWaypoint[] _points;
        public int Count => _points.Length;
        public HelmWaypoint At(int index) => _points[index];

        public HelmRegionWaypoints(IEnumerable<HelmWaypoint> points)
        {
            var list = new List<HelmWaypoint>(points ?? throw new ArgumentNullException(nameof(points)));
            list.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (HelmWaypoint point in list)
                if (!point.IsValid || !ids.Add(point.Id)) throw new ArgumentException("Invalid or duplicate waypoint identity.", nameof(points));
            _points = list.ToArray();
        }

        public static HelmWaypoint SeamExit(string sourceRegionId, string stablePassageId, RegionPassage passage)
        {
            if (passage == null || passage.Target == null) throw new ArgumentException("Passage and target required.", nameof(passage));
            // Source trigger position, not the target's arrival coordinates. Access gates remain with travel.
            return new HelmWaypoint(stablePassageId, sourceRegionId, passage.Target.DisplayName,
                passage.transform.position, HelmWaypointKind.SeamExit);
        }

        public static HelmWaypoint Berth(BerthDef berth)
        {
            if (berth == null) throw new ArgumentNullException(nameof(berth));
            return new HelmWaypoint(berth.Id, berth.RegionId, berth.DisplayName,
                Vector2.Lerp(berth.Stern, berth.Bow, 0.5f), HelmWaypointKind.Berth);
        }
    }
}
