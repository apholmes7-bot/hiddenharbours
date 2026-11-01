using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App
{
    public sealed partial class ArrivalOpening
    {
        static ArrivalOpening _seaOrigin, _seaLive;
        static SeaTransfer _crossing;
        static bool _seaBooting;
        bool _waitingForSea, _seaJourney, _seaLeg, _transferring, _ownsHourHold;
        SeaTransfer _incoming;

        public bool IsAtSea => _seaLeg && IsRunning;
        public bool WaitingForSea => _waitingForSea;
        public static ArrivalOpening SeaJourney => _seaLive;

        /// <summary>Wire content and the existing door loader before enabling the opening.</summary>
        public void ConfigureSea(ArrivalSeaDef sea, RegionSceneLoader loader)
        {
            _seaOpening = sea;
            _loader = loader;
        }

        public static bool ShouldHopToSea(bool hasRestAnchor, bool alreadyArrived, bool hasSeaDef)
            => hasSeaDef && ShouldPlay(hasRestAnchor, alreadyArrived);

        bool BeginSeaHop()
        {
            if (_loader == null || _seaOpening.SeaRegion == null) return false;
            var clock = GameServices.Clock;
            if (clock is GameClock authored) clock.SeekTo(authored.StartSeconds);
            if (clock != null) clock.IsHeld = true;
            _ownsHourHold = clock != null;
            _waitingForSea = true;
            _seaOrigin = this;
            _seaBooting = true;
            GameServices.OpeningCinematicRunning = true;
            EventBus.Publish(new RegionTravelCover(true));
            GameServices.PendingArrivalKey = _seaOpening.OpeningArrivalKey;
            if (_loader.Travel(_seaOpening.SeaRegion)) return true;
            GameServices.PendingArrivalKey = null;
            _waitingForSea = _seaBooting = false;
            _seaOrigin = null;
            ReleaseTheHour();
            GameServices.OpeningCinematicRunning = false;
            EventBus.Publish(new RegionTravelCover(false));
            return false;
        }

        /// <summary>Capture before the source region disables, without moving either body.</summary>
        internal static void CaptureSeaCrossing(Scene previous, Scene next, string key)
        {
            var sea = _seaLive;
            if (sea == null || !sea._seaLeg || sea.gameObject.scene != previous || sea._boatRoot == null
                || sea._seaOpening.HomeRegion == null || next.name != sea._seaOpening.HomeRegion.SceneName
                || key != sea._seaOpening.HomeArrivalKey) return;
            var body = sea._boat.GetComponent<Rigidbody2D>();
            _crossing = new SeaTransfer
            {
                Heading = ArrivalPilot.HeadingOf(sea._boatRoot),
                Velocity = body.linearVelocity,
                AngularVelocity = body.angularVelocity,
                Below = sea.IsBelowDecks,
                DoorOpen = sea.CabinDoor != null && sea.CabinDoor.IsOpen,
                DeckPoint = sea._deckWalk != null && sea._deckWalk.IsSeated
                    ? sea._deckWalk.LocalPosition : sea._passengerDeckOffset,
                SolePoint = sea.CabinLocalPosition,
                Facing = sea.IsBelowDecks ? sea._cabin.HeadingDegrees
                    : sea._deckWalk != null ? sea._deckWalk.HeadingDegrees(sea.DrawnHeadingDegrees())
                    : sea.DrawnHeadingDegrees(),
                Pace = sea.IsBelowDecks ? sea._cabin.PaceMetresPerSecond
                    : sea._deckWalk != null ? sea._deckWalk.PaceMetresPerSecond : 0f,
                Gait = sea.IsBelowDecks ? sea._cabin.SpeedMetresPerSecond
                    : sea._deckWalk != null ? sea._deckWalk.SpeedMetresPerSecond : 0f,
                PlayerWasSimulated = sea._playerWasSimulated,
            };
            sea._transferring = true;
            sea._ownsHourHold = false;
        }

        /// <summary>The coordinator calls after activating the destination, before ordinary placement.</summary>
        internal static bool ReceiveSeaJourney(Scene next, string key, RegionAnchor anchor)
        {
            if (_seaOrigin == null || anchor == null) return false;
            if (_seaBooting && key == _seaOrigin._seaOpening.OpeningArrivalKey
                && next.name == _seaOrigin._seaOpening.SeaRegion.SceneName)
            {
                // The source has not spawned a hull: its root contains only its authored component.
                var copy = Instantiate(_seaOrigin.gameObject);
                copy.name = "SeaArrivalOpening";
                SceneManager.MoveGameObjectToScene(copy, next);
                var opening = copy.GetComponent<ArrivalOpening>();
                opening._waitingForSea = false;
                opening._seaJourney = opening._seaLeg = true;
                opening._startBelowDecks = false;
                opening._ownsHourHold = _seaOrigin._ownsHourHold;
                opening._route = new[] { (Vector2)anchor.ArrivalPointFor(key).position,
                                         opening._seaOpening.ExitTarget };
                _seaOrigin._waitingForSea = false;
                _seaOrigin._ownsHourHold = false;
                _seaBooting = false;
                _seaLive = opening;
                copy.SetActive(true);
                bool begun = opening.BeginLocalArrival();
                if (begun) opening.PlaceLensOnPassenger();
                EventBus.Publish(new RegionTravelCover(false));
                return begun;
            }
            if (_crossing == null || next.name != _seaOrigin._seaOpening.HomeRegion.SceneName
                || key != _seaOrigin._seaOpening.HomeArrivalKey) return false;
            var old = _seaLive;
            var home = _seaOrigin;
            home._seaJourney = true;
            home._seaLeg = false;
            home._startBelowDecks = false;
            home._ownsHourHold = true;
            home._incoming = _crossing;
            home._incoming.ArrivalPosition = anchor.ArrivalPointFor(key).position;
            _crossing = null;
            _seaLive = home;
            bool resumed = home.BeginLocalArrival();
            if (resumed) home.PlaceLensOnPassenger();
            if (old != null) Destroy(old.gameObject);
            return resumed;
        }

        void PlaceLensOnPassenger()
        {
            var follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow == null) return;
            var subject = follow.Target != null ? follow.Target : _boatRoot;
            if (subject == null) return;
            Vector3 at = subject.position;
            Vector3 lens = follow.transform.position;
            follow.transform.position = new Vector3(at.x, at.y, lens.z);
            // A zero step re-seeds the follow's last target, so its next frame does not read the hop as speed.
            follow.TickFollow(0f);
        }

        void RestorePassengerAcrossSeam()
        {
            if (_incoming == null) return;
            if (_cabin != null && _cabin.Door != null) _cabin.Door.SetOpen(_incoming.DoorOpen);
            if (_incoming.Below && _cabin != null)
                _cabin.SeedFromDeck(_incoming.SolePoint, _incoming.Pace, _incoming.Gait, _incoming.Facing);
            else
            {
                _passengerDeckOffset = _incoming.DeckPoint;
                _deckWalk?.SeedFromCabin(_incoming.DeckPoint, _incoming.Pace, _incoming.Gait,
                    _incoming.Facing, DrawnHeadingDegrees(), _cabin != null ? _cabin.Door : null);
            }
        }

        /// <summary>Resume inside the first fairway leg; all subsequent authored marks stay in order.</summary>
        public static Vector2[] ContinuationRoute(Vector2[] route, Vector2 arrival)
        {
            if (route == null || route.Length < 2) return new[] { arrival };
            var continuation = (Vector2[])route.Clone();
            continuation[0] = arrival;
            return continuation;
        }

        void ReleaseTheHour()
        {
            if (!_ownsHourHold) return;
            _ownsHourHold = false;
            if (GameServices.Clock != null) GameServices.Clock.IsHeld = false;
        }

        void OnDestroy()
        {
            if (_seaLive == this) _seaLive = null;
            if (_seaOrigin != this) return;
            _seaOrigin = null;
            _seaBooting = false;
            _crossing = null;
            EventBus.Publish(new RegionTravelCover(false));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSeaJourney()
        {
            _seaOrigin = _seaLive = null;
            _seaBooting = false;
            _crossing = null;
        }

        sealed class SeaTransfer
        {
            public Vector2 ArrivalPosition, Velocity, DeckPoint, SolePoint;
            public float Heading, AngularVelocity, Facing, Pace, Gait;
            public bool Below, DoorOpen, PlayerWasSimulated;
        }
    }
}
