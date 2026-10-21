using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    public class HelmHudReadabilityTests
    {
        private static readonly Vector2Int[] Screens = {
            new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440),
            new Vector2Int(3440, 1440), new Vector2Int(3840, 2160)
        };
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static float Scale(Vector2Int s) => (float)Math.Sqrt(s.x / 1280.0 * s.y / 720.0);
        private static float Top(string name) => (float)typeof(HelmHudSuppression).GetField(name).GetRawConstantValue();

        [Test]
        public void WindTextBox_WithTheAuthoredLowerAnchor_StaysAboveZeroAtEverySize()
        {
            // Pin the REAL construction and moved alignment as well as the pure footprint. A switch
            // to UpperCenter alone would otherwise make this test measure a different premise.
            string src = File.ReadAllText("Assets/_Project/Code/UI/HudController.cs");
            StringAssert.Contains("\"ApparentWind\", TextAnchor.LowerCenter", src);
            StringAssert.Contains("text.alignment = clear ? TextAnchor.LowerLeft : _navHomeAlign[i]", src);
            StringAssert.Contains("rt.pivot = new Vector2(0f, 1f)", src);
            StringAssert.Contains("LabelBoxHeightRef = 56f", src);
            foreach (Vector2Int s in Screens)
            {
                float bottom = (Top("ApparentWindTopRef") - 56f) * Scale(s);
                Assert.That(bottom, Is.GreaterThanOrEqualTo(0f), $"{s}: bottom-aligned wind box clips");
                Assert.That(HelmHudSuppression.NavClusterScreenRect(NavClusterPlacement.ClearOfTheDash,
                    s.x, s.y).yMin, Is.EqualTo(bottom).Within(0.01f));
            }
        }

        [Test]
        public void AuditWindGlyphs_KeepTheirOutlineAboveZero_AndTheirGapToSetDrift()
        {
            // L5's measured glyph bounds, independent of the production bar. These are a translation
            // proof, not a new font raster measurement; the editor test below checks real Text again.
            float[] windBottom = { -17f, -25f, -33f, -38.3f, -49f };
            float[] windHeight = { 29f, 42f, 55f, 63f, 82f };
            float[] driftBottom = { 18f, 28f, 38f, 44.3f, 57f };
            for (int i = 0; i < Screens.Length; i++)
            {
                float scale = Scale(Screens[i]);
                float wind = windBottom[i] + (Top("ApparentWindTopRef") - 40f) * scale;
                float drift = driftBottom[i] + (Top("SetDriftTopRef") - 70f) * scale;
                Assert.That(wind - 2f * scale, Is.GreaterThanOrEqualTo(0f), "include the Outline below the ink");
                Assert.That(wind + windHeight[i], Is.LessThan(drift - 2f * scale), "wind must not collide with set/drift");
            }
        }

        [Test]
        public void TheTillersCardAndMargin_FollowTheHudFactor_AtEverySize()
        {
            // One state since H2: the focus and the dash card retired, so the tiller's card is the
            // only helm card left, and the band (whole k) is HelmBandLayoutTests'.
            HelmOverlaySettings cfg = HelmOverlaySettings.Default;
            foreach (Vector2Int s in Screens)
            {
                float k = Scale(s);
                Rect tiller = HelmOverlayLayout.CardRect(120, 244, cfg, s.x, s.y);
                Assert.That(tiller.width, Is.EqualTo(120f * k).Within(0.01f), $"{s}");
                Assert.That(tiller.height, Is.EqualTo(244f * k).Within(0.01f), $"{s}");
                Assert.That(tiller.y, Is.EqualTo(16f * k).Within(0.01f), $"{s}");
            }
        }

        [Test]
        public void PortDriftDescenders_LeaveAGapBetweenBothOutlines_AtEverySize()
        {
            // Phase B's actual font sweep, before this correction (tops 60/90). The port word's
            // descender makes the ink taller than the audit's resting COG row. This independent
            // measurement fails the Phase A draft by 3 px at 720p; it is not a production-derived bar.
            float[] windBottom = { 3f, 5f, 7f, 8.1f, 11f };
            float[] windHeight = { 29f, 42f, 55f, 63f, 82f };
            float[] portBottom = { 33f, 50f, 67f, 78.7f, 102f };
            for (int i = 0; i < Screens.Length; i++)
            {
                float k = Scale(Screens[i]);
                float windTop = windBottom[i] + windHeight[i] + (Top("ApparentWindTopRef") - 60f) * k;
                float driftBottom = portBottom[i] + (Top("SetDriftTopRef") - 90f) * k;
                Assert.That(driftBottom - 2f * k, Is.GreaterThan(windTop + 2f * k),
                    $"{Screens[i]}: wind and port-drift outlines touch");
            }
        }

        [Test]
        public void BuiltFishingHint_ClearsEveryRaisedCard_AtEverySize()
        {
            // The focused dash and its title bar retired with H2. What can now rise toward the hint is
            // a raised instrument card, which never reaches into the top band (Q1: B); the hint must
            // stop clear above the tallest of them at every size, the plotter's max face included.
            var natives = new[]
            {
                new Vector2Int(DepthRigRender.W, DepthRigRender.H), new Vector2Int(FishRigRender.W, FishRigRender.H),
                new Vector2Int(RadarRigRender.W, RadarRigRender.H),
                new Vector2Int(NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH),
                new Vector2Int(NavRigGeometry.MaxW, NavRigGeometry.MaxH),
            };
            var go = new GameObject("HelmFishingHintClearance");
            HudController hud = null;
            try
            {
                hud = go.AddComponent<HudController>();
                var field = typeof(HudController).GetField("_helmFishingHint", PrivateInstance);
                if (field.GetValue(hud) == null)
                    typeof(HudController).GetMethod("BuildHud", PrivateInstance).Invoke(hud, null);
                var hint = (Text)field.GetValue(hud);
                foreach (Vector2Int s in Screens)
                {
                    float k = Scale(s);
                    float bottom = s.y + (hint.rectTransform.anchoredPosition.y - hint.rectTransform.sizeDelta.y) * k;
                    // Every raised card must stop below the whole hint box, with air.
                    foreach (Vector2Int n in natives)
                    {
                        Rect card = HelmBandLayout.RaisedCardRect(n.x, n.y, s.x, s.y, 220f * k);
                        Assert.That(bottom, Is.GreaterThanOrEqualTo(card.yMax + 4f * k - 0.01f),
                                    $"{s}: the raised {n.x}x{n.y} card reaches the fishing hint");
                    }
                }
            }
            finally
            {
                if (hud != null)
                {
                    var canvas = typeof(HudController).GetField("_canvasGo", PrivateInstance).GetValue(hud) as GameObject;
                    if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private sealed class Item : ICarriable
        {
            public string DefId { get; set; }
            public bool IsCarriable => true;
            public Transform Transform => null;
            public int BakedFacings => 0;
            public void ShowFacing(int i) { }
            public void RideSortingBand(int layer, int order) { }
            public void OnLifted(ICarrier c) { }
            public void OnPlaced() { }
        }
        private sealed class Hands : ICarrier
        {
            public ICarriable Carried { get; set; }
            public bool IsCarrying => Carried != null;
        }

        [Test]
        public void FishingHint_IsOnlyForAboardWithARod_ThroughTheCoreHandsSeam()
        {
            // Reflection lets this same test execute on e9a4ba47 and report the missing behaviour,
            // rather than failing to compile because the new UI policy did not exist then.
            var policy = typeof(HudController).Assembly.GetType("HiddenHarbours.UI.HelmFishingHint");
            Assert.NotNull(policy, "no hint exists on the old helm");
            var decide = policy.GetMethod("ShouldShow");
            var rod = new Hands { Carried = new Item { DefId = "tool.rod" } };
            foreach (ControlMode mode in Enum.GetValues(typeof(ControlMode)))
            foreach (ICarrier hands in new ICarrier[] { rod, new Hands(), null,
                         new Hands { Carried = new Item { DefId = "tool.shovel" } } })
                Assert.That((bool)decide.Invoke(null, new object[] { mode, hands, "tool.rod" }),
                    Is.EqualTo(mode == ControlMode.Aboard && ReferenceEquals(hands, rod)), $"{mode} {hands}");
            string source = File.ReadAllText("Assets/_Project/Code/UI/HudController.cs");
            StringAssert.Contains("HelmFishingHint.ShouldShow(_controlMode, GameServices.Hands, _fishingHintRodId)", source);
            StringAssert.Contains("_controlMode = e.Mode", source);
            StringAssert.Contains("_helmFishingHint.text = HudStrings.FishingAtHelm", source);
            StringAssert.DoesNotContain("HiddenHarbours.Fishing", source);
        }

        [Test]
        public void BuiltFishingHint_RespondsToModeAndHands_AndUsesHudWords()
        {
            var hintField = typeof(HudController).GetField("_helmFishingHint", PrivateInstance);
            Assert.NotNull(hintField, "the old HUD has no fishing hint label");
            var go = new GameObject("HelmFishingHintTest");
            HudController hud = null;
            ICarrier oldHands = GameServices.Hands;
            bool oldBlocked = InteractionGate.IsBlocked;
            try
            {
                Assert.False(ShellFlow.WorldInputBlocked, "fixture requires an unpaused EditMode context");
                InteractionGate.IsBlocked = false;
                hud = go.AddComponent<HudController>();
                if (hintField.GetValue(hud) == null)
                    typeof(HudController).GetMethod("BuildHud", PrivateInstance).Invoke(hud, null);
                var text = (Text)hintField.GetValue(hud);
                var modeChanged = typeof(HudController).GetMethod("OnControlModeChanged", PrivateInstance);
                var update = typeof(HudController).GetMethod("UpdateHelmFishingHint", PrivateInstance);
                var hands = new Hands();
                GameServices.Hands = hands;
                foreach (ControlMode mode in Enum.GetValues(typeof(ControlMode)))
                foreach (bool held in new[] { false, true })
                {
                    hands.Carried = held ? new Item { DefId = "tool.rod" } : null;
                    modeChanged.Invoke(hud, new object[] { new ControlModeChanged(mode) });
                    update.Invoke(hud, null);
                    Assert.That(text.enabled, Is.EqualTo(mode == ControlMode.Aboard && held));
                }
                Assert.That(text.text, Is.EqualTo(typeof(HudStrings).GetField("FishingAtHelm").GetRawConstantValue()));
                Assert.False(text.raycastTarget, "the hint never consumes a helm click");
            }
            finally
            {
                GameServices.Hands = oldHands;
                InteractionGate.IsBlocked = oldBlocked;
                if (hud != null)
                {
                    var canvas = typeof(HudController).GetField("_canvasGo", PrivateInstance).GetValue(hud) as GameObject;
                    if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuiltWindLabel_UsesTheRealAnchorAndBox_AtAllFiveCanvasScales()
        {
            // Needs Unity: no headless replacement is permitted for this actual uGUI assertion.
            var go = new GameObject("HelmHudReadability");
            HudController hud = null;
            try
            {
                hud = go.AddComponent<HudController>();
                var field = typeof(HudController).GetField("_apparentWindLabel", PrivateInstance);
                if (field.GetValue(hud) == null)
                    typeof(HudController).GetMethod("BuildHud", PrivateInstance).Invoke(hud, null);
                Text label = (Text)field.GetValue(hud);
                foreach (Vector2Int screen in Screens)
                {
                    float k = Scale(screen);
                    Assert.That(label.alignment, Is.EqualTo(TextAnchor.LowerCenter));
                    Assert.That(label.rectTransform.pivot.y, Is.EqualTo(1f));
                    Assert.That((label.rectTransform.anchoredPosition.y - label.rectTransform.sizeDelta.y) * k,
                        Is.GreaterThanOrEqualTo(0f), $"{screen}: actual wind label below screen");
                }
            }
            finally
            {
                if (hud != null)
                {
                    var canvas = typeof(HudController).GetField("_canvasGo", PrivateInstance).GetValue(hud) as GameObject;
                    if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
