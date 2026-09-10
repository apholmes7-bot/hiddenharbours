using System.Globalization;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE SKINNED EXPORT (characterIsoRig7.js rev 7.1, drop of 2026-09-09), RE-RUN.</b>
    ///
    /// <para>Rig 7 says the pass-6 body can be re-expressed as ONE skeleton, ONE bind mesh and a set
    /// of bone clips — that a mesh posed by bones lands on the same vertices the rig lathes from
    /// scratch every frame. That claim is the whole reason the mesh character path is affordable
    /// (ADR 0044), so it is measured here rather than read off the drop's README.</para>
    ///
    /// <para><b>What CI actually re-runs.</b> A drop-sized sweep is 4,620 frames over ten builds and
    /// belongs in a standalone harness; what runs here is the same claim on the two builds that
    /// bracket it — <c>fisher</c> (the baked default, 72 two-weight vertices) and <c>nan</c> (the
    /// outlier at 232, and the one build with a bone MISSING). Numbers from the full sweep of
    /// 2026-09-09 are in the PR that landed this file; the guards below are what notices when the
    /// body moves underneath the export.</para>
    ///
    /// <para><b>The failure this exists to catch is silent.</b> Nothing throws when the body is
    /// bumped and the export is not re-run: the skinned mesh simply stops agreeing with the sprite,
    /// one clip at a time, in the frames nobody screenshots.</para>
    /// </summary>
    public class CharacterSkinnedExportTests
    {
        static IRigScriptHost Host()
        {
            var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("characterSkin"));
            return host;
        }

        static double Num(string s) =>
            double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        // ---- the export is the BODY, re-expressed -------------------------------------------------

        /// <summary>
        /// Rig 7 is not a second character: its API object is <c>Object.create(CharacterIso6)</c>, so
        /// every cell fact it appears to own is really the body's, read through the prototype. Loaded
        /// without the body nothing throws — <c>W</c>, <c>pivot</c> and <c>ANIMS</c> just read
        /// <c>undefined</c> — which is why the catalog names the prerequisite and why this asserts the
        /// chain is actually there rather than that the global exists.
        /// </summary>
        [Test]
        public void TheExportInstallsOnTopOfTheBodyItReExpresses()
        {
            using var host = Host();

            Assert.AreEqual(7.0, host.EvaluateNumber("CharacterIso7.pass"), 1e-9,
                            "the export reports a different pass than the catalog names");
            Assert.AreEqual("7.1", host.EvaluateString("CharacterIso7.revision"));

            Assert.IsTrue(host.EvaluateBool("Object.getPrototypeOf(CharacterIso7) === CharacterIso6"),
                "rig 7 stopped prototyping from the body — every W/H/pivot/ANIMS read it inherits is " +
                "now its own copy, and the two can drift without a single test noticing");

            // The base it re-expresses is NAMED by the export itself. A body bump that forgets the
            // export is the one failure that costs nothing at load time and everything at draw time.
            Assert.AreEqual(host.EvaluateString("CharacterIso6.revision"),
                            host.EvaluateString("CharacterIso7.base"),
                            "the export names a base revision the body no longer reports — re-run the " +
                            "export against the shipped body, or say in the PR why the drift is safe");
        }

        // ---- the claim: bones land where the lathe lands ------------------------------------------

        /// <summary>
        /// The golden: for every clip the recipe knows (35 anims + the three long-power casts + every
        /// carry the CARRIES table rides), pose the bind mesh through the EXPORT path — locals →
        /// quaternions → composed worlds → linear blend skin — and measure the worst vertex distance
        /// against the vertices <c>CharacterIso6</c> lathes for the same frame. The rig's own
        /// <c>TOL</c> is 1e-4 m; the drop's sweep came back at 4.02e-13 m, nine orders under it, which
        /// is float noise rather than a tolerance being spent.
        /// </summary>
        [TestCase("fisher")]
        [TestCase("nan")]
        public void TheSkinnedGoldenReproduces_WithinTheRigsOwnTolerance(string preset)
        {
            using var host = Host();

            string report = host.EvaluateString(
                $"(function(){{var r=CharacterIso7.goldenReport({{preset:'{preset}'}});" +
                "return [r.rows.length, r.frames, r.worst, r.tol, r.worstAt, " +
                "r.rows.filter(function(x){return !x.ok;}).length].join('|');})()");

            string[] f = report.Split('|');
            int rows = int.Parse(f[0], CultureInfo.InvariantCulture);
            int frames = int.Parse(f[1], CultureInfo.InvariantCulture);
            double worst = Num(f[2]);
            double tol = Num(f[3]);
            int failing = int.Parse(f[5], CultureInfo.InvariantCulture);

            // A report of nothing passes trivially, so the size of the sweep is part of the assertion.
            Assert.Greater(rows, 40, $"{preset}: the golden recipe collapsed to {rows} rows — a sweep " +
                                     "that stopped covering the clips proves nothing about the ones it dropped");
            Assert.Greater(frames, 400, $"{preset}: only {frames} frames were compared");

            Assert.AreEqual(0, failing,
                $"{preset}: {failing} of {rows} golden rows exceed the rig's own tolerance " +
                $"({tol} m); the worst is {worst:E2} m at '{f[4]}'. The skinned mesh no longer lands " +
                "where the body lathes — re-run the export against the shipped body.");
            Assert.LessOrEqual(worst, tol,
                $"{preset}: worst vertex error {worst:E2} m at '{f[4]}' against tol {tol} m");
        }

        /// <summary>
        /// The golden measures VERTICES; this measures what the player would see. Rig 7 ships
        /// <c>renderSkinned()</c>, the same rasteriser fed from the skinned mesh instead of the lathe,
        /// and <c>diffPixels()</c> compares it to <c>render()</c> frame for frame. The drop's sweep was
        /// 36,960 probes at zero differing pixels; this is a per-direction slice of it, including the
        /// two clip families the export added most recently (a carry, and a saddle clip that has no
        /// machine to sit on and so renders against the rig's generic fallback).
        /// </summary>
        [Test]
        public void TheSkinnedRenderIsByteIdenticalToTheSpriteRender()
        {
            using var host = Host();

            string result = host.EvaluateString(
                "(function(){" +
                "var A=[{a:'idle'},{a:'walk'},{a:'haul'},{a:'dig'},{a:'board'}," +
                "       {a:'cast',p:'long'},{a:'walk',c:'buckets'},{a:'astride'}];" +
                "var probes=0,moved=0,px=0;" +
                "for(var i=0;i<A.length;i++){var n=CharacterIso6.ANIMS[A[i].a].frames;" +
                " for(var d=0;d<8;d++){" +
                "  var o={anim:A[i].a,frame:d%n,build:{preset:'fisher'}};" +
                "  if(A[i].p)o.power=A[i].p; if(A[i].c)o.carry=A[i].c;" +
                "  var r=CharacterIso7.diffPixels(d,o);" +
                "  probes++; px+=r.count; if(r.count>0)moved++;}}" +
                "return [probes,moved,px].join('|');})()");

            string[] f = result.Split('|');
            int probes = int.Parse(f[0], CultureInfo.InvariantCulture);
            int moved = int.Parse(f[1], CultureInfo.InvariantCulture);
            int px = int.Parse(f[2], CultureInfo.InvariantCulture);

            Assert.AreEqual(64, probes, "the probe grid changed shape — 8 clips x 8 directions");
            Assert.AreEqual(0, moved,
                $"{moved} of {probes} probes differ, {px} pixels in total. The skinned mesh and the " +
                "lathe no longer rasterise to the same image, so the mesh character would ship a " +
                "figure the sheets never showed.");
        }

        // ---- the exception, adjudicated -----------------------------------------------------------

        /// <summary>
        /// <b>The two-weight rings are load-bearing, and this is the measurement that says so.</b>
        ///
        /// <para>Almost every vertex in the bind mesh belongs to exactly ONE bone — the fisher is 2,920
        /// of 2,992. The remainder are rings the rig 6 lathe lerps between two frames (a sleeve hem at
        /// 0.62 of a tube whose DRAWN LENGTH changes, an apron or skirt row between a floor-anchored hem
        /// and the torso), and rig 7 declares each of them in <c>BLENDED</c> with the reason. A one-bone
        /// export would be simpler, smaller, and would let a consumer assume rigid parts — so the
        /// exception has to earn itself rather than be taken on trust.</para>
        ///
        /// <para>Collapse every two-weight vertex onto its heavier bone and re-run the golden: on the
        /// fisher the worst error goes from 4.02e-13 m to <b>4.52e-2 m — 452x the tolerance — and ALL
        /// 56 rows fail</b>. That is 4.5 cm of sleeve, on a figure 64 px wide. Restoring the weights
        /// puts it back. Nothing above 2 bones exists anywhere in the mesh, which is what keeps the
        /// export inside every consumer's blend budget.</para>
        /// </summary>
        [Test]
        public void TheTwoWeightRingsCannotBeCollapsedToOneBone()
        {
            using var host = Host();

            string result = host.EvaluateString(
                "(function(){" +
                "var b={preset:'fisher'};" +
                "var F=CharacterIso7.bindMesh(b), verts=0, two=0, over=0, save=[];" +
                "for(var i=0;i<F.length;i++){var W=F[i].bone||[];" +
                " for(var k=0;k<W.length;k++){ verts++;" +
                "  if(W[k].length>2){over++;}" +
                "  else if(W[k].length===2){ two++; save.push([i,k,W[k]]);" +
                "   W[k]=[[ W[k][0][1]>=W[k][1][1] ? W[k][0][0] : W[k][1][0], 1 ]]; } } }" +
                "var bad=CharacterIso7.goldenReport(b);" +
                "for(var j=0;j<save.length;j++) F[save[j][0]].bone[save[j][1]]=save[j][2];" +
                "var back=CharacterIso7.goldenReport(b);" +
                "return [verts,two,over,bad.worst,bad.rows.filter(function(x){return !x.ok;}).length," +
                "        bad.rows.length,back.worst,back.tol].join('|');})()");

            string[] f = result.Split('|');
            int verts = int.Parse(f[0], CultureInfo.InvariantCulture);
            int two = int.Parse(f[1], CultureInfo.InvariantCulture);
            int over = int.Parse(f[2], CultureInfo.InvariantCulture);
            double collapsedWorst = Num(f[3]);
            int collapsedFailing = int.Parse(f[4], CultureInfo.InvariantCulture);
            int rows = int.Parse(f[5], CultureInfo.InvariantCulture);
            double restoredWorst = Num(f[6]);
            double tol = Num(f[7]);

            Assert.AreEqual(0, over,
                $"{over} vertices carry more than two bone weights. The export's whole affordability " +
                "argument is that a consumer never needs more than two influences.");
            Assert.Greater(two, 0, "no two-weight vertices at all — either the mesh changed or the " +
                                   "census stopped finding the weights, and the test below is vacuous");
            Assert.Less(two, verts / 10,
                $"{two} of {verts} vertices are blended. The export is supposed to be overwhelmingly " +
                "rigid; if that stopped being true, the size argument in ADR 0044 needs re-making.");

            Assert.Greater(collapsedWorst, 100 * tol,
                $"collapsing the {two} blended vertices onto one bone cost only {collapsedWorst:E2} m " +
                $"against a {tol} m tolerance — the exception no longer earns itself, so take it out " +
                "of BLENDED and let the export be rigid.");
            Assert.AreEqual(rows, collapsedFailing,
                "the collapse was expected to break every golden row; it broke " +
                $"{collapsedFailing} of {rows}");

            // The census mutates the rig's cached bind mesh in place, so the restore is part of the
            // claim: an unrestored weight would leave every later assertion measuring a broken mesh.
            Assert.LessOrEqual(restoredWorst, tol,
                $"the weights were not put back: the golden still reads {restoredWorst:E2} m");
        }
    }
}
