using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One store's front</b>: its sign, always out, and its displays, out only while the store is open.
    /// Built once under the building (so it moves and unloads with it) by <see cref="StoreFrontsDirector"/>.
    /// It looks at the clock every <see cref="StoreFrontDef.CheckRealSeconds"/> and only touches the displays
    /// when the open state changes, so it costs nothing between the two changes a day. No state is saved:
    /// open or closed is worked out from the hour each time (rule 5).
    /// </summary>
    public sealed class StoreFront : MonoBehaviour
    {
        private StoreFrontDef _def;
        private readonly List<GameObject> _displays = new List<GameObject>();
        private float _sinceCheck;
        private int _shown = -1;   // -1 not yet decided, 0 put away, 1 out

        /// <summary>The front's definition.</summary>
        public StoreFrontDef Def => _def;

        /// <summary>True while the displays are out.</summary>
        public bool DisplaysOut => _shown == 1;

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
            }
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

        private void OnEnable()
        {
            _sinceCheck = 0f;
            Check();
        }

        private void Update()
        {
            if (_def == null) return;
            _sinceCheck += Time.unscaledDeltaTime;
            if (_sinceCheck < _def.CheckRealSeconds) return;
            _sinceCheck = 0f;
            Check();
        }

        private void Check()
        {
            IGameClock clock = GameServices.Clock;
            if (clock != null) ApplyHour(clock.HourOfDay);
        }
    }
}
