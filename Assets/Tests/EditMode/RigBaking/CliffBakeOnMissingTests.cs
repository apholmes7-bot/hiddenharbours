using System;
using System.IO;
using System.Linq;
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
        /// <para>It runs in whichever look the shipped material is in: v10 bakes v10, and once the owner
        /// has banked px it bakes px — then the one CI test that runs a real px bake end to end. Both
        /// region wall builders wire the colour slot by the same look
        /// (<see cref="BothRegionWallBuildersAskTheCatalogForTheColourChannel"/>).</para>
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

        /// <summary>
        /// The colour slot holds one file per look — <c>_unlit</c> in v10, the px bake's <c>_index</c>
        /// (moved into the slot, GUID and all) — and it is the first of each look's three channels.
        /// </summary>
        [Test]
        public void TheColourChannelIsIndexInPxAndUnlitInV10()
        {
            Assert.AreEqual("_unlit", CliffCatalog.ColourChannel(false), "the v10 colour slot is not _unlit");
            Assert.AreEqual("_index", CliffCatalog.ColourChannel(true), "the px colour slot is not _index");
            Assert.AreEqual(CliffCatalog.UnlitChannel, CliffCatalog.ColourChannel(false));
            Assert.AreEqual(CliffCatalog.IndexChannel, CliffCatalog.ColourChannel(true));
            Assert.AreEqual(CliffCatalog.LiveChannels[0], CliffCatalog.ColourChannel(false),
                "the v10 bake's colour channel is not the one the helper names");
            Assert.AreEqual(CliffCatalog.PxLiveChannels[0], CliffCatalog.ColourChannel(true),
                "the px bake's colour channel is not the one the helper names");
        }

        /// <summary>
        /// ⚠ <b>A SOURCE guard, because CI cannot see the wiring any other way.</b> The bake root is
        /// gitignored, so on CI a wall build finds no faces and the colour slot is null in either look —
        /// a behavioural test cannot tell a builder that asks the look from one that spells
        /// <c>_unlit</c>, and a builder that spells it wires nothing in px (every wall uncoloured, with a
        /// warning per chunk). So each builder must name neither colour file and ask the catalog, once.
        /// The real wiring is the plates' (<c>CliffPxLookPlatePlayTests</c>, on a GPU).
        /// </summary>
        [Test]
        public void BothRegionWallBuildersAskTheCatalogForTheColourChannel()
        {
            foreach (string builder in new[]
                     {
                         "Assets/_Project/Code/App/Editor/NineMileCreekCliffWalls.cs",
                         "Assets/_Project/Code/App/Editor/StPetersCliffWalls.cs",
                     })
            {
                string path = Path.Combine(RigCatalog.RepoRoot, builder);
                Assert.IsTrue(File.Exists(path), $"{builder} is not where this guard reads it");
                string source = File.ReadAllText(path);

                foreach (string literal in new[] { "\"_unlit\"", "\"_index\"" })
                    StringAssert.DoesNotContain(literal, source,
                        $"{builder} spells the colour slot's file ({literal}) — in the other look that " +
                        "file is not on disk and every wall builds uncoloured; ask " +
                        "CliffCatalog.ColourChannel(CliffBakeMenu.IsPxLook)");

                const string ask = "CliffCatalog.ColourChannel(";
                int asks = source.Split(new[] { ask }, StringSplitOptions.None).Length - 1;
                Assert.AreEqual(1, asks,
                    $"{builder} should ask the catalog for the colour channel exactly once per build");
                StringAssert.Contains("CliffCatalog.ColourChannel(CliffBakeMenu.IsPxLook)", source,
                    $"{builder} asks for the colour channel by something other than the material's look");
            }
        }

        /// <summary>
        /// <see cref="CliffBakeMenu.EnsureBaked"/> bakes in the MATERIAL'S look, and only what is missing
        /// there. Run through the pure planner with an injected "is this face present" — no bake runs and
        /// nothing is written, so a dev box's own faces are never touched.
        /// </summary>
        [Test]
        public void EnsureBakedPlansOnlyTheRocksMissingInTheMaterialsLook()
        {
            string sandstone = CliffBaker.DefaultRock, till = CliffBaker.OverburdenRock;
            string other = CliffCatalog.Rocks.First(r => !CliffBaker.DefaultRocks.Contains(r));
            CollectionAssert.AreEqual(new[] { sandstone, till }, CliffBaker.DefaultRocks,
                "the wall's rocks changed — re-read this table");

            string[] Plan(bool px, string rock, params string[] present) =>
                CliffBakeMenu.RocksEnsureBakedWouldBake(px, rock, p => present.Contains(p));
            string V10(string rock) => CliffBakeMenu.SentinelFacePathFor(rock, false);
            string Px(string rock) => CliffBakeMenu.SentinelFacePathFor(rock, true);

            CollectionAssert.AreEqual(new[] { sandstone, till }, Plan(false, null),
                "v10, no faces: bake both rocks");
            CollectionAssert.AreEqual(new[] { till }, Plan(false, null, V10(sandstone)),
                "v10, sandstone only: bake the till it lacks, not the sandstone it has");
            CollectionAssert.IsEmpty(Plan(false, null, V10(sandstone), V10(till)),
                "v10, both baked: nothing to do");
            CollectionAssert.AreEqual(new[] { sandstone, till }, Plan(true, null, V10(sandstone), V10(till)),
                "px, both baked in v10 only: bake both in px (the bake moves each _unlit to _index)");
            CollectionAssert.IsEmpty(Plan(true, null, Px(sandstone), Px(till)),
                "px, both baked in px: nothing to do");
            CollectionAssert.AreEqual(new[] { till }, Plan(true, till),
                "px, no faces, till named: bake the named rock alone");
            CollectionAssert.IsEmpty(Plan(true, other, Px(sandstone), Px(till)),
                $"px, the wall's rocks baked, {other} named: nothing — a named rock is baked only when " +
                "the wall itself is missing a face");
        }

        /// <summary>
        /// ⭐ THE LOOK SELF-HEAL'S RULE (owner, 09-24: "the narrow self-heal"). The material is tracked and
        /// the faces are not, so a pulled look change can leave them disagreeing — v10 colour read as px
        /// indices draws most of every wall near-black. The heal acts ONLY on that MISMATCH (a wall rock
        /// missing in the material's look and on disk in the other one) and never on an ABSENCE, which
        /// is every fresh clone and every CI run; in batchmode it only warns.
        /// </summary>
        [Test]
        public void TheLookSelfHealActsOnAMismatchAndNeverOnAnAbsence()
        {
            string sandstone = CliffBaker.DefaultRock, till = CliffBaker.OverburdenRock;
            string V10(string rock) => CliffBakeMenu.SentinelFacePathFor(rock, false);
            string Px(string rock) => CliffBakeMenu.SentinelFacePathFor(rock, true);
            const CliffBakeMenu.LookHeal none = CliffBakeMenu.LookHeal.None;
            const CliffBakeMenu.LookHeal bake = CliffBakeMenu.LookHeal.Bake;
            const CliffBakeMenu.LookHeal warn = CliffBakeMenu.LookHeal.Warn;

            void Expect(string row, bool px, CliffBakeMenu.LookHeal interactive, CliffBakeMenu.LookHeal batch,
                        params string[] onDisk)
            {
                Assert.AreEqual(interactive, CliffBakeMenu.HealFor(px, false, p => onDisk.Contains(p)),
                    $"in the editor: {row}");
                Assert.AreEqual(batch, CliffBakeMenu.HealFor(px, true, p => onDisk.Contains(p)),
                    $"in batchmode: {row}");
            }

            Expect("v10 look, no faces — an absence (a fresh clone, CI)", false, none, none);
            Expect("px look, no faces — an absence (a fresh clone, CI)", true, none, none);
            Expect("px look, px faces", true, none, none, Px(sandstone), Px(till));
            Expect("px look, v10 faces — the pulled flip", true, bake, warn, V10(sandstone), V10(till));
            Expect("v10 look, px faces — the pulled revert", false, bake, warn, Px(sandstone), Px(till));
            Expect("px look, sandstone px, till still v10", true, bake, warn, Px(sandstone), V10(till));
            Expect("px look, sandstone px, till in neither — an absence", true, none, none, Px(sandstone));
            Expect("v10 look, v10 faces", false, none, none, V10(sandstone), V10(till));
        }

        /// <summary>
        /// The look is the shipped material's keyword, and the sentinel follows it: <c>_index</c> in px,
        /// <c>_unlit</c> in v10. Flips the keyword IN MEMORY on the loaded asset, both ways, and in a
        /// <c>finally</c> puts it back and clears the dirty flag it raised — nothing here saves, bakes or
        /// imports, so nothing is left for a later save to write either.
        /// </summary>
        [Test]
        public void TheLookFollowsTheMaterialsKeyword_AndTheSentinelFollowsTheLook()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CliffCatalog.MaterialPath);
            Assert.IsNotNull(mat, $"no wall material at {CliffCatalog.MaterialPath} — the look lives on it");
            bool shippedPx = mat.IsKeywordEnabled(CliffCatalog.PxKeyword);
            bool wasDirty = EditorUtility.IsDirty(mat);
            try
            {
                foreach (bool px in new[] { true, false })
                {
                    if (px) mat.EnableKeyword(CliffCatalog.PxKeyword);
                    else mat.DisableKeyword(CliffCatalog.PxKeyword);

                    string look = px ? "px" : "v10";
                    Assert.AreEqual(px, CliffBakeMenu.IsPxLook,
                        $"the look does not follow the material's keyword ({look})");
                    Assert.AreEqual(CliffBakeMenu.SentinelFacePathFor(CliffBaker.DefaultRock, px),
                                    CliffBakeMenu.SentinelFacePath,
                        $"in the {look} look the sentinel is not the slot the {look} bake writes");
                    StringAssert.EndsWith($"{CliffCatalog.ColourChannel(px)}.png", CliffBakeMenu.SentinelFacePath,
                        $"in the {look} look the sentinel is not the {CliffCatalog.ColourChannel(px)} file");
                }
            }
            finally
            {
                if (shippedPx) mat.EnableKeyword(CliffCatalog.PxKeyword);
                else mat.DisableKeyword(CliffCatalog.PxKeyword);
                if (!wasDirty) EditorUtility.ClearDirty(mat);
            }
            Assert.AreEqual(shippedPx, CliffBakeMenu.IsPxLook, "the shipped look was not put back");
        }
    }
}
