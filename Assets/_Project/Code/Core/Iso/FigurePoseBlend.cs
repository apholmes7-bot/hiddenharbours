using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Blend two composed figure poses</b> (figure-space bone matrices, as
    /// <see cref="CharacterSkinPose.ComposeWorld"/> and <see cref="RowingPoseSolver.Solve"/> leave them)
    /// by blending each bone's LOCAL transform against its parent: positions lerped, rotations slerped,
    /// then composed down the chain again. Blending locals rather than the composed matrices keeps every
    /// bone at its own length mid-blend, so a limb never shrinks or stretches on the way from standing to
    /// seated. Rigid poses in, a rigid pose out; no allocation (the caller owns every array); PURE.
    /// </summary>
    public static class FigurePoseBlend
    {
        /// <summary>
        /// <paramref name="result"/> = blend of <paramref name="from"/> (weight 0) and <paramref name="to"/>
        /// (weight 1) at <paramref name="weight"/>, for <paramref name="bones"/>' hierarchy (a parent precedes
        /// its child). <paramref name="result"/> must be a third array: it is written as it is read.
        /// </summary>
        public static void BlendLocal(Matrix4x4[] from, Matrix4x4[] to, CharacterSkinDef.Bone[] bones,
                                      float weight, Matrix4x4[] result)
        {
            if (from == null || to == null || bones == null || result == null) return;
            int n = Mathf.Min(bones.Length, Mathf.Min(from.Length, Mathf.Min(to.Length, result.Length)));
            float w = Mathf.Clamp01(weight);
            for (int b = 0; b < n; b++)
            {
                int p = bones[b].Parent;
                bool hasParent = p >= 0 && p < b;
                Matrix4x4 fromLocal = hasParent ? from[p].inverse * from[b] : from[b];
                Matrix4x4 toLocal = hasParent ? to[p].inverse * to[b] : to[b];
                Vector3 pos = Vector3.Lerp(Position(fromLocal), Position(toLocal), w);
                Quaternion rot = Quaternion.Slerp(fromLocal.rotation, toLocal.rotation, w);
                Matrix4x4 local = Matrix4x4.TRS(pos, rot, Vector3.one);
                result[b] = hasParent ? result[p] * local : local;
            }
        }

        private static Vector3 Position(in Matrix4x4 m) => new Vector3(m.m03, m.m13, m.m23);
    }
}
