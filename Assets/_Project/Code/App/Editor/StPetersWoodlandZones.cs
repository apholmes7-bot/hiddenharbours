#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>ST PETERS' WOODLAND ZONES</b> — where the island's woods CLOSE, and where a path is cut through
    /// them. The owner ruled on 2026-08-16 that both regions get denser, more forest-like woods, and that
    /// <b>paths are cut, not implied</b>; this is the table that says where.
    ///
    /// <para><b>⭐ IT IS ALSO NOW THE ISLAND'S ONE CLEARING MECHANISM.</b> #551 shipped
    /// <see cref="StPetersGinnyPlot.IsInsideClearing"/> and asked it from exactly one line in
    /// <see cref="StPetersWoods.IsPlantable"/>, explicitly so this table could take it over by name. It
    /// has: her land is <see cref="GinnyPlotZone"/>, a row like any other, and <c>IsPlantable</c> now asks
    /// <see cref="IsCleared"/> instead. <b>The geometry was not copied</b> — the row READS
    /// <see cref="StPetersGinnyPlot.CottagePos"/> and <see cref="StPetersGinnyPlot.ClearingRadius"/>, so
    /// her land is still declared in her own file and a later pass that moves or grows it moves the row
    /// with it. <see cref="StPetersGinnyPlot.IsInsideClearing"/> survives as the question <i>"is this on
    /// GINNY's land"</i>, answered by asking this table about her row alone — which is a different question
    /// from <i>"is this ground cleared"</i>, and answering the general one there would put the village
    /// green inside her garden.</para>
    ///
    /// <para><b>A LOT OVERRIDES THE MOSAIC, AND THAT IS THE POINT.</b>
    /// <see cref="StPetersWoods.InStand"/> is the island's AMBIENT woods — a noise field that decides
    /// where a reverting island happens to be growing back (§5.1). A lot is AUTHORED: bounded ground the
    /// owner has ruled carries a closed stand whatever the noise says. The two compose rather than
    /// fighting — the ambient pass plants a lot's ground exactly as it always did, and the lot scatter
    /// infills between those trunks (<see cref="WoodlandZones.ScatterLots"/>), so no ambient tree moved
    /// when the island thickened.</para>
    ///
    /// <para><b>⚠ THE VILLAGE, THE SPAWN, THE CROSSING, THE DOCK AND THE PAINTED PATHS ARE NOT IN THIS
    /// TABLE.</b> They are explicit gates in <see cref="StPetersWoods.IsPlantable"/> and they stay there:
    /// the seam #551 left open was Ginny's predicate and only that one, and dragging five more rulings
    /// through a new file would have made a one-line takeover into a rewrite of placement. The rule for a
    /// later pass: a clearing that some OTHER file owns belongs here as a row that reads it; a clearing
    /// <c>IsPlantable</c> already owns in its own words stays where it is.</para>
    /// </summary>
    public static class StPetersWoodlandZones
    {
        // =================================================================================================
        //  1. THE NAMES — so a failing test, a log line and a call site all say the same word
        // =================================================================================================

        /// <summary>Aunt Ginny's land, declared in <see cref="StPetersGinnyPlot"/> and held open here.</summary>
        public const string GinnyPlotZone = "GinnyPlot";

        /// <summary>The cut lane from the village green out to her dooryard.</summary>
        public const string GinnyLaneZone = "GinnyLane";

        /// <summary>The hardwood grove in the deep southern interior.</summary>
        public const string SouthWoodZone = "SouthWood";

        /// <summary>The wood on the eastern interior, south of the slip path.</summary>
        public const string EastWoodZone = "EastWood";

        /// <summary>The wood on the north-east shoulder, east of Ginny's plot.</summary>
        public const string NorthWoodZone = "NorthWood";

        /// <summary>Suffix a lot's cut lane carries, so <c>SouthWood</c>'s corridor is
        /// <c>SouthWood_Lane</c> and nothing has to be spelled twice.</summary>
        public const string LaneSuffix = "_Lane";

        // =================================================================================================
        //  2. THE DIALS
        // =================================================================================================

        /// <summary>
        /// Trunk spacing (m) inside a lot's core — how closed a closed stand is.
        ///
        /// <para><b>3.6 m against the ambient <see cref="StPetersWoods.TreeStep"/>, and the ratio is the
        /// feature.</b> The ambient grain is a touch under one mature crown width so a stand reads closed
        /// with the occasional overlap; a lot is DENSER than that — crowns overlapping as the rule rather
        /// than the exception, which is what "forest-like" means on the ground and what the silhouette
        /// (#550) made safe to build. It does not go below
        /// <see cref="WoodlandZones.MinTrunkGapMetres"/>, because two trunks nearer than that read as one
        /// fat tree with two crowns rather than as a dense wood.</para>
        /// </summary>
        public const float LotCoreSpacingMetres = 3.6f;

        /// <summary>
        /// Fraction of a lot's radius that is solid core, the rest being the thinning fringe. 0.6 puts a
        /// little over a third of the lot's ground in a taper — enough that the edge reads as a wood
        /// petering out into meadow rather than as a disc stamped on the island.
        /// </summary>
        public const float LotCoreFraction = 0.6f;

        /// <summary>
        /// Half-width (m) of every cut lane on this island.
        ///
        /// <para><b>⭐ IT IS <see cref="StPetersWoods.PathClearance"/>, not a second number.</b> That
        /// constant already argues the case in its own words — <i>4 m, not 30 … a lane THROUGH woods,
        /// which is what a walked path in woods looks like</i>, and widening it "would read as a
        /// firebreak". A corridor cut through a dense lot is exactly the same object as the clearance
        /// beside a painted path, so it is exactly the same number, and re-ruling one re-rules both.</para>
        /// </summary>
        public static float LaneHalfWidthMetres => StPetersWoods.PathClearance;

        // =================================================================================================
        //  3. THE TABLE
        // =================================================================================================

        /// <summary>
        /// <b>Every woodland zone on St Peters.</b>
        ///
        /// <para><b>The three lots are sited on measured ground, not by eye.</b> Each one sits inside the
        /// island's sheltered interior (<see cref="StPetersWoods.InlandFraction"/> ≥ 0.58, so the habitat
        /// model offers it a real wood rather than a salt-blasted fringe), clears the village's 44 m
        /// clearing and Ginny's 18 m one with room to walk between, and is far enough from the crossing's
        /// approach and the dock that neither sightline ruling is touched.
        /// <c>StPetersWoodlandZonesTests</c> re-derives every one of those clearances from the constants
        /// rather than trusting these coordinates, and fails with the overlap it found.</para>
        ///
        /// <para><b>Each lot's lane is DERIVED, never authored.</b> See
        /// <see cref="CrossingLane"/> — a corridor is cut straight through a lot on the bearing of the
        /// village green, which is the ground you arrive from. That is the convention
        /// <see cref="StPetersGinnyPlot.Approach"/> already turns her door on, and it means re-siting a lot
        /// moves its path with it instead of leaving a lane through empty meadow.</para>
        /// </summary>
        public static IReadOnlyList<WoodlandZone> Zones
        {
            get
            {
                if (_zones != null) return _zones;

                var rows = new List<WoodlandZone>();

                // ---- the clearings ------------------------------------------------------------------
                // ⭐ HER LAND, READ FROM HER OWN FILE. See the class remarks: this is the #551 seam being
                // taken over by name, and the geometry deliberately is not restated here.
                rows.Add(WoodlandZone.Circle(
                    GinnyPlotZone, WoodlandZoneKind.Clearing,
                    StPetersGinnyPlot.CottagePos, StPetersGinnyPlot.ClearingRadius,
                    coreFraction: 1f, coreSpacingMetres: 0f,
                    reason: "Aunt Ginny's plot — her cottage, dooryard, garden, freezer and three derelict " +
                            "sheds. Declared in StPetersGinnyPlot and only read here, so the day her land " +
                            "moves or grows this row moves with it."));

                // ---- the cut lanes -------------------------------------------------------------------
                // ⭐ THE COMMUTE StPetersRoutines LEFT TO THIS LANE, in its own words: "No via-bends:
                // there is no painted path out there yet, and cutting one is the forest lane's work (its
                // path corridors), not this file's." Her lane node hangs straight off the green, so the
                // corridor is that exact segment — she walks 85 m through the island's densest woods
                // twice a day and a trunk in the tread is a person walking through a tree, which is the
                // bug StPetersWoods.PathClearance was created to fix for the painted paths.
                //
                // ⚠ It is a CUT CANOPY, not a painted path. The ground under it is the meadow's and the
                // splat's business; this row plants no dirt and repaints nothing.
                rows.Add(WoodlandZone.Lane(
                    GinnyLaneZone, StPetersBuilder.VillageGreen, StPetersGinnyPlot.Dooryard,
                    LaneHalfWidthMetres,
                    "the walk from the village green out to Ginny's dooryard — the one lane on this island " +
                    "a villager walks daily that has no painted tread under it (StPetersRoutines' " +
                    "LaneGinnyPlot, which hangs straight off the green with no via-bends)"));

                // ---- the lots ------------------------------------------------------------------------
                // Deep southern interior: elliptical distance ~69 m of 120, so InlandFraction clamps to 1
                // — the most sheltered ground on the island, which is the only place the habitat model
                // lets the hardwoods lead. This lot is the island's maple/oak/birch grove.
                AddLot(rows, SouthWoodZone, new Vector2(52f, -42f), 22f,
                       "the deep southern interior — the most sheltered ground on the island (exposure 0), " +
                       "so this is where the habitat model grows a hardwood grove rather than more spruce. " +
                       "76.4 m from the village hearth against the 44 m clearing plus its own 22 m, so it " +
                       "clears the village by a walkable 10 m rather than by a rounding error");

                // The eastern interior, south of the painted slip path: still sheltered core, and the
                // largest unbroken piece of plantable ground the island has.
                AddLot(rows, EastWoodZone, new Vector2(128f, -30f), 24f,
                       "the eastern interior below the slip path — the largest unbroken run of plantable " +
                       "ground on the island, and the wood you walk past on the way to the boat");

                // The north-east shoulder. Clears Ginny's 18 m clearing by a walkable margin and sits a
                // shade further out (InlandFraction ~0.82), which puts red spruce and fir into the mix
                // beside the hardwoods — so the island's three woods do not read as one wood three times.
                AddLot(rows, NorthWoodZone, new Vector2(126f, 36f), 20f,
                       "the north-east shoulder, east of Ginny's plot — a little more exposed than the " +
                       "other two, so its mix runs to red spruce and fir and the three woods read " +
                       "differently from each other");

                _zones = rows.ToArray();
                return _zones;
            }
        }

        static WoodlandZone[] _zones;

        /// <summary>Drop the memoised table. Nothing in a build needs this — the zones are constants — but
        /// a test that wants to prove the cache is not the thing making its assertion true can clear it,
        /// and the sabotage arms rebuild the table with a bad row in it.</summary>
        public static void InvalidateCache() => _zones = null;

        /// <summary>Add a lot AND the lane cut through it — the two always travel together, because a
        /// dense wood with no way through it is the thing the owner's "paths are cut, not implied" ruling
        /// is against.</summary>
        static void AddLot(List<WoodlandZone> rows, string name, Vector2 at, float radius, string reason)
        {
            rows.Add(WoodlandZone.Circle(name, WoodlandZoneKind.Lot, at, radius,
                                        LotCoreFraction, LotCoreSpacingMetres, reason));
            rows.Add(CrossingLane(name + LaneSuffix, at, radius, StPetersBuilder.VillageGreen,
                                  LaneHalfWidthMetres,
                                  $"the walked lane through {name} — cut on the bearing of the village " +
                                  "green, which is the ground you come up from"));
        }

        /// <summary>
        /// A lane cut clean THROUGH a circular lot, on the bearing of <paramref name="toward"/>.
        ///
        /// <para>Derived rather than authored: the spine runs from one rim to the other through the
        /// centre, so it always crosses (a lane that stops inside a wood is a dead end) and re-siting the
        /// lot re-cuts its path. Extended a lane's own half-width past each rim so the corridor meets the
        /// open ground outside instead of ending in the fringe.</para>
        /// </summary>
        public static WoodlandZone CrossingLane(string name, Vector2 centre, float radius, Vector2 toward,
                                                float halfWidth, string reason)
        {
            Vector2 d = toward - centre;
            // A lot centred exactly on the green would have no bearing to cut on; fall back to east–west
            // rather than producing a zero-length spine that clears a disc instead of a lane.
            Vector2 dir = d.sqrMagnitude < 1e-6f ? Vector2.right : d.normalized;
            float reach = radius + halfWidth;
            return WoodlandZone.Lane(name, centre - dir * reach, centre + dir * reach, halfWidth, reason);
        }

        // =================================================================================================
        //  4. WHAT PLACEMENT ASKS
        // =================================================================================================

        /// <summary>
        /// <b>⭐ THE ONE PREDICATE <see cref="StPetersWoods.IsPlantable"/> ASKS.</b> Is this ground inside
        /// a clearing or a cut lane — anything that takes trees away?
        /// </summary>
        public static bool IsCleared(Vector2 p) => WoodlandZones.IsCleared(Zones, p);

        /// <summary>Is this ground inside the named zone? Asked when a caller means one particular lot —
        /// see <see cref="StPetersGinnyPlot.IsInsideClearing"/>.</summary>
        public static bool IsInside(string name, Vector2 p) => WoodlandZones.IsInside(Zones, name, p);

        /// <summary>How strongly this ground is inside a woodland LOT (1 in a core, tapering to 0 at the
        /// rim, 0 outside), and which lot it is.</summary>
        public static float LotShareAt(Vector2 p, out string lot) =>
            WoodlandZones.LotShareAt(Zones, p, out lot);

        /// <summary>Is this ground inside a woodland lot at all — core or fringe? What
        /// <see cref="StPetersWoods.InWoods"/> adds to the ambient mosaic, so a bloom that belongs on a
        /// forest floor knows it is standing on one.</summary>
        public static bool InALot(Vector2 p) => LotShareAt(p, out _) > 0f;

        /// <summary>The lots alone, for a log line or a budget test.</summary>
        public static IEnumerable<WoodlandZone> Lots
        {
            get
            {
                foreach (var z in Zones) if (z.Kind == WoodlandZoneKind.Lot) yield return z;
            }
        }

        /// <summary>How many woodland lots the island declares — read out of the table rather than typed
        /// into a log line, so adding a fourth cannot leave the build saying "three".</summary>
        public static int LotCount
        {
            get
            {
                int n = 0;
                foreach (var z in Zones) if (z.Kind == WoodlandZoneKind.Lot) n++;
                return n;
            }
        }
    }
}
#endif
