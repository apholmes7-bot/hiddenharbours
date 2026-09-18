using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>Which hulls have a MESH room (ADR 0041), derived from the bake's own switch.</b> One derivation
    /// for every sheet-side suite, so the sprite kit's expectations shrink in lockstep with
    /// <see cref="RigMeshAssetBaker.MeshInteriorHulls"/> and never from a list somebody keeps by hand.
    ///
    /// <para><b>Three joins, each checked.</b> A name on the switch must match a fleet hull (the parity
    /// fixture's discipline — an unmatched name is inert everywhere else); her committed
    /// <see cref="HullMeshDef"/> must actually carry the room (<see cref="HullMeshDef.HasMeshInterior"/>,
    /// the runtime's predicate — a hull put on the switch and never re-baked is "converted" in the bake's
    /// eyes and a sprite hull in the game's); and her interior def must exist, because a retired sheet
    /// with no def behind it is a cabin nobody can enter. Any of the three failing THROWS at discovery,
    /// where it is visible, rather than yielding one case fewer.</para>
    /// </summary>
    public static class ConvertedInteriors
    {
        const string InteriorDefFolder = "Assets/_Project/Data/Boats/Interiors";
        const string MeshIdPrefix = "hullmesh.";
        const string DefIdPrefix = "interior.";

        public readonly struct Converted
        {
            /// <summary>The fleet key ("lobsterBoat", "lobsterInshoreHardtopFundy") — one per hull.</summary>
            public readonly string Key;
            /// <summary>The rig's global — the switch's vocabulary ("LobsterBoatIso"); a generator
            /// family shares one.</summary>
            public readonly string GlobalName;
            /// <summary>Her interior def id ("interior.lobster_boat_iso").</summary>
            public readonly string DefId;
            /// <summary>Her interior def asset path.</summary>
            public readonly string DefAssetPath;
            /// <summary>The sidecar's own <c>hull_stem</c> — the sheet contract's and the S0 ledger's
            /// vocabulary ("lobsterBoatIsoRig", "lobsterBoatVariantsIsoRig.inshore_hardtop_fundy").</summary>
            public readonly string HullStem;

            public Converted(string key, string global, string defId, string defPath, string hullStem)
            {
                Key = key; GlobalName = global; DefId = defId; DefAssetPath = defPath; HullStem = hullStem;
            }
            public override string ToString() => $"{Key} ({GlobalName}: {DefId}, {HullStem})";
        }

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        public static IReadOnlyList<Converted> All()
        {
            var defs = AssetDatabase.FindAssets("t:BoatInteriorDef", new[] { InteriorDefFolder })
                                    .Select(AssetDatabase.GUIDToAssetPath)
                                    .Select(p => (path: p, def: AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(p)))
                                    .Where(x => x.def != null)
                                    .ToDictionary(x => x.def.Id, x => x, StringComparer.Ordinal);

            var found = new List<Converted>();
            // A name on the switch is a rig GLOBAL, and a generator family (the eighteen lobster
            // variants) shares one — so a name converts every hull that carries it, and each hull is
            // derived on her own def and her own mesh. A global that matches no hull is caught first.
            foreach (string global in RigMeshAssetBaker.MeshInteriorHulls)
                if (!HullMeshFleet.Hulls.Any(h => string.Equals(h.GlobalName, global, StringComparison.Ordinal)))
                    throw new InvalidOperationException(
                        $"RigMeshAssetBaker.MeshInteriorHulls names '{global}', which no hull in " +
                        "HullMeshFleet.Hulls carries as a GlobalName — misspelled, or retired from the " +
                        "catalog. The bake would never look her up, so nothing else would notice.");

            foreach (FleetHull hull in HullMeshFleet.Hulls)
            {
                if (!RigMeshAssetBaker.IsMeshInteriorHull(hull.GlobalName)) continue;
                string global = hull.GlobalName;

                if (!hull.MeshId.StartsWith(MeshIdPrefix, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"'{hull.Key}' ({global}): mesh id '{hull.MeshId}' does not start with " +
                        $"'{MeshIdPrefix}', so her interior def id cannot be derived from it.");
                string defId = DefIdPrefix + hull.MeshId.Substring(MeshIdPrefix.Length);

                var meshDef = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                if (meshDef == null || !meshDef.HasMeshInterior())
                    throw new InvalidOperationException(
                        $"'{hull.Key}' ({global}) is converted by MeshInteriorHulls but {hull.MeshAssetPath} carries no " +
                        "InteriorRamps — she was put on the switch and not re-baked. The runtime would " +
                        "still build her a sprite room, so retiring her sheets now would leave her with " +
                        "no cabin at all. Re-bake her first.");

                if (!defs.TryGetValue(defId, out var d))
                    throw new InvalidOperationException(
                        $"'{hull.Key}' ({global}) is converted but no BoatInteriorDef with id '{defId}' exists under " +
                        $"{InteriorDefFolder}. The mesh room is cut by the def's levels; without the def " +
                        "there is nothing to walk on.");

                found.Add(new Converted(hull.Key, global, defId, d.path, HullStemOf(d.def)));
            }
            return found;
        }

        /// <summary>The sidecar's own <c>hull_stem</c>, read from the file the def names as its
        /// source — the same field <c>BoatInteriorSheetTests</c> derives the cleared set from.</summary>
        static string HullStemOf(BoatInteriorDef def)
        {
            string abs = Path.Combine(RepoRoot, def.SourceSidecar ?? "");
            if (string.IsNullOrEmpty(def.SourceSidecar) || !File.Exists(abs))
                throw new InvalidOperationException(
                    $"def '{def.Id}' names source sidecar '{def.SourceSidecar}', which is not on disk.");
            object root = DeckSidecarJson.Parse(File.ReadAllText(abs));
            string stem = DeckSidecarJson.String(DeckSidecarJson.Member(root, "hull_stem"));
            if (string.IsNullOrWhiteSpace(stem))
                throw new InvalidOperationException($"sidecar '{def.SourceSidecar}' states no hull_stem.");
            return stem;
        }

        /// <summary>
        /// <b>The same mesh with every room-flagged face removed.</b> Built by filtering TexCoord1.y,
        /// the flag the bake writes, so it removes exactly what the shader's discard would have hidden
        /// and nothing else.
        ///
        /// <para>Two callers, two different needs, one rule. <c>FullMeshInteriorRenderTests</c> wants it
        /// as the CONTROL ARM — the same hull with no room, so "closed up the room costs nothing" is
        /// measured rather than remembered. The acceptance fixtures want it as the ORACLE'S SUBJECT: the
        /// CPU reference rasterizer is a transcription of the rig's exterior renderer and has no level
        /// gate, so on a converted hull it would draw the cabin through the topsides while the GPU (gate
        /// on, nothing cut) correctly hides it.</para>
        /// </summary>
        public static Mesh MeshWithoutTheRoom(Mesh src, out int roomVerts, out int hullVerts)
        {
            var tags = new System.Collections.Generic.List<Vector2>();
            src.GetUVs(1, tags);
            var uv0 = new System.Collections.Generic.List<Vector4>();
            src.GetUVs(0, uv0);
            Vector3[] v = src.vertices, n = src.normals;
            int[] tri = src.triangles;

            var keepV = new System.Collections.Generic.List<Vector3>();
            var keepN = new System.Collections.Generic.List<Vector3>();
            var keepA = new System.Collections.Generic.List<Vector4>();
            var keepL = new System.Collections.Generic.List<Vector2>();
            var keepT = new System.Collections.Generic.List<int>();
            var remap = new int[v.Length];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            roomVerts = 0;
            for (int i = 0; i < v.Length; i++) if (tags[i].y > 0.5f) roomVerts++;
            hullVerts = v.Length - roomVerts;

            for (int t = 0; t + 2 < tri.Length; t += 3)
            {
                if (tags[tri[t]].y > 0.5f) continue;          // flat per face, so one vertex decides
                for (int k = 0; k < 3; k++)
                {
                    int src_i = tri[t + k];
                    if (remap[src_i] < 0)
                    {
                        remap[src_i] = keepV.Count;
                        keepV.Add(v[src_i]); keepN.Add(n[src_i]);
                        keepA.Add(uv0[src_i]); keepL.Add(tags[src_i]);
                    }
                    keepT.Add(remap[src_i]);
                }
            }

            var m = new Mesh { name = src.name + "_HullOnly", indexFormat = src.indexFormat };
            m.SetVertices(keepV); m.SetNormals(keepN);
            m.SetUVs(0, keepA); m.SetUVs(1, keepL);
            m.SetTriangles(keepT, 0, true);
            return m;
        }

        /// <summary>
        /// <b>One mesh holding <paramref name="first"/>'s faces and then <paramref name="second"/>'s</b>,
        /// positions, normals, TexCoord0 and TexCoord1 — the channels
        /// <see cref="MeshWithoutTheRoom"/> keeps, for the same CPU oracle.
        ///
        /// <para>For a hull whose door leaf is baked apart (2026-09-17): the GPU draws the hull and the
        /// renderer's "DoorLeaf" child in one picture, and the reference rasterizer takes one mesh.
        /// A mesh without level tags reads as zero tags, which is what the bake writes for it.</para>
        /// </summary>
        public static Mesh MeshWithAppended(Mesh first, Mesh second)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var attrs = new List<Vector4>();
            var tags = new List<Vector2>();
            var tris = new List<int>();
            foreach (Mesh part in new[] { first, second })
            {
                int baseIndex = verts.Count;
                verts.AddRange(part.vertices);
                norms.AddRange(part.normals);
                var partAttrs = new List<Vector4>();
                part.GetUVs(0, partAttrs);
                var partTags = new List<Vector2>();
                part.GetUVs(1, partTags);
                if (partTags.Count != part.vertexCount)
                    partTags = Enumerable.Repeat(Vector2.zero, part.vertexCount).ToList();
                attrs.AddRange(partAttrs);
                tags.AddRange(partTags);
                foreach (int i in part.triangles) tris.Add(baseIndex + i);
            }

            var m = new Mesh
            {
                name = first.name + "+" + second.name,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            m.SetVertices(verts); m.SetNormals(norms);
            m.SetUVs(0, attrs); m.SetUVs(1, tags);
            m.SetTriangles(tris, 0, true);
            return m;
        }

        public static HashSet<string> DefIds() =>
            new HashSet<string>(All().Select(c => c.DefId), StringComparer.Ordinal);

        public static HashSet<string> HullStems() =>
            new HashSet<string>(All().Select(c => c.HullStem), StringComparer.Ordinal);
    }
}
