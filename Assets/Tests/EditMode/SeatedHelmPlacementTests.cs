using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class SeatedHelmPlacementTests
    {
        readonly List<Object> _made = new();
        GameConfig _previous, _config;
        DeckRiderVisual _rider;
        BoatController _boat;
        BoatHullDef _hull;
        T Keep<T>(T value) where T : Object { _made.Add(value); return value; }
        static void Set(object o,string n,object v) => o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
        Vector3 Stand() => (Vector3)typeof(DeckRiderVisual).GetMethod("StandPointRigLocal",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_rider,null);

        [SetUp] public void Setup()
        {
            _previous=GameServices.Config;
            _config=Keep(ScriptableObject.CreateInstance<GameConfig>());_config.MeshCharacter=true;GameServices.Config=_config;
            _hull=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/GoFast.asset")));
            _hull.Visual=Keep(Object.Instantiate(_hull.Visual));
            _boat=Keep(new GameObject("SeatedPlacementBoat")).AddComponent<BoatController>();_boat.SetHull(_hull);_boat.enabled=false;
            var player=Keep(new GameObject("SeatedPlacementPlayer"));player.AddComponent<SpriteRenderer>();
            var character=player.AddComponent<IsoCharacterSprite>();
            character.Configure(AssetDatabase.LoadAssetAtPath<CharacterVisualDef>("Assets/_Project/Data/Characters/FisherIso.asset"));
            _rider=player.AddComponent<DeckRiderVisual>();_rider.enabled=false;
            Set(_rider,"_boat",_boat);Set(_rider,"_character",character);Set(_rider,"_mode",ControlMode.Aboard);
        }
        [TearDown] public void Teardown()
        {
            GameServices.Config=_previous;
            for(int i=_made.Count-1;i>=0;i--)if(_made[i]!=null)Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }
        [Test] public void CalibratedGoFast_SelectsItsFloorOriginAndStateTogether()
        {
            Assert.AreEqual(new Vector3(.42f,-2.18f,.62f),Stand());
            Assert.AreEqual("drive_helm_go_fast",_rider.SeatedHelmStateKey);
            Assert.AreEqual(new Vector3(.42f,-1.86f,.62f),_hull.Visual.Interior.Anchors[0].Id=="helm"
                ?_hull.Visual.Interior.Anchors[0].ReachPoint:System.Array.Find(_hull.Visual.Interior.Anchors,a=>a.Id=="helm").ReachPoint);
        }
        [Test] public void DisabledMeshOrMissingClip_KeepsTheStandingAnchor()
        {
            _config.MeshCharacter=false;
            Assert.AreEqual(new Vector3(.42f,-1.86f,.62f),Stand());Assert.IsNull(_rider.SeatedHelmStateKey);
            _config.MeshCharacter=true;_hull.Visual.SeatedHelmState="absent_pose";
            Assert.AreEqual(new Vector3(.42f,-1.86f,.62f),Stand());Assert.IsNull(_rider.SeatedHelmStateKey);
        }
        [Test] public void OnDeckAndCarried_ReleaseTheHelmState()
        {
            Set(_rider,"_mode",ControlMode.OnDeck);Assert.IsNull(_rider.SeatedHelmStateKey);
            Set(_rider,"_mode",ControlMode.Aboard);Set(_rider,"_carried",true);Set(_rider,"_carriedStand",new Vector3(2,3,4));
            Assert.AreEqual(new Vector3(2,3,4),Stand());Assert.IsNull(_rider.SeatedHelmStateKey);
        }
        [Test] public void AnExplicitPresenterSkinWithoutTheState_KeepsTheStandingOrigin()
        {
            var presenter = _rider.gameObject.AddComponent<DeckRiderMeshPresenter>();
            // EditMode does not invoke the presenter's PlayMode registration callback.
            _rider.SetFigureOverride(presenter);
            var skin = Keep(ScriptableObject.CreateInstance<CharacterSkinDef>());
            presenter.Configure(skin);
            Assert.AreSame(skin, presenter.EffectiveSkin);
            Assert.AreEqual(new Vector3(.42f,-1.86f,.62f),Stand());
            Assert.IsNull(_rider.SeatedHelmStateKey);
        }
    }
}
