#if UNITY_EDITOR
using System;
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
    /// immediately — including not-yet-adopted Nine Mile Creek / St Peters</b>, keyed purely on the scene FILE
    /// EXISTING on disk (no adoption bookkeeping). One helper = one wording, so all builders behave
    /// identically (CLAUDE.md rule 6 — no divergent copies of the same rule).</para>
    ///
    /// <para>The safe alternative the dialog points at — <c>Refresh &lt;Region&gt; Logic</c> — rebuilds ONLY
    /// the tagged <c>--LOGIC--</c> subtree and never touches the owner's layer (ADR 0011 Option A).</para>
    ///
    /// <para><b>Batchmode.</b> Nobody can answer a dialog in a <c>-batchmode -executeMethod</c> run, so there the
    /// guard never shows one: an existing scene means the rebuild is REFUSED, logged as an error naming the
    /// region and the scene path, and the editor exits with <see cref="RefusedRebuildExitCode"/>. Before this, the
    /// dialog answered no, <c>Build()</c> returned, and Unity exited 0 — a false green (ledger 09-19). There is
    /// deliberately no flag that lets a batchmode run confirm the wipe; that needs its own owner ruling.</para>
    /// </summary>
    public static class RegionBuildGuard
    {
        /// <summary>What the guard does with one CREATE request — see <see cref="Decide"/>.</summary>
        public enum Verdict
        {
            /// <summary>No scene on disk: nothing to wipe, build silently.</summary>
            Proceed,
            /// <summary>Scene exists, a person is at the editor: show the wipe warning and let them choose.</summary>
            Ask,
            /// <summary>Scene exists and nobody can be asked (batchmode): refuse, log an error, exit non-zero.</summary>
            RefuseAndFail,
        }

        /// <summary>
        /// The editor's exit code when a batchmode rebuild is refused because the scene already exists. Distinct
        /// from 1 (an <c>-executeMethod</c> that threw) and from 2 (a <c>-runTests</c> run with failures), so a log
        /// reader can tell "the guard refused" from "the build broke".
        /// </summary>
        public const int RefusedRebuildExitCode = 4;

        /// <summary>
        /// Test seam: whether this editor is running in batchmode. Defaults to <see cref="Application.isBatchMode"/>;
        /// EditMode tests replace it (and MUST restore it) so both answers can be driven from either kind of run.
        /// </summary>
        public static Func<bool> IsBatchMode = () => Application.isBatchMode;

        /// <summary>
        /// Test seam: how a refused batchmode rebuild ends the editor. Defaults to <see cref="EditorApplication.Exit"/>.
        /// CI runs EditMode in batchmode, so a test that reached the real exit would kill the whole run — tests
        /// replace this (and MUST restore it) before calling <see cref="ConfirmOverwrite"/>.
        /// </summary>
        public static Action<int> Exit = code => EditorApplication.Exit(code);

        /// <summary>
        /// The pure decision: a from-zero CREATE over no scene proceeds; over an existing scene it asks a person,
        /// or — in batchmode, where there is no one to ask — refuses and fails the run.
        /// </summary>
        public static Verdict Decide(bool sceneExists, bool isBatchMode)
        {
            if (!sceneExists)
                return Verdict.Proceed;
            return isBatchMode ? Verdict.RefuseAndFail : Verdict.Ask;
        }

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
        ///
        /// <para>In batchmode an existing scene is never asked about: the rebuild is refused with an error naming
        /// the region and the scene path, <see cref="Exit"/> is called with <see cref="RefusedRebuildExitCode"/>,
        /// and this returns <c>false</c> (so the builder still touches nothing if the exit is stubbed).</para>
        /// </summary>
        /// <param name="regionName">Human name of the region (e.g. "Nine Mile Creek") — shown in the dialog and
        /// used to name the safe <c>Refresh &lt;Region&gt; Logic</c> command.</param>
        /// <param name="scenePath">The committed scene's on-disk asset path (e.g.
        /// "Assets/_Project/Scenes/NineMileCreek.unity").</param>
        public static bool ConfirmOverwrite(string regionName, string scenePath)
        {
            switch (Decide(ShouldWarn(scenePath), IsBatchMode()))
            {
                case Verdict.Proceed:
                    // No file yet → first-ever build, nothing to wipe. Proceed silently (ADR 0019 §1).
                    return true;

                case Verdict.RefuseAndFail:
                    Debug.LogError($"[RegionBuildGuard] Refused a from-zero rebuild of {regionName} in batchmode: " +
                                   $"'{scenePath}' already exists, and rebuilding it would wipe the hand-authored " +
                                   "layer with no one to confirm. Nothing was touched; exiting with code " +
                                   $"{RefusedRebuildExitCode}. Run 'Refresh {regionName} Logic' to update logic only " +
                                   "(ADR 0019 §1 / ADR 0011).");
                    Exit(RefusedRebuildExitCode);
                    return false;
            }

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
