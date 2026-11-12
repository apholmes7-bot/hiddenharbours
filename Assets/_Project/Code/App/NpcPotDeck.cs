using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>The deck of one NPC pot boat while she works</b>: where her hand stands, what is in the hand, and what
    /// lies in the crates. Every frame is a lookup on the clock (<see cref="NpcPotDay"/>,
    /// <see cref="NpcPotDayCatch"/>): the hand walks from the helm to the rail for the gaff, takes each animal
    /// out of the pot, carries a keeper to the bench to band it and on to the crate for its grade, and puts a
    /// short or a berried one back over the side. Nothing moves that is not in the hand.
    ///
    /// <para>Everything here is a child of the hull, in hull-local metres (x = starboard, y = toward the
    /// bow), so it rides and turns with her. Renderers are made at binding and reused; a frame allocates
    /// nothing.</para>
    /// </summary>
    public sealed class NpcPotDeck
    {
        private const float WalkShare = 0.4f;          // first share of a step spent walking to where it is done
        private const float ToTheRailShare = 0.5f;     // share of the gaff spent coming from the helm
        private const float ToTheHelmShare = 0.3f;     // share of an ease between pots spent going back to it
        private const float StandEpsilonMeters = 0.001f;
        private const float FacingEpsilonDegrees = 0.5f;
        private const float StarboardDegrees = 90f;

        private readonly NpcPotDayDef _def;
        private readonly MooredBoat _hull;
        private readonly IsoCharacterSprite _skipper;
        private readonly BoatDeckDef _deck;
        private readonly GameObject _root;
        private readonly SpriteRenderer _inHand;
        private readonly SpriteRenderer[][] _crates;
        private readonly int[] _shown;
        private readonly Vector2 _helm, _rail, _bench;
        private readonly Vector2[] _crateStand;

        private int _areaHint;
        private Vector2 _stand;
        private bool _stood;
        private float _facing = float.NaN;
        private bool _wasReturning;
        private int _shownDay = int.MinValue;

        /// <summary>Where the hand stands, deck metres (x = starboard, y = toward the bow).</summary>
        public Vector2 StandDeckMeters => _stand;
        /// <summary>The animal in the hand; disabled when the hand is empty.</summary>
        public SpriteRenderer InHand => _inHand;
        /// <summary>The step last drawn.</summary>
        public NpcDeckStep Step { get; private set; }
        public int GradeCount => _crates.Length;
        /// <summary>Animals drawn in the crate for <paramref name="grade"/>.</summary>
        public int ShownInCrate(int grade) => grade >= 0 && grade < _shown.Length ? _shown[grade] : 0;
        public Vector2 HelmDeckMeters => _helm;
        public Vector2 RailDeckMeters => _rail;
        public Vector2 BenchDeckMeters => _bench;
        public Vector2 CrateDeckMeters(int grade) => _crateStand[Mathf.Clamp(grade, 0, _crateStand.Length - 1)];

        public NpcPotDeck(NpcPotDayDef def, MooredBoat hull, IsoCharacterSprite skipper)
        {
            _def = def;
            _hull = hull;
            _skipper = skipper;
            _deck = hull.Visual != null ? hull.Visual.Deck : null;

            Vector3 helm = MooredBoat.StandPointOf(_deck);
            _helm = new Vector2(helm.x, helm.y);
            _rail = OnDeck(def.HaulerLocalMeters);
            _bench = OnDeck(def.BenchLocalMeters);

            int grades = (def.GradeBreaksMm != null ? def.GradeBreaksMm.Length : 0) + 1;
            _crateStand = new Vector2[grades];
            _crates = new SpriteRenderer[grades][];
            _shown = new int[grades];

            _root = new GameObject("NpcPotDeck_" + def.Id);
            _root.transform.SetParent(hull.transform, worldPositionStays: false);

            SpriteRenderer worn = skipper != null ? skipper.GetComponent<SpriteRenderer>() : null;
            int layer = worn != null ? worn.sortingLayerID : 0;
            int order = worn != null ? worn.sortingOrder : def.CatchSortingOrder;

            for (int g = 0; g < grades; g++)
            {
                Vector2 crate = def.CrateLocalMeters + def.CrateStepLocalMeters * g;
                _crateStand[g] = OnDeck(crate);
                int capacity = Mathf.Max(1, def.CrateShownCapacity);
                int perRow = Mathf.Max(1, def.CrateRowLength);
                int rows = (capacity + perRow - 1) / perRow;
                _crates[g] = new SpriteRenderer[capacity];
                for (int j = 0; j < capacity; j++)
                {
                    float across = ((j % perRow) + 0.5f) / perRow - 0.5f;
                    float along = ((j / perRow) + 0.5f) / rows - 0.5f;
                    Vector2 at = crate + new Vector2(across * def.CrateInsideMeters.x, along * def.CrateInsideMeters.y);
                    _crates[g][j] = Make($"Crate{g}_{j}", at, layer, order);
                }
            }
            _inHand = Make("InHand", _rail, layer, order + 1);
            _stand = _helm;
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            if (_hull != null) StandAt(_helm);
        }

        /// <summary>Draws the deck at <paramref name="now"/>.</summary>
        public void Tick(in NpcPotBoatPose pose, NpcPotDay today, NpcPotDayCatch catchOfTheDay, double now)
        {
            Vector2 stand = _helm;
            float facing = 0f;            // degrees off the bow the hand faces
            Sprite carried = null;
            Vector2 carriedAt = default;
            NpcDeckStep step = NpcDeckStep.Idle;
            bool returning = false;
            int pot = pose.PotIndex;

            switch (pose.Stage)
            {
                case NpcPotBoatStage.Gaffing:
                    stand = Vector2.Lerp(_helm, _rail, Mathf.Clamp01(pose.Stage01 / ToTheRailShare));
                    facing = StarboardDegrees;
                    break;

                case NpcPotBoatStage.Hauling:
                case NpcPotBoatStage.Setting:
                    stand = _rail;
                    facing = StarboardDegrees;
                    break;

                case NpcPotBoatStage.Shifting:
                    if (today != null && pot >= 0 && pot < today.PotCount && now < today.GaffStart[pot])
                        stand = Vector2.Lerp(_rail, _helm, Mathf.Clamp01(pose.Stage01 / ToTheHelmShare));   // on to the next pot
                    else { stand = _rail; facing = StarboardDegrees; }                                     // easing to the mark, pot aboard
                    break;

                case NpcPotBoatStage.PassageHome:
                    if (today != null && today.PotCount > 0 && _def.DeckWalkSeconds > 0f)
                    {
                        double since = now - today.SetEnd[today.PotCount - 1];
                        stand = Vector2.Lerp(_rail, _helm, Mathf.Clamp01((float)(since / _def.DeckWalkSeconds)));
                    }
                    break;

                case NpcPotBoatStage.DeckWork:
                {
                    stand = _rail;
                    facing = StarboardDegrees;
                    if (catchOfTheDay == null) break;
                    NpcDeckMoment moment = catchOfTheDay.MomentAt(pot, now);
                    step = moment.Step;
                    var animals = catchOfTheDay.Animals(pot);
                    Vector2 cameFrom = _rail;
                    int before = moment.Animal >= 0 ? moment.Animal - 1 : animals.Count - 1;
                    if (before >= 0 && before < animals.Count && animals[before].Keeper)
                        cameFrom = CrateDeckMeters(animals[before].Grade);

                    switch (moment.Step)
                    {
                        case NpcDeckStep.Picking:
                            stand = Vector2.Lerp(cameFrom, _rail, Mathf.Clamp01(moment.Step01 / WalkShare));
                            if (moment.Step01 >= WalkShare)
                            {
                                carried = animals[moment.Animal].Sprite;
                                carriedAt = Vector2.Lerp(_def.HaulerLocalMeters, stand,
                                                         Mathf.InverseLerp(WalkShare, 1f, moment.Step01));
                            }
                            break;

                        case NpcDeckStep.Banding:
                            stand = Vector2.Lerp(_rail, _bench, Mathf.Clamp01(moment.Step01 / WalkShare));
                            carried = animals[moment.Animal].Sprite;
                            carriedAt = stand;
                            break;

                        case NpcDeckStep.Stowing:
                        {
                            Vector2 crate = CrateDeckMeters(animals[moment.Animal].Grade);
                            stand = Vector2.Lerp(_bench, crate, moment.Step01);
                            facing = NpcPotDay.Bearing(_bench, crate, StarboardDegrees);
                            carried = animals[moment.Animal].Sprite;
                            carriedAt = stand;
                            break;
                        }

                        case NpcDeckStep.Returning:
                        {
                            Vector2 overTheSide = _def.HaulerLocalMeters + Vector2.right * _def.ReturnOverTheSideMeters;
                            carried = animals[moment.Animal].Sprite;
                            carriedAt = Vector2.Lerp(_rail, overTheSide, Mathf.SmoothStep(0f, 1f, moment.Step01));
                            returning = true;
                            break;
                        }

                        case NpcDeckStep.Baiting:
                            stand = Vector2.Lerp(cameFrom, _rail, Mathf.Clamp01(moment.Step01 / ToTheHelmShare));
                            break;
                    }
                    break;
                }
            }

            Step = step;
            StandAt(stand);
            Face(pose.HeadingDegrees + facing);
            Carry(carried, carriedAt);
            if (_wasReturning && !returning) Splash();
            _wasReturning = returning;
            FillCrates(catchOfTheDay, now);
        }

        // ---- the hand ---------------------------------------------------------------------------------

        private Vector2 OnDeck(Vector2 deckPoint)
        {
            if (_deck == null || !_deck.HasWalkableDeck()) return deckPoint;
            int hint = 0;
            return _deck.ClampToWalkable(deckPoint, ref hint, out _);
        }

        private void StandAt(Vector2 deckPoint)
        {
            if (_stood && (deckPoint - _stand).sqrMagnitude < StandEpsilonMeters * StandEpsilonMeters) return;
            _stood = true;
            _stand = deckPoint;
            float height = 0f;
            Vector2 on = deckPoint;
            if (_deck != null && _deck.HasWalkableDeck()) on = _deck.ClampToWalkable(deckPoint, ref _areaHint, out height);
            _hull.StandTheSkipperAt(new Vector3(on.x, on.y, height));
        }

        private void Face(float headingDegrees)
        {
            if (_skipper == null) return;
            if (!float.IsNaN(_facing) && Mathf.Abs(Mathf.DeltaAngle(_facing, headingDegrees)) < FacingEpsilonDegrees) return;
            _facing = headingDegrees;
            _skipper.HoldHeading(Mathf.Repeat(headingDegrees, 360f));
        }

        private void Carry(Sprite sprite, Vector2 hullLocal)
        {
            if (sprite == null)
            {
                if (_inHand.enabled) _inHand.enabled = false;
                return;
            }
            if (_inHand.sprite != sprite) _inHand.sprite = sprite;
            _inHand.transform.localPosition = new Vector3(hullLocal.x, hullLocal.y, 0f);
            if (!_inHand.enabled) _inHand.enabled = true;
        }

        private void Splash()
        {
            TrapDef trap = _def.Trap;
            if (trap == null || trap.SplashBurstFrames == null || trap.SplashBurstFrames.Length == 0) return;
            Vector2 at = _inHand.transform.position;   // where the hand let go, in the world
            TrapSplashFx.Play(trap.SplashBurstFrames, trap.SplashBurstFps, at);
        }

        // ---- the crates -------------------------------------------------------------------------------

        private void FillCrates(NpcPotDayCatch catchOfTheDay, double now)
        {
            bool newDay = catchOfTheDay != null && catchOfTheDay.DayIndex != _shownDay;
            for (int g = 0; g < _crates.Length; g++)
            {
                int stowed = catchOfTheDay != null ? catchOfTheDay.StowedAt(now, g) : 0;
                int show = Mathf.Min(stowed, _crates[g].Length);
                if (show == _shown[g] && !newDay) continue;

                int slot = 0;
                for (int p = 0; catchOfTheDay != null && p < catchOfTheDay.PotCount && slot < show; p++)
                {
                    var animals = catchOfTheDay.Animals(p);
                    for (int k = 0; k < animals.Count && slot < show; k++)
                    {
                        if (!animals[k].Keeper || animals[k].Grade != g || now < animals[k].DoneAt) continue;
                        SpriteRenderer r = _crates[g][slot++];
                        if (r.sprite != animals[k].Sprite) r.sprite = animals[k].Sprite;
                        if (!r.enabled) r.enabled = true;
                    }
                }
                for (int j = slot; j < _crates[g].Length; j++)
                    if (_crates[g][j].enabled) _crates[g][j].enabled = false;
                _shown[g] = slot;
            }
            if (catchOfTheDay != null) _shownDay = catchOfTheDay.DayIndex;
        }

        private SpriteRenderer Make(string name, Vector2 hullLocal, int sortingLayer, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(hullLocal.x, hullLocal.y, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingLayerID = sortingLayer;
            r.sortingOrder = sortingOrder;
            r.enabled = false;
            return r;
        }
    }
}
