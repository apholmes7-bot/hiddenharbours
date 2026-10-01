using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>WHICH CARRY STANCE A HELD THING ASKS FOR</b> (<see cref="CharacterCarryPoseDef"/>): a row
    /// names its stance, ordinal; a kind with no row (or an empty stance) asks for none; hands ask for
    /// the first held kind with a row, right hand first; and the shipped table carries the pail.
    ///
    /// <para>That every stance a row names is a carry the shipped skin defs play is
    /// <c>CharacterSkinCarryStateTests</c>'s.</para>
    /// </summary>
    public sealed class CharacterCarryPoseDefTests
    {
        sealed class Thing : ICarriable, IHandLoad
        {
            public Thing(HandLoad load, string defId) { HandLoad = load; DefId = defId; }
            public HandLoad HandLoad { get; }
            public string DefId { get; }
            public bool IsCarriable => true;
            public Transform Transform => null;
            public int BakedFacings => 0;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int layer, int order) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        sealed class OneHand : ICarrier
        {
            public ICarriable Carried { get; set; }
            public bool IsCarrying => Carried != null;
        }

        sealed class TwoHands : IHandsCarrier
        {
            public HandSlots Slots { get; } = new HandSlots();
            public ICarriable Carried => Slots.Right ?? Slots.Left;
            public bool IsCarrying => Carried != null;
        }

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        CharacterCarryPoseDef Table(params (string kind, string carry)[] rows)
        {
            var def = ScriptableObject.CreateInstance<CharacterCarryPoseDef>();
            _made.Add(def);
            def.Rows = new CharacterCarryPoseDef.Row[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                def.Rows[i] = new CharacterCarryPoseDef.Row { CarriableDefId = rows[i].kind, Carry = rows[i].carry };
            return def;
        }

        [Test]
        public void ARowNamesItsStanceAndNothingElseDoes()
        {
            CharacterCarryPoseDef table = Table(("container.creel", "pot"), ("container.flat", "tray"), ("tool.gaff", ""));
            Assert.AreEqual("pot", table.CarryFor("container.creel"));
            Assert.AreEqual("tray", table.CarryFor("container.flat"));
            Assert.IsNull(table.CarryFor("tool.gaff"), "A row with no stance asked for one.");
            Assert.IsNull(table.CarryFor("tool.rod"), "A kind with no row asked for a stance.");
            Assert.IsNull(table.CarryFor("Container.Creel"), "The match is not ordinal.");
            Assert.IsNull(table.CarryFor(""));
            Assert.IsNull(table.CarryFor((string)null));

            table.Rows = null;
            Assert.IsNull(table.CarryFor("container.creel"));
        }

        [Test]
        public void OneHandAsksForWhatItHolds()
        {
            CharacterCarryPoseDef table = Table(("container.creel", "pot"));
            var hand = new OneHand();
            Assert.IsNull(table.CarryFor(hand), "Empty hands asked for a stance.");
            Assert.IsNull(table.CarryFor((ICarrier)null));

            hand.Carried = new Thing(HandLoad.Container, "container.creel");
            Assert.AreEqual("pot", table.CarryFor(hand));

            hand.Carried = new Thing(HandLoad.Tool, "tool.rod");
            Assert.IsNull(table.CarryFor(hand));
        }

        [Test]
        public void TwoHandsAskForTheFirstHeldKindWithARowRightHandFirst()
        {
            CharacterCarryPoseDef table = Table(("container.creel", "pot"), ("fish.halibut", "tray"));
            var hands = new TwoHands();
            Assert.AreEqual(HandSlotRefusal.None,
                hands.Slots.TryTake(new Thing(HandLoad.Tool, "tool.rod"), false, CarryHandSide.Right, out _));
            Assert.AreEqual(HandSlotRefusal.None,
                hands.Slots.TryTake(new Thing(HandLoad.Container, "container.creel"), false, CarryHandSide.Left, out _));
            Assert.AreEqual("pot", table.CarryFor(hands), "A row in the left hand was missed behind a rod in the right.");

            var both = new TwoHands();
            Assert.AreEqual(HandSlotRefusal.None,
                both.Slots.TryTake(new Thing(HandLoad.Catch, "fish.halibut"), false, CarryHandSide.Right, out _));
            Assert.AreEqual(HandSlotRefusal.None,
                both.Slots.TryTake(new Thing(HandLoad.Container, "container.creel"), false, CarryHandSide.Left, out _));
            Assert.AreEqual("tray", table.CarryFor(both), "The left hand's row won over the right's.");
        }

        [Test]
        public void TheShippedTableCarriesThePail()
        {
            var shipped = Resources.Load<CharacterCarryPoseDef>(CharacterCarryPoseDef.ResourcesPath);
            Assert.IsNotNull(shipped, $"No CharacterCarryPoseDef at Resources/{CharacterCarryPoseDef.ResourcesPath}.");
            Assert.AreEqual("carrypose.character", shipped.Id);
            Assert.AreEqual("buckets", shipped.CarryFor("container.bucket"));
        }
    }
}
