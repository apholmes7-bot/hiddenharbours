using System;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>One network boat per participant. Its owner follows the existing local gameplay boat.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    [DefaultExecutionOrder(200)] // sample after the authored rider's LateUpdate (100)
    public sealed class CoopBoatReplica : NetworkBehaviour
    {
        public readonly NetworkVariable<CoopBoatState> Boat = new(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<CoopSeaState> Sea = new(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        [SerializeField, Min(0.02f)] private float _seaPublishSeconds = 0.2f;
        private IGameClock _clock;
        private int _seed;
        private float _nextSea;
        private Transform _localBoat, _localSkipper;
        private IsoCharacterSprite _localCharacter;
        private DeckRiderVisual _localRider;
        private Func<string> _hull, _recipe;
        private CoopReplicaView _view;
        public bool ClockAccepted { get; private set; }

        public override void OnNetworkSpawn()
        {
            Sea.OnValueChanged += OnSea;
            Boat.OnValueChanged += OnBoat;
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            if (!IsOwner && settings != null)
            {
                _view = gameObject.AddComponent<CoopReplicaView>();
                _view.Configure(settings);
                _view.Show(Boat.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            Sea.OnValueChanged -= OnSea;
            Boat.OnValueChanged -= OnBoat;
        }

        public void BindClock(IGameClock clock, int seed)
        {
            _clock = clock; _seed = seed;
            if (IsSpawned && !IsServer) ClockAccepted = Sea.Value.Apply(_clock, _seed);
        }

        public void BindLocal(Transform boat, Transform skipper, Func<string> hull, Func<string> recipe)
        {
            _localBoat = boat; _localSkipper = skipper; _hull = hull; _recipe = recipe;
            _localCharacter = skipper != null ? skipper.GetComponent<IsoCharacterSprite>() : null;
            _localRider = skipper != null ? skipper.GetComponent<DeckRiderVisual>() : null;
        }

        public void PublishBoat(CoopBoatState value)
        {
            if (!IsSpawned || !IsOwner) throw new InvalidOperationException("Only the spawned owner publishes a boat.");
            Boat.Value = value;
        }

        private void OnSea(CoopSeaState previous, CoopSeaState next)
        {
            if (!IsServer && _clock != null) ClockAccepted = next.Apply(_clock, _seed);
        }

        private void OnBoat(CoopBoatState previous, CoopBoatState next) => _view?.Show(next);

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer && _clock != null && Time.unscaledTime >= _nextSea)
            {
                Sea.Value = CoopSeaState.Capture(_clock, _seed);
                _nextSea = Time.unscaledTime + _seaPublishSeconds;
            }
            if (!IsServer && _clock != null && ClockAccepted)
            {
                _clock.IsPaused = Sea.Value.Paused;
                _clock.TimeScale = Sea.Value.Rate;
            }
            if (!IsOwner || _localBoat == null) return;
            transform.SetPositionAndRotation(_localBoat.position, _localBoat.rotation);
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || _localBoat == null) return;
            Vector3 foot = _localSkipper != null ? _localSkipper.position : _localBoat.position;
            Vector3 drawn = foot;
            if (_localRider != null)
            {
                var mesh = _localRider.GetComponent<DeckRiderMeshPresenter>();
                if (mesh != null && mesh.DrawsInsteadOfSprite && mesh.PresentationRoot != null)
                    drawn = mesh.PresentationRoot.position;
                else if (!_localRider.BodyShownOnRoot && _localRider.RiderTransform != null)
                    drawn = _localRider.RiderTransform.position;
                // Mesh z belongs to the private hull depth buffer, not a sprite's scene depth.
                drawn.z = foot.z;
            }
            Vector3 local = _localBoat.InverseTransformPoint(foot);
            var value = new CoopBoatState
            {
                HullId = _hull?.Invoke() ?? string.Empty,
                Recipe = _recipe?.Invoke() ?? string.Empty,
                SkipperLocal = local,
                SkipperVisualOffset = _localBoat.InverseTransformPoint(drawn) - local,
                SkipperHeading = _localCharacter != null ? _localCharacter.HeadingDegrees
                    : _localSkipper != null ? _localSkipper.eulerAngles.z : 0
            };
            if (!Boat.Value.Equals(value)) Boat.Value = value;
        }
    }
}
