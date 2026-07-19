using System;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.JsEngine
{
    /// <summary>
    /// Forces every binary under <see cref="PluginFolder"/> to import as an EDITOR-ONLY plugin.
    ///
    /// ⚠️ THIS IS THE LICENCE BASIS, NOT HYGIENE. ⚠️
    ///
    /// The ClearScript/V8 bundle these files belong to includes a GNU C Library component under
    /// LGPL-2.1 (see THIRD-PARTY.md next to the binaries). The owner accepted that dependency
    /// knowingly and specifically because the binaries are unmodified, used only by the Unity
    /// editor, and never linked into or shipped in a player build — the configuration LGPL-2.1
    /// most readily permits. Hidden Harbours is headed for commercial release.
    ///
    /// So if you are reading this because the fence is in your way — because you want to call the
    /// baker from runtime code, or a build is complaining about a missing native library — DO NOT
    /// loosen it. Weakening this fence reopens the licence question and ADR 0021 must be revisited
    /// before shipping. Move your code into the editor instead, or bake the output to an asset.
    ///
    /// This postprocessor is the self-healing half of fence #1; <c>JsEnginePluginFenceTests</c>
    /// reads the settings back and fails CI if they ever drift. Fence #2 is the asmdef
    /// (<c>includePlatforms: ["Editor"]</c> + <c>overrideReferences</c>) on the baker assembly.
    /// </summary>
    sealed class JsEnginePluginFence : AssetPostprocessor
    {
        public const string PluginFolder = "Assets/_Project/Plugins/Editor/JsEngine";

        /// <summary>
        /// Build targets the fence explicitly denies. Deliberately names the platforms the game
        /// actually ships to rather than iterating every enum value, so a new Unity build target
        /// cannot quietly become "allowed by omission" — <see cref="ApplyTo"/> also clears the
        /// any-platform flag, which is what actually keeps unknown targets out.
        /// </summary>
        public static readonly BuildTarget[] DeniedTargets =
        {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneWindows,
            BuildTarget.StandaloneLinux64,
            BuildTarget.StandaloneOSX,
            BuildTarget.Android,
            BuildTarget.iOS,
            BuildTarget.WebGL,
        };

        public static bool IsFencedPath(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) &&
            assetPath.Replace('\\', '/').StartsWith(PluginFolder + "/", StringComparison.Ordinal);

        void OnPreprocessAsset()
        {
            if (assetImporter is PluginImporter pi && IsFencedPath(assetPath))
                ApplyTo(pi);
        }

        /// <summary>
        /// The one place the editor-only settings are defined. The test asserts the same
        /// predicate, so this method and the test must agree or CI goes red.
        /// </summary>
        public static void ApplyTo(PluginImporter pi)
        {
            // The load-bearing line: no "Any Platform" means the plugin is opt-in per platform,
            // and below we only ever opt in to the editor.
            pi.SetCompatibleWithAnyPlatform(false);
            pi.SetCompatibleWithEditor(true);

            foreach (var t in DeniedTargets)
                pi.SetCompatibleWithPlatform(t, false);

            // Never preload into the player domain, and never let a runtime asmdef reference it.
            pi.isPreloaded = false;
            pi.SetExcludeEditorFromAnyPlatform(false);
        }
    }
}
