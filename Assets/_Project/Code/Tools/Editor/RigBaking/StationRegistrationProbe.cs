using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// What the gas-station kit's conventions actually ARE, measured from the live rigs every time
    /// they bake rather than read off a README.
    ///
    /// <para>Every kit imported into this repo has had at least one declared convention turn out
    /// wrong, and the failures are silent: a mislabelled handedness writes eight good cells in
    /// reverse order, a missing prerequisite renders a plausible wrong picture. So the bake refuses
    /// on any disagreement between what the catalog DECLARES and what this measures.</para>
    ///
    /// <para>This kit's twist is that all three of its live traps are in its own DOCUMENTATION rather
    /// than its code — a size key, an argument order and a call shape, each of which produces a
    /// picture rather than an error. Those are measured here too, so the day a drop fixes one the
    /// test goes red and says so instead of the baker quietly relying on it.</para>
    /// </summary>
    public static class StationRegistrationProbe
    {
        /// <summary>
        /// The rig this kit's handedness is measured AGAINST, and why the answer means anything.
        ///
        /// <para><c>utilityIso</c> is registered <see cref="AzimuthConvention.CounterClockwise"/> in
        /// this repo after its own probe measured it, and it carries its own projector rather than
        /// sharing <c>IsoSolid</c>'s. Measuring both in ONE host with ONE sign convention turns
        /// "which way does this turn?" into a comparison against a known reference instead of an
        /// absolute claim about a camera basis.</para>
        /// </summary>
        public const string ReferenceRigKey = "utilityIso";

        public sealed class Report
        {
            public AzimuthConvention Measured;
            public double[] GroundBearings = Array.Empty<double>();
            public double GroundStepPerDir;
            public double SlotPairGroundStepPerDir;
            public double ReferenceGroundStepPerDir;
            public double ScreenStepMean, ReferenceScreenStepMean;

            /// <summary>Max |mount − IsoSolid.proj| in x across the facings. Zero means one camera.</summary>
            public double TurntableMaxDxPx;

            /// <summary>Spread of the y residual across facings. A CONSTANT offset is a different
            /// assumed z, not a different camera; a VARYING one would be two turntables.</summary>
            public double TurntableDySpreadPx;

            /// <summary>True while the rig's own header still names the cardlock <c>sCardlock</c> and
            /// that name still resolves to a different machine.</summary>
            public bool CardlockNameDefectPresent;

            /// <summary>True while <c>render(type, size, dir, opts)</c> — the export README's
            /// signature — renders something instead of throwing.</summary>
            public bool ReadmeRenderSignatureDefectPresent;

            /// <summary>True while <c>gameplaySections({size})</c> — the interior README's call —
            /// silently returns the pay booth for every size.</summary>
            public bool InteriorSectionsObjectDefectPresent;

            public bool SilentSizeFallbackPresent;
            public bool UnknownTypeThrows;

            /// <summary>False means an omitted option and an explicitly defaulted one diverge —
            /// the fuel kit's phantom-state defect. Measured, not assumed absent.</summary>
            public bool OptionsFunnelThroughResolve;

            /// <summary>Worst per-piece pixel difference between facing <c>d</c> and <c>d+4</c> over
            /// the folded types. The fold is only honest while this stays inside
            /// <see cref="GasStationKit.Fold180TolerancePixels"/>.</summary>
            public int Fold180WorstPixelDelta;

            public string Fold180WorstPiece = "";

            /// <summary>True when the interior's cell and pivot match the shell's at every interior
            /// size. Seamlessness is this, measured.</summary>
            public bool InteriorSharesShellCell;

            public string[] Types = Array.Empty<string>();
            public string[] Grades = Array.Empty<string>();
            public double SlotPitch;

            public override string ToString() =>
                $"gasStation: {Measured} ({GroundStepPerDir:F3}°/dir ground, slot pair " +
                $"{SlotPairGroundStepPerDir:F3}°/dir, reference {ReferenceGroundStepPerDir:F3}°/dir), " +
                $"turntable dx {TurntableMaxDxPx:F9} px / dy spread {TurntableDySpreadPx:F9} px, " +
                $"fold180 worst {Fold180WorstPixelDelta} px ({Fold180WorstPiece}), interior shares " +
                $"cell {InteriorSharesShellCell}, doc defects " +
                $"[cardlock {CardlockNameDefectPresent}, render-sig {ReadmeRenderSignatureDefectPresent}, " +
                $"sections-object {InteriorSectionsObjectDefectPresent}]";
        }

        /// <summary>
        /// Installs the whole kit (via the interior key, whose prerequisites bring the shell, the
        /// grade table and the turntable up in the only order that works) and measures everything the
        /// bake relies on. The host is left loaded so a caller can bake through it.
        /// </summary>
        public static Report Measure(IRigScriptHost host)
        {
            var interior = RigCatalog.Get(GasStationKit.InteriorRigKey);
            RigCatalog.InstallModule(host, interior);

            // The reference rig shares the host only for the handedness comparison. It installs a
            // different global, carries its own projector and touches nothing this kit reads.
            RigCatalog.InstallModule(host, RigCatalog.Get(ReferenceRigKey));

            var r = new Report();
            string g = GasStationKit.GlobalName;
            string i = GasStationKit.InteriorGlobalName;

            MeasureHandedness(host, g, r);
            MeasureTurntableIdentity(host, g, r);
            MeasureDocumentedNameDefects(host, g, i, r);
            MeasureOptionFunnel(host, g, r);
            MeasureFold180(host, g, r);
            MeasureInteriorCell(host, g, i, r);

            r.Types = Csv(host, $"{g}.TYPE_ORDER.join(',')");
            r.Grades = Csv(host, $"{g}.GRADE_ORDER.join(',')");
            r.SlotPitch = host.EvaluateNumber($"{g}.SLOT_PITCH");

            return r;
        }

        // ---- handedness ------------------------------------------------------------------------

        /// <summary>
        /// ⚠️ THE ONLY ADMISSIBLE HANDEDNESS TEST IS THE UN-SQUASHED GROUND-PLANE BEARING.
        ///
        /// <para>The obvious measurement — how far the screen image rotates per dir step — reads
        /// <b>−46.7525° for this kit and +46.7525° for the counter-clockwise reference</b>. Same
        /// magnitude, because that mean is an alternating foreshortened quantity, so a probe that
        /// compared magnitudes would call two opposite-turning families identical. Un-squashing the
        /// depth axis by <c>sin(elev)</c> first recovers the true ground-plane bearing, and there the
        /// two separate cleanly at exactly ∓45°/dir. Both figures are recorded so the report itself
        /// shows the trap rather than merely avoiding it.</para>
        ///
        /// <para>Measured TWICE by different routes, because an abstract axis and real geometry can
        /// disagree and the disagreement is what you want to hear about: the rig's own
        /// <c>project()</c>, and the island's <c>slot0 → slot3</c> mounts, which are an abeam pair of
        /// real bolt-down plates at equal height. Both read −45.000°/dir.</para>
        /// </summary>
        static void MeasureHandedness(IRigScriptHost host, string g, Report r)
        {
            string bearings = $@"(function(){{
              var DEG=Math.PI/180, Q=Math.sin(40*DEG);
              function bear(o,q){{ return [Math.atan2(-((q.y-o.y)/Q), q.x-o.x)/DEG,
                                          Math.atan2(-(q.y-o.y), q.x-o.x)/DEG]; }}
              function sweep(p){{ var out=[]; for(var d=0;d<8;d++) out.push(bear(p(d,[0,0,0]),p(d,[1,0,0])));
                return out; }}
              function step(v,i){{ var s=0,n=0; for(var k=1;k<v.length;k++){{
                  var d=v[k][i]-v[k-1][i]; while(d>180)d-=360; while(d<=-180)d+=360; s+=d;n++; }}
                return s/n; }}
              var self=function(d,p){{ var q={g}.project(p[0],p[1],p[2],d,40); return {{x:q.x,y:q.y}}; }};
              var ref=function(d,p){{ var q=UtilityIso.project(d,p,40); return {{x:q.x,y:q.y}}; }};
              var O={{size:'s4',grades:['gas','diesel','mixed']}};
              var slots=[]; for(var d=0;d<8;d++)
                slots.push(bear({g}.mount('island',d,'slot0',O), {g}.mount('island',d,'slot3',O)));
              var a=sweep(self), b=sweep(ref);
              return [a.map(function(v){{return v[0].toFixed(6);}}).join(','),
                      step(a,0).toFixed(6), step(b,0).toFixed(6),
                      step(a,1).toFixed(6), step(b,1).toFixed(6),
                      step(slots,0).toFixed(6)].join('|');
            }})()";

            var parts = host.EvaluateString(bearings).Split('|');
            r.GroundBearings = parts[0].Split(',').Select(Num).ToArray();
            r.GroundStepPerDir = Num(parts[1]);
            r.ReferenceGroundStepPerDir = Num(parts[2]);
            r.ScreenStepMean = Num(parts[3]);
            r.ReferenceScreenStepMean = Num(parts[4]);
            r.SlotPairGroundStepPerDir = Num(parts[5]);

            if (Math.Abs(Math.Abs(r.GroundStepPerDir) - 45.0) > 0.01)
                throw new InvalidOperationException(
                    $"The station turntable steps {r.GroundStepPerDir:F4}°/dir on the ground plane, " +
                    "not ±45°. Either the elevation is no longer 40° or this is not an 8-facing " +
                    "turntable; refusing to infer a handedness from it.");

            if (Math.Sign(r.GroundStepPerDir) != Math.Sign(r.SlotPairGroundStepPerDir))
                throw new InvalidOperationException(
                    $"The rig's own project() steps {r.GroundStepPerDir:F4}°/dir but its island slot " +
                    $"mounts step {r.SlotPairGroundStepPerDir:F4}°/dir. An abstract axis and the real " +
                    "geometry disagree about which way this kit turns; do not bake either answer.");

            if (Math.Sign(r.GroundStepPerDir) == Math.Sign(r.ReferenceGroundStepPerDir))
                throw new InvalidOperationException(
                    $"The station kit and {ReferenceRigKey} now step the SAME way " +
                    $"({r.GroundStepPerDir:F4}° vs {r.ReferenceGroundStepPerDir:F4}°). The reference " +
                    "is registered CounterClockwise, so this kit would be too — which contradicts the " +
                    "deck-loop turntable it shares with the fuel kit. Re-measure before baking.");

            r.Measured = r.GroundStepPerDir < 0
                ? AzimuthConvention.Clockwise
                : AzimuthConvention.CounterClockwise;
        }

        // ---- one turntable, not two that look alike ---------------------------------------------

        /// <summary>
        /// The station rig destructures <c>camBasis</c>/<c>proj</c> out of <c>IsoSolid</c>, so it
        /// should project through the registered turntable BY IDENTITY rather than by resemblance.
        ///
        /// <para>The island's <c>slot0</c> plate sits off-centre in the ground plane, so its screen
        /// position sweeps with dir. Its z is dir-invariant, which is what makes a CONSTANT y
        /// residual meaningful: it says the assumed height is off by a fixed amount, not that the
        /// camera differs. A camera difference makes the residual VARY with dir, and that is what
        /// this asserts against.</para>
        /// </summary>
        static void MeasureTurntableIdentity(IRigScriptHost host, string g, Report r)
        {
            string js = $@"(function(){{
              var O={{size:'s4',grades:['gas','diesel','mixed']}};
              var c={g}.cell('island','s4',O), xs={g}.slotXs(4), dx=[], dy=[];
              for(var d=0;d<8;d++){{
                var m={g}.mount('island',d,'slot0',O);
                var B=IsoSolid.camBasis({{dir:d,elev:40}});
                var v=IsoSolid.proj(xs[0],0,0,B,c.cx,c.cy);
                dx.push(Math.abs(m.x-v.sx)); dy.push(m.y-v.sy);
              }}
              return [Math.max.apply(null,dx).toFixed(9),
                      (Math.max.apply(null,dy)-Math.min.apply(null,dy)).toFixed(9)].join('|');
            }})()";

            var parts = host.EvaluateString(js).Split('|');
            r.TurntableMaxDxPx = Num(parts[0]);
            r.TurntableDySpreadPx = Num(parts[1]);

            if (r.TurntableMaxDxPx > 1e-6 || r.TurntableDySpreadPx > 1e-6)
                throw new InvalidOperationException(
                    $"The station rig's mounts and IsoSolid.proj disagree by {r.TurntableMaxDxPx:F9} px " +
                    $"in x and {r.TurntableDySpreadPx:F9} px of dy spread. This kit is registered as " +
                    "sharing the deck-loop turntable; a varying residual means it no longer does, and " +
                    "every mount the gameplay side reads would be off.");
        }

        // ---- the three documented-name defects ---------------------------------------------------

        /// <summary>
        /// The kit's own documentation names three things that do not exist, and every one of them
        /// renders rather than throwing. Measured here so the baker's defences are known to still be
        /// needed — and so the day a drop fixes one, this goes red and says which.
        /// </summary>
        static void MeasureDocumentedNameDefects(IRigScriptHost host, string g, string i, Report r)
        {
            const string opts = "{grades:['gas','diesel','mixed'],wear:'working'}";

            // 1. The rig's header table says `sCardlock`; the size key is `sLock`.
            r.CardlockNameDefectPresent = host.EvaluateBool(
                $"{g}.resolveSize('dispenser','sCardlock')==='sKerb' && " +
                $"{g}.sizesOf('dispenser').indexOf('sCardlock')<0");

            // 2. The export README's render(type, size, dir, opts). The size string lands where dir
            //    belongs, opts is a number, and the rig draws a kerb post without complaint.
            r.ReadmeRenderSignatureDefectPresent = host.EvaluateBool(
                $@"(function(){{ try {{ var a={g}.render('dispenser','sTwin',2,{opts});
                     return !!a && a.length>0; }} catch(e) {{ return false; }} }})()");

            // 3. The interior README's gameplaySections({size}). The rig's own call site passes a
            //    bare string; the object form falls through to the FIRST size — the pay booth — for
            //    every size asked of it, which would put the booth's sales floor in the C-store.
            r.InteriorSectionsObjectDefectPresent = host.EvaluateBool(
                $@"JSON.stringify({i}.gameplaySections({{size:'sStore'}}))===
                   JSON.stringify({i}.gameplaySections('sKiosk')) &&
                   JSON.stringify({i}.gameplaySections('sStore'))!==
                   JSON.stringify({i}.gameplaySections('sKiosk'))");

            // The asymmetry underneath all three: an unknown TYPE throws, an unknown SIZE does not.
            r.SilentSizeFallbackPresent = host.EvaluateBool(
                $"{g}.resolveSize('dispenser','no_such_size')==='sKerb'");
            r.UnknownTypeThrows = host.EvaluateBool(
                $@"(function(){{ try {{ {g}.render('forecourt',0,{{}}); return false; }}
                    catch(e) {{ return true; }} }})()");

            if (!r.UnknownTypeThrows)
                throw new InvalidOperationException(
                    "An unknown piece TYPE no longer throws. That guard is the only reason a typo in " +
                    "the build table is caught at all — the size axis has no such guard. Re-check the " +
                    "baker's AssertKnown before baking.");
        }

        // ---- the phantom-state check the fuel kit failed ------------------------------------------

        /// <summary>
        /// ⭐ The fuel kit's worst defect, checked for HERE rather than assumed absent.
        ///
        /// <para>There, the streak passes read <c>o.wear</c> RAW while <c>wearPass</c> and the cache
        /// key normalised it — so an omitted option rendered a fourth image and stored it under the
        /// default's key, and whichever call ran first won for the rest of the session. This rig
        /// funnels every option through <c>resolve()</c> before both, so the two should be one picture
        /// under one key. Measured on every piece, in BOTH call orders, because an order-dependent
        /// defect is invisible to a probe that only ever calls one way round.</para>
        /// </summary>
        static void MeasureOptionFunnel(IRigScriptHost host, string g, Report r)
        {
            string js = $@"(function(){{
              var G=['gas','diesel','mixed'];
              function full(s){{ return {{size:s,grades:G,lit:false,hoses:0,sides:0,out:-1,
                style:'fascia',open:0,span:1,bollards:true,bin:false,wear:'working',seed:11,
                keyline:false,elev:40}}; }}
              function bare(s){{ return {{size:s,grades:G}}; }}
              function px(a){{ var n=0; for(var k=0;k<a.length;k++) n=(n*31+a[k])>>>0; return n; }}
              var ok=true;
              {g}.TYPE_ORDER.forEach(function(t){{ {g}.sizesOf(t).forEach(function(s){{
                var b1=px({g}.render(t,2,bare(s))), f1=px({g}.render(t,2,full(s)));
                var f2=px({g}.render(t,3,full(s))), b2=px({g}.render(t,3,bare(s)));
                if (b1!==f1 || b2!==f2) ok=false;
              }}); }});
              return ok;
            }})()";

            r.OptionsFunnelThroughResolve = host.EvaluateBool(js);

            if (!r.OptionsFunnelThroughResolve)
                throw new InvalidOperationException(
                    "An omitted render option and an explicitly defaulted one now produce DIFFERENT " +
                    "pixels on this rig — the fuel kit's phantom-state defect has appeared here. The " +
                    "baker already passes every option explicitly so its own sheets are safe, but " +
                    "anything else calling this rig is not, and the catalog comment saying otherwise " +
                    "is now wrong. Re-measure which option diverged before baking.");
        }

        // ---- the fold ------------------------------------------------------------------------------

        /// <summary>
        /// Re-measures the 180° symmetry that <see cref="GasStationKit.Fold180Types"/> spends half the
        /// island and canopy pixels on.
        ///
        /// <para>⚠️ Counted in PIXELS, not by hash. The canopies are byte-identical between <c>d</c>
        /// and <c>d+4</c>; the islands differ by one to four pixels of tens of thousands, which is the
        /// rasteriser breaking a tie on a symmetric kerb. A hash comparison calls those islands
        /// 5- and 8-distinct and is exactly what would talk you out of a real saving — so the
        /// tolerance is a measured worst case, and a piece that ever exceeds it is genuinely
        /// asymmetric and must go back to eight facings.</para>
        /// </summary>
        static void MeasureFold180(IRigScriptHost host, string g, Report r)
        {
            var folded = GasStationKit.Fold180Types
                                      .SelectMany(t => GasStationKit.Sizes[t].Select(s => (t, s)));

            foreach (var (type, size) in folded)
            {
                string js = $@"(function(){{
                  var o={{size:'{size}',grades:['gas','diesel','mixed'],lit:false,hoses:0,sides:0,
                    out:-1,style:'fascia',open:0,span:1,bollards:true,bin:false,wear:'working',
                    seed:11,keyline:false,elev:40}};
                  var worst=0;
                  for(var d=0;d<4;d++){{
                    var a={g}.render('{type}',d,o), b={g}.render('{type}',d+4,o), n=0;
                    for(var k=0;k<a.length;k+=4)
                      if(a[k]!==b[k]||a[k+1]!==b[k+1]||a[k+2]!==b[k+2]||a[k+3]!==b[k+3]) n++;
                    if(n>worst) worst=n;
                  }}
                  return worst;
                }})()";

                int worst = (int)host.EvaluateNumber(js);
                if (r.Fold180WorstPiece.Length != 0 && worst <= r.Fold180WorstPixelDelta) continue;

                r.Fold180WorstPixelDelta = worst;
                r.Fold180WorstPiece = $"{type}/{size}";
            }

            if (r.Fold180WorstPixelDelta > GasStationKit.Fold180TolerancePixels)
                throw new InvalidOperationException(
                    $"{r.Fold180WorstPiece} now differs by {r.Fold180WorstPixelDelta} px between " +
                    $"facing d and d+4, past the kit's measured {GasStationKit.Fold180TolerancePixels} " +
                    "px fold tolerance. This piece is no longer 180°-symmetric, so folding it to four " +
                    "facings would silently show the wrong side. Move its type out of " +
                    $"{nameof(GasStationKit)}.{nameof(GasStationKit.Fold180Types)} and re-bake at eight " +
                    "rather than raising the tolerance.");
        }

        // ---- seamlessness --------------------------------------------------------------------------

        /// <summary>
        /// The seamless-interior claim, measured: the room's quad and pivot must be the shell's own,
        /// at every storefront size. This is what lets the two sheets crop independently and still
        /// register — they are aligned by a shared world pivot, not by a cell corner.
        /// </summary>
        static void MeasureInteriorCell(IRigScriptHost host, string g, string i, Report r)
        {
            const string opts = "{grades:['gas','diesel','mixed'],wear:'working'}";
            r.InteriorSharesShellCell = true;

            foreach (string size in GasStationKit.InteriorSizes)
            {
                bool same = host.EvaluateBool(
                    $@"(function(){{ var a={i}.cell('{size}',{{}}),
                         b={g}.cell('store','{size}',Object.assign({{size:'{size}'}},{opts}));
                       return a.W===b.W && a.H===b.H && a.cx===b.cx && a.cy===b.cy; }})()");

                if (same) continue;

                r.InteriorSharesShellCell = false;
                throw new InvalidOperationException(
                    $"The interior's cell for '{size}' is no longer the shell's own quad and pivot. " +
                    "The whole seamless contract rests on that identity — blit the room, blit the " +
                    "shell over it, and the doorway lines up because there is only one cell in play. " +
                    "Do not bake a room that has to be nudged into its building.");
            }
        }

        // ---- the vocabulary the build table restates -------------------------------------------------

        /// <summary>
        /// The kit table names its pieces, sizes and grades so a bake can be scoped without a host.
        /// Every one of those lists is checked against the live rig here, because a piece added
        /// upstream and missed in the table bakes a short kit and still reports success.
        /// </summary>
        public static void AssertVocabularyMatches(IRigScriptHost host, string g, Report r)
        {
            AssertSetsMatch("type", r.Types, GasStationKit.Sizes.Keys.ToArray());
            AssertSetsMatch("grade", r.Grades, GasStationKit.RigGradeKeys.ToArray());

            foreach (var kv in GasStationKit.Sizes)
            {
                var live = Csv(host, $"{g}.sizesOf('{kv.Key}').join(',')");
                AssertSetsMatch($"{kv.Key} size", live, kv.Value);
            }

            int pieces = (int)host.EvaluateNumber(
                $"{g}.TYPE_ORDER.reduce(function(n,t){{return n+{g}.sizesOf(t).length;}},0)");

            if (pieces != 21)
                throw new InvalidOperationException(
                    $"The rig publishes {pieces} pieces; this kit was imported against 21 " +
                    "(dispenser ×7, island ×4, canopy ×3, sign ×3, store ×2, fillport ×2). A drop " +
                    "that adds or drops a piece needs the build table and the sidecar re-read, not " +
                    "just a re-bake.");
        }

        static void AssertSetsMatch(string what, IReadOnlyList<string> live, IReadOnlyList<string> table)
        {
            var missing = live.Except(table).ToArray();
            var extra = table.Except(live).ToArray();
            if (missing.Length == 0 && extra.Length == 0) return;

            throw new InvalidOperationException(
                $"The rig and {nameof(GasStationKit)} disagree on the {what} list. " +
                (missing.Length > 0
                     ? $"The rig draws {string.Join(", ", missing)}, which the kit table does not ask " +
                       "for — that bakes a short kit and still reports success. "
                     : "") +
                (extra.Length > 0
                     ? $"The kit asks for {string.Join(", ", extra)}, which the rig does not know — " +
                       "and an unknown SIZE does not throw here, it renders a different machine. "
                     : ""));
        }

        // ---- helpers -----------------------------------------------------------------------------

        static string[] Csv(IRigScriptHost host, string expr)
        {
            string s = host.EvaluateString(expr);
            return string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split(',');
        }

        static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    }
}
