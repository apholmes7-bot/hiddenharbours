using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE CLIP IS A LADDER OF SAMPLES, NOT A CURVE</b> — the arithmetic the mesh figure stands on,
    /// tested without a scene, a GPU or a baked asset.
    ///
    /// <para>Rig 7 exports one POSE PER FRAME and nothing between them: adjacent frames of the same
    /// clip differ by up to 178.3° on a single bone, so anything that interpolated would swing a limb
    /// through the body. Every test here is written to fail if someone "improves" the stepping into a
    /// blend, and to fail if the phase stops being a pure function of <c>(worldSeed, gameTime)</c>
    /// (CLAUDE.md rule 5).</para>
    ///
    /// <para><b>The fence is the other half.</b> The four <c>mount*</c> clips carry a mid-clip boot
    /// spike that throws bones 100–356 m from the rig origin. A figure drawn through those frames is
    /// a flash of geometry across the whole harbour. The fence holds the previous honest pose instead
    /// — but a clip that is poisoned END TO END must NOT come back looking fenced-and-fine, because
    /// that would turn a broken bake into a silent still frame. <c>fencedCount</c> is the number that
    /// keeps that honest, so it is asserted, not just read.</para>
    /// </summary>
    public sealed class CharacterSkinPoseTests
    {
        // ------------------------------------------------------------------ synthetic clips

        private static CharacterSkinDef.SkinClip Clip(string anim, int frames, int bones, float fps,
                                                      bool loop,
                                                      System.Func<int, int, Vector3> position = null)
        {
            var keys = new CharacterSkinDef.BoneKey[frames * bones];
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < bones; b++)
                    keys[f * bones + b] = new CharacterSkinDef.BoneKey
                    {
                        Position = position != null ? position(f, b) : Vector3.zero,
                        Rotation = Quaternion.identity,
                    };

            return new CharacterSkinDef.SkinClip
            {
                Anim = anim,
                State = anim,
                FramesPerSecond = fps,
                Loop = loop,
                FrameCount = frames,
                Keys = keys,
            };
        }

        // ------------------------------------------------------------------ rule 5: the frame

        [Test]
        public void TheFrameIsAPureFunctionOfTheSeedAndTheClock()
        {
            CharacterSkinDef.SkinClip clip = Clip("idle", 24, 3, 12f, loop: true);

            for (int i = 0; i < 40; i++)
            {
                double t = i * 0.137d;
                int a = CharacterSkinPose.FrameFor(clip, 1337, t, 0d);
                int b = CharacterSkinPose.FrameFor(clip, 1337, t, 0d);
                Assert.AreEqual(a, b,
                    "the same (worldSeed, gameTime) gave two different frames at t=" + t +
                    " — the figure has picked up a source of state outside the pair rule 5 allows");
            }
        }

        [Test]
        public void TheSeedPhasesTheLoopSoTwoFiguresAreNotInLockstep()
        {
            CharacterSkinDef.SkinClip clip = Clip("idle", 24, 3, 12f, loop: true);

            int a = CharacterSkinPose.FrameFor(clip, 1337, 0d, 0d);
            int b = CharacterSkinPose.FrameFor(clip, 1338, 0d, 0d);

            Assert.AreNotEqual(a, b,
                "two worlds one seed apart opened the same idle on the same frame — the loop is not " +
                "phased, so every figure in a scene would breathe in unison");
        }

        [Test]
        public void ALoopWrapsOnItsOwnBeatRatherThanRunningOffTheEnd()
        {
            const int frames = 24;
            const float fps = 12f;
            CharacterSkinDef.SkinClip clip = Clip("idle", frames, 3, fps, loop: true);

            int atZero = CharacterSkinPose.FrameFor(clip, 1337, 0d, 0d);
            int oneLoopLater = CharacterSkinPose.FrameFor(clip, 1337, frames / fps, 0d);

            Assert.AreEqual(atZero, oneLoopLater, "a full loop did not return to its opening frame");
            for (int i = 0; i < frames * 3; i++)
            {
                int f = CharacterSkinPose.FrameFor(clip, 1337, i / (double)fps, 0d);
                Assert.That(f, Is.InRange(0, frames - 1), "the loop left the clip at tick " + i);
            }
        }

        [Test]
        public void TheFrameStepsAndNeverLandsBetweenTwoSamples()
        {
            // Sample-and-hold: inside one frame's own interval the answer must not move at all.
            // A blend would show up here as a changing value across the interval — which is exactly
            // what would swing a 178° bone through the body.
            CharacterSkinDef.SkinClip clip = Clip("walk", 16, 3, 10f, loop: true);

            // Times are held clear of the tick boundary itself: the subject here is that the answer
            // is CONSTANT across one frame's interval, not where the boundary rounds.
            int inside = CharacterSkinPose.FrameFor(clip, 7, 0.32d, 0d);
            Assert.AreEqual(inside, CharacterSkinPose.FrameFor(clip, 7, 0.35d, 0d));
            Assert.AreEqual(inside, CharacterSkinPose.FrameFor(clip, 7, 0.39d, 0d),
                "the frame moved WITHIN one sample's interval — the clip is being blended, and a " +
                "178 degree bone step would swing a limb through the body");
            Assert.AreNotEqual(inside, CharacterSkinPose.FrameFor(clip, 7, 0.42d, 0d),
                "the clip did not step into the next sample at all");
        }

        [Test]
        public void AOneShotHoldsItsLastFrameInsteadOfWrapping()
        {
            const int frames = 8;
            CharacterSkinDef.SkinClip clip = Clip("mount_dory", frames, 3, 12f, loop: false);

            Assert.AreEqual(0, CharacterSkinPose.FrameFor(clip, 1337, 0d, 0d),
                "a one-shot must open on frame 0 — a phased transition would twitch on entry");
            Assert.AreEqual(frames - 1, CharacterSkinPose.FrameFor(clip, 1337, 10d, 0d));
            Assert.AreEqual(frames - 1, CharacterSkinPose.FrameFor(clip, 1337, 10000d, 0d),
                "a one-shot wrapped: the mount animation would replay forever while she stands still");
        }

        [Test]
        public void AOneShotRunsFromItsOwnStartNotFromTheStartOfTheWorld()
        {
            CharacterSkinDef.SkinClip clip = Clip("mount_dory", 8, 3, 10f, loop: false);

            // She boarded at t = 900 s. Frame 0 is what she must be on, not frame 7.
            Assert.AreEqual(0, CharacterSkinPose.FrameFor(clip, 1337, 900d, 900d));
            Assert.AreEqual(2, CharacterSkinPose.FrameFor(clip, 1337, 900.25d, 900d));
            Assert.AreEqual(0, CharacterSkinPose.FrameFor(clip, 1337, 899d, 900d),
                "a clock behind the clip start must clamp, not run the clip backwards");
        }

        [Test]
        public void PhaseFrameIsPinnedSoTheHashCannotBeSwappedQuietly()
        {
            // FNV-1a over the key's chars, mixed with the seed. These four are GOLDEN: swap the hash
            // for string.GetHashCode() — which is randomised per PROCESS on .NET Core and would make
            // the same world replay differently on every launch — and all four move.
            Assert.AreEqual(14, CharacterSkinPose.PhaseFrame(1337, "idle", 24));
            Assert.AreEqual(11, CharacterSkinPose.PhaseFrame(1338, "idle", 24));
            Assert.AreEqual(5, CharacterSkinPose.PhaseFrame(1337, "walk", 16));
            Assert.AreEqual(13, CharacterSkinPose.PhaseFrame(0, "idle", 24));
            Assert.AreEqual(18, CharacterSkinPose.PhaseFrame(-1, "balance", 30),
                "a negative seed must fold through the unsigned hash, not sign-extend into a " +
                "negative frame index");
        }

        [Test]
        public void PhaseFrameStaysInsideTheClip()
        {
            for (int seed = -50; seed <= 50; seed++)
                foreach (string key in new[] { "idle", "walk", "run", "balance" })
                {
                    int f = CharacterSkinPose.PhaseFrame(seed, key, 7);
                    Assert.That(f, Is.InRange(0, 6), "seed " + seed + " key " + key);
                }

            Assert.AreEqual(0, CharacterSkinPose.PhaseFrame(1337, "idle", 1));
            Assert.AreEqual(0, CharacterSkinPose.PhaseFrame(1337, "idle", 0));
        }

        // ------------------------------------------------------------------ the fence

        private static CharacterSkinDef.SkinClip PoisonedClip(int frames, int bones, int[] poisoned,
                                                              float metres = 120f)
        {
            var bad = new System.Collections.Generic.HashSet<int>(poisoned);
            return Clip("mount_dory", frames, bones, 12f, loop: false,
                        (f, b) => bad.Contains(f) && b == 1
                            ? new Vector3(metres, 0f, 0f)
                            : new Vector3(0.1f * b, 0f, 0.9f));
        }

        [Test]
        public void TheFenceHoldsThePreviousHonestFrameRatherThanSkippingAhead()
        {
            CharacterSkinDef.SkinClip clip = PoisonedClip(8, 3, new[] { 3, 4, 5 });

            int[] map = CharacterSkinPose.BuildHonestFrameMap(
                clip, 3, CharacterSkinPose.FenceMetres, out int fenced);

            Assert.AreEqual(3, fenced, "the boot spike is three frames wide here");
            Assert.AreEqual(new[] { 0, 1, 2, 2, 2, 2, 6, 7 }, map,
                "a fenced frame must hold the last TRUE pose (2) — holding reads as a beat, " +
                "skipping to 6 reads as a jump cut");
        }

        [Test]
        public void AClipThatOpensPoisonedLeadsInFromItsFirstHonestFrame()
        {
            CharacterSkinDef.SkinClip clip = PoisonedClip(6, 3, new[] { 0, 1 });

            int[] map = CharacterSkinPose.BuildHonestFrameMap(
                clip, 3, CharacterSkinPose.FenceMetres, out int fenced);

            Assert.AreEqual(2, fenced);
            Assert.AreEqual(new[] { 2, 2, 2, 3, 4, 5 }, map,
                "there is no earlier honest pose to hold, so the opening must lead in from the " +
                "first one there is");
        }

        [Test]
        public void AWhollyPoisonedClipIsReportedNotHidden()
        {
            CharacterSkinDef.SkinClip clip = PoisonedClip(5, 3, new[] { 0, 1, 2, 3, 4 });

            int[] map = CharacterSkinPose.BuildHonestFrameMap(
                clip, 3, CharacterSkinPose.FenceMetres, out int fenced);

            Assert.AreEqual(5, fenced,
                "a clip with nothing honest in it must say so — silently holding frame 0 for the " +
                "whole clip would make a broken bake look like a working still pose");
            Assert.AreEqual(new[] { 0, 1, 2, 3, 4 }, map,
                "with nothing honest to hold the map is the identity, so the damage stays visible");
        }

        [Test]
        public void ANonFiniteKeyIsFencedLikeAnExcursion()
        {
            CharacterSkinDef.SkinClip clip = Clip("mount_dory", 4, 2, 12f, loop: false,
                (f, b) => f == 2 && b == 1 ? new Vector3(float.NaN, 0f, 0f) : Vector3.zero);

            Assert.IsFalse(CharacterSkinPose.FrameIsHonest(clip, 2, 2, CharacterSkinPose.FenceMetres),
                "a NaN key passes a magnitude test by accident — it must be caught explicitly");
            Assert.IsTrue(CharacterSkinPose.FrameIsHonest(clip, 1, 2, CharacterSkinPose.FenceMetres));
        }

        [Test]
        public void AnHonestClipIsLeftAlone()
        {
            CharacterSkinDef.SkinClip clip = Clip("idle", 6, 3, 12f, loop: true,
                                                  (f, b) => new Vector3(0f, 0f, 0.2f * b));

            int[] map = CharacterSkinPose.BuildHonestFrameMap(
                clip, 3, CharacterSkinPose.FenceMetres, out int fenced);

            Assert.AreEqual(0, fenced, "a clean clip must not be fenced at all");
            Assert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, map);
        }

        // ------------------------------------------------------------------ the skinning maths

        [Test]
        public void ComposeSkinMatricesMatchesAHandComposedChain()
        {
            var bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone },
                new CharacterSkinDef.Bone { Id = "spine", Parent = 0 },
                new CharacterSkinDef.Bone { Id = "head", Parent = 1 },
            };

            var locals = new[]
            {
                Matrix4x4.TRS(new Vector3(0.3f, 0f, 0f), Quaternion.AngleAxis(15f, Vector3.up), Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0f, 0.8f), Quaternion.AngleAxis(-40f, Vector3.right), Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0f, 0.5f), Quaternion.AngleAxis(95f, Vector3.forward), Vector3.one),
            };

            var keys = new CharacterSkinDef.BoneKey[3];
            for (int b = 0; b < 3; b++)
                keys[b] = new CharacterSkinDef.BoneKey
                {
                    Position = locals[b].GetColumn(3),
                    Rotation = locals[b].rotation,
                };

            var clip = new CharacterSkinDef.SkinClip
            {
                Anim = "idle", State = "idle", FramesPerSecond = 12f, FrameCount = 1, Keys = keys,
            };

            var bind = new[]
            {
                Matrix4x4.TRS(new Vector3(-0.3f, 0f, 0f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0f, -0.8f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0.1f, -1.3f), Quaternion.identity, Vector3.one),
            };

            var world = new Matrix4x4[3];
            var skin = new Matrix4x4[3];
            CharacterSkinPose.ComposeSkinMatrices(clip, 0, bones, bind, world, skin);

            Matrix4x4 w0 = locals[0];
            Matrix4x4 w1 = w0 * locals[1];
            Matrix4x4 w2 = w1 * locals[2];

            AssertMatrix(w0, world[0], "world[root]");
            AssertMatrix(w1, world[1], "world[spine]");
            AssertMatrix(w2, world[2], "world[head] — the chain did not carry through two parents");
            AssertMatrix(w2 * bind[2], skin[2], "skin[head] = world * bindpose");
        }

        [Test]
        public void AForwardParentReferenceIsTreatedAsARootRatherThanReadingAnUnwrittenMatrix()
        {
            // IsUsable() rejects such a def, but the composer is also called on hand-built data in
            // tests and tools. Reading world[p] before this pass wrote it folds the figure up in a
            // way no assertion names, so the guard is pinned here.
            var bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "a", Parent = CharacterSkinDef.NoBone },
                new CharacterSkinDef.Bone { Id = "b", Parent = 5 },
            };
            var keys = new[]
            {
                new CharacterSkinDef.BoneKey { Position = Vector3.zero, Rotation = Quaternion.identity },
                new CharacterSkinDef.BoneKey
                {
                    Position = new Vector3(0f, 0f, 1f), Rotation = Quaternion.identity,
                },
            };
            var clip = new CharacterSkinDef.SkinClip
            {
                Anim = "idle", State = "idle", FramesPerSecond = 12f, FrameCount = 1, Keys = keys,
            };

            var world = new Matrix4x4[2];
            var skin = new Matrix4x4[2];
            CharacterSkinPose.ComposeSkinMatrices(clip, 0, bones, new[]
            {
                Matrix4x4.identity, Matrix4x4.identity,
            }, world, skin);

            AssertMatrix(Matrix4x4.TRS(new Vector3(0f, 0f, 1f), Quaternion.identity, Vector3.one),
                         world[1], "a forward parent reference must fall back to the local");
        }

        [Test]
        public void SkinBlendsTwoInfluencesAndLeavesTheNormalUnitLength()
        {
            var skin = new[]
            {
                Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(3f, 0f, 0f), Quaternion.identity, Vector3.one),
            };

            var weights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f, boneIndex1 = 0, weight1 = 0f },
                new BoneWeight { boneIndex0 = 0, weight0 = 0.25f, boneIndex1 = 1, weight1 = 0.75f },
            };

            var srcV = new[] { Vector3.zero, Vector3.zero };
            var srcN = new[] { Vector3.forward, Vector3.forward };
            var outV = new Vector3[2];
            var outN = new Vector3[2];

            CharacterSkinPose.Skin(skin, weights, srcV, srcN, outV, outN);

            Assert.AreEqual(1f, outV[0].x, 1e-5f, "a single full influence must be the bone itself");
            Assert.AreEqual(0.25f * 1f + 0.75f * 3f, outV[1].x, 1e-5f,
                "two influences must blend by weight — a collapse to one is the 452x hem tolerance " +
                "failure ADR 0044 names");
            Assert.AreEqual(1f, outN[1].magnitude, 1e-5f, "the blended normal must be re-normalised");
        }

        [Test]
        public void SkinFallsBackToAnUprightNormalRatherThanEmittingAZeroVector()
        {
            var skin = new[]
            {
                Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(180f, Vector3.up), Vector3.one),
            };
            var weights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 0.5f, boneIndex1 = 1, weight1 = 0.5f },
            };

            var outV = new Vector3[1];
            var outN = new Vector3[1];
            CharacterSkinPose.Skin(skin, weights, new[] { Vector3.zero },
                                   new[] { Vector3.forward }, outV, outN);

            // The two influences cancel exactly. A zero normal reaching the facet pass would ink a
            // hole in the figure; an upright one inks a flat facet, which is wrong but visible.
            Assert.AreEqual(1f, outN[0].magnitude, 1e-5f);
        }

        private static void AssertMatrix(Matrix4x4 expected, Matrix4x4 actual, string what)
        {
            for (int i = 0; i < 16; i++)
                Assert.AreEqual(expected[i], actual[i], 1e-4f, what + " element " + i);
        }
    }
}
