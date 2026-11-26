using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Draws a remote hull and its current figure without gameplay components or service registration.</summary>
    public sealed class CoopReplicaView : MonoBehaviour, ICharacterFigureStand
    {
        private CoopSpikeSettings _settings;
        private string _hullId;
        private SpriteRenderer _hull;
        private Transform _skipper;
        private SpriteRenderer _skipperRenderer;
        private IsoCharacterSprite _character;
        private BoatHullSkinner.Rig _rig;
        private Vector3 _standRigMetres;
        private ICharacterFigure _figure;
        public string DrawnHullId => _hullId;
        public Transform Skipper => _skipper;
        public IsoCharacterSprite Character => _character;
        public ICharacterFigure Figure => _figure;
        public IsoCharacterSprite FigureCharacter => _character;
        public Transform FigureHull => _rig.Presenter?.Visual;
        public Vector3 FigureStandRigMetres => _standRigMetres;
        public float FigureDeckBearingDegrees => DeckRiderFacingMath.DeckBearingFor(
            _character.HeadingDegrees, _rig.Presenter?.DrawnHeadingDegrees() ?? 0f);
        public void Configure(CoopSpikeSettings settings)
        {
            _settings = settings;
            _hull = gameObject.AddComponent<SpriteRenderer>();
            var skipper = new GameObject("RemoteSkipper");
            skipper.transform.SetParent(transform, false);
            _skipper = skipper.transform;
            _skipperRenderer = skipper.AddComponent<SpriteRenderer>();
            _character = skipper.AddComponent<IsoCharacterSprite>();
            _character.Configure(settings.Character);
        }
        private static float LocalPictureDatum()
        {
            var own = FindFirstObjectByType<BoatController>();
            var ride = own != null ? own.GetComponent<HullTideRide>() : null;
            return ride != null ? ride.BakedWaterlineElevation : 0f;
        }

        public void Show(CoopBoatState state) => Show(state.ToPose());

        public void Show(CoopBoatPose state)
        {
            if (_settings == null) return;
            string id = state.HullId;
            if (_hullId != id)
            {
                var hull = _settings.Hull(id);
                if (hull == null) return;
                // A hull's picture is struck at one state of tide, and that is the region's fact, not
                // the boat's. Give the remote hull the datum this participant's own hull rides on, or
                // it is drawn at mean water while every local hull beside it rides the real tide.
                var ride = GetComponent<HullTideRide>();
                if (ride == null) ride = gameObject.AddComponent<HullTideRide>();
                ride.Configure(LocalPictureDatum(), hull.DraughtMeters);
                _rig = BoatHullSkinner.ApplyHull(gameObject, _hull, hull, null);
                _hullId = id;
                // The skipper rides the boat's sorting group (ADR 0059): one order above her hull inside
                // it, as a moored boat's skipper does, rather than a fixed order in the decor band.
                int hullOrder = hull.Visual != null ? hull.Visual.SortingOrder : _hull.sortingOrder;
                _skipperRenderer.sortingOrder = hullOrder + 1;
                // The existing hull data chooses its renderer. Rebind after a hull swap so the
                // figure reads that hull's private depth frame, without installing a player rider.
                _figure = CharacterFigurePresentation.Service?.Attach(_skipper.gameObject, this);
            }
            _standRigMetres = state.SkipperStandRigMetres;
            _skipper.localPosition = state.SkipperLocal + state.SkipperVisualOffset;
            _skipper.rotation = Quaternion.identity;
            _character.HoldHeading(state.SkipperHeading);
            _character.Stance = state.SkipperStance;
        }
    }
}
