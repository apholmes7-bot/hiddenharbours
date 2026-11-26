using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Four changes that landed together each name the player at this machine with their own constant:
    /// the interaction actor, the v27 save rows, the rod skill book and the wardrobe. A later change wires
    /// the session books to the saved rows by that key, so the four must stay the same string. If one
    /// moves, saved clothes, skill or rod fittings would load under nobody.
    /// </summary>
    public class LocalPlayerKeyAgreementTests
    {
        [Test]
        public void EveryLocalPlayerKey_IsTheSaveKey()
        {
            Assert.AreEqual(SavePlayerKeys.Local, InteractActor.LocalPlayerKey, "interaction actor");
            Assert.AreEqual(SavePlayerKeys.Local, RodSkillBook.LocalPlayerKey, "rod skill book");
            Assert.AreEqual(SavePlayerKeys.Local, ClothingPlayers.Local, "wardrobe");
        }

        [Test]
        public void ADefaultActor_IsTheSavesOwnPlayer()
        {
            var actor = new InteractActor(UnityEngine.Vector2.zero, UnityEngine.Vector2.up, InteractContext.OnFoot);
            Assert.AreEqual(SavePlayerKeys.Local, SavePlayerKeys.Normalise(actor.PlayerKey));
        }
    }
}
