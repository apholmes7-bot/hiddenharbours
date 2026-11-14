using System;
using HiddenHarbours.Core;
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
        private Transform _localBoat;
        private Func<CoopBoatPose> _readLocal;
        private Action<CoopBoatPose> _showRemote;
        public bool ClockAccepted { get; private set; }

        public override void OnNetworkSpawn()
        {
            Sea.OnValueChanged += OnSea;
            Boat.OnValueChanged += OnBoat;
            if (!IsOwner)
            {
                _showRemote = CoopBoatPresentation.Service?.CreateRemote(gameObject);
                _showRemote?.Invoke(Boat.Value.ToPose());
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
            var presentation = CoopBoatPresentation.Service
                ?? throw new InvalidOperationException("App must install the co-op presentation contract before binding.");
            _localBoat = boat;
            _readLocal = presentation.BindLocal(boat, skipper, hull, recipe);
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

        private void OnBoat(CoopBoatState previous, CoopBoatState next) => _showRemote?.Invoke(next.ToPose());

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
            var value = CoopBoatState.FromPose(_readLocal());
            if (!Boat.Value.Equals(value)) Boat.Value = value;
        }
    }
}
