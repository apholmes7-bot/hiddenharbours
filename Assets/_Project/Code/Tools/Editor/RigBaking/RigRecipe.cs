using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>THE RECIPE — what a baked sheet says about how it was baked.</b>
    ///
    /// <para>Every sheet under <c>Assets/_Project/Art</c> is the output of a rig call: a rig, a
    /// function, a set of <c>dir</c> values and an <c>opts</c> dict per variant axis. Until this
    /// existed, none of that survived the bake. The scene-export packages (#588) emit a
    /// <c>rig</c> + <c>call</c> block for every placement they resolve and have to leave
    /// <c>call.opts</c> EMPTY with an honest note, because the option axes that produced each baked
    /// cell were never recorded — and the rigs resolve an unknown key as a silent fallback, so
    /// guessing is strictly worse than defaulting. The owner's editor therefore draws every
    /// structure in its default variant.</para>
    ///
    /// <para><b>⚠️ A recipe is a NEW SIBLING FILE, <c>&lt;stem&gt;.recipe.json</c>, and never an
    /// addition to an existing sidecar.</b> Several shipped sidecars are byte-pinned by tests (the
    /// sidecar-hash law), and adding a key to one is how a pin breaks three suites away. The
    /// separation is not tidiness — it is the reason this can land at all.</para>
    ///
    /// <para><b>The shape is defined once</b>, here and in
    /// <c>docs/art/rig-recipe-ledger.md</c>, and every baker writes it through
    /// <see cref="Write"/>. The ledger's Node half (<c>tools/rig-recipes/</c>) writes the SAME
    /// bytes — <c>RigRecipeTests</c> pins that by re-serialising every committed recipe through
    /// this writer and comparing byte for byte, so the two halves cannot drift.</para>
    ///
    /// <para><b>What makes a recipe true rather than plausible</b> is that running it back through
    /// the rigs reproduces the committed sheet, pixel for pixel:
    /// <c>node tools/rig-recipes/verify-ledger.mjs</c>, no Unity required.</para>
    /// </summary>
    public sealed class RigRecipe
    {
        public const string Schema = "hiddenharbours.rig-recipe/1";

        /// <summary>The sheet this recipe reproduces, as a bare file name — it is always the
        /// recipe's own sibling, so a path here would be a second way to say the same thing.</summary>
        public string Sheet;

        /// <summary>The kit id, e.g. <c>fuel-storage</c>. Groups a ledger for reporting.</summary>
        public string Kit;

        /// <summary>The baker type that wrote it, e.g. <c>FuelSheetBaker</c>.</summary>
        public string Baker;

        public RigBlock Rig;
        public CallBlock Call;
        public GridBlock Grid;
        public PackBlock Pack;

        /// <summary>sha256 of the sheet's own bytes — what this recipe CLAIMS to produce. For an
        /// LFS-tracked PNG this is also its pointer's <c>oid</c>, so a checkout without the objects
        /// can still tell whether a sheet has moved under a recipe.</summary>
        public string SheetSha256;

        // ---- blocks -----------------------------------------------------------------------------

        /// <summary>
        /// WHICH BYTES drew this sheet.
        ///
        /// <para>The scene-editor review's headline finding was that nothing in an export recorded
        /// that — 15 of the 51 rigs inlined in the owner's editor matched no commit in this repo's
        /// history, in both directions, and there was no way to tell from the export. A recipe that
        /// cannot be traced to a rig source is a guess, so the source is named AND hashed.</para>
        /// </summary>
        public sealed class RigBlock
        {
            /// <summary><see cref="RigCatalog"/> key.</summary>
            public string Key;

            /// <summary>The global the rig installs, e.g. <c>FuelIso</c>.</summary>
            public string Global;

            /// <summary>Repo-relative path to the art director's file.</summary>
            public string Source;

            /// <summary>sha256 of <see cref="Source"/> with CRLF folded to LF — the repo's own
            /// convention (<c>deckIsoSolid</c> is f7fc9db5…). ⚠️ <c>core.autocrlf</c> is on in some
            /// checkouts, so a raw hash of the working tree reports a drift that does not exist.</summary>
            public string Sha256;

            /// <summary>The catalog's declared azimuth convention, which is what
            /// <see cref="RigBaker.DirForCell"/> was handed.</summary>
            public string Convention;

            /// <summary>The catalog's TRANSITIVE prerequisite closure, in load order.
            ///
            /// <para>⚠️ Recorded rather than left to the render to expose, because a missing
            /// prerequisite does not throw once something else in the same process has installed
            /// that global — the baker's own install is idempotent for the same reason. A recipe
            /// that under-declares its dependencies renders correctly in a warm host and WRONGLY in
            /// a fresh one.</para></summary>
            public List<PrereqBlock> Prerequisites = new List<PrereqBlock>();
        }

        /// <summary>
        /// One rig that has to be loaded first. Deliberately a SEPARATE type from
        /// <see cref="RigBlock"/> rather than the same one with two fields left null: the closure is
        /// already flattened into the parent's list in load order, so a prerequisite carries neither
        /// a convention (it is not the rig whose facings were corrected) nor prerequisites of its own
        /// (they are already above it in the same list).
        /// </summary>
        public sealed class PrereqBlock
        {
            public string Key, Global, Source, Sha256;
        }

        /// <summary>
        /// The call, as the baker spelled it.
        ///
        /// <para><see cref="Args"/> is a TEMPLATE: the literal <c>"$dir"</c> stands where the facing
        /// argument goes and <c>"$opts"</c> where the options object goes. That is what carries the
        /// three call shapes this repo's rigs actually have — <c>render(key, dir, opts)</c>,
        /// <c>render(dir, opts)</c> for a family that draws exactly one thing, and
        /// <c>render(key, opts)</c> for one with no facing axis at all — without a second table
        /// mapping family names to argument orders.</para>
        /// </summary>
        public sealed class CallBlock
        {
            public string Fn = "render";
            public List<object> Args = new List<object>();

            /// <summary>The base options, in the baker's own key order. Axes bound to
            /// <c>opt:&lt;key&gt;</c> override an entry here per cell.</summary>
            public OptionDict Opts = new OptionDict();
        }

        /// <summary>
        /// How the cells lay out.
        ///
        /// <para><b>The axes are an ODOMETER and the FIRST axis is the fastest.</b> Cell index
        /// <c>i = Σ (index_a × stride_a)</c>, and cell <c>i</c> lands at <c>col = i % columns</c>,
        /// <c>row = ⌊i / columns⌋</c>. Every baker in the repo packs that way: the fuel kit's
        /// facings-then-fills falls out of it, the camper's swing-then-facings does, and so does the
        /// gas-station kit's single facing axis wrapping into rows.</para>
        /// </summary>
        public sealed class GridBlock
        {
            public int Columns, Rows;

            /// <summary>The only placement rule this schema defines. Named rather than implied so a
            /// second one has somewhere to go.</summary>
            public string Order = "rowMajor";

            public List<Axis> Axes = new List<Axis>();
        }

        /// <summary>One variant axis: what it is called, where its value goes, and every value.</summary>
        public sealed class Axis
        {
            /// <summary>Human name — <c>facing</c>, <c>fill</c>, <c>swing</c>, <c>wear</c>.</summary>
            public string Name;

            /// <summary><c>dir</c> (substitutes <c>$dir</c>), <c>opt:&lt;key&gt;</c> (sets one key of
            /// <c>opts</c>) or <c>arg:&lt;n&gt;</c> (replaces one positional argument).</summary>
            public string Bind;

            public List<object> Values = new List<object>();

            public static Axis Facings(int facings, AzimuthConvention convention, string name = "facing")
            {
                var a = new Axis { Name = name, Bind = "dir" };
                for (int k = 0; k < facings; k++)
                    a.Values.Add(RigBaker.DirForCell(k, facings, convention));
                return a;
            }

            public static Axis Option(string name, string key, IEnumerable<object> values)
            {
                var a = new Axis { Name = name, Bind = "opt:" + key };
                a.Values.AddRange(values);
                return a;
            }
        }

        /// <summary>
        /// The MEASUREMENTS the bake made turning cells into a sheet.
        ///
        /// <para>⚠️ The crop is RECORDED rather than left to be re-derived, and that is deliberate:
        /// it makes a verifier a reassembler instead of a second implementation of the crop rule, so
        /// a recipe whose crop is wrong FAILS the pixel compare rather than quietly agreeing with
        /// itself.</para>
        ///
        /// <para><see cref="Rule"/> names how the crop was arrived at. It is documentation, not
        /// behaviour — <c>pivotUnionCrop</c> is <see cref="IsoPropSheetBaker.MeasureCell"/>'s
        /// pivot-inclusive ink union; <c>pivotUnionCropOverWear</c> is the nav-buoy kit's, which
        /// unions over an axis its sheets do not carry.</para>
        /// </summary>
        public sealed class PackBlock
        {
            public string Rule = "pivotUnionCrop";

            /// <summary>The rig's own uncropped buffer and pivot, top-left origin.</summary>
            public int NativeW, NativeH, NativePivotX, NativePivotY;

            /// <summary>Top-left of the crop rect inside that buffer.</summary>
            public int CropX, CropY;

            /// <summary>The cropped cell, and the pivot inside it.</summary>
            public int CellW, CellH, PivotX, PivotY;

            public int SheetW, SheetH;
        }

        /// <summary>An insertion-ordered options dict. Key order is part of the file's identity, so
        /// a <c>Dictionary</c> (whose order is an implementation detail) will not do.</summary>
        public sealed class OptionDict : List<KeyValuePair<string, object>>
        {
            public OptionDict Set(string key, object value)
            {
                Add(new KeyValuePair<string, object>(key, value));
                return this;
            }

            public OptionDict Clone()
            {
                var copy = new OptionDict();
                copy.AddRange(this);
                return copy;
            }
        }

        // ---- the options, spelled ONCE ------------------------------------------------------------

        /// <summary>
        /// The options as a JavaScript object literal, in the rigs' own house style — single-quoted
        /// strings, no spaces — i.e. exactly what every baker used to build by hand.
        ///
        /// <para><b>⭐ THIS IS WHY THE DICT IS THE SOURCE AND THE STRING IS DERIVED.</b> A baker that
        /// built its call one way and recorded its recipe another would have two definitions of the
        /// same options, and the recipe would be right until the day someone edited one of them. The
        /// rigs resolve an unknown or missing key as a SILENT fallback, so that divergence would not
        /// throw — it would ship a sheet whose recipe draws something else.</para>
        /// </summary>
        public static string Js(OptionDict opts)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < opts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(opts[i].Key).Append(':');
                Js(sb, opts[i].Value);
            }
            return sb.Append('}').ToString();
        }

        static void Js(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s:
                    sb.Append('\'').Append(s.Replace("\\", "\\\\").Replace("'", "\\'")).Append('\'');
                    return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case float f: sb.Append(Number(f)); return;
                case double d: sb.Append(Number(d)); return;
                case OptionDict nested: sb.Append(Js(nested)); return;
                case System.Collections.IEnumerable list:
                    sb.Append('[');
                    bool first = true;
                    foreach (object item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Js(sb, item);
                    }
                    sb.Append(']');
                    return;
                default:
                    throw new InvalidOperationException(
                        $"{v.GetType().Name} is not a rig option value. Keep options to the JSON " +
                        "primitives the recipe can also carry — anything else is a value the ledger " +
                        "could not record even though the rig was handed it.");
            }
        }

        // ---- writing ----------------------------------------------------------------------------

        /// <summary>
        /// Writes <c>&lt;stem&gt;.recipe.json</c> beside <paramref name="sheetAssetPath"/> and
        /// returns the recipe's own project-relative path.
        ///
        /// <para>Fills <see cref="RigRecipe.Sheet"/> and <see cref="SheetSha256"/> from the sheet on
        /// disk, so a baker cannot record a hash of something it did not just write.</para>
        /// </summary>
        public static string Write(string sheetAssetPath, RigRecipe recipe)
        {
            if (string.IsNullOrEmpty(sheetAssetPath))
                throw new ArgumentException("a recipe needs the sheet it belongs to",
                                            nameof(sheetAssetPath));

            string abs = Path.Combine(RigCatalog.RepoRoot, sheetAssetPath);
            if (!File.Exists(abs))
                throw new FileNotFoundException(
                    $"No sheet at {sheetAssetPath}. A recipe is written AFTER its sheet, and its " +
                    "sheetSha256 is that file's own bytes — writing one for a sheet that does not " +
                    "exist would pin nothing.", abs);

            recipe.Sheet = Path.GetFileName(sheetAssetPath);
            recipe.SheetSha256 = Sha256OfFile(abs);

            string recipePath = Path.ChangeExtension(sheetAssetPath, null) + ".recipe.json";
            File.WriteAllText(Path.Combine(RigCatalog.RepoRoot, recipePath), recipe.ToJson(),
                              new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return recipePath;
        }

        /// <summary>The rig block for a catalog key, prerequisites resolved transitively in load
        /// order and every source hashed. Never hand-built by a baker.</summary>
        public static RigBlock RigBlockFor(string rigKey)
        {
            var entry = RigCatalog.Get(rigKey);
            var block = new RigBlock
            {
                Key = rigKey,
                Global = entry.GlobalName,
                Source = entry.ScriptPath,
                Sha256 = Sha256OfSource(entry.ScriptPath),
                Convention = entry.DeclaredConvention.ToString(),
            };

            var seen = new HashSet<string> { rigKey };
            void Walk(string key)
            {
                foreach (string dep in RigCatalog.Get(key).Prerequisites)
                {
                    if (!seen.Add(dep)) continue;
                    Walk(dep);
                    var d = RigCatalog.Get(dep);
                    block.Prerequisites.Add(new PrereqBlock
                    {
                        Key = dep, Global = d.GlobalName, Source = d.ScriptPath,
                        Sha256 = Sha256OfSource(d.ScriptPath),
                    });
                }
            }
            Walk(rigKey);
            return block;
        }

        /// <summary>sha256 of a rig source with CRLF folded to LF — see
        /// <see cref="RigBlock.Sha256"/>.</summary>
        public static string Sha256OfSource(string repoRelativePath)
        {
            byte[] raw = File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, repoRelativePath));
            var lf = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n') continue;
                lf.Add(raw[i]);
            }
            return Hex(lf.ToArray());
        }

        static string Sha256OfFile(string absolutePath) => Hex(File.ReadAllBytes(absolutePath));

        static string Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        // ---- reading ------------------------------------------------------------------------------

        /// <summary>
        /// Reads the recipe beside a sheet, or <c>null</c> when the sheet has none.
        ///
        /// <para>Not every sheet in the repo has one — a kit joins the ledger when its baker writes
        /// recipes AND its shipped sheets have been proved to reproduce from them
        /// (<c>docs/art/rig-recipe-ledger.md</c>). A caller that treats "no recipe" as an error would
        /// be asserting a completeness this lane does not yet claim.</para>
        /// </summary>
        public static RigRecipe Read(string sheetAssetPath)
        {
            string rel = Path.ChangeExtension(sheetAssetPath, null) + ".recipe.json";
            string abs = Path.Combine(RigCatalog.RepoRoot, rel);
            return File.Exists(abs) ? Parse(File.ReadAllText(abs), rel) : null;
        }

        /// <summary>
        /// Parses a recipe. STRICT on purpose: an unknown key throws rather than being ignored,
        /// because a recipe carrying a key this build does not understand is a schema this build
        /// cannot honour — and silently dropping it is how a consumer draws the wrong variant while
        /// believing it read the file.
        /// </summary>
        public static RigRecipe Parse(string json, string label = "(recipe)")
        {
            var p = new Reader(json, label);
            var root = p.ReadObject();
            p.SkipWhitespaceToEnd();

            var recipe = new RigRecipe();
            foreach (var kv in root)
            {
                switch (kv.Key)
                {
                    case "schema":
                        if ((string)kv.Value != Schema)
                            throw new InvalidOperationException(
                                $"{label} declares schema '{kv.Value}'; this build writes '{Schema}'.");
                        break;
                    case "sheet": recipe.Sheet = (string)kv.Value; break;
                    case "kit": recipe.Kit = (string)kv.Value; break;
                    case "baker": recipe.Baker = (string)kv.Value; break;
                    case "sheetSha256": recipe.SheetSha256 = (string)kv.Value; break;
                    case "rig": recipe.Rig = ReadRig(kv.Value, label); break;
                    case "call": recipe.Call = ReadCall(kv.Value, label); break;
                    case "grid": recipe.Grid = ReadGrid(kv.Value, label); break;
                    case "pack": recipe.Pack = ReadPack(kv.Value, label); break;
                    default: throw Unknown(label, kv.Key, "the recipe");
                }
            }
            return recipe;
        }

        static InvalidOperationException Unknown(string label, string key, string where) =>
            new InvalidOperationException(
                $"{label}: '{key}' is not a key of {where}. The schema is closed — a recipe this " +
                "build cannot fully read is one it must not act on.");

        static OptionDict AsObject(object v, string label, string where) =>
            v as OptionDict ?? throw new InvalidOperationException($"{label}: {where} is not an object.");

        static List<object> AsArray(object v, string label, string where) =>
            v as List<object> ?? throw new InvalidOperationException($"{label}: {where} is not an array.");

        static int AsInt(object v, string label, string where) =>
            v is double d && d == Math.Floor(d)
                ? (int)d
                : throw new InvalidOperationException($"{label}: {where} is not a whole number.");

        static RigBlock ReadRig(object v, string label)
        {
            var block = new RigBlock();
            foreach (var kv in AsObject(v, label, "rig"))
                switch (kv.Key)
                {
                    case "key": block.Key = (string)kv.Value; break;
                    case "global": block.Global = (string)kv.Value; break;
                    case "source": block.Source = (string)kv.Value; break;
                    case "sha256": block.Sha256 = (string)kv.Value; break;
                    case "convention": block.Convention = (string)kv.Value; break;
                    case "prerequisites":
                        foreach (object dep in AsArray(kv.Value, label, "rig.prerequisites"))
                        {
                            var d = new PrereqBlock();
                            foreach (var dkv in AsObject(dep, label, "a prerequisite"))
                                switch (dkv.Key)
                                {
                                    case "key": d.Key = (string)dkv.Value; break;
                                    case "global": d.Global = (string)dkv.Value; break;
                                    case "source": d.Source = (string)dkv.Value; break;
                                    case "sha256": d.Sha256 = (string)dkv.Value; break;
                                    default: throw Unknown(label, dkv.Key, "a prerequisite");
                                }
                            block.Prerequisites.Add(d);
                        }
                        break;
                    default: throw Unknown(label, kv.Key, "the rig block");
                }
            return block;
        }

        static CallBlock ReadCall(object v, string label)
        {
            var call = new CallBlock();
            foreach (var kv in AsObject(v, label, "call"))
                switch (kv.Key)
                {
                    case "fn": call.Fn = (string)kv.Value; break;
                    case "args": call.Args = AsArray(kv.Value, label, "call.args"); break;
                    case "opts": call.Opts = AsObject(kv.Value, label, "call.opts"); break;
                    default: throw Unknown(label, kv.Key, "the call block");
                }
            return call;
        }

        static GridBlock ReadGrid(object v, string label)
        {
            var grid = new GridBlock();
            foreach (var kv in AsObject(v, label, "grid"))
                switch (kv.Key)
                {
                    case "columns": grid.Columns = AsInt(kv.Value, label, "grid.columns"); break;
                    case "rows": grid.Rows = AsInt(kv.Value, label, "grid.rows"); break;
                    case "order": grid.Order = (string)kv.Value; break;
                    case "axes":
                        foreach (object a in AsArray(kv.Value, label, "grid.axes"))
                        {
                            var axis = new Axis();
                            foreach (var akv in AsObject(a, label, "an axis"))
                                switch (akv.Key)
                                {
                                    case "name": axis.Name = (string)akv.Value; break;
                                    case "bind": axis.Bind = (string)akv.Value; break;
                                    case "values":
                                        axis.Values = AsArray(akv.Value, label, "an axis's values");
                                        break;
                                    default: throw Unknown(label, akv.Key, "an axis");
                                }
                            grid.Axes.Add(axis);
                        }
                        break;
                    default: throw Unknown(label, kv.Key, "the grid block");
                }
            return grid;
        }

        static PackBlock ReadPack(object v, string label)
        {
            var pack = new PackBlock();
            foreach (var kv in AsObject(v, label, "pack"))
                switch (kv.Key)
                {
                    case "rule": pack.Rule = (string)kv.Value; break;
                    case "nativeW": pack.NativeW = AsInt(kv.Value, label, "pack.nativeW"); break;
                    case "nativeH": pack.NativeH = AsInt(kv.Value, label, "pack.nativeH"); break;
                    case "nativePivotX":
                        pack.NativePivotX = AsInt(kv.Value, label, "pack.nativePivotX"); break;
                    case "nativePivotY":
                        pack.NativePivotY = AsInt(kv.Value, label, "pack.nativePivotY"); break;
                    case "cropX": pack.CropX = AsInt(kv.Value, label, "pack.cropX"); break;
                    case "cropY": pack.CropY = AsInt(kv.Value, label, "pack.cropY"); break;
                    case "cellW": pack.CellW = AsInt(kv.Value, label, "pack.cellW"); break;
                    case "cellH": pack.CellH = AsInt(kv.Value, label, "pack.cellH"); break;
                    case "pivotX": pack.PivotX = AsInt(kv.Value, label, "pack.pivotX"); break;
                    case "pivotY": pack.PivotY = AsInt(kv.Value, label, "pack.pivotY"); break;
                    case "sheetW": pack.SheetW = AsInt(kv.Value, label, "pack.sheetW"); break;
                    case "sheetH": pack.SheetH = AsInt(kv.Value, label, "pack.sheetH"); break;
                    default: throw Unknown(label, kv.Key, "the pack block");
                }
            return pack;
        }

        /// <summary>
        /// A small strict JSON reader. Objects come back as <see cref="OptionDict"/> so KEY ORDER
        /// survives the round trip — which is the whole point here, since the file's identity
        /// includes its ordering. Numbers come back as <c>double</c>, strings as <c>string</c>,
        /// booleans as <c>bool</c>, null as <c>null</c>.
        /// </summary>
        sealed class Reader
        {
            readonly string _s, _label;
            int _i;

            public Reader(string s, string label) { _s = s; _label = label; }

            InvalidOperationException Bad(string what) =>
                new InvalidOperationException($"{_label}: {what} at offset {_i}.");

            void Skip()
            {
                while (_i < _s.Length &&
                       (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r')) _i++;
            }

            public void SkipWhitespaceToEnd()
            {
                Skip();
                if (_i != _s.Length) throw Bad("trailing content after the recipe");
            }

            public OptionDict ReadObject()
            {
                Skip();
                if (_i >= _s.Length || _s[_i] != '{') throw Bad("expected an object");
                _i++;
                var o = new OptionDict();
                Skip();
                if (_i < _s.Length && _s[_i] == '}') { _i++; return o; }
                while (true)
                {
                    Skip();
                    string key = ReadString();
                    Skip();
                    if (_i >= _s.Length || _s[_i] != ':') throw Bad("expected ':'");
                    _i++;
                    o.Set(key, ReadValue());
                    Skip();
                    if (_i >= _s.Length) throw Bad("unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return o; }
                    throw Bad("expected ',' or '}'");
                }
            }

            object ReadValue()
            {
                Skip();
                if (_i >= _s.Length) throw Bad("expected a value");
                char c = _s[_i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (Match("true")) return true;
                if (Match("false")) return false;
                if (Match("null")) return null;
                return ReadNumber();
            }

            bool Match(string word)
            {
                if (_i + word.Length > _s.Length ||
                    string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                    return false;
                _i += word.Length;
                return true;
            }

            List<object> ReadArray()
            {
                _i++;                                  // '['
                var list = new List<object>();
                Skip();
                if (_i < _s.Length && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    list.Add(ReadValue());
                    Skip();
                    if (_i >= _s.Length) throw Bad("unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Bad("expected ',' or ']'");
                }
            }

            string ReadString()
            {
                if (_i >= _s.Length || _s[_i] != '"') throw Bad("expected a string");
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw Bad("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (_i >= _s.Length) throw Bad("unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Bad("truncated \\u escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber,
                                                      CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Bad($"unknown escape '\\{e}'");
                    }
                }
            }

            double ReadNumber()
            {
                int start = _i;
                if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.' ||
                                          _s[_i] == 'e' || _s[_i] == 'E' ||
                                          _s[_i] == '-' || _s[_i] == '+')) _i++;
                if (_i == start) throw Bad("expected a number");
                return double.Parse(_s.Substring(start, _i - start), NumberStyles.Float,
                                    CultureInfo.InvariantCulture);
            }
        }

        // ---- serialisation ------------------------------------------------------------------------

        /// <summary>
        /// The recipe as it goes to disk: <c>JSON.stringify(recipe, null, 2)</c> plus a trailing
        /// newline, with a FIXED key order.
        ///
        /// <para>⚠️ <b>The layout is JavaScript's on purpose.</b> The ledger's Node half writes the
        /// same files, and two writers that agree on content but differ on whitespace would churn
        /// every recipe on alternate runs. <c>RigRecipeTests</c> re-serialises every committed
        /// recipe through this method and compares bytes, which is what holds the two together.</para>
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            WriteObject(sb, 0, o =>
            {
                o("schema", Schema);
                o("sheet", Sheet);
                o("kit", Kit);
                o("baker", Baker);
                o("rig", Rig);
                o("call", Call);
                o("grid", Grid);
                o("pack", Pack);
                o("sheetSha256", SheetSha256);
            });
            sb.Append('\n');
            return sb.ToString();
        }

        delegate void Field(string key, object value);

        static void WriteObject(StringBuilder sb, int depth, Action<Field> fields)
        {
            var pairs = new List<KeyValuePair<string, object>>();
            fields((k, v) => pairs.Add(new KeyValuePair<string, object>(k, v)));
            WritePairs(sb, depth, pairs);
        }

        static void WritePairs(StringBuilder sb, int depth, IList<KeyValuePair<string, object>> pairs)
        {
            if (pairs.Count == 0) { sb.Append("{}"); return; }
            sb.Append("{\n");
            for (int i = 0; i < pairs.Count; i++)
            {
                Indent(sb, depth + 1);
                WriteString(sb, pairs[i].Key);
                sb.Append(": ");
                WriteValue(sb, depth + 1, pairs[i].Value);
                if (i < pairs.Count - 1) sb.Append(',');
                sb.Append('\n');
            }
            Indent(sb, depth);
            sb.Append('}');
        }

        static void WriteValue(StringBuilder sb, int depth, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case float f: sb.Append(Number(f)); return;
                case double d: sb.Append(Number(d)); return;

                case RigBlock rig:
                    WriteObject(sb, depth, o =>
                    {
                        o("key", rig.Key);
                        o("global", rig.Global);
                        o("source", rig.Source);
                        o("sha256", rig.Sha256);
                        o("convention", rig.Convention);
                        o("prerequisites", rig.Prerequisites.Cast<object>().ToList());
                    });
                    return;

                case PrereqBlock dep:
                    WriteObject(sb, depth, o =>
                    {
                        o("key", dep.Key);
                        o("global", dep.Global);
                        o("source", dep.Source);
                        o("sha256", dep.Sha256);
                    });
                    return;

                case CallBlock call:
                    WriteObject(sb, depth, o =>
                    {
                        o("fn", call.Fn);
                        o("args", call.Args);
                        o("opts", call.Opts);
                    });
                    return;

                case GridBlock grid:
                    WriteObject(sb, depth, o =>
                    {
                        o("columns", grid.Columns);
                        o("rows", grid.Rows);
                        o("order", grid.Order);
                        o("axes", grid.Axes.Cast<object>().ToList());
                    });
                    return;

                case Axis axis:
                    WriteObject(sb, depth, o =>
                    {
                        o("name", axis.Name);
                        o("bind", axis.Bind);
                        o("values", axis.Values);
                    });
                    return;

                case PackBlock pack:
                    WriteObject(sb, depth, o =>
                    {
                        o("rule", pack.Rule);
                        o("nativeW", pack.NativeW);
                        o("nativeH", pack.NativeH);
                        o("nativePivotX", pack.NativePivotX);
                        o("nativePivotY", pack.NativePivotY);
                        o("cropX", pack.CropX);
                        o("cropY", pack.CropY);
                        o("cellW", pack.CellW);
                        o("cellH", pack.CellH);
                        o("pivotX", pack.PivotX);
                        o("pivotY", pack.PivotY);
                        o("sheetW", pack.SheetW);
                        o("sheetH", pack.SheetH);
                    });
                    return;

                case OptionDict opts:
                    WritePairs(sb, depth, opts);
                    return;

                case System.Collections.IEnumerable list:
                    var items = list.Cast<object>().ToList();
                    if (items.Count == 0) { sb.Append("[]"); return; }
                    sb.Append("[\n");
                    for (int i = 0; i < items.Count; i++)
                    {
                        Indent(sb, depth + 1);
                        WriteValue(sb, depth + 1, items[i]);
                        if (i < items.Count - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                    Indent(sb, depth);
                    sb.Append(']');
                    return;

                default:
                    throw new InvalidOperationException(
                        $"{v.GetType().Name} has no place in a recipe. The schema is closed on " +
                        "purpose — a value the Node half cannot write is a file the two halves " +
                        "would disagree about.");
            }
        }

        static void Indent(StringBuilder sb, int depth) => sb.Append(' ', depth * 2);

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u")
                              .Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);   // JSON.stringify emits everything else raw, and so do we
                        break;
                }
            }
            sb.Append('"');
        }

        /// <summary>
        /// A number, formatted the way JavaScript's <c>Number.prototype.toString</c> would.
        ///
        /// <para>The SHORTEST decimal that round-trips, found by widening precision — not
        /// <c>"R"</c>, whose exact behaviour has moved between .NET runtimes, and not <c>"G17"</c>,
        /// which writes 0.25 as 0.25 but 0.1 as 0.10000000000000001. Exponent notation is REFUSED
        /// rather than emitted: no rig option in this repo needs it, and the two writers' exponent
        /// spellings differ (<c>1E+21</c> against <c>1e+21</c>), so a value that needed one would be
        /// a silent divergence rather than a loud one.</para>
        /// </summary>
        public static string Number(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                throw new InvalidOperationException(
                    $"{v} is not a JSON number. A rig option that came out NaN is a bug in the " +
                    "call, not a value to record.");
            if (v == 0) return "0";                       // and never "-0"

            string s = null;
            for (int p = 1; p <= 17; p++)
            {
                s = v.ToString("G" + p.ToString(CultureInfo.InvariantCulture),
                               CultureInfo.InvariantCulture);
                if (double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture) == v) break;
            }

            if (s!.IndexOf('E') >= 0 || s.IndexOf('e') >= 0)
                throw new InvalidOperationException(
                    $"{s} needs exponent notation, which this schema does not carry — the C# and " +
                    "JavaScript writers spell it differently and the files would silently diverge.");
            return s;
        }

        /// <summary>The float overload, so a <c>const float</c> tunable records the number the baker
        /// spliced into the call rather than its widened double (0.32f is "0.32", not
        /// "0.31999999284744263").</summary>
        public static string Number(float v) =>
            Number(double.Parse(v.ToString("R", CultureInfo.InvariantCulture),
                                NumberStyles.Float, CultureInfo.InvariantCulture));
    }
}
