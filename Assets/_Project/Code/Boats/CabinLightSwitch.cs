using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>A physical cabin-light switch on a boat</b>: one at her helm, and one by her entry door when
    /// the helm is far from it (<see cref="CabinLightSwitchRule"/>). Pressing either throws the boat's
    /// one cabin-light state (<see cref="CabinLightSwitchboard"/>).
    ///
    /// <para><b>No text at the helm.</b> This is an ordinary interactable on the shared chooser: a lone
    /// switch in reach is pressed with the interact button, and its verb is only read where the
    /// chooser lists more than one thing to do.</para>
    ///
    /// <para><b>The player's boats only.</b> The switch is fitted on every boat with a cabin (it is part
    /// of her), but it is offered only aboard the player's boat. An NPC boat keeps her lights' automatic
    /// rule; nothing about her changes.</para>
    ///
    /// <para><b>Placement.</b> Built by <see cref="BoatInteriorInstaller"/> on its own child of the root,
    /// on a <see cref="HullLocalAnchor"/> at the switch's hull-local point, exactly as a bunk or a door
    /// is. Everything read on the interaction query path is a field or a constant: no allocation.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CabinLightSwitch : MonoBehaviour, IInteractable
    {
        /// <summary>The chooser's verb when the press will turn the cabin lights on.</summary>
        public const string VerbOn = "Cabin lights on";

        /// <summary>The chooser's verb when the press will turn the cabin lights off.</summary>
        public const string VerbOff = "Cabin lights off";

        private string _id = "fixture.boat.light_switch";
        private CabinLightSwitchboard _board;
        private BoatInterior _cabin;
        private CabinLightSwitchRole _role = CabinLightSwitchRole.Helm;
        private Vector3 _localMetres;

        /// <summary>What this switch is mounted next to.</summary>
        public CabinLightSwitchRole Role => _role;

        /// <summary>Its hull-local mounting point, metres.</summary>
        public Vector3 LocalMetres => _localMetres;

        /// <summary>The board it throws.</summary>
        public CabinLightSwitchboard Board => _board;

        public string Id => _id;

        public string VerbLabel
            => _board != null && _board.Setting == CabinLightSwitchSetting.On ? VerbOff : VerbOn;

        public Vector2 WorldPosition => transform.position;

        public float ReachMeters
        {
            get
            {
                GameConfig config = GameServices.Config;
                return config != null ? config.CabinLightSwitchReachMetres
                                      : GameConfig.DefaultCabinLightSwitchReachMetres;
            }
        }

        public int Priority => InteractPriority.Fixture;

        public InteractContext Contexts => InteractContext.OnDeck | InteractContext.AtHelm;

        public bool RequiresFacing => false;

        public bool IsAvailable => _board != null && IsOnPlayersBoat && IsOnThisLevel;

        /// <summary>Is the boat this switch is on the player's? An NPC boat's switch is never offered.</summary>
        public bool IsOnPlayersBoat => _board != null && GameServices.Helm.IsPlayersBoat(_board.gameObject);

        /// <summary>
        /// Below decks, only a switch on the level he is standing on (or on no interior level at all,
        /// like the one at the door) is in reach: a flybridge helm switch drawn over the saloon is not
        /// pressed through the deckhead. On deck, reach alone decides.
        /// </summary>
        public bool IsOnThisLevel
        {
            get
            {
                if (_cabin == null || !_cabin.IsInside) return true;
                int level = _cabin.LevelIndexAtHeight(_localMetres.z);
                return level < 0 || level == _cabin.Level;
            }
        }

        public void Configure(CabinLightSwitchboard board, BoatInterior cabin, string id,
                              CabinLightSwitchRole role, Vector3 localMetres)
        {
            _board = board;
            _cabin = cabin;
            _id = string.IsNullOrEmpty(id) ? _id : id;
            _role = role;
            _localMetres = localMetres;
        }

        public void Interact(in InteractActor actor) => Press();

        /// <summary>Throw the switch. Returns whether the cabin lights are now on; false and nothing
        /// thrown when the switch is not on the player's boat.</summary>
        public bool Press()
        {
            if (!IsAvailable) return false;
            return _board.Throw(_id);
        }

        private void OnEnable() => Interactables.Register(this);

        private void OnDisable() => Interactables.Unregister(this);
    }
}
