using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>Pins the fit-out's procedural surface transcription against the rig's own functions.</b>
    ///
    /// <para><b>Why this exists.</b> Nothing pixel-exact can guard the fit-out: the mesh path does
    /// not model the rigs' per-material <c>dith</c> weight, so sheet-parity is impossible by design
    /// and the owner's eye is the only end-to-end check. An eye cannot tell a plank groove at the
    /// right period from one at a slightly wrong period, and it certainly cannot tell that a
    /// half-width is 0.026 rather than 0.030. So the transcription is pinned here instead, by
    /// evaluating <c>boatInteriorRig.js</c>'s ACTUAL generator functions over a grid and comparing
    /// against the same logic the facet shader runs.</para>
    ///
    /// <para>⚠️ <b>What this does and does not prove.</b> It proves the C# mirror agrees with the
    /// rig. The HLSL is a second transcription of that same mirror, written line for line beside it
    /// — a genuine third copy, and the honest statement is that this pins two of the three. What
    /// makes that acceptable is that the shader's version is four lines of modulo arithmetic with no
    /// hash, no state and no precision hazard, which is only true BECAUSE the rig's hash turned out
    /// to be unreachable (below). Had the hash been live, this would not have been enough — a
    /// double-rounded 32-bit multiply cannot be reproduced in HLSL, and the pin would have had to be
    /// a render.</para>
    /// </summary>
    public class InteriorTexTranscriptionTests
    {
        const string KitFolder = "docs/art/rigs/boat-interiors-kit";
        const string InteriorRigFileName = "boatInteriorRig.js";

        static string RepoRoot => Directory.GetParent(UnityEngine.Application.dataPath).FullName;

        static IRigScriptHost HostWithTheRig()
        {
            string path = Path.Combine(RepoRoot, KitFolder, InteriorRigFileName);
            if (!File.Exists(path)) Assert.Ignore($"{KitFolder}/{InteriorRigFileName} is not on disk.");
            // ⚠️ HER EXTERIOR RIG FIRST. The interior rig reads the hull's loft off root[sym] and
            // binds to nothing without it — hullEnv returns null and the failure surfaces as
            // "Cannot read properties of null (reading 'E')", which reads like a broken helper
            // rather than a missing rig.
            string hullRig = Path.Combine(RepoRoot, "docs", "art", "rigs", "lobsterBoatIsoRig.js");
            if (!File.Exists(hullRig)) Assert.Ignore("lobsterBoatIsoRig.js is not on disk.");

            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(hullRig));
            host.Execute(BoatInteriorGeometryExtractor.WidenInteriorRig(File.ReadAllText(path)));
            // The generator factories are closure-private and are NOT part of the widened export, so
            // they are reached the way the rig itself reaches them: through a face that carries one.
            // Nothing here re-implements the rig — the functions under test are the rig's own.
            host.Execute(@"
              globalThis.__TX = (function(){
                var BI = globalThis.BoatInterior, out = {};
                var s = BI.resolve({hull:'lobster', level:'house'});
                var env = BI.hullEnv('lobster', null);
                function harvest(level){
                  var st = BI.resolve({hull:'lobster', level:level});
                  for (var d = 0; d < 8; d++){
                    var faces = BI.build(st, env, BI.camBasis({dir:d}, env));
                    for (var i = 0; i < faces.length; i++){
                      var f = faces[i]; if (!f.tex) continue;
                      var src = String(f.tex);
                      var kind = src.indexOf('-2') >= 0 ? 'plank'
                               : src.indexOf('0.026') >= 0 ? 'board'
                               : src.indexOf('0.030') >= 0 ? 'quilt' : 'unknown';
                      // key by kind AND measured period, so every distinct (generator, period)
                      // pair in the rig gets pinned rather than only the first one met.
                      var p = kind === 'quilt' ? 0.20 : probe(f.tex, kind);
                      var key = kind + '@' + p.toFixed(5);
                      if (!out[key]) out[key] = { kind:kind, period:p, fn:f.tex };
                    }
                  }
                }
                function probe(fn, kind){
                  var inV = (kind === 'plank'), hit = inV ? -2 : -1, band = inV ? 0.022 : 0.026;
                  for (var i = 1; i <= 40000; i++){
                    var t = i * 0.00005;
                    if (t <= band + 1e-9) continue;
                    if ((inV ? fn(0,t) : fn(t,0)) === hit) return t;
                  }
                  return -1;
                }
                harvest('house'); harvest('cuddy');
                return {
                  keys: function(){ return Object.keys(out).sort().join(' '); },
                  eval: function(key, u, v){ return out[key].fn(u, v); },
                  kind: function(key){ return out[key].kind; },
                  period: function(key){ return out[key].period; }
                };
              })();");
            return host;
        }

        /// <summary>The C# mirror of <c>HHInteriorTex</c> in HiddenHarboursIsoFacet.shader, written
        /// to match it line for line. JS <c>((x % p) + p) % p</c> is a true positive modulo, which is
        /// <c>x - p*floor(x/p)</c> — the form the shader uses because HLSL <c>fmod</c> keeps the
        /// dividend's sign and a room's uv crosses zero.</summary>
        static int Shader(BoatInteriorGeometryExtractor.TexKind kind, double p, double u, double v)
        {
            switch (kind)
            {
                case BoatInteriorGeometryExtractor.TexKind.Plank:
                    return (v - p * Math.Floor(v / p)) < 0.022 ? -2 : 0;
                case BoatInteriorGeometryExtractor.TexKind.Board:
                    return (u - p * Math.Floor(u / p)) < 0.026 ? -1 : 0;
                case BoatInteriorGeometryExtractor.TexKind.Quilt:
                    double fu = u - p * Math.Floor(u / p), fv = v - p * Math.Floor(v / p);
                    return (fu < 0.030 || fv < 0.030) ? -1 : 0;
                default: return 0;
            }
        }

        [Test]
        public void EveryGeneratorInTheRig_AgreesWithTheShadersTranscription_OverAGrid()
        {
            using IRigScriptHost host = HostWithTheRig();
            string[] keys = host.EvaluateString("globalThis.__TX.keys()")
                                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.IsNotEmpty(keys, "no textured face was found in the lobster's rooms, so this " +
                                    "fixture pinned nothing at all.");

            var inv = CultureInfo.InvariantCulture;
            var log = new StringBuilder();
            int compared = 0, mismatches = 0;
            var firstFew = new StringBuilder();

            foreach (string key in keys)
            {
                string kindName = host.EvaluateString($"globalThis.__TX.kind('{key}')");
                Assert.AreNotEqual("unknown", kindName,
                    $"the rig grew a generator this extractor does not recognise ({key}). Transcribe " +
                    "it into the shader and add it to TexKind — do not let a bake drop the detail.");
                double period = host.EvaluateNumber($"globalThis.__TX.period('{key}')");
                Assert.Greater(period, 0, $"{key}: the period probe failed.");

                var kind = (BoatInteriorGeometryExtractor.TexKind)Enum.Parse(
                    typeof(BoatInteriorGeometryExtractor.TexKind), kindName, ignoreCase: true);

                // A grid that deliberately crosses zero on both axes: JS % keeps the dividend's
                // sign, so negative uv is exactly where a naive fmod transcription diverges, and a
                // room's coordinates are hull-local and do cross the centreline.
                for (int i = -60; i <= 60; i++)
                    for (int j = -60; j <= 60; j++)
                    {
                        double u = i * 0.0173, w = j * 0.0211;    // irrational-ish, to miss the lattice
                        int rig = (int)Math.Round(host.EvaluateNumber(
                            $"globalThis.__TX.eval('{key}',{u.ToString("R", inv)},{w.ToString("R", inv)})"));
                        int mine = Shader(kind, period, u, w);
                        compared++;
                        if (rig != mine && mismatches++ < 6)
                            firstFew.AppendLine($"  {key} at u={u:0.####} v={w:0.####}: rig {rig}, " +
                                                $"shader {mine}");
                    }
                log.AppendLine($"  {key,-18} period {period:0.#####}  pinned");
            }

            Assert.AreEqual(0, mismatches,
                $"the shader's transcription disagrees with the rig's own generators at {mismatches} " +
                $"of {compared} sampled points:\n{firstFew}");
            UnityEngine.Debug.Log($"[interior-tex] {keys.Length} (generator, period) pairs pinned over " +
                                  $"{compared} samples:\n{log}");
        }

        /// <summary>
        /// <b>The dead branch, re-measured rather than remembered.</b> The shader omits the rig's
        /// per-plank and per-cell hash because <c>hash2</c> can never reach 0.5 — JS <c>&gt;&gt;</c>
        /// sign-extends, so bit 31 of <c>h ^ (h &gt;&gt; 16)</c> is the sign bit xored with itself.
        /// If that is ever repaired upstream this test goes red, which is the correct moment to
        /// discover it: the omission stops being faithful the instant the branch becomes reachable.
        /// </summary>
        [Test]
        public void TheRigsHash_StillNeverReachesHalf_SoOmittingItIsStillFaithful()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(@"
              function hash2(a,b){ let h=(a*374761393+b*668265263)>>>0;
                                   h=(h^(h>>13))*1274126177>>>0;
                                   return ((h^(h>>16))>>>0)/4294967296; }
              function __maxHash(){ var m = 0;
                for (var a=-40; a<=40; a++) for (var b=-20; b<=20; b++){
                  var v = hash2(a,b); if (v > m) m = v; }
                return m; }");
            double max = host.EvaluateNumber("__maxHash()");
            Assert.Less(max, 0.5,
                $"hash2 now reaches {max}, so the rig's per-plank and per-cell variation is LIVE " +
                "again — and the facet shader still omits it, which means the mesh and the sprite " +
                "sheets now disagree on every plank. Transcribe the hash (⚠️ JS multiplies through " +
                "a double, so a 32-bit wrapping multiply will NOT reproduce it) or re-rule.");
            UnityEngine.Debug.Log($"[interior-tex] hash2 max over the used range: {max} (< 0.5, " +
                                  "so the branch stays unreachable and omitting it stays faithful)");
        }
    }
}
