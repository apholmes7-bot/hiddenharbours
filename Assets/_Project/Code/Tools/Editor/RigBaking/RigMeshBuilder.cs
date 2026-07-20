using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>A built mesh plus the numbers the ADR's cost table is made of.</summary>
    public sealed class RigMeshBuild
    {
        public Mesh Mesh;
        public int Faces, Vertices, Triangles, Materials;
        /// <summary>Vertex + index buffer bytes: pos(12) + normal(12) + uv0(16) per vertex,
        /// plus 4 bytes per index. The comparison ADR 0022 makes is against RGBA32 sheet bytes.</summary>
        public long BufferBytes;

        public override string ToString() =>
            $"{Faces} faces → {Triangles} tris / {Vertices} verts, {Materials} materials, " +
            $"{BufferBytes / 1024.0:F1} KB";
    }

    /// <summary>
    /// Turns an extracted <see cref="RigMeshData"/> into a <see cref="Mesh"/> shaped the way the
    /// facet shader wants it (ADR 0022 phase 3, art-pipeline's lane — this only produces the
    /// buffer it will read).
    ///
    /// <para><b>Flat normals, and why they are exact rather than an approximation.</b> The rig
    /// shades a whole polygon from ONE normal taken off its first three vertices
    /// (<c>normal(rv[0],rv[1],rv[2])</c>), then fan-triangulates. So storing that single normal on
    /// every vertex of the face is not "flat shading as an approximation of the rig" — it is
    /// literally what the rig does, including for any non-planar polygon the rigs' <c>box()</c> and
    /// <c>tube()</c> helpers happen to emit.</para>
    ///
    /// <para><b>Why the normal may be computed in object space.</b> The rig computes it AFTER
    /// rotation. <c>projVert</c> is a composition of proper rotations (roll, pitch, heading), so
    /// <c>(Ru)×(Rv) = R(u×v)</c> and the object-space normal transformed by the object matrix is
    /// the same vector. That equivalence is what lets the whole heading/rock motion stay a
    /// transform — the load-bearing fact of ADR 0022.</para>
    ///
    /// <para><b>The one lossy step in the pipeline.</b> The rig is JavaScript and works in doubles;
    /// a Unity vertex buffer is float32. This is where that quantisation happens, deliberately, in
    /// one place, so the golden master can measure its cost separately from extraction error.</para>
    /// </summary>
    public static class RigMeshBuilder
    {
        /// <summary>UV0 channel carrying the per-face constants the shader needs:
        /// <c>x = material id, y = face bias b, z = depth bias db, w = 0</c>. Flat across the face.</summary>
        public const int AttrUvChannel = 0;

        public static RigMeshBuild Build(RigMeshData data, string meshName = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            int vcount = data.VertexCount;
            var verts = new Vector3[vcount];
            var norms = new Vector3[vcount];
            var attrs = new Vector4[vcount];
            var tris = new List<int>(data.TriangleCount * 3);

            int v = 0;
            foreach (var f in data.Faces)
            {
                Vector3 n = ObjectNormal(f.V[0], f.V[1], f.V[2]).ToVector3();
                var attr = new Vector4(f.Mat, (float)f.B, (float)f.Db, 0f);

                int baseIndex = v;
                for (int k = 0; k < f.V.Length; k++, v++)
                {
                    verts[v] = f.V[k].ToVector3();
                    norms[v] = n;
                    attrs[v] = attr;
                }

                // Fan, exactly as _paint does: for(t=1; t+1<rv.length; t++) fillTri(rv[0],rv[t],rv[t+1]).
                for (int t = 1; t + 1 < f.V.Length; t++)
                {
                    tris.Add(baseIndex);
                    tris.Add(baseIndex + t);
                    tris.Add(baseIndex + t + 1);
                }
            }

            var mesh = new Mesh { name = meshName ?? $"{data.RigKey}Hull" };
            // 1,384–1,616 tris is far under 65k, but a rig with finer NSEG should not silently
            // wrap the index buffer.
            mesh.indexFormat = vcount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.SetUVs(AttrUvChannel, attrs);
            mesh.SetTriangles(tris, 0, calculateBounds: true);

            return new RigMeshBuild
            {
                Mesh = mesh,
                Faces = data.Faces.Count,
                Vertices = vcount,
                Triangles = tris.Count / 3,
                Materials = data.Materials.Count,
                BufferBytes = (long)vcount * (12 + 12 + 16) + (long)tris.Count * 4,
            };
        }

        /// <summary>
        /// The rig's <c>normal(a,b,c)</c>: <c>(b−a) × (c−a)</c>, normalised, with the rig's own
        /// degenerate guard (<c>|n| || 1</c>) so a zero-area face produces the same zero vector the
        /// rig produces rather than a NaN.
        /// </summary>
        public static Vector3d ObjectNormal(in Vector3d a, in Vector3d b, in Vector3d c)
        {
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
            double vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            double nx = uy * vz - uz * vy;
            double ny = uz * vx - ux * vz;
            double nz = ux * vy - uy * vx;
            double m = Hypot3(nx, ny, nz);
            if (m == 0.0) m = 1.0;   // the rig's `Math.hypot(...) || 1`
            return new Vector3d(nx / m, ny / m, nz / m);
        }

        /// <summary>
        /// JavaScript's <c>Math.hypot</c>, not <c>sqrt(x²+y²+z²)</c>.
        ///
        /// <para>⚠️ They are not the same number. <c>hypot</c> divides through by the largest
        /// magnitude before squaring — that is what makes it overflow-safe — and the extra
        /// multiply/divide rounds differently in the last ULP. The rig normalises every face normal
        /// with <c>Math.hypot</c>, so using <c>sqrt</c> here perturbs the normal by an ULP, which
        /// scales by GAIN into a shade index and occasionally lands the other side of an ordered-
        /// dither threshold. MEASURED cost of getting this wrong: 1 px on the lobster boat, 3 on the
        /// side dragger, 1 on the punt — small enough to shrug at, and the difference between a
        /// golden master that is exact and one that needs a tolerance nobody can justify.</para>
        /// </summary>
        public static double Hypot3(double x, double y, double z)
        {
            x = Math.Abs(x); y = Math.Abs(y); z = Math.Abs(z);
            double max = Math.Max(x, Math.Max(y, z));
            if (max == 0.0) return 0.0;
            if (double.IsInfinity(max)) return double.PositiveInfinity;
            x /= max; y /= max; z /= max;
            return max * Math.Sqrt(x * x + y * y + z * z);
        }
    }
}
