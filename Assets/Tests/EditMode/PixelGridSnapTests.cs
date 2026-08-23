using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE PIXEL GRID (owner playtest 2026-08-23 — "the running fisher and the Otter go soft during
    /// movement"). The locked <c>PixelPerfectCamera</c> runs <c>GridSnapping.PixelSnapping</c>, and
    /// that mode publishes a grid <b>SpriteRenderers alone</b> snap to. It never moves the camera,
    /// and it never reaches a MeshRenderer — so two rendered positions were still free-floating
    /// floats: the camera (which then samples the snapped world from a different sub-pixel offset
    /// every frame) and every mesh vehicle (<c>IsoFacetHullRenderer</c> draws through a MeshRenderer,
    /// ADR 0022 phase 3). <see cref="PixelGrid"/> is the rounding both now go through.
    ///
    /// <para><b>What can and cannot be proved here.</b> These are arithmetic claims — the rounding
    /// is correct, it is bounded, it is inert unwired, and every rung of the zoom ladder yields a
    /// grid COMPATIBLE with the asset lattice. Whether the resulting picture reads as crisp is an
    /// eyeball question on a real GPU at a real resolution, and that is what the owner's A/B on
    /// <c>GameConfig.PixelGridSnap</c> is for. A green run here means the maths cannot be the reason
    /// it looks wrong; it does not mean it looks right.</para>
    /// </summary>
    public class PixelGridSnapTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private const int Ppu = CameraFollow.AssetsPPU;                  // 32, locked (VS-23)
        private const int ScreenH = CameraFollow.DesignScreenHeightPx;   // 1080

        /// <summary>The asset lattice every sprite in the game is snapped onto: one texel at PPU 32.</summary>
        private const float AssetGrid = 1f / Ppu;

        // ===== the rounding itself ============================================================

        [Test]
        public void Snap_RoundsToTheNearestGridPoint_InBothDirections()
        {
            const float grid = 0.25f;
            Assert.AreEqual(1.00f, PixelGrid.Snap(1.04f, grid), 1e-6f, "just above a grid point rounds back down");
            Assert.AreEqual(1.25f, PixelGrid.Snap(1.21f, grid), 1e-6f, "just below the next rounds up");
            Assert.AreEqual(-1.25f, PixelGrid.Snap(-1.21f, grid), 1e-6f,
                "NEGATIVE world coordinates round the same way — half this coast sits at x < 0, and a " +
                "truncation-flavoured rounding would bias one side of the map by a pixel against the other");
        }

        [Test]
        public void Snap_LeavesAValueAlreadyOnTheGrid_Exactly_Untouched()
        {
            // Load-bearing for the bounds rig: the camera positions those tests pin (5000, 100, 48)
            // are whole asset pixels, so a correct snap must be a no-op on them.
            foreach (float onGrid in new[] { 0f, 5000f, 100f, 48f, -376.5f })
                Assert.AreEqual(onGrid, PixelGrid.Snap(onGrid, AssetGrid), 1e-4f,
                    $"{onGrid} is a whole number of asset pixels and must survive the snap unchanged");
        }

        [Test]
        public void Snap_IsIdempotent()
        {
            const float grid = 1f / 96f;
            float once = PixelGrid.Snap(123.456789f, grid);
            Assert.AreEqual(once, PixelGrid.Snap(once, grid), 1e-6f,
                "snapping a snapped value must not move it — otherwise the camera would creep a " +
                "pixel per frame while standing perfectly still");
        }

        [Test]
        public void Snap_NeverMovesAPositionMoreThanHalfAPixel()
        {
            // The bound CameraFollow.LateUpdate's ordering comment relies on, and the reason the snap
            // is allowed to run before the bounds clamp rather than after it.
            const float grid = 1f / 96f;
            for (int i = 0; i < 400; i++)
            {
                float x = -7.3f + i * 0.017f;
                Assert.LessOrEqual(Mathf.Abs(PixelGrid.Snap(x, grid) - x), grid * 0.5f + 1e-6f,
                    $"snapping {x} moved it further than half a rendered pixel");
            }
        }

        // ===== the unwired state ==============================================================

        [Test]
        public void ANonPositiveGrid_IsInert_SoAnUnwiredSceneRendersExactlyAsBefore()
        {
            // 0 is what GameServices.WorldUnitsPerRenderedPixel reads before any camera has
            // published a framing — a bare art scene, an EditMode fixture, the first frame of a
            // region. It must mean "leave it alone", never "snap to 1".
            var p = new Vector3(12.3456f, -7.891f, -10f);
            Assert.AreEqual(p, PixelGrid.Snap(p, 0f), "a zero grid must pass the position straight through");
            Assert.AreEqual(p, PixelGrid.Snap(p, -1f), "…and so must a negative one");
            Assert.AreEqual(12.3456f, PixelGrid.Snap(12.3456f, 0f), 1e-6f);
        }

        [Test]
        public void Snap_NeverTouchesZ()
        {
            // Under the ortho camera z is the sorting axis, and for a mesh hull it carries ADR 0033's
            // calibrated iso-depth convention — units that are nothing to do with world metres.
            // Rounding it would re-sort the scene, or throw a hull clean out of frame.
            var p = new Vector3(1.234f, 5.678f, -123.456789f);
            Assert.AreEqual(p.z, PixelGrid.Snap(p, AssetGrid).z, 0f,
                "the depth axis must come through BIT-identical, not merely close");
        }

        // ===== the ladder's grid ==============================================================

        [Test]
        public void AnUpscaleStep_IsOneScreenPixelOfTheMagnifiedView()
        {
            for (int step = 1; step <= CameraZoomPolicy.MaxStep; step++)
                Assert.AreEqual(1f / (Ppu * step),
                    CameraZoomPolicy.WorldUnitsPerRenderedPixel(step, Ppu, ScreenH), 1e-7f,
                    $"at x{step} magnification one screen pixel is a {step}th of an asset pixel");
        }

        [Test]
        public void ADownscaleStep_IsTheShrunkPixel()
        {
            for (int step = -2; step >= CameraZoomPolicy.MinStep; step--)
                Assert.AreEqual(-step / (float)Ppu,
                    CameraZoomPolicy.WorldUnitsPerRenderedPixel(step, Ppu, ScreenH), 1e-7f,
                    $"at {-step}:1 downscale one screen pixel swallows {-step} asset pixels");
        }

        [Test]
        public void EveryLadderStepsGrid_IsAWholeNumberRelativeOfTheAssetGrid()
        {
            // ⭐ THE CLAIM THE WHOLE FIX RESTS ON, and the reason the grid is derived from the ladder
            // rather than measured off the live viewport. The sprites are snapped onto the 1/32 asset
            // lattice by the engine. If the camera were snapped onto some grid that is NOT a whole
            // multiple or a whole division of that lattice, texel boundaries would still land between
            // screen pixels — the snap would look like a fix and change nothing. Every rung must
            // therefore divide the asset pixel a whole number of times, or be a whole number of them.
            for (int step = CameraZoomPolicy.MinStep; step <= CameraZoomPolicy.MaxStep; step++)
            {
                if (step == -1 || step == 0) continue;   // not steps (see CameraZoomPolicy.MinStep)

                float grid = CameraZoomPolicy.WorldUnitsPerRenderedPixel(step, Ppu, ScreenH);
                Assert.Greater(grid, 0f, $"step {step} produced a non-positive grid, which reads as 'do not snap'");

                float ratio = grid > AssetGrid ? grid / AssetGrid : AssetGrid / grid;
                Assert.AreEqual(Mathf.Round(ratio), ratio, 1e-4f,
                    $"step {step}'s grid ({grid}) is {ratio} asset pixels — not a whole number. A camera " +
                    "snapped to it would still leave sprite texels straddling screen pixels, which is " +
                    "the exact artefact this snap exists to remove.");
            }
        }

        // ===== the owner's dial ===============================================================

        [Test]
        public void TheCodeDefault_IsOn_SoAnUnwiredSceneShipsThePixelDiscipline()
        {
            Assert.IsTrue(GameConfig.DefaultPixelGridSnap,
                "integer-pixel movement on the play grid is the bible's own discipline (§3.4, §9.2), " +
                "not an opt-in — OFF exists only so the owner can A/B the pre-fix build against it");

            var fresh = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.IsTrue(fresh.PixelGridSnap, "the field initializer must agree with the const");
            }
            finally { Object.DestroyImmediate(fresh); }
        }

        [Test]
        public void TheShippedAsset_CarriesTheFlag_On()
        {
            // The silent-zero trap (see GameConfigAssetCoverageTests): a field the shipped YAML has
            // never heard of comes back as the C# TYPE default, and for a bool that is FALSE — the
            // fix silently off while every code path reports it on. The general coverage test notices
            // a MISSING key; this one pins the shipped VALUE.
            string yaml = File.ReadAllText(ConfigAssetPath);
            Match m = Regex.Match(yaml, @"^\s*PixelGridSnap:\s*(\d)\s*$", RegexOptions.Multiline);

            Assert.IsTrue(m.Success,
                $"{ConfigAssetPath} carries no PixelGridSnap key — hand-add it in the PR that declared " +
                "the field, or the owner's dial is a zero that cannot be turned on from the inspector");
            Assert.AreEqual("1", m.Groups[1].Value,
                "the shipped asset must ship the snap ON — it is the fix, not the experiment");
        }
    }
}
