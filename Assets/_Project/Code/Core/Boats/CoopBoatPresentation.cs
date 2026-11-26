using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Presentation facts only; no network package, gameplay authority or saved state.</summary>
    public struct CoopBoatPose
    {
        public string HullId, Recipe;
        public Vector3 SkipperLocal, SkipperVisualOffset, SkipperStandRigMetres;
        public float SkipperHeading;
        public CharacterStance SkipperStance;
    }

    /// <summary>App supplies the existing local pose reader and remote view to replication.</summary>
    public interface ICoopBoatPresentation
    {
        // Bind once; the returned reader samples the current pose after the rider's LateUpdate.
        Func<CoopBoatPose> BindLocal(Transform boat, Transform skipper, Func<string> hull, Func<string> recipe);
        // Null means the optional trial settings are absent. The returned view belongs to replica.
        Action<CoopBoatPose> CreateRemote(GameObject replica);
    }

    public static class CoopBoatPresentation
    {
        public static ICoopBoatPresentation Service { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Service = null;
    }
}
