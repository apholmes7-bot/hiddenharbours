using NUnit.Framework;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// M1 §7.8's exit criterion ends "...and can tell you which build they were on", so the shell puts a
    /// stamp in the corner of the title page. These pin the two things that make it worth having: it names
    /// a BUILD (not just a version — a playtest week cuts several builds from one version number), and it
    /// says so honestly in the editor, where no build was ever made.
    ///
    /// <para>The composition is parameterised for exactly this reason: <c>Application.buildGUID</c> is
    /// always empty in a test run, so a test of the live line could only ever exercise the editor case.</para>
    /// </summary>
    public class BuildStampTests
    {
        private const string Guid = "4f9c2a1b8e7d6c5a4b3c2d1e0f9a8b7c";

        // ---- what the line says --------------------------------------------------------------

        [Test]
        public void AStamp_NamesTheVersionAndTheBuild()
        {
            string line = ShellStrings.BuildStamp("0.1.0", BuildInfo.ShortBuildId(Guid), development: false);

            StringAssert.Contains("v0.1.0", line, "the version, written the way a person writes one");
            StringAssert.Contains("build 4f9c2a1", line,
                "and WHICH build of it — two builds cut from one version number are the playtest's " +
                "common case, and the version alone cannot tell them apart");
            StringAssert.DoesNotContain(ShellStrings.EditorLabel, line);
        }

        [Test]
        public void WithNoBuildId_ItSaysEditor_RatherThanNamingABuildThatDoesNotExist()
        {
            string line = ShellStrings.BuildStamp("0.1.0", string.Empty, development: true);

            StringAssert.Contains(ShellStrings.EditorLabel, line,
                "an editor session is not a build, and a bug found in one is not attributable to the " +
                "same thing (P1 truth)");
            StringAssert.DoesNotContain(ShellStrings.BuildLabel, line);
            StringAssert.DoesNotContain(ShellStrings.DevelopmentLabel, line,
                "and 'dev' is not said there either — Debug.isDebugBuild is true of EVERY editor " +
                "session, so it would be noise on the one line that exists to say something");
        }

        [Test]
        public void ADevelopmentBuild_SaysSo()
        {
            string line = ShellStrings.BuildStamp("0.1.0", BuildInfo.ShortBuildId(Guid), development: true);

            StringAssert.Contains(ShellStrings.DevelopmentLabel, line,
                "stack traces, the profiler and the deep-profile costs all differ — 'is it slow?' has a " +
                "different answer on a development build");
        }

        [Test]
        public void AMissingVersion_DegradesToTheBuild_RatherThanAStrayPrefix()
        {
            string line = ShellStrings.BuildStamp(string.Empty, BuildInfo.ShortBuildId(Guid), false);

            Assert.AreEqual(ShellStrings.BuildLabel + "4f9c2a1", line);
        }

        // ---- the id itself -------------------------------------------------------------------

        [Test]
        public void TheBuildId_IsShortEnoughToReadAndLongEnoughToBeOne()
        {
            Assert.AreEqual("4f9c2a1", BuildInfo.ShortBuildId(Guid));
            Assert.AreEqual(BuildInfo.BuildIdLength, BuildInfo.ShortBuildId(Guid).Length);
        }

        [Test]
        public void TheBuildId_HandlesTheEmptyAndTheShort_WithoutThrowing()
        {
            Assert.AreEqual(string.Empty, BuildInfo.ShortBuildId(null));
            Assert.AreEqual(string.Empty, BuildInfo.ShortBuildId(string.Empty));
            Assert.AreEqual("abc", BuildInfo.ShortBuildId("abc"), "a short guid is used whole, not padded");
        }

        [Test]
        public void TheLiveLine_IsAlwaysSomething_SoTheCornerIsNeverBlank()
        {
            Assert.IsNotEmpty(BuildInfo.StampLine);
        }

        // ---- the promise the wash makes ------------------------------------------------------

        [Test]
        public void TheTitleWash_NeverHidesTheHarbour()
        {
            // "The picture is the game" (§7.8): the world renders behind the title, so the page is a
            // composed shot of it, not a curtain over it. Dense where the type is, thin where it is not.
            Assert.Greater(PaperUi.WashNear.a, 0.75f,
                "the type edge is solid enough for chalk to read against");
            Assert.Less(PaperUi.WashFar.a, 0.5f,
                "and the far edge stays see-through — an opaque page would be a loading screen with a " +
                "menu on it, and we would have spent the one picture we already have");
        }
    }
}
