using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE HELM DASH BAND, H1 — <b>the helm's footprint seam</b> (ADR 0050): one Core statement of what
    /// the helm's UI covers at the bottom of the screen, in screen pixels — nothing, a band across the
    /// whole width, or a card's rect — which every overlay and the camera read instead of reaching into
    /// UI (rule 4).
    ///
    /// <para>What this pins: the seam says nothing until somebody speaks, and with no UI up nobody does;
    /// it raises one change event per change and none on a reset; the two keep-out rules every overlay
    /// shares (a band raises the floor, a card is stepped over by the overlay's own gap); the helm host's
    /// statement of its card (the card and its title strip); and that nothing of it reaches the save
    /// (rule 5). The host publishing and clearing it live is <c>HelmFootprintPlayTests</c>'.</para>
    /// </summary>
    public class HelmFootprintSeamTests
    {
        /// <summary>The window chrome's title strip as shipped (<c>BoatUiWindowSettings.TitleBarPx</c>).</summary>
        private const float ShippedTitleBarPx = 18f;

        private readonly List<HelmFootprintChanged> _heard = new List<HelmFootprintChanged>();

        [SetUp]
        public void SetUp()
        {
            HelmFootprint.Reset();
            EventBus.Clear<HelmFootprintChanged>();
            _heard.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<HelmFootprintChanged>(Heard);
            EventBus.Clear<HelmFootprintChanged>();
            HelmFootprint.Reset();   // a seam left covered would move every camera test after this one
        }

        private void Heard(HelmFootprintChanged e) => _heard.Add(e);

        // ===== the channel ===========================================================================

        [Test]
        public void TheSeam_SaysNothing_UntilSomebodySpeaks()
        {
            HelmFootprintArea now = HelmFootprint.Current;
            Assert.IsTrue(now.IsNone, "no helm UI up: nothing is covered");
            Assert.AreEqual(HelmFootprintKind.None, now.Kind);
            Assert.AreEqual(default(HelmFootprintArea), now, "…and nothing is the default value, so a " +
                "reader that never saw a publish reads the same thing as one told 'nothing'");
            Assert.AreEqual(0f, now.TopPx);
            Assert.AreEqual(0f, now.FullWidthHeightPx);
            Assert.IsFalse(now.SpansFullWidth);
            Assert.IsFalse(now.Overlaps(new Rect(0f, 0f, 4000f, 4000f)), "nothing overlaps nothing");
            Assert.AreEqual(0f, now.LiftToClear(new Rect(0f, -50f, 4000f, 4000f), 16f),
                "and nothing lifts anything, however low it hangs");
        }

        [Test]
        public void PublishAndClear_RaiseOneEventPerChange_WithWhatWasThereBefore()
        {
            EventBus.Subscribe<HelmFootprintChanged>(Heard);
            HelmFootprintArea band = HelmFootprintArea.FullWidthBand(144f);
            HelmFootprintArea card = HelmFootprintArea.OfCard(new Rect(580f, 16f, 120f, 262f));

            HelmFootprint.Publish(in band);
            Assert.AreEqual(1, _heard.Count, "a change is announced");
            Assert.AreEqual(band, _heard[0].Current);
            Assert.IsTrue(_heard[0].Previous.IsNone);
            Assert.AreEqual(band, HelmFootprint.Current);

            HelmFootprint.Publish(in band);
            Assert.AreEqual(1, _heard.Count, "saying the same thing again is not a change — a card at " +
                "rest publishes every frame and must cost one compare, not an event");

            HelmFootprint.Publish(in card);
            Assert.AreEqual(2, _heard.Count);
            Assert.AreEqual(card, _heard[1].Current);
            Assert.AreEqual(band, _heard[1].Previous, "the event carries what it replaced");

            HelmFootprint.Clear();
            Assert.AreEqual(3, _heard.Count);
            Assert.IsTrue(_heard[2].Current.IsNone, "Clear says 'nothing'");
            Assert.AreEqual(card, _heard[2].Previous);
            Assert.IsTrue(HelmFootprint.Current.IsNone);

            HelmFootprint.Clear();
            Assert.AreEqual(3, _heard.Count, "and clearing nothing is no news either");
        }

        [Test]
        public void Reset_SaysNothing_AndTellsNobody()
        {
            HelmFootprint.Publish(HelmFootprintArea.FullWidthBand(144f));
            EventBus.Subscribe<HelmFootprintChanged>(Heard);

            HelmFootprint.Reset();
            Assert.IsTrue(HelmFootprint.Current.IsNone, "a reset (a test, a domain reload) is back to nothing");
            Assert.AreEqual(0, _heard.Count, "…silently: a reset is housekeeping, not a change on screen");
        }

        // ===== the statement =========================================================================

        [Test]
        public void TheFactories_TurnAnEmptyAreaIntoNothing()
        {
            Assert.IsTrue(HelmFootprintArea.FullWidthBand(0f).IsNone, "a band with no height covers nothing");
            Assert.IsTrue(HelmFootprintArea.FullWidthBand(-10f).IsNone);
            Assert.IsTrue(HelmFootprintArea.FullWidthBand(float.NaN).IsNone);
            Assert.IsTrue(HelmFootprintArea.OfCard(new Rect(100f, 16f, 0f, 244f)).IsNone,
                          "a card with no width covers nothing");
            Assert.IsTrue(HelmFootprintArea.OfCard(new Rect(100f, 16f, 120f, 0f)).IsNone,
                          "…nor one with no height");

            HelmFootprintArea band = HelmFootprintArea.FullWidthBand(288f);
            Assert.AreEqual(HelmFootprintKind.Band, band.Kind);
            Assert.IsTrue(band.SpansFullWidth);
            Assert.AreEqual(288f, band.TopPx, "a band stands on the screen's bottom edge");
            Assert.AreEqual(288f, band.FullWidthHeightPx);
            Assert.AreEqual(new Rect(0f, 0f, 0f, 288f), band.Rect, "its rect carries the height only — " +
                "it has no column, it is the whole width of whatever screen reads it");

            var r = new Rect(580f, 16f, 120f, 262f);
            HelmFootprintArea card = HelmFootprintArea.OfCard(r);
            Assert.AreEqual(HelmFootprintKind.Card, card.Kind);
            Assert.IsFalse(card.SpansFullWidth, "a card is never read as a band, however wide it is");
            Assert.AreEqual(r, card.Rect);
            Assert.AreEqual(278f, card.TopPx);
            Assert.AreEqual(0f, card.FullWidthHeightPx, "the camera's number is a band's alone");
        }

        [Test]
        public void ABand_RaisesTheFloor_AnyOverhangIncluded()
        {
            HelmFootprintArea band = HelmFootprintArea.FullWidthBand(144f);

            // A box that stood 16 px off the old floor stands 16 px off the band's top: the band IS the
            // new bottom edge, so stacked boxes keep their order and their spacing.
            Assert.AreEqual(144f, band.LiftToClear(new Rect(900f, 16f, 300f, 44f), 16f));
            Assert.AreEqual(144f, band.LiftToClear(new Rect(900f, 230f, 300f, 44f), 0f),
                "a box above the band's top rises too — the floor rose under everything on it");
            // A box whose own rect hangs below the screen's edge lands with its bottom on the band.
            Assert.AreEqual(160f, band.LiftToClear(new Rect(0f, -16f, 900f, 204f), 0f));
            Assert.AreEqual(144f, band.LiftToClear(new Rect(-5000f, 0f, 1f, 1f), 0f),
                "a band has no column: where the box stands across the screen does not matter");
        }

        [Test]
        public void ACard_LiftsOnlyWhatItReaches_ByTheGapItIsAsked()
        {
            HelmFootprintArea card = HelmFootprintArea.OfCard(new Rect(580f, 16f, 120f, 262f));   // top 278

            Assert.AreEqual(0f, card.LiftToClear(new Rect(964f, 16f, 300f, 44f), 16f),
                "a box beside the card stays where it is");
            Assert.AreEqual(0f, card.LiftToClear(new Rect(600f, 300f, 60f, 20f), 16f),
                "…and so does one already clear of the card by more than its gap");
            Assert.AreEqual(294f - 100f, card.LiftToClear(new Rect(600f, 100f, 60f, 20f), 16f),
                "a box on the card rises until its foot is its gap above the card's top");
            Assert.AreEqual(4f, card.LiftToClear(new Rect(600f, 290f, 60f, 20f), 16f),
                "a box inside the gap rises by what is missing, no more");
            Assert.AreEqual(278f - 230f, card.LiftToClear(new Rect(320f, 230f, 640f, 158f), 0f),
                "with no gap its foot lands on the card's top exactly");
            Assert.AreEqual(278f - 100f, card.LiftToClear(new Rect(600f, 100f, 60f, 20f), -40f),
                "a negative gap is no gap — never a licence to overlap");
            Assert.AreEqual(0f, card.LiftToClear(new Rect(700f, 100f, 60f, 20f), 16f),
                "edges that only touch do not meet");
            Assert.AreEqual(0f, card.LiftToClear(new Rect(600f, 100f, 0f, 20f), 16f),
                "an empty box has nothing to keep out");
        }

        [Test]
        public void Overlaps_IsStrict_AndABandIgnoresTheColumn()
        {
            HelmFootprintArea band = HelmFootprintArea.FullWidthBand(144f);
            Assert.IsTrue(band.Overlaps(new Rect(-3000f, 100f, 10f, 10f)), "a band covers every column");
            Assert.IsFalse(band.Overlaps(new Rect(0f, 144f, 10f, 10f)), "a box standing ON its top is clear");
            Assert.IsTrue(band.Overlaps(new Rect(0f, 143.5f, 10f, 10f)));
            Assert.IsFalse(band.Overlaps(new Rect(0f, 100f, 0f, 10f)), "an empty box overlaps nothing");

            HelmFootprintArea card = HelmFootprintArea.OfCard(new Rect(580f, 16f, 120f, 262f));
            Assert.IsTrue(card.Overlaps(new Rect(690f, 270f, 50f, 50f)));
            Assert.IsFalse(card.Overlaps(new Rect(700f, 100f, 50f, 50f)), "touching its right edge is beside it");
            Assert.IsFalse(card.Overlaps(new Rect(600f, 278f, 50f, 50f)), "touching its top is above it");
            Assert.IsFalse(card.Overlaps(new Rect(100f, 100f, 50f, 50f)));
        }

        [Test]
        public void TwoStatements_AreEqual_OnlyWhenTheySayTheSameThing()
        {
            Assert.AreEqual(HelmFootprintArea.None, default(HelmFootprintArea));
            Assert.IsTrue(HelmFootprintArea.FullWidthBand(144f) == HelmFootprintArea.FullWidthBand(144f));
            Assert.IsTrue(HelmFootprintArea.FullWidthBand(144f) != HelmFootprintArea.FullWidthBand(288f));
            Assert.AreNotEqual(HelmFootprintArea.FullWidthBand(144f),
                               HelmFootprintArea.OfCard(new Rect(0f, 0f, 1280f, 144f)),
                               "a card as wide as the screen is still a card: a band is said, not inferred");
            var r = new Rect(580f, 16f, 120f, 262f);
            Assert.AreEqual(HelmFootprintArea.OfCard(r), HelmFootprintArea.OfCard(r));
            Assert.AreEqual(HelmFootprintArea.OfCard(r).GetHashCode(), HelmFootprintArea.OfCard(r).GetHashCode());
            Assert.AreNotEqual(HelmFootprintArea.OfCard(r),
                               HelmFootprintArea.OfCard(new Rect(581f, 16f, 120f, 262f)));
        }

        // ===== never saved (rule 5) ==================================================================

        [Test]
        public void NothingOfTheSeam_IsEverSaved()
        {
            Type[] seamTypes = { typeof(HelmFootprintArea), typeof(HelmFootprintKind), typeof(HelmFootprintSettings) };
            var offenders = SaveFields(typeof(SaveData), depth: 4)
                .Where(f => seamTypes.Contains(Unwrap(f.FieldType))
                            || f.Name.IndexOf("footprint", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => $"{f.DeclaringType?.Name}.{f.Name}")
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "what the helm covers is presentation, recomputed every frame — it must not enter the save: " +
                string.Join(", ", offenders));
        }

        private static IEnumerable<FieldInfo> SaveFields(Type t, int depth)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                yield return f;
                if (depth <= 0) continue;
                Type inner = Unwrap(f.FieldType);
                if (inner == null || inner.IsPrimitive || inner == typeof(string) || inner.IsEnum) continue;
                if (inner.Namespace == null || !inner.Namespace.StartsWith("HiddenHarbours")) continue;
                foreach (FieldInfo n in SaveFields(inner, depth - 1)) yield return n;
            }
        }

        private static Type Unwrap(Type t)
        {
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType)
            {
                Type[] args = t.GetGenericArguments();
                return args[args.Length - 1];   // List<T> → T, Dictionary<K, V> → V
            }
            return t;
        }

        // ===== the helm host's statement =============================================================

        [Test]
        public void TheHelmHost_StatesItsCardsWindow_TheTitleStripIncluded()
        {
            var card = new Rect(580f, 16f, 120f, 244f);   // the tiller's card at ×1 on a 1280-wide screen
            HelmFootprintArea said = HelmOverlayHost.FootprintOf(card, ShippedTitleBarPx);
            Assert.AreEqual(HelmFootprintKind.Card, said.Kind, "a card is never stated as a band");
            Assert.AreEqual(new Rect(580f, 16f, 120f, 262f), said.Rect,
                "the card AND the strip above it — the strip shows on hover and while dragging, and an " +
                "overlay parked on the card's top would be under it the moment the pointer arrived");

            HelmFootprintArea bar = HelmOverlayHost.FootprintOf(new Rect(580f, 16f, 120f, 0f), ShippedTitleBarPx);
            Assert.AreEqual(new Rect(580f, 16f, 120f, ShippedTitleBarPx), bar.Rect,
                "a BAR-collapsed card is its strip alone");

            Assert.AreEqual(new Rect(580f, 16f, 120f, 244f),
                            HelmOverlayHost.FootprintOf(card, -5f).Rect, "a negative strip is no strip");
            Assert.IsTrue(HelmOverlayHost.FootprintOf(new Rect(580f, 16f, 0f, 244f), ShippedTitleBarPx).IsNone,
                "a card the window gave no room covers nothing");
        }
    }
}
