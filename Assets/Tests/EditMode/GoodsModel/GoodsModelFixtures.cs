using HiddenHarbours.Tools.GoodsModel;

namespace HiddenHarbours.Tests.GoodsModel
{
    internal static class GoodsModelFixtures
    {
        public static GoodsHandLimits Limits() => new GoodsHandLimits
        {
            OneHandEnvelope = new GoodsBox(620, 500, 500), TwoHandEnvelope = new GoodsBox(1150, 600, 600),
            OneHandMassG = 8000, TwoHandMassG = 16000, MaxCatchMassG = 12000
        };

        public static GoodsContainerSpec Pan() => new GoodsContainerSpec
        {
            Id = "container.test_pan", Outside = new GoodsBox(650, 450, 160), Inside = new GoodsBox(600, 400, 120),
            Opening = new GoodsBox(600, 400, 120), UsableVolumeMl = 28800, CountLimit = 2,
            AcceptedProfiles = new[] { "goods.raw_fish" }, EmptyMassG = 1000, MaxGrossMassG = 16000,
            RequiredHands = 2, MaxHandGrossMassG = 16000, StackGroup = "pan", MaxStackCount = 3,
            MaxStackHeightMm = 480, MaxSupportedMassG = 32000,
            Slots = new[]
            {
                new GoodsSlot { Size = new GoodsBox(300, 400, 120) },
                new GoodsSlot { Xmm = 300, Size = new GoodsBox(300, 400, 120) }
            }
        };

        public static GoodsPiece Fish(string id = "fish_a", int length = 250, long mass = 1000) =>
            new GoodsPiece(id, "goods.raw_fish", GoodsKind.Catch, new GoodsBox(length, 100, 100), mass, false);

        public static GoodsPiece Shell(GoodsContainerSpec spec, string id = "pan") =>
            new GoodsPiece(id, "goods.container", GoodsKind.Container, spec.Outside, spec.EmptyMassG, false);

        public static GoodsStackMember Member(string id, long contentsMass = 1000, GoodsContainerSpec spec = null, bool closed = false) =>
            new GoodsStackMember(id, spec ?? Pan(), contentsMass, closed);
    }
}
