using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The rower's own torso, as a volume his arms must stay out of.</b> Measured once per skin from
    /// its bind mesh: for each of the trunk's three bones (pelvis, spine, chest), the vertices that bone
    /// mostly carries, in the bone's own frame, cut into <see cref="Slices"/> horizontal slices, each
    /// wrapped in an ellipse (its half-width and half-depth as the radii). Slices follow the body's own
    /// taper: the chest's top slice takes the shoulders' width, its lower ones only the ribs', so an
    /// elbow tucked against the ribs is not read as inside the shoulders. The pelvis is cut at the hip
    /// joints, so a skirt or an apron hanging over the thighs does not count as trunk.
    ///
    /// <para>Read through the posed bone matrices, so the volume leans, slumps and turns with him.
    /// <see cref="Outside"/> is a signed clearance: positive outside the trunk, negative inside. Built
    /// once, then read with no allocation; a skin without a readable bind mesh or a trunk bone has no
    /// volume (<see cref="IsUsable"/> false) and the solve skips the check.</para>
    /// </summary>
    public sealed class RowingBodyVolume
    {
        /// <summary>The chest bone's id.</summary>
        public const string ChestId = "chest";

        /// <summary>The trunk bones, bottom to top.</summary>
        public static readonly string[] TrunkBoneIds = { RowingPoseSolver.PelvisId, RowingPoseSolver.SpineId, ChestId };

        /// <summary>A vertex counts toward a bone when that bone carries at least this share of it.</summary>
        public const float DominantWeight = 0.6f;

        /// <summary>Horizontal slices per trunk bone.</summary>
        public const int Slices = 4;

        private const int Bones = 3;

        private readonly int[] _bone = new int[Bones];
        private readonly bool[] _has = new bool[Bones];
        private readonly float[] _zLo = new float[Bones], _zHi = new float[Bones];
        // Per bone, per slice: the ellipse's centre (x, y) and radii (x, y).
        private readonly Vector2[] _sliceCentre = new Vector2[Bones * Slices];
        private readonly Vector2[] _sliceRadius = new Vector2[Bones * Slices];

        /// <summary>True when at least one trunk bone carries a volume.</summary>
        public bool IsUsable { get; }

        /// <summary>The bounds of trunk bone <paramref name="k"/>'s volume (0 pelvis, 1 spine, 2 chest),
        /// in that bone's frame: centre and half-extents over all its slices. False when it has none.</summary>
        public bool TryGetBox(int k, out Vector3 centre, out Vector3 extent)
        {
            centre = extent = default;
            if (!_has[k]) return false;
            Vector2 lo = new Vector2(float.PositiveInfinity, float.PositiveInfinity), hi = -lo;
            for (int s = 0; s < Slices; s++)
            {
                Vector2 c = _sliceCentre[k * Slices + s], r = _sliceRadius[k * Slices + s];
                lo = Vector2.Min(lo, c - r);
                hi = Vector2.Max(hi, c + r);
            }
            centre = new Vector3(0.5f * (lo.x + hi.x), 0.5f * (lo.y + hi.y), 0.5f * (_zLo[k] + _zHi[k]));
            extent = new Vector3(0.5f * (hi.x - lo.x), 0.5f * (hi.y - lo.y), 0.5f * (_zHi[k] - _zLo[k]));
            return true;
        }

        /// <summary>Measure <paramref name="skin"/>'s trunk from its bind mesh.</summary>
        public static RowingBodyVolume Of(CharacterSkinDef skin)
        {
            if (skin == null || skin.BindMesh == null || skin.Bones == null) return null;
            Mesh mesh = skin.BindMesh;
            if (!mesh.isReadable) return null;
            return new RowingBodyVolume(skin.Bones, mesh.vertices, mesh.boneWeights, mesh.bindposes);
        }

        /// <summary>Measure a trunk from raw bind data (the bind mesh's vertices, weights and bind
        /// poses, parallel to <paramref name="bones"/>).</summary>
        public RowingBodyVolume(CharacterSkinDef.Bone[] bones, Vector3[] vertices, BoneWeight[] weights, Matrix4x4[] bindposes)
        {
            int hipL = Find(bones, RowingPoseSolver.HipLId);
            bool data = vertices != null && weights != null && bindposes != null && weights.Length == vertices.Length;
            for (int k = 0; k < Bones; k++)
            {
                int b = Find(bones, TrunkBoneIds[k]);
                _bone[k] = b;
                if (!data || b < 0 || b >= bindposes.Length) continue;

                // The pelvis's floor: the hip joints, in the pelvis's own frame.
                float floorZ = float.NegativeInfinity;
                if (k == 0 && hipL >= 0 && hipL < bindposes.Length)
                    floorZ = bindposes[b].MultiplyPoint3x4(bindposes[hipL].inverse.MultiplyPoint3x4(Vector3.zero)).z;

                float zLo = float.PositiveInfinity, zHi = float.NegativeInfinity;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (Share(weights[i], b) < DominantWeight) continue;
                    float z = bindposes[b].MultiplyPoint3x4(vertices[i]).z;
                    if (z < floorZ) continue;
                    zLo = Mathf.Min(zLo, z);
                    zHi = Mathf.Max(zHi, z);
                }
                if (!(zHi > zLo)) continue;
                _zLo[k] = zLo;
                _zHi[k] = zHi;

                var lo = new Vector2[Slices];
                var hi = new Vector2[Slices];
                var any = new bool[Slices];
                for (int s = 0; s < Slices; s++)
                {
                    lo[s] = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                    hi[s] = -lo[s];
                }
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (Share(weights[i], b) < DominantWeight) continue;
                    Vector3 l = bindposes[b].MultiplyPoint3x4(vertices[i]);
                    if (l.z < floorZ) continue;
                    int s = SliceOf(l.z, zLo, zHi);
                    lo[s] = Vector2.Min(lo[s], new Vector2(l.x, l.y));
                    hi[s] = Vector2.Max(hi[s], new Vector2(l.x, l.y));
                    any[s] = true;
                }
                // An empty slice borrows the nearest filled one below it, else above.
                for (int s = 0; s < Slices; s++)
                {
                    if (any[s]) continue;
                    for (int d = 1; d < Slices && !any[s]; d++)
                    {
                        int dn = s - d, up = s + d;
                        if (dn >= 0 && any[dn]) { lo[s] = lo[dn]; hi[s] = hi[dn]; any[s] = true; }
                        else if (up < Slices && any[up]) { lo[s] = lo[up]; hi[s] = hi[up]; any[s] = true; }
                    }
                }
                bool ok = true;
                for (int s = 0; s < Slices; s++)
                {
                    _sliceCentre[k * Slices + s] = 0.5f * (lo[s] + hi[s]);
                    _sliceRadius[k * Slices + s] = 0.5f * (hi[s] - lo[s]);
                    ok &= any[s] && _sliceRadius[k * Slices + s].x > 0f && _sliceRadius[k * Slices + s].y > 0f;
                }
                _has[k] = ok;
            }
            IsUsable = _has[0] || _has[1] || _has[2];
        }

        /// <summary>
        /// The signed clearance of <paramref name="point"/> (figure frame) from the trunk posed by
        /// <paramref name="world"/>: its distance outside the nearest of the three volumes, negative by
        /// how deep it is inside. Approximate across the ellipse (scaled by its smaller radius), exact
        /// along the height. No allocation.
        /// </summary>
        public float Outside(Matrix4x4[] world, Vector3 point)
        {
            float best = float.PositiveInfinity;
            for (int k = 0; k < Bones; k++)
            {
                if (!_has[k]) continue;
                Vector3 l = world[_bone[k]].inverse.MultiplyPoint3x4(point);
                int s = SliceOf(l.z, _zLo[k], _zHi[k]);
                Vector2 c = _sliceCentre[k * Slices + s], r = _sliceRadius[k * Slices + s];
                float dx = (l.x - c.x) / r.x, dy = (l.y - c.y) / r.y;
                float radial = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(r.x, r.y);
                float vertical = Mathf.Max(_zLo[k] - l.z, l.z - _zHi[k]);
                float d = radial > 0f && vertical > 0f
                    ? Mathf.Sqrt(radial * radial + vertical * vertical)
                    : Mathf.Max(radial, vertical);
                if (d < best) best = d;
            }
            return best;
        }

        private static int SliceOf(float z, float zLo, float zHi)
        {
            int s = Mathf.FloorToInt((z - zLo) / (zHi - zLo) * Slices);
            return s < 0 ? 0 : s >= Slices ? Slices - 1 : s;
        }

        private static float Share(in BoneWeight w, int bone)
        {
            float s = 0f;
            if (w.boneIndex0 == bone) s += w.weight0;
            if (w.boneIndex1 == bone) s += w.weight1;
            if (w.boneIndex2 == bone) s += w.weight2;
            if (w.boneIndex3 == bone) s += w.weight3;
            return s;
        }

        private static int Find(CharacterSkinDef.Bone[] bones, string id)
        {
            if (bones == null) return -1;
            for (int b = 0; b < bones.Length; b++)
                if (string.Equals(bones[b].Id, id, System.StringComparison.Ordinal)) return b;
            return -1;
        }
    }
}
