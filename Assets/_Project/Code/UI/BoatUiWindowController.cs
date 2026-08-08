using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// One host's WINDOW glue (2026-08-07 windowing ruling): applies the shared window state to the
    /// host's base card rect, draws the hover chrome, and turns chrome presses into moves, resizes,
    /// collapse-tier steps and hides. A plain class each host owns — the state itself lives in
    /// <see cref="BoatUiWindows"/> (shared), the maths in <see cref="BoatUiWindowLayout"/> (pure);
    /// this is only the per-frame plumbing between them and a canvas.
    ///
    /// <para><b>Call order per frame (the S1 discipline):</b> <see cref="Apply"/> to window the base
    /// rect BEFORE anything consumes it (flush mounts, hit maps, publication), then
    /// <see cref="HandlePointer"/> BEFORE the host's own pointer read — a press the chrome consumed
    /// must never also focus, collapse or steer.</para>
    ///
    /// <para><b>Never steals a control drag.</b> A live steer/lever/measure session passes
    /// <c>controlDragLive</c> and the chrome refuses to start a session under it; conversely, while a
    /// window drag is live, <see cref="BoatUiWindows.AnySession"/> tells every host to leave the
    /// pointer alone. Dragging a window moves RawImage rects only — no raster, no repaint
    /// (rule 7).</para>
    /// </summary>
    public sealed class BoatUiWindowController
    {
        private readonly BoatUiWindowId _id;
        private BoatUiWindowChrome _chrome;

        private Rect _card;          // the final windowed card rect this frame
        private bool _applied;       // Apply ran this frame's path (chrome may draw)

        // Drag-session anchors (captured at the press that began it).
        private Vector2 _startPointer;
        private Vector2 _startCenterPx;
        private float _startScale;
        private float _startCardW;

        public BoatUiWindowController(BoatUiWindowId id) { _id = id; }

        /// <summary>Is this window on screen at all (hide-all + individual hide)?</summary>
        public bool Shown => BoatUiWindows.IsShown(_id);

        /// <summary>The card rect <see cref="Apply"/> produced this frame (tests).</summary>
        public Rect Card => _card;

        /// <summary>
        /// Window the base rect: the shipped layout in, the player's layout out. Publishes the
        /// chrome bounds for the cross-host press routing. An untouched window returns
        /// <paramref name="baseRect"/> bit-exact.
        /// </summary>
        public Rect Apply(Rect baseRect, float screenW, float screenH, float reservedTopPx)
        {
            BoatUiWindowSettings cfg = GameServices.BoatUiWindowing;
            BoatUiWindowState st = BoatUiWindows.State(_id);
            _card = BoatUiWindowLayout.WindowRect(baseRect, in st, in cfg, screenW, screenH,
                                                  reservedTopPx);
            BoatUiWindows.PublishChrome(_id, BoatUiWindowLayout.ChromeBounds(_card, in cfg));
            _applied = true;
            return _card;
        }

        /// <summary>Whether the card's picture should draw at all this frame (false while
        /// BAR-collapsed — the strip is all there is, and the raster is skipped outright).</summary>
        public bool CardVisible => _card.height > 0f && _card.width > 0f;

        /// <summary>
        /// Draw or hide the hover chrome. Visible while the pointer is over the window (card or
        /// strip), while this window's drag is live, or always when BAR-collapsed. Builds its
        /// objects lazily under <paramref name="canvasParent"/> on first need.
        /// </summary>
        public void LayoutChrome(Transform canvasParent)
        {
            if (!_applied) { HideChrome(); return; }
            _applied = false;

            BoatUiWindowSettings cfg = GameServices.BoatUiWindowing;
            BoatUiWindowState st = BoatUiWindows.State(_id);

            bool hover = false;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                hover = _card.Contains(pos)
                     || BoatUiWindowLayout.BarRect(_card, in cfg).Contains(pos);
            }
            bool show = hover
                     || BoatUiWindows.SessionKind(_id) != BoatUiDragKind.None
                     || st.Collapse == BoatUiCollapse.Bar;
            if (!show) { HideChrome(); return; }

            _chrome ??= new BoatUiWindowChrome(canvasParent, "WindowChrome");
            _chrome.Show(_card, st.Collapse, in cfg);
        }

        /// <summary>
        /// Read the pointer for the chrome. Returns TRUE when the chrome owns the pointer this frame
        /// (a live move/resize, or a press that landed on the strip, a button or the grip) — the
        /// host must then skip its own pointer read entirely.
        /// </summary>
        public bool HandlePointer(bool controlDragLive)
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            Vector2 pos = mouse.position.ReadValue();
            bool held = mouse.leftButton.isPressed;

            BoatUiDragKind mine = BoatUiWindows.SessionKind(_id);
            if (mine != BoatUiDragKind.None)
            {
                if (!held)
                {
                    BoatUiWindows.EndSession(_id);
                    return true;   // consume the release frame — it is not a click
                }
                BoatUiWindowSettings cfg = GameServices.BoatUiWindowing;
                BoatUiWindowState st = BoatUiWindows.State(_id);
                if (mine == BoatUiDragKind.Move)
                {
                    st.HasCustomCenter = true;
                    st.Center01 = BoatUiWindowLayout.DragCenter01(
                        _startCenterPx, _startPointer, pos, Screen.width, Screen.height);
                }
                else
                {
                    st.Scale = BoatUiWindowLayout.DragScale(
                        _startScale, _startCardW, _startPointer, pos, in cfg);
                }
                BoatUiWindows.SetState(_id, in st);
                return true;
            }

            if (BoatUiWindows.AnySession) return false;    // another window owns the pointer
            if (controlDragLive) return false;             // never steal a live steer/control drag
            if (!mouse.leftButton.wasPressedThisFrame) return false;

            BoatUiWindowSettings cfgHit = GameServices.BoatUiWindowing;
            BoatUiChromeHit hit = BoatUiWindowLayout.HitChrome(pos, _card, in cfgHit);
            switch (hit)
            {
                case BoatUiChromeHit.Hide:
                    BoatUiWindows.SetHidden(_id, true);
                    return true;

                case BoatUiChromeHit.Collapse:
                {
                    BoatUiWindowState st = BoatUiWindows.State(_id);
                    st.Collapse = BoatUiWindows.NextCollapse(st.Collapse);
                    BoatUiWindows.SetState(_id, in st);
                    return true;
                }

                case BoatUiChromeHit.Bar:
                {
                    if (!BoatUiWindows.BeginSession(_id, BoatUiDragKind.Move)) return true;
                    _startPointer = pos;
                    _startCenterPx = _card.height > 0f
                        ? _card.center
                        // A BAR-collapsed window has no card height; drag by where the card's
                        // centre WOULD be so expanding after a move lands where the bar is.
                        : new Vector2(_card.center.x, _card.yMin);
                    return true;
                }

                case BoatUiChromeHit.Grip:
                {
                    if (!BoatUiWindows.BeginSession(_id, BoatUiDragKind.Resize)) return true;
                    BoatUiWindowState st = BoatUiWindows.State(_id);
                    _startPointer = pos;
                    _startScale = st.Scale > 0f ? st.Scale : 1f;
                    _startCardW = _card.width;
                    return true;
                }

                default:
                    return false;
            }
        }

        /// <summary>The host stood down (no card this frame): chrome off, publication withdrawn,
        /// any live drag released.</summary>
        public void StandDown()
        {
            HideChrome();
            BoatUiWindows.ClearChrome(_id);
            BoatUiWindows.EndSession(_id);
        }

        private void HideChrome() => _chrome?.Hide();
    }
}
