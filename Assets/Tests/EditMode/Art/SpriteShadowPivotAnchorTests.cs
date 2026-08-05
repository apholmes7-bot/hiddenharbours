using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The shadow stands at its caster's feet.</b> A projected shadow is pinned where its caster meets the
    /// ground, and across every rig family that point is the sprite's PIVOT (ADR 0026 — a tree's pivot is its
    /// TRUNK FOOT, a character's is <c>(0.5, GroundInsetPx/cellH)</c>, a shrub's and a shore plant's is the
    /// root crown). <see cref="SpriteShadow.PivotShearMap(Sprite)"/> is the whole of that claim in arithmetic:
    /// it maps a vertex's <c>uv.y</c> to its height above the pivot in caster heights, so the vertex shear —
    /// which is proportional to height above the GROUND — is zero exactly at the feet.
    ///
    /// <para>🔴 <b>What these would have caught.</b> The shader used to shear by <c>saturate(uv.y)</c>, which
    /// anchors at <c>uv.y == 0</c>. That is wrong twice: <c>uv.y == 0</c> is the bottom of the sprite's CELL
    /// rather than its pivot (every family carries rows under the pivot), and uv is TEXTURE space rather than
    /// cell space, so a cell sliced from row 3 of a 4-row sheet never sees a <c>uv.y</c> below 0.75 at all. The
    /// committed geometry in <see cref="Families"/> is the measure of it: the fisher's iso skin stood 12.8 m
    /// from its own shadow at a raking sun. Every case below is asserted through the SAME one-line expression
    /// the vertex stage runs (<see cref="UpFrac"/>), and
    /// <see cref="TheVertexStageStillRunsTheExpressionTheseTestsMirror"/> pins the two together.</para>
    /// </summary>
    public class SpriteShadowPivotAnchorTests
    {
        private const float Ppu = 32f;
        private const float Eps = 1e-4f;

        /// <summary>The longest rake the arc can ask for: <c>DayNightMath.ShadowLength</c> at a sun on the
        /// horizon is <c>_lengthAtHorizon</c> (5), under the <c>_maxLength</c> cap (7). This is the multiplier
        /// the worst-case offsets below are quoted at — the raking dawn/dusk sun, which is exactly when a
        /// shadow is most visible and most beautiful.</summary>
        private const float RakeMul = 5f;

        /// <summary>
        /// The one line the vertex stage runs, mirrored: <c>upFrac = uv.y * _ShadowUV.x + _ShadowUV.y</c>.
        /// Height above the caster's pivot, in caster heights.
        /// </summary>
        private static float UpFrac(Vector2 map, float uvY) => uvY * map.x + map.y;

        /// <summary>What the shader did before the anchor was fixed: shear straight off the raw texture uv.</summary>
        private static float UpFracBeforeTheFix(float uvY) => Mathf.Clamp01(uvY);

        // =====================================================================================
        //  the shipped caster population, measured off the committed .meta slices
        // =====================================================================================

        /// <summary>
        /// A real committed caster: the sheet it is sliced from, the cell, and where its pivot sits. Read
        /// straight out of the <c>.png.meta</c> sprite rects, so these are the actual numbers the shipped
        /// casters carry, not a restatement of the component.
        /// </summary>
        private readonly struct Caster
        {
            public readonly string Name;
            public readonly float SheetH, RectBottom, CellH, PivotNorm;
            public Caster(string name, float sheetH, float rectBottom, float cellH, float pivotNorm)
            { Name = name; SheetH = sheetH; RectBottom = rectBottom; CellH = cellH; PivotNorm = pivotNorm; }

            public float PivotPx => PivotNorm * CellH;
            /// <summary>Caster height in local units — the sprite bounds height, what the shear scales by.</summary>
            public float LocalHeight => CellH / Ppu;
            /// <summary>The texture-space uv.y the caster's PIVOT sits at — where the shear must be zero.</summary>
            public float PivotUv => (RectBottom + PivotPx) / SheetH;
            /// <summary>The shear length the component publishes at the longest rake, in local units.</summary>
            public float RakeLen => RakeMul * LocalHeight;
            public Vector2 Map => SpriteShadow.PivotShearMap(SheetH, RectBottom, PivotPx, Ppu, LocalHeight);
        }

        /// <summary>
        /// One caster per shipped family, each on the row that shows the defect worst (the TOP row of its
        /// sheet, which is the row Unity's slicer writes first). Sources:
        /// <list type="bullet">
        /// <item><c>Art/Foliage/Trees/WhitePine_mature_summer.png.meta</c> — 153×191 cell, ONE row, pivot
        /// 0.05759 (the trunk foot, 11 px up over the near-root flare). Tight mesh.</item>
        /// <item><c>Art/Foliage/Shrubs/Meadowsweet_full_atGreen.png.meta</c> — 55×59 cell, FOUR sway rows in a
        /// 236 px sheet, pivot 0.16949 (root crown, 10 px up).</item>
        /// <item><c>Art/Sprites/Shore/Plants/Cattail_summer_full_v0.png.meta</c> — 90×73 cell, FOUR rows in a
        /// 292 px sheet, pivot 0.17808 (root crown, 13 px up).</item>
        /// <item><c>Art/Characters/Iso/Fisher_bite.png.meta</c> — 64×92 cell, EIGHT facing rows in a 736 px
        /// sheet, pivot 0.10870 (= GroundInsetPx 10 / 92, CharacterSheetSlicer's ground contact).</item>
        /// </list>
        /// </summary>
        private static readonly Caster[] Families =
        {
            new Caster("WhitePine (tree, 1 row)",        191f, 0f,   191f, 0.05759f),
            new Caster("Meadowsweet (shrub, 4 rows)",    236f, 177f,  59f, 0.16949f),
            new Caster("Cattail (shore plant, 4 rows)",  292f, 219f,  73f, 0.17808f),
            new Caster("Fisher iso (player, 8 rows)",    736f, 644f,  92f, 0.10870f),
        };

        // =====================================================================================
        //  the defect
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>THE REGRESSION TEST.</b> The handoff's case exactly: a caster with a deliberately off-centre
        /// pivot — a tall cell, the feet at the pivot, empty headroom above — sliced from a multi-row sheet.
        /// Its raking shadow must root AT the pivot, to within a pixel.
        ///
        /// <para>Both halves matter and both fail on the pre-fix path. The first says the feet are on the feet.
        /// The second says the shadow is still a SHADOW while they are — a map that anchored everything at zero
        /// would satisfy the first and draw an unsheared blob, and that is precisely the shape a missing
        /// <c>_ShadowUV</c> takes (an unpublished vector reads back as <c>(0,0,0,0)</c>).</para>
        /// </summary>
        [Test]
        public void ACasterWithAnOffCentrePivot_RootsItsRakingShadowAtThePivot_NotTheCellBottom()
        {
            // A 32-wide, 96-tall cell on the top row of a 4-row (384 px) sheet. The feet sit 24 px up — a
            // quarter of the way — with empty headroom above, so cell-bottom and pivot are unmistakably apart.
            var caster = new Caster("off-centre-pivot fixture", 384f, 288f, 96f, 0.25f);
            Vector2 map = caster.Map;

            float atPivot = UpFrac(map, caster.PivotUv);
            Assert.AreEqual(0f, atPivot, Eps,
                $"The shear is {atPivot:F4} caster-heights at the caster's own pivot, so its shadow is rooted " +
                $"{Mathf.Abs(atPivot) * caster.RakeLen:F2} m away from its feet at a raking sun. The shadow " +
                "must be anchored where the caster meets the ground, and that point is the PIVOT.");

            // One caster-height above the feet is one full shear length along the ground — the head of the
            // silhouette. Without this the assert above is satisfied by a shadow that does not shear at all.
            float oneHeightUp = caster.PivotUv + caster.LocalHeight * Ppu / caster.SheetH;
            Assert.AreEqual(1f, UpFrac(map, oneHeightUp), Eps,
                "A point one caster-height above the feet must land one full shear length down the ground. " +
                "The silhouette is anchored but no longer rakes — the shadow has stopped being a shadow.");

            // And state what the pre-fix path did with the very same caster, which is why this test exists.
            float before = UpFracBeforeTheFix(caster.PivotUv) * caster.RakeLen;
            Assert.Greater(before, 1f,
                "sanity: this fixture is supposed to be one the OLD raw-uv.y shear got badly wrong");
            Debug.Log($"[SpriteShadowPivotAnchor] off-centre fixture: pivot at uv.y {caster.PivotUv:F4}; " +
                      $"pre-fix the shadow rooted {before:F2} m from the feet at a raking sun, now " +
                      $"{Mathf.Abs(atPivot) * caster.RakeLen:F4} m.");
        }

        /// <summary>
        /// The same claim, swept across one real caster from every shipped family — the trees, the shrubs, the
        /// shore plants and the fisher. This is the population the fix had to come right in one motion, and the
        /// logged metres are the re-derivation: what each family's shadow was standing away from its own feet.
        /// </summary>
        [Test]
        public void EveryShippedCasterFamily_RootsItsShadowAtItsPivot()
        {
            foreach (Caster c in Families)
            {
                Vector2 map = c.Map;
                float atPivot = UpFrac(map, c.PivotUv);
                float driftNow = Mathf.Abs(atPivot) * c.RakeLen;
                float driftBefore = UpFracBeforeTheFix(c.PivotUv) * c.RakeLen;

                Debug.Log($"[SpriteShadowPivotAnchor] {c.Name}: pivot at uv.y {c.PivotUv:F4}, " +
                          $"rake {c.RakeLen:F2} m · root drift before {driftBefore:F2} m, now {driftNow:F4} m");

                Assert.AreEqual(0f, atPivot, Eps,
                    $"{c.Name} roots its raking shadow {driftNow:F2} m from its own feet.");

                // Half a pixel at PPU 32 — the same "underfoot" tolerance the PlayMode suite uses.
                Assert.Less(driftNow, 1f / 64f, $"{c.Name} is off its feet by more than half a pixel.");
            }
        }

        /// <summary>
        /// 🔴 <b>THE NEGATIVE CONTROL.</b> A caster whose pivot IS its cell-bottom-centre AND whose sprite
        /// fills its whole texture is the one case the pre-fix shear had right, so it must not move by so much
        /// as a float bit: its map is the identity, <c>upFrac == uv.y</c>, byte-for-byte the old path.
        ///
        /// <para>This is not a hypothetical shape. It is what every fixture in the shadow suites is
        /// (<c>Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f)</c>), which is why those
        /// suites' numbers are expected to come through this change untouched.</para>
        /// </summary>
        [Test]
        public void ACasterPivotedAtItsCellBottomFillingItsTexture_IsUntouched()
        {
            // 16×48 at PPU 32, pivot (0.5, 0), one sprite filling the texture — the shadow suites' fixture.
            Vector2 map = SpriteShadow.PivotShearMap(48f, 0f, 0f, Ppu, 48f / Ppu);

            Assert.AreEqual(SpriteShadow.IdentityShearMap.x, map.x, 0f,
                $"The negative control moved: {map}. A caster already anchored at its cell bottom must map to " +
                "the identity and render exactly as it did, or this fix is not a fix but a change of look.");
            Assert.AreEqual(SpriteShadow.IdentityShearMap.y, map.y, 0f,
                $"The negative control moved: {map}. A caster already anchored at its cell bottom must map to " +
                "the identity and render exactly as it did, or this fix is not a fix but a change of look.");

            foreach (float uvY in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                Assert.AreEqual(UpFracBeforeTheFix(uvY), UpFrac(map, uvY), 0f,
                    $"At uv.y {uvY} the identity map disagrees with the pre-fix shear — not byte-identical.");
        }

        /// <summary>
        /// The rows a rig deliberately carries UNDER its pivot — a tree's near-root flare, which projects below
        /// the trunk foot under the 40° camera — are below the ground plane, and the shear must take them the
        /// other way rather than clamping them into a smear at the anchor. (The pre-fix path saturated, so it
        /// could not express this at all.)
        /// </summary>
        [Test]
        public void TheRowsBeneathThePivot_RakeBackwards_BecauseTheyAreBelowTheGround()
        {
            Caster pine = Families[0];
            Vector2 map = pine.Map;

            float atCellBottom = UpFrac(map, pine.RectBottom / pine.SheetH);
            Assert.Less(atCellBottom, 0f,
                $"The white pine's near-root flare sits {pine.PivotPx:F0} px BELOW its trunk foot, so it is " +
                $"below the ground plane and must rake back toward the sun. Got {atCellBottom:F4}.");
            Assert.AreEqual(-pine.PivotPx / Ppu / pine.LocalHeight, atCellBottom, Eps,
                "The backward rake is not proportional to how far below the pivot the flare actually hangs.");
        }

        // =====================================================================================
        //  guards
        // =====================================================================================

        /// <summary>
        /// Degenerate input must fall back to the identity map, not to a divide-by-zero that would fling the
        /// silhouette to infinity (or NaN it out of existence) the first frame a caster is half-built.
        /// </summary>
        [Test]
        public void DegenerateSpriteGeometry_FallsBackToTheIdentityMap()
        {
            Assert.AreEqual(SpriteShadow.IdentityShearMap, SpriteShadow.PivotShearMap(0f, 0f, 0f, 32f, 1.5f),
                "a sheet with no height");
            Assert.AreEqual(SpriteShadow.IdentityShearMap, SpriteShadow.PivotShearMap(256f, 0f, 0f, 0f, 1.5f),
                "a sprite with no PPU");
            Assert.AreEqual(SpriteShadow.IdentityShearMap, SpriteShadow.PivotShearMap(256f, 0f, 0f, 32f, 0f),
                "a caster with no height");
            Assert.AreEqual(SpriteShadow.IdentityShearMap, SpriteShadow.PivotShearMap(null),
                "no sprite at all");
        }

        /// <summary>
        /// 🔴 These tests assert through <see cref="UpFrac"/>, a C# mirror of a line of HLSL — so they are only
        /// evidence for as long as the vertex stage still runs that line. This reads the shader and checks.
        /// </summary>
        [Test]
        public void TheVertexStageStillRunsTheExpressionTheseTestsMirror()
        {
            string shader = System.IO.File.ReadAllText(
                "Assets/_Project/Art/Shaders/HiddenHarboursSpriteShadow.shader");

            StringAssert.Contains("float upFrac = IN.uv.y * _ShadowUV.x + _ShadowUV.y;", shader,
                "The vertex stage no longer computes upFrac the way UpFrac() above mirrors it, so every " +
                "assert in this file has quietly stopped being about the shipped shader. Update both together.");
            StringAssert.DoesNotContain("saturate(IN.uv.y)", shader,
                "The shear is back on raw texture uv, which anchors the silhouette at the bottom of the " +
                "SHEET rather than at the caster's pivot. That is the defect this file exists for.");
        }
    }
}
