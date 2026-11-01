using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Owner-written presentation facts; no catch, wallet, hands or save state.</summary>
    public struct CoopBoatState : INetworkSerializable, IEquatable<CoopBoatState>
    {
        public FixedString64Bytes HullId;
        public FixedString4096Bytes Recipe;
        public Vector3 SkipperLocal;
        public float SkipperHeading;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref HullId); s.SerializeValue(ref Recipe);
            s.SerializeValue(ref SkipperLocal); s.SerializeValue(ref SkipperHeading);
        }

        public bool Equals(CoopBoatState other) => HullId.Equals(other.HullId) && Recipe.Equals(other.Recipe)
            && SkipperLocal.Equals(other.SkipperLocal) && SkipperHeading.Equals(other.SkipperHeading);
        public override bool Equals(object obj) => obj is CoopBoatState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(HullId, SkipperLocal, SkipperHeading);
    }
}
