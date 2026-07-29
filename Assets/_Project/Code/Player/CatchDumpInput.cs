using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The DISPOSE verb's input (M1 §7.3): press the dump key to tip every SPOILED unit out of every
    /// hold in reach — the clam pail on your arm, the tray on the deck. Only rubbish goes over the
    /// side (<see cref="CatchDisposal.DumpSpoiled"/> never touches sellable catch), so the key is
    /// always safe to press; without it a fully-spoiled hold is a soft-lock.
    ///
    /// <para><b>Wiring.</b> Self-installed by <see cref="ClamBucket"/> at play (the ShipHold →
    /// DeckContainerPresenter pattern — no builder re-run), one instance per scene. Holds are found
    /// through the Core <see cref="IHold"/> contract only — a scene scan on the key press, never per
    /// frame (rule 7), and never a concrete Boats/App type (rule 4). The persistent proxy forwarding
    /// to a hold already emptied is harmless: a second dump finds nothing spoiled.</para>
    ///
    /// <para>New Input System only (<see cref="Keyboard.current"/>) — legacy <c>UnityEngine.Input</c>
    /// compiles here and then throws. Feedback rides <see cref="DevNotice"/> (the greybox toast), so
    /// the press is never silent. ui-ux owns the eventual real Interact mapping (§7.6).</para>
    /// </summary>
    public sealed class CatchDumpInput : MonoBehaviour
    {
        [Tooltip("Key that dumps all SPOILED catch overboard / out of the pail. Safe: never touches " +
                 "sellable catch.")]
        [SerializeField] private Key _dumpKey = Key.X;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb[_dumpKey].wasPressedThisFrame) return;
            DumpSpoiledEverywhere();
        }

        /// <summary>Dump spoiled catch from every hold in the scene. Public + static so tests and the
        /// eventual real interact verb can invoke the same logic without a key press.</summary>
        public static int DumpSpoiledEverywhere()
        {
            SpoilContext spoil = SpoilContext.Capture();
            int dumped = 0;

            // Resolve holds via the Core contract only: scan MonoBehaviours for IHold implementers.
            // Key-press-time only — this is never a per-frame cost.
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IHold hold)
                    dumped += CatchDisposal.DumpSpoiled(hold, spoil);

            EventBus.Publish(new DevNotice(dumped > 0
                ? $"Dumped {dumped} spoiled catch over the side."
                : "Nothing's gone off — the catch is keeping."));
            return dumped;
        }
    }
}
