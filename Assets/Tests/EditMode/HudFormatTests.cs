using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-17 / partial VS-19 — the HUD's pure formatters and the wind/sea conversions. These are
    /// the strings a player reads in under a second, so they are pinned: the 24h clock, the
    /// duration-to-turn, money, the payout flash, knots/Beaufort, the wind cardinal, and the
    /// sea-state word. All redundant-coded reads (a number/word, never colour alone).
    /// </summary>
    public class HudFormatTests
    {
        // ---- clock & durations --------------------------------------------------------------

        [Test]
        public void ClockHHMM_PadsAndWraps()
        {
            Assert.AreEqual("06:30", HudFormat.ClockHHMM(6.5f));
            Assert.AreEqual("00:00", HudFormat.ClockHHMM(0f));
            Assert.AreEqual("23:59", HudFormat.ClockHHMM(23f + 59f / 60f));
            Assert.AreEqual("00:00", HudFormat.ClockHHMM(24f), "24:00 wraps to 00:00");
            Assert.AreEqual("12:00", HudFormat.ClockHHMM(12f));
        }

        [Test]
        public void DurationHMM_ConvertsInGameSecondsToHMM()
        {
            // 1 in-game hour = secondsPerHour real seconds. Use 60 s/h so 1 in-game minute = 1 s.
            // Then 1h42m of in-game time = 102 in-game minutes = 102 real seconds.
            float sph = 60f;
            Assert.AreEqual("1:42", HudFormat.DurationHMM(102.0, sph));
            Assert.AreEqual("0:00", HudFormat.DurationHMM(0.0, sph));
            Assert.AreEqual("0:30", HudFormat.DurationHMM(30.0, sph));
        }

        [Test]
        public void DurationHMM_GuardsBadInput()
        {
            Assert.AreEqual("--", HudFormat.DurationHMM(-1.0, 60f), "negative duration is unknown");
            Assert.AreEqual("--", HudFormat.DurationHMM(100.0, 0f), "zero seconds-per-hour is unknown");
        }

        // ---- money --------------------------------------------------------------------------

        [Test]
        public void Money_HasCurrencyAndThousands()
        {
            Assert.AreEqual("₲0", HudFormat.Money(0));
            Assert.AreEqual("₲1,240", HudFormat.Money(1240));
            Assert.AreEqual("₲-50", HudFormat.Money(-50), "debt is shown, not hidden");
        }

        [Test]
        public void PayoutFlash_ShowsPlusForGains()
        {
            Assert.AreEqual("+₲48", HudFormat.PayoutFlash(48));
            Assert.AreEqual("+₲1,200", HudFormat.PayoutFlash(1200));
            Assert.AreEqual("₲0", HudFormat.PayoutFlash(0), "a zero payout shows no '+'");
        }

        [Test]
        public void CatchCard_ShowsNameWeightAndValue()
        {
            Assert.AreEqual("Atlantic Cod — 3.4 kg — ₲48!",
                HudFormat.CatchCard("Atlantic Cod", 3.4f, 48));
            Assert.AreEqual("Lobster — 1.0 kg — ₲1,200!",
                HudFormat.CatchCard("Lobster", 1.0f, 1200), "weight is one decimal; value has thousands");
            Assert.AreEqual("Catch — 0.5 kg — ₲0!",
                HudFormat.CatchCard("", 0.5f, -5), "a missing name and bad value are guarded");
        }

        [Test]
        public void HeightMeters_OneDecimalWithSign()
        {
            Assert.AreEqual("+1.6 m", HudFormat.HeightMeters(1.6f));
            Assert.AreEqual("-0.3 m", HudFormat.HeightMeters(-0.3f));
            Assert.AreEqual("0.0 m", HudFormat.HeightMeters(0f));
        }

        // ---- wind ---------------------------------------------------------------------------

        [Test]
        public void Knots_FromMetresPerSecond()
        {
            Assert.That(WindReadout.Knots(1f), Is.EqualTo(1.94384f).Within(1e-3));
            Assert.That(WindReadout.Knots(10f), Is.EqualTo(19.4384f).Within(1e-2));
        }

        [Test]
        public void Beaufort_BucketsByStandardScale()
        {
            Assert.AreEqual(0, WindReadout.Beaufort(0.2f),  "calm");
            Assert.AreEqual(2, WindReadout.Beaufort(2.5f),  "light breeze");
            Assert.AreEqual(4, WindReadout.Beaufort(7.0f),  "moderate breeze");
            Assert.AreEqual(6, WindReadout.Beaufort(12.0f), "strong breeze");
            Assert.AreEqual(8, WindReadout.Beaufort(19.0f), "gale");
            Assert.AreEqual(12, WindReadout.Beaufort(40.0f), "hurricane force caps at 12");
            Assert.AreEqual(0, WindReadout.Beaufort(-5f), "negative speed clamps to calm");
        }

        [Test]
        public void Cardinal_MapsVectorToCompassPoint()
        {
            // +Y = North, +X = East.
            Assert.AreEqual("N", WindReadout.Cardinal(new Vector2(0f, 1f)));
            Assert.AreEqual("E", WindReadout.Cardinal(new Vector2(1f, 0f)));
            Assert.AreEqual("S", WindReadout.Cardinal(new Vector2(0f, -1f)));
            Assert.AreEqual("W", WindReadout.Cardinal(new Vector2(-1f, 0f)));
            Assert.AreEqual("NE", WindReadout.Cardinal(new Vector2(1f, 1f)));
            Assert.AreEqual("SW", WindReadout.Cardinal(new Vector2(-1f, -1f)));
        }

        [Test]
        public void Cardinal_CalmIsUndefined()
        {
            Assert.AreEqual("--", WindReadout.Cardinal(Vector2.zero));
            Assert.AreEqual("--", WindReadout.Cardinal(new Vector2(0.001f, 0.001f)),
                "a near-zero wind has no meaningful direction");
        }

        // ---- sea-state & season words -------------------------------------------------------

        [Test]
        public void SeaState_HasAWordForEveryTier()
        {
            Assert.AreEqual("Glass", HudStrings.SeaState(SeaState.Glass));
            Assert.AreEqual("Calm",  HudStrings.SeaState(SeaState.Calm));
            Assert.AreEqual("Gale",  HudStrings.SeaState(SeaState.Gale));
            Assert.AreEqual("Storm", HudStrings.SeaState(SeaState.Storm));
        }

        [Test]
        public void TideArrow_IsAShapeNotAColour()
        {
            Assert.AreEqual("▲", HudStrings.TideArrow(true));
            Assert.AreEqual("▼", HudStrings.TideArrow(false));
            Assert.AreNotEqual(HudStrings.TideArrow(true), HudStrings.TideArrow(false),
                "rising and falling must differ by shape so colourblind players read it");
        }

        [Test]
        public void Season_HasReadableLabels()
        {
            Assert.AreEqual("Early Spring", HudStrings.Season(Season.EarlySpring));
            Assert.AreEqual("Hard Winter", HudStrings.Season(Season.HardWinter));
        }
    }
}
