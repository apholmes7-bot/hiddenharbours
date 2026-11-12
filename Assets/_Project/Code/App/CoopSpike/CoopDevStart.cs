using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
        private CoopLaunchOptions _options;
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
            if (Application.productName != SpikeProduct || !Array.Exists(args,
                arg => arg.StartsWith("--coop-spike-", StringComparison.Ordinal))) return;
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            string error = null;
            CoopLaunchOptions options = null;
            if (settings == null || !CoopLaunchOptions.TryParse(args, settings.Port, out options, out error))
            {
                Debug.LogError("[Coop] Invalid launch: " + (settings == null ? "settings missing" : error));
                Application.Quit(2);
                return;
            }
            var root = new GameObject("CoopSpikeDevStart");
            DontDestroyOnLoad(root);
            var boot = root.AddComponent<CoopDevStart>();
            boot._options = options;
            boot._client = options.Client;
        }

        private sealed class SessionSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool value) && value;
            public void SetFlag(string key, bool value) { if (!string.IsNullOrEmpty(key)) _flags[key] = value; }
            public void Save() { }
        }

        private void Awake()
        {
            if (Application.productName != SpikeProduct)
                throw new InvalidOperationException("The trial requires its isolated product.");
            // AfterSceneLoad precedes the authored core's Start/load-restore. The isolated
            // product's disk service has opened, but cannot autosave once removed here.
            foreach (var save in FindObjectsByType<SaveService>(FindObjectsSortMode.None))
                DestroyImmediate(save);
            GameServices.Save = new SessionSave();
            Application.runInBackground = true;
            Debug.Log("[Coop] Fresh memory-only session; no session persistence.");
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
            _options ??= new CoopLaunchOptions(false, "127.0.0.1", _settings.Port);
            transport.SetConnectionData(_options.Address, _options.Port, _client ? null : "0.0.0.0");
            _manager = core.AddComponent<NetworkManager>();
            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, PlayerPrefab = _settings.PlayerPrefab,
                EnableSceneManagement = false, ConnectionApproval = true
            };
            _manager.AddNetworkPrefab(_settings.PlayerPrefab);
            _manager.ConnectionApprovalCallback = (request, response) =>
            {
                response.Approved = _manager.ConnectedClientsIds.Count < 2;
                response.CreatePlayerObject = response.Approved;
                response.Reason = response.Approved ? string.Empty : "This trial supports two players.";
            };
            _manager.OnClientConnectedCallback += OnConnected;
            _manager.OnClientDisconnectCallback += OnDisconnected;
            bool started = _client ? _manager.StartClient() : _manager.StartHost();
            if (!started) throw new InvalidOperationException("The local transport did not start.");
            Ready = true;
            Debug.Log($"[Coop] {(_client ? "Joining" : "Hosting")} {_options.Address}:{_options.Port} UDP; scene={_settings.Scene}");
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("HH_COOP_PROOF_DIR")))
                gameObject.AddComponent<CoopProcessProof>().Boot = this;
        }

        // Also used by the in-process fixture: no save, opening or second service context is created.
        public static CarriableTool PrepareLocal(CoopSpikeSettings settings, BoatController boat,
            PlayerWalkController player, ControlSwitcher switcher, bool client)
        {
            var position = settings.HostStart + (client ? settings.ClientOffset : Vector2.zero);
            // The persistent boat came from the elevated St Peters landing. Its picture's
            // old datum does not describe these open-water spawn coordinates.
            boat.GetComponent<HullTideRide>()?.Configure(settings.PictureDatumElevation);
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

        private void OnConnected(ulong clientId)
        {
            Debug.Log($"[Coop] Connected participant {clientId}; local={_manager.LocalClientId}");
            StartCoroutine(BindReplicas());
        }
        private void OnDisconnected(ulong clientId)
        {
            Debug.LogWarning($"[Coop] Disconnected participant {clientId}: {_manager.DisconnectReason}");
        }
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
            if (_manager != null)
            {
                _manager.OnClientConnectedCallback -= OnConnected;
                _manager.OnClientDisconnectCallback -= OnDisconnected;
            }
        }
    }

    /// <summary>Strict network endpoint parsing without an editor, scene or socket.</summary>
    public sealed class CoopLaunchOptions
    {
        public const string AddressArgument = "--coop-spike-address";
        public const string PortArgument = "--coop-spike-port";
        public bool Client { get; }
        public string Address { get; }
        public ushort Port { get; }
        public CoopLaunchOptions(bool client, string address, ushort port)
        { Client = client; Address = address; Port = port; }

        public static bool TryParse(string[] args, ushort defaultPort, out CoopLaunchOptions options, out string error)
        {
            options = null; error = "Choose exactly one host/client role and a valid IPv4 endpoint.";
            if (!CoopDevStart.ShouldStart(args, CoopDevStart.SpikeProduct) || defaultPort == 0) return false;
            bool client = Array.IndexOf(args, CoopDevStart.ClientArgument) >= 0;
            string address = "127.0.0.1";
            ushort port = defaultPort;
            var seen = new HashSet<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (!key.StartsWith("--coop-spike-", StringComparison.Ordinal)) continue;
                if (!seen.Add(key)) return false;
                if (key == CoopDevStart.HostArgument || key == CoopDevStart.ClientArgument) continue;
                if (key != AddressArgument && key != PortArgument) return false;
                if (++i >= args.Length) return false;
                if (key == PortArgument)
                {
                    if (!ushort.TryParse(args[i], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out port) || port == 0) return false;
                }
                else
                {
                    string[] octets = args[i].Split('.');
                    if (octets.Length != 4) return false;
                    foreach (string octet in octets)
                        if (!byte.TryParse(octet, System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out _)) return false;
                    if (!IPAddress.TryParse(args[i], out var ip) || ip.AddressFamily != AddressFamily.InterNetwork
                        || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast) || ip.GetAddressBytes()[0] >= 224) return false;
                    address = ip.ToString();
                }
            }
            if (client && !seen.Contains(AddressArgument)) { error = "Client requires --coop-spike-address with the host IPv4 address."; return false; }
            options = new CoopLaunchOptions(client, address, port); error = null; return true;
        }
    }
}
