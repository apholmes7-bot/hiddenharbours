using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the CLAM SPADE kit from <c>docs/art/rigs/shovelIsoRig.js</c> — the rod overlay's
    /// structural twin, and deliberately built as one.
    ///
    /// <para><b>What the owner reported (playtest 2026-09-09).</b> <i>"the shovel seems to be the
    /// old sprite which does not look rooted in the players hand."</i> Two separable defects sit
    /// behind that sentence and only one of them is art:</para>
    ///
    /// <list type="number">
    /// <item><b>the old sprite</b> — the hand draws the loose 32 px <c>Art/Sprites/Gear/Shovel.png</c>
    /// at every heading, because <c>shovelIsoRig.js</c> has been committed and unbaked since it
    /// landed and <c>CharacterRigBaker</c> says so in its own words: <i>"shovelIsoRig.js is committed
    /// but not yet registered in this catalog"</i>. THIS baker closes that.</item>
    /// <item><b>not rooted in her hand</b> — while she WALKS, the spade hangs off
    /// <c>CarryHands.FallbackOffset</c>, one serialized hip <c>Vector2</c> reflected for the left
    /// hand, because <c>CarryAnchorTableDef</c> has no shovel row to pin it by. That row is a
    /// <c>PROPS</c> entry in <c>characterIsoRig6.hands.js</c> — the art director's file — and it is
    /// NOT baked here. See the note on <see cref="DigOnlyBecauseToolIsNullOnAGait"/>.</item>
    /// </list>
    ///
    /// <para><b>Why the DIG is bakeable today and the WALK is not.</b> The character rig's
    /// <c>ANIM_MOUNT</c> already names the spade — <c>dig:'shovel'</c> — and <c>tool(dir,opts)</c>
    /// answers with a grip, pitch, yaw and bend for all eight facings and all ten dig frames. That
    /// is the whole of what the rod baker consumes, so the dig sheet is a straight mirror of
    /// <see cref="FishingKitBaker.BakeRod"/>. But <c>ANIM_MOUNT</c> marks <c>idle</c>, <c>walk</c>
    /// and <c>run</c> as <c>'free'</c>, and <c>tool()</c> returns <b>null</b> on all three (measured
    /// in the standalone V8 harness, not inferred) — a free arm's prop is the hands table's job, not
    /// <c>tool()</c>'s. So this kit bakes every pose the shipped rigs can pose, and the carried
    /// stance is registered as an upstream ask rather than invented here.</para>
    ///
    /// <para><b>Where the sheets go, and why the fishing folder is the right lie.</b>
    /// <c>Assets/_Project/Art/Fishing/Iso</c> is not named for tackle — <c>FishingKitBaker</c>
    /// documents it as the folder for rigs that do NOT pivot on ground contact, because
    /// <c>CharacterSheetSlicer</c> slices everything under the characters' root with the one
    /// ground-contact pivot rule. The spade pivots on the GRIP, exactly like the rod, at exactly the
    /// rod's cell and pivot (112×112, (56,72)). It belongs with the rod by the only rule the folder
    /// actually encodes, and <c>FishingSheetSlicer</c>'s manifest already knows that grid.</para>
    ///
    /// <para>Slicing is <c>FishingSheetSlicer</c>'s job and import settings are
    /// <c>ArtImportPipeline</c>'s — this class writes PNGs and one sidecar, nothing else.</para>
    /// </summary>
    public static class ShovelKitBaker
    {
        /// <summary>The ADR-0006 recipe's facing count. <c>ShovelIso</c> declares no DIRS global
        /// (<see cref="RigCatalog.Install"/> reports 0), so like the fishing kit the count is the
        /// recipe, stated once, here.</summary>
        public const int Dirs = 8;

        /// <summary>Where the kit's sheets and sidecar live — see the class remarks on why this
        /// folder rather than the characters' Iso root.</summary>
        public const string DefaultOutputFolder = FishingKitBaker.DefaultOutputFolder;

        /// <summary>The mount sidecar, the rod's sibling in the same folder and the same shape.</summary>
        public const string MountSidecar = "docs/art/rigs/gameplay/FisherShovelMount.json";

        /// <summary>
        /// The character state the spade is posed against: the ONE animation the character rig
        /// declares a shovel tool pose for. Read from <c>ANIM_MOUNT</c> at bake time and asserted,
        /// never trusted as a literal — a list that is a copy is how the rod kit came to bake two
        /// rests while the rig had grown a third.
        /// </summary>
        public const string DigAnim = "dig";

        /// <summary>
        /// <b>The carried stance is an UPSTREAM ASK, not an omission.</b> Named as a symbol so a
        /// reader who wonders "why is there no walk sheet" lands on the reason instead of guessing.
        ///
        /// <para><c>CharacterIso.ANIM_MOUNT</c> marks idle/walk/run <c>'free'</c> and
        /// <c>CharacterIso.tool()</c> returns null on every one of them — there is no rig answer to
        /// pose a spade on a swinging arm. The rig's answer for a prop on a free arm is a
        /// <c>PROPS</c> row in <c>characterIsoRig6.hands.js</c>, which ships seven rows
        /// (rodTrail, rodSling, fish, clam, knife, gaff, rope) and no shovel — asserted by
        /// <c>CharacterHandPropAnchorTests.NoShovelRow_Exists_TheCarriedShovelIsAnOpenArtAsk</c> and
        /// <c>CarryAnchorImportTests.NoShovelRow_SoTheShovelDefNamesNoHandProp_…</c>, both of which
        /// exist to go red the day it lands.</para>
        ///
        /// <para><b>⚠️ And the gaff's row must not be borrowed for it.</b> The gaff is a 1.55 m pole
        /// and the spade is 1.04 m; its per-facing yaw corrections are tuned constants on a
        /// different lever. The second of those two tests says exactly this, and it is right.</para>
        ///
        /// <para><b>What the ask is worth, measured.</b> Rendered at the rig's own default held pose
        /// the spade is <b>37 opaque pixels at north</b> — a 9×6 smudge — and 78 at north-east,
        /// against 157 at south, because at those headings the tool points into the screen and
        /// collapses. That is precisely the foreshortening the rod's and gaff's rows correct with
        /// <c>facing:{N:{yaw:-34}}</c>-style deltas. So a carry sheet baked at the default pose
        /// would ship art the owner would reject a second time, which is why this kit does not bake
        /// one and asks for the row instead.</para>
        /// </summary>
        public const string DigOnlyBecauseToolIsNullOnAGait =
            "idle/walk/run are ANIM_MOUNT 'free'; tool() returns null on all three. A carried spade " +
            "needs a shovelTrail PROPS row in characterIsoRig6.hands.js (art director's lane).";

        // =====================================================================================
        // THE BAKE
        // =====================================================================================

        /// <summary>
        /// Bakes the spade's sheets: <c>Shovel_dig</c> (8 direction rows × the dig's frames, posed
        /// by <c>CharacterIso.tool()</c> — pitch/yaw in radians straight through to
        /// <c>ShovelIso.render</c>) plus one sheet per prop rest the rig declares.
        ///
        /// <para>TWO rigs run in one host — the spade is rendered, the character is the pose driver
        /// — and <b>BOTH are convention-probed and cross-checked against the catalog</b>: the spade
        /// by <see cref="ShovelRigAzimuthProbe"/>, the character by
        /// <see cref="CharacterRigAzimuthProbe"/>. They disagree (the spade is counter-clockwise, the
        /// character clockwise) and that disagreement is the whole reason the correction is applied
        /// PER RIG at every cell rather than once for the sheet.</para>
        /// </summary>
        public static FishingBakeResult Bake(bool includeRests = true,
                                             string outputFolder = DefaultOutputFolder,
                                             Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var shovelEntry = RigCatalog.Get("shovel");
            var charEntry = RigCatalog.Get("character");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var shovelGeo = RigCatalog.Install(host, shovelEntry);
            var charGeo = RigCatalog.Install(host, charEntry);
            string sh = shovelEntry.GlobalName, ch = charEntry.GlobalName;

            var result = new FishingBakeResult
            {
                RigKey = "shovel", EngineName = host.EngineName, Geometry = shovelGeo,
            };

            // ---- MEASURE both rigs' conventions from pixels, refuse on either mismatch --------
            var shovelProbe = ShovelRigAzimuthProbe.Measure(host, sh, shovelGeo, Dirs);
            FishingKitBaker.RefuseOnMismatch("shovel", shovelEntry.DeclaredConvention,
                                             shovelProbe.Convention, shovelProbe.Report);

            var charProbe = CharacterRigAzimuthProbe.Measure(host, ch, charGeo);
            FishingKitBaker.RefuseOnMismatch("character (shovel pose driver)",
                                             charEntry.DeclaredConvention,
                                             charProbe.Convention, charProbe.Report);

            result.MeasuredConvention = shovelProbe.Convention;
            result.ConventionReport = shovelProbe.Report +
                "\n---- character (pose driver) ----\n" + charProbe.Report;

            // ---- validate the WHOLE recipe before writing anything ---------------------------
            //
            // The mount is read from the rig, not asserted as a literal: if the art director ever
            // renames the layer or moves the spade onto another clip, this is the line that says so
            // instead of a sheet that silently poses from the wrong curve.
            string mount = CharacterRigBaker.MountOf(host, ch, DigAnim);
            if (!string.Equals(mount, "shovel", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"CharacterIso.ANIM_MOUNT['{DigAnim}'] is '{mount}', not 'shovel'. The spade is " +
                    "posed from the clip the character rig says drives it — fix the rig or this " +
                    "constant, do not bake from a curve that mounts something else.");

            int digFrames = CharacterRigBaker.FramesOf(host, ch, DigAnim);
            if (!host.EvaluateBool($"{ch}.tool(0,{{anim:{Js(DigAnim)},frame:0}}) !== null"))
                throw new ArgumentException(
                    $"CharacterIso declares no TOOL pose for '{DigAnim}' — tool() returns null, so " +
                    "there is nothing to pose the spade with.");

            // The rig's own DIG contract must agree with the character's clip, or the sheet has a
            // frame count nothing plays. ShovelIso.DIG.frames is the rig's own claim; the character
            // rig's ANIMS table is the truth the engine animates from.
            int rigDigFrames = (int)host.EvaluateNumber($"{sh}.DIG.frames");
            if (rigDigFrames != digFrames)
                throw new ArgumentException(
                    $"ShovelIso.DIG.frames is {rigDigFrames} but CharacterIso.ANIMS.{DigAnim}.frames " +
                    $"is {digFrames}. The spade rig and the clip that swings it disagree about the " +
                    "length of a dig — one of them has moved. This is a rig question, not a bake one.");

            var rests = includeRests
                ? FishingKitBaker.ReadStringArray(host, $"{sh}.REST")
                : Array.Empty<string>();

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            // ---- the dig sheet ---------------------------------------------------------------
            progress?.Invoke("Shovel_dig", 0f);
            result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, "Shovel_dig", Dirs, digFrames,
                shovelGeo, (d, f) =>
                {
                    // The one-place correction, applied PER RIG — the spade row renders at the
                    // spade's measured convention, the pose is read at the character's. These two
                    // are NOT the same number for this kit (CCW vs CW), which is exactly why this
                    // cannot be hoisted out of the loop.
                    double shovelDir = RigBaker.DirForCell(d, Dirs, shovelProbe.Convention);
                    double chDir = RigBaker.DirForCell(d, Dirs, charProbe.Convention);
                    string expr =
                        $"(function(){{var t={ch}.tool({Num(chDir)},{{anim:{Js(DigAnim)},frame:{f}}});" +
                        $"return {sh}.render({Num(shovelDir)},{{pitch:t.pitch,yaw:t.yaw}});}})()";
                    return FishingKitBaker.Render(host, expr, shovelGeo, renderClock, result);
                }, result));

            // ---- the rests -------------------------------------------------------------------
            //
            // One cell per facing, not an animation: unlike the rod, ShovelIso declares no
            // REST_FRAMES — a spade's rest is a still prop (blade flat on the ground, or planted
            // upright). Asserted rather than assumed, so the day the rig grows a hand-over the
            // sheet stops being a single column silently.
            if (includeRests && host.EvaluateBool($"typeof {sh}.REST_FRAMES !== 'undefined'"))
                throw new ArgumentException(
                    "ShovelIso has grown a REST_FRAMES — its rests are now animated hand-overs like " +
                    "the rod's, and this baker still writes one still cell per facing. Bake the " +
                    "frames before the set-down can be watched rather than cut to.");

            int done = 0;
            foreach (string rest in rests)
            {
                progress?.Invoke($"Shovel_{rest}", (float)++done / Math.Max(1, rests.Count));
                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, $"Shovel_{rest}", Dirs,
                    frames: 1, shovelGeo, (d, _) =>
                    {
                        double shovelDir = RigBaker.DirForCell(d, Dirs, shovelProbe.Convention);
                        return FishingKitBaker.Render(host,
                            $"{sh}.render({Num(shovelDir)},{{rest:{Js(rest)}}})",
                            shovelGeo, renderClock, result);
                    }, result));
            }

            result.AnchorJsonPath = WriteMount(host, shovelEntry, charEntry, shovelGeo, charGeo,
                                               digFrames, rests, shovelProbe, charProbe.Convention);
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            return result;
        }

        // =====================================================================================
        // THE MOUNT SIDECAR
        // =====================================================================================

        /// <summary>
        /// Writes <c>FisherShovelMount.json</c> — the human-readable half of the spade mount, the
        /// rod's sibling in the same folder and the same shape: <b>ONE cell, ONE pivot, and every
        /// pose as a curve on that one tool</b>, so a consumer that pins the pivot to her grip never
        /// has to know which state it is in.
        ///
        /// <para><b>Derived, never authored</b> — the <c>gameplay/</c> README's rule for any sidecar
        /// whose rig ships a generator. The generator is HERE and not in <c>tools/rig-recipes</c>
        /// (where the rod's <c>fisher-rod-mount.mjs</c> lives) for one measured reason: those
        /// recipes are node scripts and <b>node is not installed on this machine</b>, so a recipe
        /// would be a file nobody in this lane could run. The Unity baker already writes exactly
        /// this kind of document (<c>CharacterRigBaker.WriteAnchors</c>), runs the same rigs in the
        /// same V8, and is the one place the sheets and their sidecar can be written from one
        /// measurement — which is what makes the hash pin below mean anything.</para>
        ///
        /// <para><b>The grip is measured from the CHARACTER, not copied from the rod.</b> A rod is
        /// held at 0.375 m up a 1.25 m blank and a spade at the D-grip of a 1.04 m shaft; the pins
        /// here are <c>CharacterIso.tool()</c>'s own per-facing, per-frame answers for the DIG clip
        /// and nothing else's.</para>
        ///
        /// <para><b>⚠️ The document states what it does NOT carry.</b> There is no carried/walk
        /// block, because there is no rig answer to put in one — see
        /// <see cref="DigOnlyBecauseToolIsNullOnAGait"/>. A sidecar that invented one would be the
        /// worst outcome of this lane: a plausible number, in the file a future reader trusts,
        /// measured from nothing.</para>
        /// </summary>
        static string WriteMount(IRigScriptHost host, in RigEntry shovelEntry, in RigEntry charEntry,
                                 in RigGeometry shovelGeo, in RigGeometry charGeo, int digFrames,
                                 IReadOnlyList<string> rests,
                                 in ShovelRigAzimuthProbe.Result shovelProbe,
                                 AzimuthConvention charConvention)
        {
            string sh = shovelEntry.GlobalName, ch = charEntry.GlobalName;

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append(" \"build\": \"fisher\",\n");
            sb.Append(" \"derivedFromRigSha256\": {\n");
            sb.Append($"  \"characterIsoRig6.js\": \"{RigShaOf(charEntry.ScriptPath)}\",\n");
            sb.Append($"  \"shovelIsoRig.js\": \"{RigShaOf(shovelEntry.ScriptPath)}\"\n");
            sb.Append(" },\n");

            sb.Append(" \"order\": [");
            IReadOnlyList<string> order = FishingKitBaker.ReadStringArray(host, $"{ch}.order");
            for (int i = 0; i < order.Count; i++)
                sb.Append(i == 0 ? $"\"{order[i]}\"" : $", \"{order[i]}\"");
            sb.Append("],\n");

            sb.Append($" \"bodyCell\": {{ \"w\": {charGeo.Width}, \"h\": {charGeo.Height} }},\n");
            sb.Append($" \"shovelCell\": {{ \"w\": {shovelGeo.Width}, \"h\": {shovelGeo.Height} }},\n");
            sb.Append($" \"shovelPivot\": {{ \"x\": {Num(shovelGeo.PivotX)}, " +
                      $"\"y\": {Num(shovelGeo.PivotY)} }},\n");

            sb.Append($" \"shaftLenM\": {{ \"up\": {Num(host.EvaluateNumber($"{sh}.SPEC.lenUp"))}, " +
                      $"\"down\": {Num(host.EvaluateNumber($"{sh}.SPEC.lenDn"))} }},\n");

            sb.Append(" \"behindDirs\": [");
            for (int i = 0; i < Dirs; i++)
            {
                bool behind = host.EvaluateBool($"{sh}.behind.indexOf({Num(i)}) >= 0");
                if (behind) sb.Append(sb[sb.Length - 1] == '[' ? $"{i}" : $", {i}");
            }
            sb.Append("],\n");

            sb.Append($" \"measuredShovelConvention\": \"{shovelProbe.Convention}\",\n");
            sb.Append($" \"measuredCharacterConvention\": \"{charConvention}\",\n");
            sb.Append(" \"facingsAreCounterClockwise\": false,\n");

            sb.Append(" \"note\": \"ONE SPADE, EVERY STATE. Draw the body cell; place the shovel " +
                      "overlay so shovelPivot lands on grip[dir][frame]. shovelCell/shovelPivot/" +
                      "shaftLenM do not vary by state — that is the contract. behindDirs -> spade " +
                      "UNDER body. Spoil (tossed dirt) is RUNTIME FX and never baked: it launches at " +
                      "DIG.tossFrame from ShovelIso.tip(), projected from the grip via " +
                      "ShovelIso.project(). The two conventions above are MEASURED and they DISAGREE " +
                      "- the spade rig is counter-clockwise where the character and the rod are " +
                      "clockwise - so every cell applies the correction per rig. The sheets on disk " +
                      "are clockwise like every other sheet in the project.\",\n");

            sb.Append(" \"_absent\": \"There is no carried/walk block, and that is a finding rather " +
                      "than an omission. CharacterIso.ANIM_MOUNT marks idle/walk/run 'free' and " +
                      "tool() returns null on all three, so the shipped rigs have no answer for a " +
                      "spade on a swinging arm. A carried spade needs a 'shovelTrail' PROPS row in " +
                      "characterIsoRig6.hands.js (the art director's lane), which would then import " +
                      "through CarryAnchorTableBuilder like rodTrail does. Until it lands, " +
                      "CarryHands poses a held shovel from one serialized hip offset - which is what " +
                      "the owner saw. Measured cost of the gap: at the rig's default held pose the " +
                      "spade is 37 opaque px at north and 78 at north-east, against 157 at south, " +
                      "because it points into the screen at those headings. Do NOT borrow the gaff's " +
                      "row: 1.55 m of pole tuned on a different lever.\",\n");

            // ---- the dig pose curve ----------------------------------------------------------
            sb.Append(" \"poses\": {\n");
            sb.Append($"  \"{DigAnim}\": {{\n");
            sb.Append($"   \"frames\": {digFrames},\n");
            sb.Append($"   \"ms\": {Num(host.EvaluateNumber($"{ch}.ANIMS.{DigAnim}.ms"))},\n");
            sb.Append($"   \"tossFrame\": {Num(host.EvaluateNumber($"{sh}.DIG.tossFrame"))},\n");
            AppendGrid(sb, "grip", Dirs, digFrames, (d, f) =>
            {
                double chDir = RigBaker.DirForCell(d, Dirs, charConvention);
                double x = host.EvaluateNumber(
                    $"{ch}.tool({Num(chDir)},{{anim:{Js(DigAnim)},frame:{f}}}).grip.x");
                double y = host.EvaluateNumber(
                    $"{ch}.tool({Num(chDir)},{{anim:{Js(DigAnim)},frame:{f}}}).grip.y");
                return $"{{ \"x\": {Num(R1(x))}, \"y\": {Num(R1(y))} }}";
            });
            sb.Append(",\n");
            AppendGrid(sb, "shovelPose", Dirs, digFrames, (d, f) =>
            {
                double chDir = RigBaker.DirForCell(d, Dirs, charConvention);
                string t = $"{ch}.tool({Num(chDir)},{{anim:{Js(DigAnim)},frame:{f}}})";
                double pitch = host.EvaluateNumber($"{t}.pitch");
                double yaw = host.EvaluateNumber($"{t}.yaw");
                double bend = host.EvaluateNumber($"{t}.bend");
                return $"{{ \"pitch\": {Num(R3(pitch))}, \"yaw\": {Num(R3(yaw))}, " +
                       $"\"bend\": {Num(R3(bend))} }}";
            });
            sb.Append("\n  }\n");
            sb.Append(" },\n");

            // ---- the rests, which are placements rather than poses ---------------------------
            sb.Append(" \"rests\": [");
            for (int i = 0; i < rests.Count; i++)
                sb.Append(i == 0 ? $"\"{rests[i]}\"" : $", \"{rests[i]}\"");
            sb.Append("]\n");
            sb.Append("}\n");

            string abs = Path.Combine(RigCatalog.RepoRoot, MountSidecar);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            File.WriteAllText(abs, sb.ToString());
            return MountSidecar;
        }

        /// <summary>One [dir][frame] grid, laid out the way the rod's sidecar lays its own out.</summary>
        static void AppendGrid(StringBuilder sb, string key, int dirs, int frames,
                               Func<int, int, string> cell)
        {
            sb.Append($"   \"{key}\": [\n");
            for (int d = 0; d < dirs; d++)
            {
                sb.Append("    [");
                for (int f = 0; f < frames; f++)
                {
                    if (f > 0) sb.Append(", ");
                    sb.Append(cell(d, f));
                }
                sb.Append(d < dirs - 1 ? "],\n" : "]\n");
            }
            sb.Append("   ]");
        }

        /// <summary>
        /// A rig's sha256 with CRLF folded to LF — the form every sidecar in this repo pins and the
        /// only one that is the same number on every machine, because no <c>.gitattributes</c> rule
        /// covers <c>docs/art/rigs/**/*.js</c> and <c>core.autocrlf</c> is on here. Deferred to
        /// <see cref="DeckSidecarReader.Sha256HexLineEndingNormalised"/> rather than re-implemented:
        /// two transcriptions of one hash is two hashes.
        /// </summary>
        static string RigShaOf(string relPath) =>
            DeckSidecarReader.Sha256HexLineEndingNormalised(
                File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, relPath)));

        static double R1(double n) => Math.Round(n * 10) / 10;
        static double R3(double n) => Math.Round(n * 1000) / 1000;
        static string Js(string s) => FishingKitBaker.Js(s);
        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
