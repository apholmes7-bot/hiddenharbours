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
    /// <summary>Explicit opt-in trial; ordinary play never creates a network session.</summary>
    public sealed class CoopDevStart : MonoBehaviour
    {
        public const string ArgumentPrefix = "--coop-spike-";
        public const string HostArgument = "--coop-spike-host";
        public const string ClientArgument = "--coop-spike-client";
        // The trial build is its own product, so it keeps its files apart from the ordinary game's.
        public const string TrialProduct = "Two PC Sea Trial";
        private NetworkManager _manager;
        private BoatController _boat;
        private PlayerWalkController _player;
        private ControlSwitcher _switcher;
        private bool _client;
        private CoopSpikeSettings _settings;
        private CoopLaunchOptions _options;
        private Vector2 _berth;
        private bool _placed;
        private bool _closing;
        private float _joinDeadline;
        public bool Ready { get; private set; }
        public NetworkManager Manager => _manager;
        public CoopLaunchOptions Options => _options;
        /// <summary>The starting place the host gave this participant, or <see cref="CoopSlots.None"/>.</summary>
        public int LocalSlot { get; private set; } = CoopSlots.None;
        /// <summary>A plain sentence for the trial panel. Changes only on a session event.</summary>
        public string Status { get; private set; } = "Starting.";
        public int StatusVersion { get; private set; }

        public static bool ShouldStart(string[] args)
        {
            if (args == null) return false;
            bool host = Array.IndexOf(args, HostArgument) >= 0;
            bool client = Array.IndexOf(args, ClientArgument) >= 0;
            return host != client;
        }

        public static bool HasTrialArgument(string[] args) => args != null && Array.Exists(args,
            arg => arg != null && arg.StartsWith(ArgumentPrefix, StringComparison.Ordinal));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            StartFromArguments(System.Environment.GetCommandLineArgs());
        }

        // This is also the solo regression seam: return before loading any trial assets,
        // creating any objects or replacing services unless a trial option was supplied.
        public static CoopDevStart StartFromArguments(string[] args)
        {
            if (!HasTrialArgument(args)) return null;
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            string error = null;
            CoopLaunchOptions options = null;
            if (settings == null || !CoopLaunchOptions.TryParse(args, settings.Port, out options, out error))
            {
                Debug.LogError("[Coop] Invalid launch: " + (settings == null ? "settings missing" : error));
                Application.Quit(2);
                return null;
            }
            var root = new GameObject("CoopSpikeDevStart");
            DontDestroyOnLoad(root);
            var boot = root.AddComponent<CoopDevStart>();
            boot._options = options;
            boot._client = options.Client;
            return boot;
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
            // AfterSceneLoad precedes the authored core's Start/load-restore. The disk
            // service has opened read-only so far, and cannot autosave once removed here.
            foreach (var save in FindObjectsByType<SaveService>(FindObjectsSortMode.None))
                DestroyImmediate(save);
            GameServices.Save = new SessionSave();
            // AfterSceneLoad is before the first physics tick. This trial starts aboard,
            // so the authored on-foot spawn must not act while we await core Start.
            _player = FindFirstObjectByType<PlayerWalkController>();
            if (_player != null) _player.enabled = false;
            Application.runInBackground = true;
            Application.wantsToQuit += OnWantsToQuit;
            Debug.Log("[Coop] Fresh memory-only session; no session persistence.");
        }

        private IEnumerator Start()
        {
            _settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            if (_settings == null || _settings.PlayerPrefab == null)
                throw new InvalidOperationException("Build the trial settings before a dev start.");
            yield return null; // the authored starting core finishes its Start methods
            if (!GameServices.Ready)
                throw new InvalidOperationException("Start the trial from the authored boot scene so its persistent core exists.");
            foreach (var opening in FindObjectsByType<ArrivalOpening>(FindObjectsSortMode.None))
                opening.enabled = false;
            ShellFlow.ContinueGame();
            _boat = FindFirstObjectByType<BoatController>();
            _player = FindFirstObjectByType<PlayerWalkController>();
            _switcher = FindFirstObjectByType<ControlSwitcher>();
            if (_boat == null || _player == null || _switcher == null || GameServices.Clock == null)
                throw new InvalidOperationException("The trial needs the authored persistent boat/player core.");
            if (SceneManager.GetActiveScene().name != _settings.Scene)
            {
                // Board first: a region without its own core carries the authored one across the hop.
                TakeHelm(_boat, _player, _switcher);
                yield return SceneManager.LoadSceneAsync(_settings.Scene, LoadSceneMode.Single);
            }
            yield return null;
            // The region's ordinary arrival positioning has now finished. Its berth is the
            // origin every participant's starting place is measured from.
            _berth = _boat.transform.position;
            PrepareLocal(_settings, _boat, _player, _switcher);

            // The opt-in bootstrap owns the entire session. Destroying it must also
            // destroy the transport and manager, even when the gameplay core survives.
            var transport = gameObject.AddComponent<UnityTransport>();
            _options ??= new CoopLaunchOptions(false, "127.0.0.1", _settings.Port);
            transport.SetConnectionData(_options.Address, _options.Port, _client ? null : "0.0.0.0");
            _manager = gameObject.AddComponent<NetworkManager>();
            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, PlayerPrefab = _settings.PlayerPrefab,
                EnableSceneManagement = false, ConnectionApproval = true
            };
            _manager.AddNetworkPrefab(_settings.PlayerPrefab);
            int capacity = _settings.Capacity;
            CoopBoatReplica.SlotCapacity = capacity;
            _manager.ConnectionApprovalCallback = (request, response) =>
            {
                response.Approved = CoopSlots.HasRoom(_manager.ConnectedClientsIds.Count, capacity);
                response.CreatePlayerObject = response.Approved;
                response.Reason = response.Approved ? string.Empty : "This session is full.";
            };
            _manager.OnClientConnectedCallback += OnConnected;
            _manager.OnClientDisconnectCallback += OnDisconnected;
            bool started = _client ? _manager.StartClient() : _manager.StartHost();
            if (!started) throw new InvalidOperationException("The local transport did not start.");
            Ready = true;
            _joinDeadline = _client ? Time.realtimeSinceStartup + _settings.JoinTimeoutSeconds : 0f;
            Say(_client
                ? $"Looking for the host at {_options.Address}, port {_options.Port}."
                : HostLine(1, capacity));
            Debug.Log($"[Coop] {(_client ? "Joining" : "Hosting")} {_options.Address}:{_options.Port} UDP; scene={_settings.Scene}; capacity={capacity}");
            gameObject.AddComponent<CoopTrialPanel>().ShowSession(this);
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(CoopProcessProof.DirectoryVariable)))
                gameObject.AddComponent<CoopProcessProof>().Boot = this;
        }

        private void Update()
        {
            if (_joinDeadline <= 0f || Time.realtimeSinceStartup < _joinDeadline) return;
            _joinDeadline = 0f;
            if (_manager != null && !_manager.IsConnectedClient)
                Say($"No answer from {_options.Address}, port {_options.Port}. Check the address, " +
                    "that the host started first, and that the host's firewall allows this game.");
        }

        // Also used by the in-process fixture: no save, opening or second service context is created.
        public static CarriableTool PrepareLocal(CoopSpikeSettings settings, BoatController boat,
            PlayerWalkController player, ControlSwitcher switcher)
        {
            TakeHelm(boat, player, switcher);
            return EquipRod(settings, player);
        }

        private static void TakeHelm(BoatController boat, PlayerWalkController player, ControlSwitcher switcher)
        {
            if (switcher.Mode == ControlMode.OnFoot)
            {
                player.transform.position = boat.transform.position;
                if (!switcher.TryInteract())
                    throw new InvalidOperationException("The dev start could not board its own boat.");
            }
            if (switcher.Mode == ControlMode.OnDeck)
            {
                player.transform.position = switcher.HelmWorldPosition;
                if (!switcher.TryInteract())
                    throw new InvalidOperationException("The dev start could not take its own helm.");
            }
            if (switcher.Mode != ControlMode.Aboard)
                throw new InvalidOperationException("The dev start must finish aboard at the local helm.");
        }

        /// <summary>Put this participant's own boat at the starting place the host gave him.</summary>
        public static void PlaceBoat(BoatController boat, Vector2 position)
        {
            Vector3 carried = new Vector3(position.x, position.y, boat.transform.position.z);
            boat.transform.position = carried;
            var body = boat.GetComponent<Rigidbody2D>();
            if (body == null) return;
            body.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;
        }

        private static CarriableTool EquipRod(CoopSpikeSettings settings, PlayerWalkController player)
        {
            if (settings.Rod == null) throw new InvalidOperationException("No authored rod in trial settings.");
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
            _joinDeadline = 0f;
            Say(_manager.IsHost
                ? HostLine(_manager.ConnectedClientsIds.Count, _settings.Capacity)
                : $"Joined the host at {_options.Address}. You share his clock, tide and weather.");
            StartCoroutine(BindReplicas());
        }

        private void OnDisconnected(ulong clientId)
        {
            string reason = _manager.DisconnectReason;
            Debug.LogWarning($"[Coop] Disconnected participant {clientId}: {reason}");
            if (_manager.IsHost && clientId != _manager.LocalClientId)
                StartCoroutine(SayHostLineNextFrame());
            else if (!_manager.IsHost)
                Say(string.IsNullOrEmpty(reason)
                    ? "The session has ended. Close this window and start again."
                    : "The host said: " + reason);
        }

        // The list of participants is settled by the frame after a departure, not during it.
        private IEnumerator SayHostLineNextFrame()
        {
            yield return null;
            if (_manager != null && _manager.IsListening)
                Say(HostLine(_manager.ConnectedClientsIds.Count, _settings.Capacity));
        }
        private string HostLine(int connected, int capacity)
        {
            string addresses = string.Join("  or  ", CoopLaunchOptions.LocalAddresses());
            if (addresses.Length == 0) addresses = "(no network address found)";
            return $"Hosting. Your friend types: {addresses}   Port {_options.Port}.   Players {connected} of {capacity}.";
        }

        private void Say(string status)
        {
            Status = status;
            StatusVersion++;
        }

        private IEnumerator BindReplicas()
        {
            yield return null;
            foreach (var replica in FindObjectsByType<CoopBoatReplica>(FindObjectsSortMode.None))
            {
                if (replica.NetworkManager != _manager) continue;
                replica.BindClock(GameServices.Clock, GameServices.Environment.WorldSeed);
                if (!replica.IsOwner) continue;
                replica.BindLocal(_boat.transform, _player.transform,
                    () => _boat.Hull != null ? _boat.Hull.Id : string.Empty,
                    () => string.Empty); // the player's own look is not sent in this trial
                if (!_placed) { _placed = true; StartCoroutine(TakePlace(replica)); }
            }
        }

        // The host answers "which place is yours" per connection. Wait for that answer, then
        // move this participant's own boat there once.
        private IEnumerator TakePlace(CoopBoatReplica replica)
        {
            float end = Time.realtimeSinceStartup + _settings.JoinTimeoutSeconds;
            while (replica != null && replica.Slot.Value == CoopSlots.None && Time.realtimeSinceStartup < end)
                yield return null;
            if (replica == null || replica.Slot.Value == CoopSlots.None)
            {
                Debug.LogWarning("[Coop] The host gave no starting place; staying at the berth.");
                yield break;
            }
            LocalSlot = replica.Slot.Value;
            PlaceBoat(_boat, _berth + _settings.OffsetFor(LocalSlot));
            replica.Jump(_boat.transform.position, _boat.transform.rotation);
            Debug.Log($"[Coop] Participant {replica.OwnerClientId} takes starting place {LocalSlot}.");
        }

        // Closing the window with a live session crashed the earlier trial. Stop the session
        // first, let it finish, then let the application close.
        private bool OnWantsToQuit()
        {
            if (_closing || _manager == null || !_manager.IsListening) return true;
            _closing = true;
            Debug.Log("[Coop] Window closing; stopping the session first.");
            _manager.Shutdown();
            StartCoroutine(QuitWhenStopped());
            return false;
        }

        private IEnumerator QuitWhenStopped()
        {
            float end = Time.realtimeSinceStartup + QuitGraceSeconds;
            while (_manager != null && (_manager.ShutdownInProgress || _manager.IsListening)
                && Time.realtimeSinceStartup < end) yield return null;
            Application.Quit();
        }

        private const float QuitGraceSeconds = 2f;

        private void OnDestroy()
        {
            Ready = false;
            Application.wantsToQuit -= OnWantsToQuit;
            if (_manager != null)
            {
                _manager.OnClientConnectedCallback -= OnConnected;
                _manager.OnClientDisconnectCallback -= OnDisconnected;
                _manager.Shutdown();
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

        /// <summary>The command line that starts a host, or a guest of <paramref name="address"/>.</summary>
        public static string[] Arguments(bool client, string address, ushort port)
        {
            string portText = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return client
                ? new[] { CoopDevStart.ClientArgument, AddressArgument, address ?? string.Empty, PortArgument, portText }
                : new[] { CoopDevStart.HostArgument, PortArgument, portText };
        }

        /// <summary>This computer's own IPv4 addresses on its networks, for the host to read out.</summary>
        public static List<string> LocalAddresses()
        {
            var found = new List<string>();
            try
            {
                foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up
                        || adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        continue;
                    foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                    {
                        var ip = unicast.Address;
                        if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                        string text = ip.ToString();
                        if (!text.StartsWith("169.254.", StringComparison.Ordinal) && !found.Contains(text)) found.Add(text);
                    }
                }
            }
            catch (Exception error) { Debug.LogWarning("[Coop] Could not list network addresses: " + error.Message); }
            return found;
        }

        public static bool TryParse(string[] args, ushort defaultPort, out CoopLaunchOptions options, out string error)
        {
            options = null; error = "Choose exactly one host/client role and a valid IPv4 endpoint.";
            if (!CoopDevStart.ShouldStart(args) || defaultPort == 0) return false;
            bool client = Array.IndexOf(args, CoopDevStart.ClientArgument) >= 0;
            string address = "127.0.0.1";
            ushort port = defaultPort;
            var seen = new HashSet<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (key == null || !key.StartsWith(CoopDevStart.ArgumentPrefix, StringComparison.Ordinal)) continue;
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
                    if (!TryAddress(args[i], out address)) return false;
                }
            }
            if (client && !seen.Contains(AddressArgument)) { error = "Client requires --coop-spike-address with the host IPv4 address."; return false; }
            options = new CoopLaunchOptions(client, address, port); error = null; return true;
        }

        /// <summary>Four plain numbers, a real single computer's address. Names are not accepted.</summary>
        public static bool TryAddress(string text, out string address)
        {
            address = null;
            if (string.IsNullOrEmpty(text)) return false;
            string[] octets = text.Split('.');
            if (octets.Length != 4) return false;
            foreach (string octet in octets)
                if (!byte.TryParse(octet, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out _)) return false;
            if (!IPAddress.TryParse(text, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork
                || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast) || ip.GetAddressBytes()[0] >= 224) return false;
            address = ip.ToString();
            return true;
        }
    }
}
