using System;
using System.Collections.Generic;

namespace HiddenHarbours.World
{
    /// <summary>A capsule: the ground within <see cref="HalfWidth"/> of the segment A→B.</summary>
    [Serializable]
    public struct PlanCapsule
    {
        public PlanPoint A;
        public PlanPoint B;
        public double HalfWidth;

        public PlanCapsule(PlanPoint a, PlanPoint b, double halfWidth)
        {
            A = a; B = b; HalfWidth = halfWidth;
        }
    }

    /// <summary>A building the ground holds round (its pivot), and whether it is one of the large ones.</summary>
    [Serializable]
    public struct PlanBuilding
    {
        public string Id;
        public PlanPoint At;

        public PlanBuilding(string id, PlanPoint at)
        {
            Id = id; At = at;
        }
    }

    /// <summary>One sector of today's coast plan: from this bearing (degrees) to the next sector's, of this class.</summary>
    [Serializable]
    public struct PlanSector
    {
        public double FromDeg;
        public string Class;

        public PlanSector(double fromDeg, string cls)
        {
            FromDeg = fromDeg; Class = cls;
        }
    }

    /// <summary>
    /// The TerrainSplat shader's own height bands, as the scene's splat carries them: what it paints where no splat
    /// is (terrain pass 9's band_zone). The plan never changes them; it reads them to know today's ground.
    /// </summary>
    [Serializable]
    public sealed class PlanShaderBands
    {
        public double PaintFloor = -1.95, RippleFloor = -1.7, SandFloor = -0.4, MarramFloor = 1.6, GrassFloor = 4.2, ShingleFloor = -0.4;
        public double BandBlend = 0.35;
        public PlanPoint IslandCentre = new PlanPoint(70, 0);
        public double IslandAspect = 1.7308;
        public PlanPoint Weather = new PlanPoint(1.0 / Math.Sqrt(2), -1.0 / Math.Sqrt(2));
        public double SectorBlend = 0.08;
        public PlanPoint BarFrom = new PlanPoint(-45, 0), BarTo = new PlanPoint(-350, 0);
        public double BarHalfWidth = 30, BarEdge = 2;
    }

    /// <summary>
    /// <b>WHAT THE PLAN IS LAID OVER.</b> Everything the derivation reads besides the plan's Defs: today's ground
    /// (the analytic terrain the sim reads), today's paint, the placed things by scene root, the Landing's and the
    /// channels' shapes, the builder's roads and the coast plan's frame. A plain object, gathered once
    /// (TerrainPlanGatherer in the editor); the derivation is a pure function of it and the plan.
    /// </summary>
    public sealed class TerrainPlanSources
    {
        public TerrainPlanGrid Grid;

        /// <summary>
        /// Stamp the ambient fleet's grounds into the protected set, as pass 9 did. The parity run sets it; PR 5 does not:
        /// the grounds are region-wide, and the plan re-checks the fleet's day plan instead.
        /// </summary>
        public bool StampFleet;

        /// <summary>Today's ground (m), row-major, row 0 north: the analytic TidalTerrain's ElevationAt at each cell.</summary>
        public double[] Base;

        /// <summary>
        /// The ground the player stands on (m), row-major, row 0 north (terrain PR 5 B): the plan's ground file imported
        /// and its R16 decoded as the sim decodes it. Recomputed from the Defs at every derivation, never frozen; null
        /// where the plan has no ground file, and the derivation then paints on its own heights, as in Phase A.
        /// </summary>
        public double[] Ground;
        /// <summary>The import <see cref="Ground"/> came from: who laid each cell, and each ask's record.</summary>
        public GroundFileImport.Result Import;
        /// <summary>Today's paint: the zone the committed splat shows per cell (TerrainPlanZones; 255 = unpainted).</summary>
        public byte[] Today;

        /// <summary>The placed things by scene root name (world x, y), never the root's own (0, 0).</summary>
        public readonly Dictionary<string, List<PlanPoint>> Items = new Dictionary<string, List<PlanPoint>>();

        /// <summary>The buildings the ground holds round (their pivots).</summary>
        public readonly List<PlanBuilding> Buildings = new List<PlanBuilding>();

        /// <summary>The wharf's footprint.</summary>
        public PlanPoint WharfMin, WharfMax;

        /// <summary>The beach slip, the dredged approach and the berth pocket (their centre-lines and half widths).</summary>
        public PlanCapsule BerthSlip, ApproachCut, Pocket;

        /// <summary>The sandbar (part 1's bar capsule) and the gut across it.</summary>
        public PlanCapsule Sandbar, BarGut;

        /// <summary>The arrival route, the entrance channel.</summary>
        public PlanPoint[] Entrance = new PlanPoint[0];
        public double EntranceHalfWidth;

        /// <summary>The dock zone, the disembark and arrival marks, and the dory's mooring.</summary>
        public readonly List<PlanPoint> DockPoints = new List<PlanPoint>();

        /// <summary>The passages out of the region, and the East Water arrival.</summary>
        public readonly List<PlanPoint> Passages = new List<PlanPoint>();

        /// <summary>The ambient fleet's grounds.</summary>
        public PlanPoint FleetMin, FleetMax;

        /// <summary>The builder's lines a PathDef names in LineSource (class.member → points).</summary>
        public readonly Dictionary<string, PlanPoint[]> Lines = new Dictionary<string, PlanPoint[]>();

        /// <summary>The coast plan's frame: bearings are taken about this centre, y stretched by RadiusX / RadiusY.</summary>
        public PlanPoint IslandCentre;
        public double IslandRadiusX, IslandRadiusY;

        /// <summary>Today's coast plan, clockwise from bearing 0.</summary>
        public readonly List<PlanSector> Sectors = new List<PlanSector>();

        public PlanShaderBands Bands = new PlanShaderBands();

        /// <summary>What the sources were read from, and each input's SHA-256, for the manifest.</summary>
        public readonly SortedDictionary<string, string> Provenance = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public List<PlanPoint> ItemsOf(string root) =>
            root != null && Items.TryGetValue(root, out var l) ? l : new List<PlanPoint>();

        /// <summary>The bearing (degrees, 0 = north, clockwise) about the island in the coast plan's elliptical metric.</summary>
        public double Bearing(double x, double y)
        {
            double dx = x - IslandCentre.X, dy = (y - IslandCentre.Y) * (IslandRadiusX / IslandRadiusY);
            return TerrainPlanMath.Mod(TerrainPlanMath.Degrees(Math.Atan2(dx, dy)), 360.0);
        }

        /// <summary>The coast plan's sector class at a bearing (CoastPlan's primary sector).</summary>
        public string SectorClass(double bearing)
        {
            if (Sectors.Count == 0) return "";
            int idx = 0;
            for (int i = 0; i < Sectors.Count; i++)
            {
                double f = TerrainPlanMath.Mod(Sectors[i].FromDeg, 360.0), t = TerrainPlanMath.Mod(Sectors[(i + 1) % Sectors.Count].FromDeg, 360.0);
                bool inside = f <= t ? (bearing >= f && bearing < t) : (bearing >= f || bearing < t);
                if (inside && (i == 0 || idx == 0)) idx = i;
            }
            return Sectors[idx].Class;
        }
    }
}
