using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⚠️ THESE TESTS ARE THE LICENCE BASIS FOR SHIPPING THIS GAME. ⚠️
    ///
    /// The ClearScript/V8 bundle under Assets/_Project/Plugins/Editor/JsEngine includes a GNU C
    /// Library component under LGPL-2.1. The owner accepted it knowingly and SPECIFICALLY because
    /// the binaries are unmodified, editor-only, and never reach a player build (ADR 0021). If
    /// either fence below is loosened, that basis weakens with it and ADR 0021 must be revisited
    /// before release.
    ///
    /// So: if one of these fails, the answer is never "relax the assert". It is either "put the
    /// plugin settings back" or "go and re-open the ADR".
    /// </summary>
    public class JsEnginePluginFenceTests
    {
        const string PluginFolder = "Assets/_Project/Plugins/Editor/JsEngine";
        const string BakerAsmdef =
            "Assets/_Project/Code/Tools/Editor/RigBaking/HiddenHarbours.Tools.RigBaking.Editor.asmdef";

        static readonly string[] Binaries =
        {
            "ClearScript.Core.dll",
            "ClearScript.V8.dll",
            "ClearScript.V8.ICUData.dll",
            "ClearScriptV8.win-x64.dll",
            "ClearScriptV8.linux-x64.so",
        };

        /// <summary>Targets the game actually ships to. The any-platform flag is what keeps unknown
        /// future targets out; this list makes the important ones explicit and legible.</summary>
        static readonly BuildTarget[] MustNotShipTo =
        {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneWindows,
            BuildTarget.StandaloneLinux64,
            BuildTarget.StandaloneOSX,
            BuildTarget.Android,
            BuildTarget.iOS,
            BuildTarget.WebGL,
        };

        [Test]
        public void EveryBinary_IsPresent()
        {
            foreach (var f in Binaries)
                Assert.IsTrue(File.Exists(Path.Combine(
                                  Directory.GetParent(Application.dataPath)!.FullName,
                                  PluginFolder, f)),
                              $"{f} is missing from {PluginFolder}.");
        }

        /// <summary>
        /// ⚠️ The 11 MB ICU data assembly is required and its absence FAILS SILENTLY: Unity reports
        /// "Unable to resolve reference 'ClearScript.V8.ICUData'" and then skips the WHOLE assembly,
        /// so the run reports total=0 and exits 0 — which reads GREEN. This test exists so that
        /// failure mode becomes a red test instead of a phantom pass.
        /// </summary>
        [Test]
        public void IcuDataAssembly_IsPresent_AndFullSize()
        {
            string p = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                                    PluginFolder, "ClearScript.V8.ICUData.dll");
            Assert.IsTrue(File.Exists(p), "ClearScript.V8.ICUData.dll is missing.");

            long len = new FileInfo(p).Length;
            // ~10.8 MB. A tiny file here means an LFS pointer was committed as the real thing, or
            // the file was truncated — both of which would otherwise surface as total=0.
            Assert.Greater(len, 5_000_000L,
                $"ClearScript.V8.ICUData.dll is only {len} bytes. That is an LFS pointer or a " +
                "truncated file, not the real 11 MB assembly — V8 will fail to load and the test " +
                "run will silently report total=0.");
        }

        // ---- FENCE 1: the PluginImporter ------------------------------------------------------

        [Test]
        public void Fence1_EveryBinary_ImportsEditorOnly([ValueSource(nameof(Binaries))] string dll)
        {
            string path = $"{PluginFolder}/{dll}";
            var imp = AssetImporter.GetAtPath(path) as PluginImporter;
            Assert.IsNotNull(imp, $"{path} is not imported as a PluginImporter.");

            Assert.IsFalse(imp.GetCompatibleWithAnyPlatform(),
                $"{dll} is marked compatible with ANY platform. This is the fence that keeps an " +
                "LGPL-2.1 component out of the player build — see THIRD-PARTY.md and ADR 0021. " +
                "Do not loosen it.");

            Assert.IsTrue(imp.GetCompatibleWithEditor(),
                $"{dll} is not editor-compatible, so the baker cannot load it.");

            foreach (var t in MustNotShipTo)
                Assert.IsFalse(imp.GetCompatibleWithPlatform(t),
                    $"{dll} would ship to {t}. See THIRD-PARTY.md — this is a licence fence.");

            Assert.IsFalse(imp.isPreloaded, $"{dll} is marked preloaded.");
        }

        // ---- FENCE 2: the asmdef --------------------------------------------------------------

        [Test]
        public void Fence2_BakerAssembly_IsEditorOnly_AndOverridesReferences()
        {
            string abs = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, BakerAsmdef);
            Assert.IsTrue(File.Exists(abs), $"{BakerAsmdef} is missing.");
            string json = File.ReadAllText(abs);

            StringAssert.Contains("\"includePlatforms\"", json);
            StringAssert.Contains("\"Editor\"", json);
            Assert.IsTrue(json.Contains("\"overrideReferences\": true"),
                "The baker asmdef must set overrideReferences:true so no runtime assembly can " +
                "resolve the ClearScript DLLs. This is fence #2 of the licence basis (ADR 0021 §3).");

            foreach (var dll in new[] { "ClearScript.Core.dll", "ClearScript.V8.dll",
                                        "ClearScript.V8.ICUData.dll" })
                StringAssert.Contains(dll, json);

            // A crude but effective guard: an asmdef that lists any non-Editor platform has had its
            // fence widened.
            foreach (var bad in new[] { "\"Android\"", "\"iOS\"", "\"WindowsStandalone64\"",
                                        "\"LinuxStandalone64\"", "\"macOSStandalone\"", "\"WebGL\"" })
                Assert.IsFalse(json.Contains(bad),
                    $"The baker asmdef names {bad} — it must be Editor-only.");
        }

        [Test]
        public void Fence2_NoRuntimeAssembly_ReferencesTheJsEngine()
        {
            string root = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                                       "Assets/_Project/Code");
            foreach (var asmdef in Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                string json = File.ReadAllText(asmdef);
                if (!json.Contains("ClearScript")) continue;

                Assert.IsTrue(json.Contains("\"includePlatforms\"") && json.Contains("\"Editor\""),
                    $"{asmdef} references ClearScript but is not Editor-only. That would put an " +
                    "LGPL-2.1 native component into a player build — see THIRD-PARTY.md.");
            }
        }

        [Test]
        public void ThirdPartyNotices_ShipAlongsideTheBinaries_WithTheFullLicenceText()
        {
            string p = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                                    PluginFolder, "THIRD-PARTY.md");
            Assert.IsTrue(File.Exists(p),
                "THIRD-PARTY.md is missing. ADR 0021 requires the full bundled licence text to " +
                "ship with the binaries.");

            string text = File.ReadAllText(p);
            // The specific component the owner was asked about and accepted knowingly.
            StringAssert.Contains("GNU C Library", text);
            StringAssert.Contains("Lesser General Public License", text);
            Assert.Greater(text.Length, 60_000,
                "THIRD-PARTY.md looks truncated — it must carry the FULL bundled licence text " +
                "(~88 KB), not a summary.");
        }
    }
}
