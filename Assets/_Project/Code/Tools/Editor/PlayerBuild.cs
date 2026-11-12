using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>Local Win64 player entry point. The shell script owns the slot and packaging.</summary>
    public static class PlayerBuild
    {
        public const string DefaultProduct = "Red Skies At Night";
        public const string Company = "One More Minute Games";
        const string Version = "0.1";

        // Runtime region scenes in main's order. Greywick is a legacy town scene;
        // Greybox and SampleScene are prototypes. WestWater has no committed scene.
        static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/StPeters.unity",
            "Assets/_Project/Scenes/NineMileCreek.unity",
            "Assets/_Project/Scenes/RicePoint.unity",
            "Assets/_Project/Scenes/Cumberland.unity",
            "Assets/_Project/Scenes/EastWater.unity"
        };

        [MenuItem("Hidden Harbours/Build/Windows Player (slot required)")]
        public static void BuildFromMenu()
        {
            string output = EditorUtility.OpenFolderPanel("Player output (outside the project)", "", "");
            if (string.IsNullOrEmpty(output)) return;
            Build(output, DefaultProduct, GitRevision("HEAD"), GitRevision("origin/main"), false, false);
        }

        /// <summary>Use with -executeMethod HiddenHarbours.Tools.Editor.PlayerBuild.BuildWindows.</summary>
        public static void BuildWindows()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string compression = Argument(args, "-buildCompression", "None");
            if (compression != "None" && compression != "Lz4HC")
                throw new ArgumentException("-buildCompression must be None or Lz4HC.");
            Build(Argument(args, "-buildOutput"), Argument(args, "-buildProduct", DefaultProduct),
                Argument(args, "-buildCommit"), Argument(args, "-buildMainCommit"),
                args.Contains("-buildDevelopment"), compression == "Lz4HC");
        }

        static void Build(string output, string product, string commit, string mainCommit,
            bool development, bool lz4hc)
        {
            output = Path.GetFullPath(output);
            string buildRoot = Path.GetFullPath("C:/hh-builds").TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!output.StartsWith(buildRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Player output must be under the private build root.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (output.Equals(project, StringComparison.OrdinalIgnoreCase) ||
                output.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Player output must be outside the project.");
            if (string.IsNullOrWhiteSpace(product) || product.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                product.EndsWith(".", StringComparison.Ordinal) || product.EndsWith(" ", StringComparison.Ordinal))
                throw new ArgumentException("The product must be a valid Windows file name.");
            if (!IsCommit(commit) || !IsCommit(mainCommit))
                throw new ArgumentException("Source and main commits must be full commit hashes.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Install Windows Mono build support for the pinned editor.");
            foreach (string scene in Scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    throw new BuildFailedException("Missing player scene: " + scene);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new BuildFailedException("Use an empty output folder so stale player files cannot ship.");

            EnsureRoadBake();
            EnsureCliffBake();
            Directory.CreateDirectory(output);
            string oldProduct = PlayerSettings.productName;
            string oldCompany = PlayerSettings.companyName;
            string oldVersion = PlayerSettings.bundleVersion;
            var target = NamedBuildTarget.Standalone;
            ScriptingImplementation oldBackend = PlayerSettings.GetScriptingBackend(target);
            try
            {
                PlayerSettings.productName = product;
                PlayerSettings.companyName = Company;
                PlayerSettings.bundleVersion = Version;
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
                BuildOptions options = BuildOptions.DetailedBuildReport;
                if (development) options |= BuildOptions.Development;
                if (lz4hc) options |= BuildOptions.CompressWithLz4HC;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = Path.Combine(output, product + ".exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = options
                });
                if (report == null) throw new BuildFailedException("No player build report was returned.");
                WriteReport(output, report);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Player build failed: " + report.summary.result);
                File.WriteAllLines(Path.Combine(output, "build-info.txt"), new[]
                {
                    "main-commit: " + mainCommit,
                    "source-commit: " + commit,
                    "built-utc: " + DateTime.UtcNow.ToString("O"),
                    "editor: " + Application.unityVersion,
                    "configuration: " + (development ? "development" : "release"),
                    "target: Windows x64 / Mono",
                    "compression: " + (lz4hc ? "Lz4HC" : "None"),
                    "product: " + product,
                    "company: " + Company,
                    "version: " + Version
                }, new UTF8Encoding(false));
            }
            finally
            {
                PlayerSettings.productName = oldProduct;
                PlayerSettings.companyName = oldCompany;
                PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.SetScriptingBackend(target, oldBackend);
            }
            Debug.Log("[player-build] Completed; product, company, version and backend restored.");
        }

        static void EnsureCliffBake()
        {
            // RigBaking has its own Editor assembly; do not couple Tools.Editor to its JS dependencies.
            Type baker = Type.GetType("HiddenHarbours.Tools.RigBaking.CliffBakeMenu, " +
                "HiddenHarbours.Tools.RigBaking.Editor", true);
            if (!System.Environment.GetCommandLineArgs().Contains("-buildReuseBakedAssets"))
                baker.GetMethod("EnsureBaked").Invoke(null, new object[] { null });
            if (!(bool)baker.GetProperty("IsBaked").GetValue(null))
                throw new BuildFailedException("The cliff face bake is incomplete.");
        }

        static void EnsureRoadBake()
        {
            const string root = "Assets/StreamingAssets/RoadFloor/NineMileCreek";
            if (!File.Exists(Path.Combine(root, "RoadFloor.json")) ||
                Directory.GetFiles(root, "*.png").Length == 0)
                throw new BuildFailedException("Run tools/build/build-windows.ps1 to bake the road floor first.");
        }

        static void WriteReport(string output, BuildReport report)
        {
            var text = new StringBuilder();
            text.AppendLine("result: " + report.summary.result);
            text.AppendLine("bytes: " + report.summary.totalSize);
            text.AppendLine("seconds: " + report.summary.totalTime.TotalSeconds.ToString(
                "F3", System.Globalization.CultureInfo.InvariantCulture));
            text.AppendLine("errors: " + report.summary.totalErrors);
            text.AppendLine("warnings: " + report.summary.totalWarnings);
            text.AppendLine("scenes:");
            foreach (string scene in Scenes) text.AppendLine(scene);
            text.AppendLine("largest packed assets (bytes, asset):");
            foreach (var asset in report.packedAssets.SelectMany(p => p.contents)
                .GroupBy(a => a.sourceAssetPath)
                .Select(g => new { Path = g.Key, Bytes = g.Aggregate(0UL, (sum, a) => sum + a.packedSize) })
                .OrderByDescending(a => a.Bytes).Take(20))
                text.AppendLine(asset.Bytes + "\t" + asset.Path);
            File.WriteAllText(Path.Combine(output, "build-report.txt"), text.ToString(), new UTF8Encoding(false));
        }

        static string Argument(string[] args, string key, string fallback = null)
        {
            int index = Array.IndexOf(args, key);
            if (index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal))
                return args[index + 1];
            if (index >= 0 || fallback == null) throw new ArgumentException("Missing value for " + key);
            return fallback;
        }

        static bool IsCommit(string value) => value != null && value.Length == 40 && value.All(Uri.IsHexDigit);

        static string GitRevision(string revision)
        {
            var start = new ProcessStartInfo("git", "rev-parse " + revision)
            {
                WorkingDirectory = Path.Combine(Application.dataPath, ".."),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.EnvironmentVariables["GIT_NO_LAZY_FETCH"] = "1";
            start.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";
            using (Process process = Process.Start(start))
            {
                string value = process.StandardOutput.ReadToEnd().Trim();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0 || !IsCommit(value)) throw new BuildFailedException("git rev-parse: " + error);
                return value;
            }
        }
    }
}
