using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the SEAGULL — the fleet's first CREATURE rig (owner drop of 2026-09-10) — from
    /// <c>docs/art/rigs/seagullIsoRig.js</c> into one sheet:
    /// <c>Assets/_Project/Art/Sprites/Creatures/Seagull.png</c>, 48 columns × 8 direction rows of
    /// 64×64, plus the contract JSON beside it that <c>SeagullSheetSlicer</c> slices to.
    ///
    /// <para>Fourth sibling of <see cref="RigBaker"/> (boat turntable), <see cref="CharacterRigBaker"/>
    /// (8-dir anim rows) and <see cref="ShovelKitBaker"/> (a prop posed by a second rig). Separate
    /// for the reason those three are separate: the shape of the request differs. A creature's sheet
    /// is ONE page carrying EVERY state — the rig's own <c>sheetOrder()</c> concatenates all 13
    /// animations end to end into a single 48-column row per facing — where the character bakes one
    /// sheet per animation. That single page is also what makes this kit need its own writer; see
    /// <see cref="SizeCap"/>.</para>
    ///
    /// <para><b>⚠️ THE PIVOT IS A CONTACT POINT, NOT A WATERLINE.</b> (32,46) is the ground under a
    /// standing, walking or perched bird, the water surface under a floating one, and the point
    /// directly below an airborne one. <b>Altitude is a SCREEN OFFSET, never a sprite scale</b>
    /// (owner, 2026-09-09): strict world scale at every altitude, 32 px = 1 m, a 1.40 m span is
    /// 45 px whether the gull is on the wharf or 25 m up, because there are no giant gulls crossing
    /// the camera. Nothing in this bake varies with altitude — every cell is the bird at true scale
    /// and the runtime blits it at <c>pivot − altitude·cos(40°)·32·zoom</c> with its shadow left at
    /// the pivot. If a "bigger when closer" knob ever appears downstream of this sheet, it is a
    /// defect.</para>
    ///
    /// <para><b>Three silent fallbacks this bake is built around</b>, each probed in the standalone
    /// V8 harness at intake — every one of them renders plausible art rather than throwing:</para>
    /// <list type="number">
    /// <item><c>render(dir, …)</c> takes a facing INDEX. Passing the compass STRING the READMEs talk
    /// in ('N', 'NE', …) returns a fully TRANSPARENT cell — 0 opaque px — and throws nothing. A
    /// blank-vs-blank comparison then reads as "0 cells differ", a false green, which is why
    /// <see cref="MinOpaquePixelsPerCell"/> exists.</item>
    /// <item>An animation name the rig does not know falls back to <c>stand</c> IN SILENCE. A typo
    /// in a state id therefore bakes 48 columns of a standing bird, all non-blank, all wrong. So
    /// the column list is asked of <c>sheetOrder()</c> and never named here.</item>
    /// <item><c>sheetOrder()</c> yields <c>{anim, f}</c> but <c>render</c> reads the frame from
    /// <c>frame</c>. Passing the record through unmapped renders frame 0 of every column — 48
    /// distinct-looking cells that are all the same pose. <see cref="Column"/> does the mapping and
    /// the adjacent-frame control below asserts it took.</item>
    /// </list>
    ///
    /// <para><b>The four water states are baked WET, deliberately.</b> <c>splash</c>, <c>float</c>,
    /// <c>preen</c> and <c>peck</c> carry <c>waterZ: 0</c> in their own pose, so pixels below the
    /// waterline take the depth-graded tint and alpha exactly as <c>FishIso2</c> does. This bake
    /// passes no <c>waterZ</c> override, which is what the drop's own sheet did. A DRY preen or peck
    /// — a gull cleaning itself on a wharf — needs <c>waterZ: null</c> passed explicitly at runtime,
    /// or it renders half-submerged on dry land. (The declared transition graph has no dry route
    /// into either state; float is their only in-edge. Reported upstream, not invented here.)</para>
    ///
    /// <para>Like its three siblings this stops at "PNG + contract JSON on disk". Slicing is
    /// <c>HiddenHarbours.Art.Editor.SeagullSheetSlicer</c>'s job and the import settings live in the
    /// committed <c>.meta</c>.</para>
    /// </summary>
    public static class SeagullBaker
    {
        /// <summary>The catalog key — see <c>RigCatalog.Seagull.cs</c>, which carries the measured
        /// notes behind the entry.</summary>
        public const string RigKey = "seagull";

        /// <summary>The ADR-0006 recipe's facing count. <c>SeagullIso</c> declares no <c>DIRS</c>
        /// global (<see cref="RigCatalog.Install"/> reports 0), so like the fishing and shovel kits
        /// the count is the recipe, stated once, here — and cross-checked against the gameplay
        /// sidecar's own <c>frame.directions</c> before a cell is rendered.</summary>
        public const int Dirs = 8;

        /// <summary>
        /// Creatures get their own sprite folder: they are neither a boat turntable
        /// (<c>Art/Boats/Iso</c>), nor a character sliced on ground contact by
        /// <c>CharacterSheetSlicer</c>, nor a grip-pivoted overlay (<c>Art/Fishing/Iso</c>). This
        /// folder does not exist in the tree until the first bake creates it.
        /// </summary>
        public const string DefaultOutputFolder = "Assets/_Project/Art/Sprites/Creatures";

        /// <summary>Sheet stem. <c>SeagullSheetSlicer</c> keys its grid off this.</summary>
        public const string SheetName = "Seagull";

        /// <summary>The contract document written beside the sheet — the slicer's grid, the rig
        /// sha it was baked from, and the column order, so nobody re-derives any of the three.</summary>
        public const string ContractFileName = "Seagull.contract.json";

        /// <summary>The gameplay sidecar cross-checked before baking. See <see cref="Bake"/>.</summary>
        public const string SidecarPath = "docs/art/rigs/gameplay/seagullIsoRig.gameplay.json";

        /// <summary>
        /// ⚠️ <b>This kit CANNOT use <see cref="FishingKitBaker.WriteSheet"/>, and the reason is a
        /// number.</b> That writer refuses anything over <see cref="FishingKitBaker.ImportSizeCap"/>
        /// = 2048 because Unity's DEFAULT importer <c>maxTextureSize</c> is 2048 and a sheet over it
        /// imports SILENTLY DOWNSCALED — with the sprite COUNT still correct, so the damage only
        /// surfaces much later as a cell-size or pivot failure. That refusal is right for a kit
        /// whose slicer does no lift.
        ///
        /// <para>This sheet is <b>3072×512</b>: 48 columns is what the rig's own <c>sheetOrder()</c>
        /// yields, and a creature's states are one page by construction. So the cap here is Unity's
        /// real hard limit (<see cref="RigBaker.MaxTextureSize"/> = 4096) and the LIFT IS MANDATORY
        /// AND ELSEWHERE: <c>SeagullSheetSlicer</c> must set <c>maxTextureSize = 4096</c> on the
        /// importer, exactly as <c>IsoPackSheetSlicer</c> does for its 3784 px pack. (The character
        /// slicer deliberately does not lift it — <c>CharacterRigBaker</c> says so — which is why
        /// this is stated per kit rather than assumed.) The contract JSON written beside the sheet
        /// carries the required <c>maxTextureSize</c> so a future slicer author reads it rather than
        /// guessing.</para>
        /// </summary>
        public const int SizeCap = RigBaker.MaxTextureSize;

        /// <summary>
        /// The blank-cell control's floor. Guards fallback (1) in the class remarks: a facing NAME
        /// instead of an INDEX renders a fully transparent cell and throws nothing.
        ///
        /// <para>The floor is 1 — strictly "something was drawn" — because that is exactly the
        /// control measured at intake (<b>384/384 cells non-blank</b>). A tighter per-pose floor was
        /// NOT measured, so one is not invented here: a fabricated threshold that rejects a legal
        /// pose is a worse failure than the one it would catch.</para>
        /// </summary>
        public const int MinOpaquePixelsPerCell = 1;

        /// <summary>One column of the sheet: an animation and a frame within it.</summary>
        public readonly struct Column
        {
            public readonly string Anim;
            public readonly int Frame;
            public Column(string anim, int frame) { Anim = anim; Frame = frame; }

            /// <summary>The render options for this column — <b>the mapping that matters</b>:
            /// <c>sheetOrder()</c> calls the frame <c>f</c>, <c>render</c> reads <c>frame</c>.
            /// Passing the record straight through bakes frame 0 of everything.</summary>
            public string RenderOpts() => $"{{anim:{Js(Anim)},frame:{Num(Frame)}}}";

            public override string ToString() => $"{Anim}/{Frame}";
        }

        // =====================================================================================
        // THE BAKE
        // =====================================================================================

        /// <summary>
        /// Renders <c>sheetOrder()</c> × <see cref="Dirs"/> facings into one page and writes the
        /// contract beside it.
        ///
        /// <para>The whole recipe is validated BEFORE a byte is written — the column list against
        /// the rig's <c>AORDER</c>/<c>ANIMS</c>, the geometry and the animation table against the
        /// gameplay sidecar, and the sidecar's own <c>derivedFromRigSha256</c> against the rig bytes
        /// on disk. A kit is one drop: a re-cut rig with a stale sidecar is not half-good, and
        /// finding that out after 384 cells have overwritten the committed sheet is finding it out
        /// too late.</para>
        /// </summary>
        public static FishingBakeResult Bake(string outputFolder = DefaultOutputFolder,
                                             Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get(RigKey);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new FishingBakeResult
            {
                RigKey = RigKey, EngineName = host.EngineName, Geometry = geo,
            };

            // ---- MEASURE the convention from pixels, refuse on a mismatch --------------------
            //
            // The seagull is CLOCKWISE — the minority convention in this repo — so it is measured
            // rather than assumed, and by a BIRD-SPECIFIC probe: see SeagullRigAzimuthProbe for the
            // three sibling probes that were each tried on this rig and each measured wrong.
            var probe = SeagullRigAzimuthProbe.Measure(host, g, geo, Dirs);
            FishingKitBaker.RefuseOnMismatch(RigKey, entry.DeclaredConvention,
                                             probe.Convention, probe.Report);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;

            // ---- validate the WHOLE recipe before writing anything ---------------------------
            IReadOnlyList<Column> columns = ReadSheetOrder(host, g);
            CrossCheckAgainstSidecar(entry, geo, columns);

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            // Per-cell content digests, so the two controls below assert something about the art
            // that was actually rendered rather than about the loop that rendered it.
            var digest = new ulong[Dirs, columns.Count];
            var opaque = new int[Dirs, columns.Count];

            progress?.Invoke(SheetName, 0f);
            result.Sheets.Add(WriteSheet(outputFolder, SheetName, Dirs, columns.Count, geo, (d, f) =>
            {
                // The one-place convention correction (RigBaker.DirForCell) — for this CLOCKWISE
                // rig it is the identity, which is why the sheet's rows come out as row r = dir r.
                // It is still routed through the shared function: a rig that is later re-cut the
                // other way must not need this file edited.
                double dir = RigBaker.DirForCell(d, Dirs, probe.Convention);
                byte[] rgba = FishingKitBaker.Render(host,
                    $"{g}.render({Num(dir)},{columns[f].RenderOpts()})", geo, renderClock, result);
                digest[d, f] = Fnv1a(rgba);
                opaque[d, f] = CountOpaque(rgba);
                progress?.Invoke($"{SheetName} {columns[f]} d{d}",
                                 (d * columns.Count + f + 1) / (float)(Dirs * columns.Count));
                return rgba;
            }, result));

            AssertNothingBlank(columns, opaque);
            AssertFramesActuallyMove(columns, digest);

            result.AnchorJsonPath = WriteContract(host, entry, geo, columns, probe, outputFolder);
            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        // =====================================================================================
        // THE RECIPE, READ FROM THE RIG
        // =====================================================================================

        /// <summary>
        /// The sheet's columns, asked of the rig rather than named here (ADR 0021 §4 — geometry and
        /// order come from the rig, never from a README).
        ///
        /// <para>Read in ONE evaluation as <c>anim:f</c> pairs. <c>FishingKitBaker.ReadStringArray</c>
        /// cannot be used: <c>sheetOrder()</c> yields OBJECTS, and that parser scoops every string
        /// out of the JSON — it would return the key names as well as the values.</para>
        ///
        /// <para>Every column is then checked back against <c>AORDER</c> and <c>ANIMS</c>: the anims
        /// must appear in <c>AORDER</c>'s order, each one's frames must run 0..n−1 with no gap, and
        /// no anim may appear twice. That is what makes fallback (2) — an unknown anim silently
        /// rendering <c>stand</c> — unreachable from here.</para>
        /// </summary>
        internal static IReadOnlyList<Column> ReadSheetOrder(IRigScriptHost host, string g)
        {
            string packed = host.EvaluateString(
                $"{g}.sheetOrder().map(function(c){{return c.anim+':'+c.f;}}).join(',')");
            if (string.IsNullOrEmpty(packed))
                throw new InvalidOperationException(
                    $"{g}.sheetOrder() yielded nothing — the rig has changed shape.");

            var columns = new List<Column>();
            foreach (string token in packed.Split(','))
            {
                int colon = token.LastIndexOf(':');
                if (colon <= 0 || !int.TryParse(token.Substring(colon + 1),
                                                NumberStyles.Integer, CultureInfo.InvariantCulture,
                                                out int frame))
                    throw new InvalidOperationException(
                        $"{g}.sheetOrder() produced the column '{token}', which is not " +
                        "'<anim>:<frame>'. The rig's own record shape has moved.");
                columns.Add(new Column(token.Substring(0, colon), frame));
            }

            // The column list the rig's OTHER two tables imply, built independently and compared
            // whole. Two tables agreeing is a real check; walking one of them with a cursor is a
            // parser that can be talked into agreeing with almost anything.
            var order = FishingKitBaker.ReadStringArray(host, $"{g}.AORDER");
            var expected = new List<Column>();
            foreach (string anim in order)
            {
                int n = (int)host.EvaluateNumber($"{g}.ANIMS[{Js(anim)}].n");
                if (n <= 0)
                    throw new InvalidOperationException(
                        $"{g}.ANIMS.{anim}.n is {n}. AORDER names a state the animation table has " +
                        "no frames for — one of the two has moved.");
                for (int f = 0; f < n; f++) expected.Add(new Column(anim, f));
            }

            for (int i = 0; i < Math.Max(expected.Count, columns.Count); i++)
            {
                string got = i < columns.Count ? columns[i].ToString() : "<past the end>";
                string want = i < expected.Count ? expected[i].ToString() : "<past the end>";
                if (got != want)
                    throw new InvalidOperationException(
                        $"sheetOrder() and AORDER×ANIMS disagree at column {i}: the sheet says " +
                        $"'{got}', the animation tables say '{want}'. " +
                        $"({columns.Count} columns against {expected.Count}.) A creature sheet is " +
                        "every state on one page in the rig's own order — the two statements of " +
                        "that order must be the same statement. AORDER is: " +
                        string.Join(", ", order) + ".");
            }

            return columns;
        }

        /// <summary>
        /// The rig and its gameplay sidecar are ONE DROP, so they must agree before either is used.
        ///
        /// <para>Three things are checked, and each has a way of going wrong that this bake would
        /// otherwise launder into a plausible sheet:</para>
        /// <list type="bullet">
        /// <item><b>the pin</b> — <c>derivedFromRigSha256</c> against the rig bytes on disk. A stale
        /// pin means the sidecar describes a bird that is no longer the one being rendered, and
        /// every state count below it is then about the wrong art.</item>
        /// <item><b>the frame</b> — cell size, pivot and direction count. The sidecar's
        /// <c>frame</c> block is what every gameplay consumer positions the bird by; if it and the
        /// rig's own geometry differ, the shadow and the contact point land in different places.</item>
        /// <item><b>the animation table</b> — each state's frame count against the columns
        /// <c>sheetOrder()</c> actually produced. This is the one that catches a re-cut rig against
        /// a sidecar nobody re-generated.</item>
        /// </list>
        ///
        /// <para>All three throw. A kit is not half-good, and a bake that overwrites the committed
        /// sheet before noticing is a bake that has already done the damage.</para>
        /// </summary>
        static void CrossCheckAgainstSidecar(in RigEntry entry, in RigGeometry geo,
                                             IReadOnlyList<Column> columns)
        {
            string rigAbs = Path.Combine(RigCatalog.RepoRoot, entry.ScriptPath);
            string sidecarAbs = Path.Combine(RigCatalog.RepoRoot, SidecarPath);
            var read = SeagullSidecarReader.Read(File.ReadAllText(sidecarAbs), SidecarPath,
                                                 File.ReadAllBytes(rigAbs));
            if (!read.Ok)
                throw new InvalidOperationException(
                    $"The seagull's gameplay sidecar does not read clean, so the bake is refusing " +
                    "before it overwrites the sheet:\n  " + string.Join("\n  ", read.Errors));

            var gp = read.Gameplay;
            if (gp.CellWidth != geo.Width || gp.CellHeight != geo.Height)
                throw new InvalidOperationException(
                    $"The sidecar's frame is {gp.CellWidth}×{gp.CellHeight} but the rig renders " +
                    $"{geo.Width}×{geo.Height}. The two halves of one drop disagree about the cell.");
            if (gp.PivotX != (int)geo.PivotX || gp.PivotY != (int)geo.PivotY)
                throw new InvalidOperationException(
                    $"The sidecar's contact point is ({gp.PivotX},{gp.PivotY}) but the rig's pivot " +
                    $"is ({geo.PivotX},{geo.PivotY}). Every consumer places the bird — and leaves " +
                    "its shadow — by that point; a disagreement here is a bird standing beside its " +
                    "own feet.");
            if (gp.Directions != Dirs)
                throw new InvalidOperationException(
                    $"The sidecar declares {gp.Directions} directions and this recipe bakes {Dirs}. " +
                    "The rig exposes no DIRS global, so these two are the only statements of the " +
                    "facing count and they must be the same one.");

            foreach (var state in gp.States)
            {
                int baked = 0;
                foreach (Column c in columns) if (c.Anim == state.Id) baked++;
                if (baked == 0)
                    throw new InvalidOperationException(
                        $"The sidecar declares the state '{state.Id}' but sheetOrder() bakes no " +
                        "column for it. The game would ask for art this sheet does not carry.");
                if (baked != state.Frames)
                    throw new InvalidOperationException(
                        $"The sidecar declares {state.Frames} frames of '{state.Id}' and " +
                        $"sheetOrder() produces {baked}. The rig and its sidecar have been cut at " +
                        "different times — regenerate the sidecar upstream; do not bake across it.");
            }
        }

        // =====================================================================================
        // THE CONTROLS — asserted against the pixels that were actually written
        // =====================================================================================

        /// <summary>Fallback (1): a facing NAME renders a fully transparent cell and throws
        /// nothing, so a sweep of blanks reads as a clean bake.</summary>
        static void AssertNothingBlank(IReadOnlyList<Column> columns, int[,] opaque)
        {
            for (int d = 0; d < Dirs; d++)
            for (int f = 0; f < columns.Count; f++)
                if (opaque[d, f] < MinOpaquePixelsPerCell)
                    throw new InvalidOperationException(
                        $"Cell row {d} ('{columns[f]}') drew {opaque[d, f]} opaque pixels. " +
                        "SeagullIso.render takes a facing INDEX — a compass STRING returns a fully " +
                        "transparent cell and throws nothing, which is how a blank sheet passes a " +
                        "diff. 384/384 cells were non-blank at intake; something has moved.");
        }

        /// <summary>
        /// Fallback (3): <c>sheetOrder()</c> names the frame <c>f</c> and <c>render</c> reads
        /// <c>frame</c>, so passing the record through unmapped bakes 48 columns of frame 0 —
        /// distinct-looking art that is all the same pose.
        ///
        /// <para>Measured at intake: every animation's frames differ from their neighbours in all 8
        /// directions, so this control has no legitimate exception to carve out. A state whose
        /// frames genuinely became identical would be a rig question, not a bake one.</para>
        /// </summary>
        static void AssertFramesActuallyMove(IReadOnlyList<Column> columns, ulong[,] digest)
        {
            for (int f = 1; f < columns.Count; f++)
            {
                if (columns[f].Frame == 0) continue;   // a new animation begins; nothing to compare
                for (int d = 0; d < Dirs; d++)
                    if (digest[d, f] == digest[d, f - 1])
                        throw new InvalidOperationException(
                            $"'{columns[f].Anim}' frames {columns[f].Frame - 1} and " +
                            $"{columns[f].Frame} render IDENTICALLY at direction {d}. The likeliest " +
                            "cause is the frame never reaching the rig: sheetOrder() calls it 'f' " +
                            "and render reads 'frame'. Every animation moved in all 8 directions at " +
                            "intake, so this is a real change — check the mapping in " +
                            $"{nameof(Column)}.{nameof(Column.RenderOpts)} before touching the rig.");
            }
        }

        static int CountOpaque(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] != 0) n++;
            return n;
        }

        /// <summary>FNV-1a 64. A content digest for the adjacent-frame control only — never a
        /// provenance claim; the sheet's provenance is the rig sha in the contract.</summary>
        static ulong Fnv1a(byte[] bytes)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in bytes) { h ^= b; h *= 1099511628211UL; }
            return h;
        }

        // =====================================================================================
        // THE SHEET WRITER — this kit's own, because of the cap
        // =====================================================================================

        /// <summary>
        /// One page: <paramref name="renderCell"/>(rowFromTop, frameCol) → RGBA cell.
        ///
        /// <para>Structurally <see cref="FishingKitBaker.WriteSheet"/>, deliberately duplicated
        /// rather than parameterised for the one thing that differs: the cap. See
        /// <see cref="SizeCap"/> for why 4096 is right here and 2048 is right there — the two
        /// numbers encode two different slicer behaviours, and a shared writer with a cap argument
        /// would let a caller pass 4096 without owning the lift.</para>
        /// </summary>
        static FishingSheetBake WriteSheet(string outputFolder, string name, int rows, int frames,
                                           in RigGeometry geo, Func<int, int, byte[]> renderCell,
                                           FishingBakeResult result)
        {
            int pw = frames * geo.Width;
            int ph = rows * geo.Height;
            if (pw > SizeCap || ph > SizeCap)
                throw new InvalidOperationException(
                    $"'{name}' would bake to {pw}×{ph}, over Unity's hard {SizeCap} texture limit. " +
                    "Page the sheet — a creature's states are one page by construction, so this " +
                    "means splitting the animation list, not raising the number.");

            var pixels = new Color32[pw * ph];
            for (int d = 0; d < rows; d++)
            for (int f = 0; f < frames; f++)
                RigBaker.Blit(renderCell(d, f), geo.Width, geo.Height, pixels, pw, ph,
                              col: f, rowFromTop: d);

            string assetPath = $"{outputFolder}/{name}.png";
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);
                result.TotalPngBytes += png.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }

            return new FishingSheetBake
            {
                Name = name, AssetPath = assetPath, Width = pw, Height = ph,
                Rows = rows, Frames = frames,
            };
        }

        // =====================================================================================
        // THE CONTRACT
        // =====================================================================================

        /// <summary>
        /// Writes <c>Seagull.contract.json</c> beside the sheet: the grid, the pivot, the column
        /// order, the sprite naming and the rig sha this page was rendered from.
        ///
        /// <para><b>Derived, never authored.</b> It exists so the slicer, the golden test and any
        /// future reader take the grid from the same measurement the pixels came from, rather than
        /// three copies of it drifting apart — which is the failure the <c>gameplay/</c> README's
        /// "derived sidecars are generated" rule is about.</para>
        ///
        /// <para>It carries <c>requiredMaxTextureSize</c> explicitly. A 3072 px sheet imported at
        /// the 2048 default is silently downscaled WITH THE RIGHT SPRITE COUNT, so the lift is the
        /// one import setting that cannot be left to be noticed.</para>
        /// </summary>
        static string WriteContract(IRigScriptHost host, in RigEntry entry, in RigGeometry geo,
                                    IReadOnlyList<Column> columns,
                                    in SeagullRigAzimuthProbe.Result probe, string outputFolder)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"sheet\": \"{outputFolder}/{SheetName}.png\",\n");
            sb.Append($"  \"rig\": \"{entry.ScriptPath}\",\n");
            sb.Append($"  \"global\": \"{entry.GlobalName}\",\n");
            sb.Append($"  \"derivedFromRigSha256\": \"{RigShaOf(entry.ScriptPath)}\",\n");
            sb.Append($"  \"engine\": \"{host.EngineName}\",\n");
            sb.Append($"  \"cell\": {{ \"w\": {geo.Width}, \"h\": {geo.Height} }},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {Num(geo.PivotX)}, \"y\": {Num(geo.PivotY)} }},\n");
            sb.Append($"  \"pivotNormalisedBottomLeft\": {{ \"x\": {Num(geo.PivotX / geo.Width)}, " +
                      $"\"y\": {Num((geo.Height - geo.PivotY) / geo.Height)} }},\n");
            sb.Append($"  \"rows\": {Dirs},\n");
            sb.Append($"  \"columns\": {columns.Count},\n");
            sb.Append($"  \"pixelsPerUnit\": 32,\n");
            sb.Append($"  \"requiredMaxTextureSize\": {SizeCap},\n");
            sb.Append($"  \"measuredRigConvention\": \"{probe.Convention}\",\n");
            sb.Append("  \"facingsAreCounterClockwise\": false,\n");
            sb.Append("  \"spriteName\": \"gull_<anim>_<frame>_d<row>\",\n");

            sb.Append("  \"order\": [\n");
            for (int i = 0; i < columns.Count; i++)
                sb.Append($"    {{ \"col\": {i}, \"anim\": \"{columns[i].Anim}\", " +
                          $"\"frame\": {columns[i].Frame} }}" + (i < columns.Count - 1 ? ",\n" : "\n"));
            sb.Append("  ],\n");

            sb.Append("  \"note\": \"ONE PAGE, EVERY STATE. Row r is facing r (dir 0 = north, " +
                      "+45 deg per row - the rig is CLOCKWISE, measured from the bill's own pixels " +
                      "by SeagullRigAzimuthProbe). The pivot is a CONTACT POINT: ground when " +
                      "standing/walking/perched, the water surface when floating, the point " +
                      "directly below the bird when airborne. ALTITUDE IS A SCREEN OFFSET, NEVER A " +
                      "SCALE - strict world scale at every altitude (32 px = 1 m, a 1.40 m span is " +
                      "45 px whether the gull is on the wharf or 25 m up). Blit an airborne frame " +
                      "at pivot - altitude*cos(40deg)*32*zoom and LEAVE THE SHADOW AT THE PIVOT, so " +
                      "the bird descends onto it. requiredMaxTextureSize is not optional: at the " +
                      "2048 default this 3072 px sheet imports silently downscaled with the correct " +
                      "sprite count.\",\n");

            sb.Append("  \"wetStates\": \"splash, float, preen and peck carry waterZ:0 in their own " +
                      "pose and are baked WET - pixels below the waterline take the depth-graded " +
                      "tint and alpha. A DRY preen or peck (a gull cleaning itself on a wharf) must " +
                      "pass waterZ:null explicitly at runtime or it renders half-submerged on dry " +
                      "land. Note the declared transition graph has no dry route into either state; " +
                      "float is their only in-edge. Reported upstream, not invented here.\"\n");
            sb.Append("}\n");

            return FishingKitBaker.WriteJson(outputFolder, ContractFileName, sb.ToString());
        }

        /// <summary>
        /// A rig's sha256 with CRLF folded to LF — the form every sidecar in this repo pins, and
        /// the only one that is the same number on every machine. Deferred to
        /// <see cref="DeckSidecarReader.Sha256HexLineEndingNormalised"/> rather than
        /// re-implemented: two transcriptions of one hash is two hashes.
        /// </summary>
        static string RigShaOf(string relPath) =>
            DeckSidecarReader.Sha256HexLineEndingNormalised(
                File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, relPath)));

        static string Js(string s) => FishingKitBaker.Js(s);
        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
