using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// "Is the on-foot player standing at THIS stall?" — the proximity gate the placeholder stall inputs
    /// (<see cref="DevSellInput"/> / <see cref="DevBuyInput"/>) share, so selling/buying only fire when
    /// the walking player is in range of the stall (not from anywhere, not while aboard). The decision is
    /// the pure <see cref="StallGate"/>; this composes it with the live state.
    ///
    /// <para>Cross-module-clean: it reads the on-foot state from the Core <see cref="ControlModeChanged"/>
    /// signal (and seeds it from the optional <see cref="IActiveBoatService"/>), and resolves the player
    /// Transform WITHOUT referencing the Player module — an optional explicit ref, else
    /// <c>GameObject.Find</c> by name (the persistent "Player"), so no builder wiring is required. The
    /// owning MonoBehaviour drives <see cref="Enable"/>/<see cref="Disable"/> from OnEnable/OnDisable.</para>
    /// </summary>
    [Serializable]
    public sealed class StallReach
    {
        [Tooltip("Optional explicit on-foot player. If unset, the persistent player is found by name at " +
                 "runtime — no builder wiring needed.")]
        [SerializeField] private Transform _player;
        [Tooltip("Name of the player GameObject to locate when no explicit ref is set.")]
        [SerializeField] private string _playerObjectName = "Player";
        [Tooltip("How close the on-foot player must be to the stall to interact (metres).")]
        [SerializeField] private float _range = StallGate.DefaultRange;

        private Transform _cachedPlayer;
        private bool _onFoot = true;   // the greybox starts on foot
        private bool _subscribed;

        /// <summary>True if the player is currently on foot (vs aboard).</summary>
        public bool OnFoot => _onFoot;

        /// <summary>Start tracking the on-foot/aboard state. Call from the owner's OnEnable.</summary>
        public void Enable()
        {
            // Seed the initial mode in case we're enabled while already aboard (optional service).
            var boat = GameServices.ActiveBoat;
            _onFoot = boat == null || !boat.HasActiveBoat;

            if (_subscribed) return;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            _subscribed = true;
        }

        /// <summary>Stop tracking. Call from the owner's OnDisable.</summary>
        public void Disable()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            _subscribed = false;
        }

        private void OnControlModeChanged(ControlModeChanged e) => _onFoot = e.Mode == ControlMode.OnFoot;

        /// <summary>True iff the on-foot player is within range of <paramref name="stallPosition"/> (the
        /// stall's own transform). False while aboard or if the player can't be located.</summary>
        public bool CanInteract(Vector3 stallPosition)
        {
            Transform p = ResolvePlayer();
            return p != null && StallGate.CanInteract(_onFoot, p.position, stallPosition, _range);
        }

        // The on-foot player: an explicit ref if wired, else the persistent "Player" GO found by name
        // (cached; re-found if it was destroyed). Find runs only on a B/P press, never per frame.
        private Transform ResolvePlayer()
        {
            if (_player != null) return _player;
            if (_cachedPlayer == null && !string.IsNullOrEmpty(_playerObjectName))
            {
                var go = GameObject.Find(_playerObjectName);
                if (go != null) _cachedPlayer = go.transform;
            }
            return _cachedPlayer;
        }
    }
}
