using System;
using System.Collections;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Explicit branch-only boot; ordinary play never creates a network session.</summary>
    public sealed class CoopDevStart : MonoBehaviour
    {
        public const string HostArgument = "--coop-spike-host";
        public const string ClientArgument = "--coop-spike-client";
        public const string SpikeProduct = "Hidden Harbours Co-op Spike";
        private NetworkManager _manager;
        private BoatController _boat;
        private PlayerWalkController _player;
        private bool _client;
        private CoopSpikeSettings _settings;
        private CharacterCreatorPanel _creator;
        private string _lookJson;

        public static bool ShouldStart(string[] args, string product)
        {
            if (product != SpikeProduct || args == null) return false;
            bool host = Array.IndexOf(args, HostArgument) >= 0;
            bool client = Array.IndexOf(args, ClientArgument) >= 0;
            return host != client;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (!ShouldStart(args, Application.productName)) return;
            var root = new GameObject("CoopSpikeDevStart");
            DontDestroyOnLoad(root);
            root.AddComponent<CoopDevStart>()._client = Array.IndexOf(args, ClientArgument) >= 0;
        }

        private IEnumerator Start()
        {
            _settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            if (_settings == null || _settings.PlayerPrefab == null)
                throw new InvalidOperationException("Build the spike settings before a dev start.");
            if (SceneManager.GetActiveScene().name != _settings.Scene)
            {
                // The dev start replaces the authored opening core, not the isolated save service.
                foreach (var part in FindObjectsByType<PersistentObject>(FindObjectsSortMode.None))
                    Destroy(part.gameObject);
                yield return null;
                yield return SceneManager.LoadSceneAsync(_settings.Scene, LoadSceneMode.Single);
            }
            yield return null; // scene Start methods, including the shell, finish first
            ShellFlow.ContinueGame();
            _boat = FindFirstObjectByType<BoatController>();
            _player = FindFirstObjectByType<PlayerWalkController>();
            var switcher = FindFirstObjectByType<ControlSwitcher>();
            if (_boat == null || _player == null || switcher == null || GameServices.Clock == null)
                throw new InvalidOperationException("The spike needs the authored persistent boat/player core.");
            var position = _settings.HostStart + (_client ? _settings.ClientOffset : Vector2.zero);
            _boat.transform.position = position;
            _boat.GetComponent<Rigidbody2D>().position = position;
            _player.transform.position = _boat.transform.position;
            if (switcher.Mode == ControlMode.OnFoot && !switcher.TryInteract())
                throw new InvalidOperationException("The dev start could not board its own boat.");
            _player.transform.position = switcher.HelmWorldPosition;
            if (switcher.Mode == ControlMode.OnDeck && !switcher.TryInteract())
                throw new InvalidOperationException("The dev start could not take its own helm.");
            var picker = _boat.GetComponent<DevBoatPicker>() ?? _boat.gameObject.AddComponent<DevBoatPicker>();
            picker.Configure(_settings.Roster, _boat, _boat.GetComponent<ShipHold>(), _boat.GetComponent<SpriteRenderer>());
            EquipRod();
            _lookJson = JsonUtility.ToJson(GameServices.Appearance.Committed);
            EventBus.Subscribe<PlayerAppearanceChanged>(OnLookChanged);

            // The manager rides this process's persistent service root, never a remote gameplay clone.
            var core = ((Component)GameServices.Clock).gameObject;
            var transport = core.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", _settings.Port);
            _manager = core.AddComponent<NetworkManager>();
            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, PlayerPrefab = _settings.PlayerPrefab,
                EnableSceneManagement = false
            };
            _manager.AddNetworkPrefab(_settings.PlayerPrefab);
            _manager.OnClientConnectedCallback += OnConnected;
            bool started = _client ? _manager.StartClient() : _manager.StartHost();
            if (!started) throw new InvalidOperationException("The local transport did not start.");
            if (string.IsNullOrEmpty(PlayerName.Of(GameServices.Save?.Current)))
            {
                _creator = gameObject.AddComponent<CharacterCreatorPanel>();
                _creator.Open(new CharacterCreatorModel(CharacterBuilderDef.LoadShipped(),
                    Resources.Load<CharacterWardrobeDef>(CharacterWardrobeDef.ResourcesPath)));
            }
        }

        private void EquipRod()
        {
            if (_settings.Rod == null) throw new InvalidOperationException("No authored rod in spike settings.");
            var hands = _player.GetComponent<CarryHands>() ?? _player.gameObject.AddComponent<CarryHands>();
            var rod = new GameObject("SpikeRod");
            var picture = rod.AddComponent<SpriteRenderer>();
            picture.sprite = _settings.RodSprite;
            var tool = rod.AddComponent<CarriableTool>();
            tool.Configure(_settings.Rod, picture, "tool.coop_spike.rod");
            if (hands.TryPickUp(tool) != CarryRefusal.None)
                throw new InvalidOperationException("The dev start could not equip the authored rod.");
        }

        private void OnConnected(ulong clientId) => StartCoroutine(BindReplicas());
        private IEnumerator BindReplicas()
        {
            yield return null;
            foreach (var replica in FindObjectsByType<CoopBoatReplica>(FindObjectsSortMode.None))
            {
                if (replica.NetworkManager != _manager) continue;
                replica.BindClock(GameServices.Clock, GameServices.Environment.WorldSeed);
                if (replica.IsOwner)
                    replica.BindLocal(_boat.transform, _player.transform,
                        () => _boat.Hull != null ? _boat.Hull.Id : string.Empty,
                        () => _lookJson);
            }
        }
        private void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerAppearanceChanged>(OnLookChanged);
            if (_manager != null) _manager.OnClientConnectedCallback -= OnConnected;
        }
        private void OnLookChanged(PlayerAppearanceChanged change) => _lookJson = JsonUtility.ToJson(change.Recipe);
    }
}
