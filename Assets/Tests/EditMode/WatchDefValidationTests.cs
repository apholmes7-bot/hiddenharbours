using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WatchDefValidationTests
    {
        private WatchDef _def;

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<WatchDef>();
            _def.Id = "watch.validation_fixture";
            _def.DisplayName = "Validation fixture";
            _def.Face = WatchFaceKind.HarbourDigital;
            _def.Readouts = WatchReadouts.Time;
            _def.StartsOwned = true;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        [Test]
        public void FreeStartingEquipmentNeedsNoSeller()
        {
            Assert.That(_def.TryValidate(out string error), Is.True, error);
        }

        [TestCase("empty id")]
        [TestCase("wrong prefix")]
        [TestCase("invalid id")]
        [TestCase("missing name")]
        [TestCase("unknown face")]
        [TestCase("missing time")]
        [TestCase("unknown readout")]
        [TestCase("negative tier")]
        [TestCase("negative price")]
        [TestCase("priced without seller")]
        [TestCase("unobtainable")]
        [TestCase("self alias")]
        [TestCase("blank alias")]
        public void InvalidWatchDataIsRejected(string invalid)
        {
            switch (invalid)
            {
                case "empty id": _def.Id = null; break;
                case "wrong prefix": _def.Id = "gear.fixture"; break;
                case "invalid id": _def.Id = "watch.Bad Name"; break;
                case "missing name": _def.DisplayName = " "; break;
                case "unknown face": _def.Face = (WatchFaceKind)99; break;
                case "missing time": _def.Readouts = WatchReadouts.Date; break;
                case "unknown readout": _def.Readouts |= (WatchReadouts)128; break;
                case "negative tier": _def.Tier = -1; break;
                case "negative price": _def.Price = -1; break;
                case "priced without seller": _def.Price = 1; break;
                case "unobtainable": _def.StartsOwned = false; break;
                case "self alias": _def.LegacyGearId = _def.Id; break;
                case "blank alias": _def.LegacyGearId = " "; break;
                default: Assert.Fail("Unknown invalid fixture: " + invalid); break;
            }
            Assert.That(_def.TryValidate(out string error), Is.False, invalid);
            Assert.That(error, Is.Not.Empty, invalid);
        }
    }
}
