using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Turning a <see cref="CharacterSkinDef"/> into one posed mesh</b> — the engine-light half of
    /// the mesh-character presenter (ADR 0044 d). Frame choice, the poisoned-frame fence and the CPU
    /// skin all live here, as static functions over arrays, so they are proven headlessly in EditMode
    /// and the MonoBehaviour above them only has to own buffers.
    ///
    /// <para><b>⚠ A CLIP IS DISCRETE SAMPLES, NOT A CURVE.</b> Rig 7 exports one pose per frame, and
    /// adjacent frames step by up to 178.3° on a single bone. Nothing here slerps, lerps or eases
    /// between two frames, and nothing may be added that does: interpolating a 178° step takes the
    /// figure through the inside of her own body. <b>Sample and hold.</b> The clip's frame rate is
    /// the animation, exactly as the flipbook's would have been.</para>
    ///
    /// <para><b>Why the frame comes from the clock and not from a timer.</b> Rule 5 — the same
    /// <c>(worldSeed, gameTime)</c> must give the same picture, on a reload, on a second machine, and
    /// in a determinism test. A per-instance accumulator would drift with frame rate and would not
    /// survive a save; <see cref="FrameFor"/> is a pure function of the clock.</para>
    /// </summary>
    public static class CharacterSkinPose
    {
        /// <summary>
        /// <b>The fence bound, in metres from the rig origin.</b> A frame in which any bone lands
        /// further than this from the figure's own origin is not a pose, it is a solver blow-up, and
        /// it is held out of the animation by <see cref="BuildHonestFrameMap"/>.
        ///
        /// <para><b>This is deliberately NOT a <c>GameConfig</c> knob</b> — rule 6 governs balance and
        /// feel, not data integrity. <b>The bound is measured, not guessed.</b> Across the fisher
        /// preset's 35 clips, the 31 that are not mounts hold every one of the 45 bones within
        /// <b>1.19 m</b> of the figure's origin (worst: <c>astrideStand</c>'s pelvis, 1.1866 m). The
        /// four <c>mount*</c> clips throw <c>ankle_R</c> and <c>ankle_R_tip</c> — the boot shaft, in
        /// the rig 7 header's own words — out to <b>23.7–356.7 m</b> for four or five consecutive
        /// mid-clip frames, while no other bone on those frames leaves 1.71 m: 2 of the 45 carry the
        /// blow-up, and <c>ankle_R_cuff</c> keys a zero local and is simply carried out with them.</para>
        ///
        /// <para>Eight metres sits in the empty gap between those two populations — 1.37× past the
        /// worst pose a mount clip ramps through (5.85 m, <c>mountCab</c> f11) and 2.96× below the
        /// mildest spike (23.71 m, <c>mountDown</c> f5). No value in that gap is one anybody would
        /// want to tune, and exposing one would invite somebody to widen it until the spike came
        /// back.</para>
        ///
        /// <para><b>In shipped play this fence never fires, and that is the point.</b>
        /// <see cref="CharacterSkinStateMap"/> can only ever name four clips — <c>idle</c>,
        /// <c>walk</c>, <c>run</c>, <c>balance</c> — and all four sit in the 1.19 m population, so no
        /// mount clip is reachable from this presenter at all. The fence is defence in depth for the
        /// PR that adds a mount transition, which reaches the poisoned frames on its first frame of
        /// work.</para>
        /// </summary>
        public const float FenceMetres = 8f;

        /// <summary>The frame rate used when a clip declares none — never 0, or the clip freezes.</summary>
        public const float FallbackFramesPerSecond = 12f;

        // ---------------------------------------------------------------- frame choice

        /// <summary>
        /// The frame of <paramref name="clip"/> to draw at <paramref name="gameTimeSeconds"/>. Pure:
        /// no state, no allocation, no <c>UnityEngine.Random</c>, and no <c>string.GetHashCode</c>
        /// (which is salted per process on .NET Core and would make two runs of the same save draw
        /// two different pictures).
        ///
        /// <para>A LOOPING clip is phased by <paramref name="worldSeed"/> and the clip's own key, so
        /// two figures idling side by side in one world are not in lockstep, while the same world
        /// replays identically. A ONE-SHOT clip (<c>Settle</c>: the mounts, <c>reach</c>) runs from
        /// <paramref name="clipStartSeconds"/> and HOLDS its last frame — it is a transition, and a
        /// transition that wrapped would twitch.</para>
        /// </summary>
        public static int FrameFor(in CharacterSkinDef.SkinClip clip, int worldSeed,
                                   double gameTimeSeconds, double clipStartSeconds)
        {
            int n = clip.FrameCount;
            if (n <= 1) return 0;

            float fps = clip.FramesPerSecond > 0f ? clip.FramesPerSecond : FallbackFramesPerSecond;
            double elapsed = gameTimeSeconds - clipStartSeconds;
            if (elapsed < 0d) elapsed = 0d;

            // floor, not round: frame k is shown for the whole of its own interval, which is what
            // "sample and hold" means and what puts the step on the clip's own beat.
            double ticks = Math.Floor(elapsed * fps);
            if (!clip.Loop)
                return ticks >= n - 1 ? n - 1 : (int)ticks;

            // A long session must not lose the phase to double precision, so the wrap is done in
            // integers the moment the tick count is known.
            long t = ticks > long.MaxValue / 2 ? long.MaxValue / 2 : (long)ticks;
            long f = (t + PhaseFrame(worldSeed, clip.StateKey, n)) % n;
            return (int)f;
        }

        /// <summary>A stable 0..<paramref name="frameCount"/>-1 offset for this seed and clip — FNV-1a
        /// over the key's chars, mixed with the seed. Deterministic across processes and platforms,
        /// which is the whole point.</summary>
        public static int PhaseFrame(int worldSeed, string stateKey, int frameCount)
        {
            if (frameCount <= 1) return 0;
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)worldSeed) * 16777619u;
                if (stateKey != null)
                    for (int i = 0; i < stateKey.Length; i++)
                        h = (h ^ stateKey[i]) * 16777619u;
                return (int)(h % (uint)frameCount);
            }
        }

        // ---------------------------------------------------------------- the fence

        /// <summary>
        /// <b>The poisoned-frame fence.</b> Returns a map from clip frame to the frame that should
        /// actually be drawn: <c>map[f] == f</c> for an honest frame, and the nearest EARLIER honest
        /// frame for a poisoned one — hold the last true pose rather than skip to a later one,
        /// because holding reads as a beat and skipping reads as a jump cut.
        ///
        /// <para>When a clip opens on a poisoned run there is no earlier frame to hold, so those lead
        /// in from the first honest frame instead.</para>
        ///
        /// <para><paramref name="fencedCount"/> comes back so the caller can say out loud how much of
        /// a clip was fenced. A clip that is entirely poisoned returns an identity map and
        /// <c>fencedCount == FrameCount</c>: there is nothing honest to hold, and quietly drawing
        /// frame 0 for a whole clip would hide a broken bake.</para>
        /// </summary>
        public static int[] BuildHonestFrameMap(in CharacterSkinDef.SkinClip clip, int boneCount,
                                                float fenceMetres, out int fencedCount)
        {
            int n = Mathf.Max(0, clip.FrameCount);
            var map = new int[n];
            fencedCount = 0;
            if (n == 0 || boneCount <= 0) return map;

            var honest = new bool[n];
            int honestTotal = 0;
            for (int f = 0; f < n; f++)
            {
                honest[f] = FrameIsHonest(clip, f, boneCount, fenceMetres);
                if (honest[f]) honestTotal++;
            }

            if (honestTotal == 0)
            {
                for (int f = 0; f < n; f++) map[f] = f;
                fencedCount = n;
                return map;
            }

            int firstHonest = 0;
            while (!honest[firstHonest]) firstHonest++;

            int held = firstHonest;
            for (int f = 0; f < n; f++)
            {
                if (honest[f]) { held = f; map[f] = f; }
                else { map[f] = held; fencedCount++; }
            }
            return map;
        }

        /// <summary>
        /// Does every bone of this frame land inside the fence? A local key past the fence is already
        /// past it in the figure — the chain composition can only carry it further out — so the test
        /// is done on the keys themselves and costs no matrix work.
        /// </summary>
        public static bool FrameIsHonest(in CharacterSkinDef.SkinClip clip, int frame, int boneCount,
                                         float fenceMetres)
        {
            if (boneCount <= 0) return true;
            float sq = fenceMetres * fenceMetres;

            for (int b = 0; b < boneCount; b++)
            {
                CharacterSkinDef.BoneKey k = clip.KeyOf(frame, b, boneCount);
                if (!IsFinite(k.Position)) return false;
                if (k.Position.sqrMagnitude > sq) return false;
            }
            return true;
        }

        private static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        // ---------------------------------------------------------------- the skin

        /// <summary>
        /// Compose one frame's bone locals into figure-space matrices and then into skinning matrices.
        /// <c>world[b] = parent &lt; 0 ? local : world[parent] * local</c>, then
        /// <c>skin[b] = world[b] * bindpose[b]</c> — the standard two-step, done on the CPU.
        ///
        /// <para><b>Why the CPU and not a SkinnedMeshRenderer.</b> #830 measured it: the facet
        /// renderer list DOES admit a <c>SkinnedMeshRenderer</c> and draws the right silhouette, but
        /// writes DIFFERENT facet values through it, so the ink comes out wrong. That is ADR 0044
        /// §3.7's option (b). If it ever changes, #830's
        /// <c>TheFacetListDrawsASkinnedRenderer_ButNotWithTheSameFacetValues</c> is the test that will
        /// say so, in its own assertion message.</para>
        /// </summary>
        public static void ComposeSkinMatrices(in CharacterSkinDef.SkinClip clip, int frame,
                                               CharacterSkinDef.Bone[] bones, Matrix4x4[] bindposes,
                                               Matrix4x4[] world, Matrix4x4[] skin)
        {
            int n = bones.Length;
            for (int b = 0; b < n; b++)
            {
                CharacterSkinDef.BoneKey k = clip.KeyOf(frame, b, n);
                var local = Matrix4x4.TRS(k.Position, k.Rotation, Vector3.one);
                int p = bones[b].Parent;
                // A parent always precedes its child in the rig's bone order. The >= b guard is not
                // defensive tidiness: a def that broke that order would otherwise read a matrix this
                // pass has not written yet, and the figure would fold up in a way no test names.
                world[b] = p < 0 || p >= b ? local : world[p] * local;
                skin[b] = world[b] * bindposes[b];
            }
        }

        /// <summary>
        /// Skin the bind mesh through <paramref name="skin"/> into the output arrays — two influences
        /// per vertex (<see cref="CharacterSkinDef.MaxBoneInfluences"/>), position and normal.
        ///
        /// <para>The normal is transformed by the same matrix and re-normalised rather than by the
        /// inverse-transpose, which is correct here and cheaper: every bone matrix is a rigid TRS with
        /// unit scale, so it cannot shear the normal. If a scaled bone ever appears in the rig this is
        /// the line that has to change — and because the facet pass inks from the normal, it will go
        /// wrong in an obvious way rather than a subtle one.</para>
        /// </summary>
        public static void Skin(Matrix4x4[] skin, BoneWeight[] weights,
                                Vector3[] srcVerts, Vector3[] srcNorms,
                                Vector3[] outVerts, Vector3[] outNorms)
        {
            for (int v = 0; v < srcVerts.Length; v++)
            {
                BoneWeight w = weights[v];
                Matrix4x4 m0 = skin[w.boneIndex0];
                Vector3 pos = m0.MultiplyPoint3x4(srcVerts[v]) * w.weight0;
                Vector3 nrm = m0.MultiplyVector(srcNorms[v]) * w.weight0;
                if (w.weight1 != 0f)
                {
                    Matrix4x4 m1 = skin[w.boneIndex1];
                    pos += m1.MultiplyPoint3x4(srcVerts[v]) * w.weight1;
                    nrm += m1.MultiplyVector(srcNorms[v]) * w.weight1;
                }
                outVerts[v] = pos;
                outNorms[v] = nrm.sqrMagnitude > 1e-12f ? nrm.normalized : Vector3.up;
            }
        }
    }
}
