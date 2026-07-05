#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Pins the ADR 0019 §1 wipe-warning seam: <see cref="RegionBuildGuard.ShouldWarn"/> must warn IFF the
    /// scene file exists on disk — the file-exists key the ADR chose so no adoption bookkeeping is needed.
    /// A first-ever build (no file) proceeds silently; a re-run over a committed/hand-edited scene warns, so
    /// a builder re-run can never silently wipe hand work (the boat-spotlight incident).
    ///
    /// <para>Only the pure predicate is unit-tested — the modal
    /// <see cref="RegionBuildGuard.ConfirmOverwrite"/> path shows an <c>EditorUtility.DisplayDialog</c>, which
    /// can't run headlessly; it's verified by the owner running Build on an existing scene and seeing the
    /// prompt.</para>
    /// </summary>
    public class RegionBuildGuardTests
    {
        [Test]
        public void ShouldWarn_False_WhenSceneFileDoesNotExist()
        {
            // A path that cannot exist on disk → first-ever build, nothing to wipe → no warning.
            string missing = Path.Combine(Path.GetTempPath(), "hh_no_such_scene_" + Path.GetRandomFileName() + ".unity");
            Assert.IsFalse(File.Exists(missing), "test precondition: the path must not exist");

            Assert.IsFalse(RegionBuildGuard.ShouldWarn(missing));
        }

        [Test]
        public void ShouldWarn_True_WhenSceneFileExists()
        {
            // A real file on disk → a re-run would wipe it → must warn.
            string existing = Path.GetTempFileName(); // creates the file
            try
            {
                Assert.IsTrue(File.Exists(existing), "test precondition: the file must exist");
                Assert.IsTrue(RegionBuildGuard.ShouldWarn(existing));
            }
            finally
            {
                File.Delete(existing);
            }
        }

        [Test]
        public void ShouldWarn_False_WhenPathIsNullOrEmpty()
        {
            // Defensive: a null/empty path is never a real scene → don't warn (and don't throw).
            Assert.IsFalse(RegionBuildGuard.ShouldWarn(null));
            Assert.IsFalse(RegionBuildGuard.ShouldWarn(string.Empty));
        }
    }
}
#endif
