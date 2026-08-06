using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One station along a cliff run, sampled off the SAME analytic profile the walk gate reads —
    /// the lip, the foot, and how far it is between them.
    ///
    /// <para><b>Everything here is PLAN geometry plus one elevation drop.</b> The projection into
    /// screen units happens once, in <see cref="CliffWallGeometry"/>, so the sampler never has to
    /// know about the camera and the geometry never has to know about the coast.</para>
    /// </summary>
    public struct CliffWallSample
    {
        /// <summary>The clifftop lip, in world XY — a point on the plateau edge.</summary>
        public Vector2 BrowPlan;

        /// <summary>The foot of the face, in world XY, <b>in plan</b> — where the plunge stops
        /// falling. Not where the toe is DRAWN; see <see cref="CliffWallGeometry.ToeScreen"/>.</summary>
        public Vector2 ToePlan;

        /// <summary>Brow elevation minus toe elevation, in metres. Always positive on a real face.</summary>
        public float DropMetres;

        public CliffWallSample(Vector2 browPlan, Vector2 toePlan, float dropMetres)
        {
            BrowPlan = browPlan;
            ToePlan = toePlan;
            DropMetres = dropMetres;
        }
    }

    /// <summary>
    /// The cliff wall's <b>geometry law</b>: how a run of sampled coast becomes a face quad strip that
    /// the Cliff Face kit v10 can be tiled across honestly.
    ///
    /// <para><b>Pure and engine-light on purpose.</b> Nothing here touches a scene, an asset, the clock
    /// or a random number — it is <see cref="Vector2"/> arithmetic and trigonometry, so every rule below
    /// is asserted headless by <c>CliffWallGeometryTests</c>. That matters more than usual here, because
    /// all three of its failure modes are SILENT: a wall built at the wrong metre scale still renders
    /// perfectly and merely looks like slightly wrong rock.</para>
    ///
    /// <para><b>⭐ THE THREE RULES THIS CLASS EXISTS TO HOLD.</b></para>
    /// <list type="number">
    /// <item><description><b>Height draws at <c>cos 40°</c>, ground travel does not.</b> The project's
    /// shared ¾ camera sits 40° above the horizon (<see cref="SpriteLightMath.CameraElevationDeg"/>), so
    /// one metre of HEIGHT draws <see cref="SpriteLightMath.HeightScale"/> ≈ 0.766 world units up the
    /// screen. A cliff's drop is height; its plunge is plan. They are projected by DIFFERENT terms and
    /// adding them as if they were the same is how a 6 m wall comes out looking like a 9 m one.</description></item>
    /// <item><description><b>The texture is addressed in SURFACE metres, never in height.</b> The kit
    /// bakes 12 × 9 m at 32 px/m measured ALONG THE SLOPING FACE (<c>CliffCatalog.FaceMetresT</c>, and
    /// the rig README §4: "Size the quad by surface length, not by height"). A battered face is longer
    /// than it is tall, so <see cref="SurfaceLengthMetres"/> — not the drop, and not the drawn screen
    /// height — is what the <c>v</c> coordinate counts.</description></item>
    /// <item><description><b>The tiling is baked into the UVs, not asked of the shader.</b>
    /// <c>HiddenHarboursCliffFace.shader</c> DECLARES <c>_TileMetresS</c>/<c>_TileMetresT</c> and never
    /// reads them in the fragment stage — they are documentation, not a transform. So this class emits
    /// UVs already in tile units (<see cref="TileU"/>/<see cref="TileV"/>) and the material is left with
    /// its defaults. Wiring the shader instead would work too, but it would move a pinned constant into
    /// <c>CliffLightMathTests</c>' blast radius for no gain.</description></item>
    /// </list>
    /// </summary>
    public static class CliffWallGeometry
    {
        /// <summary>
        /// Where a sample's toe is DRAWN, in world XY: its plan position pushed down-screen by the drop,
        /// projected at the shared camera's <see cref="SpriteLightMath.HeightScale"/>.
        ///
        /// <para><b>⚠ Both terms push the same way and neither may be dropped.</b> A south-facing face
        /// recedes down-screen in PLAN as well (its toe is south of its brow), so the drawn face is
        /// taller than <c>drop × 0.766</c> alone; an east-facing one recedes sideways instead and the
        /// quad comes out SHEARED. That shear is not a defect — it is what a ¾ view of an east wall
        /// looks like, and squaring it up is what makes a cliff read as a flat painted band.</para>
        /// </summary>
        public static Vector2 ToeScreen(in CliffWallSample s) =>
            new Vector2(s.ToePlan.x, s.ToePlan.y - s.DropMetres * SpriteLightMath.HeightScale);

        /// <summary>How far out the face reaches in PLAN — the horizontal run of the plunge, metres.</summary>
        public static float PlanRunMetres(in CliffWallSample s) => (s.ToePlan - s.BrowPlan).magnitude;

        /// <summary>
        /// The face's length ALONG ITS OWN SURFACE, metres — <c>√(run² + drop²)</c>, the hypotenuse of
        /// the plunge. This is the only length the <c>v</c> coordinate may be measured in (rule 2), and
        /// it is strictly the largest of the three candidates: a battered face is longer than it is tall
        /// and longer than it reaches.
        /// </summary>
        public static float SurfaceLengthMetres(in CliffWallSample s)
        {
            float run = PlanRunMetres(s);
            return Mathf.Sqrt(run * run + s.DropMetres * s.DropMetres);
        }

        /// <summary>
        /// The face's TRUE batter — its angle from horizontal, degrees. 90° is a vertical wall, 48° a
        /// bank. Derived from the analytic profile the sim walks, never authored: the coast plan states a
        /// plunge WIDTH and a toe ELEVATION, and the angle between them is a consequence.
        ///
        /// <para><b>Why it varies along one sector.</b> The plunge width is authored in ELLIPTICAL
        /// distance, so on a 120 × 70 m island a degree of bearing near due south buys less world-metre
        /// run than one near the harbour — the same authored plunge is steeper at the island's flank than
        /// at its end. That is honest, and it is why <see cref="SnapBatterIndex"/> exists.</para>
        /// </summary>
        public static float BatterDegrees(in CliffWallSample s)
        {
            float run = PlanRunMetres(s);
            if (run <= 1e-4f) return 90f;
            return Mathf.Atan2(Mathf.Max(0f, s.DropMetres), run) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Index of the BAKED batter nearest an angle, out of the angles a checkout actually carries.
        ///
        /// <para><b>⚠ The batter must SNAP where the azimuth must not.</b> The shader takes the true,
        /// unsnapped <c>_WallAzimuth</c> so lighting stays continuous along a curving coast — but its
        /// <c>_Batter</c> property says the opposite in as many words: <i>"Must match the batter the face
        /// texture was baked at, or the bedding spacing in the pixels disagrees with the frame the light
        /// is resolved in."</i> The kit respaces its bedding per batter (rig README §4.3: beds are
        /// horizontal in the WORLD, so a sloped surface carries fewer per texture metre), and that
        /// respacing is in the PIXELS. So the drawn angle is quantised to the bake and only the azimuth
        /// runs free.</para>
        /// </summary>
        public static int SnapBatterIndex(float batterDegrees, float[] bakedAngles)
        {
            if (bakedAngles == null || bakedAngles.Length == 0) return 0;
            int best = 0;
            float bestError = Mathf.Abs(batterDegrees - bakedAngles[0]);
            for (int i = 1; i < bakedAngles.Length; i++)
            {
                float e = Mathf.Abs(batterDegrees - bakedAngles[i]);
                if (e < bestError) { bestError = e; best = i; }
            }
            return best;
        }

        /// <summary>
        /// How many face tiles wide a run of shore is: plan arc length over the kit's 12 m period.
        /// Fractional by design — the face is exactly periodic in <c>s</c> and imported Repeat, so a run
        /// stops wherever the coast does rather than rounding to a whole tile.
        ///
        /// <para><b>⭐⭐ <paramref name="alongShoreMetres"/> IS MEASURED FROM THE START OF THE RUN, NOT OF
        /// THE CHUNK — and getting that wrong is the whole of the B3 "cliff gaps" defect.</b> A chunk is
        /// a SLICE of a run, cut for sorting reasons alone (see
        /// <c>StPetersCliffWalls.ChunkToeSpanMetres</c>), so nothing about where the slice falls may
        /// change the picture. Restart this at zero per chunk and two things break at once, both
        /// MEASURED on the real coast:</para>
        /// <list type="number">
        /// <item><description>The rock TEXTURE jumps, because the shared boundary station is addressed at
        /// <c>u</c> by one chunk and at 0 by the next — a mean of <b>3.30 m</b> and a worst of 6.00 m of
        /// the kit's 12 m period, at <b>76 of the coast's 78 chunk boundaries</b>.</description></item>
        /// <item><description>Worse, the plan-displacement profile is sampled at that same <c>u</c>, so
        /// the two copies of the shared station are pushed DIFFERENT distances along the outward normal
        /// and the overlap that exists to prevent a hole is torn open instead. Measured against the rig
        /// itself: <b>RMS 0.40 m and up to 1.10 m</b> at the coast's mean cut. A metre of open sky, 76
        /// times.</description></item>
        /// </list>
        /// <para>So the offset is a RUN quantity that the builder carries across every cut, and
        /// <see cref="RowsBasisSurfaceMetres"/> is its twin for the other axis.</para>
        /// </summary>
        public static float TileU(float alongShoreMetres, float faceMetresS) =>
            faceMetresS <= 0f ? 0f : alongShoreMetres / faceMetresS;

        /// <summary>
        /// Wrap a tile coordinate into <c>[0,1)</c> for a CPU texture read.
        ///
        /// <para><b>⚠ For the CPU sample ONLY — never for the mesh UVs.</b> The GPU wraps a Repeat
        /// texture itself and interpolates <c>u</c> correctly across a period boundary (0.98 → 1.02); a
        /// UV that had been wrapped per-vertex would read 0.98 → 0.02 and run the whole texture backwards
        /// between those two columns. <see cref="UnityEngine.Texture2D.GetPixelBilinear"/> is the one that
        /// needs it, and doing it here rather than trusting the importer means a profile that arrived
        /// Clamp still samples the right texel instead of smearing its last column down a whole run.</para>
        /// </summary>
        public static float WrapTile01(float tiles) => tiles - Mathf.Floor(tiles);

        /// <summary>
        /// The surface length a whole RUN's row count is derived from — the largest face anywhere in it.
        ///
        /// <para><b>Why the run and not the chunk.</b> Rows are how finely the profile displacement is
        /// resolved, and the displaced face is a CURVE. Two chunks that meet at a shared station but
        /// subdivide it differently approximate that curve with different polylines, so the shared edge
        /// does not close — the T-junction case of the same "a slice must not change the picture" rule
        /// <see cref="TileU"/> states. MEASURED before the fix: <b>26 of the coast's 76 cuts</b> joined
        /// chunks with different row counts.</para>
        /// </summary>
        public static float RowsBasisSurfaceMetres(
            System.Collections.Generic.IReadOnlyList<CliffWallSample> samples)
        {
            if (samples == null) return 0f;
            float longest = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                CliffWallSample s = samples[i];
                longest = Mathf.Max(longest, SurfaceLengthMetres(in s));
            }
            return longest;
        }

        /// <summary>
        /// ⭐ <b>THE SOIL HORIZON, AS THE FACE SEES IT.</b> A depth of overburden is a VERTICAL thickness
        /// — a soil profile is so many metres deep, whatever the ground below it does — but a face is
        /// addressed in SURFACE metres, so a battered wall presents more of it than a vertical one.
        /// <c>overburden / sin(batter)</c>, the same conversion the kit's README applies to a bank's
        /// height ("an 8 m bank at 48° needs 8/sin(48°) of t").
        ///
        /// <para>This is what makes ONE number express the owner's whole brief. He asked for stratified
        /// faces — eroded topsoil above, red sandstone below — but also said solid rock is right in SOME
        /// places. A soil horizon of fixed depth gives both without a second dial: on St Peters' 10 m
        /// faces it is a sixth of the wall and the cliff reads as rock with a soil lip, while on its
        /// shortest 2.85 m ones it is over half and the same cliff reads as an eroding bank. Height does
        /// the art direction, because on a real coast it does.</para>
        /// </summary>
        public static float OverburdenSurfaceMetres(float overburdenMetres, float batterDegrees)
        {
            float sin = Mathf.Sin(Mathf.Clamp(batterDegrees, 1f, 90f) * Mathf.Deg2Rad);
            return Mathf.Max(0f, overburdenMetres) / Mathf.Max(sin, 0.02f);
        }

        /// <summary>
        /// How many face tiles LONG a face is, measured down its own surface (rule 2). An 8 m bank at 48°
        /// is 10.8 m of surface = 1.2 tiles, and sizing it by the 8 m drop instead is the mistake the
        /// kit's README calls out by name.
        /// </summary>
        public static float TileV(float surfaceMetres, float faceMetresT) =>
            faceMetresT <= 0f ? 0f : surfaceMetres / faceMetresT;

        /// <summary>
        /// How many rows to subdivide a face into so the profile displacement has somewhere to land.
        /// The README asks for 0.25 m along the wall (<c>CliffCatalog.ProfileSubdivideMetres</c>); at
        /// 32 px/m that is 8 texels a step, which is as fine as the displacement map can express.
        /// Always at least one row, so a degenerate sample still yields a quad rather than nothing.
        /// </summary>
        public static int RowsFor(float surfaceMetres, float subdivideMetres) =>
            Mathf.Max(1, Mathf.CeilToInt(surfaceMetres / Mathf.Max(0.01f, subdivideMetres)));

        /// <summary>
        /// The wall's outward PLAN normal at a sample — brow to toe, normalised. The direction profile
        /// displacement pushes along, and the direction whose compass azimuth becomes
        /// <c>_WallAzimuth</c>.
        ///
        /// <para>Falls back to due south on a degenerate (zero-run, vertical) sample: a wall with no plan
        /// run has no derivable facing, and south is the aspect the kit treats as canonical.</para>
        /// </summary>
        public static Vector2 OutwardPlan(in CliffWallSample s)
        {
            Vector2 d = s.ToePlan - s.BrowPlan;
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.down;
        }

        /// <summary>
        /// Compass azimuth (degrees, N = 0, clockwise) of a plan direction — the frame the whole cliff
        /// stack is written in (<c>CoastPlan.AspectAzimuths</c>, <c>CliffCatalog.AspectAzimuths</c>, and
        /// the shader's <c>_WallAzimuth</c>). <c>atan2(east, north)</c>, not the other way round: a
        /// mislabelled compass is this repo's most-repeated art defect.
        /// </summary>
        public static float AzimuthOf(Vector2 planDirection)
        {
            if (planDirection.sqrMagnitude < 1e-12f) return 180f;
            float a = Mathf.Atan2(planDirection.x, planDirection.y) * Mathf.Rad2Deg;
            return a < 0f ? a + 360f : a;
        }

        /// <summary>
        /// Decode one texel of the kit's plan-displacement profile into metres:
        /// <c>(grey − 0.5) × 2 × CliffCatalog.ProfileMetres</c>, exactly the decode the baker's grey
        /// encoding was written against.
        ///
        /// <para><b>⚠ The scale is a parameter, not a literal.</b> <c>CliffBaker</c> throws if the rig's
        /// own <c>metres</c> and the catalog's <c>ProfileMetres</c> ever disagree — but the catalog lives
        /// in an Editor assembly this module cannot reference, so the number is passed in and the
        /// builder is what reads it from the one true place.</para>
        /// </summary>
        public static float ProfileMetresFromGrey(float grey01, float profileMetres) =>
            (grey01 - 0.5f) * 2f * profileMetres;

        /// <summary>
        /// The world Y a chunk SORTS at: the lowest drawn toe over its stations.
        ///
        /// <para><b>Why the lowest and not the mean.</b> The order must satisfy two inequalities at once
        /// — over everything at the brow, under everything at the foot — and only the extreme toe
        /// guarantees the second for the whole chunk. Using the mean would put the southern end of every
        /// chunk in front of a player standing at its foot.</para>
        ///
        /// <para>Shared by <see cref="CliffWallSurface"/> and by the pins, so a test asserts the
        /// PROPERTY (a brow sorts behind, a foot sorts in front) rather than restating the formula and
        /// agreeing with itself.</para>
        /// </summary>
        public static float SortY(System.Collections.Generic.IReadOnlyList<CliffWallSample> samples)
        {
            if (samples == null || samples.Count == 0) return 0f;
            float lowest = float.MaxValue;
            for (int i = 0; i < samples.Count; i++)
            {
                CliffWallSample s = samples[i];
                float y = ToeScreen(in s).y;
                if (y < lowest) lowest = y;
            }
            return lowest;
        }

        /// <summary>
        /// How far below its own brow a face's toe is DRAWN, in world units — <c>drop × cos 40°</c>.
        ///
        /// <para><b>⭐ WHY THIS NUMBER IS THE ONE THE SORTING RESTS ON.</b> A chunk carries a single
        /// sorting order and takes it from its LOWEST toe. For that to put the wall over everything on
        /// the clifftop, the chunk's brow must nowhere dip below that toe — and this separation is the
        /// slack it has to do it in. A chunk whose brow wanders further than this in Y would sort behind
        /// the ground it stands on.</para>
        ///
        /// <para><b>⚠ It is a SEPARATION, not a chunk length.</b> An earlier draft used it as the cut,
        /// on the reasoning that the brow's Y moves at most a metre per metre of shore — which is true
        /// but irrelevant, because what makes one order dishonest is the Y a chunk spans, not the shore
        /// it covers, and on an east–west coast those differ by six times. The cut lives in
        /// <c>StPetersCliffWalls.ChunkToeSpanMetres</c> and is measured in Y.</para>
        /// </summary>
        public static float BrowToToeSeparationMetres(float dropMetres) =>
            Mathf.Max(0f, dropMetres) * SpriteLightMath.HeightScale;

        // =============================================================================================
        //  the decals — where the kit's brow and toe strips sit against a face
        // =============================================================================================

        /// <summary>
        /// How far INLAND of the brow, in PLAN metres, a brow strip's top edge sits.
        ///
        /// <para>The kit's brow strip is 12 × 4 m and its sod line — <c>CliffCatalog.BrowLineAt</c>, the
        /// rig's own <c>anchor</c> — sits 42% of the way down it. Above that line the strip is drawing the
        /// PLAN surface of the clifftop (grass, seen from above); below it, the face. So the strip
        /// straddles the brow and this is the plan half of it.</para>
        /// </summary>
        public static float BrowDecalInlandMetres(float stripMetresT, float browLineAt) =>
            Mathf.Max(0f, stripMetresT) * Mathf.Clamp01(browLineAt);

        /// <summary>How far DOWN THE SURFACE of the face a brow strip reaches — the rest of the strip.
        /// This is where the sod's undercut shadow, its hanging clods and the soil wash spilling onto the
        /// rock are drawn, and it is why the strip is measured in surface metres like everything else that
        /// touches a face.</summary>
        public static float BrowDecalFaceMetres(float stripMetresT, float browLineAt) =>
            Mathf.Max(0f, stripMetresT) * (1f - Mathf.Clamp01(browLineAt));

        /// <summary>
        /// Where a toe strip's TOP edge sits, as a depth in surface metres measured UP from the drawn toe
        /// — i.e. the strip occupies the last <paramref name="stripMetresT"/> of the face.
        ///
        /// <para><b>⚠ The kit gives no anchor constant for the toe, unlike <c>BrowLineAt</c> for the
        /// brow.</b> Its own note says what the strip contains — the undercut notch the sea cuts at
        /// high water, the salt-bleached basal beds "in every low-tide photograph", and the debris lying
        /// against the foot — all of which are features OF the bottom of the face. MEASURED on the baked
        /// strip, its alpha is zero across the top quarter and only reaches full weight in the bottom
        /// three-eighths, so seating its bottom edge on the drawn toe puts the notch and the bleached
        /// band where they belong and lets the top fade out onto plain rock. Anything else would have to
        /// invent an anchor the rig does not state.</para>
        /// </summary>
        public static float ToeDecalFaceMetres(float stripMetresT) => Mathf.Max(0f, stripMetresT);
    }
}
