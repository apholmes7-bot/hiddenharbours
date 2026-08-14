using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Put a real tool in a real pair of hands</b> — the one-line precondition every test of a
    /// tool-gated action now needs (the owner's tools-in-hand ruling, 2026-08-13).
    ///
    /// <para><b>Why this builds the REAL components instead of a fake carrier.</b> A hand-rolled
    /// <c>FakeCarrier</c> would be four lines and would prove nothing: it would satisfy
    /// <see cref="CarriedItem.InHand"/> by construction, so the gate could be checking anything at all and
    /// the test would still pass. Standing up an actual <see cref="CarriableTool"/> and lifting it with an
    /// actual <see cref="CarryHands"/> means the chain under test is the chain that ships — Def id →
    /// <c>ICarriable.DefId</c> → the carrier's fake-null-laundered <c>Carried</c> → the Core relay → the
    /// gate. If any link of that breaks, these tests go red, which is the entire point of having them.</para>
    ///
    /// <para><b>And why it is SHARED rather than copied into each fixture.</b> This suite already carries
    /// ~20 hand-rolled <c>FakeHold</c> stubs that drifted apart (several omit <c>Clear()</c>), which is the
    /// standing argument for not starting a 21st family. One helper, two callers today, and the next test
    /// that needs a shovel in hand gets it right by default.</para>
    ///
    /// <para>Everything it spawns is appended to the caller's own teardown list, so a fixture that already
    /// destroys its <c>_spawned</c> needs no new teardown code. It does NOT register the tool with
    /// <see cref="Interactables"/> — lifting does not require registration, and a test that wants the
    /// resolver to see the tool should register it deliberately.</para>
    /// </summary>
    internal static class ToolInHand
    {
        /// <summary>
        /// Build a pair of hands holding a tool of the given id, and publish them on
        /// <see cref="GameServices.Hands"/> the way a live <see cref="CarryHands"/> publishes itself.
        ///
        /// <para>⚠️ The publish is explicit because EditMode never fires <c>OnEnable</c> on a plain
        /// MonoBehaviour — without this line the relay stays null and every gate reads "nothing in hand",
        /// which passes the negative tests for entirely the wrong reason.</para>
        /// </summary>
        /// <param name="toolId">The tool Def id to hold, e.g. <c>ToolDef.ShovelId</c>.</param>
        /// <param name="spawned">The caller's teardown list; everything created is added to it.</param>
        public static CarryHands Holding(string toolId, List<Object> spawned)
        {
            CarryHands hands = Empty(spawned);

            var def = ScriptableObject.CreateInstance<ToolDef>();
            def.Id = toolId;
            def.DisplayName = toolId;
            spawned.Add(def);

            var go = new GameObject($"Tool({toolId})");
            spawned.Add(go);
            var tool = go.AddComponent<CarriableTool>();
            tool.Configure(def, go.AddComponent<SpriteRenderer>(), $"{toolId}#test");

            CarryRefusal refusal = hands.TryPickUp(tool);
            if (refusal != CarryRefusal.None)
                Debug.LogError($"[ToolInHand] could not put {toolId} in hand: {refusal}");

            return hands;
        }

        /// <summary>Build an EMPTY pair of hands and publish them — the "you own it but you left it at
        /// home" case, which is the one the ruling actually changed.</summary>
        public static CarryHands Empty(List<Object> spawned)
        {
            var go = new GameObject("Player(hands)");
            spawned.Add(go);
            go.AddComponent<SpriteRenderer>();
            var hands = go.AddComponent<CarryHands>();

            // ⚠️ BOTH relays, because a live CarryHands publishes both in OnEnable — and publishing only
            // the read side would be worse than publishing neither. A catch source checks
            // GameServices.CatchHands to decide whether to land in the hand or fall back to the hold, so a
            // fixture that left it null would silently take the FALLBACK path while looking like it had
            // hands: every dig would fill the pail, the in-hand behaviour would never run, and the tests
            // would be green about the wrong game.
            GameServices.Hands = hands;
            GameServices.CatchHands = hands;
            return hands;
        }
    }
}
