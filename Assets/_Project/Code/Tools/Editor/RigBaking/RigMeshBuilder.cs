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
        /// uv1(8) on a hull that carries level tags or uv1(16) on a rig 9 figure that carries face
        /// attributes, plus uv2(16) on a rig 10 figure that carries mark attributes, plus 4 bytes per
        /// index. The comparison ADR 0022 makes is against RGBA32 sheet bytes.</summary>
        public long BufferBytes;

        /// <summary>How many faces a face group owns — 0 on every mesh that does not carry rig 9's
        /// face attributes. Reported so a bake log SAYS the face channel was written.</summary>
        public int GroupedFaces;

        /// <summary>How many faces carry a real level tag — 0 on every rig that publishes no
        /// <c>geometry()</c>. Reported so a bake log SAYS whether the cutaway channel was written,
        /// rather than leaving it to be inferred from a byte count.</summary>
        public int TaggedFaces;

        public override string ToString() =>
            $"{Faces} faces → {Triangles} tris / {Vertices} verts, {Materials} materials, " +
            $"{BufferBytes / 1024.0:F1} KB" +
            (TaggedFaces > 0 ? $", {TaggedFaces} level-tagged" : "") +
            (GroupedFaces > 0 ? $", {GroupedFaces} in face groups" : "");
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
        /// UV1 on a rig 9 FIGURE: the face attributes, a Vector4 flat across the face —
        /// <c>x = face group</c> (1 + its index in the rig's <c>GROUP_ORDER</c>; 0 on a face no group
        /// owns), <c>y = role</c> (<see cref="HiddenHarbours.Core.CharacterSkinDef.FaceRole"/>: which of
        /// the def's thresholds the face culls at), <c>z = 1</c> on a face the head snap moves,
        /// <c>w = 0</c>. The facet shader's figure variant reads it to draw one group per slot and
        /// cull each face as the rig culls it, with no second draw call.
        ///
        /// <para>The same channel as <see cref="LevelUvChannel"/>, and never both on one mesh: a hull
        /// is never a figure, and <c>HH_LEVEL_GATE</c> and <c>HH_FIGURE</c> are one keyword set. Written
        /// only when <see cref="RigMeshData.CarriesFaceAttributes"/>, so every hull, fitting and rig 7
        /// figure keeps exactly the bytes it had.</para>
        /// </summary>
        public const int FaceUvChannel = 1;

        /// <summary>
        /// UV2 on a rig 10 FIGURE: the mark attributes, a Vector4 flat across the face —
        /// <c>xyz = the face's smooth normal</c> (<see cref="RigFace.SmoothNormal"/>, bind frame; 0 on a
        /// face with none) and <c>w = its mark flags</c> (<see cref="MarkFlags"/>), with UV1.w the turn
        /// band of a mark (<see cref="RigFace.MarkAz"/>; 0 on every other face). The facet shader's
        /// figure variant reads UV1.w to cull a mark by its band, and lights a face by its smooth normal,
        /// which the figure renderer poses on every re-skin (<c>IsoCharacterFigureRenderer</c>, since the
        /// rig 10 intake's Phase B). The flags are for the bake's own port of the rig's paint
        /// (<see cref="CharacterSkinInk9"/>), which poses the mesh as the engine does.
        ///
        /// <para>The same channel as <see cref="TexUvChannel"/>, and never both on one mesh: a room
        /// rides a hull, never a figure. Written only when <see cref="RigMeshData.CarriesMarkAttributes"/>,
        /// so every hull, fitting and rig 9 figure keeps exactly the bytes it had.</para>
        /// </summary>
        public const int MarkUvChannel = 2;

        /// <summary>The bits of <see cref="MarkFlags"/>: a point mark; a mark that also draws over the
        /// hair; a face a mark may draw over; a hair face; a face with a smooth normal.</summary>
        public const int MarkBit = 1, OverHairBit = 2, UnderMarkBit = 4, HairBit = 8, SmoothBit = 16;

        /// <summary>A face's mark flags, UV2.w on a rig 10 figure (<see cref="MarkBit"/> and its fellows).</summary>
        public static int MarkFlags(RigFace f) =>
            (f.Mark ? MarkBit : 0) | (f.OverHair ? OverHairBit : 0) | (f.UnderMark ? UnderMarkBit : 0) |
            (f.Hair ? HairBit : 0) | (f.SmoothNormal.HasValue ? SmoothBit : 0);

        /// <summary>A face's UV1.w on a rig 10 figure: a mark's turn band, or 0 (no band).</summary>
        public static float MarkBand(RigFace f) => f.Mark && !double.IsNaN(f.MarkAz) ? (float)f.MarkAz : 0f;

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
            bool faced = data.CarriesFaceAttributes;
            if (faced && tagged)
                throw new InvalidOperationException(
                    $"{data.RigKey} carries both level tags and rig 9 face attributes. Both live in " +
                    "TexCoord1 and the shader reads one or the other by keyword; a mesh with both would " +
                    "have one of them silently read as the other.");
            var faceAttrs = faced ? new Vector4[vcount] : null;
            bool marked = data.CarriesMarkAttributes;
            if (marked && !faced)
                throw new InvalidOperationException(
                    $"{data.RigKey} carries rig 10 mark attributes without the face attributes they extend " +
                    "(a mark's band rides UV1.w).");
            if (marked && textured)
                throw new InvalidOperationException(
                    $"{data.RigKey} carries both interior geometry and rig 10 mark attributes. Both live in " +
                    "TexCoord2; a figure never carries a room.");
            var markAttrs = marked ? new Vector4[vcount] : null;
            int taggedFaces = 0, groupedFaces = 0;
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
                var faceAttr = faced ? new Vector4(f.FaceGroup, f.FaceRole, f.Head ? 1f : 0f,
                                                   marked ? MarkBand(f) : 0f) : default;
                Vector4 markAttr = default;
                if (marked)
                {
                    // UV1.w reads 0 as "no band", and the shader culls a mark by its band only above 0.
                    // A band at or below 0 would ride as none, or skip the cull the rig makes.
                    if (f.Mark && !double.IsNaN(f.MarkAz) && !(f.MarkAz > 0))
                        throw new InvalidOperationException(
                            $"{data.RigKey} face {faceIndex - 1} is a mark with the turn band {f.MarkAz}. UV1.w " +
                            "carries a band above 0 (rig 10's are cosines of 21 and 24 degrees) and reads 0 as none.");
                    Vector3 sn = f.SmoothNormal.HasValue ? f.SmoothNormal.Value.ToVector3() : Vector3.zero;
                    markAttr = new Vector4(sn.x, sn.y, sn.z, MarkFlags(f));
                }
                if (faced && f.FaceGroup > 0) groupedFaces++;

                int baseIndex = v;
                for (int k = 0; k < f.V.Length; k++, v++)
                {
                    verts[v] = f.V[k].ToVector3();
                    norms[v] = n;
                    attrs[v] = attr;
                    if (tagged) levels[v] = levelTag;
                    if (faced) faceAttrs[v] = faceAttr;
                    if (marked) markAttrs[v] = markAttr;
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
            if (faced) mesh.SetUVs(FaceUvChannel, faceAttrs);
            if (marked) mesh.SetUVs(MarkUvChannel, markAttrs);
            mesh.SetTriangles(tris, 0, calculateBounds: true);

            return new RigMeshBuild
            {
                Mesh = mesh,
                Faces = data.Faces.Count,
                Vertices = vcount,
                Triangles = tris.Count / 3,
                Materials = data.Materials.Count,
                TaggedFaces = taggedFaces,
                GroupedFaces = groupedFaces,
                BufferBytes = (long)vcount * (12 + 12 + 16 + (tagged ? 8 : 0) + (faced ? 16 : 0) +
                                              (marked ? 16 : 0)) +
                              (long)tris.Count * 4,
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
