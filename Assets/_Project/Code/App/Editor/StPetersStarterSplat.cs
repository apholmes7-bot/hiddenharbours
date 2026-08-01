#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The STARTER splat paint for St Peters (owner request 2026-07-30) — a subtle, deterministic
    /// first pass the owner will repaint over with the Material brush, authored through the SAME
    /// stroke code the brush uses (<see cref="TerrainSplatBrush"/>), so what the menu lays down
    /// and what his hand lays down are one footprint math.
    ///
    /// <para><b>What it paints (with restraint — a starting point, not a finished ground):</b>
    /// a worn DIRT path from the village green to the slip, a second from the village to the bar
    /// head, SILT patches hugging the boat channel's edges on the flats, and a MARSH pocket in a
    /// sheltered NW hollow with a thin SEDGE fringe grading into the meadow.</para>
    ///
    /// <para><b>Every position derives from builder constants</b> (<see cref="StPetersBuilder"/>'s
    /// village/berth/bar/channel geometry — the island just shrank once already; a literal here
    /// would go stale the next time it moves) and all jitter is <see cref="StPetersShoreMap.Hash01"/>
    /// (no System.Random, no DateTime — rule 5). Re-running the menu reproduces the same paint
    /// bit-for-bit over whatever is there.</para>
    /// </summary>
    public static class StPetersStarterSplat
    {
        // --- Materials (canonical splat indices — TerrainSplatBrush.MaterialNames) -------------
        public const int Silt = 6;
        public const int Dirt = 7;
        public const int Marsh = 8;
        public const int Sedge = 9;

        // --- Stroke tunables (the owner's ask: subtle, low intensity) ---------------------------
        public const float PathWidthMetres = 2.5f;          // → brush radius 1.25
        public const float PathDabSpacingMetres = 0.75f;
        public const float PathFalloff = 0.5f;
        public const float SlipPathIntensity = 0.35f;       // village green → the slip
        public const float BarPathIntensity = 0.3f;         // village → the bar head
        public const float SiltIntensityMin = 0.3f;
        public const float SiltIntensityMax = 0.5f;
        public const float SiltRadiusMin = 3f;              // blobs 6–12 m across
        public const float SiltRadiusMax = 6f;
        public const float SiltFalloff = 0.65f;
        public const float MarshIntensity = 0.5f;
        public const float MarshRadiusMetres = 5f;          // a ~10 m pocket
        public const float MarshFalloff = 0.6f;
        public const float SedgeIntensity = 0.4f;
        public const float SedgeRadiusMetres = 3f;          // thin fringe dabs
        public const float SedgeFalloff = 0.8f;
        public const int SedgeFringeCount = 10;

        // Hash salts — one lane per feature so a tweak to one never re-rolls another.
        private const int SaltSlipPath = 61;
        private const int SaltBarPath = 62;
        private const int SaltSilt = 63;

        /// <summary>The marsh pocket's target ground: the middle of the upper sand band — just
        /// above the sand floor, below the marram line (upper intertidal, where a salt marsh
        /// actually sits). Derived from the classifier's own floors, never a literal.</summary>
        public static float MarshPocketElevation =>
            (StPetersShoreMap.SandFloorElevation + StPetersShoreMap.MarramFloorElevation) * 0.5f;

        // ============================ THE STROKE PLANS (pure — tested headless) =================

        /// <summary>
        /// The village-green → slip path: a gentle curve east across the plateau to the head of
        /// the dredged slip (<see cref="StPetersBuilder.BerthTo"/>, the shoreline end), with three
        /// hash-jittered bends so it reads as walked, not surveyed.
        /// </summary>
        public static Vector2[] VillageToSlipPath() =>
            BentPath(StPetersBuilder.VillageGreen,
                     new Vector2(StPetersBuilder.BerthTo.x, StPetersBuilder.BerthTo.y),
                     bends: 3, amplitudeMetres: 8f, salt: SaltSlipPath);

        /// <summary>The village → bar-head path (<see cref="StPetersBuilder.SandbarFrom"/> — where
        /// the low-tide walk leaves the island), two gentle bends.</summary>
        public static Vector2[] VillageToBarHeadPath() =>
            BentPath(StPetersBuilder.VillageGreen, StPetersBuilder.SandbarFrom,
                     bends: 2, amplitudeMetres: 5f, salt: SaltBarPath);

        /// <summary>A straight line bent at evenly-spaced interior points by a deterministic
        /// perpendicular offset — the "gentle curve, not a straight line" shape.</summary>
        public static Vector2[] BentPath(Vector2 from, Vector2 to, int bends, float amplitudeMetres, int salt)
        {
            var pts = new Vector2[bends + 2];
            pts[0] = from;
            pts[bends + 1] = to;
            Vector2 dir = (to - from).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            for (int i = 1; i <= bends; i++)
            {
                float t = i / (float)(bends + 1);
                float off = (StPetersShoreMap.Hash01(i, 0, salt) * 2f - 1f) * amplitudeMetres;
                pts[i] = Vector2.Lerp(from, to, t) + perp * off;
            }
            return pts;
        }

        /// <summary>One silt blob of the starter plan.</summary>
        public struct Blob
        {
            public Vector2 Center;
            public float Radius;
            public float Intensity;
        }

        /// <summary>Where the boat channel crosses the bar — the SAME lerp the terrain carves the
        /// gut with (<c>TidalTerrain.ElevationAtZones</c>), so the silt lands on the real feature.</summary>
        public static Vector2 ChannelCrossing() =>
            Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo,
                         StPetersBuilder.ChannelAlong);

        /// <summary>
        /// Silt patches on the flats flanking the boat channel: three blobs per side, each pushed
        /// past the channel's half-width along the bar (so they HUG the gut's edges rather than
        /// sit in it) and spread across the bar's width, all sized/placed/weighted by hash.
        /// </summary>
        public static Blob[] SiltBlobs()
        {
            Vector2 crossing = ChannelCrossing();
            Vector2 barDir = (StPetersBuilder.SandbarTo - StPetersBuilder.SandbarFrom).normalized;
            Vector2 perp = new Vector2(-barDir.y, barDir.x);
            float acrossMax = StPetersBuilder.SandbarHalfWidth - 16f;   // stay on the flats, off the deep edge

            var blobs = new Blob[6];
            int n = 0;
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                int saltSide = SaltSilt + (side > 0 ? 0 : 1);
                float radius = Mathf.Lerp(SiltRadiusMin, SiltRadiusMax,
                                          StPetersShoreMap.Hash01(i, 0, saltSide));
                float along = side * (StPetersBuilder.ChannelHalfWidth + radius + 2f
                                      + StPetersShoreMap.Hash01(i, 1, saltSide) * 6f);
                float across = Mathf.Lerp(-acrossMax, acrossMax,
                                          StPetersShoreMap.Hash01(i, 2, saltSide));
                blobs[n++] = new Blob
                {
                    Center = crossing + barDir * along + perp * across,
                    Radius = radius,
                    Intensity = Mathf.Lerp(SiltIntensityMin, SiltIntensityMax,
                                           StPetersShoreMap.Hash01(i, 3, saltSide)),
                };
            }
            return blobs;
        }

        /// <summary>
        /// Find the marsh pocket: march NORTH-WEST from the island centre — the sheltered side
        /// (the weather coast faces <see cref="StPetersShoreMap.WeatherCoastFacing"/>, SE) — until
        /// the authored ground first drops to <see cref="MarshPocketElevation"/>. Terrain-derived,
        /// so it keeps finding the hollow if the island is resized again.
        /// </summary>
        public static Vector2 FindMarshPocket(Func<Vector2, float> elevationAt)
        {
            Vector2 dir = new Vector2(-1f, 1f).normalized;   // NW — opposite the weather sector
            Vector2 origin = StPetersBuilder.IslandCenter;
            for (float t = 0f; t <= 400f; t += 0.5f)
            {
                Vector2 pos = origin + dir * t;
                if (elevationAt(pos) <= MarshPocketElevation) return pos;
            }
            return origin;   // degenerate terrain — callers treat centre as "not found"
        }

        /// <summary>The sedge fringe: a thin ring of dab centres around the marsh pocket, just
        /// outside its rim, grading it into the meadow.</summary>
        public static Vector2[] SedgeFringe(Vector2 marshCenter)
        {
            var pts = new Vector2[SedgeFringeCount];
            float ringRadius = MarshRadiusMetres + 2f;
            for (int i = 0; i < SedgeFringeCount; i++)
            {
                float ang = i * (2f * Mathf.PI / SedgeFringeCount);
                pts[i] = marshCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ringRadius;
            }
            return pts;
        }

        // ============================ THE MENU / BATCH ENTRY ====================================

        [MenuItem("Hidden Harbours/Tools/Paint St Peters Starter Splat", priority = 41)]
        public static void PaintMenu()
        {
            if (Paint())
                Debug.Log("[StPetersStarterSplat] Starter splat painted. Open the Terrain Paint " +
                          "Tool's Material brush to repaint it your way, or rebuild St Peters to " +
                          "see it wired in the scene.");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> (the seabed re-bake's pattern):
        /// paints the starter splat headlessly, exiting nonzero on failure.</summary>
        public static void PaintStarterSplatFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (!Paint()) EditorApplication.Exit(1);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogError("[StPetersStarterSplat] (batch) starter paint threw: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Author the starter pass into the splat PNGs (creating them blank if absent) via
        /// the shared stroke code, then commit with the linear-data importer. Deterministic and
        /// re-runnable: identical inputs paint identical pixels over whatever is there.
        /// </summary>
        public static bool Paint()
        {
            RegionDef region = TerrainPaintTool.DefaultRegion();
            if (region == null || !region.HasUsableExtent)
            {
                Debug.LogError("[StPetersStarterSplat] region.st_peters missing or has an unusable " +
                               "extent — nothing to size the splat maps from.");
                return false;
            }

            Vector2 worldSize = region.WorldSizeMeters;
            Vector2 worldMin = region.WorldCenter - worldSize * 0.5f;
            Vector2Int texels = region.SeabedTexels;

            var textures = new Texture2D[TerrainSplatBrush.TextureCount];
            var pixels = new Color[TerrainSplatBrush.TextureCount][];
            if (!TerrainSplatAssets.LoadOrCreate(texels, textures, pixels)) return false;
            int w = textures[0].width, h = textures[0].height;

            // The authored terrain the marsh finder reads — a transient TidalTerrain configured
            // with the canon St Peters zones (the BakeStPetersSeabed pattern), discarded after.
            var go = EditorUtility.CreateGameObjectWithHideFlags("~StarterSplat", HideFlags.HideAndDontSave,
                                                                 typeof(TidalTerrain));
            var terrain = go.GetComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);

            try
            {
                float pathRadius = PathWidthMetres * 0.5f;

                // 1) The dirt paths — the green to the slip, the green to the bar head.
                TerrainSplatBrush.PaintPolyline(pixels, w, h, worldMin, worldSize,
                    VillageToSlipPath(), PathDabSpacingMetres, pathRadius, PathFalloff,
                    Dirt, SlipPathIntensity, exclusive: true);
                TerrainSplatBrush.PaintPolyline(pixels, w, h, worldMin, worldSize,
                    VillageToBarHeadPath(), PathDabSpacingMetres, pathRadius, PathFalloff,
                    Dirt, BarPathIntensity, exclusive: true);

                // 2) Silt hugging the boat channel's edges on the flats.
                foreach (Blob blob in SiltBlobs())
                    TerrainSplatBrush.Dab(pixels, w, h, worldMin, worldSize, blob.Center,
                        blob.Radius, SiltFalloff, Silt, blob.Intensity, 1f, exclusive: true);

                // 3) The marsh pocket in the sheltered NW hollow + its sedge fringe (fringe
                //    second, exclusive, so it eats the pocket's rim into a grade).
                Vector2 marsh = FindMarshPocket(terrain.ElevationAtZones);
                if (marsh != StPetersBuilder.IslandCenter)
                {
                    TerrainSplatBrush.Dab(pixels, w, h, worldMin, worldSize, marsh,
                        MarshRadiusMetres, MarshFalloff, Marsh, MarshIntensity, 1f, exclusive: true);
                    foreach (Vector2 p in SedgeFringe(marsh))
                        TerrainSplatBrush.Dab(pixels, w, h, worldMin, worldSize, p,
                            SedgeRadiusMetres, SedgeFalloff, Sedge, SedgeIntensity, 1f, exclusive: true);
                }
                else Debug.LogWarning("[StPetersStarterSplat] no NW hollow at the marsh elevation — " +
                                      "marsh + sedge skipped.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            TerrainSplatAssets.Commit(textures, pixels);

            // Re-feed any open splat surface from the RELOADED assets (Commit reimported them —
            // the old in-memory references are invalid; wire only the fresh loads).
            foreach (var s in Object.FindObjectsByType<HiddenHarbours.Art.TerrainSplatSurface>(FindObjectsSortMode.None))
            {
                s.ConfigureSplat(textures[0], textures[1], textures[2], textures[3]);
                if (s.isActiveAndEnabled) { s.enabled = false; s.enabled = true; }   // OnEnable → MPB push
            }

            Debug.Log($"[StPetersStarterSplat] painted the starter pass into {TerrainSplatAssets.PathOf(0)} " +
                      $"/B/C/D at {w} × {h} texels: dirt green→slip ({SlipPathIntensity:0.##}) and " +
                      $"green→bar head ({BarPathIntensity:0.##}), {SiltBlobs().Length} silt blobs at the " +
                      "channel, a marsh pocket + sedge fringe NW. Subtle by design — repaint it with " +
                      "the Material brush.");
            return true;
        }
    }
}
#endif
