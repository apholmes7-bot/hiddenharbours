using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    public sealed class FishingSheetBake
    {
        public string Name;        // e.g. "Fish_cod_swim"
        public string AssetPath;
        public int Width, Height, Rows, Frames;
        public override string ToString() =>
            $"{AssetPath}  {Width}×{Height}  ({Frames} frames × {Rows} rows)";
    }

    public sealed class FishingBakeResult
    {
        public string RigKey;
        public string EngineName;
        public RigGeometry Geometry;
        /// <summary>Null for the bobber — it has no azimuth term and is never probed.</summary>
        public AzimuthConvention? MeasuredConvention;
        public string ConventionReport;
        public readonly List<FishingSheetBake> Sheets = new List<FishingSheetBake>();
        public string AnchorJsonPath;
        public double RenderMilliseconds;
        public double TotalMilliseconds;
        public long TotalPngBytes;
        public int CellsRendered;
    }

    /// <summary>
    /// Bakes the fishing kit's fight-critical rigs — the parametric FISH, the rod BOBBER and the
    /// ROD overlay — so Rod Fishing v2 wave 3 (the fight) has sprites. Third sibling of
    /// <see cref="RigBaker"/> (boat turntable) and <see cref="CharacterRigBaker"/> (8-dir anim
    /// rows), separate for the same reason those two are: none of the three shapes here fits
    /// either existing request. The fish multiplies a SPECIES axis into the character sheet shape;
    /// the rod's pose axis is driven by a SECOND rig (<c>CharacterIso.tool()</c>) living in the
    /// same host; the bobber has no direction axis at all. What IS shared stays shared:
    /// <see cref="RigBaker.Blit"/> and <see cref="RigBaker.DirForCell"/> (the one-place convention
    /// correction).
    ///
    /// Sheet contract (must match <c>FishingSheetSlicer</c>, which slices these untouched):
    /// rows from the TOP are direction rows (8 for fish/rod, 1 for the bobber — the bobber is a
    /// state sprite, not a turntable); N columns = the state's frames, read from the rig's own
    /// tables (ADR 0021 §4 — geometry from the rig, never a README); cell = the rig's W×H.
    ///
    /// ⚠️ CONVENTION: the fish CLAIMS clockwise (unverified kit) and the rod is in the README's
    /// pixel-verified clockwise group — but per the README's own correction note both are MEASURED
    /// from rendered pixels by <see cref="FishingRigAzimuthProbe"/>, and the bake REFUSES on a
    /// mismatch with the catalog. The rod bake additionally measures the CHARACTER rig (via
    /// <see cref="CharacterRigAzimuthProbe"/>) because every rod pose is read from
    /// <c>CharacterIso.tool()</c> — its rows must be emitted with its own measured convention.
    ///
    /// Like its siblings this deliberately stops at "PNG (+ anchors JSON) on disk": slicing is
    /// <c>FishingSheetSlicer</c>'s job and import settings are <c>ArtImportPipeline</c>'s.
    /// </summary>
    public static class FishingKitBaker
    {
        /// <summary>The ADR-0006 recipe's facing count. The fishing rigs declare no DIRS global
        /// (<see cref="RigCatalog.Install"/> reports 0 for them), so unlike the character path the
        /// count cannot be read from the rig — it is the recipe, stated once, here.</summary>
        public const int Dirs = 8;

        /// <summary>The DEFAULT importer texture cap — same rationale as
        /// <see cref="CharacterRigBaker.ImportSizeCap"/>: <c>FishingSheetSlicer</c> does no
        /// maxTextureSize lift, so anything over this imports SILENTLY DOWNSCALED.</summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Where the kit's sheets and anchors live. A sibling of the characters' Iso
        /// folder rather than inside it: <c>CharacterSheetSlicer</c> slices EVERY png under its
        /// root with the one ground-contact pivot rule, and none of these three rigs pivots on
        /// ground contact (fish = water-surface point, bobber = waterline, rod = grip).</summary>
        public const string DefaultOutputFolder = "Assets/_Project/Art/Fishing/Iso";

        /// <summary>
        /// The character states the rod overlay is baked against — every ANIMS state the character
        /// rig declares a tool (rod) pose for, minus <c>dig</c> (that is the shovel kit's, not
        /// ours) and minus plain <c>cast</c> (the flick-cast wave shipped on the
        /// castBack/castRelease sub-ranges; hold/cast_short/cast_long overlays would pair with
        /// legacy sheets already slated for retirement). Frame counts are READ from the rig's
        /// ANIMS table, never restated here.
        /// </summary>
        public static readonly string[] RodToolAnims =
        {
            "hold", "bite", "strike", "reel", "land", "castBack", "castRelease",
        };

        /// <summary>The rod's two prop rests, exactly as <c>RodIso.REST</c> declares them.</summary>
        public static readonly string[] RodRests = { "ground", "stored" };

        // =====================================================================================
        // FISH — Fish_<species>_<state>.png, 8 direction rows × the state's frames
        // =====================================================================================

        /// <summary>
        /// Bakes fish sheets at reference scale (the rig's <c>scale</c> stays a runtime dial;
        /// sheets are per species only). Null lists mean "everything the rig declares": species
        /// from <c>ORDER</c>, states from <c>AORDER</c> (water anims, baked with the default
        /// waterZ so the depth tint is in the pixels) followed by <c>RESTS</c> (dry deck/held
        /// poses). The anchors JSON carries <c>hold()</c> per species and the fight's
        /// <c>mouth()</c> line-attach per water-anim × dir × frame.
        /// </summary>
        public static FishingBakeResult BakeFish(IReadOnlyList<string> species = null,
                                                 IReadOnlyList<string> states = null,
                                                 string outputFolder = DefaultOutputFolder,
                                                 Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get("fish");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new FishingBakeResult
            {
                RigKey = "fish", EngineName = host.EngineName, Geometry = geo,
            };

            // ---- MEASURE the convention from pixels, then cross-check the declaration --------
            var probe = FishingRigAzimuthProbe.MeasureFish(host, g, geo, Dirs);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;
            RefuseOnMismatch("fish", entry.DeclaredConvention, probe.Convention, probe.Report);

            species ??= ReadStringArray(host, $"{g}.ORDER");
            states ??= ReadFishStateOrder(host, g);

            // Validate the WHOLE recipe before writing anything — a mistyped species or state
            // fails with zero files on disk rather than a half-baked set.
            foreach (var sp in species)
                if (!host.EvaluateBool($"typeof {g}.SPECIES[{Js(sp)}] === 'object'"))
                    throw new ArgumentException(
                        $"FishIso declares no species '{sp}'. Known: " +
                        string.Join(", ", ReadStringArray(host, $"{g}.ORDER")) + ".");
            foreach (var st in states) FishStateFrames(host, g, st);

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int done = 0, plan = species.Count * states.Count;
            foreach (var sp in species)
            foreach (var st in states)
            {
                progress?.Invoke($"Fish_{sp}_{st}", (float)done++ / plan);
                int frames = FishStateFrames(host, g, st);
                bool isRest = IsFishRest(host, g, st);

                result.Sheets.Add(WriteSheet(outputFolder, $"Fish_{sp}_{st}", Dirs, frames, geo,
                    (d, f) =>
                    {
                        double dir = RigBaker.DirForCell(d, Dirs, probe.Convention);
                        string poseKey = isRest ? "rest" : "anim";
                        string expr = $"{g}.render({Num(dir)},{{species:{Js(sp)},{poseKey}:{Js(st)},frame:{f}}})";
                        return Render(host, expr, geo, renderClock, result);
                    }, result));
            }

            result.AnchorJsonPath = WriteFishAnchors(host, entry, geo, species, states,
                                                     probe.Convention, outputFolder);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Water anims (AORDER, in rig order) then dry rests (RESTS, in rig order).</summary>
        public static IReadOnlyList<string> ReadFishStateOrder(IRigScriptHost host, string g)
        {
            var all = new List<string>();
            all.AddRange(ReadStringArray(host, $"{g}.AORDER"));
            all.AddRange(ReadStringArray(host, $"{g}.RESTS"));
            return all;
        }

        /// <summary>Frame count of one fish state, from the rig's own tables: <c>ANIMS[s].n</c>
        /// for water anims (the fish table's field is <c>n</c>, not the character rig's
        /// <c>frames</c>), <c>RPOSE[s].length</c> for rests.</summary>
        public static int FishStateFrames(IRigScriptHost host, string g, string state)
        {
            if (host.EvaluateBool($"typeof {g}.ANIMS[{Js(state)}] === 'object' && {g}.ANIMS[{Js(state)}] !== null"))
                return CheckFrames(state, (int)host.EvaluateNumber($"{g}.ANIMS[{Js(state)}].n"));
            if (host.EvaluateBool($"Array.isArray({g}.RPOSE[{Js(state)}])"))
                return CheckFrames(state, (int)host.EvaluateNumber($"{g}.RPOSE[{Js(state)}].length"));
            throw new ArgumentException(
                $"FishIso declares no state '{state}'. Known anims: " +
                string.Join(", ", ReadStringArray(host, $"{g}.AORDER")) + "; rests: " +
                string.Join(", ", ReadStringArray(host, $"{g}.RESTS")) + ". If the state genuinely " +
                "does not exist yet, that is an art-director rig change, not a baker workaround.");
        }

        static bool IsFishRest(IRigScriptHost host, string g, string state) =>
            host.EvaluateBool($"Array.isArray({g}.RPOSE[{Js(state)}])");

        static string WriteFishAnchors(IRigScriptHost host, in RigEntry entry, in RigGeometry geo,
                                       IReadOnlyList<string> species, IReadOnlyList<string> states,
                                       AzimuthConvention convention, string outputFolder)
        {
            string g = entry.GlobalName;
            var sb = new StringBuilder();
            sb.Append("{\n");
            AppendCommonHeader(sb, entry.ScriptPath, g, geo, Dirs, convention);
            sb.Append("  \"_note\": \"Baked in-engine with the rig's measured convention applied, so row d of every sheet depicts heading 360*d/dirs. mouth px are offsets FROM THE PIVOT (the water-surface point under the body centre), per direction row then per frame column, at reference scale — scale any runtime size onto them. mouth is exported for the WATER anims only: the rest poses re-pivot the cell to the grip while the rig's mouth() stays in body space, so a rest-pose mouth would be wrong data — and the fight (the one consumer) only ever attaches the line in the water.\",\n");
            sb.Append("  \"species\": {\n");

            for (int s = 0; s < species.Count; s++)
            {
                string sp = species[s];
                string hold = host.EvaluateString($"JSON.stringify({g}.hold({Js(sp)},1))");
                sb.Append($"    \"{sp}\": {{\n");
                sb.Append($"      \"hold\": {hold},\n");
                sb.Append("      \"states\": {\n");

                var waterStates = new List<string>();
                foreach (var st in states)
                    if (!IsFishRest(host, g, st)) waterStates.Add(st);

                for (int a = 0; a < waterStates.Count; a++)
                {
                    string st = waterStates[a];
                    int frames = FishStateFrames(host, g, st);
                    sb.Append($"        \"{st}\": {{ \"frames\": {frames}, \"mouth\": [\n");
                    for (int d = 0; d < Dirs; d++)
                    {
                        double dir = RigBaker.DirForCell(d, Dirs, convention);
                        sb.Append("          [");
                        for (int f = 0; f < frames; f++)
                        {
                            sb.Append(host.EvaluateString(
                                $"JSON.stringify({g}.mouth({Num(dir)},{{species:{Js(sp)},anim:{Js(st)},frame:{f}}}))"));
                            if (f < frames - 1) sb.Append(", ");
                        }
                        sb.Append(d < Dirs - 1 ? "],\n" : "]\n");
                    }
                    sb.Append(a < waterStates.Count - 1 ? "        ] },\n" : "        ] }\n");
                }

                sb.Append("      }\n");
                sb.Append(s < species.Count - 1 ? "    },\n" : "    }\n");
            }

            sb.Append("  }\n}\n");
            return WriteJson(outputFolder, "FishIsoAnchors.json", sb.ToString());
        }

        // =====================================================================================
        // BOBBER — Bobber_<state>.png, ONE row × the state's frames (not directional)
        // =====================================================================================

        /// <summary>
        /// Bakes the bobber's four state strips. No direction axis, no azimuth probe — the rig has
        /// no <c>dir</c> argument at all (<c>render(state, frame)</c>). The anchors JSON carries
        /// the line-attach point (the stem top) per state × frame, read from the rig's own POSE
        /// table exactly as its renderer uses it.
        /// </summary>
        public static FishingBakeResult BakeBobber(string outputFolder = DefaultOutputFolder,
                                                   Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get("bobber");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new FishingBakeResult
            {
                RigKey = "bobber", EngineName = host.EngineName, Geometry = geo,
                MeasuredConvention = null,
                ConventionReport = "not directional — no azimuth term, nothing to probe",
            };

            var states = ReadStringArray(host, $"{g}.ORDER");
            foreach (var st in states) BobberStateFrames(host, g, st);   // validate before writing

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            for (int i = 0; i < states.Count; i++)
            {
                string st = states[i];
                progress?.Invoke($"Bobber_{st}", (float)i / states.Count);
                int frames = BobberStateFrames(host, g, st);

                result.Sheets.Add(WriteSheet(outputFolder, $"Bobber_{st}", rows: 1, frames, geo,
                    (d, f) => Render(host, $"{g}.render({Js(st)},{f})", geo, renderClock, result),
                    result));
            }

            result.AnchorJsonPath = WriteBobberAnchors(host, entry, geo, states, outputFolder);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Frame count of one bobber state, from the rig's own STATES table (field
        /// <c>n</c>).</summary>
        public static int BobberStateFrames(IRigScriptHost host, string g, string state)
        {
            if (!host.EvaluateBool($"typeof {g}.STATES[{Js(state)}] === 'object' && {g}.STATES[{Js(state)}] !== null"))
                throw new ArgumentException(
                    $"RodBobber declares no state '{state}'. Known: " +
                    string.Join(", ", ReadStringArray(host, $"{g}.ORDER")) + ".");
            return CheckFrames(state, (int)host.EvaluateNumber($"{g}.STATES[{Js(state)}].n"));
        }

        static string WriteBobberAnchors(IRigScriptHost host, in RigEntry entry, in RigGeometry geo,
                                         IReadOnlyList<string> states, string outputFolder)
        {
            string g = entry.GlobalName;
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"rig\": \"{entry.ScriptPath}\",\n");
            sb.Append($"  \"global\": \"{g}\",\n");
            sb.Append($"  \"cell\": {{ \"w\": {geo.Width}, \"h\": {geo.Height} }},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {Num(geo.PivotX)}, \"y\": {Num(geo.PivotY)} }},\n");
            sb.Append("  \"directional\": false,\n");
            sb.Append("  \"_note\": \"The pivot is THE WATERLINE POINT — blit the cell at the surface and the baked underwater tint does the dipping. lineAttach is the stem-top pixel as an offset FROM THE PIVOT: dy = -6 + (the pose's px-below-waterline, 0 while airborne) — the same formula the rig's renderer uses for its top row, read from its own POSE table. ms per state comes from STATES.\",\n");
            sb.Append("  \"states\": {\n");

            for (int i = 0; i < states.Count; i++)
            {
                string st = states[i];
                int frames = BobberStateFrames(host, g, st);
                int ms = (int)host.EvaluateNumber($"{g}.STATES[{Js(st)}].ms");
                bool fly = st == "fly";

                sb.Append($"    \"{st}\": {{ \"frames\": {frames}, \"ms\": {ms}, \"lineAttach\": [");
                for (int f = 0; f < frames; f++)
                {
                    int dip = fly ? 0 : (int)host.EvaluateNumber($"{g}.POSE[{Js(st)}][{f}].d");
                    sb.Append($"{{ \"dx\": 0, \"dy\": {-6 + dip} }}");
                    if (f < frames - 1) sb.Append(", ");
                }
                sb.Append(i < states.Count - 1 ? "] },\n" : "] }\n");
            }

            sb.Append("  }\n}\n");
            return WriteJson(outputFolder, "BobberAnchors.json", sb.ToString());
        }

        // =====================================================================================
        // ROD — Rod_<tier>_<state>.png, 8 direction rows × the state's frames
        // =====================================================================================

        /// <summary>
        /// Bakes the rod overlay sheets: per tier, every <see cref="RodToolAnims"/> state posed by
        /// <c>CharacterIso.tool()</c> (pitch/yaw/bend per dir × frame, radians straight through to
        /// <c>RodIso.render</c>) plus the two prop rests. TWO rigs run in one host — the rod is
        /// rendered, the character is the pose driver — and BOTH are convention-probed: the rod by
        /// <see cref="FishingRigAzimuthProbe.MeasureRod"/>, the character by
        /// <see cref="CharacterRigAzimuthProbe"/>, each cross-checked against the catalog. The
        /// anchors JSON carries the tier-independent grip pins (body-cell px, the missing fight
        /// half of <c>FisherRodMount.json</c>) and per tier × state the <c>tip()</c> cell px and
        /// <c>tipLocal()</c> 3D points the line FX draw from.
        /// </summary>
        public static FishingBakeResult BakeRod(IReadOnlyList<string> tiers = null,
                                                IReadOnlyList<string> anims = null,
                                                bool includeRests = true,
                                                string outputFolder = DefaultOutputFolder,
                                                Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var rodEntry = RigCatalog.Get("rod");
            var charEntry = RigCatalog.Get("character");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var rodGeo = RigCatalog.Install(host, rodEntry);
            var charGeo = RigCatalog.Install(host, charEntry);
            string rod = rodEntry.GlobalName, ch = charEntry.GlobalName;

            var result = new FishingBakeResult
            {
                RigKey = "rod", EngineName = host.EngineName, Geometry = rodGeo,
            };

            // ---- MEASURE both rigs' conventions from pixels ----------------------------------
            var rodProbe = FishingRigAzimuthProbe.MeasureRod(host, rod, rodGeo, Dirs);
            RefuseOnMismatch("rod", rodEntry.DeclaredConvention, rodProbe.Convention, rodProbe.Report);

            var charProbe = CharacterRigAzimuthProbe.Measure(host, ch, charGeo);
            RefuseOnMismatch("character (rod pose driver)", charEntry.DeclaredConvention,
                             charProbe.Convention, charProbe.Report);

            result.MeasuredConvention = rodProbe.Convention;
            result.ConventionReport = rodProbe.Report +
                "\n---- character (pose driver) ----\n" + charProbe.Report;

            tiers ??= ReadStringArray(host, $"{rod}.order");
            anims ??= RodToolAnims;

            // Validate the WHOLE recipe before writing anything.
            foreach (var t in tiers)
                if (!host.EvaluateBool($"typeof {rod}.TIERS[{Js(t)}] === 'object'"))
                    throw new ArgumentException(
                        $"RodIso declares no tier '{t}'. Known: " +
                        string.Join(", ", ReadStringArray(host, $"{rod}.order")) + ".");
            foreach (var a in anims)
            {
                CharacterRigBaker.FramesOf(host, ch, a);   // the anim exists on the character rig
                if (!host.EvaluateBool($"{ch}.tool(0,{{anim:{Js(a)},frame:0}}) !== null"))
                    throw new ArgumentException(
                        $"CharacterIso declares no TOOL pose for '{a}' — tool() returns null, so " +
                        "there is nothing to pose the rod with. Rod overlays only exist for the " +
                        "rig's tool-carrying states.");
            }

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int planned = tiers.Count * (anims.Count + (includeRests ? RodRests.Length : 0));
            int done = 0;
            foreach (var tier in tiers)
            {
                foreach (var anim in anims)
                {
                    progress?.Invoke($"Rod_{tier}_{anim}", (float)done++ / planned);
                    int frames = CharacterRigBaker.FramesOf(host, ch, anim);

                    result.Sheets.Add(WriteSheet(outputFolder, $"Rod_{tier}_{anim}", Dirs, frames,
                        rodGeo, (d, f) =>
                        {
                            // The one-place correction, applied PER RIG: the rod row renders at the
                            // rod's measured convention, the pose is read at the character's.
                            double rodDir = RigBaker.DirForCell(d, Dirs, rodProbe.Convention);
                            double chDir = RigBaker.DirForCell(d, Dirs, charProbe.Convention);
                            string expr =
                                $"(function(){{var t={ch}.tool({Num(chDir)},{{anim:{Js(anim)},frame:{f}}});" +
                                $"return {rod}.render({Num(rodDir)},{{tier:{Js(tier)},pitch:t.pitch,yaw:t.yaw,bend:t.bend}});}})()";
                            return Render(host, expr, rodGeo, renderClock, result);
                        }, result));
                }

                if (includeRests)
                {
                    foreach (var rest in RodRests)
                    {
                        progress?.Invoke($"Rod_{tier}_{rest}", (float)done++ / planned);
                        result.Sheets.Add(WriteSheet(outputFolder, $"Rod_{tier}_{rest}", Dirs,
                            frames: 1, rodGeo, (d, f) =>
                            {
                                double rodDir = RigBaker.DirForCell(d, Dirs, rodProbe.Convention);
                                return Render(host,
                                    $"{rod}.render({Num(rodDir)},{{tier:{Js(tier)},rest:{Js(rest)}}})",
                                    rodGeo, renderClock, result);
                            }, result));
                    }
                }
            }

            result.AnchorJsonPath = WriteRodAnchors(host, rodEntry, charEntry, rodGeo, charGeo,
                                                    tiers, anims, includeRests,
                                                    rodProbe.Convention, charProbe.Convention,
                                                    outputFolder);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        static string WriteRodAnchors(IRigScriptHost host, in RigEntry rodEntry, in RigEntry charEntry,
                                      in RigGeometry rodGeo, in RigGeometry charGeo,
                                      IReadOnlyList<string> tiers, IReadOnlyList<string> anims,
                                      bool includeRests,
                                      AzimuthConvention rodConv, AzimuthConvention charConv,
                                      string outputFolder)
        {
            string rod = rodEntry.GlobalName, ch = charEntry.GlobalName;

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"rigs\": {{ \"rod\": \"{rodEntry.ScriptPath}\", \"character\": \"{charEntry.ScriptPath}\" }},\n");
            sb.Append($"  \"globals\": {{ \"rod\": \"{rod}\", \"character\": \"{ch}\" }},\n");
            sb.Append($"  \"cell\": {{ \"w\": {rodGeo.Width}, \"h\": {rodGeo.Height} }},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {Num(rodGeo.PivotX)}, \"y\": {Num(rodGeo.PivotY)} }},\n");
            sb.Append($"  \"bodyCell\": {{ \"w\": {charGeo.Width}, \"h\": {charGeo.Height} }},\n");
            sb.Append($"  \"behindDirs\": {host.EvaluateString($"JSON.stringify({rod}.behind)")},\n");
            sb.Append($"  \"dirs\": {Dirs},\n");
            sb.Append($"  \"measuredRodConvention\": \"{rodConv}\",\n");
            sb.Append($"  \"measuredCharacterConvention\": \"{charConv}\",\n");
            sb.Append("  \"facingsAreCounterClockwise\": false,\n");
            sb.Append("  \"_note\": \"Baked in-engine with each rig's measured convention applied, so row d depicts heading 360*d/dirs on every sheet. grips: the rod pivot (grip centre) lands on grip[dir][frame] in the CHARACTER'S 64x88 body cell — tier-independent, per tool anim; the fight-state half FisherRodMount.json does not carry. behindDirs rows draw the rod UNDER the body sprite. tip: absolute ROD-CELL px of the rod tip per tier x state x dir x frame (the line's start); tipLocal: the same tip as a character-local 3D point in metres (dir-independent) for RodIso.project()-style FX. Rest states have no grip — the rod is a prop, pivot per rest as the rig defines it.\",\n");

            // ---- grips: tier-independent, per anim × dir × frame -----------------------------
            sb.Append("  \"grips\": {\n");
            for (int a = 0; a < anims.Count; a++)
            {
                string anim = anims[a];
                int frames = CharacterRigBaker.FramesOf(host, ch, anim);
                sb.Append($"    \"{anim}\": {{ \"frames\": {frames}, \"px\": [\n");
                for (int d = 0; d < Dirs; d++)
                {
                    double chDir = RigBaker.DirForCell(d, Dirs, charConv);
                    sb.Append("      [");
                    for (int f = 0; f < frames; f++)
                    {
                        sb.Append(host.EvaluateString(
                            $"JSON.stringify((function(){{var t={ch}.tool({Num(chDir)},{{anim:{Js(anim)},frame:{f}}});" +
                            "return {x:Math.round(t.grip.x*10)/10,y:Math.round(t.grip.y*10)/10};})())"));
                        if (f < frames - 1) sb.Append(", ");
                    }
                    sb.Append(d < Dirs - 1 ? "],\n" : "]\n");
                }
                sb.Append(a < anims.Count - 1 ? "    ] },\n" : "    ] }\n");
            }
            sb.Append("  },\n");

            // ---- tiers: castMul + tip/tipLocal per state -------------------------------------
            var states = new List<string>(anims);
            if (includeRests) states.AddRange(RodRests);

            sb.Append("  \"tiers\": {\n");
            for (int t = 0; t < tiers.Count; t++)
            {
                string tier = tiers[t];
                string castMul = host.EvaluateString($"String({rod}.TIERS[{Js(tier)}].castMul)");
                sb.Append($"    \"{tier}\": {{ \"castMul\": {castMul}, \"states\": {{\n");

                for (int s = 0; s < states.Count; s++)
                {
                    string st = states[s];
                    bool isRest = Array.IndexOf(RodRests, st) >= 0;
                    int frames = isRest ? 1 : CharacterRigBaker.FramesOf(host, ch, st);

                    sb.Append($"      \"{st}\": {{ \"frames\": {frames}, \"tip\": [\n");
                    for (int d = 0; d < Dirs; d++)
                    {
                        double rodDir = RigBaker.DirForCell(d, Dirs, rodConv);
                        double chDir = RigBaker.DirForCell(d, Dirs, charConv);
                        sb.Append("        [");
                        for (int f = 0; f < frames; f++)
                        {
                            string optsJs = isRest
                                ? $"{{tier:{Js(tier)},rest:{Js(st)}}}"
                                : $"(function(){{var t={ch}.tool({Num(chDir)},{{anim:{Js(st)},frame:{f}}});" +
                                  $"return {{tier:{Js(tier)},pitch:t.pitch,yaw:t.yaw,bend:t.bend}};}})()";
                            sb.Append(host.EvaluateString(
                                $"JSON.stringify((function(){{var p={rod}.tip({Num(rodDir)},{optsJs});" +
                                "return {x:Math.round(p.x*10)/10,y:Math.round(p.y*10)/10};})())"));
                            if (f < frames - 1) sb.Append(", ");
                        }
                        sb.Append(d < Dirs - 1 ? "],\n" : "]\n");
                    }
                    sb.Append("      ], \"tipLocal\": [");
                    for (int f = 0; f < frames; f++)
                    {
                        string optsJs = isRest
                            ? $"{{tier:{Js(tier)},rest:{Js(st)}}}"
                            : $"(function(){{var t={ch}.tool(0,{{anim:{Js(st)},frame:{f}}});" +
                              $"return {{tier:{Js(tier)},pitch:t.pitch,yaw:t.yaw,bend:t.bend}};}})()";
                        sb.Append(host.EvaluateString(
                            $"JSON.stringify({rod}.tipLocal({optsJs}).map(function(v){{return Math.round(v*1000)/1000;}}))"));
                        if (f < frames - 1) sb.Append(", ");
                    }
                    sb.Append(s < states.Count - 1 ? "] },\n" : "] }\n");
                }

                sb.Append(t < tiers.Count - 1 ? "    } },\n" : "    } }\n");
            }
            sb.Append("  }\n}\n");

            return WriteJson(outputFolder, "RodIsoAnchors.json", sb.ToString());
        }

        // =====================================================================================
        // shared plumbing
        // =====================================================================================

        static void RefuseOnMismatch(string what, AzimuthConvention declared,
                                     AzimuthConvention measured, string report)
        {
            if (declared == measured) return;
            throw new InvalidOperationException(
                $"AZIMUTH MISMATCH on rig '{what}'.\n" +
                $"  the catalog declares       : {declared}\n" +
                $"  the rendered pixels say    : {measured}\n\n" +
                report + "\n\n" +
                "The bake is refusing rather than guessing. A silent guess here is how this " +
                "mislabel shipped defects in five kits — and the fishing kit's own README entry " +
                "is explicitly UNVERIFIED. Look at the art, decide which is right, and correct " +
                "the catalog (or flag the rig upstream) — do not relax this check.");
        }

        /// <summary>One sheet: <paramref name="renderCell"/>(rowFromTop, frameCol) → RGBA cell.</summary>
        static FishingSheetBake WriteSheet(string outputFolder, string name, int rows, int frames,
                                           in RigGeometry geo, Func<int, int, byte[]> renderCell,
                                           FishingBakeResult result)
        {
            int pw = frames * geo.Width;
            int ph = rows * geo.Height;
            if (pw > ImportSizeCap || ph > ImportSizeCap)
                throw new InvalidOperationException(
                    $"'{name}' would bake to {pw}×{ph}, over the {ImportSizeCap} default import " +
                    "cap. Unity would import it DOWNSCALED with a matching sprite count, so this " +
                    "would only surface as a slice-test dimension failure much later. Split the " +
                    "state or page the sheet before raising this limit.");

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

        static byte[] Render(IRigScriptHost host, string expr, in RigGeometry geo,
                             Stopwatch renderClock, FishingBakeResult result)
        {
            renderClock.Start();
            byte[] rgba = host.EvaluateBytes(expr);
            renderClock.Stop();
            result.CellsRendered++;
            if (rgba.Length != geo.Width * geo.Height * 4)
                throw new InvalidOperationException(
                    $"`{expr}` came back {rgba.Length} bytes, expected " +
                    $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
            return rgba;
        }

        static string WriteJson(string outputFolder, string fileName, string json)
        {
            string assetPath = $"{outputFolder}/{fileName}";
            File.WriteAllText(Path.Combine(RigCatalog.RepoRoot, assetPath), json);
            return assetPath;
        }

        static void AppendCommonHeader(StringBuilder sb, string scriptPath, string g,
                                       in RigGeometry geo, int dirs, AzimuthConvention convention)
        {
            sb.Append($"  \"rig\": \"{scriptPath}\",\n");
            sb.Append($"  \"global\": \"{g}\",\n");
            sb.Append($"  \"cell\": {{ \"w\": {geo.Width}, \"h\": {geo.Height} }},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {Num(geo.PivotX)}, \"y\": {Num(geo.PivotY)} }},\n");
            sb.Append($"  \"dirs\": {dirs},\n");
            sb.Append($"  \"measuredRigConvention\": \"{convention}\",\n");
            sb.Append("  \"facingsAreCounterClockwise\": false,\n");
        }

        static IReadOnlyList<string> ReadStringArray(IRigScriptHost host, string arrayExpr)
        {
            // The rigs' order arrays are plain identifier strings — parse the same defensive way
            // CharacterRigAzimuthProbe parses ramps.
            string json = host.EvaluateString($"JSON.stringify({arrayExpr})");
            var outp = new List<string>();
            int i = 0;
            while (i < json.Length)
            {
                if (json[i] == '"')
                {
                    int end = json.IndexOf('"', i + 1);
                    if (end < 0) break;
                    outp.Add(json.Substring(i + 1, end - i - 1));
                    i = end + 1;
                }
                else i++;
            }
            if (outp.Count == 0)
                throw new InvalidOperationException($"`{arrayExpr}` yielded no strings — rig changed shape?");
            return outp;
        }

        static int CheckFrames(string state, int frames) =>
            frames > 0 ? frames
                       : throw new InvalidOperationException($"State '{state}' declares {frames} frames.");

        /// <summary>Single-quoted JS string literal (names are plain identifiers, but escape
        /// defensively — a quote in a name must not become an injection).</summary>
        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
