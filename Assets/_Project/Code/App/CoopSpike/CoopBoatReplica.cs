using System;
using HiddenHarbours.Core;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>One network boat per participant. Its owner follows the existing local gameplay boat.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
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
            var value = new CoopBoatState
            {
                HullId = _hull?.Invoke() ?? string.Empty,
                Recipe = _recipe?.Invoke() ?? string.Empty,
                SkipperLocal = _localSkipper != null ? _localBoat.InverseTransformPoint(_localSkipper.position) : Vector3.zero,
                SkipperHeading = _localSkipper != null ? _localSkipper.eulerAngles.z : 0
            };
            if (!Boat.Value.Equals(value)) Boat.Value = value;
        }
    }
}
