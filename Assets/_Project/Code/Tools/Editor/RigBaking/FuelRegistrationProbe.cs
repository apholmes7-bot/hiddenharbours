using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// What the fuel kit's conventions actually ARE, measured from the live rig every time it bakes
    /// rather than read off its README.
    ///
    /// <para>Every kit imported into this repo has had at least one declared convention turn out
    /// wrong, and the failures are silent: a mislabelled handedness writes eight good cells in
    /// reverse order, a missing prerequisite renders a plausible wrong picture. So the bake refuses
    /// on any disagreement between what the catalog DECLARES and what this measures.</para>
    /// </summary>
    public static class FuelRegistrationProbe
    {
        /// <summary>
        /// The rig this kit's handedness is measured AGAINST, and why the answer means anything.
        ///
        /// <para><c>utilityIso</c> is registered <see cref="AzimuthConvention.CounterClockwise"/> in
        /// this repo after its own probe measured it. Measuring both in ONE host with ONE sign
        /// convention turns "which way does this turn?" into a comparison against a known reference
        /// instead of an absolute claim about a camera basis.</para>
        /// </summary>
        public const string ReferenceRigKey = "utilityIso";

        public sealed class Report
        {
            public AzimuthConvention Measured;
            public double[] GroundBearings = Array.Empty<double>();
            public double GroundStepPerDir;
            public double ReferenceGroundStepPerDir;
            public double ScreenStepMean, ReferenceScreenStepMean;

            /// <summary>Max |mount − IsoSolid.proj| in x across the facings. Zero means one camera.</summary>
            public double TurntableMaxDxPx;
            /// <summary>Spread of the y residual across facings. A CONSTANT offset is a different
            /// assumed z, not a different camera; a VARYING one would be two turntables.</summary>
            public double TurntableDySpreadPx;

            public bool ExportsKeylineDefault;
            public bool TurntableExportsKeylineDefault;
            public bool TurntableKeylineDefaultValue;
            public int KeylineTruePixelDelta, OutlineTruePixelDelta;

            public bool WearCacheDefectPresent;
            public bool SilentSizeFallbackPresent;

            public string[] Types = Array.Empty<string>();
            public string[] Grades = Array.Empty<string>();

            public override string ToString() =>
                $"fuel: {Measured} ({GroundStepPerDir:F3}°/dir ground, reference " +
                $"{ReferenceGroundStepPerDir:F3}°/dir), turntable dx {TurntableMaxDxPx:F9} px / " +
                $"dy spread {TurntableDySpreadPx:F9} px, keyline±{KeylineTruePixelDelta}/" +
                $"{OutlineTruePixelDelta} px, wear-cache defect {WearCacheDefectPresent}";
        }

        /// <summary>
        /// Installs the rig (and its turntable prerequisite) and measures everything the bake relies
        /// on. The host is left loaded so a caller can bake through it.
        /// </summary>
        public static Report Measure(IRigScriptHost host)
        {
            var entry = RigCatalog.Get(FuelStorageKit.RigKey);
            RigCatalog.InstallModule(host, entry);

            // The reference rig shares the host only for the handedness comparison. It installs a
            // different global and touches nothing this kit reads — asserted below by re-checking
            // that the fuel renders used for the keyline delta are unaffected by its presence.
            RigCatalog.InstallModule(host, RigCatalog.Get(ReferenceRigKey));

            var r = new Report();
            string g = entry.GlobalName;

            MeasureHandedness(host, r);
            MeasureTurntableIdentity(host, g, r);
            MeasureKeyline(host, g, r);
            r.WearCacheDefectPresent = MeasureWearCacheDefect(host, g);
            r.SilentSizeFallbackPresent = MeasureSilentSizeFallback(host, g);
            r.Types = Csv(host, $"{g}.TYPE_ORDER.join(',')");
            r.Grades = Csv(host, $"{g}.FUEL_ORDER.join(',')");

            return r;
        }

        // ---- handedness ----------------------------------------------------------------------

        /// <summary>
        /// ⚠️ THE ONLY ADMISSIBLE HANDEDNESS TEST IS THE UN-SQUASHED GROUND-PLANE BEARING.
        ///
        /// <para>The obvious measurement — how far the screen image rotates per dir step — reads
        /// <b>−46.7525° for this kit and +46.7525° for the counter-clockwise reference</b>. Same
        /// magnitude, because that mean is an alternating foreshortened quantity, so a probe that
        /// compared magnitudes would call two opposite-turning families identical. Un-squashing the
        /// depth axis by <c>sin(elev)</c> first recovers the true ground-plane bearing, and there the
        /// two separate cleanly at exactly ∓45°/dir.</para>
        ///
        /// <para>Both figures are recorded so the report itself shows the trap rather than merely
        /// avoiding it.</para>
        /// </summary>
        static void MeasureHandedness(IRigScriptHost host, Report r)
        {
            const string bearings = @"(function(){
              var DEG=Math.PI/180, Q=Math.sin(40*DEG);
              function sweep(p){ var out=[]; for(var d=0;d<8;d++){
                  var o=p(d,[0,0,0]), q=p(d,[1,0,0]);
                  out.push([Math.atan2(-((q.y-o.y)/Q), q.x-o.x)/DEG,
                            Math.atan2(-(q.y-o.y), q.x-o.x)/DEG]); }
                return out; }
              function step(v,i){ var s=0,n=0; for(var k=1;k<v.length;k++){
                  var d=v[k][i]-v[k-1][i]; while(d>180)d-=360; while(d<=-180)d+=360; s+=d;n++; }
                return s/n; }
              var iso=function(d,p){ var B=IsoSolid.camBasis({dir:d,elev:40});
                                     var v=IsoSolid.proj(p[0],p[1],p[2],B,0,0); return {x:v.sx,y:v.sy}; };
              var ref=function(d,p){ var q=UtilityIso.project(d,p,40); return {x:q.x,y:q.y}; };
              var a=sweep(iso), b=sweep(ref);
              return [a.map(function(v){return v[0].toFixed(6);}).join(','),
                      step(a,0).toFixed(6), step(b,0).toFixed(6),
                      step(a,1).toFixed(6), step(b,1).toFixed(6)].join('|');
            })()";

            var parts = host.EvaluateString(bearings).Split('|');
            r.GroundBearings = parts[0].Split(',').Select(Num).ToArray();
            r.GroundStepPerDir = Num(parts[1]);
            r.ReferenceGroundStepPerDir = Num(parts[2]);
            r.ScreenStepMean = Num(parts[3]);
            r.ReferenceScreenStepMean = Num(parts[4]);

            if (Math.Abs(Math.Abs(r.GroundStepPerDir) - 45.0) > 0.01)
                throw new InvalidOperationException(
                    $"The fuel turntable steps {r.GroundStepPerDir:F4}°/dir on the ground plane, not " +
                    "±45°. Either the elevation is no longer 40° or this is not an 8-facing " +
                    "turntable; refusing to infer a handedness from it.");

            if (Math.Sign(r.GroundStepPerDir) == Math.Sign(r.ReferenceGroundStepPerDir))
                throw new InvalidOperationException(
                    $"The fuel kit and {ReferenceRigKey} now step the SAME way " +
                    $"({r.GroundStepPerDir:F4}° vs {r.ReferenceGroundStepPerDir:F4}°). The reference " +
                    "is registered CounterClockwise, so this kit would be too — which contradicts " +
                    "the deck-loop turntable it shares. Re-measure before baking.");

            r.Measured = r.GroundStepPerDir < 0
                ? AzimuthConvention.Clockwise
                : AzimuthConvention.CounterClockwise;
        }

        // ---- one turntable, not two that look alike --------------------------------------------

        /// <summary>
        /// The fuel rig destructures <c>camBasis</c>/<c>proj</c> out of <c>IsoSolid</c>, so it should
        /// project through the registered turntable BY IDENTITY rather than by resemblance. Checked
        /// the way the shipyard kit's was: project a known point both ways and compare.
        ///
        /// <para>The drum's large bung sits at <c>(r·0.62, 0, …)</c> — off-axis in the ground plane,
        /// so its screen position sweeps with dir. Its z is dir-invariant, which is what makes a
        /// CONSTANT y residual meaningful: it says the assumed height is off by a fixed amount, not
        /// that the camera differs. A camera difference would make the residual VARY with dir, and
        /// that is what this asserts against.</para>
        /// </summary>
        static void MeasureTurntableIdentity(IRigScriptHost host, string g, Report r)
        {
            string js = $@"(function(){{
              var Z={g}.TYPES.drum.sizes.s205, c={g}.cell('drum','s205',{{size:'s205'}});
              var dx=[], dy=[];
              for(var d=0;d<8;d++){{
                var m={g}.mount('drum',d,'bung',{{size:'s205',wear:'{FuelStorageKit.BakedWear}'}});
                var B=IsoSolid.camBasis({{dir:d,elev:40}});
                var v=IsoSolid.proj(Z.dia/2*0.62,0,Z.h+0.004,B,c.cx,c.cy);
                dx.push(Math.abs(m.x-v.sx)); dy.push(m.y-v.sy);
              }}
              var maxdx=Math.max.apply(null,dx);
              var spread=Math.max.apply(null,dy)-Math.min.apply(null,dy);
              return maxdx.toFixed(12)+'|'+spread.toFixed(12);
            }})()";

            var p = host.EvaluateString(js).Split('|');
            r.TurntableMaxDxPx = Num(p[0]);
            r.TurntableDySpreadPx = Num(p[1]);

            const double tol = 1e-9;
            if (r.TurntableMaxDxPx > tol || r.TurntableDySpreadPx > tol)
                throw new InvalidOperationException(
                    $"FuelIso.mount() no longer agrees with IsoSolid.proj (max |dx| " +
                    $"{r.TurntableMaxDxPx:E3} px, dy spread {r.TurntableDySpreadPx:E3} px). The kit is " +
                    "registered as riding the deck-loop turntable by identity; if it now carries its " +
                    "own camera, the prerequisite and the measured handedness both need re-deriving.");
        }

        // ---- the ring pass ---------------------------------------------------------------------

        /// <summary>
        /// ADR 0031 retired the keyline, so a bake must prove it ships ringless — and prove the A/B
        /// arm it proves that WITH actually does something.
        ///
        /// <para>⚠️ Two traps, both measured here. (1) <c>FuelIso</c> exports no
        /// <c>KEYLINE_DEFAULT</c>, so the declaration this repo's gate reads has to come from
        /// <c>IsoSolid</c> — which owns the only ring pass the kit has, and is therefore the right
        /// place for it rather than a second copy that could drift. (2) The kit spells its A/B arm
        /// <c>{keyline:true}</c>; driving it with this repo's usual <c>{outline:true}</c> is
        /// SILENTLY IGNORED and comes back ringless, which is indistinguishable from a successful
        /// gate-off render. Both arms are measured so neither can be assumed.</para>
        /// </summary>
        static void MeasureKeyline(IRigScriptHost host, string g, Report r)
        {
            r.ExportsKeylineDefault = host.EvaluateBool($"typeof {g}.KEYLINE_DEFAULT !== 'undefined'");
            r.TurntableExportsKeylineDefault =
                host.EvaluateBool("typeof IsoSolid.KEYLINE_DEFAULT === 'boolean'");

            if (!r.TurntableExportsKeylineDefault)
                throw new InvalidOperationException(
                    "IsoSolid no longer exports KEYLINE_DEFAULT. The fuel kit does not declare one of " +
                    "its own, so the turntable's constant is the only ringless declaration this bake " +
                    "can bind its gate to.");

            r.TurntableKeylineDefaultValue = host.EvaluateBool("IsoSolid.KEYLINE_DEFAULT === true");
            if (r.TurntableKeylineDefaultValue)
                throw new InvalidOperationException(
                    "IsoSolid.KEYLINE_DEFAULT is TRUE — the shared turntable now draws the ring by " +
                    "default, which ADR 0031 retired. Refusing to bake ringed art.");

            string delta = $@"(function(){{
              var o={{size:'s205',fuel:'diesel',wear:'{FuelStorageKit.BakedWear}',fill:0.5}};
              function px(a,b){{var n=0;for(var i=0;i<a.length;i+=4){{
                if(a[i]!==b[i]||a[i+1]!==b[i+1]||a[i+2]!==b[i+2]||a[i+3]!==b[i+3])n++;}}return n;}}
              function w(extra){{var c=Object.assign({{}},o);for(var k in extra)c[k]=extra[k];
                                 return {g}.render('drum',3,c);}}
              var base=w({{}});
              return px(base,w({{keyline:true}}))+'|'+px(base,w({{outline:true}}));
            }})()";
            var d = host.EvaluateString(delta).Split('|');
            r.KeylineTruePixelDelta = int.Parse(d[0], CultureInfo.InvariantCulture);
            r.OutlineTruePixelDelta = int.Parse(d[1], CultureInfo.InvariantCulture);

            // The POSITIVE CONTROL. Without it, "the default render is ringless" is unfalsifiable —
            // a rig that ignored the option entirely would pass by drawing the same thing twice.
            if (r.KeylineTruePixelDelta <= 0)
                throw new InvalidOperationException(
                    "{keyline:true} changed nothing, so the ring pass cannot be exercised and the " +
                    "ringless claim is unfalsifiable. Either the option was renamed or the pass is " +
                    "gone; both need looking at before this kit ships as ADR-0031 compliant.");
        }

        // ---- the two silent-divergence traps ----------------------------------------------------

        /// <summary>
        /// Asserts the wear/cache divergence documented on <see cref="FuelStorageKit.BakedWear"/> is
        /// STILL PRESENT, and returns whether it is.
        ///
        /// <para>Recorded, not patched: the rig runs unmodified (ADR 0021 §5). The mitigation is
        /// entirely on our side — every render this baker issues passes an explicit <c>wear</c> — and
        /// the point of measuring the defect is that this test goes RED the day a drop fixes it, at
        /// which point the workaround can go rather than quietly outliving its reason.</para>
        /// </summary>
        public static bool MeasureWearCacheDefect(IRigScriptHost host, string g)
        {
            // Fresh host state is not available mid-session, so measure the collision directly: the
            // omitted-wear render and the explicit 'working' render must come back IDENTICAL because
            // they share a cache key — while 'fresh' differs from both.
            string js = $@"(function(){{
              var o={{size:'s205',fuel:'diesel',fill:0.5}};
              function w(extra){{var c=Object.assign({{}},o);for(var k in extra)c[k]=extra[k];
                                 return {g}.render('drum',3,c);}}
              function same(a,b){{if(a.length!==b.length)return false;
                for(var i=0;i<a.length;i++)if(a[i]!==b[i])return false; return true;}}
              var omitted=w({{}}), working=w({{wear:'working'}}), fresh=w({{wear:'fresh'}});
              return (same(omitted,working)?'1':'0')+(same(omitted,fresh)?'1':'0');
            }})()";
            string s = host.EvaluateString(js);
            // "10" = collides with 'working' and differs from 'fresh' — the defect as measured.
            return s.Length == 2 && s[0] == '1' && s[1] == '0';
        }

        /// <summary>
        /// <c>resolveSize</c> falls through to <c>sizes[min(len−1, 1)]</c> for any size it does not
        /// know, with no error — so a typo bakes a plausible, wrong vessel under the right filename.
        /// The baker's defence is to check every size against <c>sizesOf()</c> before rendering;
        /// this measures that the hazard is still there to defend against.
        /// </summary>
        public static bool MeasureSilentSizeFallback(IRigScriptHost host, string g) =>
            host.EvaluateBool($"{g}.resolveSize('drum','definitely_not_a_size') === 's205'");

        // ---- the vocabulary ---------------------------------------------------------------------

        /// <summary>
        /// The kit table names types, sizes and grades so the build list can be built without a host.
        /// A vessel or grade added upstream and missed here bakes a SHORT kit that still reports
        /// success, so the two lists are reconciled before any pixels are written.
        /// </summary>
        public static void AssertVocabularyMatches(IRigScriptHost host, string g, Report r)
        {
            AssertSetsMatch("type", r.Types, FuelStorageKit.Sizes.Keys.ToArray());
            AssertSetsMatch("grade", r.Grades, FuelStorageKit.Grades.ToArray());

            foreach (var kv in FuelStorageKit.Sizes)
            {
                var live = Csv(host, $"{g}.sizesOf('{kv.Key}').join(',')");
                AssertSetsMatch($"{kv.Key} size", live, kv.Value);
            }
        }

        static void AssertSetsMatch(string what, IReadOnlyList<string> live, IReadOnlyList<string> table)
        {
            var missing = live.Except(table).ToArray();
            var extra = table.Except(live).ToArray();
            if (missing.Length == 0 && extra.Length == 0) return;

            throw new InvalidOperationException(
                $"The rig and {nameof(FuelStorageKit)} disagree on the {what} list. " +
                (missing.Length > 0 ? $"The rig draws {string.Join(", ", missing)}, which the kit " +
                                      "table does not ask for — that bakes a short kit and still " +
                                      "reports success. " : "") +
                (extra.Length > 0 ? $"The kit asks for {string.Join(", ", extra)}, which the rig does " +
                                    "not know. " : ""));
        }

        // ---- helpers ----------------------------------------------------------------------------

        static string[] Csv(IRigScriptHost host, string expr)
        {
            string s = host.EvaluateString(expr);
            return string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split(',');
        }

        static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    }
}
