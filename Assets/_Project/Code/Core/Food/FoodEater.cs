using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Eat</b> (first food slice): use up one unit of a food and lower hunger by exactly its
    /// <see cref="FoodDef.HungerRelief"/>, which clamps at fed. Since the food follow-up the bite goes
    /// through the body (<see cref="IBodyService.Eat(float, float, Vector2, string)"/>): the stomach fills,
    /// so eating too much at once is sick, and the food's <see cref="FoodDef.EnergyKcal"/> past what hunger
    /// was short of becomes mass. The body relieves hunger through <see cref="INeedsService.Eat"/> itself;
    /// the eater calls the needs directly only when there is no body.
    /// Two sources: the counted supply locker (shop food, <see cref="SupplyLocker"/>) and the loose catch in
    /// the hands (<see cref="ICatchHands"/>).
    ///
    /// <para><b>Diegetic only</b> (owner ruling 2026-11-14). It publishes <see cref="AteFood"/> once on
    /// success and <see cref="EatRefused"/> once on a refusal, with a reason code and no text, so audio, the
    /// rig and dialogue react; nothing here draws, and no number reaches a screen.</para>
    ///
    /// <para><b>A refusal changes nothing.</b> Every check runs before anything is used up, so a raw cod
    /// stays in the hand and the locker keeps its count.</para>
    ///
    /// <para><b>SEAM, not yet wired to a press.</b> No "use the held item" input exists (the key ledger is
    /// spent), and adding a driver means a scene or prefab edit. A later driver calls
    /// <see cref="TryEatFromHand"/> or <see cref="TryEatFromStock"/> through <see cref="GameServices.Food"/>.
    /// </para>
    ///
    /// <para>Deterministic: no roll, no clock read of its own; the same save, hands and Defs give the same
    /// result (rule 5). Every seam is injectable for tests and defaults to its <see cref="GameServices"/>
    /// accessor, read at each call.</para>
    /// </summary>
    public sealed class FoodEater
    {
        private readonly Func<FoodBook> _book;
        private readonly Func<INeedsService> _needs;
        private readonly Func<ISaveService> _save;
        private readonly Func<ICatchHands> _hands;
        private readonly Func<IBodyService> _body;
        private readonly Func<Vector2> _position;
        private readonly Func<string> _regionId;
        private FoodBook _shipped;

        /// <param name="body">The one door a bite goes through (food follow-up): the body relieves hunger
        /// by the relief, fills the stomach (overeating is real), and stores the food's energy past what
        /// hunger was short of. Only when there is no body does the bite fall back to
        /// <paramref name="needs"/> alone. Never both, so hunger is relieved once.</param>
        /// <param name="position">Where the player stands, for the mess an overfull stomach leaves.</param>
        /// <param name="regionId">The region the player is in, for that mess.</param>
        public FoodEater(Func<FoodBook> book = null, Func<INeedsService> needs = null,
                         Func<ISaveService> save = null, Func<ICatchHands> hands = null,
                         Func<IBodyService> body = null, Func<Vector2> position = null,
                         Func<string> regionId = null)
        {
            _book = book ?? (() => _shipped ??= FoodBook.FromResources());
            _needs = needs ?? (() => GameServices.Needs);
            _save = save ?? (() => GameServices.Save);
            _hands = hands ?? (() => GameServices.CatchHands);
            _body = body ?? (() => GameServices.Body);
            _position = position ?? (() => PlayerPosition());
            _regionId = regionId ?? (() => GameServices.CurrentRegionId);
        }

        private static Vector2 PlayerPosition()
        {
            Transform player = GameServices.PlayerTransform;
            return player != null ? (Vector2)player.position : Vector2.zero;
        }

        /// <summary>Would this item be eaten as it is? Changes nothing and publishes nothing.</summary>
        public EatRefusal Check(string itemId, out FoodDef food)
        {
            food = null;
            if (string.IsNullOrEmpty(itemId)) return EatRefusal.NothingToEat;
            food = _book()?.FindByItem(itemId);
            if (food == null) return EatRefusal.NotFood;
            return food.EdibleRaw ? EatRefusal.None : EatRefusal.NotEdibleRaw;
        }

        /// <summary>Eat one <paramref name="itemId"/> from the supply locker (a loaf bought at the store).</summary>
        public EatRefusal TryEatFromStock(string itemId)
        {
            SaveData save = _save()?.Current;
            if (!SupplyLocker.Has(save, itemId)) return Refuse(itemId, EatRefusal.NothingToEat);

            EatRefusal refusal = Check(itemId, out FoodDef food);
            if (refusal != EatRefusal.None) return Refuse(itemId, refusal);

            if (!SupplyLocker.TrySpend(save, itemId)) return Refuse(itemId, EatRefusal.NothingToEat);
            return Ate(food, itemId);
        }

        /// <summary>Eat the loose catch in the hands (a pail's contents are never touched).</summary>
        public EatRefusal TryEatFromHand()
        {
            ICatchHands hands = _hands();
            if (hands == null || !hands.TryPeekHand(out CatchItem held))
                return Refuse(null, EatRefusal.NothingToEat);

            EatRefusal refusal = Check(held.SpeciesId, out FoodDef food);
            if (refusal != EatRefusal.None) return Refuse(held.SpeciesId, refusal);

            if (!hands.TryTakeFromHand(out _)) return Refuse(held.SpeciesId, EatRefusal.NothingToEat);
            return Ate(food, held.SpeciesId);
        }

        private EatRefusal Ate(FoodDef food, string itemId)
        {
            // ONE path (food follow-up): the body relieves hunger itself (INeedsService.Eat inside), so a
            // bite through the body never also calls the needs. An overfull stomach brings it back up: the
            // unit is still used, the body publishes Vomited and leaves the mess.
            IBodyService body = _body();
            if (body != null) body.Eat(food.HungerRelief, food.EnergyKcal, _position(), _regionId());
            else _needs()?.Eat(food.HungerRelief);
            EventBus.Publish(new AteFood(food.Id, itemId, food.HungerRelief));
            return EatRefusal.None;
        }

        private static EatRefusal Refuse(string itemId, EatRefusal reason)
        {
            EventBus.Publish(new EatRefused(itemId, reason));
            return reason;
        }
    }
}
