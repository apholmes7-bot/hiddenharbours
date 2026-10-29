using HiddenHarbours.Art;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// NMC H4: the road floor's PNG decoder, which reads each streamed map on a worker. Every row filter decodes to the
    /// bytes written, rows come out bottom first (Unity's order, which the shader's RF4Texel flips back), and a file the
    /// bake would not write, or a damaged one, is refused with a reason rather than streamed. Pure: no engine call.
    /// </summary>
    public class RoadFloorPngTests
    {
        const int Side = RoadFloorMaps.ChunkTexels, Stride = Side * 4;

        [TestCase(0, TestName = "Filter0_None")]
        [TestCase(1, TestName = "Filter1_Sub")]
        [TestCase(2, TestName = "Filter2_Up")]
        [TestCase(3, TestName = "Filter3_Average")]
        [TestCase(4, TestName = "Filter4_Paeth")]
        [TestCase(-1, TestName = "FiltersMixedByRow")]
        public void EachRowFilter_DecodesToTheBytesWritten_BottomRowFirst(int filter)
        {
            byte[] top = RoadFloorTestPngs.Pattern(Side, 3 + filter);
            byte[] file = RoadFloorTestPngs.Encode(top, Side, r => (byte)(filter >= 0 ? filter : r % 5));
            byte[] rgba = new byte[RoadFloorMaps.MapBytes], scratch = new byte[RoadFloorPng.ScratchBytes];

            Assert.IsNull(RoadFloorPng.Decode(file, rgba, scratch), "a well-formed map was refused");
            for (int r = 0; r < Side; r++)
            for (int i = 0; i < Stride; i++)
                if (rgba[(Side - 1 - r) * Stride + i] != top[r * Stride + i])
                    Assert.Fail($"the file's row {r} byte {i} came out as {rgba[(Side - 1 - r) * Stride + i]}, " +
                                $"not {top[r * Stride + i]} at row {Side - 1 - r} (rows run bottom first)");
        }

        [Test]
        public void ImageDataInTwoChunks_IsJoined()
        {
            byte[] top = RoadFloorTestPngs.Pattern(Side, 11);
            byte[] file = RoadFloorTestPngs.Encode(top, Side, r => 4, splitIdat: true);
            byte[] rgba = new byte[RoadFloorMaps.MapBytes];
            Assert.IsNull(RoadFloorPng.Decode(file, rgba, new byte[RoadFloorPng.ScratchBytes]), "two IDATs were refused");
            Assert.AreEqual(top[0], rgba[(Side - 1) * Stride], "the first byte of the file's top row is not at the bottom row");
            Assert.AreEqual(top[Stride * Side - 1], rgba[Stride - 1], "the last byte of the file's bottom row is not at row 0");
        }

        [Test]
        public void AFileTheBakeWouldNotWrite_OrADamagedOne_IsRefused()
        {
            byte[] top = RoadFloorTestPngs.Pattern(Side, 5), rgba = new byte[RoadFloorMaps.MapBytes];
            byte[] scratch = new byte[RoadFloorPng.ScratchBytes];
            byte[] good = RoadFloorTestPngs.Encode(top, Side, r => 1);

            Assert.IsNotNull(RoadFloorPng.Decode(null, rgba, scratch), "no file was decoded");
            Assert.IsNotNull(RoadFloorPng.Decode(new byte[] { 1, 2, 3 }, rgba, scratch), "three bytes were decoded");

            byte[] badSignature = (byte[])good.Clone();
            badSignature[1] = (byte)'Q';
            Assert.IsNotNull(RoadFloorPng.Decode(badSignature, rgba, scratch), "a file that is not a PNG was decoded");

            byte[] damaged = (byte[])good.Clone();
            damaged[damaged.Length / 2] ^= 0x40;   // inside the image data: its chunk's CRC no longer matches
            StringAssert.Contains("CRC", RoadFloorPng.Decode(damaged, rgba, scratch), "a damaged chunk was decoded");

            byte[] cut = new byte[good.Length - 12];   // no IEND
            System.Array.Copy(good, cut, cut.Length);
            StringAssert.Contains("IEND", RoadFloorPng.Decode(cut, rgba, scratch), "a file cut short was decoded");

            byte[] small = RoadFloorTestPngs.Encode(RoadFloorTestPngs.Pattern(128, 1), 128, r => 0);
            StringAssert.Contains("square", RoadFloorPng.Decode(small, rgba, scratch), "a 128 px map was decoded into a 256 px slot");

            var rgbTop = new byte[Side * Side * 3];
            byte[] rgb = RoadFloorTestPngs.Encode(rgbTop, Side, r => 0, colourType: 2);
            StringAssert.Contains("RGBA", RoadFloorPng.Decode(rgb, rgba, scratch), "an RGB map was decoded as RGBA");

            Assert.IsNotNull(RoadFloorPng.Decode(good, new byte[16], scratch), "a map was decoded into a buffer too small");
            Assert.IsNotNull(RoadFloorPng.Decode(good, rgba, new byte[16]), "a map was decoded with a scratch too small");
            Assert.IsNull(RoadFloorPng.Decode(good, rgba, scratch), "premise: the undamaged file decodes");
        }

        [Test]
        public void ABadAdler_IsRefused_EvenWithItsChunkCrcMadeGood()
        {
            byte[] good = RoadFloorTestPngs.Encode(RoadFloorTestPngs.Pattern(Side, 9), Side, r => 2);
            // The IDAT is the second chunk: signature 8, IHDR 25. Its data ends with the Adler-32; change it, then make
            // the chunk's CRC good again, so only the stream's own check can catch it.
            int at = 8 + 25, len = (int)((uint)good[at] << 24 | (uint)good[at + 1] << 16 | (uint)good[at + 2] << 8 | good[at + 3]);
            byte[] file = (byte[])good.Clone();
            file[at + 8 + len - 1] ^= 0x01;
            var body = new byte[4 + len];
            System.Array.Copy(file, at + 4, body, 0, body.Length);
            RoadFloorTestPngs.PutU32(file, at + 8 + len, RoadFloorTestPngs.Crc(body));

            StringAssert.Contains("Adler", RoadFloorPng.Decode(file, new byte[RoadFloorMaps.MapBytes], new byte[RoadFloorPng.ScratchBytes]),
                "image data whose Adler-32 is wrong was decoded");
        }
    }
}
