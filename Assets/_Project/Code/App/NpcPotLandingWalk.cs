using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>The skipper ashore with her crates</b>: the figure on the ladder and the wharf while she lands the
    /// day's catch, the crate in her hands, and the crates put down at the buyer's counter. Every frame is a
    /// lookup on the clock (<see cref="NpcPotLanding"/>); nothing is saved. While she is ashore the figure on
    /// the boat is hidden, so there is only ever one of her.
    ///
    /// <para>Renderers are made at binding and reused; a frame allocates nothing. The figure is sorted by its
    /// feet on the shared decor band (<see cref="SortingBands"/>), like everything else that stands on the
    /// wharf.</para>
    /// </summary>
    public sealed class NpcPotLandingWalk
    {
        private readonly NpcPotDayDef _def;
        private readonly SpriteRenderer _aboard;
        private readonly Transform _hull;
        private readonly GameObject _root;
        private readonly Transform _figure;
        private readonly SpriteRenderer _body;
        private readonly IsoCharacterSprite _iso;
        private readonly SpriteRenderer _inHands;
        private readonly SpriteRenderer[] _atCounter;
        private bool _aboardHidden;

        /// <summary>True while the figure on the wharf is the one drawn.</summary>
        public bool IsAshore => _body.enabled;
        /// <summary>Where the ashore figure stands, world metres.</summary>
        public Vector2 Position => _figure.position;
        /// <summary>The crate in her hands ashore; disabled when her hands are empty.</summary>
        public SpriteRenderer InHands => _inHands;
        /// <summary>The step last drawn.</summary>
        public NpcLandingStep Step { get; private set; }
        public int GradeCount => _atCounter.Length;
        /// <summary>True when the crate for <paramref name="grade"/> is drawn at the buyer's counter.</summary>
        public bool ShownAtCounter(int grade) => grade >= 0 && grade < _atCounter.Length && _atCounter[grade].enabled;

        /// <param name="aboard">The figure's renderer on the boat, hidden while she is ashore. May be null.</param>
        /// <param name="hull">Her hull, as it is drawn: the foot of the ladder is pinned to its rail. May be null.</param>
        public NpcPotLandingWalk(NpcPotDayDef def, CharacterVisualDef skipper, SpriteRenderer aboard, Transform hull,
                                 Transform parent, float z)
        {
            _def = def;
            _aboard = aboard;
            _hull = hull;
            _root = new GameObject("NpcPotLanding_" + def.Id);
            _root.transform.SetParent(parent, worldPositionStays: true);

            var figure = new GameObject("SkipperAshore");
            _figure = figure.transform;
            _figure.SetParent(_root.transform, worldPositionStays: true);
            _figure.position = new Vector3(def.BerthPosition.x, def.BerthPosition.y, z);
            _body = figure.AddComponent<SpriteRenderer>();
            _iso = figure.AddComponent<IsoCharacterSprite>();
            if (skipper != null) _iso.Configure(skipper);
            _body.enabled = false;

            var hands = new GameObject("CrateInHands");
            hands.transform.SetParent(_figure, worldPositionStays: false);
            hands.transform.localPosition = new Vector3(def.CrateCarryOffsetMeters.x, def.CrateCarryOffsetMeters.y, 0f);
            _inHands = hands.AddComponent<SpriteRenderer>();
            _inHands.sprite = def.LandingCrateSprite;
            _inHands.enabled = false;

            int grades = (def.GradeBreaksMm != null ? def.GradeBreaksMm.Length : 0) + 1;
            _atCounter = new SpriteRenderer[grades];
            Vector2 counter = def.LandingRoute != null && def.LandingRoute.Length > 0
                ? def.LandingRoute[def.LandingRoute.Length - 1]
                : def.BerthPosition;
            for (int g = 0; g < grades; g++)
            {
                var crate = new GameObject("CrateAtCounter" + g);
                crate.transform.SetParent(_root.transform, worldPositionStays: true);
                Vector2 at = counter + def.CounterCrateOffsetMeters
                             + Vector2.right * ((g - (grades - 1) * 0.5f) * def.CounterCrateSpacingMeters);
                crate.transform.position = new Vector3(at.x, at.y, z);
                var r = crate.AddComponent<SpriteRenderer>();
                r.sprite = def.LandingCrateSprite;
                r.sortingOrder = OrderAt(at.y);
                r.enabled = false;
                _atCounter[g] = r;
            }
        }

        public void Dispose()
        {
            ShowAboard(true);
            if (_root != null) Object.Destroy(_root);
        }

        /// <summary>Draws the landing at <paramref name="now"/>.</summary>
        public void Tick(NpcPotLanding landing, double now)
        {
            bool landingNow = landing != null && landing.HasLanding && now >= landing.StartSeconds && now < landing.EndSeconds;
            NpcLandingMoment moment = landingNow ? landing.MomentAt(now) : default;
            Step = moment.Step;
            bool ashore = landingNow && moment.Ashore;

            if (ashore)
            {
                Vector2 at = moment.Position;
                if (moment.OnLadder && _hull != null && _def.LandingRoute.Length > 0)
                {
                    // The foot of the ladder is her rail where the hull is drawn (she rides the tide); the
                    // top is the wharf. Up with a crate, down without.
                    Vector2 foot = _hull.TransformPoint(new Vector3(_def.StepAshoreLocalMeters.x, _def.StepAshoreLocalMeters.y, 0f));
                    Vector2 top = _def.LandingRoute[0];
                    at = moment.Step == NpcLandingStep.Carrying ? Vector2.Lerp(foot, top, moment.Step01)
                                                               : Vector2.Lerp(top, foot, moment.Step01);
                }
                Vector3 p = _figure.position;
                _figure.position = new Vector3(at.x, at.y, p.z);
                float heading = Mathf.Repeat(moment.HeadingDegrees, 360f);
                _iso.HoldHeading(heading);
                _body.sortingOrder = OrderAt(at.y);
                bool holding = moment.Crate >= 0 && _inHands.sprite != null;
                // Walking away from the camera the crate is in front of her, so her back hides it.
                bool away = heading < 90f || heading > 270f;
                if (holding) _inHands.sortingOrder = _body.sortingOrder + (away ? -1 : 1);
                if (_inHands.enabled != holding) _inHands.enabled = holding;
            }
            else if (_inHands.enabled) _inHands.enabled = false;

            if (_body.enabled != ashore) _body.enabled = ashore;
            ShowAboard(!ashore);

            for (int g = 0; g < _atCounter.Length; g++)
            {
                bool down = landing != null && landing.AtCounterAt(now, g) > 0 && _atCounter[g].sprite != null;
                if (_atCounter[g].enabled != down) _atCounter[g].enabled = down;
            }
        }

        private void ShowAboard(bool show)
        {
            if (_aboard == null || _aboardHidden == !show) return;
            _aboardHidden = !show;
            _aboard.enabled = show;
        }

        private static int OrderAt(float worldY)
            => Mathf.Clamp(Mathf.RoundToInt(SortingBands.DecorBase - worldY * SortingBands.OrdersPerMetre),
                           SortingBands.DecorFloor, SortingBands.DecorCeiling);
    }
}
