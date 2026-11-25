using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.UI;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards from the text review: the pixel face is drawn at a whole number of screen pixels per kit
    /// pixel, its strings use only glyphs it has (with the known gaps named), money is written one way,
    /// and the tide table never shows a raw id.
    /// </summary>
    public class UiTextReviewTests
    {
        // ---- the snapped canvas scale --------------------------------------------------------------

        private const int Kit = DialogueBubbleKit.ArtScale;

        private static float BubbleScale(int w, int h)
            => PixelCanvasScale.Snap(PixelCanvasScale.MatchWidthOrHeight(w, h, 1280f, 720f, 0.5f), Kit);

        [TestCase(1280, 720, 1f)]
        [TestCase(2560, 1440, 2f)]
        [TestCase(3840, 2160, 3f)]
        [TestCase(1920, 1080, 4f / 3f)]
        [TestCase(1366, 768, 1f)]
        [TestCase(1600, 900, 1f)]
        public void TheBubbleScale_IsTheWindowsScaleRoundedDownToAWholeKitPixel(int w, int h, float expected)
        {
            Assert.That(BubbleScale(w, h), Is.EqualTo(expected).Within(1e-5f), $"{w}x{h}");
        }

        [Test]
        public void AKitPixel_IsAWholeNumberOfScreenPixels_OnEveryCommonWindow()
        {
            int[,] windows =
            {
                { 1280, 720 }, { 1366, 768 }, { 1440, 900 }, { 1536, 864 }, { 1600, 900 }, { 1680, 1050 },
                { 1920, 1080 }, { 1920, 1200 }, { 2560, 1080 }, { 2560, 1440 }, { 3440, 1440 }, { 3840, 2160 },
                { 800, 600 }, { 640, 360 },
            };
            for (int i = 0; i < windows.GetLength(0); i++)
            {
                int w = windows[i, 0], h = windows[i, 1];
                float raw = PixelCanvasScale.MatchWidthOrHeight(w, h, 1280f, 720f, 0.5f);
                float snapped = PixelCanvasScale.Snap(raw, Kit);
                float px = PixelCanvasScale.ScreenPixelsPerKitPixel(snapped, Kit);
                Assert.That(px, Is.EqualTo(Mathf.Round(px)).Within(1e-4f), $"{w}x{h}: a kit pixel is {px} screen pixels");
                Assert.That(px, Is.GreaterThanOrEqualTo(1f), $"{w}x{h}: never below one screen pixel");
                Assert.That(snapped, Is.LessThanOrEqualTo(raw + 1e-4f), $"{w}x{h}: never larger than the window's own scale");
            }
        }

        [Test]
        public void TheSnap_MatchesUnitysOwnScaleWhereThatIsAlreadyWhole()
        {
            Assert.That(PixelCanvasScale.MatchWidthOrHeight(1920, 1080, 1280, 720, 0.5f), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(PixelCanvasScale.Snap(2f, Kit), Is.EqualTo(2f).Within(1e-6f));
            Assert.That(PixelCanvasScale.Snap(0f, Kit), Is.EqualTo(1f / Kit).Within(1e-6f));
            Assert.That(PixelCanvasScale.Snap(1.37f, 0), Is.EqualTo(1.37f), "no kit: unchanged");
        }

        /// <summary>
        /// Every runtime canvas that sets the pixel face AND scales with the screen must use the snapped
        /// scaler, or a constant integer scale. The two left on the plain scaler are named: they place
        /// themselves with the HUD band's own scale, so moving them is a layout change of its own.
        /// </summary>
        [Test]
        public void EveryPixelFaceCanvasThatScalesWithTheScreen_IsSnapped_OrNamedHere()
        {
            var stillPlain = new HashSet<string>
            {
                "UI/InteractChooserView.cs",   // shares PaperUi.MakeScreen and the HUD band's scale
                "UI/InteractPopup.cs",         // ditto
                "UI/CharacterCreatorPanel.cs", // 1920x1080 reference: whole at 720p/1080p/1440p, not at 768p
            };

            string root = Path.Combine(Application.dataPath, "_Project", "Code");
            var offenders = new List<string>();
            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string rel = path.Substring(root.Length + 1).Replace('\\', '/');
                if (rel.Contains("/Editor/")) continue;
                string src = File.ReadAllText(path);
                bool face = src.Contains("HarbourType.ResourceKey") || src.Contains("HarbourType.Load(")
                            || src.Contains("BubbleFace.");
                bool scales = src.Contains("ScaleMode.ScaleWithScreenSize") || src.Contains("PaperUi.MakeScreen");
                if (!face || !scales) continue;
                if (src.Contains("PixelSnappedCanvasScaler")) continue;
                if (stillPlain.Contains(rel)) continue;
                offenders.Add(rel);
            }
            Assert.That(offenders, Is.Empty,
                "these draw the pixel face on a canvas that scales with the window but are not snapped " +
                "(at 1920x1080 a kit pixel would be 4.5 screen pixels): " + string.Join(", ", offenders));
        }

        // ---- the pixel face's glyphs ---------------------------------------------------------------

        /// <summary>Characters strings ask the face for that it does not have yet. Each needs a glyph drawn
        /// in the bubble kit and the face re-baked; until then the character draws as nothing.</summary>
        private static readonly char[] KnownMissing = { '₲' /* ₲ */, '▸' /* ▸ */, '◀' /* ◀ */ };

        private static HashSet<char> FaceGlyphs()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(HarbourType.FontPath);
            Assert.That(font, Is.Not.Null, HarbourType.FontPath + " does not load");
            return font.characterInfo.Select(c => (char)c.index).ToHashSet();
        }

        private static IEnumerable<string> StringsTheFaceDraws()
        {
            yield return string.Format(ShelfGood.BuyLabelFormat, "Hooks", MoneyFormat.Balance(1240));
            yield return string.Format(ShelfGood.SoldOutLabelFormat, "Bread");
            yield return CatalogBook.Price(1800);
            yield return CatalogBook.SoldOutStatus;
            yield return MoneyFormat.Payout(35);
            yield return WorldStrings.ContinueHint;
            yield return WorldStrings.OptionCursor;
            // The pages now set in the face: the shell, the tide table, the sell screen.
            foreach (var f in typeof(ShellStrings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string)) yield return (string)f.GetRawConstantValue();
            yield return HudStrings.TideTableTitle;
            yield return HudStrings.TideTableCloseHint;
            yield return HudStrings.TideTableNowMarker;
            yield return HudStrings.TideTableDayHeading(false, 1);
            yield return HudStrings.TideTurnLabel(true) + HudStrings.TideTurnLabel(false);
            yield return MoneyFormat.Balance(0) + "   x3";
        }

        [Test]
        public void TheFacesStrings_UseOnlyItsGlyphs_ExceptTheNamedGaps()
        {
            HashSet<char> have = FaceGlyphs();
            var unexpected = new SortedSet<string>();
            foreach (string s in StringsTheFaceDraws())
                foreach (char c in s)
                    if (c != ' ' && !have.Contains(c) && !KnownMissing.Contains(c))
                        unexpected.Add($"U+{(int)c:X4} '{c}' in \"{s}\"");
            Assert.That(unexpected, Is.Empty, "the pixel face cannot draw: " + string.Join("; ", unexpected));
        }

        [Test]
        public void TheNamedGaps_AreStillGaps()
        {
            HashSet<char> have = FaceGlyphs();
            foreach (char c in KnownMissing)
                Assert.That(have.Contains(c), Is.False,
                    $"the face now has U+{(int)c:X4}: take it off KnownMissing so the guard covers it again");
        }

        // ---- wording ------------------------------------------------------------------------------

        [Test]
        public void MoneyIsWrittenOneWay()
        {
            Assert.AreEqual(MoneyFormat.Balance(1800), CatalogBook.Price(1800));
            Assert.AreEqual("₲1,800", CatalogBook.Price(1800));
            Assert.AreEqual(MoneyFormat.Currency, CatalogBook.Money);
        }

        [Test]
        public void TheTideTable_NamesThePlace_NeverItsId()
        {
            Assert.AreEqual("for St Peters Island", HudStrings.TideTableForRegion("St Peters Island"));
            Assert.AreEqual(string.Empty, HudStrings.TideTableForRegion(null));
            Assert.AreEqual(string.Empty, HudStrings.TideTableForRegion("  "));
        }

        [Test]
        public void TheTideTable_CountsDaysFromOne_LikeTheTitlePage()
        {
            Assert.AreEqual("Today", HudStrings.TideTableDayHeading(true, 0));
            Assert.AreEqual("Tomorrow — day 2", HudStrings.TideTableDayHeading(false, 1),
                "clock day index 1 is the player's second day");
        }

        [Test]
        public void FaintInk_StillReadsOnThePaper()
        {
            // Faint ink is ink laid thin over the stock: blend it, then the usual contrast ratio.
            Color paper = NotebookInk.Paper, faint = NotebookInk.InkFaint;
            Color seen = Color.Lerp(paper, faint, faint.a);
            Assert.GreaterOrEqual(ContrastRatio(seen, paper), 4.5f,
                "a done step or a greyed row is faded, not gone — small pixel text needs 4.5:1");
            Assert.Less(faint.a, 1f, "and it is still lighter than full ink");
        }

        private static float ContrastRatio(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static float Luminance(Color c)
        {
            static float Ch(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Ch(c.r) + 0.7152f * Ch(c.g) + 0.0722f * Ch(c.b);
        }

        // ---- pass 2: no ids, no dev words, one way to name a key ---------------------------------

        static readonly System.Text.RegularExpressions.Regex IdShape =
            new System.Text.RegularExpressions.Regex(@"\b[a-z]+\.[a-z][a-z0-9]*_[a-z0-9_]+\b|\b(fish|seller|region|npc|item|license|trade|quest|bait)\.[a-z_]+\b");

        static readonly string[] DevWords = { "(dev)", "wired", "ADR ", "TODO", "OPTIONAL", "UPGRADEABLE", "null" };

        /// <summary>Every player-facing constant on the strings tables, minus the ones that are ids by
        /// name (a transit id, a resource key). Reflection, so a new line is checked the day it lands.</summary>
        static IEnumerable<TestCaseData> PlayerStrings()
        {
            var types = new List<System.Type>
            {
                typeof(HudStrings), typeof(WorldStrings), typeof(ShellStrings),
                typeof(WardrobeStrings), typeof(HiddenHarbours.Player.ControlStrings),
                typeof(HiddenHarbours.Boats.BoatAnchor), typeof(HiddenHarbours.Fishing.DevFishingInput),
            };
            System.Type creator = typeof(HudStrings).Assembly.GetType("HiddenHarbours.UI.CharacterCreatorViewStrings");
            if (creator != null) types.Add(creator);
            foreach (System.Type t in types)
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                                              System.Reflection.BindingFlags.Static))
                {
                    if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                    if (f.Name.StartsWith("Id") || f.Name.EndsWith("Id") || f.Name.EndsWith("Key") || f.Name.EndsWith("Path")
                        || f.Name.EndsWith("Prefix") || f.Name.EndsWith("Folder")) continue;
                    yield return new TestCaseData((string)f.GetRawConstantValue()).SetName($"PlayerString_{t.Name}_{f.Name}");
                }
        }

        [TestCaseSource(nameof(PlayerStrings))]
        public void NoPlayerString_ShowsAnIdOrADevWord(string text)
        {
            Assert.IsFalse(IdShape.IsMatch(text ?? ""), $"\"{text}\" shows an id to the player");
            foreach (string w in DevWords)
                StringAssert.DoesNotContain(w, text ?? "", $"\"{text}\" carries a developer word");
        }

        [TestCaseSource(nameof(PlayerStrings))]
        public void NoPlayerString_SpellsAKeyAnotherWay(string text)
        {
            foreach (string old in new[] { "Esc / B", "Enter / A", "Arrows / stick", "Enter / Start", "Enter or pad A", "(E)", "Enter or Done", "Start or Done", "·", "←", "→", "↑", "↓" })
                StringAssert.DoesNotContain(old, text ?? "", $"\"{text}\" names a key by hand; write it through KeyNames");
        }

        [TestCaseSource(nameof(PlayerStrings))]
        public void NoPlayerString_SaysPail(string text)
            => Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text ?? "", @"\b[Pp]ails?\b"),
                $"\"{text}\" says pail; the word is bucket");

        [Test]
        public void NoDataTheyRead_SaysPail()
        {
            // Every text line in the data folder, minus the lines that are ids or asset names (a bucket
            // TIER id "pail" is append-only and never shown).
            var skip = new System.Text.RegularExpressions.Regex(@"^\s*-?\s*(m_Name|m_EditorClassIdentifier|\w*Id|Provenance|\w*Path|guid)\s*:");
            var pail = new System.Text.RegularExpressions.Regex(@"\b[Pp]ails?\b");
            var hits = new List<string>();
            foreach (string path in Directory.GetFiles("Assets/_Project/Data", "*.asset", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                    if (pail.IsMatch(lines[i]) && !skip.IsMatch(lines[i]))
                        hits.Add($"{path}:{i + 1}: {lines[i].Trim()}");
            }
            Assert.IsEmpty(hits, "the word is bucket:\n" + string.Join("\n", hits));
        }

        [Test]
        public void EverySeller_HasAName_AndTheBookShowsIt_NeverTheId()
        {
            SellerDef[] sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder);
            Assert.IsNotEmpty(sellers);
            foreach (SellerDef s in sellers)
            {
                string shown = SellerDirectory.DisplayNameFor(s.Id, sellers);
                Assert.IsFalse(string.IsNullOrWhiteSpace(shown), s.Id + " has no display name");
                Assert.AreNotEqual(s.Id, shown);
                Assert.IsFalse(IdShape.IsMatch(shown), $"{s.Id} is named \"{shown}\"");
            }
            Assert.AreEqual("", SellerDirectory.DisplayNameFor("seller.nobody_at_all", sellers),
                "an unknown seller reads as nothing, never as its id");
        }

        [TestCase("fish.atlantic_cod", "Atlantic cod")]
        [TestCase("bootCol", "Boot")]
        [TestCase("hatCol", "Hat")]
        [TestCase("soft_shell_clam", "Soft shell clam")]
        [TestCase("", "")]
        public void ACodeName_ReadsPlain(string id, string expected)
            => Assert.AreEqual(expected, PlainName.FromId(id));

        [Test]
        public void TheCatalogue_IsHeadedByTheSellersName()
        {
            string src = File.ReadAllText("Assets/_Project/Code/Economy/CatalogBookPresenter.cs");
            StringAssert.Contains("SellerDirectory.DisplayNameFor(sellerId)", src,
                "the head reads the seller's name from data, not the id");
        }

        [Test]
        public void KeyNames_UseOnlyTheFacesLetters()
        {
            Font face = AssetDatabase.LoadAssetAtPath<Font>(HarbourType.FontPath);
            Assume.That(face, Is.Not.Null, "the face is not imported in this box");
            foreach (var f in typeof(KeyNames).GetFields())
            {
                if (!f.IsLiteral) continue;
                string s = (string)f.GetRawConstantValue();
                foreach (char c in s)
                    Assert.IsTrue(face.HasCharacter(c), $"KeyNames.{f.Name} uses '{c}', which the face does not have");
            }
        }

        [Test]
        public void TheHints_NameTheirKeysThroughTheTable()
        {
            StringAssert.Contains(KeyNames.BackEither + KeyNames.Says, ReadCreatorHint(), "the creator hint names Esc/B from the table");
            StringAssert.StartsWith(KeyNames.TideTable + " ", WorldStrings.OnboardDigClams);
            StringAssert.StartsWith(KeyNames.Interact + " ", WorldStrings.ContinueHint);
            StringAssert.Contains(KeyNames.Back + KeyNames.Says, ShellStrings.PauseHint);
        }

        static string ReadCreatorHint()
        {
            System.Type creator = typeof(HudStrings).Assembly.GetType("HiddenHarbours.UI.CharacterCreatorViewStrings");
            Assume.That(creator, Is.Not.Null);
            return (string)creator.GetField("Hint").GetRawConstantValue();
        }
    }
}
