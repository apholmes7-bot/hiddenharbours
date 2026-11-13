using System;
using HiddenHarbours.Core;
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
        public Vector3 SkipperVisualOffset;
        public float SkipperHeading;
        // The drawn feet live in the hull's rig frame, not the sprite's flattened scene depth.
        public Vector3 SkipperStandRigMetres;
        public CharacterStance SkipperStance;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref HullId); s.SerializeValue(ref Recipe);
            s.SerializeValue(ref SkipperLocal); s.SerializeValue(ref SkipperVisualOffset);
            s.SerializeValue(ref SkipperHeading);
            s.SerializeValue(ref SkipperStandRigMetres); s.SerializeValue(ref SkipperStance);
        }

        public bool Equals(CoopBoatState other) => HullId.Equals(other.HullId) && Recipe.Equals(other.Recipe)
            && SkipperLocal.Equals(other.SkipperLocal) && SkipperVisualOffset.Equals(other.SkipperVisualOffset)
            && SkipperHeading.Equals(other.SkipperHeading)
            && SkipperStandRigMetres.Equals(other.SkipperStandRigMetres) && SkipperStance == other.SkipperStance;
        public override bool Equals(object obj) => obj is CoopBoatState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(HullId, SkipperLocal, SkipperVisualOffset,
            SkipperHeading, SkipperStandRigMetres, SkipperStance);
    }
}
