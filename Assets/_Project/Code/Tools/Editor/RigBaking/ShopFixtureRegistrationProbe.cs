using System;
using System.Globalization;
using System.Text;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// What <c>ShopInterior.renderItem()</c> actually does, measured before anything is baked with it.
    ///
    /// <para><b>Why a fixture needs its own probe when <see cref="ShopRegistrationProbe"/> already
    /// measures this kit.</b> That one measures how a floor PLAN registers under its SHELL — two
    /// rigs, one turntable, the door anchors as the common landmark. A fixture has no shell and no
    /// door. It is one object on its own ground point, and the questions that decide whether its
    /// sheet is right are different ones: does the fixture exist at all, does the cell turn the way
    /// the baked-cell frame assumes, and does the picture depend on which of the kit's three rigs
    /// happen to be loaded.</para>
    ///
    /// <para><b>The last one is the interesting answer.</b> The shop kit carries three documented,
    /// silent load-order divergences and <c>RigCatalog.ShopKit.cs</c> declares the dependencies to
    /// keep callers out of them. MEASURED here: <c>renderItem</c> is INVARIANT to all of it.
    /// <c>dims({room:'salesFloor'})</c> really does return the whole 8.00 × 10.00 m shell without
    /// <c>ShopBuilding</c> loaded and the planned 8.00 × 7.09 m room with it — and the counter renders
    /// byte-for-byte the same in both, at every facing, because <c>renderItem</c> never calls
    /// <c>build()</c> and the plan reaches nothing but the shell. That is a fact worth pinning rather
    /// than a coincidence worth relying on: <see cref="MeasureLoadOrderInvariance"/> is what notices
    /// the day a drop makes a fixture read its room.</para>
    /// </summary>
    public static class ShopFixtureRegistrationProbe
    {
        /// <summary>How far up the screen one metre of northward GROUND travel draws — <c>sin 40°</c> at
        /// the shared bake camera. A screen-space angle is not a ground bearing, and the difference is
        /// up to 20°, which is half a cell.</summary>
        public const double GroundDepthScale = ShopRegistrationProbe.GroundDepthScale;

        /// <summary>Degrees of slack on the measured per-cell step. The anchors are a rigid rotation of
        /// an on-floor point, so the true figure is exact; this is rounding margin.</summary>
        public const double StepToleranceDeg = 0.5;

        public sealed class Report
        {
            public int NativeW, NativeH, PixelsPerMetre, Dirs;
            public double PivotX, PivotY;

            /// <summary>Max px by which <c>project(dir,[0,0,0])</c> strays from the rig's declared pivot
            /// over all facings. The fixture is placed at world (0,0), so this being zero is what makes
            /// "the pivot is its ground centre, at every facing" a measurement.</summary>
            public double PivotDeviationPx;

            /// <summary>Signed mean ground-bearing step of the service face per BAKED CELL. Negative is
            /// clockwise on the ground, which is what a counter-clockwise rig baked through
            /// <see cref="RigBaker.DirForCell"/> must produce.</summary>
            public double CellStepDeg;

            public AzimuthConvention MeasuredInCellFrame;

            /// <summary>Whether the service face is on <c>+y</c> in the model at rot 0 — measured from
            /// the projected anchor, not read off the rig's <c>anchors()</c> source.</summary>
            public bool ServiceFaceIsPlusY;

            public string Text;
        }

        // =================================================================================
        //  the two silent fall-throughs
        // =================================================================================

        /// <summary>
        /// True while an unknown fixture name renders a full-size, FULLY TRANSPARENT buffer instead of
        /// throwing.
        ///
        /// <para><c>placeItem</c> opens <c>const P=FIXTURES[name]; if(!P) return;</c>, so a typo'd name
        /// produces a valid sheet of nothing: right dimensions, right sprite count once sliced, no ink
        /// and no error anywhere. <c>ShopFixtureSheetBaker.AssertKnown</c> is the defence and this pins
        /// that the hazard is still there to defend against.</para>
        /// </summary>
        public static bool MeasureSilentUnknownFixture(IRigScriptHost host, string g)
        {
            const string bogus = "notAFixtureInThisKit";
            if (host.EvaluateBool($"{g}.list().indexOf('{bogus}') >= 0")) return false;

            return host.EvaluateBool(
                $"(function(){{var b={g}.renderItem('{bogus}',0,{{type:'generalStore'}});" +
                "if(!b) return false; for(var i=3;i<b.length;i+=4) if(b[i]!==0) return false;" +
                "return b.length>0;})()");
        }

        /// <summary>
        /// True while an unknown TRADE falls through to <c>generalStore</c> silently.
        ///
        /// <para><c>resolve()</c> opens <c>TYPES[tk]||TYPES.generalStore</c>, so a misspelled trade
        /// bakes a real fixture in the WRONG shop's paint under the right file name — a defect every
        /// downstream check survives, because the sheet is valid.</para>
        /// </summary>
        public static bool MeasureSilentUnknownTrade(IRigScriptHost host, string g, string fixture,
                                                     double size)
        {
            const string bogusTrade = "notATradeInThisKit";
            if (host.EvaluateBool($"typeof {g}.TYPES['{bogusTrade}'] === 'object'")) return false;

            string bogus = Render(g, fixture, 0, Opts(bogusTrade, size, ShopFixtureKit.BakedRot));
            string store = Render(g, fixture, 0, Opts("generalStore", size, ShopFixtureKit.BakedRot));
            return host.EvaluateBool($"__sameBytes({bogus},{store})");
        }

        // =================================================================================
        //  rot is redundant with dir
        // =================================================================================

        /// <summary>
        /// The <c>dir</c> a given <c>rot</c> reproduces exactly, or −1 if none does.
        ///
        /// <para>MEASURED on the counter: <c>rot = r</c> at <c>dir 0</c> is byte-identical to
        /// <c>rot = 0</c> at <c>dir = 2r</c>, for all four r. So the sheet needs ONE angular axis, and a
        /// <c>rot</c> column would ship four exact duplicates. <see cref="ShopFixtureKit.BakedRot"/> is
        /// pinned to 0 on the strength of this, and this is what notices if a drop makes rot do
        /// something a camera turn cannot.</para>
        /// </summary>
        public static int MeasureRotEquivalentDir(IRigScriptHost host, string g, string fixture,
                                                  string trade, double size, int rot)
        {
            string rotated = Render(g, fixture, 0, Opts(trade, size, rot));
            for (int dir = 0; dir < 8; dir++)
                if (host.EvaluateBool($"__sameBytes({rotated},{Render(g, fixture, dir, Opts(trade, size, 0))})"))
                    return dir;
            return -1;
        }

        // =================================================================================
        //  load-order invariance
        // =================================================================================

        /// <summary>
        /// Renders a fixture at every facing in <paramref name="host"/> and returns one hash per facing,
        /// so two differently-loaded hosts can be compared without either holding the pixels.
        /// </summary>
        public static string[] FacingHashes(IRigScriptHost host, string g, string fixture,
                                            string trade, double size, int facings,
                                            AzimuthConvention convention)
        {
            var hashes = new string[facings];
            for (int cell = 0; cell < facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, facings, convention);
                byte[] bytes = host.EvaluateBytes(
                    $"{g}.renderItem('{fixture}',{Num(dir)},{Opts(trade, size, ShopFixtureKit.BakedRot)})");
                hashes[cell] = Sha(bytes);
            }
            return hashes;
        }

        /// <summary>
        /// True when a fixture renders identically with the interior rig ALONE and with the whole
        /// commercial block loaded — the claim <see cref="ShopFixtureSheetBaker"/> installs on.
        ///
        /// <para>Costs two fresh hosts, so it belongs in a test rather than in every bake. The
        /// divergence it is looking for is real for <c>dims()</c> — reported in
        /// <paramref name="dimsReport"/> so a green result cannot be mistaken for "the load order does
        /// not diverge", which is false. What is measured is that the divergence does not REACH a
        /// fixture.</para>
        /// </summary>
        public static bool MeasureLoadOrderInvariance(string fixture, string trade, double size,
                                                      int facings, AzimuthConvention convention,
                                                      out string dimsReport)
        {
            string g = ShopFixtureKit.GlobalName;

            using (var alone = RigScriptHostFactory.Create())
            using (var whole = RigScriptHostFactory.Create())
            {
                RigCatalog.InstallModule(alone, RigCatalog.Get(ShopFixtureKit.RigKey));
                // shopBuilding depth-firsts shopInterior and shopfront — the whole block in one host.
                RigCatalog.InstallModule(whole, RigCatalog.Get("shopBuilding"));

                InstallByteCompare(alone);
                InstallByteCompare(whole);

                string room = alone.EvaluateString($"({g}.ROOMS['{trade}']||['floor'])[0]");
                string dimsJs = $"{g}.dims({{type:'{trade}',room:'{room}',size:{Num(size)}}})";
                dimsReport =
                    $"dims(type:{trade}, room:{room}) — interior ALONE: " +
                    $"{alone.EvaluateNumber(dimsJs + ".Wd"):0.00} × {alone.EvaluateNumber(dimsJs + ".Ln"):0.00} m " +
                    $"(planned={alone.EvaluateBool(dimsJs + ".planned")}); WHOLE KIT: " +
                    $"{whole.EvaluateNumber(dimsJs + ".Wd"):0.00} × {whole.EvaluateNumber(dimsJs + ".Ln"):0.00} m " +
                    $"(planned={whole.EvaluateBool(dimsJs + ".planned")})";

                var a = FacingHashes(alone, g, fixture, trade, size, facings, convention);
                var b = FacingHashes(whole, g, fixture, trade, size, facings, convention);

                for (int i = 0; i < facings; i++)
                    if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
                return true;
            }
        }

        // =================================================================================
        //  the measurement the bake gates on
        // =================================================================================

        /// <summary>
        /// Measures the pivot and the direction the BAKED CELLS turn, from the fixture's own service
        /// face. Throws with the full working rather than returning a number nobody checked.
        ///
        /// <para>Handedness is measured in the <b>baked-cell frame</b>, not the rig's <c>dir</c> frame,
        /// and that distinction is this kit's own recorded scar: two rigs that agree perfectly on their
        /// turntable still produce mirrored SHEETS if their bakers apply different corrections, and this
        /// kit shipped exactly that on its first bake. <see cref="BuildingFacing"/> aims a fixture on
        /// the assumption that the model turns −45° per CELL; this is the measurement that entitles it
        /// to.</para>
        /// </summary>
        public static Report Measure(IRigScriptHost host, string g, string fixture, string trade,
                                     double size, int facings, AzimuthConvention convention)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SHOP FIXTURE REGISTRATION PROBE");

            var r = new Report
            {
                NativeW = (int)host.EvaluateNumber($"{g}.W"),
                NativeH = (int)host.EvaluateNumber($"{g}.H"),
                PixelsPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                Dirs = (int)host.EvaluateNumber($"{g}.DIRS"),
                PivotX = host.EvaluateNumber($"{g}.pivot.x"),
                PivotY = host.EvaluateNumber($"{g}.pivot.y"),
            };

            sb.AppendLine($"  native cell     : {r.NativeW} × {r.NativeH} px, pivot ({r.PivotX}, {r.PivotY})");
            sb.AppendLine($"  scale / facings : {r.PixelsPerMetre} px per metre, rig declares {r.Dirs} dirs");

            if (r.PixelsPerMetre != ShopFixtureKit.PixelsPerUnit)
                throw new InvalidOperationException(
                    $"The rig draws at {r.PixelsPerMetre} px per metre but the kit locks its import at " +
                    $"{ShopFixtureKit.PixelsPerUnit}. Every fixture would stand at the wrong size beside " +
                    "the buildings it belongs to, and nothing would report it.");

            if (r.Dirs != facings)
                throw new InvalidOperationException(
                    $"The rig declares {r.Dirs} facings and this kit bakes {facings}. Baking fewer is a " +
                    "decision somebody has to take on purpose (the owner ruled eight on 2026-08-05); " +
                    "baking more writes duplicate cells.");

            // ---- 1. the pivot is the projected world origin, at every facing -------------------------
            for (int cell = 0; cell < facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, facings, convention);
                double px = host.EvaluateNumber($"{g}.project({Num(dir)},[0,0,0],{Num(ShopFixtureKit.Elevation)}).x");
                double py = host.EvaluateNumber($"{g}.project({Num(dir)},[0,0,0],{Num(ShopFixtureKit.Elevation)}).y");
                r.PivotDeviationPx = Math.Max(r.PivotDeviationPx,
                                              Math.Max(Math.Abs(px - r.PivotX), Math.Abs(py - r.PivotY)));
            }
            sb.AppendLine($"  pivot           : project(dir,[0,0,0]) strays {r.PivotDeviationPx:F4} px from " +
                          "the rig pivot over every facing — renderItem places a fixture at world (0,0), " +
                          "so the pivot IS its ground centre");

            if (r.PivotDeviationPx > 1e-6)
                throw new InvalidOperationException(
                    $"The projected world origin moves {r.PivotDeviationPx:F4} px between facings. The " +
                    "whole family pins ONE pivot for all eight cells; if the origin moves, every cell " +
                    "needs its own and the contract's single pivot is a lie.");

            // ---- 2. which way do the BAKED CELLS turn? ----------------------------------------------
            double depth = host.EvaluateNumber($"{g}.fixtureFoot('{fixture}',0).d");
            var bearing = new double[facings];
            for (int cell = 0; cell < facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, facings, convention);
                string p = $"{g}.project({Num(dir)},[0,{Num(depth / 2.0)},0],{Num(ShopFixtureKit.Elevation)})";
                double dx = host.EvaluateNumber($"{p}.x") - r.PivotX;
                // ⚠️ Un-squash before taking any angle: screen y is up-negative and the ground plane is
                // squashed by sin(elev), so atan2 on raw px is not a bearing.
                double dy = -(host.EvaluateNumber($"{p}.y") - r.PivotY) / GroundDepthScale;
                bearing[cell] = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                if (cell == 0) r.ServiceFaceIsPlusY = host.EvaluateNumber($"{p}.y") < r.PivotY;
            }

            double total = 0;
            for (int cell = 0; cell < facings; cell++)
                total += Delta(bearing[cell], bearing[(cell + 1) % facings]);
            r.CellStepDeg = total / facings;
            r.MeasuredInCellFrame = r.CellStepDeg < 0 ? AzimuthConvention.CounterClockwise
                                                      : AzimuthConvention.Clockwise;

            sb.AppendLine($"  service face    : model {(r.ServiceFaceIsPlusY ? "+y" : "−y")} at cell 0 " +
                          $"(front face centre {depth / 2.0:F3} m out, projected {(r.ServiceFaceIsPlusY ? "above" : "below")} the pivot)");
            sb.AppendLine($"  cell rotation   : {r.CellStepDeg:+0.0;-0.0}° per BAKED CELL " +
                          $"({total:+0.0;-0.0}° over a full turn) — " +
                          $"{(r.CellStepDeg < 0 ? "clockwise" : "counter-clockwise")} on the ground");

            if (Math.Abs(Math.Abs(total) - 360.0) > 1.0)
                throw new InvalidOperationException(
                    $"The service face sweeps {total:F1}° over {facings} cells, not one full turn.\n\n" + sb +
                    "\nIt is not a rigid rotation about the pivot, so its sense is not measurable and " +
                    "nothing may be inferred from it.");

            double expected = -360.0 / facings;
            if (Math.Abs(r.CellStepDeg - expected) > StepToleranceDeg)
                throw new InvalidOperationException(
                    $"The baked cells step {r.CellStepDeg:+0.0;-0.0}° each, not the {expected:+0.0;-0.0}° " +
                    $"BuildingFacing assumes.\n\n" + sb +
                    "\nBuildingFacing.DoorBearings derives its table as `atZero − 360/facings × cell`, " +
                    "which is right only while a rising cell index turns the model the other way — the " +
                    "correction RigBaker.DirForCell applies for a counter-clockwise rig. Aiming a " +
                    "fixture off a table with the wrong sign is the defect #495 found in the village " +
                    "kit, where the schoolhouse door came out 92° from the green it faces.");

            if (!r.ServiceFaceIsPlusY)
                throw new InvalidOperationException(
                    "The fixture's service face is on −y at cell 0.\n\n" + sb +
                    "\nThe rig's anchors() puts the customer queue at +dv and the keeper's station at " +
                    "−dv, so rot 0 faces +y. A fixture that does not would need its own sign carried " +
                    "into the contract, which is the trap the house/room pair records — refusing rather " +
                    "than aiming every counter backwards.");

            r.Text = sb.ToString();
            return r;
        }

        // =================================================================================
        //  shared JS
        // =================================================================================

        /// <summary>
        /// The render options, spelled out in full on every call.
        ///
        /// <para>MEASURED, the knobs that move a fixture's pixels are <c>type</c>, <c>size</c>,
        /// <c>stock</c>, <c>weather</c>, <c>night</c>, <c>seed</c>, <c>rot</c> and <c>elev</c> — so all
        /// eight are passed. Unlike the fuel rig, omitting one here is not live sabotage: this rig has
        /// no geometry cache, omission equals the documented default, and two identical calls return
        /// identical bytes in any order (all measured). Passing them anyway is what keeps a re-drop that
        /// moves a default visible in the diff instead of in the art.</para>
        /// </summary>
        public static string Opts(string trade, double size, int rot) =>
            $"{{type:'{trade}',size:{Num(size)},stock:{Num(ShopFixtureKit.BakedStock)}," +
            $"weather:{Num(ShopFixtureKit.BakedWeather)}," +
            $"night:{(ShopFixtureKit.BakedNight ? "true" : "false")}," +
            $"seed:{ShopFixtureKit.BakedSeed},rot:{rot},elev:{Num(ShopFixtureKit.Elevation)}}}";

        static string Render(string g, string fixture, int dir, string opts) =>
            $"{g}.renderItem('{fixture}',{dir},{opts})";

        /// <summary>Installs a byte-compare helper so two renders can be compared inside the engine
        /// rather than marshalled across the host boundary twice.</summary>
        public static void InstallByteCompare(IRigScriptHost host) =>
            host.Execute(
                "globalThis.__sameBytes=function(a,b){if(!a||!b||a.length!==b.length)return false;" +
                "for(var i=0;i<a.length;i++) if(a[i]!==b[i]) return false; return true;};");

        static string Sha(byte[] bytes)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }

        /// <summary>Signed shortest angular difference b − a, in degrees.</summary>
        static double Delta(double a, double b)
        {
            double d = (b - a) % 360.0;
            if (d > 180.0) d -= 360.0;
            if (d < -180.0) d += 360.0;
            return d;
        }

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
