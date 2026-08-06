// CLIFF WALL PROOF HARNESS — editor-only evidence tooling, never shipped in a build.
//
// Answers the two questions the cliff-face kit's own README asks of an engine and no other tool in
// this repo can answer: (1) is the wall GEOMETRY rather than a plane — does the profile displacement
// actually reach the silhouette? and (2) does a face LIVE-LIT by the project's own sun read as rock
// across the whole 24 h cycle, without collapsing to black before ADR 0013's overlay reaches it?
//
// ⭐ IT RASTERISES ON THE CPU, THROUGH CliffLightMath. That is not a compromise, it is the point.
// CliffLightMath is the shader's TESTED twin (its terms are pinned by CliffLightMathTests), so a
// sheet drawn through it shows the same answer the GPU will and it draws on a runner with no
// graphics device — which is where this project's rendering proofs keep going dark. A GPU capture of
// the real scene is the owner's own eyeballs' job and is deliberately not attempted here.
//
// Deterministic throughout (rule 5): the region's own authored coast, fixed hours, no RNG, no wall
// clock. Sheets land in docs/art/proofs/ (the house convention: flat, *-csharp.png, no metas) and
// every number is written to the log beside them.
//
// ⚠ WHY THIS LIVES UNDER App/Editor AND NOT Tools/Editor/. ShoreSeamProof is the STRUCTURAL
// precedent (menu + dedicated renderer + evidence + log) and this follows it. But the proof needs
// the region's coast plan and the cliff catalog, and HiddenHarbours.Tools.Editor references neither
// — widening that shared assembly so one proof can reach App.Editor and Art.Editor is exactly the
// coupling rule 4 exists to prevent. App.Editor already sees everything this needs.
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    public static class CliffProofMenu
    {
        /// <summary>The house's proof folder — flat, repo-root-relative, outside Assets so no metas.</summary>
        static string OutDir => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "docs/art/proofs");

        /// <summary>The hours the day-cycle strip is drawn at: night, dawn, morning, noon, afternoon,
        /// dusk, night again. Chosen to straddle every branch in the shade model — sun below the
        /// horizon (the <c>SkyFloor</c> case), sun on the horizon, and sun at its own height.</summary>
        static readonly float[] Hours = { 2f, 6f, 9f, 13f, 17f, 20f, 23f };

        const float Sunrise = 6f, Sunset = 20f, ShadowSouthBias = 0.2f, ShadowNoonLift = 0.9f;

        /// <summary>Pixels per metre the sheets are drawn at. Deliberately NOT the kit's 32: these are
        /// diagrams of a whole cliff run, not texture proofs, and 12 m of coast at 32 px/m would not
        /// fit a sheet anyone will look at.</summary>
        const int Ppm = 24;

        [MenuItem("Hidden Harbours/Dev/Cliff Walls — proof sheets", priority = 149)]
        public static void RunAll()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Directory.CreateDirectory(OutDir);
            var log = new StringBuilder();

            var go = new GameObject("CliffProofTerrain");
            try
            {
                var terrain = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);

                var chunks = StPetersCliffWalls.ResolveChunks(terrain);
                log.AppendLine($"coast: {chunks.Count} chunks resolved from the authored plan");
                if (chunks.Count == 0)
                {
                    Debug.LogError("[cliff-proof] the coast resolved to no wall at all — nothing to prove.");
                    return;
                }

                // The tallest, plainest run: due south, the sector the owner's complaint is about.
                var subject = PickRun(chunks, 180f, out float subjectBearing);
                log.AppendLine($"subject: a {subject.Count}-station run near bearing {subjectBearing:F1}°, " +
                               $"drop {subject[0].DropMetres:F2} m, " +
                               $"batter {CliffWallGeometry.BatterDegrees(subject[0]):F1}°, " +
                               $"surface {CliffWallGeometry.SurfaceLengthMetres(subject[0]):F2} m");

                Texture2D profile = LoadProfile(subject[0]);
                log.AppendLine(profile != null
                    ? $"profile: {profile.name} {profile.width}x{profile.height}, readable"
                    : "profile: ABSENT — the form sheet will show the plane on both halves. The kit " +
                      "ships as the rig; run the region builder or the bake menu.");

                log.AppendLine(CliffProofRenderer.FormVsPlane(
                    subject, profile, Ppm, Path.Combine(OutDir, "cliff-form-vs-plane-csharp.png")));
                log.AppendLine(CliffProofRenderer.DayCycle(
                    subject, profile, Ppm, Hours, Sunrise, Sunset, ShadowSouthBias, ShadowNoonLift,
                    Path.Combine(OutDir, "cliff-daycycle-csharp.png")));

                File.WriteAllText(Path.Combine(OutDir, "cliff-proof-log.txt"), log.ToString());
                Debug.Log($"[cliff-proof] sheets written to docs/art/proofs/\n{log}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>The chunk nearest a bearing, as a plain sample list — the run the sheets are drawn
        /// of. Nearest rather than "the first cliff" so the subject is a stated place on the island
        /// rather than whichever run the loop happened to emit first.</summary>
        static System.Collections.Generic.List<CliffWallSample> PickRun(
            System.Collections.Generic.List<StPetersCliffWalls.Chunk> chunks, float bearing,
            out float actualBearing)
        {
            Vector2 target = StPetersCliffWalls.ShorePoint(bearing);
            var best = chunks[0];
            float bestDistance = float.MaxValue;
            foreach (StPetersCliffWalls.Chunk c in chunks)
            {
                float d = Vector2.Distance(c.Samples[0].BrowPlan, target);
                if (d < bestDistance) { bestDistance = d; best = c; }
            }
            actualBearing = best.WallAzimuth;
            return best.Samples;
        }

        /// <summary>The displacement map for a face of this batter, if the checkout carries a bake.
        /// Null is a legitimate answer (the sheets say so) — the PNGs are gitignored by design.</summary>
        static Texture2D LoadProfile(CliffWallSample sample)
        {
            int baked = CliffWallGeometry.SnapBatterIndex(
                CliffWallGeometry.BatterDegrees(in sample), StPetersCliffWalls.BakedBatterAngles());
            int catalogBatter = StPetersCliffWalls.BakedBatterToCatalogIndex(baked);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{HiddenHarbours.Tools.RigBaking.CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Profile)}/" +
                $"{CliffCatalog.ProfileName(StPetersCliffWalls.Rock, catalogBatter)}.png");
        }
    }
}
