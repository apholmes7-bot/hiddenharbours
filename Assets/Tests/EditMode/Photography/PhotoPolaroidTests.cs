using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The album's little Polaroids (owner 2026-11-26: "yes photoalbum should save small pictures as little
    /// poloraids"): the stored thumbnail's form and size, its round trip through the save, the v27 to v28
    /// step, the album's cap, the load repair, the camera def's tunables and the album page's pictured
    /// block. All headless.
    /// </summary>
    public class PhotoPolaroidTests
    {
        const string Local = SavePlayerKeys.Local;
        const string Guest = "player.guest.1";
        const string Seal = "wildlife.grey_seal";
        const string Region = "region.st_peters";

        const string V27File =
            "{\"SchemaVersion\":27,\"WorldSeed\":77,\"GameTimeSeconds\":4321.5,\"Money\":250,\"DayIndex\":3," +
            "\"OwnedBoats\":[\"boat.dory\"],\"ActiveHullId\":\"boat.dory\"}";

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ---- the stored form ----------------------------------------------------------------------------

        [Test]
        public void TheStoredSize_IsTheHeaderAndTwoBytesAPixel()
        {
            Assert.AreEqual(2 + 2 * 24 * 17, PhotoThumbnail.ByteLength(24, 17));
            Assert.AreEqual(818, PhotoThumbnail.ByteLength(24, 17));
            Assert.AreEqual(1092, PhotoThumbnail.EncodedLength(24, 17));
            string stored = PhotoThumbnail.Encode(Pattern(24, 17), 24, 17);
            Assert.AreEqual(PhotoThumbnail.EncodedLength(24, 17), stored.Length);
        }

        [Test]
        public void EncodeDecode_RoundTrips_WhiteAndBlackExactly_AndKeepsTheRowOrder()
        {
            const int w = 5, h = 3;
            Color32[] source = Pattern(w, h);
            source[0] = new Color32(255, 255, 255, 255);
            source[w * h - 1] = new Color32(0, 0, 0, 255);

            Color32[] back = PhotoThumbnail.Decode(PhotoThumbnail.Encode(source, w, h), out int bw, out int bh);

            Assert.AreEqual(w, bw);
            Assert.AreEqual(h, bh);
            Assert.AreEqual(new Color32(255, 255, 255, 255), back[0], "white stays white");
            Assert.AreEqual(new Color32(0, 0, 0, 255), back[w * h - 1], "black stays black");
            for (int i = 0; i < w * h; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(source[i].r - back[i].r), 7, "red " + i);
                Assert.LessOrEqual(Mathf.Abs(source[i].g - back[i].g), 3, "green " + i);
                Assert.LessOrEqual(Mathf.Abs(source[i].b - back[i].b), 7, "blue " + i);
                Assert.AreEqual(255, back[i].a, "a picture is opaque");
            }
        }

        [Test]
        public void ADecodedPicture_EncodesToTheSameString()
        {
            string stored = PhotoThumbnail.Encode(Pattern(24, 17), 24, 17);
            Color32[] back = PhotoThumbnail.Decode(stored, out int w, out int h);
            Assert.AreEqual(stored, PhotoThumbnail.Encode(back, w, h), "the stored form is stable");
        }

        [Test]
        public void ABrokenString_IsNotAPicture()
        {
            string good = PhotoThumbnail.Encode(Pattern(4, 4), 4, 4);
            Assert.IsTrue(PhotoThumbnail.IsValid(good));
            Assert.IsFalse(PhotoThumbnail.IsValid(null));
            Assert.IsFalse(PhotoThumbnail.IsValid(""));
            Assert.IsFalse(PhotoThumbnail.IsValid("not base64 at all!"));
            Assert.IsFalse(PhotoThumbnail.IsValid(System.Convert.ToBase64String(new byte[] { 4, 4, 1, 2 })),
                           "a length that does not match its header");
            Assert.IsFalse(PhotoThumbnail.IsValid(System.Convert.ToBase64String(new byte[] { 0, 4 })), "a zero side");
            Assert.IsNull(PhotoThumbnail.Decode("not base64 at all!", out _, out _));
            Assert.IsNull(PhotoThumbnail.Encode(new Color32[3], 2, 2), "a short array");
            Assert.IsNull(PhotoThumbnail.Encode(new Color32[256 * 1], 256, 1), "a side over a header byte");
        }

        [Test]
        public void PointSample_TakesTheOnePixelAtEachCentre_NeverABlend()
        {
            // 4 x 2, rows bottom to top: each 2 x 2 quarter a single colour.
            var red = new Color32(200, 0, 0, 255);
            var blue = new Color32(0, 0, 200, 255);
            var source = new[] { red, red, blue, blue, red, red, blue, blue };
            var into = new Color32[2];

            Assert.IsTrue(PhotoThumbnail.PointSample(source, 4, 2, into, 2, 1));
            Assert.AreEqual(red, into[0]);
            Assert.AreEqual(blue, into[1]);
            Assert.IsFalse(PhotoThumbnail.PointSample(source, 4, 2, new Color32[1], 2, 1), "a short target");
        }

        // ---- the save -----------------------------------------------------------------------------------

        [Test]
        public void APictureWithItsThumbnail_RoundTripsThroughTheSave_ByteStable()
        {
            SaveData save = SaveMigration.NewGame();
            string stored = PhotoThumbnail.Encode(Pattern(24, 17), 24, 17);
            Assert.IsNotNull(Add(save, Local, 120, stored));

            string first = SaveSerialization.ToJson(save);
            SaveData loaded = SaveSerialization.FromJson(first);
            Assert.AreEqual(stored, loaded.Photos[0].Thumbnail);
            Assert.AreEqual(first, SaveSerialization.ToJson(loaded));
            Assert.AreEqual(first, SaveSerialization.ToJson(SaveSerialization.FromJson(SaveSerialization.ToJson(loaded))));
        }

        [Test]
        public void AThumbnail_AddsExactlyItsEncodedLengthToTheSave()
        {
            SaveData without = SaveMigration.NewGame();
            SaveData with = SaveMigration.NewGame();
            Add(without, Local, 120, null);
            Add(with, Local, 120, PhotoThumbnail.Encode(Pattern(24, 17), 24, 17));

            int growth = SaveSerialization.ToJson(with).Length - SaveSerialization.ToJson(without).Length;
            Assert.AreEqual(PhotoThumbnail.EncodedLength(24, 17), growth);
        }

        [Test]
        public void AV27File_StepsToV28_WithAnEmptyAlbum_AndAThumbnailFieldOnEveryNewPicture()
        {
            SaveData save = SaveSerialization.FromJson(V27File);
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(0, save.Photos.Count);
            Assert.AreEqual(250, save.Money);

            PhotoRecordDto row = Add(save, Local, 120, null);
            Assert.AreEqual("", row.Thumbnail, "a headless shutter keeps the record with no picture");
            StringAssert.Contains("\"Thumbnail\"", SaveSerialization.ToJson(save));
        }

        [Test]
        public void AV28RowWrittenBeforeTheThumbnail_LoadsWithAnEmptyOne()
        {
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":28,\"Photos\":[{\"PlayerKey\":\"player.local\",\"Number\":1,\"Score\":50," +
                "\"Subjects\":[{\"SubjectId\":\"wildlife.grey_seal\",\"Count\":1,\"Pose\":0,\"Score\":50}]}]}");
            Assert.AreEqual(1, save.Photos.Count);
            Assert.AreEqual("", save.Photos[0].Thumbnail);
        }

        [Test]
        public void TheAlbumCap_KeepsNoMore_ButAnotherPlayersIsHisOwn()
        {
            SaveData save = SaveMigration.NewGame();
            string stored = PhotoThumbnail.Encode(Pattern(4, 4), 4, 4);
            for (int i = 0; i < 3; i++) Assert.IsNotNull(Add(save, Local, 3, stored));
            Assert.IsNull(Add(save, Local, 3, stored), "a full album keeps no more, picture or not");
            Assert.IsNotNull(Add(save, Guest, 3, stored));
            Assert.AreEqual(3, PhotoAlbumRows.Count(save, Local));
            Assert.AreEqual(4, save.Photos.Count);
        }

        [Test]
        public void TheShippedCap_BoundsTheWorstCaseGrowth()
        {
            PhotoCameraDef def = PhotoCameraDef.Load();
            Assert.IsNotNull(def, "the camera ships");
            SaveData save = SaveMigration.NewGame();
            string stored = PhotoThumbnail.Encode(Pattern(def.ThumbnailWidthPx, def.ThumbnailHeightPx),
                                                  def.ThumbnailWidthPx, def.ThumbnailHeightPx);
            for (int i = 0; i < def.AlbumCapacity; i++) Assert.IsNotNull(Add(save, Local, def.AlbumCapacity, stored));
            Assert.IsNull(Add(save, Local, def.AlbumCapacity, stored));

            int thumbnails = 0;
            foreach (PhotoRecordDto p in save.Photos) thumbnails += p.Thumbnail.Length;
            Assert.AreEqual(def.AlbumCapacity * def.ThumbnailEncodedLength, thumbnails);
            SaveData loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            Assert.AreEqual(def.AlbumCapacity, loaded.Photos.Count);
        }

        [Test]
        public void Add_DoesNotKeepABrokenThumbnail_ButKeepsThePicture()
        {
            SaveData save = SaveMigration.NewGame();
            PhotoRecordDto row = Add(save, Local, 120, "not base64 at all!");
            Assert.IsNotNull(row);
            Assert.AreEqual("", row.Thumbnail);
        }

        [Test]
        public void SetThumbnail_GivesAKeptPictureItsImage_AndRefusesABrokenOne()
        {
            SaveData save = SaveMigration.NewGame();
            PhotoRecordDto row = Add(save, Local, 120, null);
            string stored = PhotoThumbnail.Encode(Pattern(4, 4), 4, 4);

            Assert.IsFalse(PhotoAlbumRows.SetThumbnail(save, row.Number, "broken"));
            Assert.AreEqual("", row.Thumbnail);
            Assert.IsFalse(PhotoAlbumRows.SetThumbnail(save, row.Number + 5, stored), "no such picture");
            Assert.IsTrue(PhotoAlbumRows.SetThumbnail(save, row.Number, stored));
            Assert.AreEqual(stored, row.Thumbnail);
            Assert.IsFalse(PhotoAlbumRows.SetThumbnail(null, 1, stored));
        }

        [Test]
        public void TheLoadRepair_ClearsABrokenThumbnail_AndKeepsTheRecord()
        {
            string good = PhotoThumbnail.Encode(Pattern(4, 4), 4, 4);
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":28,\"Photos\":[" + Row(1, "broken!") + "," + Row(2, good) + "]}");

            Assert.AreEqual(2, save.Photos.Count, "no picture is dropped for its thumbnail");
            Assert.AreEqual("", save.Photos[0].Thumbnail);
            Assert.AreEqual(50, save.Photos[0].Score);
            Assert.AreEqual(good, save.Photos[1].Thumbnail);

            string once = SaveSerialization.ToJson(save);
            SaveMigration.Migrate(save);
            Assert.AreEqual(once, SaveSerialization.ToJson(save), "the repair is idempotent");
        }

        // ---- the camera def's tunables ------------------------------------------------------------------

        [Test]
        public void TheShippedCamera_SizesASmallPolaroid_WithTheWiderBorderAtTheFoot()
        {
            PhotoCameraDef def = PhotoCameraDef.Load();
            Assert.IsNotNull(def);
            Assert.IsNull(def.ValidationError());
            Assert.Greater(def.ThumbnailWidthPx, 0);
            Assert.Greater(def.ThumbnailHeightPx, 0);
            Assert.Greater(def.PolaroidBottomBorderPx, def.PolaroidBorderPx);
            Assert.AreEqual(def.ThumbnailWidthPx + 2 * def.PolaroidBorderPx, def.PolaroidWidthPx);
            Assert.AreEqual(def.PolaroidBorderPx + def.ThumbnailHeightPx + def.PolaroidBottomBorderPx,
                            def.PolaroidHeightPx);
            Assert.AreEqual(PhotoThumbnail.EncodedLength(def.ThumbnailWidthPx, def.ThumbnailHeightPx),
                            def.ThumbnailEncodedLength);
            Assert.AreEqual(255, def.PolaroidFrameColour.a, "the card is opaque");
        }

        [Test]
        public void ADefWithTheNarrowerBorderAtTheFoot_IsRefused()
        {
            PhotoCameraDef def = Shipped();
            def.PolaroidBottomBorderPx = def.PolaroidBorderPx - 1;
            StringAssert.Contains("bottom border", def.ValidationError());
            def = Shipped();
            def.ThumbnailWidthPx = 0;
            StringAssert.Contains("thumbnail size", def.ValidationError());
        }

        // ---- the album page -----------------------------------------------------------------------------

        [Test]
        public void TheShippedCamera_Keeps48x34Pictures_InFourAndTwelveBorders_SixtyToAnAlbum()
        {
            PhotoCameraDef def = PhotoCameraDef.Load();
            Assert.AreEqual(48, def.ThumbnailWidthPx);
            Assert.AreEqual(34, def.ThumbnailHeightPx);
            Assert.AreEqual(4, def.PolaroidBorderPx);
            Assert.AreEqual(12, def.PolaroidBottomBorderPx);
            Assert.AreEqual(60, def.AlbumCapacity);
            Assert.AreEqual(3266, PhotoThumbnail.ByteLength(48, 34));
            Assert.AreEqual(4356, def.ThumbnailEncodedLength);
            Assert.AreEqual(261360, def.AlbumCapacity * def.ThumbnailEncodedLength, "about 255 KiB a player");
        }

        [Test]
        public void ADefWithNegativeAlbumSpacing_IsRefused()
        {
            PhotoCameraDef def = Shipped();
            def.PolaroidMinGapPx = -1;
            StringAssert.Contains("spacing", def.ValidationError());
            def = Shipped();
            def.AlbumAirRows = -1;
            StringAssert.Contains("spacing", def.ValidationError());
            def = Shipped();
            def.AlbumCaptionWhenFormat = " ";
            StringAssert.Contains("empty", def.ValidationError());
        }

        [Test]
        public void TheAlbumPage_HandsEachPictureItsThumbnail_TheDefsPolaroid_AndAShortCaption()
        {
            PhotoCameraDef def = PhotoCameraDef.Load();
            SaveData save = SaveMigration.NewGame();
            string stored = PhotoThumbnail.Encode(Pattern(def.ThumbnailWidthPx, def.ThumbnailHeightPx),
                                                  def.ThumbnailWidthPx, def.ThumbnailHeightPx);
            Add(save, Local, 120, stored);
            Add(save, Local, 120, null);

            List<NotebookPhotoEntry> entries = NotebookPhotoPage.Entries(save, Local, def, 1200f);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("", entries[0].Thumbnail, "newest first: the one with no picture");
            Assert.AreEqual(stored, entries[1].Thumbnail);
            NotebookPolaroidStyle s = entries[1].Style;
            Assert.AreEqual(def.PolaroidWidthPx, s.WidthPx);
            Assert.AreEqual(def.PolaroidHeightPx, s.HeightPx);
            Assert.AreEqual(def.PolaroidBorderPx, s.BorderPx);
            Assert.AreEqual(def.PolaroidBottomBorderPx, s.BottomBorderPx);
            Assert.AreEqual(def.PolaroidMinGapPx, s.MinGapPx);
            Assert.AreEqual(def.AlbumAirRows, s.AirRows);
            Assert.IsTrue(entries[0].HasPolaroid, "an empty card is still drawn");
            Assert.AreEqual("Grey seal", entries[1].Caption);
            Assert.AreEqual("Day 6, St Peters", entries[1].CaptionWhen, "6000 s at 1200 s a day is day 6");
        }

        [Test]
        public void TheCaption_NamesTheKindItHoldsMostOf_CountedWithTheFirstWinningATie()
        {
            PhotoCameraDef def = PhotoCameraDef.Load();
            var p = new PhotoRecordDto
            {
                Score = 80,
                Subjects = new List<PhotoSubjectDto>
                {
                    new PhotoSubjectDto { SubjectId = "wildlife.other", Count = 1 },
                    new PhotoSubjectDto { SubjectId = Seal, Count = 4 },
                },
            };
            Assert.AreEqual("4 grey seals", NotebookPhotoPage.Caption(def, p));

            p.Subjects[1].Count = 1;
            Assert.AreEqual("Wildlife.other", NotebookPhotoPage.Caption(def, p), "a tie: the first listed");

            p.Subjects.Clear();
            Assert.AreEqual("A fine picture", NotebookPhotoPage.Caption(def, p), "naming nothing: its quality");
        }

        [Test]
        public void TheGrid_FitsAsManyStraightCardsAsTheLaneHoldsWithAir_AtEveryBookWidth()
        {
            NotebookPolaroidStyle s = NotebookPhotoPage.Style(PhotoCameraDef.Load());
            for (int cols = NotebookKit.MinCols; cols <= NotebookKit.MaxCols; cols++)
            {
                NotebookPhotoGrid g = NotebookPhotoGrid.For(s, cols);
                int lane = NotebookKit.CellWidth * NotebookKit.TitleColsFor(cols) - 1;
                Assert.GreaterOrEqual(g.Across, 1, "cols " + cols);
                Assert.LessOrEqual(g.Across * g.SlotWidthPx, lane, "the slots stay in the lane, cols " + cols);
                Assert.GreaterOrEqual(g.SlotWidthPx, s.WidthPx, "a card fits its slot, cols " + cols);
                if (lane >= s.WidthPx + s.MinGapPx)
                    Assert.LessOrEqual(g.Across * (s.WidthPx + s.MinGapPx), lane, "air beside each card, cols " + cols);
                Assert.Greater((g.Across + 1) * (s.WidthPx + s.MinGapPx), lane, "no room left for one more, cols " + cols);
                Assert.LessOrEqual(g.SlotCols * NotebookKit.CellWidth - 1, g.SlotWidthPx, "cols " + cols);
            }

            NotebookPhotoGrid at31 = NotebookPhotoGrid.For(s, 31);
            Assert.AreEqual(2, at31.Across, "56 px cards with 12 px air: two across a 164 px lane");
            Assert.AreEqual(82, at31.SlotWidthPx);
            Assert.AreEqual(16, at31.SlotCols);
        }

        [Test]
        public void WrapWords_NeverBreaksAWord_AndKeepsEveryWordInOrder()
        {
            var lines = new List<string>();
            NotebookPhotoGrid.WrapWords("Picture 4  4 grey seals unbelievablylongword x", 8, lines);
            CollectionAssert.AreEqual(new[] { "Picture", "4 4 grey", "seals", "unbelievablylongword", "x" }, lines);
            var words = new List<string>();
            foreach (string l in lines) words.AddRange(l.Split(' '));
            CollectionAssert.AreEqual("Picture 4 4 grey seals unbelievablylongword x".Split(' '), words);

            lines.Clear();
            NotebookPhotoGrid.WrapWords("Day 12, St Peters", 16, lines);
            CollectionAssert.AreEqual(new[] { "Day 12,", "St Peters" }, lines, "a line breaks after the comma first");
            lines.Clear();
            NotebookPhotoGrid.WrapWords("Day 1, St Peters", 16, lines);
            CollectionAssert.AreEqual(new[] { "Day 1, St Peters" }, lines);
            lines.Clear();
            NotebookPhotoGrid.WrapWords("Day 12, St Peters", 17, lines);
            CollectionAssert.AreEqual(new[] { "Day 12, St Peters" }, lines);

            lines.Clear();
            NotebookPhotoGrid.WrapWords("   ", 8, lines);
            NotebookPhotoGrid.WrapWords(null, 8, lines);
            Assert.AreEqual(0, lines.Count);
        }

        [Test]
        public void TheShippedWords_FitTheNarrowestSlot_WordByWord()
        {
            PhotoCameraDef def = PhotoCameraDef.Load();
            NotebookPhotoGrid g = NotebookPhotoGrid.For(NotebookPhotoPage.Style(def), NotebookKit.MinCols);
            var texts = new List<string>();
            foreach (PhotoSubjectEntry s in def.Subjects)
            {
                texts.Add(def.CountedName(s.SubjectId, 1));
                texts.Add(def.CountedName(s.SubjectId, 99));
            }
            foreach (PhotoPlaceEntry place in def.Places)
                texts.Add(NotebookPhotoPage.CaptionWhen(def, 998d * 1200d, place.RegionId, 1200f));
            foreach (string word in new[] { def.GoodWord, def.FairWord, def.PoorWord }) texts.Add(word);

            foreach (string text in texts)
                foreach (string word in text.Split(' '))
                    Assert.LessOrEqual(word.Length, g.SlotCols, "\"" + word + "\" of \"" + text + "\"");
        }

        [Test]
        public void AlbumBlocks_AreRowsOfCards_EachTallEnoughForTheCardsAndTheirCaptions()
        {
            PhotoCameraDef def = PhotoCameraDef.Load();
            NotebookPolaroidStyle s = NotebookPhotoPage.Style(def);
            var photos = new[] { Entry(3, s), Entry(2, s), Entry(1, s) };
            List<NotebookBlock> blocks = NotebookContent.PhotoBlocks(photos, 31);

            Assert.AreEqual(2, blocks.Count, "two across: a row of two, then one");
            Assert.AreEqual(5, s.CardRows, "a 50 px card over a 10 px ruling");
            Assert.AreEqual(s.CardRows + 3 + s.AirRows, blocks[0].Height,
                            "card, \"4 grey seals\", \"Day 12,\" over \"St Peters\", air");
            Assert.AreEqual(NotebookContent.PhotoSourcePrefix + "3", blocks[0].SourceId);
            IReadOnlyList<NotebookRow> rows = blocks[0].Rows;
            Assert.IsFalse(rows[0].Continuation, "the cards are drawn once, from the first line");
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual("photo_grid.3_2", rows[i].IconId, "row " + i);
                Assert.AreEqual("", rows[i].Text, "the captions are lettered in their slots, row " + i);
                if (i > 0) Assert.IsTrue(rows[i].Continuation, "row " + i);
            }
            CollectionAssert.AreEqual(new[] { 3, 2 }, NotebookContent.PhotoGridNumbers(rows[0].IconId));
            CollectionAssert.AreEqual(new[] { 1 }, NotebookContent.PhotoGridNumbers(blocks[1].Rows[0].IconId));
            Assert.AreEqual(0, NotebookContent.PhotoGridNumbers("photo.3").Count);
        }

        [Test]
        public void APageTurn_MovesWholeRowsOfCards()
        {
            NotebookPolaroidStyle s = NotebookPhotoPage.Style(PhotoCameraDef.Load());
            var photos = new List<NotebookPhotoEntry>();
            for (int n = 9; n >= 1; n--) photos.Add(Entry(n, s));
            List<NotebookBlock> blocks = NotebookContent.PhotoBlocks(photos, 31);
            Assert.AreEqual(5, blocks.Count, "nine pictures, two across");

            foreach (int lines in new[] { 12, 18, 22 })
            {
                int placed = 0;
                foreach (NotebookSpread spread in NotebookLayout.Paginate(blocks, 31, lines))
                    foreach (NotebookLeaf leaf in new[] { spread.Left, spread.Right })
                        foreach (NotebookPlacement p in leaf.Placements)
                        {
                            Assert.AreEqual(0, p.FromRow, "a row of cards starts on its leaf, lines " + lines);
                            Assert.AreEqual(p.Block.Height, p.RowCount, "and ends on it, lines " + lines);
                            placed++;
                        }
                Assert.AreEqual(blocks.Count, placed, "every row of cards is drawn, lines " + lines);
            }
        }

        [Test]
        public void AnEntryWithNoPolaroid_IsAPlainSection()
        {
            var entry = new NotebookPhotoEntry(3, "Picture 3", new[] { "A grey seal, resting" });
            List<NotebookBlock> blocks = NotebookContent.PhotoBlocks(new[] { entry }, 30);
            Assert.IsFalse(entry.HasPolaroid);
            foreach (NotebookRow row in blocks[0].Rows) Assert.IsFalse(row.HasIcon);
        }

        // ---- helpers ------------------------------------------------------------------------------------

        static NotebookPhotoEntry Entry(int number, NotebookPolaroidStyle style) =>
            new NotebookPhotoEntry(number, "Picture " + number, new[] { "Grey seal, resting" }, "4 grey seals",
                                   "Day 12, St Peters", "", style);

        PhotoCameraDef Shipped()
        {
            PhotoCameraDef def = Object.Instantiate(PhotoCameraDef.Load());
            _made.Add(def);
            return def;
        }

        /// <summary>A deterministic picture: every pixel a different colour.</summary>
        static Color32[] Pattern(int w, int h)
        {
            var p = new Color32[w * h];
            for (int i = 0; i < p.Length; i++)
                p[i] = new Color32((byte)(i * 37 % 256), (byte)(i * 91 % 256), (byte)(i * 13 % 256), 255);
            return p;
        }

        static PhotoRecordDto Add(SaveData save, string key, int capacity, string thumbnail) =>
            PhotoAlbumRows.Add(save, key, 6000d, Region,
                               new[] { new PhotoSubjectResult { SubjectId = Seal, Count = 1, Pose = WildlifePose.Resting, Score = 50 } },
                               1, capacity, thumbnail);

        static string Row(int number, string thumbnail) =>
            "{\"PlayerKey\":\"player.local\",\"Number\":" + number + ",\"GameTimeSeconds\":1.0," +
            "\"RegionId\":\"region.st_peters\",\"Score\":50,\"Subjects\":[{\"SubjectId\":\"wildlife.grey_seal\"," +
            "\"Count\":1,\"Pose\":0,\"Score\":50}],\"Thumbnail\":\"" + thumbnail + "\"}";
    }
}
