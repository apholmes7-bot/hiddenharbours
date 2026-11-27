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
        public void TheAlbumPage_HandsEachPictureItsThumbnailAndTheDefsPolaroid()
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
            Assert.AreEqual(def.PolaroidWidthPx, entries[1].PolaroidWidthPx);
            Assert.AreEqual(def.PolaroidHeightPx, entries[1].PolaroidHeightPx);
            Assert.AreEqual(def.PolaroidBorderPx, entries[1].PolaroidBorderPx);
            Assert.AreEqual(def.PolaroidBottomBorderPx, entries[1].PolaroidBottomBorderPx);
            Assert.IsTrue(entries[0].HasPolaroid, "an empty card is still drawn");
        }

        [Test]
        public void APolaroidBlock_IsPicturedOnEveryRow_AndTallEnoughForTheCard()
        {
            var entry = new NotebookPhotoEntry(7, "Picture 7", new[] { "Three grey seals, resting", "A fine picture" },
                                               "", 28, 25, 2, 6, new Color32(244, 241, 232, 255));
            List<NotebookBlock> blocks = NotebookContent.PhotoBlocks(new[] { entry }, 30);

            Assert.AreEqual(1, blocks.Count);
            IReadOnlyList<NotebookRow> rows = blocks[0].Rows;
            Assert.GreaterOrEqual(rows.Count, entry.PolaroidRows);
            Assert.AreEqual(3, entry.PolaroidRows, "25 px over a 10 px ruling");
            Assert.AreEqual(7, entry.PolaroidLaneCells, "28 px is six 5 px cells, then one of air");
            Assert.IsFalse(rows[0].Continuation, "the card is drawn once, beside the first line");
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(NotebookContent.PhotoSourcePrefix + "7", rows[i].IconId, "row " + i);
                if (i > 0) Assert.IsTrue(rows[i].Continuation, "row " + i);
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
