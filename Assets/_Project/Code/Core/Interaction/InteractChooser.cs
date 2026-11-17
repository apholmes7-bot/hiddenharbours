namespace HiddenHarbours.Core
{
    /// <summary>
    /// The chooser opened, closed, or moved its selection. Presentation only: the view repaints on it.
    /// <see cref="Revision"/> rises on every change, so a view that missed a publish (a test's
    /// <c>EventBus.Clear</c>) can reconcile by comparing one int.
    /// </summary>
    public readonly struct InteractChooserChanged
    {
        public readonly bool IsOpen;
        public readonly int Selected;
        public readonly int Revision;

        public InteractChooserChanged(bool isOpen, int selected, int revision)
        {
            IsOpen = isOpen;
            Selected = selected;
            Revision = revision;
        }
    }

    /// <summary>
    /// <b>The open chooser</b> — the small list the player picks an action from when more than one is in
    /// reach (owner ask 2026-11-15). Opened by <see cref="InteractVerb.TryPress"/>, driven by the UI's
    /// view (keys, number keys, mouse), and closed by a confirm or a cancel.
    ///
    /// <para><b>While it is open it owns the interact key and the move axis</b>, the picker contract the
    /// project already has: it raises <see cref="InteractionGate"/> (so no other reader of E acts under
    /// it, and the popup steps aside for the list) and <see cref="MoveActionClaim"/> (so W/S and the
    /// arrows move the selection, not the fisher). It releases only what it raised.</para>
    ///
    /// <para><b>The list is frozen at the open</b>, so rows do not reshuffle under the cursor. A confirm
    /// re-checks the picked thing live — still registered, still in reach, still free — and does nothing
    /// if it is not, rather than acting on a thing that walked away.</para>
    ///
    /// <para>Static, like the other interaction seams; <see cref="Reset"/> for teardown and tests.</para>
    /// </summary>
    public static class InteractChooser
    {
        private static readonly InteractChoiceList OpenRows = new InteractChoiceList();
        private static InteractActor _actor;
        private static float _arc;
        private static bool _heldGate;
        private static bool _heldMove;
        private static bool _heldCancel;
        private static bool _unregistered;

        /// <summary>True while the list is up.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>The frozen rows. Read only; empty when closed.</summary>
        public static InteractChoiceList Rows => OpenRows;

        /// <summary>Rises on every open, close and selection change.</summary>
        public static int Revision { get; private set; }

        private static int _openedFrame = -1;
        private static int _closedFrame = -1;

        private static readonly System.Func<int> UnityFrame = () => UnityEngine.Time.frameCount;

        /// <summary>The frame counter the open/close guards compare against: Unity's frame count. A test
        /// steps its own (EditMode has no frame loop, so <c>Time.frameCount</c> stands still there).
        /// Null restores Unity's; <see cref="Reset"/> does too.</summary>
        public static System.Func<int> FrameClock
        {
            get => _frameClock;
            set => _frameClock = value ?? UnityFrame;
        }

        private static System.Func<int> _frameClock = UnityFrame;

        /// <summary>True on the frame the list went up. The press that opened it is still "pressed this
        /// frame", so the view ignores input until the next frame or that press would also confirm.</summary>
        public static bool OpenedThisFrame => IsOpen && _openedFrame == _frameClock();

        /// <summary>True on the frame the list went down. The confirm press is still "pressed this frame",
        /// so <see cref="InteractVerb.TryPress"/> swallows it rather than reopening the list under a reader
        /// that runs later in the same frame.</summary>
        public static bool ClosedThisFrame => !IsOpen && _closedFrame == _frameClock();

        /// <summary>
        /// Put <paramref name="built"/> up. Refused (false) when it is not a choice, when the chooser is
        /// already open, or when a modal already holds the key.
        /// </summary>
        public static bool Open(InteractChoiceList built, in InteractActor actor, float facingArcDegrees)
            => OpenRowsFrom(built, actor, facingArcDegrees, unregistered: false);

        /// <summary>
        /// Put up rows its owner built for one thing that is NOT in <see cref="Interactables"/> (a villager,
        /// found by the conversation driver: <see cref="InteractChoiceList.BuildActionsOf"/>). The same
        /// refusals and holds as <see cref="Open"/>; a confirm re-checks the thing's context, availability,
        /// reach and facing live, but not its registration, which it never had.
        /// </summary>
        public static bool OpenUnregistered(InteractChoiceList built, in InteractActor actor, float facingArcDegrees)
            => OpenRowsFrom(built, actor, facingArcDegrees, unregistered: true);

        private static bool OpenRowsFrom(InteractChoiceList built, in InteractActor actor, float facingArcDegrees,
                                         bool unregistered)
        {
            if (IsOpen || built == null || !built.IsChoice || InteractionGate.IsBlocked) return false;
            _unregistered = unregistered;
            OpenRows.CopyFrom(built);
            _actor = actor;
            _arc = facingArcDegrees;
            IsOpen = true;
            _openedFrame = _frameClock();

            InteractionGate.IsBlocked = true;
            _heldGate = true;
            if (!MoveActionClaim.IsClaimed)
            {
                MoveActionClaim.IsClaimed = true;
                _heldMove = true;
            }
            // Esc puts the list down, so it must not also open the pause menu (ShellPresenter reads this
            // claim, closing frame included).
            if (!CancelKeyClaim.IsHeld)
            {
                CancelKeyClaim.Hold();
                _heldCancel = true;
            }
            Announce();
            return true;
        }

        /// <summary>One row down (&gt; 0) or up (&lt; 0), wrapping, skipping greyed rows.</summary>
        public static bool Step(int direction)
        {
            if (!IsOpen || !OpenRows.Step(direction)) return false;
            Announce();
            return true;
        }

        /// <summary>Move the selection to row <paramref name="index"/> (a hover). Greyed rows refuse.</summary>
        public static bool Select(int index)
        {
            if (!IsOpen || index == OpenRows.Selected || !OpenRows.TrySelect(index)) return false;
            Announce();
            return true;
        }

        /// <summary>Select row <paramref name="index"/> and confirm it (a number key, a click). A greyed
        /// row refuses and the list stays up.</summary>
        public static bool Pick(int index)
        {
            if (!IsOpen || !OpenRows.TrySelect(index)) return false;
            return Confirm();
        }

        /// <summary>
        /// Act on the selected row: close the list, re-check the thing live, and perform it through
        /// <see cref="InteractVerb"/>. False only when nothing could be confirmed (closed, or no pickable
        /// row) — the list then stays as it was.
        /// </summary>
        public static bool Confirm()
        {
            if (!IsOpen) return false;
            InteractChoice choice = OpenRows[OpenRows.Selected];
            if (choice.Candidate == null || !choice.Enabled) return false;

            InteractActor actor = InteractActorProbe.Has ? InteractActorProbe.Current : _actor;
            float arc = _arc;
            bool unregistered = _unregistered;
            Close();

            if (!StillWorkable(choice, actor, arc, unregistered)) return true;   // it went away under the cursor
            InteractVerb.Perform(choice.Candidate, choice.Action, actor);
            return true;
        }

        /// <summary>Put the list down without doing anything.</summary>
        public static void Cancel()
        {
            if (IsOpen) Close();
        }

        /// <summary>Close and forget (teardown, tests). Releases only what this raised.</summary>
        public static void Reset()
        {
            if (IsOpen) Close();
            OpenRows.Clear();
            _openedFrame = -1;
            _closedFrame = -1;
            _frameClock = UnityFrame;
        }

        private static bool StillWorkable(in InteractChoice choice, in InteractActor actor, float arc, bool unregistered)
        {
            IInteractable c = choice.Candidate;
            // An unregistered list skips the registry check (its thing was never in it); the rest still runs.
            bool found = unregistered;
            var live = Interactables.Active;
            for (int i = 0; i < live.Count && !found; i++)
                if (ReferenceEquals(live[i], c)) found = true;
            if (!found) return false;
            if (c is IExclusiveInteractable ex && ex.IsOccupied) return false;
            return InteractResolver.Qualifies(c, actor, actor.Facing, InteractArc.CosHalfArc(arc), out _);
        }

        private static void Close()
        {
            IsOpen = false;
            _closedFrame = _frameClock();
            if (_heldGate) InteractionGate.IsBlocked = false;
            if (_heldMove) MoveActionClaim.IsClaimed = false;
            if (_heldCancel) CancelKeyClaim.Release();
            _heldGate = false;
            _heldMove = false;
            _heldCancel = false;
            _unregistered = false;
            OpenRows.Clear();
            Announce();
        }

        private static void Announce()
        {
            Revision++;
            EventBus.Publish(new InteractChooserChanged(IsOpen, OpenRows.Selected, Revision));
        }
    }
}
