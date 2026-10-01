using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE LOOK SEAM</b> (<see cref="CharacterLookTargets"/>): by default a figure that asks looks at
    /// the published player, and at nothing with none published or the published one destroyed; a
    /// lane that installs its own source is asked with the figure's key and position; restoring puts
    /// the default back.
    /// </summary>
    public sealed class CharacterLookTargetsTests
    {
        sealed class Spy : ICharacterLookTargetSource
        {
            public string Key;
            public Vector3 From;
            public Vector3 Answer = new Vector3(7f, 8f, 0f);

            public bool TryGetLookTarget(string figureKey, Vector3 figureWorldPosition, out Vector3 targetWorldPosition)
            {
                Key = figureKey;
                From = figureWorldPosition;
                targetWorldPosition = Answer;
                return true;
            }
        }

        GameObject _player;

        [SetUp]
        public void SetUp()
        {
            CharacterLookTargets.Restore();
            GameServices.PlayerTransform = null;
        }

        [TearDown]
        public void TearDown()
        {
            CharacterLookTargets.Restore();
            GameServices.PlayerTransform = null;
            if (_player != null) Object.DestroyImmediate(_player);
        }

        Transform Player(Vector3 at)
        {
            _player = new GameObject("Player");
            _player.transform.position = at;
            return _player.transform;
        }

        [Test]
        public void WithNoPlayerPublishedTheDefaultSeesNothing()
        {
            Assert.IsFalse(CharacterLookTargets.TryGet("npc.skipper", Vector3.zero, out Vector3 target));
            Assert.AreEqual(default(Vector3), target);
        }

        [Test]
        public void TheDefaultLooksAtThePublishedPlayer()
        {
            GameServices.PlayerTransform = Player(new Vector3(3f, 4f, 0f));
            Assert.IsTrue(CharacterLookTargets.TryGet("npc.skipper", new Vector3(1f, 1f, 0f), out Vector3 target));
            Assert.AreEqual(new Vector3(3f, 4f, 0f), target);
        }

        [Test]
        public void ADestroyedPlayerIsNothingToLookAt()
        {
            GameServices.PlayerTransform = Player(new Vector3(3f, 4f, 0f));
            Object.DestroyImmediate(_player);
            Assert.IsFalse(CharacterLookTargets.TryGet("npc.skipper", Vector3.zero, out _),
                "A figure looked at a destroyed player.");
        }

        [Test]
        public void AnInstalledSourceIsAskedWithTheFiguresKeyAndPosition()
        {
            var spy = new Spy();
            CharacterLookTargets.Source = spy;
            Assert.AreSame(spy, CharacterLookTargets.Source);
            Assert.IsTrue(CharacterLookTargets.TryGet("npc.villager_7", new Vector3(-2f, 5f, 0f), out Vector3 target));
            Assert.AreEqual("npc.villager_7", spy.Key);
            Assert.AreEqual(new Vector3(-2f, 5f, 0f), spy.From);
            Assert.AreEqual(spy.Answer, target);
        }

        [Test]
        public void RestoringOrClearingTheSourcePutsThePlayerBack()
        {
            GameServices.PlayerTransform = Player(new Vector3(3f, 4f, 0f));
            var spy = new Spy();

            CharacterLookTargets.Source = spy;
            CharacterLookTargets.Restore();
            Assert.AreNotSame(spy, CharacterLookTargets.Source);
            Assert.IsTrue(CharacterLookTargets.TryGet("npc.skipper", Vector3.zero, out Vector3 target));
            Assert.AreEqual(new Vector3(3f, 4f, 0f), target);
            Assert.IsNull(spy.Key, "The restored seam still asked the installed source.");

            CharacterLookTargets.Source = spy;
            CharacterLookTargets.Source = null;
            Assert.IsNotNull(CharacterLookTargets.Source, "Clearing the source left the seam with none.");
            Assert.IsTrue(CharacterLookTargets.TryGet("npc.skipper", Vector3.zero, out target));
            Assert.AreEqual(new Vector3(3f, 4f, 0f), target);
            Assert.IsNull(spy.Key);
        }
    }
}
