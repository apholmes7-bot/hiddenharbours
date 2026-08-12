using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The navigation-buoy kit's registration, proven against the LIVE rig.
    ///
    /// <para><b>⭐ WHY THIS FIXTURE EXISTS.</b> Three of this kit's registration facts would fail
    /// SILENTLY rather than loudly if they were wrong, and each has an in-repo precedent for doing
    /// exactly that:</para>
    /// <list type="number">
    ///   <item><b>The turntable substitution.</b> The drop ships its own <c>isoSolid.js</c> installing
    ///     <c>globalThis.IsoSolid</c> — a global the deck-loop kit already owns. We register the
    ///     EXISTING one as a prerequisite instead of committing a second copy. If the two ever part,
    ///     whichever loaded last would silently win for BOTH kits and the wrong one renders. Pinned by
    ///     <see cref="TheVendoredTurntableIsCharacterIdenticalToTheRegisteredOne"/>.</item>
    ///   <item><b>The handedness.</b> Registered Clockwise. Apply the fleet's CounterClockwise
    ///     correction and all eight cells of all seventy mirror — plausible art, wrong compass. Pinned
    ///     by <see cref="TheKitIsClockwiseOnTheGroundPlane"/>, and
    ///     <see cref="TheScreenAngleCannotDecideHandedness"/> proves why the easy measurement is the
    ///     wrong one.</item>
    ///   <item><b>The pivot.</b> It is the WATERLINE, not the ground — every other kit here pivots on
    ///     the ground under the object. Pinned by <see cref="ThePivotIsTheWaterlineAtEveryFacing"/>.</item>
    /// </list>
    /// </summary>
    public class NavBuoyRegistrationProbeTests
    {
        const string Key = NavBuoyKit.RigKey;
        const string G = "NavBuoy";

        static IRigScriptHost _host;
        static NavBuoyRegistrationProbe.Result _probe;

        [OneTimeSetUp]
        public void InstallOnce()
        {
            _host = RigScriptHostFactory.Create();

            // InstallModule, never Install: this rig exposes no W/H/pivot triple and Install throws on
            // the missing pivot. The declared prerequisite (deckIsoSolid) comes with it, depth first.
            RigCatalog.InstallModule(_host, RigCatalog.Get(Key));

            _probe = NavBuoyRegistrationProbe.Measure(_host);
        }

        [OneTimeTearDown]
        public void Dispose() => _host?.Dispose();

        // ---- the registration itself ---------------------------------------------------------------

        [Test]
        public void TheEntryIsRegisteredAndDistinctFromTheLobsterBuoy()
        {
            var nav = RigCatalog.Get(Key);
            var pot = RigCatalog.Get("buoyIso");

            Assert.That(nav.GlobalName, Is.EqualTo("NavBuoy"));
            Assert.That(pot.GlobalName, Is.EqualTo("BuoyIso"));
            Assert.That(nav.GlobalName, Is.Not.EqualTo(pot.GlobalName),
                "The two buoy families must install DIFFERENT globals. A collision would not throw — " +
                "the second load would replace the first and one kit would render as the other.");
            Assert.That(nav.ScriptPath, Is.Not.EqualTo(pot.ScriptPath));
        }

        [Test]
        public void EveryRegisteredGlobalIsUnique()
        {
            // The whole catalog, not just the two buoys: a new kit colliding with ANY existing global
            // is the same silent failure, and the shop kit's load-order defects (#437) rendered wrong
            // rather than throwing.
            var dupes = RigCatalog.Entries.Values
                                  .GroupBy(e => e.GlobalName, StringComparer.Ordinal)
                                  .Where(g => g.Count() > 1)
                                  .Select(g => g.Key)
                                  .ToArray();

            Assert.That(dupes, Is.Empty,
                $"Two catalog entries install the same global: {string.Join(", ", dupes)}. Load order " +
                "would decide which rig wins, silently.");
        }

        [Test]
        public void TheKitDeclaresTheDeckLoopTurntableAsItsPrerequisite()
        {
            var nav = RigCatalog.Get(Key);
            Assert.That(nav.Prerequisites, Does.Contain("deckIsoSolid"),
                "Without the turntable this rig throws on the first render. Declaring it here is what " +
                "stops each caller having to remember.");
        }

        /// <summary>
        /// The sha256 of the <c>isoSolid.js</c> the 2026-08-11 nav-buoy drop shipped, line endings
        /// normalised to LF. The registered <c>deckIsoSolid</c> hashed to exactly this, which is what
        /// licensed registering the existing turntable instead of committing the drop's copy.
        /// </summary>
        const string DropTurntableSha256Lf =
            "f7fc9db510b0346dea568c43dc7378f1a12fd71ee8f5a7653e78377812b792bd";

        /// <summary>
        /// ⚠️ The load-bearing assertion behind the whole registration. The drop's <c>isoSolid.js</c>
        /// is NOT committed (a second copy of a registered global's source is the drift the no-edit
        /// rule exists to prevent), so this pins the claim that licensed omitting it: the registered
        /// turntable is still character-for-character what the nav-buoy drop was authored against.
        ///
        /// <para>Hash rather than file-diff on purpose. The uncommitted copy cannot be compared to in
        /// a clean checkout, and a test that quietly skips is not a guard. This fires on the direction
        /// that CAN change silently in-repo — someone editing <c>deckIsoSolid</c> for the trap loop and
        /// moving this kit's turntable as a side effect.</para>
        /// </summary>
        [Test]
        public void TheRegisteredTurntableStillMatchesTheOneTheDropWasAuthoredAgainst()
        {
            string registered = Path.Combine(RigCatalog.RepoRoot,
                                             RigCatalog.Get("deckIsoSolid").ScriptPath);
            Assert.That(File.Exists(registered), Is.True, $"missing {registered}");

            // Line endings differ by checkout (the repo's copy is CRLF, the drop's LF); that is not a
            // content change, so the hash is taken over LF-normalised text.
            string lf = File.ReadAllText(registered).Replace("\r\n", "\n");
            string sha;
            using (var h = System.Security.Cryptography.SHA256.Create())
                sha = BitConverter.ToString(h.ComputeHash(new System.Text.UTF8Encoding(false).GetBytes(lf)))
                                  .Replace("-", "").ToLowerInvariant();

            Assert.That(sha, Is.EqualTo(DropTurntableSha256Lf),
                "The registered deckIsoSolid has CHANGED since the nav-buoy kit was measured against " +
                "it. Both kits lathe against this one file, so a change here moves nav-buoy art too — " +
                "and this kit's byte-identical reference-sheet reproduction is no longer proven. " +
                "Re-run the four reference-sheet comparisons in docs/art/rigs/nav-buoy-kit/IMPORT.md, " +
                "then update this hash deliberately.");
        }

        // ---- handedness ------------------------------------------------------------------------------

        [Test]
        public void TheKitIsClockwiseOnTheGroundPlane()
        {
            Assert.That(_probe.Convention, Is.EqualTo(AzimuthConvention.Clockwise));
            Assert.That(_probe.GroundStepDeg, Is.EqualTo(NavBuoyKit.GroundStepDeg).Within(1e-6),
                "The +X ground-plane bearing must step −45°/dir. This is THE handedness test and the " +
                "bake applies a correction based on it.");
        }

        [Test]
        public void TheCatalogsDeclarationMatchesTheMeasurement()
        {
            var declared = RigCatalog.Get(Key).DeclaredConvention;
            Assert.That(declared, Is.EqualTo(NavBuoyKit.MeasuredConvention));
            Assert.DoesNotThrow(() => NavBuoyRegistrationProbe.MeasureAndAssert(_host, declared),
                "The bake refuses on a declaration/measurement mismatch; this asserts it currently agrees.");
        }

        /// <summary>
        /// ⚠️ The trap that made this lane mislabel five kits. The SCREEN step is −46.75°/dir here —
        /// and the iso-rig-pack records the SAME figure for rigs that turn the OTHER way. A test that
        /// read handedness off the screen angle would pass on both and discriminate neither.
        /// </summary>
        [Test]
        public void TheScreenAngleCannotDecideHandedness()
        {
            Assert.That(_probe.ScreenMeanStepDeg, Is.EqualTo(NavBuoyKit.ScreenMeanStepDeg).Within(1e-3));
            Assert.That(Math.Abs(_probe.ScreenMeanStepDeg), Is.Not.EqualTo(45.0).Within(0.5),
                "The screen mean is a foreshortened, alternating quantity — it is NOT ±45 and must " +
                "never be substituted for the ground-plane bearing.");
        }

        [Test]
        public void ClockwiseMeansTheBakeRendersDirZeroThroughSeven()
        {
            // DirForCell is the identity for Clockwise. Pinned because the baker routes through it,
            // and a silent change there re-orders every sheet in the kit.
            for (int cell = 0; cell < NavBuoyKit.Facings; cell++)
                Assert.That(RigBaker.DirForCell(cell, NavBuoyKit.Facings, AzimuthConvention.Clockwise),
                            Is.EqualTo((double)cell),
                            $"cell {cell} must render dir {cell} — not its counter-clockwise mirror.");
        }

        // ---- the pivot -------------------------------------------------------------------------------

        /// <summary>
        /// The pivot is model <c>z = 0</c>, the still waterline, and it must land on the SAME cell
        /// point at every facing — otherwise a mark would swim sideways as it turns, and the bob would
        /// be measured from a moving origin.
        /// </summary>
        [Test]
        public void ThePivotIsTheWaterlineAtEveryFacing()
        {
            _host.Execute($"globalThis.__nbC = {G}.cell('CardinalW','s20');");
            double cx = _host.EvaluateNumber("globalThis.__nbC.cx");
            double cy = _host.EvaluateNumber("globalThis.__nbC.cy");

            for (int dir = 0; dir < NavBuoyKit.Facings; dir++)
            {
                _host.Execute(
                    $"globalThis.__nbB = IsoSolid.camBasis({{dir:{dir},elev:IsoSolid.DEFAULT_ELEV}});" +
                    $"globalThis.__nbP = IsoSolid.proj(0,0,0,globalThis.__nbB,{cx},{cy});");

                Assert.That(_host.EvaluateNumber("globalThis.__nbP.sx"), Is.EqualTo(cx).Within(1e-9),
                            $"dir {dir}: z=0 must project to the pivot column.");
                Assert.That(_host.EvaluateNumber("globalThis.__nbP.sy"), Is.EqualTo(cy).Within(1e-9),
                            $"dir {dir}: z=0 must project to the pivot ROW — this is the waterline the " +
                            "bob rides, and a per-facing offset would make a mark bob about a moving point.");
            }
        }

        /// <summary>The cell rule seeds the union at the pivot so a cell always contains it. Measured:
        /// the seeding is INVISIBLE here because the waterline crosses painted hull on all 70 cells —
        /// which is exactly why it is asserted rather than assumed.</summary>
        [Test]
        public void EveryCellContainsItsOwnPivotInPaintedInk()
        {
            foreach (string type in NavBuoyKit.BakeTypes)
            {
                foreach (string size in NavBuoyKit.Sizes)
                {
                    _host.Execute($"globalThis.__nbC = {G}.cell('{type}','{size}');");
                    int w = (int)_host.EvaluateNumber("globalThis.__nbC.W");
                    int h = (int)_host.EvaluateNumber("globalThis.__nbC.H");
                    int px = (int)_host.EvaluateNumber("globalThis.__nbC.cx");
                    int py = (int)_host.EvaluateNumber("globalThis.__nbC.cy");

                    byte[] buf = NavBuoyKit.RenderRaw(_host, G, type, 0, size,
                                                      NavBuoyKit.BakeWear, NavBuoyKit.BakeLit);
                    Assert.That(NavBuoyKit.InkBox(buf, w, h, out int x0, out int y0, out int x1, out int y1),
                                Is.True, $"{type}|{size} rendered nothing at dir 0.");
                    Assert.That(px, Is.InRange(x0, x1), $"{type}|{size}: pivot x outside the ink.");
                    Assert.That(py, Is.InRange(y0, y1), $"{type}|{size}: pivot y outside the ink.");
                }
            }
        }

        // ---- shape facts a README got to have an opinion about ---------------------------------------

        [Test]
        public void TheRigCarriesFourteenTypesNotThirteen()
        {
            // The rig's own header comment says "13 TYPES" and is wrong. Same class of internal
            // inconsistency as the deck-loop kit's five-versus-six clips. The count is 14.
            Assert.That(_probe.TypeCount, Is.EqualTo(NavBuoyKit.AllTypes.Count));
            Assert.That(_probe.TypeCount, Is.EqualTo(14));
        }

        [Test]
        public void EveryTypeThisKitBakesExistsInTheRig()
        {
            string ids = _host.EvaluateString($"{G}.TYPE_IDS.join(',')");
            var live = ids.Split(',');
            foreach (string t in NavBuoyKit.BakeTypes)
                Assert.That(live, Does.Contain(t), $"BakeTypes names '{t}', which the rig does not have.");
            foreach (string t in NavBuoyKit.AllTypes)
                Assert.That(live, Does.Contain(t), $"AllTypes names '{t}', which the rig does not have.");
        }

        [Test]
        public void TheRigExposesNoFixedCellGlobals()
        {
            // Why the entry loads with InstallModule. Install reads W/H/pivot and throws on the pivot;
            // papering that over with defaults is the silent-wrong-geometry failure the split entry
            // points exist to prevent.
            foreach (string field in new[] { "W", "H", "pivot", "DIRS", "defaultElev" })
                Assert.That(_host.EvaluateBool($"typeof {G}.{field} === 'undefined'"), Is.True,
                            $"{G}.{field} now exists — the rig changed shape and the Install path " +
                            "should be reconsidered.");
        }

        // ---- ADR 0031: the ring -----------------------------------------------------------------------

        /// <summary>
        /// ⚠️ Records a real GAP, and asserts the behaviour the missing declaration would have gated.
        /// <c>IsoPackContract.AssertKeylineGated</c> probes <c>&lt;Global&gt;.KEYLINE_DEFAULT</c> and
        /// refuses a family without one — the shipyard kit landed in this state and could not bake
        /// until #477 added the export UPSTREAM. This rig has no such export either, and we cannot add
        /// one (his file runs unmodified, ADR 0021 §5). So the bake asserts the BEHAVIOUR instead, and
        /// this test pins both arms: "no ring pixels" would also pass on a renderer that had stopped
        /// drawing entirely.
        /// </summary>
        [Test]
        public void TheDefaultRenderIsRinglessAndTheRingIsStillReachable()
        {
            Assert.That(_probe.ExportsKeylineDefault, Is.False,
                "If the owner's next drop ADDS KEYLINE_DEFAULT, delete this assertion and switch the " +
                "bake to the standard AssertKeylineGated — the gap is worth noticing when it closes.");
            Assert.That(_probe.DefaultRenderIsRingless, Is.True,
                "ADR 0031: art authored from 2026-08 bakes ringless, and {keyline:true} must still " +
                "reach a live ring pass — otherwise this is a renderer that stopped drawing, not a gate.");
        }

        /// <summary>
        /// ⚠️ <c>{outline:true}</c> — the name #463/#477 and the rest of the repo use — is NOT wired to
        /// this rig's ring pass. It is silently ignored and returns the DEFAULT render, so an A/B driven
        /// with the repo-standard name comes back ringless and reads as a pass. Recorded as data; the
        /// fix belongs upstream in the art director's file.
        /// </summary>
        [Test]
        public void OutlineIsNotYetAnAliasOfKeylineOnThisKit()
        {
            Assert.That(_probe.OutlineIsLiveAlias, Is.False,
                "If this now passes, the drop wired {outline:true} through — update IMPORT.md's " +
                "upstream-request list and flip this assertion.");
        }
    }
}
