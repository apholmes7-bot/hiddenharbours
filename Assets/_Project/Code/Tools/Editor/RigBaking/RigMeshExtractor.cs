using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One flat-shaded facet, exactly as the rig's own face list holds it.</summary>
    public sealed class RigFace
    {
        /// <summary>Rig-space object coordinates, metres, z-up. Doubles because the rig is
        /// JavaScript and every number in it is a double; quantising to float is a decision the
        /// MESH makes (see <see cref="RigMeshBuilder"/>), not one extraction should make for it.</summary>
        public Vector3d[] V;
        /// <summary>Index into <see cref="RigMeshData.Materials"/>.</summary>
        public int Mat;
        /// <summary>Per-face shade bias — the rig's <c>f.b</c>. Also the flag for the rig's
        /// interior/backface rescue: <c>b &lt;= -1</c> opts a face into it.</summary>
        public double B;
        /// <summary>
        /// Per-face depth bias toward the camera — the rig's <c>f.db</c>. Reaches the mesh as
        /// <b>UV0.z</b>, which the facet vertex program subtracts from clip depth while leaving the
        /// true depth (<c>o.wpos.z</c>) alone.
        ///
        /// <para>⚠️ <b>This is the cutaway's fore lever, and it was already here.</b> The
        /// interior-mesh spike measured that the level swap ALONE leaves a revealed room 20.3%
        /// visible — the hull's own near topsides stand between the camera and a cabin sole in a ¾
        /// view, and a sheet never met that because a sheet composites OVER the hull. <c>db</c> in
        /// UV0.z is what reproduces the sprite's compositing inside the depth test, and it took the
        /// same room from 20.3% to 97.6%. Nothing about the level tag may quietly drop it; the
        /// pass-3 hulls' bake pins it (<c>HullLevelTagBakeTests</c>) precisely so a future re-bake
        /// cannot. See <c>docs/design/spikes/interior-mesh-verdict.md</c> §B.</para>
        /// </summary>
        public double Db;

        /// <summary>
        /// <b>Which level this face DECLARES itself to belong to</b> — the pass-3 rigs' per-face
        /// <c>lv</c>, resolved through that rig's own <c>geometry().ids</c>.
        /// <see cref="RigLevelTags.Untagged"/> on every rig that publishes no level table (every hull
        /// baked before the cutaway kit, and every fitting).
        ///
        /// <para>Read, never derived — see <see cref="RigLevelTags"/> for what derivation was measured
        /// to cost. A rig that publishes a table and hands over a face with no <c>lv</c>, or an
        /// <c>lv</c> the table does not name, is REFUSED at extraction rather than defaulted: the
        /// silent default would be <c>hull</c>, i.e. "never cull this", which is a room that quietly
        /// stops opening.</para>
        /// </summary>
        public int Level = RigLevelTags.Untagged;

        /// <summary>The rig's own name for <see cref="Level"/> (<c>house</c>, <c>cuddy</c>,
        /// <c>rigging</c>…), or null on an untagged rig. Carried for the bake log and for tests that
        /// want to say which room went wrong rather than which integer.</summary>
        public string LevelName;

        /// <summary>
        /// <b>True when this face does NOT move with the fitting's pose</b> — measured, by building the
        /// face list twice at two different poses and seeing which vertices changed
        /// (<see cref="RigPropExtraction.PoseProbeFaceBuilderCall"/>). Always false for a hull.
        ///
        /// <para>⚠️ <b>An outboard is not one rigid body, and nothing before phase 7 had met one.</b>
        /// Both motor rigs build the clamp bracket (and the skiff's tilt-tube cap) through the
        /// IDENTITY placement <c>I</c> rather than through the posed <c>X</c>, because the bracket is
        /// bolted to the transom and the engine swivels ON it. Rotating those faces with the rest
        /// puts the bracket a couple of pixels out and reshades it — measured as a 39–53 px connected
        /// patch, worst at hard-over and full tilt, which is precisely where nobody would notice by
        /// eye.</para>
        /// </summary>
        public bool FixedInPose;
    }

    /// <summary>A MATS entry: a palette ramp plus a constant index offset.</summary>
    public sealed class RigMaterial
    {
        public string Name;
        public Color32[] Ramp;
        /// <summary>The raw hex strings, in ramp order. Kept because the rig's keyline pass
        /// resolves colours by IDENTITY against its ramp table, so the reference rasteriser needs
        /// to dedupe ramps by content — see <see cref="RigMeshReferenceRasterizer"/>.</summary>
        public string[] RampHex;
        public int Off;
    }

    /// <summary>
    /// Everything the rig's renderer is: static geometry, palettes, one fixed light, and two
    /// scalars. This is the whole input to the facet shader — if something the rig draws is not in
    /// here, the golden master will say so in pixels.
    /// </summary>
    public sealed class RigMeshData
    {
        public string RigKey;
        public string GlobalName;
        public List<RigFace> Faces = new List<RigFace>();
        public List<RigMaterial> Materials = new List<RigMaterial>();

        /// <summary>
        /// The JS these faces actually came from, when it was anything other than the rig's static
        /// <c>F</c> — a fitting's builder call, or a generator hull's variant expression. Empty for
        /// the static case, so a def written from it stays empty on every hull baked before
        /// generators existed.
        ///
        /// <para>It exists because eighteen lobster boats out of one file are otherwise
        /// indistinguishable in their own asset: same rig path, same global, different boat. This is
        /// the field that says WHICH one.</para>
        /// </summary>
        public string SourceFaceExpression = "";

        /// <summary>The rig's LN, already normalised by the rig itself.</summary>
        public Vector3d LightN;
        public double Gain, Bias;

        /// <summary>The rig's 4×4 ordered-dither matrix, already in the rig's <c>(v+0.5)/16</c>
        /// form, indexed <c>[x &amp; 3][y &amp; 3]</c>.</summary>
        public double[,] Bayer = new double[4, 4];
        /// <summary>True when <see cref="Bayer"/> came from the rig, false when it fell back to the
        /// canonical matrix. Reported, never assumed — see <see cref="RigMeshSymbols"/>.</summary>
        public bool BayerWasExported;

        /// <summary>
        /// <b>Does the rig's interior/backface rescue need a face to OPT IN?</b> The hull rigs gate it
        /// (<c>if(sh&lt;0 &amp;&amp; ((f.b||0)&lt;=-1))</c>, puntIsoRig.js:152) — the facet shader
        /// reproduces exactly that. <c>skiffMotorRig.js</c>:169 does NOT: it rescues EVERY back-facing
        /// face, and not one of its faces carries a bias at or below −1, so the two rules are
        /// genuinely different functions on that rig rather than the same one written twice.
        ///
        /// <para>Extraction cannot detect this — it is a property of the rig's <c>_paint</c>, not of
        /// its data — so it is declared per fitting on <see cref="RigPropExtraction"/> and adjudicated
        /// in pixels like every other transcription. Default true = the hull rule, so no existing bake
        /// changes.</para>
        /// </summary>
        public bool BackfaceRescueNeedsOptIn = true;

        /// <summary>
        /// <b>Does this render path run the rig's 1 px depth-discontinuity darkening?</b> A hull's
        /// <c>render()</c> passes <c>doEdge = true</c>; every FITTING entry point in the repo passes
        /// false (<c>renderOars</c>, both <c>renderMotor</c>s), and <c>skiffMotorRig</c> has no such
        /// pass at all. Comparing a fitting against an oracle that darkens where the rig does not is
        /// a difference the fixture would blame on the geometry.
        /// </summary>
        public bool DepthEdgeDarkening = true;

        /// <summary>
        /// The rig's own level vocabulary — <c>geometry().ids</c>, name → the int that goes in
        /// TexCoord1.x. EMPTY on every rig that publishes no <c>geometry()</c>, which is every hull
        /// baked before the cutaway kit and every fitting; that emptiness is what keeps their meshes
        /// byte-identical through this change (<see cref="RigMeshBuilder"/> writes no TexCoord1
        /// channel at all without it).
        /// </summary>
        public IReadOnlyDictionary<string, int> LevelIds = RigLevelTables.NoIds;

        /// <summary>
        /// The rig's <c>geometry().levels</c> — one record per WALKABLE level, with the sole, the
        /// ceiling (or a declared open sky) and the <c>BoatInteriorDef</c> level id it is the same
        /// room as. Empty alongside <see cref="LevelIds"/>.
        /// </summary>
        public IReadOnlyList<RigLevelRecord> Levels = RigLevelTables.NoLevels;

        /// <summary>True when this rig declared a level vocabulary, so its faces carry real tags and
        /// the mesh gains a TexCoord1 channel.</summary>
        public bool CarriesLevelTags => LevelIds != null && LevelIds.Count > 0;

        public Color32 Keyline;
        public int W, H;
        /// <summary>Pivot in cell pixels from the TOP-LEFT — the rigs' screen origin, and the
        /// origin the dither grid is phased against (<c>_DitherPhase</c>, ADR 0022).</summary>
        public double PivotX, PivotY;
        public int PxPerMetre;
        public double DefaultElev;

        /// <summary>Which symbols had to be shimmed in. Empty means the art director exported
        /// everything and <see cref="RigMeshExtractor"/>'s widening never ran.</summary>
        public IReadOnlyList<string> ShimmedSymbols = Array.Empty<string>();

        /// <summary>
        /// The subset of <see cref="ShimmedSymbols"/> that did not merely have to be made READABLE
        /// but had to be REBUILT, because the rig has no such symbol at all
        /// (<see cref="RigMeshSymbols.Reconstructions"/> — today: the dory's material table).
        ///
        /// <para>Kept apart from <see cref="ShimmedSymbols"/> because the two are very different
        /// claims: widening asserts nothing about the art, whereas a reconstruction is OUR reading of
        /// what the rig's <c>_paint</c> means and has to be adjudicated in pixels.</para>
        /// </summary>
        public IReadOnlyList<string> ReconstructedSymbols = Array.Empty<string>();

        public int VertexCount
        {
            get { int n = 0; foreach (var f in Faces) n += f.V.Length; return n; }
        }

        /// <summary>How many faces do not move with the pose — see <see cref="RigFace.FixedInPose"/>.</summary>
        public int FixedFaceCount
        {
            get { int n = 0; foreach (var f in Faces) if (f.FixedInPose) n++; return n; }
        }

        /// <summary>
        /// The same rig, described by a SUBSET of its faces — everything else (materials, light,
        /// dither, cell) shared by reference, because it is the same rig either way. Used to build a
        /// fitting's swivelling and fixed halves as two meshes off ONE extraction, so they cannot
        /// disagree about palette or cell.
        /// </summary>
        public RigMeshData WithFaces(List<RigFace> faces) => new RigMeshData
        {
            RigKey = RigKey,
            GlobalName = GlobalName,
            Faces = faces,
            Materials = Materials,
            LightN = LightN,
            Gain = Gain,
            Bias = Bias,
            Bayer = Bayer,
            BayerWasExported = BayerWasExported,
            BackfaceRescueNeedsOptIn = BackfaceRescueNeedsOptIn,
            DepthEdgeDarkening = DepthEdgeDarkening,
            Keyline = Keyline,
            W = W,
            H = H,
            PivotX = PivotX,
            PivotY = PivotY,
            PxPerMetre = PxPerMetre,
            DefaultElev = DefaultElev,
            ShimmedSymbols = ShimmedSymbols,
            ReconstructedSymbols = ReconstructedSymbols,
            // The level table is a property of the RIG, not of any one slice of its faces — a
            // fitting split into swivelling and fixed halves must not have one half forget the
            // vocabulary the other half's tags are written in.
            LevelIds = LevelIds,
            Levels = Levels,
        };

        /// <summary>Fan triangulation, which is what the rig itself does in <c>_paint</c>.</summary>
        public int TriangleCount
        {
            get { int n = 0; foreach (var f in Faces) n += Mathf.Max(0, f.V.Length - 2); return n; }
        }

        public override string ToString() =>
            $"{RigKey}: {Faces.Count} faces / {TriangleCount} tris / {Materials.Count} materials, " +
            $"cell {W}×{H}, pivot ({PivotX},{PivotY}), {PxPerMetre} px/m, elev {DefaultElev}°, " +
            (ShimmedSymbols.Count == 0
                ? "all symbols EXPORTED by the rig"
                : $"SHIMMED: {string.Join(",", ShimmedSymbols)}") +
            (ReconstructedSymbols.Count == 0
                ? ""
                : $" ⚠️ RECONSTRUCTED (rebuilt from the rig's own values, not merely widened): " +
                  $"{string.Join(",", ReconstructedSymbols)}");
    }

    /// <summary>
    /// A double-precision 3-vector. The rig is JavaScript; every coordinate in it is a double, and
    /// the golden master is only meaningful if extraction is lossless. <see cref="Vector3"/> is
    /// float and is introduced deliberately, once, when the Mesh is built.
    /// </summary>
    public readonly struct Vector3d
    {
        public readonly double X, Y, Z;
        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public Vector3 ToVector3() => new Vector3((float)X, (float)Y, (float)Z);
        public override string ToString() =>
            $"({X.ToString("R", CultureInfo.InvariantCulture)}, " +
            $"{Y.ToString("R", CultureInfo.InvariantCulture)}, " +
            $"{Z.ToString("R", CultureInfo.InvariantCulture)})";
    }

    /// <summary>
    /// The closure-private symbols a mesh needs but the rigs' public API does not (yet) expose.
    ///
    /// ⚠️ ADR 0022 open question #4 says the delta is "one property (<c>F,</c>)". MEASURED
    /// 2026-07-20 against lobsterBoatIsoRig.js, sideDraggerIsoRig.js, puntIsoRig.js and
    /// capeIslanderIsoRig.js: it is FIVE. <c>F</c> is private, and so are <c>MATS</c>, <c>GAIN</c>,
    /// <c>BIAS</c> and <c>LN</c>. The individual ramps (HULL, BOOT, …) ARE exported, but the MATS
    /// mapping is not — and the <c>blk</c>/<c>dark</c> aliases exist ONLY as MATS entries with a
    /// negative <c>off</c>, so the exported ramps alone cannot reconstruct the material table.
    /// </summary>
    public static class RigMeshSymbols
    {
        /// <summary>What the art director must export, per rig, for the shim to become dead code.</summary>
        public static readonly string[] Required = { "F", "MATS", "GAIN", "BIAS", "LN" };

        /// <summary>Preferred from the rig when exported, otherwise the canonical matrix below.
        /// Optional because it is identical in every rig inspected — and because the golden master
        /// adjudicates it: a wrong dither matrix is not subtle, it is a visibly different image.</summary>
        public const string OptionalBayer = "BAYER";

        /// <summary>The rig's matrix, already in <c>(v+0.5)/16</c> form.</summary>
        public static readonly int[,] CanonicalBayer =
        {
            { 0, 8, 2, 10 },
            { 12, 4, 14, 6 },
            { 3, 11, 1, 9 },
            { 15, 7, 13, 5 },
        };

        /// <summary>
        /// ⚠️ <b>Rigs that predate a convention, and the JS that RECONSTRUCTS the missing symbol from
        /// the rig's own values.</b>
        ///
        /// <para>The ordinary shim widens the exported literal with <c>MATS:MATS</c> — it makes a
        /// closure-private symbol readable. That only works if the symbol EXISTS. The dory does not
        /// have one: she is the oldest hull rig in the repo and she predates the <c>MATS</c> table
        /// entirely, selecting her two ramps inline instead:</para>
        /// <code>
        ///   if (f.mat==='iron') col = IRON[clamp(idx-2, 0, 2)];
        ///   else                col = RAMP[clamp(idx,   0, 6)];
        /// </code>
        /// <para>which is the canonical <c>M.ramp[clamp(idx + M.off, 0, M.ramp.length-1)]</c> with
        /// <c>{wood:{ramp:RAMP,off:0}, iron:{ramp:IRON,off:-2}}</c> — the clamp bounds ARE the ramp
        /// lengths (RAMP has 7 entries, IRON has 3), so the two forms are the same function, not
        /// approximations of each other. Every face in her list is <c>mat:'wood'</c>; the iron branch
        /// is unreachable in her and is reconstructed anyway because <c>_paint</c> has it.</para>
        ///
        /// <para><b>Order is load-bearing:</b> the face packer resolves an unknown material name to
        /// index 0, so the DEFAULT ramp must be the first key — the same fallback the punt writes
        /// explicitly as <c>MATS[f.mat] || MATS.paint</c>.</para>
        ///
        /// <para><b>This is a transcription, and it is not trusted.</b> Reading a rig and declaring
        /// what it means is exactly how this project has shipped mirrored boats five times. So the
        /// dory's bake is adjudicated in PIXELS against her own renderer, like every other hull —
        /// and a wrong ramp is not a subtle failure, it recolours the entire boat. The proper fix is
        /// still the art director exporting a <c>MATS</c>; on the day she does, deleting the entry
        /// below changes nothing.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
            Reconstructions = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["doryIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MATS"] = "{wood:{ramp:RAMP,off:0},iron:{ramp:IRON,off:-2}}",
                },

                // ---- the outboards (ADR 0022 phase 7) --------------------------------------------
                // Both motor rigs describe their swivel as a PAIR of private consts rather than as a
                // point: `const YA = -L/2 - 0.06, ZT = MOUNT.z;` (punt) and `const L = 7.0,
                // YA = -L/2 - 0.07, ZT = 0.72;` (skiff). Their `mxform(opts)` then tilts about the
                // line {y = 0, z = ZT} and steers about the vertical through {x = 0, y = 0}, before
                // translating the whole thing to (mx, YA, ·) — so the ONE point every real pose is a
                // pure rotation about is (0, YA, ZT). That reading is what this expression asserts,
                // and it is adjudicated in pixels by OutboardPropMeshAcceptanceTests: get it wrong
                // and the engine still swings, but about the wrong point, and the silhouette moves.
                // ---- paint became DATA (small-craft rig kit v2, 2026-07-25) ----------------------
                // ⚠️ The punt and the console skiff no longer HAVE a `MATS` constant. Their pass-2
                // rigs carry named `SCHEMES` and derive every ramp per render:
                //
                //     const pal = palette(opts), MATS = pal.mats, RINDEX = pal.rindex;
                //
                // so the material table is now a function of the colourway, not a property of the
                // rig. Widening `MATS:MATS` therefore fails outright ("MATS is not defined") and
                // BOTH hulls stopped baking. `palette({})` is the rig's own resolver called with no
                // colourway, which its own code resolves to `DEFAULT_SCHEME` — i.e. this pins the
                // bake to each rig's DEFAULT SCHEME (measured: 'harbour-white' on both) and reads
                // the table the rig itself would use, rather than transcribing one here.
                //
                // ⚠️ WHAT THIS DELIBERATELY DOES NOT DO. Choosing a colourway at runtime, and the
                // new per-material `dith` weight the pass-2 console carries (0 = crisp banded
                // paint, 1 = full 4×4 Bayer), are NOT modelled: `RigMeshData.Materials` is
                // {ramp, off} and the reference rasteriser thresholds against a single uniform
                // Bayer value (see its `data.Bayer[x & 3, y & 3]`). Mesh and oracle therefore agree
                // with each other and both dither the console's painted panels where her renderer
                // now wants them crisp. That is a real, visible gap and it is a separate piece of
                // work — one mesh per scheme, or ramps derived at runtime, is an architecture
                // decision, not a bake flag.
                ["puntIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["swivelPt"] = "function(){return [0,YA,ZT];}",
                    ["MATS"] = "palette({}).mats",
                },

                ["consoleIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MATS"] = "palette({}).mats",
                },

                // ---- the lobster boat's paint kit (drop of 2026-08-12) ---------------------------
                // The third hull to lose its `MATS` const to a paint axis, and the first where the
                // swap was adjudicated in pixels BEFORE it landed. Her rig now carries 12 named
                // schemes and derives the table per render (`matsFor(id)` → {MATS, RINDEX}), so the
                // ordinary `MATS:MATS` widening fails exactly as it did on the punt and the console
                // ("MATS is not defined") and the first mesh hull in the game stops baking.
                //
                // `matsFor('gelcoat')` is the rig's own resolver at its own `defaultPaint`, so this
                // pins the bake to the WHITE GELCOAT scheme and reads the table the rig itself would
                // use — the same move as the two above, not a transcription.
                //
                // MEASURED, not argued (V8 harness, 2026-08-12, against the pre-paint rig):
                //   · `matsFor('gelcoat').MATS` ≡ the old `MATS` const — all 11 entries, same KEY
                //     ORDER (hull,boot,cream,deck,grip,glas,blue,steel,iron,blk,dark), same ramps,
                //     same offsets (blk −1, dark −2). Order is load-bearing here: the face packer
                //     resolves an unknown material to index 0.
                //   · Her 676-face list is byte-identical with and without paint — vertices to 1e-9.
                //     Paint moves no vertex, so one mesh serves every scheme.
                //   · All 8 facings render byte-identical to the pre-paint rig at 456×420×4.
                //   · The committed LobsterBoatIsoHullMesh.asset already holds exactly these 11
                //     ramps, so re-baking her is a no-op on every baker-written field.
                // That chain is what makes "unset scheme = today's boat" a fact rather than a hope;
                // HullPaintSchemeBakeTests pins it, and the scheme assets are baked THROUGH this
                // same resolver so no ramp is ever transcribed into C#.
                ["lobsterBoatIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MATS"] = "matsFor('gelcoat').MATS",
                    // Pass 3 (cutaway kit, 2026-08-26): her aft door is a POSED LEAF, not a fitting.
                    // Her own render() draws `F.concat(doorFaces(opts))`, so taking `F` alone bakes a
                    // boat whose mesh is missing geometry her picture draws — the sport fisher's
                    // outrigger lesson, verbatim (see faceList('stowed') below). doorOpen 0 is the
                    // fleet default and the pose every committed sheet was baked at; the leaf's faces
                    // carry lv:'house', so the cutaway takes the door with the room, as it should.
                    // Measured by the #660 lane in node before this entry existed: extracting bare F
                    // leaves 490 px of leaf undrawn on her renders (1.29%, one cluster, 0 silhouette).
                    ["F"] = "F.concat(doorFaces({doorOpen:0}))",
                },

                // ---- the cutaway kit's pass-3 tie pair (2026-08-26) ------------------------------
                // Same posed-leaf rule as the lobster above: pass 3 gave both ships a hinged door
                // whose leaf lives in doorFaces(opts), composed by their own render() as
                // `F.concat(doorFaces(opts))`. Bare `F` was correct for their pass-1 rigs and stops
                // being the drawn boat at pass 3. doorOpen 0 = closed = the fleet default.
                ["sternTrawlerIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["F"] = "F.concat(doorFaces({doorOpen:0}))",
                },

                ["coastalPacketIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["F"] = "F.concat(doorFaces({doorOpen:0}))",
                },

                // ---- the Cape Islander's paint axis (drop of 2026-08-12) -------------------------
                // The FOURTH hull to lose its `MATS` const to a paint axis, and the last one at Nine
                // Mile Creek that could gain one — after her, only a boat with no hull mesh at all is
                // unpaintable. She takes the small craft's API rather than the lobster's, because she
                // is their sibling on this pipeline (her own header says so) and because that API is
                // already exported: `palette({})` needs NO shim, which is why her line in
                // HullPaintSchemeBaker.Fleet is one line.
                //
                // MEASURED, not argued (V8 harness, 2026-08-12, pre-paint rig at bcbadf75 against
                // post-paint, 92/92 checks):
                //   · `palette({}).mats` ≡ the old `MATS` const — all 10 entries, same KEY ORDER
                //     (hull,boot,cream,wood,glas,gold,iron,moto,blk,dark), same ramps, same offsets
                //     (gold −1, blk −2, dark −3). Order is load-bearing: the face packer resolves an
                //     unknown material to index 0, and her `_paint` still falls back to `MATS.hull`.
                //   · `palette({})` ≡ `palette({scheme:'sage-green'})`, so the mesh bake and the
                //     default scheme asset are pinned to one table, not two that happen to agree.
                //   · Her 509-face list is byte-identical with and without paint — every vertex, bias
                //     and depth bias — and does not move after rendering two other colourways. One
                //     mesh serves all eight schemes.
                //   · All 8 facings render byte-identical to the pre-paint rig at 456×420×4: 0 bytes
                //     differ, through the unset default, the named default, AND an unknown scheme id.
                //   · The keyline's reverse ramp index (46 distinct colours, LAST-write-wins over the
                //     same 8 ramps in the same order) is unchanged — that table is what darkens the
                //     far side of a depth step, so a first-wins rewrite would have moved pixels.
                //   · The committed CapeIslanderIsoHullMesh.asset already holds exactly these 10
                //     ramps, so re-baking her is a no-op on every baker-written field.
                // Sabotage-proved: nudging ONE ramp step by 1/255 reddens 128–3,497 bytes per facing,
                // and swapping two keys in the table reddens the order oracle while leaving the
                // pixels alone — which is exactly why the order is asserted separately from them.
                ["capeIslanderIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MATS"] = "palette({}).mats",
                },

                // ---- the lobster-boat GENERATOR (fleet rig pack, 2026-08-13) ----------------------
                // The first rig in the repo that is not one boat. 3 sizes × 2 styles × 3 regions, and
                // no `F` anywhere: her faces come from a private facesFor(V) keyed on the exported
                // resolve(v), so the face list is a FUNCTION OF THE VARIANT.
                //
                // `variantFaces` is the per-FILE half of that — how to reach the builder at all. WHICH
                // variant is the per-HULL half and lives on FleetHull.Extraction (RigHullExtraction).
                // One reconstruction, eighteen boats.
                //
                // ⚠️ NAMED `variantFaces`, NOT `facesFor`/`facesOf`. The widening adds a PROPERTY to
                // the exported literal while the expression is evaluated in the rig's own closure, so
                // a shim named after a rig private happens to work — the zodiac generator already has
                // a private `facesOf` and it resolves fine. It is still a trap for the next reader,
                // and `RigMeshExtractionTests` pins the shim names as absent from the unmodified rigs.
                //
                // MEASURED, not argued (repo's own V8 host, 2026-08-13, shim applied exactly as
                // WidenExportedLiteral applies it):
                //   · She exports NONE of F/MATS/GAIN/BIAS/LN; PX, defaultElev and resolve ARE public.
                //   · All 18 variants build, and all 18 are DISTINCT by a hash over
                //     {mat, b, db, vertices@1e-6} in face order — 591…834 faces. ⚠️ Face COUNT alone
                //     is not an oracle: inshore_hardtop_northumberland and inshore_hardtop_fundy are
                //     both 637 faces and different boats.
                //   · `matsFor('gelcoat').MATS` gives 11 entries in the order
                //     hull,boot,cream,deck,grip,glas,blue,steel,iron,blk,dark — byte-for-byte the key
                //     order recorded for lobsterBoatIsoRig.js above, independently re-confirming that
                //     her paint table IS the hero hull's and that the committed paint.lobster_* defs
                //     cover her. Order is load-bearing: the face packer resolves an unknown material
                //     to index 0.
                ["lobsterBoatVariantsIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["variantFaces"] = "function(v){return facesFor(resolve(v)).F;}",
                    ["MATS"] = "matsFor('gelcoat').MATS",
                },

                // ---- the zodiac GENERATOR (fleet rig pack, 2026-08-12; baked 2026-08-16) ----------
                // The second generator, and the simplest of the three: ONE cell (272×248) serving TWO
                // builds, with `BUILDS` and `buildIds` already public and only the assembler private.
                // So her reconstruction is one line and she needs no scope and no inner widening.
                //
                // ⚠️ `facesOf(BUILDS[b])` rather than the rig's own `facesOf(buildOf({build:b}))`, and
                // the difference is the failure mode. `buildOf` is written to fall back
                // (`BUILDS[o.build] || BUILDS[DEFAULT_BUILD]`), so a mistyped build id would hand back
                // the hurricane silently — the same trap the lobster generator's `resolve` carries and
                // the sport fisher's `byId` carries. Indexing BUILDS directly means an unknown id is
                // `undefined` and `facesOf` throws on `B.id` — measured 2026-08-15:
                // `variantFaces('NONSENSE')` → "TypeError: Cannot read properties of undefined".
                // A loud failure at the one point a typo can enter is worth more than a fallback.
                //
                // MEASURED (repo's own V8 host, 2026-08-15, shim applied as WidenExportedLiteral
                // applies it): she exports none of F/MATS/GAIN/BIAS/LN; PX, defaultElev, KEY, BUILDS
                // and buildIds ARE public. hurricane builds 1780 faces, frc 1704 — distinct, and the
                // per-hull geometry hash is what proves it rather than those counts.
                //
                // ⚠️ HER MATS IS THE ONLY ONE IN THE FLEET THAT DOES NOT FIT THE SHADER, and the fix
                // is a MEASUREMENT rather than a shader change. She declares EIGHTEEN materials and
                // `_RampMeta` is a `float4[16]` — a real uniform array, guarded in three places
                // (HullMeshDef.IsUsable, IsoFacetHullRenderer.Configure, HullPaintSchemeBaker) — so
                // her first bake failed with "not usable" and no hull-side change could have fixed
                // it.
                //
                // Measured in the repo's own V8, 2026-08-16: her hull FACES reference exactly
                // FOURTEEN of those eighteen, and the same fourteen on BOTH builds —
                // alu,blk,blu,boot,glas,hull,liner,moto,rub,seat,silv,sole,tube,white. The four that
                // no face names are `tubed`, `blulit`, `amb` and `amblit`, and they are not an
                // oversight: `blulit`/`amblit` are the +2-offset LIT variants of her nav lights and
                // belong to `renderBeacon`/`renderLight`, which draw a beacon this mesh does not
                // carry. A ramp no face references cannot colour a pixel.
                //
                // So this hands the extractor the materials her hull ACTUALLY USES, in the rig's own
                // MATS order, derived from the rig's own face lists — not a hand-written list of
                // fourteen names, which is the transcription this project has been burned by. Order
                // is preserved and therefore still load-bearing: `hull` is first in MATS and is used,
                // so it stays index 0, which is what the face packer resolves an unknown material to.
                //
                // ⚠️ Per-FILE, so no other hull's ramp table moves — the eleven and the eighteen keep
                // their committed bytes. And it is adjudicated in PIXELS by her acceptance fixture
                // against her own renderer, like every other hull: if dropping those four changed
                // anything visible, that is where it shows.
                ["zodiacIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["variantFaces"] = "function(b){return facesOf(BUILDS[b]).F;}",
                    ["MATS"] =
                        "(function(){var used={},out={};" +
                        "for(var b in BUILDS){var F=facesOf(BUILDS[b]).F;" +
                        "for(var i=0;i<F.length;i++)used[F[i].mat]=1;}" +
                        "for(var k in MATS)if(used[k])out[k]=MATS[k];" +
                        "return out;})()",
                },

                // ---- the sport fisher REGISTRY (fleet rig pack, 2026-08-12; baked 2026-08-16) -----
                // The third generator shape, and the only one whose face list is out of the global
                // widening's reach entirely. The other two build their hulls from a MODULE-level
                // function, so widening the exported literal — which is evaluated in the module
                // closure — can call it. This rig builds each hull inside `makeRig(spec)`, and `F`,
                // `RIGF` and `faceList` are locals of that CALL. There is no expression in the module
                // closure that names them, so no Reconstructions entry alone can reach them; see
                // RigMeshSymbols.InnerWidenings, which widens `makeRig`'s own return literal.
                //
                // ⚠️ `faceList('stowed')`, NOT `F`. Her outriggers are a state, not a fitting:
                // `faceList(r) = F.concat(RIGF[r])` and her own `render` calls
                // `faceList(opts.riggers||'stowed')`. Taking `F` alone would bake a boat whose mesh
                // is missing the geometry her own picture draws, and the acceptance compares the two.
                // Reading the rig's OWN composer rather than transcribing the concat is the same
                // move `palette({}).mats` makes for the small craft.
                //
                // ⚠️ Throws on an unknown id ON PURPOSE. `byId` falls back to the convertible
                // silently, so `byId(typo).faceList('stowed')` would bake the 16.2 m boat under the
                // 27.4 m boat's id and every count would look plausible.
                //
                // MEASURED (repo's own V8 host, 2026-08-15, both widenings applied): the widened
                // `faceList('stowed').length` equals the rig's OWN published `faceCount` on both
                // hulls — 3200 and 3770 — which is an independent check that this expression reaches
                // the list the rig means, not merely a list.
                ["sportFisherIsoRig2.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["variantFaces"] =
                        "function(id){var h=null;for(var i=0;i<HULLS.length;i++)" +
                        "if(HULLS[i].id===id)h=HULLS[i];" +
                        "if(!h)throw new Error('sportFisherIsoRig2.js: no hull \"'+id+'\". " +
                        "byId() would have returned the DEFAULT hull silently.');" +
                        "return h.faceList('stowed');}",
                    // Her paint is an axis, like the lobster's and the small craft's, and
                    // `defaultPaint` is 'gelcoat' on both hulls — so this reads the table the rig
                    // itself would use rather than transcribing one. Her twelve entries are
                    // hull,boot,cream,stripe,teak,deck,grip,glas,steel,iron,blk,dark; order is
                    // load-bearing because the face packer resolves an unknown material to index 0.
                    ["MATS"] = "matsFor('gelcoat').MATS",
                    // ⚠️ NO `defaultElev` entry, deliberately. It is not one of Required, so it is
                    // never shimmed — it is read directly, and for this rig it is read from the HULL
                    // (RigHullExtraction.HullScope), which is where she publishes it. A module-level
                    // reconstruction would bind the same 40 by a second mechanism and hide the fact
                    // that elevation is a per-hull fact on this rig.
                },

                // ---- the RESHAPED sport skiff (fleet rig pack, 2026-08-12; baked 2026-08-16) -----
                // The FIFTH hull to lose her `MATS` const to a paint axis, and she takes the small
                // craft's API for the same reason the Cape Islander does: she IS one of them, and
                // `palette` is already exported so the entry is one line. Her `defaultScheme` is
                // 'gelcoat-white', and `palette({})` is her own resolver called with no colourway,
                // which her own code resolves to that default — the table she would use, not one
                // transcribed here.
                //
                // ⚠️ Without this the bake fails outright with "ReferenceError: MATS is not defined",
                // exactly as the punt and the console did. That is what it did on 2026-08-16's first
                // run, and it is a loud failure rather than a wrong boat — which is the one good
                // thing about this class of gap.
                ["sportSkiffMk2IsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MATS"] = "palette({}).mats",
                },

                // ---- the DUALLY 3500 — the first ROAD VEHICLE (ADR 0035) -------------------------
                // Same gap as the five hulls above and for the same reason: she has no `MATS` const
                // at all, because her table depends on a paint axis (`makeMats(s)` reads s.paint and
                // s.weather). Widening `MATS:MATS` fails outright with "ReferenceError: MATS is not
                // defined", which is what her first bake did on 2026-08-17.
                //
                // `resolve({})` is her own resolver called with no options, so this is the table she
                // would use for her default build (`paint:'white'`, `weather:0.32`) — not one
                // transcribed here. It is the SAME pose her face list is extracted at
                // (`build(resolve({}))`), which is what keeps the geometry and the palette describing
                // one truck.
                //
                // ⚠️ Both names are unqualified ON PURPOSE. This expression is inserted INSIDE the
                // rig's own closure, so `makeMats` and `resolve` are in scope. Qualifying them as
                // `VehicleIso.makeMats(...)` is the shape a PROBE needs (the widening puts symbols on
                // the global, it does not put them in scope) and it is wrong here.
                //
                // ⚠️ Her MATS is an OBJECT KEYED BY NAME, so the fleet's "MATS order IS the baked
                // material index" law does not transfer. What DOES carry over is the part that
                // matters: the packer resolves an unknown material name to index 0, so the default
                // ramp must be FIRST — and `paint` is her first key, which her own fallback
                // (`MATS[f.mat] || MATS.paint`) agrees with. Pinned by DuallyIsoKitProbeTests.
                // ⚠️ AND SHE DOES NOT FIT THE SHADER EITHER, exactly as the zodiac does not: she
                // declares SEVENTEEN materials and `_RampMeta` is a `float4[16]`, so the plain
                // `makeMats(resolve({}))` bakes a def that is "not usable" and no vehicle-side change
                // could fix it. Same fix, and it is a MEASUREMENT rather than a hand-written list.
                //
                // Measured in the repo's own V8, 2026-08-17: her faces reference exactly SIXTEEN of
                // the seventeen. The one nothing names is `glow`, and it is not an oversight — it is
                // the LIT variant of her lamps, belonging to the rig's night pass, which this mesh
                // does not carry. A ramp no face references cannot colour a pixel.
                //
                // Order is preserved and therefore still load-bearing: `paint` is first in her MATS
                // and IS used (172 faces), so it stays index 0 — which is what the face packer
                // resolves an unknown material name to, and what her own `MATS[f.mat] || MATS.paint`
                // fallback agrees with.
                ["vehicleIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MATS"] =
                        "(function(){var M=makeMats(resolve({})),F=build(resolve({}))," +
                        "used={},out={};" +
                        "for(var i=0;i<F.length;i++)used[F[i].mat]=1;" +
                        "for(var k in M)if(used[k])out[k]=M[k];" +
                        "return out;})()",
                },

                // ---- the OTTER 8x8 — the second road vehicle, and the first amphibian ----------
                // Same gap and the same fix as the Dually's above: no `MATS` const, because her table
                // is built per-pose by `makeMats(s)` off her paint and weather axes. The expression is
                // character-for-character the Dually's — build the table, build the face list at the
                // SAME pose, and keep only the ramps some face actually names.
                //
                // ⚠️ FOR HER THE FILTER IS NOT WHAT MAKES HER FIT, and that distinction is the whole
                // history of this vehicle. She declared 22 and USED 17 — one over the shader's 16 — so
                // filtering could not save her the way it saved the Dually (17/16) and the zodiac
                // (18/14), and she sat unbakeable from #558. The fix was an ART merge, landed
                // 2026-08-19: the cockpit `mat` folded into `mesh`. She now declares 21 and uses 16,
                // and the filter drops only the five no face names — `trim`, `canvas`, `track`,
                // `glass` and `glow`. Each is genuinely absent from THIS build: `track` belongs to the
                // tracked variant (a different face list entirely, 1224 vs 1296), `canvas` to the
                // canopy fittings, and `glow`/`glass` to her night pass.
                //
                // ⚠️ So the used count is 16 EXACTLY, with no headroom. A 17th ramp reaching a face
                // makes her unplaceable again, which is why OtterIsoKitProbeTests pins the count in
                // both directions rather than only asserting she fits.
                //
                // Order is load-bearing here as everywhere: `paint` is her first key and IS used
                // (116 faces), so it stays index 0 — which is what the face packer resolves an unknown
                // material name to, and what her own `MATS[f.mat] || MATS.paint` fallback agrees with.
                ["amphibIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MATS"] =
                        "(function(){var M=makeMats(resolve({})),F=build(resolve({}))," +
                        "used={},out={};" +
                        "for(var i=0;i<F.length;i++)used[F[i].mat]=1;" +
                        "for(var k in M)if(used[k])out[k]=M[k];" +
                        "return out;})()",
                },

                // The skiff motor is a LAYER, not a hull, so its export omits two things every hull
                // rig publishes and the extractor reads unconditionally: the pixel scale and the bake
                // elevation. Both exist under the rig's own names (`S`, `DEFAULT_ELEV`) — this is a
                // rename, not an invention, and a wrong one would misplace every vertex on screen.
                ["skiffMotorRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["swivelPt"] = "function(){return [0,YA,ZT];}",
                    ["PX"] = "S",
                    ["defaultElev"] = "DEFAULT_ELEV",
                },
            };

        /// <summary>One extra object literal a rig has to have widened before the exported one can
        /// reach its contents. See <see cref="InnerWidenings"/>.</summary>
        public sealed class InnerWidening
        {
            /// <summary>Must match EXACTLY ONCE, and the insertion goes immediately after it. Written
            /// tightly enough to name one literal rather than "a return statement".</summary>
            public string AnchorPattern;

            /// <summary>Verbatim JS inserted after the anchor — property names the closure already
            /// binds, so the widening EXPOSES what is there rather than computing anything.</summary>
            public string Insert;

            /// <summary>What the widening buys, for the log line and the failure message.</summary>
            public string Why;
        }

        /// <summary>
        /// ⚠️ <b>Rigs whose geometry is out of the exported literal's REACH — not merely unexported.
        /// </b>
        ///
        /// <para><see cref="Reconstructions"/> handles a symbol that is private to the rig's MODULE:
        /// the shim adds a property to the exported literal, that literal is evaluated in the module
        /// closure, so any module-level name is reachable. Every generator so far has been of that
        /// kind — the lobster's <c>facesFor</c>/<c>resolve</c> and the zodiac's <c>facesOf</c> are
        /// module-level functions, and one line each is enough.</para>
        ///
        /// <para><b><c>sportFisherIsoRig2.js</c> is not.</b> She builds each of her two hulls inside
        /// <c>makeRig(spec)</c> and returns an object that deliberately omits the geometry:
        /// <c>F</c>, <c>RIGF</c> and <c>faceList</c> are locals of that CALL, and the two calls have
        /// two different sets of them. No expression evaluated in the module closure can name
        /// either, so there is no <see cref="Reconstructions"/> entry that could work — the reach
        /// problem has to be solved where the values live, by widening <c>makeRig</c>'s OWN return
        /// literal. Both hulls get the property because both come out of the same function, which is
        /// the point: one anchor, two hulls, and no per-hull transcription.</para>
        ///
        /// <para><b>Same discipline as the outer shim, for the same reasons:</b> anchored on a regex
        /// that must match exactly once, operating on a string in memory, never a file on disk, and
        /// it retires the day the rig exports <c>faceList</c> itself. It is louder about failing than
        /// the outer one because a mis-aimed insert here produces a rig that still RUNS and still
        /// draws — it would simply have no faces to extract.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<InnerWidening>>
            InnerWidenings = new Dictionary<string, IReadOnlyList<InnerWidening>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["sportFisherIsoRig2.js"] = new[]
                {
                    new InnerWidening
                    {
                        // `return {` followed by makeRig's own first property. Deliberately NOT a
                        // bare `return {`: this rig has several (proj's `{x,y}`, DOORS, NAV), and an
                        // anchor that matched one of those would expose nothing and fail far away.
                        AnchorPattern = @"return\s*\{\s*\n\s*id:spec\.id,",
                        Insert = " faceList, F, RIGF,",
                        Why = "makeRig() keeps this hull's face list in its own closure; the mesh " +
                              "bake reads faceList('stowed'), which is exactly what her render() " +
                              "draws (F.concat(RIGF.stowed)).",
                    },
                },
            };

        /// <summary>The extra literals to widen for this rig, or an empty list.</summary>
        public static IReadOnlyList<InnerWidening> InnerWideningsFor(string scriptPath) =>
            InnerWidenings.TryGetValue(Path.GetFileName(scriptPath ?? string.Empty), out var list)
                ? list
                : Array.Empty<InnerWidening>();

        /// <summary>
        /// The JS expression the shim should bind <paramref name="symbol"/> to for this rig. Normally
        /// the symbol's own name (a widening); a <see cref="Reconstructions"/> entry overrides it.
        /// </summary>
        public static string ExpressionFor(string scriptPath, string symbol)
        {
            string file = Path.GetFileName(scriptPath ?? string.Empty);
            return Reconstructions.TryGetValue(file, out var bySymbol)
                   && bySymbol.TryGetValue(symbol, out string expr)
                ? expr
                : symbol;
        }

        /// <summary>True when this rig/symbol pair is reconstructed rather than merely widened —
        /// reported separately because the two are very different claims.</summary>
        public static bool IsReconstructed(string scriptPath, string symbol) =>
            ExpressionFor(scriptPath, symbol) != symbol;
    }

    /// <summary>
    /// Pulls the STATIC face list, material table and lighting constants out of an art-director rig
    /// so they can become a real mesh (ADR 0022 phase 2).
    ///
    /// <para><b>How it gets at them.</b> Two paths, probed in order, and the first one that works
    /// wins:</para>
    /// <list type="number">
    /// <item><b>The exported path.</b> Run the rig UNMODIFIED (ADR 0021 §5) and read
    /// <c>Global.F</c>, <c>Global.MATS</c>, … straight off the public object. This is the only path
    /// that should exist long-term.</item>
    /// <item><b>The shim.</b> If — and only if — a symbol is missing, re-run a MODIFIED IN-MEMORY
    /// COPY of the source whose exported object literal is widened with exactly the missing
    /// properties. ⚠️ The file on disk is NEVER written. <c>docs/art/rigs/**</c> is the art
    /// director's source and ours to read only. <c>RigMeshExtractionTests</c> asserts the rig files
    /// are byte-identical after a run.</item>
    /// </list>
    ///
    /// <para>The shim is scoped per SYMBOL, not all-or-nothing, so partial adoption works: the day
    /// <c>F,</c> lands in a rig, F stops being shimmed with no edit here, and the day the last of
    /// the five lands, <see cref="RigMeshData.ShimmedSymbols"/> comes back empty and the widening
    /// never executes. That is the intended end state — the code below is designed to become
    /// unreachable rather than to be deleted.</para>
    /// </summary>
    /// <summary>
    /// How to extract an articulated FITTING (ADR 0022 phase 7) rather than a hull.
    ///
    /// <para><b>The one structural difference, and everything else follows from it.</b> A hull's
    /// geometry is the rig's static <c>F</c> array, built once at load and posed afterwards by
    /// transforms. A fitting has no such array: its faces come from a BUILDER that takes a pose
    /// (<c>buildOar(side, {sweep,dip})</c>, <c>motorFaces({steer,tilt,variant})</c>) and returns a
    /// freshly-generated list. So extraction must CALL something, at a pose chosen on purpose, and
    /// the choice is part of what the baked asset means — which is why it is recorded on the def.</para>
    ///
    /// <para><b>Pick the canonical pose so the runtime transform is exactly rigid.</b> The runtime
    /// poses a fitting by rotating the baked mesh about its pivot, so the baked pose must be one the
    /// real poses are a pure rotation away from. Dead ahead and untilted for an outboard; sweep 0,
    /// dip 0 for an oar. ⚠️ That is NOT always reachable through the rig's public pose API — the
    /// dory's <c>oarPose('row', t)</c> traces an ellipse that never passes through (0,0), which is
    /// precisely why the builder is called directly instead of through <c>oarFaces</c>.</para>
    /// </summary>
    public sealed class RigPropExtraction
    {
        /// <summary>JS returning the face list, evaluated against the rig's global — e.g.
        /// <c>buildOar(1,{sweep:0,dip:0})</c>. Prefixed with <c>&lt;Global&gt;.</c> by the extractor.</summary>
        public string FaceBuilderCall;

        /// <summary>
        /// <b>The same builder at a DIFFERENT pose</b> — how the extractor finds out which faces are
        /// bolted down. Optional; null means "every face moves", which is right for an oar.
        ///
        /// <para>⚠️ <b>A fitting is not necessarily one rigid body.</b> Both outboard rigs build the
        /// clamp bracket through the identity placement <c>I</c> instead of the posed <c>X</c>,
        /// because the bracket is fixed to the transom and the engine swivels ON it — so a mesh that
        /// rotated the whole face list would carry the bracket round with the cowl. That is a real
        /// defect and a subtle one (it only appears off dead ahead), and it is exactly what this probe
        /// exists to catch.</para>
        ///
        /// <para><b>Identified by MEASUREMENT, never by transcription.</b> Building the list twice and
        /// comparing vertices asks the rig which faces move; reading the rig and declaring "the
        /// bracket is the first six faces" is the class of claim this project has been burned by. Pick
        /// a probe pose with BOTH articulation axes non-zero, so a face that happens to be invariant
        /// under one of them is not mistaken for a fixed one.</para>
        /// </summary>
        public string PoseProbeFaceBuilderCall;

        /// <summary>JS returning <c>[x,y,z]</c> in hull-rig metres: the point the fitting turns
        /// about. Same prefixing.</summary>
        public string PivotCall;

        /// <summary>The object to read cell geometry (<c>W</c>/<c>H</c>/<c>pivot</c>) from, relative
        /// to the global. Empty = the global itself, which is right for a fitting that shares its
        /// hull's cell (the dory's oars are drawn through the same camera and pivot as her hull);
        /// <c>"MOTOR"</c> for one that ships its own wider cell.</summary>
        public string CellPath = "";

        /// <summary>Closure-private symbols this extraction needs on top of the usual five. The
        /// builders and the pivot function are private in every rig inspected, so they are shimmed
        /// exactly as <c>F</c>/<c>MATS</c> are — and retire the same way, on the day they are
        /// exported.</summary>
        public string[] ExtraSymbols = Array.Empty<string>();

        /// <summary>Optional JS for the fitting's articulation limits and mounts; null when it does
        /// not steer, tilt, or carry more than one instance.</summary>
        public string MaxSteerCall, MaxTiltCall, LateralMountsCall;

        /// <summary>The rig's backface rule for THIS fitting — see
        /// <see cref="RigMeshData.BackfaceRescueNeedsOptIn"/>.</summary>
        public bool BackfaceRescueNeedsOptIn = true;

        /// <summary>Whether this fitting's render path darkens depth discontinuities. <b>Defaults to
        /// FALSE</b>, unlike a hull's, because every fitting entry point in the repo passes
        /// <c>doEdge = false</c> — see <see cref="RigMeshData.DepthEdgeDarkening"/>.</summary>
        public bool DepthEdgeDarkening = false;

        /// <summary>
        /// Drop faces that are an EXACT REVERSE of an earlier one — the rigs' way of making a
        /// zero-thickness surface visible from both sides (the dory's oar blade pushes every segment
        /// twice, <c>q</c> then <c>[q3,q2,q1,q0]</c>, at <c>doryIsoRig.js:241</c>).
        ///
        /// <para><b>Why a mesh must not keep both, measured rather than assumed.</b> The pair is
        /// exactly coplanar with identical <c>db</c>, so the two carry identical depth and OPPOSITE
        /// normals, and which one you see is decided purely by how the depth test breaks a tie:</para>
        /// <list type="bullet">
        ///   <item>the rig's rasteriser (and phase 2's transcription of it) uses a strict
        ///   <c>deff &lt; zbuf</c>, so the FIRST face wins;</item>
        ///   <item>the facet shader is <c>Cull Off</c> with <c>ZTest LEqual</c>, so the LAST one
        ///   wins.</item>
        /// </list>
        /// <para>They therefore disagree by construction — and worse, neither is stable: the two
        /// faces fan-triangulate differently, so the interpolated depth at a given pixel differs in
        /// the last bits and the winner changes across a patch. Measured on the dory's oar as a
        /// 49-pixel connected patch shading from the opposite normal. On a GPU that same ambiguity
        /// is z-fighting, which on a moving oar reads as a shimmering blade.</para>
        ///
        /// <para>⚠️ <b>OFF by default, because MEASURING it refuted the obvious conclusion.</b> The
        /// reasoning above predicts that dropping the reverse should make the mesh agree with the
        /// rig. It does not: on the dory's oar the worst connected cluster got WORSE, 49 → 60
        /// (24 → 16 triangles, 4 faces dropped per oar).</para>
        ///
        /// <para><b>Why — settled by instrumenting the rasteriser rather than by reasoning
        /// (<see cref="RigPaintTrace"/>).</b> At every pixel where the mesh and the rig disagreed,
        /// the rig had painted a colour one of OUR OWN two twins computed there — 1,519 such pixels
        /// over 8 headings × 8 stroke phases, zero exceptions. So neither face is "the right one":
        /// the rig picks between them by the last bit of a barycentric sum, and its pick matches
        /// first-wins 50.6% of the time, last-wins 50.4%, its own <c>Float32Array</c> z-buffer 44.6%,
        /// front-facing 51.2%, lit 51.2%. Six rules, all chance. Dropping the twin makes us
        /// deterministic and therefore agree LESS often than a coin. The residual is not a defect and
        /// there is nothing left open: <see cref="RigAmbiguousPixels"/> is how the acceptance handles
        /// it, and the fixture that uses it resolves a half-degree feather error.</para>
        ///
        /// <para>Kept as a switch rather than deleted: the CPU-versus-GPU tie-break disagreement is
        /// real and independently worth knowing (it applies to any rig surface built this way,
        /// including the outboards), and this is the measured way to test its effect on a new
        /// fitting rather than reasoning about it.</para>
        /// </summary>
        public bool DropReverseDuplicateFaces = false;
    }

    /// <summary>
    /// How to extract ONE HULL out of a rig that builds SEVERAL (ADR 0022 phase 8).
    ///
    /// <para><b>The problem, and it is not the fitting's problem wearing a hat.</b> Every hull baked
    /// so far is one boat per rig file, and its geometry is the static <c>F</c> array the rig builds
    /// once at load. Two rigs in the 2026-08-13 fleet pack are GENERATORS — one file, many hulls,
    /// each built on demand by a private function of a variant descriptor:</para>
    /// <list type="bullet">
    ///   <item><c>lobsterBoatVariantsIsoRig.js</c> — <c>facesFor(resolve(v)).F</c>, 18 hulls
    ///   (3 sizes × 2 styles × 3 regions).</item>
    ///   <item><c>zodiacIsoRig.js</c> — <c>facesOf(buildOf(o)).F</c>, 2 builds off one loft.</item>
    /// </list>
    /// <para>Neither has an <c>F</c> at all. Twenty hulls, and the only thing standing in the way was
    /// that <see cref="RigMeshExtractor.ExtractFrom"/> hard-coded <c>{global}.F</c> for anything that
    /// was not a fitting.</para>
    ///
    /// <para><b>Why not just reuse <see cref="RigPropExtraction"/>.</b> It carries five knobs that
    /// mean something only to an articulated fitting — a pose probe, a swivel point, its own cell,
    /// and two rasteriser flags whose defaults are deliberately the OPPOSITE of a hull's
    /// (<see cref="RigPropExtraction.DepthEdgeDarkening"/> is false for a fitting and true for a
    /// hull). Routing hulls through it would change the defaults all eleven baked hulls take today,
    /// to buy nothing. A hull variant is still a hull: it wants the hull's rasteriser rules and the
    /// hull's cell, and the ONLY thing it does differently is where its face list comes from. So this
    /// type carries exactly that, and nothing else.</para>
    ///
    /// <para><b>Per-FILE knowledge stays in <see cref="RigMeshSymbols.Reconstructions"/>; per-HULL
    /// knowledge lives here.</b> How to reach a generator's private builder is a fact about the rig
    /// file and is shimmed like any other missing symbol. WHICH variant to build is a fact about the
    /// hull, and is this field. That split is why one <c>variantFaces</c> reconstruction serves all
    /// eighteen lobster boats.</para>
    /// </summary>
    public sealed class RigHullExtraction
    {
        /// <summary>
        /// JS returning this variant's face list, evaluated against the rig's global and prefixed
        /// with <c>&lt;Global&gt;.</c> — e.g.
        /// <c>variantFaces({size:'offshore',style:'hardtop',region:'fundy'})</c>.
        ///
        /// <para>Null means the static <c>F</c> array, which is the path every previously-baked hull
        /// takes and is left bit-for-bit alone.</para>
        /// </summary>
        public string FaceExpression;

        /// <summary>Closure-private symbols <see cref="FaceExpression"/> needs on top of the usual
        /// five — the generator's builder, normally. Shimmed exactly as <c>F</c>/<c>MATS</c> are, and
        /// retires the same way, on the day the rig exports it.</summary>
        public string[] ExtraSymbols = Array.Empty<string>();

        /// <summary>
        /// The same variant, written as a JS object literal for the rig's own PUBLIC entry points —
        /// e.g. <c>{size:'offshore',style:'hardtop',region:'fundy'}</c> for
        /// <c>render(dir, opts)</c>, <c>navMounts(dir, opts)</c>, <c>anchors(v)</c>.
        ///
        /// <para><b>Not a duplicate of <see cref="FaceExpression"/>, and the difference matters.</b>
        /// That one is a call into a SHIMMED private builder and is only valid inside the widened
        /// host; this one is a plain descriptor the rig accepts anywhere, and is what a probe needs
        /// in order to photograph THIS hull rather than the generator's default. Measured
        /// 2026-08-15: without it, all eighteen lobster cells rendered an identical 45,211 opaque
        /// pixels at the azimuth probe — one hull's picture deciding seventeen hulls' convention.</para>
        ///
        /// <para>Null for a static-F hull, whose rig takes no descriptor at all.</para>
        /// </summary>
        public string ViewOptions;

        /// <summary>
        /// The object THIS HULL's own cell and camera live on, relative to the global — e.g.
        /// <c>byId('convertible')</c>. Empty (the case for every hull baked before 2026-08-16) means
        /// the global itself, and that path is left bit-for-bit alone.
        ///
        /// <para><b>Why a scope and not a cell path.</b> <see cref="RigPropExtraction.CellPath"/>
        /// redirects exactly one thing — <c>W</c>/<c>H</c>/<c>pivot</c> — because a fitting shares
        /// its hull's camera and differs only in how much room its cowl needs. That is not the shape
        /// of <c>sportFisherIsoRig2.js</c>. She is a REGISTRY: her global carries no
        /// <c>defaultElev</c>, no <c>render</c>, no <c>ROCK</c> and no <c>navMounts</c> at all
        /// (measured in the repo's own V8, 2026-08-15 — the global holds only <c>PX</c>,
        /// <c>KEY</c>, <c>HULLS</c>, <c>byId</c> and the paint tables), because those are per-hull
        /// facts and her two hulls genuinely differ in every one of them: 820×770 against
        /// 1200×1170, pivots (410,475) against (600,730).</para>
        ///
        /// <para>So this redirects the whole per-hull GROUP — cell, elevation, render, rock, nav
        /// mounts — and the shading half (<c>GAIN</c>/<c>BIAS</c>/<c>LN</c>/<c>MATS</c>/<c>KEY</c>)
        /// stays on the global, which is where that rig puts it. Splitting them is not a
        /// convenience: reading <c>defaultElev</c> off her global yields <c>undefined</c>, and an
        /// undefined elevation does not fail — it poses the whole bake at NaN.</para>
        ///
        /// <para>⚠️ <b>An unknown id here is silent.</b> <c>byId</c> resolves anything it does not
        /// recognise to the FIRST hull, so a typo in this path bakes the convertible under the
        /// skybridge's id and nothing complains. That is why the per-hull geometry hash exists and
        /// why face COUNT is not the oracle.</para>
        /// </summary>
        public string HullScope = "";

        /// <summary>True when this extraction names a variant rather than taking the static array.
        /// </summary>
        public bool IsVariant => !string.IsNullOrEmpty(FaceExpression);

        /// <summary><paramref name="globalName"/>, or the per-hull object when this extraction names
        /// one. The single place the "global unless scoped" rule is written.</summary>
        public string ScopeOr(string globalName) =>
            string.IsNullOrEmpty(HullScope) ? globalName : $"{globalName}.{HullScope}";
    }

    public static class RigMeshExtractor
    {
        /// <summary>Extracts from a catalogued rig, in its own throwaway host.</summary>
        public static RigMeshData Extract(string rigKey)
        {
            var entry = RigCatalog.Get(rigKey);
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var data = ExtractFrom(host, entry.ScriptPath, entry.GlobalName);
            data.RigKey = rigKey;
            return data;
        }

        /// <summary>Extracts from a catalogued rig into a host the caller owns.</summary>
        public static RigMeshData Extract(IRigScriptHost host, string rigKey)
        {
            var entry = RigCatalog.Get(rigKey);
            var data = ExtractFrom(host, entry.ScriptPath, entry.GlobalName);
            data.RigKey = rigKey;
            return data;
        }

        /// <summary>
        /// Extracts from a rig that is not in <see cref="RigCatalog"/>. The catalog deliberately
        /// lists only the rigs the sprite baker actually bakes (CLAUDE.md rule 8 — importing source
        /// is not a licence to wire content), and adding the side dragger there would offer the
        /// owner a 433 MiB sprite bake that ADR 0022 exists to avoid. So mesh extraction takes an
        /// explicit path instead.
        /// </summary>
        /// <param name="scriptPath">Repo-relative, e.g. "docs/art/rigs/sideDraggerIsoRig.js".</param>
        /// <param name="globalName">The global the IIFE installs, e.g. "SideDraggerIso".</param>
        /// <param name="prop">Non-null to extract an articulated FITTING instead of the hull — its
        /// faces come from a builder called at a canonical pose rather than from the static
        /// <c>F</c> array. See <see cref="RigPropExtraction"/>.</param>
        /// <param name="hull">Non-null to extract ONE HULL from a rig that generates several — its
        /// faces come from a per-variant expression rather than from the static <c>F</c> array. See
        /// <see cref="RigHullExtraction"/>. Mutually exclusive with <paramref name="prop"/>.</param>
        public static RigMeshData ExtractFrom(IRigScriptHost host, string scriptPath, string globalName,
                                              RigPropExtraction prop = null,
                                              RigHullExtraction hull = null)
        {
            // Nonsense rather than a silent precedence rule: a fitting is not a hull variant, and
            // picking one arm quietly is how a bake ends up meaning something nobody chose.
            if (prop != null && hull != null && hull.IsVariant)
                throw new ArgumentException(
                    $"Both a fitting and a hull-variant extraction were given for '{scriptPath}'. " +
                    "They are alternative face sources — pass exactly one.", nameof(hull));

            if (host == null) throw new ArgumentNullException(nameof(host));
            string full = Path.Combine(RigCatalog.RepoRoot, scriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Rig source missing at {full}. The rigs are committed under docs/art/rigs/ — " +
                    "if this fired, the branch predates that import.", full);

            // READ ONLY. Nothing below ever writes this path. See the class doc.
            string source = File.ReadAllText(full);
            string g = globalName;

            // ---- pass 1: run the rig UNMODIFIED and see what it already gives us -------------
            host.Execute(source);
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{scriptPath}' ran but did not install globalThis.{g}. Either the global " +
                    "name is wrong or the rig changed shape.");

            // A fitting needs the shading half of the usual five but NOT `F` — its geometry comes
            // from a builder, and demanding a static face array of a rig that has none would shim in
            // a symbol nothing reads. It needs that builder (and its pivot) instead.
            //
            // A GENERATOR hull is in exactly the same position and for exactly the same reason: it
            // has no `F` either, only a private builder keyed on a variant. Measured 2026-08-13 in
            // the repo's own V8 host — both pack generators export none of F/MATS/GAIN/BIAS/LN.
            bool variantHull = hull != null && hull.IsVariant;
            var required = new List<string>();
            foreach (string sym in RigMeshSymbols.Required)
                if ((prop == null && !variantHull) || sym != "F") required.Add(sym);
            if (prop != null) required.AddRange(prop.ExtraSymbols);
            if (variantHull) required.AddRange(hull.ExtraSymbols);

            var missing = new List<string>();
            foreach (string sym in required)
                if (!HasSymbol(host, g, sym)) missing.Add(sym);

            bool bayerExported = HasSymbol(host, g, RigMeshSymbols.OptionalBayer);

            // ---- pass 2: the shim, only for what is actually missing -------------------------
            if (missing.Count > 0)
            {
                // The INNER literals first, where a rig keeps geometry out of the exported literal's
                // reach entirely, then the exported literal itself. Order matters only in that both
                // must be present in the one string pass 2 executes — executing them separately would
                // re-run the IIFE twice and the second run would win.
                string widened = ApplyInnerWidenings(source, scriptPath);
                widened = WidenExportedLiteral(widened, g, missing, scriptPath);
                host.Execute(widened);

                var stillMissing = new List<string>();
                foreach (string sym in missing)
                    if (!HasSymbol(host, g, sym)) stillMissing.Add(sym);

                if (stillMissing.Count > 0)
                    throw new InvalidOperationException(
                        $"In-memory widening of '{scriptPath}' did not take: {g}." +
                        $"{{{string.Join(",", stillMissing)}}} is still missing after inserting it " +
                        $"into the exported literal. The rig's shape has changed and this shim must " +
                        "be re-aimed — or, far better, retired by exporting " +
                        $"{string.Join(", ", required)} from {scriptPath} directly. " +
                        "⚠️ Do NOT fix this by editing docs/art/rigs/**; that is the art director's source.");

                var widened_ = missing.Where(s => !RigMeshSymbols.IsReconstructed(scriptPath, s)).ToList();
                var rebuilt = missing.Where(s => RigMeshSymbols.IsReconstructed(scriptPath, s)).ToList();

                Debug.LogWarning(
                    $"[rig-mesh] {scriptPath}: shimmed {string.Join(", ", missing)} via an IN-MEMORY " +
                    "widening because the rig does not export them (ADR 0022 open question #4). The " +
                    "file on disk was not touched. Ask the art director to add " +
                    $"`{string.Join(", ", widened_)},` to the exported literal and this warning — and the " +
                    "shim — disappear on their own." +
                    (rebuilt.Count == 0
                        ? ""
                        : $"\n⚠️ {string.Join(", ", rebuilt)} was RECONSTRUCTED, not widened: this rig " +
                          "has no such symbol at all, so the shim rebuilt it from the rig's own values " +
                          "(RigMeshSymbols.Reconstructions). That is a transcription of what the rig's " +
                          "_paint does — it is adjudicated in pixels by the acceptance fixture, never " +
                          "trusted on its face."));
            }

            // A fitting may ship its OWN cell — the skiff outboard's 272×216 against a 244×216 hull,
            // because the engine swings outboard of the transom. Reading the hull's cell for it would
            // crop the cowl on hard-over headings, so the cell source is part of the extraction.
            //
            // A HULL may ship its own too, and for a different reason: a registry rig
            // (sportFisherIsoRig2.js) publishes no cell on its global at all, because a 16.2 m boat
            // and a 27.4 m boat share nothing but their paint. Reading the global's would read
            // `undefined`, which does not fail — it poses the bake at NaN. See
            // RigHullExtraction.HullScope. Null scope = the global, which is the path every hull
            // baked before 2026-08-16 takes, unchanged.
            string hullScope = hull != null ? hull.ScopeOr(g) : g;
            string cell = prop != null && !string.IsNullOrEmpty(prop.CellPath)
                ? $"{g}.{prop.CellPath}"
                : hullScope;

            var data = new RigMeshData
            {
                RigKey = g,
                GlobalName = g,
                W = (int)host.EvaluateNumber($"{cell}.W"),
                H = (int)host.EvaluateNumber($"{cell}.H"),
                PivotX = host.EvaluateNumber($"{cell}.pivot.x"),
                PivotY = host.EvaluateNumber($"{cell}.pivot.y"),
                PxPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                // The elevation follows the CELL, not the global: a rig that gives each hull its own
                // camera gives each hull its own bake elevation with it. A fitting keeps the global's,
                // because it is drawn through its hull's camera by construction.
                DefaultElev = host.EvaluateNumber($"{(prop != null ? g : hullScope)}.defaultElev"),
                Gain = host.EvaluateNumber($"{g}.GAIN"),
                Bias = host.EvaluateNumber($"{g}.BIAS"),
                LightN = new Vector3d(host.EvaluateNumber($"{g}.LN[0]"),
                                      host.EvaluateNumber($"{g}.LN[1]"),
                                      host.EvaluateNumber($"{g}.LN[2]")),
                Keyline = ParseHex(host.EvaluateString($"{g}.KEY")),
                BayerWasExported = bayerExported,
                // The two facts about the rig's RASTERISER that its data cannot carry. A hull keeps
                // the defaults; a fitting declares them (see RigPropExtraction).
                BackfaceRescueNeedsOptIn = prop == null || prop.BackfaceRescueNeedsOptIn,
                DepthEdgeDarkening = prop == null || prop.DepthEdgeDarkening,
                ShimmedSymbols = missing,
                ReconstructedSymbols =
                    missing.Where(s => RigMeshSymbols.IsReconstructed(scriptPath, s)).ToList(),
            };

            ReadBayer(host, g, bayerExported, data);
            ReadMaterials(host, g, data);
            // BEFORE the faces, and it has to be: the packer resolves each face's `lv` string against
            // this table inside JavaScript, so the vocabulary must exist by the time the blob is
            // built. A rig without geometry() leaves it empty and every face comes back Untagged.
            ReadLevels(host, g, data);

            // Three arms, and the LAST is the one every hull baked before 2026-08-13 takes — passing
            // neither extraction leaves this method on the identical code path it has always had.
            string faceSource =
                prop != null ? $"{g}.{prop.FaceBuilderCall}"
                : variantHull ? $"{g}.{hull.FaceExpression}"
                : $"{g}.F";
            data.SourceFaceExpression = prop != null || variantHull ? faceSource : "";
            ReadFaces(host, g, faceSource, data);

            if (prop != null && !string.IsNullOrEmpty(prop.PoseProbeFaceBuilderCall))
                MarkFacesFixedInPose(host, g, prop, data, faceSource);

            if (prop != null && prop.DropReverseDuplicateFaces)
            {
                int dropped = DropReverseDuplicateFaces(data);
                if (dropped > 0)
                    Debug.Log($"[rig-mesh] {faceSource}: dropped {dropped} exact-reverse duplicate " +
                              $"face(s) of {dropped * 2} in double-sided pairs. See " +
                              $"{nameof(RigPropExtraction)}.{nameof(RigPropExtraction.DropReverseDuplicateFaces)} " +
                              "— keeping both makes the visible normal depend on how the depth test " +
                              "breaks an exact tie, which the CPU oracle and the GPU resolve opposite ways.");
            }

            if (data.Faces.Count == 0)
                throw new InvalidOperationException(
                    prop != null
                        ? $"{faceSource} returned no faces. The builder ran but produced nothing — " +
                          "check the canonical pose's arguments against the rig's own signature " +
                          "(a mistyped option name silently yields a default-posed empty list)."
                    : variantHull
                        ? $"{faceSource} returned no faces. The generator ran but produced nothing " +
                          "for this variant — check the descriptor's keys and values against the " +
                          "rig's own axis tables. ⚠️ A rig that resolves an UNKNOWN id to its " +
                          "default (both pack generators do) will not fail here; it will hand back " +
                          "the default hull instead, which is why the per-variant distinctness test " +
                          "hashes geometry rather than trusting this check."
                        : $"{g}.F is present but empty. The rig builds its face list once at load " +
                          "(`(function build(){…})`); an empty list means build() did not run.");

            return data;
        }

        // ⚠️ ONE interpolated string, deliberately. Splitting it across a `$"…" + "…"` concat is how
        // the brace escaping goes wrong: `}}` only collapses to `}` inside an INTERPOLATED string,
        // so a plain second fragment emits a stray brace and V8 answers "SyntaxError: Unexpected
        // token '}'" with no hint that C# built the script wrong. Cost a full test cycle.
        static bool HasSymbol(IRigScriptHost host, string g, string symbol) =>
            host.EvaluateBool(
                $"(function(){{var v={g}.{symbol};return v!==undefined&&v!==null&&" +
                $"!(Array.isArray(v)&&v.length===0);}})()");

        /// <summary>
        /// ⚠️⚠️ THE ONE UGLY THING, AND IT IS DELIBERATELY SMALL AND LOUD. ⚠️⚠️
        ///
        /// Inserts <c>F:F, MATS:MATS, …</c> — only the missing names, under their CANONICAL names,
        /// so the read path above is identical whether the rig exported them or we widened them —
        /// immediately after the opening brace of <c>root.&lt;Global&gt; = {</c>.
        ///
        /// Single site, anchored on a regex that must match EXACTLY ONCE, operating on a string in
        /// memory. It never returns a path and nothing here opens a file for writing.
        /// </summary>
        /// <remarks>Public only so the tests can hit it without an engine — it is not an entry
        /// point, and the day the rigs export their symbols it should be deleted outright.</remarks>
        public static string WidenExportedLiteral(
            string source, string globalName, IReadOnlyList<string> missingSymbols, string scriptPathForMessages)
        {
            if (missingSymbols == null || missingSymbols.Count == 0) return source;

            var anchor = new Regex(@"root\." + Regex.Escape(globalName) + @"\s*=\s*\{",
                                   RegexOptions.CultureInvariant);
            var matches = anchor.Matches(source);
            if (matches.Count != 1)
                throw new InvalidOperationException(
                    $"Expected exactly one `root.{globalName} = {{` in {scriptPathForMessages}, found " +
                    $"{matches.Count}. The shim widens a SINGLE exported object literal; it will not " +
                    "guess which of several to aim at. Export " +
                    $"{string.Join(", ", RigMeshSymbols.Required)} from the rig instead.");

            // `sym:sym` for a plain widening; `sym:<expression>` for a rig that predates the
            // convention and has to have the symbol RECONSTRUCTED from its own values. The
            // expression is evaluated inside the rig's own closure, which is why it may name the
            // rig's private consts (the dory's RAMP/IRON) — see RigMeshSymbols.Reconstructions.
            var insert = new StringBuilder();
            foreach (string sym in missingSymbols)
                insert.Append(' ').Append(sym).Append(':')
                      .Append(RigMeshSymbols.ExpressionFor(scriptPathForMessages, sym)).Append(',');

            var m = matches[0];
            return source.Insert(m.Index + m.Length, insert.ToString());
        }

        /// <summary>
        /// Applies this rig's <see cref="RigMeshSymbols.InnerWidenings"/> — the extra object literals
        /// whose contents the exported literal cannot reach. A no-op for every rig but the sport
        /// fisher, so the string every other bake executes is byte-identical to what it always was.
        /// </summary>
        /// <remarks>Public for the same reason <see cref="WidenExportedLiteral"/> is: the tests hit
        /// it without an engine. Not an entry point.</remarks>
        public static string ApplyInnerWidenings(string source, string scriptPath)
        {
            foreach (var w in RigMeshSymbols.InnerWideningsFor(scriptPath))
            {
                var anchor = new Regex(w.AnchorPattern, RegexOptions.CultureInvariant);
                var matches = anchor.Matches(source);
                if (matches.Count != 1)
                    throw new InvalidOperationException(
                        $"Inner widening for {scriptPath} matched its anchor {matches.Count} times, " +
                        "expected exactly 1.\n" +
                        $"  anchor : {w.AnchorPattern}\n" +
                        $"  buys   : {w.Why}\n" +
                        "⚠️ This one fails QUIETLY if it is ever loosened: a mis-aimed insert still " +
                        "leaves a rig that runs and draws, it simply has no faces to extract, and " +
                        "the error surfaces later as an empty face list from an unrelated-looking " +
                        "expression. Re-aim the anchor against the rig's current text — or, far " +
                        "better, retire this entry by asking the art director to export the " +
                        "symbols it exposes. ⚠️ Do NOT fix this by editing docs/art/rigs/**.");

                var m = matches[0];
                source = source.Insert(m.Index + m.Length, w.Insert);
                Debug.LogWarning(
                    $"[rig-mesh] {scriptPath}: widened an INNER literal in memory ({w.Insert.Trim()}) " +
                    $"because {w.Why} The file on disk was not touched. This retires the day the rig " +
                    "returns those names from that literal itself.");
            }
            return source;
        }

        static void ReadBayer(IRigScriptHost host, string g, bool exported, RigMeshData data)
        {
            if (!exported)
            {
                for (int x = 0; x < 4; x++)
                    for (int y = 0; y < 4; y++)
                        data.Bayer[x, y] = (RigMeshSymbols.CanonicalBayer[x, y] + 0.5) / 16.0;
                return;
            }

            string blob = host.EvaluateString(
                $"(function(){{var o=[];for(var x=0;x<4;x++)for(var y=0;y<4;y++)" +
                $"o.push({g}.BAYER[x][y]);return o.join(',');}})()");
            string[] parts = blob.Split(',');
            if (parts.Length != 16)
                throw new InvalidOperationException(
                    $"{g}.BAYER is exported but is not 4×4 ({parts.Length} values).");
            for (int i = 0; i < 16; i++)
                data.Bayer[i / 4, i % 4] = double.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        static void ReadMaterials(IRigScriptHost host, string g, RigMeshData data)
        {
            // name|off|#rrggbb,#rrggbb,… ; …  — key order is the JS object's own enumeration order,
            // which is also the order the face packer resolves names against, below.
            string blob = host.EvaluateString(
                $"(function(){{var M={g}.MATS,o=[];for(var k in M)" +
                "o.push(k+'|'+(M[k].off||0)+'|'+M[k].ramp.join(','));return o.join(';');})()");

            foreach (string part in blob.Split(';'))
            {
                string[] f = part.Split('|');
                if (f.Length != 3)
                    throw new InvalidOperationException(
                        $"{g}.MATS entry '{part}' is not name|off|ramp. MATS must be " +
                        "{name:{ramp:[…],off:n}}.");
                string[] hex = f[2].Split(',');
                data.Materials.Add(new RigMaterial
                {
                    Name = f[0],
                    Off = int.Parse(f[1], CultureInfo.InvariantCulture),
                    RampHex = hex,
                    Ramp = Array.ConvertAll(hex, ParseHex),
                });
            }

            if (data.Materials.Count == 0)
                throw new InvalidOperationException($"{g}.MATS is empty.");
        }

        /// <summary>
        /// For every face, the index of the face that is its EXACT REVERSE, or -1 when it has none.
        ///
        /// <para>Exact vertex equality is the right test and not a fragile one: the pair comes from
        /// the SAME array in the rig (<c>[q[3],q[2],q[1],q[0]]</c>), so the doubles are bit-identical,
        /// and anything merely close is a different face that must survive.</para>
        ///
        /// <para><b>Why this is worth naming rather than leaving inside the dedupe.</b> A reverse pair
        /// is EXACTLY coplanar with an identical <c>db</c> and OPPOSITE normals, which makes it the
        /// one place in a rig where the picture is not a function of the geometry — see
        /// <see cref="RigAmbiguousPixels"/>, which needs to identify the pairs without removing them.
        /// Structural identification beats a depth tolerance: once the mesh is float32 the quad is no
        /// longer exactly planar, and the two triangulations' interpolated depths drift apart by up to
        /// ~2e-8, which no honest fixed tolerance sits cleanly above.</para>
        /// </summary>
        public static int[] FindReverseDuplicatePartners(RigMeshData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var partner = new int[data.Faces.Count];
            for (int i = 0; i < partner.Length; i++) partner[i] = -1;

            for (int i = 0; i < data.Faces.Count; i++)
            {
                if (partner[i] >= 0) continue;
                for (int j = i + 1; j < data.Faces.Count; j++)
                {
                    if (partner[j] >= 0) continue;
                    if (!IsExactReverse(data.Faces[i], data.Faces[j])) continue;
                    partner[i] = j;
                    partner[j] = i;
                    break;
                }
            }
            return partner;
        }

        static bool IsExactReverse(RigFace f, RigFace k)
        {
            if (k.V.Length != f.V.Length || k.Mat != f.Mat) return false;
            for (int i = 0; i < f.V.Length; i++)
            {
                Vector3d a = f.V[i], b = k.V[f.V.Length - 1 - i];
                if (a.X != b.X || a.Y != b.Y || a.Z != b.Z) return false;
            }
            return true;
        }

        /// <summary>
        /// Builds the fitting's face list a SECOND time at a different pose and flags every face whose
        /// vertices did not move — see <see cref="RigPropExtraction.PoseProbeFaceBuilderCall"/>.
        ///
        /// <para>Exact equality is the right test: a fixed face comes out of the rig's identity
        /// placement both times, so its doubles are bit-identical, while a face that moved by any
        /// amount at all moved by far more than a last-bit difference.</para>
        /// </summary>
        static void MarkFacesFixedInPose(IRigScriptHost host, string g, RigPropExtraction prop,
                                         RigMeshData data, string faceSource)
        {
            var probe = new RigMeshData { Materials = data.Materials };
            ReadFaces(host, g, $"{g}.{prop.PoseProbeFaceBuilderCall}", probe);

            if (probe.Faces.Count != data.Faces.Count)
                throw new InvalidOperationException(
                    $"{faceSource} yields {data.Faces.Count} faces but the pose probe " +
                    $"{prop.PoseProbeFaceBuilderCall} yields {probe.Faces.Count}. The probe must differ " +
                    "from the canonical pose ONLY in the articulation — a different variant or part " +
                    "changes which faces exist, and then 'this face did not move' is meaningless.");

            int fixedCount = 0, lastFixed = -1, firstMoving = int.MaxValue;
            for (int i = 0; i < data.Faces.Count; i++)
            {
                RigFace a = data.Faces[i], b = probe.Faces[i];
                bool same = a.V.Length == b.V.Length;
                for (int k = 0; same && k < a.V.Length; k++)
                    same = a.V[k].X == b.V[k].X && a.V[k].Y == b.V[k].Y && a.V[k].Z == b.V[k].Z;

                data.Faces[i].FixedInPose = same;
                if (same) { fixedCount++; lastFixed = i; }
                else firstMoving = Math.Min(firstMoving, i);
            }

            if (fixedCount == 0 || fixedCount == data.Faces.Count) return;

            // ⚠️ The two halves are drawn as two meshes, so they are rasterised in the order
            // (fixed, then moving) rather than in the rig's interleaved order. That is only
            // equivalent when the rig itself emits every fixed face first — which both outboard rigs
            // do, and which is asserted rather than assumed, because an interleaved rig would need
            // the depth ties to fall the same way and they would not.
            if (lastFixed > firstMoving)
                throw new InvalidOperationException(
                    $"{faceSource}: the pose-fixed faces are INTERLEAVED with the moving ones (last " +
                    $"fixed at {lastFixed}, first moving at {firstMoving}). Splitting the fitting into " +
                    "a fixed mesh and a swivelling one reorders them, which is safe only while every " +
                    "fixed face precedes every moving one. This rig needs a different split.");

            Debug.Log($"[rig-mesh] {faceSource}: {fixedCount} of {data.Faces.Count} faces do NOT move " +
                      $"with the pose (measured against {prop.PoseProbeFaceBuilderCall}) — the fitting " +
                      "is bolted-down geometry plus geometry that articulates, and they are baked as " +
                      "two meshes.");
        }

        /// <summary>Remove the later member of every reverse-duplicate pair.</summary>
        static int DropReverseDuplicateFaces(RigMeshData data)
        {
            int[] partner = FindReverseDuplicatePartners(data);
            var kept = new List<RigFace>(data.Faces.Count);
            int dropped = 0;

            for (int i = 0; i < data.Faces.Count; i++)
            {
                if (partner[i] >= 0 && partner[i] < i) { dropped++; continue; }
                kept.Add(data.Faces[i]);
            }

            data.Faces.Clear();
            data.Faces.AddRange(kept);
            return dropped;
        }

        /// <summary>
        /// Read the rig's <c>geometry()</c> — its level vocabulary and its per-level sole/ceiling
        /// records — if it publishes one. A rig without <c>geometry()</c> leaves
        /// <see cref="RigMeshData.LevelIds"/> empty and is on exactly the code path it has always
        /// had.
        ///
        /// <para><b>Delimited strings, not JSON.</b> The rig side has no serialiser this host can
        /// rely on and the C# side has no JSON dependency in this assembly; the ids are snake_case
        /// identifiers and the kinds are three literal words, so a tab/newline split is total. Numbers
        /// cross as <c>String(n)</c>, which is JavaScript's shortest ROUND-TRIP form — the double that
        /// comes back is the double the rig computed, not a 3-decimal print of it.</para>
        ///
        /// <para><b>The guards are structural, and they refuse rather than repair.</b> A vocabulary
        /// with no <c>hull</c>, two levels sharing one int, or a level record naming an id the table
        /// does not hold are all upstream mistakes that would otherwise ship as a room that half
        /// opens. Batch 2 of the kit is "the same mechanism, no new semantics", so these hold for it
        /// too — and if they ever do not, the bake stops with the reason in the message.</para>
        /// </summary>
        static void ReadLevels(IRigScriptHost host, string g, RigMeshData data)
        {
            if (!HasSymbol(host, g, "geometry")) return;

            string idsRaw = host.EvaluateString(
                $"(function(){{var G={g}.geometry();var ids=(G&&G.ids)?G.ids:{{}};var a=[];" +
                "for(var k in ids){if(Object.prototype.hasOwnProperty.call(ids,k))" +
                "a.push(k+'\\t'+ids[k]);}return a.join('\\n');})()");

            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            var byTag = new Dictionary<int, string>();
            foreach (string line in SplitLines(idsRaw))
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tag))
                    throw new InvalidOperationException(
                        $"{g}.geometry().ids has an entry this reader cannot parse: '{line}'. The table " +
                        "is name → int and nothing else.");
                if (byTag.TryGetValue(tag, out string already))
                    throw new InvalidOperationException(
                        $"{g}.geometry().ids gives BOTH '{already}' and '{parts[0]}' the id {tag}. " +
                        "TexCoord1.x carries this int and one compare in the fragment shader decides " +
                        "the cut, so two levels sharing an id are one level as far as the gate is " +
                        "concerned — the second room would open the first one's walls.");
                ids[parts[0]] = tag;
                byTag[tag] = parts[0];
            }

            if (ids.Count == 0) return;

            if (!ids.ContainsKey(RigLevelTags.HullLevelId))
                throw new InvalidOperationException(
                    $"{g}.geometry().ids names no '{RigLevelTags.HullLevelId}' level. The exterior " +
                    "silhouette is the one class a cut may never take — the room shows INSIDE the " +
                    "hull's own outline — so a vocabulary without it cannot express a cutaway.");

            string levelsRaw = host.EvaluateString(
                $"(function(){{var G={g}.geometry();var L=(G&&G.levels)?G.levels:[];var b=[];" +
                "for(var i=0;i<L.length;i++){var l=L[i];var c=l.ceiling||{};" +
                "b.push([l.id,(l.deck==null?'':l.deck),String(l.soleZ)," +
                "(l.ceilingZ==null?'':String(l.ceilingZ)),(c.kind==null?'':c.kind)].join('\\t'));}" +
                "return b.join('\\n');})()");

            var levels = new List<RigLevelRecord>();
            foreach (string line in SplitLines(levelsRaw))
            {
                string[] p = line.Split('\t');
                if (p.Length != 5)
                    throw new InvalidOperationException(
                        $"{g}.geometry().levels has a record this reader cannot parse: '{line}'.");
                if (!ids.TryGetValue(p[0], out int tag))
                    throw new InvalidOperationException(
                        $"{g}.geometry().levels publishes a level '{p[0]}' that geometry().ids does " +
                        "not name. The ids table is the bake table — a level outside it has no int " +
                        "to be tagged with and no way to be shown.");

                bool enclosed = !string.IsNullOrEmpty(p[3]);
                levels.Add(new RigLevelRecord
                {
                    Id = p[0],
                    DeckId = p[1],
                    Tag = tag,
                    SoleZ = ParseJsNumber(p[2], $"{g}.geometry().levels['{p[0]}'].soleZ"),
                    Enclosed = enclosed,
                    CeilingZ = enclosed
                        ? ParseJsNumber(p[3], $"{g}.geometry().levels['{p[0]}'].ceilingZ")
                        : 0.0,
                    CeilingKind = p[4],
                });
            }

            data.LevelIds = ids;
            data.Levels = levels;
        }

        static IEnumerable<string> SplitLines(string raw) =>
            string.IsNullOrEmpty(raw)
                ? Array.Empty<string>()
                : raw.Split('\n').Where(s => !string.IsNullOrEmpty(s));

        static double ParseJsNumber(string s, string what) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                ? v
                : throw new InvalidOperationException($"{what} came across as '{s}', which is not a number.");

        /// <param name="faceSource">JS evaluating to the face list — <c>&lt;Global&gt;.F</c> for a
        /// hull, a builder call at a canonical pose for a fitting.</param>
        static void ReadFaces(IRigScriptHost host, string g, string faceSource, RigMeshData data)
        {
            // The face list comes across as ONE packed binary blob through the bulk ReadBytes path.
            // Per-property marshalling of ~1,400 faces × 4 verts would erase the engine advantage
            // ADR 0021 was decided on (see IRigScriptHost.EvaluateBytes).
            //
            // Layout, little-endian:
            //   [i32 faceCount]  then per face
            //   [i32 nv][i32 matId][i32 levelCode][f64 b][f64 db][nv × 3 × f64]
            //
            // f64, not f32: extraction must be lossless. Quantisation to float belongs to the Mesh.
            //
            // levelCode rides the SAME blob rather than a second pass because the tag is a property
            // of the face and the two must not be able to fall out of order — a level list zipped
            // back on by index is one filtered face away from tagging the wrong room, and it would
            // look plausible doing it. −1 = this rig publishes no vocabulary; −2 = it does and this
            // face carries no `lv`; −3 = it does and the `lv` is not in the table. The last two are
            // refused below, never defaulted.
            var matOrder = new StringBuilder();
            foreach (var m in data.Materials)
                matOrder.Append(JsStringLiteral(m.Name)).Append(',');

            var lvTable = new StringBuilder();
            foreach (var kv in data.LevelIds)
                lvTable.Append(JsStringLiteral(kv.Key)).Append(':')
                       .Append(kv.Value.ToString(CultureInfo.InvariantCulture)).Append(',');
            string hasLv = data.CarriesLevelTags ? "1" : "0";

            string packer =
                $"globalThis.__hhRigMeshPack=(function(){{var F={faceSource};" +
                $"var order=[{matOrder.ToString().TrimEnd(',')}];" +
                $"var lvix={{{lvTable.ToString().TrimEnd(',')}}};var hasLv={hasLv};" +
                "var ix={};order.forEach(function(n,i){ix[n]=i;});" +
                "var n=0;for(var i=0;i<F.length;i++)n+=F[i].v.length*3;" +
                "var buf=new ArrayBuffer(4+F.length*(12+16)+n*8);var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,F.length,true);p+=4;" +
                "for(var i=0;i<F.length;i++){var f=F[i];" +
                "var mi=ix[f.mat];if(mi==null)mi=ix['hull'];if(mi==null)mi=0;" +
                "var li=-1;if(hasLv){li=(f.lv==null)?-2:" +
                "(Object.prototype.hasOwnProperty.call(lvix,f.lv)?lvix[f.lv]:-3);}" +
                "dv.setInt32(p,f.v.length,true);p+=4;" +
                "dv.setInt32(p,mi,true);p+=4;" +
                "dv.setInt32(p,li,true);p+=4;" +
                "dv.setFloat64(p,f.b||0,true);p+=8;dv.setFloat64(p,f.db||0,true);p+=8;" +
                "for(var k=0;k<f.v.length;k++){var v=f.v[k];" +
                "dv.setFloat64(p,v[0],true);p+=8;dv.setFloat64(p,v[1],true);p+=8;" +
                "dv.setFloat64(p,v[2],true);p+=8;}}" +
                "return new Uint8ClampedArray(buf);})();";
            host.Execute(packer);
            byte[] blob = host.EvaluateBytes("globalThis.__hhRigMeshPack");

            var levelNames = new Dictionary<int, string>();
            foreach (var kv in data.LevelIds) levelNames[kv.Value] = kv.Key;

            int off = 0;
            int faceCount = BitConverter.ToInt32(blob, off); off += 4;
            for (int i = 0; i < faceCount; i++)
            {
                int nv = BitConverter.ToInt32(blob, off); off += 4;
                int mat = BitConverter.ToInt32(blob, off); off += 4;
                int lvl = BitConverter.ToInt32(blob, off); off += 4;
                double b = BitConverter.ToDouble(blob, off); off += 8;
                double db = BitConverter.ToDouble(blob, off); off += 8;
                if (lvl < 0 && data.CarriesLevelTags)
                    throw new InvalidOperationException(
                        $"{faceSource}: this rig publishes geometry().ids, so every face it hands over " +
                        "must DECLARE its level — and these do not: " +
                        UntaggedFaceReport(host, faceSource, data) +
                        "\n\nNot defaulted on purpose. The only defensible default is 'hull', which " +
                        "means NEVER CULL — so a missed stamp would ship as a room that quietly stops " +
                        "opening, in one wall, on one heading. The cursor stamps every emission path " +
                        "in the rig (face / boxF / tubeF / direct push); a face that escaped it came " +
                        "in through a path the cursor does not ride, or through a widening that built " +
                        "faces outside it.");
                if (nv < 3)
                    throw new InvalidOperationException(
                        $"{faceSource}[{i}] has {nv} vertices. A face the rig can fan-triangulate has at least 3.");
                var vs = new Vector3d[nv];
                for (int k = 0; k < nv; k++)
                {
                    vs[k] = new Vector3d(BitConverter.ToDouble(blob, off),
                                         BitConverter.ToDouble(blob, off + 8),
                                         BitConverter.ToDouble(blob, off + 16));
                    off += 24;
                }
                data.Faces.Add(new RigFace
                {
                    V = vs, Mat = mat, B = b, Db = db,
                    Level = lvl,
                    LevelName = levelNames.TryGetValue(lvl, out string ln) ? ln : null,
                });
            }

            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"Face blob for {g} was {blob.Length} bytes but {off} were consumed. The packer " +
                    "and the reader disagree about layout.");
        }

        /// <summary>
        /// Name the faces whose <c>lv</c> is missing or unknown, for the refusal message above. A
        /// SECOND pass over the face list, run only on the failure path: an integer code is what the
        /// blob can carry cheaply, and "face 412 is untagged" sends the reader hunting through a
        /// thousand-line rig, where "face 412: lv 'wheelhouse' is not in the table" names the typo.
        /// </summary>
        static string UntaggedFaceReport(IRigScriptHost host, string faceSource, RigMeshData data)
        {
            var lvTable = new StringBuilder();
            foreach (var kv in data.LevelIds)
                lvTable.Append(JsStringLiteral(kv.Key)).Append(':')
                       .Append(kv.Value.ToString(CultureInfo.InvariantCulture)).Append(',');

            string report = host.EvaluateString(
                $"(function(){{var F={faceSource};var lvix={{{lvTable.ToString().TrimEnd(',')}}};" +
                "var bad=[],n=0;for(var i=0;i<F.length;i++){var f=F[i];" +
                "if(f.lv==null){n++;if(bad.length<8)bad.push(i+': no lv at all');}" +
                "else if(!Object.prototype.hasOwnProperty.call(lvix,f.lv)){n++;" +
                "if(bad.length<8)bad.push(i+\": lv '\"+f.lv+\"' is not in geometry().ids\");}}" +
                "return n+' of '+F.length+' — '+bad.join('; ');})()");

            return report + "\n  the vocabulary this rig published: " +
                   string.Join(", ", data.LevelIds.OrderBy(kv => kv.Value)
                                                  .Select(kv => $"{kv.Key} {kv.Value}"));
        }

        static string JsStringLiteral(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static Color32 ParseHex(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length < 6)
                throw new FormatException($"'{hex}' is not a #rrggbb colour.");
            return new Color32(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16), 255);
        }
    }
}
