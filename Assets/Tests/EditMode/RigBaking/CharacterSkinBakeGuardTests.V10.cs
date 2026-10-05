using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE RIG 10 BAKE (characterIsoRig10.js rev 10.2), HELD TO RIG 9.2'S GUARDS.</b>
    ///
    /// <para>The rig 10 intake ports the bake onto rig 10 (<see cref="CharacterRigKit.Rig10"/>), and
    /// since its Phase B the committed skins are rig 10's. Every guard 9.2's bake answers to runs here on
    /// rig 10's fresh bakes, through the same bodies (<see cref="GuardRig9"/>): the tone rule and the
    /// rig's sources, the material limit, the pose replay on every clip and frame, the committed
    /// export's clips, the face and tool tracks, the blink, the look, the turn, the golden aim, and
    /// the ink against the rig's own render.</para>
    ///
    /// <para>Three guards are rig 10's own, where 10.2 moved from 9.2: the cell (80 × 104 px, the feet
    /// at (40, 90)); the face, whose marks cull by their own turn band (<c>az</c>) where 9.2's faces
    /// culled by a role's <c>minT</c> (<c>ROLE</c> is gone), with the groups <c>FACE_EMPTY</c> names
    /// binding no face; and the ink, which draws no keyline (owner, 10-01, ruling K4) and holds the
    /// floors the rig's paint culls its marks at. 9.2's versions of the last two
    /// (<see cref="V9_EveryPresetBindsEveryFaceGroupOfTheRig"/>,
    /// <see cref="V9_TheInkIsTheRigsShadingAndRole"/>) read <c>ROLE</c> and stay rig 9's.</para>
    ///
    /// <para>The ten bakes are composed once, on first use, in a host of their own.</para>
    /// </summary>
    public partial class CharacterSkinBakeGuardTests
    {
        IRigScriptHost _v10Host;
        readonly Dictionary<string, CharacterSkinAssetBaker.SkinBake> _v10Bakes =
            new Dictionary<string, CharacterSkinAssetBaker.SkinBake>(StringComparer.Ordinal);
        GuardRig9 _guard10;

        IRigScriptHost V10Host
        {
            get
            {
                if (_v10Host == null)
                {
                    _v10Host = RigScriptHostFactory.Create();
                    CharacterSkinExtractor.Load9(_v10Host, CharacterRigKit.Rig10);
                }
                return _v10Host;
            }
        }

        CharacterSkinAssetBaker.SkinBake V10Bake(string preset)
        {
            if (!_v10Bakes.TryGetValue(preset, out CharacterSkinAssetBaker.SkinBake bake))
                _v10Bakes[preset] = bake = CharacterSkinAssetBaker.ComposeV9(V10Host, preset);
            return bake;
        }

        /// <summary>Rig 10.2 as the guard bodies read it.</summary>
        GuardRig9 Guard10 => _guard10 ??= new GuardRig9(CharacterRigKit.Rig10, () => V10Host, V10Bake);

        /// <summary>The fresh bake of <paramref name="preset"/> from the kit the baker loads today
        /// (<see cref="CharacterSkinAssetBaker.LiveKit"/>): rig 10 since the rig 10 intake's Phase B
        /// (2026-10-02), rig 9 while <see cref="CharacterSkinAssetBaker.LiveRig"/> names it.</summary>
        CharacterSkinAssetBaker.SkinBake LiveBake(string preset) =>
            CharacterSkinAssetBaker.LiveKit == CharacterRigKit.Rig10 ? V10Bake(preset) : V9Bake(preset);

        [OneTimeTearDown]
        public void DisposeV10()
        {
            foreach (CharacterSkinAssetBaker.SkinBake bake in _v10Bakes.Values) DestroyDef(bake.Def);
            _v10Bakes.Clear();
            _v10Host?.Dispose();
            _v10Host = null;
            _guard10 = null;
        }

        // =======================================================================================
        // v10 1-4. rig 9.2's bake guards, on rig 10
        // =======================================================================================

        /// <summary><see cref="V9_ComposesThePlayerUnderTheV9ToneRule"/> on rig 10, whose shading keeps
        /// 9.2's tone rule: the rig, its poses and its revision, the hashes of the committed export,
        /// and every material carried one the bind mesh paints.</summary>
        [Test]
        public void V10_ComposesThePlayerUnderTheV9ToneRule() =>
            ComposesThePlayerUnderTheV9ToneRule(Guard10);

        /// <summary><see cref="V9_EveryPresetPaintsWithinTheV9MaterialLimit"/> on rig 10's ten presets
        /// (<c>CAST10</c>; the twenty NPCs come with the wardrobe).</summary>
        [Test]
        public void V10_EveryPresetPaintsWithinTheV9MaterialLimit() =>
            EveryPresetPaintsWithinTheV9MaterialLimit(Guard10);

        /// <summary><see cref="V9_TheDefReplaysTheRigsOwnPoseOnEveryClipAndFrame"/> on rig 10: every
        /// preset, clip and frame, corner for corner, within <see cref="V9ReplayTolerance"/>.</summary>
        [Test]
        public void V10_TheDefReplaysTheRigsOwnPoseOnEveryClipAndFrame() =>
            TheDefReplaysTheRigsOwnPoseOnEveryClipAndFrame(Guard10);

        /// <summary><see cref="V9_TheDefCarriesEveryClipOfTheCommittedExport"/> on rig 10's committed
        /// exports (<c>builds/&lt;preset&gt;.v10.json</c>).</summary>
        [Test]
        public void V10_TheDefCarriesEveryClipOfTheCommittedExport() =>
            TheDefCarriesEveryClipOfTheCommittedExport(Guard10);

        // =======================================================================================
        // v10 life 2-7 and 9. rig 9.2's life guards, on rig 10
        // =======================================================================================

        /// <summary><see cref="V9_EveryClipCarriesTheRigsFaceAndToolTracks"/> on rig 10.</summary>
        [Test]
        public void V10_EveryClipCarriesTheRigsFaceAndToolTracks() =>
            EveryClipCarriesTheRigsFaceAndToolTracks(Guard10);

        /// <summary><see cref="V9_TheBlinkIsTheRigsBlink"/> on rig 10.</summary>
        [Test]
        public void V10_TheBlinkIsTheRigsBlink() =>
            TheBlinkIsTheRigsBlink(Guard10);

        /// <summary><see cref="V9_TheLookIsTheRigsLook"/> on rig 10.</summary>
        [Test]
        public void V10_TheLookIsTheRigsLook() =>
            TheLookIsTheRigsLook(Guard10);

        /// <summary><see cref="V9_TheLookPortMatchesTheRigsLookAtOverAGrid"/> on rig 10.</summary>
        [Test]
        public void V10_TheLookPortMatchesTheRigsLookAtOverAGrid() =>
            TheLookPortMatchesTheRigsLookAtOverAGrid(Guard10);

        /// <summary><see cref="V9_TheTurnComposesAsTheRigsLookDoes"/> on rig 10.</summary>
        [Test]
        public void V10_TheTurnComposesAsTheRigsLookDoes() =>
            TheTurnComposesAsTheRigsLookDoes(Guard10);

        /// <summary><see cref="V9_TheLookPortLandsWhereTheRigsGoldenCheckDoes"/> on rig 10, against rig
        /// 10's golden report, whose bars are its own (2.82° to 3.73° at 10.2, over 21 targets each:
        /// the taller body aims less closely than 9.2's 1.70° and 2.40°).</summary>
        [Test]
        public void V10_TheLookPortLandsWhereTheRigsGoldenCheckDoes() =>
            TheLookPortLandsWhereTheRigsGoldenCheckDoes(Guard10);

        /// <summary><see cref="V9_EveryPresetsInkMatchesTheRigsOwnRender"/> on rig 10: the def's paint
        /// against rig 10's own render, with the marks culled by their band and no keyline (the rig's
        /// default, which the bake shoots).</summary>
        [Test]
        public void V10_EveryPresetsInkMatchesTheRigsOwnRender() =>
            EveryPresetsInkMatchesTheRigsOwnRender(Guard10);

        // =======================================================================================
        // v10 5. every def is drawn in the rig's own cell
        // =======================================================================================

        /// <summary>
        /// Every def carries the rig's cell, read off the rig (<c>W</c>, <c>H</c>, <c>pivot</c>,
        /// <c>PX</c>, <c>ELEV</c>): 80 × 104 px with the feet at (40, 90) at 10.2, where 9.2's was
        /// 64 × 92 at (32, 82). The figure is painted and measured in that cell, so a def that kept
        /// 9.2's would crop rig 10's taller body.
        /// </summary>
        [Test]
        public void V10_EveryPresetBakesTheRigsOwnCell()
        {
            GuardRig9 guard = Guard10;
            string[] c = guard.Host.EvaluateString(
                "(function(){var G=" + guard.G + ";return [G.W,G.H,G.pivot.x,G.pivot.y,G.PX,G.ELEV].join(',');})()")
                .Split(',');
            Assert.AreEqual(6, c.Length, "Rig 10's cell read as the wrong number of fields.");

            int presets = 0;
            foreach (string preset in CharacterSkinExtractor.Presets9(guard.Host))
            {
                CharacterSkinDef def = guard.Bake(preset).Def;
                Assert.AreEqual(I9(c[0]), def.CellW, $"{preset}: the cell's width (W).");
                Assert.AreEqual(I9(c[1]), def.CellH, $"{preset}: the cell's height (H).");
                Assert.AreEqual((float)D9(c[2]), def.PivotPx.x, $"{preset}: the pivot's x.");
                Assert.AreEqual((float)D9(c[3]), def.PivotPx.y, $"{preset}: the pivot's y.");
                Assert.AreEqual(I9(c[4]), def.PxPerMetre, $"{preset}: the pixels a metre (PX).");
                Assert.AreEqual((float)D9(c[5]), def.ElevationDeg, $"{preset}: the camera's elevation (ELEV).");
                presets++;
            }
            Assert.Greater(presets, 0, "Rig 10 baked no preset.");
            Debug.Log($"[CharacterSkinBakeGuardTests] v10 cell: {presets} presets in {c[0]} x {c[1]} px, pivot " +
                      $"({c[2]}, {c[3]}), {c[4]} px/m, elevation {c[5]} deg.");
        }

        // =======================================================================================
        // v10 6. every preset binds the rig's face, its marks culled by their own turn band
        // =======================================================================================

        /// <summary>
        /// Every def binds rig 10's whole face, as <see cref="V9_EveryPresetBindsEveryFaceGroupOfTheRig"/>
        /// holds 9.2's: its groups are the rig's <c>GROUP_ORDER</c>, its slots the rig's
        /// <c>FACE_SLOTS</c>, its rest face the rig's <c>baseIntent</c> face, and every face of the rig's
        /// <c>bindMesh</c> sits in the one bind mesh, in order. Each corner carries, in UV1, its face's
        /// group, role 0 (no face of rig 10 carries a <c>minT</c>, and the rig has no <c>ROLE</c>), its
        /// head flag, and its mark's turn band (<c>az</c>, the cosine of the band; 0 on a face that is
        /// not a mark); in UV2 its smooth normal (<c>sn</c>) and its flags: a mark (<c>pt</c>), drawn
        /// over the hair (<c>oh</c>), a part the marks draw over (<c>PT_UNDER</c>), the hair, smooth.
        /// The groups <c>FACE_EMPTY</c> names bind no face, on purpose (the flat brow is the fringe's
        /// edge); every other group binds one at least.
        /// </summary>
        [Test]
        public void V10_EveryPresetBindsTheRigsFaceWithMarksCulledByTheirOwnBand()
        {
            GuardRig9 guard = Guard10;
            IRigScriptHost host = guard.Host;
            string g = guard.G;
            string[] order = host.EvaluateString(g + ".GROUP_ORDER.join(',')").Split(',');
            string[] slots = host.EvaluateString("Object.keys(" + g + ".FACE_SLOTS).join(',')").Split(',');
            CollectionAssert.AreEqual(slots, CharacterSkinDef.FaceSlotNames,
                "The def's face slots are not rig 10's FACE_SLOTS, in order.");
            string emptyText = host.EvaluateString("Object.keys(" + g + ".FACE_EMPTY||{}).join(',')");
            var empty = new HashSet<string>(emptyText.Length == 0 ? Array.Empty<string>() : emptyText.Split(','),
                                            StringComparer.Ordinal);
            foreach (string e in empty)
                Assert.Contains(e, order, $"Rig 10's FACE_EMPTY names '{e}', which its GROUP_ORDER does not.");

            string rowsJs =
                "var G=" + g + ",B=G.buildOf(P),F=G.bindMesh(P),GO=G.GROUP_ORDER,PU=G.PT_UNDER||{},hx=B.sk.ix.head,o=[];" +
                "for(var i=0;i<F.length;i++){var f=F[i],gi=f.group?GO.indexOf(f.group)+1:0;if(f.group&&!gi)gi=-1;" +
                "var fl=(f.pt?" + RigMeshBuilder.MarkBit + ":0)|(f.oh?" + RigMeshBuilder.OverHairBit + ":0)|" +
                "(PU[f.part]?" + RigMeshBuilder.UnderMarkBit + ":0)|(f.part==='hair'?" + RigMeshBuilder.HairBit + ":0)|" +
                "(f.sn?" + RigMeshBuilder.SmoothBit + ":0),hd=typeof f.head==='boolean'?f.head:f.bone[0][0][0]===hx;" +
                "o.push([f.v.length,gi,hd?1:0,f.minT||0,f.pt&&f.az!=null?f.az:0,fl,f.sn?f.sn.join('|'):'0|0|0'].join(':'));}" +
                "return o.join(',');";

            var report = new StringBuilder();
            long marksTotal = 0;
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = guard.Bake(preset).Def;
                CollectionAssert.AreEqual(order, def.FaceGroups,
                    $"{preset}: the def's face groups are not rig 10's GROUP_ORDER.");
                Assert.AreEqual(1, def.BindMesh.subMeshCount,
                    $"{preset}: the bind mesh has {def.BindMesh.subMeshCount} submeshes; the face draws in " +
                    "the figure's one draw call.");

                string[] rest = host.EvaluateString(
                    "(function(){var G=" + g + ",f=G.baseIntent(G.buildOf(" + JsQuote9(preset) + ").D).face;" +
                    "return Object.keys(G.FACE_SLOTS).map(function(s){return s+'.'+f[s];}).join(',');})()")
                    .Split(',');
                Assert.AreEqual(rest.Length, def.RestFace.Length,
                    $"{preset}: the def rests {def.RestFace.Length} face slots and the rig {rest.Length}.");
                for (int s = 0; s < rest.Length; s++)
                    Assert.AreEqual(rest[s], GroupName9(def, def.RestFace[s]),
                        $"{preset}: the rest face's {slots[s]} is not the rig's baseIntent face.");

                string[] faces = host.EvaluateString("(function(P){" + rowsJs + "})(" + JsQuote9(preset) + ")").Split(',');
                var uv1 = new List<Vector4>();
                var uv2 = new List<Vector4>();
                def.BindMesh.GetUVs(RigMeshBuilder.FaceUvChannel, uv1);
                def.BindMesh.GetUVs(RigMeshBuilder.MarkUvChannel, uv2);
                Assert.AreEqual(def.BindMesh.vertexCount, uv1.Count,
                    $"{preset}: UV1 holds {uv1.Count} values for {def.BindMesh.vertexCount} vertices.");
                Assert.AreEqual(def.BindMesh.vertexCount, uv2.Count,
                    $"{preset}: UV2 holds {uv2.Count} values for {def.BindMesh.vertexCount} vertices.");

                int corner = 0, misses = 0, marks = 0, smooth = 0, overHair = 0, headFaces = 0;
                var perGroup = new int[order.Length + 1];
                string firstMiss = null;
                for (int i = 0; i < faces.Length; i++)
                {
                    string[] p = faces[i].Split(':');
                    Assert.AreEqual(7, p.Length, $"{preset}: rig face {i} read as {p.Length} fields: {faces[i]}");
                    int n = I9(p[0]), grp = I9(p[1]), head = I9(p[2]), flags = I9(p[5]);
                    string[] sn = p[6].Split('|');
                    Assert.GreaterOrEqual(grp, 0, $"{preset}: rig face {i} names a group GROUP_ORDER does not.");
                    Assert.AreEqual(0d, D9(p[3]), $"{preset}: rig face {i} culls at minT {p[3]}; rig 10's faces carry none.");
                    Assert.LessOrEqual(corner + n, uv1.Count,
                        $"{preset}: the rig's faces run past the bind mesh's {uv1.Count} corners at face {i}.");
                    var e1 = new Vector4(grp, (int)CharacterSkinDef.FaceRole.Body, head, (float)D9(p[4]));
                    var e2 = new Vector4((float)D9(sn[0]), (float)D9(sn[1]), (float)D9(sn[2]), flags);
                    for (int k = 0; k < n; k++)
                    {
                        Vector4 a = uv1[corner + k], b = uv2[corner + k];
                        if (a.x == e1.x && a.y == e1.y && a.z == e1.z && a.w == e1.w &&
                            b.x == e2.x && b.y == e2.y && b.z == e2.z && b.w == e2.w) continue;
                        misses++;
                        firstMiss ??= $"face {i} corner {k} carries {a.ToString("R")} {b.ToString("R")} and the rig " +
                                      $"gives it {e1.ToString("R")} {e2.ToString("R")}";
                    }
                    corner += n;
                    perGroup[grp]++;
                    headFaces += head;
                    if ((flags & RigMeshBuilder.MarkBit) != 0) marks++;
                    if ((flags & RigMeshBuilder.SmoothBit) != 0) smooth++;
                    if ((flags & RigMeshBuilder.OverHairBit) != 0) overHair++;
                }
                Assert.AreEqual(uv1.Count, corner,
                    $"{preset}: the rig's {faces.Length} faces hold {corner} corners and the bind mesh {uv1.Count}.");
                Assert.AreEqual(0, misses,
                    $"{preset}: {misses} bind-mesh corners carry face attributes the rig does not give them; " +
                    $"the first: {firstMiss}.");
                for (int gi = 1; gi <= order.Length; gi++)
                {
                    string name = order[gi - 1];
                    if (empty.Contains(name))
                        Assert.AreEqual(0, perGroup[gi],
                            $"{preset}: '{name}' is one of rig 10's FACE_EMPTY and binds {perGroup[gi]} faces.");
                    else
                        Assert.Greater(perGroup[gi], 0, $"{preset}: no face of the bind mesh is in '{name}'.");
                }
                Assert.Greater(marks, 0, $"{preset}: no face of the bind mesh is a mark, so the band cull is untried.");
                marksTotal += marks;
                report.Append($"\n  {preset}: {faces.Length} faces, {marks} marks, {smooth} smooth, {overHair} over " +
                              $"the hair, {headFaces} on the head; {uv1.Count} corners");
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] v10 face: every preset binds rig 10's {order.Length} face " +
                      $"groups ([{string.Join(",", empty)}] binding none), each face with the rig's group, head " +
                      $"flag, band and flags; {marksTotal:N0} marks:{report}");
        }

        // =======================================================================================
        // v10 7. the ink is the rig's SHADING, with no keyline, and its paint's floors
        // =======================================================================================

        /// <summary>
        /// Each def's ink fields are rig 10's, as <see cref="V9_TheInkIsTheRigsShadingAndRole"/> holds
        /// 9.2's, where 10.2 moved: <c>SHADING.edge</c>, <c>keylineMix</c> and <c>keyline</c>, and the head
        /// snap; no keyline unless a caller asks (<c>SHADING.keylineDefault</c> is false and the paint's
        /// one keyline test is <c>o.keyline===true</c>), so the def draws none (owner, 10-01, K4); no
        /// face culled by a role (the rig has no <c>ROLE</c>); and the floors the paint culls at, each
        /// read off its source and held there once: the face cull
        /// (<c>toward&lt;=Math.max(floor, f.minT||0)</c>), the marks' own loop
        /// (<c>-ny*C.ce+nz*C.se&lt;=floor</c>: the same number, as the def carries one floor), the
        /// shortest horizontal normal a mark's band is asked of (<c>hh&lt;floor</c>), and how near a
        /// pixel edge a mark's centre may fall (one number for x and y).
        /// </summary>
        [Test]
        public void V10_TheInkIsTheRigsShadingWithNoKeylineAndTheMarksFloors()
        {
            GuardRig9 guard = Guard10;
            IRigScriptHost host = guard.Host;
            string g = guard.G;
            string[] s = host.EvaluateString(
                "(function(){var S=" + g + ".SHADING;return [String(S.edge),String(S.keylineMix),String(S.keyline)," +
                "(typeof S.headSnap==='string'&&S.headSnap.length>0)?'1':'0',String(S.keylineDefault)," +
                "typeof " + g + ".ROLE].join('|');})()").Split('|');
            Assert.AreEqual(6, s.Length, "Rig 10's SHADING read as the wrong number of fields.");
            Assert.AreEqual("false", s[4],
                $"Rig 10's SHADING.keylineDefault is '{s[4]}'; ruling K4 follows a rig that draws no keyline.");
            Assert.AreEqual("undefined", s[5], "Rig 10 carries a ROLE; its faces were to cull by their own band.");
            string hex = s[2];
            Assert.IsTrue(Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}$"), $"Rig 10's SHADING.keyline is '{hex}', not #rrggbb.");
            byte Hex(int at) => byte.Parse(hex.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            string source = File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, guard.Kit.ScriptPath));
            double floor = OneLiteral10(source, "its face cull (toward <= Math.max(floor, f.minT||0))",
                @"toward\s*<=\s*Math\.max\(\s*([0-9.eE+-]+)\s*,\s*f\.minT\s*\|\|\s*0\s*\)");
            double markFloor = OneLiteral10(source, "its marks' cull (-ny*C.ce+nz*C.se <= floor)",
                @"if\s*\(\s*-ny\s*\*\s*C\.ce\s*\+\s*nz\s*\*\s*C\.se\s*<=\s*([0-9.eE+-]+)\s*\)\s*continue\s*;");
            double azFloor = OneLiteral10(source, "its marks' band floor (hh < floor || -ny/hh <= f.az)",
                @"if\s*\(\s*hh\s*<\s*([0-9.eE+-]+)\s*\|\|\s*-ny\s*/\s*hh\s*<=\s*f\.az\s*\)\s*continue\s*;");
            MatchCollection edges = Regex.Matches(source,
                @"Math\.abs\(\s*cx\s*-\s*Math\.round\(\s*cx\s*\)\s*\)\s*<\s*([0-9.eE+-]+)\s*\|\|\s*" +
                @"Math\.abs\(\s*cy\s*-\s*Math\.round\(\s*cy\s*\)\s*\)\s*<\s*([0-9.eE+-]+)");
            Assert.AreEqual(1, edges.Count, $"Rig 10's paint holds its marks' pixel-edge test {edges.Count} times, not once.");
            double edgeX = D9(edges[0].Groups[1].Value), edgeY = D9(edges[0].Groups[2].Value);
            Assert.AreEqual(edgeX, edgeY, "Rig 10's marks keep off a pixel edge by two numbers; the def carries one.");
            Assert.AreEqual(floor, markFloor, "Rig 10 culls its marks at a floor its faces do not use; the def carries one.");
            int asked = Regex.Matches(source, @"o\.keyline\s*===\s*true").Count;
            int drawn = Regex.Matches(source, @"o\.keyline\s*!==\s*false").Count;
            Assert.AreEqual(1, asked, $"Rig 10's paint asks for its keyline (o.keyline===true) {asked} times, not once.");
            Assert.AreEqual(0, drawn, $"Rig 10's paint draws its keyline unless refused (o.keyline!==false) {drawn} times.");

            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = guard.Bake(preset).Def;
                Assert.AreEqual(ToneRule.V9, def.ToneRule, $"{preset}: tone rule.");
                Assert.IsTrue(def.HasInk, $"{preset}: the def carries no ink.");
                Assert.AreEqual((float)D9(s[0]), def.Edge, $"{preset}: SHADING.edge.");
                Assert.AreEqual((float)D9(s[1]), def.KeylineMix, $"{preset}: SHADING.keylineMix.");
                Assert.AreEqual(Hex(1), def.Keyline.r, $"{preset}: the keyline's red.");
                Assert.AreEqual(Hex(3), def.Keyline.g, $"{preset}: the keyline's green.");
                Assert.AreEqual(Hex(5), def.Keyline.b, $"{preset}: the keyline's blue.");
                Assert.AreEqual(255, def.Keyline.a, $"{preset}: the keyline's alpha.");
                Assert.AreEqual(s[3] == "1", def.HeadSnap, $"{preset}: SHADING.headSnap.");
                Assert.IsFalse(def.KeylineDefault,
                    $"{preset}: the def draws a keyline unasked; rig 10 draws none unless a caller asks (K4).");
                Assert.AreEqual(Vector4.zero, def.FaceMinToward, $"{preset}: the def culls faces by a role's minT.");
                Assert.AreEqual((float)floor, def.FaceCullFloor, $"{preset}: the paint's cull floor.");
                Assert.AreEqual((float)azFloor, def.FaceMarkAzFloor, $"{preset}: the marks' band floor.");
                Assert.AreEqual((float)edgeX, def.FaceMarkEdge, $"{preset}: the marks' pixel edge.");
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] v10 ink: edge {s[0]}, keyline {hex} mix {s[1]}, drawn by default " +
                      $"{s[4]}, head snap {s[3] == "1"}; floors: faces and marks {R9(floor)}, band {R9(azFloor)}, " +
                      $"pixel edge {R9(edgeX)}.");
        }

        /// <summary>The one literal <paramref name="pattern"/> captures in the rig's source.</summary>
        static double OneLiteral10(string source, string what, string pattern)
        {
            MatchCollection m = Regex.Matches(source, pattern);
            Assert.AreEqual(1, m.Count, $"Rig 10's paint holds {what} {m.Count} times, not once.");
            return D9(m[0].Groups[1].Value);
        }
    }
}
