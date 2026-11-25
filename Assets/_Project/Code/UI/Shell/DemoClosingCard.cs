using System;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>The demo's closing card.</b> A plain page in the shell's own type and colours, shown when the
    /// island week ends in a demo (<see cref="DemoEnded"/>): a title, two lines, and one way on, back to
    /// the title. The world is stopped behind it, exactly as it is behind the pause menu.
    ///
    /// <para>It decides nothing. Whether the demo ended is Core's (<see cref="DemoWeek"/>); leaving is
    /// <see cref="ShellFlow.QuitToTitle"/>, which saves first. In the full game the signal is never
    /// raised, so the card is never built.</para>
    ///
    /// <para>Every player in the game is shown it: the signal carries no player, and each machine
    /// builds its own card. Only the host's leaving writes the save.</para>
    /// </summary>
    public sealed class DemoClosingCard : MonoBehaviour
    {
        private static DemoClosingCard _instance;
        private static bool _listening;

        /// <summary>The way on from the card. Unset, it is the shell's quit to the title, which saves
        /// first. A seam so the card can be driven without rebuilding the world.</summary>
        public static Action Leave { get; set; }

        /// <summary>True while the card is on screen.</summary>
        public static bool IsOpen => _instance != null;

        // Layout, in the 1280x720 reference frame the shell pages share.
        private const float MarginX = 110f;
        private const float TitleY  = -200f;
        private const float RuleY   = -264f;
        private const float Line1Y  = -300f;
        private const float LineH   = 40f;
        private const float TextW   = 1000f;
        private const float ItemY   = -430f;
        private const float ItemW   = 420f;
        private const float ItemH   = 54f;
        private const float SavedY  = -500f;

        private bool _gateWasBlocked;
        private bool _released;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _listening = false;
            Leave = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Listen();

        /// <summary>Listen for the demo's end. Safe to call again.</summary>
        public static void Listen()
        {
            EventBus.Unsubscribe<DemoEnded>(OnDemoEnded);
            EventBus.Subscribe<DemoEnded>(OnDemoEnded);
            _listening = true;
        }

        /// <summary>True once <see cref="Listen"/> has run in this session.</summary>
        public static bool Listening => _listening;

        private static void OnDemoEnded(DemoEnded e) => Open();

        /// <summary>Put the card up. A second call while it is up does nothing.</summary>
        public static DemoClosingCard Open()
        {
            if (_instance == null)
            {
                var go = new GameObject("DemoClosingCard");
                _instance = go.AddComponent<DemoClosingCard>();
            }
            return _instance;
        }

        /// <summary>Take the card down without leaving the world (teardown, tests).</summary>
        public static void CloseIfOpen()
        {
            if (_instance != null) _instance.Close();
        }

        private void Awake()
        {
            PaperUi.EnsureEventSystem();
            ShellPause.Pause();
            _gateWasBlocked = InteractionGate.IsBlocked;
            InteractionGate.IsBlocked = true;
            Build();
        }

        private void OnDestroy()
        {
            Release();
            if (_instance == this) _instance = null;
        }

        private void Release()
        {
            if (_released) return;
            _released = true;
            ShellPause.Resume();
            InteractionGate.IsBlocked = _gateWasBlocked;
        }

        private void Close()
        {
            Release();
            if (_instance == this) _instance = null;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void Build()
        {
            RectTransform host = PaperUi.MakeScreen(transform, "DemoClosingCard_Canvas", PaperUi.PauseSortingOrder);
            PaperUi.MakeScrim(host, PaperUi.Scrim);

            PaperUi.MakeText(host, ShellStrings.DemoEndTitle, 52, TextAnchor.UpperLeft,
                             MarginX, TitleY, TextW, 66f, PaperUi.Chalk);
            PaperUi.MakeRule(host, MarginX, RuleY, 520f);
            PaperUi.MakeText(host, ShellStrings.DemoEndLine1, 26, TextAnchor.UpperLeft,
                             MarginX, Line1Y, TextW, LineH, PaperUi.Chalk);
            PaperUi.MakeText(host, ShellStrings.DemoEndLine2, 26, TextAnchor.UpperLeft,
                             MarginX, Line1Y - LineH, TextW, LineH, PaperUi.Chalk);

            Button leave = PaperUi.MakeMenuItem(host, "BackToTitle", ShellStrings.DemoEndLeave,
                                                MarginX, ItemY, ItemW, ItemH, OnLeave);
            PaperUi.WireVerticalNavigation(new Selectable[] { leave });

            PaperUi.MakeText(host, ShellStrings.DemoEndSaved, 22, TextAnchor.UpperLeft,
                             MarginX + 36f, SavedY, TextW, 30f, PaperUi.ChalkFaint);
        }

        private void OnLeave()
        {
            Close();                    // hand the clock back before the world is rebuilt
            if (Leave != null) Leave();
            else ShellFlow.QuitToTitle();   // saves first, then returns to the title
        }
    }
}
