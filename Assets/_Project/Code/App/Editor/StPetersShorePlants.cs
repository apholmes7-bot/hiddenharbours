#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The tidal coast's planting</b> — rockweed, marsh and the first pass at the ocean floor, laid
    /// out by the five tidal zones the shoreline plant rig already understands. Pure and deterministic,
    /// reading the same painted seabed everything else on this island reads;
    /// <see cref="StPetersWoodsPlanter"/> does the Unity work and
    /// <c>ShorePlantTideView</c> makes each plant follow the water once it is placed.
    ///
    /// <para><b>⭐ THE ZONES ARE ST PETERS', NOT THE KIT'S.</b> The kit bakes its states against a
    /// NOMINAL 4 m harbour range with zone bases at 0.15 / 1.55 / 2.55 / 3.35 / 4.65 m. St Peters is a
    /// ±2.2 m island (<c>StPeters.asset</c>: <c>TideAmplitude 2.2</c>, mean level 0). Planting against
    /// the kit's numbers would put the whole marsh above this island's highest water and leave the
    /// intertidal empty — every plant would sit in one state forever. So the bands below are derived
    /// from THIS region's own tide, and the kit's ladder is only ever used the way
    /// <c>ShorePlantTideMath</c> uses it: to choose which baked state a live depth is nearest.</para>
    ///
    /// <para><b>⭐ THE WET END IS THE POINT.</b> Two of these five bands are under water for part or
    /// all of the day, which is the thing that had never been placed: intertidal plants the tide covers
    /// and uncovers, and subtidal beds below low water that read THROUGH the shallows. The subtidal
    /// band thins with depth on the water shader's own number — <c>_ShallowSeeThroughDepth</c> = 0.6 m
    /// (ADR 0027) — because a bed nobody can see through the water is a bed that costs draw calls and
    /// buys nothing.</para>
    ///
    /// <para><b>Nothing plants in deep water.</b> <see cref="SubtidalFloorElevation"/> stops the beds
    /// well inside the reef shelf. Past it the column is deeper than the seabed ramp reads through at
    /// any state of the tide, and the painted ground has stopped too
    /// (<see cref="StPetersShoreMap.PaintFloorElevation"/> = −1.95 m) — there would be nothing to see a
    /// bed against.</para>
    /// </summary>
    public static class StPetersShorePlants
    {
        // =====================================================================================
        // this island's tidal staircase
        // =====================================================================================

        /// <summary>Mean high water springs, metres above chart datum — <c>StPeters.asset</c>'s
        /// <c>TideAmplitude</c> about its zero mean level. Ground above this is never wet.</summary>
        public const float HighWaterElevation = 2.2f;

        /// <summary>Mean low water springs. Ground below this is never exposed.</summary>
        public const float LowWaterElevation = -2.2f;

        /// <summary>The high-marsh band's floor: the top 0.8 m of the intertidal, flooded only near the
        /// top of the tide. This is where cattail, black rush, threesquare and saltmeadow hay live.</summary>
        public const float HighMarshFloorElevation = 1.4f;

        /// <summary>The low-marsh band's floor — mean sea level. Cordgrass and glasswort: wet twice a
        /// day, standing in air the rest of the time.</summary>
        public const float LowMarshFloorElevation = 0f;

        /// <summary>
        /// The mid-intertidal floor: the rockweed band, covered and uncovered EVERY tide — the
        /// most-seen zone in the game, because it is the one that visibly changes.
        ///
        /// <para><b>⚠ −1.0 m, and where this line falls is the whole subtidal question.</b> The reef
        /// shelf rings this island at −1.0 to −1.5 m (<see cref="StPetersBuilder.ReefShelfInnerElevation"/>
        /// and its outer twin) and it is 25 m wide — by far the largest piece of shallow seabed St
        /// Peters has. Put this floor below the shelf and the shelf becomes "intertidal", which swallows
        /// the coast: measured, that gave 865 rockweed plants and <b>38</b> subtidal ones, which is not
        /// populating an ocean floor by any reading. The shelf only bares at the lowest spring tides and
        /// spends the great majority of the cycle under a metre or so of water — which is exactly the
        /// shallow bottom the owner asked to see beds through. So the shelf is the FRINGE, and the mid
        /// intertidal is the beach's own lower face above it.</para>
        /// </summary>
        public const float MidIntertidalFloorElevation = -1.0f;

        /// <summary>
        /// 🔴 The seaward limit of planting. −2.6 m is 0.4 m below mean low water springs, so beds here
        /// are genuinely subtidal (never exposed), and it is only 0.65 m past the painted ground's own
        /// outer edge (<see cref="StPetersShoreMap.PaintFloorElevation"/>). Deeper than this the water
        /// column exceeds what the seabed reads through, AND there is no painted bottom behind the bed
        /// — so it would be an invisible draw call. Not a limit to raise without the water shader.
        /// </summary>
        public const float SubtidalFloorElevation = -2.6f;

        /// <summary>The water shader's own see-through depth (ADR 0027's
        /// <c>_ShallowSeeThroughDepth</c>): at or under this much water at MEAN tide a bed reads
        /// clearly, so it plants at full density. Below it density falls away linearly to nothing at
        /// <see cref="SubtidalFloorElevation"/>.</summary>
        public const float ShallowSeeThroughDepthM = 0.6f;

        /// <summary>The kit's five zone keys, in the order the contract publishes them.</summary>
        public const string ZoneFringe = "fringe";
        public const string ZoneMid = "mid";
        public const string ZoneLowMarsh = "low";
        public const string ZoneHighMarsh = "high";
        public const string ZoneUpland = "upland";

        /// <summary>
        /// Which tidal zone a metre of St Peters is, or null where nothing plants (too deep, or inland
        /// past the upland's own reach). Elevation is the painted seabed — the ONE height map — so this
        /// agrees with the walkability gate and the water shader by construction.
        /// </summary>
        public static string ZoneAt(float elevation)
        {
            if (elevation < SubtidalFloorElevation) return null;              // deep water: nothing yet
            if (elevation < MidIntertidalFloorElevation) return ZoneFringe;   // subtidal beds
            if (elevation < LowMarshFloorElevation) return ZoneMid;           // the rockweed band
            if (elevation < HighMarshFloorElevation) return ZoneLowMarsh;
            if (elevation < HighWaterElevation) return ZoneHighMarsh;
            if (elevation < StPetersShoreMap.GrassFloorElevation) return ZoneUpland;
            return null;                                                       // the meadow: grass's job
        }

        // =====================================================================================
        // who grows where
        // =====================================================================================

        /// <summary>
        /// The species each zone plants, by the kit's own keys. Deliberately a SUBSET of the sixteen:
        /// this is a Nova Scotian island, and the whole kit on one shore would read as a botanical
        /// garden. Species not listed here are baked, Def'd and placeable by hand — they are simply not
        /// in the automatic pass.
        /// </summary>
        public static readonly Dictionary<string, string[]> ZoneSpecies =
            new Dictionary<string, string[]>
            {
                // Below low water — the ocean floor's first pass. Eelgrass beds on the sandy shelf,
                // kelp on the harder ground, Irish moss in the gaps.
                [ZoneFringe] = new[] { "Eelgrass", "SugarKelp", "IrishMoss" },
                // The rockweed band, bared and covered every tide — the zone the tide is READ off.
                [ZoneMid] = new[] { "KnottedWrack", "Bladderwrack", "SeaLettuce" },
                [ZoneLowMarsh] = new[] { "Cordgrass", "Glasswort" },
                [ZoneHighMarsh] = new[] { "SaltmeadowHay", "BlackRush", "Threesquare" },
                // The dune top. Cattail is held back from the automatic pass — it wants standing FRESH
                // water and this island has none authored, so it would be a botanical mistake the eye
                // does not catch but a Maritimer would.
                [ZoneUpland] = new[] { "MarramGrass", "BeachPea", "Bayberry", "SweetFern" },
            };

        // =====================================================================================
        // the scatter
        // =====================================================================================

        /// <summary>Grid spacing (m) of candidate sites. Coarser than the grass — these are whole
        /// plants and clumps, not blades, and a shore is a band rather than a field.</summary>
        public const float PlantStep = 3.2f;

        public const float PlantJitter = 1.3f;

        /// <summary>Feature size (m) of the patchiness field — beds come in patches with clear ground
        /// between, the way weed actually grows on a shore.</summary>
        public const float PatchScale = 18f;

        /// <summary>The patch field must clear this for a site to be considered.</summary>
        public const float PatchThreshold = -0.3f;

        /// <summary>Per-zone base chance a qualifying cell plants. The mid intertidal is the densest —
        /// rockweed carpets a Nova Scotian shore — and the upland is thinnest because the dune is
        /// mostly sand.</summary>
        public static float ChanceFor(string zone) =>
            zone == ZoneMid ? 0.78f :
            zone == ZoneFringe ? 0.72f :     // the shelf: the headline zone, before DepthFade thins it
            zone == ZoneLowMarsh ? 0.62f :
            zone == ZoneHighMarsh ? 0.55f : 0.34f;

        public const float ScaleMin = 0.85f;
        public const float ScaleMax = 1.2f;

        /// <summary>
        /// How much of the subtidal chance survives at this depth. 1 where the water is shallow enough
        /// to read straight through (<see cref="ShallowSeeThroughDepthM"/> at mean tide), falling
        /// linearly to 0 at <see cref="SubtidalFloorElevation"/>. This is the "thinning out as it
        /// deepens" the owner asked for, and it is keyed to the shader's own see-through number rather
        /// than to taste.
        /// </summary>
        public static float DepthFade(float elevation)
        {
            float depthAtMeanTide = -elevation;                       // mean level is 0 on this island
            if (depthAtMeanTide <= ShallowSeeThroughDepthM) return 1f;
            float far = -SubtidalFloorElevation;
            return Mathf.Clamp01(1f - (depthAtMeanTide - ShallowSeeThroughDepthM)
                                      / Mathf.Max(0.01f, far - ShallowSeeThroughDepthM));
        }

        /// <summary>One placed plant: where, which species, which zone it was placed for, how big.</summary>
        public struct ShorePlantSite
        {
            public Vector2 Position;
            /// <summary>A kit species key — <c>KnottedWrack</c>, <c>Eelgrass</c>… The planter resolves
            /// it to the matching <c>ShorePlantDef</c>; nothing here knows a sprite.</summary>
            public string SpeciesKey;
            public string Zone;
            public float Scale;
        }

        /// <summary>
        /// Every shore plant on the island, deterministically. Swept over the whole region rectangle
        /// rather than the island box, because the subtidal band reaches out past the land.
        /// </summary>
        public static List<ShorePlantSite> Scatter(ITidalTerrain terrain)
        {
            var sites = new List<ShorePlantSite>();
            if (terrain == null) return sites;

            // The coast plus its shelf: the island box grown by the beach falloff and the reef shelf,
            // which is exactly as far out as SubtidalFloorElevation can reach.
            float margin = StPetersBuilder.IslandFalloff + StPetersBuilder.ReefShelfWidth;
            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius - margin;
            float maxX = StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius + margin;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY - margin;
            float maxY = StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY + margin;

            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / PlantStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / PlantStep));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                float cx = minX + (ix + 0.5f) * PlantStep
                           + (StPetersShoreMap.Hash01(ix, iy, 211) * 2f - 1f) * PlantJitter;
                float cy = minY + (iy + 0.5f) * PlantStep
                           + (StPetersShoreMap.Hash01(ix, iy, 223) * 2f - 1f) * PlantJitter;
                var p = new Vector2(cx, cy);

                float e = terrain.ElevationAt(p);
                string zone = ZoneAt(e);
                if (zone == null) continue;

                if (!IsPlantableShore(p)) continue;

                float patch = StPetersShoreMap.Wiggle(
                    p * (StPetersShoreMap.BandWiggleScale / PatchScale), salt: 227);
                if (patch <= PatchThreshold) continue;

                float chance = ChanceFor(zone);
                if (zone == ZoneFringe) chance *= DepthFade(e);
                if (StPetersShoreMap.Hash01(ix, iy, 229) > chance) continue;

                var species = ZoneSpecies[zone];
                int pick = Mathf.Min(species.Length - 1,
                                     (int)(StPetersShoreMap.Hash01(ix, iy, 233) * species.Length));

                sites.Add(new ShorePlantSite
                {
                    Position = p,
                    SpeciesKey = species[pick],
                    Zone = zone,
                    Scale = Mathf.Lerp(ScaleMin, ScaleMax, StPetersShoreMap.Hash01(ix, iy, 239)),
                });
            }
            return sites;
        }

        /// <summary>
        /// The shore's own clearings. Deliberately NOT <see cref="StPetersWoods.IsPlantable"/>: that
        /// one's radii are cut for trees on the plateau, and it would strip the whole intertidal in
        /// front of the village. What actually has to stay clear on a shore is what the player uses —
        /// the berth, the dock, and the sandbar crossing, all of which are walked or sailed and would
        /// read as blocked if weed grew across them.
        /// </summary>
        public static bool IsPlantableShore(Vector2 p)
        {
            if (Vector2.Distance(p, StPetersBuilder.StartSpawnPos) < StPetersWoods.SpawnClearingRadius)
                return false;
            if (Vector2.Distance(p, StPetersBuilder.DockZonePos) < StPetersWoods.DockClearance)
                return false;
            if (StPetersShoreMap.DistanceToSegment(
                    p, StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo) < StPetersWoods.DockClearance)
                return false;
            // The crossing is a WALKING line the player reads off the ground — weed over it would say
            // "do not cross" (StPetersShoreMap's own note about the bar spine being signage, not scenery).
            if (StPetersShoreMap.DistanceToSegment(
                    p, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo)
                < StPetersBuilder.SandbarHalfWidth)
                return false;
            return true;
        }
    }
}
#endif
