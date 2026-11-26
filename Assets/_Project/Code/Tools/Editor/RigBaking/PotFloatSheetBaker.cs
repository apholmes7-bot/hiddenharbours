using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The pot floats, baked through the nav buoys' own path.</b> The pot-float rig
    /// (<c>docs/art/rigs/pot-float-kit/potFloatRig.js</c>) is lathed on the SAME turntable as the nav
    /// kit (<c>deckIsoSolid</c>, the deck-loop kit's <c>isoSolid.js</c>), run in the same V8 host, and
    /// written to one sheet: three shapes left to right (bullet, ball, spar), one facing each, pivot on
    /// the still waterline. The sheet is baked in the rig's KEY coats; owners' colours are swapped in at
    /// draw time (<c>PotFloatPaint</c>), so there is exactly one file.
    ///
    /// <para><b>Host code, not a catalog entry.</b> The rig needs nothing from the catalog but its
    /// turntable, so this installs <c>deckIsoSolid</c> and runs the rig's source unmodified beside it.
    /// <c>PotFloatArtTests</c> re-renders the sheet this way and holds the committed PNG to it, pixel
    /// for pixel, so the picture in the repo cannot drift from its source.</para>
    /// </summary>
    public static class PotFloatSheetBaker
    {
        public const string RigPath = "docs/art/rigs/pot-float-kit/potFloatRig.js";
        public const string GlobalName = "PotFloat";
        public const string SheetPath = "Assets/_Project/Art/Fishing/PotFloats.png";

        /// <summary>Sheet cells left to right, as the rig's <c>SHAPES</c> lists them; each sprite is
        /// named <c>PotFloats_&lt;shape&gt;</c>.</summary>
        public static readonly string[] Shapes = { "bullet", "ball", "spar" };

        /// <summary>Install the turntable and the rig into <paramref name="host"/>.</summary>
        public static void Install(IRigScriptHost host)
        {
            RigCatalog.InstallModule(host, RigCatalog.Get("deckIsoSolid"));
            string full = Path.Combine(RigCatalog.RepoRoot, RigPath);
            if (!File.Exists(full)) throw new FileNotFoundException("The pot-float rig is missing.", full);
            host.Execute(File.ReadAllText(full));
            if (!host.EvaluateBool($"typeof {GlobalName} === 'object' && {GlobalName} !== null"))
                throw new InvalidOperationException($"{RigPath} ran but did not install globalThis.{GlobalName}.");
        }

        /// <summary>
        /// Render the whole sheet as Unity pixels (rows BOTTOM-up, as <see cref="Texture2D"/> stores
        /// them; the rig renders top-down). The host must already hold the rig (<see cref="Install"/>).
        /// </summary>
        public static Color32[] RenderSheet(IRigScriptHost host, out int width, out int height)
        {
            width = (int)host.EvaluateNumber($"{GlobalName}.W * {GlobalName}.SHAPES.length");
            height = (int)host.EvaluateNumber($"{GlobalName}.H");
            byte[] rgba = host.EvaluateBytes($"{GlobalName}.sheet({{}})");
            if (rgba.Length != width * height * 4)
                throw new InvalidOperationException(
                    $"The pot-float sheet came back {rgba.Length} bytes for {width}x{height}.");

            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int dst = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    px[dst + x] = new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
                }
            }
            return px;
        }

        [MenuItem("Hidden Harbours/Art/Bake Pot Floats (spindle, ball, spar)", priority = 69)]
        public static void BakeMenu()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            Install(host);
            Color32[] px = RenderSheet(host, out int w, out int h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply(false, false);
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, SheetPath), tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[PotFloatSheetBaker] baked {SheetPath} ({w}x{h}, {Shapes.Length} floats) on {host.EngineName}");
        }
    }
}
