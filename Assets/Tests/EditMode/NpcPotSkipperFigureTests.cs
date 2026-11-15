using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class NpcPotSkipperFigureTests
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        readonly List<Object> _objects = new();
        ICharacterFigurePresentationService _saved;
        NpcPotDayDef _day;
        BoatOwnerDef _owner;
        GameObject _root;

        sealed class RecordingService : ICharacterFigurePresentationService
        {
            public ICharacterFigureStand Stand;
            public GameObject Host;
            public ICharacterFigure Attach(GameObject host, ICharacterFigureStand stand)
            {
                Host = host;
                Stand = stand;
                return null;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _saved = CharacterFigurePresentation.Service;
            _day = AssetDatabase.LoadAssetAtPath<NpcPotDayDef>("Assets/_Project/Data/Traps/NmcDoironYvettePotDay.asset");
            Assert.IsNotNull(_day);
            _owner = AssetDatabase.FindAssets("t:BoatOwnerDef", new[] { "Assets/_Project/Data/Boats/Owners" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(AssetDatabase.GUIDToAssetPath(guid)))
                .Single(owner => owner.Id == _day.OwnerId);
            _root = new GameObject("PotSkipperFigureTest");
            _objects.Add(_root);
        }

        [TearDown]
        public void TearDown()
        {
            CharacterFigurePresentation.Service = _saved;
            foreach (var obj in _objects) if (obj != null) Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void Landing_AttachesTheOwnersDataSkinThroughTheCoreAshoreStand()
        {
            var service = new RecordingService();
            CharacterFigurePresentation.Service = service;
            var walk = new NpcPotLandingWalk(_day, _owner.Skipper, null, null, _root.transform, 0f);
            Assert.AreSame(walk, service.Stand);
            Assert.AreSame(walk.FigureCharacter.gameObject, service.Host);
            Assert.IsNull(walk.FigureHull);
            Assert.AreEqual(_owner.Id, walk.FigureKey);
            AssertSkin(walk.FigureCharacter);
            Assert.IsFalse(walk.TryGetFigureClip(out _, out _), "no ladder clip before landing");
        }

        [Test]
        public void Deck_AttachesTheSameDataSkinThroughTheMooredStand()
        {
            var service = new RecordingService();
            CharacterFigurePresentation.Service = service;
            var hull = _root.AddComponent<MooredBoat>();
            hull.Configure(_owner, _day.BerthHeadingDegrees);
            // Exercise the production skipper builder without installing a hull or any render pipeline.
            typeof(MooredBoat).GetField("_rig", Private).SetValue(hull,
                new BoatHullSkinner.Rig { Visual = _root.transform });
            typeof(MooredBoat).GetMethod("StandTheSkipper", Private).Invoke(hull, null);
            var materialField = typeof(MooredBoat).GetField("_skipperMaterial", Private);
            var material = materialField.GetValue(hull) as Material;
            if (material != null) _objects.Add(material);
            materialField.SetValue(hull, null); // EditMode owns immediate cleanup.
            Assert.AreSame(hull, service.Stand);
            Assert.AreSame(service.Stand.FigureCharacter.gameObject, service.Host);
            AssertSkin(service.Stand.FigureCharacter);
            Assert.AreEqual(_owner.Id, ((ICharacterFigureIdentity)service.Stand).FigureKey);
        }

        [Test]
        public void Landing_WithoutASkin_KeepsTheSpriteAndAddsNoMeshPresenter()
        {
            CharacterFigurePresentation.Service = new CharacterFigurePresentationService();
            var visual = Object.Instantiate(_owner.Skipper);
            _objects.Add(visual);
            visual.Skin = null;
            var walk = new NpcPotLandingWalk(_day, visual, null, null, _root.transform, 0f);
            Assert.IsNotNull(walk.FigureCharacter.GetComponent<SpriteRenderer>());
            Assert.IsNull(walk.FigureCharacter.GetComponent<CharacterFigurePresenter>());
            Assert.IsTrue(visual.HasAnyArt(), "the original sprite sheets remain available");
        }

        void AssertSkin(IsoCharacterSprite character)
        {
            Assert.AreSame(_owner.Skipper, character.Visual);
            CharacterSkinDef skin = character.Visual.Skin;
            Assert.IsNotNull(skin);
            Assert.IsTrue(skin.IsUsable());
            foreach (string state in new[] { CharacterSkinStateMap.Idle, CharacterSkinStateMap.Walk, CharacterClipStateMap.LadderDown })
            {
                Assert.IsTrue(skin.TryGetClip(state, out var clip), state);
                Assert.IsTrue(clip.KeysWellFormed(skin.BoneCount), state);
                Assert.IsTrue(skin.DrawsAsMesh(state), state);
            }
        }
    }
}
