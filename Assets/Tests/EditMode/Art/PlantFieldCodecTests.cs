using System;
using HiddenHarbours.Art;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantFieldCodecTests
    {
        [Test] public void EmptyAndLongRunsRoundTrip()
        {
            foreach (int n in new[] { 0, 1, 65535, 65536, 100000 })
            {
                var a = new byte[n]; for (int i = n / 2; i < n; i++) a[i] = PlantFieldScatter.Pack(48, 1);
                Assert.That(PlantFieldCodec.Decode(PlantFieldCodec.Encode(a), n), Is.EqualTo(a));
            }
        }
        [Test] public void HeaderAndRunsArePinned()
        {
            Assert.That(Convert.FromBase64String(PlantFieldCodec.Encode(new byte[] { 1, 1, 0 })),
                Is.EqualTo(new byte[] { 80,76,70,1,3,0,0,0,2,0,1,1,0,0 }));
        }
        [Test] public void TruncationOverflowAndReservedCellsFail()
        {
            var bytes = Convert.FromBase64String(PlantFieldCodec.Encode(new byte[] { 1, 1 }));
            bytes[8] = 3; Assert.Throws<FormatException>(() => PlantFieldCodec.Decode(Convert.ToBase64String(bytes), 2));
            bytes[8] = 0; Assert.Throws<FormatException>(() => PlantFieldCodec.Decode(Convert.ToBase64String(bytes), 2));
            bytes[8] = 2; bytes[10] = 192; Assert.Throws<FormatException>(() => PlantFieldCodec.Decode(Convert.ToBase64String(bytes), 2));
            Assert.Throws<FormatException>(() => PlantFieldCodec.Decode("UExGAQ==", 1));
            Assert.Throws<FormatException>(() => PlantFieldCodec.Decode(PlantFieldCodec.Encode(new byte[1]), 2));
        }
        [Test] public void HostileDeclaredSizeDoesNotAllocate()
        {
            Assert.Throws<FormatException>(() => PlantFieldCodec.Decode("UExGAf////8=", 100));
        }
    }
}
