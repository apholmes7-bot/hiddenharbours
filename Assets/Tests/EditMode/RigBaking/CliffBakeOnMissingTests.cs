using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐ THE FRESH-CLONE SELF-HEAL — the condition the rig-not-sheets trade was accepted on.
    ///
    /// <para>PR #427 shipped the cliff kit as its rig rather than as ~90 MB of regenerable PNGs, and the
    /// coordinator made one consequence binding: <i>"PR 2's builder must bake-on-missing so a fresh clone
    /// self-heals — a checkout that renders pink until someone finds a menu item is not shipped."</i></para>
    ///
    /// <para><b>Why this is a TEST and not a line in a PR body.</b> The bake root is gitignored, so on CI
    /// — and in any fresh clone — it genuinely does not exist. That makes this the one place where the
    /// self-heal can be exercised for real instead of reasoned about: the suite finds no baked faces,
    /// calls the same entry point the region builder calls, and then asserts the faces are there. A
    /// claim in prose would have been indistinguishable from a claim about code that was never run.</para>
    /// </summary>
    public class CliffBakeOnMissingTests
    {
        /// <summary>
        /// The sentinel has to name a file the default bake ACTUALLY writes. Get this wrong in the
        /// forgiving direction and <see cref="CliffBakeMenu.EnsureBaked"/> re-bakes on every single
        /// region build (~30 s each, silently); get it wrong the other way and it never fires at all and
        /// the fresh clone stays untextured. Neither shows up as an error anywhere.
        /// </summary>
        [Test]
        public void TheSentinelNamesAFaceTheDefaultBakeWrites()
        {
            string expected =
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Face)}/" +
                $"{CliffCatalog.FaceName(CliffBaker.DefaultRock, "S", 0, CliffCatalog.BaseStep, "_unlit")}.png";
            Assert.AreEqual(expected, CliffBakeMenu.SentinelFacePath,
                "the bake-on-missing sentinel does not name a file the default bake produces");

            // …and the pieces it is built from must be the ones the default bake actually uses.
            CollectionAssert.Contains(CliffCatalog.Rocks, CliffBaker.DefaultRock);
            CollectionAssert.Contains(CliffCatalog.Aspects, "S");
            CollectionAssert.Contains(CliffBaker.DefaultBatters, 0);
            CollectionAssert.Contains(CliffCatalog.LiveChannels, "_unlit");
            Assert.AreEqual("", CliffCatalog.BatterSuffixes[0],
                "batter 0 is the vertical wall and carries no suffix — if that changes the sentinel " +
                "names a file that is never written");
        }

        /// <summary>
        /// ⭐ THE SELF-HEAL ITSELF. Runs the builder's own entry point against whatever state this
        /// checkout is in and asserts the outcome either way: if the faces were missing it must have
        /// baked them, and if they were already present it must NOT have re-baked (a 30 s bake on every
        /// build, overwriting a rock the owner may have chosen from the alternate menu items).
        /// </summary>
        [Test]
        public void EnsureBakedLeavesTheCheckoutTextured_AndDoesNotReBakeWhenItAlreadyIs()
        {
            bool wasBaked = CliffBakeMenu.IsBaked;

            bool baked = CliffBakeMenu.EnsureBaked();
            AssetDatabase.Refresh();

            Assert.AreEqual(!wasBaked, baked,
                wasBaked
                    ? "EnsureBaked re-baked a checkout that already carried faces — that is ~30 s on " +
                      "every region build and it silently reverts a non-default rock"
                    : "EnsureBaked did nothing on a checkout with no faces — the fresh clone stays " +
                      "untextured, which is exactly what the binding condition forbids");

            Assert.IsTrue(CliffBakeMenu.IsBaked,
                $"no cliff face at {CliffBakeMenu.SentinelFacePath} after EnsureBaked — a fresh clone " +
                "of this repository renders its coast untextured");

            var face = AssetDatabase.LoadAssetAtPath<Texture2D>(CliffBakeMenu.SentinelFacePath);
            Assert.IsNotNull(face, "the sentinel path does not load as a texture");
            Assert.AreEqual(CliffCatalog.FaceWidth, face.width,
                "the baked face is not the kit's canvas width — the import contract did not apply");
            Assert.AreEqual(CliffCatalog.FaceHeight, face.height);

            // Idempotent: a second call on a now-baked checkout must be a no-op, or the builder pays the
            // bake twice the first time it runs.
            Assert.IsFalse(CliffBakeMenu.EnsureBaked(),
                "EnsureBaked is not idempotent — it bakes again immediately after baking");
        }
    }
}
