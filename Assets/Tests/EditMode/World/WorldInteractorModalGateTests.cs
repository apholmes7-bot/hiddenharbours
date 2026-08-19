using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>An interact press that a modal has already taken cannot also start a conversation.</b>
    ///
    /// <para><b>Why this test exists now and not before.</b> This component reads the interact key
    /// directly and, alone among the readers of it (<c>ControlSwitcher</c>, <c>InteractVerb</c>), never
    /// consulted <see cref="InteractionGate"/> — it only ever RAISED the gate, through the dialogue
    /// presenter. That was harmless while the dialogue was the only modal, because a conversation being
    /// up is handled by its own branch. It stopped being harmless when the wardrobe picker arrived: the
    /// picker confirms on the interact key while the player stands at a fixture, and Aunt Ginny stands
    /// 1.80 m from her own furniture against this component's 1.8 m radius — so one press would both wear
    /// a coat and start a conversation.</para>
    ///
    /// <para>Driven through <c>BeginInteract</c>, which is the input-free half <c>Update</c> calls, so
    /// what is asserted here is what a press does. A virtual key press is undeliverable to the new Input
    /// System's device state, and would be asserting on a press that never happened.</para>
    /// </summary>
    public class WorldInteractorModalGateTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp() => InteractionGate.Reset();

        [TearDown]
        public void TearDown()
        {
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        GameObject Spawn(string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.position = at;
            _spawned.Add(go);
            return go;
        }

        /// <summary>A player, and a neighbour standing well inside the interactor's reach.</summary>
        WorldInteractor StandAnInteractorWithSomebodyInReach()
        {
            var player = Spawn("player", Vector3.zero);

            var npcGo = Spawn("neighbour", new Vector3(0.5f, 0f, 0f));
            var npc = npcGo.AddComponent<Interactable>();
            npc.Configure(InteractKind.Talk, "Ginny", "conversation.test", "flag.test");

            var host = Spawn("interactor", Vector3.zero);
            var interactor = host.AddComponent<WorldInteractor>();
            // No presenter: Begin() bails on a null one, but BeginInteract still reports the press SPENT,
            // which is the answer under test — whether the press was taken at all.
            interactor.Configure(player.transform, null, new[] { npc }, 1.8f);
            return interactor;
        }

        [Test]
        public void With_no_modal_up_a_press_starts_a_conversation()
        {
            var interactor = StandAnInteractorWithSomebodyInReach();

            Assert.IsTrue(interactor.BeginInteract(),
                          "the control arm: somebody is in reach and the press is theirs");
        }

        [Test]
        public void While_a_modal_owns_the_key_the_press_is_refused()
        {
            var interactor = StandAnInteractorWithSomebodyInReach();

            InteractionGate.IsBlocked = true;   // the wardrobe picker is up

            Assert.IsFalse(interactor.BeginInteract(),
                           "⭐ one press cannot both wear a coat and start a conversation");
        }

        [Test]
        public void Lowering_the_gate_hands_the_press_straight_back()
        {
            var interactor = StandAnInteractorWithSomebodyInReach();

            InteractionGate.IsBlocked = true;
            Assert.IsFalse(interactor.BeginInteract());

            InteractionGate.IsBlocked = false;
            Assert.IsTrue(interactor.BeginInteract(),
                          "the refusal is a hold, not a latch — closing the panel restores the world");
        }
    }
}
