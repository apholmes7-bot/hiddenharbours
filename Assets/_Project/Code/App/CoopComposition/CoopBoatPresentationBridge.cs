using System;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>The composition root owns concrete rider/view wiring; replication sees Core only.</summary>
    public sealed class CoopBoatPresentationBridge : ICoopBoatPresentation
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => CoopBoatPresentation.Service = new CoopBoatPresentationBridge();

        public Action<CoopBoatPose> CreateRemote(GameObject replica)
        {
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            if (settings == null) return null;
            var view = replica.AddComponent<CoopReplicaView>();
            view.Configure(settings);
            return view.Show;
        }

        public Func<CoopBoatPose> BindLocal(Transform boat, Transform skipper, Func<string> hull, Func<string> recipe)
        {
            var character = skipper != null ? skipper.GetComponent<IsoCharacterSprite>() : null;
            var rider = skipper != null ? skipper.GetComponent<DeckRiderVisual>() : null;
            return () =>
            {
                Vector3 foot = skipper != null ? skipper.position : boat.position;
                Vector3 drawn = foot;
                if (rider != null)
                {
                    var mesh = rider.GetComponent<DeckRiderMeshPresenter>();
                    if (mesh != null && mesh.DrawsInsteadOfSprite && mesh.PresentationRoot != null)
                        drawn = mesh.PresentationRoot.position;
                    else if (!rider.BodyShownOnRoot && rider.RiderTransform != null)
                        drawn = rider.RiderTransform.position;
                    // Mesh z belongs to the private hull depth buffer, not a sprite's scene depth.
                    drawn.z = foot.z;
                }
                Vector3 local = boat.InverseTransformPoint(foot);
                return new CoopBoatPose
                {
                    HullId = hull?.Invoke() ?? string.Empty,
                    Recipe = recipe?.Invoke() ?? string.Empty,
                    SkipperLocal = local,
                    SkipperVisualOffset = boat.InverseTransformPoint(drawn) - local,
                    SkipperStandRigMetres = rider != null ? rider.DeckStandRigLocal : Vector3.zero,
                    SkipperStance = character != null ? character.Stance : CharacterStance.Free,
                    SkipperHeading = character != null ? character.HeadingDegrees
                        : skipper != null ? skipper.eulerAngles.z : 0
                };
            };
        }
    }
}
