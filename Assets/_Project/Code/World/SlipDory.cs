using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The broken dory on the St Peters slip.</b> She lies there from a new game's first morning, she is
    /// her owner's, and pressing on her shows her price and buys her if his purse allows
    /// (<see cref="SlipBoatSale"/>). After that she is his, still broken, still on the slip, and this
    /// candidate stands aside: the mending station on the same prop mends and launches her, and a launch
    /// (<see cref="SlipDoryLaunched"/>) hides the prop.
    ///
    /// <para><b>Shown only in a game that has her.</b> Her body is drawn and pressable only while the save
    /// says the dory lies on the slip (<see cref="SlipDoryStory"/>). An old save never set that flag, so
    /// it sees no second dory and no sale; its own boat is where it always was.</para>
    ///
    /// <para><b>No allocation per frame.</b> The two labels are composed when the state is read (start and
    /// each game load), never in the verb's getter, which the interact prompt reads every frame.</para>
    /// </summary>
    public sealed class SlipDory : MonoBehaviour, IInteractable
    {
        [Tooltip("The sale: price, seller, hull, labels and lines.")]
        [SerializeField] private SlipBoatSaleDef _sale;

        [Tooltip("The drawn hull and its collider. Shown only while the save has her on the slip.")]
        [SerializeField] private GameObject _body;

        private string _buyLabel;
        private string _ownedLabel;
        private bool _onTheSlip;
        private bool _registered;
        private bool _subscribed;

        /// <summary>The sale this dory carries (tests and tools).</summary>
        public SlipBoatSaleDef Sale => _sale;

        /// <summary>True while the save has her on the slip and she is drawn.</summary>
        public bool IsShown => _onTheSlip;

        /// <summary>Wire by hand (tests); a prefab wires the two fields in the inspector.</summary>
        public void Configure(SlipBoatSaleDef sale, GameObject body)
        {
            _sale = sale;
            _body = body;
            Refresh();
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                EventBus.Subscribe<GameLoaded>(OnGameLoaded);
                EventBus.Subscribe<SlipDoryLaunched>(OnLaunched);
                _subscribed = true;
            }
            Refresh();
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            if (_subscribed)
            {
                EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
                EventBus.Unsubscribe<SlipDoryLaunched>(OnLaunched);
                _subscribed = false;
            }
            SetRegistered(false);
        }

        private void OnGameLoaded(GameLoaded e) => Refresh();

        // Launched: the flag is gone, so Refresh hides the prop and takes her off the interact list.
        private void OnLaunched(SlipDoryLaunched e) => Refresh();

        /// <summary>Re-read the save: is she on the slip, and whose is she? Shows or hides her body and
        /// recomposes the labels. Called on enable, at start, on each game load and after a press.</summary>
        public void Refresh()
        {
            _onTheSlip = _sale != null && SlipDoryStory.IsOnTheSlip(GameServices.Save);
            if (_body != null && _body.activeSelf != _onTheSlip) _body.SetActive(_onTheSlip);
            if (_sale != null)
            {
                _buyLabel = _sale.WithPrice(_sale.BuyLabel);
                _ownedLabel = _sale.WithPrice(_sale.OwnedLabel);
            }
            SetRegistered(_onTheSlip && isActiveAndEnabled);
        }

        private void SetRegistered(bool on)
        {
            if (on == _registered) return;
            if (on) Interactables.Register(this);
            else Interactables.Unregister(this);
            _registered = on;
        }

        // ---- IInteractable -------------------------------------------------------------------

        public string Id => _sale != null ? _sale.Id : string.Empty;

        public string VerbLabel => SlipBoatSale.IsHis(_sale, GameServices.Save) ? _ownedLabel : _buyLabel;

        public Vector2 WorldPosition => transform.position;

        public float ReachMeters => _sale != null ? _sale.ReachMeters : 0f;

        public int Priority => InteractPriority.Fixture;

        public InteractContext Contexts => InteractContext.OnFoot;

        public bool RequiresFacing => false;

        // Once she is his, the work on her is the mending station's verb on the same prop; offering the
        // look-over beside it would put a chooser in front of every held press.
        public bool IsAvailable => _onTheSlip && !SlipBoatSale.IsHis(_sale, GameServices.Save);

        public void Interact(in InteractActor actor) => Press();

        /// <summary>The press: try the sale, then think the line that goes with how it came out.</summary>
        public SlipBoatSaleResult Press()
        {
            SlipBoatSaleResult result = SlipBoatSale.TryBuy(_sale, GameServices.Save, GameServices.Wallet);
            Think(SlipBoatSale.LineFor(_sale, result));
            return result;
        }

        private void Think(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Transform player = GameServices.PlayerTransform;
            EventBus.Publish(new AmbientSpeechRequested(
                AmbientSpeechId.Next(), player, new Vector3(0f, InnerVoiceDirector.BubbleHeightMetres, 0f),
                null, null, line, _sale.VoiceId, AmbientSpeechKind.InnerVoice));
        }
    }
}
