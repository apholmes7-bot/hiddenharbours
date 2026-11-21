using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The player's arms stay out of his body</b> on the three clips that used to bury them: dig
    /// (the left forearm sank into chest and belly, 4.6 rig px), hauler (2.2 px on every frame) and
    /// strike (frames 3 and 4, 2.1 px into chest and face). The poses source moved only left-arm keys
    /// (rig 10.3 arm clearance, characterIsoRig10.poses.js); this pins the result on the COMMITTED
    /// skin, as the game draws it.
    ///
    /// <para>The measure: each body part is a capsule along its bones, its radius the 60th
    /// percentile of its own bind vertices' distances from that segment. The torso runs
    /// pelvis, spine, chest, neck; the head runs from the head bone 0.18 m on along neck to head.
    /// A vertex belongs to a part when its first weight (at least 0.5) is that part's bone. An arm
    /// vertex's depth inside the torso or head is the capsule radius minus its distance from the
    /// capsule's segment, in rig pixels. The limit is <see cref="GameConfig.MeshArmClearancePx"/>
    /// in the committed GameConfig, so it is tuned there and not here.</para>
    /// </summary>
    public class CharacterSkinArmClearanceTests
    {
        const string PlayerSkinPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        static readonly string[] Clips = { "dig", "hauler", "strike" };

        // The capsule model's own geometry, the same the clearance sweep measured with: how far the
        // head capsule reaches past the head bone, and which percentile of a part's bind distances
        // is its radius.
        const float HeadReachMetres = 0.18f;
        const float RadiusPercentile = 0.6f;
        const float OwnWeight = 0.5f;

        enum Part { None = -1, Torso, Head, ForearmL, HandL, ForearmR, HandR }

        sealed class Capsules
        {
            public int[][] Segments;   // per part: bone index pairs; -2 as the end means "head reach"
            public int Head, Neck;

            public float Distance(Part part, Vector3 v, Vector3[] bonePos)
            {
                int[] s = Segments[(int)part];
                float best = float.PositiveInfinity;
                for (int k = 0; k < s.Length; k += 2)
                {
                    Vector3 a = bonePos[s[k]], b;
                    if (s[k + 1] == -2)
                    {
                        Vector3 up = bonePos[Head] - bonePos[Neck];
                        b = a + up.normalized * HeadReachMetres;
                    }
                    else b = bonePos[s[k + 1]];
                    Vector3 ab = b - a;
                    float l2 = ab.sqrMagnitude;
                    float t = l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(v - a, ab) / l2) : 0f;
                    best = Mathf.Min(best, (v - (a + ab * t)).magnitude);
                }
                return best;
            }
        }

        [Test]
        public void DigHaulerAndStrike_KeepEveryArmVertexOutOfTorsoAndHead_WithinTheConfiguredLimit()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, $"{ConfigAssetPath} does not load as a GameConfig.");
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(PlayerSkinPath);
            Assert.IsNotNull(def, $"{PlayerSkinPath} does not load as a CharacterSkinDef.");
            Assert.IsNotNull(def.BindMesh, "the player's skin has no bind mesh.");

            float limitPx = config.MeshArmClearancePx;
            Mesh mesh = def.BindMesh;
            Vector3[] bind = mesh.vertices;
            Vector3[] norms = mesh.normals;
            if (norms == null || norms.Length != bind.Length) norms = new Vector3[bind.Length];
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindposes = mesh.bindposes;
            CharacterSkinDef.Bone[] bones = def.Bones;
            int n = bones.Length;
            Assert.AreEqual(n, bindposes.Length, "the bind mesh's bindposes are not parallel to the def's bones.");
            Assert.AreEqual(bind.Length, weights.Length, "the bind mesh has no weight per vertex.");

            int B(string id)
            {
                int i = def.IndexOfBone(id);
                Assert.GreaterOrEqual(i, 0, $"the player's skin has no '{id}' bone.");
                return i;
            }

            var caps = new Capsules
            {
                Head = B("head"),
                Neck = B("neck"),
                Segments = new[]
                {
                    new[] { B("pelvis"), B("spine"), B("spine"), B("chest"), B("chest"), B("neck") },
                    new[] { B("head"), -2 },
                    new[] { B("elbow_L"), B("hand_L") },
                    new[] { B("hand_L"), B("hand_L") },
                    new[] { B("elbow_R"), B("hand_R") },
                    new[] { B("hand_R"), B("hand_R") },
                },
            };
            var boneToPart = new Dictionary<int, Part>
            {
                { B("pelvis"), Part.Torso }, { B("spine"), Part.Torso }, { B("chest"), Part.Torso },
                { B("head"), Part.Head },
                { B("elbow_L"), Part.ForearmL }, { B("hand_L"), Part.HandL },
                { B("elbow_R"), Part.ForearmR }, { B("hand_R"), Part.HandR },
            };

            var part = new Part[bind.Length];
            for (int v = 0; v < bind.Length; v++)
                part[v] = weights[v].weight0 >= OwnWeight && boneToPart.TryGetValue(weights[v].boneIndex0, out Part p)
                    ? p : Part.None;

            var bindBonePos = new Vector3[n];
            for (int b = 0; b < n; b++) bindBonePos[b] = bindposes[b].inverse.GetColumn(3);

            float RadiusOf(Part target)
            {
                var d = new List<float>();
                for (int v = 0; v < bind.Length; v++)
                    if (part[v] == target) d.Add(caps.Distance(target, bind[v], bindBonePos));
                Assert.IsNotEmpty(d, $"no bind vertex belongs to the {target}; the capsule has no radius.");
                d.Sort();
                return d[(int)Math.Floor(d.Count * RadiusPercentile)];
            }

            var bodies = new[] { Part.Torso, Part.Head };
            var arms = new[] { Part.ForearmL, Part.HandL, Part.ForearmR, Part.HandR };
            var radius = new Dictionary<Part, float>();
            foreach (Part body in bodies) radius[body] = RadiusOf(body);

            var world = new Matrix4x4[n];
            var skin = new Matrix4x4[n];
            var bonePos = new Vector3[n];
            var outVerts = new Vector3[bind.Length];
            var outNorms = new Vector3[bind.Length];
            var over = new List<string>();
            int framesMeasured = 0;

            foreach (string state in Clips)
            {
                Assert.IsTrue(def.TryGetClip(state, out CharacterSkinDef.SkinClip clip),
                    $"the player's skin has no '{state}' clip.");
                for (int f = 0; f < clip.FrameCount; f++)
                {
                    CharacterSkinPose.ComposeSkinMatrices(clip, f, bones, bindposes, world, skin);
                    CharacterSkinPose.Skin(skin, weights, bind, norms, outVerts, outNorms);
                    for (int b = 0; b < n; b++) bonePos[b] = world[b].GetColumn(3);
                    framesMeasured++;

                    foreach (Part arm in arms)
                    foreach (Part body in bodies)
                    {
                        float deepest = 0f;
                        for (int v = 0; v < outVerts.Length; v++)
                        {
                            if (part[v] != arm) continue;
                            float depth = radius[body] - caps.Distance(body, outVerts[v], bonePos);
                            if (depth > deepest) deepest = depth;
                        }
                        float px = deepest * def.PxPerMetre;
                        if (px > limitPx) over.Add($"{state} frame {f}: {arm} {px:0.00} px into the {body}");
                    }
                }
            }

            Assert.Greater(framesMeasured, 0, "no frame was measured; the check would pass on nothing.");
            Assert.IsEmpty(over,
                $"an arm sits deeper than {limitPx:0.00} rig px (GameConfig.MeshArmClearancePx) inside the " +
                "player's body. Move the arm's keys in characterIsoRig10.poses.js and re-bake the skin; " +
                "never raise the limit to pass.\n" + string.Join("\n", over));
        }
    }
}
