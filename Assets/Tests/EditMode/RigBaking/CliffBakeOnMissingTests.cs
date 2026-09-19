using System;
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
            string FaceIn(string channel) =>
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Face)}/" +
                $"{CliffCatalog.FaceName(CliffBaker.DefaultRock, "S", 0, CliffCatalog.BaseStep, channel)}.png";

            // One colour slot per look (owner, 09-18: "bake switch"): the v10 bake writes _unlit, and the
            // px bake MOVES that file to _index, GUID and all, before it writes the index into it.
            Assert.AreEqual(FaceIn("_unlit"), CliffBakeMenu.SentinelFacePathFor(CliffBaker.DefaultRock, false),
                "the v10 sentinel does not name a file the v10 bake produces");
            Assert.AreEqual(FaceIn("_index"), CliffBakeMenu.SentinelFacePathFor(CliffBaker.DefaultRock, true),
                "the px sentinel does not name a file the px bake produces");
            Assert.AreEqual(CliffBakeMenu.SentinelFacePathFor(CliffBaker.DefaultRock, CliffBakeMenu.IsPxLook),
                            CliffBakeMenu.SentinelFacePath,
                "the bake-on-missing sentinel does not name a file the default bake produces in the look " +
                "the shipped material is in");

            // …and the pieces it is built from must be the ones the default bake actually uses.
            CollectionAssert.Contains(CliffCatalog.Rocks, CliffBaker.DefaultRock);
            CollectionAssert.Contains(CliffCatalog.Aspects, "S");
            CollectionAssert.Contains(CliffBaker.DefaultBatters, 0);
            CollectionAssert.Contains(CliffCatalog.LiveChannels, "_unlit");
            CollectionAssert.Contains(CliffCatalog.PxLiveChannels, "_index");
            Assert.AreEqual("", CliffCatalog.BatterSuffixes[0],
                "batter 0 is the vertical wall and carries no suffix — if that changes the sentinel " +
                "names a file that is never written");
        }

        /// <summary>
        /// ⭐ THE SELF-HEAL ITSELF. Runs the builder's own entry point against whatever state this
        /// checkout is in and asserts the outcome either way: if the faces were missing it must have
        /// baked them, and if they were already present it must NOT have re-baked (a 30 s bake on every
        /// build, overwriting a rock the owner may have chosen from the alternate menu items).
        ///
        /// <para>⚠ The self-heal is the v10 look's. In the px look a region builder cannot wire the
        /// faces yet (it loads each wall's colour by its v10 name until the band's field is renamed, a
        /// later PR), so <c>EnsureBaked</c> refuses instead — pinned by the next test in either shipped
        /// look. Once the owner has banked px, this one reports INCONCLUSIVE with that reason: its
        /// premise, a shipped look that heals, no longer holds.</para>
        /// </summary>
        [Test]
        public void EnsureBakedLeavesTheCheckoutTextured_AndDoesNotReBakeWhenItAlreadyIs()
        {
            Assume.That(!CliffBakeMenu.IsPxLook,
                "the shipped cliff material is in the px look, where EnsureBaked refuses by design until the " +
                "band's colour field is renamed; InThePxLookEnsureBakedRefuses_AndNamesTheWayBack pins that");

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

        /// <summary>
        /// ⚠ <b>In the px look the self-heal REFUSES — loudly, and with the way back.</b> Both region
        /// builders wire each wall's colour by its v10 name (<c>_unlit</c>), and the px bake has moved
        /// that file to <c>_index</c>, so a build in the px look would stand the coast up with no colour
        /// on any wall and nothing saying why. The refusal is the loud version, and its message names the
        /// two menu items the way back runs through.
        ///
        /// <para>The look is the shipped material's keyword, so this turns it on IN MEMORY on the loaded
        /// asset when the material ships in v10, and in a <c>finally</c> turns it back off and clears the
        /// dirty flag it raised. <c>EnsureBaked</c> refuses before it bakes or saves anything, so nothing
        /// here writes the asset, and nothing is left for a later save to write either.</para>
        /// </summary>
        [Test]
        public void InThePxLookEnsureBakedRefuses_AndNamesTheWayBack()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CliffCatalog.MaterialPath);
            Assert.IsNotNull(mat, $"no wall material at {CliffCatalog.MaterialPath} — the look lives on it");
            bool shippedPx = mat.IsKeywordEnabled(CliffCatalog.PxKeyword);
            bool wasDirty = EditorUtility.IsDirty(mat);
            try
            {
                mat.EnableKeyword(CliffCatalog.PxKeyword);
                Assert.IsTrue(CliffBakeMenu.IsPxLook, "the look does not follow the material's keyword");
                Assert.AreEqual(CliffBakeMenu.SentinelFacePathFor(CliffBaker.DefaultRock, true),
                                CliffBakeMenu.SentinelFacePath,
                    "in the px look the sentinel is the _index slot the px bake writes");

                var refused = Assert.Throws<InvalidOperationException>(() => CliffBakeMenu.EnsureBaked(),
                    "a region build in the px look would wire no colour to any wall");
                foreach (string item in new[] { CliffBakeMenu.KitV10Item, CliffBakeMenu.KitPxItem })
                    StringAssert.Contains(item.Replace("/", " ▸ "), refused.Message,
                        "the refusal must name the menu items the way back runs through");
                Assert.Throws<InvalidOperationException>(() => CliffBakeMenu.EnsureBaked("till"),
                    "a named rock is refused as well");
            }
            finally
            {
                if (!shippedPx) mat.DisableKeyword(CliffCatalog.PxKeyword);
                if (!wasDirty) EditorUtility.ClearDirty(mat);
            }
            Assert.AreEqual(shippedPx, CliffBakeMenu.IsPxLook, "the shipped look was not put back");
        }
    }
}
