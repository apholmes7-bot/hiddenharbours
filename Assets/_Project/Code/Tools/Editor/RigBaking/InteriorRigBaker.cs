using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// What one interior bake was asked to draw. Two shapes, because the interior kit has two rigs and
    /// they do NOT share a call signature: the room is <c>InteriorIso.render(dir, opts)</c> like every
    /// building rig, the furniture is <c>PropIso.render(name, dir, opts)</c>.
    /// </summary>
    public readonly struct InteriorBakeRequest
    {
        /// <summary>Catalog rig key — <c>"interior"</c> or <c>"interiorProp"</c>.</summary>
        public readonly string RigKey;

        /// <summary>The JS options literal handed to <c>render()</c>, e.g.
        /// <c>Object.assign({},InteriorIso.PRESETS['cottageBedroom'])</c>.</summary>
        public readonly string OptsJs;

        /// <summary>For the PROP rig only: which prop. Null for a room.</summary>
        public readonly string PropName;

        public readonly string Label;
        public readonly string OutputFolder;
        public readonly string BaseName;
        public readonly int Facings;

        /// <summary>
        /// The cap the grid is solved against — which MUST be the cap the consumer imports at. Over it
        /// Unity downscales silently and the sprite COUNT still comes out right, so nothing but a cell
        /// or pivot assert catches it. <c>InteriorKit.ImportSizeCap</c> is the number, and it is passed
        /// in rather than read here so the baker has no opinion about any one kit.
        /// </summary>
        public readonly int MaxSheetDimension;

        /// <summary>
        /// Assert the options actually changed the render. On for every build in this kit: both rigs
        /// resolve options as <c>opts[k] != null ? opts[k] : fallback</c>, so a misspelled KEY is
        /// ignored in total silence — the recorded example is <c>winD</c> for <c>winDensity</c>.
        /// </summary>
        public readonly bool RequireDistinctFromDefault;

        InteriorBakeRequest(string rigKey, string optsJs, string propName, string label,
                            string outputFolder, string baseName, int facings,
                            int maxSheetDimension, bool requireDistinctFromDefault)
        {
            RigKey = rigKey; OptsJs = optsJs; PropName = propName; Label = label;
            OutputFolder = outputFolder; BaseName = baseName; Facings = facings;
            MaxSheetDimension = maxSheetDimension;
            RequireDistinctFromDefault = requireDistinctFromDefault;
        }

        public bool IsProp => PropName != null;

        /// <summary>A ROOM build: <c>InteriorIso.render(dir, opts)</c>.</summary>
        public static InteriorBakeRequest Room(string optsJs, string label, string outputFolder,
                                               string baseName, int facings, int maxSheetDimension,
                                               bool requireDistinctFromDefault = true) =>
            new InteriorBakeRequest("interior", optsJs, null, label, outputFolder, baseName, facings,
                                    maxSheetDimension, requireDistinctFromDefault);

        /// <summary>
        /// A ROOM build from one of the rig's own PRESETS.
        ///
        /// <para>⚠️ SPREAD the table, never pass <c>{preset:'name'}</c>. No building rig in this repo
        /// reads a <c>preset</c> key — <c>resolve()</c> would fall straight through to the default and
        /// bake seven identical rooms under seven names, in silence. (The interior rig happens to read
        /// <c>opts.preset</c>, but spelling it the same way as every sibling is what keeps the trap
        /// from coming back the next time a rig is re-dropped.)</para>
        /// </summary>
        public static InteriorBakeRequest RoomPreset(string preset, string globalName,
                                                     string outputFolder, string baseName, int facings,
                                                     int maxSheetDimension) =>
            Room($"Object.assign({{}},{globalName}.PRESETS['{preset}'])", preset, outputFolder,
                 baseName, facings, maxSheetDimension);

        /// <summary>A PROP build: <c>PropIso.render(name, dir, opts)</c>.</summary>
        public static InteriorBakeRequest Prop(string propName, string optsJs, string label,
                                               string outputFolder, string baseName, int facings,
                                               int maxSheetDimension,
                                               bool requireDistinctFromDefault = false) =>
            new InteriorBakeRequest("interiorProp", optsJs, propName, label, outputFolder, baseName,
                                    facings, maxSheetDimension, requireDistinctFromDefault);

        /// <summary>The exact JS expression that renders one facing. The ONE place the two rigs'
        /// differing call signatures are expressed, so nothing downstream has to know which it is.</summary>
        public string RenderJs(string globalName, double dir) =>
            IsProp
                ? $"{globalName}.render('{Escape(PropName)}',{Num(dir)},{OptsJs})"
                : $"{globalName}.render({Num(dir)},{OptsJs})";

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("'", "\\'") ?? "";
    }

    /// <summary>Everything one interior bake produced. Every number came from the rig.</summary>
    public sealed class InteriorBakeResult
    {
        public string RigKey, Label, PropName, AssetPath, EngineName;
        public AzimuthConvention Convention;

        /// <summary>The rig's native cell, before the union crop.</summary>
        public int NativeCellWidth, NativeCellHeight;

        /// <summary>The cropped cell every facing is packed at.</summary>
        public int CellWidth, CellHeight;

        /// <summary>Crop origin in the native cell (top-left space) — what the pivot shifted by.</summary>
        public int CropX, CropY;

        /// <summary>Pivot in the CROPPED cell, top-left origin px — the FLOOR CENTRE for a room, the
        /// footprint's floor centre for a prop. The same convention in both, which is why a prop drops
        /// onto a room floor point with no offset maths.</summary>
        public double PivotX, PivotY;

        public int Columns, Rows, Facings;
        public int SheetWidth, SheetHeight;
        public long PngBytes;
        public double RenderMilliseconds, TotalMilliseconds;

        /// <summary>The rig's own <c>PX</c> (pixels per metre) — read from the rig, never assumed.</summary>
        public int PixelsPerMetre;

        /// <summary>ROOM: the footprint the rig resolves, in metres. Zero for a prop, which reports its
        /// extent through <see cref="PropFootprintWidth"/>/<see cref="PropFootprintDepth"/> instead.</summary>
        public double FootprintWidthMetres, FootprintLengthMetres;

        /// <summary>PROP: <c>footprint(name,opts)</c> in metres — what a placer collides and spaces by.</summary>
        public double PropFootprintWidth, PropFootprintDepth;

        /// <summary>ROOM, per facing, in CROPPED cell px: the doorway (the threshold), the floor centre,
        /// the hearth (or NaN when the build has none) and the two lamp points. Parallel arrays because
        /// <c>JsonUtility</c> will not round-trip a nested array of objects.</summary>
        public double[] DoorX, DoorY, FloorX, FloorY, HearthX, HearthY;

        public double CropSaving =>
            NativeCellWidth * NativeCellHeight == 0
                ? 0
                : 1.0 - (double)CellWidth * CellHeight / ((double)NativeCellWidth * NativeCellHeight);

        public long RuntimeBytesRgba32 => (long)SheetWidth * SheetHeight * 4;

        public long UncroppedBytesRgba32 =>
            (long)NativeCellWidth * NativeCellHeight * 4 * Facings;
    }

    /// <summary>Every facing rendered once, plus the union crop — the bake without the file writing, so
    /// a test can measure the real crop headless.</summary>
    public sealed class InteriorCellSet
    {
        public string RigKey;
        public RigGeometry Geometry;
        public AzimuthConvention Convention;
        public int PixelsPerMetre;
        public byte[][] Cells;
        public int CropX, CropY, CellWidth, CellHeight;
        public double PivotX, PivotY;
        public double RenderMilliseconds;
    }

    /// <summary>
    /// Bakes the INTERIOR kit — the rooms out of <c>interiorIsoRig.js</c> and the furniture out of
    /// <c>interiorPropRig.js</c> — to cropped, packed turntable sheets.
    ///
    /// <para><b>Why this is not <see cref="BuildingRigBaker"/> with another rig key.</b> Three things
    /// differ, and each of them is silent if papered over:</para>
    /// <list type="bullet">
    ///   <item><b>The convention probe.</b> <see cref="BuildingRigAzimuthProbe"/> reads which SIDE the
    ///   door lands on and assumes the door is on the <c>+Y</c> gable. The room's is on <c>−Y</c>, so
    ///   that probe returns <c>Clockwise</c> — confidently, with a report — and the bake would apply the
    ///   opposite correction to every cell. <see cref="InteriorRigAzimuthProbe"/> measures the gable
    ///   first. This is exactly the "rotation direction is per-artwork DATA" trap, hit on the first
    ///   rig of a new kit.</item>
    ///   <item><b>The sidecar's anchor set.</b> That baker writes <c>ridge</c> and
    ///   <c>chimneys</c>/<c>stacks</c>; a room has neither, and asking for <c>.ridge.x</c> throws. A
    ///   room has a doorway, a floor centre, a hearth and lamps.</item>
    ///   <item><b>The render signature.</b> <c>PropIso.render(name, dir, opts)</c> takes three
    ///   arguments. <see cref="InteriorBakeRequest.RenderJs"/> is the one place that lives.</item>
    /// </list>
    ///
    /// <para>Everything the two bakers genuinely share is CALLED, not copied: the union crop
    /// (<see cref="BuildingRigBaker.UnionAlphaBounds"/>), the grid solver
    /// (<see cref="BuildingRigBaker.ChooseGrid"/>), the alpha bounds
    /// (<see cref="BuildingRigAzimuthProbe.AlphaBounds"/>) and the top-left → bottom-origin row flip
    /// (<see cref="BuildingRigBaker.BlitCropped"/>).</para>
    /// </summary>
    public static class InteriorRigBaker
    {
        /// <summary>Transparent margin kept around the art — one pixel, same as the building baker, so
        /// the keyline never touches a cell edge.</summary>
        public const int Padding = BuildingRigBaker.Padding;

        /// <summary>
        /// Render every facing, measure the convention and compute the union crop. No files, no
        /// <c>Texture2D</c> — so an EditMode test can measure the REAL cropped cell of every build on a
        /// machine with no graphics device, which is how this kit's texture budget gets checked before
        /// anything is committed rather than after.
        ///
        /// <para><paramref name="host"/> is caller-owned. The rig is (re)executed into it here.</para>
        /// </summary>
        public static InteriorCellSet RenderCells(InteriorBakeRequest req, IRigScriptHost host)
        {
            var entry = RigCatalog.Get(req.RigKey);
            RigGeometry geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            if (req.IsProp) AssertPropNameIsReal(host, req, g);
            AssertRenders(host, req, g, geo.Width, geo.Height);
            if (req.RequireDistinctFromDefault) AssertDiffersFromDefault(host, req, g);

            AzimuthConvention convention = MeasureConvention(host, req, entry, g);

            var clock = new Stopwatch();
            var cells = new byte[req.Facings][];
            for (int cell = 0; cell < req.Facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, req.Facings, convention);
                clock.Start();
                cells[cell] = host.EvaluateBytes(req.RenderJs(g, dir));
                clock.Stop();

                if (cells[cell].Length != geo.Width * geo.Height * 4)
                    throw new InvalidOperationException(
                        $"Cell {cell} of '{req.Label}' came back {cells[cell].Length} bytes, expected " +
                        $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
            }

            BuildingRigBaker.UnionAlphaBounds(cells, geo.Width, geo.Height,
                                              out int xMin, out int yMin, out int xMax, out int yMax);
            if (xMax < xMin)
                throw new InvalidOperationException(
                    $"Every facing of '{req.Label}' rendered fully transparent. The options resolved but " +
                    "drew no pixels — check the build names a real prop / a real preset.");

            xMin = Mathf.Max(0, xMin - Padding);
            yMin = Mathf.Max(0, yMin - Padding);
            xMax = Mathf.Min(geo.Width - 1, xMax + Padding);
            yMax = Mathf.Min(geo.Height - 1, yMax + Padding);

            // THE PIVOT MOVES WITH THE CROP. The rig reports it in native-cell top-left space; after
            // cropping, the same world point sits (cropX, cropY) closer to the origin. Getting this
            // wrong throws nothing — every room simply stands a fixed distance from where it was
            // placed, which reads as a placement bug in some other system.
            return new InteriorCellSet
            {
                RigKey = req.RigKey,
                Geometry = geo,
                Convention = convention,
                PixelsPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                Cells = cells,
                CropX = xMin,
                CropY = yMin,
                CellWidth = xMax - xMin + 1,
                CellHeight = yMax - yMin + 1,
                PivotX = geo.PivotX - xMin,
                PivotY = geo.PivotY - yMin,
                RenderMilliseconds = clock.Elapsed.TotalMilliseconds,
            };
        }

        /// <summary>Render, crop, pack and write the PNG. Returns everything a contract needs.</summary>
        public static InteriorBakeResult Bake(InteriorBakeRequest req)
        {
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InteriorCellSet set = RenderCells(req, host);
            string g = RigCatalog.Get(req.RigKey).GlobalName;

            var r = new InteriorBakeResult
            {
                RigKey = req.RigKey,
                Label = req.Label,
                PropName = req.PropName,
                EngineName = host.EngineName,
                Convention = set.Convention,
                NativeCellWidth = set.Geometry.Width,
                NativeCellHeight = set.Geometry.Height,
                Facings = req.Facings,
                PixelsPerMetre = set.PixelsPerMetre,
                CropX = set.CropX,
                CropY = set.CropY,
                CellWidth = set.CellWidth,
                CellHeight = set.CellHeight,
                PivotX = set.PivotX,
                PivotY = set.PivotY,
            };

            ReadAnchors(host, req, g, r);

            BuildingRigBaker.ChooseGrid(r.CellWidth, r.CellHeight, req.Facings,
                                        out int cols, out int rows, req.MaxSheetDimension);
            r.Columns = cols; r.Rows = rows;

            int pw = cols * r.CellWidth, ph = rows * r.CellHeight;
            r.SheetWidth = pw; r.SheetHeight = ph;

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < req.Facings; cell++)
                BuildingRigBaker.BlitCropped(set.Cells[cell], set.Geometry.Width, set.Geometry.Height,
                                             set.CropX, set.CropY, r.CellWidth, r.CellHeight,
                                             pixels, pw, ph, col: cell % cols, rowFromTop: cell / cols);

            r.AssetPath = $"{req.OutputFolder}/{req.BaseName}.png";
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, req.OutputFolder));

            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, r.AssetPath), png);
                r.PngBytes = png.Length;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }

            r.RenderMilliseconds = set.RenderMilliseconds;
            total.Stop();
            r.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return r;
        }

        // =================================================================================
        //  the convention — MEASURED, and by a different measurement per rig shape
        // =================================================================================

        /// <summary>
        /// Measure which way this rig's turntable runs, and refuse on a mismatch with the catalog's
        /// declaration — the same refusal the building bake makes, for the same reason.
        ///
        /// <para>The ROOM is measured against its EXTERIOR: <see cref="InteriorRigAzimuthProbe"/> proves
        /// the two rigs project identically at all eight facings and then finds the one facing offset
        /// that lines their doorways up. A rig that turned the other way admits no such offset, so that
        /// single measurement is the handedness proof and the registration number at once.</para>
        ///
        /// <para>A PROP is measured against the ROOM the same way, minus the door: a prop has no
        /// doorway, so the projection identity IS the measurement. It is a strong one — <c>project()</c>
        /// is the arithmetic <c>render()</c> draws with, evaluated per facing, not a claim about it.</para>
        /// </summary>
        static AzimuthConvention MeasureConvention(IRigScriptHost host, in InteriorBakeRequest req,
                                                   in RigEntry entry, string g)
        {
            var reference = RigCatalog.Get(req.IsProp ? "interior" : "house");
            RigCatalog.Install(host, reference);

            if (!InteriorRigAzimuthProbe.ProjectionsAgree(host, reference.GlobalName, g,
                                                          out string report))
                throw new InvalidOperationException(
                    $"INTERIOR BAKE REFUSED for '{req.Label}': {report}\n\n" +
                    "The interior kit's whole premise is that a room, its furniture and the shell over " +
                    "them stand on ONE turntable. Refusing rather than baking a set that turns apart.");

            if (!req.IsProp)
            {
                // The room additionally has to line up with its shell, which is a stronger claim than
                // sharing a turntable and is the one the pilot actually rests on.
                InteriorRigAzimuthProbe.MeasureRegistration(
                    host, reference.GlobalName, ExteriorOptsFor(req), g, req.OptsJs, req.Facings);
            }

            // Same turntable as the reference rig ⇒ same convention. The reference's own convention is
            // the catalog's, which the building bake measures independently from its door anchor.
            AzimuthConvention measured = reference.DeclaredConvention;

            if (measured != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"AZIMUTH MISMATCH on rig '{req.RigKey}'.\n" +
                    $"  the catalog declares : {entry.DeclaredConvention}\n" +
                    $"  the measurement says : {measured}\n\n{report}\n\n" +
                    "Refusing rather than guessing — a silent guess here has shipped a mislabelled " +
                    "facing set in five kits.");

            return measured;
        }

        /// <summary>
        /// The EXTERIOR options a room is registered against: the same <c>size</c>, and nothing else.
        ///
        /// <para><c>size</c> is the ONLY axis both rigs share (<c>Wd = 6 + size·2.4</c>,
        /// <c>Ln = 7 + size·4.2</c> in both), and the registration probe asserts that footprint
        /// agreement rather than trusting it. Siding, roof and porch have no interior meaning and are
        /// deliberately not carried across.</para>
        /// </summary>
        static string ExteriorOptsFor(in InteriorBakeRequest req) =>
            $"{{size:({req.OptsJs}).size!=null?({req.OptsJs}).size:0.3}}";

        // =================================================================================
        //  the anchors
        // =================================================================================

        /// <summary>
        /// Read the rig's per-facing anchors into the result, in CROPPED cell px so a runtime overlay
        /// (a lamp flicker, a hearth fire) or a door trigger lands without re-deriving the crop.
        ///
        /// <para>A build with no hearth reports <see cref="float.NaN"/> rather than <c>0</c>: zero is a
        /// legal cell coordinate, so a missing hearth written as zero would put a fire in the corner of
        /// the room.</para>
        /// </summary>
        static void ReadAnchors(IRigScriptHost host, in InteriorBakeRequest req, string g,
                                InteriorBakeResult r)
        {
            if (req.IsProp)
            {
                string name = req.PropName.Replace("\\", "\\\\").Replace("'", "\\'");
                r.PropFootprintWidth = host.EvaluateNumber($"{g}.footprint('{name}',{req.OptsJs}).w");
                r.PropFootprintDepth = host.EvaluateNumber($"{g}.footprint('{name}',{req.OptsJs}).d");
                r.DoorX = Array.Empty<double>();
                r.DoorY = Array.Empty<double>();
                r.FloorX = Array.Empty<double>();
                r.FloorY = Array.Empty<double>();
                r.HearthX = Array.Empty<double>();
                r.HearthY = Array.Empty<double>();
                return;
            }

            r.FootprintWidthMetres = host.EvaluateNumber($"{g}.anchors(0,{req.OptsJs}).Wd");
            r.FootprintLengthMetres = host.EvaluateNumber($"{g}.anchors(0,{req.OptsJs}).Ln");

            int n = req.Facings;
            r.DoorX = new double[n]; r.DoorY = new double[n];
            r.FloorX = new double[n]; r.FloorY = new double[n];
            r.HearthX = new double[n]; r.HearthY = new double[n];

            for (int cell = 0; cell < n; cell++)
            {
                double dir = RigBaker.DirForCell(cell, n, r.Convention);
                string a = $"{g}.anchors({dir.ToString("R", CultureInfo.InvariantCulture)},{req.OptsJs})";

                r.DoorX[cell] = host.EvaluateNumber($"{a}.door.x") - r.CropX;
                r.DoorY[cell] = host.EvaluateNumber($"{a}.door.y") - r.CropY;
                r.FloorX[cell] = host.EvaluateNumber($"{a}.floor.x") - r.CropX;
                r.FloorY[cell] = host.EvaluateNumber($"{a}.floor.y") - r.CropY;

                if (host.EvaluateBool($"{a}.hearth !== null && {a}.hearth !== undefined"))
                {
                    r.HearthX[cell] = host.EvaluateNumber($"{a}.hearth.x") - r.CropX;
                    r.HearthY[cell] = host.EvaluateNumber($"{a}.hearth.y") - r.CropY;
                }
                else
                {
                    r.HearthX[cell] = double.NaN;
                    r.HearthY[cell] = double.NaN;
                }
            }
        }

        // =================================================================================
        //  the guards
        // =================================================================================

        /// <summary>
        /// The PROP rig's own silent failure, and it is a different one from the room's.
        ///
        /// <para><c>PropIso</c> resolves a prop as <c>PROPS[name] || PROPS.chair</c>, so a misspelled
        /// or renamed prop bakes a CHAIR under the dresser's name, at the dresser's stem, into the
        /// dresser's contract entry — with the right cell, the right pivot and the right facing count.
        /// Nothing throws and nothing looks broken until somebody notices the cottage has five chairs
        /// in it.</para>
        /// </summary>
        static void AssertPropNameIsReal(IRigScriptHost host, in InteriorBakeRequest req, string g)
        {
            string name = req.PropName.Replace("\\", "\\\\").Replace("'", "\\'");
            if (host.EvaluateBool($"{g}.PROPS['{name}'] !== undefined")) return;

            string known = host.EvaluateString($"{g}.list().join(', ')");
            throw new ArgumentException(
                $"'{req.PropName}' is not a prop of {g}. Known: {known}.\n\n" +
                "Refusing rather than baking: this rig resolves an unknown name to PROPS.chair, so the " +
                "bake would have written a chair under this prop's stem, with the right cell and pivot " +
                "and nothing to see in the log.");
        }

        static void AssertRenders(IRigScriptHost host, in InteriorBakeRequest req, string g,
                                  int width, int height)
        {
            byte[] rgba = host.EvaluateBytes(req.RenderJs(g, 0));
            if (rgba.Length != width * height * 4)
                throw new InvalidOperationException(
                    $"Build '{req.Label}' rendered {rgba.Length} bytes, expected {width * height * 4} " +
                    $"for {width}×{height} RGBA. The rig's declared cell and what it draws disagree, " +
                    "so the crop and the pivot would both be measured against the wrong frame.");
        }

        /// <summary>
        /// Assert the options actually CHANGED the render.
        ///
        /// <para>This is the silent-option tripwire, and it exists because of one already-made mistake
        /// that costs nothing to repeat: these rigs read <c>opts[k] != null ? opts[k] : fallback</c>, so
        /// a MISSPELLED KEY is ignored without a word. Nothing throws; you get a folder of identical
        /// rooms under different names and the only way to notice is to look at all of them at once.
        /// The worked example on record is <c>winD</c> (the rig's internal field) versus
        /// <c>winDensity</c> (the option it actually reads).</para>
        ///
        /// <para>A byte comparison against the rig default catches the whole-options-ignored case, which
        /// is the one with teeth. It is not proof that every key applied.</para>
        /// </summary>
        static void AssertDiffersFromDefault(IRigScriptHost host, in InteriorBakeRequest req, string g)
        {
            byte[] dialled = host.EvaluateBytes(req.RenderJs(g, 0));
            byte[] plain = host.EvaluateBytes(
                req.IsProp
                    ? $"{g}.render('{req.PropName}',0,{{}})"
                    : $"{g}.render(0,{{}})");

            if (dialled.Length != plain.Length) return;
            for (int i = 0; i < dialled.Length; i++) if (dialled[i] != plain[i]) return;

            throw new InvalidOperationException(
                $"Build '{req.Label}' rendered byte-identical to the rig's DEFAULT.\n\n" +
                "Every option was therefore ignored. These rigs resolve options as " +
                "opts[k] != null ? opts[k] : fallback, so an unknown KEY is accepted in silence — the " +
                "recorded worked example is winD (the rig's internal field) versus winDensity (the " +
                "option it reads). Every key this kit dials is listed in InteriorKit.RoomOptionKeys / " +
                "PropOptionKeys, and InteriorKitTests greps each of them out of the rig source — check " +
                "the build's keys against those.");
        }

        /// <summary>One line per build for the bake report.</summary>
        public static string Describe(InteriorBakeResult r) =>
            $"  ✓ {r.Label,-18} {r.CellWidth,4}×{r.CellHeight,-4} cell ({r.Columns}×{r.Rows}) → " +
            $"{r.SheetWidth}×{r.SheetHeight}, {r.PngBytes / 1024.0:F0} KB png, " +
            $"crop saved {r.CropSaving:P0}, pivot ({r.PivotX:F0},{r.PivotY:F0}), " +
            (r.PropName != null
                ? $"{r.PropFootprintWidth:F2}×{r.PropFootprintDepth:F2} m footprint"
                : $"{r.FootprintWidthMetres:F1}×{r.FootprintLengthMetres:F1} m room");

        /// <summary>Total texture cost of a set, in MiB of RGBA32 — reported, never presumed.</summary>
        public static double TotalRuntimeMib(IEnumerable<InteriorBakeResult> results)
        {
            long bytes = 0;
            foreach (var r in results) bytes += r.RuntimeBytesRgba32;
            return bytes / 1024.0 / 1024.0;
        }
    }
}
