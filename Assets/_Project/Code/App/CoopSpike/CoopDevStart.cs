using System;
using System.Collections;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
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
        public bool Ready { get; private set; }
        public NetworkManager Manager => _manager;

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
            yield return null; // the authored starting core finishes its Start methods
            if (!GameServices.Ready)
                throw new InvalidOperationException("Start the spike from the authored boot scene so its persistent core exists.");
            foreach (var opening in FindObjectsByType<ArrivalOpening>(FindObjectsSortMode.None))
                opening.enabled = false;
            ShellFlow.ContinueGame();
            if (SceneManager.GetActiveScene().name != _settings.Scene)
            {
                // Water regions contain no replacement core. Carry the authored core across the hop.
                yield return SceneManager.LoadSceneAsync(_settings.Scene, LoadSceneMode.Single);
            }
            yield return null;
            _boat = FindFirstObjectByType<BoatController>();
            _player = FindFirstObjectByType<PlayerWalkController>();
            var switcher = FindFirstObjectByType<ControlSwitcher>();
            if (_boat == null || _player == null || switcher == null || GameServices.Clock == null)
                throw new InvalidOperationException("The spike needs the authored persistent boat/player core.");
            PrepareLocal(_settings, _boat, _player, switcher, _client);

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
            Ready = true;
        }

        // Also used by the in-process fixture: no save, opening or second service context is created.
        public static CarriableTool PrepareLocal(CoopSpikeSettings settings, BoatController boat,
            PlayerWalkController player, ControlSwitcher switcher, bool client)
        {
            var position = settings.HostStart + (client ? settings.ClientOffset : Vector2.zero);
            boat.transform.position = position;
            var body = boat.GetComponent<Rigidbody2D>();
            body.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;
            player.transform.position = boat.transform.position;
            if (switcher.Mode == ControlMode.OnFoot && !switcher.TryInteract())
                throw new InvalidOperationException("The dev start could not board its own boat.");
            player.transform.position = switcher.HelmWorldPosition;
            if (switcher.Mode == ControlMode.OnDeck && !switcher.TryInteract())
                throw new InvalidOperationException("The dev start could not take its own helm.");
            if (switcher.Mode != ControlMode.Aboard)
                throw new InvalidOperationException("The dev start must finish aboard at the local helm.");
            var picker = boat.GetComponent<DevBoatPicker>() ?? boat.gameObject.AddComponent<DevBoatPicker>();
            picker.Configure(settings.Roster, boat, boat.GetComponent<ShipHold>(), boat.GetComponent<SpriteRenderer>());
            return EquipRod(settings, player);
        }

        private static CarriableTool EquipRod(CoopSpikeSettings settings, PlayerWalkController player)
        {
            if (settings.Rod == null) throw new InvalidOperationException("No authored rod in spike settings.");
            var hands = player.GetComponent<CarryHands>() ?? player.gameObject.AddComponent<CarryHands>();
            var rod = new GameObject("SpikeRod");
            var picture = rod.AddComponent<SpriteRenderer>();
            picture.sprite = settings.RodSprite;
            var tool = rod.AddComponent<CarriableTool>();
            tool.Configure(settings.Rod, picture, "tool.coop_spike.rod");
            if (hands.TryPickUp(tool) != CarryRefusal.None)
                throw new InvalidOperationException("The dev start could not equip the authored rod.");
            return tool;
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
                        () => string.Empty); // creator join/persistence integration stays held
            }
        }
        private void OnDestroy()
        {
            if (_manager != null) _manager.OnClientConnectedCallback -= OnConnected;
        }
    }
}
