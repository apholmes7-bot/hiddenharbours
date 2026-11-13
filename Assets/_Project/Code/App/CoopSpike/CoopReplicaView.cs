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
            var renderer = skipper.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            _character = skipper.AddComponent<IsoCharacterSprite>();
            _character.Configure(settings.Character);
        }
        public void Show(CoopBoatState state)
        {
            if (_settings == null) return;
            string id = state.HullId.ToString();
            if (_hullId != id)
            {
                var hull = _settings.Hull(id);
                if (hull == null) return;
                _rig = BoatHullSkinner.ApplyHull(gameObject, _hull, hull, null);
                _hullId = id;
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
