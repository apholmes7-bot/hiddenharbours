using HiddenHarbours.Core;
using HiddenHarbours.CoopSpike;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.CoopSpike
{
    public class CoopBoatPresentationTests
    {
        [Test]
        public void WireToCorePreservesEveryPresentationField()
        {
            var state = new CoopBoatState
            {
                HullId = "boat.dory", Recipe = "recipe",
                SkipperLocal = new Vector3(1, 2, 3), SkipperVisualOffset = new Vector3(4, 5, 6),
                SkipperStandRigMetres = new Vector3(7, 8, 9), SkipperHeading = 123,
                SkipperStance = CharacterStance.Helm
            };
            var pose = state.ToPose();
            Assert.That(pose.HullId, Is.EqualTo("boat.dory"));
            Assert.That(pose.Recipe, Is.EqualTo("recipe"));
            Assert.That(pose.SkipperLocal, Is.EqualTo(state.SkipperLocal));
            Assert.That(pose.SkipperVisualOffset, Is.EqualTo(state.SkipperVisualOffset));
            Assert.That(pose.SkipperStandRigMetres, Is.EqualTo(state.SkipperStandRigMetres));
            Assert.That(pose.SkipperHeading, Is.EqualTo(state.SkipperHeading));
            Assert.That(pose.SkipperStance, Is.EqualTo(state.SkipperStance));
        }

        [Test]
        public void CoreToWirePreservesEveryPresentationField()
        {
            var pose = new CoopBoatPose
            {
                HullId = "boat.dory", Recipe = "recipe",
                SkipperLocal = new Vector3(1, 2, 3), SkipperVisualOffset = new Vector3(4, 5, 6),
                SkipperStandRigMetres = new Vector3(7, 8, 9), SkipperHeading = 123,
                SkipperStance = CharacterStance.Helm
            };
            var state = CoopBoatState.FromPose(pose);
            Assert.That(state.HullId.ToString(), Is.EqualTo(pose.HullId));
            Assert.That(state.Recipe.ToString(), Is.EqualTo(pose.Recipe));
            Assert.That(state.SkipperLocal, Is.EqualTo(pose.SkipperLocal));
            Assert.That(state.SkipperVisualOffset, Is.EqualTo(pose.SkipperVisualOffset));
            Assert.That(state.SkipperStandRigMetres, Is.EqualTo(pose.SkipperStandRigMetres));
            Assert.That(state.SkipperHeading, Is.EqualTo(pose.SkipperHeading));
            Assert.That(state.SkipperStance, Is.EqualTo(pose.SkipperStance));
        }

        [Test]
        public void UnboundIdentityStaysEmptyOnTheWire()
        {
            var state = CoopBoatState.FromPose(default);
            Assert.That(state.HullId.ToString(), Is.Empty);
            Assert.That(state.Recipe.ToString(), Is.Empty);
        }
    }
}
