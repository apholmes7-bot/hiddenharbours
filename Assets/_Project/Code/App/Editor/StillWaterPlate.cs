using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Terrain pass 9, PR 4's plate (ADR 0046 §10): still water above the tide, photographed in the engine — what
    /// the pass-9 preview composite draws, a pond and a brook's fresh reach standing over the tide, shown by the
    /// shipped water shader. No region has a still map before PR 5, so the plate makes its own: a scratch
    /// 48 × 48 m scene with two ponds and a stepped brook running from pond A's outlet down a slope to the tide,
    /// from an R16 height map and an R16 still map built here. It commits nothing and saves nothing — the scratch
    /// scene is never saved, the maps live only in memory, and the scenes open before it are reopened — and it
    /// writes its evidence (PNGs and a manifest) to <c>Evidence~/still-water-plate/</c> in the project folder.
    ///
    /// <para><b>What it shows.</b> The ponds at low, mid and high tide (they stand above every tide); the brook's
    /// fresh reach meeting the tide at its head; and the wade model — the middle of a pond deeper than
    /// <c>WadeDepth</c> is swum. The shots are the painted sea with the still map registered and without it (the
    /// ponds and the brook must appear only with it), a working sea (sea state 0.55) at high tide, and the walker's
    /// own depth bands (<see cref="TidalWalkability"/>), tide water blue and fresh water green.</para>
    ///
    /// <para><b>Two seas.</b> Sea 1 is on the painted path (<see cref="WaterSurface.ConfigurePaintedHeightMap"/>):
    /// the shader reads the plate's R16 height map itself, at its 0.25 m texels. Sea 2 is on Auto, as every
    /// committed region's sea is: it bakes the registered terrain at 16² over the 48 m frame — 3 m cells, about
    /// St Peters' density (256² over its ~760 × 520 m is 2.97 × 2.03 m). Sea 2 is diagnostic, for PR 5, and is
    /// not judged: a 1.2 m brook falls between 3 m samples.</para>
    ///
    /// <para><b>The expected values are literals</b>, each probe with its hand arithmetic, derived for
    /// GameConfig's WadeDepth 0.5 and SwimLimit 2.0. The plate refuses to run if either has moved, rather than
    /// judge against a bar it did not derive; it never asks the code under test for its bar.</para>
    ///
    /// <para><b>Hard checks</b> (any one fails the plate): the maps decode to the plate's levels; the walker's
    /// band at every probe and tide; sea 1 is on the painted path and sea 2 bakes the plate's terrain; the shaders
    /// finish compiling before a shot; each shot's pushed <c>_WaterLevel</c> is its tide and its
    /// <c>_HHStillRange.z</c> says whether a still map is registered; sea 1 draws both ponds with the still map,
    /// and neither pond nor the brook without it; no shot draws water on the plateau. <b>Soft checks</b>
    /// (warnings, for the owner's eye): the brook drawn with the still map; the drawn water against the walker's
    /// (±0.10 m); a working sea spilling past a pond's rim. The plate is judged on its pictures.</para>
    ///
    /// <para><b>Running it.</b> Hidden Harbours ▸ Dev ▸ Still Water Plate, or in batch on a machine with a GPU:
    /// <c>-batchmode -projectPath &lt;project&gt; -executeMethod HiddenHarbours.App.Editor.StillWaterPlate.RunBatch</c>
    /// without <c>-quit</c> (it exits itself: 0 on a pass, 1 otherwise). It refuses with no graphics device
    /// (<c>-nographics</c>, CI), on a device that cannot sample R16, and while a loaded scene has unsaved changes
    /// that a batch run may not save, or that the owner declines to save at the menu's prompt.</para>
    /// </summary>
    public static class StillWaterPlate
    {
        public const string MenuPath = "Hidden Harbours/Dev/Still Water Plate";
        private const string Tag = "[StillWaterPlate]";

        // The frame and its maps: 48 m square on the origin; R16 at 0.25 m texels over −4 … +7 m (ADR 0046 §8).
        private const float Frame = 48f;
        private const int MapRes = 192;
        private const float MapMin = -4f, MapMax = 7f;
        // Sea 1 is configured as a land region's sea is, then put on the painted path; sea 2 bakes 16² (3 m cells).
        private const int SeaBakeRes = 256, CoarseBakeRes = 16;

        // Pictures: 960² shots at 0.05 m a pixel, 320² tiles, a 240² ground at 0.2 m, 8 px gutters on the sheet.
        private const int ShotPx = 960, ThumbPx = 320, GroundPx = 240, BandPx = ThumbPx, Gap = 8;
        private const int GroundScale = ShotPx / GroundPx;
        private const int SheetRows = 4, SheetCols = 3;
        private const float PxMetres = Frame / ShotPx;

        private const float Noon = 12f;
        private const float BlowSea01 = 0.55f;   // the water plate sweep's "Blow"

        // Thresholds on the max-channel |raw − ground| of the camera's linear values.
        private const float DrawnThreshold = 0.01f;   // a pixel counts as drawn water
        private const float DryTolerance = 0.006f;    // a 5 × 5 patch reads as bare ground
        private const float PondDrawnMin = 0.02f;     // a pond's centre patch reads as water
        private const float PushTolerance = 1e-3f;    // the pushed _WaterLevel against the tide asked for
        private const float WetMargin = 0.10f;        // the walker's depth either side of zero, for the wet-mask check
        // Half a square metre: less than a metre of the 1.2 m brook, more than a waterline antialiased over a pixel.
        private const float WetMismatchWarnM2 = 0.5f;

        // The wade model the probes' expected bands were derived for (GameConfig.WadeDepth / SwimLimit).
        private const float WadeExpected = 0.5f, SwimExpected = 2.0f;

        private const string WaterMaterialPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string SeaTilePath = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        private const string Sea1 = "sea1", Sea2 = "sea2";

        // Spring low, mean, the bar crest, spring high.
        private static readonly float[] Tides = { -2.2f, 0f, 0.88f, 2.2f };
        private static readonly float[] ShotTides = { -2.2f, 0f, 2.2f };
        private static readonly float[] BandTides = { -2.2f, 0.88f, 2.2f };

        private static readonly Vector2 FrameSize = new Vector2(Frame, Frame);
        private static readonly Vector2 WindHeading = new Vector2(6f, -5.3f).normalized;   // the sweep's
        private static readonly Vector2 PondACentre = new Vector2(-8f, 11f);
        private static readonly Vector2 PondBCentre = new Vector2(9f, 13f);
        private static readonly Vector2 BrookMid = new Vector2((float)Geometry.BrookX(-5.0), -5f);
        private static readonly Vector2 PlateauNorth = new Vector2(0f, 20f);
        private static readonly Vector2 PlateauEast = new Vector2(16f, 2f);
        private static readonly IReadOnlyList<IStandableSurface> NoSurfaces = Array.Empty<IStandableSurface>();

        // Read back by the names the shaders read, not through the C# that publishes them.
        private static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");
        private static readonly int IdSunDir = Shader.PropertyToID("_SunDir");
        private static readonly int IdSunElevation = Shader.PropertyToID("_SunElevation");
        private static readonly int IdMoonDir = Shader.PropertyToID("_MoonDir");
        private static readonly int IdMoonPhaseState = Shader.PropertyToID("_MoonPhaseState");
        private static readonly int IdReflectTex = Shader.PropertyToID("_HHReflectTex");
        private static readonly int IdHeightTex = Shader.PropertyToID("_HeightTex");
        private static readonly int IdWaterLevel = Shader.PropertyToID("_WaterLevel");
        private static readonly int IdStillRange = Shader.PropertyToID("_HHStillRange");
        private static readonly int IdStillRect = Shader.PropertyToID("_HHStillRect");

        // The ground, flat colours with a light stripe every 0.5 m of height so the contours read.
        private static readonly Color Mud = new Color(0.33f, 0.26f, 0.18f);     // carved: pond beds, the brook's trough
        private static readonly Color Sand = new Color(0.76f, 0.68f, 0.48f);    // below the spring high tide
        private static readonly Color Grass = new Color(0.36f, 0.52f, 0.26f);
        private static readonly Color Bog = new Color(0.45f, 0.42f, 0.28f);     // the plateau above +5.5
        // The walker's bands: tide water blue, fresh water green, shallow to deep.
        private static readonly Color BandDry = new Color(0.80f, 0.78f, 0.70f);
        private static readonly Color TideWade = new Color(0.55f, 0.75f, 0.90f);
        private static readonly Color TideSwim = new Color(0.20f, 0.45f, 0.75f);
        private static readonly Color TideDeep = new Color(0.05f, 0.15f, 0.40f);
        private static readonly Color FreshWade = new Color(0.55f, 0.90f, 0.60f);
        private static readonly Color FreshSwim = new Color(0.15f, 0.65f, 0.35f);
        private static readonly Color FreshDeep = new Color(0.05f, 0.35f, 0.15f);
        private static readonly Color Gutter = new Color(0.08f, 0.08f, 0.09f);
        private static readonly Color Blank = new Color(0.5f, 0.5f, 0.5f);

        // sheet.png, top row first; a key is the stem of a shot or band map, and a missing one is a grey tile.
        private static readonly string[,] SheetLayout =
        {
            { "sea1_glass_still_tide-2.20", "sea1_glass_still_tide+0.00", "sea1_glass_still_tide+2.20" },
            { "sea1_glass_nostill_tide-2.20", "sea1_glass_nostill_tide+0.00", "sea1_glass_nostill_tide+2.20" },
            { "sea1_blow_still_tide+2.20", "bands_tide-2.20", "bands_tide+2.20" },
            { "sea2_glass_still_tide-2.20", "sea2_glass_still_tide+2.20", "bands_tide+0.88" },
        };

        [MenuItem(MenuPath, priority = 102)]
        private static void RunMenu()
        {
            try
            {
                Run(true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Still Water Plate", "The plate threw: " + e.Message + "\nSee the Console.", "OK");
            }
        }

        /// <summary>The batch entry (<c>-executeMethod</c>): exits 0 on a pass and 1 on a failure or a refusal.</summary>
        public static void RunBatch()
        {
            bool pass = false;
            try
            {
                pass = Run(false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(pass ? 0 : 1);
        }

        /// <summary>Lay the plate, photograph it, check it, and put the editor back. True on a pass.</summary>
        public static bool Run(bool interactive)
        {
            string refusal = Preflight(out Kit kit) ?? ClearTheScenes(interactive);
            if (refusal != null)
            {
                Debug.LogError(Tag + " REFUSED: " + refusal);
                if (interactive) EditorUtility.DisplayDialog("Still Water Plate", "Refused: " + refusal, "OK");
                return false;
            }

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var session = new Session(kit, interactive);
            try
            {
                return session.Photograph();
            }
            finally
            {
                session.TearDown();
                RestoreScenes(setup);
            }
        }

        // ---- before anything is laid --------------------------------------------------------------------------

        private static string Preflight(out Kit kit)
        {
            kit = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "the editor is in Play mode, or entering it: the plate lays a scratch scene in edit mode";
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return "there is no graphics device (-nographics, or CI): the plate is pictures and needs a GPU";
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.R16))
                return "this device cannot sample an R16 texture, and the plate's maps are R16 (ADR 0046 §8)";

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(WaterSceneTemplate.GameConfigAsset);
            if (config == null) return "GameConfig was not found at " + WaterSceneTemplate.GameConfigAsset;
            if (Mathf.Abs(config.WadeDepth - WadeExpected) > 1e-6f || Mathf.Abs(config.SwimLimit - SwimExpected) > 1e-6f)
                return I($"GameConfig's wade model is WadeDepth {config.WadeDepth}, SwimLimit {config.SwimLimit}, and the probes' expected bands were derived for {WadeExpected} and {SwimExpected}: re-derive the probes' expected bands from their arithmetic first");

            MethodInfo push = typeof(WaterSurface).GetMethod("PushUniforms", BindingFlags.Instance | BindingFlags.NonPublic,
                                                             null, Type.EmptyTypes, null);
            if (push == null) return "WaterSurface.PushUniforms() was not found: the plate cannot snap the sea's uniforms to a shot";

            var water = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (water == null) return "the water material is missing at " + WaterMaterialPath;
            Sprite tile = WaterSceneTemplate.LoadSpriteAny(SeaTilePath);
            if (tile == null)
                return "the sea tile did not load from " + SeaTilePath + " (an LFS pointer? `git lfs checkout " + SeaTilePath +
                       "`): without it ConfigureSeaPlane leaves the sea undrawn";
            string unloaded = FirstUnloadedTexture(WaterMaterialPath, WaterSceneTemplate.ArtWaterOverlayMat);
            if (unloaded != null)
                return "the water's texture " + unloaded + " did not load (an LFS pointer? `git lfs checkout " + unloaded +
                       "`): the sea would be drawn without it";

            Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            if (unlit == null) return "no unlit sprite shader was found to draw the ground";

            kit = new Kit { Config = config, Push = push, WaterMaterial = water, SeaTile = tile, Unlit = unlit };
            return null;
        }

        private static string FirstUnloadedTexture(params string[] assets)
        {
            foreach (string path in AssetDatabase.GetDependencies(assets, true))
            {
                switch (System.IO.Path.GetExtension(path).ToLowerInvariant())
                {
                    case ".png": case ".tga": case ".psd": case ".exr": case ".jpg": case ".tif": case ".tiff":
                        if (AssetDatabase.LoadAssetAtPath<Texture>(path) == null) return path;
                        break;
                }
            }
            return null;
        }

        /// <summary>The plate replaces the open scenes with a scratch one, so their unsaved changes are the owner's
        /// to save first (the menu asks); a batch run never saves or discards them, so it refuses.</summary>
        private static string ClearTheScenes(bool interactive)
        {
            if (interactive)
                return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() ? null : "cancelled at the save prompt";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                    return "a loaded scene has unsaved changes (" + (string.IsNullOrEmpty(scene.path) ? scene.name : scene.path) +
                           "), and a batch run never saves or discards them";
            }
            return null;
        }

        private static void RestoreScenes(SceneSetup[] setup)
        {
            bool restorable = setup != null && setup.Length > 0;
            if (restorable)
            {
                foreach (SceneSetup scene in setup)
                {
                    if (string.IsNullOrEmpty(scene.path)) { restorable = false; break; }
                }
            }
            if (restorable)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                return;
            }
            Debug.Log(Tag + " the scenes open before the plate had no saved path to reopen; the empty scratch scene " +
                      "stays open, unsaved.");
        }

        private static string I(FormattableString s) => FormattableString.Invariant(s);

        private static string Pct(int part, int whole)
            => whole == 0 ? "n/a" : (100.0 * part / whole).ToString("F0", CultureInfo.InvariantCulture) + "%";

        // ---- the walker's probes --------------------------------------------------------------------------------

        /// <summary>Each probe's expected band at each of <see cref="Tides"/>, from the hand arithmetic beside it
        /// (the plate's geometry is <see cref="Geometry"/>; WadeDepth 0.5, SwimLimit 2.0).</summary>
        private static Probe[] BuildProbes()
        {
            const DepthBand Dry = DepthBand.Dry, Wade = DepthBand.Wade, Swim = DepthBand.Swim, Deep = DepthBand.Deep;
            Vector2 Brook(double y, double offset = 0.0) => new Vector2((float)(Geometry.BrookX(y) + offset), (float)y);
            return new[]
            {
                new Probe("pond A centre", new Vector2(-8f, 11f),
                          "still 5.30 over bed 4.65: 0.65 m, over the 0.5 m wade limit, so swum at every tide", Every(Swim)),
                new Probe("pond A wade ring (r 0.8)", new Vector2(-8f + 0.8f * 2.6f, 11f),
                          "bed 4.65 + 0.65 × 0.8² = 5.066 under still 5.30: 0.234 m, waded", Every(Wade)),
                new Probe("pond A bank (r 1.35)", new Vector2(-8f + 1.35f * 2.6f, 11f),
                          "bank 5.30 + (5.8375 − 5.30) × 0.784 = 5.721, and no still water past r 1.2: dry", Every(Dry)),
                new Probe("pond B centre", new Vector2(9f, 13f),
                          "still 5.25 over bed 4.70: 0.55 m, swum", Every(Swim)),
                new Probe("brook y +4", Brook(4.0),
                          "bed 5.16 − 0.02 × 5.35 = 5.053, still 5.193: 0.14 m, waded", Every(Wade)),
                new Probe("brook y -5", Brook(-5.0),
                          "ground 2.2 + 5 × 0.35 = 3.95, bed 3.95 − 0.4 = 3.55, still 3.69: 0.14 m, waded; no tide reaches it",
                          Every(Wade)),
                new Probe("brook y -8.5 (above the head of tide)", Brook(-8.5),
                          "ground 2.725, bed 2.325, still 2.465: 0.14 m, waded; at +2.2 the fresh water stands over the tide",
                          Every(Wade)),
                new Probe("brook y -8.75 (at the head of tide)", Brook(-8.75),
                          "bed 2.2375; a texel upstream the bed is 2.325 ≥ 2.2, so the reach holds its level: still 2.3775, 0.14 m, waded",
                          Every(Wade)),
                new Probe("brook y -9.5 (below the head)", Brook(-9.5),
                          "bed 1.975; a texel upstream the bed is 2.0625 < 2.2, so no still water: dry until the tide wades it at +2.2 (0.225 m)",
                          new[] { Dry, Dry, Dry, Wade }),
                new Probe("beach (10, -16)", new Vector2(10f, -16f),
                          "ground −3 + 8 × 5.2/14 = −0.029: dry at −2.2, 0.029 m at 0, 0.909 m at +0.88, 2.229 m at +2.2",
                          new[] { Dry, Wade, Swim, Deep }),
                new Probe("brook bank (1.5 m off the line, y -5)", Brook(-5.0, 1.5),
                          "the trough 3.55 + 0.9 = 4.45 stands above the ground 3.95, and 1.5 m is past the still water's 1.1 m: dry",
                          Every(Dry)),
                new Probe("plateau north (0, 20)", new Vector2(0f, 20f), "ground 5.7 + 20 × 0.0125 = 5.95: dry", Every(Dry)),
                new Probe("plateau east (16, 2)", new Vector2(16f, 2f), "ground 5.7 + 2 × 0.0125 = 5.725: dry", Every(Dry)),
            };
        }

        private static DepthBand[] Every(DepthBand band) => new[] { band, band, band, band };

        private readonly struct Probe
        {
            public readonly string Name, Arithmetic;
            public readonly Vector2 Position;
            public readonly DepthBand[] Expected;   // at Tides: −2.2, 0, +0.88, +2.2

            public Probe(string name, Vector2 position, string arithmetic, DepthBand[] expected)
            {
                Name = name;
                Position = position;
                Arithmetic = arithmetic;
                Expected = expected;
            }
        }

        // ---- the plate's ground and water, by hand ------------------------------------------------------------

        /// <summary>
        /// The scratch frame, on the pass-9 design (§4) at the frame's scale. Ground: a beach from −3.0 at the south
        /// edge to +2.2 at y −10, a 0.35 slope to +5.7 at y 0, a plateau rising to +6.0 at the north edge. Pond A is
        /// the design's Bog Pond (5.2 × 3.3 m, +5.30 over a +4.65 bed), pond B its Fen Pool (6.5 × 4.0 m at −8°,
        /// +5.25 over +4.70): a bowl to the rim, a smooth bank out to r 1.5, and the still level held to r 1.2, where
        /// the bank already stands above it. The brook leaves pond A's south rim (y +9.35) and meanders down the
        /// slope, 1.2 m wide with 0.14 m of water; its bed is the lower of a 0.02 fall a metre and the ground less
        /// 0.4 m, so it never climbs downstream, and its fresh water is written to one texel past the head of tide —
        /// where the bed drops below +2.2 — so the filtered level holds to the head and meets the tide there, not a
        /// dry riffle (ADR 0046 §5). Doubles throughout; the maps round once, as the tool's writer does.
        /// </summary>
        private static class Geometry
        {
            public const double Cell = (double)Frame / MapRes;   // 0.25 m: the maps' texel
            public const double HeadOfTide = 2.2;                 // the spring high tide
            public const double BrookOutY = 9.35;                 // pond A's south rim, 11 − 1.65: the outlet
            public const double BrookDepth = 0.14, BrookHalfW = 0.6, BrookStillHalfW = 1.1;
            public const double PondStillR = 1.2;

            public static readonly Pond PondA = new Pond(-8.0, 11.0, 2.6, 1.65, 0.0, 5.30, 4.65);
            public static readonly Pond PondB = new Pond(9.0, 13.0, 3.25, 2.0, -8.0, 5.25, 4.70);

            public static double Ground(double y)
            {
                if (y <= -10.0) return -3.0 + (y + 24.0) * (5.2 / 14.0);
                if (y <= 0.0) return 2.2 + (y + 10.0) * 0.35;
                return 5.7 + y * (0.3 / 24.0);
            }

            public static double BrookX(double y) => -8.0 + 1.2 * Math.Sin(Math.PI * (BrookOutY - y) / 12.0);

            public static double BrookBed(double y)
            {
                if (y > BrookOutY + 1.0) return double.PositiveInfinity;
                double upstream = Math.Min(y, BrookOutY);
                return Math.Min(5.16 - 0.02 * (BrookOutY - upstream), Ground(y) - 0.4);
            }

            private static double Trough(double x, double y)
            {
                double bed = BrookBed(y);
                if (double.IsPositiveInfinity(bed)) return double.PositiveInfinity;
                return bed + Math.Max(0.0, Math.Abs(x - BrookX(y)) - BrookHalfW);
            }

            private static double BrookStill(double x, double y)
            {
                double bed = BrookBed(y);
                if (double.IsPositiveInfinity(bed) || BrookBed(y + Cell) < HeadOfTide ||
                    Math.Abs(x - BrookX(y)) > BrookStillHalfW)
                    return double.NegativeInfinity;
                return bed + BrookDepth;
            }

            public static double Terrain(double x, double y)
                => Math.Min(Math.Min(Ground(y), PondA.Surface(x, y)), Math.Min(PondB.Surface(x, y), Trough(x, y)));

            public static double Still(double x, double y)
                => Math.Max(Math.Max(PondA.Still(x, y), PondB.Still(x, y)), BrookStill(x, y));
        }

        private readonly struct Pond
        {
            public readonly double Cx, Cy, A, B, Rot, Level, Bed;

            public Pond(double cx, double cy, double a, double b, double rotDeg, double level, double bed)
            {
                Cx = cx;
                Cy = cy;
                A = a;
                B = b;
                Rot = rotDeg * (Math.PI / 180.0);
                Level = level;
                Bed = bed;
            }

            /// <summary>The normalised elliptical radius: 1 at the rim.</summary>
            public double R(double x, double y)
            {
                double dx = x - Cx, dy = y - Cy, c = Math.Cos(Rot), s = Math.Sin(Rot);
                double lx = dx * c + dy * s, ly = -dx * s + dy * c;
                return Math.Sqrt((lx / A) * (lx / A) + (ly / B) * (ly / B));
            }

            public double Surface(double x, double y)
            {
                double r = R(x, y);
                if (r <= 1.0) return Bed + (Level - Bed) * r * r;
                if (r >= 1.5) return double.PositiveInfinity;
                double t = Math.Min(Math.Max((r - 1.0) / 0.5, 0.0), 1.0);
                return Level + (Geometry.Ground(y) - Level) * t * t * (3 - 2 * t);
            }

            public double Still(double x, double y) => R(x, y) <= Geometry.PondStillR ? Level : double.NegativeInfinity;
        }

        // ---- the session ----------------------------------------------------------------------------------------

        private sealed class Kit
        {
            public GameConfig Config;
            public MethodInfo Push;
            public Material WaterMaterial;
            public Sprite SeaTile;
            public Shader Unlit;
        }

        private sealed class Shot
        {
            public string Stem, Sea, Baked;
            public float Sea01, Tide, Pushed;
            public bool WithStill;
            public Vector4 StillRange, StillRect;
            public bool[] Drawn;   // ShotPx², max-channel |raw − ground| > DrawnThreshold
            public float PondA, PondB, Brook, PlateauNorth, PlateauEast;   // 5 × 5 patch means of |raw − ground|
        }

        /// <summary>The plate's sea: one level at every time, the wind and sea state each shot asks for.</summary>
        private sealed class PlateEnvironment : IEnvironmentService
        {
            public Vector2 Wind;
            public float SeaState01, WaterLevel;
            public float Visibility = 1f;
            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }

            public EnvironmentSample Sample()
                => new EnvironmentSample(Wind, Vector2.zero, WaterLevel, WeatherModel.SeaFromWind(Wind.magnitude),
                                         Visibility, SeaState01);

            public float TideHeightAt(double totalSeconds) => WaterLevel;
        }

        private sealed class Session
        {
            private readonly Kit _kit;
            private readonly bool _interactive;
            private readonly PlateEnvironment _env = new PlateEnvironment();
            private readonly List<Object> _made = new List<Object>();
            private readonly List<GameObject> _seas = new List<GameObject>();   // destroyed first, newest first
            private readonly List<string> _fails = new List<string>();
            private readonly List<string> _warns = new List<string>();
            private readonly List<string> _notes = new List<string>();
            private readonly List<string> _probeLines = new List<string>();
            private readonly List<string> _wetLines = new List<string>();
            private readonly List<Shot> _shots = new List<Shot>();
            private readonly Dictionary<string, Color[]> _tiles = new Dictionary<string, Color[]>();

            // What the plate replaces, put back at teardown.
            private readonly GameConfig _prevConfig;
            private readonly IEnvironmentService _prevEnvironment;
            private readonly ITidalTerrain _prevTerrain;
            private readonly IStillWater _prevStill;
            private readonly Color _prevTint;
            private readonly Vector4 _prevSunDir, _prevMoonDir, _prevMoonPhase;
            private readonly float _prevSunElevation;
            private readonly Texture _prevReflect;

            private string _dir;
            private PaintedHeightMap _map;
            private PaintedTidalTerrain _terrain;
            private DayNightProfile _profile;
            private Camera _cam;
            private RenderTexture _rt;
            private GameObject _sea1, _sea2;
            private Color[] _ground;   // GroundPx², the ground sprite's colours as drawn
            private Color _tint = Color.white;
            private string _swashLine, _bakeLine;

            public Session(Kit kit, bool interactive)
            {
                _kit = kit;
                _interactive = interactive;
                _prevConfig = GameServices.Config;
                _prevEnvironment = GameServices.Environment;
                _prevTerrain = GameServices.TidalTerrain;
                _prevStill = GameServices.StillWater;
                _prevTint = Shader.GetGlobalColor(IdDayNightTint);
                _prevSunDir = Shader.GetGlobalVector(IdSunDir);
                _prevSunElevation = Shader.GetGlobalFloat(IdSunElevation);
                _prevMoonDir = Shader.GetGlobalVector(IdMoonDir);
                _prevMoonPhase = Shader.GetGlobalVector(IdMoonPhaseState);
                _prevReflect = Shader.GetGlobalTexture(IdReflectTex);
            }

            public bool Photograph()
            {
                try
                {
                    Steps();
                }
                catch (Exception e)
                {
                    Fail("the plate threw before it finished: " + e.GetType().Name + ": " + e.Message);
                    Debug.LogException(e);
                }
                WriteManifest();
                return Verdict();
            }

            private void Steps()
            {
                _dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,
                                              "Evidence~", "still-water-plate");
                System.IO.Directory.CreateDirectory(_dir);

                Progress("Laying the plate's maps", 0.02f);
                BuildMap();
                BuildTerrain();
                if (!MapDecodes()) return;   // every check downstream would be measuring some other map

                Progress("Asking the walker", 0.08f);
                ProbeTheWalker();

                Progress("Laying the ground", 0.12f);
                BuildGround();
                BuildCamera();
                PrepareSky();

                ShootSea1();
                ShootSea2();

                Progress("Mapping the walker's bands", 0.85f);
                MapBands();
                Progress("Comparing the drawn water with the walker's", 0.9f);
                CompareWetMasks();
                MeasureRimSwash();
                CompareTheBakes();

                Progress("Writing the sheet", 0.97f);
                WriteSheet();
            }

            // ---- the maps, the terrain, the walker ------------------------------------------------------------

            private void BuildMap()
            {
                int count = MapRes * MapRes;
                var heightPx = new Color[count];
                var stillPx = new Color[count];
                var stillCodes = new ushort[count];
                for (int j = 0; j < MapRes; j++)
                {
                    double y = -Frame * 0.5 + (j + 0.5) * Geometry.Cell;   // row 0 at the south edge, as the decode
                    for (int i = 0; i < MapRes; i++)
                    {
                        double x = -Frame * 0.5 + (i + 0.5) * Geometry.Cell;
                        int k = j * MapRes + i;
                        float r = PaintedHeightField.EncodeElevation((float)Geometry.Terrain(x, y), MapMin, MapMax);
                        heightPx[k] = new Color(r, r, r, 1f);
                        ushort code = StillWaterLevels.EncodeCode((float)Geometry.Still(x, y), MapMin, MapMax);
                        stillCodes[k] = code;
                        float s = code / (float)StillWaterLevels.CodeCount;
                        stillPx[k] = new Color(s, s, s, 1f);
                    }
                }

                Texture2D height = R16Texture("StillWaterPlate.Height", PaintedHeightPng.EncodeCodes(heightPx));
                Texture2D still = R16Texture("StillWaterPlate.Still", stillCodes);
                WriteFile("height_r16.png", PaintedHeightPng.EncodeR16Png(heightPx, MapRes, MapRes));
                WriteFile("still_r16.png", PaintedHeightPng.EncodeR16Png(stillPx, MapRes, MapRes));

                _map = ScriptableObject.CreateInstance<PaintedHeightMap>();
                _map.name = "StillWaterPlate.Map";
                _map.hideFlags = HideFlags.HideAndDontSave;
                _made.Add(_map);
                using (var so = new SerializedObject(_map))
                {
                    Property(so, "_heightTexture").objectReferenceValue = height;
                    Property(so, "_stillLevelTexture").objectReferenceValue = still;
                    Property(so, "_worldCenter").vector2Value = Vector2.zero;
                    Property(so, "_worldSize").vector2Value = FrameSize;
                    Property(so, "_minElevation").floatValue = MapMin;
                    Property(so, "_maxElevation").floatValue = MapMax;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                _map.Rebuild();
            }

            private Texture2D R16Texture(string name, ushort[] codes)
            {
                var tex = new Texture2D(MapRes, MapRes, TextureFormat.R16, false, true)
                {
                    name = name,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _made.Add(tex);
                tex.SetPixelData(codes, 0);
                tex.Apply(false, false);   // stays CPU-readable: the sim decodes it
                return tex;
            }

            private static SerializedProperty Property(SerializedObject so, string name)
            {
                SerializedProperty property = so.FindProperty(name);
                if (property == null)
                    throw new InvalidOperationException("PaintedHeightMap has no serialized field " + name);
                return property;
            }

            private void BuildTerrain()
            {
                var go = new GameObject("StillWaterPlate.Terrain");
                _made.Add(go);
                _terrain = go.AddComponent<PaintedTidalTerrain>();
                _terrain.Map = _map;
                // A region's terrain registers itself on enable, in play mode only; the plate is in edit mode, so it
                // registers by hand what that enable would: the terrain, and its map's still water.
                GameServices.Config = _kit.Config;
                GameServices.Environment = _env;
                GameServices.TidalTerrain = _terrain;
                GameServices.StillWater = _map.StillWater;
            }

            private bool MapDecodes()
            {
                if (_map.Field == null || _map.StillWater == null || !_map.StillWater.IsBound)
                {
                    Fail("the plate's maps did not decode (PaintedHeightMap.Field " + (_map.Field == null ? "null" : "decoded") +
                         ", StillWater " + (_map.StillWater == null ? "null" : _map.StillWater.IsBound ? "bound" : "unbound") + ")");
                    return false;
                }
                // Pond A's centre by hand: the bed 4.65 + 0.65 × r² at the four nearest texel centres (r² 0.0080) =
                // 4.6552; the still level 5.30 at all four, so exactly 5.30 to half a code (0.08 mm).
                float bed = _terrain.ElevationAt(PondACentre);
                float level = _map.StillWater.StillLevelAt(PondACentre);
                _notes.Add(I($"decode at pond A's centre: bed {bed:F4} m (by hand 4.6552), still level {level:F4} m (by hand 5.3000)"));
                bool ok = true;
                if (Mathf.Abs(bed - 4.6552f) > 0.02f)
                {
                    Fail(I($"the terrain decodes pond A's bed at {bed:F4} m, not 4.6552 (±0.02): it is not reading the plate's map"));
                    ok = false;
                }
                if (Mathf.Abs(level - 5.30f) > 0.001f)
                {
                    Fail(I($"the still map decodes pond A's level at {level:F4} m, not 5.3000 (±0.001)"));
                    ok = false;
                }
                return ok;
            }

            private void ProbeTheWalker()
            {
                float wade = _kit.Config.WadeDepth, swim = _kit.Config.SwimLimit;
                foreach (Probe p in BuildProbes())
                {
                    var line = new StringBuilder(I($"{p.Name} ({p.Position.x:F3}, {p.Position.y:F3}): {p.Arithmetic}"));
                    for (int t = 0; t < Tides.Length; t++)
                    {
                        float tide = Tides[t];
                        _env.WaterLevel = tide;
                        float depth = TidalWalkability.DepthAt(_terrain, _env, _map.StillWater, NoSurfaces, 0d, p.Position);
                        float tideAlone = TidalWalkability.DepthAt(_terrain, _env, EmptyStillWater.Instance, NoSurfaces, 0d,
                                                                   p.Position);
                        DepthBand band = TidalWalkability.BandAt(_terrain, _env, _map.StillWater, NoSurfaces, 0d, p.Position,
                                                                 wade, swim);
                        bool match = band == p.Expected[t];
                        line.Append(I($"\n    tide {tide:+0.00;-0.00;+0.00}: depth {depth:+0.000;-0.000;0.000} m (the tide alone {tideAlone:+0.000;-0.000;0.000}) {band}, expected {p.Expected[t]}{(match ? "" : "   <-- FAIL")}"));
                        if (!match)
                            Fail(I($"probe {p.Name}, tide {tide:+0.00;-0.00;+0.00}: the walker reads {band} ({depth:F3} m) where the arithmetic says {p.Expected[t]} ({p.Arithmetic})"));
                    }
                    _probeLines.Add(line.ToString());
                }
            }

            // ---- the stage ------------------------------------------------------------------------------------

            private void BuildGround()
            {
                const float cell = Frame / GroundPx;
                var tex = new Texture2D(GroundPx, GroundPx, TextureFormat.RGBA32, false, true)
                {
                    name = "StillWaterPlate.Ground",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _made.Add(tex);
                var texels = new Color32[GroundPx * GroundPx];
                _ground = new Color[texels.Length];
                for (int j = 0; j < GroundPx; j++)
                {
                    float y = -Frame * 0.5f + (j + 0.5f) * cell;
                    float natural = (float)Geometry.Ground(y);
                    for (int i = 0; i < GroundPx; i++)
                    {
                        float x = -Frame * 0.5f + (i + 0.5f) * cell;
                        float e = _terrain.ElevationAt(new Vector2(x, y));
                        Color c = e < natural - 0.05f ? Mud : e < 2.2f ? Sand : e < 5.5f ? Grass : Bog;
                        float shade = 0.92f + 0.08f * (Mathf.FloorToInt(e / 0.5f) & 1);
                        Color32 q = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                        texels[j * GroundPx + i] = q;
                        _ground[j * GroundPx + i] = q;   // quantised, as the camera sees it
                    }
                }
                tex.SetPixels32(texels);
                tex.Apply(false, false);

                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, GroundPx, GroundPx), new Vector2(0.5f, 0.5f),
                                              GroundPx / Frame, 0, SpriteMeshType.FullRect);
                sprite.name = "StillWaterPlate.Ground";
                sprite.hideFlags = HideFlags.HideAndDontSave;
                _made.Add(sprite);
                var material = new Material(_kit.Unlit) { name = "StillWaterPlate.Ground", hideFlags = HideFlags.HideAndDontSave };
                _made.Add(material);

                var go = new GameObject("StillWaterPlate.Ground");
                _made.Add(go);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = material;
                sr.sortingOrder = SortingBands.PaintedSeabedMin;
            }

            private void BuildCamera()
            {
                var go = new GameObject("StillWaterPlate.Camera");
                _made.Add(go);
                go.transform.position = new Vector3(0f, 0f, -100f);
                _cam = go.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.orthographicSize = Frame * 0.5f;
                _cam.nearClipPlane = 1f;
                _cam.farClipPlane = 400f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
                _cam.allowMSAA = false;
                _rt = new RenderTexture(ShotPx, ShotPx, 24, RenderTextureFormat.ARGBHalf)
                {
                    name = "StillWaterPlate.Shot",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                };
                _made.Add(_rt);
                _cam.targetTexture = _rt;
            }

            private void PrepareSky()
            {
                _profile = Resources.Load<DayNightProfile>("DayNightProfile");
                if (_profile == null)
                {
                    _profile = DayNightProfile.CreateDefault();
                    _profile.hideFlags = HideFlags.HideAndDontSave;
                    _made.Add(_profile);   // ours; a loaded asset is never destroyed
                    _notes.Add("no DayNightProfile in Resources: lit with DayNightProfile.CreateDefault()");
                }
                // No reflection probe in a scratch scene: a clear one, as the sweep binds.
                var reflect = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true)
                {
                    name = "StillWaterPlate.NoReflection",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                reflect.SetPixel(0, 0, Color.clear);
                reflect.Apply(false, false);
                _made.Add(reflect);
                Shader.SetGlobalTexture(IdReflectTex, reflect);
                // A new moon: no moonlight in a noon plate.
                Shader.SetGlobalVector(IdMoonDir, Vector4.zero);
                Shader.SetGlobalVector(IdMoonPhaseState, new Vector4(0.02f, -1f, 0f, 0f));
            }

            /// <summary>A land region's sea over the frame, as the water plate sweep builds one: configured whole
            /// while inactive, so its first enable bakes and holds the globals once.</summary>
            private GameObject BuildSea(string name, int bakeResolution)
            {
                var go = new GameObject(name);
                _seas.Add(go);
                go.SetActive(false);
                go.transform.position = Vector3.zero;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingBands.Sea;
                sr.sharedMaterial = _kit.WaterMaterial;
                sr.sprite = _kit.SeaTile;
                WaterSceneTemplate.ConfigureSeaPlane(sr, FrameSize);
                go.AddComponent<WaterSurface>();
                WaterSceneTemplate.ConfigureLandRegionWater(go, Vector2.zero, FrameSize, bakeResolution, MapMin, MapMax,
                                                            StPetersBuilder.ShoreGradient);
                return go;
            }

            // ---- the shots ------------------------------------------------------------------------------------

            private void ShootSea1()
            {
                Progress("Building the painted sea", 0.2f);
                _sea1 = BuildSea("StillWaterPlate.Sea1", SeaBakeRes);
                var surface = _sea1.GetComponent<WaterSurface>();
                surface.ConfigurePaintedHeightMap(_map.HeightTexture, _map.WorldCenter, _map.WorldSize,
                                                  _map.MinElevation, _map.MaxElevation);
                _sea1.SetActive(true);

                var block = new MaterialPropertyBlock();
                _sea1.GetComponent<SpriteRenderer>().GetPropertyBlock(block);
                Texture fed = block.GetTexture(IdHeightTex);
                _notes.Add("sea 1: baked terrain " + (surface.BakedTerrain == null ? "none" : "SET") + ", _HeightTex " +
                           (fed == null ? "unset" : fed.name));
                if (surface.BakedTerrain != null || fed != _map.HeightTexture)
                {
                    Fail("sea 1 is not on the painted path (it must bake no terrain and read the plate's height map as " +
                         "_HeightTex): its shots would not be of the plate's map");
                    return;
                }

                Progress("Warming the water shader", 0.25f);
                if (!WarmUp(surface, "sea 1")) return;

                foreach (float tide in ShotTides) Shoot(Sea1, surface, 0f, tide, true);
                foreach (float tide in ShotTides) Shoot(Sea1, surface, 0f, tide, false);
                Shoot(Sea1, surface, BlowSea01, 2.2f, true);
            }

            private void ShootSea2()
            {
                Progress("Building the Auto sea at 3 m cells", 0.7f);
                _sea2 = BuildSea("StillWaterPlate.Sea2", CoarseBakeRes);
                if (_sea1 != null) _sea1.SetActive(false);   // its disable unpublishes; then sea 2 publishes its own
                _sea2.SetActive(true);
                var surface = _sea2.GetComponent<WaterSurface>();
                _notes.Add("sea 2: baked terrain " + (surface.BakedTerrain == null ? "none"
                           : ReferenceEquals(surface.BakedTerrain, _terrain) ? "the plate's" : "ANOTHER") + I($", {CoarseBakeRes}² over {Frame} m"));
                if (!ReferenceEquals(surface.BakedTerrain, _terrain))
                {
                    Fail("sea 2 did not bake the plate's terrain on Auto: its shots would not be of the plate's map");
                    return;
                }

                Progress("Warming the water shader", 0.75f);
                if (!WarmUp(surface, "sea 2")) return;

                Shoot(Sea2, surface, 0f, -2.2f, true);
                Shoot(Sea2, surface, 0f, 2.2f, true);
            }

            /// <summary>The two ends of the plate's variants — a working sea at high tide over registered still
            /// water, and glass at low tide with none — so no shot is taken through a half-compiled shader.</summary>
            private bool WarmUp(WaterSurface surface, string sea)
            {
                Publish(surface, BlowSea01, 2.2f, true);
                bool ok = WaitOutCompilation();
                Publish(surface, 0f, -2.2f, false);
                ok = ok && WaitOutCompilation();
                if (!ok) Fail(sea + ": the shaders were still compiling after the warm-up (10 renders, up to 120 s each)");
                return ok;
            }

            /// <summary>LampShadowRenderTests' wait: render to request the variants, then wait out the compiler.</summary>
            private bool WaitOutCompilation()
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    _cam.Render();
                    if (!ShaderUtil.anythingCompiling) return true;
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < 120)
                        System.Threading.Thread.Sleep(25);
                }
                return false;
            }

            /// <summary>Pin everything a shot depends on, as the water plate sweep does: the environment (tide, wind,
            /// sea state), the still water registered or not, the wave field and the sky at noon — then snap the
            /// sea's uniforms to them.</summary>
            private void Publish(WaterSurface surface, float sea01, float tide, bool withStill)
            {
                _env.SeaState01 = sea01;
                _env.Wind = sea01 <= 0f ? Vector2.zero : WindHeading * WeatherModel.WindStrengthFor(sea01);
                _env.WaterLevel = tide;
                GameServices.StillWater = withStill ? _map.StillWater : null;

                WaveTrains trains = WaveMath.TrainsFrom(_env.Wind, sea01, GameServices.WaveField);
                WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in trains));
                WaveFieldBridge.PublishFetchGlobals(GameServices.WaveFetch, _env.Wind);
                WaveFieldBridge.PublishBreakerGlobals(trains.Dominant, GameServices.WaveFetch, GameServices.Breakers);

                _tint = DayNightMath.DayNightTint(Noon, _profile, _env.Visibility, sea01);
                Vector2 sunDir = DayNightMath.SunDirection(Noon, _profile.SunriseHour, _profile.SunsetHour,
                                                           _profile.ShadowSouthBias, _profile.ShadowNoonLift);
                float sunElevation = DayNightMath.SunElevation(Noon, _profile.SunriseHour, _profile.SunsetHour);
                Shader.SetGlobalColor(IdDayNightTint, _tint);
                Shader.SetGlobalVector(IdSunDir, new Vector4(sunDir.x, sunDir.y, 0f, 0f));
                Shader.SetGlobalFloat(IdSunElevation, sunElevation);

                _kit.Push.Invoke(surface, null);
            }

            private void Shoot(string sea, WaterSurface surface, float sea01, float tide, bool withStill)
            {
                string stem = I($"{sea}_{(sea01 > 0f ? "blow" : "glass")}_{(withStill ? "still" : "nostill")}_tide{tide:+0.00;-0.00;+0.00}");
                Progress("Shooting " + stem, 0.3f + 0.4f * _shots.Count / 9f);
                Publish(surface, sea01, tide, withStill);
                if (!WaitOutCompilation()) Fail(stem + ": a shader was still compiling at the capture");
                _cam.Render();
                Color[] raw = ReadBack();

                var block = new MaterialPropertyBlock();
                surface.GetComponent<SpriteRenderer>().GetPropertyBlock(block);
                var shot = new Shot
                {
                    Stem = stem,
                    Sea = sea,
                    Sea01 = sea01,
                    Tide = tide,
                    WithStill = withStill,
                    Pushed = block.GetFloat(IdWaterLevel),
                    StillRange = Shader.GetGlobalVector(IdStillRange),
                    StillRect = Shader.GetGlobalVector(IdStillRect),
                    Baked = surface.BakedTerrain == null ? "none (painted path)"
                          : ReferenceEquals(surface.BakedTerrain, _terrain) ? "the plate's terrain (Auto)" : "ANOTHER terrain",
                    Drawn = new bool[raw.Length],
                };
                for (int py = 0; py < ShotPx; py++)
                {
                    for (int px = 0; px < ShotPx; px++)
                    {
                        int k = py * ShotPx + px;
                        shot.Drawn[k] = Diff(raw[k], GroundAt(px, py)) > DrawnThreshold;
                    }
                }
                shot.PondA = PatchDiff(raw, PondACentre);
                shot.PondB = PatchDiff(raw, PondBCentre);
                shot.Brook = PatchDiff(raw, BrookMid);
                shot.PlateauNorth = PatchDiff(raw, PlateauNorth);
                shot.PlateauEast = PatchDiff(raw, PlateauEast);

                // The evidence as the sweep writes it: linear × the day-night tint, clamped, no display gamma.
                var ldr = new Color[raw.Length];
                for (int i = 0; i < raw.Length; i++)
                {
                    Color c = raw[i];
                    ldr[i] = new Color(Mathf.Clamp01(c.r * _tint.r), Mathf.Clamp01(c.g * _tint.g),
                                       Mathf.Clamp01(c.b * _tint.b), 1f);
                }
                WritePng(stem + ".png", ldr, ShotPx, ShotPx);
                _tiles[stem] = Thumb(ldr);
                _shots.Add(shot);
                JudgeShot(shot);
            }

            private Color[] ReadBack()
            {
                RenderTexture previous = RenderTexture.active;
                var hdr = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBAFloat, false, true);
                try
                {
                    RenderTexture.active = _rt;
                    hdr.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0, false);
                    hdr.Apply(false, false);
                    return hdr.GetPixels();
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(hdr);
                }
            }

            private void JudgeShot(Shot s)
            {
                if (Mathf.Abs(s.Pushed - s.Tide) > PushTolerance)
                    Fail(I($"{s.Stem}: the sea pushed _WaterLevel {s.Pushed:F4}, not the tide asked for ({s.Tide:F2})"));
                float bound = s.WithStill ? 1f : 0f;
                if (Mathf.Abs(s.StillRange.z - bound) > 1e-4f)
                    Fail(I($"{s.Stem}: _HHStillRange.z reads {s.StillRange.z:F3}, and with the still map {(s.WithStill ? "registered" : "cleared")} it must read {bound:F0}"));
                if (s.PlateauNorth > DryTolerance || s.PlateauEast > DryTolerance)
                    Fail(I($"{s.Stem}: water is drawn on the plateau, over 3.5 m above the highest tide (|raw − ground| {s.PlateauNorth:F4} north, {s.PlateauEast:F4} east; bare ground reads ≤ {DryTolerance})"));
                if (s.Sea != Sea1) return;   // sea 2 is diagnostic beyond its honesty

                if (s.WithStill)
                {
                    if (s.PondA <= PondDrawnMin || s.PondB <= PondDrawnMin)
                        Fail(I($"{s.Stem}: a pond is not drawn with its still map registered (|raw − ground| at the centres: A {s.PondA:F4}, B {s.PondB:F4}; water reads > {PondDrawnMin})"));
                    if (s.Brook <= DrawnThreshold)
                        Warn(I($"{s.Stem}: the brook at y -5 reads {s.Brook:F4} against the ground (drawn is > {DrawnThreshold}): its 0.14 m of fresh water may not be drawn"));
                }
                else if (s.PondA > DryTolerance || s.PondB > DryTolerance || s.Brook > DryTolerance)
                {
                    Fail(I($"{s.Stem}: water is drawn with no still map registered, where only the still map puts any (|raw − ground|: pond A {s.PondA:F4}, pond B {s.PondB:F4}, brook {s.Brook:F4}; bare ground reads ≤ {DryTolerance})"));
                }
            }

            // ---- the walker's bands, and the drawn water against them -----------------------------------------

            private void MapBands()
            {
                const float cell = Frame / BandPx;
                float wade = _kit.Config.WadeDepth, swim = _kit.Config.SwimLimit;
                foreach (float tide in BandTides)
                {
                    _env.WaterLevel = tide;
                    var pixels = new Color[BandPx * BandPx];
                    for (int j = 0; j < BandPx; j++)
                    {
                        float y = -Frame * 0.5f + (j + 0.5f) * cell;
                        for (int i = 0; i < BandPx; i++)
                        {
                            var w = new Vector2(-Frame * 0.5f + (i + 0.5f) * cell, y);
                            float depth = TidalWalkability.DepthAt(_terrain, _env, _map.StillWater, NoSurfaces, 0d, w);
                            bool fresh = _map.StillWater.StillLevelAt(w) > tide;
                            pixels[j * BandPx + i] = BandColour(TidalExposure.BandForDepth(depth, wade, swim), fresh);
                        }
                    }
                    string stem = I($"bands_tide{tide:+0.00;-0.00;+0.00}");
                    WritePng(stem + ".png", pixels, BandPx, BandPx);
                    _tiles[stem] = pixels;
                }
            }

            private static Color BandColour(DepthBand band, bool fresh)
            {
                switch (band)
                {
                    case DepthBand.Wade: return fresh ? FreshWade : TideWade;
                    case DepthBand.Swim: return fresh ? FreshSwim : TideSwim;
                    case DepthBand.Deep: return fresh ? FreshDeep : TideDeep;
                    default: return BandDry;
                }
            }

            /// <summary>Soft: every glass shot of sea 1 against the walker at 0.1 m samples — water the walker wades or
            /// swims (deeper than the margin) that the sea leaves undrawn, and ground it calls dry that the sea
            /// draws wet.</summary>
            private void CompareWetMasks()
            {
                const float sampleM2 = 4f * PxMetres * PxMetres;
                foreach (Shot shot in _shots)
                {
                    if (shot.Sea != Sea1 || shot.Sea01 > 0f) continue;
                    _env.WaterLevel = shot.Tide;
                    IStillWater still = shot.WithStill ? (IStillWater)_map.StillWater : EmptyStillWater.Instance;
                    int wetUndrawn = 0, dryDrawn = 0;
                    for (int py = 0; py < ShotPx; py += 2)
                    {
                        for (int px = 0; px < ShotPx; px += 2)
                        {
                            float depth = TidalWalkability.DepthAt(_terrain, _env, still, NoSurfaces, 0d, PixelWorld(px, py));
                            bool drawn = shot.Drawn[py * ShotPx + px];
                            if (depth > WetMargin && !drawn) wetUndrawn++;
                            else if (depth < -WetMargin && drawn) dryDrawn++;
                        }
                    }
                    float undrawn = wetUndrawn * sampleM2, overdrawn = dryDrawn * sampleM2;
                    _wetLines.Add(I($"{shot.Stem}: wet to the walker but undrawn {undrawn:F2} m²; dry to the walker but drawn {overdrawn:F2} m²"));
                    if (undrawn > WetMismatchWarnM2 || overdrawn > WetMismatchWarnM2)
                        Warn(I($"{shot.Stem}: the drawn water disagrees with the walker's by more than {WetMismatchWarnM2} m² (undrawn {undrawn:F2}, overdrawn {overdrawn:F2}; ±{WetMargin} m margin)"));
                }
            }

            /// <summary>Soft: whether a working sea draws water past a pond's still water (r 1.2 to 1.6) where glass
            /// does not, at high tide with the still map registered.</summary>
            private void MeasureRimSwash()
            {
                Shot blow = FindShot(Sea1, BlowSea01, 2.2f, true), glass = FindShot(Sea1, 0f, 2.2f, true);
                if (blow == null || glass == null)
                {
                    _swashLine = "not measured: a shot is missing";
                    return;
                }
                int count = 0;
                for (int py = 0; py < ShotPx; py++)
                {
                    for (int px = 0; px < ShotPx; px++)
                    {
                        int k = py * ShotPx + px;
                        if (!blow.Drawn[k] || glass.Drawn[k]) continue;
                        Vector2 w = PixelWorld(px, py);
                        double ra = Geometry.PondA.R(w.x, w.y), rb = Geometry.PondB.R(w.x, w.y);
                        if ((ra > Geometry.PondStillR && ra <= 1.6) || (rb > Geometry.PondStillR && rb <= 1.6)) count++;
                    }
                }
                float m2 = count * PxMetres * PxMetres;
                _swashLine = I($"drawn in the blow but not in glass, on the ponds' banks (r 1.2 to 1.6): {m2:F3} m²");
                if (m2 > 0f) Warn(I($"a working sea draws {m2:F3} m² of water on the ponds' banks that glass does not: swash over still water"));
            }

            /// <summary>Diagnostic, for PR 5: the brook's fresh reach and the ponds' inner discs, drawn by the painted
            /// sea against the 3 m Auto bake, glass at high tide with the still map registered.</summary>
            private void CompareTheBakes()
            {
                Shot fine = FindShot(Sea1, 0f, 2.2f, true), coarse = FindShot(Sea2, 0f, 2.2f, true);
                if (fine == null || coarse == null)
                {
                    _bakeLine = "not measured: a shot is missing";
                    return;
                }
                _env.WaterLevel = 2.2f;
                int brook = 0, brookFine = 0, brookCoarse = 0, pond = 0, pondFine = 0, pondCoarse = 0;
                for (int py = 0; py < ShotPx; py++)
                {
                    for (int px = 0; px < ShotPx; px++)
                    {
                        int k = py * ShotPx + px;
                        Vector2 w = PixelWorld(px, py);
                        if (Geometry.PondA.R(w.x, w.y) <= 1.0 || Geometry.PondB.R(w.x, w.y) <= 1.0)
                        {
                            pond++;
                            if (fine.Drawn[k]) pondFine++;
                            if (coarse.Drawn[k]) pondCoarse++;
                            continue;
                        }
                        if (w.y >= Geometry.BrookOutY || Math.Abs(w.x - Geometry.BrookX(w.y)) > Geometry.BrookHalfW) continue;
                        if (_map.StillWater.StillLevelAt(w) <= 2.2f) continue;
                        if (TidalWalkability.DepthAt(_terrain, _env, _map.StillWater, NoSurfaces, 0d, w) <= 0.05f) continue;
                        brook++;
                        if (fine.Drawn[k]) brookFine++;
                        if (coarse.Drawn[k]) brookCoarse++;
                    }
                }
                const float pixelM2 = PxMetres * PxMetres;
                _bakeLine = I($"the brook's fresh reach ({brook * pixelM2:F1} m² the walker wades): drawn {Pct(brookFine, brook)} on the painted sea, {Pct(brookCoarse, brook)} on the 3 m Auto bake; the ponds' inner discs (r ≤ 1, {pond * pixelM2:F1} m²): {Pct(pondFine, pond)} against {Pct(pondCoarse, pond)}");
            }

            private Shot FindShot(string sea, float sea01, float tide, bool withStill)
                => _shots.Find(s => s.Sea == sea && Mathf.Approximately(s.Sea01, sea01) && Mathf.Approximately(s.Tide, tide) &&
                                    s.WithStill == withStill);

            // ---- pixels ---------------------------------------------------------------------------------------

            private Color GroundAt(int px, int py) => _ground[(py / GroundScale) * GroundPx + px / GroundScale];

            private static Vector2 PixelWorld(int px, int py)
                => new Vector2(-Frame * 0.5f + (px + 0.5f) * PxMetres, -Frame * 0.5f + (py + 0.5f) * PxMetres);

            private static float Diff(Color a, Color b)
                => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

            /// <summary>The mean |raw − ground| over the 5 × 5 pixels (0.25 m) centred on a world point.</summary>
            private float PatchDiff(Color[] raw, Vector2 world)
            {
                int cx = Mathf.FloorToInt((world.x + Frame * 0.5f) / PxMetres);
                int cy = Mathf.FloorToInt((world.y + Frame * 0.5f) / PxMetres);
                double sum = 0.0;
                int n = 0;
                for (int py = cy - 2; py <= cy + 2; py++)
                {
                    for (int px = cx - 2; px <= cx + 2; px++)
                    {
                        if (px < 0 || py < 0 || px >= ShotPx || py >= ShotPx) continue;
                        sum += Diff(raw[py * ShotPx + px], GroundAt(px, py));
                        n++;
                    }
                }
                return n == 0 ? 0f : (float)(sum / n);
            }

            private static Color[] Thumb(Color[] ldr)
            {
                const int step = ShotPx / ThumbPx;
                const float n = step * step;
                var thumb = new Color[ThumbPx * ThumbPx];
                for (int ty = 0; ty < ThumbPx; ty++)
                {
                    for (int tx = 0; tx < ThumbPx; tx++)
                    {
                        float r = 0f, g = 0f, b = 0f;
                        for (int dy = 0; dy < step; dy++)
                        {
                            for (int dx = 0; dx < step; dx++)
                            {
                                Color c = ldr[(ty * step + dy) * ShotPx + tx * step + dx];
                                r += c.r;
                                g += c.g;
                                b += c.b;
                            }
                        }
                        thumb[ty * ThumbPx + tx] = new Color(r / n, g / n, b / n, 1f);
                    }
                }
                return thumb;
            }

            private void WriteSheet()
            {
                const int width = SheetCols * (ThumbPx + Gap) + Gap, height = SheetRows * (ThumbPx + Gap) + Gap;
                var pixels = new Color[width * height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Gutter;
                for (int row = 0; row < SheetRows; row++)
                {
                    for (int col = 0; col < SheetCols; col++)
                    {
                        int x0 = Gap + col * (ThumbPx + Gap), y0 = height - Gap - ThumbPx - row * (ThumbPx + Gap);
                        _tiles.TryGetValue(SheetLayout[row, col], out Color[] tile);
                        for (int y = 0; y < ThumbPx; y++)
                            for (int x = 0; x < ThumbPx; x++)
                                pixels[(y0 + y) * width + x0 + x] = tile != null ? tile[y * ThumbPx + x] : Blank;
                    }
                }
                WritePng("sheet.png", pixels, width, height);
            }

            private void WritePng(string file, Color[] pixels, int width, int height)
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                try
                {
                    tex.SetPixels(pixels);
                    tex.Apply(false, false);
                    WriteFile(file, tex.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }

            private void WriteFile(string file, byte[] bytes) => System.IO.File.WriteAllBytes(System.IO.Path.Combine(_dir, file), bytes);

            // ---- the record -----------------------------------------------------------------------------------

            private void WriteManifest()
            {
                if (_dir == null) return;
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Still Water Plate: terrain pass 9, PR 4 (ADR 0046 §10)");
                    sb.AppendLine(I($"unity {Application.unityVersion} | device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}) | R16 sampled: {SystemInfo.SupportsTextureFormat(TextureFormat.R16)}"));
                    sb.AppendLine("the PNGs are the camera's linear values × the day-night tint, clamped, with no display gamma, as the water plate sweep writes them");
                    sb.AppendLine("judged on its pictures (owner decision 3): the checks below are the plate's own honesty, not the verdict on the look");
                    sb.AppendLine();
                    sb.AppendLine("## the frame (a scratch scene, never saved; the maps live only in memory)");
                    sb.AppendLine(I($"48 × 48 m on the origin; height_r16.png and still_r16.png are its maps: R16, {MapRes}², 0.25 m texels, {MapMin} … +{MapMax} m (a code is 0.1678 mm), row 0 at the south edge"));
                    sb.AppendLine("ground: a beach from -3.0 at the south edge to +2.2 at y -10, a 0.35 slope to +5.7 at y 0, a plateau to +6.0 at the north edge");
                    sb.AppendLine("pond A (-8, 11): 5.2 × 3.3 m, +5.30 over a +4.65 bed; pond B (9, 13): 6.5 × 4.0 m at -8°, +5.25 over +4.70");
                    sb.AppendLine("the brook: from pond A's outlet (y +9.35) down the slope, 1.2 m wide, 0.14 m of water, its bed never climbing downstream, to the head of tide at y -8.86 (the bed below +2.2)");
                    sb.AppendLine("sea 1: the painted path, reading the height map itself; sea 2: Auto, baking the registered terrain at 16² (3 m cells, about St Peters' 256² over ~760 × 520 m)");
                    sb.AppendLine();
                    sb.AppendLine("## notes");
                    foreach (string note in _notes) sb.AppendLine(note);
                    sb.AppendLine();
                    sb.AppendLine("## shots");
                    sb.AppendLine("file | sea | sea01 | tide | still map | pushed _WaterLevel | _HHStillRange | _HHStillRect | baked terrain | |raw - ground| at pond A, pond B, brook y -5, plateau N, plateau E");
                    foreach (Shot s in _shots)
                        sb.AppendLine(I($"{s.Stem}.png | {s.Sea} | {s.Sea01:F2} | {s.Tide:+0.00;-0.00;+0.00} | {(s.WithStill ? "registered" : "none")} | {s.Pushed:F4} | ({s.StillRange.x:F3}, {s.StillRange.y:F3}, {s.StillRange.z:F0}, {s.StillRange.w:F0}) | ({s.StillRect.x:F3}, {s.StillRect.y:F3}, {s.StillRect.z:F5}, {s.StillRect.w:F5}) | {s.Baked} | {s.PondA:F4} {s.PondB:F4} {s.Brook:F4} {s.PlateauNorth:F4} {s.PlateauEast:F4}"));
                    sb.AppendLine();
                    sb.AppendLine("## the walker (TidalWalkability.BandAt, still map registered; WadeDepth 0.5, SwimLimit 2.0)");
                    foreach (string line in _probeLines) sb.AppendLine(line);
                    sb.AppendLine();
                    sb.AppendLine(I($"## the drawn water against the walker's (sea 1, glass; 0.1 m samples; ±{WetMargin} m margin)"));
                    foreach (string line in _wetLines) sb.AppendLine(line);
                    sb.AppendLine();
                    sb.AppendLine("## a working sea at the ponds' banks (sea 1, +2.20, still map registered, blow 0.55 against glass)");
                    sb.AppendLine(_swashLine ?? "not measured");
                    sb.AppendLine();
                    sb.AppendLine("## the Auto bake at 3 m cells (sea 2, glass, +2.20, still map registered): diagnostic, for PR 5");
                    sb.AppendLine(_bakeLine ?? "not measured");
                    sb.AppendLine();
                    sb.AppendLine("## sheet.png: rows top to bottom, 320 px tiles, 8 px gutters; a grey tile was not shot");
                    sb.AppendLine("bands: pale = dry; blue = the tide's water, green = fresh water; light, mid, dark = wade, swim, deep");
                    for (int row = 0; row < SheetRows; row++)
                        sb.AppendLine(I($"row {row + 1}: {SheetLayout[row, 0]} | {SheetLayout[row, 1]} | {SheetLayout[row, 2]}"));
                    sb.AppendLine();
                    sb.AppendLine("## verdict");
                    sb.AppendLine(I($"{(_fails.Count == 0 ? "PASS" : "FAIL")}: {_fails.Count} failure(s), {_warns.Count} warning(s)"));
                    foreach (string fail in _fails) sb.AppendLine("FAIL: " + fail);
                    foreach (string warn in _warns) sb.AppendLine("WARN: " + warn);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(_dir, "plate.txt"), sb.ToString(), new UTF8Encoding(false));
                }
                catch (Exception e)
                {
                    Fail("the manifest could not be written: " + e.Message);
                    Debug.LogException(e);
                }
            }

            private bool Verdict()
            {
                if (_interactive) EditorUtility.ClearProgressBar();
                foreach (string warn in _warns) Debug.LogWarning(Tag + " WARN: " + warn);
                foreach (string fail in _fails) Debug.LogError(Tag + " FAIL: " + fail);
                bool pass = _fails.Count == 0;
                string summary = I($"{(pass ? "PASS" : "FAIL")}: {_fails.Count} failure(s), {_warns.Count} warning(s), {_shots.Count} shot(s). Evidence: {_dir ?? "none written"}. Judged on its pictures.");
                if (pass) Debug.Log(Tag + " " + summary);
                else Debug.LogError(Tag + " " + summary);
                if (_interactive) EditorUtility.DisplayDialog("Still Water Plate", summary, "OK");
                return pass;
            }

            private void Fail(string message) => _fails.Add(message);

            private void Warn(string message) => _warns.Add(message);

            private void Progress(string info, float progress)
            {
                if (_interactive) EditorUtility.DisplayProgressBar("Still Water Plate", info, progress);
            }

            /// <summary>The seas first, while every texture they read is alive (their disable releases the
            /// still-water globals and unpublishes the sea level and the seabed), then everything the plate made,
            /// newest first; then the services and the globals it replaced.</summary>
            public void TearDown()
            {
                if (_interactive) EditorUtility.ClearProgressBar();
                for (int i = _seas.Count - 1; i >= 0; i--)
                {
                    if (_seas[i] != null) Object.DestroyImmediate(_seas[i]);
                }
                _seas.Clear();
                if (_cam != null) _cam.targetTexture = null;
                if (_rt != null) _rt.Release();
                for (int i = _made.Count - 1; i >= 0; i--)
                {
                    if (_made[i] != null) Object.DestroyImmediate(_made[i]);
                }
                _made.Clear();

                GameServices.StillWater = ReferenceEquals(_prevStill, EmptyStillWater.Instance) ? null : _prevStill;
                GameServices.TidalTerrain = _prevTerrain;
                GameServices.Environment = _prevEnvironment;
                GameServices.Config = _prevConfig;

                Shader.SetGlobalColor(IdDayNightTint, _prevTint);
                Shader.SetGlobalVector(IdSunDir, _prevSunDir);
                Shader.SetGlobalFloat(IdSunElevation, _prevSunElevation);
                Shader.SetGlobalVector(IdMoonDir, _prevMoonDir);
                Shader.SetGlobalVector(IdMoonPhaseState, _prevMoonPhase);
                Shader.SetGlobalTexture(IdReflectTex, _prevReflect);
                WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
                WaveFieldBridge.PublishFetchOff();
                WaveFieldBridge.PublishBreakersOff();
            }
        }
    }
}
