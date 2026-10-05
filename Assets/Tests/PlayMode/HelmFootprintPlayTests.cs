using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.UI;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// THE HELM DASH BAND, H1 — <b>the helm's footprint, end to end</b> (ADR 0050). The helm host says
    /// what its card covers through the Core seam (<see cref="HelmFootprint"/>); the overlays that can
    /// stand over the helm read it and keep out of it, and the camera reads it and moves for a
    /// full-width band only. Real frames, the real host, the real overlays and the real follow — nothing
    /// stubbed between the card and the things that keep clear of it.
    ///
    /// <para><b>Which overlays.</b> Of the four #416 named, three draw nothing of their own on this base:
    /// the helm hint and the interact prompt went into the interact popup (2026-08-19), the onboarding
    /// banner into the quest note (2026-08-22). What can stand over the helm is the dev toast, the popup
    /// and the note, so all three are up here, with the camera.</para>
    ///
    /// <para>Each overlay is checked twice: it stands where its own layout puts it for what the seam
    /// says (so it read the seam, in a real frame), and its box on THIS run's screen does not meet what
    /// is covered. The screen is whatever the runner gave; nothing here assumes a size. Waits run on the
    /// camera's own clock (<see cref="Time.deltaTime"/>), never on a frame count.</para>
    /// </summary>
    public class HelmFootprintPlayTests
    {
        // The toast's authored stack (DevToast's serialized defaults): the lowest line, the spacing,
        // the pool.
        private const float ToastBaseY = 230f, ToastSpacing = 38f;
        private const int ToastSlots = 4;
        private const float Eps = 0.01f;
        private const float PlaceTolerance = 1e-3f;
        private const int FrameCap = 2000;   // a stalled clock ends the wait; the assertion then says so

        private readonly List<Object> _spawned = new();
        private GameObject _popupHost;   // only set when THIS test had to stand the popup up

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            BoatUiWindows.Reset();
            HelmInstrumentExpansion.Collapse();
            HelmFootprint.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            BoatUiWindows.Reset();
            HelmInstrumentExpansion.Collapse();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            if (_popupHost != null) { Object.Destroy(_popupHost); _popupHost = null; }
            HelmFootprint.Reset();
        }

        // ---- today's card: said, kept clear of, and no camera move ---------------------------------

        [UnityTest]
        public IEnumerator TodaysCard_IsWhatTheSeamSays_TheOverlaysStandClear_AndTheCameraHoldsStill()
        {
            AssertTheScreenHasSize();
            HelmOverlayHost host = Host();
            DevToast toast = NewToast();
            QuestPanelPresenter note = NewNote();
            InteractPopup popup = Popup();
            var (boatGo, boat) = NewBoat();
            CameraFollow follow = NewCamera(boatGo.transform);
            yield return null;

            // The outboard tiller: an engine hull with no console.
            boat.SetHull(NewHull("boat.test_footprint_tiller", null));
            toast.Show("A word while she steers");
            note.Show("A line on the note while she steers");
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Assert.AreEqual(0f, follow.BandShiftMeters, "a card never moves the camera (the tiller's)");
            }
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Tiller), "premise: the tiller's card");
            Rect tiller = HelmOverlayLayout.CardRect(false, TillerRigRender.W, TillerRigRender.H,
                                                     GameServices.HelmOverlay, Screen.width, Screen.height);
            Assert.AreEqual(HelmOverlayHost.FootprintOf(tiller, GameServices.BoatUiWindowing.TitleBarPx),
                            HelmFootprint.Current,
                            "⭐ the seam says the tiller's card: its window, title strip included");
            AssertEveryOverlayStandsClear("the tiller's card", toast, note, popup);

            // The composed dash: a console hull, same boat.
            boat.SetHull(NewHull("boat.test_footprint_dash", NewConsole("footprint_dash")));
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Assert.AreEqual(0f, follow.BandShiftMeters, "a card never moves the camera (the dash)");
            }
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash), "premise: the dash");
            Assert.That(HelmOverlayHost.TryDashCard(out Rect dash, out _), Is.True, "premise: the dash draws");
            Assert.AreEqual(HelmOverlayHost.FootprintOf(dash, GameServices.BoatUiWindowing.TitleBarPx),
                            HelmFootprint.Current, "⭐ the seam says the dash, wherever the window put it");
            Assert.AreEqual(HelmFootprintKind.Card, HelmFootprint.Current.Kind, "a card, not a band");
            AssertEveryOverlayStandsClear("the dash", toast, note, popup);
        }

        [UnityTest]
        public IEnumerator HideAllAndGoingAshore_SayNothingIsCovered_AndEverythingGoesHome()
        {
            AssertTheScreenHasSize();
            HelmOverlayHost host = Host();
            DevToast toast = NewToast();
            QuestPanelPresenter note = NewNote();
            InteractPopup popup = Popup();
            var (boatGo, boat) = NewBoat();
            CameraFollow follow = NewCamera(boatGo.transform);
            yield return null;

            boat.SetHull(NewHull("boat.test_footprint_hideall", NewConsole("footprint_hideall")));
            toast.Show("A word while she steers");
            note.Show("A line on the note while she steers");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(HelmFootprintKind.Card, HelmFootprint.Current.Kind, "premise: the dash is said");

            BoatUiWindows.ToggleHideAll();
            yield return null;
            yield return null;   // the host stands down, then every reader catches up
            Assert.IsTrue(HelmFootprint.Current.IsNone, "⭐ hide-all: the card is gone, so nothing is covered");
            AssertEverythingIsHome("hide-all", toast, note, popup);
            Assert.AreEqual(0f, follow.BandShiftMeters, "hide-all: no camera move");

            BoatUiWindows.ToggleHideAll();
            yield return null;
            yield return null;
            Assert.AreEqual(HelmFootprintKind.Card, HelmFootprint.Current.Kind,
                            "one toggle brings the card back, and the seam says it again");
            AssertEveryOverlayStandsClear("the dash, restored", toast, note, popup);

            GameServices.Helm.SetPilotedHull(null);   // stepped ashore
            yield return null;
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.None), "premise: no card ashore");
            Assert.IsTrue(HelmFootprint.Current.IsNone, "⭐ ashore: nothing is covered");
            AssertEverythingIsHome("ashore", toast, note, popup);
            Assert.AreEqual(0f, follow.BandShiftMeters, "ashore: no camera move");
        }

        // ---- a full-width band, said through the seam as H2's band will say it ----------------------

        [UnityTest]
        public IEnumerator ABandSaidThroughTheSeam_LiftsEveryOverlay_AndEasesTheCameraUpAndBack()
        {
            AssertTheScreenHasSize();
            DevToast toast = NewToast();
            QuestPanelPresenter note = NewNote();
            InteractPopup popup = Popup();
            GameObject target = Spawn("FootprintTarget");   // no boat, no helm: nothing re-frames the view
            CameraFollow follow = NewCamera(target.transform);
            Camera cam = follow.GetComponent<Camera>();
            toast.Show("A word over the band");
            note.Show("A line on the note over the band");
            yield return null;
            yield return null;
            Assert.IsTrue(HelmFootprint.Current.IsNone, "premise: nobody is covering anything");
            AssertEverythingIsHome("before the band", toast, note, popup);
            float ortho = cam.orthographicSize;

            HelmFootprintSettings tune = Tuning();
            float bandPx = 144f * Mathf.Max(1, Screen.height / 1080);
            HelmFootprint.Publish(HelmFootprintArea.FullWidthBand(bandPx));
            yield return WaitOnTheCamerasClock(tune.CameraEaseSeconds + 0.25f);
            yield return null;   // and the camera's LateUpdate of the last frame counted

            AssertEveryOverlayStandsClear("the band", toast, note, popup);
            Assert.Greater(toast.Lift, 0f, "a band raises the floor the toast stands on");
            Assert.Greater(popup.Panel.anchoredPosition.y, InteractPopupLayout.PanelAnchoredPosition().y,
                           "…and the popup's");
            Assert.Greater(NoteRect(note).anchoredPosition.y, QuestPanelLayout.AnchoredPosition(note.Fit).y,
                           "…and the note's");

            Assert.AreEqual(ortho, cam.orthographicSize, "premise: the framing held still throughout");
            float metersPerPx = CameraFollow.MetersPerScreenPixel(false, cam.pixelWidth, cam.pixelHeight, 0, 0,
                                                                  CameraFollow.AssetsPPU, cam.orthographicSize);
            float expected = CameraFollow.BandShiftTargetPx(HelmFootprint.Current, tune.CameraBandShare)
                           * metersPerPx;
            Assert.Greater(expected, 0f, "premise: the band asks the camera for something");
            Assert.AreEqual(expected, follow.BandShiftMeters, 1e-5f,
                            "⭐ the whole ease is done: the band's share, in metres by the framing on screen");
            Assert.Less(cam.transform.position.y, target.transform.position.y - 0.25f * expected,
                        "the view stands below her, so she sits higher on screen, clear of the band");

            HelmFootprint.Clear();
            yield return WaitOnTheCamerasClock(tune.CameraEaseSeconds + 0.25f);
            yield return null;
            Assert.AreEqual(0f, follow.BandShiftMeters, "⭐ eased back out, to exactly nothing");
            AssertEverythingIsHome("after the band", toast, note, popup);
        }

        // ---- what each overlay must be ------------------------------------------------------------

        /// <summary>Each overlay stands where its layout puts it for what the seam says, and its box on
        /// this screen does not meet what is covered.</summary>
        private static void AssertEveryOverlayStandsClear(string what, DevToast toast, QuestPanelPresenter note,
                                                          InteractPopup popup)
        {
            int w = Screen.width, h = Screen.height;
            HelmFootprintArea covered = HelmFootprint.Current;

            // The toast: its lift is its layout's answer to the seam, and the whole stack clears.
            Assert.AreEqual(DevToast.LiftRef(in covered, w, h, ToastBaseY, ToastSpacing, ToastSlots), toast.Lift,
                            PlaceTolerance, $"{what}: the toast reads the seam");
            Rect stack = DevToast.StackScreenRect(w, h, ToastBaseY + toast.Lift, ToastSpacing, ToastSlots);
            Assert.IsFalse(Meets(in covered, stack), $"{what}: the toast's stack {stack} meets {covered}");

            // The popup: straight up its own column.
            Vector2 popupHome = InteractPopupLayout.PanelAnchoredPosition();
            Vector2 popupAt = popup.Panel.anchoredPosition;
            AssertAt(InteractPopupLayout.PanelAnchoredPosition(in covered, w, h), popupAt,
                     $"{what}: the popup reads the seam");
            Assert.AreEqual(popupHome.x, popupAt.x, PlaceTolerance, $"{what}: the popup keeps its column");
            Rect panel = InteractPopupLayout.PanelScreenRect(w, h);
            panel.y += (popupAt.y - popupHome.y) * HudBandLayout.ScaleFactor(w, h);
            Assert.IsFalse(Meets(in covered, panel), $"{what}: the popup {panel} meets {covered}");

            // The note: straight up out of its corner.
            QuestPanelFit fit = note.Fit;
            Vector2 noteHome = QuestPanelLayout.AnchoredPosition(in fit);
            Vector2 noteAt = NoteRect(note).anchoredPosition;
            AssertAt(QuestPanelLayout.AnchoredPosition(in fit, in covered, w, h), noteAt,
                     $"{what}: the note reads the seam");
            Assert.AreEqual(noteHome.x, noteAt.x, PlaceTolerance, $"{what}: the note keeps its corner's column");
            Rect box = QuestPanelLayout.NoteScreenRect(in fit, w, h);
            box.y += noteAt.y - noteHome.y;
            Assert.IsFalse(Meets(in covered, box), $"{what}: the note {box} meets {covered}");
        }

        /// <summary>With nothing covered, every overlay is where it was built.</summary>
        private static void AssertEverythingIsHome(string what, DevToast toast, QuestPanelPresenter note,
                                                   InteractPopup popup)
        {
            Assert.AreEqual(0f, toast.Lift, $"{what}: the toast stands on its authored base");
            AssertAt(InteractPopupLayout.PanelAnchoredPosition(), popup.Panel.anchoredPosition,
                     $"{what}: the popup is home");
            AssertAt(QuestPanelLayout.AnchoredPosition(note.Fit), NoteRect(note).anchoredPosition,
                     $"{what}: the note is in its corner");
        }

        private static void AssertAt(Vector2 expected, Vector2 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, PlaceTolerance, message + " (x)");
            Assert.AreEqual(expected.y, actual.y, PlaceTolerance, message + " (y)");
        }

        /// <summary>A box meets the covered area if any of its inside does (a shared edge is clear).</summary>
        private static bool Meets(in HelmFootprintArea covered, Rect box)
        {
            if (covered.IsNone) return false;
            return covered.Overlaps(Rect.MinMaxRect(box.xMin + Eps, box.yMin + Eps, box.xMax - Eps, box.yMax - Eps));
        }

        /// <summary>
        /// The overlays are placed on the screen's own pixels, and a screen with no size is an
        /// ENVIRONMENT fact rather than a defect here — so say which one it is up front.
        /// </summary>
        private static void AssertTheScreenHasSize()
        {
            Assert.That(Screen.width, Is.GreaterThan(0),
                        "ENVIRONMENT: the player has no screen size, so no canvas has a rect and nothing can " +
                        "be placed on it. Run without -nographics (the documented verification posture).");
            Assert.That(Screen.height, Is.GreaterThan(0), "ENVIRONMENT: see above.");
        }

        /// <summary>Wait until <paramref name="seconds"/> of the camera's clock have passed.</summary>
        private static IEnumerator WaitOnTheCamerasClock(float seconds)
        {
            float waited = 0f;
            for (int frames = 0; waited < seconds && frames < FrameCap; frames++)
            {
                yield return null;
                waited += Time.deltaTime;
            }
        }

        private static HelmFootprintSettings Tuning()
            => GameServices.Config != null ? GameServices.Config.HelmFootprint : HelmFootprintSettings.Default;

        // ---- the rig --------------------------------------------------------------------------------

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private HelmOverlayHost Host()
        {
            HelmOverlayHost host = HelmOverlayHost.Instance;
            if (host == null) host = Spawn("HelmOverlayHost(footprint test)").AddComponent<HelmOverlayHost>();
            return host;
        }

        private InteractPopup Popup()
        {
            InteractPopup popup = InteractPopup.Instance;
            if (popup == null)
            {
                _popupHost = new GameObject("InteractPopup(footprint test)");
                popup = _popupHost.AddComponent<InteractPopup>();
            }
            return popup;
        }

        private DevToast NewToast() => Spawn("DevToast(footprint test)").AddComponent<DevToast>();

        private QuestPanelPresenter NewNote()
            => Spawn("QuestNote(footprint test)").AddComponent<QuestPanelPresenter>();

        private static RectTransform NoteRect(QuestPanelPresenter note)
        {
            RectTransform rect = note.GetComponentsInChildren<RectTransform>(true)
                                     .FirstOrDefault(r => r.name == "QuestNote");
            Assert.IsNotNull(rect, "premise: the note's own rect");
            return rect;
        }

        private CameraFollow NewCamera(Transform target)
        {
            GameObject go = Spawn("FootprintCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DefaultWorldHeightMeters);
            go.transform.position = new Vector3(target.position.x, target.position.y, -10f);
            var follow = go.AddComponent<CameraFollow>();
            follow.Target = target;
            return follow;
        }

        private (GameObject go, BoatController boat) NewBoat()
        {
            GameObject go = Spawn("FootprintTestBoat");
            var boat = go.AddComponent<BoatController>();   // Awake self-adds HelmControlRelay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            GameServices.Helm.SetPilotedHull(boat);         // she is at this boat's helm
            return (go, boat);
        }

        private HelmConsoleDef NewConsole(string id)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_" + id;
            console.Rig = ConsoleRigKind.Console;
            console.Lever = HelmLeverFinish.Graphite;
            console.Wheel = HelmWheelRim.Rubber;
            console.DefaultCompass = CompassMount.Dome;
            _spawned.Add(console);
            return console;
        }

        /// <summary>An engine hull: with a console it shows the composed dash, without one the
        /// outboard tiller (<see cref="HelmControlRelay.StyleFor"/>).</summary>
        private BoatHullDef NewHull(string id, HelmConsoleDef console)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = "Footprint Test Hull";
            h.Propulsion = PropulsionType.Engine;
            h.MassKg = 700f;
            h.EnginePower = 650f;
            h.RudderAuthority = 600f;
            h.ForwardDrag = 140f;
            h.LateralDrag = 360f;
            h.WindExposure = 0f;
            h.DraughtMeters = 0.5f;
            h.Helm = console;
            _spawned.Add(h);
            return h;
        }
    }
}
