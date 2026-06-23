using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The Core id → icon seam the UI reads (IconRegistry) and the authored IconLibrary that publishes
    /// into it. These pin the contract the sell screen / catch card / HUD rely on: an icon resolves by
    /// the same stable content id the UI already knows (a species id, gear/boat id, ui.* glyph key), the
    /// registry is forgiving and first-wins, and an empty registry returns null so consumers fall back to
    /// text-only — never throw. Mirrors RegionDisplayNames' lookup contract.
    /// </summary>
    public class IconRegistryTests
    {
        private Sprite _a;
        private Sprite _b;

        [SetUp]
        public void SetUp()
        {
            IconRegistry.Reset();
            _a = MakeSprite();
            _b = MakeSprite();
        }

        [TearDown]
        public void TearDown()
        {
            IconRegistry.Reset();
            if (_a != null) Object.DestroyImmediate(_a.texture);
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b.texture);
            if (_b != null) Object.DestroyImmediate(_b);
        }

        [Test]
        public void RegisterThenGet_ReturnsTheIcon()
        {
            IconRegistry.Register("fish.atlantic_cod", _a);
            Assert.AreSame(_a, IconRegistry.Get("fish.atlantic_cod"));
            Assert.IsTrue(IconRegistry.Has("fish.atlantic_cod"));
        }

        [Test]
        public void Get_IsCaseInsensitive()
        {
            IconRegistry.Register("gear.rod", _a);
            Assert.AreSame(_a, IconRegistry.Get("GEAR.ROD"));
        }

        [Test]
        public void Get_UnknownId_ReturnsNull()
        {
            Assert.IsNull(IconRegistry.Get("fish.nope"));
            Assert.IsFalse(IconRegistry.Has("fish.nope"));
        }

        [Test]
        public void FirstRegistrationWins()
        {
            IconRegistry.Register("boat.punt", _a);
            IconRegistry.Register("boat.punt", _b);   // ignored — first wins
            Assert.AreSame(_a, IconRegistry.Get("boat.punt"));
        }

        [Test]
        public void BlankIdOrNullIcon_AreIgnored()
        {
            IconRegistry.Register("", _a);
            IconRegistry.Register("   ", _a);
            IconRegistry.Register(null, _a);
            IconRegistry.Register("fish.haddock", null);
            Assert.AreEqual(0, IconRegistry.Count);
            Assert.IsNull(IconRegistry.Get("fish.haddock"));
        }

        [Test]
        public void Get_NullOrBlankKey_ReturnsNullNeverThrows()
        {
            Assert.IsNull(IconRegistry.Get(null));
            Assert.IsNull(IconRegistry.Get(""));
            Assert.IsFalse(IconRegistry.Has(null));
        }

        [Test]
        public void IconLibrary_RegisterAll_PublishesEveryNonNullEntry()
        {
            var lib = ScriptableObject.CreateInstance<IconLibrary>();
            lib.Entries = new[]
            {
                new IconLibrary.Entry { Id = "fish.atlantic_cod", Icon = _a },
                new IconLibrary.Entry { Id = "ui.coin",           Icon = _b },
                new IconLibrary.Entry { Id = "",                  Icon = _a },  // blank id → skipped
                new IconLibrary.Entry { Id = "fish.no_icon",      Icon = null }, // null icon → skipped
            };

            lib.RegisterAll();

            Assert.AreSame(_a, IconRegistry.Get("fish.atlantic_cod"));
            Assert.AreSame(_b, IconRegistry.Get("ui.coin"));
            Assert.IsNull(IconRegistry.Get("fish.no_icon"));
            Assert.AreEqual(2, IconRegistry.Count);

            Object.DestroyImmediate(lib);
        }

        // A throwaway 4×4 sprite — enough to be a distinct, non-null Sprite reference for the lookup.
        private static Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
