using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A villager's rod fight</b> (ADR 0053 §11): the line from his rod tip, the splashes, the fish at
    /// the surface and the fish in his hand, drawn with the very pieces the player's
    /// <see cref="RodFightPresenter"/> draws with (<see cref="RodLineStrand"/>, <see cref="RodSplashFlourish"/>,
    /// <see cref="SurfacedFishDrawer"/>, <see cref="LandedCatchHold"/>), one copy of each. Fed by pull from
    /// an <see cref="IAnglerStand"/>; never by the player's <see cref="FishingStateChanged"/>.
    ///
    /// <para><b>A function of the clock.</b> Nothing here accumulates time: every second a piece takes is
    /// the stand's seconds in the phase or an edge time on the figure's clock
    /// (<see cref="IGameClock.FlowSeconds"/>), so two runs at the same clock draw the same picture.</para>
    ///
    /// <para><b>No flourish on a stale edge</b> (review change 9). A splash fires only on an edge between
    /// two consecutive drawn frames: never on the first sample after attaching or after he was hidden, and
    /// never across a clock jump (a sleep skip, a seek). A frame that does not follow the last drawn one
    /// primes the surfaced fish (<see cref="SurfacedFishDrawer.Prime"/>) and forgets her motion.</para>
    ///
    /// <para><b>Where his look comes from.</b> Every number and the splash frames are the stand's
    /// <see cref="AnglerLook"/> (his activity Def); none is copied from the player's presenter. The fish's
    /// rung ladder and the landed fish's mesh table are the shared fish art table's
    /// (<see cref="FishArtLibrary.Shared"/>). His rod is the work prop his figure presenter already draws;
    /// the line leaves its tip (<see cref="CharacterFigurePresenter.WorkPropTipWorld"/>). Not drawn for a
    /// villager: a bobber, a load lean on the rod, the boil. In their place the fish herself is drawn coming to
    /// his bait and under the water through the deep fight, with the schools' swim art
    /// (<see cref="DrawApproach"/>).</para>
    /// </summary>
    [DefaultExecutionOrder(101)]   // after the figure presenter (100) poses him and draws his rod
    public sealed class NpcAnglerPresenter : MonoBehaviour, IAnglerFigure
    {
        /// <summary>How far two clock reads may disagree, in seconds, and still be one frame's step: float
        /// rounding of a phase's seconds against the double clock, not a tunable.</summary>
        private const double EdgeToleranceSeconds = 1e-3;

        private IAnglerStand _stand;
        private CharacterFigurePresenter _figure;
        private SpriteRenderer _bodySr, _fishSr, _splashSr, _approachSr;
        private float _approachHeading;
        private RodLineStrand _strand;
        private RodSplashFlourish _splash;
        private SurfacedFishDrawer _fish;
        private LandedCatchHold _land;

        private bool _drawnLast;
        private int _lastFrame = int.MinValue;
        private double _lastNow;
        private float _lastSeconds;
        private FishingState _last;
        private double _splashEdgeSeconds;
        private Vector2 _lastFar;
        private AnglerLook _look;   // the last look drawn with, so a splash can finish after the session

        /// <summary>True on a frame his line is drawn.</summary>
        public bool DrawsLine { get; private set; }

        /// <summary>The line, once built. For tests and plates.</summary>
        public LineRenderer Line => _strand?.Line;

        /// <summary>The fish at the surface, the splash, and the fish in hand. For tests and plates.</summary>
        public SpriteRenderer FishRenderer => _fishSr;
        public SpriteRenderer SplashRenderer => _splashSr;
        /// <summary>The fish swimming in to his bait, and under the water through the deep fight. For tests and
        /// plates.</summary>
        public SpriteRenderer ApproachRenderer => _approachSr;
        public SurfacedFishDrawer Fish => _fish;
        public LandedCatchHold Land => _land;

        /// <summary>Feed him from <paramref name="stand"/>. A new stand starts with no history.</summary>
        public void Bind(IAnglerStand stand)
        {
            if (!ReferenceEquals(stand, _stand)) _drawnLast = false;
            _stand = stand;
        }

        private void Awake() => EnsurePieces();

        private void EnsurePieces()
        {
            if (_fish != null) return;
            _bodySr = GetComponent<SpriteRenderer>();
            _figure = GetComponent<CharacterFigurePresenter>();
            _fishSr = MakeChild("NpcFishOnTheLine");
            _splashSr = MakeChild("NpcSplash");
            _approachSr = MakeChild("NpcFishComingIn");
            _splash = new RodSplashFlourish(_splashSr);
            _fish = new SurfacedFishDrawer(_fishSr);
            _land = new LandedCatchHold(transform);
        }

        private void OnDisable()
        {
            HideAll();
            _splash?.Stop();
        }

        private void OnDestroy() => _land?.Dispose();

        private void LateUpdate() => Present(Time.frameCount);

        /// <summary>
        /// Draw this frame. Public so tests can step frames in edit mode; <paramref name="frame"/> is the
        /// frame number, which is what "consecutive" is judged by.
        /// </summary>
        public void Present(int frame)
        {
            EnsurePieces();
            double now = GameServices.Clock != null ? GameServices.Clock.FlowSeconds : 0d;
            if (_stand == null || !_stand.TryGetAnglerState(out FishingState s, out float seconds) || !s.IsActive)
            {
                HideInteraction();
                RenderSplash(now, in _look);   // a landing's splash outlives the session's last beat
                _drawnLast = false;
                return;
            }
            AnglerLook look = _stand.Look;
            _look = look;
            Vector2 angler = transform.position;

            // One frame on from the last drawn one, on a clock that ran forward by about that step.
            bool samePhase = s.Phase == _last.Phase;
            bool consecutive = _drawnLast && frame == _lastFrame + 1 && now >= _lastNow
                               && (samePhase ? seconds >= _lastSeconds
                                             : seconds <= now - _lastNow + EdgeToleranceSeconds);

            if (_fish.Resolve(in s, null, null, GameServices.FishBehaviour, look.FallbackJumpPeriodSeconds))
            {
                FishArtLibrary table = FishArtLibrary.Shared;
                _land.Resolve(table != null ? table.CatchArt : null, s.FishId, s.WeightKg);
            }

            Vector2 fishPos = angler + new Vector2(s.FishOffsetX, s.FishOffsetY);
            if (consecutive && !samePhase)
            {
                if (s.Phase == FishingPhase.FightSurface) { _fish.OnSurfaced(); _fish.ResetMotion(fishPos); }
                switch (RodPresenterMath.SplashFor(in _last, in s))
                {
                    case RodSplashEdge.Touchdown: SplashAt(angler + new Vector2(s.CastAimX, s.CastAimY), look, now - seconds); break;
                    case RodSplashEdge.PulledUnder: SplashAt(angler + new Vector2(_last.CastAimX, _last.CastAimY), look, now - seconds); break;
                    case RodSplashEdge.Breaks: SplashAt(fishPos, look, now - seconds); break;
                    case RodSplashEdge.Result: SplashAt(_lastFar, look, now - seconds); break;
                }
            }
            else if (!consecutive)
            {
                _fish.ResetMotion(fishPos);
                if (s.Phase == FishingPhase.FightSurface) _fish.Prime(seconds);
            }

            Draw(in s, seconds, look, angler, frame, now, consecutive);
            DrawApproach(in s, in look, angler, now);
            RenderSplash(now, look);

            _last = s;
            _lastSeconds = seconds;
            _lastNow = now;
            _lastFrame = frame;
            _drawnLast = true;
        }

        private void Draw(in FishingState s, float seconds, in AnglerLook look, Vector2 angler, int frame,
                          double now, bool consecutive)
        {
            bool castPath = s.CastAimX * s.CastAimX + s.CastAimY * s.CastAimY > 1e-4f;
            RodElements show = RodPresenterMath.ElementsFor(s.Phase, castPath);
            bool rod = _figure != null && _figure.WorkPropDrawn;
            Vector2 tip = rod ? _figure.WorkPropTipWorld : angler;

            Vector2 far = tip;
            if ((show & RodElements.FishSurface) != 0)
            {
                float dt = consecutive ? (float)(now - _lastNow) : 0f;
                far = _fish.Render(angler, in s, dt, seconds, seconds, look.FishSecondsPerFrame,
                                   look.FishDartSpeedMps, look.FishSpeedSmoothing01, look.WaterElementOrder,
                                   out bool splashDue, out Vector2 splashAt);
                if (splashDue && consecutive) SplashAt(splashAt, look, now);
            }
            else
            {
                _fishSr.enabled = false;
                if ((show & RodElements.FishShadow) != 0) far = angler + new Vector2(s.FishOffsetX, s.FishOffsetY);
                else if (castPath) far = angler + new Vector2(s.CastAimX, s.CastAimY);
            }

            if ((show & RodElements.HeldFish) != 0)
            {
                GameConfig config = GameServices.Config;
                if (!_land.Wants(config) || !_land.TryDraw(config, _figure != null ? _figure.AshoreFigure : null))
                    _land.Hide();
            }
            else _land.Hide();

            DrawsLine = rod && (show & RodElements.Line) != 0;
            if (DrawsLine)
            {
                RodLineLook line = look.Line;
                _strand ??= new RodLineStrand(transform, look.LineWidthM, look.LineSegments);
                bool sorted = _bodySr != null;
                int order = sorted ? EffectSorting.OrderOver(SortedEffect.RodLine, _bodySr.sortingOrder) : 0;
                FishFeelSettings feel = GameServices.Config != null ? GameServices.Config.FishFeel : FishFeelSettings.Default;
                _strand.Render(tip, far, in s, in line, in feel, RodPresenterMath.FightLoad01(s.RodBend01, s.Tension01),
                               0f, seconds, sorted, order);
                _lastFar = far;
            }
            else _strand?.Hide();
        }

        /// <summary>
        /// The fish coming to his bait (<see cref="AnglerApproachMath"/>) over the last of his wait, held at the
        /// bait through the bite, and under the water along the line through the deep fight; the surfaced fish
        /// takes over at her break. Drawn with the schools' own swim art (<see cref="SwimFishArt"/>); nothing
        /// when the look sets no approach, the stand names no fish, or the species has no swim art.
        /// </summary>
        private void DrawApproach(in FishingState s, in AnglerLook look, Vector2 angler, double now)
        {
            ISwimFishArt art = SwimFishArt.Service;
            var bobber = new Vector2(s.CastAimX, s.CastAimY);
            string id = null;
            Vector2 offset = bobber;
            float heading = _approachHeading, alpha = look.ApproachAlpha01;
            bool show = false;
            if (art != null && look.ApproachSeconds > 0f)
            {
                if (s.Phase == FishingPhase.Waiting)
                {
                    if (_stand is IAnglerApproach coming
                        && coming.TryGetApproach(out id, out float kg, out float toBite))
                        show = AnglerApproachMath.At(in look, bobber, toBite, FishFightSeed.For(id, kg),
                                                     out offset, out heading, out alpha);
                }
                else if (!string.IsNullOrEmpty(s.FishId) && s.Phase == FishingPhase.Bite)
                {
                    id = s.FishId;
                    heading = AnglerApproachMath.ArrivalHeading(in look, bobber, FishFightSeed.For(id, s.WeightKg));
                    show = true;
                }
                else if (!string.IsNullOrEmpty(s.FishId) && s.Phase == FishingPhase.FightDeep)
                {
                    id = s.FishId;
                    offset = new Vector2(s.FishOffsetX, s.FishOffsetY);
                    heading = AnglerApproachMath.HeadingOf(new Vector2(s.FishRunDirX, s.FishRunDirY), _approachHeading);
                    show = true;
                }
            }
            if (!show || !art.TrySwimCell(id, heading, now, out Sprite cell))
            {
                _approachSr.enabled = false;
                return;
            }
            _approachHeading = heading;
            _approachSr.sprite = cell;
            _approachSr.sortingOrder = look.ApproachSortingOrder;
            _approachSr.color = new Color(1f, 1f, 1f, alpha);
            _approachSr.transform.position = new Vector3(angler.x + offset.x, angler.y + offset.y, transform.position.z);
            _approachSr.enabled = true;
        }

        private void SplashAt(Vector2 pos, in AnglerLook look, double edgeSeconds)
        {
            if (_splash.Begin(pos, look.SplashFrames)) _splashEdgeSeconds = edgeSeconds;
        }

        private void RenderSplash(double now, in AnglerLook look)
        {
            if (!_splash.Active || look.SplashFrames == null || look.SplashFrames.Length == 0)
            {
                _splash.Stop();
                _splashSr.enabled = false;
                return;
            }
            bool sorted = _bodySr != null;
            int order = sorted ? EffectSorting.OrderOver(SortedEffect.RodSplash, _bodySr.sortingOrder) : 0;
            _splash.Render((float)(now - _splashEdgeSeconds), look.SplashFrames, look.SplashFps, sorted, order);
        }

        private void HideInteraction()
        {
            DrawsLine = false;
            _strand?.Hide();
            if (_fishSr != null) _fishSr.enabled = false;
            if (_approachSr != null) _approachSr.enabled = false;
            _land?.Hide();
        }

        private void HideAll()
        {
            HideInteraction();
            _splash?.Hide();
        }

        private SpriteRenderer MakeChild(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false;
            return sr;
        }
    }

    /// <summary>
    /// The Player implementation of <see cref="IAnglerPresentationService"/>: adds (or re-binds) one
    /// <see cref="NpcAnglerPresenter"/> on the host. Self-registered before the first scene loads, as Art's
    /// figure service is; a service already set (a test's) is left alone.
    /// </summary>
    public sealed class AnglerPresentationService : IAnglerPresentationService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (AnglerPresentation.Service == null) AnglerPresentation.Service = new AnglerPresentationService();
        }

        public IAnglerFigure Attach(GameObject host, IAnglerStand stand)
        {
            if (host == null || stand == null) return null;
            if (!host.TryGetComponent(out NpcAnglerPresenter presenter))
                presenter = host.AddComponent<NpcAnglerPresenter>();
            presenter.Bind(stand);
            return presenter;
        }
    }
}
