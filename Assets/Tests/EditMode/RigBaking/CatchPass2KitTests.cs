using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Holds catch pass 2 to what it shipped as, and to what its rigs actually DO.
    ///
    /// <para>Two halves, and both matter. The first is the intake pin: every rig and sidecar hashes
    /// to the number the drop declared, so a file edited in our tree — or a re-export dropped in
    /// without its paperwork — is caught here rather than at a bake. The second runs the rigs in the
    /// V8 host and asserts their behaviour, because a README is not evidence: three of the claims in
    /// this kit's own README are wrong, and the only way that was found was by rendering.</para>
    ///
    /// <para>⚠️ Hashes are compared LF-NORMALISED. <c>core.autocrlf</c> is on for this checkout, so a
    /// rig is stored LF in the blob and checked out CRLF — the same file has two hashes, and LF is
    /// the one git stores and the one every sidecar pins.</para>
    ///
    /// <para>⚠️ And they are compared as FULL 64-character digests. A prefix check is not a hash
    /// check: a stamping defect once shared its rig's first SIXTEEN hex digits and diverged over the
    /// remaining forty-eight.</para>
    /// </summary>
    public sealed class CatchPass2KitTests
    {
        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;
        const string Kit = "docs/art/rigs/catch-pass-2-kit";

        /// <summary>The one copy of the shared turntable. The kit shipped its own, byte-identical;
        /// it was deliberately NOT landed a second time.</summary>
        const string IsoSolid = "docs/art/rigs/deck-loop-kit/Art/isoSolid.js";

        static string Abs(string repoRelative) =>
            Path.Combine(RepoRoot, repoRelative.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>sha256 of a file with CRLF folded to LF — see the class remarks.</summary>
        static string LfSha256(string repoRelative)
        {
            string path = Abs(repoRelative);
            Assert.IsTrue(File.Exists(path), $"missing: {repoRelative}");
            byte[] raw = File.ReadAllBytes(path);

            var lf = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n') continue;
                lf.Add(raw[i]);
            }

            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(lf.ToArray()).Select(b => b.ToString("x2")));
        }

        // =========================================================================================
        // the intake pin
        // =========================================================================================

        /// <summary>The kit's own SHA256SUMS.txt, parsed to (repo-relative path -> digest).</summary>
        static Dictionary<string, string> DeclaredSums()
        {
            string text = File.ReadAllText(Abs(Kit + "/SHA256SUMS.txt"));
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(text, @"^(?<sha>[0-9a-f]{64})\s+(?<path>\S+)",
                                              RegexOptions.Multiline))
                map[m.Groups["path"].Value] = m.Groups["sha"].Value;
            return map;
        }

        [Test]
        public void EveryLandedRigAndSidecar_HashesToWhatTheDropDeclared()
        {
            var declared = DeclaredSums();
            Assert.GreaterOrEqual(declared.Count, 11,
                                  "SHA256SUMS.txt should cover 6 rigs and 5 sidecars");

            foreach (var kv in declared)
            {
                // isoSolid is the one entry that does NOT live in this kit — it is referenced from
                // the deck-loop kit, and that is exactly the thing the next test asserts.
                string repoPath = kv.Key == "Art/isoSolid.js" ? IsoSolid : Kit + "/" + kv.Key;
                Assert.AreEqual(kv.Value, LfSha256(repoPath),
                                $"{repoPath} does not hash to the digest the drop declared. If the " +
                                "art director re-exported, land the new drop with its new sums — do " +
                                "not re-stamp on our side (docs/art/rigs is his lane).");
            }
        }

        [Test]
        public void IsoSolidIsReferencedFromTheDeckLoopKit_NotLandedTwice()
        {
            Assert.IsFalse(File.Exists(Abs(Kit + "/Art/isoSolid.js")),
                           "the kit shipped its own isoSolid.js, byte-identical to the deck-loop " +
                           "kit's. Two copies of a shared turntable is two things to keep in step; " +
                           "the catalog declares deckIsoSolid as the prerequisite instead.");

            Assert.AreEqual(DeclaredSums()["Art/isoSolid.js"], LfSha256(IsoSolid),
                            "the copy we kept is no longer the copy the kit was derived against");
        }

        [Test]
        public void EverySidecar_PinsTheRigItNames()
        {
            foreach (string rig in new[] { "fishIsoRig2", "crustaceanRig2", "shellfishRig2",
                                           "clamHodRig", "catchKit2" })
            {
                string json = File.ReadAllText(Abs($"{Kit}/sidecars/{rig}.rig.json"));

                string pinned = Regex.Match(json, "\"derivedFromRigSha256\"\\s*:\\s*\"(?<v>[0-9a-f]{64})\"")
                                     .Groups["v"].Value;
                Assert.AreEqual(64, pinned.Length,
                                $"{rig}.rig.json carries no full derivedFromRigSha256. An ABSENT " +
                                "field is not a mismatch, it is a refusal.");
                Assert.AreEqual(LfSha256($"{Kit}/Art/{rig}.js"), pinned,
                                $"{rig}.rig.json pins a rig it no longer describes");

                string names = Regex.Match(json, "\"rig\"\\s*:\\s*\"(?<v>[^\"]+)\"").Groups["v"].Value;
                Assert.AreEqual($"Art/{rig}.js", names, $"{rig}.rig.json names a different rig file");
            }
        }

        // =========================================================================================
        // what the rigs DO — measured, not read
        // =========================================================================================

        static IRigScriptHost Chain()
        {
            var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("deckIsoSolid"));
            RigCatalog.Install(host, RigCatalog.Get("fish2"));
            RigCatalog.InstallModule(host, RigCatalog.Get("shellfish2"));
            RigCatalog.Install(host, RigCatalog.Get("crustacean2"));
            return host;
        }

        [Test]
        public void TheFishRig_DeclaresSevenSpecies_AndAThirtyFiveColumnSheet()
        {
            using var host = Chain();
            Assert.AreEqual(7, host.EvaluateNumber("FishIso2.ORDER.length"), "seven species");
            CollectionAssert.AreEqual(
                new[] { "cod", "haddock", "pollock", "mackerel", "bass", "flounder", "herring" },
                FishingKitBaker.ReadStringArray(host, "FishIso2.ORDER"),
                "the species, in the rig's own order");

            CollectionAssert.AreEqual(new[] { "swim", "dart", "thrash", "shadow", "roll", "jump" },
                                      FishingKitBaker.ReadStringArray(host, "FishIso2.AORDER"));
            CollectionAssert.AreEqual(new[] { "deck", "gill", "tail", "cradle" },
                                      FishingKitBaker.ReadStringArray(host, "FishIso2.RESTS"));

            // 25 water frames + 10 rest frames. sheetOrder() is the column map a headless bake uses,
            // so a change here silently re-shapes every sheet.
            Assert.AreEqual(35, host.EvaluateNumber("FishIso2.sheetOrder().length"),
                            "sheetOrder() is the 35-column sheet layout");

            int fromTables = 0;
            foreach (string a in FishingKitBaker.ReadStringArray(host, "FishIso2.AORDER"))
                fromTables += (int)host.EvaluateNumber($"FishIso2.ANIMS['{a}'].n");
            foreach (string r in FishingKitBaker.ReadStringArray(host, "FishIso2.RESTS"))
                fromTables += (int)host.EvaluateNumber($"FishIso2.RPOSE['{r}'].length");
            Assert.AreEqual(35, fromTables,
                            "sheetOrder() must be exactly the ANIMS + RPOSE frames, not a second " +
                            "opinion about them");
        }

        [Test]
        public void TheFishCellAndPivot_AreThePassOneCellAndPivot()
        {
            // This is what lets the pass-2 sheets ride FishingSheetSlicer's existing "Fish_" spec
            // with no slicer change at all.
            using var host = Chain();
            Assert.AreEqual(64, host.EvaluateNumber("FishIso2.W"));
            Assert.AreEqual(64, host.EvaluateNumber("FishIso2.H"));
            Assert.AreEqual(32, host.EvaluateNumber("FishIso2.pivot.x"));
            Assert.AreEqual(38, host.EvaluateNumber("FishIso2.pivot.y"));
        }

        [Test]
        public void AnUnknownSpecies_WouldSilentlyRenderAMackerel_SoTheBakerAsserts()
        {
            // The rig resolves SPECIES[key] || SPECIES.mackerel. This is not a complaint about the
            // rig — it is why CatchPass2Baker.AssertSpecies exists, and this proves the hazard is
            // real rather than imagined.
            using var host = Chain();
            Assert.AreEqual(host.EvaluateNumber("FishIso2.hold('mackerel',1).mass"),
                            host.EvaluateNumber("FishIso2.hold('not_a_fish',1).mass"),
                            "an unknown species is quietly weighed as a mackerel");

            Assert.Throws<ArgumentException>(
                () => CatchPass2Baker.AssertSpecies(host, "FishIso2", "not_a_fish"),
                "the baker must refuse what the rig would happily draw");
        }

        [Test]
        public void TheMassLawIsCubic_SoTheLadderCanBeInvertedFromIt()
        {
            using var host = Chain();
            double k = CatchPass2Baker.MassAtUnitScale(host, "FishIso2", "cod");
            Assert.Greater(k, 0);

            foreach (double s in new[] { 0.6, 0.9, 1.0, 1.3, 1.6 })
            {
                double fromRig = host.EvaluateNumber($"FishIso2.hold('cod',{S(s)}).mass");
                Assert.AreEqual(k * s * s * s, fromRig, 0.005 + k * s * s * s * 1e-6,
                                $"mass at scale {s} is not k*s^3 — the ladder's inverse would be wrong");
            }

            // And the inverse round-trips: ask for a weight, get a scale that weighs it.
            double scale = CatchPass2Baker.ScaleForKg(host, "FishIso2", "cod", 12.0);
            Assert.AreEqual(12.0, host.EvaluateNumber($"FishIso2.hold('cod',{S(scale)}).mass"), 0.01,
                            "ScaleForKg does not invert the rig's own law");
        }

        [Test]
        public void TheCrustaceanRig_TakesTheKindFirst_AndRemapsEachKindsInvalidPoses()
        {
            using var host = Chain();
            CollectionAssert.AreEqual(new[] { "lobster", "crab" },
                                      FishingKitBaker.ReadStringArray(host, "Crustacean2.KINDS"));

            // ⚠️ render(KIND, opts) — the kind is the FIRST argument and the camera is opts.dir.
            // Handing it a dir first is not an error: KINDS.indexOf fails and it draws a lobster.
            Assert.AreEqual(0, DiffPixels(host,
                    "Crustacean2.render('haddock',{dir:2,pose:'walk',frame:0})",
                    "Crustacean2.render('lobster',{dir:2,pose:'walk',frame:0})"),
                "an unknown kind renders a LOBSTER, silently — which is why a baker must assert");

            // The remaps, measured by rendering rather than read from the README.
            Assert.AreEqual(0, DiffPixels(host,
                    "Crustacean2.render('lobster',{dir:3,pose:'sidle',frame:1})",
                    "Crustacean2.render('lobster',{dir:3,pose:'walk',frame:1})"),
                "a lobster cannot sidle — it renders as walk");
            Assert.AreEqual(0, DiffPixels(host,
                    "Crustacean2.render('lobster',{dir:3,pose:'burrow',frame:1})",
                    "Crustacean2.render('lobster',{dir:3,pose:'walk',frame:1})"),
                "a lobster cannot burrow — it renders as walk");
            Assert.AreEqual(0, DiffPixels(host,
                    "Crustacean2.render('crab',{dir:3,pose:'flip',frame:1})",
                    "Crustacean2.render('crab',{dir:3,pose:'walk',frame:1})"),
                "a crab cannot tail-flip — it renders as walk");

            // And the poses each kind DOES own are genuinely distinct.
            Assert.Greater(DiffPixels(host,
                    "Crustacean2.render('lobster',{dir:3,pose:'flip',frame:1})",
                    "Crustacean2.render('lobster',{dir:3,pose:'walk',frame:1})"), 0,
                "the lobster's tail-flip is its own pose");
            Assert.Greater(DiffPixels(host,
                    "Crustacean2.render('crab',{dir:3,pose:'sidle',frame:1})",
                    "Crustacean2.render('crab',{dir:3,pose:'walk',frame:1})"), 0,
                "the crab's sidle is its own pose");
        }

        [Test]
        public void TurningTheAnimal_ComposesExactlyWithMovingTheCamera()
        {
            // ang turns the animal on the spot, dir is the camera, and one dir step is 45°. So a
            // quarter-turn of the animal at dir 0 must be the same pixels as the camera at dir 1.
            using var host = Chain();
            Assert.AreEqual(0, DiffPixels(host,
                    "Crustacean2.render('lobster',{dir:0,pose:'walk',frame:0,ang:Math.PI/4})",
                    "Crustacean2.render('lobster',{dir:1,pose:'walk',frame:0,ang:0})"),
                "ang and dir do not compose — one of them is not measured in what it claims");
        }

        [Test]
        public void TheShellfishSpurt_MatchesItsOwnSidecar()
        {
            // The same contract ClamSpurtMathTests holds the GAME to, checked here at the source so
            // a rig change is caught in the kit rather than only in the gameplay transcription.
            using var host = Chain();
            host.Execute("var HS = Shellfish2.holes(3, 400, 100, 100);");

            Assert.AreEqual(400, host.EvaluateNumber("HS.length"));
            Assert.AreEqual(420, host.EvaluateNumber("Math.min.apply(null,HS.map(function(h){return h.dur;}))"),
                            "dur is a flat 420 ms on every hole");
            Assert.AreEqual(420, host.EvaluateNumber("Math.max.apply(null,HS.map(function(h){return h.dur;}))"));

            double lo = host.EvaluateNumber("Math.min.apply(null,HS.map(function(h){return h.period;}))");
            double hi = host.EvaluateNumber("Math.max.apply(null,HS.map(function(h){return h.period;}))");
            Assert.GreaterOrEqual(lo, HiddenHarbours.Fishing.ClamSpurtMath.MinPeriodMilliseconds);
            Assert.Less(hi, HiddenHarbours.Fishing.ClamSpurtMath.MaxPeriodMilliseconds);

            // ⚠️ The window opens at t = period - (phase % period), NOT at t = phase.
            host.Execute("var H0 = HS[0]; var T0 = H0.period - (H0.phase % H0.period);");
            Assert.AreEqual(0.0, host.EvaluateNumber("Shellfish2.spurt(H0, T0).rise"), 1e-9,
                            "the window opens at the lip");
            Assert.AreEqual(1.0, host.EvaluateNumber("Shellfish2.spurt(H0, T0 + H0.dur/2).rise"), 1e-9,
                            "the crest is half way through");
            Assert.IsTrue(host.EvaluateBool("Shellfish2.spurt(H0, T0 + H0.dur + 1) === null"),
                          "one millisecond past the window is quiet");
        }

        [Test]
        public void TheGameTranscriptionOfTheSpurt_AgreesWithTheRigItself()
        {
            // The transcription law: two transcriptions never agree bit for bit, so this asserts
            // agreement to a tolerance across the whole window rather than equality.
            using var host = Chain();
            host.Execute("var H = Shellfish2.holes(11, 1, 10, 10)[0]; " +
                         "var T = H.period - (H.phase % H.period);");
            double dur = host.EvaluateNumber("H.dur");
            float period = (float)host.EvaluateNumber("H.period");

            for (int step = 0; step <= 20; step++)
            {
                double offset = dur * step / 20.0;
                double fromRig = host.EvaluateNumber($"Shellfish2.spurt(H, T + {S(offset)}).rise");

                bool on = HiddenHarbours.Fishing.ClamSpurtMath.TrySpurt(
                    offset, phaseMs: 0f, periodMs: period, out float ours, out _);
                Assert.IsTrue(on, $"our transcription went quiet at +{offset:F0} ms");
                Assert.AreEqual(fromRig, ours, 1e-5,
                                $"rise disagrees with the rig at +{offset:F0} ms into the window");
            }
        }

        [Test]
        public void TheCatchKit_CarriesFourteenKindsPlusMixed()
        {
            using var host = Chain();
            RigCatalog.Install(host, RigCatalog.Get("clamHod"));
            host.Execute(CatchStorageBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get("catchKit2"));

            Assert.AreEqual(15, host.EvaluateNumber("CatchKit2.CATCHES.length"), "14 kinds plus 'mixed'");
            Assert.AreEqual(7, host.EvaluateNumber("CatchKit2.FISH.length"));
            Assert.AreEqual(2, host.EvaluateNumber("CatchKit2.CRUST.length"));
            Assert.AreEqual(5, host.EvaluateNumber("CatchKit2.SHELL.length"));
            Assert.AreEqual(14, host.EvaluateNumber(
                "CatchKit2.FISH.length + CatchKit2.CRUST.length + CatchKit2.SHELL.length"));

            // The five shellfish fill as heaps, not as placed items.
            foreach (string k in new[] { "mussel", "clam", "scallop", "oyster", "periwinkle" })
                Assert.IsTrue(host.EvaluateBool($"CatchKit2.isHeap('{k}')"), $"{k} fills as a heap");
            foreach (string k in new[] { "cod", "lobster", "crab" })
                Assert.IsFalse(host.EvaluateBool($"CatchKit2.isHeap('{k}')"), $"{k} is placed, not heaped");
        }

        [Test]
        public void FillItemsIsMonotonicOnlyWhereDenseIsOne_WhichTheReadmeDoesNotSay()
        {
            // The kit README says fillItems is MONOTONIC — "growing a fill never moves earlier
            // items". It is, for ten of the fourteen kinds. For the four DENSE ones it is not: the
            // jitter gate is `i >= n/dense`, n grows with the fill, so the threshold moves across
            // items already placed and the two extra rng draws shift everything after them.
            //
            // This test PINS THE DEFECT rather than papering over it. When the rig is fixed upstream
            // it goes red, and that is the signal to delete it.
            using var host = Chain();
            RigCatalog.Install(host, RigCatalog.Get("clamHod"));
            host.Execute(CatchStorageBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get("catchKit2"));

            host.Execute(@"
                function prefixViolations(kind){
                  var fills=['empty','few','half','full','brim'], v=0;
                  for (var seed=1; seed<=8; seed++){
                    var prev=null;
                    for (var f=0; f<fills.length; f++){
                      var got=CatchKit2.fillItems(kind, fills[f], seed, 24);
                      if(prev) for(var i=0;i<prev.length;i++){
                        if(!got[i] || JSON.stringify(prev[i])!==JSON.stringify(got[i])) v++;
                      }
                      prev=got;
                    }
                  }
                  return v;
                }");

            foreach (string k in new[] { "cod", "haddock", "pollock", "bass", "lobster", "mixed" })
            {
                Assert.AreEqual(1, host.EvaluateNumber($"CatchKit2.DENSE['{k}']||1"),
                                $"{k} is expected to be a DENSE==1 kind");
                Assert.AreEqual(0, host.EvaluateNumber($"prefixViolations('{k}')"),
                                $"{k} must be monotonic — it has no jitter gate to move");
            }

            foreach (string k in new[] { "herring", "mackerel", "flounder", "crab" })
            {
                Assert.Greater(host.EvaluateNumber($"CatchKit2.DENSE['{k}']||1"), 1,
                               $"{k} is expected to be a DENSE kind");
                Assert.Greater(host.EvaluateNumber($"prefixViolations('{k}')"), 0,
                               $"{k} is NOT monotonic today. If this went green the rig was fixed " +
                               "upstream — delete this half of the test and tell the arc.");
            }
        }

        [Test]
        public void TheHodsRollerGrip_IsTheSamePointAtEveryFacing_AndThatIsCorrect()
        {
            // cpivot projects (0, 0, GZ) — dead centre in x and y — so rotating the camera about the
            // vertical axis cannot move it. Pinned because "the same at all 8 dirs" reads like a
            // stub, and the next reader should not go looking for a bug that is not there.
            using var host = Chain();
            RigCatalog.Install(host, RigCatalog.Get("clamHod"));

            double x0 = host.EvaluateNumber("ClamHod.cpivot(0).x");
            double y0 = host.EvaluateNumber("ClamHod.cpivot(0).y");
            for (int d = 1; d < 8; d++)
            {
                Assert.AreEqual(x0, host.EvaluateNumber($"ClamHod.cpivot({d}).x"), 0, $"dir {d} x");
                Assert.AreEqual(y0, host.EvaluateNumber($"ClamHod.cpivot({d}).y"), 0, $"dir {d} y");
            }

            Assert.AreEqual(4, host.EvaluateNumber("ClamHod.depthPx()"), "rim-to-floor is 4 px");
            Assert.AreEqual(40, host.EvaluateNumber("ClamHod.W"));
            Assert.AreEqual(40, host.EvaluateNumber("ClamHod.H"));
        }

        // =========================================================================================
        // the hod's HEAP — what the bake composes between the two wire layers
        // =========================================================================================

        /// <summary>The heap's own chain: everything <c>CatchKit2</c> composes from, then the canvas
        /// shim, then the glue, then the hod. Exactly what <c>CatchPass2StorageBaker.BakeClamHod</c>
        /// installs — a test that built a different chain would be measuring a different bake.</summary>
        static IRigScriptHost HeapChain()
        {
            var host = Chain();
            host.Execute(CatchStorageBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get("catchKit2"));
            RigCatalog.Install(host, RigCatalog.Get("clamHod"));
            return host;
        }

        [Test]
        public void TheHodBakesTheFourBandsThatHeapSomething_AndNotEmpty()
        {
            using var host = HeapChain();

            CollectionAssert.AreEqual(new[] { "empty", "few", "half", "full", "brim" },
                                      FishingKitBaker.ReadStringArray(host, "ClamHod.FILLS"),
                                      "the rig's own band ladder");

            CollectionAssert.AreEqual(new[] { "few", "half", "full", "brim" },
                                      CatchPass2StorageBaker.HeapBands(host, "ClamHod"),
                                      "the bands with a non-zero fraction — 'empty' heaps nothing, so " +
                                      "an empty hod is the back+front pair with nothing between them");

            // ⚠️ heap() answers NULL for a zero fraction and — silently — when Shellfish2 is absent.
            // Both would bake four fully transparent sheets that pass every geometry guard.
            Assert.IsTrue(host.EvaluateBool(
                "CatchKit2.heap('clam', ClamHod.opening(0), ClamHod.depthPx(), 'empty') === null"),
                "'empty' must heap nothing");
            foreach (string band in new[] { "few", "half", "full", "brim" })
                Assert.IsFalse(host.EvaluateBool(
                    $"CatchKit2.heap('clam', ClamHod.opening(0), ClamHod.depthPx(), '{band}') === null"),
                    $"'{band}' must heap something");
        }

        [Test]
        public void EveryHeapLandsInsideTheHodsCell_AtThePivotTheRigReports()
        {
            // The bake places the heap at pivot + (x, y) and REFUSES rather than cropping. If the rim
            // quad ever outgrows the cell this is where it says so, before four sheets are written.
            using var host = HeapChain();
            int w = (int)host.EvaluateNumber("ClamHod.W"), h = (int)host.EvaluateNumber("ClamHod.H");
            int px = (int)host.EvaluateNumber("ClamHod.pivot.x"), py = (int)host.EvaluateNumber("ClamHod.pivot.y");

            foreach (string band in new[] { "few", "half", "full", "brim" })
            for (int d = 0; d < 8; d++)
            {
                host.Execute($"globalThis.__h = CatchKit2.heap('clam', ClamHod.opening({d}), " +
                             $"ClamHod.depthPx(), '{band}');");
                int x0 = px + (int)host.EvaluateNumber("__h.x"), y0 = py + (int)host.EvaluateNumber("__h.y");
                int hw = (int)host.EvaluateNumber("__h.w"), hh = (int)host.EvaluateNumber("__h.h");

                Assert.GreaterOrEqual(x0, 0, $"{band} d{d}: heap starts left of the cell");
                Assert.GreaterOrEqual(y0, 0, $"{band} d{d}: heap starts above the cell");
                Assert.LessOrEqual(x0 + hw, w, $"{band} d{d}: heap runs past the right of the cell");
                Assert.LessOrEqual(y0 + hh, h, $"{band} d{d}: heap runs past the bottom of the cell");

                Assert.AreEqual(hw * hh * 4, host.EvaluateBytes("__h.canvas.__img.data").Length,
                                $"{band} d{d}: the shim's mailbox does not hold a {hw}×{hh} RGBA surface");
            }
        }

        [Test]
        public void TheBandLadderRises_AndOnlyBrimCrownsAboveTheRim()
        {
            // What the ladder IS, measured: the same heap surface LOWERED into the basket by
            // (1 − min(1, frac/0.85)) × depthPx — 3 px at few, 1 at half, 0 at full — and then brim
            // alone crowns 2 px proud of the rim. So few→full is one plate translated three pixels,
            // and brim is the only band that changes shape. Pinned because it is easy to look at the
            // four sheets, see near-identical pixel counts, and conclude the bake is broken.
            using var host = HeapChain();
            string[] bands = { "few", "half", "full", "brim" };

            for (int d = 0; d < 8; d++)
            {
                var top = new int[bands.Length];
                var area = new int[bands.Length];
                for (int b = 0; b < bands.Length; b++)
                {
                    host.Execute($"globalThis.__h = CatchKit2.heap('clam', ClamHod.opening({d}), " +
                                 $"ClamHod.depthPx(), '{bands[b]}');");
                    top[b] = (int)host.EvaluateNumber("__h.y");
                    byte[] rgba = host.EvaluateBytes("__h.canvas.__img.data");
                    int n = 0;
                    for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] != 0) n++;
                    area[b] = n;
                }

                for (int b = 1; b < bands.Length; b++)
                    Assert.Less(top[b], top[b - 1],
                                $"d{d}: '{bands[b]}' must sit higher in the basket than '{bands[b - 1]}' " +
                                $"(tops {string.Join(",", top)})");

                Assert.AreEqual(area[0], area[1], $"d{d}: few and half are the same plate, lowered");
                Assert.AreEqual(area[0], area[2], $"d{d}: few and full are the same plate, lowered");
                Assert.Greater(area[3], area[2], $"d{d}: brim crowns, so it is the one band that grows");
            }
        }

        [Test]
        public void LoadingTheHeapsChain_DoesNotMoveOneHodPixel()
        {
            // BakeClamHod now loads fish2/shellfish2/crustacean2/the shim/catchKit2 BEFORE the hod,
            // where it used to load the hod alone. Nothing in that chain should reach the hod's own
            // render — but "should" is not evidence, and the two shipped sheets are the thing a
            // re-bake must not move.
            using var bare = Chain();
            RigCatalog.Install(bare, RigCatalog.Get("clamHod"));
            using var full = HeapChain();

            foreach (string layer in new[] { "back", "front" })
            for (int d = 0; d < 8; d++)
            {
                byte[] a = bare.EvaluateBytes($"ClamHod.render({d},{{layer:'{layer}'}})");
                byte[] b = full.EvaluateBytes($"ClamHod.render({d},{{layer:'{layer}'}})");
                Assert.AreEqual(a.Length, b.Length, $"{layer} d{d}: cell size moved");
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i])
                        Assert.Fail($"Hod2_{layer} d{d} differs at byte {i} once the heap's chain is " +
                                    "loaded — the two shipped sheets would move on a re-bake.");
            }
        }

        // ---- helpers ---------------------------------------------------------------------------

        /// <summary>Pixels that differ between two render expressions.</summary>
        static int DiffPixels(IRigScriptHost host, string exprA, string exprB)
        {
            byte[] a = host.EvaluateBytes(exprA);
            byte[] b = host.EvaluateBytes(exprB);
            Assert.AreEqual(a.Length, b.Length, "two renders of one rig came back different sizes");

            int n = 0;
            for (int i = 0; i < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3])
                    n++;
            return n;
        }

        static string S(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
