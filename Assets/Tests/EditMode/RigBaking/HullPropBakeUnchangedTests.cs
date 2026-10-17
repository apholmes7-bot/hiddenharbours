using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The held-prop bake (ADR 0053) left the hull fittings exactly where they were.</b>
    ///
    /// <para>The held props bake through the fittings' own extractor and baker. What they added is a
    /// branch a fitting never takes — <see cref="RigPropExtraction.MaterialsCall"/>, null for all six —
    /// and a section of <see cref="RigPropMeshBaker"/> below the fittings' code. This fixture proves
    /// the fittings still take the identical road: it re-extracts and rebuilds all six in memory, with
    /// no asset written, and compares them with the committed defs — every vertex attribute bit for
    /// bit, every index, and every field the bake writes. Not one byte of an oar or an outboard may
    /// move.</para>
    ///
    /// <para>It is a freshness guard too, as <see cref="PropMeshMaterialOrderTests"/> is for the ramp
    /// order: a rig edit that moves an oar or an outboard reds it until the fittings are re-baked
    /// (<see cref="RigPropMeshBaker.BakeAllProps"/>) and the result committed.</para>
    /// </summary>
    public class HullPropBakeUnchangedTests
    {
        [Test]
        public void HullPropBake_Unchanged()
        {
            var report = new StringBuilder();
            bool bad = false;

            foreach (FleetProp p in HullPropFleet.All)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(p.AssetPath);
                if (def == null)
                {
                    report.AppendLine($"  ✗ {p.Label}: {p.AssetPath} did not load.");
                    bad = true;
                    continue;
                }

                var wrong = new List<string>();
                RigMeshBuild build = null, fixedBuild = null;
                try
                {
                    // Bake's own steps, in its order, short of writing the asset.
                    using IRigScriptHost host = RigScriptHostFactory.Create();
                    RigMeshData data = RigMeshExtractor.ExtractFrom(host, p.ScriptPath, p.GlobalName, p.Extraction);
                    var moving = new List<RigFace>();
                    var fixedFaces = new List<RigFace>();
                    foreach (RigFace f in data.Faces) (f.FixedInPose ? fixedFaces : moving).Add(f);
                    build = RigMeshBuilder.Build(data.WithFaces(moving), $"{p.Key}PropMesh");
                    if (fixedFaces.Count > 0)
                        fixedBuild = RigMeshBuilder.Build(data.WithFaces(fixedFaces), $"{p.Key}PropFixedMesh");

                    SameMesh("Mesh", def.Mesh, build.Mesh, wrong);
                    SameMesh("FixedMesh", def.FixedMesh, fixedBuild?.Mesh, wrong);
                    SameDef(def, p, data, host, wrong);
                }
                catch (Exception e)
                {
                    wrong.Add($"the re-bake threw {e.GetType().Name}: {e.Message}");
                }
                finally
                {
                    if (build != null) UnityEngine.Object.DestroyImmediate(build.Mesh);
                    if (fixedBuild != null) UnityEngine.Object.DestroyImmediate(fixedBuild.Mesh);
                }

                if (wrong.Count == 0)
                {
                    report.AppendLine($"  ✓ {p.Label}: {def.Mesh.vertexCount} verts" +
                                      (def.FixedMesh == null ? "" : $" + {def.FixedMesh.vertexCount} bolted down") +
                                      ", every byte as committed");
                    continue;
                }
                bad = true;
                report.AppendLine($"  ✗ {p.Label} ({p.AssetPath}):");
                foreach (string w in wrong) report.AppendLine($"      {w}");
            }

            Assert.IsFalse(bad,
                "A hull fitting no longer re-bakes to its committed bytes. The held-prop bake (ADR 0053) must " +
                "leave the oars and the outboards exactly as they were. If a RIG moved one on purpose, re-bake " +
                $"the fittings (RigPropMeshBaker.BakeAllProps) and commit the result.\n{report}");
            Debug.Log($"[hull-prop] all {HullPropFleet.All.Count} fittings re-bake to their committed bytes:\n{report}");
        }

        // ---- the mesh: layout, every attribute, every index -------------------------------------

        static void SameMesh(string slot, Mesh committed, Mesh rebaked, List<string> wrong)
        {
            if (committed == null || rebaked == null)
            {
                if (committed != rebaked)
                    wrong.Add($"{slot}: committed {NameOf(committed)}, re-baked {NameOf(rebaked)}");
                return;
            }

            if (committed.name != rebaked.name) wrong.Add($"{slot} name: committed {committed.name}, re-baked {rebaked.name}");
            if (committed.vertexCount != rebaked.vertexCount)
            {
                wrong.Add($"{slot} vertex count: committed {committed.vertexCount}, re-baked {rebaked.vertexCount}");
                return;
            }

            string layoutA = Layout(committed.GetVertexAttributes()), layoutB = Layout(rebaked.GetVertexAttributes());
            if (layoutA != layoutB) wrong.Add($"{slot} vertex layout: committed {layoutA}, re-baked {layoutB}");

            SameVectors($"{slot} positions", committed.vertices.Select(v => (Vector4)v).ToArray(),
                        rebaked.vertices.Select(v => (Vector4)v).ToArray(), wrong);
            SameVectors($"{slot} normals", committed.normals.Select(v => (Vector4)v).ToArray(),
                        rebaked.normals.Select(v => (Vector4)v).ToArray(), wrong);
            SameVectors($"{slot} tangents", committed.tangents, rebaked.tangents, wrong);
            SameVectors($"{slot} colours", committed.colors32.Select(c => new Vector4(c.r, c.g, c.b, c.a)).ToArray(),
                        rebaked.colors32.Select(c => new Vector4(c.r, c.g, c.b, c.a)).ToArray(), wrong);
            for (int ch = 0; ch < 8; ch++)
            {
                var a = new List<Vector4>();
                var b = new List<Vector4>();
                committed.GetUVs(ch, a);
                rebaked.GetUVs(ch, b);
                SameVectors($"{slot} UV{ch}", a.ToArray(), b.ToArray(), wrong);
            }

            if (committed.indexFormat != rebaked.indexFormat)
                wrong.Add($"{slot} index format: committed {committed.indexFormat}, re-baked {rebaked.indexFormat}");
            if (committed.subMeshCount != rebaked.subMeshCount)
            {
                wrong.Add($"{slot} submeshes: committed {committed.subMeshCount}, re-baked {rebaked.subMeshCount}");
                return;
            }
            for (int s = 0; s < committed.subMeshCount; s++)
            {
                SubMeshDescriptor da = committed.GetSubMesh(s), db = rebaked.GetSubMesh(s);
                string sa = $"{da.indexStart}+{da.indexCount} {da.topology} base {da.baseVertex} verts {da.firstVertex}+{da.vertexCount}";
                string sb = $"{db.indexStart}+{db.indexCount} {db.topology} base {db.baseVertex} verts {db.firstVertex}+{db.vertexCount}";
                if (sa != sb) wrong.Add($"{slot} submesh {s}: committed {sa}, re-baked {sb}");

                int[] ia = committed.GetIndices(s), ib = rebaked.GetIndices(s);
                if (ia.Length != ib.Length)
                {
                    wrong.Add($"{slot} submesh {s} indices: committed {ia.Length}, re-baked {ib.Length}");
                    continue;
                }
                for (int i = 0; i < ia.Length; i++)
                {
                    if (ia[i] == ib[i]) continue;
                    wrong.Add($"{slot} submesh {s} index {i}: committed {ia[i]}, re-baked {ib[i]}");
                    break;
                }
            }
        }

        static string NameOf(Mesh m) => m == null ? "none" : m.name;

        static string Layout(VertexAttributeDescriptor[] attrs) =>
            string.Join(" ", attrs.Select(d => $"{d.attribute}:{d.format}x{d.dimension}@{d.stream}"));

        /// <summary>Bit for bit: a re-bake that moved a value by one ULP, or flipped the sign of a
        /// zero, has moved a byte.</summary>
        static void SameVectors(string what, Vector4[] a, Vector4[] b, List<string> wrong)
        {
            if (a.Length != b.Length)
            {
                wrong.Add($"{what}: committed {a.Length}, re-baked {b.Length}");
                return;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (SameBits(a[i].x, b[i].x) && SameBits(a[i].y, b[i].y) &&
                    SameBits(a[i].z, b[i].z) && SameBits(a[i].w, b[i].w)) continue;
                wrong.Add($"{what} [{i}]: committed {Fmt(a[i])}, re-baked {Fmt(b[i])}");
                return;
            }
        }

        // ---- the def: every field the bake writes ------------------------------------------------

        static void SameDef(HullPropMeshDef def, in FleetProp p, RigMeshData data, IRigScriptHost host,
                            List<string> wrong)
        {
            string g = p.GlobalName;
            RigPropExtraction x = p.Extraction;

            Same(wrong, "Id", def.Id, p.PropId);
            Same(wrong, "SourceRigPath", def.SourceRigPath, p.ScriptPath);
            Same(wrong, "SourceFaceBuilder", def.SourceFaceBuilder, x.FaceBuilderCall);
            SameFloats(wrong, "LightN", Floats(def.LightN), Floats(data.LightN.ToVector3()));
            SameFloats(wrong, "Gain", new[] { def.Gain }, new[] { (float)data.Gain });
            SameFloats(wrong, "Bias", new[] { def.Bias }, new[] { (float)data.Bias });
            Same(wrong, "Keyline", Rgba(def.Keyline), Rgba(data.Keyline));
            SameFloats(wrong, "PivotPx", new[] { def.PivotPx.x, def.PivotPx.y },
                       new[] { (float)data.PivotX, (float)data.PivotY });
            Same(wrong, "PxPerMetre", def.PxPerMetre, data.PxPerMetre);
            Same(wrong, "CellW", def.CellW, data.W);
            Same(wrong, "CellH", def.CellH, data.H);
            SameFloats(wrong, "ElevationDeg", new[] { def.ElevationDeg }, new[] { (float)data.DefaultElev });
            SameFloats(wrong, "PivotLocalMeters", Floats(def.PivotLocalMeters),
                       Floats(ReadVector3(host, $"{g}.{x.PivotCall}")));
            SameFloats(wrong, "MaxSteerDegrees", new[] { def.MaxSteerDegrees },
                       new[] { ReadOptionalNumber(host, g, x.MaxSteerCall) });
            SameFloats(wrong, "MaxTiltDegrees", new[] { def.MaxTiltDegrees },
                       new[] { ReadOptionalNumber(host, g, x.MaxTiltCall) });
            SameFloats(wrong, "LateralMountsMeters", def.LateralMountsMeters ?? Array.Empty<float>(),
                       ReadOptionalNumbers(host, g, x.LateralMountsCall));

            HullMeshDef.Ramp[] ramps = def.Ramps ?? Array.Empty<HullMeshDef.Ramp>();
            if (ramps.Length != data.Materials.Count)
                wrong.Add($"Ramps: committed {ramps.Length}, re-baked {data.Materials.Count}");
            else
                for (int m = 0; m < ramps.Length; m++)
                {
                    Same(wrong, $"Ramps[{m}] ({data.Materials[m].Name}) colours",
                         string.Join(" ", (ramps[m].Colors ?? Array.Empty<Color32>()).Select(c => Rgba(c))),
                         string.Join(" ", data.Materials[m].Ramp.Select(c => Rgba(c))));
                    Same(wrong, $"Ramps[{m}] ({data.Materials[m].Name}) offset", ramps[m].Offset, data.Materials[m].Off);
                }

            var bayer = new float[16];
            for (int bx = 0; bx < 4; bx++)
                for (int by = 0; by < 4; by++)
                    bayer[bx * 4 + by] = (float)data.Bayer[bx, by];
            SameFloats(wrong, "Bayer16", def.Bayer16 ?? Array.Empty<float>(), bayer);
        }

        static void Same<T>(List<string> wrong, string field, T committed, T rebaked)
        {
            if (!EqualityComparer<T>.Default.Equals(committed, rebaked))
                wrong.Add($"{field}: committed {committed}, re-baked {rebaked}");
        }

        static void SameFloats(List<string> wrong, string field, float[] committed, float[] rebaked)
        {
            if (committed.Length == rebaked.Length && committed.Zip(rebaked, (a, b) => SameBits(a, b)).All(s => s)) return;
            wrong.Add($"{field}: committed [{string.Join(", ", committed.Select(f => Fmt(f)))}], " +
                      $"re-baked [{string.Join(", ", rebaked.Select(f => Fmt(f)))}]");
        }

        // The baker's own readers (RigPropMeshBaker.ReadVector3 / ReadOptionalNumber / ReadOptionalNumbers,
        // private to it), line for line: the same JavaScript and the same parse, so a value compared
        // here is the value the bake writes.

        static Vector3 ReadVector3(IRigScriptHost host, string expr)
        {
            string[] parts = host.EvaluateString($"(function(){{var v={expr};return v.join(',');}})()").Split(',');
            if (parts.Length != 3)
                throw new InvalidOperationException($"{expr} was expected to yield [x,y,z] but gave {parts.Length} values.");
            return new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture),
                               float.Parse(parts[1], CultureInfo.InvariantCulture),
                               float.Parse(parts[2], CultureInfo.InvariantCulture));
        }

        static float ReadOptionalNumber(IRigScriptHost host, string g, string call) =>
            string.IsNullOrEmpty(call) ? 0f : (float)host.EvaluateNumber($"{g}.{call}");

        static float[] ReadOptionalNumbers(IRigScriptHost host, string g, string call)
        {
            if (string.IsNullOrEmpty(call)) return Array.Empty<float>();
            string blob = host.EvaluateString($"(function(){{var v={g}.{call};return v.join(',');}})()");
            if (string.IsNullOrWhiteSpace(blob)) return Array.Empty<float>();
            return blob.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        }

        static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        static float[] Floats(Vector3 v) => new[] { v.x, v.y, v.z };
        static string Rgba(Color32 c) => $"#{c.r:x2}{c.g:x2}{c.b:x2}{c.a:x2}";
        static string Fmt(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        static string Fmt(Vector4 v) => $"({Fmt(v.x)}, {Fmt(v.y)}, {Fmt(v.z)}, {Fmt(v.w)})";
    }
}
