using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE GEAR PAGE</b>: the gear he owns, each with its picture, name and a count or state, under a
    /// few plain headings, on its own stub beside the catch page. Rows come from a fake ownership source
    /// here; the save-backed source is proven on its own below.
    /// </summary>
    public class NotebookGearPageTests
    {
        const int Cols = 31;

        /// <summary>The closest-fitting layout at 1280x720 and at 1920x1080 alike (scale 4 and 6).</summary>
        const int ShippedCols = 17, ShippedLines = 12;

        const string PicturedId = "gear.test_pictured_item";
        const string UnpicturedId = "gear.test_unpictured_item";

        readonly List<Object> _spawned = new List<Object>();

        sealed class FakeGear : INotebookGearSource
        {
            public readonly List<NotebookGearHolding> Held = new List<NotebookGearHolding>();
            public void Collect(List<NotebookGearHolding> into)
            {
                into.Clear();
                into.AddRange(Held);
            }
        }

        static NotebookGearHolding Kept(string id) => new NotebookGearHolding(id, NotebookGearCount.Presence);

        NotebookGearPageDef Page(bool listUnowned = false)
        {
            var page = ScriptableObject.CreateInstance<NotebookGearPageDef>();
            _spawned.Add(page);
            page.Id = "notebook_page.test";
            page.ListUnowned = listUnowned;
            page.OtherHeading = "Other gear";
            page.Groups = new[]
            {
                new NotebookGearPageDef.Group
                {
                    Heading = "Rods and tackle",
                    Entries = new[]
                    {
                        new NotebookGearPageDef.Entry { Id = "gear.rod", Name = "Fishing Rod" },
                        new NotebookGearPageDef.Entry { Id = "tackle.spoon", Name = "Spoon" },
                    },
                },
                new NotebookGearPageDef.Group
                {
                    Heading = "Pots and lines",
                    Entries = new[] { new NotebookGearPageDef.Entry { Id = "trap.lobster", Name = "Lobster Pot" } },
                },
                new NotebookGearPageDef.Group
                {
                    Heading = "Boat electronics",
                    Entries = new[] { new NotebookGearPageDef.Entry { Id = "instrument.depth_sounder", Name = "Depth Sounder" } },
                },
            };
            return page;
        }

        static string[] Texts(NotebookBlock b) => b.Rows.Select(r => r.Text).ToArray();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
        }

        // ---- the stub ---------------------------------------------------------------------------------

        [Test]
        public void NothingOwnedMeansNoStub_AndOwningSomethingAddsItBesideTheCatchPage()
        {
            var none = NotebookContent.Build(null, null, null, new InMemoryFlagStore(), Cols, null, null,
                                             new List<NotebookGearHolding>(), Page());
            Assert.That(none.Any(t => t.Key == NotebookContent.TabGear), Is.False);

            var caught = new[] { new CaughtSpeciesEntry("fish.atlantic_cod", "Atlantic Cod", 1, 0, 2f) };
            var tabs = NotebookContent.Build(null, null, null, new InMemoryFlagStore(), Cols, null, caught,
                                             new[] { Kept("gear.rod") }, Page());

            Assert.That(tabs.Select(t => t.Key).ToArray(),
                        Is.EqualTo(new[] { "tab.quests", "tab.done", NotebookContent.TabCaught, NotebookContent.TabGear, "tab.notes" }));
            NotebookTab gear = tabs.First(t => t.Key == NotebookContent.TabGear);
            Assert.That(gear.Kind, Is.EqualTo(NotebookPageKind.Gear));
            Assert.That(gear.Label.Length, Is.LessThanOrEqualTo(NotebookKit.ChipChars), "the chip never clips it");
        }

        // ---- rows and grouping ------------------------------------------------------------------------

        [Test]
        public void RowsComeFromTheSourceInPageOrder_HeadingOnTheFirstItemOfEachGroupOnly()
        {
            // Acquisition order is NOT page order: the page Def decides.
            var held = new[]
            {
                new NotebookGearHolding("trap.lobster", NotebookGearCount.Stock, 5, 2),
                Kept("tackle.spoon"),
                Kept("gear.rod"),
                new NotebookGearHolding("instrument.depth_sounder", NotebookGearCount.Fitted, 2),
            };

            var blocks = NotebookGearPage.Blocks(held, Page(), Cols);

            Assert.That(blocks.Select(b => b.SourceId).ToArray(), Is.EqualTo(new[]
            {
                "gear_page.gear.rod", "gear_page.tackle.spoon", "gear_page.trap.lobster", "gear_page.instrument.depth_sounder",
            }));
            Assert.That(Texts(blocks[0]), Is.EqualTo(new[] { "RODS AND TACKLE", "Fishing Rod", "" }),
                        "heading, name, and a blank second line so the picture has its two lines");
            Assert.That(Texts(blocks[1]), Is.EqualTo(new[] { "Spoon", "" }), "no second heading in the same group");
            Assert.That(Texts(blocks[2]), Is.EqualTo(new[] { "POTS AND LINES", "Lobster Pot", "Have 5, 2 set" }));
            Assert.That(Texts(blocks[3]), Is.EqualTo(new[] { "BOAT ELECTRONICS", "Depth Sounder", "On 2 boats" }));
        }

        [Test]
        public void EveryItemLineCarriesItsPicture_TheHeadingNone_AndOnlyTheFirstLineDrawsIt()
        {
            var blocks = NotebookGearPage.Blocks(new[] { new NotebookGearHolding("trap.lobster", NotebookGearCount.Stock, 3) },
                                                 Page(), Cols);
            NotebookRow[] rows = blocks[0].Rows.ToArray();

            Assert.That(rows[0].HasIcon, Is.False, "the heading is lettered across the full lane");
            Assert.That(rows.Skip(1).All(r => r.IconId == "trap.lobster"), Is.True);
            Assert.That(rows[1].Continuation, Is.False, "the picture is drawn beside the name");
            Assert.That(rows.Skip(2).All(r => r.Continuation), Is.True, "and once only");
            Assert.That(Texts(blocks[0])[2], Is.EqualTo("Have 3"));
        }

        [Test]
        public void OwnedGearNoGroupListsGoesUnderOtherGear_NamedFromItsId_NeverDropped()
        {
            var blocks = NotebookGearPage.Blocks(new[] { Kept("gear.rod"), Kept("gear.rod_rack"), Kept("gear.lobster_gaff") },
                                                 Page(), Cols);

            Assert.That(blocks.Count, Is.EqualTo(3));
            Assert.That(Texts(blocks[1]), Is.EqualTo(new[] { "OTHER GEAR", "Rod rack", "" }));
            Assert.That(Texts(blocks[2]), Is.EqualTo(new[] { "Lobster gaff", "" }));
        }

        [Test]
        public void ANullPageStillListsEverythingOwned_UnderOneHeading()
        {
            var blocks = NotebookGearPage.Blocks(new[] { Kept("gear.rod"), Kept("tackle.spoon") }, null, Cols);
            Assert.That(Texts(blocks[0]), Is.EqualTo(new[] { "GEAR", "Rod", "" }));
            Assert.That(Texts(blocks[1]), Is.EqualTo(new[] { "Spoon", "" }));
        }

        [Test]
        public void ListUnownedIsAPageFlag_TheUnownedAreFaintAndSayNotYet()
        {
            var held = new[] { Kept("gear.rod") };
            Assert.That(NotebookGearPage.Blocks(held, Page(), Cols).Count, Is.EqualTo(1), "owned only by default");

            var all = NotebookGearPage.Blocks(held, Page(listUnowned: true), Cols);
            Assert.That(all.Count, Is.EqualTo(4), "every entry, same source");
            Assert.That(all[0].Rows.Skip(1).All(r => r.State == NotebookStepState.Todo), Is.True, "owned: full ink");
            Assert.That(Texts(all[1]), Is.EqualTo(new[] { "Spoon", "Not yet" }));
            Assert.That(all[1].Rows.All(r => r.State == NotebookStepState.Done), Is.True, "unowned: lettered faint");
        }

        [Test]
        public void EveryLineFitsItsLaneAtTheNarrowestBook()
        {
            var held = new[]
            {
                new NotebookGearHolding("watch.harbour_digital", NotebookGearCount.Presence),
                new NotebookGearHolding("trap.lobster", NotebookGearCount.Stock, 1200, 1100),
                new NotebookGearHolding("instrument.loran", NotebookGearCount.Fitted, 12),
                Kept("gear.a_very_long_piece_of_gear_with_a_long_name"),
            };
            int cols = NotebookKit.MinCols;
            foreach (NotebookBlock b in NotebookGearPage.Blocks(held, Page(), cols))
                foreach (NotebookRow r in b.Rows)
                {
                    int lane = r.HasIcon ? NotebookLayout.IconTextColsFor(cols) : NotebookKit.TitleColsFor(cols);
                    Assert.That(r.Text.Length, Is.LessThanOrEqualTo(lane), $"'{r.Text}' overruns its lane");
                }
        }

        // ---- paging -----------------------------------------------------------------------------------

        [Test]
        public void ALateGameKitPagesOntoMoreSpreads_NothingDroppedNothingSplit()
        {
            var held = new List<NotebookGearHolding>();
            for (int i = 0; i < 30; i++) held.Add(new NotebookGearHolding("gear.item_" + i, NotebookGearCount.Stock, i + 1));

            var blocks = NotebookGearPage.Blocks(held, Page(), ShippedCols);
            var spreads = NotebookLayout.Paginate(blocks, ShippedCols, ShippedLines);

            Assert.That(spreads.Count, Is.GreaterThan(1), "overflow is more pages");
            var placements = spreads.SelectMany(s => s.Left.Placements.Concat(s.Right.Placements)).ToList();
            Assert.That(placements.Select(p => p.Block).Distinct().Count(), Is.EqualTo(blocks.Count), "every item somewhere");
            Assert.That(placements.Any(p => p.IsSplit), Is.False, "a picture never parts from its name");
        }

        [Test]
        public void TheBookTurnsThroughTheGearPage()
        {
            var fake = new FakeGear();
            for (int i = 0; i < 30; i++) fake.Held.Add(Kept("gear.item_" + i));
            NotebookPresenter book = Book(fake, Page());

            book.RebuildModel();
            int gearTab = book.TabIndexOf(NotebookContent.TabGear);
            Assert.That(gearTab, Is.GreaterThanOrEqualTo(0));
            book.SelectTab(gearTab);
            Assert.That(book.Spreads.Count, Is.GreaterThan(1));

            for (int i = 0; i < book.Spreads.Count; i++) book.TurnPage(1);
            Assert.That(book.SpreadIndex, Is.EqualTo(book.Spreads.Count - 1), "the axis reaches the last page");
            Assert.That(book.HasNextSpread, Is.False);
        }

        // ---- the drawing: picture or paper placeholder ------------------------------------------------

        [Test]
        public void AnIdWithAnIconDrawsIt_AnIdWithoutOneDrawsThePaperPlaceholder()
        {
            var tex = new Texture2D(24, 24);
            _spawned.Add(tex);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32f);
            _spawned.Add(sprite);
            IconRegistry.Register(PicturedId, sprite);

            var fake = new FakeGear();
            fake.Held.Add(Kept(PicturedId));
            fake.Held.Add(Kept(UnpicturedId));
            NotebookPresenter book = Book(fake, Page());

            book.Open();
            book.SelectTab(book.TabIndexOf(NotebookContent.TabGear));

            Image[] icons = book.GetComponentsInChildren<Image>(true)
                                .Where(i => i.name == NotebookPresenter.IconPieceName).ToArray();
            Image[] slots = book.GetComponentsInChildren<Image>(true)
                                .Where(i => i.name == NotebookPresenter.IconPlaceholderName).ToArray();

            Assert.That(icons.Length, Is.EqualTo(1), "one picture");
            Assert.That(icons[0].sprite, Is.SameAs(sprite), "and it is the id's own icon");
            Assert.That(slots.Length, Is.EqualTo(1), "the id with no icon gets the paper slot");
            Assert.That(slots[0].sprite, Is.Null, "a tinted paper rect, never a missing-texture square");
            Assert.That(slots[0].GetComponentsInChildren<Text>(true).Select(t => t.text).ToArray(), Is.EqualTo(new[] { "T" }),
                        "lettered with the item's initial");
            string lettered = string.Join(" ", book.GetComponentsInChildren<Text>(true).Select(t => t.text));
            Assert.That(lettered, Does.Contain("Test").And.Contain("unpictured"),
                        "and its name beside it (wrapped or not, at whatever size this screen gives the book)");
            book.Close();
        }

        // ---- the save-backed source -------------------------------------------------------------------

        [Test]
        public void TheSaveSourceReadsTheLockers_TheWatchOnceAndFirst()
        {
            var watch = ScriptableObject.CreateInstance<WatchDef>();
            _spawned.Add(watch);
            watch.Id = "watch.harbour_digital";
            watch.StartsOwned = true;
            watch.LegacyGearId = "gear.watch";

            var save = new SaveData();
            save.OwnedGear.AddRange(new[] { "gear.rod", "gear.watch", "tackle.spoon", "gear.rod" });
            save.PotStock.Add(new PotStock("trap.lobster", 5));
            save.PlacedTraps.Add(new PlacedTrapDto { InstanceId = "a", TrapDefId = "trap.lobster" });
            save.PlacedTraps.Add(new PlacedTrapDto { InstanceId = "b", TrapDefId = "trap.lobster" });
            save.PotStock.Add(new PotStock("trap.crab", 0));
            save.TubStock.Add(new TubStock("longline.groundfish_tub", 3));
            save.BaitStock.Add(new BaitStock("bait.herring", 12));
            save.HullInstruments.Add(new HullInstrument("boat.skiff", "instrument.depth_sounder"));
            save.HullInstruments.Add(new HullInstrument("boat.lobster_boat", "instrument.depth_sounder"));

            var held = new List<NotebookGearHolding>();
            SaveGearSource.Collect(save, new[] { watch }, held);

            Assert.That(held.Select(h => h.Id).ToArray(), Is.EqualTo(new[]
            {
                "watch.harbour_digital", "gear.rod", "tackle.spoon", "trap.lobster",
                "longline.groundfish_tub", "bait.herring", "instrument.depth_sounder",
            }), "the legacy watch id and the empty crab stock are not listed; nothing twice");
            NotebookGearHolding pots = held.First(h => h.Id == "trap.lobster");
            Assert.That((pots.Kind, pots.Count, pots.InWater), Is.EqualTo((NotebookGearCount.Stock, 5, 2)));
            NotebookGearHolding sounder = held.First(h => h.Id == "instrument.depth_sounder");
            Assert.That((sounder.Kind, sounder.Count), Is.EqualTo((NotebookGearCount.Fitted, 2)));

            SaveGearSource.Collect(null, null, held);
            Assert.That(held, Is.Empty, "no save holds nothing");
        }

        // ---- the shipped page Def ---------------------------------------------------------------------

        [Test]
        public void TheShippedPage_LoadsAndNamesEveryEntryAsItsOwnDefDoes()
        {
            NotebookGearPageDef page = NotebookContentSource.GearPage();
            Assert.That(page, Is.Not.Null, "Resources/" + NotebookContentSource.GearPageKey + " must ship");
            Assert.That(page.ListUnowned, Is.False, "owned gear only, by the seat's lean");

            // One pass over the data root, indexing every declared id to the first file that declares it.
            // The data root is about 100 MB of text (hull meshes and skins are megabytes each); scanning all
            // of it again for every entry on the page ran this one test to the 180 s timeout on CI.
            var idLine = new Regex(@"^\s+Id: (.+?)\r?$", RegexOptions.Multiline);
            var declaringById = new Dictionary<string, string>();
            foreach (string path in Directory.EnumerateFiles("Assets/_Project/Data", "*.asset", SearchOption.AllDirectories)
                                             .Where(p => !p.Replace('\\', '/').Contains("/NotebookPages/")))
            {
                string text = File.ReadAllText(path);
                if (text.IndexOf("Id: ", System.StringComparison.Ordinal) < 0) continue;
                foreach (Match m in idLine.Matches(text))
                    if (!declaringById.ContainsKey(m.Groups[1].Value)) declaringById.Add(m.Groups[1].Value, text);
            }
            var seen = new HashSet<string>();

            foreach (NotebookGearPageDef.Group g in page.Groups)
            {
                Assert.That(g.Heading, Is.Not.Empty);
                foreach (NotebookGearPageDef.Entry e in g.Entries)
                {
                    Assert.That(seen.Add(e.Id), Is.True, $"{e.Id}: listed twice");
                    declaringById.TryGetValue(e.Id, out string declaring);
                    Assert.That(declaring, Is.Not.Null, $"{e.Id}: no Def declares this id");

                    Match name = Regex.Match(declaring, @"^  DisplayName: (.+?)\r?$", RegexOptions.Multiline);
                    if (name.Success)
                        Assert.That(e.Name, Is.EqualTo(name.Groups[1].Value.Trim().Trim('\'')),
                                    $"{e.Id}: the page must letter the Def's own name");
                    else
                        Assert.That(e.Name, Is.Not.Empty, $"{e.Id}: its Def has no name, so the page must give one");
                }
            }
        }

        NotebookPresenter Book(INotebookGearSource gear, NotebookGearPageDef page)
        {
            var go = new GameObject("book");
            _spawned.Add(go);
            var book = go.AddComponent<NotebookPresenter>();
            book.WireContent(() => new List<QuestDef>(), () => new List<KnowledgePageDef>(),
                             () => new List<NotebookNote>(), new InMemoryFlagStore());
            book.WireGear(gear, page);
            return book;
        }
    }
}
