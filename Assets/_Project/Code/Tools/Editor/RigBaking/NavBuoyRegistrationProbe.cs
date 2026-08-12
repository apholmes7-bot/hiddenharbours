using System;
using System.Globalization;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Measures the nav-buoy kit's conventions FROM THE LIVE RIG, so the registration in
    /// <see cref="RigCatalog"/> is a prior that gets checked rather than a claim that gets trusted.
    ///
    /// <para>The declared facing order has shipped wrong five times in this repo and every one of
    /// those defects came from believing a declaration. This kit's own README declares CW — and is
    /// right — but "the README is right" is a measurement result here, not an assumption.</para>
    /// </summary>
    public static class NavBuoyRegistrationProbe
    {
        public sealed class Result
        {
            public AzimuthConvention Convention;
            public double GroundStepDeg;
            public double ScreenMeanStepDeg;
            public double Elevation;
            public double DepthScale;
            public int TypeCount;
            public bool ExportsKeylineDefault;
            public bool DefaultRenderIsRingless;
            public bool OutlineIsLiveAlias;
            public string EngineName;

            public override string ToString() =>
                $"nav-buoy: {Convention} ({GroundStepDeg:F3}°/dir ground, {ScreenMeanStepDeg:F4}° screen), " +
                $"elev {Elevation}°, {TypeCount} types, ringless={DefaultRenderIsRingless}";
        }

        /// <summary>
        /// ⚠️ <b>The ground-plane bearing is the handedness test; the screen angle is not.</b> A
        /// projected point carries the elevation foreshorten, so the screen step is an alternating
        /// quantity whose mean (−46.75°) is shared by families that turn OPPOSITE ways. Un-squash
        /// the depth by <c>/sin(elev)</c> first, then read the angle.
        /// </summary>
        public static Result Measure(IRigScriptHost host, string globalName = "NavBuoy",
                                     string turntableGlobal = "IsoSolid")
        {
            var r = new Result { EngineName = host.EngineName };

            r.Elevation = host.EvaluateNumber($"{turntableGlobal}.DEFAULT_ELEV");
            r.DepthScale = Math.Sin(r.Elevation * Math.PI / 180.0);

            // +X ground-plane bearing per dir, depth un-squashed.
            var bearings = new double[NavBuoyKit.Facings];
            var screen = new double[NavBuoyKit.Facings];
            for (int d = 0; d < NavBuoyKit.Facings; d++)
            {
                host.Execute(
                    $"globalThis.__nbB = {turntableGlobal}.camBasis({{dir:{d},elev:{turntableGlobal}.DEFAULT_ELEV}});" +
                    $"globalThis.__nbO = {turntableGlobal}.proj(0,0,0,globalThis.__nbB,0,0);" +
                    $"globalThis.__nbP = {turntableGlobal}.proj(1,0,0,globalThis.__nbB,0,0);");
                double dx = host.EvaluateNumber("globalThis.__nbP.sx - globalThis.__nbO.sx");
                double dy = host.EvaluateNumber("globalThis.__nbP.sy - globalThis.__nbO.sy");

                bearings[d] = Math.Atan2(-dy / r.DepthScale, dx) * 180.0 / Math.PI;
                screen[d] = Math.Atan2(-dy, dx) * 180.0 / Math.PI;
            }

            r.GroundStepDeg = MeanStep(bearings, out bool uniform);
            r.ScreenMeanStepDeg = MeanStep(screen, out _);

            if (!uniform)
                throw new InvalidOperationException(
                    "nav-buoy ground-plane bearing does not step uniformly across the 8 facings. " +
                    "That is not a turntable — refusing to name a handedness for it.");

            r.Convention = r.GroundStepDeg < 0 ? AzimuthConvention.Clockwise
                                               : AzimuthConvention.CounterClockwise;

            r.TypeCount = (int)host.EvaluateNumber($"{globalName}.TYPE_IDS.length");

            // ---- the ADR 0031 ring, and the gap this kit ships with -----------------------------
            //
            // ⚠️ This rig exports NO KEYLINE_DEFAULT. IsoPackContract.AssertKeylineGated probes
            // exactly that field on the rig's own global and REFUSES a family without one — the
            // shipyard kit landed in this state and could not bake until #477 added the export
            // upstream. We cannot add it here: the art director's file runs UNMODIFIED (ADR 0021 §5),
            // and patching it is the drift the no-edit rule exists to prevent.
            //
            // So the BEHAVIOUR is asserted instead of the declaration: the default render must be
            // the ringless one, and {keyline:true} must still reach a live ring pass. Both arms are
            // required — "0 ring pixels" would also pass on a renderer that had simply stopped
            // drawing. The missing export is recorded in IMPORT.md as an upstream request.
            r.ExportsKeylineDefault =
                host.EvaluateBool($"typeof {globalName}.KEYLINE_DEFAULT !== 'undefined'");

            int plain = OpaqueCount(host, globalName, keyline: false, outline: false);
            int ringed = OpaqueCount(host, globalName, keyline: true, outline: false);
            int aliased = OpaqueCount(host, globalName, keyline: false, outline: true);

            r.DefaultRenderIsRingless = ringed > plain;

            // ⚠️ {outline:true} — the name #463/#477 and the rest of the repo use — is NOT wired to
            // this rig's ring pass. It is silently ignored and returns the default. An A/B driven
            // with the repo-standard name therefore comes back ringless and looks like a pass.
            r.OutlineIsLiveAlias = aliased == ringed && aliased != plain;

            return r;
        }

        static int OpaqueCount(IRigScriptHost host, string g, bool keyline, bool outline)
        {
            string opts = $"{{size:'s20',wear:'{NavBuoyKit.BakeWear}'" +
                          (keyline ? ",keyline:true" : "") + (outline ? ",outline:true" : "") + "}";
            host.Execute($"globalThis.__nbR = {g}.render('CardinalW',0,{opts});");
            return (int)host.EvaluateNumber(
                "(function(a){var n=0;for(var i=3;i<a.length;i+=4)if(a[i]!==0)n++;return n;})" +
                "(globalThis.__nbR)");
        }

        static double MeanStep(double[] angles, out bool uniform)
        {
            double sum = 0, first = 0;
            uniform = true;
            for (int i = 1; i < angles.Length; i++)
            {
                double d = angles[i] - angles[i - 1];
                while (d > 180) d -= 360;
                while (d <= -180) d += 360;
                if (i == 1) first = d;
                else if (Math.Abs(d - first) > 1e-6) uniform = false;
                sum += d;
            }
            return sum / (angles.Length - 1);
        }

        /// <summary>Measures, then refuses the bake if the catalog's declaration disagrees.</summary>
        public static Result MeasureAndAssert(IRigScriptHost host, AzimuthConvention declared)
        {
            var r = Measure(host);
            if (r.Convention != declared)
                throw new InvalidOperationException(
                    $"nav-buoy measures {r.Convention} ({r.GroundStepDeg:F3}°/dir on the ground plane) " +
                    $"but RigCatalog declares {declared}. Applying the wrong correction MIRRORS all " +
                    "eight cells of every mark — refusing. Correct the catalog, having first " +
                    "confirmed the measurement by eye.");
            return r;
        }
    }
}
