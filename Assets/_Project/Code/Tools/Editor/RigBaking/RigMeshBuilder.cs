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
        /// <summary>Vertex + index buffer bytes: pos(12) + normal(12) + uv0(16) per vertex, plus
        /// uv1(8) on a hull that carries level tags, plus 4 bytes per index. The comparison ADR 0022
        /// makes is against RGBA32 sheet bytes.</summary>
        public long BufferBytes;

        /// <summary>How many faces carry a real level tag — 0 on every rig that publishes no
        /// <c>geometry()</c>. Reported so a bake log SAYS whether the cutaway channel was written,
        /// rather than leaving it to be inferred from a byte count.</summary>
        public int TaggedFaces;

        public override string ToString() =>
            $"{Faces} faces → {Triangles} tris / {Vertices} verts, {Materials} materials, " +
            $"{BufferBytes / 1024.0:F1} KB" +
            (TaggedFaces > 0 ? $", {TaggedFaces} level-tagged" : "");
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
        /// <c>x = material id, y = face bias b, z = depth bias db, w = interior SIDE CODE</c>. Flat
        /// across the face. <c>w</c> is the ADR 0023 per-face interior mask, PER SIDE
        /// (<see cref="RigMeshInteriorClassifier.ClassifySides"/>): 0 = exterior both sides (and the
        /// value every mesh baked before the mask existed carries, so an un-rebaked hull renders
        /// exactly as before), 1 = interior both sides, 2 = interior when the camera renders the
        /// FRONT (the side the face normal points toward), 3 = interior when it renders the BACK.
        /// The guard pass decodes the rendered side from the stored normal, so the code is
        /// meaningful whichever way mirroring left the winding.</summary>
        public const int AttrUvChannel = 0;

        /// <summary>
        /// UV1 channel carrying the CUTAWAY tag: <c>x = level id, y = 1 on emitted INTERIOR
        /// geometry and 0 on the hull's own faces</c>. Flat across the face, like UV0.
        ///
        /// <para><b>Written only when the rig published a level vocabulary</b>
        /// (<see cref="RigMeshData.CarriesLevelTags"/>). Every hull baked before the cutaway kit, and
        /// every fitting, gets no channel at all — so their meshes are byte-for-byte what they were,
        /// their golden masters do not move, and <c>Mesh.HasVertexAttribute(TexCoord1)</c> is an
        /// honest answer to "can this hull be cut?" rather than a field of zeros that reads as
        /// "everything is hull".</para>
        ///
        /// <para><b>y is 0 on every face this builder writes today.</b> The hull is the only half of
        /// the spike's HYBRID that exists: shell geometry (the room) is a later lane, and when it
        /// arrives it writes 1 here so ONE fragment compare does both halves of the swap — cull the
        /// house you are inside of, draw the room. Reserving the component now costs nothing and
        /// keeps the shader's decode from having to change under a shipped bake.</para>
        /// </summary>
        public const int LevelUvChannel = 1;

        /// <summary>UV2: the ROOM's procedural surface — <c>xy = generator id + period</c> (flat per
        /// face), <c>zw = the rig's own per-vertex uv</c>. Written only on a hull that carries
        /// interior geometry, so no mesh baked before full-mesh interiors gains a channel and no
        /// golden master moves.</summary>
        public const int TexUvChannel = 2;

        /// <summary>
        /// Build the mesh. <paramref name="interior"/> is the side-blind per-FACE interior mask, in
        /// <c>data.Faces</c> order — kept for callers that predate the per-side codes; true maps to
        /// <see cref="RigMeshInteriorClassifier.SideInterior"/>.
        ///
        /// <para>⚠️ It defaults to <c>null</c> deliberately, and must stay that way: fittings are
        /// built through this same method, and every prop mesh must remain EXTERIOR. An outboard's
        /// leg and propeller have to stay wettable — flagging a cowl top interior would mean a
        /// green sea could never swallow the engine. A non-null default would also silently change
        /// every fitting mesh and force an unnecessary prop re-bake.</para>
        /// </summary>
        public static RigMeshBuild Build(RigMeshData data, string meshName = null,
                                         bool[] interior = null)
        {
            byte[] sides = null;
            if (interior != null)
            {
                sides = new byte[interior.Length];
                for (int i = 0; i < interior.Length; i++)
                    sides[i] = interior[i] ? RigMeshInteriorClassifier.SideInterior
                                           : RigMeshInteriorClassifier.SideExterior;
            }
            return Build(data, meshName, sides);
        }

        /// <summary>
        /// Build the mesh. <paramref name="interiorSides"/> is the per-face SIDE CODE array
        /// (<see cref="RigMeshInteriorClassifier.ClassifySides"/>), in <c>data.Faces</c> order;
        /// null (every fitting) bakes 0 = exterior everywhere — see the overload's warning.
        /// </summary>
        public static RigMeshBuild Build(RigMeshData data, string meshName, byte[] interiorSides)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            int vcount = data.VertexCount;
            var verts = new Vector3[vcount];
            var norms = new Vector3[vcount];
            var attrs = new Vector4[vcount];
            bool tagged = data.CarriesLevelTags;
            var levels = tagged ? new Vector2[vcount] : null;
            // THE ROOM'S PROCEDURAL SURFACE, in its own channel so TexCoord1 does not have to widen
            // and no existing hull's vertex layout moves. xy are flat per face (generator + period),
            // zw are the rig's own per-VERTEX uv, which paint() interpolates before calling the
            // generator — so they must interpolate here too.
            bool textured = data.CarriesInteriorGeometry;
            var texAttrs = textured ? new Vector4[vcount] : null;
            int taggedFaces = 0;
            var tris = new List<int>(data.TriangleCount * 3);

            int v = 0;
            int faceIndex = 0;
            foreach (var f in data.Faces)
            {
                Vector3 n = ObjectNormal(f.V[0], f.V[1], f.V[2]).ToVector3();
                float sideCode = interiorSides != null && faceIndex < interiorSides.Length
                    ? interiorSides[faceIndex] : 0f;
                faceIndex++;
                var attr = new Vector4(f.Mat, (float)f.B, (float)f.Db, sideCode);

                // ⚠️ A tagged rig cannot have an untagged face — extraction refuses one — so this is
                // the invariant restated at the only other place that could break it, not a fallback.
                // Writing 0 here for a face whose tag went missing would mean 'hull' = never culled.
                if (tagged && f.Level < 0)
                    throw new InvalidOperationException(
                        $"{data.RigKey} publishes a level vocabulary but face {faceIndex - 1} carries " +
                        $"no tag ({nameof(RigFace.Level)} = {f.Level}). A mesh must not be built from a " +
                        "half-tagged face list: the missing tag would bake as level 0 = hull = never " +
                        "cull, and the room would stop opening in exactly one wall.");
                // y is the INTERIOR flag (ADR 0038, full mesh). 0 on the hull's own faces, so
                // every mesh baked before rooms existed keeps the exact bytes it had.
                var levelTag = tagged ? new Vector2(f.Level, f.Interior ? 1f : 0f) : default;
                if (tagged) taggedFaces++;

                int baseIndex = v;
                for (int k = 0; k < f.V.Length; k++, v++)
                {
                    verts[v] = f.V[k].ToVector3();
                    norms[v] = n;
                    attrs[v] = attr;
                    if (tagged) levels[v] = levelTag;
                    if (textured)
                    {
                        Vector2 uv = f.Uv != null && k < f.Uv.Length ? f.Uv[k] : Vector2.zero;
                        texAttrs[v] = new Vector4(f.TexKind, (float)f.TexPeriod, uv.x, uv.y);
                    }
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
            if (tagged) mesh.SetUVs(LevelUvChannel, levels);
            if (textured) mesh.SetUVs(TexUvChannel, texAttrs);
            mesh.SetTriangles(tris, 0, calculateBounds: true);

            return new RigMeshBuild
            {
                Mesh = mesh,
                Faces = data.Faces.Count,
                Vertices = vcount,
                Triangles = tris.Count / 3,
                Materials = data.Materials.Count,
                TaggedFaces = taggedFaces,
                BufferBytes = (long)vcount * (12 + 12 + 16 + (tagged ? 8 : 0)) + (long)tris.Count * 4,
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
