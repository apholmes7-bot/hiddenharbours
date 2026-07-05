#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The <b>ADR 0019 Phase-1 safety net</b>: the ONE shared wipe-warning guard every region builder's
    /// CREATE / <c>Build</c> path calls before it clears a scene from zero.
    ///
    /// <para><b>Why this exists.</b> A from-zero build (<c>EditorSceneManager.NewScene(EmptyScene, Single)</c>
    /// → rebuild) discards <em>everything</em> the owner hand-authored in that scene — painted terrain, decor,
    /// and (the incident that motivated this) hand-placed lights like a boat spotlight. ADR 0011 shipped this
    /// guard on the cove pilot only; ADR 0019 §1 makes it <b>mandatory on EVERY region builder's CREATE,
    /// immediately — including not-yet-adopted Greywick / St Peters</b>, keyed purely on the scene FILE
    /// EXISTING on disk (no adoption bookkeeping). One helper = one wording, so all builders behave
    /// identically (CLAUDE.md rule 6 — no divergent copies of the same rule).</para>
    ///
    /// <para>The safe alternative the dialog points at — <c>Refresh &lt;Region&gt; Logic</c> — rebuilds ONLY
    /// the tagged <c>--LOGIC--</c> subtree and never touches the owner's layer (ADR 0011 Option A).</para>
    /// </summary>
    public static class RegionBuildGuard
    {
        /// <summary>
        /// The pure, headlessly-testable seam: a from-zero CREATE should warn IFF the scene already exists on
        /// disk. First-ever build (no file) proceeds silently; a re-run over a committed/hand-edited scene must
        /// warn. Keyed on file-exists alone — no adoption state needed (ADR 0019 §1). No dialogs, no editor
        /// side effects, so an EditMode test can pin the contract.
        /// </summary>
        public static bool ShouldWarn(string scenePath)
        {
            return !string.IsNullOrEmpty(scenePath) && File.Exists(scenePath);
        }

        /// <summary>
        /// Guard a region builder's CREATE / from-zero rebuild. Returns <c>true</c> if the builder may proceed
        /// to clear + rebuild the scene, <c>false</c> if it must ABORT and touch nothing.
        ///
        /// <para>If <paramref name="scenePath"/> does NOT exist (first-ever build) this returns <c>true</c>
        /// silently. If it DOES exist, it shows the modal wipe warning; the builder proceeds only if the user
        /// confirms the destructive rebuild. On cancel it logs the safe alternative and returns <c>false</c>.</para>
        /// </summary>
        /// <param name="regionName">Human name of the region (e.g. "Port Greywick") — shown in the dialog and
        /// used to name the safe <c>Refresh &lt;Region&gt; Logic</c> command.</param>
        /// <param name="scenePath">The committed scene's on-disk asset path (e.g.
        /// "Assets/_Project/Scenes/Greywick.unity").</param>
        public static bool ConfirmOverwrite(string regionName, string scenePath)
        {
            // No file yet → first-ever build, nothing to wipe. Proceed silently (ADR 0019 §1).
            if (!ShouldWarn(scenePath))
                return true;

            bool proceed = EditorUtility.DisplayDialog(
                "Hidden Harbours — full rebuild will WIPE hand-authored visuals",
                $"'Build {regionName} Scene' rebuilds {regionName} FROM ZERO. It will DISCARD any terrain " +
                "you've painted and any decor, lights, or other objects you've placed by hand (the whole " +
                "hand-authored layer).\n\nIf you only want to update the gameplay logic and KEEP your work, " +
                $"cancel and run 'Hidden Harbours ▸ Refresh {regionName} Logic' instead.\n\n" +
                "Rebuild from zero anyway?",
                "Rebuild from zero (lose hand work)", "Cancel");

            if (!proceed)
            {
                Debug.Log($"[RegionBuildGuard] Full rebuild of {regionName} cancelled — run 'Refresh " +
                          $"{regionName} Logic' to update logic without touching the hand-authored layer " +
                          "(ADR 0019 §1 / ADR 0011).");
            }

            return proceed;
        }
    }
}
#endif
