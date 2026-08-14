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
    /// One sheet's worth of request: an anim from the rig's own <c>ANIMS</c> table, plus the optional
    /// <c>power</c> sub-range that <c>cast</c> reads and the optional <c>carry</c> stance a free anim
    /// can ride.
    ///
    /// <para><b>The sheet axis is not the anim axis, twice over.</b> <c>cast</c> bakes as
    /// <c>cast_short</c> and <c>cast_long</c>, because the rig scales the same ten frames over a
    /// different sub-range per power. And a carry stance bakes as its own sheet
    /// (<c>walk_buckets</c>) because <b>carry changes the POSE, not just the pins</b> — the rig's
    /// <c>pose()</c> takes the stance, so a bucket walk braces the arms and leans differently from a
    /// free walk. Baking one free <c>walk</c> and pinning pails to <c>carry()</c>'s hand anchors
    /// would hang the pails off a body that is not carrying them.</para>
    /// </summary>
    public readonly struct CharacterState
    {
        /// <summary>Anim name exactly as the rig's ANIMS table declares it.</summary>
        public readonly string Anim;

        /// <summary>The rig's <c>power</c> opt (<c>short</c> / <c>long</c>), or null for none.</summary>
        public readonly string Power;

        /// <summary>The rig's <c>carry</c> stance, or null. Only legal on a FREE anim — a tool anim
        /// always wins and the rig ignores carry, so baking one would be a duplicate sheet.</summary>
        public readonly string Carry;

        public CharacterState(string anim, string power = null, string carry = null)
        {
            Anim = anim ?? throw new ArgumentNullException(nameof(anim));
            Power = string.IsNullOrEmpty(power) ? null : power;
            Carry = string.IsNullOrEmpty(carry) ? null : carry;
        }

        /// <summary>Sheet suffix and sidecar key: <c>reel</c>, <c>cast_short</c>, <c>walk_buckets</c>.</summary>
        public string Key =>
            Anim + (Power == null ? "" : "_" + Power) + (Carry == null ? "" : "_" + Carry);

        public override string ToString() => Key;

        public static implicit operator CharacterState(string anim) => new CharacterState(anim);
    }

    public sealed class CharacterSheetBake
    {
        public string State;
        public string AssetPath;
        public int Width, Height, Frames;
        public override string ToString() =>
            $"{AssetPath}  {Width}×{Height}  ({Frames} frames × 8 direction rows)";
    }

    public sealed class CharacterBakeResult
    {
        public string RigKey;
        public string EngineName;
        /// <summary>The preset key this run baked, or null for the rig's DEFAULT_BUILD.</summary>
        public string BuildPreset;
        public RigGeometry Geometry;
        public AzimuthConvention MeasuredConvention;
        public string ConventionReport;
        public readonly List<CharacterSheetBake> Sheets = new List<CharacterSheetBake>();
        public string AnchorJsonPath;
        public double RenderMilliseconds;
        public double TotalMilliseconds;
        public long TotalPngBytes;
        public int CellsRendered;

        /// <summary>Uncompressed RGBA32 footprint of every sheet this run wrote — what the sheets
        /// actually cost in texture memory once imported, which the PNG size does not tell you.</summary>
        public long TotalRgba32Bytes
        {
            get
            {
                long n = 0;
                foreach (var s in Sheets) n += (long)s.Width * s.Height * 4;
                return n;
            }
        }
    }

    /// <summary>
    /// Runs a CHARACTER rig and writes 8-direction ANIMATION sheets — the sibling of
    /// <see cref="RigBaker"/>, which is a boat TURNTABLE (N facings × rock frames) and cannot
    /// express "rows are directions, columns are animation frames driven by
    /// <c>render(dir, {anim, frame})</c>". The two stay separate deliberately: folding an anim axis
    /// into the turntable request would complicate the proven boat path for no shared code beyond
    /// the blit, which IS shared (<see cref="RigBaker.Blit"/>).
    ///
    /// Sheet contract (must match <c>CharacterSheetSlicer</c>, which slices these untouched):
    /// 8 rows = directions, row d at the TOP of the canvas is direction d; N columns = the anim's
    /// frames, read from the rig's own ANIMS table (ADR 0021 §4 — geometry from the rig, never a
    /// README); cell = the rig's W×H.
    ///
    /// ⚠️ CONVENTION: nothing here trusts a declaration. <see cref="CharacterRigAzimuthProbe"/>
    /// measures the convention from rendered pixels and the bake REFUSES on a mismatch with the
    /// catalog, exactly like the boat path. <see cref="RigBaker.DirForCell"/> is still used for the
    /// emission so the correction logic lives in one place — for a measured-clockwise rig it is the
    /// identity, and re-mirroring already-correct art is the exact blanket-fix mistake that method
    /// documents.
    ///
    /// Like the boat baker this deliberately stops at "PNG (+ anchors JSON) on disk": slicing is
    /// <c>CharacterSheetSlicer</c>'s job and import settings are <c>ArtImportPipeline</c>'s.
    /// </summary>
    public static class CharacterRigBaker
    {
        /// <summary>
        /// The DEFAULT importer texture cap. Boat pages check against Unity's hard 4096 cap because
        /// <c>SpriteSheetSlicer</c> lifts maxTextureSize from its manifest; the character slicer
        /// does no such lift, so a character sheet must fit under the untouched default — over it,
        /// the sheet imports SILENTLY DOWNSCALED while the sprite COUNT still matches, and only the
        /// slice tests' dimension/pivot asserts would ever notice.
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>
        /// Margin around each cell, in px. <b>Zero, deliberately, and it is not an oversight.</b>
        ///
        /// <para>The pass-6 kit README bakes with <c>PAD = 24</c> because its browser harness
        /// composites the PROP LAYER into the same cell — the rod, the spade and the pails overhang
        /// the body's 64 × 92 and would clip without the margin. <b>This baker composites no props.</b>
        /// Props are separate sheets pinned at runtime from the <c>tool</c>/<c>carry</c> anchors in the
        /// sidecar (<c>RodFightPresenter</c>, the outboard-motor mount pattern) — which is what lets
        /// the rod tier change without re-baking the fisher.</para>
        ///
        /// <para>So padding here would buy nothing: the rig renders INTO a W×H buffer, so anything
        /// outside the cell is already clipped at source and a wider sheet cannot recover it. What it
        /// would cost is real — a 112 × 140 cell is <b>2.66×</b> the pixels of 64 × 92, on every sheet
        /// of every build, for margin that is transparent in all of them. If a future bake ever does
        /// composite a prop, this is the constant to raise, and the sidecar's cell px must move with
        /// it.</para>
        /// </summary>
        public const int PadPx = 0;

        /// <summary>
        /// Host-side helper installed before any anchor is read: rounds every number in a
        /// <c>JSON.stringify</c> to 3 decimals (≈ 1/1000 px, three orders below anything the art can
        /// express). Raw <c>JSON.stringify</c> emits full double precision — <c>31.999999999999996</c>
        /// — which multiplies the sidecar's size and makes its diff churn on floating-point noise no
        /// consumer can act on.
        ///
        /// <para>This is HOST code, in the baker, named out of the rigs' way. The art director's files
        /// are still executed unmodified (ADR 0021 §5) — same standing as
        /// <c>CatchStorageBaker.CanvasShimJs</c>.</para>
        /// </summary>
        const string RoundingHelperJs =
            "globalThis.__hhRound3 = function (k, v) { " +
            "return typeof v === 'number' && isFinite(v) ? Math.round(v * 1000) / 1000 : v; };";

        /// <summary>
        /// Bake one sheet per state, plus one combined anchors JSON. One host and one convention
        /// probe serve the whole batch — the states all come off the same rig.
        /// </summary>
        /// <param name="rigKey">Catalog key, e.g. "character".</param>
        /// <param name="states">Anim (+ optional power) per sheet.</param>
        /// <param name="outputFolder">Project-relative, e.g. "Assets/_Project/Art/Characters/Iso".</param>
        /// <param name="baseNamePrefix">Sheet file prefix, e.g. "Fisher_" → Fisher_bite.png.</param>
        /// <param name="anchorFileName">File name for the combined anchors JSON, e.g.
        /// "FisherFightAnchors.json". Null/empty skips anchors.</param>
        /// <param name="buildPreset">A key of the rig's own BUILDS table (e.g. "nan"), or null to
        /// bake the rig's DEFAULT_BUILD. Validated against the rig before anything is written.</param>
        /// <param name="withCarryStances">Also bake, for every FREE state in the recipe, one sheet
        /// per carry stance the rig's own CARRIES table rides on that anim. The stances are never
        /// listed by a caller — they are read from the rig, so a kit that adds one grows a sheet with
        /// no code change.</param>
        /// <param name="carryStanceExclusions">Anims to leave OUT of that expansion, baked free-handed
        /// only. A BUDGET lever, never a correctness one: the rig's table is still the authority on
        /// which stance may ride which anim, and this only declines art the game cannot reach yet
        /// (CLAUDE.md rule 7 and rule 8). Null/empty expands everything, which is the old behaviour
        /// exactly.</param>
        /// <param name="progress">Optional (label, 0..1) callback for a progress bar.</param>
        public static CharacterBakeResult Bake(string rigKey, IReadOnlyList<CharacterState> states,
                                               string outputFolder, string baseNamePrefix,
                                               string anchorFileName = null,
                                               string buildPreset = null,
                                               bool withCarryStances = false,
                                               ICollection<string> carryStanceExclusions = null,
                                               Action<string, float> progress = null)
        {
            if (states == null || states.Count == 0)
                throw new ArgumentException("Nothing to bake — no states named.", nameof(states));

            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get(rigKey);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            host.Execute(RoundingHelperJs);
            string g = entry.GlobalName;

            // The rev-6.4 hand-prop anchor layer, plus the prop rigs its table asks questions of.
            // Best-effort and AFTER the body (it reads the body's camera and anchors) — a kit without
            // it still bakes every sheet and every existing anchor, and simply writes no handProps
            // block. See AppendHandProps.
            InstallHandPropLayer(host);

            var result = new CharacterBakeResult
            {
                RigKey = rigKey,
                EngineName = host.EngineName,
                BuildPreset = buildPreset,
                Geometry = geo,
            };

            // ---- MEASURE the azimuth convention from pixels, then cross-check the declaration ----
            var probe = CharacterRigAzimuthProbe.Measure(host, g, geo);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;

            if (probe.Convention != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"AZIMUTH MISMATCH on rig '{rigKey}'.\n" +
                    $"  the catalog declares       : {entry.DeclaredConvention}\n" +
                    $"  the rendered pixels say    : {probe.Convention}\n\n" +
                    probe.Report + "\n\n" +
                    "The bake is refusing rather than guessing. A silent guess here is how this " +
                    "mislabel shipped defects in five kits — and this rig has been mislabelled " +
                    "twice before being fixed at source. Look at the art, decide which is right, " +
                    "and correct the catalog (or the rig) — do not relax this check.");

            // Grow the recipe by the stances the RIG rides, before validation, so the expansion is
            // held to the same refusals a hand-written recipe is.
            if (withCarryStances) states = ExpandCarryStances(host, g, states, carryStanceExclusions);

            // Validate the WHOLE recipe against the rig before writing anything, so a mistyped
            // state name (or build key, or an illegal carry) fails with zero files on disk rather
            // than a half-baked set.
            ValidateBuild(host, g, buildPreset);
            foreach (var state in states)
            {
                FramesOf(host, g, state.Anim);
                ValidateCarry(host, g, state);
            }

            int dirs = geo.NativeDirs;
            var renderClock = new Stopwatch();
            string outAbs = Path.Combine(RigCatalog.RepoRoot, outputFolder);
            Directory.CreateDirectory(outAbs);

            // ---- Bake one sheet per state ------------------------------------------------------
            for (int a = 0; a < states.Count; a++)
            {
                CharacterState state = states[a];
                progress?.Invoke($"{baseNamePrefix}{state.Key}", (float)a / states.Count);

                int frames = FramesOf(host, g, state.Anim);
                int cellW = geo.Width + PadPx * 2, cellH = geo.Height + PadPx * 2;
                int pw = frames * cellW;
                int ph = dirs * cellH;
                if (pw > ImportSizeCap || ph > ImportSizeCap)
                    throw new InvalidOperationException(
                        $"'{state.Key}' would bake to {pw}×{ph}, over the {ImportSizeCap} default " +
                        "import cap. Unity would import it DOWNSCALED with a matching sprite count, " +
                        "so this would only surface as a slice-test dimension failure much later. " +
                        "Split the state or page the sheet before raising this limit.");

                var pixels = new Color32[pw * ph];

                for (int d = 0; d < dirs; d++)
                {
                    // The one-place correction: identity for a measured-clockwise rig.
                    double dir = RigBaker.DirForCell(d, dirs, probe.Convention);

                    for (int f = 0; f < frames; f++)
                    {
                        renderClock.Start();
                        byte[] rgba = host.EvaluateBytes(
                            $"{g}.render({Num(dir)},{OptsJs(state, f, buildPreset)})");
                        renderClock.Stop();
                        result.CellsRendered++;

                        if (rgba.Length != geo.Width * geo.Height * 4)
                            throw new InvalidOperationException(
                                $"'{state.Key}' dir {d} frame {f} came back {rgba.Length} bytes, " +
                                $"expected {geo.Width * geo.Height * 4} for " +
                                $"{geo.Width}×{geo.Height} RGBA.");

                        RigBaker.Blit(rgba, geo.Width, geo.Height, pixels, pw, ph,
                                      col: f, rowFromTop: d);
                    }
                }

                string assetPath = $"{outputFolder}/{baseNamePrefix}{state.Key}.png";
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

                result.Sheets.Add(new CharacterSheetBake
                {
                    State = state.Key, AssetPath = assetPath,
                    Width = pw, Height = ph, Frames = frames,
                });
            }

            // ---- Anchors (the boat WriteAnchors pattern, per state × dir × frame) ---------------
            if (!string.IsNullOrEmpty(anchorFileName))
                result.AnchorJsonPath = WriteAnchors(host, entry, geo, states, probe.Convention,
                                                     buildPreset, outputFolder, anchorFileName);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Convenience overload for a recipe of plain anim names (no power variants).</summary>
        public static CharacterBakeResult Bake(string rigKey, IReadOnlyList<string> anims,
                                               string outputFolder, string baseNamePrefix,
                                               string anchorFileName = null,
                                               string buildPreset = null,
                                               bool withCarryStances = false,
                                               ICollection<string> carryStanceExclusions = null,
                                               Action<string, float> progress = null)
        {
            if (anims == null || anims.Count == 0)
                throw new ArgumentException("Nothing to bake — no anims named.", nameof(anims));
            var states = new CharacterState[anims.Count];
            for (int i = 0; i < anims.Count; i++) states[i] = new CharacterState(anims[i]);
            return Bake(rigKey, states, outputFolder, baseNamePrefix, anchorFileName, buildPreset,
                        withCarryStances, carryStanceExclusions, progress);
        }

        /// <summary>Frame count of one anim, from the rig's own ANIMS table — never a README.</summary>
        public static int FramesOf(IRigScriptHost host, string globalName, string anim)
        {
            string animJs = JsString(anim);
            if (!host.EvaluateBool($"typeof {globalName}.ANIMS[{animJs}] === 'object' && " +
                                   $"{globalName}.ANIMS[{animJs}] !== null"))
            {
                string known = host.EvaluateString(
                    $"Object.keys({globalName}.ANIMS).join(', ')");
                throw new ArgumentException(
                    $"Rig '{globalName}' declares no anim '{anim}'. Known: {known}. " +
                    "If the state genuinely does not exist yet, that is an art-director rig change, " +
                    "not a baker workaround.");
            }
            int frames = (int)host.EvaluateNumber($"{globalName}.ANIMS[{animJs}].frames");
            if (frames <= 0)
                throw new InvalidOperationException($"Anim '{anim}' declares {frames} frames.");
            return frames;
        }

        /// <summary>
        /// Which layer an anim drives, from the rig's own ANIM_MOUNT table: <c>free</c> / <c>rod</c> /
        /// <c>shovel</c> / <c>ladder</c>. A missing table means the rig changed shape, and defaulting to
        /// "free" would write a sidecar with no tool pins that still looks complete — so it refuses
        /// instead.
        ///
        /// <para><c>ladder</c> arrived with rev 6.2 and is neither free nor a tool: both hands are
        /// committed to the rungs, so no carry stance may ride the clip and no prop layer mounts on it.
        /// Everything here already does the right thing with it by treating "not free" as "carry is
        /// refused" — <see cref="ExpandCarryStances"/> skips it and <see cref="ValidateCarry"/> rejects
        /// a hand-written one, which is exactly the rig's statement.</para>
        /// </summary>
        public static string MountOf(IRigScriptHost host, string globalName, string anim)
        {
            if (!host.EvaluateBool($"typeof {globalName}.ANIM_MOUNT === 'object' && " +
                                   $"{globalName}.ANIM_MOUNT !== null"))
                throw new InvalidOperationException(
                    $"Rig '{globalName}' declares no ANIM_MOUNT table. Pass 6 states the mount " +
                    "contract in the rig precisely so a baker never has to guess it — a rig without " +
                    "one is a shape change, not something to default around.");

            return host.EvaluateString(
                $"String({globalName}.ANIM_MOUNT[{JsString(anim)}] || 'free')");
        }

        /// <summary>
        /// The recipe, plus one sheet per (free state × stance the rig rides on it). A state that
        /// already names a stance is passed through untouched; a tool state contributes nothing,
        /// because the rig ignores carry there.
        ///
        /// <para>The stance list comes from the rig's CARRIES table — <c>helm</c> and <c>oars</c>
        /// ride idle and walk but not a run, and that is the rig's statement about its own art, not
        /// a rule worth restating in a menu.</para>
        /// </summary>
        static IReadOnlyList<CharacterState> ExpandCarryStances(
            IRigScriptHost host, string g, IReadOnlyList<CharacterState> states,
            ICollection<string> exclusions = null)
        {
            var grown = new List<CharacterState>(states.Count * 2);
            foreach (var state in states)
            {
                grown.Add(state);
                if (state.Carry != null) continue;
                if (exclusions != null && exclusions.Contains(state.Anim)) continue;
                if (MountOf(host, g, state.Anim) != "free") continue;
                foreach (string stance in CarriesFor(host, g, state.Anim))
                    grown.Add(new CharacterState(state.Anim, state.Power, stance));
            }
            return grown;
        }

        /// <summary>
        /// Refuse a carry stance the rig would silently ignore. Two ways that happens, both of which
        /// bake a duplicate of the free sheet under a name that promises a stance: the anim drives a
        /// TOOL (the tool always wins and <c>pose()</c> drops the carry), or the rig's own CARRIES
        /// table does not list that anim for that stance (helm and oars do not ride a run).
        /// </summary>
        static void ValidateCarry(IRigScriptHost host, string g, in CharacterState state)
        {
            if (state.Carry == null) return;

            string mount = MountOf(host, g, state.Anim);
            if (mount != "free")
                throw new ArgumentException(
                    $"'{state.Anim}' drives the {mount} layer, so the rig IGNORES carry on it — a " +
                    $"'{state.Key}' sheet would be a byte-identical copy of '{state.Anim}' under a " +
                    "name that claims a stance. Carry stances ride free anims only.");

            var allowed = CarriesFor(host, g, state.Anim);
            bool ok = false;
            foreach (string c in allowed) if (c == state.Carry) { ok = true; break; }
            if (!ok)
                throw new ArgumentException(
                    $"The rig's CARRIES table does not ride '{state.Carry}' on '{state.Anim}'. It " +
                    $"allows: {(allowed.Count == 0 ? "(none)" : string.Join(", ", allowed))}. The " +
                    "rig would resolve the stance and then not use it — a sheet that looks like a " +
                    "stance and is not one.");
        }

        /// <summary>Refuse a build key the rig's BUILDS table does not carry — before any file lands.</summary>
        static void ValidateBuild(IRigScriptHost host, string g, string buildPreset)
        {
            if (string.IsNullOrEmpty(buildPreset)) return;
            if (host.EvaluateBool($"typeof {g}.BUILDS[{JsString(buildPreset)}] === 'object' && " +
                                  $"{g}.BUILDS[{JsString(buildPreset)}] !== null")) return;

            string known = host.EvaluateString($"Object.keys({g}.BUILDS).join(', ')");
            throw new ArgumentException(
                $"Rig '{g}' has no build preset '{buildPreset}'. Known: {known}. " +
                "The rig resolves an unknown preset key to DEFAULT_BUILD in silence, which would " +
                "bake ten identical fishers under ten cast names — refusing instead.");
        }

        /// <summary>
        /// The opts literal for one cell: anim, frame, the optional power sub-range, the state's
        /// carry stance and the optional build preset. <b>Every call site uses this one</b> — the
        /// pixels, the body anchors, the tool pins and the carry pins all describe the SAME opts, so
        /// a pin can never belong to a pose the sheet does not show. Anything omitted falls through
        /// to the rig's own defaults, which is the documented contract — but a preset is never
        /// omitted silently, see <see cref="ValidateBuild"/>.
        /// </summary>
        static string OptsJs(CharacterState state, int frame, string buildPreset)
        {
            var sb = new StringBuilder("{anim:");
            sb.Append(JsString(state.Anim));
            sb.Append(",frame:").Append(frame.ToString(CultureInfo.InvariantCulture));
            if (state.Power != null) sb.Append(",power:").Append(JsString(state.Power));
            if (state.Carry != null) sb.Append(",carry:").Append(JsString(state.Carry));
            if (!string.IsNullOrEmpty(buildPreset))
                sb.Append(",build:{preset:").Append(JsString(buildPreset)).Append('}');
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// The combined sidecar: for every state, the body <c>anchors</c> per direction row per frame,
        /// plus the pin its mount needs — <c>tool</c> (grip, wrist, pitch/yaw/bend) for a rod or shovel
        /// state, <c>carry</c> (the hand/mid pins and the pail pendulum) for a state baked in a carry
        /// stance. A free state with no stance gets anchors only: there is nothing in its hands.
        ///
        /// <para>Every pin is computed from the SAME opts as the pixels of the cell it belongs to, so
        /// a stance's hand positions always describe the body the sheet actually shows.</para>
        ///
        /// <para>Additive over the shape <c>RodKitImporter</c> already parses (<c>pivotTopLeft</c>,
        /// <c>states.&lt;key&gt;.anchors</c>): MiniJson reads only the keys it names, so existing
        /// consumers see no change. Numbers are rounded to 3 decimals by the host-side replacer.</para>
        /// </summary>
        static string WriteAnchors(IRigScriptHost host, in RigEntry entry, in RigGeometry geo,
                                   IReadOnlyList<CharacterState> states, AzimuthConvention convention,
                                   string buildPreset, string outputFolder, string anchorFileName)
        {
            string g = entry.GlobalName;
            int dirs = geo.NativeDirs;

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"rig\": \"{entry.ScriptPath}\",\n");
            sb.Append($"  \"global\": \"{g}\",\n");
            sb.Append($"  \"build\": \"{buildPreset ?? "default"}\",\n");
            sb.Append($"  \"cell\": {{ \"w\": {geo.Width}, \"h\": {geo.Height} }},\n");
            sb.Append($"  \"padPx\": {PadPx},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {Num(geo.PivotX)}, \"y\": {Num(geo.PivotY)} }},\n");
            sb.Append($"  \"dirs\": {dirs},\n");
            sb.Append($"  \"measuredRigConvention\": \"{convention}\",\n");
            sb.Append("  \"facingsAreCounterClockwise\": false,\n");
            sb.Append("  \"_note\": \"Baked in-engine with the rig's measured convention applied, so row d of every sheet depicts heading 360*d/dirs. Anchor cell px are TOP-LEFT origin, per direction row then per frame column. 'tool' is the prop grip for the rod/shovel states; 'carry' holds one block per stance the rig allows on that anim. 'clipPins' holds the clip-side pins named by 'clipPinSource' (boardMount / haulGrip / ladderMount). Props pin from these at RUNTIME and are never baked into the sheet.\",\n");
            sb.Append("  \"_clipNote\": \"The boarding clips re-solve per railZ and are baked here at the rig's DEFAULT 0.55 m (a dory sheer) — a hull with a different sheer wants its own sheet, per the kit README's 'bake one sheet per rail height you ship'. The ladder clip is baked at the rig's default rung 0.30 m / width 0.45 m (WharfIso.FIT.ladder).\",\n");
            sb.Append("  \"states\": {\n");

            for (int a = 0; a < states.Count; a++)
            {
                CharacterState state = states[a];
                int frames = FramesOf(host, g, state.Anim);
                string mount = MountOf(host, g, state.Anim);

                sb.Append($"    \"{state.Key}\": {{ \"anim\": \"{state.Anim}\", ");
                if (state.Power != null) sb.Append($"\"power\": \"{state.Power}\", ");
                if (state.Carry != null) sb.Append($"\"carry\": \"{state.Carry}\", ");
                sb.Append($"\"frames\": {frames}, \"ms\": {Num(MsOf(host, g, state.Anim))}, ");
                sb.Append($"\"mount\": \"{(state.Carry ?? mount)}\",\n");

                sb.Append("      \"anchors\": ");
                AppendDirFrameGrid(sb, host, g, dirs, frames, convention,
                                   (d, f) => $"{g}.anchors({d},{OptsJs(state, f, buildPreset)})");

                if (mount == "rod" || mount == "shovel")
                {
                    sb.Append(",\n      \"tool\": ");
                    AppendDirFrameGrid(sb, host, g, dirs, frames, convention,
                                       (d, f) => $"{g}.tool({d},{OptsJs(state, f, buildPreset)})");
                }
                else if (state.Carry != null)
                {
                    sb.Append(",\n      \"carryPins\": ");
                    AppendDirFrameGrid(sb, host, g, dirs, frames, convention,
                                       (d, f) => $"{g}.carry({d},{OptsJs(state, f, buildPreset)})");
                }

                // The CLIP pins (rev 6.1 / 6.2). Independent of the two branches above rather than an
                // else-arm of them: a boarding clip baked in a carry stance has BOTH a carry pin (where
                // the pail hangs) and a clip pin (where the plant hand meets the rail), and they are
                // different questions about the same cell.
                string clipPin = ClipPinCallFor(host, g, state.Anim);
                if (clipPin != null)
                {
                    sb.Append($",\n      \"clipPinSource\": \"{clipPin}\",\n      \"clipPins\": ");
                    AppendDirFrameGrid(sb, host, g, dirs, frames, convention,
                                       (d, f) => $"{g}.{clipPin}({d},{OptsJs(state, f, buildPreset)})");
                }

                sb.Append(a < states.Count - 1 ? "\n    },\n" : "\n    }\n");
            }

            sb.Append("  },\n");
            AppendHandProps(sb, host, geo, convention, buildPreset);
            sb.Append("}\n");

            string assetPath = $"{outputFolder}/{anchorFileName}";
            File.WriteAllText(Path.Combine(RigCatalog.RepoRoot, assetPath), sb.ToString());
            return assetPath;
        }

        /// <summary>
        /// Load the hand-prop anchor layer and the prop rigs it interrogates.
        ///
        /// <para><b>Order is the whole contract.</b> <c>characterHands</c> declares the body as its
        /// prerequisite, so installing it here — after <see cref="RigCatalog.Install"/> has already put
        /// the body in — is idempotent and correct. Loaded BEFORE the body it would not throw: its
        /// <c>C()</c> resolves to null and every <c>pin()</c> silently returns null, which is the
        /// failure mode the prerequisite exists to make impossible.</para>
        ///
        /// <para><b>Why the prop rigs come too.</b> One row is not a constant: <c>fish</c> declares
        /// <c>hands:'auto'</c> and resolves it by asking <c>FishIso.hold(species, scale)</c> whether
        /// that species is a one-hand gill grip or a two-hand cradle. Without <c>FishIso</c> in the
        /// host the string <c>'auto'</c> falls through into the sidecar unresolved — not an error, just
        /// a number nobody can use. The others are loaded for the same reason in principle and cost a
        /// file read each.</para>
        ///
        /// <para><b>Best-effort by design.</b> A rig the catalog does not know (the shovel and the rope
        /// coil have no registration yet) is SKIPPED, not fatal: the hand-prop table names seven props
        /// and only some have art, which is the drop's own stated position. A missing prop rig costs
        /// its own row's precision and nothing else.</para>
        /// </summary>
        static void InstallHandPropLayer(IRigScriptHost host)
        {
            foreach (string key in HandPropRigKeys)
            {
                if (!RigCatalog.Has(key)) continue;
                try { RigCatalog.InstallModule(host, RigCatalog.Get(key)); }
                catch (Exception e)
                {
                    // A prop rig that fails to load costs its own row's precision. Refusing the whole
                    // character bake over it would be a worse trade — every sheet and every existing
                    // anchor is independent of this layer.
                    UnityEngine.Debug.LogWarning(
                        $"[CharacterRigBaker] hand-prop layer: rig '{key}' did not load — " +
                        $"rows that ask it a question fall back to the table's literal. {e.Message}");
                }
            }
        }

        /// <summary>
        /// The anchor layer, then the prop rigs its rows interrogate. <c>characterHands</c> is FIRST
        /// so its own prerequisite chain (eye → head → body) is satisfied before anything else runs.
        ///
        /// <para>Not every prop in the table appears here, and that is the drop's own position rather
        /// than an omission: <c>knife</c> and <c>gaff</c> have no rig at all (their rows carry a
        /// <c>stub</c> spec instead), the carried rope coil is a half-scaled deck prop awaiting its own
        /// bake, and <c>shovelIsoRig.js</c> is committed but not yet registered in this catalog.</para>
        /// </summary>
        static readonly string[] HandPropRigKeys = { "characterHands", "rod", "fish", "shellfish" };

        /// <summary>
        /// The rev-6.4 HAND-PROP anchors: for each carried object, the eight per-facing rows saying
        /// which hand holds it at that heading, how far off the wrist it sits, how it is angled, which
        /// cell of its own turntable to draw, and whether it draws over or under the sprite.
        ///
        /// <para><b>Why this is a sibling of <c>states</c> and not inside it.</b> The prop's own grip
        /// offset is a function of (prop, dir) ONLY — the rig projects a body-local metre offset through
        /// the camera basis, which depends on dir/elev/roll/pitch and not on the anim or the frame. So
        /// there is exactly one table, not one per state, and folding it into <c>states</c> would bake
        /// the same eight rows twenty-nine times over.</para>
        ///
        /// <para><b>The two point fields are DIFFERENT QUANTITIES and the names say so.</b>
        /// <c>gripDx/gripDy</c> is the frame-independent offset FROM THE WRIST — add it to the per-frame
        /// <c>anchors</c> grid above and a carried object tracks the hand as it swings.
        /// <c>restX/restY</c> is the absolute pin in cell px at the REST POSE ONLY (idle frame 0, named
        /// in <c>restPose</c>), for a consumer that hangs the object off the body rather than off a
        /// tracked hand. Reading <c>restX</c> as if it were live is the one mistake this block can
        /// invite, which is why it is not called <c>x</c>.</para>
        ///
        /// <para>Silent no-op when the hand-prop layer is not in the host — a rig kit without it is an
        /// older kit, not a broken one, and an absent block reads as absent rather than as
        /// <c>undefined</c>.</para>
        /// </summary>
        static void AppendHandProps(StringBuilder sb, IRigScriptHost host, in RigGeometry geo,
                                    AzimuthConvention convention, string buildPreset)
        {
            const string H = "CharacterHands6";
            if (!host.EvaluateBool($"typeof {H} === 'object' && {H} !== null"))
            {
                sb.Append("  \"handProps\": null,\n");
                return;
            }

            int dirs = geo.NativeDirs;
            string build = string.IsNullOrEmpty(buildPreset)
                ? "{}" : $"{{preset:{JsString(buildPreset)}}}";

            sb.Append("  \"handProps\": {\n");
            sb.Append($"    \"pass\": {Num(host.EvaluateNumber($"{H}.pass"))},\n");
            sb.Append($"    \"torsoHalfMetres\": {Num(host.EvaluateNumber($"{H}.TORSO_HALF"))},\n");
            sb.Append($"    \"restPose\": {{ \"anim\": \"{HandPropRestAnim}\", \"frame\": {HandPropRestFrame} }},\n");
            sb.Append("    \"_note\": \"Per-facing anchors for a CARRIED object (CharacterHands6, rev 6.4). gripDx/gripDy is the prop's offset FROM THE WRIST in cell px and is frame-INDEPENDENT — add it to the per-frame 'anchors' grid to track a swinging hand. restX/restY is the absolute pin in cell px at the rest pose named above, for a consumer that hangs the object off the body instead. 'hand' is already swapped for the heading; 'itemDir' is the cell of the PROP's own turntable to draw (a turntable prop has no yaw, only eight cells); 'behind' means draw under the sprite. A prop whose 'rig' is null has no art baked yet and carries its spec in 'stub'.\",\n");
            sb.Append("    \"props\": {\n");

            string[] props = host.EvaluateString($"{H}.PROP_ORDER.join(',')").Split(',');
            for (int p = 0; p < props.Length; p++)
            {
                // ⚠️ TWO different quotings of the same word, and they are not interchangeable.
                // `js` is a JS string literal (single-quoted, JsString's job) and is only ever passed
                // INTO an expression the rig host evaluates. The KEY written here is JSON, which
                // admits double quotes only — emitting JsString here produces 'rodTrail': …, which
                // every JS engine accepts and every JSON parser rejects at exactly that column.
                string prop = props[p], js = JsString(prop);
                sb.Append($"      \"{prop}\": {{ ");
                sb.Append($"\"label\": {host.EvaluateString($"JSON.stringify({H}.PROPS[{js}].label)")}, ");
                sb.Append($"\"mount\": {host.EvaluateString($"JSON.stringify({H}.PROPS[{js}].mount)")}, ");
                sb.Append($"\"rig\": {host.EvaluateString($"JSON.stringify({H}.PROPS[{js}].rig || null)")}, ");
                sb.Append($"\"itemScale\": {Num(host.EvaluateNumber($"{H}.PROPS[{js}].itemScale || 1"))}, ");
                sb.Append($"\"item\": {host.EvaluateString($"JSON.stringify({H}.item({js}), __hhRound3)")}, ");
                sb.Append($"\"stub\": {host.EvaluateString($"JSON.stringify({H}.PROPS[{js}].stub || null)")}, ");
                sb.Append($"\"rest\": {host.EvaluateString($"JSON.stringify({H}.PROPS[{js}].rest || null)")}, ");
                sb.Append($"\"note\": {host.EvaluateString($"JSON.stringify({H}.PROPS[{js}].note || null)")},\n");
                sb.Append("        \"rows\": [\n");

                for (int d = 0; d < dirs; d++)
                {
                    // The SAME DirForCell correction the pixels take, so row d of this table describes
                    // the heading row d of every sheet depicts. Identity for a clockwise rig — but
                    // written out rather than assumed, because that is the assumption this lane has
                    // got wrong twice.
                    string ds = Num(RigBaker.DirForCell(d, dirs, convention));
                    string opts = $"{{anim:{JsString(HandPropRestAnim)},frame:{HandPropRestFrame}," +
                                  $"elev:{Num(geo.DefaultElevation)},build:{build}}}";
                    sb.Append("          ").Append(host.EvaluateString(
                        $"(function(){{var p={H}.pin({js},{ds},{opts});return JSON.stringify({{" +
                        "dir:p.dir,facing:p.facing,hand:p.hand,hands:p.hands,mode:p.mode," +
                        "gripDx:p.dx,gripDy:p.dy,restX:p.x,restY:p.y," +
                        "yaw:p.yaw,pitch:p.pitch,bend:p.bend,turn:p.turn,itemDir:p.itemDir," +
                        // ⚠️ This segment is NOT interpolated, so a brace here is LITERAL. The
                        // interpolated first segment above needs `{{` to emit one `{`; this one needs
                        // a single `}`. Writing `}}` here (the symmetry your eye wants) emits two and
                        // the rig host dies with "SyntaxError: Unexpected token '}'" at bake time —
                        // invisible to the compiler, because the string is only assembled at runtime.
                        "behind:p.behind,swapped:p.swapped},__hhRound3);})()"));
                    sb.Append(d < dirs - 1 ? ",\n" : "\n");
                }

                sb.Append("        ]\n      }");
                sb.Append(p < props.Length - 1 ? ",\n" : "\n");
            }

            sb.Append("    }\n  }\n");
        }

        /// <summary>
        /// The pose the absolute <c>restX/restY</c> pins are measured at. Idle frame 0 is the rig's
        /// rest stance — the pose a consumer that does not track the hand per frame should hang a
        /// carried object from. Named as constants so the sidecar can STATE which pose it baked
        /// rather than leaving a reader to infer it.
        /// </summary>
        public const string HandPropRestAnim = "idle";

        /// <inheritdoc cref="HandPropRestAnim"/>
        public const int HandPropRestFrame = 0;

        /// <summary>One <c>[dir][frame]</c> grid of whatever the expression returns, JSON-stringified
        /// through the rounding replacer. Rows follow the same DirForCell correction the pixels do, so
        /// an anchor never describes a different heading than the cell it belongs to.</summary>
        static void AppendDirFrameGrid(StringBuilder sb, IRigScriptHost host, string g,
                                       int dirs, int frames, AzimuthConvention convention,
                                       Func<string, int, string> expression,
                                       string indent = "        ")
        {
            sb.Append("[\n");
            for (int d = 0; d < dirs; d++)
            {
                string ds = Num(RigBaker.DirForCell(d, dirs, convention));
                sb.Append(indent).Append('[');
                for (int f = 0; f < frames; f++)
                {
                    sb.Append(host.EvaluateString(
                        $"JSON.stringify({expression(ds, f)}, __hhRound3)"));
                    if (f < frames - 1) sb.Append(", ");
                }
                sb.Append(d < dirs - 1 ? "],\n" : "]\n");
            }
            sb.Append(indent.Substring(2)).Append(']');
        }

        /// <summary>
        /// The rig function that returns this anim's CLIP pins, or null for an anim that has none.
        /// Rev 6.1 added <c>boardMount()</c> (the rail plant, the landing re-seat) and
        /// <c>haulGrip()</c> (the two rope pins and the tension envelope); rev 6.2 added
        /// <c>ladderMount()</c> (the rung locks, the standoff and the per-frame descent). They are the
        /// clip-side twins of <c>tool()</c> and <c>carry()</c>, and the ladder gameplay slice reads
        /// them straight out of the sidecar rather than re-deriving a climber's contact points.
        ///
        /// <para><b>Asked of the rig, not of a README</b> (ADR 0021 §4): the boarding and ladder clips
        /// are read from the rig's own <c>BOARDING</c> / <c>LADDER</c> tables. <c>haul</c> is matched by
        /// NAME because the rig declares no table for it — there is exactly one haul clip — and the
        /// match is guarded by a <c>typeof</c> check, so a rig without <c>haulGrip</c> writes no pins
        /// rather than emitting <c>undefined</c> into the sidecar.</para>
        /// </summary>
        static string ClipPinCallFor(IRigScriptHost host, string g, string anim)
        {
            string a = JsString(anim);
            if (host.EvaluateBool($"typeof {g}.boardMount === 'function' && " +
                                  $"!!({g}.BOARDING && {g}.BOARDING[{a}])"))
                return "boardMount";
            if (host.EvaluateBool($"typeof {g}.ladderMount === 'function' && " +
                                  $"!!({g}.LADDER && {g}.LADDER[{a}])"))
                return "ladderMount";
            if (anim == "haul" && host.EvaluateBool($"typeof {g}.haulGrip === 'function'"))
                return "haulGrip";
            return null;
        }

        /// <summary>The carry stances the rig's own CARRIES table allows on this anim, in
        /// CARRY_ORDER. Empty for a tool anim (a tool always wins and carry is ignored).</summary>
        static IReadOnlyList<string> CarriesFor(IRigScriptHost host, string g, string anim)
        {
            if (!host.EvaluateBool($"typeof {g}.CARRIES === 'object' && {g}.CARRIES !== null &&" +
                                   $" Array.isArray({g}.CARRY_ORDER)"))
                return Array.Empty<string>();

            string csv = host.EvaluateString(
                $"{g}.CARRY_ORDER.filter(function (c) {{ var e = {g}.CARRIES[c]; " +
                $"return e && Array.isArray(e.anims) && e.anims.indexOf({JsString(anim)}) >= 0; }})" +
                ".join(',')");
            return string.IsNullOrEmpty(csv) ? Array.Empty<string>() : csv.Split(',');
        }

        static double MsOf(IRigScriptHost host, string g, string anim) =>
            host.EvaluateNumber($"{g}.ANIMS[{JsString(anim)}].ms");

        /// <summary>Single-quoted JS string literal for an anim name (names are plain identifiers,
        /// but escape defensively — a quote in a name must not become an injection).</summary>
        static string JsString(string s) =>
            "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
