using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>A CLIFF FACE STYLE, BAKED BY THE KIT ITSELF.</b> Renders a <see cref="CliffFaceStyleDef"/>'s faces with
    /// the px kit's own <c>face()</c> and <c>channels()</c>: in a fresh rig host, after the rig, it loads the
    /// style's add-on painter (<see cref="CliffFaceStyleDef.PainterScriptPath"/>) and, for each face, swaps the
    /// rock's painter for the style's with the Def's tunables, renders, and puts the rock's painter back. The
    /// form field, tiers, cast shadow, channels and palette rows are the kit's, so a style face is co-registered
    /// with the kit face of the same rock, aspect and batter and draws through any palette of that rock.
    ///
    /// <para>The rig file is executed unmodified (ADR 0021 §5); the add-on is a separate file.</para>
    ///
    /// <para><b>Committed, like the kit.</b> The faces land in <see cref="CliffFaceStyleDef.OutputFolder"/> as
    /// PNGs (LFS) with metas whose GUIDs are a hash of the file's place (<see cref="StableGuid"/>), written before
    /// Unity first sees the file, so a bake from nothing names the same assets the Def holds. A face is written
    /// only when its pixels differ. CI and a fresh checkout read the committed files; nothing bakes at load.
    /// <c>CliffFaceStyleBakeTests</c> holds a committed face equal to a fresh bake of its Def.</para>
    /// </summary>
    public static class CliffFaceStyleBaker
    {
        const string PG = CliffCatalog.PxRigGlobalName;
        const string ResultGlobal = "__hhCliffStyleBake";

        /// <summary>What every style GUID's hash key starts with (never a kit GUID: that namespace differs).</summary>
        public const string GuidNamespace = "hh.cliff-face-style/";

        /// <summary>A style face file's GUID: MD5 of the namespace plus its asset path.</summary>
        public static string StableGuid(string assetPath)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(GuidNamespace + assetPath.Replace('\\', '/')));
                var hex = new StringBuilder(32);
                foreach (byte b in hash) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>The asset path of one of a style's face channels.</summary>
        public static string FacePath(CliffFaceStyleDef style, string aspect, int batter, string channel)
        {
            string kitName = CliffCatalog.FaceName(style.Rock, aspect, batter, CliffCatalog.BaseStep, channel);
            string tail = kitName.Substring(kitName.IndexOf('_'));      // _S_S76_index
            return $"{style.OutputFolder}/{style.FilePrefix}{tail}.png";
        }

        /// <summary>A rig host with the px kit and the style's add-on painter installed.</summary>
        public static IRigScriptHost CreateHost(CliffFaceStyleDef style)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            string painter = Path.Combine(RigCatalog.RepoRoot, style.PainterScriptPath);
            if (!File.Exists(painter))
                throw new FileNotFoundException($"[cliff-style] {style.Id}: painter script missing at {painter}.", painter);
            IRigScriptHost host = RigScriptHostFactory.Create();
            try
            {
                CliffBaker.InstallPxRig(host);
                host.Execute(File.ReadAllText(painter));
                if (!host.EvaluateBool("typeof PxCliffCoursed === 'object' && typeof PxCliffCoursed.painter === 'function'"))
                    throw new InvalidOperationException($"[cliff-style] {style.Id}: the painter did not install PxCliffCoursed.painter().");
                if (!host.EvaluateBool($"!!{PG}.DEF['{style.Rock}']"))
                    throw new InvalidOperationException($"[cliff-style] {style.Id}: the kit has no rock '{style.Rock}'.");
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        /// <summary>Renders one face in the style; leaves its channels on <see cref="ResultGlobal"/>.</summary>
        public static void Render(IRigScriptHost host, CliffFaceStyleDef style, string aspect, string slope,
                                  out int w, out int h)
        {
            string rock = style.Rock;
            // ONE script: the painter swapped in, face then channels (channels() reads the batter face() set),
            // and the rock's own painter put back whatever happens.
            host.Execute(
                $"(function(){{ var d = {PG}.DEF['{rock}'], keep = d.paint; " +
                $"d.paint = PxCliffCoursed.painter({style.OptionsJson()}); " +
                $"try {{ var b = {PG}.face('{rock}', '{aspect}', {CliffCatalog.BaseStep}, {{slope: '{slope}'}}); " +
                $"b.__ch = {PG}.channels(b, {{index: true}}); globalThis.{ResultGlobal} = b; }} " +
                $"finally {{ d.paint = keep; }} }})();");
            w = (int)host.EvaluateNumber($"{ResultGlobal}.W");
            h = (int)host.EvaluateNumber($"{ResultGlobal}.H");
            CliffBaker.AssertFaceCanvas(rock, aspect, slope, w, h);
        }

        /// <summary>One channel of the last render, top row first.</summary>
        public static byte[] Channel(IRigScriptHost host, string channel) =>
            host.EvaluateBytes($"{ResultGlobal}.__ch['{channel}']");

        /// <summary>Whether the committed PNG at <paramref name="assetPath"/> decodes to exactly these top-row-first pixels.</summary>
        public static bool MatchesCommitted(string assetPath, byte[] topFirstRgba, int w, int h)
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, assetPath);
            return File.Exists(abs) && CliffBaker.SamePixels(File.ReadAllBytes(abs), topFirstRgba, w, h);
        }

        /// <summary>Bakes every face of a style, writes the ones whose pixels changed, imports them with the kit's
        /// face contract and fills the Def's face list. Returns how many PNGs it wrote.</summary>
        public static int Bake(CliffFaceStyleDef style)
        {
            var faces = new List<CliffStyleFace>();
            int wrote = 0;
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, style.OutputFolder));
            WriteFolderMetas(style.OutputFolder);
            var written = new List<(string path, string channel)>();
            using (IRigScriptHost host = CreateHost(style))
            {
                foreach (int batter in CliffBaker.DefaultBatters)
                {
                    string slope = CliffCatalog.Batters[batter];
                    foreach (string aspect in CliffCatalog.Aspects)
                    {
                        Render(host, style, aspect, slope, out int w, out int h);
                        foreach (string channel in CliffCatalog.PxLiveChannels)
                        {
                            byte[] rgba = Channel(host, channel);
                            CliffBaker.AssertChannel(style.Rock, aspect, slope, channel, w, h, rgba);
                            string path = FacePath(style, aspect, batter, channel);
                            string abs = Path.Combine(RigCatalog.RepoRoot, path);
                            WriteMeta(path);
                            if (!File.Exists(abs) || !CliffBaker.SamePixels(File.ReadAllBytes(abs), rgba, w, h))
                            {
                                File.WriteAllBytes(abs, CliffBaker.EncodePng(rgba, w, h));
                                wrote++;
                            }
                            written.Add((path, channel));
                        }
                    }
                }
            }
            AssetDatabase.Refresh();
            foreach ((string path, string channel) in written)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                // A fresh 384x288 (4:3) PNG is guessed a cross-layout CUBEMAP by the default importer; the kit's
                // contract leaves the shape alone because its metas already say 2D, so say it here.
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureShape != TextureImporterShape.Texture2D)
                {
                    ti.textureShape = TextureImporterShape.Texture2D;
                    ti.SaveAndReimport();
                }
                CliffBaker.ApplyImportSettings(path, CliffAssetKind.Face, channel);
            }
            foreach (int batter in CliffBaker.DefaultBatters)
                foreach (string aspect in CliffCatalog.Aspects)
                {
                    string kit = CliffBaker.FaceAssetPath(CliffCatalog.BakeRoot, style.Rock, aspect, batter,
                                                          CliffCatalog.IndexChannel);
                    faces.Add(new CliffStyleFace
                    {
                        Base = AssetDatabase.LoadAssetAtPath<Texture2D>(kit),
                        Index = AssetDatabase.LoadAssetAtPath<Texture2D>(FacePath(style, aspect, batter, CliffCatalog.IndexChannel)),
                        Normal = AssetDatabase.LoadAssetAtPath<Texture2D>(FacePath(style, aspect, batter, "_normal")),
                        Mask = AssetDatabase.LoadAssetAtPath<Texture2D>(FacePath(style, aspect, batter, "_mask")),
                    });
                }
            foreach (CliffStyleFace f in faces)
                if (!f.HasChannels)
                    foreach ((string label, Texture2D t, string p) in new[]
                             {
                                 ("index", f.Index, FacePathFor(style, f.Base, CliffCatalog.IndexChannel)),
                                 ("normal", f.Normal, FacePathFor(style, f.Base, "_normal")),
                                 ("mask", f.Mask, FacePathFor(style, f.Base, "_mask")),
                             })
                        if (t == null)
                        {
                            UnityEngine.Object main =AssetDatabase.LoadMainAssetAtPath(p);
                            Debug.LogError($"[cliff-style] {label} did not load at '{p}': main asset " +
                                           $"{(main != null ? main.GetType().Name : "null")}, guid " +
                                           $"'{AssetDatabase.AssetPathToGUID(p)}', on disk {File.Exists(Path.Combine(RigCatalog.RepoRoot, p))}");
                        }
            foreach (CliffStyleFace f in faces)
                if (!f.HasChannels)
                    throw new InvalidOperationException($"[cliff-style] {style.Id}: a face did not load after the bake " +
                                                        $"(base {(f.Base != null ? f.Base.name : "missing")}).");
            style.Faces = faces;
            EditorUtility.SetDirty(style);
            return wrote;
        }

        static string FacePathFor(CliffFaceStyleDef style, Texture2D kitIndex, string channel) =>
            kitIndex == null ? "(no base)"
                : $"{style.OutputFolder}/{style.FilePrefix}{kitIndex.name.Substring(kitIndex.name.IndexOf('_')).Replace(CliffCatalog.IndexChannel, channel)}.png";

        static void WriteMeta(string assetPath)
        {
            string meta = Path.Combine(RigCatalog.RepoRoot, assetPath + ".meta");
            if (File.Exists(meta)) return;
            File.WriteAllText(meta, $"fileFormatVersion: 2\nguid: {StableGuid(assetPath)}\n", new UTF8Encoding(false));
        }

        static void WriteFolderMetas(string folder)
        {
            string f = folder.Replace('\\', '/').TrimEnd('/');
            while (f.Contains("/") && f != "Assets")
            {
                string meta = Path.Combine(RigCatalog.RepoRoot, f + ".meta");
                if (!File.Exists(meta))
                    File.WriteAllText(meta, $"fileFormatVersion: 2\nguid: {StableGuid(f)}\nfolderAsset: yes\n" +
                                            "DefaultImporter:\n  externalObjects: {}\n  userData: \n" +
                                            "  assetBundleName: \n  assetBundleVariant: \n", new UTF8Encoding(false));
                f = f.Substring(0, f.LastIndexOf('/'));
            }
        }

        /// <summary>Bakes every style in the project.</summary>
        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Styles")]
        public static void BakeAll()
        {
            int styles = 0, wrote = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CliffFaceStyleDef)))
            {
                var style = AssetDatabase.LoadAssetAtPath<CliffFaceStyleDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (style == null) continue;
                styles++;
                wrote += Bake(style);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[cliff-style] baked {styles} style(s), wrote {wrote} PNG(s).");
        }

        /// <summary>The batchmode entry: bakes the skin palettes, then the styles.
        /// <c>-executeMethod HiddenHarbours.Tools.RigBaking.CliffFaceStyleBaker.BakeAllBatch</c>.</summary>
        public static void BakeAllBatch()
        {
            try { CliffSkinPaletteBaker.BakeAll(); BakeAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
