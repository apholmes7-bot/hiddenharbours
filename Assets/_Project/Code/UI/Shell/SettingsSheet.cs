using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The settings sheet (M1 §7.8): four independent faders and fullscreen/windowed. Nothing else — no
    /// graphics presets, no key rebinding; those are explicitly not M1.
    ///
    /// <para><b>It moves the sound under your hand.</b> A fader writes straight through
    /// <see cref="IAudioMix"/> to the live director, because that is the only way a volume can be judged;
    /// the WRITING of it to disk waits until the sheet is put away (<see cref="GameSettings"/>), so a drag
    /// is not fifty file writes. With no director running, the faders are replaced by a line saying so
    /// rather than four sliders that move nothing (P1 truth).</para>
    ///
    /// <para><b>It is opened from two places and belongs to neither</b> — the title page and the pause menu
    /// both call <see cref="Open"/> with a way back. Closing calls that continuation rather than assuming
    /// where it came from, which is why there is one settings sheet in the project instead of two.</para>
    ///
    /// <para>It does not touch the clock: whoever opened it has already decided whether the world is
    /// stopped (at the title it is; under the pause menu it is). Built once on open, no per-frame work
    /// beyond the cancel key.</para>
    /// </summary>
    public sealed class SettingsSheet : MonoBehaviour
    {
        private static SettingsSheet _instance;

        /// <summary>True while the sheet is up.</summary>
        public static bool IsOpen => _instance != null;

        private const float MarginX   = 110f;
        private const float TitleY    = -132f;
        private const float RuleY     = -196f;
        private const float FirstRowY = -244f;
        private const float RowW      = 700f;
        private const float RowH      = 52f;
        private const float RowStep   = 62f;
        private const float HintY     = -664f;

        private Action _onClose;
        private Text _displayValue;

        /// <summary>
        /// Put the sheet up. <paramref name="onClose"/> is what to do when it is put away — the page that
        /// opened it comes back that way. Reuses the open sheet if there is one (and re-points its way
        /// back, so the most recent opener is the one returned to).
        /// </summary>
        public static SettingsSheet Open(Action onClose)
        {
            if (_instance == null)
            {
                var go = new GameObject("SettingsSheet");
                _instance = go.AddComponent<SettingsSheet>();
            }
            _instance._onClose = onClose;
            return _instance;
        }

        /// <summary>Take the sheet down without running the way back (a teardown, not a choice).</summary>
        public static void CloseIfOpen()
        {
            if (_instance == null) return;
            _instance._onClose = null;
            _instance.Close();
        }

        private void Awake()
        {
            PaperUi.EnsureEventSystem();
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Esc / gamepad East goes back — the project's shared Cancel convention.
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonEast.wasPressedThisFrame))
                Back();
        }

        /// <summary>Put the sheet away: remember the mix, then hand back to whoever opened it.</summary>
        private void Back()
        {
            GameSettings.StoreFrom(GameServices.AudioMix);

            Action onClose = _onClose;
            _onClose = null;
            Close();
            onClose?.Invoke();
        }

        // Release the slot NOW, not at end of frame (the tide table's lesson: a Destroy that has not
        // resolved would still answer IsOpen and be handed back by a same-frame Open).
        private void Close()
        {
            if (_instance == this) _instance = null;
            Destroy(gameObject);
        }

        // ---- the sheet --------------------------------------------------------------------------

        private void Build()
        {
            RectTransform host = PaperUi.MakeScreen(transform, "SettingsSheet_Canvas",
                                                    PaperUi.SettingsSortingOrder);
            PaperUi.MakeScrim(host, PaperUi.Scrim);

            PaperUi.MakeText(host, ShellStrings.SettingsTitle, 52, TextAnchor.UpperLeft,
                             MarginX, TitleY, 700f, 66f, PaperUi.Chalk);
            PaperUi.MakeRule(host, MarginX, RuleY, 520f);

            var rows = new System.Collections.Generic.List<Selectable>(6);
            float y = FirstRowY;

            IAudioMix mix = GameServices.AudioMix;
            if (mix == null)
            {
                // No director: say so. Four faders that move nothing would be a lie about the build.
                PaperUi.MakeText(host, ShellStrings.AudioUnavailable, 26, TextAnchor.UpperLeft,
                                 MarginX, y, RowW, 40f, PaperUi.ChalkFaint);
                y -= RowStep;
            }
            else
            {
                rows.Add(PaperUi.MakeSlider(host, "Master", ShellStrings.VolumeMaster,
                                            mix.MasterVolume, MarginX, y, RowW, RowH,
                                            v => mix.MasterVolume = v));
                y -= RowStep;
                rows.Add(PaperUi.MakeSlider(host, "Ambience", ShellStrings.VolumeAmbience,
                                            mix.AmbienceVolume, MarginX, y, RowW, RowH,
                                            v => mix.AmbienceVolume = v));
                y -= RowStep;
                rows.Add(PaperUi.MakeSlider(host, "Sfx", ShellStrings.VolumeSfx,
                                            mix.SfxVolume, MarginX, y, RowW, RowH,
                                            v => mix.SfxVolume = v));
                y -= RowStep;
                rows.Add(PaperUi.MakeSlider(host, "Music", ShellStrings.VolumeMusic,
                                            mix.MusicVolume, MarginX, y, RowW, RowH,
                                            v => mix.MusicVolume = v));
                y -= RowStep;
            }

            y -= 16f;   // a breath between the sound and the window
            rows.Add(PaperUi.MakeValueRow(host, "Display", ShellStrings.DisplayLabel,
                                          ShellStrings.DisplayMode(GameSettings.Fullscreen),
                                          MarginX, y, RowW, RowH, ToggleDisplay, out _displayValue));
            y -= RowStep + 16f;

            rows.Add(PaperUi.MakeMenuItem(host, "Back", ShellStrings.Back,
                                          MarginX, y, 380f, RowH, Back));

            PaperUi.WireVerticalNavigation(rows.ToArray());

            PaperUi.MakeText(host, ShellStrings.SettingsHint, 22, TextAnchor.UpperLeft,
                             MarginX, HintY, 700f, 32f, PaperUi.ChalkFaint);
        }

        /// <summary>Fullscreen ⇄ windowed, applied and remembered at once, and the row says which it now
        /// is. Reads the screen back rather than assuming the flip took — a display mode is a request to
        /// the platform, not a variable.</summary>
        private void ToggleDisplay()
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            if (_displayValue != null)
                _displayValue.text = ShellStrings.DisplayMode(GameSettings.Fullscreen);
        }
    }
}
