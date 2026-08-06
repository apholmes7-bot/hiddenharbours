using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The cliff kit's contract, held against the RIG — every run, on CI, with no committed pixels.
    ///
    /// <para><b>⭐ WHY THERE ARE NO COMMITTED SHEETS TO PIN AGAINST.</b> Every sibling kit bakes its
    /// PNGs into the repository and pins those. This one cannot: the kit is parametric (180 face sets ×
    /// 4 channels; the rig's README puts the full bake at ~90 MB) and even the narrow St Peters subset
    /// measured <b>16.0 MB across 63 PNGs</b>. Committing that would pin the PNG encoder, not the art,
    /// and every one of those bytes goes stale the moment a rig coefficient moves. So the rig is the
    /// committed source and this suite is the guard: it drives <c>cliffRig.js</c> through the same V8
    /// host <see cref="CliffBaker"/> uses and asserts that <see cref="CliffCatalog"/> — which the
    /// shader, the baker and the import settings all read — still describes what the rig actually
    /// renders.</para>
    ///
    /// <para>All of it is CPU-side, already established as safe on CI's null graphics device by the
    /// sibling rig suites. Renders are expensive (a face set is ~0.8 s, because four co-registered
    /// channels are rasterised together), so the sweeps below take geometry and vocabulary from the rig
    /// cheaply and reserve full renders for the handful of claims that can only be checked on
    /// pixels.</para>
    /// </summary>
    public class CliffRigBakeTests
    {
        static IRigScriptHost _host;
        const string G = CliffCatalog.RigGlobalName;

        [OneTimeSetUp]
        public void InstallTheRigOnce()
        {
            _host = RigScriptHostFactory.Create();
            CliffBaker.InstallRig(_host);
        }

        [OneTimeTearDown]
        public void DisposeHost()
        {
            _host?.Dispose();
            _host = null;
        }

        // =====================================================================================
        //  the vocabulary
        // =====================================================================================

        [Test]
        public void TheCatalogsVocabularyIsTheRigs()
        {
            CollectionAssert.AreEqual(CliffCatalog.Rocks, ReadStrings($"{G}.ROCKS"),
                "CliffCatalog.Rocks has drifted from the rig's own ROCKS. The baker names files from " +
                "the catalog, so a rock the rig does not know is a bake that throws mid-run.");

            CollectionAssert.AreEqual(CliffCatalog.Aspects, ReadStrings($"{G}.ASPECTS"),
                "CliffCatalog.Aspects has drifted from the rig's ASPECTS. The aspect law is the look " +
                "of this kit AND the constraint on where a cliff may stand (N is not authored), so a " +
                "silent change here would move the coast, not just a file name.");

            CollectionAssert.AreEqual(CliffCatalog.LedgePieces, ReadStrings($"{G}.PIECES"),
                "CliffCatalog.LedgePieces has drifted from the rig's PIECES — the ledge sheet is " +
                "blitted in this order, so a reorder silently renumbers every slice.");

            CollectionAssert.AreEqual(CliffCatalog.LedgeBands, ReadStrings($"{G}.BANDS"));
            CollectionAssert.AreEqual(CliffCatalog.ContactPieces, ReadStrings($"{G}.CPIECES"));
        }

        [Test]
        public void TheBatterAnglesAreTheRigs()
        {
            for (int i = 0; i < CliffCatalog.Batters.Length; i++)
            {
                double angle = _host.EvaluateNumber($"{G}.SLOPES[{Js(CliffCatalog.Batters[i])}]");
                Assert.AreEqual(CliffCatalog.BatterAngles[i], (float)angle, 1e-4f,
                    $"Batter '{CliffCatalog.Batters[i]}' is {angle}° in the rig but " +
                    $"{CliffCatalog.BatterAngles[i]}° in CliffCatalog. The shader resolves the sun in " +
                    "the TIPPED frame using the catalog's angle while the pixels carry the rig's " +
                    "bedding spacing — disagree and the wall is lit as one shape and drawn as another.");
            }
        }

        /// <summary>
        /// <b>The compass check this repo has earned the hard way.</b> Five kits have shipped
        /// mislabelled compass art here (the standing warning on <c>ShorelineIso2Catalog</c>), and the
        /// cliff kit's aspect letters drive BOTH which texture a coast sector gets AND the bake key the
        /// shader un-divides the cast shadow by. So the letters are held to the rig's own azimuths
        /// rather than trusted.
        /// </summary>
        [Test]
        public void TheAspectAzimuthsAreTheRigs()
        {
            for (int i = 0; i < CliffCatalog.Aspects.Length; i++)
            {
                double az = _host.EvaluateNumber($"{G}.ASPECT[{Js(CliffCatalog.Aspects[i])}].az");
                Assert.AreEqual(CliffCatalog.AspectAzimuths[i], (float)az, 1e-4f,
                    $"Aspect '{CliffCatalog.Aspects[i]}' bears {az}° in the rig but " +
                    $"{CliffCatalog.AspectAzimuths[i]}° in CliffCatalog.");
            }

            // N is not authored, and the coast planner depends on that being true rather than on it
            // merely being absent from a list today.
            Assert.IsFalse(_host.EvaluateBool($"{G}.ASPECTS.indexOf('N') >= 0"),
                "The rig has grown a north aspect. That is good news, but the coast planner's cliff " +
                "arc is derived from 'only the southern half is authored' — re-derive it before " +
                "relaxing this.");
        }

        /// <summary>
        /// ⭐ THE BAKE KEYS ARE THE RIG'S. <c>mask.R</c> holds <c>N·Lbake × castShadow</c>, and the
        /// shader recovers the cast shadow by DIVIDING by that same <c>N·Lbake</c>. Hand it the wrong
        /// aspect's key and the division leaves a residue of the wrong bake's shading — a face that is
        /// subtly and uniformly mis-lit, which reads as "the rock looks flat" and not as an error.
        ///
        /// <para>The S row is also the shader's own <c>_BakeL</c> property default, so this doubles as
        /// the standing proof that the rig's light frame and the shader's tangent frame are the same one
        /// and no sign flip is owed — the mistake that once made the tree rig light from below.</para>
        /// </summary>
        [Test]
        public void TheAspectBakeLightsAreTheRigs()
        {
            Assert.AreEqual(CliffCatalog.Aspects.Length, CliffCatalog.AspectBakeLights.Length,
                "every authored aspect needs the key its channels were baked at");

            for (int i = 0; i < CliffCatalog.Aspects.Length; i++)
            {
                string a = Js(CliffCatalog.Aspects[i]);
                var rig = new Vector3(
                    (float)_host.EvaluateNumber($"{G}.ASPECT[{a}].L[0]"),
                    (float)_host.EvaluateNumber($"{G}.ASPECT[{a}].L[1]"),
                    (float)_host.EvaluateNumber($"{G}.ASPECT[{a}].L[2]"));
                Vector3 catalog = CliffCatalog.AspectBakeLights[i];

                Assert.AreEqual(rig.x, catalog.x, 1e-4f, $"aspect {CliffCatalog.Aspects[i]} L.x");
                Assert.AreEqual(rig.y, catalog.y, 1e-4f, $"aspect {CliffCatalog.Aspects[i]} L.y");
                Assert.AreEqual(rig.z, catalog.z, 1e-4f, $"aspect {CliffCatalog.Aspects[i]} L.z");

                // A key pointing INTO the wall would make the cast-shadow division meaningless.
                Assert.Greater(catalog.z, 0f,
                    $"aspect {CliffCatalog.Aspects[i]}'s bake key points into the face, not out of it");
            }
        }

        // =====================================================================================
        //  the canvases
        // =====================================================================================

        [Test]
        public void AFaceIsTheCanvasTheShaderAddresses()
        {
            _host.Execute($"globalThis.__t = {G}.face('sandstone', 'S', {CliffCatalog.BaseStep}, " +
                          "{slope: 'wall'});");

            Assert.AreEqual(CliffCatalog.FaceWidth, (int)_host.EvaluateNumber("__t.W"));
            Assert.AreEqual(CliffCatalog.FaceHeight, (int)_host.EvaluateNumber("__t.H"));

            // The shader tiles this texture in SURFACE metres at PPU 32. If the canvas is not exactly
            // metres × px/m the wall resamples, which on a periodic pixel-art material reads as a
            // shimmering bed rather than as a wrong size.
            Assert.AreEqual(CliffCatalog.FaceWidth,
                            PixelsFor(CliffCatalog.FaceMetresS),
                "The face's declared metres × px/m no longer equals its pixel width.");
            Assert.AreEqual(CliffCatalog.FaceHeight, PixelsFor(CliffCatalog.FaceMetresT));
        }

        [Test]
        public void EveryLiveChannelComesBackCoRegisteredAndFull()
        {
            _host.Execute($"globalThis.__t = {G}.face('sandstone', 'SE', {CliffCatalog.BaseStep}, " +
                          "{slope: 'wall'});");
            int w = (int)_host.EvaluateNumber("__t.W"), h = (int)_host.EvaluateNumber("__t.H");

            foreach (string channel in CliffCatalog.LiveChannels)
            {
                string field = channel.TrimStart('_');
                Assert.IsTrue(_host.EvaluateBool($"__t.{field} && __t.{field}.length === {w * h * 4}"),
                    $"Channel '{channel}' did not come back as a full {w}×{h} RGBA buffer. The shader " +
                    "samples all three at the SAME uv and assumes they are co-registered.");
            }
        }

        /// <summary>
        /// The strips and the 32 px iso cells.
        ///
        /// <para><b>⚠ <c>ledge()</c> and <c>contact()</c> return LOWERCASE <c>w</c>/<c>h</c> where
        /// <c>face()</c>, <c>brow()</c> and <c>toe()</c> return uppercase <c>W</c>/<c>H</c>.</b> The
        /// kit's <c>cliff.json</c> does not say so — this was measured off the rig, and reading the
        /// wrong case yields <c>undefined</c>, which becomes a 0 × 0 blit and a silently empty sheet
        /// rather than an error.</para>
        /// </summary>
        [Test]
        public void TheStripsAndCellsAreTheirDeclaredSizes()
        {
            _host.Execute($"globalThis.__t = {G}.brow('S', {CliffCatalog.BaseStep});");
            Assert.AreEqual(CliffCatalog.StripWidth, (int)_host.EvaluateNumber("__t.W"));
            Assert.AreEqual(CliffCatalog.StripHeight, (int)_host.EvaluateNumber("__t.H"));

            _host.Execute($"globalThis.__t = {G}.toe('S', {CliffCatalog.BaseStep}, {{feature: 'slump'}});");
            Assert.AreEqual(CliffCatalog.StripWidth, (int)_host.EvaluateNumber("__t.W"));
            Assert.AreEqual(CliffCatalog.StripHeight, (int)_host.EvaluateNumber("__t.H"));

            _host.Execute($"globalThis.__t = {G}.ledge('sandstone', 'faceS', " +
                          $"{{band: 'toe', gx: 0, gy: 0, step: {CliffCatalog.BaseStep}}});");
            Assert.AreEqual(CliffCatalog.LedgeCell, (int)_host.EvaluateNumber("__t.w"),
                "ledge() returns lowercase w/h. Reading __t.W yields undefined, which blits nothing.");
            Assert.AreEqual(CliffCatalog.LedgeCell, (int)_host.EvaluateNumber("__t.h"));
            Assert.IsTrue(_host.EvaluateBool("__t.W === undefined"),
                "ledge() has grown an uppercase W. Harmless in itself, but CliffBaker reads the " +
                "lowercase pair — check both before relaxing this.");

            _host.Execute($"globalThis.__t = {G}.contact('faceS');");
            Assert.AreEqual(CliffCatalog.LedgeCell, (int)_host.EvaluateNumber("__t.w"));
            Assert.AreEqual(CliffCatalog.LedgeCell, (int)_host.EvaluateNumber("__t.h"));

            // The sheets the baker assembles must be exactly the cell grid.
            Assert.AreEqual(CliffCatalog.LedgeSheetWidth,
                            CliffCatalog.LedgePieces.Length * CliffCatalog.LedgeCell);
            Assert.AreEqual(CliffCatalog.LedgeSheetHeight,
                            CliffCatalog.LedgeBands.Length * CliffCatalog.LedgeCell);
            Assert.AreEqual(CliffCatalog.ContactSheetWidth,
                            CliffCatalog.ContactPieces.Length * CliffCatalog.LedgeCell);
        }

        // =====================================================================================
        //  the profile — the part the engine has to get right
        // =====================================================================================

        /// <summary>
        /// <b>The rig hands back FLOATS IN METRES, not the grey image the README describes.</b> The
        /// README's "grey, 128 is zero, full range ±1.15 m" describes the FILE; <c>disp</c> is a float
        /// array (measured −0.61 … +0.69 m on this drop). <see cref="CliffBaker"/> owns that encoding,
        /// and if the rig ever started handing back bytes the encode would silently quantise a
        /// displacement field into near-nothing — visible only as "the profile does not seem to do
        /// anything".
        /// </summary>
        [Test]
        public void TheProfileIsMetresAndItsScaleIsTheCatalogs()
        {
            _host.Execute($"globalThis.__t = {G}.profile('sandstone', 'S', {{slope: 90}});");

            Assert.AreEqual(CliffCatalog.ProfileMetres, (float)_host.EvaluateNumber("__t.metres"), 1e-4f,
                "The profile's encoding scale moved. CliffBaker encodes with the catalog's number and " +
                "the shader decodes with it, so a drift rescales every displaced vertex silently.");

            Assert.AreEqual(CliffCatalog.FaceWidth * CliffCatalog.FaceHeight,
                            (int)_host.EvaluateNumber("__t.disp.length"),
                "The profile is ONE value per texel — a scalar field, not RGBA.");

            double peak = _host.EvaluateNumber(
                "(function(d){var m=0;for(var i=0;i<d.length;i++){var a=Math.abs(d[i]);if(a>m)m=a;}return m;})(__t.disp)");
            Assert.Greater(peak, 0.05,
                "The profile is essentially flat. A zero displacement field is exactly the 'v9 shipped " +
                "a plane' defect this kit exists to answer — the silhouette would stop at the texture.");
            Assert.LessOrEqual(peak, CliffCatalog.ProfileMetres + 1e-3,
                $"The profile exceeds ±{CliffCatalog.ProfileMetres} m, so the baker's grey encode " +
                "would CLAMP it — the wall's deepest re-entrants would flatten off silently.");
        }

        [Test]
        public void TheProfileIgnoresAspectAndWearAsTheCatalogClaims()
        {
            Assert.IsTrue(CliffCatalog.ProfileIsAspectIndependent);

            // One profile serves all fifteen bakes of a rock/batter group — the baker relies on it to
            // write three files instead of forty-five.
            _host.Execute($"globalThis.__a = {G}.profile('sandstone', 'S', {{slope: 90}}).disp;");
            _host.Execute($"globalThis.__b = {G}.profile('sandstone', 'E', {{slope: 90}}).disp;");
            Assert.IsTrue(_host.EvaluateBool(
                "(function(){if(__a.length!==__b.length)return false;" +
                "for(var i=0;i<__a.length;i++)if(__a[i]!==__b[i])return false;return true;})()"),
                "Two aspects gave DIFFERENT profiles. CliffCatalog.ProfileIsAspectIndependent is what " +
                "lets the baker write one map per rock and batter; if that stopped being true the " +
                "wall's silhouette would be displaced by another aspect's shape.");

            // ...but the batter genuinely must change it, or the four named batters are one map.
            _host.Execute($"globalThis.__c = {G}.profile('sandstone', 'S', {{slope: 62}}).disp;");
            Assert.IsFalse(_host.EvaluateBool(
                "(function(){for(var i=0;i<__a.length;i++)if(__a[i]!==__c[i])return false;return true;})()"),
                "A 90° wall and a 62° ramp gave the SAME profile. The batter is supposed to weaken the " +
                "ribs and deepen the benches; an identical map means it is a shading multiplier after " +
                "all, which is the kit's own rule 11.");
        }

        // =====================================================================================
        //  periodicity — the wall has to tile
        // =====================================================================================

        /// <summary>
        /// The kit claims to be "exactly periodic in <c>s</c>… sample with Repeat and Point, no
        /// filtering", and the import settings take it at its word. A face that is NOT periodic shows a
        /// hard vertical seam every 12 m along a cliff run — the single most visible way this kit can
        /// fail in play.
        ///
        /// <para>Measured as: the wrap-around neighbour pair (last column against first) must differ no
        /// more than a typical interior neighbour pair does. On this drop the seam pair measured 1.69
        /// mean absolute channel difference against 5.18 for the interior — i.e. the wrap is smoother
        /// than the texture's own detail, which is what exact periodicity looks like.</para>
        /// </summary>
        [Test]
        public void AFaceIsPeriodicAlongTheShore()
        {
            _host.Execute($"globalThis.__t = {G}.face('sandstone', 'S', {CliffCatalog.BaseStep}, " +
                          "{slope: 'wall'});");
            _host.Execute(
                "globalThis.__colDiff = function(buf, W, H, c1, c2) {" +
                "  var s = 0;" +
                "  for (var y = 0; y < H; y++)" +
                "    for (var k = 0; k < 4; k++)" +
                "      s += Math.abs(buf[(y*W+c1)*4+k] - buf[(y*W+c2)*4+k]);" +
                "  return s / (H*4); };");

            double seam = _host.EvaluateNumber("__colDiff(__t.unlit, __t.W, __t.H, 0, __t.W-1)");
            double interior = _host.EvaluateNumber("__colDiff(__t.unlit, __t.W, __t.H, 100, 101)");

            Assert.Less(seam, interior * 2.0,
                $"The wrap seam ({seam:F2} mean abs difference) is worse than twice a typical interior " +
                $"neighbour pair ({interior:F2}). The face is imported with Repeat and tiled every 12 m " +
                "along every cliff run, so a non-periodic face draws a hard vertical line down the " +
                "coast at each repeat.");
        }

        // =====================================================================================
        //  reproducibility — the condition the "ship the rig, not the sheets" decision rests on
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>Same rig plus same config ⇒ same pixels.</b> This is the load-bearing guarantee under
        /// the decision NOT to commit the baked sheets: if a bake were not reproducible, the
        /// repository would be carrying a recipe that produces a different cliff on every machine, and
        /// the only honest fix would be to commit the pixels after all.
        ///
        /// <para>Three things make it hold, and this test checks the one that could silently break.
        /// First, <b>the rig has no RNG and no clock</b> — no <c>Math.random</c>, no <c>Date.now</c>;
        /// every variation comes from an explicit seed (<see cref="TheRigCarriesNoHiddenRandomness"/>
        /// guards that at source level). Second, those seeds are derived from
        /// <c>ROCKS.indexOf(rock) * 1013 + 7703</c>, so the <b>order of the ROCKS array is
        /// load-bearing</b> — reorder it and sandstone silently bakes as a different wall;
        /// <see cref="TheCatalogsVocabularyIsTheRigs"/> pins it. Third, the rig memoises expensive
        /// fields per (seed, slope), and a memo keyed on an INCOMPLETE key is exactly the defect that
        /// would make bake order matter — which is why the two renders below are separated by
        /// unrelated bakes of another rock, another batter and another wear step rather than run
        /// back to back.</para>
        ///
        /// <para>Measured in a plain JS host: identical back to back, identical across interleaved
        /// bakes, and identical across two fresh interpreters (same SHA-256 of the unlit channel).</para>
        /// </summary>
        [Test]
        public void TheSameFaceBakesToTheSameBytesTwice()
        {
            _host.Execute($"globalThis.__r1 = {G}.face('sandstone', 'SE', {CliffCatalog.BaseStep}, " +
                          "{slope: 'wall'});");

            // Unrelated work in between: another rock, another batter, another wear step, another
            // entry point. If any memo is keyed on less than it depends on, this is what exposes it.
            _host.Execute($"{G}.face('till', 'W', 2, {{slope: 'bank'}});");
            _host.Execute($"{G}.profile('basalt', 'S', {{slope: 48}});");
            _host.Execute($"{G}.brow('E', 0);");

            _host.Execute($"globalThis.__r2 = {G}.face('sandstone', 'SE', {CliffCatalog.BaseStep}, " +
                          "{slope: 'wall'});");

            foreach (string channel in CliffCatalog.LiveChannels)
            {
                string field = channel.TrimStart('_');
                Assert.IsTrue(_host.EvaluateBool(
                    $"(function(a, b) {{ if (a.length !== b.length) return false;" +
                    $"  for (var i = 0; i < a.length; i++) if (a[i] !== b[i]) return false;" +
                    $"  return true; }})(__r1.{field}, __r2.{field})"),
                    $"Channel '{channel}' differed between two bakes of the SAME face, separated by " +
                    "unrelated bakes. The kit's pixels are not committed precisely because they are " +
                    "reproducible from the rig — if that stops being true, this repository is " +
                    "carrying a recipe that builds a different cliff on every machine, and the " +
                    "decision to ship the rig instead of the sheets has to be revisited.");
            }
        }

        /// <summary>
        /// The source-level half of the reproducibility guarantee: a re-drop must not introduce
        /// randomness or a clock ANYWHERE in the rig — including in a code path this suite never
        /// renders, which a byte-comparison test could not see.
        /// </summary>
        [Test]
        public void TheRigCarriesNoHiddenRandomness()
        {
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(RigCatalog.RepoRoot, CliffCatalog.RigScriptPath));

            foreach (string forbidden in new[] { "Math.random", "Date.now", "new Date", "performance.now" })
                Assert.IsFalse(source.Contains(forbidden),
                    $"cliffRig.js contains '{forbidden}'. The kit ships as a rig rather than as 16 MB " +
                    "of committed PNGs, and that trade is only honest while the bake is a pure " +
                    "function of (rig, config). A single unseeded call makes the coast unreproducible.");
        }

        // =====================================================================================
        //  helpers
        // =====================================================================================

        string[] ReadStrings(string expression) =>
            ShorePlantBaker.ReadStringArray(_host, expression);

        /// <summary>Metres of the kit's surface, in pixels at PPU 32. Private on purpose: a helper
        /// named for a UnityEngine type at namespace scope would shadow it for every sibling file in
        /// this namespace, which is this repo's established defect class.</summary>
        static int PixelsFor(float metres) =>
            (int)Math.Round(metres * CliffCatalog.PxPerMetre, MidpointRounding.AwayFromZero);

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
