using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Helm instruments plan, slice 1 (the owner's 2026-11-13 ruling): no boat with a console is left
    /// without a heading read. Asserted on the authored console assets, and on every one of them, so a
    /// console added later with no compass fails here by name.
    /// </summary>
    public class EveryConsoleShipsAHeadingTests
    {
        private const string HelmsFolder = "Assets/_Project/Data/Boats/Helms";

        private static List<HelmConsoleDef> Consoles()
        {
            var found = new List<HelmConsoleDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:HelmConsoleDef", new[] { HelmsFolder }))
            {
                var console = AssetDatabase.LoadAssetAtPath<HelmConsoleDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (console != null) found.Add(console);
            }
            return found;
        }

        [Test]
        public void EveryConsole_FitsACompassOutOfTheBox_AndOneItCanMount()
        {
            List<HelmConsoleDef> consoles = Consoles();
            Assert.That(consoles.Count, Is.GreaterThan(0), "premise: the shipped consoles are found");

            var owned = new List<string>();          // a fresh boat: nothing bought
            foreach (HelmConsoleDef console in consoles)
            {
                Assert.That(console.DefaultCompass, Is.Not.EqualTo(CompassMount.None),
                            $"{console.Id} ships with no compass, so her helm has no heading read");
                Assert.IsTrue(console.SupportsCompassMount(console.DefaultCompass),
                              $"{console.Id} ships a {console.DefaultCompass} compass her dash cannot mount");
                Assert.That(BoatEquipment.EffectiveFit(console, owned).Compass,
                            Is.EqualTo(console.DefaultCompass),
                            $"{console.Id}: the fit a fresh boat resolves carries the compass she ships with");
            }
        }

        [TestCase("helm.novi")]
        [TestCase("helm.cape_islander")]
        public void TheWheelhouseConsoles_ShipTheFlushCompass(string id)
        {
            HelmConsoleDef console = Consoles().Find(c => c.Id == id);
            Assert.IsNotNull(console, $"the shipped console {id} must exist");
            Assert.That(console.DefaultCompass, Is.EqualTo(CompassMount.Flush),
                        "lobster and cape boats ship with the flush compass sunk in the dash");
        }
    }
}
