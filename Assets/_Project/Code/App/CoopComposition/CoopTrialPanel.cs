using System;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>
    /// The plain Host and Join entry of the two-PC trial, and the one status line a session shows.
    ///
    /// <para><b>Off by default.</b> The entry is offered only by an application built with the trial
    /// flag (<see cref="BuildFlag"/>), or by a development build started with
    /// <see cref="MenuArgument"/>. The ordinary game, the editor and the tests never create it.</para>
    ///
    /// <para>Host and Join start the application again with the trial's launch options. A session
    /// therefore always begins from a fresh start that has read no save and can write none.</para>
    /// </summary>
    public sealed class CoopTrialPanel : MonoBehaviour
    {
        public const string BuildFlag = "COOP_TRIAL_MENU";
        public const string MenuArgument = "--coop-trial-menu";
#if COOP_TRIAL_MENU
        public const bool BuiltWithFlag = true;
#else
        public const bool BuiltWithFlag = false;
#endif
        // Above the title page, below its settings sheet.
        private const int SortingOrder = 305;
        private static readonly Vector2 Reference = new Vector2(1280f, 720f);
        private static readonly Color Paper = new Color(0.035f, 0.055f, 0.082f, 0.92f);
        private static readonly Color Chalk = new Color(0.937f, 0.914f, 0.855f, 1f);
        private static readonly Color Strip = new Color(0.937f, 0.914f, 0.855f, 0.14f);
        private static readonly Color Warn = new Color(0.847f, 0.588f, 0.451f, 1f);
        private const float MenuW = 440f, MenuH = 236f, Pad = 16f, RowH = 40f, StatusH = 54f, StatusX = 200f;

        private Canvas _canvas;
        private Text _message;
        private InputField _address;
        private CoopDevStart _session;
        private int _shownVersion = -1;
        private bool _menu;
        private ushort _port;

        /// <summary>Whether the Host and Join entry exists in this run. Pure, so it can be pinned by a test.</summary>
        public static bool MenuOffered(bool builtWithFlag, bool developmentBuild, bool editor, bool batchMode,
            string[] args)
        {
            if (editor || batchMode || CoopDevStart.HasTrialArgument(args)) return false;
            if (builtWithFlag) return true;
            return developmentBuild && args != null && Array.IndexOf(args, MenuArgument) >= 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!MenuOffered(BuiltWithFlag, Debug.isDebugBuild, Application.isEditor, Application.isBatchMode,
                System.Environment.GetCommandLineArgs())) return;
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            if (settings == null) return;
            var root = new GameObject("CoopTrialMenu");
            DontDestroyOnLoad(root);
            var panel = root.AddComponent<CoopTrialPanel>();
            panel.ShowMenu(settings.Port);
            panel._press = System.Environment.GetEnvironmentVariable(PressVariable);
        }

        // A check without a hand on the mouse: "host", or "join" followed by a space and an
        // address, presses that entry once the title page is up. Unset in ordinary use.
        public const string PressVariable = "COOP_TRIAL_MENU_PRESS";
        private const string PressHost = "host", PressJoin = "join ";
        private const float PressAfterSeconds = 3f;
        private string _press;

        private void PressIfAsked()
        {
            if (string.IsNullOrEmpty(_press) || !ShellFlow.AtTitle || Time.realtimeSinceStartup < PressAfterSeconds) return;
            string press = _press;
            _press = null;
            // The session that starts must not press anything itself.
            System.Environment.SetEnvironmentVariable(PressVariable, null);
            if (press == PressHost) OnHost();
            else if (press.StartsWith(PressJoin, StringComparison.Ordinal))
            {
                _address.text = press.Substring(PressJoin.Length);
                OnJoin();
            }
        }

        public void ShowMenu(ushort port)
        {
            _menu = true;
            _port = port;
            RectTransform box = MakeBox(new Vector2(1f, 1f), new Vector2(-Pad, -Pad), new Vector2(MenuW, MenuH));
            MakeText(box, "Two-PC sea trial (development build)", 20, Pad, -Pad, MenuW - 2 * Pad, 28f, Chalk);
            MakeButton(box, "Host a session", Pad, -Pad - 34f, MenuW - 2 * Pad, RowH, OnHost);
            MakeText(box, "Or type the host's address, then Join:", 16, Pad, -Pad - 84f, MenuW - 2 * Pad, 22f, Chalk);
            _address = MakeField(box, Pad, -Pad - 110f, MenuW - 2 * Pad - 110f, RowH);
            MakeButton(box, "Join", MenuW - Pad - 100f, -Pad - 110f, 100f, RowH, OnJoin);
            _message = MakeText(box, "Nothing is saved in a session. Close the window to leave.", 15,
                Pad, -Pad - 158f, MenuW - 2 * Pad, 60f, Chalk);
        }

        public void ShowSession(CoopDevStart session)
        {
            _session = session;
            // Clear of the watch in the top left corner.
            RectTransform box = MakeBox(new Vector2(0f, 1f), new Vector2(StatusX, -Pad),
                new Vector2(Reference.x - StatusX - Pad, StatusH));
            _message = MakeText(box, session.Status, 16, Pad, -6f, Reference.x - StatusX - 3 * Pad,
                StatusH - 12f, Chalk);
            _shownVersion = session.StatusVersion;
        }

        private void Update()
        {
            if (_menu)
            {
                // The entry belongs to the title page only.
                if (_canvas.enabled != ShellFlow.AtTitle) _canvas.enabled = ShellFlow.AtTitle;
                PressIfAsked();
                return;
            }
            if (_session == null || _shownVersion == _session.StatusVersion) return;
            _shownVersion = _session.StatusVersion;
            _message.text = _session.Status;
        }

        private void OnHost() => Relaunch(CoopLaunchOptions.Arguments(false, null, _port));

        private void OnJoin()
        {
            string typed = _address != null ? _address.text.Trim() : string.Empty;
            if (!CoopLaunchOptions.TryAddress(typed, out string address))
            {
                Tell("That is not an address. It is four numbers with dots, like 192.168.1.20. " +
                     "The host's own screen shows his.", true);
                return;
            }
            Relaunch(CoopLaunchOptions.Arguments(true, address, _port));
        }

        private void Relaunch(string[] arguments)
        {
            try
            {
                string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                var start = new System.Diagnostics.ProcessStartInfo(exe, string.Join(" ", arguments))
                { UseShellExecute = false };
                System.Diagnostics.Process.Start(start);
                Application.Quit();
            }
            catch (Exception error)
            {
                Debug.LogError("[Coop] Could not restart into a session: " + error.Message);
                Tell("Could not restart into a session. Start the game from PowerShell with: " +
                     string.Join(" ", arguments), true);
            }
        }

        private void Tell(string text, bool warning)
        {
            _message.text = text;
            _message.color = warning ? Warn : Chalk;
        }

        // ---- a few plain controls; the trial owns no art ------------------------------------

        private RectTransform MakeBox(Vector2 corner, Vector2 offset, Vector2 size)
        {
            var canvasGo = new GameObject("CoopTrialPanel_Canvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = corner;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = Paper;
            return rt;
        }

        private static RectTransform Place(GameObject go, RectTransform parent, float x, float y, float w, float h)
        {
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static Font PlainFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Text MakeText(RectTransform parent, string text, int size, float x, float y, float w,
            float h, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            Place(go, parent, x, y, w, h);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = PlainFont();
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void MakeButton(RectTransform parent, string label, float x, float y, float w, float h,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rt = Place(go, parent, x, y, w, h);
            var image = go.GetComponent<Image>();
            image.color = Strip;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            // The title page steers its own lines with the keyboard; this entry is clicked.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);
            Text text = MakeText(rt, label, 20, 0f, 0f, w, h, Chalk);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static InputField MakeField(RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject("Address", typeof(RectTransform), typeof(Image), typeof(InputField));
            RectTransform rt = Place(go, parent, x, y, w, h);
            var image = go.GetComponent<Image>();
            image.color = Strip;
            Text text = MakeText(rt, string.Empty, 20, 8f, -8f, w - 16f, h - 16f, Chalk);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;
            Text hint = MakeText(rt, "192.168.1.20", 20, 8f, -8f, w - 16f, h - 16f,
                new Color(Chalk.r, Chalk.g, Chalk.b, 0.4f));
            var field = go.GetComponent<InputField>();
            field.targetGraphic = image;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = 15;
            field.lineType = InputField.LineType.SingleLine;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            return field;
        }
    }
}
