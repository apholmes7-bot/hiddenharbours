using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Core region-display-name seam (ADR 0009): the registry the world fills and the UI reads so
    /// the crossing fade card titles "Coddle Cove"/"Port Greywick" without the UI referencing World
    /// (closes the ui-ux #54 follow-up). Pure static lookup — engine-light and deterministic.
    /// </summary>
    public class RegionDisplayNamesTests
    {
        [SetUp]
        public void Clear() => RegionDisplayNames.Reset(); // isolate each test (static registry)

        [TearDown]
        public void ClearAfter() => RegionDisplayNames.Reset(); // don't leak into other suites

        [Test]
        public void Resolve_ReturnsRegisteredName_OverTheFallback()
        {
            RegionDisplayNames.Register("Greywick", "Port Greywick");
            Assert.AreEqual("Port Greywick", RegionDisplayNames.Resolve("Greywick", fallback: "Greywick"));
        }

        [Test]
        public void Resolve_FallsBackWhenUnregistered()
        {
            // The UI passes its own derived title as the fallback; an unregistered key yields it unchanged.
            Assert.AreEqual("Greybox", RegionDisplayNames.Resolve("Greybox", fallback: "Greybox"));
        }

        [Test]
        public void Get_IsNullWhenUnregistered_SoCallersCanFallBack()
        {
            Assert.IsNull(RegionDisplayNames.Get("Nowhere"));
            Assert.IsFalse(RegionDisplayNames.Has("Nowhere"));
        }

        [Test]
        public void Register_FirstRegistrationWins()
        {
            RegionDisplayNames.Register("CoddleCove", "Coddle Cove");
            RegionDisplayNames.Register("CoddleCove", "Something Else");
            Assert.AreEqual("Coddle Cove", RegionDisplayNames.Get("CoddleCove"), "first non-blank registration wins");
        }

        [Test]
        public void Lookup_IsCaseInsensitive()
        {
            RegionDisplayNames.Register("Greywick", "Port Greywick");
            Assert.AreEqual("Port Greywick", RegionDisplayNames.Get("greywick"), "scene names resolve regardless of case");
            Assert.IsTrue(RegionDisplayNames.Has("GREYWICK"));
        }

        [Test]
        public void Register_IgnoresBlankKeyOrName()
        {
            RegionDisplayNames.Register("", "Port Greywick");
            RegionDisplayNames.Register("Greywick", "   ");
            RegionDisplayNames.Register(null, "x");
            Assert.IsFalse(RegionDisplayNames.Has(""));
            Assert.IsNull(RegionDisplayNames.Get("Greywick"), "a blank display name does not register");
        }

        [Test]
        public void RegisterById_AndByScene_BothResolve()
        {
            // The world may register under the stable id and/or the scene name; either key resolves.
            RegionDisplayNames.Register("region.port_greywick", "Port Greywick");
            RegionDisplayNames.Register("Greywick", "Port Greywick");
            Assert.AreEqual("Port Greywick", RegionDisplayNames.Get("region.port_greywick"));
            Assert.AreEqual("Port Greywick", RegionDisplayNames.Get("Greywick"));
        }

        [Test]
        public void Reset_ClearsTheRegistry()
        {
            RegionDisplayNames.Register("Greywick", "Port Greywick");
            RegionDisplayNames.Reset();
            Assert.IsFalse(RegionDisplayNames.Has("Greywick"));
        }
    }
}
