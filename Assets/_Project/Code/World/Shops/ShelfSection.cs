using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One section of a store's shelves holding several goods</b> (store sections; owner ask 2026-11-25:
    /// several foods on one shelf, picked with the quick chooser). The ONE place the player reaches for a
    /// <see cref="ShopShelfSection"/>: an <see cref="IInteractable"/> that is also an
    /// <see cref="IMultiActionInteractable"/> with one action per good, so the press opens the existing
    /// chooser (keyboard, mouse and gamepad rows) listing each good with its price and how many are left.
    ///
    /// <para><b>Buying is the shelf's own.</b> A row's confirm calls that good's
    /// <see cref="ShelfGood.Press"/>, exactly what pressing a single shelf good does: the same till, the same
    /// money, the same hands or paper bag, the same saved purchase rows, the same bubble line. Each good keeps
    /// its own line, so each runs out on its own: a sold-out good's row says so and is greyed (the chooser
    /// skips it), and its sold-out card stands on the shelf as before.</para>
    ///
    /// <para><b>Cheap.</b> Rows read the goods' cached labels (rebuilt only when a quote moves); nothing here
    /// allocates per frame.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShelfSection : MonoBehaviour, IInteractable, IMultiActionInteractable
    {
        private string _id = "";
        private string _verb = "";
        private float _reach = 1f;
        private System.Func<bool> _inside;
        private ShelfGood[] _goods = new ShelfGood[0];

        /// <summary>The goods this section sells, in row order.</summary>
        public ShelfGood[] Goods => _goods;

        /// <summary>Wires the section. Every good is handed over (<see cref="ShelfGood.PutInSection"/>), so
        /// none is offered on its own. <paramref name="inside"/> answers whether the player is in the store,
        /// downstairs.</summary>
        public void Configure(string id, string verbLabel, float reachMeters, System.Func<bool> inside,
                              ShelfGood[] goods)
        {
            _id = id ?? "";
            _verb = verbLabel ?? "";
            _reach = Mathf.Max(0f, reachMeters);
            _inside = inside;
            _goods = goods ?? new ShelfGood[0];
            for (int i = 0; i < _goods.Length; i++)
                if (_goods[i] != null) _goods[i].PutInSection();
        }

        // ---- the interact seam ---------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reach;

        /// <summary>A fixture: the shelf is bolted to the room.</summary>
        public int Priority => InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Offered while the player is in the store downstairs and the till knows at least one of
        /// its goods. A section with every good sold out is still offered: its rows say so.</summary>
        public bool IsAvailable
        {
            get
            {
                if (_inside != null && !_inside()) return false;
                for (int i = 0; i < _goods.Length; i++)
                    if (_goods[i] != null && _goods[i].IsAvailable) return true;
                return false;
            }
        }

        /// <inheritdoc/>
        public string VerbLabel => _verb;

        /// <summary>The plain press (one row, or a list with a single good left to buy): the first good with
        /// stock, else the first good, whose press then says the shelf is empty.</summary>
        public void Interact(in InteractActor actor)
        {
            int pick = -1;
            for (int i = 0; i < _goods.Length && pick < 0; i++)
                if (ActionAvailable(i)) pick = i;
            if (pick < 0)
                for (int i = 0; i < _goods.Length && pick < 0; i++)
                    if (_goods[i] != null) pick = i;
            if (pick >= 0) Interact(actor, pick);
        }

        // ---- the chooser's rows --------------------------------------------------------------

        /// <inheritdoc/>
        public int ActionCount => _goods.Length;

        /// <summary>Row <paramref name="index"/>: the good's name, price and how many are left, or that it is
        /// sold out. Empty (no row) for a good the till does not know.</summary>
        public string ActionLabel(int index)
        {
            ShelfGood g = At(index);
            return g != null && g.IsAvailable ? g.ChoiceLabel : null;
        }

        /// <summary>A row can be picked while its good is on the shelf.</summary>
        public bool ActionAvailable(int index)
        {
            ShelfGood g = At(index);
            return g != null && g.IsAvailable && !g.SoldOut;
        }

        /// <summary>Buys one of row <paramref name="index"/>'s good, exactly as pressing that good would.</summary>
        public void Interact(in InteractActor actor, int index) => Buy(index, GameServices.Clock?.TotalSeconds ?? 0.0);

        /// <summary>Buys one of row <paramref name="index"/>'s good at <paramref name="now"/> and returns the
        /// outcome (<see cref="ShelfBuyResult.NotSold"/> for no such row). Public so a test drives it.</summary>
        public ShelfBuyResult Buy(int index, double now)
        {
            ShelfGood g = At(index);
            return g != null ? g.Press(now) : ShelfBuyResult.NotSold;
        }

        private ShelfGood At(int index) => index >= 0 && index < _goods.Length ? _goods[index] : null;

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);
    }
}
