using System;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Owner ruling 2026-11-20: no hull flies the little red wind pennant, sail hulls included. The
    /// presenter that drew it, its masthead data and its tunables are gone; this guard fails if any of
    /// them comes back. The wind is read on the helm instruments and on the sea itself.
    /// </summary>
    public class NoBoatFliesAPennantTests
    {
        [Test]
        public void NoPennantPresenterTypeExists()
        {
            var found = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("HiddenHarbours", StringComparison.Ordinal))
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .Where(t => t.Name == "MastheadTelltale")
                .Select(t => t.FullName)
                .ToArray();
            Assert.That(found, Is.Empty, "no boat flies a pennant, so nothing may draw one");
        }

        [Test]
        public void HullDataCarriesNoPennantMasthead()
        {
            Assert.That(typeof(BoatHullDef).GetField("MastheadRigMetres"), Is.Null,
                "the masthead point existed only to fly the pennant");
        }

        [Test]
        public void GameConfigCarriesNoPennantTunables()
        {
            Assert.That(typeof(HiddenHarbours.Core.GameConfig).GetField("Telltale"), Is.Null,
                "the pennant's tunables went with it");
        }
    }
}
