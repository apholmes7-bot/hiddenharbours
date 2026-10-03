using System;
using System.Linq;
using HiddenHarbours.World;
using NUnit.Framework;
using static HiddenHarbours.Tests.EditMode.StPetersVillageTestData;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillageWindowsTests
    {
        [Test]
        public void G12_EveryRouteWindowHasAnAuthoredFocal()
        {
            var d = Load(); var windows = d.Derive().Windows;
            Check(windows.Length > 0, 12, d.Plan.Id, "windows derived from route crossings");
            foreach (var w in windows)
            {
                Check(!string.IsNullOrWhiteSpace(w.Focal), 12, w.Key, "a door, commons, light, roadside piece or named biome focal");
                foreach (var lot in d.Lots)
                    if (w.Focal.IndexOf(lot.BuildingId.Substring("building.stp_".Length), StringComparison.Ordinal) >= 0)
                        Check(w.BuildingIds.Contains(lot.BuildingId), 12, w.Key + "/" + lot.BuildingId,
                            "the building named as focal is actually in the derived window");
            }
        }

        [Test]
        public void G13_NoWindowShowsABuildingsBack()
        {
            var d = Load(); var derived = d.Derive();
            foreach (var w in derived.Windows)
                foreach (var id in w.BuildingIds)
                {
                    var b = derived.Buildings.Single(x => x.Id == id);
                    Check(b.FacingCell >= 2 && b.FacingCell <= 6, 13, w.Key + "/" + id, "camera can read the front or a side");
                }
        }

        [Test]
        public void G14_EdgeOnDoorsHaveAGateAndLantern()
        {
            var d = Load(); var derived = d.Derive();
            foreach (var w in derived.Windows)
                foreach (var id in w.BuildingIds)
                {
                    var b = derived.Buildings.Single(x => x.Id == id);
                    var lot = d.Lots.Single(l => l.Id == b.LotId);
                    if (!VillageGeometry.InBounds(w.Bounds, b.Door) || (lot.DoorFaces != "W" && lot.DoorFaces != "E")) continue;
                    Check(d.Lights.Any(l => l.LotId == lot.Id && (l.KitPiece.Contains("doorstep") || l.KitPiece == "entry.lantern") &&
                        VillageGeometry.Distance(l.Position, b.Door) <= d.T("MARKER_REACH")), 14, w.Key + "/" + id, "doorstep lantern marks side entry");
                    Check(d.Yards.Any(y => y.LotId == lot.Id && y.Fence != YardFence.None &&
                        VillageGeometry.Distance(y.Gate, lot.Gate) <= d.T("EPSILON")), 14, w.Key + "/" + id, "gate marks the house walk");
                }
        }

        [Test]
        public void G15_LightPoolsStayWithinTheGamesProfile()
        {
            var d = Load();
            foreach (var w in d.Derive().Windows)
                Check(w.Pools <= d.Budget.MaxPools, 15, w.Key, $"{w.Pools} light pools <= {d.Budget.MaxPools}");
        }

        [Test]
        public void G16_ShadowPairsStayWithinTheGamesProfile()
        {
            var d = Load();
            foreach (var w in d.Derive().Windows)
                Check(w.ShadowPairs <= d.Budget.MaxShadows, 16, w.Key, $"{w.ShadowPairs} lamp/caster pairs <= {d.Budget.MaxShadows}");
        }
    }
}
