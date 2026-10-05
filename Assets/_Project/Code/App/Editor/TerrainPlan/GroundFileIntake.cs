using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>A GROUND FILE INTO DATA, ONE PRESS</b> (terrain PR 5 B; amendment 1 §4.1 item 7). A package's ground file
    /// (<c>hidden-harbours/island-ground@2</c>) becomes:
    /// <list type="bullet">
    /// <item>its base, the PNG's bytes as they came (the import checks its pixels, not its file);</item>
    /// <item>one <see cref="GroundAskDef"/> per ask, by id, with its patch beside it as a 16-bit PNG (row 0 at the box's
    /// south edge is the image's bottom row), read back and compared code for code before the ask points at it;</item>
    /// <item>the <see cref="GroundFileDef"/>, listing the package's asks in the file's order, then any the package
    /// dropped (retired, their ids kept), then the game's own asks: those it listed already, in their order, and then
    /// any game ask in the folder it did not list yet, in the order of its asset's name.</item>
    /// </list>
    /// A revised file is a re-run: the package's asks are rewritten by id, and the game's asks are never touched. The
    /// file's JSON stays out of the repo (it is mostly base64). Every check STOPs the intake before it writes anything.
    /// </summary>
    public static class GroundFileIntake
    {
        public const string Schema = "hidden-harbours/island-ground@2";
        public const string AskPrefix = "GroundAsk_";
        public const string PatchPrefix = "GroundPatch_";
        public const string BasePrefix = "GroundBase_";
        const int CodeCount = 65535;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>One ask as the file holds it.</summary>
        public sealed class FileAsk
        {
            public string Id = "", Scene = "", FileKind = "", Ruled = "", Rule = "";
            public bool HasPatch;
            public double X0, Y0, X1, Y1, Step, Lo, Hi;
            public int Width, Height;
            /// <summary>The samples, row 0 at the box's south edge, and the sha256 of their bytes as the file holds them.</summary>
            public ushort[] Codes;
            public string PatchSha256 = "";
            public Vector2[] Shore = new Vector2[0];
            public bool HasCircle;
            public Vector2 Centre;
            public float Reach;
        }

        /// <summary>The file as read: its identity, its base's frame and hashes, and its asks in order.</summary>
        public sealed class FileRead
        {
            public string Id = "", Sha256 = "";
            public string BaseTexture = "", BaseFileSha256 = "", BasePixelsSha256 = "";
            public Vector2 BaseRange, RectMin, RectMax;
            public int TexelsPerUnit, BaseWidth, BaseHeight;
            public readonly List<FileAsk> Asks = new List<FileAsk>();
        }

        // ---- the file, read (pure) ---------------------------------------------------------------------------------------

        static readonly Regex RectWords = new Regex(
            @"^centre \((-?\d+(?:\.\d+)?), (-?\d+(?:\.\d+)?)\), (\d+(?:\.\d+)?) x (\d+(?:\.\d+)?) units, (\d+) texels a unit;", RegexOptions.CultureInvariant);
        static readonly Regex EncWords = new Regex(
            @"^u16 big-endian, base64; height = (-?\d+(?:\.\d+)?) \+ (\d+(?:\.\d+)?) x code / 65535; row 0 at the box.s south edge, column 0 at its west edge$", RegexOptions.CultureInvariant);

        /// <summary>
        /// PURE: the file's text read and checked, its asks' samples decoded. Throws naming the first thing that does not
        /// read: the schema, the base's frame (its words), an encoding other than the one the import reads, a patch whose
        /// box, step and size disagree, or whose samples do not fill it.
        /// </summary>
        public static FileRead Read(string json, string sha256)
        {
            object doc;
            try { doc = TerrainPlanSourcesJson.ParseDocument(json); }
            catch (FormatException e) { throw new InvalidDataException("[GroundFileIntake] the ground file does not read: " + e.Message); }
            var root = doc as Dictionary<string, object> ?? throw Bad("the file is not a JSON object");
            if (Str(root, "schema") != Schema) throw Bad("the schema is " + Str(root, "schema") + ", not " + Schema);
            var f = new FileRead { Id = Str(root, "id"), Sha256 = sha256 ?? "" };
            if (!TerrainPlanValidation.IsId(f.Id) || !f.Id.StartsWith("ground.", StringComparison.Ordinal)) throw Bad("the file's id '" + f.Id + "' is not ground.snake_case");

            var b = Obj(root, "base");
            f.BaseTexture = Str(b, "texture");
            if (f.BaseTexture.IndexOfAny(new[] { '/', '\\' }) >= 0 || !f.BaseTexture.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw Bad("the base's texture '" + f.BaseTexture + "' is not a PNG beside the file");
            f.BaseFileSha256 = Str(b, "fileSha256");
            f.BasePixelsSha256 = Str(b, "pixelsSha256");
            var range = Nums(b, "range", 2);
            f.BaseRange = new Vector2((float)range[0], (float)range[1]);
            var m = RectWords.Match(Str(b, "rect"));
            if (!m.Success) throw Bad("the base's rect does not read as \"centre (x, y), w x h units, n texels a unit; …\"");
            double cx = D(m.Groups[1].Value), cy = D(m.Groups[2].Value), w = D(m.Groups[3].Value), h = D(m.Groups[4].Value);
            f.TexelsPerUnit = int.Parse(m.Groups[5].Value, Inv);
            f.RectMin = new Vector2((float)(cx - w / 2), (float)(cy - h / 2));
            f.RectMax = new Vector2((float)(cx + w / 2), (float)(cy + h / 2));
            f.BaseWidth = (int)Math.Round(w * f.TexelsPerUnit);
            f.BaseHeight = (int)Math.Round(h * f.TexelsPerUnit);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in Arr(root, "asks"))
            {
                var a = o as Dictionary<string, object> ?? throw Bad("an ask is not an object");
                var k = new FileAsk
                {
                    Id = Str(a, "id"),
                    Scene = a.TryGetValue("scene", out var sc) && sc is string ss ? ss : "",
                    FileKind = a.TryGetValue("kind", out var kd) && kd is string ks ? ks : "",
                    Ruled = a.TryGetValue("ruled", out var rl) && rl is string rs ? rs : "",
                };
                if (!TerrainPlanValidation.IsId(k.Id) || !k.Id.StartsWith("ground.", StringComparison.Ordinal)) throw Bad("the ask id '" + k.Id + "' is not ground.snake_case");
                if (!seen.Add(k.Id)) throw Bad(k.Id + " is listed twice");
                k.Rule = Get(a, "rule") is string rule ? rule : throw Bad(k.Id + "'s rule is not words");
                if (a.TryGetValue("shore", out var shore) && shore != null) k.Shore = Points(shore, k.Id + "'s shore");
                if (Get(a, "patch") is Dictionary<string, object> p)
                {
                    k.HasPatch = true;
                    var box = Nums(p, "box", 4);
                    k.X0 = box[0]; k.Y0 = box[1]; k.X1 = box[2]; k.Y1 = box[3];
                    k.Step = Num(p, "step");
                    k.Width = (int)Num(p, "w");
                    k.Height = (int)Num(p, "h");
                    var em = EncWords.Match(Str(p, "enc"));
                    if (!em.Success) throw Bad(k.Id + "'s samples are not \"u16 big-endian, base64; height = lo + span x code / 65535; row 0 at the box's south edge, column 0 at its west edge\"");
                    k.Lo = D(em.Groups[1].Value);
                    k.Hi = k.Lo + D(em.Groups[2].Value);
                    if (!(k.Step > 0) || k.Width < 2 || k.Height < 2 ||
                        Math.Abs(k.X0 + (k.Width - 1) * k.Step - k.X1) > 1e-6 || Math.Abs(k.Y0 + (k.Height - 1) * k.Step - k.Y1) > 1e-6)
                        throw Bad(k.Id + "'s box, step and size disagree (" + k.Width + " × " + k.Height + " samples of " + k.Step.ToString("R", Inv) + ")");
                    byte[] raw;
                    try { raw = Convert.FromBase64String(Str(p, "data")); }
                    catch (FormatException) { throw Bad(k.Id + "'s samples are not base64"); }
                    if (raw.Length != 2 * k.Width * k.Height) throw Bad(k.Id + "'s samples are " + raw.Length + " bytes, not " + 2 * k.Width * k.Height);
                    k.Codes = new ushort[k.Width * k.Height];
                    for (int i = 0; i < k.Codes.Length; i++) k.Codes[i] = (ushort)((raw[2 * i] << 8) | raw[2 * i + 1]);
                    using (var sha = SHA256.Create()) k.PatchSha256 = TerrainPlanResult.Hex(sha.ComputeHash(raw));
                }
                else
                {
                    if (a.TryGetValue("centre", out var c) && c != null)
                    {
                        var cc = Points(new List<object> { c }, k.Id + "'s centre")[0];
                        k.HasCircle = true;
                        k.Centre = cc;
                        k.Reach = (float)Num(a, "reachM");
                    }
                }
                f.Asks.Add(k);
            }
            if (f.Asks.Count == 0) throw Bad("the file has no asks");
            return f;
        }

        /// <summary>The asset name an id files under: <c>ground.stp_fen_pool_dry</c> → <c>StpFenPoolDry</c>.</summary>
        public static string NameOf(string id)
        {
            string tail = id.StartsWith("ground.", StringComparison.Ordinal) ? id.Substring(7) : id;
            return string.Concat(tail.Split('_').Where(s => s.Length > 0).Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
        }

        // ---- the intake (the editor's half) ------------------------------------------------------------------------------

        /// <summary>
        /// The file at <paramref name="jsonPath"/> (its base beside it) into <paramref name="folder"/>, as the class says.
        /// Returns the ground file's Def and adds a line per thing written. Throws, writing nothing, when a check fails.
        /// </summary>
        public static GroundFileDef Intake(string jsonPath, string folder, string package, List<string> log)
        {
            byte[] jsonBytes = File.ReadAllBytes(jsonPath);
            string jsonSha = Sha(jsonBytes);
            var f = Read(File.ReadAllText(jsonPath), jsonSha);

            // the base: its pixels, as the file states them; its size, as its rect's words say
            string basePath = Path.Combine(Path.GetDirectoryName(jsonPath) ?? "", f.BaseTexture);
            byte[] baseBytes = File.ReadAllBytes(basePath);
            var img = TerrainPlanPng.Read(baseBytes);
            if (img.BitDepth != 16 || img.Channels != 1) throw Bad("the base is " + img.BitDepth + "-bit with " + img.Channels + " channels, not 16-bit grey");
            if (img.Width != f.BaseWidth || img.Height != f.BaseHeight) throw Bad("the base is " + img.Width + " × " + img.Height + "; its rect's words make it " + f.BaseWidth + " × " + f.BaseHeight);
            string pix = GroundFileImport.Sha256BigEndian(img.Channel(0));
            if (!string.Equals(pix, f.BasePixelsSha256, StringComparison.OrdinalIgnoreCase)) throw Bad("STOP: the base's pixels hash " + pix + "; the file says " + f.BasePixelsSha256);
            log.Add("READ " + f.Id + " (" + Schema + "), sha256 " + jsonSha + ": base " + f.BaseTexture + " " + img.Width + " × " + img.Height + ", pixels " + pix + " (the file's); " +
                    f.Asks.Count + " asks, " + f.Asks.Count(a => a.HasPatch) + " with samples");

            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
            var file = AssetDatabase.LoadAssetAtPath<GroundFileDef>(FilePathOf(folder, f.Id));
            var before = file != null && file.Asks != null ? file.Asks.Where(a => a != null).ToList() : new List<GroundAskDef>();

            // the base, byte for byte
            string baseAsset = folder + "/" + BasePrefix + f.BaseTexture;
            File.WriteAllBytes(baseAsset, baseBytes);
            AssetDatabase.ImportAsset(baseAsset, ImportAssetOptions.ForceSynchronousImport);
            PaintedHeightPng.ConfigureImporter(baseAsset);
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseAsset);
            log.Add("  base " + baseAsset + " " + baseBytes.Length + " B, sha256 " + Sha(baseBytes));

            // the package's asks, by id
            var live = new List<GroundAskDef>();
            foreach (var k in f.Asks)
            {
                string name = NameOf(k.Id);
                string askPath = folder + "/" + AskPrefix + name + ".asset";
                var d = AssetDatabase.LoadAssetAtPath<GroundAskDef>(askPath);
                bool made = d == null;
                if (made)
                {
                    d = ScriptableObject.CreateInstance<GroundAskDef>();
                    AssetDatabase.CreateAsset(d, askPath);
                }
                else if (d.Source != GroundAskSource.Package || d.Id != k.Id) throw Bad(askPath + " holds " + d.Id + " (" + d.Source + "), not the package's " + k.Id);
                d.Id = k.Id;
                d.Source = GroundAskSource.Package;
                d.Retired = false;
                d.Rule = k.Rule;
                d.Package = package ?? "";
                d.GroundFileSha256 = jsonSha;
                d.Scene = k.Scene;
                d.FileKind = k.FileKind;
                d.Ruled = k.Ruled;
                d.Line = k.Shore;
                if (k.HasPatch)
                {
                    d.Kind = GroundAskKind.Patch;
                    d.BoxMin = new Vector2((float)k.X0, (float)k.Y0);
                    d.BoxMax = new Vector2((float)k.X1, (float)k.Y1);
                    d.Step = (float)k.Step;
                    d.Width = k.Width;
                    d.Height = k.Height;
                    d.PatchRange = new Vector2((float)k.Lo, (float)k.Hi);
                    d.PatchSha256 = k.PatchSha256;
                    string png = folder + "/" + PatchPrefix + name + ".png";
                    d.Patch = WritePatch(png, k);
                    log.Add("  " + (made ? "made " : "rewrote ") + askPath + ": " + k.Width + " × " + k.Height + " samples of " + k.Step.ToString("R", Inv) +
                            " over x " + k.X0.ToString("R", Inv) + "…" + k.X1.ToString("R", Inv) + ", y " + k.Y0.ToString("R", Inv) + "…" + k.Y1.ToString("R", Inv) +
                            "; " + png + " reads back code for code, " + new FileInfo(png).Length + " B");
                }
                else
                {
                    d.Kind = GroundAskKind.Rule;
                    d.Patch = null;
                    d.PatchSha256 = "";
                    d.Width = d.Height = 0;
                    if (k.HasCircle) { d.Centre = k.Centre; d.Reach = k.Reach; }
                    log.Add("  " + (made ? "made " : "rewrote ") + askPath + ": a rule" + (k.HasCircle ? ", its circle " + k.Reach.ToString("R", Inv) + " round (" +
                            k.Centre.x.ToString("R", Inv) + ", " + k.Centre.y.ToString("R", Inv) + ")" : ""));
                }
                EditorUtility.SetDirty(d);
                live.Add(d);
            }

            // the package's asks it no longer lists: retired, ids kept; the game's: as they were, then any new in the folder
            var liveIds = new HashSet<string>(live.Select(d => d.Id), StringComparer.Ordinal);
            var retired = before.Where(d => d.Source == GroundAskSource.Package && !liveIds.Contains(d.Id)).ToList();
            foreach (var d in retired)
            {
                if (!d.Retired) log.Add("  retired " + d.Id + ": the file no longer lists it");
                d.Retired = true;
                EditorUtility.SetDirty(d);
            }
            var game = before.Where(d => d.Source == GroundAskSource.Game).ToList();
            var gameIds = new HashSet<string>(game.Select(d => d.Id), StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GroundAskDef), new[] { folder }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g), StringComparer.Ordinal))
            {
                var d = AssetDatabase.LoadAssetAtPath<GroundAskDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (d == null || d.Source != GroundAskSource.Game || !gameIds.Add(d.Id)) continue;
                if (liveIds.Contains(d.Id)) throw Bad("the game's ask " + d.Id + " has a package ask's id");
                game.Add(d);
                log.Add("  listed the game's ask " + d.Id + " (" + d.Kind + ")" + (d.Retired ? ", retired" : ""));
            }

            bool newFile = file == null;
            if (newFile)
            {
                file = ScriptableObject.CreateInstance<GroundFileDef>();
                AssetDatabase.CreateAsset(file, FilePathOf(folder, f.Id));
            }
            else if (file.Id != f.Id) throw Bad(FilePathOf(folder, f.Id) + " holds " + file.Id + ", not " + f.Id);
            file.Id = f.Id;
            file.Schema = Schema;
            file.Package = package ?? "";
            file.FileSha256 = jsonSha;
            file.Base = baseTex;
            file.BaseTexture = f.BaseTexture;
            file.BasePixelsSha256 = f.BasePixelsSha256;
            file.BaseFileSha256 = f.BaseFileSha256;
            file.BaseRange = f.BaseRange;
            file.RectMin = f.RectMin;
            file.RectMax = f.RectMax;
            file.Asks = live.Concat(retired).Concat(game).ToArray();
            EditorUtility.SetDirty(file);
            AssetDatabase.SaveAssets();
            log.Add("  " + (newFile ? "made " : "rewrote ") + FilePathOf(folder, f.Id) + ": " + live.Count + " of the package's asks, " + retired.Count + " retired, " +
                    game.Count + " of the game's (" + string.Join(", ", game.Select(d => d.Id)) + ")");
            return file;
        }

        /// <summary>The ground file's Def's path in its folder: <c>GroundFile_StpIsland.asset</c> for <c>ground.stp_island</c>.</summary>
        public static string FilePathOf(string folder, string id) => folder + "/GroundFile_" + NameOf(id) + ".asset";

        /// <summary>The samples as a 16-bit grey PNG (row 0 at the box's south edge, the image's bottom row), imported as
        /// data, then read back from the file: every code must come back, or the intake stops.</summary>
        static Texture2D WritePatch(string png, FileAsk k)
        {
            var px = new Color[k.Codes.Length];
            for (int i = 0; i < px.Length; i++) { float v = k.Codes[i] / (float)CodeCount; px[i] = new Color(v, v, v, 1f); }
            var tex = PaintedHeightPng.WriteAndImport(png, px, k.Width, k.Height);
            var back = TerrainPlanPng.ReadFile(png);
            if (back.BitDepth != 16 || back.Channels != 1 || back.Width != k.Width || back.Height != k.Height) throw Bad(png + " did not write as 16-bit grey " + k.Width + " × " + k.Height);
            var got = TerrainPlanPng.FlipRows(back.Channel(0), k.Width, k.Height);
            for (int i = 0; i < got.Length; i++)
                if (got[i] != k.Codes[i]) throw Bad("STOP: " + png + " reads back " + got[i] + " at sample " + i + "; the file says " + k.Codes[i]);
            return tex;
        }

        /// <summary>A patch ask's samples as its PNG holds them (row 0 at the box's south edge): the import's read.</summary>
        public static ushort[] PatchOf(GroundAskDef d)
        {
            if (d == null || d.Patch == null) throw new InvalidDataException("[GroundFileIntake] " + (d == null ? "an empty ask" : d.Id) + " has no patch.");
            string png = AssetDatabase.GetAssetPath(d.Patch);
            var img = TerrainPlanPng.ReadFile(png);
            if (img.BitDepth != 16 || img.Channels != 1 || img.Width != d.Width || img.Height != d.Height)
                throw new InvalidDataException("[GroundFileIntake] " + png + " is not 16-bit grey " + d.Width + " × " + d.Height + ".");
            return TerrainPlanPng.FlipRows(img.Channel(0), img.Width, img.Height);
        }

        // ---- reading helpers ------------------------------------------------------------------------------------------------

        static InvalidDataException Bad(string what) => new InvalidDataException("[GroundFileIntake] " + what + ".");
        static object Get(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? v : throw Bad("no \"" + k + "\"");
        static Dictionary<string, object> Obj(Dictionary<string, object> o, string k) => Get(o, k) as Dictionary<string, object> ?? throw Bad("\"" + k + "\" is not an object");
        static List<object> Arr(Dictionary<string, object> o, string k) => Get(o, k) as List<object> ?? throw Bad("\"" + k + "\" is not an array");
        static string Str(Dictionary<string, object> o, string k) => Get(o, k) as string ?? throw Bad("\"" + k + "\" is not a string");
        static double Num(Dictionary<string, object> o, string k) => Get(o, k) is double d ? d : throw Bad("\"" + k + "\" is not a number");
        static double D(string s) => double.Parse(s, NumberStyles.Float, Inv);

        static double[] Nums(Dictionary<string, object> o, string k, int n)
        {
            var a = Arr(o, k);
            if (a.Count != n || a.Any(v => !(v is double))) throw Bad("\"" + k + "\" is not " + n + " numbers");
            return a.Select(v => (double)v).ToArray();
        }

        static Vector2[] Points(object v, string where)
        {
            var a = v as List<object> ?? throw Bad(where + " is not an array of points");
            var o = new Vector2[a.Count];
            for (int i = 0; i < a.Count; i++)
            {
                var t = a[i] as List<object>;
                if (t == null || t.Count != 2 || !(t[0] is double x) || !(t[1] is double y)) throw Bad(where + "[" + i + "] is not [x, y]");
                o[i] = new Vector2((float)x, (float)y);
            }
            return o;
        }

        static string Sha(byte[] b)
        {
            using (var h = SHA256.Create()) return TerrainPlanResult.Hex(h.ComputeHash(b));
        }
    }
}
