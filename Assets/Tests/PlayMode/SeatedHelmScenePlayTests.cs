using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>The actual St Peters picker, board/helm presses, camera and facet pipeline.</summary>
    public class SeatedHelmScenePlayTests
    {
        WharfNightStage _stage;
        bool _intro;
        readonly HashSet<GameObject> _before = new();
        static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Evidence~", "rig-facts", "production", "native");

        static GameObject[] PersistentRoots()
        {
            var probe = new GameObject("SeatedHelmRootProbe");
            Object.DontDestroyOnLoad(probe);
            var scene = probe.scene;
            Object.DestroyImmediate(probe);
            return scene.GetRootGameObjects();
        }

        [UnitySetUp] public IEnumerator Snapshot()
        {
            foreach (var root in PersistentRoots()) _before.Add(root);
            yield return null;
        }

        [UnityTearDown] public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            if (_stage != null) yield return _stage.TearDown();
            foreach (var root in PersistentRoots())
                if (root != null && !_before.Contains(root)) Object.DestroyImmediate(root);
            GameServices.Reset(); ShellFlow.Reset(); InteractionGate.Reset();
            GameServices.OpeningCinematicRunning = _intro;
            _stage = null;
            _before.Clear();
        }

        [UnityTest] public IEnumerator GoFast_RealHelm_EightHeadings() => Plate("GoFast");
        [UnityTest] public IEnumerator Bowrider_RealHelm_EightHeadings() => Plate("Bowrider");
        [UnityTest] public IEnumerator ExpressCruiser_RealHelm_EightHeadings() => Plate("ExpressCruiser");

        IEnumerator Plate(string file)
        {
            WharfNightStage.RequireAGraphicsDevice();
#if UNITY_EDITOR
            Directory.CreateDirectory(Output);
            _intro = GameServices.OpeningCinematicRunning;
            _stage = new WharfNightStage("StPeters", "SeatedHelmScene");
            yield return _stage.Load();
            ShellFlow.Reset(); InteractionGate.Reset(); GameServices.OpeningCinematicRunning = false;
            yield return _stage.SetNight(12f);
            var picker = Object.FindFirstObjectByType<DevBoatPicker>();
            var sw = Object.FindFirstObjectByType<ControlSwitcher>();
            var player = Object.FindFirstObjectByType<PlayerWalkController>();
            Assert.IsNotNull(picker); Assert.IsNotNull(sw); Assert.IsNotNull(player);
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/" + file + ".asset");
            bool found = false;
            for (int i = 0; i < picker.RosterCount; i++)
            {
                if (picker.Current == hull) { found = true; break; }
                picker.Next();
            }
            Assert.IsTrue(found, "Actual scene roster contains " + file);
            yield return null;
            var boat = picker.GetComponent<BoatController>();
            RepairLedger.MarkRepaired(GameServices.Save?.Current, hull.Id);
            player.transform.position = boat.transform.position + boat.transform.right;
            bool boarded = sw.BeginInteract();
            if (!boarded)
            {
                // Only setup positions the on-foot player; the real dispatcher still decides boarding.
                for (int side = -1; side <= 1 && !boarded; side += 2)
                    for (float y = -3; y <= 3 && !boarded; y += .5f)
                    {
                        player.transform.position = boat.transform.position + boat.transform.right * side + boat.transform.up * y;
                        if (sw.WithinBoardReach() && sw.BoardableNow()) boarded = sw.BeginInteract();
                    }
            }
            Assert.IsTrue(boarded, "The scene's boarding press must succeed");
            for (int i = 0; i < 300 && sw.IsBoardingMove; i++) yield return null;
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            player.transform.position = sw.HelmWorldPosition;
            Assert.IsTrue(sw.TakesTheHelmOnThisPress(), "The real helm press is offered");
            Assert.IsTrue(sw.BeginInteract());
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            yield return null;
            var rider = player.GetComponent<DeckRiderVisual>();
            var presenter = player.GetComponent<DeckRiderMeshPresenter>();
            Assert.IsNotNull(rider); Assert.IsNotNull(presenter);
            var input = boat.GetComponent<DevBoatInput>(); if (input != null) input.enabled = false;
            boat.Stop(); boat.enabled = false;
            yield return _stage.FrameOn(player.transform.position);
            var follow = Object.FindFirstObjectByType<CameraFollow>(); if (follow != null) follow.enabled = false;
            var csv = new StringBuilder("hull,heading,state,origin_x,origin_y,origin_z,mesh_drawn\n");
            for (int heading = 0; heading < 8; heading++)
            {
                boat.transform.rotation = Quaternion.Euler(0,0,-heading*45f);
                for (int i = 0; i < 6; i++) yield return null;
                Assert.AreEqual(hull.Visual.SeatedHelmState, presenter.DrawnStateKey,
                    presenter.NotDrawingReason);
                Assert.IsTrue(presenter.DrawsInsteadOfSprite, presenter.NotDrawingReason);
                Assert.Less(Vector3.Distance(hull.Visual.SeatedHelmFloorOrigin, rider.DeckStandRigLocal), .0001f);
                var p = rider.DeckStandRigLocal;
                csv.Append(hull.Id).Append(',').Append(heading*45).Append(',').Append(presenter.DrawnStateKey)
                    .Append(',').Append(p.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture))
                    .Append(',').Append(p.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture))
                    .Append(',').Append(p.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(",true\n");
                // Zoom the scene's own camera for a readable helm plate; no stand-in hull or figure.
                var cam = _stage.Camera;
                cam.orthographicSize = 2.8f;
                cam.transform.position = new Vector3(player.transform.position.x,player.transform.position.y+.7f,cam.transform.position.z);
                for (int i = 0; i < 3; i++) yield return null;
                var bytes = _stage.Capture();
                var texture = new Texture2D(_stage.Width,_stage.Height,TextureFormat.RGBA32,false);
                texture.LoadRawTextureData(bytes); texture.Apply();
                File.WriteAllBytes(Path.Combine(Output,file+"-"+heading+".png"),texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            File.WriteAllText(Path.Combine(Output,file+"-headings.csv"),csv.ToString());
            // Leaving the helm selects the ordinary deck route again.
            Assert.IsTrue(sw.BeginInteract());
            Assert.AreEqual(ControlMode.OnDeck,sw.Mode);
            Assert.IsNull(rider.SeatedHelmStateKey);
#else
            Assert.Ignore("Requires the actual editor-authored St Peters scene.");
            yield break;
#endif
        }
    }
}
