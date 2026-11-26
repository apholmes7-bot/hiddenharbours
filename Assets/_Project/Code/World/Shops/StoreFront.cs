using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One store's front</b>: its name board and lamps on the face above the awning, shown except while the
    /// player is inside (the wall is taken away then), and its displays, out only while the store is open.
    /// A lamp's light is asked of the village's lamp look through Core (<see cref="GroundPropPresentation"/>),
    /// so it comes on with the dark exactly as every other village lamp does.
    /// Built once under the building (so it moves and unloads with it) by <see cref="StoreFrontsDirector"/>.
    /// It looks at the clock every <see cref="StoreFrontDef.CheckRealSeconds"/> and only touches the displays
    /// when the open state changes, so it costs nothing between the two changes a day. No state is saved:
    /// open or closed is worked out from the hour each time (rule 5).
    /// </summary>
    public sealed class StoreFront : MonoBehaviour
    {
        private StoreFrontDef _def;
        private readonly List<GameObject> _displays = new List<GameObject>();
        private readonly List<GameObject> _onWall = new List<GameObject>();
        private readonly List<GameObject> _lamps = new List<GameObject>();
        private readonly List<GameObject> _nightPictures = new List<GameObject>();
        private int _night = -1;   // -1 not yet decided, 0 day, 1 night
        private BuildingInterior _building;
        private float _sinceCheck;
        private int _shown = -1;   // -1 not yet decided, 0 put away, 1 out
        private int _wallShown = -1;   // -1 not yet decided, 0 hidden (he is inside), 1 shown

        /// <summary>The front's definition.</summary>
        public StoreFrontDef Def => _def;

        /// <summary>True while the displays are out.</summary>
        public bool DisplaysOut => _shown == 1;

        /// <summary>True while the pieces on the wall (the board, its lamps) show: whenever the player is not inside.</summary>
        public bool WallPiecesShown => _wallShown != 0;

        /// <summary>The lamps' light points, one per lamp, each a child of its lamp.</summary>
        public IReadOnlyList<GameObject> LampLights => _lamps;

        /// <summary>Builds the front's pieces under <paramref name="building"/>. Public so a test can build one.</summary>
        public static StoreFront Build(StoreFrontDef def, Transform building)
        {
            var go = new GameObject("StoreFront");
            go.transform.SetParent(building, false);
            var front = go.AddComponent<StoreFront>();
            front.Configure(def);
            return front;
        }

        private void Configure(StoreFrontDef def)
        {
            _def = def;
            _building = GetComponentInParent<BuildingInterior>();
            if (def == null || def.Pieces == null) return;
            foreach (StoreFrontPiece piece in def.Pieces)
            {
                if (piece == null) continue;
                var go = new GameObject(string.IsNullOrEmpty(piece.Id) ? "piece" : piece.Id);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = piece.Offset;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = piece.Sprite;
                sr.sortingOrder = piece.SortingOrder;
                if (def.Material != null) sr.sharedMaterial = def.Material;
                if (piece.OnlyWhileOpen) _displays.Add(go);
                if (piece.HideWhileInside) _onWall.Add(go);
                if (piece.Glows) _lamps.Add(Light(go, piece));
                if (piece.NightSprite != null)
                {
                    var lit = new GameObject(go.name + ".night");
                    lit.transform.SetParent(go.transform, false);
                    var litRenderer = lit.AddComponent<SpriteRenderer>();
                    litRenderer.sprite = piece.NightSprite;
                    litRenderer.sortingOrder = def.NightSortingOrder;
                    lit.SetActive(false);
                    _nightPictures.Add(lit);
                }
            }
        }

        /// <summary>True while the lit night pictures show.</summary>
        public bool NightPicturesShown => _night == 1;

        /// <summary>Shows the lit night pictures when <paramref name="night"/> and hides them otherwise. Returns
        /// true when that changed anything. Public so a test drives it without a clock.</summary>
        public bool ApplyNight(bool night)
        {
            int want = night ? 1 : 0;
            if (want == _night) return false;
            _night = want;
            for (int i = 0; i < _nightPictures.Count; i++)
                if (_nightPictures[i] != null) _nightPictures[i].SetActive(want == 1);
            return true;
        }

        private static GameObject Light(GameObject lamp, StoreFrontPiece piece)
        {
            var at = new GameObject(lamp.name + ".light");
            at.transform.SetParent(lamp.transform, false);
            at.transform.localPosition = piece.GlowOffset;
            // The light hangs at the bulb (no lift), so its glow falls on the board under it. Visual only, never saved.
            GroundPropPresentation.Service?.Dress(at, null, new GroundGlow
            {
                Lit = true,
                Color = piece.GlowColor,
                RangeMetres = piece.GlowRangeMetres,
                Intensity = piece.GlowIntensity,
                Flicker01 = 0f,
                HeightMetres = 0f,
            });
            return at;
        }

        /// <summary>Puts the displays out or away for <paramref name="hourOfDay"/>. Returns true when that
        /// changed anything.</summary>
        public bool ApplyHour(float hourOfDay)
        {
            if (_def == null) return false;
            int open = _def.IsOpenAt(hourOfDay) ? 1 : 0;
            if (open == _shown) return false;
            _shown = open;
            for (int i = 0; i < _displays.Count; i++)
                if (_displays[i] != null) _displays[i].SetActive(open == 1);
            return true;
        }

        /// <summary>Hides the wall pieces while <paramref name="inside"/> and shows them otherwise. Returns true
        /// when that changed anything. Public so a test drives it without an occupant.</summary>
        public bool ApplyInside(bool inside)
        {
            int shown = inside ? 0 : 1;
            if (shown == _wallShown) return false;
            _wallShown = shown;
            for (int i = 0; i < _onWall.Count; i++)
                if (_onWall[i] != null) _onWall[i].SetActive(shown == 1);
            return true;
        }

        private void OnEnable()
        {
            _sinceCheck = 0f;
            Check();
        }

        private void Update()
        {
            if (_def == null) return;
            // One bool a frame: the wall comes away the moment he steps in, so the sign must too.
            if (_onWall.Count > 0) ApplyInside(_building != null && _building.IsInside);
            _sinceCheck += Time.unscaledDeltaTime;
            if (_sinceCheck < _def.CheckRealSeconds) return;
            _sinceCheck = 0f;
            Check();
        }

        private void Check()
        {
            IGameClock clock = GameServices.Clock;
            if (clock != null)
            {
                ApplyHour(clock.HourOfDay);
                // The board is lit over the same hours as the village's windows: the shared sky's sunset to sunrise.
                if (_nightPictures.Count > 0) ApplyNight(WatchFaceState.NightAt(clock.HourOfDay));
            }
            if (_onWall.Count > 0) ApplyInside(_building != null && _building.IsInside);
        }
    }
}
