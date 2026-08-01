using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Which build this is (M1 §7.8's exit criterion: a tester "can tell you which build they were on").
    ///
    /// <para><b>Why <see cref="Application.buildGUID"/> and not a date.</b> Two builds cut from the same
    /// version number on the same afternoon are the common case in a playtest week, and the version alone
    /// cannot tell them apart. A build date could — but a date has to be STAMPED at build time, which means
    /// a build hook writing a generated asset (the build lane's, not this one's) and a file that is wrong
    /// the moment anyone forgets to run it. Unity already mints a fresh GUID for every player build and
    /// leaves it empty in the editor, so seven characters of it name the build exactly, cost nothing, and
    /// cannot go stale. If the release lane later wants a date or a commit hash, this is the one place that
    /// changes.</para>
    ///
    /// <para><b>What the version says is the project's business, not this file's.</b> It reads
    /// <see cref="Application.version"/> — ProjectSettings' <c>bundleVersion</c> — verbatim. If that number
    /// is wrong for the milestone, it is wrong in ProjectSettings, and this stamp is how anyone would ever
    /// find that out.</para>
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>How much of the build GUID to show. Seven is git's habit for a short hash and is
        /// plenty to tell a playtest week's builds apart, while still fitting a corner of the page.</summary>
        public const int BuildIdLength = 7;

        /// <summary>The line the shell puts in its corner. Built on demand — a page asks once, when it is
        /// built, and never again (rule 7).</summary>
        public static string StampLine
            => ShellStrings.BuildStamp(Application.version,
                                       ShortBuildId(Application.buildGUID),
                                       Debug.isDebugBuild);

        /// <summary>The first <see cref="BuildIdLength"/> characters of a build GUID; empty for an empty
        /// one (the editor, which never made a build). Null-safe.</summary>
        public static string ShortBuildId(string buildGuid)
        {
            if (string.IsNullOrEmpty(buildGuid)) return string.Empty;
            return buildGuid.Length <= BuildIdLength ? buildGuid : buildGuid.Substring(0, BuildIdLength);
        }
    }
}
