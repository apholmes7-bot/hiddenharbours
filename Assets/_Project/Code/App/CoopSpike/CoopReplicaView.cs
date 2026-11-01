using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Draws a remote hull and baked skipper without gameplay components or service registration.</summary>
    public sealed class CoopReplicaView : MonoBehaviour
    {
        private CoopSpikeSettings _settings;
        private string _hullId;
        private SpriteRenderer _hull;
        private Transform _skipper;
        private IsoCharacterSprite _character;
        public string DrawnHullId => _hullId;
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
                BoatHullSkinner.ApplyHull(gameObject, _hull, hull, null);
                _hullId = id;
            }
            _skipper.localPosition = state.SkipperLocal;
            _skipper.rotation = Quaternion.identity;
            _character.HoldHeading(state.SkipperHeading);
        }
    }
}
