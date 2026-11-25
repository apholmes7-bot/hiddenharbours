using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A flying caster casts its WHOLE silhouette.</b> The screen-space shade arm refuses to darken its own
    /// caster: at each shadow fragment it looks up the caster's texel at the same screen point and discards if
    /// that texel is opaque. The lookup assumed the caster stands on its shadow's anchor. A gull does not - the
    /// flock lifts its transform by its altitude and sets <see cref="SpriteShadow.FootOffset"/> to match, so
    /// the shadow stays on the ground under it - and the lookup found the bird's own outline sitting on its
    /// shadow, offset only by the small shear, and punched the shadow down to a rim and two wingtips (owner,
    /// 2026-11-15: "only take to certain parts of the gull. Not the whole silhouette").
    ///
    /// <para>The fix carries the foot offset to the shader in <c>_ShadowUVPerUnit.z</c>, and the vertex stage
    /// subtracts it. These tests assert through <see cref="SpriteShadow.CasterSelfTexelUv"/>, the C# mirror of
    /// that lookup, and <see cref="TheShaderStillRunsTheLookupTheseTestsMirror"/> pins the two together.</para>
    /// </summary>
    public class SpriteShadowFlyingCasterTests
    {
        // A gull-sized cell: 32 px square at 32 ppu on a 32 px sheet -> one object unit is one uv.
        private static readonly Vector2 UvPerUnit = new Vector2(1f, 1f);
        // A noon shear: short, and pointing a little down-screen, as the arc gives at midday.
        private static readonly Vector2 NoonShear = new Vector2(0.05f, -0.15f);

        private static bool InsideUnitUv(Vector2 uv) => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;

        /// <summary>
        /// THE REGRESSION TEST. A gull 3 m up: from every fragment of its shadow the caster's texel must be
        /// OFF the sprite (no self-exclusion possible), so the shadow is the whole silhouette. Before the fix
        /// (foot offset 0 in the lookup) the same fragments landed back inside the bird's own cell.
        /// </summary>
        [Test]
        public void AGullAtAltitude_NeverFindsItsOwnTexelUnderItsShadow()
        {
            const float altitude = 3f;
            int punchedBefore = 0, punchedAfter = 0, samples = 0;
            for (float u = 0.05f; u < 1f; u += 0.1f)
            for (float v = 0.05f; v < 1f; v += 0.1f)
            {
                var uv = new Vector2(u, v);
                Vector2 shear = NoonShear * v;   // the vertex shear grows with height above the pivot
                samples++;
                if (InsideUnitUv(SpriteShadow.CasterSelfTexelUv(uv, shear, 0f, UvPerUnit))) punchedBefore++;
                if (InsideUnitUv(SpriteShadow.CasterSelfTexelUv(uv, shear, altitude, UvPerUnit))) punchedAfter++;
            }
            Assert.Greater(punchedBefore, samples / 2,
                "The pre-fix lookup should have found the gull's own cell under most of its shadow - that is " +
                "the defect. If it does not, this test has stopped describing it.");
            Assert.AreEqual(0, punchedAfter,
                $"{punchedAfter}/{samples} shadow fragments of a gull {altitude} m up still find the bird's own " +
                "texel, so its shadow is cut by an outline that is not there.");
        }

        /// <summary>Everything that stands on its shadow (foot offset 0) looks up exactly what it did before.</summary>
        [Test]
        public void AGroundedCaster_LooksUpExactlyWhatItAlwaysDid()
        {
            var uv = new Vector2(0.3f, 0.7f);
            Vector2 shear = NoonShear * 0.7f;
            Vector2 before = uv + Vector2.Scale(shear, UvPerUnit);
            Assert.AreEqual(before, SpriteShadow.CasterSelfTexelUv(uv, shear, 0f, UvPerUnit));
        }

        /// <summary>
        /// A gull skimming low still overlaps its shadow, and there it must still not darken itself: the
        /// lookup lands on the bird's texel exactly where the drawn bird covers the shadow.
        /// </summary>
        [Test]
        public void ALowGull_StillExcludesItselfWhereItReallyOverlapsItsShadow()
        {
            const float altitude = 0.25f;
            var uv = new Vector2(0.5f, 0.1f);                 // a shadow fragment near the shadow's base
            Vector2 self = SpriteShadow.CasterSelfTexelUv(uv, Vector2.zero, altitude, UvPerUnit);
            Assert.AreEqual(uv.y - altitude, self.y, 1e-6f,
                "The caster's texel is the shadow's uv moved DOWN the cell by the altitude: the bird is drawn " +
                "that far above the anchor, so its texel at this screen point is that much lower in its cell.");
        }

        /// <summary>The foot offset is carried into the shadow's units by the two lossy scales.</summary>
        [Test]
        public void TheFootOffset_IsCarriedAcrossTheTwoScales()
        {
            Assert.AreEqual(3f, SpriteShadow.FootOffsetInShadowUnits(3f, 1f, 1f), 1e-6f, "the planted child");
            Assert.AreEqual(6f, SpriteShadow.FootOffsetInShadowUnits(3f, 2f, 1f), 1e-6f, "a caster scaled up");
            Assert.AreEqual(3f, SpriteShadow.FootOffsetInShadowUnits(3f, -1f, 1f), 1e-6f, "a flipped caster");
            Assert.AreEqual(3f, SpriteShadow.FootOffsetInShadowUnits(3f, 1f, 0f), 1e-6f,
                "a degenerate shadow scale passes the offset through rather than dividing by zero");
        }

        /// <summary>
        /// On a real sheet the lookup must stay inside the caster's OWN cell. A gull is one cell of a sheet
        /// with eight bearing rows: displaced down by its altitude, the lookup lands on the row below, which
        /// holds another bird. Bounded by the whole sheet (the old bound) that would punch the shadow with a
        /// different frame; bounded by <see cref="SpriteShadow.CellUvRect(Rect,int,int)"/> it cannot.
        /// </summary>
        [Test]
        public void TheSelfLookup_IsBoundedByTheCastersOwnCell_NotTheSheet()
        {
            const int cellPx = 32, rows = 8, sheetW = 256, sheetH = cellPx * rows;
            const float ppu = 32f;
            var rect = new Rect(0f, 5 * cellPx, cellPx, cellPx);          // row 5 of 8
            Vector4 cell = SpriteShadow.CellUvRect(rect, sheetW, sheetH);
            Assert.AreEqual(new Vector4(0f, 5f / 8f, 1f / 8f, 6f / 8f), cell, "the cell in uv");

            var perUnit = new Vector2(ppu / sheetW, ppu / sheetH);
            var uv = new Vector2(0.5f / 8f, 5.5f / 8f);                   // the middle of the cell
            Vector2 self = SpriteShadow.CasterSelfTexelUv(uv, Vector2.zero, 1.5f, perUnit);   // 1.5 m up
            bool inSheet = self.x >= 0f && self.x <= 1f && self.y >= 0f && self.y <= 1f;
            bool inCell = self.x >= cell.x && self.y >= cell.y && self.x <= cell.z && self.y <= cell.w;
            Assert.IsTrue(inSheet, "the displaced lookup lands on ANOTHER row of the sheet - the trap");
            Assert.IsFalse(inCell, "and the cell bound refuses it, so no other frame punches this shadow");

            Assert.AreEqual(SpriteShadow.FullSheetUvRect, SpriteShadow.CellUvRect(rect, 0, sheetH),
                "a degenerate texture falls back to the whole sheet, the bound the lookup always had");
        }

        /// <summary>
        /// These tests assert through a C# mirror of the vertex stage's line, so they are evidence only while
        /// the shader still runs that line.
        /// </summary>
        [Test]
        public void TheShaderStillRunsTheLookupTheseTestsMirror()
        {
            string shader = System.IO.File.ReadAllText(
                "Assets/_Project/Art/Shaders/HiddenHarboursSpriteShadow.shader");
            StringAssert.Contains(
                "OUT.shearUV = (shear - float2(0.0, _ShadowUVPerUnit.z)) * _ShadowUVPerUnit.xy;", shader,
                "The vertex stage no longer subtracts the caster's foot offset from the self-test lookup, so a " +
                "flying gull's shadow is punched by its own outline again.");
            StringAssert.Contains("float2 uvSelf = IN.uv + IN.shearUV;", shader,
                "The fragment's self-test lookup changed; CasterSelfTexelUv no longer mirrors it.");
            StringAssert.Contains("all(uvSelf >= _ShadowSelfRect.xy) && all(uvSelf <= _ShadowSelfRect.zw)", shader,
                "The self lookup is no longer bounded by the caster's own cell, so a lifted gull's lookup can " +
                "land on a neighbouring frame of its sheet and punch it into the shadow.");

            string component = System.IO.File.ReadAllText("Assets/_Project/Code/Art/SpriteShadow.cs");
            StringAssert.DoesNotContain("new Vector4(_uvPerUnit.x, _uvPerUnit.y, 0f, 0f)", component,
                "A _ShadowUVPerUnit write publishes a zero foot offset again; every write must go through " +
                "UvPerUnitBlock() so a lifted caster's block carries its offset.");
            StringAssert.Contains("SyncFootOffset();", component,
                "The per-frame foot-offset sync is gone: a gull's altitude changes every frame it climbs.");
        }
    }
}
