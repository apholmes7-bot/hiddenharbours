using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One bone as the rig declares it, in the rig's own order (parent is an index).</summary>
    public sealed class RigBone
    {
        public string Id;
        public int Parent = -1;
        /// <summary>Rest transform LOCAL to the parent — the rig's <c>skeleton(build)</c>.</summary>
        public Vector3d RestPos;
        public double RestRx, RestRy, RestRz, RestRw;
        /// <summary>Rest transform in the FIGURE frame — the rig's <c>skeletonWorld(build)</c>.
        /// The bindposes are the inverse of these, which is the only place they are used.</summary>
        public Vector3d WorldPos;
        public double WorldRx, WorldRy, WorldRz, WorldRw;
        /// <summary>True when at least one bind-mesh corner is weighted to this bone.</summary>
        public bool OwnsVertex;
    }

    /// <summary>
    /// The per-corner skinning of the bind mesh, in <see cref="RigMeshData.Faces"/> corner order —
    /// the same order <see cref="RigMeshBuilder"/> emits vertices in, which is what lets the weights
    /// be attached to a mesh the existing builder produced rather than to a second one.
    /// </summary>
    public sealed class RigSkinning
    {
        /// <summary>Influences per corner, fixed width, padded with bone −1 / weight 0.</summary>
        public int Width;
        public int CornerCount;
        /// <summary>[corner * Width + i] → bone index, or −1 for padding.</summary>
        public int[] Bone;
        /// <summary>[corner * Width + i] → weight.</summary>
        public double[] Weight;
        /// <summary>The rig's own part name per FACE ('thigh_L', 'inseam', 'head', …).</summary>
        public string[] FacePart;
        /// <summary>The bind-mesh vertex positions rig 7 reports, corner by corner. Kept so the
        /// bake can prove rig 7's bind mesh IS rig 6's idle[0] face list, vertex for vertex, rather
        /// than assuming the two agree because the rig says they do.</summary>
        public Vector3d[] Position;
        /// <summary>The largest influence count any corner carries. MEASURED; see the class remarks
        /// on <see cref="CharacterSkinExtractor"/>.</summary>
        public int MaxInfluences;
    }

    /// <summary>One <c>ANIMS</c> row of bone animation, straight off <c>CharacterIso7.clip</c>.</summary>
    public sealed class RigSkinClip
    {
        public string Anim;
        public int Frames;
        public double Ms;
        public bool Loop, Settle;
        public string Mount = "", Carry = "", Power = "";
        public string[] Parked = Array.Empty<string>();
        /// <summary>Frame-major local transforms: [(frame * bones + bone)] → pos/rot.</summary>
        public Vector3d[] Pos;
        public double[] Rx, Ry, Rz, Rw;
    }

    /// <summary>
    /// Reads <c>characterIsoRig7.js</c> — the SKINNED EXPORT layer — out of the repo's own V8 host:
    /// a skeleton, the same skeleton in the figure frame (whose inverse is the bindposes), the
    /// per-corner bone weights of the bind mesh, and one clip of bone transforms per <c>ANIMS</c>
    /// row. The companion of <see cref="CharacterPoseMeshExtractor"/>, which stays the authority on
    /// GEOMETRY and SHADING; this class adds only what a flipbook does not have.
    ///
    /// <para><b>The bind mesh is not extracted here, and that is the point.</b> Rig 7's bind pose is
    /// <c>pose('idle', 0, build)</c> — literally the pose the flipbook's <c>idle[0]</c> mesh is
    /// baked from. So the bake takes its geometry, materials, Bayer matrix and cell facts from
    /// <see cref="CharacterPoseMeshExtractor.ExtractPose"/> exactly as the flipbook does, and this
    /// class hands back weights in that same corner order. Two consequences worth the arrangement:
    /// the skinned bind mesh is byte-identical to the flipbook's first frame, so a presenter
    /// swapping paths cannot get a different silhouette by accident; and
    /// <see cref="RigSkinning.Position"/> lets the bake PROVE the two agree rather than trust
    /// it — <see cref="AssertBindAgrees"/>.</para>
    ///
    /// <para>⚠️ <b>Load order is load-bearing and its failure is silent.</b>
    /// <see cref="CharacterPoseMeshExtractor.Load"/> must run FIRST: it installs eye → head → body
    /// with the body's single in-memory widening applied. Rig 7 is <c>Object.create(CharacterIso6)</c>
    /// and captures whatever <c>CharacterIso6</c> is at the moment it runs. Install rig 7 first and
    /// its prerequisite installs an UNWIDENED body; a later widened re-execute then replaces the
    /// global and leaves rig 7 prototyping from an orphan — every call still works, and answers from
    /// a rig nobody can see. The catalog skips a prerequisite whose global
    /// already exists, which is what makes the correct order simply work.</para>
    ///
    /// <para>⚠️ <b>The head chain deletes a third of the mesh without erroring.</b> Rig 6 reads
    /// <c>root.HeadIso</c> through <c>const HI = root.HeadIso; if (HI) { … }</c>. With the head rig
    /// absent, BOTH <c>facesOf</c> and <c>bindMesh</c> emit no head geometry — fisher drops 711
    /// faces to 454 — and every A/B comparison stays perfectly clean because both sides are equally
    /// blind. <see cref="AssertKitLoaded"/> asserts the globals AND that the bind mesh actually
    /// carries head-part faces, because a face count on its own cannot see this.</para>
    ///
    /// <para>⚠️ <b>Rotations are quaternions and must stay quaternions.</b> The rig exports
    /// <c>rot</c> as a unit quaternion and <c>rotEuler</c> as a convenience, and says which is the
    /// truth: the tube frames pass through vertical on every walk cycle, where any Euler order has
    /// a pole. Nothing here reads <c>rotEuler</c>.</para>
    ///
    /// <para><b>Frame: rig space, verbatim.</b> Right-handed metres, +x right (curb), +y forward
    /// (nose), +z up, origin at the cell pivot on the ground — the same frame every mesh this repo
    /// bakes already lives in. The rig 7 header's "Unity map: pos (x, z, y), quaternion (−x, −z,
    /// −y, w)" is for a y-up importer and is deliberately NOT applied; see
    /// <see cref="Core.CharacterSkinDef"/>.</para>
    /// </summary>
    public static class CharacterSkinExtractor
    {
        /// <summary>The catalog key for the skinned export — <c>characterIsoRig7.js</c>, global
        /// <c>CharacterIso7</c>, prerequisite <c>character</c>.</summary>
        public const string CatalogKey = "characterSkin";

        public static RigEntry Entry => RigCatalog.Get(CatalogKey);
        public static string ScriptPath => Entry.ScriptPath;
        public static string GlobalName => Entry.GlobalName;

        /// <summary>The rig's own vertex tolerance — <c>CharacterIso7.TOL</c>, 1e-4 m. Read from the
        /// rig rather than spelled here, so a drop that tightens it tightens the guards too.</summary>
        public static double Tolerance(IRigScriptHost host) =>
            host.EvaluateNumber($"{GlobalName}.TOL");

        // ---------------------------------------------------------------------------------------
        // Loading
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Load eye → head → body (widened, via <see cref="CharacterPoseMeshExtractor.Load"/>) and
        /// then the skinned export on top, and assert the whole chain arrived. See the class
        /// remarks: doing this in the other order is a silent poison, not an error.
        /// </summary>
        public static void Load(IRigScriptHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            CharacterPoseMeshExtractor.Load(host);      // eye -> head -> body, body widened in memory
            RigCatalog.InstallModule(host, Entry);      // prerequisite 'character' is already present

            string g = GlobalName;
            string b = CharacterPoseMeshExtractor.GlobalName;

            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException($"{ScriptPath} ran but did not install globalThis.{g}.");

            // The export IS the body, re-expressed. If the prototype link is not intact then rig 7
            // is sitting on a different (older, or unwidened) body than the one the flipbook bakes
            // from, and every vertex below would agree with itself and with nothing else.
            if (!host.EvaluateBool($"Object.getPrototypeOf({g}) === {b}"))
                throw new InvalidOperationException(
                    $"{g} is not Object.create({b}) any more. The skinned export must ride on the " +
                    "SAME body object the pose extractor widened — otherwise the bind mesh and the " +
                    "flipbook are two different characters that happen to share a name.");

            string baseRev = host.EvaluateString($"String({g}.base||'')");
            string bodyRev = host.EvaluateString($"String({b}.revision||'')");
            if (!string.Equals(baseRev, bodyRev, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{g}.base is '{baseRev}' but {b}.revision is '{bodyRev}'. The body was bumped " +
                    "without re-running the export: the skinned mesh will stop agreeing with the " +
                    "sprite silently, one clip at a time. Ask the art director for a rig 7 re-drop; " +
                    "do NOT re-stamp the pin here.");

            foreach (string fn in new[] { "skeleton", "skeletonWorld", "bindMesh", "clip", "clips" })
                if (!host.EvaluateBool($"typeof {g}.{fn} === 'function'"))
                    throw new InvalidOperationException(
                        $"{g} does not export {fn}(). The skinned bake reads the rig's own export " +
                        "surface; without it there is nothing to bake.");
        }

        /// <summary>
        /// THE CONTROL, and it is not optional. Asserts the head kit is actually in the host by the
        /// only statistic that can see its absence: whether the bind mesh carries HEAD-part faces.
        /// A face count cannot see it — with <c>HeadIso</c> missing every consumer loses the same
        /// faces and every comparison stays clean.
        /// </summary>
        public static void AssertKitLoaded(IRigScriptHost host, string preset)
        {
            string g = GlobalName;
            foreach (string dep in new[] { "EyeIso", "HeadIso3" })
                if (!host.EvaluateBool($"typeof {dep} === 'object' && {dep} !== null"))
                    throw new InvalidOperationException(
                        $"globalThis.{dep} is absent. The body reads root.HeadIso through an " +
                        "`if (HI)` and renders WITHOUT A HEAD rather than failing — fisher goes " +
                        "711 faces to 454, and every A/B stays clean because both sides are blind.");

            int headFaces = (int)host.EvaluateNumber(
                $"(function(){{var m={g}.bindMesh({Js(preset)}),n=0;" +
                "for(var i=0;i<m.length;i++)if(m[i].part==='head')n++;return n;})()");
            if (headFaces == 0)
                throw new InvalidOperationException(
                    $"The bind mesh for '{preset}' carries no face tagged part 'head'. The head kit " +
                    "is in the host but produced nothing — do not bake a headless character.");

            int faces = (int)host.EvaluateNumber($"{g}.bindMesh({Js(preset)}).length");
            int poseFaces = (int)host.EvaluateNumber(
                $"(function(){{var C={CharacterPoseMeshExtractor.GlobalName};" +
                $"var b=C.resolveBuild({{build:{{preset:{Js(preset)}}}}});" +
                "return C.facesOf(C.pose('idle',0,b),b).length;})()");
            if (faces != poseFaces)
                throw new InvalidOperationException(
                    $"'{preset}': rig 7's bind mesh has {faces} faces but rig 6's own " +
                    $"pose('idle',0) has {poseFaces}. The export is not re-expressing this build.");
        }

        // ---------------------------------------------------------------------------------------
        // The skeleton
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Read <c>skeleton(build)</c> and <c>skeletonWorld(build)</c> into one table. Parent is an
        /// INDEX (−1 at the root) and the rig declares parents before children, which the bake
        /// relies on to compose world matrices in a single ordered pass.
        /// </summary>
        public static RigBone[] ReadSkeleton(IRigScriptHost host, string preset)
        {
            string g = GlobalName;
            string ids = host.EvaluateString(
                $"{g}.skeleton({Js(preset)}).map(function(b){{return b.id;}}).join(',')");
            string[] idList = ids.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            // [i32 n] then per bone [i32 parent][f64 lx,ly,lz][f64 lrx..lrw][f64 wx,wy,wz][f64 wrx..wrw]
            host.Execute(
                $"globalThis.__hhSkelPack=(function(){{var S={g}.skeleton({Js(preset)})," +
                $"W={g}.skeletonWorld({Js(preset)});" +
                "if(S.length!==W.length)throw new Error('skeleton/skeletonWorld disagree: '+S.length+' vs '+W.length);" +
                "var buf=new ArrayBuffer(4+S.length*(4+7*8+7*8));var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,S.length,true);p+=4;" +
                "for(var i=0;i<S.length;i++){var s=S[i],w=W[i];" +
                "dv.setInt32(p,s.parent,true);p+=4;" +
                "var a=s.rest.pos,q=s.rest.rot;" +
                "dv.setFloat64(p,a[0],true);p+=8;dv.setFloat64(p,a[1],true);p+=8;dv.setFloat64(p,a[2],true);p+=8;" +
                "dv.setFloat64(p,q[0],true);p+=8;dv.setFloat64(p,q[1],true);p+=8;" +
                "dv.setFloat64(p,q[2],true);p+=8;dv.setFloat64(p,q[3],true);p+=8;" +
                "var A=w.pos,Q=w.rot;" +
                "dv.setFloat64(p,A[0],true);p+=8;dv.setFloat64(p,A[1],true);p+=8;dv.setFloat64(p,A[2],true);p+=8;" +
                "dv.setFloat64(p,Q[0],true);p+=8;dv.setFloat64(p,Q[1],true);p+=8;" +
                "dv.setFloat64(p,Q[2],true);p+=8;dv.setFloat64(p,Q[3],true);p+=8;}" +
                "return new Uint8ClampedArray(buf);})();");
            byte[] blob = host.EvaluateBytes("globalThis.__hhSkelPack");

            int off = 0;
            int n = BitConverter.ToInt32(blob, off); off += 4;
            if (n != idList.Length)
                throw new InvalidOperationException(
                    $"skeleton('{preset}') reported {idList.Length} ids and {n} records.");

            var bones = new RigBone[n];
            for (int i = 0; i < n; i++)
            {
                var bone = new RigBone { Id = idList[i] };
                bone.Parent = BitConverter.ToInt32(blob, off); off += 4;
                bone.RestPos = ReadV3(blob, ref off);
                bone.RestRx = D(blob, ref off); bone.RestRy = D(blob, ref off);
                bone.RestRz = D(blob, ref off); bone.RestRw = D(blob, ref off);
                bone.WorldPos = ReadV3(blob, ref off);
                bone.WorldRx = D(blob, ref off); bone.WorldRy = D(blob, ref off);
                bone.WorldRz = D(blob, ref off); bone.WorldRw = D(blob, ref off);

                // Parents before children is the rig's own contract, and the bake composes world
                // matrices in one ordered pass on the strength of it. A forward reference would
                // compose against an identity that has not been filled in yet — wrong, and silent.
                if (bone.Parent >= i || bone.Parent < -1)
                    throw new InvalidOperationException(
                        $"Bone {i} '{bone.Id}' names parent {bone.Parent}, which is not an earlier " +
                        "index. The skinned bake requires parents to precede children.");
                bones[i] = bone;
            }
            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"Skeleton blob was {blob.Length} bytes and {off} consumed — packer/reader disagree.");
            if (bones[0].Parent != -1)
                throw new InvalidOperationException($"Bone 0 '{bones[0].Id}' is not the root.");
            return bones;
        }

        /// <summary>
        /// Compose every bone's LOCAL rest down the hierarchy and require the result to equal the
        /// rig's own <c>skeletonWorld</c> within tolerance.
        ///
        /// <para><b>Why this is not paranoia.</b> The two halves of a skinned mesh come from
        /// different places: Unity builds each frame's bone matrix by composing the LOCAL
        /// transforms down the Transform hierarchy, and multiplies it by
        /// <c>bindposes[i]</c> — which this bake derives from <c>skeletonWorld</c>. If the local
        /// chain does not reproduce the world table, then at the BIND POSE those two disagree and
        /// every vertex is displaced by the difference. The character would be wrong in the rest
        /// pose, before a single clip plays, and no clip guard would explain why: they would all
        /// be wrong by the same amount and stay perfectly consistent with each other.</para>
        /// </summary>
        public static void AssertRestComposes(RigBone[] bones, double tolerance)
        {
            if (bones == null || bones.Length == 0)
                throw new ArgumentException("No bones.", nameof(bones));

            var wp = new Vector3d[bones.Length];
            var wq = new double[bones.Length * 4];
            double worst = 0; int worstAt = -1;

            for (int i = 0; i < bones.Length; i++)
            {
                RigBone b = bones[i];
                double px, py, pz, qx, qy, qz, qw;
                if (b.Parent < 0)
                {
                    px = b.RestPos.X; py = b.RestPos.Y; pz = b.RestPos.Z;
                    qx = b.RestRx; qy = b.RestRy; qz = b.RestRz; qw = b.RestRw;
                }
                else
                {
                    int p = b.Parent;                       // parents precede children — checked in ReadSkeleton
                    double ax = wq[p * 4], ay = wq[p * 4 + 1], az = wq[p * 4 + 2], aw = wq[p * 4 + 3];
                    Rotate(ax, ay, az, aw, b.RestPos.X, b.RestPos.Y, b.RestPos.Z,
                           out double rx, out double ry, out double rz);
                    px = wp[p].X + rx; py = wp[p].Y + ry; pz = wp[p].Z + rz;
                    Mul(ax, ay, az, aw, b.RestRx, b.RestRy, b.RestRz, b.RestRw,
                        out qx, out qy, out qz, out qw);
                }
                wp[i] = new Vector3d(px, py, pz);
                wq[i * 4] = qx; wq[i * 4 + 1] = qy; wq[i * 4 + 2] = qz; wq[i * 4 + 3] = qw;

                double d = RigMeshBuilder.Hypot3(px - b.WorldPos.X, py - b.WorldPos.Y, pz - b.WorldPos.Z);
                if (d > worst) { worst = d; worstAt = i; }
            }

            if (worst > tolerance)
                throw new InvalidOperationException(
                    $"Composing the local rest chain lands bone {worstAt} " +
                    $"'{bones[Math.Max(0, worstAt)].Id}' {worst:E3} m from the rig's own " +
                    $"skeletonWorld, against {tolerance:E1} m. The bind pose and the bindposes " +
                    "would disagree by that much on every vertex, in every clip, identically.");
        }

        /// <summary>Mark the bones at least one bind-mesh corner is weighted to. A bone with no
        /// vertices is still REAL — it carries children, and the tip bones exist precisely so the
        /// blended rings have something to interpolate toward — so this records the fact rather
        /// than pruning anything.</summary>
        public static void MarkOwnership(RigBone[] bones, RigSkinning skin)
        {
            if (bones == null) throw new ArgumentNullException(nameof(bones));
            if (skin == null) throw new ArgumentNullException(nameof(skin));

            for (int i = 0; i < bones.Length; i++) bones[i].OwnsVertex = false;
            for (int i = 0; i < skin.Bone.Length; i++)
            {
                int b = skin.Bone[i];
                if (b < 0) continue;                        // padding
                if (b >= bones.Length)
                    throw new InvalidOperationException(
                        $"A bind-mesh corner is weighted to bone {b} and the skeleton has " +
                        $"{bones.Length}. The mesh and the skeleton came from different builds.");
                if (skin.Weight[i] > 0) bones[b].OwnsVertex = true;
            }
        }

        /// <summary>q ⊗ v — rotate a vector by a unit quaternion [x,y,z,w].</summary>
        static void Rotate(double qx, double qy, double qz, double qw,
                           double vx, double vy, double vz,
                           out double x, out double y, out double z)
        {
            double tx = 2 * (qy * vz - qz * vy);
            double ty = 2 * (qz * vx - qx * vz);
            double tz = 2 * (qx * vy - qy * vx);
            x = vx + qw * tx + (qy * tz - qz * ty);
            y = vy + qw * ty + (qz * tx - qx * tz);
            z = vz + qw * tz + (qx * ty - qy * tx);
        }

        /// <summary>a ∘ b — quaternion product, parent on the left, matching the rig's
        /// <c>worldsOf</c>: <c>W.R = Wp.R · L.R</c>.</summary>
        static void Mul(double ax, double ay, double az, double aw,
                        double bx, double by, double bz, double bw,
                        out double x, out double y, out double z, out double w)
        {
            x = aw * bx + ax * bw + ay * bz - az * by;
            y = aw * by - ax * bz + ay * bw + az * bx;
            z = aw * bz + ax * by - ay * bx + az * bw;
            w = aw * bw - ax * bx - ay * by - az * bz;
        }

        // ---------------------------------------------------------------------------------------
        // The skinning
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Read the bind mesh's per-CORNER bone weights, its per-FACE part names, and its vertex
        /// positions — in the rig's face order, which is <see cref="RigMeshData.Faces"/> order.
        ///
        /// <para>⚠️ <b>The width is measured and then enforced, in that order.</b> Rig 7's BLENDED
        /// rings carry TWO weights and everything else carries one; the export is packed at the
        /// measured maximum and the bake REFUSES anything above
        /// <see cref="Core.CharacterSkinDef.MaxBoneInfluences"/> rather than truncating — a
        /// truncated weight is not a smaller mesh, it is the hem collapse that measured 4.52e-2 m,
        /// 452× the rig's own tolerance, on every one of the 56 golden rows.</para>
        /// </summary>
        public static RigSkinning ReadSkinning(IRigScriptHost host, string preset, int maxWidth)
        {
            string g = GlobalName;
            if (maxWidth < 1) throw new ArgumentOutOfRangeException(nameof(maxWidth));

            int width = (int)host.EvaluateNumber(
                $"(function(){{var m={g}.bindMesh({Js(preset)}),w=0;" +
                "for(var i=0;i<m.length;i++)for(var k=0;k<m[i].bone.length;k++)" +
                "if(m[i].bone[k].length>w)w=m[i].bone[k].length;return w;})()");
            if (width < 1)
                throw new InvalidOperationException($"'{preset}': no bind-mesh corner carries a weight.");
            if (width > maxWidth)
                throw new InvalidOperationException(
                    $"'{preset}': the bind mesh carries up to {width} influences per vertex and this " +
                    $"path supports {maxWidth}. Widening is a renderer decision (SkinQuality and the " +
                    "def's MaxInfluences move together); silently dropping the tail is the measured " +
                    "452×-tolerance hem collapse. Refusing.");

            string parts = host.EvaluateString(
                $"{g}.bindMesh({Js(preset)}).map(function(f){{return f.part||'';}}).join(',')");

            // [i32 faceCount][i32 width] then per face [i32 nv]
            //   then nv × ( [f64 x,y,z] + width × ([i32 bone][f64 weight]) )
            host.Execute(
                $"globalThis.__hhSkinPack=(function(){{var m={g}.bindMesh({Js(preset)});" +
                $"var W={width.ToString(CultureInfo.InvariantCulture)};" +
                "var corners=0;for(var i=0;i<m.length;i++)corners+=m[i].v.length;" +
                "var buf=new ArrayBuffer(8+m.length*4+corners*(24+W*12));" +
                "var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,m.length,true);p+=4;dv.setInt32(p,W,true);p+=4;" +
                "for(var i=0;i<m.length;i++){var f=m[i];" +
                "if(f.bone.length!==f.v.length)throw new Error('face '+i+' has '+f.v.length+" +
                "' vertices and '+f.bone.length+' weight lists');" +
                "dv.setInt32(p,f.v.length,true);p+=4;" +
                "for(var k=0;k<f.v.length;k++){var v=f.v[k],bw=f.bone[k];" +
                "dv.setFloat64(p,v[0],true);p+=8;dv.setFloat64(p,v[1],true);p+=8;dv.setFloat64(p,v[2],true);p+=8;" +
                "for(var j=0;j<W;j++){" +
                "if(j<bw.length){dv.setInt32(p,bw[j][0],true);p+=4;dv.setFloat64(p,bw[j][1],true);p+=8;}" +
                "else{dv.setInt32(p,-1,true);p+=4;dv.setFloat64(p,0,true);p+=8;}}}}" +
                "return new Uint8ClampedArray(buf);})();");
            byte[] blob = host.EvaluateBytes("globalThis.__hhSkinPack");

            int off = 0;
            int faceCount = BitConverter.ToInt32(blob, off); off += 4;
            int w = BitConverter.ToInt32(blob, off); off += 4;
            string[] partList = parts.Split(',');
            if (partList.Length != faceCount)
                throw new InvalidOperationException(
                    $"bindMesh('{preset}') reported {partList.Length} part names and {faceCount} faces.");

            var cornerBone = new List<int>();
            var cornerWeight = new List<double>();
            var pos = new List<Vector3d>();
            for (int i = 0; i < faceCount; i++)
            {
                int nv = BitConverter.ToInt32(blob, off); off += 4;
                for (int k = 0; k < nv; k++)
                {
                    pos.Add(ReadV3(blob, ref off));
                    for (int j = 0; j < w; j++)
                    {
                        cornerBone.Add(BitConverter.ToInt32(blob, off)); off += 4;
                        cornerWeight.Add(D(blob, ref off));
                    }
                }
            }
            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"Skinning blob was {blob.Length} bytes and {off} consumed — packer/reader disagree.");

            return new RigSkinning
            {
                Width = w,
                MaxInfluences = w,
                CornerCount = pos.Count,
                Bone = cornerBone.ToArray(),
                Weight = cornerWeight.ToArray(),
                FacePart = partList,
                Position = pos.ToArray(),
            };
        }

        /// <summary>
        /// Prove rig 7's bind mesh IS rig 6's <c>pose('idle', 0)</c> face list — same faces, same
        /// corner order, same positions — before a single weight is attached to a mesh built from
        /// the latter. Without this the two could drift and nothing downstream would notice: the
        /// mesh would be correct, the weights would be correct, and they would belong to different
        /// vertices.
        /// </summary>
        public static void AssertBindAgrees(RigMeshData bind, RigSkinning skin, double tolerance)
        {
            if (bind == null) throw new ArgumentNullException(nameof(bind));
            if (skin == null) throw new ArgumentNullException(nameof(skin));

            if (bind.Faces.Count != skin.FacePart.Length)
                throw new InvalidOperationException(
                    $"rig 6 pose('idle',0) has {bind.Faces.Count} faces and rig 7's bind mesh has " +
                    $"{skin.FacePart.Length}. They are not the same geometry.");
            if (bind.VertexCount != skin.CornerCount)
                throw new InvalidOperationException(
                    $"rig 6 pose('idle',0) has {bind.VertexCount} corners and rig 7's bind mesh has " +
                    $"{skin.CornerCount}.");

            int c = 0;
            double worst = 0;
            int worstFace = -1;
            foreach (RigFace f in bind.Faces)
            {
                for (int k = 0; k < f.V.Length; k++, c++)
                {
                    Vector3d a = f.V[k], b = skin.Position[c];
                    double d = RigMeshBuilder.Hypot3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
                    if (d > worst) { worst = d; worstFace = c; }
                }
            }
            if (worst > tolerance)
                throw new InvalidOperationException(
                    $"rig 7's bind mesh differs from rig 6's pose('idle',0) by {worst:E3} m at corner " +
                    $"{worstFace}, against a tolerance of {tolerance:E1} m. The export is not " +
                    "re-expressing the body this bake takes its geometry from.");
        }

        // ---------------------------------------------------------------------------------------
        // The clips
        // ---------------------------------------------------------------------------------------

        /// <summary>Every <c>ANIMS</c> key, in the rig's own declaration order. Reached through the
        /// prototype, so it is the BODY's table — one clip per row is one clip per animation the
        /// sheets ship.</summary>
        public static string[] Anims(IRigScriptHost host) =>
            host.EvaluateString($"Object.keys({GlobalName}.ANIMS).join(',')")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// Read one clip's bone tracks. The rig's <c>clip()</c> resolves the whole row — frame count,
        /// rate, loop/settle, mount, carry and power — so none of that is re-derived in C#.
        /// </summary>
        public static RigSkinClip ReadClip(IRigScriptHost host, string preset, string anim,
                                           int boneCount)
        {
            string g = GlobalName;
            host.Execute($"globalThis.__hhClip={g}.clip({Js(anim)},{Js(preset)});");
            if (!host.EvaluateBool("globalThis.__hhClip !== null && globalThis.__hhClip !== undefined"))
                throw new InvalidOperationException(
                    $"clip('{anim}','{preset}') returned null — the rig's ANIMS table does not carry " +
                    $"'{anim}'. Clips are enumerated FROM that table, so this means the table moved " +
                    "under the bake mid-run.");

            var clip = new RigSkinClip
            {
                Anim = anim,
                Frames = (int)host.EvaluateNumber("globalThis.__hhClip.frames"),
                Ms = host.EvaluateNumber("globalThis.__hhClip.ms"),
                Loop = host.EvaluateBool("!!globalThis.__hhClip.loop"),
                Settle = host.EvaluateBool("!!globalThis.__hhClip.settle"),
                Mount = host.EvaluateString("String(globalThis.__hhClip.mount||'')"),
                Carry = host.EvaluateString("String(globalThis.__hhClip.carry||'')"),
                Power = host.EvaluateString("String(globalThis.__hhClip.power||'')"),
            };
            clip.Parked = host.EvaluateString("(globalThis.__hhClip.parked||[]).join(',')")
                              .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            int tracks = (int)host.EvaluateNumber("globalThis.__hhClip.tracks.length");
            if (tracks != clip.Frames)
                throw new InvalidOperationException(
                    $"clip('{anim}','{preset}') declares {clip.Frames} frames and carries {tracks} " +
                    "tracks. A clip whose declaration and payload disagree plays a different pose " +
                    "than the sheet baked, one frame at a time.");

            // The rig's `bones` array is the track key ORDER, and it must be the skeleton's order:
            // the def indexes bones, not names, so a permutation here would animate the right
            // skeleton with the wrong limbs and never throw.
            string order = host.EvaluateString("globalThis.__hhClip.bones.join(',')");

            // [f64 px,py,pz][f64 rx,ry,rz,rw] × frames × bones, frame-major.
            host.Execute(
                "globalThis.__hhClipPack=(function(){var c=globalThis.__hhClip,ids=c.bones;" +
                "var buf=new ArrayBuffer(c.tracks.length*ids.length*7*8);" +
                "var dv=new DataView(buf);var p=0;" +
                "for(var t=0;t<c.tracks.length;t++){var B=c.tracks[t].bones;" +
                "for(var i=0;i<ids.length;i++){var b=B[ids[i]];" +
                "if(!b)throw new Error('frame '+t+' has no track for bone '+ids[i]);" +
                "var a=b.pos,q=b.rot;" +
                "dv.setFloat64(p,a[0],true);p+=8;dv.setFloat64(p,a[1],true);p+=8;dv.setFloat64(p,a[2],true);p+=8;" +
                "dv.setFloat64(p,q[0],true);p+=8;dv.setFloat64(p,q[1],true);p+=8;" +
                "dv.setFloat64(p,q[2],true);p+=8;dv.setFloat64(p,q[3],true);p+=8;}}" +
                "return new Uint8ClampedArray(buf);})();");
            byte[] blob = host.EvaluateBytes("globalThis.__hhClipPack");

            string[] ids = order.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length != boneCount)
                throw new InvalidOperationException(
                    $"clip('{anim}','{preset}') animates {ids.Length} bones and the skeleton has " +
                    $"{boneCount}.");

            int n = clip.Frames * boneCount;
            clip.Pos = new Vector3d[n];
            clip.Rx = new double[n]; clip.Ry = new double[n];
            clip.Rz = new double[n]; clip.Rw = new double[n];

            int off = 0;
            for (int i = 0; i < n; i++)
            {
                clip.Pos[i] = ReadV3(blob, ref off);
                clip.Rx[i] = D(blob, ref off); clip.Ry[i] = D(blob, ref off);
                clip.Rz[i] = D(blob, ref off); clip.Rw[i] = D(blob, ref off);
            }
            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"Clip blob for '{anim}' was {blob.Length} bytes and {off} consumed.");
            return clip;
        }

        /// <summary>The bone-id order a clip's tracks are packed in — asserted against the skeleton
        /// so a permutation cannot animate the right rig with the wrong limbs.</summary>
        public static string[] ClipBoneOrder(IRigScriptHost host, string preset, string anim) =>
            host.EvaluateString($"{GlobalName}.clip({Js(anim)},{Js(preset)}).bones.join(',')")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        // ---------------------------------------------------------------------------------------

        static Vector3d ReadV3(byte[] b, ref int off)
        {
            var v = new Vector3d(BitConverter.ToDouble(b, off),
                                 BitConverter.ToDouble(b, off + 8),
                                 BitConverter.ToDouble(b, off + 16));
            off += 24;
            return v;
        }

        static double D(byte[] b, ref int off)
        {
            double d = BitConverter.ToDouble(b, off);
            off += 8;
            return d;
        }

        static string Js(string s) =>
            "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";


        /// <summary>
        /// SHA-256 of rig 7's source with line endings normalised to LF — the def's stale-bake
        /// guard for the SKINNING half of the kit.
        ///
        /// <para>Byte-for-byte the same rule as
        /// <see cref="CharacterPoseMeshExtractor.SourceSha256"/>, deliberately: a def pins BOTH
        /// rigs, and two normalisations that disagreed about a lone CR would let one hash go stale
        /// while the other stayed fresh — the exact failure the pin exists to catch.</para>
        /// </summary>
        public static string SourceSha256()
        {
            string full = Path.Combine(RigCatalog.RepoRoot, ScriptPath);
            byte[] raw = File.ReadAllBytes(full);
            var lf = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
                if (raw[i] != (byte)'\r' || i + 1 >= raw.Length || raw[i + 1] != (byte)'\n')
                    lf.Add(raw[i]);
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(lf.ToArray());
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>A one-line census for the bake log — the absolute counts the standing control
        /// asks for, so a run that quietly lost the head kit is visible in the log and not only in
        /// an exception.</summary>
        public static string Census(IRigScriptHost host, string preset)
        {
            var sb = new StringBuilder();
            string g = GlobalName;
            sb.Append(preset).Append(": ")
              .Append((int)host.EvaluateNumber($"{g}.bindMesh({Js(preset)}).length")).Append(" bind faces, ")
              .Append((int)host.EvaluateNumber($"{g}.skeleton({Js(preset)}).length")).Append(" bones, ")
              .Append(Anims(host).Length).Append(" ANIMS rows");
            return sb.ToString();
        }
    }
}
