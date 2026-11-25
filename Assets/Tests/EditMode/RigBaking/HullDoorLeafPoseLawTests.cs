using System;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class HullDoorLeafPoseLawTests
    {
        static RigFace Face(double x = 0) => new RigFace
        {
            Mat = 2, Level = 1, B = .25, Db = .01,
            V = new[] { new Vector3d(x, 0, 0), new Vector3d(x + 1, 0, 0), new Vector3d(x, 1, 0) }
        };

        static RigMeshData Pose(params RigFace[] faces)
        {
            var pose = new RigMeshData();
            pose.Faces.AddRange(faces);
            return pose;
        }

        [Test]
        public void MovedShutFacesThenUncoveredFaces_Agree()
        {
            Assert.That(RigMeshAssetBaker.DoorLeafPosesAgree(Pose(Face()), Pose(Face(3), Face(5))), Is.True);
        }

        [Test]
        public void FewerFacesOpen_Refuse()
        {
            Assert.That(RigMeshAssetBaker.DoorLeafPosesAgree(Pose(Face(), Face(2)), Pose(Face(3))), Is.False);
        }

        [Test]
        public void ChangedShutFaceAttributesOrVertexCount_Refuse()
        {
            Action<RigFace>[] changes = {
                face => face.Mat = 3, face => face.Level = 2,
                face => face.B = .5, face => face.Db = .02,
                face => face.V = new[] { new Vector3d(0, 0, 0), new Vector3d(1, 0, 0),
                    new Vector3d(1, 1, 0), new Vector3d(0, 1, 0) }
            };
            foreach (var change in changes)
            {
                var moved = Face(3);
                change(moved);
                Assert.That(RigMeshAssetBaker.DoorLeafPosesAgree(Pose(Face()), Pose(moved, Face(5))), Is.False);
            }
        }

        [Test]
        public void LegacyHullWithUnequalPoses_KeepsItsOriginalRefusal()
        {
            using var host = RigScriptHostFactory.Create();
            var data = HullWalkFixture.Extract(host);
            data.Walk = null;
            host.Execute("BoxWalk.doorFaces = (function(original) { return function(o) { " +
                "var faces = original(o.doorOpen); faces.forEach(function(f) { f.lv = 'room'; }); " +
                "return o.doorOpen === 1 ? faces.concat(faces) : faces; " +
                "}; })(BoxWalk.doorFaces);");
            var error = Assert.Throws<InvalidOperationException>(() => RigMeshAssetBaker.FindDoorLeaf(host, "BoxWalk", data));
            Assert.That(error.Message, Is.EqualTo("BoxWalk: her door leaf has 1 faces shut and 2 open. " +
                "The renderer swaps one leaf mesh for the other, so the two poses must be the same faces moved, not different faces."));
        }
    }
}
