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
        private SpriteRenderer _bodySr, _fishSr, _splashSr, _approachSr, _bedShadowSr;
        private float _approachHeading;
        private Vector2 _landFrom;      // the water point she was last drawn at in the surface fight
        private bool _landFromSet;
        private bool _underDrawn;      // this frame's fish under the water is drawn, at _underWorld
        private Vector2 _underWorld;
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

        /// <summary>Where the line's far end was put on the last drawn frame (her mouth while she is up),
        /// whether or not a rod was there to draw the line from. For tests and plates.</summary>
        public Vector2 LineFarEnd { get; private set; }
        public SpriteRenderer SplashRenderer => _splashSr;
        /// <summary>The fish swimming in to his bait, and under the water through the deep fight. For tests and
        /// plates.</summary>
        public SpriteRenderer ApproachRenderer => _approachSr;
        /// <summary>Her shadow on the bed, in clear shallow water. For tests and plates.</summary>
        public SpriteRenderer BedShadowRenderer => _bedShadowSr;
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
            _bedShadowSr = MakeChild("NpcFishBedShadow");
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
                    case RodSplashEdge.Result: SplashAt(RodPresenterMath.ResultSplashAt(in _last, angler, _lastFar), look, now - seconds); break;
                }
            }
            else if (!consecutive)
            {
                _fish.ResetMotion(fishPos);
                if (s.Phase == FishingPhase.FightSurface) _fish.Prime(seconds);
            }

            DrawApproach(in s, seconds, in look, angler, now);
            Draw(in s, seconds, look, angler, frame, now, consecutive);
            RenderSplash(now, look);

            _last = s;
            _lastSeconds = seconds;
            _lastNow = now;
            _lastFrame = frame;
            _drawnLast = true;
        }

        /// <summary>
        /// LANDED, ON THE LINE: over <see cref="FishFeelSettings.LandLiftTravelSeconds"/> she travels hanging
        /// head-up from the water point where she left it to the rod tip, then hangs there for
        /// <see cref="FishFeelSettings.LandHangSeconds"/>, in front of him (the rod's over-fisher rung), before
        /// she goes to his hand. A landing not seen from the surface (a seek) starts her at the tip.
        /// </summary>
        private bool TryHang(float seconds, in AnglerLook look, Vector2 tip, out Vector2 grip)
        {
            FishFeelSettings feel = GameServices.Config != null ? GameServices.Config.FishFeel : FishFeelSettings.Default;
            grip = tip;
            if (!(seconds < feel.LandLiftTravelSeconds + feel.LandHangSeconds)) return false;
            grip = RodPresenterMath.LiftGrip(_landFromSet ? _landFrom : tip, tip, seconds, feel.LandLiftTravelSeconds);
            int order = _bodySr != null ? _bodySr.sortingOrder + EffectSorting.RodOverFisherRungs : look.WaterElementOrder;
            int scale = Mathf.Max(1, look.FishDrawScale);
            if (!_fish.RenderHanging(grip, seconds, look.FishSecondsPerFrame, order, scale)) return false;
            ScaleFish(scale);
            return true;
        }

        /// <summary>Her renderer at the look's whole-number fish scale, whatever the parent's scale.</summary>
        private void ScaleFish(int scale)
        {
            Vector3 parent = transform.lossyScale;
            _fishSr.transform.localScale = new Vector3(scale / NonZero(parent.x), scale / NonZero(parent.y), 1f);
        }

        private void Draw(in FishingState s, float seconds, in AnglerLook look, Vector2 angler, int frame,
                          double now, bool consecutive)
        {
            bool castPath = s.CastAimX * s.CastAimX + s.CastAimY * s.CastAimY > 1e-4f;
            RodElements show = RodPresenterMath.ElementsFor(s.Phase, castPath);
            bool rod = _figure != null && _figure.WorkPropDrawn;
            Vector2 tip = rod ? _figure.WorkPropTipWorld : angler;

            Vector2 far = tip;
            bool hanging = false;
            if (s.Phase == FishingPhase.FightSurface)
            {
                _landFrom = angler + new Vector2(s.FishOffsetX, s.FishOffsetY);   // where she will leave the water
                _landFromSet = true;
            }
            else if (s.Phase != FishingPhase.Landed) _landFromSet = false;
            if ((show & RodElements.FishSurface) != 0)
            {
                float dt = consecutive ? (float)(now - _lastNow) : 0f;
                // Behind him while she is in the water beyond his feet, in front only short of them (Core
                // EffectSorting.FishOnLineOrder); with no body to sort by, the look's water order.
                int fishOrder = _bodySr != null
                    ? EffectSorting.FishOnLineOrder(_bodySr.sortingOrder, s.FishOffsetY)
                    : look.WaterElementOrder;
                // At his whole-number fish scale, the line still leaves the mouth of the frame drawn.
                int scale = Mathf.Max(1, look.FishDrawScale);
                far = _fish.Render(angler, in s, dt, seconds, seconds, look.FishSecondsPerFrame,
                                   look.FishDartSpeedMps, look.FishSpeedSmoothing01, fishOrder, scale,
                                   out bool splashDue, out Vector2 splashAt);
                if (splashDue && consecutive) SplashAt(splashAt, look, now, _fish.SplashFirstFrame);
                // The break's column and crown carry no fish; the line ends at the water point under them.
                if (s.Phase == FishingPhase.FightSurface && _fish.InBreakColumn(seconds, look.SplashFps,
                                                                                  look.SplashFrames != null ? look.SplashFrames.Length : 0))
                {
                    _fish.Hide();
                    far = angler + new Vector2(s.FishOffsetX, s.FishOffsetY);
                }
                if (_fishSr.enabled) ScaleFish(scale);
            }
            else if (s.Phase == FishingPhase.Landed && TryHang(seconds, in look, tip, out Vector2 grip))
            {
                hanging = true;
                far = grip;
            }
            else
            {
                _fish.Hide();
                if ((show & RodElements.FishShadow) != 0)
                    far = _underDrawn ? _underWorld : angler + new Vector2(s.FishOffsetX, s.FishOffsetY);
                else if (castPath) far = angler + new Vector2(s.CastAimX, s.CastAimY);
            }

            LineFarEnd = far;

            if ((show & RodElements.HeldFish) != 0 && !hanging)
            {
                GameConfig config = GameServices.Config;
                if (!_land.Wants(config) || !_land.TryDraw(config, _figure != null ? _figure.AshoreFigure : null))
                    _land.Hide();
            }
            else _land.Hide();

            DrawsLine = rod && ((show & RodElements.Line) != 0 || hanging);
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
        private void DrawApproach(in FishingState s, float seconds, in AnglerLook look, Vector2 angler, double now)
        {
            _underDrawn = false;
            ISwimFishArt art = SwimFishArt.Service;
            AnglerApproachLook a = look.Approach;
            var bobber = new Vector2(s.CastAimX, s.CastAimY);
            string id = null;
            var pose = new ApproachPose { Offset = bobber, HeadingRadians = _approachHeading, FadeIn01 = 1f };
            bool show = false;
            if (art != null && a.Seconds > 0f)
            {
                float roll = art.PoseSeconds(SwimFishPose.Roll);
                if (s.Phase == FishingPhase.Waiting)
                {
                    if (_stand is IAnglerApproach coming && coming.TryGetApproach(out id, out float kg, out float toBite))
                        show = AnglerApproachMath.At(in a, bobber, toBite, FishFightSeed.For(id, kg), roll, out pose);
                }
                else if (!string.IsNullOrEmpty(s.FishId) && s.Phase == FishingPhase.Bite)
                {
                    id = s.FishId;
                    pose = AnglerApproachMath.AtTheBait(in a, bobber, FishFightSeed.For(id, s.WeightKg), seconds);
                    show = true;
                }
                else if (!string.IsNullOrEmpty(s.FishId) && s.Phase == FishingPhase.FightDeep)
                {
                    // Hooked: she thrashes along the line, drawn out from the bait to where the fight has her so
                    // the strike never jumps (the line follows her: see Draw).
                    id = s.FishId;
                    float blend = a.HookBlendSeconds > 0f ? Mathf.SmoothStep(0f, 1f, seconds / a.HookBlendSeconds) : 1f;
                    pose.Offset = Vector2.Lerp(bobber, new Vector2(s.FishOffsetX, s.FishOffsetY), blend);
                    pose.HeadingRadians = AnglerApproachMath.HeadingOf(new Vector2(s.FishRunDirX, s.FishRunDirY),
                                                                        _approachHeading);
                    pose.Pose = SwimFishPose.Thrash;
                    pose.PoseSeconds = seconds;
                    show = true;
                }
            }
            if (!show || !art.TrySwimCell(id, pose.Pose, pose.HeadingRadians, pose.PoseSeconds, out Sprite cell))
            {
                _approachSr.enabled = false;
                _bedShadowSr.enabled = false;
                return;
            }

            Vector2 world = angler + pose.Offset;
            float depth = WaterDepthAt(world);
            float water = a.DepthFadeM > 0f ? Mathf.Clamp01(depth / a.DepthFadeM) : 0f;
            float alpha = a.Alpha01 * pose.FadeIn01 * Mathf.Lerp(1f, a.DeepAlpha01, water);
            int scale = Mathf.Max(1, look.FishDrawScale);

            _approachHeading = pose.HeadingRadians;
            _approachSr.sprite = cell;
            _approachSr.sortingOrder = a.SortingOrder;
            // The body is drawn darker so her shape reads against the water; the flank flash (the roll, and the
            // thrash, whose frames flash her pale side) is drawn as baked.
            Color tint = pose.Pose == SwimFishPose.Roll || pose.Pose == SwimFishPose.Thrash ? Color.white : a.BodyTint;
            _approachSr.color = new Color(tint.r, tint.g, tint.b, alpha);
            Place(_approachSr, world, cell, scale);
            _approachSr.enabled = true;
            _underDrawn = true;
            _underWorld = Snap(world, cell, scale);

            // Her shadow on the bed, in clear shallow water: thrown away from her by the water under her.
            if (a.BedShadowMaxDepthM > 0f && depth < a.BedShadowMaxDepthM
                && art.TrySwimCell(id, SwimFishPose.Shadow, pose.HeadingRadians, pose.PoseSeconds, out Sprite shade))
            {
                float strength = 1f - Mathf.Clamp01(Mathf.Max(0f, depth) / a.BedShadowMaxDepthM);
                Color c = a.BedShadowColor;
                _bedShadowSr.sprite = shade;
                _bedShadowSr.sortingOrder = a.SortingOrder - 1;
                _bedShadowSr.color = new Color(c.r, c.g, c.b, c.a * strength * pose.FadeIn01);
                Place(_bedShadowSr, world + a.BedShadowOffsetPerM * Mathf.Max(0f, depth), shade, scale);
                _bedShadowSr.enabled = true;
            }
            else _bedShadowSr.enabled = false;
        }

        /// <summary>The water over the bed at <paramref name="world"/> now (m), the game's one depth rule; 0 with no
        /// tide or ground service (thin water: drawn plainly).</summary>
        private static float WaterDepthAt(Vector2 world)
        {
            IEnvironmentService env = GameServices.Environment;
            ITidalTerrain ground = GameServices.TidalTerrain;
            IGameClock clock = GameServices.Clock;
            if (env == null || ground == null || clock == null) return 0f;
            return TidalExposure.WaterDepth(env.WaterLevelAt(clock.TotalSeconds), ground.ElevationAt(world));
        }

        /// <summary>Put <paramref name="sr"/> at <paramref name="world"/>, on its art's pixel grid, at a whole-number
        /// scale of its art (so every art pixel is a square block; no resampling).</summary>
        private void Place(SpriteRenderer sr, Vector2 world, Sprite cell, int scale)
        {
            Vector2 at = Snap(world, cell, scale);
            sr.transform.position = new Vector3(at.x, at.y, transform.position.z);
            Vector3 parent = transform.lossyScale;
            sr.transform.localScale = new Vector3(scale / NonZero(parent.x), scale / NonZero(parent.y), 1f);
        }

        private static Vector2 Snap(Vector2 world, Sprite cell, int scale)
        {
            float ppu = cell != null && cell.pixelsPerUnit > 0f ? cell.pixelsPerUnit / scale : 0f;
            return ppu > 0f ? new Vector2(Mathf.Round(world.x * ppu) / ppu, Mathf.Round(world.y * ppu) / ppu) : world;
        }

        private static float NonZero(float v) => Mathf.Abs(v) > 1e-6f ? v : 1f;

        private void SplashAt(Vector2 pos, in AnglerLook look, double edgeSeconds, int firstFrame = 0)
        {
            if (_splash.Begin(pos, look.SplashFrames, firstFrame)) _splashEdgeSeconds = edgeSeconds;
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
            // On the water behind him when it lands beyond his feet, in front only short of them.
            int order = sorted
                ? EffectSorting.RodSplashOrder(_bodySr.sortingOrder, _splash.Position.y - transform.position.y)
                : 0;
            _splash.Render((float)(now - _splashEdgeSeconds), look.SplashFrames, look.SplashFps, sorted, order);
        }

        private void HideInteraction()
        {
            DrawsLine = false;
            _strand?.Hide();
            if (_fishSr != null) _fishSr.enabled = false;
            _fish?.HideUnderwater();
            if (_approachSr != null) _approachSr.enabled = false;
            if (_bedShadowSr != null) _bedShadowSr.enabled = false;
            _underDrawn = false;
            _landFromSet = false;
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
