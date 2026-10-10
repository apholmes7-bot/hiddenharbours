using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The fight on the wharf is the sprite's</b> (the owner's ruling of 2026-10-08 on the held-props
    /// report, decisions 5 and 6). The fishing animator claims her skin for the fight
    /// (<see cref="IsoCharacterSprite.Suspend"/>, in <c>PlayerFishingAnimator.BeginOwnership</c>) and writes
    /// the fight's cells onto her body sprite: the very renderer her mesh holds off while it stands in for
    /// her ashore. The mesh has no pose for any of those cells, so a claimed skin hands the draw back to the
    /// sprite, ashore in the pure rule (<see cref="DeckRiderMeshPresenter.WhyNotShownAshore"/>) and aboard
    /// on the presenter's own call, where the rider's child, the mirror of that body, keeps drawing. Each
    /// reason is written out here as a literal and never read back from the code, as in
    /// <c>DeckRiderAshoreHandOverTests</c>. This fixture gives her no skin, so aboard the fence after the
    /// claim is the skin's; the frames themselves (the dock fight, before and after) are a plate's.
    /// </summary>
    public class DeckRiderSuspendedSkinTests
    {
        private const string SuspendedAshore =
            "ashore — her skin is suspended (the fight, the haul), and the sprite shows what it is made to show";
        private const string SuspendedAboard =
            "her skin is suspended (the fight, the haul, a clip), and the sprite shows what it is made to show";
        private const string NoSkin = "no CharacterSkinDef — none set here and none on the art def";

        private GameConfig _previousConfig;
        private GameConfig _config;
        private GameObject _player;
        private IsoCharacterSprite _isoSkin;
        private DeckRiderMeshPresenter _presenter;
        private Stand _stand;

        /// <summary>Where she stands; before the claim the gate reads her skin and nothing else.</summary>
        private sealed class Stand : ICharacterFigureStand
        {
            public IsoCharacterSprite Character;

            public IsoCharacterSprite FigureCharacter => Character;
            public Transform FigureHull => null;
            public Vector3 FigureStandRigMetres => Vector3.zero;
            public float FigureDeckBearingDegrees => 0f;
        }

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.MeshCharacter = true;          // both switches on, as the shipped asset has them
            _config.MeshCharacterAshore = true;
            GameServices.Config = _config;

            _player = new GameObject("TestPlayer");
            _isoSkin = _player.AddComponent<IsoCharacterSprite>();         // [RequireComponent] adds her body sprite
            _presenter = _player.AddComponent<DeckRiderMeshPresenter>();   // [RequireComponent] adds her rider
            _stand = new Stand { Character = _isoSkin };
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = _previousConfig;
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_config);
        }

        /// <summary>The fight's frame ashore: a live, wired rider, her body shown on her own feet, no clip
        /// playing, dry ground. Every fence but the claim is clear.</summary>
        private string Ashore() => DeckRiderMeshPresenter.WhyNotShownAshore(
            riderLive: true, hasRiderChild: true, hasBody: true, bodyShownOnRoot: true,
            clipPlaying: false, water: OnFootWaterState.Dry, skinSuspended: _isoSkin.IsSuspended);

        /// <summary>The fight's frame on a deck: the presenter's own call, and why it did not draw.</summary>
        private string Aboard()
        {
            _presenter.PoseFigure(_stand, aboard: true);
            return _presenter.NotDrawingReason;
        }

        [Test]
        public void MeshFigure_HandsBack_WhenTheSkinIsSuspended()
        {
            Assert.IsNull(Ashore(), "harness: nothing has claimed her skin and every fence is clear: she is shown ashore");
            Assert.AreEqual(NoSkin, Aboard(), "harness: nothing has claimed her skin, so the aboard gate goes on to its skin");

            _isoSkin.Suspend();   // the fight's claim, as PlayerFishingAnimator.BeginOwnership makes it

            Assert.AreEqual(SuspendedAshore, Ashore(),
                            "ashore, no clip, dry: the fight writes her body sprite, so the sprite draws and her mesh does not");
            Assert.AreEqual(SuspendedAboard, Aboard(),
                            "aboard: the claim stops the gate, and the rider's mirror of her body draws the fight");
            Assert.IsFalse(_presenter.DrawsInsteadOfSprite, "aboard: the mesh does not take the draw from the fight");
        }

        [Test]
        public void MeshFigure_Returns_WhenTheSkinResumes()
        {
            _isoSkin.Suspend();   // the fight
            _isoSkin.Suspend();   // a second claimant over it: claims nest
            _isoSkin.Release();

            Assert.AreEqual(SuspendedAshore, Ashore(), "one claim given back, one still held: ashore, still the sprite");
            Assert.AreEqual(SuspendedAboard, Aboard(), "one claim given back, one still held: aboard, still the sprite");

            _isoSkin.Release();   // the last claim given back, as PlayerFishingAnimator.EndPose gives it

            Assert.IsNull(Ashore(), "every claim given back: she is shown ashore again, and her mesh takes the draw back");
            Assert.AreEqual(NoSkin, Aboard(), "every claim given back: the aboard gate goes on past the claim to its skin");
        }
    }
}
